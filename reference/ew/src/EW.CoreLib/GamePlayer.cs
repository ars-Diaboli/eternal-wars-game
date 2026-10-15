using System.Diagnostics.CodeAnalysis;
using EW.CoreLib.Interfaces;
using EW.CoreLib.Messaging;
using EW.CoreLib.Types;
using EW.CoreLib.Types.Events;

namespace EW.CoreLib;

public class GamePlayer() 
    : IHandleEvent<NewDay>, IPersist<GamePlayerStateDto>
{
    public required string DisplayName { get; init; }
    public required PlayerControlType ControlType { get; init; }
    public required PlayerColor PlayerColor { get; init; }
    public List<Hero> Heroes { get; } = new(8);
    public List<Town> Towns { get; } = new(8);
    public List<IProfitableEntity> ProfitableEntities { get; init; } = new();
    public ResourceEconomy Economy { get; } = new();
    public FogOfWarSystem FogOfWar { get; } = new();

    public void Handle(NewDay e)
    {
        foreach (var profitableEntity in ProfitableEntities)
        {
            Economy.Add(profitableEntity.GetTurnIncome());
        }
    }

    public GamePlayerStateDto ToState() => new()
    {
        DisplayName = DisplayName,
        ControlType = ControlType,
        Color = PlayerColor,
        Resources = Economy.Resources
            .Select(resource => new ResourceStateDto
            {
                ResourceType = resource.Key,
                Amount = resource.Value
            })
            .ToList()
    };

    public void FromState(GamePlayerStateDto dto)
    {
        foreach (var resource in dto.Resources)
            Economy.Add(resource.ResourceType, resource.Amount);
    }
}