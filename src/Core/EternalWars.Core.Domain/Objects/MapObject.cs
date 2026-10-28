using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// An object placed on the adventure map. <see cref="Position"/> is the top-left tile of the
/// definition's <see cref="Footprint"/>; heroes interact by stepping onto <see cref="VisitPosition"/>.
/// One subclass per <see cref="MapObjectKind"/> holds the mutable, per-instance state.
/// </summary>
public abstract class MapObject
{
    protected MapObject(MapObjectId id, MapObjectDefinition definition, MapPosition position, MapObjectKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Kind != expectedKind)
        {
            throw new ArgumentException(
                $"Definition '{definition.Id}' is a {definition.Kind}, expected {expectedKind}.", nameof(definition));
        }

        Id = id;
        Definition = definition;
        Position = position;
    }

    public MapObjectId Id { get; }

    public MapObjectDefinition Definition { get; }

    public MapObjectKind Kind => Definition.Kind;

    public MapPosition Position { get; }

    public MapPosition VisitPosition => Position.Offset(Definition.Footprint.VisitOffset.Dx, Definition.Footprint.VisitOffset.Dy);

    public IEnumerable<(MapPosition Position, FootprintCell Cell)> OccupiedCells() =>
        Definition.Footprint.OccupiedCells.Select(c => (Position.Offset(c.Dx, c.Dy), c.Cell));

    /// <summary>
    /// Whether stepping onto the entrance ends a route (ADR-013: visiting is mandatory). Objects whose visit
    /// does nothing (one-way gate exits) can be walked through.
    /// </summary>
    public virtual bool EndsRoute => true;

    /// <summary>The object whose entrance a visit teleports the hero to, if this is a usable gate.</summary>
    public virtual MapObjectId? TeleportTarget => null;

    /// <summary>Who collected the object's reward and when, for its <see cref="MapObjectDefinition.VisitScope"/> (ADR-068).</summary>
    public VisitLog Visits { get; } = new();

    /// <summary>Whether <paramref name="hero"/> may collect the reward now (the visit itself always happens).</summary>
    public bool CanCollect(HeroId hero, GameDate date) => Visits.CanCollect(Definition.VisitScope, hero, date);

    public override string ToString() => $"{Definition.Name} {Id} at {Position}";

    /// <summary>Creates an instance of the right subclass with default per-instance values.</summary>
    public static MapObject Create(MapObjectId id, MapObjectDefinition definition, MapPosition position) => definition.Kind switch
    {
        MapObjectKind.ResourcePile => new ResourcePile(id, definition, position),
        MapObjectKind.Mine => new Mine(id, definition, position),
        MapObjectKind.TreasureChest => new TreasureChest(id, definition, position),
        MapObjectKind.TwoWayGate => new TwoWayGate(id, definition, position),
        MapObjectKind.OneWayGateEntrance => new OneWayGateEntrance(id, definition, position),
        MapObjectKind.OneWayGateExit => new OneWayGateExit(id, definition, position),
        MapObjectKind.Monster => new MonsterGuard(id, definition, position),
        MapObjectKind.Town => new Towns.Town(id, definition, position),
        MapObjectKind.Artifact => new ArtifactPickup(id, definition, position),
        MapObjectKind.Dwelling => new CreatureDwelling(id, definition, position),
        MapObjectKind.Marketplace => new Marketplace(id, definition, position),
        MapObjectKind.ResourceProvider => new ResourceProvider(id, definition, position),
        MapObjectKind.Loot => new LootSite(id, definition, position),
        MapObjectKind.Buff => new BuffSite(id, definition, position),
        MapObjectKind.Training => new TrainingSite(id, definition, position),
        MapObjectKind.Sign => new SignPost(id, definition, position),
        MapObjectKind.Observatory => new Observatory(id, definition, position),
        MapObjectKind.HillFort => new HillFort(id, definition, position),
        MapObjectKind.TownGate => new TownGate(id, definition, position),
        MapObjectKind.WarMachineFactory => new WarMachineFactory(id, definition, position),
        MapObjectKind.Prison => new Prison(id, definition, position),
        MapObjectKind.CreatureBank => new CreatureBank(id, definition, position),
        MapObjectKind.DenOfThieves => new DenOfThieves(id, definition, position),
        MapObjectKind.AstrologersTower => new AstrologersTower(id, definition, position),
        MapObjectKind.Sanctuary => new Sanctuary(id, definition, position),
        MapObjectKind.Scenery => new Scenery(id, definition, position),
        MapObjectKind.Cartographer => new Cartographer(id, definition, position),
        MapObjectKind.ArtifactMerchant => new ArtifactMerchant(id, definition, position),
        MapObjectKind.RefugeeCamp => new RefugeeCamp(id, definition, position),
        MapObjectKind.SacrificialAltar => new SacrificialAltar(id, definition, position),
        MapObjectKind.Sphinx => new Sphinx(id, definition, position),
        MapObjectKind.WitchHut => new WitchHut(id, definition, position),
        MapObjectKind.Deposit => new Deposit(id, definition, position),
        MapObjectKind.MagiWindow => new MagiWindow(id, definition, position),
        MapObjectKind.MagiEye => new MagiEye(id, definition, position),
        MapObjectKind.KeymasterTent => new KeymasterTent(id, definition, position),
        MapObjectKind.BorderPost => new BorderPost(id, definition, position),
        MapObjectKind.MapTavern => new MapTavern(id, definition, position),
        MapObjectKind.Garrison => new MapGarrison(id, definition, position),
        _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.Kind, null),
    };
}

/// <summary>An object a player can own (mines, towns): it flies the owner's flag and watches its surroundings for them.</summary>
public interface IFlaggable
{
    PlayerId? Owner { get; }

    /// <summary>How far around its entrance the object reveals and watches for its owner.</summary>
    int ScoutingRadius { get; }
}

/// <summary>
/// Picked up on visit: resources go to the hero's owner and the pile disappears. An amount the map leaves open (null) is rolled in
/// <see cref="AmountRange"/> when the game starts (ADR-068).
/// </summary>
public sealed class ResourcePile(MapObjectId id, MapObjectDefinition definition, MapPosition position, int? amount = null)
    : MapObject(id, definition, position, MapObjectKind.ResourcePile)
{
    public ResourceType Resource => Definition.Resource!.Value;

    /// <summary>The fixed amount; null until the game starts when the map leaves it open.</summary>
    public int? Amount { get; internal set; } = amount;

    /// <summary>The range an open amount is rolled in: 5–10 lumber or ore, 3–7 of a magic resource, 500–1000 gold in steps of <see cref="GoldStep"/>.</summary>
    public static (int Min, int Max) AmountRange(ResourceType resource) => resource switch
    {
        ResourceType.Gold => (500, 1000),
        ResourceType.Lumber or ResourceType.Ore => (5, 10),
        _ => (3, 7),
    };

    public const int GoldStep = 100;

    /// <summary>Rolls an open amount from <see cref="AmountRange"/>.</summary>
    internal static int Roll(ResourceType resource, GameRandom random)
    {
        var (min, max) = AmountRange(resource);
        return resource == ResourceType.Gold ? random.NextInclusive(min / GoldStep, max / GoldStep) * GoldStep : random.NextInclusive(min, max);
    }
}

/// <summary>
/// Flagged by the visiting hero's owner; produces <see cref="DailyYield"/> for its owner every turn.
/// An optional <see cref="Guard"/> must be beaten before the first flagging (ADR-034); afterwards any hero
/// re-flags the mine by visiting it.
/// </summary>
public sealed class Mine(MapObjectId id, MapObjectDefinition definition, MapPosition position, PlayerId? owner = null, GuardStack? guard = null)
    : MapObject(id, definition, position, MapObjectKind.Mine), IFlaggable
{
    /// <summary>Owned mines reveal and watch a small area around them.</summary>
    public const int MineScoutingRadius = 2;

    public int ScoutingRadius => MineScoutingRadius;

    public ResourceType Resource => Definition.Resource!.Value;

    public int DailyYield => Definition.DailyYield;

    public PlayerId? Owner { get; internal set; } = owner;

    public GuardStack? Guard { get; internal set; } = guard;
}

/// <summary>A guarding creature stack; a null <see cref="Count"/> is rolled from the tier range at game start.</summary>
public sealed record GuardStack(string Creature, int? Count = null);

/// <summary>Gold or experience a chest offers.</summary>
public sealed record ChestValues(int Gold, int Experience);

/// <summary>
/// On visit the hero chooses between gold and experience; then the chest disappears. The contents are rolled when the chest is
/// opened (<see cref="Odds"/>, ADR-068): a gold-or-experience tier, or now and then a Minor artifact, unless the map author fixed the values.
/// </summary>
public sealed class TreasureChest(MapObjectId id, MapObjectDefinition definition, MapPosition position, ChestValues? fixedValues = null)
    : MapObject(id, definition, position, MapObjectKind.TreasureChest)
{
    /// <summary>The owner's table (ADR-068, replacing ADR-039's three equal tiers): weight and values; null values = a Minor artifact instead.</summary>
    public static IReadOnlyList<(int Weight, ChestValues? Values)> Odds { get; } =
        [(30, new(1000, 500)), (30, new(1500, 1000)), (25, new(2000, 1500)), (10, new(2500, 2000)), (5, null)];

    /// <summary>The gold-or-experience tiers of <see cref="Odds"/>.</summary>
    public static IReadOnlyList<ChestValues> RollTable { get; } = [.. Odds.Select(o => o.Values).OfType<ChestValues>()];

    /// <summary>Values chosen by the map author; null = rolled with the game RNG on opening.</summary>
    public ChestValues? FixedValues { get; internal set; } = fixedValues;
}

/// <summary>
/// An artifact lying on the map (ADR-061). A hero that steps on it puts it in its backpack (nothing happens when that is
/// full). <see cref="Artifact"/> null is rolled from the definition's tier when the game starts.
/// </summary>
public sealed class ArtifactPickup(MapObjectId id, MapObjectDefinition definition, MapPosition position, string? artifact = null)
    : MapObject(id, definition, position, MapObjectKind.Artifact)
{
    public string? Artifact { get; internal set; } = artifact;

    public Artifacts.ArtifactTier Tier => Definition.ArtifactTier ?? Artifacts.ArtifactTier.Minor;
}

/// <summary>How many base creatures guard a neutral map dwelling of a tier when the game starts (ADR-068): tiers 1 and 2 are unguarded.</summary>
public static class DwellingGuards
{
    public static int? Count(int tier) => tier switch
    {
        3 => 9,
        4 => 6,
        5 => 3,
        _ => null,
    };
}

/// <summary>
/// A dwelling of one creature line on the adventure map (ADR-054). It grows by the creature's weekly growth every Monday,
/// owned or not; a hero standing on its entrance can recruit from it, and visiting flags it for the visitor's owner, whose
/// towns with a caravan may then hire from it. <see cref="Available"/> null = one week's growth, filled when the game starts.
/// </summary>
public sealed class CreatureDwelling(
    MapObjectId id, MapObjectDefinition definition, MapPosition position,
    string creature = MonsterGuard.DefaultCreature, PlayerId? owner = null, int? available = null)
    : MapObject(id, definition, position, MapObjectKind.Dwelling), IFlaggable
{
    /// <summary>Owned dwellings reveal and watch a small area around them, like mines.</summary>
    public const int DwellingScoutingRadius = 2;

    public int ScoutingRadius => DwellingScoutingRadius;

    /// <summary>Id of the creature recruited here (the base creature of its line).</summary>
    public string Creature { get; internal set; } = creature;

    public PlayerId? Owner { get; internal set; } = owner;

    public int? Available { get; internal set; } = available;

    /// <summary>
    /// The neutral guard that must be beaten before the first flagging (ADR-068): set when the game starts for an unowned dwelling of tier 3–5
    /// (<see cref="DwellingGuards"/>), gone once beaten. Not map data.
    /// </summary>
    public GuardStack? Guard { get; internal set; }
}

/// <summary>
/// A marketplace on the adventure map (ADR-067). It has no owner and no per-instance state: a hero on its entrance lets its
/// owner trade at a fixed rate (<see cref="Economy.MarketplaceRules.MapRate"/>), whatever towns the player has.
/// </summary>
public sealed class Marketplace(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Marketplace);

/// <summary>How a neutral stack reacts to a hero (HoMM3 dispositions, deterministic thresholds, ADR-040).</summary>
public enum GuardDisposition
{
    Compliant,
    Friendly,
    Aggressive,
    Hostile,
    Savage,
}

/// <summary>How a neutral stack guards the eight tiles around it; set per stack (ADR-040, ADR-065).</summary>
public enum GuardMode
{
    /// <summary>HoMM3, the general-purpose default: entering a tile next to the monster starts the fight, but pickups there can be taken from outside.</summary>
    Classic,

    /// <summary>For treasure guards: the whole 3×3 area is guarded; any interaction inside it, pickups included, starts the fight.</summary>
    Area,
}

/// <summary>
/// A neutral creature stack on the adventure map. Its tile is the entrance (attack it by stepping on it) and it
/// guards the eight tiles around it as its own <see cref="Mode"/> says (ADR-065).
/// </summary>
public sealed class MonsterGuard(
    MapObjectId id,
    MapObjectDefinition definition,
    MapPosition position,
    string creature = MonsterGuard.DefaultCreature,
    int? count = null,
    GuardDisposition disposition = GuardDisposition.Aggressive,
    bool neverFlees = false,
    GuardMode mode = GuardMode.Classic)
    : MapObject(id, definition, position, MapObjectKind.Monster)
{
    public const string DefaultCreature = Creatures.BuiltInCreatures.Wolf;

    public string Creature { get; internal set; } = creature;

    /// <summary>Null until rolled at game start from the creature's tier range.</summary>
    public int? Count { get; internal set; } = count;

    public GuardDisposition Disposition { get; internal set; } = disposition;

    public bool NeverFlees { get; internal set; } = neverFlees;

    /// <summary>Whether pickups next to this stack can be taken from outside (Classic) or start the fight too (Area).</summary>
    public GuardMode Mode { get; internal set; } = mode;

    /// <summary>Tiles guarded besides its own (the 8 neighbours on the same level).</summary>
    public IEnumerable<MapPosition> GuardedTiles()
    {
        foreach (var direction in DirectionExtensions.All)
        {
            yield return Position.Step(direction);
        }
    }
}

/// <summary>
/// One end of a linked pair: teleports the visiting hero to the entrance of <see cref="LinkedGate"/>.
/// Subterranean gates (across levels) and two-way monoliths (same level) are definitions of this kind.
/// </summary>
public sealed class TwoWayGate(MapObjectId id, MapObjectDefinition definition, MapPosition position, MapObjectId? linkedGate = null)
    : MapObject(id, definition, position, MapObjectKind.TwoWayGate)
{
    public MapObjectId? LinkedGate { get; internal set; } = linkedGate;

    public override MapObjectId? TeleportTarget => LinkedGate;
}

/// <summary>Teleports the visiting hero to <see cref="Exit"/>; there is no way back through it.</summary>
public sealed class OneWayGateEntrance(MapObjectId id, MapObjectDefinition definition, MapPosition position, MapObjectId? exit = null)
    : MapObject(id, definition, position, MapObjectKind.OneWayGateEntrance)
{
    public MapObjectId? Exit { get; internal set; } = exit;

    public override MapObjectId? TeleportTarget => Exit;
}

/// <summary>Where one-way gates deliver heroes. Visiting it does nothing, so routes may pass over it.</summary>
public sealed class OneWayGateExit(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.OneWayGateExit)
{
    public override bool EndsRoute => false;
}
