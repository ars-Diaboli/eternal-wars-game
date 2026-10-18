using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Generation;

/// <summary>
/// Stage 6 of the generator (ADR-064, RMG-1 and RMG-2): treasure by value band. For every band of a zone, <see cref="TreasureBand.Count"/>
/// items each worth a value drawn between the band's bounds:
/// <list type="bullet">
/// <item>below <see cref="ArtifactThreshold"/>: a resource pile (2 in 3) or a treasure chest (1 in 3);</item>
/// <item>from it: an artifact (1 in 2), else a chest or a pile. The artifact is the best tier the value reaches (<see cref="ArtifactValue"/>),
/// a specific artifact of that tier, relics at most once per map.</item>
/// <item>from <see cref="MarketplaceThreshold"/>, a chest or pile may instead be a map marketplace (ADR-067): 1 in <see cref="MarketplaceOdds"/>, at most one
/// per zone, counted as <see cref="MarketplaceValue"/> for its guard.</item>
/// </list>
/// The generator fixes what an item holds (pile amounts, chest gold and experience, the artifact), so a band is what the template says. An
/// item worth more than <see cref="GuardThreshold"/> in a zone with monsters gets a guard next to it in <see cref="GuardMode.Area"/>, so it
/// cannot be picked up without a fight (user rule): <see cref="GuardShareMin"/>–<see cref="GuardShareMax"/> % of the value (by the zone's strength, half in start zones).
/// Values are placeholders to tune; resources count at <see cref="GoldValue"/>. Runs after the roads, whose tiles it keeps free.
/// Streams: <c>treasure</c> per zone.
/// </summary>
public static class TreasurePlacer
{
    public const int ArtifactThreshold = 4000;

    public const int GuardThreshold = 1000;

    /// <summary>A map marketplace (ADR-067) can replace a chest or pile only where the draw is worth this much (placeholder).</summary>
    public const int MarketplaceThreshold = 4000;

    /// <summary>What a map marketplace counts for as treasure, for its guard (placeholder).</summary>
    public const int MarketplaceValue = 4000;

    /// <summary>One chance in this many for an item that can be one; a zone gets at most one marketplace.</summary>
    public const int MarketplaceOdds = 4;

    /// <summary>The share of an item's value its guard has, before the zone's strength: 40–60 %.</summary>
    public const int GuardShareMin = 40;

    public const int GuardShareMax = 60;

    /// <summary>What a unit of a resource is worth in gold for treasure (placeholder until marketplace rates, TOWN-2).</summary>
    public static int GoldValue(ResourceType resource) => resource switch
    {
        ResourceType.Gold => 1,
        ResourceType.Lumber or ResourceType.Ore => 100,
        _ => 200,
    };

    /// <summary>The value an artifact of a tier is worth.</summary>
    public static int ArtifactValue(ArtifactTier tier) => tier switch
    {
        ArtifactTier.Minor => 4000,
        ArtifactTier.Major => 8000,
        _ => 15000,
    };

    /// <summary>Guard power for an item: 0 below the threshold or without monsters; less in start zones.</summary>
    public static int GuardValue(int value, MonsterStrength strength, bool startZone, int sharePercent)
    {
        if (value <= GuardThreshold || strength == MonsterStrength.None)
        {
            return 0;
        }

        var factor = strength switch
        {
            MonsterStrength.Weak => 0.75,
            MonsterStrength.Average => 1.0,
            _ => 1.5,
        };
        return (int)Math.Round(value * sharePercent / 100.0 * factor * (startZone ? 0.5 : 1.0));
    }

    private static readonly (ResourceType Resource, int Weight)[] PileWeights =
    [
        (ResourceType.Gold, 3), (ResourceType.Lumber, 2), (ResourceType.Ore, 2), (ResourceType.Crystal, 1), (ResourceType.Mercury, 1), (ResourceType.Sulfur, 1),
    ];

    public static void Place(MapTemplate template, GeneratedMap map, ulong seed, CreatureCatalog? creatures = null, ArtifactCatalog? artifacts = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        creatures ??= CreatureCatalog.Default;
        artifacts ??= ArtifactCatalog.Default;
        if (map.Layout.ZoneIds.Count != template.Zones.Count)
        {
            throw new ArgumentException("The map was not made from this template.", nameof(map));
        }

        var streams = new SeedStreams(seed);
        var relics = new HashSet<string>(map.Objects.OfType<ArtifactPickup>().Select(p => p.Artifact).OfType<string>(), StringComparer.Ordinal);
        for (var zone = 0; zone < template.Zones.Count; zone++)
        {
            var spec = template.Zones[zone];
            var random = streams.For("treasure", zone);
            var tiles = map.Layout.Tiles(zone).Select(t => new MapPosition(t.X, t.Y, t.Level)).ToList();
            var placed = new List<MapPosition>();
            var isStart = spec.Type is ZoneType.PlayerStart or ZoneType.AiStart;
            var marketplacePlaced = false;
            foreach (var band in spec.Treasure)
            {
                for (var i = 0; i < band.Count; i++)
                {
                    var value = band.MinValue + random.Next(Math.Max(0, band.MaxValue - band.MinValue) + 1);
                    if (!Item(map, spec, tiles, placed, value, isStart, random, creatures, artifacts, relics, ref marketplacePlaced))
                    {
                        map.Problem($"Zone '{spec.Id}': no room for treasure {i + 1} of {band.Count} ({band.MinValue}-{band.MaxValue}).");
                        break;
                    }
                }
            }
        }
    }

    private static bool Item(GeneratedMap map, TemplateZone spec, List<MapPosition> tiles, List<MapPosition> placed, int value, bool isStart, GameRandom random,
        CreatureCatalog creatures, ArtifactCatalog artifacts, HashSet<string> relics, ref bool marketplacePlaced)
    {
        var roll = random.Next(4);
        MapObject? item = null;
        var guardedValue = value;
        if (value >= ArtifactThreshold && roll < 2 && PickArtifact(artifacts, value, random, relics) is { } artifact)
        {
            var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.ArtifactId(artifact.Tier));
            if (Spot(map, definition, tiles, placed, random) is { } at)
            {
                item = map.Add(new ArtifactPickup(map.NextId(), definition, at, artifact.Id));
                if (artifact.Tier == ArtifactTier.Relic)
                {
                    relics.Add(artifact.Id);
                }
            }
        }
        else if (value >= MarketplaceThreshold && !marketplacePlaced && roll >= 2 && random.Next(MarketplaceOdds) == 0
            && Spot(map, MapObjectCatalog.Default.Get(BuiltInMapObjects.Marketplace), tiles, placed, random) is { } market)
        {
            // A map marketplace (ADR-067) is a treasure item: no owner, guarded like any item of its value.
            item = map.Add(new Marketplace(map.NextId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.Marketplace), market));
            marketplacePlaced = true;
            guardedValue = MarketplaceValue;
        }
        else if (value >= ArtifactThreshold ? roll == 2 : roll == 3)
        {
            var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.TreasureChest);
            if (Spot(map, definition, tiles, placed, random) is { } at)
            {
                // Gold or experience, as a chest offers: the gold is the value, the experience 500 less (as in the roll table, ADR-039).
                var gold = Math.Max(600, (int)Math.Round(value / 100.0) * 100);
                item = map.Add(new TreasureChest(map.NextId(), definition, at, new ChestValues(gold, gold - 500)));
            }
        }
        else
        {
            var weightRoll = random.Next(PileWeights.Sum(w => w.Weight));
            var resource = PileWeights.First(w => (weightRoll -= w.Weight) < 0).Resource;
            var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.PileId(resource));
            if (Spot(map, definition, tiles, placed, random) is { } at)
            {
                var amount = Math.Max(1, (int)Math.Round((double)value / GoldValue(resource)));
                item = map.Add(new ResourcePile(map.NextId(), definition, at, amount));
            }
        }

        if (item is null)
        {
            return false;
        }

        placed.Add(item.VisitPosition);
        var guard = GuardValue(guardedValue, spec.Monsters, isStart, GuardShareMin + random.Next(GuardShareMax - GuardShareMin + 1));
        if (guard > 0)
        {
            var near = DirectionExtensions.All.Select(item.VisitPosition.Step).Where(p => map.IsFree(p) && !map.Roads.Contains(p)).OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            if (near.Count == 0)
            {
                map.Problem($"Zone '{spec.Id}': no room for the guard of the treasure at {item.VisitPosition}.");
                return true;
            }

            var (creature, count) = GuardPicker.Pick(creatures, guard, random);
            map.Add(new MonsterGuard(map.NextId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.Monster), near[random.Next(near.Count)], creature.Id, count, mode: GuardMode.Area));
        }

        return true;
    }

    /// <summary>A place with room around it (for the item and a guard next to it), three tiles from the zone's other treasure.</summary>
    private static MapPosition? Spot(GeneratedMap map, MapObjectDefinition definition, List<MapPosition> tiles, List<MapPosition> placed, GameRandom random) =>
        SettlementPlacer.Find(map, definition, tiles, random, null, closest: false, avoid: placed, spacing: 3);

    /// <summary>An artifact of the best tier the value reaches; a relic only once per map, else the tier below.</summary>
    private static ArtifactDefinition? PickArtifact(ArtifactCatalog artifacts, int value, GameRandom random, HashSet<string> relics)
    {
        foreach (var tier in new[] { ArtifactTier.Relic, ArtifactTier.Major, ArtifactTier.Minor }.Where(t => ArtifactValue(t) <= value))
        {
            var choices = artifacts.OfTier(tier).Where(a => tier != ArtifactTier.Relic || !relics.Contains(a.Id)).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
            if (choices.Count > 0)
            {
                return choices[random.Next(choices.Count)];
            }
        }

        return null;
    }
}
