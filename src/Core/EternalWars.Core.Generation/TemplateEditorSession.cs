using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Generation;

/// <summary>
/// The template in a <see cref="TemplateEditorSession"/> changed; <see cref="Issues"/> are the validator's findings on the new one.
/// <paramref name="Replaced"/>: the whole template was put in place (opened, undone, redone), so views show every part of it again; otherwise
/// one operation changed it, made through the view that shows the change already.
/// </summary>
[SeenBy(Audience.Nobody)]
public sealed record TemplateChanged(MapTemplate Template, IReadOnlyList<TemplateIssue> Issues, bool Replaced = false) : IEvent;

/// <summary>
/// The template editor (ADR-070 RMG-10, ADR-117), engine-free like <see cref="MapEditorSession"/>: it holds one <see cref="MapTemplate"/> and
/// changes it through the operations below, each replacing the immutable template and publishing <see cref="TemplateChanged"/> with the
/// validator's issues, so the views show the zone graph and the live validation. Operations that cannot apply (an unknown zone, an id in use)
/// return false and change nothing; one that would leave the template as it is records nothing either.
/// Every change is one named undo step (EDIT-2, ADR-250) that keeps the template before it (templates are values): <see cref="Undo"/>,
/// <see cref="Redo"/> and <see cref="UndoTo"/> put a kept template back, and <see cref="History"/> lists the steps and remembers the step the
/// template was saved at (<see cref="IsDirty"/>). Changes made between <see cref="BeginStep"/> and <see cref="EndStep"/> (a drag) are one step
/// (ADR-268).
/// <list type="bullet">
/// <item>Zones: <see cref="AddZone"/> (a new id, a start zone takes the next free player slot), <see cref="UpdateZone"/>, <see cref="RenameZone"/>
/// (connections and mirrors follow), <see cref="MoveZone"/> (its place in the graph), <see cref="RemoveZone"/> (with its connections).</item>
/// <item>Connections: <see cref="Connect"/>, <see cref="UpdateConnection"/>, <see cref="Disconnect"/>.</item>
/// <item>The template's own settings: <see cref="UpdateTemplate"/> (zones and connections are kept).</item>
/// </list>
/// The player count follows the start zones (<see cref="SyncPlayers"/>), so a template stays valid while start zones are added and removed.
/// </summary>
public sealed class TemplateEditorSession : IUndoSession
{
    private readonly IEventBus _bus;
    private readonly ContentCatalogs? _content;

    /// <summary>The step <see cref="BeginStep"/> opened: its name and the template before it; null when none is open.</summary>
    private (string Description, MapTemplate Before)? _step;

    /// <summary>What <see cref="UnsavedWorkChanged"/> said last.</summary>
    private bool _dirty;

    /// <summary>A session on the template (a blank one when left out), checked against the game's tables (terrains, start buildings; the built-in ones when <paramref name="content"/> is left out).</summary>
    public TemplateEditorSession(MapTemplate? template = null, IEventBus? bus = null, ContentCatalogs? content = null)
    {
        _bus = bus ?? new EventBus();
        _content = content;
        Template = template ?? Blank();
    }

    public IEventSubscriber Events => _bus;

    public MapTemplate Template { get; private set; }

    public IReadOnlyList<TemplateIssue> Issues => TemplateValidator.Validate(Template, _content);

    /// <summary>The undo steps (their names, how many are applied) and the step the template was saved at.</summary>
    public UndoHistory History { get; } = new();

    /// <summary>The template differs from the one last saved or opened (<see cref="MarkSaved"/>).</summary>
    public bool IsDirty => History.IsDirty;

    /// <summary>A step of <see cref="BeginStep"/> is open (a zone is being dragged).</summary>
    public bool IsStepOpen => _step is not null;

    /// <summary>A new template: two start zones joined through a junction, the smallest thing that validates.</summary>
    public static MapTemplate Blank() => new()
    {
        Name = "New template",
        Sizes = [72, 96, 128],
        Zones =
        [
            StartZone("start-1", 0) with { GraphX = 0, GraphY = 0 },
            StartZone("start-2", 1) with { GraphX = 4, GraphY = 0 },
            new TemplateZone { Id = "middle", Type = ZoneType.Junction, Size = 2, Monsters = MonsterStrength.Average, GraphX = 2, GraphY = 0 },
        ],
        Connections =
        [
            new TemplateConnection { From = "start-1", To = "middle", GuardValue = 1500 },
            new TemplateConnection { From = "start-2", To = "middle", GuardValue = 1500 },
        ],
    };

    private static TemplateZone StartZone(string id, int owner) => new()
    {
        Id = id, Type = ZoneType.PlayerStart, Owner = owner, Size = 3, PlayerTowns = CountRange.Exactly(1), Monsters = MonsterStrength.None,
        Mines = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 1, [ResourceType.Lumber] = 1, [ResourceType.Ore] = 1 },
        ExtraPiles = 3, Treasure = [new TreasureBand(500, 1500, 4)],
    };

    /// <summary>
    /// Replaces the whole template (a file opened, "New"): the history starts anew and the template counts as saved. With
    /// <paramref name="arrange"/>, zones without a graph place get one (<see cref="ArrangeGraph"/>) as part of opening, not as a step.
    /// </summary>
    public void Load(MapTemplate template, bool arrange = false)
    {
        ArgumentNullException.ThrowIfNull(template);
        _step = null; // a drag still open belongs to the template that goes
        Set(arrange ? Arranged(template, all: false) : template, replaced: true);
        History.Clear();
        History.MarkSaved();
        PublishHistory();
    }

    /// <summary>The template as it is now was saved: it has no unsaved work until the history moves.</summary>
    public void MarkSaved()
    {
        History.MarkSaved();
        PublishUnsaved();
    }

    /// <summary>The template as it is now is in no file (a restored autosave): it has unsaved work until it is saved.</summary>
    public void MarkUnsaved()
    {
        History.MarkUnsaved();
        PublishUnsaved();
    }

    public bool Undo() => ChangeHistory(History.Undo);

    public bool Redo() => ChangeHistory(History.Redo);

    /// <summary>Undoes or redoes until <paramref name="step"/> steps are applied (0: everything undone); false when it is there already.</summary>
    public bool UndoTo(int step) => ChangeHistory(() => History.MoveTo(step));

    /// <summary>
    /// Opens one step named <paramref name="description"/>: the changes until <see cref="EndStep"/> show as they are made but are undone
    /// together (a zone dragged in the graph). Steps do not nest. An undo, a redo or a load ends the open step first (ADR-268).
    /// </summary>
    public void BeginStep(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (_step is not null)
        {
            throw new InvalidOperationException($"The step '{_step.Value.Description}' is still open.");
        }

        _step = (description, Template);
    }

    /// <summary>Closes the step <see cref="BeginStep"/> opened: one undo step when anything changed, else none. None open: nothing happens.</summary>
    public void EndStep()
    {
        if (_step is not { } step)
        {
            return;
        }

        _step = null;
        if (!Same(step.Before, Template))
        {
            Record(step.Description, step.Before);
        }
        else if (!ReferenceEquals(step.Before, Template))
        {
            Set(step.Before, replaced: false); // a drag that came back where it started: the template as it was, no step
        }
    }

    /// <summary>Whether two templates hold the same values: their own settings, and their zones and connections item by item.</summary>
    private static bool Same(MapTemplate a, MapTemplate b) =>
        ReferenceEquals(a, b)
        || ((a with { Zones = b.Zones, Connections = b.Connections }) == b && a.Zones.SequenceEqual(b.Zones) && a.Connections.SequenceEqual(b.Connections));

    /// <summary>Changes the template's own settings (name, players, sizes, teams, symmetry, …); its zones and connections are kept.</summary>
    public void UpdateTemplate(Func<MapTemplate, MapTemplate> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var changed = change(Template) with { Zones = Template.Zones, Connections = Template.Connections };
        if (changed != Template)
        {
            Change("Change the template's settings", changed);
        }
    }

    /// <summary>
    /// Adds a zone of the type at a place in the graph and returns its id (<c>start-N</c>, <c>treasure-N</c>, <c>junction-N</c>); a start zone takes the
    /// lowest free player slot (none above 7: refused, null).
    /// </summary>
    public string? AddZone(ZoneType type, double x, double y)
    {
        var prefix = type switch { ZoneType.PlayerStart or ZoneType.AiStart => "start", ZoneType.Treasure => "treasure", _ => "junction" };
        var id = Enumerable.Range(1, 999).Select(n => $"{prefix}-{n}").First(candidate => Template.Zone(candidate) is null);
        TemplateZone zone;
        if (type is ZoneType.PlayerStart or ZoneType.AiStart)
        {
            var used = Template.Zones.Select(z => z.Owner).OfType<int>().ToHashSet();
            if (Enumerable.Range(0, Domain.Game.GameState.MaxPlayers).Where(s => !used.Contains(s)).Select(s => (int?)s).FirstOrDefault() is not { } slot)
            {
                return null;
            }

            zone = StartZone(id, slot) with { Type = type };
        }
        else
        {
            zone = new TemplateZone
            {
                Id = id, Type = type, Size = 2, Monsters = type == ZoneType.Junction ? MonsterStrength.Strong : MonsterStrength.Average,
                Treasure = type == ZoneType.Treasure ? [new TreasureBand(1500, 4000, 4)] : [], ExtraPiles = 2,
            };
        }

        ChangeSynced($"Add zone {id}", Template with { Zones = [.. Template.Zones, zone with { GraphX = x, GraphY = y }] });
        return id;
    }

    /// <summary>Replaces the zone of the same id (its graph place is kept unless the new one has one).</summary>
    public bool UpdateZone(TemplateZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return ReplaceZone(zone, $"Change zone {zone.Id}");
    }

    private bool ReplaceZone(TemplateZone zone, string description)
    {
        if (Template.Zone(zone.Id) is not { } old)
        {
            return false;
        }

        var placed = zone with { GraphX = zone.GraphX ?? old.GraphX, GraphY = zone.GraphY ?? old.GraphY };
        if (placed != old)
        {
            ChangeSynced(description, Template with { Zones = [.. Template.Zones.Select(z => z.Id == zone.Id ? placed : z)] });
        }

        return true;
    }

    /// <summary>Gives a zone a new id; its connections and the mirrors that name it follow. Refused for an empty or used id.</summary>
    public bool RenameZone(string id, string newId)
    {
        newId = newId?.Trim() ?? "";
        if (Template.Zone(id) is null || newId.Length == 0 || (newId != id && Template.Zone(newId) is not null))
        {
            return false;
        }

        if (newId == id)
        {
            return true;
        }

        string Rename(string name) => name == id ? newId : name;
        Change($"Rename zone {id} to {newId}", Template with
        {
            Zones = [.. Template.Zones.Select(z => z with { Id = Rename(z.Id), Mirror = z.Mirror is { } m ? Rename(m) : null })],
            Connections = [.. Template.Connections.Select(c => c with { From = Rename(c.From), To = Rename(c.To) })],
        });
        return true;
    }

    /// <summary>Moves a zone's node in the graph (no effect on the map).</summary>
    public bool MoveZone(string id, double x, double y)
    {
        if (Template.Zone(id) is not { } zone)
        {
            return false;
        }

        return ReplaceZone(zone with { GraphX = x, GraphY = y }, $"Move zone {id}");
    }

    /// <summary>Removes a zone, its connections and the mirrors that name it.</summary>
    public bool RemoveZone(string id)
    {
        if (Template.Zone(id) is null)
        {
            return false;
        }

        ChangeSynced($"Remove zone {id}", Template with
        {
            Zones = [.. Template.Zones.Where(z => z.Id != id).Select(z => z.Mirror == id ? z with { Mirror = null } : z)],
            Connections = [.. Template.Connections.Where(c => c.From != id && c.To != id)],
        });
        return true;
    }

    /// <summary>Joins two zones by a normal connection guarded by <paramref name="guardValue"/>; refused for a zone to itself or a pair already joined.</summary>
    public bool Connect(string from, string to, int guardValue = 1500)
    {
        if (from == to || Template.Zone(from) is null || Template.Zone(to) is null
            || Template.Connections.Any(c => (c.From == from && c.To == to) || (c.From == to && c.To == from)))
        {
            return false;
        }

        var levels = Template.Zone(from)!.Level != Template.Zone(to)!.Level;
        Change($"Connect {from} – {to}", Template with { Connections = [.. Template.Connections, new TemplateConnection { From = from, To = to, GuardValue = guardValue, Kind = levels ? ConnectionKind.Gate : ConnectionKind.Normal }] });
        return true;
    }

    /// <summary>Replaces the connection at an index (the zones it joins may change too, to existing zones).</summary>
    public bool UpdateConnection(int index, TemplateConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (index < 0 || index >= Template.Connections.Count || Template.Zone(connection.From) is null || Template.Zone(connection.To) is null)
        {
            return false;
        }

        if (connection != Template.Connections[index])
        {
            Change($"Change connection {connection.From} – {connection.To}", Template with { Connections = [.. Template.Connections.Select((c, i) => i == index ? connection : c)] });
        }

        return true;
    }

    public bool Disconnect(int index)
    {
        if (index < 0 || index >= Template.Connections.Count)
        {
            return false;
        }

        var removed = Template.Connections[index];
        Change($"Remove connection {removed.From} – {removed.To}", Template with { Connections = [.. Template.Connections.Where((_, i) => i != index)] });
        return true;
    }

    /// <summary>
    /// Places every zone without a graph place (and, with <paramref name="all"/>, every zone) on rings around the best connected one, like the
    /// layout's first guess: zones one connection away on the first ring, and so on.
    /// </summary>
    public void ArrangeGraph(bool all = false)
    {
        var arranged = Arranged(Template, all);
        if (!arranged.Zones.SequenceEqual(Template.Zones))
        {
            Change("Arrange the graph", arranged);
        }
    }

    /// <summary>The template with its zones placed as <see cref="ArrangeGraph"/> places them.</summary>
    private static MapTemplate Arranged(MapTemplate template, bool all)
    {
        var zones = template.Zones;
        if (zones.Count == 0)
        {
            return template;
        }

        int Degree(TemplateZone z) => template.Connections.Count(c => c.From == z.Id || c.To == z.Id);
        var centre = zones.OrderByDescending(Degree).First().Id;
        var depth = new Dictionary<string, int> { [centre] = 0 };
        var queue = new Queue<string>([centre]);
        while (queue.TryDequeue(out var at))
        {
            foreach (var next in template.Connections.Where(c => c.From == at || c.To == at).Select(c => c.From == at ? c.To : c.From))
            {
                if (depth.TryAdd(next, depth[at] + 1))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var deepest = depth.Values.Max() + 1;
        var rings = zones.GroupBy(z => depth.GetValueOrDefault(z.Id, deepest)).ToDictionary(g => g.Key, g => g.ToList());
        var placed = zones.Select(z =>
        {
            if (!all && z.GraphX is not null && z.GraphY is not null)
            {
                return z;
            }

            var ring = depth.GetValueOrDefault(z.Id, deepest);
            var members = rings[ring];
            var angle = (2 * Math.PI * members.IndexOf(z) / members.Count) + (ring * 0.4);
            var radius = 3.0 * ring;
            return z with { GraphX = Math.Round(radius * Math.Cos(angle), 2), GraphY = Math.Round(radius * Math.Sin(angle), 2) };
        });
        return template with { Zones = [.. placed] };
    }

    /// <summary>
    /// Keeps the player count with the start zones: the most players is their number, the fewest not above it, the human limit and team counts
    /// within it.
    /// </summary>
    public static MapTemplate SyncPlayers(MapTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var starts = Math.Max(2, template.Zones.Count(z => z.Type is ZoneType.PlayerStart or ZoneType.AiStart));
        return template with
        {
            MaxPlayers = starts,
            MinPlayers = Math.Clamp(template.MinPlayers, 2, starts),
            MaxHumans = template.MaxHumans is { } humans ? Math.Min(humans, starts) : null,
            MinTeams = template.MinTeams is { } min ? Math.Min(min, starts) : null,
            MaxTeams = template.MaxTeams is { } max ? Math.Min(max, starts) : null,
        };
    }

    private void ChangeSynced(string description, MapTemplate template) => Change(description, SyncPlayers(template));

    /// <summary>Puts <paramref name="template"/> in place as the step <paramref name="description"/>, or as part of the open step.</summary>
    private void Change(string description, MapTemplate template)
    {
        var before = Template;
        Set(template, replaced: false);
        if (_step is null)
        {
            Record(description, before);
        }
    }

    private void Record(string description, MapTemplate before)
    {
        History.Record(new TemplateStep(description, before, Template, t => Set(t, replaced: true)));
        PublishHistory();
    }

    private bool ChangeHistory(Func<bool> change)
    {
        EndStep(); // a drag whose release never came is a step of its own, before the history moves
        var changed = change();
        if (changed)
        {
            PublishHistory();
        }

        return changed;
    }

    private void Set(MapTemplate template, bool replaced)
    {
        Template = template;
        _bus.Publish(new TemplateChanged(template, TemplateValidator.Validate(template, _content), replaced));
    }

    private void PublishHistory()
    {
        _bus.Publish(new UndoHistoryChanged(History.UndoDescription, History.RedoDescription, History.Position, History.Count));
        PublishUnsaved();
    }

    /// <summary>Tells when the template starts or stops having unsaved work, not at every step.</summary>
    private void PublishUnsaved()
    {
        if (_dirty != History.IsDirty)
        {
            _dirty = History.IsDirty;
            _bus.Publish(new UnsavedWorkChanged(_dirty));
        }
    }

    /// <summary>One undo step: the template before it and after it, both kept (they are values).</summary>
    private sealed class TemplateStep(string description, MapTemplate before, MapTemplate after, Action<MapTemplate> restore) : IEditorCommand
    {
        public string Description { get; } = description;

        public void Execute() => restore(after);

        public void Undo() => restore(before);
    }
}
