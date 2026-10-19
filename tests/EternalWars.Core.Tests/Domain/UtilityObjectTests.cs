using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Sign, Observatory, Hill Fort, Town Gate, War Machine Factory and Prison (ADR-068).</summary>
public class UtilityObjectTests
{
    private static GameState World(GameOptions? options = null, int size = 12)
    {
        var rows = Enumerable.Range(0, size).Select(y => new string('.', size).ToCharArray()).ToArray();
        rows[2][3] = 'A';
        rows[size - 1][0] = 'C';
        rows[size - 1][size - 1] = 'B';
        var state = Create(options ?? GameOptions.Default, rows.Select(r => new string(r)).ToArray());
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000, Lumber: 20, Ore: 20, Crystal: 20, Mercury: 20, Sulfur: 20);
        return state;
    }

    private static void Visit(GameSession session, MapObject site, HeroId hero) => session.Interactions.Visit(session.State.GetHero(hero), site);

    [Fact]
    public void ASign_ShowsItsMessage_AndKeepsItInTheMap()
    {
        var editor = new MapEditorSession(BlankScenario.Create(8, 6));
        editor.PlaceObject(BuiltInMapObjects.Sign, new(2, 2), out var id);
        Assert.Equal(EditResult.Ok, editor.SetSignMessage(id, "Beware the swamp."));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario))));
        Assert.Equal("Beware the swamp.", ((SignPost)restored.GetObject(id)).Message);
        editor.Undo();
        Assert.Equal("", ((SignPost)editor.Scenario.GetObject(id)).Message);

        var state = World();
        var sign = Add<SignPost>(state, BuiltInMapObjects.Sign, 2, 1); // entrance (3, 1)
        sign.Message = "Hello";
        var session = Start(state);
        var read = new List<SignRead>();
        session.Events.Subscribe<SignRead>(read.Add);
        session.Step(A, Direction.North);
        Assert.Equal(new[] { new SignRead(A, sign.Id, "Hello") }, read);
    }

    [Fact]
    public void AnObservatory_RevealsTheTerrainWithinTwentyTiles()
    {
        var state = World(GameOptions.Default, size: 60);
        var observatory = Add<Observatory>(state, BuiltInMapObjects.Observatory, 2, 0); // entrance (3, 1)
        var session = Start(state, revealAll: false);
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(20, 10)));

        session.Step(A, Direction.North);

        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(20, 10)));
        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(3, 21)));
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(3, 30)));
        Assert.Contains(A, observatory.Visits.Heroes);
    }

    [Fact]
    public void TheHillFort_UpgradesTierOneToThreeStacks_ForOneAndAHalfTheDifference()
    {
        var state = World();
        var fort = Add<HillFort>(state, BuiltInMapObjects.HillFort, 2, 0); // entrance (3, 1)
        var hero = state.GetHero(A);
        hero.Army.Set(0, new CreatureStack("haven.footman", 10));
        hero.Army.Set(1, new CreatureStack("haven.lancer", 2));
        var session = Start(state);
        session.Step(A, Direction.North);

        var footman = state.Creatures.Get("haven.footman");
        var crusader = state.Creatures.Get(footman.UpgradesTo!);
        var expected = (int)Math.Ceiling((crusader.Cost.Gold - footman.Cost.Gold) * 10 * 1.5);
        var gold = state.GetPlayer(Blue).Resources.Gold;

        Assert.True(session.Execute(new UpgradeAtHillFortCommand(Blue, fort.Id, 0)).IsAccepted);
        Assert.Equal(new CreatureStack(crusader.Id, 10), hero.Army[0]);
        Assert.Equal(gold - expected, state.GetPlayer(Blue).Resources.Gold);
        Assert.Contains("tiers 1–3", session.Execute(new UpgradeAtHillFortCommand(Blue, fort.Id, 1)).Error!, StringComparison.Ordinal); // a tier 4 lancer
        Assert.False(session.Execute(new UpgradeAtHillFortCommand(Blue, fort.Id, 0)).IsAccepted); // already upgraded
        session.Step(A, Direction.South);
        Assert.False(session.Execute(new UpgradeAtHillFortCommand(Blue, fort.Id, 0)).IsAccepted); // not standing there
    }

    [Fact]
    public void TheTownGate_ListsOwnTowns_BlocksOccupiedOnes_AndMovesTheHeroForFree()
    {
        var state = World(size: 30);
        var gate = Add<TownGate>(state, BuiltInMapObjects.TownGate, 3, 0); // entrance (3, 1)
        var near = AddTown(state, TownIds.Haven, 10, 10, Blue);
        var far = AddTown(state, TownIds.Haven, 20, 20, Blue);
        AddTown(state, TownIds.Inferno, 10, 20, Red);
        state.GetHero(C).Position = far.VisitPosition; // blocks the far town
        var session = Start(state);
        var hero = state.GetHero(A);

        session.Step(A, Direction.North);
        var points = hero.MovementPoints;
        var decision = Assert.IsType<TownGateDecision>(state.PendingDecision);
        Assert.Equal(new[] { new TownGateOption(near.Id, near.Name, true), new TownGateOption(far.Id, far.Name, false) }, decision.Towns);
        Assert.False(session.Execute(new ChooseTownGateCommand(Blue, far.Id)).IsAccepted);

        Assert.True(session.Execute(new ChooseTownGateCommand(Blue, near.Id)).IsAccepted);

        Assert.Equal(near.VisitPosition, hero.Position);
        Assert.Equal(points, hero.MovementPoints);
        Assert.Null(state.PendingDecision);
    }

    [Fact]
    public void TheTownGate_MayBeLeftAlone_AndSendsAwayAPlayerWithoutTowns()
    {
        var state = World();
        var gate = Add<TownGate>(state, BuiltInMapObjects.TownGate, 3, 0);
        var session = Start(state);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);
        Visit(session, gate, A);
        Assert.Null(state.PendingDecision);
        Assert.Single(refused);

        var town = AddTown(state, TownIds.Haven, 5, 4, Blue);
        Visit(session, gate, A);
        Assert.True(session.Execute(new ChooseTownGateCommand(Blue, null)).IsAccepted);
        Assert.NotEqual(town.VisitPosition, state.GetHero(A).Position);
    }

    [Fact]
    public void TheFactory_SellsTheHerosOwnMachines_AtTheBlacksmithsPrices_NotTwice()
    {
        var state = World();
        var factory = Add<WarMachineFactory>(state, BuiltInMapObjects.WarMachineFactory, 2, 0);
        var hero = state.GetHero(A);
        var session = Start(state);
        session.Step(A, Direction.North);
        var gold = state.GetPlayer(Blue).Resources.Gold;

        Assert.True(session.Execute(new BuyWarMachineAtFactoryCommand(Blue, factory.Id, WarMachineSlot.Cart)).IsAccepted);

        Assert.Contains(WarMachineSlot.Cart, hero.WarMachines);
        Assert.Equal(gold - WarMachines.Cost(WarMachineSlot.Cart).Gold, state.GetPlayer(Blue).Resources.Gold);
        Assert.False(session.Execute(new BuyWarMachineAtFactoryCommand(Blue, factory.Id, WarMachineSlot.Cart)).IsAccepted);
        Assert.False(session.Execute(new BuyWarMachineAtFactoryCommand(Blue, factory.Id, WarMachineSlot.Offensive)).IsAccepted); // heroes start with it
    }

    [Fact]
    public void APrison_IsFilledAtTheStart_WithAHeroNotOnTheMap_AndALevelByTheOdds()
    {
        var levels = new List<int>();
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var state = World(new GameOptions(Seed: seed));
            state.GetHero(A).Definition = state.HeroRoster.All.First().Id;
            var prison = Add<Prison>(state, BuiltInMapObjects.Prison, 6, 0);
            Start(state);
            Assert.NotNull(prison.Hero);
            Assert.NotEqual(state.GetHero(A).Definition, prison.Hero);
            levels.Add(prison.Level!.Value);
        }

        Assert.All(levels, l => Assert.Contains(l, new[] { 1, 5, 10, 20, 30 }));
        Assert.InRange(levels.Count(l => l == 1), 40, 80);  // 30 %
        Assert.InRange(levels.Count(l => l == 30), 2, 25);  // 5 %
    }

    [Fact]
    public void FreeingAPrisoner_GivesTheHeroAtItsLevelWhereThePrisonStood()
    {
        var state = World();
        var roster = state.HeroRoster.All.First();
        var prison = Add<Prison>(state, BuiltInMapObjects.Prison, 2, 1); // the prison at (2, 1), its entrance (3, 1)
        prison.Hero = roster.Id;
        prison.Level = 10;
        var session = Start(state);
        var freed = new List<HeroFreed>();
        session.Events.Subscribe<HeroFreed>(freed.Add);

        session.Step(A, Direction.North);

        var e = Assert.Single(freed);
        var hero = state.GetHero(e.Hero);
        Assert.Equal(roster.Id, hero.Definition);
        Assert.Equal(Blue, hero.Owner);
        Assert.Equal(new MapPosition(2, 1), hero.Position);
        Assert.Equal(10, hero.Level);
        Assert.Equal(0, hero.PendingLevelUps);
        Assert.Null(state.FindObject(prison.Id));
        Assert.Null(state.PendingDecision); // the game chose the level-ups
    }

    [Fact]
    public void APrison_KeepsItsPrisoner_WhileThePlayerHasAllTheHeroesItMay()
    {
        var state = World();
        state.SetRules(ScenarioRules.Default with { MaxHeroes = 2 }); // Blue has A and C
        var prison = Add<Prison>(state, BuiltInMapObjects.Prison, 2, 1);
        var session = Start(state);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);

        session.Step(A, Direction.North);

        Assert.Single(refused);
        Assert.Same(prison, state.FindObject(prison.Id));
        Assert.Equal(2, state.HeroesOf(Blue).Count());
    }

    [Fact]
    public void ThePrison_InTheEditorAndTheMapFormat()
    {
        var editor = new MapEditorSession(BlankScenario.Create(10, 6));
        editor.PlaceObject(BuiltInMapObjects.Prison, new(2, 2), out var id);
        var hero = editor.Scenario.HeroRoster.All.First().Id;
        Assert.Equal(EditResult.InvalidValue, editor.SetPrison(id, "nobody", null));
        Assert.Equal(EditResult.Ok, editor.SetPrison(id, hero, 20));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = (Prison)MapConverter.ToGameState(MapSerializer.Deserialize(json)).GetObject(id);

        Assert.True(MapDocument.CurrentFormatVersion >= 19);
        Assert.Equal((hero, 20), (restored.Hero, restored.Level));
        Assert.Contains("\"prisonerLevel\": 20", json, StringComparison.Ordinal);
        ((Prison)editor.Scenario.GetObject(id)).Hero = "nobody";
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error && i.Message.Contains("unknown hero", StringComparison.Ordinal));
    }

    [Fact]
    public void TheAi_FreesAPrisonerNearby_AndStaysWhenATownGateAsks()
    {
        var state = World();
        var prison = Add<Prison>(state, BuiltInMapObjects.Prison, 2, 1);
        var session = Start(state);
        new AdventureAi(session, controlHumanSlots: true).PlayTurn();
        Assert.Null(state.FindObject(prison.Id));
        Assert.Equal(3, state.HeroesOf(Blue).Count());

        var other = World();
        var gate = Add<TownGate>(other, BuiltInMapObjects.TownGate, 3, 0);
        AddTown(other, TownIds.Haven, 5, 4, Blue);
        var gateSession = Start(other);
        Visit(gateSession, gate, A);
        var ai = new AdventureAi(gateSession, controlHumanSlots: true);
        ai.PlayTurn();
        Assert.Equal(0, ai.Refused);
        Assert.Null(other.PendingDecision);
    }
}
