using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

/// <summary>An editor edit that would not change anything records no undo step and publishes nothing (<see cref="PropertyEdit{T}"/>).</summary>
public class EditorNoOpEditTests
{
    /// <summary>Counts every event the session publishes from now on.</summary>
    private static List<IEvent> Record(MapEditorSession editor)
    {
        var events = new List<IEvent>();
        editor.Events.Subscribe<IEvent>(events.Add);
        return events;
    }

    [Fact]
    public void PropertyEdit_IsNull_WhenTheValueWouldNotChange()
    {
        var value = 3;

        Assert.Null(PropertyEdit<int>.Create("Set", () => value, v => value = v, 3));
        Assert.Null(PropertyEdit<IReadOnlyList<string>>.Create("Set", () => ["a", "b"], _ => { }, ["a", "b"], EditorContext.SameItems<string>));
    }

    [Fact]
    public void PropertyEdit_SetsTheNewValue_AndUndoRestoresTheOld()
    {
        var value = 3;
        var seen = new List<int>();
        var edit = PropertyEdit<int>.Create("Set five", () => value, v => { value = v; seen.Add(v); }, 5)!;

        Assert.Equal("Set five", edit.Description);
        Assert.Empty(seen); // creating does not apply
        edit.Execute();
        Assert.Equal(5, value);
        edit.Undo();
        Assert.Equal(3, value);
        edit.Execute();
        Assert.Equal([5, 3, 5], seen);
    }

    [Fact]
    public void RenamingATownToItsName_RecordsNothing_ButANewNameIsUndoable()
    {
        var editor = TestEditor.Blank();
        var town = BuiltInMapObjects.TownId(TownIds.Haven);
        var free = Enumerable.Range(0, 12 * 10).Select(i => new MapPosition(i % 12, i / 12))
            .First(p => editor.Objects.CanPlace(editor.Catalog.Get(town), p) == EditResult.Ok);
        Assert.Equal(EditResult.Ok, editor.Objects.Place(town, free, out var id));
        editor.Towns.SetName(id, "Brightwater");
        var events = Record(editor);

        Assert.Equal(EditResult.Ok, editor.Towns.SetName(id, "  Brightwater "));
        Assert.Empty(events);
        Assert.Equal("Rename town", editor.History.UndoDescription);

        Assert.Equal(EditResult.Ok, editor.Towns.SetName(id, "Dawnford"));
        Assert.Equal([typeof(MapObjectChanged), typeof(UndoHistoryChanged)], events.Select(e => e.GetType()));
        editor.Undo();
        Assert.Equal("Brightwater", editor.Scenario.ObjectsOf<Town>().Single().Name);
    }

    [Fact]
    public void SettingAHeroOrMineToWhatItHas_RecordsNothing()
    {
        var editor = TestEditor.Blank();
        editor.Heroes.Place(new PlayerId(0), new MapPosition(4, 4), "Orrin", out var hero);
        editor.Objects.Place(BuiltInMapObjects.MineId(ResourceType.Gold), new MapPosition(6, 6), out var mine);
        editor.Heroes.SetStats(hero, 3, 2, SkillLevel.Advanced);
        editor.Heroes.SetArtifacts(hero, [], []);
        editor.Heroes.SetSpells(hero, false, []);
        editor.Objects.SetMineOwner(mine, new PlayerId(1));
        var undoSteps = UndoSteps(editor);
        var events = Record(editor);

        editor.Heroes.SetStats(hero, 3, 2, SkillLevel.Advanced);
        editor.Heroes.SetArtifacts(hero, [], []);
        editor.Heroes.SetSpells(hero, false, []);
        editor.Heroes.SetPlaceholder(hero, false);
        editor.Heroes.SetArmySlot(hero, 6, null);
        editor.Objects.SetMineOwner(mine, new PlayerId(1));
        editor.Objects.SetMineGuard(mine, null);

        Assert.Empty(events);
        Assert.Equal(undoSteps, UndoSteps(editor));
    }

    [Fact]
    public void SettingContentsMapAndPlayersToWhatTheyHave_RecordsNothing()
    {
        var editor = TestEditor.Blank();
        editor.Objects.Place(BuiltInMapObjects.WitchHut, new MapPosition(2, 2), out var hut);
        editor.Contents.SetWitchHutSkills(hut, [BuiltInSkills.Archery, BuiltInSkills.Logistics]);
        editor.Map.SetProperties("Isles", "Two islands");
        editor.Players.SetTeamsLocked(true);
        var undoSteps = UndoSteps(editor);
        var events = Record(editor);

        editor.Contents.SetWitchHutSkills(hut, [BuiltInSkills.Archery, BuiltInSkills.Logistics]); // a new list with the same skills
        editor.Map.SetProperties("Isles", "Two islands");
        editor.Map.SetRules(editor.Scenario.Rules);
        editor.Map.SetGrail(null);
        editor.Players.SetTeamsLocked(true);
        editor.Players.SetTeam(new PlayerId(0), editor.Scenario.GetPlayer(new PlayerId(0)).Team);
        var red = editor.Scenario.GetPlayer(new PlayerId(1));
        editor.Players.Update(red.Id, red.Name, red.IsHuman);

        Assert.Empty(events);
        Assert.Equal(undoSteps, UndoSteps(editor));
    }

    [Fact]
    public void PaintingWhatIsThere_RecordsNothing()
    {
        var editor = TestEditor.Blank();
        var events = Record(editor);

        Assert.Equal(0, editor.Terrain.Paint(new MapPosition(5, 5), TerrainType.Grass, radius: 1));
        Assert.Equal(0, editor.Terrain.PaintRoad(new MapPosition(5, 5), RoadType.None));
        Assert.Equal(0, editor.Terrain.PaintRiver(new MapPosition(5, 5), RiverType.None));

        Assert.Empty(events);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void PaintRoad_UndoRestoresEachPreviousRoad_WithOneEventPerRoad()
    {
        var editor = TestEditor.Blank();
        editor.Terrain.PaintRoad(new MapPosition(4, 4), RoadType.Dirt);
        var painted = new List<RoadsPainted>();
        editor.Events.Subscribe<RoadsPainted>(painted.Add);

        Assert.Equal(9, editor.Terrain.PaintRoad(new MapPosition(5, 5), RoadType.Cobblestone, radius: 1));
        editor.Undo();

        Assert.Equal(RoadType.Dirt, editor.Scenario.Map.GetRoad(new MapPosition(4, 4)));
        Assert.Equal(RoadType.None, editor.Scenario.Map.GetRoad(new MapPosition(5, 5)));
        Assert.Equal([RoadType.Cobblestone, RoadType.Dirt, RoadType.None], painted.Select(p => p.Road));
        Assert.Equal([9, 1, 8], painted.Select(p => p.Positions.Count));
    }

    private static int UndoSteps(MapEditorSession editor)
    {
        var steps = 0;
        while (editor.Undo())
        {
            steps++;
        }

        for (var i = 0; i < steps; i++)
        {
            editor.Redo();
        }

        return steps;
    }
}
