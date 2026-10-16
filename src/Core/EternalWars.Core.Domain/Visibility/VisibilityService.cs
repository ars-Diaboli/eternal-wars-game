using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Visibility;

/// <summary>
/// Keeps <see cref="FogOfWar"/> and <see cref="ObjectMemory"/> up to date by reacting to other rules'
/// events: a core-side <see cref="IEventHandler{TEvent}"/> example. Publishes <see cref="TilesRevealed"/>,
/// <see cref="SightChanged"/> (refog mode) and <see cref="KnownObjectsChanged"/>.
/// </summary>
public sealed class VisibilityService(GameState state, IEventPublisher events)
    : IEventHandler<GameStarted>, IEventHandler<HeroMoved>, IEventHandler<HeroTeleported>,
      IEventHandler<MineOwnerChanged>, IEventHandler<TownOwnerChanged>, IEventHandler<DwellingOwnerChanged>, IEventHandler<MapObjectRemoved>, IEventHandler<HeroDefeated>,
      IEventHandler<HeroHired>
{
    public void Handle(GameStarted @event)
    {
        foreach (var hero in state.Heroes)
        {
            Reveal(hero.Owner, hero.Position, hero.ScoutingRadius);
        }

        foreach (var flagged in Flagged())
        {
            if (flagged.Owner is { } owner)
            {
                Reveal(owner, flagged.Object.VisitPosition, flagged.ScoutingRadius);
            }
        }

        foreach (var player in state.Players)
        {
            ObserversChanged(player.Id);
        }
    }

    // Reveal around the event's position, not the hero's current one: when a move is made from inside
    // another handler these events are queued and delivered after the hero has already walked on.
    public void Handle(HeroMoved @event) => OnHeroRelocated(state.GetHero(@event.Hero), @event.To);

    public void Handle(HeroTeleported @event) => OnHeroRelocated(state.GetHero(@event.Hero), @event.To);

    public void Handle(MineOwnerChanged @event) => OnOwnerChanged(@event.Mine, @event.Current);

    public void Handle(TownOwnerChanged @event) => OnOwnerChanged(@event.Town, @event.Current);

    public void Handle(DwellingOwnerChanged @event) => OnOwnerChanged(@event.Dwelling, @event.Current);

    public void Handle(MapObjectRemoved @event)
    {
        foreach (var player in state.Players)
        {
            RefreshMemory(player.Id);
        }
    }

    // A hired hero stands at a town entrance and looks around from there.
    public void Handle(HeroHired @event) => OnHeroRelocated(state.GetHero(@event.Hero), state.GetHero(@event.Hero).Position);

    // The defeated hero no longer observes anything; others may notice it is gone.
    public void Handle(HeroDefeated @event)
    {
        foreach (var player in state.Players)
        {
            ObserversChanged(player.Id);
        }
    }

    private void OnOwnerChanged(MapObjectId id, PlayerId? current)
    {
        var mapObject = state.GetObject(id);
        if (current is { } owner && mapObject is IFlaggable flagged)
        {
            Reveal(owner, mapObject.VisitPosition, flagged.ScoutingRadius);
        }

        // The flag change is news for everyone who can see the object; the owners' sight changes too.
        foreach (var player in state.Players)
        {
            ObserversChanged(player.Id);
        }
    }

    private IEnumerable<(MapObject Object, PlayerId? Owner, int ScoutingRadius)> Flagged() =>
        state.Objects.OfType<IFlaggable>().Select(f => ((MapObject)f, f.Owner, f.ScoutingRadius));

    private void OnHeroRelocated(Hero hero, MapPosition at)
    {
        Reveal(hero.Owner, at, hero.ScoutingRadius);
        ObserversChanged(hero.Owner);
    }

    private void Reveal(PlayerId player, MapPosition center, int radius)
    {
        var revealed = state.Fog.Reveal(player, center, radius);
        if (revealed.Count > 0)
        {
            events.Publish(new TilesRevealed(player, revealed));
        }
    }

    private void ObserversChanged(PlayerId player)
    {
        if (!state.HasPlayer(player))
        {
            return;
        }

        UpdateSight(player);
        RefreshMemory(player);
    }

    /// <summary>Sight is a pure function of current observers, so recomputing from state is always correct.</summary>
    private void UpdateSight(PlayerId player)
    {
        if (state.Options.FogMode != FogMode.ShroudWithRefog)
        {
            return;
        }

        var observers = state.HeroesOf(player)
            .Select(h => (h.Position, h.ScoutingRadius))
            .Concat(Flagged().Where(f => f.Owner == player).Select(f => (f.Object.VisitPosition, f.ScoutingRadius)))
            .ToList();
        var (entered, left) = state.Fog.UpdateSight(player, observers, state.Options.RefogMargin);
        if (entered.Count > 0 || left.Count > 0)
        {
            events.Publish(new SightChanged(player, entered, left));
        }
    }

    private void RefreshMemory(PlayerId player)
    {
        var changed = state.Memory.Refresh(state, player);
        if (changed.Count > 0)
        {
            events.Publish(new KnownObjectsChanged(player, changed));
        }
    }
}
