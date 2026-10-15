namespace EternalWars.Core.Domain.Editing;

/// <summary>A reversible edit. <see cref="Execute"/> is also used for redo, so it must be repeatable after <see cref="Undo"/>.</summary>
public interface IEditorCommand
{
    string Description { get; }

    void Execute();

    void Undo();
}

internal sealed class DelegateCommand(string description, Action execute, Action undo) : IEditorCommand
{
    public string Description { get; } = description;

    public void Execute() => execute();

    public void Undo() => undo();
}

/// <summary>Several commands undone/redone as one (e.g. a paint stroke made while dragging).</summary>
internal sealed class CompositeCommand(string description, IReadOnlyList<IEditorCommand> commands) : IEditorCommand
{
    public string Description { get; } = description;

    public void Execute()
    {
        foreach (var command in commands)
        {
            command.Execute();
        }
    }

    public void Undo()
    {
        for (var i = commands.Count - 1; i >= 0; i--)
        {
            commands[i].Undo();
        }
    }
}

/// <summary>Linear undo/redo stack. Recording a new command discards the redo branch.</summary>
public sealed class UndoHistory(int capacity = 200)
{
    private readonly LinkedList<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoDescription => _undo.Last?.Value.Description;

    public string? RedoDescription => _redo.TryPeek(out var command) ? command.Description : null;

    /// <summary>Records an already executed command.</summary>
    public void Record(IEditorCommand command)
    {
        _undo.AddLast(command);
        if (_undo.Count > capacity)
        {
            _undo.RemoveFirst();
        }

        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Last is not { } node)
        {
            return false;
        }

        _undo.RemoveLast();
        node.Value.Undo();
        _redo.Push(node.Value);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var command))
        {
            return false;
        }

        command.Execute();
        _undo.AddLast(command);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
