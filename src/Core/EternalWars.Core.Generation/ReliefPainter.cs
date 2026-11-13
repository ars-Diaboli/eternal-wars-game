using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Generation;

/// <summary>
/// The generator's relief pass (ADR-182 d): the ground's height from terrain (<see cref="ReliefShaper.FromTerrain(AdventureMap)"/>), then a
/// little noise so no two maps look alike, and dunes on sand; then the limits (<see cref="ReliefLimits"/>). Draws only from its own streams
/// (<c>heights</c> per level, <c>dunes</c> per zone), so the rest of a generated map is the same with or without it. Looks only.
/// <list type="bullet">
/// <item>Noise: smooth value noise over a lattice <see cref="NoiseSpacing"/> corners apart, up to <see cref="NoiseAmplitude"/> eighths
/// (¼ cell) up or down. None on corners inside flat terrain (shallow water, ice).</item>
/// <item>Dunes: on corners with only sand around them, rows <see cref="DuneWavelength"/> corners apart, up to <see cref="DuneAmplitude"/>
/// eighths; each zone draws its rows' direction and phase, and the noise bends them.</item>
/// </list>
/// Only arithmetic (no <see cref="Math.Sin"/>), so heights are the same on every platform.
/// </summary>
public static class ReliefPainter
{
    /// <summary>The noise's reach either way, in eighths: ¼ cell.</summary>
    public const double NoiseAmplitude = 2;

    /// <summary>How far apart the noise's lattice points are, in corners.</summary>
    public const int NoiseSpacing = 6;

    /// <summary>The dunes' reach either way, in eighths: about 0.15 cell.</summary>
    public const double DuneAmplitude = 1.2;

    /// <summary>How far apart two dune crests are, in corners.</summary>
    public const double DuneWavelength = 5;

    private static readonly (int Dx, int Dy)[] DuneDirections = [(1, 0), (0, 1), (1, 1), (1, -1)];

    /// <summary>
    /// Shapes every level of <paramref name="map"/> from its terrain plus noise and dunes drawn from <paramref name="seed"/>.
    /// <paramref name="zoneOf"/> gives a tile's zone, whose dunes run one way (left out: one zone per map).
    /// </summary>
    public static void Paint(AdventureMap map, ulong seed, Func<MapPosition, int>? zoneOf = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        zoneOf ??= _ => 0;
        ReliefShaper.FromTerrain(map);
        var streams = new SeedStreams(seed);
        var dunes = new Dictionary<int, Dune>();
        for (var level = 0; level < map.Levels; level++)
        {
            var noise = new ValueNoise(streams.For("heights", level), map.Width + 1, map.Height + 1);
            for (var cy = 0; cy <= map.Height; cy++)
            {
                for (var cx = 0; cx <= map.Width; cx++)
                {
                    var around = TilesAround(map, level, cx, cy).ToList();
                    if (around.All(ReliefTable.IsFlat))
                    {
                        continue;
                    }

                    var bend = noise.At(cx, cy);
                    var value = map.GetCorner(level, cx, cy) + (NoiseAmplitude * bend);
                    if (around.All(t => t == TerrainType.Sand))
                    {
                        var zone = zoneOf(new MapPosition(Math.Min(cx, map.Width - 1), Math.Min(cy, map.Height - 1), level));
                        if (!dunes.TryGetValue(zone, out var dune))
                        {
                            dunes[zone] = dune = Dune.Draw(streams.For("dunes", zone));
                        }

                        value += DuneAmplitude * dune.At(cx, cy, bend);
                    }

                    map.SetCorner(level, cx, cy, ReliefLimits.Clamp(map, level, cx, cy, (int)Math.Round(value, MidpointRounding.AwayFromZero)));
                }
            }
        }
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

    /// <summary>A smooth step from 0 to 1 over 0 … 1.</summary>
    private static double Smooth(double t) => t * t * (3 - (2 * t));

    /// <summary>Random values in −1 … 1 on a lattice, blended smoothly between.</summary>
    private sealed class ValueNoise
    {
        private readonly double[] _values;
        private readonly int _stride;

        public ValueNoise(GameRandom random, int width, int height)
        {
            _stride = (width / NoiseSpacing) + 2;
            var rows = (height / NoiseSpacing) + 2;
            _values = new double[_stride * rows];
            for (var i = 0; i < _values.Length; i++)
            {
                _values[i] = (random.Next(2001) / 1000.0) - 1;
            }
        }

        public double At(int cx, int cy)
        {
            var (x, fx) = (cx / NoiseSpacing, Smooth((double)(cx % NoiseSpacing) / NoiseSpacing));
            var (y, fy) = (cy / NoiseSpacing, Smooth((double)(cy % NoiseSpacing) / NoiseSpacing));
            var top = Lerp(Value(x, y), Value(x + 1, y), fx);
            var bottom = Lerp(Value(x, y + 1), Value(x + 1, y + 1), fx);
            return Lerp(top, bottom, fy);
        }

        private double Value(int x, int y) => _values[(y * _stride) + x];

        private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
    }

    /// <summary>One zone's dune rows: their direction and where the first crest lies.</summary>
    private sealed record Dune(int Dx, int Dy, double Phase)
    {
        public static Dune Draw(GameRandom random)
        {
            var (dx, dy) = DuneDirections[random.Next(DuneDirections.Length)];
            return new Dune(dx, dy, random.Next(1000) / 1000.0);
        }

        /// <summary>The rows' height in −1 … 1 at a corner, shifted by up to half a row where <paramref name="bend"/> (−1 … 1) says.</summary>
        public double At(int cx, int cy, double bend)
        {
            var along = (((Dx * cx) + (Dy * cy)) / DuneWavelength) + Phase + (bend / 2);
            var t = along - Math.Floor(along);
            var triangle = (4 * Math.Abs(t - 0.5)) - 1; // 1 at a crest, −1 in a trough
            return triangle * (1.5 - (0.5 * triangle * triangle)); // rounded off at both
        }
    }
}
