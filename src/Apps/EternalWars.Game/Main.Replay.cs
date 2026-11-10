using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// The replay viewer's composition and input (ADR-101, ADR-164): the Replays and Saves tabs, then a <see cref="ReplayPlayer"/> wired to the
/// same world, HUD and battle views as a game, watch-only, with <see cref="ReplayControlBar"/> along the bottom. Each frame, once the
/// heroes' steps and the battle's animations have played, the next command runs; a finished battle's result stays a moment. Jumps (next turn,
/// next day, a day) rebuild the views on a session put at that point.
/// </summary>
public partial class Main
{
    /// <summary>What the game app runs: a game, a replay being watched, or a battle played on from a replay.</summary>
    private enum GameMode
    {
        Play,

        /// <summary>The replay viewer (ADR-164): the record's commands play one at a time; nothing else issues one.</summary>
        Watch,

        /// <summary>"Play from here" in a battle: a separate game, not ranked, that goes back to the replay when the battle scene closes.</summary>
        TakeOver,
    }

    /// <summary>How long a battle's result stays before the replay goes on, at normal speed.</summary>
    private const double ResultSeconds = 1.5;

    /// <summary>The pause at the start of each turn, at normal speed, so turns read apart.</summary>
    private const double TurnSeconds = 0.5;

    private GameMode _mode = GameMode.Play;
    private ReplayPlayer? _replay;
    private ReplayControlBar? _replayBar;
    private bool _replayPlaying;
    private AnimationSpeed _replaySpeed = AnimationSpeed.Normal;

    /// <summary>0 = the whole map, else the player at this index − 1.</summary>
    private int _replayViewerIndex;

    private double _replayWait;
    private double _resultWait;

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
            (_mode, _replay, _replayPlaying, _replayViewerIndex) = (GameMode.Watch, player, true, 0);
            (_replayWait, _resultWait) = (0, 0);
            _generated = record.Generated;
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
    private void StopWatching()
    {
        (_mode, _replay, _replayBar, _replayPlaying) = (GameMode.Play, null, null, false);
    }

    /// <summary>Whose eyes the viewer's map is seen through: null for the whole map.</summary>
    private PlayerId? ReplayViewer() =>
        _replayViewerIndex > 0 && _replayViewerIndex <= _session.State.Players.Count ? _session.State.Players[_replayViewerIndex - 1].Id : null;

    private double Pause(double normalSeconds) => SpeedSettings.Seconds(_replaySpeed, normalSeconds);

    private void BuildReplayBar()
    {
        var layer = new CanvasLayer { Name = "ReplayBar", Layer = 4 };
        _game!.AddChild(layer);
        _replayBar = new ReplayControlBar();
        layer.AddChild(_replayBar);
        _replayBar.Bind(_session.State.Players.Select(p => p.Name).ToList(), _replaySpeed, _replayViewerIndex);
        _replayBar.TakingOver = _mode == GameMode.TakeOver;
        _replayBar.PlayToggled += ToggleReplay;
        _replayBar.SpeedChanged += speed =>
        {
            _replaySpeed = speed;
            ApplySpeeds();
        };
        _replayBar.NextTurnRequested += () => SeekReplay(_replay!.NextTurnPosition());
        _replayBar.NextDayRequested += () => SeekReplay(_replay!.NextDayPosition());
        _replayBar.DayRequested += day => SeekReplay(_replay!.DayPosition(day));
        _replayBar.ViewerChanged += index =>
        {
            _replayViewerIndex = index;
            _context.SetViewer(ReplayViewer());
        };
        _replayBar.SkipRequested += SkipBattle;
        _replayBar.TakeOverRequested += TakeOver;
        _replayBar.LeaveRequested += ShowMainMenu;
        _replayBar.BackRequested += BackToReplay;
        if (_mode == GameMode.TakeOver)
        {
            _battle.Closed += BackToReplay; // the battle is over and its result seen
            _replayBar.SetStatus("Playing the battle on from the replay: a separate game, not ranked; the replay stays as it is.");
        }
    }

    private void ToggleReplay()
    {
        if (_replay is { IsAtEnd: false })
        {
            _replayPlaying = !_replayPlaying;
        }
    }

    /// <summary>A turn started in the replay: a short pause before its first command.</summary>
    private void ReplayTurnStarted() => _replayWait = Pause(TurnSeconds);

    /// <summary>
    /// One frame of the viewer: the bar shows where the replay is; while playing, the next command runs once the views have caught up (heroes'
    /// steps, the battle's animations, a finished battle's result for a moment). The record's end pauses the replay.
    /// </summary>
    private void ProcessReplay(double delta)
    {
        if (_mode != GameMode.Watch || _replay is null || _context is null)
        {
            return;
        }

        if (_replay.IsAtEnd)
        {
            _replayPlaying = false;
        }

        ShowReplayState();
        if (!_replayPlaying || _menu.Visible || _context.Now < _context.PlaybackEnd || _battle.IsBusy)
        {
            return;
        }

        if (_battle.IsOpen && _battle.Result is not null)
        {
            _resultWait += delta;
            if (_resultWait >= Pause(ResultSeconds))
            {
                _resultWait = 0;
                _battleHud.Continue(); // the Necromancy screen first, if undead were raised, then the map
            }

            return;
        }

        if (_replayWait > 0)
        {
            _replayWait -= delta;
            return;
        }

        try
        {
            _replay.Step();
        }
        catch (InvalidOperationException e)
        {
            // Only a command from outside the record could make the watched game differ: rebuild it from the record where it was, paused.
            GD.PushWarning($"Replay viewer: {e.Message}");
            _replayPlaying = false;
            SeekReplay(_replay.Position);
        }
    }

    private void ShowReplayState()
    {
        if (_replayBar is null || _replay is null)
        {
            return;
        }

        var state = _replay.Session.State;
        var end = !_replay.IsAtEnd ? string.Empty
            : state.Outcome is { } outcome ? $"   ·   The game is over: {outcome.Reason}"
            : _replay.Record.Save is not null ? "   ·   The end of the save"
            : "   ·   The end of the replay";
        _replayBar.Display(new ReplayBarState(
            _replayPlaying,
            $"{state.Date}, {state.CurrentPlayer.Name}'s turn   ·   command {_replay.Position} of {_replay.Count}{end}",
            _replay.CurrentTurn.Date.AbsoluteDay,
            _replay.LastDay,
            _battle.IsOpen,
            _battle.IsOpen && _replay.CanTakeOver,
            _replay.IsAtEnd));
    }

    /// <summary>Jumps to <paramref name="position"/> (a turn's or day's start), back or forward: the views are built again on a session put there.</summary>
    private void SeekReplay(int position)
    {
        if (_replay is null || _mode != GameMode.Watch)
        {
            return;
        }

        _replay.Seek(position);
        (_replayWait, _resultWait) = (0, 0);
        BeginGame(_replay.Session, _replay.Record.Map, resume: true);
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
        while (_replay.IsInBattle(_replay.Position) && _replay.Step())
        {
        }

        _battle.FinishAnimations();
        _battle.Speed = speed;
        _resultWait = 0;
    }

    /// <summary>"Play from here": the battle goes on by hand in a separate game; the replay waits, paused, where it was.</summary>
    private void TakeOver()
    {
        if (_replay is not { CanTakeOver: true } || _mode != GameMode.Watch)
        {
            return;
        }

        var session = _replay.TakeOver();
        (_mode, _replayPlaying) = (GameMode.TakeOver, false);
        BeginGame(session, _replay.Record.Map, resume: true);
    }

    /// <summary>Leaves the battle played on and shows the replay again where it was, paused.</summary>
    private void BackToReplay()
    {
        if (_mode != GameMode.TakeOver || _replay is null)
        {
            return;
        }

        _mode = GameMode.Watch;
        BeginGame(_replay.Session, _replay.Record.Map, resume: true);
    }

    /// <summary>
    /// The viewer's input: Space plays or pauses, Enter or Escape leaves a battle's result, L switches the level, a right click opens the
    /// information window (through the eyes shown, or the current player's on the whole map); nothing issues a command. While a battle is played
    /// on, the battle's own input works and the map's does not. True when the event is the viewer's (the game's input then does nothing).
    /// </summary>
    private bool HandleReplayInput(InputEvent @event)
    {
        if (_mode == GameMode.TakeOver)
        {
            return !_battle.IsOpen;
        }

        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space }:
                ToggleReplay();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter or Key.Escape } when _battle.IsOpen && _battle.Result is not null:
                _battleHud.Continue();
                break;
            case InputEventMouseMotion motion when _battle.IsOpen:
                _battleInput!.Hover(motion.Position); // the hovered stack's numbers in the battle HUD
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click when _battle.IsOpen:
                if (_session.State.Battle is { } shown && _battle.PickPoint(click.Position) is { } point && shown.UnitAt(BattleSpace.ToCell(point)) is { } unit)
                {
                    _info.Open(Core.Domain.Info.InfoSheets.BattleStack(shown, unit, _info.Detailed), click.Position);
                }

                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click:
                if (MapSpace.PickTile(_camera, click.Position, _context.Level) is { } pointed
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
