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
/// The adventure map's HUD (ADR-050, ADR-156): this layer holds the parts the "Layout" tab moves (the turn, resources, status and questions
/// block, the minimap, the hero and town lists, the action buttons), and a second layer above it, added beside it, holds the full-screen
/// panels (town, hero, exchange, dwelling, marketplace, services, Thieves' Guild, tavern, kingdom, quest log, puzzle map, intel). A hero
/// stepping into an own town, a dwelling, a marketplace or a service opens its screen. In the replay viewer (<c>playing</c> false) nothing
/// here issues a command: no End Turn, questions, exchange, Dig or Marketplace buttons, and the town list only moves the camera (ADR-164).
/// </summary>
public partial class AdventureHud : CanvasLayer
{
    private GameSession _session = null!;
    private MapViewContext _context = null!;
    private AdventureWorldView _world = null!;
    private StrategyCamera _camera = null!;
    private AdventureHudLayout _layout = null!;
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

    /// <summary>The status line under the resources.</summary>
    public Label Status { get; private set; } = null!;

    public HeroListPanel HeroList { get; private set; } = null!;

    public TownPanel TownScreen { get; private set; } = null!;

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
        var top = new VBoxContainer();
        _layout.SetInfo(top);
        var turnPanel = new TurnPanel();
        top.AddChild(turnPanel);
        turnPanel.Bind(session, endTurn: playing);
        var resources = new ResourceBar();
        top.AddChild(resources);
        resources.Bind(session);
        Status = new Label
        {
            Text = playing
                ? "Click: route, click again: go. Tab: next hero  L: level  K: kingdom  Q: quests  P: puzzle  G: dig  WASD/QE/wheel: camera"
                : "Watching. Space: play or pause  L: level  right click: information  WASD/QE/wheel: camera",
        };
        top.AddChild(Status);
        if (playing)
        {
            // The replay viewer leaves out the questions: the record answers them (and an unbound panel's parts would never enter the tree).
            var decisions = new DecisionPanel();
            top.AddChild(decisions);
            decisions.Bind(session);
        }

        var notice = new NoticePanel();
        top.AddChild(notice);
        notice.Bind(session);
        // HoMM-style by default: the minimap, the hero and town lists and the action panel down the right.
        var minimapBox = new VBoxContainer();
        _layout.SetPart(HudPart.Minimap, minimapBox);
        var minimap = new MinimapWidget { CellPixels = Math.Max(2, 320f / Math.Max(session.State.Map.Width, session.State.Map.Height)) };
        minimapBox.AddChild(minimap);
        minimap.Bind(context);
        minimap.TileClicked += tile => _camera.FocusOn(MapSpace.ToWorld(tile));
        var level = new Button { Text = "Switch level (L)", Disabled = session.State.Map.Levels < 2 };
        level.Pressed += () => _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels);
        minimapBox.AddChild(level);
        var lists = new VBoxContainer();
        _layout.SetPart(HudPart.Lists, lists);
        HeroList = new HeroListPanel();
        lists.AddChild(HeroList);
        HeroList.Bind(session);
        HeroList.HeroSelected += hero => HeroSelected?.Invoke(hero);
        HeroList.HeroInfoRequested += hero =>
        {
            if (playing)
            {
                ShowHero(hero);
            }
        };
        var townList = new TownListPanel();
        lists.AddChild(townList);
        townList.Bind(session);
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
        var side = new VBoxContainer();
        _layout.SetPart(HudPart.Actions, side);

        BuildScreens(playing, screens, side);
        _layout.BottomReserve = playing ? 0 : ReplayControlBar.ReservedHeight;
        _layout.Apply(layout);
    }

    /// <summary>Places the parts as <paramref name="layout"/> says (the "Layout" tab changed).</summary>
    public void ApplyLayout(LayoutSettings layout) => _layout.Apply(layout);

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

    /// <summary>Closes the screens over the map (town, hero, dwelling, market, services, kingdom, quest log, Thieves' Guild).</summary>
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
        var towns = TownListPanel.TownsOf(_session.State);
        if (towns.Count == 0)
        {
            Status.Text = "You have no town.";
            return;
        }

        var open = TownScreen.CurrentTown;
        var index = open is null ? -1 : towns.ToList().FindIndex(t => t.Id == open);
        ShowTown(towns[(index + 1) % towns.Count].Id);
    }

    /// <summary>The full-screen panels and the action buttons that open them.</summary>
    private void BuildScreens(bool playing, Node screens, VBoxContainer side)
    {
        // The town screen covers the map and the HUD (ADR-050); a hero stepping into an own town opens it.
        var townLayer = new CanvasLayer { Name = "TownScreen", Layer = 1 };
        screens.AddChild(townLayer);
        TownScreen = new TownPanel { FullScreen = true };
        townLayer.AddChild(TownScreen);
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
        TownScreen.TradeRequested += () => _marketScreen.Open();
        _guildScreen = new ThievesGuildPanel();
        townLayer.AddChild(_guildScreen);
        _guildScreen.Bind(_session);
        TownScreen.ThievesGuildRequested += () => _guildScreen.Open(_session.State.CurrentPlayer.Id);
        var tavernScreen = new TavernPanel();
        townLayer.AddChild(tavernScreen);
        tavernScreen.Bind(_session);
        TownScreen.TavernRequested += tavernScreen.Open;
        TownScreen.Closed += tavernScreen.Close;
        tavernScreen.ThievesGuildRequested += () => _guildScreen.Open(_session.State.CurrentPlayer.Id);
        townLayer.MoveChild(_guildScreen, -1); // the guild opens over the tavern
        KingdomScreen = new KingdomOverviewPanel();
        townLayer.AddChild(KingdomScreen);
        KingdomScreen.Bind(_session);
        var kingdom = new Button { Text = "Kingdom (K)", TooltipText = "Every source of income and the totals" }.WithTreasuryIcon();
        kingdom.Pressed += () => KingdomScreen.Open(_session.State.CurrentPlayer.Id);
        side.AddChild(kingdom);
        QuestScreen = new QuestLogPanel();
        townLayer.AddChild(QuestScreen);
        QuestScreen.Bind(_session);
        QuestScreen.ShowRequested += position =>
        {
            _context.SetLevel(position.Level);
            _camera.FocusOn(MapSpace.ToWorld(position));
        };
        var quests = new Button { Text = "Quests (Q)", TooltipText = "The quests your heroes were shown" }.WithIcon(UiIcon.Quests);
        quests.Pressed += () => QuestScreen.Open(_session.State.CurrentPlayer.Id);
        side.AddChild(quests);
        PuzzleScreen = new PuzzleMapPanel();
        townLayer.AddChild(PuzzleScreen);
        PuzzleScreen.Bind(_session);
        var grail = new HBoxContainer();
        side.AddChild(grail);
        var puzzle = new Button { Text = "Puzzle map (P)", TooltipText = "The ground around the Grail, as your obelisks show it" }.WithIcon(UiIcon.PuzzleMap);
        puzzle.Pressed += () => PuzzleScreen.Open(_session.State.CurrentPlayer.Id);
        grail.AddChild(puzzle);
        var dig = new Button { Text = "Dig (G)", TooltipText = "The selected hero digs where it stands, with a whole day's movement" }.WithIcon(UiIcon.Dig);
        dig.Pressed += () => DigRequested?.Invoke();
        dig.Visible = playing;
        grail.AddChild(dig);

        // Trading belongs to the player, not a hero: a button, or a hero visiting a map marketplace (ADR-067).
        var market = new Button { Text = "Marketplace" }.WithResourceIcon(ResourceType.Gold);
        void RefreshMarketButton()
        {
            market.Disabled = !MarketplacePanel.CanOpenFromTowns(_session.State);
            market.TooltipText = market.Disabled ? "Build a marketplace in a town to trade" : "Trade gold against resources";
        }

        market.Pressed += () => _marketScreen.Open();
        market.Visible = playing;
        side.AddChild(market);
        RefreshMarketButton();
        this.SubscribeWhileInTree<TownBuilt>(_session.Events, _ => RefreshMarketButton());
        this.SubscribeWhileInTree<TownOwnerChanged>(_session.Events, _ => RefreshMarketButton());
        this.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => RefreshMarketButton());
        this.SubscribeWhileInTree<MapObjectVisited>(_session.Events, e =>
        {
            if (playing)
            {
                OpenVisitedScreen(e);
            }
        });
    }

    /// <summary>A human's hero visited an object with a screen of its own: the marketplace, a service, a dwelling, an own town.</summary>
    private void OpenVisitedScreen(MapObjectVisited e)
    {
        if (_session.State.CurrentPlayer.IsHuman && _session.State.FindObject(e.Object) is Marketplace marketplace)
        {
            _marketScreen.Open(marketplace.Id);
        }

        if (_session.State.CurrentPlayer.IsHuman && _session.State.FindObject(e.Object) is { } served && ObjectServicePanel.Serves(served))
        {
            _serviceScreen.Open(served.Id); // Hill Fort, War Machine Factory (ADR-068)
        }

        if (_session.State.CurrentPlayer.IsHuman && _session.State.FindObject(e.Object) is CreatureDwelling dwelling)
        {
            _dwellingScreen.Open(dwelling.Id); // flagged by now (the visit flagged it first); recruit from it here
        }

        if (_session.State.CurrentPlayer.IsHuman && _session.State.FindObject(e.Object) is Town town
            && town.Owner == _session.State.CurrentPlayer.Id)
        {
            ShowTown(town.Id);
        }
    }
}
