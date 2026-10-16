using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Map dwellings and caravans (ADR-054).</summary>
public class CaravanTests
{
    private const string Footman = BuiltInCreatures.Footman;

    /// <summary>Open grass, <paramref name="width"/> wide; hero A at (3, 7) below the first town, Bravo in the far corner.</summary>
    private static string[] Grass(int width, Func<int, int, char?>? special = null) =>
        Enumerable.Range(0, 12).Select(y => string.Concat(Enumerable.Range(0, width).Select(x =>
            special?.Invoke(x, y) ?? (x == 3 && y == 7 ? 'A' : x == width - 1 && y == 11 ? 'B' : '.')))).ToArray();

    /// <summary>A town with a caravan at the left, an owned dwelling of footmen at (<paramref name="dwellingX"/>, 3).</summary>
    private static (GameSession Session, Town Town, CreatureDwelling Dwelling) Setup(int width = 40, int dwellingX = 20, Func<int, int, char?>? special = null)
    {
        var state = Create(Grass(width, special));
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Caravan, 1);
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, dwellingX, 3);
        dwelling.Creature = Footman;
        dwelling.Owner = Blue;
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 100_000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);
        return (session, town, dwelling);
    }

    private static void NextDay(GameSession session)
    {
        session.EndTurn();
        session.EndTurn();
    }

    [Fact]
    public void ADwelling_OpensWithOneWeek_AndGrowsEveryMonday_OwnedOrNot()
    {
        var state = Create(Grass(20));
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 10, 3);
        dwelling.Creature = Footman;
        var session = Start(state);
        var growth = state.Creatures.Get(Footman).WeeklyGrowth;
        var grew = new List<DwellingCreaturesGrew>();
        session.Events.Subscribe<DwellingCreaturesGrew>(grew.Add);

        Assert.Null(dwelling.Owner);
        Assert.Equal(growth, dwelling.Available);
        for (var day = 1; day <= 7; day++)
        {
            NextDay(session);
        }

        Assert.Equal(growth * 2, dwelling.Available);
        Assert.Equal(new DwellingCreaturesGrew(dwelling.Id, growth * 2), Assert.Single(grew));
    }

    [Fact]
    public void AVisitingHero_FlagsTheDwelling_AndRecruitsFromIt()
    {
        var state = Create(Grass(20));
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 6, 5); // entrance (7, 6)
        dwelling.Creature = Footman;
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000);
        var changes = new List<DwellingOwnerChanged>();
        session.Events.Subscribe<DwellingOwnerChanged>(changes.Add);

        Assert.False(session.Execute(new RecruitAtDwellingCommand(Blue, dwelling.Id, 1)).IsAccepted); // nobody there yet
        session.MoveTo(A, dwelling.VisitPosition);
        Assert.Equal(Blue, dwelling.Owner);
        Assert.Equal(new DwellingOwnerChanged(dwelling.Id, null, Blue), Assert.Single(changes));

        var footman = state.Creatures.Get(Footman);
        var waiting = dwelling.Available!.Value;
        session.Execute(new RecruitAtDwellingCommand(Blue, dwelling.Id, 4)).ThrowIfRejected();

        Assert.Equal(waiting - 4, dwelling.Available);
        Assert.Equal(14, state.GetHero(A).Army.Stacks.Single(s => s.Stack.Creature == Footman).Stack.Count);
        Assert.Equal(10_000 - (4 * footman.Cost.Gold), state.GetPlayer(Blue).Resources.Gold);
        Assert.False(session.Execute(new RecruitAtDwellingCommand(Blue, dwelling.Id, waiting)).IsAccepted); // more than is left
    }

    [Fact]
    public void RecruitingAtADwelling_UsesTheHeroesOwnStewardship()
    {
        var state = Create(Grass(20));
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 6, 5);
        dwelling.Creature = Footman;
        state.GetHero(A).Skills.Set(BuiltInSkills.Stewardship, SkillLevel.Expert);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000);
        session.MoveTo(A, dwelling.VisitPosition);

        session.Execute(new RecruitAtDwellingCommand(Blue, dwelling.Id, 10)).ThrowIfRejected();

        var gold = state.Creatures.Get(Footman).Cost.Gold;
        Assert.Equal(10 * ((gold * 80 + 99) / 100), 10_000 - state.GetPlayer(Blue).Resources.Gold);
    }

    [Fact]
    public void ACaravan_HiresFromAnOwnedDwelling_AndArrivesAfterTheTravelTime()
    {
        var (session, town, dwelling) = Setup();
        var state = session.State;
        var footman = state.Creatures.Get(Footman);
        var waiting = dwelling.Available!.Value;
        var route = CaravanRules.FindRoute(state, Blue, dwelling.VisitPosition, town.VisitPosition, footman)!;
        var events = new List<IEvent>();
        session.Events.Subscribe<CaravanDispatched>(events.Add);
        session.Events.Subscribe<CaravanArrived>(events.Add);

        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 5)).ThrowIfRejected();

        Assert.Equal(waiting - 5, dwelling.Available);
        Assert.Equal(100_000 - (5 * footman.Cost.Gold), state.GetPlayer(Blue).Resources.Gold);
        var caravan = Assert.Single(state.Caravans);
        Assert.Equal(route.Days, caravan.DaysLeft);
        Assert.InRange(route.Days, 2, CaravanRules.MaxDays);
        Assert.Equal(0, town.Garrison.StackCount);

        for (var day = 1; day < route.Days; day++)
        {
            NextDay(session);
            Assert.Single(state.Caravans);
            Assert.Equal(0, town.Garrison.StackCount);
        }

        NextDay(session);
        Assert.Empty(state.Caravans);
        Assert.Equal(5, Assert.Single(town.Garrison.Stacks).Stack.Count);
        Assert.IsType<CaravanDispatched>(events[0]);
        Assert.Equal(new CaravanArrived(caravan.Id, town.Id, Footman, 5), events[1]);
    }

    [Fact]
    public void TheTownsGovernor_MakesTheCaravanCheaper()
    {
        var (session, town, dwelling) = Setup();
        var state = session.State;
        state.GetHero(A).Skills.Set(BuiltInSkills.Stewardship, SkillLevel.Advanced);
        town.Governor = A;
        var gold = state.Creatures.Get(Footman).Cost.Gold;

        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 10)).ThrowIfRejected();

        Assert.Equal(10 * ((gold * 85 + 99) / 100), 100_000 - state.GetPlayer(Blue).Resources.Gold);
    }

    [Fact]
    public void ACaravan_NeedsTheBuilding_AnOwnedDwelling_RoomAndMoney()
    {
        var (session, town, dwelling) = Setup();
        town.SetLevel(BuiltInBuildings.Caravan, 0);
        Assert.Contains("no caravan", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).Error);

        town.SetLevel(BuiltInBuildings.Caravan, 1);
        dwelling.Owner = null;
        Assert.Contains("not yours", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).Error);

        dwelling.Owner = Blue;
        Assert.Contains("wait in the dwelling", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, dwelling.Available!.Value + 1)).Error);
        Assert.False(session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 0)).IsAccepted);

        session.State.GetPlayer(Blue).Resources = ResourceSet.Empty;
        Assert.Contains("Not enough", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).Error);

        session.State.GetPlayer(Blue).Resources = new ResourceSet(Gold: 100_000);
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            town.Garrison.Set(slot, new CreatureStack(session.State.Creatures.All.Skip(slot + 20).First().Id, 1));
        }

        Assert.Contains("no free slot", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).Error);
        Assert.Empty(session.State.Caravans);
    }

    [Fact]
    public void ADwellingMoreThanFiveDaysAway_CannotSupplyTheTown_AndNothingIsPaid()
    {
        var (session, town, dwelling) = Setup(width: 110, dwellingX: 100);
        var gold = session.State.GetPlayer(Blue).Resources.Gold;

        var result = session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1));

        Assert.Contains("days away", result.Error);
        Assert.Equal(gold, session.State.GetPlayer(Blue).Resources.Gold);
    }

    [Fact]
    public void WithoutAnOpenRoute_NoCaravanIsSent_AndABlockedRouteSendsItBackWithTheMoney()
    {
        // A rock wall across the map at x = 12 with a one-tile gap at y = 5.
        char? Wall(int x, int y) => x == 12 && y != 5 ? '#' : null;
        var (session, town, dwelling) = Setup(special: Wall);
        var state = session.State;
        var waiting = dwelling.Available!.Value;
        var gold = state.GetPlayer(Blue).Resources.Gold;
        var returned = new List<CaravanReturned>();
        session.Events.Subscribe<CaravanReturned>(returned.Add);

        // An enemy hero in the gap: no route, so the order is refused.
        state.GetHero(B).Position = new MapPosition(12, 5);
        Assert.Contains("No open route", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 3)).Error);

        state.GetHero(B).Position = new MapPosition(39, 11);
        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 3)).ThrowIfRejected();
        Assert.Equal(gold - (3 * state.Creatures.Get(Footman).Cost.Gold), state.GetPlayer(Blue).Resources.Gold);

        state.GetHero(B).Position = new MapPosition(12, 5); // the way closes while the caravan travels
        NextDay(session);

        Assert.Empty(state.Caravans);
        Assert.Equal(waiting, dwelling.Available);
        Assert.Equal(gold + TownRules.DailyIncome(state, town).Gold, state.GetPlayer(Blue).Resources.Gold); // refunded, plus the day's income
        Assert.Equal(new CaravanReturned(1, dwelling.Id, Footman, 3), Assert.Single(returned));
    }

    [Fact]
    public void AMonsterOrItsGuardedArea_BlocksTheRoute()
    {
        char? Wall(int x, int y) => x == 12 && y != 5 ? '#' : null;
        var (session, town, dwelling) = Setup(special: Wall);
        var wolf = Add<MonsterGuard>(session.State, BuiltInMapObjects.Monster, 12, 6); // guards (11..13, 5..7), so the gap too

        Assert.Contains("No open route", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).Error);
        Assert.NotNull(wolf);
    }

    [Fact]
    public void WhenTheDestinationIsLost_TheCaravansCreaturesScatter()
    {
        var (session, town, dwelling) = Setup();
        var lost = new List<CaravanLost>();
        session.Events.Subscribe<CaravanLost>(lost.Add);
        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 5)).ThrowIfRejected();
        var waiting = dwelling.Available;

        town.Owner = Red; // captured while the caravan travels
        NextDay(session);

        Assert.Empty(session.State.Caravans);
        Assert.Equal(waiting, dwelling.Available);
        Assert.Equal(0, town.Garrison.StackCount);
        Assert.Equal(5, Assert.Single(lost).Count);
    }

    [Fact]
    public void ACaravanWithNoRoomAtTheGates_WaitsUntilThereIsOne()
    {
        var (session, town, dwelling) = Setup();
        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 5)).ThrowIfRejected();
        var other = session.State.Creatures.All.Where(c => c.Id != Footman).Take(Army.SlotCount).ToList();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            town.Garrison.Set(slot, new CreatureStack(other[slot].Id, 1)); // the garrison fills up meanwhile
        }

        for (var day = 0; day < CaravanRules.MaxDays + 1; day++)
        {
            NextDay(session);
        }

        Assert.Single(session.State.Caravans);
        Assert.Equal(0, session.State.Caravans[0].DaysLeft);

        town.Garrison.Set(0, null);
        NextDay(session);
        Assert.Empty(session.State.Caravans);
        Assert.Equal(5, town.Garrison.Stacks.Single(s => s.Stack.Creature == Footman).Stack.Count);
    }

    [Fact]
    public void Troops_CanBeSentToAnotherOwnTown_ByCaravan()
    {
        var state = Create(Grass(40));
        var first = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var second = AddTown(state, TownIds.Haven, 12, 0, Blue);
        first.SetLevel(BuiltInBuildings.Caravan, 1);
        first.Garrison.Set(2, new CreatureStack(Footman, 20));
        var session = Start(state);

        Assert.Contains("another own town", session.Execute(new SendTroopsCommand(Blue, first.Id, 2, first.Id)).Error);
        Assert.Contains("slot is empty", session.Execute(new SendTroopsCommand(Blue, first.Id, 0, second.Id)).Error);
        Assert.Contains("no caravan", session.Execute(new SendTroopsCommand(Blue, second.Id, 0, first.Id)).Error);

        session.Execute(new SendTroopsCommand(Blue, first.Id, 2, second.Id, 15)).ThrowIfRejected();
        Assert.Equal(5, first.Garrison[2]!.Count);
        var caravan = Assert.Single(state.Caravans);
        Assert.Equal(ResourceSet.Empty, caravan.Paid);

        for (var day = 0; day < caravan.DaysLeft; day++)
        {
            NextDay(session);
        }

        Assert.Empty(state.Caravans);
        Assert.Equal(15, Assert.Single(second.Garrison.Stacks).Stack.Count);
    }

    [Fact]
    public void AGameWithCaravans_ReplaysExactly()
    {
        var (session, town, dwelling) = Setup();
        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 5)).ThrowIfRejected();
        NextDay(session);
        var digest = GameStateDigest.Describe(session.State);

        Assert.Contains("caravan 1 owner 0", digest);
        Assert.Single(session.State.Caravans);
    }

    [Fact]
    public void ADwelling_SurvivesTheMapFormat_AndTheEditorEditsIt()
    {
        var state = Create(Grass(20));
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 6, 5);
        dwelling.Creature = Footman;
        dwelling.Owner = Blue;
        dwelling.Available = 12;

        var loaded = MapConverter.ToGameState(MapConverter.ToDocument(state));

        var copy = Assert.Single(loaded.ObjectsOf<CreatureDwelling>());
        Assert.Equal((Footman, Blue, 12), (copy.Creature, copy.Owner, copy.Available));
        Assert.Equal(12, MapDocument.CurrentFormatVersion);

        var editor = new MapEditorSession(loaded);
        Assert.Equal(EditResult.Ok, editor.SetDwelling(copy.Id, BuiltInCreatures.Wolf, Red, null));
        Assert.Equal((BuiltInCreatures.Wolf, Red, (int?)null), (copy.Creature, copy.Owner, copy.Available));
        Assert.Equal(EditResult.UnknownCreature, editor.SetDwelling(copy.Id, "nope", null, null));
        Assert.Equal(EditResult.UnknownPlayer, editor.SetDwelling(copy.Id, Footman, new PlayerId(9), null));
        Assert.Equal(EditResult.InvalidValue, editor.SetDwelling(copy.Id, Footman, null, -1));
        Assert.Equal(EditResult.InvalidTarget, editor.SetDwelling(new MapObjectId(99), Footman, null, null));

        editor.Undo();
        Assert.Equal((Footman, Blue, 12), (copy.Creature, copy.Owner, copy.Available));
    }

    [Fact]
    public void ADefeatedPlayer_LosesItsDwellingsAndCaravans()
    {
        var (session, town, dwelling) = Setup();
        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 5)).ThrowIfRejected();
        var state = session.State;

        // Blue's town and hero are gone: it is out, and what it owned goes neutral.
        town.Owner = null;
        state.RemoveHero(state.GetHero(A));
        session.Flow.Evaluate();

        Assert.Null(dwelling.Owner);
        Assert.Empty(state.Caravans);
    }
}
