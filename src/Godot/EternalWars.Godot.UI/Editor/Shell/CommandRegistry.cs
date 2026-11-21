using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// Every action the editor has (EDIT-1, ADR-250), by id. The menu bar and the toolbar are built from it, and the app hands it the keys
/// nothing else took. Whoever changes what a command's <see cref="EditorCommand.WhyNot"/> or <see cref="EditorCommand.IsOn"/> reads calls
/// <see cref="NotifyStateChanged"/>, so the views that show the commands follow.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, EditorCommand> _byId = [];
    private readonly List<EditorCommand> _all = [];

    /// <summary>What a command can do or shows as on may have changed.</summary>
    public event Action? StateChanged;

    /// <summary>The commands in the order they were registered.</summary>
    public IReadOnlyList<EditorCommand> All => _all;

    public EditorCommand this[string id] => _byId[id];

    /// <summary>Every command's default shortcut (ADR-275), by id in the commands' order.</summary>
    public IReadOnlyDictionary<string, KeyChord?> Defaults => _all.ToDictionary(c => c.Id, c => c.DefaultShortcut?.Chord);

    /// <summary>Every command's shortcut in effect, by id in the commands' order.</summary>
    public IReadOnlyDictionary<string, KeyChord?> Shortcuts => _all.ToDictionary(c => c.Id, c => c.Shortcut?.Chord);

    /// <summary>
    /// Puts the user's <paramref name="kept"/> shortcuts over the defaults (<see cref="ShortcutBindings.Resolve"/>): a chord whose key the
    /// engine does not know counts as none. The views showing the commands follow.
    /// </summary>
    public void UseShortcuts(IReadOnlyDictionary<string, KeyChord?> kept)
    {
        var known = kept.ToDictionary(k => k.Key, k => CommandShortcut.Of(k.Value)?.Chord);
        foreach (var (id, chord) in ShortcutBindings.Resolve(Defaults, known))
        {
            _byId[id].Rebind(CommandShortcut.Of(chord));
        }

        NotifyStateChanged();
    }

    /// <summary>Adds <paramref name="command"/>. Its id and its default shortcut must be its own.</summary>
    public EditorCommand Register(EditorCommand command)
    {
        if (command.Shortcut is { } shortcut && _all.FirstOrDefault(c => c.Shortcut == shortcut) is { } taken)
        {
            throw new ArgumentException($"{shortcut.Text} is already the shortcut of {taken.Id}.", nameof(command));
        }

        _byId.Add(command.Id, command);
        _all.Add(command);
        return command;
    }

    /// <summary>Runs the command when it can run now. False when it cannot.</summary>
    public bool Run(string id)
    {
        var command = _byId[id];
        if (command.WhyNot?.Invoke() is not null)
        {
            return false;
        }

        command.Run();
        NotifyStateChanged();
        return true;
    }

    /// <summary>Runs the command whose shortcut <paramref name="event"/> is. True when there is one, whether it could run or not.</summary>
    public bool RunShortcut(InputEventKey @event)
    {
        foreach (var command in _all)
        {
            if (command.Shortcut is { } shortcut && shortcut.Matches(@event))
            {
                Run(command.Id);
                return true;
            }
        }

        return false;
    }

    public void NotifyStateChanged() => StateChanged?.Invoke();
}
