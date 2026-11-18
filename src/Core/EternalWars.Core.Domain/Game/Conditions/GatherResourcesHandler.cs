using System.Globalization;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Victory: the player holds at least the amount of the resource.</summary>
internal sealed class GatherResourcesHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.GatherResources;

    public override bool IsVictory => true;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Gather {Amount(condition)}";

    public override bool WonBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Amount > 0 && player.Resources[condition.Resource] >= condition.Amount;

    public override string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName) =>
        $"{player.Name} gathered {Amount(condition)}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name) =>
        condition.Amount < 1 ? new(IssueSeverity.Error, "A resource condition needs an amount above zero.") : null;

    private static string Amount(ScenarioCondition condition) =>
        $"{condition.Amount.ToString(CultureInfo.InvariantCulture)} {condition.Resource.ToString().ToLowerInvariant()}";
}
