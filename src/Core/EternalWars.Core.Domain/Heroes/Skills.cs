using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// A HoMM4-style skill group (ADR-048): three skills whose levels add up to <see cref="SkillGroup.Points"/>. Magic
/// groups are the schools of the magic towns (<see cref="School"/>); a hero learns at most
/// <see cref="HeroSkills.MaxGroups"/> groups.
/// </summary>
/// <param name="Id">Group id, also the prefix of its skill ids (<c>offense.melee</c>).</param>
/// <param name="Name">Display name.</param>
/// <param name="School">Town whose magic school this is; null for the general groups.</param>
public sealed record SkillGroup(string Id, string Name, string? School = null)
{
    /// <summary>Levels needed to learn every skill of a group fully.</summary>
    public const int Points = 10;

    public bool IsMagic => School is not null;
}

/// <param name="Id"><c>group.skill</c>.</param>
/// <param name="Group">Owning <see cref="SkillGroup.Id"/>.</param>
/// <param name="Name">Display name.</param>
/// <param name="MaxLevel">Highest level: Expert (3) or Master (4); 2 for spell mastery.</param>
/// <param name="Description">What each level does (draft numbers live in <see cref="SkillEffects"/>).</param>
public sealed record SkillDefinition(string Id, string Group, string Name, SkillLevel MaxLevel, string Description);

/// <summary>Every skill group and skill (ADR-047, ADR-048). Code, not data: their effects are rules.</summary>
public static class BuiltInSkills
{
    // General groups ---------------------------------------------------------------------------
    public const string Melee = "offense.melee";
    public const string Archery = "offense.archery";
    public const string OffensePlaceholder = "offense.third";
    public const string Armor = "defense.armor";
    public const string SpellResistance = "defense.resistance";
    public const string DefensePlaceholder = "defense.third";
    public const string Treasury = "estates.treasury";
    public const string Prospecting = "estates.prospecting";
    public const string Stewardship = "estates.stewardship";
    public const string Logistics = "pathfinding.logistics";
    public const string Pathfinding = "pathfinding.pathfinding";
    public const string TroopManagement = "pathfinding.troops";
    public const string Tactics = "tactics.tactics";
    public const string Deployment = "tactics.deployment";
    public const string Drill = "tactics.drill";

    /// <summary>Magic school names per town (working titles).</summary>
    public static IReadOnlyDictionary<string, string> Schools { get; } = new Dictionary<string, string>
    {
        [TownIds.Haven] = "Light",
        [TownIds.Sylvan] = "Nature",
        [TownIds.Academy] = "Arcana",
        [TownIds.Necropolis] = "Death",
        [TownIds.Inferno] = "Chaos",
        [TownIds.Dungeon] = "Shadow",
        [TownIds.Stronghold] = "Shamanism",
        [TownIds.SwampBestiary] = "Primal",
    };

    public static IReadOnlyList<SkillGroup> Groups { get; } =
    [
        new("offense", "Offense"),
        new("defense", "Defense"),
        new("estates", "Estates"),
        new("pathfinding", "Pathfinding"),
        new("tactics", "Tactics"),
        .. Schools.Select(s => new SkillGroup(SchoolGroup(s.Key), s.Value, s.Key)),
    ];

    public static IReadOnlyList<SkillDefinition> All { get; } =
    [
        new(Melee, "offense", "Melee", SkillLevel.Master, "+10/20/30/40 % melee damage"),
        new(Archery, "offense", "Archery", SkillLevel.Expert, "+10/25/50 % ranged damage"),
        new(OffensePlaceholder, "offense", "(offense skill to be decided)", SkillLevel.Expert, "no effect yet"),
        new(Armor, "defense", "Armor", SkillLevel.Master, "−5/10/15/20 % damage taken"),
        new(SpellResistance, "defense", "Spell resistance", SkillLevel.Expert, "resists hostile spells (magic, Phase 5)"),
        new(DefensePlaceholder, "defense", "(defense skill to be decided)", SkillLevel.Expert, "no effect yet"),
        new(Treasury, "estates", "Treasury", SkillLevel.Master, "+100/200/350/500 gold a day"),
        new(Prospecting, "estates", "Prospecting", SkillLevel.Expert, "+1 lumber and ore / +2 / +2 and one of each rare resource a day"),
        new(Stewardship, "estates", "Stewardship", SkillLevel.Expert, "−5/10/15 % costs in the town the hero is in (towns, Phase 3)"),
        new(Logistics, "pathfinding", "Logistics", SkillLevel.Master, "+10/20/30/40 % movement points"),
        new(Pathfinding, "pathfinding", "Pathfinding", SkillLevel.Expert, "−25/50/75 % of the non-native terrain penalty"),
        new(TroopManagement, "pathfinding", "Troop management", SkillLevel.Expert, "+1/2/3 to the army's adventure map speed"),
        new(Tactics, "tactics", "Tactics", SkillLevel.Expert, "choose the battlefield width (ADR-033)"),
        new(Deployment, "tactics", "Deployment", SkillLevel.Expert, "rearrange troops before the battle (later)"),
        new(Drill, "tactics", "Drill", SkillLevel.Master, "+1/2/3/4 battlefield speed for every stack"),
        .. Schools.SelectMany(s => SchoolSkills(s.Key, s.Value)),
    ];

    private static readonly Dictionary<string, SkillDefinition> ById = All.ToDictionary(s => s.Id, StringComparer.Ordinal);
    private static readonly Dictionary<string, SkillGroup> GroupById = Groups.ToDictionary(g => g.Id, StringComparer.Ordinal);

    public static SkillDefinition Get(string id) =>
        ById.TryGetValue(id, out var skill) ? skill : throw new KeyNotFoundException($"Unknown skill '{id}'.");

    public static bool Contains(string id) => ById.ContainsKey(id);

    public static SkillGroup GroupOf(string skillId) => GroupById[Get(skillId).Group];

    public static SkillGroup GetGroup(string id) => GroupById[id];

    public static IEnumerable<SkillDefinition> SkillsOf(string group) => All.Where(s => s.Group == group);

    /// <summary>Group id of a town's magic school: <c>magic.haven</c>.</summary>
    public static string SchoolGroup(string town) => $"magic.{town}";

    /// <summary>
    /// Rival towns from the lore ring (lore.md): their schools are offered to each other's heroes only rarely
    /// (<see cref="SkillOffers.RivalSchoolChance"/>). The one rule effect of rivalries (ADR-048, an exception to ADR-029).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Rivals { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [TownIds.Haven] = [TownIds.Necropolis, TownIds.Inferno],
        [TownIds.Sylvan] = [TownIds.Dungeon, TownIds.Necropolis],
        [TownIds.Academy] = [TownIds.Inferno, TownIds.Dungeon],
        [TownIds.Necropolis] = [TownIds.Haven, TownIds.Sylvan],
        [TownIds.Inferno] = [TownIds.Haven, TownIds.Academy],
        [TownIds.Dungeon] = [TownIds.Academy, TownIds.Sylvan],
        [TownIds.Stronghold] = [],
        [TownIds.SwampBestiary] = [],
    };

    /// <summary>Spells (4 levels), attunement (4 levels: mana, regeneration, cost or potency by school), mastery (2 levels: spell upgrades).</summary>
    private static IEnumerable<SkillDefinition> SchoolSkills(string town, string school)
    {
        var group = SchoolGroup(town);
        yield return new($"{group}.spells", group, $"{school} magic", SkillLevel.Master, $"learn {school} spells of levels 1/2/3/4");
        yield return new($"{group}.attunement", group, $"{school} attunement", SkillLevel.Master, "mana pool and regeneration; spell cost or potency by school (Phase 5)");
        yield return new($"{group}.mastery", group, $"{school} mastery", SkillLevel.Advanced, "upgrade learned spells to their second / third level (Phase 5)");
    }
}

/// <summary>Draft numbers of the skills that work before towns and magic exist (ADR-048).</summary>
public static class SkillEffects
{
    public static int MeleeDamagePercent(SkillLevel level) => 10 * (int)level;

    public static int RangedDamagePercent(SkillLevel level) => level switch
    {
        SkillLevel.Basic => 10,
        SkillLevel.Advanced => 25,
        >= SkillLevel.Expert => 50,
        _ => 0,
    };

    public static int DamageTakenReductionPercent(SkillLevel level) => 5 * (int)level;

    public static int TreasuryGold(SkillLevel level) => level switch
    {
        SkillLevel.Basic => 100,
        SkillLevel.Advanced => 200,
        SkillLevel.Expert => 350,
        SkillLevel.Master => 500,
        _ => 0,
    };

    public static Economy.ResourceSet Prospecting(SkillLevel level) => level switch
    {
        SkillLevel.Basic => new(Lumber: 1, Ore: 1),
        SkillLevel.Advanced => new(Lumber: 2, Ore: 2),
        >= SkillLevel.Expert => new(Lumber: 2, Ore: 2, Crystal: 1, Mercury: 1, Sulfur: 1),
        _ => Economy.ResourceSet.Empty,
    };

    public static int MovementPercent(SkillLevel level) => 10 * (int)level;

    public static int PenaltyReductionPercent(SkillLevel level) => 25 * Math.Min(3, (int)level);

    public static int MapSpeedBonus(SkillLevel level) => (int)level;

    public static int BattleSpeedBonus(SkillLevel level) => (int)level;

    /// <summary>Flat bonus of a hero's preferred creature line (base and upgrade): Attack, Defense, battle speed.</summary>
    public const int PreferredAttack = 2;

    public const int PreferredDefense = 2;

    public const int PreferredSpeed = 1;
}

/// <summary>A hero's skill levels; at most <see cref="MaxGroups"/> groups.</summary>
public sealed class HeroSkills
{
    public const int MaxGroups = 6;

    private readonly Dictionary<string, SkillLevel> _levels = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, SkillLevel> Levels => _levels;

    public SkillLevel Level(string skill) => _levels.GetValueOrDefault(skill);

    /// <summary>Groups with at least one learned skill.</summary>
    public IEnumerable<string> Groups => _levels.Keys.Select(id => BuiltInSkills.Get(id).Group).Distinct();

    /// <summary>One more level is allowed: below the skill's maximum, and its group is learned or a group slot is free.</summary>
    public bool CanRaise(string skill)
    {
        var definition = BuiltInSkills.Get(skill);
        return Level(skill) < definition.MaxLevel && (Groups.Contains(definition.Group) || Groups.Count() < MaxGroups);
    }

    /// <summary>Sets a level directly (map data, editor, roster); checks the maximum and the group limit.</summary>
    internal void Set(string skill, SkillLevel level)
    {
        var definition = BuiltInSkills.Get(skill);
        if (level > definition.MaxLevel || level < SkillLevel.None)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"{definition.Name} goes up to {definition.MaxLevel}.");
        }

        if (level == SkillLevel.None)
        {
            _levels.Remove(skill);
            return;
        }

        if (!Groups.Contains(definition.Group) && Groups.Count() >= MaxGroups)
        {
            throw new InvalidOperationException($"A hero learns at most {MaxGroups} skill groups.");
        }

        _levels[skill] = level;
    }

    internal void Raise(string skill)
    {
        if (!CanRaise(skill))
        {
            throw new InvalidOperationException($"{BuiltInSkills.Get(skill).Name} cannot be raised.");
        }

        _levels[skill] = Level(skill) + 1;
    }

    internal void ReplaceWith(IReadOnlyDictionary<string, SkillLevel> levels)
    {
        _levels.Clear();
        foreach (var (skill, level) in levels)
        {
            Set(skill, level);
        }
    }

    public override string ToString() => string.Join(",", _levels.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => $"{l.Key}={l.Value}"));
}
