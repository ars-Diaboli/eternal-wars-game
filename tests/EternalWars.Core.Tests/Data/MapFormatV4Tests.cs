using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

public class MapFormatV4Tests
{
    [Fact]
    public void ArmiesStatsGuardsAndChests_RoundTrip()
    {
        var original = DemoScenario.Create();
        original.ObjectsOf<MonsterGuard>().First().Mode = GuardMode.Area;
        original.ObjectsOf<TreasureChest>().First().FixedValues = new ChestValues(1234, 567);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(original));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(
            original.Heroes.Select(h => (h.Id, h.Attack, h.Defense, h.Tactics, Slots: string.Join(",", h.Army.Slots))),
            restored.Heroes.Select(h => (h.Id, h.Attack, h.Defense, h.Tactics, Slots: string.Join(",", h.Army.Slots))));
        Assert.Equal(
            original.ObjectsOf<MonsterGuard>().Select(m => (m.Id, m.Creature, m.Count, m.Disposition, m.NeverFlees, m.Mode)),
            restored.ObjectsOf<MonsterGuard>().Select(m => (m.Id, m.Creature, m.Count, m.Disposition, m.NeverFlees, m.Mode)));
        Assert.Equal(original.ObjectsOf<Mine>().Select(m => m.Guard), restored.ObjectsOf<Mine>().Select(m => m.Guard));
        Assert.Equal(new ChestValues(1234, 567), restored.ObjectsOf<TreasureChest>().First().FixedValues);
        Assert.Contains("\"kind\": \"monster\"", json);
    }

    [Fact]
    public void UnsetValues_AreOmitted_AndStayUnset()
    {
        var state = BlankScenario.Create(8, 6);
        state.AddObject(new TreasureChest(state.NextObjectId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.TreasureChest), new(1, 1)));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));

        Assert.DoesNotContain("\"gold\"", json);
        Assert.DoesNotContain("\"guardMode\"", json);
        Assert.Null(MapConverter.ToGameState(MapSerializer.Deserialize(json)).ObjectsOf<TreasureChest>().Single().FixedValues);
    }

    [Fact]
    public void Version3Map_LoadsChestValuesAsFixed_AndHeroesWithoutArmies()
    {
        const string json = """
            {
              "formatVersion": 3, "name": "Old", "width": 3, "height": 2, "terrain": [ [ "ggg", "ggg" ] ],
              "players": [ { "name": "Blue", "isHuman": true } ],
              "heroes": [ { "id": 1, "name": "H", "owner": 0, "x": 0, "y": 0 } ],
              "objects": [ { "kind": "chest", "id": 1, "definition": "chest", "x": 2, "y": 1, "gold": 1000, "experience": 500 } ]
            }
            """;

        var state = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(new ChestValues(1000, 500), state.ObjectsOf<TreasureChest>().Single().FixedValues);
        Assert.True(state.Heroes.Single().Army.IsEmpty);
    }

    [Theory]
    [InlineData("""{ "kind": "chest", "id": 1, "definition": "chest", "x": 2, "y": 1, "gold": 1000 }""", "[]")]
    [InlineData("""{ "kind": "pile", "id": 1, "definition": "pile.gold", "x": 2, "y": 1, "amount": 1 }""", """[ { "slot": 0, "creature": "haven.nobody", "count": 3 } ]""")]
    [InlineData("""{ "kind": "pile", "id": 1, "definition": "pile.gold", "x": 2, "y": 1, "amount": 1 }""", """[ { "slot": 7, "creature": "haven.footman", "count": 3 } ]""")]
    public void InvalidV4Data_IsAFormatError(string mapObject, string army)
    {
        var json = $$"""
            {
              "formatVersion": 4, "name": "Bad", "width": 3, "height": 2, "terrain": [ [ "ggg", "ggg" ] ],
              "players": [ { "name": "Blue", "isHuman": true } ],
              "heroes": [ { "id": 1, "name": "H", "owner": 0, "x": 0, "y": 0, "army": {{army}} } ],
              "objects": [ {{mapObject}} ]
            }
            """;

        Assert.Throws<FormatException>(() => MapConverter.ToGameState(MapSerializer.Deserialize(json)));
    }

    [Fact]
    public void HeroArmy_KeepsItsSlotPositions()
    {
        var state = BlankScenario.Create(8, 6);
        var hero = new Hero(
            state.NextHeroId(), "Gapped", state.Players[0].Id, new(1, 1),
            new Army([null, null, new CreatureStack(BuiltInCreatures.Devil, 2)]), attack: 3, tactics: SkillLevel.Expert);
        state.AddHero(hero);

        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(state))));

        var copy = restored.GetHero(hero.Id);
        Assert.Equal(new CreatureStack(BuiltInCreatures.Devil, 2), copy.Army[2]);
        Assert.Equal((3, SkillLevel.Expert), (copy.Attack, copy.Tactics));
    }
}
