using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

public class ArmyTests
{
    [Fact]
    public void Army_TryAdd_MergesSameCreatureOrUsesAFreeSlot()
    {
        var army = Army.Of(new CreatureStack(BuiltInCreatures.Imp, 5));

        Assert.True(army.TryAdd(new CreatureStack(BuiltInCreatures.Imp, 3)));
        Assert.True(army.TryAdd(new CreatureStack(BuiltInCreatures.Devil, 1)));

        Assert.Equal([new CreatureStack(BuiltInCreatures.Imp, 8), new CreatureStack(BuiltInCreatures.Devil, 1)], army.Stacks.Select(s => s.Stack));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CreatureStack(BuiltInCreatures.Imp, 0));
    }

    [Fact]
    public void NativeTerrain_WaivesThePenaltyOnlyForAnAllNativeArmy()
    {
        var state = Create("Al.", "..B");
        var hero = state.GetHero(A);
        hero.Army.Set(0, new CreatureStack(BuiltInCreatures.Imp, 5));
        hero.Position = new MapPosition(1, 0); // on lava
        var lava = new MapPosition(1, 0);
        var next = new MapPosition(2, 0);

        Assert.Equal(TownIds.Inferno, MovementRules.ArmyTown(state, hero));
        Assert.Equal(100, MovementRules.StepCost(state, hero, lava, next, Direction.East));

        hero.Army.Set(1, new CreatureStack(BuiltInCreatures.Wolf, 5)); // a neutral spoils it
        Assert.Null(MovementRules.ArmyTown(state, hero));
        Assert.Equal(150, MovementRules.StepCost(state, hero, lava, next, Direction.East));
    }

    [Fact]
    public void MoveStack_WithinOneHero_SplitsMergesAndSwaps()
    {
        var state = Create("A..", "..B");
        var army = state.GetHero(A).Army;
        army.Set(2, new CreatureStack(BuiltInCreatures.Archer, 6));
        var session = Start(state);

        Move(session, A, 0, A, 1, 4).ThrowIfRejected(); // split 4 footmen off
        Assert.Equal((6, 4), (army[0]!.Count, army[1]!.Count));

        Move(session, A, 1, A, 0, null).ThrowIfRejected(); // merge back
        Assert.Equal(10, army[0]!.Count);
        Assert.Null(army[1]);

        Move(session, A, 0, A, 2, null).ThrowIfRejected(); // swap with the archers
        Assert.Equal((BuiltInCreatures.Archer, BuiltInCreatures.Footman), (army[0]!.Creature, army[2]!.Creature));

        Assert.False(Move(session, A, 0, A, 2, 3).IsAccepted); // a split cannot land on another creature
    }

    [Fact]
    public void MoveStack_BetweenHeroes_NeedsTheirExchange_AndLeavesAtLeastOneStack()
    {
        var state = Create("AC.", "..B");
        var session = Start(state);

        Assert.False(Move(session, A, 0, C, 1, 5).IsAccepted); // no exchange open yet

        Assert.Equal(MoveResult.Met, session.Step(A, Direction.East));
        Move(session, A, 0, C, 1, 5).ThrowIfRejected();

        Assert.Equal((5, 10, 5), (state.GetHero(A).Army[0]!.Count, state.GetHero(C).Army[0]!.Count, state.GetHero(C).Army[1]!.Count));
        Assert.False(Move(session, A, 0, C, 2, null).IsAccepted); // would leave A without creatures

        session.CompleteExchange();
        Assert.False(Move(session, C, 1, A, 1, null).IsAccepted);
    }

    [Fact]
    public void MovementAllowance_UsesTheSlowestMapSpeed()
    {
        var hero = new Hero(new HeroId(1), "H", new PlayerId(0), default, Army.Of(
            new CreatureStack(BuiltInCreatures.Devil, 1), // map speed 11
            new CreatureStack(BuiltInCreatures.HellHound, 1))); // map speed 7

        Assert.Equal(7, MovementAllowance.SlowestMapSpeed(hero, CreatureCatalog.Default));
        Assert.Equal(1760, MovementAllowance.Daily(hero, CreatureCatalog.Default));
        Assert.Equal(1500, MovementAllowance.Daily(new Hero(new HeroId(2), "Alone", new PlayerId(0), default), CreatureCatalog.Default));
    }

    private static CommandResult Move(GameSession session, HeroId from, int fromSlot, HeroId to, int toSlot, int? count) =>
        session.Execute(new MoveStackCommand(session.State.GetHero(from).Owner, from, fromSlot, to, toSlot, count));
}
