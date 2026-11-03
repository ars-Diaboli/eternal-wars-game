using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Troops breaking the gate, the keep's shooting stack and Earthquake on the walls (ADR-106 g, ADR-120).</summary>
public class SiegeGateAndKeepTests
{
    private static readonly SiegeSpec Fort = new(1, 5);
    private static readonly SiegeSpec Citadel = new(2, 5);
    private const int GateId = 5;

    [Fact]
    public void TheGate_Has100HitPoints_And150BehindUpgradedFortifications()
    {
        Assert.Equal(100, SiegeRules.Create(Fort).Gate.Health);
        Assert.Equal(150, SiegeRules.Create(Citadel).Gate.Health);
        Assert.Equal(150, SiegeRules.Create(new SiegeSpec(3, 5)).Gate.Health);
    }

    [Fact]
    public void ABigStack_BreaksTheGateInTwoHits_AtMost50Each_AndThenWalksThrough()
    {
        var battle = TestBattle.Create(siege: Fort);
        var ram = battle.Add(Attacker, TestBattle.Fixed("ram", 10), 10, 20, 10); // 10 × 10 = 100 a hit, capped at 50
        battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, 33, 2);
        var gate = battle.State.Siege!.Gate;

        battle.StartWith(ram);
        Assert.Null(battle.Engine.AttackStructure(GateId, null));
        Assert.Equal(new SiegeStructureDamaged(GateId, ram.Id, null, SiegeRules.GateDamageCap, 50), battle.Events.OfType<SiegeStructureDamaged>().Single());
        Assert.True(ram.Rect.IsAdjacentTo(gate.Rect));

        battle.StartWith(ram);
        Assert.Null(battle.Engine.AttackStructure(GateId, null));
        Assert.False(gate.IsStanding);
        Assert.False(battle.State.Siege.Blocks(new CellPosition(SiegeRules.WallX, SiegeRules.GateY), Attacker));
        Assert.Contains(BattleGrid.Reachable(battle.State, ram).Keys, p => p.X > SiegeRules.WallX);
    }

    [Fact]
    public void AWeakStack_HardlyScratchesTheGate()
    {
        var battle = TestBattle.Create(siege: Fort);
        var imp = battle.Add(Attacker, TestBattle.Fixed("imp", 2, attack: 0), 1, 23, 10);

        // 2 damage, Attack 0 against the gate's Defense 10: −25 %, rounded down, but never less than 1.
        Assert.Equal(1, SiegeRules.GateDamage(battle.State, imp, 2));
        var strong = battle.Add(Attacker, TestBattle.Fixed("brute", 4, attack: 20), 5, 23, 11);
        Assert.Equal(30, SiegeRules.GateDamage(battle.State, strong, 4)); // 20 × +50 %
    }

    [Fact]
    public void OnlyTheAttackersTroops_HitOnlyTheGate()
    {
        var battle = TestBattle.Create(siege: Fort);
        var attacker = battle.Add(Attacker, TestBattle.Fixed("walker", 5), 5, 23, 10);
        var defender = battle.Add(Defender, TestBattle.Fixed("defender", 5), 5, 25, 10);

        Assert.NotNull(SiegeRules.StructureAttackRefusal(battle.State, defender, GateId));
        Assert.NotNull(SiegeRules.StructureAttackRefusal(battle.State, attacker, 1)); // a wall section needs a war machine
        Assert.Null(SiegeRules.StructureAttackRefusal(battle.State, attacker, GateId));

        battle.StartWith(defender);
        Assert.NotNull(battle.Engine.AttackStructure(GateId, null));
        Assert.Equal(100, battle.State.Siege!.Gate.Health);
    }

    [Fact]
    public void PointingAtTheGate_WithAnAttackingStack_IsAHitOnIt()
    {
        var battle = TestBattle.Create(siege: Fort);
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 5), 5, 20, 11);
        battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, 33, 2);
        battle.StartWith(walker);

        var action = Assert.IsType<AttackStructureAction>(BattleTargeting.ActionAt(battle.State, new CellPosition(SiegeRules.WallX, SiegeRules.GateY + 1)));
        Assert.Equal(GateId, action.Structure);
        Assert.True(walker.RectAt(action.From!.Value).IsAdjacentTo(battle.State.Siege!.Gate.Rect));
    }

    [Fact]
    public void TheBattleAi_BreaksTheGate_WhenThereIsNoOtherWayIn()
    {
        var battle = TestBattle.Create(siege: Fort);
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 10), 10, 18, 11);
        battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, 30, 11);
        battle.StartWith(walker);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.True(battle.State.Siege!.Gate.Health < 100);
    }

    [Fact]
    public void TheDefendersStrongestShooter_TakesTheKeep_IsSafeFromMelee_AndComesOutWhenTheKeepFalls()
    {
        var battle = TestBattle.Create(siege: Citadel, attackerBonuses: HeroBattleBonuses.None with { Ballistics = SkillLevel.Expert });
        var archer = TestBattle.Fixed("archer", 3, shots: 10);
        BattleSetup.Deploy(
            battle.State,
            [BattleStackSpec.Of(WarMachineKind.Ballista), new BattleStackSpec(TestBattle.Fixed("flyer", 3, capabilities: new CreatureCapability(CapabilityKind.Flying)), 5, 0)],
            [new BattleStackSpec(archer, 5, 0), new BattleStackSpec(archer, 20, 1), new BattleStackSpec(TestBattle.Fixed("guard", 3), 10, 2)]);
        var state = battle.State;
        var keeper = Assert.Single(state.Units, u => u.InKeep);
        Assert.Equal((Defender, 20), (keeper.Side, keeper.Count));
        Assert.True(keeper.Position.X > SiegeRules.WallX);
        Assert.Equal([keeper.Position], BattleGrid.Reachable(state, keeper).Keys);

        // A flyer that lands next to it still cannot strike it; shots at it take the keep's 1/2 cover.
        var flyer = state.Units.Single(u => u.Creature.Id == "flyer");
        flyer.Position = keeper.Position.Offset(-1, 0);
        Assert.Empty(BattleGrid.AttackPositions(state, flyer, keeper));
        Assert.Equal(ObstacleClass.Wall.CoverEighths(), BattleGrid.CoverEighths(state, flyer, keeper));

        var keep = state.Siege!.Structures.Single(s => s.Kind == SiegeStructureKind.Keep);
        keep.Health = 1;
        battle.StartWith(state.Units.Single(u => u.Machine == WarMachineKind.Ballista));
        Assert.Null(battle.Engine.ShootStructure(keep.Id)); // every shot hits at Expert
        Assert.False(keep.IsStanding);
        Assert.False(keeper.InKeep);
        Assert.Contains(new BattleUnitLeftKeep(keeper.Id), battle.Events);
        Assert.NotEmpty(BattleGrid.AttackPositions(state, flyer, keeper));
    }

    [Fact]
    public void Earthquake_HurtsEveryStack_AndTwoWallSections()
    {
        var spell = SpellCatalog.Default.Get("nature.earthquake");
        Assert.Equal((SpellEffectKind.Damage, 40, SpellReach.All, 2), (spell.Effect!.Kind, spell.Effect.Amount, spell.Effect.Reach, spell.Effect.Walls));
        var caster = new BattleCaster(0, 1, [new KnownSpell(spell, 1, spell.Mana)], null, 30);
        var battle = TestBattle.Create(siege: Fort, attackerCaster: caster);
        var attacker = battle.Add(Attacker, TestBattle.Fixed("walker", 1, health: 100), 1, 10, 10);
        var defender = battle.Add(Defender, TestBattle.Fixed("defender", 1, health: 100), 1, 30, 10);
        battle.StartWith(attacker);

        Assert.Null(battle.Engine.Cast(Attacker, spell.Id, null, null));

        var quakes = battle.Events.OfType<SiegeStructureDamaged>().ToList();
        Assert.Equal(2, quakes.Count);
        Assert.All(quakes, q => Assert.Equal((spell.Id, 1, 1), (q.Spell, q.Damage, q.Health)));
        Assert.Equal(2, quakes.Select(q => q.Structure).Distinct().Count());
        Assert.Equal((60, 60), (attacker.TotalHealth, defender.TotalHealth));
        Assert.Equal(100, battle.State.Siege!.Gate.Health); // the gate is not a wall section
    }
}
