using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// Map-editor operation: paints a square brush of terrain. Lives in core so the editor UI stays thin
/// and the operation is unit-testable (and later undoable).
/// </summary>
public sealed class TerrainBrush(AdventureMap map, IEventPublisher events)
{
    /// <summary>Brush radius in tiles; 0 paints a single tile, 1 a 3×3 square, etc.</summary>
    public int Radius { get; set; }

    public TerrainType Terrain { get; set; } = TerrainType.Grass;

    public void Paint(MapPosition center)
    {
        var changed = new List<MapPosition>();
        for (var dy = -Radius; dy <= Radius; dy++)
        {
            for (var dx = -Radius; dx <= Radius; dx++)
            {
                var position = center with { X = center.X + dx, Y = center.Y + dy };
                if (map.Contains(position) && map.GetTerrain(position) != Terrain)
                {
                    map.SetTerrain(position, Terrain);
                    changed.Add(position);
                }
            }
        }

        if (changed.Count > 0)
        {
            events.Publish(new TerrainPainted(changed, Terrain));
        }
    }
}
