using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// Shapes the ground's height (ADR-182 g): raise, lower, level and smooth with a round brush, and heights from terrain. Heights are in
/// eighths of a cell width (<see cref="ReliefLimits.StepsPerCell"/>) and every result is held to <see cref="ReliefLimits"/>. Each call is one
/// undoable command (several in a stroke make one undo step) and returns how many corners it changed; one that changes none records nothing.
/// Looks only: no rule reads the heights (ADR-182 a).
/// </summary>
/// <remarks>
/// The brush is centred on a tile: with radius r the corners within r + ¾ tile of its centre take the full strength (radius 0 is the tile's
/// four corners), and the strength fades to nothing over one more tile.
/// </remarks>
public sealed class HeightEditor
{
    /// <summary>The strongest brush: a whole cell width per dab.</summary>
    public const int MaxStrength = ReliefLimits.StepsPerCell;

    /// <summary>The strength at which <see cref="Smooth"/> takes a corner all the way to its neighbours' mean.</summary>
    private const double FullSmoothing = 4;

    private readonly EditorContext _context;

    internal HeightEditor(EditorContext context) => _context = context;

    /// <summary>
    /// Whether painting terrain also gives the painted ground, and about <see cref="ReliefShaper.BlendRadius"/> corners around it, the new
    /// terrain's height (<see cref="TerrainEditor.Paint"/>). A setting of the session, not part of the map; on by default.
    /// </summary>
    public bool AutoHeight { get; set; } = true;

    private AdventureMap Map => _context.Scenario.Map;

    /// <summary>The ground's height at the tile's centre in eighths (rounded); what <see cref="Level"/> levels to when given this tile.</summary>
    public int HeightAt(MapPosition tile) =>
        (int)Math.Round(Map.GroundHeightAt(tile) * ReliefLimits.StepsPerCell, MidpointRounding.AwayFromZero);

    /// <summary>Raises the ground under the brush by up to <paramref name="strength"/> eighths.</summary>
    public int Raise(MapPosition center, int radius, int strength) =>
        Brush("Raise ground", center, radius, strength, (height, step) => height + step);

    /// <summary>Lowers the ground under the brush by up to <paramref name="strength"/> eighths.</summary>
    public int Lower(MapPosition center, int radius, int strength) =>
        Brush("Lower ground", center, radius, strength, (height, step) => height - step);

    /// <summary>
    /// Moves the ground under the brush towards <paramref name="target"/> eighths, by up to <paramref name="strength"/> a corner (the app passes
    /// <see cref="HeightAt"/> of the tile where a stroke started, so dragging spreads that height).
    /// </summary>
    public int Level(MapPosition center, int radius, int strength, int target) =>
        Brush("Level ground", center, radius, strength, (height, step) => height + Math.Clamp(target - height, -step, step));

    /// <summary>
    /// Moves each corner under the brush towards the mean of its (up to eight) neighbours, as they were before this dab: all the way at
    /// strength 4 or more, a quarter of the way per point below.
    /// </summary>
    public int Smooth(MapPosition center, int radius, int strength)
    {
        Check(radius, strength);
        if (!Map.Contains(center))
        {
            return 0;
        }

        var level = center.Level;
        return Run("Smooth ground", () =>
        {
            var targets = Footprint(center, radius)
                .Select(c => (c.X, c.Y, Height: (double)Map.GetCorner(level, c.X, c.Y) + ((Neighbours(level, c.X, c.Y) - Map.GetCorner(level, c.X, c.Y))
                    * Math.Min(1, c.Weight * strength / FullSmoothing))))
                .ToList();
            foreach (var (x, y, height) in targets)
            {
                Set(level, x, y, (int)Math.Round(height, MidpointRounding.AwayFromZero));
            }
        });
    }

    /// <summary>Gives the ground under the brush its terrains' height (<see cref="ReliefShaper.FromTerrain(AdventureMap, int, CornerArea)"/>), then
    /// levels again the objects whose ground it moved.</summary>
    public int FromTerrain(MapPosition center, int radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        if (!Map.Contains(center))
        {
            return 0;
        }

        var area = new CornerArea(center.X - radius, center.Y - radius, center.X + 1 + radius, center.Y + 1 + radius);
        return Run("Heights from terrain", () => Reshape(center.Level, area));
    }

    /// <summary>Gives the whole map, every level, its terrains' height and levels every object again (<see cref="ReliefShaper.Shape"/>).</summary>
    public int FromTerrain() => Run("Heights from terrain", () => ReliefShaper.Shape(Map, _context.Scenario.Objects));

    /// <summary>
    /// The ground edit for a terrain paint while <see cref="AutoHeight"/> is on: the painted tiles' corners and <see cref="ReliefShaper.BlendRadius"/>
    /// around them from terrain, objects there levelled again. Null while Auto height is off.
    /// </summary>
    internal GroundEdit? AfterPaint(IReadOnlyList<MapPosition> painted) =>
        AutoHeight
            ? new GroundEdit(_context, () =>
            {
                foreach (var level in painted.GroupBy(p => p.Level))
                {
                    Reshape(level.Key, CornerArea.Of(level).Grow(ReliefShaper.BlendRadius));
                }
            })
            : null;

    /// <summary>The ground edit that levels a placed object's footprint (none for one-tile objects); other objects' footprints stay.</summary>
    internal GroundEdit? Under(MapObject placed) =>
        ReliefShaper.IsLevelled(placed)
            ? new GroundEdit(_context, () => ReliefShaper.Level(Map, [placed], _context.Scenario.Objects.Where(o => o != placed)))
            : null;

    /// <summary>The area from terrain, then the objects whose levelled ground reaches into it levelled again.</summary>
    private void Reshape(int level, CornerArea area)
    {
        area = area.Clip(Map);
        ReliefShaper.FromTerrain(Map, level, area);
        var touched = _context.Scenario.Objects
            .Where(o => o.Position.Level == level && ReliefShaper.IsLevelled(o) && ReliefShaper.Reach(o).Overlaps(area))
            .ToList();
        if (touched.Count > 0)
        {
            ReliefShaper.Level(Map, touched, _context.Scenario.Objects.Except(touched));
        }
    }

    private int Brush(string description, MapPosition center, int radius, int strength, Func<int, int, int> next)
    {
        Check(radius, strength);
        if (!Map.Contains(center))
        {
            return 0;
        }

        return Run(description, () =>
        {
            foreach (var (x, y, weight) in Footprint(center, radius))
            {
                var step = (int)Math.Round(strength * weight, MidpointRounding.AwayFromZero);
                if (step > 0)
                {
                    Set(center.Level, x, y, next(Map.GetCorner(center.Level, x, y), step));
                }
            }
        });
    }

    /// <summary>Runs the shaping as one command and records it when it changed a corner; returns how many it changed.</summary>
    private int Run(string description, Action shape)
    {
        var edit = new GroundEdit(_context, shape);
        edit.Apply();
        if (edit.Count > 0)
        {
            _context.Done(new DelegateCommand(description, edit.Apply, edit.Revert));
        }

        return edit.Count;
    }

    private void Set(int level, int x, int y, int eighths) => Map.SetCorner(level, x, y, ReliefLimits.Clamp(Map, level, x, y, eighths));

    /// <summary>The brush's corners on the map and how strongly each is touched (0 … 1).</summary>
    private IEnumerable<(int X, int Y, double Weight)> Footprint(MapPosition center, int radius)
    {
        var full = radius + 0.75;
        var none = full + 1;
        var reach = (int)Math.Ceiling(none);
        for (var y = center.Y + 1 - reach; y <= center.Y + reach; y++)
        {
            for (var x = center.X + 1 - reach; x <= center.X + reach; x++)
            {
                var distance = Math.Sqrt(Math.Pow(x - (center.X + 0.5), 2) + Math.Pow(y - (center.Y + 0.5), 2));
                if (distance >= none || !Map.ContainsCorner(center.Level, x, y))
                {
                    continue;
                }

                var t = Math.Min(1, (none - distance) / (none - full));
                yield return (x, y, t * t * (3 - (2 * t)));
            }
        }
    }

    /// <summary>The mean height of the corner's neighbours on the map.</summary>
    private double Neighbours(int level, int cx, int cy)
    {
        double sum = 0;
        var count = 0;
        for (var y = cy - 1; y <= cy + 1; y++)
        {
            for (var x = cx - 1; x <= cx + 1; x++)
            {
                if ((x, y) != (cx, cy) && Map.ContainsCorner(level, x, y))
                {
                    sum += Map.GetCorner(level, x, y);
                    count++;
                }
            }
        }

        return sum / count;
    }

    private static void Check(int radius, int strength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        ArgumentOutOfRangeException.ThrowIfLessThan(strength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(strength, MaxStrength);
    }
}
