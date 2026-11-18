using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Victory: the hero is gone, and the player is not on its owner's team.</summary>
internal sealed class DefeatHeroHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.DefeatHero;

    public override bool IsVictory => true;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Defeat {HeroOnMapName(state, condition.Hero)}";

    public override bool WonBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Hero is { } hero && state.Tracking.Heroes.TryGetValue(hero, out var known)
        && state.Heroes.All(h => h.Id != hero) && state.AreEnemies(known.Owner, player.Id);

    public override string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName) =>
        $"{player.Name} defeated {heroName ?? HeroOnMapName(state, condition.Hero)}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name)
    {
        if ((condition.Hero is { } heroId ? scenario.Heroes.FirstOrDefault(h => h.Id == heroId) : null) is not { } hero)
        {
            return Missing(condition, "points at a hero that does not exist");
        }

        return condition.Player is { } winner && (winner == hero.Owner || scenario.AreAllies(winner, hero.Owner))
            ? new(IssueSeverity.Warning, $"'{name}': the hero is on the same team.", hero.Position)
            : null;
    }
}
