using Godot;

namespace EternalWars.Godot.Shared.Cameras;

/// <summary>Where the editor's camera looks from: what a bookmark keeps (ADR-260).</summary>
public sealed record EditorCameraView(Vector3 Focus, float Distance, float YawDegrees, bool TopDown);

/// <summary>
/// The map editor's camera (ADR-260), beside the game's <see cref="StrategyCamera"/>: tilted like it (15–30° from vertical, more tilted
/// closer) or straight down (<see cref="TopDown"/>), and it goes far enough out to show the largest map. WASD pans relative to the
/// rotation, Q/E rotate, the wheel zooms by a share of the distance, and dragging with the middle button moves the map with the pointer.
/// <see cref="Frame"/> fits an area of the map into the view: the whole map (Home) or the selection (F).
/// </summary>
public partial class EditorCamera : Camera3D
{
    /// <summary>The distance the camera starts at: 100 % zoom.</summary>
    private const float DefaultDistance = 16f;

    /// <summary>The distances between which the tilt goes from the most (close) to the least (far); beyond them it stays.</summary>
    private const float TiltNear = 6f, TiltFar = 40f;

    /// <summary>The view's room around a framed area, in tiles on each side.</summary>
    private const float FrameMargin = 2f;

    private Vector3 _focus;
    private float _distance = DefaultDistance;
    private float _yawDegrees;
    private bool _topDown;
    private Vector3? _grabbed;
    private readonly HashSet<Key> _ignored = [];

    public float PanSpeed { get; set; } = 12f;

    public float RotateSpeedDegrees { get; set; } = 90f;

    /// <summary>
    /// How strongly one notch of the wheel zooms: at the camera's own 1.5 a notch divides or multiplies its distance by 1.15, so a notch
    /// feels the same close up and over a whole large map.
    /// </summary>
    public float ZoomStep { get; set; } = 1.5f;

    public float MinDistance { get; set; } = 3f;

    public float MaxDistance { get; set; } = 400f;

    public float MinTiltFromVertical { get; set; } = 15f;

    public float MaxTiltFromVertical { get; set; } = 30f;

    public float MaxYawDegrees { get; set; } = 45f;

    /// <summary>The zoom in percent of the distance the camera starts at: closer is more.</summary>
    public int ZoomPercent => Mathf.RoundToInt(DefaultDistance / _distance * 100f);

    /// <summary>Straight down instead of tilted.</summary>
    public bool TopDown
    {
        get => _topDown;
        set
        {
            _topDown = value;
            UpdateTransform();
        }
    }

    /// <summary>Where it looks from now, for a bookmark.</summary>
    public EditorCameraView View => new(_focus, _distance, _yawDegrees, _topDown);

    /// <summary>The zoom changed: the wheel, <see cref="Frame"/> or <see cref="Show"/>.</summary>
    public event Action? Zoomed;

    /// <summary>
    /// Asked every frame: true while the app shows something over the map that takes the input (a dialog, another page), so the camera
    /// keys, the wheel and the middle button do nothing. Not set: they always work.
    /// </summary>
    public Func<bool>? KeysBlocked { get; set; }

    /// <summary>The camera looks at <paramref name="point"/> from where it is, its distance and rotation kept.</summary>
    public void FocusOn(Vector3 point)
    {
        _focus = point;
        UpdateTransform();
    }

    /// <summary>Looks from a bookmarked view again.</summary>
    public void Show(EditorCameraView view)
    {
        (_focus, _distance, _yawDegrees, _topDown) = (view.Focus, Mathf.Clamp(view.Distance, MinDistance, MaxDistance), view.YawDegrees, view.TopDown);
        UpdateTransform();
        Zoomed?.Invoke();
    }

    /// <summary>
    /// Fits the ground between <paramref name="min"/> and <paramref name="max"/> (world corners, the height of <paramref name="min"/> taken as
    /// the ground's) into <paramref name="view"/> (the part of the screen the map is seen in, between the panels; the whole viewport when
    /// null) with a little room around it: the camera comes as close as that allows and the area's middle shows in the middle of the view.
    /// </summary>
    public void Frame(Vector3 min, Vector3 max, Rect2? view = null)
    {
        if (!IsInsideTree())
        {
            return;
        }

        var screen = GetViewport().GetVisibleRect().Size;
        var shown = view is { Size.X: > 0, Size.Y: > 0 } given ? given : new Rect2(Vector2.Zero, screen);
        var half = (new Vector2(Math.Abs(max.X - min.X), Math.Abs(max.Z - min.Z)) / 2) + (Vector2.One * FrameMargin);

        // The yaw turns the area in the view: its extent along the view's sides is what has to fit, in the share of the screen the view takes.
        var yaw = Mathf.DegToRad(_yawDegrees);
        var (cos, sin) = (Math.Abs(Mathf.Cos(yaw)), Math.Abs(Mathf.Sin(yaw)));
        var across = (half.X * cos) + (half.Y * sin);
        var along = (half.X * sin) + (half.Y * cos);
        var tan = Mathf.Tan(Mathf.DegToRad(Fov) / 2); // the field of view is the screen's height
        var tanHigh = tan * shown.Size.Y / screen.Y;
        var tanWide = tan * shown.Size.X / screen.Y;
        var straightDown = Mathf.Clamp(Math.Max(along / tanHigh, across / tanWide), MinDistance, MaxDistance);

        // Straight down that distance is exact. Tilted, the near edge shows larger than the far one, so the distance is searched: the
        // nearest at which the area's corners, with the room, all show inside the view once its middle is centred.
        var middle = new Vector3((min.X + max.X) / 2, min.Y, (min.Z + max.Z) / 2);
        Vector3[] corners =
        [
            middle + new Vector3(-half.X, 0, -half.Y), middle + new Vector3(half.X, 0, -half.Y),
            middle + new Vector3(-half.X, 0, half.Y), middle + new Vector3(half.X, 0, half.Y),
        ];
        bool Fits(float distance)
        {
            _distance = distance;
            Centre(middle, shown);
            return corners.All(c => !IsPositionBehind(c) && shown.Grow(0.5f).HasPoint(UnprojectPosition(c)));
        }

        if (!_topDown && !Fits(straightDown))
        {
            var (near, far) = (straightDown, straightDown);
            while (far < MaxDistance && !Fits(far))
            {
                (near, far) = (far, Math.Min(far * 1.5f, MaxDistance));
            }

            for (var step = 0; step < 24 && far - near > 0.01f; step++)
            {
                var mid = (near + far) / 2;
                if (Fits(mid))
                {
                    far = mid;
                }
                else
                {
                    near = mid;
                }
            }

            straightDown = far;
        }

        _distance = straightDown;
        Centre(middle, shown);
        Zoomed?.Invoke();
    }

    /// <summary>
    /// Looks at <paramref name="point"/> (its height taken as the ground's) so that it shows in the middle of <paramref name="view"/> (the
    /// whole viewport when null), the distance and the turn kept.
    /// </summary>
    public void Centre(Vector3 point, Rect2? view = null)
    {
        _focus = point;
        UpdateTransform();
        if (!IsInsideTree() || view is not { Size.X: > 0, Size.Y: > 0 } shown)
        {
            return;
        }

        // A move along the ground's plane moves every ground point under the screen by the same amount, so one step is exact.
        if (GroundAt(shown.GetCenter()) is { } under)
        {
            _focus += point - under;
            UpdateTransform();
        }
    }

    public override void _Ready() => UpdateTransform();

    /// <summary>
    /// The camera keys held now are ignored until they are let go: a key that also runs a command (E, the Erase tool) does not turn the
    /// camera as well.
    /// </summary>
    public void IgnoreHeldKeys()
    {
        foreach (var key in new[] { Key.Q, Key.E, Key.W, Key.A, Key.S, Key.D }.Where(Input.IsKeyPressed))
        {
            _ignored.Add(key);
        }
    }

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
        if (@event is not InputEventMouseButton { Pressed: true } press || KeysBlocked?.Invoke() == true)
        {
            return;
        }

        switch (press.ButtonIndex)
        {
            case MouseButton.WheelUp or MouseButton.WheelDown:
                var share = 1f + (ZoomStep / 10f);
                _distance = Mathf.Clamp(press.ButtonIndex == MouseButton.WheelUp ? _distance / share : _distance * share, MinDistance, MaxDistance);
                UpdateTransform();
                Zoomed?.Invoke();
                break;
            case MouseButton.Middle:
                _grabbed = GroundAt(press.Position);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    /// <summary>
    /// While the middle button is held after a press on the map, the ground point it grabbed stays under the pointer. Followed here, before
    /// the panels see the events, so the drag goes on over them and ends wherever the button is let go.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_grabbed is not { } grabbed)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: false }:
                _grabbed = null;
                break;
            case InputEventMouseMotion motion when !motion.ButtonMask.HasFlag(MouseButtonMask.Middle):
                _grabbed = null; // let go where no release reached the window
                break;
            case InputEventMouseMotion motion when GroundAt(motion.Position) is { } under:
                _focus += grabbed - under;
                UpdateTransform();
                break;
        }
    }

    /// <summary>
    /// The point of the plane at the focus's height under a screen point (the minimap draws the view's frame with it); null when the ray does
    /// not come down to it.
    /// </summary>
    public Vector3? GroundAt(Vector2 screen)
    {
        var origin = ProjectRayOrigin(screen);
        var direction = ProjectRayNormal(screen);
        if (direction.Y >= -1e-3f)
        {
            return null;
        }

        return origin + (direction * ((_focus.Y - origin.Y) / direction.Y));
    }

    private void UpdateTransform()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var yaw = Mathf.DegToRad(_yawDegrees);
        if (_topDown)
        {
            // Straight down, with the map's far side (north, -Z) at the top of the screen as in the tilted view.
            LookAtFromPosition(_focus + (Vector3.Up * _distance), _focus, Vector3.Forward.Rotated(Vector3.Up, yaw));
            return;
        }

        // Close = more tilted (MaxTilt), far = more top-down (MinTilt).
        var zoomT = Mathf.Clamp((_distance - TiltNear) / (TiltFar - TiltNear), 0f, 1f);
        var tilt = Mathf.DegToRad(Mathf.Lerp(MaxTiltFromVertical, MinTiltFromVertical, zoomT));
        var offset = new Vector3(0, Mathf.Cos(tilt), Mathf.Sin(tilt)).Rotated(Vector3.Up, yaw) * _distance;
        LookAtFromPosition(_focus + offset, _focus, Vector3.Up);
    }
}
