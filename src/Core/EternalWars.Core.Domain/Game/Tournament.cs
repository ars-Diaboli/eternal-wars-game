using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The tournament mode (ADR-366), switched on by <see cref="ScenarioRules.Tournament"/>: two players build up apart, each in a cut-off zone, for the
/// length chosen when the game is created (<see cref="GameOptions.TournamentDays"/>); then their heroes fight one battle that decides the game. This
/// holds what a tournament must be: exactly two players who are not allies, one hero each, never an arena too, and a length offered by the
/// new-game screen.
/// </summary>
public static class TournamentRules
{
    /// <summary>The length when the game names none: two weeks.</summary>
    public const int DefaultDays = 14;

    /// <summary>The longest length counted in days; longer ones are whole weeks.</summary>
    public const int MaxDailyDays = 14;

    /// <summary>The shortest length counted in weeks.</summary>
    public const int MinWeeks = 3;

    /// <summary>The longest length in weeks (about three months, the upper limit).</summary>
    public const int MaxWeeks = 12;

    /// <summary>Every length a tournament may have, shortest first: 1 to 14 days, then 21, 28, … 84 (the new-game screen's stepper).</summary>
    public static IReadOnlyList<int> Lengths { get; } =
        [.. Enumerable.Range(1, MaxDailyDays), .. Enumerable.Range(MinWeeks, MaxWeeks - MinWeeks + 1).Select(w => w * GameDate.DaysPerWeek)];

    /// <summary>Whether a tournament may last <paramref name="days"/> days: 1 to 14, or 3 to 12 whole weeks.</summary>
    public static bool IsLength(int days) =>
        days is >= 1 and <= MaxDailyDays
        || (days % GameDate.DaysPerWeek == 0 && days / GameDate.DaysPerWeek is >= MinWeeks and <= MaxWeeks);

    /// <summary>The game's length in days: the chosen one, else <see cref="DefaultDays"/>.</summary>
    public static int Days(GameOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.TournamentDays ?? DefaultDays;
    }

    /// <summary>The day the battle starts by itself, before anyone's turn: the day after the last building day (<see cref="Days"/> + 1).</summary>
    public static int BattleDay(GameOptions options) => Days(options) + 1;

    /// <summary>
    /// What keeps the map of <paramref name="scenario"/> from being a tournament, in order (none when it is one, or is no tournament): an arena
    /// too, not exactly two players, the two allied, or a player allowed other than one hero. The editor's validator lists them as errors.
    /// </summary>
    public static IEnumerable<string> MapProblems(GameState scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (!scenario.Rules.Tournament)
        {
            yield break;
        }

        if (scenario.Rules.Arena)
        {
            yield return "A map cannot be an arena and a tournament at once.";
        }

        if (scenario.Players.Count != 2)
        {
            yield return $"A tournament is for exactly two players; the map has {scenario.Players.Count}.";
        }
        else if (scenario.Players[0].Team == scenario.Players[1].Team)
        {
            yield return "The two players of a tournament cannot be allies.";
        }

        foreach (var player in scenario.Players.Where(p => scenario.HeroLimit(p.Id) != 1))
        {
            yield return $"A tournament allows one hero a player; {player.Name} may have {scenario.HeroLimit(player.Id)}.";
        }
    }

    /// <summary>
    /// Why the game of <paramref name="state"/> (its map and options, teams as set up) cannot start; null when it can. A length on a map that is no
    /// tournament, a length that is not offered (<see cref="IsLength"/>), or the first of <see cref="MapProblems"/>.
    /// </summary>
    public static string? WhyNot(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Options.TournamentDays is { } days)
        {
            if (!state.Rules.Tournament)
            {
                return "Only a tournament has a length.";
            }

            if (!IsLength(days))
            {
                return $"A tournament lasts 1 to {MaxDailyDays} days, or {MinWeeks} to {MaxWeeks} whole weeks; {days} days is neither.";
            }
        }

        return MapProblems(state).FirstOrDefault();
    }
}
