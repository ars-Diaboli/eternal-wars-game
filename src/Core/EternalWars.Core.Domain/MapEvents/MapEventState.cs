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

    public bool IsEnabled(string name) => !_disabled.Contains(name);

    public int Variable(string name) => _variables.GetValueOrDefault(name);

    /// <summary>Loads the author's events (before the game starts); the variables take their starting values.</summary>
    internal void Load(MapEventSet set)
    {
        Set = set;
        _variables.Clear();
        _disabled.Clear();
        _cells.Clear();
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
        foreach (var condition in set.Events.SelectMany(e => MapEventTree.Conditions(e.Actions)))
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

    internal void RecordMonsterDefeat(MapObjectId monster, PlayerId by) => _monstersDefeated.TryAdd(monster, by);

    internal void RecordHeroDefeat(HeroId hero, PlayerId by) => _heroesDefeated.TryAdd(hero, by);

    /// <summary>The digest line (empty for a map without events, so older replays keep their digests).</summary>
    internal string Describe() =>
        Set.IsEmpty
            ? ""
            : $"variables {string.Join(',', _variables.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key}={v.Value}"))} "
              + $"disabled {string.Join(',', _disabled.Order(StringComparer.Ordinal))} cells {_cells.Count} "
              + $"monsters {string.Join(',', _monstersDefeated.OrderBy(m => m.Key.Value).Select(m => $"{m.Key.Value}:{m.Value.Value}"))} "
              + $"heroes {string.Join(',', _heroesDefeated.OrderBy(h => h.Key.Value).Select(h => $"{h.Key.Value}:{h.Value.Value}"))}";
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

    /// <summary>Every condition of the actions, nested ones included.</summary>
    public static IEnumerable<EventCondition> Conditions(IReadOnlyList<EventAction> actions) =>
        Actions(actions).OfType<IfAction>().SelectMany(a => Flatten(a.Condition));

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
