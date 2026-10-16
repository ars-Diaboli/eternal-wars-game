using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Map;

public enum TerrainClass
{
    /// <summary><see cref="TerrainCatalog.NativeCost"/> for native armies, <see cref="TerrainDefinition.Cost"/> for everyone else.</summary>
    Passable,

    /// <summary>Cannot be walked on.</summary>
    Impassable,
}

/// <summary>Movement rules of one terrain (docs/decisions.md ADR-045).</summary>
/// <param name="Terrain">Which terrain.</param>
/// <param name="Class">Passable or not.</param>
/// <param name="Cost">Cost of leaving a tile of this terrain orthogonally for a non-native army (at least <see cref="TerrainCatalog.NativeCost"/>).</param>
/// <param name="NativeTowns">Town ids (<see cref="TownIds"/>) whose armies move at <see cref="TerrainCatalog.NativeCost"/>; a town may be native to several terrains.</param>
public sealed record TerrainDefinition(TerrainType Terrain, TerrainClass Class, int Cost, IReadOnlyList<string> NativeTowns)
{
    public void Validate()
    {
        if (Class == TerrainClass.Passable && Cost < TerrainCatalog.NativeCost)
        {
            throw new FormatException($"Terrain {Terrain} needs a cost of at least {TerrainCatalog.NativeCost}.");
        }

        if (Class == TerrainClass.Impassable && NativeTowns.Count > 0)
        {
            throw new FormatException($"Terrain {Terrain} is impassable and cannot have native towns.");
        }
    }
}

/// <summary>
/// Terrain rules as data. Built-in defaults cover every <see cref="TerrainType"/>; JSON can override them
/// (<c>EternalWars.Core.Data.Content.TerrainDefinitionSerializer</c>).
/// </summary>
public sealed class TerrainCatalog
{
    public const int NativeCost = 100;

    private readonly Dictionary<TerrainType, TerrainDefinition> _definitions;

    public TerrainCatalog(IEnumerable<TerrainDefinition> definitions)
    {
        _definitions = [];
        foreach (var definition in definitions)
        {
            definition.Validate();
            _definitions[definition.Terrain] = definition;
        }

        var missing = Enum.GetValues<TerrainType>().Where(t => !_definitions.ContainsKey(t)).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"Missing terrain definitions: {string.Join(", ", missing)}.", nameof(definitions));
        }
    }

    /// <summary>
    /// The sandbox's multipliers (ADR-045): every passable terrain costs non-natives 125–200, natives 100. Grass, dirt,
    /// sand, beach and rough have no penalty-free town except where listed; only roads, Pathfinding and artifacts help.
    /// </summary>
    public static TerrainCatalog Default { get; } = new(
    [
        Passable(TerrainType.Grass, 125, TownIds.Haven, TownIds.Sylvan),
        Passable(TerrainType.Highlands, 125, TownIds.Haven, TownIds.Conflux),
        Passable(TerrainType.ForestFloor, 125, TownIds.Sylvan),
        Passable(TerrainType.CursedLands, 125, TownIds.Necropolis),
        Passable(TerrainType.Subterranean, 125, TownIds.Dungeon),
        Passable(TerrainType.Sand, 125),
        Passable(TerrainType.Beach, 125),
        Passable(TerrainType.Dirt, 150),
        Passable(TerrainType.Rough, 150, TownIds.Stronghold),
        Passable(TerrainType.Wasteland, 150, TownIds.Stronghold),
        Passable(TerrainType.Lava, 150, TownIds.Inferno),
        Passable(TerrainType.RockyGround, 150, TownIds.DragonUtopia),
        Passable(TerrainType.Ice, 175, TownIds.Academy),
        Passable(TerrainType.ShallowWater, 175, TownIds.SwampBestiary),
        Passable(TerrainType.Snow, 200, TownIds.Academy),
        Passable(TerrainType.Swamp, 200, TownIds.SwampBestiary),
        new(TerrainType.Water, TerrainClass.Impassable, 0, []),
        new(TerrainType.Rock, TerrainClass.Impassable, 0, []),
    ]);

    public IEnumerable<TerrainDefinition> All => _definitions.Values;

    public TerrainDefinition Get(TerrainType terrain) => _definitions[terrain];

    public bool IsPassable(TerrainType terrain) => Get(terrain).Class != TerrainClass.Impassable;

    /// <summary>
    /// Orthogonal cost of leaving a tile of <paramref name="terrain"/>.
    /// <paramref name="armyTown"/> is the town every creature of the army belongs to, or null for a mixed
    /// army, a neutral army or no army (HoMM3: only an all-native army avoids the penalty).
    /// </summary>
    /// <remarks>The Pathfinding skill and artifacts (Phase 3) will reduce the non-native penalty.</remarks>
    public int MovementCost(TerrainType terrain, string? armyTown = null)
    {
        var definition = Get(terrain);
        return definition.Class switch
        {
            TerrainClass.Impassable => throw new InvalidOperationException($"{terrain} is impassable."),
            _ when armyTown is not null && definition.NativeTowns.Contains(armyTown) => NativeCost,
            _ => definition.Cost,
        };
    }

    /// <summary>Returns a new catalog where <paramref name="overrides"/> replace the matching terrains.</summary>
    public TerrainCatalog With(IEnumerable<TerrainDefinition> overrides) =>
        new(_definitions.Values.Concat(overrides).GroupBy(d => d.Terrain).Select(g => g.Last()));

    private static TerrainDefinition Passable(TerrainType terrain, int cost, params string[] nativeTowns) =>
        new(terrain, TerrainClass.Passable, cost, nativeTowns);
}
