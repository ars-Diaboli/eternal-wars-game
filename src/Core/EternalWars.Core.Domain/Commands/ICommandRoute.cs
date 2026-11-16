namespace EternalWars.Core.Domain.Commands;

/// <summary>
/// Decides where an authorized command goes before <see cref="Game.GameSession.Execute"/> runs it (<see cref="Game.GameSession.Route"/>,
/// ADR-192): a LAN host refuses the commands of players on other computers, a LAN client sends its own to the host.
/// </summary>
public interface ICommandRoute
{
    /// <summary>Null runs <paramref name="command"/> here; any result is returned in its place and the command does not run.</summary>
    CommandResult? Route(GameCommand command);
}
