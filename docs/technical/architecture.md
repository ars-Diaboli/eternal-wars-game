# Architecture

## 1. Goals
- **Game logic is engine-independent**: rules run and are tested without Godot (headless sim, AI, tools).
- **Three front-ends, one core**: the 3D game, the map editor and the 2D harness drive the same core services.
- **Event-driven presentation**: core publishes facts on a custom bus ([event-system.md](event-system.md)); views
  subscribe and render, never poll.

## 2. Tiers

```
Tier 3 · Apps (Godot projects)       EternalWars.Game (3D, Forward+), EternalWars.MapEditor (3D, Forward+),
                                     EternalWars.Harness2D (2D debug, Compatibility)
Tier 2 · Godot libraries             EternalWars.Godot.UI      HUD, panels, editor and setup screens, debug overlays
         (GodotSharp)                EternalWars.Godot.Shared  map/hero/battle views, cameras, input, event glue, GameContent
                                     EternalWars.Godot.Assets  models, textures, shaders, prefabs (no code, no csproj)
Tier 1 · Core (plain .NET 10)        EternalWars.Core.Data        file formats: maps, saves/replays, settings, templates
                                     EternalWars.Core.Generation  random maps: template model, validator, generator stages
                                     EternalWars.Core.Domain      model + rules, commands, events, AI, editing
                                     EternalWars.Core.Events      pub/sub primitives
tests/EternalWars.Core.Tests   xUnit v3, covers Tier 1
tools/EternalWars.Tools.Sim    headless console app over Tier 1 (see development.md)
```

### Dependency rules

| From \ To | Core.Events | Core.Domain | Core.Generation | Core.Data | Godot.Shared | Godot.UI |
|---|---|---|---|---|---|---|
| Core.Domain | ✓ | — | ✗ | ✗ | ✗ | ✗ |
| Core.Generation | ✓ | ✓ | — | ✗ | ✗ | ✗ |
| Core.Data | ✓ | ✓ | ✓ (template format) | — | ✗ | ✗ |
| Godot.Shared | ✓ | ✓ | (avoid) | (avoid) | — | ✗ |
| Godot.UI | ✓ | ✓ | ✓ (template editor) | (avoid) | ✓ | — |
| Apps | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Core.Events depends on nothing; apps never reference each other. Enforced by `Directory.Build.targets` (build fails if a
`src/Core` project uses `Godot.NET.Sdk` or references `GodotSharp` or a `*Godot*` project) and by `ArchitectureTests` (every project's
references against this table); `Directory.Build.props` builds core with `TreatWarningsAsErrors`.

## 3. Runtime composition

```
Player input ──► App (Main.cs) ──session.Execute(cmd)──► GameSession (Core.Domain)
                                                           │ services validate, mutate GameState, publish
Godot views ◄──subscribe── session.Events (EventBus) ◄─────┘
```

- **`GameSession`**: composition root of a running game: one `GameState`, one `IEventBus`, the services
  (`TurnService`, `HeroMovementService` + `PathFinder`, the map object services, `EconomyService`,
  `VisibilityService`, `ArmyService`, `ProgressionService`, `ArtifactService`, `BattleService`, `GuardService`,
  `TownService`, `TownVisitService`, `CaravanService`, `AdventureMagicService`, `GameFlowService`). The map object services share one
  `ObjectInteractionContext` (state, publisher, economy and the common steps: `Ask`, `RemoveFromMap`, `TryCollect`,
  `GiveResources`, `HeroAt<T>`): `MapObjectInteractionService` (visits via `VisitRules`, the reward objects' rules in
  `MapObjectRewardVisits`; hero meetings), `ObjectDecisionService` (the answers to the decisions visits open),
  `ObjectShopService` (what a hero does at an object from its panel: recruit, buy, upgrade, hire, sacrifice, lessons,
  garrison troops), `ObjectLifecycleService` (game-start rolls and prisoners, the weekly refugees / merchant stock),
  `GrailService` (burying the Grail, obelisk visits, digging; the rules in `GrailRules`) and `PandorasBoxService` (opening a box,
  after a yes or a won guard battle). `TownVisitService` is every hero's arrival in a town (governor, Grail building, unique
  buildings' bonuses, guild spells, mana after a night) and every capture; the movement, decision, battle, map event and
  adventure-magic services all go through it. `TownConstruction` raises a building or dwelling level for both the day's build and
  a map event's free build (a guild, war lodge, school or unique building lets the town's heroes take it at once). A hero joining a
  player (town or map tavern, prison, map event) goes through `HeroRecruitment`; an artifact taken away (Grail building, altar, map
  event) through `ArtifactService.Remove`. Game start: `PlaceholderService` draws the placeholders, then the Grail, the
  prisoners, the objects' rolls and the guild spells (`TownService.DrawGuilds`) are drawn in that order.
- **`*Rules` classes are pure**: static `*Rules` classes hold constants and queries (checks, costs, odds, offers) and never
  change the state or publish; the changes they lead to belong to a service. `RulesPurityTests` fails on a `*Rules` method that
  takes an `IEventPublisher` or returns nothing.
- **Commands only** (ADR-036): front-ends, AI, tools and tests change the game with `session.Execute(GameCommand)`;
  service mutators are `internal`. Accepted commands go to `session.CommandLog`; map + seed + log replay the game
  (`GameReplay`).
- **Executing a command**: `Execute` authorizes it, then hands it to its handler (`CommandHandlers`, one
  `ICommandHandler<T>` per concrete command type; adding a second throws). Authorization: an `IBattleCommand` needs a running
  battle, whoever's turn it is (the battle checks the side); `SetAutoCombatCommand` is allowed any time; everything else only in
  the player's turn, and an `IHeroCommand` only for an own hero on the map (`Hero`; `MoveStackCommand` maps it to `FromHero` and
  also needs its `ToHero` on the map). Other commands that name a hero (artifacts, governors) leave that check to their service.
- **Adding a command**: a `sealed record … : GameCommand(Player)` in `Commands/GameCommands.cs`, marked `IBattleCommand` or
  `IHeroCommand` when it is one; one line in the area's registration (`AdventureCommandHandlers`, `ObjectCommandHandlers`,
  `TownCommandHandlers`, `ArtifactCommandHandlers`, `BattleCommandHandlers`): `handlers.Add<T>(c => service.Method(...))`, or
  `AddWithOutcome<T>` when the result goes back in `CommandResult.Outcome`; and its replay name in `GameRecord.CommandTypes`
  (Core.Data). `CommandHandlerTests` fails for a command without a handler.
- **Turns**: `TurnService` moves the calendar (players, days, weeks), publishes `GameStarted` / `DayStarted` / `WeekStarted` /
  `WeekEventAnnounced` / `TurnStarted` / `TurnEnded` and rolls the week's events; everything else happens in the
  `ITurnPhaseParticipant`s the session gives it, called hook by hook (`SetUpGame`, `StockFirstWeek`, `StartDay`, `StartWeek`,
  `ApplyWeekEvent`, `StockWeek`, `OpenDay`, `PrepareTurn`, `OpenTurn`) in list order: map events, progression, movement, mana
  (`ManaRestoration`), towns, caravans, economy, guards, map objects. The order fixes the events and the random draws, so it is
  part of the rules. An arena's battle day replaces the day (`ArenaService.StartBattle`).
- **Core subscriptions**: services that react to events list them in their own `SubscribeTo(IEventSubscriber)`
  (`VisibilityService`, `MapEventService`, `AdventureMagicService`); the session calls them, visibility first, before any view
  subscribes.
- **Services** validate, mutate `GameState`, then publish past-tense events. They depend on `IEventPublisher` only.
  A rule refuses a command by throwing `CommandRefusedException` (an unknown id from `GameState.GetHero` / `GetObject` /
  `GetPlayer` does too) before it changes anything; `Execute` turns only that into a rejected `CommandResult`, so any other
  exception is a bug and goes on up. The tests turn on `GameSession.CheckRefusals`, which fails a refusal that changed the
  digest (ADR-165).
- **Content tables** travel together as `ContentCatalogs` (creatures, heroes, buildings, artifacts and their spells, map
  objects, terrains; each left out is the built-in table, `ContentCatalogs.Default` all of them). Everything that builds a
  game takes one record and no separate tables: the scenarios, `MapConverter.ToGameState(document, content, options)`,
  `GameSaves.Load` / `GameReplay.Replay` / `ReplayPlayer.Open`, `MapGenerator` and `EditorMapGenerator`. Each app builds one
  record (the Godot apps `GameContent.All` from the `--creatures=`-style flags, the Sim tool from `--creatures`) and passes it on.
- **`GameState`** is plain data with `internal` setters. It carries `GameOptions` (fog mode, refog margin, seed; game
  rules, not map data, so passed to `MapConverter.ToGameState(document, content, options)`) and `GameState.Random`
  (`GameRandom`), the only randomness rules may use.
- **Pending decisions**: outcomes needing a choice (chest, exchange, guard offer, level-up, battle, ...) set
  `GameState.PendingDecision` and publish `DecisionRequested`; movement and End Turn are refused until the matching
  command resolves it. A battle is a `BattleDecision` resolved when the battle ends. Core never waits; UI or AI answers
  whenever it wants.
- **Settle**: after `Start()` and every accepted command `GameSession` runs `GameFlowService.Evaluate()` (eliminations,
  winner, passing the turn of a just-defeated player), then publishes `CommandExecuted`. Once `GameState.Outcome` is set,
  `Execute` refuses everything. `TurnService` skips defeated players; `GameState.Rules` (`ScenarioRules`) says what counts.
- **Click-to-move**: `Execute(MoveHeroCommand)` → `MoveTo` → `PathFinder` → per step `TryStep` (validated by
  `MovementRules`, publishes `HeroMoved`) → on an entrance `MapObjectInteractionService.Visit` → outcome events /
  `DecisionRequested`. A step towards a monster or into its guarded area goes to `GuardService`; onto an enemy hero to
  `BattleService`.
- **Views** subscribe with `node.SubscribeWhileInTree<T>(...)`. Code-built forms use `FormBuilder` (Godot.Shared/Controls:
  `AddRow`, `AddSpin`, `AddOptions`, `AddChoice<T>`) and empty a container with `node.ClearChildren()`. A view that rebuilds on many
  events uses a `RefreshScheduler` (Godot.Shared/Events: `.On<T>(events)`), which refreshes once at the end of the frame however many
  asked. **Apps** hold composition and input mapping only.

### Feature map (Core.Domain unless noted)

| Area | Key types and rules |
|---|---|
| Artifacts (ADR-061, ADR-121) | `ArtifactCatalog` (from `artifacts.csv`), `HeroArtifacts`, `ArtifactBonuses` read via `Hero.TotalAttack` / `SkillLevelOf` / `Bonus`; call `GameState.RefreshArtifacts` whenever equipment changes (incl. hero joining, editor edits). `ArtifactService` equips, gives, picks up, loots, `Draw(tier)` (relics from `GameState.DrawnRelics`). |
| Marketplace (ADR-067) | `MarketplaceRules` rates; `TradeResourceCommand` → `EconomyService.Trade`. AI does not trade. |
| Tavern (ADR-060) | `TavernRules` (offer = stateless weekly shuffle; hire allowed?), `TownService.HireHero`. Tavern is a building level in `buildings.csv`. |
| Hero limit (ADR-068) | `ScenarioRules.MaxHeroes`, `Player.MaxHeroes`; `GameState.HeroLimitReached` is the one check. `GameSession.Start` removes surplus heroes. |
| Adventure AI (ADR-059, ADR-119) | `AdventureAi` (`AI/`) plays only through `Execute`; `BattleEstimator` runs the real engine on a throwaway state; `Explain` for debugging; an arena's preparation day in `AdventureAi.Arena.cs` (ADR-160). The game app calls `PlayTurn()` once a frame for computer players. |
| Magic (ADR-110, ADR-120, ADR-122) | `Magic/`: `Spells` (from `spells.csv`), `MagicRules`, `AdventureMagic` / `AdventureMagicService`; battle casting in `Battles/BattleMagic`, effect kinds in `Battles/Spells/` (see Battles). |
| Map format (ADR-174) | `MapDocument` is the current format only; `MapSerializer` / `GameRecordSerializer` upgrade an older file's JSON first through `MapMigrations` (`IMapMigration` steps by version). Pinned in tests: a sample per version with a step and the current one, the JSON shape of maps and saves, and generated maps per `MapGenerator.Version`. |
| Saves (ADR-058) | `GameSaves` (Core.Data): initial map + options + command log, digest-checked on load; works mid-battle since battle actions are commands. Autosave hooks `CommandExecuted`. Game app builds its nodes under one `Game` node (freed on load); `BattleView3D.Restore` shows a running battle. |
| Arena (ADR-097, ADR-158) | `ScenarioRules.Arena`; `ArenaRules` (preparation day: `MovementAllowance.Daily(state, hero)`, `TownRules.HasBuiltToday`; `Plan` chooses sides and champions); `ArenaService.StartBattle` runs from `TurnService` at day 2 instead of a turn; `BattleService` records the result in `GameState.Arena` (no experience, no AI retreat); `GameFlowService` ends the game from it. `CombatPlace` object. The editor's checks in `MapValidator.Arena.cs`, the contents shown on day 1 in `AdventureIntel.ArenaContents` (ADR-161). |

### Battles
- `BattleState` (`GameState.Battle`): field (width × 24 cells), units with footprints, obstacles, round, queue, own
  forked RNG. `BattleGrid`: pure geometry (reach, adjacency, line of fire, cover). `BattleEngine`: rules for the active
  unit's actions and blows; it builds one `BattleWounds` (damage, deaths, healing, Martyrdom shares, blind wake-ups; shared per action),
  `BattleSpellResolver` (casting; each effect kind's handler does the rest) and `BattleRounds` (turn order, round start, end of battle) per instance. `BattleAi`: computer
  sides. `BattleService`: the session's facade for map ↔ battle, composed of `BattleStarter` (sides, battlefield, siege, width choice),
  `BattleCommandHandler` (players' battle commands, auto combat preview), `BattleFlow` (deployment, AI play until a person acts) and
  `BattleAftermath` (mana, survivors, `NecromancyRules.Raise` after survivors and before experience, defeated heroes). A guarded map object
  implements `IBattleGuard` (keeps its survivors, or is defeated: removed, flagged, plundered, opened, captured) with a `BattleGuardOutcome`.
  `Siege`, `WarMachines`,
  `PrimalFury` (rage, warcries via `BattleWarcryCommand`), `SurrenderRules`, `AutoCombat` beside them.
- Battle spells: each `SpellEffectKind` has one `ISpellEffectHandler` (`Battles/Spells/`, registered in `SpellEffectHandlers`,
  looked up with `For(kind)`) holding that kind's rule and its AI value side by side: its own aim checks (`CastRefusal`, `AimRefusal`,
  `AffectedRefusal`), how a cast works (`Apply`), what the AI thinks it is worth (`Value(CastContext)`) and where the AI aims a
  hero's cast (`Aims`). `CastContext` bundles the battle, side, spell at its rank, target, cell, second aim and a creature caster's
  Spell Power. The checks and numbers every kind shares stay in `BattleMagic` (aim legality, reach and area, `Affects`, the damage
  plan used by the damage handler and the AI, amounts and durations); `BattleSpellResolver` pays and reports the cast and keeps the
  shared frames (`OnReach`: resisting stacks, Earthquake, wake-ups; Raise Dead's `Rise`); `BattleAi` only dispatches. A new effect
  kind is its parse in `SpellEffect.Parse` plus one handler class registered in `SpellEffectHandlers` (`SpellEffectHandlersTests`
  checks every kind has exactly one).
- Creature special rules are data (`CreatureCapability`, max `CreatureDefinition.MaxCapabilities` = 5, ADR-042); code
  asks `creature.Has(kind)`.
- Battle commands are authorized per side, not per map turn (hot-seat defender acts during the attacker's turn).
- Morale per stack at round start (`BattleUnit.RoundMorale`), luck per hit; values from `BattleState.MoraleOf` /
  `LuckOf` / `InitiativeOf` (creature + hero + timed `BattleSideEffect`s). **Nothing is rolled at 0**, so other battles'
  random sequence is unchanged.
- War machines (ADR-056) are `BattleUnit`s with `Machine` set: speed 0, no melee/retaliation/morale; only the offensive
  one is queued; one-column footprint (`WarMachines.Footprint`); `BattleState.LivingArmyOf` (creatures only) decides the
  end.
- Deployment (ADR-072, ADR-076): `BattlePhase.Deploying` for a human side with higher Tactics; `BattleDeployCommand` →
  `Redeploy` (checked by `BattleSetup.DeploymentRefusal`), `BattleStartCommand` → `EndDeployment`.
- Front-ends map a pointed cell to an action with `BattleTargeting.ActionAt` / `DeploymentPlacement`, and an action to its command
  with `BattleTargeting.CommandFor` (`CommandAt` does both).
- The end of a battle is read once into a `BattleOutcome` (winner, reason, losses, the winning hero's experience: none in an arena
  battle, the defeated hero's artifacts). `BattleAftermath.Finish` applies it and auto combat's `Preview` shows it, with the undead raised
  into the army the hero fought with (a held town's garrison for a garrison hero) from a copy of the random numbers.
- Presentation (ADR-052): `BattleView3D` (Godot.Shared/Battle) builds from `BattleStarted` and plays later events as
  one sequential animation queue (a whole AI turn resolves inside one command); it takes input only once the queue has
  caught up. It subscribes and coordinates: `BattleStage` (the field's live pieces and the step each event becomes),
  `BattleFieldBuilder` (field nodes; each material made once per builder),
  `BattleAnimationQueue` (the sequenced tween playback), `BattleAnimations` (tween recipes) and the engine-free
  `BattleNarrator` (Core `Battles/`: the combat-log line of each battle event, usable headless). `BattleHud`
  (Godot.UI/Battle) overlays it. The harness's `BattleBoard2D` redraws from live state.

### Map objects
- **Definition** (`MapObjectDefinition`, data: id, `Kind`, footprint, kind parameters) vs **instance** (`MapObject`
  subclass per kind: id, top-left position, mutable state). Built-in definitions in `BuiltInMapObjects`; JSON
  additions via `MapObjectDefinitionSerializer` + `MapObjectCatalog.With(...)`. An object may turn into another
  definition of `GameState.ObjectCatalog` (same id). Owned objects implement `IFlaggable`.
- **Footprint** rows: `B` blocked, `V` entrance (exactly one), `.` free. `GameState` indexes occupied cells (O(1) lookup).
- Behaviour the rules ask of any object is declared on the instance (`EndsRoute`, `TeleportTarget`, `IsPickup`,
  `IsDefendedAgainst`, `DigestState`), so pathfinding, movement and the digest have no per-kind code.
- **New kind** (ADR-166) = enum value + subclass (the compiler asks for its `DigestState`) + entry in `MapObjectKinds` + rule in
  `MapObjectInteractionService.VisitRules` + document type and codec in `MapConverter.ObjectCodecs`; `MapObjectKindsTests` fails
  until all of them exist. Optional, with safe defaults: AI targets (`AdventureAi`), validator checks, info sheet, editor inspector
  fields. Kind-specific rules sit in `*Rules` classes (`LootRules`, `BuffRules`, `CreatureBankRules`, ...).
- **Rewards (ADR-068)**: definition `VisitScope` + instance `VisitLog` (`MapObject.Visits`, game state only), checked and
  recorded by `ObjectInteractionContext.TryCollect`. Temporary `HeroStatus`es live on the hero, granted/expired via
  `ProgressionService`. Random rewards use `WeightedRoll.Pick`.

### Terrain, roads, teams, knowledge
- Terrain rules are data (`GameState.Terrains`, `TerrainCatalog`, ADR-045). **All movement cost goes through
  `MovementRules.StepCost`** (road → road, else terrain being left, ×1.41 diagonal; `ArmyTown` native-terrain rule);
  `PathFinder` uses it too, so a planned route costs what walking it costs.
- Roads and rivers are extra per-tile layers on `AdventureMap`.
- Teams: `Player.Team`; `AreAllies` / `AreEnemies` drive encounters (own → exchange, enemy → battle, ally → blocked).
- `FogOfWar` and `ObjectMemory` (last known object state per player) are maintained by `VisibilityService`. **Player
  views draw objects from memory, never live state.**

### Random map generation (ADR-064)
- `MapGenerator.Generate` runs static stages in order on a `GeneratedMap`: `ZoneLayouter` → `TerrainPainter` →
  `SceneryPlacer` → `ConnectionPlacer` (+ `GuardPicker`) → `SettlementPlacer` → `RoadPlacer` → `RiverPlacer` →
  `TreasurePlacer`. Each stage draws from its own `SeedStreams` stream: same inputs → same map on every platform.
- The stages take every table (objects, terrain, creatures, artifacts) from the `ContentCatalogs` the generator was given,
  which `GeneratedMap.Content` carries; none uses a built-in table directly. The scenario's game gets the same record.
- `ScenarioAssembler.Build` / `MapGenerator.Build` produce the scenario `GameState` (`PlayerSetup`); Core.Generation
  cannot see `MapDocument`. The editor uses `EditorMapGenerator.Generate`; `MapConverter.ToDocument` saves a map.
- Templates: model `MapTemplate` (Core.Generation), file `*.ewtemplate.json` via `TemplateSerializer` /
  `BuiltInTemplates` (Core.Data), `TemplateValidator` (errors, fairness), `MapFairness`. `TemplateEditorSession`
  edits a template without undo and publishes `TemplateChanged`.
- Generation touches no shared state and must stay fast (target < 5 s for 256×256×2).

### Map editor
`MapEditorSession` (`Editing/`) wraps a scenario `GameState` that never starts. Every edit is an `IEditorCommand`
(execute/undo) in `UndoHistory` and publishes the same kind of events as the game, so editor views are ordinary
subscribers. The session is a facade: edits live in per-area editors (`Terrain`, `Objects`, `Contents`, `Monsters`, `Towns`,
`Heroes`, `MapEvents`, `Players`, `Map`) over one internal `EditorContext` (scenario, catalog, bus, history, open stroke).
Single-value edits are `PropertyEdit<T>` commands; an edit to the value already there records no undo step and publishes
nothing. The three tile layers paint through `PaintLayer<TValue, TEvent>`. `BeginStroke`/`EndStroke` merge a drag into
one undo step. `MapValidator` reports what single edits cannot prevent. `MapConnectivity` tells where a hero can walk on a fresh
map (through guards and pickups, via gates); the Sim's generated-map check reports the rival towns a player cannot reach.

### Content tables (ADR-071)
`src/Core/data/*.csv` (creatures, heroes, buildings, artifacts, artifact sets, spells, riddles, scenery) are embedded
into Core.Domain; `data/templates` into Core.Data. Apps use the embedded copy (`GameContent`), or designer files via
`--creatures=`-style flags. CSV for flat tables, JSON for nested data. `DataTableSchemaTests` checks each table.

## 4. Godot libraries: constraints
- Tier 2 projects use `Godot.NET.Sdk` 4.7.2 so source generators and node overrides work.
- Godot attaches scripts by `res://` path, and `[GlobalClass]` types in referenced assemblies are not registered
  (ADR-043). So **library nodes are created from code** (`new HeroView()`) and configured with `Bind(...)`; classes that
  must be attachable in the editor (`[GlobalClass]`, `[Export]`) live in the app project.
- App `.csproj` files state `<TargetFramework>net10.0</TargetFramework>` explicitly (Godot cannot see
  `Directory.Build.props` and would insert `net8.0`).
- View composition (`Godot.Shared/Map`): `MapViewContext` (viewing player, shown level; raises a C# event) →
  `AdventureWorldView` composes per level `AdventureMapView`, `MapObjectsView` (viewer's `ObjectMemory`, or live objects
  without a viewer), `FogView`, `HeroView`s, `PathPreview`. Game and editor both use it. Panels live in `Godot.UI`
  (`Hud`, `Setup`, `Editor`, `Battle`, `Debugging`). The game's adventure HUD and its screens are one `AdventureHud`; the map's and the
  battle scene's clicks and keys are `AdventureInputController` and `BattleInputController` (deployment drags: `DeploymentDragController`);
  the screens before a game share a `SetupScreenHost`. Their texts come from core (`MoveOutcomeText`, `AdventureSpellPrompt` in
  Core.Domain; `SaveSlotText`, `RankingText` in Core.Data, which the app maps onto the panels' view records).
- Assets cannot live in a class library: `EternalWars.Godot.Assets` is mounted into each app as `res://shared/` by
  `tools/link-shared-assets.ps1` (junction, git-ignored).
- **Spacing** (ADR-163): every app calls `UiSpacing.Apply()` first, which merges the shared gaps and margins into Godot's default theme (a
  theme on the root window would not reach controls under a `CanvasLayer`). Panels take the theme's spacing instead of their own numbers:
  `AddHeading` for a section, `WithCellGap` for a row of slots, `Screens.UseOpaqueBackground` / `CenterAsScreen` for a screen over the map,
  `Screens.UseMenuPanel` for one over the main menu's picture; code that places controls itself uses `UiSpacing.ScreenEdge`.

## 5. Threading model
Single-threaded on Godot's main thread; `EventBus` is deliberately not thread-safe. Heavy work may later run on a
background thread against a **copy** of the state, returning commands to apply on the main thread.

## 6. Solution layout
```
global.json                     .NET SDK 10, Godot.NET.Sdk 4.7.2, MTP test runner
Directory.Build.props/.targets  shared settings + architecture guard
src/Core/data/                  content tables and templates, embedded at build time
src/Core/EternalWars.Core.*/    Tier 1; namespaces follow folders
src/Godot/EternalWars.Godot.*/  Tier 2
src/Apps/EternalWars.*/         Tier 3: project.godot + Main.tscn/Main.cs
tests/EternalWars.Core.Tests/   TestWorld, TestBattle, SessionCommands helpers
tools/                          link-shared-assets.ps1, smoke-godot.ps1, smoke/ maps, EternalWars.Tools.Sim
reference/ew/                   earlier prototype, kept for reading only (not built)
.github/workflows/ci.yml        build + test + sim selftest; generated-maps job
```
