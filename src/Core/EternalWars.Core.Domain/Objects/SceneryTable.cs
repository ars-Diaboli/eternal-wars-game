using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// The scenery table (<c>src/Core/data/scenery.csv</c>, ADR-079, MAP-25) in the <see cref="CsvTable"/> conventions:
/// <c>id, name, footprint, terrains, notes</c>. A footprint is its rows joined by <c>/</c> (<c>BB/BB</c>; <c>.</c> leaves a cell free, so
/// <c>B./.B</c> is a diagonal piece); scenery has no entrance and every <c>B</c> cell blocks. <c>terrains</c> lists where the generator
/// uses it (<c>grass; forest-floor</c>).
/// </summary>
public static class SceneryTable
{
    public const string Id = "id";
    public const string Name = "name";
    public const string Shape = "footprint";
    public const string Terrains = "terrains";
    public const string Notes = "notes";

    /// <summary>The columns of the shipped table, in order.</summary>
    public static IReadOnlyList<string> Columns { get; } = [Id, Name, Shape, Terrains, Notes];

    public const string ResourceName = "EternalWars.Scenery.csv";

    private static readonly string[] Required = [Id, Name, Shape];

    public static IReadOnlyList<MapObjectDefinition> Parse(string text) =>
        CsvTable.Parse(text, "Scenery", Required)
            .Read(row => row.Text(Id) is { Length: > 0 } id ? Read(row, id) : null)
            .OfType<MapObjectDefinition>()
            .ToList();

    /// <summary>Text of the embedded scenery table (what the build shipped).</summary>
    public static string EmbeddedTable()
    {
        using var stream = typeof(SceneryTable).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The scenery table '{ResourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static MapObjectDefinition Read(CsvTable.Row row, string id)
    {
        Footprint footprint;
        try
        {
            footprint = Footprint.Obstacle(row.Text(Shape).Split('/'));
        }
        catch (FormatException e)
        {
            throw row.Error(Shape, e.Message);
        }

        return new MapObjectDefinition(id, row.Text(Name), MapObjectKind.Scenery, footprint)
        {
            Terrains = row.List(Terrains).Select(t => ParseTerrain(row, t)).ToList(),
        };
    }

    private static TerrainType ParseTerrain(CsvTable.Row row, string text) =>
        Enum.TryParse<TerrainType>(text.Replace("-", "", StringComparison.Ordinal), ignoreCase: true, out var terrain)
            ? terrain
            : throw row.Error(Terrains, $"'{text}' is not a terrain");
}
