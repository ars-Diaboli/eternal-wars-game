using EternalWars.Core.Domain.Map;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The splat material of the adventure map's ground (ADR-183, <c>shaders/terrain_splat.gdshader</c>): every terrain's picture is a layer of
/// one texture array, built once and shared by every level and map; which terrain a tile has is read from a map-sized texture
/// (<see cref="TerrainMap"/>), so painting a tile is one texel, not a new mesh. A layer's alpha is its ground's height (bright = raised),
/// which the shader's height blend reads: from <c>&lt;terrain&gt;_height.png</c> when there is one, else from the picture's brightness.
/// A picture in <c>textures/terrain/adventure/</c> is used here instead of the shared one (the battlefield keeps that), and a picture with
/// <c>_normal</c>, <c>_roughness</c> or <c>_ao</c> maps beside it gets a layer of a second array: normal XY, roughness, ambient occlusion.
/// </summary>
internal static class TerrainSplat
{
    private const string ShaderPath = "res://shared/shaders/terrain_splat.gdshader";

    /// <summary>Pictures only the adventure map uses, in place of the shared ones of the same name.</summary>
    private const string AdventureFolder = "res://shared/textures/terrain/adventure/";

    /// <summary>A flat normal, the ground's usual roughness (0.95) and no occlusion: the detail of a picture without maps.</summary>
    private static readonly Color FlatDetail = Color.Color8(128, 128, 242, 255);

    /// <summary>Every layer has this size (a texture array needs one size); other pictures are resized.</summary>
    private const int LayerSize = 1024;

    /// <summary>The shader's table size (<c>MAX_TERRAINS</c>).</summary>
    private const int MaxTerrains = 32;

    private static Layers? _layers;

    /// <summary>The ground's material for one level: <paramref name="terrainMap"/> is <see cref="TerrainMap"/>'s texture.</summary>
    public static ShaderMaterial Material(Texture2D terrainMap, int width, int height, float worldUnitsPerRepeat)
    {
        var layers = _layers ??= BuildLayers();
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        material.SetShaderParameter("terrain_map", terrainMap);
        material.SetShaderParameter("map_size", new Vector2(width, height));
        material.SetShaderParameter("tile_size", MapSpace.TileSize);
        material.SetShaderParameter("albedo_layers", layers.Textures);
        material.SetShaderParameter("layer_of", layers.LayerOf);
        material.SetShaderParameter("tint_of", layers.TintOf);
        material.SetShaderParameter("detail_layers", layers.Details);
        material.SetShaderParameter("detail_of", layers.DetailOf);
        material.SetShaderParameter("uv_scale", 1f / worldUnitsPerRepeat);
        return material;
    }

    /// <summary>One byte a tile, row by row: the terrain's number, as the shader reads it.</summary>
    public static byte[] TerrainMap(AdventureMap map, int level)
    {
        var bytes = new byte[map.Width * map.Height];
        foreach (var position in map.Positions(level))
        {
            bytes[(position.Y * map.Width) + position.X] = (byte)map.GetTerrain(position);
        }

        return bytes;
    }

    /// <summary>
    /// Layer 0 is plain white: a terrain without a picture is its palette colour on it. A picture shared by several terrains (shallow water
    /// borrows water's) is one layer; the terrains differ by their tint.
    /// </summary>
    private static Layers BuildLayers()
    {
        var white = Image.CreateEmpty(LayerSize, LayerSize, false, Image.Format.Rgba8);
        white.Fill(new Color(1f, 1f, 1f, 0.5f));
        white.GenerateMipmaps();
        var images = new global::Godot.Collections.Array<Image> { white };
        var flat = Image.CreateEmpty(LayerSize, LayerSize, false, Image.Format.Rgba8);
        flat.Fill(FlatDetail);
        flat.GenerateMipmaps();
        var details = new global::Godot.Collections.Array<Image> { flat };
        var layerOfPath = new Dictionary<string, (int Layer, int Detail)>(StringComparer.Ordinal);
        var layerOf = new int[MaxTerrains];
        var detailOf = new int[MaxTerrains];
        var tintOf = Enumerable.Repeat(Colors.Magenta, MaxTerrains).ToArray();
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var path = AdventurePath(AdventureMapView.TexturePath(terrain));
            if (!ResourceLoader.Exists(path))
            {
                tintOf[(int)terrain] = Palette.ForTerrain(terrain);
                continue;
            }

            if (!layerOfPath.TryGetValue(path, out var layer))
            {
                layer = (images.Count, 0);
                images.Add(LayerImage(path));
                if (DetailImage(path) is { } detail)
                {
                    layer.Detail = details.Count;
                    details.Add(detail);
                }

                layerOfPath[path] = layer;
            }

            layerOf[(int)terrain] = layer.Layer;
            detailOf[(int)terrain] = layer.Detail;
            tintOf[(int)terrain] = AdventureMapView.Tint(terrain);
        }

        var textures = new Texture2DArray();
        var error = textures.CreateFromImages(images);
        var detailTextures = new Texture2DArray();
        if (detailTextures.CreateFromImages(details) is var detailError && detailError != Error.Ok)
        {
            error = detailError;
        }

        if (error != Error.Ok)
        {
            GD.PushError($"The terrain textures could not be put together: {error}");
        }

        return new Layers(textures, layerOf, tintOf, detailTextures, detailOf);
    }

    /// <summary>The picture in RGB and its height in A.</summary>
    private static Image LayerImage(string path)
    {
        var albedo = LoadSquare(path).GetData();
        var heightPath = HeightPath(path);
        var height = ResourceLoader.Exists(heightPath) ? Grey(LoadSquare(heightPath).GetData()) : Grey(albedo);
        Stretch(height);

        var rgba = new byte[LayerSize * LayerSize * 4];
        for (var i = 0; i < height.Length; i++)
        {
            rgba[i * 4] = albedo[i * 3];
            rgba[(i * 4) + 1] = albedo[(i * 3) + 1];
            rgba[(i * 4) + 2] = albedo[(i * 3) + 2];
            rgba[(i * 4) + 3] = height[i];
        }

        var image = Image.CreateFromData(LayerSize, LayerSize, false, Image.Format.Rgba8, rgba);
        image.GenerateMipmaps();
        return image;
    }

    /// <summary>
    /// Normal XY in RG, roughness in B, ambient occlusion in A, from <c>&lt;picture&gt;_normal</c>, <c>_roughness</c> and <c>_ao</c>; a missing
    /// one is flat, 0.95 and none. Null when the picture has none of them.
    /// </summary>
    private static Image? DetailImage(string path)
    {
        var normal = Load(Suffixed(path, "normal"));
        var roughness = Load(Suffixed(path, "roughness"));
        var occlusion = Load(Suffixed(path, "ao"));
        if (normal is null && roughness is null && occlusion is null)
        {
            return null;
        }

        var rgba = new byte[LayerSize * LayerSize * 4];
        for (var i = 0; i < LayerSize * LayerSize; i++)
        {
            rgba[i * 4] = normal?[i * 3] ?? (byte)FlatDetail.R8;
            rgba[(i * 4) + 1] = normal?[(i * 3) + 1] ?? (byte)FlatDetail.G8;
            rgba[(i * 4) + 2] = roughness?[i * 3] ?? (byte)FlatDetail.B8;
            rgba[(i * 4) + 3] = occlusion?[i * 3] ?? (byte)FlatDetail.A8;
        }

        var image = Image.CreateFromData(LayerSize, LayerSize, false, Image.Format.Rgba8, rgba);
        image.GenerateMipmaps();
        return image;

        static byte[]? Load(string path) => ResourceLoader.Exists(path) ? LoadSquare(path).GetData() : null;
    }

    /// <summary><c>textures/terrain/grass.png</c> → <c>textures/terrain/adventure/grass.png</c> when that exists.</summary>
    private static string AdventurePath(string path)
    {
        var adventure = AdventureFolder + path[(path.LastIndexOf('/') + 1)..];
        return ResourceLoader.Exists(adventure) ? adventure : path;
    }

    /// <summary><c>textures/terrain/sand.png</c> → <c>textures/terrain/sand_height.png</c>.</summary>
    private static string HeightPath(string albedoPath) => Suffixed(albedoPath, "height");

    private static string Suffixed(string albedoPath, string map) => $"{albedoPath[..^".png".Length]}_{map}.png";

    private static Image LoadSquare(string path)
    {
        var image = GD.Load<Texture2D>(path).GetImage();
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        image.Convert(Image.Format.Rgb8);
        if (image.GetWidth() != LayerSize || image.GetHeight() != LayerSize)
        {
            image.Resize(LayerSize, LayerSize, Image.Interpolation.Lanczos);
        }

        return image;
    }

    /// <summary>The brightness of each RGB8 pixel.</summary>
    private static byte[] Grey(byte[] rgb)
    {
        var grey = new byte[rgb.Length / 3];
        for (var i = 0; i < grey.Length; i++)
        {
            grey[i] = (byte)(((rgb[i * 3] * 77) + (rgb[(i * 3) + 1] * 150) + (rgb[(i * 3) + 2] * 29)) >> 8);
        }

        return grey;
    }

    /// <summary>
    /// Spreads the heights over the whole 0–255 range (the darkest and brightest 2 % clipped), so every ground's relief counts alike in the
    /// blend whether its picture is dull or contrasty.
    /// </summary>
    private static void Stretch(byte[] height)
    {
        var histogram = new int[256];
        foreach (var value in height)
        {
            histogram[value]++;
        }

        var clip = height.Length / 50;
        int low = 0, high = 255;
        for (int seen = 0; low < 255 && (seen += histogram[low]) < clip; low++)
        {
        }

        for (int seen = 0; high > 0 && (seen += histogram[high]) < clip; high--)
        {
        }

        if (high <= low)
        {
            return;
        }

        for (var i = 0; i < height.Length; i++)
        {
            height[i] = (byte)Math.Clamp((height[i] - low) * 255 / (high - low), 0, 255);
        }
    }

    private sealed record Layers(Texture2DArray Textures, int[] LayerOf, Color[] TintOf, Texture2DArray Details, int[] DetailOf);
}
