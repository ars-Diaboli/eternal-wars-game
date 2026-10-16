# Architecture

## 1. Goals

- **Game logic is engine-independent.** Rules run and are tested without Godot, which makes them
  fast to test and possible to reuse in headless servers, AI simulation and tools.
- **Three front-ends, one core.** The 3D game, map editor and 2D harness all drive the same core
  services.
- **Event-driven presentation.** Core publishes facts through a custom pub/sub bus; Godot views
  subscribe and render. Views never poll core state for changes.

## 2. Tiers

```
┌──────────────────────────────── Tier 3 · Apps (Godot projects) ───────────────────────────────┐
│  EternalWars.Game            EternalWars.MapEditor            EternalWars.Harness2D            │
│  3D game (Forward+)          3D editor (Forward+)             2D debug front-end (Compatibility)│
└──────────────▲───────────────────────────▲───────────────────────────────▲─────────────────────┘
               │                           │                               │
┌──────────────┴───────────── Tier 2 · Godot libraries (depend on GodotSharp) ───────────────────┐
│  EternalWars.Godot.UI        HUD widgets, panels, debug overlays (bound to core events)        │
│  EternalWars.Godot.Shared    map/hero views, camera, coordinate mapping, input, event glue      │
│  EternalWars.Godot.Assets    models, textures, materials, shaders, prefabs (no code, no csproj) │
└──────────────▲─────────────────────────────────────────────────────────────────────────────────┘
               │
┌──────────────┴───────────── Tier 1 · Core (plain .NET 10, no Godot) ──────────────────────────┐
│  EternalWars.Core.Data       file formats (maps, content definitions; later saves, templates)  │
│  EternalWars.Core.Generation (planned, Phase 4) random map templates + deterministic generator │
│  EternalWars.Core.Domain     model + rules: map, heroes, armies, battles, commands, editing     │
│  EternalWars.Core.Events     pub/sub primitives: IEvent, IEventHandler, IEventPublisher, bus   │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
         tests/EternalWars.Core.Tests  (xUnit v3, covers Tier 1)
         tools/EternalWars.Tools.Sim   headless console app over Tier 1 only: battle statistics, scripted
                                       games, replay verification (selftest runs in CI); AI-vs-AI and
                                       RMG batch validation later
```

### Dependency rules

| From \ To | Core.Events | Core.Domain | Core.Generation | Core.Data | Godot.Shared | Godot.UI | Apps |
|---|---|---|---|---|---|---|---|
| Core.Events | — | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| Core.Domain | ✓ | — | ✗ | ✗ | ✗ | ✗ | ✗ |
| Core.Generation | ✓ | ✓ | — | ✗ | ✗ | ✗ | ✗ |
| Core.Data | ✓ | ✓ | ✓ (template format) | — | ✗ | ✗ | ✗ |
| Godot.Shared | ✓ | ✓ | (avoid) | (avoid) | — | ✗ | ✗ |
| Godot.UI | ✓ | ✓ | (avoid) | (avoid) | ✓ | — | ✗ |
| Apps | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ (apps never reference each other) |

These are enforced by:
- `Directory.Build.targets`: the build **fails** if any project under `src/Core` uses
  `Godot.NET.Sdk`, references `GodotSharp`, or references a `*Godot*` project.
- `Directory.Build.props`: core projects build with `TreatWarningsAsErrors`.

## 3. Runtime composition

```
           input (keys, clicks)                       render
 Player ───────────────────────► App (Main.cs) ◄──────────────── Godot views (Tier 2)
                                     │  calls                           ▲
                                     ▼                                  │ subscribe
                               GameSession (Core.Domain)                │
            ┌─────────────┬─────────┼──────────────┬────────────┐     │
       TurnService  HeroMovement  MapObject      Economy   Visibility │
                    (+PathFinder) Interactions   Service   Service    │
            └─────────────┴─────────┼──────────────┴──────▲─────┘     │
                                    │ mutate GameState,   │ handles   │
                                    ▼ then publish        │ events    │
                               EventBus (Core.Events) ────┴───────────┘
```

- **`GameSession`** is the composition root of a running game: one `GameState`, one `IEventBus`,
  and the services that mutate the state (the diagram shows the adventure-map ones; `ArmyService`,
  `ProgressionService`, `BattleService`, `GuardService` and `TownService` sit beside them). Front-ends change the game **only** with commands
  (`session.Execute(new MoveHeroCommand(...))`, ADR-036) and observe it through `session.Events`; the services'
  mutators are `internal`. Accepted commands go to `session.CommandLog`, which with the map and the seed
  replays the game (`GameReplay`). Core handlers (`VisibilityService`) are subscribed first, before any view.
- **Call chain for a click-to-move**: `Execute(MoveHeroCommand)` → `Movement.MoveTo` → `PathFinder` → per step `TryStep`
  (validates via the shared `MovementRules`, publishes `HeroMoved`) → on an entrance tile
  `MapObjectInteractionService.Visit` → outcome events / `DecisionRequested`. A step towards a monster or into
  its guarded area goes to `GuardService` (offer or battle); a step onto an enemy hero to `BattleService`.
- **Pending decisions.** Some outcomes need a player choice (chest, exchange, guard offer, battle, level-up). `GameState.PendingDecision` is set, `DecisionRequested` is published, and movement and
  End Turn are refused until the matching command resolves it. A battle is a `BattleDecision` that resolves
  itself when the battle ends. The core never waits or blocks: the UI
  (or AI) answers whenever it wants.
- **Services** validate, mutate `GameState`, then publish past-tense events. They depend on
  `IEventPublisher` only.
- **`GameState`** is plain data. Setters are `internal` so only Domain services can mutate it.
  It also carries the **`GameOptions`** chosen at game creation (fog mode, refog margin, RNG seed) and the
  game's `GameRandom`. Options are game rules, not map data, so they are passed to `MapConverter.ToGameState(document, catalog, options)`.
- **Views** (`AdventureMapView`, `HeroView`, `ResourceBar`, ...) subscribe with
  `node.SubscribeWhileInTree<T>(...)`, which disposes the subscription when the node leaves the tree.
- **Apps** contain composition and input mapping only. If an app needs logic, it probably belongs
  in core.

### Artifacts (ADR-061)
- `ArtifactCatalog` (`Core.Domain/Artifacts`, tables embedded from `data/artifacts.csv`) defines items, slots, tiers and sets;
  `HeroArtifacts` holds what a hero wears and carries, and `ArtifactBonuses` is the sum the rules read
  (`Hero.TotalAttack`, `Hero.SkillLevelOf`, `Hero.Bonus`), refreshed by `GameState.RefreshArtifacts` whenever the equipment
  changes (also when a hero joins the game or the editor edits it). `ArtifactService` runs equip, unequip, give, pickups
  and loot; `ArtifactPickup` objects roll their artifact at game start.

### Tavern (ADR-060)
- `TavernRules` (Towns) decides who a tavern offers (ranks on the lore ring, a per-player weekly shuffle of the roster
  with no stored state) and whether a hire is allowed; `TownService.HireHero` creates the hero. The tavern is a building
  level in `buildings.csv`, so upgrading it is an ordinary `BuildCommand`.

### Adventure AI (ADR-059)
- `AdventureAi` (`Core.Domain/AI`) plays a computer player's turn with `GameSession.Execute` only: towns (build order,
  saving, recruiting), heroes (valued targets, exploration) and pending decisions. `BattleEstimator` runs the real
  battle engine on a throwaway `GameState` to decide whether a fight is safe. The game app calls `PlayTurn()` once a
  frame for computer players; the simulator (`sim game`) runs it for every slot with `controlHumanSlots`.

### Saved games (ADR-058)
- `GameSaves` (Core.Data) turns a session into a `GameRecordDocument` with a `SaveInfo` label and back: the initial map,
  the options and the command log, verified by the digest on load. It works in the middle of a battle because battle
  actions are commands. The game app keeps the map document taken before `Start`, builds all its game nodes under one
  `Game` node (freed on load) and lets `BattleView3D.Restore` show a battle that is already under way. Autosave (two rotating slots) hooks the
  `CommandExecuted` event, which is published after the command is logged.

### Game flow (ADR-057)
- `GameFlowService` decides eliminations and the winner. `GameSession` runs `Flow.Evaluate()` once after `Start()` and
  after every accepted command (`Settle`), and passes the turn on when the current player has just been defeated.
  `TurnService` skips defeated players. `GameState.Rules` (`ScenarioRules`, from the map) says what counts;
  `GameState.Outcome` is set once, after which `Execute` refuses everything.

### Battles (Phase 2)
- `BattleState` (`GameState.Battle`) holds the field (width × 24 cells), units with footprints, obstacles,
  round, acting queue and its own forked RNG. `BattleGrid` answers geometry (reachable placements,
  adjacency, line of fire, cover) as pure functions; `BattleEngine` applies the rules for the active unit;
  `BattleAi` plays computer sides; `BattleService` connects battles to the map (start from encounters,
  commanders per side, survivors, experience, defeated heroes and guards).
- Creature special rules are data (`CreatureCapability`, at most 5 per creature, ADR-042). The engine and
  `DamageCalculator` ask `creature.Has(kind)`; spell immunity, resistance and casting are queries (`IsImmuneTo`,
  `ResistsSpell`, `SpellCasts`, `BattleUnit.CastsLeft`) waiting for the magic system.
- Battle commands are authorized per side, not per map turn: in hot-seat the defender commands its units
  during the attacker's turn.
- Morale is rolled per stack when a round starts (`BattleUnit.RoundMorale`: extra action, lost turn, ±50 % initiative
  via `BattleState.InitiativeOf`), luck per hit (`HitModifiers.Luck`); values come from `BattleState.MoraleOf` /
  `LuckOf` (creature + hero, ADR-053). Nothing is rolled at 0, so the random sequence of other battles is unchanged.
- War machines are `BattleUnit`s with `Machine` set (ADR-056): speed 0, no melee or retaliation, no morale; only the
  offensive one is queued; the tent heals at round start; `BattleState.LivingArmyOf` (creatures only) decides the end.
- `BattleTargeting.ActionAt` maps a pointed cell to the action a click performs (shoot, melee from the pointer's
  side, move); front-ends share it.
- **Presentation** (ADR-052): the game app's `BattleView3D` (Godot.Shared/Battle) builds the battlefield from
  `BattleStarted` and plays later battle events as one sequential animation queue, since a whole AI turn resolves
  inside one command; it offers input only once the queue has caught up with the state. `BattleHud` (Godot.UI/Battle)
  is its overlay. The 2D harness keeps `BattleBoard2D`, which redraws from the live state.

### Planned evolution
- **Content definitions.** Spells and artifacts will load from data files as creatures, heroes and buildings
  already do (the tables `data/creatures.csv`, `data/heroes.csv` and `data/buildings.csv`, embedded in core and read
  by the apps at start-up, ADR-044, ADR-048, ADR-050; JSON via `CreatureDefinitionSerializer`),
  following the map-object pattern: built-in defaults in Domain code (`BuiltInMapObjects`), JSON
  overrides/additions via `MapObjectDefinitionSerializer` + `MapObjectCatalog.With(...)`.

### Map objects
- **Definition** (`MapObjectDefinition`, data): id, name, `Kind`, footprint and kind parameters
  (resource, daily yield). **Instance** (`MapObject` subclass per kind): id, top-left position,
  mutable state (pile amount, mine owner, chest rewards, gate link).
- **Footprint** rows use `B` blocked, `V` entrance (exactly one), `.` free. `GameState` indexes
  occupied cells, so "what is on this tile" is O(1) for movement and pathfinding.
- Movement-relevant behaviour is declared on the instance, so the pathfinder and movement need no
  per-kind code: `EndsRoute` (false = walkable entrance, e.g. one-way exits) and `TeleportTarget`
  (two-way partner / one-way exit). Kinds: piles, mines (optionally guarded), chests, `TwoWayGate`,
  `OneWayGateEntrance`, `OneWayGateExit`, `MonsterGuard`, `Town`. Owned objects (mines, towns) implement
  `IFlaggable`.
- Adding a new kind = enum value + subclass + a `case` in `MapObjectInteractionService.Visit` +
  map-format subtype. If kinds multiply, move to a per-kind behaviour registry.

### Terrain, roads, teams, knowledge
- **Terrain rules are data**: `GameState.Terrains` (`TerrainCatalog`) gives passability, the
  non-native cost and native towns (ADR-045). All movement code goes through `MovementRules.StepCost`
  (road → road, else terrain being left for the hero's army, ×1.41 diagonal). `MovementRules.ArmyTown`
  is the town of an army whose creatures all come from one town (else null); such an army pays 100 on that
  town's native terrains, and Pathfinding cuts everyone else's penalty above 100. `PathFinder` uses the same
  `StepCost`, so a planned route costs what walking it costs.
- **Roads** are a second per-tile layer on `AdventureMap`.
- **Teams**: `Player.Team`; `AreAllies` / `AreEnemies` drive hero encounters (own → exchange,
  enemy → battle hook, ally → blocked).
- **Knowledge**: `FogOfWar` (explored + in sight) and `ObjectMemory` (last known object state per
  player) are both maintained by `VisibilityService`. Views of a player draw objects from memory, never
  from live state, which is what makes "last known state" under refog work.

### Random map generation (planned, Phase 4)
- `EternalWars.Core.Generation` holds the template model (`MapTemplate`, `TemplateZone`,
  `TemplateConnection`), a `TemplateValidator`, and `MapGenerator.Generate(template, size, players,
  seed, catalogs) → MapDocument`. Its only randomness is a seeded PRNG, so the same inputs always give the
  same map, on every platform.
- Output is an ordinary `MapDocument`: the game starts it through `MapConverter.ToGameState(...)`, the
  editor opens it in a `MapEditorSession` for hand-editing. There is no separate "generated map" type.
- Template file format (`*.ewtemplate.json`, versioned like maps) lives in `Core.Data`.
- The template editor reuses the editor's command/undo pattern on a template document (zone graph
  edits instead of tile edits).
- Generation must stay fast (target: under 5 s for 256×256×2 on a mid-range PC) and runs off the main thread;
  it touches no shared state.

### Map editor model
`MapEditorSession` (Core.Domain/Editing) wraps a scenario `GameState` that never starts. Every edit is an
`IEditorCommand` (execute/undo closures) recorded in `UndoHistory`. Commands publish the same kind
of events as the game (`TerrainPainted`, `MapObjectPlaced`, …), so editor views are ordinary bus
subscribers. `BeginStroke`/`EndStroke` merge a mouse drag into one undo step. `MapValidator`
reports problems that single edits cannot prevent (hero on water after painting, unlinked gates).

## 4. Godot libraries: constraints

- Tier 2 projects use `Godot.NET.Sdk` (4.7.2) so the Godot source generators run and node subclasses
  (overrides such as `_Ready`, `_Process`) work.
- Godot attaches scripts by `res://` path. A class compiled into a library has no path inside the
  app's project, so **library nodes are created from code** (`new HeroView()`), not attached to
  scenes in the Godot editor. Classes that must be attachable in the editor (`[GlobalClass]`,
  inspector `[Export]`s) live in the app project.
  - *Phase 0 spike (ADR-043)*: confirmed. `[GlobalClass]` classes in referenced assemblies are **not** registered,
    so prefabs cannot carry library scripts; code-first nodes work fine.
- App `.csproj` files state `<TargetFramework>net10.0</TargetFramework>` explicitly: Godot cannot see
  `Directory.Build.props` and would otherwise insert `net8.0` on import.
- **View composition** (`Godot.Shared/Map`): `MapViewContext` holds presentation state (viewing player, shown
  level) and raises a C# event when it changes. `AdventureWorldView` composes per level `AdventureMapView`
  (terrain + roads), `MapObjectsView` (the viewer's `ObjectMemory`, or live objects without a viewer) and
  `FogView`, plus `HeroView`s and the `PathPreview`. The game and the editor both use it, with a viewer and
  without one. HUD and editor panels live in `Godot.UI` (`Hud`, `Setup`, `Editor`, `Battle`).
- **Smoke tests**: `AutoInput` (`-- --autoinput=...`) replays clicks and keys through the input pipeline;
  `tools/smoke-godot.ps1` runs scripted scenarios under Movie Maker and fails on errors.
- Assets can't live in a class library. `EternalWars.Godot.Assets` is mounted into each app as
  `res://shared/` by `tools/link-shared-assets.ps1` (junction/symlink, git-ignored).

## 5. Threading model

The simulation is single-threaded and runs on Godot's main thread. `EventBus` is deliberately not
thread-safe. Heavy work (AI turn search, pathfinding over large maps, map generation) may later run
on a background thread against a **copy** of the state, returning commands to apply on the main thread.

## 6. Solution layout

```
EternalWars.sln
global.json                     .NET SDK 10, Godot.NET.Sdk 4.7.2, MTP test runner
Directory.Build.props/.targets  shared settings + architecture guard
docs/
src/
  Core/
    EternalWars.Core.Events/
    EternalWars.Core.Domain/    Common, Map, Objects, Heroes, Armies, Creatures, Towns, Battles, Commands, Pathfinding,
                                Players, Economy, Turns, Visibility, Decisions, Editing, Game, Events, Scenarios
    EternalWars.Core.Data/      Maps (MapDocument v12, MapConverter, MapSerializer), Content (definition JSON),
                                Records (replays)
  Godot/
    EternalWars.Godot.Shared/   Map, Battle (BattleView3D, BattleUnitView, BattleSpace), Cameras, Controls, Events
    EternalWars.Godot.UI/       Hud (incl. DecisionPanel, TownPanel, TownListPanel, HeroPanel), Battle (BattleBoard2D, BattleHud), Debugging
    EternalWars.Godot.Assets/   (linked into apps as res://shared)
  Apps/
    EternalWars.Game/           project.godot + Main.tscn/Main.cs
    EternalWars.MapEditor/
    EternalWars.Harness2D/
tests/
  EternalWars.Core.Tests/       TestWorld (ASCII maps), TestBattle, Events, Domain, Battles, Data
tools/
  link-shared-assets.ps1
  EternalWars.Tools.Sim/        headless simulator (battle, record, replay, selftest)
.github/workflows/ci.yml        build + test + simulator self-test on push/PR (no Godot needed)
```
