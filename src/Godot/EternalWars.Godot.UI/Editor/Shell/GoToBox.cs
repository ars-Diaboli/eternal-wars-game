using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// Go to (Ctrl+G, ADR-261): a search field and what fits it as you type (<see cref="MapGoTo"/>): a tile by its numbers ("12, 7", a third
/// for the level), the map's heroes, named objects and events by name. Up and Down move through the list, Enter or a click goes there,
/// Escape closes it. It frees itself before the view moves.
/// </summary>
public partial class GoToBox : Control
{
    private readonly LineEdit _search = new() { PlaceholderText = "A tile (12, 7) or a name", CustomMinimumSize = new Vector2(520, 0) };
    private readonly Tree _list = new()
    {
        HideRoot = true, SelectMode = Tree.SelectModeEnum.Row, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(520, 340),
    };

    private readonly Label _none = new() { Text = "Nothing on the map fits.", Visible = false };
    private readonly List<MapLink> _shown = [];
    private GameState _scenario = null!;
    private int _level;
    private Action<MapLink> _go = _ => { };
    private int _selected;

    /// <summary>Searches <paramref name="scenario"/>; <paramref name="level"/> is the shown level, a tile's when none is typed.</summary>
    public void Bind(GameState scenario, int level, Action<MapLink> go)
    {
        (_scenario, _level, _go) = (scenario, level, go);
        var rows = EditorOverlay.Build(this, atTop: true);
        rows.AddChild(new Label { Text = "Go to" });
        rows.AddChild(_search);
        rows.AddChild(_list);
        rows.AddChild(_none);
        _search.TextChanged += _ => Fill();
        _search.TextSubmitted += _ => Go();
        _list.ItemMouseSelected += (_, button) =>
        {
            if (button != (long)MouseButton.Left)
            {
                return;
            }

            _selected = _list.GetSelected()?.GetIndex() ?? _selected;
            Go();
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
                GetViewport().SetInputAsHandled();
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
        foreach (var link in MapGoTo.Find(_scenario, _search.Text, _level))
        {
            _list.CreateItem(root).SetText(0, link.Text);
            _shown.Add(link);
        }

        _none.Visible = _shown.Count == 0;
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

    private void Go()
    {
        if (_shown.Count == 0)
        {
            return;
        }

        var link = _shown[_selected];
        if (EditorOverlay.Close(this))
        {
            _go(link);
        }
    }
}
