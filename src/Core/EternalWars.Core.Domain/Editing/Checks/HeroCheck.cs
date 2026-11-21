using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The heroes: an existing owner, passable ground outside objects (an own town's entrance excepted), an army, a spell book for known spells
/// (ADR-120), and only known types, creatures and artifacts.
/// </summary>
internal sealed class HeroCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Heroes;

    /// <summary>
    /// A hero without an army is given what a hired hero brings (ADR-279): the starter stack of its type (<see cref="TavernRules.StarterStack"/>),
    /// or, for a hero of no type (the new-map wizard's), a week's growth of the first creature with a growth of its owner's nearest town on its
    /// level, a random town (its faction drawn at the start) not counted; no fix when neither tells the creature, nor for an unknown type.
    /// </summary>
    private static HeroArmyFix? ArmyFor(GameState scenario, Hero hero)
    {
        if (hero.Definition is { } unknown && !scenario.HeroRoster.Contains(unknown))
        {
            return null;
        }

        var stack = hero.Definition is { } type
            ? TavernRules.StarterStack(scenario, scenario.HeroRoster.Get(type))
            : scenario.ObjectsOf<Town>()
                .Where(t => t.Owner == hero.Owner && !t.IsRandom && t.Position.Level == hero.Position.Level)
                .OrderBy(t => Distance(t.VisitPosition, hero.Position)).ThenBy(t => t.Id.Value)
                .Select(t => scenario.Creatures.BaseCreaturesOf(t.TownType).FirstOrDefault(c => c.WeeklyGrowth > 0))
                .FirstOrDefault() is { } creature ? new CreatureStack(creature.Id, creature.WeeklyGrowth) : null;
        return stack is { } given
            ? new HeroArmyFix($"Give it a week's {scenario.Creatures.Get(given.Creature).Name} ({given.Count})", hero.Id, given)
            : null;
    }

    /// <summary>How far apart two tiles of a level are, in steps (diagonals count one).</summary>
    internal static int Distance(MapPosition a, MapPosition b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        var map = scenario.Map;
        foreach (var hero in scenario.Heroes)
        {
            if (!scenario.HasPlayer(hero.Owner))
            {
                yield return new(IssueSeverity.Error, $"Hero {hero.Name} belongs to a missing player.", hero.Position);
            }

            if (!map.Contains(hero.Position) || !scenario.Terrains.IsPassable(map.GetTerrain(hero.Position)))
            {
                yield return new(IssueSeverity.Error, $"Hero {hero.Name} stands on impassable terrain.", hero.Position);
            }
            else if (scenario.ObjectCellAt(hero.Position) is { } cell && !MapValidator.IsOwnTownEntrance(cell, hero.Owner))
            {
                yield return new(IssueSeverity.Error, $"Hero {hero.Name} stands inside an object.", hero.Position);
            }

            // A placeholder without an army gets its drawn hero's starter stack when the game starts (ADR-126).
            if (hero.Army.IsEmpty && !hero.IsPlaceholder)
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} has no army and cannot fight.", hero.Position) { Fix = ArmyFor(scenario, hero) };
            }

            if (!hero.HasSpellBook && hero.Spells.Count > 0)
            {
                yield return new(IssueSeverity.Error, $"Hero {hero.Name} knows spells but has no spell book (ADR-120).", hero.Position);
            }

            if (hero.Definition is { } type && !scenario.HeroRoster.Contains(type))
            {
                yield return new(IssueSeverity.Error, $"Hero {hero.Name} is of unknown type '{type}'.", hero.Position);
            }

            foreach (var (_, stack) in hero.Army.Stacks.Where(s => !scenario.Creatures.Contains(s.Stack.Creature)))
            {
                yield return new(IssueSeverity.Error, $"Hero {hero.Name} has unknown creature '{stack.Creature}'.", hero.Position);
            }

            foreach (var artifact in hero.Artifacts.AllWorn.Select(w => w.Id).Concat(hero.Artifacts.Backpack).Where(a => !scenario.Artifacts.Contains(a)))
            {
                yield return new(IssueSeverity.Error, $"Hero {hero.Name} has unknown artifact '{artifact}'.", hero.Position);
            }
        }
    }
}
