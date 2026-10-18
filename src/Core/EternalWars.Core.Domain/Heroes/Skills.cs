using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// A HoMM4-style skill group (ADR-048): three skills whose levels add up to <see cref="SkillGroup.Points"/>. Magic
/// groups are the schools of the magic towns (<see cref="School"/>); a hero learns at most
/// <see cref="HeroSkills.MaxGroups"/> groups.
/// </summary>
/// <param name="Id">Group id, also the prefix of its skill ids (<c>offense.melee</c>).</param>
/// <param name="Name">Display name.</param>
/// <param name="School">Town whose magic school this is; null for the general and faction groups.</param>
/// <param name="Faction">Town whose own abilities these are (Stronghold, Swamp Bestiary: no magic school); only its heroes are offered them.</param>
public sealed record SkillGroup(string Id, string Name, string? School = null, string? Faction = null)
{
    /// <summary>Levels needed to learn every skill of a group fully.</summary>
    public const int Points = 10;

    public bool IsMagic => School is not null;

    /// <summary>The town this group belongs to: its school or its faction abilities.</summary>
    public string? Town => School ?? Faction;
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
    public const string Leadership = "offense.third";
    public const string Armor = "defense.armor";
    public const string SpellResistance = "defense.resistance";
    public const string Alertness = "defense.third";
    public const string Treasury = "estates.treasury";
    public const string Prospecting = "estates.prospecting";
    public const string Stewardship = "estates.stewardship";
    public const string Logistics = "pathfinding.logistics";
    public const string Pathfinding = "pathfinding.pathfinding";
    public const string TroopManagement = "pathfinding.troops";
    public const string Tactics = "tactics.tactics";
    public const string Deployment = "tactics.deployment";
    public const string Drill = "tactics.drill";
    public const string Ballistics = "machines.ballistics";
    public const string Healing = "machines.healing";
    public const string AmmoSupply = "machines.ammo";

    // Faction groups (Stronghold and Swamp Bestiary have no magic school, ADR-063) --------------------------
    /// <summary>Stronghold's Primal Fury group (working title; the id stays <c>rage</c>, ADR-067).</summary>
    public const string RageGroup = "rage";
    public const string AdrenalineGroup = "adrenaline";

    /// <summary>Primal Fury, 4 points: stacks that kill gain Attack for the rest of the battle (ADR-067).</summary>
    public const string BloodRage = "rage.first";

    /// <summary>Primal Fury, 3 points: the hero's warcries, instead of spells (ADR-067).</summary>
    public const string Warcry = "rage.second";

    /// <summary>Primal Fury, 3 points: the enemy hero's spells lose Spell Power; not the Defense skill of that name (ADR-067).</summary>
    public const string FactionSpellResistance = "rage.third";

    /// <summary>Magic school names per town (working titles).</summary>
    public static IReadOnlyDictionary<string, string> Schools { get; } = new Dictionary<string, string>
    {
        [TownIds.Haven] = "Light",
        [TownIds.Sylvan] = "Nature",
        [TownIds.Academy] = "Arcana",
        [TownIds.Necropolis] = "Death",
        [TownIds.Inferno] = "Chaos",
        [TownIds.Dungeon] = "Shadow",
    };

    /// <summary>Faction ability groups: group id per town without a magic school (ADR-063; Primal Fury is designed, Adrenaline is a placeholder, ADR-067).</summary>
    public static IReadOnlyDictionary<string, (string Group, string Name)> FactionGroups { get; } = new Dictionary<string, (string, string)>
    {
        [TownIds.Stronghold] = (RageGroup, "Primal Fury"),
        [TownIds.SwampBestiary] = (AdrenalineGroup, "Adrenaline"),
    };

    public static IReadOnlyList<SkillGroup> Groups { get; } =
    [
        new("offense", "Offense"),
        new("defense", "Defense"),
        new("estates", "Estates"),
        new("pathfinding", "Pathfinding"),
        new("tactics", "Tactics"),
        new("machines", "War machines"),
        .. Schools.Select(s => new SkillGroup(SchoolGroup(s.Key), s.Value, s.Key)),
        .. FactionGroups.Select(f => new SkillGroup(f.Value.Group, f.Value.Name, Faction: f.Key)),
    ];

    public static IReadOnlyList<SkillDefinition> All { get; } =
    [
        new(Melee, "offense", "Melee", SkillLevel.Master, "+10/20/30/40 % melee damage"),
        new(Archery, "offense", "Archery", SkillLevel.Expert, "+10/25/50 % ranged damage"),
        new(Leadership, "offense", "Leadership", SkillLevel.Expert, "+1/2/3 morale to every own stack"),
        new(Armor, "defense", "Armor", SkillLevel.Master, "−5/10/15/20 % damage taken"),
        new(SpellResistance, "defense", "Spell resistance", SkillLevel.Expert, "resists hostile spells (magic, Phase 5)"),
        new(Alertness, "defense", "Alertness", SkillLevel.Expert, "+5/10/15 % initiative to every own stack in battle (working title)"),
        new(Treasury, "estates", "Treasury", SkillLevel.Master, "+250/500/750/1000 gold a day; Expert and Master also 0.5/1 % of the gold on hand"),
        new(Prospecting, "estates", "Prospecting", SkillLevel.Expert, "+1 lumber and ore / +2 / +2 and one of each rare resource a day; 10/20/30 % chance of +50 % from a resource pile"),
        new(Stewardship, "estates", "Stewardship", SkillLevel.Expert, "governed town only: buildings −5/10/15 %, creatures −10/15/20 % (StewardshipRules)"),
        new(Logistics, "pathfinding", "Logistics", SkillLevel.Master, "+10/20/30/40 % movement points"),
        new(Pathfinding, "pathfinding", "Pathfinding", SkillLevel.Expert, "−25/50/75 % of the non-native terrain penalty"),
        new(TroopManagement, "pathfinding", "Troop management", SkillLevel.Expert, "+1/2/3 to the army's adventure map speed"),
        new(Tactics, "tactics", "Tactics", SkillLevel.Expert, "choose the battlefield width (ADR-033)"),
        new(Deployment, "tactics", "Deployment", SkillLevel.Expert, "rearrange troops before the battle (later)"),
        new(Drill, "tactics", "Drill", SkillLevel.Master, "+1/2/3/4 battlefield speed for every stack"),
        new(Ballistics, "machines", "Ballistics", SkillLevel.Master, "siege machine fires by itself (none), under control (Basic), at troops too and twice after a kill (Advanced), never misses a fortification and +25 % damage (Expert), 2-3 structure damage and +50 % (Master) (ADR-056, ADR-063)"),
        new(Healing, "machines", "Healing", SkillLevel.Master, "the healing tent restores 20/30/40/50 health per stack a round (10 without)"),
        new(AmmoSupply, "machines", "Ammo supply", SkillLevel.Advanced, "the ammo cart gives +50 % shots / unlimited shots (+25 % without)"),
        .. Schools.SelectMany(s => SchoolSkills(s.Key, s.Value)),
        .. FactionGroups.Values.SelectMany(f => FactionSkills(f.Group, f.Name)),
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
    /// Rival towns, the sworn enemies of the lore (docs/content/lore.md): their schools are offered to each other's heroes only
    /// rarely (<see cref="SkillOffers.RivalSchoolChance"/>, ADR-048). One of the two rule effects of rivalries, exceptions to
    /// ADR-029; the other is the tavern ring (<see cref="TavernRules.Ring"/>, ADR-060).
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

    /// <summary>Three skills per faction group (10 points: 4 + 3 + 3): Primal Fury is designed (ADR-067), Adrenaline still has placeholders (ADR-063).</summary>
    private static IEnumerable<SkillDefinition> FactionSkills(string group, string name)
    {
        if (group == RageGroup)
        {
            yield return new(BloodRage, group, "Blood Rage", SkillLevel.Master, "a stack that kills creatures in an attack gains +1 Attack, up to +2/4/6/8, until the battle ends");
            yield return new(Warcry, group, "Warcry", SkillLevel.Expert, "warcries in battle, no mana: Rally (own +1 morale), from Advanced Intimidate (enemy −1 morale, −20 % initiative), at Expert Terror (30 % that an enemy stack loses its next action)");
            yield return new(FactionSpellResistance, group, "Spell resistance (faction)", SkillLevel.Expert, "the enemy hero's spells have −2/4/6 Spell Power (magic, Phase 5)");
            yield break;
        }

        yield return new($"{group}.first", group, $"{name} (first skill to be decided)", SkillLevel.Master, "no effect yet");
        yield return new($"{group}.second", group, $"{name} (second skill to be decided)", SkillLevel.Expert, "no effect yet");
        yield return new($"{group}.third", group, $"{name} (third skill to be decided)", SkillLevel.Expert, "no effect yet");
    }

    /// <summary>Spells (4 levels), attunement (4 levels: mana, regeneration, cost or potency by school), mastery (2 levels: spell upgrades).</summary>
    private static IEnumerable<SkillDefinition> SchoolSkills(string town, string school)
    {
        var group = SchoolGroup(town);
        yield return new($"{group}.spells", group, $"{school} magic", SkillLevel.Master, $"learn {school} spells of levels 1/2/3/4");
        yield return new($"{group}.attunement", group, $"{school} attunement", SkillLevel.Master, "max mana, daily mana and spell cost by school (ManaRules)");
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
        SkillLevel.Basic => 250,
        SkillLevel.Advanced => 500,
        SkillLevel.Expert => 750,
        SkillLevel.Master => 1000,
        _ => 0,
    };

    /// <summary>Treasury Expert and Master also pay this share of the player's gold on hand, in tenths of a percent (0.5 %, 1 %).</summary>
    public static int TreasuryInterestPermille(SkillLevel level) => level switch
    {
        SkillLevel.Expert => 5,
        SkillLevel.Master => 10,
        _ => 0,
    };

    /// <summary>Prospecting: chance in percent that a picked-up resource pile gives 50 % more.</summary>
    public static int ProspectingPileChancePercent(SkillLevel level) => 10 * Math.Min(3, (int)level);

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

    /// <summary>Leadership: morale of every own stack (ADR-067).</summary>
    public static int LeadershipMorale(SkillLevel level) => Math.Min(3, (int)level);

    /// <summary>Alertness: percent initiative of every own stack in battle (ADR-067, working title).</summary>
    public static int AlertnessInitiativePercent(SkillLevel level) => 5 * Math.Min(3, (int)level);

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
