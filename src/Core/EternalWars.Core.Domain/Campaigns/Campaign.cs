using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Campaigns;

/// <summary>
/// A story campaign (ADR-241, FLOW-1): maps played one after another by one person, each won before the next. Heroes and the map events' campaign
/// variables carry from one map to the next.
/// </summary>
public sealed record Campaign(string Name, string Description, IReadOnlyList<CampaignMap> Maps);

/// <summary>
/// One map of a <see cref="Campaign"/>. <see cref="Map"/> names it for the front-end that loads it (a file next to the campaign file, or a shipped
/// map). <see cref="Intro"/> is shown before the map starts, <see cref="Outro"/> once it is won. Which heroes the last map's winner brings in is this
/// map's choice: the roster heroes <see cref="CarryHeroes"/> names (when the player still has them), then its <see cref="CarryStrongest"/> strongest
/// others; with or without their armies (<see cref="KeepArmy"/>) and artifacts (<see cref="KeepArtifacts"/>). The first map carries nothing in.
/// </summary>
public sealed record CampaignMap(string Map, string Title)
{
    public string Intro { get; init; } = "";

    public string Outro { get; init; } = "";

    public IReadOnlyList<string> CarryHeroes { get; init; } = [];

    public int CarryStrongest { get; init; }

    public bool KeepArmy { get; init; }

    public bool KeepArtifacts { get; init; } = true;
}

/// <summary>
/// Where a campaign stands (ADR-241): the map to play next (<see cref="Map"/>, from 0; the map count once the campaign is won), the difficulty
/// chosen when it began, the campaign variables carried so far, the heroes carried into the next map and the days each won map took.
/// </summary>
public sealed record CampaignProgress(int Map, Difficulty Difficulty)
{
    public IReadOnlyDictionary<string, int> Variables { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<CarriedHero> Heroes { get; init; } = [];

    public IReadOnlyList<int> Days { get; init; } = [];

    public bool IsWon(Campaign campaign) => Map >= campaign.Maps.Count;

    public bool Equals(CampaignProgress? other) =>
        other is not null && Map == other.Map && Difficulty == other.Difficulty && Heroes.SequenceEqual(other.Heroes) && Days.SequenceEqual(other.Days)
        && Variables.Count == other.Variables.Count && Variables.All(p => other.Variables.TryGetValue(p.Key, out var v) && v == p.Value);

    public override int GetHashCode() => HashCode.Combine(Map, Difficulty, Heroes.Count, Days.Count);
}
