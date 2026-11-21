namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// The editor's list of recently used files (EDIT-1, ADR-250): the newest first, each path once (whatever its letters' case), at most
/// <see cref="Capacity"/> of them. The lists are values: every change returns a new one for the front-end to keep.
/// </summary>
public static class RecentFiles
{
    /// <summary>
    /// A file's path as saved with <paramref name="extension"/> (<c>.ewmap.json</c>): the extension added to a name typed without it; a name
    /// that ends in ".json" alone, or in ".json" before the extension (a dialog that kept the ".json" of its suggested name and added its
    /// own, ADR-279), gets it once.
    /// </summary>
    public static string WithExtension(string path, string extension)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrEmpty(extension);
        const string Json = ".json";
        var doubled = Json + extension;
        if (path.EndsWith(doubled, StringComparison.OrdinalIgnoreCase) && !extension.Equals(Json, StringComparison.OrdinalIgnoreCase))
        {
            return path[..^doubled.Length] + extension;
        }

        if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return (path.EndsWith(Json, StringComparison.OrdinalIgnoreCase) ? path[..^Json.Length] : path) + extension;
    }

    /// <summary>
    /// Where a file picked in a save dialog is written: <see cref="WithExtension"/>, unless that names another file which
    /// <paramref name="exists"/> (the dialog asked about overwriting the picked one, not that one), then the picked path as it is.
    /// </summary>
    public static string SavePath(string picked, string extension, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var path = WithExtension(picked, extension);
        return path != picked && exists(path) ? picked : path;
    }

    public const int Capacity = 10;

    /// <summary><paramref name="recent"/> with <paramref name="path"/> first; the oldest falls off a full list.</summary>
    public static IReadOnlyList<string> With(IReadOnlyList<string> recent, string path) =>
        [path, .. Without(recent, path).Take(Capacity - 1)];

    /// <summary><paramref name="recent"/> with <paramref name="path"/> taken out (a file that is gone).</summary>
    public static IReadOnlyList<string> Without(IReadOnlyList<string> recent, string path) =>
        [.. recent.Where(other => !string.Equals(other, path, StringComparison.OrdinalIgnoreCase))];
}
