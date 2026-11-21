using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Visibility;

namespace EternalWars.Core.Generation;

/// <summary>Where a zone's name goes on a <see cref="ZonePicture"/>: the zone's tile nearest the middle of its tiles on that level.</summary>
public readonly record struct ZoneLabel(string Id, int X, int Y);

/// <summary>
/// A generated map seen from above for the template editor's preview and the batch's thumbnails (ADR-274): each tile as the editor's minimap
/// shows it (terrain, objects, owners; the front-end picks the colours) and the zone the generator laid it in, with the borders between zones
/// and where each zone's name goes. Built off the main thread; it only reads the map.
/// </summary>
public sealed class ZonePicture
{
    private readonly MinimapCell[][] _cells;
    private readonly int[][] _zones;
    private readonly ZoneLabel[][] _labels;

    private ZonePicture(GameState state, MinimapCell[][] cells, int[][] zones, IReadOnlyList<string> zoneIds, ZoneLabel[][] labels)
    {
        State = state;
        _cells = cells;
        _zones = zones;
        ZoneIds = zoneIds;
        _labels = labels;
    }

    /// <summary>The map pictured (its players give the owners' colours).</summary>
    public GameState State { get; }

    public int Width => State.Map.Width;

    public int Height => State.Map.Height;

    public int Levels => _cells.Length;

    /// <summary>The template's zone ids; <see cref="Zone"/> is an index into them.</summary>
    public IReadOnlyList<string> ZoneIds { get; }

    public MinimapCell Cell(int x, int y, int level) => _cells[level][(y * Width) + x];

    public int Zone(int x, int y, int level) => _zones[level][(y * Width) + x];

    /// <summary>The tile to the right lies in another zone.</summary>
    public bool BorderRight(int x, int y, int level) => x + 1 < Width && Zone(x + 1, y, level) != Zone(x, y, level);

    /// <summary>The tile below lies in another zone.</summary>
    public bool BorderBelow(int x, int y, int level) => y + 1 < Height && Zone(x, y + 1, level) != Zone(x, y, level);

    /// <summary>A label per zone with tiles on <paramref name="level"/>, in template order.</summary>
    public IReadOnlyList<ZoneLabel> Labels(int level) => _labels[level];

    /// <summary>The picture of a generated map.</summary>
    public static ZonePicture Of(EditorMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Of(map.Scenario, map.Generation.Map.Layout);
    }

    /// <summary>The picture of <paramref name="state"/>'s map, whose zones <paramref name="layout"/> gives (it must be of the map's size and levels).</summary>
    public static ZonePicture Of(GameState state, ZoneLayout layout)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(layout);
        var map = state.Map;
        if (layout.Size != map.Width || layout.Size != map.Height || layout.Levels != map.Levels)
        {
            throw new ArgumentException($"The layout is {layout.Size}×{layout.Size}×{layout.Levels}, the map {map.Width}×{map.Height}×{map.Levels}.", nameof(layout));
        }

        var cells = new MinimapCell[map.Levels][];
        var zones = new int[map.Levels][];
        var labels = new ZoneLabel[map.Levels][];
        for (var level = 0; level < map.Levels; level++)
        {
            cells[level] = Minimap.Build(state, viewer: null, level);
            var zone = zones[level] = new int[map.Width * map.Height];
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    zone[(y * map.Width) + x] = layout.ZoneAt(x, y, level);
                }
            }

            labels[level] = Place(zone, map.Width, layout.ZoneIds);
        }

        return new ZonePicture(state, cells, zones, layout.ZoneIds, labels);
    }

    /// <summary>Each zone's label: of its tiles, the one nearest their mean (so a crescent's label stays on the crescent).</summary>
    private static ZoneLabel[] Place(int[] zone, int width, IReadOnlyList<string> ids)
    {
        var sums = new (long X, long Y, int Count)[ids.Count];
        for (var i = 0; i < zone.Length; i++)
        {
            var (x, y) = (i % width, i / width);
            var sum = sums[zone[i]];
            sums[zone[i]] = (sum.X + x, sum.Y + y, sum.Count + 1);
        }

        var best = new (int Index, double Distance)[ids.Count];
        Array.Fill(best, (-1, double.MaxValue));
        for (var i = 0; i < zone.Length; i++)
        {
            var (sum, x, y) = (sums[zone[i]], i % width, i / width);
            var (dx, dy) = (x - ((double)sum.X / sum.Count), y - ((double)sum.Y / sum.Count));
            var distance = (dx * dx) + (dy * dy);
            if (distance < best[zone[i]].Distance)
            {
                best[zone[i]] = (i, distance);
            }
        }

        return [.. Enumerable.Range(0, ids.Count).Where(z => best[z].Index >= 0).Select(z => new ZoneLabel(ids[z], best[z].Index % width, best[z].Index / width))];
    }
}
