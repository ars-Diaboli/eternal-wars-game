using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>War machines (ADR-056): ownership, deployment, their rules in battle, losses and the blacksmith.</summary>
public class WarMachineTests
{
    [Fact]
    public void Skills_AreAGroupOfTen_AndEveryHeroStartsWithItsTownsOffensiveMachine()
    {
        Assert.Equal(
            [BuiltInSkills.Ballistics, BuiltInSkills.Healing, BuiltInSkills.AmmoSupply],
            BuiltInSkills.SkillsOf("machines").Select(s => s.Id));
        Assert.Equal(WarMachineKind.Cannon, WarMachines.OffensiveFor(TownIds.Inferno));
        Assert.Equal(WarMachineKind.Glaivethrower, WarMachines.OffensiveFor(TownIds.Dungeon));
        Assert.Equal(WarMachineKind.Ballista, WarMachines.OffensiveFor(null));
        Assert.Equal(2, WarMachines.OffensiveByTown.Values.Count(k => k == WarMachineKind.Catapult));

        var hero = Hero.Create(new HeroId(1), Blue, default, null, HeroCatalog.Default.Get("haven.ansel"));
        Assert.Equal([WarMachineSlot.Offensive], hero.WarMachines);
        Assert.All(WarMachines.All, d => d.Validate());
    }

    [Fact]
    public void OffensiveMachine_JoinsRegularBattles_OnlyFromAdvancedBallistics_TentAndCartWhenOwned()
    {
        var (session, _) = HeroBattle(ballistics: SkillLevel.Basic, extra: []);
        Assert.DoesNotContain(session.State.Battle!.Units, u => u.IsMachine);

        var (fielded, attacker) = HeroBattle(ballistics: SkillLevel.Advanced, extra: [WarMachineSlot.Tent, WarMachineSlot.Cart]);
        var battle = fielded.State.Battle!;
        var machines = battle.Units.Where(u => u.Side == Attacker && u.IsMachine).ToDictionary(u => u.Machine!.Value);

        Assert.Equal([WarMachineKind.Ballista, WarMachineKind.HealingTent, WarMachineKind.AmmoCart], machines.Keys.Order());
        Assert.All(machines.Values, m => Assert.Equal(0, m.Position.X)); // a column of their own at the back
        Assert.Equal(1, machines[WarMachineKind.HealingTent].Position.Y);
        Assert.Equal(BattleState.Height - 2 - 1, machines[WarMachineKind.AmmoCart].Position.Y);
        Assert.All(battle.Units.Where(u => u.Side == Attacker && !u.IsMachine), u => Assert.True(u.Position.X >= 4)); // army in front
        Assert.Equal(WarMachineKind.Ballista, Assert.Single(battle.Units, u => u.Machine is not null && WarMachines.TakesTurns(u.Machine.Value)).Machine);
        Assert.Equal(attacker, battle.Attacker.Hero);
    }

    [Fact]
    public void Machines_NeverMoveFightInMeleeOrRetaliate_ButTheOffensiveOneShootsWithoutRunningOut()
    {
        var battle = TestBattle.Create();
        var ballista = AddMachine(battle, Attacker, WarMachineKind.Ballista, 0, 10);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 500), 1, 20, 10);
        battle.StartWith(ballista);

        Assert.NotNull(battle.Engine.Move(new CellPosition(4, 10)));
        Assert.NotNull(battle.Engine.Attack(enemy.Id, null));
        Assert.Null(battle.Engine.Shoot(enemy.Id));
        Assert.Equal(1, ballista.ShotsLeft);

        // Attacked in melee, the machine does not hit back.
        enemy.Position = new CellPosition(3, 10);
        battle.State.ActiveUnit = enemy;
        Assert.Null(battle.Engine.Attack(ballista.Id, enemy.Position));
        Assert.DoesNotContain(battle.Events.OfType<BattleUnitAttacked>(), a => a.Attacker == ballista.Id && a.Kind == BattleAttackKind.Retaliation);
    }

    [Theory]
    [InlineData(SkillLevel.None, 0)]
    [InlineData(SkillLevel.Advanced, 0)]
    [InlineData(SkillLevel.Expert, 25)]
    [InlineData(SkillLevel.Master, 50)]
    public void Ballistics_RaisesTheMachinesDamage(SkillLevel ballistics, int percent)
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { Ballistics = ballistics });
        var ballista = AddMachine(battle, Attacker, WarMachineKind.Ballista, 0, 10);
        var enemy = battle.Add(Defender, TestBattle.Fixed("target", 1, defense: 10), 1, 20, 10); // Attack 10 = Defense 10

        Assert.Equal(40 * (100 + percent) / 100, DamageCalculator.Damage(battle.State, ballista, enemy, new HitModifiers(40, IsRanged: true)));
    }

    [Theory]
    [InlineData(SkillLevel.None, 10)]
    [InlineData(SkillLevel.Basic, 20)]
    [InlineData(SkillLevel.Master, 50)]
    public void HealingTent_RestoresTheTopCreatureOfEveryOwnStack_EachRound(SkillLevel healing, int amount)
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { Healing = healing });
        AddMachine(battle, Attacker, WarMachineKind.HealingTent, 0, 1);
        var wounded = battle.Add(Attacker, TestBattle.Fixed("wounded", 1, health: 100), 3, 4, 10);
        var healthy = battle.Add(Attacker, TestBattle.Fixed("healthy", 1, health: 100), 3, 4, 14);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 100), 1, 26, 10);
        wounded.TopHealth = 30;
        enemy.TopHealth = 30;

        battle.Engine.StartFighting();

        Assert.Equal(30 + amount, wounded.TopHealth);
        Assert.Equal(100, healthy.TopHealth);
        Assert.Equal(30, enemy.TopHealth); // only its own side
        Assert.Equal(new BattleUnitHealed(wounded.Id, amount, 30 + amount), Assert.Single(battle.Events.OfType<BattleUnitHealed>()));
        Assert.DoesNotContain(battle.State.Queue.Append(battle.State.ActiveUnit!.Id), id => battle.State.GetUnit(id).IsMachine); // no turns
    }

    [Theory]
    [InlineData(SkillLevel.None, 15)] // 12 shots + 25 %
    [InlineData(SkillLevel.Basic, 18)] // + 50 %
    public void AmmoCart_AddsShotsAtTheStart(SkillLevel ammo, int shots)
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { AmmoSupply = ammo });
        var archer = CreatureCatalog.Default.Get(BuiltInCreatures.Archer);
        BattleSetup.Deploy(battle.State, [new BattleStackSpec(archer, 5, 0), BattleStackSpec.Of(WarMachineKind.AmmoCart)], [new BattleStackSpec(archer, 5, 0)]);

        Assert.Equal(12, archer.Shots);
        Assert.Equal(shots, battle.State.Units.First(u => u.Side == Attacker && !u.IsMachine).ShotsLeft);
        Assert.Equal(12, battle.State.Units.First(u => u.Side == Defender).ShotsLeft);
    }

    [Fact]
    public void AmmoCart_WithAdvancedAmmoSupply_GivesUnlimitedShots_WhileItStands()
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { AmmoSupply = SkillLevel.Advanced });
        var cart = AddMachine(battle, Attacker, WarMachineKind.AmmoCart, 0, 20);
        var shooter = battle.Add(Attacker, TestBattle.Fixed("shooter", 1, shots: 2), 1, 4, 10);
        var target = battle.Add(Defender, TestBattle.Fixed("target", 1, health: 10_000), 1, 26, 10);
        battle.StartWith(shooter);

        Assert.Null(battle.Engine.Shoot(target.Id));
        Assert.Equal(2, shooter.ShotsLeft);

        cart.Count = 0; // destroyed
        Assert.False(battle.State.HasUnlimitedShots(shooter));
    }

    [Fact]
    public void ASideWithOnlyMachinesLeft_HasLost()
    {
        var battle = TestBattle.Create();
        AddMachine(battle, Attacker, WarMachineKind.Ballista, 0, 10);
        var last = battle.Add(Attacker, TestBattle.Fixed("last", 1, health: 1), 1, 4, 0);
        var killer = battle.Add(Defender, TestBattle.Fixed("killer", 50), 10, 6, 0);
        battle.StartWith(killer);

        Assert.Null(battle.Engine.Attack(last.Id, killer.Position));

        Assert.Equal(BattlePhase.Finished, battle.State.Phase);
        Assert.Equal(Defender, battle.State.Winner);
    }

    [Fact]
    public void DestroyedMachines_AreLostAfterTheBattle()
    {
        var (session, attacker) = HeroBattle(ballistics: SkillLevel.Advanced, extra: [WarMachineSlot.Tent]);
        var hero = session.State.GetHero(attacker!.Value);
        var lost = new List<WarMachineLost>();
        session.Events.Subscribe<WarMachineLost>(lost.Add);
        session.State.Battle!.Units.First(u => u.Machine == WarMachineKind.HealingTent).Count = 0;

        session.AutoResolve();

        Assert.Equal([WarMachineSlot.Offensive], hero.WarMachines.Order());
        Assert.Equal(new WarMachineLost(hero.Id, WarMachineSlot.Tent), Assert.Single(lost));
    }

    [Fact]
    public void Blacksmith_SellsMachinesTheVisitingHeroLacks()
    {
        var state = Create("............", "............", "............", "............", "............", "............", "............", "...A........", "...........B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new(Gold: 2000);
        var hero = state.GetHero(A);

        Assert.Equal($"{town.Name} has no blacksmith.", TownRules.CheckBuyWarMachine(state, town, WarMachineSlot.Tent));
        town.SetLevel(BuiltInBuildings.Blacksmith, 1);
        Assert.StartsWith("No own hero", TownRules.CheckBuyWarMachine(state, town, WarMachineSlot.Tent));

        session.Step(A, Direction.North); // onto the entrance
        Assert.False(session.Execute(new BuyWarMachineCommand(Blue, town.Id, WarMachineSlot.Offensive)).IsAccepted); // owned already
        session.Execute(new BuyWarMachineCommand(Blue, town.Id, WarMachineSlot.Tent)).ThrowIfRejected();
        session.Execute(new BuyWarMachineCommand(Blue, town.Id, WarMachineSlot.Cart)).ThrowIfRejected();

        Assert.Equal([WarMachineSlot.Offensive, WarMachineSlot.Tent, WarMachineSlot.Cart], hero.WarMachines.Order());
        Assert.Equal(250, state.GetPlayer(Blue).Resources.Gold);
        Assert.Equal("Ballista", TownRules.WarMachineName(state, hero, WarMachineSlot.Offensive));
    }

    [Fact]
    public void HeroMachines_RoundTripThroughTheMapFormat_AndTheEditorUndoes()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        editor.PlaceHero(new PlayerId(0), new MapPosition(2, 2), "Orrin", out var id);
        editor.PlaceHero(new PlayerId(0), new MapPosition(4, 2), "Plain", out _);
        Assert.Equal(EditResult.Ok, editor.SetHeroWarMachines(id, new HashSet<WarMachineSlot> { WarMachineSlot.Cart }));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal([WarMachineSlot.Cart], restored.GetHero(id).WarMachines);
        Assert.Equal([WarMachineSlot.Offensive], restored.GetHero(new HeroId(2)).WarMachines); // omitted = starting set
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"warMachines\""));

        editor.Undo();
        Assert.Equal([WarMachineSlot.Offensive], editor.Scenario.GetHero(id).WarMachines);
    }

    private static BattleUnit AddMachine(TestBattle battle, BattleSide side, WarMachineKind kind, int x, int y)
    {
        var unit = new BattleUnit(battle.State.Units.Count + 1, side, WarMachines.Definition(kind), 1, -1, new CellPosition(x, y), kind);
        battle.State.MutableUnits.Add(unit);
        return unit;
    }

    /// <summary>Blue's hero A (10 footmen) attacks Red's hero B; returns the session with the battle running.</summary>
    private static (GameSession Session, HeroId? Attacker) HeroBattle(SkillLevel ballistics, WarMachineSlot[] extra)
    {
        var state = Create("AB", "..");
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.Ballistics, ballistics);
        foreach (var slot in extra)
        {
            hero.AddWarMachine(slot);
        }

        var session = Start(state);
        session.Step(A, Direction.East);
        Assert.NotNull(session.State.Battle);
        return (session, A);
    }
}
