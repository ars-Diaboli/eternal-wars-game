using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>A LAN host's lobby (ADR-194): the host's player and this computer's addresses, for the players who type one.</summary>
public sealed record LanHosting(string HostName, string Addresses);

/// <summary>How a colour of a LAN game is played: on the host's computer, by a player on another computer, or by the computer.</summary>
public enum LanSeatKind
{
    Here,
    Remote,
    Computer,
}

/// <summary>A colour of the game as the host has set it up: how it is played, and its town, hero and team in words.</summary>
public sealed record LanSeat(PlayerId Id, string Colour, LanSeatKind Kind, string Details);

/// <summary>The game as the host has set it up, for the joined players: the map, its settings in words (never the seed), the colours.</summary>
public sealed record LanSetup(string Game, IReadOnlyList<string> Settings, IReadOnlyList<LanSeat> Seats);

/// <summary>The LAN host's lobby on the new-game screen (ADR-194).</summary>
public partial class NewGameScreen
{
    private const int Computer = 0;
    private const int LanHere = 1;
    private const int LanRemote = 2;

    /// <summary>How often the setup is compared with what the players were last shown.</summary>
    private const double LanCheckSeconds = 0.25;

    private readonly List<(PlayerId Id, string Name)> _seatNames = [];
    private readonly Dictionary<PlayerId, Control> _seatTowns = [];
    private readonly Dictionary<PlayerId, Label> _lanPlayers = [];
    private Func<PlayerId, string?> _lanPlayerAt = _ => null;
    private string _lanShown = "";
    private double _lanClock;
    private bool _openConfirmed;

    /// <summary>The players of the selected hand-made map with a start zone; null for a random map, whose generator gives each one (ADR-197).</summary>
    private HashSet<PlayerId>? _zoneOwners;

    /// <summary>The simultaneous first week's switch (a LAN host's lobby only: made when first asked for, so a screen out of the tree holds no node).</summary>
    private CheckBox? _simultaneousBox;

    private CheckBox Simultaneous => _simultaneousBox ??= new()
    {
        Text = "Simultaneous first week",
        FocusMode = Control.FocusModeEnum.None,
        TooltipText = "The people play each day of the first week at once, each on their own computer, the computer players after them. A hero "
                      + "entering another person's start zone, seeing another person's hero or visiting what another person visited that day has "
                      + "the day played again turn by turn, and every day after it. It needs a start zone for every person (random maps have "
                      + "them; hand-made maps get them in the editor) and one colour here; otherwise the days are played turn by turn. On an arena "
                      + "map it is the preparation day.",
    };

    /// <summary>What the switch does with this map and these colours (a LAN host's lobby only).</summary>
    private Label? _simultaneousNote;
    private ChatPanel? _chat;

    /// <summary>Set before <see cref="Bind"/>: the screen is a LAN host's lobby.</summary>
    public LanHosting? Lan { get; set; }

    /// <summary>The lobby's chat (a LAN host's lobby only: made when first asked for, so a screen out of the tree holds no node).</summary>
    public ChatPanel Chat => _chat ??= new ChatPanel();

    /// <summary>The setup changed: the app shows it to the joined players.</summary>
    public event Action<LanSetup>? LanChanged;

    /// <summary>The game as set up now.</summary>
    public LanSetup DescribeLan()
    {
        var settings = new List<string>();
        if (SelectedTemplate is not null)
        {
            settings.Add($"Size: {_size.GetItemText(_size.Selected)}, hero limit {(int)_maxHeroes.Value}");
        }

        if (_arena.Visible)
        {
            settings.Add("Arena: day 1 is preparation; the strongest heroes meet in one battle on day 2.");
        }

        var fog = _fog.GetItemText(_fog.Selected);
        settings.Add($"Fog of war: {fog}");
        var timer = _timerOn.ButtonPressed ? $"{(int)_timerMinutes.Value} min" : "off";
        settings.Add($"Difficulty: {_difficulty.Selected}; turn timer: {timer}");
        settings.Add(SimultaneousLine());

        var seats = new List<LanSeat>();
        for (var i = 0; i < _seatNames.Count; i++)
        {
            var (id, name) = _seatNames[i];
            if (SelectedTemplate is not null && i < _randomPlayers.Count)
            {
                seats.Add(new LanSeat(id, name, KindOf(_randomPlayers[i].Control), _randomPlayers[i].Details));
                continue;
            }

            var town = _seatTowns.GetValueOrDefault(id) switch
            {
                CrestPicker choice => $"{(choice.Town is { } chosen ? CrestPicker.NameOf(chosen) : Random)} town",
                Label { Text: not "—" } label => $"{label.Text} town",
                _ => "no town",
            };
            var team = _teams.TryGetValue(id, out var spin) ? $", team {(int)spin.Value}" : "";
            seats.Add(new LanSeat(id, name, _controllers.TryGetValue(id, out var controller) ? KindOf(controller) : LanSeatKind.Computer, town + team));
        }

        return new LanSetup(SelectedTemplate is { } template ? $"Random map: {template.Name}" : SelectedMap?.Name ?? "", settings, seats);
    }

    /// <summary>
    /// The simultaneous first week in words (an arena map's preparation day, ADR-199), naming the people's colours without a start zone, which keep
    /// every day turn by turn.
    /// </summary>
    private string SimultaneousLine()
    {
        if (_simultaneousBox?.ButtonPressed != true)
        {
            return "Every day is played turn by turn.";
        }

        var days = _arena.Visible ? "The preparation day is played at once" : "The first week's days are played at once";
        var people = _seatNames.Where((s, i) => LanSeatKind.Computer != (SelectedTemplate is not null && i < _randomPlayers.Count
            ? KindOf(_randomPlayers[i].Control)
            : _controllers.TryGetValue(s.Id, out var c) ? KindOf(c) : LanSeatKind.Computer)).ToList();
        if (people.Count < 2)
        {
            return $"{days} only with two people or more; here every day is played turn by turn.";
        }

        List<string> without = _zoneOwners is null ? [] : [.. people.Where(s => !_zoneOwners.Contains(s.Id)).Select(s => s.Name)];
        return without.Count == 0
            ? $"{days}."
            : $"{days} only with a start zone for every person: {string.Join(", ", without)} {(without.Count == 1 ? "has" : "have")} none on this map, so every day is played turn by turn.";
    }

    /// <summary>Shows who has joined each colour played on another computer.</summary>
    public void ShowLanPlayers(Func<PlayerId, string?> playerAt)
    {
        _lanPlayerAt = playerAt;
        foreach (var (id, label) in _lanPlayers)
        {
            label.Text = LanPlayerText(id);
        }
    }

    public override void _Process(double delta)
    {
        if (Lan is null || (_lanClock += delta) < LanCheckSeconds)
        {
            return;
        }

        _lanClock = 0;
        var setup = DescribeLan();
        var shown = string.Join("\n", [setup.Game, .. setup.Settings, .. setup.Seats.Select(s => $"{s.Id.Value} {s.Kind} {s.Details}")]);
        if (shown != _lanShown)
        {
            _lanShown = shown;
            if (_simultaneousNote is not null)
            {
                _simultaneousNote.Text = _simultaneousBox?.ButtonPressed == true ? SimultaneousLine() : "";
            }

            foreach (var (id, label) in _lanPlayers)
            {
                label.Text = LanPlayerText(id);
            }

            LanChanged?.Invoke(setup);
        }
    }

    /// <summary>Here, LAN player or Computer; a start for computer players only offers Computer.</summary>
    private static OptionButton LanController(int selected, bool humanAllowed)
    {
        var controller = new OptionButton
        {
            TooltipText = "Here: played on this computer. LAN player: a player who joins from another computer. Computer: the computer plays it.",
        };
        controller.AddItem("Here", LanHere);
        controller.AddItem("LAN player", LanRemote);
        controller.AddItem("Computer", Computer);
        controller.SetItemDisabled(0, !humanAllowed);
        controller.SetItemDisabled(1, !humanAllowed);
        controller.Select(controller.GetItemIndex(humanAllowed ? selected : Computer));
        return controller;
    }

    private static OptionButton HumanOrComputer(bool human, bool humanAllowed)
    {
        var controller = new OptionButton();
        controller.AddItem("Human", LanHere);
        controller.AddItem("Computer", Computer);
        controller.SetItemDisabled(0, !humanAllowed);
        controller.Select(human && humanAllowed ? 0 : 1);
        return controller;
    }

    private static LanSeatKind KindOf(OptionButton controller) => controller.GetSelectedId() switch
    {
        LanRemote => LanSeatKind.Remote,
        Computer => LanSeatKind.Computer,
        _ => LanSeatKind.Here,
    };

    /// <summary>The chat on the right of the setup.</summary>
    private HBoxContainer WithChat(Control setup)
    {
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", UiSpacing.Gap * 2);
        columns.AddChild(setup);
        Chat.CustomMinimumSize = new Vector2(300, 320);
        columns.AddChild(Chat);
        return columns;
    }

    /// <summary>Where the players join, and the simultaneous first week with what it does here.</summary>
    private void AddLanRows(VBoxContainer box)
    {
        box.AddChild(new Label
        {
            Text = $"Players join from Multiplayer on their computers, or by typing this computer's address: {Lan!.Addresses}.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(520, 0),
        });
        box.AddChild(Simultaneous);
        _simultaneousNote = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
        box.AddChild(_simultaneousNote);
    }

    /// <summary>
    /// A LAN game's options: the simultaneous first week (MP-2, ADR-197), with the colour played here as the host's; off outside a LAN host's
    /// lobby.
    /// </summary>
    private GameOptions WithLan(GameOptions options) => Lan is null || _simultaneousBox?.ButtonPressed != true
        ? options
        : options with { SimultaneousFirstWeek = true, HostPlayer = DescribeLan().Seats.FirstOrDefault(s => s.Kind == LanSeatKind.Here)?.Id };

    private void AddLanHeader()
    {
        if (Lan is not null)
        {
            _players.AddChild(new Label { Text = "Joined" });
        }
    }

    private void AddLanPlayer(PlayerId id)
    {
        if (Lan is null)
        {
            return;
        }

        var label = new Label { Text = LanPlayerText(id), CustomMinimumSize = new Vector2(110, 0) };
        _players.AddChild(label);
        _lanPlayers[id] = label;
    }

    /// <summary>The host's name on a colour played here, the joined player's (or "open") on a LAN player's colour.</summary>
    private string LanPlayerText(PlayerId id) => !_controllers.TryGetValue(id, out var controller) ? "" : KindOf(controller) switch
    {
        LanSeatKind.Here => Lan!.HostName,
        LanSeatKind.Remote => _lanPlayerAt(id) ?? "open",
        _ => "",
    };

    /// <summary>
    /// Why a LAN game cannot start yet: no colour played here; or colours nobody has joined, which the host confirms with "Start anyway" (they
    /// wait for a player during the game).
    /// </summary>
    private string? LanStartProblem()
    {
        if (Lan is null)
        {
            return null;
        }

        var seats = DescribeLan().Seats;
        if (seats.All(s => s.Kind != LanSeatKind.Here))
        {
            return "Play at least one colour here, on this computer.";
        }

        if (_simultaneousBox?.ButtonPressed == true && seats.Count(s => s.Kind == LanSeatKind.Here) > 1)
        {
            return "A simultaneous first week needs each person on their own computer: play one colour here, or turn it off.";
        }

        var open = seats.Where(s => s.Kind == LanSeatKind.Remote && _lanPlayerAt(s.Id) is null).Select(s => s.Colour).ToList();
        if (open.Count == 0 || _openConfirmed)
        {
            return null;
        }

        _openConfirmed = true;
        _start.SetLabel("Start anyway");
        return $"Nobody has joined {string.Join(", ", open)} yet: start anyway, and each waits for a player to join during the game, or wait.";
    }
}
