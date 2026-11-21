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
/// <c>B</c> blocked, <c>V</c> visit (entrance), <c>.</c> free. Example mine: <c>["BBB", "BVB"]</c>. Scenery (<see cref="Obstacle"/>) has no
/// entrance at all (ADR-079).
/// </summary>
public sealed class Footprint
{
    private readonly Footprint?[] _oriented = new Footprint?[8];

    public Footprint(IReadOnlyList<string> rows)
        : this(rows, needsVisit: true)
    {
    }

    private Footprint(IReadOnlyList<string> rows, bool needsVisit)
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

        if (needsVisit && cells.Count(c => c.Item3 == FootprintCell.Visit) != 1)
        {
            throw new FormatException("Footprint must contain exactly one visit cell 'V'.");
        }

        if (!needsVisit && (cells.Count == 0 || cells.Any(c => c.Item3 == FootprintCell.Visit)))
        {
            throw new FormatException("An obstacle's footprint has blocked cells 'B' and no visit cell.");
        }

        HasVisit = needsVisit;
        if (!needsVisit)
        {
            VisitOffset = (cells[0].Item1, cells[0].Item2); // no entrance: the first blocked cell stands for the object
        }

        Rows = rows.ToArray();
        OccupiedCells = cells;
    }

    public static Footprint Single { get; } = new(["V"]);

    /// <summary>A shape with no entrance that only blocks (scenery, ADR-079): <c>B</c> and <c>.</c> cells only.</summary>
    public static Footprint Obstacle(IReadOnlyList<string> rows) => new(rows, needsVisit: false);

    /// <summary>
    /// The shape lying as <paramref name="orientation"/> says (EDIT-6, ADR-252): mirrored left to right, then turned clockwise; a turned
    /// shape of 3 × 1 cells is 1 × 3. The same instance for the same orientation, this one for none.
    /// </summary>
    public Footprint Oriented(Orientation orientation)
    {
        if (orientation.IsNone)
        {
            return this;
        }

        var slot = orientation.Turns + (orientation.Mirrored ? 4 : 0);
        lock (_oriented)
        {
            return _oriented[slot] ??= new Footprint(Turned(orientation), HasVisit);
        }
    }

    private string[] Turned(Orientation orientation)
    {
        var rows = orientation.Mirrored ? Rows.Select(r => new string([.. r.Reverse()])).ToArray() : [.. Rows];
        for (var turn = 0; turn < orientation.Turns; turn++)
        {
            // Clockwise: the left column, read from the bottom, becomes the top row.
            var (width, height) = (rows[0].Length, rows.Length);
            rows = [.. Enumerable.Range(0, width).Select(x => new string([.. Enumerable.Range(0, height).Select(y => rows[height - 1 - y][x])]))];
        }

        return rows;
    }

    /// <summary>Whether heroes can visit the object; false for scenery.</summary>
    public bool HasVisit { get; }

    public IReadOnlyList<string> Rows { get; }

    public int Width => Rows[0].Length;

    public int Height => Rows.Count;

    public (int Dx, int Dy) VisitOffset { get; }

    /// <summary>Blocked and visit cells; free cells are omitted.</summary>
    public IReadOnlyList<(int Dx, int Dy, FootprintCell Cell)> OccupiedCells { get; }
}
