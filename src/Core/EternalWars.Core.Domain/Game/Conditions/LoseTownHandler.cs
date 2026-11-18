using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Defeat: the player no longer owns the town.</summary>
internal sealed class LoseTownHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.LoseTown;

    public override bool IsVictory => false;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Lose {TownName(state, condition.Town)}";

    public override bool LostBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Town is { } id && state.FindObject(id) is Town town && town.Owner != player.Id;

    public override string Lost(GameState state, ScenarioCondition condition, string? heroName) => $"Lost {TownName(state, condition.Town)}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name)
    {
        if ((condition.Town is { } townId ? scenario.FindObject(townId) as Town : null) is not { } town)
        {
            return Missing(condition, "points at a town that does not exist");
        }

        if (condition.Player is null)
        {
            return NeedsLoser(name);
        }

        return town.Owner != condition.Player
            ? new(IssueSeverity.Warning, $"'{name}': the player does not own the town at the start, so it would lose at once.", town.Position)
            : null;
    }
}
