using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The first set of unique buildings (ADR-126, ADR-138): a visit bonus once per hero per town (the Stables and the Mana Spring once a week), and a
/// bonus for the hero defending the town in a siege.
/// </summary>
public class UniqueBuildingTests
{
    /// <summary>A town at (0, 0) has its entrance at (3, 6); hero A stands just south of it, Red's hero B two tiles further.</summary>
    private static readonly string[] Rows =
    [
        "............",
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

    private static (GameSession Session, Town Town, Hero Hero, List<UniqueBuildingVisited> Visits) Setup(string townType, string? building, Action<GameState>? before = null)
    {
        var state = Create(Rows);
        var town = AddTown(state, townType, 0, 0, Blue);
        if (building is not null)
        {
            town.SetLevel(building, 1);
        }

        before?.Invoke(state);
        var session = Start(state);
        var visits = new List<UniqueBuildingVisited>();
        session.Events.Subscribe<UniqueBuildingVisited>(visits.Add);
        return (session, town, state.GetHero(A), visits);
    }

    private static void StepOutAndIn(GameSession session)
    {
        session.Step(A, Direction.South);
        session.Step(A, Direction.North);
    }

    [Fact]
    public void EveryTownButNecropolis_HasOneUniqueBuilding_InItsOwnRows()
    {
        var catalog = BuildingCatalog.Default;
        var towns = new[] { TownIds.Haven, TownIds.Sylvan, TownIds.Academy, TownIds.Inferno, TownIds.Dungeon, TownIds.Stronghold, TownIds.SwampBestiary };

        Assert.Equal(towns.Order(StringComparer.Ordinal), UniqueBuildings.All.Select(b => b.Town).Order(StringComparer.Ordinal));
        Assert.All(UniqueBuildings.All, b =>
        {
            Assert.True(catalog.For(b.Town).Contains(b.Id));
            Assert.Equal(1, catalog.For(b.Town).MaxLevel(b.Id));
            Assert.False(catalog.Contains(b.Id)); // no shared row
            Assert.All(towns.Where(t => t != b.Town), t => Assert.False(catalog.For(t).Contains(b.Id)));
        });
        Assert.Equal(new ResourceSet(Gold: 2000, Ore: 5, Mercury: 5), catalog.For(TownIds.Inferno).Get(UniqueBuildings.AltarOfBrimstone, 1).Cost);
        Assert.Equal([new BuildingRequirement(BuiltInBuildings.TownHall, 2)], catalog.For(TownIds.Haven).Get(UniqueBuildings.Stables, 1).Requires);
    }

    [Fact]
    public void TheProvingRing_GivesEachHeroOneAttack_OncePerTown()
    {
        var (session, town, hero, visits) = Setup(TownIds.Stronghold, UniqueBuildings.ProvingRing);
        var attack = hero.Attack;

        session.Step(A, Direction.North);
        Assert.Equal(attack + 1, hero.Attack);
        Assert.Equal(new UniqueBuildingVisited(A, town.Id, UniqueBuildings.ProvingRing), Assert.Single(visits));

        StepOutAndIn(session);
        Assert.Equal(attack + 1, hero.Attack);
        Assert.Single(visits);

        // Another Stronghold gives it again.
        var second = new Town(new MapObjectId(99), MapObjectCatalog.Default.Get(BuiltInMapObjects.TownId(TownIds.Stronghold)), new MapPosition(0, 0), Blue);
        second.SetLevel(UniqueBuildings.ProvingRing, 1);
        Assert.True(UniqueBuildings.CanTake(session.State, second, UniqueBuildings.Get(UniqueBuildings.ProvingRing)!, hero));
        Assert.False(UniqueBuildings.CanTake(session.State, town, UniqueBuildings.Get(UniqueBuildings.ProvingRing)!, hero));
    }

    [Fact]
    public void WithoutTheBuilding_OrForAnotherPlayersHero_ThereIsNoBonus()
    {
        var (session, town, hero, visits) = Setup(TownIds.Stronghold, null);
        var attack = hero.Attack;

        session.Step(A, Direction.North);

        Assert.Equal(attack, hero.Attack);
        Assert.Empty(visits);
        town.SetLevel(UniqueBuildings.ProvingRing, 1);
        Assert.False(UniqueBuildings.CanTake(session.State, town, UniqueBuildings.Get(UniqueBuildings.ProvingRing)!, session.State.GetHero(B)));
    }

    [Theory]
    [InlineData(TownIds.SwampBestiary, UniqueBuildings.ShellWall, PrimaryStat.Defense)]
    [InlineData(TownIds.Academy, UniqueBuildings.HallOfRecords, PrimaryStat.Knowledge)]
    [InlineData(TownIds.Inferno, UniqueBuildings.AltarOfBrimstone, PrimaryStat.SpellPower)]
    public void TheStatBuildings_RaiseTheirStat(string townType, string building, PrimaryStat stat)
    {
        var (session, _, hero, _) = Setup(townType, building);
        var before = hero.Stat(stat);

        session.Step(A, Direction.North);

        Assert.Equal(before + 1, hero.Stat(stat));
    }

    [Fact]
    public void TheElderGrove_Gives1000Experience()
    {
        var (session, _, hero, _) = Setup(TownIds.Sylvan, UniqueBuildings.ElderGrove);
        var before = hero.Experience;

        session.Step(A, Direction.North);

        Assert.Equal(before + UniqueBuildings.ExperienceBonus, hero.Experience);
    }

    [Fact]
    public void TheManaSpring_AddsThreeQuartersOfTheMaximum_AboveIt()
    {
        var (session, _, hero, _) = Setup(TownIds.Dungeon, UniqueBuildings.ManaSpring, state => state.GetHero(A).Knowledge = 4);
        var max = hero.MaxMana;
        Assert.Equal(max, hero.Mana);

        session.Step(A, Direction.North);

        Assert.Equal(max + (max * 3 / 4), hero.Mana);
        StepOutAndIn(session);
        Assert.Equal(max + (max * 3 / 4), hero.Mana); // once a week
    }

    [Fact]
    public void TheStables_GiveMovementNowAndForTheWeek_AgainNextWeek_WithoutUpgradingLancers()
    {
        var (session, town, hero, visits) = Setup(TownIds.Haven, UniqueBuildings.Stables,
            state => state.GetHero(A).Army.Set(1, new CreatureStack(BuffRules.StablesCreature, 3)));
        var state = session.State;

        session.Step(A, Direction.North);
        var points = hero.MovementPoints;
        Assert.Contains(hero.Statuses, s => s.Source == BuiltInMapObjects.Stables && s.Kind == HeroStatusKind.Movement && s.Value == BuffRules.StablesMovement);
        Assert.Contains(hero.Army.Stacks, s => s.Stack.Creature == BuffRules.StablesCreature);

        StepOutAndIn(session);
        Assert.Single(visits);
        Assert.True(hero.MovementPoints < points);

        // The hero stays in the town; the first night of the next week gives it again.
        while (state.Date.Day != 1 || state.CurrentPlayer.Id != Blue || state.Date.AbsoluteDay == 1)
        {
            session.EndTurn();
        }

        Assert.Equal(2, visits.Count);
        Assert.Equal(town.Id, visits[1].Town);
    }

    [Fact]
    public void BuildingIt_GivesTheVisitingHeroTheBonusAtOnce()
    {
        var (session, town, hero, visits) = Setup(TownIds.Inferno, null);
        session.Step(A, Direction.North);
        session.State.GetPlayer(Blue).Resources = new ResourceSet(Gold: 2000, Ore: 5, Mercury: 5);
        var power = hero.SpellPower;

        session.Execute(new BuildCommand(Blue, town.Id, UniqueBuildings.AltarOfBrimstone)).ThrowIfRejected();

        Assert.Equal(power + 1, hero.SpellPower);
        Assert.Single(visits);
        Assert.Equal(new ResourceSet(), session.State.GetPlayer(Blue).Resources);
    }

    [Fact]
    public void InASiege_TheDefendingHeroGetsTheTownsBonus_NotOutsideASiege()
    {
        var (session, town, hero, _) = Setup(TownIds.Stronghold, UniqueBuildings.ProvingRing);
        session.Step(A, Direction.North);
        session.EndTurn();

        session.MoveTo(B, town.VisitPosition);

        var battle = session.State.Battle!;
        Assert.NotNull(battle.Siege);
        Assert.Equal(hero.TotalAttack + 1, battle.Defender.Attack);
        Assert.Equal(hero.TotalDefense, battle.Defender.Defense);

        var (open, openTown, openHero, _) = Setup(TownIds.Stronghold, UniqueBuildings.ProvingRing, state => state.ObjectsOf<Town>().Single().SetLevel(BuiltInBuildings.Fort, 0));
        open.Step(A, Direction.North);
        open.EndTurn();
        open.MoveTo(B, openTown.VisitPosition);
        Assert.Null(open.State.Battle!.Siege);
        Assert.Equal(openHero.TotalAttack, open.State.Battle!.Defender.Attack);
    }

    [Fact]
    public void InASiege_TheGarrisonHeroGetsLuckAndMorale()
    {
        var (session, town, _, _) = Setup(TownIds.Sylvan, UniqueBuildings.ElderGrove);
        session.Step(A, Direction.North);
        while (session.State.PendingDecision is EternalWars.Core.Domain.Decisions.LevelUpDecision levelUp)
        {
            session.Execute(new ChooseSkillCommand(Blue, levelUp.Options[0])).ThrowIfRejected(); // the grove's experience
        }

        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        var hero = session.State.GetHero(A);
        session.EndTurn();

        session.MoveTo(B, town.VisitPosition);

        Assert.Equal(hero.TotalLuck + 1, session.State.Battle!.Defender.Bonus.Luck);
        Assert.Equal(hero.TotalMorale, session.State.Battle!.Defender.Bonus.Morale);
    }

    [Fact]
    public void InASiege_TheHallOfRecords_LendsManaForTheBattleOnly()
    {
        var (session, town, hero, _) = Setup(TownIds.Academy, UniqueBuildings.HallOfRecords, state =>
        {
            state.GetHero(A).HasSpellBook = true;
            state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 1));
        });
        session.Step(A, Direction.North);
        var mana = hero.Mana;
        session.EndTurn();

        session.MoveTo(B, town.VisitPosition);
        var caster = session.State.Battle!.Defender.Caster!;
        Assert.Equal(mana + ManaRules.ManaPerKnowledge, caster.Mana);
        Assert.Equal(ManaRules.ManaPerKnowledge, caster.LentMana);

        session.AutoResolve();
        Assert.Null(session.State.Battle);
        Assert.True(hero.Mana <= mana);
    }

    [Fact]
    public void TheSiegeBonuses_FollowTheTable()
    {
        Assert.Equal(new SiegeBonus(SpellPower: 1), UniqueBuildings.Get(UniqueBuildings.AltarOfBrimstone)!.Siege);
        Assert.Equal(new SiegeBonus(Morale: 1), UniqueBuildings.Get(UniqueBuildings.ManaSpring)!.Siege);
        Assert.Equal(new SiegeBonus(Defense: 1), UniqueBuildings.Get(UniqueBuildings.ShellWall)!.Siege);
        Assert.True(UniqueBuildings.Get(UniqueBuildings.Stables)!.Siege.IsNone);
        Assert.Equal(VisitScope.WeeklyPerHero, UniqueBuildings.Get(UniqueBuildings.Stables)!.Scope);
        Assert.Equal(VisitScope.OncePerHero, UniqueBuildings.Get(UniqueBuildings.ElderGrove)!.Scope);
    }
}
