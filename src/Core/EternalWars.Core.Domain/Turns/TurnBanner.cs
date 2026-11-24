using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// The adventure map's turn banner (ADR-309): "&lt;Player&gt;'s turn" with the player's crest, at the top centre. With one person at the game it
/// shows for <see cref="BriefSeconds"/> when that person's turn starts, and not in the computer players' turns; with more (hot-seat, LAN,
/// <see cref="Lasting"/>) it stays while every turn lasts, the turn timer's pill under it. A simultaneous day (ADR-197) is everyone's turn.
/// </summary>
/// <param name="Text">What the banner says.</param>
/// <param name="Player">Whose turn it is, for the banner's colour; null on a simultaneous day.</param>
/// <param name="Crest">The town type whose crest the banner shows: the player's first town's, else the first town it owns now; null for none.</param>
/// <param name="Lasting">It stays while the turn lasts, rather than for <see cref="BriefSeconds"/>.</param>
public sealed record TurnBanner(string Text, PlayerId? Player, string? Crest, bool Lasting)
{
    /// <summary>How long the banner shows with one person at the game.</summary>
    public const double BriefSeconds = 3;

    /// <summary>The banner for the turn under way, or null when there is none (no game running, a computer's turn with one person playing).</summary>
    public static TurnBanner? Of(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsStarted || state.IsOver)
        {
            return null;
        }

        var lasting = state.Players.Count(p => p.IsHuman && !p.IsDefeated) > 1;
        if (state.IsSimultaneous)
        {
            return new TurnBanner("Everyone's turn", null, null, lasting);
        }

        var player = state.CurrentPlayer;
        return !lasting && !player.IsHuman ? null : new TurnBanner($"{player.Name}'s turn", player.Id, CrestOf(state, player), lasting);
    }

    /// <summary>The town type of <paramref name="player"/>'s crest: its first town's (<see cref="Player.StartTown"/>), else its first town now.</summary>
    public static string? CrestOf(GameState state, Player player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        return player.StartTown
            ?? state.ObjectsOf<Town>().Where(t => t.Owner == player.Id).OrderBy(t => t.Id.Value).Select(t => t.TownType).FirstOrDefault();
    }
}

/// <summary>
/// The turn timer's words (ADR-091, ADR-309): the pill under the turn banner with the time left, red from <see cref="RedFrom"/> seconds down,
/// and the warning ribbon under it at <see cref="TurnTimer.Warnings"/>.
/// </summary>
public static class TurnClockText
{
    /// <summary>The pill turns red at this many seconds left.</summary>
    public const int RedFrom = 60;

    /// <summary>The time left, rounded up to whole seconds: "1:05", "0:30".</summary>
    public static string Time(double secondsLeft)
    {
        var seconds = (int)Math.Ceiling(Math.Max(0, secondsLeft));
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>What the pill says: the time left; while a battle stops the clock, so; once the time is up, that the turn ends.</summary>
    public static string Pill(double secondsLeft, bool stopped, bool up) =>
        up ? "Time is up: the turn ends as soon as it can"
        : stopped ? $"{Time(secondsLeft)} stopped during the battle"
        : Time(secondsLeft);

    /// <summary>The pill is red: the last minute running, or the time up.</summary>
    public static bool IsRed(double secondsLeft, bool stopped, bool up) => up || (!stopped && Math.Ceiling(secondsLeft) <= RedFrom);

    /// <summary>The ribbon's warning at <paramref name="secondsLeft"/> left: "1 minute left!", "30 seconds left!".</summary>
    public static string Warning(int secondsLeft) =>
        secondsLeft % 60 == 0 && secondsLeft > 0
            ? secondsLeft == 60 ? "1 minute left!" : $"{secondsLeft / 60} minutes left!"
            : secondsLeft == 1 ? "1 second left!" : $"{secondsLeft} seconds left!";
}
