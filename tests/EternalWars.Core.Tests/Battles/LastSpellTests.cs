using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>ADR-152: the last battle spells: Martyrdom, Mirror Image, Clockwork Precision, Raise Dead, Blood Price, and Puppet Master III's split.</summary>
public class LastSpellTests
{
    // The table and the grammar ----------------------------------------------------------------

    [Fact]
    public void EveryBattleSpell_NowHasAnEffect_AtEveryRank()
    {
        var battle = SpellCatalog.Default.All.Where(s => !s.IsAdventure).ToList();

        Assert.Equal(91, battle.Count);
        Assert.All(battle, s => Assert.All(new[] { 1, 2, 3 }, r => Assert.NotNull(s.EffectAt(r))));
    }

    [Fact]
    public void TheLastSpells_RankByRank()
    {
        var martyrdom = SpellCatalog.Default.Get("light.martyrdom");
        Assert.Equal((50, 100, 100), (martyrdom.EffectAt(1)!.Modifiers.MartyrPercent, martyrdom.EffectAt(2)!.Modifiers.MartyrPercent, martyrdom.EffectAt(3)!.Modifiers.MartyrPercent));
        Assert.Equal((0, 2), (martyrdom.EffectAt(2)!.MartyrDefense, martyrdom.EffectAt(3)!.MartyrDefense));
        Assert.True(martyrdom.EffectAt(1)!.TwoClicks);

        var mirror = SpellCatalog.Default.Get("arcana.mirror-image");
        Assert.Equal((SpellEffectKind.Copy, 2, 3), (mirror.EffectAt(1)!.Kind, mirror.EffectAt(1)!.Rounds!.Value, mirror.EffectAt(2)!.Rounds!.Value));
        Assert.Equal((1, 2), (mirror.EffectAt(2)!.CopyHits, mirror.EffectAt(3)!.CopyHits));

        var clockwork = SpellCatalog.Default.Get("arcana.clockwork");
        Assert.Equal((SpellFilter.Machine, SpellReach.Side), (clockwork.EffectAt(1)!.Filter, clockwork.EffectAt(1)!.Reach));
        Assert.True(clockwork.EffectAt(1)!.Modifiers.Has(SpellStatus.Twice));
        Assert.True(clockwork.EffectAt(3)!.Modifiers.Has(SpellStatus.TrueShot));

        var raise = SpellCatalog.Default.Get("death.raise-dead");
        Assert.Equal((SpellRises.None, SpellRises.Once, SpellRises.EachRound), (raise.EffectAt(1)!.Rises, raise.EffectAt(2)!.Rises, raise.EffectAt(3)!.Rises));

        var price = SpellCatalog.Default.Get("chaos.blood-price");
        Assert.Equal((5, 4, 3), (price.EffectAt(1)!.HealthPerMana, price.EffectAt(2)!.HealthPerMana, price.EffectAt(3)!.HealthPerMana));

        var puppet = SpellCatalog.Default.Get("shadow.puppet-master");
        Assert.Equal((false, true, 600), (puppet.EffectAt(2)!.Split, puppet.EffectAt(3)!.Split, puppet.EffectAt(3)!.HealthLimit));
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("copy rounds 0")]
    [InlineData("copy rounds 2 targets 2")]
    [InlineData("copy rounds 2 attack 1")]
    [InlineData("damage 30 survives 1")]
    [InlineData("raise 0")]
    [InlineData("raise 15 twice")]
    [InlineData("sacrifice 10")]
    [InlineData("sacrifice 10 per 0")]
    [InlineData("sacrifice 0 per 5")]
    [InlineData("curse attack -1 martyr% 50 rounds 2")]
    [InlineData("buff martyr% 50 rounds 3 allies")]
    [InlineData("buff martyr% 150 rounds 3")]
    [InlineData("buff attack 1 martyr-defense 2 rounds 3")]
    [InlineData("curse controlled rounds 2 split")]
    public void TheGrammar_RefusesWhatDoesNotFit(string text) => Assert.Throws<FormatException>(() => SpellEffect.Parse(text));

    // Martyrdom --------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 950, 950)]
    [InlineData(2, 1000, 900)]
    public void Martyrdom_TheMartyrTakesItsShareOfABlow(int rank, int guardedHealth, int martyrHealth)
    {
        var battle = Battle(("light.martyrdom", rank));
        var guarded = battle.Add(Attacker, Sturdy("guarded"), 1, 0, 0);
        var martyr = battle.Add(Attacker, Sturdy("martyr"), 1, 0, 10);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 10), 10, 1, 0);
        battle.StartWith(guarded);

        Assert.Null(battle.Engine.Cast(Attacker, "light.martyrdom", guarded.Id, null, martyr.Id));
        battle.State.ActiveUnit = enemy;
        Assert.Null(battle.Engine.Attack(guarded.Id, null));

        Assert.Equal((guardedHealth, martyrHealth), (guarded.TotalHealth, martyr.TotalHealth));
        Assert.Contains(battle.Events, e => e is BattleUnitShielded s && s.Martyr == martyr.Id && s.Unit == guarded.Id && s.Damage == 1000 - martyrHealth);
    }

    [Fact]
    public void Martyrdom_RankThree_StrengthensTheMartyr_AndNeedsAnotherOwnStack()
    {
        var battle = Battle(("light.martyrdom", 3));
        var guarded = battle.Add(Attacker, Sturdy("guarded"), 1, 0, 0);
        var martyr = battle.Add(Attacker, Sturdy("martyr"), 1, 0, 10);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(guarded);

        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "light.martyrdom", guarded.Id, null));
        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "light.martyrdom", guarded.Id, null, guarded.Id));
        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "light.martyrdom", guarded.Id, null, enemy.Id));
        Assert.Null(battle.Engine.Cast(Attacker, "light.martyrdom", guarded.Id, null, martyr.Id));

        Assert.Equal(2, martyr.SpellModifiers.Defense);
        Assert.Equal(0, guarded.SpellModifiers.Defense);
    }

    // Mirror Image -----------------------------------------------------------------------------

    [Fact]
    public void MirrorImage_CopiesAnOwnStack_WhichVanishesWhenHit()
    {
        var battle = Battle(("arcana.mirror-image", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 5, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 26, 0);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.mirror-image", own.Id, null));

        var copy = battle.State.Units.Single(u => u.IsCopy);
        Assert.Equal((Attacker, own.Creature, 5, true), (copy.Side, copy.Creature, copy.Count, copy.IsSummoned));
        Assert.True(copy.Rect.IsAdjacentTo(own.Rect));
        Assert.DoesNotContain(copy, battle.State.OwnedArmyOf(Attacker));
        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "arcana.mirror-image", copy.Id, null)); // no copy of a copy

        enemy.Position = new CellPosition(copy.Rect.Right, copy.Position.Y);
        battle.State.ActiveUnit = enemy;
        Assert.Null(battle.Engine.Attack(copy.Id, null));

        Assert.False(copy.IsAlive);
        Assert.Contains(battle.Events, e => e is BattleCopyVanished v && v.Unit == copy.Id);
    }

    [Fact]
    public void MirrorImage_RankThree_SurvivesOneHit_AndCopiesVanishWhenTheirRoundsEnd()
    {
        var battle = Battle(("arcana.mirror-image", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 5, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "arcana.mirror-image", own.Id, null));
        var copy = battle.State.Units.Single(u => u.IsCopy);

        Assert.Null(battle.Engine.Defend());
        while (battle.State.Round <= copy.CopyUntilRound && copy.IsAlive)
        {
            Assert.Null(battle.Engine.Defend());
        }

        Assert.False(copy.IsAlive);
        Assert.Equal(copy.CopyUntilRound + 1, battle.State.Round);
        Assert.Contains(battle.Events, e => e is BattleCopyVanished v && v.Unit == copy.Id);

        var other = Battle(("arcana.mirror-image", 3));
        own = other.Add(Attacker, Sturdy("own"), 5, 0, 0);
        other.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 26, 0);
        other.StartWith(own);
        Assert.Null(other.Engine.Cast(Attacker, "arcana.mirror-image", own.Id, null));
        copy = other.State.Units.Single(u => u.IsCopy);
        var shooter = other.State.Units.Single(u => u.Side == Defender);
        shooter.Position = new CellPosition(copy.Rect.Right, copy.Position.Y);
        other.State.ActiveUnit = shooter;
        Assert.Null(other.Engine.Attack(copy.Id, null));
        Assert.True(copy.IsAlive);
        Assert.Equal(1, copy.CopyHits);
        _ = enemy;
    }

    // Clockwork Precision ----------------------------------------------------------------------

    [Fact]
    public void ClockworkPrecision_OwnMachinesHitHarder_AndActTwice()
    {
        var battle = Battle(("arcana.clockwork", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var ballista = new BattleUnit(9, Attacker, WarMachines.Definition(WarMachineKind.Ballista), 1, -1, new CellPosition(0, 10), WarMachineKind.Ballista);
        battle.State.MutableUnits.Add(ballista);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        battle.StartWith(own);
        var before = DamageCalculator.Expected(battle.State, ballista, enemy, isRanged: true);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.clockwork", null, null));

        Assert.Contains(ballista.Spells, s => s.Spell == "arcana.clockwork");
        Assert.DoesNotContain(own.Spells, s => s.Spell == "arcana.clockwork");
        Assert.InRange(DamageCalculator.Expected(battle.State, ballista, enemy, isRanged: true), (before * 3 / 2) - 1, (before * 3 / 2) + 1);

        battle.State.MutableQueue.Remove(ballista.Id); // its turn now, not later in the round
        battle.State.ActiveUnit = ballista;
        Assert.Null(battle.Engine.Shoot(enemy.Id));
        Assert.Same(ballista, battle.State.ActiveUnit);
        Assert.Contains(battle.Events, e => e is BattleUnitActsAgain a && a.Unit == ballista.Id);
        Assert.Null(battle.Engine.Shoot(enemy.Id));
        Assert.NotSame(ballista, battle.State.ActiveUnit);
    }

    [Fact]
    public void ClockworkPrecision_NeedsAMachine()
    {
        var battle = Battle(("arcana.clockwork", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        battle.StartWith(own);

        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "arcana.clockwork", null, null));
    }

    // Raise Dead -------------------------------------------------------------------------------

    [Fact]
    public void RaiseDead_RankOne_RaisesNothingDuringTheBattle_AndIsCastOnce()
    {
        var battle = Battle(("death.raise-dead", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var dead = battle.Add(Defender, BuiltInCreatures.Footman, 100, 20, 10);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(own);
        (dead.Count, dead.TopHealth) = (0, 0);

        Assert.Null(battle.Engine.Cast(Attacker, "death.raise-dead", null, null));

        Assert.DoesNotContain(battle.State.Units, u => u.IsRaised);
        Assert.True(battle.State.Raising.ContainsKey(Attacker));
        battle.State.CastRounds.Clear();
        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "death.raise-dead", null, null));
    }

    [Fact]
    public void RaiseDead_RankTwo_RaisesTheDestroyedStackWorthMost_Once()
    {
        var battle = Battle(("death.raise-dead", 2));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var big = battle.Add(Defender, BuiltInCreatures.Footman, 100, 20, 10);
        var small = battle.Add(Defender, BuiltInCreatures.Footman, 10, 20, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(own);
        (big.Count, big.TopHealth) = (0, 0);
        (small.Count, small.TopHealth) = (0, 0);

        Assert.Null(battle.Engine.Cast(Attacker, "death.raise-dead", null, null));

        var raised = Assert.Single(battle.State.Units, u => u.IsRaised);
        Assert.Equal((Attacker, BattleMagic.RaisedCreature, big.Position), (raised.Side, raised.Creature.Id, raised.Position));
        Assert.Equal(100 * 13 * 15 / 100 / 11, raised.Count);
        Assert.Contains(battle.Events, e => e is BattleUnitRaised r && r.From == big.Id);

        Assert.Null(battle.Engine.Defend());
        while (battle.State.Round < 2)
        {
            Assert.Null(battle.Engine.Defend());
        }

        Assert.Single(battle.State.Units, u => u.IsRaised); // once
    }

    [Fact]
    public void RaiseDead_RankThree_RaisesOneMoreEachRound()
    {
        var battle = Battle(("death.raise-dead", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var big = battle.Add(Defender, BuiltInCreatures.Footman, 100, 20, 10);
        var small = battle.Add(Defender, BuiltInCreatures.Footman, 10, 20, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(own);
        (big.Count, big.TopHealth) = (0, 0);
        (small.Count, small.TopHealth) = (0, 0);

        Assert.Null(battle.Engine.Cast(Attacker, "death.raise-dead", null, null));
        Assert.Single(battle.State.Units, u => u.IsRaised);
        while (battle.State.Round < 2)
        {
            Assert.Null(battle.Engine.Defend());
        }

        Assert.Equal(2, battle.State.Units.Count(u => u.IsRaised));
        Assert.Contains(battle.State.Units, u => u.IsRaised && u.Position == small.Position && u.Count == 1); // at least 1
    }

    [Fact]
    public void RaiseDead_AfterAVictory_SkeletonsJoinTheArmy_WithoutNecromancy()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Seraph, 30));
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 100));
        var session = Start(state);
        var raised = new List<UndeadRaised>();
        session.Events.Subscribe<UndeadRaised>(raised.Add);

        session.MoveTo(A, new MapPosition(2, 0));
        state.Battle!.Raising[Attacker] = new RaiseDead("death.raise-dead", 15, SpellRises.None);
        session.AutoResolve();

        var skeletons = Assert.Single(Assert.Single(raised).Raised);
        Assert.Equal((BattleMagic.RaisedCreature, 100 * 13 * 15 / 100 / 11), (skeletons.Creature, skeletons.Count));
        Assert.Equal(skeletons.Count, state.GetHero(A).Army.Stacks.Single(s => s.Stack.Creature == BattleMagic.RaisedCreature).Stack.Count);
    }

    // Blood Price ------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    public void BloodPrice_TradesATenthOfAStack_ForMana(int rank, int gained)
    {
        var battle = Battle(("chaos.blood-price", rank));
        var own = battle.Add(Attacker, BuiltInCreatures.Footman, 10, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 26, 0);
        battle.StartWith(own);
        var mana = battle.State.ManaOf(Attacker);

        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "chaos.blood-price", enemy.Id, null));
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.blood-price", own.Id, null));

        Assert.Equal(9, own.Count);
        Assert.Equal(mana + gained, battle.State.ManaOf(Attacker));
        Assert.Contains(battle.Events, e => e is BattleManaGained g && g.Gained == gained);
    }

    // Puppet Master III ------------------------------------------------------------------------

    [Fact]
    public void PuppetMaster_RankThree_SplitsOffWhatItsLimitHolds()
    {
        var battle = Battle(("shadow.puppet-master", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, BuiltInCreatures.Footman, 100, 20, 10);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.puppet-master", enemy.Id, null));

        var part = battle.State.Units.Single(u => u.Id != own.Id && u.Id != enemy.Id);
        Assert.Equal((600 / 13, Attacker, Defender), (part.Count, part.Side, part.Owner));
        Assert.Equal((100 - (600 / 13), Defender), (enemy.Count, enemy.Side));
        Assert.Equal(100, part.InitialCount + enemy.InitialCount);
        Assert.Equal(enemy.SourceSlot, part.SourceSlot);
        Assert.Contains(battle.Events, e => e is BattleUnitSplit s && s.From == enemy.Id && s.Unit.Unit == part.Id);
    }

    [Fact]
    public void PuppetMaster_RankThree_TakesASmallStackWhole()
    {
        var battle = Battle(("shadow.puppet-master", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, BuiltInCreatures.Footman, 10, 20, 10);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.puppet-master", enemy.Id, null));

        Assert.Equal((Attacker, 10), (enemy.Side, enemy.Count));
        Assert.DoesNotContain(battle.Events, e => e is BattleUnitSplit);
    }

    // The battle AI ----------------------------------------------------------------------------

    [Fact]
    public void TheBattleAi_RaisesTheDead_ButNeverPaysTheBloodPrice()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(0, ("death.raise-dead", 2), ("chaos.blood-price", 1)));
        var own = battle.Add(Attacker, BuiltInCreatures.Footman, 10, 0, 0);
        var dead = battle.Add(Defender, BuiltInCreatures.Footman, 50, 20, 10);
        battle.Add(Defender, BuiltInCreatures.Footman, 10, 26, 0);
        battle.StartWith(own);
        (dead.Count, dead.TopHealth) = (0, 0);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.Contains(battle.Events, e => e is BattleSpellCast { Spell: "death.raise-dead" });
        Assert.DoesNotContain(battle.Events, e => e is BattleSpellCast { Spell: "chaos.blood-price" });
    }

    private static CreatureDefinition Sturdy(string id) => TestBattle.Fixed(id, 10, health: 1000);

    private static TestBattle Battle(params (string Spell, int Rank)[] spells) => TestBattle.Create(attackerCaster: Caster(0, spells));

    private static BattleCaster Caster(int spellPower, params (string Spell, int Rank)[] spells) => new(
        spellPower, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s.Spell), s.Rank, SpellCatalog.Default.Get(s.Spell).Mana)).ToList(), null, 100);
}
