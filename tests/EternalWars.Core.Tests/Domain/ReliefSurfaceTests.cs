using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The smooth ground the 3D view shows (ADR-209): through the corners, inside each tile's corners, flat where they are, smooth.</summary>
public class ReliefSurfaceTests
{
    private static AdventureMap RandomMap(int seed, int size = 8)
    {
        var random = new GameRandom((ulong)seed);
        var map = new AdventureMap(size, size);
        for (var cy = 0; cy <= size; cy++)
        {
            for (var cx = 0; cx <= size; cx++)
            {
                map.SetCorner(0, cx, cy, random.NextInclusive(-24, 40));
            }
        }

        return map;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheSurface_PassesThroughEveryCorner(int seed)
    {
        var map = RandomMap(seed);

        for (var cy = 0; cy <= map.Height; cy++)
        {
            for (var cx = 0; cx <= map.Width; cx++)
            {
                Assert.Equal(map.GetCorner(0, cx, cy) / (float)ReliefLimits.StepsPerCell, ReliefSurface.HeightAt(map, 0, cx - 0.5f, cy - 0.5f), 4);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OverATile_TheSurfaceStaysBetweenItsCorners(int seed)
    {
        var map = RandomMap(seed);

        foreach (var tile in map.Positions(0))
        {
            var corners = new[] { (0, 0), (1, 0), (0, 1), (1, 1) }.Select(c => map.GetCorner(0, tile.X + c.Item1, tile.Y + c.Item2) / (float)ReliefLimits.StepsPerCell).ToList();
            for (var i = 0; i <= 10; i++)
            {
                for (var j = 0; j <= 10; j++)
                {
                    var height = ReliefSurface.HeightAt(map, 0, tile.X - 0.5f + (i / 10f), tile.Y - 0.5f + (j / 10f));
                    Assert.InRange(height, corners.Min() - 1e-5f, corners.Max() + 1e-5f);
                }
            }
        }
    }

    [Fact]
    public void FlatGround_StaysFlatUpToACliff()
    {
        // A plateau at 32 beside a plain at 0: no ripple on either side of the step.
        var map = new AdventureMap(8, 3);
        for (var cy = 0; cy <= 3; cy++)
        {
            for (var cx = 5; cx <= 8; cx++)
            {
                map.SetCorner(0, cx, cy, 32);
            }
        }

        for (var x = -0.5f; x <= 7.5f; x += 0.125f)
        {
            var height = ReliefSurface.HeightAt(map, 0, x, 1);
            var expected = x <= 3.5f ? 0f : x >= 4.5f ? 4f : (float?)null;
            if (expected is { } flat)
            {
                Assert.Equal(flat, height);
            }
            else
            {
                Assert.InRange(height, 0f, 4f);
            }
        }
    }

    [Fact]
    public void TheSurface_IsSmoothAcrossTileEdges()
    {
        // A steady rise from both sides: the slope just left of a corner is the slope just right of it (bilinear sampling kinks there).
        var map = new AdventureMap(6, 1);
        foreach (var (cx, height) in new[] { (0, 0), (1, 4), (2, 12), (3, 16), (4, 24), (5, 26), (6, 40) })
        {
            map.SetCorner(0, cx, 0, height);
            map.SetCorner(0, cx, 1, height);
        }

        const float e = 1e-2f;
        for (var cx = 1; cx <= 5; cx++)
        {
            var at = cx - 0.5f;
            var left = (ReliefSurface.HeightAt(map, 0, at, 0) - ReliefSurface.HeightAt(map, 0, at - e, 0)) / e;
            var right = (ReliefSurface.HeightAt(map, 0, at + e, 0) - ReliefSurface.HeightAt(map, 0, at, 0)) / e;
            Assert.InRange(Math.Abs(left - right), 0f, 0.1f);
        }
    }

    [Fact]
    public void PastTheEdge_TheNearestEdgeHolds()
    {
        var map = RandomMap(4, size: 3);

        Assert.Equal(ReliefSurface.HeightAt(map, 0, -0.5f, 1.2f), ReliefSurface.HeightAt(map, 0, -3f, 1.2f));
        Assert.Equal(ReliefSurface.HeightAt(map, 0, 1.7f, 2.5f), ReliefSurface.HeightAt(map, 0, 1.7f, 9f));
    }

    [Fact]
    public void AChange_MovesTheShownGroundOnlyOverTilesTouchingItsReach()
    {
        var map = RandomMap(5);
        var before = map.Positions(0).ToDictionary(t => t, t => Samples(map, t));

        map.SetCorner(0, 4, 3, map.GetCorner(0, 4, 3) + 20);
        var reach = ReliefSurface.Reach(new CornerArea(4, 3, 4, 3));

        foreach (var tile in map.Positions(0))
        {
            var moved = !before[tile].SequenceEqual(Samples(map, tile));
            Assert.True(!moved || reach.Touches(tile), $"{tile} moved outside the reach");
            Assert.Equal(tile.X is >= 2 and <= 5 && tile.Y is >= 1 and <= 4, reach.Touches(tile));
        }

        // The tiles the corner is on move, and so (here) do some beside them, which the corner's tangents reach.
        Assert.All(map.Positions(0).Where(t => t.X is 3 or 4 && t.Y is 2 or 3), t => Assert.NotEqual(before[t], Samples(map, t)));
        Assert.Contains(map.Positions(0), t => !(t.X is 3 or 4 && t.Y is 2 or 3) && !before[t].SequenceEqual(Samples(map, t)));
    }

    [Theory]
    [InlineData(-2.75f, -1.5f, 0.25f, 30, 26)] // over the edges and past them
    [InlineData(1.5f, 2.25f, 0.125f, 9, 13)] // inside
    [InlineData(-0.5f, -0.5f, 1f, 9, 9)] // on the corners
    public void TheGrid_IsTheSurfaceAtEachPoint(float left, float top, float step, int columns, int rows)
    {
        var map = RandomMap(6);

        var grid = ReliefSurface.Grid(map, 0, left, top, step, columns, rows);

        for (var j = 0; j < rows; j++)
        {
            for (var i = 0; i < columns; i++)
            {
                Assert.Equal(ReliefSurface.HeightAt(map, 0, left + (i * step), top + (j * step)), grid[i, j], 5);
            }
        }
    }

    private static float[] Samples(AdventureMap map, MapPosition tile) =>
        [.. from i in Enumerable.Range(0, 9) from j in Enumerable.Range(0, 9) select ReliefSurface.HeightAt(map, 0, tile.X - 0.5f + (i / 8f), tile.Y - 0.5f + (j / 8f))];
}
