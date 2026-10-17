using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Stage 2 of the generator: terrain per zone, walls between zones, obstacles (Phase 4).</summary>
public class TerrainPainterTests
{
    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    private static MapTemplate Ffa() => BuiltInTemplates.Find("Free for all")!;

    private static MapTemplate WithDensity(MapTemplate template, ObstacleDensity density) =>
        template with { Zones = [.. template.Zones.Select(z => z with { Obstacles = density })] };

    private static GeneratedTerrain Paint(MapTemplate template, int size = 72, ulong seed = 3, LayoutStrategy strategy = LayoutStrategy.Voronoi) =>
        TerrainPainter.Paint(template, ZoneLayouter.Layout(template, size, seed, strategy), seed);

    private static bool Walkable(GeneratedTerrain result, int x, int y, int level) =>
        result.Map.GetTerrain(new MapPosition(x, y, level)) != TerrainPainter.WallTerrain;

    private static IEnumerable<(int X, int Y)> Tiles(GeneratedTerrain result, int zone, int level) =>
        result.Layout.Tiles(zone).Where(t => t.Level == level).Select(t => (t.X, t.Y));

    private static HashSet<TerrainType> TerrainsOf(GeneratedTerrain result, int zone) =>
        Tiles(result, zone, 0).Select(t => result.Map.GetTerrain(new MapPosition(t.X, t.Y))).Where(t => t != TerrainPainter.WallTerrain).ToHashSet();

    [Fact]
    public void TheSameInputs_GiveTheSameTerrain_AndOtherSeedsOtherTerrain()
    {
        string Dump(GeneratedTerrain g) => string.Concat(Enumerable.Range(0, 72 * 72).Select(i => (char)('a' + (int)g.Map.GetTerrain(new MapPosition(i % 72, i / 72)))));

        Assert.Equal(Dump(Paint(Duel())), Dump(Paint(Duel())));
        Assert.NotEqual(Dump(Paint(Duel())), Dump(Paint(Duel(), seed: 4)));
    }

    [Fact]
    public void AZone_IsPaintedFromItsAllowedTerrains_AndTheOthersFromTheirPool()
    {
        var template = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z.Id == "centre" ? z with { Terrains = [TerrainType.Lava] } : z.Id == "reserve-1" ? z with { Terrains = [TerrainType.Snow, TerrainType.Ice] } : z)],
        };

        foreach (var seed in new ulong[] { 1, 2, 3 })
        {
            var result = Paint(template, seed: seed);
            for (var zone = 0; zone < template.Zones.Count; zone++)
            {
                var allowed = template.Zones[zone].Terrains is { Count: > 0 } list ? list : [.. TerrainPainter.DefaultPool(0), TerrainType.Dirt, TerrainType.Rough, TerrainType.Grass];

                Assert.True(TerrainsOf(result, zone).IsSubsetOf(allowed), $"{template.Zones[zone].Id}: {string.Join(", ", TerrainsOf(result, zone))}");
            }

            Assert.Single(TerrainsOf(result, 4)); // one allowed terrain: no patches
        }
    }

    [Fact]
    public void NoOneCanStepFromOneZoneToAnother_EvenDiagonally()
    {
        foreach (var strategy in new[] { LayoutStrategy.Voronoi, LayoutStrategy.Rectangles })
        {
            var result = Paint(Ffa(), 96, 5, strategy);
            for (var y = 0; y < 96; y++)
            {
                for (var x = 0; x < 96; x++)
                {
                    foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (1, -1) })
                    {
                        var (nx, ny) = (x + dx, y + dy);
                        if (nx >= 96 || ny < 0 || ny >= 96 || result.Layout.ZoneAt(x, y, 0) == result.Layout.ZoneAt(nx, ny, 0))
                        {
                            continue;
                        }

                        Assert.False(Walkable(result, x, y, 0) && Walkable(result, nx, ny, 0), $"({x},{y}) and ({nx},{ny}) are in different zones and both walkable");
                    }
                }
            }
        }
    }

    [Fact]
    public void EveryZone_KeepsOneWalkablePiece()
    {
        foreach (var template in new[] { Duel(), Ffa(), WithDensity(Ffa(), ObstacleDensity.Dense) })
        {
            foreach (var seed in new ulong[] { 1, 2 })
            {
                var result = Paint(template, 96, seed);
                for (var zone = 0; zone < template.Zones.Count; zone++)
                {
                    var walkable = Tiles(result, zone, 0).Where(t => Walkable(result, t.X, t.Y, 0)).ToHashSet();
                    Assert.NotEmpty(walkable);

                    var seen = new HashSet<(int, int)> { walkable.First() };
                    var queue = new Queue<(int X, int Y)>([walkable.First()]);
                    while (queue.TryDequeue(out var t))
                    {
                        foreach (var next in new[] { (t.X + 1, t.Y), (t.X - 1, t.Y), (t.X, t.Y + 1), (t.X, t.Y - 1) })
                        {
                            if (walkable.Contains(next) && seen.Add(next))
                            {
                                queue.Enqueue(next);
                            }
                        }
                    }

                    Assert.Equal(walkable.Count, seen.Count);
                }
            }
        }
    }

    [Fact]
    public void TheWallsAreListedPerPairOfTouchingZones_AndAreRock()
    {
        var result = Paint(Duel());
        var index = Duel().Zones.Select((z, i) => (z.Id, i)).ToDictionary(p => p.Id, p => p.i);

        foreach (var c in Duel().Connections)
        {
            var key = index[c.From] < index[c.To] ? (index[c.From], index[c.To]) : (index[c.To], index[c.From]);
            Assert.True(result.Walls.TryGetValue(key, out var wall), $"{c.From} - {c.To} have no wall");
            Assert.NotEmpty(wall!);
            Assert.All(wall!, p => Assert.Equal(TerrainPainter.WallTerrain, result.Map.GetTerrain(p)));
        }

        foreach (var ((a, b), tiles) in result.Walls)
        {
            Assert.True(a < b);
            Assert.All(tiles, p => Assert.Contains(result.Layout.ZoneAt(p.X, p.Y, 0), new[] { a, b }));
        }
    }

    [Fact]
    public void ObstaclesAreRock_FarFromWalls_AndTheirDensityFollowsTheTemplate()
    {
        int Count(ObstacleDensity density) => Paint(WithDensity(Duel(), density), 96, 2).Obstacles.Count;

        Assert.Equal(0, Count(ObstacleDensity.None));
        Assert.InRange(Count(ObstacleDensity.Sparse), 1, Count(ObstacleDensity.Normal));
        Assert.True(Count(ObstacleDensity.Normal) < Count(ObstacleDensity.Dense));

        var dense = Paint(WithDensity(Duel(), ObstacleDensity.Dense), 96, 2);
        var walls = dense.Walls.Values.SelectMany(w => w).ToList();
        Assert.All(dense.Obstacles, o =>
        {
            Assert.Equal(TerrainPainter.WallTerrain, dense.Map.GetTerrain(o));
            Assert.All(walls, w => Assert.True(Math.Max(Math.Abs(w.X - o.X), Math.Abs(w.Y - o.Y)) >= 3));
        });
    }

    [Fact]
    public void BothLevels_ArePaintedWithTheirOwnTerrain()
    {
        var template = Duel() with
        {
            Underground = true,
            Zones = [.. Duel().Zones, new TemplateZone { Id = "deep-a", Level = 1 }, new TemplateZone { Id = "deep-b", Level = 1 }],
            Connections = [.. Duel().Connections, new TemplateConnection { From = "deep-a", To = "deep-b" }, new TemplateConnection { From = "centre", To = "deep-a", Kind = ConnectionKind.Gate }],
        };

        var result = Paint(template);

        Assert.Equal(2, result.Map.Levels);
        var underground = Enumerable.Range(0, 72 * 72).Select(i => result.Map.GetTerrain(new MapPosition(i % 72, i / 72, 1))).Where(t => t != TerrainPainter.WallTerrain).ToHashSet();
        Assert.True(underground.IsSubsetOf([TerrainType.Subterranean, TerrainType.RockyGround, TerrainType.Lava]));
        Assert.Contains(result.Walls.Keys, k => k.A >= 5 && k.B >= 5); // the deep zones touch
    }

    [Fact]
    public void ALayoutFromAnotherTemplate_IsRefused()
    {
        var layout = ZoneLayouter.Layout(Duel(), 72, 1);

        Assert.Throws<ArgumentException>(() => TerrainPainter.Paint(Ffa(), layout, 1));
    }

    [Fact]
    public void ObstacleDensity_SurvivesTheFileFormat()
    {
        var loaded = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(WithDensity(Duel(), ObstacleDensity.Dense)));

        Assert.All(loaded.Zones, z => Assert.Equal(ObstacleDensity.Dense, z.Obstacles));
        Assert.All(Duel().Zones, z => Assert.Equal(ObstacleDensity.Sparse, z.Obstacles));
    }
}
