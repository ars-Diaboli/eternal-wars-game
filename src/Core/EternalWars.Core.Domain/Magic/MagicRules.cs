using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Magic;

/// <summary>
/// Spells on the adventure map (ADR-071, ADR-106, ADR-110): each town's guild spells are drawn at game start; a hero with a spell book in an own
/// town with a mage guild learns the guild's spells its school's Magic skill allows, and a night there refills its mana; warriors buy the book for
/// 500 gold.
/// </summary>
public static class MagicRules
{
    /// <summary>What a spell book costs in a mage guild (ADR-071).</summary>
    public const int SpellBookGold = 500;

    /// <summary>How many spells a guild offers at each spell level, 1 to 4 (ADR-047).</summary>
    public static IReadOnlyList<int> GuildSpellsPerLevel { get; } = [3, 3, 2, 2];

    public static Economy.ResourceSet SpellBookCost { get; } = new(Gold: SpellBookGold);

    /// <summary>The Magic skill of a spell's school (<c>magic.haven.spells</c>).</summary>
    public static string MagicSkill(string school) => $"{BuiltInSkills.SchoolGroup(school)}.spells";

    /// <summary>Whether the hero's school skill is high enough for the spell: Basic Magic allows level 1, Master level 4 (ADR-071).</summary>
    public static bool CanLearn(Hero hero, SpellDefinition spell) =>
        hero.HasSpellBook && (int)hero.SkillLevelOf(MagicSkill(spell.School)) >= spell.Level;

    /// <summary>
    /// Game start (ADR-106, MAG-13): every town, by id, draws 3 / 3 / 2 / 2 of its school's spells per level without repeats from the game's
    /// random source. Towns without a school (Stronghold, Swamp Bestiary) offer none.
    /// </summary>
    internal static void DrawGuilds(GameState state)
    {
        foreach (var town in state.ObjectsOf<Town>().OrderBy(t => t.Id.Value))
        {
            var guild = new Dictionary<int, IReadOnlyList<string>>();
            for (var level = 1; level <= GuildSpellsPerLevel.Count; level++)
            {
                var pool = state.Spells.Of(town.TownType, level).Select(s => s.Id).ToList();
                var drawn = new List<string>();
                while (drawn.Count < GuildSpellsPerLevel[level - 1] && pool.Count > 0)
                {
                    var pick = state.Random.Next(pool.Count);
                    drawn.Add(pool[pick]);
                    pool.RemoveAt(pick);
                }

                if (drawn.Count > 0)
                {
                    drawn.Sort(StringComparer.Ordinal);
                    guild[level] = drawn;
                }
            }

            town.SetGuildSpells(guild);
        }
    }

    /// <summary>
    /// The spells the hero can cast, by id: those it has learned at their rank, and while it wears an artifact that teaches spells (the Unwritten
    /// Codex, ADR-111) every spell of those levels of every school at rank 1. Nothing without a spell book.
    /// </summary>
    public static IEnumerable<(SpellDefinition Spell, int Rank)> Known(Hero hero, SpellCatalog spells)
    {
        if (!hero.HasSpellBook)
        {
            return [];
        }

        var known = hero.Spells.Where(s => spells.Contains(s.Key)).ToDictionary(s => s.Key, s => (Spell: spells.Get(s.Key), Rank: s.Value), StringComparer.Ordinal);
        foreach (var spell in spells.All.Where(s => hero.Bonus.SpellLevels.Contains(s.Level)))
        {
            known.TryAdd(spell.Id, (spell, 1));
        }

        return known.Values.OrderBy(k => k.Spell.Id, StringComparer.Ordinal);
    }

    /// <summary>The spells the town's guild teaches now: its drawn spells up to the guild's level.</summary>
    public static IEnumerable<string> Offered(Town town) =>
        town.GuildSpells.Where(l => l.Key <= town.Level(BuiltInBuildings.MageGuild)).OrderBy(l => l.Key).SelectMany(l => l.Value);

    /// <summary>
    /// A hero in an own town (on the entrance) with a mage guild learns the offered spells it may and does not know yet. Its mana fills up only
    /// after a night there (<paramref name="night"/>: the start of the owner's turn), not on entering. Nothing happens without a guild, and no
    /// learning without a book.
    /// </summary>
    internal static void Visit(GameState state, IEventPublisher events, Hero hero, Town town, bool night = false)
    {
        if (town.Owner != hero.Owner || town.Level(BuiltInBuildings.MageGuild) < 1)
        {
            return;
        }

        var learned = Offered(town).Where(id => state.Spells.TryGet(id, out var spell) && CanLearn(hero, spell) && hero.Learn(id)).ToList();
        if (learned.Count > 0)
        {
            events.Publish(new SpellsLearned(hero.Id, town.Id, learned));
        }

        var max = hero.MaxMana;
        if (night && hero.Mana < max)
        {
            hero.Mana = max;
            events.Publish(new HeroManaRestored(hero.Id, hero.Mana, max));
        }
    }

    /// <summary>Visits the town's own hero on the entrance, if any (after a guild is built or a book bought there).</summary>
    internal static void VisitBy(GameState state, IEventPublisher events, Town town)
    {
        if (TownRules.VisitingHero(state, town) is { } hero)
        {
            Visit(state, events, hero, town);
        }
    }

    /// <summary>Why the visiting hero cannot buy a spell book here, or null: it needs a mage guild, an own hero without a book, and 500 gold.</summary>
    public static string? CheckBuySpellBook(GameState state, Town town)
    {
        if (town.Level(BuiltInBuildings.MageGuild) < 1)
        {
            return $"{town.Name} has no mage guild.";
        }

        if (TownRules.VisitingHero(state, town) is not { } hero)
        {
            return $"No own hero is visiting {town.Name}.";
        }

        if (hero.HasSpellBook)
        {
            return $"{hero.Name} already has a spell book.";
        }

        return town.Owner is { } owner && !state.GetPlayer(owner).Resources.CanAfford(SpellBookCost)
            ? "Not enough gold for a spell book."
            : null;
    }
}
