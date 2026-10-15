using EW.CoreLib.Tests.aStar;
using EW.CoreLib.Tests.WorldMap;
using EW.CoreLib.Types;
using NUnit.Framework;

namespace EW.CoreLib.Tests;

[TestFixture]
public class GameMapTests : GameMapTestsBase
{
    [Test]
    public void GetMapObjectAt_WhenPositionFallsInsideObjectFootprint_ReturnsThatObject()
    {
        SetUpMap([MapObject4x4]);

        var result = Map.GetMapObjectAt(new Position(4, 5));

        Assert.That(result, Is.SameAs(MapObject4x4));
    }

    [Test]
    public void GetMapObjectAt_WhenTileHasNoObject_ReturnsNull()
    {
        SetUpMap([MapObject4x4]);

        var result = Map.GetMapObjectAt(new Position(0, 0));

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetMapObjectAt_WhenTemporalObjectOccupiesTile_ReturnsTemporalObject()
    {
        var temporalObject = new FakeRemovableMapObject
        {
            ObjectId = "hero-1",
            Position = new Position(1, 1),
            Size = MapObjectSizes.Hero
        };
        SetUpMap(MapObject4x4, temporalObject);

        var result = Map.GetMapObjectAt(new Position(1, 1));

        Assert.That(result, Is.SameAs(temporalObject));
    }

    [Test]
    public void RemoveMapObject_WhenTemporalObjectExists_RemovesItAndFreesItsCell()
    {
        SetUpMap(ResourcePile);

        var removed = Map.RemoveObject(ResourcePile);

        Assert.That(removed, Is.True);
        Assert.That(Map.GetMapObjectAt(ResourcePile.Position), Is.Null);
        Assert.That(Map.Cells[ResourcePile.Position.Vertical, ResourcePile.Position.Horizontal].Blocked, Is.False);
    }

    [Test]
    public void OnPlayerInteract_WhenResourcePilePublishesRemovalEvent_RemovesPileFromMap()
    {
        SetUpMap(ResourcePile, RedHero);

        ResourcePile.OnPlayerInteract(RedHero);

        Assert.That(Map.GetMapObjectAt(ResourcePile.Position), Is.Null);
        Assert.That(Map.CellAt(ResourcePile.Position).Occupied, Is.False);
    }

    [Test]
    public void UpdateActorPosition_WhenHeroMoves_TransfersOccupiedCell()
    {
        SetUpMap(RedHero);

        RedHero.SetPosition(new Position(1, 2));

        Assert.That(Map.Cells[1, 1].Occupied, Is.False);
        Assert.That(Map.Cells[1, 2].Occupied, Is.True);
    }
}
