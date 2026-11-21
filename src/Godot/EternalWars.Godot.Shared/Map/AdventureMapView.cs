using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Renders one level of an <see cref="AdventureMap"/>: the ground as meshes of <see cref="ChunkTiles"/> × <see cref="ChunkTiles"/> tiles (a
/// change of heights rebuilds only the chunks it reaches; the camera skips those out of view), shaped by the map's relief (ADR-182) as the smooth <see cref="ReliefSurface"/> shows it (ADR-209): every tile is
/// <see cref="Subdivisions"/> × <see cref="Subdivisions"/> squares of four triangles each, on the surface where <see cref="MapSpace.ToGround"/>
/// puts things, so neighbouring tiles always meet. The mesh is splat-shaded (ADR-183,
/// <see cref="TerrainSplat"/>): the terrains' textures from <c>res://shared/textures/terrain/</c> run into each other where biomes meet;
/// terrains without a texture use their flat <see cref="Palette"/> colour. Painting changes only the painted tiles' texels of the terrain map. The tile grid is an overlay of the same shader, shown when
/// <see cref="MapViewContext.ShowGrid"/> is on. Roads and rivers are thin plates lying on each tile's slope. While there is fog (a viewer),
/// the ground goes on <see cref="ApronTiles"/> tiles past the map's edge, its edge tiles' terrain and heights continued, for the fog to
/// swallow along a wandering shoreline (<c>fog_of_war.gdshader</c>): the land never ends in a straight cut.
/// </summary>
public partial class AdventureMapView : Node3D
{
    private const float RoadWidth = 0.45f;
    private const float RiverWidth = 0.8f;
    private const string TextureFolder = "res://shared/textures/terrain/";

    /// <summary>How many tiles one texture repeat covers (32 squares of the mesh; the shader hides the repeat, ADR-212).</summary>
    private const float TilesPerTextureRepeat = 16f;

    /// <summary>How far the ground goes on past the map's edge, in tiles: past the fog's farthest shoreline.</summary>
    public const int ApronTiles = 4;

    /// <summary>The side of a ground chunk, in tiles.</summary>
    public const int ChunkTiles = 32;

    /// <summary>How many squares of the ground mesh a tile's side is split into.</summary>
    public const int Subdivisions = 2;

    private readonly MeshInstance3D _apron = new() { Name = "Apron" };
    private MeshInstance3D[,] _chunks = new MeshInstance3D[0, 0];
    private AdventureMap? _map;
    private int _level;
    private byte[] _terrainBytes = [];
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
        _terrainBytes = TerrainSplat.TerrainMap(map, level);
        _terrainImage = Image.CreateFromData(map.Width, map.Height, false, Image.Format.R8, _terrainBytes);
        _terrainTexture = ImageTexture.CreateFromImage(_terrainImage);
        _material = TerrainSplat.Material(_terrainTexture, map.Width, map.Height, TilesPerTextureRepeat * MapSpace.TileSize);
        _chunks = new MeshInstance3D[(map.Width + ChunkTiles - 1) / ChunkTiles, (map.Height + ChunkTiles - 1) / ChunkTiles];
        for (var cy = 0; cy < _chunks.GetLength(1); cy++)
        {
            for (var cx = 0; cx < _chunks.GetLength(0); cx++)
            {
                AddChild(_chunks[cx, cy] = new MeshInstance3D { Name = $"Ground{cx}_{cy}" });
                RebuildChunk(cx, cy);
            }
        }

        AddChild(_apron);
        RebuildApron();
        void ShowApron() => _apron.Visible = context.Viewer is not null; // no fog (the editor, the whole map in a replay): the map's own edge
        ShowApron();
        context.ViewerChanged += ShowApron;
        TreeExiting += () => context.ViewerChanged -= ShowApron;
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

        // The editor's layers (ADR-262): the ground, the roads and the rivers each hide.
        void ShowLayers()
        {
            foreach (var chunk in _chunks)
            {
                chunk.Visible = context.Shows(MapLayer.Terrain);
            }

            GetNode<Node3D>("Roads").Visible = context.Shows(MapLayer.Roads);
            GetNode<Node3D>("Rivers").Visible = context.Shows(MapLayer.Rivers);
        }

        ShowLayers();
        context.HiddenChanged += ShowLayers;
        TreeExiting += () => context.HiddenChanged -= ShowLayers;

        // Painting changes texels of the terrain map (the painted tiles' bytes; the texture is uploaded whole, Godot has no partial update);
        // only a change of heights needs new meshes.
        this.SubscribeWhileInTree<TerrainPainted>(events, e =>
        {
            var changed = false;
            foreach (var position in e.Positions)
            {
                if (position.Level == _level && map.Contains(position))
                {
                    _terrainBytes[(position.Y * map.Width) + position.X] = (byte)map.GetTerrain(position);
                    changed = true;
                }
            }

            if (changed)
            {
                _terrainImage.SetData(map.Width, map.Height, false, Image.Format.R8, _terrainBytes);
                _terrainTexture.Update(_terrainImage);
            }
        });
        this.SubscribeWhileInTree<GroundHeightsChanged>(events, e =>
        {
            if (e.Level == _level && !e.Area.IsEmpty)
            {
                Reshape(e.Area);
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
    /// The ground under changed corners: the shown surface over a tile reads the corners one past its own (<see cref="ReliefSurface.Reach"/>)
    /// and a vertex's normal the surface just beside it, so the tiles with a corner within one of that reach are rebuilt, chunk by chunk, and
    /// the roads and rivers on tiles touching it; the apron when the reach comes to the edge.
    /// </summary>
    private void Reshape(CornerArea changed)
    {
        var map = _map!;
        changed = ReliefSurface.Reach(changed);
        var reach = changed.Grow(1).Clip(map);

        // The tiles with a corner in the reach: x from Left - 1 to Right, y from Top - 1 to Bottom, within the map.
        var (left, top) = (Math.Max(reach.Left - 1, 0), Math.Max(reach.Top - 1, 0));
        var (right, bottom) = (Math.Min(reach.Right, map.Width - 1), Math.Min(reach.Bottom, map.Height - 1));
        for (var cy = top / ChunkTiles; cy <= bottom / ChunkTiles; cy++)
        {
            for (var cx = left / ChunkTiles; cx <= right / ChunkTiles; cx++)
            {
                RebuildChunk(cx, cy);
            }
        }

        if (reach.Left <= 0 || reach.Top <= 0 || reach.Right >= map.Width || reach.Bottom >= map.Height)
        {
            RebuildApron();
        }

        // Roads and rivers lie on the slope of their tile, which its own corners set.
        var tiles = new List<MapPosition>();
        for (var y = Math.Max(changed.Top - 1, 0); y <= Math.Min(changed.Bottom, map.Height - 1); y++)
        {
            for (var x = Math.Max(changed.Left - 1, 0); x <= Math.Min(changed.Right, map.Width - 1); x++)
            {
                tiles.Add(new MapPosition(x, y, _level));
            }
        }

        RefreshRoads(tiles);
        RefreshRivers(tiles);
    }

    /// <summary>One chunk of the ground (<see cref="Ground"/>).</summary>
    private void RebuildChunk(int chunkX, int chunkY)
    {
        var map = _map!;
        var (x0, y0) = (chunkX * ChunkTiles, chunkY * ChunkTiles);
        _chunks[chunkX, chunkY].Mesh = Ground([(x0, y0, Math.Min(ChunkTiles, map.Width - x0), Math.Min(ChunkTiles, map.Height - y0))]);
    }

    /// <summary>
    /// The ground past the map's edge: <see cref="ApronTiles"/> rings of tiles around it, as four strips (top and bottom the full width, the
    /// sides between them). The surface takes the nearest edge corners' heights there, and the shader the nearest edge tile's terrain.
    /// </summary>
    private void RebuildApron()
    {
        var (width, height) = (_map!.Width, _map.Height);
        _apron.Mesh = Ground([
            (-ApronTiles, -ApronTiles, width + (2 * ApronTiles), ApronTiles), (-ApronTiles, height, width + (2 * ApronTiles), ApronTiles),
            (-ApronTiles, 0, ApronTiles, height), (width, 0, ApronTiles, height)]);
    }

    /// <summary>
    /// Ground over rectangles of tiles (left, top, width, height), on the smooth surface (<see cref="MapSpace.GroundY"/>,
    /// <see cref="ReliefSurface"/>): every tile is <see cref="Subdivisions"/> × <see cref="Subdivisions"/> squares, each four triangles from
    /// its corners to its centre, clockwise seen from above (Godot's front faces). A corner vertex is shared by the squares around it. Normals
    /// come from the surface's heights half a square either way, so light runs smoothly over the slopes and a vertex on a chunk's edge is
    /// worked out the same in both chunks: they meet without a seam.
    /// </summary>
    private ArrayMesh Ground(ReadOnlySpan<(int Left, int Top, int Width, int Height)> rectangles)
    {
        const int steps = Subdivisions * 2; // samples per tile: the squares' corners and centres
        const float step = MapSpace.TileSize / steps;
        var count = 0;
        foreach (var r in rectangles)
        {
            count += r.Width * r.Height;
        }

        var squares = count * Subdivisions * Subdivisions;
        var vertices = new Vector3[squares + (count * (Subdivisions + 1) * (Subdivisions + 1))]; // an overestimate of the shared corners
        var normals = new Vector3[vertices.Length];
        var indices = new int[squares * 12];
        var (used, next) = (0, 0);
        var floor = MapSpace.ToWorld(new MapPosition(0, 0, _level)).Y;
        foreach (var (left, top, width, height) in rectangles)
        {
            // The surface on a grid of samples, one more on every side for the normals.
            var (columns, rows) = ((width * steps) + 3, (height * steps) + 3);
            var (firstX, firstZ) = (((left - 0.5f) * MapSpace.TileSize) - step, ((top - 0.5f) * MapSpace.TileSize) - step);
            var heights = ReliefSurface.Grid(_map!, _level, firstX / MapSpace.TileSize, firstZ / MapSpace.TileSize, step / MapSpace.TileSize, columns, rows);
            float Y(int i, int j) => floor + (heights[i, j] * MapSpace.TileSize);

            int Add(int i, int j)
            {
                vertices[used] = new Vector3(firstX + (i * step), Y(i, j), firstZ + (j * step));
                normals[used] = new Vector3(Y(i - 1, j) - Y(i + 1, j), 2 * step, Y(i, j - 1) - Y(i, j + 1)).Normalized();
                return used++;
            }

            // The squares' corners are the even samples, their centres the odd ones.
            var (across, down) = (width * Subdivisions, height * Subdivisions);
            var first = used;
            for (var y = 0; y <= down; y++)
            {
                for (var x = 0; x <= across; x++)
                {
                    Add((2 * x) + 1, (2 * y) + 1);
                }
            }

            int Corner(int x, int y) => first + (y * (across + 1)) + x;
            for (var y = 0; y < down; y++)
            {
                for (var x = 0; x < across; x++)
                {
                    // a top-left, b top-right, c bottom-left, d bottom-right; then the centre.
                    var (a, b, c, d) = (Corner(x, y), Corner(x + 1, y), Corner(x, y + 1), Corner(x + 1, y + 1));
                    var centre = Add((2 * x) + 2, (2 * y) + 2);
                    foreach (var index in (ReadOnlySpan<int>)[a, b, centre, b, d, centre, d, c, centre, c, a, centre])
                    {
                        indices[next++] = index;
                    }
                }
            }
        }

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices[..used];
        arrays[(int)Mesh.ArrayType.Normal] = normals[..used];
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, _material);
        return mesh;
    }

    /// <summary>A plate lying on the tile's slope, just above the ground at its centre.</summary>
    private Transform3D OnTile(MapPosition position, float lift, bool shown)
    {
        var basis = MapSpace.OnSlope(_map!, position);
        return new Transform3D(basis.Scaled(shown ? Vector3.One : Vector3.Zero), MapSpace.ToGround(_map!, position) + (basis.Y * lift));
    }

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
