using System.Globalization;
using System.Text.Json;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Data.Records;

/// <summary>The two ranking tables (ADR-101): random maps, and the maps shipped with the game together with the maps made in the editor.</summary>
public enum RankingTable
{
    RandomMaps,
    Maps,
}

/// <summary>One row of a ranking table (ADR-101, FLOW-4 g).</summary>
/// <param name="Player">The name the winner gave on the victory screen.</param>
/// <param name="Faction">The town type the winner started with.</param>
/// <param name="Map">The map's name.</param>
/// <param name="Template">The template of a random map; null for a hand-made map.</param>
/// <param name="Size">The size of a random map.</param>
/// <param name="Custom">A map made in the editor, not shipped with the game (the maps table only).</param>
/// <param name="Difficulty">The complexity the score counted.</param>
/// <param name="TurnTimerMinutes">The game's turn timer; null is off.</param>
/// <param name="Days">The day the game was won.</param>
/// <param name="Score">The points.</param>
/// <param name="Title">The rank title: the creature's name.</param>
/// <param name="Date">When the game was won, UTC, ISO 8601.</param>
/// <param name="Replay">The game's record, relative to the ranking folder (<see cref="RankingFile.ReplayFolder"/>).</param>
public sealed record RankingEntry(
    string Player,
    string Faction,
    string Map,
    string? Template,
    int? Size,
    bool Custom,
    Complexity Difficulty,
    int? TurnTimerMinutes,
    int Days,
    int Score,
    string Title,
    string Date,
    string? Replay = null);

/// <summary>Both ranking tables, best first.</summary>
public sealed record Ranking(IReadOnlyList<RankingEntry> RandomMaps, IReadOnlyList<RankingEntry> Maps)
{
    public static Ranking Empty { get; } = new([], []);

    public IReadOnlyList<RankingEntry> this[RankingTable table] => table == RankingTable.RandomMaps ? RandomMaps : Maps;

    public Ranking With(RankingTable table, IReadOnlyList<RankingEntry> rows) =>
        table == RankingTable.RandomMaps ? this with { RandomMaps = rows } : this with { Maps = rows };
}

/// <summary>A row a won game added: which table and where (0 is the best), for the victory screen to highlight.</summary>
public sealed record RankingPlace(RankingTable Table, int Row, RankingEntry Entry);

/// <summary>
/// The ranking tables (ADR-101, FLOW-4): <c>ranking.json</c> in the user data folder, kept per computer, the best <see cref="MaxRows"/> games
/// per table. Each row keeps its game's record in <see cref="ReplayFolder"/> (a game with several human winners shares one file), so "Save
/// replay" can copy it out later; a record no row names any more is deleted. A missing or unreadable file is an empty ranking.
/// </summary>
public static class RankingFile
{
    public const string FileName = "ranking.json";

    public const string ReplayFolder = "ranking";

    public const int MaxRows = 20;

    public static Ranking Load(string path)
    {
        try
        {
            return File.Exists(path) ? Read(File.ReadAllText(path)) : Ranking.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Ranking.Empty;
        }
    }

    public static void Save(string path, Ranking ranking)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, Write(ranking));
        File.Move(temp, path, overwrite: true);
    }

    public static string Write(Ranking ranking) => JsonSerializer.Serialize(ranking, JsonDefaults.Options);

    /// <summary>The ranking in <paramref name="json"/>; an unreadable text is an empty ranking.</summary>
    public static Ranking Read(string json)
    {
        try
        {
            var read = JsonSerializer.Deserialize<Ranking>(json, JsonDefaults.Options);
            return read is null ? Ranking.Empty : new Ranking(read.RandomMaps ?? [], read.Maps ?? []);
        }
        catch (JsonException)
        {
            return Ranking.Empty;
        }
    }

    /// <summary>The table a game goes into: random maps when it was generated, the maps table otherwise.</summary>
    public static RankingTable TableOf(GameRecordDocument record) => record.Generated is null ? RankingTable.Maps : RankingTable.RandomMaps;

    /// <summary>
    /// The rows of a finished game, one per human winner (<see cref="ScoreRules.Scores"/>); none for a game that does not score.
    /// <paramref name="names"/> gives each winner's name (the victory screen asks; default: the player's name); <paramref name="custom"/> marks a
    /// hand-made map that was not shipped with the game.
    /// </summary>
    public static IReadOnlyList<RankingEntry> Entries(
        GameRecordDocument record, GameState finished, bool custom, DateTimeOffset when, Func<PlayerId, string>? names = null)
    {
        var date = when.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return ScoreRules.Scores(finished)
            .Select(s => new RankingEntry(
                names?.Invoke(s.Player) ?? finished.GetPlayer(s.Player).Name,
                s.Faction,
                record.Map.Name,
                record.Generated?.Template,
                record.Generated?.Size,
                custom && record.Generated is null,
                s.Difficulty,
                s.TurnTimerMinutes,
                s.Days,
                s.Score,
                finished.Creatures.TryGet(s.Title, out var creature) ? creature.Name : s.Title,
                date))
            .ToList();
    }

    /// <summary>
    /// Puts <paramref name="entry"/> into the table below the rows with the same or a higher score and keeps the best <see cref="MaxRows"/>.
    /// The row it got, or null when it did not make the table.
    /// </summary>
    public static (Ranking Ranking, int? Row) Insert(Ranking ranking, RankingTable table, RankingEntry entry)
    {
        var rows = ranking[table].ToList();
        var row = rows.TakeWhile(r => r.Score >= entry.Score).Count();
        if (row >= MaxRows)
        {
            return (ranking, null);
        }

        rows.Insert(row, entry);
        return (ranking.With(table, rows.Take(MaxRows).ToList()), row);
    }

    /// <summary>
    /// Adds a finished game to the ranking in <paramref name="folder"/> (the user data folder): its rows (<see cref="Entries"/>), the game's
    /// record when a row made the table, and the deletion of the records no row names any more. The places it got, best first; empty when the
    /// game does not score or no row made the table.
    /// </summary>
    public static IReadOnlyList<RankingPlace> AddGame(
        string folder, GameRecordDocument record, GameState finished, bool custom, DateTimeOffset when, Func<PlayerId, string>? names = null)
    {
        var entries = Entries(record, finished, custom, when, names);
        if (entries.Count == 0)
        {
            return [];
        }

        var path = Path.Combine(folder, FileName);
        var before = Load(path);
        var table = TableOf(record);
        var replay = $"{ReplayFolder}/{Guid.NewGuid():N}{GameRecordSerializer.FileExtension}";
        var ranking = before;
        foreach (var entry in entries)
        {
            ranking = Insert(ranking, table, entry with { Replay = replay }).Ranking;
        }

        var places = ranking[table].Select((e, row) => (e, row)).Where(r => r.e.Replay == replay)
            .Select(r => new RankingPlace(table, r.row, r.e)).ToList();
        if (places.Count == 0)
        {
            return [];
        }

        var replayPath = Path.Combine(folder, replay);
        Directory.CreateDirectory(Path.GetDirectoryName(replayPath)!);
        GameRecordSerializer.Save(record, replayPath);
        Save(path, ranking);
        DeleteUnused(folder, before, ranking);
        return places;
    }

    /// <summary>The full path of a row's record, or null when it has none.</summary>
    public static string? ReplayPath(string folder, RankingEntry entry) => entry.Replay is { } replay ? Path.Combine(folder, replay) : null;

    private static void DeleteUnused(string folder, Ranking before, Ranking after)
    {
        var kept = after.RandomMaps.Concat(after.Maps).Select(e => e.Replay).ToHashSet();
        foreach (var replay in before.RandomMaps.Concat(before.Maps).Select(e => e.Replay).OfType<string>().Distinct().Where(r => !kept.Contains(r)))
        {
            try
            {
                File.Delete(Path.Combine(folder, replay));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A record left behind takes some disk space and nothing else.
            }
        }
    }
}
