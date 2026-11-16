using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// HoMM-style sequential turns: every player acts once per day, in slot order.
/// When the last player ends the turn the calendar advances; a new week brings the towns' creature growth. What happens at each step is up to
/// the session's participants, in their order (<see cref="ITurnPhaseParticipant"/>); the service itself moves the calendar, publishes it
/// and rolls the week's events. An arena map's battle day (ADR-097) replaces the day, so the arena is not a participant.
/// <para>
/// A simultaneous day (MP-2, ADR-197; <see cref="GameOptions.SimultaneousFirstWeek"/>): every person's turn begins at once when the day opens
/// (<see cref="GameState.OpenTurns"/>), each ends its own, and once all have, the computer players play theirs one by one. A turn begins once a
/// day, so a day played again turn by turn (<see cref="PlayTurnByTurn"/>) brings no second income.
/// </para>
/// </summary>
public sealed class TurnService
{
    private readonly GameState _state;
    private readonly IEventPublisher _events;
    private readonly ArenaService _arena;
    private readonly IReadOnlyList<ITurnPhaseParticipant> _participants;

    internal TurnService(GameState state, IEventPublisher events, ArenaService arena, IReadOnlyList<ITurnPhaseParticipant> participants)
    {
        _state = state;
        _events = events;
        _arena = arena;
        _participants = participants;
    }

    internal void StartGame()
    {
        if (_state.IsStarted)
        {
            throw new InvalidOperationException("The game has already started.");
        }

        _state.IsStarted = true;
        _state.CurrentPlayerIndex = 0;
        Each(p => p.SetUpGame());
        Each(p => p.StockFirstWeek());
        _events.Publish(new GameStarted(_state.Date));
        _events.Publish(new DayStarted(_state.Date));

        // The first week has no growth to change, but a map that starts its events at once still gets its plague or its stacks.
        _state.NextWeekEvent = Roll(_state.Date);
        StartWeekEvent();
        Each(p => p.OpenDay());
        if (!OpenSimultaneousDay())
        {
            BeginTurn();
        }
    }

    /// <summary>Set when a command opened a simultaneous day; the session notes where the day's commands start and clears it.</summary>
    internal bool OpenedSimultaneousDay { get; set; }

    internal void EndTurn()
    {
        if (!_state.IsStarted)
        {
            throw new CommandRefusedException("The game has not started yet.");
        }

        if (_state.PendingDecision is not null)
        {
            throw new CommandRefusedException("Resolve the pending decision before ending the turn.");
        }

        var player = _state.CurrentPlayer.Id;
        _events.Publish(new TurnEnded(player, _state.Date));
        if (_state.IsSimultaneous)
        {
            // A person's turn of a simultaneous day: the others go on; once all have ended theirs, the computer players play in order.
            _state.OpenTurnList.Remove(player);
            _state.DoneToday.Add(player);
            if (_state.IsSimultaneous)
            {
                PointAtOpenTurn();
                return;
            }

            _state.CurrentPlayerIndex = -1;
        }

        PassTurn();
    }

    /// <summary>
    /// Today is played turn by turn from its start, and every day after it (<see cref="Commands.PlayTurnByTurnCommand"/>, MP-2): the first player in
    /// order who has not ended today's turn plays; the people's turns have begun already, so they get no second income.
    /// </summary>
    internal void PlayTurnByTurn(string reason)
    {
        if (!_state.IsSimultaneous)
        {
            throw new CommandRefusedException("Today is not played at once.");
        }

        _state.SimultaneousEnded = true;
        _state.SimultaneousConflict = null;
        _state.OpenTurnList.Clear(); // who has ended today's turn keeps it ended
        _events.Publish(new SimultaneousDaysEnded(_state.Date, reason));
        _state.CurrentPlayerIndex = -1;
        PassTurn();
    }

    /// <summary>
    /// Between commands of a simultaneous day the current player is the host's colour while its turn is open, else the first open turn
    /// (<see cref="GameOptions.HostPlayer"/>); a command runs as its own player's (the session points at it first).
    /// </summary>
    internal void PointAtOpenTurn()
    {
        if (!_state.IsSimultaneous)
        {
            return;
        }

        var anchor = _state.Options.HostPlayer is { } host && _state.OpenTurns.Contains(host) ? host : _state.OpenTurns[0];
        _state.CurrentPlayerIndex = _state.IndexOf(anchor);
    }

    /// <summary>
    /// <paramref name="player"/>'s turn leaves today's open turns (the computer took it over, ADR-195, or it is out): a computer plays it after the
    /// people; once no turn is open, the computer players play in order.
    /// </summary>
    internal void LeaveOpenTurns(PlayerId player)
    {
        if (!_state.OpenTurnList.Remove(player))
        {
            return;
        }

        if (_state.IsSimultaneous)
        {
            PointAtOpenTurn();
            return;
        }

        _state.CurrentPlayerIndex = -1;
        PassTurn();
    }

    /// <summary>The next player in order who has not played today; a new day once everyone has.</summary>
    private void PassTurn()
    {
        // Defeated players have no turn (ADR-057); the bound only matters if everyone is out, which ends the game first.
        var tries = 0;
        do
        {
            if (AdvancePlayer())
            {
                return; // a simultaneous day opened everyone's turns
            }
        }
        while (_state.Arena is null && (_state.CurrentPlayer.IsDefeated || _state.DoneToday.Contains(_state.CurrentPlayer.Id)) && ++tries <= 2 * _state.Players.Count);

        // An arena map's day 2 is its battle, which ends the game: nobody's turn starts (ADR-097).
        if (_state.Arena is null)
        {
            BeginTurn();
        }
    }

    /// <summary>The next player; past the last, a new day. True when the new day is simultaneous and has opened its turns.</summary>
    private bool AdvancePlayer()
    {
        _state.CurrentPlayerIndex++;
        if (_state.CurrentPlayerIndex >= _state.Players.Count)
        {
            _state.CurrentPlayerIndex = 0;
            _state.Date = _state.Date.Next();
            _state.DoneToday.Clear();
            _state.BegunToday.Clear();
            _events.Publish(new DayStarted(_state.Date));
            if (_state.Rules.Arena && _state.Date.AbsoluteDay == ArenaRules.BattleDay)
            {
                _arena.StartBattle();
                return false;
            }

            Each(p => p.StartDay());
            if (_state.Date.IsFirstDayOfWeek)
            {
                _events.Publish(new WeekStarted(_state.Date));
                Each(p => p.StartWeek());
                StartWeekEvent();
                Each(p => p.StockWeek());
            }

            Each(p => p.OpenDay());
            return OpenSimultaneousDay();
        }

        return false;
    }

    /// <summary>
    /// Opens today's turns at once when today is simultaneous (MP-2): every person still in the game, in order, each turn beginning as usual. False
    /// on a day played turn by turn.
    /// </summary>
    private bool OpenSimultaneousDay()
    {
        if (!IsSimultaneousDay())
        {
            return false;
        }

        var people = _state.Players.Where(p => p.IsHuman && !p.IsDefeated).Select(p => p.Id).ToList();
        _state.OpenTurnList.AddRange(people);
        foreach (var person in people)
        {
            _state.CurrentPlayerIndex = _state.IndexOf(person);
            BeginTurn();
        }

        PointAtOpenTurn();
        OpenedSimultaneousDay = true;
        _events.Publish(new SimultaneousDayStarted(_state.Date, people));
        return true;
    }

    /// <summary>
    /// Whether today is played at once: the option is on, no conflict has ended it, it is the first week, the map is no arena, and at least two
    /// people still play, each with a start zone (MP-2 a).
    /// </summary>
    private bool IsSimultaneousDay()
    {
        if (!_state.Options.SimultaneousFirstWeek || _state.SimultaneousEnded || _state.Rules.Arena || _state.Date.AbsoluteDay > GameDate.DaysPerWeek)
        {
            return false;
        }

        var people = _state.Players.Where(p => p.IsHuman && !p.IsDefeated).Select(p => p.Id).ToList();
        var zones = _state.Map.StartZoneOwners;
        return people.Count >= 2 && people.All(zones.Contains);
    }

    /// <summary>
    /// The week's event is the one rolled a week ago, and the next one is rolled now (so the Astrologer's Tower knows it, MAP-32). The announcement
    /// comes before the growth it changes; a month of a creature puts its stacks on the map.
    /// </summary>
    private void StartWeekEvent()
    {
        _state.WeekEvent = _state.NextWeekEvent;
        _state.NextWeekEvent = Roll(new GameDate(_state.Date.AbsoluteDay + GameDate.DaysPerWeek));
        if (_state.WeekEvent.Kind == WeekEventKind.None)
        {
            return;
        }

        _events.Publish(new WeekEventAnnounced(_state.Date, _state.WeekEvent));
        Each(p => p.ApplyWeekEvent());
    }

    private WeekEvent Roll(GameDate weekStart) => WeekEventRules.Roll(_state.Rules.WeekEvents, _state.Creatures, weekStart, _state.Random);

    private void BeginTurn()
    {
        var player = _state.CurrentPlayer.Id;
        if (!_state.BegunToday.Add(player))
        {
            _events.Publish(new TurnStarted(player, _state.Date)); // begun already today (a day played again turn by turn): no second income
            return;
        }

        Each(p => p.PrepareTurn(player));
        _events.Publish(new TurnStarted(player, _state.Date));
        Each(p => p.OpenTurn(player));
    }

    private void Each(Action<ITurnPhaseParticipant> hook)
    {
        foreach (var participant in _participants)
        {
            hook(participant);
        }
    }
}
