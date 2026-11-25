using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Pathfinding;

namespace EternalWars.Core.Domain.Cursors;

/// <summary>
/// The adventure map's action buttons (ADR-309, ADR-316): a grid of two rows of four, Move first; Next hero (Tab), Next town (T), End turn
/// (its own tile), the hero screen (H, the hero card) and Switch level (L, the minimap's caption) have no button here.
/// </summary>
public static class AdventureActionGrid
{
    /// <summary>How many buttons a row holds.</summary>
    public const int Columns = 4;

    /// <summary>The buttons, row by row.</summary>
    public static IReadOnlyList<AdventureAction> Order { get; } =
    [
        AdventureAction.Move,
        AdventureAction.SpellBook,
        AdventureAction.Kingdom,
        AdventureAction.Quests,
        AdventureAction.PuzzleMap,
        AdventureAction.Dig,
        AdventureAction.Marketplace,
        AdventureAction.Menu,
    ];

    /// <summary>
    /// Why Move cannot walk <paramref name="hero"/> along <paramref name="route"/> (the route shown on the map) now, or null when it can: no
    /// hero selected, no route of that hero's, or no movement left today (the route's first step is reached on a later day).
    /// </summary>
    public static string? MoveRefusal(Hero? hero, HeroPath? route)
    {
        if (hero is null)
        {
            return "Select a hero first";
        }

        if (route is null || route.Hero != hero.Id || route.Steps.Count == 0)
        {
            return "Click a cell to plan a route first";
        }

        return route.Steps[0].Day > 0 || !HeroMovementService.CanAffordStep(hero.MovementPoints)
            ? $"{hero.Name} has no movement left today"
            : null;
    }
}
