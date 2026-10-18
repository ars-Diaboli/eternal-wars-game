using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Leadership, Alertness and Stronghold's Primal Fury: Blood Rage and warcries (ADR-067).</summary>
public class PrimalFuryAndLeadershipTests
{
    // Leadership and Alertness -----------------------------------------------------------------------

    [Theory]
    [InlineData(SkillLevel.None, 0)]
    [InlineData(SkillLevel.Basic, 1)]
    [InlineData(SkillLevel.Advanced, 2)]
    [InlineData(SkillLevel.Expert, 3)]
    public void Leadership_AddsMoraleToTheHero_AndSoToItsBattleBonuses(SkillLevel level, int morale)
    {
        var state = Create("A.........B");
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.Leadership, level);

        Assert.Equal(morale, hero.TotalMorale);
        Assert.Equal(morale, HeroBattleBonuses.For(hero, state.Creatures).Morale);
    }

    [Fact]
    public void Leadership_StacksWithTheHeroAndCreatureMorale_WithinFive()
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { Morale = 3 + 2 });
        var unit = battle.Add(Attacker, TestBattle.Fixed("brave", 1) with { Morale = 2 }, 1, 0, 0);

        Assert.Equal(5, battle.State.MoraleOf(unit));
    }

    [Theory]
    [InlineData(SkillLevel.None, 0)]
    [InlineData(SkillLevel.Basic, 5)]
    [InlineData(SkillLevel.Advanced, 10)]
    [InlineData(SkillLevel.Expert, 15)]
    public void Alertness_GivesInitiativePercent_ToTheHeroesBattleBonuses(SkillLevel level, int percent)
    {
        var state = Create("A.........B");
        state.GetHero(A).Skills.Set(BuiltInSkills.Alertness, level);

        Assert.Equal(percent, SkillEffects.AlertnessInitiativePercent(level));
        Assert.Equal(percent, HeroBattleBonuses.For(state.GetHero(A), state.Creatures).InitiativePercent);
    }

    [Theory]
    [InlineData(5, 10, 11)] // 10.5 rounds up
    [InlineData(10, 10, 11)]
    [InlineData(15, 10, 12)] // 11.5
    [InlineData(5, 9, 9)] // 9.45
    [InlineData(15, 16, 18)] // 18.4
    public void Alertness_RaisesInitiative_RoundedToTheNearestPoint_ForCreatureStacksOnly(int percent, int initiative, int expected)
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { InitiativePercent = percent });
        var unit = battle.Add(Attacker, TestBattle.Fixed("quick", 1, initiative: initiative), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("plain", 1, initiative: initiative), 1, 20, 0);

        Assert.Equal(expected, battle.State.InitiativeOf(unit));
        Assert.Equal(initiative, battle.State.InitiativeOf(enemy));
    }

    // Blood Rage ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(SkillLevel.Basic, 2)]
    [InlineData(SkillLevel.Advanced, 4)]
    [InlineData(SkillLevel.Expert, 6)]
    [InlineData(SkillLevel.Master, 8)]
    public void BloodRage_CapGrowsWithTheLevel(SkillLevel level, int cap) =>
        Assert.Equal(cap, PrimalFury.BloodRageMaxAttack(level));

    [Fact]
    public void BloodRage_GivesAttackForKills_UpToTheCap_AndRaisesDamage()
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { BloodRage = SkillLevel.Basic });
        var rager = battle.Add(Attacker, TestBattle.Fixed("rager", damage: 5, health: 1000, attack: 10), 10, 0, 0);
        var victim = battle.Add(Defender, TestBattle.Fixed("victim", damage: 1, health: 10, defense: 10), 100, 2, 0);
        var before = DamageCalculator.Damage(battle.State, rager, victim, new HitModifiers(5, false));

        for (var i = 0; i < 4; i++)
        {
            battle.StartWith(rager);
            Assert.Null(battle.Engine.Attack(victim.Id, null));
        }

        Assert.Equal(2, rager.RageAttack); // two kills raise it, then the cap holds
        Assert.Equal([1, 2], battle.Events.OfType<BattleRageGained>().Select(e => e.Attack));
        Assert.True(DamageCalculator.Damage(battle.State, rager, victim, new HitModifiers(5, false)) > before);
    }

    [Fact]
    public void BloodRage_NeedsTheSkill_AndAKill()
    {
        var plain = TestBattle.Create();
        var a = plain.Add(Attacker, TestBattle.Fixed("a", damage: 5), 10, 0, 0);
        var d = plain.Add(Defender, TestBattle.Fixed("d", damage: 1), 100, 2, 0);
        plain.StartWith(a);
        Assert.Null(plain.Engine.Attack(d.Id, null));
        Assert.Equal(0, a.RageAttack);

        var raging = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { BloodRage = SkillLevel.Master });
        var weak = raging.Add(Attacker, TestBattle.Fixed("weak", damage: 1, health: 10), 1, 0, 0);
        var tough = raging.Add(Defender, TestBattle.Fixed("tough", damage: 1, health: 1000), 1, 2, 0);
        raging.StartWith(weak);
        Assert.Null(raging.Engine.Attack(tough.Id, null));
        Assert.Equal(0, weak.RageAttack); // no creature died
        Assert.Empty(raging.Events.OfType<BattleRageGained>());
    }

    // Warcries --------------------------------------------------------------------------------------

    [Theory]
    [InlineData(SkillLevel.None, false, false, false)]
    [InlineData(SkillLevel.Basic, true, false, false)]
    [InlineData(SkillLevel.Advanced, true, true, false)]
    [InlineData(SkillLevel.Expert, true, true, true)]
    public void TheWarcryLevel_DecidesWhichWarcriesAreKnown(SkillLevel level, bool rally, bool intimidate, bool terror)
    {
        Assert.Equal(rally, PrimalFury.Knows(level, Warcry.Rally));
        Assert.Equal(intimidate, PrimalFury.Knows(level, Warcry.Intimidate));
        Assert.Equal(terror, PrimalFury.Knows(level, Warcry.Terror));
    }

    [Fact]
    public void Rally_RaisesOwnMoraleForThreeRoundsAfterTheOneItWasUsedIn()
    {
        var (battle, own, enemy) = Duel(SkillLevel.Basic);
        battle.StartWith(own);

        Assert.Null(battle.Engine.UseWarcry(Attacker, Warcry.Rally));

        var round = battle.State.Round;
        Assert.Equal(1, battle.State.MoraleOf(own));
        Assert.Equal(0, battle.State.MoraleOf(enemy));
        battle.State.Round = round + PrimalFury.RallyRounds;
        Assert.Equal(1, battle.State.MoraleOf(own));
        battle.State.Round = round + PrimalFury.RallyRounds + 1;
        Assert.Equal(0, battle.State.MoraleOf(own));
        var used = Assert.Single(battle.Events.OfType<BattleWarcryUsed>());
        Assert.Equal(Warcry.Rally, used.Warcry);
        Assert.Equal([own.Id], used.Affected);
    }

    [Fact]
    public void Intimidate_LowersEnemyMoraleAndInitiativeForTwoRounds_AndNeedsAdvanced()
    {
        var (basic, own, _) = Duel(SkillLevel.Basic);
        basic.StartWith(own);
        Assert.NotNull(basic.Engine.UseWarcry(Attacker, Warcry.Intimidate));

        var (battle, ownAdvanced, enemy) = Duel(SkillLevel.Advanced);
        battle.StartWith(ownAdvanced);
        Assert.Null(battle.Engine.UseWarcry(Attacker, Warcry.Intimidate));

        Assert.Equal(-1, battle.State.MoraleOf(enemy));
        Assert.Equal(8, battle.State.InitiativeOf(enemy)); // 10 − 20 %
        Assert.Equal(10, battle.State.InitiativeOf(ownAdvanced));
        battle.State.Round += PrimalFury.IntimidateRounds + 1;
        Assert.Equal(0, battle.State.MoraleOf(enemy));
        Assert.Equal(10, battle.State.InitiativeOf(enemy));
    }

    [Fact]
    public void Terror_MakesSomeEnemyStacksLoseTheirNextAction_ThenThePlayAgainsPass()
    {
        var battle = TestBattle.Create(seed: 11, attackerBonuses: HeroBattleBonuses.None with { Warcry = SkillLevel.Expert });
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var enemies = Enumerable.Range(0, 20).Select(i => battle.Add(Defender, TestBattle.Fixed($"e{i}", 1), 1, 20, i)).ToList();
        battle.StartWith(own);

        Assert.Null(battle.Engine.UseWarcry(Attacker, Warcry.Terror));

        var terrified = enemies.Where(e => e.LosesNextAction).ToList();
        Assert.InRange(terrified.Count, 1, 19); // 30 % each
        Assert.Equal(terrified.Select(e => e.Id).Order(), Assert.Single(battle.Events.OfType<BattleWarcryUsed>()).Affected.Order());

        Assert.Null(battle.Engine.Defend());
        while (battle.State.Phase == BattlePhase.Fighting && battle.State.Round == 1 && battle.State.ActiveUnit is not null)
        {
            battle.Engine.Defend();
        }

        Assert.All(terrified, e => Assert.Contains(new BattleUnitTerrified(e.Id), battle.Events));
        Assert.All(terrified, e => Assert.False(e.LosesNextAction));
    }

    [Fact]
    public void AWarcry_WorksOncePerBattle_AndOnlyOneARound()
    {
        var (battle, own, _) = Duel(SkillLevel.Expert);
        battle.StartWith(own);

        Assert.Null(battle.Engine.UseWarcry(Attacker, Warcry.Rally));
        Assert.NotNull(battle.Engine.UseWarcry(Attacker, Warcry.Intimidate)); // one a round
        battle.State.Round++;
        Assert.NotNull(battle.Engine.UseWarcry(Attacker, Warcry.Rally)); // once a battle
        Assert.Null(battle.Engine.UseWarcry(Attacker, Warcry.Intimidate));
    }

    [Fact]
    public void ASideWithoutTheSkill_UsesNoWarcry()
    {
        var (battle, own, _) = Duel(SkillLevel.None);
        battle.StartWith(own);

        Assert.NotNull(battle.Engine.UseWarcry(Attacker, Warcry.Rally));
        Assert.NotNull(battle.Engine.UseWarcry(Defender, Warcry.Rally));
        Assert.Empty(battle.Events.OfType<BattleWarcryUsed>());
    }

    [Fact]
    public void TheBattleAi_UsesWarcriesForAStrongholdHero()
    {
        var (battle, own, _) = Duel(SkillLevel.Expert);
        battle.StartWith(own);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.Equal(Warcry.Terror, Assert.Single(battle.Events.OfType<BattleWarcryUsed>()).Warcry); // the strongest first
    }

    [Fact]
    public void TheWarcryCommand_IsRefusedOutsideABattle()
    {
        var state = Create("A.........B");
        var session = Start(state);

        var result = session.Execute(new BattleWarcryCommand(Blue, Warcry.Rally));

        Assert.False(result.IsAccepted);
    }

    // The Primal Fury skills -----------------------------------------------------------------------

    [Fact]
    public void StrongholdsGroup_IsPrimalFury_WithBloodRageWarcryAndFactionSpellResistance()
    {
        var group = BuiltInSkills.GetGroup(BuiltInSkills.RageGroup);

        Assert.Equal("Primal Fury", group.Name);
        Assert.Equal(TownIds.Stronghold, group.Faction);
        Assert.Equal(["Blood Rage", "Warcry", "Spell resistance (faction)"], BuiltInSkills.SkillsOf(BuiltInSkills.RageGroup).Select(s => s.Name));
        Assert.Equal(
            [SkillLevel.Master, SkillLevel.Expert, SkillLevel.Expert],
            BuiltInSkills.SkillsOf(BuiltInSkills.RageGroup).Select(s => s.MaxLevel)); // 4 + 3 + 3 points
        Assert.Equal(SkillGroup.Points, BuiltInSkills.SkillsOf(BuiltInSkills.RageGroup).Sum(s => (int)s.MaxLevel));
    }

    [Theory]
    [InlineData(SkillLevel.None, 0)]
    [InlineData(SkillLevel.Basic, 2)]
    [InlineData(SkillLevel.Advanced, 4)]
    [InlineData(SkillLevel.Expert, 6)]
    public void FactionSpellResistance_LowersTheEnemyHeroesSpellPower_ForCasting(SkillLevel level, int penalty)
    {
        var state = Create("A.........B");
        state.GetHero(A).Skills.Set(BuiltInSkills.FactionSpellResistance, level);

        Assert.Equal(penalty, PrimalFury.SpellPowerPenalty(level));
        Assert.Equal(penalty, HeroBattleBonuses.For(state.GetHero(A), state.Creatures).EnemySpellPowerPenalty);
    }

    [Fact]
    public void TheHeroesSkills_FlowIntoTheBattleBonuses()
    {
        var state = Create("A.........B");
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.BloodRage, SkillLevel.Expert);
        hero.Skills.Set(BuiltInSkills.Warcry, SkillLevel.Advanced);

        var bonuses = HeroBattleBonuses.For(hero, state.Creatures);

        Assert.Equal(SkillLevel.Expert, bonuses.BloodRage);
        Assert.Equal(SkillLevel.Advanced, bonuses.Warcry);
    }

    private static (TestBattle Battle, BattleUnit Own, BattleUnit Enemy) Duel(SkillLevel warcry)
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { Warcry = warcry });
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1, initiative: 10), 5, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, initiative: 10), 5, 20, 0);
        return (battle, own, enemy);
    }
}
