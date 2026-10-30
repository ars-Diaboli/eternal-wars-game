using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Economy;

/// <summary>Where a row of the kingdom overview comes from.</summary>
public enum IncomeSourceKind
{
    Town,
    Mine,
    Provider,
    Hero,
}

/// <summary>
/// One source of the player's income (ADR-077): what it pays a day or, for a flagged resource provider, what a week brings (in
/// <see cref="Detail"/>, since it is rolled) and what waits there.
/// </summary>
/// <param name="Kind">A town, mine, provider or hero.</param>
/// <param name="Name">The town, mine or provider, or the hero.</param>
/// <param name="Detail">What pays: the town's paying buildings, the mine's place, the provider's week, or the hero's "Treasury", "Prospecting", "Specialty", "Artifacts".</param>
/// <param name="Daily">What it pays every day (nothing for a provider).</param>
/// <param name="Stored">What waits at a provider.</param>
/// <param name="Object">The town, mine or provider.</param>
/// <param name="Hero">The hero.</param>
public sealed record IncomeRow(
    IncomeSourceKind Kind,
    string Name,
    string Detail,
    ResourceSet Daily,
    ResourceSet Stored = default,
    MapObjectId? Object = null,
    HeroId? Hero = null);

/// <summary>The player's whole economy: every row and the totals.</summary>
/// <param name="Rows">Towns, mines, providers, then the heroes' sources.</param>
/// <param name="DailyTotal">What the player's next turn pays; equals <see cref="EconomyService.DailyIncome"/> with the default sources.</param>
/// <param name="StoredTotal">What waits at the player's flagged providers.</param>
public sealed record KingdomOverview(IReadOnlyList<IncomeRow> Rows, ResourceSet DailyTotal, ResourceSet StoredTotal);

/// <summary>
/// The kingdom overview (ADR-077, ADR-084): reads the income rules the turn pays by (<see cref="TownRules.DailyIncome"/>, the mines' yield,
/// <see cref="HeroIncome"/>, the providers' <see cref="ResourceProviderRules"/>), so it always matches.
/// </summary>
public static class KingdomOverviews
{
    public static KingdomOverview For(GameState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rows = new List<IncomeRow>();
        foreach (var town in state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value))
        {
            rows.Add(new IncomeRow(IncomeSourceKind.Town, town.Name, TownDetail(state, town), TownRules.DailyIncome(state, town), Object: town.Id));
        }

        foreach (var mine in state.ObjectsOf<Mine>().Where(m => m.Owner == player).OrderBy(m => m.Id.Value))
        {
            rows.Add(new IncomeRow(IncomeSourceKind.Mine, mine.Definition.Name, $"at {mine.Position.X}, {mine.Position.Y}",
                ResourceSet.Of(mine.Resource, mine.DailyYield), Object: mine.Id));
        }

        foreach (var provider in state.ObjectsOf<ResourceProvider>().Where(p => p.Owner == player).OrderBy(p => p.Id.Value))
        {
            rows.Add(new IncomeRow(IncomeSourceKind.Provider, provider.Definition.Name, $"{ResourceProviderRules.DescribeWeek(provider.Definition, state.Date)} a week",
                ResourceSet.Empty, provider.Stored, provider.Id));
        }

        var gold = state.GetPlayer(player).Resources.Gold;
        foreach (var hero in state.AllHeroesOf(player).OrderBy(h => h.Id.Value))
        {
            var treasury = new ResourceSet(Gold: SkillEffects.TreasuryGold(hero.SkillLevelOf(BuiltInSkills.Treasury))) + HeroIncome.TreasuryInterest(hero, gold);
            Add(hero, "Treasury", treasury);
            Add(hero, "Prospecting", SkillEffects.Prospecting(hero.SkillLevelOf(BuiltInSkills.Prospecting)));
            Add(hero, "Specialty", hero.Specialty?.DailyIncome ?? ResourceSet.Empty);
            Add(hero, "Artifacts", hero.Bonus.Income);
        }

        return new KingdomOverview(
            rows,
            rows.Aggregate(ResourceSet.Empty, (total, row) => total + row.Daily),
            rows.Aggregate(ResourceSet.Empty, (total, row) => total + row.Stored));

        void Add(Hero hero, string detail, ResourceSet daily)
        {
            if (daily != ResourceSet.Empty)
            {
                rows.Add(new IncomeRow(IncomeSourceKind.Hero, hero.Name, detail, daily, Hero: hero.Id));
            }
        }
    }

    /// <summary>The town's buildings that pay: its town hall and, when built, the resource silo.</summary>
    private static string TownDetail(GameState state, Town town)
    {
        var buildings = TownRules.Buildings(state, town);
        return string.Join(", ", town.Buildings
            .Where(b => b.Value > 0 && buildings.Contains(b.Key))
            .Select(b => buildings.Get(b.Key, Math.Min(b.Value, buildings.MaxLevel(b.Key))))
            .Where(level => level.Income != ResourceSet.Empty)
            .OrderBy(level => level.Building, StringComparer.Ordinal)
            .Select(level => level.Name));
    }
}
