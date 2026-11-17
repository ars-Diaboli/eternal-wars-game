using EternalWars.Core.Domain.Common;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>A colour of a LAN game in the lobby, as a joined player sees it: who plays it, the host's choices for it, whether it may be taken.</summary>
/// <param name="Id">The colour.</param>
/// <param name="Colour">Its name.</param>
/// <param name="Who">"Ann (host)", "Bob", "open", "computer".</param>
/// <param name="Details">Town, hero and team, in words.</param>
/// <param name="CanTake">Open: the player may take it.</param>
/// <param name="IsYours">The player's own colour.</param>
public sealed record LobbySeatView(PlayerId Id, string Colour, string Who, string Details, bool CanTake, bool IsYours);

/// <summary>The game a LAN host is setting up, as a joined player sees it.</summary>
public sealed record LobbyView(string Game, IReadOnlyList<string> Settings, IReadOnlyList<LobbySeatView> Seats, IReadOnlyList<string> Waiting);

/// <summary>
/// A joined player's lobby (ADR-194): what the link is doing ("Joining…", or why it failed), the game the host is setting up (the map, its settings,
/// each colour and who plays it; "Take" on an open colour), the players waiting for a colour, the chat, and Leave (or Escape, which the app maps).
/// The game replaces it once the host starts.
/// </summary>
public partial class LobbyScreen : PanelContainer
{
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(480, 0) };
    private readonly Label _game = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _settings = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly GridContainer _seats = new() { Columns = 4 };
    private readonly Label _waiting = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };

    public LobbyScreen()
    {
        this.UseMenuPanel();
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", UiSpacing.Gap * 2);
        AddChild(columns);

        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(480, 0) };
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        columns.AddChild(rows);
        rows.AddChild(new Label { Text = "LAN game", HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(_message);
        rows.AddChild(_game);
        rows.AddChild(_settings);
        _seats.AddThemeConstantOverride("h_separation", UiSpacing.Gap * 2);
        rows.AddChild(_seats);
        rows.AddChild(_waiting);
        var leave = new Button { Text = "Leave (Esc)" }.WithIcon(UiIcon.ArrowLeft, keepText: true);
        leave.Pressed += () => BackRequested?.Invoke();
        rows.AddChild(Screens.ButtonRow(leave));

        Chat.CustomMinimumSize = new Vector2(320, 360);
        columns.AddChild(Chat);
        Chat.StartWriting();
    }

    /// <summary>The player asked for an open colour.</summary>
    public event Action<PlayerId>? SeatRequested;

    public event Action? BackRequested;

    /// <summary>The lobby's chat.</summary>
    public ChatPanel Chat { get; } = new();

    public void ShowMessage(string text) => _message.Text = text;

    /// <summary>The game as the host has set it up now.</summary>
    public void Show(LobbyView lobby)
    {
        _game.Text = $"Map: {lobby.Game}";
        _settings.Text = string.Join("\n", lobby.Settings);
        _seats.ClearChildren(detach: true);
        foreach (var seat in lobby.Seats)
        {
            var colour = new Label { Text = seat.Colour };
            colour.AddThemeColorOverride("font_color", Palette.ForPlayer(seat.Id).Lightened(0.3f));
            _seats.AddChild(colour);
            _seats.AddChild(new Label { Text = seat.IsYours ? $"{seat.Who} (you)" : seat.Who });
            _seats.AddChild(new Label { Text = seat.Details });
            if (seat.CanTake)
            {
                var take = new Button { Text = "Take", FocusMode = FocusModeEnum.None };
                take.Pressed += () => SeatRequested?.Invoke(seat.Id);
                _seats.AddChild(take);
            }
            else
            {
                _seats.AddChild(new Control()); // the grid's cell (a hidden button would shift the next row)
            }
        }

        _waiting.Text = lobby.Waiting.Count == 0 ? "" : $"Waiting for a colour: {string.Join(", ", lobby.Waiting)}";
    }
}
