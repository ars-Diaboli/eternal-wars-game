using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Events;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The template editor's core (ADR-070 RMG-10, ADR-117): engine-free operations on a template, each publishing the live validation.</summary>
public class TemplateEditorSessionTests
{
    [Fact]
    public void ABlankTemplate_IsValid_AndGeneratesAMap()
    {
        var session = new TemplateEditorSession();
        Assert.DoesNotContain(session.Issues, i => i.Severity == IssueSeverity.Error);
        var map = EditorMapGenerator.Generate(session.Template, 72, 1);
        Assert.Equal(2, map.Scenario.Players.Count);
    }

    [Fact]
    public void Zones_AreAdded_Renamed_Moved_AndRemoved_WithTheirConnections_AndEveryChangeIsPublished()
    {
        var bus = new EventBus();
        var session = new TemplateEditorSession(bus: bus);
        var changes = new List<TemplateChanged>();
        bus.Subscribe(new Handler<TemplateChanged>(changes.Add));

        var start = session.AddZone(ZoneType.PlayerStart, 2, 3);
        Assert.Equal("start-3", start);
        Assert.Equal(2, session.Template.Zone("start-3")!.Owner);
        Assert.Equal(3, session.Template.MaxPlayers); // the player count follows the start zones
        Assert.Contains(changes.Last().Issues, i => i.Message.Contains("'start-3' has no connection"));

        Assert.True(session.Connect("start-3", "middle"));
        Assert.False(session.Connect("middle", "start-3")); // already joined
        Assert.False(session.Connect("middle", "middle"));
        Assert.DoesNotContain(changes.Last().Issues, i => i.Severity == IssueSeverity.Error);

        Assert.True(session.RenameZone("middle", "centre"));
        Assert.False(session.RenameZone("centre", "start-1")); // in use
        Assert.All(session.Template.Connections, c => Assert.True(c.From == "centre" || c.To == "centre"));

        Assert.True(session.MoveZone("centre", 5.5, -1));
        Assert.Equal((5.5, -1.0), (session.Template.Zone("centre")!.GraphX!.Value, session.Template.Zone("centre")!.GraphY!.Value));

        Assert.True(session.RemoveZone("start-3"));
        Assert.Equal(2, session.Template.MaxPlayers);
        Assert.Equal(2, session.Template.Connections.Count);
        Assert.False(session.RemoveZone("start-3"));
        Assert.Equal(5, changes.Count); // one per change that applied; refused ones publish nothing
    }

    [Fact]
    public void ZonesAndConnections_AreEdited_AndTheTemplatesOwnSettingsKeepTheGraph()
    {
        var session = new TemplateEditorSession();
        var zone = session.Template.Zone("middle")!;
        Assert.True(session.UpdateZone(zone with { Monsters = MonsterStrength.Weak, Size = 4, GraphX = null }));
        Assert.Equal(MonsterStrength.Weak, session.Template.Zone("middle")!.Monsters);
        Assert.NotNull(session.Template.Zone("middle")!.GraphX); // the graph place is kept
        Assert.False(session.UpdateZone(zone with { Id = "nowhere" }));

        Assert.True(session.UpdateConnection(0, session.Template.Connections[0] with { Kind = ConnectionKind.Wide, GuardValue = 0 }));
        Assert.False(session.UpdateConnection(9, session.Template.Connections[0]));
        Assert.True(session.Disconnect(1));
        Assert.Contains(session.Issues, i => i.Message.Contains("no connection"));

        session.UpdateTemplate(t => t with { Name = "Mine", Symmetry = TemplateSymmetry.Vertical, Zones = [] });
        Assert.Equal("Mine", session.Template.Name);
        Assert.Equal(3, session.Template.Zones.Count); // zones are not changed this way
    }

    [Fact]
    public void AStartZoneTakesTheNextFreeSlot_UpToEight()
    {
        var session = new TemplateEditorSession();
        for (var i = 0; i < 6; i++)
        {
            Assert.NotNull(session.AddZone(ZoneType.AiStart, i, 1));
        }

        Assert.Null(session.AddZone(ZoneType.PlayerStart, 0, 0));
        Assert.Equal(8, session.Template.MaxPlayers);
        Assert.Equal(Enumerable.Range(0, 8), session.Template.Zones.Select(z => z.Owner).OfType<int>().Order());
    }

    [Fact]
    public void AShippedTemplate_OpensAndIsArranged_AndAnEditedOneSurvivesTheFile()
    {
        var session = new TemplateEditorSession();
        session.Load(BuiltInTemplates.Find("2v2")!);
        Assert.All(session.Template.Zones, z => Assert.Null(z.GraphX));
        session.ArrangeGraph();
        Assert.All(session.Template.Zones, z => Assert.NotNull(z.GraphX));
        Assert.Equal(session.Template.Zones.Count, session.Template.Zones.Select(z => (z.GraphX, z.GraphY)).Distinct().Count());

        session.AddZone(ZoneType.Treasure, 9, 9);
        var loaded = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(session.Template));
        Assert.Equal(session.Template.Zones.Select(z => (z.Id, z.GraphX, z.GraphY)), loaded.Zones.Select(z => (z.Id, z.GraphX, z.GraphY)));
    }

    public static TheoryData<string, string> Changes() => new()
    {
        { "add a treasure zone", "Add zone treasure-1" },
        { "add a start zone", "Add zone start-3" },
        { "change a zone", "Change zone middle" },
        { "rename a zone", "Rename zone middle to centre" },
        { "move a zone", "Move zone middle" },
        { "remove a zone", "Remove zone start-2" },
        { "connect two zones", "Connect start-1 – start-2" },
        { "change a connection", "Change connection start-1 – middle" },
        { "remove a connection", "Remove connection start-2 – middle" },
        { "change the template's settings", "Change the template's settings" },
        { "arrange the graph", "Arrange the graph" },
        { "move several zones", "Move 2 zones" },
        { "align zones", "Align 2 zones in a column" },
        { "remove several zones", "Remove 2 zones" },
        { "paste zones", "Paste 2 zones" },
        { "duplicate a zone", "Duplicate zone middle-1" },
        { "add a zone from a preset", "Add zone treasure-1 (Rich treasure)" },
        { "apply a preset", "Apply preset Mine junction to zone middle" },
    };

    private static void Make(TemplateEditorSession session, string change)
    {
        var applied = change switch
        {
            "add a treasure zone" => session.AddZone(ZoneType.Treasure, 1, 1) is not null,
            "add a start zone" => session.AddZone(ZoneType.PlayerStart, 1, 1) is not null,
            "change a zone" => session.UpdateZone(session.Template.Zone("middle")! with { Monsters = MonsterStrength.Weak }),
            "rename a zone" => session.RenameZone("middle", "centre"),
            "move a zone" => session.MoveZone("middle", 7, 7),
            "remove a zone" => session.RemoveZone("start-2"),
            "connect two zones" => session.Connect("start-1", "start-2"),
            "change a connection" => session.UpdateConnection(0, session.Template.Connections[0] with { GuardValue = 4000 }),
            "remove a connection" => session.Disconnect(1),
            "change the template's settings" => Apply(() => session.UpdateTemplate(t => t with { Name = "Renamed" })),
            "arrange the graph" => Apply(() => session.ArrangeGraph(all: true)),
            "move several zones" => session.MoveZones(new Dictionary<string, (double X, double Y)> { ["start-1"] = (1, 1), ["middle"] = (2, 2) }),
            "align zones" => session.AlignZones(["start-1", "middle"], GraphLine.Column),
            "remove several zones" => session.RemoveZones(["start-2", "middle"]) == 2,
            "paste zones" => session.Paste(session.Copy(["start-1", "middle"])!, 0, 5) is not null,
            "duplicate a zone" => session.Duplicate(["middle"]) is not null,
            "add a zone from a preset" => session.AddZone(ZonePresets.BuiltIn[0], 4, 4) is not null,
            "apply a preset" => session.ApplyPreset("middle", ZonePresets.BuiltIn[1]),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
        Assert.True(applied);

        static bool Apply(Action change)
        {
            change();
            return true;
        }
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void EveryChange_IsOneNamedStep_ThatUndoesAndRedoes(string change, string description)
    {
        var bus = new EventBus();
        var session = new TemplateEditorSession(bus: bus);
        var changes = new List<TemplateChanged>();
        bus.Subscribe(new Handler<TemplateChanged>(changes.Add));
        var before = session.Template;

        Make(session, change);
        var after = session.Template;
        Assert.NotSame(before, after);
        Assert.Equal([description], session.History.Steps);
        Assert.True(session.IsDirty);
        Assert.False(changes.Last().Replaced); // made by an operation: the view that made it shows it

        Assert.True(session.Undo());
        Assert.Same(before, session.Template); // the template before the step, as it was (templates are values)
        Assert.True(changes.Last().Replaced); // put back whole: the views show every part again
        Assert.Equal(changes.Last().Issues, session.Issues);
        Assert.False(session.IsDirty);

        Assert.True(session.Redo());
        Assert.Same(after, session.Template);
        Assert.False(session.Redo());
    }

    [Fact]
    public void AChangeThatChangesNothing_RecordsNoStep()
    {
        var session = new TemplateEditorSession();
        var zone = session.Template.Zone("middle")!;
        Assert.True(session.UpdateZone(zone));
        Assert.True(session.MoveZone("middle", zone.GraphX!.Value, zone.GraphY!.Value));
        Assert.True(session.RenameZone("middle", "middle"));
        Assert.True(session.UpdateConnection(0, session.Template.Connections[0]));
        session.UpdateTemplate(t => t with { Name = t.Name });
        session.ArrangeGraph(); // every zone is placed already
        Assert.Empty(session.History.Steps);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void UndoTo_JumpsToAStep_AndANewChangeDropsTheUndoneSteps()
    {
        var session = new TemplateEditorSession();
        var start = session.Template;
        session.AddZone(ZoneType.Treasure, 1, 1);
        var one = session.Template;
        session.AddZone(ZoneType.Junction, 2, 2);
        session.UpdateTemplate(t => t with { Name = "Three" });

        Assert.True(session.UndoTo(1));
        Assert.Same(one, session.Template);
        Assert.Equal(1, session.History.Position);
        Assert.False(session.UndoTo(1));
        Assert.True(session.UndoTo(0));
        Assert.Same(start, session.Template);
        Assert.True(session.UndoTo(3));
        Assert.Equal("Three", session.Template.Name);

        session.UndoTo(1);
        session.RemoveZone("treasure-1");
        Assert.Equal(["Add zone treasure-1", "Remove zone treasure-1"], session.History.Steps);
        Assert.False(session.Redo());
    }

    [Fact]
    public void ChangesBetweenBeginStepAndEndStep_AreOneStep()
    {
        var session = new TemplateEditorSession();
        var before = session.Template;
        session.BeginStep("Move zone middle");
        Assert.True(session.IsStepOpen);
        session.MoveZone("middle", 1, 1);
        session.MoveZone("middle", 2, 2);
        session.MoveZone("middle", 3, 3);
        Assert.Empty(session.History.Steps); // shown as it is dragged, recorded when it ends
        Assert.Throws<InvalidOperationException>(() => session.BeginStep("Another"));
        session.EndStep();

        Assert.Equal(["Move zone middle"], session.History.Steps);
        Assert.Equal(3, session.Template.Zone("middle")!.GraphX);
        session.Undo();
        Assert.Same(before, session.Template);

        session.BeginStep("Move zone middle"); // a click without a drag
        session.EndStep();
        session.EndStep(); // none open: nothing happens
        Assert.Equal(0, session.History.Position);
        Assert.Single(session.History.Steps); // the undone drag stays to redo

        session.Redo();
        session.BeginStep("Move zone middle"); // dragged away and back: no step
        session.MoveZone("middle", 9, 9);
        session.MoveZone("middle", 3, 3);
        session.EndStep();
        Assert.Single(session.History.Steps);

        session.BeginStep("Move zone middle"); // a release that never came: an undo ends the drag first, as its own step
        session.MoveZone("middle", 5, 5);
        Assert.True(session.Undo());
        Assert.False(session.IsStepOpen);
        Assert.Equal(3, session.Template.Zone("middle")!.GraphX);
        Assert.Equal(2, session.History.Steps.Count);
    }

    [Fact]
    public void Load_StartsTheHistoryAnew_AsSaved_AndArrangesWithoutAStep()
    {
        var bus = new EventBus();
        var session = new TemplateEditorSession(bus: bus);
        var unsaved = new List<bool>();
        bus.Subscribe(new Handler<UnsavedWorkChanged>(e => unsaved.Add(e.IsDirty)));
        session.AddZone(ZoneType.Treasure, 1, 1);
        Assert.True(session.IsDirty);

        session.Load(BuiltInTemplates.Find("2v2")!, arrange: true);
        Assert.All(session.Template.Zones, z => Assert.NotNull(z.GraphX));
        Assert.Empty(session.History.Steps);
        Assert.False(session.IsDirty);
        Assert.Equal([true, false], unsaved);

        session.UpdateTemplate(t => t with { Name = "Mine" });
        session.MarkSaved(); // written to a file
        Assert.False(session.IsDirty);
        session.Undo();
        Assert.True(session.IsDirty); // the saved template is a step ahead
        session.Redo();
        Assert.False(session.IsDirty);
        session.MarkUnsaved(); // a restored autosave
        Assert.True(session.IsDirty);
        Assert.Equal([true, false, true, false, true, false, true], unsaved);
    }

    private sealed class Handler<T>(Action<T> handle) : IEventHandler<T>
        where T : IEvent
    {
        public void Handle(T @event) => handle(@event);
    }
}
