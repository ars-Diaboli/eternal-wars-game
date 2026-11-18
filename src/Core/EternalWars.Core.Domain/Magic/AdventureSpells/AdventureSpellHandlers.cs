namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>
/// The registry of adventure spells: the one <see cref="IAdventureSpellHandler"/> of each built spell (<see cref="AdventureMagic.Built"/>). The
/// checks, the cast and the aim's prompt ask it instead of switching on the spell, so a new adventure spell is its row in spells.csv plus one
/// handler class registered here (a test checks every adventure spell of the catalog has exactly one).
/// </summary>
internal static class AdventureSpellHandlers
{
    /// <summary>Every handler.</summary>
    public static IReadOnlyList<IAdventureSpellHandler> All { get; } =
    [
        new TownPortalHandler(),
        new DimensionDoorHandler(),
        new ViewTerrainHandler(),
        new NativeWalkHandler(AdventureMagic.PathfindersCall, [Map.TerrainType.ForestFloor, Map.TerrainType.Grass, Map.TerrainType.Swamp]),
        new NativeWalkHandler(AdventureMagic.Ashwalk, [Map.TerrainType.Lava, Map.TerrainType.Wasteland, Map.TerrainType.CursedLands]),
        new DistantVisionHandler(),
        new DetectMagicHandler(),
        new GraveWhisperHandler(),
        new LedgerOfAgesHandler(),
        new VeilHandler(),
        new EavesdropHandler(),
    ];

    private static readonly Dictionary<string, IAdventureSpellHandler> BySpell = All.ToDictionary(h => h.Spell, StringComparer.Ordinal);

    /// <summary>The handler of the spell, or null when it is not a built adventure spell.</summary>
    public static IAdventureSpellHandler? Find(string spell) => BySpell.GetValueOrDefault(spell);

    /// <summary>The handler of the spell.</summary>
    public static IAdventureSpellHandler For(string spell) =>
        Find(spell) ?? throw new ArgumentOutOfRangeException(nameof(spell), spell, "Unregistered adventure spell.");
}
