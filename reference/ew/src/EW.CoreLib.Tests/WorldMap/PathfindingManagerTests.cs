using EW.CoreLib.Map;
using EW.CoreLib.Tests.WorldMap;
using EW.CoreLib.Types;
using NUnit.Framework;

namespace EW.CoreLib.Tests.aStar;

[TestFixture]
public class PathfindingManagerTests : GameMapTestsBase
{
    [Test]
    public void GetPointPath_DiagonalConnectionsExist_ReturnsDirectDiagonalRoute()
    {
        SetUpMap();

        var route = Map.Pathfinding.GetPath(
            new Position(0, 0),
            new Position(2, 2),
            Faction.Neutral, 0, 0)
            .Select(w => w.Position).ToArray();

        Assert.That(route, Is.EqualTo([
            new Position(1, 1),
            new Position(2, 2)
        ]));
    }

    [Test]
    public void GetPointPath_WhenSnowShortcutIsMoreExpensive_PrefersLongerGrassRoute()
    {
        var surfaceLayer = MapSize.Test10.FillLayer(TerrainSurface.Grassland);
        surfaceLayer[0, 1] = TerrainSurface.Snow;
        surfaceLayer[0, 2] = TerrainSurface.Snow;
        surfaceLayer[0, 3] = TerrainSurface.Snow;
        surfaceLayer[0, 4] = TerrainSurface.Snow;

        var map = new GameMap(surfaceLayer);
        var destination = new Position(0, 5);

        var route = map.Pathfinding.GetPath(RedHero.Position, destination, RedHero.Faction, 0, 0)
            .Select(w => w.Position).ToArray();

        Assert.That(route, Is.Not.Empty);
        Assert.That(route[^1], Is.EqualTo(destination));
        Assert.That(route.Any(step => step.Vertical == 1),
            "The hero should leave the snowy shortcut and travel through the grass lane.");
        Assert.That(route.Where(step => step != destination).All(step => map.Cells[step.Vertical, step.Horizontal].Surface == TerrainSurface.Grassland),
            "All intermediate steps should stay on the cheaper grassland route.");
    }

    [Test]
    public void RequestRoute_HeroMovingAcrossMap_GoesAround4x4ObstacleSmoothly()
    {
        var obstacle = new FakeStaticMapObject
        {
            ObjectId = "obstacle-1",
            Position = new Position(3, 3),
            Size = MapObjectSizes._4x4
        };

        SetUpMap(obstacle);
        var destination = new Position(4, 9);

        var route = Map.Pathfinding.GetPath(RedHero.Position, destination, RedHero.Faction, 0, 0)
            .Select(w => w.Position).ToArray();

        Assert.That(route, Is.Not.Empty);
        Assert.That(route[^1], Is.EqualTo(destination));
        Assert.That(route.Any(step => step.Vertical != RedHero.Position.Vertical),
            "The hero should leave the starting row to navigate around the obstacle.");

        for (var i = 1; i < route.Length; i++)
        {
            var step = route[i];

            var previous = route[i - 1];
            var stepDistance = Math.Abs((int)step.Vertical - (int)previous.Vertical) 
                               + Math.Abs((int)step.Horizontal - (int)previous.Horizontal);

            Assert.That(stepDistance, Is.GreaterThanOrEqualTo(1),
                $"Hero movement should be smooth. Invalid step from {previous} to {step}.");
            Assert.That(stepDistance, Is.LessThanOrEqualTo(2),
                $"Hero movement should be smooth. Invalid step from {previous} to {step}.");
        }
    }

    [Test]
    public void GetPointPath_WhenDestinationIsCompletelySeparated_ReturnsEmptyPath()
    {
        var blockingWall = new FakeStaticMapObject
        {
            ObjectId = "wall-1",
            Position = new Position(5, 0),
            Size = new byte[1, 10]
            {
                { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }
            }
        };

        SetUpMap(blockingWall);
        var start = new Position(2, 2);
        var destination = new Position(8, 8);

        var route = Map.Pathfinding.GetPath(start, destination, Faction.Neutral, 0, 0)
            .Select(w => w.Position).ToArray();

        Assert.That(route, Is.Empty);
    }

    [Test]
    public void GetPointPath_WhenResourcePileBlocksDirectRoute_ConstructsPathAroundIt()
    {
        SetUpMap(ResourcePile);
        var destination = new Position(0, 4);

        var route = Map.Pathfinding.GetPath(RedHero.Position, destination, RedHero.Faction, 0, 0)
            .Select(w => w.Position).ToArray();

        Assert.That(route, Is.Not.Empty);
        Assert.That(route[^1], Is.EqualTo(destination));
        Assert.That(route, Does.Not.Contain(ResourcePile.Position),
            "The hero must not move through the occupied resource pile cell.");
        Assert.That(route.Any(step => step.Vertical != RedHero.Position.Vertical),
            "The path should detour around the resource pile instead of staying on the blocked line.");

        foreach (var step in route)
        {
            Assert.That(Map.CellAt(step).Occupied, Is.False,
                $"Route step {step} should not enter an occupied cell.");
        }
    }

    [Test]
    public void GetPointPath_AfterHeroPicksUpResourcePile_CanStepOnFormerPileCell()
    {
        var lowerLaneWall = new FakeStaticMapObject
        {
            ObjectId = "wall-1",
            Position = new Position(1, 0),
            Size = new byte[,] { { 1, 1, 1, 1, 1 } }
        };
        SetUpMap(lowerLaneWall, ResourcePile);

        ResourcePile.OnPlayerInteract(RedHero);

        var pathAfterPickup = Map.Pathfinding.GetPath(
            RedHero.Position, new Position(0, 4), RedHero.Faction, 0, 0);
        var routeAfterPickup = pathAfterPickup.Select(w => w.Position).ToArray();

        Assert.That(routeAfterPickup, Does.Contain(ResourcePile.Position),
            "After pickup the freed resource pile cell should become part of the direct route.");
        Assert.That(routeAfterPickup, Is.EqualTo([
            new Position(0, 1),
            new Position(0, 2),
            new Position(0, 3),
            new Position(0, 4)
        ]));
        Assert.That(Map.GetMapObjectAt(ResourcePile.Position), Is.Null);
    }
}
