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
/// From <see cref="BoxThreshold"/>, one item in <see cref="BoxOdds"/> that is not a site is a Pandora's Box of gold, experience or creatures worth the
/// value (ADR-100), guarded like any item of its value.
/// Before that, one item in <see cref="SiteOdds"/> is an object of the catalogue (ADR-068, <see cref="TreasureSites"/>) worth between half the
/// drawn value and the value, drawn by weight within the zone's and the map's limits; creature banks and abandoned mines get no extra guard.
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

    /// <summary>One item in this many is an adventure-map object from <see cref="TreasureSites"/> when one fits its value (placeholder).</summary>
    public const int SiteOdds = 3;

    /// <summary>From this value an item may be a Pandora's Box (ADR-100; HoMM3's smallest boxes hold 5000 gold), one in <see cref="BoxOdds"/> (placeholders).</summary>
    public const int BoxThreshold = 5000;

    public const int BoxOdds = 5;

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

    public static void Place(MapTemplate template, GeneratedMap map, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        var creatures = map.Content.Creatures;
        var artifacts = map.Content.Artifacts;
        if (map.Layout.ZoneIds.Count != template.Zones.Count)
        {
            throw new ArgumentException("The map was not made from this template.", nameof(map));
        }

        var streams = new SeedStreams(seed);
        var relics = new HashSet<string>(map.Objects.OfType<ArtifactPickup>().Select(p => p.Artifact).OfType<string>().Select(ArtifactCatalog.BaseOf), StringComparer.Ordinal);
        var mapSites = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var zone = 0; zone < template.Zones.Count; zone++)
        {
            var spec = template.Zones[zone];
            var random = streams.For("treasure", template.RollZone(zone));
            var tiles = map.Layout.Tiles(zone).Select(t => new MapPosition(t.X, t.Y, t.Level)).ToList();
            var placed = new List<MapPosition>();
            var isStart = spec.Type is ZoneType.PlayerStart or ZoneType.AiStart;
            var marketplacePlaced = false;
            var sites = new Sites(new Dictionary<string, int>(StringComparer.Ordinal), mapSites);
            // The values come first, from their own stream, so mirrored zones get the same ones whatever their places cost in draws (ADR-117).
            var valueRandom = streams.For("treasure-values", template.RollZone(zone));
            var values = spec.Treasure.Select(band => Enumerable.Range(0, band.Count).Select(_ => band.MinValue + valueRandom.Next(Math.Max(0, band.MaxValue - band.MinValue) + 1)).ToList()).ToList();
            foreach (var (band, bandIndex) in spec.Treasure.Select((b, i) => (b, i)))
            {
                for (var i = 0; i < band.Count; i++)
                {
                    var value = values[bandIndex][i];
                    if (!Item(map, spec, tiles, placed, value, isStart, random, creatures, artifacts, relics, sites, ref marketplacePlaced))
                    {
                        map.Problem($"Zone '{spec.Id}': no room for treasure {i + 1} of {band.Count} ({band.MinValue}-{band.MaxValue}).");
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// How many obelisks each zone gets (ADR-140, MAP-36): the zones that set <see cref="TemplateZone.Obelisks"/> get that many; the others split
    /// what is left of the map's <see cref="GrailRules.MaxObelisks"/> as evenly as can be, the first zones getting one more. The total never goes past
    /// the cap: zones further down lose what does not fit.
    /// </summary>
    public static IReadOnlyList<int> ObeliskCounts(MapTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var set = template.Zones.Sum(z => Math.Max(0, z.Obelisks ?? 0));
        var auto = template.Zones.Count(z => z.Obelisks is null);
        var rest = Math.Max(0, GrailRules.MaxObelisks - set);
        var counts = new List<int>();
        var left = GrailRules.MaxObelisks;
        var autoIndex = 0;
        foreach (var zone in template.Zones)
        {
            var wanted = zone.Obelisks is { } count ? Math.Max(0, count) : (rest / auto) + (autoIndex++ < rest % auto ? 1 : 0);
            var given = Math.Min(wanted, left);
            counts.Add(given);
            left -= given;
        }

        return counts;
    }

    /// <summary>
    /// Stage 6's last step, after the treasure: the obelisks (<see cref="ObeliskCounts"/>), unguarded, three tiles apart, on maps that can hold a Grail
    /// (<see cref="GrailRules.HoldsGrail"/>). A zone without room for its share gets fewer; a count the template set is reported then.
    /// Streams: <c>obelisks</c> per zone.
    /// </summary>
    public static void PlaceObelisks(MapTemplate template, GeneratedMap map, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        var streams = new SeedStreams(seed);
        if (!GrailRules.HoldsGrail(map.Map))
        {
            return;
        }

        var definition = map.Definition(BuiltInMapObjects.Obelisk);
        var counts = ObeliskCounts(template);
        for (var zone = 0; zone < template.Zones.Count; zone++)
        {
            var random = streams.For("obelisks", template.RollZone(zone));
            var tiles = map.Layout.Tiles(zone).Select(t => new MapPosition(t.X, t.Y, t.Level)).ToList();
            var placed = new List<MapPosition>();
            for (var i = 0; i < counts[zone]; i++)
            {
                if (Spot(map, definition, tiles, placed, random) is not { } at)
                {
                    if (template.Zones[zone].Obelisks is not null)
                    {
                        map.Problem($"Zone '{template.Zones[zone].Id}': no room for obelisk {i + 1} of {counts[zone]}.");
                    }

                    break;
                }

                placed.Add(map.Add(MapObject.Create(map.NextId(), definition, at)).VisitPosition);
            }
        }
    }

    private static bool Item(GeneratedMap map, TemplateZone spec, List<MapPosition> tiles, List<MapPosition> placed, int value, bool isStart, GameRandom random,
        CreatureCatalog creatures, ArtifactCatalog artifacts, HashSet<string> relics, Sites sites, ref bool marketplacePlaced)
    {
        var roll = random.Next(4);
        var guarded = spec.Monsters != MonsterStrength.None; // the spot leaves a free cell by the entrance for a guard
        MapObject? item = null;
        var guardedValue = value;
        var worth = value;
        if (random.Next(SiteOdds) == 0 && PickSite(sites, value, random) is { } site
            && Spot(map, map.Definition(site.Definition), tiles, placed, random, guarded && !site.OwnGuard) is { } siteAt)
        {
            item = map.Add(MapObject.Create(map.NextId(), map.Definition(site.Definition), siteAt));
            sites.Zone[site.Definition] = sites.Zone.GetValueOrDefault(site.Definition) + 1;
            sites.Map[site.Definition] = sites.Map.GetValueOrDefault(site.Definition) + 1;
            guardedValue = site.OwnGuard ? 0 : site.Value;
            worth = site.Value;
        }
        else if (value >= BoxThreshold && random.Next(BoxOdds) == 0
            && Spot(map, map.Definition(BuiltInMapObjects.PandorasBox), tiles, placed, random, guarded) is { } boxAt)
        {
            item = map.Add(new PandorasBox(map.NextId(), map.Definition(BuiltInMapObjects.PandorasBox), boxAt, BoxContentsFor(value, creatures, random)));
        }
        else if (value >= ArtifactThreshold && roll < 2 && PickArtifact(artifacts, value, random, relics) is { } artifact)
        {
            var definition = map.Definition(BuiltInMapObjects.ArtifactId(artifact.Tier));
            if (Spot(map, definition, tiles, placed, random, guarded) is { } at)
            {
                item = map.Add(new ArtifactPickup(map.NextId(), definition, at, artifact.Id));
                if (artifact.Tier == ArtifactTier.Relic)
                {
                    relics.Add(ArtifactCatalog.BaseOf(artifact.Id));
                }
            }
        }
        else if (value >= MarketplaceThreshold && !marketplacePlaced && roll >= 2 && random.Next(MarketplaceOdds) == 0
            && Spot(map, map.Definition(BuiltInMapObjects.Marketplace), tiles, placed, random, guarded) is { } market)
        {
            // A map marketplace (ADR-067) is a treasure item: no owner, guarded like any item of its value.
            item = map.Add(new Marketplace(map.NextId(), map.Definition(BuiltInMapObjects.Marketplace), market));
            marketplacePlaced = true;
            guardedValue = MarketplaceValue;
        }
        else if (value >= ArtifactThreshold ? roll == 2 : roll == 3)
        {
            var definition = map.Definition(BuiltInMapObjects.TreasureChest);
            if (Spot(map, definition, tiles, placed, random, guarded) is { } at)
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
            var definition = map.Definition(BuiltInMapObjects.PileId(resource));
            if (Spot(map, definition, tiles, placed, random, guarded) is { } at)
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
        map.RecordTreasure(item, worth);
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
            map.Add(new MonsterGuard(map.NextId(), map.Definition(BuiltInMapObjects.Monster), near[random.Next(near.Count)], creature.Id, count, mode: GuardMode.Area));
        }

        return true;
    }

    /// <summary>
    /// A generated box's contents (ADR-100), one kind drawn evenly: gold (the value, in steps of 500), experience (four fifths of it) or one stack of
    /// creatures worth the value (<see cref="GuardPicker"/>'s measure). Spells come after Phase 5.
    /// </summary>
    private static BoxContents BoxContentsFor(int value, CreatureCatalog creatures, GameRandom random)
    {
        switch (random.Next(3))
        {
            case 0:
                return new BoxContents { Resources = new ResourceSet(Gold: Math.Max(500, value / 500 * 500)) };
            case 1:
                return new BoxContents { Experience = Math.Max(500, value * 4 / 5 / 500 * 500) };
            default:
                var (creature, count) = GuardPicker.Pick(creatures, value, random);
                return new BoxContents { Creatures = [new Domain.Armies.CreatureStack(creature.Id, count)] };
        }
    }

    /// <summary>How many of each site the zone and the map hold so far.</summary>
    private sealed record Sites(Dictionary<string, int> Zone, Dictionary<string, int> Map);

    /// <summary>A site that fits the value and the limits, drawn by weight; none when nothing fits.</summary>
    private static TreasureSite? PickSite(Sites sites, int value, GameRandom random)
    {
        var choices = TreasureSites.All.Where(s => TreasureSites.Fits(s, value) && sites.Zone.GetValueOrDefault(s.Definition) < s.ZoneLimit
            && (s.MapLimit is not { } limit || sites.Map.GetValueOrDefault(s.Definition) < limit)).ToList();
        if (choices.Count == 0)
        {
            return null;
        }

        var roll = random.Next(choices.Sum(s => s.Weight));
        return choices.First(s => (roll -= s.Weight) < 0);
    }

    /// <summary>Whether a wide site placed at <paramref name="topLeft"/> leaves a free cell next to its entrance for its guard.</summary>
    private static bool HasGuardRoom(GeneratedMap map, MapObjectDefinition definition, MapPosition topLeft)
    {
        var probe = MapObject.Create(default, definition, topLeft);
        var own = probe.OccupiedCells().Select(c => c.Position).ToHashSet();
        return DirectionExtensions.All.Select(probe.VisitPosition.Step).Any(p => !own.Contains(p) && map.IsFree(p) && !map.Roads.Contains(p));
    }

    /// <summary>A place for the item three tiles from the zone's other treasure; with <paramref name="guardRoom"/>, one that leaves a free cell by its entrance for a guard.</summary>
    private static MapPosition? Spot(GeneratedMap map, MapObjectDefinition definition, List<MapPosition> tiles, List<MapPosition> placed, GameRandom random, bool guardRoom = false) =>
        SettlementPlacer.Find(map, definition, tiles, random, null, closest: false, avoid: placed, spacing: 3, accept: guardRoom ? at => HasGuardRoom(map, definition, at) : null);

    /// <summary>
    /// An artifact of the best tier the value reaches; a relic only once per map, else the tier below. A scroll gets its spell here, a random one of
    /// its level (ADR-136).
    /// </summary>
    private static ArtifactDefinition? PickArtifact(ArtifactCatalog artifacts, int value, GameRandom random, HashSet<string> relics)
    {
        foreach (var tier in new[] { ArtifactTier.Relic, ArtifactTier.Major, ArtifactTier.Minor }.Where(t => ArtifactValue(t) <= value))
        {
            var choices = artifacts.OfTier(tier).Where(a => tier != ArtifactTier.Relic || !relics.Contains(a.Id)).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
            if (choices.Count > 0)
            {
                var picked = ArtifactRolls.Pick(choices, random);
                return picked is { ScrollLevel: { } level, Spell: null } && artifacts.ScrollSpells(level) is { Count: > 0 } spells
                    ? artifacts.Get(ArtifactCatalog.WithSpell(picked.Id, spells[random.Next(spells.Count)]))
                    : picked;
            }
        }

        return null;
    }
}
