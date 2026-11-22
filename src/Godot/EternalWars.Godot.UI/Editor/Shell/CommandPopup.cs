using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// Commands as the items of a <see cref="PopupMenu"/>, for the menu bar and the context menus alike (ADR-250, ADR-286): an item shows its
/// command's icon, name and shortcut; one that cannot run now is greyed and says why in its tooltip; one that switches something on shows a
/// tick, or a dot for a choice.
/// </summary>
public static class CommandPopup
{
    /// <summary>Adds <paramref name="command"/> to <paramref name="popup"/> as the item of <paramref name="id"/>.</summary>
    public static void Add(PopupMenu popup, EditorCommand command, int id)
    {
        var index = popup.ItemCount;
        if (command.IsOn is null)
        {
            popup.AddItem(command.Name, id);
        }
        else if (command.IsChoice)
        {
            popup.AddRadioCheckItem(command.Name, id);
        }
        else
        {
            popup.AddCheckItem(command.Name, id);
        }

        popup.SetItemIcon(index, PlaceholderIcons.Of(command));
        popup.SetItemShortcutDisabled(index, true); // shown, not handled: the registry runs the shortcuts
    }

    /// <summary>Shows the item of <paramref name="id"/> as <paramref name="command"/> is now: greyed with the reason, its shortcut, its tick.</summary>
    public static void Show(PopupMenu popup, EditorCommand command, int id)
    {
        var index = popup.GetItemIndex(id);
        var whyNot = command.WhyNot?.Invoke();
        popup.SetItemDisabled(index, whyNot is not null);
        popup.SetItemTooltip(index, whyNot ?? "");
        popup.SetItemAccelerator(index, command.Shortcut is { } shortcut ? Accelerator(shortcut) : Key.None); // the user may rebind it (ADR-275)
        if (command.IsOn is { } isOn)
        {
            popup.SetItemChecked(index, isOn());
        }
    }

    /// <summary>
    /// Opens a context menu of <paramref name="items"/> (command ids, null for a separator line, tidied by <see cref="MenuItems.Tidy"/>) at <paramref name="at"/>, the pointer's place in <paramref name="parent"/>'s viewport. A chosen item runs through the
    /// registry; the menu frees itself once closed. Null when no item is left.
    /// </summary>
    public static PopupMenu? Open(Node parent, CommandRegistry commands, IReadOnlyList<string?> items, Vector2 at)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(commands);
        var ids = MenuItems.Tidy(items);
        if (ids.Count == 0)
        {
            return null;
        }

        var popup = new PopupMenu { Name = "ContextMenu" };
        parent.AddChild(popup);
        var commandIds = new List<string>();
        foreach (var id in ids)
        {
            if (id is null)
            {
                popup.AddSeparator();
                continue;
            }

            var command = commands[id];
            Add(popup, command, commandIds.Count);
            Show(popup, command, commandIds.Count);
            commandIds.Add(id);
        }

        popup.IdPressed += id => commands.Run(commandIds[(int)id]);
        popup.PopupHide += popup.QueueFree;
        popup.Popup(new Rect2I((Vector2I)at, Vector2I.Zero));
        return popup;
    }

    private static Key Accelerator(CommandShortcut shortcut) =>
        shortcut.Key
        | (shortcut.Ctrl ? (Key)KeyModifierMask.MaskCtrl : 0)
        | (shortcut.Shift ? (Key)KeyModifierMask.MaskShift : 0)
        | (shortcut.Alt ? (Key)KeyModifierMask.MaskAlt : 0);
}
