using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>One paid adventure spell cast: who casts what at which rank and where, and what the cast works with.</summary>
internal sealed class AdventureCast(
    GameState state, IEventPublisher events, Visibility.IFogRevealer fog, TownVisitService towns, Hero hero, Common.SpellId spell, int rank, MapPosition? target)
{
    public GameState State { get; } = state;

    public IEventPublisher Events { get; } = events;

    public Visibility.IFogRevealer Fog { get; } = fog;

    public TownVisitService Towns { get; } = towns;

    public Hero Hero { get; } = hero;

    public Common.SpellId Spell { get; } = spell;

    public int Rank { get; } = rank;

    public MapPosition? Target { get; } = target;

    /// <summary>The hero goes to <paramref name="to"/> at once.</summary>
    public void Relocate(MapPosition to)
    {
        var from = Hero.Position;
        Hero.Position = to;
        Events.Publish(new HeroTeleported(Hero.Id, from, to));
    }

    /// <summary>The hero gets the status.</summary>
    public void Mark(HeroStatus status)
    {
        Hero.AddStatus(status);
        Events.Publish(new HeroStatusGained(Hero.Id, status));
    }
}
