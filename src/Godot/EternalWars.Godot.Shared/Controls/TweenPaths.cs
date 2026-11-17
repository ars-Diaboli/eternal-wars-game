using Godot;

namespace EternalWars.Godot.Shared.Controls;

/// <summary>The property paths the views tween, made once: a string passed to <c>TweenProperty</c> becomes a new <see cref="NodePath"/> each call.</summary>
public static class TweenPaths
{
    public static readonly NodePath Position = "position";

    public static readonly NodePath PositionY = "position:y";

    public static readonly NodePath Scale = "scale";

    public static readonly NodePath AlbedoColor = "albedo_color";

    public static readonly NodePath ModulateAlpha = "modulate:a";
}
