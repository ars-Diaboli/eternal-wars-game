using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>What makes a map's events and quest objects unusable (ADR-115): checked when a map is loaded; the editor's validator will add warnings (EVT-6 c).</summary>
public static class MapEventRules
{
    /// <summary>The events' errors against the map they belong to; empty when they can run.</summary>
    public static IReadOnlyList<string> Problems(GameState state, MapEventSet set)
    {
        var problems = new List<string>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in set.Events)
        {
            if (string.IsNullOrWhiteSpace(definition.Name) || !names.Add(definition.Name))
            {
                problems.Add($"Event '{definition.Name}' needs a name of its own.");
            }

            if (definition.Kind != MapEventKind.Town && definition.Towns.Count > 0)
            {
                problems.Add($"Only a town event names towns ('{definition.Name}').");
            }

            foreach (var town in definition.Towns.Where(t => state.FindObject(new Common.MapObjectId(t)) is not Town))
            {
                problems.Add($"Event '{definition.Name}' names object {town}, which is not a town.");
            }

            problems.AddRange(Check(state, set, definition).Select(p => $"Event '{definition.Name}': {p}"));
        }

        var quests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var site in state.ObjectsOf<QuestSite>().Where(s => s.Quest is not null).OrderBy(s => s.Id.Value))
        {
            var at = $"The {site.Definition.Name.ToLowerInvariant()} {site.Id.Value}";
            if (set.Find(site.Quest!) is not { Kind: MapEventKind.Quest } quest)
            {
                problems.Add($"{at} names '{site.Quest}', which is not a quest.");
                continue;
            }

            if (!quests.Add(quest.Name))
            {
                problems.Add($"{at} shares the quest '{quest.Name}'; every quest object has its own.");
            }

            if (site.Kind == MapObjectKind.QuestGate && (quest.Stages.Count != 1 || quest.Stages[0].Actions.Count > 0))
            {
                problems.Add($"{at}: a gate's quest has one stage that takes and gives nothing.");
            }
        }

        var cells = new HashSet<Common.MapPosition>();
        foreach (var cell in set.Cells)
        {
            var at = $"The event cell at {cell.Position}";
            if (set.Find(cell.Event) is not { Kind: MapEventKind.Hero })
            {
                problems.Add($"{at} names '{cell.Event}', which is not a hero event.");
            }

            if (!cells.Add(cell.Position))
            {
                problems.Add($"{at} is there twice.");
            }

            if (!state.Map.Contains(cell.Position) || !state.Terrains.IsPassable(state.Map.GetTerrain(cell.Position)) || state.ObjectCellAt(cell.Position) is not null)
            {
                problems.Add($"{at} must be on free, passable ground.");
            }
        }

        return problems;
    }

    private static IEnumerable<string> Check(GameState state, MapEventSet set, MapEventDefinition definition)
    {
        if (definition.Kind == MapEventKind.Quest)
        {
            if (definition.Actions.Count > 0 || definition.Stages.Count == 0)
            {
                yield return "a quest has stages and no actions of its own.";
            }

            if (definition.Stages.Any(s => string.IsNullOrWhiteSpace(s.Proposal)))
            {
                yield return "every quest stage tells what it asks (its proposal).";
            }

            if (definition.Stages.SelectMany(MapEventTree.RequirementExpressions).OfType<RandomExpr>().Any())
            {
                yield return "a quest's requirement draws no random numbers.";
            }
        }
        else if (definition.Stages.Count > 0)
        {
            yield return "only a quest has stages.";
        }

        foreach (var name in MapEventTree.Expressions(definition).OfType<VariableExpr>().Select(v => v.Name)
                     .Concat(MapEventTree.Actions(definition).OfType<SetVariableAction>().Select(a => a.Variable))
                     .Where(n => !set.Variables.ContainsKey(n)).Distinct())
        {
            yield return $"the variable '{name}' is not declared.";
        }

        foreach (var random in MapEventTree.Expressions(definition).OfType<RandomExpr>().Where(r => r.Min > r.Max))
        {
            yield return $"a random number from {random.Min} to {random.Max}.";
        }

        foreach (var action in MapEventTree.Actions(definition))
        {
            if (Problem(state, set, definition, action) is { } problem)
            {
                yield return problem;
            }
        }
    }

    private static string? Problem(GameState state, MapEventSet set, MapEventDefinition definition, EventAction action) => action switch
    {
        QuestionAction { Answers.Count: < 2 or > 3 } => "a question needs two or three answers.",
        EnableEventAction { Event: { } other } when set.Find(other) is null => $"there is no event '{other}'.",
        RemoveCellAction when definition.Kind != MapEventKind.Hero => "only a hero event has a cell to remove.",
        SkillAction skill when !BuiltInSkills.Contains(skill.Skill) => $"unknown skill '{skill.Skill}'.",
        SkillAction { Level: SkillLevel.None } => "a skill is given at Basic or higher.",
        ArtifactAction artifact when !state.Artifacts.TryGet(artifact.Artifact, out _) => $"unknown artifact '{artifact.Artifact}'.",
        SpellAction spell when !state.Spells.Contains(spell.Spell) => $"unknown spell '{spell.Spell}'.",
        CreaturesAction creatures when !state.Creatures.Contains(creatures.Creature) => $"unknown creature '{creatures.Creature}'.",
        AvailableCreaturesAction available when !state.Creatures.Contains(available.Line) => $"unknown creature '{available.Line}'.",
        GiveHeroAction give when !state.HeroRoster.Contains(give.Hero) => $"unknown hero '{give.Hero}'.",
        MoraleAction morale when Math.Abs(morale.Morale) > MapEventService.MaxMoraleOrLuck || Math.Abs(morale.Luck) > MapEventService.MaxMoraleOrLuck =>
            $"morale and luck go from −{MapEventService.MaxMoraleOrLuck} to +{MapEventService.MaxMoraleOrLuck}.",
        BattleAction battle when battle.Army.Count is < 1 or > Army.SlotCount || battle.Army.Any(s => s.Count < 1 || !state.Creatures.Contains(s.Creature)) =>
            $"a battle needs one to {Army.SlotCount} stacks of known creatures.",
        BattleAction when definition.Kind is not (MapEventKind.Hero or MapEventKind.Quest) => "only a hero event or a quest fights.",
        _ => null,
    };
}
