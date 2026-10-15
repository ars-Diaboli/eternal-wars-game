using EW.CoreLib.Interfaces;
using EW.CoreLib.Types;

namespace EW.CoreLib;

public interface ITown : IOwnableObject;
public class Town : ITown, IFactionOwned, IInteractableMapObject, IPersist<TownStateDto>
{
    public required string ObjectId { get; init; }
    public required Position Position { get; init; }
    public required Faction Faction { get; init; }
    public GamePlayer? OwnerPlayer { get; set; }
    public byte[,] Size => MapObjectSizes.Town;
    public Position InteractionTile => Position + MapObjectSizes.TownInteractionTile;

    public bool CanInteractFromTile(Position tilePosition)
    {
        throw new NotImplementedException();
    }

    public void OnPlayerInteract(Hero playerHero)
    {
        throw new NotImplementedException();
    }

    public TownStateDto ToState() => new()
    {
        ObjectType = MapObjectType.Town,
        ObjectId = ObjectId,
        Position = Position,
        Faction = Faction,
    };

    public void FromState(TownStateDto dto)
    {
        // ObjectId = dto.ObjectId,
        // Position = dto.Position,
        // Size = MapObjectSizes.Town
    }
}