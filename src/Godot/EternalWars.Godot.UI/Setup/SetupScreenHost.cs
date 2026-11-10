using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// The layer of the main menu and the screens before a game (ADR-101, UI-2: the new-game screen, the leaderboard, the replay list) over the
/// <see cref="MenuBackground"/>. It shows one screen at a time: the <see cref="MainMenuScreen"/> places itself on the left, every other screen
/// stands in the middle. <see cref="Back"/> is what Escape does on the screen shown; the app maps the key.
/// </summary>
public partial class SetupScreenHost : CanvasLayer
{
    public SetupScreenHost()
    {
        Name = "Setup";
        AddChild(new MenuBackground());
    }

    /// <summary>What Escape does on the screen shown; null on the main menu.</summary>
    public Action? Back { get; private set; }

    /// <summary>Puts <paramref name="screen"/> in place of the one shown; Escape does <paramref name="back"/>.</summary>
    public void ShowScreen(Control screen, Action? back)
    {
        foreach (var child in GetChildren())
        {
            if (child is not MenuBackground)
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
            var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
            AddChild(center);
            center.AddChild(screen);
        }

        Back = back;
    }
}
