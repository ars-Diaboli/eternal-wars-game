using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Renders one level of an <see cref="AdventureMap"/> as coloured tiles using a single MultiMesh
/// (one draw call regardless of map size). Placeholder until terrain meshes/shaders are designed.
/// </summary>
public partial class AdventureMapView : Node3D
{
    private const float TileHeight = 0.1f;
    private const float TileGap = 0.04f;

    private AdventureMap? _map;
    private int _level;
    private MultiMesh? _multiMesh;

    public void Bind(AdventureMap map, IEventSubscriber events, int level = 0)
    {
        _map = map;
        _level = level;
        Build();
        this.SubscribeWhileInTree<TerrainPainted>(events, e => Refresh(e.Positions));
    }

    private void Build()
    {
        var map = _map!;
        var mesh = new BoxMesh
        {
            Size = new Vector3(MapSpace.TileSize - TileGap, TileHeight, MapSpace.TileSize - TileGap),
            Material = new StandardMaterial3D { VertexColorUseAsAlbedo = true },
        };

        _multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = map.Width * map.Height,
        };

        foreach (var position in map.Positions(_level))
        {
            var index = IndexOf(position);
            var origin = MapSpace.ToWorld(position) + (Vector3.Down * TileHeight / 2);
            _multiMesh.SetInstanceTransform(index, new Transform3D(Basis.Identity, origin));
            _multiMesh.SetInstanceColor(index, Palette.ForTerrain(map.GetTerrain(position)));
        }

        AddChild(new MultiMeshInstance3D { Multimesh = _multiMesh, Name = "Tiles" });
    }

    private void Refresh(IEnumerable<MapPosition> positions)
    {
        foreach (var position in positions.Where(p => p.Level == _level))
        {
            _multiMesh!.SetInstanceColor(IndexOf(position), Palette.ForTerrain(_map!.GetTerrain(position)));
        }
    }

    private int IndexOf(MapPosition position) => (position.Y * _map!.Width) + position.X;
}
