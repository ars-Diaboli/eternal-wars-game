# Decision log

Short ADRs. Add new entries at the bottom; never rewrite accepted ones, supersede them instead.

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
async/threading semantics we don't want. ~100 lines, fully tested. See [event-system.md](event-system.md).

### ADR-004: Separate Godot projects per app, assets shared via links
**Status**: accepted, revisit after Phase 0 spike
User-facing apps stay independent (own settings, renderer, export presets). Shared assets live once in
`EternalWars.Godot.Assets` and are linked as `res://shared`.
*Alternative considered*: a single Godot project with three main scenes (simpler asset sharing, one
import cache) that splits apps with export feature tags. Still viable if links prove painful.

### ADR-005: Square adventure grid; square combat grid with multi-cell units
**Status**: accepted (2026-10-15)
The adventure map uses a square grid with 8-way movement (as in HoMM3). Battles use a **separate
battlefield** with a fine **square cell grid**: creature stacks occupy 2×2, 3×3 or 4×4 cells by size
class. This replaces HoMM3's hex grid. Size becomes a real tactical property (blocking, surround,
reach), and one coordinate type serves both map and battle maths.
Consequences: combat pathfinding has to test footprint clearance (precompute clearance maps per
size), and AI and UI must handle footprints rather than single cells.

### ADR-006: Text-friendly map format
**Status**: accepted
`*.ewmap.json` with terrain stored as one string per row so maps can be reviewed and merged in git.
Versioned via `formatVersion`.

### ADR-007: Code-first Godot nodes in libraries
**Status**: accepted, pending Phase 0 spike
Library node classes are instantiated from code and configured through `Bind(...)`. Avoids Godot's
`res://` script-path requirement for scripts in referenced assemblies.

### ADR-008: HoMM-inspired, not a clone; dark-fantasy realistic presentation
**Status**: accepted (2026-10-15)
We keep HoMM3's loop and structure but take deliberate departures, logged in game-design.md §7.
Art: realistic lighting and PBR materials, dark and grim atmosphere, recognisable nods to the
original. Modes in priority order: single-player vs AI, hot-seat, story campaign. Online multiplayer is
out of scope.

### ADR-009: Initiative orders turns, one action per round
**Status**: accepted (2026-10-15)
Each combat round every stack acts exactly once, ordered by an Initiative stat (separate from Speed).
HoMM5's timeline UI without its "fast units act twice". This keeps the round structure that spells
and morale reason about, while Initiative stays a tuning lever separate from movement.

### ADR-010: Town roster in four groups; two non-playable towns
**Status**: accepted (2026-10-15)
Playable: Haven, Sylvan, Academy | Necropolis, Inferno, Dungeon | Stronghold, Swamp Bestiary.
Non-playable (campaign/neutral): Conflux, Dragon Utopia. Balancing effort goes to the 8 playable towns
only. Data model: a town definition carries `Group` and `IsPlayable` so map setup, random maps and the
AI can filter on them.

### ADR-011: Smooth terrain over a logical square grid
**Status**: accepted (2026-10-15)
Rendering: heightmapped, splat-textured terrain. Logic: unchanged `AdventureMap` tiles. The map
format will grow a height/decoration layer that is purely visual. Core never reads it.

### ADR-012: Map objects = data definitions + per-kind instance classes
**Status**: accepted (2026-10-15)
Definitions (footprint, kind parameters) are data: built-in defaults in code, overridable/extendable
from JSON, which keeps the door open for modding. Behaviour is selected by `MapObjectKind` and lives in
`MapObjectInteractionService`. A plugin/behaviour registry is deferred until there are many kinds.

### ADR-013: Visiting is mandatory; routes end at entrances
**Status**: accepted (2026-10-15)
Stepping onto an entrance always visits the object, so the pathfinder never routes *through* one.
Gates are the exception: the route continues from the partner gate. Pathfinding state is
(tile, arrived-by-teleport) so a gate exit can be walked away from without bouncing back.

### ADR-014: Choices are pending decisions, not callbacks
**Status**: accepted (2026-10-15)
When a rule needs player input, it stores a `PendingDecision` in `GameState` and publishes
`DecisionRequested`. Movement and End Turn are refused until it is resolved. The core never
blocks or calls back into the UI; AI, UI and future network/replay resolve decisions the same way.
They will become commands in Phase 2.

### ADR-015: Editor edits are undoable commands over a scenario GameState
**Status**: accepted (2026-10-15)
The editor edits the same `GameState` type the game starts from. This avoids a second map model.
Each edit is an execute/undo closure pair recorded in `UndoHistory`. Commands publish domain events,
so editor views reuse game views.

### ADR-016: Fog of war is per-player permanent shroud (provisional)
**Status**: superseded by ADR-017
Matches HoMM3. Stored in `GameState.Fog`, updated by `VisibilityService` reacting to movement events.
Re-fogging would add a "currently visible" layer computed from heroes, towns and mines.

### ADR-017: Fog mode is chosen per game; refog uses hysteresis
**Status**: accepted (2026-10-15)
`GameOptions.FogMode` is set at game creation and stored in `GameState` (it is a game rule, not
map data). `PermanentReveal` = HoMM3. `ShroudWithRefog` keeps terrain and objects explored, but other
players' units are only visible on tiles in sight. A tile enters sight within an observer's radius and
leaves only beyond radius + `RefogMargin`, so units at the edge of vision don't flicker. Sight is a
pure function of current observers and is recomputed per affected player, stored as a set (cheap even
on 256×256 maps). The planner ignores unseen heroes; real movement bumps into them.

### ADR-018: HoMM3 movement rules
**Status**: accepted (2026-10-15)
A step costs the terrain being left (×1.41 diagonally). Any positive remainder allows one last step.
Daily points come from the slowest creature's speed (HoMM3 table), then a percentage bonus (skills),
then a flat bonus (artifacts), recomputed at the owner's turn start (`MovementAllowance`). Until
armies exist `Hero.SlowestCreatureSpeed` is a stored placeholder; Phase 2 derives it from the army.

### ADR-019: Six resources
**Status**: accepted (2026-10-15)
Gold, lumber, ore, crystal, mercury, sulfur. No gems. Rare resources are crystal, mercury and sulfur.

### ADR-020: Map size presets up to 256, max 8 players
**Status**: accepted (2026-10-15)
Square presets 48, 72, 96, 128, 160, 192, 256 (`MapSizes`); `AdventureMap` rejects dimensions above
256 and `GameState` more than 8 players. Data structures were checked for 256×256×2 levels: pathfinding
arrays are ~260k entries per search; fog sight is set-based.

### ADR-021: Combat ground rules
**Status**: accepted (2026-10-15)
- Battlefield 24 cells tall × 28–36 wide, set by the hero's Tactics skill (default 30).
- No facing and no generic flanking bonus. Engagement (which enemies are adjacent, on how many sides)
  and per-round hit counts are tracked so **creature abilities** can use them.
- HoMM3 Wait (end of round, reverse initiative order) and ties (attacker side, then speed, then a
  seeded roll).
- Breath has HoMM3 friendly fire; cleave/multi-head hits enemies only; only the primary target retaliates.

### ADR-022: Strategy camera
**Status**: accepted (2026-10-15)
Tilt 15–30° from vertical, coupled to zoom (zoomed in = 30°, zoomed out = 15°); rotation limited to ±45°
around the vertical axis, so the map never appears upside down and art can assume a "south-ish"
viewing direction; zoom and pan. Implemented in `StrategyCamera` (Godot.Shared), not yet verified in Godot.
