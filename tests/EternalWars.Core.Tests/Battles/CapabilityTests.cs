using EternalWars.Core.Data.Content;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Domain.Creatures.CapabilityKind;

namespace EternalWars.Core.Tests.Battles;

/// <summary>HoMM4-style creature capabilities (ADR-042).</summary>
public class CapabilityTests
{
    [Fact]
    public void FirstStrike_RetaliatesBeforeTheAttack()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 1, health: 100), 10, 0, 0);
        var pikemen = battle.Add(Defender, TestBattle.Fixed("pike", 1, health: 100, capabilities: new CreatureCapability(FirstStrike)), 10, 2, 0);
        battle.StartWith(attacker);

        battle.Engine.Attack(pikemen.Id, null);

        Assert.Equal([BattleAttackKind.Retaliation, BattleAttackKind.Melee], Kinds(battle));
    }

    [Fact]
    public void FirstStrike_OnBothSides_KeepsTheNormalOrder()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 1, health: 100, capabilities: new CreatureCapability(FirstStrike)), 10, 0, 0);
        var defender = battle.Add(Defender, TestBattle.Fixed("d", 1, health: 100, capabilities: new CreatureCapability(FirstStrike)), 10, 2, 0);
        battle.StartWith(attacker);

        battle.Engine.Attack(defender.Id, null);

        Assert.Equal([BattleAttackKind.Melee, BattleAttackKind.Retaliation], Kinds(battle));
    }

    [Fact]
    public void FirstStrike_ThatKillsTheAttacker_PreventsItsBlow()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 50, health: 5), 1, 0, 0);
        var defender = battle.Add(Defender, TestBattle.Fixed("d", 10, health: 100, capabilities: new CreatureCapability(FirstStrike)), 10, 2, 0);
        battle.StartWith(attacker);

        battle.Engine.Attack(defender.Id, null);

        Assert.False(attacker.IsAlive);
        Assert.Equal([BattleAttackKind.Retaliation], Kinds(battle));
        Assert.Equal(10, defender.Count);
    }

    [Fact]
    public void RangedRetaliation_ShootsBack_UsingAmmoAndTheRoundsRetaliation()
    {
        var battle = TestBattle.Create();
        var shooter = battle.Add(Attacker, TestBattle.Fixed("s", 1, health: 100, shots: 5), 10, 0, 0);
        var monk = battle.Add(Defender, TestBattle.Fixed("monk", 1, health: 100, shots: 5, capabilities: new CreatureCapability(RangedRetaliation)), 10, 20, 0);
        battle.StartWith(shooter);

        battle.Engine.Shoot(monk.Id);

        Assert.Equal([BattleAttackKind.Ranged, BattleAttackKind.RangedRetaliation], Kinds(battle));
        Assert.Equal((4, 4), (shooter.ShotsLeft, monk.ShotsLeft));
        Assert.True(monk.HasRetaliated);

        battle.State.ActiveUnit = shooter;
        battle.Engine.Shoot(monk.Id);

        Assert.Equal(1, Kinds(battle).Count(k => k == BattleAttackKind.RangedRetaliation)); // once per round
    }

    [Fact]
    public void RangedRetaliation_NotWhenEngaged_OutOfAmmo_OrAgainstNoRetaliation()
    {
        var engaged = TestBattle.Create();
        var shooter = engaged.Add(Attacker, TestBattle.Fixed("s", 1, health: 100, shots: 5), 10, 0, 0);
        var monk = engaged.Add(Defender, TestBattle.Fixed("monk", 1, health: 100, shots: 5, capabilities: new CreatureCapability(RangedRetaliation)), 10, 20, 0);
        engaged.Add(Attacker, TestBattle.Fixed("blocker", 1, health: 100), 1, 22, 0); // adjacent to the monk
        engaged.StartWith(shooter);
        engaged.Engine.Shoot(monk.Id);
        Assert.Equal([BattleAttackKind.Ranged], Kinds(engaged));

        var devilish = TestBattle.Create();
        var sniper = devilish.Add(Attacker, TestBattle.Fixed("s", 1, health: 100, shots: 5, capabilities: new CreatureCapability(NoEnemyRetaliation)), 10, 0, 0);
        var priest = devilish.Add(Defender, TestBattle.Fixed("priest", 1, health: 100, shots: 5, capabilities: new CreatureCapability(RangedRetaliation)), 10, 20, 0);
        devilish.StartWith(sniper);
        devilish.Engine.Shoot(priest.Id);
        Assert.Equal([BattleAttackKind.Ranged], Kinds(devilish));
    }

    [Fact]
    public void ShootsTwice_FiresAgainAfterTheRangedRetaliation()
    {
        var battle = TestBattle.Create();
        var marksman = battle.Add(Attacker, TestBattle.Fixed("m", 1, health: 100, shots: 5, capabilities: new CreatureCapability(ShootsTwice)), 10, 0, 0);
        var monk = battle.Add(Defender, TestBattle.Fixed("monk", 1, health: 100, shots: 5, capabilities: new CreatureCapability(RangedRetaliation)), 10, 20, 0);
        battle.StartWith(marksman);

        battle.Engine.Shoot(monk.Id);

        Assert.Equal([BattleAttackKind.Ranged, BattleAttackKind.RangedRetaliation, BattleAttackKind.Ranged], Kinds(battle));
        Assert.Equal(3, marksman.ShotsLeft);
    }

    [Fact]
    public void Validation_LimitsCapabilities_AndChecksTheirData()
    {
        var basic = TestBattle.Fixed("x", 1);
        CreatureCapability[] six = [new(Flying), new(DoubleStrike), new(FirstStrike), new(Breath), new(Cleave), new(UnlimitedRetaliation)];

        Assert.Throws<FormatException>(() => (basic with { Capabilities = six }).Validate());
        (basic with { Capabilities = six[..5] }).Validate();
        Assert.Throws<FormatException>(() => (basic with { Capabilities = [new(SpellImmunity)] }).Validate());
        Assert.Throws<FormatException>(() => (basic with { Capabilities = [new(Caster, 0, "cure")] }).Validate());
        Assert.Throws<FormatException>(() => (basic with { Capabilities = [new(PackHunter)] }).Validate());
        Assert.Throws<FormatException>(() => (basic with { Capabilities = [new(MeleePenalty)] }).Validate()); // not a shooter
        Assert.Throws<FormatException>(() => (basic with { Capabilities = [new(FirstStrike), new(FirstStrike)] }).Validate());
        Assert.All(BuiltInCreatures.All, c => Assert.InRange(c.Capabilities.Count, 0, CreatureDefinition.MaxCapabilities));
    }

    [Fact]
    public void SpellCapabilities_AreQueryableHooks_AndSurviveJson()
    {
        var seraph = CreatureCatalog.Default.Get(BuiltInCreatures.Seraph);
        var devil = CreatureCatalog.Default.Get(BuiltInCreatures.Devil);

        Assert.True(seraph.IsImmuneTo([SpellTags.Mind]));
        Assert.False(seraph.IsImmuneTo([SpellTags.Fire]));
        Assert.True(devil.IsImmuneTo([SpellTags.Fire, SpellTags.Curse]));
        Assert.True((devil with { Capabilities = [new(SpellImmunity, Parameter: SpellTags.All)] }).IsImmuneTo(["anything"]));
        Assert.Equal(1, seraph.SpellCasts["resurrection"]);

        var battle = TestBattle.Create();
        Assert.Equal(1, battle.Add(Attacker, seraph, 1, 0, 0).CastsLeft["resurrection"]);

        var loaded = CreatureDefinitionSerializer.Deserialize(CreatureDefinitionSerializer.Serialize([seraph])).Single();
        Assert.Equal(seraph.Capabilities, loaded.Capabilities);
    }

    private static List<BattleAttackKind> Kinds(TestBattle battle) =>
        battle.Events.OfType<BattleUnitAttacked>().Select(a => a.Kind).ToList();
}
