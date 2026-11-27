using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The adventure map's HUD (ADR-050, ADR-186, ADR-309): this layer holds the map's parts as the "Layout" tab's mirror places them (the minimap
/// and the town list in the side column, the hero list at the other edge, the turn banner at the top centre, and the bottom row: the selected
/// hero's card and army, the 2×5 action buttons and End turn), the <see cref="StatusBar"/> along the bottom (the subject, the detail and the
/// calendar) and the <see cref="TopBar"/> with the resources (both on a layer over the full-screen panels), the questions and notices in
/// the middle of the screen (the map dimmed behind a question and the LAN pause card), and the hover card beside the pointer over a map
/// object (<see cref="HoverCard"/>); a second layer above it, added beside it, holds the full-screen panels (town, hero, exchange, dwelling,
/// marketplace, services, Thieves' Guild, tavern, kingdom, quest log, puzzle map, intel) and the spell book. A hero stepping into an own
/// town, a dwelling, a marketplace or a service opens its screen. In the replay viewer (<c>playing</c> false) nothing here issues a command:
/// no End turn, questions or exchange; Move and the hero, spell book, Dig and Marketplace buttons are disabled, and the town list only moves
/// the camera (ADR-164).
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

    /// <summary>The Move button (ADR-309): the selected hero walks the route shown on the map.</summary>
    public event Action? MoveRequested;

    /// <summary>A stack of the army strip was right-clicked: the hero, the slot and where, for its information window.</summary>
    public event Action<HeroId, int, Vector2>? StackInfoRequested;

    /// <summary>An adventure spell was armed on the hero screen and waits for its cell; the status line says what to click.</summary>
    public event Action<HeroId, string>? SpellAimed;

    /// <summary>The status line on the status bar: the HUD's and the map's messages.</summary>
    public Label Status => StatusBar.Message;

    /// <summary>The stripe along the bottom of the screen (ADR-186, ADR-309): the subject, the detail and the calendar.</summary>
    public StatusBar StatusBar { get; private set; } = null!;

    /// <summary>The resources in the top corner away from the minimap (ADR-309).</summary>
    public ResourceTopBar TopBar { get; private set; } = null!;

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

        // The side panel's parts (ADR-191), on the side the "Layout" tab's mirror picks (ADR-309): the info block, the minimap, the lists and the action panel.
        _layout = new AdventureHudLayout();
        AddChild(_layout);

        // The status bar along the bottom (ADR-186, ADR-309): the hints line first, then the messages and what the pointer is over; the
        // calendar on the right. In the replay viewer it stands above the viewer's bar. The resources are on the top bar.
        StatusBar = new StatusBar
        {
            BottomOffset = playing ? 0 : ReplayControlBar.ReservedHeight, // the menu button is one of the action buttons (ADR-210)
        };
        AddChild(StatusBar);
        StatusBar.Bind(session);
        TopBar = new ResourceTopBar();
        AddChild(TopBar);
        TopBar.Bind(session, () => context.ListedPlayer);
        Status.Text = playing
            ? "Click: route, click again: go. Tab: next hero  H: hero  C: spell book  L: level  K: kingdom  Q: quests  P: puzzle  G: dig  WASD/QE/wheel: camera"
            : "Watching. Space: play or pause  L: level  right click: information  WASD/QE/wheel: camera";

        // The map behind a question, End turn's question and the LAN pause card is dimmed (ADR-309): over the map, under the HUD's parts.
        _dim = new ColorRect { Name = "Dim", Color = UiTheme.MapDim, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        AddChild(_dim);
        MoveChild(_dim, 0);
        _dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        HoverCard = new MapHoverCard();
        AddChild(HoverCard);
        VisibilityChanged += HoverCard.Leave;
        Callable.From(RefreshDim).CallDeferred(); // once in the tree: a loaded game may already be waiting for an answer

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
            _decisions = new DecisionPanel();
            _decisions.UseOpaqueBackground();
            popUps.AddChild(_decisions);
            _decisions.VisibilityChanged += RefreshDim;
            _decisions.Bind(session, context.PlaysHere);
        }

        var notice = new NoticePanel();
        notice.UseOpaqueBackground();
        popUps.AddChild(notice);
        notice.Bind(session);
        // The turn banner at the top centre (ADR-309), put away with the map's other parts; the replay viewer has none.
        if (playing)
        {
            Banner = new TurnBannerPanel();
            _layout.AddChild(Banner);
            Banner.Bind(session);
        }

        // The minimap in the top right, or the top left when mirrored, the town list in its own panel below it; the hero list in its own
        // panel at the other edge (ADR-309, ADR-313).
        _layout.AddToColumn(BuildMinimap());
        HeroList = new HeroListPanel();
        _layout.SetOpposite(ListFrame("HeroListFrame", HeroList), HeroList);
        HeroList.Bind(session, () => context.ListedPlayer);
        HeroList.HeroSelected += hero => HeroSelected?.Invoke(hero);
        var townList = new TownListPanel();
        _layout.SetTownList(ListFrame("TownListFrame", townList), townList);
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

        BuildBottomRow(playing, popUps);
        BuildScreens(playing, screens);
        BuildActions(playing);

        // The status bar and the top bar stand over the town, hero and exchange screens too (ADR-204, ADR-309), which stop short of
        // them: their own layer above theirs, shown while the HUD is. While one of those screens is open the top bar runs across it.
        var barLayer = new CanvasLayer { Name = "StatusBarLayer", Layer = 1 };
        screens.AddChild(barLayer);
        StatusBar.Reparent(barLayer, keepGlobalTransform: false);
        TopBar.Reparent(barLayer, keepGlobalTransform: false);
        VisibilityChanged += () => barLayer.Visible = Visible;
        foreach (var screen in _fullScreens)
        {
            screen.VisibilityChanged += () =>
            {
                TopBar.Wide = _fullScreens.Any(s => s.Visible);
                EndTurnQuestion.Visible &= !screen.Visible; // a screen opened over it: asked again on the next End turn
                HoverCard.Leave();
            };
        }

        _layout.BottomReserve = StatusBar.Height + StatusBar.BottomOffset; // the bottom parts stand above the status bar
        _layout.Placed += () => ChatPlaceChanged?.Invoke();
        ApplyLayout(layout);
    }

    /// <summary>The card beside the pointer over a map object or a hero (ADR-309).</summary>
    public MapHoverCard HoverCard { get; private set; } = null!;

    /// <summary>Where the LAN chat goes over the map (ADR-309): under the town list, or beside it on a short screen.</summary>
    public Rect2 ChatRect => _layout.ChatRect;

    /// <summary>The HUD's parts were placed again: the chat's place may have moved.</summary>
    public event Action? ChatPlaceChanged;

    /// <summary>A LAN game is paused (ADR-195): the map is dimmed behind the pause card.</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            RefreshDim();
        }
    }

    private bool _paused;
    private ColorRect _dim = null!;
    private DecisionPanel? _decisions;

    /// <summary>The map is dimmed while a question waits (a pending decision's, End turn's) or the LAN game is paused; the hover card goes.</summary>
    private void RefreshDim()
    {
        _dim.Visible = _paused || EndTurnQuestion?.IsVisibleInTree() == true || _decisions?.IsVisibleInTree() == true; // not over the town scene
        if (_dim.Visible)
        {
            HoverCard.Leave();
        }
    }

    /// <summary>The selected hero's card in the bottom row (ADR-309); hidden with no own hero selected.</summary>
    public SelectedHeroCard HeroCard { get; private set; } = null!;

    /// <summary>The selected hero's army beside its card (ADR-309); hidden with it.</summary>
    public ArmyStrip ArmyStrip { get; private set; } = null!;

    /// <summary>End turn's tile (ADR-309); null in the replay viewer.</summary>
    public TurnPanel? EndTurnTile { get; private set; }

    /// <summary>End turn's question while a hero can still move (ADR-309); hidden otherwise.</summary>
    public PanelContainer EndTurnQuestion { get; private set; } = null!;

    private Control _cardFrame = null!;
    private Control _armyFrame = null!;
    private Label _questionText = null!;

    /// <summary>
    /// The bottom row (ADR-309), every part in a frame of one height: the selected hero's card and army strip, the action buttons and End
    /// turn; and End turn's question in the middle of the screen.
    /// </summary>
    private void BuildBottomRow(bool playing, Control popUps)
    {
        _playing = playing;
        HeroCard = new SelectedHeroCard();
        _cardFrame = Framed("HeroCardFrame", HeroCard);
        HeroCard.SizeFlagsVertical = Control.SizeFlags.Fill; // the portrait as tall as the row: the same margin all round
        _layout.SetBottom(HudPart.HeroCard, _cardFrame);
        HeroCard.HeroScreenRequested += hero => ShowHero(hero);
        ArmyStrip = new ArmyStrip();
        _armyFrame = new CutPart("ArmyStripCut", Framed("ArmyStripFrame", ArmyStrip)); // cut with a fade on a narrow screen
        ArmyStrip.SizeFlagsVertical = Control.SizeFlags.Fill; // the slots as tall as the row
        _layout.SetBottom(HudPart.ArmyStrip, _armyFrame);
        ArmyStrip.HeroScreenRequested += hero => ShowHero(hero);
        ArmyStrip.StackInfoRequested += (hero, slot, at) => StackInfoRequested?.Invoke(hero, slot, at);

        _actions = new GridContainer { Columns = AdventureActionGrid.Columns, Name = "Actions" };
        _actions.AddThemeConstantOverride("h_separation", UiSpacing.Cell);
        _actions.AddThemeConstantOverride("v_separation", UiSpacing.Cell);
        _layout.SetBottom(HudPart.Actions, Framed("ActionsFrame", _actions));

        // End turn's question: the heroes that can still move, End turn or Cancel.
        EndTurnQuestion = new PanelContainer { Name = "EndTurnQuestion", Visible = false };
        EndTurnQuestion.UseOpaqueBackground();
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Gap);
        EndTurnQuestion.AddChild(box);
        _questionText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 0) };
        box.AddChild(_questionText);
        var confirm = new Button { Text = "End the turn" }.WithIcon(UiIcon.EndTurn, keepText: true);
        confirm.Pressed += () =>
        {
            EndTurnQuestion.Visible = false;
            EndTurnTile?.EndTurnNow();
        };
        var cancel = new Button { Text = "Cancel" }.WithIcon(UiIcon.Close, keepText: true);
        cancel.Pressed += () => EndTurnQuestion.Visible = false;
        box.AddChild(Screens.ButtonRow(confirm, cancel));
        popUps.AddChild(EndTurnQuestion);
        EndTurnQuestion.VisibilityChanged += RefreshDim;

        if (playing)
        {
            EndTurnTile = new TurnPanel();
            _layout.SetBottom(HudPart.EndTurn, Framed("EndTurnFrame", EndTurnTile));
            EndTurnTile.SizeFlagsVertical = Control.SizeFlags.Fill; // the hourglass fills the row's height
            EndTurnTile.Bind(_session, endTurn: true, mine: () => _context.PlaysHere(_session.State.CurrentPlayer.Id), listed: () => _context.ListedPlayer);
            EndTurnTile.EndTurnPressed += RequestEndTurn;
        }

        RefreshHero();
        foreach (var refresh in new Action<Action>[]
                 {
                     a => this.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => { EndTurnQuestion.Visible = false; a(); }),
                     a => this.SubscribeWhileInTree<HeroMoved>(_session.Events, _ => { EndTurnQuestion.Visible = false; a(); }),
                     a => this.SubscribeWhileInTree<DecisionRequested>(_session.Events, _ => { EndTurnQuestion.Visible = false; a(); }),
                     a => this.SubscribeWhileInTree<HeroHired>(_session.Events, _ => { EndTurnQuestion.Visible = false; a(); }),
                     a => this.SubscribeWhileInTree<ArtifactGained>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<ArtifactLost>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroStatusGained>(_session.Events, _ => a()), // a temple's morale, a fountain's luck
                     a => this.SubscribeWhileInTree<HeroStatusesExpired>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<WitchHutSkillLearned>(_session.Events, _ => a()), // Leadership, Luck
                     a => this.SubscribeWhileInTree<MapEventSkillGained>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<LessonTaken>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroMovementRestored>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroMovementGained>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HoleDug>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<SphinxAnswered>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroDefeated>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<ArmyChanged>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroManaSpent>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroManaRestored>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<AdventureSpellCast>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroLeveledUp>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroExperienceGained>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroPrimaryStatRaised>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<HeroArtifactsChanged>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<ArtifactEquipped>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<ArtifactUnequipped>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<RelicAssembled>(_session.Events, _ => a()),
                     a => this.SubscribeWhileInTree<RelicTakenApart>(_session.Events, _ => a()),
                 })
        {
            refresh(() =>
            {
                RefreshHero();
                RefreshActions();
            });
        }

        _world.PathPreview.Shown += OnRouteChanged;
        _world.PathPreview.Cleared += RefreshActions;
        TreeExiting += () =>
        {
            _world.PathPreview.Shown -= OnRouteChanged;
            _world.PathPreview.Cleared -= RefreshActions;
        };
    }

    private void OnRouteChanged(Core.Domain.Pathfinding.HeroPath _) => RefreshActions();

    /// <summary>A bottom-row part in its frame, as tall as the row (the layout stretches the frame; the part stays centred).</summary>
    private static PanelContainer Framed(string name, Control part)
    {
        var frame = new PanelContainer { Name = name };
        UiFrames.Bevel(frame, BevelKind.Panel, UiFrames.Overlay(), UiSpacing.Padding); // the painted overlay's margins (ADR-310)

        part.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        frame.AddChild(part);
        return frame;
    }

    /// <summary>
    /// Shows the selected hero's card and army (a hero of the listed player: in a LAN game this screen's player's in another's turn), or hides
    /// both with no such hero. Its clicks open the hero screen only in its owner's turn, and never in the replay viewer.
    /// </summary>
    private void RefreshHero()
    {
        var hero = _selected is { } wanted ? _session.State.HeroesOf(_context.ListedPlayer).FirstOrDefault(h => h.Id == wanted) : null;
        _cardFrame.Visible = _armyFrame.Visible = hero is not null;
        if (hero is null)
        {
            HeroCard.Clear();
            return;
        }

        var card = Core.Domain.Heroes.HeroCard.Of(hero);
        var refusal = !_playing ? "Not while watching a replay"
            : hero.Owner != _session.State.CurrentPlayer.Id ? "Not in another player's turn" : null;
        HeroCard.Show(card, refusal);
        ArmyStrip.Show(_session.State, card, refusal);
    }

    /// <summary>
    /// End turn (the hourglass, Z): asks first when a hero can still move ("Mara and Aldric can still move. End the turn?"), else ends the turn.
    /// While the question is open, Z answers it.
    /// </summary>
    public void RequestEndTurn()
    {
        if (EndTurnTile is not { CanEndTurn: true } tile)
        {
            return;
        }

        if (EndTurnQuestion.Visible)
        {
            EndTurnQuestion.Visible = false;
            tile.EndTurnNow();
            return;
        }

        if (EndTurnCheck.Question(_session.State, _session.State.CurrentPlayer.Id) is not { } question)
        {
            tile.EndTurnNow();
            return;
        }

        _questionText.Text = question;
        EndTurnQuestion.Visible = true;
    }

    /// <summary>Enter answers End turn's question while it shows, before a focused button takes the key.</summary>
    public override void _Input(InputEvent @event)
    {
        if (EndTurnQuestion?.IsVisibleInTree() == true && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter }
            && !IsScreenOpen && _session.State.PendingDecision is null && GetViewport().GuiGetFocusOwner() is not (LineEdit or TextEdit))
        {
            GetViewport().SetInputAsHandled();
            RequestEndTurn();
        }
    }

    /// <summary>M, or the Marketplace button: the marketplace, when one of the player's towns has one.</summary>
    public void OpenMarketplace()
    {
        if (_marketButton.Disabled)
        {
            Status.Text = _marketButton.TooltipText;
            return;
        }

        _marketScreen.Open();
    }

    /// <summary>The turn banner at the top centre (ADR-309); null in the replay viewer.</summary>
    public TurnBannerPanel? Banner { get; private set; }

    /// <summary>The minimap's longer side at most, and at least however low the screen.</summary>
    private const float MinimapSide = 320, MinimapLeastSide = 140;

    /// <summary>The gap between the minimap's slot and the map in it: the slot's rim.</summary>
    private const int MinimapInset = 3;

    /// <summary>The minimap's level caption (two-level maps); null on a one-level map.</summary>
    public Button? LevelCaption { get; private set; }

    /// <summary>
    /// The minimap (ADR-309): the map's proportions, towns and heroes as pictograms, the camera's frame on it; under it, on one line, the
    /// level caption ("Surface" or "Underground" with its icon, on two-level maps; a click switches level), then the zoom buttons, which zoom
    /// the camera as the wheel does.
    /// </summary>
    private Control BuildMinimap()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Cell);
        var map = _session.State.Map;
        var minimap = new MinimapWidget
        {
            CellPixels = MinimapSide / Math.Max(map.Width, map.Height), ClipContents = true, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        // Sunk into the column (ADR-310), the map filling the slot up to its rim.
        var framed = new PanelContainer { Name = "MinimapFrame", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        UiFrames.Bevel(framed, BevelKind.Slot, padding: MinimapInset);
        framed.AddChild(minimap);
        box.AddChild(framed);

        // At most a third of the screen's height for the minimap's panel (ADR-316): a lower screen shrinks the map, and the panel with it.
        void Fit()
        {
            var overhead = box.GetCombinedMinimumSize().Y - minimap.CustomMinimumSize.Y + (2 * AdventureHudLayout.Margin);
            var side = Math.Clamp((_layout.Size.Y / 3) - overhead, MinimapLeastSide, MinimapSide);
            var cell = side / Math.Max(map.Width, map.Height);
            if (Math.Abs(cell - minimap.CellPixels) > 0.01f)
            {
                minimap.CellPixels = cell;
            }
        }

        _layout.Resized += Fit;
        box.TreeExiting += () => _layout.Resized -= Fit;
        box.Ready += Fit;
        minimap.Bind(_context);
        minimap.TileClicked += tile => _camera.FocusOn(MapSpace.ToWorld(tile));
        minimap.ViewFrame = ViewFrame;

        var caption = new HBoxContainer();
        caption.AddThemeConstantOverride("separation", UiSpacing.Cell);
        box.AddChild(caption);
        if (map.Levels > 1)
        {
            var level = new Button
            {
                TooltipText = Hint(AdventureAction.SwitchLevel), FocusMode = Control.FocusModeEnum.None, Flat = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left,
            };
            LevelCaption = level;
            void ShowLevel()
            {
                level.Text = MapLevels.Name(_context.Level);
                level.Icon = UiIcons.Get(_context.Level == 0 ? UiIcon.LevelSurface : UiIcon.LevelUnderground, UiIcons.StatusBarSize);
            }

            ShowLevel();
            _context.LevelChanged += ShowLevel;
            level.TreeExiting += () => _context.LevelChanged -= ShowLevel;
            level.Pressed += () => _context.SetLevel((_context.Level + 1) % map.Levels);
            caption.AddChild(level);
        }
        else
        {
            caption.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        }

        var zoomIn = new Button { Text = "Zoom in", TooltipText = "As the mouse wheel does", FocusMode = Control.FocusModeEnum.None }
            .WithIcon(UiIcon.ZoomIn, UiIcons.StatusBarSize);
        UiFrames.Bevel(zoomIn, BevelKind.Button);
        zoomIn.Pressed += () => _camera.Zoom(1);
        caption.AddChild(zoomIn);
        var zoomOut = new Button { Text = "Zoom out", TooltipText = "As the mouse wheel does", FocusMode = Control.FocusModeEnum.None }
            .WithIcon(UiIcon.ZoomOut, UiIcons.StatusBarSize);
        UiFrames.Bevel(zoomOut, BevelKind.Button);
        zoomOut.Pressed += () => _camera.Zoom(-1);
        caption.AddChild(zoomOut);
        return box;
    }

    /// <summary>The ground under the screen's corners, in tiles: the camera's frame on the minimap; null when a corner looks past the ground.</summary>
    private IReadOnlyList<Vector2>? ViewFrame()
    {
        if (!_camera.IsInsideTree() || !_camera.Current)
        {
            return null;
        }

        var screen = _camera.GetViewport().GetVisibleRect().Size;
        Vector2[] corners = [Vector2.Zero, new(screen.X, 0), screen, new(0, screen.Y)];
        var frame = new Vector2[corners.Length];
        for (var i = 0; i < corners.Length; i++)
        {
            if (_camera.GroundAt(corners[i]) is not { } ground)
            {
                return null;
            }

            frame[i] = new Vector2(ground.X, ground.Z) / MapSpace.TileSize;
        }

        return frame;
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
        set
        {
            _layout.Visible = _popUps.Visible = !value;
            HoverCard.Leave();
            RefreshDim();
        }
    }

    /// <summary>
    /// What the pointer is over on the map goes to the status bar (its name as the subject, its description as the detail; null: nothing
    /// there) and, for a map object or a hero, to the hover card at <paramref name="pointer"/>; none while the map is dimmed.
    /// </summary>
    public void ShowHover(MapDescription? description, Vector2 pointer)
    {
        StatusBar.ShowSubject(description?.Name, description?.Status);
        HoverCard.Point(_dim.Visible ? null : description, pointer);
    }

    /// <summary>The pointer moved on the same cell: the hover card goes with it, or comes back unless the map is dimmed.</summary>
    public void FollowHover(Vector2 pointer)
    {
        if (!_dim.Visible)
        {
            HoverCard.Follow(pointer);
        }
    }

    /// <summary>
    /// A list's panel: the HUD's bevelled panel with the painted column's margins (ADR-310), the list inside scrolling with the wheel, with
    /// no bar, when it is taller than the room.
    /// </summary>
    private static PanelContainer ListFrame(string name, Control list)
    {
        var frame = new PanelContainer { Name = name };
        UiFrames.Bevel(frame, BevelKind.Panel, padding: UiSpacing.Padding); // the same padding on every side
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
        };
        frame.AddChild(scroll);
        scroll.AddChild(list);
        return frame;
    }

    /// <summary>Places the parts as <paramref name="layout"/> says (the "Layout" tab changed): the default or its mirror (ADR-309).</summary>
    public void ApplyLayout(LayoutSettings layout)
    {
        _layout.Apply(layout);
        var mirrored = HudArrangement.SideOf(layout, HudPart.Minimap) == ScreenSide.Left;
        TopBar.OnRight = mirrored; // the corner away from the minimap
        HeroList.CardToLeft = mirrored; // the hero list on the right: its card opens towards the middle
    }

    /// <summary>The screens that cover the map and stop short of the top and status bars: town, hero, exchange.</summary>
    private readonly List<Control> _fullScreens = [];

    /// <summary>The 2×5 action buttons (ADR-309).</summary>
    private GridContainer _actions = null!;

    private Button _moveButton = null!;
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
        RefreshHero();
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
    /// The action buttons (ADR-309, ADR-316): two rows of four square icon buttons, in <see cref="AdventureActionGrid.Order"/>: Move, spell
    /// book, kingdom, quests; puzzle map, dig, marketplace, menu. Each one's name and key ("Dig [G]") go to the status bar on
    /// hover. A button whose action cannot be taken now is greyed, with the reason after its name. In the replay viewer the ones that would
    /// issue a command or open a hero's screens stay greyed.
    /// </summary>
    private void BuildActions(bool playing)
    {
        _playing = playing;
        var buttons = new Dictionary<AdventureAction, Button>();
        foreach (var action in AdventureActionGrid.Order)
        {
            buttons[action] = action switch
            {
                AdventureAction.Move => AddAction(action, UiIcon.Move, () => MoveRequested?.Invoke()),
                AdventureAction.SpellBook => AddAction(action, UiIcon.SpellBook, () => OpenSpellBook(_selected)),
                AdventureAction.Kingdom => AddAction(action, UiIcon.Kingdom, () => KingdomScreen.Open(_session.State.CurrentPlayer.Id), "Every source of income and the totals"),
                AdventureAction.Quests => AddAction(action, UiIcon.Quests, () => QuestScreen.Open(_session.State.CurrentPlayer.Id), "The quests your heroes were shown"),
                AdventureAction.PuzzleMap => AddAction(action, UiIcon.PuzzleMap, () => PuzzleScreen.Open(_session.State.CurrentPlayer.Id),
                    "The ground around the Grail, as your obelisks show it"),
                AdventureAction.Dig => AddAction(action, UiIcon.Dig, () => DigRequested?.Invoke()),

                // Trading belongs to the player, not a hero: this button, or a hero visiting a map marketplace (ADR-067).
                AdventureAction.Marketplace => AddAction(action, UiIcon.Marketplace, OpenMarketplace),
                AdventureAction.Menu => AddAction(action, UiIcon.Menu, () => MenuRequested?.Invoke(), "Save, load, settings"),
                _ => throw new InvalidOperationException($"No button for {action}"),
            };
        }

        _moveButton = buttons[AdventureAction.Move];
        _bookButton = buttons[AdventureAction.SpellBook];
        _digButton = buttons[AdventureAction.Dig];
        _marketButton = buttons[AdventureAction.Marketplace];
        RefreshActions();
        this.SubscribeWhileInTree<TownBuilt>(_session.Events, _ => RefreshActions());
        this.SubscribeWhileInTree<TownOwnerChanged>(_session.Events, _ => RefreshActions());
        this.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => RefreshActions());
        this.SubscribeWhileInTree<SpellBookBought>(_session.Events, _ => RefreshActions());
        this.SubscribeWhileInTree<HeroDefeated>(_session.Events, _ => RefreshActions());
    }

    /// <summary>An action's name and key from the hotkey table (ADR-309): "Dig [G]".</summary>
    private static string Hint(AdventureAction action) => AdventureHotkeys.Of(action).Hint;

    /// <summary>An action button's icon side; the button is a square round it.</summary>
    private const int ActionIconSize = 36;

    private Button AddAction(AdventureAction action, UiIcon icon, Action pressed, string tooltip = "")
    {
        var button = new Button { Text = Hint(action), TooltipText = tooltip, FocusMode = Control.FocusModeEnum.None }.WithIcon(icon, ActionIconSize);
        var side = ActionIconSize + (2 * UiIcons.IconPadding);
        button.CustomMinimumSize = new Vector2(side, side); // square (ADR-310)
        button.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        UiFrames.Bevel(button, BevelKind.Button, padding: UiIcons.IconPadding); // the theme's wider margins would stretch it
        button.Pressed += pressed;
        _actions.AddChild(button);
        return button;
    }

    /// <summary>Enables the buttons that depend on the selected hero, the towns or the replay viewer, and says why the others are not.</summary>
    private void RefreshActions()
    {
        var hero = OwnHero(_selected);
        var noHero = hero is null ? "Select a hero first" : null;
        Enable(_moveButton, _playing ? AdventureActionGrid.MoveRefusal(hero, _world.PathPreview.Path) : "Not while watching a replay",
            "The selected hero walks the route shown on the map");
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

    /// <summary>
/// The status line tells of the game's flow: the deciding battle's day (ADR-097, ADR-366), defeats, a lost last town's grace period, and a human's lost
/// town or hero (ADR-317).
/// </summary>
    public void ReportGameFlow()
    {
        // The deciding battle's day (an arena's day 2, a tournament's day after the last; ADR-097, ADR-366): the battle opens by itself; the
        // status line says who meets whom.
        this.SubscribeWhileInTree<ChampionsChosen>(_session.Events, _ =>
            Status.Text = $"Day {_session.State.Date.AbsoluteDay}: the {DecidingBattles.Name(_session.State.Rules)}. {_session.State.DecidingBattle?.Summary}");

        this.SubscribeWhileInTree<PlayerDefeated>(_session.Events, e =>
            Status.Text = $"{_session.State.GetPlayer(e.Player).Name} is defeated: {e.Reason}");
        this.SubscribeWhileInTree<GracePeriodStarted>(_session.Events, e =>
            Status.Text = $"{_session.State.GetPlayer(e.Player).Name} lost its last town and is defeated on {e.DefeatDay} unless it owns one again.");
        this.SubscribeWhileInTree<GracePeriodEnded>(_session.Events, e =>
            Status.Text = $"{_session.State.GetPlayer(e.Player).Name} owns a town again.");

        // A human's lost town or hero is a status line, not a card (ADR-317).
        this.SubscribeWhileInTree<TownOwnerChanged>(_session.Events, e =>
        {
            if (e.Previous is { } previous && _session.State.GetPlayer(previous).IsHuman && _session.State.FindObject(e.Town) is Town town)
            {
                Status.Text = $"{_session.State.GetPlayer(previous).Name} lost {town.Name}"
                              + (e.Current is { } taker ? $" to {_session.State.GetPlayer(taker).Name}." : ".");
            }
        });
        this.SubscribeWhileInTree<HeroDefeated>(_session.Events, e =>
        {
            if (_session.State.GetPlayer(e.Owner).IsHuman)
            {
                var name = _session.State.Heroes.FirstOrDefault(h => h.Id == e.Hero)?.Name ?? "A hero";
                Status.Text = $"{_session.State.GetPlayer(e.Owner).Name}: {name} {(e.Retreated ? "retreated" : "was defeated")}.";
            }
        });
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
        _fullScreens.AddRange([TownScreen, HeroScreen]);
        // Two own heroes met: the exchange screen covers the map like the hero screen (ADR-153).
        if (playing)
        {
            var exchange = new ExchangePanel(); // it opens itself when two own heroes meet; not in the replay viewer
            townLayer.AddChild(exchange);
            exchange.Bind(_session);
            _fullScreens.Add(exchange);
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
