using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// Finding what a text names (ADR-257), where the editor asks for an object or a hero: a number is an id, anything else a name, matched without
/// case. The map keeps the id, so a later rename changes nothing.
/// </summary>
public static class ObjectNames
{
    /// <summary>The object with that id or name; null when there is none, or when several share the name.</summary>
    public static MapObject? Find(GameState state, string text)
    {
        ArgumentNullException.ThrowIfNull(state);
        text = text?.Trim() ?? "";
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return state.FindObject(new MapObjectId(id));
        }

        return state.Objects.Where(o => string.Equals(o.Name, text, StringComparison.OrdinalIgnoreCase)).ToList() is [var only] ? only : null;
    }

    /// <summary>The map hero with that id or name; null when there is none, or when several share the name.</summary>
    public static Hero? FindHero(GameState state, string text)
    {
        ArgumentNullException.ThrowIfNull(state);
        text = text?.Trim() ?? "";
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return state.Heroes.FirstOrDefault(h => h.Id.Value == id);
        }

        return state.Heroes.Where(h => string.Equals(h.Name, text, StringComparison.OrdinalIgnoreCase)).ToList() is [var only] ? only : null;
    }
}
