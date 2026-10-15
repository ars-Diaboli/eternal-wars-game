namespace EW.CoreLib.Types;

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