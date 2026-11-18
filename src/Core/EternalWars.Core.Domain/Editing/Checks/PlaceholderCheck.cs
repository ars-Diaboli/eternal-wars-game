using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>Random monsters, towns and hero placeholders (ADR-126): a monster's tier must have creatures to draw.</summary>
internal sealed class PlaceholderCheck : IMapCheck
{
    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        foreach (var monster in scenario.ObjectsOf<MonsterGuard>().Where(m => m.RandomTier is not null))
        {
            if (monster.RandomTier is < 0 or > Creatures.CreatureDefinition.MaxTier)
            {
                yield return new(IssueSeverity.Error, $"A random monster has the tier {monster.RandomTier}; tiers go from 1 to {Creatures.CreatureDefinition.MaxTier} (0 = any).", monster.Position);
            }
            else if (PlaceholderRules.MonsterCandidates(scenario.Creatures, monster.RandomTier!.Value).Count == 0)
            {
                yield return new(IssueSeverity.Warning, $"No creature of tier {monster.RandomTier} can be drawn for the random monster; it stays {monster.Creature}.", monster.Position);
            }
        }
    }
}
