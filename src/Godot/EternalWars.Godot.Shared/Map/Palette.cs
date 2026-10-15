using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>Placeholder colours until real terrain materials and hero models exist.</summary>
public static class Palette
{
    private static readonly Color[] PlayerColors =
    [
        new(0.20f, 0.40f, 0.95f),
        new(0.90f, 0.20f, 0.20f),
        new(0.95f, 0.75f, 0.15f),
        new(0.25f, 0.75f, 0.30f),
        new(0.95f, 0.55f, 0.15f),
        new(0.60f, 0.30f, 0.85f),
        new(0.20f, 0.80f, 0.80f),
        new(0.90f, 0.45f, 0.70f),
    ];

    public static Color ForTerrain(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => new Color(0.36f, 0.62f, 0.26f),
        TerrainType.Dirt => new Color(0.50f, 0.38f, 0.22f),
        TerrainType.Sand => new Color(0.86f, 0.78f, 0.50f),
        TerrainType.Snow => new Color(0.93f, 0.95f, 0.98f),
        TerrainType.Swamp => new Color(0.28f, 0.36f, 0.24f),
        TerrainType.Rough => new Color(0.55f, 0.48f, 0.36f),
        TerrainType.Lava => new Color(0.30f, 0.12f, 0.10f),
        TerrainType.Subterranean => new Color(0.35f, 0.30f, 0.28f),
        TerrainType.Water => new Color(0.16f, 0.36f, 0.66f),
        TerrainType.Rock => new Color(0.18f, 0.18f, 0.20f),
        _ => Colors.Magenta,
    };

    public static Color ForPlayer(PlayerId player) => PlayerColors[player.Value % PlayerColors.Length];
}
