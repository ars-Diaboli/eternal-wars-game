using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Battle;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// The deployment step's pointer (ADR-072, ADR-155) over a <see cref="BattleView3D"/>: a press on an own stack picks it and starts dragging it,
/// a press elsewhere puts the picked stack there (click to place). While dragging, the stack's model follows the pointer; letting go puts it
/// where it was dropped. Dropped off the field, back on its own cells or where it may not stand, it goes back (and stays picked). Letting go
/// without a drag leaves it picked for click to place. Placing issues <see cref="BattleDeployCommand"/>; the <see cref="BattleHud"/> reports it.
/// </summary>
public sealed class DeploymentDragController(GameSession session, BattleView3D battle, BattleHud hud)
{
    /// <summary>A pointer moved this far while the button is held makes the press a drag, not a click.</summary>
    public const float DragPixels = 6;

    private DeployDrag? _drag;

    /// <summary>A press was made on a stack and its button is not let go yet (the release ends it, even over the HUD).</summary>
    public bool IsPressed => _drag is not null;

    /// <summary>The pressed stack is being dragged (the pointer moved far enough).</summary>
    public bool IsDragging => _drag is { Moved: true };

    /// <summary>A press at <paramref name="point"/> on the field (<paramref name="screen"/> on the screen) in <paramref name="player"/>'s deployment.</summary>
    public void Press(PlayerId player, BattleState state, Vector2 point, Vector2 screen)
    {
        if (state.UnitAt(BattleSpace.ToCell(point)) is { IsMachine: false } own && own.Side == state.Deployer)
        {
            if (own.Id != battle.Picked)
            {
                battle.Pick(own.Id);
            }

            _drag = new DeployDrag(own.Id, screen);
            return;
        }

        PlacePicked(player, state, point);
    }

    /// <summary>The pointer moved: while dragging, the stack's model follows it (the placement preview shows where it would stand).</summary>
    public void Move(Vector2 screen)
    {
        if (_drag is not { } drag || battle.Picked != drag.Unit)
        {
            return;
        }

        drag.Moved |= screen.DistanceTo(drag.From) >= DragPixels;
        if (drag.Moved)
        {
            battle.Carry(battle.PickPoint(screen));
        }
    }

    /// <summary>The button was let go at <paramref name="screen"/>.</summary>
    public void Release(Vector2 screen)
    {
        if (_drag is not { Moved: true } drag)
        {
            _drag = null;
            return;
        }

        _drag = null;
        if (battle.Deployer is not { } player || session.State.Battle is not { } state || state.FindUnit(drag.Unit) is not { } unit
            || battle.PickPoint(screen) is not { } point || BattleTargeting.DeploymentPlacement(state, unit, point.X, point.Y) == unit.Position)
        {
            battle.Carry(null);
            return;
        }

        if (!PlacePicked(player, state, point))
        {
            battle.Carry(null);
        }
    }

    /// <summary>Escape during a drag: the stack goes back.</summary>
    public void Cancel()
    {
        _drag = null;
        battle.Carry(null);
    }

    /// <summary>Puts the picked stack where <paramref name="point"/> places it; whether that was accepted.</summary>
    private bool PlacePicked(PlayerId player, BattleState state, Vector2 point)
    {
        if (battle.Picked is not { } id || state.FindUnit(id) is not { } picked)
        {
            return false;
        }

        var result = session.Execute(new BattleDeployCommand(player, id, BattleTargeting.DeploymentPlacement(state, picked, point.X, point.Y)));
        hud.Report(result);
        if (result.IsAccepted)
        {
            battle.Pick(null);
            battle.ShowPlacement(null, false);
        }

        return result.IsAccepted;
    }

    /// <summary>The stack being dragged, and where the press was.</summary>
    private sealed record DeployDrag(int Unit, Vector2 From)
    {
        public bool Moved { get; set; }
    }
}
