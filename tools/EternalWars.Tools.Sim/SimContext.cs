using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Tools.Sim;

/// <summary>
/// What every command runs with: the content (the built-in tables, with the creature table of <c>--creatures</c> if given) and the adventure AI's
/// weights (the defaults with the <c>--weight</c> options applied in order).
/// </summary>
internal sealed class SimContext(ContentCatalogs content, AiWeights weights, IReadOnlyList<string> weightOptions)
{
    /// <summary>The towns a player can pick, in the order the balance tables list them.</summary>
    public static readonly string[] PlayableTowns =
        [TownIds.Haven, TownIds.Sylvan, TownIds.Academy, TownIds.Necropolis, TownIds.Inferno, TownIds.Dungeon, TownIds.Stronghold, TownIds.SwampBestiary];

    public ContentCatalogs Content { get; } = content;

    /// <summary>The creatures of <see cref="Content"/>.</summary>
    public CreatureCatalog Catalog => Content.Creatures;

    /// <summary>The adventure AI's weights.</summary>
    public AiWeights Weights { get; } = weights;

    /// <summary>The <c>--weight</c> options given, as Name=value.</summary>
    public IReadOnlyList<string> WeightOptions { get; } = weightOptions;

    /// <summary>A template by built-in name or file key, or a template file.</summary>
    public static MapTemplate LoadTemplate(string template) =>
        File.Exists(template) ? TemplateSerializer.Load(template) : BuiltInTemplates.Find(template)
            ?? throw new FormatException($"No template '{template}' (built-in: {string.Join(", ", BuiltInTemplates.Keys)}).");

    /// <summary>An army from <c>creature:count[,creature:count...]</c>; every creature must be in the catalog.</summary>
    public Army ParseArmy(string spec)
    {
        var stacks = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part =>
        {
            var (creature, count) = part.Split(':') is [var c, var n] && int.TryParse(n, out var parsed)
                ? (c, parsed)
                : throw new FormatException($"'{part}' is not creature:count.");
            Catalog.Get(creature);
            return new CreatureStack(creature, count);
        }).ToArray();
        return stacks.Length is > 0 and <= Army.SlotCount ? Army.Of(stacks) : throw new FormatException("An army has 1 to 7 stacks.");
    }

    /// <summary>The four seven-dwelling builds of a town: either line of tier 3 × either line of tier 4.</summary>
    public List<Army> WeekArmies(string town, bool upgraded)
    {
        var lines = Catalog.BaseCreaturesOf(town).ToList();
        return
        [
            .. from third in lines.Where(c => c.Tier == 3)
               from fourth in lines.Where(c => c.Tier == 4)
               select Army.Of(lines.Where(c => c.Tier is 1 or 2 or 5).Append(third).Append(fourth).OrderBy(c => c.Tier)
                   .Select(c => new CreatureStack(upgraded ? c.UpgradesTo! : c.Id, c.WeeklyGrowth)).ToArray()),
        ];
    }
}
