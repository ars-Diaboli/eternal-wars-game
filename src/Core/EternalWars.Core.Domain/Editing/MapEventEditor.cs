using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// Edits the map's events (EVT-6): the event list, map and campaign variables and hero event cells. Every change replaces the scenario's
/// <see cref="MapEventSet"/> and publishes <see cref="MapEventsChanged"/>.
/// </summary>
public sealed class MapEventEditor
{
    private readonly EditorContext _context;

    internal MapEventEditor(EditorContext context) => _context = context;

    private Game.GameState Scenario => _context.Scenario;

    /// <summary>A name no event of the map has: <paramref name="stem"/>, else "stem 2", "stem 3", ….</summary>
    public string UnusedName(string stem)
    {
        stem = string.IsNullOrWhiteSpace(stem) ? "event" : stem.Trim();
        var name = stem;
        for (var i = 2; Scenario.MapEvents.Set.Find(name) is not null; i++)
        {
            name = $"{stem} {i}";
        }

        return name;
    }

    /// <summary>Adds an event at the end of the list (events run in list order). Refused when its name is empty or taken.</summary>
    public EditResult Add(MapEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var set = Scenario.MapEvents.Set;
        if (string.IsNullOrWhiteSpace(definition.Name) || set.Find(definition.Name) is not null)
        {
            return EditResult.InvalidValue;
        }

        ChangeEvents($"Add event {definition.Name}", set with { Events = [.. set.Events, definition] });
        return EditResult.Ok;
    }

    /// <summary>
    /// Replaces the event <paramref name="name"/> (its tree, kind, towns, stages or name). A new name must be free; the cells, Pandora's boxes,
    /// quest objects and events that named the old one name the new one.
    /// </summary>
    public EditResult Replace(string name, MapEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var set = Scenario.MapEvents.Set;
        if (set.Find(name) is null)
        {
            return EditResult.InvalidTarget;
        }

        var renamed = definition.Name != name;
        if (string.IsNullOrWhiteSpace(definition.Name) || (renamed && set.Find(definition.Name) is not null))
        {
            return EditResult.InvalidValue;
        }

        var events = set.Events.Select(e => e.Name == name ? definition : e);
        if (!renamed)
        {
            ChangeEvents($"Edit event {name}", set with { Events = [.. events] });
            return EditResult.Ok;
        }

        var to = definition.Name;
        EventAction Rename(EventAction action) => action switch
        {
            RunEventAction run when run.Event == name => run with { Event = to },
            EnableEventAction enable when enable.Event == name => enable with { Event = to },
            _ => action,
        };
        var next = set with
        {
            Events = [.. events.Select(e => MapEventTreeEdit.Rewrite(e, Rename))],
            Cells = [.. set.Cells.Select(c => c.Event == name ? c with { Event = to } : c)],
        };
        ChangeEvents($"Rename event {name} to {to}", next, [.. References(name).Select(o => (o, (string?)to))]);
        return EditResult.Ok;
    }

    /// <summary>
    /// Removes the event and its cells; the Pandora's boxes and quest objects that named it name none. Events that still run or enable it are the
    /// validator's errors.
    /// </summary>
    public EditResult Remove(string name)
    {
        var set = Scenario.MapEvents.Set;
        if (set.Find(name) is null)
        {
            return EditResult.InvalidTarget;
        }

        var next = set with { Events = [.. set.Events.Where(e => e.Name != name)], Cells = [.. set.Cells.Where(c => c.Event != name)] };
        ChangeEvents($"Remove event {name}", next, [.. References(name).Select(o => (o, (string?)null))]);
        return EditResult.Ok;
    }

    /// <summary>Moves the event up (negative) or down the list, which is the order events of one moment run in (EVT-2 e).</summary>
    public EditResult Move(string name, int offset)
    {
        var set = Scenario.MapEvents.Set;
        var from = set.Events.ToList().FindIndex(e => e.Name == name);
        if (from < 0)
        {
            return EditResult.InvalidTarget;
        }

        var to = Math.Clamp(from + offset, 0, set.Events.Count - 1);
        if (to != from)
        {
            var events = set.Events.ToList();
            var moved = events[from];
            events.RemoveAt(from);
            events.Insert(to, moved);
            ChangeEvents($"Move event {name}", set with { Events = events });
        }

        return EditResult.Ok;
    }

    /// <summary>Declares a map variable, or changes its starting value (EVT-3 c). Refused for an empty name.</summary>
    public EditResult SetVariable(string name, int value)
    {
        var set = Scenario.MapEvents.Set;
        if (string.IsNullOrWhiteSpace(name))
        {
            return EditResult.InvalidValue;
        }

        name = name.Trim();
        if (set.Variables.TryGetValue(name, out var old) && old == value)
        {
            return EditResult.Ok;
        }

        var variables = new Dictionary<string, int>(set.Variables, StringComparer.Ordinal) { [name] = value };
        ChangeEvents($"Set variable {name}", set with { Variables = variables });
        return EditResult.Ok;
    }

    /// <summary>Removes a map variable; events that still use it are the validator's errors.</summary>
    public EditResult RemoveVariable(string name)
    {
        var set = Scenario.MapEvents.Set;
        if (!set.Variables.ContainsKey(name))
        {
            return EditResult.InvalidTarget;
        }

        ChangeEvents($"Remove variable {name}", set with
        {
            Variables = set.Variables.Where(v => v.Key != name).ToDictionary(StringComparer.Ordinal),
            KeptVariables = [.. set.KeptVariables.Where(v => v != name)],
            TakenVariables = [.. set.TakenVariables.Where(v => v != name)],
        });
        return EditResult.Ok;
    }

    /// <summary>
    /// Marks a map variable as a campaign variable (EVT-1 f, FLOW-1): <paramref name="kept"/> "kept for the next map", <paramref name="taken"/>
    /// "taken from the last map" (both, or neither for an ordinary variable). Refused for a variable that is not declared.
    /// </summary>
    public EditResult SetCampaignVariable(string name, bool kept, bool taken)
    {
        var set = Scenario.MapEvents.Set;
        if (!set.Variables.ContainsKey(name))
        {
            return EditResult.InvalidTarget;
        }

        if (set.IsKept(name) == kept && set.IsTaken(name) == taken)
        {
            return EditResult.Ok;
        }

        static IReadOnlyList<string> Mark(IReadOnlyList<string> names, string name, bool on) =>
            [.. names.Where(v => v != name).Concat(on ? [name] : []).Order(StringComparer.Ordinal)];
        ChangeEvents($"Campaign variable {name}", set with { KeptVariables = Mark(set.KeptVariables, name, kept), TakenVariables = Mark(set.TakenVariables, name, taken) });
        return EditResult.Ok;
    }

    /// <summary>
    /// Puts an invisible cell of the hero event <paramref name="event"/> at the position (EVT-2), replacing any cell there, or removes the cell there
    /// (null). Refused off the map, on impassable ground or under an object, and for a name that is not a hero event.
    /// </summary>
    public EditResult SetCell(MapPosition position, string? @event)
    {
        var set = Scenario.MapEvents.Set;
        var old = set.Cells.FirstOrDefault(c => c.Position == position);
        if (@event is null)
        {
            if (old is null)
            {
                return EditResult.InvalidTarget;
            }

            ChangeEvents("Remove event cell", set with { Cells = [.. set.Cells.Where(c => c != old)] });
            return EditResult.Ok;
        }

        if (!Scenario.Map.Contains(position))
        {
            return EditResult.OutOfBounds;
        }

        if (!Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(position)))
        {
            return EditResult.ImpassableTerrain;
        }

        if (Scenario.ObjectCellAt(position) is not null)
        {
            return EditResult.Overlaps;
        }

        if (set.Find(@event) is not { Kind: MapEventKind.Hero })
        {
            return EditResult.InvalidTarget;
        }

        if (old?.Event == @event)
        {
            return EditResult.Ok;
        }

        var cell = new EventCell(position.X, position.Y, position.Level, @event);
        ChangeEvents($"Place event cell of {@event}", set with { Cells = [.. set.Cells.Where(c => c != old), cell] });
        return EditResult.Ok;
    }

    /// <summary>The Pandora's boxes and quest objects that name the event.</summary>
    private IEnumerable<MapObject> References(string name) =>
        Scenario.Objects.Where(o => o is PandorasBox { Event: var e } && e == name || o is QuestSite { Quest: var q } && q == name);

    /// <summary>One undoable change of the map events, with the event names some objects get (a rename or removal).</summary>
    private void ChangeEvents(string description, MapEventSet next, IReadOnlyList<(MapObject Object, string? Name)>? names = null)
    {
        var previous = Scenario.MapEvents.Set;
        var after = names ?? [];
        var before = after.Select(n => (n.Object, Name: n.Object is PandorasBox box ? box.Event : ((QuestSite)n.Object).Quest)).ToList();
        _context.Run(description, () => Apply(next, after), () => Apply(previous, before));

        void Apply(MapEventSet set, IReadOnlyList<(MapObject Object, string? Name)> objects)
        {
            foreach (var (mapObject, name) in objects)
            {
                switch (mapObject)
                {
                    case PandorasBox box:
                        box.Event = name;
                        break;
                    case QuestSite site:
                        site.Quest = name;
                        break;
                }

                _context.Publish(new MapObjectChanged(mapObject.Id));
            }

            Scenario.SetMapEvents(set, check: false);
            _context.Publish(new MapEventsChanged());
        }
    }
}
