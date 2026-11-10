using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The content tables a game is built from, passed together so no table is left at its built-in copy by mistake: creatures, the hero roster,
/// buildings, artifacts and the spells the artifact table's scrolls hold, map object definitions and terrain rules. Each one left out is the
/// built-in table.
/// </summary>
public sealed record ContentCatalogs
{
    public ContentCatalogs(
        CreatureCatalog? creatures = null,
        HeroCatalog? heroes = null,
        BuildingCatalog? buildings = null,
        ArtifactCatalog? artifacts = null,
        MapObjectCatalog? objects = null,
        TerrainCatalog? terrains = null)
    {
        Creatures = creatures ?? CreatureCatalog.Default;
        Heroes = heroes ?? HeroCatalog.Default;
        Buildings = buildings ?? BuildingCatalog.Default;
        Artifacts = artifacts ?? ArtifactCatalog.Default;
        Objects = objects ?? MapObjectCatalog.Default;
        Terrains = terrains ?? TerrainCatalog.Default;
    }

    /// <summary>The built-in tables.</summary>
    public static ContentCatalogs Default { get; } = new();

    public CreatureCatalog Creatures { get; init; }

    public HeroCatalog Heroes { get; init; }

    public BuildingCatalog Buildings { get; init; }

    public ArtifactCatalog Artifacts { get; init; }

    /// <summary>Map object definitions: what a map's objects are made from (<see cref="GameState.ObjectCatalog"/>).</summary>
    public MapObjectCatalog Objects { get; init; }

    /// <summary>Terrain movement rules (<see cref="GameState.Terrains"/>).</summary>
    public TerrainCatalog Terrains { get; init; }

    /// <summary>The game's spells: those of the artifact table (its scrolls hold them).</summary>
    public SpellCatalog Spells => Artifacts.Spells;
}
