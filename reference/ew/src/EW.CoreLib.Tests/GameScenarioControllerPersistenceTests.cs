using EW.CoreLib.Events;
using EW.CoreLib.Map;
using EW.CoreLib.Messaging;
using EW.CoreLib.Tests.WorldMap;
using EW.CoreLib.Types;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace EW.CoreLib.Tests;

[TestFixture]
public class GameScenarioControllerPersistenceTests : GameMapTestsBase
{
    private string _tempFilePath;

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Test]
    public void SaveToJsonFile_AndLoadFromJsonFile_RestoresScenarioState()
    {
        RedPlayer.Economy.Add(ResourceType.Gold, 500);
        BluePlayer.Economy.Add(ResourceType.Wood, 7);

        RedHero.ObjectId = "hero-red-1";
        RedHero.SetPosition(new Position(2, 3));
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(2, 4), 100, 1f, true, true));
        RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(2, 5), 100, 1f, true, true));
        RedHero.Path.StartMovement();
        RedHero.Path.RequestStopAtNearestWaypoint();

        var blueHero = new Hero(Faction.Academy, new Position(6, 7), BluePlayer)
        {
            ObjectId = "hero-blue-1"
        };

        RedPlayer.Heroes.Add(RedHero);
        BluePlayer.Heroes.Add(blueHero);

        var resourcePile = new ResourcePile
        {
            ObjectId = "resource-1",
            Position = new Position(4, 12),
            ResourceType = ResourceType.Gold,
            Amount = 500
        };

        var surface = MapSize.Test16.FillLayer(TerrainSurface.Grassland);
        surface[1, 1] = TerrainSurface.Snow;

        var map = new GameMap(surface, NeutralTown, resourcePile, RedHero, blueHero);

        var scenario = new GameScenarioController(map, [RedPlayer, BluePlayer], RedPlayer)
        {
            Metadata = new()
            {
                DisplayName = "Persistence Test",
                Description = "Round-trip scenario state through JSON."
            }
        };
        scenario.UiHost.SelectHero(RedHero);
        Hub.Instance.Publish(new PlayerEndedTurn());

        _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");

        scenario.SaveToJsonFile(_tempFilePath);
        var restored = GameScenarioController.LoadFromJsonFile(_tempFilePath);

        Assert.That(restored.Metadata.DisplayName, Is.EqualTo("Persistence Test"));
        Assert.That(restored.Metadata.Description, Is.EqualTo("Round-trip scenario state through JSON."));
        Assert.That(restored.Players.Count, Is.EqualTo(2));
        Assert.That(restored.Map.Cells[1, 1].Surface, Is.EqualTo(TerrainSurface.Snow));
        Assert.That(restored.Map.Cells[8, 10].Blocked, Is.True);
        Assert.That(restored.Map.Cells[4, 12].Occupied, Is.True);
        Assert.That(restored.UiHost.HostPlayer.DisplayName, Is.EqualTo("Red Player"));
        Assert.That(restored.UiHost.SelectedHero?.ObjectId, Is.EqualTo("hero-red-1"));
        Assert.That(restored.TurnController.CurrentPlayer.DisplayName, Is.EqualTo("Blue Player"));

        var restoredRedPlayer = restored.Players.Single(player => player.DisplayName == "Red Player");
        var restoredBluePlayer = restored.Players.Single(player => player.DisplayName == "Blue Player");
        var restoredRedHero = restoredRedPlayer.Heroes.Single(hero => hero.ObjectId == "hero-red-1");
        var restoredBlueHero = restoredBluePlayer.Heroes.Single(hero => hero.ObjectId == "hero-blue-1");

        Assert.That(restoredRedPlayer.PlayerColor.Value, Is.EqualTo(PlayerColor.Red.Value));
        Assert.That(restoredBluePlayer.PlayerColor.Value, Is.EqualTo(PlayerColor.Blue.Value));
        Assert.That(restoredRedPlayer.Economy.Resources[ResourceType.Gold], Is.EqualTo(500));
        Assert.That(restoredBluePlayer.Economy.Resources[ResourceType.Wood], Is.EqualTo(7));
        Assert.That(restoredRedHero.Faction, Is.EqualTo(Faction.Temple));
        Assert.That(restoredRedHero.Position, Is.EqualTo(new Position(2, 3)));
        Assert.That(restoredRedHero.BaseStats.MaxMovementPoints, Is.EqualTo(3400));
        Assert.That(restoredRedHero.Path.CurrentMovementPoints, Is.EqualTo(1200));
        Assert.That(restoredRedHero.Path.MovementWaypoints.Select(waypoint => waypoint.Position).ToArray(),
            Is.EqualTo([new Position(2, 4), new Position(2, 5)]));
        Assert.That(restoredBlueHero.Position, Is.EqualTo(new Position(6, 7)));
        Assert.That(restored.Map.GetMapObjectAt(new Position(8, 10)), Is.TypeOf<Town>());
        Assert.That(restored.Map.GetMapObjectAt(new Position(4, 12)), Is.TypeOf<ResourcePile>());
        Assert.That(restored.Map.GetMapObjectAt(restoredRedHero.Position), Is.SameAs(restoredRedHero));
    }
}

