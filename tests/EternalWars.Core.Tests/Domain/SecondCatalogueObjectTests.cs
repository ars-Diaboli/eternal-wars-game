using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
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
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The second catalogue's deposits, Windows and Eyes of the Magi, Keymasters and border posts, the map tavern, garrisons and banks (ADR-069 …
/// ADR-071, ADR-082).
/// </summary>
public class SecondCatalogueObjectTests
{
    private static string Red(string kind) => BuiltInMapObjects.ColouredId(kind, ObjectColour.Red);

    private static List<T> Record<T>(GameSession session)
        where T : IEvent
    {
        var list = new List<T>();
        session.Events.Subscribe<T>(list.Add);
        return list;
    }

    // Deposits -----------------------------------------------------------------------------------------

    [Fact]
    public void ADeposit_BecomesItsMine_ForThePlayerWhoPays()
    {
        var state = Create(".......", ".......", "...A...", "......B");
        var vein = Add<Deposit>(state, DepositRules.Id(ResourceType.Crystal), 2, 0); // entrance (3, 1)
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000, Ore: 8);
        var session = Start(state);
        var bought = Record<DepositBought>(session);

        session.Step(A, Direction.North);
        Assert.Equal(new DepositDecision(A, vein.Id, new ResourceSet(Gold: 3000, Ore: 5)), state.PendingDecision);
        session.Execute(new AnswerDepositCommand(Blue, Buy: true)).ThrowIfRejected();

        var mine = Assert.IsType<Mine>(state.GetObject(vein.Id));
        Assert.Equal((BuiltInMapObjects.MineId(ResourceType.Crystal), Blue), (mine.Definition.Id, mine.Owner));
        Assert.Equal(new ResourceSet(Gold: 2000, Ore: 3), state.GetPlayer(Blue).Resources);
        Assert.Single(bought);
    }

    [Fact]
    public void ADeposit_MayBeLeft_AndIsRefusedWithoutThePrice()
    {
        var state = Create(".......", ".......", "...A...", "......B");
        var vein = Add<Deposit>(state, DepositRules.Id(ResourceType.Gold), 2, 0);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000, Ore: 10);
        var session = Start(state);
        var refused = Record<MapObjectRefused>(session);

        session.Step(A, Direction.North);
        session.Execute(new AnswerDepositCommand(Blue, Buy: false)).ThrowIfRejected();
        Assert.IsType<Deposit>(state.GetObject(vein.Id));
        Assert.Equal(new ResourceSet(Gold: 5000, Ore: 10), state.GetPlayer(Blue).Resources);

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 4999, Ore: 10);
        session.Interactions.Visit(state.GetHero(A), vein);
        Assert.Null(state.PendingDecision);
        Assert.Contains("5000 gold", Assert.Single(refused).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheComputer_BuysADepositItCanAfford()
    {
        var state = Create(".......", ".......", "...A...", "......B");
        var vein = Add<Deposit>(state, DepositRules.Id(ResourceType.Lumber), 2, 0);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000, Ore: 20);
        state.GetPlayer(Blue).IsHuman = false;
        var session = Start(state);

        new AdventureAi(session).PlayTurn();

        Assert.Equal(Blue, Assert.IsType<Mine>(state.GetObject(vein.Id)).Owner);
    }

    // Windows and Eyes of the Magi ----------------------------------------------------------------------

    [Fact]
    public void AWindowOfTheMagi_RevealsAroundEveryEyeOfItsColour_OnEveryVisit()
    {
        var rows = Enumerable.Repeat(new string('.', 40), 12).ToArray();
        rows[2] = "...A" + new string('.', 36);
        rows[11] = new string('.', 39) + "B";
        var state = Create(rows);
        var window = Add<MagiWindow>(state, Red(BuiltInMapObjects.MagiWindow), 2, 0); // entrance (3, 1)
        Add<MagiEye>(state, Red(BuiltInMapObjects.MagiEye), 30, 6);
        Add<MagiEye>(state, BuiltInMapObjects.ColouredId(BuiltInMapObjects.MagiEye, ObjectColour.Blue), 20, 10);
        var session = Start(state, revealAll: false);
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(30, 6)));

        session.Step(A, Direction.North);

        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(30 + MagiWindow.Radius, 6)));
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(20, 10))); // another colour
        var revealed = Record<SurroundingsRevealed>(session);
        session.Interactions.Visit(state.GetHero(A), window);
        Assert.Single(revealed); // every visit looks again
    }

    [Fact]
    public void TheEditor_RefusesAFifthEyeOfAColour_AndTheValidatorFlagsLonelyWindowsAndPosts()
    {
        var editor = new MapEditorSession(BlankScenario.Create(30, 10));
        for (var i = 0; i < MagiWindow.MaxEyes; i++)
        {
            Assert.Equal(EditResult.Ok, editor.PlaceObject(Red(BuiltInMapObjects.MagiEye), new MapPosition(2 * i, 0), out _));
        }

        Assert.Equal(EditResult.TooManyEyes, editor.PlaceObject(Red(BuiltInMapObjects.MagiEye), new MapPosition(9, 0), out _));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.ColouredId(BuiltInMapObjects.MagiEye, ObjectColour.Green), new MapPosition(9, 0), out _));

        editor.PlaceObject(BuiltInMapObjects.ColouredId(BuiltInMapObjects.MagiWindow, ObjectColour.Black), new MapPosition(12, 2), out _);
        editor.PlaceObject(Red(BuiltInMapObjects.BorderGate), new MapPosition(16, 2), out _);
        var issues = MapValidator.Validate(editor.Scenario).Select(i => i.Message).ToList();
        Assert.Contains(issues, m => m.Contains("no Eye of its colour", StringComparison.Ordinal));
        Assert.Contains(issues, m => m.Contains("no Keymaster's Tent", StringComparison.Ordinal));
    }

    // Keymasters and border posts ----------------------------------------------------------------------

    /// <summary>A wall of rock on row 3 with one gap at (5, 3) holding the border post; a red Keymaster's Tent at (2, 0), entrance (3, 1).</summary>
    private static (GameState State, GameSession Session, BorderPost Post) Border(string post)
    {
        var state = Create("..........", "..........", "A....C....", "#####.####", "..........", ".........B");
        Add<KeymasterTent>(state, Red(BuiltInMapObjects.KeymasterTent), 2, 0);
        var border = Add<BorderPost>(state, Red(post), 5, 3);
        return (state, Start(state), border);
    }

    [Fact]
    public void ABorderGuard_BarsHeroesWithoutTheKey_AndLeavesAfterTheFirstKeyHolder()
    {
        var (state, session, guard) = Border(BuiltInMapObjects.BorderGuard);
        var refused = Record<MapObjectRefused>(session);

        Assert.Equal(MoveResult.NoPath, session.MoveTo(C, new MapPosition(5, 5)).StopReason);
        Assert.Equal(MoveResult.Blocked, session.Step(C, Direction.South));
        Assert.Contains("red key", Assert.Single(refused).Reason, StringComparison.Ordinal);

        session.MoveTo(A, new MapPosition(3, 1));
        Assert.Contains(ObjectColour.Red, state.GetPlayer(Blue).Keys);

        var outcome = session.MoveTo(C, new MapPosition(5, 5));
        Assert.True(outcome.Arrived);
        Assert.Null(state.FindObject(guard.Id));
    }

    [Fact]
    public void ABorderGate_Stays_AndLetsEveryKeyHolderThrough_ButNoOtherPlayer()
    {
        var (state, session, gate) = Border(BuiltInMapObjects.BorderGate);
        session.MoveTo(A, new MapPosition(3, 1));
        var keys = Record<KeyReceived>(session);
        session.Interactions.Visit(state.GetHero(A), state.ObjectsOf<KeymasterTent>().Single());
        Assert.Empty(keys); // a second visit gives nothing new

        Assert.True(session.MoveTo(C, new MapPosition(5, 5)).Arrived);
        Assert.NotNull(state.FindObject(gate.Id));
        Assert.True(session.MoveTo(A, new MapPosition(4, 4)).Arrived);

        session.EndTurn();
        Assert.Equal(MoveResult.NoPath, session.MoveTo(B, new MapPosition(5, 2)).StopReason); // Red has no key
    }

    // The map tavern -----------------------------------------------------------------------------------

    [Fact]
    public void TheMapTavern_HiresFromAWeeklyPoolSharedByAllPlayers_OntoTheCellBelowTheEntrance()
    {
        var state = Create(".......", ".......", "...A...", ".......", "......B");
        var tavern = Add<MapTavern>(state, BuiltInMapObjects.MapTavern, 2, 0); // entrance (3, 1)
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000);
        var session = Start(state);
        session.Step(A, Direction.North);

        var offers = MapTavernRules.Offers(state, tavern);
        Assert.Equal(MapTavernRules.OfferCount, offers.Count);
        Assert.Contains(offers.Select(o => o.Town).Distinct(), _ => true);
        var hired = offers[1];
        session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, hired.Id)).ThrowIfRejected();

        var hero = Assert.Single(state.Heroes, h => h.Definition == hired.Id);
        Assert.Equal((Blue, new MapPosition(3, 2)), (hero.Owner, hero.Position));
        Assert.False(hero.Army.IsEmpty);
        Assert.Equal(10_000 - 2500, state.GetPlayer(Blue).Resources.Gold);
        var after = MapTavernRules.Offers(state, tavern);
        Assert.DoesNotContain(after, h => h.Id == hired.Id);
        Assert.Equal(offers.Where(o => o.Id != hired.Id), after.Take(3));

        state.Date = new GameDate(8);
        Assert.NotEqual(after.Select(o => o.Id), MapTavernRules.Offers(state, tavern).Select(o => o.Id)); // a new week, a new pool
    }

    [Fact]
    public void TheMapTavern_RefusesAtTheHeroLimit_WithoutAFreeCell_OrFromAfar()
    {
        var state = Create(".......", ".......", "...A...", "...C...", "......B");
        var tavern = Add<MapTavern>(state, BuiltInMapObjects.MapTavern, 2, 0);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000);
        var session = Start(state);
        var offer = MapTavernRules.Offers(state, tavern)[0].Id;
        Assert.False(session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, offer)).IsAccepted); // nobody on the entrance

        session.Step(A, Direction.North);
        state.GetPlayer(Blue).MaxHeroes = 2;
        Assert.Contains("at most 2", session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, offer)).Error!, StringComparison.Ordinal);

        state.GetPlayer(Blue).MaxHeroes = null;
        session.Step(C, Direction.North); // C takes the cell below; the next free neighbour is used
        session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, offer)).ThrowIfRejected();
        Assert.NotEqual(new MapPosition(3, 2), state.Heroes.Single(h => h.Definition == offer).Position);
    }

    // Garrisons ----------------------------------------------------------------------------------------

    /// <summary>A rock wall on row 3 with a horizontal garrison at (4, 3) whose entrance (5, 3) is the only way through.</summary>
    private static (GameState State, GameSession Session, MapGarrison Garrison) Pass(Army? troops = null)
    {
        var state = Create("..........", "..........", ".....A....", "####...###", "..........", ".........B");
        var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.Garrison);
        var garrison = new MapGarrison(state.NextObjectId(), definition, new MapPosition(4, 3), troops: troops ?? Army.Of(new CreatureStack("neutral.wolf", 5)));
        state.AddObject(garrison);
        state.GetHero(A).Army.Set(1, new CreatureStack("dragon-utopia.black-dragon", 10));
        return (state, Start(state), garrison);
    }

    [Fact]
    public void ANeutralGarrison_MustBeBeaten_ThenItsOwnerPassesBothWays()
    {
        var (state, session, garrison) = Pass();

        Assert.Null(session.Movement.FindPath(A, new MapPosition(5, 5))); // the fight ends any route at the entrance
        Assert.NotNull(session.Movement.FindPath(A, new MapPosition(5, 3)));

        session.MoveTo(A, new MapPosition(5, 3));
        Assert.NotNull(state.Battle);
        session.AutoResolve();

        Assert.Equal(Blue, garrison.Owner);
        Assert.True(garrison.Troops.IsEmpty);
        Assert.True(session.MoveTo(A, new MapPosition(5, 5)).Arrived);
        Assert.True(session.MoveTo(A, new MapPosition(5, 1)).Arrived);
    }

    [Fact]
    public void TheOwner_LeavesAndTakesTroops_AndTheTroopsDefendTheGarrison()
    {
        var (state, session, garrison) = Pass(new Army());
        session.MoveTo(A, new MapPosition(5, 3));
        Assert.Equal(Blue, garrison.Owner); // an empty garrison is flagged at once

        session.Execute(new MoveGarrisonStackCommand(Blue, garrison.Id, 1, ToGarrison: true)).ThrowIfRejected();
        Assert.Equal(new CreatureStack("dragon-utopia.black-dragon", 10), garrison.Troops[0]);
        Assert.False(session.Execute(new MoveGarrisonStackCommand(Blue, garrison.Id, 0, ToGarrison: true)).IsAccepted); // keeps one stack

        session.MoveTo(A, new MapPosition(5, 1));
        session.EndTurn();
        var bravo = state.GetHero(B);
        Assert.Null(session.Movement.FindPath(B, new MapPosition(5, 1))); // not through an enemy's garrison
        session.MoveTo(B, new MapPosition(5, 3));
        session.AutoResolve();

        Assert.DoesNotContain(state.Heroes, h => h.Id == bravo.Id);
        Assert.Equal(Blue, garrison.Owner);
        Assert.Equal("dragon-utopia.black-dragon", garrison.Troops[0]!.Creature);
    }

    [Fact]
    public void GarrisonsAndTheOtherNewObjects_AreInTheMapFormat_AndTheOldRuinsLoadAsTheCrypt()
    {
        var state = BlankScenario.Create(30, 10);
        var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.VerticalGarrison);
        state.AddObject(new MapGarrison(state.NextObjectId(), definition, new MapPosition(1, 1), new PlayerId(0), Army.Of(new CreatureStack("neutral.wolf", 7))));
        Add<Deposit>(state, DepositRules.Id(ResourceType.Sulfur), 4, 1);
        Add<MagiEye>(state, Red(BuiltInMapObjects.MagiEye), 8, 1);
        Add<MagiWindow>(state, Red(BuiltInMapObjects.MagiWindow), 10, 1);
        Add<KeymasterTent>(state, Red(BuiltInMapObjects.KeymasterTent), 13, 1);
        Add<BorderPost>(state, Red(BuiltInMapObjects.BorderGate), 16, 1);
        Add<MapTavern>(state, BuiltInMapObjects.MapTavern, 18, 1);
        Add<CreatureBank>(state, BuiltInMapObjects.DragonCity, 22, 1);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(state.Objects.Select(o => (o.Id, o.Definition.Id, o.Position)), restored.Objects.Select(o => (o.Id, o.Definition.Id, o.Position)));
        var garrison = restored.ObjectsOf<MapGarrison>().Single();
        Assert.Equal((new PlayerId(0), new CreatureStack("neutral.wolf", 7)), (garrison.Owner!.Value, garrison.Troops[0]!));

        var old = json.Replace(BuiltInMapObjects.DragonCity, "bank.ruins", StringComparison.Ordinal).Replace("\"formatVersion\": 23", "\"formatVersion\": 22", StringComparison.Ordinal);
        Assert.Equal(BuiltInMapObjects.Crypt, MapConverter.ToGameState(MapSerializer.Deserialize(old)).ObjectsOf<CreatureBank>().Single().Definition.Id);
    }

    [Fact]
    public void TheEditor_SetsAMapGarrisonsOwnerAndTroops_Undoably()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 8));
        editor.PlaceObject(BuiltInMapObjects.Garrison, new MapPosition(2, 2), out var id);

        Assert.Equal(EditResult.Ok, editor.SetMapGarrisonOwner(id, new PlayerId(0)));
        Assert.Equal(EditResult.Ok, editor.SetGarrisonSlot(id, 2, new CreatureStack("neutral.wolf", 4)));
        var garrison = (MapGarrison)editor.Scenario.GetObject(id);
        Assert.Equal((new PlayerId(0), 4), (garrison.Owner!.Value, garrison.Troops[2]!.Count));

        editor.Undo();
        editor.Undo();
        Assert.Equal((null, true), (garrison.Owner, garrison.Troops.IsEmpty));
    }

    // Banks --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(BuiltInMapObjects.Ruins, "stronghold.orc")]
    [InlineData(BuiltInMapObjects.Lair, "dungeon.spectator")]
    [InlineData(BuiltInMapObjects.Keep, "haven.pikeman")]
    [InlineData(BuiltInMapObjects.Hideout, "neutral.bandit")]
    [InlineData(BuiltInMapObjects.ForestGlen, "neutral.wolf")]
    [InlineData(BuiltInMapObjects.DragonCity, "dragon-utopia.black-dragon")]
    public void TheNewBanks_AreGuardedByTheirCreatures(string definition, string creature)
    {
        var state = Create("........", "........", "...A....", ".......B");
        var bank = Add<CreatureBank>(state, definition, 2, 0);
        Start(state);

        Assert.True(bank.IsGuarded);
        Assert.Contains(bank.Guard, s => s.Creature == creature);
        Assert.False(bank.Reward.IsEmpty);
    }

    [Fact]
    public void DragonCity_PaysARelicAndMajorArtifacts()
    {
        var state = Create("........", "........", "...A....", ".......B");
        state.GetHero(A).Army.Set(0, new CreatureStack("dragon-utopia.black-dragon", 80));
        var city = Add<CreatureBank>(state, BuiltInMapObjects.DragonCity, 2, 0);
        var session = Start(state);
        var majors = city.MoreArtifacts.Count;
        Assert.Equal(ArtifactTier.Relic, city.RewardArtifact);
        Assert.InRange(majors, 1, 3);

        session.Step(A, Direction.North);
        session.AutoResolve();

        Assert.False(city.IsGuarded);
        var hero = state.GetHero(A);
        Assert.Equal(1 + majors, hero.Artifacts.AllWorn.Count() + hero.Artifacts.Backpack.Count);
    }
}
