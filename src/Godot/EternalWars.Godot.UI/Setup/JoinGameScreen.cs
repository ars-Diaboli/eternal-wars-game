using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// Joining a LAN game (ADR-192): what the link to the host is doing ("Joining…", or why it failed) and Back (or Escape, which the app maps) to the
/// main menu. The game replaces it once the host has taken the player in.
/// </summary>
public partial class JoinGameScreen : PanelContainer
{
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
    private readonly Button _back = new Button { Text = "Back (Esc)" }.WithIcon(UiIcon.ArrowLeft);

    public event Action? BackRequested;

    public JoinGameScreen()
    {
        this.UseMenuPanel();
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(rows);
        rows.AddChild(new Label { Text = "LAN game", HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(_message);
        _back.Pressed += () => BackRequested?.Invoke();
        rows.AddChild(_back);
        _back.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void ShowMessage(string text) => _message.Text = text;
}
