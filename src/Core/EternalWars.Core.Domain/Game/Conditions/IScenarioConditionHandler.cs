using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>
/// What one <see cref="ConditionKind"/> means (ADR-057, ADR-073): whether it is a victory, when it holds for a player, how it reads, and what the
/// validator says about it. The rules every kind shares (whom a condition applies to, deadlines, the order of the checks) are
/// <see cref="GameFlowService"/>'s and <see cref="MapValidator"/>'s; the handler for a kind is <see cref="ScenarioConditionHandlers.For"/>.
/// </summary>
internal interface IScenarioConditionHandler
{
    /// <summary>The kind this handler works.</summary>
    ConditionKind Kind { get; }

    /// <summary>Whether it is a victory condition (else a defeat condition).</summary>
    bool IsVictory { get; }

    /// <summary>Whether a deadline makes sense (ADR-073).</summary>
    bool CanHaveDeadline { get; }

    /// <summary>Whether, as a defeat condition without a player, it applies to every player (else to none).</summary>
    bool DefeatsEveryone { get; }

    /// <summary>The condition in a few words, without its player or deadline.</summary>
    string Describe(GameState state, ScenarioCondition condition);

    /// <summary>Whether the victory condition holds for the player now.</summary>
    bool WonBy(GameState state, ScenarioCondition condition, Player player);

    /// <summary>Whether the defeat condition holds for the player now.</summary>
    bool LostBy(GameState state, ScenarioCondition condition, Player player);

    /// <summary>The name of the hero the end's reason tells of (the defeated or lost hero, the freed prisoner), or null.</summary>
    string? HeroName(GameState state, ScenarioCondition condition);

    /// <summary>What the player achieved: the reason a game ended.</summary>
    string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName);

    /// <summary>Why the defeat condition eliminated its player.</summary>
    string Lost(GameState state, ScenarioCondition condition, string? heroName);

    /// <summary>
    /// The validator's issue with what the condition points at, or null; <paramref name="name"/> is its description. The player and the deadline
    /// are checked before.
    /// </summary>
    MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name);
}
