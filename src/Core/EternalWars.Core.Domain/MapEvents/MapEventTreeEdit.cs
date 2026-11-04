namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// One step down an event tree (EVT-6): the <see cref="List"/>-th action list of the node above, and the action's <see cref="Index"/> in it. An
/// event has one list (its actions), a quest one per stage; an "if" has then and else, a question one per answer, a loop its body, a message its
/// rewards (<see cref="MapEventTreeEdit.Lists(EventAction)"/>).
/// </summary>
public readonly record struct EventStep(int List, int Index);

/// <summary>
/// Edits an event's tree by path (the editor's "Events" tab, EVT-6): get, insert, replace, remove and move an action anywhere in it. The records
/// are immutable, so every edit returns a new event; a path that leads nowhere throws <see cref="ArgumentOutOfRangeException"/>.
/// </summary>
public static class MapEventTreeEdit
{
    /// <summary>The event's action lists: a quest's stages' actions, else its own actions.</summary>
    public static IReadOnlyList<IReadOnlyList<EventAction>> Lists(MapEventDefinition definition) =>
        definition.Kind == MapEventKind.Quest ? [.. definition.Stages.Select(s => s.Actions)] : [definition.Actions];

    public static IReadOnlyList<string> ListNames(MapEventDefinition definition) =>
        definition.Kind == MapEventKind.Quest ? [.. definition.Stages.Select((_, i) => $"Stage {i + 1}")] : ["Actions"];

    /// <summary>The action lists an action holds (none for most).</summary>
    public static IReadOnlyList<IReadOnlyList<EventAction>> Lists(EventAction action) => action switch
    {
        IfAction branch => [branch.Then, branch.Else],
        RepeatAction repeat => [repeat.Actions],
        WhileAction loop => [loop.Actions],
        QuestionAction question => [.. question.Answers.Select(a => a.Actions)],
        MessageAction message => [message.Rewards],
        _ => [],
    };

    public static IReadOnlyList<string> ListNames(EventAction action) => action switch
    {
        IfAction => ["Then", "Else"],
        RepeatAction or WhileAction => ["Do"],
        QuestionAction question => [.. question.Answers.Select(a => $"Answer \"{a.Text}\"")],
        MessageAction => ["Rewards (given once it is read)"],
        _ => [],
    };

    /// <summary>The action at the path, or null when it leads nowhere (or is empty).</summary>
    public static EventAction? Get(MapEventDefinition definition, IReadOnlyList<EventStep> path)
    {
        EventAction? action = null;
        var lists = Lists(definition);
        foreach (var step in path)
        {
            if (step.List < 0 || step.List >= lists.Count || step.Index < 0 || step.Index >= lists[step.List].Count)
            {
                return null;
            }

            action = lists[step.List][step.Index];
            lists = Lists(action);
        }

        return action;
    }

    /// <summary>Puts <paramref name="action"/> into list <paramref name="list"/> of the node at <paramref name="parent"/> (empty: the event) at <paramref name="index"/>.</summary>
    public static MapEventDefinition Insert(MapEventDefinition definition, IReadOnlyList<EventStep> parent, int list, int index, EventAction action) =>
        Change(definition, parent, list, actions =>
        {
            if (index < 0 || index > actions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return [.. actions.Take(index), action, .. actions.Skip(index)];
        });

    public static MapEventDefinition Replace(MapEventDefinition definition, IReadOnlyList<EventStep> path, EventAction action) =>
        Change(definition, Parent(path), path[^1].List, actions => [.. actions.Select((a, i) => i == Checked(actions, path[^1].Index) ? action : a)]);

    public static MapEventDefinition Remove(MapEventDefinition definition, IReadOnlyList<EventStep> path) =>
        Change(definition, Parent(path), path[^1].List, actions => [.. actions.Where((_, i) => i != Checked(actions, path[^1].Index))]);

    /// <summary>Moves the action up (negative) or down its list; at the ends it stays.</summary>
    public static MapEventDefinition Move(MapEventDefinition definition, IReadOnlyList<EventStep> path, int offset) =>
        Change(definition, Parent(path), path[^1].List, actions =>
        {
            var from = Checked(actions, path[^1].Index);
            var to = Math.Clamp(from + offset, 0, actions.Count - 1);
            var moved = actions.ToList();
            var action = moved[from];
            moved.RemoveAt(from);
            moved.Insert(to, action);
            return moved;
        });

    /// <summary>The path's action after a move by <paramref name="offset"/> (clamped like <see cref="Move"/>).</summary>
    public static IReadOnlyList<EventStep> Moved(MapEventDefinition definition, IReadOnlyList<EventStep> path, int offset)
    {
        var parent = Parent(path);
        var count = (parent.Count == 0 ? Lists(definition) : Lists(Get(definition, parent)!))[path[^1].List].Count;
        return [.. parent, path[^1] with { Index = Math.Clamp(path[^1].Index + offset, 0, count - 1) }];
    }

    /// <summary>A quest's stage replaced, its actions kept unless the new stage brings its own (null removes the stage).</summary>
    public static MapEventDefinition SetStage(MapEventDefinition definition, int index, QuestStage? stage)
    {
        if (index < 0 || index >= definition.Stages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return stage is null
            ? definition with { Stages = [.. definition.Stages.Where((_, i) => i != index)] }
            : definition with { Stages = [.. definition.Stages.Select((s, i) => i == index ? stage : s)] };
    }

    /// <summary>Every action of the event (its stages' included, nested ones first) passed through <paramref name="change"/>.</summary>
    public static MapEventDefinition Rewrite(MapEventDefinition definition, Func<EventAction, EventAction> change)
    {
        IReadOnlyList<EventAction> All(IReadOnlyList<EventAction> actions) => [.. actions.Select(a => change(With(a, [.. Lists(a).Select(All)])))];
        return definition with { Actions = All(definition.Actions), Stages = [.. definition.Stages.Select(s => s with { Actions = All(s.Actions) })] };
    }

    private static IReadOnlyList<EventStep> Parent(IReadOnlyList<EventStep> path) =>
        path.Count == 0 ? throw new ArgumentOutOfRangeException(nameof(path), "The path names no action.") : [.. path.Take(path.Count - 1)];

    private static int Checked(IReadOnlyList<EventAction> actions, int index) =>
        index >= 0 && index < actions.Count ? index : throw new ArgumentOutOfRangeException(nameof(index));

    private static MapEventDefinition Change(
        MapEventDefinition definition, IReadOnlyList<EventStep> parent, int list, Func<IReadOnlyList<EventAction>, IReadOnlyList<EventAction>> change)
    {
        var lists = Lists(definition).ToList();
        var at = parent.Count == 0 ? list : parent[0].List;
        if (at < 0 || at >= lists.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(list));
        }

        lists[at] = parent.Count == 0 ? change(lists[at]) : Nested(lists[at], parent, list, change);
        return definition.Kind == MapEventKind.Quest
            ? definition with { Stages = [.. definition.Stages.Select((s, i) => s with { Actions = lists[i] })] }
            : definition with { Actions = lists[0] };
    }

    private static IReadOnlyList<EventAction> Nested(
        IReadOnlyList<EventAction> actions, IReadOnlyList<EventStep> parent, int list, Func<IReadOnlyList<EventAction>, IReadOnlyList<EventAction>> change)
    {
        var index = Checked(actions, parent[0].Index);
        var action = actions[index];
        var rest = parent.Skip(1).ToList();
        var lists = Lists(action).ToList();
        var at = rest.Count == 0 ? list : rest[0].List;
        if (at < 0 || at >= lists.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(list));
        }

        lists[at] = rest.Count == 0 ? change(lists[at]) : Nested(lists[at], rest, list, change);
        var changed = With(action, lists);
        return [.. actions.Select((a, i) => i == index ? changed : a)];
    }

    private static EventAction With(EventAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists) => action switch
    {
        IfAction branch => branch with { Then = lists[0], Else = lists[1] },
        RepeatAction repeat => repeat with { Actions = lists[0] },
        WhileAction loop => loop with { Actions = lists[0] },
        QuestionAction question => question with { Answers = [.. question.Answers.Select((a, i) => a with { Actions = lists[i] })] },
        MessageAction message => message with { Rewards = lists[0] },
        _ => action,
    };
}
