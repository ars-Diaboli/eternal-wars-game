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
- [x] **Spike** (ADR-043): all three projects run in Godot 4.7.2 .NET; library nodes created in code work;
      `[GlobalClass]` is not registered for referenced assemblies (code-first stays); apps pin `net10.0`
- [x] CI (GitHub Actions): `dotnet build` + `dotnet test` on push/PR (`.github/workflows/ci.yml`)

## Phase 1: Adventure map (~4–6 weeks) ✅

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

### Core follow-ups ✅
- [x] Meeting heroes (ADR-027): teams in `Player.Team` / map player slots (map default, overridable at
      setup via `GameState.AssignTeams` unless `TeamsLocked`, ADR-032); routes may end on own/visible
      enemy heroes; own → `HeroesMet` + `HeroExchangeDecision` (`CompleteExchange`), enemy →
      `BattleTriggered` hook (Phase 2 resolves), ally → `Occupied`
- [x] Refog (ADR-031): `MoveResult.EnemySpotted` auto-stop; `BattleTriggered.IsAmbush` when bumping into
      an unseen enemy; per-player `ObjectMemory` (last known state) + `KnownObjectsChanged`; minimap
      draws objects from memory
- [x] Terrain: highlands, forest floor, wasteland (codes `h`/`f`/`x`, colours, demo) (ADR-032)
- [x] Gates: `TwoWayGate` kind (subterranean gate + two-way monolith definitions), one-way
      `OneWayGateEntrance` → `OneWayGateExit` (exits are walkable landing spots), directional
      pathfinding, editor `LinkGates` / `SetGateExit` with link-safe removal and undo, validator checks;
      dangling links are dead ends, not crashes (ADR-026)
- [x] Roads: road layer in `AdventureMap`, dirt 50 / gravel 65 / cobblestone 75 road → road costs in
      movement and pathfinding (A* heuristic uses road cost), editor `PaintRoad` with undo, validator
      warning on impassable terrain (ADR-024, ADR-032)
- [x] Terrain definitions as data: `TerrainCatalog` (class open/rough/native/impassable, cost, native
      towns), JSON overrides via `TerrainDefinitionSerializer`; native cost applies once armies have a
      town (`MovementRules.ArmyTown`, Phase 2/3)
- [x] Map format v3: roads, teams + `teamsLocked`, one-way gates; v1/v2 still load; null fields omitted

### Godot (Tier 2/3) ✅ (verified in Godot 4.7.2 with `tools/smoke-godot.ps1`)
- [x] `AdventureWorldView` + `MapViewContext`: terrain and roads, map objects from the viewer's `ObjectMemory`
      (live state in the editor), fog overlay (unexplored black, out of sight dimmed), both levels with a level
      switch (L, button, follows gates), unseen enemy heroes hidden (`CanSee`); hot-seat switches the viewer
- [x] New-game setup screen (`NewGameScreen`): demo, `--map`, editor maps; teams unless locked; fog mode, refog
      margin, seed
- [x] Exchange screen (`ExchangePanel`): both armies, click-to-move stacks, split, close; battles fall back to quick
      combat in the 3D game (see Phase 2)
- [x] Editor: road brush, gate link tool, players with teams and teams-locked
- [x] Mouse picking and hero selection; path preview with day-coloured markers; click twice to move; Tab, arrows
- [x] `HeroView`: one queued tween per `HeroMoved`, `HeroTeleported` jumps in order, selection ring, level/sight
      visibility
- [x] Decision prompts (`DecisionPanel`): treasure chest, guard offers, battle width, quick combat
- [x] Hero list panel (`HeroListPanel`) and minimap (`MinimapWidget`, click to look there)
- [x] Editor UI: tool palette (terrain, road, object, monster, hero, erase, link gates, select), map
      properties, players, inspector, validation panel, two-level maps, Ctrl+Z/Ctrl+Y
- [x] 2D harness: objects from memory, fog, roads, path preview, decision prompt

**Exit**: a hot-seat game on an editor-made map where players race to collect resources. Playable in the game app (hot-seat,
fog, guards, quick-combat battles); final art and a real exchange/battle screen come later.

## Phase 2: Armies and combat (~6–8 weeks): core done, 3D presentation pending

### Core (Tier 1) ✅
- [x] Command pipeline (ADR-036): serializable `GameCommand`s through `GameSession.Execute` only (services'
      mutators are internal), command log, `GameRandom` (seeded SplitMix64) in state, replay files
      (`*.ewreplay.json`) verified by `GameStateDigest`
- [x] Headless simulator `tools/EternalWars.Tools.Sim`: `battle` statistics, `record`/`replay`, `selftest`
      (runs in CI). AI-vs-AI games follow with the adventure AI in Phase 3
- [x] Treasure chests roll HoMM3-style reward tiers from the game RNG; map authors may fix values (ADR-039)
- [x] Creature definitions as data (ADR-037): tier 1–5, base/upgraded, size, battle `Speed` + adventure
      `MapSpeed`, initiative, shots, capabilities, tags; JSON overrides; stacks; hero army (7 slots); exchange
      (`MoveStackCommand`: split, merge, swap, keep one stack)
- [x] Tactical battle (ADR-038) on a 24 × 28–34 cell grid with 2×2 / 3×3 / 4×4 footprints: automatic
      deployment, rolled obstacles with cover classes, footprint-aware movement (clearance, no corner
      squeezing, flyers), initiative order with HoMM3 wait/ties, defend, melee/ranged, retaliation,
      engagement and hit-history hooks (`PackHunter`, `Relentless`), body-blocking, cumulative cover in
      eighths with the > 7/8 no-shot rule (ADR-035), breath with friendly fire, cleave, multi-head, HoMM3
      damage formula, Tactics width choice, victory/defeat, retreat, casualties and experience
- [x] Deterministic battle AI for neutrals and computer players; quick combat for either side
- [x] Army speed drives `MovementAllowance`; all-native armies get native terrain costs
- [x] Neutral monsters with two map-level guard modes (HoMM3 zone of control with stealing, or a guarded
      3×3 area), HoMM3 dispositions (join / flee / fight, ADR-040), counts rolled by tier; optional mine guards;
      ambush battles (ADR-031); hero vs hero battles with hot-seat control of both sides
- [x] **Haven and Inferno** draft rosters (9 lines each, 5 tiers, all upgradeable) + 5 neutrals
- [x] **All eight town rosters**, Conflux, Dragon Utopia and lore neutrals from the sandbox, first balance pass with
      `sim lines` / `sim towns` / `sim creatures` (ADR-044)
- [x] Sandbox terrain model (ADR-045): every terrain penalises non-natives, several natives per town, beach / ice /
      shallow water / cursed lands / rocky ground, map format v5
- [x] Creature table `data/creatures.csv` as the single source of stats: embedded in core, read by the apps at
      start-up so it can be edited without a rebuild or re-import (ADR-044)
- [x] HoMM4-style creature capabilities, at most 5 per creature (ADR-042): first strike, ranged retaliation,
      strikes/shoots twice, opt-in melee penalty, unlimited/no retaliation, area attacks, engagement hooks;
      spell immunity and casting as data + hooks until magic (Phase 5)
- [x] Roster revision from the design notes (ADR-049): Dragon Utopia wyvern line, Efreet and Golden Golem neutrals,
      melee Dark/Grim Raiders, non-dragon Shadow Horror, spell resistance for the elemental dragons
- [x] Map format v4 (armies, hero stats, monsters, mine guards, guard mode, optional chest values) and editor
      commands for all of it with undo; validator checks

### Godot (Tier 2/3)
- [x] 2D harness: battle board (`BattleBoard2D`) with click-to-move/attack/shoot, wait/defend/retreat/quick
      keys, width choice and quick combat; verified in Godot 4.7.2 (smoke scenario `harness-guard-battle`)
- [ ] Battle presentation in 3D (the game app falls back to quick combat through `DecisionPanel`)
- [x] Editor UI for armies, hero stats, monsters, mine guards, guard mode and chest values (`InspectorPanel`,
      `MapPropertiesPanel`)

**Exit**: heroes fight neutral stacks and each other; battles are replayable from logs. Core-side met
(`sim selftest`, replay tests); the 3D battle scene is still missing.

## Phase 3: Towns and progression (~6–8 weeks)
- Towns: 7×7 map object (corners free, entrance bottom-middle), building tree (ADR-046 draft: town hall, fort,
  blacksmith, marketplace, mage guild, tavern, dwellings), one build per day, weekly growth, recruiting, garrison
- Dwelling choice: 9 dwellings (2 per tier 1–4, 1 at tier 5), build 7, at least one per tier; upgrades (ADR-030)
- Towns for the slice: **Haven and Inferno**
- Native terrain: penalty waived only for all-native armies; Pathfinding skill 25/50/75 % (ADR-024, ADR-032)
- Income sources beyond mines: towns, Estates-like skills, hero specialisations, artifacts (ADR-025)
- [x] Hero roster (`data/heroes.csv`, 16 per town, mage/warrior, preferred creature, specialty), skill groups,
      levels with skill offers and rival-school odds, the skill effects that need no towns or magic (ADR-048)
- Primary stat growth; stewardship, deployment, spell resistance, skill/spell specialties; hiring heroes in taverns
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
- Magic: six schools, four spell levels and guild levels, spells upgradable twice (ADR-047); combat and adventure
  spells
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
