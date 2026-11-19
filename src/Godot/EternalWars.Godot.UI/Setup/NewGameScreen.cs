using EternalWars.Godot.Shared.Controls;
using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// A map the player can start: a name, a factory building its initial state for the chosen options, the tab it is listed on (ADR-201;
/// <see cref="MapKinds.Of"/> for a map file) and its size, for sorting and filtering the list (ADR-204; 0 when unknown: a file that does not load).
/// </summary>
public sealed record ScenarioChoice(string Name, Func<GameOptions, GameState> Create, MapKind Kind = MapKind.Scenario, int Width = 0, int Height = 0);

/// <summary>
/// A start slot of a random map template: its player's name, whether a human may (and by default does) take it, its default team and whether its
/// player picks the start town's tier-1 dwelling (the start zone gives none).
/// </summary>
public sealed record RandomMapSlot(string Name, bool HumanAllowed, bool HumanByDefault, int Team, bool PicksTier1Dwelling = true);

/// <summary>
/// One player of a random map as set up: the slot, human or computer, the town (null = random) and hero (null = random), the team (null = the
/// template's, when locked) and the start town's tier-1 dwelling (a base creature id; null = random).
/// </summary>
public sealed record RandomMapPlayer(int Slot, bool IsHuman, string? Town, string? Hero, int? Team, string? Tier1Dwelling = null);

/// <summary>
/// What the screen asks the generator for: the map size, the players (the first slots, in order), the game options (their seed is the map's) and
/// the hero limit (null = the template's, ADR-068).
/// </summary>
public sealed record RandomMapRequest(int Size, IReadOnlyList<RandomMapPlayer> Players, GameOptions Options, int? MaxHeroes = null);

/// <summary>A generated game and the generator's problems when every attempt had some (the player may still play it).</summary>
public sealed record RandomMapResult(GameState State, IReadOnlyList<string> Problems);

/// <summary>
/// A random map template the player can start (ADR-086): its sizes, the player count range, the start slots, a factory that generates the game
/// and its hero limit. Refused setups (too many humans, a hero picked twice, ...) throw <see cref="ArgumentException"/>, which the screen shows.
/// </summary>
public sealed record RandomMapChoice(
    string Name,
    string Description,
    IReadOnlyList<int> Sizes,
    int MinPlayers,
    bool TeamsLocked,
    IReadOnlyList<RandomMapSlot> Slots,
    Func<RandomMapRequest, RandomMapResult> Create,
    int? MinTeams = null,
    int? MaxTeams = null,
    int MaxHeroes = ScenarioRules.HeroLimit);

/// <summary>
/// New-game setup (ADR-201: four tabs, Scenarios, Random maps, Arenas and Campaign, each a list of its maps on the left, sorted and filtered by
/// size as the player chose (ADR-204), and the chosen one's details and players on the right, the game options under them (ADR-225): fog of war
/// (Permanent reveal or Refog) with the hero limit beside it, the difficulty as five chess-piece buttons, the turn timer as a checkbox with its
/// minutes, and the seed in debug builds only (a release build draws one for every game). Each player has a colour (a swatch: picking another
/// player's colour swaps the two). For a map: its players, with teams unless the map locks them (ADR-032), the town as a crest where the map
/// leaves it open; on an arena map a line on the mode and, per player, Human or Computer (ADR-159); the map's own difficulty is preselected. For a
/// template (ADR-086): the size, the hero limit (the template's by default, ADR-068) and the players, "+" adding one and each row's "−" removing it
/// within the template's range, each human or computer, town (a crest), hero, the start town's tier-1 dwelling (a human player with a picked
/// town, when the start zone gives none; RMG-7) and team (unless the template locks them); the map is generated on "Start" and, if it still has
/// problems, shown so the player can start it anyway or change something. The "×" in the top-right corner (or Escape) closes the screen. Raises <see cref="Started"/> with the ready state; the app then builds the session and the world. As a LAN host's lobby
/// (<see cref="Lan"/>, ADR-194) each colour a person may play is played here, by a LAN player or by the computer, and the chat sits beside it.
/// </summary>
public partial class NewGameScreen : PanelContainer
{
    private const string Random = "Random";

    private const int LabelWidth = 120;

    /// <summary>The turn timer's minutes when it is first ticked.</summary>
    private const int FirstTimerMinutes = 5;

    /// <summary>The seed is shown, and may be typed, only in a debug build (ADR-225); a release build draws one for every game.</summary>
    private static bool ShowsSeed => OS.IsDebugBuild();

    private readonly Dictionary<MapTab, List<MapEntry>> _entries = Enum.GetValues<MapTab>().ToDictionary(t => t, _ => new List<MapEntry>());
    private readonly Dictionary<MapTab, Button> _tabButtons = [];
    private readonly Dictionary<MapTab, MapEntry> _lastSelected = [];
    private readonly VBoxContainer _list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _title = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _summary = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.75f) };
    private readonly Label _description = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _playersHeading = new() { Text = "Players" };
    private readonly HSeparator _playersRule = new();
    private readonly OptionButton _size = new();
    private readonly SpinBox _maxHeroes = new() { MinValue = 1, MaxValue = ScenarioRules.HeroLimit, Step = 1 };
    private readonly HBoxContainer _heroLimit = new();
    private readonly OptionButton _fog = new();
    private readonly DifficultyRow _difficulty = new();
    private readonly CheckBox _timerOn = new() { Text = "Turn timer", FocusMode = FocusModeEnum.None };
    private readonly SpinBox _timerMinutes = new() { MinValue = 1, MaxValue = TurnTimer.MaxMinutes, Step = 1, Value = FirstTimerMinutes, Suffix = "min", Visible = false };
    private readonly LineEdit _seed = new() { CustomMinimumSize = new Vector2(220, 0) };
    private readonly GridContainer _players = new() { Columns = 3 };
    private readonly Button _addPlayer = new Button { Text = "Add a player", FocusMode = FocusModeEnum.None }.WithIcon(UiIcon.Plus);
    private readonly Dictionary<PlayerId, ColourPicker> _colours = [];
    private readonly Dictionary<PlayerId, (Label Label, string Name)> _names = [];
    private int _randomCount;
    private readonly Button _start = new Button { Text = "Start" }.WithIcon(UiIcon.Confirm, keepText: true);
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _arena = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
    private readonly Dictionary<PlayerId, SpinBox> _teams = [];
    private readonly Dictionary<PlayerId, OptionButton> _controllers = [];
    private readonly Dictionary<int, CrestPicker> _startTowns = [];
    private readonly List<RandomPlayerRow> _randomPlayers = [];
    private readonly List<Control> _randomRows = [];
    private Func<string, IReadOnlyList<HeroDefinition>> _heroesOf = _ => [];
    private Func<string, IReadOnlyList<CreatureDefinition>> _tier1Of = _ => [];
    private readonly Button _sortByName = new() { ToggleMode = true, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Button _sortBySize = new() { ToggleMode = true, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly OptionButton _sizeFilter = new() { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private MapListView _view = MapListView.Default;
    private RandomMapResult? _generated;
    private MapTab _tab;
    private MapEntry? _selected;
    private bool _busy;

    /// <summary>The screen's tabs, in their order: the hand-made maps by <see cref="MapKind"/>, and the random map templates.</summary>
    private enum MapTab
    {
        Scenarios,
        RandomMaps,
        Arenas,
        Campaign,
    }

    public event Action<GameState>? Started;

    /// <summary>The player closed the screen, back to the main menu (the "×", or Escape, which the app maps).</summary>
    public event Action? BackRequested;

    /// <summary>
    /// The player sorted or filtered the map lists (ADR-204): the app keeps the view in the settings file and hands it to <see cref="Bind"/> the
    /// next time.
    /// </summary>
    public event Action<MapListView>? MapListChanged;

    /// <summary>
    /// Binds the maps, the random map templates, the heroes a player of a town can pick and the tier-1 dwellings its start town can begin with
    /// (for the templates' hero and dwelling choices), and how the map lists are sorted and filtered (ADR-204; by name, every size, when null).
    /// </summary>
    public void Bind(
        IReadOnlyList<ScenarioChoice> choices,
        IReadOnlyList<RandomMapChoice>? templates = null,
        Func<string, IReadOnlyList<HeroDefinition>>? heroesOf = null,
        Func<string, IReadOnlyList<CreatureDefinition>>? tier1Of = null,
        MapListView? mapList = null)
    {
        _heroesOf = heroesOf ?? (_ => []);
        _tier1Of = tier1Of ?? (_ => []);
        _view = mapList ?? MapListView.Default;
        foreach (var choice in choices)
        {
            var size = MapSizeClass.Of(choice.Width, choice.Height);
            _entries[TabOf(choice.Kind)].Add(new MapEntry(choice.Name, choice, null, size is { } s ? [s] : []));
        }

        foreach (var template in templates ?? [])
        {
            _entries[MapTab.RandomMaps].Add(new MapEntry(template.Name, null, template, MapSizeClass.OfTemplate(template.Sizes)));
        }

        this.UseMenuPanel();
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(Lan is null ? 1240 : 1120, 0) };
        box.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(Lan is null ? box : WithChat(box));
        var heading = new Label { Text = Lan is null ? "New game" : "LAN game: you host", HorizontalAlignment = HorizontalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 26);
        box.AddChild(HeadingRow(heading));
        if (Lan is not null)
        {
            AddLanRows(box);
        }

        box.AddChild(Tabs());
        box.AddChild(MapArea());
        box.AddChild(new HSeparator());
        box.AddChild(Options());
        box.AddChild(new HSeparator());

        _start.Pressed += Start;
        box.AddChild(Screens.ButtonRow(_start));
        _start.CallDeferred(Control.MethodName.GrabFocus); // Enter starts (and the smoke scripts need no coordinates)
        box.AddChild(_status);

        // The screen opens on the first map's tab (a map given with --map comes first, and is chosen if the filter shows it), else on the first
        // tab that has maps.
        var first = choices.Count > 0 ? TabOf(choices[0].Kind) : Enum.GetValues<MapTab>().FirstOrDefault(t => _entries[t].Count > 0);
        if (choices.Count > 0)
        {
            _lastSelected[first] = _entries[first][0];
        }

        ShowTab(first);
    }

    /// <summary>The heading in the middle, the "×" that closes the screen in the top-right corner (ADR-225).</summary>
    private HBoxContainer HeadingRow(Label heading)
    {
        var row = new HBoxContainer();
        var close = new Button { Text = "Close (Esc)" }.WithIcon(UiIcon.Close);
        close.Pressed += () => BackRequested?.Invoke();
        row.AddChild(new Control { CustomMinimumSize = close.CustomMinimumSize, MouseFilter = MouseFilterEnum.Ignore }); // keeps the heading centred
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(heading);
        row.AddChild(close);
        return row;
    }

    /// <summary>One toggle button a tab, with its map count; an empty tab is still shown (it says how to get its maps).</summary>
    private HBoxContainer Tabs()
    {
        var row = new HBoxContainer();
        var group = new ButtonGroup();
        foreach (var tab in Enum.GetValues<MapTab>())
        {
            var button = new Button
            {
                ToggleMode = true,
                ButtonGroup = group,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None,
                TooltipText = TabTip(tab),
            };
            button.Pressed += () => ShowTab(tab);
            row.AddChild(button);
            _tabButtons[tab] = button;
        }

        ShowTabCounts();
        return row;
    }

    /// <summary>Each tab's label with the number of its maps the size filter shows.</summary>
    private void ShowTabCounts()
    {
        foreach (var (tab, button) in _tabButtons)
        {
            button.Text = $"{TabName(tab)} ({Shown(tab).Count})";
        }
    }

    /// <summary>A tab's maps that pass the size filter, in the chosen order (ADR-204).</summary>
    private IReadOnlyList<MapEntry> Shown(MapTab tab) => _view.Arrange(_entries[tab], e => e.Name, e => e.Sizes);

    /// <summary>
    /// The sort buttons and the size filter above the list (ADR-204): pressing the active sort again reverses it. The same for every tab; the app
    /// keeps them.
    /// </summary>
    private VBoxContainer ListControls()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Row);
        var sort = new HBoxContainer();
        sort.AddThemeConstantOverride("separation", UiSpacing.Gap);
        sort.AddChild(new Label { Text = "Sort" });
        sort.AddChild(_sortByName);
        sort.AddChild(_sortBySize);
        _sortByName.Pressed += () => ChangeView(_view.Pressed(MapSort.Name));
        _sortBySize.Pressed += () => ChangeView(_view.Pressed(MapSort.Size));
        box.AddChild(sort);

        _sizeFilter.AddItem("All sizes", MapListView.AllSizes);
        foreach (var size in Core.Domain.Map.MapSizes.Standard)
        {
            _sizeFilter.AddItem($"{size}×{size}", size);
        }

        _sizeFilter.TooltipText = "Show the maps of one size: a map counts as the smallest size that holds its larger side; a random map template "
                                  + "is listed under every size it supports.";
        _sizeFilter.ItemSelected += index => ChangeView(_view.Filtered(_sizeFilter.GetItemId((int)index)));
        var filter = new HBoxContainer();
        filter.AddThemeConstantOverride("separation", UiSpacing.Gap);
        filter.AddChild(new Label { Text = "Size" });
        filter.AddChild(_sizeFilter);
        box.AddChild(filter);
        ShowListControls();
        return box;
    }

    /// <summary>The sort buttons and the size filter as the view is: the active sort pressed, with its direction.</summary>
    private void ShowListControls()
    {
        var byName = _view.Sort == MapSort.Name;
        _sortByName.SetPressedNoSignal(byName);
        _sortBySize.SetPressedNoSignal(!byName);
        _sortByName.Text = !byName ? "Name" : _view.Reversed ? "Name Z–A" : "Name A–Z";
        _sortBySize.Text = byName ? "Size" : _view.Reversed ? "Large first" : "Small first";
        _sortByName.TooltipText = byName ? "Sorted by name; press again to reverse." : "Sort by name, A–Z.";
        _sortBySize.TooltipText = !byName ? "Sorted by size, then by name; press again to reverse." : "Sort by size, small first, then by name.";
        _sizeFilter.Select(_sizeFilter.GetItemIndex(_view.Size));
    }

    /// <summary>The player sorted or filtered: every tab's list follows (a chosen map the filter hides gives way to the first listed), the app keeps it.</summary>
    private void ChangeView(MapListView view)
    {
        _view = view;
        ShowListControls();
        ShowTabCounts();
        ShowTab(_tab);
        MapListChanged?.Invoke(view);
    }

    /// <summary>The tab's maps on the left; the chosen map's name, size, description, random map settings and players on the right.</summary>
    private HBoxContainer MapArea()
    {
        var area = new HBoxContainer { CustomMinimumSize = new Vector2(0, 430) };
        area.AddThemeConstantOverride("separation", UiSpacing.Section);
        var listColumn = new VBoxContainer { CustomMinimumSize = new Vector2(Lan is null ? 300 : 200, 0) }; // the lobby's chat takes the room
        listColumn.AddThemeConstantOverride("separation", UiSpacing.Row);
        listColumn.AddChild(ListControls());
        var listScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        listScroll.AddChild(_list);
        listColumn.AddChild(listScroll);
        area.AddChild(listColumn);
        area.AddChild(new VSeparator());

        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", UiSpacing.Row);
        _title.AddThemeFontSizeOverride("font_size", 22);
        details.AddChild(_title);
        details.AddChild(_summary);
        details.AddChild(_arena);
        details.AddChild(_description);

        var settings = new HBoxContainer();
        settings.AddThemeConstantOverride("separation", UiSpacing.Section);
        settings.AddRow("Size", _size);
        _size.ItemSelected += _ => Changed();
        details.AddChild(settings);
        _randomRows.Add(settings);

        details.AddChild(_playersRule);
        details.AddChild(_playersHeading);
        var playersScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _players.AddThemeConstantOverride("h_separation", Lan is null ? UiSpacing.Section : UiSpacing.Gap);
        _players.AddThemeConstantOverride("v_separation", UiSpacing.Row);
        var playersList = new VBoxContainer();
        playersList.AddThemeConstantOverride("separation", UiSpacing.Row);
        playersList.AddChild(_players);
        _addPlayer.TooltipText = "Another player, up to the template's most.";
        _addPlayer.Pressed += () => SetRandomCount(_randomCount + 1);
        playersList.AddChild(_addPlayer);
        playersScroll.AddChild(playersList);
        details.AddChild(playersScroll);
        area.AddChild(details);
        return area;
    }

    /// <summary>
    /// The game options (ADR-225), in two columns: fog of war with the hero limit beside it (a random map's), the difficulty; the turn timer, and
    /// the seed in a debug build.
    /// </summary>
    private GridContainer Options()
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", UiSpacing.Section * 3);
        grid.AddThemeConstantOverride("v_separation", UiSpacing.Row);

        _fog.AddItem("Permanent reveal", (int)FogMode.PermanentReveal);
        _fog.SetItemTooltip(0, "HoMM3's fog: once explored, the land and everything on it stays in view.");
        _fog.AddItem("Refog", (int)FogMode.ShroudWithRefog);
        _fog.SetItemTooltip(1, "Explored land stays in view, but other players' heroes are seen only while one of yours is near.");
        _fog.TooltipText = "Permanent reveal: once explored, everything stays in view. Refog: other players' heroes only while yours are near.";
        _fog.ItemSelected += _ => Changed();
        var fogRow = grid.AddRow("Fog of war", _fog, LabelWidth);
        fogRow.AddThemeConstantOverride("separation", UiSpacing.Section);
        _maxHeroes.TooltipText = "The most heroes a player may have on the map at once; the template sets the default.";
        _maxHeroes.ValueChanged += _ => Changed();
        _heroLimit.AddRow("Hero limit", _maxHeroes);
        _heroLimit.GetChild<HBoxContainer>(0).AddChild(new Label { Text = "heroes per player" });
        fogRow.AddChild(_heroLimit);
        _randomRows.Add(_heroLimit);

        _difficulty.Changed += Changed;
        grid.AddRow("Difficulty", _difficulty, LabelWidth);

        // The turn timer (ADR-091): ticked, a human player's turn lasts the minutes beside it.
        _timerOn.TooltipText = "A human player's turn lasts the minutes set beside it; then it ends.";
        _timerMinutes.SelectAllOnFocus = true;
        _timerOn.Toggled += on =>
        {
            _timerMinutes.Visible = on;
            if (on)
            {
                _timerMinutes.GetLineEdit().CallDeferred(Control.MethodName.GrabFocus); // ready to type the minutes
            }

            Changed();
        };
        _timerMinutes.TooltipText = $"Minutes a turn lasts, 1 to {TurnTimer.MaxMinutes}.";
        _timerMinutes.ValueChanged += _ => Changed();
        var timerRow = new HBoxContainer();
        timerRow.AddThemeConstantOverride("separation", UiSpacing.Gap);
        timerRow.AddChild(_timerOn);
        timerRow.AddChild(_timerMinutes);
        grid.AddRow("", timerRow, LabelWidth);

        _seed.Text = NewSeed();
        if (ShowsSeed)
        {
            _seed.TextChanged += _ => Changed();
            var seedRow = grid.AddRow("Seed", _seed, LabelWidth);
            var draw = new Button { Text = "New seed", FocusMode = FocusModeEnum.None, TooltipText = "Draw another seed." };
            draw.Pressed += () =>
            {
                _seed.Text = NewSeed(); // setting the text from code raises no TextChanged
                Changed();
            };
            seedRow.AddChild(draw);
        }

        return grid;
    }

    /// <summary>A new seed; the one given with <c>--seed=</c> instead, so a scripted run plays the same game every time (ADR-235).</summary>
    private static string NewSeed() =>
        (Shared.LaunchArguments.Seed ?? (ulong)System.Random.Shared.NextInt64()).ToString(CultureInfo.InvariantCulture);

    private static MapTab TabOf(MapKind kind) => kind switch
    {
        MapKind.Arena => MapTab.Arenas,
        MapKind.Campaign => MapTab.Campaign,
        _ => MapTab.Scenarios,
    };

    private static string TabName(MapTab tab) => tab == MapTab.RandomMaps ? "Random maps" : tab.ToString();

    private static string TabTip(MapTab tab) => tab switch
    {
        MapTab.Scenarios => "Hand-made maps played to win: the shipped ones and those saved in the map editor.",
        MapTab.RandomMaps => "A map generated from a template for the players you set up.",
        MapTab.Arenas => "A day to prepare, then one battle decides the game. Not ranked.",
        _ => "Story maps: a person starts without a town, a map event favours people, or the map carries campaign variables. Not ranked.",
    };

    /// <summary>What an empty tab says in place of a map.</summary>
    private static string EmptyTab(MapTab tab) => tab switch
    {
        MapTab.Campaign =>
            "No campaign-like maps yet. A map saved in the map editor is listed here when a person starts without a town, a map event "
            + "gives something only to people, or the map keeps or takes campaign variables.",
        MapTab.Arenas => "No arena maps. Make one in the map editor (Conditions, Arena).",
        MapTab.RandomMaps => "No random map templates.",
        _ => "No maps. Maps saved in the map editor are listed here.",
    };

    /// <summary>
    /// Shows a tab: its maps that pass the size filter as a list in the chosen order, the one last chosen on it (else, or when the filter hides it,
    /// the first listed) chosen again. The map already shown is kept as set up (a new order or filter does not reset its players).
    /// </summary>
    private void ShowTab(MapTab tab)
    {
        _tab = tab;
        _tabButtons[tab].SetPressedNoSignal(true);
        _list.ClearChildren(detach: true);
        var group = new ButtonGroup();
        var entries = Shown(tab);
        var chosen = _lastSelected.GetValueOrDefault(tab) is { } last && entries.Contains(last) ? last : entries.FirstOrDefault();
        foreach (var entry in entries)
        {
            var button = new Button
            {
                Text = entry.Name,
                ToggleMode = true,
                ButtonGroup = group,
                Alignment = HorizontalAlignment.Left,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None,
                TooltipText = entry.Name,
                ButtonPressed = entry == chosen,
            };
            button.Pressed += () => Select(entry);
            _list.AddChild(button);
        }

        if (chosen is not null)
        {
            if (chosen != _selected)
            {
                Select(chosen);
            }

            return;
        }

        _selected = null;
        _title.Text = TabName(tab);
        _summary.Text = "";
        _description.Text = _entries[tab].Count == 0
            ? EmptyTab(tab)
            : $"No map on this tab is {_view.Size}×{_view.Size}. Choose another size, or All sizes.";
        _description.Visible = true;
        _arena.Visible = false;
        _playersHeading.Visible = _playersRule.Visible = _summary.Visible = false;
        _start.Disabled = true;
        foreach (var row in _randomRows)
        {
            row.Visible = false;
        }

        _status.Text = "";
        _addPlayer.Visible = false;
        Changed();
        ClearPlayers();
    }

    private void Select(MapEntry entry)
    {
        _selected = entry;
        _lastSelected[_tab] = entry;
        _title.Text = entry.Name;
        _playersHeading.Visible = _playersRule.Visible = _summary.Visible = true;
        _start.Disabled = false;
        ShowPlayers();
    }

    /// <summary>What an arena map is, under the map's name (ADR-097).</summary>
    public const string ArenaLine =
        "Arena: day 1 is preparation (heroes move nearly without limit, towns build as much as they can pay). At the start of day 2 each side's "
        + "strongest hero meets at the combat place, and the winner of that one battle wins the game. Not ranked.";

    private FogMode SelectedFog => (FogMode)_fog.GetSelectedId();

    private ScenarioChoice? SelectedMap => _selected?.Map;

    private RandomMapChoice? SelectedTemplate => _selected?.Template;

    private void ClearPlayers()
    {
        _players.ClearChildren(detach: true);

        _teams.Clear();
        _controllers.Clear();
        _seatNames.Clear();
        _seatTowns.Clear();
        _lanPlayers.Clear();
        _zoneOwners = null;
        _startTowns.Clear();
        _randomPlayers.Clear();
        _colours.Clear();
        _names.Clear();
    }

    /// <summary>Shows the selected map's player slots (team boxes disabled when the map locks its teams), or the selected template's setup.</summary>
    private void ShowPlayers()
    {
        _status.Text = "";
        _arena.Visible = false;
        var template = SelectedTemplate;
        foreach (var row in _randomRows)
        {
            row.Visible = template is not null;
        }

        if (template is not null)
        {
            _description.Text = template.Description;
            _description.Visible = template.Description.Length > 0;
            var players = template.MinPlayers == template.Slots.Count ? $"{template.MinPlayers}" : $"{template.MinPlayers} to {template.Slots.Count}";
            _summary.Text = $"Random map · {players} players · sizes "
                            + string.Join(", ", template.Sizes.Select(size => $"{size}×{size}")) + TeamCounts(template.MinTeams, template.MaxTeams);
            _size.Clear();
            foreach (var size in template.Sizes)
            {
                _size.AddItem($"{size}×{size}", size);
            }

            _size.Select(template.Sizes.Count / 2);
            _randomCount = template.Slots.Count;
            _maxHeroes.SetValueNoSignal(template.MaxHeroes);
            _difficulty.Show(Difficulty.Normal);
            ShowRandomPlayers([]);
            return;
        }

        _addPlayer.Visible = false;
        Changed();
        ClearPlayers();
        _players.Columns = Lan is null ? 4 : 5;
        if (SelectedMap is not { } map)
        {
            return;
        }

        GameState preview;
        try
        {
            preview = map.Create(GameOptions.Default);
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            _status.Text = $"Cannot load {map.Name}: {e.Message}";
            return;
        }

        var levels = preview.Map.Levels > 1 ? $", {preview.Map.Levels} levels" : "";
        var people = preview.Players.Count(p => p.IsHuman);
        _summary.Text = $"{preview.Map.Width}×{preview.Map.Height}{levels} · {preview.Players.Count} players, {people} played by people"
                        + TeamCounts(preview.MinTeams, preview.MaxTeams);
        _description.Text = preview.Map.Description;
        _description.Visible = preview.Map.Description.Length > 0;
        _difficulty.Show(preview.Rules.Difficulty ?? Difficulty.Normal); // the map's own difficulty, preselected

        // An arena map (ADR-097, ADR-159): its mode said in one line, and each player played by a person (hot-seat) or the computer.
        _zoneOwners = [.. preview.Map.StartZoneOwners];
        _arena.Visible = preview.Rules.Arena;
        _arena.Text = ArenaLine;
        _players.AddChild(new Label { Text = "Player" });
        _players.AddChild(new Label { Text = "Controlled by" });
        AddLanHeader();
        _players.AddChild(new Label { Text = preview.TeamsLocked ? "Team (locked by the map)" : "Team" });
        _players.AddChild(new Label { Text = "Town" });
        var firstHuman = preview.Players.FirstOrDefault(p => p.IsHuman)?.Id;
        foreach (var player in preview.Players)
        {
            _players.AddChild(PlayerCell(player.Id, player.Name, player.Colour));
            _seatNames.Add((player.Id, player.Name));
            if (Lan is not null && (player.IsHuman || preview.Rules.Arena))
            {
                // Every player of an arena map may be a person: its other colours wait for LAN players (ADR-199).
                var controller = LanController(player.Id == firstHuman ? LanHere : player.IsHuman || preview.Rules.Arena ? LanRemote : Computer, humanAllowed: true);
                _players.AddChild(controller);
                _controllers[player.Id] = controller;
            }
            else if (preview.Rules.Arena)
            {
                var controller = new OptionButton();
                controller.AddItem("Human", 1);
                controller.AddItem("Computer", 0);
                controller.Select(controller.GetItemIndex(player.IsHuman ? 1 : 0));
                _players.AddChild(controller);
                _controllers[player.Id] = controller;
            }
            else
            {
                _players.AddChild(new Label { Text = player.IsHuman ? "Human" : "Computer" });
            }

            AddLanPlayer(player.Id);
            var team = new SpinBox { MinValue = 0, MaxValue = GameState.MaxPlayers - 1, Value = player.Team.Value, Editable = !preview.TeamsLocked };
            _players.AddChild(team);
            _teams[player.Id] = team;
            var town = TownChoice(preview, player.Id);
            _players.AddChild(town);
            _seatTowns[player.Id] = town;
        }
    }

    /// <summary>
    /// The player's faction (ADR-126): a choice when the map leaves it open (a random town of the player's, or a hero placeholder without a fixed
    /// town), else the type of its first town, or a dash.
    /// </summary>
    private Control TownChoice(GameState preview, PlayerId player)
    {
        var towns = preview.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList();
        var open = towns.Any(t => t.IsRandom) || (towns.Count == 0 && preview.HeroesOf(player).Any(h => h.IsPlaceholder));
        if (!open)
        {
            return towns.FirstOrDefault()?.TownType is { } type ? Heraldry.Titled(type, type) : new Label { Text = "—" };
        }

        var choice = new CrestPicker();
        choice.Changed += Changed;
        _startTowns[player.Value] = choice;
        return choice;
    }

    /// <summary>The factions chosen for the map's players (ADR-126); null when nobody chose one.</summary>
    private IReadOnlyDictionary<int, string>? ChosenTowns()
    {
        var chosen = _startTowns.Where(c => c.Value.Town is not null).ToDictionary(c => c.Key, c => c.Value.Town!);
        return chosen.Count == 0 ? null : chosen;
    }

    /// <summary>The colours chosen for the players (ADR-225), by player number; null when every player keeps its own.</summary>
    private IReadOnlyDictionary<int, int>? ChosenColours()
    {
        var chosen = _colours.Where(c => c.Value.Colour != c.Key.Value % Core.Domain.Players.Player.ColourCount)
            .ToDictionary(c => c.Key.Value, c => c.Value.Colour);
        return chosen.Count == 0 ? null : chosen;
    }

    /// <summary>The team counts a map or template allows (ADR-117), for the summary line: " · 2 teams", " · 2 to 4 teams"; empty without a limit.</summary>
    private static string TeamCounts(int? min, int? max) => (min, max) switch
    {
        (null, null) => "",
        ({ } a, { } b) when a == b => $" · {a} teams",
        ({ } a, null) => $" · at least {a} teams",
        (null, { } b) => $" · at most {b} teams",
        ({ } a, { } b) => $" · {a} to {b} teams",
    };

    /// <summary>A player's colour swatch and name; picking another player's colour swaps the two (ADR-225).</summary>
    private HBoxContainer PlayerCell(PlayerId id, string name, int colour)
    {
        var cell = new HBoxContainer();
        cell.AddThemeConstantOverride("separation", UiSpacing.Gap);
        var picker = new ColourPicker(colour);
        picker.Changed += picked => SwapColour(id, picked);
        _colours[id] = picker;
        cell.AddChild(picker);
        var label = new Label { VerticalAlignment = VerticalAlignment.Center };
        _names[id] = (label, name);
        cell.AddChild(label);
        ShowName(id);
        return cell;
    }

    /// <summary>Gives <paramref name="id"/> the colour <paramref name="colour"/>; the player that had it takes <paramref name="id"/>'s.</summary>
    private void SwapColour(PlayerId id, int colour)
    {
        var mine = _colours[id].Colour;
        foreach (var (other, picker) in _colours)
        {
            if (other != id && picker.Colour == colour)
            {
                picker.Colour = mine;
                ShowName(other);
            }
        }

        _colours[id].Colour = colour;
        ShowName(id);
        Changed();
    }

    /// <summary>
    /// The player's name in its colour; a player named after its own colour takes the chosen one's name, as the game will call it
    /// (<see cref="Core.Domain.Players.Player.Colour"/>).
    /// </summary>
    private void ShowName(PlayerId id)
    {
        var (label, name) = _names[id];
        var colour = _colours[id].Colour;
        var names = Core.Domain.Players.Player.ColourNames;
        var shown = name == names[id.Value % names.Count] ? names[colour] : name;
        label.Text = shown;
        label.AddThemeColorOverride("font_color", Palette.ForColour(colour).Lightened(0.3f));
        var seat = _seatNames.FindIndex(s => s.Id == id);
        if (seat >= 0)
        {
            _seatNames[seat] = (id, shown);
        }
    }

    /// <summary>
    /// A random map's players: <paramref name="count"/> of the template's slots within its range ("+" and each row's "−", ADR-225), the choices
    /// made so far kept by the players that stay.
    /// </summary>
    private void SetRandomCount(int count) => ShowRandomPlayers(_randomPlayers.Select(r => r.Choice(_colours[new PlayerId(r.Slot)].Colour)).Take(count).ToList(), count);

    /// <summary>Removes the player in <paramref name="slot"/>: the players after it move up one slot, with their choices.</summary>
    private void RemoveRandomPlayer(int slot)
    {
        var kept = _randomPlayers.Where(r => r.Slot != slot).Select(r => r.Choice(_colours[new PlayerId(r.Slot)].Colour)).ToList();
        ShowRandomPlayers(kept, kept.Count);
    }

    /// <summary>
    /// The template's first slots, one row each: colour, human or computer, town, hero (of that town), tier-1 dwelling (of that town), team, and
    /// "−" while there are more than the template's fewest. Row i takes <paramref name="choices"/>[i] when given (a player that stays), else the
    /// slot's defaults and the first colour nobody has.
    /// </summary>
    private void ShowRandomPlayers(IReadOnlyList<RandomPlayerChoice> choices, int? count = null)
    {
        Changed();
        ClearPlayers();
        if (SelectedTemplate is not { } template)
        {
            return;
        }

        _randomCount = Math.Clamp(count ?? _randomCount, template.MinPlayers, template.Slots.Count);
        var removable = _randomCount > template.MinPlayers;
        _addPlayer.Visible = _randomCount < template.Slots.Count;
        _players.Columns = (Lan is null ? 6 : 7) + (removable ? 1 : 0);
        _players.AddChild(new Label { Text = "Player" });
        _players.AddChild(new Label { Text = "Controlled by" });
        AddLanHeader();
        _players.AddChild(new Label { Text = "Town" });
        _players.AddChild(new Label { Text = "Hero" });
        _players.AddChild(new Label { Text = "Tier-1 dwelling" });
        _players.AddChild(new Label { Text = template.TeamsLocked ? "Team (locked)" : "Team" });
        if (removable)
        {
            _players.AddChild(new Control());
        }

        var colours = Enumerable.Range(0, _randomCount).Select(i => i < choices.Count ? choices[i].Colour : -1).ToList();
        for (var i = 0; i < _randomCount; i++)
        {
            if (colours[i] < 0)
            {
                var own = i % Core.Domain.Players.Player.ColourCount;
                colours[i] = !colours.Contains(own) ? own
                    : Enumerable.Range(0, Core.Domain.Players.Player.ColourCount).First(c => !colours.Contains(c));
            }
        }

        for (var i = 0; i < _randomCount; i++)
        {
            var slot = template.Slots[i];
            var row = new RandomPlayerRow(i, slot, template.TeamsLocked, Lan is not null);
            if (i < choices.Count)
            {
                row.Apply(choices[i], _heroesOf, _tier1Of);
            }
            else if (!template.TeamsLocked
                     && choices.Take(_randomCount).Select(c => c.Team).Concat(_randomPlayers.Select(r => (int)r.Team.Value)).ToHashSet() is var taken
                     && taken.Contains((int)row.Team.Value))
            {
                // A player added after others moved up a place: a team of its own, not one a moved player kept.
                row.Team.Value = Enumerable.Range(0, GameState.MaxPlayers).First(t => !taken.Contains(t));
            }

            _seatNames.Add((new PlayerId(i), slot.Name));
            _players.AddChild(PlayerCell(new PlayerId(i), slot.Name, colours[i]));
            _players.AddChild(row.Control);
            _controllers[new PlayerId(i)] = row.Control; // for the LAN lobby; a random map's players are set in its request
            AddLanPlayer(new PlayerId(i));
            _players.AddChild(row.Town);
            _players.AddChild(row.Hero);
            _players.AddChild(row.Dwelling);
            _players.AddChild(row.Team);
            if (removable)
            {
                var remove = new Button { Text = $"Remove {slot.Name}", FocusMode = FocusModeEnum.None }.WithIcon(UiIcon.Minus);
                remove.TooltipText = "The players after this one move up a place.";
                var removed = i;
                remove.Pressed += () => RemoveRandomPlayer(removed);
                _players.AddChild(remove);
            }

            row.Control.ItemSelected += _ =>
            {
                row.ShowDwellings(_tier1Of);
                Changed();
            };
            row.Town.Changed += () =>
            {
                row.ShowHeroes(_heroesOf);
                row.ShowDwellings(_tier1Of);
                Changed();
            };
            row.Hero.ItemSelected += _ => Changed();
            row.Dwelling.ItemSelected += _ => Changed();
            row.Team.ValueChanged += _ => Changed();
            _randomPlayers.Add(row);
        }
    }

    /// <summary>A setting changed: a map generated with the old ones (shown for its problems) is dropped.</summary>
    private void Changed()
    {
        _generated = null;
        _openConfirmed = false;
        _start.SetLabel("Start");
    }

    private void Start()
    {
        if (_busy)
        {
            return;
        }

        if (!ShowsSeed)
        {
            _seed.Text = NewSeed(); // a release build plays every game on a new seed (ADR-225)
        }

        if (!ulong.TryParse(_seed.Text, CultureInfo.InvariantCulture, out var seed))
        {
            _status.Text = "Enter a numeric seed.";
            return;
        }

        if (LanStartProblem() is { } problem)
        {
            _status.Text = problem;
            return;
        }

        var options = WithLan(new GameOptions(
            SelectedFog, GameOptions.Default.RefogMargin, seed, _difficulty.Selected, _timerOn.ButtonPressed ? (int)_timerMinutes.Value : null)
        {
            Colours = ChosenColours(),
        });
        if (SelectedTemplate is { } template)
        {
            StartRandom(template, options);
            return;
        }

        if (SelectedMap is not { } map)
        {
            _status.Text = "Choose a map.";
            return;
        }

        try
        {
            var state = map.Create(options with { StartTowns = ChosenTowns() });
            if (!state.TeamsLocked)
            {
                state.AssignTeams(_teams.ToDictionary(t => t.Key, t => new TeamId((int)t.Value.Value)));
            }

            if (_controllers.Count > 0)
            {
                state.AssignControllers(_controllers.ToDictionary(c => c.Key, c => c.Value.GetSelectedId() != Computer));
            }

            Started?.Invoke(state);
        }
        catch (Exception e) when (e is FormatException or IOException or InvalidOperationException)
        {
            _status.Text = e.Message;
        }
    }

    /// <summary>
    /// Generates the map, a frame after showing "Generating…" (it takes a moment on large maps). A map without problems starts at once; one with
    /// problems is shown, and "Start anyway" starts it.
    /// </summary>
    private async void StartRandom(RandomMapChoice template, GameOptions options)
    {
        if (_generated is { } shown)
        {
            Started?.Invoke(shown.State);
            return;
        }

        _busy = true;
        _status.Text = "Generating the map…";
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        try
        {
            var maxHeroes = (int)_maxHeroes.Value;
            var request = new RandomMapRequest(
                _size.GetSelectedId(), _randomPlayers.Select(p => p.Player).ToList(), options, maxHeroes == template.MaxHeroes ? null : maxHeroes);
            var result = template.Create(request);
            if (result.Problems.Count == 0)
            {
                Started?.Invoke(result.State);
                return;
            }

            _generated = result;
            _start.SetLabel("Start anyway");
            _status.Text = $"Every try of this seed left problems; the best map has: {string.Join(" ", result.Problems)} Start it anyway, or {(ShowsSeed ? "change the seed" : "change a setting for another map")}.";
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            _status.Text = e.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// A map on a tab's list: a hand-made map or a random map template, with its size classes (<see cref="MapSizeClass"/>: a map's one, a
    /// template's every supported size, none when unknown).
    /// </summary>
    private sealed record MapEntry(string Name, ScenarioChoice? Map, RandomMapChoice? Template, IReadOnlyList<int> Sizes);

    /// <summary>What was chosen on a random map's player row, carried to another slot when a player before it is removed (ADR-225).</summary>
    private sealed record RandomPlayerChoice(int Controller, string? Town, string? Hero, string? Dwelling, int Team, int Colour);

    /// <summary>The controls of one player of a random map.</summary>
    private sealed class RandomPlayerRow
    {
        private readonly int _slot;
        private readonly bool _teamsLocked;
        private readonly bool _picksDwelling;
        private IReadOnlyList<HeroDefinition> _heroes = [];
        private IReadOnlyList<CreatureDefinition> _dwellings = [];

        public RandomPlayerRow(int slot, RandomMapSlot setup, bool teamsLocked, bool lan)
        {
            _slot = slot;
            _teamsLocked = teamsLocked;
            _picksDwelling = setup.PicksTier1Dwelling;
            Control = lan
                ? LanController(setup.HumanByDefault ? LanHere : setup.HumanAllowed ? LanRemote : Computer, setup.HumanAllowed)
                : HumanOrComputer(setup.HumanByDefault, setup.HumanAllowed);
            if (!setup.HumanAllowed)
            {
                Control.TooltipText = "This start is for computer players only.";
            }

            Hero.AddItem(Random);
            Hero.Disabled = true;
            Dwelling.AddItem(Random);
            Dwelling.Disabled = true;
            Dwelling.TooltipText = DwellingTip();
            Team.Value = setup.Team;
            Team.Editable = !teamsLocked;
        }

        public OptionButton Control { get; }

        /// <summary>The player's slot (and player number) on the template.</summary>
        public int Slot => _slot;

        public CrestPicker Town { get; } = new();

        public OptionButton Hero { get; } = new() { CustomMinimumSize = new Vector2(150, 0) };

        public OptionButton Dwelling { get; } = new() { CustomMinimumSize = new Vector2(120, 0) };

        public SpinBox Team { get; } = new() { MinValue = 0, MaxValue = GameState.MaxPlayers - 1, Step = 1 };

        private string? SelectedTown => Town.Town;

        public RandomMapPlayer Player => new(
            _slot,
            IsHuman,
            SelectedTown,
            Hero.Selected <= 0 || Hero.Selected > _heroes.Count ? null : _heroes[Hero.Selected - 1].Id,
            _teamsLocked ? null : (int)Team.Value,
            Dwelling.Selected <= 0 || Dwelling.Selected > _dwellings.Count ? null : _dwellings[Dwelling.Selected - 1].Id);

        private bool IsHuman => Control.GetSelectedId() != Computer;

        /// <summary>The town, hero and team chosen, in words (a LAN lobby's colour).</summary>
        public string Details =>
            $"{(SelectedTown is { } town ? CrestPicker.NameOf(town) : Random)} town, {(Hero.Selected <= 0 ? "random hero" : Hero.GetItemText(Hero.Selected))}"
            + (_teamsLocked ? "" : $", team {(int)Team.Value}");

        /// <summary>The row's choices, with the player's <paramref name="colour"/>.</summary>
        public RandomPlayerChoice Choice(int colour) => new(
            Control.GetSelectedId(), SelectedTown, Player.Hero, Player.Tier1Dwelling, (int)Team.Value, colour);

        /// <summary>
        /// Takes another row's choices: the controller when this slot allows it, the town, then the hero and dwelling when this slot offers them, and
        /// the team unless the template locks it.
        /// </summary>
        public void Apply(RandomPlayerChoice choice, Func<string, IReadOnlyList<HeroDefinition>> heroesOf, Func<string, IReadOnlyList<CreatureDefinition>> tier1Of)
        {
            if (Control.GetItemIndex(choice.Controller) is var index and >= 0 && !Control.IsItemDisabled(index))
            {
                Control.Select(index);
            }

            Town.Town = choice.Town;
            ShowHeroes(heroesOf);
            ShowDwellings(tier1Of);
            if (choice.Hero is { } hero && _heroes.ToList().FindIndex(h => h.Id == hero) is var heroAt and >= 0)
            {
                Hero.Select(heroAt + 1);
            }

            if (choice.Dwelling is { } dwelling && _dwellings.ToList().FindIndex(d => d.Id == dwelling) is var dwellingAt and >= 0)
            {
                Dwelling.Select(dwellingAt + 1);
            }

            if (!_teamsLocked)
            {
                Team.Value = choice.Team;
            }
        }

        /// <summary>
        /// The dwelling choice (RMG-7): "Random" and the town's tier-1 dwellings, for a human player with a picked town whose start zone gives
        /// no tier-1 dwelling of its own; otherwise only "Random" (a computer player's is always drawn).
        /// </summary>
        public void ShowDwellings(Func<string, IReadOnlyList<CreatureDefinition>> tier1Of)
        {
            Dwelling.Clear();
            Dwelling.AddItem(Random);
            _dwellings = _picksDwelling && IsHuman && SelectedTown is { } town ? tier1Of(town) : [];
            foreach (var line in _dwellings)
            {
                Dwelling.AddItem(line.Name);
            }

            Dwelling.Disabled = _dwellings.Count == 0;
            Dwelling.TooltipText = DwellingTip();
            Dwelling.Select(0);
        }

        private string DwellingTip() =>
            !_picksDwelling ? "This start town already has its tier-1 dwelling."
            : !IsHuman ? "A computer player's tier-1 dwelling is drawn at random."
            : SelectedTown is null ? "Pick a town to choose its tier-1 dwelling."
            : "The tier-1 dwelling the start town begins with.";

        /// <summary>The hero choice follows the town: "Random" and the town's heroes; a random town leaves only a random hero.</summary>
        public void ShowHeroes(Func<string, IReadOnlyList<HeroDefinition>> heroesOf)
        {
            Hero.Clear();
            Hero.AddItem(Random);
            _heroes = SelectedTown is { } town ? heroesOf(town) : [];
            foreach (var hero in _heroes)
            {
                Hero.AddItem($"{hero.Name} ({hero.Class.ToString().ToLowerInvariant()})");
            }

            Hero.Disabled = _heroes.Count == 0;
            Hero.Select(0);
        }
    }
}
