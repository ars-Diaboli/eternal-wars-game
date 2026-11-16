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
