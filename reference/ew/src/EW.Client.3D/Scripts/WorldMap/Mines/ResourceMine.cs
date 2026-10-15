using System.Collections.Generic;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Players;
using Godot;

namespace EW.Scripts.WorldMap.Mines;

public partial class ResourceMine : CapturableMapObject
{
    [Export] public ResourceType GeneratedResource { get; set; } = ResourceType.Gold;
    [Export] public int AmountPerTurn { get; set; } = 1;

    public ResourceMine()
    {
        MapObjectSize = new Vector2I(3, 2);
    }

    public override void OnPlayerInteract(Heroes.Hero playerHero)
    {
        GamePlayer previousOwner = OwnerPlayer;
        base.OnPlayerInteract(playerHero);

        if (!ReferenceEquals(OwnerPlayer, previousOwner))
        {
            GD.Print($"{Name}: now produces {AmountPerTurn} {GeneratedResource} per turn for {OwnerPlayer}.");
        }
    }

    public override IReadOnlyDictionary<ResourceType, int> GetTurnIncome()
    {
        return new Dictionary<ResourceType, int>
        {
            { GeneratedResource, AmountPerTurn }
        };
    }
}
