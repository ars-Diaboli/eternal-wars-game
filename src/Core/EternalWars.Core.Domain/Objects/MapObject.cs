using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

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

    /// <summary>
    /// Whether the object is a pickup (ADR-065): a pickup next to Classic monsters can be taken from outside their zone, and only Area stacks
    /// defend it.
    /// </summary>
    public virtual bool IsPickup => false;

    /// <summary>
    /// Whether stepping onto the entrance means a fight for a hero of <paramref name="visitor"/> (it then needs an army): a guard of its own, a
    /// garrison, a defended town.
    /// </summary>
    public virtual bool IsDefendedAgainst(GameState state, PlayerId visitor) => false;

    /// <summary>
    /// The object's own state for <see cref="GameStateDigest"/>; every kind says it, "" for none, so new state cannot be left out by mistake.
    /// The text is part of recorded digests: changing it breaks older replays.
    /// </summary>
    internal abstract string DigestState();

    /// <summary>Creates an instance of the right subclass with default per-instance values (<see cref="MapObjectKinds"/>).</summary>
    public static MapObject Create(MapObjectId id, MapObjectDefinition definition, MapPosition position) =>
        MapObjectKinds.Get(definition.Kind).Create(id, definition, position);
}

/// <summary>An object a player can own (mines, towns): it flies the owner's flag and watches its surroundings for them.</summary>
public interface IFlaggable
{
    PlayerId? Owner { get; }

    /// <summary>How far around its entrance (a town: its middle) the object reveals and watches for its owner.</summary>
    int ScoutingRadius { get; }
}

/// <summary>
/// Picked up on visit: resources go to the hero's owner and the pile disappears. An amount the map leaves open (null) is rolled in
/// <see cref="AmountRange"/> when the game starts (ADR-068).
/// </summary>
public sealed class ResourcePile(MapObjectId id, MapObjectDefinition definition, MapPosition position, int? amount = null)
    : MapObject(id, definition, position, MapObjectKind.ResourcePile)
{
    /// <inheritdoc/>
    internal override string DigestState() => $"amount {Amount}";

    /// <inheritdoc/>
    public override bool IsPickup => true;

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
    : MapObject(id, definition, position, MapObjectKind.Mine), IFlaggable, IBattleGuard
{
    /// <inheritdoc/>
    void IBattleGuard.KeepSurvivors(BattleGuardOutcome outcome)
    {
        if (Guard is { } stack)
        {
            Guard = stack with { Count = outcome.Survivors };
        }

        outcome.Events.Publish(new MapObjectChanged(Id));
    }

    /// <inheritdoc/>
    /// <remarks>The beaten guard leaves the mine (an abandoned one becomes a normal mine), and the mine is the winner's.</remarks>
    void IBattleGuard.Defeated(BattleGuardOutcome outcome, Hero? winner)
    {
        Guard = null;
        var mine = ClearAbandoned(outcome);
        outcome.Events.Publish(new MineGuardDefeated(mine.Id));
        if (winner is not null && mine.Owner != winner.Owner)
        {
            var previous = mine.Owner;
            mine.Owner = winner.Owner;
            outcome.Events.Publish(new MineOwnerChanged(mine.Id, previous, winner.Owner));
        }
    }

    /// <summary>
    /// A beaten abandoned mine becomes the normal mine of its resource (ADR-068): same id, place and owner, the definition it
    /// <see cref="MapObjectDefinition.Becomes"/>. Views redraw it on <see cref="MapObjectChanged"/>.
    /// </summary>
    private Mine ClearAbandoned(BattleGuardOutcome outcome)
    {
        if (Definition.Becomes is not { } becomes || !outcome.State.ObjectCatalog.TryGet(becomes, out var normal))
        {
            return this;
        }

        var cleared = new Mine(Id, normal, Position, Owner);
        outcome.State.RemoveObject(this);
        outcome.State.AddObject(cleared);
        outcome.Events.Publish(new MapObjectChanged(cleared.Id));
        return cleared;
    }

    /// <inheritdoc/>
    internal override string DigestState() => $"owner {Owner?.Value} guard {Guard}";

    /// <inheritdoc/>
    public override bool IsDefendedAgainst(GameState state, PlayerId visitor) => Guard is not null;

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
    /// <inheritdoc/>
    internal override string DigestState() => $"fixed {FixedValues}";

    /// <inheritdoc/>
    public override bool IsPickup => true;

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
    /// <inheritdoc/>
    internal override string DigestState() => $"artifact {Artifact}";

    /// <inheritdoc/>
    public override bool IsPickup => true;

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
    : MapObject(id, definition, position, MapObjectKind.Dwelling), IFlaggable, IBattleGuard
{
    /// <inheritdoc/>
    void IBattleGuard.KeepSurvivors(BattleGuardOutcome outcome)
    {
        if (Guard is { } stack)
        {
            Guard = stack with { Count = outcome.Survivors };
        }

        outcome.Events.Publish(new MapObjectChanged(Id));
    }

    /// <inheritdoc/>
    /// <remarks>The beaten guard leaves the dwelling, and the dwelling is the winner's.</remarks>
    void IBattleGuard.Defeated(BattleGuardOutcome outcome, Hero? winner)
    {
        Guard = null;
        outcome.Events.Publish(new MapObjectChanged(Id));
        if (winner is not null && Owner != winner.Owner)
        {
            var before = Owner;
            Owner = winner.Owner;
            outcome.Events.Publish(new DwellingOwnerChanged(Id, before, winner.Owner));
        }
    }

    /// <inheritdoc/>
    internal override string DigestState() => $"{Creature} owner {Owner?.Value} available {Available}" + (Guard is { } g ? $" guard {g}" : "");

    /// <inheritdoc/>
    public override bool IsDefendedAgainst(GameState state, PlayerId visitor) => Guard is not null;

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
    : MapObject(id, definition, position, MapObjectKind.Marketplace)
{
    /// <inheritdoc/>
    internal override string DigestState() => "";
}

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
    GuardMode mode = GuardMode.Classic,
    int? randomTier = null)
    : MapObject(id, definition, position, MapObjectKind.Monster), IBattleGuard
{
    /// <inheritdoc/>
    void IBattleGuard.KeepSurvivors(BattleGuardOutcome outcome)
    {
        Count = outcome.Survivors;
        outcome.Events.Publish(new MapObjectChanged(Id));
    }

    /// <inheritdoc/>
    /// <remarks>A beaten monster leaves the map.</remarks>
    void IBattleGuard.Defeated(BattleGuardOutcome outcome, Hero? winner)
    {
        outcome.State.RemoveObject(this);
        outcome.Events.Publish(new MapObjectRemoved(Id));
    }

    /// <inheritdoc/>
    internal override string DigestState() => $"{Creature}:{Count} {Disposition}";

    public const string DefaultCreature = Creatures.BuiltInCreatures.Wolf;

    public string Creature { get; internal set; } = creature;

    /// <summary>Null until rolled at game start from the creature's tier range.</summary>
    public int? Count { get; internal set; } = count;

    public GuardDisposition Disposition { get; internal set; } = disposition;

    public bool NeverFlees { get; internal set; } = neverFlees;

    /// <summary>Whether pickups next to this stack can be taken from outside (Classic) or start the fight too (Area).</summary>
    public GuardMode Mode { get; internal set; } = mode;

    /// <summary>
    /// A random monster (ADR-126): 0 = any tier, 1–7 = that tier; <see cref="Creature"/> is drawn when the game starts
    /// (<see cref="Game.PlaceholderRules"/>) and until then only shows it. Null = the creature as placed.
    /// </summary>
    public int? RandomTier { get; internal set; } = randomTier;

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
    /// <inheritdoc/>
    internal override string DigestState() => LinkedGate?.ToString() ?? "";

    public MapObjectId? LinkedGate { get; internal set; } = linkedGate;

    public override MapObjectId? TeleportTarget => LinkedGate;
}

/// <summary>Teleports the visiting hero to <see cref="Exit"/>; there is no way back through it.</summary>
public sealed class OneWayGateEntrance(MapObjectId id, MapObjectDefinition definition, MapPosition position, MapObjectId? exit = null)
    : MapObject(id, definition, position, MapObjectKind.OneWayGateEntrance)
{
    /// <inheritdoc/>
    internal override string DigestState() => Exit?.ToString() ?? "";

    public MapObjectId? Exit { get; internal set; } = exit;

    public override MapObjectId? TeleportTarget => Exit;
}

/// <summary>Where one-way gates deliver heroes. Visiting it does nothing, so routes may pass over it.</summary>
public sealed class OneWayGateExit(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.OneWayGateExit)
{
    /// <inheritdoc/>
    internal override string DigestState() => "";

    public override bool EndsRoute => false;
}
