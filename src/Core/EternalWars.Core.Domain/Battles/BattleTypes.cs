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
public sealed record BattleParticipant(
    BattleSide Side,
    PlayerId? Owner,
    HeroId? Hero,
    MapObjectId? Guard,
    int Attack,
    int Defense,
    Heroes.SkillLevel Tactics,
    bool IsComputer,
    HeroBattleBonuses? Bonuses = null)
{
    public HeroBattleBonuses Bonus => Bonuses ?? HeroBattleBonuses.None;
}

/// <summary>
/// What a hero adds to its side in battle (ADR-048): skill percentages (Melee, Archery, Armor), Drill speed, and the flat
/// bonus of its preferred creature line (<see cref="Heroes.SkillEffects"/>).
/// </summary>
public sealed record HeroBattleBonuses(int MeleePercent, int RangedPercent, int DamageTakenReductionPercent, int SpeedBonus, IReadOnlySet<string> Preferred)
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

        var skills = hero.Skills;
        return new(
            Heroes.SkillEffects.MeleeDamagePercent(skills.Level(Heroes.BuiltInSkills.Melee)),
            Heroes.SkillEffects.RangedDamagePercent(skills.Level(Heroes.BuiltInSkills.Archery)),
            Heroes.SkillEffects.DamageTakenReductionPercent(skills.Level(Heroes.BuiltInSkills.Armor)),
            Heroes.SkillEffects.BattleSpeedBonus(skills.Level(Heroes.BuiltInSkills.Drill)),
            preferred);
    }
}

public enum BattlePhase
{
    /// <summary>The side with effective Tactics picks the battlefield width.</summary>
    ChoosingWidth,
    Fighting,
    Finished,
}

public enum BattleEndReason
{
    Defeated,
    Retreated,
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
