using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Data.Records;

/// <summary>
/// "Play again" on an arena map's result screen (ADR-159, ADR-199, ADR-231): the game's start map with the same players, teams, factions and
/// options, and a new seed, so the luck of the rematch is its own.
/// </summary>
public static class GameRematch
{
    /// <summary>
    /// The rematch's game, not started. The front-end draws <paramref name="seed"/> as it does a new game's: a new game's seed is not a rule's
    /// randomness, which comes from the game's own generator. A LAN host's rematch (<paramref name="keepControllers"/>) keeps who plays each
    /// colour, so a colour handed to the computer stays the computer's.
    /// </summary>
    public static GameState State(MapDocument initialMap, GameState finished, ContentCatalogs content, ulong seed, bool keepControllers)
    {
        var state = MapConverter.ToGameState(initialMap, content, finished.Options with { Seed = seed });
        if (keepControllers)
        {
            state.AssignControllers(finished.Players.ToDictionary(p => p.Id, p => p.IsHuman));
        }

        return state;
    }
}
