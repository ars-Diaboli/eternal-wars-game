using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Victory: the calendar reached the day with the player still in the game; it has its own day, so no deadline.</summary>
internal sealed class SurviveUntilDayHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.SurviveUntilDay;

    public override bool IsVictory => true;

    public override bool CanHaveDeadline => false;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Survive until day {Day(condition)}";

    public override bool WonBy(GameState state, ScenarioCondition condition, Player player) => state.Date.AbsoluteDay >= condition.Day;

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name) =>
        condition.Day < 2 ? new(IssueSeverity.Error, "Surviving needs a day after the first.") : null;
}
