using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Armies;

/// <summary>One part of a spread: <see cref="Count"/> creatures from the spread stack's slot into the empty slot <see cref="ToSlot"/>.</summary>
public sealed record SpreadMove(int ToSlot, int Count);

/// <summary>
/// "Spread a stack evenly" (Alt + click on an army slot, ADR-294): the stack shares its creatures with every empty slot of its army, as even as
/// whole creatures allow; the stack keeps what does not divide. A front-end runs the moves as stack commands (<see cref="ForHero"/>,
/// <see cref="ForTown"/>), which already take a count, so a spread is nothing new to the rules.
/// </summary>
public static class StackSpread
{
    /// <summary>
    /// The moves that spread the stack in <paramref name="slot"/> over the empty slots of <paramref name="army"/>, in slot order. Empty when the slot
    /// is empty, there is no empty slot, or the stack has a single creature.
    /// </summary>
    public static IReadOnlyList<SpreadMove> Plan(Army army, int slot)
    {
        if (!Army.IsValidSlot(slot) || army[slot] is not { } stack)
        {
            return [];
        }

        var empty = Enumerable.Range(0, Army.SlotCount).Where(s => army[s] is null).ToList();
        var parts = Math.Min(empty.Count + 1, stack.Count);
        if (parts < 2)
        {
            return [];
        }

        var share = stack.Count / parts;
        return [.. empty.Take(parts - 1).Select(s => new SpreadMove(s, share))];
    }

    /// <summary>The spread of a hero's stack as <see cref="MoveStackCommand"/>s within the hero's army.</summary>
    public static IReadOnlyList<MoveStackCommand> ForHero(PlayerId player, HeroId hero, Army army, int slot) =>
        [.. Plan(army, slot).Select(m => new MoveStackCommand(player, hero, slot, hero, m.ToSlot, m.Count))];

    /// <summary>The spread of a town army's stack (the garrison or the visiting hero) as <see cref="MoveTownStackCommand"/>s within that army.</summary>
    public static IReadOnlyList<MoveTownStackCommand> ForTown(PlayerId player, MapObjectId town, TownArmy which, Army army, int slot) =>
        [.. Plan(army, slot).Select(m => new MoveTownStackCommand(player, town, which, slot, which, m.ToSlot, m.Count))];
}
