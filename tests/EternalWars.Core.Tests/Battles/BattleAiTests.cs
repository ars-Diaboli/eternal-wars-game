using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Events;
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

    [Theory]
    [InlineData(BattleAiLevel.HoldBack, "light.bless")]
    [InlineData(BattleAiLevel.CastAhead, "arcana.magic-arrow")]
    public void CastAhead_CastsWhatPaysWithinTheNextRound_NotWhatLooksBest(BattleAiLevel level, string cast)
    {
        // A slow stack far from the enemy: Bless looks worth more than the arrow (a share of its value for three rounds), but it cannot strike
        // in the two rounds the look-ahead plays, so only one round of the blessing is left to count; the arrow's creatures stay dead.
        var battle = TestBattle.Create(attackerCaster: Caster("light.bless", "arcana.magic-arrow"));
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 1, speed: 1) with { FightValue = 100 }, 8, 0, 10);
        battle.Add(Defender, TestBattle.Fixed("target", 1, speed: 1) with { FightValue = 20 }, 10, 28, 10);
        battle.StartWith(walker);
        var options = BattleAi.CastOptions(battle.State, Attacker);
        Assert.Equal("light.bless", options[0].Spell);
        Assert.Contains(options, o => o.Spell == "arcana.magic-arrow");

        BattleAi.Act(battle.State, battle.Engine, level);

        Assert.Equal(cast, Assert.Single(battle.Events.OfType<BattleSpellCast>()).Spell);
    }

    [Fact]
    public void CastAhead_PlaysItsCopiesUnheard_AndTheSameBattleTheSameWay()
    {
        List<IEvent> Turn()
        {
            var battle = TestBattle.Create(attackerCaster: Caster("light.bless", "arcana.magic-arrow", "arcana.slow"));
            var hitter = battle.Add(Attacker, Hitter with { Speed = 6 }, 20, 2, 10);
            battle.Add(Attacker, TestBattle.Fixed("archer", 2, shots: 10) with { FightValue = 30 }, 10, 0, 14);
            battle.Add(Defender, TestBattle.Fixed("footman", 3, speed: 5) with { FightValue = 25 }, 15, 20, 10);
            battle.Add(Defender, TestBattle.Fixed("archer", 2, shots: 10) with { FightValue = 30 }, 10, 28, 6);
            battle.StartWith(hitter);
            battle.Events.Clear();
            BattleAi.Act(battle.State, battle.Engine, BattleAiLevel.CastAhead);
            return battle.Events;
        }

        var events = Turn();
        Assert.Equal(events, Turn());
        Assert.True(events.OfType<BattleTurnStarted>().Count() <= 1); // the next unit's turn, nothing from the copies' turns
        Assert.DoesNotContain(events, e => e is BattleRoundStarted);
        Assert.Single(events.OfType<BattleSpellCast>());
    }

    [Fact]
    public void Standing_IsTheHealthValueLessTheEnemys_PlusTheLastingSpellsLeft()
    {
        var battle = TestBattle.Create();
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1) with { FightValue = 30 }, 10, 0, 10);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1) with { FightValue = 20 }, 5, 20, 10);
        battle.StartWith(own);
        Assert.Equal((10 * 30) - (5 * 20), BattleAi.Standing(battle.State, Attacker));
        Assert.Equal((5 * 20) - (10 * 30), BattleAi.Standing(battle.State, Defender));

        // A buff with two rounds left (this one and the next) on the own stack, a curse of the attacker's on the enemy with one.
        own.MutableSpells.Add(new ActiveSpell("buff", Attacker, new EternalWars.Core.Domain.Magic.SpellModifiers(Attack: 2), battle.State.Round + 1));
        enemy.MutableSpells.Add(new ActiveSpell("curse", Attacker, new EternalWars.Core.Domain.Magic.SpellModifiers(Defense: -2), battle.State.Round));
        var lasting = (10 * 30 * BattleAi.EnchantPercentPerRound * 2 / 100) + (5 * 20 * BattleAi.EnchantPercentPerRound / 100);
        Assert.Equal((10 * 30) - (5 * 20) + lasting, BattleAi.Standing(battle.State, Attacker));
    }

    [Fact]
    public void Sieges_AimTheSiegeEngineAtTheGate_ThenTheKeepAndTowers_ThenTheWallsBesideTheGate()
    {
        var siege = SiegeRules.Create(new SiegeSpec(3, 5));
        var order = new List<SiegeStructureKind>();
        var walls = new List<int>();
        while (BattleAi.AimedStructure(siege) is { } next)
        {
            order.Add(next.Kind);
            if (next.Kind == SiegeStructureKind.Wall)
            {
                walls.Add(next.Id);
            }

            next.Health = 0;
        }

        Assert.Equal(
            [SiegeStructureKind.Gate, SiegeStructureKind.Keep, SiegeStructureKind.Tower, SiegeStructureKind.Tower, .. Enumerable.Repeat(SiegeStructureKind.Wall, 4)],
            order);
        Assert.Equal([2, 3], walls.Take(2).Order()); // the sections either side of the gate first
    }

    [Theory]
    [InlineData(BattleAiLevel.CastAhead, false)]
    [InlineData(BattleAiLevel.Sieges, true)]
    public void Sieges_ABesiegerDoesNotReachTheGateAheadOfItsSide(BattleAiLevel level, bool gathers)
    {
        var battle = TestBattle.Create(siege: new SiegeSpec(1, 5));
        var runner = battle.Add(Attacker, Hitter with { Speed = 10 }, 5, 14, 11);
        battle.Add(Attacker, Hitter with { Speed = 1 }, 50, 0, 11); // most of the side, far behind
        battle.Add(Defender, TestBattle.Fixed("footman", 1), 5, 32, 11);
        battle.StartWith(runner);

        BattleAi.Act(battle.State, battle.Engine, level);

        var gate = battle.State.Siege!.Gate;
        Assert.Equal(!gathers, battle.Events.OfType<SiegeStructureDamaged>().Any(e => e.Unit == runner.Id));
        if (gathers)
        {
            Assert.Equal(BattleAi.GatherGap + 1, runner.Rect.GapTo(gate.Rect));
        }
    }

    [Fact]
    public void Sieges_ABesiegerGoesIn_WhenItsSideCanFollow()
    {
        var battle = TestBattle.Create(siege: new SiegeSpec(1, 5));
        var runner = battle.Add(Attacker, Hitter with { Speed = 10 }, 5, 14, 11);
        battle.Add(Attacker, Hitter with { Speed = 10 }, 50, 12, 15);
        battle.Add(Defender, TestBattle.Fixed("footman", 1), 5, 32, 11);
        battle.StartWith(runner);

        BattleAi.Act(battle.State, battle.Engine, BattleAiLevel.Sieges);

        Assert.Contains(battle.Events.OfType<SiegeStructureDamaged>(), e => e.Unit == runner.Id);
    }

    [Theory]
    [InlineData(BattleAiLevel.CastAhead, false)]
    [InlineData(BattleAiLevel.Sieges, true)]
    public void Sieges_ADefenderBlocksTheBrokenGate(BattleAiLevel level, bool blocks)
    {
        var battle = TestBattle.Create(siege: new SiegeSpec(1, 5));
        battle.State.Siege!.Gate.Health = 0;
        battle.Add(Attacker, Hitter with { Speed = 4 }, 5, 4, 11);
        var guard = battle.Add(Defender, TestBattle.Fixed("guard", 1, speed: 10), 5, 33, 11);
        battle.StartWith(guard);

        BattleAi.Act(battle.State, battle.Engine, level);

        var gate = battle.State.Siege.Gate;
        Assert.Equal(blocks, guard.Rect.GapTo(gate.Rect) == 0);
        Assert.True(guard.Position.X >= SiegeRules.WallX); // it never goes out
    }

    private static BattleCaster Caster(params string[] spells) => new(
        0, 1, spells.Select(s => new KnownSpell(EternalWars.Core.Domain.Magic.SpellCatalog.Default.Get(s), 1, EternalWars.Core.Domain.Magic.SpellCatalog.Default.Get(s).Mana)).ToList(), null, 100);

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
