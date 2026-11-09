using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// The main menu the game opens on (ADR-101, UI-2, ADR-163): Continue, New game, Load, Multiplayer, Campaign, Watch a replay, Leaderboard,
/// Quit, in a column on the left of the <see cref="MenuBackground"/>. Continue loads the newest save (it names it in its tooltip); without one
/// it is disabled. Multiplayer (LAN, ADR-098), Campaign (Phase 6) and Watch a replay (the replay viewer) are shown disabled until they exist;
/// disabled buttons take no focus, so the arrow keys move between the ones that work. Continue has the focus when there is a save, else New
/// game, so Enter picks it.
/// </summary>
public partial class MainMenuScreen : PanelContainer
{
    /// <summary>The menu column's distance from the window's left edge.</summary>
    public const float LeftMargin = 96;

    private readonly Button _continue = new() { Text = "Continue", Disabled = true, FocusMode = FocusModeEnum.None, TooltipText = "No saved game yet." };
    private readonly Button _newGame = new() { Text = "New game" };

    public event Action? ContinueRequested;

    public event Action? NewGameRequested;

    public event Action? LoadRequested;

    public event Action? LeaderboardRequested;

    public event Action? QuitRequested;

    public MainMenuScreen()
    {
        this.UseMenuPanel();
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(rows);
        var title = new Label { Text = "Eternal Wars", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 40);
        rows.AddChild(title);
        rows.AddChild(UiSpacing.Space(UiSpacing.Gap));

        rows.AddChild(Item(_continue, () => ContinueRequested?.Invoke()));
        rows.AddChild(Item(_newGame, () => NewGameRequested?.Invoke()));
        rows.AddChild(Item(new Button { Text = "Load" }, () => LoadRequested?.Invoke()));
        rows.AddChild(Later("Multiplayer", "LAN games come later (ADR-098)."));
        rows.AddChild(Later("Campaign", "The campaign comes in Phase 6."));
        rows.AddChild(Later("Watch a replay", "Comes with the replay viewer."));
        rows.AddChild(Item(new Button { Text = "Leaderboard" }, () => LeaderboardRequested?.Invoke()));
        rows.AddChild(UiSpacing.Space(UiSpacing.Gap));
        rows.AddChild(Item(new Button { Text = "Quit" }, () => QuitRequested?.Invoke()));
        _newGame.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Offers Continue for the newest save, described by <paramref name="latest"/> (its name, map and date); null leaves it disabled.</summary>
    public void ShowContinue(string? latest)
    {
        _continue.Disabled = latest is null;
        _continue.FocusMode = latest is null ? FocusModeEnum.None : FocusModeEnum.All;
        _continue.TooltipText = latest is null ? "No saved game yet." : $"Load the newest save: {latest}";
        (latest is null ? _newGame : _continue).CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Ready()
    {
        // Vertically centred on the left of the picture, growing both ways as the column does.
        GrowVertical = GrowDirection.Both;
        SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft, LayoutPresetMode.Minsize, (int)LeftMargin);
    }

    private static Button Item(Button button, Action pressed)
    {
        button.Pressed += pressed;
        return button;
    }

    private static Button Later(string text, string why) =>
        new() { Text = text, Disabled = true, FocusMode = FocusModeEnum.None, TooltipText = why };
}
