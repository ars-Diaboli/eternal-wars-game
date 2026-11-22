using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The town screen's foundation in core (ADR-294): the garrison and visiting heroes swapped in one step, a stack spread evenly over the empty
/// slots, and a dwelling line's status, closed for good by the 7-of-9 rule.
/// </summary>
public class TownScreenFoundationTests
{
    /// <summary>A Haven town at (0, 0) has its entrance at (3, 6), where hero A stands; Red's hero B waits two tiles below.</summary>
    private static readonly string[] Rows =
    [
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "...A........",
        "............",
        "...B........",
        "............",
    ];

    private static readonly ResourceSet Rich = new(Gold: 100_000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);

    /// <summary>Hero A on the entrance with footmen and archers; a garrison hero "Inside" with three pikemen, which become the garrison.</summary>
    private static (GameSession Session, Town Town, Hero Visiting, Hero Inside) WithBothHeroes(Action<GameState>? before = null)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var visiting = state.GetHero(A);
        visiting.Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));
        visiting.Army.Set(1, new CreatureStack(BuiltInCreatures.Archer, 4));
        var inside = new Hero(new HeroId(9), "Inside", Blue, new MapPosition(6, 9), Army.Of(new CreatureStack(BuiltInCreatures.Pikeman, 3)));
        state.AddHero(inside);
        before?.Invoke(state);
        var session = Start(state);

        // "Inside" goes into the garrison by the rules while A stands aside, then A comes back to the entrance.
        state.RemoveHero(visiting);
        inside.Position = town.VisitPosition;
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        visiting.Position = town.VisitPosition;
        state.AddHero(visiting);
        return (session, town, visiting, inside);
    }

    [Fact]
    public void Swapping_PutsTheVisitorInTheGarrison_AndLeadsTheGarrisonHeroOut_EachWithItsOwnTroops()
    {
        var (session, town, visiting, inside) = WithBothHeroes();
        var state = session.State;
        var events = new List<(object, bool)>();
        // Each event is checked against the state when it is published: the change comes first (ADR-036).
        session.Events.Subscribe<HeroEnteredGarrison>(e => events.Add((e, town.GarrisonHero == visiting)));
        session.Events.Subscribe<HeroLeftGarrison>(e => events.Add((e, state.HeroAt(town.VisitPosition) == inside)));
        session.Events.Subscribe<GarrisonChanged>(e => events.Add((e, town.Garrison[1]?.Creature == BuiltInCreatures.Archer)));
        session.Events.Subscribe<ArmyChanged>(e => events.Add((e.Hero, true)));

        session.Execute(new SwapTownHeroesCommand(Blue, town.Id)).ThrowIfRejected();

        Assert.Same(visiting, town.GarrisonHero);
        Assert.Equal(new CreatureStack(BuiltInCreatures.Footman, 10), town.Garrison[0]);
        Assert.Equal(new CreatureStack(BuiltInCreatures.Archer, 4), town.Garrison[1]);
        Assert.True(visiting.Army.IsEmpty);
        Assert.DoesNotContain(visiting, state.Heroes);
        Assert.Same(inside, state.HeroAt(town.VisitPosition));
        Assert.Equal(new CreatureStack(BuiltInCreatures.Pikeman, 3), inside.Army[0]);
        Assert.Equal(1, inside.Army.StackCount);
        Assert.Equal(
            [(new HeroEnteredGarrison(town.Id, visiting.Id), true), (new HeroLeftGarrison(town.Id, inside.Id, town.VisitPosition), true)],
            events.Take(2));
        Assert.IsType<GarrisonChanged>(events[2].Item1);
        Assert.True(events[2].Item2);
        Assert.Equal([(visiting.Id, true), (inside.Id, true)], events.Skip(3));
        Assert.Equal(MovementAllowance.Daily(state, inside), inside.MaxMovementPoints);

        // And back again.
        session.Execute(new SwapTownHeroesCommand(Blue, town.Id)).ThrowIfRejected();
        Assert.Same(inside, town.GarrisonHero);
        Assert.Same(visiting, state.HeroAt(town.VisitPosition));
        Assert.Equal(new CreatureStack(BuiltInCreatures.Archer, 4), visiting.Army[1]);
    }

    [Fact]
    public void Swapping_IsNotStoppedByTheHeroLimit_TheHeroesOnTheMapStayAsMany()
    {
        // Two heroes on the map at most; with a third hero out there Blue is at the limit (the garrison hero does not count, TOWN-8).
        var (session, town, visiting, inside) = WithBothHeroes(state => state.SetHeroLimit(Blue, 2));
        var state = session.State;
        state.AddHero(new Hero(new HeroId(10), "Other", Blue, new MapPosition(10, 9), Army.Of(new CreatureStack(BuiltInCreatures.Footman, 1))));
        Assert.True(state.HeroLimitReached(Blue));

        session.Execute(new SwapTownHeroesCommand(Blue, town.Id)).ThrowIfRejected();

        Assert.Same(inside, state.HeroAt(town.VisitPosition));
        Assert.Equal(2, state.HeroesOf(Blue).Count());
        Assert.Same(visiting, town.GarrisonHero);
    }

    [Fact]
    public void UpgradeLine_NeedsTheTownsOwnLine_WithAnUpgrade_AndItsDwellingUpgraded()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var footman = state.Creatures.Get(BuiltInCreatures.Footman);

        Assert.Contains("cannot upgrade", TownRules.CheckUpgradeLine(state, town, "inferno.imp", out _)); // another town's creature
        Assert.Contains("cannot upgrade", TownRules.CheckUpgradeLine(state, town, footman.UpgradesTo!.Value, out _)); // upgraded already
        Assert.Contains("not upgraded", TownRules.CheckUpgradeLine(state, town, footman.Id, out _));

        town.SetDwelling(footman.Id, 2);
        Assert.Null(TownRules.CheckUpgradeLine(state, town, footman.Id, out var line));
        Assert.Same(footman, line);
    }

    [Fact]
    public void Swapping_NeedsBothHeroes_AndTroopsForTheOneComingOut()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));
        var session = Start(state);

        Assert.Contains("no garrison hero", session.Execute(new SwapTownHeroesCommand(Blue, town.Id)).Error);

        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        Assert.Contains("No own hero is visiting", session.Execute(new SwapTownHeroesCommand(Blue, town.Id)).Error);

        var (both, full, _, _) = WithBothHeroes();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            full.Garrison.Set(slot, null);
        }

        Assert.Contains("no troops to lead out", both.Execute(new SwapTownHeroesCommand(Blue, full.Id)).Error);
        Assert.False(both.Execute(new SwapTownHeroesCommand(Red, full.Id)).IsAccepted);
    }

    [Fact]
    public void Spreading_SharesTheStackWithEveryEmptySlot_TheStackKeepingTheRest()
    {
        var army = Army.Of(new CreatureStack(BuiltInCreatures.Footman, 23), new CreatureStack(BuiltInCreatures.Archer, 2));

        var moves = StackSpread.Plan(army, 0);

        // 23 over six parts (the stack and five empty slots): 3 each, the stack keeps 8.
        Assert.Equal([new SpreadMove(2, 3), new SpreadMove(3, 3), new SpreadMove(4, 3), new SpreadMove(5, 3), new SpreadMove(6, 3)], moves);
        Assert.Equal([new SpreadMove(2, 1)], StackSpread.Plan(army, 1)); // two creatures: one moves
        Assert.Empty(StackSpread.Plan(army, 4)); // an empty slot
        Assert.Empty(StackSpread.Plan(Army.Of(new CreatureStack(BuiltInCreatures.Footman, 1)), 0)); // one creature
        Assert.Empty(StackSpread.Plan(new Army(Enumerable.Repeat(new CreatureStack(BuiltInCreatures.Footman, 5), Army.SlotCount)), 0));
    }

    [Fact]
    public void Spreading_RunsAsStackCommands_ForAHeroAndForTheGarrison()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.Garrison.Set(3, new CreatureStack(BuiltInCreatures.Pikeman, 13));
        var hero = state.GetHero(A);
        hero.Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 7));
        for (var slot = 1; slot < 5; slot++)
        {
            hero.Army.Set(slot, new CreatureStack(BuiltInCreatures.Archer, 1));
        }

        var session = Start(state);

        foreach (var command in StackSpread.ForHero(Blue, hero.Id, hero.Army, 0))
        {
            session.Execute(command).ThrowIfRejected();
        }

        foreach (var command in StackSpread.ForTown(Blue, town.Id, TownArmy.Garrison, town.Garrison, 3))
        {
            session.Execute(command).ThrowIfRejected();
        }

        Assert.Equal([3, 2, 2], new[] { 0, 5, 6 }.Select(s => hero.Army[s]!.Count));
        Assert.Equal(13, town.Garrison.Stacks.Sum(s => s.Stack.Count));
        Assert.Equal(Army.SlotCount, town.Garrison.StackCount);
        Assert.Equal(7, town.Garrison[3]!.Count); // 13 over seven parts: one each, the stack keeps seven
    }

    [Fact]
    public void DwellingStatus_TellsAClosedLineFromOneThatWaits()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = Rich;
        var footman = state.Creatures.Get(BuiltInCreatures.Footman);
        var griffin = state.Creatures.Get(BuiltInCreatures.Griffin);

        Assert.Equal(BuildingStatus.Available, BuildingTree.DwellingStatus(state, town, footman));

        // Waiting for tomorrow, then for money (the shipped dwellings need no building, so "needs buildings" is a custom table's).
        town.LastBuilt = state.Date;
        Assert.Equal(BuildingStatus.BuiltToday, BuildingTree.DwellingStatus(state, town, footman));
        town.LastBuilt = null;
        state.GetPlayer(Blue).Resources = ResourceSet.Empty;
        Assert.Equal(BuildingStatus.CannotAfford, BuildingTree.DwellingStatus(state, town, footman));
        state.GetPlayer(Blue).Resources = Rich;

        foreach (var line in new[] { BuiltInCreatures.Footman, BuiltInCreatures.Pikeman, BuiltInCreatures.Zealot, BuiltInCreatures.Archer, BuiltInCreatures.Monk })
        {
            town.SetDwelling(line, 1);
        }

        // Six built with tiers 4 and 5 empty would leave one dwelling for two tiers: the second tier 3 line is closed for good.
        Assert.Equal(BuildingStatus.Closed, BuildingTree.DwellingStatus(state, town, griffin));
        Assert.NotEqual(BuildingStatus.Closed, BuildingTree.DwellingStatus(state, town, footman)); // its upgrade is open


        state.GetPlayer(Blue).Resources = Rich;
        town.SetDwelling(BuiltInCreatures.Footman, 2);
        Assert.Equal(BuildingStatus.Complete, BuildingTree.DwellingStatus(state, town, footman));
    }
}
