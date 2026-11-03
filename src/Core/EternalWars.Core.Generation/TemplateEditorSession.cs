using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Events;

namespace EternalWars.Core.Generation;

/// <summary>The template in a <see cref="TemplateEditorSession"/> changed; <see cref="Issues"/> are the validator's findings on the new one.</summary>
public sealed record TemplateChanged(MapTemplate Template, IReadOnlyList<TemplateIssue> Issues) : IEvent;

/// <summary>
/// The template editor (ADR-070 RMG-10, ADR-117), engine-free like <see cref="MapEditorSession"/> but without undo (the owner's answer): it holds
/// one <see cref="MapTemplate"/> and changes it through the operations below, each replacing the immutable template and publishing
/// <see cref="TemplateChanged"/> with the validator's issues, so the views show the zone graph and the live validation. Operations that cannot
/// apply (an unknown zone, an id in use) return false and change nothing.
/// <list type="bullet">
/// <item>Zones: <see cref="AddZone"/> (a new id, a start zone takes the next free player slot), <see cref="UpdateZone"/>, <see cref="RenameZone"/>
/// (connections and mirrors follow), <see cref="MoveZone"/> (its place in the graph), <see cref="RemoveZone"/> (with its connections).</item>
/// <item>Connections: <see cref="Connect"/>, <see cref="UpdateConnection"/>, <see cref="Disconnect"/>.</item>
/// <item>The template's own settings: <see cref="UpdateTemplate"/> (zones and connections are kept).</item>
/// </list>
/// The player count follows the start zones (<see cref="SyncPlayers"/>), so a template stays valid while start zones are added and removed.
/// </summary>
public sealed class TemplateEditorSession
{
    private readonly IEventBus _bus;

    public TemplateEditorSession(MapTemplate? template = null, IEventBus? bus = null)
    {
        _bus = bus ?? new EventBus();
        Template = template ?? Blank();
    }

    public IEventSubscriber Events => _bus;

    public MapTemplate Template { get; private set; }

    public IReadOnlyList<TemplateIssue> Issues => TemplateValidator.Validate(Template);

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

    /// <summary>Replaces the whole template (a file opened, "New").</summary>
    public void Load(MapTemplate template) => Set(template ?? throw new ArgumentNullException(nameof(template)));

    /// <summary>Changes the template's own settings (name, players, sizes, teams, symmetry, …); its zones and connections are kept.</summary>
    public void UpdateTemplate(Func<MapTemplate, MapTemplate> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var changed = change(Template);
        Set(changed with { Zones = Template.Zones, Connections = Template.Connections });
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

        SetSynced(Template with { Zones = [.. Template.Zones, zone with { GraphX = x, GraphY = y }] });
        return id;
    }

    /// <summary>Replaces the zone of the same id (its graph place is kept unless the new one has one).</summary>
    public bool UpdateZone(TemplateZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (Template.Zone(zone.Id) is not { } old)
        {
            return false;
        }

        var placed = zone with { GraphX = zone.GraphX ?? old.GraphX, GraphY = zone.GraphY ?? old.GraphY };
        SetSynced(Template with { Zones = [.. Template.Zones.Select(z => z.Id == zone.Id ? placed : z)] });
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

        string Rename(string name) => name == id ? newId : name;
        Set(Template with
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

        return UpdateZone(zone with { GraphX = x, GraphY = y });
    }

    /// <summary>Removes a zone, its connections and the mirrors that name it.</summary>
    public bool RemoveZone(string id)
    {
        if (Template.Zone(id) is null)
        {
            return false;
        }

        SetSynced(Template with
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
        Set(Template with { Connections = [.. Template.Connections, new TemplateConnection { From = from, To = to, GuardValue = guardValue, Kind = levels ? ConnectionKind.Gate : ConnectionKind.Normal }] });
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

        Set(Template with { Connections = [.. Template.Connections.Select((c, i) => i == index ? connection : c)] });
        return true;
    }

    public bool Disconnect(int index)
    {
        if (index < 0 || index >= Template.Connections.Count)
        {
            return false;
        }

        Set(Template with { Connections = [.. Template.Connections.Where((_, i) => i != index)] });
        return true;
    }

    /// <summary>
    /// Places every zone without a graph place (and, with <paramref name="all"/>, every zone) on rings around the best connected one, like the
    /// layout's first guess: zones one connection away on the first ring, and so on.
    /// </summary>
    public void ArrangeGraph(bool all = false)
    {
        var zones = Template.Zones;
        if (zones.Count == 0)
        {
            return;
        }

        int Degree(TemplateZone z) => Template.Connections.Count(c => c.From == z.Id || c.To == z.Id);
        var centre = zones.OrderByDescending(Degree).First().Id;
        var depth = new Dictionary<string, int> { [centre] = 0 };
        var queue = new Queue<string>([centre]);
        while (queue.TryDequeue(out var at))
        {
            foreach (var next in Template.Connections.Where(c => c.From == at || c.To == at).Select(c => c.From == at ? c.To : c.From))
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
        Set(Template with { Zones = [.. placed] });
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

    private void SetSynced(MapTemplate template) => Set(SyncPlayers(template));

    private void Set(MapTemplate template)
    {
        Template = template;
        _bus.Publish(new TemplateChanged(template, TemplateValidator.Validate(template)));
    }
}
