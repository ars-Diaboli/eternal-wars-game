using Godot;

namespace EternalWars.Godot.Shared.Cameras;

/// <summary>
/// Strategy camera (docs/project/decisions.md ADR-022): tilted 15–30° from vertical, rotatable ±45° around the
/// vertical axis, zoomable. Zooming in tilts towards 30° for a more cinematic view; zooming out looks
/// more top-down. WASD pans relative to the current rotation, Q/E rotate, mouse wheel zooms.
/// </summary>
public partial class StrategyCamera : Camera3D
{
    private Vector3 _focus;
    private float _distance = 16f;
    private float _yawDegrees;
    private readonly HashSet<Key> _ignored = [];

    public float PanSpeed { get; set; } = 12f;

    public float RotateSpeedDegrees { get; set; } = 90f;

    public float MinDistance { get; set; } = 6f;

    public float MaxDistance { get; set; } = 40f;

    public float MinTiltFromVertical { get; set; } = 15f;

    public float MaxTiltFromVertical { get; set; } = 30f;

    public float MaxYawDegrees { get; set; } = 45f;

    public void FocusOn(Vector3 point)
    {
        _focus = point;
        UpdateTransform();
    }

    public override void _Ready() => UpdateTransform();

    /// <summary>
    /// The camera keys held now are ignored until they are let go: a key that also opens or closes a screen (Q, the quest log) does not turn the
    /// camera as well.
    /// </summary>
    public void IgnoreHeldKeys()
    {
        foreach (var key in new[] { Key.Q, Key.E, Key.W, Key.A, Key.S, Key.D }.Where(Input.IsKeyPressed))
        {
            _ignored.Add(key);
        }
    }

    private bool Held(Key key)
    {
        if (!Input.IsKeyPressed(key))
        {
            _ignored.Remove(key);
            return false;
        }

        return !_ignored.Contains(key);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var changed = false;

        var rotate = (Held(Key.E) ? 1f : 0f) - (Held(Key.Q) ? 1f : 0f);
        if (rotate != 0f)
        {
            _yawDegrees = Mathf.Clamp(_yawDegrees + (rotate * RotateSpeedDegrees * dt), -MaxYawDegrees, MaxYawDegrees);
            changed = true;
        }

        var pan = Vector2.Zero;
        if (Held(Key.W)) pan.Y += 1;
        if (Held(Key.S)) pan.Y -= 1;
        if (Held(Key.A)) pan.X -= 1;
        if (Held(Key.D)) pan.X += 1;
        if (pan != Vector2.Zero)
        {
            var forward = Vector3.Forward.Rotated(Vector3.Up, Mathf.DegToRad(_yawDegrees));
            var right = forward.Cross(Vector3.Up);
            var step = pan.Normalized() * PanSpeed * dt * (_distance / 16f);
            _focus += (forward * step.Y) + (right * step.X);
            changed = true;
        }

        if (changed)
        {
            UpdateTransform();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } wheel)
        {
            var zoom = wheel.ButtonIndex switch
            {
                MouseButton.WheelUp => -1.5f,
                MouseButton.WheelDown => 1.5f,
                _ => 0f,
            };

            if (zoom != 0f)
            {
                _distance = Mathf.Clamp(_distance + zoom, MinDistance, MaxDistance);
                UpdateTransform();
            }
        }
    }

    private void UpdateTransform()
    {
        if (!IsInsideTree())
        {
            return;
        }

        // Close = more tilted (MaxTilt), far = more top-down (MinTilt).
        var zoomT = (_distance - MinDistance) / (MaxDistance - MinDistance);
        var tilt = Mathf.DegToRad(Mathf.Lerp(MaxTiltFromVertical, MinTiltFromVertical, zoomT));
        var offset = new Vector3(0, Mathf.Cos(tilt), Mathf.Sin(tilt)).Rotated(Vector3.Up, Mathf.DegToRad(_yawDegrees)) * _distance;
        LookAtFromPosition(_focus + offset, _focus, Vector3.Up);
    }
}
