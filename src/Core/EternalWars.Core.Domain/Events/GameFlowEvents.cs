using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A player is out of the game (ADR-057): its heroes are gone and its towns and mines are neutral.</summary>
[SeenBy(Audience.Everyone)]
public sealed record PlayerDefeated(PlayerId Player, string Reason) : IEvent;

/// <summary>
/// A player lost its last town (ADR-057). It is eliminated at the start of <see cref="DefeatDay"/> unless it owns a town
/// again before (<see cref="GracePeriodEnded"/>).
/// </summary>
[SeenBy(Audience.Everyone)]
public sealed record GracePeriodStarted(PlayerId Player, GameDate DefeatDay) : IEvent;

/// <summary>A player without towns captured one in time and is no longer counting down.</summary>
[SeenBy(Audience.Everyone)]
public sealed record GracePeriodEnded(PlayerId Player) : IEvent;

/// <summary>The computer plays <see cref="Player"/> from now on (ADR-195): a LAN player left and the host handed the colour over.</summary>
[SeenBy(Audience.Everyone)]
public sealed record PlayerHandedToComputer(PlayerId Player) : IEvent;

/// <summary>A simultaneous day began (MP-2, ADR-197): <paramref name="Players"/>, every person in the game, play it at once.</summary>
[SeenBy(Audience.Everyone)]
public sealed record SimultaneousDayStarted(GameDate Date, IReadOnlyList<PlayerId> Players) : IEvent;

/// <summary>
/// Something one person did on a simultaneous day concerns another (<paramref name="Reason"/>, MP-2): the day stops, and the LAN host plays it again
/// turn by turn.
/// </summary>
[SeenBy(Audience.Everyone)]
public sealed record SimultaneousDayConflicted(string Reason) : IEvent;

/// <summary>From today on, every day is played turn by turn (<see cref="Commands.PlayTurnByTurnCommand"/>; <paramref name="Reason"/>, the conflict).</summary>
[SeenBy(Audience.Everyone)]
public sealed record SimultaneousDaysEnded(GameDate Date, string Reason) : IEvent;

/// <summary>
/// Day 2 of an arena map began (ADR-097, ADR-100): each side's champion was chosen and moved to <see cref="CombatPlace"/> (null on a map without
/// one: the heroes stay where they are). Published after <see cref="DayStarted"/> and the heroes' <c>HeroTeleported</c> or
/// <c>HeroLeftGarrison</c>, before the battle's <c>DecisionRequested</c> and <c>BattleStarted</c>; no turn starts that day. A side without an
/// army loses without a battle, and when nobody can fight no event comes: the game just ends (<see cref="GameEnded"/>).
/// </summary>
[SeenBy(Audience.Witnesses)]
public sealed record ArenaChampionsChosen(MapObjectId? CombatPlace, PlayerId Attacker, HeroId AttackerHero, PlayerId Defender, HeroId DefenderHero) : IEvent;

/// <summary>The game is over; no command is accepted any more. <see cref="Winners"/> is empty when nobody won.</summary>
[SeenBy(Audience.Everyone)]
public sealed record GameEnded(IReadOnlyList<PlayerId> Winners, string Reason, GameDate Date) : IEvent;

/// <summary>
/// An accepted command was applied, logged and settled (victory and defeat checked): the state is consistent with
/// <c>GameSession.CommandLog</c> now, which is when to save it (autosave, ADR-058). Events published while the command ran,
/// such as <see cref="TurnStarted"/>, come before it.
/// </summary>
[SeenBy(Audience.Special)]
public sealed record CommandExecuted(Commands.GameCommand Command) : IEvent;

/// <summary>The editor changed the scenario's victory and defeat conditions. Views re-read <c>GameState.Rules</c>.</summary>
[SeenBy(Audience.Nobody)]
public sealed record ScenarioRulesChanged : IEvent;
