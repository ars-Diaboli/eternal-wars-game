using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>One menu of the <see cref="MenuBarView"/>: its title and its commands' ids in order, null for a separator line.</summary>
public sealed record CommandMenu(string Title, IReadOnlyList<string?> Items);

/// <summary>
/// The editor's menus, built from the <see cref="CommandRegistry"/>. An item shows its command's name and shortcut; one that cannot run now
/// is greyed and says why in its tooltip. The shortcuts are only shown here: the app hands the keys to the registry.
/// </summary>
public partial class MenuBarView : MenuBar
{
    private CommandRegistry _commands = null!;

    public void Bind(CommandRegistry commands, IReadOnlyList<CommandMenu> menus)
    {
        _commands = commands;
        foreach (var menu in menus)
        {
            var popup = new PopupMenu { Name = menu.Title };
            AddChild(popup);
            var ids = new List<string>();
            foreach (var id in menu.Items)
            {
                if (id is null)
                {
                    popup.AddSeparator();
                    continue;
                }

                var command = commands[id];
                var index = popup.ItemCount;
                if (command.IsOn is null)
                {
                    popup.AddItem(command.Name, ids.Count);
                }
                else if (command.IsChoice)
                {
                    popup.AddRadioCheckItem(command.Name, ids.Count);
                }
                else
                {
                    popup.AddCheckItem(command.Name, ids.Count);
                }

                if (command.Shortcut is { } shortcut)
                {
                    popup.SetItemAccelerator(index, Accelerator(shortcut));
                    popup.SetItemShortcutDisabled(index, true); // shown, not handled: the registry runs the shortcuts
                }

                ids.Add(id);
            }

            popup.AboutToPopup += () => Refresh(popup, ids);
            popup.IdPressed += id => _commands.Run(ids[(int)id]);
            Refresh(popup, ids);
        }
    }

    private void Refresh(PopupMenu popup, List<string> ids)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            var command = _commands[ids[i]];
            var index = popup.GetItemIndex(i);
            var whyNot = command.WhyNot?.Invoke();
            popup.SetItemDisabled(index, whyNot is not null);
            popup.SetItemTooltip(index, whyNot ?? "");
            if (command.IsOn is { } isOn)
            {
                popup.SetItemChecked(index, isOn());
            }
        }
    }

    private static Key Accelerator(CommandShortcut shortcut) =>
        shortcut.Key
        | (shortcut.Ctrl ? (Key)KeyModifierMask.MaskCtrl : 0)
        | (shortcut.Shift ? (Key)KeyModifierMask.MaskShift : 0)
        | (shortcut.Alt ? (Key)KeyModifierMask.MaskAlt : 0);
}
