using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The map editor's overlays (ADR-264), one at a time: a <see cref="MapOverlay"/> from the core drawn as one multimesh per overlay kind, a
/// see-through plate on each tinted tile, or a thin bar along each line (the height's contours), in its band's colour
/// (<see cref="Palette.ForTint"/>). The game never adds this view. Whoever shows an overlay computes it again when the map or the level
/// changes; this view only draws what it is given.
/// </summary>
public partial class OverlayView : Node3D
{
    /// <summary>How far plates and lines float over the ground: above the start zones' plates.</summary>
    private const float Lift = 0.06f;

    private MapViewContext _context = null!;

    /// <summary>Each kind's multimesh, made the first time the kind is shown; only the shown one is visible.</summary>
    private readonly Dictionary<OverlayKind, MultiMeshInstance3D> _meshes = [];

    /// <summary>The overlay drawn, or null for none.</summary>
    public MapOverlay? Overlay { get; private set; }

    public void Bind(MapViewContext context) => _context = context;

    /// <summary>Draws <paramref name="overlay"/> in place of the one drawn before; null shows none.</summary>
    public void Show(MapOverlay? overlay)
    {
        Overlay = overlay;
        foreach (var mesh in _meshes.Values)
        {
            mesh.Visible = false;
        }

        if (overlay is null || _context is null)
        {
            return;
        }

        if (!_meshes.TryGetValue(overlay.Kind, out var instance))
        {
            instance = new MultiMeshInstance3D { Name = overlay.Kind.ToString(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _meshes[overlay.Kind] = instance;
            AddChild(instance);
        }

        instance.Multimesh = overlay.Lines.Count > 0 || overlay.Kind == OverlayKind.Height ? Lines(overlay) : Plates(overlay);
        instance.Visible = true;
    }

    private static MultiMesh NewMultiMesh(Vector3 size, int count) => new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        UseColors = true,
        Mesh = new BoxMesh
        {
            Size = size,
            Material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        },
        InstanceCount = count,
    };

    /// <summary>A plate on every tinted tile, on its slope.</summary>
    private MultiMesh Plates(MapOverlay overlay)
    {
        var map = _context.State.Map;
        var colours = overlay.Legend.Select(b => Palette.ForTint(b.Tint) with { A = 0.5f }).ToArray();
        var cells = overlay.Cells.Where(c => map.Contains(c.Tile) && c.Band >= 0 && c.Band < colours.Length).ToList();
        var multi = NewMultiMesh(new Vector3(0.94f, 0.02f, 0.94f), cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            var tile = cells[i].Tile;
            multi.SetInstanceTransform(i, new Transform3D(MapSpace.OnSlope(map, tile), MapSpace.ToGround(map, tile) + (MapSpace.GroundNormal(map, tile) * Lift)));
            multi.SetInstanceColor(i, colours[cells[i].Band]);
        }

        return multi;
    }

    /// <summary>A thin bar along every line, on the ground; the bands after the first are drawn thicker.</summary>
    private MultiMesh Lines(MapOverlay overlay)
    {
        var map = _context.State.Map;
        var colours = overlay.Legend.Select(b => Palette.ForTint(b.Tint)).ToArray();
        var lines = overlay.Lines.Where(l => l.Band >= 0 && l.Band < colours.Length).ToList();
        var multi = NewMultiMesh(new Vector3(1, 0.02f, 1), lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var (from, to) = (Point(line.X1, line.Y1), Point(line.X2, line.Y2));
            var along = to - from;
            var length = along.Length();
            var x = length > 1e-5f ? along / length : Vector3.Right;
            var z = x.Cross(Vector3.Up).Normalized();
            var y = z.Cross(x);
            var width = line.Band == 0 ? 0.04f : 0.08f;
            multi.SetInstanceTransform(i, new Transform3D(new Basis(x * (length + width), y, z * width), ((from + to) / 2) + (Vector3.Up * Lift)));
            multi.SetInstanceColor(i, colours[line.Band]);
        }

        return multi;

        // Corner coordinates: tile (x, y)'s centre is at (x + 0.5, y + 0.5), and at world (x, y).
        Vector3 Point(float cx, float cy)
        {
            var (wx, wz) = ((cx - 0.5f) * MapSpace.TileSize, (cy - 0.5f) * MapSpace.TileSize);
            return new Vector3(wx, MapSpace.GroundY(map, overlay.Level, wx, wz), wz);
        }
    }
}
