namespace EternalWars.Core.Domain.Commands;

/// <summary>
/// The session's handlers by command type: exactly one per concrete <see cref="GameCommand"/> (adding a second one for a type throws). The area
/// registrations (<see cref="AdventureCommandHandlers"/>, <see cref="ObjectCommandHandlers"/>, <see cref="TownCommandHandlers"/>,
/// <see cref="ArtifactCommandHandlers"/>, <see cref="BattleCommandHandlers"/>) fill it when the session is built.
/// </summary>
internal sealed class CommandHandlers
{
    private readonly Dictionary<Type, Func<GameCommand, object?>> _handlers = [];

    /// <summary>The command types that have a handler.</summary>
    internal IReadOnlyCollection<Type> CommandTypes => _handlers.Keys;

    internal void Add<TCommand>(ICommandHandler<TCommand> handler)
        where TCommand : GameCommand
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryAdd(typeof(TCommand), command => handler.Handle((TCommand)command)))
        {
            throw new InvalidOperationException($"{typeof(TCommand).Name} already has a handler.");
        }
    }

    /// <summary>A command without an outcome: whatever <paramref name="apply"/>'s service call returns is dropped.</summary>
    internal void Add<TCommand>(Action<TCommand> apply)
        where TCommand : GameCommand =>
        Add(new DelegateCommandHandler<TCommand>(command =>
        {
            apply(command);
            return null;
        }));

    /// <summary>A command whose result (a move's outcome) goes back to the caller in <see cref="CommandResult.Outcome"/>.</summary>
    internal void AddWithOutcome<TCommand>(Func<TCommand, object?> handle)
        where TCommand : GameCommand =>
        Add(new DelegateCommandHandler<TCommand>(handle));

    /// <summary>Applies <paramref name="command"/> with the handler of its type.</summary>
    internal object? Handle(GameCommand command) =>
        _handlers.TryGetValue(command.GetType(), out var handle)
            ? handle(command)
            : throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command type.");
}
