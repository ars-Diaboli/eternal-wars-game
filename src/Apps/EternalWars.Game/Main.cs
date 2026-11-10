using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Settings;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Battle;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// Game entry point: the main menu (ADR-101) and the new-game screen, then a <see cref="GameSession"/> wired to the 3D world view and the
/// HUD. This class only composes and maps input to commands; all rules live in core.
/// </summary>
/// <remarks>
/// Run with <c>-- --new-game</c> to open on the new-game screen. Mouse: click an own hero to select it, click a tile to preview the route (markers coloured by day), click the
/// same tile again to go; right-click clears the preview and opens the information window of what is there (ADR-154). Keys: arrows/numpad step, Tab next hero, T next town (the town screen; Escape closes it), L switches
/// level, WASD/QE/wheel camera. Run with <c>-- --map=&lt;path&gt;</c> to offer an editor map first.
/// Battles open the 3D battle scene (<see cref="BattleView3D"/>): point at a cell to preview, click to move, attack (from
/// the side the pointer is on) or shoot; W wait, D defend, R retreat, Q quick combat, wheel zoom; Enter or Escape leave
/// the result screen.
/// </remarks>
public partial class Main : Node3D
{
    private const string EditorMapsFolder = "Eternal Wars Map Editor/maps";

    /// <summary>Where the editor's template editor saves (its <c>user://templates</c>, ADR-070).</summary>
    private const string EditorTemplatesFolder = "Eternal Wars Map Editor/templates";

    private const string SavesFolder = "user://saves";

    private const string ReplaysFolder = "user://replays";

    /// <summary>The maps that ship with the game (by name); every other hand-made map is "custom" in the ranking (ADR-101).</summary>
    private static readonly HashSet<string> ShippedMaps = ["Demo Valley", "Two Towns", EmberMarchScenario.Name];

    private GameSession _session = null!;
    private Node3D? _game;
    private MapDocument _initialMap = null!;

    /// <summary>How the map of the game being played was generated (null for a hand-made map); saves keep it.</summary>
    private GenerationInfo? _generated;

    /// <summary>The last map the new-game screen had generated and how; used when that is the state the game starts with.</summary>
    private (GameState State, GenerationInfo Info)? _lastGenerated;
    private GameMenuPanel _menu = null!;

    /// <summary>The "Menu (F10)" button; hidden on the screens before a game, which have their own buttons (F10 still opens the menu).</summary>
    private Button _menuButton = null!;

    /// <summary>The layer of the main menu, the new-game screen and the leaderboard, over the <see cref="MenuBackground"/>; null during a game.</summary>
    private CanvasLayer? _setup;

    /// <summary>What Escape does on the screen shown in <see cref="_setup"/>.</summary>
    private Action? _setupBack;
    private string _saveFolder = "";
    private bool _turnBegan;
    private AdventureAi? _ai;
    private MapViewContext _context = null!;
    private AdventureWorldView _world = null!;
    private StrategyCamera _camera = null!;
    private HeroListPanel _heroList = null!;
    private TownListPanel _townList = null!;
    private TownPanel _townScreen = null!;
    private HeroPanel _heroScreen = null!;
    private DwellingPanel _dwellingScreen = null!;
    private MarketplacePanel _marketScreen = null!;
    private KingdomOverviewPanel _kingdomScreen = null!;
    private QuestLogPanel _questScreen = null!;
    private PuzzleMapPanel _puzzleScreen = null!;
    private ObjectServicePanel _serviceScreen = null!;
    private ThievesGuildPanel _guildScreen = null!;
    private AdventureIntelPanel _intelScreen = null!;
    private Label _status = null!;
    private CanvasLayer _hud = null!;
    private BattleView3D _battle = null!;
    private BattleHud _battleHud = null!;
    private AutoCombatPanel? _autoCombat;
    private TurnTimer? _timer;
    private TurnTimerPanel? _timerPanel;
    private GameResultPanel _result = null!;

    /// <summary>The session last put into the ranking, so a game is entered once.</summary>
    private GameSession? _rankedSession;
    private HeroId? _selectedHero;
    private string _settingsPath = "";
    private SpeedSettings _speeds = SpeedSettings.Default;
    private InterfaceSettings _interface = InterfaceSettings.Default;
    private LayoutSettings _layout = LayoutSettings.Default;

    /// <summary>Places the adventure map's HUD parts as the layout says (ADR-156); null before the first game.</summary>
    private AdventureHudLayout? _hudLayout;

    /// <summary>The right-click information window (ADR-154), above every screen, for the whole run.</summary>
    private readonly InfoWindow _info = new();

    /// <summary>The computer hero the camera last followed (ADR-090).</summary>
    private HeroId? _followed;

    /// <summary>A human player's turn that started while computer moves were still shown: the map turns to them once they are.</summary>
    private PlayerId? _turnToShow;

    public override void _Ready()
    {
        UiSpacing.Apply(); // the shared margins and gaps (ADR-163)
        if (AutoInput.FromLaunchArguments() is { } autoInput)
        {
            AddChild(autoInput);
        }

        _saveFolder = ProjectSettings.GlobalizePath(SavesFolder);
        BuildMenu();
        if (LaunchArguments.ReplayPath is { } replay)
        {
            ShowReplays();
            Watch(replay, null);
        }
        else if (LaunchArguments.NewGame)
        {
            ShowNewGame();
        }
        else
        {
            ShowMainMenu();
        }
    }

    // The main menu and the screens before a game (ADR-101, UI-2) ---------------------------------------

    /// <summary>The main menu; a game being played is left (its autosave stays).</summary>
    private void ShowMainMenu()
    {
        LeaveGame();
        var menu = new MainMenuScreen();
        ShowSetup(menu, back: null);
        var latest = GameSaves.List(_saveFolder).FirstOrDefault(e => e.Info is not null);
        menu.ShowContinue(latest?.Info is { } info ? $"{SaveTitle(latest.Path, info)}, {info.Map}, {info.Date} ({LocalTime(info.SavedAt)})" : null);
        menu.ContinueRequested += () =>
        {
            // The menu shows why, if the save does not load; a load closes it.
            _menu.Open();
            Load(latest!.Path);
        };
        menu.NewGameRequested += ShowNewGame;
        menu.LoadRequested += _menu.Open;
        menu.WatchRequested += ShowReplays;
        menu.LeaderboardRequested += ShowLeaderboard;
        menu.QuitRequested += () => GetTree().Quit();
    }

    private void ShowNewGame()
    {
        var setup = new NewGameScreen();
        ShowSetup(setup, back: ShowMainMenu);
        setup.Bind(
            Scenarios(), RandomMaps(), town => StartSlots.Heroes(GameContent.Heroes, town), town => StartSlots.Tier1Dwellings(GameContent.Creatures, town));
        setup.BackRequested += ShowMainMenu;
        setup.Started += state =>
        {
            CloseSetup();
            _generated = _lastGenerated is { } last && ReferenceEquals(last.State, state) ? last.Info : null;
            StartGame(state);
        };
    }

    private void ShowLeaderboard()
    {
        var board = new LeaderboardPanel();
        ShowSetup(board, back: ShowMainMenu);
        var ranking = RankingFile.Load(Path.Combine(ProjectSettings.GlobalizePath("user://"), RankingFile.FileName));
        var none = new HashSet<int>();
        board.Bind([RankingViewOf(RankingTable.Maps, ranking.Maps, none), RankingViewOf(RankingTable.RandomMaps, ranking.RandomMaps, none)]);
        board.BackRequested += ShowMainMenu;
    }

    /// <summary>Puts a screen in the middle of the setup layer in place of the one shown; Escape does <paramref name="back"/> (none on the main menu).</summary>
    private void ShowSetup(Control screen, Action? back)
    {
        if (_setup is null)
        {
            _setup = new CanvasLayer { Name = "Setup" };
            AddChild(_setup);
            _setup.AddChild(new MenuBackground());
        }

        foreach (var child in _setup.GetChildren())
        {
            if (child is not MenuBackground)
            {
                child.QueueFree();
            }
        }

        if (screen is MainMenuScreen)
        {
            _setup.AddChild(screen); // places itself on the left
        }
        else
        {
            var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
            _setup.AddChild(center);
            center.AddChild(screen);
        }

        _setupBack = back;
        _menuButton.Visible = false;
    }

    private void CloseSetup()
    {
        _setup?.QueueFree();
        _setup = null;
        _setupBack = null;
        _menuButton.Visible = true;
    }

    /// <summary>Takes the game being played down: its world, HUD and screens. Nothing runs until a new game or a load builds them again.</summary>
    private void LeaveGame()
    {
        _menu.Close();
        _menu.CanSave = false;
        StopWatching();
        if (_game is null)
        {
            return;
        }

        _game.QueueFree();
        _game = null;
        _context = null!;
        _status = null!;
        _ai = null;
        _timer = null;
        _timerPanel = null;
        _autoCombat = null;
        _selectedHero = null;
        _turnToShow = null;
        _followed = null;
    }

    /// <summary>
    /// Computer players play their turns here, one a frame (ADR-059). The AI waits while a battle scene is open (it may be
    /// a human's fight) and while the menu is. The turn timer's clock runs here too (ADR-091); it ends a turn only once the battle scene,
    /// the auto combat question and the menu are closed. While computer heroes' moves are being shown (ADR-090), the AI, the clock and the
    /// next human turn wait for them.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_mode != GameMode.Play)
        {
            ProcessReplay(delta); // the replay viewer runs the record, not the AI or the clock (ADR-164)
            return;
        }

        var showing = ShowingComputerMoves;
        if (_timer is not null && _timerPanel is not null)
        {
            if (_timer.Tick(showing ? 0 : delta, canEnd: !showing && !_menu.Visible && !_battle.IsOpen && _autoCombat?.Visible != true) is { } warning)
            {
                _timerPanel.Warn(warning);
            }

            _timerPanel.ShowTime(_timer.SecondsLeft, _timer.IsStopped, _timer.IsUp, delta);
        }

        if (showing)
        {
            return;
        }

        if (_turnToShow is { } player)
        {
            _turnToShow = null;
            ShowTurn(player);
        }

        if (_ai is not null && !_menu.Visible && !_battle.IsOpen && _autoCombat?.Visible != true && AdventureAi.IsComputerTurn(_session.State))
        {
            _ai.PlayTurn();
        }
    }

    /// <summary>Hot-seat: the map is drawn through the eyes of whoever plays now, with their first hero selected.</summary>
    private void ShowTurn(PlayerId player)
    {
        if (_session.State.CurrentPlayer.Id != player)
        {
            return; // the turn has passed on meanwhile
        }

        _context.SetViewer(player);
        Select(_session.State.HeroesOf(player).FirstOrDefault()?.Id, focus: true);
        FocusOnEventPlace(_session.State.PendingDecision);
    }

    /// <summary>
    /// A turn-start message or question of a map event moves the camera to what it concerns (EVT-2 f): a town, a revealed area, a hero given.
    /// Not while the computer's moves are still shown; the turn's showing comes back to it.
    /// </summary>
    private void FocusOnEventPlace(PendingDecision? decision)
    {
        if (decision is MapEventDecision { Place: { } place } && !ShowingComputerMoves && _session.State.CurrentPlayer.IsHuman)
        {
            _context.SetLevel(place.Level);
            _camera.FocusOn(MapSpace.ToWorld(place));
        }
    }

    /// <summary>A deployment drag ends where the button is let go, even over the HUD (which would otherwise take the release).</summary>
    public override void _Input(InputEvent @event)
    {
        if (_deployDrag is not null && @event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release)
        {
            DropDeploy(release.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } hotkey && HandleSaveKey(hotkey.Keycode))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _setupBack is { } back && !_menu.Visible)
        {
            GetViewport().SetInputAsHandled();
            back();
            return;
        }

        if (_mode != GameMode.Play && _context is not null && !_menu.Visible && HandleReplayInput(@event))
        {
            return;
        }

        if (_context is null || _menu.Visible || _autoCombat?.Visible == true || ShowingComputerMoves || _turnToShow is not null)
        {
            return;
        }

        if (_battle.IsOpen)
        {
            HandleBattleInput(@event);
            return;
        }

        if (_questScreen.Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q })
        {
            _questScreen.Visible = false;
            _camera.IgnoreHeldKeys(); // Q also turns the camera; this press only closes the log
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_puzzleScreen.Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.P })
        {
            _puzzleScreen.Visible = false;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_townScreen.Visible || _heroScreen.Visible || _dwellingScreen.Visible || _marketScreen.Visible || _serviceScreen.Visible || _kingdomScreen.Visible
            || _questScreen.Visible || _puzzleScreen.Visible)
        {
            return; // a full-screen panel takes the input (Escape closes it)
        }

        if (_session.State.PendingDecision is not null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                if (MapSpace.PickTile(_camera, click.Position, _context.Level) is { } tile && _session.State.Map.Contains(tile))
                {
                    ClickTile(tile);
                }

                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } rightClick:
                _world.PathPreview.Clear();
                _aimedSpell = null;
                if (MapSpace.PickTile(_camera, rightClick.Position, _context.Level) is { } pointed
                    && Core.Domain.Info.InfoSheets.MapCell(_session.State, _session.State.CurrentPlayer.Id, pointed, _info.Detailed) is { } sheet)
                {
                    _info.Open(sheet, rightClick.Position);
                }

                break;
            case InputEventKey { Pressed: true, Keycode: Key.Tab }:
                SelectNextHero();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.T }:
                OpenNextTown();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.H }:
                ShowHero(_selectedHero);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.K } when _session.State.CurrentPlayer.IsHuman:
                _kingdomScreen.Open(_session.State.CurrentPlayer.Id);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q } when _session.State.CurrentPlayer.IsHuman:
                _questScreen.Open(_session.State.CurrentPlayer.Id);
                _camera.IgnoreHeldKeys(); // Q also turns the camera; this press only opens the log
                break;
            case InputEventKey { Pressed: true, Keycode: Key.L }:
                _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels);
                _camera.FocusOn(MapSpace.ToWorld(new MapPosition(_session.State.Map.Width / 2, _session.State.Map.Height / 2, _context.Level)));
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.P } when _session.State.CurrentPlayer.IsHuman:
                _puzzleScreen.Open(_session.State.CurrentPlayer.Id);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.G } when _session.State.CurrentPlayer.IsHuman:
                Dig();
                break;
            case InputEventKey { Pressed: true } key when _selectedHero is { } hero && KeyboardDirections.ToDirection(key.Keycode) is { } direction:
                _world.PathPreview.Clear();
                Report(_session.Execute(new StepHeroCommand(_session.State.CurrentPlayer.Id, hero, direction)));
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void KeepSettings()
    {
        try
        {
            SettingsFile.Save(_settingsPath, _speeds, _interface, _layout);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _menu.SetMessage($"Could not keep the settings: {e.Message}");
        }
    }

    private void StartGame(GameState state)
    {
        // The map as it is now is the start of the game: saves replay from it (ADR-058).
        var initial = MapConverter.ToDocument(state);
        StopWatching();
        BeginGame(new GameSession(state), initial, resume: false);
    }

    /// <summary>
    /// Builds the world and the HUD for a session. A new game is started here; a loaded one (<paramref name="resume"/>)
    /// is already running, so the views are built from its state and a battle in progress is shown as it stands.
    /// </summary>
    private void BeginGame(GameSession session, MapDocument initialMap, bool resume)
    {
        _game?.QueueFree();
        _game = new Node3D { Name = "Game" };
        AddChild(_game);

        _session = session;
        _initialMap = initialMap;
        _selectedHero = null;
        var playing = _mode == GameMode.Play;
        _ai = playing ? new AdventureAi(session) : null;
        _menu.CanSave = playing;
        var state = session.State;
        _context = new MapViewContext(state, _session.Events, playing ? state.CurrentPlayer.Id : ReplayViewer());

        _world = new AdventureWorldView { Name = "World" };
        _game.AddChild(_world);
        _world.Bind(_context);

        _game.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0), ShadowEnabled = true });
        _camera = new StrategyCamera { Name = "Camera" };
        _game.AddChild(_camera);

        // A game begun from inside another ("Play again", a load) would otherwise keep the old game's camera, freed at the end of the frame.
        _camera.MakeCurrent();

        BuildHud();
        BuildBattle();
        if (playing)
        {
            BuildResult();
            BuildTimer();
        }
        else
        {
            (_timer, _timerPanel) = (null, null);
            BuildReplayBar(); // the viewer's controls; no ranking, end screen, clock or autosave (ADR-164)
        }

        _turnToShow = null;
        _followed = null;
        ApplySpeeds();

        // Autosave once the command that began a turn is logged and settled (ADR-058).
        _turnBegan = false;
        if (playing)
        {
            _game.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => _turnBegan = true);
            _game.SubscribeWhileInTree<CommandExecuted>(_session.Events, _ =>
            {
                if (_turnBegan)
                {
                    Autosave();
                }
            });
        }

        _game.SubscribeWhileInTree<TurnStarted>(_session.Events, e =>
        {
            CloseScreens(); // the turn may have ended on the timer with a screen open
            _followed = null;
            if (!playing)
            {
                ReplayTurnStarted(); // the viewer keeps its own eyes on the map
                return;
            }

            if (!_session.State.GetPlayer(e.Player).IsHuman)
            {
                return; // the map stays with the last human, who watches the computer heroes it can see (ADR-090)
            }

            if (ShowingComputerMoves)
            {
                _turnToShow = e.Player; // once the computer's moves have been shown
                return;
            }

            ShowTurn(e.Player);
        });
        _game.SubscribeWhileInTree<DecisionRequested>(_session.Events, e => FocusOnEventPlace(e.Decision));
        _context.HeroStepShown += FollowStep;
        _game.SubscribeWhileInTree<HeroDefeated>(_session.Events, e =>
        {
            if (e.Hero == _selectedHero)
            {
                Select(_session.State.HeroesOf(_session.State.CurrentPlayer.Id).FirstOrDefault()?.Id, focus: false);
            }
        });
        _game.SubscribeWhileInTree<HeroTeleported>(_session.Events, e =>
        {
            if (e.Hero == _selectedHero && e.To.Level != _context.Level)
            {
                _context.SetLevel(e.To.Level);
                _camera.FocusOn(MapSpace.ToWorld(e.To));
            }
        });

        if (resume)
        {
            Select(state.HeroesOf(state.CurrentPlayer.Id).FirstOrDefault()?.Id, focus: true);
            _battle.Restore();
            if (playing)
            {
                ShowResult();
            }
        }
        else
        {
            _session.Start();
            Autosave();
        }
    }

    /// <summary>
    /// The turn timer (ADR-091), when the game has one: the clock over the map and the battle scene. A loaded game starts the current turn with
    /// the full time.
    /// </summary>
    private void BuildTimer()
    {
        _timer = _session.State.Options.TurnTimerMinutes is null ? null : new TurnTimer(_session);
        var layer = new CanvasLayer { Name = "TurnTimer", Layer = 4 };
        _game!.AddChild(layer);
        _timerPanel = new TurnTimerPanel();
        layer.AddChild(_timerPanel);
        _timerPanel.Bind();
    }

    /// <summary>Closes the screens over the map (town, hero, dwelling, market, services, kingdom, quest log, Thieves' Guild).</summary>
    private void CloseScreens()
    {
        _townScreen.Close();
        _heroScreen.Close();
        _dwellingScreen.Close();
        _marketScreen.Close();
        _serviceScreen.Close();
        _kingdomScreen.Hide();
        _questScreen.Hide();
        _puzzleScreen.Hide();
        _guildScreen.Hide();
    }

    /// <summary>The selected hero digs where it stands (ADR-140): a whole day's movement, for the Grail.</summary>
    private void Dig()
    {
        if (_selectedHero is not { } hero)
        {
            _status.Text = "Select a hero to dig.";
            return;
        }

        _world.PathPreview.Clear();
        Report(_session.Execute(new DigCommand(_session.State.CurrentPlayer.Id, hero)));
    }

    /// <summary>The start of a turn goes into the two autosave slots: this turn's and the previous turn's.</summary>
    private void Autosave()
    {
        _turnBegan = false;
        try
        {
            GameSaves.Autosave(_saveFolder, _initialMap, _session, _generated);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Autosave failed: {e.Message}";
        }
    }

    // Save and load (ADR-058) ---------------------------------------------------------------------

    /// <summary>The menu is above everything, also above the battle scene and its HUD, so saving works in battle too.</summary>
    private void BuildMenu()
    {
        var layer = new CanvasLayer { Name = "Menu", Layer = 5 };
        AddChild(layer);
        _menu = new GameMenuPanel();
        layer.AddChild(_menu);
        _menu.Bind(() => SaveItems(GameSaves.List(_saveFolder)), GameSaves.Delete);
        _menu.SaveRequested += name => Save(name);
        _menu.LoadRequested += Load;
        _menu.MainMenuRequested += ShowMainMenu;
        _menu.Opened += () => _menu.ShowAutoCombat(
            AutoCombatSettingPlayer() is { } player ? _session.State.GetPlayer(player).Name : null,
            AutoCombatSettingPlayer() is { } shown ? (int)_session.State.GetPlayer(shown).AutoCombat : 0);
        _menu.AutoCombatSelected += mode =>
        {
            if (AutoCombatSettingPlayer() is { } player)
            {
                var result = _session.Execute(new SetAutoCombatCommand(player, (AutoCombatMode)mode));
                _menu.SetMessage(result.IsAccepted ? $"Auto combat: {(AutoCombatMode)mode}." : $"Refused: {result.Error}");
            }
        };

        // The speed settings (ADR-090, UI-1) live in the settings file, not in the game.
        _settingsPath = LaunchArguments.SettingsPath ?? Path.Combine(ProjectSettings.GlobalizePath("user://"), SettingsFile.FileName);
        _speeds = SettingsFile.Load(_settingsPath);
        _menu.ShowSpeeds(_speeds);
        _menu.SpeedsChanged += speeds =>
        {
            _speeds = speeds;
            ApplySpeeds();
            KeepSettings();
        };

        // "Detailed tooltips" (ADR-154): the information windows' formulas and numbers, kept with the speeds.
        _interface = SettingsFile.LoadInterface(_settingsPath);
        AddChild(_info);
        _info.Detailed = _interface.DetailedTooltips;
        _menu.ShowInterface(_interface);
        _menu.InterfaceChanged += settings =>
        {
            _interface = settings;
            _info.Detailed = settings.DetailedTooltips;
            KeepSettings();
        };

        // The layout (ADR-156): applied at once to the map's HUD and the battle's, kept with the other settings.
        _layout = SettingsFile.LoadLayout(_settingsPath);
        _menu.ShowLayout(_layout);
        _menu.LayoutChanged += layout =>
        {
            _layout = layout;
            if (IsInstanceValid(_hudLayout))
            {
                _hudLayout!.Apply(layout);
            }

            if (IsInstanceValid(_battleHud))
            {
                _battleHud.Layout = layout;
            }

            KeepSettings();
        };

        _menuButton = new Button
        {
            Text = "Menu (F10)", AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = -132 - UiSpacing.ScreenEdge, OffsetTop = -28 - UiSpacing.ScreenEdge, OffsetRight = -UiSpacing.ScreenEdge, OffsetBottom = -UiSpacing.ScreenEdge,
        }.WithIcon(UiIcon.Settings);
        _menuButton.Pressed += _menu.Toggle;
        layer.AddChild(_menuButton);
    }

    private void ApplySpeeds()
    {
        if (_context is not null)
        {
            // The replay viewer plays every hero and battle at its own speed (ADR-164).
            _context.Speeds = _mode == GameMode.Play ? _speeds : new SpeedSettings(_replaySpeed, _replaySpeed, ShowComputerHeroes: true, _replaySpeed);
            _battle.Speed = _context.Speeds.Battle;
        }
    }

    /// <summary>The save menu's and the replay list's lines for a folder's files.</summary>
    private static List<SaveItem> SaveItems(IReadOnlyList<SaveEntry> entries) => entries
        .Select(e => e.Info is { } info
            ? new SaveItem(e.Path, info.Date.Length == 0
                ? $"{info.Name}   {info.Map}   ({LocalTime(info.SavedAt)})" // a replay: no save label
                : $"{SaveTitle(e.Path, info)}   {info.Map}, {info.Date}, {info.Player}'s turn{(info.InBattle ? ", in battle" : string.Empty)}   ({LocalTime(info.SavedAt)})",
                GameSaves.SlotOf(e.Path) is null ? info.Name : null, true)
            : new SaveItem(e.Path, $"{Path.GetFileName(e.Path)}   unreadable: {e.Error}", null, false))
        .ToList();

    /// <summary>
    /// Follows a computer hero with the camera when it starts walking where the player can see it (ADR-090), unless computer heroes are not
    /// shown.
    /// </summary>
    private void FollowStep(Core.Domain.Heroes.Hero hero, MapPosition to)
    {
        var watching = _mode == GameMode.Watch; // the viewer follows every hero it can see, on either level (ADR-164)
        if ((watching || (_speeds.ShowComputerHeroes && !_session.State.GetPlayer(hero.Owner).IsHuman)) && hero.Id != _followed
            && (watching || to.Level == _context.Level) && _context.IsInSight(to))
        {
            _followed = hero.Id;
            _context.SetLevel(to.Level);
            _camera.FocusOn(MapSpace.ToWorld(to));
        }
    }

    /// <summary>Whether computer heroes' moves are still being shown: the next turn, the AI and the turn clock wait for them.</summary>
    private bool ShowingComputerMoves => _context is not null && _context.IsShowingComputer;

    /// <summary>
    /// Whose auto combat setting the menu shows (ADR-090): the human in the battle against the computer, else the current player if human; null
    /// before a game or on a computer's turn.
    /// </summary>
    private PlayerId? AutoCombatSettingPlayer() =>
        _context is null || _mode != GameMode.Play ? null
        : _session.Battles.AutoCombatPlayer() ?? (_session.State.CurrentPlayer.IsHuman ? _session.State.CurrentPlayer.Id : null);

    private static string SaveTitle(string path, SaveInfo info) => GameSaves.SlotOf(path) switch
    {
        AutosaveSlot.Current => "Autosave: start of the latest turn",
        AutosaveSlot.Previous => "Autosave: start of the turn before",
        _ => info.Name,
    };

    private static string LocalTime(string iso) =>
        DateTimeOffset.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time)
            ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : iso;

    /// <summary>F10 menu, F5 quick save, F9 quick load; they work on the adventure map and in battle.</summary>
    private bool HandleSaveKey(Key key)
    {
        switch (key)
        {
            case Key.F10:
                _menu.Toggle();
                return true;
            case Key.F5 when _context is not null && _mode == GameMode.Play:
                Save(GameSaves.QuickSaveName);
                return true;
            case Key.F9:
                if (File.Exists(GameSaves.PathFor(_saveFolder, GameSaves.QuickSaveName)))
                {
                    Load(GameSaves.PathFor(_saveFolder, GameSaves.QuickSaveName));
                }
                else
                {
                    _status?.SetText("There is no quick save.");
                }

                return true;
            default:
                return false;
        }
    }

    private void Save(string name)
    {
        try
        {
            GameSaves.Save(_saveFolder, name, _initialMap, _session, _generated);
            _menu.SetMessage($"Saved '{name}'.");
            _status.Text = $"Saved '{name}'.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _menu.SetMessage($"Could not save: {e.Message}");
        }
    }

    private void Load(string path)
    {
        try
        {
            var (session, record) = GameSaves.Load(
                path, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings, artifacts: GameContent.Artifacts);
            CloseSetup();
            _menu.Close();
            _generated = record.Generated;
            StopWatching();
            BeginGame(session, record.Map, resume: true);
            _status.Text = $"Loaded '{record.Save?.Name ?? Path.GetFileNameWithoutExtension(path)}'." +
                (record.Generated is { } generated ? $" {generated.Template} {generated.Size}×{generated.Size}, seed {generated.Seed}. {generated.VersionWarning}" : "");
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or System.Text.Json.JsonException or IOException or KeyNotFoundException)
        {
            _menu.SetMessage($"Could not load: {e.Message}");
        }
    }

    /// <summary>The hero and the aimed adventure spell waiting for its cell (Dimension Door, Distant Vision, Eavesdrop, Town Portal II): the next map click casts it there.</summary>
    private (HeroId Hero, string Spell)? _aimedSpell;

    private void ClickTile(MapPosition tile)
    {
        var state = _session.State;
        var player = state.CurrentPlayer.Id;
        if (_aimedSpell is { } aimed)
        {
            _aimedSpell = null;
            _world.PathPreview.Clear();
            Report(_session.Execute(new CastAdventureSpellCommand(player, aimed.Hero, aimed.Spell, tile)));
            return;
        }

        if (state.HeroAt(tile) is { } clicked && clicked.Owner == player)
        {
            Select(clicked.Id, focus: false);
            return;
        }

        if (_selectedHero is not { } hero)
        {
            return;
        }

        // Second click on the previewed destination: go.
        if (_world.PathPreview.Path is { } planned && planned.Hero == hero && planned.Destination == tile)
        {
            _world.PathPreview.Clear();
            Report(_session.Execute(new MoveHeroCommand(player, hero, tile)));
            return;
        }

        if (_session.Movement.FindPath(hero, tile) is { } path)
        {
            _world.PathPreview.Show(path);
            _status.Text = $"{path.Steps.Count} steps, {path.Turns} day(s). Click again to go.";
        }
        else
        {
            _world.PathPreview.Clear();
            _status.Text = "No route there.";
        }
    }

    private void Select(HeroId? hero, bool focus)
    {
        _selectedHero = hero;
        _world.Select(hero);
        _world.PathPreview.Clear();
        _heroList.ShowSelected(hero);
        if (hero is { } id && focus)
        {
            var position = _session.State.GetHero(id).Position;
            _context.SetLevel(position.Level);
            _camera.FocusOn(MapSpace.ToWorld(position));
        }
    }

    /// <summary>The town screen of a town: the map centres on it first.</summary>
    private void ShowTown(MapObjectId id)
    {
        if (_session.State.FindObject(id) is not Core.Domain.Towns.Town town || town.Owner != _session.State.CurrentPlayer.Id)
        {
            return;
        }

        _context.SetLevel(town.VisitPosition.Level);
        _camera.FocusOn(MapSpace.ToWorld(town.VisitPosition));
        _world.PathPreview.Clear();
        _townScreen.Open(id);
    }

    /// <summary>The hero screen of an own hero (H: the selected one).</summary>
    private void ShowHero(HeroId? id)
    {
        if (id is not { } hero || _session.State.HeroesOf(_session.State.CurrentPlayer.Id).All(h => h.Id != hero))
        {
            _status.Text = "Select a hero first.";
            return;
        }

        _heroScreen.Open(hero);
    }

    /// <summary>T: the town screen of the next own town.</summary>
    private void OpenNextTown()
    {
        var towns = TownListPanel.TownsOf(_session.State);
        if (towns.Count == 0)
        {
            _status.Text = "You have no town.";
            return;
        }

        var open = _townScreen.CurrentTown;
        var index = open is null ? -1 : towns.ToList().FindIndex(t => t.Id == open);
        ShowTown(towns[(index + 1) % towns.Count].Id);
    }

    private void SelectNextHero()
    {
        var heroes = _session.State.HeroesOf(_session.State.CurrentPlayer.Id).Select(h => h.Id).ToList();
        if (heroes.Count > 0)
        {
            var next = _selectedHero is { } current ? heroes[(heroes.IndexOf(current) + 1) % heroes.Count] : heroes[0];
            Select(next, focus: true);
        }
    }

    private void Report(CommandResult result) =>
        _status.Text = result.IsAccepted ? Describe(result.Outcome) : $"Refused: {result.Error}";

    /// <summary>A step's or a move's result in words for the status line.</summary>
    private static string Describe(object? outcome) => outcome switch
    {
        MovementOutcome move => move.Arrived ? $"Arrived ({move.StepsTaken} steps)." : $"{Describe(move.StopReason)} ({move.StepsTaken} steps).",
        MoveResult reason => reason switch
        {
            MoveResult.Moved => "Moved.",
            MoveResult.NotYourTurn => "Not your turn.",
            MoveResult.DecisionPending => "Answer the open question first.",
            MoveResult.OutOfBounds => "The edge of the map.",
            MoveResult.Impassable => "Impassable ground.",
            MoveResult.Blocked => "The way is blocked.",
            MoveResult.Occupied => "An allied hero stands there.",
            MoveResult.NotEnoughMovementPoints => "No movement left today.",
            MoveResult.NoPath => "No path there.",
            MoveResult.Interrupted => "The way changed: stopped.",
            MoveResult.Met => "Meeting.",
            MoveResult.BattleStarted => "Battle!",
            MoveResult.EnemySpotted => "An enemy hero comes into sight: stopped.",
            MoveResult.GuardEncountered => "Guards bar the way.",
            MoveResult.CollectedFromAdjacent => "Picked up from beside the guards.",
            MoveResult.NoArmy => "A hero without an army cannot attack.",
            MoveResult.QuestVisited => "A quest.",
            _ => reason.ToString(),
        },
        null => "",
        _ => outcome.ToString() ?? "",
    };

    private void HandleBattleInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                HoverBattle(motion.Position);
                DragDeploy(motion.Position);
                return;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                ClickBattle(click.Position);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Escape } when _deployDrag is { Moved: true }:
                _deployDrag = null;
                _battle.Carry(null);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } rightClick
                when _session.State.Battle is { } shown && _battle.PickPoint(rightClick.Position) is { } point && shown.UnitAt(BattleSpace.ToCell(point)) is { } unit:
                _info.Open(Core.Domain.Info.InfoSheets.BattleStack(shown, unit, _info.Detailed), rightClick.Position);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter or Key.Escape } when _battle.Result is not null:
                _battleHud.Continue();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Escape } when _battleHud.ArmedSpell is not null:
                _battleHud.Disarm();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter } when _battle.Deployer is { } deployer:
                _battleHud.Report(_session.Execute(new BattleStartCommand(deployer)));
                break;
            case InputEventKey { Pressed: true } key when BattleKeyCommand(key.Keycode) is { } command:
                _battleHud.Report(_session.Execute(command));
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>W wait, D defend, R retreat for the active unit's commander; Q quick combat for either human side.</summary>
    private GameCommand? BattleKeyCommand(Key key)
    {
        if (_battle.Result is not null)
        {
            return null;
        }

        if (key == Key.Q)
        {
            return (_session.Battles.Commander(BattleSide.Attacker) ?? _session.Battles.Commander(BattleSide.Defender)) is { } any
                ? new AutoResolveBattleCommand(any)
                : null;
        }

        return _battle.Commander is not { } player ? null : key switch
        {
            Key.W => new BattleWaitCommand(player),
            Key.D => new BattleDefendCommand(player),
            Key.R => new BattleRetreatCommand(player),
            _ => null,
        };
    }

    private void HoverBattle(Vector2 screen)
    {
        if (_session.State.Battle is not { } battle || _battle.PickPoint(screen) is not { } point)
        {
            _battle.ShowIntent(null);
            _battleHud.ShowUnit(null);
            return;
        }

        var cell = BattleSpace.ToCell(point);
        _battleHud.ShowUnit(battle.UnitAt(cell));
        if (_battle.Deployer is not null)
        {
            // Deployment step (ADR-072): preview where the picked stack would stand.
            var picked = _battle.Picked is { } id ? battle.FindUnit(id) : null;
            var at = picked is null ? (CellPosition?)null : BattleTargeting.DeploymentPlacement(battle, picked, point.X, point.Y);
            _battle.ShowPlacement(at is { } p ? picked!.RectAt(p) : null, at is { } q && BattleSetup.DeploymentRefusal(battle, picked!, q) is null);
            return;
        }

        _battle.ShowIntent(_battle.Commander is not null ? BattleTargeting.ActionAt(battle, cell, point.X, point.Y) : null);
    }

    private void ClickBattle(Vector2 screen)
    {
        if (_battle.Deployer is { } deployer && _session.State.Battle is { } deploying && _battle.PickPoint(screen) is { } spot)
        {
            DeployAt(deployer, deploying, spot, screen);
            return;
        }

        if (_battle.Commander is not { } player || _session.State.Battle is not { } battle || _battle.PickPoint(screen) is not { } point)
        {
            return;
        }

        if (_battleHud.TryCastAt(BattleSpace.ToCell(point)))
        {
            _battle.ShowIntent(null);
            return;
        }

        GameCommand? command = BattleTargeting.ActionAt(battle, BattleSpace.ToCell(point), point.X, point.Y) switch
        {
            MoveAction move => new BattleMoveCommand(player, move.To),
            AttackAction attack => new BattleAttackCommand(player, attack.Target, attack.From),
            ShootAction shoot => new BattleShootCommand(player, shoot.Target),
            ShootStructureAction structure => new BattleShootStructureCommand(player, structure.Structure),
            AttackStructureAction hit => new BattleAttackStructureCommand(player, hit.Structure, hit.From),
            _ => null,
        };
        if (command is not null)
        {
            _battle.ShowIntent(null);
            _battleHud.Report(_session.Execute(command));
        }
    }

    /// <summary>
    /// Deployment step (ADR-072, ADR-155): a press on an own stack picks it and starts dragging it; a press elsewhere puts the picked stack
    /// there (click to place).
    /// </summary>
    private void DeployAt(PlayerId player, BattleState battle, Vector2 point, Vector2 screen)
    {
        if (battle.UnitAt(BattleSpace.ToCell(point)) is { IsMachine: false } own && own.Side == battle.Deployer)
        {
            if (own.Id != _battle.Picked)
            {
                _battle.Pick(own.Id);
            }

            _deployDrag = new DeployDrag(own.Id, screen);
            return;
        }

        PlacePicked(player, battle, point);
    }

    /// <summary>A pointer moved this far while the button is held makes the press a drag, not a click.</summary>
    private const float DragPixels = 6;

    /// <summary>The stack being dragged in the deployment step, and where the press was.</summary>
    private sealed record DeployDrag(int Unit, Vector2 From)
    {
        public bool Moved { get; set; }
    }

    private DeployDrag? _deployDrag;

    /// <summary>While dragging, the stack's model follows the pointer (the placement preview shows where it would stand).</summary>
    private void DragDeploy(Vector2 screen)
    {
        if (_deployDrag is not { } drag || _battle.Picked != drag.Unit)
        {
            return;
        }

        drag.Moved |= screen.DistanceTo(drag.From) >= DragPixels;
        if (drag.Moved)
        {
            _battle.Carry(_battle.PickPoint(screen));
        }
    }

    /// <summary>
    /// Letting go after a drag puts the stack where it was dropped; dropped off the field, back on its own cells or where it may not stand,
    /// it goes back (and stays picked). Letting go without a drag leaves it picked for click to place.
    /// </summary>
    private void DropDeploy(Vector2 screen)
    {
        if (_deployDrag is not { Moved: true } drag)
        {
            _deployDrag = null;
            return;
        }

        _deployDrag = null;
        if (_battle.Deployer is not { } player || _session.State.Battle is not { } battle || battle.FindUnit(drag.Unit) is not { } unit
            || _battle.PickPoint(screen) is not { } point || BattleTargeting.DeploymentPlacement(battle, unit, point.X, point.Y) == unit.Position)
        {
            _battle.Carry(null);
            return;
        }

        if (!PlacePicked(player, battle, point))
        {
            _battle.Carry(null);
        }
    }

    /// <summary>Puts the picked stack where <paramref name="point"/> places it; whether that was accepted.</summary>
    private bool PlacePicked(PlayerId player, BattleState battle, Vector2 point)
    {
        if (_battle.Picked is not { } id || battle.FindUnit(id) is not { } picked)
        {
            return false;
        }

        var result = _session.Execute(new BattleDeployCommand(player, id, BattleTargeting.DeploymentPlacement(battle, picked, point.X, point.Y)));
        _battleHud.Report(result);
        if (result.IsAccepted)
        {
            _battle.Pick(null);
            _battle.ShowPlacement(null, false);
        }

        return result.IsAccepted;
    }

    /// <summary>The battle scene replaces the adventure map and its HUD while it is open.</summary>
    private void BuildBattle()
    {
        _battle = new BattleView3D { Watching = _mode == GameMode.Watch };
        _game!.AddChild(_battle);
        _battle.Bind(_session);

        var layer = new CanvasLayer { Name = "BattleHud", Layer = 2 };
        _game.AddChild(layer);
        _battleHud = new BattleHud();
        layer.AddChild(_battleHud);
        _battleHud.Bind(_session, _battle);
        _battleHud.Layout = _layout;
        _battleHud.BottomReserve = _mode == GameMode.Play ? 0 : ReplayControlBar.ReservedHeight;

        // Auto combat (ADR-090) asks above the battle scene and above the map (a battle still choosing its width); not in the replay viewer.
        if (_mode == GameMode.Play)
        {
            var autoLayer = new CanvasLayer { Name = "AutoCombat", Layer = 4 };
            _game.AddChild(autoLayer);
            _autoCombat = new AutoCombatPanel();
            autoLayer.AddChild(_autoCombat);
            _autoCombat.Bind(_session, _battle);
        }
        else
        {
            _autoCombat = null;
        }

        _battle.Opened += () =>
        {
            _world.Visible = false;
            _hud.Visible = false;
            _camera.ProcessMode = ProcessModeEnum.Disabled;
            _world.PathPreview.Clear();
        };
        _battle.Closed += () =>
        {
            _world.Visible = true;
            _hud.Visible = true;
            _camera.ProcessMode = ProcessModeEnum.Inherit;
            _camera.MakeCurrent();
        };
    }

    /// <summary>
    /// The result screen of a won or lost game (ADR-057). A battle that decided the game is shown to its end first.
    /// </summary>
    private void BuildResult()
    {
        var layer = new CanvasLayer { Name = "Result", Layer = 3 };
        _game!.AddChild(layer);
        _result = new GameResultPanel();
        layer.AddChild(_result);
        _result.Bind(EnterRanking, SaveReplay);
        _result.MainMenuRequested += ShowMainMenu;
        _result.PlayAgainRequested += PlayAgain;

        // An arena map's day 2 (ADR-097): the battle opens by itself; the status line says who meets whom.
        _game.SubscribeWhileInTree<ArenaChampionsChosen>(_session.Events, _ =>
            _status.Text = $"Day 2: the arena battle. {_session.State.Arena?.Summary}");

        _game.SubscribeWhileInTree<PlayerDefeated>(_session.Events, e =>
            _status.Text = $"{_session.State.GetPlayer(e.Player).Name} is defeated: {e.Reason}");
        _game.SubscribeWhileInTree<GracePeriodStarted>(_session.Events, e =>
            _status.Text = $"{_session.State.GetPlayer(e.Player).Name} lost its last town and is defeated on {e.DefeatDay} unless it owns one again.");
        _game.SubscribeWhileInTree<GracePeriodEnded>(_session.Events, e =>
            _status.Text = $"{_session.State.GetPlayer(e.Player).Name} owns a town again.");
        _game.SubscribeWhileInTree<GameEnded>(_session.Events, _ =>
        {
            if (!_battle.IsOpen)
            {
                ShowResult();
            }
        });
        _battle.Closed += () =>
        {
            if (_session.State.Outcome is not null)
            {
                ShowResult();
            }
        };
    }

    /// <summary>
    /// "Play again" on an arena map's result screen (ADR-159): the game's start map with the same players, teams, factions and options, and a new
    /// seed, so the luck of the rematch is its own.
    /// </summary>
    private void PlayAgain()
    {
        var options = _session.State.Options with { Seed = (ulong)System.Random.Shared.NextInt64() };
        var state = MapConverter.ToGameState(
            _initialMap, options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings,
            artifacts: GameContent.Artifacts);
        StartGame(state);
        _status.Text = $"{_initialMap.Name}: a new game, seed {options.Seed}.";
    }

    private void ShowResult()
    {
        if (_session.State.Outcome is { } outcome)
        {
            _result.ShowOutcome(_session.State, outcome);
        }
    }

    /// <summary>
    /// Puts the finished game into the ranking in the user data folder (ADR-101) under the names the winners typed; the table it went into, with
    /// its rows highlighted, or null when no row made it. A session is entered once.
    /// </summary>
    private RankingView? EnterRanking(IReadOnlyDictionary<PlayerId, string> names)
    {
        if (ReferenceEquals(_rankedSession, _session))
        {
            return null;
        }

        _rankedSession = _session;
        var folder = ProjectSettings.GlobalizePath("user://");
        try
        {
            var record = GameReplay.Record(_initialMap, _session, generated: _generated);
            var places = RankingFile.AddGame(
                folder, record, _session.State, custom: !ShippedMaps.Contains(_initialMap.Name), DateTimeOffset.UtcNow,
                id => names.TryGetValue(id, out var name) ? name : _session.State.GetPlayer(id).Name);
            if (places.Count == 0)
            {
                return null;
            }

            var table = places[0].Table;
            var rows = RankingFile.Load(Path.Combine(folder, RankingFile.FileName))[table];
            return RankingViewOf(table, rows, places.Select(p => p.Row).ToHashSet());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Could not write the ranking: {e.Message}";
            return null;
        }
    }

    /// <summary>A ranking table as the victory screen and the leaderboard show it; <paramref name="highlight"/> are the rows to mark.</summary>
    private static RankingView RankingViewOf(RankingTable table, IReadOnlyList<RankingEntry> rows, IReadOnlySet<int> highlight) => new(
        table == RankingTable.RandomMaps ? "Random maps" : "Maps",
        rows.Select((e, i) => new RankingRowView(
            i + 1, e.Player, FactionName(e.Faction), MapLabel(e), e.Difficulty.ToString(), e.Days, e.Score, e.Title,
            LocalTime(e.Date), highlight.Contains(i))).ToList());

    private static string MapLabel(RankingEntry e) =>
        e.Template is { } template ? $"{template} {e.Size}×{e.Size}" : e.Custom ? $"{e.Map} (custom)" : e.Map;

    private static string FactionName(string town) =>
        MapObjectCatalog.Default.TryGet(BuiltInMapObjects.TownId(town), out var definition) ? definition.Name : town;

    /// <summary>"Save replay" on the end screen (ADR-101): the game's record in <c>user://replays</c>.</summary>
    private string SaveReplay(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"{_initialMap.Name} day {_session.State.Date.AbsoluteDay}";
        }

        try
        {
            var path = GameSaves.SaveReplay(ProjectSettings.GlobalizePath(ReplaysFolder), name, _initialMap, _session, _generated);
            return $"Replay saved as {Path.GetFileName(path)}.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"Could not save the replay: {e.Message}";
        }
    }

    private void BuildHud()
    {
        var hud = new CanvasLayer { Name = "Hud" };
        _hud = hud;
        _game!.AddChild(hud);

        // The parts the "Layout" tab moves (ADR-156): the info block, the minimap, the lists and the action panel.
        _hudLayout = new AdventureHudLayout();
        hud.AddChild(_hudLayout);
        var top = new VBoxContainer();
        _hudLayout.SetInfo(top);
        var turnPanel = new TurnPanel();
        top.AddChild(turnPanel);
        var playing = _mode == GameMode.Play; // the replay viewer only watches: nothing here issues a command (ADR-164)
        turnPanel.Bind(_session, endTurn: playing);
        var resources = new ResourceBar();
        top.AddChild(resources);
        resources.Bind(_session);
        _status = new Label
        {
            Text = playing
                ? "Click: route, click again: go. Tab: next hero  L: level  K: kingdom  Q: quests  P: puzzle  G: dig  WASD/QE/wheel: camera"
                : "Watching. Space: play or pause  L: level  right click: information  WASD/QE/wheel: camera",
        };
        top.AddChild(_status);
        if (playing)
        {
            // The replay viewer leaves out the questions: the record answers them (and an unbound panel's parts would never enter the tree).
            var decisions = new DecisionPanel();
            top.AddChild(decisions);
            decisions.Bind(_session);
        }

        var notice = new NoticePanel();
        top.AddChild(notice);
        notice.Bind(_session);
        // HoMM-style by default: the minimap, the hero and town lists and the action panel down the right.
        var minimapBox = new VBoxContainer();
        _hudLayout.SetPart(HudPart.Minimap, minimapBox);
        var minimap = new MinimapWidget { CellPixels = Math.Max(2, 320f / Math.Max(_session.State.Map.Width, _session.State.Map.Height)) };
        minimapBox.AddChild(minimap);
        minimap.Bind(_context);
        minimap.TileClicked += tile => _camera.FocusOn(MapSpace.ToWorld(tile));
        var level = new Button { Text = "Switch level (L)", Disabled = _session.State.Map.Levels < 2 };
        level.Pressed += () => _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels);
        minimapBox.AddChild(level);
        var lists = new VBoxContainer();
        _hudLayout.SetPart(HudPart.Lists, lists);
        _heroList = new HeroListPanel();
        lists.AddChild(_heroList);
        _heroList.Bind(_session);
        _heroList.HeroSelected += hero => Select(hero, focus: true);
        _heroList.HeroInfoRequested += hero =>
        {
            if (playing)
            {
                ShowHero(hero);
            }
        };
        _townList = new TownListPanel();
        lists.AddChild(_townList);
        _townList.Bind(_session);
        _townList.TownSelected += town =>
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
        (_hudLayout.HeroList, _hudLayout.TownList) = (_heroList, _townList);
        var side = new VBoxContainer();
        _hudLayout.SetPart(HudPart.Actions, side);

        // The town screen covers the map and the HUD (ADR-050); a hero stepping into an own town opens it.
        var townLayer = new CanvasLayer { Name = "TownScreen", Layer = 1 };
        _game.AddChild(townLayer);
        _townScreen = new TownPanel { FullScreen = true };
        townLayer.AddChild(_townScreen);
        _townScreen.Bind(_session);
        _heroScreen = new HeroPanel { FullScreen = true };
        townLayer.AddChild(_heroScreen);
        _heroScreen.Bind(_session);
        // Two own heroes met: the exchange screen covers the map like the hero screen (ADR-153).
        if (playing)
        {
            var exchange = new ExchangePanel(); // it opens itself when two own heroes meet; not in the replay viewer
            townLayer.AddChild(exchange);
            exchange.Bind(_session);
        }

        _heroScreen.AdventureSpellArmed += (hero, spell) =>
        {
            _aimedSpell = (hero, spell);
            var rank = Core.Domain.Magic.AdventureMagic.RankOf(_session.State, _session.State.GetHero(hero), spell);
            _status.Text = spell switch
            {
                Core.Domain.Magic.AdventureMagic.DimensionDoor => $"Dimension Door: click a tile up to {Core.Domain.Magic.AdventureMagic.DimensionDoorRange(rank)} tiles away in a straight line.",
                Core.Domain.Magic.AdventureMagic.DistantVision => "Distant Vision: click a cell in sight.",
                Core.Domain.Magic.AdventureMagic.Eavesdrop => rank >= 2 ? "Eavesdrop: click an enemy town." : "Eavesdrop: click an enemy town in sight.",
                _ => "Town Portal: click an own town.",
            };
        };
        _intelScreen = new AdventureIntelPanel();
        townLayer.AddChild(_intelScreen);
        _intelScreen.Bind(_session);
        _heroScreen.LedgerReopened += hero =>
            _intelScreen.ShowLedger(Core.Domain.Magic.AdventureIntel.Ledger(_session.State, _session.State.GetHero(hero).Owner, inTowns: true));
        _heroScreen.EavesdropReopened += (hero, town) =>
        {
            if (_session.State.FindObject(town) is Core.Domain.Towns.Town listened)
            {
                _heroScreen.Close();
                _intelScreen.ShowTown(Core.Domain.Magic.AdventureIntel.Town(_session.State, listened, _session.State.GetHero(hero).Owner));
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
        _townScreen.TradeRequested += () => _marketScreen.Open();
        _guildScreen = new ThievesGuildPanel();
        townLayer.AddChild(_guildScreen);
        _guildScreen.Bind(_session);
        _townScreen.ThievesGuildRequested += () => _guildScreen.Open(_session.State.CurrentPlayer.Id);
        var tavernScreen = new TavernPanel();
        townLayer.AddChild(tavernScreen);
        tavernScreen.Bind(_session);
        _townScreen.TavernRequested += tavernScreen.Open;
        _townScreen.Closed += tavernScreen.Close;
        tavernScreen.ThievesGuildRequested += () => _guildScreen.Open(_session.State.CurrentPlayer.Id);
        townLayer.MoveChild(_guildScreen, -1); // the guild opens over the tavern
        _kingdomScreen = new KingdomOverviewPanel();
        townLayer.AddChild(_kingdomScreen);
        _kingdomScreen.Bind(_session);
        var kingdom = new Button { Text = "Kingdom (K)", TooltipText = "Every source of income and the totals" }.WithTreasuryIcon();
        kingdom.Pressed += () => _kingdomScreen.Open(_session.State.CurrentPlayer.Id);
        side.AddChild(kingdom);
        _questScreen = new QuestLogPanel();
        townLayer.AddChild(_questScreen);
        _questScreen.Bind(_session);
        _questScreen.ShowRequested += position =>
        {
            _context.SetLevel(position.Level);
            _camera.FocusOn(MapSpace.ToWorld(position));
        };
        var quests = new Button { Text = "Quests (Q)", TooltipText = "The quests your heroes were shown" };
        quests.Pressed += () => _questScreen.Open(_session.State.CurrentPlayer.Id);
        side.AddChild(quests);
        _puzzleScreen = new PuzzleMapPanel();
        townLayer.AddChild(_puzzleScreen);
        _puzzleScreen.Bind(_session);
        var grail = new HBoxContainer();
        side.AddChild(grail);
        var puzzle = new Button { Text = "Puzzle map (P)", TooltipText = "The ground around the Grail, as your obelisks show it" };
        puzzle.Pressed += () => _puzzleScreen.Open(_session.State.CurrentPlayer.Id);
        grail.AddChild(puzzle);
        var dig = new Button { Text = "Dig (G)", TooltipText = "The selected hero digs where it stands, with a whole day's movement" };
        dig.Pressed += Dig;
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
        _game.SubscribeWhileInTree<TownBuilt>(_session.Events, _ => RefreshMarketButton());
        _game.SubscribeWhileInTree<TownOwnerChanged>(_session.Events, _ => RefreshMarketButton());
        _game.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => RefreshMarketButton());
        _game.SubscribeWhileInTree<MapObjectVisited>(_session.Events, e =>
        {
            if (playing)
            {
                OpenVisitedScreen(e);
            }
        });
        _hudLayout.BottomReserve = playing ? 0 : ReplayControlBar.ReservedHeight;
        _hudLayout.Apply(_layout);
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

        if (_session.State.CurrentPlayer.IsHuman && _session.State.FindObject(e.Object) is Core.Domain.Towns.Town town
            && town.Owner == _session.State.CurrentPlayer.Id)
        {
            ShowTown(town.Id);
        }
    }

    /// <summary>The demo, the map given with --map, and every map saved by the editor.</summary>
    private static List<ScenarioChoice> Scenarios()
    {
        var choices = new List<ScenarioChoice>();
        if (LaunchArguments.MapPath is { } path)
        {
            choices.Add(FromFile(path));
        }

        choices.Add(new ScenarioChoice("Demo Valley", options => MapConverter.ToGameState(MapConverter.ToDocument(DemoScenario.Create()), options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings)));
        choices.Add(new ScenarioChoice("Two Towns", options => MapConverter.ToGameState(MapConverter.ToDocument(TownsScenario.Create(GameContent.Creatures)), options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings)));
        choices.Add(new ScenarioChoice(EmberMarchScenario.Name, options => MapConverter.ToGameState(MapConverter.ToDocument(EmberMarchScenario.Create(GameContent.Creatures, GameContent.Heroes, GameContent.Buildings)), options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings, artifacts: GameContent.Artifacts)));
        var editorMaps = Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorMapsFolder);
        if (Directory.Exists(editorMaps))
        {
            choices.AddRange(Directory.GetFiles(editorMaps, "*" + MapSerializer.FileExtension).Order().Select(FromFile));
        }

        return choices;

        static ScenarioChoice FromFile(string file) =>
            new(Path.GetFileName(file), options => MapConverter.ToGameState(MapSerializer.Load(file), options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings, artifacts: GameContent.Artifacts));
    }

    /// <summary>
    /// The shipped templates (ADR-086), then the valid ones the editor's template editor saved (ADR-117): generated on "Start" for
    /// the players set up, the map seed being the game seed.
    /// </summary>
    private List<RandomMapChoice> RandomMaps() =>
    [
        .. BuiltInTemplates.All.Select(template => RandomMap(template, template.Name)),
        .. UserTemplates().Select(u => RandomMap(u.Template, $"{u.Template.Name} ({Path.GetFileName(u.Path)})")),
    ];

    /// <summary>The valid template files in the map editor's templates folder; unreadable or invalid ones are skipped with a warning.</summary>
    private static List<(string Path, MapTemplate Template)> UserTemplates()
    {
        var directory = Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorTemplatesFolder);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var found = new List<(string, MapTemplate)>();
        foreach (var file in Directory.GetFiles(directory, "*" + TemplateSerializer.FileExtension).Order(StringComparer.Ordinal))
        {
            try
            {
                var template = TemplateSerializer.Load(file);
                if (TemplateValidator.IsValid(template))
                {
                    found.Add((file, template));
                }
                else
                {
                    GD.PushWarning($"Skipping template {file}: it does not pass the template checks");
                }
            }
            catch (Exception e) when (e is FormatException or NotSupportedException or IOException)
            {
                GD.PushWarning($"Skipping template {file}: {e.Message}");
            }
        }

        return found;
    }

    private RandomMapChoice RandomMap(MapTemplate template, string label) => new(
        label,
        template.Description,
        template.Sizes,
        template.MinPlayers,
        template.TeamsLocked,
        StartSlots.Of(template).Select(s => new RandomMapSlot(s.Name, s.HumanAllowed, s.HumanByDefault, s.Team, s.PicksTier1Dwelling)).ToList(),
        request =>
        {
            var slots = StartSlots.Of(template);
            var players = request.Players
                .Select(p => new PlayerSetup(p.Slot, slots[p.Slot].Name, p.IsHuman, p.Town ?? PlayerSetup.RandomTown, p.Hero, p.Team, p.Tier1Dwelling))
                .ToList();
            var (state, generation) = MapGenerator.Build(
                template, request.Size, request.Options.Seed, players, request.Options, GameContent.Creatures, GameContent.Heroes, GameContent.Artifacts,
                GameContent.Buildings, request.MaxHeroes);
            _lastGenerated = (state, GenerationInfo.Of(template.Name, request.Size, request.Options.Seed, generation.Attempt));
            return new RandomMapResult(state, generation.Problems);
        },
        template.MinTeams,
        template.MaxTeams,
        template.MaxHeroes);
}
