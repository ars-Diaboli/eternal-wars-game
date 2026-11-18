# Eternal Wars codebase review and refactoring plan

The architecture is in good shape. What needs work is performance and the networking code, not the design.

I read about 73k lines of source and 34k lines of tests (four reviewers working in parallel). The build is clean; the only warnings are 3 xUnit analyzer warnings in tests, and there are no TODO/FIXME markers. I checked the most serious claims myself in the code and all of them held. No profiling was done, so every cost below is an estimate from reading the code.

## Overall verdict

**What is solid and should be kept:**
- **Layering:** Core has no Godot dependency, and the dependency table is enforced by the build and by `ArchitectureTests`.
- **Commands:** everything goes through `session.Execute`, with one handler per command type.
- **Refusals:** `CheckRefusals` verifies through the state digest that a refused command changed nothing.
- **Turn phases:** `ITurnPhaseParticipant` fixes the order explicitly.
- **Table dispatch:** map object visits and battle spell effects use handler tables with tests that check completeness.
- **Value types:** `MapPosition` and the other position types are `readonly record struct`.
- **Cached settings:** JSON options are created once, not per call.
- **Views:** `RefreshScheduler` and `SubscribeWhileInTree` work correctly.

**Where it falls short:**
- **Not enough lookup indexes:** core queries that run per tile or per step are linear scans that also allocate.
- **Work repeated instead of reused:** pathfinding, battle reach searches, sending network views.
- **Very large classes:** a few classes far outgrew the clean extension patterns used elsewhere.
- **The game app's `Main`:** it has taken on policy that belongs in Core.

## Main findings

### Performance hot paths (all confirmed)

| # | Where | Problem |
|---|---|---|
| P1 | `PathFinder.cs:69-74` | Each search allocates 3 arrays the size of the map. On a 144² map with 2 levels that is about 330 KB each, which goes on the large-object heap. The AI runs one search per target. The baseline measured 58–78 searches per turn on average and about 210 at most, at 1.8–5 ms each. |
| P2 | `MoveResult.cs:122` `ArmyTown` | A `Select/Distinct/ToList` chain runs for every edge expanded by every search. |
| P3 | `GameState.cs:272, 444-451` | `HeroAt` scans every hero. `IsGuarded` runs `Select/OfType/OrderBy` and then `.Any()`. Both run for every pathfinding node. |
| P4 | `PathFinder.cs:76` | One teleporter anywhere on the map turns off A*, so every search becomes a full Dijkstra over the map. |
| P5 | `ObjectMemory.cs:65-89` | On every hero step, each player's memory of map objects is rebuilt at O(objects × cells), with a nested scan through `Objects.Any`. |
| P6 | `BattleGrid.cs:200`, `BattleMagic.cs:502,582` | The same reach search runs about 2E+A+2 times per battle AI action (E = enemy stacks, A = allied stacks). A new `Clearance` grid is built for each candidate cell. |
| P7 | `BattleEstimator.cs:66-91` | Each battle estimate builds 3 complete `GameSession`s. Its cache is cleared every turn and keyed by an interpolated string. |
| P8 | `GameHost.cs:291` → `PlayerViewFeed.Put` | Every frame, it writes each client's whole view through reflection and compares the JSON text, even when nothing changed. |
| P9 | `EventBus.cs:61-73` | Each `Publish` checks every subscription with `IsAssignableFrom`. |
| P10 | `MapViewContext.cs:132` | Changing the level raises the same `Changed` event as changing the viewer, so every level's object view and fog view is rebuilt from scratch. |
| P11 | `MapObjectsView.cs:191-213` | Every map object shape gets its own new mesh and `StandardMaterial3D`, with no batching. |
| P12 | Map editor `AdventureMapView.cs:71-89` | Each mouse move during a paint or height stroke rebuilds the whole splat map image or the whole ground mesh. |
| P13 | `TurnTimerPanel`, `BattleInputController` | The timer label is rebuilt as a string every frame. Battle hover runs the targeting check and rebuilds the label text on every mouse motion. |

### Correctness and robustness

- **`PlayerViewFeed.Shows` defaults to "show" (`:181`).** An event without a hero, object or owner property goes to every client. A new private event would leak to opponents without anyone noticing. **Fixed in Phase 0 (ADR-202).**
- **No limits before a client has joined (`Connections.cs:136-141`).** A peer can claim a 64 MB frame and the host allocates the buffer before the data arrives. There is no handshake timeout and no cap on peers that have not joined. **Fixed in Phase 0 (ADR-203).**
- **Reader task can fault silently (`ReadAsync`).** It catches only a few exception types, so a bad message can stop the reader without closing the connection, leaving a seat that is never read. **Fixed in Phase 0 (ADR-203).**
- **State the digest misses (`GameFlowService.cs:19-21`).** It keeps the state the scenario rules use (the heroes and prisoners at start, the teams at start) outside `GameState`. That state is not in the digest, the LAN view or AI copies of the state. **Fixed in Phase 3 (ADR-213).**
- **Publish before mutate (`TurnService.cs:75-80`).** `TurnEnded` is published before the turn state changes, which breaks the mutate-then-publish rule. **Fixed in Phase 3 (ADR-214).**
- **Events used as commands.** `AreaRevealed` is published so that `VisibilityService` will change the fog, which only works because visibility subscribes first. **Fixed in Phase 3 (ADR-214)**, with `SurroundingsRevealed`, which was used the same way.
- **Hand-kept command list.** No test checks that every `GameCommand` appears in `GameRecordSerializer.CommandTypes`, so a missing entry only shows up at runtime, on a save or over LAN. **Fixed in Phase 0 (`RecordCommandTypesTests`).**

### SOLID and code smells

- **God classes:**
  - `AdventureAi` (1783 lines).
  - `SpellEffect.Parse` (one 473-line method with 58 cases).
  - `MapEventService` (863 lines, 8 dependencies, a type switch over about 25 action types).
  - `GameHost` (612 lines).
  - `TownPanel`, `InspectorPanel` and `EventsPanel` rebuild everything on any event.
  - The game app's `Main` partials (about 2k lines) hold timer and AI gating, seat ownership, LAN announce rules and replay stepping. That breaks the rule that Apps hold composition and input mapping only.
- **Switches where the rest of the code uses handler tables:** adventure spells (`AdventureMagic.Cast`), `ScenarioCondition.Kind` (switched on in 6 places), the `MapEventService` action switch, and `MapValidator`.
- **Duplication:** setting a `PendingDecision` and publishing `DecisionRequested` is written by hand in 5 services. `new DecisionResolved` appears 15 times. The nine decision resolvers repeat the same take/clear/publish block.
- **Magic numbers:** AI weights such as 800+80×, the 3000 gold cushion and the 0.08 distance decay are written inline and copied.
- **Smaller issues:**
  - Content ids (creatures, spells, artifacts, buildings) are plain strings.
  - `InfoSheets` builds English text inside the domain, which blocks localisation.
  - `CheckRefusals` is a process-wide static flag.
  - Two public mutable collections: `Cartographer.Buyers` and `ArtifactMerchant.Stock`.

## Refactoring plan

The order is: safety first, then a measurement baseline, then cheap performance wins, then structural work. Each step can ship on its own behind the existing tests.

### Phase 0: Guardrails (about 1–2 days)
- [x] **Event visibility:** make every event type declare whether clients may see it (an attribute or an explicit table), with a test that fails on any event without one.
  - Done (ADR-202). Each event carries `[SeenBy(Audience.…)]`: `Everyone`, `Owner`, `Witnesses`, `BattleSides`, `Special` or `Nobody`. `PlayerViewFeed.Shows` reads only the attribute, and an event without one reaches no client.
  - `EventAudienceTests` checks that every event declares an audience, that `Owner` and `Witnesses` events name what they need, and that the `Special` events match `ShowsSpecial`.
  - The map-script events (`MapVariableChanged`, `MapEventEnabledChanged`, `MapEventCellRemoved`, `MapEventRan`) no longer go to every client.
- [x] **Command list test:** add a test that every `GameCommand` subclass appears in `GameRecordSerializer.CommandTypes`.
  - Done. `RecordCommandTypesTests` checks that every concrete command is listed, that only commands are listed, and that each type and each name appears once.
- [x] **Network limits:** about 64 KB per frame until a join is accepted, a handshake timeout of about 10 s, a cap on peers that have not joined, a cap on messages handled per `Poll`, `catch (Exception)` then `Close` in the reader, and a rate limit on `RecordRequest`.
  - Done (ADR-203, `NetLimits`), the same for `LobbyHost` and `GameHost`:
    - a client's messages are at most 64 KB for the link's whole life, with at most 256 waiting;
    - a peer must join within 10 s;
    - at most 8 peers wait to join at once;
    - a `Poll` handles at most 16 messages per link;
    - a client gets the record at most every 5 s;
    - the reader closes the link on any exception.
  - The hosts read the time through an internal `TimeProvider`, so tests can move it by hand. `NetLimitsTests` covers each limit.
- [x] **Baseline:** add `FindPath` call counters and per-turn timing to `sim game`. Record a baseline on a 144² map with 2 levels and teleporters, and on a battle-heavy seed.
  - Done. `PathFinder.Counters` (per thread) counts searches, routes found, tiles expanded and search time. `sim game --profile` prints them with each AI turn's time and battles, per player, per week and for the slowest turns (`development.md`).
  - The counts are the same on every run of a seed, so after a change they show whether the AI still makes the same searches. Times are wall clock (Release build, this machine) and vary by about 10 %.
  - **Big map:** `sim game --template ffa-8 --size 144 --players 8 --days 28 --seed 1 --profile`, giving 144², 2 levels, 2 teleporters (so no A* distance estimate), digest `B96E797972B4EDFC`.

    | Measure | Value |
    |---|---|
    | Turns | 224, at 391 ms a turn (median 234 ms) |
    | Path searches | 12,972 (58 a turn), 12,945 found |
    | Tiles expanded | 6,315,863 (487 a search) |
    | Search time | 65.3 s, which is 75 % of the turn time |
    | Battles | 12 in 133 ms |
    | Week 1 → week 4 | 12.0 s → 31.7 s (searches 2.7k → 3.8k, tiles 0.36 M → 3.2 M) |
    | Slowest turn | 1.5 s (day 28, Player 8: 127 searches, 165k tiles, 1.25 s searching) |

  - **Battle-heavy seed:** `sim game --template duel --size 72 --days 40 --seed 2 --profile`, giving 72², 1 level, no teleporters, 24 battles, won by conquest on day 37, digest `02E607C4B08FDC80`.

    | Measure | Value |
    |---|---|
    | Turns | 73, at 263 ms a turn (median 246 ms) |
    | Path searches | 5,680 (78 a turn), 5,593 found |
    | Tiles expanded | 1,516,286 (267 a search) |
    | Search time | 10.1 s, which is 53 % of the turn time |
    | Battles | 24 in 523 ms |
    | Slowest turn | 882 ms (day 2, Player 2: 108 searches, only 7.4k tiles in 329 ms, 1 battle in 116 ms) |

  - What the baseline shows:
    - Path search is the largest single cost: 75 % of turn time on the big map and 53 % on the duel.
    - A search costs about 5 ms on the big map and 1.8 ms on the duel, and short searches are expensive too. So the cost of each search and each tile (P1–P3) matters as much as the number of tiles (P4).
    - The AI makes far fewer searches than P1 estimated: 58–78 a turn on average and about 210 at most, not 1–4k.
    - Battles themselves are cheap: 5–20 ms each with auto-resolve. The time the AI spends estimating battles (P7) falls under the rest of the turn and is not measured separately.

### Phase 1: Cheap performance wins (about 1 week; local changes, behaviour unchanged, digests unchanged)
- [x] **`GameState` indexes:** `Dictionary<MapPosition, Hero>`, a guarded-tile `bool[]` per level, per-type object buckets for `ObjectsOf<T>`, and a cached garrison list. `IsGuarded` becomes an allocation-free loop.
  - Done (P3). None of the indexes is saved or part of the digest:
    - `HeroAt` reads a tile → hero dictionary. Setting any `Hero.Position` bumps the static `Hero.Moves` counter, and adding or removing a hero marks the dictionary stale. The dictionary is rebuilt on the next lookup after either. Heroes don't know their game, and tests and scenario builders set `Position` directly, so a counter is safer than updating the index on every move. When two heroes share a tile, the first in `Heroes` still wins.
    - `IsGuarded` reads one flat `int[]` over all levels. Each entry counts the monster cells on the tile's eight neighbours, so removing a monster can subtract its cells; a `bool[]` couldn't. `AddObject` and `RemoveObject` keep it up to date. `GuardsOf` returns at once on an unguarded tile.
    - `ObjectsOf<T>` returns a cached read-only list, in `Objects` order and including subtypes. Adding or removing an object drops the cached lists of every type it belongs to.
    - The towns, sorted by id, are cached for `GarrisonHeroes`, `GetHero` and the owner of a pending decision. The cache is dropped when a town is added or removed.
  - `GameStateIndexTests` checks every lookup against a plain scan as heroes move, join and leave and as objects are added, removed and put back.
  - The baseline games give the same digests and the same search counts. Times changed as follows (one Release run each):

    | Game | Turn (mean / median) | Search time |
    |---|---|---|
    | Big map | 391 → 294 ms / 234 → 171 ms | 65.3 → 49.7 s |
    | Duel | 263 → 214 ms / 246 → 172 ms | 10.1 → 7.9 s |
- [x] **`PathFinder` buffers:** pooled arrays plus a generation stamp instead of `closed`, a reused `PriorityQueue`, a per-terrain cost table built once per search (replaces `ArmyTown` per edge), and a cached teleporter list.
  - Done (P1, P2):
    - Each thread keeps one set of search buffers: the cost and parent arrays, two generation-stamp arrays (reached and closed) and the `PriorityQueue`. A new search bumps the generation instead of clearing the arrays. The arrays grow when a bigger map needs them, and a search started inside another gets its own set.
    - `MovementRules.StepCost` takes an optional terrain cost table. The search remembers the hero's cost for each terrain type the first time it needs it, so `ArmyTown` and the skill and artifact checks run once per terrain per search instead of on every step. `ArmyTown` itself is now a loop that allocates nothing.
    - `GameState.HasTeleporters` checks the cached lists of the two gate types instead of every object. A test fails if another type starts to override `TeleportTarget`.
  - `PathFinderTests` checks that searches reusing a thread's buffers return the same routes as a thread that has never searched, across heroes with different terrain costs and maps of different sizes. `GameStateIndexTests` checks that `HasTeleporters` follows gate links as they are set and cleared.
  - The baseline games give the same digests and the same search counts. Times changed as follows (one Release run each, after the `GameState` indexes above):

    | Game | Turn (mean / median) | Search time |
    |---|---|---|
    | Big map | 294 → 98 ms / 171 → 44 ms | 49.7 → 4.7 s (22 % of turn time, down from 75 %) |
    | Duel | 214 → 150 ms / 172 → 120 ms | 7.9 → 1.9 s (17 % of turn time, down from 50 %) |

    On the big map, a run with the terrain table switched off took 20.2 s of search time. So the buffers, the allocation-free `ArmyTown` and the teleporter check together saved about 29 s, and the terrain table saved the other 15 s.
- [x] **Event bus:** cache `Dictionary<Type, Subscription[]>` per concrete event type, cleared when subscriptions change.
  - Done (P9). `Dispatch` looks up the event's concrete type. The first event of a type collects the matching subscriptions in subscription order, so later events of that type skip the `IsAssignableFrom` checks. Subscribing or unsubscribing clears the whole cache. The cached arrays are never changed in place, so handlers can subscribe and unsubscribe during dispatch as before. A new subscription applies from the next event, and a handler unsubscribed during dispatch is skipped at once.
  - `EventBusTests` adds two tests: handlers are worked out again after each subscription change, and a handler subscribed during dispatch receives the events queued after that point.
  - The duel baseline gives the same digest. Its turn time doesn't change measurably, because the sim has only a few subscribers. The gain is in the Godot apps, where every view subscribes.
- [x] **Memory of map objects (`ObjectMemory`):** use `FindObject(id)`, look only at tiles that just came into sight, and compare before allocating.
  - Done (P5):
    - A hero that moves, is hired, freed, given or leaves a garrison, or learns Scouting, now refreshes its owner's memory only on the tiles that came into sight. These are the newly explored tiles, plus, with refog, the tiles that entered sight. Every other trigger still refreshes everything in sight.
    - This is safe because each change to what memory keeps (owner, definition, removal) publishes an event that refreshes everything. `GarrisonOwnerChanged` was the one such event `VisibilityService` didn't handle. A garrison flagged in sight was noticed only on a later refresh, and the old full refresh on each step ran before the visit that flagged it. It now refreshes everyone's memory. It reveals nothing, so the fog, and with it the digest, is unchanged.
    - A remembered object is compared field by field, and a new snapshot is made only when something differs. Objects that are gone are found with `FindObject`, replacing the old `Objects.Any` scan, which was O(remembered × objects). Footprints are walked without LINQ, and nothing is allocated when nothing changed.
  - `VisibilityTests` adds two tests:
    - in both fog modes, after every step a full refresh finds nothing the step's refresh missed, including a pile taken in sight;
    - a garrison flagged in sight is seen flagged at once.
  - The baseline games give the same digests. Memory refresh was timed separately, with temporary instrumentation, on the same 13,769 / 5,338 calls: 875 → 149 ms on the big map and 149 → 30 ms on the duel. That was about 1 % of turn time, so turn times don't change measurably in the sim. Memory isn't in the digest, and no rule or AI reads it; only views, cursors, info sheets and the network feed do.
- [x] **Network views:** set a dirty flag in `GameHost.Capture` and call `SendViews` only when it is set. Store a hash in `_sent` instead of JSON. Cache the field keys and compiled accessors in `StateCodec`.
  - Done (P8):
    - `GameHost` builds the clients' views only after the game has published something. Every state change publishes an event, and fog changes come as events too. A client seated in between gets the whole view on joining, and pause and clock messages already had their own checks. The flag is cleared only after all views are sent, so if a send fails, the next frame tries again, as before.
    - `PlayerViewFeed` keeps 128 bits of a SHA-256 hash of each piece it sent, instead of the JSON text. It writes the JSON into a reused byte buffer, so it no longer builds a string to compare.
    - `StateCodec` keeps one `Shape` per type: its fields, the keys they are written under (worked out once, not on every write), compiled field getters, and whether the type is a sequence or a dictionary. Dictionary entries use compiled `Key`/`Value` getters. Reading still sets fields through reflection, because compiled expressions can't assign `readonly` fields.
  - New tests: `LanGameTests` checks that an idle host sends no updates until the game changes. `StateCodecTests` checks the written keys for a field name shared by a base and a derived type, a struct field and dictionary entries. The existing round-trip tests cover the rest.
  - Measured with a temporary benchmark: a generated 72² duel with refog and one remote client (Release).

    | Case | Before | After |
    |---|---|---|
    | 300 idle host polls | 380–440 ms (about 1.4 ms a frame) | 0.2 ms |
    | 4 polls after AI days | 7.4 ms | 2.5–3.0 ms (including compiling the getters once) |
- [x] **Small Godot fixes:** all five are done; the battle-input one through Phase 2 "Battle reach".
  - [x] Split `MapViewContext.Changed` into `ViewerChanged` and `LevelChanged`.
    - Done (P10). Each view subscribes to what it draws:
      - viewer only: the per-level views (fog, map objects, the map apron);
      - level only: level visibility, event cells, start zones;
      - both: heroes, grail holes, the minimap.
    - Switching levels no longer rebuilds every level's fog and objects.
  - [x] Share materials and meshes per colour in `MapObjectsView` and `PathPreview`.
    - Done. A `MeshCache` makes each placeholder mesh, and its material, once per shape, size and colour. Each view owns its cache, so the resources are freed with the view and don't linger in statics. No code changes these materials after they are made.
  - [x] Throttle the timer label and keep its `StringName` in a static field.
    - Done. `TurnTimerPanel` redraws only when the shown second, the stopped or time-up state, the warning or the colour changes, and it uses a static `font_color` name.
  - [x] Remember the last hovered cell in battle input.
    - Done through Phase 2 "Battle reach", not with a per-cell cache. `BattleTargeting.ActionAt` depends on the exact pointer position, not only the cell: the attack side and the tie-breaks use the pointer's distance. It also depends on the battle state, so a per-cell cache would change which stand a click picks. Its cost was the reach search, and hovering now gets the kept reach until the battle changes.
  - [x] Use `static readonly NodePath` for tween property paths.
    - Done. `TweenPaths` holds the five paths the views tween; all 21 `TweenProperty` calls use them.
  - Checked with the Godot smoke scenarios (Movie Maker; they fail on any Godot or C# error): route and move, 3D battle, turn timer, grail, replay viewer, generated connections in the editor, hero exchange and gate, and siege.
### Phase 2: Algorithm-level performance (about 1–2 weeks; check digests and AI behaviour with sim A/B runs)
- [x] **AI path costs:** one Dijkstra flood per (hero, position, command count), with candidate costs read from its cost array. Build the full route only for the winner. Do a reverse flood for Dimension Door landings, and cache the last path so the `MoveHeroCommand` does not search again.
  - Done (ADR-205):
    - `PathFinder.CostsFrom` and `CostsTo` are Dijkstra searches over the route rules that run only as far as the questions so far need. They answer exactly what `FindPath` gives as the route's cost. Steps only a target may take (bumping into a hero or quest object, stepping onto a monster from the tile it guards) are kept apart and count only for that tile. `PathFinder` and the cost maps share the rule helpers, so they can't drift apart.
    - The AI keeps one cost map per hero until `GameSession.ChangeStamp` moves (commands accepted, events published, hero moves). It prices its candidate targets, its exploring tiles, Town Portal and the walk before a Dimension Door with that map. One `CostsTo` map prices every Dimension Door landing; an origin with a visit tile falls back to a search, since a route may come back over it. The arena gathering and the route spells still search, because they need the route itself or move right away.
    - `HeroMovementService.FindPath` returns its last route while the change stamp stays the same, so the move after a check doesn't search again.
  - The flood exposed a bug in `FindPath`: its distance estimate counted 71 for a diagonal, but a dirt-road diagonal costs 70. A* then sometimes settled for a route up to a few points dearer, on maps without gates. The estimate now counts 70, so A* and the cost maps agree everywhere. Checked with a temporary comparison of every AI cost query against `FindPath` over 12 generated games: before the fix, 5 games had mismatches; after it, none did.
  - `PathCostMapTests` compares every tile's answer with `FindPath`, in three orders of asking. It covers two levels with monsters, linked and one-way gates, a garrison, a quest guard, a border guard and heroes, with and without fog, plus a reverse map for nine targets. It also has random road maps on which the old estimate fails. Two more tests cover the last-route cache.
  - Sim A/B against the previous commit, 18 generated games of 60 days (duel, ffa-4, 2v2, seeds 1–6): 15 end in an identical state. In 3, the cheaper routes change choices; the duel among them is won by the same player, on day 54 instead of 46. Total wall time 561 → 509 s. The baseline games keep their digests (the duel takes one more move on the way).
  - Baselines (one Release run each, after Phase 1):

    | Game | Path searches | Tiles expanded | Search time | Turn (mean / median) |
    |---|---|---|---|---|
    | Big map | 12,972 → 924, plus 924 cost maps | 6.3 M → 1.4 M | 4.7 → 1.9 s (8 % of turn time) | 98 → 106 ms / 44 → 47 ms, within this machine's noise |
    | Duel | 5,680 → 329, plus 329 cost maps | 1.5 M → 0.5 M | 1.9 → 1.4 s | 150 → 117 ms / 120 → 106 ms |

    On the big map, path search was already only a fifth of turn time after Phase 1, so most of the remaining turn time is elsewhere (battle estimates, P7).
- [x] **Battle reach:** remember `Search(battle, unit)` on `BattleState`, keyed by a version number bumped on any move, death or spell. Add `AttackPositions(reach, …)` so the AI filters one search. Build one `Clearance` for each placement scan. Add a unit occupancy grid and an id→unit dictionary.
  - Done (P6), with three changes to the plan:
    - **Key.** The cache is keyed by what the search reads, not by a version number. The search reads more than moves, deaths and spells: the unit's place, footprint, side and move limit (spells, the hero's bonus, native terrain); whether it flies or is held; the moat; standing walls; obstacles; the other living units' footprints; and the zones that block it. A version number would have to be bumped wherever any of these changes, and one missed bump would silently hand out a wrong reach, which the rules also use to validate moves. The key holds those inputs as numbers and is compared in full (about 50 integers, against a search of 0.2–0.4 ms). A kept reach can therefore never be stale, even when the AI or a test moves a unit by hand.
    - **Storage.** The cache sits beside the battle in a `ConditionalWeakTable`, not in `BattleState`. The battle is saved, sent to LAN clients and copied field by field (`StateCodec`, `BattleCopyTests`), so a cache field would travel with it. Each battle (and each copy) gets its own cache, which goes when the battle does.
    - **Not done: the occupancy grid and the id→unit dictionary.** Counted over the whole duel game, `UnitAt` runs about 330k times and `GetUnit`/`FindUnit` about 52k, each a scan over about 14 units: around 20 ms in all. That doesn't justify another index that must follow every move, death, summon and split.
  - Also done:
    - `AttackPositions(reach, unit, target)` lets the battle AI weigh every target from one reach. Its sort gives the same order as before.
    - `FreePlacements(battle, unit)` builds one `Clearance` for a whole placement scan: placements near a stack, a summon's spot, a skeleton's spot, the keep shooter's spot.
    - The walker search keeps its state in arrays over the field instead of dictionaries and a hash set, and still hands out the placements in the order they were first reached. Straight-line lengths come from a table, the clearance table is a flat array, and the line check no longer allocates on each step.
  - The reaches handed out are shared, so callers must not change them; they are `IReadOnlyDictionary`.
  - `BattleReachCacheTests`:
    - plays four battles (two of them sieges) with the AI, and after every action checks every unit's kept reach, its paths and its attack placements against a fresh search on a copy;
    - casts Thornwall, Haste and Entangle, takes a wall down, kills a stack, adds an obstacle and moves units, checking each time that the reach is searched again;
    - checks that `FreePlacements` agrees with `IsFree` everywhere.
    
    Leaving the zones, or the other side's units, out of the key makes these tests fail.
  - The 18 A/B games of the AI path costs item end in the same states as before: same digests, and the same results in 200 `sim battle` runs. Their total time fell from 509 to 286 s. About half the reach calls in battles are now kept answers, and the AI makes about 40 % fewer of them. Baselines (one Release run each):

    | Game | Turn (mean / median) | Battles |
    |---|---|---|
    | Big map | 106 → 52 ms / 47 → 38 ms | 150 → 41 ms |
    | Duel | 117 → 94 ms / 106 → 74 ms | 399 → 173 ms |

    Most of the gain is in the AI's battle estimates (P7), which play battles out and so search reaches too.
- [x] **`BattleEstimator`:** build the `BattleState` directly, following `BattleCommandHandler.Preview`. Use a struct hash key and a bounded cache that survives across turns.
  Done (ADR-206): the estimate fights set up their battle as a hero battle is set up and play it through `BattleFlow.PlayComputer`, with no
  game around it; their fixed seeds are the battles the throwaway games played, so every estimate is unchanged. Estimates are kept for the
  game under a struct key, in two generations of 4,096. `sim game --profile` counts them (`BattleEstimateCounters`). The 18 A/B games
  keep their digests (339 → 243 s).

    | Baseline | Turn time (mean / median) | Estimates asked / played |
    |---|---|---|
    | Big map | 58 → 35 ms / 42 → 22 ms | 2,959 / 150 (2.6 s) |
    | Duel | 106 → 72 ms / 81 → 65 ms | 511 / 111 (1.7 s) |

  The fights still take about a third of the AI's time: each plays a whole battle with the battle AI.
- [x] **Visibility:** cache flagged observers per player, and recompute only the circle of the observer that moved.
  - Done:
    - `FogOfWar.UpdateSight` keeps the observers (place, radius) and margin each player's sight was last worked out from. It compares
      them with the new ones and works out only the tiles within sight radius + margin of the observers that moved, came, went or changed
      their radius, checking each against the observers that can reach it. Every other tile keeps the observer that put it in sight, or
      still has none near enough, so the answer is the same as a fresh recompute. With more changed than kept, a new margin, or sight set
      from outside (a LAN client), it works everything out afresh.
    - `GameState.Flaggables` keeps the objects that can fly a flag, forgotten when one is added or removed like the lists by type. The
      observers read their owner and radius live, so a flag or an Observatory needs no event to be seen.
  - `FogSightTests` adds two tests: random observers stepping, jumping, teleporting between levels, coming, going, doubling up and
    changing radius, with margins 0–3, give after each change the same sight, entered and left tiles as a fresh recompute; a new margin
    and sight set from outside are worked out afresh.
  - `sim game --fog refog` plays with refog. The baselines give the same digests in both fog modes (duel with refog 858EBBAAB60E783F), and so
    do 9 generated games of 60 days with refog (duel, ffa-4, 2v2, seeds 1–3).
    Sight upkeep, timed with temporary instrumentation over the same updates: 577–780 → 217–248 ms on the big map (3,938 updates) and
    662–875 → 183–208 ms on the duel (1,829). That is under 1 % of the turn time either way, so turn times don't change measurably in the
    sim; the gain is per step in the apps.
- [x] **Map editor:** ground split into chunks of about 32×32 tiles, rebuilding only the chunks a stroke touches, and patch only the painted pixels of the splat map.
  - Done (P12, ADR-207):
    - `AdventureMapView` draws each level's ground as meshes of 32 × 32 tiles. A change of heights rebuilds the chunks with a tile that has a
      corner within one of a changed corner, the apron only when that reaches the map's edge, and the roads and rivers of the touched tiles
      (found from the area, not by walking the whole map).
    - Painting writes the painted tiles' bytes into the kept terrain map and uploads it; the texture upload stays whole (Godot has no
      partial update), but the map is no longer read tile by tile.
    - Timing the editor's dabs showed a bigger cost the item didn't name: `StartZonesView` laid every start zone plate again on each change of
      heights (about 160 ms a dab on a generated 256 × 256 map, whose zones are large). It now lays again only the plates on the touched tiles.
  - Measured with temporary timing in the editor on a generated 256 × 256 two-level map (Debug build, 20 strokes of each kind):

    | Dab | Before | After |
    |---|---|---|
    | Height (whole dab) | 250 ms | 12 ms |
    | of which the ground view | ~80 ms | ~2 ms |
    | of which start zones | ~160 ms | <1 ms |
    | Terrain paint (whole dab) | 78 ms | 5 ms |

    On a 36 × 36 map the difference doesn't show. What is left per stroke (not per dab) is the validation panel, about 60 ms when a stroke
    ends.
  - The `editor-heights` smoke frames are byte-identical to the old code's (60 of 60); `editor-tools` differs only in a toolbar button's hover
    highlight.
- [x] **Map objects:** `MultiMesh` per level for object cells, and show captions only nearby or on hover.
  - Done (P11, ADR-208):
    - `MapObjectsView` draws the boxes, plates and flags of its level's objects as instances of one `MultiMesh` per box size, coloured per
      instance with the colour taken as sRGB, so they look as before. Rebuilding an object hides its instances and reuses them; when they
      are all taken the instances are doubled and written again. Only the quest objects' shapes stay nodes.
    - Captions have a visibility range of 20 (fading over the last 2), so those around the camera's focus show and zooming out hides them.
      `MapViewContext.Pointed` (set by the game's `AdventureInputController` and the editor on pointer motion) shows the caption of the
      object under the pointer from any distance.
  - Measured with temporary monitors in the editor, 60 frames in:

    | Map | Objects | Nodes | Draw calls in view |
    |---|---|---|---|
    | 256 × 256 | 1,969 | 7,017 → 1,250 | 133 → 132 |
    | 144 × 144 | 712 | 3,883 → 1,176 | 147 → 139 |

    The camera sees only part of the map, so draw calls barely change; the gain is the nodes (and their memory and tree upkeep) and the far
    captions.
  - Smoke frames of object-heavy scenarios (`game-map-sites`, `game-quests`, `editor-inspector-objects`) show the same objects in the same
    colours; they differ where a far caption is now hidden, and in the hover status line.

### Phase 3: Correctness and architecture cleanup (about 1 week)
- [x] **Scenario tracking state:** move `GameFlowService` tracking into `GameState` (for example a `ScenarioTracking` record) and add it to the digest. Bump the digest version and keep older replays checking.
  - Done (ADR-213). `GameState.Tracking` (`ScenarioTracking`) holds the teams at start, the heroes at start and the tracked prisoners; `GameFlowService` keeps no state of its own.
  - `GameStateDigest` has versions: version 2 adds the tracking line once the game has started. A record stores the version its final digest was computed with (`DigestVersion`, record format 4); a file without one checks with version 1.
  - The digests quoted in the baselines above are version 1. The duel baseline still gives `02E607C4B08FDC80` in version 1, so the rules play the same; its version 2 digest is `A0F1FADCC6D8DEE4`.
- [x] **Mutate before publishing:** fix `TurnService.EndTurn`. Replace `AreaRevealed` used as a request with a direct `IFogRevealer` call followed by the event.
  - Done (ADR-214). `EndTurn` takes the turn out of today's open turns (a simultaneous day) before `TurnEnded`; the next turn follows the event.
  - `IFogRevealer.RevealArea` (implemented by `VisibilityService`) reveals for View Terrain and map events before `AreaRevealed`, and for observatories, Magi windows, the Ruins and cartographers before `SurroundingsRevealed` (`ObjectInteractionContext.RevealSurroundings`). `VisibilityService` no longer handles either event.
  - `TilesRevealed` and `KnownObjectsChanged` now come before the reveal event instead of after it. The fog, the random draws and the digests are unchanged: the duel baseline still gives `A0F1FADCC6D8DEE4` and the sim self-test `82AE941CD62212E2`.
- [ ] **Shared decision helpers:** `Ask`, `TakeDecision<T>`, `Resolve` and `RemoveObject` on one shared helper, used by all services, with one fixed order.
- [ ] **Battle guard outcomes:** `IBattleGuard` returns a description of the outcome, and a service applies it, so entities stop mutating state.
- [ ] **Simultaneous days:** one `SimultaneousDayCoordinator` replaces the public flag and the state spread over the session and `TurnService`.
- [ ] **Network access:** replace `InternalsVisibleTo` for Network with a narrow sink interface that applies the client's view updates. Add a schema hash to the join handshake.

### Phase 4: Breaking up large classes (ongoing; one per PR)
- [ ] **Per-kind handlers**, the same pattern as the battle spell handlers, for `SpellEffect.Parse`, `MapEventService` actions, adventure spells, `ScenarioCondition` and `MapValidator` checks.
- [ ] **`AdventureAi`:** extract `ThreatMap`, `TargetValuer`, `TownManager` and `DecisionResponder`, plus an `AiWeights` record the sim tool can tune.
- [ ] **`GameHost`:** split out `SeatManager`, `PausePolicy`, `ChatRelay` and `SimultaneousDayRewinder`.
- [ ] **Game app `Main`:** move AI and timer gating and local seat ownership into a Core `GameDirector` (with tests), replay stepping into a `ReplayController`, and screens into a coordinator in Godot.UI.
- [ ] **Panels:** split `TownPanel` and `HeroPanel` into sections with their own subscriptions and schedulers. Give the inspector one section per object type. Add a `FormBuilder.AddOwnerChoice` helper. Move colours and sizes into `UiTheme` tokens.
- [ ] **Lower priority:** typed content ids, `InfoSheets` returning structured lines instead of English text, splitting the sim tool's `Program.cs` into one class per command with strict option parsing, autosave written straight to a stream, and save listing that reads only the header.

**What to watch:**
- Digests: Phases 1–2 must not change the random draw order or tie-breaks. Run the replay and digest pin tests after each step.
- AI behaviour: Phase 2 changes to the AI and the estimator need a sim A/B comparison of win rates, not only green tests.
- Recording decisions: each Phase 3 and 4 change that alters a documented rule needs an ADR in `docs/project/decisions.md`.
