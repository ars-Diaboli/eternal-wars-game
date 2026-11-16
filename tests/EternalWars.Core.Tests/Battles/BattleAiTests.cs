using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The battle AI's steps (ADR-188), each against the level before it.</summary>
public class BattleAiTests
{
    [Theory]
    [InlineData(BattleAiLevel.Greedy, false)]
    [InlineData(BattleAiLevel.FocusFire, true)]
    public void FocusFire_ShootsTheBiggestThreat_WhenTheShotIsWorthTheSame(BattleAiLevel level, bool shootsTheBigStack)
    {
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 10), 1, 0, 10);
        var twin = TestBattle.Fixed("twin", 1);
        var small = battle.Add(Defender, twin, 5, 20, 6);
        var big = battle.Add(Defender, twin, 50, 20, 14);
        battle.StartWith(archer);

        BattleAi.Act(battle.State, battle.Engine, level);

        // Neither stack dies, and a hit point of either is worth the same: the old AI takes the lower id, the new one the bigger threat.
        Assert.Equal((shootsTheBigStack ? big : small).Id, ShotAt(battle, archer));
    }

    [Theory]
    [InlineData(BattleAiLevel.Greedy, false)]
    [InlineData(BattleAiLevel.FocusFire, true)]
    public void FocusFire_FinishesOffAStackItCanDestroy(BattleAiLevel level, bool finishesIt)
    {
        var expected = ExpectedBlow();
        var battle = TestBattle.Create();
        var hitter = battle.Add(Attacker, Hitter, 50, 10, 10);
        var lone = battle.Add(Defender, TestBattle.Fixed("lone", 1) with { FightValue = 100 }, 1, 12, 10);

        // Each blow at the tough stack takes off a little more Fight Value than destroying the lone creature (110 against 100).
        var tough = battle.Add(Defender, TestBattle.Fixed("tough", 1, health: 100) with { FightValue = 11_000 / expected }, 100, 8, 10);
        battle.Add(Defender, TestBattle.Fixed("far", 1) with { FightValue = 1000 }, 1000, 28, 0); // the biggest threat, out of reach
        battle.StartWith(hitter);
        Assert.True(BattleAi.Score(battle.State, hitter, tough, isRanged: false) > BattleAi.Score(battle.State, hitter, lone, isRanged: false));

        BattleAi.Act(battle.State, battle.Engine, level);

        Assert.Equal((finishesIt ? lone : tough).Id, HitBy(battle, hitter));
        Assert.Equal(finishesIt, !lone.IsAlive);
    }

    [Fact]
    public void Threat_IsFightValueTimesCount_RaisedForAShooterThatCanShoot()
    {
        var battle = TestBattle.Create();
        var footmen = battle.Add(Attacker, TestBattle.Fixed("footman", 1) with { FightValue = 30 }, 7, 0, 0);
        var archers = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 5) with { FightValue = 40 }, 10, 0, 10);
        var blocked = battle.Add(Attacker, TestBattle.Fixed("blocked", 1, shots: 5) with { FightValue = 40 }, 10, 0, 20);
        battle.Add(Defender, TestBattle.Fixed("guard", 1), 1, 2, 20);

        Assert.Equal(7 * 30, BattleAi.Threat(battle.State, footmen));
        Assert.Equal(10 * 40 * BattleAi.ActiveThreatPercent / 100, BattleAi.Threat(battle.State, archers));
        Assert.Equal(10 * 40, BattleAi.Threat(battle.State, blocked)); // an enemy next to it stops its shots
    }

    [Theory]
    [InlineData(BattleAiLevel.FocusFire, false)]
    [InlineData(BattleAiLevel.AvoidRetaliation, true)]
    public void AvoidRetaliation_HitsTheStackThatHasAlreadyHitBack(BattleAiLevel level, bool hitsTheSpentOne)
    {
        var battle = TestBattle.Create();
        var hitter = battle.Add(Attacker, Hitter, 1, 10, 10);
        var twin = TestBattle.Fixed("twin", 1);
        var fresh = battle.Add(Defender, twin, 50, 12, 10);
        var spent = battle.Add(Defender, twin, 50, 8, 10);
        battle.StartWith(hitter);
        spent.HasRetaliated = true; // the round has started: it hit back earlier in it

        BattleAi.Act(battle.State, battle.Engine, level);

        // The same blow at the same stack: the older levels take the lower id, the new one the stack that cannot hit back this round.
        Assert.Equal((hitsTheSpentOne ? spent : fresh).Id, HitBy(battle, hitter));
    }

    [Fact]
    public void RetaliationLoss_IsTheBlowOfTheCreaturesLeft_AndNoneWhenTheyCannotHitBack()
    {
        var battle = TestBattle.Create();
        var hitter = battle.Add(Attacker, Hitter, 1, 10, 10);
        var target = battle.Add(Defender, TestBattle.Fixed("twin", 1), 10, 11, 10);
        var full = BattleAi.RetaliationLoss(battle.State, hitter, target, isRanged: false, expected: 0);

        Assert.True(full > 0);
        Assert.InRange(BattleAi.RetaliationLoss(battle.State, hitter, target, isRanged: false, expected: 50), (full / 2) - 1, (full / 2) + 1);
        Assert.Equal(0, BattleAi.RetaliationLoss(battle.State, hitter, target, isRanged: false, expected: 100)); // destroyed
        Assert.Equal(0, BattleAi.RetaliationLoss(battle.State, hitter, target, isRanged: true, expected: 0)); // it cannot shoot back
        target.HasRetaliated = true;
        Assert.Equal(0, BattleAi.RetaliationLoss(battle.State, hitter, target, isRanged: false, expected: 0));
    }

    [Theory]
    [InlineData(BattleAiLevel.FocusFire, false)]
    [InlineData(BattleAiLevel.AvoidRetaliation, true)]
    public void AvoidRetaliation_WaitsForAnOwnShooterStillToAct(BattleAiLevel level, bool waits)
    {
        var battle = TestBattle.Create();
        var hitter = battle.Add(Attacker, Hitter, 5, 10, 10);
        battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 10), 5, 0, 20);
        var target = battle.Add(Defender, TestBattle.Fixed("twin", 1), 50, 12, 10);
        battle.StartWith(hitter);

        BattleAi.Act(battle.State, battle.Engine, level);

        Assert.Equal(waits, battle.Events.OfType<BattleUnitWaited>().Any(e => e.Unit == hitter.Id));
        Assert.Equal(!waits, battle.Events.OfType<BattleUnitAttacked>().Any(e => e.Attacker == hitter.Id && e.Target == target.Id));
    }

    [Fact]
    public void ProtectShooters_WalksAtTheEnemyShooter_RatherThanTheNearestStack()
    {
        int GapToArcher(BattleAiLevel level)
        {
            var battle = TestBattle.Create();
            var walker = battle.Add(Attacker, Hitter with { Speed = 4 }, 5, 2, 10);
            battle.Add(Defender, TestBattle.Fixed("footman", 1), 5, 14, 10);
            var archer = battle.Add(Defender, TestBattle.Fixed("archer", 1, shots: 10), 5, 2, 23);
            battle.StartWith(walker);

            BattleAi.Act(battle.State, battle.Engine, level);

            Assert.NotEqual(new CellPosition(2, 10), walker.Position);
            return walker.Rect.GapTo(archer.Rect);
        }

        Assert.True(GapToArcher(BattleAiLevel.ProtectShooters) < GapToArcher(BattleAiLevel.AvoidRetaliation));
    }

    [Theory]
    [InlineData(BattleAiLevel.ProtectShooters, false)]
    [InlineData(BattleAiLevel.HoldBack, true)]
    public void HoldBack_WaitsForTheEnemyToCome_WhenThereIsNothingToHit(BattleAiLevel level, bool waits)
    {
        var battle = TestBattle.Create();
        var walker = battle.Add(Attacker, Hitter with { Speed = 4 }, 5, 2, 10);
        battle.Add(Defender, TestBattle.Fixed("footman", 1), 5, 26, 10);
        battle.StartWith(walker);

        BattleAi.Act(battle.State, battle.Engine, level);

        Assert.Equal(waits, battle.Events.OfType<BattleUnitWaited>().Any(e => e.Unit == walker.Id));
        Assert.Equal(!waits, walker.Position != new CellPosition(2, 10));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void HoldBack_KeepsItsGroundBesideItsShooters_WhileItsSideShootsBetter(bool enemyShootsToo, bool holds)
    {
        var battle = TestBattle.Create();
        var walker = battle.Add(Attacker, Hitter with { Speed = 4 }, 5, 2, 10);
        battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 10), 20, 0, 12);
        battle.Add(Defender, TestBattle.Fixed("footman", 1), 5, 26, 10);
        if (enemyShootsToo)
        {
            battle.Add(Defender, TestBattle.Fixed("archer", 1, shots: 10), 40, 28, 20);
        }

        battle.StartWith(walker);
        battle.State.WaitedThisRound.Add(walker.Id); // it has waited already: now it holds or walks

        BattleAi.Act(battle.State, battle.Engine, BattleAiLevel.HoldBack);

        Assert.Equal(holds, walker.Position == new CellPosition(2, 10));
        Assert.Equal(holds, battle.Events.OfType<BattleUnitDefended>().Any(e => e.Unit == walker.Id));
    }

    private static readonly CreatureDefinition Hitter = TestBattle.Fixed("hitter", 10, speed: 2);

    /// <summary>The hitters' expected blow at a creature with the test stats (at least the lone creature's 10 hit points).</summary>
    private static int ExpectedBlow()
    {
        var probe = TestBattle.Create();
        var hitter = probe.Add(Attacker, Hitter, 50, 10, 10);
        var target = probe.Add(Defender, TestBattle.Fixed("tough", 1, health: 100), 100, 12, 10);
        var expected = DamageCalculator.Expected(probe.State, hitter, target, isRanged: false);
        Assert.InRange(expected, 10, 10_000);
        return expected;
    }

    private static int ShotAt(TestBattle battle, BattleUnit shooter) =>
        battle.Events.OfType<BattleUnitAttacked>().Single(e => e.Attacker == shooter.Id && e.Kind == BattleAttackKind.Ranged).Target;

    private static int HitBy(TestBattle battle, BattleUnit attacker) =>
        battle.Events.OfType<BattleUnitAttacked>().Single(e => e.Attacker == attacker.Id && e.Kind == BattleAttackKind.Melee).Target;
}
