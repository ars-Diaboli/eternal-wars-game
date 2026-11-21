using EternalWars.Core.Domain.Editing;
using EternalWars.Godot.UI.Editor.Help;
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
            HighContrast = settings.Get(Section, "high_contrast", defaults.HighContrast).AsBool(),
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
        settings.Set(Section, "high_contrast", preferences.HighContrast);
        settings.Set(Section, "autosave_minutes", preferences.AutosaveMinutes);
        settings.Set(Section, "camera_pan_speed", preferences.CameraPanSpeed);
        settings.Set(Section, "camera_rotate_speed", preferences.CameraRotateSpeed);
        settings.Set(Section, "camera_zoom_speed", preferences.CameraZoomSpeed);
        settings.Set(Section, "default_map_size", preferences.DefaultMapSize);
        settings.Set(Section, "game_path", preferences.GamePath);
    }
}

/// <summary>
/// The Preferences window (EDIT-1, ADR-250), a box over the whole editor. The General tab: the UI's scale and contrast (ADR-277), the autosave interval, the
/// camera's speeds, a new map's size and the game the Test starts (ADR-267); the Shortcuts tab: every command's key (ADR-275,
/// <see cref="ShortcutEditor"/>). "OK" hands the preferences and the keys to the app, which keeps and applies them, and cannot while two
/// commands share a key; "Defaults" puts a new installation's values into the tab shown; "Cancel" or Escape closes the window with
/// nothing changed.
/// </summary>
public partial class PreferencesWindow : Control
{
    private readonly OptionButton _scale = new();
    private readonly CheckBox _contrast = new() { Text = "High contrast", TooltipText = "Opaque grounds, light frames and white text" };
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

    private readonly ShortcutEditor _shortcuts = new() { Name = "Shortcuts" };
    private readonly TabContainer _tabs = new() { Name = "Tabs" };

    /// <summary>
    /// Shows <paramref name="current"/> and the <paramref name="commands"/>' keys; OK hands <paramref name="apply"/> the chosen preferences
    /// and every command's chosen key by id.
    /// </summary>
    public void Bind(EditorPreferences current, CommandRegistry commands, Action<EditorPreferences, IReadOnlyDictionary<string, KeyChord?>> apply)
    {
        var rows = EditorOverlay.Build(this);
        rows.AddChild(new Label { Text = "Preferences", HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(_tabs);
        var grid = new GridContainer { Name = "General", Columns = 2 }.Tag("Preferences"); // F1 (ADR-276): not the map settings' General
        grid.AddThemeConstantOverride("h_separation", UiSpacing.Section);
        grid.AddThemeConstantOverride("v_separation", UiSpacing.Row);
        _tabs.AddChild(grid);
        _tabs.AddChild(_shortcuts.Tag("Shortcuts"));
        _tabs.TabChanged += tab =>
        {
            _shortcuts.StopWaiting();
            if (tab == _shortcuts.GetIndex())
            {
                _shortcuts.FocusFilter();
            }
        };
        _shortcuts.Bind([.. commands.All.Select(c => (c.Id, c.Name))], commands.Defaults, commands.Shortcuts);
        foreach (var scale in EditorPreferences.UiScales)
        {
            _scale.AddItem($"{scale} %", scale);
        }

        Row(grid, "UI scale", _scale);
        Row(grid, "Contrast", _contrast);
        Row(grid, "Autosave every (minutes)", _autosave);
        Row(grid, "Camera move speed (tiles a second)", _pan);
        Row(grid, "Camera turn speed (degrees a second)", _rotate);
        Row(grid, $"Camera zoom speed ({EditorPreferences.MinZoomSpeed} to {EditorPreferences.MaxZoomSpeed})", _zoom);
        Row(grid, "New map's size (tiles a side)", _mapSize);
        Row(grid, "Game (for Test, F5)", _game);
        Show(current);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        rows.AddChild(buttons);
        var ok = Button(buttons, "OK", () =>
        {
            var chosen = Chosen();
            var keys = new Dictionary<string, KeyChord?>(_shortcuts.Bindings);
            if (_shortcuts.Conflicts.Count == 0 && EditorOverlay.Close(this))
            {
                apply(chosen, keys);
            }
        });
        ok.CallDeferred(Control.MethodName.GrabFocus);
        void Allow()
        {
            ok.Disabled = _shortcuts.Conflicts.Count > 0;
            ok.TooltipText = ok.Disabled ? "Two commands share a key (the Shortcuts tab)" : "";
        }

        _shortcuts.Changed += Allow;
        Allow();
        Button(buttons, "Defaults", () =>
        {
            if (_tabs.CurrentTab == _shortcuts.GetIndex())
            {
                _shortcuts.ShowDefaults();
            }
            else
            {
                Show(new EditorPreferences());
            }
        }).TooltipText = "A new installation's values in the tab shown";
        Button(buttons, "Cancel", () => EditorOverlay.Close(this));
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } && !_shortcuts.IsWaiting) // Escape then stops waiting for a key
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
        _contrast.ButtonPressed = preferences.HighContrast;
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
            HighContrast = _contrast.ButtonPressed,
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
