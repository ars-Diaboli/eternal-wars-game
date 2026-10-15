using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Players;

public sealed class Player(PlayerId id, string name, bool isHuman, TeamId? team = null)
{
    public PlayerId Id { get; } = id;

    public string Name { get; internal set; } = name;

    public bool IsHuman { get; internal set; } = isHuman;

    /// <summary>Alliance; defaults to a team of its own (free-for-all).</summary>
    public TeamId Team { get; internal set; } = team ?? new TeamId(id.Value);

    public ResourceSet Resources { get; internal set; }
}
