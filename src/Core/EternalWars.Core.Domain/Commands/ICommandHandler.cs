namespace EternalWars.Core.Domain.Commands;

/// <summary>
/// Applies one command type for <see cref="Game.GameSession.Execute"/>, after the session has authorized it. It validates before it mutates and
/// refuses with <see cref="CommandRefusedException"/>; what it returns is the command's <see cref="CommandResult.Outcome"/>.
/// </summary>
internal interface ICommandHandler<in TCommand>
    where TCommand : GameCommand
{
    object? Handle(TCommand command);
}
