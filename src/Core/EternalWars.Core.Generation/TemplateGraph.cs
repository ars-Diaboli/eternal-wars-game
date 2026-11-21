namespace EternalWars.Core.Generation;

/// <summary>A line of zones in the template graph: a row (one height, spread left to right) or a column (one x, spread top to bottom).</summary>
public enum GraphLine
{
    Row,
    Column,
}

/// <summary>
/// The template graph's geometry (ADR-270), engine-free: grid snapping, aligning and spreading zones. Graph places are in graph units, kept to
/// two decimals like the editor writes them; they do not change the map.
/// </summary>
public static class TemplateGraph
{
    /// <summary>The graph editor's grid: half a graph unit.</summary>
    public const double Grid = 0.5;

    /// <summary>The room a zone's card takes at the editor's usual zoom, in graph units: zones nearer than this across and down overlap.</summary>
    public static readonly (double X, double Y) CardRoom = (2.2, 1.3);

    /// <summary>Whether a zone at <paramref name="place"/> would overlap a card at one of <paramref name="places"/>.</summary>
    public static bool Crowded((double X, double Y) place, IEnumerable<(double X, double Y)> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        return places.Any(p => Math.Abs(p.X - place.X) < CardRoom.X && Math.Abs(p.Y - place.Y) < CardRoom.Y);
    }

    /// <summary><paramref name="value"/> on the nearest line of a grid of <paramref name="grid"/> units; unchanged (but rounded) without a grid (0).</summary>
    public static double Snap(double value, double grid) =>
        grid <= 0 ? Math.Round(value, 2) : Math.Round(Math.Round(value / grid) * grid, 2);

    /// <summary>A zone's graph place (0, 0 when it has none).</summary>
    public static (double X, double Y) Place(TemplateZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return (zone.GraphX ?? 0, zone.GraphY ?? 0);
    }

    /// <summary>The middle of the box around the zones' places; 0, 0 for none.</summary>
    public static (double X, double Y) Centre(IReadOnlyCollection<TemplateZone> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (zones.Count == 0)
        {
            return (0, 0);
        }

        var places = zones.Select(Place).ToList();
        return ((places.Min(p => p.X) + places.Max(p => p.X)) / 2, (places.Min(p => p.Y) + places.Max(p => p.Y)) / 2);
    }

    /// <summary>
    /// The zones on one <paramref name="line"/>: a row gets the zones' mean height, a column their mean x, snapped to <paramref name="grid"/>
    /// (0: none); the other coordinate stays.
    /// </summary>
    public static IReadOnlyList<TemplateZone> Align(IReadOnlyList<TemplateZone> zones, GraphLine line, double grid = 0)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (zones.Count == 0)
        {
            return zones;
        }

        var places = zones.Select(Place).ToList();
        var at = Snap(line == GraphLine.Row ? places.Average(p => p.Y) : places.Average(p => p.X), grid);
        return [.. zones.Select((z, i) => line == GraphLine.Row ? z with { GraphX = places[i].X, GraphY = at } : z with { GraphX = at, GraphY = places[i].Y })];
    }

    /// <summary>
    /// The zones evenly spaced along a <paramref name="line"/>: the first and the last (by x for a row, by y for a column) stay, the others
    /// get even steps between them in the same order; the other coordinate stays. The result is in the order given.
    /// </summary>
    public static IReadOnlyList<TemplateZone> Spread(IReadOnlyList<TemplateZone> zones, GraphLine line)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (zones.Count < 3)
        {
            return zones;
        }

        double Along(TemplateZone z) => line == GraphLine.Row ? Place(z).X : Place(z).Y;
        var order = zones.Select((z, i) => (Zone: z, Index: i)).OrderBy(e => Along(e.Zone)).ThenBy(e => e.Index).ToList();
        var (first, last) = (Along(order[0].Zone), Along(order[^1].Zone));
        var step = (last - first) / (order.Count - 1);
        var result = zones.ToArray();
        for (var rank = 0; rank < order.Count; rank++)
        {
            var (zone, index) = order[rank];
            var at = rank == order.Count - 1 ? last : Math.Round(first + (step * rank), 2);
            var (x, y) = Place(zone);
            result[index] = line == GraphLine.Row ? zone with { GraphX = at, GraphY = y } : zone with { GraphX = x, GraphY = at };
        }

        return result;
    }
}
