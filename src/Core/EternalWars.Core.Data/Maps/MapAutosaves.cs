namespace EternalWars.Core.Data.Maps;

/// <summary>The map editor's autosaves and its session marker (<see cref="AutosaveFolder"/>): map files named by their time.</summary>
public sealed class MapAutosaves(string folder, int keep = 5) : AutosaveFolder(folder, MapSerializer.FileExtension, keep)
{
    /// <summary>Writes <paramref name="document"/> as the autosave of <paramref name="now"/> and deletes all but the newest ones. Returns its path.</summary>
    public string Save(MapDocument document, DateTime now) => Save(path => MapSerializer.Save(document, path), now);

    /// <summary>When the map autosave at <paramref name="path"/> was made, from its name; null when the name is not an autosave's.</summary>
    public static DateTime? TimeOf(string path) => TimeOf(path, MapSerializer.FileExtension);
}
