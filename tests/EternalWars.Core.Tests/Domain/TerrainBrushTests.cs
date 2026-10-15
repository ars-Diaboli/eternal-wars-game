using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

public class TerrainBrushTests
{
    [Fact]
    public void Paint_WithRadius_ClipsToMapAndReportsOnlyChangedTiles()
    {
        var map = new AdventureMap(4, 4);
        map.SetTerrain(new MapPosition(1, 0), TerrainType.Sand);
        var bus = new EventBus();
        TerrainPainted? painted = null;
        bus.Subscribe<TerrainPainted>(e => painted = e);
        var brush = new TerrainBrush(map, bus) { Radius = 1, Terrain = TerrainType.Sand };

        brush.Paint(new MapPosition(0, 0));

        Assert.NotNull(painted);
        Assert.Equal(3, painted.Positions.Count); // 2x2 visible corner, one tile already sand
        Assert.Equal(TerrainType.Sand, map.GetTerrain(new MapPosition(1, 1)));
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(2, 2)));
    }
}
