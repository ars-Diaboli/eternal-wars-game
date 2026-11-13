using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Paints the map's tile layers (terrain, roads, rivers) with a square brush (radius 0 = one tile).</summary>
public sealed class TerrainEditor
{
    private readonly PaintLayer<TerrainType, TerrainPainted> _terrain;
    private readonly PaintLayer<RoadType, RoadsPainted> _roads;
    private readonly PaintLayer<RiverType, RiversPainted> _rivers;

    internal TerrainEditor(EditorContext context, HeightEditor heights)
    {
        var map = context.Scenario.Map;
        _terrain = new(context, map.GetTerrain, map.SetTerrain, (positions, terrain) => new TerrainPainted(positions, terrain), heights.AfterPaint);
        _roads = new(context, map.GetRoad, map.SetRoad, (positions, road) => new RoadsPainted(positions, road));
        _rivers = new(context, map.GetRiver, map.SetRiver, (positions, river) => new RiversPainted(positions, river));
    }

    /// <summary>
    /// Paints terrain. Returns the number of tiles changed. While <see cref="HeightEditor.AutoHeight"/> is on, the same command gives the
    /// ground there the new terrain's height (<see cref="HeightEditor.FromTerrain(MapPosition, int)"/>), so one undo takes back both.
    /// </summary>
    public int Paint(MapPosition center, TerrainType terrain, int radius = 0) => _terrain.Paint(center, radius, terrain, $"Paint {terrain}");

    /// <summary>Paints (or, with <see cref="RoadType.None"/>, erases) roads. Returns tiles changed.</summary>
    public int PaintRoad(MapPosition center, RoadType road, int radius = 0) =>
        _roads.Paint(center, radius, road, road == RoadType.None ? "Erase road" : $"Paint {road} road");

    /// <summary>Paints (or, with <see cref="RiverType.None"/>, erases) rivers (ADR-079). Returns tiles changed.</summary>
    public int PaintRiver(MapPosition center, RiverType river, int radius = 0) =>
        _rivers.Paint(center, radius, river, river == RiverType.None ? "Erase river" : $"Paint {river} river");
}
