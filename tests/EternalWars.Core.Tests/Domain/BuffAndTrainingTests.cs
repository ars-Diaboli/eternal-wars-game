using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Temporary buffs and permanent power-ups on the adventure map (ADR-068).</summary>
public class BuffAndTrainingTests
{
    // The 2×2 site at (2, 0) has its entrance at (3, 1); hero A starts just below.
    private static (GameSession Session, T Site) World<T>(string definition, ResourceSet? resources = null)
        where T : MapObject
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        var site = Add<T>(state, definition, 2, 0);
        state.GetPlayer(Blue).Resources = resources ?? new ResourceSet(Gold: 10_000);
        return (Start(state), site);
    }

    private static Hero Alpha(GameSession session) => session.State.GetHero(A);

    private static void Visit(GameSession session, MapObject site, HeroId hero, int? day = null)
    {
        if (day is { } d)
        {
            session.State.Date = new GameDate(d);
        }

        session.Interactions.Visit(session.State.GetHero(hero), site);
    }

    [Fact]
    public void TheFountain_GivesMovementNowAndMoraleForTheNextBattle_OnceADayPerHero()
    {
        var (session, fountain) = World<BuffSite>(BuiltInMapObjects.FountainOfYouth);
        var hero = Alpha(session);
        var gained = new List<HeroMovementGained>();
        session.Events.Subscribe<HeroMovementGained>(gained.Add);

        session.Step(A, Direction.North);
        var points = hero.MovementPoints;

        Assert.Equal(1, hero.TotalMorale);
        Assert.Equal(BuffRules.FountainMovement, Assert.Single(gained).Amount);
        Visit(session, fountain, A); // again today: nothing
        Assert.Equal(points, hero.MovementPoints);
        Visit(session, fountain, C); // another hero may
        Assert.Equal(1, session.State.GetHero(C).TotalMorale);
        Visit(session, fountain, A, day: 2);
        Assert.Equal(points + BuffRules.FountainMovement, hero.MovementPoints);
        Assert.Equal(1, hero.TotalMorale); // renewed, not added
    }

    [Fact]
    public void BuffsOfDifferentSites_Stack_AndTheTempleGivesThreeOnDaySeven()
    {
        var (session, temple) = World<BuffSite>(BuiltInMapObjects.Temple);
        var ring = Add<BuffSite>(session.State, BuiltInMapObjects.FaerieRing, 5, 0);
        var spring = Add<BuffSite>(session.State, BuiltInMapObjects.MineralSpring, 5, 3);
        var hero = Alpha(session);

        Visit(session, temple, A, day: 6);
        Assert.Equal(2, hero.TotalMorale);
        Visit(session, temple, A, day: 7);
        Assert.Equal(3, hero.TotalMorale);
        Visit(session, ring, A);
        Visit(session, spring, A);
        Assert.Equal(2, hero.TotalLuck); // +1 and +1
        Assert.Equal(3, Assert.IsType<HeroStatus>(hero.Statuses.Single(s => s.Source == BuiltInMapObjects.Temple)).Value);
    }

    [Fact]
    public void TheStables_GiveMovementForTheWeek_AndUpgradeLancersForFree()
    {
        var (session, stables) = World<BuffSite>(BuiltInMapObjects.Stables);
        var hero = Alpha(session);
        hero.Army.Set(1, new CreatureStack("haven.lancer", 4));
        var before = hero.MovementPoints;

        Visit(session, stables, A);

        Assert.Equal(before + BuffRules.StablesMovement, hero.MovementPoints);
        Assert.Equal(new CreatureStack("haven.champion", 4), hero.Army[1]);
        Assert.Equal(BuffRules.StablesMovement, hero.StatusTotal(HeroStatusKind.Movement));

        hero.Army.Set(2, new CreatureStack("haven.lancer", 2));
        Visit(session, stables, A, day: 5); // the same week: no movement again, but the upgrade still works
        Assert.Equal(new CreatureStack("haven.champion", 2), hero.Army[2]);
        Assert.Equal(before + BuffRules.StablesMovement, hero.MovementPoints);
    }

    [Fact]
    public void TheMagicWell_FillsTheManaOnceADay()
    {
        var (session, well) = World<BuffSite>(BuiltInMapObjects.MagicWell);
        var hero = Alpha(session);
        hero.Knowledge = 3;
        hero.Mana = 0;

        Visit(session, well, A);

        Assert.Equal(hero.MaxMana, hero.Mana);
        Assert.True(hero.MaxMana > 0);
    }

    [Fact]
    public void TheLearningStone_GivesExperienceOncePerHero()
    {
        var (session, stone) = World<TrainingSite>(BuiltInMapObjects.LearningStone);
        var hero = Alpha(session);

        Visit(session, stone, A);
        Visit(session, stone, A, day: 30);

        Assert.Equal(TrainingRules.LearningStoneExperience, hero.Experience);
    }

    [Fact]
    public void TheArena_AsksForAttackOrDefense_AndMustBeAnswered()
    {
        var (session, arena) = World<TrainingSite>(BuiltInMapObjects.Arena);
        var hero = Alpha(session);
        var attack = hero.Attack;

        session.Step(A, Direction.North);

        var decision = Assert.IsType<TrainingDecision>(session.State.PendingDecision);
        Assert.Equal(new[] { TrainingOption.Attack, TrainingOption.Defense }, decision.Options);
        Assert.False(decision.CanDecline);
        Assert.False(session.Execute(new ChooseTrainingCommand(Blue, null)).IsAccepted);
        Assert.False(session.Execute(new ChooseTrainingCommand(Blue, TrainingOption.Knowledge)).IsAccepted);

        Assert.True(session.Execute(new ChooseTrainingCommand(Blue, TrainingOption.Attack)).IsAccepted);

        Assert.Equal(attack + 2, hero.Attack);
        Visit(session, arena, A, day: 9);
        Assert.Null(session.State.PendingDecision); // once per hero
    }

    [Fact]
    public void TheColosseum_RaisesSpellPowerOrKnowledge()
    {
        var (session, colosseum) = World<TrainingSite>(BuiltInMapObjects.ColosseumOfTheMagi);
        Visit(session, colosseum, A);
        session.Execute(new ChooseTrainingCommand(Blue, TrainingOption.Knowledge)).ThrowIfRejected();
        Assert.Equal(2, Alpha(session).Knowledge);
    }

    [Fact]
    public void TheLibrary_RaisesAllFourStats_OnlyFromLevelTen()
    {
        var (session, library) = World<TrainingSite>(BuiltInMapObjects.LibraryOfEnlightenment);
        var hero = Alpha(session);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);

        Visit(session, library, A);
        Assert.Single(refused);
        Assert.False(library.Visits.IsSpent); // may come back later

        hero.Level = 10;
        var (attack, defense) = (hero.Attack, hero.Defense);
        Visit(session, library, A);
        Assert.Equal((attack + 2, defense + 2, 2, 2), (hero.Attack, hero.Defense, hero.SpellPower, hero.Knowledge));
    }

    [Fact]
    public void TheGazebo_SellsExperience_AndMayBeDeclinedOrRefusedWithoutGold()
    {
        var (session, gazebo) = World<TrainingSite>(BuiltInMapObjects.Gazebo);
        var hero = Alpha(session);

        Visit(session, gazebo, A);
        Assert.True(Assert.IsType<TrainingDecision>(session.State.PendingDecision).CanDecline);
        session.Execute(new ChooseTrainingCommand(Blue, null)).ThrowIfRejected();
        Assert.False(gazebo.Visits.IsSpent);
        Assert.Equal(10_000, session.State.GetPlayer(Blue).Resources.Gold);

        Visit(session, gazebo, A);
        session.Execute(new ChooseTrainingCommand(Blue, TrainingOption.PayGold)).ThrowIfRejected();
        Assert.Equal(10_000 - TrainingRules.GazeboGold, session.State.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(TrainingRules.GazeboExperience, hero.Experience);

        var (poor, poorGazebo) = World<TrainingSite>(BuiltInMapObjects.Gazebo, new ResourceSet(Gold: 1000));
        var refused = new List<MapObjectRefused>();
        poor.Events.Subscribe<MapObjectRefused>(refused.Add);
        Visit(poor, poorGazebo, A);
        Assert.Null(poor.State.PendingDecision);
        Assert.Single(refused);
    }

    [Fact]
    public void TheTreeOfKnowledge_TakesTheHeroToTheNextLevel_ForGoldOrMagicResources()
    {
        var (session, tree) = World<TrainingSite>(BuiltInMapObjects.TreeOfKnowledge, new ResourceSet(Gold: 3000, Crystal: 5, Mercury: 5, Sulfur: 5));
        var hero = Alpha(session);
        hero.Experience = 100;

        Visit(session, tree, A);
        var decision = Assert.IsType<TrainingDecision>(session.State.PendingDecision);
        Assert.Equal(new[] { TrainingOption.PayGold, TrainingOption.PayMagicResources }, decision.Options);

        session.Execute(new ChooseTrainingCommand(Blue, TrainingOption.PayMagicResources)).ThrowIfRejected();

        Assert.Equal(ExperienceTable.For(2), hero.Experience);
        Assert.Equal(new ResourceSet(Gold: 3000), session.State.GetPlayer(Blue).Resources);
        Assert.True(tree.Visits.IsSpent);

        var (onlyGold, goldTree) = World<TrainingSite>(BuiltInMapObjects.TreeOfKnowledge, new ResourceSet(Gold: 2500));
        Visit(onlyGold, goldTree, A);
        Assert.Equal(new[] { TrainingOption.PayGold }, Assert.IsType<TrainingDecision>(onlyGold.State.PendingDecision).Options);
    }

    [Fact]
    public void BuffAndTrainingSites_AreInTheMapFormat()
    {
        var editor = new MapEditorSession(BlankScenario.Create(14, 10));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.Stables, new(2, 2), out _));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.Arena, new(6, 2), out _));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(18, MapDocument.CurrentFormatVersion);
        Assert.Contains("\"kind\": \"buff\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"training\"", json, StringComparison.Ordinal);
        Assert.Single(restored.ObjectsOf<BuffSite>());
        Assert.Single(restored.ObjectsOf<TrainingSite>());
    }

    [Fact]
    public void TheAi_TrainsAtTheArenaNearby()
    {
        var (session, arena) = World<TrainingSite>(BuiltInMapObjects.Arena);
        var attack = Alpha(session).Attack;

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        Assert.True(arena.Visits.IsSpent);
        Assert.Equal(attack + 2, Alpha(session).Attack);
    }
}
