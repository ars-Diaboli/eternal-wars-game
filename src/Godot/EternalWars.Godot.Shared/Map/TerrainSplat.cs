using EternalWars.Core.Domain.Map;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The splat material of the adventure map's ground (ADR-183, <c>shaders/terrain_splat.gdshader</c>): every terrain's picture is a layer of
/// one texture array, built once and shared by every level and map; which terrain a tile has is read from a map-sized texture
/// (<see cref="TerrainMap"/>), so painting a tile is one texel, not a new mesh.
/// </summary>
internal static class TerrainSplat
{
    private const string ShaderPath = "res://shared/shaders/terrain_splat.gdshader";

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
        var white = Image.CreateEmpty(LayerSize, LayerSize, false, Image.Format.Rgb8);
        white.Fill(Colors.White);
        white.GenerateMipmaps();
        var images = new global::Godot.Collections.Array<Image> { white };
        var layerOfPath = new Dictionary<string, int>(StringComparer.Ordinal);
        var layerOf = new int[MaxTerrains];
        var tintOf = Enumerable.Repeat(Colors.Magenta, MaxTerrains).ToArray();
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var path = AdventureMapView.TexturePath(terrain);
            if (!ResourceLoader.Exists(path))
            {
                tintOf[(int)terrain] = Palette.ForTerrain(terrain);
                continue;
            }

            if (!layerOfPath.TryGetValue(path, out var layer))
            {
                layer = images.Count;
                images.Add(LayerImage(path));
                layerOfPath[path] = layer;
            }

            layerOf[(int)terrain] = layer;
            tintOf[(int)terrain] = AdventureMapView.Tint(terrain);
        }

        var textures = new Texture2DArray();
        var error = textures.CreateFromImages(images);
        if (error != Error.Ok)
        {
            GD.PushError($"The terrain textures could not be put together: {error}");
        }

        return new Layers(textures, layerOf, tintOf);
    }

    private static Image LayerImage(string path)
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

        image.GenerateMipmaps();
        return image;
    }

    private sealed record Layers(Texture2DArray Textures, int[] LayerOf, Color[] TintOf);
}
