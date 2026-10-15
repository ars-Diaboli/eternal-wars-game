using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Renders one level of an <see cref="AdventureMap"/>: one MultiMesh per terrain type (one draw call each,
/// whatever the map size) textured from <c>res://shared/textures/terrain/</c> with world-space triplanar UVs,
/// so neighbouring tiles of one terrain join seamlessly; plus a road layer. Terrains without a texture use their
/// flat <see cref="Palette"/> colour. Placeholder until the heightmapped terrain (ADR-011) exists.
/// </summary>
public partial class AdventureMapView : Node3D
{
    private const float TileHeight = 0.1f;
    private const float TileGap = 0.02f;
    private const float RoadWidth = 0.45f;
    private const string TextureFolder = "res://shared/textures/terrain/";

    /// <summary>How many tiles one texture repeat covers.</summary>
    private const float TilesPerTextureRepeat = 3f;

    private readonly Dictionary<TerrainType, MultiMesh> _tiles = [];
    private AdventureMap? _map;
    private int _level;
    private MultiMesh? _roads;

    public void Bind(AdventureMap map, IEventSubscriber events, int level = 0)
    {
        _map = map;
        _level = level;
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            _tiles[terrain] = Layer(TileMaterial(terrain), new Vector3(MapSpace.TileSize - TileGap, TileHeight, MapSpace.TileSize - TileGap), $"Tiles_{terrain}");
        }

        _roads = Layer(new StandardMaterial3D { VertexColorUseAsAlbedo = true }, new Vector3(RoadWidth, 0.02f, RoadWidth), "Roads");
        _roads.InstanceCount = map.Width * map.Height;
        RebuildTerrain();
        RefreshRoads(map.Positions(level));

        // A paint stroke can change any terrain into any other; regrouping the instances is cheap.
        this.SubscribeWhileInTree<TerrainPainted>(events, e =>
        {
            if (e.Positions.Any(p => p.Level == _level))
            {
                RebuildTerrain();
            }
        });
        this.SubscribeWhileInTree<RoadsPainted>(events, e => RefreshRoads(e.Positions));
    }

    /// <summary>Textured when <c>textures/terrain/&lt;terrain&gt;.png</c> exists, else the flat palette colour.</summary>
    private static StandardMaterial3D TileMaterial(TerrainType terrain)
    {
        var path = $"{TextureFolder}{FileName(terrain)}.png";
        if (!ResourceLoader.Exists(path))
        {
            return new StandardMaterial3D { AlbedoColor = Palette.ForTerrain(terrain) };
        }

        return new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>(path),
            AlbedoColor = Tint(terrain),
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = Vector3.One / TilesPerTextureRepeat,
            Roughness = 0.95f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    /// <summary>Colour multiplied into the texture: lava darker and redder, water blue, the rest untouched.</summary>
    private static Color Tint(TerrainType terrain) => terrain switch
    {
        TerrainType.Lava => new Color(0.75f, 0.35f, 0.25f),
        TerrainType.Water => new Color(0.35f, 0.55f, 0.95f),
        TerrainType.Rock => new Color(0.55f, 0.55f, 0.58f),
        _ => Colors.White,
    };

    /// <summary>File name of a terrain's texture: snake_case of the enum name (ForestFloor → forest_floor).</summary>
    public static string FileName(TerrainType terrain) =>
        string.Concat(terrain.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : $"{char.ToLowerInvariant(c)}"));

    private MultiMesh Layer(Material material, Vector3 size, string name)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new BoxMesh { Size = size, Material = material },
        };
        AddChild(new MultiMeshInstance3D { Multimesh = multiMesh, Name = name });
        return multiMesh;
    }

    private void RebuildTerrain()
    {
        var map = _map!;
        foreach (var group in map.Positions(_level).GroupBy(map.GetTerrain))
        {
            var tiles = _tiles[group.Key];
            var positions = group.ToList();
            tiles.InstanceCount = positions.Count;
            for (var i = 0; i < positions.Count; i++)
            {
                tiles.SetInstanceTransform(i, new Transform3D(Basis.Identity, MapSpace.ToWorld(positions[i]) + (Vector3.Down * TileHeight / 2)));
                tiles.SetInstanceColor(i, Colors.White);
            }
        }

        var present = map.Positions(_level).Select(map.GetTerrain).ToHashSet();
        foreach (var (terrain, tiles) in _tiles.Where(t => !present.Contains(t.Key)))
        {
            tiles.InstanceCount = 0;
        }
    }

    private void RefreshRoads(IEnumerable<MapPosition> positions)
    {
        var map = _map!;
        foreach (var position in positions.Where(p => p.Level == _level))
        {
            // Hidden road instances are scaled to nothing; MultiMesh instances cannot be removed individually.
            var road = map.GetRoad(position);
            var scale = road == RoadType.None ? Vector3.Zero : Vector3.One;
            var index = (position.Y * map.Width) + position.X;
            _roads!.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(scale), MapSpace.ToWorld(position) + (Vector3.Up * 0.01f)));
            _roads.SetInstanceColor(index, Palette.ForRoad(road));
        }
    }
}
