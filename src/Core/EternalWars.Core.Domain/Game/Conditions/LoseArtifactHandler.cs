using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Defeat: no hero of the player carries the artifact any more (ADR-073).</summary>
internal sealed class LoseArtifactHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.LoseArtifact;

    public override bool IsVictory => false;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Lose {ArtifactName(state, condition.Artifact)}";

    public override bool LostBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Artifact is { } artifact && !state.AllHeroesOf(player.Id).Any(h => h.Artifacts.Has(artifact));

    public override string Lost(GameState state, ScenarioCondition condition, string? heroName) => $"Lost {ArtifactName(state, condition.Artifact)}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name)
    {
        if (condition.Artifact is not { } artifact || !scenario.Artifacts.Contains(artifact))
        {
            return Missing(condition, "names an unknown artifact");
        }

        if (condition.Player is not { } loser)
        {
            return NeedsLoser(name);
        }

        return !scenario.HeroesOf(loser).Any(h => h.Artifacts.Has(artifact))
            ? new(IssueSeverity.Warning, $"'{name}': no hero of the player carries it at the start, so it would lose at once.")
            : null;
    }
}
