using System.Collections.Generic;
using EW.Scripts.Calendar;
using EW.Scripts.DataTypes;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;
using EW.Scripts.Players;
using EW.Scripts.WorldMap;
using Godot;

namespace EW.Scripts.Heroes;

public partial class Hero : Node3D, IInteractableMapObject, IFactionOwned, IArmyOwner, IHandleEvent<TurnStart>, IHandleEvent<PlayerHeroEvent>
{
    [Export] public WorldMapSettings WorldMapSettings { get; set; }
    [Export] public Faction Faction { get; set; } = Faction.Neutral;
    [Export]
    public Godot.Collections.Array<ArmyUnitStack> Army
    {
        get => _army;
        set => _army = ArmyUtils.Normalize(value);
    }

    [Export] public float StepSpeed { get; set; } = 6.0f;
    [Export] public int MaxMovementPoints { get; set; } = 250;
    [Export] public FogOfWarTextureSystem VisionSystem { get; set; }

    public GamePlayer OwnerPlayer { get; internal set; }
    public bool IsIdle => _gridMovement.IsIdle;
    public int CurrentMovementPoints => _gridMovement.CurrentMovementPoints;
    public Vector2I MapObjectSize => Vector2I.One;
    public IEnumerable<Vector2I> OccupiedTiles
    {
        get
        {
            yield return GridPosition;
        }
    }

    public Vector2I InteractionTile => GridPosition;

    private readonly HeroGridMovement _gridMovement;
    private Godot.Collections.Array<ArmyUnitStack> _army = ArmyUtils.CreateEmptyArmy();
    private Node3D _visualMeshNode;

    public Hero()
    {
        _gridMovement = new HeroGridMovement(this);
    }

    public override void _Ready()
    {
        Army = _army;

        WorldMapSettings ??= ResolveWorldMapSettings();

        if (WorldMapSettings != null)
        {
            GlobalPosition = WorldMapSettings.SnapWorldPositionToTileCenter(GlobalPosition);
            SnapHeroToTerrain();
        }

        _gridMovement.Initialize();

        _visualMeshNode = GetNode<Node3D>("MeshInstance3D");

        GetTree().CurrentScene.GetNodeOrNull<FogOfWarTextureSystem>("FogOfWarTextureSystem")?.RegisterObject(this);
        VisionSystem?.RevealVisionAroundPosition(GlobalPosition);
    }

    public override void _ExitTree()
    {
        GetTree().CurrentScene.GetNodeOrNull<FogOfWarTextureSystem>("FogOfWarTextureSystem")?.UnregisterObject(this);
    }

    public bool MoveToGridCell(Vector3I targetCoords)
    {
        return _gridMovement.MoveToGridCell(targetCoords);
    }

    public bool MoveToTile(Vector2I targetTile)
    {
        return _gridMovement.MoveToTile(targetTile);
    }

    public List<PathWaypoint> StopMovementAtCurrentCell()
    {
        return _gridMovement.StopMovementAtCurrentCell();
    }

    public List<PathWaypoint> PreviewFullPathToCell(Vector3I targetCoords)
    {
        return _gridMovement.PreviewFullPathToCell(targetCoords);
    }

    public List<PathWaypoint> PreviewFullPathToTile(Vector2I targetTile)
    {
        return _gridMovement.PreviewFullPathToTile(targetTile);
    }

    public override void _Process(double delta)
    {
        _gridMovement.Process(delta);
    }

    public void Handle(TurnStart eventData)
    {
        if (eventData.Player != OwnerPlayer) return;

        _gridMovement.RefillMovementPoints();
    }

    public void Handle(PlayerHeroEvent eventData)
    {
        OwnerPlayer = eventData.Player;
    }

    public void RefillMovementPoints()
    {
        _gridMovement.RefillMovementPoints();
    }
    
    public Vector2I GridPosition
    {
        get
        {
            if (WorldMapSettings == null) return Vector2I.Zero;
            return WorldMapSettings.WorldToTilePosition(GlobalPosition);
        }
    }

    public bool OccupiesTile(Vector2I tilePosition)
    {
        return tilePosition == GridPosition;
    }

    public bool CanInteractFromTile(Vector2I tilePosition)
    {
        Vector2I gridOffset = GridPosition - tilePosition;
        return Mathf.Abs(gridOffset.X) <= 1
            && Mathf.Abs(gridOffset.Y) <= 1
            && gridOffset != Vector2I.Zero;
    }

    public void OnVisionUpdated(bool isRevealed)
    {
        if (_visualMeshNode != null)
        {
            _visualMeshNode.Visible = isRevealed;
        }
    }

    public void OnPlayerInteract(Hero playerHero)
    {
        if (playerHero == this) return;

        GD.Print($"COMBAT TRIGGERED: Player Hero {playerHero.Name} attacked Enemy Hero {Name}!");
    }

    internal void SnapHeroToTerrain()
    {
        this.SnapToTerrain();
    }

    internal float GetHeroTerrainHeight(Vector3 worldPosition)
    {
        return this.GetTerrainHeightAt(worldPosition);
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
