using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>ADR-150: the spells that change whom a stack fights (Frenzy, Hypnotize, Puppet Master), spells that last turns, and the battle's end.</summary>
public class ControlChangeTests
{
    // The table and the grammar ----------------------------------------------------------------

    [Theory]
    [InlineData("chaos.frenzy", SpellStatus.Frenzied)]
    [InlineData("shadow.hypnotize", SpellStatus.Controlled)]
    [InlineData("shadow.puppet-master", SpellStatus.Controlled)]
    public void TheThreeSpells_CanBeCastInBattle_AtEveryRank(string id, SpellStatus status)
    {
        var spell = SpellCatalog.Default.Get(id);
        Assert.All(new[] { 1, 2, 3 }, rank => Assert.True(spell.EffectAt(rank)!.Modifiers.Has(status)));
    }

    [Fact]
    public void TheRanks_ChangeTurnsHealthAndDamageTaken()
    {
        var frenzy = SpellCatalog.Default.Get("chaos.frenzy");
        var hypnotize = SpellCatalog.Default.Get("shadow.hypnotize");

        Assert.Equal((1, 2, 2), (frenzy.EffectAt(1)!.Turns, frenzy.EffectAt(2)!.Turns, frenzy.EffectAt(3)!.Turns));
        Assert.Equal(20, frenzy.EffectAt(3)!.Modifiers.DamageTakenPercent);
        Assert.Equal((300, 450, 450), (hypnotize.EffectAt(1)!.HealthLimit, hypnotize.EffectAt(2)!.HealthLimit, hypnotize.EffectAt(3)!.HealthLimit));
        Assert.Equal(2, hypnotize.EffectAt(3)!.Turns);
        Assert.Equal((2, 3), (SpellCatalog.Default.Get("shadow.puppet-master").EffectAt(1)!.Rounds, SpellCatalog.Default.Get("shadow.puppet-master").EffectAt(2)!.Rounds));
    }

    [Theory]
    [InlineData("curse frenzied turns 0")]
    [InlineData("curse confused turns 1 rounds 2")]
    [InlineData("heal 20 turns 1")]
    [InlineData("buff attack 2 health 300 rounds 2")]
    [InlineData("curse controlled rounds 2 targets 2")]
    [InlineData("buff controlled rounds 2")]
    [InlineData("curse frenzied rounds 1 enemies")]
    public void TheGrammar_RefusesWhatDoesNotFit(string text) => Assert.Throws<FormatException>(() => SpellEffect.Parse(text));

    // Frenzy -----------------------------------------------------------------------------------

    [Fact]
    public void AFrenziedStack_AttacksTheNearestStack_EvenAFriend_ForItsNextTurn()
    {
        var battle = Battle(("chaos.frenzy", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 0);
        var friend = battle.Add(Defender, Sturdy("friend"), 1, 22, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.frenzy", enemy.Id, null));
        Assert.Contains(battle.Events, e => e is BattleSpellApplied { Spell: "chaos.frenzy", Turns: 1 });

        battle.State.ActiveUnit = enemy;
        BattleAi.ActFrenzied(battle.State, battle.Engine);

        Assert.Contains(battle.Events, e => e is BattleUnitAttacked { Kind: BattleAttackKind.Melee } a && a.Attacker == enemy.Id && a.Target == friend.Id);
        Assert.Contains(battle.Events, e => e is BattleUnitAttacked { Kind: BattleAttackKind.Retaliation } a && a.Attacker == friend.Id);
        Assert.Empty(enemy.Spells); // its one turn is used
        Assert.Contains(battle.Events, e => e is BattleSpellExpired { Spell: "chaos.frenzy" } x && x.Unit == enemy.Id);
    }

    [Fact]
    public void AFrenziedStack_WalksTowardsTheNearestStack_WhenItCannotReachIt()
    {
        var battle = Battle(("chaos.frenzy", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("slow", 10, health: 1000, speed: 2), 1, 20, 20);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.frenzy", enemy.Id, null));

        battle.State.ActiveUnit = enemy;
        BattleAi.ActFrenzied(battle.State, battle.Engine);

        var moved = Assert.Single(battle.Events.OfType<BattleUnitMoved>());
        Assert.True(enemy.Rect.GapTo(own.Rect) < new CellRect(20, 20, 2, 2).GapTo(own.Rect));
        Assert.Equal(enemy.Id, moved.Unit);
    }

    [Fact]
    public void Frenzy_RankTwo_LastsTwoTurns_AndATurnPassedCounts_RankThreeAlsoTakesMoreDamage()
    {
        var battle = Battle(("chaos.frenzy", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.frenzy", enemy.Id, null));
        Assert.Equal(20, enemy.SpellModifiers.DamageTakenPercent);

        enemy.LosesNextAction = true; // its next turn passes
        Assert.Null(battle.Engine.Defend());
        Assert.Equal(1, Assert.Single(enemy.Spells).TurnsLeft);
    }

    // Hypnotize --------------------------------------------------------------------------------

    [Fact]
    public void Hypnotize_BringsAStackOverForItsNextTurn_ThenGivesItBack()
    {
        var battle = Battle(("shadow.hypnotize", 1));
        var own = battle.Add(Attacker, Sturdy("own", 100), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy", 100), 3, 20, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.hypnotize", enemy.Id, null));
        Assert.Equal((Attacker, Defender), (enemy.Side, enemy.Owner));
        Assert.Contains(battle.Events, e => e is BattleUnitSideChanged { Side: Attacker } c && c.Unit == enemy.Id);
        Assert.False(BattleGrid.IsFoe(own, enemy)); // no longer a foe of the caster's stacks

        battle.State.ActiveUnit = enemy;
        Assert.Null(battle.Engine.Defend());

        Assert.Equal(Defender, enemy.Side);
        Assert.Contains(battle.Events, e => e is BattleUnitSideChanged { Side: Defender } c && c.Unit == enemy.Id);
    }

    [Fact]
    public void Hypnotize_TakesOnlyAStackOfUpToItsHealth_WhichGrowsWithSpellPower()
    {
        var weak = Battle(("shadow.hypnotize", 1));
        var own = weak.Add(Attacker, Sturdy("own", 100), 1, 0, 0);
        var big = weak.Add(Defender, Sturdy("enemy", 100), 4, 20, 0);
        weak.StartWith(own);
        Assert.Contains("too strong", BattleMagic.CastRefusal(weak.State, Attacker, "shadow.hypnotize", big.Id, null));

        var strong = Battle(10, ("shadow.hypnotize", 1));
        own = strong.Add(Attacker, Sturdy("own", 100), 1, 0, 0);
        big = strong.Add(Defender, Sturdy("enemy", 100), 6, 20, 0);
        strong.StartWith(own);
        Assert.Null(BattleMagic.CastRefusal(strong.State, Attacker, "shadow.hypnotize", big.Id, null)); // 600 at Spell Power 10
    }

    [Fact]
    public void AHypnotizedStack_FightsItsOwnSide_NotTheCasters()
    {
        var battle = Battle(("shadow.hypnotize", 1));
        var own = battle.Add(Attacker, Sturdy("own", 100), 1, 18, 2);
        var enemy = battle.Add(Defender, Sturdy("enemy", 100), 1, 20, 0);
        var friend = battle.Add(Defender, Sturdy("friend", 100), 1, 22, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "shadow.hypnotize", enemy.Id, null));

        battle.State.ActiveUnit = enemy;
        Assert.NotNull(battle.Engine.Attack(own.Id, null));
        Assert.Null(battle.Engine.Attack(friend.Id, null));

        Assert.Contains(battle.Events, e => e is BattleUnitAttacked { Kind: BattleAttackKind.Melee } a && a.Attacker == enemy.Id && a.Target == friend.Id);
    }

    // Puppet Master ----------------------------------------------------------------------------

    [Fact]
    public void PuppetMaster_LastsItsRounds_AndTheBattleGoesOnWhileTheOwnersLastStackIsTaken()
    {
        var battle = Battle(("shadow.puppet-master", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.puppet-master", enemy.Id, null));
        Assert.Equal(3, Assert.Single(enemy.Spells).UntilRound);
        Assert.Equal(Attacker, enemy.Side);

        for (var round = 0; round < 2; round++)
        {
            EndRound(battle);
            Assert.Equal(BattlePhase.Fighting, battle.State.Phase);
            Assert.Equal(Attacker, enemy.Side);
        }

        EndRound(battle);
        Assert.Equal(4, battle.State.Round);
        Assert.Equal(Defender, enemy.Side);
        Assert.Contains(battle.Events, e => e is BattleUnitSideChanged { Side: Defender });
    }

    [Fact]
    public void ADispel_FreesAStackTheEnemyControls()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(0, ("shadow.puppet-master", 1)), defenderCaster: Caster(0, ("arcana.dispel", 1)));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "shadow.puppet-master", enemy.Id, null));

        Assert.Null(battle.Engine.Cast(Defender, "arcana.dispel", enemy.Id, null));

        Assert.Equal(Defender, enemy.Side);
        Assert.Contains(battle.Events, e => e is BattleUnitSideChanged { Side: Defender });
    }

    [Fact]
    public void MindImmuneStacks_AndWarMachines_CannotBeTaken()
    {
        var battle = Battle(("shadow.puppet-master", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var undead = battle.Add(Defender, Sturdy("ghost") with { Capabilities = [new CreatureCapability(CapabilityKind.SpellImmunity, 0, "mind")] }, 1, 20, 0);
        battle.StartWith(own);

        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "shadow.puppet-master", undead.Id, null));
    }

    [Fact]
    public void AStackTheEnemyControlled_DiesAsItsOwners()
    {
        var battle = Battle(("shadow.puppet-master", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("frail", 10, health: 5), 1, 20, 0);
        battle.Add(Defender, Sturdy("other"), 1, 22, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "shadow.puppet-master", enemy.Id, null));

        battle.State.ActiveUnit = battle.State.Units[2];
        Assert.Null(battle.Engine.Attack(enemy.Id, null));

        Assert.False(enemy.IsAlive);
        Assert.Equal(Defender, enemy.Side);
    }

    // The AI -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("chaos.frenzy")]
    [InlineData("shadow.hypnotize")]
    [InlineData("shadow.puppet-master")]
    public void TheBattleAi_CastsTheControlSpells_OnAnEnemyStack(string spell)
    {
        var battle = TestBattle.Create(attackerCaster: Caster(0, (spell, 1)));
        var own = battle.Add(Attacker, BuiltInCreatures.Footman, 10, 0, 0);
        var enemy = battle.Add(Defender, BuiltInCreatures.Footman, 10, 25, 0);
        battle.StartWith(own);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.Contains(battle.Events, e => e is BattleSpellCast c && c.Spell == spell && c.Target == enemy.Id);
    }

    private static CreatureDefinition Sturdy(string id, int health = 1000) => TestBattle.Fixed(id, 10, health: health);

    private static TestBattle Battle(params (string Spell, int Rank)[] spells) => Battle(0, spells);

    private static TestBattle Battle(int spellPower, params (string Spell, int Rank)[] spells) => TestBattle.Create(attackerCaster: Caster(spellPower, spells));

    private static BattleCaster Caster(int spellPower, params (string Spell, int Rank)[] spells) => new(
        spellPower, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s.Spell), s.Rank, SpellCatalog.Default.Get(s.Spell).Mana)).ToList(), null, 100);

    /// <summary>Every stack still to act this round defends, so the next round starts.</summary>
    private static void EndRound(TestBattle battle)
    {
        var round = battle.State.Round;
        while (battle.State.Round == round && battle.State.ActiveUnit is not null)
        {
            Assert.Null(battle.Engine.Defend());
        }
    }
}
