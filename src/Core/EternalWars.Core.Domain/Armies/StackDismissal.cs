using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Armies;

/// <summary>
/// Dismissing a stack (TOWN-17 i, ADR-296): the creatures leave for good, nothing is paid back. A hero on the map never dismisses its last stack;
/// a garrison may be emptied (a garrison hero waits without troops). Only on the owner's turn with no question open.
/// </summary>
public static class StackDismissal
{
    /// <summary>Why the player cannot dismiss the stack in <paramref name="slot"/> of its hero <paramref name="hero"/>, or null.</summary>
    public static string? Check(GameState state, PlayerId player, Hero hero, int slot)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(hero);
        if (hero.Owner != player || !state.Heroes.Contains(hero))
        {
            return $"{hero.Name} is not one of your heroes on the map.";
        }

        if (CheckSlot(state, hero.Army, slot) is { } refusal)
        {
            return refusal;
        }

        return hero.Army.Stacks.Count() <= 1 ? $"{hero.Name} cannot dismiss its last stack." : null;
    }

    /// <summary>Why the player cannot dismiss the stack in <paramref name="slot"/> of its town <paramref name="town"/>'s garrison, or null.</summary>
    public static string? Check(GameState state, PlayerId player, Town town, int slot)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(town);
        return town.Owner != player ? $"{town.Name} does not belong to {player}." : CheckSlot(state, town.Garrison, slot);
    }

    private static string? CheckSlot(GameState state, Army army, int slot)
    {
        if (state.PendingDecision is not null)
        {
            return "Resolve the pending decision first.";
        }

        return !Army.IsValidSlot(slot) || army[slot] is null ? "The slot is empty." : null;
    }
}
