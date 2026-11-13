using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

/// <summary>Map format v38: the arena switch and combat places (ADR-097, ADR-100); older maps still load, without the arena.</summary>
public class ArenaFormatTests
{
    [Fact]
    public void TheArenaSwitchAndCombatPlaces_RoundTrip()
    {
        var state = BlankScenario.Create(8, 6);
        state.SetRules(state.Rules with { Arena = true });
        state.AddObject(new CombatPlace(state.NextObjectId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.CombatPlace), new(2, 1)));
        state.AddObject(new CombatPlace(state.NextObjectId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.VerticalCombatPlace), new(5, 2)));

        var json = TestFormat.Save(state);
        var restored = TestFormat.Load(json);

        Assert.Contains("\"arena\": true", json);
        Assert.Contains("\"kind\": \"combatPlace\"", json);
        Assert.Contains($"\"formatVersion\": {MapDocument.CurrentFormatVersion}", json);
        Assert.True(restored.Rules.Arena);
        Assert.Equal(
            [(new MapPosition(3, 1), new MapPosition(2, 1)), (new MapPosition(5, 3), new MapPosition(5, 2))],
            restored.ObjectsOf<CombatPlace>().OrderBy(c => c.Id.Value).Select(c => (c.VisitPosition, c.AttackCell)));
    }

    [Fact]
    public void AnOrdinaryMap_WritesNoArenaSwitch()
    {
        var json = TestFormat.Save(BlankScenario.Create(8, 6));

        Assert.DoesNotContain("arena", json);
    }

    [Fact]
    public void AV37MapLoads_WithoutTheArena()
    {
        const string old = """
            {
              "formatVersion": 37, "name": "Old", "width": 8, "height": 4, "terrain": [ [ "gggggggg", "gggggggg", "gggggggg", "gggggggg" ] ],
              "players": [ { "name": "Blue", "isHuman": true } ],
              "rules": { "elimination": false },
              "objects": []
            }
            """;

        var state = TestFormat.Load(old);

        Assert.False(state.Rules.Arena);
        Assert.True(MapDocument.CurrentFormatVersion >= 38);
        Assert.Equal(MapDocument.CurrentFormatVersion, MapConverter.ToDocument(state).FormatVersion);
    }
}
