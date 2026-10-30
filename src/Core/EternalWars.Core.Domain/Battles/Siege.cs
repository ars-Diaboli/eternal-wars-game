using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Battles;

/// <summary>A part of a town's fortifications in a siege (ADR-099, ADR-106).</summary>
public enum SiegeStructureKind
{
    /// <summary>One of the four wall sections; a destroyed one is a gap the attackers walk through.</summary>
    Wall,

    /// <summary>The gate: the defenders pass, the attackers break it.</summary>
    Gate,

    /// <summary>The keep (Citadel): shoots once a round, double damage.</summary>
    Keep,

    /// <summary>One of the two towers (Castle): shoots once a round.</summary>
    Tower,
}

/// <summary>
/// One structure of a siege. Wall sections and the gate stand on the wall line of the field; the keep and the towers stand off the grid (behind
/// the defenders' edge and at the two ends of the wall line), so they block nothing and only the attacker's offensive machine reaches them.
/// </summary>
public sealed class SiegeStructure
{
    internal SiegeStructure(int id, SiegeStructureKind kind, CellRect rect, int health)
    {
        Id = id;
        Kind = kind;
        Rect = rect;
        MaxHealth = health;
        Health = health;
    }

    public int Id { get; }

    public SiegeStructureKind Kind { get; }

    public CellRect Rect { get; }

    public int MaxHealth { get; }

    public int Health { get; internal set; }

    public bool IsStanding => Health > 0;

    /// <summary>Keep and towers shoot at the attackers (ADR-106).</summary>
    public bool Shoots => Kind is SiegeStructureKind.Keep or SiegeStructureKind.Tower;

    /// <summary>Wall sections and the gate stand on the field and block it while they stand.</summary>
    public bool OnField => Kind is SiegeStructureKind.Wall or SiegeStructureKind.Gate;

    internal SiegeStructure Copy() => new(Id, Kind, Rect, MaxHealth) { Health = Health };

    public override string ToString() => $"{Kind} #{Id} {Health}/{MaxHealth}";
}

/// <summary>What a siege is fought at: the town's fort level (1 Fort, 2 Citadel, 3 Castle) and its building count (ADR-106).</summary>
public sealed record SiegeSpec(int FortLevel, int Buildings);

/// <summary>The fortifications of a siege battle (<see cref="BattleState.Siege"/>).</summary>
public sealed class SiegeState
{
    private readonly List<SiegeStructure> _structures;

    internal SiegeState(SiegeSpec spec, IEnumerable<SiegeStructure> structures, IReadOnlyList<CellRect> moat)
    {
        Spec = spec;
        _structures = [.. structures];
        Moat = moat;
    }

    public SiegeSpec Spec { get; }

    public int FortLevel => Spec.FortLevel;

    public IReadOnlyList<SiegeStructure> Structures => _structures;

    /// <summary>The moat's cells in front of the walls (Citadel and Castle); the gate's rows are bridged.</summary>
    public IReadOnlyList<CellRect> Moat { get; }

    /// <summary>A tower's shot (ADR-106, placeholder): 10 + 2 per building in the town; the keep's is double.</summary>
    public int ShotDamage(SiegeStructure structure) =>
        (structure.Kind == SiegeStructureKind.Keep ? 2 : 1) * (SiegeRules.TowerBaseDamage + (SiegeRules.TowerDamagePerBuilding * Spec.Buildings));

    /// <summary>What entering the moat does to a stack: about two tower shots, 2 × (10 + 2 per building) (ADR-106, placeholder).</summary>
    public int MoatDamage => 2 * (SiegeRules.TowerBaseDamage + (SiegeRules.TowerDamagePerBuilding * Spec.Buildings));

    public SiegeStructure? Find(int id) => _structures.FirstOrDefault(s => s.Id == id);

    /// <summary>Whether a standing wall section or gate blocks the cell for a unit of <paramref name="side"/>: the defenders pass the gate.</summary>
    public bool Blocks(CellPosition cell, BattleSide side) =>
        _structures.Any(s => s.IsStanding && s.OnField && (s.Kind == SiegeStructureKind.Wall || side == BattleSide.Attacker) && s.Rect.Contains(cell));

    /// <summary>Whether the footprint touches the moat.</summary>
    public bool InMoat(CellRect rect) => Moat.Any(m => m.Intersects(rect));

    /// <summary>
    /// Where the offensive machine fires by itself (ADR-106): the wall sections, the gate once all four sections are down, then the keep and the
    /// towers (the order after the gate is assumed); null when nothing stands.
    /// </summary>
    public SiegeStructure? AutoTarget()
    {
        var standing = _structures.Where(s => s.IsStanding).ToList();
        return standing.FirstOrDefault(s => s.Kind == SiegeStructureKind.Wall)
               ?? standing.FirstOrDefault(s => s.Kind == SiegeStructureKind.Gate)
               ?? standing.FirstOrDefault(s => s.Kind == SiegeStructureKind.Keep)
               ?? standing.FirstOrDefault(s => s.Kind == SiegeStructureKind.Tower);
    }

    internal SiegeState Copy() => new(Spec, _structures.Select(s => s.Copy()), Moat);
}

/// <summary>
/// Siege rules (ADR-099, ADR-106; numbers are placeholders). Every battle at a town with a Fort is a siege on the 36-wide field: four wall sections
/// and a gate (Fort), the keep and a moat (Citadel), two towers and stronger walls (Castle). There is no free siege catapult: the attacker's
/// offensive machine is the siege engine (<see cref="WarMachines"/>' Ballistics rules). Shots of attacking shooters across a standing wall take the
/// wall's 1/2 cover; the attackers stop in the moat and take its damage.
/// </summary>
public static class SiegeRules
{
    /// <summary>The wall line's column on the 36-wide field: 24 columns for the attackers, 11 behind the walls for the defenders.</summary>
    public const int WallX = BattleState.SiegeWidth - 12;

    /// <summary>The moat is this many columns wide, right in front of the walls.</summary>
    public const int MoatColumns = 2;

    /// <summary>The gate's rows: 4 cells, so that a 4 × 4 creature passes.</summary>
    public const int GateY = 10;

    public const int GateHeight = 4;

    public const int GateHealth = 1;

    /// <summary>Keep and towers have 3 hit points each.</summary>
    public const int TowerHealth = 3;

    public const int TowerBaseDamage = 10;

    public const int TowerDamagePerBuilding = 2;

    /// <summary>A wall section's hit points: 2 (Fort, Citadel), 3 (Castle).</summary>
    public static int WallHealth(int fortLevel) => fortLevel >= 3 ? 3 : 2;

    /// <summary>A town is besieged when it has a Fort (ADR-106); null for a town without one.</summary>
    public static SiegeSpec? SpecOf(Town town) =>
        town.Level(BuiltInBuildings.Fort) is > 0 and var level ? new SiegeSpec(Math.Min(3, level), Buildings(town)) : null;

    /// <summary>
    /// The town's buildings for the towers' damage: every built level of every building and dwelling counts as one (an upgrade is a building of its
    /// own, as in HoMM3; assumed).
    /// </summary>
    public static int Buildings(Town town) => town.Buildings.Values.Sum() + town.Dwellings.Values.Sum();

    /// <summary>The fortifications for <paramref name="spec"/> on the siege field.</summary>
    public static SiegeState Create(SiegeSpec spec)
    {
        var wall = WallHealth(spec.FortLevel);
        var height = BattleState.Height;
        var lower = GateY + GateHeight;
        var structures = new List<SiegeStructure>
        {
            new(1, SiegeStructureKind.Wall, new CellRect(WallX, 0, 1, GateY / 2), wall),
            new(2, SiegeStructureKind.Wall, new CellRect(WallX, GateY / 2, 1, GateY - (GateY / 2)), wall),
            new(3, SiegeStructureKind.Wall, new CellRect(WallX, lower, 1, (height - lower) / 2), wall),
            new(4, SiegeStructureKind.Wall, new CellRect(WallX, lower + ((height - lower) / 2), 1, height - lower - ((height - lower) / 2)), wall),
            new(5, SiegeStructureKind.Gate, new CellRect(WallX, GateY, 1, GateHeight), GateHealth),
        };
        var moat = new List<CellRect>();
        if (spec.FortLevel >= 2)
        {
            structures.Add(new(6, SiegeStructureKind.Keep, new CellRect(BattleState.SiegeWidth, GateY, 2, GateHeight), TowerHealth));
            moat.Add(new CellRect(WallX - MoatColumns, 0, MoatColumns, GateY));
            moat.Add(new CellRect(WallX - MoatColumns, lower, MoatColumns, height - lower));
        }

        if (spec.FortLevel >= 3)
        {
            structures.Add(new(7, SiegeStructureKind.Tower, new CellRect(WallX, -2, 1, 2), TowerHealth));
            structures.Add(new(8, SiegeStructureKind.Tower, new CellRect(WallX, height, 1, 2), TowerHealth));
        }

        return new SiegeState(spec, structures, moat);
    }

    /// <summary>The attacker's offensive machine in a siege.</summary>
    public static bool IsSiegeEngine(BattleState battle, BattleUnit unit) =>
        battle.Siege is not null && unit.Side == BattleSide.Attacker && unit.Machine is { } kind && WarMachines.SlotOf(kind) == WarMachineSlot.Offensive;

    /// <summary>Without Ballistics the siege engine fires by itself, whoever commands its side (ADR-063, ADR-106).</summary>
    public static bool FiresByItself(BattleState battle, BattleUnit unit) =>
        IsSiegeEngine(battle, unit) && WarMachines.FiresAutomatically(battle.Participant(unit.Side).Bonus.Ballistics);

    /// <summary>Why the active unit may not fire at the structure, or null.</summary>
    public static string? ShotRefusal(BattleState battle, BattleUnit unit, int structure)
    {
        if (!IsSiegeEngine(battle, unit))
        {
            return "Only the attacker's offensive machine fires at the fortifications.";
        }

        return battle.Siege!.Find(structure) is { IsStanding: true } ? null : "That structure is not standing.";
    }

    /// <summary>Whether a unit of <paramref name="side"/> stops in the moat and takes its damage on entering it (the attackers; assumed).</summary>
    public static bool FearsMoat(BattleState battle, BattleSide side) => battle.Siege is { Moat.Count: > 0 } && side == BattleSide.Attacker;
}
