using Godot;

namespace EternalWars.Godot.Shared.Cameras;

/// <summary>
/// Strategy camera (ADR-022): tilted 15–30° from vertical, rotatable ±45° around the
/// vertical axis, zoomable. Zooming in tilts towards 30° for a more cinematic view; zooming out looks
/// more top-down. WASD pans relative to the current rotation, Q/E rotate, mouse wheel zooms.
/// </summary>
public partial class StrategyCamera : Camera3D
{
    /// <summary>The distance the camera starts at: 100 % zoom.</summary>
    private const float DefaultDistance = 16f;

    private Vector3 _focus;
    private float _distance = DefaultDistance;
    private float _yawDegrees;
    private readonly HashSet<Key> _ignored = [];

    public float PanSpeed { get; set; } = 12f;

    public float RotateSpeedDegrees { get; set; } = 90f;

    /// <summary>How far one notch of the wheel moves the camera towards the map or away from it.</summary>
    public float ZoomStep { get; set; } = 1.5f;

    public float MinDistance { get; set; } = 6f;

    public float MaxDistance { get; set; } = 40f;

    public float MinTiltFromVertical { get; set; } = 15f;

    public float MaxTiltFromVertical { get; set; } = 30f;

    public float MaxYawDegrees { get; set; } = 45f;

    /// <summary>The zoom in percent of the distance the camera starts at: closer is more.</summary>
    public int ZoomPercent => Mathf.RoundToInt(DefaultDistance / _distance * 100f);

    /// <summary>The wheel changed the zoom.</summary>
    public event Action? Zoomed;

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

    /// <summary>
    /// Asked every frame: true while the app shows something over the map that takes the keyboard (a dialog, another page), so the
    /// camera keys do nothing. Not set: the keys always work.
    /// </summary>
    public Func<bool>? KeysBlocked { get; set; }

    /// <summary>The keys are not the camera's now: Ctrl is held, a text field is being typed into, or the app blocks them (<see cref="KeysBlocked"/>).</summary>
    private bool KeysAreText() =>
        Input.IsKeyPressed(Key.Ctrl) // Ctrl+A, Ctrl+S: a command's key, not the camera's
        || KeysBlocked?.Invoke() == true
        || GetViewport().GuiGetFocusOwner() is TextEdit || (GetViewport().GuiGetFocusOwner() is LineEdit field && field.IsEditing());

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
        if (KeysAreText())
        {
            return;
        }

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
            var step = pan.Normalized() * PanSpeed * dt * (_distance / DefaultDistance);
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
                MouseButton.WheelUp => -ZoomStep,
                MouseButton.WheelDown => ZoomStep,
                _ => 0f,
            };

            if (zoom != 0f)
            {
                _distance = Mathf.Clamp(_distance + zoom, MinDistance, MaxDistance);
                UpdateTransform();
                Zoomed?.Invoke();
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
