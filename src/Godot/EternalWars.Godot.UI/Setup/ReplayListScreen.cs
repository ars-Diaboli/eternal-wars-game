using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// "Watch a replay" in the main menu (ADR-101, ADR-164): two tabs, Replays (<c>user://replays</c>) and Saves (the save folder, autosaves too),
/// newest first. Watch (or a double click) asks the app to open the selected file in the replay viewer; a save plays to its last command.
/// Back (or Escape, which the app maps) returns. The app lists the files and shows why one cannot be watched with <see cref="SetMessage"/>.
/// </summary>
public partial class ReplayListScreen : PanelContainer
{
    private readonly TabContainer _tabs = new() { CustomMinimumSize = new Vector2(720, 360) };
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(720, 0) };
    private readonly Button _watch = new() { Text = "Watch", Disabled = true };
    private readonly Button _back = new Button { Text = "Back (Esc)" }.WithIcon(UiIcon.ArrowLeft);
    private readonly List<(ItemList List, IReadOnlyList<SaveItem> Items)> _pages = [];

    /// <summary>The player asked to watch this file.</summary>
    public event Action<string>? WatchRequested;

    public event Action? BackRequested;

    public ReplayListScreen()
    {
        this.UseMenuPanel();
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(rows);
        rows.AddChild(new Label { Text = "Watch a replay", HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(_tabs);
        rows.AddChild(_message);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", UiSpacing.Gap);
        rows.AddChild(buttons);
        buttons.AddChild(_back);
        buttons.AddChild(_watch);
        _back.Pressed += () => BackRequested?.Invoke();
        _watch.Pressed += RequestWatch;
        _tabs.TabChanged += _ => UpdateButton();
        _back.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Fills the two tabs; an empty one says so.</summary>
    public void Bind(IReadOnlyList<SaveItem> replays, IReadOnlyList<SaveItem> saves)
    {
        AddPage("Replays", replays, "No replays yet: an end screen's \"Save replay\" keeps one.");
        AddPage("Saves", saves, "No saves yet.");
        UpdateButton();
    }

    public void SetMessage(string text) => _message.Text = text;

    private void AddPage(string title, IReadOnlyList<SaveItem> items, string empty)
    {
        var list = new ItemList { Name = title, SizeFlagsVertical = SizeFlags.ExpandFill };
        foreach (var item in items)
        {
            list.AddItem(item.Label);
            list.SetItemDisabled(list.ItemCount - 1, !item.Loadable);
        }

        if (items.Count == 0)
        {
            list.AddItem(empty);
            list.SetItemDisabled(0, true);
        }

        list.ItemSelected += _ => UpdateButton();
        list.ItemActivated += _ => RequestWatch();
        _tabs.AddChild(list);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        _pages.Add((list, items));
    }

    private SaveItem? Selected()
    {
        if (_tabs.CurrentTab < 0 || _tabs.CurrentTab >= _pages.Count)
        {
            return null;
        }

        var (list, items) = _pages[_tabs.CurrentTab];
        var selected = list.GetSelectedItems();
        return selected.Length > 0 && selected[0] < items.Count && items[selected[0]].Loadable ? items[selected[0]] : null;
    }

    private void UpdateButton() => _watch.Disabled = Selected() is null;

    private void RequestWatch()
    {
        if (Selected() is { } item)
        {
            SetMessage("Checking the record…");
            WatchRequested?.Invoke(item.Path);
        }
    }
}
