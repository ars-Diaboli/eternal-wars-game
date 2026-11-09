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
`src/Core` project uses `Godot.NET.Sdk` or references `GodotSharp` or a `*Godot*` project); `Directory.Build.props`
builds core with `TreatWarningsAsErrors`.

## 3. Runtime composition

```
Player input ──► App (Main.cs) ──session.Execute(cmd)──► GameSession (Core.Domain)
                                                           │ services validate, mutate GameState, publish
Godot views ◄──subscribe── session.Events (EventBus) ◄─────┘
```

- **`GameSession`**: composition root of a running game: one `GameState`, one `IEventBus`, the services
  (`TurnService`, `HeroMovementService` + `PathFinder`, `MapObjectInteractionService`, `EconomyService`,
  `VisibilityService`, `ArmyService`, `ProgressionService`, `ArtifactService`, `BattleService`, `GuardService`,
  `TownService`, `CaravanService`, `AdventureMagicService`, `GameFlowService`).
- **Commands only** (ADR-036): front-ends, AI, tools and tests change the game with `session.Execute(GameCommand)`;
  service mutators are `internal`. Accepted commands go to `session.CommandLog`; map + seed + log replay the game
  (`GameReplay`).
- **Services** validate, mutate `GameState`, then publish past-tense events. They depend on `IEventPublisher` only.
- **`GameState`** is plain data with `internal` setters. It carries `GameOptions` (fog mode, refog margin, seed; game
  rules, not map data, so passed to `MapConverter.ToGameState(document, catalog, options)`) and `GameState.Random`
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
- **Views** subscribe with `node.SubscribeWhileInTree<T>(...)`. **Apps** hold composition and input mapping only.

### Feature map (Core.Domain unless noted)

| Area | Key types and rules |
|---|---|
| Artifacts (ADR-061, ADR-121) | `ArtifactCatalog` (from `artifacts.csv`), `HeroArtifacts`, `ArtifactBonuses` read via `Hero.TotalAttack` / `SkillLevelOf` / `Bonus`; call `GameState.RefreshArtifacts` whenever equipment changes (incl. hero joining, editor edits). `ArtifactService` equips, gives, picks up, loots, `Draw(tier)` (relics from `GameState.DrawnRelics`). |
| Marketplace (ADR-067) | `MarketplaceRules` rates; `TradeResourceCommand` → `EconomyService.Trade`. AI does not trade. |
| Tavern (ADR-060) | `TavernRules` (offer = stateless weekly shuffle; hire allowed?), `TownService.HireHero`. Tavern is a building level in `buildings.csv`. |
| Hero limit (ADR-068) | `ScenarioRules.MaxHeroes`, `Player.MaxHeroes`; `GameState.HeroLimitReached` is the one check. `GameSession.Start` removes surplus heroes. |
| Adventure AI (ADR-059, ADR-119) | `AdventureAi` (`AI/`) plays only through `Execute`; `BattleEstimator` runs the real engine on a throwaway state; `Explain` for debugging. The game app calls `PlayTurn()` once a frame for computer players. |
| Magic (ADR-110, ADR-120, ADR-122) | `Magic/`: `Spells` (from `spells.csv`), `MagicRules`, `AdventureMagic` / `AdventureMagicService`; battle casting in `Battles/BattleMagic`. |
| Saves (ADR-058) | `GameSaves` (Core.Data): initial map + options + command log, digest-checked on load; works mid-battle since battle actions are commands. Autosave hooks `CommandExecuted`. Game app builds its nodes under one `Game` node (freed on load); `BattleView3D.Restore` shows a running battle. |
| Arena (ADR-097, ADR-158) | `ScenarioRules.Arena`; `ArenaRules` (preparation day: `MovementAllowance.Daily(state, hero)`, `TownRules.HasBuiltToday`; `Plan` chooses sides and champions); `ArenaService.StartBattle` runs from `TurnService` at day 2 instead of a turn; `BattleService` records the result in `GameState.Arena` (no experience, no AI retreat); `GameFlowService` ends the game from it. `CombatPlace` object. |

### Battles
- `BattleState` (`GameState.Battle`): field (width × 24 cells), units with footprints, obstacles, round, queue, own
  forked RNG. `BattleGrid`: pure geometry (reach, adjacency, line of fire, cover). `BattleEngine`: rules for the active
  unit. `BattleAi`: computer sides. `BattleService`: map ↔ battle (start, commanders, survivors, experience, defeated
  heroes and guards, `NecromancyRules.Raise` after survivors and before experience). `Siege`, `WarMachines`,
  `PrimalFury` (rage, warcries via `BattleWarcryCommand`), `SurrenderRules`, `AutoCombat` beside them.
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
- Front-ends map a pointed cell to an action with `BattleTargeting.ActionAt` / `DeploymentPlacement`.
- Presentation (ADR-052): `BattleView3D` (Godot.Shared/Battle) builds from `BattleStarted` and plays later events as
  one sequential animation queue (a whole AI turn resolves inside one command); it takes input only once the queue has
  caught up. `BattleHud` (Godot.UI/Battle) overlays it. The harness's `BattleBoard2D` redraws from live state.

### Map objects
- **Definition** (`MapObjectDefinition`, data: id, `Kind`, footprint, kind parameters) vs **instance** (`MapObject`
  subclass per kind: id, top-left position, mutable state). Built-in definitions in `BuiltInMapObjects`; JSON
  additions via `MapObjectDefinitionSerializer` + `MapObjectCatalog.With(...)`. An object may turn into another
  definition of `GameState.ObjectCatalog` (same id). Owned objects implement `IFlaggable`.
- **Footprint** rows: `B` blocked, `V` entrance (exactly one), `.` free. `GameState` indexes occupied cells (O(1) lookup).
- Movement behaviour is declared on the instance (`EndsRoute`, `TeleportTarget`), so pathfinding has no per-kind code.
- **New kind** = enum value + subclass + `case` in `MapObjectInteractionService.Visit` + map-format subtype. Kind-specific
  rules sit in `*Rules` classes (`LootRules`, `BuffRules`, `CreatureBankRules`, ...).
- **Rewards (ADR-068)**: definition `VisitScope` + instance `VisitLog` (`MapObject.Visits`, game state only), checked and
  recorded by `MapObjectInteractionService.TryCollect`. Temporary `HeroStatus`es live on the hero, granted/expired via
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
- `ScenarioAssembler.Build` / `MapGenerator.Build` produce the scenario `GameState` (`PlayerSetup`); Core.Generation
  cannot see `MapDocument`. The editor uses `EditorMapGenerator.Generate`; `MapConverter.ToDocument` saves a map.
- Templates: model `MapTemplate` (Core.Generation), file `*.ewtemplate.json` via `TemplateSerializer` /
  `BuiltInTemplates` (Core.Data), `TemplateValidator` (errors, fairness), `MapFairness`. `TemplateEditorSession`
  edits a template without undo and publishes `TemplateChanged`.
- Generation touches no shared state and must stay fast (target < 5 s for 256×256×2).

### Map editor
`MapEditorSession` (`Editing/`) wraps a scenario `GameState` that never starts. Every edit is an `IEditorCommand`
(execute/undo) in `UndoHistory` and publishes the same kind of events as the game, so editor views are ordinary
subscribers. `BeginStroke`/`EndStroke` merge a drag into one undo step. `MapValidator` reports what single edits cannot
prevent.

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
  (`Hud`, `Setup`, `Editor`, `Battle`, `Debugging`).
- Assets cannot live in a class library: `EternalWars.Godot.Assets` is mounted into each app as `res://shared/` by
  `tools/link-shared-assets.ps1` (junction, git-ignored).

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
