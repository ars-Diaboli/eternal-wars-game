using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

public class BattleGridTests
{
    [Fact]
    public void CellRect_Adjacency_IncludesCornersButNotOverlapOrGaps()
    {
        var unit = new CellRect(new CellPosition(4, 4), 2);

        Assert.True(unit.IsAdjacentTo(new CellRect(new CellPosition(6, 4), 2))); // edge
        Assert.True(unit.IsAdjacentTo(new CellRect(new CellPosition(6, 6), 2))); // corner
        Assert.False(unit.IsAdjacentTo(new CellRect(new CellPosition(7, 4), 2))); // one cell gap
        Assert.False(unit.IsAdjacentTo(new CellRect(new CellPosition(5, 5), 2))); // overlap
        Assert.Equal(1, unit.GapTo(new CellRect(new CellPosition(7, 4), 2)));
    }

    [Fact]
    public void Reachable_RespectsSpeedAndFootprintClearance()
    {
        var battle = TestBattle.Create();
        var footman = battle.Add(Attacker, BuiltInCreatures.Footman, 5, 0, 0); // 2×2, speed 10
        battle.AddObstacle(ObstacleClass.Big, 4, 0); // cells 4..6 × 0..2

        var reachable = BattleGrid.Reachable(battle.State, footman);

        Assert.Equal(0, reachable[footman.Position]);
        Assert.Equal(10, reachable.Values.Max());
        Assert.False(reachable.ContainsKey(new CellPosition(3, 0))); // footprint 3..4 would overlap the obstacle
        Assert.False(reachable.ContainsKey(new CellPosition(11, 0))); // 11 steps away
        Assert.DoesNotContain(reachable.Keys, p => p.X < 0 || p.Y < 0 || p.X + 2 > 30 || p.Y + 2 > 24);
    }

    [Fact]
    public void Reachable_DiagonalStep_CannotSqueezeBetweenTwoBlockers()
    {
        // A 2×2 unit at (2,2). The diagonal step to (3,3) is free itself but needs placement (3,2) or (2,3).
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("slow", 1, speed: 1), 1, 2, 2);
        battle.AddObstacle(ObstacleClass.Wall, 4, 2); // one cell: blocks placement (3,2) only
        battle.AddObstacle(ObstacleClass.Wall, 2, 4); // one cell: blocks placement (2,3) only

        var reachable = BattleGrid.Reachable(battle.State, unit);

        Assert.False(reachable.ContainsKey(new CellPosition(3, 3)));
        Assert.True(reachable.ContainsKey(new CellPosition(1, 1)));

        battle.State.MutableObstacles.RemoveAt(1);
        Assert.True(BattleGrid.Reachable(battle.State, unit).ContainsKey(new CellPosition(3, 3)));
    }

    [Fact]
    public void Reachable_Flyer_LandsBehindAWall()
    {
        var battle = TestBattle.Create();
        var griffin = battle.Add(Attacker, BuiltInCreatures.Griffin, 1, 0, 10); // 3×3, flying, speed 18
        for (var y = 0; y < 24; y += 3)
        {
            battle.AddObstacle(ObstacleClass.Big, 5, y);
        }

        var reachable = BattleGrid.Reachable(battle.State, griffin);

        Assert.True(reachable.ContainsKey(new CellPosition(10, 10)));
        Assert.False(reachable.ContainsKey(new CellPosition(5, 10)));
    }

    [Fact]
    public void LineOfFire_ThroughExactCorner_IncludesBothSideCells()
    {
        // Centres (1,1) and (5,5): the diagonal passes through grid corners (2,2), (3,3), (4,4).
        var cells = BattleGrid.LineOfFireCells(new CellRect(new CellPosition(0, 0), 2), new CellRect(new CellPosition(4, 4), 2)).ToHashSet();

        Assert.Contains(new CellPosition(2, 2), cells);
        Assert.Contains(new CellPosition(1, 2), cells); // touched at the corner (2,2)
        Assert.Contains(new CellPosition(2, 1), cells);
        Assert.DoesNotContain(new CellPosition(4, 1), cells);
    }

    [Fact]
    public void Cover_AddsEachBlockerOnce_AndAboveSevenEighthsForbidsTheShot()
    {
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, BuiltInCreatures.Archer, 10, 0, 10);
        var target = battle.Add(Defender, BuiltInCreatures.Footman, 10, 26, 10);
        battle.Add(Attacker, BuiltInCreatures.RedDragon, 1, 4, 9); // 4×4 friend in the way: 2/8
        battle.AddObstacle(ObstacleClass.Big, 10, 10); // 2/8
        battle.AddObstacle(ObstacleClass.Big, 15, 10); // 2/8

        Assert.Equal(6, BattleGrid.CoverEighths(battle.State, archer, target));
        Assert.True(BattleGrid.CanShoot(battle.State, archer, target, out _));

        battle.AddObstacle(ObstacleClass.Small, 20, 10); // 7/8: still allowed
        Assert.True(BattleGrid.CanShoot(battle.State, archer, target, out var cover));
        Assert.Equal(7, cover);

        battle.Add(Defender, BuiltInCreatures.Pikeman, 1, 23, 10); // 8/8: no shot
        Assert.False(BattleGrid.CanShoot(battle.State, archer, target, out cover));
        Assert.Equal(8, cover);
    }

    [Fact]
    public void CanShoot_NotWhileAnEnemyIsAdjacent()
    {
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, BuiltInCreatures.Archer, 10, 0, 0);
        var target = battle.Add(Defender, BuiltInCreatures.Footman, 10, 20, 0);
        battle.Add(Defender, BuiltInCreatures.Pikeman, 1, 2, 2);

        Assert.False(BattleGrid.CanShoot(battle.State, archer, target, out _));
    }
}
