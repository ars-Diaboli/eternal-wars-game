using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>A LAN game found on the network, as listed: the address to join, a line about it, and whether it can be joined.</summary>
public sealed record LanGameListing(string Address, string Text, bool CanJoin);

/// <summary>
/// Multiplayer from the main menu (ADR-194): the player's name; "Host a game" (the host's lobby, where the host sets the game up); the LAN games
/// found, each joined with a click; an address to type for a host that is not found (another network, a blocked broadcast); Back. The app keeps
/// the list of games up to date (<see cref="ShowGames"/>).
/// </summary>
public partial class MultiplayerScreen : PanelContainer
{
    private readonly LineEdit _name = new() { CustomMinimumSize = new Vector2(260, 0), MaxLength = 24 };
    private readonly VBoxContainer _games = new();
    private readonly LineEdit _address = new() { PlaceholderText = "192.168.1.20", CustomMinimumSize = new Vector2(260, 0) };
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };

    /// <summary>"Host a game", with the player's name.</summary>
    public event Action<string>? HostRequested;

    /// <summary>Join the game at an address (the player's name, the address).</summary>
    public event Action<string, string>? JoinRequested;

    public event Action? BackRequested;

    /// <summary>Builds the screen with the player's name filled in.</summary>
    public void Bind(string playerName)
    {
        this.UseMenuPanel();
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(rows);
        rows.AddChild(new Label { Text = "Multiplayer (LAN)", HorizontalAlignment = HorizontalAlignment.Center });
        _name.Text = playerName;
        rows.AddRow("Your name", _name, 140);

        var host = new Button { Text = "Host a game", TooltipText = "Set up a game the players on this network join" }.WithIcon(UiIcon.Confirm);
        host.Pressed += () => HostRequested?.Invoke(PlayerName);
        rows.AddChild(host);

        rows.AddChild(new Label { Text = "Games on this network" });
        rows.AddChild(_games);
        ShowGames([]);

        var join = new Button { Text = "Join" };
        join.Pressed += JoinTyped;
        _address.TextSubmitted += _ => JoinTyped();
        var addressRow = rows.AddRow("Address", _address, 140);
        addressRow.AddChild(join);
        rows.AddChild(_message);

        var back = new Button { Text = "Back (Esc)" }.WithIcon(UiIcon.ArrowLeft);
        back.Pressed += () => BackRequested?.Invoke();
        rows.AddChild(back);
        host.CallDeferred(Control.MethodName.GrabFocus);
    }

    private string PlayerName => _name.Text.Trim().Length > 0 ? _name.Text.Trim() : "Player";

    /// <summary>The games found; one button each, which joins it.</summary>
    public void ShowGames(IReadOnlyList<LanGameListing> games)
    {
        _games.ClearChildren(detach: true);
        if (games.Count == 0)
        {
            _games.AddChild(new Label { Text = "Looking for games…" });
            return;
        }

        foreach (var game in games)
        {
            var button = new Button { Text = game.Text, Disabled = !game.CanJoin, Alignment = HorizontalAlignment.Left, TooltipText = game.Address };
            button.Pressed += () => JoinRequested?.Invoke(PlayerName, game.Address);
            _games.AddChild(button);
        }
    }

    /// <summary>Why something did not work (a port in use, an address that does not read).</summary>
    public void ShowMessage(string text) => _message.Text = text;

    private void JoinTyped()
    {
        if (_address.Text.Trim().Length == 0)
        {
            ShowMessage("Type the address of the computer that hosts the game.");
            return;
        }

        JoinRequested?.Invoke(PlayerName, _address.Text.Trim());
    }
}
