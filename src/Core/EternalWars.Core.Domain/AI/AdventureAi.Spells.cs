using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The adventure spells the computer casts (ADR-184), each through <see cref="CastAdventureSpellCommand"/> like a player: Town Portal to a
/// threatened town or to troops waiting far away, Dimension Door to shorten a long route, Pathfinder's Call and Ashwalk when today's route
/// crosses enough of their terrain, View Terrain over unexplored ground, Grave Whisper for a necromancer, Veil on the main hero once an enemy
/// hero is in sight. The spells that only show something (Distant Vision, Detect Magic, Ledger of Ages, Eavesdrop) are not cast: the AI reads
/// what its player knows from the state (ADR-125), so they would tell it nothing new.
/// </summary>
public sealed partial class AdventureAi
{
    /// <summary>Movement a Dimension Door must save, beyond the 500 the cast costs, to be worth it.</summary>
    private const int DoorSaving = 300;

    /// <summary>How often a turn the AI weighs a Dimension Door for one hero (each weighing prices up to 72 landings).</summary>
    private const int DoorChecksPerHero = 3;

    /// <summary>View Terrain is cast when at least this share of the ground in its radius is unexplored.</summary>
    private const double ViewTerrainShare = 0.3;

    /// <summary>Pathfinder's Call or Ashwalk must save this much movement today beyond what the cast costs.</summary>
    private const int NativeWalkSaving = 100;

    private readonly Dictionary<HeroId, int> _doorChecks = [];

    /// <summary>Whether the hero could cast the spell now (before picking any cell).</summary>
    private bool CanCast(Hero hero, string spell) => AdventureMagic.Refusal(_state, hero, spell, null, aimed: false) is null;

    /// <summary>
    /// Whether the hero can spare the mana for a convenience spell: what is left still pays for its dearest battle spell (a fight may come
    /// today).
    /// </summary>
    private bool Spares(Hero hero, string spell)
    {
        var keep = MagicRules.Known(hero, _state.Spells).Where(k => !k.Spell.IsAdventure)
            .Select(k => ManaRules.SpellCost(hero, k.Spell.Mana)).DefaultIfEmpty(0).Max();
        return hero.Mana - AdventureMagic.Cost(_state, hero, spell) >= keep;
    }

    private bool Cast(PlayerId player, Hero hero, string spell, MapPosition? target = null) =>
        Execute(new CastAdventureSpellCommand(player, hero.Id, spell, target)).IsAccepted;

    /// <summary>The spells cast before the hero picks where to go: they change what it knows (View Terrain) or last past today.</summary>
    private void CastBeforeMoving(PlayerId player, Hero hero)
    {
        if (Preparing || hero.Army.IsEmpty)
        {
            return;
        }

        // Grave Whisper: the next victory raises more undead; it lasts until then or the week's end.
        if (hero.SkillLevelOf(NecromancyRules.Skill) > SkillLevel.None && AdventureMagic.NecromancyPercent(hero) == 0
            && CanCast(hero, AdventureMagic.GraveWhisper) && Spares(hero, AdventureMagic.GraveWhisper))
        {
            Cast(player, hero, AdventureMagic.GraveWhisper);
        }

        // Veil: once an enemy hero is in sight, the main army's counts are hidden from it for the week.
        if (Active(player) && hero.Id == _main && AdventureMagic.VeilRank(hero) == 0 && Threats(player).Count > 0
            && CanCast(hero, AdventureMagic.Veil) && Spares(hero, AdventureMagic.Veil))
        {
            Cast(player, hero, AdventureMagic.Veil);
        }

        if (Active(player) && CanCast(hero, AdventureMagic.ViewTerrain) && Spares(hero, AdventureMagic.ViewTerrain)
            && UnexploredShare(hero.Owner, hero.Position, AdventureMagic.ViewTerrainRadius(AdventureMagic.RankOf(_state, hero, AdventureMagic.ViewTerrain))) >= ViewTerrainShare)
        {
            Cast(player, hero, AdventureMagic.ViewTerrain);
        }
    }

    /// <summary>The share of the map's cells within <paramref name="radius"/> of <paramref name="center"/> that the player has not explored.</summary>
    private double UnexploredShare(PlayerId owner, MapPosition center, int radius)
    {
        var (all, unexplored) = (0, 0);
        for (var y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (var x = center.X - radius; x <= center.X + radius; x++)
            {
                var cell = new MapPosition(x, y, center.Level);
                if (_state.Map.Contains(cell))
                {
                    all++;
                    unexplored += _state.Fog.IsExplored(owner, cell) ? 0 : 1;
                }
            }
        }

        return all == 0 ? 0 : (double)unexplored / all;
    }

    /// <summary>
    /// The spells cast on the way to <paramref name="target"/>: Pathfinder's Call or Ashwalk when today's route crosses enough of their terrain,
    /// then a Dimension Door when it shortens a route the hero cannot finish today. True when the hero moved (the target is still the goal).
    /// </summary>
    private bool CastForRoute(PlayerId player, Hero hero, MapPosition target)
    {
        if (Preparing)
        {
            return false;
        }

        foreach (var spell in new[] { AdventureMagic.PathfindersCall, AdventureMagic.Ashwalk })
        {
            if (Active(player) && !hero.Statuses.Any(s => s.Source == spell) && CanCast(hero, spell) && Spares(hero, spell)
                && session.Movement.FindPath(hero.Id, target) is { } path)
            {
                var rank = AdventureMagic.RankOf(_state, hero, spell);
                var extra = rank >= 3 ? hero.MaxMovementPoints * AdventureMagic.NativeWalkMovementPercent / 100 : 0;
                if (NativeSaving(hero, path, AdventureMagic.NativeTerrains(spell, rank)) + extra >= AdventureMagic.MovementCost + NativeWalkSaving)
                {
                    Cast(player, hero, spell);
                }
            }
        }

        return Active(player) && DoorTowards(player, hero, target);
    }

    /// <summary>
    /// The movement today's part of the route would save with <paramref name="terrains"/> native: each step that leaves such a tile off a road
    /// would cost the native price (rivers and diagonals included).
    /// </summary>
    private int NativeSaving(Hero hero, HeroPath path, IReadOnlySet<TerrainType> terrains)
    {
        var map = _state.Map;
        var saving = 0;
        var from = hero.Position;
        foreach (var step in path.Steps)
        {
            if (step.Day > 0)
            {
                break;
            }

            if (step.Kind == PathStepKind.Walk && terrains.Contains(map.GetTerrain(from))
                && !(map.GetRoad(from) != RoadType.None && map.GetRoad(step.Position) != RoadType.None))
            {
                var native = map.GetRiver(from) != RiverType.None ? RiverRules.WithPenalty(TerrainCatalog.NativeCost) : TerrainCatalog.NativeCost;
                var diagonal = from.X != step.Position.X && from.Y != step.Position.Y;
                saving += Math.Max(0, step.Cost - (diagonal ? (int)Math.Round(native * HeroMovementService.DiagonalMultiplier) : native));
            }

            from = step.Position;
        }

        return saving;
    }

    /// <summary>
    /// Dimension Door towards <paramref name="target"/> when the hero cannot walk there today: the landing (a straight line in one of the eight
    /// directions, safe from enemy heroes too strong for it) whose route to the target is cheapest, if it saves <see cref="DoorSaving"/> beyond
    /// the cast's 500 movement points.
    /// </summary>
    private bool DoorTowards(PlayerId player, Hero hero, MapPosition target)
    {
        if (!CanCast(hero, AdventureMagic.DimensionDoor) || _doorChecks.GetValueOrDefault(hero.Id) >= DoorChecksPerHero)
        {
            return false;
        }

        _doorChecks[hero.Id] = _doorChecks.GetValueOrDefault(hero.Id) + 1;
        var walk = WalkCost(hero, target);
        if (walk is { } cost && cost <= hero.MovementPoints)
        {
            return false;
        }

        var range = AdventureMagic.DimensionDoorRange(AdventureMagic.RankOf(_state, hero, AdventureMagic.DimensionDoor));
        var bestCost = walk is { } w ? w - AdventureMagic.MovementCost - DoorSaving : int.MaxValue;
        MapPosition? best = null;
        PathFinder.ReverseCostMap? fromLandings = null;
        foreach (var direction in DirectionExtensions.All)
        {
            var landing = hero.Position;
            for (var distance = 1; distance <= range; distance++)
            {
                landing = landing.Step(direction);
                if (AdventureMagic.Refusal(_state, hero, AdventureMagic.DimensionDoor, landing) is not null
                    || !Holds(ArmyPower(hero.Army), Danger(hero.Owner, landing)))
                {
                    continue;
                }

                fromLandings ??= CostsTo(hero, target);
                if (fromLandings.CostFrom(landing) is { } rest && rest < bestCost)
                {
                    (best, bestCost) = (landing, rest);
                }
            }
        }

        return best is { } door && Cast(player, hero, AdventureMagic.DimensionDoor, door);
    }

    /// <summary>
    /// Town Portal (ADR-120, ADR-184), to the town the hero would go to (from rank II any own town, else the nearest): one threatened by an enemy
    /// the garrison alone cannot hold but the two together can, or one whose waiting troops call the hero home (<see cref="TownNeed"/>) when the
    /// walk there takes more than a day. Not when the hero could walk to a threatened town today.
    /// </summary>
    private bool PortalHome(PlayerId player, Hero hero)
    {
        if (Preparing || !CanCast(hero, AdventureMagic.TownPortal))
        {
            return false;
        }

        var rank = AdventureMagic.RankOf(_state, hero, AdventureMagic.TownPortal);
        var towns = rank >= 2
            ? _state.ObjectsOf<Town>().Where(t => AdventureMagic.PortalTown(_state, hero, t.VisitPosition) == t).ToList()
            : AdventureMagic.PortalTown(_state, hero) is { } nearest ? [nearest] : [];
        foreach (var town in towns.OrderByDescending(t => TownNeed(hero, t)).ThenBy(t => t.Id.Value))
        {
            var need = TownNeed(hero, town);
            if (need <= 0)
            {
                continue;
            }

            var danger = Danger(player, town.VisitPosition);
            var walk = WalkCost(hero, town.VisitPosition);
            var worth = danger > 0
                ? walk is not { } cost || cost > hero.MovementPoints
                : walk is not { } far || far > hero.MaxMovementPoints;
            if (worth)
            {
                return Cast(player, hero, AdventureMagic.TownPortal, rank >= 2 ? town.VisitPosition : null);
            }
        }

        return false;
    }
}
