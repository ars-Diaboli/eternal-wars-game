using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// The layer of the main menu and the screens before a game (ADR-101, UI-2: the new-game screen, the leaderboard, the replay list) over the
/// <see cref="MenuBackground"/>. It shows one screen at a time: the <see cref="MainMenuScreen"/> places itself on the left, every other screen
/// stands in the middle. <see cref="Back"/> is what Escape does on the screen shown; the app maps the key. A status bar along the bottom
/// (ADR-204) names the button under the pointer.
/// </summary>
public partial class SetupScreenHost : CanvasLayer
{
    public SetupScreenHost()
    {
        Name = "Setup";
        AddChild(new MenuBackground());
        AddChild(Bar);
        Bar.Bind(null);
    }

    /// <summary>The status bar along the bottom (ADR-204): the labels of the buttons under the pointer.</summary>
    public Hud.StatusBar Bar { get; } = new();

    /// <summary>What Escape does on the screen shown; null on the main menu.</summary>
    public Action? Back { get; private set; }

    /// <summary>Puts <paramref name="screen"/> in place of the one shown; Escape does <paramref name="back"/>.</summary>
    public void ShowScreen(Control screen, Action? back)
    {
        foreach (var child in GetChildren())
        {
            if (child is not MenuBackground && child != Bar)
            {
                child.QueueFree();
            }
        }

        if (screen is MainMenuScreen)
        {
            AddChild(screen); // places itself on the left
        }
        else
        {
            var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1, OffsetBottom = -Hud.StatusBar.Height };
            AddChild(center);
            center.AddChild(screen);
        }

        GameCursors.Show(Core.Domain.Cursors.GameCursor.Pointer); // no map cursor left over from a game
        MoveChild(Bar, -1); // over the screen
        Bar.Message.Text = string.Empty;
        Back = back;
    }
}
