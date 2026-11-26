using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// The mouse and keys over an open <see cref="BattleView3D"/> with its <see cref="BattleHud"/>: point at a cell to preview, click to move, attack
/// (from the side the pointer is on), shoot or cast the armed spell; right click opens a stack's information window. The keys are
/// <see cref="BattleHotkeys"/>' (ADR-314): W wait, D defend, R retreat for the active unit's commander, Q quick combat for either human side
/// (not between two computers, ADR-195), C the spell book; Enter or Escape leave the result screen, Escape closes the HUD's window over the
/// battle (the spell book, the combat settings) or disarms a spell. While a window is open the field takes no commands: a click beside it
/// closes it. The pointer over the field puts what a click would do, with its estimate, on the status bar (<see cref="BattleHud.ShowHover"/>). The deployment step (ADR-072,
/// ADR-155) goes to a <see cref="DeploymentDragController"/>; Enter there starts the battle, Escape cancels a drag. F10 is the app's menu key.
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
        battle.Idle += () =>
        {
            if (_pointed && _battle.IsOpen && !OverHud && !_hud.WindowOpen && _hud.GetViewport() is { } viewport)
            {
                // The field settled under a still pointer: its preview and the status bar's line now (ADR-314). Where the pointer is now, not
                // where it last moved over the field: a button it pressed may have gone from under it.
                Hover(viewport.GetMousePosition());
            }
        };
    }

    /// <summary>The pointer has moved over the battle: the settled field may be hovered where it stands.</summary>
    private bool _pointed;

    /// <summary>The pointer is over a HUD control (the log, a badge, a button): the field's preview and line give way to the control's.</summary>
    private bool OverHud => _hud.GetViewport()?.GuiGetHoveredControl() is not null;

    /// <summary>
    /// A deployment drag over the HUD, which would otherwise take its motion and its release: for the app's <c>_Input</c>. The carried stack
    /// follows the pointer over a button or the log too, and is placed where the button is let go. True when the event was that release.
    /// </summary>
    public bool HandleRelease(InputEvent @event)
    {
        if (_deployment.IsPressed && @event is InputEventMouseMotion motion)
        {
            Point(motion.Position);
            return false; // the HUD still sees it (its hover)
        }

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
                Point(motion.Position);
                return false;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } when _hud.WindowOpen:
                _hud.CloseWindow(); // a click beside the window closes it and does nothing on the field
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                Click(click.Position);
                break;
            case InputEventKey { Pressed: true } key:
                return HandleKey(key.Keycode);
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } rightClick
                when _session.State.Battle is { } shown && _battle.PickPoint(rightClick.Position) is { } point && shown.UnitAt(BattleSpace.ToCell(point)) is { } unit:
                _info.Open(InfoSheets.BattleStack(shown, unit, _info.Detailed), rightClick.Position);
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>
    /// The pointer moved to <paramref name="screen"/>: over a HUD control (a badge, the action grid's gaps) the control names itself on the
    /// status bar, and while a window is open a click only closes it, so the field shows no preview; a carried stack still shows where it
    /// would stand. Elsewhere <see cref="Hover"/>.
    /// </summary>
    public void Point(Vector2 screen)
    {
        _pointed = true;
        if ((OverHud || _hud.WindowOpen) && !_deployment.IsDragging)
        {
            _battle.ShowIntent(null);
            GameCursors.Show(GameCursor.Pointer);
        }
        else
        {
            Hover(screen);
        }

        _deployment.Move(screen);
    }

    /// <summary>The pointer at <paramref name="screen"/>: the hovered stack's numbers in the HUD, and the action or the deployment placement it would make.</summary>
    private void Hover(Vector2 screen)
    {
        if (_session.State.Battle is not { } battle || _battle.PickPoint(screen) is not { } point)
        {
            _battle.ShowIntent(null);
            _hud.ShowUnit(null);
            _hud.ShowHover(null, null, null);
            GameCursors.Show(GameCursor.Pointer);
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
            _hud.ShowHover(battle, cell, null);
            GameCursors.Show(GameCursor.Pointer);
            return;
        }

        // The stand cells of a move or a blow are highlighted; the cursor says what a click does, a sword pointing from the side (ADR-204).
        var acting = _battle.Commander is not null;
        var action = acting && _battle.Result is null ? BattleTargeting.ActionAt(battle, cell, point.X, point.Y) : null;
        _battle.ShowIntent(action);
        _hud.ShowHover(battle, cell, action); // what a click would do, with its estimate, on the status bar (ADR-314)
        GameCursors.Show(_battle.Result is not null ? GameCursor.Pointer : BattleCursors.For(battle, acting, _hud.ArmedSpellIsArea, action));
    }

    /// <summary>A pressed key, by the battle's hotkey table; true when the battle used it.</summary>
    private bool HandleKey(Key keycode)
    {
        switch (BattleHotkeys.ActionOf(keycode.ToString()))
        {
            case BattleHotkeyAction.Cancel when _deployment.IsDragging:
                _deployment.Cancel();
                return true;
            case BattleHotkeyAction.Cancel when _hud.WindowOpen:
                _hud.CloseWindow();
                return true;
            case BattleHotkeyAction.SpellBook when _battle.IsOpen && _battle.Result is null:
                _hud.ToggleSpellBook();
                return true;
            case BattleHotkeyAction.Confirm or BattleHotkeyAction.Cancel when _battle.Result is not null:
                _hud.Continue();
                return true;
            case BattleHotkeyAction.Cancel when _hud.ArmedSpell is not null:
                _hud.Disarm();
                return true;
            case BattleHotkeyAction.Confirm when _battle.Deployer is { } deployer && !_hud.WindowOpen:
                _hud.Report(_session.Execute(new BattleStartCommand(deployer)));
                return true;
            case { } action when !_hud.WindowOpen && KeyCommand(action) is { } command:
                _hud.Report(_session.Execute(command));
                return true;
            default:
                return false;
        }
    }

    /// <summary>Wait, Defend and Retreat for the active unit's commander; Quick combat for either human side, both played here.</summary>
    private GameCommand? KeyCommand(BattleHotkeyAction action)
    {
        if (_battle.Result is not null)
        {
            return null;
        }

        if (action == BattleHotkeyAction.QuickCombat)
        {
            return _battle.QuickCombatPlayer is { } any ? new AutoResolveBattleCommand(any) : null;
        }

        return _battle.Commander is not { } player ? null : action switch
        {
            BattleHotkeyAction.Wait => new BattleWaitCommand(player),
            BattleHotkeyAction.Defend => new BattleDefendCommand(player),
            BattleHotkeyAction.Retreat => new BattleRetreatCommand(player),
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
