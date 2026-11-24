using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>What the status bar and the hover card say about the thing under the pointer, worded by who owns it (ADR-309).</summary>
public class MapDescriptionTests
{
    // Blue's A at (0, 4), Red's B at (19, 4); three ore pits at the top: Blue's, Red's and nobody's.
    private static GameState Mines(out Mine own, out Mine enemy, out Mine free)
    {
        var state = Create([.. Enumerable.Repeat(new string('.', 20), 4), "A..................B", .. Enumerable.Repeat(new string('.', 20), 6)]);
        own = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 1, 0);
        enemy = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 7, 0);
        free = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 13, 0);
        (own.Owner, enemy.Owner) = (Blue, Red);
        return state;
    }

    private static MapDescription At(GameState state, MapObject mapObject) => MapDescriptions.At(state, Blue, mapObject.VisitPosition)!;

    [Fact]
    public void AnOwnMine_SaysYours_AndAnUnownedOne_WhatItWouldProduce()
    {
        var state = Mines(out var own, out var enemy, out var free);
        Start(state);

        Assert.Equal(new MapDescription("Ore pit", "you", "+2 ore a day", "Yours: +2 ore a day"), At(state, own));
        Assert.Equal(new MapDescription("Ore pit", "Red", "+2 ore a day", "Red's: +2 ore a day; yours when captured"), At(state, enemy));
        Assert.Equal(new MapDescription("Ore pit", MapDescriptions.NoOwner, "+2 ore a day", "Produces 2 ore a day when captured"), At(state, free));
    }

    [Fact]
    public void TheHoverCard_ShowsTheName_TheOwner_AndWhatItGives()
    {
        var state = Mines(out _, out var enemy, out _);
        Start(state);

        Assert.Equal(["Ore pit", "Owner: Red", "+2 ore a day"], At(state, enemy).CardLines);
    }

    [Fact]
    public void ACapturedMine_ChangesItsWording()
    {
        var state = Mines(out _, out _, out var free);
        var session = Start(state);

        Assert.True(session.MoveTo(A, free.VisitPosition).Arrived);

        var said = MapDescriptions.Of(state, Blue, free); // its visit cell now shows the hero
        Assert.Equal((Blue as PlayerId?, "you", "Yours: +2 ore a day"), (free.Owner, said.Owner, said.Status));
    }

    [Fact]
    public void ADwelling_TellsItsOwnerHowManyWait()
    {
        var state = Mines(out _, out _, out _);
        var own = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 1, 6);
        var free = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 7, 6);
        var enemy = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 13, 6);
        (own.Owner, enemy.Owner) = (Blue, Red);
        Start(state);
        (own.Available, enemy.Available) = (4, 9);
        var creature = state.Creatures.Get(own.Creature).Name;

        Assert.Equal($"Yours: 4 {creature} waiting", At(state, own).Status);
        Assert.Equal($"Recruits {creature} when captured", At(state, free).Status);
        Assert.Equal($"Recruits {creature}", At(state, free).Gives);
        Assert.Equal($"Red's: Recruits {creature}; yours when captured", At(state, enemy).Status); // no count: only the owner sees it
    }

    [Fact]
    public void TownsAndHeroes_SayWhoseTheyAre()
    {
        var state = Mines(out _, out _, out _);
        var town = AddTown(state, TownIds.Haven, 12, 4, Red);
        Start(state);
        var b = state.GetHero(B);

        Assert.Equal((town.Name, "Red", "Red's; yours when captured"), (At(state, town).Name, At(state, town).Owner, At(state, town).Status));
        town.Owner = Blue;
        Assert.Equal(("you", "Yours"), (At(state, town).Owner, At(state, town).Status));
        town.Owner = null;
        Assert.Equal((MapDescriptions.NoOwner, "Yours when captured"), (At(state, town).Owner, At(state, town).Status));
        Assert.Equal(new MapDescription(state.GetHero(A).Name, "you", null, "Your hero"), MapDescriptions.At(state, Blue, state.GetHero(A).Position));
        Assert.Equal(new MapDescription(b.Name, "Red", null, "Red's hero"), MapDescriptions.At(state, Blue, b.Position));
    }

    [Fact]
    public void AProvider_SaysWhatThisWeekBrings_AndAGarrison_OnlyWhoseItIs()
    {
        var state = Mines(out _, out _, out _);
        var wheel = Add<ResourceProvider>(state, BuiltInMapObjects.WaterWheel, 1, 6);
        var garrison = Add<MapGarrison>(state, BuiltInMapObjects.Garrison, 7, 6);
        (wheel.Owner, garrison.Owner) = (Blue, Red);
        Start(state);

        Assert.Equal(new MapDescription("Water wheel", "you", "This week: 500 gold", "Yours: This week: 500 gold"), At(state, wheel));
        Assert.Equal(new MapDescription("Garrison", "Red", null, "Red's; yours when captured"), At(state, garrison));
    }

    [Fact]
    public void WhatCannotBeOwned_HasNoOwner_AndTheFogHidesTheRest()
    {
        var state = Mines(out _, out _, out var free);
        var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 3, 6);
        Start(state, revealAll: false);

        var shown = MapDescriptions.At(state, Blue, pile.VisitPosition)!;
        Assert.Equal((pile.Definition.Name, null, "A pile of gold."), (shown.Name, shown.Owner, shown.Status));
        Assert.Equal([pile.Definition.Name], shown.CardLines);
        Assert.Null(MapDescriptions.At(state, Blue, free.Position)); // not yet explored
        Assert.Equal("Grass", MapDescriptions.At(state, Blue, new MapPosition(0, 5))!.Name);
    }

    [Fact]
    public void AMineUnderTheFog_ShowsAsLastSeen()
    {
        var state = Create(new GameOptions(FogMode.ShroudWithRefog, RefogMargin: 2), new string('.', 20), new string('.', 20), "A" + new string('.', 19), new string('.', 20));
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 8, 0);
        mine.Owner = Red;
        var session = Start(state, revealAll: false);
        Assert.True(session.MoveTo(A, new MapPosition(5, 2)).Arrived);
        session.MoveTo(A, new MapPosition(0, 2));

        Assert.Equal(new MapDescription("Ore pit", "Red (then)", null, "Last seen here; it may have changed since."), MapDescriptions.At(state, Blue, mine.Position));
    }
}
