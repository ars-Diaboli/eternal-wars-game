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

/// <summary>
/// Linear undo/redo history: its steps in the order they were made, and how many of them are applied now (<see cref="Position"/>).
/// Recording a new command discards the steps after the position. It also remembers the position the map was saved at
/// (<see cref="MarkSaved"/>): the map has unsaved work whenever it is not there (<see cref="IsDirty"/>).
/// </summary>
public sealed class UndoHistory(int capacity = 200)
{
    private readonly List<IEditorCommand> _steps = [];

    /// <summary>The position the map was saved at; null when no undo or redo leads back to the saved map.</summary>
    private int? _saved = 0;

    /// <summary>The descriptions of all steps, oldest first: the first <see cref="Position"/> are applied, the rest are undone.</summary>
    public IReadOnlyList<string> Steps => [.. _steps.Select(step => step.Description)];

    /// <summary>How many of the <see cref="Steps"/> are applied: 0 when everything is undone, the steps' count when nothing is.</summary>
    public int Position { get; private set; }

    public int Count => _steps.Count;

    /// <summary>
    /// The map differs from the one last saved (<see cref="MarkSaved"/>; a new history counts as saved). Undoing or redoing back to the
    /// saved step makes it clean again; a step recorded over the saved one, or the saved one falling out of a full history, leaves it
    /// dirty until the next save.
    /// </summary>
    public bool IsDirty => _saved != Position;

    public bool CanUndo => Position > 0;

    public bool CanRedo => Position < _steps.Count;

    public string? UndoDescription => CanUndo ? _steps[Position - 1].Description : null;

    public string? RedoDescription => CanRedo ? _steps[Position].Description : null;

    /// <summary>Records an already executed command.</summary>
    public void Record(IEditorCommand command)
    {
        if (_saved > Position)
        {
            _saved = null; // the saved map was among the undone steps this one replaces
        }

        _steps.RemoveRange(Position, _steps.Count - Position);
        _steps.Add(command);
        if (_steps.Count > capacity)
        {
            _steps.RemoveAt(0);
            _saved = _saved > 0 ? _saved - 1 : null; // saved before the dropped step: no undo reaches it any more
        }

        Position = _steps.Count;
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        Position--;
        _steps[Position].Undo();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        _steps[Position].Execute();
        Position++;
        return true;
    }

    /// <summary>
    /// Undoes or redoes one step at a time until <paramref name="position"/> steps are applied; returns whether anything changed.
    /// </summary>
    public bool MoveTo(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, _steps.Count);
        if (position == Position)
        {
            return false;
        }

        while (Position > position)
        {
            Undo();
        }

        while (Position < position)
        {
            Redo();
        }

        return true;
    }

    /// <summary>The map as it is now was saved.</summary>
    public void MarkSaved() => _saved = Position;

    /// <summary>Forgets every step; unsaved work stays unsaved.</summary>
    public void Clear()
    {
        _saved = IsDirty ? null : 0;
        _steps.Clear();
        Position = 0;
    }
}
