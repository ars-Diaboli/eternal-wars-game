using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// The map events of a game (ADR-115): the author's <see cref="Set"/>, and what changes while it runs (variables, enabled events, removed cells,
/// the "defeated by" records). Running events are kept by <see cref="MapEventService"/>; a replay rebuilds them from the commands.
/// </summary>
public sealed class MapEventState
{
    private readonly Dictionary<string, int> _variables = new(StringComparer.Ordinal);
    private readonly HashSet<string> _disabled = new(StringComparer.Ordinal);
    private readonly Dictionary<MapPosition, string> _cells = [];
    private readonly Dictionary<MapObjectId, PlayerId> _monstersDefeated = [];
    private readonly Dictionary<HeroId, PlayerId> _heroesDefeated = [];
    private readonly Dictionary<PlayerId, List<QuestLogEntry>> _questLogs = [];

    public MapEventSet Set { get; private set; } = MapEventSet.Empty;

    public IReadOnlyDictionary<string, int> Variables => _variables;

    /// <summary>The event cells still on the map, by position.</summary>
    public IReadOnlyDictionary<MapPosition, string> Cells => _cells;

    /// <summary>The monsters an event names, and who beat them (EVT-3 e: only those an event names are recorded).</summary>
    public IReadOnlyDictionary<MapObjectId, PlayerId> MonstersDefeated => _monstersDefeated;

    public IReadOnlyDictionary<HeroId, PlayerId> HeroesDefeated => _heroesDefeated;

    /// <summary>Monsters and heroes some condition names: their defeats are recorded.</summary>
    internal HashSet<MapObjectId> WatchedMonsters { get; } = [];

    internal HashSet<HeroId> WatchedHeroes { get; } = [];

    /// <summary>The player's quest log (EVT-5): the quests its heroes were shown, in the order they were first shown; empty for none.</summary>
    public IReadOnlyList<QuestLogEntry> QuestLog(PlayerId player) => _questLogs.TryGetValue(player, out var log) ? log : [];

    /// <summary>The text a quest log entry shows: its stage's proposal (the completed quest's last one).</summary>
    public string QuestText(QuestLogEntry entry) =>
        Set.Find(entry.Quest) is { Stages: { Count: > 0 } stages } ? stages[Math.Min(entry.Stage, stages.Count - 1)].Proposal : "";

    public bool IsEnabled(string name) => !_disabled.Contains(name);

    public int Variable(string name) => _variables.GetValueOrDefault(name);

    /// <summary>Loads the author's events (before the game starts); the variables take their starting values.</summary>
    internal void Load(MapEventSet set)
    {
        Set = set;
        _variables.Clear();
        _disabled.Clear();
        _cells.Clear();
        _questLogs.Clear();
        foreach (var (name, value) in set.Variables)
        {
            _variables[name] = value;
        }

        foreach (var definition in set.Events.Where(e => !e.Enabled))
        {
            _disabled.Add(definition.Name);
        }

        foreach (var cell in set.Cells)
        {
            _cells[cell.Position] = cell.Event;
        }

        WatchedMonsters.Clear();
        WatchedHeroes.Clear();
        foreach (var condition in set.Events.SelectMany(MapEventTree.Conditions))
        {
            switch (condition)
            {
                case MonsterDefeatedCondition monster:
                    WatchedMonsters.Add(new MapObjectId(monster.Monster));
                    break;
                case HeroDefeatedCondition hero:
                    WatchedHeroes.Add(new HeroId(hero.Hero));
                    break;
            }
        }
    }

    internal void SetVariable(string name, int value) => _variables[name] = value;

    internal void SetEnabled(string name, bool enabled)
    {
        if (enabled)
        {
            _disabled.Remove(name);
        }
        else
        {
            _disabled.Add(name);
        }
    }

    internal bool RemoveCell(MapPosition position) => _cells.Remove(position);

    /// <summary>Writes the player's entry for the quest object (a new one goes last); false when it was already so.</summary>
    internal bool SetQuestLog(PlayerId player, QuestLogEntry entry)
    {
        if (!_questLogs.TryGetValue(player, out var log))
        {
            _questLogs[player] = log = [];
        }

        var index = log.FindIndex(e => e.Site == entry.Site);
        if (index >= 0 && log[index] == entry)
        {
            return false;
        }

        if (index >= 0)
        {
            log[index] = entry;
        }
        else
        {
            log.Add(entry);
        }

        return true;
    }

    /// <summary>Removes every other player's open entry for the quest object (its stage was completed or it left); the players whose log changed.</summary>
    internal List<PlayerId> DropOpenQuestEntries(MapObjectId site, PlayerId except)
    {
        var changed = new List<PlayerId>();
        foreach (var (player, log) in _questLogs.OrderBy(l => l.Key.Value))
        {
            if (player != except && log.RemoveAll(e => e.Site == site && !e.Done) > 0)
            {
                changed.Add(player);
            }
        }

        return changed;
    }

    internal void RecordMonsterDefeat(MapObjectId monster, PlayerId by) => _monstersDefeated.TryAdd(monster, by);

    internal void RecordHeroDefeat(HeroId hero, PlayerId by) => _heroesDefeated.TryAdd(hero, by);

    /// <summary>The digest line (empty for a map without events, so older replays keep their digests).</summary>
    internal string Describe() =>
        Set.IsEmpty
            ? ""
            : $"variables {string.Join(',', _variables.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key}={v.Value}"))} "
              + $"disabled {string.Join(',', _disabled.Order(StringComparer.Ordinal))} cells {_cells.Count} "
              + $"monsters {string.Join(',', _monstersDefeated.OrderBy(m => m.Key.Value).Select(m => $"{m.Key.Value}:{m.Value.Value}"))} "
              + $"heroes {string.Join(',', _heroesDefeated.OrderBy(h => h.Key.Value).Select(h => $"{h.Key.Value}:{h.Value.Value}"))}"
              + (_questLogs.Count == 0 ? "" : $" quests {string.Join(';', _questLogs.OrderBy(l => l.Key.Value).Select(l => $"{l.Key.Value}:{string.Join(',', l.Value.Select(e => $"{e.Site.Value}/{e.Stage}{(e.Done ? "!" : "")}"))}"))}");
}

/// <summary>Walks an event tree.</summary>
public static class MapEventTree
{
    /// <summary>Every action of the list, nested ones included (branches, answers, rewards), depth first.</summary>
    public static IEnumerable<EventAction> Actions(IReadOnlyList<EventAction> actions)
    {
        foreach (var action in actions)
        {
            yield return action;
            var nested = action switch
            {
                IfAction branch => branch.Then.Concat(branch.Else),
                RepeatAction repeat => repeat.Actions,
                WhileAction loop => loop.Actions,
                QuestionAction question => question.Answers.SelectMany(a => a.Actions),
                MessageAction message => message.Rewards,
                _ => [],
            };
            foreach (var inner in Actions(nested.ToList()))
            {
                yield return inner;
            }
        }
    }

    /// <summary>Every action of the event, its quest stages' included, nested ones included.</summary>
    public static IEnumerable<EventAction> Actions(MapEventDefinition definition) =>
        Actions(definition.Actions).Concat(definition.Stages.SelectMany(s => Actions(s.Actions)));

    /// <summary>Every condition of the event, its quest stages' requirements and actions included.</summary>
    public static IEnumerable<EventCondition> Conditions(MapEventDefinition definition) =>
        Conditions(definition.Actions).Concat(definition.Stages.SelectMany(s => Flatten(s.Requirement).Concat(Conditions(s.Actions))));

    /// <summary>Every expression of the event, its quest stages' included.</summary>
    public static IEnumerable<EventExpr> Expressions(MapEventDefinition definition) =>
        Expressions(definition.Actions).Concat(definition.Stages.SelectMany(s => RequirementExpressions(s).Concat(Expressions(s.Actions))));

    /// <summary>Every expression of a quest stage's requirement (none may be random: routes test it).</summary>
    public static IEnumerable<EventExpr> RequirementExpressions(QuestStage stage) =>
        Flatten(stage.Requirement).OfType<CompareCondition>().SelectMany(c => new[] { c.Left, c.Right }).SelectMany(Flatten);

    /// <summary>Every condition of the actions, nested ones included.</summary>
    public static IEnumerable<EventCondition> Conditions(IReadOnlyList<EventAction> actions) =>
        Actions(actions).SelectMany(a => a switch
        {
            IfAction branch => Flatten(branch.Condition),
            WhileAction loop => Flatten(loop.Condition),
            _ => [],
        });

    /// <summary>Every expression of the actions and their conditions, nested ones included.</summary>
    public static IEnumerable<EventExpr> Expressions(IReadOnlyList<EventAction> actions) =>
        Actions(actions).SelectMany(a => a switch
            {
                SetVariableAction set => [set.Value],
                ResourceAction resource => [resource.Amount],
                ExperienceAction experience => [experience.Amount],
                PrimaryStatAction stat => [stat.Amount],
                ManaAction mana => [mana.Amount],
                MovementAction movement => [movement.Amount],
                CreaturesAction creatures => [creatures.Count],
                AvailableCreaturesAction available => [available.Count],
                RepeatAction repeat => [repeat.Times],
                _ => Array.Empty<EventExpr>(),
            })
            .Concat(Conditions(actions).OfType<CompareCondition>().SelectMany(c => new[] { c.Left, c.Right }))
            .SelectMany(Flatten);

    private static IEnumerable<EventCondition> Flatten(EventCondition condition) => condition switch
    {
        AllCondition all => all.Conditions.SelectMany(Flatten).Prepend(condition),
        AnyCondition any => any.Conditions.SelectMany(Flatten).Prepend(condition),
        NotCondition not => Flatten(not.Condition).Prepend(condition),
        _ => [condition],
    };

    private static IEnumerable<EventExpr> Flatten(EventExpr expr) =>
        expr is ArithmeticExpr arithmetic ? Flatten(arithmetic.Left).Concat(Flatten(arithmetic.Right)).Prepend(expr) : [expr];
}
