using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The map editor's start zones (MP-4, ADR-197): a thin plate of the player's colour on every tile of a start zone, on the shown level. The game
/// never adds this view. Redrawn when zones are painted, or the level or the ground change.
/// </summary>
public partial class StartZonesView : Node3D
{
    private MapViewContext _context = null!;
    private MultiMeshInstance3D? _plates;

    public void Bind(MapViewContext context)
    {
        _context = context;
        context.Changed += Rebuild;
        TreeExiting += () => context.Changed -= Rebuild;
        this.SubscribeWhileInTree<StartZonesPainted>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<GroundHeightsChanged>(context.Events, _ => Rebuild());
        Rebuild();
    }

    private void Rebuild()
    {
        if (_context is null)
        {
            return;
        }

        _plates?.QueueFree();
        _plates = null;
        var map = _context.State.Map;
        var tiles = map.Positions(_context.Level).Where(p => map.GetStartZone(p) is not null).ToList();
        if (tiles.Count == 0)
        {
            return;
        }

        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.96f, 0.02f, 0.96f),
                Material = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            },
            InstanceCount = tiles.Count,
        };
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            var at = MapSpace.ToGround(map, tile) + (MapSpace.GroundNormal(map, tile) * 0.03f);
            multi.SetInstanceTransform(i, new Transform3D(MapSpace.OnSlope(map, tile), at));
            multi.SetInstanceColor(i, Palette.ForPlayer(map.GetStartZone(tile)!.Value) with { A = 0.35f });
        }

        _plates = new MultiMeshInstance3D { Multimesh = multi };
        AddChild(_plates);
    }
}
