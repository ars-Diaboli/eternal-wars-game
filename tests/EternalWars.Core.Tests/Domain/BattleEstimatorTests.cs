using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The AI's battle estimates play a few fights on battles of their own, outside the game, and keep what they found: an estimate depends only on
/// the two armies, the sides' Attack and Defense and a siege's fortifications with the attacker's war machines.
/// </summary>
public class BattleEstimatorTests
{
    private static readonly GameState State = new(new AdventureMap(3, 1), [new Player(new PlayerId(0), "Red", isHuman: false)], []);

    private static Hero HeroWith(params CreatureStack[] army) => HeroWith(0, 0, army);

    private static Hero HeroWith(int attack, int defense, params CreatureStack[] army) =>
        new(new HeroId(7), "Leader", new PlayerId(0), new MapPosition(0, 0), new Army(army), attack, defense);

    private static Army ArmyOf(params CreatureStack[] stacks) => new(stacks);

    /// <summary>The estimate, and whether it was played (not kept).</summary>
    private static (BattleEstimate Estimate, bool Played) Ask(BattleEstimator estimator, Hero attacker, Army defender, int attack = 0, int defense = 0, SiegeSpec? siege = null)
    {
        var before = BattleEstimateCounters.Current;
        var estimate = estimator.Fight(attacker, defender, attack, defense, siege);
        var done = BattleEstimateCounters.Current - before;
        Assert.Equal(1, done.Asked);
        return (estimate, done.Played == 1);
    }

    [Fact]
    public void AStrongArmy_WinsEveryFight_AndAWeakOneLosesItsWholeArmy()
    {
        var estimator = new BattleEstimator(State);

        var strong = estimator.Fight(HeroWith(new CreatureStack(BuiltInCreatures.Footman, 60)), ArmyOf(new CreatureStack(BuiltInCreatures.Archer, 5)));
        var weak = estimator.Fight(HeroWith(new CreatureStack(BuiltInCreatures.Archer, 5)), ArmyOf(new CreatureStack(BuiltInCreatures.Footman, 60)));

        Assert.True(strong.IsSafe(0.2), strong.ToString());
        Assert.Equal(3, strong.Runs);
        Assert.Equal(new BattleEstimate(0, 3, 1), weak);
    }

    [Fact]
    public void WithoutAnArmy_TheAttackerLoses_AndAnEmptyDefenderIsOverrun()
    {
        var estimator = new BattleEstimator(State);

        Assert.Equal(new BattleEstimate(0, 3, 1), estimator.Fight(HeroWith(), ArmyOf(new CreatureStack(BuiltInCreatures.Footman, 5))));
        Assert.Equal(new BattleEstimate(3, 3, 0), estimator.Fight(HeroWith(new CreatureStack(BuiltInCreatures.Footman, 5)), ArmyOf()));
    }

    [Fact]
    public void AnEstimate_IsTheSame_ForAnotherEstimatorAndAnotherGame()
    {
        var attacker = HeroWith(2, 1, new CreatureStack(BuiltInCreatures.Footman, 20), new CreatureStack(BuiltInCreatures.Archer, 12));
        var defender = ArmyOf(new CreatureStack("inferno.imp", 30), new CreatureStack("inferno.hell-hound", 6));
        var other = new GameState(new AdventureMap(9, 9, fill: TerrainType.Snow), [new Player(new PlayerId(3), "Blue", isHuman: true)], [], options: new GameOptions(Seed: 99));

        var estimate = new BattleEstimator(State).Fight(attacker, defender, 1, 3);

        Assert.Equal(estimate, new BattleEstimator(other).Fight(attacker, defender, 1, 3));
        Assert.InRange(estimate.Wins, 1, 3); // a close fight, so the three seeds matter
    }

    [Fact]
    public void AnEstimate_IsKept_UntilWhatItDependsOnChanges()
    {
        var estimator = new BattleEstimator(State);
        var attacker = HeroWith(2, 1, new CreatureStack(BuiltInCreatures.Footman, 20));
        var defender = ArmyOf(new CreatureStack("inferno.imp", 30));
        var siege = new SiegeSpec(1, 5);

        var first = Ask(estimator, attacker, defender);
        Assert.True(first.Played);
        Assert.Equal((first.Estimate, false), Ask(estimator, HeroWith(2, 1, new CreatureStack(BuiltInCreatures.Footman, 20)), ArmyOf(new CreatureStack("inferno.imp", 30))));

        Assert.True(Ask(estimator, HeroWith(2, 1, new CreatureStack(BuiltInCreatures.Footman, 21)), defender).Played, "another count");
        Assert.True(Ask(estimator, HeroWith(2, 1, null!, new CreatureStack(BuiltInCreatures.Footman, 20)), defender).Played, "another slot");
        Assert.True(Ask(estimator, HeroWith(3, 1, new CreatureStack(BuiltInCreatures.Footman, 20)), defender).Played, "another Attack");
        Assert.True(Ask(estimator, attacker, ArmyOf(new CreatureStack("inferno.familiar", 30))).Played, "another creature");
        Assert.True(Ask(estimator, attacker, defender, defense: 2).Played, "the defender's Defense");
        Assert.True(Ask(estimator, attacker, defender, siege: siege).Played, "a siege");
        Assert.True(Ask(estimator, attacker, defender, siege: new SiegeSpec(2, 5)).Played, "a stronger fort");
        Assert.False(Ask(estimator, attacker, defender, siege: siege with { Town = "haven" }).Played, "the town only changes the look");

        // The war machines fight only in a siege.
        attacker.SetWarMachines([WarMachineSlot.Offensive, WarMachineSlot.Tent]);
        Assert.False(Ask(estimator, attacker, defender).Played);
        Assert.True(Ask(estimator, attacker, defender, siege: siege).Played);
    }

    [Fact]
    public void ASiegeAttacker_BreaksNoWalls_WithoutItsOffensiveMachine()
    {
        var estimator = new BattleEstimator(State);
        var defender = ArmyOf(new CreatureStack(BuiltInCreatures.Archer, 12));
        var siege = new SiegeSpec(1, 3);
        var withEngine = HeroWith(new CreatureStack(BuiltInCreatures.Footman, 60)); // a hero starts with its siege engine
        var without = HeroWith(new CreatureStack(BuiltInCreatures.Footman, 60));
        without.SetWarMachines([]);

        var breaking = estimator.Fight(withEngine, defender, siege: siege);
        var outside = estimator.Fight(without, defender, siege: siege);

        Assert.True(breaking.IsSafe(0.1), breaking.ToString());
        Assert.True(outside.LossFraction > 0.2, outside.ToString());
    }

    [Fact]
    public void TheKeptEstimates_AreBounded_TheOldestDropped()
    {
        var estimator = new BattleEstimator(State);
        var nobody = HeroWith(); // an attacker without an army loses at once, so the fights cost nothing
        Army Guard(int count) => ArmyOf(new CreatureStack(BuiltInCreatures.Footman, count));

        for (var count = 1; count <= BattleEstimator.Capacity; count++)
        {
            Assert.True(Ask(estimator, nobody, Guard(count)).Played);
        }

        Assert.False(Ask(estimator, nobody, Guard(1)).Played, "full, nothing is dropped yet");
        for (var count = BattleEstimator.Capacity + 1; count <= BattleEstimator.Capacity * 2; count++)
        {
            Assert.True(Ask(estimator, nobody, Guard(count)).Played);
        }

        // Asked again, the first one moved to the newer generation and stays; the second was dropped with the older one.
        Assert.False(Ask(estimator, nobody, Guard(1)).Played);
        Assert.True(Ask(estimator, nobody, Guard(2)).Played);
        Assert.False(Ask(estimator, nobody, Guard(BattleEstimator.Capacity * 2)).Played);
    }
}
