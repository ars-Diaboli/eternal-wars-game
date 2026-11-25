using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Domain.Creatures.CapabilityKind;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The attack estimate on the status bar (ADR-314): the damage range, the kills, whether the target strikes back.</summary>
public class AttackEstimateTests
{
    private static CreatureDefinition Ranged(string id, int min, int max, int health = 10, int shots = 0, params CreatureCapability[] capabilities) =>
        TestBattle.Fixed(id, max, health: health, shots: shots, capabilities: capabilities) with { MinDamage = min };

    [Fact]
    public void Melee_GivesTheLowestAndHighestRoll_AndTheKillsOfEach()
    {
        var battle = TestBattle.Create(attackerAttack: 4); // +4 Attack: +20 %
        var swordsmen = battle.Add(Attacker, Ranged("swordsmen", 2, 4), 10, 0, 0);
        var wolves = battle.Add(Defender, TestBattle.Fixed("wolves", 1, health: 10), 10, 2, 0);

        var estimate = AttackEstimate.Melee(battle.State, swordsmen, wolves);

        Assert.Equal(new AttackEstimate(24, 48, 2, 4, StrikesBack: true), estimate);
    }

    [Theory]
    [InlineData(4, 0)] // the wounded top creature (5 of 10) takes the blow first
    [InlineData(5, 1)]
    [InlineData(14, 1)]
    [InlineData(15, 2)]
    [InlineData(25, 3)] // all of them
    [InlineData(99, 3)]
    public void Kills_TakeTheWoundedTopCreatureFirst(int damage, int kills)
    {
        var battle = TestBattle.Create();
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 10), 3, 2, 0);
        target.TopHealth = 5;

        Assert.Equal(kills, AttackEstimate.Kills(target, damage));
    }

    [Fact]
    public void BlessFixesTheRoll_SoBothEndsAreTheMaximum()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, Ranged("a", 2, 4), 10, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 100), 10, 2, 0);
        attacker.MutableSpells.Add(new ActiveSpell("light.bless", Attacker, new SpellModifiers(Roll: 1), 3));

        var estimate = AttackEstimate.Melee(battle.State, attacker, target);

        Assert.Equal((40, 40), (estimate.MinDamage, estimate.MaxDamage));
    }

    [Fact]
    public void TheTargetStrikesBack_ByTheEnginesRule()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 1), 1, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 100), 10, 2, 0);
        Assert.True(AttackEstimate.Melee(battle.State, attacker, target).StrikesBack);

        target.HasRetaliated = true; // no return blow left this round
        Assert.False(AttackEstimate.Melee(battle.State, attacker, target).StrikesBack);

        var unlimited = battle.Add(Defender, TestBattle.Fixed("u", 1, health: 100, capabilities: new CreatureCapability(UnlimitedRetaliation)), 10, 2, 4);
        unlimited.HasRetaliated = true;
        Assert.True(AttackEstimate.Melee(battle.State, attacker, unlimited).StrikesBack);

        var unanswered = battle.Add(Attacker, TestBattle.Fixed("n", 1, capabilities: new CreatureCapability(NoEnemyRetaliation)), 1, 0, 8);
        Assert.False(AttackEstimate.Melee(battle.State, unanswered, unlimited).StrikesBack);
    }

    [Fact]
    public void ATargetTheWeakestBlowKills_StrikesBackOnlyWithFirstStrike()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 50), 10, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 10), 3, 2, 0);
        var first = battle.Add(Defender, TestBattle.Fixed("f", 1, health: 10, capabilities: new CreatureCapability(FirstStrike)), 3, 2, 4);

        Assert.Equal((3, 3, false), (AttackEstimate.Melee(battle.State, attacker, target).MinKills, AttackEstimate.Melee(battle.State, attacker, target).MaxKills,
            AttackEstimate.Melee(battle.State, attacker, target).StrikesBack));
        Assert.True(AttackEstimate.Melee(battle.State, attacker, first).StrikesBack); // it answers before the blow lands
    }

    [Fact]
    public void TheEnginesBlow_FallsInTheEstimate()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            // 10 to 90 damage against 70 health: some blows kill the whole stack, which then cannot strike back.
            var battle = TestBattle.Create(seed: seed);
            var attacker = battle.Add(Attacker, Ranged("a", 1, 9), 10, 0, 0);
            var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 7), 10, 2, 0);
            battle.StartWith(attacker);
            var estimate = AttackEstimate.Melee(battle.State, attacker, target);

            battle.Engine.Attack(target.Id, null);

            var blow = battle.Events.OfType<BattleUnitAttacked>().First(e => e.Kind == BattleAttackKind.Melee);
            Assert.InRange(blow.Damage, estimate.MinDamage, estimate.MaxDamage);
            Assert.InRange(blow.Killed, estimate.MinKills, estimate.MaxKills);
            Assert.Equal(estimate.StrikesBack && target.IsAlive, battle.Events.OfType<BattleUnitAttacked>().Any(e => e.Kind == BattleAttackKind.Retaliation));
        }
    }

    [Fact]
    public void AFirstStrikeTarget_AnswersBeforeTheBlow_EvenOneTheBlowWouldKill()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 50, health: 100), 10, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("f", 1, health: 10, capabilities: new CreatureCapability(FirstStrike)), 3, 2, 0);
        battle.StartWith(attacker);
        Assert.True(AttackEstimate.Melee(battle.State, attacker, target).StrikesBack);

        battle.Engine.Attack(target.Id, null);

        Assert.Equal(BattleAttackKind.Retaliation, battle.Events.OfType<BattleUnitAttacked>().First().Kind);
        Assert.False(target.IsAlive);
    }

    [Fact]
    public void Shot_TakesTheRangePenalty_AndAShooterWithRangedRetaliationShootsBack()
    {
        var battle = TestBattle.Create();
        var shooter = battle.Add(Attacker, TestBattle.Fixed("s", 10, shots: 5), 8, 0, 0);
        var far = battle.Add(Defender, TestBattle.Fixed("far", 1, health: 100), 1, 22, 0); // 20 cells: 2/8
        var monk = battle.Add(Defender, TestBattle.Fixed("monk", 1, health: 100, shots: 5, capabilities: new CreatureCapability(RangedRetaliation)), 10, 12, 6);

        Assert.Equal(new AttackEstimate(60, 60, 0, 0, StrikesBack: false), AttackEstimate.Shot(battle.State, shooter, far));
        Assert.True(AttackEstimate.Shot(battle.State, shooter, monk)!.StrikesBack);

        battle.Add(Defender, TestBattle.Fixed("blocker", 1, health: 100), 1, 2, 0); // next to the shooter: it cannot shoot
        Assert.Null(AttackEstimate.Shot(battle.State, shooter, far));
    }

    [Fact]
    public void ADamageSpell_HasOneDamage_AndNothingStrikesBack()
    {
        var arrow = new KnownSpell(SpellCatalog.Default.Get("arcana.magic-arrow"), 1, 5);
        var bless = new KnownSpell(SpellCatalog.Default.Get("light.bless"), 1, 5);
        var battle = TestBattle.Create(attackerCaster: new BattleCaster(0, 1, [arrow, bless], null, 100));
        battle.Add(Attacker, TestBattle.Fixed("a", 1), 1, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 15), 10, 20, 0);
        var other = battle.Add(Defender, TestBattle.Fixed("o", 1, health: 15), 10, 20, 8);
        var damage = BattleMagic.Damage(battle.State, Attacker, arrow, target);

        var estimate = AttackEstimate.Spell(battle.State, Attacker, arrow, target.Id, null, target);

        Assert.Equal(new AttackEstimate(damage, damage, damage / 15, damage / 15, StrikesBack: false), estimate);
        Assert.Null(AttackEstimate.Spell(battle.State, Attacker, arrow, target.Id, null, other)); // not in its reach
        Assert.Null(AttackEstimate.Spell(battle.State, Attacker, bless, target.Id, null, target)); // not a damage spell
    }
}
