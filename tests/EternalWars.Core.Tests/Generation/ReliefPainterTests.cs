using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The generator's relief pass (ADR-182 d): terrain heights, noise from its own stream, dunes on sand, objects levelled.</summary>
public class ReliefPainterTests
{
    [Fact]
    public void TheSameSeed_GivesTheSameGround_AnotherSeedAnother()
    {
        var a = Painted(TerrainType.Grass, 1);
        var b = Painted(TerrainType.Grass, 1);
        var c = Painted(TerrainType.Grass, 2);

        Assert.Equal(Corners(a), Corners(b));
        Assert.NotEqual(Corners(a), Corners(c));
    }

    [Fact]
    public void Grass_VariesByAtMostAQuarterCell_FromItsTerrainHeight()
    {
        var map = Painted(TerrainType.Grass, 7);
        var plain = Shaped(TerrainType.Grass);

        var offsets = Corners(map).Zip(Corners(plain), (a, b) => a - b).ToList();

        Assert.All(offsets, o => Assert.InRange(o, -ReliefPainter.NoiseAmplitude, ReliefPainter.NoiseAmplitude));
        Assert.True(offsets.Distinct().Count() >= 3);
    }

    [Fact]
    public void Sand_HasDunes_OnTopOfTheNoise()
    {
        var map = Painted(TerrainType.Sand, 7);
        var plain = Shaped(TerrainType.Sand);

        var offsets = Corners(map).Zip(Corners(plain), (a, b) => a - b).ToList();
        var reach = (int)Math.Ceiling(ReliefPainter.NoiseAmplitude + ReliefPainter.DuneAmplitude);

        Assert.All(offsets, o => Assert.InRange(o, -reach, reach));
        Assert.Contains(offsets, o => Math.Abs(o) > ReliefPainter.NoiseAmplitude);
    }

    [Theory]
    [InlineData(TerrainType.ShallowWater)]
    [InlineData(TerrainType.Ice)]
    public void FlatTerrain_GetsNoNoise(TerrainType terrain)
    {
        var map = Painted(terrain, 3);

        Assert.All(Corners(map), h => Assert.Equal(ReliefTable.Of(terrain).Base, h));
    }

    [Fact]
    public void AGeneratedMap_IsWithinTheLimits_WithItsObjectsLevelled()
    {
        var generated = MapGenerator.Generate(BuiltInTemplates.Find("duel")!, 72, 4);
        var map = generated.Map;

        Assert.True(map.HasGroundHeights);
        for (var level = 0; level < map.Levels; level++)
        {
            for (var cy = 0; cy <= map.Height; cy++)
            {
                for (var cx = 0; cx <= map.Width; cx++)
                {
                    var (min, max) = ReliefLimits.RangeAt(map, level, cx, cy);
                    Assert.InRange(map.GetCorner(level, cx, cy), min, max);
                }
            }
        }

        var levelled = generated.Objects.Where(o => o.Definition.Footprint is { HasVisit: true, OccupiedCells.Count: > 1 }).ToList();
        Assert.NotEmpty(levelled);
        foreach (var mapObject in levelled)
        {
            var level = mapObject.Position.Level;
            var free = mapObject.OccupiedCells().Select(c => c.Position)
                .SelectMany(p => new[] { (p.X, p.Y), (p.X + 1, p.Y), (p.X, p.Y + 1), (p.X + 1, p.Y + 1) })
                .Where(c => ReliefLimits.RangeAt(map, level, c.Item1, c.Item2) is var (min, max) && min < max); // not held by a shore or cliff
            Assert.InRange(free.Select(c => map.GetCorner(level, c.Item1, c.Item2)).Distinct().Count(), 0, 1);
        }
    }

    [Fact]
    public void TheGround_ChangesNothingTheRulesSee()
    {
        var scenario = EditorMapGenerator.Generate(BuiltInTemplates.Find("duel")!, 72, 5).Scenario;
        var digest = GameStateDigest.Describe(scenario);

        ReliefPainter.Paint(scenario.Map, 99);
        ReliefShaper.Level(scenario.Map, scenario.Objects);
        Assert.Equal(digest, GameStateDigest.Describe(scenario));

        for (var level = 0; level < scenario.Map.Levels; level++)
        {
            for (var cy = 0; cy <= scenario.Map.Height; cy++)
            {
                for (var cx = 0; cx <= scenario.Map.Width; cx++)
                {
                    scenario.Map.SetCorner(level, cx, cy, 0);
                }
            }
        }

        Assert.Equal(digest, GameStateDigest.Describe(scenario));
    }

    private static AdventureMap Painted(TerrainType terrain, ulong seed)
    {
        var map = new AdventureMap(40, 30, fill: terrain);
        ReliefPainter.Paint(map, seed);
        return map;
    }

    private static AdventureMap Shaped(TerrainType terrain)
    {
        var map = new AdventureMap(40, 30, fill: terrain);
        ReliefShaper.FromTerrain(map);
        return map;
    }

    private static List<int> Corners(AdventureMap map) =>
        [.. from cy in Enumerable.Range(0, map.Height + 1) from cx in Enumerable.Range(0, map.Width + 1) select map.GetCorner(0, cx, cy)];
}
