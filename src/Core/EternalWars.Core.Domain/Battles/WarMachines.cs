using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Battles;

/// <summary>The three war machines a hero can own (ADR-056).</summary>
public enum WarMachineSlot
{
    /// <summary>The town's offensive machine: ballista, cannon, catapult or glaivethrower.</summary>
    Offensive,

    /// <summary>Healing tent.</summary>
    Tent,

    /// <summary>Ammo (supply) cart.</summary>
    Cart,
}

public enum WarMachineKind
{
    Ballista,
    Cannon,
    Catapult,
    Glaivethrower,
    HealingTent,
    AmmoCart,
}

/// <summary>
/// War machines (ADR-056, draft numbers). Every hero owns its town's offensive machine; the healing tent and the ammo
/// cart are bought at a blacksmith. In battle they are units behind their side's lines: they never move or fight in
/// melee, do not retaliate, have no morale, and are lost when destroyed. The offensive machine shoots on its own turn
/// with unlimited shots, but outside sieges only from Advanced Ballistics; the tent heals every own stack at the start of
/// each round; the cart adds shots to every own shooter. Their stats are code, not the creature table, so they never
/// appear as guards or recruits.
/// </summary>
public static class WarMachines
{
    public const string IdPrefix = "machine.";

    private static readonly Dictionary<WarMachineKind, CreatureDefinition> Definitions = new()
    {
        [WarMachineKind.Ballista] = Machine(WarMachineKind.Ballista, "Ballista", CreatureSize.Medium, attack: 10, defense: 10, damage: (40, 55), health: 250, initiative: 10),
        [WarMachineKind.Cannon] = Machine(WarMachineKind.Cannon, "Cannon", CreatureSize.Medium, attack: 12, defense: 8, damage: (55, 75), health: 220, initiative: 7),
        [WarMachineKind.Catapult] = Machine(WarMachineKind.Catapult, "Catapult", CreatureSize.Medium, attack: 10, defense: 10, damage: (45, 70), health: 260, initiative: 8),
        [WarMachineKind.Glaivethrower] = Machine(WarMachineKind.Glaivethrower, "Glaivethrower", CreatureSize.Medium, attack: 11, defense: 9, damage: (25, 35), health: 200, initiative: 12,
            new CreatureCapability(CapabilityKind.ShootsTwice)),
        [WarMachineKind.HealingTent] = Machine(WarMachineKind.HealingTent, "Healing Tent", CreatureSize.Small, attack: 0, defense: 5, damage: (1, 1), health: 150, initiative: 1),
        [WarMachineKind.AmmoCart] = Machine(WarMachineKind.AmmoCart, "Ammo Cart", CreatureSize.Small, attack: 0, defense: 5, damage: (1, 1), health: 200, initiative: 1),
    };

    /// <summary>Offensive machine per town, paired by theme (user decision); towns not listed (and custom heroes) use the ballista.</summary>
    public static IReadOnlyDictionary<string, WarMachineKind> OffensiveByTown { get; } = new Dictionary<string, WarMachineKind>
    {
        [TownIds.Haven] = WarMachineKind.Ballista,
        [TownIds.Necropolis] = WarMachineKind.Ballista,
        [TownIds.Academy] = WarMachineKind.Cannon,
        [TownIds.Inferno] = WarMachineKind.Cannon,
        [TownIds.Stronghold] = WarMachineKind.Catapult,
        [TownIds.SwampBestiary] = WarMachineKind.Catapult,
        [TownIds.Sylvan] = WarMachineKind.Glaivethrower,
        [TownIds.Dungeon] = WarMachineKind.Glaivethrower,
    };

    /// <summary>What a new hero owns: its offensive machine.</summary>
    public static IReadOnlySet<WarMachineSlot> StartingSet { get; } = new HashSet<WarMachineSlot> { WarMachineSlot.Offensive };

    public static CreatureDefinition Definition(WarMachineKind kind) => Definitions[kind];

    public static IEnumerable<CreatureDefinition> All => Definitions.Values;

    /// <summary>A machine's definition by creature id (battle views and casualty lists), if the id is a machine's.</summary>
    public static bool TryGet(string id, out CreatureDefinition definition)
    {
        definition = Definitions.Values.FirstOrDefault(d => d.Id == id)!;
        return definition is not null;
    }

    public static WarMachineKind OffensiveFor(string? town) =>
        town is not null && OffensiveByTown.TryGetValue(town, out var kind) ? kind : WarMachineKind.Ballista;

    public static WarMachineKind KindOf(WarMachineSlot slot, string? town) => slot switch
    {
        WarMachineSlot.Tent => WarMachineKind.HealingTent,
        WarMachineSlot.Cart => WarMachineKind.AmmoCart,
        _ => OffensiveFor(town),
    };

    public static WarMachineSlot SlotOf(WarMachineKind kind) => kind switch
    {
        WarMachineKind.HealingTent => WarMachineSlot.Tent,
        WarMachineKind.AmmoCart => WarMachineSlot.Cart,
        _ => WarMachineSlot.Offensive,
    };

    /// <summary>
    /// Battlefield footprint (ADR-073): every machine occupies one column, the outermost of its side; the offensive machine and the tent
    /// are 1 × 3 cells, the ammo cart 1 × 2. The model may overhang the grid's edge.
    /// </summary>
    public static (int Width, int Height) Footprint(WarMachineKind kind) => kind == WarMachineKind.AmmoCart ? (1, 2) : (1, 3);

    /// <summary>Tent and cart work without taking turns; only the offensive machine acts.</summary>
    public static bool TakesTurns(WarMachineKind kind) => SlotOf(kind) == WarMachineSlot.Offensive;

    /// <summary>Blacksmith prices (HoMM3's).</summary>
    public static ResourceSet Cost(WarMachineSlot slot) => slot switch
    {
        WarMachineSlot.Tent => new ResourceSet(Gold: 750),
        WarMachineSlot.Cart => new ResourceSet(Gold: 1000),
        _ => new ResourceSet(Gold: 2500),
    };

    /// <summary>Outside sieges the offensive machine fights only from Advanced Ballistics (user decision).</summary>
    public static bool FieldsOffensive(SkillLevel ballistics) => ballistics >= SkillLevel.Advanced;

    // Sieges (ADR-063, placeholder numbers; there are no fortifications in battle yet) ------------------------------

    /// <summary>Hit points of a defence structure of a fully upgraded fortification.</summary>
    public const int MaxFortificationHealth = 3;

    /// <summary>Without Ballistics the offensive machine fires by itself, and only at fortifications.</summary>
    public static bool FiresAutomatically(SkillLevel ballistics) => ballistics == SkillLevel.None;

    /// <summary>From Basic the player controls the machine (still only against fortifications until Advanced).</summary>
    public static bool IsControlled(SkillLevel ballistics) => ballistics >= SkillLevel.Basic;

    /// <summary>From Advanced the machine may also target enemy troops.</summary>
    public static bool CanTargetTroops(SkillLevel ballistics) => ballistics >= SkillLevel.Advanced;

    /// <summary>From Advanced a shot that destroys a structure is followed by a second one, at an automatically chosen target.</summary>
    public static bool FiresAgainAfterKill(SkillLevel ballistics) => ballistics >= SkillLevel.Advanced;

    /// <summary>Expert: every shot at a fortification hits; otherwise 2 of 3 do.</summary>
    public static bool HitsFortification(SkillLevel ballistics, Common.GameRandom random) =>
        ballistics >= SkillLevel.Expert || random.Next(3) < 2;

    /// <summary>Hit points a hit takes off a defence structure: 1–2, 2–3 at Master.</summary>
    public static int FortificationDamage(SkillLevel ballistics, Common.GameRandom random) =>
        (ballistics >= SkillLevel.Master ? 2 : 1) + random.Next(2);

    /// <summary>Ballistics: +25 % (Expert) / +50 % (Master) offensive machine damage.</summary>
    public static int DamageBonusPercent(SkillLevel ballistics) => ballistics switch
    {
        SkillLevel.Expert => 25,
        SkillLevel.Master => 50,
        _ => 0,
    };

    /// <summary>Health the tent restores to the top creature of every own stack each round: 10 + 10 per Healing level.</summary>
    public static int TentHealing(SkillLevel healing) => 10 + (10 * (int)healing);

    /// <summary>Extra shots from the cart at battle start: +25 % (no skill), +50 % (Basic Ammo supply); null = unlimited (Advanced).</summary>
    public static int? CartShotBonusPercent(SkillLevel ammoSupply) => ammoSupply switch
    {
        SkillLevel.None => 25,
        SkillLevel.Basic => 50,
        _ => null,
    };

    private static CreatureDefinition Machine(
        WarMachineKind kind, string name, CreatureSize size, int attack, int defense, (int Min, int Max) damage, int health, int initiative,
        params CreatureCapability[] capabilities) =>
        new($"{IdPrefix}{kind.ToString().ToLowerInvariant()}", name, 3, size)
        {
            Attack = attack,
            Defense = defense,
            MinDamage = damage.Min,
            MaxDamage = damage.Max,
            Health = health,
            Speed = 1,
            MapSpeed = 1,
            Initiative = initiative,
            Shots = TakesTurns(kind) ? 1 : 0,
            Tags = ["war-machine"],
            Capabilities = capabilities,
        };
}
