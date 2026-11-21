using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The template editor's mirror tools (ADR-272): making a zone's images and keeping them in sync.</summary>
public class TemplateMirrorTests
{
    /// <summary>A blank template mirrored across the vertical axis: the starts are a pair, the middle its own image.</summary>
    private static TemplateEditorSession Mirrored(TemplateSymmetry symmetry = TemplateSymmetry.Vertical)
    {
        var blank = TemplateEditorSession.Blank();
        return new TemplateEditorSession(blank with
        {
            Symmetry = symmetry,
            Zones = [.. blank.Zones.Select(z => z.Id switch { "start-1" => z with { Mirror = "start-2" }, "start-2" => z with { Mirror = "start-1" }, _ => z })],
        });
    }

    [Theory]
    [InlineData(TemplateSymmetry.Vertical, 1, 4.0, 1.0)]
    [InlineData(TemplateSymmetry.RandomAxis, 1, 4.0, 1.0)]
    [InlineData(TemplateSymmetry.Horizontal, 1, 1.0, -2.0)]
    [InlineData(TemplateSymmetry.Diagonal, 1, 4.0, -2.0)]
    [InlineData(TemplateSymmetry.HalfTurn, 1, 4.0, -2.0)]
    [InlineData(TemplateSymmetry.QuarterTurn, 1, 1.0, -2.0)]
    [InlineData(TemplateSymmetry.QuarterTurn, 2, 4.0, -2.0)]
    [InlineData(TemplateSymmetry.QuarterTurn, 4, 1.0, 1.0)]
    public void Place_MirrorsAPointAroundTheCentre(TemplateSymmetry symmetry, int steps, double x, double y) =>
        Assert.Equal((x, y), TemplateMirrors.Place((1, 1), symmetry, steps, (2.5, -0.5)));

    [Fact]
    public void CycleAndImage_FollowClosedMirrors_AndRefuseBrokenOnes()
    {
        var template = Mirrored().Template;
        Assert.Equal(["start-2", "start-1"], TemplateMirrors.Cycle(template, "start-2"));
        Assert.Null(TemplateMirrors.Cycle(template, "middle")); // its own image
        Assert.Equal("start-2", TemplateMirrors.Image(template, "start-1", 1));
        Assert.Equal("start-1", TemplateMirrors.Image(template, "start-1", 2));
        Assert.Equal("middle", TemplateMirrors.Image(template, "middle", 1));

        var broken = template with { Zones = [.. template.Zones.Select(z => z.Id == "start-2" ? z with { Mirror = null } : z)] };
        Assert.Null(TemplateMirrors.Cycle(broken, "start-1"));
        Assert.Null(TemplateMirrors.Image(broken, "start-1", 1)); // names a mirror that does not come back
        Assert.Null(TemplateMirrors.Image(broken, "start-2", 1)); // named, but names none

        Assert.Null(TemplateMirrors.Cycle(template with { Symmetry = TemplateSymmetry.None }, "start-1")); // no symmetry, no cycle
    }

    [Fact]
    public void MakeMirror_AddsTheZonesImage_WithItsConnections_AsOneValidStep()
    {
        var session = Mirrored();
        var treasure = session.AddZone(ZoneType.Treasure, 1, 2)!;
        Assert.True(session.Connect("start-1", treasure, 800));
        Assert.True(session.Connect("middle", treasure, 900));

        var images = session.MakeMirror([treasure]);

        Assert.Equal(["treasure-2"], images);
        Assert.Equal("Mirror zone treasure-1", session.History.UndoDescription);
        var image = session.Template.Zone("treasure-2")!;
        Assert.Equal("treasure-1", image.Mirror);
        Assert.Equal("treasure-2", session.Template.Zone(treasure)!.Mirror);
        Assert.Equal((5.0, 2.0), (image.GraphX!.Value, image.GraphY!.Value)); // mirrored around the other zones' middle, x 3
        Assert.Equal(800, session.Template.Connections[TemplateMirrors.Find(session.Template, "start-2", "treasure-2")].GuardValue); // start-1's image
        Assert.Equal(900, session.Template.Connections[TemplateMirrors.Find(session.Template, "middle", "treasure-2")].GuardValue); // middle is its own
        Assert.DoesNotContain(session.Issues, i => i.Severity == IssueSeverity.Error);

        Assert.True(session.Undo());
        Assert.Null(session.Template.Zone("treasure-2"));
        Assert.Null(session.Template.Zone(treasure)!.Mirror);
    }

    [Fact]
    public void MakeMirror_ByAQuarterTurn_MakesACycleOfFour_AndGivesStartZonesFreeSlots()
    {
        var session = new TemplateEditorSession(new MapTemplate
        {
            Name = "Quarter", MaxPlayers = 4, Symmetry = TemplateSymmetry.QuarterTurn,
            Zones =
            [
                new TemplateZone { Id = "hub", Type = ZoneType.Junction, GraphX = 0, GraphY = 0 },
                new TemplateZone { Id = "base", Type = ZoneType.PlayerStart, Owner = 0, Team = 1, PlayerTowns = CountRange.Exactly(1), GraphX = -3, GraphY = 0 },
            ],
            Connections = [new TemplateConnection { From = "base", To = "hub", GuardValue = 2000 }],
        });

        var images = session.MakeMirror(["base"]); // the hub stays its own image, in the middle

        Assert.NotNull(images);
        Assert.Equal(["base-1", "base-2", "base-3"], images);
        Assert.Equal(["base", "base-1", "base-2", "base-3"], TemplateMirrors.Cycle(session.Template, "base"));
        Assert.Equal([0, 1, 2, 3], session.Template.Zones.Where(z => z.Type == ZoneType.PlayerStart).Select(z => z.Owner!.Value).Order());
        Assert.Equal([1, null, null, null], session.Template.Zones.Where(z => z.Type == ZoneType.PlayerStart).Select(z => z.Team)); // images are other players
        Assert.DoesNotContain(session.Issues, i => i.Severity == IssueSeverity.Error);
        Assert.Equal(4, session.Template.Connections.Count); // base-k – hub
        Assert.Equal(4, session.Template.MaxPlayers);
        Assert.Equal(session.Template.Zones.Count, session.Template.Zones.Select(z => (z.GraphX, z.GraphY)).Distinct().Count()); // no card on another
    }

    [Fact]
    public void MakeMirror_RefusesWithoutASymmetry_AMirroredZone_OrTooFewSlots()
    {
        var plain = new TemplateEditorSession();
        Assert.Equal("The template has no symmetry: choose one in its settings first", plain.MirrorRefusal(["middle"]));
        Assert.Null(plain.MakeMirror(["middle"]));

        var session = Mirrored();
        Assert.Contains("mirrored already", session.MirrorRefusal(["start-1"]));
        Assert.Equal("No zone is selected", session.MirrorRefusal(["nowhere"]));
        Assert.Null(session.MirrorRefusal(["middle"]));

        var full = Mirrored(TemplateSymmetry.QuarterTurn);
        for (var i = 0; i < 4; i++)
        {
            Assert.NotNull(full.AddZone(ZoneType.PlayerStart, i, 5));
        }

        Assert.StartsWith("No room", full.MirrorRefusal(["start-3"])); // 6 slots used, 3 images wanted
        Assert.Null(full.MakeMirror(["start-3"]));
        Assert.DoesNotContain(full.History.Steps, s => s.StartsWith("Mirror", StringComparison.Ordinal));
    }

    [Fact]
    public void EditingAMirroredZone_CarriesTheChangedFieldsToItsImages_ButNotItsOwnSlotOrPlace()
    {
        var session = Mirrored();
        var before = session.Template.Zone("start-2")!;

        Assert.True(session.UpdateZone(session.Template.Zone("start-1")! with
        {
            Size = 5, Mines = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 2 }, Terrains = [TerrainType.Snow], Team = 3,
        }));

        var image = session.Template.Zone("start-2")!;
        Assert.Equal(5, image.Size);
        Assert.Equal(2, image.Mines[ResourceType.Gold]);
        Assert.Equal([TerrainType.Snow], image.Terrains);
        Assert.Equal((before.Owner, before.Team, before.GraphX, before.Mirror), (image.Owner, image.Team, image.GraphX, image.Mirror));
        Assert.Equal(["Change zone start-1"], session.History.Steps); // one step for both
        Assert.DoesNotContain(session.Issues, i => i.Severity == IssueSeverity.Error);

        Assert.True(session.MoveZone("start-1", -2, 4));
        Assert.Equal(before.GraphX, session.Template.Zone("start-2")!.GraphX); // places are each zone's own
    }

    [Fact]
    public void AFieldTheEditDidNotTouch_StaysAsTheImageHasIt()
    {
        var session = Mirrored();
        var odd = session.Template with { Zones = [.. session.Template.Zones.Select(z => z.Id == "start-2" ? z with { ExtraPiles = 9 } : z)] };
        session.Load(odd);

        Assert.True(session.UpdateZone(session.Template.Zone("start-1")! with { Size = 4 }));

        Assert.Equal(4, session.Template.Zone("start-2")!.Size);
        Assert.Equal(9, session.Template.Zone("start-2")!.ExtraPiles);
    }

    [Fact]
    public void ATypeChange_CarriesTheSlot_ToTheImages()
    {
        var session = Mirrored();
        var treasure = session.AddZone(ZoneType.Treasure, 1, 2)!;
        Assert.NotNull(session.MakeMirror([treasure]));

        Assert.True(session.UpdateZone(session.Template.Zone(treasure)! with { Type = ZoneType.AiStart, Owner = 2, PlayerTowns = CountRange.Exactly(1) }));
        Assert.Equal((ZoneType.AiStart, 3), (session.Template.Zone("treasure-2")!.Type, session.Template.Zone("treasure-2")!.Owner));

        Assert.True(session.UpdateZone(session.Template.Zone("treasure-2")! with { Type = ZoneType.Junction, Owner = null, PlayerTowns = CountRange.None }));
        Assert.Equal((ZoneType.Junction, (int?)null), (session.Template.Zone(treasure)!.Type, session.Template.Zone(treasure)!.Owner));
    }

    [Fact]
    public void ConnectionsBetweenMirroredZones_AreMadeChangedAndRemoved_WithTheirImages()
    {
        var session = Mirrored();
        var treasure = session.AddZone(ZoneType.Treasure, 1, 2)!;
        Assert.NotNull(session.MakeMirror([treasure]));
        var count = session.Template.Connections.Count;

        Assert.True(session.Connect("start-1", treasure, 700));
        Assert.Equal(count + 2, session.Template.Connections.Count);
        var image = TemplateMirrors.Find(session.Template, "start-2", "treasure-2");
        Assert.Equal(700, session.Template.Connections[image].GuardValue);

        var index = TemplateMirrors.Find(session.Template, "start-1", treasure);
        Assert.True(session.UpdateConnection(index, session.Template.Connections[index] with { Passages = 2, Chance = 50 }));
        Assert.Equal((2, 50, 700), (session.Template.Connections[image].Passages, session.Template.Connections[image].Chance, session.Template.Connections[image].GuardValue));

        Assert.True(session.Disconnect(image));
        Assert.Equal(count, session.Template.Connections.Count);
        Assert.Equal(-1, TemplateMirrors.Find(session.Template, "start-1", treasure));
        Assert.Equal("Remove connection start-2 – treasure-2", session.History.UndoDescription);

        Assert.True(session.Connect("start-1", "start-2")); // a zone to its own image: its image is itself
        Assert.Equal(count + 1, session.Template.Connections.Count);
    }

    [Fact]
    public void BreakMirror_UnlinksTheCycle_AndEditsNoLongerCarry()
    {
        var session = Mirrored();
        Assert.False(session.BreakMirror("middle"));

        Assert.True(session.BreakMirror("start-2"));
        Assert.Equal("Break the mirror of start-2", session.History.UndoDescription);
        Assert.All(session.Template.Zones, z => Assert.Null(z.Mirror));

        Assert.True(session.UpdateZone(session.Template.Zone("start-1")! with { Size = 6 }));
        Assert.Equal(3, session.Template.Zone("start-2")!.Size);
    }

    [Theory]
    [InlineData(TemplateSymmetry.Horizontal)]
    [InlineData(TemplateSymmetry.Diagonal)]
    [InlineData(TemplateSymmetry.HalfTurn)]
    public void MakeMirror_GivesAValidTemplate_ForEveryTwoImageSymmetry(TemplateSymmetry symmetry)
    {
        var session = Mirrored(symmetry);
        var treasure = session.AddZone(ZoneType.Treasure, 1, 2)!;
        Assert.True(session.Connect("start-1", treasure, 800));
        Assert.True(session.Connect("middle", treasure, 900));

        Assert.NotNull(session.MakeMirror([treasure]));
        var mirrored = session.Template;
        Assert.DoesNotContain(session.Issues, i => i.Severity == IssueSeverity.Error);

        Assert.True(session.Undo());
        Assert.True(session.Redo());
        Assert.Same(mirrored, session.Template);
    }

    [Fact]
    public void AZoneNamingItselfAsMirror_IsItsOwnImage()
    {
        var session = Mirrored();
        Assert.True(session.UpdateZone(session.Template.Zone("middle")! with { Mirror = "middle" }));
        var treasure = session.AddZone(ZoneType.Treasure, 1, 2)!;
        Assert.True(session.Connect("middle", treasure, 900));

        Assert.Null(session.MirrorRefusal(["middle"]));
        Assert.Equal("middle", TemplateMirrors.Image(session.Template, "middle", 1));
        Assert.NotNull(session.MakeMirror([treasure]));
        Assert.NotEqual(-1, TemplateMirrors.Find(session.Template, "middle", "treasure-2"));
        Assert.DoesNotContain(session.Issues, i => i.Severity == IssueSeverity.Error);
        Assert.False(session.BreakMirror("middle"));
    }

    [Fact]
    public void AnEditWithoutATypeChange_LeavesTheImagesSlotAndTeam()
    {
        var session = Mirrored();
        var odd = session.Template with { Zones = [.. session.Template.Zones.Select(z => z.Id == "start-2" ? z with { Team = 4 } : z)] };
        session.Load(odd);

        Assert.True(session.MoveZone("start-1", 1, 1));
        Assert.True(session.UpdateZone(session.Template.Zone("start-1")! with { Size = 4 }));

        Assert.Equal((1, 4), (session.Template.Zone("start-2")!.Owner!.Value, session.Template.Zone("start-2")!.Team!.Value));
    }

    [Fact]
    public void ATypeChangeNeedingMoreSlotsThanAreFree_IsRefused()
    {
        var session = Mirrored(TemplateSymmetry.QuarterTurn);
        var treasure = session.AddZone(ZoneType.Treasure, 1, 4)!;
        Assert.NotNull(session.MakeMirror([treasure]));
        for (var i = 0; i < 4; i++)
        {
            Assert.NotNull(session.AddZone(ZoneType.PlayerStart, i, 9)); // 6 of 8 slots
        }

        var before = session.Template;
        Assert.False(session.UpdateZone(session.Template.Zone(treasure)! with { Type = ZoneType.PlayerStart, Owner = 6, PlayerTowns = CountRange.Exactly(1) }));
        Assert.Same(before, session.Template);
    }

    [Fact]
    public void InAQuarterTurnRing_ChangingOrRemovingOneConnection_DoesItToAllFour()
    {
        var session = new TemplateEditorSession(new MapTemplate
        {
            Name = "Ring", MaxPlayers = 4, Symmetry = TemplateSymmetry.QuarterTurn,
            Zones = [new TemplateZone { Id = "a", Type = ZoneType.Treasure, GraphX = -3, GraphY = 0 }, new TemplateZone { Id = "hub", Type = ZoneType.Junction, GraphX = 0, GraphY = 0 }],
            Connections = [new TemplateConnection { From = "a", To = "hub", GuardValue = 100 }],
        });
        Assert.NotNull(session.MakeMirror(["a"]));
        Assert.True(session.Connect("a", "a-1", 300)); // the ring: a – a-1 – a-2 – a-3 – a
        Assert.Equal(8, session.Template.Connections.Count);

        var image = TemplateMirrors.Find(session.Template, "a-2", "a-3");
        Assert.True(session.UpdateConnection(image, session.Template.Connections[image] with { GuardValue = 700 }));
        Assert.Equal(4, session.Template.Connections.Count(c => c.GuardValue == 700));

        var moved = TemplateMirrors.Find(session.Template, "a", "a-1");
        Assert.True(session.UpdateConnection(moved, session.Template.Connections[moved] with { To = "a-2", GuardValue = 900 })); // its ends change
        Assert.Equal(1, session.Template.Connections.Count(c => c.GuardValue == 900)); // the images stay

        Assert.True(session.Disconnect(TemplateMirrors.Find(session.Template, "a-1", "a-2")));
        Assert.Equal(5, session.Template.Connections.Count); // a – a-2 and the four spokes
    }
}
