namespace EternalWars.Core.Domain.Commands;

/// <summary>
/// A rule refuses the command: the reason a player reads. <see cref="Game.GameSession.Execute"/> turns it into a rejected
/// <see cref="CommandResult"/> and logs nothing, so a service throws it only before it changes the state. Any other exception out of a command is
/// a bug and is not taken for a refusal.
/// </summary>
public sealed class CommandRefusedException(string reason) : InvalidOperationException(reason);
