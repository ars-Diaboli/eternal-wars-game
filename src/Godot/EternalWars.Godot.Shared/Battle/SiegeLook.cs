using EternalWars.Core.Domain.Towns;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>The shape on top of a siege's towers, keep and the town's houses behind the walls (ADR-144).</summary>
public enum RoofShape
{
    /// <summary>A pointed round roof.</summary>
    Cone,

    /// <summary>A four-sided pointed roof.</summary>
    Pyramid,

    /// <summary>A half sphere.</summary>
    Dome,

    /// <summary>A crown of thin spikes.</summary>
    Spikes,

    /// <summary>A flat top with battlements.</summary>
    Battlements,
}

/// <summary>
/// How a town's fortifications look on the siege field (ADR-144): stone of the walls, keep and towers, the gate, the roofs and their shape, the
/// moat (water, swamp, lava; <see cref="MoatGlows"/> for lava), and the ground behind the walls. Placeholder shapes and colours.
/// </summary>
public sealed record SiegeLook(Color Stone, Color Gate, Color Roof, RoofShape Roofs, Color Moat, bool MoatGlows, Color Yard)
{
    /// <summary>Grey stone, a wooden gate, flat battlements, plain water: a town without a look of its own.</summary>
    public static SiegeLook Default { get; } = new(
        new Color(0.62f, 0.60f, 0.56f), new Color(0.45f, 0.30f, 0.16f), new Color(0.50f, 0.48f, 0.45f), RoofShape.Battlements,
        new Color(0.18f, 0.32f, 0.55f, 0.8f), false, new Color(0.40f, 0.38f, 0.34f));

    private static readonly Dictionary<string, SiegeLook> Looks = new(StringComparer.Ordinal)
    {
        // Pale limestone, oak, blue slate cones.
        [TownIds.Haven] = new(
            new Color(0.80f, 0.77f, 0.70f), new Color(0.48f, 0.32f, 0.17f), new Color(0.22f, 0.32f, 0.62f), RoofShape.Cone,
            new Color(0.20f, 0.38f, 0.62f, 0.8f), false, new Color(0.52f, 0.50f, 0.44f)),

        // Mossy stone, living wood, leaf-green cones, clear green water.
        [TownIds.Sylvan] = new(
            new Color(0.50f, 0.56f, 0.44f), new Color(0.36f, 0.26f, 0.14f), new Color(0.24f, 0.50f, 0.22f), RoofShape.Cone,
            new Color(0.18f, 0.45f, 0.42f, 0.8f), false, new Color(0.30f, 0.42f, 0.24f)),

        // White marble, bronze, golden domes, turquoise water.
        [TownIds.Academy] = new(
            new Color(0.88f, 0.86f, 0.80f), new Color(0.62f, 0.45f, 0.22f), new Color(0.85f, 0.68f, 0.25f), RoofShape.Dome,
            new Color(0.16f, 0.52f, 0.58f, 0.8f), false, new Color(0.66f, 0.62f, 0.52f)),

        // Dark basalt, bone gate, purple spikes, murky green water.
        [TownIds.Necropolis] = new(
            new Color(0.28f, 0.27f, 0.30f), new Color(0.78f, 0.74f, 0.62f), new Color(0.32f, 0.18f, 0.38f), RoofShape.Spikes,
            new Color(0.12f, 0.16f, 0.10f, 0.92f), false, new Color(0.22f, 0.22f, 0.20f)),

        // Black and red rock, iron, red spikes, a moat of lava.
        [TownIds.Inferno] = new(
            new Color(0.30f, 0.16f, 0.14f), new Color(0.22f, 0.22f, 0.24f), new Color(0.70f, 0.18f, 0.10f), RoofShape.Spikes,
            new Color(0.95f, 0.40f, 0.08f, 0.95f), true, new Color(0.26f, 0.14f, 0.10f)),

        // Violet stone, iron, violet pyramids, black water.
        [TownIds.Dungeon] = new(
            new Color(0.34f, 0.28f, 0.40f), new Color(0.24f, 0.24f, 0.26f), new Color(0.42f, 0.22f, 0.52f), RoofShape.Pyramid,
            new Color(0.10f, 0.12f, 0.20f, 0.85f), false, new Color(0.24f, 0.22f, 0.26f)),

        // A log palisade, a hide gate, brown battlements, a muddy ditch.
        [TownIds.Stronghold] = new(
            new Color(0.48f, 0.34f, 0.20f), new Color(0.62f, 0.50f, 0.34f), new Color(0.40f, 0.28f, 0.16f), RoofShape.Battlements,
            new Color(0.36f, 0.28f, 0.16f, 0.85f), false, new Color(0.46f, 0.36f, 0.24f)),

        // Grey-green stone, reeds, thatched cones, swamp water.
        [TownIds.SwampBestiary] = new(
            new Color(0.44f, 0.48f, 0.40f), new Color(0.56f, 0.52f, 0.30f), new Color(0.62f, 0.54f, 0.30f), RoofShape.Cone,
            new Color(0.24f, 0.34f, 0.18f, 0.85f), false, new Color(0.30f, 0.34f, 0.24f)),

        // Pale crystal, silver, violet domes, a shimmering moat.
        [TownIds.Conflux] = new(
            new Color(0.70f, 0.84f, 0.90f), new Color(0.72f, 0.74f, 0.80f), new Color(0.56f, 0.40f, 0.78f), RoofShape.Dome,
            new Color(0.48f, 0.38f, 0.85f, 0.75f), true, new Color(0.50f, 0.54f, 0.60f)),

        // Gold-red stone, bronze, red pyramids, deep blue water.
        [TownIds.DragonUtopia] = new(
            new Color(0.70f, 0.52f, 0.34f), new Color(0.58f, 0.40f, 0.20f), new Color(0.66f, 0.16f, 0.14f), RoofShape.Pyramid,
            new Color(0.10f, 0.22f, 0.50f, 0.85f), false, new Color(0.52f, 0.40f, 0.28f)),
    };

    /// <summary>The look of the town type (<see cref="TownIds"/>), or <see cref="Default"/>.</summary>
    public static SiegeLook Of(string? town) => town is not null && Looks.TryGetValue(town, out var look) ? look : Default;
}
