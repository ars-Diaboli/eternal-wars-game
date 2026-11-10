using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// The battle view's animation recipes, each adding its tweeners to a step's tween (<see cref="BattleAnimationQueue"/>): lunges, projectiles,
/// hit flashes with the losses floating up, captions and shudders. Projectiles and captions come from <paramref name="field"/>.
/// </summary>
public sealed class BattleAnimations(BattleFieldBuilder field)
{
    private static readonly Color HitColor = new(1.0f, 0.15f, 0.10f);

    /// <summary>A melee attacker's body lunges a little towards its target and back.</summary>
    public static void Lunge(Tween t, BattleUnitView attacker, Vector3 from, Vector3 to)
    {
        var lunge = (to - from) with { Y = 0 };
        lunge = lunge.LengthSquared() > 0 ? lunge.Normalized() * 0.25f : Vector3.Zero;
        t.TweenProperty(attacker.Body, "position", lunge, 0.12f);
        t.TweenProperty(attacker.Body, "position", Vector3.Zero, 0.12f);
    }

    /// <summary>A projectile flies from <paramref name="from"/> to <paramref name="to"/> in <paramref name="duration"/> seconds.</summary>
    public void Shot(Tween t, Vector3 from, Vector3 to, float duration)
    {
        var projectile = field.Projectile(from);
        t.TweenCallback(Callable.From(() => projectile.Visible = true));
        t.TweenProperty(projectile, "position", to, duration);
        t.TweenCallback(Callable.From(projectile.QueueFree));
    }

    /// <summary>The stack standing at <paramref name="to"/> flashes red while <paramref name="caption"/> (its losses) floats up and fades over it.</summary>
    public void Hit(Tween t, BattleUnitView target, string caption, Vector3 to)
    {
        var text = field.FloatingText(caption, to + (Vector3.Up * ((target.BodyHeight * 2) + 0.4f)));
        t.TweenCallback(Callable.From(() => text.Visible = true));
        t.TweenProperty(target.BodyMaterial, "albedo_color", HitColor, 0.08f);
        t.Parallel().TweenProperty(text, "position", text.Position + (Vector3.Up * 0.6f), 0.45f);
        t.Parallel().TweenProperty(text, "modulate:a", 0f, 0.45f);
        t.Parallel().TweenProperty(target.BodyMaterial, "albedo_color", target.BaseColor, 0.15f).SetDelay(0.08f);
        t.TweenCallback(Callable.From(text.QueueFree));
    }

    /// <summary>The stack flashes red and back, without a caption.</summary>
    public static void Flash(Tween t, BattleUnitView unit)
    {
        t.TweenProperty(unit.BodyMaterial, "albedo_color", HitColor, 0.08f);
        t.TweenProperty(unit.BodyMaterial, "albedo_color", unit.BaseColor, 0.15f);
    }

    /// <summary>
    /// <paramref name="caption"/> rises 0.4 from <paramref name="at"/> over <paramref name="duration"/> seconds, fading after
    /// <paramref name="fadeDelay"/>; <paramref name="color"/> tints it.
    /// </summary>
    public void Caption(Tween t, string caption, Vector3 at, float duration, float fadeDelay, Color? color = null)
    {
        var text = field.FloatingText(caption, at);
        if (color is { } tint)
        {
            text.Modulate = tint;
        }

        t.TweenCallback(Callable.From(() => text.Visible = true));
        t.TweenProperty(text, "position", at + (Vector3.Up * 0.4f), duration);
        t.Parallel().TweenProperty(text, "modulate:a", 0f, duration).SetDelay(fadeDelay);
        t.TweenCallback(Callable.From(text.QueueFree));
    }

    /// <summary>A struck structure squashes to <paramref name="squash"/> and springs back.</summary>
    public static void Shudder(Tween t, Node3D node, Vector3 squash)
    {
        t.TweenProperty(node, "scale", squash, 0.06f);
        t.TweenProperty(node, "scale", Vector3.One, 0.08f);
    }
}
