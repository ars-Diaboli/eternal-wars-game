using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// The registry of map object kinds (ADR-166): which <see cref="MapObject"/> subclass holds each <see cref="MapObjectKind"/> and how a fresh one is
/// made. Every other table keyed by kind (the map format's codecs, the visit rules) is checked against it by tests, so a new kind is one
/// registration here plus the places those tests name.
/// </summary>
public static class MapObjectKinds
{
    /// <summary>A kind, its instance type, and a factory for an instance with default per-instance values.</summary>
    public sealed record Entry(MapObjectKind Kind, Type Type, Func<MapObjectId, MapObjectDefinition, MapPosition, MapObject> Create);

    private static readonly Dictionary<MapObjectKind, Entry> Entries = new Entry[]
    {
        Of(MapObjectKind.ResourcePile, (id, d, p) => new ResourcePile(id, d, p)),
        Of(MapObjectKind.Mine, (id, d, p) => new Mine(id, d, p)),
        Of(MapObjectKind.TreasureChest, (id, d, p) => new TreasureChest(id, d, p)),
        Of(MapObjectKind.TwoWayGate, (id, d, p) => new TwoWayGate(id, d, p)),
        Of(MapObjectKind.OneWayGateEntrance, (id, d, p) => new OneWayGateEntrance(id, d, p)),
        Of(MapObjectKind.OneWayGateExit, (id, d, p) => new OneWayGateExit(id, d, p)),
        Of(MapObjectKind.Monster, (id, d, p) => new MonsterGuard(id, d, p)),
        Of(MapObjectKind.Town, (id, d, p) => new Towns.Town(id, d, p)),
        Of(MapObjectKind.Artifact, (id, d, p) => new ArtifactPickup(id, d, p)),
        Of(MapObjectKind.Dwelling, (id, d, p) => new CreatureDwelling(id, d, p)),
        Of(MapObjectKind.Marketplace, (id, d, p) => new Marketplace(id, d, p)),
        Of(MapObjectKind.ResourceProvider, (id, d, p) => new ResourceProvider(id, d, p)),
        Of(MapObjectKind.Loot, (id, d, p) => new LootSite(id, d, p)),
        Of(MapObjectKind.Buff, (id, d, p) => new BuffSite(id, d, p)),
        Of(MapObjectKind.Training, (id, d, p) => new TrainingSite(id, d, p)),
        Of(MapObjectKind.Sign, (id, d, p) => new SignPost(id, d, p)),
        Of(MapObjectKind.Observatory, (id, d, p) => new Observatory(id, d, p)),
        Of(MapObjectKind.HillFort, (id, d, p) => new HillFort(id, d, p)),
        Of(MapObjectKind.TownGate, (id, d, p) => new TownGate(id, d, p)),
        Of(MapObjectKind.WarMachineFactory, (id, d, p) => new WarMachineFactory(id, d, p)),
        Of(MapObjectKind.Prison, (id, d, p) => new Prison(id, d, p)),
        Of(MapObjectKind.CreatureBank, (id, d, p) => new CreatureBank(id, d, p)),
        Of(MapObjectKind.DenOfThieves, (id, d, p) => new DenOfThieves(id, d, p)),
        Of(MapObjectKind.AstrologersTower, (id, d, p) => new AstrologersTower(id, d, p)),
        Of(MapObjectKind.Sanctuary, (id, d, p) => new Sanctuary(id, d, p)),
        Of(MapObjectKind.Scenery, (id, d, p) => new Scenery(id, d, p)),
        Of(MapObjectKind.Cartographer, (id, d, p) => new Cartographer(id, d, p)),
        Of(MapObjectKind.ArtifactMerchant, (id, d, p) => new ArtifactMerchant(id, d, p)),
        Of(MapObjectKind.RefugeeCamp, (id, d, p) => new RefugeeCamp(id, d, p)),
        Of(MapObjectKind.SacrificialAltar, (id, d, p) => new SacrificialAltar(id, d, p)),
        Of(MapObjectKind.Sphinx, (id, d, p) => new Sphinx(id, d, p)),
        Of(MapObjectKind.WitchHut, (id, d, p) => new WitchHut(id, d, p)),
        Of(MapObjectKind.Shrine, (id, d, p) => new Shrine(id, d, p)),
        Of(MapObjectKind.SkillSchool, (id, d, p) => new SkillSchool(id, d, p)),
        Of(MapObjectKind.Obelisk, (id, d, p) => new Obelisk(id, d, p)),
        Of(MapObjectKind.CombatPlace, (id, d, p) => new CombatPlace(id, d, p)),
        Of(MapObjectKind.Deposit, (id, d, p) => new Deposit(id, d, p)),
        Of(MapObjectKind.MagiWindow, (id, d, p) => new MagiWindow(id, d, p)),
        Of(MapObjectKind.MagiEye, (id, d, p) => new MagiEye(id, d, p)),
        Of(MapObjectKind.KeymasterTent, (id, d, p) => new KeymasterTent(id, d, p)),
        Of(MapObjectKind.BorderPost, (id, d, p) => new BorderPost(id, d, p)),
        Of(MapObjectKind.MapTavern, (id, d, p) => new MapTavern(id, d, p)),
        Of(MapObjectKind.Garrison, (id, d, p) => new MapGarrison(id, d, p)),
        Of(MapObjectKind.PandorasBox, (id, d, p) => new PandorasBox(id, d, p)),
        Of(MapObjectKind.SeerHut, (id, d, p) => new QuestSite(id, d, p)),
        Of(MapObjectKind.QuestGuard, (id, d, p) => new QuestSite(id, d, p)),
        Of(MapObjectKind.QuestGate, (id, d, p) => new QuestSite(id, d, p)),
    }.ToDictionary(e => e.Kind);

    /// <summary>Every kind, in <see cref="MapObjectKind"/> order.</summary>
    public static IReadOnlyList<Entry> All { get; } = [.. Entries.Values.OrderBy(e => e.Kind)];

    /// <summary>Every instance type (several kinds may share one: the quest objects).</summary>
    public static IReadOnlyList<Type> Types { get; } = [.. All.Select(e => e.Type).Distinct()];

    public static Entry Get(MapObjectKind kind) =>
        Entries.TryGetValue(kind, out var entry) ? entry : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unregistered map object kind.");

    private static Entry Of<T>(MapObjectKind kind, Func<MapObjectId, MapObjectDefinition, MapPosition, T> create)
        where T : MapObject => new(kind, typeof(T), create);
}
