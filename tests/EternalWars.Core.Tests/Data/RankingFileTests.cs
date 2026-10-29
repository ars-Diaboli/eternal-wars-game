using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Data;

/// <summary>The ranking tables (ADR-101, FLOW-4): <c>ranking.json</c> with each row's game record.</summary>
public sealed class RankingFileTests : IDisposable
{
    private static readonly DateTimeOffset When = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-ranking-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static RankingEntry Row(int score, string player = "P") =>
        new(player, TownIds.Haven, "Map", null, null, false, Complexity.Normal, null, 10, score, "Imp", "2026-10-29");

    /// <summary>Blue wins on day 1 with its Haven town against a computer Red; the record is the game's.</summary>
    private static (GameRecordDocument Record, GameState Finished) WonGame(GenerationInfo? generated = null)
    {
        var state = Create(
            "........", "........", "........", "........", "........", "........", "........",
            "...A...B");
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetPlayer(Red).IsHuman = false;
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var initial = MapConverter.ToDocument(state);
        var session = Start(state, revealAll: false);
        session.MoveTo(A, state.GetHero(B).Position);
        session.AutoResolve();
        Assert.True(state.IsOver);
        return (GameReplay.Record(initial, session, generated: generated), state);
    }

    [Fact]
    public void Insert_KeepsTheBestFirstAndANewRowBelowEqualScores()
    {
        var ranking = Ranking.Empty;
        ranking = RankingFile.Insert(ranking, RankingTable.Maps, Row(100, "first")).Ranking;
        ranking = RankingFile.Insert(ranking, RankingTable.Maps, Row(200)).Ranking;
        var (after, row) = RankingFile.Insert(ranking, RankingTable.Maps, Row(100, "second"));

        Assert.Equal(2, row);
        Assert.Equal(["P", "first", "second"], after.Maps.Select(r => r.Player));
        Assert.Empty(after.RandomMaps);
    }

    [Fact]
    public void Insert_KeepsTheBest20Rows()
    {
        var ranking = Ranking.Empty;
        for (var i = 1; i <= RankingFile.MaxRows; i++)
        {
            ranking = RankingFile.Insert(ranking, RankingTable.RandomMaps, Row(i * 10)).Ranking;
        }

        var (unchanged, none) = RankingFile.Insert(ranking, RankingTable.RandomMaps, Row(10));
        Assert.Null(none);
        Assert.Same(ranking, unchanged);

        var (after, row) = RankingFile.Insert(ranking, RankingTable.RandomMaps, Row(15));
        Assert.Equal(19, row);
        Assert.Equal(RankingFile.MaxRows, after.RandomMaps.Count);
        Assert.Equal(15, after.RandomMaps[^1].Score);
    }

    [Fact]
    public void TheFile_RoundTripsAndABrokenOrMissingFileIsEmpty()
    {
        var ranking = RankingFile.Insert(Ranking.Empty, RankingTable.Maps, Row(150) with { TurnTimerMinutes = 3, Replay = "ranking/x.ewreplay.json" }).Ranking;
        var path = Path.Combine(_folder, RankingFile.FileName);
        RankingFile.Save(path, ranking);

        var loaded = RankingFile.Load(path);
        Assert.Equal(ranking.Maps, loaded.Maps);
        Assert.Empty(loaded.RandomMaps);

        Assert.Empty(RankingFile.Read("{ not json").Maps);
        Assert.Empty(RankingFile.Read("{}").RandomMaps);
        Assert.Empty(RankingFile.Load(Path.Combine(_folder, "missing.json")).Maps);
    }

    [Fact]
    public void Entries_DescribeEachHumanWinner()
    {
        var (record, finished) = WonGame();

        var entry = Assert.Single(RankingFile.Entries(record, finished, custom: true, When, _ => "Ann"));
        Assert.Equal("Ann", entry.Player);
        Assert.Equal(TownIds.Haven, entry.Faction);
        Assert.Equal(record.Map.Name, entry.Map);
        Assert.Null(entry.Template);
        Assert.True(entry.Custom);
        Assert.Equal(1, entry.Days);
        Assert.Equal(258, entry.Score);
        Assert.Equal("Pit Lord", entry.Title);
        Assert.Equal("2026-10-29T12:00:00.0000000+00:00", entry.Date);
        Assert.Equal(RankingTable.Maps, RankingFile.TableOf(record));
    }

    [Fact]
    public void ARandomMapGame_GoesIntoTheRandomMapsTableWithItsTemplate()
    {
        var (record, finished) = WonGame(GenerationInfo.Of("duel", 36, 7, 0));

        var entry = Assert.Single(RankingFile.Entries(record, finished, custom: true, When));
        Assert.Equal("duel", entry.Template);
        Assert.Equal(36, entry.Size);
        Assert.False(entry.Custom);
        Assert.Equal("Blue", entry.Player);
        Assert.Equal(RankingTable.RandomMaps, RankingFile.TableOf(record));
    }

    [Fact]
    public void AddGame_WritesTheRowAndTheGamesRecord()
    {
        var (record, finished) = WonGame();

        var place = Assert.Single(RankingFile.AddGame(_folder, record, finished, custom: false, When));

        Assert.Equal(RankingTable.Maps, place.Table);
        Assert.Equal(0, place.Row);
        var saved = Assert.Single(RankingFile.Load(Path.Combine(_folder, RankingFile.FileName)).Maps);
        Assert.Equal(place.Entry, saved);
        var replay = GameRecordSerializer.Load(RankingFile.ReplayPath(_folder, saved)!);
        Assert.Equal(record.FinalDigest, GameStateDigest.Compute(GameReplay.Replay(replay).State));
    }

    [Fact]
    public void AddGame_DeletesTheRecordOfARowThatFallsOutOfTheTable()
    {
        var (record, finished) = WonGame();
        var ranking = Ranking.Empty;
        for (var i = 0; i < RankingFile.MaxRows; i++)
        {
            ranking = RankingFile.Insert(ranking, RankingTable.Maps, Row(1000 - i) with { Replay = $"ranking/old{i}.ewreplay.json" }).Ranking;
        }

        // The last row scores below the new game.
        ranking = ranking.With(RankingTable.Maps, [.. ranking.Maps.Take(RankingFile.MaxRows - 1), Row(1) with { Replay = "ranking/weak.ewreplay.json" }]);
        Directory.CreateDirectory(Path.Combine(_folder, RankingFile.ReplayFolder));
        File.WriteAllText(Path.Combine(_folder, "ranking/weak.ewreplay.json"), "{}");
        File.WriteAllText(Path.Combine(_folder, "ranking/old0.ewreplay.json"), "{}");
        RankingFile.Save(Path.Combine(_folder, RankingFile.FileName), ranking);

        var place = Assert.Single(RankingFile.AddGame(_folder, record, finished, custom: false, When));

        Assert.Equal(RankingFile.MaxRows - 1, place.Row);
        Assert.False(File.Exists(Path.Combine(_folder, "ranking/weak.ewreplay.json")));
        Assert.True(File.Exists(Path.Combine(_folder, "ranking/old0.ewreplay.json")));
        Assert.True(File.Exists(RankingFile.ReplayPath(_folder, place.Entry)));
    }

    [Fact]
    public void AGameThatDoesNotMakeTheTable_WritesNothing()
    {
        var (record, finished) = WonGame();
        var ranking = Ranking.Empty;
        for (var i = 0; i < RankingFile.MaxRows; i++)
        {
            ranking = RankingFile.Insert(ranking, RankingTable.Maps, Row(1000)).Ranking;
        }

        RankingFile.Save(Path.Combine(_folder, RankingFile.FileName), ranking);

        Assert.Empty(RankingFile.AddGame(_folder, record, finished, custom: false, When));
        Assert.False(Directory.Exists(Path.Combine(_folder, RankingFile.ReplayFolder)));
    }
}
