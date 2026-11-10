using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Settings;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// The game app's menu and files (composition): the F10 menu with its settings (speeds, tooltips, layout, auto combat), save and load (ADR-058)
/// with F5 quick save and F9 quick load, the autosave slots, and the ranking and replay of a finished game (ADR-101).
/// </summary>
public partial class Main
{
    private const string SavesFolder = "user://saves";

    private const string ReplaysFolder = "user://replays";

    /// <summary>The maps that ship with the game (by name); every other hand-made map is "custom" in the ranking (ADR-101).</summary>
    private static readonly HashSet<string> ShippedMaps = ["Demo Valley", "Two Towns", EmberMarchScenario.Name];

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

        _menuButton = new Button
        {
            Text = "Menu (F10)", AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = -132 - UiSpacing.ScreenEdge, OffsetTop = -28 - UiSpacing.ScreenEdge, OffsetRight = -UiSpacing.ScreenEdge, OffsetBottom = -UiSpacing.ScreenEdge,
        }.WithIcon(UiIcon.Settings);
        _menuButton.Pressed += _menu.Toggle;
        layer.AddChild(_menuButton);
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
            _status.Text = SaveSlotText.Loaded(path, record);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or System.Text.Json.JsonException or IOException or KeyNotFoundException)
        {
            _menu.SetMessage($"Could not load: {e.Message}");
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
