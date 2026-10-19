using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Abandoned mines and the guards of map dwellings (ADR-068).</summary>
public class AbandonedMineAndDwellingGuardTests
{
    // A 3×2 object at (2, 0) has its entrance at (3, 1); hero A starts below it with an army that wins.
    private static GameState World(ulong seed = 1)
    {
        var state = Create(new GameOptions(Seed: seed), ".........", ".........", "...A.....", ".........", "C.......B");
        state.GetHero(A).Army.Set(0, new CreatureStack("dragon-utopia.black-dragon", 40));
        return state;
    }

    private static Mine AbandonedMine(GameState state, ResourceType resource) =>
        Add<Mine>(state, BuiltInMapObjects.AbandonedMineId(resource), 2, 0);

    private static CreatureDwelling Dwelling(GameState state, string creature, PlayerId? owner = null)
    {
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 2, 0);
        dwelling.Creature = creature;
        dwelling.Owner = owner;
        return dwelling;
    }

    [Fact]
    public void AnAbandonedMine_IsGuardedByTroglodytes_RolledInItsRangeAtTheStart()
    {
        var gold = new List<int>();
        var other = new List<int>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var state = World(seed);
            var goldMine = AbandonedMine(state, ResourceType.Gold);
            var sawmill = Add<Mine>(state, BuiltInMapObjects.AbandonedMineId(ResourceType.Lumber), 5, 3);
            Start(state);
            Assert.Equal(BuiltInMapObjects.AbandonedMineGuard, goldMine.Guard!.Creature);
            gold.Add(goldMine.Guard.Count!.Value);
            other.Add(sawmill.Guard!.Count!.Value);
        }

        Assert.All(gold, c => Assert.InRange(c, 150, 295));
        Assert.All(other, c => Assert.InRange(c, 100, 245));
        Assert.True(gold.Distinct().Count() > 10);
    }

    [Fact]
    public void AGuardTheMapSets_IsKept()
    {
        var state = World();
        var mine = AbandonedMine(state, ResourceType.Ore);
        mine.Guard = new GuardStack(BuiltInCreatures.Wolf, 7);
        Start(state);
        Assert.Equal(new GuardStack(BuiltInCreatures.Wolf, 7), mine.Guard);
    }

    [Fact]
    public void TheGoldMineIsFoughtUnderground_AndBecomesANormalMineForTheWinner()
    {
        var state = World();
        state.Map.SetTerrain(new MapPosition(3, 1), TerrainType.Grass);
        var mine = AbandonedMine(state, ResourceType.Gold);
        var session = Start(state);
        var changed = new List<MapObjectChanged>();
        session.Events.Subscribe<MapObjectChanged>(changed.Add);

        session.Step(A, Direction.North);
        Assert.Equal(TerrainType.Subterranean, state.Battle!.Terrain);
        session.AutoResolve();

        var cleared = Assert.IsType<Mine>(state.GetObject(mine.Id));
        Assert.Equal(BuiltInMapObjects.MineId(ResourceType.Gold), cleared.Definition.Id);
        Assert.Null(cleared.Guard);
        Assert.Equal(Blue, cleared.Owner);
        Assert.Equal(mine.Position, cleared.Position);
        Assert.Contains(changed, e => e.Object == mine.Id);
        Assert.Equal(BuiltInMapObjects.MineId(ResourceType.Gold), state.Memory.Get(Blue, mine.Id)!.Definition.Id); // the player sees the new mine
    }

    [Fact]
    public void TheSawmillIsFoughtOnTheMapsTerrain()
    {
        var state = World();
        AbandonedMine(state, ResourceType.Lumber);
        var session = Start(state);

        session.Step(A, Direction.North);

        Assert.Equal(state.Map.GetTerrain(new MapPosition(3, 1)), state.Battle!.Terrain);
    }

    [Fact]
    public void AbandonedMines_AreOrdinaryMineObjectsInTheMapFormat()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 8));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.AbandonedMineId(ResourceType.Crystal), new(2, 2), out var id));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Contains("\"kind\": \"mine\"", json, StringComparison.Ordinal);
        Assert.Equal(BuiltInMapObjects.AbandonedMineId(ResourceType.Crystal), restored.GetObject(id).Definition.Id);
        Assert.Null(((Mine)restored.GetObject(id)).Guard); // rolled when a game starts
    }

    [Theory]
    [InlineData("haven.footman", null)]
    [InlineData("haven.archer", null)]
    [InlineData("haven.griffin", 9)]
    [InlineData("haven.lancer", 6)]
    [InlineData("haven.radiant-spirit", 3)]
    public void NeutralDwellings_OfTierThreeToFive_AreGuardedByTheirBaseCreature(string creature, int? guards)
    {
        var state = World();
        var dwelling = Dwelling(state, creature);
        Start(state);
        Assert.Equal(guards is { } count ? new GuardStack(creature, count) : null, dwelling.Guard);
    }

    [Fact]
    public void AnOwnedDwelling_IsNotGuarded()
    {
        var state = World();
        var dwelling = Dwelling(state, "haven.griffin", Red);
        Start(state);
        Assert.Null(dwelling.Guard);
    }

    [Fact]
    public void BeatingADwellingsGuard_FlagsIt_AndThenItRecruits()
    {
        var state = World();
        var dwelling = Dwelling(state, "haven.griffin");
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000);
        var session = Start(state);

        session.Step(A, Direction.North);
        Assert.NotNull(state.Battle);
        session.AutoResolve();

        Assert.Null(dwelling.Guard);
        Assert.Equal(Blue, dwelling.Owner);
        Assert.True(session.Execute(new EternalWars.Core.Domain.Commands.RecruitAtDwellingCommand(Blue, dwelling.Id, 1)).IsAccepted);
    }
}
