using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>
/// ADR-123: control and revival spells (Blind, Confusion, Resurrection), the new effect words (dispels, burning, chains, executions, wards,
/// kills), elements, the table's tags and creature caster spells, and the battle AI casting buffs and curses.
/// </summary>
public class ControlSpellTests
{
    // Blind and Confusion ------------------------------------------------------------------------

    [Fact]
    public void Blind_TakesTheStacksTurns()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("shadow.blind", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 10, health: 1000), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 10, health: 1000), 1, 20, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.blind", enemy.Id, null));
        Assert.Null(battle.Engine.Defend());

        Assert.Contains(battle.Events, e => e is BattleUnitSpellbound { Spell: "shadow.blind" } s && s.Unit == enemy.Id);
        Assert.Same(own, battle.State.ActiveUnit); // the enemy's turn passed, a new round began
    }

    [Fact]
    public void ABlindStack_RetaliatesAtHalf_AndWakesWhenHit()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("shadow.blind", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 10, health: 1000), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 10, health: 1000), 1, 2, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "shadow.blind", enemy.Id, null));

        Assert.Null(battle.Engine.Attack(enemy.Id, own.Position));

        Assert.Equal(995, own.TopHealth); // 10 at half
        Assert.Empty(enemy.Spells);
        Assert.Contains(battle.Events, e => e is BattleSpellExpired { Spell: "shadow.blind" });
    }

    [Fact]
    public void AConfusedStack_ActsByItself_AtRandom()
    {
        var battle = TestBattle.Create();
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 10, health: 1000), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("enemy", 10, health: 1000), 1, 2, 0);
        battle.StartWith(own);
        own.MutableSpells.Add(new ActiveSpell("shadow.confusion", Defender, SpellEffect.Parse("curse confused rounds 1").Modifiers, 2));

        BattleAi.ActConfused(battle.State, battle.Engine);

        Assert.NotSame(own, battle.State.ActiveUnit);
        Assert.Contains(battle.Events, e => e is BattleUnitAttacked a && a.Attacker == own.Id || e is BattleUnitMoved m && m.Unit == own.Id);
    }

    [Fact]
    public void Terror_DoesNotTakeHoldOnAFearlessStack()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("chaos.terror", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 10), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 10), 1, 20, 0);
        battle.StartWith(own);
        enemy.MutableSpells.Add(new ActiveSpell("light.steadfast", Defender, SpellEffect.Parse("buff fearless rounds 3").Modifiers, 4));

        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "chaos.terror", enemy.Id, null));
    }

    [Fact]
    public void Stasis_KeepsTheStackFromAttacksAndSpells()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("arcana.magic-arrow", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 10), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 10), 1, 2, 0);
        battle.StartWith(own);
        enemy.MutableSpells.Add(new ActiveSpell("arcana.stasis", Attacker, SpellEffect.Parse("curse stasis rounds 2").Modifiers, 3));

        Assert.Empty(BattleGrid.AttackPositions(battle.State, own, enemy));
        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "arcana.magic-arrow", enemy.Id, null));
    }

    // Revival ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 0)]
    public void Resurrection_RevivesTheDead_ForTheBattleAtRankOne_AndForGoodAtRankTwo(int rank, int revivedForTheBattle)
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("light.resurrection", rank)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1, health: 10), 10, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 20, 0);
        battle.StartWith(own);
        own.Count = 4;

        Assert.Null(battle.Engine.Cast(Attacker, "light.resurrection", own.Id, null));

        Assert.Equal((10, revivedForTheBattle, 10 - revivedForTheBattle), (own.Count, own.Revived, own.KeptCount));
        Assert.Contains(battle.Events, e => e is BattleUnitRevived { Health: 60, Count: 10 });
    }

    [Fact]
    public void Resurrection_RankThree_BringsBackAStackDestroyedInTheBattle()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("light.resurrection", 3)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var fallen = battle.Add(Attacker, TestBattle.Fixed("fallen", 1, health: 10), 20, 0, 10);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 20, 0);
        battle.StartWith(own);
        fallen.Count = 0;
        fallen.TopHealth = 0;

        Assert.NotNull(BattleMagic.CastRefusal(TestBattle.Create(attackerCaster: Caster(("light.resurrection", 1))).State, Attacker, "light.resurrection", fallen.Id, null));
        Assert.Null(battle.Engine.Cast(Attacker, "light.resurrection", fallen.Id, null));

        Assert.Equal((15, 10), (fallen.Count, fallen.TopHealth));
    }

    [Fact]
    public void CreaturesRevivedForTheBattle_DieFirst()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("light.resurrection", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1, health: 10), 10, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 30, health: 1000), 1, 2, 0);
        battle.StartWith(own);
        own.Count = 4;
        Assert.Null(battle.Engine.Cast(Attacker, "light.resurrection", own.Id, null));
        Assert.Null(battle.Engine.Wait());

        Assert.Null(battle.Engine.Attack(own.Id, enemy.Position)); // the enemy hits for 30: 3 creatures

        Assert.Equal((7, 3, 4), (own.Count, own.Revived, own.KeptCount));
    }

    // The new effect words -----------------------------------------------------------------------

    [Fact]
    public void Purge_TakesTheEnemysSpellsOff_AndKeepsTheOwn()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("light.purge", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 20, 0);
        battle.StartWith(own);
        own.MutableSpells.Add(new ActiveSpell("death.weakness", Defender, new SpellModifiers(Attack: -3), 4));
        own.MutableSpells.Add(new ActiveSpell("light.bless", Attacker, new SpellModifiers(Roll: 1), 4));

        Assert.Null(battle.Engine.Cast(Attacker, "light.purge", own.Id, null));

        Assert.Equal("light.bless", Assert.Single(own.Spells).Spell);
        Assert.Contains(battle.Events, e => e is BattleSpellDispelled { Spell: "death.weakness", By: "light.purge" });
    }

    [Fact]
    public void Inferno_BurnsAtTheStartOfEachRound()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("chaos.inferno", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000), 1, 20, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.inferno", enemy.Id, null));
        Assert.Equal(900, enemy.TopHealth);
        Assert.Null(battle.Engine.Defend());
        Assert.Null(battle.Engine.Defend()); // the enemy's turn; round 2 starts

        Assert.Equal(885, enemy.TopHealth);
    }

    [Fact]
    public void ChainLightning_JumpsToTheNearestStacks_AtHalfEachTime()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("nature.chain-lightning", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1, health: 1000), 1, 0, 0);
        var a = battle.Add(Defender, TestBattle.Fixed("a", 1, health: 1000), 1, 20, 5);
        var b = battle.Add(Defender, TestBattle.Fixed("b", 1, health: 1000), 1, 22, 5);
        var c = battle.Add(Defender, TestBattle.Fixed("c", 1, health: 1000), 1, 26, 5);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "nature.chain-lightning", a.Id, null));

        Assert.Equal((930, 965, 983, 1000), (a.TopHealth, b.TopHealth, c.TopHealth, own.TopHealth));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 2)]
    public void Harvest_DestroysAStackBelowAQuarter_ElseDealsItsDamage(int count, int left)
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("chaos.harvest", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("other", 1), 1, 25, 20);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 100), 10, 20, 0);
        battle.StartWith(own);
        enemy.Count = count;

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.harvest", enemy.Id, null));

        Assert.Equal(left, enemy.Count);
    }

    [Fact]
    public void FinalReckoning_KillsATenthOfEveryLivingStack_AtLeastOne()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("death.final-reckoning", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 5, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1), 20, 20, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "death.final-reckoning", null, null));

        Assert.Equal((4, 18), (own.Count, enemy.Count));
    }

    [Fact]
    public void AntiMagic_StopsEverySpellUpToItsLevel_EvenTheOwn()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("light.bless", 1), ("nature.barkskin", 1)));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 20, 0);
        battle.StartWith(own);
        own.MutableSpells.Add(new ActiveSpell("arcana.anti-magic", Attacker, SpellEffect.Parse("buff immune-level 3 rounds 3").Modifiers, 4));

        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "light.bless", own.Id, null));
        Assert.Null(BattleMagic.CastRefusal(battle.State, Attacker, "nature.barkskin", own.Id, null)); // level 4
    }

    [Theory]
    [InlineData("curse blind rounds 2", SpellStatus.Blind)]
    [InlineData("buff sanctuary vigil rounds 0", SpellStatus.Sanctuary | SpellStatus.Vigil)]
    [InlineData("curse morale -2 no-retaliation rounds 2", SpellStatus.NoRetaliation)]
    public void StatusWords_Parse(string text, SpellStatus status) => Assert.Equal(status, SpellEffect.Parse(text).Modifiers.Status);

    [Theory]
    [InlineData("heal 40 splash 50")]
    [InlineData("buff attack 1 rounds 3 lasting")]
    [InlineData("damage 40 ring")]
    [InlineData("dispel some")]
    [InlineData("revive 0")]
    public void BadEffects_AreRefused(string text) => Assert.Throws<FormatException>(() => SpellEffect.Parse(text));

    // Elements ---------------------------------------------------------------------------------

    [Fact]
    public void MagicArrow_TakesTheBestOrb_AndBindElemental_TheBoundKindsElement()
    {
        var caster = new BattleCaster(0, 1, [], null, 0, new Dictionary<SpellElement, int> { [SpellElement.Fire] = 25 });

        Assert.Equal(25, caster.ElementBonus(SpellCatalog.Default.Get("arcana.magic-arrow")));
        var bind = SpellCatalog.Default.Get("arcana.bind-elemental");
        Assert.Equal((25, 0, 0), (caster.ElementBonus(bind, "conflux.fire-elemental"), caster.ElementBonus(bind, "conflux.water-elemental"), caster.ElementBonus(bind)));
    }

    // The table ---------------------------------------------------------------------------------

    [Fact]
    public void EveryCreatureCasterSpell_IsASpellThatWorksInBattle_OrTheRebirth()
    {
        foreach (var creature in CreatureCatalog.Default.All)
        {
            foreach (var cast in creature.SpellCasts.Keys)
            {
                if (cast == BattleMagic.Rebirth)
                {
                    continue;
                }

                Assert.True(SpellCatalog.Default.TryGet(cast, out var spell), $"{creature.Id}: {cast}");
                Assert.NotNull(spell.Effect);
            }
        }
    }

    [Fact]
    public void SummonsAreTaggedSummon_AndHolySpellsHurtTheUndeadTwice()
    {
        Assert.All(SpellCatalog.Default.All.Where(s => s.Effect is not null), s => Assert.Equal(s.Effect!.Kind == SpellEffectKind.Summon, s.HasTag(SpellCatalog.SummonTag)));
        Assert.Throws<FormatException>(() => new SpellCatalog([SpellCatalog.Default.Get("chaos.hellgate") with { Tags = [] }]));
        Assert.True(SpellCatalog.Default.Get("light.smite").HasTag(BattleMagic.Holy));
    }

    // The AI -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("light.bless", true)]
    [InlineData("shadow.blind", false)]
    public void TheBattleAi_CastsBuffsOnOwnStacks_AndCursesOnEnemyStacks(string spell, bool onOwn)
    {
        var battle = TestBattle.Create(attackerCaster: Caster((spell, 1)));
        var own = battle.Add(Attacker, BuiltInCreatures.Footman, 50, 0, 0);
        var enemy = battle.Add(Defender, BuiltInCreatures.Footman, 50, 25, 0);
        battle.StartWith(own);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.Contains(battle.Events, e => e is BattleSpellCast c && c.Spell == spell && c.Target == (onOwn ? own : enemy).Id);
    }

    private static BattleCaster Caster(params (string Spell, int Rank)[] spells) =>
        new(0, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s.Spell), s.Rank, SpellCatalog.Default.Get(s.Spell).Mana)).ToList(), null, 100);
}
