using EW.CoreLib.Interfaces;
using EW.CoreLib.Types;

namespace EW.CoreLib.Tests.aStar;

public sealed class FakeStaticMapObject : IStaticMapObject
{
    public required string ObjectId { get; init; }
    public required Position Position { get; init; }
    public required byte[,] Size { get; init; }
}

public sealed class FakeRemovableMapObject : IRemovableMapObject
{
    public required string ObjectId { get; init; }
    public required Position Position { get; init; }
    public required byte[,] Size { get; init; }
    public Position InteractionTile => Position;
    public bool CanInteractFromTile(Position tilePosition) => (InteractionTile - tilePosition).Length() < 2;
    public void OnPlayerInteract(Hero playerHero) { }
}