using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

/// <summary>Map format v13: the guard mode is stored per monster; older maps' map-wide mode applies to every monster (ADR-065).</summary>
public class GuardModeFormatTests
{
    private const string Monsters = """
        [ { "kind": "monster", "id": 1, "definition": "monster", "x": 1, "y": 1, "creature": "neutral.wolf", "count": 5 },
          { "kind": "monster", "id": 2, "definition": "monster", "x": 4, "y": 1, "creature": "neutral.wolf", "count": 5 } ]
        """;

    [Fact]
    public void PerMonsterModes_RoundTrip_AndClassicIsOmitted()
    {
        var state = BlankScenario.Create(8, 6);
        var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.Monster);
        state.AddObject(new MonsterGuard(state.NextObjectId(), definition, new(1, 1), BuiltInCreatures.Wolf, 5));
        state.AddObject(new MonsterGuard(state.NextObjectId(), definition, new(5, 1), BuiltInCreatures.Troll, 3, mode: GuardMode.Area));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal([GuardMode.Classic, GuardMode.Area], restored.ObjectsOf<MonsterGuard>().OrderBy(m => m.Id.Value).Select(m => m.Mode));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"mode\""));
        Assert.DoesNotContain("\"guardMode\"", json);
        Assert.Contains($"\"formatVersion\": {MapDocument.CurrentFormatVersion}", json);
    }

    [Theory]
    [InlineData("\"guardMode\": \"Area\",", GuardMode.Area)]
    [InlineData("\"guardMode\": \"Classic\",", GuardMode.Classic)]
    [InlineData("", GuardMode.Classic)]
    public void OldMap_AppliesItsMapWideMode_ToEveryMonster(string guardMode, GuardMode expected)
    {
        var json = $$"""
            {
              "formatVersion": 12, "name": "Old", "width": 8, "height": 4, "terrain": [ [ "gggggggg", "gggggggg", "gggggggg", "gggggggg" ] ],
              {{guardMode}}
              "players": [ { "name": "Blue", "isHuman": true } ],
              "objects": {{Monsters}}
            }
            """;

        var state = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(2, state.ObjectsOf<MonsterGuard>().Count());
        Assert.All(state.ObjectsOf<MonsterGuard>(), m => Assert.Equal(expected, m.Mode));
    }

    [Fact]
    public void OldMapLoadedAndSaved_KeepsTheModeOnEachMonster()
    {
        var json = $$"""
            {
              "formatVersion": 4, "name": "Old", "width": 8, "height": 4, "terrain": [ [ "gggggggg", "gggggggg", "gggggggg", "gggggggg" ] ],
              "guardMode": "Area",
              "players": [ { "name": "Blue", "isHuman": true } ],
              "objects": {{Monsters}}
            }
            """;

        var saved = MapSerializer.Serialize(MapConverter.ToDocument(MapConverter.ToGameState(MapSerializer.Deserialize(json))));
        var reloaded = MapConverter.ToGameState(MapSerializer.Deserialize(saved));

        Assert.DoesNotContain("\"guardMode\"", saved);
        Assert.All(reloaded.ObjectsOf<MonsterGuard>(), m => Assert.Equal(GuardMode.Area, m.Mode));
    }

    [Fact]
    public void CurrentMap_IgnoresAStrayMapWideMode_AndReadsEachMonstersOwn()
    {
        var json = $$"""
            {
              "formatVersion": 13, "name": "New", "width": 8, "height": 4, "terrain": [ [ "gggggggg", "gggggggg", "gggggggg", "gggggggg" ] ],
              "guardMode": "Area",
              "players": [ { "name": "Blue", "isHuman": true } ],
              "objects": [ { "kind": "monster", "id": 1, "definition": "monster", "x": 1, "y": 1, "creature": "neutral.wolf" },
                           { "kind": "monster", "id": 2, "definition": "monster", "x": 4, "y": 1, "creature": "neutral.wolf", "mode": "Area" } ]
            }
            """;

        var state = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal([GuardMode.Classic, GuardMode.Area], state.ObjectsOf<MonsterGuard>().OrderBy(m => m.Id.Value).Select(m => m.Mode));
    }
}
