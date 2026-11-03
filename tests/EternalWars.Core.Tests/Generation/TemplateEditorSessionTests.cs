using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Editing;
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

    private sealed class Handler<T>(Action<T> handle) : IEventHandler<T>
        where T : IEvent
    {
        public void Handle(T @event) => handle(@event);
    }
}
