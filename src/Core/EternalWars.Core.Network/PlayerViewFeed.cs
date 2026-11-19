using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Events;

namespace EternalWars.Core.Network;

/// <summary>
/// The host's side of one client's view (ADR-193, MP-3): what the player may see of the game, sent as it changes. The host's rules keep the
/// player's fog and memory; this sends
/// <list type="bullet">
/// <item>the player's own heroes, towns and everything it owns, as they are;</item>
/// <item>other heroes while the player sees them, taken away when it no longer does;</item>
/// <item>map objects while any of their cells is in the player's sight; out of sight the client keeps them as last sent ("as last seen"), and
/// an object the player remembers but has not seen since joining comes as a bare object of its kind;</item>
/// <item>the game's date, whose turn it is and the outcome; a question only when it is the player's; a battle only when the player fights it;</item>
/// <item>of other players their name, team and whether they are out, never their resources;</item>
/// <item>the events the player may see (<see cref="Shows"/>).</item>
/// </list>
/// Nothing under the player's fog is ever sent: no hero it does not see, no object it has not seen, not the seed.
/// </summary>
internal sealed class PlayerViewFeed(GameState state, PlayerId viewer)
{
    /// <summary>The <see cref="Audience.Special"/> events, each decided in <see cref="ShowsSpecial"/>.</summary>
    internal static readonly IReadOnlySet<Type> SpecialEvents =
        new HashSet<Type> { typeof(CommandExecuted), typeof(DecisionRequested), typeof(DecisionResolved), typeof(HeroMoved), typeof(HeroTeleported) };

    private static readonly Dictionary<Type, EventShape> ShapesByType = [];

    private readonly StateCodec _codec = new(state);

    /// <summary>A hash of the JSON last sent of each piece of the view, by <c>kind:id</c> (128 bits of SHA-256: a missed change would need a collision).</summary>
    private readonly Dictionary<string, UInt128> _sent = [];

    private readonly ArrayBufferWriter<byte> _json = new();

    private readonly Dictionary<MapObjectId, KnownObject> _memory = [];
    private readonly List<JsonNode> _events = [];
    private readonly List<MapPosition> _explored = [];
    private HashSet<MapPosition> _sight = [];
    private int _sequence;

    public PlayerId Viewer => viewer;

    /// <summary>Notes an event the host's game just published, if the player may see it; <paramref name="battleSides"/> fight the current battle.</summary>
    public void Capture(IEvent e, IReadOnlyCollection<PlayerId> battleSides)
    {
        if (e is TilesRevealed revealed && revealed.Player == viewer)
        {
            _explored.AddRange(revealed.Positions);
        }

        if (Shows(e, battleSides))
        {
            _events.Add(_codec.Write<IEvent>(e)!);
        }
    }

    /// <summary>The first update: the whole view.</summary>
    public ViewUpdate First()
    {
        _explored.Clear();
        _explored.AddRange(state.Fog.ExploredOf(viewer));
        _events.Clear();
        return Next()!;
    }

    /// <summary>What changed since the last update; null when nothing did.</summary>
    public ViewUpdate? Next()
    {
        var changes = new List<ViewChange>();
        Put(changes, "game", 0, GameView.Of(state, viewer));
        foreach (var player in state.Players)
        {
            Put(changes, "player", player.Id.Value, PlayerView.Of(player, viewer));
        }

        PutObjects(changes);
        PutMemory(changes);
        PutHeroes(changes);
        Put(changes, "retired", 0, state.RetiredHeroesOf(viewer).ToList());
        Put(changes, "caravans", 0, state.Caravans.Where(c => c.Owner == viewer).ToList());

        IReadOnlyList<MapPosition>? sight = null;
        if (state.Fog.Mode != FogMode.PermanentReveal && state.Fog.SightOf(viewer) is var now && !_sight.SetEquals(now))
        {
            _sight = [.. now];
            sight = [.. now];
        }

        if (changes.Count == 0 && _explored.Count == 0 && sight is null && _events.Count == 0)
        {
            return null;
        }

        var update = new ViewUpdate(++_sequence, changes, [.. _explored], sight, [.. _events]);
        _explored.Clear();
        _events.Clear();
        return update;
    }

    /// <summary>
    /// Whether the player may see the event, by the event's declared <see cref="Audience"/> (ADR-202). An event that declares none is
    /// never shown.
    /// </summary>
    internal bool Shows(IEvent e, IReadOnlyCollection<PlayerId> battleSides)
    {
        var shape = EventShape.Of(e.GetType());
        return shape.Audience switch
        {
            Audience.Everyone => true,
            Audience.Owner => shape.Owner?.GetValue(e) is PlayerId owner && owner == viewer,
            Audience.Witnesses => Witnesses(e, shape.Subjects),
            Audience.BattleSides => battleSides.Contains(viewer),
            Audience.Special => ShowsSpecial(e, battleSides),
            _ => false,
        };
    }

    private bool ShowsSpecial(IEvent e, IReadOnlyCollection<PlayerId> battleSides)
    {
        switch (e)
        {
            case CommandExecuted executed:
                return executed.Command.Player == viewer;
            case DecisionRequested or DecisionResolved:
                var decision = e is DecisionRequested requested ? requested.Decision : ((DecisionResolved)e).Decision;
                return decision is BattleDecision ? battleSides.Contains(viewer) : GameView.Asks(state, decision) == viewer;
            case HeroMoved moved:
                return SeesHeroAt(moved.Hero, moved.From, moved.To);
            case HeroTeleported teleported:
                return SeesHeroAt(teleported.Hero, teleported.From, teleported.To);
            default:
                return false;
        }
    }

    /// <summary>The player sees a hero the event names; naming no hero, an object it names.</summary>
    private bool Witnesses(IEvent e, PropertyInfo[] subjects)
    {
        var heroes = new List<HeroId>();
        var objects = new List<MapObjectId>();
        foreach (var property in subjects)
        {
            switch (property.GetValue(e))
            {
                case HeroId hero:
                    heroes.Add(hero);
                    break;
                case MapObjectId mapObject:
                    objects.Add(mapObject);
                    break;
                case IEnumerable<HeroId> many:
                    heroes.AddRange(many);
                    break;
                case IEnumerable<MapObjectId> many:
                    objects.AddRange(many);
                    break;
            }
        }

        return heroes.Count > 0 ? heroes.Any(SeesHero) : objects.Any(SeesObject);
    }

    /// <summary>
    /// What <see cref="Shows"/> reads of an event type: its declared audience (null when it declares none), the property naming the player an
    /// <see cref="Audience.Owner"/> event is for, and the properties naming the heroes and objects a <see cref="Audience.Witnesses"/> event is about.
    /// </summary>
    internal sealed record EventShape(Audience? Audience, PropertyInfo? Owner, PropertyInfo[] Subjects)
    {
        public static EventShape Of(Type type)
        {
            lock (ShapesByType)
            {
                if (!ShapesByType.TryGetValue(type, out var shape))
                {
                    var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract").ToArray();
                    shape = new EventShape(
                        type.GetCustomAttribute<SeenByAttribute>()?.Audience,
                        properties.FirstOrDefault(p => p.PropertyType == typeof(PlayerId) && p.Name is "Player" or "Owner"),
                        properties.Where(p => Names<HeroId>(p.PropertyType) || Names<MapObjectId>(p.PropertyType)).ToArray());
                    ShapesByType[type] = shape;
                }

                return shape;
            }
        }

        private static bool Names<TId>(Type type)
            where TId : struct =>
            type == typeof(TId) || type == typeof(TId?) || typeof(IEnumerable<TId>).IsAssignableFrom(type);
    }

    private Hero? FindHero(HeroId id) => state.Heroes.FirstOrDefault(h => h.Id == id) ?? state.GarrisonHeroes.FirstOrDefault(h => h.Id == id);

    /// <summary>The player's own heroes, those it sees, and one it was shown that is gone now (its defeat, its removal).</summary>
    private bool SeesHero(HeroId id) => FindHero(id) is { } hero
        ? hero.Owner == viewer || (state.Heroes.Contains(hero) ? state.CanSee(viewer, hero) : SeesObject(state.GarrisonOf(id)!.Id))
        : _sent.ContainsKey(Key("hero", id.Value));

    private bool SeesHeroAt(HeroId id, MapPosition from, MapPosition to) =>
        FindHero(id)?.Owner == viewer || state.Fog.IsInSight(viewer, from) || state.Fog.IsInSight(viewer, to);

    /// <summary>The player's own objects, those with a cell in its sight, and one it remembers that is gone now.</summary>
    private bool SeesObject(MapObjectId id) => state.FindObject(id) is { } mapObject
        ? Owns(mapObject) || InSight(mapObject)
        : _memory.ContainsKey(id);

    private bool Owns(MapObject mapObject) => mapObject is IFlaggable { Owner: { } owner } && owner == viewer;

    private bool InSight(MapObject mapObject) => mapObject.OccupiedCells().Any(c => state.Fog.IsInSight(viewer, c.Position));

    private void PutHeroes(List<ViewChange> changes)
    {
        var shown = state.Heroes.Where(h => state.CanSee(viewer, h)).ToList();
        foreach (var hero in shown)
        {
            Put(changes, "hero", hero.Id.Value, hero);
        }

        TakeAway(changes, "hero", shown.Select(h => h.Id.Value));
    }

    private void PutObjects(List<ViewChange> changes)
    {
        var known = state.Memory.Of(viewer).Select(k => k.Id).ToHashSet();
        foreach (var mapObject in state.Objects)
        {
            if (Owns(mapObject) || (known.Contains(mapObject.Id) && InSight(mapObject)))
            {
                Put(changes, "object", mapObject.Id.Value, mapObject);
            }
        }

        // Remembered but not seen since the client joined: an object of its kind, as the player last saw it (its place and flag).
        foreach (var remembered in state.Memory.Of(viewer).OrderBy(k => k.Id.Value))
        {
            if (!_sent.ContainsKey(Key("object", remembered.Id.Value)) && Bare(remembered) is { } bare)
            {
                Put(changes, "object", remembered.Id.Value, bare);
            }
        }

        // Gone, as far as the player knows: its memory let the object go (seen gone). What it owns it always knows.
        TakeAway(changes, "object", known.Select(id => id.Value).Concat(state.Objects.Where(Owns).Select(o => o.Id.Value)));
    }

    private static MapObject? Bare(KnownObject known)
    {
        try
        {
            var bare = MapObject.Create(known.Id, known.Definition, known.Position);
            if (known.Owner is { } owner && bare.GetType().GetProperty(nameof(IFlaggable.Owner)) is { CanWrite: true } flag)
            {
                flag.SetValue(bare, owner);
            }

            return bare;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }

    private void PutMemory(List<ViewChange> changes)
    {
        var current = state.Memory.Of(viewer).ToDictionary(k => k.Id);
        foreach (var (id, known) in current.OrderBy(k => k.Key.Value))
        {
            if (!_memory.TryGetValue(id, out var sent) || sent != known)
            {
                _memory[id] = known;
                changes.Add(new ViewChange("memory", id.Value, _codec.Write(known)));
            }
        }

        foreach (var id in _memory.Keys.Where(id => !current.ContainsKey(id)).ToList())
        {
            _memory.Remove(id);
            changes.Add(new ViewChange("memory", id.Value, null));
        }
    }

    /// <summary>Adds the piece when it differs from what was last sent of it.</summary>
    private void Put<T>(List<ViewChange> changes, string kind, int id, T value)
    {
        var node = _codec.Write(value);
        var hash = Hash(node);
        var key = Key(kind, id);
        if (!_sent.TryGetValue(key, out var sent) || sent != hash)
        {
            _sent[key] = hash;
            changes.Add(new ViewChange(kind, id, node));
        }
    }

    private UInt128 Hash(JsonNode? node)
    {
        _json.ResetWrittenCount();
        using (var writer = new Utf8JsonWriter(_json))
        {
            if (node is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                node.WriteTo(writer);
            }
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(_json.WrittenSpan, digest);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt128LittleEndian(digest);
    }

    /// <summary>Takes away the pieces of <paramref name="kind"/> sent before and not among <paramref name="kept"/>.</summary>
    private void TakeAway(List<ViewChange> changes, string kind, IEnumerable<int> kept)
    {
        var keep = kept.Select(id => Key(kind, id)).ToHashSet();
        foreach (var key in _sent.Keys.Where(k => k.StartsWith(kind + ":", StringComparison.Ordinal) && !keep.Contains(k)).ToList())
        {
            _sent.Remove(key);
            changes.Add(new ViewChange(kind, int.Parse(key[(kind.Length + 1)..], System.Globalization.CultureInfo.InvariantCulture), null));
        }
    }

    private static string Key(string kind, int id) => $"{kind}:{id}";
}
