using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Stage 3 of the generator: passages through the walls, their guards, and gates (Phase 4).</summary>
public class ConnectionPlacerTests
{
    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    private static TemplateZone Zone(string id, int level = 0, ZoneType type = ZoneType.Treasure, int? owner = null) => new()
    {
        Id = id, Level = level, Type = type, Owner = owner, PlayerTowns = type == ZoneType.PlayerStart ? CountRange.Exactly(1) : CountRange.None,
        Obstacles = ObstacleDensity.None,
    };

    /// <summary>Two start zones side by side (a long border), joined by one connection.</summary>
    private static MapTemplate Pair(TemplateConnection connection) => new()
    {
        Name = "Pair",
        Sizes = [96],
        Zones = [Zone("a", type: ZoneType.PlayerStart, owner: 0), Zone("b", type: ZoneType.PlayerStart, owner: 1)],
        Connections = [connection],
    };

    private static GeneratedMap Generate(MapTemplate template, int size = 96, ulong seed = 3, LayoutStrategy strategy = LayoutStrategy.Voronoi)
    {
        var layout = ZoneLayouter.Layout(template, size, seed, strategy);
        var map = GeneratedMap.From(TerrainPainter.Paint(template, layout, seed));
        ConnectionPlacer.Place(template, map, seed);
        return map;
    }

    /// <summary>Tiles reachable from a tile by 8-way steps over ground; monsters can be fought through, gates are not followed.</summary>
    private static HashSet<MapPosition> Reach(GeneratedMap map, MapPosition start)
    {
        var seen = new HashSet<MapPosition> { start };
        var queue = new Queue<MapPosition>([start]);
        var blocked = map.Objects.SelectMany(o => o.OccupiedCells()).Where(c => c.Cell == FootprintCell.Blocked).Select(c => c.Position).ToHashSet();
        while (queue.TryDequeue(out var p))
        {
            foreach (var d in DirectionExtensions.All)
            {
                var next = p.Step(d);
                if (map.Map.Contains(next) && map.Map.GetTerrain(next) != TerrainPainter.WallTerrain && !blocked.Contains(next) && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }

    private static MapPosition AnyTileOf(GeneratedMap map, int zone) =>
        map.Layout.Tiles(zone).Select(t => new MapPosition(t.X, t.Y, t.Level)).First(t => map.Map.GetTerrain(t) != TerrainPainter.WallTerrain);

    [Fact]
    public void TheSameInputs_GiveTheSameMap()
    {
        string Dump(GeneratedMap m) => string.Join(";", m.Objects.Select(o => $"{o.Definition.Id}@{o.Position}")) + string.Concat(m.Passages.Select(p => p.Anchor.ToString()));

        Assert.Equal(Dump(Generate(Duel())), Dump(Generate(Duel())));
        Assert.NotEqual(Dump(Generate(Duel())), Dump(Generate(Duel(), seed: 4)));
    }

    [Fact]
    public void Zones_AreSeparatedByWalls_UntilTheirConnectionsAreOpened()
    {
        var template = Duel();
        var layout = ZoneLayouter.Layout(template, 96, 3);
        var closed = GeneratedMap.From(TerrainPainter.Paint(template, layout, 3));
        Assert.DoesNotContain(AnyTileOf(closed, 1), Reach(closed, AnyTileOf(closed, 0)));

        foreach (var seed in new ulong[] { 1, 2, 3 })
        {
            var open = Generate(template, 96, seed);
            Assert.Empty(open.Problems);
            Assert.Contains(AnyTileOf(open, 1), Reach(open, AnyTileOf(open, 0))); // the two players can reach each other
            for (var zone = 2; zone < template.Zones.Count; zone++)
            {
                Assert.Contains(AnyTileOf(open, zone), Reach(open, AnyTileOf(open, 0)));
            }
        }
    }

    [Fact]
    public void NormalPassages_AreOneTileWide_AndGuarded_WidePassagesAreWiderAndNot()
    {
        var normal = Generate(Pair(new TemplateConnection { From = "a", To = "b", GuardValue = 1500 }), strategy: LayoutStrategy.Rectangles);
        var wide = Generate(Pair(new TemplateConnection { From = "a", To = "b", Kind = ConnectionKind.Wide, GuardValue = 1500 }), strategy: LayoutStrategy.Rectangles);

        var passage = Assert.Single(normal.Passages);
        var guard = Assert.Single(normal.Objects.OfType<MonsterGuard>());
        Assert.Equal(passage.Anchor, guard.Position);
        Assert.Contains(passage.Anchor, passage.Tiles);
        Assert.InRange(passage.Tiles.Count, 2, 8); // the way through a two-tile wall

        Assert.Single(wide.Passages);
        Assert.Empty(wide.Objects);
        Assert.True(wide.Passages[0].Tiles.Count >= 4 * passage.Tiles.Count);

        Assert.Contains(AnyTileOf(normal, 1), Reach(normal, AnyTileOf(normal, 0)));
        Assert.Contains(AnyTileOf(wide, 1), Reach(wide, AnyTileOf(wide, 0)));
    }

    [Fact]
    public void AConnection_MakesTheConfiguredNumberOfPassages_SpreadAlongTheBorder()
    {
        var map = Generate(Pair(new TemplateConnection { From = "a", To = "b", Passages = 3, GuardValue = 800 }), strategy: LayoutStrategy.Rectangles);

        Assert.Equal(3, map.Passages.Count);
        Assert.Equal(3, map.Objects.OfType<MonsterGuard>().Count());
        foreach (var (first, second) in map.Passages.SelectMany((p, i) => map.Passages.Skip(i + 1).Select(q => (p, q))))
        {
            Assert.True(Math.Max(Math.Abs(first.Anchor.X - second.Anchor.X), Math.Abs(first.Anchor.Y - second.Anchor.Y)) >= ConnectionPlacer.Spacing(96));
        }

        Assert.Empty(map.Problems);
    }

    [Fact]
    public void AskingForMorePassagesThanTheBorderHoldsIsReported_NotFatal()
    {
        var map = Generate(Pair(new TemplateConnection { From = "a", To = "b", Passages = 8 }) with { Sizes = [48] }, 48, 3, LayoutStrategy.Rectangles);

        Assert.InRange(map.Passages.Count, 1, 7);
        Assert.Contains(map.Problems, p => p.Contains("of 8 passages"));
    }

    [Fact]
    public void TheGuardsFollowTheValue_FromAnyTown_WithOverlappingTiers()
    {
        var valued = Generate(Duel()).Objects.OfType<MonsterGuard>().ToList();
        var creatures = CreatureCatalog.Default;

        Assert.NotEmpty(valued);
        Assert.All(valued, g =>
        {
            var power = (double)g.Count!.Value * creatures.Get(g.Creature).Value;
            Assert.InRange(power, 1500 * 0.75, 3000 * 1.25); // the duel's guard values are 1500 and 3000
        });
    }

    [Fact]
    public void Gates_AreLinkedPairs_OneInEachZone_AndGuardedOnBothSides()
    {
        var template = Duel() with
        {
            Underground = true,
            Zones = [.. Duel().Zones, Zone("deep-a", 1), Zone("deep-b", 1)],
            Connections =
            [
                .. Duel().Connections,
                new TemplateConnection { From = "deep-a", To = "deep-b" },
                new TemplateConnection { From = "centre", To = "deep-a", Kind = ConnectionKind.Gate, GuardValue = 1200, Passages = 2 },
                new TemplateConnection { From = "reserve-1", To = "reserve-2", Kind = ConnectionKind.Gate, GuardValue = 600 },
            ],
        };

        var map = Generate(template);
        var gates = map.Objects.OfType<TwoWayGate>().ToList();

        Assert.Empty(map.Problems);
        Assert.Equal(6, gates.Count); // two pairs across the levels, one pair on the surface
        Assert.All(gates, g => Assert.Equal(g.Id, gates.Single(o => o.Id == g.LinkedGate).LinkedGate));
        Assert.Equal(4, gates.Count(g => g.Definition.Id == BuiltInMapObjects.SubterraneanGate));
        Assert.Equal(2, gates.Count(g => g.Definition.Id == BuiltInMapObjects.TwoWayMonolith));
        Assert.Equal(2, gates.Count(g => g.Position.Level == 1)); // one end of each subterranean pair is below
        Assert.Equal(4 + 6, map.Objects.OfType<MonsterGuard>().Count()); // the duel's four guarded passages and a guard at each of the six gates
        Assert.Equal(6, gates.Select(g => g.Position).Distinct().Count());

        // Gates need no wall: the surface zones joined only by a gate stay separate on foot.
        Assert.DoesNotContain(map.Passages, p => p.Connection >= template.Connections.Count - 2 && p.Tiles.Count > 0);
    }

    [Fact]
    public void ApplyingTheStagesToTheWrongTemplate_IsRefused()
    {
        var map = GeneratedMap.From(TerrainPainter.Paint(Duel(), ZoneLayouter.Layout(Duel(), 72, 1), 1));

        Assert.Throws<ArgumentException>(() => ConnectionPlacer.Place(BuiltInTemplates.Find("Free for all")!, map, 1));
    }

    [Fact]
    public void Passages_AreChecked_AndSurviveTheFileFormat()
    {
        var zero = Duel() with { Connections = [Duel().Connections[0] with { Passages = 0 }, .. Duel().Connections.Skip(1)] };
        var many = Duel() with { Connections = [Duel().Connections[0] with { Passages = TemplateValidator.MaxPassages + 1 }, .. Duel().Connections.Skip(1)] };
        var three = Duel() with { Connections = [Duel().Connections[0] with { Passages = 3 }, .. Duel().Connections.Skip(1)] };

        Assert.Contains(TemplateValidator.Validate(zero), i => i.Severity == IssueSeverity.Error && i.Message.Contains("passages"));
        Assert.Contains(TemplateValidator.Validate(many), i => i.Severity == IssueSeverity.Error && i.Message.Contains("passages"));
        Assert.Empty(TemplateValidator.Validate(three));
        Assert.Equal(3, TemplateSerializer.Deserialize(TemplateSerializer.Serialize(three)).Connections[0].Passages);
        Assert.All(Duel().Connections, c => Assert.Equal(1, c.Passages));
    }
}
