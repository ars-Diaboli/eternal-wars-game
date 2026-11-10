using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>One line of the save list. <see cref="Name"/> fills the name field; unreadable files cannot be loaded.</summary>
public sealed record SaveItem(string Path, string Label, string? Name, bool Loadable);

/// <summary>
/// The game menu, in three tabs. "Save and load" (ADR-058): the saves in a folder, newest first, a name field and Save / Load / Delete. Works on
/// the adventure map and in battle, because a save is the game's command log; saving under an existing name replaces that save. "Settings":
/// the current human player's auto combat setting (ADR-090, CMB-10), which the app turns into a command, the speed settings (ADR-090, UI-1)
/// and "Detailed tooltips" (ADR-154). "Layout" (ADR-156): the initiative bar's edge, the minimap's corner, the hero and town lists and the
/// action panel's side. The app lists the saves, answers the events and keeps the settings in the settings file; the panel only shows and asks.
/// </summary>
public partial class GameMenuPanel : CenterContainer
{
    private readonly ItemList _list = new() { CustomMinimumSize = new Vector2(560, 220) };
    private readonly LineEdit _name = new() { PlaceholderText = "Name of the save", CustomMinimumSize = new Vector2(300, 0) };
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
    private readonly Button _save = new() { Text = "Save" };
    private readonly Button _load = new() { Text = "Load" };
    private readonly Button _delete = new Button { Text = "Delete" }.WithIcon(UiIcon.Close);
    private readonly Button _mainMenu = new() { Text = "Main menu", TooltipText = "Leave the game; the autosave keeps the start of this turn", Visible = false };
    private readonly HBoxContainer _autoCombatRow = new() { Visible = false };
    private readonly Label _autoCombatLabel = new();
    private readonly OptionButton _autoCombat = new();
    private readonly OptionButton _ownSpeed = SpeedButton(withHidden: false);
    private readonly OptionButton _computerSpeed = SpeedButton(withHidden: true);
    private readonly OptionButton _battleSpeed = SpeedButton(withHidden: false);
    private SpeedSettings _speeds = SpeedSettings.Default;
    private readonly CheckBox _detailed = new() { Text = "Detailed tooltips (formulas and numbers in the right-click windows)" };
    private readonly TabContainer _tabs = new() { CustomMinimumSize = new Vector2(580, 340), UseHiddenTabsForMinSize = true }; // the same size on every tab
    private readonly OptionButton _initiativeBar = Choices("Top", "Bottom");
    private readonly OptionButton _minimapCorner = Choices("Top right", "Top left", "Bottom right", "Bottom left");
    private readonly OptionButton _lists = Choices("Beside the minimap", "Along the bottom");
    private readonly OptionButton _actionSide = Choices("Right", "Left");
    private LayoutSettings _layout = LayoutSettings.Default;
    private IReadOnlyList<SaveItem> _entries = [];
    private Func<IReadOnlyList<SaveItem>> _listSaves = null!;
    private Action<string> _deleteSave = null!;
    private bool _canSave;

    /// <summary>The player asked to save under this name.</summary>
    public event Action<string>? SaveRequested;

    /// <summary>The player asked to load this file.</summary>
    public event Action<string>? LoadRequested;

    /// <summary>The menu was opened (the app shows the current player's settings).</summary>
    public event Action? Opened;

    /// <summary>The player picked an auto combat setting: 0 off, 1 ask before each battle, 2 always.</summary>
    public event Action<int>? AutoCombatSelected;

    /// <summary>The player changed a speed setting (the app keeps it in the settings file and applies it).</summary>
    public event Action<SpeedSettings>? SpeedsChanged;

    /// <summary>The player changed the interface settings ("Detailed tooltips", ADR-154); the app keeps them in the settings file.</summary>
    public event Action<InterfaceSettings>? InterfaceChanged;

    /// <summary>The player changed the layout ("Layout" tab, ADR-156); the app keeps it in the settings file and applies it at once.</summary>
    public event Action<LayoutSettings>? LayoutChanged;

    /// <summary>The player asked to leave the game for the main menu (offered only in a game).</summary>
    public event Action? MainMenuRequested;

    /// <summary>Whether there is a running game to save or leave (false on the main menu and the new-game screen).</summary>
    public bool CanSave
    {
        get => _canSave;
        set
        {
            _canSave = value;
            _save.Disabled = !value;
            _name.Editable = value;
            _mainMenu.Visible = value;
        }
    }

    public void Bind(Func<IReadOnlyList<SaveItem>> saves, Action<string> delete)
    {
        _listSaves = saves;
        _deleteSave = delete;
        AnchorRight = 1;
        AnchorBottom = 1;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var panel = new PanelContainer();
        panel.UseOpaqueBackground();
        AddChild(panel);
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", UiSpacing.Gap);
        panel.AddChild(outer);
        outer.AddChild(_tabs);
        var rows = Tab("Save and load");
        var settings = Tab("Settings");
        var layout = Tab("Layout");
        rows.AddChild(_list);

        var saveRow = new HBoxContainer();
        saveRow.AddChild(_name);
        saveRow.AddChild(_save);
        rows.AddChild(saveRow);
        var saveActions = new HBoxContainer();
        saveActions.AddChild(_load);
        saveActions.AddChild(_delete);
        rows.AddChild(saveActions);

        // Below the tabs, whichever is shown: the messages and leaving the menu.
        outer.AddChild(_message);
        var actions = new HBoxContainer();
        var close = new Button { Text = "Close (Esc)" }.WithIcon(UiIcon.Close);
        close.Pressed += Close;
        actions.AddChild(close);
        _mainMenu.Pressed += () => MainMenuRequested?.Invoke();
        actions.AddChild(_mainMenu);
        outer.AddChild(actions);

        _autoCombat.AddItem("Off");
        _autoCombat.AddItem("Ask before each battle");
        _autoCombat.AddItem("Always");
        _autoCombat.ItemSelected += index => AutoCombatSelected?.Invoke((int)index);
        _autoCombatRow.AddChild(_autoCombatLabel);
        _autoCombatRow.AddChild(_autoCombat);
        settings.AddChild(_autoCombatRow);

        settings.AddHeading("Speed (kept on this computer)");
        settings.AddRow("Your heroes", _ownSpeed, 180);
        settings.AddRow("Computer heroes", _computerSpeed, 180);
        settings.AddRow("Battle animations", _battleSpeed, 180);
        _ownSpeed.ItemSelected += index => ChangeSpeeds(_speeds with { OwnHeroes = (AnimationSpeed)index });
        _computerSpeed.ItemSelected += index => ChangeSpeeds(index == HiddenItem
            ? _speeds with { ShowComputerHeroes = false }
            : _speeds with { ComputerHeroes = (AnimationSpeed)index, ShowComputerHeroes = true });
        _battleSpeed.ItemSelected += index => ChangeSpeeds(_speeds with { Battle = (AnimationSpeed)index });
        ShowSpeeds(_speeds);
        settings.AddChild(new HSeparator());
        settings.AddChild(_detailed);
        _detailed.Toggled += on => InterfaceChanged?.Invoke(new InterfaceSettings(DetailedTooltips: on));

        // Layout (ADR-118, UI-3; ADR-156): where the screens put their parts, kept on this computer; applied at once.
        layout.AddChild(new Label { Text = "Where the screens put their parts (kept on this computer)" });
        layout.AddRow("Initiative bar (battle)", _initiativeBar, 180);
        layout.AddRow("Minimap", _minimapCorner, 180);
        layout.AddRow("Hero and town lists", _lists, 180);
        layout.AddRow("Action panel", _actionSide, 180);
        _initiativeBar.ItemSelected += index => ChangeLayout(_layout with { InitiativeBar = (ScreenEdge)index });
        _minimapCorner.ItemSelected += index => ChangeLayout(_layout with { Minimap = (ScreenCorner)index });
        _lists.ItemSelected += index => ChangeLayout(_layout with { Lists = (ListOrientation)index });
        _actionSide.ItemSelected += index => ChangeLayout(_layout with { ActionPanel = (ScreenSide)index });
        var reset = new Button { Text = "Default layout", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        reset.Pressed += () =>
        {
            ShowLayout(LayoutSettings.Default);
            ChangeLayout(LayoutSettings.Default);
        };
        layout.AddChild(reset);
        ShowLayout(_layout);

        _list.ItemSelected += index =>
        {
            if (_entries[(int)index].Name is { } name)
            {
                _name.Text = name;
            }

            UpdateButtons();
        };
        _list.ItemActivated += index => RequestLoad((int)index);
        _save.Pressed += () =>
        {
            var name = _name.Text.Trim();
            if (name.Length == 0)
            {
                SetMessage("Give the save a name.");
                return;
            }

            SaveRequested?.Invoke(name);
            Refresh();
        };
        _load.Pressed += () => RequestLoad(_list.GetSelectedItems().FirstOrDefault(-1));
        _delete.Pressed += () =>
        {
            if (_list.GetSelectedItems().FirstOrDefault(-1) is var index and >= 0)
            {
                try
                {
                    _deleteSave(_entries[index].Path);
                    SetMessage("Deleted.");
                }
                catch (IOException e)
                {
                    SetMessage($"Could not delete: {e.Message}");
                }

                Refresh();
            }
        };
        CanSave = false;
    }

    public void Toggle()
    {
        if (Visible)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        Visible = true;
        Refresh();
        Opened?.Invoke();
    }

    /// <summary>
    /// Shows <paramref name="player"/>'s auto combat setting against computer players and neutrals (0 off, 1 ask, 2 always); null hides it
    /// (no game, or no human player's turn).
    /// </summary>
    public void ShowAutoCombat(string? player, int mode)
    {
        _autoCombatRow.Visible = player is not null;
        _autoCombatLabel.Text = $"Auto combat for {player} against the computer and neutrals:";
        _autoCombat.Selected = mode;
    }

    /// <summary>Shows the speed settings read from the settings file.</summary>
    public void ShowSpeeds(SpeedSettings speeds)
    {
        _speeds = speeds;
        _ownSpeed.Selected = (int)speeds.OwnHeroes;
        _computerSpeed.Selected = speeds.ShowComputerHeroes ? (int)speeds.ComputerHeroes : HiddenItem;
        _battleSpeed.Selected = (int)speeds.Battle;
    }

    /// <summary>Shows the interface settings read from the settings file.</summary>
    public void ShowInterface(InterfaceSettings settings) => _detailed.SetPressedNoSignal(settings.DetailedTooltips);

    /// <summary>Shows the layout settings read from the settings file.</summary>
    public void ShowLayout(LayoutSettings layout)
    {
        _layout = layout;
        _initiativeBar.Selected = (int)layout.InitiativeBar;
        _minimapCorner.Selected = (int)layout.Minimap;
        _lists.Selected = (int)layout.Lists;
        _actionSide.Selected = (int)layout.ActionPanel;
    }

    public void Close() => Visible = false;

    /// <summary>The computer heroes' extra choice, after the four speeds: "Don't show".</summary>
    private const int HiddenItem = 4;

    private static OptionButton SpeedButton(bool withHidden)
    {
        var button = new OptionButton { CustomMinimumSize = new Vector2(160, 0) };
        foreach (var label in new[] { "Slow", "Normal", "Fast", "Instant" })
        {
            button.AddItem(label);
        }

        if (withHidden)
        {
            button.AddItem("Don't show");
        }

        return button;
    }

    /// <summary>A drop-down of <paramref name="labels"/>, in the order of the enum it picks from.</summary>
    private static OptionButton Choices(params string[] labels)
    {
        var button = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
        foreach (var label in labels)
        {
            button.AddItem(label);
        }

        return button;
    }

    /// <summary>A tab page: its rows, titled <paramref name="title"/>.</summary>
    private VBoxContainer Tab(string title)
    {
        var page = new VBoxContainer { Name = title };
        page.AddThemeConstantOverride("separation", UiSpacing.Gap);
        _tabs.AddChild(page);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return page;
    }

    private void ChangeSpeeds(SpeedSettings speeds)
    {
        _speeds = speeds;
        SpeedsChanged?.Invoke(speeds);
    }

    private void ChangeLayout(LayoutSettings layout)
    {
        _layout = layout;
        LayoutChanged?.Invoke(layout);
    }

    public void SetMessage(string text) => _message.Text = text;

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void RequestLoad(int index)
    {
        if (index >= 0 && index < _entries.Count)
        {
            LoadRequested?.Invoke(_entries[index].Path);
        }
    }

    private void Refresh()
    {
        _entries = _listSaves();
        _list.Clear();
        foreach (var entry in _entries)
        {
            _list.AddItem(entry.Label);
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = _list.GetSelectedItems().Length > 0 ? _list.GetSelectedItems()[0] : -1;
        _load.Disabled = selected < 0 || !_entries[selected].Loadable;
        _delete.Disabled = selected < 0;
    }
}
