using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Cursors;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The side a melee blow comes from and the battlefield's cursors (ADR-204).</summary>
public class BattleCursorTests
{
    private static (TestBattle Battle, BattleUnit Unit, BattleUnit Target) Duel(CreatureDefinition? attacker = null)
    {
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, attacker ?? TestBattle.Fixed("runner", 1, speed: 20), 1, 0, 10);
        var target = battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 8, 10); // cells 8..9 × 10..11, centre (9, 11)
        battle.StartWith(unit);
        return (battle, unit, target);
    }

    [Theory]
    [InlineData(8.1f, 10.1f, 6, 8, CompassSide.NorthWest)]
    [InlineData(9.0f, 10.05f, 8, 8, CompassSide.North)]
    [InlineData(9.9f, 10.1f, 10, 8, CompassSide.NorthEast)]
    [InlineData(9.9f, 11.0f, 10, 10, CompassSide.East)]
    [InlineData(9.9f, 11.9f, 10, 12, CompassSide.SouthEast)]
    [InlineData(9.0f, 11.95f, 8, 12, CompassSide.South)]
    [InlineData(8.1f, 11.9f, 6, 12, CompassSide.SouthWest)]
    [InlineData(8.1f, 11.0f, 6, 10, CompassSide.West)]
    public void ThePointersSector_PicksOneOfEightSides(float x, float y, int standX, int standY, CompassSide side)
    {
        var (battle, _, target) = Duel();
        var cell = new CellPosition((int)x, (int)y);

        var action = BattleTargeting.ActionAt(battle.State, cell, x, y);

        Assert.Equal(new AttackAction(target.Id, new CellPosition(standX, standY)), action);
        Assert.Equal(side, BattleTargeting.SideOf(battle.State, action));
        Assert.Equal(BattleCursors.Melee(side), BattleCursors.For(battle.State, acting: true, armedSpell: null, action));
    }

    [Fact]
    public void AnUnreachableSide_GivesWayToTheNearestReachableOne()
    {
        var (battle, _, target) = Duel();
        battle.AddObstacle(ObstacleClass.Small, 10, 10); // fills the east side
        var action = BattleTargeting.ActionAt(battle.State, new CellPosition(9, 11), 9.9f, 11f);

        var side = BattleTargeting.SideOf(battle.State, action);

        var attack = Assert.IsType<AttackAction>(action);
        Assert.Equal(target.Id, attack.Target);
        Assert.Contains(side, new CompassSide?[] { CompassSide.NorthEast, CompassSide.SouthEast });
    }

    [Fact]
    public void Moves_ShowHowTheStackMoves()
    {
        var walker = Duel();
        var flyer = Duel(TestBattle.Fixed("flyer", 1, speed: 20, capabilities: new CreatureCapability(CapabilityKind.Flying)));
        var teleporter = Duel(TestBattle.Fixed("blinker", 1, speed: 20, capabilities: new CreatureCapability(CapabilityKind.Teleporting)));
        var free = new CellPosition(4, 4);

        GameCursor CursorAt(TestBattle battle) => BattleCursors.For(battle.State, true, null, BattleTargeting.ActionAt(battle.State, free));

        Assert.Equal(GameCursor.Walk, CursorAt(walker.Battle));
        Assert.Equal(GameCursor.Fly, CursorAt(flyer.Battle));
        Assert.Equal(GameCursor.Teleport, CursorAt(teleporter.Battle));
    }

    [Fact]
    public void Shots_SpellsAndNothingToDo_HaveTheirCursors()
    {
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 5), 1, 0, 0);
        var near = battle.Add(Defender, TestBattle.Fixed("near", 1), 1, 6, 0);
        battle.StartWith(archer);

        Assert.Equal(GameCursor.Shoot, BattleCursors.For(battle.State, true, null, new ShootAction(near.Id)));
        Assert.Equal(GameCursor.Spell, BattleCursors.For(battle.State, true, false, new ShootAction(near.Id)));
        Assert.Equal(GameCursor.SpellArea, BattleCursors.For(battle.State, true, true, null));
        Assert.Equal(GameCursor.Blocked, BattleCursors.For(battle.State, true, null, null));
        Assert.Equal(GameCursor.Busy, BattleCursors.For(battle.State, false, null, new ShootAction(near.Id)));
    }

    [Fact]
    public void AShotThatDealsHalfOrLess_IsWeak()
    {
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 5), 1, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("far", 1), 1, 28, 22);
        battle.AddObstacle(ObstacleClass.Big, 14, 11);
        battle.StartWith(archer);

        Assert.True(BattleGrid.CanShoot(battle.State, archer, target, out var penalty));
        Assert.True(penalty >= BattleCursors.WeakShotPenaltyEighths, $"penalty {penalty}/8");
        Assert.Equal(GameCursor.ShootWeak, BattleCursors.For(battle.State, true, null, new ShootAction(target.Id)));
    }

    [Theory]
    [InlineData(1, 0, CompassSide.East)]
    [InlineData(1, 1, CompassSide.SouthEast)]
    [InlineData(0, -1, CompassSide.North)]
    [InlineData(-1, -1, CompassSide.NorthWest)]
    [InlineData(-3, 1, CompassSide.West)]
    public void Directions_SnapToTheNearestSide(float dx, float dy, CompassSide side) => Assert.Equal(side, CompassSides.Nearest(dx, dy));
}
