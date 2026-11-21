using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>The user's <see cref="EditorPreferences"/> in the <see cref="EditorSettingsFile"/>.</summary>
public static class PreferencesStore
{
    private const string Section = "preferences";

    /// <summary>The preferences kept in <paramref name="settings"/>, each inside its range; a new installation's where none is kept.</summary>
    public static EditorPreferences Read(EditorSettingsFile settings)
    {
        var defaults = new EditorPreferences();
        return new EditorPreferences
        {
            UiScalePercent = settings.Get(Section, "ui_scale", defaults.UiScalePercent).AsInt32(),
            AutosaveMinutes = settings.Get(Section, "autosave_minutes", defaults.AutosaveMinutes).AsInt32(),
            CameraPanSpeed = settings.Get(Section, "camera_pan_speed", defaults.CameraPanSpeed).AsInt32(),
            CameraRotateSpeed = settings.Get(Section, "camera_rotate_speed", defaults.CameraRotateSpeed).AsInt32(),
            CameraZoomSpeed = settings.Get(Section, "camera_zoom_speed", defaults.CameraZoomSpeed).AsInt32(),
            DefaultMapSize = settings.Get(Section, "default_map_size", defaults.DefaultMapSize).AsInt32(),
            GamePath = settings.Get(Section, "game_path", defaults.GamePath).AsString(),
        }.Normalized();
    }

    public static void Write(EditorSettingsFile settings, EditorPreferences preferences)
    {
        settings.Set(Section, "ui_scale", preferences.UiScalePercent);
        settings.Set(Section, "autosave_minutes", preferences.AutosaveMinutes);
        settings.Set(Section, "camera_pan_speed", preferences.CameraPanSpeed);
        settings.Set(Section, "camera_rotate_speed", preferences.CameraRotateSpeed);
        settings.Set(Section, "camera_zoom_speed", preferences.CameraZoomSpeed);
        settings.Set(Section, "default_map_size", preferences.DefaultMapSize);
        settings.Set(Section, "game_path", preferences.GamePath);
    }
}

/// <summary>
/// The Preferences window (EDIT-1, ADR-250), a box over the whole editor: the UI's scale, the autosave interval, the camera's speeds, a
/// new map's size and the game the Test starts (ADR-267). "OK" hands the preferences to the app, which keeps and applies them; "Defaults" puts a new installation's values into
/// the fields; "Cancel" or Escape closes the window with nothing changed.
/// </summary>
public partial class PreferencesWindow : Control
{
    private readonly OptionButton _scale = new();
    private readonly SpinBox _autosave = Spin(EditorPreferences.MinAutosaveMinutes, EditorPreferences.MaxAutosaveMinutes);
    private readonly SpinBox _pan = Spin(EditorPreferences.MinPanSpeed, EditorPreferences.MaxPanSpeed);
    private readonly SpinBox _rotate = Spin(EditorPreferences.MinRotateSpeed, EditorPreferences.MaxRotateSpeed);
    private readonly SpinBox _zoom = Spin(EditorPreferences.MinZoomSpeed, EditorPreferences.MaxZoomSpeed);
    private readonly SpinBox _mapSize = Spin(EditorPreferences.MinMapSize, EditorPreferences.MaxMapSize);
    private readonly LineEdit _game = new()
    {
        Name = "GamePath", PlaceholderText = "The game project beside the editor's", CustomMinimumSize = new Vector2(320, 0),
        TooltipText = "The game Test starts: an exported game's program, or the game's Godot project (its folder or project.godot)",
    };

    public void Bind(EditorPreferences current, Action<EditorPreferences> apply)
    {
        var rows = EditorOverlay.Build(this);
        rows.AddChild(new Label { Text = "Preferences", HorizontalAlignment = HorizontalAlignment.Center });
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", UiSpacing.Section);
        grid.AddThemeConstantOverride("v_separation", UiSpacing.Row);
        rows.AddChild(grid);
        foreach (var scale in EditorPreferences.UiScales)
        {
            _scale.AddItem($"{scale} %", scale);
        }

        Row(grid, "UI scale", _scale);
        Row(grid, "Autosave every (minutes)", _autosave);
        Row(grid, "Camera move speed (tiles a second)", _pan);
        Row(grid, "Camera turn speed (degrees a second)", _rotate);
        Row(grid, $"Camera zoom speed ({EditorPreferences.MinZoomSpeed} to {EditorPreferences.MaxZoomSpeed})", _zoom);
        Row(grid, "New map's size (tiles a side)", _mapSize);
        Row(grid, "Game (for Test, F5)", _game);
        Show(current);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        rows.AddChild(buttons);
        Button(buttons, "OK", () =>
        {
            var chosen = Chosen();
            if (EditorOverlay.Close(this))
            {
                apply(chosen);
            }
        }).CallDeferred(Control.MethodName.GrabFocus);
        Button(buttons, "Defaults", () => Show(new EditorPreferences()));
        Button(buttons, "Cancel", () => EditorOverlay.Close(this));
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetViewport()?.SetInputAsHandled();
            EditorOverlay.Close(this);
        }
    }

    private void Show(EditorPreferences preferences)
    {
        // The offered scale nearest to the kept one (a file may hold any percent).
        var nearest = EditorPreferences.UiScales.MinBy(s => Math.Abs(s - preferences.UiScalePercent));
        _scale.Select(_scale.GetItemIndex(nearest));
        _autosave.Value = preferences.AutosaveMinutes;
        _pan.Value = preferences.CameraPanSpeed;
        _rotate.Value = preferences.CameraRotateSpeed;
        _zoom.Value = preferences.CameraZoomSpeed;
        _mapSize.Value = preferences.DefaultMapSize;
        _game.Text = preferences.GamePath;
    }

    private EditorPreferences Chosen()
    {
        foreach (var spin in new[] { _autosave, _pan, _rotate, _zoom, _mapSize })
        {
            spin.Apply(); // a number typed but not yet entered counts
        }

        return new EditorPreferences
        {
            UiScalePercent = _scale.GetSelectedId(),
            AutosaveMinutes = (int)_autosave.Value,
            CameraPanSpeed = (int)_pan.Value,
            CameraRotateSpeed = (int)_rotate.Value,
            CameraZoomSpeed = (int)_zoom.Value,
            DefaultMapSize = (int)_mapSize.Value,
            GamePath = _game.Text,
        }.Normalized();
    }

    private static SpinBox Spin(int min, int max) => new() { MinValue = min, MaxValue = max, Rounded = true, CustomMinimumSize = new Vector2(110, 0) };

    private static void Row(GridContainer grid, string label, Control field)
    {
        grid.AddChild(new Label { Text = label });
        grid.AddChild(field);
    }

    private static Button Button(Container parent, string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(110, 0) };
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }
}
