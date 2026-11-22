using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's Go to (ADR-261): tiles by their numbers, and the map's named things as you type.</summary>
public class MapGoToTests
{
    [Theory]
    [InlineData("12, 7", 12, 7, 0)]
    [InlineData("12 7", 12, 7, 0)]
    [InlineData(" 12,7 ", 12, 7, 0)]
    [InlineData("12, 7, 1", 12, 7, 1)]
    [InlineData("0 0 1", 0, 0, 1)]
    public void TwoOrThreeNumbers_AreATile(string query, int x, int y, int level)
    {
        var map = BlankScenario.Create(40, 30, levels: 2).Map;
        Assert.Equal(new MapPosition(x, y, level), MapGoTo.Tile(map, query, level: 0));
    }

    [Theory]
    [InlineData("40, 7")] // off the map
    [InlineData("12, 7, 2")] // no third level
    [InlineData("12")]
    [InlineData("12, 7, 1, 3")]
    [InlineData("castle 12, 7")]
    [InlineData("-1, 2")]
    [InlineData("١٢, ٧")] // Arabic-Indic digits
    [InlineData("1234, 7")]
    [InlineData("")]
    public void AnythingElse_IsNoTile(string query) =>
        Assert.Null(MapGoTo.Tile(BlankScenario.Create(40, 30, levels: 2).Map, query, level: 0));

    [Fact]
    public void TheShownLevel_IsTheTilesWhenNoneIsTyped()
    {
        var map = BlankScenario.Create(40, 30, levels: 2).Map;
        Assert.Equal(new MapPosition(3, 4, 1), MapGoTo.Tile(map, "3 4", level: 1));
    }

    [Fact]
    public void Names_AreSearchedAsYouType_TheTileFirst_AndEachLinkSelectsItsThing()
    {
        var editor = new MapEditorSession(BlankScenario.Create(40, 30));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(10, 10), out var town));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(5, 5), out var chest));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(chest, "Old chest"));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(6, 5), out _)); // no name: not offered
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("chest trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(7, 5), "chest trap"));
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("cellless", MapEventKind.Hero, []))); // no cell: not offered
        var scenario = editor.Scenario;
        var hero = scenario.Heroes[0];
        var townName = scenario.GetObject(town).Name!;

        // The event's name starts with the word, the object's has it at a word's start: the event first.
        var chests = MapGoTo.Find(scenario, "chest", level: 0);
        Assert.Equal(["chest trap: event at 7, 5", "Old chest: Treasure chest at 5, 5"], chests.Take(2).Select(l => l.Text));
        Assert.Equal((new MapPosition(7, 5), (MapObjectId?)null), (chests[0].Position, chests[0].Object));
        Assert.Equal(chest, chests[1].Object);

        Assert.Equal(town, MapGoTo.Find(scenario, townName.ToUpperInvariant(), level: 0).First().Object);
        Assert.Equal(hero.Id, MapGoTo.Find(scenario, hero.Name, level: 0).First().Hero);

        var all = MapGoTo.Find(scenario, "", level: 0);
        Assert.Equal(scenario.Heroes.Count + 2 + 1, all.Count); // the heroes, the town and the named chest, the event with a cell

        var tileFirst = MapGoTo.Find(scenario, "5, 5", level: 0);
        Assert.Equal(new MapPosition(5, 5), tileFirst[0].Position);
        Assert.Null(tileFirst[0].Object);
        Assert.Empty(MapGoTo.Find(scenario, "nothing like it", level: 0));
    }

    [Fact]
    public void AnId_FindsItsObject_NamedOrNot()
    {
        var editor = new MapEditorSession(BlankScenario.Create(40, 30));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(6, 5), out var chest));
        var found = MapGoTo.Find(editor.Scenario, $"#{chest.Value}", level: 0);
        Assert.Contains(found, l => l.Object == chest && l.Position == editor.Scenario.GetObject(chest).VisitPosition);
        Assert.Empty(MapGoTo.Find(editor.Scenario, "#99999", level: 0).Where(l => l.Object is not null));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("#x")]
    [InlineData("# 3")]
    public void AnythingButHashAndDigits_IsNoId(string query)
    {
        var scenario = new MapEditorSession(BlankScenario.Create(40, 30)).Scenario;
        Assert.DoesNotContain(MapGoTo.Find(scenario, query, level: 0), l => l.Text.StartsWith('#'));
    }

    [Fact]
    public void AnId_FindsItsHero()
    {
        var scenario = new MapEditorSession(BlankScenario.Create(40, 30)).Scenario;
        var hero = scenario.Heroes[0];
        var found = MapGoTo.Find(scenario, $"#{hero.Id.Value}", level: 0);
        Assert.Contains(found, l => l.Hero == hero.Id && l.Position == hero.Position);
    }
}
