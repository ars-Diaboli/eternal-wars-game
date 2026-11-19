using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Tests.Domain;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Data;

/// <summary>
/// Going on with a loaded save (ADR-196, ADR-197, ADR-231) and an arena's rematch (ADR-159, ADR-199, ADR-231).
/// </summary>
public class SavedGameStartTests
{
    private static readonly GameOptions Simultaneous = new(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = true };

    [Fact]
    public void ASave_AsksWhoPlays_OnlyWhenMoreThanOnePersonIsStillInTheGame()
    {
        var two = Create("A...B");
        Assert.True(SavedGameStart.AsksWhoPlays(two));

        two.GetPlayer(Red).IsHuman = false;
        Assert.False(SavedGameStart.AsksWhoPlays(two));
    }

    [Fact]
    public void GoingOn_HandsTheChosenPeoplesColoursToTheComputer_ByCommand()
    {
        var session = Start(Create("A...B"));
        var commands = session.CommandLog.Count;

        SavedGameStart.GoOn(session, [Red, Red], lan: false); // the second time Red is the computer's already: nothing happens

        Assert.False(session.State.GetPlayer(Red).IsHuman);
        Assert.Equal(commands + 1, session.CommandLog.Count);
    }

    [Fact]
    public void ASimultaneousDay_GoesOnTurnByTurn_OnOneComputer_ButNotInALanGame()
    {
        var alone = SimultaneousDayTests.Started(SimultaneousDayTests.Duel(Simultaneous));
        var lan = SimultaneousDayTests.Started(SimultaneousDayTests.Duel(Simultaneous));
        Assert.True(alone.State.IsSimultaneous);

        SavedGameStart.GoOn(alone, [], lan: false);
        SavedGameStart.GoOn(lan, [], lan: true);

        Assert.False(alone.State.IsSimultaneous);
        Assert.True(lan.State.IsSimultaneous);
    }

    [Fact]
    public void ARematch_IsTheStartMapWithTheSameOptions_AndTheSeedGiven()
    {
        var state = Create(new GameOptions(TurnTimerMinutes: 3) { Seed = 7 }, "A...B");
        var initial = MapConverter.ToDocument(state);
        var finished = Start(state);
        finished.Execute(new Core.Domain.Commands.StepHeroCommand(Blue, A, Core.Domain.Common.Direction.East)).ThrowIfRejected();

        var rematch = GameRematch.State(initial, finished.State, GameContent(), seed: 99, keepControllers: false);

        Assert.Equal(finished.State.Options with { Seed = 99 }, rematch.Options);
        Assert.Equal(new Core.Domain.Common.MapPosition(0, 0), rematch.GetHero(A).Position); // the start map, not where the game ended
    }

    [Fact]
    public void ALanHostsRematch_KeepsWhoPlaysEachColour()
    {
        var state = Create("A...B");
        var initial = MapConverter.ToDocument(state);
        var finished = Start(state);
        finished.Execute(new Core.Domain.Commands.HandToComputerCommand(Red)).ThrowIfRejected();

        Assert.False(GameRematch.State(initial, finished.State, GameContent(), 1, keepControllers: true).GetPlayer(Red).IsHuman);
        Assert.Equal(
            MapConverter.ToGameState(initial, GameContent()).GetPlayer(Red).IsHuman,
            GameRematch.State(initial, finished.State, GameContent(), 1, keepControllers: false).GetPlayer(Red).IsHuman);
    }

    private static ContentCatalogs GameContent() => ContentCatalogs.Default;
}
