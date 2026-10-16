using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// HoMM-style sequential turns: every player acts once per day, in slot order.
/// When the last player ends the turn the calendar advances; a new week brings the towns' creature growth.
/// </summary>
public sealed class TurnService(
    GameState state,
    IEventPublisher events,
    EconomyService economy,
    HeroMovementService movement,
    GuardService guards,
    ProgressionService progression,
    TownService towns)
{
    internal void StartGame()
    {
        if (state.IsStarted)
        {
            throw new InvalidOperationException("The game has already started.");
        }

        state.IsStarted = true;
        state.CurrentPlayerIndex = 0;
        guards.RollUnsetCounts();
        events.Publish(new GameStarted(state.Date));
        events.Publish(new DayStarted(state.Date));
        BeginTurn();
    }

    internal void EndTurn()
    {
        if (!state.IsStarted)
        {
            throw new InvalidOperationException("The game has not started yet.");
        }

        if (state.PendingDecision is not null)
        {
            throw new InvalidOperationException("Resolve the pending decision before ending the turn.");
        }

        events.Publish(new TurnEnded(state.CurrentPlayer.Id, state.Date));

        state.CurrentPlayerIndex++;
        if (state.CurrentPlayerIndex >= state.Players.Count)
        {
            state.CurrentPlayerIndex = 0;
            state.Date = state.Date.Next();
            events.Publish(new DayStarted(state.Date));
            if (state.Date.IsFirstDayOfWeek)
            {
                events.Publish(new WeekStarted(state.Date));
                towns.GrowWeekly();
            }
        }

        BeginTurn();
    }

    private void BeginTurn()
    {
        var player = state.CurrentPlayer;
        movement.RestoreMovement(player.Id);
        economy.CollectDailyIncome(player.Id);
        events.Publish(new TurnStarted(player.Id, state.Date));
        progression.OfferNext();
    }
}
