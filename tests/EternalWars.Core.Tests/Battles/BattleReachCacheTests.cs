using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>
/// A unit's reach is kept beside its battle and handed out again until something the search reads changes: whatever happens, it is what a fresh
/// search gives (a copy of the battle has nothing kept, so it searches afresh).
/// </summary>
public class BattleReachCacheTests
{
    /// <summary>What a fresh search on a copy of the battle gives for the unit: every placement and its length, in order.</summary>
    private static List<KeyValuePair<CellPosition, int>> Fresh(BattleState battle, BattleUnit unit)
    {
        var copy = battle.Copy();
        return [.. BattleGrid.Reachable(copy, copy.GetUnit(unit.Id))];
    }

    /// <summary>Every living unit's reach, paths and attack placements agree with a fresh search's, and asking again hands out the same reach.</summary>
    private static void CheckEveryUnit(BattleState battle)
    {
        var copy = battle.Copy();
        foreach (var unit in battle.Units.Where(u => u.IsAlive))
        {
            var reach = BattleGrid.Reachable(battle, unit);
            var again = copy.GetUnit(unit.Id);
            Assert.Same(reach, BattleGrid.Reachable(battle, unit));
            Assert.Equal([.. BattleGrid.Reachable(copy, again)], [.. reach]);
            foreach (var to in reach.Keys.Where((_, i) => i % 7 == 0))
            {
                Assert.Equal(BattleGrid.Path(copy, again, to), BattleGrid.Path(battle, unit, to));
            }

            foreach (var enemy in battle.Units.Where(u => u.IsAlive && u.Side != unit.Side))
            {
                Assert.Equal(BattleGrid.AttackPositions(copy, again, copy.GetUnit(enemy.Id)), BattleGrid.AttackPositions(reach, unit, enemy));
            }
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void TheKeptReach_IsAFreshSearchsAfterEveryAction_ThroughABattle(ulong seed, bool siege)
    {
        var battle = TestBattle.Create(seed: seed, siege: siege ? new SiegeSpec(2, 8) : null);
        var right = battle.State.Width - 4;
        var first = battle.Add(Attacker, BuiltInCreatures.Footman, 30, 1, 2);
        battle.Add(Attacker, BuiltInCreatures.Archer, 15, 1, 8);
        battle.Add(Attacker, "haven.griffin", 6, 1, 14);
        battle.Add(Attacker, "inferno.hell-hound", 8, 1, 19);
        battle.Add(Defender, "inferno.imp", 40, right, 3);
        battle.Add(Defender, "inferno.hell-hound", 8, right, 10);
        battle.Add(Defender, BuiltInCreatures.Archer, 15, right + 1, 18);
        if (!siege)
        {
            battle.AddObstacle(ObstacleClass.Wall, 12, 8);
            battle.AddObstacle(ObstacleClass.Wall, 15, 14);
        }

        battle.StartWith(first);

        var actions = 0;
        while (battle.State.Winner is null && battle.State.ActiveUnit is not null && actions++ < 400)
        {
            CheckEveryUnit(battle.State);
            BattleAi.Act(battle.State, battle.Engine, BattleAiLevel.Latest);
        }

        Assert.True(actions > 20, $"only {actions} actions");
    }

    [Fact]
    public void TheReach_IsSearchedAgain_WhenWhatItReadsChanges()
    {
        KnownSpell Known(string id) => new(SpellCatalog.Default.Get(id), 1, SpellCatalog.Default.Get(id).Mana);
        var battle = TestBattle.Create(
            siege: new SiegeSpec(1, 5),
            attackerCaster: new BattleCaster(5, 1, [Known("nature.thornwall"), Known("nature.entangle"), Known("arcana.haste")], null, 200));
        var state = battle.State;
        var walker = battle.Add(Attacker, BuiltInCreatures.Footman, 20, 8, 10);
        var friend = battle.Add(Attacker, BuiltInCreatures.Archer, 10, 2, 3);
        var foe = battle.Add(Defender, "inferno.imp", 30, 12, 9);
        battle.StartWith(walker);
        var kept = BattleGrid.Reachable(state, walker);

        void Changed(string what)
        {
            var reach = BattleGrid.Reachable(state, walker);
            Assert.True(!ReferenceEquals(kept, reach), $"{what}: the kept reach was handed out");
            Assert.Equal(Fresh(state, walker), [.. reach]);
            Assert.Same(reach, BattleGrid.Reachable(state, walker));
            kept = reach;
        }

        Assert.Same(kept, BattleGrid.Reachable(state, walker));

        friend.Position = new CellPosition(8, 13);
        Changed("another unit moved next to it");

        Assert.Null(battle.Engine.Cast(Attacker, "nature.thornwall", null, new CellPosition(10, 6)));
        Changed("a blocking zone");
        NextRound(battle);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.haste", walker.Id, null));
        Changed("Haste");
        NextRound(battle);

        walker.MutableSpells.Clear();
        Changed("Haste gone");

        var foeReach = BattleGrid.Reachable(state, foe);
        Assert.Null(battle.Engine.Cast(Attacker, "nature.entangle", foe.Id, null));
        var held = BattleGrid.Reachable(state, foe);
        Assert.NotSame(foeReach, held);
        Assert.Equal(Fresh(state, foe), [.. held]);
        Assert.Single(held); // held where it stands

        state.Siege!.Structures.First(s => s.Kind == SiegeStructureKind.Wall).Health = 0;
        Changed("a wall section fell");

        foe.Count = 0;
        Changed("a stack died");

        battle.AddObstacle(ObstacleClass.Wall, 7, 11);
        Changed("an obstacle");

        walker.Position = new CellPosition(5, 16);
        Changed("it moved");
    }

    [Fact]
    public void FreePlacements_AgreesWithIsFree_OnEveryPlacement()
    {
        var battle = TestBattle.Create(siege: new SiegeSpec(2, 8));
        var griffin = battle.Add(Attacker, "haven.griffin", 5, 4, 4);
        battle.Add(Attacker, BuiltInCreatures.Footman, 5, 6, 4);
        battle.Add(Defender, "inferno.imp", 5, 30, 10);
        battle.AddObstacle(ObstacleClass.Wall, 10, 10);

        var free = BattleGrid.FreePlacements(battle.State, griffin);

        for (var y = -1; y <= BattleState.Height; y++)
        {
            for (var x = -1; x <= battle.State.Width; x++)
            {
                var at = new CellPosition(x, y);
                Assert.Equal(BattleGrid.IsFree(battle.State, griffin, at), free(at));
            }
        }
    }

    /// <summary>Every stack still to act this round defends, so the next round starts (a side's hero casts once a round).</summary>
    private static void NextRound(TestBattle battle)
    {
        var round = battle.State.Round;
        while (battle.State.Round == round && battle.State.ActiveUnit is not null)
        {
            Assert.Null(battle.Engine.Defend());
        }
    }
}
