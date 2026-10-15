using EW.CoreLib.Types;

namespace EW.CoreLib.Interfaces;

public interface IWorldMapObject
{
    string ObjectId { get; }
    Position Position { get; }
    byte[,] Size { get; }
    public int Height => Size.GetLength(0);
    public int Width => Size.GetLength(1);
}
public interface IInteractableMapObject : IWorldMapObject
{
    Position InteractionTile { get; }
    bool CanInteractFromTile(Position tilePosition);
    void OnPlayerInteract(Hero playerHero);
}
public interface IStaticMapObject : IWorldMapObject { }
public interface IRemovableMapObject : IInteractableMapObject { }

public interface IMapActor : IInteractableMapObject
{
    void SetPosition(Position position);
}
