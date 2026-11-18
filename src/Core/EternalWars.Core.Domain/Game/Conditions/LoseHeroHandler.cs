using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>Defeat: the hero is gone.</summary>
internal sealed class LoseHeroHandler : ScenarioConditionHandler
{
    public override ConditionKind Kind => ConditionKind.LoseHero;

    public override bool IsVictory => false;

    public override string Describe(GameState state, ScenarioCondition condition) => $"Lose {HeroOnMapName(state, condition.Hero)}";

    public override bool LostBy(GameState state, ScenarioCondition condition, Player player) =>
        condition.Hero is { } hero && state.Tracking.Heroes.ContainsKey(hero) && state.Heroes.All(h => h.Id != hero);

    public override string Lost(GameState state, ScenarioCondition condition, string? heroName) =>
        $"Lost {heroName ?? HeroOnMapName(state, condition.Hero)}.";

    public override MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name)
    {
        if ((condition.Hero is { } heroId ? scenario.Heroes.FirstOrDefault(h => h.Id == heroId) : null) is null)
        {
            return Missing(condition, "points at a hero that does not exist");
        }

        return condition.Player is null ? NeedsLoser(name) : null;
    }
}
