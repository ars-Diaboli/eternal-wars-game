using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// A rule that does something when the calendar moves (<see cref="TurnService"/>). Every hook does nothing unless the participant overrides it. The
/// turn service calls each hook on its participants in the order of their list, which the session builds; that order is part of the rules, since
/// it fixes the order of the events and of the draws from <see cref="Game.GameState.Random"/>.
/// <para>
/// Game start: <see cref="SetUpGame"/>, <see cref="StockFirstWeek"/>, then <c>GameStarted</c> and <c>DayStarted</c>, the first week's event
/// (<see cref="ApplyWeekEvent"/>), <see cref="OpenDay"/> and the first turn. A new day: <c>DayStarted</c>, <see cref="StartDay"/>; on a week's first
/// day <c>WeekStarted</c>, <see cref="StartWeek"/>, the week's event (<see cref="ApplyWeekEvent"/>) and <see cref="StockWeek"/>; then
/// <see cref="OpenDay"/>. A turn: <see cref="PrepareTurn"/>, <c>TurnStarted</c>, <see cref="OpenTurn"/>.
/// </para>
/// </summary>
internal interface ITurnPhaseParticipant
{
    /// <summary>Game setup, before <c>GameStarted</c>: the values the map left open (no events but the starting resources).</summary>
    void SetUpGame()
    {
    }

    /// <summary>Game setup, after <see cref="SetUpGame"/>: what the first week brings.</summary>
    void StockFirstWeek()
    {
    }

    /// <summary>A new day (after the first), right after <c>DayStarted</c>.</summary>
    void StartDay()
    {
    }

    /// <summary>A week's first day (after the first week), right after <c>WeekStarted</c> and before the week's event.</summary>
    void StartWeek()
    {
    }

    /// <summary>The week's event was announced (<c>WeekEventAnnounced</c>; not for a week without one), the first week's too.</summary>
    void ApplyWeekEvent()
    {
    }

    /// <summary>A week's first day (after the first week), after the week's event: the week's growth and stock.</summary>
    void StockWeek()
    {
    }

    /// <summary>Every day, the first too, once the calendar has moved and before the first player's turn.</summary>
    void OpenDay()
    {
    }

    /// <summary>A player's turn begins, before <c>TurnStarted</c>.</summary>
    void PrepareTurn(PlayerId player)
    {
    }

    /// <summary>A player's turn has begun, after <c>TurnStarted</c>.</summary>
    void OpenTurn(PlayerId player)
    {
    }
}
