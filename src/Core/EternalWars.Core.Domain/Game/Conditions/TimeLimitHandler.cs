using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Defeat: the calendar passed the day without the player winning; without a player it applies to every player.</summary>
internal sealed class TimeLimitHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.TimeLimit;

    public override bool IsVictory => false;

    public override bool DefeatsEveryone => true;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Fail to win by day {Day(condition)}";

    public override bool LostBy(GameState state, ScenarioCondition condition, Player player) => state.Date.AbsoluteDay > condition.Day;

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name) =>
        condition.Day < 1 ? new(IssueSeverity.Error, "A time limit needs a day of 1 or more.") : null;
}
