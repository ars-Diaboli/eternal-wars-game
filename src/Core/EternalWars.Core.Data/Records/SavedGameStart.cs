using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Data.Records;

/// <summary>
/// Going on with a loaded save (ADR-196, ADR-197, ADR-231): whether to ask who plays its people's colours from now on, and the commands that
/// set it going as answered, so the game's record keeps them.
/// </summary>
public static class SavedGameStart
{
    /// <summary>Whether a save asks who plays each person's colour from now on: when more than one person is still in the game.</summary>
    public static bool AsksWhoPlays(GameState state) => GameSaves.People(state).Count > 1;

    /// <summary>
    /// Sets a loaded game going: each person's colour in <paramref name="toComputer"/> is the computer's from now on; on one computer
    /// (<paramref name="lan"/> false) a save made during a simultaneous day goes on turn by turn, since nobody's day can be hidden from another's
    /// there (ADR-197).
    /// </summary>
    public static void GoOn(GameSession session, IEnumerable<PlayerId> toComputer, bool lan)
    {
        foreach (var player in toComputer.Where(p => session.State.GetPlayer(p).IsHuman))
        {
            session.Execute(new HandToComputerCommand(player)).ThrowIfRejected();
        }

        if (!lan && session.State.IsSimultaneous)
        {
            session.Execute(new PlayTurnByTurnCommand(session.State.CurrentPlayer.Id, OneComputer)).ThrowIfRejected();
        }
    }

    /// <summary>Why a simultaneous day loaded on one computer goes on turn by turn.</summary>
    public const string OneComputer = "The game goes on on one computer.";
}
