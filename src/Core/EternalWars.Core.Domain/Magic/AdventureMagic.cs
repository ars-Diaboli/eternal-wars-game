using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Magic;

/// <summary>
/// Adventure spells (ADR-071, ADR-073, ADR-120; [spells.md](../content/spells.md#adventure-spells)): cast from the spell book on the map for the
/// spell's mana and a fixed <see cref="MovementCost"/> movement points; a hero with fewer left cannot cast. All eleven are built, with their
/// ranks (the per-rank numbers are the constants and functions below, not columns of spells.csv): Town Portal, Dimension Door, View Terrain,
/// Pathfinder's Call, Ashwalk, and the spells that show something (Distant Vision, Detect Magic, Grave Whisper, Ledger of Ages, Veil, Eavesdrop),
/// whose events carry what they show (<see cref="AdventureIntel"/>).
/// </summary>
public static class AdventureMagic
{
    /// <summary>Movement points every adventure spell costs besides its mana (ADR-073, MAG-5).</summary>
    public const int MovementCost = 500;

    public const string TownPortal = "arcana.town-portal";
    public const string DimensionDoor = "shadow.dimension-door";
    public const string ViewTerrain = "nature.view-terrain";
    public const string PathfindersCall = "nature.pathfinders-call";
    public const string Ashwalk = "chaos.ashwalk";
    public const string DistantVision = "light.distant-vision";
    public const string DetectMagic = "arcana.detect-magic";
    public const string GraveWhisper = "death.grave-whisper";
    public const string LedgerOfAges = "death.ledger-of-ages";
    public const string Veil = "shadow.veil";
    public const string Eavesdrop = "shadow.eavesdrop";

    /// <summary>The radius around the hero in which Grave Whisper III shows the neutral stacks' numbers.</summary>
    public const int GraveWhisperRadius = 10;

    /// <summary>Veil III: the hero is hidden on the map from other players unless one of their heroes is this close (cells, any direction).</summary>
    public const int VeilSightRange = 3;

    /// <summary>Pathfinder's Call III and Ashwalk III add this share of the day's movement allowance, in percent.</summary>
    public const int NativeWalkMovementPercent = 10;

    /// <summary>The adventure spells that can be cast now: all of them.</summary>
    public static IReadOnlySet<string> Built { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        TownPortal, DimensionDoor, ViewTerrain, PathfindersCall, Ashwalk, DistantVision, DetectMagic, GraveWhisper, LedgerOfAges, Veil, Eavesdrop,
    };

    private static int AtRank(int rank, int one, int two, int three) => rank >= 3 ? three : rank == 2 ? two : one;

    /// <summary>How far Dimension Door goes in a straight line: 6 cells at rank I (ADR-072), 9 from rank II.</summary>
    public static int DimensionDoorRange(int rank) => AtRank(rank, 6, 9, 9);

    /// <summary>How often a day Dimension Door can be cast: once, twice at rank III.</summary>
    public static int DimensionDoorCasts(int rank) => AtRank(rank, 1, 1, 2);

    /// <summary>View Terrain's radius: 12, 18 from rank II (rank III's "stays explored" is how every rank works, ADR-120).</summary>
    public static int ViewTerrainRadius(int rank) => AtRank(rank, 12, 18, 18);

    /// <summary>Distant Vision's radius: 10, 14 from rank II; rank III also sees under the fog.</summary>
    public static int DistantVisionRadius(int rank) => AtRank(rank, 10, 14, 14);

    /// <summary>Detect Magic's radius: 10, 15 from rank II; rank III also shows creature banks' rewards.</summary>
    public static int DetectMagicRadius(int rank) => AtRank(rank, 10, 15, 15);

    /// <summary>What Grave Whisper adds to the Necromancy share, in percent: 10, 15 from rank II.</summary>
    public static int GraveWhisperPercent(int rank) => AtRank(rank, 10, 15, 15);

    /// <summary>The rank the hero casts the spell at (1–3); 1 when it does not know it.</summary>
    public static int RankOf(GameState state, Hero hero, string spell) =>
        Math.Max(1, MagicRules.Known(hero, state.Spells).Where(k => k.Spell.Id == spell).Select(k => k.Rank).DefaultIfEmpty(1).Max());

    /// <summary>
    /// Whether the cast needs a cell picked on the map: Dimension Door (the tile), Distant Vision (the cell looked at), Eavesdrop (a cell of the
    /// town) and Town Portal from rank II (a cell of the own town to go to; without one it takes the nearest).
    /// </summary>
    public static bool NeedsTarget(GameState state, Hero hero, string spell) => spell switch
    {
        DimensionDoor or DistantVision or Eavesdrop => true,
        TownPortal => RankOf(state, hero, spell) >= 2,
        _ => false,
    };

    /// <summary>The terrains a spell makes native for the rest of the day at rank I (Pathfinder's Call: forest floor, grass, swamp; Ashwalk: lava, wasteland, cursed lands).</summary>
    public static IReadOnlySet<TerrainType> NativeTerrains(string spell) => spell switch
    {
        PathfindersCall => new HashSet<TerrainType> { TerrainType.ForestFloor, TerrainType.Grass, TerrainType.Swamp },
        Ashwalk => new HashSet<TerrainType> { TerrainType.Lava, TerrainType.Wasteland, TerrainType.CursedLands },
        _ => new HashSet<TerrainType>(),
    };

    /// <summary>The terrains the spell makes native at <paramref name="rank"/>: its three at rank I, every terrain from rank II.</summary>
    public static IReadOnlySet<TerrainType> NativeTerrains(string spell, int rank) =>
        rank >= 2 ? Enum.GetValues<TerrainType>().ToHashSet() : NativeTerrains(spell);

    /// <summary>A terrain set as a status value: one bit per <see cref="TerrainType"/>.</summary>
    public static int TerrainMask(IEnumerable<TerrainType> terrains) => terrains.Aggregate(0, (mask, t) => mask | (1 << (int)t));

    /// <summary>Whether a status makes <paramref name="terrain"/> native to the hero's army (Pathfinder's Call, Ashwalk).</summary>
    public static bool MakesNative(Hero hero, TerrainType terrain) =>
        hero.Statuses.Any(s => s.Kind == HeroStatusKind.NativeTerrain && (s.Value & (1 << (int)terrain)) != 0);

    /// <summary>The rank of the Veil on the hero, 0 without one.</summary>
    public static int VeilRank(Hero hero) => hero.Statuses.Where(s => s.Kind == HeroStatusKind.Veil).Select(s => s.Value).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Veil III: whether the hero is hidden on the map from <paramref name="viewer"/>, another player none of whose heroes is within
    /// <see cref="VeilSightRange"/> cells of it (<see cref="GameState.CanSee"/> asks).
    /// </summary>
    public static bool HidesFrom(GameState state, Hero hero, PlayerId viewer) =>
        hero.Owner != viewer && VeilRank(hero) >= 3
        && !state.HeroesOf(viewer).Any(h => h.Position.Level == hero.Position.Level
                                             && Math.Max(Math.Abs(h.Position.X - hero.Position.X), Math.Abs(h.Position.Y - hero.Position.Y)) <= VeilSightRange);

    /// <summary>
    /// What Grave Whisper adds to the hero's Necromancy share now, in percent (0 without it). Necromancy reads it with the hero's other points;
    /// the status ends when the hero raises undead, or when the week ends.
    /// </summary>
    public static int NecromancyPercent(Hero hero) => hero.StatusTotal(HeroStatusKind.Necromancy);

    /// <summary>Whether Ledger of Ages III is still open on the hero (a week): the ledger can be read again at no cost.</summary>
    public static bool ReadsLedger(Hero hero) => hero.Statuses.Any(s => s.Source == LedgerOfAges && s.Kind == HeroStatusKind.Cast);

    /// <summary>The town Eavesdrop III still listens to for the hero (a week), if any.</summary>
    public static MapObjectId? ListensTo(Hero hero) =>
        hero.Statuses.FirstOrDefault(s => s.Source == Eavesdrop && s.Kind == HeroStatusKind.Cast) is { } status ? new MapObjectId(status.Value) : null;

    /// <summary>
    /// Town Portal's town at rank I: the nearest own town whose entrance is free (the same level first, then by the distance in tiles, then by id);
    /// null when there is none other than where the hero stands.
    /// </summary>
    public static Town? PortalTown(GameState state, Hero hero) =>
        state.ObjectsOf<Town>()
            .Where(t => PortalOpen(state, hero, t))
            .OrderBy(t => t.VisitPosition.Level != hero.Position.Level)
            .ThenBy(t => Math.Max(Math.Abs(t.VisitPosition.X - hero.Position.X), Math.Abs(t.VisitPosition.Y - hero.Position.Y)))
            .ThenBy(t => t.Id.Value)
            .FirstOrDefault();

    /// <summary>
    /// The town a Town Portal aimed at <paramref name="target"/> goes to: the nearest without a target; from rank II the own town the cell belongs
    /// to (null when it is not an own town with a free entrance).
    /// </summary>
    public static Town? PortalTown(GameState state, Hero hero, MapPosition? target) =>
        target is not { } cell ? PortalTown(state, hero)
        : AdventureIntel.TownAt(state, cell) is { } town && PortalOpen(state, hero, town) ? town : null;

    private static bool PortalOpen(GameState state, Hero hero, Town town) =>
        town.Owner == hero.Owner && town.VisitPosition != hero.Position && state.HeroAt(town.VisitPosition) is null;

    /// <summary>The adventure spells the hero could cast (it knows them, from its book or its artifacts, and they are built), whether or not it can pay now.</summary>
    public static IEnumerable<SpellDefinition> Known(GameState state, Hero hero) =>
        MagicRules.Known(hero, state.Spells).Select(k => k.Spell).Where(s => s.IsAdventure && Built.Contains(s.Id))
            .OrderBy(s => s.Level).ThenBy(s => s.Id, StringComparer.Ordinal);

    /// <summary>What casting the spell costs the hero in mana (its attunement lowers it, <see cref="ManaRules.SpellCost"/>).</summary>
    public static int Cost(GameState state, Hero hero, string spell) => ManaRules.SpellCost(hero, state.Spells.Get(spell).Mana);

    /// <summary>
    /// Why the hero cannot cast the adventure spell now (aimed at <paramref name="target"/>), or null: a spell book that holds it, a built
    /// adventure spell, its mana, 500 movement points; then each spell's own conditions (the aim is not checked when <paramref name="aimed"/> is
    /// false: the cell is still to be picked). Dimension Door: once a day (twice at rank III), up to 6 tiles (9 from rank II) in a straight line
    /// on the same level to an explored, free, passable and unguarded tile. Town Portal: an own town with a free entrance (at rank I only the
    /// nearest). Distant Vision: a cell in sight. Eavesdrop: a town of another player, not an ally, or a neutral one, in sight (rank I) or
    /// explored (from rank II).
    /// </summary>
    public static string? Refusal(GameState state, Hero hero, string spell, MapPosition? target, bool aimed = true)
    {
        if (!hero.HasSpellBook)
        {
            return $"{hero.Name} has no spell book.";
        }

        if (!state.Spells.TryGet(spell, out var definition) || MagicRules.Known(hero, state.Spells).All(k => k.Spell.Id != spell))
        {
            return $"{hero.Name} does not know that spell.";
        }

        if (!definition.IsAdventure || !Built.Contains(spell))
        {
            return $"{definition.Name} cannot be cast on the map{(definition.IsAdventure ? " yet" : string.Empty)}.";
        }

        if (hero.Mana < Cost(state, hero, spell))
        {
            return $"Not enough mana for {definition.Name}.";
        }

        if (hero.MovementPoints < MovementCost)
        {
            return $"Casting {definition.Name} needs {MovementCost} movement points.";
        }

        var rank = RankOf(state, hero, spell);
        switch (spell)
        {
            case TownPortal when PortalTown(state, hero) is null:
                return "There is no own town to return to.";
            case TownPortal when !aimed || target is null:
                return null;
            case TownPortal when rank < 2:
                return PortalTown(state, hero, target) == PortalTown(state, hero) ? null : "Town Portal goes only to the nearest own town at rank I.";
            case TownPortal:
                return PortalTown(state, hero, target) is null ? "Town Portal needs an own town with a free entrance." : null;
            case DimensionDoor when hero.Statuses.FirstOrDefault(s => s.Source == DimensionDoor && s.Kind == HeroStatusKind.Cast) is { } used
                                    && used.Value >= DimensionDoorCasts(rank):
                return DimensionDoorCasts(rank) == 1 ? "Dimension Door works once a day." : $"Dimension Door works {DimensionDoorCasts(rank)} times a day.";
            case DimensionDoor:
                return aimed ? DoorRefusal(state, hero, target, DimensionDoorRange(rank)) : null;
            case DistantVision when aimed:
                return target is { } cell && state.Map.Contains(cell) && state.Fog.IsInSight(hero.Owner, cell) ? null : "Distant Vision looks at a cell in sight.";
            case Eavesdrop when aimed:
                return EavesdropRefusal(state, hero, target, rank);
            default:
                return null;
        }
    }

    /// <summary>The town an aimed Eavesdrop listens to, or null.</summary>
    public static Town? EavesdropTown(GameState state, MapPosition? target) => target is { } cell ? AdventureIntel.TownAt(state, cell) : null;

    private static string? EavesdropRefusal(GameState state, Hero hero, MapPosition? target, int rank)
    {
        if (EavesdropTown(state, target) is not { } town)
        {
            return "Eavesdrop needs a town to listen to.";
        }

        if (town.Owner is { } owner && (owner == hero.Owner || state.AreAllies(owner, hero.Owner)))
        {
            return "Eavesdrop listens only to enemy towns.";
        }

        return rank >= 2
            ? AdventureIntel.Explored(state, hero.Owner, town) ? null : "Eavesdrop needs a town the player has explored."
            : AdventureIntel.InSight(state, hero.Owner, town) ? null : "Eavesdrop at rank I needs a town in sight.";
    }

    private static string? DoorRefusal(GameState state, Hero hero, MapPosition? target, int range)
    {
        if (target is not { } to)
        {
            return "Dimension Door needs a tile to go to.";
        }

        var (dx, dy) = (to.X - hero.Position.X, to.Y - hero.Position.Y);
        var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (to.Level != hero.Position.Level || distance == 0 || distance > range || (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy)))
        {
            return $"Dimension Door goes up to {range} tiles in a straight line.";
        }

        if (!state.Map.Contains(to) || !state.Fog.IsExplored(hero.Owner, to))
        {
            return "Dimension Door lands only on explored tiles.";
        }

        return MovementRules.CheckTile(state, to) != MoveResult.Moved || state.HeroAt(to) is not null || state.ObjectCellAt(to) is not null || state.IsGuarded(to)
            ? "Dimension Door needs a free, passable and unguarded tile."
            : null;
    }
}

/// <summary>
/// Casts adventure spells (ADR-120): validates, pays the mana and the movement points, then does what the spell does at the hero's rank and
/// publishes it. It also ends Grave Whisper once the hero has raised undead (<see cref="UndeadRaised"/>).
/// </summary>
internal sealed class AdventureMagicService(GameState state, IEventPublisher events, TownVisitService towns) : IEventHandler<UndeadRaised>
{
    /// <summary>Subscribes to raised undead (Grave Whisper is used up).</summary>
    internal void SubscribeTo(IEventSubscriber bus) => bus.Subscribe<UndeadRaised>(this);

    internal void Cast(PlayerId player, HeroId heroId, string spell, MapPosition? target)
    {
        var hero = state.Heroes.FirstOrDefault(h => h.Id == heroId && h.Owner == player) ?? throw new CommandRefusedException($"{heroId} is not an own hero on the map.");
        if (AdventureMagic.Refusal(state, hero, spell, target) is { } refusal)
        {
            throw new CommandRefusedException(refusal);
        }

        var rank = AdventureMagic.RankOf(state, hero, spell);
        var cost = AdventureMagic.Cost(state, hero, spell);
        hero.Mana -= cost;

        // Town Portal below rank III ends the day's movement (rank III: "costs no movement points beyond the 500", proposal).
        hero.MovementPoints = spell == AdventureMagic.TownPortal && rank < 3 ? 0 : hero.MovementPoints - AdventureMagic.MovementCost;
        events.Publish(new AdventureSpellCast(hero.Id, spell, target, cost, hero.Mana, hero.MovementPoints));
        switch (spell)
        {
            case AdventureMagic.TownPortal:
                var town = AdventureMagic.PortalTown(state, hero, rank >= 2 ? target : null)!;
                Relocate(hero, town.VisitPosition);
                towns.Enter(hero, town);
                break;
            case AdventureMagic.DimensionDoor:
                var used = hero.Statuses.FirstOrDefault(s => s.Source == spell && s.Kind == HeroStatusKind.Cast)?.Value ?? 0;
                Relocate(hero, target!.Value);
                Mark(hero, new HeroStatus(spell, HeroStatusKind.Cast, used + 1, StatusExpiry.EndOfDay));
                break;
            case AdventureMagic.ViewTerrain:
                events.Publish(new AreaRevealed(hero.Owner, hero.Position, AdventureMagic.ViewTerrainRadius(rank)));
                break;
            case AdventureMagic.PathfindersCall or AdventureMagic.Ashwalk:
                Mark(hero, new HeroStatus(spell, HeroStatusKind.NativeTerrain, AdventureMagic.TerrainMask(AdventureMagic.NativeTerrains(spell, rank)), StatusExpiry.EndOfDay));
                if (rank >= 3 && hero.MaxMovementPoints * AdventureMagic.NativeWalkMovementPercent / 100 is var extra and > 0)
                {
                    hero.MovementPoints += extra;
                    events.Publish(new HeroMovementGained(hero.Id, extra, hero.MovementPoints));
                }

                break;
            case AdventureMagic.DistantVision:
                var center = target!.Value;
                var radius = AdventureMagic.DistantVisionRadius(rank);
                events.Publish(new ArmiesSighted(hero.Id, hero.Owner, center, radius, AdventureIntel.ArmiesAround(state, hero.Owner, center, radius, underFog: rank >= 3)));
                break;
            case AdventureMagic.DetectMagic:
                var detectRadius = AdventureMagic.DetectMagicRadius(rank);
                events.Publish(new MagicDetected(hero.Id, hero.Owner, hero.Position, detectRadius, AdventureIntel.ObjectsAround(state, hero.Position, detectRadius, bankRewards: rank >= 3)));
                break;
            case AdventureMagic.GraveWhisper:
                var percent = AdventureMagic.GraveWhisperPercent(rank);
                Mark(hero, new HeroStatus(spell, HeroStatusKind.Necromancy, percent, StatusExpiry.EndOfWeek));
                events.Publish(new GravesMarked(hero.Id, hero.Owner, percent,
                    rank >= 3 ? AdventureIntel.MonstersAround(state, hero.Owner, hero.Position, AdventureMagic.GraveWhisperRadius) : []));
                break;
            case AdventureMagic.LedgerOfAges:
                if (rank >= 3)
                {
                    Mark(hero, new HeroStatus(spell, HeroStatusKind.Cast, 1, StatusExpiry.EndOfWeek));
                }

                events.Publish(new LedgerRead(hero.Id, hero.Owner, AdventureIntel.Ledger(state, hero.Owner, inTowns: rank >= 2)));
                break;
            case AdventureMagic.Veil:
                Mark(hero, new HeroStatus(spell, HeroStatusKind.Veil, rank, StatusExpiry.EndOfWeek));
                break;
            case AdventureMagic.Eavesdrop:
                var listened = AdventureMagic.EavesdropTown(state, target)!;
                if (rank >= 3)
                {
                    Mark(hero, new HeroStatus(spell, HeroStatusKind.Cast, listened.Id.Value, StatusExpiry.EndOfWeek));
                }

                events.Publish(new TownOverheard(hero.Id, hero.Owner, AdventureIntel.Town(state, listened, hero.Owner)));
                break;
        }
    }

    /// <summary>Grave Whisper is used up by the victory that raised undead.</summary>
    public void Handle(UndeadRaised @event)
    {
        var hero = state.Heroes.Concat(state.GarrisonHeroes).FirstOrDefault(h => h.Id == @event.Hero);
        if (hero?.RemoveStatuses(AdventureMagic.GraveWhisper, HeroStatusKind.Necromancy) is { Count: > 0 } ended)
        {
            events.Publish(new HeroStatusesExpired(hero.Id, ended));
        }
    }

    private void Relocate(Hero hero, MapPosition to)
    {
        var from = hero.Position;
        hero.Position = to;
        events.Publish(new HeroTeleported(hero.Id, from, to));
    }

    private void Mark(Hero hero, HeroStatus status)
    {
        hero.AddStatus(status);
        events.Publish(new HeroStatusGained(hero.Id, status));
    }
}
