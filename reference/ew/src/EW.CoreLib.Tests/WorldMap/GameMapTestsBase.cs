using EW.CoreLib.Interfaces;
using EW.CoreLib.Map;
using EW.CoreLib.Tests.aStar;
using EW.CoreLib.Types;
using NUnit.Framework;

namespace EW.CoreLib.Tests.WorldMap;

public abstract class GameMapTestsBase
{
   protected static GamePlayer RedPlayer;
   protected static GamePlayer BluePlayer;
   protected static Hero RedHero;
   protected static ResourcePile ResourcePile;
   protected static FakeStaticMapObject Rock;
   protected static Town NeutralTown;
   protected static FakeStaticMapObject MapObject4x4;
   protected static GameMap Map;

   [SetUp]
   public virtual void Setup()
   {
      RedPlayer = new() { DisplayName = "Red Player", PlayerColor = PlayerColor.Red, ControlType = PlayerControlType.Human };
      BluePlayer = new () { DisplayName = "Blue Player", PlayerColor = PlayerColor.Blue, ControlType = PlayerControlType.Ai };
      RedHero = new Hero(Faction.Temple, Position.Zero, RedPlayer) 
      { 
         ObjectId = "hero-1",
         BaseStats = new HeroData { MaxMovementPoints = 3400 },
         Path = { CurrentMovementPoints = 1200 } 
      };
      RedPlayer.Heroes.Add(RedHero);
      ResourcePile = new ResourcePile
      {
         ObjectId = "resource-1",
         Position = new Position(0, 2),
         ResourceType = ResourceType.Gold,
         Amount = 500
      };
      Rock = new FakeStaticMapObject
      {
         ObjectId = "rock-1",
         Position = new Position(0, 2),
         Size = MapObjectSizes._1x1
      };
      MapObject4x4 = new FakeStaticMapObject
      {
         ObjectId = "object-4x4",
         Position = new Position(2, 3),
         Size = MapObjectSizes._4x4
      };
      NeutralTown = new Town
      {
         ObjectId = "town-1",
         Position = new Position(8, 8),
         Faction = Faction.Academy
      };
   }
   
   protected void SetUpMap(params IWorldMapObject[] maoObjects)
   {
      Map = new GameMap(MapSize.Test10.FillLayer(TerrainSurface.Grassland), maoObjects);
   }
}