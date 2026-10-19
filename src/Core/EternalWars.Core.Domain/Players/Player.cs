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

    /// <summary>The player's own hero limit (campaign-like maps, ADR-068); null = the map's <c>ScenarioRules.MaxHeroes</c>.</summary>
    public int? MaxHeroes { get; internal set; }

    /// <summary>Out of the game (ADR-057): its turns are skipped and it owns nothing any more.</summary>
    public bool IsDefeated { get; internal set; }

    /// <summary>The day the player lost its last town; the grace period (<c>ScenarioRules.GraceDays</c>) runs from it. Null while it owns a town.</summary>
    public Common.GameDate? TownlessSince { get; internal set; }

    /// <summary>Owned a town at some point; only such players have a grace period.</summary>
    internal bool HadTown { get; set; }

    /// <summary>Had a hero or a town when the game started; only such players can be eliminated or win.</summary>
    internal bool TookPart { get; set; }
}
