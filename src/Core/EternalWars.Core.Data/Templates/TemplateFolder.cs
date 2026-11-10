using EternalWars.Core.Generation;

namespace EternalWars.Core.Data.Templates;

/// <summary>A folder of template files (<c>*.ewtemplate.json</c>), such as the one the editor's template editor saves to (ADR-117).</summary>
public static class TemplateFolder
{
    /// <summary>
    /// The valid templates in <paramref name="directory"/>, by file name (ordinal); none when it does not exist. A file that cannot be read or
    /// does not pass the template checks is skipped, with a <paramref name="warn"/> saying why.
    /// </summary>
    public static List<(string Path, MapTemplate Template)> LoadValid(string directory, Action<string> warn)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var found = new List<(string, MapTemplate)>();
        foreach (var file in Directory.GetFiles(directory, "*" + TemplateSerializer.FileExtension).Order(StringComparer.Ordinal))
        {
            try
            {
                var template = TemplateSerializer.Load(file);
                if (TemplateValidator.IsValid(template))
                {
                    found.Add((file, template));
                }
                else
                {
                    warn($"Skipping template {file}: it does not pass the template checks");
                }
            }
            catch (Exception e) when (e is FormatException or NotSupportedException or IOException)
            {
                warn($"Skipping template {file}: {e.Message}");
            }
        }

        return found;
    }
}
