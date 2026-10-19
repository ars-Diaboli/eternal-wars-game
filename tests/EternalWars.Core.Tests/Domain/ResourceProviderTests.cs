using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Resource providers (ADR-068): Water Wheel, Warehouses, Windmill, Mystical Garden, collected once a week per object.</summary>
public class ResourceProviderTests
{
    // The provider's 2×2 footprint at (2, 0) puts its entrance at (3, 1); hero A starts below it.
    private static (GameSession Session, ResourceProvider Provider) World(string definition)
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        var provider = Add<ResourceProvider>(state, definition, 2, 0);
        return (Start(state), provider);
    }

    private static ResourceSet Resources(GameSession session) => session.State.GetPlayer(Blue).Resources;

    /// <summary>Sends <paramref name="hero"/> through the provider's visit again, as if it walked in on <paramref name="day"/>.</summary>
    private static void VisitOn(GameSession session, ResourceProvider provider, HeroId hero, int day)
    {
        session.State.Date = new GameDate(day);
        session.Interactions.Visit(session.State.GetHero(hero), provider);
    }

    [Fact]
    public void TheWaterWheel_Gives500InTheFirstWeek_1000Later_AndOnlyOnceAWeek()
    {
        var (session, wheel) = World(BuiltInMapObjects.WaterWheel);
        var found = new List<ResourcesFound>();
        var empty = new List<MapObjectFoundEmpty>();
        session.Events.Subscribe<ResourcesFound>(found.Add);
        session.Events.Subscribe<MapObjectFoundEmpty>(empty.Add);
        var before = Resources(session).Gold;

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North)); // onto the entrance

        Assert.Equal(before + 500, Resources(session).Gold);
        Assert.Equal(new[] { new ResourcesFound(A, wheel.Id, new ResourceSet(Gold: 500)) }, found);

        VisitOn(session, wheel, C, 7); // same week, another hero: nothing
        Assert.Equal(before + 500, Resources(session).Gold);
        Assert.Equal(new[] { new MapObjectFoundEmpty(C, wheel.Id) }, empty);

        VisitOn(session, wheel, C, 8);
        Assert.Equal(before + 1500, Resources(session).Gold);
        VisitOn(session, wheel, A, 36); // month 2: still 1000, "the first week" is the game's first week only
        Assert.Equal(before + 2500, Resources(session).Gold);
    }

    [Fact]
    public void AWarehouse_OffersItsResourceOrGold_AndTheWeekIsTakenOnlyWhenTheVisitorChooses()
    {
        var (session, warehouse) = World(BuiltInMapObjects.WarehouseId(ResourceType.Lumber));
        var lumber = Resources(session).Lumber;

        session.Step(A, Direction.North);

        var decision = Assert.IsType<ResourceChoiceDecision>(session.State.PendingDecision);
        Assert.Equal(new[] { new ResourceSet(Lumber: 8), new ResourceSet(Gold: 500) }, decision.Options);
        Assert.False(warehouse.Visits.IsSpent);
        Assert.Equal(MoveResult.DecisionPending, session.Step(A, Direction.South)); // nothing else until it is answered
        Assert.False(session.Execute(new ChooseResourceRewardCommand(Blue, 2)).IsAccepted);
        Assert.False(session.Execute(new ChooseResourceRewardCommand(Red, 0)).IsAccepted);

        Assert.True(session.Execute(new ChooseResourceRewardCommand(Blue, 0)).IsAccepted);

        Assert.Null(session.State.PendingDecision);
        Assert.Equal(lumber + 8, Resources(session).Lumber);
        Assert.True(warehouse.Visits.IsSpent);
        VisitOn(session, warehouse, C, 5);
        Assert.Null(session.State.PendingDecision); // taken this week
    }

    [Fact]
    public void TheMagicWarehouses_GiveFive_AndTheGoldWarehouse2000WithoutAChoice()
    {
        var (session, sulfur) = World(BuiltInMapObjects.WarehouseId(ResourceType.Sulfur));
        session.Step(A, Direction.North);
        Assert.Equal(new[] { new ResourceSet(Sulfur: 5), new ResourceSet(Gold: 500) }, Assert.IsType<ResourceChoiceDecision>(session.State.PendingDecision).Options);

        var (goldSession, _) = World(BuiltInMapObjects.WarehouseId(ResourceType.Gold));
        var gold = Resources(goldSession).Gold;
        goldSession.Step(A, Direction.North);
        Assert.Null(goldSession.State.PendingDecision);
        Assert.Equal(gold + 2000, Resources(goldSession).Gold);
        Assert.Equal(MapObjectKind.ResourceProvider, sulfur.Kind);
    }

    [Fact]
    public void TheWindmill_GivesThreeToSixOfOneResourceOtherThanGold()
    {
        var (session, windmill) = World(BuiltInMapObjects.Windmill);
        var found = new List<ResourceSet>();
        session.Events.Subscribe<ResourcesFound>(e => found.Add(e.Resources));

        for (var week = 1; week <= 40; week++)
        {
            VisitOn(session, windmill, A, ((week - 1) * 7) + 1);
        }

        Assert.Equal(40, found.Count);
        Assert.All(found, r =>
        {
            Assert.Equal(0, r.Gold);
            var kinds = Enum.GetValues<ResourceType>().Where(t => r[t] > 0).ToList();
            Assert.InRange(r[Assert.Single(kinds)], 3, 6);
        });
        Assert.True(found.Select(r => Enum.GetValues<ResourceType>().First(t => r[t] > 0)).Distinct().Count() > 2);
    }

    [Fact]
    public void TheMysticalGarden_GivesAtRandom500GoldOrFiveOfOneMagicResource()
    {
        var (session, garden) = World(BuiltInMapObjects.MysticalGarden);
        var found = new List<ResourceSet>();
        session.Events.Subscribe<ResourcesFound>(e => found.Add(e.Resources));

        for (var week = 1; week <= 40; week++)
        {
            VisitOn(session, garden, A, ((week - 1) * 7) + 1);
        }

        Assert.Null(session.State.PendingDecision); // never a choice
        Assert.All(found, r => Assert.True(r == new ResourceSet(Gold: 500) || r == new ResourceSet(Crystal: 5) || r == new ResourceSet(Mercury: 5) || r == new ResourceSet(Sulfur: 5), r.ToString()));
        Assert.Contains(new ResourceSet(Gold: 500), found);
        Assert.Contains(found, r => r.Gold == 0);
    }

    [Fact]
    public void Providers_AreInTheMapFormat_TheEditorPlacesThem_AndTheValidatorAcceptsThem()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.WaterWheel, new(2, 2), out _));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.WarehouseId(ResourceType.Ore), new(6, 2), out _));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.True(MapDocument.CurrentFormatVersion >= 16);
        Assert.Contains("\"kind\": \"provider\"", json, StringComparison.Ordinal);
        Assert.Equal(
            new[] { BuiltInMapObjects.WaterWheel, BuiltInMapObjects.WarehouseId(ResourceType.Ore) },
            restored.ObjectsOf<ResourceProvider>().Select(p => p.Definition.Id));
    }

    [Fact]
    public void TheChoice_IsACommandThatReplays()
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        Add<ResourceProvider>(state, BuiltInMapObjects.WarehouseId(ResourceType.Crystal), 2, 0);
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 5)));
        session.Start();
        session.Step(A, Direction.North);
        session.Execute(new ChooseResourceRewardCommand(Blue, 1)).ThrowIfRejected();

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Contains("resourceReward", json, StringComparison.Ordinal);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void TheAi_VisitsAProviderNearby_AndTakesTheMoreValuableOption()
    {
        var (session, warehouse) = World(BuiltInMapObjects.WarehouseId(ResourceType.Crystal));
        var crystal = Resources(session).Crystal;

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        Assert.True(warehouse.Visits.IsSpent);
        Assert.Equal(crystal + 5, Resources(session).Crystal); // 5 crystal beats 500 gold by the AI's values
    }
}
