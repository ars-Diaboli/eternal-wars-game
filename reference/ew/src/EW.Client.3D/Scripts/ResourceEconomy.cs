using System.Collections.Generic;
using System.Linq;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;

namespace EW.Scripts;

public sealed class ResourceIncomeEvent : GamePlayerEvent
{
    public ResourceType ResourceType { get; init; }
    public int Amount { get; init; }
}

public sealed class ResourceSpendRequestEvent : GamePlayerEvent
{
    public IReadOnlyDictionary<ResourceType, int> Costs { get; init; }
    public bool Succeeded { get; set; }
}

public class ResourcesCollection : Dictionary<ResourceType, int>
{
    public ResourcesCollection(IReadOnlyDictionary<ResourceType, int> resources)
    {
        foreach ((ResourceType resourceType, int amount) in resources)
        {
            Add(resourceType, amount);
        }
    }

    public override string ToString() => string.Join(", ", this.Select(pair => $"{pair.Key}: {pair.Value}"));
}

public class ResourceEconomy
{
    private readonly Dictionary<ResourceType, int> _resources = new();
    
    public ResourceEconomy()
    {
        foreach (ResourceType resourceType in System.Enum.GetValues<ResourceType>())
        {
            _resources[resourceType] = 0;
        }
    }
    
    public ResourcesCollection Resources => new (_resources);

    public bool CanAfford(IReadOnlyDictionary<ResourceType, int> costs)
    {
        foreach ((ResourceType resourceType, int amount) in costs)
        {
            if (amount <= 0 || _resources[resourceType] < amount)
            {
                return false;
            }
        }

        return true;
    }

    public void Add(ResourceType resourceType, int amount)
    {
        if (amount <= 0) return;

        _resources[resourceType] += amount;
    }    
    
    public void Add(IReadOnlyDictionary<ResourceType, int> resources)
    {
        if (resources.Any(r => r.Value <= 0)) return;

        foreach ((ResourceType resourceType, int amount) in resources)
        {
            _resources[resourceType] += amount;
        }
    }

    public bool TrySpend(IReadOnlyDictionary<ResourceType, int> costs)
    {
        if (!CanAfford(costs))
        {
            return false;
        }

        foreach ((ResourceType resourceType, int amount) in costs)
        {
            _resources[resourceType] -= amount;
        }

        return true;
    }
}
