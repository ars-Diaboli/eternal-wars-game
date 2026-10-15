using System;
using System.Linq;
using EW.CoreLib.Events;
using EW.CoreLib.Messaging;
using EW.CoreLib.Types;

namespace EW.CoreLib;

public partial class GameScenarioController
{
    private bool _aiTurnInProgress = false;
    private bool _aiMovementStarted = false;
    private Hero? _aiHero = null;
    private Position? _aiTargetPosition = null;

    partial void UpdateAi(double timeDelta)
    {
        var currentPlayer = TurnController?.CurrentPlayer;
        if (currentPlayer == null || currentPlayer.ControlType != PlayerControlType.Ai)
        {
            _aiTurnInProgress = false;
            _aiMovementStarted = false;
            _aiHero = null;
            _aiTargetPosition = null;
            return;
        }

        if (!_aiTurnInProgress)
        {
            _aiHero = currentPlayer.Heroes.FirstOrDefault();
            if (_aiHero == null || _aiHero.Path.CurrentMovementPoints < 100)
            {
                EndAiTurn();
                return;
            }

            _aiTargetPosition = ChooseRandomTargetPosition();
            if (_aiTargetPosition == null)
            {
                EndAiTurn();
                return;
            }

            _aiTurnInProgress = true;
            _aiMovementStarted = false;
        }

        if (_aiTurnInProgress && _aiHero != null && _aiTargetPosition != null)
        {
            if (!_aiMovementStarted)
            {
                var oldSelectedHero = UiHost.SelectedHero;
                UiHost.SelectHero(_aiHero);

                var originalHostPlayer = UiHost.HostPlayer;
                UiHost.HostPlayer = TurnController.CurrentPlayer;

                // Send LeftClickOnMap to build the path
                Handle(new LeftClickOnMap { Position = _aiTargetPosition.Value });

                // If path was successfully built, start moving!
                if (_aiHero?.Path?.MovementWaypoints.Count > 0)
                {
                    // Send LeftClickOnMap again to start movement
                    Handle(new LeftClickOnMap { Position = _aiTargetPosition.Value });
                    _aiMovementStarted = true;
                }
                else
                {
                    EndAiTurn();
                }

                UiHost.HostPlayer = originalHostPlayer;

                if (oldSelectedHero != null)
                {
                    UiHost.SelectHero(oldSelectedHero);
                }
            }
            else
            {
                if (!_aiHero.Path.IsMoving || _aiHero.Path.CurrentMovementPoints < 100)
                {
                    EndAiTurn();
                }
            }
        }
    }

    private void EndAiTurn()
    {
        _aiTurnInProgress = false;
        _aiMovementStarted = false;
        _aiHero = null;
        _aiTargetPosition = null;

        Hub.Instance.Publish(new PlayerEndedTurn { Player = TurnController?.CurrentPlayer });
    }

    private Position? ChooseRandomTargetPosition()
    {
        if (Map?.Cells == null || _aiHero == null) return null;

        int height = Map.Cells.GetLength(0);
        int width = Map.Cells.GetLength(1);
        var rand = new System.Random();

        for (int attempt = 0; attempt < 100; attempt++)
        {
            int r = rand.Next(height);
            int c = rand.Next(width);

            if (!Map.Cells[r, c].Blocked && !Map.Cells[r, c].Occupied)
            {
                var target = new Position((uint)r, (uint)c);
                var totalMovementBudget = _aiHero.BaseStats?.MaxMovementPoints ?? _aiHero.Path.CurrentMovementPoints;
                
                var path = Map.Pathfinding.GetPath(
                    _aiHero.Position, target, _aiHero.Faction,
                    _aiHero.Path.CurrentMovementPoints, totalMovementBudget);

                if (path != null && path.Length > 0)
                {
                    return target;
                }
            }
        }

        return null;
    }
}
