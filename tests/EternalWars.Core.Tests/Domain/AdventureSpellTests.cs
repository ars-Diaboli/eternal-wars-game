using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Adventure spells (ADR-071, ADR-073, ADR-120): mana and 500 movement points; Town Portal, Dimension Door, View Terrain, Pathfinder's Call, Ashwalk.</summary>
public class AdventureSpellTests
{
    private static Hero Mage(GameState state, params string[] spells)
    {
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        hero.Knowledge = 10;
        foreach (var spell in spells)
        {
            hero.Learn(spell);
        }

        return hero;
    }

    private static CommandResult Cast(GameSession session, string spell, MapPosition? target = null) =>
        session.Execute(new CastAdventureSpellCommand(Blue, A, spell, target));

    [Fact]
    public void ACast_CostsItsMana_And500MovementPoints_AndNeedsBoth()
    {
        var state = Create("A.........................", "........................B.");
        var hero = Mage(state, AdventureMagic.ViewTerrain);
        var session = Start(state);
        var (mana, movement) = (hero.Mana, hero.MovementPoints);

        Assert.True(Cast(session, AdventureMagic.ViewTerrain).IsAccepted);
        Assert.Equal((mana - AdventureMagic.Cost(state, hero, AdventureMagic.ViewTerrain), movement - AdventureMagic.MovementCost), (hero.Mana, hero.MovementPoints));

        hero.MovementPoints = AdventureMagic.MovementCost - 1;
        Assert.Contains("500 movement points", Cast(session, AdventureMagic.ViewTerrain).Error);
        hero.MovementPoints = 2000;
        hero.Mana = 0;
        Assert.Contains("mana", Cast(session, AdventureMagic.ViewTerrain).Error);
        hero.Mana = 50;
        Assert.False(Cast(session, AdventureMagic.TownPortal).IsAccepted); // not in its book
        hero.HasSpellBook = false;
        Assert.Contains("spell book", Cast(session, AdventureMagic.ViewTerrain).Error);
        Assert.Contains("cannot be cast on the map", AdventureMagic.Refusal(state, Mage(state, "light.bless"), "light.bless", null));
    }

    [Fact]
    public void TownPortal_TakesTheHeroToTheNearestOwnTownWithAFreeEntrance()
    {
        var state = Create(Enumerable.Repeat("..............................", 10).Append("...........................A..").Append("............................B.").ToArray());
        var near = AddTown(state, TownIds.Haven, 20, 0, Blue);
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        Mage(state, AdventureMagic.TownPortal);
        var session = Start(state);

        Assert.True(Cast(session, AdventureMagic.TownPortal).IsAccepted);
        Assert.Equal(near.VisitPosition, state.GetHero(A).Position);
    }

    [Theory]
    [InlineData(6, 0, true)]
    [InlineData(4, 4, true)]
    [InlineData(7, 0, false)] // too far
    [InlineData(2, 1, false)] // not a straight line
    public void DimensionDoor_GoesUpToSixTilesInAStraightLine(int dx, int dy, bool goes)
    {
        var state = Create(new[] { "A............." }.Concat(Enumerable.Repeat("..............", 9)).Append(".............B").ToArray());
        var hero = Mage(state, AdventureMagic.DimensionDoor);
        var session = Start(state);
        var to = new MapPosition(hero.Position.X + dx, hero.Position.Y + dy);

        Assert.Equal(goes, Cast(session, AdventureMagic.DimensionDoor, to).IsAccepted);
        Assert.Equal(goes ? to : new MapPosition(0, 0), hero.Position);
    }

    [Fact]
    public void DimensionDoor_LandsOnlyOnExploredFreeGround_OnceADay()
    {
        var state = Create("A..#..........", "..............", "..............", ".............B");
        var hero = Mage(state, AdventureMagic.DimensionDoor);
        hero.Knowledge = 20;
        var session = Start(state);

        Assert.False(Cast(session, AdventureMagic.DimensionDoor, new MapPosition(3, 0)).IsAccepted); // rock
        Assert.True(Cast(session, AdventureMagic.DimensionDoor, new MapPosition(5, 0)).IsAccepted); // over the rock
        Assert.Contains("once a day", Cast(session, AdventureMagic.DimensionDoor, new MapPosition(8, 0)).Error);

        session.EndTurn();
        session.EndTurn();
        Assert.True(Cast(session, AdventureMagic.DimensionDoor, new MapPosition(8, 0)).IsAccepted);

        // Before anything is explored (a game not started): the tile is under the fog.
        var fogged = Create("A..................", "................B.");
        Assert.Contains("explored", AdventureMagic.Refusal(fogged, Mage(fogged, AdventureMagic.DimensionDoor), AdventureMagic.DimensionDoor, new MapPosition(6, 0)));
    }

    [Fact]
    public void ViewTerrain_RevealsTwelveTilesAround()
    {
        var state = Create(new[] { "A............................." }.Concat(Enumerable.Repeat("..............................", 29)).Append(".............................B").ToArray());
        Mage(state, AdventureMagic.ViewTerrain);
        var session = Start(state, revealAll: false);
        var edge = new MapPosition(12, 0); // the radius is round
        Assert.False(state.Fog.IsExplored(Blue, edge));

        Assert.True(Cast(session, AdventureMagic.ViewTerrain).IsAccepted);

        Assert.True(state.Fog.IsExplored(Blue, edge));
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(20, 20)));
    }

    [Fact]
    public void PathfindersCall_MakesSwampNative_UntilTheDayEnds()
    {
        var state = Create("Assssssss", "........B");
        var hero = Mage(state, AdventureMagic.PathfindersCall);
        var session = Start(state);
        var swamp = MovementRules.StepCost(state, hero, new MapPosition(1, 0), new MapPosition(2, 0), Direction.East);
        Assert.True(swamp > TerrainCatalog.NativeCost);

        Assert.True(Cast(session, AdventureMagic.PathfindersCall).IsAccepted);
        Assert.Equal(TerrainCatalog.NativeCost, MovementRules.StepCost(state, hero, new MapPosition(1, 0), new MapPosition(2, 0), Direction.East));

        session.EndTurn();
        session.EndTurn();
        Assert.Equal(swamp, MovementRules.StepCost(state, hero, new MapPosition(1, 0), new MapPosition(2, 0), Direction.East));
    }
}
