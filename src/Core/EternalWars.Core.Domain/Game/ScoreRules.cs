using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game;

/// <summary>A human winner's score (ADR-101, FLOW-4).</summary>
/// <param name="Player">The winner.</param>
/// <param name="Faction">The town type the player started with (<see cref="Player.StartTown"/>).</param>
/// <param name="Days">The day the game ended (<see cref="GameOutcome.Date"/>).</param>
/// <param name="Difficulty">The complexity the score counts (<see cref="ScoreRules.Difficulty"/>).</param>
/// <param name="TurnTimerMinutes">The game's turn timer; null is off.</param>
/// <param name="Score">The points.</param>
/// <param name="Title">The rank title: a creature id (<see cref="ScoreRules.Titles"/>).</param>
public sealed record GameScore(PlayerId Player, string Faction, int Days, Complexity Difficulty, int? TurnTimerMinutes, int Score, string Title);

/// <summary>
/// The victory screen's score (ADR-101, FLOW-4; HoMM3-like): max(10, 200 − days) × the difficulty factor × the turn timer factor, rounded down,
/// and a rank title named after a creature. Only a won game on a map that is not campaign-like scores, and only its human winners. The score
/// changes no rule; the factors are placeholders to tune.
/// </summary>
public static class ScoreRules
{
    /// <summary>Base points for a game won on day 1 … 190; from day 190 on, <see cref="MinimumBase"/>.</summary>
    public const int BaseDays = 200;

    public const int MinimumBase = 10;

    /// <summary>
    /// Rank titles from the weakest to a dragon: the lowest score of each band and the creature it is named after (14 bands, placeholder
    /// bounds).
    /// </summary>
    public static IReadOnlyList<(int MinScore, string Creature)> Titles { get; } =
    [
        (0, "inferno.imp"),
        (20, "stronghold.goblin"),
        (40, "necropolis.skeleton"),
        (60, "haven.pikeman"),
        (80, "stronghold.centaur"),
        (100, "dungeon.medusa"),
        (125, "haven.griffin"),
        (150, "necropolis.vampire"),
        (175, "stronghold.ogre"),
        (200, "academy.naga"),
        (230, "inferno.pit-lord"),
        (260, "sylvan.phoenix"),
        (300, "academy.titan"),
        (350, "dragon-utopia.black-dragon"),
    ];

    /// <summary>
    /// Whether the game may be ranked: a map is campaign-like (not ranked) when a human player started without a town (ADR-075). The map events'
    /// check (an event that favours humans over computer players) comes with the map events (ADR-096).
    /// </summary>
    public static bool IsRanked(GameState state) =>
        state.IsStarted && state.Players.Where(p => p.TookPart && p.IsHuman).All(p => p.StartTown is not null);

    /// <summary>The difficulty a game counts as: its complexity; "Map default" (none chosen) counts as Normal.</summary>
    public static Complexity Difficulty(GameOptions options) => options.Complexity ?? Complexity.Normal;

    /// <summary>The difficulty factor in percent (HoMM3's): Beginner 80, Easy 100, Normal 130, Hard 160, Expert 200.</summary>
    public static int DifficultyPercent(Complexity difficulty) => difficulty switch
    {
        Complexity.Beginner => 80,
        Complexity.Easy => 100,
        Complexity.Normal => 130,
        Complexity.Hard => 160,
        _ => 200,
    };

    /// <summary>The turn timer factor in percent: a shorter timer is worth more; off or over 10 minutes 100, 6–10 105, 3–5 110, 1–2 120.</summary>
    public static int TurnTimerPercent(int? minutes) => minutes switch
    {
        null or > 10 => 100,
        > 5 => 105,
        > 2 => 110,
        _ => 120,
    };

    /// <summary>The points for a game won on day <paramref name="days"/>.</summary>
    public static int Score(int days, Complexity difficulty, int? turnTimerMinutes) =>
        Math.Max(MinimumBase, BaseDays - days) * DifficultyPercent(difficulty) * TurnTimerPercent(turnTimerMinutes) / 10_000;

    /// <summary>The rank title (a creature id) of a score.</summary>
    public static string Title(int score) => Titles.Last(t => score >= t.MinScore).Creature;

    /// <summary>
    /// The scores of a finished game: one per human winner, in player order; none when the game is not over, nobody won, only computer players
    /// won or the map is campaign-like (<see cref="IsRanked"/>).
    /// </summary>
    public static IReadOnlyList<GameScore> Scores(GameState state)
    {
        if (state.Outcome is not { HasWinner: true } outcome || !IsRanked(state))
        {
            return [];
        }

        var difficulty = Difficulty(state.Options);
        var timer = state.Options.TurnTimerMinutes;
        var days = outcome.Date.AbsoluteDay;
        var score = Score(days, difficulty, timer);
        return state.Players
            .Where(p => p.IsHuman && outcome.Won(p.Id))
            .Select(p => new GameScore(p.Id, p.StartTown!, days, difficulty, timer, score, Title(score)))
            .ToList();
    }
}
