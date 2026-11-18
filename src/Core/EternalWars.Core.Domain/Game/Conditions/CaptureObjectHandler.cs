using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Victory: the player's team owns the object, one with a flag (mine, dwelling, provider, garrison, town; ADR-073).</summary>
internal sealed class CaptureObjectHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.CaptureObject;

    public override bool IsVictory => true;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Capture {ObjectName(state, condition.Target)}";

    public override bool WonBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Target is { } id && state.FindObject(id) is IFlaggable { Owner: { } owner } && OwnTeam(state, owner, player);

    public override string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName) =>
        $"{player.Name} captured {ObjectName(state, condition.Target)}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name)
    {
        var target = condition.Target is { } targetId ? scenario.FindObject(targetId) : null;
        if (target is not IFlaggable flagged)
        {
            return Missing(condition, "points at no object a player can own (a mine, dwelling, provider, garrison or town)");
        }

        return flagged.Owner is { } owner && OwnedAtStart(scenario, condition, owner)
            ? new(IssueSeverity.Warning, $"'{name}': the object is owned from the start, so the game would end at once.", target.Position)
            : null;
    }
}
