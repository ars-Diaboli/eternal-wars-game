using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>One line of the save list. <see cref="Name"/> fills the name field; unreadable files cannot be loaded.</summary>
public sealed record SaveItem(string Path, string Label, string? Name, bool Loadable);

/// <summary>
/// Save and load (ADR-058): the saves in a folder, newest first, a name field and Save / Load / Delete. Works on the
/// adventure map and in battle, because a save is the game's command log. The app lists the saves and answers the
/// events; the panel only shows and asks. Saving under an existing name replaces that save. Below the saves, the current human player's
/// auto combat setting (ADR-090, CMB-10), which the app turns into a command.
/// </summary>
public partial class GameMenuPanel : CenterContainer
{
    private readonly ItemList _list = new() { CustomMinimumSize = new Vector2(560, 220) };
    private readonly LineEdit _name = new() { PlaceholderText = "Name of the save", CustomMinimumSize = new Vector2(300, 0) };
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
    private readonly Button _save = new() { Text = "Save" };
    private readonly Button _load = new() { Text = "Load" };
    private readonly Button _delete = new() { Text = "Delete" };
    private readonly HBoxContainer _autoCombatRow = new() { Visible = false };
    private readonly Label _autoCombatLabel = new();
    private readonly OptionButton _autoCombat = new();
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

    /// <summary>Whether there is a running game to save (false on the new-game screen).</summary>
    public bool CanSave
    {
        get => _canSave;
        set
        {
            _canSave = value;
            _save.Disabled = !value;
            _name.Editable = value;
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
        AddChild(panel);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 8);
        panel.AddChild(rows);
        rows.AddChild(new Label { Text = "Save and load" });
        rows.AddChild(_list);

        var saveRow = new HBoxContainer();
        saveRow.AddChild(_name);
        saveRow.AddChild(_save);
        rows.AddChild(saveRow);
        var actions = new HBoxContainer();
        actions.AddChild(_load);
        actions.AddChild(_delete);
        var close = new Button { Text = "Close (Esc)" };
        close.Pressed += Close;
        actions.AddChild(close);
        rows.AddChild(actions);
        rows.AddChild(_message);

        _autoCombat.AddItem("Off");
        _autoCombat.AddItem("Ask before each battle");
        _autoCombat.AddItem("Always");
        _autoCombat.ItemSelected += index => AutoCombatSelected?.Invoke((int)index);
        _autoCombatRow.AddChild(_autoCombatLabel);
        _autoCombatRow.AddChild(_autoCombat);
        rows.AddChild(_autoCombatRow);

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

    public void Close() => Visible = false;

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
