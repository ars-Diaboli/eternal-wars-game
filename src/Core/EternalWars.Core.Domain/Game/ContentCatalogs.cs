using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The content tables a game is built from, passed together so no table is left at its built-in copy by mistake: creatures, the hero roster,
/// buildings and artifacts, and the spells the artifact table's scrolls hold. Each one left out is the built-in table.
/// </summary>
public sealed record ContentCatalogs
{
    public ContentCatalogs(CreatureCatalog? creatures = null, HeroCatalog? heroes = null, BuildingCatalog? buildings = null, ArtifactCatalog? artifacts = null)
    {
        Creatures = creatures ?? CreatureCatalog.Default;
        Heroes = heroes ?? HeroCatalog.Default;
        Buildings = buildings ?? BuildingCatalog.Default;
        Artifacts = artifacts ?? ArtifactCatalog.Default;
    }

    /// <summary>The built-in tables.</summary>
    public static ContentCatalogs Default { get; } = new();

    public CreatureCatalog Creatures { get; init; }

    public HeroCatalog Heroes { get; init; }

    public BuildingCatalog Buildings { get; init; }

    public ArtifactCatalog Artifacts { get; init; }

    /// <summary>The game's spells: those of the artifact table (its scrolls hold them).</summary>
    public SpellCatalog Spells => Artifacts.Spells;
}
