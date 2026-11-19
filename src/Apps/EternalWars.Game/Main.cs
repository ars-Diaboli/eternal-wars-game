using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Settings;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Generation;
using EternalWars.Core.Network;
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
/// Run with <c>-- --new-game</c> to open on the new-game screen, with <c>-- --map=&lt;path&gt;</c> to offer an editor map first. The map's
/// clicks and keys are <see cref="AdventureInputController"/>'s (WASD/QE/wheel move the camera); battles open the 3D battle scene
/// (<see cref="BattleView3D"/>), whose input is <see cref="BattleInputController"/>'s. Here: F10 menu, F5/F9 quick save and load, Escape back
/// on the screens before a game.
/// </remarks>
public partial class Main : Node3D
{
    private GameSession _session = null!;
    private Node3D? _game;
    private MapDocument _initialMap = null!;

    /// <summary>How the map of the game being played was generated (null for a hand-made map); saves keep it.</summary>
    private GenerationInfo? _generated;

    /// <summary>The last map the new-game screen had generated and how; used when that is the state the game starts with.</summary>
    private (GameState State, GenerationInfo Info)? _lastGenerated;
    private GameMenuPanel _menu = null!;

    /// <summary>Which screen is shown: the screens before a game, the menu, the map or the battle scene (ADR-222).</summary>
    private GameScreens _screens = null!;
    private string _saveFolder = "";

    /// <summary>When the game shown is autosaved (ADR-231); null outside a game.</summary>
    private AutosaveSchedule? _autosave;

    /// <summary>The game's computer players, turn timer and the colours played here (ADR-222); null outside a game.</summary>
    private GameDirector? _director;

    /// <summary>Where the map's camera goes and which hero is selected (ADR-231); null outside a game.</summary>
    private MapFocus _focus = null!;
    private MapViewContext _context = null!;
    private AdventureWorldView _world = null!;
    private StrategyCamera _camera = null!;
    private Label _status = null!;

    /// <summary>The adventure map's HUD and its screens; its parts move as the layout says (ADR-156).</summary>
    private AdventureHud _hud = null!;
    private BattleView3D _battle = null!;
    private BattleHud _battleHud = null!;
    private BattleInputController? _battleInput;

    /// <summary>The adventure map's clicks and keys, and the selected hero.</summary>
    private AdventureInputController _mapInput = null!;
    private AutoCombatPanel? _autoCombat;
    private TurnTimerPanel? _timerPanel;
    private GameResultPanel _result = null!;

    /// <summary>The session last put into the ranking, so a game is entered once.</summary>
    private GameSession? _rankedSession;
    private string _settingsPath = "";
    private SpeedSettings _speeds = SpeedSettings.Default;
    private InterfaceSettings _interface = InterfaceSettings.Default;
    private LayoutSettings _layout = LayoutSettings.Default;

    /// <summary>The right-click information window (ADR-154), above every screen, for the whole run.</summary>
    private readonly InfoWindow _info = new();

    public override void _ExitTree() => GameCursors.Release();

    public override void _Ready()
    {
        UiTheme.Apply(); // the shared margins and gaps (ADR-163) and the look (ADR-191)
        AddChild(new TouchInput()); // a touch screen as the mouse: a long press opens the information window (ADR-190)
        if (AutoInput.FromLaunchArguments() is { } autoInput)
        {
            AddChild(autoInput);
        }

        _saveFolder = LaunchArguments.SavesPath ?? ProjectSettings.GlobalizePath(SavesFolder);
        BuildMenu();
        if (LaunchArguments.ReplayPath is { } replay)
        {
            ShowReplays();
            Watch(replay, null);
        }
        else if (LaunchArguments.Join is { } address)
        {
            JoinGame(address);
        }
        else if (LaunchArguments.Host)
        {
            HostLobby(saved: false);
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
        menu.ShowContinue(SaveSlotText.Continue(latest));
        menu.ContinueRequested += Continue;
        menu.NewGameRequested += ShowNewGame;
        menu.LoadRequested += _menu.Open;
        menu.MultiplayerRequested += ShowMultiplayer;
        menu.WatchRequested += ShowReplays;
        menu.LeaderboardRequested += ShowLeaderboard;
        menu.QuitRequested += () => GetTree().Quit();
    }

    private void ShowNewGame()
    {
        var setup = new NewGameScreen();
        ShowSetup(setup, back: ShowMainMenu);
        BindNewGame(setup);
        setup.BackRequested += ShowMainMenu;
    }

    /// <summary>
    /// The new-game screen's maps and templates, sorted and filtered as kept in the settings file (ADR-204); its Start starts the game (a LAN host's
    /// lobby's too).
    /// </summary>
    private void BindNewGame(NewGameScreen setup)
    {
        setup.Bind(
            NewGameChoices.Scenarios(), NewGameChoices.RandomMaps((state, info) => _lastGenerated = (state, info)), town => StartSlots.Heroes(GameContent.Heroes, town), town => StartSlots.Tier1Dwellings(GameContent.Creatures, town),
            _interface.MapList());
        setup.MapListChanged += view =>
        {
            _interface = _interface.WithMapList(view);
            _menu.ShowInterface(_interface); // the menu changes its copy of the interface settings, so it must hold this one
            KeepSettings();
        };
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

    /// <summary>Puts a screen on the setup layer in place of the one shown; Escape does <paramref name="back"/> (none on the main menu).</summary>
    private void ShowSetup(Control screen, Action? back) => _screens.ShowSetup(screen, back);

    private void CloseSetup() => _screens.CloseSetup();

    /// <summary>Takes the game being played down: its world, HUD and screens. Nothing runs until a new game or a load builds them again.</summary>
    private void LeaveGame()
    {
        StopNetwork(keep: null);
        _menu.Close();
        _menu.CanSave = false;
        StopWatching();
        if (_game is null)
        {
            return;
        }

        _game.QueueFree();
        _game = null;
        _screens.LeaveGame();
        _context = null!;
        _status = null!;
        _director = null;
        _focus = null!;
        _autosave = null;
        _timerPanel = null;
        _autoCombat = null;
        _chat = null;
        _pause = null;
    }

    /// <summary>
    /// Once a frame the <see cref="GameDirector"/> runs the game (ADR-222): the turn timer's clock (ADR-091), the turn the map waits to turn to,
    /// then the computer players' turns, one a frame (ADR-059). The screens open here (the menu, the battle scene, the auto combat question) hold
    /// the AI and the end of a turn played here; while computer heroes' moves are being shown (ADR-090), the AI, the clock and the next human
    /// turn wait for them.
    /// </summary>
    public override void _Process(double delta)
    {
        PollNetwork(delta); // a LAN game's lobby, joins, commands and chat (ADR-192, ADR-194)
        if (!Playing)
        {
            ProcessReplay(delta); // the replay viewer runs the record, not the AI or the clock (ADR-164)
            return;
        }

        if (_director is not { } director)
        {
            return;
        }

        var showing = ShowingComputerMoves;
        if (director.RunClock(delta, showing, _screens.HoldTheGame) is { } clock && _timerPanel is not null)
        {
            if (clock.Warning is { } warning)
            {
                _timerPanel.Warn(warning);
            }

            _timerPanel.ShowTime(clock.SecondsLeft, clock.Stopped, clock.Up, delta);
        }

        if (showing)
        {
            return;
        }

        if (director.TakeTurnToShow() is { } player)
        {
            ShowTurn(player);
        }

        director.PlayComputer(_screens.HoldTheGame);
    }

    /// <summary>Hot-seat: the map is drawn through the eyes of whoever plays now, with their first hero selected.</summary>
    private void ShowTurn(PlayerId player)
    {
        if (!_focus.StillTheTurnOf(player))
        {
            return; // the turn has passed on meanwhile
        }

        _context.SetViewer(player);
        _mapInput.Select(_focus.FirstHeroOf(player), focus: true);
        FocusOnEventPlace(_session.State.PendingDecision);
    }

    /// <summary>
    /// A turn-start message or question of a map event moves the camera to what it concerns (EVT-2 f): a town, a revealed area, a hero given.
    /// Not while the computer's moves are still shown; the turn's showing comes back to it.
    /// </summary>
    private void FocusOnEventPlace(PendingDecision? decision)
    {
        if (_focus.EventPlace(decision, ShowingComputerMoves) is { } place)
        {
            _context.SetLevel(place.Level);
            _camera.FocusOn(MapSpace.ToWorld(place));
        }
    }

    /// <summary>
    /// Alt+Enter switches the window mode. A deployment drag ends where the button is let go, even over the HUD (which would otherwise take the
    /// release).
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        // Alt+Enter switches the window mode (ADR-186) wherever the focus is, a text field included.
        if (WindowModes.IsToggleKey(@event))
        {
            GetViewport().SetInputAsHandled();
            ToggleWindowMode();
            return;
        }

        if (_battleInput is not null && _battleInput.HandleRelease(@event))
        {
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

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _screens.GoBack())
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!Playing && _context is not null && !_menu.Visible && HandleReplayInput(@event))
        {
            return;
        }

        if (_context is null || _menu.Visible || _autoCombat?.Visible == true || ShowingComputerMoves || _director?.HasTurnToShow == true)
        {
            return;
        }

        if (_battle.IsOpen)
        {
            // The chat in a LAN battle, while Enter does not start the battle or leave its result.
            if ((_battle.Deployer is null && _battle.Result is null && HandleChatKey(@event)) || _battleInput!.Handle(@event))
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (HandleChatKey(@event) || _mapInput.Handle(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void StartGame(GameState state)
    {
        // The map as it is now is the start of the game: saves replay from it (ADR-058).
        var initial = MapConverter.ToDocument(state);
        var lobby = _lobby; // a LAN host's lobby hands its players to the game
        _lobby = null;
        StopWatching();
        StopNetwork(keep: null, listening: lobby is not null);
        var session = new GameSession(state);
        HostLobbyGame(session, lobby); // before the map is shown: it is drawn for this computer's colours
        BeginGame(session, initial, resume: false);
    }

    /// <summary>
    /// Builds the world and the HUD for a session. A new game is started here; a loaded one (<paramref name="resume"/>)
    /// is already running, so the views are built from its state and a battle in progress is shown as it stands.
    /// </summary>
    private void BeginGame(GameSession session, MapDocument initialMap, bool resume)
    {
        StopNetwork(keep: session); // a load or a replay leaves a LAN game
        _game?.QueueFree();
        _game = new Node3D { Name = "Game" };
        AddChild(_game);

        _screens.EnterGame();
        _session = session;
        _initialMap = initialMap;
        var playing = Playing;

        // The computer players and the clock are a LAN host's; a client only shows them (ADR-198).
        // The views and handlers below hold this game's director, not the field: a callback after LeaveGame still finds it.
        var director = _director = new GameDirector(session, _host is not null ? LanSeats.Of(_host) : _client is not null ? LanSeats.Of(_client) : null, watching: !playing);
        _focus = new MapFocus(session, director, watching: Watching);
        _autosave = new AutosaveSchedule(session, watching: !playing);
        _menu.CanSave = playing; // a LAN client saves the host's record of the game (ADR-196)
        var state = session.State;
        _context = new MapViewContext(state, _session.Events, playing ? director.Viewer() : _replay!.Viewer(state))
        {
            FogLook = _interface.Fog, ShowGrid = _interface.ShowGrid, PlaysElsewhere = director.PlaysElsewhere,
        };

        _world = new AdventureWorldView { Name = "World" };
        _game.AddChild(_world);
        _world.Bind(_context);

        _game.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0), ShadowEnabled = true });
        _camera = new StrategyCamera { Name = "Camera" };
        _game.AddChild(_camera);

        // A game begun from inside another ("Play again", a load) would otherwise keep the old game's camera, freed at the end of the frame.
        _camera.MakeCurrent();

        BuildHud();
        BuildChat();
        BuildBattle(director);
        if (playing)
        {
            BuildResult(director);
            BuildTimer();
        }
        else
        {
            _timerPanel = null;
            BuildReplayBar(); // the viewer's controls; no ranking, end screen, clock or autosave (ADR-164)
        }

        ApplySpeeds();

        // Autosave once the command that began a turn is logged and settled (ADR-058).
        var autosave = _autosave;
        _game.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => autosave.TurnStarted());
        _game.SubscribeWhileInTree<CommandExecuted>(_session.Events, _ =>
        {
            if (autosave.CommandExecuted())
            {
                Autosave();
            }
        });

        // The simultaneous first week of a LAN game (ADR-197): the status line says how today is played.
        _game.SubscribeWhileInTree<SimultaneousDayStarted>(_session.Events, e =>
            Tell($"Day {e.Date.AbsoluteDay}: every person plays at once, each on their own computer; the computer players after them."));
        _game.SubscribeWhileInTree<SimultaneousDaysEnded>(_session.Events, e => Tell($"{e.Reason} From now on the days are played turn by turn."));

        _game.SubscribeWhileInTree<TurnStarted>(_session.Events, e =>
        {
            _hud.CloseScreens(); // the turn may have ended on the timer with a screen open
            _focus.TurnStarted();
            if (!playing)
            {
                ReplayTurnStarted(); // the viewer keeps its own eyes on the map
                return;
            }

            // The map stays with the last human here, who watches the computer heroes it can see (ADR-090), or another computer's player; while
            // the computer's moves are shown, the turn waits for them.
            if (director.TurnStarted(e.Player, ShowingComputerMoves))
            {
                ShowTurn(e.Player);
            }
        });
        _game.SubscribeWhileInTree<DecisionRequested>(_session.Events, e => FocusOnEventPlace(e.Decision));
        _context.HeroStepShown += FollowStep;
        _game.SubscribeWhileInTree<HeroDefeated>(_session.Events, e =>
        {
            if (_focus.Reselects(e, _mapInput.SelectedHero, out var next))
            {
                _mapInput.Select(next, focus: false);
            }
        });
        _game.SubscribeWhileInTree<HeroTeleported>(_session.Events, e =>
        {
            if (MapFocus.AfterTeleport(e, _mapInput.SelectedHero, _context.Level) is { } to)
            {
                _context.SetLevel(to.Level);
                _camera.FocusOn(MapSpace.ToWorld(to));
            }
        });

        if (resume)
        {
            _mapInput.Select(_focus.FirstHeroOf(_context.Viewer ?? state.CurrentPlayer.Id), focus: true);
            _battle.Restore();
            if (playing)
            {
                ShowResult();
            }
        }
        else
        {
            _session.Start();
            if (_autosave.Started())
            {
                Autosave();
            }
        }
    }

    /// <summary>
    /// The turn timer's clock (ADR-091) over the map and the battle scene; the <see cref="GameDirector"/> runs it, or on a LAN client shows the
    /// host's (ADR-198).
    /// </summary>
    private void BuildTimer()
    {
        var layer = new CanvasLayer { Name = "TurnTimer", Layer = 4 };
        _game!.AddChild(layer);
        _timerPanel = new TurnTimerPanel();
        layer.AddChild(_timerPanel);
        _timerPanel.Bind();
    }

    private void ApplySpeeds()
    {
        if (_context is not null)
        {
            // The replay viewer plays every hero and battle at its own speed (ADR-164).
            _context.Speeds = Playing ? _speeds : new SpeedSettings(_replaySpeed, _replaySpeed, ShowComputerHeroes: true, _replaySpeed);
            _battle.Speed = _context.Speeds.Battle;
        }
    }

    /// <summary>The camera follows a hero starting to walk where the player can see it, when <see cref="MapFocus"/> says (ADR-090, ADR-164).</summary>
    private void FollowStep(Core.Domain.Heroes.Hero hero, MapPosition to)
    {
        if (_focus.Follows(hero, to, _speeds.ShowComputerHeroes, _context.Level, _context.IsInSight))
        {
            _context.SetLevel(to.Level);
            _camera.FocusOn(MapSpace.ToWorld(to));
        }
    }

    /// <summary>Whether computer heroes' moves are still being shown: the next turn, the AI and the turn clock wait for them.</summary>
    private bool ShowingComputerMoves => _context is not null && _context.IsShowingComputer;

    /// <summary>The battle scene replaces the adventure map and its HUD while it is open.</summary>
    private void BuildBattle(GameDirector director)
    {
        // A LAN game shows only the battles of this computer's players, each commanding its own side (ADR-195).
        _battle = new BattleView3D { Watching = Watching, PlaysHere = _host is null && _client is null ? null : p => !director.PlaysElsewhere(p) };
        _game!.AddChild(_battle);
        _battle.Bind(_session);

        var layer = new CanvasLayer { Name = "BattleHud", Layer = 2 };
        _game.AddChild(layer);
        _battleHud = new BattleHud();
        layer.AddChild(_battleHud);
        _battleHud.Bind(_session, _battle);
        _battleHud.Layout = _layout;
        _battleHud.BottomReserve = Playing ? 0 : ReplayControlBar.ReservedHeight;
        _battleInput = new BattleInputController(_session, _battle, _battleHud, _info);

        // Auto combat (ADR-090) asks above the battle scene and above the map (a battle still choosing its width); not in the replay viewer.
        if (Playing)
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

        _screens.ShowBattle(_battle, _autoCombat, _world, _camera, _chat); // in place of the map while it is open
    }

    /// <summary>
    /// The result screen of a won or lost game (ADR-057). A battle that decided the game is shown to its end first.
    /// </summary>
    private void BuildResult(GameDirector director)
    {
        var layer = new CanvasLayer { Name = "Result", Layer = 3 };
        _game!.AddChild(layer);
        _result = new GameResultPanel();
        layer.AddChild(_result);
        _result.Bind(EnterRanking, SaveReplay);
        _result.MainMenuRequested += ShowMainMenu;
        _result.PlayAgainRequested += PlayAgain;
        _result.CanPlayAgain = _client is null; // a LAN client's rematch is the host's to start (ADR-199)
        _result.PlaysHere = director.PlaysHere;

        _hud.ReportGameFlow(); // the arena's day 2, defeats, a lost last town
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
    /// seed, so the luck of the rematch is its own. A LAN host's rematch keeps its players: each client gets the new game (ADR-199).
    /// </summary>
    private void PlayAgain()
    {
        var seed = (ulong)System.Random.Shared.NextInt64(); // a new game's seed, as the new-game screen draws one
        var state = GameRematch.State(_initialMap, _session.State, GameContent.All, seed, keepControllers: _host is not null);
        if (_host is { } host)
        {
            var session = new GameSession(state);
            session.Start();
            if (host.PlayAgain(session) is { IsAccepted: false, Error: var error })
            {
                _status.Text = error ?? "";
                return;
            }

            BeginGame(session, _initialMap, resume: true);
        }
        else
        {
            StartGame(state);
        }

        _status.Text = $"{_initialMap.Name}: a new game, seed {seed}.";
    }

    private void ShowResult()
    {
        if (_session.State.Outcome is { } outcome)
        {
            _result.ShowOutcome(_session.State, outcome);
        }
    }

    private void BuildHud()
    {
        _hud = new AdventureHud();
        _game!.AddChild(_hud);
        var playing = Playing; // the replay viewer only watches: nothing here issues a command (ADR-164)
        _hud.Bind(_session, _context, _world, _camera, playing, _layout, screens: _game);
        _status = _hud.Status;
        _mapInput = new AdventureInputController(_session, _context, _world, _camera, _hud, _info);
        _hud.HeroSelected += hero => _mapInput.Select(hero, focus: true);
        _hud.DigRequested += _mapInput.Dig;
        _hud.SpellAimed += _mapInput.Aim;
        _hud.MenuRequested += _menu.Toggle;
        _screens.ShowHud(_hud);
    }
}
