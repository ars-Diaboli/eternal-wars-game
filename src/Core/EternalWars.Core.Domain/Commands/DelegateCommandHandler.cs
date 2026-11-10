namespace EternalWars.Core.Domain.Commands;

/// <summary>A handler that forwards the command to a service call (the usual case: one line per command).</summary>
internal sealed class DelegateCommandHandler<TCommand>(Func<TCommand, object?> handle) : ICommandHandler<TCommand>
    where TCommand : GameCommand
{
    public object? Handle(TCommand command) => handle(command);
}
