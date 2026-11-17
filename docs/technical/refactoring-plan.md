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
- **State the digest misses (`GameFlowService.cs:19-21`).** It keeps the state the scenario rules use (the heroes and prisoners at start, the teams at start) outside `GameState`. That state is not in the digest, the LAN view or AI copies of the state.
- **Publish before mutate (`TurnService.cs:75-80`).** `TurnEnded` is published before the turn state changes, which breaks the mutate-then-publish rule.
- **Events used as commands.** `AreaRevealed` is published so that `VisibilityService` will change the fog, which only works because visibility subscribes first.
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
- [ ] **`GameState` indexes:** `Dictionary<MapPosition, Hero>`, a guarded-tile `bool[]` per level, per-type object buckets for `ObjectsOf<T>`, and a cached garrison list. `IsGuarded` becomes an allocation-free loop.
- [ ] **`PathFinder` buffers:** pooled arrays plus a generation stamp instead of `closed`, a reused `PriorityQueue`, a per-terrain cost table built once per search (replaces `ArmyTown` per edge), and a cached teleporter list.
- [ ] **Event bus:** cache `Dictionary<Type, Subscription[]>` per concrete event type, cleared when subscriptions change.
- [ ] **Memory of map objects (`ObjectMemory`):** use `FindObject(id)`, look only at tiles that just came into sight, and compare before allocating.
- [ ] **Network views:** set a dirty flag in `GameHost.Capture` and call `SendViews` only when it is set. Store a hash in `_sent` instead of JSON. Cache the field keys and compiled accessors in `StateCodec`.
- [ ] **Small Godot fixes:**
  - Split `MapViewContext.Changed` into `ViewerChanged` and `LevelChanged`.
  - Share materials and meshes per colour in `MapObjectsView` and `PathPreview`.
  - Throttle the timer label and keep its `StringName` in a static field.
  - Remember the last hovered cell in battle input.
  - Use `static readonly NodePath` for tween property paths.

### Phase 2: Algorithm-level performance (about 1–2 weeks; check digests and AI behaviour with sim A/B runs)
- [ ] **AI path costs:** one Dijkstra flood per (hero, position, command count), with candidate costs read from its cost array. Build the full route only for the winner. Do a reverse flood for Dimension Door landings, and cache the last path so the `MoveHeroCommand` does not search again.
- [ ] **Battle reach:** remember `Search(battle, unit)` on `BattleState`, keyed by a version number bumped on any move, death or spell. Add `AttackPositions(reach, …)` so the AI filters one search. Build one `Clearance` for each placement scan. Add a unit occupancy grid and an id→unit dictionary.
- [ ] **`BattleEstimator`:** build the `BattleState` directly, following `BattleCommandHandler.Preview`. Use a struct hash key and a bounded cache that survives across turns.
- [ ] **Visibility:** cache flagged observers per player, and recompute only the circle of the observer that moved.
- [ ] **Map editor:** ground split into chunks of about 32×32 tiles, rebuilding only the chunks a stroke touches, and patch only the painted pixels of the splat map.
- [ ] **Map objects:** `MultiMesh` per level for object cells, and show captions only nearby or on hover.

### Phase 3: Correctness and architecture cleanup (about 1 week)
- [ ] **Scenario tracking state:** move `GameFlowService` tracking into `GameState` (for example a `ScenarioTracking` record) and add it to the digest. Bump the digest version and keep older replays checking.
- [ ] **Mutate before publishing:** fix `TurnService.EndTurn`. Replace `AreaRevealed` used as a request with a direct `IFogRevealer` call followed by the event.
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
