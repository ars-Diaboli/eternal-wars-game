namespace EternalWars.Core.Generation;

/// <summary>
/// The first stage of the generator (ADR-064): which zone every tile of the map belongs to, per level. Zones are numbered as
/// in the template (<see cref="ZoneIds"/>). A layout is only an area map: terrain, towns and objects come in later stages.
/// </summary>
public sealed class ZoneLayout
{
    private readonly int[][] _cells;

    internal ZoneLayout(int size, IReadOnlyList<string> zoneIds, int[][] cells, IReadOnlyList<(string From, string To)> unmet)
    {
        Size = size;
        ZoneIds = zoneIds;
        _cells = cells;
        UnmetConnections = unmet;
    }

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

    public IEnumerable<(int X, int Y, int Level)> Tiles(int zone)
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
