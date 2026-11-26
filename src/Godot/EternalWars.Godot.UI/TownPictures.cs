using System.Collections.Generic;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The town pictures in <c>res://shared/textures/ui/towns/</c> (docs/content/prompts/town-picture-prompts.md): per town type a wide painted
/// view, <c>town_&lt;id&gt;.png</c> with its walls and <c>town_&lt;id&gt;_village.png</c> without (a town without a fort), 260 × 128 for the
/// town list's 130 × 64 entries. Loaded once and cached; a missing file gives null (a village falls back to the walled picture), and callers
/// keep the faction's crest.
/// </summary>
public static class TownPictures
{
    private const string Folder = "res://shared/textures/ui/towns/";
    private static readonly Dictionary<string, Texture2D?> Cache = [];

    /// <summary>The picture of <paramref name="townType"/>, as a <paramref name="village"/> or walled, or null.</summary>
    public static Texture2D? Of(string? townType, bool village)
    {
        if (string.IsNullOrEmpty(townType))
        {
            return null;
        }

        return (village ? Load($"town_{townType}_village") : null) ?? Load($"town_{townType}");
    }

    private static Texture2D? Load(string file)
    {
        if (!Cache.TryGetValue(file, out var texture))
        {
            var path = $"{Folder}{file}.png";
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Cache[file] = texture;
        }

        return texture;
    }
}
