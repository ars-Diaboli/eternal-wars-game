using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Decisions;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Play-tests from the editor (EDIT-5, ADR-267): the launch arguments, the setup, the start, the others off, the cheats, no ranking.</summary>
public class PlayTestTests
{
    private static GameState Map(PlayTestArguments test, params string[] rows)
    {
        var state = Create(GameOptions.Default with { PlayTest = test.Rules }, rows);
        test.Prepare(state);
        return state;
    }

    [Fact]
    public void Arguments_ReadBackWhatTheyWrite_AndOnlyPlayerMakesAPlayTest()
    {
        var test = new PlayTestArguments(2, PlayTestOthers.Off, Reveal: true, Start: new MapPosition(3, 4, 1), Day: 8, Cheats: true);

        Assert.Equal(["--player=2", "--others=off", "--reveal", "--start=3,4,1", "--day=8", "--cheats"], test.ToArguments());
        Assert.Equal((test, (string?)null), PlayTestArguments.Parse(["--map=x.ewmap.json", .. test.ToArguments()]));
        Assert.Equal(["--player=1"], new PlayTestArguments(1).ToArguments());
        Assert.Equal((new PlayTestArguments(1, Start: new MapPosition(5, 6)), (string?)null), PlayTestArguments.Parse(["--player=1", "--start=5,6"]));
        Assert.Equal((null, null), PlayTestArguments.Parse(["--map=x.ewmap.json", "--reveal"]));
    }

    [Theory]
    [InlineData("--player=0")]
    [InlineData("--player=red")]
    [InlineData("--others=maybe")]
    [InlineData("--start=1")]
    [InlineData("--start=1,-2")]
    [InlineData("--day=0")]
    [InlineData("--day=10000")]
    public void Arguments_ThatCannotBeRead_SayWhich(string bad)
    {
        var (arguments, error) = PlayTestArguments.Parse(bad.StartsWith("--player", StringComparison.Ordinal) ? [bad] : ["--player=1", bad]);

        Assert.Null(arguments);
        Assert.StartsWith(bad, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Prepare_LetsAPersonPlayThePlayTestersPlayer_AndTheComputerTheOthers()
    {
        var state = Map(new PlayTestArguments(2), "A...B.....");

        Assert.False(state.GetPlayer(Blue).IsHuman);
        Assert.True(state.GetPlayer(Red).IsHuman);
    }

    [Fact]
    public void Prepare_RefusesAPlayerTheMapHasNot_AndAStartThatCannotBeUsed()
    {
        var state = Create("A...B.....", "####......");

        Assert.Contains("no player 3", Assert.Throws<InvalidOperationException>(() => new PlayTestArguments(3).Prepare(state)).Message, StringComparison.Ordinal);
        Assert.Contains("terrain", Assert.Throws<InvalidOperationException>(() => new PlayTestArguments(1, Start: new MapPosition(0, 1)).Prepare(state)).Message, StringComparison.Ordinal);
        Assert.Contains("another player's hero", Assert.Throws<InvalidOperationException>(() => new PlayTestArguments(1, Start: new MapPosition(4, 0)).Prepare(state)).Message, StringComparison.Ordinal);
        Assert.Contains("not on the map", Assert.Throws<InvalidOperationException>(() => new PlayTestArguments(1, Start: new MapPosition(20, 0)).Prepare(state)).Message, StringComparison.Ordinal);

        state.SetRules(state.Rules with { Arena = true });
        Assert.Contains("day 1", new PlayTestArguments(1, Day: 2).WhyNot(state), StringComparison.Ordinal); // the arena's battle is on day 2
        Assert.Null(new PlayTestArguments(1).WhyNot(state));
    }

    [Fact]
    public void TheStart_PutsTheFirstHeroOnTheTile_OnTheDayAsked_WithTheMapRevealed()
    {
        var wide = new string('.', 40);
        var state = Map(new PlayTestArguments(1, Reveal: true, Start: new MapPosition(6, 1), Day: 9), "A........B" + new string('.', 30), wide, wide);

        Start(state, revealAll: false);

        Assert.Equal(new MapPosition(6, 1), state.GetHero(A).Position);
        Assert.Equal(9, state.Date.AbsoluteDay);
        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(39, 2))); // far beyond what the hero sees from where it starts
        Assert.False(state.Fog.IsExplored(Red, new MapPosition(39, 2))); // the computer's fog is its own
    }

    [Fact]
    public void TheStart_LeavesAHeroOfThePlayTesterThatIsOnTheTile()
    {
        var state = Map(new PlayTestArguments(1, Start: new MapPosition(5, 0)), "A....C...B");

        Start(state);

        Assert.Equal((new MapPosition(0, 0), new MapPosition(5, 0)), (state.GetHero(A).Position, state.GetHero(C).Position));
    }

    [Fact]
    public void OthersOff_EndTheirTurnsAtOnce_WithoutMoving()
    {
        var state = Map(new PlayTestArguments(1, PlayTestOthers.Off), "A........B");
        var session = Start(state);
        var director = new GameDirector(session);
        var before = state.GetHero(B).Position;

        Assert.True(session.Execute(new EndTurnCommand(Blue)).IsAccepted);
        Assert.True(director.PlayComputer(screensOpen: false));

        Assert.Equal(Blue, state.CurrentPlayer.Id);
        Assert.Equal(2, state.Date.AbsoluteDay);
        Assert.Equal(before, state.GetHero(B).Position);
        Assert.IsType<EndTurnCommand>(session.CommandLog[^1]);
    }

    [Fact]
    public void OthersOff_AnswerWhatTheyAreAsked_ThenEndTheirTurn()
    {
        var state = Map(new PlayTestArguments(1, PlayTestOthers.Off), "A........B");
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                new MapEventDefinition("ask", MapEventKind.Player,
                [
                    new IfAction(new PlayerIsCondition(1), [new QuestionAction("Which?", [new EventAnswer("First", []), new EventAnswer("Second", [])])]),
                ]),
            ],
        });
        var session = Start(state);
        var director = new GameDirector(session);

        Assert.True(session.Execute(new EndTurnCommand(Blue)).IsAccepted);
        Assert.IsType<MapEventDecision>(state.PendingDecision); // Red is asked at the start of its turn
        Assert.True(director.PlayComputer(screensOpen: false));

        Assert.Equal((Blue, 2), (state.CurrentPlayer.Id, state.Date.AbsoluteDay));
    }

    [Fact]
    public void Cheats_WaitForAQuestion_AndAreRefusedInALanGame()
    {
        var state = Map(new PlayTestArguments(1, Cheats: true), "A........B");
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                new MapEventDefinition("ask", MapEventKind.Player,
                [
                    new IfAction(new PlayerIsCondition(0), [new QuestionAction("Which?", [new EventAnswer("First", []), new EventAnswer("Second", [])])]),
                ]),
            ],
        });
        var session = Start(state);
        session.Route = new RunsHere();

        Assert.Contains("LAN", session.Execute(new CheatResourcesCommand(Blue, new ResourceSet(Gold: 1))).Error, StringComparison.Ordinal);

        session.Route = null;
        Assert.Equal(Blue, state.CurrentPlayer.Id);
        Assert.IsType<MapEventDecision>(state.PendingDecision); // Blue is asked at the start of its turn
        Assert.Contains("question", session.Execute(new CheatResourcesCommand(Blue, new ResourceSet(Gold: 1))).Error, StringComparison.Ordinal);
    }

    /// <summary>A route that runs every command here, as a LAN host does with its own player's.</summary>
    private sealed class RunsHere : ICommandRoute
    {
        public CommandResult? Route(GameCommand command) => null;
    }

    [Fact]
    public void Cheats_AreRefused_OutsideAPlayTestWithCheats()
    {
        var plain = Start(Create("A........B"));
        var noCheats = Start(Map(new PlayTestArguments(1), "A........B"));

        foreach (var session in new[] { plain, noCheats })
        {
            Assert.Contains("--cheats", session.Execute(new CheatResourcesCommand(Blue, new ResourceSet(Gold: 100))).Error, StringComparison.Ordinal);
            Assert.False(session.Execute(new CheatLevelsCommand(Blue, A, 1)).IsAccepted);
            Assert.False(session.Execute(new CheatCreaturesCommand(Blue, A, BuiltInCreatures.Footman, 5)).IsAccepted);
        }
    }

    [Fact]
    public void Cheats_GiveResourcesLevelsAndCreatures()
    {
        var state = Map(new PlayTestArguments(1, Cheats: true), "A........B");
        var session = Start(state);
        var (gold, ore) = (state.GetPlayer(Blue).Resources.Gold, state.GetPlayer(Blue).Resources.Ore);
        var hero = state.GetHero(A);

        Assert.True(session.Execute(new CheatResourcesCommand(Blue, new ResourceSet(Gold: 5000, Ore: 10))).IsAccepted);
        Assert.True(session.Execute(new CheatCreaturesCommand(Blue, A, BuiltInCreatures.Footman, 5)).IsAccepted);
        Assert.True(session.Execute(new CheatLevelsCommand(Blue, A, 2)).IsAccepted); // last: its level-ups are a question to answer

        Assert.Equal((gold + 5000, ore + 10), (state.GetPlayer(Blue).Resources.Gold, state.GetPlayer(Blue).Resources.Ore));
        Assert.Equal(3, ExperienceTable.LevelFor(hero.Experience)); // the level-ups are offered as usual
        Assert.Equal(15, hero.Army.Stacks.Single().Stack.Count);
    }

    [Fact]
    public void Cheats_RefuseWhatTheyCannotGive()
    {
        var state = Map(new PlayTestArguments(1, Cheats: true), "A........B");
        var session = Start(state);
        var hero = state.GetHero(A);
        var creatures = state.Creatures.All.Where(c => c.Id != BuiltInCreatures.Footman).Take(Army.SlotCount - 1).ToList();
        for (var slot = 1; slot < Army.SlotCount; slot++)
        {
            hero.Army.Set(slot, new CreatureStack(creatures[slot - 1].Id, 1));
        }

        Assert.False(session.Execute(new CheatResourcesCommand(Blue, new ResourceSet(Gold: -1))).IsAccepted);
        Assert.False(session.Execute(new CheatResourcesCommand(Blue, ResourceSet.Empty)).IsAccepted);
        Assert.False(session.Execute(new CheatLevelsCommand(Blue, A, 11)).IsAccepted);
        Assert.False(session.Execute(new CheatCreaturesCommand(Blue, A, BuiltInCreatures.Footman, 0)).IsAccepted);
        var another = state.Creatures.All.First(c => c.Id != BuiltInCreatures.Footman && !creatures.Contains(c)).Id;
        Assert.Contains("no room", session.Execute(new CheatCreaturesCommand(Blue, A, another, 3)).Error, StringComparison.Ordinal);
        Assert.False(session.Execute(new CheatLevelsCommand(Blue, B, 1)).IsAccepted); // not the player's hero
    }

    [Fact]
    public void TheLaunch_RunsAGodotProjectInTheEditorsGodot_AndAProgramAsItIs()
    {
        var folder = Directory.CreateTempSubdirectory("ew-playtest-").FullName;
        try
        {
            var project = Path.Combine(folder, "game");
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, PlayTestLaunch.ProjectFile), "");
            var program = Path.Combine(folder, "Eternal Wars.exe");
            File.WriteAllText(program, "");
            var test = new PlayTestArguments(2, Reveal: true);

            var (fromProject, _) = PlayTestLaunch.For(project, "godot.exe", "map.ewmap.json", test);
            var (fromFile, _) = PlayTestLaunch.For(Path.Combine(project, PlayTestLaunch.ProjectFile), "godot.exe", "map.ewmap.json", test);
            var (fromProgram, _) = PlayTestLaunch.For(program, "godot.exe", "map.ewmap.json", test);

            Assert.Equal(new[] { "--path", project, "--", "--map=map.ewmap.json", "--player=2", "--reveal" }, fromProject!.Arguments);
            Assert.Equal(("godot.exe", "godot.exe"), (fromProject.Program, fromFile!.Program));
            Assert.Equal(fromProject.Arguments, fromFile.Arguments);
            Assert.Equal(program, fromProgram!.Program);
            Assert.Equal(["--", "--map=map.ewmap.json", "--player=2", "--reveal"], fromProgram.Arguments);
            Assert.StartsWith($"\"{program}\" -- ", fromProgram.Line, StringComparison.Ordinal); // a path with spaces is quoted

            Assert.Contains("Preferences", PlayTestLaunch.For("  ", "godot.exe", "m", test).Error, StringComparison.Ordinal);
            Assert.Contains("neither", PlayTestLaunch.For(Path.Combine(folder, "missing.exe"), "godot.exe", "m", test).Error, StringComparison.Ordinal);
            Assert.Contains(PlayTestLaunch.ProjectFile, PlayTestLaunch.For(folder, "godot.exe", "m", test).Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void APlayTest_IsNeverRanked()
    {
        var state = Map(new PlayTestArguments(1), "A........B");
        Start(state);

        Assert.False(ScoreRules.IsRanked(state));
    }
}
