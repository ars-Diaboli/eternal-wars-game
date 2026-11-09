using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>ADR-151: the spells aimed with two clicks: Teleport and Shadow Step (a stack, then its cell), Drain Life (an enemy, then the own stack it heals).</summary>
public class TwoClickSpellTests
{
    // The table and the grammar ----------------------------------------------------------------

    [Fact]
    public void TheMoveSpells_RankByRank()
    {
        var teleport = SpellCatalog.Default.Get("arcana.teleport");
        var step = SpellCatalog.Default.Get("shadow.shadow-step");

        Assert.All(new[] { teleport, step }, s => Assert.All(new[] { 1, 2, 3 }, r => Assert.Equal(SpellEffectKind.Move, s.EffectAt(r)!.Kind)));
        Assert.Equal((10, 0, 0), (teleport.EffectAt(1)!.MoveRange, teleport.EffectAt(2)!.MoveRange, teleport.EffectAt(3)!.MoveRange));
        Assert.Equal((SpellTurnAgain.None, SpellTurnAgain.Act), (teleport.EffectAt(2)!.Again, teleport.EffectAt(3)!.Again));
        Assert.Equal((true, false, false), (step.EffectAt(1)!.Shadow, step.EffectAt(2)!.Shadow, step.EffectAt(3)!.Shadow));
        Assert.Equal(SpellTurnAgain.Attack, step.EffectAt(3)!.Again);
        Assert.All(new[] { teleport, step }, s => Assert.True(s.EffectAt(1)!.TwoClicks));
    }

    [Fact]
    public void DrainLife_RankByRank()
    {
        var drain = SpellCatalog.Default.Get("death.drain-life");

        Assert.Equal((50, 50, SpellFilter.Living), (drain.EffectAt(1)!.Amount, drain.EffectAt(1)!.DrainPercent, drain.EffectAt(1)!.Filter));
        Assert.Equal((1, 2, 2), (drain.EffectAt(1)!.Targets, drain.EffectAt(2)!.Targets, drain.EffectAt(3)!.Targets));
        Assert.Equal((false, false, true), (drain.EffectAt(1)!.DrainRevives, drain.EffectAt(2)!.DrainRevives, drain.EffectAt(3)!.DrainRevives));
        Assert.True(drain.EffectAt(1)!.TwoClicks);
    }

    [Theory]
    [InlineData("move 0")]
    [InlineData("move")]
    [InlineData("move field rounds 2")]
    [InlineData("move 10 targets 2")]
    [InlineData("move field attack 2")]
    [InlineData("buff attack 2 rounds 2 shadow")]
    [InlineData("damage 30 again")]
    [InlineData("heal 20 drain% 50")]
    [InlineData("damage 30 area 3 drain% 50")]
    [InlineData("damage 30 drain-revive")]
    [InlineData("damage 30 drain% 0")]
    public void TheGrammar_RefusesWhatDoesNotFit(string text) => Assert.Throws<FormatException>(() => SpellEffect.Parse(text));

    // Teleport ---------------------------------------------------------------------------------

    [Fact]
    public void Teleport_MovesAnOwnStackWithinTen_AndTheActiveStackKeepsItsAction()
    {
        var battle = Battle(("arcana.teleport", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 20);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.teleport", own.Id, new CellPosition(8, 6)));

        Assert.Equal(new CellPosition(8, 6), own.Position);
        Assert.Contains(battle.Events, e => e is BattleUnitTeleported { Spell: "arcana.teleport" } t && t.Unit == own.Id && t.From == new CellPosition(0, 0));
        Assert.Contains(battle.Events, e => e is BattleSpellCast { Spell: "arcana.teleport" } c && c.Cell == new CellPosition(8, 6));
        Assert.Same(own, battle.State.ActiveUnit);
    }

    [Fact]
    public void Teleport_RefusesFarCells_TakenCells_TheSameCell_AndEnemyStacks()
    {
        var battle = Battle(("arcana.teleport", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var friend = battle.Add(Attacker, Sturdy("friend"), 1, 6, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 26, 20);
        battle.StartWith(own);

        Assert.NotNull(Refusal(battle, "arcana.teleport", own, new CellPosition(11, 0)));
        Assert.NotNull(Refusal(battle, "arcana.teleport", own, new CellPosition(5, 0))); // overlaps the friend
        Assert.NotNull(Refusal(battle, "arcana.teleport", own, own.Position));
        Assert.NotNull(Refusal(battle, "arcana.teleport", own, null));
        Assert.NotNull(Refusal(battle, "arcana.teleport", enemy, new CellPosition(20, 20)));
        Assert.Null(Refusal(battle, "arcana.teleport", friend, new CellPosition(6, 10))); // any own stack, not only the active one
    }

    [Fact]
    public void Teleport_RankTwo_GoesAnywhere()
    {
        var battle = Battle(("arcana.teleport", 2));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 20);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.teleport", own.Id, new CellPosition(24, 20)));
        Assert.Equal(new CellPosition(24, 20), own.Position);
    }

    [Fact]
    public void Teleport_RankThree_GivesAStackThatHasActed_AnotherTurn_ButNotOneStillToAct()
    {
        var battle = Battle(("arcana.teleport", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var done = battle.Add(Attacker, Sturdy("done"), 1, 0, 6);
        var later = battle.Add(Attacker, Sturdy("later"), 1, 0, 12);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 20);
        battle.StartWith(own);
        battle.State.MutableQueue.Remove(done.Id); // it has taken its turn this round

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.teleport", done.Id, new CellPosition(10, 6)));

        Assert.Contains(done.Id, battle.State.Waiting);
        Assert.Contains(battle.Events, e => e is BattleUnitTurnRegained { InPlace: false } t && t.Unit == done.Id);
        Assert.False(done.StaysPut);

        var other = Battle(("arcana.teleport", 3));
        own = other.Add(Attacker, Sturdy("own"), 1, 0, 0);
        later = other.Add(Attacker, Sturdy("later"), 1, 0, 12);
        other.Add(Defender, Sturdy("enemy"), 1, 26, 20);
        other.StartWith(own);
        Assert.Null(other.Engine.Cast(Attacker, "arcana.teleport", later.Id, new CellPosition(10, 12)));
        Assert.DoesNotContain(later.Id, other.State.Waiting);
        Assert.DoesNotContain(other.Events, e => e is BattleUnitTurnRegained);
    }

    [Fact]
    public void RootedStacks_AndWarMachines_CannotBeMoved()
    {
        var battle = Battle(("arcana.teleport", 2));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var rooted = new BattleUnit(9, Attacker, Sturdy("treant"), 1, -1, new CellPosition(0, 10)) { IsRooted = true };
        battle.State.MutableUnits.Add(rooted);
        var tent = new BattleUnit(10, Attacker, WarMachines.Definition(WarMachineKind.HealingTent), 1, -1, new CellPosition(0, 20), WarMachineKind.HealingTent);
        battle.State.MutableUnits.Add(tent);
        battle.Add(Defender, Sturdy("enemy"), 1, 26, 20);
        battle.StartWith(own);

        Assert.NotNull(Refusal(battle, "arcana.teleport", rooted, new CellPosition(10, 10)));
        Assert.NotNull(Refusal(battle, "arcana.teleport", tent, new CellPosition(10, 20)));
    }

    // Shadow Step ------------------------------------------------------------------------------

    [Fact]
    public void ShadowStep_LandsOnlyAwayFromEnemies_RankTwoAlsoNextToThem()
    {
        var battle = Battle(("shadow.shadow-step", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        battle.StartWith(own);

        Assert.NotNull(Refusal(battle, "shadow.shadow-step", own, new CellPosition(18, 10)));
        Assert.Null(Refusal(battle, "shadow.shadow-step", own, new CellPosition(16, 10)));

        var two = Battle(("shadow.shadow-step", 2));
        own = two.Add(Attacker, Sturdy("own"), 1, 0, 0);
        two.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        two.StartWith(own);
        Assert.Null(two.Engine.Cast(Attacker, "shadow.shadow-step", own.Id, new CellPosition(18, 10)));
        Assert.Null(two.Engine.Attack(two.State.Units[1].Id, own.Position)); // it attacks from where it landed
    }

    [Fact]
    public void ShadowStep_RankThree_GivesATurnInPlace_ToAttackFromWhereItLanded()
    {
        var battle = Battle(("shadow.shadow-step", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var done = battle.Add(Attacker, Sturdy("done"), 1, 0, 10);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        battle.StartWith(own);
        battle.State.MutableQueue.Remove(done.Id);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.shadow-step", done.Id, new CellPosition(18, 10)));
        Assert.True(done.StaysPut);
        Assert.Contains(battle.Events, e => e is BattleUnitTurnRegained { InPlace: true } t && t.Unit == done.Id);

        battle.State.ActiveUnit = done;
        Assert.Single(BattleGrid.Reachable(battle.State, done)); // it may not move on this turn
        Assert.NotNull(battle.Engine.Wait());
        Assert.Null(battle.Engine.Attack(enemy.Id, null));
        Assert.False(done.StaysPut);
    }

    // Drain Life -------------------------------------------------------------------------------

    [Fact]
    public void DrainLife_HurtsALivingEnemy_AndHealsTheOwnStackPicked_ByHalfTheDamage()
    {
        var battle = Battle(("death.drain-life", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 0);
        battle.StartWith(own);
        own.TopHealth = 900;

        Assert.Null(battle.Engine.Cast(Attacker, "death.drain-life", enemy.Id, null, own.Id));

        Assert.Equal(950, enemy.TotalHealth);
        Assert.Equal(925, own.TopHealth);
        Assert.Contains(battle.Events, e => e is BattleSpellCast { Spell: "death.drain-life" } c && c.Second == own.Id);
        Assert.Contains(battle.Events, e => e is BattleUnitHealed { Healed: 25 } h && h.Unit == own.Id);
    }

    [Fact]
    public void DrainLife_RefusesTheUndead_AndAnEnemyToHeal_AndNeedsNoSecondStack()
    {
        var battle = Battle(("death.drain-life", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 0);
        var ghost = battle.Add(Defender, Sturdy("ghost") with { Tags = [CreatureTags.Undead] }, 1, 20, 10);
        battle.StartWith(own);

        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "death.drain-life", ghost.Id, null, own.Id));
        Assert.NotNull(BattleMagic.CastRefusal(battle.State, Attacker, "death.drain-life", enemy.Id, null, ghost.Id));
        Assert.Null(battle.Engine.Cast(Attacker, "death.drain-life", enemy.Id, null));
        Assert.DoesNotContain(battle.Events, e => e is BattleUnitHealed);
    }

    [Fact]
    public void DrainLife_RankTwo_HitsTwoStacks_RankThree_RevivesTheDeadForGood()
    {
        var battle = Battle(("death.drain-life", 3));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 10, health: 10), 10, 0, 0);
        battle.Add(Defender, Sturdy("enemy"), 1, 20, 0);
        var second = battle.Add(Defender, Sturdy("second"), 1, 20, 4);
        battle.StartWith(own);
        own.Count = 4; // 6 of its 10 have died

        Assert.Null(battle.Engine.Cast(Attacker, "death.drain-life", 2, null, own.Id));

        Assert.Equal(950, second.TotalHealth);
        Assert.Equal(9, own.Count); // half of 100 damage: 50 health, 5 creatures
        Assert.Equal(9, own.KeptCount);
    }

    // The AI -----------------------------------------------------------------------------------

    [Fact]
    public void TheBattleAi_TeleportsAMeleeStackThatCannotReachAnEnemy_NextToIt_AndAttacks()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(0, ("arcana.teleport", 2)));
        var own = battle.Add(Attacker, BuiltInCreatures.Footman, 10, 0, 0);
        var enemy = battle.Add(Defender, BuiltInCreatures.Footman, 10, 26, 20);
        battle.StartWith(own);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.Contains(battle.Events, e => e is BattleUnitTeleported t && t.Unit == own.Id);
        Assert.Contains(battle.Events, e => e is BattleUnitAttacked { Kind: BattleAttackKind.Melee } a && a.Attacker == own.Id && a.Target == enemy.Id);
    }

    [Fact]
    public void TheBattleAi_DrainsIntoItsMostWoundedStack()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(0, ("death.drain-life", 1)));
        var own = battle.Add(Attacker, BuiltInCreatures.Footman, 10, 0, 0);
        var wounded = battle.Add(Attacker, BuiltInCreatures.Footman, 10, 0, 10);
        battle.Add(Defender, BuiltInCreatures.Footman, 10, 26, 0);
        battle.StartWith(own);
        wounded.TopHealth = 1;

        BattleAi.Act(battle.State, battle.Engine);

        Assert.Contains(battle.Events, e => e is BattleSpellCast { Spell: "death.drain-life" } c && c.Second == wounded.Id);
    }

    private static string? Refusal(TestBattle battle, string spell, BattleUnit unit, CellPosition? to) =>
        BattleMagic.CastRefusal(battle.State, Attacker, spell, unit.Id, to);

    private static CreatureDefinition Sturdy(string id) => TestBattle.Fixed(id, 10, health: 1000);

    private static TestBattle Battle(params (string Spell, int Rank)[] spells) => TestBattle.Create(attackerCaster: Caster(0, spells));

    private static BattleCaster Caster(int spellPower, params (string Spell, int Rank)[] spells) => new(
        spellPower, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s.Spell), s.Rank, SpellCatalog.Default.Get(s.Spell).Mana)).ToList(), null, 100);
}
