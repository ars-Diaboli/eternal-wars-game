using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Players;

public sealed class Player(PlayerId id, string name, bool isHuman)
{
    public PlayerId Id { get; } = id;

    public string Name { get; } = name;

    public bool IsHuman { get; } = isHuman;

    public ResourceSet Resources { get; internal set; }
}
