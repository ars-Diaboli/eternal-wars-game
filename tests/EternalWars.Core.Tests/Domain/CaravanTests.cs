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

/// <summary>Map dwellings and caravans (ADR-054, the reach and queue of ADR-073 / ADR-075).</summary>
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
        var (session, town, dwelling) = Setup(width: 60, dwellingX: 40);
        var state = session.State;
        var footman = state.Creatures.Get(Footman);
        var waiting = dwelling.Available!.Value;
        var route = CaravanRules.FindRoute(state, Blue, dwelling.VisitPosition, town.VisitPosition)!;
        var events = new List<IEvent>();
        session.Events.Subscribe<CaravanDispatched>(events.Add);
        session.Events.Subscribe<CaravanArrived>(events.Add);

        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 5)).ThrowIfRejected();

        Assert.Equal(waiting - 5, dwelling.Available);
        Assert.Equal(100_000 - (5 * footman.Cost.Gold), state.GetPlayer(Blue).Resources.Gold);
        var caravan = Assert.Single(state.Caravans);
        Assert.Equal(route.Days, caravan.DaysLeft);
        Assert.True(route.Days >= 2);
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
    public void ADwellingBeyondTheReach_IsNotListed_AndCannotSupplyTheTown()
    {
        var (session, town, dwelling) = Setup(width: 110, dwellingX: 100); // about 98 steps: 9800 points
        var gold = session.State.GetPlayer(Blue).Resources.Gold;

        var result = session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1));

        Assert.Contains("beyond the reach", result.Error);
        Assert.Equal(gold, session.State.GetPlayer(Blue).Resources.Gold);
        Assert.Empty(CaravanRules.Dwellings(session.State, town));
    }

    [Fact]
    public void TheReachAndTheSpeed_IgnoreTerrain_SoEvenSwampAtTheEdgeOfTheReachTakesThreeDays()
    {
        // Swamp everywhere: 70 steps are 7000 + points of plain movement whatever the ground, three days at 2500 a day.
        var (session, town, dwelling) = Setup(width: 80, dwellingX: 72, special: (x, y) => x == 3 && y == 7 ? 'A' : x == 79 && y == 11 ? 'B' : 's');
        var state = session.State;
        var distance = CaravanRules.Distance(state, null, dwelling.VisitPosition, town.VisitPosition);
        var route = CaravanRules.FindRoute(state, Blue, dwelling.VisitPosition, town.VisitPosition)!;

        Assert.InRange(distance!.Value, 6900, CaravanRules.MaxReach);
        Assert.Equal(3, route.Days);
        Assert.Equal([dwelling], CaravanRules.Dwellings(state, town));

        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 2)).ThrowIfRejected();
        Assert.Equal(route.Days, Assert.Single(state.Caravans).DaysLeft);
    }

    [Fact]
    public void TheRawReach_IgnoresHostiles_ButAnOrderNeedsAnOpenRoute()
    {
        // A rock wall at x = 12 with a gap at y = 5, and a second dwelling behind a wall without a gap.
        char? Walls(int x, int y) => (x == 12 && y != 5) || (x == 30 && y < 11) || (x > 30 && y == 10) ? '#' : null;
        var (session, town, dwelling) = Setup(special: Walls);
        var state = session.State;
        var closedIn = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 34, 3);
        closedIn.Creature = Footman;
        closedIn.Owner = Blue;
        state.GetHero(B).Position = new MapPosition(12, 5); // an enemy in the gap

        Assert.Equal([dwelling], CaravanRules.Dwellings(state, town)); // the hero does not matter for the list, the rock does
        Assert.Contains("No open route", CaravanRules.CheckOrder(state, town, dwelling, 1));
        Assert.Contains("beyond the reach", CaravanRules.CheckOrder(state, town, closedIn, 1));
    }

    [Fact]
    public void TheTownsQueue_ListsCaravansOnTheirWay_ThenThoseWaitingForRoom()
    {
        var (session, town, near) = Setup(dwellingX: 10);
        var state = session.State;
        var far = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 30, 3);
        far.Creature = BuiltInCreatures.Wolf;
        far.Owner = Blue;
        far.Available = 10;
        session.Execute(new OrderCaravanCommand(Blue, town.Id, far.Id, 2)).ThrowIfRejected();
        session.Execute(new OrderCaravanCommand(Blue, town.Id, near.Id, 2)).ThrowIfRejected();
        var (first, second) = (state.Caravans[0], state.Caravans[1]);
        Assert.True(first.DaysLeft > second.DaysLeft);

        Assert.Equal([second, first], CaravanRules.Queue(state, town)); // fewest days left first

        var other = state.Creatures.All.Where(c => c.Id != Footman && c.Id != BuiltInCreatures.Wolf).Take(Army.SlotCount).ToList();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            town.Garrison.Set(slot, new CreatureStack(other[slot].Id, 1));
        }

        for (var day = 0; day < second.DaysLeft; day++)
        {
            NextDay(session);
        }

        var queue = CaravanRules.Queue(state, town);
        Assert.Equal(2, queue.Count);
        Assert.Equal(first.Id, queue[0].Id); // still on its way
        Assert.Equal((second.Id, 0), (queue[1].Id, queue[1].DaysLeft)); // waiting at the gates
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

        for (var day = 0; day < 8; day++)
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
        var (session, town, dwelling) = Setup(width: 60, dwellingX: 40);
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
        Assert.True(MapDocument.CurrentFormatVersion >= 12);

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
    public void MapDwellings_SellBaseCreaturesOnly()
    {
        var state = Create(Grass(20));
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 6, 5);
        dwelling.Creature = Footman;
        var crusader = state.Creatures.Get(Footman).UpgradesTo!;
        var editor = new MapEditorSession(state);

        Assert.Equal(EditResult.UpgradedCreature, editor.SetDwelling(dwelling.Id, crusader, null, null));
        Assert.Equal(Footman, dwelling.Creature);

        // A map made before the rule: the validator rejects the upgrade, and loading turns it into its line's base creature.
        dwelling.Creature = crusader;
        Assert.Contains(MapValidator.Validate(state), i => i.Severity == IssueSeverity.Error && i.Message.Contains("base creatures only", StringComparison.Ordinal));
        var loaded = MapConverter.ToGameState(MapConverter.ToDocument(state));
        Assert.Equal(Footman, Assert.Single(loaded.ObjectsOf<CreatureDwelling>()).Creature);
        Assert.Equal((Footman, Footman), (state.Creatures.BaseOf(crusader), state.Creatures.BaseOf(Footman)));
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

    [Fact]
    public void ACaravan_Covers2500PointsADay_SoTheWholeReachTakesAtMostThreeDays()
    {
        Assert.Equal(new CaravanRoute(0, 1), CaravanRules.FindRoute(Create(Grass(5)), Blue, new(1, 1), new(1, 1)));
        var (session, town, dwelling) = Setup(width: 60, dwellingX: 22); // 2500 points or less: one day
        Assert.Equal(1, CaravanRules.RouteBetween(session.State, dwelling, town, out _)!.Days);
        Assert.Equal(3, CaravanRules.MaxReach / CaravanRules.DailyMovement);
    }

    [Fact]
    public void ATown_TakesSixCaravansAtOnce_EightWithAGovernor()
    {
        var (session, town, dwelling) = Setup();
        var state = session.State;
        dwelling.Available = 50;
        for (var i = 0; i < CaravanRules.MaxCaravans; i++)
        {
            session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).ThrowIfRejected();
        }

        Assert.Contains("already has 6 caravans", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).Error);
        Assert.Equal(6, state.Caravans.Count);

        state.GetHero(A).Skills.Set(BuiltInSkills.Stewardship, SkillLevel.Basic);
        town.Governor = A;
        Assert.Equal(CaravanRules.GovernedMaxCaravans, CaravanRules.Limit(state, town));
        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).ThrowIfRejected();
        session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).ThrowIfRejected();
        Assert.Contains("already has 8 caravans", session.Execute(new OrderCaravanCommand(Blue, town.Id, dwelling.Id, 1)).Error);
    }

    [Fact]
    public void TroopsSentBetweenOwnTowns_CountAtTheDestination()
    {
        var state = Create(Grass(40));
        var first = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var second = AddTown(state, TownIds.Haven, 12, 0, Blue);
        first.SetLevel(BuiltInBuildings.Caravan, 1);
        second.SetLevel(BuiltInBuildings.Caravan, 1);
        first.Garrison.Set(0, new CreatureStack(Footman, 20));
        second.Garrison.Set(0, new CreatureStack(Footman, 20));
        var session = Start(state);
        for (var i = 0; i < CaravanRules.MaxCaravans; i++)
        {
            session.Execute(new SendTroopsCommand(Blue, first.Id, 0, second.Id, 1)).ThrowIfRejected();
        }

        Assert.Contains("already has 6 caravans", session.Execute(new SendTroopsCommand(Blue, first.Id, 0, second.Id, 1)).Error);
        Assert.Empty(CaravanRules.Queue(state, first));
        session.Execute(new SendTroopsCommand(Blue, second.Id, 0, first.Id, 1)).ThrowIfRejected(); // the other way is free
    }

    [Fact]
    public void AResourceCaravan_FetchesWhatWaitsAtAnOwnProvider_For100Gold_AndCountsTowardsTheLimit()
    {
        var (session, town, _) = Setup(width: 60);
        var state = session.State;
        var wheel = Add<ResourceProvider>(state, BuiltInMapObjects.WaterWheel, 40, 3);
        Assert.Empty(CaravanRules.Providers(state, town));
        wheel.Owner = Blue;
        wheel.Stored = new ResourceSet(Gold: 2500);
        var gold = state.GetPlayer(Blue).Resources.Gold;
        var events = new List<IEvent>();
        session.Events.Subscribe<CaravanDispatched>(events.Add);
        session.Events.Subscribe<CaravanArrived>(events.Add);

        Assert.Equal([wheel], CaravanRules.Providers(state, town));
        session.Execute(new FetchResourcesCommand(Blue, town.Id, wheel.Id)).ThrowIfRejected();

        Assert.True(wheel.Stored.IsEmpty);
        Assert.Equal(gold - 100, state.GetPlayer(Blue).Resources.Gold);
        var caravan = Assert.Single(CaravanRules.Queue(state, town));
        Assert.Equal((new ResourceSet(Gold: 2500), 2), (caravan.Cargo, caravan.DaysLeft));
        Assert.Contains("Nothing waits", session.Execute(new FetchResourcesCommand(Blue, town.Id, wheel.Id)).Error);

        NextDay(session);
        NextDay(session);

        Assert.Empty(state.Caravans);
        Assert.Equal(gold - 100 + 2500 + (2 * TownRules.DailyIncome(state, town).Gold), state.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(new CaravanArrived(caravan.Id, town.Id, "", 0, new ResourceSet(Gold: 2500)), events[1]);
    }

    [Fact]
    public void AResourceCaravan_NeedsAnOwnProvider_TheMoney_AndAnOpenRoute_AndReturnsItsCargoWhenBlocked()
    {
        char? Wall(int x, int y) => x == 12 && y != 5 ? '#' : null;
        var (session, town, _) = Setup(special: Wall);
        var state = session.State;
        var mill = Add<ResourceProvider>(state, BuiltInMapObjects.Windmill, 20, 6);
        mill.Stored = new ResourceSet(Ore: 5);
        Assert.Contains("not yours", session.Execute(new FetchResourcesCommand(Blue, town.Id, mill.Id)).Error);

        mill.Owner = Blue;
        var resources = new ResourceSet(Gold: 50);
        state.GetPlayer(Blue).Resources = resources;
        Assert.Contains("costs 100 gold", session.Execute(new FetchResourcesCommand(Blue, town.Id, mill.Id)).Error);

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 1000);
        state.GetHero(B).Position = new MapPosition(12, 5);
        Assert.Contains("No open route", session.Execute(new FetchResourcesCommand(Blue, town.Id, mill.Id)).Error);

        state.GetHero(B).Position = new MapPosition(39, 11);
        session.Execute(new FetchResourcesCommand(Blue, town.Id, mill.Id)).ThrowIfRejected();
        state.GetHero(B).Position = new MapPosition(12, 5);
        NextDay(session);

        Assert.Empty(state.Caravans);
        Assert.Equal(new ResourceSet(Ore: 5), mill.Stored);
        Assert.Equal(1000 + TownRules.DailyIncome(state, town).Gold, state.GetPlayer(Blue).Resources.Gold); // the 100 gold refunded
    }

    [Fact]
    public void AGameWithAResourceCaravan_ReplaysExactly()
    {
        var state = Create(Grass(40));
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Caravan, 1);
        Add<ResourceProvider>(state, BuiltInMapObjects.WaterWheel, 20, 3).Owner = Blue;
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 3)));
        session.Start();
        var wheel = session.State.ObjectsOf<ResourceProvider>().Single();
        session.Execute(new FetchResourcesCommand(Blue, town.Id, wheel.Id)).ThrowIfRejected();

        var json = Core.Data.Records.GameRecordSerializer.Serialize(Core.Data.Records.GameReplay.Record(map, session));
        var replayed = Core.Data.Records.GameReplay.Replay(Core.Data.Records.GameRecordSerializer.Deserialize(json));

        Assert.Contains("fetchResources", json, StringComparison.Ordinal);
        Assert.Contains("cargo", GameStateDigest.Describe(session.State), StringComparison.Ordinal);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }
}
