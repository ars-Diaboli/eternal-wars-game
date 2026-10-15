using EW.CoreLib.Types;

namespace EW.CoreLib;

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
