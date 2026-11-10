# Event system (custom pub/sub)

All state changes are announced through `EternalWars.Core.Events`: a small, dependency-free, deterministic
publish/subscribe layer.

## Building blocks

| Type | Role |
|---|---|
| `IEvent` | marker for all events |
| `IEventHandler<in T>` | `void Handle(T @event)` |
| `IEventPublisher` | `Publish<T>(T)`: what Domain services depend on |
| `IEventSubscriber` | `IDisposable Subscribe<T>(IEventHandler<T>)`: what views and handlers depend on |
| `IEventBus` / `EventBus` | both; only composition roots (`GameSession`, `MapEditorSession`, app `Main`) see it |
| `subscriber.Subscribe<T>(Action<T>)` | delegate shortcut (`EventSubscriberExtensions`) |
| `SubscriptionGroup` | disposes many subscriptions at once |
| `node.SubscribeWhileInTree<T>(events, handler)` | Godot.Shared; subscribed while the node is in the tree (from now, or when it enters), disposed when it leaves, subscribed again if it comes back; subscribe on a node that goes away with what the handler keeps alive, not on a root that never leaves |

## Semantics (guaranteed, covered by `EventBusTests`)

| Rule | Why |
|---|---|
| **Synchronous**: handlers run inside `Publish`, on the caller's thread. | Turn-based; deterministic. |
| **Subscription order**: handlers run in the order they subscribed. | Determinism; predictable UI layering. |
| **Polymorphic**: a handler for `T` receives every event assignable to `T`; `Subscribe<IEvent>` sees all. | Logging, debug panels. |
| **Breadth-first re-entrancy**: events published by a handler are queued until the current event has reached *all* handlers. | Every listener sees `A` before anything caused by `A`. |
| **(Un)subscribe during dispatch** takes effect from the next event; a handler disposed mid-dispatch is skipped. | Views can unsubscribe themselves. |
| **Fail fast**: a throwing handler aborts dispatch, discards queued events, rethrows to the outermost publisher. | Bugs surface immediately. |
| **Not thread-safe.** | Single-threaded simulation ([architecture §5](architecture.md#5-threading-model)). |
| **No priorities**, on purpose. | If two handlers must be ordered, make one publish a follow-up event. |

## Conventions
- Events are **facts in the past tense** (`HeroMoved`, `DayStarted`). Requests are commands (`GameCommand`, via
  `GameSession.Execute`), never events.
- Events are **immutable `sealed record`s** carrying ids and values, not mutable domain objects (read-only lists of
  values are fine).
- Carry enough for a view to update without querying state when practical (`ResourcesChanged` has `Previous` and
  `Current`).
- **Publish after mutating**: when a handler runs, `GameState` already reflects the event.
- **Core handlers use the event's data, not current state** (`VisibilityService` reveals around `HeroMoved.To`, not
  the hero's position): with breadth-first queuing the state may have moved on by the time they run.
- Core handlers (`VisibilityService`) are subscribed **before** any view, so their follow-ups (`TilesRevealed`) are
  queued before views react to the original event.
- Animation gating (delaying presentation until an animation ends) belongs in a Tier 2 queue (as in `BattleView3D`),
  never in the core bus.

## Where events live
- Game and editor events: `src/Core/EternalWars.Core.Domain/Events/*Events.cs`, one file per area (Turn, GameFlow,
  Economy, Hero, Army, Artifact, Magic, MapObject, Town, Battle, Visibility, Decision, Editor). Template editor:
  `TemplateChanged` in `Core.Generation/TemplateEditorSession.cs`.
- Every event has an XML doc comment saying when it fires and what precedes or follows it; **write one for each new
  event**, with the ADR id. To find the publisher, search for `new EventName(`.

## Ordering guarantees views rely on

| Sequence | Order |
|---|---|
| Any accepted command | its events … → `CommandExecuted` last (command already in `CommandLog`; autosave hooks here) |
| Map object visit | `MapObjectVisited` first, then the outcome events (or `DecisionRequested`) |
| Choice needed | `DecisionRequested` … `DecisionResolved`; a battle's `DecisionResolved(BattleDecision)` comes after `BattleEnded` and its map consequences |
| Battle end | `BattleEnded` → survivors' `ArmyChanged` → `UndeadRaised` → experience / level-up → `DecisionResolved` |
| Player eliminated | its `HeroDefeated`, `TownOwnerChanged`, `MineOwnerChanged` … → `PlayerDefeated` |
| Object replaced in place (same id) | `MapObjectChanged`, then owner events (e.g. `DepositBought` → `MapObjectChanged` → `MineOwnerChanged`) |
| Object gone | `MapObjectRemoved` before the event that explains it (`HeroFreed`, `BorderGuardPassed`, ...) |
| Garrison / army | `GarrisonChanged` and `ArmyChanged` always carry all seven slots |
| Fog | `KnownObjectsChanged`: views re-read `GameState.Memory`; a null entry means "no longer there" |

## Example

```csharp
// Core service: validate, mutate, publish.
hero.Position = target;
hero.MovementPoints -= cost;
events.Publish(new HeroMoved(hero.Id, from, target, hero.MovementPoints));

// Godot view: subscription dropped automatically when the node leaves the tree.
this.SubscribeWhileInTree<HeroMoved>(session.Events, e =>
{
    if (e.Hero == hero.Id)
        CreateTween().TweenProperty(this, "position", MapSpace.ToWorld(e.To), 0.15f);
});

// Core rules reacting to other rules (wired in GameSession, before views).
public sealed class VisibilityService(GameState state, IEventPublisher events)
    : IEventHandler<GameStarted>, IEventHandler<HeroMoved>, ...
{
    public void Handle(HeroMoved e) => RevealAround(state.GetHero(e.Hero), e.To); // publishes TilesRevealed
}
```
