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
/// <item>Zones: <see cref="AddZone(ZoneType, double, double)"/> (a new id, a start zone takes the next free player slot), <see cref="UpdateZone"/>, <see cref="RenameZone"/>
/// (connections and mirrors follow), <see cref="MoveZone"/> (its place in the graph), <see cref="RemoveZone"/> (with its connections).</item>
/// <item>Several zones (a selection in the graph): <see cref="MoveZones"/>, <see cref="AlignZones"/> and <see cref="SpreadZones"/> (ADR-270),
/// <see cref="RemoveZones"/>, and <see cref="Copy"/>, <see cref="Paste"/> and <see cref="Duplicate"/> with their inner connections and new ids
/// (ADR-271).</item>
/// <item>Connections: <see cref="Connect"/>, <see cref="UpdateConnection"/>, <see cref="Disconnect"/>.</item>
/// <item>Mirrors (ADR-272): <see cref="MakeMirror"/> adds the images of zones under the template's symmetry, <see cref="BreakMirror"/> unlinks a
/// cycle. While a zone is in a closed cycle of mirrors, an edit of it carries to its images (<see cref="TemplateMirrors.Carry(TemplateZone, TemplateZone, TemplateZone, Func{int})"/>),
/// and connecting, changing or removing a connection does the same to the connection's images, in the same step.</item>
/// <item>Presets (ADR-273): <see cref="AddZone(ZonePreset, double, double)"/> and <see cref="ApplyPreset"/>.</item>
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

    /// <summary>The game's tables the template is checked against (terrains, buildings), for the inspector's pickers.</summary>
    public ContentCatalogs Content => _content ?? ContentCatalogs.Default;

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
            StartZone("start-2", 1) with { GraphX = 6, GraphY = 0 },
            new TemplateZone { Id = "middle", Type = ZoneType.Junction, Size = 2, Monsters = MonsterStrength.Average, GraphX = 3, GraphY = 0 },
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
    public string? AddZone(ZoneType type, double x, double y) => Add(type switch
    {
        ZoneType.PlayerStart or ZoneType.AiStart => StartZone(ZonePresets.PresetId, 0) with { Type = type },
        _ => new TemplateZone
        {
            Id = ZonePresets.PresetId, Type = type, Size = 2, Monsters = type == ZoneType.Junction ? MonsterStrength.Strong : MonsterStrength.Average,
            Treasure = type == ZoneType.Treasure ? [new TreasureBand(1500, 4000, 4)] : [], ExtraPiles = 2,
        },
    }, x, y, "");

    /// <summary>
    /// Adds a zone with the preset's settings at a place in the graph (ADR-273), as <see cref="AddZone(ZoneType, double, double)"/> does: the
    /// type's id, the lowest free slot for a start zone (none: refused, null), one step "Add zone x (preset)".
    /// </summary>
    public string? AddZone(ZonePreset preset, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return Add(ZonePresets.Settings(preset.Zone), x, y, $" ({preset.Name})");
    }

    private string? Add(TemplateZone settings, double x, double y, string note)
    {
        var prefix = settings.Type switch { ZoneType.PlayerStart or ZoneType.AiStart => "start", ZoneType.Treasure => "treasure", _ => "junction" };
        var id = Enumerable.Range(1, 999).Select(n => $"{prefix}-{n}").First(candidate => Template.Zone(candidate) is null);
        var zone = settings with { Id = id, GraphX = x, GraphY = y, Owner = null, Team = null };
        if (settings.Type is ZoneType.PlayerStart or ZoneType.AiStart)
        {
            if (!FreeSlots(Template).TryDequeue(out var slot))
            {
                return null;
            }

            zone = zone with { Owner = slot };
        }

        ChangeSynced($"Add zone {id}{note}", Template with { Zones = [.. Template.Zones, zone] });
        return id;
    }

    /// <summary>
    /// Gives the zone the preset's settings (ADR-273), keeping its id, slot, team, mirror, level and graph place, as one step "Apply preset
    /// p to zone x"; a zone in a mirror cycle carries the change to its images. False for an unknown zone, or a zone made a start zone with no
    /// player slot free.
    /// </summary>
    public bool ApplyPreset(string id, ZonePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (Template.Zone(id) is not { } zone)
        {
            return false;
        }

        var slots = FreeSlots(Template);
        var full = false;
        var applied = ZonePresets.Apply(zone, preset, () =>
        {
            full = !slots.TryDequeue(out var slot);
            return slot;
        });
        return !full && ReplaceZone(applied, $"Apply preset {preset.Name} to zone {id}");
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
            var images = placed.Mirror == old.Mirror && TemplateMirrors.Cycle(Template, zone.Id) is { } cycle ? cycle.Skip(1).ToHashSet() : [];
            var slots = FreeSlots(Template with { Zones = [.. Template.Zones.Select(z => z.Id == zone.Id ? placed : z)] });
            var full = false;
            int Slot()
            {
                full |= !slots.TryDequeue(out var slot);
                return slot;
            }

            var zones = Template.Zones.Select(z => z.Id == zone.Id ? placed : images.Contains(z.Id) ? TemplateMirrors.Carry(old, placed, z, Slot) : z).ToList();
            if (full)
            {
                return false; // the images made start zones need more player slots than are free
            }

            ChangeSynced(description, Template with { Zones = zones });
        }

        return true;
    }

    /// <summary>The player slots no zone of <paramref name="template"/> holds, lowest first.</summary>
    private static Queue<int> FreeSlots(MapTemplate template)
    {
        var used = template.Zones.Select(z => z.Owner).OfType<int>().ToHashSet();
        return new Queue<int>(Enumerable.Range(0, GameState.MaxPlayers).Where(s => !used.Contains(s)));
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

    /// <summary>
    /// Moves several zones' nodes at once (a selection dragged, ADR-271): each id to its place. False when an id is unknown (nothing moves); one
    /// step "Move zone x" or "Move N zones".
    /// </summary>
    public bool MoveZones(IReadOnlyDictionary<string, (double X, double Y)> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        if (places.Count == 0 || places.Keys.Any(id => Template.Zone(id) is null))
        {
            return false;
        }

        return ReplaceZones([.. places.Select(p => Template.Zone(p.Key)! with { GraphX = p.Value.X, GraphY = p.Value.Y })], Moved([.. places.Keys]));
    }

    /// <summary>
    /// Puts the zones on one row (one height) or column (one x), at their mean, snapped to <paramref name="grid"/> (ADR-270). False for fewer
    /// than two known zones.
    /// </summary>
    public bool AlignZones(IEnumerable<string> ids, GraphLine line, double grid = 0)
    {
        var zones = Known(ids);
        return zones.Count >= 2
            && ReplaceZones(TemplateGraph.Align(zones, line, grid), $"Align {zones.Count} zones in a {(line == GraphLine.Row ? "row" : "column")}");
    }

    /// <summary>Spaces the zones evenly along a row or a column, the outer two staying (ADR-270). False for fewer than three known zones.</summary>
    public bool SpreadZones(IEnumerable<string> ids, GraphLine line)
    {
        var zones = Known(ids);
        return zones.Count >= 3
            && ReplaceZones(TemplateGraph.Spread(zones, line), $"Spread {zones.Count} zones {(line == GraphLine.Row ? "across" : "down")}");
    }

    /// <summary>The known zones of <paramref name="ids"/>, in the template's order, each once.</summary>
    private List<TemplateZone> Known(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var wanted = ids.ToHashSet();
        return [.. Template.Zones.Where(z => wanted.Contains(z.Id))];
    }

    private static string Moved(IReadOnlyCollection<string> ids) => ids.Count == 1 ? $"Move zone {ids.First()}" : $"Move {ids.Count} zones";

    /// <summary>Replaces the zones of the same ids (graph places only change) as one step; true, also when nothing changed (no step then).</summary>
    private bool ReplaceZones(IReadOnlyList<TemplateZone> zones, string description)
    {
        var byId = zones.ToDictionary(z => z.Id);
        var changed = Template with { Zones = [.. Template.Zones.Select(z => byId.GetValueOrDefault(z.Id, z))] };
        if (!changed.Zones.SequenceEqual(Template.Zones))
        {
            Change(description, changed);
        }

        return true;
    }

    /// <summary>Removes a zone, its connections and the mirrors that name it.</summary>
    public bool RemoveZone(string id) => Template.Zone(id) is not null && RemoveAll([id], $"Remove zone {id}") > 0;

    /// <summary>Removes the zones (a selection), their connections and the mirrors that name them, as one step; returns how many were removed.</summary>
    public int RemoveZones(IEnumerable<string> ids)
    {
        var zones = Known(ids);
        return zones.Count == 0 ? 0 : RemoveAll(zones.Select(z => z.Id), zones.Count == 1 ? $"Remove zone {zones[0].Id}" : $"Remove {zones.Count} zones");
    }

    private int RemoveAll(IEnumerable<string> ids, string description)
    {
        var gone = ids.ToHashSet();
        ChangeSynced(description, Template with
        {
            Zones = [.. Template.Zones.Where(z => !gone.Contains(z.Id)).Select(z => z.Mirror is { } m && gone.Contains(m) ? z with { Mirror = null } : z)],
            Connections = [.. Template.Connections.Where(c => !gone.Contains(c.From) && !gone.Contains(c.To))],
        });
        return gone.Count;
    }

    /// <summary>The zones of <paramref name="ids"/> with the connections between them, for the clipboard (ADR-271); null when none is known.</summary>
    public TemplateFragment? Copy(IEnumerable<string> ids) => TemplateFragment.Of(Template, ids);

    /// <summary>
    /// Adds the fragment's zones with the connections between them, as one step "Paste N zones", centred on <paramref name="x"/>,
    /// <paramref name="y"/> in the graph (ADR-271). Each zone gets a new id (its name's stem and the next free number: <c>treasure-3</c>), a start
    /// zone the lowest free player slot (its team kept); a mirror inside the fragment follows its zone, one outside is dropped. Returns the new
    /// ids, or null when the fragment is empty or the free player slots are too few for its start zones (nothing changes then).
    /// </summary>
    public IReadOnlyList<string>? Paste(TemplateFragment fragment, double x, double y) => PasteAs(fragment, x, y, "Paste");

    /// <summary>Pastes a copy of the zones beside them (<paramref name="dx"/>, <paramref name="dy"/> graph units off), as <see cref="Paste"/> does, as one step "Duplicate N zones".</summary>
    public IReadOnlyList<string>? Duplicate(IEnumerable<string> ids, double dx = 1, double dy = 1)
    {
        if (Copy(ids) is not { } fragment)
        {
            return null;
        }

        var (cx, cy) = fragment.Centre;
        return PasteAs(fragment, cx + dx, cy + dy, "Duplicate");
    }

    private IReadOnlyList<string>? PasteAs(TemplateFragment fragment, double x, double y, string verb)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var slots = FreeSlots(Template);
        if (fragment.Zones.Count == 0 || fragment.StartZones > slots.Count)
        {
            return null;
        }

        var taken = Template.Zones.Select(z => z.Id).ToHashSet();
        var ids = fragment.Zones.ToDictionary(z => z.Id, z => NewId(z.Id, taken));

        var (cx, cy) = fragment.Centre;
        var zones = fragment.Zones.Select(zone =>
        {
            var (zx, zy) = TemplateGraph.Place(zone);
            return zone with
            {
                Id = ids[zone.Id],
                Owner = zone.Type is ZoneType.PlayerStart or ZoneType.AiStart ? slots.Dequeue() : zone.Owner,
                Mirror = zone.Mirror is { } m && ids.TryGetValue(m, out var image) ? image : null,
                GraphX = Math.Round(zx - cx + x, 2),
                GraphY = Math.Round(zy - cy + y, 2),
            };
        }).ToList();
        var connections = fragment.Connections
            .Where(c => ids.ContainsKey(c.From) && ids.ContainsKey(c.To))
            .Select(c => c with { From = ids[c.From], To = ids[c.To] });
        ChangeSynced(zones.Count == 1 ? $"{verb} zone {zones[0].Id}" : $"{verb} {zones.Count} zones", Template with
        {
            Zones = [.. Template.Zones, .. zones],
            Connections = [.. Template.Connections, .. connections],
        });
        return [.. zones.Select(z => z.Id)];
    }

    /// <summary>A new id for a copy of <paramref name="id"/>: its stem and the next number not in <paramref name="taken"/>, which gets it.</summary>
    private static string NewId(string id, HashSet<string> taken)
    {
        var stem = Stem(id);
        var newId = Enumerable.Range(1, int.MaxValue - 1).Select(n => $"{stem}-{n}").First(candidate => !taken.Contains(candidate));
        taken.Add(newId);
        return newId;
    }

    /// <summary>
    /// Why <see cref="MakeMirror"/> cannot mirror the zones; null when it can: the template needs a symmetry, and every zone must be known,
    /// name no mirror and be named by none; each start zone needs a free player slot per image.
    /// </summary>
    public string? MirrorRefusal(IEnumerable<string> ids)
    {
        var zones = Known(ids);
        if (Template.Symmetry == TemplateSymmetry.None)
        {
            return "The template has no symmetry: choose one in its settings first";
        }

        if (zones.Count == 0)
        {
            return "No zone is selected";
        }

        if (zones.FirstOrDefault(z => !TemplateMirrors.IsOwnImage(z) || TemplateMirrors.IsNamed(Template, z.Id)) is { } mirrored)
        {
            return $"Zone {mirrored.Id} is mirrored already (break its mirror first)";
        }

        var images = MapTemplate.Order(Template.Symmetry) - 1;
        var starts = zones.Count(z => z.Type is ZoneType.PlayerStart or ZoneType.AiStart) * images;
        return starts > FreeSlots(Template).Count ? $"No room: the images need {starts} free player slot(s)" : null;
    }

    /// <summary>
    /// Adds the images of the zones under the template's symmetry (ADR-272), as one step "Mirror zone x" or "Mirror N zones": one image per
    /// step of the symmetry (one across an axis or a half turn, three for a quarter turn), alike in everything but the id (the stem and the next
    /// free number), the player slot (the lowest free, with no team) and the graph place (the zone's place mirrored around the middle of the other zones). The
    /// zones and their images name each other as mirrors in a cycle. Each connection of a zone gets its images: to the other zone's image when
    /// that is mirrored too (or among the zones), to the zone itself when it is its own image. Returns the new ids, or null when
    /// <see cref="MirrorRefusal"/> refuses (nothing changes then).
    /// </summary>
    public IReadOnlyList<string>? MakeMirror(IEnumerable<string> ids)
    {
        var zones = Known(ids);
        if (MirrorRefusal(zones.Select(z => z.Id)) is not null)
        {
            return null;
        }

        var symmetry = Template.Symmetry;
        var order = MapTemplate.Order(symmetry);
        var selected = zones.Select(z => z.Id).ToHashSet();
        var others = Template.Zones.Where(z => !selected.Contains(z.Id)).ToList();
        var centre = others.Count > 0
            ? TemplateGraph.Centre(others)
            : (zones.Max(z => TemplateGraph.Place(z).X) + 1.5, TemplateGraph.Centre(zones).Y); // nothing else: the axis just right of the zones
        var taken = Template.Zones.Select(z => z.Id).ToHashSet();
        var slots = FreeSlots(Template);
        var placesTaken = Template.Zones.Select(TemplateGraph.Place).ToList();

        // image[k][id]: the id of the zone's image after k steps (k = 0: the zone itself).
        var image = new List<Dictionary<string, string>> { selected.ToDictionary(id => id, id => id) };
        for (var k = 1; k < order; k++)
        {
            image.Add(zones.ToDictionary(z => z.Id, z => NewId(z.Id, taken)));
        }

        var added = new List<TemplateZone>();
        for (var k = 1; k < order; k++)
        {
            foreach (var zone in zones)
            {
                var place = TemplateMirrors.Place(TemplateGraph.Place(zone), symmetry, k, centre);
                while (TemplateGraph.Crowded(place, placesTaken))
                {
                    place = (place.X, place.Y + 1.5); // clear of the cards there (a zone near the axis would hide its image)
                }

                placesTaken.Add(place);
                added.Add(zone with
                {
                    Id = image[k][zone.Id],
                    Owner = zone.Type is ZoneType.PlayerStart or ZoneType.AiStart ? slots.Dequeue() : zone.Owner,
                    Team = null, // another player: no ally of the zone's unless set so
                    Mirror = image[(k + 1) % order][zone.Id],
                    GraphX = place.X,
                    GraphY = place.Y,
                });
            }
        }

        string? ImageOf(string id, int k) => selected.Contains(id) ? image[k][id] : TemplateMirrors.Image(Template, id, k);
        var connections = Template.Connections.ToList();
        foreach (var c in Template.Connections.Where(c => selected.Contains(c.From) || selected.Contains(c.To)))
        {
            for (var k = 1; k < order; k++)
            {
                if (ImageOf(c.From, k) is { } from && ImageOf(c.To, k) is { } to && from != to
                    && !connections.Any(o => (o.From == from && o.To == to) || (o.From == to && o.To == from)))
                {
                    connections.Add(c with { From = from, To = to });
                }
            }
        }

        var linked = Template.Zones.Select(z => selected.Contains(z.Id) ? z with { Mirror = image[1][z.Id] } : z);
        ChangeSynced(zones.Count == 1 ? $"Mirror zone {zones[0].Id}" : $"Mirror {zones.Count} zones", Template with
        {
            Zones = [.. linked, .. added],
            Connections = connections,
        });
        return [.. added.Select(z => z.Id)];
    }

    /// <summary>
    /// Unlinks the mirrors of the zone's cycle (or of the zone and the zone naming it, when the cycle is broken), as one step "Break the mirror
    /// of x": the zones stay, each its own image, and edits no longer carry between them. False when the zone is in no mirror.
    /// </summary>
    public bool BreakMirror(string id)
    {
        if (Template.Zone(id) is not { } zone || (TemplateMirrors.IsOwnImage(zone) && !TemplateMirrors.IsNamed(Template, id)))
        {
            return false;
        }

        var members = (TemplateMirrors.Cycle(Template, id) ?? [id]).ToHashSet();
        Change($"Break the mirror of {id}", Template with
        {
            Zones = [.. Template.Zones.Select(z => members.Contains(z.Id) || (z.Mirror is { } m && members.Contains(m)) ? z with { Mirror = null } : z)],
        });
        return true;
    }

    /// <summary>
    /// The pairs of zones the connection <paramref name="from"/> – <paramref name="to"/> turns into under the symmetry's steps (ADR-272): only
    /// where both ends have an image, without the pair itself or a zone joined to itself, each pair once.
    /// </summary>
    private List<(string From, string To)> ImagePairs(string from, string to)
    {
        var pairs = new List<(string From, string To)>();
        for (var k = 1; k < MapTemplate.Order(Template.Symmetry); k++)
        {
            if (TemplateMirrors.Image(Template, from, k) is { } a && TemplateMirrors.Image(Template, to, k) is { } b && a != b
                && !((a == from && b == to) || (a == to && b == from)) && !pairs.Any(p => (p.From == a && p.To == b) || (p.From == b && p.To == a)))
            {
                pairs.Add((a, b));
            }
        }

        return pairs;
    }

    /// <summary>A zone id without its number: <c>treasure-2</c> → <c>treasure</c>; <c>middle</c> stays.</summary>
    private static string Stem(string id)
    {
        var dash = id.LastIndexOf('-');
        return dash > 0 && dash < id.Length - 1 && id[(dash + 1)..].All(char.IsAsciiDigit) ? id[..dash] : id;
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
        var connection = new TemplateConnection { From = from, To = to, GuardValue = guardValue, Kind = levels ? ConnectionKind.Gate : ConnectionKind.Normal };
        var images = ImagePairs(from, to).Where(p => TemplateMirrors.Find(Template, p.From, p.To) < 0).Select(p => connection with { From = p.From, To = p.To });
        Change($"Connect {from} – {to}", Template with { Connections = [.. Template.Connections, connection, .. images] });
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

        var old = Template.Connections[index];
        if (connection != old)
        {
            // While its ends stay, the images of the connection (ADR-272) take the changed fields.
            var images = old.From == connection.From && old.To == connection.To
                ? ImagePairs(old.From, old.To).Select(p => TemplateMirrors.Find(Template, p.From, p.To)).Where(i => i >= 0 && i != index).ToHashSet()
                : [];
            Change($"Change connection {connection.From} – {connection.To}", Template with
            {
                Connections = [.. Template.Connections.Select((c, i) => i == index ? connection : images.Contains(i) ? TemplateMirrors.Carry(old, connection, c) : c)],
            });
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
        var gone = ImagePairs(removed.From, removed.To).Select(p => TemplateMirrors.Find(Template, p.From, p.To)).Append(index).ToHashSet();
        Change($"Remove connection {removed.From} – {removed.To}", Template with { Connections = [.. Template.Connections.Where((_, i) => !gone.Contains(i))] });
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
