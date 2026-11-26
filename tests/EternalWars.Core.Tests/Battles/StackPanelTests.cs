using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The stack panel's numbers and marks (ADR-314): the stack as it strikes and is struck now, and what is on it.</summary>
public class StackPanelTests
{
    [Fact]
    public void TheNumbers_AreTheStacksNow_WithTheHerosStats_AndTheNativeTerrain()
    {
        var battle = TestBattle.Create(attackerAttack: 3, nativeTowns: ["haven"]);
        var swordsmen = battle.Add(Attacker, TestBattle.Fixed("swordsmen", 4, health: 12) with { Town = "haven", MinDamage = 2 }, 7, 0, 0);
        swordsmen.TopHealth = 5;

        var panel = StackPanel.Of(battle.State, swordsmen);

        Assert.Equal(("swordsmen", 7, false), (panel.Name, panel.Count, panel.IsMachine));
        Assert.Equal((14, 11), (panel.Attack, panel.Defense)); // 10 + 3 from the hero + 1 native; 10 + 1 native
        Assert.Equal((2, 4, 5, 12), (panel.MinDamage, panel.MaxDamage, panel.Health, panel.MaxHealth));
        Assert.Equal((11, battle.State.InitiativeOf(swordsmen)), (panel.Speed, panel.Initiative)); // 10 + 1 native
        Assert.Equal((battle.State.MoraleOf(swordsmen), battle.State.LuckOf(swordsmen)), (panel.Morale, panel.Luck));
        Assert.Null(panel.Shots); // it does not shoot
        Assert.Equal([new StackMark(StackMarkKind.NativeTerrain, "Native terrain: +1 Attack, +1 Defense, +1 Speed")], panel.Marks);
    }

    [Fact]
    public void Defending_RaisesTheDefense_AndIsMarked()
    {
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("u", 1), 1, 0, 0);
        unit.IsDefending = true;

        var panel = StackPanel.Of(battle.State, unit);

        Assert.Equal(10 + DamageCalculator.DefendBonus(10), panel.Defense);
        Assert.Equal([StackMarkKind.Defending], panel.Marks.Select(m => m.Kind));
    }

    [Fact]
    public void TheSpellsOnIt_AreMarked_WithHowLongTheyLast_AndWhoseTheyAre()
    {
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("u", 1), 1, 0, 0);
        unit.MutableSpells.Add(new ActiveSpell("light.bless", Attacker, new SpellModifiers(Roll: 1), 3));
        unit.MutableSpells.Add(new ActiveSpell("x.unknown", Defender, new SpellModifiers(), 9, TurnsLeft: 2));

        var marks = StackPanel.Of(battle.State, unit).Marks;

        Assert.Equal(
            [
                new StackMark(StackMarkKind.Spell, "Bless: until the end of round 3", "light.bless", Friendly: true),
                new StackMark(StackMarkKind.Spell, "x.unknown: 2 more turns", "x.unknown", Friendly: false), // a spell the catalog lacks keeps its id
            ],
            marks);
    }

    [Fact]
    public void AShooter_HasItsShots_AndACreatureWithoutMorale_None()
    {
        var battle = TestBattle.Create();
        var archers = battle.Add(Attacker, TestBattle.Fixed("archers", 2, shots: 6), 5, 0, 0);
        archers.ShotsLeft = 4;
        var ghouls = battle.Add(Defender, TestBattle.Fixed("ghouls", 2) with { Tags = [CreatureTags.Undead] }, 5, 20, 0);

        Assert.Equal((4, false), (StackPanel.Of(battle.State, archers).Shots, StackPanel.Of(battle.State, archers).UnlimitedShots));
        Assert.NotNull(StackPanel.Of(battle.State, archers).Morale);
        Assert.Null(StackPanel.Of(battle.State, ghouls).Morale);
    }

    [Fact]
    public void AWarMachine_HasOnlyTheNumbersItHas()
    {
        var battle = TestBattle.Create();
        var ballista = new BattleUnit(1, Attacker, WarMachines.Definition(WarMachineKind.Ballista), 1, -1, new CellPosition(0, 10), WarMachineKind.Ballista);
        var tent = new BattleUnit(2, Attacker, WarMachines.Definition(WarMachineKind.HealingTent), 1, -1, new CellPosition(0, 20), WarMachineKind.HealingTent);
        battle.State.MutableUnits.Add(ballista);
        battle.State.MutableUnits.Add(tent);

        var shooter = StackPanel.Of(battle.State, ballista);
        var other = StackPanel.Of(battle.State, tent);

        Assert.True(shooter.IsMachine);
        Assert.Equal(DamageCalculator.AttackOf(battle.State, ballista), shooter.Attack);
        Assert.NotNull(shooter.MinDamage);
        Assert.NotNull(shooter.Initiative);
        Assert.Equal(ballista.ShotsLeft, shooter.Shots);
        Assert.Equal<int?>([null, null, null, null], [shooter.Defense, shooter.Speed, shooter.Morale, shooter.Luck]);
        Assert.Empty(shooter.Marks);

        Assert.Equal(tent.TopHealth, other.Health);
        Assert.Equal<int?>([null, null, null, null, null], [other.Attack, other.MinDamage, other.Initiative, other.Shots, other.Speed]);
    }
}
