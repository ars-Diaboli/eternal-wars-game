using EternalWars.Core.Domain.Battles;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

public class BattleTargetingTests
{
    [Fact]
    public void FreeCell_MovesToTheCoveringPlacementNearestThePointer()
    {
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("walker", 1), 1, 0, 0); // 2×2, speed 10
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 26, 20);
        battle.StartWith(unit);

        // The cell's centre is equally close to all four placements covering it: the fewest steps wins.
        Assert.Equal(new MoveAction(new CellPosition(4, 4)), BattleTargeting.ActionAt(battle.State, new CellPosition(5, 5)));

        // Pointing at the cell's lower-right corner picks the placement extending that way.
        Assert.Equal(new MoveAction(new CellPosition(5, 5)), BattleTargeting.ActionAt(battle.State, new CellPosition(5, 5), 5.9f, 5.9f));
    }

    [Fact]
    public void OwnUnits_Obstacles_AndCellsOutOfReach_HaveNoAction()
    {
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("walker", 1), 1, 0, 0);
        battle.Add(Attacker, TestBattle.Fixed("friend", 1), 1, 0, 4);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 26, 20);
        battle.AddObstacle(ObstacleClass.Big, 4, 0);
        battle.StartWith(unit);

        Assert.Null(BattleTargeting.ActionAt(battle.State, new CellPosition(1, 1))); // itself
        Assert.Null(BattleTargeting.ActionAt(battle.State, new CellPosition(0, 5))); // a friend
        Assert.Null(BattleTargeting.ActionAt(battle.State, new CellPosition(5, 1))); // inside the obstacle
        Assert.Null(BattleTargeting.ActionAt(battle.State, new CellPosition(20, 10))); // too far
        Assert.Null(BattleTargeting.ActionAt(battle.State, new CellPosition(26, 20))); // an enemy out of reach
    }

    [Fact]
    public void Enemy_IsAttackedFromTheSideThePointerIsOn()
    {
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("runner", 1, speed: 20), 1, 0, 10);
        var target = battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 8, 10); // cells 8..9 × 10..11
        battle.StartWith(unit);

        Assert.Equal(new AttackAction(target.Id, new CellPosition(6, 10)), BattleTargeting.ActionAt(battle.State, new CellPosition(8, 10), 8.1f, 11f));
        Assert.Equal(new AttackAction(target.Id, new CellPosition(10, 10)), BattleTargeting.ActionAt(battle.State, new CellPosition(9, 10), 9.9f, 11f));
    }

    [Fact]
    public void Shooter_ShootsInsteadOfWalkingUp_UnlessEngaged()
    {
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 5), 1, 0, 10);
        var target = battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 8, 10);
        battle.StartWith(archer);

        Assert.Equal(new ShootAction(target.Id), BattleTargeting.ActionAt(battle.State, new CellPosition(8, 10)));

        archer.Position = new CellPosition(6, 10); // adjacent: no shot, so a melee blow where it stands
        Assert.Equal(new AttackAction(target.Id, new CellPosition(6, 10)), BattleTargeting.ActionAt(battle.State, new CellPosition(8, 10), 8.1f, 11f));
    }
}
