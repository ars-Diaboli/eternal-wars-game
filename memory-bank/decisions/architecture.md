# Decisions: Architecture and code

**Covers**: The engine, the three tiers, events, commands and replays, the map and save formats, code structure, code reviews and refactors.

**Current rules**: [docs/technical/architecture.md](../../docs/technical/architecture.md), [docs/technical/event-system.md](../../docs/technical/event-system.md), [docs/technical/development.md](../../docs/technical/development.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [001](#adr-001-godot-472-net--c-on-net-10) | Godot 4.7.2 (.NET) + C# on .NET 10 | accepted |
| [002](#adr-002-three-tiers-engine-free-core) | Three tiers, engine-free core | accepted |
| [003](#adr-003-custom-synchronous-pubsub-instead-of-a-library-mediatr-rx-godot-signals) | Custom synchronous pub/sub instead of a library (MediatR, Rx, Godot signals) | accepted |
| [004](#adr-004-separate-godot-projects-per-app-assets-shared-via-links) | Separate Godot projects per app, assets shared via links | accepted |
| [006](#adr-006-text-friendly-map-format) | Text-friendly map format | accepted |
| [007](#adr-007-code-first-godot-nodes-in-libraries) | Code-first Godot nodes in libraries | accepted |
| [014](#adr-014-choices-are-pending-decisions-not-callbacks) | Choices are pending decisions, not callbacks | accepted |
| [036](#adr-036-commands-only-seeded-rng-replays) | Commands only, seeded RNG, replays | accepted |
| [041](#adr-041-map-format-v4) | Map format v4 | accepted |
| [165](#adr-165-refusals-content-tables-and-running-events-after-the-code-review-adr-036-adr-042-adr-115-2026-11-10) | Refusals, content tables and running events after the code review (ADR-036, ADR-042, ADR-115, 2026-11-10) | implemented |
| [166](#adr-166-map-object-kinds-in-registries-not-type-switches-adr-013-adr-065-2026-11-10) | Map object kinds in registries, not type switches (ADR-013, ADR-065, 2026-11-10) | implemented |
| [167](#adr-167-the-large-classes-split-along-their-seams-adr-036-adr-166-2026-11-10) | The large classes split along their seams (ADR-036, ADR-166, 2026-11-10) | implemented |
| [168](#adr-168-a-handler-per-spell-effect-kind-adr-099-adr-123-adr-167-2026-11-10) | A handler per spell effect kind (ADR-099, ADR-123, ADR-167, 2026-11-10) | implemented |
| [169](#adr-169-commands-by-handler-and-marker-turn-phases-as-one-ordered-list-adr-036-adr-167-2026-11-10) | Commands by handler and marker, turn phases as one ordered list (ADR-036, ADR-167, 2026-11-10) | implemented |
| [170](#adr-170-rules-classes-are-pure-queries-their-changes-are-services-adr-167-2026-11-10) | `*Rules` classes are pure queries; their changes are services' (ADR-167, 2026-11-10) | implemented |
| [171](#adr-171-one-contentcatalogs-record-from-the-apps-to-the-generator-adr-165-2026-11-10) | One `ContentCatalogs` record from the apps to the generator (ADR-165, 2026-11-10) | implemented |
| [172](#adr-172-one-copy-of-each-shared-step-code-review-section-3-2026-11-10) | One copy of each shared step (code review section 3, 2026-11-10) | implemented |
| [173](#adr-173-caches-beside-what-they-cache-code-review-section-4-2026-11-10) | Caches beside what they cache (code review section 4, 2026-11-10) | implemented |
| [174](#adr-174-map-format-upgrades-and-testing-safety-nets-code-review-section-5-2026-11-11) | Map format upgrades and testing safety nets (code review section 5, 2026-11-11) | implemented |
| [202](#adr-202-every-event-declares-who-may-learn-of-it-adr-193-mp-3-2026-11-17) | Every event declares who may learn of it (ADR-193, MP-3, 2026-11-17) | implemented |
| [213](#adr-213-the-scenario-rules-memory-is-game-state-and-the-digest-has-versions-adr-036-adr-057-adr-058-2026-11-18) | The scenario rules' memory is game state, and the digest has versions (ADR-036, ADR-057, ADR-058, 2026-11-18) | implemented |
| [214](#adr-214-events-tell-what-happened-never-ask-for-it-the-turns-end-is-recorded-before-turnended-adr-016-adr-197-2026-11-18) | Events tell what happened, never ask for it; the turn's end is recorded before `TurnEnded` (ADR-016, ADR-197, 2026-11-18) | implemented |
| [215](#adr-215-one-set-of-decision-steps-taken-in-one-order-adr-016-adr-214-2026-11-18) | One set of decision steps, taken in one order (ADR-016, ADR-214, 2026-11-18) | implemented |
| [216](#adr-216-a-battle-guard-describes-its-outcome-a-service-applies-it-adr-068-adr-215-2026-11-18) | A battle guard describes its outcome; a service applies it (ADR-068, ADR-215, 2026-11-18) | implemented |
| [219](#adr-219-per-kind-rules-are-handler-classes-in-registries-adr-120-2026-11-19) | Per-kind rules are handler classes in registries (ADR-120, 2026-11-19) | implemented |
| [222](#adr-222-the-game-apps-policy-lives-in-a-gamedirector-a-replaycontroller-and-gamescreens-adr-059-adr-091-adr-164-2026-11-19) | The game app's policy lives in a `GameDirector`, a `ReplayController` and `GameScreens` (ADR-059, ADR-091, ADR-164, 2026-11-19) | implemented |
| [223](#adr-223-the-town-and-hero-screens-are-sections-the-inspector-has-one-section-per-object-type-adr-046-adr-191-2026-11-19) | The town and hero screens are sections; the inspector has one section per object type (ADR-046, ADR-191, 2026-11-19) | implemented |
| [224](#adr-224-the-sim-tools-commands-are-classes-with-strict-options-saves-stream-to-disk-and-list-from-their-label-adr-034-adr-058-2026-11-19) | The sim tool's commands are classes with strict options; saves stream to disk and list from their label (ADR-034, ADR-058, 2026-11-19) | implemented |
| [231](#adr-231-the-game-apps-remaining-policy-moves-to-core-autosave-rematch-a-saves-seats-lan-rules-replay-recovery-the-maps-focus-adr-222-adr-226-2026-11-19) | The game app's remaining policy moves to core: autosave, rematch, a save's seats, LAN rules, replay recovery, the map's focus (ADR-222, ADR-226, 2026-11-19) | implemented |
| [232](#adr-232-code-health-continue-falls-back-to-a-save-that-loads-the-events-panel-refreshes-once-and-while-shown-every-ai-tuning-number-is-a-weight-leftover-cleanups-adr-220-adr-224-adr-226-2026-11-19) | Code health: Continue falls back to a save that loads, the events panel refreshes once and while shown, every AI tuning number is a weight, leftover cleanups (ADR-220, ADR-224, ADR-226, 2026-11-19) | implemented |
| [236](#adr-236-a-lan-clients-view-writes-move-the-change-stamp-a-players-sight-is-handed-out-as-a-copy-adr-205-adr-218-adr-226-2026-11-19) | A LAN client's view writes move the change stamp; a player's sight is handed out as a copy (ADR-205, ADR-218, ADR-226, 2026-11-19) | implemented |
| [237](#adr-237-a-map-event-actions-handler-tells-what-the-action-holds-its-editor-line-and-its-worth-to-the-ai-adr-219-adr-226-2026-11-19) | A map event action's handler tells what the action holds, its editor line and its worth to the AI (ADR-219, ADR-226, 2026-11-19) | implemented |


### ADR-001: Godot 4.7.2 (.NET) + C# on .NET 10
**Status**: accepted (2026-10-15)
Latest stable Godot with C# support. `Godot.NET.Sdk` 4.7.2 builds against `net10.0`, so one runtime
is used across core, tests and apps. Upgrade to 4.8 once it ships stable.

### ADR-002: Three tiers, engine-free core
**Status**: accepted
Core (no Godot) → Godot libraries → apps. Core rules are testable in milliseconds and reusable for
AI simulation, servers and tools. Enforced in `Directory.Build.targets`.

### ADR-003: Custom synchronous pub/sub instead of a library (MediatR, Rx, Godot signals)
**Status**: accepted
Requirements are narrow (deterministic order, breadth-first re-entrancy, lifetime-scoped
subscriptions) and must be engine-agnostic. Godot signals would leak the engine into core; MediatR/Rx add
async/threading semantics we don't want. ~100 lines, fully tested. See [event-system.md](../../docs/technical/event-system.md).

### ADR-004: Separate Godot projects per app, assets shared via links
**Status**: accepted; confirmed by the Phase 0 spike (ADR-043)
User-facing apps stay independent (own settings, renderer, export presets). Shared assets live once in
`EternalWars.Godot.Assets` and are linked as `res://shared`.
*Alternative considered*: a single Godot project with three main scenes (simpler asset sharing, one
import cache) that splits apps with export feature tags. Still viable if links prove painful.

### ADR-006: Text-friendly map format
**Status**: accepted
`*.ewmap.json` with terrain stored as one string per row so maps can be reviewed and merged in git.
Versioned via `formatVersion`.

### ADR-007: Code-first Godot nodes in libraries
**Status**: accepted; confirmed by the Phase 0 spike (ADR-043)
Library node classes are instantiated from code and configured through `Bind(...)`. Avoids Godot's
`res://` script-path requirement for scripts in referenced assemblies.

### ADR-014: Choices are pending decisions, not callbacks
**Status**: accepted (2026-10-15); decisions are answered by commands (ADR-036)
When a rule needs player input, it stores a `PendingDecision` in `GameState` and publishes
`DecisionRequested`. Movement and End Turn are refused until it is resolved. The core never
blocks or calls back into the UI; AI, UI and future network/replay resolve decisions the same way.
They will become commands in Phase 2.

### ADR-036: Commands only, seeded RNG, replays
**Status**: accepted (2026-10-15)
- Every player intent is a serializable `GameCommand` record (`Core.Domain/Commands`) executed by
  `GameSession.Execute`. The services' mutating methods are `internal`: front-ends, AI, tests and the
  simulator all go through commands, so nothing can bypass the log (user decision).
- `Execute` authorizes the command (the player whose turn it is on the map, or the commander of the active
  battle side), dispatches it, and appends it to `GameSession.CommandLog` only when accepted. A refusal returns
  `CommandResult.Rejected(reason)` and changes nothing; rule outcomes that are not refusals (a step into
  water) come back as `CommandResult.Outcome` (`MoveResult`, `MovementOutcome`). Services validate before
  they mutate, so a refusal never leaves half-applied state.
- `session.Start()` is not a command: every game has it.
- Randomness: `GameRandom` (SplitMix64; its whole state is one `ulong`) seeded from `GameOptions.Seed`, owned
  by `GameState` and drawn only by Domain services, in command order. Each battle forks its own generator from
  it. `System.Random` is banned from rules.
- Replays: `GameRecordDocument` (`*.ewreplay.json`, `Core.Data/Records`) = initial map + options + commands +
  final `GameStateDigest`. `GameReplay.Replay` re-executes the commands and throws if one is refused or the
  digest differs. The command hierarchy is registered for JSON in `Core.Data` (a resolver modifier), so
  Domain stays free of serialization attributes.

### ADR-041: Map format v4
**Status**: accepted (2026-10-15)
Adds hero `attack`/`defense`/`tactics`/`army` (slot, creature, count), `guardMode`, `monster` objects, a mine
`guard`, and makes chest `gold`/`experience` optional (both omitted = rolled). v1–v3 still load: their chests
keep their values as fixed values, and their heroes start without an army.

### ADR-165: Refusals, content tables and running events after the code review (ADR-036, ADR-042, ADR-115, 2026-11-10)
**Status**: implemented (2026-11-10); tested. Fixes from the code quality review.

- **Refusals** (ADR-036): a rule refuses a command with `CommandRefusedException` (Core.Domain `Commands`), thrown before anything changes;
  `GameSession.Execute` catches only that. Before, it took every `InvalidOperationException` for a refusal, so a bug (a `First()` on nothing)
  passed as a quiet refusal. `GameState.GetHero` / `GetObject` / `GetPlayer` refuse an unknown id with it. The answers to a chest, a deposit, a
  cartographer, a Pandora's Box, a witch hut and an event's army choice now find everything they need before they spend, take or clear
  anything (a deposit's price was paid before the deposit was looked up). `GameSession.CheckRefusals` (internal) compares the digest text
  before and after every refused command and fails loudly when it changed; the test assembly turns it on for every game (`TestSetup`),
  about 10 s on the whole run *(choice: every test game, not one dedicated test)*.
- **Content tables**: `ContentCatalogs` (creatures, heroes, buildings, artifacts; the spells are the artifact table's) is what the
  scenarios and `MapConverter.ToGameState(document, content, options)` take; the apps pass `GameContent.All`. An edited artifact table
  (`--artifacts=`) was lost for Demo Valley, Two Towns, the editor's blank maps and the 2D harness. `MapConverter` checks shrine and
  guild spells against the game's spell table, not the built-in one.
- **Creature casters** (ADR-042, ADR-120): `Caster` in `creatures.csv` names the spell table id (`Caster chaos.fireball 1`); the code's
  name table (`BattleMagic.CasterSpells`) is gone, and a caster's spell comes from the battle's spell table (`BattleState.Spells`, the
  game's), not the built-in one. `rebirth` stays the Phoenix's own ability.
- **Running map events** (ADR-115): the queue of events to run, the one running and the battle record for "defeated by" are kept in
  `MapEventState` (`Runs`, `Current`, `Fight`), not in the service; the running ones are in the digest while there are any *(a save made
  while an event waited for an answer no longer matches its old digest)*.
- **Map files without a version** are version 1 (`MapDocument.UnversionedFormatVersion`), so every older version's handling applies; a
  document made in code is still current.
- **Smaller fixes**: the AI's battle estimate cache key uses the hero's total attack and defence (with artifacts), as the fight does;
  `EconomyService.Grant` / `TrySpend` are internal; `SubscribeWhileInTree` subscribes while the node is in the tree (again if it comes
  back), and the map editor subscribes its undo line on the session's UI instead of the root, which kept every old session alive.
- **Not done**: the tests still call `session.Interactions.Visit`, `Progression.GrantStatus` / `OfferNext`, `Artifacts.Draw` / `Loot` /
  `Grant` and `Interactions.TryCollect` / `StartWeek` directly (about 55 calls in 20 files) instead of going through `Execute`.

### ADR-166: Map object kinds in registries, not type switches (ADR-013, ADR-065, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R5).

- **Problem**: a new map object kind touched 9–12 places, several of which fell through silently: the digest's `_ =>` left a new kind's
  state out, `Visit` did nothing for an unknown type, and the map format needed a `[JsonDerivedType]` plus a `case` in both directions.
- **Domain**: `MapObjectKinds` registers each `MapObjectKind` with its instance type and a factory (`MapObject.Create` uses it). Each
  subclass states its own digest text (`MapObject.DigestState`, abstract, so the compiler asks for it; "" for none) and whether it is a
  pickup (`IsPickup`) or defended against a visitor (`IsDefendedAgainst`), which the move reads instead of a type list. The digest text is
  unchanged (checked against the old switch on the built-in and generated maps, and fresh instances of every kind).
- **Visits**: `MapObjectInteractionService` keeps one rule per instance type (a table, `Nothing` for objects used from a panel); visiting a
  type without a rule throws instead of doing nothing.
- **Map format**: `MapConverter.ObjectCodecs` (Core.Data) holds each type's `kind` name, document type, save and load; the JSON
  discriminators are added from it by a resolver modifier (`AddObjectTypes`, in `JsonDefaults` and the replay options) instead of 45
  attributes on `MapObjectDocument`. The names and the JSON are the same, so the format version stays 38.
- **Held together by tests** (`MapObjectKindsTests`): every kind is registered and makes its type; every type has a visit rule and exactly
  one codec under a unique name; a fresh object of every kind comes back from a map file with the same digest text.
- **Not done** *(choice)*: the AI's targets, the validator, the info sheets and the editor's inspector still switch on the type. Each has a
  safe default for a kind it does not know (no target, no check, a plain sheet, no fields), so a new kind is playable before they learn it.

### ADR-167: The large classes split along their seams (ADR-036, ADR-166, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R6).

- **Problem**: six classes of 1100–2000 lines each mixed several jobs, and some held copies of the same rule (five ways to heal the top
  creature of a stack, seven-way type switches over guard kinds). One change meant reading the whole file.
- **Rule**: a pure refactor of the game rules. Events, their order, refusals, `GameState.Random` draws and the digest text are unchanged;
  AI-played games on the built-in scenarios and every built-in template (two seeds each) give the same per-turn digest and event log as
  before. The map format is unchanged.
- **Map editor**: `MapEditorSession` is a facade over per-area editors (`Terrain`, `Objects`, `Contents`, `Monsters`, `Towns`, `Heroes`,
  `MapEvents`, `Players`, `Map`) sharing an internal `EditorContext` (scenario, catalog, bus, undo history, open stroke). One-value edits are a
  `PropertyEdit<T>`, which records and publishes nothing when the value is already there; the three paint tools share `PaintLayer<T>`. Edits
  that are not one value (hero type, town type, gate links, adding or removing events and players) stay their own undoable commands.
- **Map objects**: `MapObjectInteractionService` keeps the visit rules; shops, decisions (`Resolve*`), the week and map preparation, and the
  Grail are `ObjectShopService`, `ObjectDecisionService`, `ObjectLifecycleService` and `GrailService`, with rewarding visits in
  `MapObjectRewardVisits` and shared helpers in `ObjectInteractionContext`.
- **Battles**: `BattleService` is a facade over `BattleStarter`, `BattleCommandHandler`, `BattleFlow` (deployment and the AI loop both need)
  and `BattleAftermath`. The objects that guard a battle implement `IBattleGuard` (what defeat and survivors do to them) instead of two type
  switches. `BattleEngine` keeps actions and turn flow; spells are `BattleSpellResolver`, rounds and the end `BattleRounds`, and damage,
  deaths and healing `BattleWounds`, which holds the one `HealTop`, `KillCreatures` and `RollDamage`. `BattleState.NextUnitId()` makes new
  stack ids.
- **Presentation**: the combat log text is `BattleNarrator` in Core (tested). `BattleView3D` coordinates `BattleAnimationQueue`,
  `BattleFieldBuilder` (one material per colour per battle), `BattleStage` and `BattleAnimations`. The game app's `Main` keeps composition;
  status text moved to Core (`MoveOutcomeText`, `AdventureSpellPrompt`, `SaveSlotText`, `RankingText`, `TemplateFolder`), and the HUD,
  map and battle input, deployment drag and setup screens to Godot.UI (`AdventureHud`, `AdventureInputController`,
  `BattleInputController`, `DeploymentDragController`, `SetupScreenHost`). The input controllers are made per game, so an armed spell or a
  drag in progress does not carry into a loaded game.
- **Not done** *(choice)*: the map editor app keeps its own template-folder loading; `Main.Saves.cs` stays a partial of `Main` because it
  needs most of its state.

### ADR-168: A handler per spell effect kind (ADR-099, ADR-123, ADR-167, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R7).

- **Problem**: what one effect kind does was spread over the target checks, the resolver and the AI's valuation, each switching on
  `SpellEffectKind`; a new mechanic meant five or six edits, and the AI's value of a spell could drift from what the spell does.
- **Rule**: each `SpellEffectKind` has one internal sealed handler in `Battles/Spells/` (`ISpellEffectHandler`, found through
  `SpellEffectHandlers.For(kind)`). It holds the kind's own refusals (at three fixed points: before the aim checks, before and after
  `Affects`), which extra stacks `targets N` reaches, how the cast resolves, the AI's value of a cast (`Value(CastContext)`) and the aims the
  AI tries. Buff and Curse share `EnchantHandler`, which the damage handler also uses for damage that leaves an effect.
- **Shared**: aiming, reach and area cells, filters, immunity and resistance, and the damage plan (used by the rule and the AI) stay in
  `BattleMagic`, as do small formulas both a rule and its value read (`Missing`, `Dispelled`, `Slain`, `SacrificeYield`, …).
- **A new effect kind** is its parse in `SpellEffect.Parse` plus one handler class; a test fails while a kind has no handler or two.
- **Behaviour**: events, their order and the random draws are unchanged (AI games with 89 casts replay the same). The kind's refusal
  checks run at their fixed points, which gives the same answer for every effect the spell table can express.
- **Not done** *(choice)*: the spell table parser and `SpellEffect.IsHostile`/`TwoClicks` stay on the effect (they describe the format);
  the battle HUD keeps its two checks of `Move` for the two-click prompt, as the registry is internal to Core.

### ADR-169: Commands by handler and marker, turn phases as one ordered list (ADR-036, ADR-167, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R8).

- **Commands**: `GameSession` dispatches through `CommandHandlers`, one registration per command type, grouped by area
  (`AdventureCommandHandlers`, `ObjectCommandHandlers`, `TownCommandHandlers`, `ArtifactCommandHandlers`, `BattleCommandHandlers`). A second
  registration for a type throws, and a test fails while a command has no handler. A new command is its record plus one registration line.
- **Authorization**: `IBattleCommand` marks the commands that need a running battle and are checked against the side to act;
  `IHeroCommand` (`HeroId Hero`) marks those whose hero the session checks is on the map and the player's. Equip, Unequip, Assemble, Take
  Relic Apart and Appoint Governor carry a hero but are checked by their own services, with their own refusals, and are not marked;
  `MoveStackCommand` names its `FromHero` as the hero; `SetAutoCombatCommand` is allowed at any time. The markers add nothing to the
  commands' JSON.
- **Turns**: `TurnService` calls an ordered list of `ITurnPhaseParticipant`s built in `GameSession` (map events, progression, movement, mana,
  towns, caravans, economy, guards, map objects), each hook a no-op by default (`SetUpGame`, `StockFirstWeek`, `StartDay`, `StartWeek`,
  `ApplyWeekEvent`, `StockWeek`, `OpenDay`, `PrepareTurn`, `OpenTurn`). The list's order is the order of the turn. The arena stays a direct
  dependency, as its battle day replaces the whole day.
- **Subscriptions**: visibility, map events and adventure magic subscribe themselves (`SubscribeTo(bus)`), in that order.
- **Behaviour**: unchanged (events, refusals, random draws, digests; AI games replay the same).

### ADR-170: `*Rules` classes are pure queries; their changes are services' (ADR-167, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R9).

- **Rule**: a static `*Rules` class holds constants and queries (checks, costs, odds, offers) only. Whatever changes the state or publishes
  lives in a service that the session builds and hands to its callers. `RulesPurityTests` fails on a `*Rules` method that takes an
  `IEventPublisher` or returns nothing.
- **`TownVisitService`** (new): a hero's arrival in a town (`Enter` = governor by Stewardship, then `Visit`: the Grail building, the unique
  buildings' bonuses, the war lodge's warcries, the guild's spells and their ranks, mana after a night), `VisitBy` after a build, governors
  (`AutoAppoint`, `SetGovernor`) and `Capture`. Movement, object decisions, Town Portal, map events, `TownService` and the battle aftermath use
  it; the guard kinds reach it through `BattleGuardOutcome.Towns`. It replaces `MagicRules.Visit`/`VisitBy`, `GrailRules.RaiseBuilding`,
  `UniqueBuildings.Visit` and the static `TownService.Capture`/`AutoAppoint`.
- **Others**: `GrailService.Bury` and `VisitObelisk` (were `GrailRules.Bury`/`Visit`), `ObjectLifecycleService.PreparePrisons`
  (was `PrisonRules.Prepare`), `PandorasBoxService.Open` (was `PandorasBoxRules.Open` with 7 parameters; a won guard battle reaches it through
  `BattleGuardOutcome.Boxes`), `PlaceholderService.Resolve` (was `PlaceholderRules.Resolve`), `TownService.DrawGuilds` (was
  `MagicRules.DrawGuilds`). `CreatureBankRules.Roll` returns the rolled tier and `GuardService` fills the bank with it (was
  `CreatureBankRules.Fill`). The unique buildings' bonuses use the session's `ProgressionService` instead of a new one per visit.
- **Behaviour**: unchanged (events, refusals, random draws, digests; AI games replay the same). The game-start order is the same: placeholders,
  Grail, surplus heroes, begin, unset pickups, prisoners, object rolls, guild spells.

### ADR-171: One `ContentCatalogs` record from the apps to the generator (ADR-165, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R10).

- **Record**: `ContentCatalogs` holds every content table a game reads (creatures, spells, artifacts, buildings, hero roster, and also the map
  objects and terrains); `ContentCatalogs.Default` is the built-in content. `GameState` takes its object and terrain tables from it like the
  others.
- **Entry points**: `MapConverter.ToGameState(document, content?, options?, checkEvents)`, `GameSaves.Load(path, content?)`,
  `GameReplay.Replay` / `ReplayPlayer.Open(record, content?)`, and the generator's `MapGenerator.Build` / `Generate` / `GenerateBest` and
  `EditorMapGenerator.Generate` take one optional `ContentCatalogs` instead of one optional parameter per table; omitted means the built-in
  content. The apps build their record once (`GameContent.All`; the Sim tool from `--creatures`) and pass it everywhere.
- **Generator**: `GeneratedMap.Content` carries the record through every stage; no stage reads a built-in table itself
  (`ZoneLayouter`, `TerrainPainter` and `TemplateValidator` take the record too). The template editor still validates against the built-in
  tables, as it has no game content.
- **Behaviour**: unchanged with the built-in content (digests and AI games replay the same).

### ADR-172: One copy of each shared step (code review section 3, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (section 3, duplication).

- **Joining heroes**: `HeroRecruitment.Join` (a roster hero with its experience levelled up at once, a hired one's starter stack, moving
  today or next turn) and `Return` (a retired hero hired again) replace the four copies in `TownService.HireHero`, the map tavern, prisons
  and map events. The callers still check and publish.
- **Building**: `TownConstruction.Raise` / `RaiseDwelling` are the day's build and a map event's free build alike; the rule that a guild,
  war lodge, school or unique building lets the town's heroes take it at once is written once.
- **Taking an artifact away**: `ArtifactService.Remove(hero, artifact, wornFirst)` for the Grail building, the altar and map events (a map
  event takes a worn one first, the others a carried one).
- **Battle end**: `BattleOutcome.Of` reads the outcome once; `BattleAftermath.Finish` applies it and auto combat's preview shows it, and
  the preview raises undead into the army the hero fought with (`BattleAftermath.PreviewRaised`). This fixes two disagreements: the
  preview promised experience for the arena battle, and it raised a garrison hero's undead into the hero's own (empty) army rather than the
  garrison. `BattleOutcome.KeepSurvivors` is the one survivors-into-slots step (hero, town or map garrison).
- **Battle input**: `BattleTargeting.CommandFor` / `CommandAt` turn an action into its command for the game and the 2D harness. The keys
  stay each app's input mapping (the harness keeps S for surrender; the game has the HUD's button).
- **UI**: `FormBuilder` (Godot.Shared/Controls) replaces the panels' own `Row` / `Spin` / `Options` helpers (inspector, events, conditions,
  template editor, new game, game menu); `node.ClearChildren(detach)` replaces 35 free-all-children loops.
- **Sim**: its decision answers are `TurnTimer.Default` except where it deliberately differs (witch huts and training take the first
  option, riddles and level-ups the first answer, boxes stay shut, guards are always fought). The connectivity check moved to
  `MapConnectivity` in Core.Domain (`Editing/`), next to `MapValidator` so the validator can use it; not in Core.Generation, which the
  validator cannot reference.
- **Behaviour**: unchanged apart from the preview fixes (events, random draws, digests; AI games replay the same; the Sim's self-test digest
  is the same).

### ADR-173: Caches beside what they cache (code review section 4, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (section 4, performance).

- **Spell sums**: a stack's spells are an `ActiveSpellList` that keeps their sum (`BattleUnit.SpellModifiers`, under every `Has(status)`,
  damage roll and AI estimate) until the list changes.
- **Ids**: `GameState` keeps hero and object dictionaries by id beside its lists (and the cell index); `GetHero`, `GetObject` and
  `FindObject` no longer scan. Garrison heroes are still found through their towns.
- **AI threats**: `AdventureAi` keeps each player's visible enemy threats until the next command (only commands change the game, so the
  session's command count is the key); every candidate target and every flooded explore tile used to scout every enemy hero again.
  The 10-candidate cap still counts only safe candidates: counting every fight simulation would change what the AI picks, which is
  tuning, not a cache.
- **Views**: `TownPanel` and `HeroPanel` (29 and 24 event kinds) and the minimap refresh through a `RefreshScheduler`, once at the end of
  the frame. The minimap's tiles are an image, one pixel each, of which only the changed pixels are written again; heroes are drawn over
  it.
- **Behaviour**: unchanged (events, random draws, digests; AI games replay the same).

### ADR-174: Map format upgrades and testing safety nets (code review section 5, 2026-11-11)
**Status**: implemented (2026-11-11); tested. From the code quality review (section 5).

- **Upgrades**: an older map file's JSON is upgraded before it is read (`MapMigrations` in Core.Data `Maps`, `IMapMigration` steps, oldest
  first; each runs on files older than its `Version`), so `MapDocument` and `MapConverter` know only the current format. The steps: v5
  `tactics` → the hero's `skills`; v13 the map-wide `guardMode` → each monster's `mode`; v23 `bank.ruins` → `bank.crypt`. A file without
  `formatVersion` is v1. `MapSerializer.Deserialize` and `GameRecordSerializer.Deserialize` (a save's or replay's `map`) upgrade; a file of
  the current version is read as it is. Gone: `MapDocument.GuardMode`, `HeroPlacementDocument.Tactics`, `PerMonsterGuardModeVersion`,
  `RenamedDefinitions`. A change: the three old forms now count only in files older than their version (before, a current file with
  `tactics` or `bank.ruins` was read the old way). A version that only adds fields needs no step. Upgrading through `JsonNode` reads
  property names without case, as the serializer does.
- **Pins** (`Pinned`: a file next to the test; `EW_UPDATE_PINNED=1` rewrites it): a sample map per format version that has an upgrade step
  (the version before it, and v1) and for the current version (`Data/FormatSamples`, with every object kind), each read and saved again to a
  pinned result; the JSON shape of maps and saves (`Data/FormatShape.txt`: property names, required fields, object and command kinds, enum
  names), so a rename shows; the maps the shipped templates generate (`Generation/GeneratedMaps.txt`), whose re-pin is refused unless
  `MapGenerator.Version` changed. A new map format version needs its sample.
- **Editor undo**: `EditorUndoTests` runs every editor operation (73 cases, a reflection check that none is missing) and compares the
  map file and digest after undo and redo. It found that undoing a removal put the object or hero back at the end of its list, and a
  town's type change moved the town to the end: `GameState.RemoveObject` / `RemoveHero` return the index and the editor puts it back there.
- **Battle copy**: `BattleCopyTests` compares a busy siege battle and its `Copy()` field by field, checks that nothing changed in place is
  shared, and lists the fields of the copied types, so a new field fails until `Copy()` is checked.
- **Architecture**: `ArchitectureTests` checks every project's references against the dependency table (architecture.md).
- **Refusals**: already covered by ADR-165 (`GameSession.CheckRefusals` in every test game).
- **Test cleanup**: `TestFormat` (`Save`, `Load`, `RoundTrip`) and `TestEditor` (`Blank`, `World`) replace the inline round trips and
  editor factories; the milestone-named catalogue tests are split by object (`CartographerAndMerchantTests`, `SphinxAndWitchHutTests`,
  `RefugeeCampAndAltarTests`, `MapGarrisonTests`, `DepositTests`, `MagiWindowAndBorderPostTests`, `MapTavernTests`, …).
- **Behaviour**: unchanged for play (AI games replay the same); the editor's undo of a removal and a town's type change keep the order.

### ADR-202: Every event declares who may learn of it (ADR-193, MP-3, 2026-11-17)
**Status**: implemented. No rule, save or map format changes; the network protocol is unchanged.

- **Why**: a LAN host chose the events it passes to a client by reading their properties (`PlayerViewFeed.Shows`): an owner, heroes,
  objects, a name starting with "Battle", a hand-kept list of public events. An event naming none of them went to **every** client, so a
  new private event, or one carrying its player under another name, leaked to the other players without a test noticing. Map-script
  internals did (`MapVariableChanged`, `MapEventEnabledChanged`, `MapEventCellRemoved`, `MapEventRan` without a player).
- **Rule**: each event type carries `[SeenBy(Audience.…)]` (Core.Domain `Events/SeenByAttribute.cs`): `Everyone`, `Owner` (the player
  its `Player` / `Owner` property names), `Witnesses` (the players who see a hero it names; naming no hero, an object it names),
  `BattleSides`, `Special` (commands, questions, hero moves: `PlayerViewFeed.ShowsSpecial`) or `Nobody` (map-script internals and the
  editors' events). The host reads only the attribute; **an event without one reaches no client**, and a `Witnesses` event that names no
  hero or object at the time is not shown either.
- **Checked** by `EventAudienceTests`: every event declares an audience, an `Owner` event has its `PlayerId` property, a `Witnesses`
  event has a hero or object property, the `Special` events are the ones the host decides.
- **Classified as before**, except: the calendar and the game's end (`GameStarted`, `DayStarted`, `WeekStarted`, `WeekEventAnnounced`,
  `GameEnded`) and `SimultaneousDayConflicted` are declared `Everyone` (they already went to everyone); the map-script events above go
  to `Nobody` (no client view uses them).

### ADR-213: The scenario rules' memory is game state, and the digest has versions (ADR-036, ADR-057, ADR-058, 2026-11-18)
**Status**: implemented. No rule or map format change; saves and replays gain a field (record format 4).

- **Why**: `GameFlowService` kept what the scenario rules remember from the start (the teams that took part, the heroes there were, who sits
  in each prison a "release hero" condition names) in private fields. It was not in `GameState`, so not in the digest, a copied state or
  anything else that reads the state.
- **State**: `GameState.Tracking` (`ScenarioTracking`: `TeamsAtStart`, `Heroes` as `TrackedHero(Owner, Name)` by hero id, `Prisoners` by
  prison id; immutable, sorted by id). `GameFlowService.Begin` sets it when the game starts, `Evaluate` adds prisoners as before; the rules
  read it from the state. `ScenarioTracking.None` before the game starts.
- **Digest versions** (`GameStateDigest.FirstVersion` = 1, `CurrentVersion` = 2): `Compute` and `Describe` take a version, the current one
  when left out. Version 2 adds a `tracking` line once the game has started (a scenario not started tracks nothing, so the generator's and
  the editor's pinned texts hold); version 1 is the text as it was. A new digest field from now on goes in a new version instead of being
  written "only when present".
- **Records**: `GameRecordDocument.DigestVersion` says which version `FinalDigest` was computed with; a file without it was made before
  (version 1), so older replays and saves still check. `GameReplay.Record` writes the current version; `CheckFinalDigest` (used by
  `GameReplay.Replay` and `ReplayPlayer.Open`) checks with the record's version and refuses an unknown one like other content.
  `GameRecordDocument.CurrentFormatVersion` is 4.
- **Checked**: `ScenarioTrackingTests`. The sim's duel baseline (`--template duel --size 72 --days 40 --seed 2`) still gives
  `02E607C4B08FDC80` in version 1 (`A0F1FADCC6D8DEE4` in version 2): the rules play the same.

### ADR-214: Events tell what happened, never ask for it; the turn's end is recorded before `TurnEnded` (ADR-016, ADR-197, 2026-11-18)
**Status**: implemented. No rule, save, map or protocol format change; the order of a few events changes.

- **Why**: View Terrain and map events published `AreaRevealed`, and observatories, Magi windows, the Ruins and cartographers
  `SurroundingsRevealed`, so that `VisibilityService` would reveal the area: the events were requests, and they worked only because
  visibility subscribes first. `TurnService.EndTurn` published `TurnEnded` before it took the turn out of a simultaneous day's open turns.
- **Reveals**: `IFogRevealer.RevealArea(player, center, radius)` (Visibility; `VisibilityService` implements it: explore, refresh memory,
  publish `TilesRevealed` / `KnownObjectsChanged`). `AdventureMagicService` and `MapEventService` call it and then publish `AreaRevealed`;
  the object services call `ObjectInteractionContext.RevealSurroundings`, which reveals and then publishes `SurroundingsRevealed`.
  `VisibilityService` no longer handles either event. A LAN client's session holds the service but never reveals: it runs no rules.
- **Order**: `TilesRevealed` and `KnownObjectsChanged` now come before the reveal event instead of after it; when a view gets the reveal
  event, the fog already shows the area.
- **End of turn**: on a simultaneous day the turn leaves the open turns, joins `DoneToday` and the current player points at the next open
  turn before `TurnEnded`; the next turn (`PassTurn`, with its own events) follows it. Turn by turn, `TurnEnded` comes with the ending
  player still current, as before.
- **Rule**: an event tells of a change already made. A rule that needs another service to change state calls it through a narrow interface,
  then publishes its own event (`event-system.md`).
- **Checked**: `RevealOrderTests`, `SimultaneousDayTests.TurnEnded_ComesOnceTheTurnIsOut_OfTodaysOpenTurns`. The digests are unchanged
  (duel baseline `A0F1FADCC6D8DEE4`, sim self-test `82AE941CD62212E2`).

### ADR-215: One set of decision steps, taken in one order (ADR-016, ADR-214, 2026-11-18)
**Status**: implemented. No rule, save, map or protocol format change; the order of a few events changes.

- **Why**: asking (`PendingDecision` + `DecisionRequested`) was written by hand in five services and resolving in fifteen places, each
  in its own order: some resolvers published `DecisionResolved` before the answer's effects, some after, so a level-up the answer
  brought could be requested before the old decision was resolved.
- **Helper**: `DecisionSteps` (Decisions) with `Ask`, `TakeDecision<T>` (the pending decision of that type, else a refusal),
  `Resolve` (cleared, then `DecisionResolved`) and `RemoveObject` (removed, then `MapObjectRemoved`). The object services use it
  through `ObjectInteractionContext.Steps`; `GuardService`, `ProgressionService`, `BattleStarter`, `BattleAftermath`,
  `MapEventService` and `GameFlowService` hold one each.
- **Order**: take the decision; check the answer, a price that can be refused among the checks; a refusal changes nothing and leaves
  the decision pending. Then resolve, then apply the answer: a decision its effects open (a level-up, a battle) is requested after
  this one's `DecisionResolved`. A battle's decision lasts the battle and is resolved after its consequences, as before.
- **Changes**: `DecisionResolved` now precedes the answer's effects for the treasure chest, the town gate, training, the sphinx, the
  level-up choice and a map event's army choice. The witch hut checks that the skill can be raised before it resolves. A joining or
  fleeing guard leaves the map before `GuardJoined` / `GuardFled` ("object gone" order). A meeting publishes `HeroesMet` before its
  exchange decision is pending. A defeated player's hero with a decision pending now gets its `DecisionResolved`, so a LAN client
  drops it too.
- **Not moved**: an object replaced in place under its id (a bought deposit, a cleared abandoned mine, a retyped town) is not removed;
  a beaten monster leaves from `IBattleGuard.Defeated`, reworked by the battle guard outcome item; the editor removes through its own
  undoable steps.
- **Checked**: `DecisionStepsTests`. The digests are unchanged (duel baseline `A0F1FADCC6D8DEE4`, sim self-test `82AE941CD62212E2`).

### ADR-216: A battle guard describes its outcome; a service applies it (ADR-068, ADR-215, 2026-11-18)
**Status**: implemented. No rule, save, map or protocol format change; no event order change.

- **Why**: `IBattleGuard.KeepSurvivors` / `Defeated` ran the rules inside the map objects: a monster removed itself, a mine replaced itself
  in `GameState`, a creature bank paid its reward through the economy and drew artifacts, each through a `BattleGuardOutcome` that handed
  them the state, the publisher and five services. Entities changed state and published, against "only Domain services mutate".
- **Description**: `IBattleGuard.Survived()` (the guard won, or its attacker fled or surrendered) and `Defeated()` return a `GuardOutcome`
  record naming the object: `GuardUnchanged`, `MonsterSurvives`, `SiteGuardSurvives` (a mine's or a dwelling's stack), `TroopsSurvive`
  (a town's or a map garrison's troops); `MonsterBeaten`, `MineGuardBeaten`, `DwellingGuardBeaten`, `BankPlundered`, `BoxWon`, `TownWon`,
  `GarrisonWon`. Asking changes nothing.
- **Applying**: `BattleGuardService` (Battles; `BattleAftermath` owns it) switches on the record and mutates, then publishes, in the order
  the entities used; the winner is passed in, and outcomes that need one do nothing without it, as before. A won guard that is not an
  `IBattleGuard` gets `GuardUnchanged`. A beaten monster leaves through `DecisionSteps.RemoveObject`. `BattleGuardOutcome` is removed;
  its `KeepTroops` is the service's.
- **Checked**: `BattleGuardOutcomeTests`, and the existing battle and object tests for what each outcome does. The digests are unchanged
  (duel baseline `A0F1FADCC6D8DEE4`, sim self-test `82AE941CD62212E2`).

### ADR-219: Per-kind rules are handler classes in registries (ADR-120, 2026-11-19)
**Status**: implemented. No rule, save, map format or protocol change.

- **Why**: rules that differ by kind lived in long switches: `SpellEffect.Parse` (one method, 58 cases), the `MapEventService` action
  switch (about 25 types, plus the message-reward and place switches), `AdventureMagic.Cast`, `ScenarioCondition.Kind` (switched on in
  six places) and `MapValidator`. A new kind meant finding every switch.
- **Pattern** (as `SpellEffectHandlers`): one small class per kind behind an interface, a base class with the defaults, a static registry
  (`All`, `For`) that throws on a kind it does not know, and a test per registry that fails while any kind is missing or doubled.
  - Battle effect grammar: `Magic/Parsing/SpellEffectHeads` (per `SpellEffectKind`) and `SpellEffectWords` (the keyword table; a
    kind-bound word comes before the general one); `SpellEffectReader` keeps the cross-word checks.
  - Map event actions: `MapEvents/Actions/MapEventActionHandlers`, keyed by action type (`Apply`, `Fix`, `Describe`, `Place`; more in ADR-237).
    `AdvanceQuestAction` stays nested in `MapEventService` so its saved type name does not change.
  - Adventure spells: `Magic/AdventureSpells/AdventureSpellHandlers`, keyed by spell id; `AdventureCast` carries the cast.
  - Scenario conditions: `Game/Conditions/ScenarioConditionHandlers`, one class per kind with what it means for the game's end, its
    sentences and the validator's issue. A kind the enum does not define (from a map file) keeps the old fallbacks.
  - Map checks: `Editing/Checks/MapChecks.All`, run in a fixed order; `MapValidator.Validate` runs them.
- **Stays a switch**: `MapConverter.ToDocument` (Core.Data cannot see the handlers; it picks the stored fields) and the editor's
  `ConditionsPanel` (Godot UI).
- **Checked**: `SpellEffectParsersTests`, `MapEventActionHandlersTests`, `AdventureSpellHandlersTests`, `ScenarioConditionHandlersTests`,
  `MapChecksTests`; messages, issue order, event order and the sim digests are unchanged.

### ADR-222: The game app's policy lives in a `GameDirector`, a `ReplayController` and `GameScreens` (ADR-059, ADR-091, ADR-164, 2026-11-19)
**Status**: implemented; extended by ADR-231 (the policy `Main` still held). No save, map or protocol change.

- **Why**: the game app's `Main` held when the computer players play, when the turn timer runs and ends a turn, which colours are played on
  this computer, what a LAN host announces, the replay viewer's stepping and which screen is open. Apps hold composition and input mapping
  only.
- **Parts**:
  - `GameDirector` (Core.Domain, Game): built per shown game from the session, a `LocalSeats` and whether it is watched. It owns the
    `AdventureAi` and the `TurnTimer` (neither in the replay viewer, nor for a session following a LAN host) and answers once a frame:
    `RunClock` (the clock stands still while computer moves are shown or the game is paused; a time that is up ends the turn unless a
    screen here holds a timed colour played here; a clock shown from elsewhere gives its warnings), `TakeTurnToShow` and `PlayComputer`
    (not while a screen holds the game, while paused, or, where battles are played elsewhere, during a battle). `PlaysHere`,
    `PlaysElsewhere` and `Viewer` say whose game this screen shows.
  - `LocalSeats` (Core.Domain, Game): the colours played elsewhere, the pause, how the timer's commands run, where its clock goes, a clock
    shown from elsewhere, and whether the computer waits for battles. `LanSeats.Of(GameHost)` and `LanSeats.Of(GameClient)` (Core.Network)
    make a host's and a client's. `LanAnnouncer.Describe` is what a host announces.
  - `ReplayController` (Core.Data, Records): over a `ReplayPlayer`, playing or paused (the end pauses it), the speed, the viewer's eyes,
    the moments at a turn's start and on a battle's result, seeking, skipping a battle and playing one on; `Advance` says what a frame did.
  - `GameScreens` (Godot.UI): the setup layer and its Escape, the F10 menu and the placing of its button, and the swap between the map and
    the battle scene (the chat of a LAN game moving with it).
- `Main` builds the views, passes them on and maps input. The AI's construction, the timer's ticks and the commands they give come in the
  same order as before.
- **Checked**: `GameDirectorTests`, `LanSeatsTests` and `ReplayControllerTests`; the duel baseline still gives `A0F1FADCC6D8DEE4` and the
  sim self-test `82AE941CD62212E2`; the game's smoke frames compared before and after.

### ADR-223: The town and hero screens are sections; the inspector has one section per object type (ADR-046, ADR-191, 2026-11-19)
**Status**: implemented. No rule or map format change; the screens look and read as before.

- **Why**: `TownPanel` (about 800 lines) rebuilt the whole screen on any of 28 events, `HeroPanel` on any of 25, and `InspectorPanel`
  (about 800 lines) was one switch over the object types that rebuilt at once after every edit. The same owner drop-down was written five
  times, and colours and widths were literals in each panel.
- **Parts** (Godot.UI):
  - `ScreenSection<TSubject>` (Hud): a part of a screen with its own `RefreshScheduler` on one of its controls and its own events; it
    rebuilds alone, once a frame, and only while the screen shows its subject. The screen lays the sections' controls out and rebuilds them
    all when it opens and on the events that change what it shows at all.
  - Town screen (`Hud/TownScreen`): `TownHeaderSection`, `BuildingTreeSection`, `DwellingsSection`, `GovernorSection`, `TownArmiesSection`,
    `CaravanSection`, `TransformerSection`, `BlacksmithSection`, `TavernSection`. The whole screen: owner, decision asked or answered, turn,
    building raised or downgraded, governor (every price). The picked stack and the ticks stay the screen's; the transformer keeps its own
    choices.
  - Hero screen (`Hud/HeroScreen`): `HeroHeaderSection`, `HeroStatsSection`, `HeroSkillsSection`, `HeroArmySection`,
    `HeroArtifactsSection`, `WarMachinesSection`, `SpellBookSection`, `AdventureSpellsSection`. The whole screen: turn, hero defeated.
  - Inspector (`Editor/Inspector`): one `InspectorSection<T>` per map object type in the `InspectorSections` registry (a second section
    for a type throws when it is built; a type without one shows only its caption, as fields stay optional for a kind), `HeroInspector`
    for heroes, and `InspectorForm` for what they share (the edits, a garrison's rows, an event link). It fills again once a frame after
    `UndoHistoryChanged`.
- **Helpers**: `FormBuilder.AddOwnerChoice` (no owner first, then the players); `UiTheme` tokens for text tints (`TextCaution`,
  `TextQuiet`, `TextDone`, the building states, `TextRefusal`, `IssueError` / `IssueWarning`) and the screens' and inspector's widths and
  slot sizes. Drawing colours (the battle board, the template graph, the puzzle) stay with their drawing code.
- **Checked**: build and tests; the smoke scenarios that show the town screen, the hero screen and the inspector, frame by frame before and after (PNG): the same pixels wherever the new game's random seed, the pointer and the computer's turn are the same.

### ADR-224: The sim tool's commands are classes with strict options; saves stream to disk and list from their label (ADR-034, ADR-058, 2026-11-19)
**Status**: implemented. No rule, save or map format change; the sim's output and exit codes for valid command lines are unchanged.

- **Why**: the sim's `Program.cs` held every command in 1,100 lines, and its option reading ignored a typo (`--day 5`), a missing value and a
  bad one (`--runs x` ran with the default). Saves were built as one string before being written, and the save menu deserialized every
  save in the folder, map and commands included, to show its label.
- **Parts**:
  - Sim commands (`tools/EternalWars.Tools.Sim/Commands`): one `SimCommand` class per command (`BattleCommand`, `RecordCommand`,
    `ReplayCommand`, `PlayGameCommand` for `game`, `MapsCommand`, `TownsCommand`, `AiCommand`, `CreaturesCommand`, `LinesCommand`,
    `LayoutCommand`, `GenerateCommand`, `ExportCommand`, `FairnessCommand`, `SelfTestCommand`) naming its positionals and options;
    `SimContext` carries the content and the AI weights; `SimGames`, `DemoGame`, `Fights`, `FightLog`, `TurnProfile` are shared.
  - `CommandLine` parses strictly: an unknown option, a missing value, a bad value (not a number, below the option's minimum, not one of
    its choices), an option given twice, a missing or extra positional or a missing required option is `error: ...` naming the option and
    listing what the command takes, exit code 2. `--creatures` and `--weight` are taken from anywhere; `--weight` only for `game`, `maps`
    and `selftest`; `game` refuses `--map` with `--template` and `--size`/`--players` without it.
  - `GameRecordSerializer.Save` writes through a `FileStream` as it serializes (`Write(record, stream)`): the same bytes as before
    (UTF-8, no byte order mark), still to a `.tmp` file moved into place.
  - `GameRecordLabel.Read`: a `Utf8JsonReader` pass over the file a buffer at a time that takes the map's name on the way, steps over the
    map and the commands without building them and stops after the `save` object. The format keeps the label after the commands (no
    format change: every file reads as before); a save whose commands no longer load still lists, and loading it reports the error.
    `GameSaves.List`/`ListReplays` use it; a file whose JSON breaks before the label is listed as unreadable.
- **Checked**: `SimCommandLineTests` (the tests reference the sim project), `SaveFileStreamTests` (same bytes as the text, a 100,000-command
  save, labels cut at any buffer size, damaged commands, a file cut after its label, unreadable files, a version 1 save with a byte
  order mark); the sim digests and the output of every command compared before and after (times aside).

### ADR-231: The game app's remaining policy moves to core: autosave, rematch, a save's seats, LAN rules, replay recovery, the map's focus (ADR-222, ADR-226, 2026-11-19)
**Status**: implemented (2026-11-19), the roadmap's "Code health" item; extends ADR-222. No rule, save, map or protocol change.

- **Why** (the refactoring close-out, ADR-226): after ADR-222 the game app's `Main` still decided when to autosave, built the rematch,
  set a loaded save's colours going, held the LAN lobby's and the game list's rules, recovered a replay that diverged and chose where the
  camera goes. Apps hold composition and input mapping only, and none of it was tested.
- **Parts** (engine-free, each with its tests):
  - `AutosaveSchedule` (Core.Domain, Game): once a game has started, then after the command that began each turn; never in the replay
    viewer nor on a session following a LAN host (`AutosaveScheduleTests`).
  - `MapFocus` (Core.Domain, Game): the computer hero followed when it starts walking in sight (once, until another walks or a turn
    starts; the replay viewer follows every hero on either level), a map event's place, the selected hero teleported to another level,
    the hero selected when a turn is shown and when the selected one is defeated (`MapFocusTests`).
  - `GameRematch` (Core.Data, Records): the start map with the finished game's options and a new seed; a LAN host's keeps who plays each
    colour. The front-end draws the seed as the new-game screen does: a new game's seed is not a rule's randomness.
  - `SavedGameStart` (Core.Data, Records): whether a save asks who plays (more than one person still in it), the colours handed to the
    computer by command, and a simultaneous day loaded on one computer going on turn by turn (`SavedGameStartTests`, also the rematch).
  - `LanAddress`, `LanListing`, `LanAccept` (Core.Network): an address typed to join, a found game's line and whether it can be joined, who
    plays a lobby's colour and whether it can be taken (`LobbyState.Open` uses the same rule), and where a hosting computer's new connection
    goes (`LanRulesTests`).
  - `ReplayController`: `Advance` takes `ReplayViews` (the menu, heroes' steps and a battle's animations hold it; a result stays its
    moment); a diverged game is put back where it was on a new session, paused, before `Diverged` is returned; `CanPlayFromHere`
    (`ReplayControllerTests`).
- `Main` asks these and moves the views; what it shows and the order of commands are unchanged. The 37 game smoke scenarios pass after it.

### ADR-232: Code health: Continue falls back to a save that loads, the events panel refreshes once and while shown, every AI tuning number is a weight, leftover cleanups (ADR-220, ADR-224, ADR-226, 2026-11-19)
**Status**: implemented (2026-11-19), four of the roadmap's "Code health" items; extends ADR-220. No rule, save, map or protocol change;
the AI plays as before.

- **Continue** (ADR-224 lists saves from their labels alone, so a save whose commands no longer load still offered Continue):
  `GameSaves.LoadNewest` loads the newest save that loads, passing over newer ones whose label reads but whose game does not, and names
  them (`SkippedSave`, `SaveSlotText.Skipped`) on the status line or the saved-game screen; when none loads, the save menu opens and says
  why. Files whose label cannot be read never offer Continue. `GameSaves.TryLoad` is the one place that turns a load's exceptions into a
  reason (`ContinueSaveTests`).
- **The editor's events panel** rebuilt itself whole for every `UndoHistoryChanged`, one deferred call each, also while another tab was
  shown, and ran the validator twice a rebuild. It refreshes through a `RefreshScheduler` (once for a burst of edits), and
  `RefreshScheduler.OnlyWhileShown` holds a refresh while the view is hidden, running it once when it shows again; the validator runs once a
  rebuild.
- **AI weights**: the tuning numbers left inline in `TargetValuer`, `TownManager`, `DecisionResponder` and `AiContext` are `AiWeights`:
  `PlausiblePercent` (70), `ArtifactValue` (800), `ArtifactScoreValue` (80), `BankArtifactValue` (1500), `ChestValue` (1500),
  `BoxCreatureShare` (0.85), `TroopsTripPercent` (60), `TroopsTripShare` (0.3), `TroopsTripFloor` (1000), `TrainingCushion` (3000),
  `KeptGoldPart` (3: a third of the gold kept), `CartographerGoldTimes` (3), `BuildGoldDays` (3), `BuildResourceDays` (7), `MostHeroes` (4).
  Comparisons that were on integers stay on integers (a percent), so the defaults decide exactly as the literals did. The expected values of
  objects' rules (a training site's, a buff's, a loot site's worth in gold) stay in `TargetValuer`: they translate a rule, they are not
  tuned. `AiWeights.With` refuses a negative value.
- **Cleanups**: a "release hero" condition's prison is tracked by its handler (`IScenarioConditionHandler.Track`), not by a kind test in
  `GameFlowService`; the handler interface's comment names the validator's rules check. The game app's views and handlers hold the game's
  `GameDirector` itself, so a callback after `LeaveGame` no longer dereferences the cleared field. The map chat's place and ground are
  `UiTheme` tokens, set by `ChatPanel.OverTheMap`.

### ADR-236: A LAN client's view writes move the change stamp; a player's sight is handed out as a copy (ADR-205, ADR-218, ADR-226, 2026-11-19)
**Status**: implemented

- **Change stamp**: `GameSession.ChangeStamp` (ADR-205) counted commands, events and hero moves, but a LAN client's state changes through
  its `IViewSink` (ADR-218), which does none of these. A route cached by `HeroMovementService.FindPath` could then outlive the host's write
  that changed the map (the hover preview showing a route through a hero that just arrived). The sink now counts its writes, and the stamp
  adds them.
- **Sight**: `FogOfWar.SightOf` returns a copy of the player's tiles in sight, not the live set, so no reader can see it change under it
  or cast it back and change the fog. Its one hot reader, `PlayerViewFeed`, reads it once a send.
- Tests: `ViewWritesTests`.

### ADR-237: A map event action's handler tells what the action holds, its editor line and its worth to the AI (ADR-219, ADR-226, 2026-11-19)
**Status**: implemented. No rule, save, map format or protocol change.

- Domain switched on the map event action type outside its handlers (ADR-219) in three places: `MapEventTree` (the lists, conditions and
  expressions an action holds), `MapEventText` (the editor's line) and the AI's event worth (`TargetValuer`). A new action had to be added
  to each, and nothing failed when one was missed. They are now members of `IMapEventActionHandler`:
  - `Lists`, `ListNames`, `WithLists`: the action lists an action holds, their names in the editor, and the action with them replaced
    (`MapEventTree.Actions` and `MapEventTreeEdit` use them).
  - `Conditions`, `Expressions`: the action's own conditions and expressions (the tree walks flatten them).
  - `Line`: the editor's line (`MapEventText.Describe(action)`), abstract, so every handler has one.
  - `Worth`: the action's worth to the AI in gold and whether it ends the event. The handler prices it through `IMapEventValuer` (the
    event's hero, town and player, expressions at their averages, conditions, the worth of nested actions, a battle's estimate and an
    artifact's value), which the target valuer implements. The numbers are as before.
  - `IsReward`: whether the action gives or takes something (else it steers the event); the arena's preparation-day look at a quest lists only rewards.
- `MapEventActionHandlersTests` checks every handler against its action's fields: a reward with an amount draws it in `Fix` (a
  message's line would otherwise show the formula); a handler overrides `Expressions`, `Conditions` and the list members exactly when its
  action holds an expression, a condition or action lists.
- The checks that look for particular actions (`MapEventChecks`, `MapEventRules.Problem`, the editor's event rename) stay where they are:
  they ask about a few actions, not every type.
