using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Analysis;

/// <summary>
/// How far a hero gets from a tile, in days: the days of every tile it reaches (<see cref="DaysTo"/>), 0 on the tile it starts from.
/// </summary>
public sealed class ReachMap
{
    private readonly PathFinder.CostMap _costs;
    private readonly MapPosition _from;
    private readonly int _daily;

    internal ReachMap(PathFinder.CostMap costs, MapPosition from, int daily) => (_costs, _from, _daily) = (costs, from, daily);

    /// <summary>The route's movement cost to <paramref name="tile"/>; 0 on the start, null where no route goes.</summary>
    public int? CostTo(MapPosition tile) => tile == _from ? 0 : _costs.CostTo(tile);

    /// <summary>
    /// The days a hero needs to <paramref name="tile"/>: its route's cost over a day's movement, rounded up; null where no route goes. The
    /// game lets a day's last step run over what is left, so this may count a day more than a hero takes (an upper bound).
    /// </summary>
    public int? DaysTo(MapPosition tile) => CostTo(tile) is { } cost ? (cost + _daily - 1) / _daily : null;
}

/// <summary>
/// The fairness numbers of one start (EDIT-9, ADR-263): where the player begins, how many days to the nearest town (not its own), mine of
/// each resource and resource provider (null: none reached), the treasure value and the guard strength within 3 and within 7 days, and how
/// many mines of each resource lie within 3 days.
/// </summary>
public sealed record StartAnalysis(
    PlayerId Player,
    MapPosition Start,
    int? DaysToTown,
    IReadOnlyDictionary<ResourceType, int?> DaysToMine,
    int? DaysToProvider,
    long TreasureWithin3,
    long TreasureWithin7,
    long GuardsWithin3,
    long GuardsWithin7,
    IReadOnlyDictionary<ResourceType, int> MinesWithin3);

/// <summary>The starts' numbers, and a warning for each number where a start is far from the others.</summary>
public sealed record FairnessAnalysis(IReadOnlyList<StartAnalysis> Starts, IReadOnlyList<string> Warnings);

/// <summary>
/// The core's map analysis (EDIT-9, ADR-263) over any <see cref="GameState"/>, a scenario in the editor or a game: how many days a hero needs
/// from a tile to every other (<see cref="Reach"/>), how strong the guards of a cell are (<see cref="GuardStrength"/>), and the fairness
/// numbers of every start (<see cref="Fairness"/>).
/// </summary>
/// <remarks>
/// Reach uses the game's step costs (<see cref="PathFinder.CostsFor"/>) for a scout: a hero of the player without skill or artifact whose army
/// is at home on its faction's terrain (one creature of the start town), and a day is <see cref="MovementAllowance.NoArmyBase"/> points
/// whatever the army, so factions compare. Unlike a route in the game it goes on past guards, pickups and visits, as a hero that fights or
/// takes them walks on; heroes on the map stand in the way. Strength is army power (count × the creature's fight value), as the
/// guards and the generator weigh it; a count left open counts as the middle of the range it is rolled from, a random monster as the mean of
/// the creatures it may be. Treasure is valued in gold as the generator values it (<see cref="GoldValue"/>, <see cref="ArtifactValue"/>).
/// </remarks>
public sealed class MapAnalysis(GameState state)
{
    /// <summary>The nearer of the two ranges the fairness numbers count treasure and guards in, in days.</summary>
    public const int NearDays = 3;

    /// <summary>The farther range.</summary>
    public const int FarDays = 7;

    /// <summary>How far a start's number may be from the starts' mean, in percent, before a warning (a placeholder, EDIT-9).</summary>
    public const int WarningPercent = 20;

    /// <summary>What a unit of a resource is worth in gold for treasure (placeholder until marketplace rates, TOWN-2).</summary>
    public static int GoldValue(ResourceType resource) => resource switch
    {
        ResourceType.Gold => 1,
        ResourceType.Lumber or ResourceType.Ore => 100,
        _ => 200,
    };

    /// <summary>What an artifact of a tier is worth in gold for treasure.</summary>
    public static int ArtifactValue(ArtifactTier tier) => tier switch
    {
        ArtifactTier.Minor => 4000,
        ArtifactTier.Major => 8000,
        _ => 15000,
    };

    /// <summary>
    /// The army power of what guards with <paramref name="mapObject"/>: a monster's stack, a garrison's troops; 0 for anything else. An open
    /// count is the middle of its tier's range (<see cref="GuardService.CountRange"/>); a random monster is the mean of its candidates.
    /// </summary>
    public static long GuardPower(MapObject mapObject, CreatureCatalog creatures)
    {
        ArgumentNullException.ThrowIfNull(mapObject);
        ArgumentNullException.ThrowIfNull(creatures);
        return mapObject switch
        {
            MonsterGuard { RandomTier: { } tier } monster when PlaceholderRules.MonsterCandidates(creatures, tier) is { Count: > 0 } candidates =>
                (long)Math.Round(candidates.Average(c => (double)Power(c, monster.Count))),
            MonsterGuard monster when creatures.TryGet(monster.Creature, out var creature) => Power(creature, monster.Count),
            MapGarrison garrison => garrison.Troops.Stacks.Sum(s => creatures.TryGet(s.Stack.Creature, out var c) ? (long)s.Stack.Count * c.FightValue : 0),
            _ => 0,
        };

        static long Power(CreatureDefinition creature, int? count)
        {
            var (min, max) = GuardService.CountRange(creature.Tier);
            return (long)(count ?? (min + max) / 2) * creature.FightValue;
        }
    }

    /// <summary>
    /// What <paramref name="mapObject"/> is worth as treasure, in gold: a resource pile (an open amount as the middle of its range), a chest
    /// (its fixed gold or experience, the more, else what its rolls give on average), an artifact on the ground, a Pandora's box (its resources, experience and
    /// artifacts); 0 for anything else.
    /// </summary>
    public static long TreasureValue(MapObject mapObject, ArtifactCatalog artifacts)
    {
        ArgumentNullException.ThrowIfNull(mapObject);
        ArgumentNullException.ThrowIfNull(artifacts);
        return mapObject switch
        {
            ResourcePile pile => (long)(pile.Amount ?? Middle(ResourcePile.AmountRange(pile.Resource))) * GoldValue(pile.Resource),
            TreasureChest { FixedValues: { } values } => Math.Max(values.Gold, values.Experience), // the hero takes the better
            TreasureChest => ChestAverage,
            ArtifactPickup pickup => ArtifactValue(pickup.Artifact is { } id && artifacts.TryGet(id, out var artifact) ? artifact.Tier : pickup.Tier),
            PandorasBox { Contents: { } contents } => Value(contents.Resources) + contents.Experience
                + contents.Artifacts.Sum(a => (long)ArtifactValue(artifacts.TryGet(a, out var artifact) ? artifact.Tier : ArtifactTier.Minor)),
            _ => 0,
        };

        static int Middle((int Min, int Max) range) => (range.Min + range.Max) / 2;
    }

    /// <summary>A chest's worth on average: the gold of each roll by its odds, an artifact (a minor one) for the roll without gold.</summary>
    private static long ChestAverage =>
        TreasureChest.Odds.Sum(o => (long)o.Weight * (o.Values?.Gold ?? ArtifactValue(ArtifactTier.Minor))) / TreasureChest.Odds.Sum(o => o.Weight);

    private static long Value(ResourceSet resources) => Enum.GetValues<ResourceType>().Sum(r => (long)resources[r] * GoldValue(r));

    /// <summary>
    /// The days from <paramref name="from"/> to every tile for a scout of <paramref name="player"/> at home on the terrain of
    /// <paramref name="town"/>'s faction (none: at home nowhere), fog ignored.
    /// </summary>
    public ReachMap Reach(MapPosition from, PlayerId player, string? town = null)
    {
        if (!state.Map.Contains(from))
        {
            throw new ArgumentOutOfRangeException(nameof(from), from, "The tile is not on the map.");
        }

        var army = state.Creatures.All.Where(c => c.Town == town && c.Tier == 1).OrderBy(c => c.Id.Value).FirstOrDefault() is { } native && town is not null
            ? Armies.Army.Of(new Armies.CreatureStack(native.Id, 1))
            : null;
        var scout = new Hero(new HeroId(-1), "Scout", player, from, army);
        return new ReachMap(new PathFinder(state).CostsFor(scout, from, respectFog: false, passThrough: true), from, MovementAllowance.NoArmyBase);
    }

    /// <summary>The army power that guards <paramref name="cell"/>: the monsters whose zone it is in, and the one standing on it.</summary>
    public long GuardStrength(MapPosition cell)
    {
        var power = state.GuardsOf(cell).Sum(m => GuardPower(m, state.Creatures));
        if (state.ObjectCellAt(cell)?.Object is MonsterGuard here && !state.GuardsOf(cell).Contains(here))
        {
            power += GuardPower(here, state.Creatures);
        }

        return power;
    }

    /// <summary>
    /// The fairness numbers of every player that has a start (its first town, else its first hero), and a warning for each number where
    /// a start is more than <see cref="WarningPercent"/> from the starts' mean, or reaches nothing where another does.
    /// </summary>
    public FairnessAnalysis Fairness()
    {
        var starts = new List<StartAnalysis>();
        foreach (var player in state.Players)
        {
            if (StartOf(player.Id) is var (start, town))
            {
                starts.Add(Measure(player.Id, start, town));
            }
        }

        var warnings = new List<string>();
        if (starts.Count > 1)
        {
            // Days are whole numbers: a start a day off is warned only when that day is also more than the share of the mean.
            Compare(starts, "days to the nearest town", s => s.DaysToTown, 1, warnings);
            foreach (var resource in starts.SelectMany(s => s.DaysToMine.Keys).Distinct().Order())
            {
                Compare(starts, $"days to the nearest {resource.ToString().ToLowerInvariant()} mine", s => s.DaysToMine.GetValueOrDefault(resource), 1, warnings);
            }

            Compare(starts, "days to the nearest resource provider", s => s.DaysToProvider, 1, warnings);
            Compare(starts, $"treasure within {NearDays} days", s => s.TreasureWithin3, warnings);
            Compare(starts, $"treasure within {FarDays} days", s => s.TreasureWithin7, warnings);
            Compare(starts, $"guards within {NearDays} days", s => s.GuardsWithin3, warnings);
            Compare(starts, $"guards within {FarDays} days", s => s.GuardsWithin7, warnings);
        }

        return new FairnessAnalysis(starts, warnings);
    }

    /// <summary>
    /// Where <paramref name="player"/> begins, and the faction whose terrain it is at home on: the entrance of its first town on the map, else
    /// where its first hero stands with the town of the hero's army; null for neither.
    /// </summary>
    private (MapPosition Start, string? Town)? StartOf(PlayerId player)
    {
        if (state.ObjectsOf<Town>().Where(t => t.Owner == player && state.Map.Contains(t.VisitPosition)).OrderBy(t => t.Id.Value).FirstOrDefault() is { } town)
        {
            return (town.VisitPosition, town.TownType);
        }

        return state.Heroes.FirstOrDefault(h => h.Owner == player && state.Map.Contains(h.Position)) is { } hero
            ? (hero.Position, MovementRules.ArmyTown(state, hero))
            : null;
    }

    private StartAnalysis Measure(PlayerId player, MapPosition start, string? town)
    {
        var reach = Reach(start, player, town);
        bool Guards(MapObject o) => o is not MapGarrison garrison || !garrison.LetsThrough(state, player); // its own and allies' let it through
        var reached = state.Objects
            .Where(o => o.Definition.Footprint.HasVisit && state.Map.Contains(o.VisitPosition))
            .Select(o => (Object: o, Days: reach.DaysTo(o.VisitPosition)))
            .Where(r => r.Days is not null)
            .Select(r => (r.Object, Days: r.Days!.Value))
            .ToList();
        int? Nearest(Func<MapObject, bool> what) => reached.Where(r => what(r.Object)).Select(r => (int?)r.Days).Min();
        long Sum(int days, Func<MapObject, long> value) => reached.Where(r => r.Days <= days).Sum(r => value(r.Object));

        var resources = state.Objects.OfType<Mine>().Select(m => m.Resource).Distinct().Order().ToList();
        return new StartAnalysis(
            player,
            start,
            Nearest(o => o is Town && o.VisitPosition != start),
            resources.ToDictionary(r => r, r => Nearest(o => o is Mine mine && mine.Resource == r)),
            Nearest(o => o is ResourceProvider),
            Sum(NearDays, o => TreasureValue(o, state.Artifacts)),
            Sum(FarDays, o => TreasureValue(o, state.Artifacts)),
            Sum(NearDays, o => Guards(o) ? GuardPower(o, state.Creatures) : 0),
            Sum(FarDays, o => Guards(o) ? GuardPower(o, state.Creatures) : 0),
            resources.ToDictionary(r => r, r => reached.Count(x => x.Object is Mine mine && mine.Resource == r && x.Days <= NearDays)));
    }

    private static void Compare<T>(List<StartAnalysis> starts, string what, Func<StartAnalysis, T?> measure, double leastGap, List<string> warnings)
        where T : struct, IConvertible
    {
        var values = starts.Select(s => (Start: s, Value: measure(s))).ToList();
        foreach (var (start, _) in values.Where(v => v.Value is null && values.Any(o => o.Value is not null)))
        {
            warnings.Add($"Player {start.Player.Value + 1}: {what}: none is reached, where another start reaches one.");
        }

        var known = values.Where(v => v.Value is not null).Select(v => (v.Start, Value: Convert.ToDouble(v.Value!.Value, System.Globalization.CultureInfo.InvariantCulture))).ToList();
        if (known.Count < 2)
        {
            return;
        }

        var mean = known.Average(v => v.Value);
        foreach (var (start, value) in known.Where(v => Math.Abs(v.Value - mean) > Math.Max(mean * WarningPercent / 100, leastGap)))
        {
            warnings.Add($"Player {start.Player.Value + 1}: {what} {value:0.#}, the starts' mean {mean:0.#} (more than {WarningPercent} % off).");
        }
    }

    private static void Compare(List<StartAnalysis> starts, string what, Func<StartAnalysis, long> measure, List<string> warnings) =>
        Compare(starts, what, s => (long?)measure(s), 0, warnings);
}
