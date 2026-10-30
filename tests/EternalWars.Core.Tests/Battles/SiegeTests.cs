using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Sieges (ADR-099, ADR-106): the fortifications, walls and gate, the moat, the keep and towers, the siege engine and the battle AI.</summary>
public class SiegeTests
{
    private static readonly SiegeSpec Fort = new(1, 5);
    private static readonly SiegeSpec Citadel = new(2, 5);
    private static readonly SiegeSpec Castle = new(3, 5);

    /// <summary>A town at (0, 0) with its entrance at (3, 6); hero A just below it, B far away.</summary>
    private static readonly string[] Rows =
    [
        .. Enumerable.Repeat("............", 7),
        "...A........",
        "............",
        "...........B",
    ];

    [Fact]
    public void Fortifications_GrowWithTheFortLevel()
    {
        var fort = SiegeRules.Create(Fort);
        Assert.Equal(
            [(SiegeStructureKind.Wall, 2), (SiegeStructureKind.Wall, 2), (SiegeStructureKind.Wall, 2), (SiegeStructureKind.Wall, 2), (SiegeStructureKind.Gate, 1)],
            fort.Structures.Select(s => (s.Kind, s.Health)));
        Assert.Empty(fort.Moat);

        // The wall line covers the whole column: four sections and the 4-cell gate in the middle.
        var cells = fort.Structures.SelectMany(s => s.Rect.Cells()).ToList();
        Assert.All(cells, c => Assert.Equal(SiegeRules.WallX, c.X));
        Assert.Equal(Enumerable.Range(0, BattleState.Height), cells.Select(c => c.Y).Order());
        Assert.Equal(new CellRect(SiegeRules.WallX, 10, 1, 4), fort.Structures.Single(s => s.Kind == SiegeStructureKind.Gate).Rect);

        var citadel = SiegeRules.Create(Citadel);
        Assert.Equal(SiegeRules.TowerHealth, citadel.Structures.Single(s => s.Kind == SiegeStructureKind.Keep).Health);
        Assert.Equal(2, citadel.Moat.Count);
        Assert.DoesNotContain(citadel.Structures, s => s.Kind == SiegeStructureKind.Tower);

        var castle = SiegeRules.Create(Castle);
        Assert.Equal(2, castle.Structures.Count(s => s.Kind == SiegeStructureKind.Tower));
        Assert.All(castle.Structures.Where(s => s.Kind == SiegeStructureKind.Wall), s => Assert.Equal(3, s.Health));
    }

    [Fact]
    public void ATownIsBesieged_FromItsFort_AndItsBuildingsCountEveryLevel()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);

        Assert.Equal(new SiegeSpec(1, 3), SiegeRules.SpecOf(town)); // town hall, fort and tavern
        town.SetLevel(BuiltInBuildings.Fort, 2);
        town.SetDwelling(BuiltInCreatures.Footman, 2);
        Assert.Equal(new SiegeSpec(2, 6), SiegeRules.SpecOf(town));

        town.SetLevel(BuiltInBuildings.Fort, 0);
        Assert.Null(SiegeRules.SpecOf(town));
    }

    [Fact]
    public void Walls_BlockBothSides_TheGateOnlyTheAttackers_FlyersCrossThem()
    {
        var battle = TestBattle.Create(siege: Fort);
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 1), 1, 18, 11);

        Assert.DoesNotContain(BattleGrid.Reachable(battle.State, walker), r => r.Key.X > SiegeRules.WallX - 2);

        var defender = battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, 20, 8);
        Assert.Contains(new CellPosition(SiegeRules.WallX + 1, 11), BattleGrid.Reachable(battle.State, defender).Keys); // through the gate
        Assert.DoesNotContain(new CellPosition(SiegeRules.WallX + 1, 1), BattleGrid.Reachable(battle.State, defender).Keys);

        var flyer = battle.Add(Attacker, TestBattle.Fixed("flyer", 1, capabilities: new CreatureCapability(CapabilityKind.Flying)), 1, 18, 16);
        Assert.Contains(new CellPosition(SiegeRules.WallX + 2, 16), BattleGrid.Reachable(battle.State, flyer).Keys);
    }

    [Fact]
    public void ADestroyedSection_IsAGapTheAttackersWalkThrough()
    {
        var battle = TestBattle.Create(siege: Fort);
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 1), 1, 18, 1);
        var target = new CellPosition(SiegeRules.WallX + 1, 1);
        Assert.DoesNotContain(target, BattleGrid.Reachable(battle.State, walker).Keys);

        battle.State.Siege!.Find(1)!.Health = 0;

        Assert.Contains(target, BattleGrid.Reachable(battle.State, walker).Keys);
    }

    [Fact]
    public void AnAttackerWalkingIntoTheMoat_StopsThere_AndTakesItsDamage()
    {
        var battle = TestBattle.Create(siege: Citadel);
        var siege = battle.State.Siege!;
        siege.Find(3)!.Health = 0; // a breach in rows 14–18
        siege.Structures.Single(s => s.Kind == SiegeStructureKind.Keep).Health = 0; // no keep shots in the way
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 1, health: 10), 10, 17, 15);
        battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, 33, 2);

        // The first placement that touches the moat is as far as it gets this turn.
        var edge = new CellPosition(SiegeRules.WallX - SiegeRules.MoatColumns - 1, 15);
        var reach = BattleGrid.Reachable(battle.State, walker);
        Assert.Contains(edge, reach.Keys);
        Assert.DoesNotContain(new CellPosition(SiegeRules.WallX - 2, 15), reach.Keys); // not on through it
        Assert.DoesNotContain(new CellPosition(SiegeRules.WallX + 1, 15), reach.Keys);

        battle.StartWith(walker);
        Assert.Null(battle.Engine.Move(edge));

        Assert.Equal(40, siege.MoatDamage); // 2 × (10 + 2 × 5)
        Assert.Equal(new BattleUnitSiegeDamaged(walker.Id, SiegeDamageSource.Moat, null, 40, 4, 6), Assert.Single(battle.Events.OfType<BattleUnitSiegeDamaged>()));

        // Once in the moat it moves on freely.
        Assert.Contains(new CellPosition(SiegeRules.WallX + 1, 15), BattleGrid.Reachable(battle.State, walker).Keys);
    }

    [Fact]
    public void TheMoat_DoesNotHurtTheDefenders_OrAFlyerThatPassesOverIt()
    {
        var battle = TestBattle.Create(siege: Citadel);
        battle.State.Siege!.Structures.Single(s => s.Kind == SiegeStructureKind.Keep).Health = 0;
        var flyer = battle.Add(Attacker, TestBattle.Fixed("flyer", 1, capabilities: new CreatureCapability(CapabilityKind.Flying)), 1, 16, 2);
        var defender = battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, SiegeRules.WallX + 2, 11);

        battle.StartWith(flyer);
        Assert.Null(battle.Engine.Move(new CellPosition(SiegeRules.WallX + 2, 2)));
        battle.State.ActiveUnit = defender;
        Assert.Null(battle.Engine.Move(new CellPosition(SiegeRules.WallX - 2, 11))); // out through the gate
        battle.State.ActiveUnit = defender;
        Assert.Null(battle.Engine.Move(new CellPosition(SiegeRules.WallX - 2, 15))); // into the moat

        Assert.Empty(battle.Events.OfType<BattleUnitSiegeDamaged>());
    }

    [Fact]
    public void AttackersShootingAcrossAStandingWall_TakeItsHalfCover_TheDefendersShootOutFreely()
    {
        var battle = TestBattle.Create(siege: Fort);
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 5), 1, 10, 2);
        var defender = battle.Add(Defender, TestBattle.Fixed("defender", 1, shots: 5), 1, 30, 2);

        Assert.Equal(1 + 4, BattleGrid.RangedPenaltyEighths(battle.State, archer, defender)); // 18 cells apart and the wall
        Assert.Equal(1, BattleGrid.RangedPenaltyEighths(battle.State, defender, archer));

        battle.State.Siege!.Find(1)!.Health = 0;
        Assert.Equal(1, BattleGrid.RangedPenaltyEighths(battle.State, archer, defender));
    }

    [Fact]
    public void TheKeepAndTowers_ShootOnceARound_AtTheAttackers()
    {
        var battle = TestBattle.Create(siege: Castle);
        var siege = battle.State.Siege!;
        var keep = siege.Structures.Single(s => s.Kind == SiegeStructureKind.Keep);
        Assert.Equal(40, siege.ShotDamage(keep)); // double 10 + 2 × 5
        Assert.All(siege.Structures.Where(s => s.Kind == SiegeStructureKind.Tower), t => Assert.Equal(20, siege.ShotDamage(t)));

        var attacker = battle.Add(Attacker, TestBattle.Fixed("attacker", 1, health: 10), 20, 2, 2);
        var defender = battle.Add(Defender, TestBattle.Fixed("defender", 1, health: 10), 20, 30, 2);

        battle.Engine.StartFighting();

        var shots = battle.Events.OfType<BattleUnitSiegeDamaged>().ToList();
        Assert.Equal([keep.Id, 7, 8], shots.Select(s => s.Structure!.Value));
        Assert.All(shots, s => Assert.Equal((attacker.Id, SiegeDamageSource.Tower), (s.Unit, s.Source)));
        Assert.Equal(12, attacker.Count); // 80 damage
        Assert.Equal(20, defender.Count);
    }

    [Fact]
    public void TheSiegeEngine_AimsAtTheWalls_ThenTheGate_ThenTheKeepAndTowers()
    {
        var siege = SiegeRules.Create(Castle);
        Assert.Equal(1, siege.AutoTarget()!.Id);
        foreach (var wall in siege.Structures.Where(s => s.Kind == SiegeStructureKind.Wall))
        {
            wall.Health = 0;
        }

        Assert.Equal(SiegeStructureKind.Gate, siege.AutoTarget()!.Kind);
        siege.Find(5)!.Health = 0;
        Assert.Equal(SiegeStructureKind.Keep, siege.AutoTarget()!.Kind);
        siege.Structures.Single(s => s.Kind == SiegeStructureKind.Keep).Health = 0;
        Assert.Equal(7, siege.AutoTarget()!.Id);
        siege.Find(7)!.Health = 0;
        siege.Find(8)!.Health = 0;
        Assert.Null(siege.AutoTarget());
    }

    [Fact]
    public void FromAdvancedBallistics_ADestroyedStructure_IsFollowedByASecondShot()
    {
        var battle = TestBattle.Create(siege: Fort, attackerBonuses: HeroBattleBonuses.None with { Ballistics = SkillLevel.Expert });
        var ballista = AddMachine(battle, WarMachineKind.Ballista, 0, 10);
        battle.Add(Attacker, TestBattle.Fixed("attacker", 1), 1, 2, 2);
        battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, 30, 2);
        battle.StartWith(ballista);

        Assert.Null(battle.Engine.ShootStructure(5)); // the gate: 1 hit point, every shot hits at Expert

        var shots = battle.Events.OfType<SiegeStructureShot>().ToList();
        Assert.Equal(new SiegeStructureShot(ballista.Id, 5, true, 1, 0), shots[0]);
        Assert.Equal((1, true), (shots[1].Structure, shots[1].Hit)); // then the next automatic target
        Assert.Equal(2, shots.Count);
        Assert.False(battle.State.Siege!.Find(5)!.IsStanding);
    }

    [Fact]
    public void BelowAdvancedBallistics_TheSiegeEngine_FiresOnlyAtFortifications_AndNothingElseDoes()
    {
        var battle = TestBattle.Create(siege: Fort, attackerBonuses: HeroBattleBonuses.None with { Ballistics = SkillLevel.Basic });
        var ballista = AddMachine(battle, WarMachineKind.Ballista, 0, 10);
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 5), 1, 2, 2);
        var defender = battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, 30, 2);
        battle.StartWith(ballista);

        Assert.False(BattleGrid.CanShoot(battle.State, ballista, defender, out _));
        Assert.Null(battle.Engine.ShootStructure(2));
        Assert.Single(battle.Events.OfType<SiegeStructureShot>()); // no second shot below Advanced

        battle.State.ActiveUnit = archer;
        Assert.NotNull(battle.Engine.ShootStructure(1));
    }

    [Fact]
    public void AGarrisonBehindAFort_IsBesieged_AndTheAttackersEngineFiresByItself()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 5));
        var session = Start(state);
        var shots = new List<SiegeStructureShot>();
        session.Events.Subscribe<SiegeStructureShot>(shots.Add);
        var started = new List<BattleStarted>();
        session.Events.Subscribe<BattleStarted>(started.Add);

        session.Step(A, Direction.North);

        var battle = Assert.IsType<BattleState>(session.State.Battle);
        Assert.Equal((BattleState.SiegeWidth, 1), (battle.Width, battle.Siege!.FortLevel));
        Assert.Equal(5, Assert.Single(started).Siege!.Structures.Count);
        var ballista = Assert.Single(battle.Units, u => u.Machine == WarMachineKind.Ballista); // no Ballistics needed in a siege
        Assert.Equal(Attacker, ballista.Side);

        // Both sides only defend until the engine has had its turn; nobody commands it.
        for (var turn = 0; turn < 20 && shots.Count == 0; turn++)
        {
            var active = battle.ActiveUnit!;
            Assert.False(active.IsMachine);
            Assert.True(session.Execute(new BattleDefendCommand(battle.Participant(active.Side).Owner!.Value)).IsAccepted);
        }

        Assert.Equal(1, shots[0].Structure);
    }

    [Fact]
    public void WithBasicBallistics_ThePlayerAimsTheEngine()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 5));
        state.GetHero(A).Skills.Set(BuiltInSkills.Ballistics, SkillLevel.Basic);
        var session = Start(state);
        session.Step(A, Direction.North);
        var battle = session.State.Battle!;

        for (var turn = 0; turn < 20 && battle.ActiveUnit is { IsMachine: false } active; turn++)
        {
            session.Execute(new BattleDefendCommand(battle.Participant(active.Side).Owner!.Value));
        }

        Assert.True(battle.ActiveUnit!.IsMachine);
        Assert.True(session.Execute(new BattleShootStructureCommand(Blue, 5)).IsAccepted); // the gate, though walls stand
    }

    [Fact]
    public void PointingAtAStandingStructure_AimsTheSiegeEngine_OnTheWallLineOrOffTheField()
    {
        var battle = TestBattle.Create(siege: Castle, attackerBonuses: HeroBattleBonuses.None with { Ballistics = SkillLevel.Basic });
        var ballista = AddMachine(battle, WarMachineKind.Ballista, 0, 10);
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 5), 20, 2, 2); // lasts through the towers' shots
        battle.Add(Defender, TestBattle.Fixed("defender", 1), 1, 30, 2);
        battle.StartWith(ballista);

        Assert.Equal(new ShootStructureAction(5), BattleTargeting.ActionAt(battle.State, new CellPosition(SiegeRules.WallX, 11)));
        Assert.Equal(new ShootStructureAction(7), BattleTargeting.ActionAt(battle.State, new CellPosition(SiegeRules.WallX, -1)));
        battle.State.Siege!.Find(5)!.Health = 0;
        Assert.Null(BattleTargeting.ActionAt(battle.State, new CellPosition(SiegeRules.WallX, 11)));

        battle.State.ActiveUnit = archer;
        Assert.IsNotType<ShootStructureAction>(BattleTargeting.ActionAt(battle.State, new CellPosition(SiegeRules.WallX, 2)));
    }

    [Fact]
    public void ATownWithoutAFort_HasAnOrdinaryBattle()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.SetLevel(BuiltInBuildings.Fort, 0);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 5));
        var session = Start(state);

        session.Step(A, Direction.North);

        var battle = session.State.Battle!;
        Assert.Null(battle.Siege);
        Assert.Equal(BattleState.DefaultWidth, battle.Width);
        Assert.DoesNotContain(battle.Units, u => u.IsMachine);
    }

    [Fact]
    public void AHeroInItsOwnTown_DefendsItBehindTheWalls()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.SetLevel(BuiltInBuildings.Fort, 3);
        state.GetHero(B).Position = town.VisitPosition;
        var session = Start(state);

        session.Step(A, Direction.North);

        var battle = session.State.Battle!;
        Assert.Equal(B, battle.Defender.Hero);
        Assert.Equal(3, battle.Siege!.FortLevel);
        Assert.All(battle.LivingArmyOf(Defender), u => Assert.True(u.Position.X > SiegeRules.WallX));
        Assert.All(battle.Obstacles, o => Assert.True(o.Rect.Right < SiegeRules.WallX - SiegeRules.MoatColumns));
    }

    [Fact]
    public void TheBattleAi_HoldsTheWalls_AndWaitsOutsideTheMoatWhileTheyStand()
    {
        var battle = TestBattle.Create(siege: Citadel);
        battle.State.Siege!.Structures.Single(s => s.Kind == SiegeStructureKind.Keep).Health = 0;
        var attacker = battle.Add(Attacker, TestBattle.Fixed("attacker", 1, speed: 20), 1, 2, 2);
        var defender = battle.Add(Defender, TestBattle.Fixed("defender", 1, speed: 20), 1, 30, 2);

        battle.StartWith(defender);
        BattleAi.Act(battle.State, battle.Engine);
        Assert.Equal(new CellPosition(30, 2), defender.Position); // no enemy in reach: it stays behind the walls

        battle.State.ActiveUnit = attacker;
        BattleAi.Act(battle.State, battle.Engine);
        Assert.False(battle.State.Siege.InMoat(attacker.Rect)); // no way in: up to the moat, not into it
        Assert.Equal(SiegeRules.WallX - SiegeRules.MoatColumns - attacker.Width, attacker.Position.X);

        battle.State.Siege.Find(1)!.Health = 0;
        battle.State.ActiveUnit = attacker;
        BattleAi.Act(battle.State, battle.Engine);
        Assert.True(battle.State.Siege.InMoat(attacker.Rect)); // towards the breach, through the moat
    }

    [Fact]
    public void QuickCombat_PlaysASiegeToTheEnd()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.SetLevel(BuiltInBuildings.Fort, 2);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 40));
        var session = Start(state);
        var shots = new List<SiegeStructureShot>();
        session.Events.Subscribe<SiegeStructureShot>(shots.Add);
        var hurt = new List<BattleUnitSiegeDamaged>();
        session.Events.Subscribe<BattleUnitSiegeDamaged>(hurt.Add);
        session.Step(A, Direction.North);

        session.AutoResolve();

        Assert.Null(session.State.Battle);
        Assert.Equal(Blue, town.Owner);
        Assert.NotEmpty(shots);
        Assert.Contains(hurt, h => h.Source == SiegeDamageSource.Tower);
    }

    private static BattleUnit AddMachine(TestBattle battle, WarMachineKind kind, int x, int y)
    {
        var unit = new BattleUnit(battle.State.Units.Count + 1, Attacker, WarMachines.Definition(kind), 1, -1, new CellPosition(x, y), kind);
        battle.State.MutableUnits.Add(unit);
        return unit;
    }
}
