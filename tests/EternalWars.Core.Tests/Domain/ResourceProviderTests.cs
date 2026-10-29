using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
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

/// <summary>
/// Resource providers (ADR-068): Water Wheel, Warehouses, Windmill, Mystical Garden. Flagged like mines, they store a week's output every
/// week for their owner, who collects it with a visit or a resource caravan (ADR-079, TOWN-9).
/// </summary>
public class ResourceProviderTests
{
    // The provider's 2×2 footprint at (2, 0) puts its entrance at (3, 1); hero A starts below it.
    private static (GameSession Session, ResourceProvider Provider) World(string definition, PlayerId? owner = null)
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        var provider = Add<ResourceProvider>(state, definition, 2, 0);
        if (owner is { } player)
        {
            provider.Owner = player;
            provider.WasClaimed = true;
        }

        return (Start(state), provider);
    }

    private static ResourceSet Resources(GameSession session) => session.State.GetPlayer(Blue).Resources;

    /// <summary>Sends <paramref name="hero"/> through the provider's visit again, as if it walked in on <paramref name="day"/>.</summary>
    private static void VisitOn(GameSession session, ResourceProvider provider, HeroId hero, int day)
    {
        session.State.Date = new GameDate(day);
        session.Interactions.Visit(session.State.GetHero(hero), provider);
    }

    private static List<ResourceSet> Weeks(GameSession session, string definition, int weeks) =>
        Enumerable.Range(1, weeks).Select(w => ResourceProviderRules.Roll(MapObjectCatalog.Default.Get(definition), new GameDate(((w - 1) * 7) + 1), session.State.Random)).ToList();

    [Fact]
    public void FlaggingANeutralWaterWheel_Gives500AtOnce_AndAnotherVisitThatWeekFindsItEmpty()
    {
        var (session, wheel) = World(BuiltInMapObjects.WaterWheel);
        var events = new List<IEvent>();
        session.Events.Subscribe<ProviderOwnerChanged>(events.Add);
        session.Events.Subscribe<ResourcesFound>(events.Add);
        session.Events.Subscribe<MapObjectFoundEmpty>(events.Add);
        var before = Resources(session).Gold;

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North)); // onto the entrance

        Assert.Equal(Blue, wheel.Owner);
        Assert.Equal(before + 500, Resources(session).Gold);
        VisitOn(session, wheel, C, 7);
        Assert.Equal(before + 500, Resources(session).Gold);
        Assert.Equal(
            new IEvent[] { new ProviderOwnerChanged(wheel.Id, null, Blue), new ResourcesFound(A, wheel.Id, new ResourceSet(Gold: 500)), new MapObjectFoundEmpty(C, wheel.Id) },
            events);
    }

    [Fact]
    public void AnOwnedProvider_StoresAWeekAtEveryWeekStart_AndTheOwnersVisitTakesEverything()
    {
        var (session, wheel) = World(BuiltInMapObjects.WaterWheel, Blue);
        Assert.Equal(new ResourceSet(Gold: 500), wheel.Stored); // owned from the start: the first week is stored at game start

        while (session.State.Date.AbsoluteDay < 15)
        {
            session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id)).ThrowIfRejected();
        }

        Assert.Equal(new ResourceSet(Gold: 2500), wheel.Stored); // + 1000 in weeks 2 and 3
        var before = Resources(session).Gold;
        session.Interactions.Visit(session.State.GetHero(A), wheel);

        Assert.Equal(before + 2500, Resources(session).Gold);
        Assert.True(wheel.Stored.IsEmpty);
    }

    [Fact]
    public void CapturingAnEnemyProvider_TakesWhatItsOwnerLeftThere_AndNoExtraWeek()
    {
        var (session, mill) = World(BuiltInMapObjects.Windmill, Red);
        mill.Stored = new ResourceSet(Ore: 4, Crystal: 3);
        var ore = Resources(session).Ore;

        session.Step(A, Direction.North);

        Assert.Equal(Blue, mill.Owner);
        Assert.True(mill.Stored.IsEmpty);
        Assert.Equal(ore + 4, Resources(session).Ore);
    }

    [Fact]
    public void AWarehouse_ProducesItsResourceOrGoldAtRandom_WithoutAChoice()
    {
        var (session, _) = World(BuiltInMapObjects.Windmill);

        var lumber = Weeks(session, BuiltInMapObjects.WarehouseId(ResourceType.Lumber), 40);
        Assert.All(lumber, r => Assert.True(r == new ResourceSet(Lumber: 8) || r == new ResourceSet(Gold: 500), r.ToString()));
        Assert.Contains(new ResourceSet(Lumber: 8), lumber);
        Assert.Contains(new ResourceSet(Gold: 500), lumber);

        Assert.All(Weeks(session, BuiltInMapObjects.WarehouseId(ResourceType.Sulfur), 20), r => Assert.True(r == new ResourceSet(Sulfur: 5) || r == new ResourceSet(Gold: 500)));
        Assert.All(Weeks(session, BuiltInMapObjects.WarehouseId(ResourceType.Gold), 5), r => Assert.Equal(new ResourceSet(Gold: 2000), r));
    }

    [Fact]
    public void TheWindmill_ProducesThreeToSixOfOneResourceOtherThanGold()
    {
        var (session, _) = World(BuiltInMapObjects.Windmill);
        var weeks = Weeks(session, BuiltInMapObjects.Windmill, 40);

        Assert.All(weeks, r =>
        {
            Assert.Equal(0, r.Gold);
            var kinds = Enum.GetValues<ResourceType>().Where(t => r[t] > 0).ToList();
            Assert.InRange(r[Assert.Single(kinds)], 3, 6);
        });
        Assert.True(weeks.Select(r => Enum.GetValues<ResourceType>().First(t => r[t] > 0)).Distinct().Count() > 2);
    }

    [Fact]
    public void TheMysticalGarden_ProducesAtRandom500GoldOrFiveOfOneMagicResource()
    {
        var (session, _) = World(BuiltInMapObjects.MysticalGarden);
        var weeks = Weeks(session, BuiltInMapObjects.MysticalGarden, 40);

        Assert.All(weeks, r => Assert.True(r == new ResourceSet(Gold: 500) || r == new ResourceSet(Crystal: 5) || r == new ResourceSet(Mercury: 5) || r == new ResourceSet(Sulfur: 5), r.ToString()));
        Assert.Contains(new ResourceSet(Gold: 500), weeks);
        Assert.Contains(weeks, r => r.Gold == 0);
    }

    [Fact]
    public void TheWaterWheel_Produces500InTheGamesFirstWeekOnly()
    {
        var wheel = MapObjectCatalog.Default.Get(BuiltInMapObjects.WaterWheel);
        var random = new GameRandom(1);
        Assert.Equal(new ResourceSet(Gold: 500), ResourceProviderRules.Roll(wheel, new GameDate(3), random));
        Assert.Equal(new ResourceSet(Gold: 1000), ResourceProviderRules.Roll(wheel, new GameDate(8), random));
        Assert.Equal(new ResourceSet(Gold: 1000), ResourceProviderRules.Roll(wheel, new GameDate(36), random));
    }

    [Fact]
    public void Providers_AndTheirOwner_AreInTheMapFormat_SetInTheEditor_AndUndoable()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.WaterWheel, new(2, 2), out var wheel));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.WarehouseId(ResourceType.Ore), new(6, 2), out _));
        var player = editor.Scenario.Players[0].Id;
        Assert.Equal(EditResult.Ok, editor.SetProviderOwner(wheel, player));
        Assert.Equal(EditResult.UnknownPlayer, editor.SetProviderOwner(wheel, new PlayerId(7)));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(26, MapDocument.CurrentFormatVersion);
        Assert.Contains("\"kind\": \"provider\"", json, StringComparison.Ordinal);
        Assert.Equal(
            new[] { (BuiltInMapObjects.WaterWheel, (PlayerId?)player), (BuiltInMapObjects.WarehouseId(ResourceType.Ore), null) },
            restored.ObjectsOf<ResourceProvider>().Select(p => (p.Definition.Id, p.Owner)));
        Assert.True(restored.ObjectsOf<ResourceProvider>().First().WasClaimed);

        editor.Undo();
        Assert.Null(((ResourceProvider)editor.Scenario.Objects.First(o => o.Id == wheel)).Owner);
    }

    [Fact]
    public void FlaggingAndCollecting_Replay()
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        Add<ResourceProvider>(state, BuiltInMapObjects.WarehouseId(ResourceType.Crystal), 2, 0);
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 5)));
        session.Start();
        session.Step(A, Direction.North);

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Contains("claimed True", GameStateDigest.Describe(session.State), StringComparison.Ordinal);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void TheAi_FlagsANeutralProviderNearby()
    {
        var (session, warehouse) = World(BuiltInMapObjects.WarehouseId(ResourceType.Crystal));

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        Assert.Equal(Blue, warehouse.Owner);
    }
}
