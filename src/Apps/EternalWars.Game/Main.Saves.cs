using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Settings;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// The game app's menu and files (composition): the F10 menu with its settings (speeds, tooltips, layout, auto combat), save and load (ADR-058)
/// with F5 quick save and F9 quick load, the autosave slots, and the ranking and replay of a finished game (ADR-101). A save with several
/// people's colours asks who plays them from now on; a LAN client saves the host's record of the game (ADR-196).
/// </summary>
public partial class Main
{
    private const string SavesFolder = "user://saves";

    private const string ReplaysFolder = "user://replays";

    /// <summary>The maps that ship with the game (by name); every other hand-made map is "custom" in the ranking (ADR-101).</summary>
    private static readonly HashSet<string> ShippedMaps = ["Demo Valley", "Two Towns", EmberMarchScenario.Name, .. ArenaScenarios.All.Select(a => a.Name)];

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

    /// <summary>The interface settings changed, in the menu or by Alt+Enter: applied at once and kept.</summary>
    private void ChangeInterface(InterfaceSettings settings)
    {
        _interface = settings;
        _info.Detailed = settings.DetailedTooltips;
        WindowModes.Apply(settings.Window);
        if (_context is not null)
        {
            _context.FogLook = settings.Fog;
            _context.ShowGrid = settings.ShowGrid;
        }

        KeepSettings();
    }

    /// <summary>Alt+Enter: the other window mode, shown in the menu too.</summary>
    private void ToggleWindowMode()
    {
        ChangeInterface(_interface.WithWindowToggled());
        _menu.ShowInterface(_interface);
    }

    /// <summary>The start of a turn goes into the two autosave slots: this turn's and the previous turn's.</summary>
    private void Autosave()
    {
        _turnBegan = false;
        if (_session.FollowsHost)
        {
            return; // a LAN client holds its player's view, not the game: the host autosaves (ADR-193); the client saves the host's record (ADR-196)
        }

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
        WindowModes.Apply(_interface.Window); // the window mode (ADR-186), at start and whenever it changes
        AddChild(_info);
        _info.Detailed = _interface.DetailedTooltips;
        _menu.ShowInterface(_interface);
        _menu.InterfaceChanged += ChangeInterface;

        // The layout (ADR-156): applied at once to the map's HUD and the battle's, kept with the other settings.
        _layout = SettingsFile.LoadLayout(_settingsPath);
        _menu.ShowLayout(_layout);
        _menu.LayoutChanged += layout =>
        {
            _layout = layout;
            if (IsInstanceValid(_hud))
            {
                _hud.ApplyLayout(layout);
            }

            if (IsInstanceValid(_battleHud))
            {
                _battleHud.Layout = layout;
            }

            KeepSettings();
        };

        _menuButton = new Button { Text = "Menu (F10)" }.WithIcon(UiIcon.Menu, UiIcons.StatusBarSize);
        _menuButton.Pressed += _menu.Toggle;
        layer.AddChild(_menuButton);
        PlaceMenuButton();
        GetViewport().SizeChanged += PlaceMenuButton;
    }

    /// <summary>
    /// The menu button sits on the status bar's right end while a game is played, on the map and in battle (ADR-186, ADR-204); in the replay
    /// viewer (beside its bar) and before a game it stands in the bottom right corner.
    /// </summary>
    private void PlaceMenuButton()
    {
        var screen = GetViewport().GetVisibleRect().Size;
        if (_mode == GameMode.Play && IsInstanceValid(_hud) && _game is not null) // the map's and the battle's bars share the slot (ADR-204)
        {
            var slot = StatusBar.MenuSlot(screen);
            _menuButton.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _menuButton.Position = slot.Position;
            _menuButton.Size = slot.Size;
            return;
        }

        _menuButton.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        (_menuButton.OffsetLeft, _menuButton.OffsetTop) = (-132 - UiSpacing.ScreenEdge, -28 - UiSpacing.ScreenEdge);
        (_menuButton.OffsetRight, _menuButton.OffsetBottom) = (-UiSpacing.ScreenEdge, -UiSpacing.ScreenEdge);
    }

    /// <summary>The save menu's and the replay list's lines for a folder's files.</summary>
    private static List<SaveItem> SaveItems(IReadOnlyList<SaveEntry> entries) =>
        SaveSlotText.Lines(entries).Select(l => new SaveItem(l.Path, l.Label, l.Name, l.Loadable)).ToList();

    /// <summary>
    /// Whose auto combat setting the menu shows (ADR-090): the human in the battle against the computer, else the current player if human; null
    /// before a game or on a computer's turn.
    /// </summary>
    private PlayerId? AutoCombatSettingPlayer() =>
        _context is null || _mode != GameMode.Play ? null
        : _session.Battles.AutoCombatPlayer() ?? (_session.State.CurrentPlayer.IsHuman ? _session.State.CurrentPlayer.Id : null);

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
                    Load(GameSaves.PathFor(_saveFolder, GameSaves.QuickSaveName), ask: false);
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
        if (_client is { } client)
        {
            // A LAN client's save is the host's whole record of the game (ADR-196), which loads on one computer.
            _menu.SetMessage("Asking the host for the game…");
            client.RequestRecord(name.Trim(), (record, error) =>
            {
                try
                {
                    Report(record is null ? $"Could not save: {error}" : $"Saved '{Path.GetFileName(GameSaves.SaveRecord(_saveFolder, name, record))[..^GameRecordSerializer.SaveExtension.Length]}'.");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    Report($"Could not save: {e.Message}");
                }
            });
            return;
        }

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

    /// <summary>The save menu's message and the status line, for an answer that comes later (a LAN client's save).</summary>
    private void Report(string text)
    {
        _menu.SetMessage(text);
        Tell(text);
    }

    private void Load(string path) => Load(path, ask: true);

    /// <summary>
    /// Loads a save. One with several people's colours still in the game asks who plays them from now on (<paramref name="ask"/>, ADR-196; F9's
    /// quick load does not: everyone plays here, as saved); one with a single person goes on at once.
    /// </summary>
    private void Load(string path, bool ask)
    {
        if (LoadSave(path, out var error) is not { } loaded)
        {
            _menu.SetMessage($"Could not load: {error}");
            return;
        }

        if (ask && GameSaves.People(loaded.Session.State).Count > 1)
        {
            ShowSavedGame(lan: null, loaded);
            return;
        }

        StartSaved(loaded, []);
    }

    /// <summary>Replays a save; null (and why) when it does not load.</summary>
    private static SavedGame? LoadSave(string path, out string error)
    {
        try
        {
            var (session, record) = GameSaves.Load(path, GameContent.All);
            error = "";
            return new SavedGame(path, session, record);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or System.Text.Json.JsonException or IOException or KeyNotFoundException)
        {
            error = e.Message;
            return null;
        }
    }

    /// <summary>
    /// The saved-game screen (ADR-196): a save and how each colour a person plays in it is played from now on: here, by the computer or, for a LAN
    /// host (<paramref name="lan"/>), by a LAN player. It shows <paramref name="loaded"/>, else loads the save picked. <paramref name="wire"/> joins a
    /// LAN host's lobby to the screen before the first save is shown.
    /// </summary>
    private void ShowSavedGame(LanHosting? lan, SavedGame? loaded, Action<SavedGameScreen>? wire = null)
    {
        if (lan is null)
        {
            LeaveGame(); // the game being played ends here, as a load ends it
        }

        _menu.Close();
        Action back = lan is null ? ShowMainMenu : ShowMultiplayer;
        var screen = new SavedGameScreen { Lan = lan };
        ShowSetup(screen, back);
        screen.BackRequested += back;
        screen.Bind(SaveItems(GameSaves.List(_saveFolder)), loaded?.Path);
        var shown = loaded;
        screen.SaveChosen += path =>
        {
            shown = LoadSave(path, out var error);
            if (shown is null)
            {
                screen.ShowMessage($"Could not load: {error}");
                return;
            }

            screen.ShowGame(ViewOfSave(shown));
        };
        screen.StartRequested += seats =>
        {
            if (shown is not null)
            {
                StartSaved(shown, seats);
            }
        };
        wire?.Invoke(screen);
        if (loaded is not null)
        {
            screen.ShowGame(ViewOfSave(loaded));
        }
        else
        {
            screen.Choose();
        }
    }

    /// <summary>A loaded save as the saved-game screen shows it: the map and the save, the date and whose turn, the colours still in the game.</summary>
    private static SavedGameView ViewOfSave(SavedGame saved)
    {
        var state = saved.Session.State;
        var lines = new List<string> { $"{state.Date}, {state.CurrentPlayer.Name}'s turn{(state.Battle is null ? "" : ", in a battle")}" };
        return new SavedGameView(
            $"{saved.Record.Map.Name}: '{saved.Record.Save?.Name ?? Path.GetFileName(saved.Path)}'",
            lines,
            [
                .. state.Players.Where(p => !p.IsDefeated).Select(p => new SavedSeat(
                    p.Id,
                    p.Name,
                    p.IsHuman,
                    $"{state.ObjectsOf<Core.Domain.Towns.Town>().Count(t => t.Owner == p.Id)} towns, {state.AllHeroesOf(p.Id).Count()} heroes")),
            ]);
    }

    /// <summary>
    /// Goes on with a loaded save: the colours <paramref name="seats"/> gives the computer are its from now on (a command, so the game's record keeps
    /// it); a LAN host's lobby hands its players to the game, in the colours of LAN players.
    /// </summary>
    private void StartSaved(SavedGame saved, IReadOnlyList<LanSeat> seats)
    {
        var (path, session, record) = saved;
        var lobby = _lobby;
        _lobby = null;
        CloseSetup();
        _menu.Close();
        StopWatching();
        StopNetwork(keep: null, listening: lobby is not null);
        foreach (var seat in seats.Where(s => s.Kind == LanSeatKind.Computer && session.State.GetPlayer(s.Id).IsHuman))
        {
            session.Execute(new HandToComputerCommand(seat.Id)).ThrowIfRejected(); // the screen keeps a colour here
        }

        // On one computer nobody's day can be hidden from another's: a save made during a simultaneous day goes on turn by turn (ADR-197).
        if (lobby is null && session.State.IsSimultaneous)
        {
            session.Execute(new PlayTurnByTurnCommand(session.State.CurrentPlayer.Id, "The game goes on on one computer.")).ThrowIfRejected();
        }

        _generated = record.Generated;
        HostLobbyGame(session, lobby);
        BeginGame(session, record.Map, resume: true);
        _status.Text = SaveSlotText.Loaded(path, record);
    }

    /// <summary>A save, replayed to where it was saved.</summary>
    private sealed record SavedGame(string Path, GameSession Session, GameRecordDocument Record);

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
        if (_session.FollowsHost)
        {
            return null; // the host's ranking has the game
        }

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
        RankingText.TableName(table),
        RankingText.Rows(rows, highlight)
            .Select(r => new RankingRowView(r.Rank, r.Player, r.Faction, r.Map, r.Difficulty, r.Days, r.Score, r.Title, r.Date, r.Highlight)).ToList());

    /// <summary>"Save replay" on the end screen (ADR-101): the game's record in <c>user://replays</c>.</summary>
    private string SaveReplay(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"{_initialMap.Name} day {_session.State.Date.AbsoluteDay}";
        }

        if (_client is { } client)
        {
            // A LAN client's replay is the host's record of the game (ADR-196).
            var folder = ProjectSettings.GlobalizePath(ReplaysFolder);
            client.RequestRecord(null, (record, error) =>
            {
                try
                {
                    _result.ShowMessage(record is null
                        ? $"Could not save the replay: {error}"
                        : $"Replay saved as {Path.GetFileName(GameSaves.SaveReplayRecord(folder, name, record))}.");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    _result.ShowMessage($"Could not save the replay: {e.Message}");
                }
            });
            return "Asking the host for the game's record…";
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
}
