using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>
/// Victory: the player's team freed the hero held in the prison (ADR-073); the game remembers who sits there, as freeing the hero removes the
/// prison.
/// </summary>
internal sealed class ReleaseHeroHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.ReleaseHero;

    public override bool IsVictory => true;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Release {PrisonerName(state, condition.Target)} from prison";

    public override bool WonBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Target is { } prison && state.FindObject(prison) is null && state.Tracking.Prisoners.TryGetValue(prison, out var prisoner)
        && state.Heroes.Any(h => h.Definition == prisoner && OwnTeam(state, h.Owner, player));

    /// <summary>The freed prisoner, as the game remembered it.</summary>
    public override string? HeroName(GameState state, ScenarioCondition condition) =>
        condition.Target is { } id && state.Tracking.Prisoners.TryGetValue(id, out var hero) && state.HeroRoster.Contains(hero) ? state.HeroRoster.Get(hero).Name : null;

    public override string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName) =>
        $"{player.Name} released {heroName ?? "the prisoner"}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name) =>
        (condition.Target is { } targetId ? scenario.FindObject(targetId) : null) is not Prison ? Missing(condition, "points at a prison that does not exist") : null;
}
