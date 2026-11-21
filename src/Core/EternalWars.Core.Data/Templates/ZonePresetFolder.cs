using System.Text.Json;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Data.Templates;

/// <summary>On-disk form of a user's zone preset (<c>*.ewzone.json</c>, ADR-273): a name and a zone in the template format.</summary>
public sealed record ZonePresetDocument
{
    /// <summary>The template format (<see cref="MapTemplate.CurrentFormatVersion"/>) the zone is written in.</summary>
    public int FormatVersion { get; init; } = MapTemplate.CurrentFormatVersion;

    public required string Name { get; init; }

    public required TemplateZone Zone { get; init; }
}

/// <summary>A folder of zone presets, the template editor's <c>user://template-presets/</c> (ADR-273).</summary>
public static class ZonePresetFolder
{
    public const string FileExtension = ".ewzone.json";

    /// <summary>
    /// The presets in <paramref name="directory"/>, by name (ignoring case); none when it does not exist. A file that is no preset, or one
    /// from a newer game, is left out.
    /// </summary>
    public static IReadOnlyList<ZonePreset> All(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var presets = new List<ZonePreset>();
        foreach (var file in Directory.GetFiles(directory, "*" + FileExtension))
        {
            try
            {
                presets.Add(Deserialize(File.ReadAllText(file)));
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
            {
                // left out: the list shows what can be used
            }
        }

        return [.. presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The file a preset named <paramref name="name"/> is kept in: characters a file name cannot have become <c>_</c>, and no name is "preset".</summary>
    public static string PathOf(string directory, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim().TrimEnd('.');
        return Path.Combine(directory, (safe.Length == 0 ? ZonePresets.PresetId : safe) + FileExtension);
    }

    /// <summary>Writes the preset (over one of that name), making the folder. Returns the file's path.</summary>
    public static string Save(string directory, ZonePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        Directory.CreateDirectory(directory);
        var path = PathOf(directory, preset.Name);
        File.WriteAllText(path, Serialize(preset));
        return path;
    }

    /// <summary>
    /// The file that holds the preset named <paramref name="name"/> (ignoring case): the one whose name is it, else one renamed by hand
    /// that holds it; null when none does.
    /// </summary>
    public static string? Find(string directory, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var path = PathOf(directory, name);
        if (File.Exists(path) || !Directory.Exists(directory))
        {
            return File.Exists(path) ? path : null;
        }

        foreach (var file in Directory.GetFiles(directory, "*" + FileExtension))
        {
            try
            {
                if (string.Equals(Deserialize(File.ReadAllText(file)).Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
            {
                // not this one
            }
        }

        return null;
    }

    /// <summary>Deletes the preset's file (<see cref="Find"/>); false when there is none.</summary>
    public static bool Delete(string directory, string name)
    {
        if (Find(directory, name) is not { } path)
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public static string Serialize(ZonePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return JsonSerializer.Serialize(new ZonePresetDocument { Name = preset.Name, Zone = ZonePresets.Settings(preset.Zone) }, JsonDefaults.Options);
    }

    /// <summary>Throws <see cref="FormatException"/> for a file that is no preset, or one from a newer game.</summary>
    public static ZonePreset Deserialize(string json)
    {
        ZonePresetDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ZonePresetDocument>(json, JsonDefaults.Options);
        }
        catch (JsonException e)
        {
            throw new FormatException($"Not a zone preset: {e.Message}", e);
        }

        if (document is null || string.IsNullOrWhiteSpace(document.Name) || document.Zone is null)
        {
            throw new FormatException("Not a zone preset: it needs a name and a zone.");
        }

        if (document.FormatVersion > MapTemplate.CurrentFormatVersion)
        {
            throw new FormatException($"The preset is of template format v{document.FormatVersion}, newer than v{MapTemplate.CurrentFormatVersion}.");
        }

        return new ZonePreset(document.Name.Trim(), ZonePresets.Settings(document.Zone));
    }
}
