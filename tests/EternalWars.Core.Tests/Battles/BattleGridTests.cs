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
        var footman = battle.Add(Attacker, TestBattle.Fixed("walker", 1, speed: 10), 5, 0, 0); // 2×2
        battle.AddObstacle(ObstacleClass.Big, 4, 0); // cells 4..6 × 0..2

        var reachable = BattleGrid.Reachable(battle.State, footman);

        Assert.Equal(0, reachable[footman.Position]);
        Assert.True(reachable.Values.Max() <= 10 * BattleGrid.Unit + BattleGrid.ReachSlack);
        Assert.False(reachable.ContainsKey(new CellPosition(3, 0))); // footprint 3..4 would overlap the obstacle
        Assert.True(reachable.ContainsKey(new CellPosition(0, 10))); // 10 cells straight down
        Assert.False(reachable.ContainsKey(new CellPosition(0, 11))); // 11 cells
        Assert.False(reachable.ContainsKey(new CellPosition(8, 8))); // 11.3 cells diagonally
        Assert.DoesNotContain(reachable.Keys, p => p.X < 0 || p.Y < 0 || p.X + 2 > 30 || p.Y + 2 > 24);
    }

    [Fact]
    public void Reachable_DiagonalStep_CannotSqueezeBetweenTwoBlockers()
    {
        // A 2×2 unit at (2,2). The diagonal step to (3,3) is free itself but needs placement (3,2) or (2,3).
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("slow", 1, speed: 2), 1, 2, 2); // a diagonal step (1.41) needs Speed 2
        battle.AddObstacle(ObstacleClass.Wall, 4, 2); // one cell: blocks placement (3,2) only
        battle.AddObstacle(ObstacleClass.Wall, 2, 4); // one cell: blocks placement (2,3) only

        var reachable = BattleGrid.Reachable(battle.State, unit);

        Assert.False(reachable.ContainsKey(new CellPosition(3, 3)));
        Assert.True(reachable.ContainsKey(new CellPosition(1, 1)));

        battle.State.MutableObstacles.RemoveAt(1);
        Assert.True(BattleGrid.Reachable(battle.State, unit).ContainsKey(new CellPosition(3, 3)));
    }

    [Fact]
    public void Reachable_IsACircle_OfSpeedAndAQuarter_ForWalkersAndFlyers()
    {
        // CMB-9: on an open field a placement is reachable when its straight-line distance is at most Speed + 0.25.
        var battle = TestBattle.Create();
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 1, speed: 5), 1, 14, 11);
        var expected = new HashSet<CellPosition>();
        for (var dy = -6; dy <= 6; dy++)
        {
            for (var dx = -6; dx <= 6; dx++)
            {
                if ((dx * dx) + (dy * dy) <= 5.25 * 5.25)
                {
                    expected.Add(new CellPosition(14 + dx, 11 + dy));
                }
            }
        }

        var reachable = BattleGrid.Reachable(battle.State, walker);
        Assert.Equal(89, expected.Count); // 88 cells around the stack
        Assert.Equal(expected.OrderBy(p => (p.Y, p.X)), reachable.Keys.OrderBy(p => (p.Y, p.X)));
        Assert.Equal(BattleGrid.Length(5, 1), reachable[new CellPosition(19, 12)]);
        Assert.False(reachable.ContainsKey(new CellPosition(18, 15))); // (4, 4) is 5.66 away

        var flying = TestBattle.Create();
        var flyer = flying.Add(Attacker, TestBattle.Fixed("flyer", 1, speed: 5, capabilities: new CreatureCapability(CapabilityKind.Flying)), 1, 14, 11);
        Assert.Equal(expected.OrderBy(p => (p.Y, p.X)), BattleGrid.Reachable(flying.State, flyer).Keys.OrderBy(p => (p.Y, p.X)));
    }

    [Fact]
    public void Reachable_AroundAnObstacle_CountsTheWayAround()
    {
        var battle = TestBattle.Create();
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 1, speed: 9), 1, 2, 10); // 2×2
        battle.AddObstacle(ObstacleClass.Big, 5, 9); // cells 5..7 × 9..11, straight in the way

        var behind = new CellPosition(9, 10); // 7 cells in a straight line, which the block cuts
        var reachable = BattleGrid.Reachable(battle.State, walker);

        Assert.True(reachable.ContainsKey(behind));
        Assert.True(reachable[behind] > BattleGrid.Length(7, 0));
        Assert.True(reachable[behind] < BattleGrid.Length(7, 0) + (3 * BattleGrid.Unit)); // a short way round, not a staircase
    }

    [Fact]
    public void Lengths_AreThousandthsOfACell_RoundedDown()
    {
        Assert.Equal(5000, BattleGrid.Length(3, 4));
        Assert.Equal(1414, BattleGrid.Length(1, 1));
        Assert.Equal(1414, BattleGrid.Length(-1, 1));
        Assert.Equal(0, BattleGrid.Length(0, 0));
        Assert.Equal(BattleGrid.Length(8, 8), new CellRect(new CellPosition(0, 0), 2).DistanceTo(new CellRect(new CellPosition(10, 10), 2)));
    }

    [Fact]
    public void DistancePenalty_MeasuresTheGapAsAStraightLine()
    {
        // 8 cells across and 8 down between the footprints: 11.3 cells, so 1/8 (counted 8 before CMB-9: none).
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, BuiltInCreatures.Archer, 10, 0, 0);
        var target = battle.Add(Defender, BuiltInCreatures.Footman, 10, 10, 10);

        Assert.Equal(1, BattleGrid.RangedPenaltyEighths(battle.State, archer, target));
        Assert.Equal(0, BattleGrid.DistancePenaltyEighths(BattleGrid.Length(9, 4))); // 9.8 cells
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
    public void Reachable_Teleporter_MovesLikeAFlyer()
    {
        var battle = TestBattle.Create();
        var walker = TestBattle.Fixed("walker", 1, speed: 8);
        var teleporter = TestBattle.Fixed("teleporter", 1, speed: 8, capabilities: new CreatureCapability(CapabilityKind.Teleporting));
        var unit = battle.Add(Attacker, teleporter, 1, 0, 10);
        for (var y = 0; y < 24; y += 3)
        {
            battle.AddObstacle(ObstacleClass.Big, 3, y);
        }

        Assert.True(BattleGrid.Reachable(battle.State, unit).ContainsKey(new CellPosition(7, 10)));
        Assert.True(teleporter.MovesFreely);
        Assert.Equal(walker.Value * 6 / 5, teleporter.Value);
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
        // Marksmanship 2/8 cancels the distance penalty (24 cells: 2/8), so only the cover counts.
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { MarksmanshipEighths = 2 });
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
