using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The template graph's geometry and the zone clipboard (ADR-270, ADR-271): snapping, aligning, spreading, moving, copying, pasting.</summary>
public class TemplateGraphTests
{
    private static TemplateZone Zone(string id, double? x, double? y) => new() { Id = id, GraphX = x, GraphY = y };

    [Theory]
    [InlineData(1.24, 0.5, 1.0)]
    [InlineData(1.26, 0.5, 1.5)]
    [InlineData(-0.74, 0.5, -0.5)]
    [InlineData(1.237, 0, 1.24)]
    public void Snap_PutsAValueOnTheNearestGridLine(double value, double grid, double expected) =>
        Assert.Equal(expected, TemplateGraph.Snap(value, grid));

    [Fact]
    public void Align_PutsZonesOnTheirMeanRowOrColumn_SnappedToTheGrid_AndKeepsTheOtherCoordinate()
    {
        IReadOnlyList<TemplateZone> zones = [Zone("a", 0, 0), Zone("b", 3, 1), Zone("c", 6, 1.4)];

        var row = TemplateGraph.Align(zones, GraphLine.Row);
        Assert.All(row, z => Assert.Equal(0.8, z.GraphY));
        Assert.Equal([0.0, 3.0, 6.0], row.Select(z => z.GraphX!.Value));

        var snapped = TemplateGraph.Align(zones, GraphLine.Row, TemplateGraph.Grid);
        Assert.All(snapped, z => Assert.Equal(1.0, z.GraphY));

        var column = TemplateGraph.Align(zones, GraphLine.Column);
        Assert.All(column, z => Assert.Equal(3.0, z.GraphX));
        Assert.Equal([0.0, 1.0, 1.4], column.Select(z => z.GraphY!.Value));
    }

    [Fact]
    public void Spread_SpacesZonesEvenly_BetweenTheOuterTwo_InTheirOrderAlongTheLine()
    {
        IReadOnlyList<TemplateZone> zones = [Zone("far", 9, 0), Zone("near", 1, 2), Zone("left", 0, 5), Zone("mid", 2, 1)];

        var spread = TemplateGraph.Spread(zones, GraphLine.Row);
        Assert.Equal(["far", "near", "left", "mid"], spread.Select(z => z.Id)); // in the order given
        Assert.Equal([9.0, 3.0, 0.0, 6.0], spread.Select(z => z.GraphX!.Value));
        Assert.Equal([0.0, 2.0, 5.0, 1.0], spread.Select(z => z.GraphY!.Value)); // the heights stay

        var column = TemplateGraph.Spread(zones, GraphLine.Column);
        Assert.Equal([0.0, 3.33, 5.0, 1.67], column.Select(z => z.GraphY!.Value));

        IReadOnlyList<TemplateZone> two = [.. zones.Take(2)];
        Assert.Same(two, TemplateGraph.Spread(two, GraphLine.Row)); // two zones stay where they are
    }

    [Fact]
    public void TheSession_AlignsAndSpreads_AsOneStep_AndRefusesTooFewZones()
    {
        var session = new TemplateEditorSession();
        Assert.True(session.MoveZone("middle", 1, 3));
        Assert.False(session.AlignZones(["middle"], GraphLine.Row)); // one zone is no line
        Assert.False(session.SpreadZones(["start-1", "middle"], GraphLine.Row)); // two stay where they are
        Assert.False(session.AlignZones(["middle", "nowhere"], GraphLine.Row)); // an unknown id does not count

        Assert.True(session.AlignZones(["start-1", "start-2", "middle"], GraphLine.Row, TemplateGraph.Grid));
        Assert.All(session.Template.Zones, z => Assert.Equal(1.0, z.GraphY));
        Assert.True(session.SpreadZones(["start-1", "start-2", "middle"], GraphLine.Row));
        Assert.Equal(3.0, session.Template.Zone("middle")!.GraphX);
        Assert.Equal(["Move zone middle", "Align 3 zones in a row", "Spread 3 zones across"], session.History.Steps);

        Assert.True(session.SpreadZones(["start-1", "start-2", "middle"], GraphLine.Row)); // even already: no step
        Assert.Equal(3, session.History.Steps.Count);
    }

    [Fact]
    public void MoveZones_MovesASelectionTogether_AndRefusesAnUnknownZone()
    {
        var session = new TemplateEditorSession();
        Assert.False(session.MoveZones(new Dictionary<string, (double X, double Y)> { ["middle"] = (1, 1), ["nowhere"] = (2, 2) }));
        Assert.Empty(session.History.Steps);

        session.BeginStep("Move 2 zones");
        Assert.True(session.MoveZones(new Dictionary<string, (double X, double Y)> { ["middle"] = (2, 1), ["start-1"] = (0, 1) }));
        Assert.True(session.MoveZones(new Dictionary<string, (double X, double Y)> { ["middle"] = (2, 2), ["start-1"] = (0, 2) }));
        session.EndStep();
        Assert.Equal(["Move 2 zones"], session.History.Steps); // a dragged selection is one step
        Assert.Equal((2.0, 2.0), TemplateGraph.Place(session.Template.Zone("middle")!));
        Assert.Equal((0.0, 2.0), TemplateGraph.Place(session.Template.Zone("start-1")!));
    }

    [Fact]
    public void Copy_TakesTheZonesWithTheConnectionsBetweenThem()
    {
        var session = new TemplateEditorSession();
        var fragment = session.Copy(["start-1", "middle", "nowhere"])!;
        Assert.Equal(["start-1", "middle"], fragment.Zones.Select(z => z.Id));
        var inner = Assert.Single(fragment.Connections);
        Assert.Equal(("start-1", "middle"), (inner.From, inner.To)); // start-2 – middle leaves the fragment
        Assert.Equal(1, fragment.StartZones);
        Assert.Equal((1.5, 0.0), fragment.Centre);
        Assert.Null(session.Copy(["nowhere"]));
    }

    [Fact]
    public void Paste_GivesNewIds_FreeSlots_ThePlaceAsked_AndTheInnerConnections()
    {
        var session = new TemplateEditorSession();
        var fragment = session.Copy(["start-1", "middle"])!;

        var ids = session.Paste(fragment, 10, 5)!;
        Assert.Equal(["start-3", "middle-1"], ids);
        var start = session.Template.Zone("start-3")!;
        Assert.Equal(2, start.Owner); // the lowest free player slot
        Assert.Equal(3, session.Template.MaxPlayers);
        Assert.Equal((8.5, 5.0), TemplateGraph.Place(start)); // the fragment's centre lands on 10, 5
        Assert.Equal((11.5, 5.0), TemplateGraph.Place(session.Template.Zone("middle-1")!));
        Assert.Contains(session.Template.Connections, c => c.From == "start-3" && c.To == "middle-1" && c.GuardValue == 1500);
        Assert.Equal(fragment.Zones[0] with { Id = "start-3", Owner = 2, GraphX = 8.5, GraphY = 5 }, start); // everything else as copied

        Assert.Equal(["start-4", "middle-2"], session.Paste(fragment, 0, 9)); // pasted again: the next ids
        Assert.Equal(["Paste 2 zones", "Paste 2 zones"], session.History.Steps);
    }

    [Fact]
    public void Paste_IsRefused_WhenThePlayerSlotsAreTooFew_AndChangesNothing()
    {
        var session = new TemplateEditorSession();
        for (var i = 0; i < 6; i++)
        {
            session.AddZone(ZoneType.PlayerStart, i, 3);
        }

        var before = session.Template;
        Assert.Null(session.Paste(session.Copy(["start-1", "start-2"])!, 0, 0)); // eight slots used, two more asked
        Assert.Same(before, session.Template);
        Assert.Null(session.Duplicate(["start-1"]));
        Assert.Equal(["junction-1"], session.Paste(new TemplateFragment([Zone("junction-1", 0, 0) with { Type = ZoneType.Junction }], []), 0, 0));
    }

    [Fact]
    public void Duplicate_PastesBesideTheOriginals_AndAMirrorFollowsOnlyInsideTheCopy()
    {
        var session = new TemplateEditorSession(TemplateEditorSession.Blank() with
        {
            Symmetry = TemplateSymmetry.Vertical,
            Zones =
            [
                .. TemplateEditorSession.Blank().Zones.Select(z => z.Id switch
                {
                    "start-1" => z with { Mirror = "start-2" },
                    "start-2" => z with { Mirror = "start-1" },
                    _ => z,
                }),
                Zone("treasure-1", 2, 2) with { Mirror = "middle" },
            ],
        });

        Assert.Equal(["start-3", "start-4"], session.Duplicate(["start-1", "start-2"]));
        Assert.Equal("start-4", session.Template.Zone("start-3")!.Mirror); // the pair stays a pair
        Assert.Equal("start-3", session.Template.Zone("start-4")!.Mirror);
        Assert.Equal((1.0, 1.0), TemplateGraph.Place(session.Template.Zone("start-3")!)); // one unit right and down

        Assert.Equal(["treasure-2"], session.Duplicate(["treasure-1"]));
        Assert.Null(session.Template.Zone("treasure-2")!.Mirror); // its image is not in the copy
        Assert.Null(session.Duplicate(["nowhere"]));
        Assert.Equal(["Duplicate 2 zones", "Duplicate zone treasure-2"], session.History.Steps);
    }

    [Fact]
    public void RemoveZones_RemovesASelection_WithItsConnectionsAndMirrors_AsOneStep()
    {
        var session = new TemplateEditorSession();
        Assert.Equal(0, session.RemoveZones(["nowhere"]));
        Assert.Equal(2, session.RemoveZones(["start-2", "middle", "nowhere"]));
        Assert.Equal(["start-1"], session.Template.Zones.Select(z => z.Id));
        Assert.Empty(session.Template.Connections);
        Assert.Equal(["Remove 2 zones"], session.History.Steps);
        Assert.True(session.Undo());
        Assert.Equal(3, session.Template.Zones.Count);
    }
}
