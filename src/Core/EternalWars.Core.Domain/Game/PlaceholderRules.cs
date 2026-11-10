using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The map's random placeholders (ADR-126, HoMM3), drawn once when the game starts from <see cref="GameState.Random"/>, before anything else:
/// random towns first (an owned one takes its owner's chosen faction, <see cref="GameOptions.StartTowns"/>, else one faction drawn for that player
/// and shared by all its random towns; a neutral one a playable faction of its own), then random monsters (a creature of the tier from the playable
/// towns' and the neutral lines), then hero placeholders (a roster hero of the owner's faction).
/// </summary>
public static class PlaceholderRules
{
    /// <summary>The creatures a random monster of <paramref name="tier"/> (0 = any) may become: growing creatures of the playable towns and neutral ones, base and upgraded, by id.</summary>
    public static IReadOnlyList<CreatureDefinition> MonsterCandidates(CreatureCatalog creatures, int tier) =>
        creatures.All
            .Where(c => c.WeeklyGrowth > 0 && (c.Town is null || BuiltInMapObjects.PlayableTowns.Contains(c.Town)) && (tier == 0 || c.Tier == tier))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

}
