using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The map editor's start zones (MP-4, ADR-197): a thin plate of the player's colour on every tile of a start zone, on the shown level. The game
/// never adds this view. Redrawn when zones are painted or the level changes; a change of the ground lays again only the plates on the tiles it
/// reaches.
/// </summary>
public partial class StartZonesView : Node3D
{
    private MapViewContext _context = null!;
    private MultiMeshInstance3D? _plates;

    /// <summary>Each plate's instance in <see cref="_plates"/>, by tile.</summary>
    private readonly Dictionary<MapPosition, int> _plateOf = [];

    public void Bind(MapViewContext context)
    {
        _context = context;
        context.LevelChanged += Rebuild;
        TreeExiting += () => context.LevelChanged -= Rebuild;
        this.SubscribeWhileInTree<StartZonesPainted>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<GroundHeightsChanged>(context.Events, e =>
        {
            if (e.Level == _context.Level)
            {
                Relay(ReliefSurface.Reach(e.Area));
            }
        });
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
        _plateOf.Clear();
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
            multi.SetInstanceTransform(i, Plate(tile));
            multi.SetInstanceColor(i, Palette.ForPlayer(map.GetStartZone(tile)!.Value) with { A = 0.35f });
            _plateOf[tile] = i;
        }

        _plates = new MultiMeshInstance3D { Multimesh = multi };
        AddChild(_plates);
    }

    /// <summary>Lays again the plates on the tiles with a corner in <paramref name="area"/> (they lie on their tile's slope).</summary>
    private void Relay(CornerArea area)
    {
        if (_plates?.Multimesh is not { } multi || area.IsEmpty)
        {
            return;
        }

        var map = _context.State.Map;
        for (var y = Math.Max(area.Top - 1, 0); y <= Math.Min(area.Bottom, map.Height - 1); y++)
        {
            for (var x = Math.Max(area.Left - 1, 0); x <= Math.Min(area.Right, map.Width - 1); x++)
            {
                var tile = new MapPosition(x, y, _context.Level);
                if (_plateOf.TryGetValue(tile, out var plate))
                {
                    multi.SetInstanceTransform(plate, Plate(tile));
                }
            }
        }
    }

    private Transform3D Plate(MapPosition tile)
    {
        var map = _context.State.Map;
        return new Transform3D(MapSpace.OnSlope(map, tile), MapSpace.ToGround(map, tile) + (MapSpace.GroundNormal(map, tile) * 0.03f));
    }
}
