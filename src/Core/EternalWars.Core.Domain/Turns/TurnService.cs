using EternalWars.Core.Domain.Common;
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
    TownService towns,
    CaravanService caravans,
    MapObjectInteractionService objects)
{
    internal void StartGame()
    {
        if (state.IsStarted)
        {
            throw new InvalidOperationException("The game has already started.");
        }

        state.IsStarted = true;
        state.CurrentPlayerIndex = 0;
        if (state.Options.Complexity is { } complexity)
        {
            foreach (var player in state.Players)
            {
                economy.Set(player.Id, StartingResources.For(complexity));
            }
        }

        guards.RollUnsetCounts();
        towns.FillNewDwellings();
        economy.StockProviders();
        events.Publish(new GameStarted(state.Date));
        events.Publish(new DayStarted(state.Date));

        // The first week has no growth to change, but a map that starts its events at once still gets its plague or its stacks.
        state.NextWeekEvent = Roll(state.Date);
        StartWeekEvent();
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

        // Defeated players have no turn (ADR-057); the bound only matters if everyone is out, which ends the game first.
        var tries = 0;
        do
        {
            AdvancePlayer();
        }
        while (state.CurrentPlayer.IsDefeated && ++tries <= state.Players.Count);

        BeginTurn();
    }

    private void AdvancePlayer()
    {
        state.CurrentPlayerIndex++;
        if (state.CurrentPlayerIndex >= state.Players.Count)
        {
            state.CurrentPlayerIndex = 0;
            state.Date = state.Date.Next();
            events.Publish(new DayStarted(state.Date));
            guards.RefillBanks();
            if (state.Date.IsFirstDayOfWeek)
            {
                events.Publish(new WeekStarted(state.Date));
                progression.ExpireStatuses(state.Heroes, StatusExpiry.EndOfWeek);
                StartWeekEvent();
                towns.GrowWeekly();
                economy.StockProviders();
                objects.StartWeek(state.Date.IsFirstDayOfMonth);
            }
        }
    }

    /// <summary>
    /// The week's event is the one rolled a week ago, and the next one is rolled now (so the Astrologer's Tower knows it, MAP-32). The announcement
    /// comes before the growth it changes; a month of a creature puts its stacks on the map.
    /// </summary>
    private void StartWeekEvent()
    {
        state.WeekEvent = state.NextWeekEvent;
        state.NextWeekEvent = Roll(new GameDate(state.Date.AbsoluteDay + GameDate.DaysPerWeek));
        if (state.WeekEvent.Kind == WeekEventKind.None)
        {
            return;
        }

        events.Publish(new WeekEventAnnounced(state.Date, state.WeekEvent));
        towns.ApplyPlague();
        guards.SpawnMonthly();
    }

    private WeekEvent Roll(GameDate weekStart) => WeekEventRules.Roll(state.Rules.WeekEvents, state.Creatures, weekStart, state.Random);

    /// <summary>Every own hero regains its daily mana (ADR-063); nothing happens, and nothing is published, when the pool is full.</summary>
    private void RestoreMana(PlayerId player)
    {
        foreach (var hero in state.HeroesOf(player).OrderBy(h => h.Id.Value))
        {
            var max = hero.MaxMana;
            if (hero.Mana < max)
            {
                hero.Mana = Math.Min(max, hero.Mana + ManaRules.DailyMana(hero));
                events.Publish(new HeroManaRestored(hero.Id, hero.Mana, max));
            }
        }
    }

    private void BeginTurn()
    {
        var player = state.CurrentPlayer;
        progression.ExpireStatuses(state.HeroesOf(player.Id), StatusExpiry.EndOfDay);
        movement.RestoreMovement(player.Id);
        RestoreMana(player.Id);
        towns.AppointVisitors(player.Id);
        caravans.Advance(player.Id);
        economy.CollectDailyIncome(player.Id);
        events.Publish(new TurnStarted(player.Id, state.Date));
        progression.OfferNext();
    }
}
