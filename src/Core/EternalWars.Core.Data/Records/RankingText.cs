using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Data.Records;

/// <summary>One ranking row in words, as the victory screen and the leaderboard show it (ADR-101); <see cref="Highlight"/> marks a row to mark.</summary>
public sealed record RankingRowText(
    int Rank, string Player, string Faction, string Map, string Difficulty, int Days, int Score, string Title, string Date, bool Highlight);

/// <summary>A ranking table in words (ADR-101): its name and its rows, best first.</summary>
public static class RankingText
{
    /// <summary>The table's name.</summary>
    public static string TableName(RankingTable table) => table == RankingTable.RandomMaps ? "Random maps" : "Maps";

    /// <summary>
    /// The rows, ranked from 1; <paramref name="highlight"/> are the indexes of the rows to mark. Dates are shown in <paramref name="zone"/>
    /// (null: the computer's).
    /// </summary>
    public static List<RankingRowText> Rows(IReadOnlyList<RankingEntry> rows, IReadOnlySet<int> highlight, TimeZoneInfo? zone = null) =>
        rows.Select((e, i) => new RankingRowText(
            i + 1, e.Player, FactionName(e.Faction), MapLabel(e), e.Difficulty.ToString(), e.Days, e.Score, e.Title,
            SaveSlotText.LocalTime(e.Date, zone), highlight.Contains(i))).ToList();

    /// <summary>A random map's template and size, else the map's name, marked when it was made in the editor.</summary>
    public static string MapLabel(RankingEntry e) =>
        e.Template is { } template ? $"{template} {e.Size}×{e.Size}" : e.Custom ? $"{e.Map} (custom)" : e.Map;

    /// <summary>The name of a town type (the faction), or the id when the catalog does not know it.</summary>
    public static string FactionName(string town) =>
        MapObjectCatalog.Default.TryGet(BuiltInMapObjects.TownId(town), out var definition) ? definition.Name : town;
}
