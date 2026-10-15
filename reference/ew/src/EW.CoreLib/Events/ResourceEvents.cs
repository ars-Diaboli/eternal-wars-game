using EW.CoreLib.Types;

namespace EW.CoreLib.Events;

public sealed class ResourceIncomeEvent : GamePlayerEvent
{
    public ResourceType ResourceType { get; init; }
    public int Amount { get; init; }
}

public sealed class ResourceSpendRequestEvent : GamePlayerEvent
{
    public required IReadOnlyDictionary<ResourceType, int> Costs { get; init; }
    public bool Succeeded { get; set; }
}