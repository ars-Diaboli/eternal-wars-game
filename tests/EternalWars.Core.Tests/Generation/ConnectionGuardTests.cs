using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Template format v7 (ADR-112): garrisons, border guards and gates on connections, portals, and optional connections.</summary>
public class ConnectionGuardTests
{
    private static TemplateZone Zone(string id, int level = 0, ZoneType type = ZoneType.Treasure, int? owner = null) => new()
    {
        Id = id, Level = level, Type = type, Owner = owner, PlayerTowns = type == ZoneType.PlayerStart ? CountRange.Exactly(1) : CountRange.None,
        Obstacles = ObstacleDensity.None,
    };

    private static MapTemplate Pair(TemplateConnection connection) => new()
    {
        Name = "Pair",
        Sizes = [96],
        Zones = [Zone("a", type: ZoneType.PlayerStart, owner: 0), Zone("b", type: ZoneType.PlayerStart, owner: 1)],
        Connections = [connection],
    };

    /// <summary>Three zones in a ring.</summary>
    private static MapTemplate Ring(params TemplateConnection[] connections) => new()
    {
        Name = "Ring",
        Sizes = [96],
        Zones = [Zone("a", type: ZoneType.PlayerStart, owner: 0), Zone("b", type: ZoneType.PlayerStart, owner: 1), Zone("c")],
        Connections = connections,
    };

    private static GeneratedMap Generate(MapTemplate template, ulong seed = 3, LayoutStrategy strategy = LayoutStrategy.Rectangles)
    {
        var layout = ZoneLayouter.Layout(template, 96, seed, strategy);
        var map = GeneratedMap.From(TerrainPainter.Paint(template, layout, seed));
        ConnectionPlacer.Place(template, map, seed);
        return map;
    }

    /// <summary>Tiles reachable by 8-way steps over ground, stopping at blocked cells and, with <paramref name="stopAtVisits"/>, at every entrance too.</summary>
    private static HashSet<MapPosition> Reach(GeneratedMap map, MapPosition start, bool stopAtVisits)
    {
        var seen = new HashSet<MapPosition> { start };
        var queue = new Queue<MapPosition>([start]);
        var blocked = map.Objects.SelectMany(o => o.OccupiedCells()).Where(c => stopAtVisits || c.Cell == FootprintCell.Blocked).Select(c => c.Position).ToHashSet();
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
        map.Layout.Tiles(zone).Select(t => new MapPosition(t.X, t.Y, t.Level)).First(t => map.Map.GetTerrain(t) != TerrainPainter.WallTerrain && !map.Objects.Any(o => o.OccupiedCells().Any(c => c.Position == t)));

    private static int ZoneOf(GeneratedMap map, MapPosition p) => map.Layout.ZoneAt(p.X, p.Y, p.Level);

    [Fact]
    public void AGarrisonGuard_StandsInTheWay_WithNeutralTroopsOfTheValue()
    {
        var map = Generate(Pair(new TemplateConnection { From = "a", To = "b", GuardValue = 1500, Guard = ConnectionGuard.Garrison }));

        var passage = Assert.Single(map.Passages);
        var garrison = Assert.Single(map.Objects.OfType<MapGarrison>());
        Assert.Empty(map.Objects.OfType<MonsterGuard>());
        Assert.Equal(passage.Anchor, garrison.VisitPosition);
        Assert.Null(garrison.Owner);
        var stack = Assert.Single(garrison.Troops.Stacks).Stack;
        Assert.InRange((double)stack.Count * Core.Domain.Creatures.CreatureCatalog.Default.Get(stack.Creature).Value, 1500 * 0.75, 1500 * 1.25);

        // Only through the garrison: the way is open on foot, closed once its entrance is.
        Assert.Contains(AnyTileOf(map, 1), Reach(map, AnyTileOf(map, 0), stopAtVisits: false));
        Assert.DoesNotContain(AnyTileOf(map, 1), Reach(map, AnyTileOf(map, 0), stopAtVisits: true));
        Assert.Empty(map.Problems);
    }

    [Fact]
    public void ABorderGate_StandsInThePassage_AndTheKeymasterOfItsColourInTheFirstZone()
    {
        var map = Generate(Pair(new TemplateConnection { From = "a", To = "b", GuardValue = 1000, Guard = ConnectionGuard.BorderGate, Colour = ObjectColour.Green }));

        var post = Assert.Single(map.Objects.OfType<BorderPost>());
        Assert.Equal(Assert.Single(map.Passages).Anchor, post.Position);
        Assert.True(post.IsGate);
        Assert.Equal(ObjectColour.Green, post.Colour);
        var tent = Assert.Single(map.Objects.OfType<KeymasterTent>());
        Assert.Equal(ObjectColour.Green, tent.Colour);
        Assert.Equal(0, ZoneOf(map, tent.VisitPosition));
        var guard = Assert.Single(map.Objects.OfType<MonsterGuard>()); // the value guards the tent, not the post
        Assert.True(Math.Abs(guard.Position.X - tent.VisitPosition.X) + Math.Abs(guard.Position.Y - tent.VisitPosition.Y) == 1);
        Assert.Contains(tent.VisitPosition, Reach(map, AnyTileOf(map, 0), stopAtVisits: false));
        Assert.Empty(map.Problems);
    }

    [Fact]
    public void TheTemplatesGuard_AppliesToConnectionsWithoutTheirOwn_AndOneKeymasterServesAColour()
    {
        var template = Ring(
            new TemplateConnection { From = "a", To = "b" },
            new TemplateConnection { From = "b", To = "c", Colour = ObjectColour.Red },
            new TemplateConnection { From = "c", To = "a", Colour = ObjectColour.Red, Guard = ConnectionGuard.Monsters, GuardValue = 500 }) with { ConnectionGuard = ConnectionGuard.BorderGuard };

        var map = Generate(template, strategy: LayoutStrategy.Voronoi);

        var posts = map.Objects.OfType<BorderPost>().ToList();
        Assert.Equal(2, posts.Count);
        Assert.All(posts, p => Assert.False(p.IsGate));
        Assert.Contains(posts, p => p.Colour == ObjectColour.Red);
        Assert.Single(map.Objects.OfType<MonsterGuard>()); // c–a keeps its monsters
        Assert.Equal(posts.Select(p => p.Colour).Distinct().Count(), map.Objects.OfType<KeymasterTent>().Count());
    }

    [Fact]
    public void APortal_LinksTwoZonesAnywhere_EvenAcrossLevels_WithoutTheirTouching()
    {
        var template = new MapTemplate
        {
            Name = "Portals",
            Sizes = [96],
            Underground = true,
            Zones = [Zone("a", type: ZoneType.PlayerStart, owner: 0), Zone("b", type: ZoneType.PlayerStart, owner: 1), Zone("deep", 1)],
            Connections =
            [
                new TemplateConnection { From = "a", To = "b", GuardValue = 800 },
                new TemplateConnection { From = "b", To = "deep", Kind = ConnectionKind.Portal, GuardValue = 600 },
            ],
        };
        Assert.DoesNotContain(TemplateValidator.Validate(template), i => i.Severity == IssueSeverity.Error);

        var map = Generate(template);

        var portals = map.Objects.OfType<TwoWayGate>().ToList();
        Assert.Equal(2, portals.Count);
        Assert.All(portals, p => Assert.Equal(BuiltInMapObjects.TwoWayMonolith, p.Definition.Id));
        Assert.Equal(portals[1].Id, portals[0].LinkedGate);
        Assert.Equal([1, 2], portals.Select(p => ZoneOf(map, p.VisitPosition)).Order());
        Assert.Equal(1 + 2, map.Objects.OfType<MonsterGuard>().Count());
        Assert.Empty(map.Problems);
    }

    [Fact]
    public void AnOptionalConnection_IsSometimesMade_ButNeverOneTheZonesNeed()
    {
        var ring = Ring(new TemplateConnection { From = "a", To = "b" }, new TemplateConnection { From = "b", To = "c" }, new TemplateConnection { From = "c", To = "a", Chance = 50 });
        var rolls = Enumerable.Range(1, 40).Select(s => ConnectionPlacer.Present(ring, (ulong)s)).ToList();

        Assert.All(rolls, r => Assert.True(r[0] && r[1]));
        Assert.Contains(rolls, r => r[2]);
        Assert.Contains(rolls, r => !r[2]);
        Assert.Equal(rolls.Select(r => r[2]), Enumerable.Range(1, 40).Select(s => ConnectionPlacer.Present(ring, (ulong)s)[2])); // from the seed

        // A bridge stays whatever its chance.
        var bridge = Ring(new TemplateConnection { From = "a", To = "b", Chance = 1 }, new TemplateConnection { From = "b", To = "c", Chance = 1 });
        Assert.All(Enumerable.Range(1, 20), s => Assert.All(ConnectionPlacer.Present(bridge, (ulong)s), Assert.True));

        // A dropped connection opens no passage.
        var seed = (ulong)(rolls.FindIndex(r => !r[2]) + 1);
        var map = Generate(ring, seed, LayoutStrategy.Voronoi);
        Assert.DoesNotContain(map.Passages, p => p.Connection == 2);
        Assert.Contains(map.Passages, p => p.Connection == 0);
    }

    [Fact]
    public void TheNewFields_AreChecked_AndSurviveTheFileFormat()
    {
        var pair = Pair(new TemplateConnection { From = "a", To = "b", GuardValue = 500 });
        MapTemplate With(TemplateConnection c) => pair with { Connections = [c] };

        Assert.Contains(TemplateValidator.Validate(With(pair.Connections[0] with { Chance = 0 })), i => i.Severity == IssueSeverity.Error && i.Message.Contains("chance"));
        Assert.Contains(TemplateValidator.Validate(With(pair.Connections[0] with { Chance = 101 })), i => i.Severity == IssueSeverity.Error && i.Message.Contains("chance"));
        Assert.Contains(TemplateValidator.Validate(With(pair.Connections[0] with { Kind = ConnectionKind.Wide, GuardValue = 0, Guard = ConnectionGuard.Garrison })),
            i => i.Severity == IssueSeverity.Warning && i.Message.Contains("ignored"));
        Assert.Contains(TemplateValidator.Validate(With(pair.Connections[0] with { Colour = ObjectColour.Blue })), i => i.Severity == IssueSeverity.Warning && i.Message.Contains("colour"));
        Assert.Contains(TemplateValidator.Validate(With(pair.Connections[0] with { Guard = ConnectionGuard.Garrison, GuardValue = 0 })), i => i.Message.Contains("no troops"));

        var full = pair with
        {
            ConnectionGuard = ConnectionGuard.Garrison,
            Connections = [pair.Connections[0] with { Guard = ConnectionGuard.BorderGate, Colour = ObjectColour.Black, Chance = 60, Kind = ConnectionKind.Normal }],
        };
        var loaded = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(full));
        Assert.Equal(7, loaded.FormatVersion);
        Assert.Equal(ConnectionGuard.Garrison, loaded.ConnectionGuard);
        Assert.Equal(full.Connections[0], loaded.Connections[0]);
        var portal = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(With(pair.Connections[0] with { Kind = ConnectionKind.Portal })));
        Assert.Equal(ConnectionKind.Portal, portal.Connections[0].Kind);

        // A v6 file has none of them: monsters, every connection made.
        var old = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(pair).Replace("\"formatVersion\": 7", "\"formatVersion\": 6"));
        Assert.Equal(6, old.FormatVersion);
        Assert.Equal(ConnectionGuard.Monsters, old.GuardOf(old.Connections[0]));
        Assert.Equal(100, old.Connections[0].Chance);
    }

    [Fact]
    public void AWholeGeneratedGame_WithGarrisonsAndBorderPosts_Validates()
    {
        var duel = BuiltInTemplates.Find("duel")!;
        var template = duel with
        {
            ConnectionGuard = ConnectionGuard.Garrison,
            Connections = [.. duel.Connections.Take(2), duel.Connections[2] with { Guard = ConnectionGuard.BorderGuard }, .. duel.Connections.Skip(3)],
        };

        var map = MapGenerator.Generate(template, 96, 5);
        Assert.Empty(map.Problems);
        Assert.Equal(3, map.Objects.OfType<MapGarrison>().Count() + map.Passages.Count(p => p.Connection >= 3 && map.Objects.OfType<MonsterGuard>().Any(g => g.Position == p.Anchor)));

        Assert.NotEmpty(map.Objects.OfType<MapGarrison>());
        Assert.All(Enumerable.Range(1, 4), s => Assert.Equal(3, MapGenerator.Generate(template, 96, (ulong)s).Objects.OfType<MapGarrison>().Count())); // irregular borders too
        Assert.NotEmpty(map.Objects.OfType<BorderPost>());
        Assert.NotEmpty(map.Objects.OfType<KeymasterTent>());
    }
}
