using EW.CoreLib.Events;
using EW.CoreLib.Interfaces;
using EW.CoreLib.Messaging;
using EW.CoreLib.Types;

namespace EW.CoreLib.Map;

public class GameMap : IPersist<MapStateDto>,
    IHandleEvent<MapObjectRemoved>,
    IHandleEvent<MapActorPlaced>,
    IHandleEvent<MapActorRemoved>,
    IHandleEvent<MapActorPositionChanged>
{
    private Dictionary<string, IWorldMapObject> MapObjects { get; } = new();
    private Dictionary<string, IRemovableMapObject> RemovableObjects { get; } = new();
    private Dictionary<string, IMapActor> Heroes { get; } = new();
    public IReadOnlyCollection<IWorldMapObject> PermanentObjects => MapObjects.Values;
    public IReadOnlyCollection<IRemovableMapObject> TemporalObjects => RemovableObjects.Values;
    public IReadOnlyCollection<IMapActor> MapHeroes => Heroes.Values;
    public MapCell[,] Cells { get; private set; }
    public AStarGrid2D Pathfinding { get; } = new();

    private int Height => Cells.GetLength(0);
    private int Width => Cells.GetLength(1);
    public MapCell CellAt(Position position) => Cells[position.Vertical, position.Horizontal];

    private int MaxObjectHeight => MapObjects.Any() ? MapObjects.Max(x => x.Value.Height) : 0;
    private int MaxObjectWidth => MapObjects.Any() ? MapObjects.Max(x => x.Value.Width) : 0;

    public GameMap()
    {
        Hub.Instance.Subscribe(this);
    }

    public GameMap(TerrainSurface[,] surfaceLayer, params IWorldMapObject[]? objects)
        : this()
    {
        InitCells(surfaceLayer);

        if (objects != null)
        {
            foreach (var mapObject in objects)
            {
                if (mapObject is IRemovableMapObject removable)
                    AddRemovableObject(removable);
                else if (mapObject is IMapActor hero)
                    AddHero(hero);
                else AddPermanentObject(mapObject);
            }
        }
        
        InitPathfinding();
    }

    private void InitCells(TerrainSurface[,] surfaceLayer)
    {
        Cells = new MapCell[surfaceLayer.GetLength(0), surfaceLayer.GetLength(1)];
        for (uint x = 0; x < Height; x++)
        for (uint y = 0; y < Width; y++)
        {
            Cells[x, y] = new MapCell { Surface = surfaceLayer[x, y] };
        }
    }

    private void AddRemovableObject(IRemovableMapObject obj)
    {
        if (!obj.ValidatePlacement(Height, Width)) throw new IndexOutOfRangeException("Object is out of bounds");

        RemovableObjects.Add(obj.ObjectId, obj);

        Cells.BakeOccupiedCells(obj);
        InitPathfinding();
    }

    private void AddPermanentObject(IWorldMapObject obj)
    {
        if (obj is IRemovableMapObject or IMapActor) throw new ArgumentException("Invalid object type");
        if (!obj.ValidatePlacement(Height, Width)) throw new IndexOutOfRangeException("Object is out of bounds");

        MapObjects.Add(obj.ObjectId, obj);

        Cells.BakeBlockedCells(obj);
        InitPathfinding();
    }

    private void AddHero(IMapActor hero)
    {
        if (!hero.ValidatePlacement(Height, Width)) throw new IndexOutOfRangeException("Hero is out of bounds");

        Heroes.Add(hero.ObjectId, hero);

        Cells.BakeOccupiedCells(hero);
        //InitPathfinding();
    }

    private void UpdateActorPosition(IMapActor actor, Position previousPosition)
    {
        if (actor.Position == previousPosition)
        {
            return;
        }

        if (!actor.ValidatePlacement(Height, Width))
        {
            throw new IndexOutOfRangeException("Hero is out of bounds");
        }

        Cells.UnOccupyCells(actor, previousPosition);
        Cells.BakeOccupiedCells(actor);
        //InitPathfinding();
    }

    public bool RemoveObject(IWorldMapObject obj)
    {
        if (obj is IRemovableMapObject removableObject)
            return RemoveTemporalMapObject(removableObject);
        if (obj is IMapActor hero)
            //RemoveHero(hero)
            return false;
        
        return RemovePermanentMapObject(obj);
    }

    private bool RemovePermanentMapObject(IWorldMapObject obj)
    {
        if (!MapObjects.Remove(obj.ObjectId)) return false;

        var start = new Position(
            Math.Max(obj.Position.Vertical - MaxObjectHeight, 0),
            Math.Max(obj.Position.Horizontal - MaxObjectWidth, 0));

        var end = new Position(
            obj.Position.Vertical + obj.Height,
            obj.Position.Horizontal + obj.Width);

        Cells.UnblockSector(start, end);

        foreach (var o in MapObjects.ObjectsInSector(start, end))
            Cells.BakeBlockedCells(o);

        //InitPathfinding();

        return true;
    }

    private bool RemoveTemporalMapObject(IRemovableMapObject obj)
    {
        if (!RemovableObjects.Remove(obj.ObjectId)) return false;

        var start = new Position(
            Math.Max(obj.Position.Vertical - MaxObjectHeight, 0),
            Math.Max(obj.Position.Horizontal - MaxObjectWidth, 0));

        var end = new Position(
            obj.Position.Vertical + obj.Height,
            obj.Position.Horizontal + obj.Width);

        Cells.UnOccupySector(start, end);

        foreach (var o in RemovableObjects.RemovableObjectsInSector(start, end))
            Cells.BakeOccupiedCells(o);

        InitPathfinding();

        return true;
    }

    private void InitPathfinding()
    {
        Pathfinding.Init(Cells);
    }

    public IWorldMapObject? GetMapObjectAt(Position position)
    {
        if (position.Vertical >= Height || position.Horizontal >= Width) return null;

        foreach (var temporalObject in RemovableObjects.Values)
        {
            if (temporalObject.OccupiesPosition(position)) return temporalObject;
        }

        foreach (var hero in Heroes.Values)
        {
            if (hero.OccupiesPosition(position)) return hero;
        }

        foreach (var mapObject in MapObjects.Values)
        {
            if (mapObject.OccupiesPosition(position)) return mapObject;
        }

        return null;
    }
    
    #region EventHandlers

    public void Handle(MapObjectRemoved e)
    {
        RemoveObject(e.MapObject);
    }
    
    public void Handle(MapActorPlaced e)
    {
        Cells.BakeOccupiedCells(e.Hero);
    }
    
    public void Handle(MapActorRemoved e)
    {
        Cells.UnOccupyCells(e.Hero, e.Hero.Position);
    }

    public void Handle(MapActorPositionChanged e)
    {
        if (e.Hero.Position == e.PreviousPosition) return;

        if (!e.Hero.ValidatePlacement(Height, Width))
            throw new IndexOutOfRangeException("Hero is out of bounds");

        Cells.UnOccupyCells(e.Hero, e.PreviousPosition);
        Cells.BakeOccupiedCells(e.Hero);
    }

    #endregion

    #region IPersist

    public MapStateDto ToState()
    {
        var surfaceTiles = new List<TerrainSurface>(Height * Width);
        for (var row = 0; row < Height; row++)
        for (var column = 0; column < Width; column++)
        {
            surfaceTiles.Add(Cells[row, column].Surface);
        }

        return new MapStateDto
        {
            SurfaceLayer = new SurfaceLayerStateDto
            {
                Height = Height,
                Width = Width,
                Tiles = surfaceTiles
            },
            StaticObjects = PermanentObjects.Select(ToStaticObjectState).ToList(),
            RemovableObjects = TemporalObjects.Select(ToRemovableObjectState).ToList(),
            // Hero DTOs' OwnerPlayerIndex is patched by the controller, which holds the players list.
            Heroes = MapHeroes.OfType<Hero>().Select(hero => hero.ToState()).ToList()
        };
    }

    private static StaticMapObjectStateDto ToStaticObjectState(IWorldMapObject mapObject) =>
        mapObject switch
        {
            IPersist<TownStateDto> town => town.ToState(),
            IPersist<StaticMapObjectStateDto> persist => persist.ToState(),
            _ => throw new NotSupportedException($"Static map object type '{mapObject.GetType().Name}' is not supported.")
        };

    private static RemovableMapObjectStateDto ToRemovableObjectState(IRemovableMapObject removableObject) =>
        removableObject is IPersist<RemovableMapObjectStateDto> persist
            ? persist.ToState()
            : throw new NotSupportedException($"Removable map object type '{removableObject.GetType().Name}' is not supported.");

    public void FromState(MapStateDto dto)
    {
        InitCells(CreateSurfaceLayer(dto.SurfaceLayer));

        foreach (var staticObjectState in dto.StaticObjects)
        {
            AddPermanentObject(CreateStaticObject(staticObjectState));
        }

        foreach (var removableObjectState in dto.RemovableObjects)
        {
            AddRemovableObject(CreateRemovableObject(removableObjectState));
        }

        foreach (var heroState in dto.Heroes)
        {
            var hero = new Hero();
            hero.FromState(heroState);
            AddHero(hero);
        }

        InitPathfinding();
    }

    private static TerrainSurface[,] CreateSurfaceLayer(SurfaceLayerStateDto surfaceLayerState)
    {
        if (surfaceLayerState.Tiles.Count != surfaceLayerState.Height * surfaceLayerState.Width)
        {
            throw new InvalidDataException("Scenario state contains an invalid surface layer.");
        }

        var surfaceLayer = new TerrainSurface[surfaceLayerState.Height, surfaceLayerState.Width];
        for (var row = 0; row < surfaceLayerState.Height; row++)
        for (var column = 0; column < surfaceLayerState.Width; column++)
        {
            surfaceLayer[row, column] = surfaceLayerState.Tiles[(row * surfaceLayerState.Width) + column];
        }

        return surfaceLayer;
    }

    private static IWorldMapObject CreateStaticObject(StaticMapObjectStateDto dto)
    {
        switch (dto)
        {
            case TownStateDto townState:
                var town = new Town { ObjectId = townState.ObjectId, Position = townState.Position, Faction = townState.Faction };
                town.FromState(townState);
                return town;
            default:
                throw new NotSupportedException(
                    $"Static map object type '{dto.ObjectType}' is not supported.");
        }
    }

    private static IRemovableMapObject CreateRemovableObject(RemovableMapObjectStateDto dto) =>
        dto.ObjectType switch
        {
            RemovableMapObjectTypeDto.ResourcePile => 
                new ResourcePile { ObjectId = dto.ObjectId, Position = dto.Position, ResourceType = dto.ResourceType, Amount = dto.Amount },
            _ => throw new NotSupportedException(
                $"Removable map object type '{dto.ObjectType}' is not supported.")
        };

    #endregion
}
