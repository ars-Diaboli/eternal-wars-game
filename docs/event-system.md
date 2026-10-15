# Event system (custom pub/sub)

All game logic communicates state changes through a small, purpose-built publish/subscribe layer in
`EternalWars.Core.Events`. It has no dependencies (not even on DI or Rx) and is fully deterministic.

## Building blocks

```csharp
public interface IEvent;                                        // marker for all events

public interface IEventHandler<in TEvent> where TEvent : IEvent // reacts to one kind of event
{
    void Handle(TEvent @event);
}

public interface IEventPublisher                                // what services depend on
{
    void Publish<TEvent>(TEvent @event) where TEvent : IEvent;
}

public interface IEventSubscriber                               // what views/handlers depend on
{
    IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
}

public interface IEventBus : IEventPublisher, IEventSubscriber;
public sealed class EventBus : IEventBus { ... }                // the implementation
```

Helpers:
- `subscriber.Subscribe<T>(Action<T>)`: delegate shortcut.
- `SubscriptionGroup`: collects tokens and disposes them all at once.
- `node.SubscribeWhileInTree<T>(events, handler)` (Godot.Shared): ties a subscription to a node's lifetime.

## Semantics (guaranteed, covered by `EventBusTests`)

| Rule | Why |
|---|---|
| **Synchronous**: handlers run inside `Publish`, on the caller's thread. | Turn-based; simplest mental model; deterministic. |
| **Subscription order**: handlers run in the order they subscribed. | Determinism; predictable UI layering. |
| **Polymorphic**: a handler for `T` receives every event assignable to `T`. `Subscribe<IEvent>` sees everything. | Logging, replays, debug panels. |
| **Breadth-first re-entrancy**: events published by a handler are queued and run after the current event has reached *all* handlers. | Every listener sees `A` before anything caused by `A`; no half-updated observers. |
| **Safe (un)subscribe during dispatch**: takes effect from the next event. A handler disposed mid-dispatch is skipped. | Views can unsubscribe themselves; no "collection modified" errors. |
| **Fail fast**: a throwing handler aborts dispatch, discards queued events and rethrows to the outermost publisher. | Bugs surface immediately instead of corrupting later turns. |
| **Not thread-safe.** | The simulation runs on one thread (see architecture §5). |

## Conventions

- Events are **facts in the past tense**: `HeroMoved`, `DayStarted`, `ResourcesChanged`. Requests
  ("please move") are not events; they are service calls today and commands later.
- Events are **immutable `sealed record`s** carrying ids and values, not mutable domain objects.
  Exception: read-only lists of value types (`TerrainPainted.Positions`).
- Include enough data for a view to update **without** querying state when practical (e.g.
  `ResourcesChanged` carries both `Previous` and `Current`, so UI can animate the delta).
- **Publish after mutating.** By the time a handler runs, `GameState` already reflects the event.
- Group events per area in `Core.Domain/Events/` (`TurnEvents`, `EconomyEvents`, `HeroEvents`,
  `MapObjectEvents`, `VisibilityEvents`, `DecisionEvents`, `EditorEvents`).
- **Core handlers that react to events use the event's data, not current state** (e.g.
  `VisibilityService` reveals around `HeroMoved.To`, not the hero's position). When a move is made
  from inside another handler, its events are queued and delivered after the hero has walked on.
- Core handlers are subscribed in the composition root **before** any view, so their follow-up
  events (e.g. `TilesRevealed`) are queued before views react to the original event.
- Domain services take `IEventPublisher`; views take `IEventSubscriber`. Only composition roots
  (`GameSession`, app `Main`) see `IEventBus`.

## Example

```csharp
// Core service: validate, mutate, publish.
hero.Position = target;
hero.MovementPoints -= cost;
events.Publish(new HeroMoved(hero.Id, from, target, hero.MovementPoints));

// Godot view (Tier 2): react; subscription is dropped automatically when the node is freed.
this.SubscribeWhileInTree<HeroMoved>(session.Events, e =>
{
    if (e.Hero == hero.Id)
        CreateTween().TweenProperty(this, "position", MapSpace.ToWorld(e.To), 0.15f);
});

// Dedicated handler class: core rules reacting to other rules. Real example: fog of war.
public sealed class VisibilityService(GameState state, IEventPublisher events)
    : IEventHandler<GameStarted>, IEventHandler<HeroMoved>, IEventHandler<HeroTeleported>, IEventHandler<MineOwnerChanged>
{
    public void Handle(HeroMoved e) => RevealAround(state.GetHero(e.Hero), e.To); // publishes TilesRevealed
    ...
}
bus.Subscribe<HeroMoved>(visibility); // wired in GameSession
```

## Current event catalogue

| Event | Published by | When |
|---|---|---|
| `GameStarted(Date)` | `TurnService.StartGame` | once |
| `DayStarted(Date)` | `TurnService` | game start and after the last player ends a turn |
| `WeekStarted(Date)` | `TurnService` | day 8, 15, ... |
| `TurnStarted(Player, Date)` / `TurnEnded(...)` | `TurnService` | each player turn |
| `ResourcesChanged(Player, Previous, Current)` | `EconomyService` | any resource change |
| `HeroMoved(Hero, From, To, RemainingMP)` | `HeroMovementService` | each successful step (a `MoveTo` publishes one per step) |
| `HeroTeleported(Hero, From, To)` | `MapObjectInteractionService` | a gate moved the hero |
| `HeroMovementRestored(Hero, MP)` | `HeroMovementService` | owner's turn start |
| `HeroExperienceGained(Hero, Amount, Total)` | `MapObjectInteractionService` | chest reward taken as XP |
| `MapObjectVisited(Hero, Object)` | `MapObjectInteractionService` | hero stepped onto an entrance (before the outcome events) |
| `ResourcePileCollected(Hero, Object, Resource, Amount)` | `MapObjectInteractionService` | pile picked up |
| `MineOwnerChanged(Mine, Previous, Current)` | interactions / editor | mine flagged |
| `MapObjectRemoved(Object)` | interactions / editor | pile/chest consumed, or deleted in editor |
| `MapObjectPlaced(Object, DefinitionId, Position)` | editor | placed or restored by undo |
| `GateLinkChanged(Gate, LinkedGate)` | editor | gate (un)linked |
| `DecisionRequested(Decision)` / `DecisionResolved(Decision)` | `MapObjectInteractionService` | e.g. chest gold-or-XP choice |
| `TilesRevealed(Player, Positions)` | `VisibilityService` | newly explored tiles only |
| `SightChanged(Player, Entered, Left)` | `VisibilityService` | `ShroudWithRefog` only: tiles entering/leaving live sight (show/hide units) |
| `TerrainPainted(Positions, Terrain)` | `MapEditorSession` | paint and its undo/redo |
| `HeroPlaced` / `HeroRemoved` | `MapEditorSession` | editor hero placement |
| `MapPropertiesChanged(Name, Description)` | `MapEditorSession` | |
| `PlayerSlotsChanged` | `MapEditorSession` | slot added/removed/edited |
| `UndoHistoryChanged(UndoDescription, RedoDescription)` | `MapEditorSession` | after every recorded edit, undo or redo |

## Future extensions (only when needed)
- **Animation gating**: front-ends may want to delay presenting events until an animation ends.
  This belongs in a presentation-side queue (Tier 2) that buffers bus events, never in the core bus.
- **Event log / replay**: a `Subscribe<IEvent>` recorder feeding saves and debugging tools.
- **Priorities**: not supported on purpose; if ordering matters between two handlers, make one
  publish a follow-up event.
