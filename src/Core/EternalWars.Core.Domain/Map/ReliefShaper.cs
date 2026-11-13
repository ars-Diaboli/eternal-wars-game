using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Map;

/// <summary>
/// Shapes the ground's height (ADR-181, ADR-182): from terrain, and levelled under objects. Pure and deterministic,
/// no randomness; the generator adds its noise on top. Looks only: no rule reads the result.
/// </summary>
public static class ReliefShaper
{
    /// <summary>How far, in corners, a height blends into its neighbours (about 3 tiles, ADR-182 e).</summary>
    public const int BlendRadius = 3;

    /// <summary>Gives every corner of every level its terrains' blended height (<see cref="FromTerrain(AdventureMap, int)"/>).</summary>
    public static void FromTerrain(AdventureMap map)
    {
        for (var level = 0; level < map.Levels; level++)
        {
            FromTerrain(map, level);
        }
    }

    /// <summary>
    /// Gives every corner of <paramref name="level"/> its terrains' height (<see cref="ReliefTable"/>): the mean of the
    /// tiles around it, blurred over <see cref="BlendRadius"/> corners so terrains meet without a step, then held to
    /// <see cref="ReliefLimits"/>. Corners inside flat terrain (shallow water, ice) keep its own height.
    /// </summary>
    public static void FromTerrain(AdventureMap map, int level) => FromTerrain(map, level, CornerArea.Whole(map));

    /// <summary>
    /// <see cref="FromTerrain(AdventureMap, int)"/> for the corners in <paramref name="area"/> only (the editor's brush and "Auto height"): each
    /// takes exactly the height a whole-level reshape would give it, the corners outside keep theirs.
    /// </summary>
    public static void FromTerrain(AdventureMap map, int level, CornerArea area)
    {
        area = area.Clip(map);
        if (area.IsEmpty)
        {
            return;
        }

        // The blur reads BlendRadius corners either way, so the window around the area holds every value the area needs.
        var window = area.Grow(BlendRadius).Clip(map);
        var heights = Blur(TerrainHeights(map, level, window), window);
        var stride = window.Right - window.Left + 1;
        for (var cy = area.Top; cy <= area.Bottom; cy++)
        {
            for (var cx = area.Left; cx <= area.Right; cx++)
            {
                var value = FlatHeight(map, level, cx, cy) ?? heights[((cy - window.Top) * stride) + cx - window.Left];
                map.SetCorner(level, cx, cy, ReliefLimits.Clamp(map, level, cx, cy, (int)Math.Round(value, MidpointRounding.AwayFromZero)));
            }
        }
    }

    /// <summary>
    /// Levels the ground under an object (ADR-182 f): every corner of the <paramref name="footprint"/> tiles takes the
    /// height of the <paramref name="entrance"/> tile's centre; the ring of corners around them moves halfway, so no step
    /// shows. All tiles are on the entrance's level.
    /// </summary>
    public static void Level(AdventureMap map, IEnumerable<MapPosition> footprint, MapPosition entrance) =>
        Level(map, [Plan(map, footprint, entrance)]);

    /// <summary>
    /// Gives a map built in code its ground (ADR-182 j): every level from terrain, then its objects levelled
    /// (<see cref="Level(AdventureMap, IEnumerable{MapObject})"/>).
    /// </summary>
    public static void Shape(AdventureMap map, IEnumerable<MapObject> objects)
    {
        FromTerrain(map);
        Level(map, objects);
    }

    /// <summary>
    /// Levels the ground under every multi-tile object with an entrance (<see cref="Level(AdventureMap, IEnumerable{MapPosition}, MapPosition)"/>);
    /// one-tile objects and scenery follow the ground. Each takes the height the ground had before any was levelled, and one object's ring never
    /// moves another's footprint.
    /// </summary>
    public static void Level(AdventureMap map, IEnumerable<MapObject> objects) => Level(map, objects, []);

    /// <summary>
    /// <see cref="Level(AdventureMap, IEnumerable{MapObject})"/> for some objects of a map (the editor: one placed object, or those near
    /// reshaped ground); the levelled footprints of <paramref name="others"/> stay as they are.
    /// </summary>
    public static void Level(AdventureMap map, IEnumerable<MapObject> objects, IEnumerable<MapObject> others) =>
        Level(map, [.. objects.Where(IsLevelled).Select(o => Plan(map, Footprint(o), o.VisitPosition))],
            others.Where(IsLevelled).SelectMany(o => Corners(Footprint(o), o.VisitPosition).Select(c => (o.VisitPosition.Level, c.X, c.Y))));

    /// <summary>Whether the ground under the object is levelled: a footprint of more than one tile, with an entrance.</summary>
    public static bool IsLevelled(MapObject mapObject) => mapObject.Definition.Footprint is { HasVisit: true, OccupiedCells.Count: > 1 };

    /// <summary>The corners a levelled object flattens and the ring it feathers (<see cref="Level(AdventureMap, IEnumerable{MapObject})"/>).</summary>
    public static CornerArea Reach(MapObject mapObject) => CornerArea.Of(Footprint(mapObject).Append(mapObject.VisitPosition)).Grow(1);

    private static IEnumerable<MapPosition> Footprint(MapObject mapObject) => mapObject.OccupiedCells().Select(c => c.Position);

    /// <summary>The corners of the footprint's and the entrance's tiles.</summary>
    private static HashSet<(int X, int Y)> Corners(IEnumerable<MapPosition> footprint, MapPosition entrance)
    {
        var corners = new HashSet<(int X, int Y)>();
        foreach (var tile in footprint.Append(entrance))
        {
            for (var dy = 0; dy <= 1; dy++)
            {
                for (var dx = 0; dx <= 1; dx++)
                {
                    corners.Add((tile.X + dx, tile.Y + dy));
                }
            }
        }

        return corners;
    }

    private static Levelling Plan(AdventureMap map, IEnumerable<MapPosition> footprint, MapPosition entrance)
    {
        var corners = Corners(footprint, entrance);
        var ring = new HashSet<(int X, int Y)>();
        foreach (var (x, y) in corners)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (!corners.Contains((x + dx, y + dy)))
                    {
                        ring.Add((x + dx, y + dy));
                    }
                }
            }
        }

        var target = (int)Math.Round(map.GroundHeightAt(entrance) * ReliefLimits.StepsPerCell, MidpointRounding.AwayFromZero);
        return new Levelling(entrance.Level, target, corners, ring);
    }

    /// <summary>
    /// Feathers every ring first, then flattens every footprint, so no ring undoes a footprint; rings leave the <paramref name="kept"/> corners
    /// (other objects' footprints) alone too.
    /// </summary>
    private static void Level(AdventureMap map, IReadOnlyList<Levelling> levellings, IEnumerable<(int Floor, int X, int Y)>? kept = null)
    {
        var flat = levellings.SelectMany(l => l.Corners.Select(c => (l.Floor, c.X, c.Y))).Concat(kept ?? []).ToHashSet();
        foreach (var (floor, target, _, ring) in levellings)
        {
            foreach (var (x, y) in ring.Where(c => map.ContainsCorner(floor, c.X, c.Y) && !flat.Contains((floor, c.X, c.Y))))
            {
                var halfway = (int)Math.Round((map.GetCorner(floor, x, y) + target) / 2.0, MidpointRounding.AwayFromZero);
                map.SetCorner(floor, x, y, ReliefLimits.Clamp(map, floor, x, y, halfway));
            }
        }

        foreach (var (floor, target, corners, _) in levellings)
        {
            foreach (var (x, y) in corners.Where(c => map.ContainsCorner(floor, c.X, c.Y)))
            {
                map.SetCorner(floor, x, y, ReliefLimits.Clamp(map, floor, x, y, target));
            }
        }
    }

    /// <summary>The mean table height of the tiles around each corner of <paramref name="window"/>, row by row.</summary>
    private static double[] TerrainHeights(AdventureMap map, int level, CornerArea window)
    {
        var stride = window.Right - window.Left + 1;
        var heights = new double[stride * (window.Bottom - window.Top + 1)];
        for (var cy = window.Top; cy <= window.Bottom; cy++)
        {
            for (var cx = window.Left; cx <= window.Right; cx++)
            {
                double sum = 0;
                var count = 0;
                foreach (var terrain in TilesAround(map, level, cx, cy))
                {
                    sum += ReliefTable.Of(terrain).Base;
                    count++;
                }

                heights[((cy - window.Top) * stride) + cx - window.Left] = sum / count;
            }
        }

        return heights;
    }

    /// <summary>The flat terrain's height when every tile around the corner is flat terrain, else null.</summary>
    private static double? FlatHeight(AdventureMap map, int level, int cx, int cy)
    {
        double sum = 0;
        var count = 0;
        foreach (var terrain in TilesAround(map, level, cx, cy))
        {
            if (!ReliefTable.IsFlat(terrain))
            {
                return null;
            }

            sum += ReliefTable.Of(terrain).Base;
            count++;
        }

        return sum / count;
    }

    private static IEnumerable<TerrainType> TilesAround(AdventureMap map, int level, int cx, int cy)
    {
        for (var y = Math.Max(cy - 1, 0); y <= Math.Min(cy, map.Height - 1); y++)
        {
            for (var x = Math.Max(cx - 1, 0); x <= Math.Min(cx, map.Width - 1); x++)
            {
                yield return map.GetTerrain(new(x, y, level));
            }
        }
    }

    /// <summary>
    /// A separable triangle blur of radius <see cref="BlendRadius"/> over the window's values; its edges repeat the border value (exact at the
    /// map's edges; inside the map only the window's rim is off, which <see cref="FromTerrain(AdventureMap, int, CornerArea)"/> does not read).
    /// </summary>
    private static double[] Blur(double[] values, CornerArea window)
    {
        var width = window.Right - window.Left + 1;
        var height = window.Bottom - window.Top + 1;
        var across = new double[values.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                across[(y * width) + x] = Kernel(i => values[(y * width) + Math.Clamp(x + i, 0, width - 1)]);
            }
        }

        var result = new double[values.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                result[(y * width) + x] = Kernel(i => across[(Math.Clamp(y + i, 0, height - 1) * width) + x]);
            }
        }

        return result;
    }

    private static double Kernel(Func<int, double> at)
    {
        double sum = 0;
        double weights = 0;
        for (var i = -BlendRadius; i <= BlendRadius; i++)
        {
            var weight = BlendRadius + 1 - Math.Abs(i);
            sum += weight * at(i);
            weights += weight;
        }

        return sum / weights;
    }

    /// <summary>One object's levelling: the corners made flat at <paramref name="Target"/> and the ring around them, on level <paramref name="Floor"/>.</summary>
    private sealed record Levelling(int Floor, int Target, HashSet<(int X, int Y)> Corners, HashSet<(int X, int Y)> Ring);
}
