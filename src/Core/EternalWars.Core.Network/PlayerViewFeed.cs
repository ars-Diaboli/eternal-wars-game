using System.Collections;
using System.Reflection;
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
    /// <summary>Events everyone sees, whoever they name: the turn order, who is out.</summary>
    private static readonly HashSet<Type> PublicEvents =
        [typeof(TurnStarted), typeof(TurnEnded), typeof(PlayerDefeated), typeof(GracePeriodStarted), typeof(GracePeriodEnded)];

    /// <summary>Events that name an owner but are about a hero others may see: they follow the hero.</summary>
    private static readonly HashSet<Type> HeroEvents =
        [typeof(HeroDefeated), typeof(HeroSurrendered), typeof(HeroPlaced), typeof(HeroFreed), typeof(MapEventHeroGiven)];

    private static readonly Dictionary<Type, PropertyInfo[]> PropertiesByType = [];

    private readonly StateCodec _codec = new(state);

    /// <summary>The JSON last sent of each piece of the view, by <c>kind:id</c>.</summary>
    private readonly Dictionary<string, string> _sent = [];

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
        if (state.Fog.Mode != FogMode.PermanentReveal && !_sight.SetEquals(state.Fog.SightOf(viewer)))
        {
            _sight = [.. state.Fog.SightOf(viewer)];
            sight = [.. _sight];
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

    /// <summary>Whether the player may see the event (it happened in its sight, to it, or to everyone).</summary>
    internal bool Shows(IEvent e, IReadOnlyCollection<PlayerId> battleSides)
    {
        var type = e.GetType();
        switch (e)
        {
            case CommandExecuted executed:
                return executed.Command.Player == viewer;
            case DecisionRequested or DecisionResolved:
                var decision = e is DecisionRequested requested ? requested.Decision : ((DecisionResolved)e).Decision;
                return decision is BattleDecision ? battleSides.Contains(viewer) : state.CurrentPlayer.Id == viewer;
            case HeroMoved moved:
                return SeesHeroAt(moved.Hero, moved.From, moved.To);
            case HeroTeleported teleported:
                return SeesHeroAt(teleported.Hero, teleported.From, teleported.To);
            case BattleTriggered:
                break;
            default:
                if (type.Name.StartsWith("Battle", StringComparison.Ordinal) || type.Name.StartsWith("Siege", StringComparison.Ordinal))
                {
                    return battleSides.Contains(viewer);
                }

                break;
        }

        if (PublicEvents.Contains(type))
        {
            return true;
        }

        var heroes = new List<HeroId>();
        var objects = new List<MapObjectId>();
        PlayerId? owner = null;
        foreach (var property in PropertiesOf(type))
        {
            switch (property.GetValue(e))
            {
                case HeroId hero:
                    heroes.Add(hero);
                    break;
                case MapObjectId mapObject:
                    objects.Add(mapObject);
                    break;
                case PlayerId player when property.Name is "Player" or "Owner":
                    owner = player;
                    break;
                case IEnumerable<HeroId> many:
                    heroes.AddRange(many);
                    break;
                case IEnumerable<MapObjectId> many:
                    objects.AddRange(many);
                    break;
            }
        }

        if (owner is { } private_ && !HeroEvents.Contains(type))
        {
            return private_ == viewer;
        }

        if (heroes.Count > 0)
        {
            return heroes.Any(SeesHero);
        }

        return objects.Count == 0 || objects.Any(SeesObject);
    }

    private static PropertyInfo[] PropertiesOf(Type type)
    {
        lock (PropertiesByType)
        {
            if (!PropertiesByType.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract").ToArray();
                PropertiesByType[type] = properties;
            }

            return properties;
        }
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
        var text = node?.ToJsonString() ?? "null";
        var key = Key(kind, id);
        if (!_sent.TryGetValue(key, out var sent) || sent != text)
        {
            _sent[key] = text;
            changes.Add(new ViewChange(kind, id, node));
        }
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

/// <summary>The game as a whole, as one player may see it (ADR-193).</summary>
internal sealed record GameView(
    GameDate Date, Domain.Turns.WeekEvent WeekEvent, int CurrentPlayerIndex, bool IsStarted, GameOutcome? Outcome, ArenaBattle? Arena,
    PendingDecision? Decision, BattleState? Battle)
{
    public static GameView Of(GameState state, PlayerId viewer)
    {
        var fights = state.Battle is { } battle && (battle.Attacker.Owner == viewer || battle.Defender.Owner == viewer);
        var decision = state.PendingDecision is BattleDecision ? (fights ? state.PendingDecision : null)
            : state.CurrentPlayer.Id == viewer ? state.PendingDecision : null;
        return new(state.Date, state.WeekEvent, state.CurrentPlayerIndex, state.IsStarted, state.Outcome, state.Arena, decision,
            fights ? state.Battle : null);
    }

    public void ApplyTo(GameState state)
    {
        state.Date = Date;
        state.WeekEvent = WeekEvent;
        state.CurrentPlayerIndex = CurrentPlayerIndex;
        state.IsStarted = IsStarted;
        state.Outcome = Outcome;
        state.Arena = Arena;
        state.PendingDecision = Decision;
        state.Battle = Battle;
    }
}

/// <summary>A player as another may see it (ADR-193): never another's resources.</summary>
internal sealed record PlayerView(
    string Name, bool IsHuman, TeamId Team, bool IsDefeated, int? MaxHeroes, GameDate? TownlessSince, string? StartTown, ResourceSet Resources,
    AutoCombatMode AutoCombat)
{
    public static PlayerView Of(Domain.Players.Player player, PlayerId viewer)
    {
        var own = player.Id == viewer;
        return new(player.Name, player.IsHuman, player.Team, player.IsDefeated, player.MaxHeroes, player.TownlessSince, player.StartTown,
            own ? player.Resources : default, own ? player.AutoCombat : default);
    }

    public void ApplyTo(Domain.Players.Player player)
    {
        player.Name = Name;
        player.IsHuman = IsHuman;
        player.Team = Team;
        player.IsDefeated = IsDefeated;
        player.MaxHeroes = MaxHeroes;
        player.TownlessSince = TownlessSince;
        player.StartTown = StartTown;
        player.Resources = Resources;
        player.AutoCombat = AutoCombat;
    }
}
