using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A player is out of the game (ADR-057): its heroes are gone and its towns and mines are neutral.</summary>
public sealed record PlayerDefeated(PlayerId Player, string Reason) : IEvent;

/// <summary>
/// A player lost its last town (ADR-057). It is eliminated at the start of <see cref="DefeatDay"/> unless it owns a town
/// again before (<see cref="GracePeriodEnded"/>).
/// </summary>
public sealed record GracePeriodStarted(PlayerId Player, GameDate DefeatDay) : IEvent;

/// <summary>A player without towns captured one in time and is no longer counting down.</summary>
public sealed record GracePeriodEnded(PlayerId Player) : IEvent;

/// <summary>The game is over; no command is accepted any more. <see cref="Winners"/> is empty when nobody won.</summary>
public sealed record GameEnded(IReadOnlyList<PlayerId> Winners, string Reason, GameDate Date) : IEvent;

/// <summary>The editor changed the scenario's victory and defeat conditions. Views re-read <c>GameState.Rules</c>.</summary>
public sealed record ScenarioRulesChanged : IEvent;
