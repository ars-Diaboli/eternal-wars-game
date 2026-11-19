using System.Reflection;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Events;

namespace EternalWars.Core.Network;

/// <summary>
/// The client's side of its view (ADR-193): puts what the host sent (<see cref="ViewUpdate"/>) into the client's <see cref="GameState"/> and
/// publishes the events the host let through, so the views on this computer follow the game as they do on one computer. It changes the state
/// through the session's <see cref="IViewSink"/> (ADR-218), except that a hero or object already in the view takes the host's values in place
/// (<see cref="StateCodec.CopyInto"/>), so the views that hold it follow; a hero copied so is reported to the sink (its place may have changed).
/// </summary>
/// <remarks>
/// The views know heroes from the events that bring them onto the map; a hero that comes into sight is announced with
/// <see cref="HeroPlaced"/> before the host's events, one that goes out of it with <see cref="HeroRemoved"/> after them. An event naming a hero
/// or an object the view does not hold (it was in sight only in between two updates) is left out, unless it takes one away.
/// </remarks>
internal sealed class ViewMirror(GameState state, IViewSink sink, PlayerId viewer, IEventBus bus)
{
    /// <summary>Events that tell of a hero leaving the map: the views take it away, whether or not it is still in the view.</summary>
    private static readonly HashSet<Type> Departures =
        [typeof(HeroRemoved), typeof(HeroDefeated), typeof(HeroSurrendered), typeof(HeroEnteredGarrison), typeof(MapObjectRemoved)];

    private readonly StateCodec _codec = new(state);

    public void Apply(ViewUpdate update)
    {
        var arrived = new List<Hero>();
        var departed = new List<HeroId>();
        var remembered = new List<MapObjectId>();
        foreach (var change in update.Changes)
        {
            switch (change.Kind)
            {
                case "game":
                    sink.ShowGame(_codec.Read<GameView>(change.Value)!);
                    break;
                case "player":
                    sink.ShowPlayer(new PlayerId(change.Id), _codec.Read<PlayerView>(change.Value)!);
                    break;
                case "hero":
                    PutHero(new HeroId(change.Id), change.Value is null ? null : _codec.Read<Hero>(change.Value), arrived, departed);
                    break;
                case "object":
                    PutObject(new MapObjectId(change.Id), change.Value is null ? null : _codec.Read<MapObject>(change.Value));
                    break;
                case "memory":
                    if (change.Value is null)
                    {
                        sink.Forget(viewer, new MapObjectId(change.Id));
                    }
                    else
                    {
                        sink.Remember(viewer, _codec.Read<KnownObject>(change.Value)!);
                    }

                    remembered.Add(new MapObjectId(change.Id));
                    break;
                case "retired":
                    sink.ReplaceRetiredHeroes(viewer, _codec.Read<List<Hero>>(change.Value) ?? []);
                    break;
                case "caravans":
                    sink.ReplaceCaravans(viewer, _codec.Read<List<Caravan>>(change.Value) ?? []);
                    break;
                default:
                    throw new InvalidDataException($"The host sent a piece of the game this computer does not know: {change.Kind}.");
            }
        }

        sink.MarkExplored(viewer, update.Explored);
        if (update.Sight is { } sight)
        {
            sink.SetSight(viewer, sight);
        }

        foreach (var hero in arrived)
        {
            bus.Publish(new HeroPlaced(hero.Id, hero.Owner, hero.Position));
        }

        if (remembered.Count > 0)
        {
            bus.Publish(new KnownObjectsChanged(viewer, remembered));
        }

        foreach (var node in update.Events)
        {
            if (_codec.Read<IEvent>(node) is { } e && Holds(e))
            {
                bus.Publish(e);
            }
        }

        foreach (var hero in departed)
        {
            bus.Publish(new HeroRemoved(hero));
        }
    }

    /// <summary>A hero already in the view takes the new values in place, so the views that hold it follow; a new one is added.</summary>
    private void PutHero(HeroId id, Hero? hero, List<Hero> arrived, List<HeroId> departed)
    {
        var old = state.Heroes.FirstOrDefault(h => h.Id == id);
        if (hero is null)
        {
            if (old is not null)
            {
                sink.RemoveHero(old);
            }

            departed.Add(id);
        }
        else if (old is not null)
        {
            StateCodec.CopyInto(old, hero);
            sink.HeroChanged(old);
        }
        else
        {
            sink.AddHero(hero);
            arrived.Add(hero);
        }
    }

    /// <summary>An object already in the view takes the new values in place (an object of another kind replaces it); a new one is added.</summary>
    private void PutObject(MapObjectId id, MapObject? mapObject)
    {
        var old = state.FindObject(id);
        if (mapObject is not null && old is not null && old.GetType() == mapObject.GetType() && old.Position == mapObject.Position
            && old.Definition == mapObject.Definition)
        {
            StateCodec.CopyInto(old, mapObject);
            return;
        }

        var index = old is not null ? sink.RemoveObject(old) : -1;
        if (mapObject is null)
        {
            return;
        }

        // What stood there as last seen and is gone now (a mine turned into another object) makes room for what the host shows.
        foreach (var (position, _) in mapObject.OccupiedCells())
        {
            if (state.ObjectCellAt(position) is { } stale)
            {
                sink.RemoveObject(stale.Object);
            }
        }

        sink.AddObject(mapObject, index >= 0 ? Math.Min(index, state.Objects.Count) : null);
    }

    /// <summary>Whether the view holds every hero and object the event names (or the event takes one away).</summary>
    private bool Holds(IEvent e)
    {
        if (Departures.Contains(e.GetType()))
        {
            return true;
        }

        foreach (var property in e.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var missing = property.GetValue(e) switch
            {
                HeroId hero => !HoldsHero(hero),
                MapObjectId mapObject => state.FindObject(mapObject) is null && state.Memory.Get(viewer, mapObject) is null,
                _ => false,
            };
            if (missing)
            {
                return false;
            }
        }

        return true;
    }

    private bool HoldsHero(HeroId id) =>
        state.Heroes.Any(h => h.Id == id) || state.GarrisonHeroes.Any(h => h.Id == id) || state.RetiredHeroes.Any(h => h.Id == id);
}
