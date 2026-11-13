using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Renders one level of an <see cref="AdventureMap"/>: the ground as one mesh (one draw call, whatever the map size), shaped by the map's
/// relief (ADR-182): every tile is four triangles from its four corners' heights to its centre's (the corners' mean, where
/// <see cref="MapSpace.ToGround"/> puts things), so neighbouring tiles always meet. The mesh is splat-shaded (ADR-183,
/// <see cref="TerrainSplat"/>): the terrains' textures from <c>res://shared/textures/terrain/</c> run into each other where biomes meet;
/// terrains without a texture use their flat <see cref="Palette"/> colour. The tile grid is an overlay of the same shader, shown when
/// <see cref="MapViewContext.ShowGrid"/> is on. Roads and rivers are thin plates lying on each tile's slope.
/// </summary>
public partial class AdventureMapView : Node3D
{
    private const float RoadWidth = 0.45f;
    private const float RiverWidth = 0.8f;
    private const string TextureFolder = "res://shared/textures/terrain/";

    /// <summary>How many tiles one texture repeat covers.</summary>
    private const float TilesPerTextureRepeat = 3f;

    private readonly MeshInstance3D _ground = new() { Name = "Ground" };
    private AdventureMap? _map;
    private int _level;
    private Image _terrainImage = null!;
    private ImageTexture _terrainTexture = null!;
    private ShaderMaterial _material = null!;
    private MultiMesh? _roads;
    private MultiMesh? _rivers;

    public void Bind(MapViewContext context, int level = 0)
    {
        var map = context.State.Map;
        var events = context.Events;
        _map = map;
        _level = level;
        _terrainImage = Image.CreateFromData(map.Width, map.Height, false, Image.Format.R8, TerrainSplat.TerrainMap(map, level));
        _terrainTexture = ImageTexture.CreateFromImage(_terrainImage);
        _material = TerrainSplat.Material(_terrainTexture, map.Width, map.Height, TilesPerTextureRepeat * MapSpace.TileSize);
        AddChild(_ground);
        RebuildGround();
        ShowGrid();
        void ShowGrid() => _material.SetShaderParameter("grid", context.ShowGrid ? 1f : 0f);
        context.ShowGridChanged += ShowGrid;
        TreeExiting += () => context.ShowGridChanged -= ShowGrid;

        _roads = Layer(new StandardMaterial3D { VertexColorUseAsAlbedo = true }, new Vector3(RoadWidth, 0.02f, RoadWidth), "Roads");
        _roads.InstanceCount = map.Width * map.Height;
        _rivers = Layer(new StandardMaterial3D { VertexColorUseAsAlbedo = true }, new Vector3(RiverWidth, 0.015f, RiverWidth), "Rivers");
        _rivers.InstanceCount = map.Width * map.Height;
        RefreshRoads(map.Positions(level));
        RefreshRivers(map.Positions(level));

        // Painting changes texels of the terrain map; only a change of heights needs a new mesh.
        this.SubscribeWhileInTree<TerrainPainted>(events, e =>
        {
            if (e.Positions.Any(p => p.Level == _level))
            {
                _terrainImage.SetData(map.Width, map.Height, false, Image.Format.R8, TerrainSplat.TerrainMap(map, _level));
                _terrainTexture.Update(_terrainImage);
            }
        });
        this.SubscribeWhileInTree<GroundHeightsChanged>(events, e =>
        {
            if (e.Level == _level)
            {
                RebuildGround();
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
    /// triplanar UVs; one texture repeat covers <paramref name="worldUnitsPerRepeat"/>. One terrain alone: the battlefield's ground.
    /// </summary>
    public static StandardMaterial3D TerrainMaterial(TerrainType terrain, float worldUnitsPerRepeat)
    {
        var path = TexturePath(terrain);
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

    /// <summary>Where a terrain's texture is (its own or a borrowed one); the file may be missing.</summary>
    internal static string TexturePath(TerrainType terrain) => $"{TextureFolder}{FileName(TextureOf(terrain))}.png";

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
    internal static Color Tint(TerrainType terrain) => terrain switch
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

    /// <summary>
    /// A vertex per corner, shared by the tiles around it, and one per tile centre; four triangles a tile, clockwise seen from above (Godot's
    /// front faces). Each corner's normal comes from its neighbouring corners, so light runs smoothly over the slopes.
    /// </summary>
    private void RebuildGround()
    {
        var map = _map!;
        var (width, height) = (map.Width, map.Height);
        var cornerCount = (width + 1) * (height + 1);
        var vertices = new Vector3[cornerCount + (width * height)];
        var normals = new Vector3[vertices.Length];
        var indices = new int[width * height * 12];
        int CornerIndex(int cx, int cy) => (cy * (width + 1)) + cx;
        for (var cy = 0; cy <= height; cy++)
        {
            for (var cx = 0; cx <= width; cx++)
            {
                vertices[CornerIndex(cx, cy)] = new Vector3((cx - 0.5f) * MapSpace.TileSize, Base + Corner(cx, cy), (cy - 0.5f) * MapSpace.TileSize);
                normals[CornerIndex(cx, cy)] = CornerNormal(cx, cy);
            }
        }

        var next = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Corners: a top-left, b top-right, c bottom-left, d bottom-right; then the centre.
                var (a, b, c, d) = (CornerIndex(x, y), CornerIndex(x + 1, y), CornerIndex(x, y + 1), CornerIndex(x + 1, y + 1));
                var centre = cornerCount + (y * width) + x;
                vertices[centre] = MapSpace.ToGround(map, new MapPosition(x, y, _level));
                normals[centre] = (normals[a] + normals[b] + normals[c] + normals[d]).Normalized();
                foreach (var index in (ReadOnlySpan<int>)[a, b, centre, b, d, centre, d, c, centre, c, a, centre])
                {
                    indices[next++] = index;
                }
            }
        }

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, _material);
        _ground.Mesh = mesh;
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
