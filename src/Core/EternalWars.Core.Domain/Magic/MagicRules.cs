using EternalWars.Core.Domain.Common;
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

    /// <summary>The Mastery skill of a spell's school (<c>magic.haven.mastery</c>).</summary>
    public static string MasterySkill(string school) => $"{BuiltInSkills.SchoolGroup(school)}.mastery";

    /// <summary>Whether the hero's school skill is high enough for the spell: Basic Magic allows level 1, Master level 4 (ADR-071).</summary>
    public static bool CanLearn(Hero hero, SpellDefinition spell) =>
        hero.HasSpellBook && (int)hero.SkillLevelOf(MagicSkill(spell.School)) >= spell.Level;

    /// <summary>A spell's name with its rank for the screens: "Bless", "Bless II", "Bless III".</summary>
    public static string NameAtRank(SpellDefinition spell, int rank) => rank switch
    {
        >= 3 => $"{spell.Name} III",
        2 => $"{spell.Name} II",
        _ => spell.Name,
    };

    /// <summary>The highest rank the hero's Mastery of the spell's school allows (ADR-072, MAG-3): 1 without it, 2 at Basic, 3 at Advanced.</summary>
    public static int MaxRank(Hero hero, SpellDefinition spell) =>
        Math.Min(Creatures.SpellLevels.MaxRank, 1 + (int)hero.SkillLevelOf(MasterySkill(spell.School)));

    /// <summary>
    /// Game start (ADR-106, MAG-13): every town, by id, draws 3 / 3 / 2 / 2 of its school's spells per level without repeats from the game's
    /// random source; the spells the map fixes (<see cref="Town.FixedGuildSpells"/>) take their places first and the draw fills the rest from the
    /// others. Towns without a school (Stronghold, Swamp Bestiary) offer none. Then every town, by id, draws its spell buildings' spells
    /// (<see cref="Town.SchoolSpells"/>) from the same source, school by school in id order, one per level.
    /// </summary>
    internal static void DrawGuilds(GameState state)
    {
        var towns = state.ObjectsOf<Town>().OrderBy(t => t.Id.Value).ToList();
        foreach (var town in towns)
        {
            var guild = new Dictionary<int, IReadOnlyList<string>>();
            for (var level = 1; level <= GuildSpellsPerLevel.Count; level++)
            {
                var pool = state.Spells.Of(town.TownType, level).Select(s => s.Id).ToList();
                var drawn = town.FixedGuildSpells.Where(pool.Contains).Distinct(StringComparer.Ordinal).Take(GuildSpellsPerLevel[level - 1]).ToList();
                pool.RemoveAll(drawn.Contains);
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

        foreach (var town in towns)
        {
            var schools = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var school in TownRules.Buildings(state, town).TownBuildings.Select(BuiltInBuildings.SchoolOf).OfType<string>().Distinct().Order(StringComparer.Ordinal))
            {
                var spells = new List<string>();
                for (var level = 1; level <= GuildSpellsPerLevel.Count; level++)
                {
                    var pool = state.Spells.Of(school, level);
                    if (pool.Count > 0)
                    {
                        spells.Add(pool[state.Random.Next(pool.Count)].Id);
                    }
                }

                schools[school] = spells;
            }

            town.SetSchoolSpells(schools);
        }
    }

    /// <summary>
    /// Why the spells a map fixes for a town's guild (<see cref="Town.FixedGuildSpells"/>, map v30) are wrong, or null: each must be a known spell of
    /// the town's school, of a level the guild has, listed once, and no level may list more than the guild offers there (3 / 3 / 2 / 2).
    /// </summary>
    public static string? GuildSpellsProblem(SpellCatalog spells, string townType, IReadOnlyList<string> fixedSpells)
    {
        foreach (var id in fixedSpells)
        {
            if (!spells.TryGet(id, out var spell))
            {
                return $"'{id}' is not a spell.";
            }

            if (spell.School != townType)
            {
                return $"{spell.Name} is not a spell of the {townType} school.";
            }

            if (spell.Level < 1 || spell.Level > GuildSpellsPerLevel.Count)
            {
                return $"{spell.Name} is a level {spell.Level} spell; a guild teaches levels 1 to {GuildSpellsPerLevel.Count}.";
            }
        }

        if (fixedSpells.GroupBy(s => s, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            return $"{spells.Get(twice.Key).Name} is listed twice.";
        }

        foreach (var level in fixedSpells.GroupBy(s => spells.Get(s).Level))
        {
            if (level.Count() > GuildSpellsPerLevel[level.Key - 1])
            {
                return $"{level.Count()} level {level.Key} spells; the guild offers {GuildSpellsPerLevel[level.Key - 1]}.";
            }
        }

        return null;
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

    /// <summary>The spells the town's guild teaches now: its drawn spells up to the guild's level, then those of its built spell buildings.</summary>
    public static IEnumerable<string> Offered(GameState state, Town town) =>
        town.GuildSpells.Where(l => l.Key <= town.Level(BuiltInBuildings.MageGuild)).OrderBy(l => l.Key).SelectMany(l => l.Value)
            .Concat(SchoolBuildings(town).SelectMany(school => SchoolSpells(state, town, school).Take(town.Level(BuiltInBuildings.MageGuild))));

    /// <summary>The schools of the town's built spell buildings (ADR-072), in id order.</summary>
    public static IEnumerable<string> SchoolBuildings(Town town) =>
        town.Buildings.Where(b => b.Value > 0).Select(b => BuiltInBuildings.SchoolOf(b.Key)).OfType<string>().Order(StringComparer.Ordinal);

    /// <summary>
    /// The spells a spell building of <paramref name="school"/> adds in this town (ADR-072, MAG-12): one of the school's spells for each spell
    /// level 1 to 4, in level order; a guild of level L teaches the first L. They are drawn at game start from the game's random source like the
    /// guild's (<see cref="DrawGuilds"/>; the map does not fix them). A level without spells of the school is skipped; empty before the game starts.
    /// </summary>
    public static IReadOnlyList<string> SchoolSpells(GameState state, Town town, string school) =>
        town.SchoolSpells.GetValueOrDefault(school) ?? [];

    /// <summary>
    /// A hero in an own town (on the entrance) with a mage guild learns the offered spells it may and does not know yet, and the offered spells it
    /// knows rise, free, straight to the rank its Mastery allows (ADR-072, ADR-122; a spell learned elsewhere rises too). Its mana fills up only
    /// after a night there (<paramref name="night"/>: the start of the owner's turn), not on entering. Nothing happens without a guild, and no
    /// learning without a book. A war lodge instead teaches the warcries up to its level (<see cref="Hero.WarcriesLearned"/>).
    /// </summary>
    internal static void Visit(GameState state, IEventPublisher events, Hero hero, Town town, bool night = false)
    {
        if (town.Owner != hero.Owner)
        {
            return;
        }

        // Stronghold's and Swamp Bestiary's war lodge (ADR-071, placeholder) teaches the warcries up to its level instead of spells.
        if (town.Level(BuiltInBuildings.WarLodge) is var lodge and > 0 && lodge > hero.WarcriesLearned)
        {
            hero.WarcriesLearned = lodge;
            events.Publish(new WarcriesLearned(hero.Id, town.Id, lodge));
        }

        if (town.Level(BuiltInBuildings.MageGuild) < 1)
        {
            return;
        }

        var offered = Offered(state, town).ToList();
        var learned = offered.Where(id => state.Spells.TryGet(id, out var spell) && CanLearn(hero, spell) && hero.Learn(id)).ToList();
        if (learned.Count > 0)
        {
            events.Publish(new SpellsLearned(hero.Id, town.Id, learned));
        }

        if (hero.HasSpellBook)
        {
            var raised = offered.Distinct(StringComparer.Ordinal)
                .Where(id => state.Spells.TryGet(id, out var spell) && hero.RaiseSpell(id, MaxRank(hero, spell)))
                .Select(id => new SpellRank(id, hero.Spells[id]))
                .ToList();
            if (raised.Count > 0)
            {
                events.Publish(new SpellsRaised(hero.Id, town.Id, raised));
            }
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
