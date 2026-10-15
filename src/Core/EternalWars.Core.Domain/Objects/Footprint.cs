namespace EternalWars.Core.Domain.Objects;

public enum FootprintCell
{
    /// <summary>Part of the object's bounding box but walkable (decoration overhang).</summary>
    Free,

    /// <summary>Impassable.</summary>
    Blocked,

    /// <summary>The entrance: walking onto it visits the object. Exactly one per footprint.</summary>
    Visit,
}

/// <summary>
/// Shape of a map object, relative to its top-left tile. Written as rows of characters:
/// <c>B</c> blocked, <c>V</c> visit (entrance), <c>.</c> free. Example mine: <c>["BBB", "BVB"]</c>.
/// </summary>
public sealed class Footprint
{
    public Footprint(IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0 || rows[0].Length == 0 || rows.Any(r => r.Length != rows[0].Length))
        {
            throw new FormatException("Footprint rows must be non-empty and of equal length.");
        }

        var cells = new List<(int, int, FootprintCell)>();
        for (var dy = 0; dy < rows.Count; dy++)
        {
            for (var dx = 0; dx < rows[dy].Length; dx++)
            {
                var cell = rows[dy][dx] switch
                {
                    '.' => FootprintCell.Free,
                    'B' => FootprintCell.Blocked,
                    'V' => FootprintCell.Visit,
                    var c => throw new FormatException($"Unknown footprint cell '{c}'."),
                };

                if (cell == FootprintCell.Visit)
                {
                    VisitOffset = (dx, dy);
                }

                if (cell != FootprintCell.Free)
                {
                    cells.Add((dx, dy, cell));
                }
            }
        }

        if (cells.Count(c => c.Item3 == FootprintCell.Visit) != 1)
        {
            throw new FormatException("Footprint must contain exactly one visit cell 'V'.");
        }

        Rows = rows.ToArray();
        OccupiedCells = cells;
    }

    public static Footprint Single { get; } = new(["V"]);

    public IReadOnlyList<string> Rows { get; }

    public int Width => Rows[0].Length;

    public int Height => Rows.Count;

    public (int Dx, int Dy) VisitOffset { get; }

    /// <summary>Blocked and visit cells; free cells are omitted.</summary>
    public IReadOnlyList<(int Dx, int Dy, FootprintCell Cell)> OccupiedCells { get; }
}
