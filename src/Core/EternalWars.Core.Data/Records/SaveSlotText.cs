using System.Globalization;

namespace EternalWars.Core.Data.Records;

/// <summary>One line of the save menu or the replay list: the file, what it says, the name a save can be overwritten under, and whether it loads.</summary>
/// <param name="Path">The file.</param>
/// <param name="Label">What the list shows.</param>
/// <param name="Name">The save's name, to save over it; null for an autosave slot or an unreadable file.</param>
/// <param name="Loadable">The file could be read.</param>
public sealed record SaveSlotLine(string Path, string Label, string? Name, bool Loadable);

/// <summary>Saved games and replays in words (ADR-058, ADR-101): the save menu's and the replay list's lines, the main menu's "Continue".</summary>
public static class SaveSlotText
{
    /// <summary>
    /// The lines for a folder's files (<see cref="GameSaves.List"/> or <see cref="GameSaves.ListReplays"/>), in their order; the save times in
    /// <paramref name="zone"/> (null: the computer's).
    /// </summary>
    public static List<SaveSlotLine> Lines(IReadOnlyList<SaveEntry> entries, TimeZoneInfo? zone = null) => entries
        .Select(e => e.Info is { } info
            ? new SaveSlotLine(e.Path, info.Date.Length == 0
                ? $"{info.Name}   {info.Map}   ({LocalTime(info.SavedAt, zone)})" // a replay: no save label
                : $"{Title(e.Path, info)}   {info.Map}, {info.Date}, {info.Player}'s turn{(info.InBattle ? ", in battle" : string.Empty)}   ({LocalTime(info.SavedAt, zone)})",
                GameSaves.SlotOf(e.Path) is null ? info.Name : null, true)
            : new SaveSlotLine(e.Path, $"{Path.GetFileName(e.Path)}   unreadable: {e.Error}", null, false))
        .ToList();

    /// <summary>The main menu's "Continue" line for the latest readable save; null when there is none.</summary>
    public static string? Continue(SaveEntry? latest, TimeZoneInfo? zone = null) =>
        latest?.Info is { } info ? $"{Title(latest.Path, info)}, {info.Map}, {info.Date} ({LocalTime(info.SavedAt, zone)})" : null;

    /// <summary>A save's title: what an autosave slot holds, else the name it was saved under.</summary>
    public static string Title(string path, SaveInfo info) => GameSaves.SlotOf(path) switch
    {
        AutosaveSlot.Current => "Autosave: start of the latest turn",
        AutosaveSlot.Previous => "Autosave: start of the turn before",
        _ => info.Name,
    };

    /// <summary>An ISO 8601 time as <c>yyyy-MM-dd HH:mm</c> in <paramref name="zone"/> (null: the computer's); the text as it is when it does not parse.</summary>
    public static string LocalTime(string iso, TimeZoneInfo? zone = null) =>
        DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? (zone is null ? time.ToLocalTime() : TimeZoneInfo.ConvertTime(time, zone)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : iso;

    /// <summary>The status line after loading <paramref name="path"/>: the save's name, and for a random map its template, size and seed.</summary>
    public static string Loaded(string path, GameRecordDocument record) =>
        $"Loaded '{record.Save?.Name ?? Path.GetFileNameWithoutExtension(path)}'." +
        (record.Generated is { } generated ? $" {generated.Template} {generated.Size}×{generated.Size}, seed {generated.Seed}. {generated.VersionWarning}" : "");
}
