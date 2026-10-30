using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Settings;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
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
/// same tile again to go; right-click clears the preview. Keys: arrows/numpad step, Tab next hero, T next town (the town screen; Escape closes it), L switches
/// level, WASD/QE/wheel camera. Run with <c>-- --map=&lt;path&gt;</c> to offer an editor map first.
/// Battles open the 3D battle scene (<see cref="BattleView3D"/>): point at a cell to preview, click to move, attack (from
/// the side the pointer is on) or shoot; W wait, D defend, R retreat, Q quick combat, wheel zoom; Enter or Escape leave
/// the result screen.
/// </remarks>
public partial class Main : Node3D
{
    private const string EditorMapsFolder = "Eternal Wars Map Editor/maps";

    private const string SavesFolder = "user://saves";

    private const string ReplaysFolder = "user://replays";

    /// <summary>The maps that ship with the game (by name); every other hand-made map is "custom" in the ranking (ADR-101).</summary>
    private static readonly HashSet<string> ShippedMaps = ["Demo Valley", "Two Towns"];

    private GameSession _session = null!;
    private Node3D? _game;
    private MapDocument _initialMap = null!;

    /// <summary>How the map of the game being played was generated (null for a hand-made map); saves keep it.</summary>
    private GenerationInfo? _generated;

    /// <summary>The last map the new-game screen had generated and how; used when that is the state the game starts with.</summary>
    private (GameState State, GenerationInfo Info)? _lastGenerated;
    private GameMenuPanel _menu = null!;

    /// <summary>The layer of the main menu, the new-game screen and the leaderboard; null during a game.</summary>
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
    private ObjectServicePanel _serviceScreen = null!;
    private ThievesGuildPanel _guildScreen = null!;
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

    /// <summary>The computer hero the camera last followed (ADR-090).</summary>
    private HeroId? _followed;

    /// <summary>A human player's turn that started while computer moves were still shown: the map turns to them once they are.</summary>
    private PlayerId? _turnToShow;

    public override void _Ready()
    {
        if (AutoInput.FromLaunchArguments() is { } autoInput)
        {
            AddChild(autoInput);
        }

        _saveFolder = ProjectSettings.GlobalizePath(SavesFolder);
        BuildMenu();
        if (LaunchArguments.NewGame)
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
        menu.NewGameRequested += ShowNewGame;
        menu.LoadRequested += _menu.Open;
        menu.LeaderboardRequested += ShowLeaderboard;
        menu.QuitRequested += () => GetTree().Quit();
    }

    private void ShowNewGame()
    {
        var setup = new NewGameScreen();
        ShowSetup(setup, back: ShowMainMenu);
        setup.Bind(Scenarios(), RandomMaps(), town => StartSlots.Heroes(GameContent.Heroes, town));
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
        }

        foreach (var child in _setup.GetChildren())
        {
            child.QueueFree();
        }

        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        _setup.AddChild(center);
        center.AddChild(screen);
        _setupBack = back;
    }

    private void CloseSetup()
    {
        _setup?.QueueFree();
        _setup = null;
        _setupBack = null;
    }

    /// <summary>Takes the game being played down: its world, HUD and screens. Nothing runs until a new game or a load builds them again.</summary>
    private void LeaveGame()
    {
        _menu.Close();
        _menu.CanSave = false;
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

        if (_context is null || _menu.Visible || _autoCombat?.Visible == true || ShowingComputerMoves || _turnToShow is not null)
        {
            return;
        }

        if (_battle.IsOpen)
        {
            HandleBattleInput(@event);
            return;
        }

        if (_townScreen.Visible || _heroScreen.Visible || _dwellingScreen.Visible || _marketScreen.Visible || _serviceScreen.Visible || _kingdomScreen.Visible)
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
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                _world.PathPreview.Clear();
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
            case InputEventKey { Pressed: true, Keycode: Key.L }:
                _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels);
                _camera.FocusOn(MapSpace.ToWorld(new MapPosition(_session.State.Map.Width / 2, _session.State.Map.Height / 2, _context.Level)));
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

    private void StartGame(GameState state)
    {
        // The map as it is now is the start of the game: saves replay from it (ADR-058).
        var initial = MapConverter.ToDocument(state);
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
        _ai = new AdventureAi(session);
        _menu.CanSave = true;
        var state = session.State;
        _context = new MapViewContext(state, _session.Events, state.CurrentPlayer.Id);

        _world = new AdventureWorldView { Name = "World" };
        _game.AddChild(_world);
        _world.Bind(_context);

        _game.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0), ShadowEnabled = true });
        _camera = new StrategyCamera { Name = "Camera" };
        _game.AddChild(_camera);

        BuildHud();
        BuildBattle();
        BuildResult();
        BuildTimer();
        _turnToShow = null;
        _followed = null;
        ApplySpeeds();

        // Autosave once the command that began a turn is logged and settled (ADR-058).
        _turnBegan = false;
        _game.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => _turnBegan = true);
        _game.SubscribeWhileInTree<CommandExecuted>(_session.Events, _ =>
        {
            if (_turnBegan)
            {
                Autosave();
            }
        });
        _game.SubscribeWhileInTree<TurnStarted>(_session.Events, e =>
        {
            CloseScreens(); // the turn may have ended on the timer with a screen open
            _followed = null;
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
            ShowResult();
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

    /// <summary>Closes the screens over the map (town, hero, dwelling, market, services, kingdom, Thieves' Guild).</summary>
    private void CloseScreens()
    {
        _townScreen.Close();
        _heroScreen.Close();
        _dwellingScreen.Close();
        _marketScreen.Close();
        _serviceScreen.Close();
        _kingdomScreen.Hide();
        _guildScreen.Hide();
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
        _menu.Bind(
            () => GameSaves.List(_saveFolder)
                .Select(e => e.Info is { } info
                    ? new SaveItem(e.Path, $"{SaveTitle(e.Path, info)}   {info.Map}, {info.Date}, {info.Player}'s turn{(info.InBattle ? ", in battle" : string.Empty)}   ({LocalTime(info.SavedAt)})", GameSaves.SlotOf(e.Path) is null ? info.Name : null, true)
                    : new SaveItem(e.Path, $"{Path.GetFileName(e.Path)}   unreadable: {e.Error}", null, false))
                .ToList(),
            GameSaves.Delete);
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
            try
            {
                SettingsFile.Save(_settingsPath, speeds);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _menu.SetMessage($"Could not keep the settings: {e.Message}");
            }
        };

        var button = new Button
        {
            Text = "Menu (F10)", AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = -120, OffsetTop = -36, OffsetRight = -8, OffsetBottom = -8,
        };
        button.Pressed += _menu.Toggle;
        layer.AddChild(button);
    }

    private void ApplySpeeds()
    {
        if (_context is not null)
        {
            _context.Speeds = _speeds;
            _battle.Speed = _speeds.Battle;
        }
    }

    /// <summary>
    /// Follows a computer hero with the camera when it starts walking where the player can see it (ADR-090), unless computer heroes are not
    /// shown.
    /// </summary>
    private void FollowStep(Core.Domain.Heroes.Hero hero, MapPosition to)
    {
        if (_speeds.ShowComputerHeroes && hero.Id != _followed && !_session.State.GetPlayer(hero.Owner).IsHuman
            && to.Level == _context.Level && _context.IsInSight(to))
        {
            _followed = hero.Id;
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
        _context is null ? null
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
            case Key.F5 when _context is not null:
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
            BeginGame(session, record.Map, resume: true);
            _status.Text = $"Loaded '{record.Save?.Name ?? Path.GetFileNameWithoutExtension(path)}'." +
                (record.Generated is { } generated ? $" {generated.Template} {generated.Size}×{generated.Size}, seed {generated.Seed}. {generated.VersionWarning}" : "");
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or System.Text.Json.JsonException or IOException or KeyNotFoundException)
        {
            _menu.SetMessage($"Could not load: {e.Message}");
        }
    }

    private void ClickTile(MapPosition tile)
    {
        var state = _session.State;
        var player = state.CurrentPlayer.Id;
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
        _status.Text = result.IsAccepted ? $"{result.Outcome}" : $"Refused: {result.Error}";

    private void HandleBattleInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                HoverBattle(motion.Position);
                return;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                ClickBattle(click.Position);
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
            DeployAt(deployer, deploying, spot);
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
            _ => null,
        };
        if (command is not null)
        {
            _battle.ShowIntent(null);
            _battleHud.Report(_session.Execute(command));
        }
    }

    /// <summary>Deployment step: a click on an own stack picks it, a click elsewhere puts the picked stack there.</summary>
    private void DeployAt(PlayerId player, BattleState battle, Vector2 point)
    {
        if (battle.UnitAt(BattleSpace.ToCell(point)) is { IsMachine: false } own && own.Side == battle.Deployer && own.Id != _battle.Picked)
        {
            _battle.Pick(own.Id);
            return;
        }

        if (_battle.Picked is { } id && battle.FindUnit(id) is { } picked)
        {
            var result = _session.Execute(new BattleDeployCommand(player, id, BattleTargeting.DeploymentPlacement(battle, picked, point.X, point.Y)));
            _battleHud.Report(result);
            if (result.IsAccepted)
            {
                _battle.Pick(null);
                _battle.ShowPlacement(null, false);
            }
        }
    }

    /// <summary>The battle scene replaces the adventure map and its HUD while it is open.</summary>
    private void BuildBattle()
    {
        _battle = new BattleView3D();
        _game!.AddChild(_battle);
        _battle.Bind(_session);

        var layer = new CanvasLayer { Name = "BattleHud", Layer = 2 };
        _game.AddChild(layer);
        _battleHud = new BattleHud();
        layer.AddChild(_battleHud);
        _battleHud.Bind(_session, _battle);

        // Auto combat (ADR-090) asks above the battle scene and above the map (a battle still choosing its width).
        var autoLayer = new CanvasLayer { Name = "AutoCombat", Layer = 4 };
        _game.AddChild(autoLayer);
        _autoCombat = new AutoCombatPanel();
        autoLayer.AddChild(_autoCombat);
        _autoCombat.Bind(_session, _battle);

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

        var top = new VBoxContainer { Position = new Vector2(12, 8) };
        hud.AddChild(top);
        var turnPanel = new TurnPanel();
        top.AddChild(turnPanel);
        turnPanel.Bind(_session);
        var resources = new ResourceBar();
        top.AddChild(resources);
        resources.Bind(_session);
        _status = new Label { Text = "Click: route, click again: go. Tab: next hero  L: level  K: kingdom  WASD/QE/wheel: camera" };
        top.AddChild(_status);
        var decisions = new DecisionPanel();
        top.AddChild(decisions);
        decisions.Bind(_session);
        var notice = new NoticePanel();
        top.AddChild(notice);
        notice.Bind(_session);
        var exchange = new ExchangePanel();
        top.AddChild(exchange);
        exchange.Bind(_session);

        // HoMM-style right-hand panel: minimap and hero list.
        var side = new VBoxContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -340, OffsetTop = 8, OffsetRight = -8 };
        hud.AddChild(side);
        var minimap = new MinimapWidget { CellPixels = Math.Max(2, 320f / Math.Max(_session.State.Map.Width, _session.State.Map.Height)) };
        side.AddChild(minimap);
        minimap.Bind(_context);
        minimap.TileClicked += tile => _camera.FocusOn(MapSpace.ToWorld(tile));
        var level = new Button { Text = "Switch level (L)", Disabled = _session.State.Map.Levels < 2 };
        level.Pressed += () => _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels);
        side.AddChild(level);
        _heroList = new HeroListPanel();
        side.AddChild(_heroList);
        _heroList.Bind(_session);
        _heroList.HeroSelected += hero => Select(hero, focus: true);
        _heroList.HeroInfoRequested += hero => ShowHero(hero);
        _townList = new TownListPanel();
        side.AddChild(_townList);
        _townList.Bind(_session);
        _townList.TownSelected += ShowTown;

        // The town screen covers the map and the HUD (ADR-050); a hero stepping into an own town opens it.
        var townLayer = new CanvasLayer { Name = "TownScreen", Layer = 1 };
        _game.AddChild(townLayer);
        _townScreen = new TownPanel { FullScreen = true };
        townLayer.AddChild(_townScreen);
        _townScreen.Bind(_session);
        _heroScreen = new HeroPanel { FullScreen = true };
        townLayer.AddChild(_heroScreen);
        _heroScreen.Bind(_session);
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
        _kingdomScreen = new KingdomOverviewPanel();
        townLayer.AddChild(_kingdomScreen);
        _kingdomScreen.Bind(_session);
        var kingdom = new Button { Text = "Kingdom (K)", TooltipText = "Every source of income and the totals" };
        kingdom.Pressed += () => _kingdomScreen.Open(_session.State.CurrentPlayer.Id);
        side.AddChild(kingdom);

        // Trading belongs to the player, not a hero: a button, or a hero visiting a map marketplace (ADR-067).
        var market = new Button { Text = "Marketplace" };
        void RefreshMarketButton()
        {
            market.Disabled = !MarketplacePanel.CanOpenFromTowns(_session.State);
            market.TooltipText = market.Disabled ? "Build a marketplace in a town to trade" : "Trade gold against resources";
        }

        market.Pressed += () => _marketScreen.Open();
        side.AddChild(market);
        RefreshMarketButton();
        _game.SubscribeWhileInTree<TownBuilt>(_session.Events, _ => RefreshMarketButton());
        _game.SubscribeWhileInTree<TownOwnerChanged>(_session.Events, _ => RefreshMarketButton());
        _game.SubscribeWhileInTree<TurnStarted>(_session.Events, _ => RefreshMarketButton());
        _game.SubscribeWhileInTree<MapObjectVisited>(_session.Events, e =>
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
        });
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
        var editorMaps = Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorMapsFolder);
        if (Directory.Exists(editorMaps))
        {
            choices.AddRange(Directory.GetFiles(editorMaps, "*" + MapSerializer.FileExtension).Order().Select(FromFile));
        }

        return choices;

        static ScenarioChoice FromFile(string file) =>
            new(Path.GetFileName(file), options => MapConverter.ToGameState(MapSerializer.Load(file), options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings, artifacts: GameContent.Artifacts));
    }

    /// <summary>The shipped templates (ADR-086): generated on "Start" for the players set up, the map seed being the game seed.</summary>
    private List<RandomMapChoice> RandomMaps() => BuiltInTemplates.All.Select(template => new RandomMapChoice(
        template.Name,
        template.Description,
        template.Sizes,
        template.MinPlayers,
        template.TeamsLocked,
        StartSlots.Of(template).Select(s => new RandomMapSlot(s.Name, s.HumanAllowed, s.HumanByDefault, s.Team)).ToList(),
        request =>
        {
            var slots = StartSlots.Of(template);
            var players = request.Players
                .Select(p => new PlayerSetup(p.Slot, slots[p.Slot].Name, p.IsHuman, p.Town ?? PlayerSetup.RandomTown, p.Hero, p.Team))
                .ToList();
            var (state, generation) = MapGenerator.Build(
                template, request.Size, request.Options.Seed, players, request.Options, GameContent.Creatures, GameContent.Heroes, GameContent.Artifacts, GameContent.Buildings);
            _lastGenerated = (state, GenerationInfo.Of(template.Name, request.Size, request.Options.Seed, generation.Attempt));
            return new RandomMapResult(state, generation.Problems);
        })).ToList();
}
