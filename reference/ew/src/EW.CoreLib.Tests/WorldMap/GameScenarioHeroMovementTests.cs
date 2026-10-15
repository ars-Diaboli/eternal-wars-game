using EW.CoreLib.Map;
using EW.CoreLib.Tests.WorldMap;
using EW.CoreLib.Types;
using NUnit.Framework;

namespace EW.CoreLib.Tests;

[TestFixture]
public class GameScenarioHeroMovementTests : GameMapTestsBase
{
    [Test]
    public void ProcessAnimations_WhenHeroMovesForwardThenBehindStartingPosition_FollowsStraightPath()
    {
        var startingPosition = new Position(4, 4);
        RedHero.SetPosition(startingPosition);
        SetUpMap(RedHero);
        var sut = new GameScenarioController(Map, [RedPlayer], RedPlayer);
        sut.UiHost.SelectHero(RedHero);
        var visitedPositions = new List<Position> { RedHero.Position };

        Position[][] expectedPaths =
        [
            [new Position(4, 5), new Position(4, 6)],
            [new Position(4, 5), startingPosition, new Position(4, 3)]
        ];

        foreach (var expectedPath in expectedPaths)
        {
            var destination = expectedPath[^1];
            sut.Handle(new LeftClickOnMap { Position = destination });
            Assert.That(RedHero.Path.MovementWaypoints.Select(step => step.Position),
                Is.EqualTo(expectedPath));

            sut.Handle(new LeftClickOnMap { Position = destination });
            Assert.That(RedHero.Path.IsMoving, Is.True);

            foreach (var expectedPosition in expectedPath)
            {
                sut.ProcessAnimations(0.25);
                visitedPositions.Add(RedHero.Position);
                Assert.That(RedHero.Position, Is.EqualTo(expectedPosition));
            }

            Assert.That(RedHero.Path.IsMoving, Is.False);
            Assert.That(RedHero.Path.MovementWaypoints, Is.Empty);
        }

        Assert.That(visitedPositions, Is.EqualTo(new[]
        {
            startingPosition,
            new Position(4, 5),
            new Position(4, 6),
            new Position(4, 5),
            startingPosition,
            new Position(4, 3)
        }));
    }

    [Test]
    public void ProcessAnimations_WhenHeroMoves_PathfindingAvoidsNewlyOccupiedCell()
    {
        RedHero.SetPosition(new Position(4, 4));
        SetUpMap(RedHero);
        var sut = new GameScenarioController(Map, [RedPlayer], RedPlayer);
        sut.UiHost.SelectHero(RedHero);
        var destination = new Position(4, 5);
        sut.Handle(new LeftClickOnMap { Position = destination });
        sut.Handle(new LeftClickOnMap { Position = destination });

        sut.ProcessAnimations(0.25);

        Assert.That(RedHero.Position, Is.EqualTo(destination));
        var route = Map.Pathfinding.GetPath(
            new Position(4, 4), new Position(4, 6), RedHero.Faction, 1200, 3400)
            .Select(step => step.Position).ToArray();

        Assert.That(route, Is.Not.Empty);
        Assert.That(route[^1], Is.EqualTo(new Position(4, 6)));
        Assert.That(route, Does.Not.Contain(destination));
    }

    [Test]
    public void HandleLeftClickOnMap_WhenSelectedHeroAndTargetTileIsFree_BuildsPathForHero()
    {
        SetUpMap();
        var sut = new GameScenarioController(Map, [RedPlayer], RedPlayer);
        sut.UiHost.SelectHero(RedHero);

        sut.Handle(new LeftClickOnMap { Position = new Position(0, 2) });

        Assert.That(RedHero.Path.MovementWaypoints.Select(step => step.Position).ToArray(), Is.EqualTo([
            new Position(0, 1),
            new Position(0, 2)
        ]));
        Assert.That(RedHero.Path.MovementWaypoints.All(step => step.MovementCost > 0), Is.True);
    }

    [Test]
    public void HandleLeftClickOnMap_WhenTargetTileIsOccupied_DoesNotBuildPath()
    {
        SetUpMap([Rock]);
        var map = new GameMap(MapSize.Test10.FillLayer(TerrainSurface.Grassland), Rock);
        var sut = new GameScenarioController(map, [RedPlayer], RedPlayer);
        sut.UiHost.SelectHero(RedHero);

        sut.Handle(new LeftClickOnMap { Position = new Position(0, 2) });

        Assert.That(RedHero.Path.MovementWaypoints, Is.Empty);
    }

    [Test]
    public void HandleLeftClickOnMap_WhenClickedPositionMatchesQueuedDestination_SetsHeroMoving()
    {
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1, true, true));
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 2), 100, 1, true, true));

        SetUpMap();
        var sut = new GameScenarioController(Map, [RedPlayer], RedPlayer);
        sut.UiHost.SelectHero(RedHero);

        sut.Handle(new LeftClickOnMap { Position = new Position(0, 2) });

        Assert.That(RedHero.Path.IsMoving, Is.True);
        Assert.That(RedHero.Path.MovementWaypoints.Select(step => step.Position).ToArray(), Is.EqualTo([
            new Position(0, 1),
            new Position(0, 2)
        ]));
    }

    [Test]
    public void HandleRightClickOnMap_WhenSelectedHeroIsMoving_RequestsStopAtNearestWaypoint()
    {
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1, true, true));
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 2), 100, 1, true, true));
        RedHero.Path.StartMovement();

        SetUpMap([Rock]);
        var sut = new GameScenarioController(Map, [RedPlayer], RedPlayer);
        sut.UiHost.SelectHero(RedHero);

        sut.Handle(new RightClickOnMap { Position = new Position(0, 2) });

        Assert.That(RedHero.Path.IsMoving, Is.True);
        Assert.That(RedHero.Path.StopAtNextWaypoint, Is.True);
    }

    [Test]
    public void HandleLeftClickOnMap_WhenClickedObjectIsResourcePile_BuildsPathToAdjacentCell()
    {
        SetUpMap(ResourcePile);
        var sut = new GameScenarioController(Map, [RedPlayer], RedPlayer);
        sut.UiHost.SelectHero(RedHero);

        sut.Handle(new LeftClickOnMap { Position = ResourcePile.Position });

        Assert.That(RedHero.Path.MovementWaypoints.Select(step => step.Position).ToArray(), Is.EqualTo([
            new Position(0, 1)
        ]));
    }

    [Test]
    public void HandleHeroStopped_WhenHeroStopsAdjacentToResourcePile_CollectsAndRemovesIt()
    {
        SetUpMap(ResourcePile);
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1, true, true));
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 2), 100, 1, true, true));
        RedHero.Path.SetPendingInteractionTarget(ResourcePile);
        RedHero.Path.StartMovement();
        RedHero.ProcessAnimation(0.25);
        RedHero.ProcessAnimation(0.25);
        
        Assert.That(RedPlayer.Economy.Resources[ResourceType.Gold], Is.EqualTo(500));
        Assert.That(Map.GetMapObjectAt(ResourcePile.Position), Is.Null);
    }

    [Test]
    public void ProcessAnimations_WhenMapActorIsAnimatable_AdvancesItAndUpdatesMapOccupancy()
    {
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1f, true, true));
        RedHero.Path.StartMovement();

        SetUpMap(RedHero);

        var sut = new GameScenarioController(Map, [RedPlayer], RedPlayer);

        sut.ProcessAnimations(0.25);

        Assert.That(RedHero.Position, Is.EqualTo(new Position(0, 1)));
        Assert.That(Map.Cells[0, 0].Occupied, Is.False);
        Assert.That(Map.Cells[0, 1].Occupied, Is.True);
    }
}
