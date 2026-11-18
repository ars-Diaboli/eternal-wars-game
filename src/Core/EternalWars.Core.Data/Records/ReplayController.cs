using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Data.Records;

/// <summary>What one frame of the replay viewer did (<see cref="ReplayController.Advance"/>).</summary>
public enum ReplayAdvance
{
    /// <summary>Nothing ran: paused, held by the front-end, at the end, or waiting a moment.</summary>
    Waited,

    /// <summary>A finished battle's result has been shown long enough: the front-end leaves it.</summary>
    LeaveResult,

    /// <summary>The next command ran.</summary>
    Stepped,

    /// <summary>The watched game differed from its record (<see cref="ReplayFrame.Problem"/>): paused; the front-end shows the record where it was.</summary>
    Diverged,
}

/// <summary>A frame of the replay viewer: what it did and, when the watched game diverged, why.</summary>
public readonly record struct ReplayFrame(ReplayAdvance Kind, string? Problem = null);

/// <summary>
/// The replay viewer's stepping (ADR-164, ADR-222) over a <see cref="ReplayPlayer"/>: playing or paused, the speed, whose eyes the map is seen
/// through, and the pauses that let a game be followed (a moment at each turn's start, a finished battle's result for a moment). The front-end
/// calls <see cref="Advance"/> once a frame with the time passed and whether its views are still catching up; the next command runs once they
/// have. The record's end pauses it. "Play from here" in a battle (<see cref="TakeOver"/>) plays it on in a separate session while the replay
/// waits, paused, where it was.
/// </summary>
public sealed class ReplayController(ReplayPlayer player, AnimationSpeed speed = AnimationSpeed.Normal)
{
    /// <summary>How long a battle's result stays before the replay goes on, at normal speed.</summary>
    public const double ResultSeconds = 1.5;

    /// <summary>The pause at the start of each turn, at normal speed, so turns read apart.</summary>
    public const double TurnSeconds = 0.5;

    private bool _playing = true;
    private double _turnWait;
    private double _resultWait;

    public ReplayPlayer Player { get; } = player;

    /// <summary>Every hero's and battle's speed in the viewer.</summary>
    public AnimationSpeed Speed { get; set; } = speed;

    /// <summary>Whether the replay plays (the record's end pauses it at the next frame).</summary>
    public bool IsPlaying => _playing && !Player.IsAtEnd;

    /// <summary>0 = the whole map, else the player at this index − 1.</summary>
    public int ViewerIndex { get; set; }

    /// <summary>Whether a battle is being played on from the replay (<see cref="TakeOver"/>).</summary>
    public bool IsTakingOver { get; private set; }

    /// <summary>Whose eyes the map is seen through in <paramref name="state"/>: null for the whole map.</summary>
    public PlayerId? Viewer(GameState state) =>
        ViewerIndex > 0 && ViewerIndex <= state.Players.Count ? state.Players[ViewerIndex - 1].Id : null;

    /// <summary>Plays or pauses; nothing at the record's end.</summary>
    public void Toggle()
    {
        if (!Player.IsAtEnd)
        {
            _playing = !_playing;
        }
    }

    /// <summary>A turn started in the replay: a short pause before its first command.</summary>
    public void TurnStarted() => _turnWait = SpeedSettings.Seconds(Speed, TurnSeconds);

    /// <summary>Jumps to <paramref name="position"/> (a turn's or day's start), back or forward, on a new watched session.</summary>
    public void Seek(int position)
    {
        Player.Seek(position);
        (_turnWait, _resultWait) = (0, 0);
    }

    /// <summary>
    /// One frame of <paramref name="seconds"/>: while playing and not <paramref name="held"/> (the menu open, heroes' steps or a battle's
    /// animations still shown), a finished battle's result (<paramref name="resultShown"/>) stays a moment, a turn's start pauses a moment, else the
    /// next command runs. A command the record's game refuses pauses the replay.
    /// </summary>
    public ReplayFrame Advance(double seconds, bool held, bool resultShown)
    {
        if (Player.IsAtEnd)
        {
            _playing = false;
        }

        if (!_playing || held)
        {
            return new ReplayFrame(ReplayAdvance.Waited);
        }

        if (resultShown)
        {
            _resultWait += seconds;
            if (_resultWait < SpeedSettings.Seconds(Speed, ResultSeconds))
            {
                return new ReplayFrame(ReplayAdvance.Waited);
            }

            _resultWait = 0;
            return new ReplayFrame(ReplayAdvance.LeaveResult);
        }

        if (_turnWait > 0)
        {
            _turnWait -= seconds;
            return new ReplayFrame(ReplayAdvance.Waited);
        }

        try
        {
            Player.Step();
            return new ReplayFrame(ReplayAdvance.Stepped);
        }
        catch (InvalidOperationException e)
        {
            // Only a command from outside the record could make the watched game differ: the front-end rebuilds it from the record, paused.
            _playing = false;
            return new ReplayFrame(ReplayAdvance.Diverged, e.Message);
        }
    }

    /// <summary>"Skip to the result": the battle's remaining commands run at once; its result shows from the start of its moment.</summary>
    public void SkipBattle()
    {
        while (Player.IsInBattle(Player.Position) && Player.Step())
        {
        }

        _resultWait = 0;
    }

    /// <summary>
    /// "Play from here" in a battle: a separate session at this point, in which the battle goes on by hand while the replay waits, paused; null
    /// when the battle cannot be taken over.
    /// </summary>
    public GameSession? TakeOver()
    {
        if (IsTakingOver || !Player.CanTakeOver)
        {
            return null;
        }

        var session = Player.TakeOver();
        (IsTakingOver, _playing) = (true, false);
        return session;
    }

    /// <summary>Leaves the battle played on: the replay is watched again where it was, paused. False when no battle was played on.</summary>
    public bool BackToReplay()
    {
        if (!IsTakingOver)
        {
            return false;
        }

        IsTakingOver = false;
        return true;
    }

    /// <summary>The viewer's status line: the date, whose turn, the command, and the end of the game, save or replay.</summary>
    public string Status()
    {
        var state = Player.Session.State;
        var end = !Player.IsAtEnd ? string.Empty
            : state.Outcome is { } outcome ? $"   ·   The game is over: {outcome.Reason}"
            : Player.Record.Save is not null ? "   ·   The end of the save"
            : "   ·   The end of the replay";
        return $"{state.Date}, {state.CurrentPlayer.Name}'s turn   ·   command {Player.Position} of {Player.Count}{end}";
    }
}
