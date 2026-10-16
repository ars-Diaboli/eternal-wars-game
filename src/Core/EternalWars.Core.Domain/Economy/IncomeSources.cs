using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Economy;

/// <summary>
/// One kind of daily income: mines, towns and heroes (skills, specialisations) now; artifacts later. There is no base income: a player earns only what their sources produce.
/// </summary>
public interface IIncomeSource
{
    ResourceSet DailyIncome(GameState state, PlayerId player);
}

/// <summary>Every mine pays its daily yield to its owner.</summary>
public sealed class MineIncome : IIncomeSource
{
    public ResourceSet DailyIncome(GameState state, PlayerId player) =>
        state.ObjectsOf<Mine>()
            .Where(m => m.Owner == player)
            .Aggregate(ResourceSet.Empty, (total, mine) => total + ResourceSet.Of(mine.Resource, mine.DailyYield));
}

/// <summary>Heroes on the map earn for their owner: Treasury, Prospecting, resource specialties (ADR-048) and income artifacts (ADR-061).</summary>
public sealed class HeroIncome : IIncomeSource
{
    public ResourceSet DailyIncome(GameState state, PlayerId player) =>
        state.HeroesOf(player).Aggregate(ResourceSet.Empty, (total, hero) => total + Of(hero));

    public static ResourceSet Of(Hero hero) =>
        new ResourceSet(Gold: SkillEffects.TreasuryGold(hero.SkillLevelOf(BuiltInSkills.Treasury)))
        + SkillEffects.Prospecting(hero.SkillLevelOf(BuiltInSkills.Prospecting))
        + (hero.Specialty?.DailyIncome ?? ResourceSet.Empty)
        + hero.Bonus.Income;
}

/// <summary>Owned towns pay what their buildings produce: town hall gold, the resource silo (ADR-046).</summary>
public sealed class TownIncome : IIncomeSource
{
    public ResourceSet DailyIncome(GameState state, PlayerId player) =>
        state.ObjectsOf<Town>()
            .Where(t => t.Owner == player)
            .Aggregate(ResourceSet.Empty, (total, town) => total + TownRules.DailyIncome(state, town));
}
