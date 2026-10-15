using System.Collections.Generic;
using Godot;

namespace EW.Scripts.DataTypes;

public interface IWorldMapObject
{
    Vector2I GridPosition { get; }
    Vector2I MapObjectSize { get; }
    IEnumerable<Vector2I> OccupiedTiles { get; }
    Vector3 GlobalPosition { get; }
    bool OccupiesTile(Vector2I tilePosition);
    void OnVisionUpdated(bool isRevealed);
}
