using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>A docked part of the <see cref="EditorWorkspace"/>: a tab box beside or under the map view.</summary>
public enum EditorRegion
{
    Left,
    Right,
    Bottom,
}

/// <summary>
/// The editor's window layout (EDIT-1, ADR-250): a bar on top, then three docked regions around the map view, each a tab box that is resized
/// with a splitter and collapsed with its button on the bar; F11 hides and shows them all. The map itself is drawn behind the workspace: the
/// middle is a hole that lets the pointer through to the app. The splitter positions, the collapsed regions and each region's open tab are
/// kept in the editor's settings file (<see cref="SettingsFileName"/>) on every change.
/// </summary>
public partial class EditorWorkspace : Control
{
    /// <summary>The editor's own settings file, in the user data folder.</summary>
    public const string SettingsFileName = "editor-settings.cfg";

    private const string Section = "layout";

    /// <summary>Each region's splitter offset when no settings file says otherwise: the left one's width, the others' size from the far edge.</summary>
    private static readonly Dictionary<EditorRegion, int> DefaultOffsets = new()
    {
        [EditorRegion.Left] = 320,
        [EditorRegion.Right] = -560,
        [EditorRegion.Bottom] = -200,
    };

    private readonly Dictionary<EditorRegion, TabContainer> _regions = [];
    private readonly Dictionary<EditorRegion, PanelContainer> _panels = [];
    private readonly Dictionary<EditorRegion, Button> _toggles = [];
    private readonly Dictionary<EditorRegion, bool> _open = [];
    private readonly Dictionary<EditorRegion, string> _savedTabs = [];
    private readonly Dictionary<Control, Control> _tabOf = [];
    private readonly ConfigFile _settings = new();
    private string? _settingsPath;
    private bool _allHidden;
    private bool _addingTab;

    /// <summary>The bar above the regions: the app adds its rows here.</summary>
    public VBoxContainer TopBar { get; } = new() { Name = "TopBar", SizeFlagsHorizontal = SizeFlags.ExpandFill };

    /// <summary>
    /// Builds the regions and applies the layout kept in <paramref name="settingsPath"/> (null: the defaults, and nothing is kept). Add the
    /// tabs after this.
    /// </summary>
    public void Bind(string? settingsPath)
    {
        _settingsPath = settingsPath;
        if (settingsPath is not null && File.Exists(settingsPath) && _settings.Load(settingsPath) != Error.Ok)
        {
            GD.PushWarning($"Could not read {settingsPath}: the default layout is used.");
            _settings.Clear();
        }

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 0);
        AddChild(rows);
        rows.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var bar = Opaque(new PanelContainer { Name = "Bar" });
        rows.AddChild(bar);
        var barRow = new HBoxContainer();
        bar.AddChild(barRow);
        barRow.AddChild(TopBar);
        var toggles = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
        barRow.AddChild(toggles);

        // Left | (map over Bottom) | Right. A region is the side of its splitter that does not expand, so the map takes what is left.
        var outer = Splitter(new HSplitContainer { Name = "Outer", SizeFlagsVertical = SizeFlags.ExpandFill }, EditorRegion.Left);
        rows.AddChild(outer);
        outer.AddChild(Region(EditorRegion.Left, toggles));
        var inner = Splitter(new HSplitContainer { Name = "Inner", SizeFlagsHorizontal = SizeFlags.ExpandFill }, EditorRegion.Right);
        outer.AddChild(inner);
        var centre = Splitter(new VSplitContainer { Name = "Centre", SizeFlagsHorizontal = SizeFlags.ExpandFill }, EditorRegion.Bottom);
        inner.AddChild(centre);
        centre.AddChild(new Control { Name = "MapView", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill });
        centre.AddChild(Region(EditorRegion.Bottom, toggles));
        inner.AddChild(Region(EditorRegion.Right, toggles));
        ApplyVisibility();
    }

    /// <summary>
    /// Adds <paramref name="panel"/> as a tab of <paramref name="region"/>, titled with its node name. The tab that was open when the layout
    /// was last kept opens again. With <paramref name="scroll"/> a panel larger than its region scrolls, so it never widens the region; pass
    /// false for a panel that scrolls by itself (a list).
    /// </summary>
    public void AddTab(EditorRegion region, Control panel, UiIcon? icon = null, bool scroll = true)
    {
        var tabs = _regions[region];
        var tab = panel;
        if (scroll)
        {
            tab = new ScrollContainer { Name = panel.Name };
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsVertical = SizeFlags.ExpandFill;
            tab.AddChild(panel);
        }

        _tabOf[panel] = tab;
        _addingTab = true;
        tabs.AddChild(tab);
        if (icon is { } shown)
        {
            tabs.WithTabIcon(tab.GetIndex(), shown);
        }

        if (_savedTabs.GetValueOrDefault(region) == tab.Name.ToString())
        {
            tabs.CurrentTab = tab.GetIndex();
        }

        _addingTab = false;
    }

    /// <summary>Brings <paramref name="panel"/>'s tab to the front, opening its region when that is collapsed or hidden.</summary>
    public void ShowTab(Control panel)
    {
        var tab = _tabOf[panel];
        foreach (var (region, tabs) in _regions)
        {
            if (tab.GetParent() != tabs)
            {
                continue;
            }

            tabs.CurrentTab = tab.GetIndex();
            _allHidden = false;
            SetOpen(region, true);
            return;
        }
    }

    /// <summary>Whether <paramref name="region"/> shows (not collapsed and not hidden with the others).</summary>
    public bool IsShown(EditorRegion region) => _panels[region].Visible;

    /// <summary>F11 hides every region, and shows those that were open again.</summary>
    public override void _ShortcutInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            _allHidden = !_allHidden;
            ApplyVisibility();
            GetViewport().SetInputAsHandled();
        }
    }

    private SplitContainer Splitter(SplitContainer splitter, EditorRegion region)
    {
        splitter.MouseFilter = MouseFilterEnum.Ignore; // the draggers are controls of their own
        splitter.SplitOffsets = [_settings.GetValue(Section, SettingKey(region, "offset"), DefaultOffsets[region]).AsInt32()];
        splitter.DragEnded += () =>
        {
            _settings.SetValue(Section, SettingKey(region, "offset"), splitter.SplitOffsets[0]);
            Save();
        };
        return splitter;
    }

    private PanelContainer Region(EditorRegion region, HBoxContainer toggles)
    {
        var horizontal = region != EditorRegion.Bottom;
        var panel = Opaque(new PanelContainer
        {
            Name = region.ToString(),
            CustomMinimumSize = horizontal ? new Vector2(160, 0) : new Vector2(0, 80),
        });
        var tabs = new TabContainer { Name = "Tabs" };
        panel.AddChild(tabs);
        _panels[region] = panel;
        tabs.TabChanged += index =>
        {
            if (_addingTab || index < 0)
            {
                return;
            }

            _settings.SetValue(Section, SettingKey(region, "tab"), tabs.GetTabControl((int)index).Name.ToString());
            Save();
        };
        _regions[region] = tabs;
        _open[region] = _settings.GetValue(Section, SettingKey(region, "open"), true).AsBool();
        _savedTabs[region] = _settings.GetValue(Section, SettingKey(region, "tab"), "").AsString();

        var toggle = new Button
        {
            Text = $"{region} panel",
            ToggleMode = true,
            TooltipText = $"Show or collapse the {region.ToString().ToLowerInvariant()} region (F11: all of them)",
        };
        toggle.Toggled += on =>
        {
            _allHidden = false;
            SetOpen(region, on);
        };
        toggles.AddChild(toggle);
        _toggles[region] = toggle;
        return panel;
    }

    private void SetOpen(EditorRegion region, bool open)
    {
        if (_open[region] != open)
        {
            _open[region] = open;
            _settings.SetValue(Section, SettingKey(region, "open"), open);
            Save();
        }

        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        foreach (var (region, panel) in _panels)
        {
            panel.Visible = _open[region] && !_allHidden;
            _toggles[region].SetPressedNoSignal(panel.Visible);
        }
    }

    /// <summary>A docked panel hides the map behind it (the theme's panels over the map let it show through).</summary>
    private static PanelContainer Opaque(PanelContainer panel)
    {
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTheme.PanelBackground, 1), UiTheme.Border, UiSpacing.Padding));
        return panel;
    }

    private void Save()
    {
        if (_settingsPath is not null && _settings.Save(_settingsPath) != Error.Ok)
        {
            GD.PushWarning($"Could not keep the layout in {_settingsPath}.");
        }
    }

    private static string SettingKey(EditorRegion region, string what) => $"{region.ToString().ToLowerInvariant()}_{what}";
}
