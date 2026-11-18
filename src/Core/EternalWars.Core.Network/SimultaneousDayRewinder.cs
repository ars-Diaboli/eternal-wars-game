using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Network;

/// <summary>
/// Takes a simultaneous day back on its conflict (MP-2, ADR-197): the game goes back to the day's start (the host's record up to
/// <see cref="GameSession.SimultaneousDayStart"/>) and plays it again turn by turn, in a new session. It holds no state: the conflict is the
/// game's, the record the host's.
/// </summary>
internal static class SimultaneousDayRewinder
{
    /// <summary>
    /// The day played again from its start, with the line that tells everyone why; null while <paramref name="session"/> has no conflict, or
    /// when the host keeps no record (<paramref name="recordOf"/> null).
    /// </summary>
    public static (GameSession Again, string Announcement)? PlayDayAgain(GameSession session, Func<string?, GameRecordDocument>? recordOf)
    {
        if (session.State.SimultaneousConflict is not { } reason || recordOf is null)
        {
            return null;
        }

        var record = recordOf(null);
        var state = session.State;
        var by = state.Options.HostPlayer ?? state.Players.First(p => p.IsHuman).Id;
        var again = GameReplay.Replay(
            record with { Commands = [.. record.Commands.Take(session.SimultaneousDayStart), new PlayTurnByTurnCommand(by, reason)], FinalDigest = null },
            state.Content);
        return (again, $"{reason} The day is played again, turn by turn, and so is every day after it.");
    }
}
