using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

public class DamageCalculatorTests
{
    [Theory]
    [InlineData(10, 10, 100)] // equal: base × count
    [InlineData(14, 10, 120)] // +4 attack: +20 %
    [InlineData(90, 10, 400)] // +80 attack: capped at +300 %
    [InlineData(10, 14, 90)] // +4 defense: −10 %
    [InlineData(10, 90, 30)] // capped at −70 %
    public void Damage_FollowsHomm3AttackDefenseRule(int attack, int defense, int expected)
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 10, attack: attack), 10, 0, 0);
        var defender = battle.Add(Defender, TestBattle.Fixed("d", 1, defense: defense), 10, 2, 0);

        Assert.Equal(expected, DamageCalculator.Damage(battle.State, attacker, defender, new HitModifiers(10, IsRanged: false)));
    }

    [Fact]
    public void Damage_AddsHeroStats_AndDefendBonus()
    {
        var battle = TestBattle.Create(attackerAttack: 4);
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 10), 10, 0, 0);
        var defender = battle.Add(Defender, TestBattle.Fixed("d", 1), 10, 2, 0);

        Assert.Equal(120, DamageCalculator.Damage(battle.State, attacker, defender, new HitModifiers(10, false)));

        defender.IsDefending = true; // defense 10 → 12
        Assert.Equal(110, DamageCalculator.Damage(battle.State, attacker, defender, new HitModifiers(10, false)));
    }

    [Fact]
    public void Ranged_LosesTheRangedPenaltyInEighths()
    {
        var battle = TestBattle.Create();
        var shooter = battle.Add(Attacker, TestBattle.Fixed("s", 10, shots: 5), 8, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1), 1, 10, 0);

        Assert.Equal(80, DamageCalculator.Damage(battle.State, shooter, target, new HitModifiers(10, true)));
        Assert.Equal(60, DamageCalculator.Damage(battle.State, shooter, target, new HitModifiers(10, true, PenaltyEighths: 2)));
        Assert.Equal(10, DamageCalculator.Damage(battle.State, shooter, target, new HitModifiers(10, true, PenaltyEighths: 7)));
    }

    [Theory]
    [InlineData(11, 0)] // 9 cells between the 2-cell footprints: none
    [InlineData(12, 1)] // 10 cells: 1/8
    [InlineData(22, 2)] // 20 cells: 2/8
    [InlineData(28, 2)]
    public void DistancePenalty_IsAnEighthPerWholeTenCellsBetween(int x, int eighths)
    {
        var battle = TestBattle.Create();
        var shooter = battle.Add(Attacker, TestBattle.Fixed("s", 10, shots: 5), 8, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1), 1, x, 0);

        Assert.True(BattleGrid.CanShoot(battle.State, shooter, target, out var penalty));
        Assert.Equal(eighths, penalty);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    [InlineData(6, 0)] // never below 0
    public void Marksmanship_TakesEighthsOffTheRangedPenalty(int marksmanship, int eighths)
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { MarksmanshipEighths = marksmanship });
        var shooter = battle.Add(Attacker, TestBattle.Fixed("s", 10, shots: 5), 8, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1), 1, 22, 0); // 20 cells: 2/8

        Assert.Equal(eighths, BattleGrid.RangedPenaltyEighths(battle.State, shooter, target));
    }

    [Fact]
    public void Marksmanship_ComesFromTheHerosSkill()
    {
        Assert.Equal([0, 2, 4, 6], new[] { SkillLevel.None, SkillLevel.Basic, SkillLevel.Advanced, SkillLevel.Expert }.Select(SkillEffects.MarksmanshipEighths));
        var state = TestWorld.Create("A.........B");
        state.GetHero(TestWorld.A).Skills.Set(BuiltInSkills.Marksmanship, SkillLevel.Advanced);
        Assert.Equal(4, HeroBattleBonuses.For(state.GetHero(TestWorld.A), state.Creatures).MarksmanshipEighths);
    }

    [Fact]
    public void APenaltyAboveSevenEighths_MakesTheShotImpossible()
    {
        Assert.Equal(7, BattleState.MaxRangedPenaltyEighths);
        var battle = TestBattle.Create(width: 100);
        var shooter = battle.Add(Attacker, TestBattle.Fixed("s", 10, shots: 5), 8, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1), 1, 82, 0); // 80 cells: 8/8

        Assert.False(BattleGrid.CanShoot(battle.State, shooter, target, out var penalty));
        Assert.Equal(8, penalty);
    }

    [Fact]
    public void MeleePenalty_HalvesMeleeDamage_ShootersWithoutItFightAtFullStrength()
    {
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 10, shots: 5, capabilities: new CreatureCapability(CapabilityKind.MeleePenalty)), 10, 0, 0);
        var marksman = battle.Add(Attacker, TestBattle.Fixed("marksman", 10, shots: 5), 10, 0, 4);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1), 1, 2, 2);

        Assert.Equal(50, DamageCalculator.Damage(battle.State, archer, target, new HitModifiers(10, false)));
        Assert.Equal(100, DamageCalculator.Damage(battle.State, marksman, target, new HitModifiers(10, false)));
    }

    [Fact]
    public void EngagementAndHitHistoryAbilities_AddPercentages()
    {
        var battle = TestBattle.Create();
        var hound = battle.Add(Attacker, TestBattle.Fixed("hound", 10, capabilities: new CreatureCapability(CapabilityKind.PackHunter, 15)), 10, 0, 2);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1), 1, 2, 2);
        battle.Add(Attacker, TestBattle.Fixed("friend1", 1), 1, 4, 2);
        battle.Add(Attacker, TestBattle.Fixed("friend2", 1), 1, 2, 4);

        Assert.Equal(130, DamageCalculator.Damage(battle.State, hound, target, new HitModifiers(10, false)));

        var tormentor = battle.Add(Attacker, TestBattle.Fixed("tormentor", 10, capabilities: new CreatureCapability(CapabilityKind.Relentless, 25)), 10, 2, 0);
        target.HitsThisRound = 2;
        Assert.Equal(150, DamageCalculator.Damage(battle.State, tormentor, target, new HitModifiers(10, false)));
    }
}
