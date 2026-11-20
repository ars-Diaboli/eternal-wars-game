using System.Text.Json;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Data.Stamps;

/// <summary>
/// On-disk representation of a stamp (<c>*.ewstamp.json</c>): a <see cref="MapFragment"/> kept to be pasted into any map (ADR-250).
/// Like a map, it has one string per row and one character per tile, with a space where the stamp has no tile (a fragment need not
/// fill its box).
/// </summary>
/// <remarks>
/// Version history: 1 = terrain, roads, rivers, corner heights and objects. The objects are written as a map writes them, in the map
/// format <see cref="MapFormat"/> names.
/// </remarks>
public sealed record StampDocument
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>The map format (<see cref="MapDocument.CurrentFormatVersion"/>) the <see cref="Objects"/> were written in.</summary>
    public int MapFormat { get; init; } = MapDocument.CurrentFormatVersion;

    public required string Name { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    /// <summary><see cref="Height"/> rows of <see cref="Width"/> chars (<see cref="TerrainCodes"/>); a space: no tile there.</summary>
    public required IReadOnlyList<string> Terrain { get; init; }

    /// <summary>Same shape as <see cref="Terrain"/> (<see cref="RoadCodes"/>, a space where there is no tile); omitted when the stamp has no roads.</summary>
    public IReadOnlyList<string>? Roads { get; init; }

    /// <summary>Same shape as <see cref="Terrain"/> (<see cref="RiverCodes"/>); omitted when the stamp has no rivers.</summary>
    public IReadOnlyList<string>? Rivers { get; init; }

    /// <summary>
    /// The ground's height per tile corner (<see cref="HeightCodes"/>): <see cref="Height"/> + 1 rows of <see cref="Width"/> + 1 chars, a
    /// space where the stamp has no corner. Omitted when the stamp has no tiles.
    /// </summary>
    public IReadOnlyList<string>? Heights { get; init; }

    /// <summary>The objects as a map keeps them, their places counted from the stamp's top-left tile; the ids are their own inside the stamp.</summary>
    public IReadOnlyList<MapObjectDocument> Objects { get; init; } = [];
}

/// <summary>Stamps between their file and the editor's <see cref="MapFragment"/>.</summary>
public static class StampSerializer
{
    public const string FileExtension = ".ewstamp.json";

    private const char NoTile = ' ';

    public static StampDocument ToDocument(MapFragment fragment, string name)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var tiles = fragment.Tiles.ToDictionary(t => (t.X, t.Y));
        var corners = fragment.Corners.ToDictionary(c => (c.X, c.Y), c => c.Height);
        List<string> Rows(Func<FragmentTile, char> code) =>
        [
            .. Enumerable.Range(0, fragment.Height).Select(y => string.Concat(
                Enumerable.Range(0, fragment.Width).Select(x => tiles.TryGetValue((x, y), out var tile) ? code(tile) : NoTile))),
        ];

        return new StampDocument
        {
            Name = name,
            Width = fragment.Width,
            Height = fragment.Height,
            Terrain = Rows(t => TerrainCodes.ToCode(t.Terrain)),
            Roads = fragment.Tiles.Any(t => t.Road != RoadType.None) ? Rows(t => RoadCodes.ToCode(t.Road)) : null,
            Rivers = fragment.Tiles.Any(t => t.River != RiverType.None) ? Rows(t => RiverCodes.ToCode(t.River)) : null,
            Heights = corners.Count == 0
                ? null
                :
                [
                    .. Enumerable.Range(0, fragment.Height + 1).Select(y => string.Concat(
                        Enumerable.Range(0, fragment.Width + 1).Select(x => corners.TryGetValue((x, y), out var height) ? HeightCodes.ToCode(height) : NoTile))),
                ],
            Objects = [.. fragment.Objects.Select(MapConverter.ToObjectDocument)],
        };
    }

    /// <summary>
    /// The stamp as a fragment, its objects made with <paramref name="target"/>'s content. Throws <see cref="FormatException"/> when the
    /// file is not a stamp this version reads or the content lacks what an object uses.
    /// </summary>
    public static MapFragment ToFragment(StampDocument document, GameState target)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(target);
        if (document.FormatVersion is < 1 or > StampDocument.CurrentFormatVersion || document.MapFormat > MapDocument.CurrentFormatVersion)
        {
            throw new FormatException($"Stamp format {document.FormatVersion} (map format {document.MapFormat}) is not one this version reads.");
        }

        if (document.Terrain is null || document.Objects is null || document.Objects.Any(o => o is null || string.IsNullOrEmpty(o.Definition)))
        {
            throw new FormatException("The stamp has no terrain rows, or an object without a definition.");
        }

        if (document.Width is < 1 or > MapSizes.MaxDimension || document.Height is < 1 or > MapSizes.MaxDimension)
        {
            throw new FormatException($"A stamp of {document.Width} × {document.Height} tiles is no stamp.");
        }

        Check(document.Terrain, "terrain", document.Width, document.Height);
        Check(document.Roads, "roads", document.Width, document.Height);
        Check(document.Rivers, "rivers", document.Width, document.Height);
        Check(document.Heights, "heights", document.Width + 1, document.Height + 1);

        var tiles = new List<FragmentTile>();
        for (var y = 0; y < document.Height; y++)
        {
            for (var x = 0; x < document.Width; x++)
            {
                if (document.Terrain[y][x] is var code && code != NoTile)
                {
                    tiles.Add(new FragmentTile(
                        x, y, TerrainCodes.FromCode(code),
                        document.Roads?[y][x] is { } road && road != NoTile ? RoadCodes.FromCode(road) : RoadType.None,
                        document.Rivers?[y][x] is { } river && river != NoTile ? RiverCodes.FromCode(river) : RiverType.None));
                }
            }
        }

        var corners = new List<FragmentCorner>();
        for (var y = 0; y <= document.Height && document.Heights is not null; y++)
        {
            for (var x = 0; x <= document.Width; x++)
            {
                if (document.Heights[y][x] is var code && code != NoTile)
                {
                    corners.Add(new FragmentCorner(x, y, HeightCodes.FromCode(code)));
                }
            }
        }

        // A paste sets the corners of its tiles: a stamp whose tiles lack one would leave holes in the ground.
        var given = corners.Select(c => (c.X, c.Y)).ToHashSet();
        if (tiles.Any(t => !given.Contains((t.X, t.Y)) || !given.Contains((t.X + 1, t.Y)) || !given.Contains((t.X, t.Y + 1)) || !given.Contains((t.X + 1, t.Y + 1))))
        {
            throw new FormatException("The stamp's heights do not cover the corners of its tiles.");
        }

        var objects = document.Objects.Select(o => MapConverter.ToObject(o, target)).ToList();
        if (objects.Select(o => o.Id).Distinct().Count() != objects.Count)
        {
            throw new FormatException("Two objects of the stamp have the same id.");
        }

        var cells = objects.SelectMany(o => o.OccupiedCells().Select(c => c.Position)).ToList();
        if (cells.Distinct().Count() != cells.Count
            || cells.Any(c => c.X < 0 || c.Y < 0 || c.X >= document.Width || c.Y >= document.Height || c.Level != 0))
        {
            throw new FormatException("An object of the stamp lies outside it or on another object.");
        }

        return new MapFragment(document.Width, document.Height, tiles, corners, objects);
    }

    public static string Serialize(StampDocument document) => JsonSerializer.Serialize(document, JsonDefaults.Options);

    public static StampDocument Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<StampDocument>(json, JsonDefaults.Options) ?? throw new FormatException("Stamp file is empty.");
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            // NotSupportedException: an object without its "kind".
            throw new FormatException($"Not a stamp file: {e.Message}", e);
        }
    }

    public static void Save(StampDocument document, string path) => File.WriteAllText(path, Serialize(document));

    public static StampDocument Load(string path) => Deserialize(File.ReadAllText(path));

    private static void Check(IReadOnlyList<string>? rows, string layer, int width, int height)
    {
        if (rows is not null && (rows.Count != height || rows.Any(r => r is null || r.Length != width)))
        {
            throw new FormatException($"The stamp's {layer} are not {height} rows of {width} characters.");
        }
    }
}

/// <summary>A folder of stamp files, such as the editor's <c>user://stamps/</c> (ADR-250).</summary>
public static class StampFolder
{
    /// <summary>The stamp files in <paramref name="directory"/> by name (ordinal, without the extension); none when it does not exist.</summary>
    public static IReadOnlyList<string> Names(string directory) =>
        Directory.Exists(directory)
            ? [.. Directory.GetFiles(directory, "*" + StampSerializer.FileExtension)
                .Select(f => Path.GetFileName(f)[..^StampSerializer.FileExtension.Length]).Order(StringComparer.Ordinal)]
            : [];

    /// <summary>The file a stamp named <paramref name="name"/> is kept in: characters a file name cannot have become <c>_</c>, and no name is "stamp".</summary>
    public static string PathOf(string directory, string name)
    {
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim().TrimEnd('.');
        return Path.Combine(directory, (safe.Length == 0 ? "stamp" : safe) + StampSerializer.FileExtension);
    }

    /// <summary>Writes the fragment as the stamp <paramref name="name"/> (over one of that name), making the folder. Returns the file's path.</summary>
    public static string Save(string directory, string name, MapFragment fragment)
    {
        Directory.CreateDirectory(directory);
        var path = PathOf(directory, name);
        StampSerializer.Save(StampSerializer.ToDocument(fragment, name.Trim()), path);
        return path;
    }

    /// <summary>
    /// The stamp <paramref name="name"/> as a fragment for <paramref name="target"/>. Throws <see cref="FormatException"/> for a file that is
    /// no stamp and <see cref="IOException"/> for one that cannot be read.
    /// </summary>
    public static MapFragment Load(string directory, string name, GameState target) =>
        StampSerializer.ToFragment(StampSerializer.Load(PathOf(directory, name)), target);
}
