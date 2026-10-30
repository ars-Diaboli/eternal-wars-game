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
/// <param name="Factions">Towns whose own abilities these are (Stronghold and Swamp Bestiary: no magic school, ADR-069); only their heroes are offered them.</param>
public sealed record SkillGroup(string Id, string Name, string? School = null, IReadOnlyList<string>? Factions = null)
{
    /// <summary>Levels needed to learn every skill of a group fully.</summary>
    public const int Points = 10;

    public bool IsMagic => School is not null;

    public bool IsFaction => Factions is not null;

    /// <summary>Whether this is the town's own group: its school or its faction abilities.</summary>
    public bool BelongsTo(string? town) => town is not null && (School == town || (Factions?.Contains(town) ?? false));
}

/// <param name="Id"><c>group.skill</c>.</param>
/// <param name="Group">Owning <see cref="SkillGroup.Id"/>.</param>
/// <param name="Name">Display name.</param>
/// <param name="MaxLevel">Highest level: Expert (3) or Master (4); 2 for spell mastery.</param>
/// <param name="Description">What each level does (draft numbers live in <see cref="SkillEffects"/>).</param>
public sealed record SkillDefinition(string Id, string Group, string Name, SkillLevel MaxLevel, string Description);

/// <summary>Every skill group and skill (ADR-047, ADR-048, reworked by ADR-069). Code, not data: their effects are rules.</summary>
public static class BuiltInSkills
{
    // General groups ---------------------------------------------------------------------------
    public const string Melee = "offense.melee";
    public const string Archery = "offense.archery";
    public const string Marksmanship = "offense.marksmanship";
    public const string Armor = "defense.armor";
    public const string SpellResistance = "defense.resistance";
    public const string Leadership = "defense.leadership";
    public const string Treasury = "estates.treasury";
    public const string Prospecting = "estates.prospecting";
    public const string Stewardship = "estates.stewardship";
    public const string Pathfinding = "pathfinding.pathfinding";
    public const string Logistics = "pathfinding.logistics";
    public const string Scouting = "pathfinding.scouting";
    public const string WarTempo = "tactics.wartempo";
    public const string Tactics = "tactics.tactics";
    public const string Alertness = "tactics.alert";
    public const string Ballistics = "machines.ballistics";
    public const string Healing = "machines.healing";
    public const string AmmoSupply = "machines.ammo";

    // Faction group (Stronghold and Swamp Bestiary have no magic school, ADR-063, ADR-069) ------------------
    /// <summary>Primal Fury, the group of Stronghold and Swamp Bestiary (working title; the id stays <c>rage</c>, ADR-067, ADR-069).</summary>
    public const string RageGroup = "rage";

    /// <summary>Primal Fury, 4 points: stacks that kill gain Attack for the rest of the battle (ADR-067).</summary>
    public const string BloodRage = "rage.first";

    /// <summary>Primal Fury, 3 points: the hero's warcries, instead of spells (ADR-067).</summary>
    public const string Warcry = "rage.second";

    /// <summary>Primal Fury, 3 points: the enemy hero's spells lose Spell Power and cost more mana (ADR-069, ADR-072).</summary>
    public const string Interference = "rage.third";

    /// <summary>
    /// Ids before the ADR-069 rework and their successors (ADR-072, HERO-6): maps, saves, the roster and replays read them as the new skill;
    /// a level above the new skill's maximum drops to it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Renamed { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["offense.third"] = Leadership,
        ["defense.third"] = Alertness,
        ["tactics.drill"] = WarTempo,
        ["tactics.deployment"] = Tactics,
        ["pathfinding.troops"] = Scouting,
        ["adrenaline.first"] = BloodRage,
        ["adrenaline.second"] = Warcry,
        ["adrenaline.third"] = Interference,
    };

    /// <summary>The current id of a skill: its successor for an id from before the rework, else the id itself.</summary>
    public static string Resolve(string id) => Renamed.GetValueOrDefault(id, id);

    /// <summary>
    /// Skill levels read from older data with the renamed ids resolved (ADR-072): a level above the skill's maximum drops to it, and two old
    /// skills that became one keep the higher level. Unknown ids are kept, so the caller reports them.
    /// </summary>
    public static IReadOnlyDictionary<string, SkillLevel> Migrate(IReadOnlyDictionary<string, SkillLevel> levels)
    {
        var migrated = new Dictionary<string, SkillLevel>(StringComparer.Ordinal);
        foreach (var (old, level) in levels)
        {
            var id = Resolve(old);
            var capped = Contains(id) && level > Get(id).MaxLevel ? Get(id).MaxLevel : level;
            migrated[id] = migrated.TryGetValue(id, out var other) && other > capped ? other : capped;
        }

        return migrated;
    }

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

    /// <summary>Towns without a magic school, whose heroes are offered Primal Fury instead (ADR-063, ADR-069).</summary>
    public static IReadOnlyList<string> PrimalFuryTowns { get; } = [TownIds.Stronghold, TownIds.SwampBestiary];

    /// <summary>Whether the town's heroes have the Primal Fury group (their mages have no spells, ADR-072).</summary>
    public static bool IsPrimalFuryTown(string? town) => town is not null && PrimalFuryTowns.Contains(town);

    public static IReadOnlyList<SkillGroup> Groups { get; } =
    [
        new("offense", "Offense"),
        new("defense", "Defense"),
        new("estates", "Estates"),
        new("pathfinding", "Pathfinding"),
        new("tactics", "Tactics"),
        new("machines", "War machines"),
        .. Schools.Select(s => new SkillGroup(SchoolGroup(s.Key), s.Value, s.Key)),
        new(RageGroup, "Primal Fury", Factions: PrimalFuryTowns),
    ];

    public static IReadOnlyList<SkillDefinition> All { get; } =
    [
        new(Melee, "offense", "Melee", SkillLevel.Master, "+9/16/23/30 % melee damage"),
        new(Archery, "offense", "Archery", SkillLevel.Expert, "+15/25/35 % ranged damage"),
        new(Marksmanship, "offense", "Marksmanship", SkillLevel.Expert, "−2/8, −4/8, −6/8 of the ranged penalty (distance and cover)"),
        new(Armor, "defense", "Armor", SkillLevel.Master, "−5/10/15/20 % damage taken"),
        new(SpellResistance, "defense", "Spell resistance", SkillLevel.Expert, "hostile spells −8/14/20 % damage and duration"),
        new(Leadership, "defense", "Leadership", SkillLevel.Expert, "+1/2/3 morale to every own stack"),
        new(Treasury, "estates", "Treasury", SkillLevel.Master, "+250/500/750/1000 gold a day; Expert and Master also 0.5/1 % of the gold on hand"),
        new(Prospecting, "estates", "Prospecting", SkillLevel.Expert, "+1 lumber and ore / +2 / +2 and one of each rare resource a day; 10/20/30 % chance of +50 % from a resource pile"),
        new(Stewardship, "estates", "Stewardship", SkillLevel.Expert, "governed town only: buildings −5/10/15 %, creatures −10/15/20 % (StewardshipRules)"),
        new(Pathfinding, "pathfinding", "Pathfinding", SkillLevel.Master, "−25/50/75/100 % of the non-native terrain penalty"),
        new(Logistics, "pathfinding", "Logistics", SkillLevel.Expert, "+10/20/30 % movement points"),
        new(Scouting, "pathfinding", "Scouting", SkillLevel.Expert, "+2/4/6 sight radius on the map"),
        new(WarTempo, "tactics", "War Tempo", SkillLevel.Master, "+1/2/3/4 battlefield speed for every stack"),
        new(Tactics, "tactics", "Tactics", SkillLevel.Expert, "place the troops before the battle within 4/5/6 columns; from Advanced also choose the width (30–32, Expert 28–34)"),
        new(Alertness, "tactics", "Alertness", SkillLevel.Expert, "+1/2/3 initiative to every own stack in battle (working title)"),
        new(Ballistics, "machines", "Ballistics", SkillLevel.Master, "siege machine fires by itself (none), under control (Basic), at troops too and twice after a kill (Advanced), never misses a fortification and +25 % damage (Expert), 2-3 structure damage and +50 % (Master) (ADR-056, ADR-063)"),
        new(Healing, "machines", "Healing", SkillLevel.Master, "the healing tent restores 20/30/40/50 health per stack a round (10 without)"),
        new(AmmoSupply, "machines", "Ammo supply", SkillLevel.Advanced, "the ammo cart gives +50 % shots / unlimited shots (+25 % without)"),
        .. Schools.SelectMany(s => SchoolSkills(s.Key, s.Value)),
        new(BloodRage, RageGroup, "Blood Rage", SkillLevel.Master, "a stack that kills creatures in an attack gains +1 Attack, up to +2/4/6/8, until the battle ends"),
        new(Warcry, RageGroup, "Warcry", SkillLevel.Expert, "warcries in battle, no mana: Rally (own +1 morale), from Advanced Intimidate (enemy −1 morale, −20 % initiative), at Expert Terror (30 % that an enemy stack loses its next action)"),
        new(Interference, RageGroup, "Interference", SkillLevel.Expert, "the enemy hero's spells have −2/4/6 Spell Power and cost +1/2/3 mana (magic, Phase 5)"),
    ];

    private static readonly Dictionary<string, SkillDefinition> ById = All.ToDictionary(s => s.Id, StringComparer.Ordinal);
    private static readonly Dictionary<string, SkillGroup> GroupById = Groups.ToDictionary(g => g.Id, StringComparer.Ordinal);

    /// <summary>The skill with this id; an id from before the rework gives its successor (<see cref="Resolve"/>).</summary>
    public static SkillDefinition Get(string id) =>
        ById.TryGetValue(Resolve(id), out var skill) ? skill : throw new KeyNotFoundException($"Unknown skill '{id}'.");

    public static bool Contains(string id) => ById.ContainsKey(Resolve(id));

    public static SkillGroup GroupOf(string skillId) => GroupById[Get(skillId).Group];

    public static SkillGroup GetGroup(string id) => GroupById[id];

    public static IEnumerable<SkillDefinition> SkillsOf(string group) => All.Where(s => s.Group == group);

    /// <summary>Group id of a town's magic school: <c>magic.haven</c>.</summary>
    public static string SchoolGroup(string town) => $"magic.{town}";

    /// <summary>
    /// Rival towns: the arch-rival across the ring of the lore (docs/content/lore.md, ADR-072, HERO-3), one per town; their schools are
    /// offered to each other's heroes only rarely (<see cref="SkillOffers.RivalSchoolChance"/>, ADR-048). The tavern uses the same ring
    /// (<see cref="TavernRules.Ring"/>, ADR-060).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Rivals { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [TownIds.Haven] = [TownIds.Necropolis],
        [TownIds.Sylvan] = [TownIds.Dungeon],
        [TownIds.Academy] = [TownIds.Inferno],
        [TownIds.Necropolis] = [TownIds.Haven],
        [TownIds.Inferno] = [TownIds.Academy],
        [TownIds.Dungeon] = [TownIds.Sylvan],
        [TownIds.Stronghold] = [],
        [TownIds.SwampBestiary] = [],
    };

    /// <summary>Spells (4 levels), attunement (4 levels: mana, regeneration, cost or potency by school), mastery (2 levels: spell upgrades).</summary>
    private static IEnumerable<SkillDefinition> SchoolSkills(string town, string school)
    {
        var group = SchoolGroup(town);
        yield return new($"{group}.spells", group, $"{school} magic", SkillLevel.Master, $"learn {school} spells of levels 1/2/3/4");
        yield return new($"{group}.attunement", group, $"{school} attunement", SkillLevel.Master, "max mana, daily mana and spell cost by school (ManaRules)");
        yield return new($"{group}.mastery", group, $"{school} mastery", SkillLevel.Advanced, "upgrade learned spells to their second / third level (Phase 5)");
    }
}

/// <summary>Draft numbers of the skills that work before towns and magic exist (ADR-048, ADR-069).</summary>
public static class SkillEffects
{
    /// <summary>A skill specialty raises its skill's effect by this many percent per hero level (ADR-072, HERO-1).</summary>
    public const int SpecialtyPercentPerLevel = 5;

    /// <summary>
    /// <paramref name="effect"/> of <paramref name="skill"/>, raised by <see cref="SpecialtyPercentPerLevel"/> per hero level when it is the hero's
    /// skill specialty (rounded down). Used for the effects that are amounts: Melee, Archery, Logistics and the attunements' mana.
    /// </summary>
    public static int WithSpecialty(Hero hero, string skill, int effect) =>
        hero.Specialty is { Kind: SpecialtyKind.Skill } specialty && specialty.Target == skill
            ? effect * (100 + (SpecialtyPercentPerLevel * hero.Level)) / 100
            : effect;

    public static int MeleeDamagePercent(SkillLevel level) => level switch
    {
        SkillLevel.Basic => 9,
        SkillLevel.Advanced => 16,
        SkillLevel.Expert => 23,
        SkillLevel.Master => 30,
        _ => 0,
    };

    public static int RangedDamagePercent(SkillLevel level) => level switch
    {
        SkillLevel.Basic => 15,
        SkillLevel.Advanced => 25,
        >= SkillLevel.Expert => 35,
        _ => 0,
    };

    /// <summary>Marksmanship: eighths taken off the ranged penalty (distance and cover), 2 per level (ADR-070, CMB-6).</summary>
    public static int MarksmanshipEighths(SkillLevel level) => 2 * Math.Min(3, (int)level);

    public static int DamageTakenReductionPercent(SkillLevel level) => 5 * (int)level;

    /// <summary>Spell resistance: percent taken off hostile spells' damage and duration (ADR-071, MAG-6).</summary>
    public static int SpellResistancePercent(SkillLevel level) => level switch
    {
        SkillLevel.Basic => 8,
        SkillLevel.Advanced => 14,
        >= SkillLevel.Expert => 20,
        _ => 0,
    };

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

    /// <summary>Logistics: percent movement points, up to Expert (ADR-069).</summary>
    public static int MovementPercent(SkillLevel level) => 10 * Math.Min(3, (int)level);

    /// <summary>Pathfinding: percent of the non-native terrain penalty taken off, up to Master's 100 % (ADR-069).</summary>
    public static int PenaltyReductionPercent(SkillLevel level) => 25 * (int)level;

    /// <summary>Scouting: tiles added to the hero's sight radius (ADR-069).</summary>
    public static int ScoutingRadius(SkillLevel level) => 2 * Math.Min(3, (int)level);

    /// <summary>War Tempo: battlefield speed of every own stack.</summary>
    public static int BattleSpeedBonus(SkillLevel level) => (int)level;

    /// <summary>Leadership: morale of every own stack (ADR-067).</summary>
    public static int LeadershipMorale(SkillLevel level) => Math.Min(3, (int)level);

    /// <summary>Alertness: initiative points of every own stack in battle, flat (ADR-069, working title).</summary>
    public static int AlertnessInitiative(SkillLevel level) => Math.Min(3, (int)level);

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

    public SkillLevel Level(string skill) => _levels.GetValueOrDefault(BuiltInSkills.Resolve(skill));

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
        skill = BuiltInSkills.Resolve(skill);
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
        skill = BuiltInSkills.Resolve(skill);
        if (!CanRaise(skill))
        {
            throw new InvalidOperationException($"{BuiltInSkills.Get(skill).Name} cannot be raised.");
        }

        _levels[skill] = Level(skill) + 1;
    }

    /// <summary>Takes over these levels; ids from before the rework are read as their successors (<see cref="BuiltInSkills.Migrate"/>).</summary>
    internal void ReplaceWith(IReadOnlyDictionary<string, SkillLevel> levels)
    {
        _levels.Clear();
        foreach (var (skill, level) in BuiltInSkills.Migrate(levels))
        {
            Set(skill, level);
        }
    }

    public override string ToString() => string.Join(",", _levels.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => $"{l.Key}={l.Value}"));
}
