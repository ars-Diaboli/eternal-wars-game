using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The status bar's line for a click on the field (ADR-314): the action, its estimate, whether the target answers, a spell's refusal.</summary>
public class BattleHoverLineTests
{
    [Fact]
    public void AnAttack_NamesTheTarget_TheDamage_TheKills_AndTheReturnBlow()
    {
        var battle = TestBattle.Create();
        var swordsmen = battle.Add(Attacker, TestBattle.Fixed("Swordsmen", 10) with { MinDamage = 5 }, 10, 0, 0);
        var wolves = battle.Add(Defender, TestBattle.Fixed("Wolves", 1, health: 30), 10, 2, 0);
        battle.StartWith(swordsmen);
        var estimate = AttackEstimate.Melee(battle.State, swordsmen, wolves);

        var line = BattleHoverLine.Of(battle.State, new AttackAction(wolves.Id));

        Assert.Equal(
            new BattleHoverLine("Swordsmen", $"Attack the Wolves: {estimate.MinDamage}–{estimate.MaxDamage} damage, {estimate.MinKills}–{estimate.MaxKills} die; they strike back"),
            line);
    }

    [Fact]
    public void AMove_AndNoAction()
    {
        var battle = TestBattle.Create();
        var swordsmen = battle.Add(Attacker, TestBattle.Fixed("Swordsmen", 10), 10, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("Wolves", 1), 10, 20, 0);
        battle.StartWith(swordsmen);

        Assert.Equal(new BattleHoverLine("Swordsmen", "Move here"), BattleHoverLine.Of(battle.State, new MoveAction(new CellPosition(3, 3))));
        Assert.Null(BattleHoverLine.Of(battle.State, null));
    }

    [Theory]
    [InlineData(5, 5, 1, 1, true, false, false, "5 damage, 1 dies; they strike back")]
    [InlineData(5, 9, 0, 0, false, false, false, "5–9 damage, none die; no return blow")]
    [InlineData(20, 20, 2, 2, true, true, false, "20 damage, 2 die; they shoot back")]
    [InlineData(40, 60, 4, 4, false, false, false, "40–60 damage, all 4 die; no return blow")]
    [InlineData(12, 12, 1, 1, false, false, true, "12 damage, 1 dies")]
    public void Describe_TheRangeTheKillsAndTheAnswer(int min, int max, int minKills, int maxKills, bool back, bool ranged, bool spell, string expected)
    {
        var battle = TestBattle.Create();
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 10), 4, 2, 0);

        Assert.Equal(expected, BattleHoverLine.Describe(new AttackEstimate(min, max, minKills, maxKills, back), target, ranged, spell));
    }

    [Fact]
    public void ADamageSpell_GivesItsEstimate_AndAWrongTargetItsRefusal()
    {
        var arrow = new KnownSpell(SpellCatalog.Default.Get("arcana.magic-arrow"), 1, 5);
        var battle = TestBattle.Create(attackerCaster: new BattleCaster(0, 1, [arrow], null, 100));
        var own = battle.Add(Attacker, TestBattle.Fixed("Swordsmen", 1), 1, 0, 0);
        var wolves = battle.Add(Defender, TestBattle.Fixed("Wolves", 1, health: 15), 10, 20, 0);
        battle.StartWith(own);
        var damage = BattleMagic.Damage(battle.State, Attacker, arrow, wolves);

        var line = BattleHoverLine.OfSpell(battle.State, wolves.Position, "arcana.magic-arrow", creature: false);

        Assert.Equal(arrow.Spell.Name, line!.Subject);
        Assert.Equal($"Cast at the Wolves: {damage} damage, {damage / 15} {(damage / 15 == 1 ? "dies" : "die")}", line.Detail);
        var refused = BattleHoverLine.OfSpell(battle.State, own.Position, "arcana.magic-arrow", creature: false);
        Assert.Equal(BattleMagic.CastRefusal(battle.State, Attacker, arrow.Spell.Id, own.Id, own.Position), refused!.Detail);
        Assert.Null(BattleHoverLine.OfSpell(battle.State, own.Position, "light.bless", creature: false)); // not in the book
    }

    [Fact]
    public void ARevive_AimsAtAFallenStack_AsTheCastDoes()
    {
        var resurrection = new KnownSpell(SpellCatalog.Default.Get("light.resurrection"), 3, 5);
        var battle = TestBattle.Create(attackerCaster: new BattleCaster(0, 1, [resurrection], null, 100));
        var own = battle.Add(Attacker, TestBattle.Fixed("Swordsmen", 1), 1, 0, 0);
        var fallen = battle.Add(Attacker, TestBattle.Fixed("Pikemen", 1, health: 10), 20, 0, 10);
        battle.Add(Defender, TestBattle.Fixed("Wolves", 1), 1, 20, 0);
        battle.StartWith(own);
        fallen.Count = 0;
        fallen.TopHealth = 0;

        Assert.Same(fallen, battle.State.SpellTargetAt(fallen.Position, Attacker, resurrection.Effect!));
        Assert.Null(battle.State.UnitAt(fallen.Position));
        Assert.Null(BattleMagic.CastRefusal(battle.State, Attacker, resurrection.Spell.Id, fallen.Id, fallen.Position));
        Assert.Equal(
            new BattleHoverLine(resurrection.Spell.Name, "Cast at the fallen Pikemen"),
            BattleHoverLine.OfSpell(battle.State, fallen.Position, "light.resurrection", creature: false));
    }

    [Fact]
    public void ARevive_TakesTheOwnFallenStack_UnderAnEnemyOne()
    {
        var resurrection = new KnownSpell(SpellCatalog.Default.Get("light.resurrection"), 3, 5);
        var battle = TestBattle.Create(attackerCaster: new BattleCaster(0, 1, [resurrection], null, 100));
        var own = battle.Add(Attacker, TestBattle.Fixed("Swordsmen", 1), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("Wolves", 1), 5, 10, 5);
        var fallen = battle.Add(Attacker, TestBattle.Fixed("Pikemen", 1, health: 10), 20, 10, 5);
        battle.Add(Defender, TestBattle.Fixed("Bats", 1), 1, 20, 0);
        battle.StartWith(own);
        (enemy.Count, enemy.TopHealth, fallen.Count, fallen.TopHealth) = (0, 0, 0, 0);

        Assert.Same(fallen, battle.State.SpellTargetAt(fallen.Position, Attacker, resurrection.Effect!));
        Assert.Same(enemy, battle.State.SpellTargetAt(fallen.Position, Defender, resurrection.Effect!)); // for the defender, its own Wolves
    }

    [Fact]
    public void AnAreaSpell_AtACorpse_IsCastAtTheCell()
    {
        var fireball = new KnownSpell(SpellCatalog.Default.Get("chaos.fireball"), 1, 5);
        var battle = TestBattle.Create(attackerCaster: new BattleCaster(0, 1, [fireball], null, 100));
        var own = battle.Add(Attacker, TestBattle.Fixed("Swordsmen", 1), 1, 0, 0);
        var dead = battle.Add(Defender, TestBattle.Fixed("Wolves", 1), 5, 10, 5);
        battle.Add(Defender, TestBattle.Fixed("Bats", 1), 1, 20, 0);
        battle.StartWith(own);
        (dead.Count, dead.TopHealth) = (0, 0);

        Assert.Null(battle.State.SpellTargetAt(dead.Position, Attacker, fireball.Effect!));
        Assert.Equal(new BattleHoverLine(fireball.Spell.Name, "Cast here"), BattleHoverLine.OfSpell(battle.State, dead.Position, "chaos.fireball", creature: false));
    }

    [Fact]
    public void ATwoClickSpell_NamesItsFirstAim_ThenItsSecond_WithNoEstimate()
    {
        var teleport = new KnownSpell(SpellCatalog.Default.Get("arcana.teleport"), 1, 5);
        var battle = TestBattle.Create(attackerCaster: new BattleCaster(0, 1, [teleport], null, 100));
        var own = battle.Add(Attacker, TestBattle.Fixed("Swordsmen", 1), 1, 0, 0);
        var wolves = battle.Add(Defender, TestBattle.Fixed("Wolves", 1, health: 15), 10, 20, 0);
        battle.StartWith(own);

        Assert.True(teleport.Effect!.TwoClicks);
        Assert.Equal("Cast at the Swordsmen", BattleHoverLine.OfSpell(battle.State, own.Position, "arcana.teleport", false)!.Detail);
        Assert.Equal("Second aim: the Wolves", BattleHoverLine.OfSpell(battle.State, wolves.Position, "arcana.teleport", false, secondAim: true)!.Detail);
        Assert.Equal("Second aim here", BattleHoverLine.OfSpell(battle.State, new CellPosition(5, 5), "arcana.teleport", false, secondAim: true)!.Detail);
    }
}
