namespace EW.Scripts;

using EW.Scripts.Messaging;
using Godot;

public sealed class TownScreenVisibilityChanged : IGameEvent
{
    public bool IsOpen { get; init; }
}

public partial class CameraController : Node3D, IHandleEvent<TownScreenVisibilityChanged>
{
    [Export] public float MoveSpeed { get; set; } = 20.0f;
    [Export] public float ZoomSpeed { get; set; } = 3.0f;
    [Export] public Vector2 ZoomLimits { get; set; } = new Vector2(5.0f, 30.0f);

    private Camera3D _camera;
    private float _targetZoomHeight;
    private bool _isTownScreenOpen;

    public override void _Ready()
    {
        // Fetch our nested camera node reference on startup
        _camera = GetNode<Camera3D>("Camera3D");

        // Use the initial inspector height position as our default starting zoom height
        _targetZoomHeight = _camera.Position.Y;
        Hub.Instance?.Subscribe(this);
    }

    public override void _ExitTree()
    {
        Hub.Instance?.Unsubscribe(this);
    }

    public override void _Process(double delta)
    {
        if (IsTownScreenOpen())
        {
            return;
        }

        float fDelta = (float)delta;
        HandleKeyboardMovement(fDelta);
        HandleSmoothZoomLerp(fDelta);
    }

    private void HandleKeyboardMovement(float delta)
    {
        Vector3 movementInput = Vector3.Zero;

        // Use Godot's built-in default UI map inputs (Arrows / WASD mapping keys)
        if (Input.IsActionPressed("ui_right")) movementInput.X += 1;
        if (Input.IsActionPressed("ui_left")) movementInput.X -= 1;
        if (Input.IsActionPressed("ui_down")) movementInput.Z += 1;
        if (Input.IsActionPressed("ui_up")) movementInput.Z -= 1;

        if (movementInput != Vector3.Zero)
        {
            // Normalize inputs so diagonal travel isn't accidentally faster
            movementInput = movementInput.Normalized();

            // Translate the entire base crane rig cleanly across the world's ground plane
            GlobalTranslate(movementInput * MoveSpeed * delta);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsTownScreenOpen())
        {
            return;
        }

        // Catch mouse scroll wheels to safely modify our destination zoom target heights
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                _targetZoomHeight -= ZoomSpeed;
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                _targetZoomHeight += ZoomSpeed;
            }

            // Lock our target zoom calculations safely inside our limits bounds
            _targetZoomHeight = Mathf.Clamp(_targetZoomHeight, ZoomLimits.X, ZoomLimits.Y);
        }
    }

    private void HandleSmoothZoomLerp(float delta)
    {
        Vector3 currentCameraPos = _camera.Position;

        // Linear Interpolation (Lerp) gives a lovely smooth slide behavior rather than instant jumps
        float calculatedY = Mathf.Lerp(currentCameraPos.Y, _targetZoomHeight, 10.0f * delta);

        // To maintain our perfect isometric 45-degree angle, Z must match our Y offset depth exactly
        float calculatedZ = Mathf.Lerp(currentCameraPos.Z, _targetZoomHeight, 10.0f * delta);

        _camera.Position = new Vector3(currentCameraPos.X, calculatedY, calculatedZ);
    }

    public void Handle(TownScreenVisibilityChanged eventData)
    {
        _isTownScreenOpen = eventData?.IsOpen == true;
    }

    private bool IsTownScreenOpen()
    {
        return _isTownScreenOpen;
    }
}
