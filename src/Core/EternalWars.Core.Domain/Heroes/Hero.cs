using EternalWars.Core.Domain.Armies;
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

    /// <summary>Primary stat added to every stack's Attack in battle. Levels and stat growth arrive in Phase 3.</summary>
    public int Attack { get; internal set; } = attack;

    /// <summary>Primary stat added to every stack's Defense in battle.</summary>
    public int Defense { get; internal set; } = defense;

    /// <summary>Skill levels by skill id (ADR-048).</summary>
    public HeroSkills Skills { get; } = HeroSkillsWith(tactics);

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

    /// <summary>Today's allowance, recomputed at the start of the owner's turn (<see cref="MovementAllowance"/>).</summary>
    public int MaxMovementPoints { get; internal set; } = MovementAllowance.NoArmyBase;

    public int MovementPoints { get; internal set; } = MovementAllowance.NoArmyBase;

    /// <summary>Percentage movement bonus from skills (e.g. Logistics +10/20/30).</summary>
    public int MovementBonusPercent { get; internal set; }

    /// <summary>Flat movement bonus from artifacts.</summary>
    public int MovementBonusFlat { get; internal set; }

    /// <summary>Tiles revealed around the hero, in tiles (rounded circle).</summary>
    public int ScoutingRadius { get; internal set; } = DefaultScoutingRadius;

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
        string? name = null, int? attack = null, int? defense = null, IReadOnlyDictionary<string, SkillLevel>? skills = null)
    {
        var hero = new Hero(id, name ?? definition?.Name ?? $"Hero {id.Value}", owner, position, army);
        if (definition is not null)
        {
            hero.Apply(definition);
        }

        hero.Name = name ?? hero.Name;
        hero.Attack = attack ?? hero.Attack;
        hero.Defense = defense ?? hero.Defense;
        if (skills is not null)
        {
            hero.Skills.ReplaceWith(skills);
        }

        return hero;
    }

    /// <summary>Takes over a roster entry: identity, class, preferred creature, specialty, and its starting stats and skills.</summary>
    internal void Apply(HeroDefinition definition)
    {
        Definition = definition.Id;
        Name = definition.Name;
        Class = definition.Class;
        PreferredCreature = definition.PreferredCreature;
        Specialty = definition.Specialty;
        Attack = definition.Attack;
        Defense = definition.Defense;
        Skills.ReplaceWith(definition.StartingSkills);
    }

    private static HeroSkills HeroSkillsWith(SkillLevel tactics)
    {
        var skills = new HeroSkills();
        skills.Set(BuiltInSkills.Tactics, tactics);
        return skills;
    }
}
