using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Towns;

/// <summary>The rows of the Thieves' Guild (ADR-079, TOWN-11), in the order the table shows them.</summary>
public enum ThievesGuildRow
{
    Towns,
    Heroes,
    Gold,

    /// <summary>Lumber and ore together.</summary>
    LumberAndOre,

    /// <summary>Crystal, mercury and sulfur together.</summary>
    RareResources,
    Artifacts,

    /// <summary>Daily gold income.</summary>
    Income,

    /// <summary>The value of every creature in the player's heroes' armies and town garrisons.</summary>
    ArmyStrength,

    /// <summary>The player's highest-level hero.</summary>
    BestHero,

    /// <summary>The most valuable creature the player has in an army or garrison.</summary>
    BestCreature,
}

/// <summary>One player's place in a row: 1 is the best (equal values share a place); <see cref="Detail"/> names the best hero or creature.</summary>
public sealed record ThievesGuildEntry(PlayerId Player, int Place, long Value, string? Detail = null);

/// <summary>
/// The Thieves' Guild (ADR-079, TOWN-11): how every player still in the game, computers and teammates included, ranks by the rows the
/// viewer can see. Like HoMM3 the rows grow with the viewer's taverns; the Den of Thieves map object shows the whole table.
/// </summary>
public static class ThievesGuild
{
    /// <summary>Taverns that show every row.</summary>
    public const int FullTable = 4;

    /// <summary>The rows <paramref name="taverns"/> taverns show: none without a tavern, 2 rows with one, 5 with two, 7 with three, all with four.</summary>
    public static IReadOnlyList<ThievesGuildRow> RowsFor(int taverns) => taverns switch
    {
        <= 0 => [],
        1 => [ThievesGuildRow.Towns, ThievesGuildRow.Heroes],
        2 => [.. RowsFor(1), ThievesGuildRow.Gold, ThievesGuildRow.LumberAndOre, ThievesGuildRow.RareResources],
        3 => [.. RowsFor(2), ThievesGuildRow.Artifacts, ThievesGuildRow.Income],
        _ => Enum.GetValues<ThievesGuildRow>(),
    };

    /// <summary>How many of the player's towns have a tavern.</summary>
    public static int Taverns(GameState state, PlayerId player) =>
        state.ObjectsOf<Town>().Count(t => t.Owner == player && t.Level(BuiltInBuildings.Tavern) > 0);

    /// <summary>The table for <paramref name="taverns"/> taverns; null = the whole table (the Den of Thieves).</summary>
    public static IReadOnlyList<(ThievesGuildRow Row, IReadOnlyList<ThievesGuildEntry> Entries)> Table(GameState state, int? taverns = null) =>
        RowsFor(taverns ?? FullTable).Select(row => (row, Rank(state, row))).ToList();

    /// <summary>Every player still in the game, best first, ties by slot.</summary>
    public static IReadOnlyList<ThievesGuildEntry> Rank(GameState state, ThievesGuildRow row)
    {
        var values = state.Players.Where(p => !p.IsDefeated).Select(p => (p.Id, Value: Value(state, row, p.Id, out var detail), Detail: detail))
            .OrderByDescending(v => v.Value).ThenBy(v => v.Id.Value).ToList();
        return values.Select(v => new ThievesGuildEntry(v.Id, 1 + values.Count(o => o.Value > v.Value), v.Value, v.Detail)).ToList();
    }

    private static long Value(GameState state, ThievesGuildRow row, PlayerId player, out string? detail)
    {
        detail = null;
        var resources = state.GetPlayer(player).Resources;
        var heroes = state.HeroesOf(player).ToList();
        switch (row)
        {
            case ThievesGuildRow.Towns:
                return state.ObjectsOf<Town>().Count(t => t.Owner == player);
            case ThievesGuildRow.Heroes:
                return heroes.Count;
            case ThievesGuildRow.Gold:
                return resources.Gold;
            case ThievesGuildRow.LumberAndOre:
                return resources.Lumber + resources.Ore;
            case ThievesGuildRow.RareResources:
                return resources.Crystal + resources.Mercury + resources.Sulfur;
            case ThievesGuildRow.Artifacts:
                return heroes.Sum(h => h.Artifacts.Backpack.Count + h.Artifacts.AllWorn.Count());
            case ThievesGuildRow.Income:
                return EconomyService.DefaultIncomeSources.Aggregate(ResourceSet.Empty, (total, source) => total + source.DailyIncome(state, player)).Gold;
            case ThievesGuildRow.ArmyStrength:
                return Stacks(state, player).Sum(s => (long)s.Count * state.Creatures.Get(s.Creature).Value);
            case ThievesGuildRow.BestHero:
                var best = heroes.OrderByDescending(h => h.Level).ThenByDescending(h => h.Experience).ThenBy(h => h.Id.Value).FirstOrDefault();
                detail = best is null ? null : $"{best.Name} (level {best.Level})";
                return best?.Level ?? 0;
            default:
                var creature = Stacks(state, player).Select(s => state.Creatures.Get(s.Creature)).OrderByDescending(c => c.Value).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                detail = creature?.Name;
                return creature?.Value ?? 0;
        }
    }

    private static IEnumerable<Armies.CreatureStack> Stacks(GameState state, PlayerId player) =>
        state.HeroesOf(player).SelectMany(h => h.Army.Stacks.Select(s => s.Stack))
            .Concat(state.ObjectsOf<Town>().Where(t => t.Owner == player).SelectMany(t => t.Garrison.Stacks.Select(s => s.Stack)));
}
