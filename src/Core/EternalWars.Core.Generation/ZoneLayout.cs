namespace EternalWars.Core.Generation;

/// <summary>
/// The first stage of the generator (ADR-064): which zone every tile of the map belongs to, per level. Zones are numbered as
/// in the template (<see cref="ZoneIds"/>). A layout is only an area map: terrain, towns and objects come in later stages.
/// </summary>
public sealed class ZoneLayout
{
    private readonly int[][] _cells;

    private readonly TemplateSymmetry _symmetry;
    private readonly int[]? _image;

    internal ZoneLayout(int size, IReadOnlyList<string> zoneIds, int[][] cells, IReadOnlyList<(string From, string To)> unmet,
        TemplateSymmetry symmetry = TemplateSymmetry.None, int[]? image = null)
    {
        Size = size;
        ZoneIds = zoneIds;
        _cells = cells;
        UnmetConnections = unmet;
        _symmetry = image is null ? TemplateSymmetry.None : symmetry;
        _image = image;
    }

    /// <summary>The symmetry the layout was made with (a random axis resolved; <see cref="TemplateSymmetry.None"/> for a template without one).</summary>
    public TemplateSymmetry Symmetry => _symmetry;

    /// <summary>A tile's image under one step of a symmetry on a map of this side.</summary>
    internal static (int X, int Y) Image(TemplateSymmetry symmetry, int x, int y, int size) => symmetry switch
    {
        TemplateSymmetry.Vertical => (size - 1 - x, y),
        TemplateSymmetry.Horizontal => (x, size - 1 - y),
        TemplateSymmetry.Diagonal => (y, x),
        TemplateSymmetry.HalfTurn => (size - 1 - x, size - 1 - y),
        TemplateSymmetry.QuarterTurn => (size - 1 - y, x),
        _ => (x, y),
    };

    /// <summary>Width and height of every level.</summary>
    public int Size { get; }

    public int Levels => _cells.Length;

    /// <summary>Zone ids in template order; a tile's zone is an index into this list.</summary>
    public IReadOnlyList<string> ZoneIds { get; }

    /// <summary>Connections between zones of one level whose zones do not touch (the layout could not make them; normally empty).</summary>
    public IReadOnlyList<(string From, string To)> UnmetConnections { get; }

    public int ZoneAt(int x, int y, int level) => _cells[level][(y * Size) + x];

    public string IdAt(int x, int y, int level) => ZoneIds[ZoneAt(x, y, level)];

    /// <summary>Number of tiles of a zone.</summary>
    public int Area(int zone) => _cells.Sum(level => level.Count(c => c == zone));

    /// <summary>
    /// The zone's tiles, in reading order; on a mirrored layout (ADR-117) a zone that is the image of an earlier one lists the images of that
    /// zone's tiles in its order (then any tiles of its own the clean-up left without a counterpart), so the stages, drawing the same rolls for
    /// both, pick mirrored places.
    /// </summary>
    public IEnumerable<(int X, int Y, int Level)> Tiles(int zone)
    {
        if (_image is not null && Source(zone) is { } source && source.Zone != zone)
        {
            var seen = new HashSet<(int, int, int)>();
            foreach (var (x, y, level) in ReadingOrder(source.Zone))
            {
                var (ix, iy) = (x, y);
                for (var step = 0; step < source.Steps; step++)
                {
                    (ix, iy) = Image(_symmetry, ix, iy, Size);
                }

                if (_cells[level][(iy * Size) + ix] == zone && seen.Add((ix, iy, level)))
                {
                    yield return (ix, iy, level);
                }
            }

            foreach (var tile in ReadingOrder(zone).Where(t => !seen.Contains(t)))
            {
                yield return tile;
            }

            yield break;
        }

        foreach (var tile in ReadingOrder(zone))
        {
            yield return tile;
        }
    }

    /// <summary>
    /// The tile in the frame of the first image (ADR-117): the tile <paramref name="steps"/> symmetry steps back. Stages sort places by it, so a
    /// zone or connection and its images list them alike.
    /// </summary>
    public (int X, int Y) Unmirror(int x, int y, int steps)
    {
        var order = MapTemplate.Order(_symmetry);
        for (var step = 0; step < (order - (steps % order)) % order; step++)
        {
            (x, y) = Image(_symmetry, x, y, Size);
        }

        return (x, y);
    }

    /// <summary>How many symmetry steps lead from the first zone of the zone's cycle to it (0 on a layout without symmetry).</summary>
    public int MirrorSteps(int zone) => _image is null ? 0 : Source(zone)!.Value.Steps;

    /// <summary>
    /// How many symmetry steps turn the zone pair (<paramref name="firstA"/>, <paramref name="firstB"/>) into (<paramref name="a"/>, <paramref name="b"/>),
    /// either way round; 0 when none does or without symmetry.
    /// </summary>
    public int MirrorSteps(int firstA, int firstB, int a, int b)
    {
        if (_image is null)
        {
            return 0;
        }

        var (x, y) = (firstA, firstB);
        for (var step = 0; step < MapTemplate.Order(_symmetry); step++)
        {
            if ((x == a && y == b) || (x == b && y == a))
            {
                return step;
            }

            (x, y) = (_image[x], _image[y]);
        }

        return 0;
    }

    /// <summary>The first zone of the zone's cycle and how many symmetry steps lead from it to the zone.</summary>
    private (int Zone, int Steps)? Source(int zone)
    {
        var path = new List<int> { zone };
        for (var next = _image![zone]; next != zone && path.Count <= _image.Length; next = _image[next])
        {
            path.Add(next);
        }

        var first = path.Min();
        if (first == zone)
        {
            return (zone, 0);
        }

        // Steps forward from the first to the zone: walk the cycle from the first.
        var steps = 0;
        for (var at = first; at != zone && steps <= _image.Length; at = _image[at])
        {
            steps++;
        }

        return (first, steps);
    }

    private IEnumerable<(int X, int Y, int Level)> ReadingOrder(int zone)
    {
        for (var level = 0; level < Levels; level++)
        {
            for (var i = 0; i < _cells[level].Length; i++)
            {
                if (_cells[level][i] == zone)
                {
                    yield return (i % Size, i / Size, level);
                }
            }
        }
    }

    /// <summary>Whether two zones have tiles side by side (on one level).</summary>
    public bool Touch(int a, int b)
    {
        foreach (var cells in _cells)
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var here = cells[(y * Size) + x];
                    if ((here == a || here == b) && (Next(cells, x + 1, y) is { } right && Pair(here, right, a, b)
                                                      || Next(cells, x, y + 1) is { } below && Pair(here, below, a, b)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>One line of text per row of a level, a character per zone (<c>0-9, a-z, A-Z</c>), to look at a layout.</summary>
    public string ToAscii(int level)
    {
        const string symbols = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var lines = new string[Size];
        for (var y = 0; y < Size; y++)
        {
            lines[y] = string.Create(Size, (y, level), (span, state) =>
            {
                for (var x = 0; x < span.Length; x++)
                {
                    var zone = _cells[state.level][(state.y * Size) + x];
                    span[x] = zone < symbols.Length ? symbols[zone] : '?';
                }
            });
        }

        return string.Join('\n', lines);
    }

    private int? Next(int[] cells, int x, int y) => x < Size && y < Size ? cells[(y * Size) + x] : null;

    private static bool Pair(int one, int other, int a, int b) => (one == a && other == b) || (one == b && other == a);
}
