using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The factions a player may take on a fixed map (ADR-257): the map's list for its slot, else every playable town. The new-game screen offers
/// these; a choice outside them, or none, is drawn among them when the game starts (<see cref="PlaceholderService"/>).
/// </summary>
public static class PlayerFactions
{
    /// <summary>The factions <paramref name="player"/> may take, in <see cref="BuiltInMapObjects.PlayableTowns"/>' order.</summary>
    public static IReadOnlyList<string> Allowed(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Factions ?? BuiltInMapObjects.PlayableTowns;
    }

    /// <summary>
    /// A list as the map keeps it: in <see cref="BuiltInMapObjects.PlayableTowns"/>' order, without repeats; null for null or every playable
    /// town. Throws <see cref="ArgumentException"/> for an empty list or a town that is not playable.
    /// </summary>
    public static IReadOnlyList<string>? Normalize(IEnumerable<string>? factions)
    {
        if (factions is null)
        {
            return null;
        }

        var chosen = factions.ToHashSet(StringComparer.Ordinal);
        if (chosen.FirstOrDefault(f => !BuiltInMapObjects.PlayableTowns.Contains(f)) is { } unknown)
        {
            throw new ArgumentException($"'{unknown}' is not a playable town.", nameof(factions));
        }

        if (chosen.Count == 0)
        {
            throw new ArgumentException("A player needs at least one faction it may take.", nameof(factions));
        }

        return chosen.Count == BuiltInMapObjects.PlayableTowns.Count ? null : [.. BuiltInMapObjects.PlayableTowns.Where(chosen.Contains)];
    }
}
