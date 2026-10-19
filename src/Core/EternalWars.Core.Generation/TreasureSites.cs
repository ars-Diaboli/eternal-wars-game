using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Generation;

/// <summary>
/// One adventure-map object the treasure stage may place (ADR-068): its treasure <paramref name="Value"/> (what it counts for in a value band and for
/// its guard), how often it is drawn among the objects that fit (<paramref name="Weight"/>), how many a zone may hold (<paramref name="ZoneLimit"/>) and
/// the map (<paramref name="MapLimit"/>, none when null). With <paramref name="OwnGuard"/> it fights for itself (creature banks, abandoned mines), so
/// it gets no guard next to it.
/// </summary>
public sealed record TreasureSite(string Definition, int Value, int Weight = 2, int ZoneLimit = 1, int? MapLimit = null, bool OwnGuard = false);

/// <summary>
/// The objects of the catalogue (ADR-068) the generator places as treasure, with placeholder values to tune. An item of a value band whose drawn
/// value is <c>v</c> may be any site worth between <c>v / 2</c> and <c>v</c>. Signs (no text to give) and map dwellings (placed by the zone rules
/// later) are not here.
/// </summary>
public static class TreasureSites
{
    public static IReadOnlyList<TreasureSite> All { get; } =
    [
        // One-time loot.
        new(BuiltInMapObjects.Campfire, 1200, Weight: 3, ZoneLimit: 2),
        new(BuiltInMapObjects.SupplyCrates, 1500, Weight: 3, ZoneLimit: 2),
        new(BuiltInMapObjects.TravelersWagon, 1500, ZoneLimit: 2),
        new(BuiltInMapObjects.Corpse, 800, ZoneLimit: 2),
        new(BuiltInMapObjects.LeanTo, 600, ZoneLimit: 2),
        new(BuiltInMapObjects.WarriorsTomb, 4000),

        // Resource providers.
        new(BuiltInMapObjects.WaterWheel, 1500),
        new(BuiltInMapObjects.Windmill, 1500),
        new(BuiltInMapObjects.MysticalGarden, 1000),
        new(BuiltInMapObjects.WarehouseId(ResourceType.Lumber), 1500, Weight: 1),
        new(BuiltInMapObjects.WarehouseId(ResourceType.Ore), 1500, Weight: 1),
        new(BuiltInMapObjects.WarehouseId(ResourceType.Crystal), 2000, Weight: 1),
        new(BuiltInMapObjects.WarehouseId(ResourceType.Mercury), 2000, Weight: 1),
        new(BuiltInMapObjects.WarehouseId(ResourceType.Sulfur), 2000, Weight: 1),
        new(BuiltInMapObjects.WarehouseId(ResourceType.Gold), 4000, Weight: 1),

        // Temporary buffs.
        new(BuiltInMapObjects.FaerieRing, 500),
        new(BuiltInMapObjects.FountainOfYouth, 500),
        new(BuiltInMapObjects.Temple, 500),
        new(BuiltInMapObjects.MineralSpring, 500),
        new(BuiltInMapObjects.MagicWell, 500),
        new(BuiltInMapObjects.Stables, 1000),

        // Permanent power-ups.
        new(BuiltInMapObjects.LearningStone, 1500),
        new(BuiltInMapObjects.Gazebo, 1500),
        new(BuiltInMapObjects.TreeOfKnowledge, 2500),
        new(BuiltInMapObjects.Arena, 3000),
        new(BuiltInMapObjects.ColosseumOfTheMagi, 3000),
        new(BuiltInMapObjects.LibraryOfEnlightenment, 10000, Weight: 1),

        // Utilities and the prison.
        new(BuiltInMapObjects.Observatory, 750, Weight: 1),
        new(BuiltInMapObjects.WarMachineFactory, 1500, Weight: 1),
        new(BuiltInMapObjects.TownGate, 3000, Weight: 1, MapLimit: 1),
        new(BuiltInMapObjects.HillFort, 7000, Weight: 1),
        new(BuiltInMapObjects.Prison, 5000, Weight: 1),

        // Creature banks and abandoned mines guard themselves.
        new(BuiltInMapObjects.ImpCache, 2500, OwnGuard: true),
        new(BuiltInMapObjects.DwarvenTreasury, 2500, OwnGuard: true),
        new(BuiltInMapObjects.MedusaStores, 3000, OwnGuard: true),
        new(BuiltInMapObjects.Churchyard, 3500, OwnGuard: true),
        new(BuiltInMapObjects.Ruins, 3500, OwnGuard: true),
        new(BuiltInMapObjects.NagaBank, 4000, OwnGuard: true),
        new(BuiltInMapObjects.Mansion, 10000, Weight: 1, OwnGuard: true),
        new(BuiltInMapObjects.AbandonedMineId(ResourceType.Lumber), 2500, Weight: 1, OwnGuard: true),
        new(BuiltInMapObjects.AbandonedMineId(ResourceType.Ore), 2500, Weight: 1, OwnGuard: true),
        new(BuiltInMapObjects.AbandonedMineId(ResourceType.Crystal), 3500, Weight: 1, OwnGuard: true),
        new(BuiltInMapObjects.AbandonedMineId(ResourceType.Mercury), 3500, Weight: 1, OwnGuard: true),
        new(BuiltInMapObjects.AbandonedMineId(ResourceType.Sulfur), 3500, Weight: 1, OwnGuard: true),
        new(BuiltInMapObjects.AbandonedMineId(ResourceType.Gold), 6000, Weight: 1, OwnGuard: true),
    ];

    private static readonly Dictionary<string, TreasureSite> ById = All.ToDictionary(s => s.Definition, StringComparer.Ordinal);

    /// <summary>The site of an object definition, if the generator places it as treasure.</summary>
    public static TreasureSite? Of(string definition) => ById.GetValueOrDefault(definition);

    /// <summary>Whether a site fits an item whose drawn value is <paramref name="value"/>.</summary>
    public static bool Fits(TreasureSite site, int value) => site.Value <= value && site.Value * 2 >= value;
}
