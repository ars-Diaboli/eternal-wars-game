using EternalWars.Core.Domain.Common;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>A colour of a saved game: whether a person plays it (else the computer does, for good) and what it holds, in words.</summary>
public sealed record SavedSeat(PlayerId Id, string Colour, bool IsPerson, string Details);

/// <summary>A saved game as loaded: its title (map and save), lines about it (date, whose turn) and its colours still in the game.</summary>
public sealed record SavedGameView(string Title, IReadOnlyList<string> Lines, IReadOnlyList<SavedSeat> Seats);

/// <summary>
/// A saved game with several people's colours, before it goes on (ADR-196): the save (a list of the saves), and for each colour a person plays
/// how it is played from now on: here (on this computer; several make a hot-seat game), by the computer (for the rest of the game) or, in a LAN
/// host's lobby (<see cref="Lan"/>), by a LAN player, with who has joined it and the chat. The app loads the save picked
/// (<see cref="SaveChosen"/>, then <see cref="ShowGame"/>) and starts it (<see cref="StartRequested"/>).
/// </summary>
public partial class SavedGameScreen : PanelContainer
{
    private const int Here = 0;
    private const int Remote = 1;
    private const int Computer = 2;

    private readonly OptionButton _saves = new() { CustomMinimumSize = new Vector2(420, 0) };
    private readonly Label _title = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
    private readonly Label _lines = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly GridContainer _seats = new() { Columns = 4 };
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
    private readonly Button _start = new Button { Text = "Start" }.WithIcon(UiIcon.Confirm, keepText: true);
    private readonly Dictionary<PlayerId, OptionButton> _controllers = [];
    private readonly Dictionary<PlayerId, Label> _joined = [];
    private readonly List<string> _paths = [];
    private SavedGameView? _game;
    private Func<PlayerId, string?> _playerAt = _ => null;
    private bool _openConfirmed;
    private ChatPanel? _chat;

    /// <summary>Set before <see cref="Bind"/>: the screen is a LAN host's lobby for the saved game.</summary>
    public LanHosting? Lan { get; set; }

    /// <summary>The lobby's chat (a LAN host's lobby only).</summary>
    public ChatPanel Chat => _chat ??= new ChatPanel();

    /// <summary>A save was picked (its path): the app loads it and shows it (<see cref="ShowGame"/>), or says why it does not load.</summary>
    public event Action<string>? SaveChosen;

    /// <summary>Start: how each colour of <see cref="ShowGame"/>'s game is played from now on.</summary>
    public event Action<IReadOnlyList<LanSeat>>? StartRequested;

    /// <summary>The game or how a colour is played changed: the app shows it to the joined players (a LAN host's lobby).</summary>
    public event Action<LanSetup>? LanChanged;

    public event Action? BackRequested;

    /// <summary>The save picked now; null when there is none.</summary>
    public string? SelectedPath => _saves.Selected >= 0 && _saves.Selected < _paths.Count ? _paths[_saves.Selected] : null;

    /// <summary>Builds the screen with the saves that load, newest first; <paramref name="selected"/> is picked (else the first).</summary>
    public void Bind(IReadOnlyList<SaveItem> saves, string? selected)
    {
        this.UseMenuPanel();
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", UiSpacing.Gap * 2);
        AddChild(columns);
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        columns.AddChild(rows);

        rows.AddChild(new Label { Text = Lan is null ? "Load a game" : "Host a saved game (LAN)", HorizontalAlignment = HorizontalAlignment.Center });
        foreach (var save in saves.Where(s => s.Loadable))
        {
            _saves.AddItem(save.Label);
            _paths.Add(save.Path);
        }

        _saves.Select(Math.Max(0, selected is null ? 0 : _paths.IndexOf(selected)));
        _saves.ItemSelected += _ => Choose();
        rows.AddRow("Save", _saves, 80);
        rows.AddChild(_title);
        rows.AddChild(_lines);
        _seats.AddThemeConstantOverride("h_separation", UiSpacing.Gap * 2);
        rows.AddChild(_seats);
        if (Lan is not null)
        {
            rows.AddChild(new Label
            {
                Text = $"Players join from Multiplayer on their computers, or by typing this computer's address: {Lan.Addresses}.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(520, 0),
            });
        }

        rows.AddChild(_message);
        var back = new Button { Text = "Back (Esc)" }.WithIcon(UiIcon.ArrowLeft, keepText: true);
        back.Pressed += () => BackRequested?.Invoke();
        _start.Pressed += Start;
        rows.AddChild(Screens.ButtonRow(_start, back));

        if (Lan is not null)
        {
            Chat.CustomMinimumSize = new Vector2(300, 320);
            columns.AddChild(Chat);
        }

        if (_paths.Count == 0)
        {
            ShowGame(null);
            ShowMessage("There is no saved game.");
        }

        _start.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>
    /// The loaded save's colours, each a person plays played here (on a LAN host the first here, the others by LAN players); null: nothing to
    /// start (the save did not load).
    /// </summary>
    public void ShowGame(SavedGameView? game)
    {
        _game = game;
        _openConfirmed = false;
        _start.SetLabel("Start");
        _start.Disabled = game is null;
        _title.Text = game?.Title ?? "";
        _lines.Text = game is null ? "" : string.Join("\n", game.Lines);
        _message.Text = "";
        _seats.ClearChildren(detach: true);
        _controllers.Clear();
        _joined.Clear();
        if (game is null)
        {
            return;
        }

        var first = true;
        foreach (var seat in game.Seats)
        {
            var colour = new Label { Text = seat.Colour, CustomMinimumSize = new Vector2(80, 0) };
            colour.AddThemeColorOverride("font_color", Palette.ForSlot(seat.Id).Lightened(0.3f));
            _seats.AddChild(colour);
            if (seat.IsPerson)
            {
                var controller = Controller(first || Lan is null ? Here : Remote);
                controller.ItemSelected += _ => Changed();
                _controllers[seat.Id] = controller;
                _seats.AddChild(controller);
                first = false;
            }
            else
            {
                _seats.AddChild(new Label { Text = "Computer" });
            }

            var joined = new Label { CustomMinimumSize = new Vector2(Lan is null ? 0 : 110, 0) };
            _joined[seat.Id] = joined;
            _seats.AddChild(joined);
            _seats.AddChild(new Label { Text = seat.Details });
        }

        Changed();
    }

    /// <summary>Why a save does not load, or why the game cannot start.</summary>
    public void ShowMessage(string text) => _message.Text = text;

    /// <summary>Shows who has joined each colour played by a LAN player.</summary>
    public void ShowLanPlayers(Func<PlayerId, string?> playerAt)
    {
        _playerAt = playerAt;
        ShowJoined();
    }

    /// <summary>The game as set up now: how each colour is played, for the joined players.</summary>
    public LanSetup Describe() => new(
        _game?.Title ?? "",
        _game?.Lines ?? [],
        _game is null ? [] : [.. _game.Seats.Select(s => new LanSeat(s.Id, s.Colour, KindOf(s.Id), s.Details))]);

    /// <summary>Loads the save picked: the app answers with <see cref="ShowGame"/>.</summary>
    public void Choose()
    {
        if (SelectedPath is { } path)
        {
            ShowGame(null);
            SaveChosen?.Invoke(path);
        }
    }

    private OptionButton Controller(int selected)
    {
        var controller = new OptionButton
        {
            TooltipText = Lan is null
                ? "Here: played on this computer (several colours here take turns: hot-seat). Computer: the computer plays it for the rest of the game."
                : "Here: played on this computer. LAN player: a player who joins from another computer. Computer: the computer plays it for the rest of the game.",
        };
        controller.AddItem("Here", Here);
        if (Lan is not null)
        {
            controller.AddItem("LAN player", Remote);
        }

        controller.AddItem("Computer", Computer);
        controller.Select(controller.GetItemIndex(selected));
        return controller;
    }

    private LanSeatKind KindOf(PlayerId id) => !_controllers.TryGetValue(id, out var controller) ? LanSeatKind.Computer : controller.GetSelectedId() switch
    {
        Remote => LanSeatKind.Remote,
        Computer => LanSeatKind.Computer,
        _ => LanSeatKind.Here,
    };

    private void Changed()
    {
        _openConfirmed = false;
        _start.SetLabel("Start");
        ShowJoined();
        LanChanged?.Invoke(Describe());
    }

    /// <summary>The host's name on a colour played here, the joined player's (or "open") on a LAN player's colour.</summary>
    private void ShowJoined()
    {
        foreach (var (id, label) in _joined)
        {
            label.Text = Lan is null ? "" : KindOf(id) switch
            {
                LanSeatKind.Here => Lan.HostName,
                LanSeatKind.Remote => _playerAt(id) ?? "open",
                _ => "",
            };
        }
    }

    private void Start()
    {
        if (_game is null)
        {
            return;
        }

        var seats = Describe().Seats;
        if (seats.All(s => s.Kind != LanSeatKind.Here))
        {
            ShowMessage("Play at least one colour here, on this computer.");
            return;
        }

        var open = seats.Where(s => s.Kind == LanSeatKind.Remote && _playerAt(s.Id) is null).Select(s => s.Colour).ToList();
        if (open.Count > 0 && !_openConfirmed)
        {
            _openConfirmed = true;
            _start.SetLabel("Start anyway");
            ShowMessage($"Nobody has joined {string.Join(", ", open)} yet: start anyway, and each waits for a player to join during the game, or wait.");
            return;
        }

        StartRequested?.Invoke(seats);
    }
}
