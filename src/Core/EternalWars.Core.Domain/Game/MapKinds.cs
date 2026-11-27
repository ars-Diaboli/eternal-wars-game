using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Game;

/// <summary>The new-game screen's tabs for hand-made maps (ADR-201); random map templates are a tab of their own.</summary>
public enum MapKind
{
    /// <summary>A regular scenario: played to win and ranked.</summary>
    Scenario,

    /// <summary>An arena map (ADR-097): a day to prepare, then one battle.</summary>
    Arena,

    /// <summary>A campaign-like map: a story map, not ranked (<see cref="ScoreRules.IsRanked"/>).</summary>
    Campaign,

    /// <summary>A tournament map (ADR-366): two players build up apart for the chosen length, then one battle.</summary>
    Tournament,
}

/// <summary>Which tab of the new-game screen a hand-made map belongs to (ADR-201), read from the map as loaded, before the game starts.</summary>
public static class MapKinds
{
    /// <summary>
    /// An arena map is <see cref="MapKind.Arena"/>, a tournament map <see cref="MapKind.Tournament"/>. A map is <see cref="MapKind.Campaign"/> when it is campaign-like as the ranking reads it
    /// (a human player starts without a town, ADR-075, or a map event favours people, ADR-101) or when it carries campaign variables
    /// (EVT-1 f); anything else is a <see cref="MapKind.Scenario"/>.
    /// </summary>
    public static MapKind Of(GameState state)
    {
        if (state.Rules.Arena)
        {
            return MapKind.Arena;
        }

        if (state.Rules.Tournament)
        {
            return MapKind.Tournament;
        }

        var set = state.MapEvents.Set;
        var humanWithoutTown = state.Players.Any(p => p.IsHuman && !state.ObjectsOf<Town>().Any(t => t.Owner == p.Id));
        var campaign = humanWithoutTown
                       || MapEventChecks.FavoursPeople(set) is not null
                       || set.KeptVariables.Count > 0
                       || set.TakenVariables.Count > 0;
        return campaign ? MapKind.Campaign : MapKind.Scenario;
    }
}
