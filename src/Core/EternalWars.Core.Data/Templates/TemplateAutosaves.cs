using EternalWars.Core.Generation;

namespace EternalWars.Core.Data.Templates;

/// <summary>The template editor's autosaves and its session marker (<see cref="AutosaveFolder"/>): template files named by their time.</summary>
public sealed class TemplateAutosaves(string folder, int keep = 5) : AutosaveFolder(folder, TemplateSerializer.FileExtension, keep)
{
    /// <summary>Writes <paramref name="template"/> as the autosave of <paramref name="now"/> and deletes all but the newest ones. Returns its path.</summary>
    public string Save(MapTemplate template, DateTime now) => Save(path => TemplateSerializer.Save(template, path), now);

    /// <summary>When the template autosave at <paramref name="path"/> was made, from its name; null when the name is not an autosave's.</summary>
    public static DateTime? TimeOf(string path) => TimeOf(path, TemplateSerializer.FileExtension);
}
