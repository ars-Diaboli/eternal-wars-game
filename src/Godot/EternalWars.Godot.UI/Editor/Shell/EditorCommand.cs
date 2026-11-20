using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>A key with its modifiers, as a command's shortcut.</summary>
public readonly record struct CommandShortcut(Key Key, bool Ctrl = false, bool Shift = false, bool Alt = false)
{
    /// <summary>The shortcut as menus and tooltips show it: "Ctrl+Shift+S".</summary>
    public string Text => $"{(Ctrl ? "Ctrl+" : "")}{(Shift ? "Shift+" : "")}{(Alt ? "Alt+" : "")}{Key}";

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
    /// <summary>The command's own name in the registry: "edit.undo".</summary>
    public string Id { get; } = id;

    /// <summary>The name on menus and buttons.</summary>
    public string Name { get; } = name;

    public Action Run { get; } = run;

    public UiIcon? Icon { get; init; }

    public CommandShortcut? Shortcut { get; init; }

    /// <summary>Why the command cannot run now, as its tooltip says it; null when it can. Not set: it always can.</summary>
    public Func<string?>? WhyNot { get; init; }

    /// <summary>For a command that switches something on and off, or picks one of several: whether it is on. Not set: a plain action.</summary>
    public Func<bool>? IsOn { get; init; }

    /// <summary>With <see cref="IsOn"/>: the command picks one of several (a tool), so menus mark it with a dot, not a tick.</summary>
    public bool IsChoice { get; init; }

    /// <summary>The name with the shortcut after it: "Undo (Ctrl+Z)".</summary>
    public string Label => Shortcut is { } shortcut ? $"{Name} ({shortcut.Text})" : Name;
}
