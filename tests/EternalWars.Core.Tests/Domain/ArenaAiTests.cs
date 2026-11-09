using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The adventure AI on an arena map's preparation day (ADR-160): everything for the one battle at the start of day 2.</summary>
public class ArenaAiTests
{
    /// <summary>
    /// Blue: a Haven town at the top left (entrance 3, 6), A below it, C to the east, a gold mine (entrance 10, 5). Red's B behind a rock wall.
    /// Blue starts with the Beginner resources.
    /// </summary>
    private static (GameSession Session, Town Town, Mine Mine) Setup(bool arena)
    {
        var state = Create(
            new GameOptions(Complexity: Complexity.Beginner),
            "..............#.",
            "............C.#.",
            "..............#.",
            "..............#.",
            "..............#.",
            "..............#.",
            "..............#.",
            "...A..........#.",
            "..............#.",
            "..............#B");
        if (arena)
        {
            state.SetRules(ScenarioRules.Default with { Arena = true });
            Add<CombatPlace>(state, BuiltInMapObjects.CombatPlace, 12, 8);
        }

        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 9, 4);
        return (Start(state), town, mine);
    }

    private static long Power(GameState state, EternalWars.Core.Domain.Armies.Army army) =>
        army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).Value);

    [Fact]
    public void OnThePreparationDay_ATownBuildsSeveralBuildings_AndSpendsItsGoldOnTheArmy()
    {
        var (session, _, _) = Setup(arena: true);
        var state = session.State;
        var built = 0;
        session.Events.Subscribe<TownBuilt>(_ => built++);
        session.Events.Subscribe<DwellingBuilt>(_ => built++);
        var before = state.HeroesOf(Blue).Sum(h => Power(state, h.Army));
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.True(built >= 2, $"built {built}");
        Assert.True(state.GetPlayer(Blue).Resources.Gold < 2000, $"{state.GetPlayer(Blue).Resources.Gold} gold left");
        Assert.True(state.HeroesOf(Blue).Sum(h => Power(state, h.Army)) > 2 * before);
        Assert.Equal(0, ai.Refused);
        Assert.Equal(Red, state.CurrentPlayer.Id);
    }

    [Fact]
    public void OnThePreparationDay_MinesAreNotWorthAVisit_ThoughEveryOtherDayTheyAre()
    {
        var (arena, _, unvisited) = Setup(arena: true);
        Assert.True(new AdventureAi(arena, controlHumanSlots: true).PlayTurn());
        Assert.Null(unvisited.Owner);

        // On an ordinary map the same mine is taken within the first days.
        var (ordinary, _, mine) = Setup(arena: false);
        new AdventureAi(ordinary, controlHumanSlots: true).PlayTurns(limit: 6);
        Assert.Equal(Blue, mine.Owner);
    }

    [Fact]
    public void AtTheEndOfThePreparationDay_EveryArmyIsInOneHero()
    {
        var (session, town, _) = Setup(arena: true);
        var state = session.State;
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        var champion = ArenaRules.Champion(state, state.GetPlayer(Blue).Team)!;
        Assert.True(town.Garrison.IsEmpty);
        Assert.All(state.HeroesOf(Blue).Where(h => h != champion), h => Assert.True(h.Army.Stacks.Sum(s => s.Stack.Count) <= 1));
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void OnDayTwo_TheComputerFightsTheArenaBattleToTheEnd()
    {
        var (session, _, _) = Setup(arena: true);
        var state = session.State;
        var ai = new AdventureAi(session, controlHumanSlots: true);
        var ended = new List<BattleEnded>();
        session.Events.Subscribe<BattleEnded>(ended.Add);

        ai.PlayTurns(limit: 3); // Blue prepares, Red ends its day, then day 2's battle, fought by the AI on both sides

        Assert.True(state.IsOver);
        Assert.Equal(EternalWars.Core.Domain.Battles.BattleEndReason.Defeated, Assert.Single(ended).Reason);
        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Equal(0, ai.Refused);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OnThePreparationDay_AWeakerEnemyHeroWithinReachIsLeftAlone(bool arena, bool attacked)
    {
        // Beating Red's only hero on day 1 would leave nobody for the battle: a loss for both.
        var state = Create("A...B");
        state.GetHero(A).Army.Set(0, new EternalWars.Core.Domain.Armies.CreatureStack(EternalWars.Core.Domain.Creatures.BuiltInCreatures.Champion, 20));
        if (arena)
        {
            state.SetRules(ScenarioRules.Default with { Arena = true });
        }

        var session = Start(state);
        Assert.True(new AdventureAi(session, controlHumanSlots: true).PlayTurn());

        Assert.Equal(attacked, state.Heroes.All(h => h.Id != B));
    }
}
