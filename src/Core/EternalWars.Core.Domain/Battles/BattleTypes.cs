using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Battles;

public enum BattleSide
{
    Attacker,
    Defender,
}

public static class BattleSideExtensions
{
    public static BattleSide Opponent(this BattleSide side) =>
        side == BattleSide.Attacker ? BattleSide.Defender : BattleSide.Attacker;
}

/// <summary>A cell of the battlefield grid; (0,0) is the top-left, Y grows downwards. Attackers deploy on the left.</summary>
public readonly record struct CellPosition(int X, int Y)
{
    public CellPosition Offset(int dx, int dy) => new(X + dx, Y + dy);

    public override string ToString() => $"[{X},{Y}]";
}

/// <summary>A square or rectangular block of cells: a unit footprint, an obstacle, an area of effect.</summary>
public readonly record struct CellRect(int X, int Y, int Width, int Height)
{
    public CellRect(CellPosition topLeft, int size)
        : this(topLeft.X, topLeft.Y, size, size)
    {
    }

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(CellPosition cell) => cell.X >= X && cell.X < Right && cell.Y >= Y && cell.Y < Bottom;

    public bool Intersects(CellRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    /// <summary>Touching edge or corner without overlapping: the 8-way melee adjacency of footprints.</summary>
    public bool IsAdjacentTo(CellRect other) => !Intersects(other) && Grow(1).Intersects(other);

    public CellRect Grow(int by) => new(X - by, Y - by, Width + (2 * by), Height + (2 * by));

    /// <summary>Chebyshev gap between two footprints in cells (0 = touching or overlapping).</summary>
    public int GapTo(CellRect other)
    {
        var dx = Math.Max(0, Math.Max(other.X - Right, X - other.Right));
        var dy = Math.Max(0, Math.Max(other.Y - Bottom, Y - other.Bottom));
        return Math.Max(dx, dy);
    }

    /// <summary>
    /// Straight-line gap between two footprints in thousandths of a cell (<see cref="BattleGrid.Unit"/>; 0 = touching or overlapping): the
    /// cells between them across and down, measured like movement (CMB-9).
    /// </summary>
    public int DistanceTo(CellRect other)
    {
        var dx = Math.Max(0, Math.Max(other.X - Right, X - other.Right));
        var dy = Math.Max(0, Math.Max(other.Y - Bottom, Y - other.Bottom));
        return BattleGrid.Length(dx, dy);
    }

    /// <summary>Centre in doubled coordinates, so it is always integral.</summary>
    public (int X2, int Y2) Center2 => ((2 * X) + Width, (2 * Y) + Height);

    public IEnumerable<CellPosition> Cells()
    {
        for (var y = Y; y < Bottom; y++)
        {
            for (var x = X; x < Right; x++)
            {
                yield return new CellPosition(x, y);
            }
        }
    }
}

/// <summary>Cover class of a battlefield obstacle (ADR-035).</summary>
public enum ObstacleClass
{
    /// <summary>2×2 cells, 1/8 cover.</summary>
    Small,

    /// <summary>3×3 cells, 1/4 cover.</summary>
    Big,

    /// <summary>City wall (sieges, <see cref="SiegeStructure"/>), 1/2 cover.</summary>
    Wall,

    /// <summary>A long cliff that splits the field partly (ADR-146): <see cref="BattleObstacle.CliffThickness"/> wide, 6–12 cells long, 1/4 cover like a crag.</summary>
    Cliff,
}

public static class ObstacleClassExtensions
{
    public static int CoverEighths(this ObstacleClass cover) => cover switch
    {
        ObstacleClass.Small => 1,
        ObstacleClass.Big or ObstacleClass.Cliff => 2,
        _ => 4,
    };

    public static int Cells(this ObstacleClass cover) => cover switch
    {
        ObstacleClass.Small => 2,
        ObstacleClass.Big => 3,
        _ => 1,
    };
}

/// <summary>An obstacle on the field; a <see cref="ObstacleClass.Cliff"/> runs <paramref name="Length"/> cells down the field.</summary>
public sealed record BattleObstacle(int Id, CellPosition Position, ObstacleClass Class, int Length = 0)
{
    /// <summary>How many columns wide a cliff is.</summary>
    public const int CliffThickness = 2;

    public CellRect Rect => Class == ObstacleClass.Cliff
        ? new(Position.X, Position.Y, CliffThickness, Length)
        : new(Position, Class.Cells());
}

/// <summary>
/// One side of a battle: a hero's army or a neutral guard. Hero stats are copied at the start so the battle
/// is self-contained.
/// </summary>
/// <param name="Side">Attacker (deploys left) or defender.</param>
/// <param name="Owner">Player commanding the side; null for neutral guards.</param>
/// <param name="Hero">Leading hero, if any.</param>
/// <param name="Guard">Map object whose guard fights (monster or guarded mine), if any.</param>
/// <param name="Attack">Hero Attack added to every stack (0 for guards).</param>
/// <param name="Defense">Hero Defense added to every stack.</param>
/// <param name="Tactics">Hero Tactics level (width choice).</param>
/// <param name="IsComputer">Commanded by the battle AI (neutrals, computer players, quick combat).</param>
/// <param name="Bonuses">Hero skill and preferred-creature bonuses (none for guards).</param>
/// <param name="ArmyMorale">Morale the whole army gets from its make-up and where it fights: mixed factions, a town's Brotherhood (ADR-075).</param>
/// <param name="Caster">The hero's spell book, Spell Power and mana (ADR-110); null for a side whose hero has no book, or no hero.</param>
public sealed record BattleParticipant(
    BattleSide Side,
    PlayerId? Owner,
    HeroId? Hero,
    MapObjectId? Guard,
    int Attack,
    int Defense,
    Heroes.SkillLevel Tactics,
    bool IsComputer,
    HeroBattleBonuses? Bonuses = null,
    int ArmyMorale = 0,
    BattleCaster? Caster = null)
{
    public HeroBattleBonuses Bonus => Bonuses ?? HeroBattleBonuses.None;
}

/// <summary>A spell the side's hero knows, at its rank, with what it costs this hero (attunement taken off, ADR-063).</summary>
public sealed record KnownSpell(Magic.SpellDefinition Spell, int Rank, int Cost)
{
    /// <summary>The spell's battle effect at this rank (ADR-122); null when it cannot be cast in battle.</summary>
    public Magic.SpellEffect? Effect => Spell.EffectAt(Rank);
}

/// <summary>
/// What a hero brings to cast in battle (ADR-110), copied when the battle starts: Spell Power, level (spell specialties grow with it), the
/// spells of its book (and those its artifacts teach while worn, ADR-111), its specialty spell, its mana, and the percent its elemental orbs add to
/// spells of their element.
/// </summary>
public sealed record BattleCaster(
    int SpellPower, int Level, IReadOnlyList<KnownSpell> Spells, string? Specialty, int Mana, IReadOnlyDictionary<Magic.SpellElement, int>? ElementPercent = null)
{
    public KnownSpell? Find(string spell) => Spells.FirstOrDefault(s => s.Spell.Id == spell);

    /// <summary>Mana in <see cref="Mana"/> lent for this battle only (a siege's Knowledge, ADR-138): spent first, the rest gone afterwards.</summary>
    public int LentMana { get; init; }

    /// <summary>
    /// The percent the hero's artifacts (the elemental orbs) add to the effect of the spell's element; for Magic Arrow the best of the four, for
    /// Bind Elemental that of the bound kind's element (ADR-123).
    /// </summary>
    public int ElementBonus(Magic.SpellDefinition spell, string? creature = null) =>
        ElementPercent is { Count: > 0 } percent ? spell.ElementsFor(creature).Select(e => percent.GetValueOrDefault(e)).DefaultIfEmpty(0).Max() : 0;

    public static BattleCaster? For(Heroes.Hero hero, Magic.SpellCatalog spells)
    {
        if (!hero.HasSpellBook)
        {
            return null;
        }

        var known = Magic.MagicRules.Known(hero, spells)
            .Select(s => new KnownSpell(s.Spell, s.Rank, Heroes.ManaRules.SpellCost(hero, s.Spell.Mana)))
            .ToList();
        var specialty = hero.Specialty is { Kind: Heroes.SpecialtyKind.Spell } special ? special.Target : null;
        var elements = new[] { Magic.SpellElement.Air, Magic.SpellElement.Earth, Magic.SpellElement.Fire, Magic.SpellElement.Water }.Where(e => hero.Bonus.ElementBonus(e) != 0).ToDictionary(e => e, hero.Bonus.ElementBonus);
        return new BattleCaster(hero.TotalSpellPower, hero.Level, known, specialty, hero.Mana, elements);
    }
}

/// <summary>
/// What a hero adds to its side in battle (ADR-048): skill percentages (Melee, Archery, Armor), War Tempo speed, and the flat
/// bonus of its preferred creature line (<see cref="Heroes.SkillEffects"/>), its morale and luck (ADR-053), and its war
/// machine skills (ADR-056), Alertness' initiative (flat, ADR-069), Marksmanship and the Primal Fury skills (ADR-067).
/// <see cref="EnemySpellPowerPenalty"/> and <see cref="EnemySpellCostPenalty"/> (Interference) weaken the enemy hero's spells, and Spell resistance cuts hostile spells' damage and duration (ADR-110).
/// Artifacts (ADR-111) make the army immune to spells with some tags, forbid retreat and surrender to both sides, and cap the spell level both sides may cast.
/// </summary>
public sealed record HeroBattleBonuses(
    int MeleePercent, int RangedPercent, int DamageTakenReductionPercent, int SpeedBonus, IReadOnlySet<string> Preferred, int Morale = 0, int Luck = 0,
    Heroes.SkillLevel Ballistics = Heroes.SkillLevel.None, Heroes.SkillLevel Healing = Heroes.SkillLevel.None,
    Heroes.SkillLevel AmmoSupply = Heroes.SkillLevel.None, int RangedDamageTakenReductionPercent = 0,
    int Initiative = 0, Heroes.SkillLevel BloodRage = Heroes.SkillLevel.None, Heroes.SkillLevel Warcry = Heroes.SkillLevel.None,
    int EnemySpellPowerPenalty = 0, int EnemySpellCostPenalty = 0, int MarksmanshipEighths = 0, int SpellResistancePercent = 0,
    IReadOnlySet<string>? Immunities = null, bool NoRetreat = false, int? SpellLevelCap = null)
{
    public static HeroBattleBonuses None { get; } = new(0, 0, 0, 0, new HashSet<string>());

    public bool Prefers(Creatures.CreatureDefinition creature) => Preferred.Contains(creature.Id);

    /// <summary>Whether the army is immune to a spell with any of these tags.</summary>
    public bool IsImmuneTo(IEnumerable<string> tags) => Immunities is { Count: > 0 } immune && tags.Any(immune.Contains);

    public static HeroBattleBonuses For(Heroes.Hero hero, Creatures.CreatureCatalog creatures)
    {
        var preferred = new HashSet<string>(StringComparer.Ordinal);
        if (hero.PreferredCreature is { } line && creatures.TryGet(line, out var creature))
        {
            preferred.Add(creature.Id);
            if (creature.UpgradesTo is { } upgrade)
            {
                preferred.Add(upgrade);
            }
        }

        var artifacts = hero.Bonus;
        return new(
            Heroes.SkillEffects.WithSpecialty(hero, Heroes.BuiltInSkills.Melee, Heroes.SkillEffects.MeleeDamagePercent(hero.SkillLevelOf(Heroes.BuiltInSkills.Melee))) + artifacts.MeleePercent,
            Heroes.SkillEffects.WithSpecialty(hero, Heroes.BuiltInSkills.Archery, Heroes.SkillEffects.RangedDamagePercent(hero.SkillLevelOf(Heroes.BuiltInSkills.Archery))) + artifacts.RangedPercent,
            Math.Min(90, Heroes.SkillEffects.WithSpecialty(hero, Heroes.BuiltInSkills.Armor, Heroes.SkillEffects.DamageTakenReductionPercent(hero.SkillLevelOf(Heroes.BuiltInSkills.Armor))) + artifacts.DamageTakenReductionPercent),
            Heroes.SkillEffects.WithSpecialty(hero, Heroes.BuiltInSkills.WarTempo, Heroes.SkillEffects.BattleSpeedBonus(hero.SkillLevelOf(Heroes.BuiltInSkills.WarTempo))) + artifacts.BattleSpeed,
            preferred,
            hero.TotalMorale,
            hero.TotalLuck,
            hero.SkillLevelOf(Heroes.BuiltInSkills.Ballistics),
            hero.SkillLevelOf(Heroes.BuiltInSkills.Healing),
            hero.SkillLevelOf(Heroes.BuiltInSkills.AmmoSupply),
            artifacts.RangedDamageTakenReductionPercent,
            Heroes.SkillEffects.AlertnessInitiative(hero.SkillLevelOf(Heroes.BuiltInSkills.Alertness)),
            hero.SkillLevelOf(Heroes.BuiltInSkills.BloodRage),
            PrimalFury.WarcryLevel(hero),
            PrimalFury.SpellPowerPenalty(hero.SkillLevelOf(Heroes.BuiltInSkills.Interference)),
            PrimalFury.SpellCostPenalty(hero.SkillLevelOf(Heroes.BuiltInSkills.Interference)),
            Heroes.SkillEffects.MarksmanshipEighths(hero.SkillLevelOf(Heroes.BuiltInSkills.Marksmanship)),
            Heroes.SkillEffects.SpellResistancePercent(hero.SkillLevelOf(Heroes.BuiltInSkills.SpellResistance)),
            artifacts.Immunities,
            artifacts.NoRetreat,
            artifacts.SpellLevelCap)
        {
            BloodRagePercent = Heroes.SkillEffects.WithSpecialty(hero, Heroes.BuiltInSkills.BloodRage, 100),
            WarcryPercent = Heroes.SkillEffects.WithSpecialty(hero, Heroes.BuiltInSkills.Warcry, 100),
            TacticsColumns = Heroes.SkillEffects.TacticsSpecialtyColumns(hero),
        };
    }

    /// <summary>The strength of Blood Rage in percent (100; a Blood Rage specialty +5 % a hero level): it scales the Attack cap.</summary>
    public int BloodRagePercent { get; init; } = 100;

    /// <summary>Blood Rage: the most Attack a stack gains from kills (<see cref="PrimalFury.BloodRageMaxAttack"/> × <see cref="BloodRagePercent"/>, rounded down).</summary>
    public int BloodRageMaxAttack => PrimalFury.BloodRageMaxAttack(BloodRage) * BloodRagePercent / 100;

    /// <summary>The strength of the hero's warcries in percent (100; a Warcry specialty +5 % a hero level): Intimidate's initiative cut and Terror's chance.</summary>
    public int WarcryPercent { get; init; } = 100;

    /// <summary>Deployment columns a Tactics specialist adds (<see cref="Heroes.SkillEffects.TacticsSpecialtyColumns"/>).</summary>
    public int TacticsColumns { get; init; }
}

public enum BattlePhase
{
    /// <summary>The side with effective Tactics picks the battlefield width.</summary>
    ChoosingWidth,

    /// <summary>The side with the higher Tactics places its stacks in its deployment zone before the first round (ADR-072).</summary>
    Deploying,
    Fighting,
    Finished,
}

public enum BattleEndReason
{
    Defeated,
    Retreated,

    /// <summary>The side's hero paid the other side to leave with its army (ADR-081).</summary>
    Surrendered,
}

public enum BattleAttackKind
{
    Melee,
    Ranged,
    Retaliation,

    /// <summary>A shooter firing back at a shooter (ranged retaliation).</summary>
    RangedRetaliation,

    /// <summary>A secondary target of breath, cleave or multi-head.</summary>
    Area,
}

/// <summary>Where a siege's damage to a stack came from (ADR-106).</summary>
public enum SiegeDamageSource
{
    /// <summary>A shot of the keep or a tower.</summary>
    Tower,

    /// <summary>Entering the moat.</summary>
    Moat,
}
