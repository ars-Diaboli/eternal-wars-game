using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Renders one level of an <see cref="AdventureMap"/> as coloured tiles plus a road layer, each a single
/// MultiMesh (one draw call regardless of map size). Placeholder until terrain meshes/shaders are designed.
/// </summary>
public partial class AdventureMapView : Node3D
{
    private const float TileHeight = 0.1f;
    private const float TileGap = 0.04f;
    private const float RoadWidth = 0.45f;

    private AdventureMap? _map;
    private int _level;
    private MultiMesh? _tiles;
    private MultiMesh? _roads;

    public void Bind(AdventureMap map, IEventSubscriber events, int level = 0)
    {
        _map = map;
        _level = level;
        Build();
        this.SubscribeWhileInTree<TerrainPainted>(events, e => Refresh(e.Positions));
        this.SubscribeWhileInTree<RoadsPainted>(events, e => Refresh(e.Positions));
    }

    private void Build()
    {
        var map = _map!;
        _tiles = Layer(new Vector3(MapSpace.TileSize - TileGap, TileHeight, MapSpace.TileSize - TileGap), "Tiles");
        _roads = Layer(new Vector3(RoadWidth, 0.02f, RoadWidth), "Roads");
        Refresh(map.Positions(_level));
    }

    private MultiMesh Layer(Vector3 size, string name)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new BoxMesh { Size = size, Material = new StandardMaterial3D { VertexColorUseAsAlbedo = true } },
            InstanceCount = _map!.Width * _map.Height,
        };
        AddChild(new MultiMeshInstance3D { Multimesh = multiMesh, Name = name });
        return multiMesh;
    }

    private void Refresh(IEnumerable<MapPosition> positions)
    {
        var map = _map!;
        foreach (var position in positions.Where(p => p.Level == _level))
        {
            var index = IndexOf(position);
            var ground = MapSpace.ToWorld(position);
            _tiles!.SetInstanceTransform(index, new Transform3D(Basis.Identity, ground + (Vector3.Down * TileHeight / 2)));
            _tiles.SetInstanceColor(index, Palette.ForTerrain(map.GetTerrain(position)));

            // Hidden road instances are scaled to nothing; MultiMesh instances cannot be removed individually.
            var road = map.GetRoad(position);
            var scale = road == RoadType.None ? Vector3.Zero : Vector3.One;
            _roads!.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(scale), ground + (Vector3.Up * 0.01f)));
            _roads.SetInstanceColor(index, Palette.ForRoad(road));
        }
    }

    private int IndexOf(MapPosition position) => (position.Y * _map!.Width) + position.X;
}
