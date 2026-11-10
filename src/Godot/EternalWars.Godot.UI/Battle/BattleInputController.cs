using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// The mouse and keys over an open <see cref="BattleView3D"/> with its <see cref="BattleHud"/>: point at a cell to preview, click to move, attack
/// (from the side the pointer is on), shoot or cast the armed spell; W wait, D defend, R retreat for the active unit's commander, Q quick
/// combat for either human side; right click opens a stack's information window; Enter or Escape leave the result screen, Escape disarms a
/// spell. The deployment step (ADR-072, ADR-155) goes to a <see cref="DeploymentDragController"/>; Enter there starts the battle.
/// </summary>
public sealed class BattleInputController
{
    private readonly GameSession _session;
    private readonly BattleView3D _battle;
    private readonly BattleHud _hud;
    private readonly InfoWindow _info;
    private readonly DeploymentDragController _deployment;

    public BattleInputController(GameSession session, BattleView3D battle, BattleHud hud, InfoWindow info)
    {
        (_session, _battle, _hud, _info) = (session, battle, hud, info);
        _deployment = new DeploymentDragController(session, battle, hud);
    }

    /// <summary>
    /// Ends a deployment drag where the button is let go, even over the HUD (which would otherwise take the release): for the app's
    /// <c>_Input</c>. True when the event was that release.
    /// </summary>
    public bool HandleRelease(InputEvent @event)
    {
        if (_deployment.IsPressed && @event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release)
        {
            _deployment.Release(release.Position);
            return true;
        }

        return false;
    }

    /// <summary>Handles an unhandled input event while the battle is open; true when it was the battle's (the app marks it handled).</summary>
    public bool Handle(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                Hover(motion.Position);
                _deployment.Move(motion.Position);
                return false;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                Click(click.Position);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Escape } when _deployment.IsDragging:
                _deployment.Cancel();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } rightClick
                when _session.State.Battle is { } shown && _battle.PickPoint(rightClick.Position) is { } point && shown.UnitAt(BattleSpace.ToCell(point)) is { } unit:
                _info.Open(InfoSheets.BattleStack(shown, unit, _info.Detailed), rightClick.Position);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter or Key.Escape } when _battle.Result is not null:
                _hud.Continue();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Escape } when _hud.ArmedSpell is not null:
                _hud.Disarm();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter } when _battle.Deployer is { } deployer:
                _hud.Report(_session.Execute(new BattleStartCommand(deployer)));
                break;
            case InputEventKey { Pressed: true } key when KeyCommand(key.Keycode) is { } command:
                _hud.Report(_session.Execute(command));
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>The pointer at <paramref name="screen"/>: the hovered stack's numbers in the HUD, and the action or the deployment placement it would make.</summary>
    public void Hover(Vector2 screen)
    {
        if (_session.State.Battle is not { } battle || _battle.PickPoint(screen) is not { } point)
        {
            _battle.ShowIntent(null);
            _hud.ShowUnit(null);
            return;
        }

        var cell = BattleSpace.ToCell(point);
        _hud.ShowUnit(battle.UnitAt(cell));
        if (_battle.Deployer is not null)
        {
            // Deployment step (ADR-072): preview where the picked stack would stand.
            var picked = _battle.Picked is { } id ? battle.FindUnit(id) : null;
            var at = picked is null ? (CellPosition?)null : BattleTargeting.DeploymentPlacement(battle, picked, point.X, point.Y);
            _battle.ShowPlacement(at is { } p ? picked!.RectAt(p) : null, at is { } q && BattleSetup.DeploymentRefusal(battle, picked!, q) is null);
            return;
        }

        _battle.ShowIntent(_battle.Commander is not null ? BattleTargeting.ActionAt(battle, cell, point.X, point.Y) : null);
    }

    /// <summary>W wait, D defend, R retreat for the active unit's commander; Q quick combat for either human side.</summary>
    private GameCommand? KeyCommand(Key key)
    {
        if (_battle.Result is not null)
        {
            return null;
        }

        if (key == Key.Q)
        {
            return (_session.Battles.Commander(BattleSide.Attacker) ?? _session.Battles.Commander(BattleSide.Defender)) is { } any
                ? new AutoResolveBattleCommand(any)
                : null;
        }

        return _battle.Commander is not { } player ? null : key switch
        {
            Key.W => new BattleWaitCommand(player),
            Key.D => new BattleDefendCommand(player),
            Key.R => new BattleRetreatCommand(player),
            _ => null,
        };
    }

    private void Click(Vector2 screen)
    {
        if (_battle.Deployer is { } deployer && _session.State.Battle is { } deploying && _battle.PickPoint(screen) is { } spot)
        {
            _deployment.Press(deployer, deploying, spot, screen);
            return;
        }

        if (_battle.Commander is not { } player || _session.State.Battle is not { } battle || _battle.PickPoint(screen) is not { } point)
        {
            return;
        }

        if (_hud.TryCastAt(BattleSpace.ToCell(point)))
        {
            _battle.ShowIntent(null);
            return;
        }

        if (BattleTargeting.CommandAt(player, battle, BattleSpace.ToCell(point), point.X, point.Y) is { } command)
        {
            _battle.ShowIntent(null);
            _hud.Report(_session.Execute(command));
        }
    }
}
