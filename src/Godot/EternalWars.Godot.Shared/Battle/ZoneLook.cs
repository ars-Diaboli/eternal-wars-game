using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>The placeholder colour a field zone (ADR-149) is drawn in, by its spell; translucent so the stacks in it stay visible.</summary>
public static class ZoneLook
{
    public static Color Of(string spell) => spell switch
    {
        "chaos.fire-wall" => new Color(1.0f, 0.45f, 0.1f, 0.55f),
        "light.consecrate" => new Color(1.0f, 0.9f, 0.45f, 0.4f),
        "nature.thornwall" => new Color(0.25f, 0.55f, 0.2f, 0.7f),
        "death.bone-wall" => new Color(0.85f, 0.82f, 0.72f, 0.8f),
        "shadow.darkness" => new Color(0.12f, 0.02f, 0.25f, 0.8f),
        _ => new Color(0.6f, 0.3f, 0.9f, 0.45f),
    };

    /// <summary>How high the zone is drawn, in world units: walls stand up, the others lie on the ground.</summary>
    public static float Height(string spell) => spell switch
    {
        "nature.thornwall" or "death.bone-wall" => 0.45f,
        "chaos.fire-wall" => 0.3f,
        _ => 0.02f,
    };
}
