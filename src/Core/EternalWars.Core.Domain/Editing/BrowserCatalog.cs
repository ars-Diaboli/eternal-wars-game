using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// What the editor's object browser lists (EDIT-1, ADR-253): every kind of map object in its category, with search words, the terrains it
/// suits and a one-line rule. A kind whose instances differ by one choice is listed once per choice: a monster and a dwelling per
/// creature (grouped by faction, in tier order), an artifact pickup per artifact (grouped by slot, in tier order) beside the pickups
/// that roll one of a tier. Scenery is grouped by its kind (trees, rocks…). The hero is an entry too.
/// </summary>
public sealed class BrowserCatalog
{
    private readonly Dictionary<string, BrowserEntry> _byId;

    public BrowserCatalog(MapObjectCatalog objects, CreatureCatalog creatures, ArtifactCatalog artifacts)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(artifacts);
        Entries =
        [
            .. objects.All.SelectMany(d => Of(d, creatures, artifacts)).Append(Hero)
                .OrderBy(e => e.Category).ThenBy(e => e.GroupOrder).ThenBy(e => e.Group, StringComparer.Ordinal)
                .ThenBy(e => e.Order).ThenBy(e => e.Name, StringComparer.Ordinal),
        ];
        _byId = Entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// Every entry: by category, then group (the factions by name with the neutrals last, the slots as a hero wears them, the working
    /// mines before the abandoned ones), then <see cref="BrowserEntry.Order"/>, then name.
    /// </summary>
    public IReadOnlyList<BrowserEntry> Entries { get; }

    private static BrowserEntry Hero { get; } = new(BrowserEntry.HeroId, "Hero", BrowserCategory.Heroes, "", null)
    {
        Placement = BrowserPlacement.Hero,
        Words = ["heroes", "player"],
        Rule = "A hero of the chosen player; its army, skills and artifacts are set in the inspector.",
    };

    /// <summary>The category's name as the browser shows it.</summary>
    public static string NameOf(BrowserCategory category) => category switch
    {
        BrowserCategory.Towns => "Towns",
        BrowserCategory.Heroes => "Heroes",
        BrowserCategory.Monsters => "Monsters",
        BrowserCategory.Mines => "Mines and providers",
        BrowserCategory.Dwellings => "Dwellings",
        BrowserCategory.Treasure => "Treasure",
        BrowserCategory.Artifacts => "Artifacts",
        BrowserCategory.PowerUps => "Power-ups",
        BrowserCategory.Utilities => "Utilities and travel",
        BrowserCategory.Banks => "Banks",
        BrowserCategory.Events => "Events and quests",
        BrowserCategory.Scenery => "Scenery",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>The category the objects of <paramref name="kind"/> are listed in. Every kind has one.</summary>
    public static BrowserCategory CategoryOf(MapObjectKind kind) => kind switch
    {
        MapObjectKind.Town => BrowserCategory.Towns,
        MapObjectKind.Monster => BrowserCategory.Monsters,
        MapObjectKind.Mine or MapObjectKind.ResourceProvider or MapObjectKind.Deposit => BrowserCategory.Mines,
        MapObjectKind.Dwelling or MapObjectKind.RefugeeCamp => BrowserCategory.Dwellings,
        MapObjectKind.ResourcePile or MapObjectKind.TreasureChest or MapObjectKind.Loot => BrowserCategory.Treasure,
        MapObjectKind.Artifact => BrowserCategory.Artifacts,
        MapObjectKind.Buff or MapObjectKind.Training or MapObjectKind.WitchHut or MapObjectKind.Shrine or MapObjectKind.SkillSchool => BrowserCategory.PowerUps,
        MapObjectKind.CreatureBank => BrowserCategory.Banks,
        MapObjectKind.PandorasBox or MapObjectKind.SeerHut or MapObjectKind.QuestGuard or MapObjectKind.QuestGate or MapObjectKind.Sphinx => BrowserCategory.Events,
        MapObjectKind.Scenery => BrowserCategory.Scenery,
        MapObjectKind.TwoWayGate or MapObjectKind.OneWayGateEntrance or MapObjectKind.OneWayGateExit or MapObjectKind.Marketplace
            or MapObjectKind.Sign or MapObjectKind.Observatory or MapObjectKind.HillFort or MapObjectKind.TownGate
            or MapObjectKind.WarMachineFactory or MapObjectKind.Prison or MapObjectKind.DenOfThieves or MapObjectKind.Sanctuary
            or MapObjectKind.Cartographer or MapObjectKind.ArtifactMerchant or MapObjectKind.SacrificialAltar or MapObjectKind.MagiWindow
            or MapObjectKind.MagiEye or MapObjectKind.KeymasterTent or MapObjectKind.BorderPost or MapObjectKind.MapTavern
            or MapObjectKind.Garrison or MapObjectKind.AstrologersTower or MapObjectKind.Obelisk or MapObjectKind.CombatPlace => BrowserCategory.Utilities,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind has no browser category."),
    };

    /// <summary>The entry of that id; null when the browser has none (a favourite of content that is gone).</summary>
    public BrowserEntry? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// What the Object tool's random variant picks among for <paramref name="entry"/> (ADR-286): the entries of its category and group
    /// placed the same way and of the same order (a creature's or an artifact's tier), <paramref name="entry"/> first and the others in the
    /// catalog's order. An entry with no group (a town, the hero, most utilities) has no variant but itself.
    /// </summary>
    public IReadOnlyList<BrowserEntry> VariantsOf(BrowserEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Group.Length == 0
            ? [entry]
            : [entry, .. Entries.Where(e => e != entry && e.Category == entry.Category && e.Group == entry.Group && e.Placement == entry.Placement && e.Order == entry.Order)];
    }

    /// <summary>The entries of <paramref name="category"/>, in the catalog's order.</summary>
    public IEnumerable<BrowserEntry> Of(BrowserCategory category) => Entries.Where(e => e.Category == category);

    /// <summary>
    /// The entries that fit what was typed, the best first: every typed word must start a word of the entry's name or one of its search
    /// words, or stand inside the name. A name that starts with the word comes before one that holds it as a later word, then one that
    /// holds it inside a word, then an entry found by its search words only; entries that fit alike keep the catalog's order. With
    /// <paramref name="terrain"/>, only entries that suit it. Nothing typed: every entry.
    /// </summary>
    public IReadOnlyList<BrowserEntry> Search(string query, TerrainType? terrain = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); // at any white space
        var found = new List<(BrowserEntry Entry, int Score)>();
        foreach (var entry in Entries)
        {
            if (terrain is { } ground && !entry.Suits(ground))
            {
                continue;
            }

            var total = 0;
            foreach (var word in words)
            {
                if (Score(entry, word) is not { } score)
                {
                    total = -1;
                    break;
                }

                total += score;
            }

            if (total >= 0)
            {
                found.Add((entry, total));
            }
        }

        return [.. found.OrderBy(f => f.Score).Select(f => f.Entry)]; // a stable sort: the catalog's order among equals
    }

    private static int? Score(BrowserEntry entry, string word)
    {
        var name = entry.Name;
        var at = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        if (at == 0)
        {
            return 0;
        }

        for (var i = at; i > 0; i = name.IndexOf(word, i + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (!char.IsLetterOrDigit(name[i - 1]))
            {
                return 1;
            }
        }

        return at > 0 ? 2
            : entry.Words.Any(w => w.StartsWith(word, StringComparison.OrdinalIgnoreCase)) ? 3
            : null;
    }

    private static IEnumerable<BrowserEntry> Of(MapObjectDefinition definition, CreatureCatalog creatures, ArtifactCatalog artifacts)
    {
        var category = CategoryOf(definition.Kind);
        switch (definition.Kind)
        {
            case MapObjectKind.Monster:
                foreach (var creature in creatures.All)
                {
                    yield return new($"{definition.Id}/{creature.Id}", creature.Name, category, Faction(creature), definition)
                    {
                        Placement = BrowserPlacement.Monster, Creature = creature.Id, Order = creature.Tier, GroupOrder = creature.Town is null ? 1 : 0,
                        Words = Words(category, Faction(creature), "monster", "creature", "guard", $"tier{creature.Tier}", $"t{creature.Tier}"),
                        Rule = $"Tier {creature.Tier} creature ({Faction(creature)}): a neutral stack that guards the tiles round it.",
                    };
                }

                break;
            case MapObjectKind.Dwelling:
                foreach (var creature in creatures.All.Where(c => !creatures.IsUpgrade(c.Id)))
                {
                    yield return new($"{definition.Id}/{creature.Id}", $"{creature.Name} dwelling", category, Faction(creature), definition)
                    {
                        Placement = BrowserPlacement.Dwelling, Creature = creature.Id, Order = creature.Tier, GroupOrder = creature.Town is null ? 1 : 0,
                        Words = Words(category, Faction(creature), "creature", "recruit", $"tier{creature.Tier}", $"t{creature.Tier}"),
                        Rule = $"Sells {creature.Name} (tier {creature.Tier}) every week to the player who flags it.",
                    };
                }

                break;
            case MapObjectKind.Artifact:
                var tier = definition.ArtifactTier!.Value;
                yield return new(definition.Id, $"Random {Lower(tier)} artifact", category, "Random", definition)
                {
                    Order = (int)tier,
                    Words = Words(category, "random", Lower(tier)),
                    Rule = $"An artifact of the {Lower(tier)} tier, rolled when the game starts.",
                };
                foreach (var artifact in artifacts.All.Where(a => a.Tier == tier && !a.IsGrail))
                {
                    yield return new($"{definition.Id}/{artifact.Id}", artifact.Name, category, artifact.Slot.ToString(), definition)
                    {
                        Placement = BrowserPlacement.Artifact, Artifact = artifact.Id, Order = (int)tier, GroupOrder = 1 + (int)artifact.Slot,
                        Words = Words(category, Lower(artifact.Slot), Lower(tier), artifact.Set ?? "", artifact.IsScroll ? "scroll spell" : ""),
                        Rule = $"{tier} artifact for the {Lower(artifact.Slot)} slot; the hero who steps on it takes it.",
                    };
                }

                break;
            default:
                yield return new(definition.Id, definition.Name, category, GroupOf(definition), definition)
                {
                    Words = Words(category, GroupOf(definition), [.. Extra(definition)]),
                    Rule = RuleOf(definition),
                    GroupOrder = definition.Kind switch
                    {
                        MapObjectKind.ResourceProvider => 1,
                        MapObjectKind.Deposit => 2,
                        MapObjectKind.Mine when definition.Becomes is not null => 3,
                        MapObjectKind.RefugeeCamp => 2, // after the dwellings of the factions and the neutrals
                        _ => 0,
                    },
                };
                break;
        }
    }

    private static string GroupOf(MapObjectDefinition definition) => definition.Kind switch
    {
        MapObjectKind.Scenery => Pretty(SceneryTable.KindOf(definition)),
        MapObjectKind.Mine => definition.Becomes is null ? "Mines" : "Abandoned mines",
        MapObjectKind.Deposit => "Deposits",
        MapObjectKind.ResourceProvider => "Providers",
        _ => "",
    };

    /// <summary>Search words from what the definition says: its kind, its resource, its colour, what a bank pays.</summary>
    private static IEnumerable<string> Extra(MapObjectDefinition definition)
    {
        yield return Spaced(definition.Kind.ToString());
        if (definition.Resource is { } resource)
        {
            yield return Lower(resource);
        }

        if (definition.Colour is { } colour)
        {
            yield return Lower(colour);
        }

        if (definition.Town is { } town)
        {
            yield return town;
            yield return "castle";
        }

        if (definition.Bank is { } bank)
        {
            var tiers = CreatureBankRules.Tables[bank];
            foreach (var paid in Enum.GetValues<ResourceType>().Where(r => tiers.Any(t => t.Reward[r] > 0)))
            {
                yield return Lower(paid);
            }

            if (tiers.Any(t => t.Artifact is not null))
            {
                yield return "artifact";
            }

            yield return "guard fight";
        }

        yield return definition.Kind switch
        {
            MapObjectKind.ResourcePile => "resource pickup",
            MapObjectKind.TreasureChest => "gold experience pickup",
            MapObjectKind.Loot => "pickup resource",
            MapObjectKind.Mine => "resource income",
            MapObjectKind.Deposit => "mine resource",
            MapObjectKind.ResourceProvider => "resource weekly income",
            MapObjectKind.TwoWayGate or MapObjectKind.OneWayGateEntrance or MapObjectKind.OneWayGateExit => "travel teleport monolith",
            MapObjectKind.TownGate => "travel teleport",
            MapObjectKind.Marketplace => "trade resource",
            MapObjectKind.Buff => "bonus morale luck movement mana",
            MapObjectKind.Training => "bonus experience attack defense power knowledge",
            MapObjectKind.WitchHut or MapObjectKind.SkillSchool => "skill learn",
            MapObjectKind.Shrine => "spell learn",
            MapObjectKind.KeymasterTent or MapObjectKind.BorderPost => "key border",
            MapObjectKind.SeerHut or MapObjectKind.QuestGuard or MapObjectKind.QuestGate => "quest",
            MapObjectKind.PandorasBox => "event box",
            MapObjectKind.Sphinx => "riddle",
            MapObjectKind.Obelisk => "grail puzzle",
            MapObjectKind.MapTavern => "hero hire",
            MapObjectKind.Prison => "hero",
            MapObjectKind.Scenery => "obstacle decoration",
            _ => "",
        };
    }

    private static string RuleOf(MapObjectDefinition d) => d.Kind switch
    {
        MapObjectKind.Town => $"A {d.Name} town: built up, garrisoned and owned as its inspector says.",
        MapObjectKind.ResourcePile => $"A pile of {Lower(d.Resource!.Value)}; the hero who steps on it takes it.",
        MapObjectKind.Mine when d.Becomes is not null => $"Guarded by {d.GuardMin}–{d.GuardMax} creatures; beaten, it becomes a mine.",
        MapObjectKind.Mine => $"Gives its owner {d.DailyYield} {Lower(d.Resource!.Value)} a day once a hero flags it.",
        MapObjectKind.Deposit => $"Becomes a {Lower(d.Resource!.Value)} mine for the player who pays its price.",
        MapObjectKind.ResourceProvider => "Stores resources every week for the hero who comes for them.",
        MapObjectKind.TreasureChest => "The hero chooses gold or experience; it may hold an artifact instead.",
        MapObjectKind.Loot => "Loot taken once; what it holds is rolled when the game starts.",
        MapObjectKind.TwoWayGate => "Linked in pairs: a hero who enters one comes out of the other.",
        MapObjectKind.OneWayGateEntrance => "Sends the hero to its exit; there is no way back through it.",
        MapObjectKind.OneWayGateExit => "Where a one-way portal lets its heroes out.",
        MapObjectKind.Marketplace => "Trades resources; every marketplace a player owns betters the rate.",
        MapObjectKind.Buff => "A bonus that lasts for a time (morale, luck, movement or mana).",
        MapObjectKind.Training => "A lasting gain for the visiting hero (experience or a primary skill).",
        MapObjectKind.Sign => "Shows the map author's message.",
        MapObjectKind.Observatory => "Reveals the land round it.",
        MapObjectKind.HillFort => "Upgrades the visiting hero's creatures for a price.",
        MapObjectKind.TownGate => "Takes the visitor to one of the owner's towns.",
        MapObjectKind.WarMachineFactory => "Sells war machines.",
        MapObjectKind.Prison => "Holds a hero who joins the player that frees it.",
        MapObjectKind.CreatureBank => "A guard to beat for a reward; it fills again four weeks later.",
        MapObjectKind.DenOfThieves => "Shows the thieves' guild table of every player.",
        MapObjectKind.Sanctuary => "A hero inside cannot be attacked.",
        MapObjectKind.Scenery => "Blocks its tiles; nothing else.",
        MapObjectKind.Cartographer => "Sells the map of its level.",
        MapObjectKind.ArtifactMerchant => "Sells seven artifacts, restocked every month.",
        MapObjectKind.RefugeeCamp => "Offers one creature to recruit, another every week.",
        MapObjectKind.SacrificialAltar => "Takes creatures or artifacts for experience.",
        MapObjectKind.Sphinx => "Asks a riddle, once per hero: a reward or a penalty.",
        MapObjectKind.WitchHut => "Teaches one of two skills.",
        MapObjectKind.Shrine => $"Teaches a level {d.Amount} spell to a hero who can learn it.",
        MapObjectKind.SkillSchool => "Sells lessons in skills for gold and rare resources.",
        MapObjectKind.Obelisk => "Shows a share of the puzzle map round the Grail.",
        MapObjectKind.CombatPlace => "Where an arena map's battle is fought.",
        MapObjectKind.MagiWindow => "Reveals the land round the Eyes of its colour.",
        MapObjectKind.MagiEye => "What a Window of the Magi of its colour shows.",
        MapObjectKind.KeymasterTent => "Gives the key of its colour.",
        MapObjectKind.BorderPost => d.Stays ? "Only holders of its colour's key pass; it stays." : "Only a holder of its colour's key passes; then it is gone.",
        MapObjectKind.MapTavern => "Hires heroes from the weekly pool.",
        MapObjectKind.Garrison => "A flagged pass with troops inside.",
        MapObjectKind.AstrologersTower => "Tells next week's event.",
        MapObjectKind.PandorasBox => "The map author's contents, opened once, maybe behind a guard.",
        MapObjectKind.SeerHut => "A quest in stages, each with its reward.",
        MapObjectKind.QuestGuard => "A barrier that leaves once its quest is done.",
        MapObjectKind.QuestGate => "A barrier that lets through the heroes who meet its quest.",
        _ => "",
    };

    private static string Faction(CreatureDefinition creature) => creature.Town is { } town ? Pretty(town) : "Neutral";

    /// <summary>The search words of an entry: its category's and group's names and <paramref name="more"/>, as single lower-case words.</summary>
    private static IReadOnlyList<string> Words(BrowserCategory category, string group, params string[] more) =>
    [
        .. more.Append(NameOf(category)).Append(group)
            .SelectMany(w => w.ToLowerInvariant().Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w != "and")
            .Distinct(StringComparer.Ordinal),
    ];

    private static string Lower<T>(T value)
        where T : struct, Enum => Spaced(value.ToString()).ToLowerInvariant();

    /// <summary><c>dead-tree</c> → <c>Dead tree</c>.</summary>
    private static string Pretty(string id) => id.Length == 0 ? id : char.ToUpperInvariant(id[0]) + id[1..].Replace('-', ' ');

    /// <summary><c>ResourcePile</c> → <c>Resource Pile</c>.</summary>
    private static string Spaced(string name) => string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
}
