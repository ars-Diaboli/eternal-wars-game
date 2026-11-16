using EternalWars.Core.Domain.Settings;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The window mode setting on the screen (ADR-186): windowed, or Godot's borderless fullscreen at the screen's own size (not exclusive).
/// Alt+Enter switches it in the game and the map editor.
/// </summary>
public static class WindowModes
{
    /// <summary>Shows the window as <paramref name="mode"/> says; nothing changes when it already does.</summary>
    public static void Apply(WindowMode mode)
    {
        var wanted = mode == WindowMode.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed;
        var current = DisplayServer.WindowGetMode();
        var fullscreen = current is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (fullscreen != (wanted == DisplayServer.WindowMode.Fullscreen))
        {
            DisplayServer.WindowSetMode(wanted);
        }
    }

    /// <summary>Alt+Enter (either Enter key), pressed now.</summary>
    public static bool IsToggleKey(InputEvent @event) =>
        @event is InputEventKey { Pressed: true, Echo: false, AltPressed: true, Keycode: Key.Enter or Key.KpEnter };
}
