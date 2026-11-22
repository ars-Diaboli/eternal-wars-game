using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The adventure map's HUD (ADR-050, ADR-156, ADR-186): this layer holds the parts the "Layout" tab moves (the turn block, the minimap, the hero
/// and town lists, the 3×3 action buttons of ADR-210), the <see cref="StatusBar"/> along the bottom (the status line and the resources), and
/// the questions and notices in the middle of the screen; a second layer above it, added beside it, holds the full-screen
/// panels (town, hero, exchange, dwelling, marketplace, services, Thieves' Guild, tavern, kingdom, quest log, puzzle map, intel) and the
/// spell book. A hero stepping into an own town, a dwelling, a marketplace or a service opens its screen. In the replay viewer (<c>playing</c>
/// false) nothing here issues a command: no End Turn, questions or exchange; the hero, spell book, Dig and Marketplace buttons are disabled,
/// and the town list only moves the camera (ADR-164).
/// </summary>
public partial class AdventureHud : CanvasLayer
{
    private GameSession _session = null!;
    private MapViewContext _context = null!;
    private AdventureWorldView _world = null!;
    private StrategyCamera _camera = null!;
    private AdventureHudLayout _layout = null!;
    private Control _popUps = null!;
    private DwellingPanel _dwellingScreen = null!;
    private MarketplacePanel _marketScreen = null!;
    private ObjectServicePanel _serviceScreen = null!;
    private ThievesGuildPanel _guildScreen = null!;
    private AdventureIntelPanel _intelScreen = null!;

    public AdventureHud()
    {
        Name = "Hud";
    }

    /// <summary>A hero was picked in the hero list.</summary>
    public event Action<HeroId>? HeroSelected;

    /// <summary>The Dig button (ADR-140).</summary>
    public event Action? DigRequested;

    /// <summary>An adventure spell was armed on the hero screen and waits for its cell; the status line says what to click.</summary>
    public event Action<HeroId, string>? SpellAimed;

    /// <summary>The status line on the status bar: the HUD's and the map's messages.</summary>
    public Label Status => StatusBar.Message;

    /// <summary>The stripe along the bottom of the screen (ADR-186): the status line and the resources.</summary>
    public StatusBar StatusBar { get; private set; } = null!;

    public HeroListPanel HeroList { get; private set; } = null!;

    /// <summary>The town screen (ADR-246, ADR-296); today's <see cref="TownPanel"/> stays in the 2D harness as a tool (TOWN-16).</summary>
    public TownScreen.TownScreenPanel TownScreen { get; private set; } = null!;

    public HeroPanel HeroScreen { get; private set; } = null!;

    public KingdomOverviewPanel KingdomScreen { get; private set; } = null!;

    public QuestLogPanel QuestScreen { get; private set; } = null!;

    public PuzzleMapPanel PuzzleScreen { get; private set; } = null!;

    /// <summary>A full-screen panel covers the map and takes the input (Escape closes it); the Thieves' Guild and the intel window do not count.</summary>
    public bool IsScreenOpen => TownScreen.Visible || HeroScreen.Visible || _dwellingScreen.Visible || _marketScreen.Visible || _serviceScreen.Visible
        || KingdomScreen.Visible || QuestScreen.Visible || PuzzleScreen.Visible;

    /// <summary>
    /// Builds the HUD for <paramref name="session"/> seen through <paramref name="context"/>, placed as <paramref name="layout"/> says; the
    /// full-screen panels' layer goes into <paramref name="screens"/>. The minimap, the quest log and the town screen move
    /// <paramref name="camera"/>.
    /// </summary>
    public void Bind(
        GameSession session, MapViewContext context, AdventureWorldView world, StrategyCamera camera, bool playing, LayoutSettings layout, Node screens)
    {
        (_session, _context, _world, _camera) = (session, context, world, camera);

        // The parts the "Layout" tab moves (ADR-156): the info block, the minimap, the lists and the action panel.
        _layout = new AdventureHudLayout();
        AddChild(_layout);
        _turn = new TurnPanel();
        _layout.SetInfo(_turn);
        _turn.Bind(session, endTurn: playing);

        // The status bar along the bottom (ADR-186): the hints line first, then the messages and what the pointer is over; the resources on
        // the right. In the replay viewer it stands above the viewer's bar.
        StatusBar = new StatusBar
        {
            BottomOffset = playing ? 0 : ReplayControlBar.ReservedHeight, // the menu button is one of the action buttons (ADR-210)
        };
        AddChild(StatusBar);
        StatusBar.Bind(session, () => context.ListedPlayer, calendar: true);
        Status.Text = playing
            ? "Click: route, click again: go. Tab: next hero  H: hero  C: spell book  L: level  K: kingdom  Q: quests  P: puzzle  G: dig  WASD/QE/wheel: camera"
            : "Watching. Space: play or pause  L: level  right click: information  WASD/QE/wheel: camera";

        // Questions and notices open in the middle of the screen, over the map (ADR-186).
        var middle = new CenterContainer { Name = "PopUps", MouseFilter = Control.MouseFilterEnum.Ignore };
        _popUps = middle;
        AddChild(middle);
        middle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var popUps = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(PopUpWidth, 0) };
        popUps.AddThemeConstantOverride("separation", UiSpacing.Gap);
        middle.AddChild(popUps);
        if (playing)
        {
            // The replay viewer leaves out the questions: the record answers them (and an unbound panel's parts would never enter the tree).
            var decisions = new DecisionPanel();
            decisions.UseOpaqueBackground();
            popUps.AddChild(decisions);
            decisions.Bind(session, context.PlaysHere);
        }

        var notice = new NoticePanel();
        notice.UseOpaqueBackground();
        popUps.AddChild(notice);
        notice.Bind(session);
        // HoMM-style by default: the minimap, the hero and town lists and the action panel down the right.
        var minimapBox = new VBoxContainer();
        _layout.SetPart(HudPart.Minimap, minimapBox);
        var minimap = new MinimapWidget { CellPixels = Math.Max(2, 320f / Math.Max(session.State.Map.Width, session.State.Map.Height)) };
        minimapBox.AddChild(minimap);
        minimap.Bind(context);
        minimap.TileClicked += tile => _camera.FocusOn(MapSpace.ToWorld(tile));
        var lists = new VBoxContainer();
        _layout.SetPart(HudPart.Lists, lists);
        HeroList = new HeroListPanel();
        lists.AddChild(HeroList);
        HeroList.Bind(session, () => context.ListedPlayer);
        HeroList.HeroSelected += hero => HeroSelected?.Invoke(hero);
        var townList = new TownListPanel();
        lists.AddChild(townList);
        townList.Bind(session, () => context.ListedPlayer);
        townList.TownSelected += town =>
        {
            if (playing)
            {
                ShowTown(town);
            }
            else if (_session.State.FindObject(town) is { } watched)
            {
                _context.SetLevel(watched.VisitPosition.Level);
                _camera.FocusOn(MapSpace.ToWorld(watched.VisitPosition));
            }
        };
        (_layout.HeroList, _layout.TownList) = (HeroList, townList);

        // The action buttons (ADR-210): a 3×3 grid of square icon buttons in the corner the "Layout" tab picks.
        _actions = new GridContainer { Columns = 3 };
        _actions.AddThemeConstantOverride("h_separation", UiSpacing.Gap);
        _actions.AddThemeConstantOverride("v_separation", UiSpacing.Gap);
        var actionsPart = new CenterContainer(); // centred in a stack made wider by the minimap or the lists
        actionsPart.AddChild(_actions);
        _layout.SetPart(HudPart.Actions, actionsPart);

        BuildScreens(playing, screens);
        BuildActions(playing);

        // The status bar stands over the town, hero and exchange screens too (ADR-204), which stop short of it: its own layer above
        // theirs, shown while the HUD is.
        var barLayer = new CanvasLayer { Name = "StatusBarLayer", Layer = 1 };
        screens.AddChild(barLayer);
        StatusBar.Reparent(barLayer, keepGlobalTransform: false);
        VisibilityChanged += () => barLayer.Visible = Visible;
        _layout.BottomReserve = StatusBar.Height + StatusBar.BottomOffset; // the bottom parts stand above the status bar
        ApplyLayout(layout);
    }

    /// <summary>How wide the questions and notices in the middle of the screen are.</summary>
    private const float PopUpWidth = 520;

    /// <summary>
    /// Whether the map's parts (the info block, the minimap, the lists, the action buttons) and its notices are put away: behind the town
    /// screen's 3D scene (ADR-297), which shows through the screen's main area, as the screen's backdrop hid them before. The status bar
    /// stays; a question hides the town screen, so it shows as usual.
    /// </summary>
    public bool MapPartsHidden
    {
        get => !_layout.Visible;
        set => _layout.Visible = _popUps.Visible = !value;
    }

    /// <summary>What the pointer is over on the map goes to the status line (null: nothing there).</summary>
    public void ShowHover(string? text) => StatusBar.ShowHover(text);

    /// <summary>Places the parts as <paramref name="layout"/> says (the "Layout" tab changed).</summary>
    public void ApplyLayout(LayoutSettings layout)
    {
        // With the side panel on, End turn stands with its actions (ADR-204); off, at the top of the map.
        _turn.InPanel = HudArrangement.PanelSide(layout) is not null;
        _layout.Apply(layout);
    }

    private TurnPanel _turn = null!;

    /// <summary>The 3×3 action buttons (ADR-210).</summary>
    private GridContainer _actions = null!;

    private Button _heroButton = null!;
    private Button _bookButton = null!;
    private Button _digButton = null!;
    private Button _marketButton = null!;
    private SpellBookPanel _spellBook = null!;
    private bool _playing;
    private HeroId? _selected;

    /// <summary>The Menu action button (F10): the app opens or closes its game menu.</summary>
    public event Action? MenuRequested;

    /// <summary>The hero the app selected (on the map or in the list): the list shows it, the hero, spell book and dig buttons act on it.</summary>
    public void ShowSelected(HeroId? hero)
    {
        _selected = hero;
        HeroList.ShowSelected(hero);
        RefreshActions();
    }

    /// <summary>C, or the spell book button: the book of the selected hero, if it has one.</summary>
    public void OpenSpellBook(HeroId? id)
    {
        if (_spellBook.Visible)
        {
            _spellBook.Close();
            return;
        }

        if (OwnHero(id) is not { } hero)
        {
            Status.Text = "Select a hero first.";
            return;
        }

        if (!hero.HasSpellBook)
        {
            Status.Text = $"{hero.Name} has no spell book.";
            return;
        }

        _spellBook.Open(hero.Id);
    }

    /// <summary>The hero of the player whose turn it is with this id, or null (none selected, or someone else's).</summary>
    private Core.Domain.Heroes.Hero? OwnHero(HeroId? id) =>
        id is { } wanted ? _session.State.HeroesOf(_session.State.CurrentPlayer.Id).FirstOrDefault(h => h.Id == wanted) : null;

    /// <summary>
    /// The action buttons, three to a row (ADR-210): hero screen, spell book, kingdom; quests, puzzle map, dig; marketplace, level, menu. A
    /// button whose action cannot be taken now is disabled, with the reason in its tooltip. In the replay viewer the ones that would issue a
    /// command or open a hero's screens stay disabled.
    /// </summary>
    private void BuildActions(bool playing)
    {
        _playing = playing;
        _heroButton = AddAction("Hero screen (H)", UiIcon.Hero, () => ShowHero(_selected));
        _bookButton = AddAction("Spell book (C)", UiIcon.SpellBook, () => OpenSpellBook(_selected));
        AddAction("Kingdom (K)", UiIcon.Kingdom, () => KingdomScreen.Open(_session.State.CurrentPlayer.Id), "Every source of income and the totals");
        AddAction("Quests (Q)", UiIcon.Quests, () => QuestScreen.Open(_session.State.CurrentPlayer.Id), "The quests your heroes were shown");
        AddAction("Puzzle map (P)", UiIcon.PuzzleMap, () => PuzzleScreen.Open(_session.State.CurrentPlayer.Id), "The ground around the Grail, as your obelisks show it");
        _digButton = AddAction("Dig (G)", UiIcon.Dig, () => DigRequested?.Invoke());

        // Trading belongs to the player, not a hero: this button, or a hero visiting a map marketplace (ADR-067).
        _marketButton = AddAction("Marketplace", UiIcon.Marketplace, () => _marketScreen.Open());
        var level = AddAction("Switch level (L)", UiIcon.SwitchLevel, () => _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels),
            "Between the surface and the underground");
        if (_session.State.Map.Levels < 2)
        {
            level.Disabled = true;
            level.TooltipText = "This map has one level";
        }

        AddAction("Menu (F10)", UiIcon.Menu, () => MenuRequested?.Invoke(), "Save, load, settings");

        RefreshActions();
        this.SubscribeWhileInTree<TownBuilt>(_session.Events, _ => RefreshActions());
        this.SubscribeWhileInTree<TownOwnerChanged>(_session.Events, _ => RefreshActions());
        this.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => RefreshActions());
        this.SubscribeWhileInTree<SpellBookBought>(_session.Events, _ => RefreshActions());
        this.SubscribeWhileInTree<HeroDefeated>(_session.Events, _ => RefreshActions());
    }

    private Button AddAction(string label, UiIcon icon, Action pressed, string tooltip = "")
    {
        var button = new Button { Text = label, TooltipText = tooltip }.WithIcon(icon, UiIcons.BarSize);
        button.Pressed += pressed;
        _actions.AddChild(button);
        return button;
    }

    /// <summary>Enables the buttons that depend on the selected hero, the towns or the replay viewer, and says why the others are not.</summary>
    private void RefreshActions()
    {
        var hero = OwnHero(_selected);
        var noHero = hero is null ? "Select a hero first" : null;
        Enable(_heroButton, _playing ? noHero : "Not while watching a replay");
        Enable(_bookButton, !_playing ? "Not while watching a replay" : noHero ?? (hero!.HasSpellBook ? null : $"{hero.Name} has no spell book"));
        Enable(_digButton, _playing ? noHero : "Not while watching a replay", "The selected hero digs where it stands, with a whole day's movement");
        Enable(_marketButton,
            !_playing ? "Not while watching a replay" : MarketplacePanel.CanOpenFromTowns(_session.State) ? null : "Build a marketplace in a town to trade",
            "Trade gold against resources");
    }

    private static void Enable(Button button, string? refusal, string tooltip = "")
    {
        button.Disabled = refusal is not null;
        button.TooltipText = refusal ?? tooltip;
    }

    /// <summary>The status line tells of the game's flow: the arena battle's day (ADR-097), defeats and a lost last town's grace period.</summary>
    public void ReportGameFlow()
    {
        // An arena map's day 2 (ADR-097): the battle opens by itself; the status line says who meets whom.
        this.SubscribeWhileInTree<ArenaChampionsChosen>(_session.Events, _ =>
            Status.Text = $"Day 2: the arena battle. {_session.State.Arena?.Summary}");

        this.SubscribeWhileInTree<PlayerDefeated>(_session.Events, e =>
            Status.Text = $"{_session.State.GetPlayer(e.Player).Name} is defeated: {e.Reason}");
        this.SubscribeWhileInTree<GracePeriodStarted>(_session.Events, e =>
            Status.Text = $"{_session.State.GetPlayer(e.Player).Name} lost its last town and is defeated on {e.DefeatDay} unless it owns one again.");
        this.SubscribeWhileInTree<GracePeriodEnded>(_session.Events, e =>
            Status.Text = $"{_session.State.GetPlayer(e.Player).Name} owns a town again.");
    }

    /// <summary>Closes the screens over the map (town, hero, dwelling, market, services, kingdom, quest log, Thieves' Guild, spell book).</summary>
    public void CloseScreens()
    {
        TownScreen.Close();
        HeroScreen.Close();
        _dwellingScreen.Close();
        _marketScreen.Close();
        _serviceScreen.Close();
        KingdomScreen.Hide();
        QuestScreen.Hide();
        PuzzleScreen.Hide();
        _guildScreen.Hide();
        _spellBook.Close();
    }

    /// <summary>The town screen of an own town: the map centres on it first.</summary>
    public void ShowTown(MapObjectId id)
    {
        if (_session.State.FindObject(id) is not Town town || town.Owner != _session.State.CurrentPlayer.Id)
        {
            return;
        }

        _context.SetLevel(town.VisitPosition.Level);
        _camera.FocusOn(MapSpace.ToWorld(town.VisitPosition));
        _world.PathPreview.Clear();
        TownScreen.Open(id);
    }

    /// <summary>The hero screen of an own hero (H: the selected one).</summary>
    public void ShowHero(HeroId? id)
    {
        if (id is not { } hero || _session.State.HeroesOf(_session.State.CurrentPlayer.Id).All(h => h.Id != hero))
        {
            Status.Text = "Select a hero first.";
            return;
        }

        HeroScreen.Open(hero);
    }

    /// <summary>T: the town screen of the next own town.</summary>
    public void OpenNextTown()
    {
        var towns = TownListPanel.TownsOf(_session.State, _context.ListedPlayer);
        if (towns.Count == 0)
        {
            Status.Text = "You have no town.";
            return;
        }

        var open = TownScreen.CurrentTown;
        var index = open is null ? -1 : towns.ToList().FindIndex(t => t.Id == open);
        ShowTown(towns[(index + 1) % towns.Count].Id);
    }

    /// <summary>The full-screen panels and the windows over the map.</summary>
    private void BuildScreens(bool playing, Node screens)
    {
        // The town screen covers the map and the HUD (ADR-050); a hero stepping into an own town opens it.
        var townLayer = new CanvasLayer { Name = "TownScreen", Layer = 1 };
        screens.AddChild(townLayer);
        TownScreen = new TownScreen.TownScreenPanel();
        townLayer.AddChild(TownScreen);
        TownScreen.HeroRequested += hero => HeroScreen.Open(hero);
        TownScreen.SwitchRequested += ShowTown;
        TownScreen.Bind(_session);
        HeroScreen = new HeroPanel { FullScreen = true };
        townLayer.AddChild(HeroScreen);
        HeroScreen.Bind(_session);
        // Two own heroes met: the exchange screen covers the map like the hero screen (ADR-153).
        if (playing)
        {
            var exchange = new ExchangePanel(); // it opens itself when two own heroes meet; not in the replay viewer
            townLayer.AddChild(exchange);
            exchange.Bind(_session);
        }

        HeroScreen.AdventureSpellArmed += (hero, spell) =>
        {
            SpellAimed?.Invoke(hero, spell);
            Status.Text = AdventureSpellPrompt.Aim(_session.State, hero, spell);
        };
        _intelScreen = new AdventureIntelPanel();
        townLayer.AddChild(_intelScreen);
        _intelScreen.Bind(_session);
        HeroScreen.LedgerReopened += hero =>
            _intelScreen.ShowLedger(AdventureIntel.Ledger(_session.State, _session.State.GetHero(hero).Owner, inTowns: true));
        HeroScreen.EavesdropReopened += (hero, town) =>
        {
            if (_session.State.FindObject(town) is Town listened)
            {
                HeroScreen.Close();
                _intelScreen.ShowTown(AdventureIntel.Town(_session.State, listened, _session.State.GetHero(hero).Owner));
            }
        };
        _dwellingScreen = new DwellingPanel();
        townLayer.AddChild(_dwellingScreen);
        _dwellingScreen.Bind(_session);
        _marketScreen = new MarketplacePanel();
        townLayer.AddChild(_marketScreen);
        _marketScreen.Bind(_session);
        _serviceScreen = new ObjectServicePanel();
        townLayer.AddChild(_serviceScreen);
        _serviceScreen.Bind(_session);
        _guildScreen = new ThievesGuildPanel();
        townLayer.AddChild(_guildScreen);
        _guildScreen.Bind(_session);
        TownScreen.ThievesGuildRequested += () => _guildScreen.Open(_session.State.CurrentPlayer.Id);
        TownScreen.IsCovered = () => HeroScreen.Visible || _marketScreen.Visible || _guildScreen.Visible;
        townLayer.MoveChild(_guildScreen, -1); // the guild opens over the town screen's windows
        KingdomScreen = new KingdomOverviewPanel();
        townLayer.AddChild(KingdomScreen);
        KingdomScreen.Bind(_session);
        QuestScreen = new QuestLogPanel();
        townLayer.AddChild(QuestScreen);
        QuestScreen.Bind(_session);
        QuestScreen.ShowRequested += position =>
        {
            _context.SetLevel(position.Level);
            _camera.FocusOn(MapSpace.ToWorld(position));
        };
        PuzzleScreen = new PuzzleMapPanel();
        townLayer.AddChild(PuzzleScreen);
        PuzzleScreen.Bind(_session);

        // The selected hero's spell book (ADR-210): its adventure spells are cast as from the hero screen.
        _spellBook = new SpellBookPanel();
        townLayer.AddChild(_spellBook);
        _spellBook.Bind(_session);
        _spellBook.AdventureSpellArmed += (hero, spell) =>
        {
            SpellAimed?.Invoke(hero, spell);
            Status.Text = AdventureSpellPrompt.Aim(_session.State, hero, spell);
        };
        _spellBook.LedgerReopened += hero =>
            _intelScreen.ShowLedger(AdventureIntel.Ledger(_session.State, _session.State.GetHero(hero).Owner, inTowns: true));
        _spellBook.EavesdropReopened += (hero, town) =>
        {
            if (_session.State.FindObject(town) is Town listened)
            {
                _spellBook.Close();
                _intelScreen.ShowTown(AdventureIntel.Town(_session.State, listened, _session.State.GetHero(hero).Owner));
            }
        };
        townLayer.MoveChild(_intelScreen, -1); // the ledger and the town open over the book

        this.SubscribeWhileInTree<MapObjectVisited>(_session.Events, e =>
        {
            if (playing)
            {
                OpenVisitedScreen(e);
            }
        });
    }

    /// <summary>A hero of a player at this screen visited an object with a screen of its own: the marketplace, a service, a dwelling, an own town.</summary>
    private void OpenVisitedScreen(MapObjectVisited e)
    {
        if (_context.PlaysHere(_session.State.CurrentPlayer.Id) && _session.State.FindObject(e.Object) is Marketplace marketplace)
        {
            _marketScreen.Open(marketplace.Id);
        }

        if (_context.PlaysHere(_session.State.CurrentPlayer.Id) && _session.State.FindObject(e.Object) is { } served && ObjectServicePanel.Serves(served))
        {
            _serviceScreen.Open(served.Id); // Hill Fort, War Machine Factory (ADR-068)
        }

        if (_context.PlaysHere(_session.State.CurrentPlayer.Id) && _session.State.FindObject(e.Object) is CreatureDwelling dwelling)
        {
            _dwellingScreen.Open(dwelling.Id); // flagged by now (the visit flagged it first); recruit from it here
        }

        if (_context.PlaysHere(_session.State.CurrentPlayer.Id) && _session.State.FindObject(e.Object) is Town town
            && town.Owner == _session.State.CurrentPlayer.Id)
        {
            ShowTown(town.Id);
        }
    }
}
