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
- Creature definitions (data), stacks, hero army (7 slots)
- Tactical battle on a square cell grid with 2×2 / 3×3 / 4×4 unit footprints: deployment,
  footprint-aware pathfinding (clearance maps), initiative order, melee/ranged, retaliation, wait/defend,
  flanking and body-blocking/cover, area abilities (breath, cleave/multi-head), obstacles,
  victory/defeat, casualties and experience
- Army speed drives `MovementAllowance` (replaces the `Hero.SlowestCreatureSpeed` placeholder)
- Battle presentation in 3D + battle view in the 2D harness
- Neutral guards on the adventure map; quick combat

**Exit**: heroes fight neutral stacks and each other; battles are replayable from logs.

## Phase 3: Towns and progression (~6–8 weeks)
- Towns: building tree, one build per day, weekly growth, recruiting, garrison
- Hero levels, primary stats, secondary skills, level-up choices
- Artifacts (slots, backpack, stat effects)
- Basic AI: economy + exploration + attacks it can win
- Victory/loss conditions; save/load

**Exit**: complete small-map game vs. AI from start to victory.

## Phase 4: Content and polish
- Magic (schools, mage guild, combat and adventure spells)
- Second faction, siege battles ❓
- Art pass (dark fantasy, realistic lighting): terrain splat shader, PBR models, fog/volumetrics,
  UI theme, audio. An early **look-dev spike** (one terrain biome + one creature + lighting setup)
  should run in parallel with Phase 1 to lock the visual target and its performance budget.
- Random map generator ❓

## Phase 5: Story campaign
- Scenario chaining, hero carry-over, map triggers/events and dialogue, editor support for scripting

## Risks
| Risk | Mitigation |
|---|---|
| Godot C# library limitations (scripts in referenced assemblies) | Phase 0 spike; code-first nodes; app-side wrappers for editor-attachable scripts |
| 3D readability on dense maps | Early camera and art-style prototypes; keep the 2D harness as the logic reference |
| AI complexity | Deterministic core + commands give headless simulation for AI tuning |
| Asset production cost | Look-dev spike early; buy/adapt PBR asset packs where the dark-fantasy style allows |
