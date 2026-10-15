using System.Diagnostics.CodeAnalysis;
using EW.CoreLib.Interfaces;
using EW.CoreLib.Events;
using EW.CoreLib.Messaging;
using EW.CoreLib.Types;

namespace EW.CoreLib;

public class ResourcePile : IRemovableMapObject, IPersist<RemovableMapObjectStateDto>
{
    public required string ObjectId { get; init; }
    public required Position Position { get; init; }
    public required ResourceType ResourceType { get; init; }
    public required int Amount { get; init; }
    public Position InteractionTile => Position;
    public byte[,] Size => MapObjectSizes.RemovableObject;

    public ResourcePile() { }

    public bool CanInteractFromTile(Position tilePosition)
    {
        throw new NotImplementedException();
    }

    public void OnPlayerInteract(Hero playerHero)
    {
        playerHero.OwnerPlayer?.Economy.Add(ResourceType, Amount);
        Hub.Instance.Publish(new MapObjectRemoved { MapObject = this });
    }

    public RemovableMapObjectStateDto ToState() => new()
    {
        ObjectType = RemovableMapObjectTypeDto.ResourcePile,
        ObjectId = ObjectId,
        Position = Position,
        ResourceType = ResourceType,
        Amount = Amount
    };

    public void FromState(RemovableMapObjectStateDto dto)
    {
        // ObjectId = dto.ObjectId;
        // Position = dto.Position;
        // ResourceType = dto.ResourceType;
        // Amount = dto.Amount;
    }
}
