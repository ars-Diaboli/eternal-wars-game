using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Renders one level of an <see cref="AdventureMap"/>: the ground as one mesh per terrain type (one draw call each, whatever the map size),
/// shaped by the map's relief (ADR-182): every tile is four triangles from its four corners' heights to its centre's (the corners' mean, where
/// <see cref="MapSpace.ToGround"/> puts things), so neighbouring tiles always meet. Textured from <c>res://shared/textures/terrain/</c> with
/// world-space triplanar UVs, so tiles of one terrain join seamlessly and slopes are not stretched; terrains without a texture use their flat
/// <see cref="Palette"/> colour. Roads and rivers are thin plates lying on each tile's slope. Placeholder until the splat-shaded terrain exists.
/// </summary>
public partial class AdventureMapView : Node3D
{
    private const float RoadWidth = 0.45f;
    private const float RiverWidth = 0.8f;
    private const string TextureFolder = "res://shared/textures/terrain/";

    /// <summary>How many tiles one texture repeat covers.</summary>
    private const float TilesPerTextureRepeat = 3f;

    /// <summary>The tile grid: faint lines along every tile edge, just above the ground.</summary>
    private static readonly Color GridColour = new(0f, 0f, 0f, 0.35f);

    private readonly Dictionary<TerrainType, (MeshInstance3D Node, Material Material)> _ground = [];
    private readonly MeshInstance3D _grid = new() { Name = "Grid", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    private AdventureMap? _map;
    private int _level;
    private MultiMesh? _roads;
    private MultiMesh? _rivers;

    public void Bind(AdventureMap map, IEventSubscriber events, int level = 0)
    {
        _map = map;
        _level = level;
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var node = new MeshInstance3D { Name = $"Ground_{terrain}" };
            AddChild(node);
            _ground[terrain] = (node, TerrainMaterial(terrain, TilesPerTextureRepeat * MapSpace.TileSize));
        }

        AddChild(_grid);

        _roads = Layer(new StandardMaterial3D { VertexColorUseAsAlbedo = true }, new Vector3(RoadWidth, 0.02f, RoadWidth), "Roads");
        _roads.InstanceCount = map.Width * map.Height;
        _rivers = Layer(new StandardMaterial3D { VertexColorUseAsAlbedo = true }, new Vector3(RiverWidth, 0.015f, RiverWidth), "Rivers");
        _rivers.InstanceCount = map.Width * map.Height;
        RebuildTerrain();
        RefreshRoads(map.Positions(level));
        RefreshRivers(map.Positions(level));

        // A paint stroke can change any terrain into any other; rebuilding the meshes is cheap.
        this.SubscribeWhileInTree<TerrainPainted>(events, e =>
        {
            if (e.Positions.Any(p => p.Level == _level))
            {
                RebuildTerrain();
            }
        });
        this.SubscribeWhileInTree<GroundHeightsChanged>(events, e =>
        {
            if (e.Level == _level)
            {
                RebuildTerrain();
                var tiles = map.Positions(_level).Where(e.Area.Touches).ToList();
                RefreshRoads(tiles);
                RefreshRivers(tiles);
            }
        });
        this.SubscribeWhileInTree<RoadsPainted>(events, e => RefreshRoads(e.Positions));
        this.SubscribeWhileInTree<RiversPainted>(events, e => RefreshRivers(e.Positions));
    }

    /// <summary>
    /// Textured when <c>textures/terrain/&lt;terrain&gt;.png</c> exists, else the flat palette colour. World-space
    /// triplanar UVs; one texture repeat covers <paramref name="worldUnitsPerRepeat"/>. Also the battlefield ground.
    /// </summary>
    public static StandardMaterial3D TerrainMaterial(TerrainType terrain, float worldUnitsPerRepeat)
    {
        var path = $"{TextureFolder}{FileName(TextureOf(terrain))}.png";
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
            Uv1Scale = Vector3.One / worldUnitsPerRepeat,
            Roughness = 0.95f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    /// <summary>Terrains without a texture of their own borrow a related one and are told apart by <see cref="Tint"/>.</summary>
    private static TerrainType TextureOf(TerrainType terrain) => terrain switch
    {
        TerrainType.ShallowWater => TerrainType.Water,
        TerrainType.RockyGround => TerrainType.Rough,
        TerrainType.CursedLands => TerrainType.Wasteland,
        TerrainType.Ice => TerrainType.Snow,
        _ => terrain,
    };

    /// <summary>Colour multiplied into the texture: lava darker and redder, water blue, the rest untouched.</summary>
    private static Color Tint(TerrainType terrain) => terrain switch
    {
        TerrainType.ShallowWater => new Color(0.55f, 0.75f, 0.95f),
        TerrainType.RockyGround => new Color(0.70f, 0.70f, 0.72f),
        TerrainType.CursedLands => new Color(0.60f, 0.50f, 0.65f),
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
        var groups = map.Positions(_level).GroupBy(map.GetTerrain).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var (terrain, (node, material)) in _ground)
        {
            node.Mesh = groups.TryGetValue(terrain, out var tiles) ? GroundMesh(tiles, material) : null;
        }

        _grid.Mesh = GridMesh();
    }

    /// <summary>One line per tile edge, corner to corner (the ground's tile edges are straight), lifted a little so it is not hidden.</summary>
    private ArrayMesh GridMesh()
    {
        var map = _map!;
        var lines = new List<Vector3>(2 * 2 * (map.Width + 1) * (map.Height + 1));
        Vector3 At(int cx, int cy) => new((cx - 0.5f) * MapSpace.TileSize, Base + Corner(cx, cy) + 0.01f, (cy - 0.5f) * MapSpace.TileSize);
        for (var cy = 0; cy <= map.Height; cy++)
        {
            for (var cx = 0; cx <= map.Width; cx++)
            {
                if (cx < map.Width)
                {
                    lines.Add(At(cx, cy));
                    lines.Add(At(cx + 1, cy));
                }

                if (cy < map.Height)
                {
                    lines.Add(At(cx, cy));
                    lines.Add(At(cx, cy + 1));
                }
            }
        }

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = lines.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            AlbedoColor = GridColour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = -1, // before the fog pass, so the fog covers it
        });
        return mesh;
    }

    /// <summary>
    /// Five vertices a tile (the corners and the centre) and four triangles, clockwise seen from above (Godot's front faces). Each vertex's
    /// normal comes from its neighbouring corners, so light runs smoothly over the slopes.
    /// </summary>
    private ArrayMesh GroundMesh(List<MapPosition> tiles, Material material)
    {
        var map = _map!;
        var vertices = new Vector3[tiles.Count * 5];
        var normals = new Vector3[tiles.Count * 5];
        var indices = new int[tiles.Count * 12];
        for (var i = 0; i < tiles.Count; i++)
        {
            var (x, y) = (tiles[i].X, tiles[i].Y);
            var v = i * 5;
            (int Cx, int Cy)[] corners = [(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)];
            for (var c = 0; c < 4; c++)
            {
                var (cx, cy) = corners[c];
                vertices[v + c] = new Vector3((cx - 0.5f) * MapSpace.TileSize, Base + Corner(cx, cy), (cy - 0.5f) * MapSpace.TileSize);
                normals[v + c] = CornerNormal(cx, cy);
            }

            vertices[v + 4] = MapSpace.ToGround(map, tiles[i]);
            normals[v + 4] = (normals[v] + normals[v + 1] + normals[v + 2] + normals[v + 3]).Normalized();

            // Corners: 0 top-left, 1 top-right, 2 bottom-left, 3 bottom-right; 4 the centre.
            int[] triangles = [0, 1, 4, 1, 3, 4, 3, 2, 4, 2, 0, 4];
            for (var t = 0; t < 12; t++)
            {
                indices[(i * 12) + t] = v + triangles[t];
            }
        }

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    /// <summary>A corner's height above the level's base, in world units.</summary>
    private float Corner(int cx, int cy) =>
        _map!.GetCorner(_level, Math.Clamp(cx, 0, _map.Width), Math.Clamp(cy, 0, _map.Height)) * MapSpace.TileSize / ReliefLimits.StepsPerCell;

    private Vector3 CornerNormal(int cx, int cy) =>
        new Vector3(Corner(cx - 1, cy) - Corner(cx + 1, cy), 2 * MapSpace.TileSize, Corner(cx, cy - 1) - Corner(cx, cy + 1)).Normalized();

    /// <summary>A plate lying on the tile's slope, just above the ground at its centre.</summary>
    private Transform3D OnTile(MapPosition position, float lift, bool shown)
    {
        var basis = MapSpace.OnSlope(_map!, position);
        return new Transform3D(basis.Scaled(shown ? Vector3.One : Vector3.Zero), MapSpace.ToGround(_map!, position) + (basis.Y * lift));
    }

    /// <summary>The level's base height in the world.</summary>
    private float Base => MapSpace.ToWorld(new MapPosition(0, 0, _level)).Y;

    // Rivers lie under the roads, wider, so a road over a river reads as a bridge.
    private void RefreshRivers(IEnumerable<MapPosition> positions)
    {
        var map = _map!;
        foreach (var position in positions.Where(p => p.Level == _level))
        {
            var river = map.GetRiver(position);
            var index = (position.Y * map.Width) + position.X;
            _rivers!.SetInstanceTransform(index, OnTile(position, 0.02f, river != RiverType.None));
            _rivers.SetInstanceColor(index, Palette.ForRiver(river));
        }
    }

    private void RefreshRoads(IEnumerable<MapPosition> positions)
    {
        var map = _map!;
        foreach (var position in positions.Where(p => p.Level == _level))
        {
            // Hidden road instances are scaled to nothing; MultiMesh instances cannot be removed individually.
            var road = map.GetRoad(position);
            var index = (position.Y * map.Width) + position.X;
            _roads!.SetInstanceTransform(index, OnTile(position, 0.035f, road != RoadType.None));
            _roads.SetInstanceColor(index, Palette.ForRoad(road));
        }
    }
}
