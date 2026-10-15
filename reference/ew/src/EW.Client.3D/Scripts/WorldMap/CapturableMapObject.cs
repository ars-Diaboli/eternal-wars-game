using System.Collections.Generic;
using System;
using EW.Scripts.DataTypes;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;
using EW.Scripts.Players;
using EW.Scripts.WorldMap;
using Godot;

namespace EW.Scripts;

public sealed class CapturableOwnershipChanged : IGameEvent
{
    public CapturableMapObject CapturableObject { get; init; }
    public GamePlayer PreviousOwner { get; init; }
    public GamePlayer NewOwner { get; init; }
}

public partial class CapturableMapObject : Node3D, ICapturableMapObject
{
    [Export] public GamePlayer OwnerPlayer { get; private set; }
    [Export] public WorldMapSettings WorldMapSettings { get; set; }
    [Export] public Vector2I MapObjectSize { get; set; } = Vector2I.One;
    [Export] public Color NeutralColor { get; set; } = Colors.LightGray;
    [Export] public Color EnvironmentColor { get; set; } = Colors.LightGray;

    protected Node3D VisualMeshNode { get; private set; }
    private MeshInstance3D _ownerIndicator;

    public override void _Ready()
    {
        VisualMeshNode = GetNodeOrNull<Node3D>("MeshInstance3D");
        _ownerIndicator = GetNodeOrNull<MeshInstance3D>("OwnerIndicator");

        WorldMapSettings ??= ResolveWorldMapSettings();

        if (WorldMapSettings != null)
        {
            GlobalPosition = WorldMapSettings.SnapWorldPositionToTileCenter(GlobalPosition);
        }

        OwnerPlayer ??= ResolveEnvironmentPlayer();
        this.SnapToTerrain();
        ApplyOwnerVisuals();

        ResolveFogSystem()?.RegisterObject(this);
    }

    public override void _ExitTree()
    {
        ResolveFogSystem()?.UnregisterObject(this);
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

    public virtual void OnPlayerInteract(Heroes.Hero playerHero)
    {
        GamePlayer newOwner = ResolveOwnerForHero(playerHero);
        if (newOwner == null || ReferenceEquals(OwnerPlayer, newOwner))
        {
            return;
        }

        GamePlayer previousOwner = OwnerPlayer;
        OwnerPlayer = newOwner;
        ApplyOwnerVisuals();
        Hub.Instance?.Publish(new CapturableOwnershipChanged
        {
            CapturableObject = this,
            PreviousOwner = previousOwner,
            NewOwner = newOwner
        });
        GD.Print($"{Name} captured by {OwnerPlayer}.");
    }

    public virtual void OnOwnerTurnStarted(GamePlayer activePlayer)
    {
    }

    public virtual IReadOnlyDictionary<ResourceType, int> GetTurnIncome()
    {
        return new Dictionary<ResourceType, int>();
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

    public void OnVisionUpdated(bool isRevealed)
    {
        if (VisualMeshNode != null)
        {
            VisualMeshNode.Visible = isRevealed;
        }

        if (_ownerIndicator != null)
        {
            _ownerIndicator.Visible = isRevealed;
        }
    }

    protected virtual void ApplyOwnerVisuals()
    {
        if (_ownerIndicator == null)
        {
            return;
        }

        StandardMaterial3D material = new StandardMaterial3D();
        material.AlbedoColor = GetOwnerColor();
        _ownerIndicator.MaterialOverride = material;
    }

    private Color GetOwnerColor()
    {
        if (OwnerPlayer == null)
        {
            return NeutralColor;
        }

        return OwnerPlayer.IsEnvironment
            ? EnvironmentColor
            : OwnerPlayer.PlayerColor;
    }

    private Vector2I GetClampedMapObjectSize()
    {
        return new Vector2I(
            Mathf.Clamp(MapObjectSize.X, 1, 8),
            Mathf.Clamp(MapObjectSize.Y, 1, 8)
        );
    }

    private GamePlayer ResolveOwnerForHero(Heroes.Hero hero)
    {
        TurnManager turnManager = GetTree().CurrentScene.GetNodeOrNull<TurnManager>("TurnManager");
        return turnManager?.GetPlayerForHero(hero) ?? hero?.OwnerPlayer;
    }

    private GamePlayer ResolveEnvironmentPlayer()
    {
        TurnManager turnManager = GetTree().CurrentScene.GetNodeOrNull<TurnManager>("TurnManager");
        return turnManager?.EnvironmentPlayer;
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

    private FogOfWarTextureSystem ResolveFogSystem()
    {
        foreach (Node sibling in GetTree().CurrentScene.GetChildren())
        {
            if (sibling is FogOfWarTextureSystem fogSystem)
            {
                return fogSystem;
            }
        }

        return null;
    }
}
