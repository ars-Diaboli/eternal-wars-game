using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>Skill levels (ADR-048): skills stop at Advanced, Expert or Master (<see cref="SkillDefinition.MaxLevel"/>).</summary>
public enum SkillLevel
{
    None,
    Basic,
    Advanced,
    Expert,
    Master,
}

public sealed class Hero(
    HeroId id,
    string name,
    PlayerId owner,
    MapPosition position,
    Army? army = null,
    int attack = 0,
    int defense = 0,
    SkillLevel tactics = SkillLevel.None)
{
    public const int DefaultScoutingRadius = 5;

    public HeroId Id { get; } = id;

    public string Name { get; internal set; } = name;

    public PlayerId Owner { get; internal set; } = owner;

    public MapPosition Position { get; internal set; } = position;

    /// <summary>Up to seven creature stacks. Speed (<see cref="Creatures.CreatureDefinition.MapSpeed"/>) drives movement.</summary>
    public Army Army { get; } = army ?? new Army();

    /// <summary>Primary stat added to every stack's Attack in battle; grows with levels (<see cref="StatGrowth"/>).</summary>
    public int Attack { get; internal set; } = attack;

    /// <summary>Primary stat added to every stack's Defense in battle.</summary>
    public int Defense { get; internal set; } = defense;

    /// <summary>Primary stat: strength of the hero's spells (magic, Phase 5).</summary>
    public int SpellPower { get; internal set; }

    /// <summary>Primary stat: the hero's mana (magic, Phase 5).</summary>
    public int Knowledge { get; internal set; }

    public int Stat(PrimaryStat stat) => stat switch
    {
        PrimaryStat.Attack => Attack,
        PrimaryStat.Defense => Defense,
        PrimaryStat.SpellPower => SpellPower,
        _ => Knowledge,
    };

    private int? _mana;

    /// <summary>
    /// Mana on hand (ADR-063); a hero that has not spent any has a full pool. Only a Mana Vortex takes it above <see cref="MaxMana"/>, to at most
    /// twice that, until it is spent (ADR-070).
    /// </summary>
    public int Mana
    {
        get => Math.Min(2 * MaxMana, _mana ?? MaxMana);
        internal set => _mana = Math.Max(0, value);
    }

    public int MaxMana => ManaRules.MaxMana(this);

    private readonly SortedDictionary<string, int> _spells = new(StringComparer.Ordinal);

    /// <summary>Whether the hero has a spell book (ADR-071): mages of the magic towns start with one, others buy it in a mage guild.</summary>
    public bool HasSpellBook { get; internal set; }

    /// <summary>Spells in the hero's book with their rank (1–3, ADR-072; all rank 1 until Mastery's upgrades are built).</summary>
    public IReadOnlyDictionary<string, int> Spells => _spells;

    /// <summary>Puts a spell in the book at rank 1; false when the hero already knows it.</summary>
    internal bool Learn(string spell) => _spells.TryAdd(spell, 1);

    internal bool AddWarMachine(Battles.WarMachineSlot slot) => _warMachines.Add(slot);

    internal bool RemoveWarMachine(Battles.WarMachineSlot slot) => _warMachines.Remove(slot);

    internal void SetWarMachines(IEnumerable<Battles.WarMachineSlot> slots)
    {
        _warMachines.Clear();
        _warMachines.UnionWith(slots);
    }

    internal void Raise(PrimaryStat stat)
    {
        switch (stat)
        {
            case PrimaryStat.Attack: Attack++; break;
            case PrimaryStat.Defense: Defense++; break;
            case PrimaryStat.SpellPower: SpellPower++; break;
            default: Knowledge++; break;
        }
    }

    /// <summary>Added to the morale of every stack in battle, −5…+5 (ADR-053). Map data until skills and artifacts add to it.</summary>
    public int Morale { get; internal set; }

    /// <summary>Added to the luck of every stack in battle, −5…+5 (ADR-053).</summary>
    public int Luck { get; internal set; }

    private readonly HashSet<Battles.WarMachineSlot> _warMachines = [.. Battles.WarMachines.StartingSet];

    /// <summary>War machines the hero owns (ADR-056); a new hero has its town's offensive machine.</summary>
    public IReadOnlySet<Battles.WarMachineSlot> WarMachines => _warMachines;

    /// <summary>Skill levels by skill id (ADR-048); artifacts may add to them (<see cref="SkillLevelOf"/>).</summary>
    public HeroSkills Skills { get; } = HeroSkillsWith(tactics);

    /// <summary>What the hero wears and carries (ADR-061).</summary>
    public HeroArtifacts Artifacts { get; } = new();

    /// <summary>What the worn artifacts add, kept up to date by <c>GameState.RefreshArtifacts</c>.</summary>
    public ArtifactBonuses Bonus { get; internal set; } = ArtifactBonuses.None;

    public int TotalAttack => Attack + Bonus.Attack;

    public int TotalDefense => Defense + Bonus.Defense;

    public int TotalSpellPower => SpellPower + Bonus.SpellPower;

    public int TotalKnowledge => Knowledge + Bonus.Knowledge;

    /// <summary>Morale: the hero's own, artifacts, Leadership (ADR-067) and temporary statuses (ADR-068).</summary>
    public int TotalMorale => Morale + Bonus.Morale + SkillEffects.LeadershipMorale(SkillLevelOf(BuiltInSkills.Leadership)) + StatusTotal(HeroStatusKind.Morale);

    public int TotalLuck => Luck + Bonus.Luck + StatusTotal(HeroStatusKind.Luck);

    private readonly List<HeroStatus> _statuses = [];

    /// <summary>Temporary effects from map objects, in the order they were gained (ADR-068).</summary>
    public IReadOnlyList<HeroStatus> Statuses => _statuses;

    /// <summary>The sum of the statuses of one kind.</summary>
    public int StatusTotal(HeroStatusKind kind) => _statuses.Where(s => s.Kind == kind).Sum(s => s.Value);

    /// <summary>Adds a status; one of the same source and kind is replaced (the new one keeps its place at the end).</summary>
    internal void AddStatus(HeroStatus status)
    {
        _statuses.RemoveAll(s => s.Source == status.Source && s.Kind == status.Kind);
        _statuses.Add(status);
    }

    /// <summary>Removes and returns the statuses that end with <paramref name="expiry"/>.</summary>
    internal IReadOnlyList<HeroStatus> RemoveStatuses(StatusExpiry expiry)
    {
        var ended = _statuses.Where(s => s.Expires == expiry).ToList();
        _statuses.RemoveAll(s => s.Expires == expiry);
        return ended;
    }

    /// <summary>
    /// The skill level the rules use: learned levels plus what artifacts add to it (and to every started skill), up to the
    /// skill's maximum. A skill an artifact adds to counts even if the hero has not learned it.
    /// </summary>
    public SkillLevel SkillLevelOf(string skill)
    {
        var learned = Skills.Level(skill);
        var bonus = Bonus.SkillBonus(skill) + (learned > SkillLevel.None ? Bonus.AllSkills : 0);
        return bonus == 0 ? learned : (SkillLevel)Math.Min((int)BuiltInSkills.Get(skill).MaxLevel, (int)learned + bonus);
    }

    /// <summary>Tactics skill (<see cref="BuiltInSkills.Tactics"/>): lets the hero pick the battlefield width (ADR-033).</summary>
    public SkillLevel Tactics
    {
        get => Skills.Level(BuiltInSkills.Tactics);
        internal set => Skills.Set(BuiltInSkills.Tactics, value);
    }

    /// <summary>Roster entry (<see cref="HeroDefinition"/>) the hero was made from; null for a custom map hero.</summary>
    public string? Definition { get; internal set; }

    public HeroClass Class { get; internal set; }

    /// <summary>Base id of the creature line (base and upgrade) that gets the preferred-creature bonus in battle.</summary>
    public string? PreferredCreature { get; internal set; }

    public HeroSpecialty? Specialty { get; internal set; }

    /// <summary>Levels gained and chosen so far; <see cref="ExperienceTable.LevelFor"/> may be higher until the choices are made.</summary>
    public int Level { get; internal set; } = 1;

    /// <summary>The level the hero last learned at a witch hut (ADR-079, MAP-34): one witch hut upgrade per level.</summary>
    public int? WitchHutLevel { get; internal set; }

    /// <summary>Today's allowance, recomputed at the start of the owner's turn (<see cref="MovementAllowance"/>).</summary>
    public int MaxMovementPoints { get; internal set; } = MovementAllowance.NoArmyBase;

    public int MovementPoints { get; internal set; } = MovementAllowance.NoArmyBase;

    /// <summary>Percentage movement bonus from skills (e.g. Logistics +10/20/30).</summary>
    public int MovementBonusPercent { get; internal set; }

    /// <summary>Flat movement bonus from artifacts.</summary>
    public int MovementBonusFlat { get; internal set; }

    private int _baseScoutingRadius = DefaultScoutingRadius;

    /// <summary>Tiles revealed around the hero, in tiles (rounded circle): 5, plus Scouting's 2 / 4 / 6 (ADR-069). Setting it sets the base.</summary>
    public int ScoutingRadius
    {
        get => _baseScoutingRadius + SkillEffects.ScoutingRadius(SkillLevelOf(BuiltInSkills.Scouting));
        internal set => _baseScoutingRadius = value;
    }

    /// <summary>Accumulated experience; each <see cref="ExperienceTable"/> level brings a skill choice.</summary>
    public int Experience { get; internal set; }

    /// <summary>Level-ups earned but not chosen yet.</summary>
    public int PendingLevelUps => Math.Max(0, ExperienceTable.LevelFor(Experience) - Level);

    /// <summary>
    /// Builds a hero for a new game or the editor: the roster entry's identity, stats and starting skills, then whatever the
    /// map sets itself (name, stats, skills) on top.
    /// </summary>
    public static Hero Create(
        HeroId id, PlayerId owner, MapPosition position, Army? army, HeroDefinition? definition,
        string? name = null, int? attack = null, int? defense = null, IReadOnlyDictionary<string, SkillLevel>? skills = null,
        int morale = 0, int luck = 0, int? spellPower = null, int? knowledge = null,
        IEnumerable<Battles.WarMachineSlot>? warMachines = null,
        IEnumerable<string>? artifacts = null, IEnumerable<string>? backpack = null, ArtifactCatalog? catalog = null)
    {
        if (spellPower < 0 || knowledge < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spellPower), "Spell Power and Knowledge cannot be negative.");
        }

        if (Math.Abs(morale) > Battles.MoraleAndLuck.Limit || Math.Abs(luck) > Battles.MoraleAndLuck.Limit)
        {
            throw new ArgumentOutOfRangeException(nameof(morale), $"Morale and luck go from −{Battles.MoraleAndLuck.Limit} to +{Battles.MoraleAndLuck.Limit}.");
        }

        var hero = new Hero(id, name ?? definition?.Name ?? $"Hero {id.Value}", owner, position, army);
        if (definition is not null)
        {
            hero.Apply(definition);
        }

        hero.Name = name ?? hero.Name;
        hero.Attack = attack ?? hero.Attack;
        hero.Defense = defense ?? hero.Defense;
        hero.SpellPower = spellPower ?? hero.SpellPower;
        hero.Knowledge = knowledge ?? hero.Knowledge;
        if (warMachines is not null)
        {
            hero.SetWarMachines(warMachines);
        }

        if (artifacts is not null || backpack is not null)
        {
            hero.Artifacts.ReplaceWith(artifacts ?? [], backpack ?? [], catalog ?? ArtifactCatalog.Default);
        }

        hero.Morale = morale;
        hero.Luck = luck;
        if (skills is not null)
        {
            hero.Skills.ReplaceWith(skills);
        }

        return hero;
    }

    /// <summary>
    /// Takes over a roster entry: identity, class, preferred creature, specialty, and its starting stats and skills. A mage of a magic town starts
    /// with a spell book (ADR-071; Primal Fury mages have no spells, ADR-072) holding its specialty spell, if it has one (assumed).
    /// </summary>
    internal void Apply(HeroDefinition definition)
    {
        Definition = definition.Id;
        Name = definition.Name;
        Class = definition.Class;
        PreferredCreature = definition.PreferredCreature;
        Specialty = definition.Specialty;
        Attack = definition.Attack;
        Defense = definition.Defense;
        SpellPower = definition.SpellPower;
        Knowledge = definition.Knowledge;
        Skills.ReplaceWith(definition.StartingSkills);
        HasSpellBook = definition.Class == HeroClass.Mage && !BuiltInSkills.IsPrimalFuryTown(definition.Town);
        _spells.Clear();
        if (HasSpellBook && definition.Specialty is { Kind: SpecialtyKind.Spell, Target: var spell })
        {
            Learn(spell);
        }
    }

    private static HeroSkills HeroSkillsWith(SkillLevel tactics)
    {
        var skills = new HeroSkills();
        skills.Set(BuiltInSkills.Tactics, tactics);
        return skills;
    }
}
