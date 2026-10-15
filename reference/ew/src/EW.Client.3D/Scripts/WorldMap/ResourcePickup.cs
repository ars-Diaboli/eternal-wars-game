using System.Collections.Generic;
using EW.Scripts.DataTypes;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Players;
using EW.Scripts.WorldMap;
using Godot;

namespace EW.Scripts;

public partial class ResourcePickup : Node3D, IInteractableMapObject
{
    [Export] public ResourceType Type { get; set; } = ResourceType.Gold;
    [Export] public int Amount { get; set; } = 500;
    [Export] public WorldMapSettings WorldMapSettings { get; set; }
    [Export] public Vector2I MapObjectSize { get; set; } = Vector2I.One;

    private Node3D _visualMeshNode;

    public override void _Ready()
    {
        _visualMeshNode = GetNode<Node3D>("MeshInstance3D");

        WorldMapSettings ??= ResolveWorldMapSettings();

        if (WorldMapSettings != null)
        {
            GlobalPosition = WorldMapSettings.SnapWorldPositionToTileCenter(GlobalPosition);
        }

        this.SnapToTerrain();

        var fogSystem = GetTree().CurrentScene.GetNodeOrNull<FogOfWarTextureSystem>("FogOfWarSystem");
        fogSystem?.RegisterObject(this);
    }

    public override void _ExitTree()
    {
        var fogSystem = GetTree().CurrentScene.GetNodeOrNull<FogOfWarTextureSystem>("FogOfWarSystem");
        fogSystem?.UnregisterObject(this);
    }

    public Vector2I GridPosition
    {
        get
        {
            WorldMapSettings ??= ResolveWorldMapSettings();
            return WorldMapSettings?.WorldToTilePosition(GlobalPosition) ?? Vector2I.Zero;
        }
    }

    public IEnumerable<Vector2I> OccupiedTiles
    {
        get
        {
            Vector2I size = GetClampedMapObjectSize();
            Vector2I origin = GridPosition;

            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    yield return origin + new Vector2I(x, y);
                }
            }
        }
    }

    public Vector2I InteractionTile
    {
        get
        {
            Vector2I size = GetClampedMapObjectSize();
            return GridPosition + new Vector2I((size.X - 1) / 2, size.Y);
        }
    }

    public void OnVisionUpdated(bool isRevealed)
    {
        if (_visualMeshNode != null) _visualMeshNode.Visible = isRevealed;
    }

    public void OnPlayerInteract(Heroes.Hero playerHero)
    {
        TurnManager turnManager = GetTree().CurrentScene.GetNodeOrNull<TurnManager>("TurnManager");

        if (turnManager != null)
        {
            GamePlayer ownerPlayer = turnManager.GetPlayerForHero(playerHero);
            turnManager.AddResource(ownerPlayer, Type, Amount);
        }
        else
        {
            GD.Print($"Collected +{Amount} {Type}!");
        }

        QueueFree();
    }

    public bool OccupiesTile(Vector2I tilePosition)
    {
        Vector2I origin = GridPosition;
        Vector2I size = GetClampedMapObjectSize();

        return tilePosition.X >= origin.X
            && tilePosition.X < origin.X + size.X
            && tilePosition.Y >= origin.Y
            && tilePosition.Y < origin.Y + size.Y;
    }

    public bool CanInteractFromTile(Vector2I tilePosition)
    {
        Vector2I size = GetClampedMapObjectSize();
        if (size == Vector2I.One)
        {
            Vector2I gridOffset = GridPosition - tilePosition;
            return Mathf.Abs(gridOffset.X) <= 1
                && Mathf.Abs(gridOffset.Y) <= 1
                && gridOffset != Vector2I.Zero;
        }

        return tilePosition == InteractionTile;
    }

    private Vector2I GetClampedMapObjectSize()
    {
        return new Vector2I(
            Mathf.Clamp(MapObjectSize.X, 1, 8),
            Mathf.Clamp(MapObjectSize.Y, 1, 8)
        );
    }

    private WorldMapSettings ResolveWorldMapSettings()
    {
        Node currentScene = GetTree().CurrentScene;
        if (currentScene is WorldMapSettings worldMapSettings)
        {
            return worldMapSettings;
        }

        return currentScene?.GetNodeOrNull<WorldMapSettings>("WorldMapSettings");
    }
}
