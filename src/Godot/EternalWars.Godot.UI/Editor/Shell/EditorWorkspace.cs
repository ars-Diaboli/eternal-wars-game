using EternalWars.Godot.UI.Editor.Help;
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
/// with a splitter and collapsed (<see cref="SetOpen"/>); <see cref="ToggleAll"/> hides and shows them all; a status bar at the bottom.
/// A thin side bar on each edge holds a button per panel, its icon over its name (ADR-282): the left bar the left region's panels at
/// its top and the bottom region's at its foot, the right bar the right region's. A button shows its panel, or collapses the region when
/// its panel is the one showing; a region shows one panel at a time under a header with its name and a hide button. The map itself is drawn behind the workspace: the
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
        [EditorRegion.Left] = 320 - SideBarWidth, // the side bars take their width from the regions, so the map's room stays as it was
        [EditorRegion.Right] = -(560 - SideBarWidth),
        [EditorRegion.Bottom] = -200,
    };

    /// <summary>
    /// The most of the room a region takes when the layout is applied (at start, and when the window or the UI's scale changes, ADR-277), so
    /// the map keeps room on a small window or at a large scale; a region dragged wider stays so until then.
    /// </summary>
    private static readonly Dictionary<EditorRegion, float> MostOfRoom = new()
    {
        [EditorRegion.Left] = 0.3f,
        [EditorRegion.Right] = 0.35f,
        [EditorRegion.Bottom] = 0.4f,
    };

    private readonly Dictionary<EditorRegion, SplitContainer> _splitters = [];
    private readonly Dictionary<EditorRegion, TabContainer> _regions = [];
    private readonly Dictionary<EditorRegion, PanelContainer> _panels = [];
    private readonly Dictionary<EditorRegion, VBoxContainer> _footers = [];
    private readonly Dictionary<EditorRegion, bool> _open = [];
    private readonly Dictionary<EditorRegion, string> _savedTabs = [];
    private readonly Dictionary<Control, Control> _tabOf = [];

    /// <summary>A side bar's width: room for an icon over a short name.</summary>
    public const int SideBarWidth = 52;

    /// <summary>The size of a side bar button's icon.</summary>
    private const int SideIconSize = 20;

    private readonly Dictionary<EditorRegion, VBoxContainer> _sideGroups = [];
    private readonly Dictionary<Control, (EditorRegion Region, Button Button, Texture2D Icon)> _sideButtons = [];
    private readonly Dictionary<EditorRegion, (Control Header, TextureRect Icon, Label Title)> _headers = [];
    private EditorSettingsFile _settings = null!;
    private readonly List<Control> _pages = [];
    private readonly TabBar _pageTabs = new TabBar { Name = "Pages", ClipTabs = false, FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkEnd }
        .Tag("tools-map-and-template-tabs"); // F1 (ADR-276)
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

        // Side bar | Left | (map over Bottom) | Right | side bar. A region is the side of its splitter that does not expand, so the map takes
        // what is left.
        var frame = new HBoxContainer { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeConstantOverride("separation", 0);
        _body.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pages.Add(frame);
        frame.AddChild(SideBar("LeftBar", EditorRegion.Left, EditorRegion.Bottom));
        var outer = Splitter(new HSplitContainer { Name = "Outer", SizeFlagsHorizontal = SizeFlags.ExpandFill }, EditorRegion.Left);
        frame.AddChild(outer);
        outer.AddChild(Region(EditorRegion.Left));
        var inner = Splitter(new HSplitContainer { Name = "Inner", SizeFlagsHorizontal = SizeFlags.ExpandFill }, EditorRegion.Right);
        outer.AddChild(inner);
        var centre = Splitter(new VSplitContainer { Name = "Centre", SizeFlagsHorizontal = SizeFlags.ExpandFill }, EditorRegion.Bottom);
        inner.AddChild(centre);
        centre.AddChild(MapView);
        centre.AddChild(Region(EditorRegion.Bottom));
        inner.AddChild(Region(EditorRegion.Right));
        frame.AddChild(SideBar("RightBar", EditorRegion.Right, null));

        var status = Opaque(new PanelContainer { Name = "StatusPanel" });
        rows.AddChild(status);
        status.AddChild(StatusBar);
        ApplyVisibility();
        _body.Resized += FitRegions;
    }

    /// <summary>Each region at its kept size, or at its share of the room when that is less (<see cref="MostOfRoom"/>).</summary>
    private void FitRegions()
    {
        var room = _body.Size - new Vector2(2 * SideBarWidth, 0);
        if (room.X <= 0 || room.Y <= 0)
        {
            return;
        }

        foreach (var (region, splitter) in _splitters)
        {
            var kept = _settings.Get(Section, SettingKey(region, "offset"), DefaultOffsets[region]).AsInt32();
            var most = (int)((region == EditorRegion.Bottom ? room.Y : room.X) * MostOfRoom[region]);
            splitter.SplitOffsets = [kept < 0 ? -Math.Min(-kept, most) : Math.Min(kept, most)];
        }
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
            // The scroll bar's room is kept even while it is not needed: a panel whose height depends on its width (wrapping text, a
            // flow of buttons) would otherwise turn the bar on and off for ever at some widths, and the editor would hang.
            tab = new ScrollContainer
            {
                Name = panel.Name,
                VerticalScrollMode = ScrollContainer.ScrollMode.Reserve,
            };
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

        var picture = (icon is { } own ? UiIcons.Get(own, SideIconSize) : null) ?? PlaceholderIcons.Of(panel.Name.ToString(), SideIconSize);
        var button = SideButton(panel.Name.ToString(), picture);
        button.Pressed += () => Toggle(region, tab);
        _sideGroups[region].AddChild(button);
        _sideButtons[tab] = (region, button, picture);

        // The header stands where the tab bar stood, as tall, so what is under it keeps its place.
        _headers[region].Header.CustomMinimumSize = new Vector2(0, tabs.GetTabBar().GetCombinedMinimumSize().Y);
        _addingTab = false;
        Refresh();
    }

    /// <summary>
    /// A side bar: the buttons of <paramref name="top"/>'s panels from its top, of <paramref name="foot"/>'s (if any) from its foot.
    /// </summary>
    private PanelContainer SideBar(string name, EditorRegion top, EditorRegion? foot)
    {
        var bar = new PanelContainer { Name = name, CustomMinimumSize = new Vector2(SideBarWidth, 0) };
        bar.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = UiTheme.SideBarBackground, ContentMarginTop = 4, ContentMarginBottom = 4 });
        var column = new VBoxContainer();
        bar.AddChild(column);
        _sideGroups[top] = Group(column);
        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        if (foot is { } bottom)
        {
            _sideGroups[bottom] = Group(column);
        }

        return bar;

        static VBoxContainer Group(VBoxContainer column)
        {
            var group = new VBoxContainer();
            group.AddThemeConstantOverride("separation", 2);
            column.AddChild(group);
            return group;
        }
    }

    /// <summary>A side bar's button: the icon over the name, lit while its panel shows.</summary>
    private static Button SideButton(string title, Texture2D icon)
    {
        var button = new Button
        {
            Name = title + "Button",
            Text = title,
            Icon = icon,
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Top,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimChar,
            CustomMinimumSize = new Vector2(SideBarWidth, 44),
            TooltipText = $"{title}: show it, or hide it when it shows",
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeConstantOverride("h_separation", 1);
        var clear = new StyleBoxFlat { BgColor = Colors.Transparent, ContentMarginTop = 3, ContentMarginBottom = 2, ContentMarginLeft = 1, ContentMarginRight = 1 };
        var hover = (StyleBoxFlat)clear.Duplicate();
        hover.BgColor = new Color(1, 1, 1, 0.08f);
        hover.SetCornerRadiusAll(3);
        var lit = (StyleBoxFlat)clear.Duplicate();
        lit.BgColor = new Color(1, 1, 1, 0.22f);
        lit.SetCornerRadiusAll(3);
        button.AddThemeStyleboxOverride("normal", clear);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", lit);
        button.AddThemeStyleboxOverride("hover_pressed", lit);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return button;
    }

    /// <summary>A side bar button pressed: shows <paramref name="tab"/>, or collapses its region when it is the panel showing.</summary>
    private void Toggle(EditorRegion region, Control tab)
    {
        var tabs = _regions[region];
        if (IsShown(region) && tabs.GetCurrentTabControl() == tab)
        {
            SetOpen(region, false);
            return;
        }

        tabs.CurrentTab = tab.GetIndex();
        SetOpen(region, true);
    }

    /// <summary>The side bar buttons lit for the panels that show, and each region's header naming its panel.</summary>
    private void Refresh()
    {
        foreach (var (tab, (region, button, _)) in _sideButtons)
        {
            button.SetPressedNoSignal(_panels[region].Visible && _regions[region].GetCurrentTabControl() == tab);
        }

        foreach (var (region, (_, icon, title)) in _headers)
        {
            var current = _regions[region].GetCurrentTabControl();
            title.Text = current?.Name.ToString() ?? "";
            icon.Texture = current is not null && _sideButtons.TryGetValue(current, out var side) ? side.Icon : null;
        }
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
        _splitters[region] = splitter;
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
        column.AddThemeConstantOverride("separation", 0);
        panel.AddChild(column);

        // One panel at a time under its header (the side bars pick it), so no tab bar.
        var header = new HBoxContainer { Name = "Header" };
        var icon = new TextureRect { StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(24, 0) };
        var title = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        var hide = new Button
        {
            Text = "Hide", Flat = true, FocusMode = FocusModeEnum.None,
            TooltipText = "Hide this region; its panels' buttons on the side bar show it again",
        }.WithIcon(UiIcon.Minus, UiIcons.ButtonSize);
        hide.Pressed += () => SetOpen(region, false);
        header.AddChild(icon);
        header.AddChild(title);
        header.AddChild(hide);
        column.AddChild(header);
        _headers[region] = (header, icon, title);
        var tabs = new TabContainer { Name = "Tabs", SizeFlagsVertical = SizeFlags.ExpandFill, TabsVisible = false };
        column.AddChild(tabs);
        var footer = new VBoxContainer { Name = "Footer" };
        column.AddChild(footer);
        _footers[region] = footer;
        _panels[region] = panel;
        tabs.TabChanged += index =>
        {
            Refresh();
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

        Refresh();

        RegionsChanged?.Invoke();
    }

    /// <summary>A docked panel hides the map behind it (the theme's panels over the map let it show through).</summary>
    private static PanelContainer Opaque(PanelContainer panel)
    {
        panel.ThemeTypeVariation = UiTheme.RegionPanel;
        return panel;
    }

    private static string SettingKey(EditorRegion region, string what) => $"{region.ToString().ToLowerInvariant()}_{what}";
}
