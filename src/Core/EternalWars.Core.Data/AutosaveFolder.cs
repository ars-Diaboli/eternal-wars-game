using System.Globalization;

namespace EternalWars.Core.Data;

/// <summary>
/// An editor's autosaves and its session marker (EDIT-1, ADR-250), in one folder: the map editor's (<see cref="Maps.MapAutosaves"/>) and the
/// template editor's (<see cref="Templates.TemplateAutosaves"/>) each have their own. An autosave is a file of <see cref="Extension"/> named by
/// its time; only the newest <c>keep</c> stay. The marker is written when the editor starts (<see cref="BeginSession"/>) and removed when it
/// closes properly (<see cref="EndSession"/>): a marker found at start means the last session ended without closing. It holds the time since
/// which nothing unsaved could have been lost (<see cref="MarkSafe"/>: a save, another file opened), so only an autosave made after that time
/// is offered for recovery. Times are UTC and given by the caller.
/// </summary>
public abstract class AutosaveFolder(string folder, string extension, int keep)
{
    public const string MarkerName = "session.marker";

    private const string Prefix = "autosave-";
    private const string TimeFormat = "yyyyMMdd-HHmmss";

    private int _processId;
    private bool _owns;

    public string Folder { get; } = folder;

    /// <summary>The autosaves' extension, with its dot.</summary>
    public string Extension { get; } = extension;

    /// <summary>
    /// Starts the editor's session. Returns the autosave to offer: the newest one made after the safe time of a session that never ended,
    /// or null (the last session closed properly, or lost nothing). When the marker belongs to an editor that is still running
    /// (<paramref name="isRunning"/> of its process id), nothing is offered and the marker stays that editor's.
    /// </summary>
    public string? BeginSession(int processId, Func<int, bool> isRunning, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(isRunning);
        Directory.CreateDirectory(Folder);
        string? recoverable = null;
        if (File.Exists(MarkerPath))
        {
            var (owner, safe) = ReadMarker();
            if (owner is { } other && other != processId && isRunning(other))
            {
                return null;
            }

            recoverable = Files().FirstOrDefault(file => TimeOf(file, Extension) > safe);
        }

        _processId = processId;
        _owns = true;
        MarkSafe(now);
        return recoverable;
    }

    /// <summary>Nothing unsaved is older than <paramref name="now"/>: the file was saved, or another one was opened.</summary>
    public void MarkSafe(DateTime now)
    {
        if (_owns)
        {
            File.WriteAllLines(MarkerPath, [_processId.ToString(CultureInfo.InvariantCulture), now.Ticks.ToString(CultureInfo.InvariantCulture)]);
        }
    }

    /// <summary>The editor closes properly: the next start offers nothing.</summary>
    public void EndSession()
    {
        if (_owns)
        {
            File.Delete(MarkerPath);
            _owns = false;
        }
    }

    /// <summary>The autosaves, the newest first.</summary>
    public IReadOnlyList<string> Files() =>
        Directory.Exists(Folder)
            ? [.. Directory.GetFiles(Folder, Prefix + "*" + Extension).Where(file => TimeOf(file, Extension) is not null).OrderByDescending(file => TimeOf(file, Extension))]
            : [];

    /// <summary>When the autosave at <paramref name="path"/> was made, from its name; null when the name is not an autosave's of <paramref name="extension"/>.</summary>
    public static DateTime? TimeOf(string path, string extension)
    {
        var name = Path.GetFileName(path);
        if (!name.StartsWith(Prefix, StringComparison.Ordinal) || !name.EndsWith(extension, StringComparison.Ordinal))
        {
            return null;
        }

        var time = name[Prefix.Length..^extension.Length];
        return DateTime.TryParseExact(time, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var made)
            ? made
            : null;
    }

    /// <summary>Writes the autosave of <paramref name="now"/> through <paramref name="write"/> (given its path) and deletes all but the newest ones. Returns its path.</summary>
    protected string Save(Action<string> write, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(write);
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, Prefix + now.ToString(TimeFormat, CultureInfo.InvariantCulture) + Extension);
        write(path);
        foreach (var old in Files().Skip(keep))
        {
            File.Delete(old);
        }

        return path;
    }

    private string MarkerPath => Path.Combine(Folder, MarkerName);

    /// <summary>The marker's process id and safe time; a marker that cannot be read has no owner and was never safe.</summary>
    private (int? Owner, DateTime Safe) ReadMarker()
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(MarkerPath);
        }
        catch (IOException)
        {
            return (null, DateTime.MinValue);
        }

        int? owner = lines.Length > 0 && int.TryParse(lines[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        var safe = lines.Length > 1 && long.TryParse(lines[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            && ticks <= DateTime.MaxValue.Ticks
            ? new DateTime(ticks, DateTimeKind.Utc)
            : DateTime.MinValue;
        return (owner, safe);
    }
}
