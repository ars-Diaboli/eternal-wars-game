using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// What the map editor's user sets in Preferences (EDIT-1, ADR-250): the UI's scale and contrast (ADR-277), how often unsaved work is autosaved, the camera's
/// speeds, the size of a new map and the game the Test starts (ADR-267). The values are what a new installation starts with; <see cref="Normalized"/> brings values read
/// from a file or typed into a field into their ranges.
/// </summary>
public sealed record EditorPreferences
{
    public const int MinUiScale = 100;
    public const int MaxUiScale = 200;

    /// <summary>The UI scales the Preferences window offers, in percent.</summary>
    public static IReadOnlyList<int> UiScales { get; } = [100, 125, 150, 175, 200];

    public const int MinAutosaveMinutes = 1;
    public const int MaxAutosaveMinutes = 30;
    public const int MinPanSpeed = 4;
    public const int MaxPanSpeed = 40;
    public const int MinRotateSpeed = 30;
    public const int MaxRotateSpeed = 180;
    public const int MinZoomSpeed = 1;
    public const int MaxZoomSpeed = 5;
    public const int MinMapSize = 16;
    public const int MaxMapSize = MapSizes.MaxDimension;

    /// <summary>The size of the editor's UI, in percent of its design size.</summary>
    public int UiScalePercent { get; init; } = 100;

    /// <summary>The theme's high contrast (ADR-277): opaque grounds, light frames, white text.</summary>
    public bool HighContrast { get; init; }

    /// <summary>The minutes between two autosaves of a map with unsaved work.</summary>
    public int AutosaveMinutes { get; init; } = 2;

    /// <summary>How fast the keys move the camera over the map, in tiles a second at 100 % zoom.</summary>
    public int CameraPanSpeed { get; init; } = 12;

    /// <summary>How fast the keys turn the camera, in degrees a second.</summary>
    public int CameraRotateSpeed { get; init; } = 90;

    /// <summary>How far one notch of the wheel zooms: 3 is the camera's own step, 1 a third of it.</summary>
    public int CameraZoomSpeed { get; init; } = 3;

    /// <summary>A new map's side, in tiles.</summary>
    public int DefaultMapSize { get; init; } = 36;

    /// <summary>
    /// The game the Test starts (<see cref="PlayTestLaunch"/>): an exported game's program or the game's Godot project; empty for the game
    /// project beside the editor's, where there is one.
    /// </summary>
    public string GamePath { get; init; } = "";

    /// <summary>The same preferences with every value inside its range.</summary>
    public EditorPreferences Normalized() => this with
    {
        UiScalePercent = Math.Clamp(UiScalePercent, MinUiScale, MaxUiScale),
        AutosaveMinutes = Math.Clamp(AutosaveMinutes, MinAutosaveMinutes, MaxAutosaveMinutes),
        CameraPanSpeed = Math.Clamp(CameraPanSpeed, MinPanSpeed, MaxPanSpeed),
        CameraRotateSpeed = Math.Clamp(CameraRotateSpeed, MinRotateSpeed, MaxRotateSpeed),
        CameraZoomSpeed = Math.Clamp(CameraZoomSpeed, MinZoomSpeed, MaxZoomSpeed),
        DefaultMapSize = Math.Clamp(DefaultMapSize, MinMapSize, MaxMapSize),
        GamePath = (GamePath ?? "").Trim(),
    };
}
