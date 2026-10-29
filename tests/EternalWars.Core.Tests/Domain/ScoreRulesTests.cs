using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The victory screen's score and rank title (ADR-101, FLOW-4).</summary>
public class ScoreRulesTests
{
    /// <summary>
    /// Blue (A) below the entrance of its Haven town at the top left, Red (B, a computer) in the bottom right corner with a weak army; Blue's
    /// strong hero beats Red on day 1.
    /// </summary>
    private static GameState Duel(GameOptions? options = null, Action<GameState>? adjust = null)
    {
        var state = Create(options ?? GameOptions.Default,
            "..............", "..............", "..............", "..............", "..............", "..............", "..............",
            "...A.........B");
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetPlayer(Red).IsHuman = false;
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        adjust?.Invoke(state);
        return state;
    }

    private static GameState Won(GameState state)
    {
        var session = Start(state);
        session.MoveTo(A, state.GetHero(B).Position);
        session.AutoResolve();
        Assert.True(state.IsOver);
        return state;
    }

    [Theory]
    [InlineData(1, Complexity.Normal, null, 258)] // 199 × 1.3
    [InlineData(50, Complexity.Normal, null, 195)]
    [InlineData(195, Complexity.Beginner, null, 8)] // the minimum base 10 × 0.8
    [InlineData(10, Complexity.Expert, 1, 456)] // 190 × 2 × 1.2
    [InlineData(100, Complexity.Hard, 5, 176)] // 100 × 1.6 × 1.1
    [InlineData(100, Complexity.Easy, 10, 105)]
    [InlineData(100, Complexity.Easy, 11, 100)]
    public void TheScore_IsHoMM3sDaysTimesTheDifficultyAndTheTurnTimer(int days, Complexity difficulty, int? timer, int expected) =>
        Assert.Equal(expected, ScoreRules.Score(days, difficulty, timer));

    [Fact]
    public void MapDefault_CountsAsNormal()
    {
        Assert.Equal(Complexity.Normal, ScoreRules.Difficulty(GameOptions.Default));
        Assert.Equal(Complexity.Hard, ScoreRules.Difficulty(new GameOptions(Complexity: Complexity.Hard)));
    }

    [Fact]
    public void RankTitles_Are14CreaturesFromTheWeakestToADragon()
    {
        Assert.Equal(14, ScoreRules.Titles.Count);
        Assert.Equal(0, ScoreRules.Titles[0].MinScore);
        Assert.True(ScoreRules.Titles.Zip(ScoreRules.Titles.Skip(1)).All(p => p.First.MinScore < p.Second.MinScore));
        Assert.All(ScoreRules.Titles, t => Assert.True(CreatureCatalog.Default.Contains(t.Creature), t.Creature));
        Assert.Contains("dragon", ScoreRules.Titles[^1].Creature);

        Assert.Equal("inferno.imp", ScoreRules.Title(0));
        Assert.Equal("inferno.imp", ScoreRules.Title(19));
        Assert.Equal("stronghold.goblin", ScoreRules.Title(20));
        Assert.Equal("dragon-utopia.black-dragon", ScoreRules.Title(1000));
    }

    [Fact]
    public void AHumanWinner_ScoresWithItsStartTownAndTheDay()
    {
        var state = Won(Duel());

        var score = Assert.Single(ScoreRules.Scores(state));
        Assert.Equal(Blue, score.Player);
        Assert.Equal(TownIds.Haven, score.Faction);
        Assert.Equal(1, score.Days);
        Assert.Equal(Complexity.Normal, score.Difficulty);
        Assert.Equal(258, score.Score);
        Assert.Equal("inferno.pit-lord", score.Title);
    }

    [Fact]
    public void TheComplexityAndTheTurnTimer_CountInTheScore()
    {
        var state = Won(Duel(new GameOptions(Complexity: Complexity.Expert, TurnTimerMinutes: 2)));

        var score = Assert.Single(ScoreRules.Scores(state));
        Assert.Equal(Complexity.Expert, score.Difficulty);
        Assert.Equal(2, score.TurnTimerMinutes);
        Assert.Equal(477, score.Score); // 199 × 2 × 1.2
        Assert.Equal("dragon-utopia.black-dragon", score.Title);
    }

    [Fact]
    public void AComputerPlayerStartingWithoutATown_KeepsTheGameRanked()
    {
        var state = Won(Duel());

        Assert.True(ScoreRules.IsRanked(state));
        Assert.Null(state.GetPlayer(Red).StartTown);
    }

    [Fact]
    public void AHumanPlayerStartingWithoutATown_MakesTheMapCampaignLikeAndNotRanked()
    {
        var state = Won(Duel(adjust: s => s.GetPlayer(Red).IsHuman = true));

        Assert.False(ScoreRules.IsRanked(state));
        Assert.Empty(ScoreRules.Scores(state));
    }

    [Fact]
    public void TheStartTown_IsTheTownOwnedWhenTheGameStarts()
    {
        var state = Duel(adjust: s => s.GetPlayer(Red).IsHuman = true);
        Start(state);

        Assert.Equal(TownIds.Haven, state.GetPlayer(Blue).StartTown);
        Assert.Null(state.GetPlayer(Red).StartTown);
        Assert.False(ScoreRules.IsRanked(state));
    }

    [Fact]
    public void AGameWonOnlyByAComputerPlayer_DoesNotScore()
    {
        var state = Won(Duel());
        state.GetPlayer(Blue).IsHuman = false;

        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Empty(ScoreRules.Scores(state));
    }

    [Fact]
    public void AGameStillGoing_DoesNotScore()
    {
        var state = Duel();
        Start(state);

        Assert.Empty(ScoreRules.Scores(state));
    }

    [Fact]
    public void EveryHumanWinner_GetsAScore()
    {
        var green = new PlayerId(2);
        var state = Duel(adjust: s =>
        {
            s.AddPlayer(new Player(green, "Green", true, new TeamId(Blue.Value)));
            AddTown(s, TownIds.Necropolis, 7, 0, green);
        });
        Won(state);

        Assert.Equal([Blue, green], ScoreRules.Scores(state).Select(s => s.Player));
        Assert.Equal(TownIds.Necropolis, ScoreRules.Scores(state)[1].Faction);
    }
}
