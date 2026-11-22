using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The town scene's scenery (the town screen plan's step 3, task 3, ADR-301): streets by the town hall's level, walls as the siege has them, the
/// owner's banners and the garrison hero's banner on the keep.
/// </summary>
public class TownSceneryTests
{
    /// <summary>A Haven town at (0, 0) has its entrance at (3, 6), where Blue's hero A stands.</summary>
    private static readonly string[] Rows =
    [
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "...A........",
        "............",
    ];

    [Theory]
    [InlineData(0, Settlement.None)]
    [InlineData(1, Settlement.Village)]
    [InlineData(2, Settlement.Town)]
    [InlineData(3, Settlement.City)]
    [InlineData(BuiltInBuildings.CapitolLevel, Settlement.City)]
    public void Settlement_GrowsWithTheTownHall_InThreeSteps(int townHall, Settlement expected)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.TownHall, townHall);

        Assert.Equal(expected, TownScenery.Of(state, town).Settlement);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Walls_AreTheSiegesParts_ForEachFortLevel(int fort)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Fort, fort);

        var scenery = TownScenery.Of(state, town);

        var siege = SiegeRules.SpecOf(town) is { } spec ? SiegeRules.Create(spec) : null;
        Assert.Equal(siege?.Structures.Select(s => s.Kind).ToHashSet() ?? [], scenery.WallParts);
        Assert.Equal(siege is { Moat.Count: > 0 }, scenery.Moat);
        var expected = fort switch
        {
            0 => (Parts: Array.Empty<SiegeStructureKind>(), Moat: false, Health: 0),
            1 => ([SiegeStructureKind.Wall, SiegeStructureKind.Gate], false, 2),
            2 => ([SiegeStructureKind.Wall, SiegeStructureKind.Gate, SiegeStructureKind.Keep], true, 2),
            _ => ([SiegeStructureKind.Wall, SiegeStructureKind.Gate, SiegeStructureKind.Keep, SiegeStructureKind.Tower], true, 3),
        };
        Assert.Equal(expected.Parts.ToHashSet(), scenery.WallParts);
        Assert.Equal(expected.Moat, scenery.Moat);
        Assert.Equal(expected.Health, scenery.WallHealth);
    }

    [Fact]
    public void Banners_WearTheOwnersColour_AndTheGarrisonHerosBannerFliesOnceAHeroIsInside()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var session = Start(state);

        var empty = TownScenery.Of(state, town);
        Assert.Equal(Blue, empty.Owner);
        Assert.Null(empty.GarrisonHero);
        Assert.Null(empty.GarrisonHeroTown);

        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();

        var held = TownScenery.Of(state, town);
        Assert.Equal(A, held.GarrisonHero);
        Assert.Equal(ProgressionService.TownOf(state, state.GetHero(A)), held.GarrisonHeroTown);
    }

    [Fact]
    public void ANeutralTown_HasNoOwnerForItsBanners()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, null);

        Assert.Null(TownScenery.Of(state, town).Owner);
    }
}
