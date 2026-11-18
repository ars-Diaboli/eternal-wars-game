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
    : IFogRevealer, IEventHandler<GameStarted>, IEventHandler<HeroMoved>, IEventHandler<HeroTeleported>,
      IEventHandler<MineOwnerChanged>, IEventHandler<TownOwnerChanged>, IEventHandler<DwellingOwnerChanged>, IEventHandler<ProviderOwnerChanged>, IEventHandler<MapObjectRemoved>, IEventHandler<HeroDefeated>,
      IEventHandler<HeroHired>, IEventHandler<HeroLeftGarrison>, IEventHandler<MapObjectChanged>, IEventHandler<HeroFreed>, IEventHandler<MapEventHeroGiven>,
      IEventHandler<HeroLeveledUp>, IEventHandler<MapObjectPlaced>, IEventHandler<TownBuilt>, IEventHandler<GarrisonOwnerChanged>
{
    /// <summary>
    /// Subscribes to every event that changes what a player sees. The session calls it before any other rule subscribes, so the fog is up to date
    /// (and its follow-up events queued) before other handlers and the views react.
    /// </summary>
    internal void SubscribeTo(IEventSubscriber bus)
    {
        bus.Subscribe<GameStarted>(this);
        bus.Subscribe<HeroMoved>(this);
        bus.Subscribe<HeroTeleported>(this);
        bus.Subscribe<MineOwnerChanged>(this);
        bus.Subscribe<TownOwnerChanged>(this);
        bus.Subscribe<DwellingOwnerChanged>(this);
        bus.Subscribe<ProviderOwnerChanged>(this);
        bus.Subscribe<MapObjectRemoved>(this);
        bus.Subscribe<HeroDefeated>(this);
        bus.Subscribe<HeroHired>(this);
        bus.Subscribe<HeroLeftGarrison>(this);
        bus.Subscribe<MapObjectChanged>(this);
        bus.Subscribe<MapObjectPlaced>(this);
        bus.Subscribe<HeroFreed>(this);
        bus.Subscribe<MapEventHeroGiven>(this);
        bus.Subscribe<HeroLeveledUp>(this);
        bus.Subscribe<TownBuilt>(this);
        bus.Subscribe<GarrisonOwnerChanged>(this);
    }

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
                Reveal(owner, SightCenter(flagged.Object), flagged.ScoutingRadius);
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

    public void Handle(ProviderOwnerChanged @event) => OnOwnerChanged(@event.Provider, @event.Current);

    public void Handle(MapObjectRemoved @event)
    {
        foreach (var player in state.Players)
        {
            RefreshMemory(player.Id);
        }
    }

    // An object appeared during the game (a month's monster stack, ADR-073): whoever sees the tile notices.
    public void Handle(MapObjectPlaced @event)
    {
        foreach (var player in state.Players)
        {
            RefreshMemory(player.Id);
        }
    }

    // An object changed in place (a cleared abandoned mine, a beaten dwelling guard): whoever sees it notices.
    public void Handle(MapObjectChanged @event)
    {
        foreach (var player in state.Players)
        {
            RefreshMemory(player.Id);
        }
    }

    // An observatory, a cartographer, View Terrain or a map event shows an area for good (explored, like any revealed terrain; ADR-068, ADR-214).
    public void RevealArea(PlayerId player, MapPosition center, int radius)
    {
        Reveal(player, center, radius);
        RefreshMemory(player);
    }

    // A freed prisoner looks around from where the prison stood.
    public void Handle(HeroFreed @event) => OnHeroRelocated(state.GetHero(@event.Hero), state.GetHero(@event.Hero).Position);

    public void Handle(MapEventHeroGiven @event) => OnHeroRelocated(state.GetHero(@event.Hero), state.GetHero(@event.Hero).Position);

    // Scouting widens the hero's sight at once (ADR-069).
    public void Handle(HeroLeveledUp @event)
    {
        if (@event.Skill is { } skill && BuiltInSkills.Resolve(skill) == BuiltInSkills.Scouting && state.Heroes.FirstOrDefault(h => h.Id == @event.Hero) is { } hero)
        {
            OnHeroRelocated(hero, hero.Position);
        }
    }

    // A garrison's new flag is news for everyone who can see it. (Its sight is a garrison's, not a mine's: it reveals nothing new.)
    public void Handle(GarrisonOwnerChanged @event)
    {
        foreach (var player in state.Players)
        {
            RefreshMemory(player.Id);
        }
    }

    // The Academy's Observatory widens its town's sight at once (ADR-143).
    public void Handle(TownBuilt @event)
    {
        if (@event.Building == Towns.Town.Observatory)
        {
            OnOwnerChanged(@event.Town, ((Towns.Town)state.GetObject(@event.Town)).Owner);
        }
    }

    // A hired hero stands at a town entrance and looks around from there.
    public void Handle(HeroHired @event) => OnHeroRelocated(state.GetHero(@event.Hero), state.GetHero(@event.Hero).Position);

    public void Handle(HeroLeftGarrison @event) => OnHeroRelocated(state.GetHero(@event.Hero), @event.Position);

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
            Reveal(owner, SightCenter(mapObject), flagged.ScoutingRadius);
        }

        // The flag change is news for everyone who can see the object; the owners' sight changes too.
        foreach (var player in state.Players)
        {
            ObserversChanged(player.Id);
        }
    }

    private IEnumerable<(MapObject Object, PlayerId? Owner, int ScoutingRadius)> Flagged() =>
        state.Flaggables.Select(f => ((MapObject)f, f.Owner, f.ScoutingRadius));

    // A town sees from its middle, so the land beyond its walls is known too; other objects from their entrance.
    private static MapPosition SightCenter(MapObject mapObject) => mapObject is Towns.Town town ? town.SightCenter : mapObject.VisitPosition;

    // Only what came into sight is new to the hero's owner: objects already in sight publish their own changes (ObjectMemory.Refresh).
    private void OnHeroRelocated(Hero hero, MapPosition at)
    {
        var revealed = Reveal(hero.Owner, at, hero.ScoutingRadius);
        if (!state.HasPlayer(hero.Owner))
        {
            return;
        }

        var entered = UpdateSight(hero.Owner);
        var changed = state.Memory.Refresh(state, hero.Owner, revealed, entered);
        if (changed.Count > 0)
        {
            events.Publish(new KnownObjectsChanged(hero.Owner, changed));
        }
    }

    private IReadOnlyList<MapPosition> Reveal(PlayerId player, MapPosition center, int radius)
    {
        var revealed = state.Fog.Reveal(player, center, radius);
        if (revealed.Count > 0)
        {
            events.Publish(new TilesRevealed(player, revealed));
        }

        return revealed;
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

    /// <summary>Sight is a pure function of current observers, so recomputing from state is always correct. Returns the tiles that came into sight.</summary>
    private IReadOnlyList<MapPosition> UpdateSight(PlayerId player)
    {
        if (state.Options.FogMode != FogMode.ShroudWithRefog)
        {
            return [];
        }

        var observers = new List<(MapPosition, int)>();
        foreach (var hero in state.HeroesOf(player))
        {
            observers.Add((hero.Position, hero.ScoutingRadius));
        }

        foreach (var flagged in state.Flaggables)
        {
            if (flagged.Owner == player)
            {
                observers.Add((SightCenter((MapObject)flagged), flagged.ScoutingRadius));
            }
        }

        var (entered, left) = state.Fog.UpdateSight(player, observers, state.Options.RefogMargin);
        if (entered.Count > 0 || left.Count > 0)
        {
            events.Publish(new SightChanged(player, entered, left));
        }

        return entered;
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
