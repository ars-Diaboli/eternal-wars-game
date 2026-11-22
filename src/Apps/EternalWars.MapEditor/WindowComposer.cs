using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor.Help;
using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// The editor's own windows (ADR-288): the command palette, Preferences with the shortcuts (ADR-275) and the help (ADR-276); and the
/// preferences taken into effect.
/// </summary>
internal sealed class WindowComposer(EditorParts parts)
{
    /// <summary>Where the pointer last was over the window, as the mouse events say it: for the help's page under it.</summary>
    public Vector2 Pointer { get; set; }

    /// <summary>Preferences and the command palette.</summary>
    public void Register()
    {
        parts.Command("edit.preferences", "Preferences", OpenPreferences, UiIcon.Settings, new(Key.Comma, Ctrl: true));
        parts.Command("view.palette", "Command palette", OpenPalette, shortcut: new(Key.P, Ctrl: true, Shift: true), icon: UiIcon.CommandPalette);
    }

    /// <summary>The help, after the other commands that need no panel (the order the palette and the preferences list).</summary>
    public void RegisterHelp() =>
        parts.Command("help.help", "Help", () => OpenHelp(null), shortcut: new(Key.F1), icon: UiIcon.Help);

    /// <summary>
    /// The preferences take effect: the UI's scale and contrast (ADR-277), the autosave interval and the camera's speeds (a new map reads its
    /// size when it is made).
    /// </summary>
    public void ApplyPreferences()
    {
        var preferences = parts.Preferences;
        parts.Root.GetWindow().ContentScaleFactor = preferences.UiScalePercent / 100f;
        if (UiTheme.HighContrast != preferences.HighContrast)
        {
            UiTheme.Apply(compact: true, preferences.HighContrast);
        }

        parts.Files.AutosaveMinutes = preferences.AutosaveMinutes;
        parts.TemplateFiles.AutosaveMinutes = preferences.AutosaveMinutes;
        parts.Camera.PanSpeed = preferences.CameraPanSpeed;
        parts.Camera.RotateSpeedDegrees = preferences.CameraRotateSpeed;
        parts.Camera.ZoomStep = preferences.CameraZoomSpeed * 0.5f; // 3, the default, is the camera's own step
    }

    /// <summary>The user's shortcuts (ADR-275) over the commands' defaults; the tool buttons name them.</summary>
    public void UseShortcuts()
    {
        parts.Commands.UseShortcuts(ShortcutStore.Read(parts.Settings));
        parts.Tools.ShowShortcuts(tool => parts.Commands[MenuComposer.ToolCommand(tool)].Shortcut?.Text);
    }

    /// <summary>
    /// The help (ADR-276) at the page for <paramref name="hovered"/> (null: the first page), the Template tab's pages first there. Not
    /// while a question or a file dialog is open, nor a second help; over the palette or a window it opens on top.
    /// </summary>
    public void OpenHelp(Control? hovered)
    {
        if (parts.Files.IsBusy || parts.TemplateFiles.IsBusy || parts.Overlays.GetChildren().OfType<HelpWindow>().Any())
        {
            return;
        }

        var page = EditorHelp.PageAt(hovered, Pointer, parts.OnTemplateTab ? EditorHelp.RandomMaps : EditorHelp.Tools);
        var help = new HelpWindow { Name = "Help" };
        parts.Overlays.AddChild(help);
        help.Bind(EditorHelp.Book, page);
    }

    private void OpenPalette()
    {
        if (parts.IsBusy)
        {
            return;
        }

        var palette = new CommandPalette { Name = "Palette" };
        parts.Overlays.AddChild(palette);
        palette.Bind(parts.Commands, except: "view.palette");
    }

    private void OpenPreferences()
    {
        if (parts.IsBusy)
        {
            return;
        }

        var window = new PreferencesWindow { Name = "Preferences" };
        parts.Overlays.AddChild(window);
        window.Bind(parts.Preferences, parts.Commands, (chosen, keys) =>
        {
            parts.Preferences = chosen;
            PreferencesStore.Write(parts.Settings, chosen);
            ShortcutStore.Write(parts.Settings, parts.Commands.Defaults, keys);
            ApplyPreferences();
            UseShortcuts();
            parts.Say("Preferences kept");
        });
    }
}
