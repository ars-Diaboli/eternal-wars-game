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
| **Not thread-safe.** | The simulation runs on one thread (see [architecture §5](architecture.md#5-threading-model)). |

## Conventions

- Events are **facts in the past tense**: `HeroMoved`, `DayStarted`, `ResourcesChanged`. Requests
  ("please move") are not events; they are commands (`GameCommand`, executed by `GameSession.Execute`).
- Events are **immutable `sealed record`s** carrying ids and values, not mutable domain objects.
  Exception: read-only lists of value types (`TerrainPainted.Positions`).
- Include enough data for a view to update **without** querying state when practical (e.g.
  `ResourcesChanged` carries both `Previous` and `Current`, so UI can animate the delta).
- **Publish after mutating.** By the time a handler runs, `GameState` already reflects the event.
- Group events per area in `Core.Domain/Events/` (`TurnEvents`, `GameFlowEvents`, `EconomyEvents`, `HeroEvents`,
  `ArmyEvents`, `ArtifactEvents`, `MapObjectEvents`, `TownEvents`, `BattleEvents`, `VisibilityEvents`, `DecisionEvents`,
  `EditorEvents`).
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
    : IEventHandler<GameStarted>, IEventHandler<HeroMoved>, IEventHandler<HeroTeleported>, IEventHandler<MineOwnerChanged>, ...
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
| `PlayerDefeated(Player, Reason)` | `GameFlowService` | a player is eliminated; its heroes (`HeroDefeated`) and flags (`TownOwnerChanged`, `MineOwnerChanged`) were released just before |
| `GracePeriodStarted(Player, DefeatDay)` / `GracePeriodEnded(Player)` | `GameFlowService` | a player that had a town lost its last one (countdown begins) / owns a town again |
| `CommandExecuted(Command)` | `GameSession` | last event of every accepted command, after it is logged and settled; the state matches `CommandLog` (autosave hooks here) |
| `GameEnded(Winners, Reason, Date)` | `GameFlowService` | a team won (or nobody did); commands are refused from now on |
| `ResourcesChanged(Player, Previous, Current)` | `EconomyService` | any resource change |
| `ResourcesTraded(Player, Resource, Direction, Amount, Gold, Marketplace)` | `EconomyService` | a marketplace trade (ADR-067), just after its `ResourcesChanged`; `Marketplace` is the map marketplace used, null for the player's towns |
| `HeroMoved(Hero, From, To, RemainingMP)` | `HeroMovementService` | each successful step (a `MoveTo` publishes one per step) |
| `HeroTeleported(Hero, From, To)` | `MapObjectInteractionService` | a gate moved the hero |
| `HeroMovementRestored(Hero, MP)` | `HeroMovementService` | owner's turn start |
| `HeroManaRestored(Hero, Mana, MaxMana)` | `TurnService` | owner's turn start, daily mana (ADR-063); not published when the pool is full |
| `HeroExperienceGained(Hero, Amount, Total)` | `ProgressionService` | chest reward taken as XP, battle won |
| `HeroStatusGained(Hero, Status)` / `HeroStatusesExpired(Hero, Statuses)` | `ProgressionService` (granted by map objects; expired by `BattleService` after the hero's battle, `TurnService` at the owner's turn start and when a week starts) | temporary morale, luck or daily movement from a map object (ADR-068) |
| `HeroMovementGained(Hero, Amount, MovementPoints)` | `MapObjectInteractionService` | a buff site added movement points for today (ADR-068) |
| `HeroPrimaryStatRaised(Hero, Stat, Amount, Value)` | `ProgressionService` | a training site raised a primary stat (Arena, Colosseum, Library; ADR-068) |
| `HeroLeveledUp(Hero, Level, Skill, SkillLevel, Stat, StatValue)` | `ProgressionService` | a level-up choice applied: the skill picked (null: none) and the primary stat raised |
| `WarMachineBought(Town, Hero, Slot)` | `TownService` | the blacksmith sold a war machine to the visiting hero (ADR-056) |
| `WarMachineLost(Hero, Slot)` | `BattleService` | a hero's war machine was destroyed in battle |
| `MapObjectVisited(Hero, Object)` | `MapObjectInteractionService` | hero stepped onto an entrance (before the outcome events) |
| `MapObjectFoundEmpty(Hero, Object)` | `MapObjectInteractionService` | a hero visited an object whose reward was already taken (this week, by this hero, or by the first visitor; ADR-068) |
| `ResourcesFound(Hero, Object, Resources)` | `MapObjectInteractionService` | a resource provider paid out (after `ResourcesChanged`); for a warehouse only once the visitor chose (`ChooseResourceRewardCommand`) |
| `MapObjectRefused(Hero, Object, Reason)` | `MapObjectInteractionService` | an object turned the hero away with a reason to show (Library below level 10, a price nobody can pay; ADR-068) |
| `LootFound(Hero, Object, Resources, Artifact)` | `MapObjectInteractionService` | the first visitor searched a loot site, or a chest held an artifact (ADR-068); either part may be empty; vanishing sites were removed just before |
| `MapObjectCollected(Hero, Object)` | `MapObjectInteractionService.TryCollect` | a hero took the reward of an object with a limited visit scope; its `Visits` log changed (ADR-068), the reward's events follow |
| `ResourcePileCollected(Hero, Object, Resource, Amount)` | `MapObjectInteractionService` | pile picked up |
| `MineOwnerChanged(Mine, Previous, Current)` | interactions, `BattleService`, `GameFlowService`, editor | mine flagged (after its guard was beaten), released by a defeated player, or set in the editor |
| `DwellingOwnerChanged(Dwelling, Previous, Current)` | interactions, `GameFlowService`, editor | a map dwelling flagged, released by a defeated player, or set in the editor |
| `DwellingCreaturesGrew(Dwelling, Available)` | `TownService` | a map dwelling's weekly growth arrived (after `WeekStarted`) |
| `DwellingRecruited(Dwelling, Hero, Creature, Count, Available)` | interactions, `CaravanService` | recruited by a visiting hero, or ordered for a caravan (Hero null) |
| `TownOwnerChanged(Town, Previous, Current)` | `TownService.Capture` (from interactions, `BattleService`), `GameFlowService`, editor | town captured (empty garrison walked into, or garrison beaten), released by a defeated player, or set in the editor |
| `GovernorChanged(Town, Previous, Current)` | `TownService` | a town's governor set or cleared (ADR-063); a hero governing another town leaves it first |
| `TownBuilt(Town, Building, Level)` / `DwellingBuilt(Town, Line, Level, Available)` | `TownService` | a building level or a dwelling (1 built, 2 upgraded) |
| `CreaturesRecruited(Town, Creature, Count, Hero, Available)` / `StackUpgraded(Town, Hero, Slot, From, To, Count)` | `TownService` | recruits into the garrison (Hero null) or the visiting hero; a stack upgraded for the price difference |
| `GarrisonChanged(Town, Slots)` | `TownService`, `BattleService`, `CaravanService`, editor | all seven garrison slots after any change |
| `TownCreaturesGrew(Town, Available)` | `TownService` | a new week's growth arrived (after `WeekStarted`) |
| `CaravanDispatched(Caravan, Owner, Source, Destination, Creature, Count, Days)` | `CaravanService` | a caravan left a map dwelling or a town (ADR-054) |
| `CaravanArrived(Caravan, Destination, Creature, Count)` | `CaravanService` | owner's turn start: the troops joined the destination garrison (`GarrisonChanged` just before) |
| `CaravanReturned(Caravan, Source, Creature, Count)` | `CaravanService` | the route was blocked: troops back at the source (a dwelling refunds the price) |
| `CaravanLost(Caravan, Destination, Creature, Count, Reason)` | `CaravanService` | the destination was lost, or the way back was closed |
| `HeroHired(Town, Hero, Definition)` | `TownService` | a hero was hired in a tavern and stands at the town entrance |
| `ArtifactEquipped(Hero, Artifact, Slot)` / `ArtifactUnequipped(Hero, Artifact)` | `ArtifactService` | an artifact put on from, or taken off into, the backpack |
| `ArtifactGiven(From, To, Artifact)` | `ArtifactService` | an artifact changed hands during a hero exchange |
| `ArtifactGained(Hero, Artifact, Source)` / `ArtifactLost(Hero, Artifact)` / `ArtifactBackpackFull(Hero, Artifact)` | `ArtifactService` (pickups, chests and loot sites from interactions, loot from `BattleService`) | picked up, found or looted; loot that did not fit; a pickup left (or a found artifact lost) because the backpack is full |
| `HeroArtifactsChanged(Hero)` | editor | the editor changed what a hero wears and carries |
| `MapObjectRemoved(Object)` | interactions, `GuardService`, `BattleService`, editor | pile/chest consumed, guard joined, fled or beaten, or deleted in editor |
| `MapObjectPlaced(Object, DefinitionId, Position)` | editor | placed or restored by undo |
| `ScenarioRulesChanged` | editor | the victory and defeat conditions changed (views re-read `GameState.Rules`) |
| `GateLinkChanged(Gate, LinkedGate)` | editor | two-way partner or one-way exit changed |
| `DecisionRequested(Decision)` / `DecisionResolved(Decision)` | interactions, `GuardService`, `BattleService`, `ProgressionService` | chest, warehouse resource choice, training choice, exchange, guard offer, battle (resolved when it ends), level-up |
| `TilesRevealed(Player, Positions)` | `VisibilityService` | newly explored tiles only |
| `SightChanged(Player, Entered, Left)` | `VisibilityService` | `ShroudWithRefog` only: tiles entering/leaving live sight (show/hide units) |
| `KnownObjectsChanged(Player, Objects)` | `VisibilityService` | a player's last-known view of objects changed (seen, changed, or seen gone); owned mines and towns observe like heroes |
| `HeroesMet(Hero, Other)` | `MapObjectInteractionService` | own heroes met; `HeroExchangeDecision` follows |
| `BattleTriggered(Attacker, Defender, IsAmbush)` | `MapObjectInteractionService` | hero stepped onto an enemy hero; the battle follows |
| `BattleStarted(AttackerHero, DefenderHero, Guard, Width, Height, IsAmbush, Units, Obstacles)` | `BattleService` | battlefield set up (after the width choice) |
| `BattleRoundStarted(Round)` / `BattleTurnStarted(Unit, Round)` | `BattleEngine` | new round / a unit's turn |
| `BattleUnitMoved(Unit, From, To)` | `BattleEngine` | a unit moved (also before a melee attack) |
| `BattleUnitAttacked(Attacker, Target, Kind, Damage, Killed, RemainingCount, Luck)` | `BattleEngine` | each hit: melee, ranged, retaliation, ranged retaliation, area; `Luck` says whether luck changed the damage |
| `BattleMoraleTriggered(Unit, Effect)` | `BattleEngine` | morale fired: haste or slow at round start, an extra action, or a lost turn |
| `BattleUnitHealed(Unit, Healed, TopHealth)` | `BattleEngine` | the healing tent healed a stack at round start |
| `BattleRageGained(Unit, Attack)` | `BattleEngine` | Blood Rage: a stack that killed gained +1 Attack (total `Attack`, ADR-067) |
| `BattleWarcryUsed(Side, Warcry, Affected)` / `BattleUnitTerrified(Unit)` | `BattleEngine` | a hero's warcry (Rally, Intimidate, Terror); Terror made a stack lose its action when its turn came |
| `BattleUnitDied(Unit)` / `BattleUnitWaited(Unit)` / `BattleUnitDefended(Unit)` | `BattleEngine` | |
| `BattleEnded(Winner, Reason, Casualties, Experience)` | `BattleService` | before its map consequences; `DecisionResolved(BattleDecision)` comes last |
| `ArmyChanged(Hero, Slots)` | `ArmyService` (also for `BattleService` casualties), `GuardService`, `TownService`, `MapObjectInteractionService`, editor | stacks moved, casualties, neutrals joined, recruits and upgrades in a town or at a map dwelling, editor edit |
| `HeroDefeated(Hero, Owner, Retreated)` | `BattleService`, `GameFlowService` | a hero lost or fled a battle and left the map, or its player was eliminated |
| `GuardJoined(Hero, Guard, Creature, Count)` / `GuardFled(Hero, Guard)` | `GuardService` | a guard offer was accepted (then `MapObjectRemoved`) |
| `MineGuardDefeated(Mine)` | `BattleService` | the mine can now be flagged (`MineOwnerChanged` follows) |
| `MapObjectChanged(Object)` | editor, `BattleService` | per-instance data changed (monster incl. its guard mode, mine guard, chest values) |
| `RoadsPainted(Positions, Road)` | `MapEditorSession` | road paint/erase and its undo/redo |
| `TerrainPainted(Positions, Terrain)` | `MapEditorSession` | paint and its undo/redo |
| `HeroPlaced` / `HeroRemoved` | `MapEditorSession`; `GameFlowService` (`HeroRemoved` only) | editor hero placement; a surplus hero removed when the game starts (ADR-068) |
| `MapPropertiesChanged(Name, Description)` | `MapEditorSession` | |
| `HeroStatsChanged(Hero, Attack, Defense, Tactics)` | `MapEditorSession` | (the map-wide `GuardModeChanged` is gone: a monster's guard mode is per stack and reported by `MapObjectChanged`, ADR-065) |
| `PlayerSlotsChanged` | `MapEditorSession` | slot added/removed/edited |
| `UndoHistoryChanged(UndoDescription, RedoDescription)` | `MapEditorSession` | after every recorded edit, undo or redo |

## Future extensions (only when needed)
- **Animation gating**: front-ends may want to delay presenting events until an animation ends.
  This belongs in a presentation-side queue (Tier 2) that buffers bus events, never in the core bus.
- **Event log / replay**: a `Subscribe<IEvent>` recorder feeding saves and debugging tools.
- **Priorities**: not supported on purpose; if ordering matters between two handlers, make one
  publish a follow-up event.
