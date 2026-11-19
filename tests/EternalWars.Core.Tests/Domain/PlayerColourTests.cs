using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Scenarios;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>A player's colour apart from its number (ADR-225): chosen at setup, set when the game is created, kept in saves; no rule reads it.</summary>
public class PlayerColourTests
{
    private static readonly string[] Rows = ["A..", "..B"];

    [Fact]
    public void APlayer_IsDrawnInItsNumbersColour_ByDefault()
    {
        var state = Create(Rows);

        Assert.Equal(0, state.GetPlayer(Blue).Colour);
        Assert.Equal(1, state.GetPlayer(Red).Colour);
        Assert.Equal(Player.ColourCount, Player.ColourNames.Count);
    }

    [Fact]
    public void AChosenColour_IsTheGames_AndAPlayerNamedAfterItsColour_TakesTheNewName()
    {
        var state = Create(new GameOptions { Colours = new Dictionary<int, int> { [Blue.Value] = 1, [Red.Value] = 0 } }, Rows);

        Assert.Equal((1, "Red"), (state.GetPlayer(Blue).Colour, state.GetPlayer(Blue).Name));
        Assert.Equal((0, "Blue"), (state.GetPlayer(Red).Colour, state.GetPlayer(Red).Name));
    }

    [Fact]
    public void AColourOutOfRange_IsIgnored()
    {
        var state = Create(new GameOptions { Colours = new Dictionary<int, int> { [Blue.Value] = Player.ColourCount, [Red.Value] = -1 } }, Rows);

        Assert.Equal((0, "Blue"), (state.GetPlayer(Blue).Colour, state.GetPlayer(Blue).Name));
        Assert.Equal((1, "Red"), (state.GetPlayer(Red).Colour, state.GetPlayer(Red).Name));
    }

    [Fact]
    public void AMapsOwnPlayerName_StaysWithAnotherColour()
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var named = map with { Players = [.. map.Players.Select((p, i) => i == 0 ? p with { Name = "The Old Guard" } : p)] };
        

        var state = MapConverter.ToGameState(named, options: new GameOptions { Colours = new Dictionary<int, int> { [0] = 5 } });

        Assert.Equal((5, "The Old Guard"), (state.GetPlayer(new(0)).Colour, state.GetPlayer(new(0)).Name));
    }

    [Fact]
    public void TheColours_AreKeptInTheSave_AndChangeNoRule()
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var options = new GameOptions(Seed: 3) { Colours = new Dictionary<int, int> { [0] = 6 } };
        var session = new GameSession(MapConverter.ToGameState(map, options: options));
        session.Start();
        var plain = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 3)));
        plain.Start();

        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session))));

        Assert.Equal(6, replayed.State.GetPlayer(new(0)).Colour);
        Assert.Equal(GameStateDigest.Compute(plain.State), GameStateDigest.Compute(session.State));
    }

    [Fact]
    public void ADifficulty_IsDescribedByItsStartAndItsScoreFactor()
    {
        Assert.Equal(
            "Hard: every player starts with 5000 gold, 15 lumber, 15 ore, 3 crystal, 3 mercury, 3 sulfur; the score counts 130 %.",
            DifficultyText.Describe(Difficulty.Hard));
        Assert.Equal(["pawn", "knight", "bishop", "rook", "queen"], Enum.GetValues<Difficulty>().Select(DifficultyText.Piece));
    }
}
