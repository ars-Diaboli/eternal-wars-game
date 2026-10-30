using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Garrison heroes (ADR-112): a hero in a town's garrison is off the map, leads the garrison in a siege and keeps its war machines there.</summary>
public class GarrisonHeroTests
{
    /// <summary>A Haven town at (0, 0) has its entrance at (3, 6), where hero A stands; Red's hero B waits two tiles below.</summary>
    private static readonly string[] Rows =
    [
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "...A........",
        "............",
        "...B........",
        "............",
    ];

    private static (GameSession Session, Town Town, Hero Hero) Setup(Action<GameState>? before = null)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 5));
        var hero = state.GetHero(A);
        hero.Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));
        hero.Army.Set(1, new CreatureStack(BuiltInCreatures.Archer, 4));
        before?.Invoke(state);
        return (Start(state), town, hero);
    }

    [Fact]
    public void EnteringTheGarrison_MergesTheArmy_AndTakesTheHeroOffTheMap()
    {
        var (session, town, hero) = Setup();
        var state = session.State;
        var entered = new List<HeroEnteredGarrison>();
        session.Events.Subscribe<HeroEnteredGarrison>(entered.Add);

        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();

        Assert.Same(hero, town.GarrisonHero);
        Assert.Equal(new CreatureStack(BuiltInCreatures.Footman, 15), town.Garrison[0]);
        Assert.Equal(new CreatureStack(BuiltInCreatures.Archer, 4), town.Garrison[1]);
        Assert.True(hero.Army.IsEmpty);
        Assert.DoesNotContain(hero, state.Heroes);
        Assert.Null(state.HeroAt(town.VisitPosition));
        Assert.Empty(state.HeroesOf(Blue));
        Assert.Contains(hero, state.AllHeroesOf(Blue));
        Assert.Same(hero, state.GetHero(hero.Id));
        Assert.Equal(new HeroEnteredGarrison(town.Id, hero.Id), Assert.Single(entered));
        Assert.Contains("Unknown hero", session.Execute(new StepHeroCommand(Blue, hero.Id, Direction.South)).Error);
    }

    [Fact]
    public void EnteringIsRefused_WithAGarrisonHeroThere_OrWhenTheArmyDoesNotFit()
    {
        var (session, town, _) = Setup(state =>
        {
            var town = state.ObjectsOf<Town>().Single();
            var lines = state.Creatures.BaseCreaturesOf(TownIds.Haven).Where(c => c.Id != BuiltInCreatures.Footman && c.Id != BuiltInCreatures.Archer).Take(6).ToList();
            for (var slot = 1; slot < Army.SlotCount; slot++)
            {
                town.Garrison.Set(slot, new CreatureStack(lines[slot - 1].Id, 1));
            }
        });

        Assert.Contains("does not fit", session.Execute(new EnterGarrisonCommand(Blue, town.Id)).Error);
        Assert.Null(town.GarrisonHero);
    }

    [Fact]
    public void LeavingTheGarrison_LeadsItOutOntoTheEntrance_UnlessThePlayerIsAtItsHeroLimit()
    {
        var (session, town, hero) = Setup(state => state.SetHeroLimit(Blue, 1));
        var state = session.State;
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();

        // TOWN-8: the garrison hero does not count, so another hero may come; then it cannot leave.
        var other = new Hero(new HeroId(9), "Other", Blue, new MapPosition(6, 6), Army.Of(new CreatureStack(BuiltInCreatures.Footman, 1)));
        state.AddHero(other);
        Assert.Contains("heroes on the map", session.Execute(new LeaveGarrisonCommand(Blue, town.Id)).Error);

        state.RemoveHero(other);
        session.Execute(new LeaveGarrisonCommand(Blue, town.Id)).ThrowIfRejected();

        Assert.Null(town.GarrisonHero);
        Assert.Contains(hero, state.Heroes);
        Assert.Equal(town.VisitPosition, hero.Position);
        Assert.Equal(new CreatureStack(BuiltInCreatures.Footman, 15), hero.Army[0]);
        Assert.True(town.Garrison.IsEmpty);
        Assert.Contains("no garrison hero", session.Execute(new LeaveGarrisonCommand(Blue, town.Id)).Error);
    }

    [Fact]
    public void AGarrisonHero_RestsAndRegainsMana_LikeTheOthers()
    {
        var (session, town, hero) = Setup();
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        hero.Mana = 0;
        hero.MovementPoints = 0;

        session.EndTurn();
        session.EndTurn();

        Assert.Equal(ManaRules.DailyMana(hero), hero.Mana);
        Assert.Equal(hero.MaxMovementPoints, hero.MovementPoints);
    }

    [Fact]
    public void InASiege_TheGarrisonHeroLeadsTheGarrison_WithItsWarMachines_AndCannotFlee()
    {
        var (session, town, hero) = Setup();
        var state = session.State;
        hero.AddWarMachine(WarMachineSlot.Tent);
        hero.Skills.Set(BuiltInSkills.Melee, SkillLevel.Basic);
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        session.EndTurn();

        session.MoveTo(B, town.VisitPosition);

        var battle = state.Battle!;
        Assert.NotNull(battle.Siege);
        Assert.Equal(hero.Id, battle.Defender.Hero);
        Assert.True(battle.Defender.Bonus.MeleePercent > 0);
        Assert.Contains(battle.Units, u => u.Side == BattleSide.Defender && u.Machine is WarMachineKind.HealingTent);
        Assert.Equal(15 + 4, battle.Units.Where(u => u.Side == BattleSide.Defender && !u.IsMachine).Sum(u => u.Count));
        Assert.Equal(SurrenderRules.GarrisonRefusal, SurrenderRules.CheckRetreat(battle, BattleSide.Defender));
        Assert.Equal(SurrenderRules.GarrisonRefusal, SurrenderRules.Check(state, battle, BattleSide.Defender));
    }

    [Fact]
    public void AWinningGarrisonHero_KeepsTheSurvivorsInTheGarrison_AndGainsTheExperience()
    {
        var (session, town, hero) = Setup(state => state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 1)));
        var state = session.State;
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        session.EndTurn();

        session.MoveTo(B, town.VisitPosition);
        session.AutoResolve();

        Assert.Null(state.Battle);
        Assert.Same(hero, town.GarrisonHero);
        Assert.Equal(Blue, town.Owner);
        Assert.False(town.Garrison.IsEmpty);
        Assert.True(hero.Experience > 0);
        Assert.True(hero.Army.IsEmpty);
    }

    [Fact]
    public void ABeatenGarrisonHero_IsGone_AndTheTownFalls()
    {
        var (session, town, hero) = Setup(state => state.GetHero(B).Army.Set(0, new CreatureStack("dragon-utopia.black-dragon", 30)));
        var state = session.State;
        var defeated = new List<HeroDefeated>();
        session.Events.Subscribe<HeroDefeated>(defeated.Add);
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        session.EndTurn();

        session.MoveTo(B, town.VisitPosition);
        session.AutoResolve();

        Assert.Null(state.Battle);
        Assert.Null(town.GarrisonHero);
        Assert.Equal(Red, town.Owner);
        Assert.DoesNotContain(hero, state.AllHeroesOf(Blue));
        Assert.Contains(defeated, d => d.Hero == hero.Id);
    }
}
