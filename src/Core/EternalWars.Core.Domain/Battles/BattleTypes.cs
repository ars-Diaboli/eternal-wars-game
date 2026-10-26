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

    /// <summary>City wall segment (sieges, later), 1/2 cover.</summary>
    Wall,
}

public static class ObstacleClassExtensions
{
    public static int CoverEighths(this ObstacleClass cover) => cover switch
    {
        ObstacleClass.Small => 1,
        ObstacleClass.Big => 2,
        _ => 4,
    };

    public static int Cells(this ObstacleClass cover) => cover switch
    {
        ObstacleClass.Small => 2,
        ObstacleClass.Big => 3,
        _ => 1,
    };
}

public sealed record BattleObstacle(int Id, CellPosition Position, ObstacleClass Class)
{
    public CellRect Rect => new(Position, Class.Cells());
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
    int ArmyMorale = 0)
{
    public HeroBattleBonuses Bonus => Bonuses ?? HeroBattleBonuses.None;
}

/// <summary>
/// What a hero adds to its side in battle (ADR-048): skill percentages (Melee, Archery, Armor), War Tempo speed, and the flat
/// bonus of its preferred creature line (<see cref="Heroes.SkillEffects"/>), its morale and luck (ADR-053), and its war
/// machine skills (ADR-056), Alertness' initiative (flat, ADR-069), Marksmanship and the Primal Fury skills (ADR-067).
/// <see cref="EnemySpellPowerPenalty"/> and <see cref="EnemySpellCostPenalty"/> (Interference) wait for casting (Phase 5).
/// </summary>
public sealed record HeroBattleBonuses(
    int MeleePercent, int RangedPercent, int DamageTakenReductionPercent, int SpeedBonus, IReadOnlySet<string> Preferred, int Morale = 0, int Luck = 0,
    Heroes.SkillLevel Ballistics = Heroes.SkillLevel.None, Heroes.SkillLevel Healing = Heroes.SkillLevel.None,
    Heroes.SkillLevel AmmoSupply = Heroes.SkillLevel.None, int RangedDamageTakenReductionPercent = 0,
    int Initiative = 0, Heroes.SkillLevel BloodRage = Heroes.SkillLevel.None, Heroes.SkillLevel Warcry = Heroes.SkillLevel.None,
    int EnemySpellPowerPenalty = 0, int EnemySpellCostPenalty = 0, int MarksmanshipEighths = 0)
{
    public static HeroBattleBonuses None { get; } = new(0, 0, 0, 0, new HashSet<string>());

    public bool Prefers(Creatures.CreatureDefinition creature) => Preferred.Contains(creature.Id);

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
            Heroes.SkillEffects.MeleeDamagePercent(hero.SkillLevelOf(Heroes.BuiltInSkills.Melee)) + artifacts.MeleePercent,
            Heroes.SkillEffects.RangedDamagePercent(hero.SkillLevelOf(Heroes.BuiltInSkills.Archery)) + artifacts.RangedPercent,
            Math.Min(90, Heroes.SkillEffects.DamageTakenReductionPercent(hero.SkillLevelOf(Heroes.BuiltInSkills.Armor)) + artifacts.DamageTakenReductionPercent),
            Heroes.SkillEffects.BattleSpeedBonus(hero.SkillLevelOf(Heroes.BuiltInSkills.WarTempo)) + artifacts.BattleSpeed,
            preferred,
            hero.TotalMorale,
            hero.TotalLuck,
            hero.SkillLevelOf(Heroes.BuiltInSkills.Ballistics),
            hero.SkillLevelOf(Heroes.BuiltInSkills.Healing),
            hero.SkillLevelOf(Heroes.BuiltInSkills.AmmoSupply),
            artifacts.RangedDamageTakenReductionPercent,
            Heroes.SkillEffects.AlertnessInitiative(hero.SkillLevelOf(Heroes.BuiltInSkills.Alertness)),
            hero.SkillLevelOf(Heroes.BuiltInSkills.BloodRage),
            hero.SkillLevelOf(Heroes.BuiltInSkills.Warcry),
            PrimalFury.SpellPowerPenalty(hero.SkillLevelOf(Heroes.BuiltInSkills.Interference)),
            PrimalFury.SpellCostPenalty(hero.SkillLevelOf(Heroes.BuiltInSkills.Interference)),
            Heroes.SkillEffects.MarksmanshipEighths(hero.SkillLevelOf(Heroes.BuiltInSkills.Marksmanship)));
    }
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
