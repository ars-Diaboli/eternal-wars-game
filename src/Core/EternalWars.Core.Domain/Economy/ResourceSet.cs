namespace EternalWars.Core.Domain.Economy;

public enum ResourceType
{
    Gold,
    Wood,
    Ore,
    Mercury,
    Sulfur,
    Crystal,
    Gems,
}

/// <summary>Immutable bag of the seven HoMM-style resources.</summary>
public readonly record struct ResourceSet(
    int Gold = 0,
    int Wood = 0,
    int Ore = 0,
    int Mercury = 0,
    int Sulfur = 0,
    int Crystal = 0,
    int Gems = 0)
{
    public static ResourceSet Empty { get; } = new();

    public int this[ResourceType type] => type switch
    {
        ResourceType.Gold => Gold,
        ResourceType.Wood => Wood,
        ResourceType.Ore => Ore,
        ResourceType.Mercury => Mercury,
        ResourceType.Sulfur => Sulfur,
        ResourceType.Crystal => Crystal,
        ResourceType.Gems => Gems,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public bool CanAfford(ResourceSet cost) =>
        Gold >= cost.Gold && Wood >= cost.Wood && Ore >= cost.Ore && Mercury >= cost.Mercury &&
        Sulfur >= cost.Sulfur && Crystal >= cost.Crystal && Gems >= cost.Gems;

    public static ResourceSet operator +(ResourceSet a, ResourceSet b) => new(
        a.Gold + b.Gold, a.Wood + b.Wood, a.Ore + b.Ore, a.Mercury + b.Mercury,
        a.Sulfur + b.Sulfur, a.Crystal + b.Crystal, a.Gems + b.Gems);

    public static ResourceSet operator -(ResourceSet a, ResourceSet b) => new(
        a.Gold - b.Gold, a.Wood - b.Wood, a.Ore - b.Ore, a.Mercury - b.Mercury,
        a.Sulfur - b.Sulfur, a.Crystal - b.Crystal, a.Gems - b.Gems);
}
