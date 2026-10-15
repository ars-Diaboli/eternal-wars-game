using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;
using Godot;

namespace EW.Scripts.WorldMap;

public partial class WorldMapSettings : Node
{
    [Export] public MapSize MapSize { get; private set; } = MapSize._64x64;

    /// <summary>
    /// Size of one logical adventure-map tile in world units.
    /// This is independent from Terrain3D resolution.
    /// </summary>
    [Export] public float TileWorldSize { get; private set; } = 1.0f;

    public int WidthInTiles => GetTileLength(MapSize);
    public int HeightInTiles => GetTileLength(MapSize);

    public Vector2I SizeInTiles => new(WidthInTiles, HeightInTiles);

    public Vector2 SizeInWorldUnits => new(WidthInTiles * TileWorldSize, HeightInTiles * TileWorldSize);

    public Vector2 HalfSizeInWorldUnits => SizeInWorldUnits * 0.5f;

    public Rect2I TileBounds => new(Vector2I.Zero, SizeInTiles);

    public Rect2 WorldBounds => new(-HalfSizeInWorldUnits, SizeInWorldUnits);

    public bool IsInsideMap(Vector2I tilePosition) => TileBounds.HasPoint(tilePosition);

    public bool IsInsideMap(Vector3 worldPosition) => WorldBounds.HasPoint(new Vector2(worldPosition.X, worldPosition.Z));

    public Vector3 TileToWorldPosition(Vector2I tilePosition, float y = 0.0f)
    {
        Vector2 worldPosition = new Vector2(
            (tilePosition.X + 0.5f) * TileWorldSize,
            (tilePosition.Y + 0.5f) * TileWorldSize
        ) - HalfSizeInWorldUnits;

        return new Vector3(worldPosition.X, y, worldPosition.Y);
    }

    public Vector2I WorldToTilePosition(Vector3 worldPosition)
    {
        Vector2 localMapPosition = new Vector2(worldPosition.X, worldPosition.Z) + HalfSizeInWorldUnits;

        return new Vector2I(
            Mathf.FloorToInt(localMapPosition.X / TileWorldSize),
            Mathf.FloorToInt(localMapPosition.Y / TileWorldSize)
        );
    }

    public Vector3 SnapWorldPositionToTileCenter(Vector3 worldPosition)
    {
        Vector2I tilePosition = WorldToTilePosition(worldPosition);
        return TileToWorldPosition(tilePosition, worldPosition.Y);
    }

    private static int GetTileLength(MapSize mapSize) => (int)mapSize * 32;

    public override void _Ready()
    {
        GD.Print($"World map initialized: {WidthInTiles}x{HeightInTiles} tiles.");
        GD.Print($"World size: {SizeInWorldUnits.X} x {SizeInWorldUnits.Y} units.");
        
        // Scan initial layout
        Hub.Instance.RegisterHierarchy(this);
    
        // Listen for any new children added to this scene container at runtime
        ChildEnteredTree += OnChildEnteredTree;
        ChildExitingTree += OnChildExitedTree;
    }
    
     public override void _ExitTree()
     {
         ChildEnteredTree -= OnChildEnteredTree;
         ChildExitingTree -= OnChildExitedTree;
     }
     
     // Instantly hook up the newly spawned node (and any internal children it brought with it)
    private void OnChildEnteredTree(Node node) => Hub.Instance.RegisterHierarchy(node);

    private void OnChildExitedTree(Node node) => Hub.Instance.UnregisterHierarchy(node);
}