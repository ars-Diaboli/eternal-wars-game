namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// The editor's list of recently used files (EDIT-1, ADR-250): the newest first, each path once (whatever its letters' case), at most
/// <see cref="Capacity"/> of them. The lists are values: every change returns a new one for the front-end to keep.
/// </summary>
public static class RecentFiles
{
    public const int Capacity = 10;

    /// <summary><paramref name="recent"/> with <paramref name="path"/> first; the oldest falls off a full list.</summary>
    public static IReadOnlyList<string> With(IReadOnlyList<string> recent, string path) =>
        [path, .. Without(recent, path).Take(Capacity - 1)];

    /// <summary><paramref name="recent"/> with <paramref name="path"/> taken out (a file that is gone).</summary>
    public static IReadOnlyList<string> Without(IReadOnlyList<string> recent, string path) =>
        [.. recent.Where(other => !string.Equals(other, path, StringComparison.OrdinalIgnoreCase))];
}
