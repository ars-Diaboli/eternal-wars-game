# Roadmap

Each phase ends with something **playable or usable** and a green test suite. Scope beyond Phase 3
will be refined once the open questions are answered.

## Phase 0: Foundation ✅
- [x] Three-tier solution, .NET 10, Godot 4.7.2 (`Godot.NET.Sdk`), architecture guard in MSBuild
- [x] Custom pub/sub (`EventBus`) with documented semantics and tests
- [x] Domain skeleton: map, terrain costs, heroes, 8-way movement, players, resources, calendar, turns
- [x] Map file format (`*.ewmap.json`, git-diffable terrain rows) + round-trip tests
- [x] Game: 3D tile map, hero capsules, strategy camera, HUD, keyboard movement, end turn
- [x] Map editor: terrain palette, brush radius, paint by mouse, save/load
- [x] 2D harness: top-down map, click/keyboard movement, live event log
- [ ] **Spike**: open all three projects in the Godot 4.7.2 .NET editor and run them; confirm
      library-node instantiation and `[GlobalClass]` behaviour for referenced assemblies
- [x] CI (GitHub Actions): `dotnet build` + `dotnet test` on push/PR (`.github/workflows/ci.yml`)

## Phase 1: Adventure map (~4–6 weeks): core done, Godot front-ends pending

### Core (Tier 1) ✅
- [x] Pathfinding: A*/Dijkstra, terrain costs, diagonals, heroes and object cells as blockers,
      visit tiles end a route, gates continue it, fog-aware; per-step **day markers** (`HeroPath`)
- [x] Click-to-move: `MoveTo`/`MoveAlong` re-validate every step, publish one `HeroMoved` per step,
      stop at day boundaries, on decisions, or when the world changed (`Interrupted`)
- [x] Map objects framework: data-driven `MapObjectDefinition` (footprint `B`/`V`/`.`), built-in
      catalog + JSON definition loader for content/mods, visit events
- [x] Objects: resource piles (7), mines (7, ownership + daily income), treasure chest (gold-or-XP
      choice through the new **pending decision** mechanism), subterranean gate (linked teleport)
- [x] Fog of war per player, two modes chosen per game (permanent reveal / shroud + unit refog with
      hysteresis), hero scouting radius 5, owned mines radius 2, `TilesRevealed`/`SightChanged`
- [x] HoMM3 movement rules: cost of tile left, last step, daily points from slowest creature + bonuses
- [x] Six resources (gold, lumber, ore, crystal, mercury, sulfur); map presets up to 256×256; 8 players
- [x] Minimap query (`Minimap.Build`): per-tile explored/terrain/marker/owner for any viewer
- [x] Editor model `MapEditorSession`: terrain painting with strokes, object placement/removal,
      gate linking, mine owners, hero placement, player slots, map properties, **undo/redo**, `MapValidator`
- [x] Two-level maps end to end (domain, pathfinding, map format v2, demo scenario)
- [x] Map format v2 (description + polymorphic objects, still loads v1)

### Core follow-ups
- [ ] Meeting heroes (ADR-027): teams/alliances in `GameState` and map player slots (map default,
      overridable at setup unless locked, ADR-032); routes may end on own/enemy heroes; own → exchange
      session (`HeroesMet` + exchange decision), enemy → battle hook (Phase 2), ally → blocked
- [ ] Refog (ADR-031): `MoveResult.EnemySpotted` auto-stop; ambush battle hook when bumping into an
      unseen enemy; per-player last-known object snapshots
- [ ] Terrain: add highlands, forest floor, wasteland (codes, editor, demo) (ADR-032)
- [ ] Gates: generalise the subterranean gate into a two-way gate kind (+ two-way monolith definition);
      add one-way gate entrance/exit with directional pathfinding, editor link tool, validation,
      map format v3 (ADR-026)
- [ ] Roads: road layer in `AdventureMap` + map format (one char per tile), road → road step costs in
      movement and pathfinding, editor road brush with undo (ADR-024, ADR-032)
- [ ] Terrain definitions as data (class, base cost, native towns); native/rough penalties switch on
      when towns/factions exist (Phase 3)

### Godot (Tier 2/3): needs the Godot editor to build and verify visually
- [ ] Render map objects and both levels (level switch), fog overlay; hide unseen enemy heroes (`CanSee`)
- [ ] New-game setup screen: map, players, fog mode/refog margin (`GameOptions`)
- [ ] Mouse picking → hero selection; path preview with day-coloured markers; click twice to move
- [ ] `HeroView`: queue per-step animations (one tween per `HeroMoved`), handle `HeroTeleported`
- [ ] Treasure chest dialog bound to `DecisionRequested`; resource bar already follows `ResourcesChanged`
- [ ] Hero list panel, minimap widget (colours from `Minimap.Build`)
- [ ] Editor UI: object palette, hero/player/property panels, gate linking tool, validation panel
      (undo/redo buttons and stroke-based painting are already wired)
- [ ] 2D harness: draw objects, fog and path preview; decision prompt

**Exit**: a hot-seat game on an editor-made map where players race to collect resources.

## Phase 2: Armies and combat (~6–8 weeks)
- Command pipeline (serializable commands, deterministic RNG in state, replay from command log)
- Headless simulator `tools/EternalWars.Tools.Sim` (scripted scenarios, replays, AI-vs-AI later) in CI
- Treasure chests roll HoMM3-style reward tiers from the game RNG (ADR-034)
- Creature definitions (data: tier 1–5, base/upgraded, size, speed, initiative, abilities, tags), stacks,
  hero army (7 slots)
- Tactical battle on a square cell grid with 2×2 / 3×3 / 4×4 unit footprints: deployment,
  footprint-aware pathfinding (clearance maps), initiative order, melee/ranged, retaliation, wait/defend,
  engagement tracking for ability hooks, body-blocking and cover from units/obstacles, area abilities
  (breath with friendly fire, cleave/multi-head), obstacles, HoMM3 damage formula, Tactics width
  choice (28–34), victory/defeat, casualties and experience (ADR-021, ADR-033)
- Army speed drives `MovementAllowance` (replaces the `Hero.SlowestCreatureSpeed` placeholder)
- Battle presentation in 3D + battle view in the 2D harness
- Neutral guards on the adventure map, optional mine guards; quick combat; ambush battles (ADR-031)
- First creatures: **Haven and Inferno** rosters (9 each, 5 tiers, upgrades) + a few neutrals

**Exit**: heroes fight neutral stacks and each other; battles are replayable from logs.

## Phase 3: Towns and progression (~6–8 weeks)
- Towns: building tree, one build per day, weekly growth, recruiting, garrison
- Dwelling choice: 9 dwellings (2 per tier 1–4, 1 at tier 5), build 7, at least one per tier; upgrades (ADR-030)
- Towns for the slice: **Haven and Inferno**
- Native terrain: penalty waived only for all-native armies; Pathfinding skill 25/50/75 % (ADR-024, ADR-032)
- Income sources beyond mines: towns, Estates-like skills, hero specialisations, artifacts (ADR-025)
- Hero levels, primary stats, secondary skills, level-up choices
- Artifacts (slots, backpack, stat effects)
- Basic AI: economy + exploration + attacks it can win
- Victory/loss conditions; save/load

**Exit**: complete small-map game vs. AI from start to victory.

## Phase 4: Random map generation (~5–7 weeks)
Needs towns, guards and treasure from Phases 2–3. The template *format* and validator can start earlier.
- New core project `EternalWars.Core.Generation` (engine-free, deterministic from seed)
- Template model + `*.ewtemplate.json` format: zones, connections, richness, towns, guards, terrain
  (see game-design §4.9), plus a template validator
- Generator pipeline: zone layout → terrain → borders/obstacles → connections and guards → towns
  → mines/resources → treasure → neutral monsters → connectivity validation. The output is an
  ordinary `MapDocument`, editable in the map editor
- Headless batch tool/tests: generate N maps per template × size × seed and validate them
  (reachability, fairness metrics between start zones)
- Editor: **template editor** (zone graph: add zones, set properties, draw connections) and
  "generate map from template" into an editable map
- Game: new-game screen lets players pick template, size, players, seed and game options, then generate
- Ship a starter set of templates (e.g. 2-player duel, 4-player free-for-all, 2v2, 8-player)
- 1.0 scope: mirrored/symmetric templates; save a generated map as a normal map. Not in 1.0: water/islands,
  HotA template import (ADR-034)

**Exit**: a player picks a template in the game and gets a fair, playable random map; designers
author templates in the editor.

## Phase 5: Content and polish
- Magic (schools, mage guild, combat and adventure spells)
- Remaining towns, siege battles (36-wide battlefield) ❓
- Platform pass: Linux/Steam Deck builds, controller-friendly UI, Deck performance budget (ADR-034)
- Art pass (dark fantasy, realistic lighting): terrain splat shader, PBR models, fog/volumetrics,
  UI theme, audio. An early **look-dev spike** (one terrain biome + one creature + lighting setup)
  should run in parallel with Phase 1 to lock the visual target and its performance budget.

## Phase 6: Story campaign
- Scenario chaining, hero carry-over, map triggers/events and dialogue, editor support for scripting

## Risks
| Risk | Mitigation |
|---|---|
| Godot C# library limitations (scripts in referenced assemblies) | Phase 0 spike; code-first nodes; app-side wrappers for editor-attachable scripts |
| 3D readability on dense maps | Early camera and art-style prototypes; keep the 2D harness as the logic reference |
| AI complexity | Deterministic core + commands give headless simulation for AI tuning |
| Random maps unfair or broken | Batch generation + automated validation/fairness metrics in CI; templates versioned like maps |
| Asset production cost | Look-dev spike early; buy/adapt PBR asset packs where the dark-fantasy style allows |
