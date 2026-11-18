using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Victory: the player's team owns the town.</summary>
internal sealed class CaptureTownHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.CaptureTown;

    public override bool IsVictory => true;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Capture {TownName(state, condition.Town)}";

    public override bool WonBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Town is { } id && state.FindObject(id) is Town { Owner: { } owner } && OwnTeam(state, owner, player);

    public override string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName) =>
        $"{player.Name} captured {TownName(state, condition.Town)}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name)
    {
        if ((condition.Town is { } townId ? scenario.FindObject(townId) as Town : null) is not { } town)
        {
            return Missing(condition, "points at a town that does not exist");
        }

        return town.Owner is { } owner && OwnedAtStart(scenario, condition, owner)
            ? new(IssueSeverity.Warning, $"'{name}': the town is owned from the start, so the game would end at once.", town.Position)
            : null;
    }
}
