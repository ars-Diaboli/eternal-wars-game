namespace EternalWars.Core.Domain.Common;

/// <summary>Strongly typed id of a player slot (0-based, matches turn order).</summary>
public readonly record struct PlayerId(int Value)
{
    public override string ToString() => $"Player#{Value}";
}

/// <summary>Strongly typed id of a hero, unique within a game.</summary>
public readonly record struct HeroId(int Value)
{
    public override string ToString() => $"Hero#{Value}";
}

/// <summary>Strongly typed id of an adventure-map object, unique within a game.</summary>
public readonly record struct MapObjectId(int Value)
{
    public override string ToString() => $"Object#{Value}";
}
