using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The inspector's jump links (EDIT-1, ADR-254): what a thing on the map is tied to.</summary>
public class EditorLinksTests
{
    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);

    private static MapObjectId Place(MapEditorSession editor, string definition, int x, int y)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, new MapPosition(x, y), out var id));
        return id;
    }

    private static IReadOnlyList<MapLink> Links(MapEditorSession editor, MapObjectId id) => MapLinks.Of(editor.Scenario, editor.Scenario.FindObject(id)!);

    [Fact]
    public void TwoWayGates_LinkToEachOther_Once()
    {
        var editor = Editor();
        var (a, b) = (Place(editor, BuiltInMapObjects.TwoWayMonolith, 5, 20), Place(editor, BuiltInMapObjects.TwoWayMonolith, 30, 20));
        Assert.Empty(Links(editor, a));

        Assert.Equal(EditResult.Ok, editor.Objects.LinkGates(a, b));
        var link = Assert.Single(Links(editor, a));
        Assert.Equal(b, link.Object);
        Assert.Equal(editor.Scenario.FindObject(b)!.VisitPosition, link.Position);
        Assert.StartsWith("Leads to", link.Text);
        Assert.Equal(a, Assert.Single(Links(editor, b)).Object);

        editor.Undo();
        Assert.Empty(Links(editor, a));
    }

    [Fact]
    public void OneWayGate_LeadsToItsExit_AndTheExitNamesEveryEntrance()
    {
        var editor = Editor();
        var first = Place(editor, BuiltInMapObjects.OneWayMonolithEntrance, 5, 20);
        var second = Place(editor, BuiltInMapObjects.OneWayMonolithEntrance, 10, 20);
        var exit = Place(editor, BuiltInMapObjects.OneWayMonolithExit, 30, 20);
        Assert.Equal(EditResult.Ok, editor.Objects.SetGateExit(first, exit));
        Assert.Equal(EditResult.Ok, editor.Objects.SetGateExit(second, exit));

        Assert.Equal(exit, Assert.Single(Links(editor, first)).Object);
        var back = Links(editor, exit);
        Assert.Equal([first, second], back.Select(l => l.Object!.Value));
        Assert.All(back, l => Assert.StartsWith("Entered from", l.Text));
    }

    [Fact]
    public void QuestObject_LinksToWhatItsQuestNames_AndTheTargetLinksBack()
    {
        var editor = Editor();
        var hut = Place(editor, BuiltInMapObjects.SeerHut, 10, 20);
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(25, 20), BuiltInCreatures.Wolf, 10, GuardDisposition.Aggressive, false, out var wolf));
        Assert.Equal(EditResult.Ok, editor.Heroes.Place(new PlayerId(1), new MapPosition(30, 25), "Orrin", out var hero));
        var quest = new MapEventDefinition("hunt", MapEventKind.Quest, [])
        {
            Stages =
            [
                new QuestStage(new MonsterDefeatedCondition(wolf.Value), "Kill the wolves.", []),
                new QuestStage(
                    new AllCondition([new HeroDefeatedCondition(hero.Value), new NotCondition(new MonsterDefeatedCondition(wolf.Value)), new MonsterDefeatedCondition(9999)]),
                    "Then the hero.",
                    []),
            ],
        };
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(quest));
        Assert.Empty(Links(editor, wolf));
        Assert.Equal(EditResult.Ok, editor.Contents.SetQuestSiteQuest(hut, "hunt"));

        // The monster once though two stages name it; the object that is not on the map is left out.
        var targets = Links(editor, hut);
        Assert.Equal(2, targets.Count);
        Assert.Equal(wolf, targets[0].Object);
        Assert.Equal(hero, targets[1].Hero);
        Assert.Equal(new MapPosition(30, 25), targets[1].Position);

        Assert.Equal(hut, Assert.Single(Links(editor, wolf)).Object);
        Assert.Equal(hut, Assert.Single(MapLinks.Of(editor.Scenario, editor.Scenario.GetHero(hero))).Object);
    }

    [Fact]
    public void QuestObject_NeverLinksToItself_NorToAnIdOfTheWrongKind()
    {
        var editor = Editor();
        var hut = Place(editor, BuiltInMapObjects.SeerHut, 10, 20);
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 20, 20);
        var quest = new MapEventDefinition("odd", MapEventKind.Quest, [])
        {
            Stages =
            [
                new QuestStage(
                    new AllCondition([new MonsterDefeatedCondition(hut.Value), new MonsterDefeatedCondition(chest.Value), new TownOwnerCondition(chest.Value, null)]),
                    "Nothing real.",
                    []),
            ],
        };
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(quest));
        Assert.Equal(EditResult.Ok, editor.Contents.SetQuestSiteQuest(hut, "odd"));

        Assert.Empty(Links(editor, hut));
        Assert.Empty(Links(editor, chest));
    }

    [Fact]
    public void PandorasBox_LinksToTheCellsOfItsEvent()
    {
        var editor = Editor();
        var box = Place(editor, BuiltInMapObjects.PandorasBox, 10, 20);
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("other", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(12, 22), "trap"));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(14, 22), "other"));
        Assert.Empty(Links(editor, box));

        Assert.Equal(EditResult.Ok, editor.Contents.SetPandorasBoxEvent(box, "trap"));
        var cell = Assert.Single(Links(editor, box));
        Assert.Equal(new MapPosition(12, 22), cell.Position);
        Assert.Null(cell.Object);
        Assert.Null(cell.Hero);
    }

    [Fact]
    public void AMonsterAnEventNames_LinksToTheCellsAndBoxesThatRunIt()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(25, 20), BuiltInCreatures.Wolf, 10, GuardDisposition.Aggressive, false, out var wolf));
        var box = Place(editor, BuiltInMapObjects.PandorasBox, 10, 20);
        var gate = new MapEventDefinition("gate", MapEventKind.Hero, [new IfAction(new MonsterDefeatedCondition(wolf.Value), [])]);
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(gate));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(12, 22), "gate"));
        Assert.Equal(EditResult.Ok, editor.Contents.SetPandorasBoxEvent(box, "gate"));

        var links = Links(editor, wolf);
        Assert.Equal(2, links.Count);
        Assert.Equal(box, links[0].Object);
        Assert.Equal(new MapPosition(12, 22), links[1].Position);
        Assert.All(links, l => Assert.Contains("\"gate\"", l.Text));
    }

    [Fact]
    public void EveryLink_OfEveryThingOnEmberMarch_PointsAtSomethingOnTheMap()
    {
        var scenario = EmberMarchScenario.Create();
        var links = scenario.Objects.SelectMany(o => MapLinks.Of(scenario, o)).Concat(scenario.Heroes.SelectMany(h => MapLinks.Of(scenario, h))).ToList();
        Assert.All(links, link =>
        {
            Assert.True(scenario.Map.Contains(link.Position), link.Text);
            Assert.True(link.Object is not { } id || scenario.FindObject(id) is not null, link.Text);
            Assert.True(link.Hero is not { } hero || scenario.Heroes.Any(h => h.Id == hero), link.Text);
        });
    }
}
