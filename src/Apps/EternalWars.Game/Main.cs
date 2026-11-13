using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Settings;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
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

    /// <summary>The "Menu (F10)" button; hidden on the screens before a game, which have their own buttons (F10 still opens the menu).</summary>
    private Button _menuButton = null!;

    /// <summary>The layer of the main menu, the new-game screen and the leaderboard; null during a game.</summary>
    private SetupScreenHost? _setup;
    private string _saveFolder = "";
    private bool _turnBegan;
    private AdventureAi? _ai;
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
    private TurnTimer? _timer;
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
        menu.ShowContinue(SaveSlotText.Continue(latest));
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
            NewGameChoices.Scenarios(), NewGameChoices.RandomMaps((state, info) => _lastGenerated = (state, info)), town => StartSlots.Heroes(GameContent.Heroes, town), town => StartSlots.Tier1Dwellings(GameContent.Creatures, town));
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

    /// <summary>Puts a screen on the setup layer in place of the one shown; Escape does <paramref name="back"/> (none on the main menu).</summary>
    private void ShowSetup(Control screen, Action? back)
    {
        if (_setup is null)
        {
            _setup = new SetupScreenHost();
            AddChild(_setup);
        }

        _setup.ShowScreen(screen, back);
        _menuButton.Visible = false;
    }

    private void CloseSetup()
    {
        _setup?.QueueFree();
        _setup = null;
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
        _mapInput.Select(_session.State.HeroesOf(player).FirstOrDefault()?.Id, focus: true);
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

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _setup?.Back is { } back && !_menu.Visible)
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
            if (_battleInput!.Handle(@event))
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (_mapInput.Handle(@event))
        {
            GetViewport().SetInputAsHandled();
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
        var playing = _mode == GameMode.Play;
        _ai = playing ? new AdventureAi(session) : null;
        _menu.CanSave = playing;
        var state = session.State;
        _context = new MapViewContext(state, _session.Events, playing ? state.CurrentPlayer.Id : ReplayViewer()) { FogLook = _interface.Fog, ShowGrid = _interface.ShowGrid };

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
            _hud.CloseScreens(); // the turn may have ended on the timer with a screen open
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
            if (e.Hero == _mapInput.SelectedHero)
            {
                _mapInput.Select(_session.State.HeroesOf(_session.State.CurrentPlayer.Id).FirstOrDefault()?.Id, focus: false);
            }
        });
        _game.SubscribeWhileInTree<HeroTeleported>(_session.Events, e =>
        {
            if (e.Hero == _mapInput.SelectedHero && e.To.Level != _context.Level)
            {
                _context.SetLevel(e.To.Level);
                _camera.FocusOn(MapSpace.ToWorld(e.To));
            }
        });

        if (resume)
        {
            _mapInput.Select(state.HeroesOf(state.CurrentPlayer.Id).FirstOrDefault()?.Id, focus: true);
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

    private void ApplySpeeds()
    {
        if (_context is not null)
        {
            // The replay viewer plays every hero and battle at its own speed (ADR-164).
            _context.Speeds = _mode == GameMode.Play ? _speeds : new SpeedSettings(_replaySpeed, _replaySpeed, ShowComputerHeroes: true, _replaySpeed);
            _battle.Speed = _context.Speeds.Battle;
        }
    }

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
        _battleInput = new BattleInputController(_session, _battle, _battleHud, _info);

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
    /// seed, so the luck of the rematch is its own.
    /// </summary>
    private void PlayAgain()
    {
        var options = _session.State.Options with { Seed = (ulong)System.Random.Shared.NextInt64() };
        var state = MapConverter.ToGameState(_initialMap, GameContent.All, options);
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

    private void BuildHud()
    {
        _hud = new AdventureHud();
        _game!.AddChild(_hud);
        var playing = _mode == GameMode.Play; // the replay viewer only watches: nothing here issues a command (ADR-164)
        _hud.Bind(_session, _context, _world, _camera, playing, _layout, screens: _game);
        _status = _hud.Status;
        _mapInput = new AdventureInputController(_session, _context, _world, _camera, _hud, _info);
        _hud.HeroSelected += hero => _mapInput.Select(hero, focus: true);
        _hud.DigRequested += _mapInput.Dig;
        _hud.SpellAimed += _mapInput.Aim;
    }
}
