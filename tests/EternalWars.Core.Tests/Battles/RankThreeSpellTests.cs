using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>
/// ADR-177: the rank changes ADR-123 left unbuilt: Heal's cure and revival, Purge's healing, Meteor Shower's rubble, Summon Demons staying, Ashfall
/// sparing own demons, Bind Elemental's two kinds, the Familiars' mana theft, Entangle III's damage, Unravel III's Spell Power, Dispel III's refund,
/// Hellfire III, Inferno III's +20 %, Sanctuary III's clear line and Unseen III's first strike.
/// </summary>
public class RankThreeSpellTests
{
    // The table and the grammar ----------------------------------------------------------------

    [Fact]
    public void TheRanks_HaveTheirOwnEffects_Now()
    {
        var spells = SpellCatalog.Default;
        Assert.Equal((false, true, true), (spells.Get("light.heal").EffectAt(1)!.Cures, spells.Get("light.heal").EffectAt(2)!.Cures, spells.Get("light.heal").EffectAt(3)!.Cures));
        Assert.Equal(25, spells.Get("light.heal").EffectAt(3)!.RevivePercent);
        Assert.Equal((0, 20, 20), (spells.Get("light.purge").EffectAt(1)!.Amount, spells.Get("light.purge").EffectAt(2)!.Amount, spells.Get("light.purge").EffectAt(3)!.Amount));
        Assert.True(spells.Get("chaos.meteor-shower").EffectAt(3)!.Rubble);
        Assert.Equal((false, true), (spells.Get("chaos.summon-demons").EffectAt(2)!.Stays, spells.Get("chaos.summon-demons").EffectAt(3)!.Stays));
        Assert.True(spells.Get("chaos.ashfall").EffectAt(3)!.SparesOwnDemons);
        Assert.Equal((1, 2), (spells.Get("arcana.bind-elemental").EffectAt(2)!.Kinds, spells.Get("arcana.bind-elemental").EffectAt(3)!.Kinds));
        Assert.Equal((0, 15), (spells.Get("nature.entangle").EffectAt(2)!.Amount, spells.Get("nature.entangle").EffectAt(3)!.Amount));
        Assert.Equal(1, spells.Get("arcana.unravel").EffectAt(3)!.SapPower);
        Assert.Equal(50, spells.Get("arcana.dispel").EffectAt(3)!.RefundPercent);
        Assert.Equal((SpellReach.All, 50), (spells.Get("chaos.hellfire").EffectAt(3)!.Reach, spells.Get("chaos.hellfire").EffectAt(3)!.OwnPercent));
        Assert.Equal(20, spells.Get("chaos.inferno").EffectAt(3)!.Modifiers.DamageTakenPercent);
        Assert.True(spells.Get("light.sanctuary").EffectAt(3)!.Modifiers.Has(SpellStatus.Screened));
        Assert.Equal(25, spells.Get("shadow.unseen").EffectAt(3)!.Modifiers.AmbushPercent);
        Assert.Equal(1, CreatureCatalog.Default.Get("inferno.familiar").CapabilityValue(CapabilityKind.ManaThief));
    }

    [Fact]
    public void EveryBattleSpell_HasItsOwnRankThreeEffect()
    {
        var battle = SpellCatalog.Default.All.Where(s => s.Effect is not null).ToList();

        Assert.Equal(91, battle.Count);
        Assert.All(battle, s => Assert.NotNull(s.Rank3));
    }

    [Theory]
    [InlineData("heal 40 rubble")]
    [InlineData("damage 70 rubble")]
    [InlineData("damage 70 column 1 rubble")]
    [InlineData("summon inferno.imp 100 kinds 2")]
    [InlineData("summon inferno.imp 100 lasting")]
    [InlineData("dispel own refund% 150")]
    [InlineData("curse entangled rounds 1 hit 0")]
    [InlineData("damage 50 spare-own-demons")]
    [InlineData("damage 50 all own% 120")]
    [InlineData("buff attack 2 rounds 2 cure")]
    public void BadRankThreeWords_AreRefused(string text) => Assert.Throws<FormatException>(() => SpellEffect.Parse(text));

    // Heal and Purge ---------------------------------------------------------------------------

    [Fact]
    public void HealTwo_TakesTheEnemysSpellsOff_AndKeepsTheOwn()
    {
        var battle = Battle(("light.heal", 2));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        own.TopHealth = 1;
        own.MutableSpells.Add(new ActiveSpell("death.weakness", Defender, new SpellModifiers(Attack: -3), 5));
        own.MutableSpells.Add(new ActiveSpell("light.bless", Attacker, new SpellModifiers(Roll: 1), 5));
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "light.heal", own.Id, null));

        Assert.Equal(61, own.TopHealth);
        Assert.Equal(["light.bless"], own.Spells.Select(s => s.Spell));
        Assert.Contains(battle.Events, e => e is BattleSpellDispelled { Spell: "death.weakness", By: "light.heal" });
    }

    [Theory]
    [InlineData(2, 10)]
    [InlineData(3, 12)] // 55 health left over, a quarter of the 10 lost is 2 creatures
    public void HealThree_RevivesUpToAQuarterOfTheDead_ForGood(int rank, int count)
    {
        var battle = Battle(("light.heal", rank));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1, health: 10), 20, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        (own.Count, own.TopHealth) = (10, 5);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "light.heal", own.Id, null));

        Assert.Equal((count, 10, count), (own.Count, own.TopHealth, own.KeptCount));
    }

    [Fact]
    public void PurgeTwo_AlsoHeals_AndMayBeAimedAtAWoundedStackWithNoSpells()
    {
        var battle = Battle(("light.purge", 2));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        own.TopHealth = 1;
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "light.purge", own.Id, null));
        Assert.Equal(21, own.TopHealth);

        var plain = Battle(("light.purge", 1));
        var wounded = plain.Add(Attacker, Sturdy("own"), 1, 0, 0);
        plain.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        wounded.TopHealth = 1;
        plain.StartWith(wounded);
        Assert.NotNull(BattleMagic.CastRefusal(plain.State, Attacker, "light.purge", wounded.Id, null));
    }

    // Meteor Shower, Ashfall, Hellfire, Inferno -------------------------------------------------

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 21)] // the 5 × 5 area less the 4 cells the stack stands on
    public void MeteorShowerThree_LeavesRubbleOnTheAreasFreeCells(int rank, int rubble)
    {
        var battle = Battle(("chaos.meteor-shower", rank));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.meteor-shower", null, new CellPosition(21, 11)));

        var fallen = battle.State.Obstacles.Where(o => o.Class == ObstacleClass.Rubble).ToList();
        Assert.Equal(rubble, fallen.Count);
        Assert.DoesNotContain(fallen, o => o.Rect.Intersects(enemy.Rect));
        if (rubble > 0)
        {
            Assert.Contains(battle.Events, e => e is BattleRubbleFell r && r.Rubble.Count == rubble);
            Assert.False(BattleGrid.IsFree(battle.State, own, new CellPosition(19, 9)));
            Assert.Equal(fallen.Count, fallen.Select(o => o.Id).Distinct().Count());
        }
    }

    [Fact]
    public void AshfallThree_SparesOwnDemons_ButNotTheOwnOthers()
    {
        var battle = Battle(("chaos.ashfall", 3));
        var demons = battle.Add(Attacker, "inferno.horned-demon", 50, 0, 0);
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 10);
        var enemyDemons = battle.Add(Defender, "inferno.horned-demon", 50, 26, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.ashfall", null, null));

        Assert.Equal(50, demons.Count);
        Assert.Equal(1000 - 160, own.TopHealth);
        Assert.Equal(50 - 10, enemyDemons.Count); // 160 damage to 16-health demons
    }

    [Fact]
    public void HellfireThree_HitsTheWholeField_OwnStacksAtHalf()
    {
        var battle = Battle(("chaos.hellfire", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var far = battle.Add(Defender, Sturdy("far"), 1, 26, 0);
        var near = battle.Add(Defender, Sturdy("near"), 1, 10, 18);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.hellfire", null, null));

        Assert.Equal((1000 - 80, 1000 - 160, 1000 - 160), (own.TopHealth, far.TopHealth, near.TopHealth));
    }

    [Fact]
    public void InfernoThree_LeavesItsTargetsTakingMoreDamage()
    {
        var battle = Battle(("chaos.inferno", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.inferno", enemy.Id, null));

        Assert.Equal((20, 15), (enemy.SpellModifiers.DamageTakenPercent, enemy.SpellModifiers.Burn));
    }

    // Summons ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void SummonDemonsThree_StayWithTheWinnersArmy(int rank, bool stay)
    {
        var battle = Battle(("chaos.summon-demons", rank));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.summon-demons", null, null));
        var demons = battle.State.Units.Single(u => u.IsSummoned);

        battle.State.Winner = Attacker;
        var army = new Army();
        BattleOutcome.KeepSurvivors(battle.State, Attacker, army);

        Assert.Equal(stay, demons.Stays);
        Assert.Equal(stay ? demons.Count : 0, army.Stacks.Where(s => s.Stack.Creature == "inferno.horned-demon").Sum(s => s.Stack.Count));

        battle.State.Winner = Defender; // a lost battle keeps none
        var lost = new Army();
        BattleOutcome.KeepSurvivors(battle.State, Attacker, lost);
        Assert.Empty(lost.Stacks);
    }

    [Fact]
    public void BindElementalThree_CallsTwoKindsAtOnce()
    {
        var battle = Battle(("arcana.bind-elemental", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.bind-elemental", null, null));

        var called = battle.State.Units.Where(u => u.IsSummoned).Select(u => u.Creature.Id).ToList();
        Assert.Equal(["conflux.air-elemental", "conflux.earth-elemental"], called);
        Assert.Equal(2, battle.Events.OfType<BattleUnitSummoned>().Count());
    }

    [Fact]
    public void Familiars_StealTheEnemyHerosMana_WithEachAttack()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(0, 10), defenderCaster: Caster(0, 20));
        var familiars = battle.Add(Attacker, "inferno.familiar", 5, 10, 10);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 12, 10);
        battle.StartWith(familiars);

        Assert.Null(battle.Engine.Attack(enemy.Id, null));

        Assert.Equal((11, 19), (battle.State.ManaOf(Attacker), battle.State.ManaOf(Defender)));
        Assert.Contains(battle.Events, e => e is BattleManaStolen { From: Defender, Stolen: 1, FromMana: 19 });
    }

    // Entangle, Unravel, Dispel ----------------------------------------------------------------

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 15)]
    public void EntangleThree_AlsoHurtsTheStacksItHolds(int rank, int damage)
    {
        var battle = Battle(("nature.entangle", rank));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemies = new[] { battle.Add(Defender, Sturdy("a"), 1, 26, 0), battle.Add(Defender, Sturdy("b"), 1, 26, 3), battle.Add(Defender, Sturdy("c"), 1, 26, 6) };
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "nature.entangle", enemies[0].Id, null));

        Assert.All(enemies.Take(rank), e => Assert.True(e.Has(SpellStatus.Entangled)));
        Assert.All(enemies.Take(rank), e => Assert.Equal(1000 - damage, e.TopHealth));
    }

    [Fact]
    public void UnravelThree_TakesSpellPowerFromTheEnemyHero_ForTheBattle()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(0, 100, ("arcana.unravel", 3)), defenderCaster: Caster(5, 100));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        enemy.MutableSpells.Add(new ActiveSpell("light.bless", Defender, new SpellModifiers(Roll: 1), 5));
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.unravel", null, null));

        Assert.Empty(enemy.Spells);
        Assert.Equal(4, BattleMagic.SpellPower(battle.State, Defender));
        Assert.Contains(battle.Events, e => e is BattleSpellPowerSapped { Side: Defender, Lost: 1 });
    }

    [Theory]
    [InlineData(false, 100 - 25 + 12)]
    [InlineData(true, 100 - 25)]
    public void DispelThree_GivesHalfTheManaBack_WhenOnlyTheEnemysSpellsCameOff(bool ownBuff, int mana)
    {
        var battle = Battle(("arcana.dispel", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        enemy.MutableSpells.Add(new ActiveSpell("light.bless", Defender, new SpellModifiers(Roll: 1), 5));
        if (ownBuff)
        {
            own.MutableSpells.Add(new ActiveSpell("light.bless", Attacker, new SpellModifiers(Roll: 1), 5));
        }

        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.dispel", null, null));

        Assert.Equal(mana, battle.State.ManaOf(Attacker));
    }

    // Sanctuary and Unseen ---------------------------------------------------------------------

    [Theory]
    [InlineData(2, true, true)]
    [InlineData(3, true, false)]
    [InlineData(3, false, true)]
    public void SanctuaryThree_LetsShootersFireOnlyAlongAClearLine(int rank, bool blocked, bool canShoot)
    {
        var battle = Battle(("light.sanctuary", rank));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 10);
        var blocker = battle.Add(Attacker, Sturdy("blocker"), 1, 12, blocked ? 10 : 0);
        var archers = battle.Add(Defender, TestBattle.Fixed("archers", 5, shots: 10), 5, 26, 10);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "light.sanctuary", own.Id, null));

        Assert.Equal(canShoot, BattleGrid.CanShoot(battle.State, archers, own, out _));
        Assert.Equal(blocked, blocker.Rect.Y == own.Rect.Y);
    }

    [Theory]
    [InlineData(2, 100)]
    [InlineData(3, 125)]
    public void UnseenThree_StrikesHarderOutOfHiding(int rank, int damage)
    {
        var battle = Battle(("shadow.unseen", rank));
        var hidden = battle.Add(Attacker, TestBattle.Fixed("hidden", 10), 10, 10, 10);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 12, 10);
        battle.StartWith(hidden);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.unseen", hidden.Id, null));
        Assert.Null(battle.Engine.Attack(enemy.Id, null));

        Assert.Equal(1000 - damage, enemy.TopHealth);
    }

    private static CreatureDefinition Sturdy(string id) => TestBattle.Fixed(id, 10, health: 1000);

    private static TestBattle Battle(params (string Spell, int Rank)[] spells) => TestBattle.Create(attackerCaster: Caster(0, 100, spells));

    private static BattleCaster Caster(int spellPower, int mana, params (string Spell, int Rank)[] spells) => new(
        spellPower, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s.Spell), s.Rank, SpellCatalog.Default.Get(s.Spell).Mana)).ToList(), null, mana);
}
