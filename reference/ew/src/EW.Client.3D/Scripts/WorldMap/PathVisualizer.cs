using System.Linq;
using EW.Scripts.Heroes;

namespace EW.Scripts;

using EW.Scripts.WorldMap;
using Godot;
using System.Collections.Generic;

public partial class PathVisualizer : Node3D
{
    [Export] public Color AllowedColor { get; set; } = Colors.MediumSeaGreen;
    [Export] public Color BlockedColor { get; set; } = Colors.Red;
    [Export] public float MarkerSize { get; set; } = 0.4f;
    [Export] public float HeightOffset { get; set; } = 0.3f; // Float clearly above the grid surface

    private List<MeshInstance3D> _activeMarkers = new();
    private SphereMesh _markerMesh;
    private StandardMaterial3D _allowedMaterial;
    private StandardMaterial3D _blockedMaterial;

    public override void _Ready()
    {
        EnsureSharedResources();
    }

    public void DrawComplexPath(List<PathWaypoint> waypoints)
    {
        EnsureSharedResources();
        ClearPath(); // Wipe out old visual markers

        if (waypoints == null || waypoints.Count < 2) return;

        // Skip index 0 because that's the exact tile the Hero is already occupying
        foreach (var wp in waypoints.Skip(1))
        {
            // 1. Create a brand new visual 3D node on the fly
            MeshInstance3D marker = new MeshInstance3D();
            marker.Mesh = _markerMesh;
            marker.MaterialOverride = wp.CanReachThisTurn ? _allowedMaterial : _blockedMaterial;

            // 4. Position the marker directly in the center of the target grid cell space
            AddChild(marker);
            marker.GlobalPosition = GetTerrainAdjustedPosition(wp.WorldPosition);

            // Keep track of it so we can delete it later
            _activeMarkers.Add(marker);
        }
    }

    private Vector3 GetTerrainAdjustedPosition(Vector3 worldPosition)
    {
        worldPosition.Y = this.GetTerrainHeightAt(worldPosition) + HeightOffset;
        return worldPosition;
    }

    private void EnsureSharedResources()
    {
        _markerMesh ??= CreateMarkerMesh();
        _allowedMaterial ??= CreateMarkerMaterial(AllowedColor);
        _blockedMaterial ??= CreateMarkerMaterial(BlockedColor);

        _markerMesh.Radius = MarkerSize * 0.5f;
        _markerMesh.Height = MarkerSize;
        _allowedMaterial.AlbedoColor = AllowedColor;
        _blockedMaterial.AlbedoColor = BlockedColor;
    }

    public void ClearPath()
    {
        foreach (MeshInstance3D marker in _activeMarkers)
        {
            if (IsInstanceValid(marker))
            {
                marker.QueueFree();
            }
        }
        _activeMarkers.Clear();
    }

    private static StandardMaterial3D CreateMarkerMaterial(Color color)
    {
        StandardMaterial3D mat = new StandardMaterial3D();
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.AlbedoColor = color;
        mat.NoDepthTest = true; // FORCES the dot to render on top of floor blocks, no matter what!
        return mat;
    }

    private SphereMesh CreateMarkerMesh()
    {
        SphereMesh sphere = new SphereMesh();
        sphere.Radius = MarkerSize * 0.5f;
        sphere.Height = MarkerSize;
        return sphere;
    }
}
