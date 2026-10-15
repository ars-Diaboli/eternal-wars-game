# Roadmap

Each phase ends with something **playable or usable** and a green test suite. Scope beyond Phase 3
will be refined once the open questions are answered.

## Phase 0: Foundation ✅ (this commit)
- [x] Three-tier solution, .NET 10, Godot 4.7.2 (`Godot.NET.Sdk`), architecture guard in MSBuild
- [x] Custom pub/sub (`EventBus`) with documented semantics and tests
- [x] Domain skeleton: map, terrain costs, heroes, 8-way movement, players, resources, calendar, turns
- [x] Map file format (`*.ewmap.json`, git-diffable terrain rows) + round-trip tests
- [x] Game: 3D tile map, hero capsules, strategy camera, HUD, keyboard movement, end turn
- [x] Map editor: terrain palette, brush radius, paint by mouse, save/load
- [x] 2D harness: top-down map, click/keyboard movement, live event log
- [ ] **Spike**: open all three projects in the Godot 4.7.2 .NET editor and run them; confirm
      library-node instantiation and `[GlobalClass]` behaviour for referenced assemblies
- [ ] CI (GitHub Actions): `dotnet build` + `dotnet test` on push

## Phase 1: Adventure map (~4–6 weeks)
- Pathfinding (A*, terrain costs, diagonals, other heroes as blockers), path preview with day markers,
  click-to-move with step-by-step animation
- Map objects framework: object definitions (data), placement, "visit" interaction events
- First objects: resource piles, mines (daily income, ownership flags), treasure chest
- Fog of war per player
- Hero selection, hero list panel, minimap
- Editor: object placement, player slots, hero placement, map properties, undo/redo
- Two-level maps + underground gate

**Exit**: a hot-seat game on an editor-made map where players race to collect resources.

## Phase 2: Armies and combat (~6–8 weeks)
- Command pipeline (serializable commands, deterministic RNG in state, replay from command log)
- Creature definitions (data), stacks, hero army (7 slots)
- Tactical battle on a square cell grid with 2×2 / 3×3 / 4×4 unit footprints: deployment,
  footprint-aware pathfinding (clearance maps), turn order, melee/ranged, retaliation, wait/defend,
  obstacles, victory/defeat, casualties and experience
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
| Asset production cost | Decide art style early (open questions); lean on stylised / low-poly |
