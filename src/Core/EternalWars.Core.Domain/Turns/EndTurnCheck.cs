using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Turns;

/// <summary>End turn asks first when a hero can still move (ADR-309): "Mara and Aldric can still move. End the turn?"</summary>
public static class EndTurnCheck
{
    /// <summary>
    /// The <paramref name="player"/>'s heroes on the map that can still move today: any movement left allows one more step (the last-step rule,
    /// <see cref="HeroMovementService.CanAffordStep"/>). Heroes in a town's garrison are not on the map.
    /// </summary>
    public static IReadOnlyList<Hero> HeroesThatCanMove(GameState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        return [.. state.HeroesOf(player).Where(h => HeroMovementService.CanAffordStep(h.MovementPoints))];
    }

    /// <summary>The question End turn asks first, or null when no hero can still move.</summary>
    public static string? Question(GameState state, PlayerId player)
    {
        var names = HeroesThatCanMove(state, player).Select(h => h.Name).ToList();
        return names.Count switch
        {
            0 => null,
            1 => $"{names[0]} can still move. End the turn?",
            _ => $"{string.Join(", ", names[..^1])} and {names[^1]} can still move. End the turn?",
        };
    }
}
