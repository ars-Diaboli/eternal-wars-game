using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// Stewardship and governors (ADR-063, draft numbers). A hero with Stewardship can be appointed governor of one town; a
/// hero governs at most one town. Stewardship bonuses apply only to a town with a governor, at the governor's level:
/// buildings 5 / 10 / 15 % cheaper, creatures 10 / 15 / 20 % cheaper. A visiting hero with the skill becomes governor of
/// a town that has none, by itself.
/// </summary>
public static class StewardshipRules
{
    public static int BuildingDiscountPercent(SkillLevel level) => 5 * (int)level;

    public static int CreatureDiscountPercent(SkillLevel level) => level == SkillLevel.None ? 0 : 5 + (5 * (int)level);

    /// <summary>The town's governor: the appointed hero if it is still around, still the owner's and still has the skill.</summary>
    public static Hero? GovernorOf(GameState state, Town town) =>
        town.Governor is { } id && town.Owner is { } owner
        && state.Heroes.FirstOrDefault(h => h.Id == id) is { } hero
        && hero.Owner == owner && hero.SkillLevelOf(BuiltInSkills.Stewardship) > SkillLevel.None
            ? hero
            : null;

    /// <summary>The town governed by the hero, if any.</summary>
    public static Town? TownGovernedBy(GameState state, Hero hero) =>
        state.ObjectsOf<Town>().FirstOrDefault(t => t.Governor == hero.Id && GovernorOf(state, t) == hero);

    public static SkillLevel LevelIn(GameState state, Town town) =>
        GovernorOf(state, town)?.SkillLevelOf(BuiltInSkills.Stewardship) ?? SkillLevel.None;

    public static ResourceSet DiscountBuilding(GameState state, Town town, ResourceSet cost) =>
        Discount(cost, BuildingDiscountPercent(LevelIn(state, town)));

    public static ResourceSet DiscountCreatures(GameState state, Town town, ResourceSet cost) =>
        Discount(cost, CreatureDiscountPercent(LevelIn(state, town)));

    /// <summary>Creature costs for a hero recruiting at a map dwelling (ADR-054): its own Stewardship level counts, governed town or not.</summary>
    public static ResourceSet DiscountCreatures(ResourceSet cost, SkillLevel level) => Discount(cost, CreatureDiscountPercent(level));

    /// <summary>Why the hero cannot govern the town, or null.</summary>
    public static string? CheckAppoint(GameState state, Town town, Hero hero)
    {
        if (town.Owner != hero.Owner)
        {
            return $"{hero.Name} cannot govern {town.Name}: it is not the hero's own town.";
        }

        return hero.SkillLevelOf(BuiltInSkills.Stewardship) == SkillLevel.None ? $"{hero.Name} has no Stewardship." : null;
    }

    /// <summary>Every resource reduced by the percentage, rounded up (a discount never makes something free).</summary>
    private static ResourceSet Discount(ResourceSet cost, int percent)
    {
        if (percent <= 0)
        {
            return cost;
        }

        var result = ResourceSet.Empty;
        foreach (var type in Enum.GetValues<ResourceType>())
        {
            result += ResourceSet.Of(type, (cost[type] * (100 - percent) + 99) / 100);
        }

        return result;
    }
}
