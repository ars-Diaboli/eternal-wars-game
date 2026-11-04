using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// The editor's warnings about map events (EVT-6 c): events that run themselves, events and variables nothing uses, an event that starts disabled
/// and that nothing enables, and quests that cannot be finished, where it can tell. The errors that stop a map loading are
/// <see cref="MapEventRules"/>'.
/// </summary>
public static class MapEventChecks
{
    public static IReadOnlyList<MapEventProblem> Warnings(GameState state, MapEventSet set)
    {
        var warnings = new List<MapEventProblem>();
        var actions = set.Events.SelectMany(MapEventTree.Actions).ToList();
        var run = actions.OfType<RunEventAction>().Select(a => a.Event).ToHashSet(StringComparer.Ordinal);
        var enabled = actions.OfType<EnableEventAction>().Where(a => a.Enabled && a.Event is not null).Select(a => a.Event!).ToHashSet(StringComparer.Ordinal);
        foreach (var definition in set.Events)
        {
            var name = definition.Name;
            if (Cycle(set, name) is { } cycle)
            {
                warnings.Add(new($"Event '{name}' runs itself ({string.Join(" → ", cycle)}); runs deeper than {MapEventService.MaxRunDepth} are skipped.", name));
            }

            var unused = definition.Kind switch
            {
                MapEventKind.Hero when !run.Contains(name) && !set.Cells.Any(c => c.Event == name) && !state.ObjectsOf<PandorasBox>().Any(b => b.Event == name) =>
                    "is never run: no event cell, Pandora's box or other event runs it.",
                MapEventKind.Quest when !state.ObjectsOf<QuestSite>().Any(q => q.Quest == name) => "is not the quest of any Seer's Hut, Quest Guard or Quest Gate.",
                MapEventKind.Town when definition.Towns.Count == 0 && !run.Contains(name) => "names no town, so it never runs.",
                _ => null,
            };
            if (unused is not null)
            {
                warnings.Add(new($"Event '{name}' {unused}", name));
            }

            if (!definition.Enabled && !enabled.Contains(name))
            {
                warnings.Add(new($"Event '{name}' starts disabled and no event enables it.", name));
            }

            if (MapEventTree.Actions(definition).OfType<MessageAction>().Any(m => string.IsNullOrWhiteSpace(m.Text)))
            {
                warnings.Add(new($"Event '{name}' shows a message without text.", name));
            }

            for (var i = 0; i < definition.Stages.Count; i++)
            {
                if (definition.Kind == MapEventKind.Quest && Impossible(state, definition.Stages[i].Requirement) is { } reason)
                {
                    warnings.Add(new($"Quest '{name}', stage {i + 1} cannot be met: {reason}", name));
                }
            }
        }

        var read = set.Events.SelectMany(MapEventTree.Expressions).OfType<VariableExpr>().Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var variable in set.Variables.Keys.Where(v => !read.Contains(v)).Order(StringComparer.Ordinal))
        {
            warnings.Add(new($"The variable '{variable}' is never read."));
        }

        return warnings;
    }

    /// <summary>The way an event runs itself through "run another event" (its name first and last), or null when it does not.</summary>
    public static IReadOnlyList<string>? Cycle(MapEventSet set, string name)
    {
        var path = new List<string> { name };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return Visit(name) ? path : null;

        bool Visit(string current)
        {
            foreach (var next in Runs(set, current))
            {
                path.Add(next);
                if (next == name || (seen.Add(next) && Visit(next)))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }
    }

    private static IEnumerable<string> Runs(MapEventSet set, string name) =>
        set.Find(name) is { } definition
            ? MapEventTree.Actions(definition).OfType<RunEventAction>().Select(r => r.Event).Where(e => set.Find(e) is { Kind: not MapEventKind.Quest }).Distinct()
            : [];

    /// <summary>Why the requirement can never be met on this map, when that can be told (things that do not exist, a level past the last).</summary>
    public static string? Impossible(GameState state, EventCondition condition) => condition switch
    {
        AllCondition all => all.Conditions.Select(c => Impossible(state, c)).FirstOrDefault(r => r is not null),
        AnyCondition any when any.Conditions.Count > 0 && any.Conditions.Select(c => Impossible(state, c)).ToList() is var reasons && reasons.All(r => r is not null) =>
            string.Join(" ", reasons),
        HeroIsCondition hero when !state.HeroRoster.Contains(hero.Hero) => $"there is no hero '{hero.Hero}'.",
        PlayerIsCondition player when player.Player < 0 || player.Player >= state.Players.Count => $"the map has no player {player.Player + 1}.",
        MonsterDefeatedCondition monster when state.FindObject(new MapObjectId(monster.Monster)) is not MonsterGuard => $"object {monster.Monster} is not a monster.",
        HeroDefeatedCondition beaten when !state.Heroes.Any(h => h.Id.Value == beaten.Hero) => $"there is no hero {beaten.Hero} on the map.",
        TownOwnerCondition owner when state.FindObject(new MapObjectId(owner.Town)) is not Town => $"object {owner.Town} is not a town.",
        CompareCondition { Left: QuantityExpr quantity, Op: CompareOp.GreaterOrEqual or CompareOp.Greater or CompareOp.Equal, Right: NumberExpr { Value: > 0 } number } =>
            Unknown(state, quantity) ?? (quantity.Quantity == Quantity.HeroLevel && number.Value > ExperienceTable.LevelFor(int.MaxValue)
                ? $"heroes stop at level {ExperienceTable.LevelFor(int.MaxValue)}."
                : null),
        _ => null,
    };

    /// <summary>A quantity of something that does not exist (which no hero or player ever has).</summary>
    private static string? Unknown(GameState state, QuantityExpr quantity) => quantity.Quantity switch
    {
        Quantity.Artifacts when quantity.Id is not { } id || !state.Artifacts.Contains(id) => $"there is no artifact '{quantity.Id}'.",
        Quantity.Creatures when quantity.Id is not { } id || !state.Creatures.Contains(id) => $"there is no creature '{quantity.Id}'.",
        Quantity.Resource when !Enum.TryParse<ResourceType>(quantity.Id, ignoreCase: true, out _) => $"there is no resource '{quantity.Id}'.",
        Quantity.SkillLevel when quantity.Id is not { } id || !BuiltInSkills.Contains(id) => $"there is no skill '{quantity.Id}'.",
        _ => null,
    };
}
