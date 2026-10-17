using EternalWars.Core.Data.Templates;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Stage 1 of the generator: dividing the map into zones (Phase 4).</summary>
public class ZoneLayoutTests
{
    public static TheoryData<LayoutStrategy> Strategies => new() { LayoutStrategy.Voronoi, LayoutStrategy.Rectangles };

    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    private static TemplateZone Zone(string id, int size = 1, ZoneType type = ZoneType.Treasure, int? owner = null, int level = 0) => new()
    {
        Id = id, Size = size, Type = type, Owner = owner, Level = level,
        PlayerTowns = type == ZoneType.PlayerStart ? CountRange.Exactly(1) : CountRange.None,
    };

    private static TemplateConnection Link(string from, string to, ConnectionKind kind = ConnectionKind.Normal) => new() { From = from, To = to, Kind = kind };

    /// <summary>Four starts around a hub, each next to a treasure zone: a busier graph than the duel.</summary>
    private static MapTemplate Quad() => new()
    {
        Name = "Quad",
        MinPlayers = 2,
        MaxPlayers = 4,
        Sizes = [96, 128],
        Zones =
        [
            Zone("s1", 3, ZoneType.PlayerStart, 0), Zone("s2", 3, ZoneType.PlayerStart, 1), Zone("s3", 3, ZoneType.PlayerStart, 2), Zone("s4", 3, ZoneType.PlayerStart, 3),
            Zone("t1", 2), Zone("t2", 2), Zone("t3", 2), Zone("t4", 2),
            Zone("hub", 3, ZoneType.Junction),
        ],
        Connections =
        [
            Link("s1", "t1", ConnectionKind.Wide), Link("s2", "t2", ConnectionKind.Wide), Link("s3", "t3", ConnectionKind.Wide), Link("s4", "t4", ConnectionKind.Wide),
            Link("t1", "hub"), Link("t2", "hub"), Link("t3", "hub"), Link("t4", "hub"),
            Link("t1", "t2"), Link("t2", "t3"), Link("t3", "t4"), Link("t4", "t1"),
        ],
    };

    private static bool IsOnePiece(ZoneLayout layout, int zone, int level)
    {
        var tiles = layout.Tiles(zone).Where(t => t.Level == level).Select(t => (t.X, t.Y)).ToHashSet();
        if (tiles.Count == 0)
        {
            return true;
        }

        var seen = new HashSet<(int, int)> { tiles.First() };
        var queue = new Queue<(int X, int Y)>([tiles.First()]);
        while (queue.TryDequeue(out var t))
        {
            foreach (var next in new[] { (t.X + 1, t.Y), (t.X - 1, t.Y), (t.X, t.Y + 1), (t.X, t.Y - 1) })
            {
                if (tiles.Contains(next) && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen.Count == tiles.Count;
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public void TheSameInputs_GiveTheSameLayout_AndOtherSeedsOtherLayouts(LayoutStrategy strategy)
    {
        var first = ZoneLayouter.Layout(Duel(), 72, 5, strategy).ToAscii(0);

        Assert.Equal(first, ZoneLayouter.Layout(Duel(), 72, 5, strategy).ToAscii(0));
        Assert.NotEqual(first, ZoneLayouter.Layout(Duel(), 72, 6, strategy).ToAscii(0));
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public void EveryTile_BelongsToAZone_EveryZoneIsOnePiece_AndGetsItsShare(LayoutStrategy strategy)
    {
        var template = Quad();
        var weights = template.Zones.Sum(z => z.Size);
        foreach (var seed in Enumerable.Range(1, 5).Select(s => (ulong)s))
        {
            var layout = ZoneLayouter.Layout(template, 96, seed, strategy);

            Assert.Equal(96 * 96, Enumerable.Range(0, template.Zones.Count).Sum(layout.Area));
            for (var zone = 0; zone < template.Zones.Count; zone++)
            {
                var share = 96.0 * 96 * template.Zones[zone].Size / weights;
                Assert.True(IsOnePiece(layout, zone, 0), $"seed {seed}: {layout.ZoneIds[zone]} is in pieces\n{layout.ToAscii(0)}");
                Assert.InRange(layout.Area(zone), share * (strategy == LayoutStrategy.Voronoi ? 0.4 : 0.25), share * (strategy == LayoutStrategy.Voronoi ? 1.8 : 2.5));
            }
        }
    }

    [Fact]
    public void Voronoi_MakesEveryConnectionTouch_EvenForBusyGraphs()
    {
        foreach (var template in new[] { Duel(), Quad(), BuiltInTemplates.Find("Free for all")! })
        {
            foreach (var seed in Enumerable.Range(1, 6).Select(s => (ulong)s))
            {
                var layout = ZoneLayouter.Layout(template, 96, seed, LayoutStrategy.Voronoi);

                Assert.Empty(layout.UnmetConnections);
                AssertAllTouch(template, layout, seed);
            }
        }
    }

    [Fact]
    public void Rectangles_MakeSimpleGraphsTouch_AndReportWhatTheyCouldNot()
    {
        foreach (var seed in Enumerable.Range(1, 6).Select(s => (ulong)s))
        {
            var layout = ZoneLayouter.Layout(Duel(), 96, seed, LayoutStrategy.Rectangles);
            Assert.Empty(layout.UnmetConnections);
            AssertAllTouch(Duel(), layout, seed);

            // A star of four around a hub is no guillotine cut; whatever stays apart is reported, and nothing else is.
            var busy = ZoneLayouter.Layout(Quad(), 96, seed, LayoutStrategy.Rectangles);
            var index = Quad().Zones.Select((z, i) => (z.Id, i)).ToDictionary(p => p.Id, p => p.i);
            var apart = Quad().Connections.Where(c => !busy.Touch(index[c.From], index[c.To])).Select(c => (c.From, c.To)).Order().ToList();
            Assert.Equal(apart, busy.UnmetConnections.Order().ToList());
        }
    }

    private static void AssertAllTouch(MapTemplate template, ZoneLayout layout, ulong seed)
    {
        var index = template.Zones.Select((z, i) => (z.Id, i)).ToDictionary(p => p.Id, p => p.i);
        foreach (var c in template.Connections)
        {
            Assert.True(layout.Touch(index[c.From], index[c.To]), $"{template.Name} seed {seed}: {c.From} - {c.To} do not touch{System.Environment.NewLine}{layout.ToAscii(0)}");
        }
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public void EachLevelHoldsOnlyItsOwnZones_AndIsFullyCovered(LayoutStrategy strategy)
    {
        var template = Duel() with
        {
            Underground = true,
            Zones = [.. Duel().Zones, Zone("deep-a", 2, level: 1), Zone("deep-b", 1, level: 1)],
            Connections = [.. Duel().Connections, Link("deep-a", "deep-b"), Link("centre", "deep-a", ConnectionKind.Gate)],
        };

        var layout = ZoneLayouter.Layout(template, 72, 3, strategy);

        Assert.Equal(2, layout.Levels);
        for (var level = 0; level < 2; level++)
        {
            for (var y = 0; y < 72; y++)
            {
                for (var x = 0; x < 72; x++)
                {
                    Assert.Equal(level, template.Zone(layout.IdAt(x, y, level))!.Level);
                }
            }
        }

        Assert.Empty(layout.UnmetConnections); // the gate between levels needs no contact
        Assert.True(IsOnePiece(layout, 5, 1) && IsOnePiece(layout, 6, 1));
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public void ASingleZone_FillsTheWholeMap(LayoutStrategy strategy)
    {
        var template = new MapTemplate { Name = "One", MaxPlayers = 2, Zones = [Zone("a", 1, ZoneType.PlayerStart, 0), Zone("b", 1, ZoneType.PlayerStart, 1)], Connections = [Link("a", "b")] };
        var level = ZoneLayouter.Layout(template with { Zones = template.Zones }, 32, 1, strategy);

        Assert.Equal(32 * 32, level.Area(0) + level.Area(1));
        Assert.True(level.Touch(0, 1));
    }

    [Fact]
    public void AnInvalidTemplate_OrSize_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ZoneLayouter.Layout(Duel() with { Name = "" }, 72, 1));
        Assert.Throws<ArgumentException>(() => ZoneLayouter.Layout(Duel() with { Underground = true }, 72, 1)); // level 1 has no zone
        Assert.Throws<ArgumentOutOfRangeException>(() => ZoneLayouter.Layout(Duel(), 8, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZoneLayouter.Layout(Duel(), 300, 1));
    }

    [Fact]
    public void TheTextDump_HasOneCharacterPerTile()
    {
        var lines = ZoneLayouter.Layout(Duel(), 48, 1).ToAscii(0).Split('\n');

        Assert.Equal(48, lines.Length);
        Assert.All(lines, l => Assert.Equal(48, l.Length));
        Assert.Equal(5, string.Concat(lines).Distinct().Count());
    }
}
