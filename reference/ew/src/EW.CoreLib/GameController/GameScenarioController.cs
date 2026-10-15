using EW.CoreLib.Interfaces;
using EW.CoreLib.Map;
using EW.CoreLib.Messaging;
using EW.CoreLib.Types;
using EW.CoreLib.Types.Events;
using System.Text.Json;

namespace EW.CoreLib;

public class LeftClickOnMap : IGameEvent
{
    public Position Position { get; set; }
}
public class RightClickOnMap : IGameEvent
{
    public Position Position { get; set; }
}

public partial class GameScenarioController : 
    IHandleEvent<LeftClickOnMap>, 
    IHandleEvent<RightClickOnMap>, 
    // IHandleEvent<HeroStopped>, 
    IPersist<GameScenarioControllerStateDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true
    };

    public ScenarioMetadata Metadata { get; set; }
    public List<GamePlayer> Players { get; set; } = new();
    public GameMap Map { get; set; }
    public UiHost UiHost { get; set; }
    public TurnController TurnController { get; private set; }

    public GameScenarioController()
    {
        Hub.Instance.Subscribe(this);
    }
    public GameScenarioController(GameMap map, List<GamePlayer> players, GamePlayer hostPlayer, GamePlayer? currentPlayer = null)
        : this()
    {
        Map = map;
        Players = players;
        UiHost = new UiHost {HostPlayer = hostPlayer};
        var environment = new GamePlayer
        {
            DisplayName = "Environment", 
            PlayerColor = PlayerColor.Neutral, 
            ControlType = PlayerControlType.Environment
        };
        TurnController = new TurnController(players, environment, currentPlayer ?? players[0]);
    }
    
    public void Handle(LeftClickOnMap e)
    {
        var hero = UiHost.SelectedHero;

        if (UiHost.HostPlayer != TurnController.CurrentPlayer) return;

        if (hero == null)
        {
            if (Map.GetMapObjectAt(e.Position) is ITown)
            {
                // todo: open town screen
            }
            return;
        }
        
        if (hero.Path.IsMoving) return;

        if (hero.Position == e.Position)
        {
            // todo: open hero screen
            return;
        }
        
        if (hero.Path.IsNextWaypointReachable() && hero.Path.IsMovementDestination(e.Position))
        {
            hero.Path.StartMovement();
            return;
        }

        var mapObject = Map.GetMapObjectAt(e.Position);

        if (mapObject is IRemovableMapObject removableObject)
        {
            HandleRemovableObjectClick(hero, removableObject);
            return;
        }

        // if (mapObject is null)
        // {
        //     
        // }

        var totalMovementBudget = hero.BaseStats?.MaxMovementPoints ?? hero.Path.CurrentMovementPoints;

        var path = Map.Pathfinding.GetPath(
            hero.Position, e.Position, hero.Faction,
            hero.Path.CurrentMovementPoints, totalMovementBudget);

        if (!path.Any()) return;

        hero.Path.AssignNewPath(path);
    }

    public void Handle(RightClickOnMap e)
    {
        if (UiHost.HostPlayer != TurnController.CurrentPlayer) return;
        if (UiHost.SelectedHero?.Path.IsMoving != true) return;

        UiHost.SelectedHero.Path.RequestStopAtNearestWaypoint();
    }

    // public void Handle(HeroStopped eventData)
    // {
    // }

    private void HandleRemovableObjectClick(Hero hero, IRemovableMapObject removableObject)
    {
        if (IsAdjacentToObject(hero.Position, removableObject))
        {
            removableObject.OnPlayerInteract(hero);
            hero.Path.ClearPendingInteractionTarget();
            hero.Path.MovementWaypoints.Clear();
            return;
        }

        var bestPath = GetShortestInteractionPath(hero, removableObject);
        if (bestPath.Length == 0) return;

        hero.Path.MovementWaypoints.Clear();
        hero.Path.SetPendingInteractionTarget(removableObject);

        foreach (var waypoint in bestPath)
        {
            hero.Path.MovementWaypoints.Enqueue(waypoint);
        }
    }

    private PathWaypoint[] GetShortestInteractionPath(Hero hero, IWorldMapObject target)
    {
        var totalMovementBudget = hero.BaseStats?.MaxMovementPoints ?? hero.Path.CurrentMovementPoints;

        return GetInteractionPositions(target)
            .Select(position => Map.Pathfinding.GetPath(
                hero.Position, position, hero.Faction,
                hero.Path.CurrentMovementPoints, totalMovementBudget))
            .Where(path => path.Length > 1)
            .OrderBy(path => path.Sum(step => step.MovementCost))
            .ThenBy(path => path.Length)
            .Select(path => path[..^1])
            .FirstOrDefault() ?? [];
    }

    private IEnumerable<Position> GetInteractionPositions(IWorldMapObject target)
    {
        var positions = new HashSet<Position>();

        for (uint x = 0; x < target.Width; x++)
        for (uint y = 0; y < target.Height; y++)
        {
            if (target.Size[y, x] == 1)
                positions.Add(target.Position + new Position(y, x));
        }

        return positions;
    }

    private bool IsAdjacentToObject(Position position, IWorldMapObject target)
    {
        return GetInteractionPositions(target).Any(targetPosition =>
            position != targetPosition &&
            Math.Abs((int)position.Vertical - (int)targetPosition.Vertical) +
            Math.Abs((int)position.Horizontal - (int)targetPosition.Horizontal) == 1);
    }
}

