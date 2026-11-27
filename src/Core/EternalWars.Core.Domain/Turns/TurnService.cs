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
/// and rolls the week's events. The deciding battle's day of an arena or a tournament (ADR-097, ADR-366) replaces the day, so the battle is not a participant.
/// <para>
/// A simultaneous day (MP-2, ADR-197, ADR-366; <see cref="GameOptions.SimultaneousFirstWeek"/>): every person's turn begins at once when the day opens
/// (<see cref="GameState.OpenTurns"/>), each ends its own, and once all have, the computer players play theirs one by one. A turn begins once a
/// day, so a day played again turn by turn (<see cref="PlayTurnByTurn"/>) brings no second income. Which days are played at once and the open
/// turns are <see cref="SimultaneousDayCoordinator"/>'s (ADR-217); this service begins and passes the turns.
/// </para>
/// </summary>
public sealed class TurnService
{
    private readonly GameState _state;
    private readonly IEventPublisher _events;
    private readonly DecidingBattleService _decidingBattle;
    private readonly SimultaneousDayCoordinator _days;
    private readonly IReadOnlyList<ITurnPhaseParticipant> _participants;

    internal TurnService(
        GameState state, IEventPublisher events, DecidingBattleService decidingBattle, SimultaneousDayCoordinator days, IReadOnlyList<ITurnPhaseParticipant> participants)
    {
        _state = state;
        _events = events;
        _decidingBattle = decidingBattle;
        _days = days;
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
        if (!_days.TryOpen(BeginTurn))
        {
            BeginTurn();
        }
    }

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

        // The turn's end is recorded before TurnEnded tells of it (ADR-214); the next turn, with its own events, follows the event.
        var player = _state.CurrentPlayer.Id;
        var simultaneous = _state.IsSimultaneous;
        if (simultaneous)
        {
            // A person's turn of a simultaneous day: the others go on; once all have ended theirs, the computer players play in order.
            _days.EndTurn(player);
        }

        _events.Publish(new TurnEnded(player, _state.Date));
        if (simultaneous)
        {
            if (_state.IsSimultaneous)
            {
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
        _days.EndDays(reason);
        _state.CurrentPlayerIndex = -1;
        PassTurn();
    }

    /// <summary>
    /// <paramref name="player"/>'s turn leaves today's open turns (the computer took it over, ADR-195, or it is out): a computer plays it after the
    /// people; once no turn is open, the computer players play in order.
    /// </summary>
    internal void LeaveOpenTurns(PlayerId player)
    {
        if (!_days.Leave(player))
        {
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
        while (_state.DecidingBattle is null && (_state.CurrentPlayer.IsDefeated || _days.HasEnded(_state.CurrentPlayer.Id)) && ++tries <= 2 * _state.Players.Count);

        // The deciding battle's day (an arena's day 2, a tournament's day after the last) ends the game: nobody's turn starts (ADR-097, ADR-366).
        if (_state.DecidingBattle is null)
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
            _days.NewDay();
            _state.BegunToday.Clear();
            _events.Publish(new DayStarted(_state.Date));
            if (DecidingBattles.IsDue(_state))
            {
                _decidingBattle.StartBattle();
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
            return _days.TryOpen(BeginTurn);
        }

        return false;
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
