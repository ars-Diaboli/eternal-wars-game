using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// The replay viewer's composition and input (ADR-101, ADR-164): the Replays and Saves tabs, then a <see cref="ReplayController"/> (ADR-222)
/// wired to the same world, HUD and battle views as a game, watch-only, with <see cref="ReplayControlBar"/> along the bottom. Each frame, once
/// the heroes' steps and the battle's animations have played, the controller runs the next command; a finished battle's result stays a moment.
/// Jumps (next turn, next day, a day) rebuild the views on a session put at that point. "Play from here" plays a battle on by hand (one only the
/// computer fought from its start, ADR-233) and comes back, or on the map leaves the viewer for a game of the player's own from that point.
/// </summary>
public partial class Main
{
    /// <summary>The replay being watched (or a battle played on from it, <see cref="ReplayController.IsTakingOver"/>); null while playing a game.</summary>
    private ReplayController? _replay;
    private ReplayControlBar? _replayBar;

    /// <summary>The viewer's speed, kept for the next replay watched.</summary>
    private AnimationSpeed _replaySpeed = AnimationSpeed.Normal;

    /// <summary>Whether a game is played (not a replay watched, nor a battle played on from one).</summary>
    private bool Playing => _replay is null;

    /// <summary>Whether a replay is watched (ADR-164): the record's commands play one at a time; nothing else issues one.</summary>
    private bool Watching => _replay is { IsTakingOver: false };

    private void ShowReplays()
    {
        var screen = new ReplayListScreen();
        ShowSetup(screen, back: ShowMainMenu);
        screen.Bind(SaveItems(GameSaves.ListReplays(ProjectSettings.GlobalizePath(ReplaysFolder))), SaveItems(GameSaves.List(_saveFolder)));
        screen.BackRequested += ShowMainMenu;
        screen.WatchRequested += path => Watch(path, screen);
    }

    /// <summary>Opens a replay or a save in the viewer, playing from day 1; a record that does not replay stays on the list with the reason.</summary>
    private void Watch(string path, ReplayListScreen? screen)
    {
        try
        {
            var record = GameRecordSerializer.Load(path);
            var player = ReplayPlayer.Open(record, GameContent.All);
            CloseSetup();
            _replay = new ReplayController(player, _replaySpeed);
            _generated = record.Generated;
            _campaign = null; // the viewer only watches
            BeginGame(player.Session, record.Map, resume: true);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or System.Text.Json.JsonException or IOException or KeyNotFoundException)
        {
            var message = $"This cannot be watched: {e.Message}";
            if (screen is null)
            {
                GD.PushError(message); // --replay= from the command line
            }
            else
            {
                screen.SetMessage(message);
            }
        }
    }

    /// <summary>Back to playing games: the viewer's state goes.</summary>
    private void StopWatching() => (_replay, _replayBar) = (null, null);

    private void BuildReplayBar()
    {
        var replay = _replay!;
        var layer = new CanvasLayer { Name = "ReplayBar", Layer = 4 };
        _game!.AddChild(layer);
        _replayBar = new ReplayControlBar();
        layer.AddChild(_replayBar);
        _replayBar.Bind(_session.State.Players.Select(p => p.Name).ToList(), _replaySpeed, replay.ViewerIndex);
        _replayBar.TakingOver = replay.IsTakingOver;
        _replayBar.PlayToggled += replay.Toggle;
        _replayBar.SpeedChanged += speed =>
        {
            (_replaySpeed, replay.Speed) = (speed, speed);
            ApplySpeeds();
        };
        _replayBar.NextTurnRequested += () => SeekReplay(replay.Player.NextTurnPosition());
        _replayBar.NextDayRequested += () => SeekReplay(replay.Player.NextDayPosition());
        _replayBar.DayRequested += day => SeekReplay(replay.Player.DayPosition(day));
        _replayBar.ViewerChanged += index =>
        {
            replay.ViewerIndex = index;
            _context.SetViewer(replay.Viewer(_session.State));
        };
        _replayBar.SkipRequested += SkipBattle;
        _replayBar.TakeOverRequested += TakeOver;
        _replayBar.LeaveRequested += ShowMainMenu;
        _replayBar.BackRequested += BackToReplay;
        if (replay.IsTakingOver)
        {
            _battle.Closed += BackToReplay; // the battle is over and its result seen
            _replayBar.SetStatus(replay.TakeOverStatus);
        }
    }

    /// <summary>A turn started in the replay: a short pause before its first command.</summary>
    private void ReplayTurnStarted() => _replay?.TurnStarted();

    /// <summary>
    /// One frame of the viewer: the bar shows where the replay is; while playing, the controller runs the next command once the views have
    /// caught up (heroes' steps, the battle's animations, a finished battle's result for a moment).
    /// </summary>
    private void ProcessReplay(double delta)
    {
        if (!Watching || _replay is not { } replay || _context is null)
        {
            return;
        }

        ShowReplayState();
        var frame = replay.Advance(
            delta,
            new ReplayViews(_menu.Visible, _context.Now < _context.PlaybackEnd, _battle.IsBusy, _battle.IsOpen && _battle.Result is not null));
        switch (frame.Kind)
        {
            case ReplayAdvance.LeaveResult:
                _battleHud.Continue(); // the Necromancy screen first, if undead were raised, then the map
                break;
            case ReplayAdvance.Diverged:
                // The controller has put the record's game back where it was, paused: the views are built again on it.
                GD.PushWarning($"Replay viewer: {frame.Problem}");
                BeginGame(replay.Player.Session, replay.Player.Record.Map, resume: true);
                break;
        }
    }

    private void ShowReplayState()
    {
        if (_replayBar is null || _replay is not { } replay)
        {
            return;
        }

        _replayBar.Display(new ReplayBarState(
            replay.IsPlaying,
            replay.Status(),
            replay.Player.CurrentTurn.Date.AbsoluteDay,
            replay.Player.LastDay,
            _battle.IsOpen,
            replay.CanPlayFromHere(_battle.IsOpen),
            replay.Player.IsAtEnd,
            _battle.IsOpen && replay.Player.TakesOverFromTheStart));
    }

    /// <summary>Jumps to <paramref name="position"/> (a turn's or day's start), back or forward: the views are built again on a session put there.</summary>
    private void SeekReplay(int position)
    {
        if (!Watching || _replay is not { } replay)
        {
            return;
        }

        replay.Seek(position);
        BeginGame(replay.Player.Session, replay.Player.Record.Map, resume: true);
    }

    /// <summary>
    /// "Skip to the result": the battle's remaining commands run at once with the battle shown instantly, so its result screen comes up; a battle
    /// the core played in one command (the computer's, quick combat) only skips its animation.
    /// </summary>
    private void SkipBattle()
    {
        if (_replay is null || !_battle.IsOpen)
        {
            return;
        }

        var speed = _battle.Speed;
        _battle.Speed = AnimationSpeed.Instant;
        _battle.FinishAnimations();
        _replay.SkipBattle();
        _battle.FinishAnimations();
        _battle.Speed = speed;
    }

    /// <summary>
    /// "Play from here": in a battle, the battle goes on by hand in a separate game while the replay waits, paused, where it was; on the map,
    /// the viewer is left for <see cref="PlayOn"/>.
    /// </summary>
    private void TakeOver()
    {
        if (!Watching || _replay is not { } replay)
        {
            return;
        }

        if (!_battle.IsOpen)
        {
            PlayOn();
            return;
        }

        if (replay.TakeOver(out var problem) is { } session)
        {
            BeginGame(session, replay.Player.Record.Map, resume: true);
        }
        else if (problem is not null)
        {
            GD.PushWarning($"Replay viewer: {problem}");
            _replayBar?.SetStatus($"This battle cannot be played: {problem}");
        }
    }

    /// <summary>
    /// "Play from here" on the adventure map: the viewer closes and the game goes on from this command as a game of the player's own, as if a
    /// save cut here were loaded (saves, autosaves, the end screen and the ranking as usual); the replay file does not change. The map is
    /// seen through a person's eyes: the current player's, else the next person's in turn order (a computer's turn plays on first).
    /// </summary>
    private void PlayOn()
    {
        if (_replay?.Player is not { CanPlayOn: true } replay)
        {
            return;
        }

        var session = replay.PlayOn();
        var map = replay.Record.Map;
        StopWatching();
        BeginGame(session, map, resume: true);
        var state = session.State;
        if (ReplayPlayer.PersonToShow(state) is { } person && person != state.CurrentPlayer.Id)
        {
            _context.SetViewer(person);
            _mapInput.Select(_focus.FirstHeroOf(person), focus: true);
        }

        _status.Text = $"Playing on from the replay, {state.Date}: your own game now; the replay stays as it is.";
    }

    /// <summary>Leaves the battle played on and shows the replay again where it was, paused.</summary>
    private void BackToReplay()
    {
        if (_replay is { } replay && replay.BackToReplay())
        {
            BeginGame(replay.Player.Session, replay.Player.Record.Map, resume: true);
        }
    }

    /// <summary>
    /// The viewer's input: Space plays or pauses, Enter or Escape leaves a battle's result, L switches the level, a right click opens the
    /// information window (through the eyes shown, or the current player's on the whole map); nothing issues a command. While a battle is played
    /// on, the battle's own input works and the map's does not. True when the event is the viewer's (the game's input then does nothing).
    /// </summary>
    private bool HandleReplayInput(InputEvent @event)
    {
        if (_replay is { IsTakingOver: true })
        {
            return !_battle.IsOpen;
        }

        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space }:
                _replay?.Toggle();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Escape } when _battle.IsOpen && _battleHud.WindowOpen:
                _battleHud.CloseWindow(); // the combat settings over the replayed battle (ADR-314)
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter or Key.Escape } when _battle.IsOpen && _battle.Result is not null:
                _battleHud.Continue();
                break;
            case InputEventMouseMotion motion when _battle.IsOpen:
                _battleInput!.Point(motion.Position); // the hovered stack's numbers in the battle HUD
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } when _battle.IsOpen && _battleHud.WindowOpen:
                _battleHud.CloseWindow(); // a click beside the combat settings closes them, as in a game
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click when _battle.IsOpen:
                if (_session.State.Battle is { } shown && _battle.PickPoint(click.Position) is { } point && shown.UnitAt(BattleSpace.ToCell(point)) is { } unit)
                {
                    _info.Open(Core.Domain.Info.InfoSheets.BattleStack(shown, unit, _info.Detailed), click.Position);
                }

                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click:
                if (MapSpace.PickTile(_camera, click.Position, _session.State.Map, _context.Level) is { } pointed
                    && Core.Domain.Info.InfoSheets.MapCell(_session.State, _context.Viewer ?? _session.State.CurrentPlayer.Id, pointed, _info.Detailed) is { } sheet)
                {
                    _info.Open(sheet, click.Position);
                }

                break;
            case InputEventKey { Pressed: true, Keycode: Key.L } when !_battle.IsOpen && _session.State.Map.Levels > 1:
                _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels);
                break;
            default:
                return true;
        }

        GetViewport().SetInputAsHandled();
        return true;
    }
}
