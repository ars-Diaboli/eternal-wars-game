using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Stages 5 and 6 of the generator: roads (RMG-4) and treasure by value band (RMG-1, RMG-2).</summary>
public class RoadAndTreasureTests
{
    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    /// <summary>Stages 1–5: the map before treasure.</summary>
    private static GeneratedMap UpToRoads(MapTemplate template, int size = 96, ulong seed = 3)
    {
        var map = GeneratedMap.From(TerrainPainter.Paint(template, ZoneLayouter.Layout(template, size, seed), seed));
        ConnectionPlacer.Place(template, map, seed);
        SettlementPlacer.Place(template, map, seed);
        RoadPlacer.Place(template, map);
        return map;
    }

    private static int ZoneOf(GeneratedMap map, MapPosition p) => map.Layout.ZoneAt(p.X, p.Y, p.Level);

    /// <summary>Road tiles reachable from a road tile by 8-way steps over road tiles.</summary>
    private static HashSet<MapPosition> RoadNetwork(GeneratedMap map, MapPosition start)
    {
        var seen = new HashSet<MapPosition> { start };
        var queue = new Queue<MapPosition>([start]);
        while (queue.TryDequeue(out var p))
        {
            foreach (var next in DirectionExtensions.All.Select(p.Step).Where(n => map.Map.Contains(n) && map.Map.GetRoad(n) != RoadType.None && seen.Add(n)))
            {
                queue.Enqueue(next);
            }
        }

        return seen;
    }

    // Roads ---------------------------------------------------------------------------------------

    [Fact]
    public void Roads_JoinTheTowns_ThroughThePassages_AndReachTheNearestLumberAndOreMines()
    {
        foreach (var seed in new ulong[] { 1, 2, 3 })
        {
            var map = UpToRoads(Duel(), 96, seed);
            Assert.Empty(map.Problems);
            var towns = map.Towns.Select(t => t.Town.VisitPosition).ToList();
            var network = RoadNetwork(map, towns[0]);

            Assert.All(towns, t => Assert.Contains(t, network)); // every town of the map is on one road network
            Assert.All(map.Passages, p => Assert.Contains(p.Anchor, network));
            foreach (var start in map.Towns.Where(t => t.Slot is not null))
            {
                foreach (var resource in new[] { ResourceType.Lumber, ResourceType.Ore })
                {
                    var nearest = map.Objects.OfType<Mine>().Where(m => m.Resource == resource && ZoneOf(map, m.VisitPosition) == start.Zone)
                        .MinBy(m => Math.Max(Math.Abs(m.VisitPosition.X - start.Town.VisitPosition.X), Math.Abs(m.VisitPosition.Y - start.Town.VisitPosition.Y)));
                    Assert.NotNull(nearest);
                    Assert.Contains(nearest.VisitPosition, network);
                }
            }

            Assert.All(map.Roads, r => Assert.Equal(RoadType.Dirt, map.Map.GetRoad(r)));
        }
    }

    [Fact]
    public void RoadsFollowTheTemplate_AndNeverCrossAnObjectsBody()
    {
        var map = UpToRoads(Duel() with { Roads = new TemplateRoads { Type = RoadType.Cobblestone, Mines = [] } });
        var bodies = map.Objects.SelectMany(o => o.OccupiedCells()).Where(c => c.Cell == FootprintCell.Blocked).Select(c => c.Position).ToHashSet();

        Assert.NotEmpty(map.Roads);
        Assert.All(map.Roads, r => Assert.Equal(RoadType.Cobblestone, map.Map.GetRoad(r)));
        Assert.DoesNotContain(map.Roads, bodies.Contains);
        Assert.True(map.Roads.Count < UpToRoads(Duel()).Roads.Count); // no mine roads

        var none = UpToRoads(Duel() with { Roads = new TemplateRoads { Enabled = false } });
        Assert.Empty(none.Roads);
    }

    [Fact]
    public void GatesOfAZoneWithATown_GetARoad()
    {
        var template = Duel() with
        {
            Connections = [.. Duel().Connections, new TemplateConnection { From = "start-1", To = "reserve-2", Kind = ConnectionKind.Gate }],
        };
        var map = UpToRoads(template);
        var start = map.Towns.First(t => t.Slot == 0);
        var gate = map.Objects.OfType<TwoWayGate>().Single(g => ZoneOf(map, g.VisitPosition) == start.Zone);

        Assert.Contains(gate.VisitPosition, RoadNetwork(map, start.Town.VisitPosition));
        Assert.Equal(MapTemplate.CurrentFormatVersion, TemplateSerializer.Deserialize(TemplateSerializer.Serialize(template with { Roads = new TemplateRoads { Gates = false } })).FormatVersion);
        Assert.False(TemplateSerializer.Deserialize(TemplateSerializer.Serialize(template with { Roads = new TemplateRoads { Gates = false } })).Roads.Gates);
        Assert.True(TemplateSerializer.Deserialize("""{ "formatVersion": 1, "name": "Old", "zones": [] }""").Roads.Enabled); // v1 files get the defaults
    }

    // Treasure ------------------------------------------------------------------------------------

    private static int Value(MapObject item) => item switch
    {
        ResourcePile p => p.Amount * TreasurePlacer.GoldValue(p.Resource),
        TreasureChest c => c.FixedValues!.Gold,
        ArtifactPickup a => TreasurePlacer.ArtifactValue(a.Tier),
        Marketplace => TreasurePlacer.MarketplaceValue,
        _ => TreasureSites.Of(item.Definition.Id)?.Value ?? throw new ArgumentException("Not treasure."),
    };

    /// <summary>Whether the item gets a guard next to it: everything but the sites that fight for themselves.</summary>
    private static bool NeedsGuard(MapObject item) => TreasureSites.Of(item.Definition.Id) is not { OwnGuard: true };

    private static (GeneratedMap Map, List<MapObject> Treasure) WithTreasure(MapTemplate template, ulong seed = 3)
    {
        var map = UpToRoads(template, 96, seed);
        var before = map.Objects.ToHashSet();
        TreasurePlacer.Place(template, map, seed);
        return (map, map.Objects.Where(o => !before.Contains(o) && o is not MonsterGuard).ToList());
    }

    [Fact]
    public void EveryBand_GetsItsCountOfItems_WorthTheBandsValue_WithFixedContents()
    {
        var template = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z.Id == "centre" ? z with { Treasure = [new TreasureBand(500, 3000, 9), new TreasureBand(10000, 15000, 2)] } : z)],
        };

        var (map, treasure) = WithTreasure(template);

        Assert.Empty(map.Problems);
        Assert.Equal(template.Zones.Sum(z => z.Treasure.Sum(b => b.Count)), treasure.Count);
        var centre = template.Zones.Select((z, i) => (z, i)).Single(p => p.z.Id == "centre").i;
        var items = treasure.Where(t => ZoneOf(map, t.VisitPosition) == centre).Select(Value).OrderBy(v => v).ToList();
        Assert.Equal(11, items.Count);
        Assert.All(items.Take(9), v => Assert.InRange(v, 450, 3100)); // rounding of pile amounts and chest gold
        Assert.All(items.Skip(9), v => Assert.InRange(v, TreasurePlacer.ArtifactValue(ArtifactTier.Minor), 15100));
        Assert.All(treasure.OfType<TreasureChest>(), c => Assert.NotNull(c.FixedValues));
        Assert.All(treasure.OfType<ArtifactPickup>(), a => Assert.NotNull(a.Artifact));
        Assert.DoesNotContain(treasure, t => map.Roads.Contains(t.VisitPosition));
    }

    [Fact]
    public void ArtifactsComeOnlyFromValuableBands_AndRelicsOnlyOnce()
    {
        var rich = Duel() with { Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(20000, 20000, 6)], ExtraPiles = 0 })] };
        var poor = Duel() with { Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(500, 3999, 8)] })] };

        var (_, valuable) = WithTreasure(rich);
        var (_, cheap) = WithTreasure(poor);

        var relics = valuable.OfType<ArtifactPickup>().Where(a => a.Tier == ArtifactTier.Relic).Select(a => a.Artifact).ToList();
        Assert.NotEmpty(valuable.OfType<ArtifactPickup>());
        Assert.Equal(relics.Count, relics.Distinct().Count());
        Assert.Empty(cheap.OfType<ArtifactPickup>());
        Assert.True(cheap.OfType<ResourcePile>().Count() > cheap.OfType<TreasureChest>().Count()); // piles 2 : chests 1
    }

    [Fact]
    public void ValuableTreasure_IsGuardedInAreaMode_ByTheZonesStrength_LighterInStartZones()
    {
        var template = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(3000, 3000, 3), new TreasureBand(500, 900, 3)], Monsters = z.Type == ZoneType.PlayerStart ? MonsterStrength.Average : z.Monsters })],
        };

        var (map, treasure) = WithTreasure(template);
        var guards = map.Objects.OfType<MonsterGuard>().Where(g => g.Mode == GuardMode.Area).ToList();
        var creatures = CreatureCatalog.Default;

        Assert.All(treasure.Where(t => NeedsGuard(t) && Value(t) > TreasurePlacer.GuardThreshold && template.Zones[ZoneOf(map, t.VisitPosition)].Monsters != MonsterStrength.None), t =>
            Assert.Contains(guards, g => Math.Max(Math.Abs(g.Position.X - t.VisitPosition.X), Math.Abs(g.Position.Y - t.VisitPosition.Y)) == 1));
        Assert.All(treasure.Where(t => Value(t) <= 900), t =>
            Assert.DoesNotContain(guards, g => Math.Max(Math.Abs(g.Position.X - t.VisitPosition.X), Math.Abs(g.Position.Y - t.VisitPosition.Y)) == 1));
        Assert.All(map.Objects.OfType<MonsterGuard>().Where(g => g.Mode == GuardMode.Classic), g => Assert.Contains(map.Passages, p => p.Anchor == g.Position));

        // Sites are guarded by their own value (half to all of the draw), so only the guards of the other items are worth exactly 3000.
        var ordinary = treasure.Where(t => TreasureSites.Of(t.Definition.Id) is null).ToList();
        var start = guards.Where(g => template.Zones[ZoneOf(map, g.Position)].Type == ZoneType.PlayerStart)
            .Where(g => ordinary.Any(t => Math.Max(Math.Abs(g.Position.X - t.VisitPosition.X), Math.Abs(g.Position.Y - t.VisitPosition.Y)) == 1)
                        && !treasure.Except(ordinary).Any(t => Math.Max(Math.Abs(g.Position.X - t.VisitPosition.X), Math.Abs(g.Position.Y - t.VisitPosition.Y)) == 1))
            .Select(g => g.Count!.Value * creatures.Get(g.Creature).Value).ToList();
        Assert.NotEmpty(start);
        Assert.All(start, power => Assert.InRange(power, 3000 * 0.4 * 0.5 * 0.7, 3000 * 0.6 * 0.5 * 1.3));
        Assert.Equal(0, TreasurePlacer.GuardValue(5000, MonsterStrength.None, startZone: false, 50));
        Assert.True(TreasurePlacer.GuardValue(5000, MonsterStrength.Strong, false, 50) > TreasurePlacer.GuardValue(5000, MonsterStrength.Weak, false, 50));
    }

    [Fact]
    public void MapMarketplaces_ComeOnlyFromValuableBands_OnePerZoneAtMost_AndAreGuardedLikeTreasure()
    {
        var rich = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(5000, 5000, 8)], ExtraPiles = 0, Monsters = MonsterStrength.Average })],
        };
        var poor = Duel() with { Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(500, 3999, 8)] })] };

        var found = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var (map, treasure) = WithTreasure(rich, seed);
            var markets = treasure.OfType<Marketplace>().ToList();
            found += markets.Count;
            Assert.All(markets.GroupBy(m => ZoneOf(map, m.VisitPosition)), zone => Assert.Single(zone));
            var guards = map.Objects.OfType<MonsterGuard>().Where(g => g.Mode == GuardMode.Area).ToList();
            Assert.All(markets, m => Assert.Contains(guards, g => Math.Max(Math.Abs(g.Position.X - m.VisitPosition.X), Math.Abs(g.Position.Y - m.VisitPosition.Y)) == 1));
            Assert.DoesNotContain(markets, m => map.Roads.Contains(m.VisitPosition));

            Assert.DoesNotContain(WithTreasure(poor, seed).Treasure, t => t is Marketplace);
        }

        Assert.True(found > 0, "No map marketplace in 12 seeds.");
    }

    [Fact]
    public void AMarketplaceWithoutMonstersInItsZone_HasNoGuard()
    {
        var rich = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(5000, 5000, 8)], ExtraPiles = 0, Monsters = MonsterStrength.None })],
        };

        var (map, treasure) = WithTreasure(rich, 4);

        Assert.DoesNotContain(map.Objects.OfType<MonsterGuard>(), g => g.Mode == GuardMode.Area);
        Assert.NotEmpty(treasure);
    }

    // Map objects as treasure (ADR-068) -----------------------------------------------------------

    [Fact]
    public void CatalogueObjects_FillPartOfTheBands_WorthHalfToAllOfTheDrawnValue_WithinTheirLimits()
    {
        var template = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(500, 3000, 12), new TreasureBand(8000, 12000, 3)], ExtraPiles = 0 })],
        };

        var kinds = new HashSet<string>(StringComparer.Ordinal);
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var (map, treasure) = WithTreasure(template, seed);
            var sites = treasure.Where(t => TreasureSites.Of(t.Definition.Id) is not null).ToList();
            Assert.NotEmpty(sites);
            Assert.True(sites.Count < treasure.Count / 2, "Sites should be about a third of the items.");
            Assert.All(sites.GroupBy(s => (ZoneOf(map, s.VisitPosition), s.Definition.Id)), g => Assert.True(g.Count() <= TreasureSites.Of(g.Key.Id)!.ZoneLimit));
            Assert.True(sites.Count(s => s.Definition.Id == BuiltInMapObjects.TownGate) <= 1);
            Assert.All(sites, s => Assert.InRange(Value(s), 250, 12000));
            Assert.DoesNotContain(sites, s => map.Roads.Contains(s.VisitPosition));
            kinds.UnionWith(sites.Select(s => s.Definition.Id));
        }

        Assert.True(kinds.Count >= 10, $"Only {kinds.Count} kinds of objects in 6 maps.");
        Assert.All(TreasureSites.All, s => Assert.NotNull(MapObjectCatalog.Default.Get(s.Definition)));
    }

    [Fact]
    public void BanksAndAbandonedMines_GetNoGuardBesideThem_OtherSitesAreGuardedLikeTreasure()
    {
        var template = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(2500, 4000, 14)], ExtraPiles = 0, Monsters = MonsterStrength.Average })],
        };

        var selfGuarded = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var (map, treasure) = WithTreasure(template, seed);
            var guards = map.Objects.OfType<MonsterGuard>().Where(g => g.Mode == GuardMode.Area).ToList();
            bool Guarded(MapObject o) => guards.Any(g => Math.Max(Math.Abs(g.Position.X - o.VisitPosition.X), Math.Abs(g.Position.Y - o.VisitPosition.Y)) == 1);
            var own = treasure.Where(t => !NeedsGuard(t)).ToList();
            selfGuarded += own.Count;
            Assert.All(own.Where(o => !treasure.Any(t => t != o && NeedsGuard(t) && Math.Max(Math.Abs(t.VisitPosition.X - o.VisitPosition.X), Math.Abs(t.VisitPosition.Y - o.VisitPosition.Y)) <= 2)),
                o => Assert.False(Guarded(o)));
            Assert.All(treasure.Where(t => NeedsGuard(t) && TreasureSites.Of(t.Definition.Id) is not null), t => Assert.True(Guarded(t)));
        }

        Assert.True(selfGuarded > 0, "No bank or abandoned mine in 6 maps.");
    }

    [Fact]
    public void AGeneratedMapWithCatalogueObjects_StartsAGame_WithBanksAndMinesRolled()
    {
        var template = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z with { Treasure = [new TreasureBand(2500, 4000, 14)] })],
        };
        var players = template.Zones.Where(z => z.Owner is not null).Select(z => new PlayerSetup(z.Owner!.Value, $"P{z.Owner}", IsHuman: false, EternalWars.Core.Domain.Towns.TownIds.Haven)).ToList();

        var (state, _) = MapGenerator.Build(template, 96, 7, players);
        var session = new EternalWars.Core.Domain.Game.GameSession(state);
        session.Start();

        Assert.All(state.Objects.OfType<CreatureBank>(), b => Assert.True(b.IsGuarded));
        Assert.All(state.Objects.OfType<Mine>().Where(m => m.Definition.Becomes is not null), m => Assert.NotNull(m.Guard));
        Assert.Contains(state.Objects, o => TreasureSites.Of(o.Definition.Id) is not null);
    }

    [Fact]
    public void TheWholeGenerator_IsDeterministic()
    {
        string Dump(GeneratedMap m) => string.Join(";", m.Objects.Select(o => $"{o.Definition.Id}@{o.Position}")) + string.Join(",", m.Roads.OrderBy(r => r.Y).ThenBy(r => r.X));

        Assert.Equal(Dump(MapGenerator.Generate(Duel(), 72, 5)), Dump(MapGenerator.Generate(Duel(), 72, 5)));
        Assert.NotEqual(Dump(MapGenerator.Generate(Duel(), 72, 5)), Dump(MapGenerator.Generate(Duel(), 72, 6)));
    }
}
