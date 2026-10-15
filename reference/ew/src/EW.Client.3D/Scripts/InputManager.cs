using EW.Scripts.Heroes;
using EW.Scripts.WorldMap;
using Godot;

namespace EW.Scripts;

public partial class InputManager : Node
{
    private static readonly Vector2I InvalidPreviewTile = new(-999, -999);

    [Export] public WorldMapSettings WorldMapSettings { get; set; }
    [Export] public Heroes.Hero OverworldHero { get; set; }
    [Export] public TurnManager ActiveTurnManager { get; set; }
    [Export] public PathVisualizer Visualizer { get; set; }
    [Export] public Label MovementLabel { get; set; }

    private Camera3D _camera;
    private WorldMapController _worldMapController;
    private Vector2I _confirmedPreviewTile = InvalidPreviewTile;
    private bool _hasActivePreview = false;
    private int _lastPreviewSignature;

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera3D();

        if (WorldMapSettings == null)
        {
            WorldMapSettings = ResolveWorldMapSettings();
        }

        _worldMapController = ResolveWorldMapController();
    }

    public override void _Process(double delta)
    {
        UpdateMovementUI();

        if (IsTownScreenOpen())
        {
            CancelPreview();
            return;
        }

        // If it's not the player's turn, force-clear any visual paths
        if (ActiveTurnManager != null && !ActiveTurnManager.CanControlHero(OverworldHero))
        {
            CancelPreview();
            return;
        }

        // Keep updating the path layout visually if a tile has been selected
        if (_hasActivePreview && OverworldHero != null)
        {
            var pathPoints = OverworldHero.PreviewFullPathToTile(_confirmedPreviewTile);
            if (pathPoints.Count > 1)
            {
                int previewSignature = ComputePathSignature(pathPoints);
                if (previewSignature != _lastPreviewSignature)
                {
                    Visualizer?.DrawComplexPath(pathPoints);
                    _lastPreviewSignature = previewSignature;
                }
            }
            else
            {
                CancelPreview();
            }
        }
    }

    private void UpdateMovementUI()
    {
        if (MovementLabel != null && OverworldHero != null)
        {
            MovementLabel.Text = $"Movement Points: {OverworldHero.CurrentMovementPoints} / {OverworldHero.MaxMovementPoints}";
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsTownScreenOpen())
        {
            return;
        }

        // Block interaction entirely during the opponent's turn
        if (ActiveTurnManager != null && !ActiveTurnManager.CanControlHero(OverworldHero)) return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            // --- RIGHT CLICK: CANCEL PREVIEW ---
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                if (OverworldHero != null)
                {
                    var remainingPath = OverworldHero.StopMovementAtCurrentCell();
                    if (remainingPath.Count > 1)
                    {
                        _hasActivePreview = false;
                        _confirmedPreviewTile = InvalidPreviewTile;
                        _lastPreviewSignature = ComputePathSignature(remainingPath);
                        Visualizer?.DrawComplexPath(remainingPath);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }

                CancelPreview();
                GetViewport().SetInputAsHandled();
                return;
            }

            // --- LEFT CLICK: PREVIEW OR MOVE ---
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                Vector3? hitPoint = RaycastFromMousePosition(mouseButton.Position);

                if (!hitPoint.HasValue || WorldMapSettings == null)
                {
                    return;
                }

                Vector2I clickedTile = WorldMapSettings.WorldToTilePosition(hitPoint.Value);

                if (!WorldMapSettings.IsInsideMap(clickedTile))
                {
                    CancelPreview();
                    return;
                }

                // SECOND CLICK: If clicking the exact same tile twice, execute movement.
                if (_hasActivePreview && clickedTile == _confirmedPreviewTile)
                {
                    GD.Print($"Moving to confirmed tile: {clickedTile}");
                    OverworldHero?.MoveToTile(clickedTile);
                    CancelPreview();
                }
                // FIRST CLICK: Setup path preview line.
                else
                {
                    var pathPoints = OverworldHero?.PreviewFullPathToTile(clickedTile);

                    if (pathPoints != null && pathPoints.Count > 1)
                    {
                        _confirmedPreviewTile = clickedTile;
                        _hasActivePreview = true;
                        _lastPreviewSignature = ComputePathSignature(pathPoints);
                        Visualizer?.DrawComplexPath(pathPoints);
                        GD.Print($"Path preview established to {clickedTile}. Click again to confirm.");
                    }
                }
            }
        }
    }

    private void CancelPreview()
    {
        _hasActivePreview = false;
        _confirmedPreviewTile = InvalidPreviewTile;
        _lastPreviewSignature = 0;
        Visualizer?.ClearPath();
    }

    private static int ComputePathSignature(System.Collections.Generic.IEnumerable<PathWaypoint> pathPoints)
    {
        unchecked
        {
            int hash = 17;

            foreach (var waypoint in pathPoints)
            {
                hash = (hash * 31) + waypoint.WorldPosition.X.GetHashCode();
                hash = (hash * 31) + waypoint.WorldPosition.Y.GetHashCode();
                hash = (hash * 31) + waypoint.WorldPosition.Z.GetHashCode();
                hash = (hash * 31) + waypoint.CanReachThisTurn.GetHashCode();
            }

            return hash;
        }
    }

    private WorldMapSettings ResolveWorldMapSettings()
    {
        Node currentScene = GetTree().CurrentScene;
        if (currentScene is WorldMapSettings worldMapSettings)
        {
            return worldMapSettings;
        }

        return currentScene?.GetNodeOrNull<WorldMapSettings>("WorldMapSettings");
    }

    private WorldMapController ResolveWorldMapController()
    {
        return GetTree().CurrentScene?.GetNodeOrNull<WorldMapController>("WorldMapController");
    }

    private bool IsTownScreenOpen()
    {
        _worldMapController ??= ResolveWorldMapController();
        return _worldMapController?.IsTownScreenOpen() == true;
    }

    private Vector3? RaycastFromMousePosition(Vector2 mouseScreenPos)
    {
        if (_camera == null)
        {
            _camera = GetViewport().GetCamera3D();
        }

        if (_camera == null) return null;

        var spaceState = GetViewport().GetWorld3D().DirectSpaceState;
        Vector3 rayOrigin = _camera.ProjectRayOrigin(mouseScreenPos);
        Vector3 rayEnd = rayOrigin + _camera.ProjectRayNormal(mouseScreenPos) * 2000.0f;

        var physicsQuery = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
        var collisionResult = spaceState.IntersectRay(physicsQuery);

        if (collisionResult.Count > 0)
        {
            return (Vector3)collisionResult["position"];
        }

        return null;
    }
}
