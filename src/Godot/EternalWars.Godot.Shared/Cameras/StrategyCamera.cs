using Godot;

namespace EternalWars.Godot.Shared.Cameras;

/// <summary>
/// HoMM-style angled camera: pans over the map with WASD / arrow keys / screen edges,
/// zooms with the mouse wheel. Attach as a child of the scene root.
/// </summary>
public partial class StrategyCamera : Camera3D
{
    private const float PitchDegrees = 55f;

    private Vector3 _focus;
    private float _distance = 14f;

    public float PanSpeed { get; set; } = 12f;

    public float MinDistance { get; set; } = 5f;

    public float MaxDistance { get; set; } = 40f;

    public void FocusOn(Vector3 point)
    {
        _focus = point;
        UpdateTransform();
    }

    public override void _Ready() => UpdateTransform();

    public override void _Process(double delta)
    {
        var pan = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) pan.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) pan.Y += 1;
        if (Input.IsKeyPressed(Key.A)) pan.X -= 1;
        if (Input.IsKeyPressed(Key.D)) pan.X += 1;

        if (pan != Vector2.Zero)
        {
            var step = pan.Normalized() * PanSpeed * (float)delta * (_distance / 14f);
            _focus += new Vector3(step.X, 0, step.Y);
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

        var pitch = Mathf.DegToRad(PitchDegrees);
        var offset = new Vector3(0, Mathf.Sin(pitch), Mathf.Cos(pitch)) * _distance;
        LookAtFromPosition(_focus + offset, _focus, Vector3.Up);
    }
}
