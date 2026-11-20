using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Paints the map's tile layers (terrain, roads, rivers, start zones) on sets of tiles: under a brush (<see cref="Brush"/>), along a line, or as a flood fill.</summary>
public sealed class TerrainEditor
{
    private readonly PaintLayer<TerrainType, TerrainPainted> _terrain;
    private readonly PaintLayer<RoadType, RoadsPainted> _roads;
    private readonly PaintLayer<RiverType, RiversPainted> _rivers;
    private readonly PaintLayer<PlayerId?, StartZonesPainted> _startZones;

    private readonly AdventureMap _map;

    internal TerrainEditor(EditorContext context, HeightEditor heights)
    {
        var map = _map = context.Scenario.Map;
        _terrain = new(context, map.GetTerrain, map.SetTerrain, (positions, terrain) => new TerrainPainted(positions, terrain), heights.AfterPaint);
        _roads = new(context, map.GetRoad, map.SetRoad, (positions, road) => new RoadsPainted(positions, road));
        _rivers = new(context, map.GetRiver, map.SetRiver, (positions, river) => new RiversPainted(positions, river));
        _startZones = new(context, map.GetStartZone, map.SetStartZone, (positions, owner) => new StartZonesPainted(positions, owner));
    }

    /// <summary>
    /// Paints terrain under a square brush of <paramref name="radius"/> (0: one tile). Returns the number of tiles changed. While
    /// <see cref="HeightEditor.AutoHeight"/> is on, the same command gives the ground there the new terrain's height
    /// (<see cref="HeightEditor.FromTerrain(MapPosition, int)"/>), so one undo takes back both.
    /// </summary>
    public int Paint(MapPosition center, TerrainType terrain, int radius = 0) => Paint(Brush.OfRadius(radius).Tiles(center), terrain);

    /// <summary>Paints terrain on <paramref name="tiles"/> (those off the map are left out). Returns the number of tiles changed.</summary>
    public int Paint(IEnumerable<MapPosition> tiles, TerrainType terrain) => Paint(tiles, terrain, shapeGround: true);

    /// <summary><see cref="Paint(IEnumerable{MapPosition}, TerrainType)"/>; <paramref name="shapeGround"/> false leaves the heights alone (a paste brings its own).</summary>
    internal int Paint(IEnumerable<MapPosition> tiles, TerrainType terrain, bool shapeGround) =>
        _terrain.Paint(tiles, terrain, $"Paint {terrain}", shapeGround);

    /// <summary>
    /// Flood fill: gives <paramref name="terrain"/> to the tile and to every tile of the same terrain joined to it side by side (not
    /// across corners) on its level, as one undo step. Returns the number of tiles changed (0 when the tile has that terrain already).
    /// </summary>
    public int Fill(MapPosition start, TerrainType terrain)
    {
        if (!_map.Contains(start) || _map.GetTerrain(start) == terrain)
        {
            return 0;
        }

        var old = _map.GetTerrain(start);
        var area = new HashSet<MapPosition> { start };
        var open = new Queue<MapPosition>([start]);
        while (open.TryDequeue(out var tile))
        {
            foreach (var next in new[] { tile.Offset(1, 0), tile.Offset(-1, 0), tile.Offset(0, 1), tile.Offset(0, -1) })
            {
                if (_map.Contains(next) && _map.GetTerrain(next) == old && area.Add(next))
                {
                    open.Enqueue(next);
                }
            }
        }

        return _terrain.Paint(area, terrain, $"Fill {terrain}");
    }

    /// <summary>Paints (or, with <see cref="RoadType.None"/>, erases) roads under a square brush. Returns tiles changed.</summary>
    public int PaintRoad(MapPosition center, RoadType road, int radius = 0) => PaintRoad(Brush.OfRadius(radius).Tiles(center), road);

    /// <summary>Paints (or erases) roads on <paramref name="tiles"/>. Returns tiles changed.</summary>
    public int PaintRoad(IEnumerable<MapPosition> tiles, RoadType road) =>
        _roads.Paint(tiles, road, road == RoadType.None ? "Erase road" : $"Paint {road} road");

    /// <summary>Paints (or, with <see cref="RiverType.None"/>, erases) rivers (ADR-079) under a square brush. Returns tiles changed.</summary>
    public int PaintRiver(MapPosition center, RiverType river, int radius = 0) => PaintRiver(Brush.OfRadius(radius).Tiles(center), river);

    /// <summary>Paints (or erases) rivers on <paramref name="tiles"/>. Returns tiles changed.</summary>
    public int PaintRiver(IEnumerable<MapPosition> tiles, RiverType river) =>
        _rivers.Paint(tiles, river, river == RiverType.None ? "Erase river" : $"Paint {river} river");

    /// <summary>
    /// Paints tiles into <paramref name="owner"/>'s start zone (MP-4, ADR-197), or, with null, out of every start zone. Returns tiles changed.
    /// </summary>
    public int PaintStartZone(MapPosition center, PlayerId? owner, int radius = 0) => PaintStartZone(Brush.OfRadius(radius).Tiles(center), owner);

    /// <summary>Paints <paramref name="tiles"/> into (or, with null, out of) a start zone. Returns tiles changed.</summary>
    public int PaintStartZone(IEnumerable<MapPosition> tiles, PlayerId? owner) =>
        _startZones.Paint(tiles, owner, owner is { } player ? $"Paint start zone {player.Value + 1}" : "Erase start zone");
}
