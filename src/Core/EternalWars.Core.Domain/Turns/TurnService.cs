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
        BeginTurn();
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

        _events.Publish(new TurnEnded(_state.CurrentPlayer.Id, _state.Date));

        // Defeated players have no turn (ADR-057); the bound only matters if everyone is out, which ends the game first.
        var tries = 0;
        do
        {
            AdvancePlayer();
        }
        while (_state.Arena is null && _state.CurrentPlayer.IsDefeated && ++tries <= _state.Players.Count);

        // An arena map's day 2 is its battle, which ends the game: nobody's turn starts (ADR-097).
        if (_state.Arena is null)
        {
            BeginTurn();
        }
    }

    private void AdvancePlayer()
    {
        _state.CurrentPlayerIndex++;
        if (_state.CurrentPlayerIndex >= _state.Players.Count)
        {
            _state.CurrentPlayerIndex = 0;
            _state.Date = _state.Date.Next();
            _events.Publish(new DayStarted(_state.Date));
            if (_state.Rules.Arena && _state.Date.AbsoluteDay == ArenaRules.BattleDay)
            {
                _arena.StartBattle();
                return;
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
        }
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
