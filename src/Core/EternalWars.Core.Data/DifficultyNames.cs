using System.Text.Json.Nodes;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Data;

/// <summary>
/// The difficulty's names before ADR-225 (maps before v41, saves before v5, ranking files before v2): the same five levels, each named one step
/// lower (Beginner … Expert). An older file's name is the level at its place.
/// </summary>
internal static class DifficultyNames
{
    private static readonly string[] Old = ["Beginner", "Easy", "Normal", "Hard", "Expert"];

    /// <summary>The current name of the level an older file calls <paramref name="name"/>; null for a name the old files never had.</summary>
    public static string? FromOld(string name) =>
        Array.FindIndex(Old, o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase)) is var index and >= 0
            ? Enum.GetName((Difficulty)index)
            : null;

    /// <summary>Renames <paramref name="obj"/>'s difficulty at <paramref name="property"/>, written with an old name, to its current name; anything else stays.</summary>
    public static void Upgrade(JsonObject obj, string property)
    {
        if (obj[property] is JsonValue value && value.TryGetValue<string>(out var name) && FromOld(name) is { } current)
        {
            obj[property] = current;
        }
    }
}
