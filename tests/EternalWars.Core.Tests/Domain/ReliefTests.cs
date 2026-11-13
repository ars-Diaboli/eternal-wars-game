using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The ground's height layer (ADR-181, ADR-182): the table, the limits, shaping from terrain, levelling, sampling.</summary>
public class ReliefTests
{
    [Fact]
    public void TheTable_GivesEveryTerrainAHeightInsideItsLimits()
    {
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var height = ReliefTable.Of(terrain);
            Assert.InRange(height.Base, height.Min, height.Max);
            var (min, max) = terrain switch
            {
                TerrainType.Water => (ReliefLimits.StoredMin, -1),
                TerrainType.Rock => (ReliefLimits.WalkableMax + 1, ReliefLimits.StoredMax),
                _ => (0, ReliefLimits.WalkableMax),
            };
            Assert.InRange(height.Min, min, max);
            Assert.InRange(height.Max, min, max);
        }
    }

    [Fact]
    public void ANewMap_IsFlatAtZero()
    {
        var map = new AdventureMap(3, 2, levels: 2);

        Assert.All(Corners(map, 0).Concat(Corners(map, 1)), c => Assert.Equal(0, map.GetCorner(c.Level, c.X, c.Y)));
    }

    [Fact]
    public void OneTerrain_IsShapedFlatAtItsHeight()
    {
        var map = new AdventureMap(8, 8, fill: TerrainType.Snow);

        ReliefShaper.FromTerrain(map);

        Assert.All(Corners(map, 0), c => Assert.Equal(16, map.GetCorner(0, c.X, c.Y)));
        Assert.Equal(2f, map.GroundHeightAt(new MapPosition(3, 3)));
    }

    [Fact]
    public void TheShoreline_IsAtZero_WaterBelowIt_LandAboveIt()
    {
        var map = Sketch(
            "~~~~....",
            "~~~~....",
            "~~~~....",
            "~~~~....");

        ReliefShaper.FromTerrain(map);

        for (var cy = 0; cy <= map.Height; cy++)
        {
            Assert.Equal(0, map.GetCorner(0, 4, cy));
            Assert.All(Enumerable.Range(0, 4), cx => Assert.True(map.GetCorner(0, cx, cy) < 0));
            Assert.All(Enumerable.Range(5, 4), cx => Assert.InRange(map.GetCorner(0, cx, cy), 0, ReliefLimits.WalkableMax));
        }
    }

    [Fact]
    public void TheCliffFoot_IsAtFour_RockAboveIt()
    {
        var map = Sketch(
            "####....",
            "####....",
            "####....");

        ReliefShaper.FromTerrain(map);

        for (var cy = 0; cy <= map.Height; cy++)
        {
            Assert.Equal(ReliefLimits.WalkableMax, map.GetCorner(0, 4, cy));
            Assert.All(Enumerable.Range(0, 4), cx => Assert.True(map.GetCorner(0, cx, cy) > ReliefLimits.WalkableMax));
        }
    }

    [Fact]
    public void ShallowWaterAndIce_StayFlatInside()
    {
        var map = Sketch(
            "hhhhhhhh",
            "hssssiih",
            "hssssiih",
            "hssssiih",
            "hhhhhhhh");

        ReliefShaper.FromTerrain(map);

        for (var cy = 2; cy <= 3; cy++)
        {
            for (var cx = 2; cx <= 6; cx++)
            {
                Assert.Equal(ReliefTable.Of(TerrainType.ShallowWater).Base, map.GetCorner(0, cx, cy));
            }
        }
    }

    [Fact]
    public void TwoTerrains_MeetWithoutAStep()
    {
        var map = Sketch(
            "........HHHHHHHH",
            "........HHHHHHHH",
            "........HHHHHHHH");

        ReliefShaper.FromTerrain(map);

        // Grass (1) to highlands (3) is 16 eighths, spread over about 3 tiles each side: no step above 5 eighths.
        for (var cy = 0; cy <= map.Height; cy++)
        {
            for (var cx = 1; cx <= map.Width; cx++)
            {
                var step = map.GetCorner(0, cx, cy) - map.GetCorner(0, cx - 1, cy);
                Assert.InRange(step, 0, 5);
            }

            Assert.Equal(8, map.GetCorner(0, 0, cy));
            Assert.Equal(24, map.GetCorner(0, map.Width, cy));
        }
    }

    [Fact]
    public void Levelling_MakesTheFootprintFlatAtTheEntrancesHeight()
    {
        var map = Sketch(
            "........HHHHHHHH",
            "........HHHHHHHH",
            "........HHHHHHHH",
            "........HHHHHHHH");
        ReliefShaper.FromTerrain(map);
        var footprint = new[] { new MapPosition(6, 1), new MapPosition(7, 1), new MapPosition(8, 1), new MapPosition(6, 2), new MapPosition(8, 2) };
        var entrance = new MapPosition(7, 2);
        var expected = (int)Math.Round(map.GroundHeightAt(entrance) * ReliefLimits.StepsPerCell, MidpointRounding.AwayFromZero);
        var before = map.GetCorner(0, 11, 1);

        ReliefShaper.Level(map, footprint, entrance);

        for (var cy = 1; cy <= 3; cy++)
        {
            for (var cx = 6; cx <= 9; cx++)
            {
                Assert.Equal(expected, map.GetCorner(0, cx, cy));
            }
        }

        // The ring moves halfway; corners further out keep their height.
        Assert.InRange(map.GetCorner(0, 10, 2), expected, 24);
        Assert.Equal(before, map.GetCorner(0, 11, 1));
    }

    [Fact]
    public void Levelling_AtTheMapsEdge_StaysInsideTheCornerGrid()
    {
        var map = new AdventureMap(3, 3, fill: TerrainType.Rough);
        ReliefShaper.FromTerrain(map);

        ReliefShaper.Level(map, [new MapPosition(0, 0), new MapPosition(1, 0)], new MapPosition(1, 1));

        Assert.Equal(20, map.GetCorner(0, 0, 0));
    }

    [Fact]
    public void TheHeight_IsSampledBilinearly()
    {
        var map = new AdventureMap(1, 1);
        map.SetCorner(0, 0, 0, 0);
        map.SetCorner(0, 1, 0, 8);
        map.SetCorner(0, 0, 1, 16);
        map.SetCorner(0, 1, 1, 24);

        Assert.Equal(1.5f, map.GroundHeightAt(new MapPosition(0, 0)));
        Assert.Equal(0f, map.GroundHeightAt(0, -0.5f, -0.5f));
        Assert.Equal(0.5f, map.GroundHeightAt(0, 0f, -0.5f));
        Assert.Equal(3f, map.GroundHeightAt(0, 0.5f, 0.5f));
        Assert.Equal(3f, map.GroundHeightAt(0, 9f, 9f));
    }

    [Fact]
    public void ACorner_IsClampedToTheStoredRange()
    {
        var map = new AdventureMap(2, 2);

        map.SetCorner(0, 2, 2, 500);
        map.SetCorner(0, 0, 0, -500);

        Assert.Equal(ReliefLimits.StoredMax, map.GetCorner(0, 2, 2));
        Assert.Equal(ReliefLimits.StoredMin, map.GetCorner(0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetCorner(0, 3, 0));
    }

    [Fact]
    public void TheUnderground_IsShapedOnItsOwn()
    {
        var map = new AdventureMap(4, 4, levels: 2, fill: TerrainType.Grass);
        foreach (var position in map.Positions(1))
        {
            map.SetTerrain(position, TerrainType.Subterranean);
        }

        ReliefShaper.FromTerrain(map);

        Assert.Equal(8, map.GetCorner(0, 2, 2));
        Assert.Equal(10, map.GetCorner(1, 2, 2));
    }

    public static TheoryData<string> BuiltInMaps => [EmberMarchScenario.Name, .. ArenaScenarios.All.Select(a => a.Name)];

    [Theory]
    [MemberData(nameof(BuiltInMaps))]
    public void ABuiltInMap_IsShapedFromTerrain_WithItsObjectsLevelled(string name)
    {
        var state = name == EmberMarchScenario.Name ? EmberMarchScenario.Create() : ArenaScenarios.All.Single(a => a.Name == name).Create(null);
        var map = state.Map;
        var levelled = new HashSet<(int X, int Y)>();
        foreach (var mapObject in state.Objects.Where(o => o.Definition.Footprint is { HasVisit: true, OccupiedCells.Count: > 1 }))
        {
            var corners = mapObject.OccupiedCells().Select(c => c.Position)
                .SelectMany(p => new[] { (p.X, p.Y), (p.X + 1, p.Y), (p.X, p.Y + 1), (p.X + 1, p.Y + 1) })
                .ToList();
            var free = corners.Where(c => ReliefLimits.RangeAt(map, 0, c.Item1, c.Item2) is var (min, max) && min < max); // not held by a shore or cliff
            Assert.InRange(free.Select(c => map.GetCorner(0, c.Item1, c.Item2)).Distinct().Count(), 0, 1);
            levelled.UnionWith(corners.SelectMany(c => Around(c.Item1, c.Item2)));
        }

        Assert.NotEmpty(levelled);

        // Away from the objects the ground is the terrain's.
        var fromTerrain = new AdventureMap(map.Width, map.Height);
        foreach (var position in map.Positions(0))
        {
            fromTerrain.SetTerrain(position, map.GetTerrain(position));
        }

        ReliefShaper.FromTerrain(fromTerrain);
        Assert.True(map.HasGroundHeights);
        Assert.All(
            Corners(map, 0).Where(c => !levelled.Contains((c.X, c.Y))),
            c => Assert.Equal(fromTerrain.GetCorner(0, c.X, c.Y), map.GetCorner(0, c.X, c.Y)));
    }

    [Fact]
    public void OneTileObjects_AreNotLevelled()
    {
        var state = BlankScenario.Create(6, 4);
        state.Map.SetCorner(0, 2, 2, 20);
        var pile = new ResourcePile(state.NextObjectId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.PileId(ResourceType.Gold)), new(2, 2));

        ReliefShaper.Level(state.Map, [pile]);

        Assert.Equal(20, state.Map.GetCorner(0, 2, 2));
    }

    private static IEnumerable<(int X, int Y)> Around(int x, int y) =>
        from dy in Enumerable.Range(-1, 3) from dx in Enumerable.Range(-1, 3) select (x + dx, y + dy);

    private static IEnumerable<(int X, int Y, int Level)> Corners(AdventureMap map, int level) =>
        from cy in Enumerable.Range(0, map.Height + 1)
        from cx in Enumerable.Range(0, map.Width + 1)
        select (cx, cy, level);

    /// <summary>A one-level map from rows: <c>.</c> grass, <c>~</c> water, <c>#</c> rock, <c>H</c> highlands, <c>s</c> shallow water, <c>i</c> ice; anything else grass.</summary>
    private static AdventureMap Sketch(params string[] rows)
    {
        var map = new AdventureMap(rows[0].Length, rows.Length);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                map.SetTerrain(new MapPosition(x, y), rows[y][x] switch
                {
                    '~' => TerrainType.Water,
                    '#' => TerrainType.Rock,
                    'H' => TerrainType.Highlands,
                    's' => TerrainType.ShallowWater,
                    'i' => TerrainType.Ice,
                    _ => TerrainType.Grass,
                });
            }
        }

        return map;
    }
}
