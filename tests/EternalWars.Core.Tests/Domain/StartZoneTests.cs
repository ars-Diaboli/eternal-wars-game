using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Start zones in the map (MP-4, ADR-197): a cell layer of the map format (v40), written by the generator from the template's start zones, painted
/// in the editor (undoable) and checked by the validator (a zone of a missing player, a player without one, zones that touch, a zone without the
/// player's town or with another player's).
/// </summary>
public class StartZoneTests
{
    [Fact]
    public void StartZones_AreACellLayerOfTheMapFormat()
    {
        var state = Create("A.....B");
        state.Map.SetStartZone(new MapPosition(0, 0), Blue);
        state.Map.SetStartZone(new MapPosition(6, 0), Red);

        var document = MapConverter.ToDocument(state);
        Assert.Equal(["0.....1"], document.StartZones![0]);
        var loaded = TestFormat.Load(MapSerializer.Serialize(document));
        Assert.Equal(Blue, loaded.Map.GetStartZone(new MapPosition(0, 0)));
        Assert.Equal(Red, loaded.Map.GetStartZone(new MapPosition(6, 0)));
        Assert.Null(loaded.Map.GetStartZone(new MapPosition(3, 0)));
        Assert.Equal([Blue, Red], loaded.Map.StartZoneOwners);

        Assert.Null(MapConverter.ToDocument(Create("A.B")).StartZones); // a map without zones writes none
    }

    [Fact]
    public void TheGenerator_WritesEachPlayersStartZone_AroundItsStartTown()
    {
        var duel = BuiltInTemplates.Find("duel")!;
        var scenario = EditorMapGenerator.Generate(duel, 72, 5).Scenario;
        var map = scenario.Map;

        Assert.Equal(scenario.Players.Select(p => p.Id), map.StartZoneOwners);
        foreach (var player in scenario.Players)
        {
            var town = scenario.ObjectsOf<Town>().First(t => t.Owner == player.Id);
            Assert.Equal(player.Id, map.GetStartZone(town.Position));
            Assert.All(scenario.HeroesOf(player.Id), h => Assert.Equal(player.Id, map.GetStartZone(h.Position)));
        }

        Assert.DoesNotContain(MapValidator.Validate(scenario), i => i.Message.Contains("start zone", StringComparison.Ordinal) && !i.Message.Contains("touch", StringComparison.Ordinal));
    }

    [Fact]
    public void TheEditor_PaintsAndErasesStartZones_InOneUndoableCommand()
    {
        var editor = new MapEditorSession(Create("A.....B", "......."));
        var painted = new List<StartZonesPainted>();
        editor.Events.Subscribe<StartZonesPainted>(painted.Add);

        Assert.Equal(4, editor.Terrain.PaintStartZone(new MapPosition(0, 0), Blue, radius: 1));
        Assert.Equal(Blue, editor.Scenario.Map.GetStartZone(new MapPosition(1, 1)));
        Assert.Equal(1, editor.Terrain.PaintStartZone(new MapPosition(1, 1), null));
        Assert.Null(editor.Scenario.Map.GetStartZone(new MapPosition(1, 1)));

        Assert.True(editor.Undo());
        Assert.Equal(Blue, editor.Scenario.Map.GetStartZone(new MapPosition(1, 1)));
        Assert.True(editor.Undo());
        Assert.False(editor.Scenario.Map.HasStartZones);
        Assert.Equal(4, painted.Count);
    }

    [Fact]
    public void TheValidator_ChecksStartZones()
    {
        var state = Create("A..B...");
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("start zone", StringComparison.Ordinal)); // none: nothing to check

        state.Map.SetStartZone(new MapPosition(0, 0), Blue);
        state.Map.SetStartZone(new MapPosition(1, 0), Red); // touches Blue's
        state.Map.SetStartZone(new MapPosition(5, 0), new PlayerId(5)); // nobody's player
        var issues = MapValidator.Validate(state);

        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("player 6", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Message == "Blue's and Red's start zones touch: a hero steps from one into the other at once.");

        var two = Create("A..B...");
        two.Map.SetStartZone(new MapPosition(0, 0), Blue);
        Assert.Contains(MapValidator.Validate(two), i => i.Message == "Red has no start zone: a simultaneous first week needs one for every player.");

        var crossed = Create("A..B...");
        crossed.Map.SetStartZone(new MapPosition(3, 0), Blue); // Red's hero stands in Blue's zone
        crossed.Map.SetStartZone(new MapPosition(6, 0), Red);
        Assert.Contains(MapValidator.Validate(crossed), i => i.Message.EndsWith("stands in Blue's start zone.", StringComparison.Ordinal));
    }
}
