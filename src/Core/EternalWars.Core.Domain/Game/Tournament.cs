using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The tournament mode (ADR-366), switched on by <see cref="ScenarioRules.Tournament"/>: two players build up apart, each in a cut-off zone, for the
/// length chosen when the game is created (<see cref="GameOptions.TournamentDays"/>); then their heroes fight one battle that decides the game. This
/// holds what a tournament must be: exactly two players who are not allies, one hero each, never an arena too, and a length offered by the
/// new-game screen.
/// </summary>
public static class TournamentRules
{
    /// <summary>Why a player of a tournament cannot send the other resources (ADR-366): the two never trade.</summary>
    public const string NoTradingRefusal = "There is no trading between the players of a tournament.";

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
    /// The length <paramref name="steps"/> steps from <paramref name="days"/> on the new-game screen's stepper (<see cref="Lengths"/>): a day a
    /// step up to 14 days, then a week a step; it stops at the shortest and the longest. From a length not offered, the first step goes to the
    /// offered one beside it in that direction.
    /// </summary>
    public static int Step(int days, int steps)
    {
        var above = Lengths.Count(l => l < days); // the index of the first offered length not shorter than days
        var from = Lengths.Contains(days) || steps < 0 ? above : above - 1;
        return Lengths[Math.Clamp(from + steps, 0, Lengths.Count - 1)];
    }

    /// <summary>A length in words, as the new-game screen shows it: "1 day", "9 days", "14 days (2 weeks)", "21 days (3 weeks)".</summary>
    public static string LengthText(int days)
    {
        var text = days == 1 ? "1 day" : $"{days} days";
        var weeks = days / GameDate.DaysPerWeek;
        return days % GameDate.DaysPerWeek != 0 || weeks == 0 ? text : $"{text} ({weeks} {(weeks == 1 ? "week" : "weeks")})";
    }

    /// <summary>What the last building day's notice says to each person when its turn starts.</summary>
    public const string LastDayNotice =
        "The last day before the battle. Tomorrow your hero fights with its own army only, not the garrison it stands by: gather your troops "
        + "into it today.";

    /// <summary>
    /// The calendar's countdown in a tournament (ADR-366): "Battle in 5 days", "Last day before the battle" on the last building day; null on a
    /// map that is no tournament, and from the battle's day on.
    /// </summary>
    public static string? Countdown(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Rules.Tournament)
        {
            return null;
        }

        return (BattleDay(state.Options) - state.Date.AbsoluteDay) switch
        {
            1 => "Last day before the battle",
            > 1 and var left => $"Battle in {left} days",
            _ => null,
        };
    }

    /// <summary>Whether today is a tournament's last building day, when each person's turn opens with <see cref="LastDayNotice"/>.</summary>
    public static bool IsLastDay(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Rules.Tournament && state.Date.AbsoluteDay == Days(state.Options);
    }

    /// <summary>
    /// Whether <paramref name="player"/> may see, reveal or reach <paramref name="position"/> (ADR-366): anywhere on a map that is no tournament; in a
    /// tournament never inside the other player's start zone, so sight, scouting, revealing objects, the puzzle map and the adventure spells stop at
    /// its edge. Tiles in no zone (the walls between them) stay in reach, so a map painted only in part is not blind.
    /// </summary>
    public static bool Reaches(GameState state, PlayerId player, MapPosition position)
    {
        ArgumentNullException.ThrowIfNull(state);
        return !state.Rules.Tournament || state.Map.GetStartZone(position) is not { } zone || zone == player;
    }

    /// <summary>
    /// The start zone whose shares <paramref name="player"/> keeps apart from the other's (a tournament's artifact rolls, ADR-366): its own when the
    /// tournament's map gives it one; null (the map's single share) otherwise.
    /// </summary>
    public static PlayerId? ZoneOf(GameState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Rules.Tournament && state.Map.StartZoneOwners.Contains(player) ? player : null;
    }

    /// <summary>The start zone of <paramref name="position"/> whose shares are kept apart in a tournament (<see cref="ZoneOf(GameState, PlayerId)"/>); null otherwise.</summary>
    public static PlayerId? ZoneOf(GameState state, MapPosition position)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Rules.Tournament ? state.Map.GetStartZone(position) : null;
    }

    /// <summary>
    /// The tournament's battle, chosen at the start of its day: a coin toss from <see cref="GameState.Random"/> makes one of the two players the attacker,
    /// and each fights with its one hero (<see cref="DecidingBattles.Champion"/>: on the map, else in a town's garrison). There is no combat place: the
    /// battlefield is neutral ground (<see cref="Battlefield"/>). A side without a hero loses without a battle, and so does one whose hero has no army
    /// when the other has a hero; when neither side can fight it is a draw. Draws once from the game's random numbers, so it is called once, by the
    /// battle's start.
    /// </summary>
    public static DecidingBattle Plan(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var name = DecidingBattles.Name(state.Rules);
        var players = state.Players.Where(p => p.TookPart && !p.IsDefeated).ToList();
        if (players.Count < 2)
        {
            return new(null, null, null, null, null) { Result = DecidingBattleResult.NoBattle, Reason = $"Nobody was left to fight the {name}." };
        }

        var (attacker, defender) = state.Random.Next(2) == 0 ? (players[0], players[1]) : (players[1], players[0]);
        var attackerHero = DecidingBattles.Champion(state, attacker.Team);
        var defenderHero = DecidingBattles.Champion(state, defender.Team);
        var plan = new DecidingBattle(null, attacker.Id, attackerHero?.Id, defender.Id, defenderHero?.Id)
        {
            Summary = attackerHero is null || defenderHero is null ? null
                : $"{attackerHero.Name} ({attacker.Name}) won the coin toss and attacked {defenderHero.Name} ({defender.Name}).",
        };
        return (attackerHero, defenderHero) switch
        {
            (null, null) => plan with { Result = DecidingBattleResult.Draw, Reason = $"Neither {attacker.Name} nor {defender.Name} had a hero." },
            (null, { } hero) => NoHero(plan, attacker.Name, hero, defender.Id),
            ({ } hero, null) => NoHero(plan, defender.Name, hero, attacker.Id),
            _ => plan,
        };

        DecidingBattle NoHero(DecidingBattle plan, string without, Hero other, PlayerId otherPlayer) =>
            DecidingBattles.ArmyOf(state, other).IsEmpty
                ? plan with { Result = DecidingBattleResult.Draw, Reason = $"{without} had no hero, and {other.Name} ({state.GetPlayer(otherPlayer).Name}) no army." }
                : plan with { Result = DecidingBattleResult.Won, Winner = otherPlayer, Reason = $"{without} had no hero." };
    }

    /// <summary>
    /// The tournament battle's ground: neutral, so a terrain no town is native to (sand, beach, dirt with the default terrains), drawn from
    /// <see cref="GameState.Random"/>; any passable terrain when the terrains give none. Its obstacles come from the battle's own random numbers, forked
    /// from the game's as every battle's.
    /// </summary>
    public static TerrainType Battlefield(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var passable = state.Terrains.All.Where(t => t.Class == TerrainClass.Passable).OrderBy(t => t.Terrain).ToList();
        var neutral = passable.Where(t => t.NativeTowns.Count == 0).ToList();
        var choices = neutral.Count > 0 ? neutral : passable;
        return choices[state.Random.Next(choices.Count)].Terrain;
    }

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
