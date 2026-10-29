using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
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
    /// completely before the slots are rotated. <paramref name="generated"/> is how a random map was made (null for a hand-made map).
    /// </summary>
    public static void Autosave(string folder, MapDocument initialMap, GameSession session, GenerationInfo? generated = null)
    {
        Directory.CreateDirectory(folder);
        var current = PathFor(folder, AutosaveCurrentName);
        var temp = current + ".tmp";
        GameRecordSerializer.Save(GameReplay.Record(initialMap, session, "Autosave", generated), temp);
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
    /// how it was generated (null for a hand-made map).
    /// </summary>
    public static string Save(string folder, string name, MapDocument initialMap, GameSession session, GenerationInfo? generated = null)
    {
        var path = PathFor(folder, name);
        if (SlotOf(path) is not null)
        {
            throw new ArgumentException($"'{name}' is reserved for the autosave.", nameof(name));
        }

        Directory.CreateDirectory(folder);
        var temp = path + ".tmp";
        GameRecordSerializer.Save(GameReplay.Record(initialMap, session, name.Trim(), generated), temp);
        File.Move(temp, path, overwrite: true);
        return path;
    }

    /// <summary>
    /// Writes the session's replay (ADR-101: "Save replay" on an end screen) as <paramref name="name"/><c>.ewreplay.json</c> in
    /// <paramref name="folder"/>, replacing one of that name; the same record as a save, without the save label.
    /// </summary>
    public static string SaveReplay(string folder, string name, MapDocument initialMap, GameSession session, GenerationInfo? generated = null)
    {
        var path = Path.Combine(folder, Sanitize(name) + GameRecordSerializer.FileExtension);
        Directory.CreateDirectory(folder);
        var temp = path + ".tmp";
        GameRecordSerializer.Save(GameReplay.Record(initialMap, session, generated: generated), temp);
        File.Move(temp, path, overwrite: true);
        return path;
    }

    /// <summary>
    /// Replays a saved game to where it was saved; the session is started and mid-battle if it was saved there. Throws
    /// <see cref="InvalidOperationException"/> when the content has changed since (the digest differs) and
    /// <see cref="FormatException"/>/<see cref="System.Text.Json.JsonException"/> for damaged files.
    /// </summary>
    public static (GameSession Session, GameRecordDocument Record) Load(
        string path,
        MapObjectCatalog? catalog = null,
        CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null,
        BuildingCatalog? buildings = null,
        TerrainCatalog? terrains = null,
        ArtifactCatalog? artifacts = null)
    {
        var record = GameRecordSerializer.Load(path);
        return (GameReplay.Replay(record, catalog, creatures, heroRoster, buildings, terrains, artifacts), record);
    }

    /// <summary>The saves in a folder, newest first; unreadable files are listed with their error. A missing folder is empty.</summary>
    public static IReadOnlyList<SaveEntry> List(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var entries = new List<(SaveEntry Entry, DateTime Written)>();
        foreach (var file in Directory.GetFiles(folder, "*" + GameRecordSerializer.SaveExtension))
        {
            var written = File.GetLastWriteTimeUtc(file);
            try
            {
                var record = GameRecordSerializer.Load(file);
                var info = record.Save ?? new SaveInfo(Path.GetFileName(file), written.ToString("O", System.Globalization.CultureInfo.InvariantCulture), "", "", false, record.Map.Name);
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
