using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Map;

public enum TerrainClass
{
    /// <summary><see cref="TerrainDefinition.Cost"/> for everyone.</summary>
    Open,

    /// <summary>A fixed penalty for every faction: <see cref="TerrainDefinition.Cost"/> for everyone.</summary>
    Rough,

    /// <summary><see cref="TerrainCatalog.NativeCost"/> for native armies, <see cref="TerrainDefinition.Cost"/> for everyone else.</summary>
    Native,

    /// <summary>Cannot be walked on.</summary>
    Impassable,
}

/// <summary>Movement rules of one terrain (docs/decisions.md ADR-024, ADR-032).</summary>
/// <param name="Terrain">Which terrain.</param>
/// <param name="Class">Penalty class.</param>
/// <param name="Cost">Cost of leaving a tile of this terrain orthogonally; for native terrain, the non-native cost.</param>
/// <param name="NativeTowns">Town ids (<see cref="TownIds"/>) whose armies move at <see cref="TerrainCatalog.NativeCost"/>.</param>
public sealed record TerrainDefinition(TerrainType Terrain, TerrainClass Class, int Cost, IReadOnlyList<string> NativeTowns)
{
    public void Validate()
    {
        if (Class != TerrainClass.Impassable && Cost <= 0)
        {
            throw new FormatException($"Terrain {Terrain} needs a positive cost.");
        }

        if ((Class == TerrainClass.Native) != (NativeTowns.Count > 0))
        {
            throw new FormatException($"Terrain {Terrain}: native towns must be listed exactly when the class is Native.");
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

    public static TerrainCatalog Default { get; } = new(
    [
        new(TerrainType.Grass, TerrainClass.Open, 100, []),
        new(TerrainType.Dirt, TerrainClass.Open, 100, []),
        new(TerrainType.Rough, TerrainClass.Rough, 125, []),
        new(TerrainType.Highlands, TerrainClass.Native, 150, [TownIds.Haven]),
        new(TerrainType.ForestFloor, TerrainClass.Native, 150, [TownIds.Sylvan]),
        new(TerrainType.Wasteland, TerrainClass.Native, 150, [TownIds.Necropolis]),
        new(TerrainType.Snow, TerrainClass.Native, 150, [TownIds.Academy]),
        new(TerrainType.Lava, TerrainClass.Native, 150, [TownIds.Inferno]),
        new(TerrainType.Subterranean, TerrainClass.Native, 150, [TownIds.Dungeon]),
        new(TerrainType.Sand, TerrainClass.Native, 150, [TownIds.Stronghold]),
        new(TerrainType.Swamp, TerrainClass.Native, 175, [TownIds.SwampBestiary]),
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
    /// <remarks>The Pathfinding skill (Phase 3) will reduce non-native and rough penalties by 25/50/75 %.</remarks>
    public int MovementCost(TerrainType terrain, string? armyTown = null)
    {
        var definition = Get(terrain);
        return definition.Class switch
        {
            TerrainClass.Impassable => throw new InvalidOperationException($"{terrain} is impassable."),
            TerrainClass.Native when armyTown is not null && definition.NativeTowns.Contains(armyTown) => NativeCost,
            _ => definition.Cost,
        };
    }

    /// <summary>Returns a new catalog where <paramref name="overrides"/> replace the matching terrains.</summary>
    public TerrainCatalog With(IEnumerable<TerrainDefinition> overrides) =>
        new(_definitions.Values.Concat(overrides).GroupBy(d => d.Terrain).Select(g => g.Last()));
}
