using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>A key with its modifiers, as a command's shortcut.</summary>
public readonly record struct CommandShortcut(Key Key, bool Ctrl = false, bool Shift = false, bool Alt = false)
{
    /// <summary>The shortcut as menus and tooltips show it: "Ctrl+Shift+S".</summary>
    public string Text => Chord.Text;

    /// <summary>The shortcut as the editor settings keep it (ADR-275).</summary>
    public KeyChord Chord => new(Key.ToString(), Ctrl, Shift, Alt);

    /// <summary>The shortcut <paramref name="chord"/> names; null when its key is not one of the engine's.</summary>
    public static CommandShortcut? Of(KeyChord? chord) =>
        chord is { } c && Enum.TryParse<Key>(c.Key, ignoreCase: true, out var key) && Enum.IsDefined(key) && key != Key.None && !int.TryParse(c.Key, out _)
            ? new CommandShortcut(key, c.Ctrl, c.Shift, c.Alt)
            : null;

    /// <summary>
    /// The shortcut a key press is (the Preferences' shortcut editor), by its key code as <see cref="Matches"/> reads it; null for a modifier
    /// on its own or a press without a key code.
    /// </summary>
    public static CommandShortcut? Of(InputEventKey @event)
    {
        var key = @event.Keycode;
        return key is Key.None or Key.Ctrl or Key.Shift or Key.Alt or Key.Meta or Key.Unknown
            ? null
            : new CommandShortcut(key, @event.CtrlPressed, @event.ShiftPressed, @event.AltPressed);
    }

    /// <summary>Whether <paramref name="event"/> is this key going down with exactly these modifiers.</summary>
    public bool Matches(InputEventKey @event) =>
        @event is { Pressed: true, Echo: false } && @event.Keycode == Key
        && @event.CtrlPressed == Ctrl && @event.ShiftPressed == Shift && @event.AltPressed == Alt;
}

/// <summary>
/// One action of the editor (EDIT-1, ADR-250): the menus, the toolbar and the shortcuts all run it through the <see cref="CommandRegistry"/>.
/// </summary>
public sealed class EditorCommand(string id, string name, Action run)
{
    private CommandShortcut? _shortcut;

    /// <summary>The command's own name in the registry: "edit.undo".</summary>
    public string Id { get; } = id;

    /// <summary>The name on menus and buttons.</summary>
    public string Name { get; } = name;

    public Action Run { get; } = run;

    public UiIcon? Icon { get; init; }

    /// <summary>The shortcut in effect: <see cref="DefaultShortcut"/>, or the user's own (ADR-275, <see cref="CommandRegistry.UseShortcuts"/>).</summary>
    public CommandShortcut? Shortcut
    {
        get => _shortcut;
        init
        {
            _shortcut = value;
            DefaultShortcut = value;
        }
    }

    /// <summary>The shortcut the command is registered with.</summary>
    public CommandShortcut? DefaultShortcut { get; private init; }

    internal void Rebind(CommandShortcut? shortcut) => _shortcut = shortcut;

    /// <summary>Why the command cannot run now, as its tooltip says it; null when it can. Not set: it always can.</summary>
    public Func<string?>? WhyNot { get; init; }

    /// <summary>For a command that switches something on and off, or picks one of several: whether it is on. Not set: a plain action.</summary>
    public Func<bool>? IsOn { get; init; }

    /// <summary>With <see cref="IsOn"/>: the command picks one of several (a tool), so menus mark it with a dot, not a tick.</summary>
    public bool IsChoice { get; init; }

    /// <summary>The name with the shortcut after it: "Undo (Ctrl+Z)".</summary>
    public string Label => Shortcut is { } shortcut ? $"{Name} ({shortcut.Text})" : Name;
}
