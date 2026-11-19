using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Data.Records;

/// <summary>The two autosave slots (ADR-058).</summary>
public enum AutosaveSlot
{
    /// <summary>The start of the current turn.</summary>
    Current,

    /// <summary>The start of the turn before.</summary>
    Previous,
}

/// <summary>One file of the save folder: its label, or why it could not be read.</summary>
public sealed record SaveEntry(string Path, SaveInfo? Info, string? Error);

/// <summary>A save replayed to where it was saved (<see cref="GameSaves.Load"/>), and its file.</summary>
public sealed record LoadedSave(string Path, GameSession Session, GameRecordDocument Record);

/// <summary>A save newer than the one Continue loaded, whose label reads but whose game does not load, and why (ADR-232).</summary>
public sealed record SkippedSave(SaveEntry Entry, string Error);

/// <summary>
/// Saved games (ADR-058): a save is the game record of ADR-036 (initial map, options, accepted commands) plus a label, so
/// it works at any moment, including in the middle of a battle. Loading replays the commands and checks the digest.
/// </summary>
public static class GameSaves
{
    public const string QuickSaveName = "quicksave";

    /// <summary>File names of the autosave slots; players cannot save under them.</summary>
    public const string AutosaveCurrentName = "autosave-current";

    public const string AutosavePreviousName = "autosave-previous";

    /// <summary>The autosave slot a file is, or null for an ordinary save.</summary>
    public static AutosaveSlot? SlotOf(string path) => Path.GetFileName(path) switch
    {
        var file when file.Equals(AutosaveCurrentName + GameRecordSerializer.SaveExtension, StringComparison.OrdinalIgnoreCase) => AutosaveSlot.Current,
        var file when file.Equals(AutosavePreviousName + GameRecordSerializer.SaveExtension, StringComparison.OrdinalIgnoreCase) => AutosaveSlot.Previous,
        _ => null,
    };

    /// <summary>
    /// Autosave at the start of a turn, with two slots: the new save becomes the current one and the one that was current
    /// becomes the previous one, so the start of this turn and of the turn before are always there. The new save is written
    /// completely before the slots are rotated. <paramref name="generated"/> is how a random map was made (null for a hand-made map),
    /// <paramref name="campaign"/> the campaign the game is a map of (ADR-241; null for a single game).
    /// </summary>
    public static void Autosave(string folder, MapDocument initialMap, GameSession session, GenerationInfo? generated = null, CampaignRecord? campaign = null)
    {
        Directory.CreateDirectory(folder);
        var current = PathFor(folder, AutosaveCurrentName);
        var temp = current + ".tmp";
        GameRecordSerializer.Save(GameReplay.Record(initialMap, session, "Autosave", generated, campaign), temp);
        if (File.Exists(current))
        {
            File.Move(current, PathFor(folder, AutosavePreviousName), overwrite: true);
        }

        File.Move(temp, current, overwrite: true);
    }

    /// <summary>A file name made of the player's text: characters a file name cannot hold become underscores.</summary>
    public static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('.', ' ');
        return cleaned.Length == 0 ? "save" : cleaned;
    }

    public static string PathFor(string folder, string name) => Path.Combine(folder, Sanitize(name) + GameRecordSerializer.SaveExtension);

    /// <summary>
    /// Writes the session as <paramref name="name"/> in <paramref name="folder"/>, replacing a save of that name; the old
    /// file stays until the new one is complete. <paramref name="initialMap"/> is the map the game started from, <paramref name="generated"/>
    /// how it was generated (null for a hand-made map), <paramref name="campaign"/> the campaign it is a map of (ADR-241).
    /// </summary>
    public static string Save(
        string folder, string name, MapDocument initialMap, GameSession session, GenerationInfo? generated = null, CampaignRecord? campaign = null) =>
        SaveRecord(folder, name, GameReplay.Record(initialMap, session, name.Trim(), generated, campaign));

    /// <summary>
    /// Writes a game's record as the save <paramref name="name"/> in <paramref name="folder"/>, like <see cref="Save"/>: a LAN client's save is the
    /// host's whole record (ADR-196).
    /// </summary>
    public static string SaveRecord(string folder, string name, GameRecordDocument record)
    {
        var path = PathFor(folder, name);
        if (SlotOf(path) is not null)
        {
            throw new ArgumentException($"'{name}' is reserved for the autosave.", nameof(name));
        }

        Directory.CreateDirectory(folder);
        var temp = path + ".tmp";
        GameRecordSerializer.Save(record, temp);
        File.Move(temp, path, overwrite: true);
        return path;
    }

    /// <summary>
    /// Writes the session's replay (ADR-101: "Save replay" on an end screen) as <paramref name="name"/><c>.ewreplay.json</c> in
    /// <paramref name="folder"/>, replacing one of that name; the same record as a save, without the save label.
    /// </summary>
    public static string SaveReplay(
        string folder, string name, MapDocument initialMap, GameSession session, GenerationInfo? generated = null, CampaignRecord? campaign = null) =>
        SaveReplayRecord(folder, name, GameReplay.Record(initialMap, session, generated: generated, campaign: campaign));

    /// <summary>Writes a game's record as the replay <paramref name="name"/>, like <see cref="SaveReplay"/> (a LAN client's is the host's, ADR-196).</summary>
    public static string SaveReplayRecord(string folder, string name, GameRecordDocument record)
    {
        var path = Path.Combine(folder, Sanitize(name) + GameRecordSerializer.FileExtension);
        Directory.CreateDirectory(folder);
        var temp = path + ".tmp";
        GameRecordSerializer.Save(record with { Save = null }, temp);
        File.Move(temp, path, overwrite: true);
        return path;
    }

    /// <summary>
    /// The colours a person plays in a loaded game and who are still in it (ADR-196): each is played on this computer, on another (a LAN game) or,
    /// from now on, by the computer (<see cref="Domain.Commands.HandToComputerCommand"/>).
    /// </summary>
    public static IReadOnlyList<PlayerId> People(GameState state) =>
        [.. state.Players.Where(p => p.IsHuman && !p.IsDefeated).Select(p => p.Id)];

    /// <summary>
    /// Replays a saved game to where it was saved; the session is started and mid-battle if it was saved there. Throws
    /// <see cref="InvalidOperationException"/> when the content has changed since (the digest differs) and
    /// <see cref="FormatException"/>/<see cref="System.Text.Json.JsonException"/> for damaged files. <paramref name="content"/> is the game's
    /// content tables (the built-in tables when left out).
    /// </summary>
    public static (GameSession Session, GameRecordDocument Record) Load(string path, ContentCatalogs? content = null)
    {
        var record = GameRecordSerializer.Load(path);
        return (GameReplay.Replay(record, content), record);
    }

    /// <summary>Replays a save (<see cref="Load"/>); null and why when the file is damaged or its content has changed since.</summary>
    public static LoadedSave? TryLoad(string path, ContentCatalogs? content, out string error)
    {
        try
        {
            var (session, record) = Load(path, content);
            error = "";
            return new LoadedSave(path, session, record);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or System.Text.Json.JsonException or IOException
                                      or KeyNotFoundException or NotSupportedException)
        {
            error = e.Message;
            return null;
        }
    }

    /// <summary>
    /// The main menu's Continue (ADR-232): the newest of <paramref name="entries"/> (in <see cref="List"/>'s order) that loads, with the newer ones
    /// whose label reads but whose game does not in <paramref name="skipped"/>; null when none loads. Files whose label cannot be read are passed
    /// over: they never offer Continue.
    /// </summary>
    public static LoadedSave? LoadNewest(IEnumerable<SaveEntry> entries, ContentCatalogs? content, out IReadOnlyList<SkippedSave> skipped)
    {
        var failed = new List<SkippedSave>();
        skipped = failed;
        foreach (var entry in entries.Where(e => e.Info is not null))
        {
            if (TryLoad(entry.Path, content, out var error) is { } loaded)
            {
                return loaded;
            }

            failed.Add(new SkippedSave(entry, error));
        }

        return null;
    }

    /// <summary>
    /// The saves in a folder, newest first, from their labels only (<see cref="GameRecordLabel.Read(string)"/>: the commands are not read, so a
    /// save that fails to load still lists); files whose label cannot be read are listed with their error. A missing folder is empty.
    /// </summary>
    public static IReadOnlyList<SaveEntry> List(string folder) => ListFiles(folder, GameRecordSerializer.SaveExtension);

    /// <summary>
    /// The replays in a folder (<c>*.ewreplay.json</c>, ADR-101), newest first, for the replay viewer's Replays tab: the label's name is the file
    /// name, its date the day the game reached; unreadable files are listed with their error. A missing folder is empty.
    /// </summary>
    public static IReadOnlyList<SaveEntry> ListReplays(string folder) => ListFiles(folder, GameRecordSerializer.FileExtension);

    private static IReadOnlyList<SaveEntry> ListFiles(string folder, string extension)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var entries = new List<(SaveEntry Entry, DateTime Written)>();
        foreach (var file in Directory.GetFiles(folder, "*" + extension))
        {
            var written = File.GetLastWriteTimeUtc(file);
            try
            {
                var label = GameRecordLabel.Read(file);
                var name = Path.GetFileName(file)[..^extension.Length];
                var info = label.Save ?? new SaveInfo(name, written.ToString("O", System.Globalization.CultureInfo.InvariantCulture), "", "", false,
                    label.MapName ?? throw new FormatException("The record's map has no name."));
                entries.Add((new SaveEntry(file, info, null), written));
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or FormatException or IOException or NotSupportedException)
            {
                entries.Add((new SaveEntry(file, null, e.Message), written));
            }
        }

        return entries.OrderByDescending(e => e.Written).Select(e => e.Entry).ToList();
    }

    public static void Delete(string path) => File.Delete(path);
}
