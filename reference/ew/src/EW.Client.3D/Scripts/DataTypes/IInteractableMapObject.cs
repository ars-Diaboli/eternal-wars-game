using Godot;

namespace EW.Scripts.DataTypes;

public interface IInteractableMapObject : IWorldMapObject
{
    Vector2I InteractionTile { get; }
    bool CanInteractFromTile(Vector2I tilePosition);
    void OnPlayerInteract(Heroes.Hero playerHero);
}
