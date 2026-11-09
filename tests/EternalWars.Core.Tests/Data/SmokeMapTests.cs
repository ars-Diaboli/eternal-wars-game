using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Data;

/// <summary>The maps under <c>tools/smoke/</c> load, and do what the scripted input of <c>tools/smoke-godot.ps1</c> expects.</summary>
public class SmokeMapTests
{
    [Fact]
    public void BattleMap_FirstStepEastStartsAFightTheHumanCommands()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("battle.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.East);

        Assert.Equal(BattlePhase.Fighting, session.State.Battle?.Phase);
        Assert.NotNull(session.Battles.Commander(BattleSide.Attacker));
    }

    [Fact]
    public void SiegeMap_FirstStepNorthStartsASiegeOfTheCastleWithTheBallista()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("siege.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.North);

        var battle = session.State.Battle;
        Assert.Equal(BattlePhase.Fighting, battle?.Phase);
        Assert.NotNull(battle!.Siege);
        Assert.Contains(battle.Siege!.Structures, s => s.Kind == SiegeStructureKind.Keep);
        Assert.Contains(battle.Siege.Structures, s => s.Kind == SiegeStructureKind.Tower);
        Assert.Contains(battle.LivingUnits, u => u.Side == BattleSide.Attacker && u.Machine is not null);
    }

    [Fact]
    public void TownsMap_FirstStepNorthVisitsTheOwnTownWhoseGarrisonTheHeroCanEnter()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("towns.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.North);

        var town = session.State.ObjectsOf<Town>().Single(t => t.Name == "Castlebridge");
        Assert.Equal(new HeroId(1), TownRules.VisitingHero(session.State, town)?.Id);
        Assert.Null(TownRules.CheckEnterGarrison(session.State, town));
    }

    [Fact]
    public void CaravanMap_FirstStepNorthVisitsADwelling_AndTheTownCanHireFromTheOwnedOneByCaravan()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("caravan.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.North);

        var visited = session.State.ObjectsOf<CreatureDwelling>().Single(d => d.Creature == "haven.pikeman");
        Assert.Equal(session.State.CurrentPlayer.Id, visited.Owner);
        var town = session.State.ObjectsOf<Town>().Single();
        var owned = session.State.ObjectsOf<CreatureDwelling>().Single(d => d.Creature == "haven.footman");
        Assert.Contains(owned, CaravanRules.Dwellings(session.State, town));
        Assert.Null(CaravanRules.CheckOrder(session.State, town, owned, owned.Available!.Value));
    }

    [Fact]
    public void QuestsMap_TwoStepsNorthEnterTheSeersHut_WhoseFirstStageAsksToComplete_ThenShowsTheNext()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("quests.ewmap.json"))));
        session.Start();
        Assert.Equal(3, session.State.ObjectsOf<QuestSite>().Count());

        session.Step(new HeroId(1), Direction.North);
        session.Step(new HeroId(1), Direction.North);
        var question = Assert.IsType<MapEventDecision>(session.State.PendingDecision);
        Assert.Equal(["Complete", "Not now"], question.Answers);
        session.Execute(new AnswerMapEventCommand(session.State.CurrentPlayer.Id, 0)).ThrowIfRejected();

        Assert.Equal("Come back when you have reached level 30.", Assert.IsType<MapEventDecision>(session.State.PendingDecision).Text);
        Assert.Single(session.State.MapEvents.QuestLog(session.State.CurrentPlayer.Id));
    }

    [Fact]
    public void ConnectionsTemplate_GeneratesEveryV7ConnectionObject()
    {
        var template = TemplateSerializer.Load(SmokeMap("connections.ewtemplate.json"));
        Assert.DoesNotContain(TemplateValidator.Validate(template), i => i.Severity == IssueSeverity.Error);

        var map = MapGenerator.Generate(template, 72, 1);

        Assert.Empty(map.Problems);
        Assert.Single(map.Objects.OfType<MapGarrison>());
        Assert.Equal(2, map.Objects.OfType<BorderPost>().Count());
        Assert.Equal(2, map.Objects.OfType<KeymasterTent>().Count());
        Assert.Equal(2, map.Objects.Count(o => o.Definition.Id == BuiltInMapObjects.TwoWayMonolith));
    }

    [Fact]
    public void UniqueBuildingsMap_TheNecropolisTownCanTransform_AndTheGroveAndStablesGiveTheirBonusOnTheFirstStep()
    {
        var session = Started("unique-buildings.ewmap.json");
        var grimhollow = session.State.ObjectsOf<Town>().Single(t => t.Name == "Grimhollow");
        Assert.Null(UndeadTransformer.CheckTown(grimhollow));
        Assert.Null(UndeadTransformer.Of(session.State, grimhollow, "haven.pikeman", 12).Refusal);

        session.Step(new HeroId(2), Direction.North);
        Assert.True(session.State.GetHero(new HeroId(2)).Experience >= UniqueBuildings.ExperienceBonus);
        var levelUp = Assert.IsType<LevelUpDecision>(session.State.PendingDecision);
        session.Execute(new ChooseSkillCommand(session.State.CurrentPlayer.Id, levelUp.Options[0])).ThrowIfRejected();
        var before = session.State.GetHero(new HeroId(3)).MovementPoints;
        session.Step(new HeroId(3), Direction.North);
        Assert.True(session.State.GetHero(new HeroId(3)).MovementPoints > before);
    }

    [Fact]
    public void SitesMap_EachHeroStepsNorthOntoItsSite()
    {
        var session = Started("sites.ewmap.json");

        session.Step(new HeroId(1), Direction.North);
        Assert.NotEmpty(SkillSchoolRules.LessonsOf(session.State.ObjectsOf<SkillSchool>().Single(s => s.TeachesMagic), new HeroId(1)));
        session.Step(new HeroId(3), Direction.North);
        Assert.IsType<TrainingDecision>(session.State.PendingDecision);
    }

    [Fact]
    public void GrailMap_TheObeliskIsNorthOfObelisa_AndDigbyStandsOnTheGrail_TwoStepsFromTheTown()
    {
        var session = Started("grail.ewmap.json");

        session.Step(new HeroId(1), Direction.North);
        Assert.Equal(1, GrailRules.Puzzle(session.State, session.State.CurrentPlayer.Id)!.Visited);
        session.Execute(new DigCommand(session.State.CurrentPlayer.Id, new HeroId(2))).ThrowIfRejected();
        Assert.True(session.State.Grail.IsFound);
    }

    [Fact]
    public void MagicMap_MagusVisitsTheGuildNorth_AndAttacksTheComputerMageSouth()
    {
        var session = Started("magic.ewmap.json");
        var magus = session.State.GetHero(new HeroId(1));
        Assert.True(magus.HasSpellBook);
        Assert.False(session.State.GetHero(new HeroId(2)).HasSpellBook);

        session.Step(new HeroId(1), Direction.North);
        Assert.Equal(new HeroId(1), TownRules.VisitingHero(session.State, session.State.ObjectsOf<Town>().Single(t => t.Name == "Castlebridge"))?.Id);
    }

    [Fact]
    public void NecromancyMap_FirstStepEastAsksTheBattleWidth_WithDeathAttunementAndAllThreeMachines()
    {
        var session = Started("necromancy.ewmap.json");
        var hero = session.State.GetHero(new HeroId(1));
        Assert.NotEqual(Core.Domain.Heroes.SkillLevel.None, hero.SkillLevelOf(NecromancyRules.Skill));
        Assert.Equal(3, hero.WarMachines.Count);

        session.Step(new HeroId(1), Direction.East);
        Assert.IsType<BattleDecision>(session.State.PendingDecision); // Advanced Tactics picks the width; the 50 wolves do not offer to join
    }

    [Fact]
    public void SiegeMagicMap_FirstStepNorthStartsASiege_WhereTheHeroCanCastEarthquake()
    {
        var session = Started("siege-magic.ewmap.json");

        session.Step(new HeroId(1), Direction.North);

        var battle = session.State.Battle!;
        Assert.NotNull(battle.Siege);
        Assert.Contains(battle.Participant(BattleSide.Attacker).Caster!.Spells, s => s.Spell.Id == "nature.earthquake");
    }

    private static GameSession Started(string map)
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap(map))));
        session.Start();
        return session;
    }

    [Fact]
    public void ArenaMap_AgainstTheComputer_DayTwoOpensTheBattleAtTheCombatPlace_AndItsWinnerWins()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("arena.ewmap.json"))));
        session.Start();
        var state = session.State;
        var place = Assert.Single(state.ObjectsOf<CombatPlace>());

        Assert.True(state.Rules.Arena);
        session.Execute(new EndTurnCommand(new PlayerId(0))).ThrowIfRejected();
        new EternalWars.Core.Domain.AI.AdventureAi(session).PlayTurns(limit: 1); // Red prepares and ends day 1

        Assert.Equal(2, state.Date.AbsoluteDay);
        Assert.NotNull(state.Battle);
        Assert.Equal(place.Id, state.Arena!.CombatPlace);
        Assert.Matches(@"^Orrin \(Blue\) attacked \w+ \(Red\)\.$", state.Arena.Summary); // the AI may have hired a stronger hero on day 1
        Assert.Equal(place.AttackCell, state.GetHero(new HeroId(1)).Position);
        Assert.NotNull(session.Battles.Commander(BattleSide.Attacker)); // the human fights it by hand

        session.Execute(new AutoResolveBattleCommand(new PlayerId(0))).ThrowIfRejected();

        Assert.True(state.IsOver);
        Assert.Single(state.Outcome!.Winners);
        Assert.False(ScoreRules.IsRanked(state));
    }

    [Fact]
    public void ArenaMap_HotSeat_TheSetupMakesBothPlayersPeople()
    {
        var state = MapConverter.ToGameState(MapSerializer.Load(SmokeMap("arena.ewmap.json")));

        state.AssignControllers(new Dictionary<PlayerId, bool> { [new PlayerId(1)] = true });

        Assert.All(state.Players, p => Assert.True(p.IsHuman));
        Assert.Throws<InvalidOperationException>(() =>
            state.AssignControllers(new Dictionary<PlayerId, bool> { [new PlayerId(0)] = false, [new PlayerId(1)] = false }));
        Assert.Throws<InvalidOperationException>(() => state.AssignControllers(new Dictionary<PlayerId, bool> { [new PlayerId(5)] = true }));
        new GameSession(state).Start();
        Assert.Throws<InvalidOperationException>(() => state.AssignControllers(new Dictionary<PlayerId, bool> { [new PlayerId(1)] = false }));
    }

    [Fact]
    public void ArenaMap_PassesTheValidator_ThePlayersCannotMeetOnDayOne()
    {
        var state = MapConverter.ToGameState(MapSerializer.Load(SmokeMap("arena.ewmap.json")));

        Assert.Empty(MapValidator.Validate(state).Select(i => i.Message).Where(m =>
            m.Contains("combat place", StringComparison.Ordinal) || m.Contains("meet on day 1", StringComparison.Ordinal) || m.Contains("after day 1", StringComparison.Ordinal)));
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Severity == IssueSeverity.Error);
    }

    private static string SmokeMap(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EternalWars.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."), "tools", "smoke", name);
    }
}
