using EW.Scripts.DataTypes;
using EW.Scripts.Messaging;
using EW.Scripts.Players;
using Godot;

namespace EW.Scripts;

public sealed class AiTurnStarted : IGameEvent
{
    public GamePlayer Player { get; init; }
}

public sealed class AiTurnCompleted : IGameEvent
{
    public GamePlayer Player { get; init; }
}

public partial class AiBrain : Node
{
    [Export] public Heroes.Hero ControlledHero { get; set; }

    private bool _isExecutingTurn = false;

    public async void ExecuteAiTurn()
    {
        GamePlayer controlledPlayer = ResolveControlledPlayer();
        Hub.Instance?.Publish(new AiTurnStarted { Player = controlledPlayer });

        if (ControlledHero == null || !IsInstanceValid(ControlledHero))
        {
            GD.PushWarning("AI Brain: ControlledHero is not assigned. Skipping opponent turn.");
            CompleteAiTurn();
            return;
        }

        _isExecutingTurn = true;
        GD.Print($"AI Brain: Active turn for {ControlledHero.Name}. Current Move Points: {ControlledHero.CurrentMovementPoints}");

        while (ControlledHero.CurrentMovementPoints >= 10 && _isExecutingTurn)
        {
            IWorldMapObject target = FindNearestObjective();

            if (target != null)
            {
                Vector3I targetCoords3D = new Vector3I(target.GridPosition.X, 0, target.GridPosition.Y);

                GD.Print($"AI Brain: Targeted nearest objective at {target.GridPosition}. Moving!");

                bool movementStarted = ControlledHero.MoveToGridCell(targetCoords3D);
                if (!movementStarted)
                {
                    GD.Print($"AI Brain: Could not path to objective at {target.GridPosition}. Ending AI turn.");
                    break;
                }

                await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
                while (!ControlledHero.IsIdle)
                {
                    await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
                }
            }
            else
            {
                GD.Print("AI Brain: No targets left on the map.");
                break;
            }
        }

        _isExecutingTurn = false;
        CompleteAiTurn();
    }

    private IWorldMapObject FindNearestObjective()
    {
        IWorldMapObject closest = null;
        float minDistance = float.MaxValue;

        foreach (Node node in GetParent().GetChildren())
        {
            if (node is IWorldMapObject mapObject && mapObject != ControlledHero && IsInstanceValid(node))
            {
                GamePlayer controlledPlayer = ResolveControlledPlayer();
                if (mapObject is ICapturableMapObject capturableObject && ReferenceEquals(capturableObject.OwnerPlayer, controlledPlayer))
                {
                    continue;
                }

                if (mapObject is Heroes.Hero hero && ReferenceEquals(hero.OwnerPlayer, controlledPlayer))
                {
                    continue;
                }

                var fogSystem = ResolveFogSystem();
                if (fogSystem != null && !fogSystem.IsCellRevealed(mapObject.GridPosition))
                {
                    continue;
                }

                float distance = ControlledHero.GlobalPosition.DistanceTo((node as Node3D).GlobalPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = mapObject;
                }
            }
        }

        return closest;
    }

    private void CompleteAiTurn()
    {
        _isExecutingTurn = false;
        Hub.Instance?.Publish(new AiTurnCompleted { Player = ResolveControlledPlayer() });
    }

    private FogOfWarTextureSystem ResolveFogSystem()
    {
        foreach (Node sibling in GetParent().GetChildren())
        {
            if (sibling is FogOfWarTextureSystem fogSystem)
            {
                return fogSystem;
            }
        }

        return null;
    }

    private GamePlayer ResolveControlledPlayer()
    {
        return ControlledHero?.OwnerPlayer;
    }
}
