using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Victory: a hero of the player's team carries the artifact, worn or in the backpack (ADR-073).</summary>
internal sealed class ObtainArtifactHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.ObtainArtifact;

    public override bool IsVictory => true;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Obtain {ArtifactName(state, condition.Artifact)}";

    public override bool WonBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Artifact is { } artifact && state.Heroes.Any(h => h.Artifacts.Has(artifact) && OwnTeam(state, h.Owner, player));

    public override string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName) =>
        $"{player.Name} obtained {ArtifactName(state, condition.Artifact)}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name)
    {
        if (condition.Artifact is not { } artifact || !scenario.Artifacts.Contains(artifact))
        {
            return Missing(condition, "names an unknown artifact");
        }

        return scenario.Heroes.Any(h => h.Artifacts.Has(artifact) && OwnedAtStart(scenario, condition, h.Owner))
            ? new(IssueSeverity.Warning, $"'{name}': a hero carries it from the start, so the game would end at once.")
            : null;
    }
}
