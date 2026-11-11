using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The map tavern (ADR-071, ADR-082): a weekly pool of heroes shared by all players, hired onto a cell next to the entrance.</summary>
public class MapTavernTests
{
    [Fact]
    public void TheMapTavern_HiresFromAWeeklyPoolSharedByAllPlayers_OntoTheCellBelowTheEntrance()
    {
        var state = Create(".......", ".......", "...A...", ".......", "......B");
        var tavern = Add<MapTavern>(state, BuiltInMapObjects.MapTavern, 2, 0); // entrance (3, 1)
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000);
        var session = Start(state);
        session.Step(A, Direction.North);

        var offers = MapTavernRules.Offers(state, tavern);
        Assert.Equal(MapTavernRules.OfferCount, offers.Count);
        Assert.Contains(offers.Select(o => o.Town).Distinct(), _ => true);
        var hired = offers[1];
        session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, hired.Id)).ThrowIfRejected();

        var hero = Assert.Single(state.Heroes, h => h.Definition == hired.Id);
        Assert.Equal((Blue, new MapPosition(3, 2)), (hero.Owner, hero.Position));
        Assert.False(hero.Army.IsEmpty);
        Assert.Equal(10_000 - 2500, state.GetPlayer(Blue).Resources.Gold);
        var after = MapTavernRules.Offers(state, tavern);
        Assert.DoesNotContain(after, h => h.Id == hired.Id);
        Assert.Equal(offers.Where(o => o.Id != hired.Id), after.Take(3));

        state.Date = new GameDate(8);
        Assert.NotEqual(after.Select(o => o.Id), MapTavernRules.Offers(state, tavern).Select(o => o.Id)); // a new week, a new pool
    }

    [Fact]
    public void TheMapTavern_RefusesAtTheHeroLimit_WithoutAFreeCell_OrFromAfar()
    {
        var state = Create(".......", ".......", "...A...", "...C...", "......B");
        var tavern = Add<MapTavern>(state, BuiltInMapObjects.MapTavern, 2, 0);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000);
        var session = Start(state);
        var offer = MapTavernRules.Offers(state, tavern)[0].Id;
        Assert.False(session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, offer)).IsAccepted); // nobody on the entrance

        session.Step(A, Direction.North);
        state.GetPlayer(Blue).MaxHeroes = 2;
        Assert.Contains("at most 2", session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, offer)).Error!, StringComparison.Ordinal);

        state.GetPlayer(Blue).MaxHeroes = null;
        session.Step(C, Direction.North); // C takes the cell below; the next free neighbour is used
        session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, offer)).ThrowIfRejected();
        Assert.NotEqual(new MapPosition(3, 2), state.Heroes.Single(h => h.Definition == offer).Position);
    }
}
