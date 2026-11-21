using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
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
        TerrainType.Highlands => new Color(0.47f, 0.52f, 0.38f),
        TerrainType.ForestFloor => new Color(0.20f, 0.34f, 0.18f),
        TerrainType.Wasteland => new Color(0.42f, 0.40f, 0.36f),
        TerrainType.Lava => new Color(0.30f, 0.12f, 0.10f),
        TerrainType.Subterranean => new Color(0.35f, 0.30f, 0.28f),
        TerrainType.Water => new Color(0.16f, 0.36f, 0.66f),
        TerrainType.Rock => new Color(0.18f, 0.18f, 0.20f),
        TerrainType.Beach => new Color(0.80f, 0.74f, 0.58f),
        TerrainType.Ice => new Color(0.72f, 0.85f, 0.95f),
        TerrainType.ShallowWater => new Color(0.30f, 0.55f, 0.72f),
        TerrainType.CursedLands => new Color(0.30f, 0.26f, 0.32f),
        TerrainType.RockyGround => new Color(0.45f, 0.43f, 0.42f),
        _ => Colors.Magenta,
    };

    /// <summary>One of the eight player colours (<see cref="Core.Domain.Players.Player.Colour"/>).</summary>
    public static Color ForColour(int colour) => PlayerColors[((colour % PlayerColors.Length) + PlayerColors.Length) % PlayerColors.Length];

    /// <summary>The colour <paramref name="player"/> plays in (its <see cref="Core.Domain.Players.Player.Colour"/>, ADR-225).</summary>
    public static Color ForPlayer(GameState state, PlayerId player) => ForColour(state.HasPlayer(player) ? state.GetPlayer(player).Colour : player.Value);

    /// <summary>A player slot's own colour, where no game has set one yet (the editor, a lobby's or a save's seats).</summary>
    public static Color ForSlot(PlayerId player) => ForColour(player.Value);

    public static Color Neutral { get; } = new(0.6f, 0.6f, 0.6f);

    public static Color ForRoad(RoadType road) => road switch
    {
        RoadType.Dirt => new Color(0.42f, 0.30f, 0.18f),
        RoadType.Gravel => new Color(0.55f, 0.53f, 0.50f),
        RoadType.Cobblestone => new Color(0.35f, 0.35f, 0.38f),
        _ => Colors.Transparent,
    };

    public static Color ForRiver(RiverType river) => river switch
    {
        RiverType.Clear => new Color(0.30f, 0.60f, 0.95f),
        RiverType.Icy => new Color(0.75f, 0.90f, 1.00f),
        RiverType.Muddy => new Color(0.40f, 0.35f, 0.20f),
        RiverType.Lava => new Color(0.95f, 0.40f, 0.10f),
        _ => Colors.Transparent,
    };

    public static Color ForObject(MapObjectKind kind) => kind switch
    {
        MapObjectKind.ResourcePile => new Color(0.95f, 0.80f, 0.25f),
        MapObjectKind.Mine => new Color(0.40f, 0.36f, 0.32f),
        MapObjectKind.TreasureChest => new Color(0.55f, 0.32f, 0.12f),
        MapObjectKind.TwoWayGate => new Color(0.50f, 0.30f, 0.75f),
        MapObjectKind.OneWayGateEntrance => new Color(0.65f, 0.25f, 0.80f),
        MapObjectKind.OneWayGateExit => new Color(0.80f, 0.60f, 0.90f),
        MapObjectKind.Monster => new Color(0.80f, 0.12f, 0.10f),
        MapObjectKind.Town => new Color(0.62f, 0.58f, 0.50f),
        MapObjectKind.Dwelling => new Color(0.30f, 0.62f, 0.40f),
        MapObjectKind.Artifact => new Color(0.35f, 0.75f, 0.95f),
        MapObjectKind.Marketplace => new Color(0.85f, 0.65f, 0.20f),
        MapObjectKind.ResourceProvider => new Color(0.55f, 0.70f, 0.30f),
        MapObjectKind.Loot => new Color(0.70f, 0.55f, 0.35f),
        MapObjectKind.Buff => new Color(0.45f, 0.75f, 0.85f),
        MapObjectKind.Training => new Color(0.80f, 0.45f, 0.70f),
        MapObjectKind.Sign => new Color(0.55f, 0.45f, 0.30f),
        MapObjectKind.Observatory => new Color(0.75f, 0.75f, 0.85f),
        MapObjectKind.HillFort => new Color(0.50f, 0.45f, 0.40f),
        MapObjectKind.TownGate => new Color(0.30f, 0.55f, 0.90f),
        MapObjectKind.WarMachineFactory => new Color(0.45f, 0.40f, 0.35f),
        MapObjectKind.Prison => new Color(0.30f, 0.30f, 0.35f),
        MapObjectKind.CreatureBank => new Color(0.60f, 0.25f, 0.30f),
        MapObjectKind.DenOfThieves => new Color(0.35f, 0.30f, 0.40f),
        MapObjectKind.AstrologersTower => new Color(0.25f, 0.30f, 0.55f),
        MapObjectKind.Sanctuary => new Color(0.90f, 0.90f, 0.75f),
        MapObjectKind.Scenery => new Color(0.45f, 0.45f, 0.42f),
        MapObjectKind.Cartographer => new Color(0.85f, 0.80f, 0.60f),
        MapObjectKind.ArtifactMerchant => new Color(0.70f, 0.50f, 0.85f),
        MapObjectKind.RefugeeCamp => new Color(0.60f, 0.55f, 0.40f),
        MapObjectKind.SacrificialAltar => new Color(0.55f, 0.15f, 0.15f),
        MapObjectKind.Sphinx => new Color(0.85f, 0.70f, 0.35f),
        MapObjectKind.WitchHut => new Color(0.35f, 0.50f, 0.30f),
        MapObjectKind.Shrine => new Color(0.45f, 0.55f, 0.85f),
        MapObjectKind.SkillSchool => new Color(0.30f, 0.45f, 0.70f),
        MapObjectKind.Obelisk => new Color(0.70f, 0.62f, 0.45f),
        MapObjectKind.CombatPlace => new Color(0.80f, 0.25f, 0.20f),
        MapObjectKind.Deposit => new Color(0.55f, 0.50f, 0.45f),
        MapObjectKind.MagiWindow or MapObjectKind.MagiEye => new Color(0.40f, 0.60f, 0.95f),
        MapObjectKind.KeymasterTent or MapObjectKind.BorderPost => new Color(0.90f, 0.85f, 0.30f),
        MapObjectKind.MapTavern => new Color(0.65f, 0.40f, 0.20f),
        MapObjectKind.Garrison => new Color(0.50f, 0.50f, 0.55f),
        MapObjectKind.PandorasBox => new Color(0.75f, 0.30f, 0.65f),
        MapObjectKind.SeerHut => new Color(0.55f, 0.40f, 0.70f),
        MapObjectKind.QuestGuard or MapObjectKind.QuestGate => new Color(0.95f, 0.55f, 0.20f),
        _ => Colors.Magenta,
    };

    /// <summary>The four object colours (keymasters, border guards and gates, Eyes and Windows of the Magi), so a tent and its posts match.</summary>
    public static Color ForColour(ObjectColour colour) => colour switch
    {
        ObjectColour.Red => new Color(0.85f, 0.18f, 0.15f),
        ObjectColour.Green => new Color(0.20f, 0.70f, 0.25f),
        ObjectColour.Blue => new Color(0.20f, 0.40f, 0.95f),
        ObjectColour.Black => new Color(0.12f, 0.12f, 0.14f),
        _ => Colors.Magenta,
    };

    /// <summary>
    /// An overlay band's colour (ADR-264): passability in red, white and orange; players in their colours; heat and days from green over
    /// yellow to red (a day past the overlay's last in dark red); zones in hues spread around the wheel; contours in white, at whole cells in gold.
    /// </summary>
    public static Color ForTint(OverlayTint tint) => tint.Kind switch
    {
        TintKind.Blocked => new Color(0.85f, 0.15f, 0.15f),
        TintKind.Entrance => new Color(0.95f, 0.95f, 0.95f),
        TintKind.Guarded => new Color(1.00f, 0.55f, 0.10f),
        TintKind.Player => ForColour(tint.Value),
        TintKind.Neutral => Neutral,
        TintKind.Heat => Ramp(tint.Value / 100f),
        TintKind.Day => tint.Value > MapOverlays.ReachDays ? new Color(0.45f, 0.08f, 0.08f) : Ramp((tint.Value - 1) / (float)Math.Max(1, MapOverlays.ReachDays - 1)),
        TintKind.Zone => Color.FromHsv(tint.Value * 0.618034f % 1f, 0.65f, 0.95f), // golden-ratio hues: neighbours in the list differ
        TintKind.Contour => tint.Value == 0 ? new Color(1, 1, 1, 0.6f) : new Color(1.00f, 0.85f, 0.35f, 0.95f),
        _ => Colors.Magenta,
    };

    /// <summary>Green (0) over yellow to red (1).</summary>
    private static Color Ramp(float t) =>
        t < 0.5f ? new Color(0.25f, 0.85f, 0.30f).Lerp(new Color(0.95f, 0.85f, 0.20f), t * 2) : new Color(0.95f, 0.85f, 0.20f).Lerp(new Color(0.90f, 0.20f, 0.20f), (t - 0.5f) * 2);

    /// <summary>Path markers: today green, tomorrow yellow, the day after orange, later red (HoMM-style).</summary>
    public static Color ForPathDay(int day) => day switch
    {
        0 => new Color(0.30f, 0.90f, 0.30f),
        1 => new Color(0.95f, 0.85f, 0.20f),
        2 => new Color(0.95f, 0.55f, 0.15f),
        _ => new Color(0.90f, 0.20f, 0.20f),
    };
}
