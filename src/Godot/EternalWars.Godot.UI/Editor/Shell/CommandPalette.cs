using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// The command palette (EDIT-1, ADR-250): a search field over the <see cref="CommandRegistry"/> and the commands that fit what was typed
/// (<see cref="CommandSearch"/>), each with its icon and its shortcut. Only commands that can run when the palette opens are offered.
/// Up and Down move through the list, Enter or a click runs the command, Escape closes the palette. It frees itself before the command runs.
/// </summary>
public partial class CommandPalette : Control
{
    private readonly LineEdit _search = new() { PlaceholderText = "Type a command's name", CustomMinimumSize = new Vector2(520, 0) };
    private readonly Tree _list = new()
    {
        Columns = 2, HideRoot = true, SelectMode = Tree.SelectModeEnum.Row, FocusMode = FocusModeEnum.None,
        CustomMinimumSize = new Vector2(520, 340),
    };

    private readonly List<EditorCommand> _shown = [];
    private CommandRegistry _commands = null!;
    private List<EditorCommand> _offered = [];
    private int _selected;

    /// <summary>Offers the commands of <paramref name="commands"/> that can run now, but not <paramref name="except"/> (the one that opens the palette).</summary>
    public void Bind(CommandRegistry commands, string except)
    {
        _commands = commands;
        _offered = [.. commands.All.Where(c => c.Id != except && c.WhyNot?.Invoke() is null)];
        var rows = EditorOverlay.Build(this, atTop: true);
        rows.AddChild(_search);
        rows.AddChild(_list);
        _list.SetColumnExpand(1, false);
        _list.SetColumnCustomMinimumWidth(1, 150);
        _search.TextChanged += _ => Fill();
        _search.TextSubmitted += _ => Run();
        _list.ItemMouseSelected += (_, _) =>
        {
            _selected = _list.GetSelected()?.GetIndex() ?? _selected;
            Run();
        };
        Fill();
        _search.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Escape:
                GetViewport().SetInputAsHandled(); // before the palette leaves the tree: the key goes no further
                EditorOverlay.Close(this);
                return;
            case Key.Down:
                Select(_selected + 1);
                break;
            case Key.Up:
                Select(_selected - 1);
                break;
            default:
                return;
        }

        GetViewport()?.SetInputAsHandled();
    }

    private void Fill()
    {
        _list.Clear();
        _shown.Clear();
        var root = _list.CreateItem();
        foreach (var index in CommandSearch.Find(_search.Text, [.. _offered.Select(c => c.Name)]))
        {
            var command = _offered[index];
            var item = _list.CreateItem(root);
            item.SetIcon(0, PlaceholderIcons.Of(command));
            item.SetText(0, command.Name);
            item.SetText(1, command.Shortcut?.Text ?? "");
            item.SetTextAlignment(1, HorizontalAlignment.Right);
            item.SetCustomColor(1, UiTheme.TextQuiet);
            _shown.Add(command);
        }

        _selected = 0;
        Select(0);
    }

    private void Select(int index)
    {
        if (_shown.Count == 0)
        {
            return;
        }

        _selected = Math.Clamp(index, 0, _shown.Count - 1);
        var item = _list.GetRoot().GetChild(_selected);
        item.Select(0);
        _list.ScrollToItem(item);
    }

    private void Run()
    {
        if (_shown.Count == 0)
        {
            return;
        }

        var command = _shown[_selected];
        if (EditorOverlay.Close(this))
        {
            _commands.Run(command.Id);
        }
    }
}
