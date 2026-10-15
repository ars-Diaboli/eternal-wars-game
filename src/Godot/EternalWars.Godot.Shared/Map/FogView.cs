using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Fog over one level for the context's viewer: unexplored tiles are black, explored tiles out of sight
/// (refog mode only) are dimmed. Hidden entirely when there is no viewer (editor).
/// </summary>
public partial class FogView : Node3D
{
    private static readonly Color Unexplored = new(0.02f, 0.02f, 0.03f, 1f);
    private static readonly Color OutOfSight = new(0.02f, 0.02f, 0.03f, 0.45f);

    private MapViewContext _context = null!;
    private int _level;
    private MultiMesh _fog = null!;

    public void Bind(MapViewContext context, int level)
    {
        _context = context;
        _level = level;
        var map = context.State.Map;
        _fog = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new BoxMesh
            {
                Size = new Vector3(MapSpace.TileSize, 0.05f, MapSpace.TileSize),
                Material = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            },
            InstanceCount = map.Width * map.Height,
        };
        AddChild(new MultiMeshInstance3D { Multimesh = _fog, Name = "Fog", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        RefreshAll();

        context.Changed += RefreshAll;
        TreeExiting += () => context.Changed -= RefreshAll;
        this.SubscribeWhileInTree<TilesRevealed>(context.Events, e => RefreshFor(e.Player, e.Positions));
        this.SubscribeWhileInTree<SightChanged>(context.Events, e => RefreshFor(e.Player, e.Entered.Concat(e.Left)));
    }

    private void RefreshFor(PlayerId player, IEnumerable<MapPosition> positions)
    {
        if (player == _context.Viewer)
        {
            Refresh(positions);
        }
    }

    private void RefreshAll()
    {
        Visible = _context.Viewer is not null;
        Refresh(_context.State.Map.Positions(_level));
    }

    private void Refresh(IEnumerable<MapPosition> positions)
    {
        if (_context.Viewer is null)
        {
            return;
        }

        var width = _context.State.Map.Width;
        foreach (var position in positions.Where(p => p.Level == _level))
        {
            var color = !_context.IsExplored(position) ? Unexplored : !_context.IsInSight(position) ? OutOfSight : Colors.Transparent;
            var scale = color.A == 0 ? Vector3.Zero : Vector3.One;
            var index = (position.Y * width) + position.X;
            _fog.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(scale), MapSpace.ToWorld(position) + (Vector3.Up * 0.6f)));
            _fog.SetInstanceColor(index, color);
        }
    }
}
