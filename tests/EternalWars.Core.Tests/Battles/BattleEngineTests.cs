using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Domain.Creatures.CapabilityKind;

namespace EternalWars.Core.Tests.Battles;

public class BattleEngineTests
{
    [Fact]
    public void Round_OrdersByInitiative_ThenAttackerSide_ThenSpeed()
    {
        var battle = TestBattle.Create();
        var slowDefender = battle.Add(Defender, TestBattle.Fixed("d10", 1, initiative: 10, speed: 20), 1, 20, 0);
        var fast = battle.Add(Attacker, TestBattle.Fixed("a12", 1, initiative: 12), 1, 0, 0);
        var attackerSlow = battle.Add(Attacker, TestBattle.Fixed("a10s5", 1, initiative: 10, speed: 5), 1, 0, 4);
        var attackerQuick = battle.Add(Attacker, TestBattle.Fixed("a10s9", 1, initiative: 10, speed: 9), 1, 0, 8);

        battle.Engine.StartFighting();

        Assert.Equal(fast, battle.State.ActiveUnit);
        Assert.Equal([attackerQuick.Id, attackerSlow.Id, slowDefender.Id], battle.State.Queue);
        Assert.Equal(1, battle.State.Round);
    }

    [Fact]
    public void Wait_ActsAfterEveryone_InReverseInitiativeOrder()
    {
        var battle = TestBattle.Create();
        var first = battle.Add(Attacker, TestBattle.Fixed("i14", 1, initiative: 14), 1, 0, 0);
        var second = battle.Add(Attacker, TestBattle.Fixed("i12", 1, initiative: 12), 1, 0, 4);
        var third = battle.Add(Defender, TestBattle.Fixed("i8", 1, initiative: 8), 1, 26, 0);
        battle.Engine.StartFighting();

        Assert.Null(battle.Engine.Wait()); // i14 waits
        Assert.Equal(second, battle.State.ActiveUnit);
        Assert.Null(battle.Engine.Wait()); // i12 waits
        Assert.Equal(third, battle.State.ActiveUnit);
        Assert.Null(battle.Engine.Defend());

        Assert.Equal(second, battle.State.ActiveUnit); // lower initiative waiter first
        Assert.NotNull(battle.Engine.Wait()); // only once per round
        Assert.Null(battle.Engine.Defend());
        Assert.Equal(first, battle.State.ActiveUnit);
        Assert.Null(battle.Engine.Defend());
        Assert.Equal(2, battle.State.Round);
    }

    [Fact]
    public void Retaliation_OncePerRound_UnlessUnlimited()
    {
        var battle = TestBattle.Create();
        var a1 = battle.Add(Attacker, TestBattle.Fixed("a1", 1), 10, 0, 0);
        var a2 = battle.Add(Attacker, TestBattle.Fixed("a2", 1), 10, 4, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 100), 10, 2, 0);
        battle.StartWith(a1);

        battle.Engine.Attack(target.Id, a1.Position);
        battle.State.ActiveUnit = a2;
        battle.Engine.Attack(target.Id, a2.Position);

        var hits = battle.Events.OfType<BattleUnitAttacked>().ToList();
        Assert.Equal(
            [(a1.Id, target.Id, BattleAttackKind.Melee), (target.Id, a1.Id, BattleAttackKind.Retaliation), (a2.Id, target.Id, BattleAttackKind.Melee)],
            hits.Select(h => (h.Attacker, h.Target, h.Kind)));
    }

    [Fact]
    public void UnlimitedRetaliation_AndNoEnemyRetaliation()
    {
        var battle = TestBattle.Create();
        var a1 = battle.Add(Attacker, TestBattle.Fixed("a1", 1), 10, 0, 0);
        var devil = battle.Add(Attacker, TestBattle.Fixed("devil", 1, capabilities: new CreatureCapability(NoEnemyRetaliation)), 10, 4, 0);
        var a3 = battle.Add(Attacker, TestBattle.Fixed("a3", 1), 10, 2, 2);
        var guardian = battle.Add(Defender, TestBattle.Fixed("guardian", 1, health: 100, capabilities: new CreatureCapability(UnlimitedRetaliation)), 10, 2, 0);
        battle.StartWith(a1);

        battle.Engine.Attack(guardian.Id, a1.Position);
        battle.State.ActiveUnit = devil;
        battle.Engine.Attack(guardian.Id, devil.Position);
        battle.State.ActiveUnit = a3;
        battle.Engine.Attack(guardian.Id, a3.Position);

        var retaliations = battle.Events.OfType<BattleUnitAttacked>().Where(h => h.Kind == BattleAttackKind.Retaliation).Select(h => h.Target);
        Assert.Equal([a1.Id, a3.Id], retaliations);
    }

    [Fact]
    public void DoubleStrike_HitsAgainAfterTheRetaliation()
    {
        var battle = TestBattle.Create();
        var crusader = battle.Add(Attacker, TestBattle.Fixed("double", 1, capabilities: new CreatureCapability(DoubleStrike)), 10, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 100), 10, 2, 0);
        battle.StartWith(crusader);

        battle.Engine.Attack(target.Id, null);

        Assert.Equal(
            [BattleAttackKind.Melee, BattleAttackKind.Retaliation, BattleAttackKind.Melee],
            battle.Events.OfType<BattleUnitAttacked>().Select(h => h.Kind));
    }

    [Fact]
    public void Breath_HitsTheAreaBehindTheTarget_FriendsIncluded()
    {
        var battle = TestBattle.Create();
        var dragon = battle.Add(Attacker, TestBattle.Fixed("dragon", 5, size: CreatureSize.Large, capabilities: new CreatureCapability(Breath)), 1, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 100), 1, 4, 1);
        var behind = battle.Add(Attacker, TestBattle.Fixed("friend", 1, health: 100), 1, 6, 1); // in the 2×2 area right of the target
        var aside = battle.Add(Defender, TestBattle.Fixed("aside", 1, health: 100), 1, 4, 6);
        battle.StartWith(dragon);

        battle.Engine.Attack(target.Id, dragon.Position);

        var hits = battle.Events.OfType<BattleUnitAttacked>().Where(h => h.Attacker == dragon.Id).ToList();
        Assert.Equal([(target.Id, BattleAttackKind.Melee), (behind.Id, BattleAttackKind.Area)], hits.Select(h => (h.Target, h.Kind)));
        Assert.DoesNotContain(hits, h => h.Target == aside.Id);
    }

    [Fact]
    public void Cleave_HitsUpToTwoEnemiesNextToAttackerAndTarget_MultiHeadHitsAllAround()
    {
        var battle = TestBattle.Create();
        var cerberus = battle.Add(Attacker, TestBattle.Fixed("cerberus", 1, capabilities: new CreatureCapability(Cleave)), 10, 4, 4);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 100), 1, 6, 4);
        var flank = battle.Add(Defender, TestBattle.Fixed("flank", 1, health: 100), 1, 6, 6); // next to both
        var rear = battle.Add(Defender, TestBattle.Fixed("rear", 1, health: 100), 1, 2, 4); // next to the attacker only
        battle.StartWith(cerberus);

        battle.Engine.Attack(target.Id, cerberus.Position);

        var cleaved = battle.Events.OfType<BattleUnitAttacked>().Where(h => h.Kind == BattleAttackKind.Area).Select(h => h.Target);
        Assert.Equal([flank.Id], cleaved);

        var hydra = TestBattle.Create();
        var heads = hydra.Add(Attacker, TestBattle.Fixed("hydra", 1, capabilities: new CreatureCapability(MultiHead)), 10, 4, 4);
        var t1 = hydra.Add(Defender, TestBattle.Fixed("t1", 1, health: 100), 1, 6, 4);
        var t2 = hydra.Add(Defender, TestBattle.Fixed("t2", 1, health: 100), 1, 6, 6);
        var t3 = hydra.Add(Defender, TestBattle.Fixed("t3", 1, health: 100), 1, 2, 4);
        hydra.StartWith(heads);

        hydra.Engine.Attack(t1.Id, heads.Position);

        Assert.Equal([t2.Id, t3.Id], hydra.Events.OfType<BattleUnitAttacked>().Where(h => h.Kind == BattleAttackKind.Area).Select(h => h.Target));
        Assert.Equal([t1.Id, t2.Id, t3.Id], hydra.Events.OfType<BattleUnitAttacked>().Where(h => h.Attacker == heads.Id).Select(h => h.Target));
        Assert.DoesNotContain(rear.Id, cleaved);
    }

    [Fact]
    public void Damage_KillsWholeCreaturesAndWoundsTheTopOne()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 23), 1, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 10), 5, 2, 0);
        battle.StartWith(attacker);

        battle.Engine.Attack(target.Id, null);

        Assert.Equal((3, 7), (target.Count, target.TopHealth));
        Assert.Contains(new BattleUnitAttacked(attacker.Id, target.Id, BattleAttackKind.Melee, 23, 2, 3), battle.Events);
    }

    [Fact]
    public void LastDefenderDying_EndsTheBattle()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 100), 1, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("t", 1, health: 10), 3, 2, 0);
        battle.StartWith(attacker);

        battle.Engine.Attack(target.Id, null);

        Assert.Equal(BattlePhase.Finished, battle.State.Phase);
        Assert.Equal(Attacker, battle.State.Winner);
        Assert.Contains(new BattleUnitDied(target.Id), battle.Events);
        Assert.Null(battle.State.ActiveUnit);
    }

    [Fact]
    public void Shooting_UsesAmmunition_AndIsRefusedWhenEngaged()
    {
        var battle = TestBattle.Create();
        var archer = battle.Add(Attacker, BuiltInCreatures.Archer, 10, 0, 0);
        var target = battle.Add(Defender, BuiltInCreatures.Footman, 50, 20, 0);
        battle.StartWith(archer);

        Assert.Null(battle.Engine.Shoot(target.Id));
        Assert.Equal(11, archer.ShotsLeft);

        battle.Add(Defender, BuiltInCreatures.Pikeman, 5, 2, 0);
        battle.State.ActiveUnit = archer;
        Assert.NotNull(battle.Engine.Shoot(target.Id));
        Assert.Equal(11, archer.ShotsLeft);
    }

    [Fact]
    public void Attack_FromAPlacementOutOfReach_IsRefusedWithoutChanges()
    {
        var battle = TestBattle.Create();
        var footman = battle.Add(Attacker, BuiltInCreatures.Footman, 10, 0, 0);
        var target = battle.Add(Defender, BuiltInCreatures.Footman, 10, 26, 0);
        battle.StartWith(footman);

        Assert.NotNull(battle.Engine.Attack(target.Id, null)); // 24 cells away, speed 10
        Assert.Equal(new CellPosition(0, 0), footman.Position);
        Assert.Equal(footman, battle.State.ActiveUnit);
        Assert.Empty(battle.Events.OfType<BattleUnitAttacked>());
    }

    [Fact]
    public void BattleAi_IsDeterministic_AndFinishesTheFight()
    {
        static (BattleSide? Winner, string Digest) Fight(ulong seed)
        {
            var battle = TestBattle.Create(seed: seed);
            var a = new List<BattleStackSpec>
            {
                new(CreatureCatalog.Default.Get(BuiltInCreatures.Pikeman), 30, 0),
                new(CreatureCatalog.Default.Get(BuiltInCreatures.Archer), 15, 1),
            };
            var d = new List<BattleStackSpec> { new(CreatureCatalog.Default.Get(BuiltInCreatures.HellHound), 12, 0) };
            BattleSetup.Deploy(battle.State, a, d);
            battle.Engine.StartFighting();
            while (battle.State.Phase == BattlePhase.Fighting)
            {
                BattleAi.Act(battle.State, battle.Engine);
            }

            return (battle.State.Winner, string.Join(";", battle.Events.Select(e => e.ToString())));
        }

        var first = Fight(3);
        var again = Fight(3);

        Assert.NotNull(first.Winner);
        Assert.Equal(first, again);
    }
}
