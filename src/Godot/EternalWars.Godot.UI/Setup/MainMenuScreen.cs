using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// The main menu the game opens on (ADR-101, UI-2): New game, Load, Multiplayer, Campaign, Watch a replay, Leaderboard, Quit. Multiplayer (LAN,
/// ADR-098), Campaign (Phase 6) and Watch a replay (the replay viewer) are shown disabled until they exist; disabled buttons take no focus, so the
/// arrow keys move between the ones that work. New game has the focus, so Enter starts the new-game screen.
/// </summary>
public partial class MainMenuScreen : PanelContainer
{
    private readonly Button _newGame = new() { Text = "New game" };

    public event Action? NewGameRequested;

    public event Action? LoadRequested;

    public event Action? LeaderboardRequested;

    public event Action? QuitRequested;

    public MainMenuScreen()
    {
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        rows.AddThemeConstantOverride("separation", 10);
        AddChild(rows);
        var title = new Label { Text = "Eternal Wars", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 40);
        rows.AddChild(title);

        rows.AddChild(Item(_newGame, () => NewGameRequested?.Invoke()));
        rows.AddChild(Item(new Button { Text = "Load" }, () => LoadRequested?.Invoke()));
        rows.AddChild(Later("Multiplayer", "LAN games come later (ADR-098)."));
        rows.AddChild(Later("Campaign", "The campaign comes in Phase 6."));
        rows.AddChild(Later("Watch a replay", "Comes with the replay viewer."));
        rows.AddChild(Item(new Button { Text = "Leaderboard" }, () => LeaderboardRequested?.Invoke()));
        rows.AddChild(Item(new Button { Text = "Quit" }, () => QuitRequested?.Invoke()));
        _newGame.CallDeferred(Control.MethodName.GrabFocus);
    }

    private static Button Item(Button button, Action pressed)
    {
        button.Pressed += pressed;
        return button;
    }

    private static Button Later(string text, string why) =>
        new() { Text = text, Disabled = true, FocusMode = FocusModeEnum.None, TooltipText = why };
}
