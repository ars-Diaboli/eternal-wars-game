using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Towns;

/// <summary>How far streets and houses fill the town scene, by the town hall's level (TOWN-19, ADR-301).</summary>
public enum Settlement
{
    /// <summary>No town hall: no streets.</summary>
    None,

    /// <summary>The Village Hall.</summary>
    Village,

    /// <summary>The Town Hall.</summary>
    Town,

    /// <summary>The City Hall and the Capitol.</summary>
    City,
}

/// <summary>
/// What the town scene shows besides the plots (the town screen plan's step 3, task 3, ADR-301): the streets and houses of its
/// <see cref="Settlement"/>, the walls as the siege has them (<see cref="WallParts"/>, <see cref="Moat"/>, <see cref="WallHealth"/>; the same
/// <see cref="SiegeRules.Create"/> builds both, so the town looks as it fights), the owner whose colour its banners wear, and the garrison hero
/// whose banner flies on the keep (TOWN-22) with the town type of the hero's own school (its charge on the banner).
/// </summary>
public sealed record TownScenery(
    Settlement Settlement,
    IReadOnlySet<SiegeStructureKind> WallParts,
    bool Moat,
    int WallHealth,
    PlayerId? Owner,
    HeroId? GarrisonHero,
    string? GarrisonHeroTown)
{
    /// <summary>The settlement a town hall level gives: none at 0, a village at 1, a town at 2, a city from 3 on (the Capitol too).</summary>
    public static Settlement SettlementOf(int townHallLevel) => townHallLevel switch
    {
        <= 0 => Settlement.None,
        1 => Settlement.Village,
        2 => Settlement.Town,
        _ => Settlement.City,
    };

    /// <summary>The town's scenery today.</summary>
    public static TownScenery Of(GameState state, Town town)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(town);
        var siege = SiegeRules.SpecOf(town) is { } spec ? SiegeRules.Create(spec) : null;
        var parts = siege?.Structures.Select(s => s.Kind).ToHashSet() ?? [];
        var hero = town.GarrisonHero;
        return new TownScenery(
            SettlementOf(town.Level(BuiltInBuildings.TownHall)),
            parts,
            siege is { Moat.Count: > 0 },
            siege is null ? 0 : SiegeRules.WallHealth(siege.FortLevel),
            town.Owner,
            hero?.Id,
            hero is null ? null : ProgressionService.TownOf(state, hero));
    }
}
