using System.Collections.Immutable;
using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Game;

/// <summary>A hero as it was when the game started: its owner then and its name, for hero conditions after it has left the game.</summary>
public sealed record TrackedHero(PlayerId Owner, string Name);

/// <summary>
/// What the scenario rules remember from the start of the game (<see cref="GameFlowService"/>, ADR-213): how many teams took part, the heroes
/// there were (a defeated hero leaves the game, yet "defeat hero" and "lose hero" still name it) and who sits in each prison a "release hero"
/// condition names (freeing the hero removes the prison). Part of <see cref="GameState"/> and its digest; sorted by id, so the digest is stable.
/// </summary>
public sealed record ScenarioTracking(
    int TeamsAtStart,
    ImmutableSortedDictionary<HeroId, TrackedHero> Heroes,
    ImmutableSortedDictionary<MapObjectId, string> Prisoners)
{
    /// <summary>Before the game starts.</summary>
    public static ScenarioTracking None { get; } = new(0, EmptyHeroes, EmptyPrisoners);

    internal static ImmutableSortedDictionary<HeroId, TrackedHero> EmptyHeroes =>
        ImmutableSortedDictionary.Create<HeroId, TrackedHero>(Comparer<HeroId>.Create((a, b) => a.Value.CompareTo(b.Value)));

    internal static ImmutableSortedDictionary<MapObjectId, string> EmptyPrisoners =>
        ImmutableSortedDictionary.Create<MapObjectId, string>(Comparer<MapObjectId>.Create((a, b) => a.Value.CompareTo(b.Value)));

    /// <summary>The digest's text (digest version 2 on, <see cref="GameStateDigest"/>).</summary>
    internal string Describe() =>
        FormattableString.Invariant(
            $"tracking teams {TeamsAtStart} heroes {string.Join(',', Heroes.Select(h => $"{h.Key.Value}:{h.Value.Owner.Value}:{h.Value.Name}"))} prisoners {string.Join(',', Prisoners.Select(p => $"{p.Key.Value}:{p.Value}"))}");
}
