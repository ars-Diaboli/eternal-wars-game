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
/// with a splitter and collapsed (<see cref="SetOpen"/>); <see cref="ToggleAll"/> hides and shows them all; a status bar at the bottom. The map itself is drawn behind the workspace: the
/// middle is a hole that lets the pointer through to the app. The regions are the "Map" page; other pages (EDIT-3: the template editor)
/// fill the room between the bars instead (<see cref="AddPage"/>), and a tab for each page stands after the menus. Every page stays as
/// it was left while another shows. The splitter positions, the collapsed regions and each region's open tab are
/// kept in the <see cref="EditorSettingsFile"/> on every change.
/// </summary>
public partial class EditorWorkspace : Control
{
    private const string Section = "layout";

    /// <summary>The page with the regions round the map view: the first tab.</summary>
    public const int MapPage = 0;

    /// <summary>Each region's splitter offset when no settings file says otherwise: the left one's width, the others' size from the far edge.</summary>
    private static readonly Dictionary<EditorRegion, int> DefaultOffsets = new()
    {
        [EditorRegion.Left] = 320,
        [EditorRegion.Right] = -560,
        [EditorRegion.Bottom] = -200,
    };

    private readonly Dictionary<EditorRegion, TabContainer> _regions = [];
    private readonly Dictionary<EditorRegion, PanelContainer> _panels = [];
    private readonly Dictionary<EditorRegion, VBoxContainer> _footers = [];
    private readonly Dictionary<EditorRegion, bool> _open = [];
    private readonly Dictionary<EditorRegion, string> _savedTabs = [];
    private readonly Dictionary<Control, Control> _tabOf = [];
    private EditorSettingsFile _settings = null!;
    private readonly List<Control> _pages = [];
    private readonly TabBar _pageTabs = new() { Name = "Pages", ClipTabs = false, FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkEnd };
    private readonly Control _body = new() { Name = "Body", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill };
    private bool _allHidden;
    private bool _addingTab;

    /// <summary>A region was collapsed, opened, hidden or shown.</summary>
    public event Action? RegionsChanged;

    /// <summary>Another page shows.</summary>
    public event Action? PageChanged;

    /// <summary>The page that shows: <see cref="MapPage"/>, or the number <see cref="AddPage"/> gave.</summary>
    public int Page { get; private set; }

    /// <summary>The bar above the pages: the app adds its rows here, under the <see cref="MenuRow"/>.</summary>
    public VBoxContainer TopBar { get; } = new() { Name = "TopBar", SizeFlagsHorizontal = SizeFlags.ExpandFill };

    /// <summary>The first row of the <see cref="TopBar"/>: the app's menus (<see cref="SetMenus"/>), then the pages' tabs.</summary>
    public HBoxContainer MenuRow { get; } = new() { Name = "MenuRow" };

    /// <summary>The bar under the regions: the app adds its status bar here.</summary>
    public HBoxContainer StatusBar { get; } = new() { Name = "StatusBar", SizeFlagsHorizontal = SizeFlags.ExpandFill };

    /// <summary>The room the map shows through, between the regions; it lets the pointer through (what is put on it may take it).</summary>
    public Control MapView { get; } = new() { Name = "MapView", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill };

    /// <summary>
    /// Builds the regions and applies the layout kept in <paramref name="settings"/>. Add the tabs after this.
    /// </summary>
    public void Bind(EditorSettingsFile settings)
    {
        _settings = settings;

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 0);
        AddChild(rows);
        rows.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var bar = Opaque(new PanelContainer { Name = "Bar" });
        rows.AddChild(bar);
        bar.AddChild(TopBar);
        TopBar.AddChild(MenuRow);
        MenuRow.AddChild(_pageTabs);
        _pageTabs.AddTab("Map");
        _pageTabs.TabChanged += index => ShowPage((int)index);
        rows.AddChild(_body);

        // Left | (map over Bottom) | Right. A region is the side of its splitter that does not expand, so the map takes what is left.
        var outer = Splitter(new HSplitContainer { Name = "Outer" }, EditorRegion.Left);
        _body.AddChild(outer);
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pages.Add(outer);
        outer.AddChild(Region(EditorRegion.Left));
        var inner = Splitter(new HSplitContainer { Name = "Inner", SizeFlagsHorizontal = SizeFlags.ExpandFill }, EditorRegion.Right);
        outer.AddChild(inner);
        var centre = Splitter(new VSplitContainer { Name = "Centre", SizeFlagsHorizontal = SizeFlags.ExpandFill }, EditorRegion.Bottom);
        inner.AddChild(centre);
        centre.AddChild(MapView);
        centre.AddChild(Region(EditorRegion.Bottom));
        inner.AddChild(Region(EditorRegion.Right));

        var status = Opaque(new PanelContainer { Name = "StatusPanel" });
        rows.AddChild(status);
        status.AddChild(StatusBar);
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

    /// <summary>
    /// Shows <paramref name="footer"/> under <paramref name="region"/>'s tabs, whichever tab is open (the minimap under the right region's),
    /// in place of the one shown before, which is freed.
    /// </summary>
    public void SetFooter(EditorRegion region, Control footer)
    {
        var holder = _footers[region];
        foreach (var old in holder.GetChildren())
        {
            holder.RemoveChild(old);
            old.QueueFree();
        }

        holder.AddChild(footer);
    }

    /// <summary>Puts the app's menus first in the <see cref="MenuRow"/>.</summary>
    public void SetMenus(Control menus)
    {
        MenuRow.AddChild(menus);
        MenuRow.MoveChild(menus, 0);
    }

    /// <summary>
    /// Adds a page that fills the room between the bars while its tab, titled <paramref name="title"/>, is the open one. Returns its
    /// number for <see cref="ShowPage"/>.
    /// </summary>
    public int AddPage(string title, Control page)
    {
        page.Visible = false;
        _body.AddChild(page);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pages.Add(page);
        _pageTabs.AddTab(title);
        return _pages.Count - 1;
    }

    /// <summary>Gives a page's tab another title (the Template tab's asterisk over unsaved work).</summary>
    public void SetPageTitle(int page, string title) => _pageTabs.SetTabTitle(page, title);

    /// <summary>Shows the page of that number; the others stay as they are, hidden.</summary>
    public void ShowPage(int page)
    {
        if (page == Page)
        {
            return;
        }

        Page = page;
        for (var i = 0; i < _pages.Count; i++)
        {
            _pages[i].Visible = i == page;
        }

        _pageTabs.CurrentTab = page;
        PageChanged?.Invoke();
    }

    /// <summary>
    /// Brings <paramref name="panel"/>'s tab to the front, opening its region when that is collapsed or hidden, on the
    /// <see cref="MapPage"/>.
    /// </summary>
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
            SetOpen(region, true);
            return;
        }
    }

    /// <summary>Whether <paramref name="region"/> shows (not collapsed and not hidden with the others).</summary>
    public bool IsShown(EditorRegion region) => _panels[region].Visible;

    /// <summary>Whether every region is hidden (<see cref="ToggleAll"/>).</summary>
    public bool AllHidden => _allHidden;

    /// <summary>Hides every region, or shows those that were open again. Not kept in the settings.</summary>
    public void ToggleAll()
    {
        _allHidden = !_allHidden;
        ApplyVisibility();
    }

    private SplitContainer Splitter(SplitContainer splitter, EditorRegion region)
    {
        splitter.MouseFilter = MouseFilterEnum.Ignore; // the draggers are controls of their own
        splitter.SplitOffsets = [_settings.Get(Section, SettingKey(region, "offset"), DefaultOffsets[region]).AsInt32()];
        splitter.DragEnded += () =>
        {
            _settings.Set(Section, SettingKey(region, "offset"), splitter.SplitOffsets[0]);
        };
        return splitter;
    }

    private PanelContainer Region(EditorRegion region)
    {
        var horizontal = region != EditorRegion.Bottom;
        var panel = Opaque(new PanelContainer
        {
            Name = region.ToString(),
            CustomMinimumSize = horizontal ? new Vector2(160, 0) : new Vector2(0, 80),
        });
        var column = new VBoxContainer { Name = "Column" };
        panel.AddChild(column);
        var tabs = new TabContainer { Name = "Tabs", SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(tabs);
        var footer = new VBoxContainer { Name = "Footer" };
        column.AddChild(footer);
        _footers[region] = footer;
        _panels[region] = panel;
        tabs.TabChanged += index =>
        {
            if (_addingTab || index < 0)
            {
                return;
            }

            _settings.Set(Section, SettingKey(region, "tab"), tabs.GetTabControl((int)index).Name.ToString());
        };
        _regions[region] = tabs;
        _open[region] = _settings.Get(Section, SettingKey(region, "open"), true).AsBool();
        _savedTabs[region] = _settings.Get(Section, SettingKey(region, "tab"), "").AsString();
        return panel;
    }

    /// <summary>
    /// Opens or collapses <paramref name="region"/>, and shows the regions again when they were all hidden. Opening one shows the
    /// <see cref="MapPage"/>.
    /// </summary>
    public void SetOpen(EditorRegion region, bool open)
    {
        if (open)
        {
            ShowPage(MapPage);
        }

        _allHidden = false;
        if (_open[region] != open)
        {
            _open[region] = open;
            _settings.Set(Section, SettingKey(region, "open"), open);
        }

        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        foreach (var (region, panel) in _panels)
        {
            panel.Visible = _open[region] && !_allHidden;
        }

        RegionsChanged?.Invoke();
    }

    /// <summary>A docked panel hides the map behind it (the theme's panels over the map let it show through).</summary>
    private static PanelContainer Opaque(PanelContainer panel)
    {
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTheme.PanelBackground, 1), UiTheme.Border, UiSpacing.Padding));
        return panel;
    }

    private static string SettingKey(EditorRegion region, string what) => $"{region.ToString().ToLowerInvariant()}_{what}";
}
