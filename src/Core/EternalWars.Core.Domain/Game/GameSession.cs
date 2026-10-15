using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Composition root of a running game: one state, one bus, the services that mutate the state.
/// Front-ends (3D game, 2D harness, tests, AI) talk to the game exclusively through this object
/// and observe it exclusively through <see cref="Events"/>.
/// </summary>
public sealed class GameSession
{
    public GameSession(GameState state, IEventBus? bus = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Bus = bus ?? new EventBus();

        // Core handlers subscribe first so their follow-up events are queued before views react.
        var visibility = new VisibilityService(State, Bus);
        Bus.Subscribe<GameStarted>(visibility);
        Bus.Subscribe<HeroMoved>(visibility);
        Bus.Subscribe<HeroTeleported>(visibility);
        Bus.Subscribe<MineOwnerChanged>(visibility);

        Economy = new EconomyService(State, Bus);
        Interactions = new MapObjectInteractionService(State, Bus, Economy);
        Movement = new HeroMovementService(State, Bus, Interactions);
        Turns = new TurnService(State, Bus, Economy, Movement);
    }

    public GameState State { get; }

    public IEventSubscriber Events => Bus;

    public EconomyService Economy { get; }

    public MapObjectInteractionService Interactions { get; }

    public HeroMovementService Movement { get; }

    public TurnService Turns { get; }

    private IEventBus Bus { get; }
}
