using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>The tabs of the <see cref="MapSettingsWindow"/>, in their order.</summary>
public enum MapSettingsTab
{
    General,
    Players,
    VictoryAndLoss,
    Bans,
    WeekEvents,
}

/// <summary>
/// The map settings window (EDIT-1, ADR-256), opened from Map → Map settings or F7: the map's settings in tabs over the editor (General,
/// Players, Victory and loss, Bans, Week events). Each change is an undoable session edit at once, as in a panel; Undo and Redo (also
/// Ctrl+Z and Ctrl+Y outside a text field) take them back while the window is open. Close, Escape or F7 closes it; the next opening shows
/// the tab shown last.
/// </summary>
public partial class MapSettingsWindow : Control
{
    private static readonly string[] Titles = ["General", "Players", "Victory and loss", "Bans", "Week events"];

    /// <summary>The tab shown last, for the next opening (kept while the editor runs).</summary>
    private static MapSettingsTab _last = MapSettingsTab.General;

    private readonly TabContainer _tabs = new() { Name = "Tabs", CustomMinimumSize = new Vector2(780, 520) };
    private readonly Button _undo = new() { Text = "Undo", CustomMinimumSize = new Vector2(110, 0) };
    private readonly Button _redo = new() { Text = "Redo", CustomMinimumSize = new Vector2(110, 0) };
    private MapEditorSession _editor = null!;

    /// <summary>The tab the window shows.</summary>
    public MapSettingsTab Tab => (MapSettingsTab)_tabs.CurrentTab;

    /// <summary>Fills the window (it is in the tree); <paramref name="tab"/> null shows the tab shown last.</summary>
    public void Bind(MapEditorSession editor, MapSettingsTab? tab = null)
    {
        _editor = editor;
        var rows = EditorOverlay.Build(this);
        rows.AddChild(new Label { Text = "Map settings", HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(_tabs);
        var general = Page(MapSettingsTab.General, new GeneralSettingsPanel());
        general.Bind(editor);
        Page(MapSettingsTab.Players, new PlayersPanel()).Bind(editor);
        Page(MapSettingsTab.VictoryAndLoss, new ConditionsPanel()).Bind(editor);
        Page(MapSettingsTab.Bans, new BansPanel()).Bind(editor);
        Page(MapSettingsTab.WeekEvents, new WeekEventsPanel()).Bind(editor);
        _last = tab ?? _last;
        _tabs.CurrentTab = (int)_last;
        _tabs.TabChanged += index =>
        {
            if (IsInsideTree()) // freeing the pages also changes the tab
            {
                _last = (MapSettingsTab)(int)index;
            }
        };

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        rows.AddChild(buttons);
        _undo.Pressed += () => editor.Undo();
        buttons.AddChild(_undo);
        _redo.Pressed += () => editor.Redo();
        buttons.AddChild(_redo);
        var close = new Button { Text = "Close", CustomMinimumSize = new Vector2(110, 0) };
        close.Pressed += () => EditorOverlay.Close(this);
        buttons.AddChild(close);
        ShowHistory();
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => ShowHistory());
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        var typing = GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;
        if (key.Keycode is Key.Escape or Key.F7)
        {
            GetViewport().SetInputAsHandled(); // before the window leaves the tree, so the editor's shortcuts never see the key
            EditorOverlay.Close(this);
            return;
        }
        else if (key.CtrlPressed && !typing && key.Keycode is Key.Z or Key.Y)
        {
            if (key.Keycode == Key.Z)
            {
                _editor.Undo();
            }
            else
            {
                _editor.Redo();
            }
        }
        else
        {
            return;
        }

        GetViewport()?.SetInputAsHandled();
    }

    /// <summary>A tab: <paramref name="panel"/> in a page that scrolls up and down.</summary>
    private T Page<T>(MapSettingsTab tab, T panel)
        where T : Control
    {
        var page = new ScrollContainer { Name = Titles[(int)tab], HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _tabs.AddChild(page);
        panel.Name = tab.ToString();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        page.AddChild(panel);
        return panel;
    }

    private void ShowHistory()
    {
        _undo.Disabled = !_editor.History.CanUndo;
        _undo.TooltipText = _editor.History.UndoDescription ?? "Nothing to undo";
        _redo.Disabled = !_editor.History.CanRedo;
    }
}
