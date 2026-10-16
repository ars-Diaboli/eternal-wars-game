namespace EternalWars.Core.Domain.Economy;

public enum ResourceType
{
    Gold,
    Lumber,
    Ore,
    Crystal,
    Mercury,
    Sulfur,
}

/// <summary>Immutable bag of the six resources.</summary>
public readonly record struct ResourceSet(
    int Gold = 0,
    int Lumber = 0,
    int Ore = 0,
    int Crystal = 0,
    int Mercury = 0,
    int Sulfur = 0)
{
    public static ResourceSet Empty { get; } = new();

    public static ResourceSet Of(ResourceType type, int amount) => type switch
    {
        ResourceType.Gold => new(Gold: amount),
        ResourceType.Lumber => new(Lumber: amount),
        ResourceType.Ore => new(Ore: amount),
        ResourceType.Crystal => new(Crystal: amount),
        ResourceType.Mercury => new(Mercury: amount),
        ResourceType.Sulfur => new(Sulfur: amount),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public int this[ResourceType type] => type switch
    {
        ResourceType.Gold => Gold,
        ResourceType.Lumber => Lumber,
        ResourceType.Ore => Ore,
        ResourceType.Crystal => Crystal,
        ResourceType.Mercury => Mercury,
        ResourceType.Sulfur => Sulfur,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public bool CanAfford(ResourceSet cost) =>
        Gold >= cost.Gold && Lumber >= cost.Lumber && Ore >= cost.Ore &&
        Crystal >= cost.Crystal && Mercury >= cost.Mercury && Sulfur >= cost.Sulfur;

    public static ResourceSet operator +(ResourceSet a, ResourceSet b) => new(
        a.Gold + b.Gold, a.Lumber + b.Lumber, a.Ore + b.Ore,
        a.Crystal + b.Crystal, a.Mercury + b.Mercury, a.Sulfur + b.Sulfur);

    public static ResourceSet operator -(ResourceSet a, ResourceSet b) => new(
        a.Gold - b.Gold, a.Lumber - b.Lumber, a.Ore - b.Ore,
        a.Crystal - b.Crystal, a.Mercury - b.Mercury, a.Sulfur - b.Sulfur);

    public static ResourceSet operator *(ResourceSet a, int factor) => new(
        a.Gold * factor, a.Lumber * factor, a.Ore * factor,
        a.Crystal * factor, a.Mercury * factor, a.Sulfur * factor);
}
