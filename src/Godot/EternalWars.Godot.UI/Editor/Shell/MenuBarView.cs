using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// One menu of the <see cref="MenuBarView"/>: its title and its commands' ids in order, null for a separator line, and
/// <c>&gt;title</c> for the one of its <see cref="Lists"/> with that title.
/// </summary>
public sealed record CommandMenu(string Title, IReadOnlyList<string?> Items)
{
    public IReadOnlyList<CommandList> Lists { get; init; } = [];
}

/// <summary>
/// A submenu whose items are not commands but a list read each time the menu opens (the recent files): every item's text and what it
/// does. With no items the submenu is greyed and its tooltip says <paramref name="Empty"/>.
/// </summary>
public sealed record CommandList(string Title, Func<IReadOnlyList<(string Text, Action Run)>> Items, string Empty);

/// <summary>
/// The editor's menus, built from the <see cref="CommandRegistry"/>. An item shows its command's icon, name and shortcut; one that cannot run now
/// is greyed and says why in its tooltip. The shortcuts are only shown here: the app hands the keys to the registry.
/// </summary>
public partial class MenuBarView : MenuBar
{
    private const int ListIds = 1000;

    private CommandRegistry _commands = null!;

    public void Bind(CommandRegistry commands, IReadOnlyList<CommandMenu> menus)
    {
        _commands = commands;
        foreach (var menu in menus)
        {
            var popup = new PopupMenu { Name = menu.Title };
            AddChild(popup);
            var ids = new List<string>();
            var lists = new List<(int Id, PopupMenu Popup, CommandList List)>();
            foreach (var id in menu.Items)
            {
                if (id is null)
                {
                    popup.AddSeparator();
                    continue;
                }

                if (id.StartsWith('>'))
                {
                    var list = menu.Lists.First(l => l.Title == id[1..]);
                    var items = new PopupMenu { Name = list.Title };
                    var listId = ListIds + lists.Count; // its own, away from the commands' ids
                    popup.AddSubmenuNodeItem(list.Title, items, listId);
                    items.IdPressed += item =>
                    {
                        list.Items()[(int)item].Run();
                        _commands.NotifyStateChanged();
                    };
                    lists.Add((listId, items, list));
                    continue;
                }

                CommandPopup.Add(popup, commands[id], ids.Count);
                ids.Add(id);
            }

            popup.AboutToPopup += () =>
            {
                Refresh(popup, ids);
                foreach (var (id, items, list) in lists)
                {
                    Fill(popup, popup.GetItemIndex(id), items, list);
                }
            };
            popup.IdPressed += id =>
            {
                if (id < ListIds)
                {
                    _commands.Run(ids[(int)id]);
                }
            };
            Refresh(popup, ids);
        }
    }

    private static void Fill(PopupMenu popup, int index, PopupMenu items, CommandList list)
    {
        var current = list.Items();
        items.Clear();
        foreach (var (text, _) in current)
        {
            items.AddItem(text);
        }

        popup.SetItemDisabled(index, current.Count == 0);
        popup.SetItemTooltip(index, current.Count == 0 ? list.Empty : "");
    }

    private void Refresh(PopupMenu popup, List<string> ids)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            CommandPopup.Show(popup, _commands[ids[i]], i);
        }
    }
}
