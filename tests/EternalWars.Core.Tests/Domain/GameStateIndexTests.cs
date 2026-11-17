using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The lookups <see cref="GameState"/> indexes (hero by tile, guarded tiles, objects by type, garrisons) answer as a scan would.</summary>
public class GameStateIndexTests
{
    [Fact]
    public void HeroAt_FindsTheHero_HoweverItMovedJoinedOrLeft()
    {
        var state = Create("A....", ".....", "....B");
        var a = state.GetHero(A);
        var b = state.GetHero(B);
        Assert.Same(a, state.HeroAt(new MapPosition(0, 0)));

        var session = Start(state);
        session.Step(A, Direction.East);
        Assert.Null(state.HeroAt(new MapPosition(0, 0)));
        Assert.Same(a, state.HeroAt(new MapPosition(1, 0)));

        a.Position = new MapPosition(3, 1); // set directly, as tests and scenario builders do
        Assert.Null(state.HeroAt(new MapPosition(1, 0)));
        Assert.Same(a, state.HeroAt(new MapPosition(3, 1)));

        var index = state.RemoveHero(b);
        Assert.Null(state.HeroAt(new MapPosition(4, 2)));
        state.AddHero(b, index);
        Assert.Same(b, state.HeroAt(new MapPosition(4, 2)));

        // Two on one tile: the first in Heroes, as before.
        b.Position = a.Position;
        Assert.Same(a, state.HeroAt(a.Position));
        Assert.Null(state.HeroAt(new MapPosition(9, 9)));
    }

    [Fact]
    public void IsGuarded_AgreesWithTheMonstersAround_AsTheyComeAndGo()
    {
        var state = CreateLevels(["......", "......", "......", "......"], ["......", "......", "......", "......"]);
        var corner = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 0, 0);
        var middle = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 2, 2);
        Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 5, 3, level: 1);
        Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 4, 0); // not a monster: guards nothing
        AssertGuardsMatchScan(state);
        Assert.True(state.IsGuarded(new MapPosition(1, 1)));
        Assert.False(state.IsGuarded(new MapPosition(0, 0))); // a monster's own tile is its entrance
        Assert.False(state.IsGuarded(new MapPosition(-1, 0)));
        Assert.Equal([corner, middle], state.GuardsOf(new MapPosition(1, 1))); // lowest id first

        state.RemoveObject(corner);
        AssertGuardsMatchScan(state);
        Assert.False(state.IsGuarded(new MapPosition(0, 1)));
        Assert.Empty(state.GuardsOf(new MapPosition(0, 1)));
        Assert.Equal([middle], state.GuardsOf(new MapPosition(1, 1)));

        state.AddObject(corner, 0); // an undone removal
        AssertGuardsMatchScan(state);
    }

    [Fact]
    public void ObjectsOf_ListsTheType_AndItsSubtypes_InObjectOrder_AfterEachChange()
    {
        var state = Create("..........", "..........", "..........");
        var first = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 0, 0);
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 4, 0);
        AssertObjectsMatchScan(state);

        var second = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 8, 2);
        Assert.Equal([first, second], state.ObjectsOf<MonsterGuard>());

        state.RemoveObject(first);
        AssertObjectsMatchScan(state);
        state.AddObject(first, 2); // after the chest and the second monster
        Assert.Equal([second, first], state.ObjectsOf<MonsterGuard>());
        AssertObjectsMatchScan(state);

        state.RemoveObject(chest);
        Assert.Empty(state.ObjectsOf<TreasureChest>());
        AssertObjectsMatchScan(state);
    }

    [Fact]
    public void GarrisonHeroes_AreFoundByTownId_UntilTheirTownGoes()
    {
        var state = Create(new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20),
            new string('.', 20), new string('.', 20), new string('.', 20));
        var west = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var east = AddTown(state, TownIds.Haven, 10, 0, Blue);
        state.RemoveObject(west);
        state.AddObject(west); // now after the east town in Objects, still the lower id
        var westHero = new Hero(new HeroId(8), "West", Blue, west.VisitPosition);
        var eastHero = new Hero(new HeroId(9), "East", Blue, east.VisitPosition);
        (west.GarrisonHero, east.GarrisonHero) = (westHero, eastHero);

        Assert.Equal([westHero, eastHero], state.GarrisonHeroes);
        Assert.Same(eastHero, state.GetHero(eastHero.Id));
        Assert.Same(west, state.GarrisonOf(westHero.Id));

        state.RemoveObject(west);
        Assert.Equal([eastHero], state.GarrisonHeroes);
        Assert.Throws<CommandRefusedException>(() => state.GetHero(westHero.Id));
    }

    [Fact]
    public void HasTeleporters_FollowsTheGateLinks()
    {
        var state = CreateLevels(["........", "........", "........", "........"], ["........", "........", "........", "........"]);
        var surface = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 2, 0);
        var underground = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 0, 0, 1);
        Assert.False(state.HasTeleporters);

        Link(surface, underground);
        Assert.True(state.HasTeleporters);

        (surface.LinkedGate, underground.LinkedGate) = (null, null);
        var entrance = Add<OneWayGateEntrance>(state, BuiltInMapObjects.OneWayMonolithEntrance, 5, 2, 1);
        var exit = Add<OneWayGateExit>(state, BuiltInMapObjects.OneWayMonolithExit, 5, 2);
        Assert.False(state.HasTeleporters);

        entrance.Exit = exit.Id;
        Assert.True(state.HasTeleporters);
    }

    [Fact]
    public void HasTeleporters_LooksAtEveryKindOfObjectThatCanTeleport()
    {
        // HasTeleporters checks these two types only; a new kind of gate must be added there.
        var teleporting = typeof(MapObject).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(MapObject)) &&
                t.GetProperty(nameof(MapObject.TeleportTarget))!.DeclaringType != typeof(MapObject))
            .OrderBy(t => t.Name);

        Assert.Equal([typeof(OneWayGateEntrance), typeof(TwoWayGate)], teleporting);
    }

    private static void AssertGuardsMatchScan(GameState state)
    {
        for (var level = 0; level < state.Map.Levels; level++)
        {
            for (var y = 0; y < state.Map.Height; y++)
            {
                for (var x = 0; x < state.Map.Width; x++)
                {
                    var tile = new MapPosition(x, y, level);
                    var scan = DirectionExtensions.All.Any(d => state.ObjectCellAt(tile.Step(d))?.Object is MonsterGuard);
                    Assert.True(scan == state.IsGuarded(tile), $"{tile}: guarded {scan}");
                    Assert.Equal(scan, state.GuardsOf(tile).Any());
                }
            }
        }
    }

    private static void AssertObjectsMatchScan(GameState state)
    {
        Assert.Equal(state.Objects.OfType<MonsterGuard>(), state.ObjectsOf<MonsterGuard>());
        Assert.Equal(state.Objects.OfType<TreasureChest>(), state.ObjectsOf<TreasureChest>());
        Assert.Equal(state.Objects, state.ObjectsOf<MapObject>());
    }
}
