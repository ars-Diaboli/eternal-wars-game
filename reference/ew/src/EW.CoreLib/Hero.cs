using EW.CoreLib.Events;
using EW.CoreLib.Interfaces;
using EW.CoreLib.Messaging;
using EW.CoreLib.Types;
using EW.CoreLib.Types.Events;

namespace EW.CoreLib;

// public class HeroStopped : PlayerHeroEvent;
public class HeroStopRequested : PlayerHeroEvent;

public class Hero : IAnimatable, IFactionOwned, IOwnableObject, IMapActor, 
    IPersist<HeroStateDto>, 
    IHandleEvent<NewDay>,
    IHandleEvent<HeroStopRequested>
{
    public string ObjectId { get; set; }
    public Position InteractionTile => Path.Position;
    public Position Position => Path.Position;

    public byte[,] Size => MapObjectSizes.Hero;

    public Faction Faction { get; private set; }
    
    public GamePlayer OwnerPlayer { get; internal set; }

    public OwnedArmy OwnedArmy { get; internal set; } = new();
    public HeroData BaseStats { get; init; } = new();

    public HeroPath Path { get; } = new();

    public Hero() { }
    public Hero(Faction faction, Position position, GamePlayer ownerPlayer)
    {
        Faction = faction;
        OwnerPlayer = ownerPlayer;
        ObjectId = $"{OwnerPlayer.DisplayName} {Guid.NewGuid()}";

        SetPosition(position);
    }

    #region Animation

    public void ProcessAnimation(double timeDelta)
    {
        if (!Path.IsMoving || !Path.IsNextWaypointReachable())
        {
            Path.StopMovement();
            return;
        }

        Path.MovementProgressed(timeDelta * HeroSpeed());

        if (!Path.ProgressedToNextWaypoint()) return;

        var previousPosition = Position;
        Path.ProgressToNextWaypoint();
        Hub.Instance.Publish(new MapActorPositionChanged
            { Hero = this, Player = OwnerPlayer, PreviousPosition = previousPosition });

        if (Path.IsCloseToInteractionTarget())
        {
            Path.InteractWithTarget(this);

            Path.StopMovement();
            return;
        }

        if (Path.StopAtNextWaypoint || !Path.IsNextWaypointReachable())
        {
            Path.StopMovement();
            return;
        }
    }

    private float HeroSpeed() => OwnerPlayer?.ControlType == PlayerControlType.Ai
        ? GameSettings.Default.AiSpeed
        : GameSettings.Default.PlayerSpeed;
    
    #endregion

    public bool CanInteractFromTile(Position tilePosition)
    {
        throw new NotImplementedException();
    }

    public void OnPlayerInteract(Hero playerHero)
    {
        throw new NotImplementedException();
    }

    public void SetPosition(Position position)
    {
        Path.SetPosition(position);
        Hub.Instance.Publish(new MapActorPlaced{Hero = this, Player = OwnerPlayer});
    }

    #region IPersist

    // Seed for deserialization; the owner is wired up by the controller after construction.
    // internal static Hero Seed() => new(Faction.Neutral, Position.Zero, null!);

    // OwnerPlayerIndex is left at its default here; the controller resolves and patches it,
    // because only the controller holds the players list.
    public HeroStateDto ToState() => new()
    {
        ObjectId = ObjectId,
        Faction = Faction,
        Position = Position,
        BaseStats = BaseStats.ToState(),
        Path = Path.ToState()
    };

    public void FromState(HeroStateDto dto)
    {
        Faction = dto.Faction;
        ObjectId = dto.ObjectId;
        BaseStats.FromState(dto.BaseStats);

        // SetPosition resets movement state (including clearing waypoints), so the position
        // must be restored before the path state is applied.
        Path.SetPosition(dto.Position);
        Path.FromState(dto.Path);
    }

    #endregion
    
    #region EventHandlers
    
    public void Handle(NewDay e)
    {
        Path.CurrentMovementPoints = BaseStats.MaxMovementPoints;
    }
    
    public void Handle(HeroStopRequested e)
    {
        throw new NotImplementedException();
    }

    #endregion
}
