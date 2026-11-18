using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The heroes: an existing owner, passable ground outside objects (an own town's entrance excepted), an army, a spell book for known spells
/// (ADR-120), and only known types, creatures and artifacts.
/// </summary>
internal sealed class HeroCheck : IMapCheck
{
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
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} has no army and cannot fight.", hero.Position);
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
