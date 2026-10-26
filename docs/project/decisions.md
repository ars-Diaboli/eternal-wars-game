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
async/threading semantics we don't want. ~100 lines, fully tested. See [event-system.md](../technical/event-system.md).

### ADR-004: Separate Godot projects per app, assets shared via links
**Status**: accepted; confirmed by the Phase 0 spike (ADR-043)
User-facing apps stay independent (own settings, renderer, export presets). Shared assets live once in
`EternalWars.Godot.Assets` and are linked as `res://shared`.
*Alternative considered*: a single Godot project with three main scenes (simpler asset sharing, one
import cache) that splits apps with export feature tags. Still viable if links prove painful.

### ADR-005: Square adventure grid; square combat grid with multi-cell units
**Status**: accepted (2026-10-15); "reach" superseded by ADR-021/ADR-038 (size grants no reach: melee needs footprint adjacency)
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
**Status**: accepted; confirmed by the Phase 0 spike (ADR-043)
Library node classes are instantiated from code and configured through `Bind(...)`. Avoids Godot's
`res://` script-path requirement for scripts in referenced assemblies.

### ADR-008: HoMM-inspired, not a clone; dark-fantasy realistic presentation
**Status**: accepted (2026-10-15)
We keep HoMM3's loop and structure but take deliberate departures, logged in [departures.md](../design/departures.md).
Art: realistic lighting and PBR materials, dark and grim atmosphere, recognisable nods to the
original. Modes in priority order: single-player vs AI, hot-seat, story campaign. Online multiplayer is
out of scope.

### ADR-009: Initiative orders turns, one action per round
**Status**: accepted (2026-10-15)
Each combat round every stack acts exactly once, ordered by an Initiative stat (separate from Speed).
HoMM5's timeline UI without its "fast units act twice". This keeps the round structure that spells
and morale reason about, while Initiative stays a tuning lever separate from movement. Amended by ADR-053: good
morale may give a stack a second action, and morale may raise or lower its initiative for a round.

### ADR-010: Town roster in four groups; two non-playable towns
**Status**: accepted (2026-10-15); no `Group`/`IsPlayable` field exists: `BuiltInMapObjects.PlayableTowns` lists the eight playable towns
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
**Status**: accepted (2026-10-15); decisions are answered by commands (ADR-036)
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
**Status**: accepted (2026-10-15); `Hero.SlowestCreatureSpeed` replaced by the army's creature `MapSpeed` (ADR-037)
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
**Status**: accepted (2026-10-15); widths refined by ADR-033 (28–34 by Tactics, 36 for sieges only)
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
viewing direction; zoom and pan. Implemented in `StrategyCamera` (Godot.Shared), verified in Godot by the Phase 0 spike (ADR-043).

### ADR-023: Standalone editor; template-based random maps in both game and editor
**Status**: accepted (2026-10-15); output refined by ADR-064 (a scenario `GameState`, saveable via `MapConverter.ToDocument`)
The map editor stays a separate app. Random maps are generated from **templates** (HotA-style zone
graphs: zones with towns, richness, treasure and monster strength, plus guarded connections). Templates
are authored in the editor's template editor. The game generates from any available template on its
new-game screen; the editor generates into an editable map. Generation lives in a new engine-free
project `EternalWars.Core.Generation`, is deterministic from its inputs, and outputs a plain
`MapDocument`, so generated and hand-made maps share one pipeline.

### ADR-024: Terrain classes and HoMM3-style roads
**Status**: accepted (2026-10-15); the terrain classes are superseded by ADR-045, roads stand
Every terrain belongs to a class:
- **Open**: no penalty for anyone.
- **Rough**: a fixed penalty for all factions (e.g. +25 %).
- **Native**: no penalty for armies native to it, a penalty for everyone else.

Natives are defined per terrain in data (`TerrainDefinition`: class, base cost, native towns), not in
code, so new terrains and towns are content. Roads are a separate per-tile map layer (dirt 50, gravel 65,
cobblestone 75). Road cost replaces terrain cost, including native and rough penalties.

Consequences:
- The map format gains a road layer, with one character per tile like the terrain rows.
- Step cost depends on the hero's army, so `StepCost` takes the moving hero/army. Pathfinding stays
  per-hero.
- The random map generator lays roads between towns and through zone connections.

### ADR-025: Income comes only from sources; no base income
**Status**: accepted (2026-10-15); income artifacts built (ADR-061)
The placeholder 500 gold per turn was removed. Daily income is the sum of pluggable `IIncomeSource`s
(`MineIncome`, `TownIncome`, `HeroIncome` for Estates skills and resource specialties; artifacts later), collected at
the start of the owner's turn. Nothing is published when the income is zero. New income features add a
source instead of editing `EconomyService`.

### ADR-026: Two-way and one-way gates, no random exits
**Status**: accepted (2026-10-15)
Two-way gates link a pair, on the same level or across levels (the former `SubterraneanGate`, now `TwoWayGate`).
One-way gates link an entrance to exactly one exit; several entrances may share an exit, and exits are
landing spots only. Random-exit portals are left out for now, so every link is fixed map data. That
keeps pathfinding exact and avoids hidden randomness. If they are added later, the exit must come from
the seeded game RNG and the pathfinder must stop at the entrance.
Implemented (2026-10-15) as planned: generalise `SubterraneanGate` into a two-way gate kind (subterranean gate and
two-way monolith become definitions), add `OneWayGateEntrance`/`OneWayGateExit` kinds, extend
`PathFinder` teleport edges to be directional, map format v3, and add an editor link tool and validation.

### ADR-027: Meeting heroes
**Status**: accepted (2026-10-15); enemy battles built (ADR-038)
Stepping onto another hero's tile is an interaction, not a move: the moving hero stays where it is.
- Own hero: exchange (opened as a `PendingDecision`-style exchange session, resolved by the UI/AI).
- Enemy: battle (Phase 2).
- Ally: no interaction for now (blocked).

The pathfinder lets a route *end* on an own or visible enemy hero, never pass through. This needs
**teams/alliances** in `GameState` (player → team) so "ally" is defined; free-for-all is the default
(every player their own team).

### ADR-028: Town groups are lore and campaign only
**Status**: accepted (2026-10-15); native terrain per town is now ADR-045 (not ADR-024); no `Group` field exists yet
(`BuiltInMapObjects.PlayableTowns` lists the eight playable towns)
The four town groups carry no gameplay rules in standard or random-map games: no automatic alliances,
shared skills, diplomacy or group bonuses. Teams come from game setup (ADR-027); native terrain is
per town (ADR-024). Groups are used by the campaign (story, scripted alliances and events) and as
content filters (setup screens, random map templates). `Group` stays on the town definition as metadata.

### ADR-029: Town rivalries do not affect combat
**Status**: accepted (2026-10-15)
Rivalries such as Haven vs Necropolis are story and campaign material only. The combat engine has no
hate/enmity bonuses and no alignment or group modifiers. Creature abilities key off creature
*properties* (tags such as `Undead`, `Flying`, `Large`), never off town or group identity. This keeps
balance independent of pairings and lets any town be designed and balanced on its own.

### ADR-030: Five creature tiers, choose 7 of 9 dwellings
**Status**: accepted (2026-10-15); open details in [towns-and-economy.md](../design/towns-and-economy.md); a town's dwellings
derive from its creature lines in `creatures.csv`, not a town definition list (ADR-044, ADR-050)
Creatures have 5 tiers. Every playable town offers 9 dwellings: 2 alternatives at each of tiers 1–4
and 1 at tier 5. A town builds at most 7, at least one per tier, giving 7 creature types to match 7 army
slots, and making town builds a strategic choice. All playable-town creatures are upgradeable;
neutrals and the non-playable towns may not be. Data model: a town definition lists dwellings
`(tier, creature, upgradedCreature?)`. Build validation enforces the limits so the AI, the editor
(pre-built towns) and random maps share one rule. The constraint is "≤ 7 built, ≥ 1 per tier as the
goal": a town can be mid-build with fewer.
- Final per town: no demolition; the unbuilt alternatives are locked once 7 are built. The build
  validator must also refuse a build that would leave a tier impossible to fill. Example: with 6
  built and tier 3 still empty, only a tier-3 dwelling may be built.
- One upgrade building per dwelling; stacks can be upgraded in a town with that upgrade.
- Creatures from unbuilt dwellings remain available from map dwellings and other towns.
- Neutral creatures share the 1–5 tier scale (`CreatureDefinition.Tier`); `Upgrade` is optional.

### ADR-031: Refog behaviour: auto-stop, ambush, last-known objects
**Status**: accepted (2026-10-15)
In `ShroudWithRefog`:
- `MoveAlong` stops after a step when a previously unseen enemy hero comes into sight
  (new `MoveResult.EnemySpotted`).
- Bumping into an unseen enemy hero starts a battle (ambush) instead of returning `Occupied`.
- Players see the **last known state** of out-of-sight objects. Each player keeps a per-object
  snapshot (owner, present/removed), refreshed when the object is in sight. Views read that snapshot,
  never live state, for objects out of sight.

### ADR-032: Teams, native terrain, Pathfinding, roads
**Status**: accepted (2026-10-15); native terrains and costs replaced by ADR-045
- Teams: maps/templates define default teams; setup may change them unless the map locks them.
  Stored in `GameState` (player → team id) and map player slots.
- Terrain: three new native terrains: **highlands** (Haven), **forest floor** (Sylvan), **wasteland**
  (Necropolis). Each needs a terrain code, colour and editor brush. A terrain is native to an army
  only if **every** creature in it belongs to that terrain's town.
- The Pathfinding skill reduces non-native and rough penalties by 25/50/75 %.
- Roads: the road cost applies only road → road (HoMM3).

### ADR-033: Combat baseline: Tactics width choice, HoMM3 damage, cover
**Status**: accepted (2026-10-15); cover rule replaced by ADR-035
- Battlefield 24 tall. The side with effective Tactics (own level − enemy level) picks the width before
  battle: Basic {30, 32}, Advanced {28, 30, 32}, Expert {28, 30, 32, 34}; otherwise 30. 36 is reserved
  for sieges. This refines ADR-021.
- Damage uses the HoMM3 formula as a baseline (+5 %/point Attack over Defense, cap +300 %;
  −2.5 %/point Defense over Attack, cap −70 %). Rolls come from the seeded battle RNG.
- Cover: any unit (friend or foe) or obstacle on the line of fire reduces ranged damage. The flat
  −25 % is superseded by ADR-035.

### ADR-034: Scope: slice towns, platforms, modding, tooling, random-map features
**Status**: accepted (2026-10-15)
- Vertical slice towns: **Haven and Inferno**.
- Treasure chests roll HoMM3-style tiers using the seeded game RNG; map authors may fix values.
- Mines may carry an optional guard; after the guard is beaten, any hero re-flags by visiting.
- Platforms for 1.0: Windows, Linux/Steam Deck. macOS later.
- Player modding is not a 1.0 requirement; formats stay data-driven and versioned anyway.
- Harness: the Godot 2D viewer plus a headless .NET simulator (`EternalWars.Tools.Sim`) for scripted
  scenarios, AI-vs-AI and RMG batch validation in CI.
- Random maps for 1.0: mirrored/symmetric templates and saving generated maps. No water/islands and no
  HotA template import.

### ADR-035: Cumulative ranged cover
**Status**: accepted (2026-10-15); supersedes the flat cover value in ADR-033
Every blocker on the line of fire between shooter and target adds a penalty, counted once per blocker:
2×2/3×3 creature 1/8, 4×4 creature 1/4, small obstacle 1/8, big obstacle 1/4, city wall 1/2.
Penalties add up and ranged damage is multiplied by (1 − total). Above 7/8 the shot is not allowed.
Penalties are exact eighths, so they are stored as integer eighths (`CoverEighths`) to keep battle
maths integer and deterministic. Units of either side count; the shooter and target never do.
Obstacle definitions carry a cover class (small/big/wall).

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

### ADR-037: Creatures as data; two speeds; draft slice rosters
**Status**: accepted (2026-10-15); roster numbers are a draft for review; `NoMeleePenalty` replaced by opt-in `MeleePenalty`
(ADR-042); stats moved to `creatures.csv` (ADR-044)
- `CreatureDefinition` (data): tier 1–5, size class (Small 2×2, Medium 3×3, Large 4×4), Attack, Defense,
  damage range, health, Initiative, shots, growth, cost, optional `UpgradesTo`, free-form tags (ADR-029) and
  abilities (`Flying`, `DoubleStrike`, `NoEnemyRetaliation`, `UnlimitedRetaliation`, `NoMeleePenalty`,
  `Breath`, `Cleave`, `MultiHead`, plus the hook abilities `PackHunter` (engagement) and `Relentless` (hit
  history)). `CreatureCatalog` and `CreatureDefinitionSerializer` follow the map-object pattern (built-ins
  plus JSON overrides).
- **Two speeds** (user decision): `Speed` is battlefield cells per turn; `MapSpeed` is the HoMM3-scale value
  the adventure movement table uses (`MovementAllowance`, slowest creature of the army). This replaces the
  `Hero.SlowestCreatureSpeed` placeholder. `MovementRules.ArmyTown` returns the army's town when every creature
  shares one, which activates native terrain (ADR-032).
- The Haven and Inferno rosters (`BuiltInCreatures`) come from the reference project, rearranged into 9 lines
  over 5 tiers, all upgradeable. Haven: footman, pikeman | zealot, archer | monk, griffin | lancer, paladin |
  radiant spirit. Inferno: imp, horned demon | hell hound, tormentor | succubus, hell charger | pit fiend,
  juggernaut | devil. Neutrals: wolf, bandit, troll, wyvern, and a Dragon Utopia red dragon. Stats are a first
  pass to tune with `sim battle`.
- `Value` (health × average damage × (20 + Attack + Defense) / 40, ×1.5 for shooters, ×1.2 for flyers) is the
  shared strength estimate of the battle AI and neutral dispositions.

### ADR-038: Tactical battle baseline
**Status**: accepted (2026-10-15); defaults confirmed by the user, tunable; `NoMeleePenalty` replaced by opt-in
`MeleePenalty` (ADR-042); morale/luck added by ADR-053, war machines by ADR-056; surrender deferred, still open (ADR-060,
[open-questions.md](open-questions.md))
- Field 24 tall, width 30; the side with effective Tactics picks the width first (ADR-033), the AI keeps 30.
  Units deploy automatically in slot order, in columns at their edge. 2–5 obstacles (small 2×2 = 1/8 cover, big
  3×3 = 1/4) are rolled between the deployment zones, never touching each other.
- Movement: 8-way, one cell per step, up to Speed. A footprint needs all its cells free (clearance by prefix
  sums); a diagonal step needs one of the two orthogonal placements free, so no squeezing through corners.
  Flyers land on any free placement within Speed.
- Turn order (ADR-009, ADR-021): Initiative, then the attacker's side, then Speed, then a seeded roll per round.
  Wait: once per round; waiting units act after everyone, in reverse order. Defend: +20 % Defense (at least
  +1) until the unit's next turn.
- Melee: move next to the target (8-way footprint adjacency) and strike; one retaliation per round unless
  `UnlimitedRetaliation`; `NoEnemyRetaliation` suppresses it; `DoubleStrike` strikes again afterwards.
- Ranged: needs shots and no adjacent enemy; half damage beyond 15 cells; shooters in melee deal half unless
  `NoMeleePenalty`. Line of fire = supercover of the segment between the footprint centres (cells touched only
  at a corner count too); cover per ADR-035; above 7/8 no shot. No skill reduces cover yet.
- Area: breath hits every unit, either side, touching the target-sized area behind the target; cleave hits up
  to two other enemies next to both attacker and target; multi-head hits every other adjacent enemy. One roll
  for all victims, each unit hit once, only the primary target retaliates.
- Damage: ADR-033 in integer maths, with the hero's Attack/Defense added. Only the top creature carries
  damage; stacks heal after the battle; dead stacks leave the army.
- End: a side without living units loses. The winning hero gains the HP of the enemy creatures killed, +500
  for defeating (not routing) a hero. Losing heroes leave the map. Safety net: a battle still undecided after
  100 rounds is won by the defender.
- Control: each human commands their own side, also during another player's turn (hot-seat). Neutrals and
  computer players use `BattleAi` (greedy, deterministic). Quick combat (`AutoResolveBattleCommand`) may be
  requested by either participant and hands both sides to the AI.
- Retreat (user decision): a hero may retreat; the army is lost, the hero leaves the map, and the winner gets
  no +500. Surrender comes with taverns in Phase 3.
- A hero without creatures cannot attack (`MoveResult.NoArmy`); an army-less defender is overrun without a
  battlefield. No ambush bonus, morale/luck, hero spells or war machines in Phase 2.

### ADR-039: Treasure chest rolls
**Status**: accepted (2026-10-15)
Unfixed chests roll from the game RNG when opened: 1000 gold or 500 XP / 1500 or 1000 / 2000 or 1500,
equally likely (HoMM3's tiers without the artifact outcome, normalised). Values fixed in the editor override
the roll.

### ADR-040: Neutral guards: two guard modes, deterministic dispositions
**Status**: accepted (2026-10-15); the guard mode became a per-stack setting (ADR-065)
- Monsters are map objects (`MonsterGuard`: creature, count, disposition, never-flees). Their tile is the
  entrance (attacked from where the hero stands) and they guard the 8 tiles around them. The pathfinder ends a
  route on a guarded tile and never passes through one.
- **The guard mode is a map setting chosen in the editor** (user decision). `Classic` = HoMM3 zone of control:
  entering a guarded tile starts the encounter, but pickups (piles, chests) on guarded tiles can be taken from
  outside without a fight ("stealing"). `Area` = the whole 3×3 area is guarded, pickups included. In both modes
  the intercepted hero stays where it was.
- Dispositions use HoMM3's names without randomness. R = hero army power ÷ guard power. A free join is offered
  when the army has room and the guard is Compliant (always), Friendly (R ≥ 1), Aggressive (R ≥ 2) or Hostile
  (R ≥ 4); Savage never joins. Otherwise the guard offers to flee when R ≥ 3, unless it never flees. Otherwise
  battle. Offers are a `GuardEncounterDecision`, answered with `RespondToGuardCommand` (accept = join, or let it
  go without XP; decline = fight).
- Counts left open are rolled at game start from the creature tier: T1 20–50, T2 12–25, T3 8–16, T4 4–10,
  T5 1–4. In battle a guard splits into min(count, 5) stacks.
- Mine guards (ADR-034) always fight; beating them flags the mine for the winner.

### ADR-041: Map format v4
**Status**: accepted (2026-10-15)
Adds hero `attack`/`defense`/`tactics`/`army` (slot, creature, count), `guardMode`, `monster` objects, a mine
`guard`, and makes chest `gold`/`experience` optional (both omitted = rolled). v1–v3 still load: their chests
keep their values as fixed values, and their heroes start without an army.

### ADR-042: HoMM4-style creature capabilities, at most five
**Status**: accepted (2026-10-15); refines ADR-037 and ADR-038
- Creature special rules are called **capabilities** (`CreatureCapability`, `CapabilityKind`; JSON
  `"capabilities"`). Every one counts, flying and area attacks included: **at most 5 per creature**, no
  duplicates, enforced when definitions are validated (user decisions).
- A capability is `(Kind, Value, Parameter)`: the value is a percentage (pack hunter, relentless) or casts per
  battle (caster); the parameter is a spell tag (immunity) or a spell id (caster).
- New combat capabilities (HoMM4 rules, confirmed by the user):
  - `FirstStrike`: the defender retaliates **before** the attacker's blow, using its retaliation for the
    round; if both have it, normal order; an attacker killed this way does not strike.
  - `RangedRetaliation`: a shooter that is shot at fires back if it could shoot the attacker right now (shots
    left, not engaged, cover ≤ 7/8); it spends a shot and its retaliation for the round.
  - `ShootsTwice`: a second shot after the (ranged) retaliation, one shot of ammunition each.
    `DoubleStrike` is the melee equivalent.
  - `MeleePenalty` is now **opt-in**: only shooters that have it deal 50 % in melee. It replaces
    `NoMeleePenalty`. Shooter-only capabilities on a creature without shots are invalid.
  - `NoEnemyRetaliation` now also prevents ranged retaliation.
- Spell capabilities are **data and hooks only** until magic exists (user decision): `SpellImmunity(tag)` with
  `CreatureDefinition.IsImmuneTo(tags)` (well-known tags in `SpellTags`: all, mind, fire, curse; the magic
  system will tag its spells), and `Caster(spell, casts)` with `CreatureDefinition.SpellCasts` and
  `BattleUnit.CastsLeft`. They change no battle outcome yet.
- The draft rosters now use them, e.g. guardian and lacerator first strike, marksman shoots twice, monk/priest
  and succubus mistress ranged retaliation, archer/monk/succubus melee penalty, mind/fire/curse immunities,
  priest/seraph/pit lord casters.
- Battle events report a shot back as `BattleAttackKind.RangedRetaliation`.

### ADR-043: Phase 0 spike results; Godot smoke tests
**Status**: accepted (2026-10-15)
Ran all three apps in Godot 4.7.2 .NET (`Godot_v4.7.2-stable_mono_win64`):
- All three import, build and run; the 2D harness on Compatibility, game and editor on Forward+.
- **Library nodes created in code work** (views, HUD, camera, panels from `Godot.Shared`/`Godot.UI`), including
  `_Ready`/`_Process`/`_Draw`/`_GuiInput` overrides and C# events. ADR-007 stands.
- **`[GlobalClass]` does not register classes from referenced assemblies**: a probe class left
  `global_script_class_cache.cfg` empty. Classes that must be attachable in the Godot editor stay in the app
  project; prefabs cannot carry library scripts.
- Separate projects with linked shared assets (ADR-004) work; the junctions import cleanly.
- Godot rewrites app `.csproj` files on import: it pins `Godot.NET.Sdk/4.7.2` and inserts
  `<TargetFramework>net8.0</TargetFramework>` because it cannot see `Directory.Build.props`. The apps therefore
  state `net10.0` explicitly; Godot keeps it, and .NET 10 assemblies load fine. Godot 4.4+ `*.cs.uid` files are
  committed.
- Smoke testing: Godot's Movie Maker (`--write-movie`, `--fixed-fps`, `--quit-after`) renders frames to PNGs, and
  `AutoInput` (`-- --autoinput=frame:click:x,y;frame:key:Name`) feeds scripted input through the normal input
  pipeline. `tools/smoke-godot.ps1` runs scripted scenarios for every app and fails on Godot/C# errors. It needs a
  GPU, so it runs locally, not in CI (yet).
- The first runs found two core bugs, now fixed and tested: no route could reach a monster (all its neighbours
  are guarded; the pathfinder now allows the last step from a tile guarded by the target monster), and
  `DecisionRequested` for a battle was published before the width-choice phase was set.

### ADR-044: All town rosters; the creature table is the source of truth
**Status**: accepted (2026-10-16); stats are a first balance pass for review; "Dark and Grim Raiders shoot" reversed by ADR-049
- **Rosters** for all eight playable towns (9 lines over 5 tiers, all upgradeable), Conflux, Dragon Utopia and
  neutrals, taken from the sandbox (`reference/ew`, `EW.CoreLib.Data/Monsters`). Only Haven and half of Academy had
  real numbers there; the rest were copy-pasted placeholders (all 10s, the class defaults, or Seraph copies), and
  Bestiary, Conflux and Dragon Utopia were name lists. Corrections against the sandbox: Academy Iron Golem upgrades
  to Animated Armor and tier 3 is Sorcerer/Mage + Snow Maiden (Thane was commented out and is now a neutral);
  Stronghold's Roc is tier 4 next to Cyclops, a Wolf Rider line fills tier 3 and the Mammoth is a neutral; Dark
  and Grim Raiders shoot, as in both sandbox copies; the sandbox's copy-paste flags (flying Cyclops and Behemoth,
  shots on Djinn and Naga, Pegasus without flying, Skeleton named "Devil") are not kept. Lore neutrals the
  sandbox had outside its live roster are in: Timber/Dire Wolf, Specter/Banshee, Thane, Gog/Magog, Breeder/Fel
  Breeder. The sandbox's Ballista and Flamethrower became war machines (ADR-056).
- **One table, no stats in code.** Every creature is one row of `src/Godot/EternalWars.Godot.Assets/data/creatures.csv`
  (`CreatureTable`: header-named columns in any order, comma/semicolon/tab, list cells with `;`, a free `notes`
  column). Core embeds the file as `CreatureCatalog.Default` (tests, simulator, default `GameState`);
  `BuiltInCreatures` keeps only the ids code refers to. The Godot apps read the file at start-up
  (`GameContent.Creatures`, `--creatures=path.csv` overrides), so a designer edits it and restarts: no build, and
  no import because `creatures.csv.import` uses Godot's `keep` importer (a `.csv` would otherwise be imported as
  translations). A broken table logs the line and column and falls back to the embedded copy.
- **Balance method.** Each line was scaled (health and damage together, so attack, defense, speed and
  capabilities keep their character) towards a per-tier weekly power target adjusted for its capabilities, then
  tuned with the simulator until lines win about half their duels within the tier (`sim lines`) and one-week town
  armies win 40–60 % against each other (`sim towns`, which rotates the four seven-dwelling builds that keep both
  lines of tiers 1–2). Gold is per tier per week (about 1000 / 1650 / 2250 / 3300 / 3300), split by growth;
  upgrades cost +30 % and are about 1.3–1.45× as strong. Neutrals sit a little below town lines; Dragon Utopia
  dragons above town tier 5, the black dragon about twice the others. `sim creatures` lists each line's value,
  weekly power against its tier and gold per value.
- Replays depend on the creature table like on any content: a record made with other stats fails its digest.

### ADR-045: Sandbox terrain model: every terrain penalises non-natives
**Status**: accepted (2026-10-16); replaces the classes of ADR-024 and the natives of ADR-032
- **Costs** (user decision, from the sandbox `TerrainExtensions`): a step costs the terrain being left, **100 for an
  all-native army** and the terrain's cost for everyone else. No terrain is free for everyone; only roads (ADR-024),
  the Pathfinding skill and artifacts reduce the penalty.

  | Non-native cost | Terrains (native towns) |
  |---|---|
  | 125 | grass (Haven, Sylvan), highlands (Haven, Conflux), forest floor (Sylvan), cursed lands (Necropolis), subterranean (Dungeon), sand/desert (none), beach (none) |
  | 150 | dirt (none), rough (Stronghold), wasteland (Stronghold), lava (Inferno), rocky ground (Dragon Utopia) |
  | 175 | ice (Academy), shallow water (Swamp Bestiary) |
  | 200 | snow (Academy), swamp (Swamp Bestiary) |
  | impassable | water (deep; no boats, ADR-051), rock (cliffs, borders) |
- **A town may be native to several terrains** (`TerrainDefinition.NativeTowns`); the whole army must be native
  (ADR-032 stands). Changes against ADR-032: Necropolis moves from wasteland to **cursed lands**, Stronghold from sand
  to **rough + wasteland**; sand becomes a neutral desert.
- **New terrains**: beach `b`, ice `i`, shallow water `=` (walkable), cursed lands `c`, rocky ground `k` (walkable;
  rock stays the impassable cliff). Map format **v5** (new codes only; v1–v4 still load). `TerrainClass` is now
  `Passable`/`Impassable`. Ice and shallow water cost 175 as in the sandbox core (its 3D client said 200).
- Consequence: heroes with no army or a mixed army pay at least 125 everywhere, so off-road daily ranges shrink by
  about a fifth and roads matter more.

### ADR-046: Towns on the map and the building list (from the sandbox)
**Status**: accepted (2026-10-16) for the footprint and dwelling upgrades; the building list is a draft for Phase 3,
amended by ADR-050 (mage guild level 4), ADR-054 (caravan) and ADR-060 (Brotherhood); the numbers live in `buildings.csv`
- **Footprint**: a town is 7×7 with the four corners free (44 blocked cells plus the entrance) and its entrance on the bottom-middle
  cell, the same convention as mines (`BBB`/`BVB`):
  ```
  .BBBBB.
  BBBBBBB
  BBBBBBB
  BBBBBBB
  BBBBBBB
  BBBBBBB
  .BBVBB.
  ```
  The sandbox core used this mask but put the entrance inside the blocked area; its 3D client used a full 7×7 block
  entered from outside. Its mines are 3×2 like ours.
- **Dwelling upgrades produce the upgraded creature** (ADR-030 stands). The sandbox's second dwelling level, which
  raised growth by 50 %, is not adopted; growth bonuses come from other buildings.
- **Draft building list** (sandbox `TownTechTreeBase`; gold / lumber / ore unless noted; the sandbox's gems are read
  as mercury):

  | Building | Levels and costs | Requires | Effect |
  |---|---|---|---|
  | Town hall | 2500/5/5, 5000/10/10, 10000/20/20 | L2: blacksmith, marketplace, mage guild; L3: fort L2 | 500 / 1000 / 2000 gold a day |
  | Fort | 1500/5/5, 3000/10/10, 6000/15/15 | L1: tavern | defence, growth bonus ❓ |
  | Blacksmith | 1000/5/5 | – | sells war machines (ADR-056) |
  | Marketplace | 1250/5 lumber, 2500/5/5 | – | L2: +1 ore and +1 lumber a day; trading ❓ |
  | Mage guild | L1 2000 + 5 lumber + 2 mercury, L2 3500 + 3 crystal + 3 mercury, L3 5000 + 5 crystal + 5 sulfur, L4 ❓ | – | spells of that level (ADR-047) |
  | Tavern | 750/5 lumber | – | hire heroes |
  | Dwelling of tier t | build 500·t gold, upgrade 1000·t; + t lumber (t ≥ 2), + t ore (t ≥ 3), + 1 mercury (t ≥ 4), + 1 crystal (t = 5); the upgrade doubles the resources | – | weekly growth of its creature |
- In the sandbox towns start without buildings; one build per day; 7 of 9 dwellings with every tier covered (as
  ADR-030). Its weekly growth per dwelling (12, 10 / 8, 7 / 5, 4 / 3, 2 / 1) differs from the creature table's
  `growth`; the creature table rules (ADR-044).

### ADR-047: Magic structure
**Status**: accepted (2026-10-16), from [ideas.md](ideas.md); spells and numbers come with Phase 5; partly superseded by
ADR-063 (eight schools → six: Stronghold and Swamp Bestiary get faction ability groups; attunement effects)
- **Eight schools**, one per playable town (user decision: Stronghold and Swamp Bestiary get one too). Working titles:
  Light (Haven), Nature (Sylvan), Arcana (Academy), Death (Necropolis), Chaos (Inferno), Shadow (Dungeon), Shamanism
  (Stronghold), Primal (Swamp Bestiary). Each school is also a hero skill group (ADR-048).
- **Four spell levels**, matched by **four mage guild levels**. Every spell has **three levels of its own** (learned,
  then upgraded twice).
- A school's skill group (4 + 4 + 2 = 10 points): **magic** (4 levels, each allowing spells of that level),
  **attunement** (4 levels: mana pool and/or regeneration, and per school spell cost or potency), **mastery** (2 levels:
  upgrade learned spells).
- Any hero may learn any school; a rival town's school is rarely offered (ADR-048).
- Replaces "four schools, five spell levels" in the game design and the sandbox's three guild levels.

### ADR-048: Heroes, skills and levels
**Status**: accepted (2026-10-16), from [ideas.md](ideas.md); numbers are a draft; Treasury numbers superseded by ADR-062;
Stewardship (governors), attunement and the schools by ADR-063. Note: the rival schools in code are the sworn-enemy pairs of
`BuiltInSkills.Rivals` (`Skills.cs`; two per school town, none for Stronghold and Swamp Bestiary), not the lore ring
- **Roster**: every playable town has **16 heroes**, a **mage** and a **warrior** for each of its eight tier 1–4
  creature lines, which is that hero's **preferred creature**. The preferred line (base and upgrade) gets a **flat
  +2 Attack, +2 Defense and +1 battlefield speed** (user decision). Each hero also has a HoMM3-style **specialty**: a
  skill, a spell or a daily resource. The roster is a table like the creatures, `data/heroes.csv` (`HeroTable`,
  embedded as `HeroCatalog.Default`, read by the apps at start-up, `--heroes=` overrides; ADR-044 conventions). Map
  heroes may name a roster entry (`type`, map format v5) and take its identity, stats and starting skills.
- **Skills** (HoMM4-like): **groups of three skills**, **10 points** to learn a group fully (usually 4/3/3; magic 4/4/2);
  a hero learns **at most 6 groups**. Levels are **Basic, Advanced, Expert, Master** (3-level skills stop at Expert).
  Groups: offense (melee, archery, a third skill to decide), defense (armor, spell resistance, a third to decide),
  estates (treasury: gold; prospecting: other resources; stewardship: cheaper costs in the hero's town), pathfinding
  (logistics: movement; pathfinding: terrain penalty; troop management: army map speed), tactics (tactics: battlefield
  width; deployment: rearrange troops before battle; drill: battlefield speed), and the eight schools (ADR-047). The
  Tactics skill of ADR-033 is now `tactics.tactics`; maps keep loading their old `tactics` field.
- **Levels**: HoMM3's experience table (+20 % per level after 12). Each level gives **one skill point**: the player
  picks one of **three offered skills** (`LevelUpDecision`, `ChooseSkillCommand`). Offers are weighted by class
  (mages towards their own school, warriors towards the general groups, skills already started ×3) and drawn from the
  game RNG. A **rival town's school** (the lore ring) is offered in only **5 %** of level-ups (user decision), the one
  rule effect of rivalries (an exception to ADR-029, which is about combat). Choices happen in the owner's turn; a hero
  that levels up during another player's turn chooses when its owner's turn starts; computer players take the first
  offer. Primary stat growth per level: ADR-055.
- **Effects now** (`SkillEffects`): melee +10 % per level, archery +10/25/50 %, armor −5 % damage taken per level,
  treasury +100/200/350/500 gold a day, prospecting +1/+2 lumber and ore (Expert also one of each rare resource),
  logistics +10 % movement per level, pathfinding −25/50/75 % of the terrain penalty, troop management +1 army map speed
  per level, drill +1 battlefield speed per level, resource specialties pay daily (`HeroIncome`). Not implemented
  yet: stewardship (towns exist now; it is a Phase 3 roadmap item), skill specialties; waiting for magic or a
  deployment step: spell resistance, deployment, the school skills, spell specialties.


### ADR-049: Roster revision from the design notes
**Status**: accepted (2026-10-16), from [ideas.md](ideas.md); amends ADR-044's roster
- **Wyvern** moves to Dragon Utopia (`dragon-utopia.wyvern`, was `neutral.wyvern`) and upgrades to the new **Wyvern
  Monarch** (Flying, Relentless 10 for its venom).
- **Dragonyte** swaps Breath for **Cleave**.
- New neutrals: **Efreet** (tier 4, elemental and demon; Flying, fire immunity, one fireball), the demonic
  counterpart of the Djinn Sultan. It is a separate neutral and not a second Djinn upgrade, because a line has one
  upgrade and an upgrade shares its town (ADR-030). **Golden Golem** (tier 3 construct; mind immunity, spell
  resistance 2).
- **Dark and Grim Raiders** are melee cavalry: the sandbox's crossbow shots are dropped (reverses that point of
  ADR-044). Health was raised to 56 / 64 to keep the line's duel results.
- **Shadow Horror / Shadow Reaper** lose everything dragon-like (Flying, Breath, the `dragon` tag) and become walking
  shadows with First Strike and mind immunity (the Reaper adds Relentless 15). Stats were retuned with `sim lines`
  to keep the tier 5 win rate where it was (about 70 %).
- New capability **`SpellResistance n`**: unaffected by spells of level n and below (n is 1–3). A spell upgraded
  twice (rank 3, ADR-047) counts as the next level (`SpellLevels.Effective`, `CreatureDefinition.ResistsSpell`).
  Red, green, azure and gold dragons have `SpellResistance 3`, so only level 4 spells and twice-upgraded level 3
  spells affect them. Like the other spell capabilities, it is data and a query until magic exists (Phase 5).

### ADR-050: Towns in play: buildings table, town turn, garrisons and capture
**Status**: accepted (2026-10-16); implements the roadmap's town and dwelling items on top of ADR-030 and ADR-046; tavern
effect built by ADR-060, AI town play by ADR-059
- **Buildings are a table** (user decision): `data/buildings.csv`, one row per building level (`id, level, name, gold,
  resources, requires, income`), embedded in core as `BuildingCatalog.Default` and read by the apps at start-up like the
  creature table (`--buildings=`). Dwellings are the rows `dwelling-1` … `dwelling-5`: their level 1 builds and level 2
  upgrades any dwelling of that tier, priced as in ADR-046. A town's nine dwellings are its creature lines from the
  creature table. The draft mage guild level 4 (7500 gold, 7 crystal, mercury and sulfur) is a placeholder.
- **Towns are map objects** (`MapObjectKind.Town`, `town.<type>` for the eight playable towns) with the ADR-046
  footprint (44 blocked cells and the entrance). Per instance: owner, name, building levels, dwelling levels,
  creatures waiting per line, the garrison (seven slots) and the day of the last build.
- **New towns start with town hall 1, fort 1 and tavern 1** (user decision; the fort requires the tavern), also when a
  map omits the buildings. Pre-built buildings keep the requirements as in play (user decision): the editor refuses a
  level whose requirements are missing and the removal of a building something built requires, and the validator flags
  maps that break them. Pre-built dwellings follow the 7-of-9 rule.
- **Town turn, HoMM3-style** (user decision): on the owner's turn, with no decision pending, the owner may at any time
  build (one building per town per day, requirements and cost checked), build or upgrade a dwelling (7 of 9, a refused
  build would leave a tier unreachable, ADR-030), recruit, upgrade whole stacks for the price difference, and move
  stacks between the garrison and the visiting hero (an own hero on the entrance; it keeps one stack). Recruits go to
  the garrison or to the visiting hero. All are commands (`BuildCommand`, `BuildDwellingCommand`, `RecruitCommand`,
  `UpgradeStackCommand`, `MoveTownStackCommand`) and replay like the others.
- **Growth**: a new dwelling opens with one week of its creature's `growth`; every Monday each built dwelling adds its
  growth, owned or not, and unbought creatures accumulate. An upgraded dwelling sells the base or the upgraded
  creature from the same pool. Fort growth bonuses are still open.
- **Income**: an owned town pays the income of the highest built level of each building (town hall 500 / 1000 / 2000
  gold, resource silo +1 lumber +1 ore), through `TownIncome`.
- **Capture**: entering an enemy or neutral town with no garrison captures it. A garrison is fought first, as a guard
  commanded by the town's owner (the AI for computer players and neutral towns) without hero stats or skills; beating it
  captures the town, losing leaves the survivors in the garrison. A hero without creatures cannot attack a garrison. An
  enemy hero standing on the entrance is a normal hero battle; the garrison does not join it (for now). Allied towns can
  be entered but not managed. Owned towns watch a radius of 5 like heroes (mines: 2).
- **Map format v6** adds `town` objects; older maps load unchanged.
- **UI**: the 2D harness has a basic town panel (`TownPanel`; `--scenario=towns` loads `TownsScenario`), the editor
  inspector edits towns, and the 3D game shows towns with flag and name. **The town screen is 2D in the first
  iteration** (user decision), also in the 3D game: a full-screen 2D panel over the map; a 3D town scene may come later.
  The game app (2026-10-17) opens it (`TownPanel` with `FullScreen`, on its own canvas layer above the HUD) when an own hero
  steps into an own town on a human's turn, from the town list in the HUD (`TownListPanel`, next to the hero list; the map
  centres on the town) or with **T**; Escape or Close shuts it, and it takes the input while open. It also shows the
  governor with Appoint / Dismiss buttons (ADR-063).
  **The hero screen** (`HeroPanel`, same canvas layer and the same full-screen mode) opens with **H** for the selected hero or
  the "Info" button in the hero list; Escape or Close shuts it, "<" and ">" go through the player's heroes. It shows stats
  with what artifacts add, mana, experience, specialty, skills with their descriptions, the army (move and split stacks
  inside the hero), war machines, the paper doll and the backpack (click to take off or put on). All of it is commands
  (`MoveStackCommand`, `EquipArtifactCommand`, `UnequipArtifactCommand`).
- **Not yet**: effects of fort, marketplace trading and mage guild (the blacksmith sells war machines, ADR-056; the
  tavern hires heroes, ADR-060); town sieges.

### ADR-051: No sea travel
**Status**: accepted (2026-10-16), from [ideas.md](ideas.md) (user decision)
- Water bodies are **obstacles only**: deep water stays impassable, and there are no boats, shipyards, sea travel or
  navigation skills (no Navigation-like skill in any group). Shallow water stays walkable terrain (ADR-045).
- Replaces "boats later" (ADR-045) and the planned boats/shipyards map objects. Random maps already left out water
  zones and islands for 1.0 (ADR-034).

### ADR-052: The 3D battle scene
**Status**: accepted (2026-10-16); replaces the game app's quick-combat-only fallback (Phase 2)
- **Same scene tree, own camera.** `BattleView3D` (Godot.Shared) lives next to the adventure map, far from it
  (`BattleSpace.Origin`), and has its own `Camera3D`. While a battle is shown, the app hides the adventure map and its
  HUD and pauses the strategy camera; after the result screen it switches back. No scene change, so the session,
  views and subscriptions stay as they are.
- **Built from events only.** The view builds the battlefield from `BattleStarted` (units, obstacles, width) and
  follows `BattleUnitMoved` / `Attacked` / `Died` / `TurnStarted` / `Ended`. Every event is one step of a single
  animation queue, because the core resolves a whole AI turn (or a quick combat) inside one command. Geometry of
  queued steps uses where units *will* stand, not the live state, which is ahead of the animation. The view is busy
  while the queue plays; reachable cells, previews and input are offered only once it has caught up. Quick combat and
  battles without a human side skip the animation.
- **Pointer intent is a core query.** `BattleTargeting.ActionAt` turns a pointed cell (with the exact pointer
  position) into the `BattleAction` a click would perform: shoot an enemy if possible, else melee it from the
  reachable side nearest the pointer, else move to the reachable placement covering the cell. Engine-free and tested;
  the 3D game and the 2D harness both use it, and the rules still validate the command.
- **Placeholders for now.** The ground uses the adventure-map texture of the terrain the battle is fought on
  (`AdventureMapView.TerrainMaterial`); units are primitive meshes (capsule walkers, cylinder shooters, hovering
  sphere flyers) with a base in the side's colour, a facing nose and a count; obstacles are boulders and crags. Real
  models, animations and themed backdrops come with the art pass (Phase 5).
- **HUD** (`BattleHud`, Godot.UI): sides and round, initiative timeline (active, queue, waiting), the log in step with
  the animation, hovered or active unit stats, wait / defend / retreat / quick combat buttons (W, D, R, Q), and a
  result screen with casualties and experience. The width choice before the battle stays in `DecisionPanel`.

### ADR-053: Morale and luck
**Status**: accepted (2026-10-16), from [ideas.md](ideas.md); answers the former open question 5 (now CMB-2 for the sources). Rates marked *draft* were filled in
where the notes left them open; artifacts add morale and luck (ADR-061)
- **Values**: every stack has morale and luck from **−5 to +5**: its creature's (`morale` and `luck` columns of
  `data/creatures.csv`, empty = 0) plus its hero's (`Hero.Morale` / `Hero.Luck`, map data in format v7 and the
  editor inspector), limited to ±5 (`BattleState.MoraleOf` / `LuckOf`). Guards and garrisons have only their
  creatures'. Skills, artifacts and army composition will add to the hero's value later.
- **Morale**, rolled for every stack at the **start of each round**:
  - good morale: **2 % per point** to **act twice**: right after its action the stack acts again (moving, attacking or
    shooting; defending does not trigger it, and a wait postpones it). This is the one exception to "exactly once per
    round" (ADR-009);
  - bad morale: 2 % per point to **lose its turn** that round (it "freezes"; its defend bonus ends);
  - if that did not trigger: **4 % per point** for **+50 % initiative** (good) or **−50 %** (bad, at least 1) for that
    round's acting order.
- **Luck**, rolled on **every hit** (retaliation, area hits and ranged retaliation included) by the attacker and by
  each target: *draft* **4 % per point**. Good luck of the attacker or bad luck of the defender means **+75 % damage**;
  good luck of the defender or bad luck of the attacker **−50 % damage**. When both sides' rolls trigger in opposite
  directions, nothing happens; two triggers in the same direction still count once. An area attack rolls the
  attacker's luck once and each target's for itself. *Draft*: the user's notes covered positive luck only; bad luck as
  the mirror image is our reading.
- Events: `BattleMoraleTriggered(unit, effect)` (haste and slow when the round starts, the extra action as it begins, the
  lost turn when it would have come) and `BattleUnitAttacked.Luck`. A stack with morale and luck 0 draws no random
  numbers, so battles without either replay exactly as before.
- Not decided yet: which skills, artifacts, buildings and army mixes change morale and luck, and whether creatures such
  as undead or constructs ignore morale (HoMM3 does).

### ADR-054: Map dwellings and caravans
**Status**: accepted design (2026-10-16), implemented 2026-10-17 with the open questions answered by the user. Core, editor,
the town / dwelling screens and the AI (see the end) are in.
- **Map dwellings** (`CreatureDwelling`, definition `dwelling`, kind `Dwelling`, 3×2 like a mine, entrance bottom-middle) are
  flaggable map objects with one creature (any creature of the catalog, set per instance, default wolf) and a count waiting
  (`Available`; omitted on the map = one week's growth when the game starts). They grow by the creature's weekly growth every
  Monday, **owned or not** (`DwellingCreaturesGrew`). An own hero that visits one flags it (`DwellingOwnerChanged`; they reveal and
  watch a radius of 2 like mines; a defeated player's dwellings turn neutral). A hero standing on the entrance recruits from it
  with `RecruitAtDwellingCommand` (any dwelling, owned or not; paid at the creature's price, discounted by the **hero's own**
  Stewardship level, ADR-063; into the hero's army). **Map format v12** (`dwelling` objects: creature, owner, available); older
  maps load. The editor places it from the object palette and edits it with `MapEditorSession.SetDwelling` (inspector rows).
- **Caravan** is a town building (`caravan` level 1: 1000 gold, 5 lumber, needs town hall 2; placeholder numbers in
  `buildings.csv`). A town with one can **hire creatures from owned map dwellings** (`OrderCaravanCommand`; paid at once at the
  town's price, so the governor's Stewardship discount applies; the creatures leave the dwelling) and **send troops from its
  garrison to another own town** (`SendTroopsCommand`, whole stack or part; the destination does not need a caravan).
- **Travel** is virtual (`CaravanRules.FindRoute`, Dijkstra): the creatures walk like an army made of them (their map speed,
  `MovementAllowance`, terrain and roads as for heroes; no hero skills; native terrain counts for the creature's own town),
  8-way, diagonals ×1.41, from the source's entrance to the town's entrance. **The route must be open**: impassable terrain and
  blocked object cells, any hostile hero, any neutral monster **and every tile it guards** block it. More than **5 days**
  (`CaravanRules.MaxDays`) means the source cannot supply the town. Both are checked **before** paying. At least 1 day.
- **Arrival** (`CaravanService.Advance`, at the start of the owner's turn, in id order): the creatures join the destination
  garrison (`CaravanArrived`); with no free slot the caravan waits at the gates until there is one.
- **User decisions (2026-10-17)**: a route that gets **blocked while travelling** makes the caravan **return** (checked every day;
  creatures go back to the dwelling and the **price is refunded**; troops return to their town's garrison, or are lost
  (`CaravanLost`) when it has no room); a **destination that is captured or lost** makes the creatures **scatter** (lost,
  gold stays spent); caravans are **invisible and cannot be attacked** (they are not on the map; only the route check matters).
- Caravans live in `GameState.Caravans` and are created only by commands, so replays rebuild them; they are not in the map file.
  They are part of the state digest. A defeated player's caravans vanish.
- **AI** (`AdventureAi`): a hero values an unflagged dwelling and what it can buy there as a target and recruits when standing on it
  (keeps a third of the gold); a town builds the caravan early once the player owns a dwelling, hires from owned dwellings with
  its spare gold (at most two caravans on the way to a town) and sends its weakest stack to an own town that has no garrison and
  no hero, keeping at least one stack.
- Not yet: a dwelling guard (use a monster next to it), marketplace
  trading with the governor's 150 %.

### ADR-055: Primary stat growth
**Status**: accepted (2026-10-16), answers the former open question 8 (user decision: HoMM3-style, odds by class); odds and
starting values are a draft; Knowledge sets mana (ADR-063)
- Heroes have four primary stats: **Attack**, **Defense**, **Spell Power** and **Knowledge**. Spell Power and Knowledge
  are new (`Hero.SpellPower`, `Hero.Knowledge`); they do nothing until magic (Phase 5: spell strength and mana).
- **Every level-up raises one primary stat by 1**, besides the skill point (ADR-048). The stat is drawn from the game
  RNG with odds by class (`StatGrowth`), one table for levels 2–9 and one from level 10:

  | Class | Levels 2–9 (A / D / SP / K) | Levels 10+ |
  |---|---|---|
  | Warrior | 35 / 45 / 10 / 10 % | 30 / 30 / 20 / 20 % |
  | Mage | 10 / 10 / 40 / 40 % | 20 / 20 / 30 / 30 % |

  HoMM3 has one table per hero class of every town; ours has one per class for now, and may get per-town tables.
- The stat is rolled together with the skill offers, so the level-up prompt shows it ("+1 Attack. Choose a skill")
  (`LevelUpDecision.Stat`); `HeroLeveledUp` carries the stat and its new value. Computer heroes get theirs too.
- **Starting values** (`data/heroes.csv`, new columns `spell_power` and `knowledge`): every roster hero has 5 points;
  warriors keep their 2/1 or 1/2 Attack/Defense and get 1/1, mages get 3/2 (with 0/0) or 2/2 (with 0/1).
- Map format **v8**: map heroes may set `spellPower` and `knowledge` (omitted = 0 for custom heroes, the roster's values
  for roster heroes); the editor inspector edits them. The state digest includes all four stats.

### ADR-056: War machines
**Status**: accepted (2026-10-16), from [ideas.md](ideas.md) and user decisions; numbers are a draft; Ballistics partly
superseded by ADR-063
- **Three per hero**: the town's **offensive machine**, a **healing tent** and an **ammo cart** (`Hero.WarMachines`,
  `WarMachineSlot`). Every hero starts with its offensive machine; the tent and cart are bought at a **blacksmith**
  (`BuyWarMachineCommand`, the visiting hero, HoMM3 prices: offensive 2500, tent 750, cart 1000 gold), which also
  replaces a lost offensive machine. A hero gets its own town's offensive machine wherever it buys it.
- **Offensive machine by town**, paired by theme (user decision): ballista (Haven, Necropolis), cannon (Academy,
  Inferno), catapult (Stronghold, Swamp Bestiary), glaivethrower (Sylvan, Dungeon, shoots twice). Custom heroes without
  a town use the ballista. Stats are code (`WarMachines`), not the creature table, so machines never appear as
  guards or recruits.
- **War machines skill group** (`machines`, 4 + 4 + 2 = 10 points, user decision):
  - **Ballistics** (4 levels, the group's main skill): the offensive machine fights only in **sieges** until **Advanced**
    Ballistics, then in **regular battles** too (user decision); Expert +25 %, Master +50 % its damage. Sieges do not
    exist yet, so Basic Ballistics has no effect for now.
  - **Healing** (4 levels): the tent restores 10 health (no skill) + 10 per level to the top creature of **every own
    stack** at the start of each round, never above its maximum and without reviving anyone (user decision: simpler).
  - **Ammo supply** (2 levels): the cart gives every own shooter **+25 %** shots at battle start without the skill,
    **+50 %** with Basic, and **unlimited** shots with Advanced while the cart stands (user decision: simpler; the
    +25 % without the skill is our draft).
- **Units on the field** (user decision): machines deploy in a column of their own at their side's back edge (tent at
  the top, offensive machine in the middle, cart at the bottom), the army in front of them. They never move, never
  fight in melee, never retaliate and have no morale; enemies can attack and destroy them. Only the offensive machine
  takes turns (by initiative, commanded like any unit; it has unlimited shots and no long-range penalty); the tent
  and the cart work passively. A side whose creatures are all gone has lost, machines or not. **Destroyed machines are
  lost** (`WarMachineLost`); the hero buys new ones.
- Map format **v9**: heroes may list `warMachines` (omitted = the offensive machine only); the editor inspector edits
  them. The 3D battle shows machines as wooden boxes with a barrel, roof or wheels; the 2D harness town panel has the
  blacksmith.
- Open: siege use (walls, Basic Ballistics), stats and prices after play-testing, exchanging machines between heroes,
  machines in garrisons.

### ADR-057: Victory and defeat
**Status**: accepted (2026-10-16), from the roadmap's game flow items and a user decision: the default is to defeat every
enemy, and a map can configure other conditions in the editor; save/load built (ADR-058); a defeated player's map
dwellings also turn neutral and its caravans vanish (ADR-054)
- **Default rules** (`ScenarioRules.Default`): a player with **no towns and no heroes is eliminated at once**, and the
  game is won when **one team is left standing**.
- **Grace period** (HoMM3's rule, `GraceDays`, default 7, 0 = off): a player that **owned a town and has none any more**
  is eliminated at the start of day *S + GraceDays* (*S* = the day it lost the last town) unless it owns a town again
  before; its heroes do not save it. `GracePeriodStarted(Player, DefeatDay)` and `GracePeriodEnded` tell the UI;
  `Player.TownlessSince` is the query. Players that never owned a town (hero-only starts) have no countdown.
- **Per scenario** (`GameState.Rules`, map format **v10**, editor tab *Conditions*, undoable with `SetScenarioRules`):
  - `GraceDays` sets the days a player without towns has (editor spin box, map field `graceDays`, omitted = 7).
  - `StandardVictory` and `Elimination` can each be switched off (`ScenarioRules.Endless` is a sandbox that never ends).
  - **Special victory conditions**, any one of which wins the game for the player's team: capture a town, defeat a hero
    (it is gone and its owner is an enemy of the player), gather an amount of one resource, survive until a day.
  - **Special defeat conditions**, any one of which eliminates the player: lose a town, lose a hero, a time limit (the
    player did not win by day N).
  - A condition may name one player; without one it applies to everybody and the first player it holds for, in turn order,
    wins. A defeat condition about a town or hero must name the player (the editor fills in the owner).
- **Who takes part**: only players that start with a hero or a town can be eliminated or win. A game that starts with a
  single team never ends by defeating enemies, which keeps one-sided tests and sandboxes running.
- **Order** (`GameFlowService.Evaluate`, after the game starts and after every accepted command): special victories, then
  defeats (special ones and the default), then the default victory. A battle in progress is settled first.
- **Defeat**: the player's heroes are removed (`HeroDefeated`), its towns and mines turn neutral, `PlayerDefeated` is
  published, its turns are skipped. A player defeated in its own turn passes it on at once.
- **The end**: `GameState.Outcome` (winners: the surviving players of the winning team; the reason in words) and
  `GameEnded`. From then on every command is refused ("The game is over"). The digest includes defeats and the outcome,
  so replays prove them.
- **Maps**: `MapDocument.Rules` is omitted for the default; older maps load with it. The validator reports conditions that
  point at a missing town, hero or player (errors), ones true at the start, and scenarios nobody can win (warnings).
- **UI**: the game shows a result screen (Victory, Defeat or Draw, the reason and the winners) after the battle screen, if
  one decided the game, has been closed.
- Open: conditions that need artifacts or
  objects (grail, flag all mines), team-wide survival, campaign hooks.

### ADR-058: Saved games
**Status**: accepted (2026-10-16), from the roadmap's save/load item and a user requirement: saving and loading must also
work during combat
- **A save is a game record** (ADR-036): the map as it was when the game started, the options (seed, fog) and every
  accepted command, plus a label (`SaveInfo`: name, UTC time, calendar date, whose turn, in battle, map name). The record
  document is version 2 (`GameRecordDocument.Save`; version 1 replays still load); saves use the extension
  `*.ewsave.json`. `GameSaves` writes (to a temp file, then moves it over the old one), lists (newest first; damaged files
  are listed with their error), loads and deletes.
- **Any moment works, a battle included**: battle commands are commands, so replaying the log lands in the same
  battle, in the same round, with the same active unit (`BattleView3D.Restore` shows it; a battle still choosing its width
  just opens when the width is chosen). The loaded state is proved against the saved `GameStateDigest`; a save made with
  other content (changed creature table) fails with that error instead of loading a different game.
- **No snapshot yet**: loading replays the whole game, which is fast for a game of a few hundred commands. If it gets too
  slow, a state snapshot taken every N days and replayed from is the planned way (roadmap).
- **The game starts from a map document**, taken right before `Start` (after the setup screen's team choice): the
  game app keeps it for saving. A loaded game keeps the map of its record, so saves can be saved again.
- **Game app**: F10 or the Menu button opens the list (name field, Save, Load, Delete); F5 quick-saves and F9 quick-loads
  (`quicksave`), on the adventure map and in battle. Saves live in `user://saves` (on Windows
  `%APPDATA%\Godot\app_userdata\Eternal Wars\saves`). The menu is also on the new-game screen, for loading.
  Loading rebuilds the world, HUD and battle scene from the loaded session; the battle HUD opens at the point it was
  saved. A game that is over can be saved and loaded too.
- **Autosave** (user decision: two slots): at the **start of every turn** (each player's turn, also at game start) the game
  app writes `autosave-current` and first moves the one that was current to `autosave-previous`, so the start of the
  latest turn and of the turn before are always there (`GameSaves.Autosave`; the new file is complete before the slots
  rotate). The save is made after the command that began the turn is logged and settled, from the new event
  `CommandExecuted(Command)` that `GameSession.Execute` publishes last: `TurnStarted` itself comes before the command
  is logged, and a save then would not match its digest. The slots are listed in the menu as "Autosave: start of the
  latest turn / of the turn before", cannot be written over by name, and loading one does not autosave. Games that are
  loaded resume without rewriting the slots until the next turn starts.
- Not covered: saving the 2D harness, a save from the editor-made `ewreplay` files, and saving games whose state changed
  outside commands (nothing does).

### ADR-059: The adventure AI
**Status**: accepted (2026-10-16), first version; the numbers are a draft to tune with `sim game`; hiring heroes and
artifacts built by ADR-060/ADR-061, dwellings and caravans by ADR-054; defence, several heroes, war machines and governors added (2026-10-17, below)
- **Through commands only** (`AdventureAi` in `Core.Domain/AI`, ADR-036): it plays with `GameSession.Execute`, so its
  turns are in the command log and replay exactly. It draws **no random numbers**; the same state gives the same turn.
  It uses only what its player has explored (pathfinding and targets), not the whole map.
- **Who plays**: computer players (`!IsHuman`) in the game app (`Main._Process`, one turn a frame, not while a battle
  scene or the menu is open); with `controlHumanSlots` every slot, as in the simulator. `PlayTurn()` can be called again
  after it returned false (a human has to act in a battle) and carries on where the turn stands.
- **A turn**: answer pending decisions (chest: experience if it levels the hero, else gold; guard offers: accept a join,
  fight a stack that would flee; exchange: close; level-up: the first offer, which computer heroes get anyway), run the
  towns, move each hero, run the towns again, end the turn.
- **Towns**: one build a day from a fixed priority (the order is the strategy): one dwelling per tier from tier 1 up
  (the line with most weekly power), marketplace, blacksmith, mage guild, town hall, dwelling upgrades, fort, town hall,
  second lines, tavern. It **saves gold** for the building it wants when that is within about three days of income (and
  other resources within a week), skips buildings that need resources it will never get, upgrades stacks whose dwelling is
  upgraded, recruits the strongest creatures first (a first garrison of the cheapest line, half of what waits), and a
  visiting hero takes the garrison with it except the weakest stack.
- **Heroes**: every known object gets a value in gold (piles by resource, chests 1500, mines eight days of yield, towns
  4000+, monsters a share of their strength, much more when they would join, enemy heroes 2000 + 80 % of their army power,
  own towns with a lot waiting). The best of `value / (0.5 + days of travel)` among the ten best candidates wins; with
  nothing to do the hero explores the nearest reachable explored tile next to unexplored ground. A hero makes at most 24
  moves a turn.
- **Fights are played, not guessed** (`BattleEstimator`): before attacking a monster, a guard, a garrison or a hero the AI
  runs three battles with the real engine on a throwaway game (fixed seeds of their own, so the game's random numbers are
  untouched). It goes only when it wins all three and loses at most 40 % of its army power against monsters, 50 %
  otherwise. The power sum (Σ count × creature value) is only a pre-filter: it called a fight even that was lost every
  time, because it does not see shooters.
- **Exploring never walks into** heroes, objects or guarded ground, which are targets with a fight check.
- **`sim game`** plays a map with the AI in every slot (`--map towns|demo|file`, `--days`, `--seed`, `--runs`, `--record`)
  and prints the outcome and each player's towns, heroes and army; the self-test replays an AI game through JSON.
- **Not yet**: spells, difficulty levels, using the marketplace, sieges. (Defence, several heroes, war machines and governors: below.)

**Phase 3 follow-ups (2026-10-17)**:
- **Town defence**: a visible enemy hero threatens what lies within a day's march of it (max movement / 150 tiles, a rough average). An own town it
  threatens that its garrison alone could not hold (garrison below 80 % of the enemy's power) is a target worth 6000 for a hero that can hold it together
  with the garrison; a hero already in such a town stays there. Heroes **avoid** places a stronger enemy can reach: a target or an exploring step is
  taken only when the hero (with the garrison, at an own town) has at least 80 % of the strongest threat's power there, the enemy being attacked
  left out. Without this a computer player lost its towns to whoever walked in first; the sim game matches on the towns map last longer now.
- **Several heroes**: the strongest army is the **main hero** and moves first; the others explore and fight as before, and carry troops: one with at
  least 1000 army power (or a quarter of the main army) goes to the main hero, and the meeting's exchange hands over its stacks, strongest first (same
  creature, a free slot, or a swap for a weaker stack of a full army); it keeps one creature.
- **War machines**: after building and recruiting, the visiting hero buys a healing tent, an ammo cart when its army shoots, and its offensive
  machine back once lost when it can field it (Advanced Ballistics), each only while 1500 gold stay beyond what is saved for a building.
- **Governors**: heroes with Stewardship that govern nothing are appointed to the ungoverned towns that spend the most (most dwellings, then the
  highest town hall), the best steward first (ADR-063).

### ADR-060: The tavern and hiring heroes
**Status**: accepted (2026-10-16), from user decisions on who a tavern offers; cost, upgrade price, limit and placement
are drafts
- **Tavern levels**: the tavern is an upgradeable building (`data/buildings.csv`): **Tavern** (750 gold, 5 lumber; every
  new town has it) offers **2** heroes, **Brotherhood** (2000 gold, 5 lumber, 5 ore, needs the fort) offers **4**.
- **Ranks** (`TavernRules`): the six main towns sit on the **ring of towns** (user decision, [lore.md](../content/lore.md)): Sylvan, Haven,
  Academy, Dungeon, Necropolis, Inferno, and back to Sylvan. For a town, a hero is **0 native**, **1 adjacent** (the
  town's two ring neighbours: Sylvan: Inferno, Haven; Haven: Sylvan, Academy; Academy: Haven, Dungeon; Dungeon: Academy,
  Necropolis; Necropolis: Dungeon, Inferno; Inferno: Necropolis, Sylvan), **2 not opposing** (the two towns two steps
  away, Stronghold and Swamp Bestiary) or **3 opposing** (the town across the ring, user decision: Sylvan–Dungeon,
  Academy–Inferno, Haven–Necropolis). The pool order is that order. The first version of this ADR used another ring
  (the sworn-enemy ring) and was corrected by the user.
- **Slots**: basic = native + adjacent; upgraded = native, native, adjacent, not opposing. Opposing heroes come last,
  only when nothing else is left. A slot with nobody left in its rank takes the next ranks, then the earlier ones.
- **Stronghold and Swamp Bestiary** (user decision) have no ring: rank 0 native, **1 warriors of the other towns**, 2 all
  other heroes. Basic = native + warrior (our reading); upgraded = 2 native + 2 warriors.
- **The pool is per player and weekly**: `TavernRules.PoolOrder` shuffles the roster from the game seed, the player and the
  week, so every player sees other heroes, the offers change every Monday, and nothing is stored or drawn from the
  game's random numbers. A hero on the map (anyone's) is not offered; a defeated or retreated hero is back in the pool.
  Hiring lets the next hero of that rank step forward; there is no empty slot until Monday.
- **Hiring** (`HireHeroCommand`, `TownService.HireHero`): the owner's turn, an own town with a tavern, a hero the town
  offers now, **2500 gold**, the entrance free, at most **8 heroes** per player. The hero stands at the entrance with its
  roster stats and skills, no army, full movement. Heroes get ids never used before (`NextUnusedHeroId`).
  `HeroHired(Town, Hero, Definition)` is published; visibility reacts to it.
- **UI**: the town panel lists the offers with a hire button (and why a hire is refused).
- **AI**: it hires the best offer (warriors first) while it has fewer heroes than one more than its towns, at most four, and
  the gold beyond what it saves for a building allows; the new hero is the town's visiting hero, so recruits go to it.
- Open: the draft numbers, whether surrender comes with taverns (ADR-038), per-town pools, hiring when an own hero stands
  at the entrance, and hero level or starter stacks for hired heroes.

### ADR-061: Artifacts
**Status**: accepted (2026-10-16), from [artifacts.md](../content/artifacts.md) and the roadmap; the numbers are a draft, and
the list was extended with HoMM3-style placeholder items; hero screen with paper doll and backpack built (`HeroPanel`, ADR-050)
- **Content is a table** (`data/artifacts.csv`, `data/artifact_sets.csv`, embedded in core, read by the apps at start-up,
  `.csv.import` "keep" like the other tables): `id, name, slot, tier, set, effects, notes`. **67 artifacts**: the
  36 lore artifacts of [artifacts.md](../content/artifacts.md) and 31 **HoMM3-style placeholders** (Centaur's Ax, Blackshard of the Dead
  Knight, Ogre's Club of Havoc, Sword of Hellfire, Titan's Gladius, the Dwarven Lords' and Gnoll King's shields, Sentinel's
  Shield, Skull Helmet, Crown of the Supreme Magi, the body armours from Petrified Wood to Titan's Cuirass, Pendant of
  Courage, Boots of Speed, Cape of Velocity, the Endless purses, bags and sack of gold, …), marked "HoMM3 style
  placeholder" in their notes, with our own flavour lines. Four **sets** (Pilgrim's Road, Armor of the Dawn Covenant, Ash Court
  Regalia, The Unbound's Trophies) add a bonus when every piece is worn.
- **Effects the game applies** (`ArtifactEffect`): Attack, Defense, Spell Power, Knowledge (only primary stats until
  magic), morale, luck, movement percent and flat, battle speed of every stack, melee and ranged damage percent, damage
  taken (all, and ranged only), daily income of a resource, native terrain (the hero's army walks it with no penalty),
  terrain penalty reduction (adds to Pathfinding, at most all of it), skill levels (one skill, or every started skill,
  capped at the skill's maximum). Effects the game cannot apply yet (mana, spell resistance, regeneration, re-rolls,
  initiative) are in the notes as "Not yet". Negative values are allowed (Titan's Gladius: +12 Attack, −3 Defense).
- **Paper doll** (`ArtifactSlot`, `HeroArtifacts`): head, neck, cloak, body, weapon, shield, 2 rings, boots, 3 miscellaneous
  and a **backpack of 50** (user decision; first 12). The rules read the sum of what is worn (`ArtifactBonuses`, kept on the hero by
  `GameState.RefreshArtifacts`; `Hero.TotalAttack`, `SkillLevelOf`, `Bonus`), never the artifacts one by one; base stats
  stay what they were, so taking an artifact off restores them.
- **Commands** (the owner's turn, no movement points): `EquipArtifactCommand` (from the backpack; the one worn in a full slot
  goes to the backpack), `UnequipArtifactCommand` (needs backpack room), `GiveArtifactCommand` (to another own hero, only
  while their exchange is open; the backpack copy goes first). Events: `ArtifactEquipped`, `ArtifactUnequipped`,
  `ArtifactGiven`, `ArtifactGained` (pickup or loot), `ArtifactLost`, `ArtifactBackpackFull`.
- **On the map**: `ArtifactPickup` objects (`artifact.minor`, `artifact.major`, `artifact.relic`); a hero stepping on one
  puts it in the backpack (a full backpack leaves it there). An open pickup **rolls its artifact when the game starts**
  from the definition's tier, from the game RNG, skipping artifacts that are already placed or carried, so relics stay
  unique while any are left (`ArtifactService.RollUnsetPickups`; no random numbers are drawn when nothing is open). Classic
  guard mode lets a pickup in a guarded tile be taken from outside, like piles and chests.
- **Loot** (user decision: all of them go to the winner): a hero that wins a battle against a hero (a defeat, not a retreat) gets all its artifacts, worn and carried, into
  its backpack; when they do not all fit, the **most valuable come first** (`ArtifactValue.BestFirst`: effects weighed, then rarer tier,
  then id; user decision) and the rest are lost. A hero that flees takes them off the map.
- **Map format v11**: heroes list `artifacts` (worn ids, each to the slot its definition names) and `backpack`; objects of kind
  `artifact` with an optional `artifact` id. The editor session has `SetHeroArtifacts` and `SetArtifactPickup` (undoable); the
  validator flags unknown artifacts. Games load their catalog like the other tables (`ArtifactCatalog` parameter).
- **AI**: it values artifacts by their effects (`AdventureAi.Score`: Attack and Defense most, battle speed, morale and luck
  high, income by gold), goes for pickups it can reach and win, and puts on what fills a free slot or beats the worst worn
  there by 20 %.
- **Not yet**: artifacts in treasure chests (ADR-039 stays without the artifact outcome), the editor inspector rows, a merchant, artifacts that give spells or war machines, the effects marked "Not yet".

### ADR-062: Treasury interest and Prospecting piles (2026-10-17)
**Status**: accepted (2026-10-17); supersedes ADR-048's Treasury numbers
- **Treasury** pays **+250 / 500 / 750 / 1000** gold a day (was 100 / 200 / 350 / 500). Expert and Master also pay **0.5 % / 1 %**
  of the player's gold on hand at the start of the day, before income is added (rounded down, `HeroIncome.TreasuryInterest`).
- **Prospecting** keeps its daily resources and adds a **10 / 20 / 30 %** chance that a picked-up resource pile gives 50 %
  more (rounded up). The roll is drawn from `GameState.Random` only when the hero has the skill; `ResourcePileCollected`
  carries the amount actually granted.

### ADR-063: Governors, complexity, mana, faction groups and siege Ballistics (2026-10-17)
**Status**: accepted (2026-10-17)
Supersedes parts of ADR-047 (eight schools), ADR-048 (Stewardship, attunement) and ADR-056 (Ballistics). Numbers are drafts.

- **Governors and Stewardship**: a hero with Stewardship can be appointed governor of an own town (`AppointGovernorCommand`,
  `DismissGovernorCommand`, event `GovernorChanged`); a hero governs one town, appointing it elsewhere moves it. The
  assignment persists while the hero roams; it is void when the hero is gone, changes owner or loses the skill
  (`StewardshipRules.GovernorOf`), and is cleared when the town is captured. A visiting own hero with the skill becomes
  governor of a governor-less town by itself (on arrival and at the start of the owner's turn) unless it governs
  another. Stewardship applies only to a town with a governor: buildings **5 / 10 / 15 %** cheaper, creatures
  **10 / 15 / 20 %** cheaper (recruiting and upgrading; rounded up per resource). Map dwellings use the recruiting hero's own level and caravans the
  destination town's governor (ADR-054). Not built yet: marketplace trading (a governed town values resources at **150 %**,
  `StewardshipRules.MarketplaceValuePercent`).
- **Complexity** (`GameOptions.Complexity`, null = the map's own resources): Beginner, Easy, Normal, Hard, Expert. At the
  start of the game every player gets `StartingResources.For(level)`: 10000 gold with 30 lumber, 30 ore and 10 of each rare
  resource; 7500, 20, 20, 6; 5000, 15, 15, 3; 3500, 12, 12, 1; Expert 2500 gold, 10 lumber, 10 ore, no rares (the
  designer's hardest set; the other four are guesses to tune). Chosen on the new-game screen; saved in replays.
- **Mana** (`ManaRules`, `Hero.Mana`): the pool is **10 x Knowledge** plus attunement; every day at the start of the owner's turn a hero
  regains **ceil(Knowledge / 10)** plus attunement (`HeroManaRestored`, only when the pool is not full); a spell costs its base
  minus attunement but never less than **25 % of the base, rounded up**. A hero that never spent mana has a full pool.
  Casting waits for Phase 5.
- **School attunement** (the second skill of each of the six schools, 4 levels):

  | Schools | Max mana | Mana a day | Spell cost |
  |---|---|---|---|
  | Arcana, Chaos | +12 / 25 / 37 / 50 | +1 / 2 / 3 / 4 | - |
  | Light, Death | - | +3 / 6 / 9 / 12 | -1 / 2 / 3 / 4 |
  | Nature, Shadow | +5 / 10 / 15 / 20 | +2 / 4 / 6 / 8 | - |

  Several attunements add up.
- **Stronghold and Swamp Bestiary have no magic school**: six schools remain. Each has a faction ability group instead,
  **Rage** (`rage`, Stronghold) and **Adrenaline** (`adrenaline`, Swamp Bestiary): three placeholder skills each
  (`rage.first/second/third`, 4 + 3 + 3 points) with no effect; the mechanics (stacks of rage / adrenaline or other
  faction abilities) are open. Only heroes of the town are offered them; their roster mages start with `.first`, and their
  former spell specialties are `skill rage.first` / `skill adrenaline.first` for now. The Shamanism and Primal spells in
  [spells.md](../content/spells.md) are drafts waiting to be reused as faction abilities.
- **Ballistics in sieges** (rules in `WarMachines`; fortifications and sieges do not exist yet, so nothing calls them in battle):
  without the skill the offensive machine **fires by itself, only at fortifications**; **Basic** gives control of it; **Advanced**
  lets it also target troops (and fields it in regular battles, as ADR-056 had it) and fires a second shot at an automatically
  chosen target when the first destroyed its structure; **Expert** never misses a fortification (otherwise 2 of 3 shots hit)
  and keeps the +25 % damage against troops; **Master** does **2-3** structure damage instead of 1-2 (and +50 % against troops).
  A fully upgraded fortification's defence structures have **3** hit points. Placeholder formulas.

### ADR-064: Random map generation: project layout and seed streams (2026-10-17)
**Status**: accepted; stages 1–6 and the scenario assembly implemented (layout, terrain, connections, towns/mines, roads, treasure; `MapGenerator`);
validation of the whole map, mirroring and the new-game screen pending
- `src/Core/EternalWars.Core.Generation` exists (engine-free, warning-free like every core project). It references `Core.Domain`
  only; `Core.Data` references it (template file format). It is a dependency of the tests, and later of the `sim` tool and the apps.
- **Refines ADR-023**: the generator outputs a **scenario `GameState`** (never started: the same object the map editor works on),
  not a `MapDocument`, because `Core.Generation` must not depend on `Core.Data`. `MapConverter.ToDocument(GameState)` turns it
  into the ordinary map file, and the game starts it directly. Generated and hand-made maps still share one pipeline.
- **Seed streams** (`SeedStreams`): each pipeline step draws from its own `GameRandom` derived from the map seed, the step's name and
  an optional index (a zone, a player), with a fixed FNV-1a hash. Tuning or adding a step does not change the numbers other steps
  see, so a seed keeps reproducing the same map where nothing it depends on changed. The pinned test values must change only with
  a generator version bump (shared seeds break).
- Generation never touches `GameState.Random`; the finished map's game starts with the seed of the game options as usual.
- Planned order: template model and validator, zone layout, terrain, borders, connections and guards, towns, mines, treasure,
  monsters, roads, validation (roadmap, Phase 4).

**Template model and file format (2026-10-17)** (`MapTemplate`, `TemplateZone`, `TemplateConnection`, `TemplateValidator`; files `*.ewtemplate.json`
via `Core.Data/Templates/TemplateSerializer`, format version 1; the shipped ones are embedded from
`EternalWars.Godot.Assets/data/templates`, listed by `BuiltInTemplates`; first one: `duel`). Decisions taken for the first version:
- **Zone size is a relative weight** (at least 1): the layout will give every zone a share of the map in proportion.
- **Guard strength is army power** (`GuardValue`, creature value × count, the AI's measure); monster strength of a zone is
  `None / Weak / Average / Strong`, mapped to power by the generator later (tune with play-testing).
- **Connections** are `Normal` (guarded passage), `Wide` (unguarded) or `Gate` (a pair of linked two-way gates, the only way
  across levels). Not in the first version: portals, optional or random connections and mirroring (the format version allows adding them).
- **Start zones**: exactly `MaxPlayers` of them (`PlayerStart` for anyone, `AiStart` computer only), each with a distinct owner slot and at
  least one player town; other zones have no owner and no player towns. `MinPlayers` ≤ players ≤ `MaxPlayers`; with fewer players the generator
  will leave the extra start zones out or neutral (decided with the generator).
- The validator reports **errors** (unusable: broken references, unreachable zones, impassable terrains listed, gate-less level changes,
  start zones that do not match the player count) and **warnings** (zones too small for the smallest size, start zones that differ in size,
  towns, mines, treasure, monsters or distance to their rivals).

**Zone layout, stage 1 (2026-10-17)** (`ZoneLayouter.Layout(template, size, seed, strategy)` → `ZoneLayout`: a zone index per tile and level, text dump,
`UnmetConnections`). Contract: every tile belongs to a zone of its level, every zone is one 4-connected piece, zones get their weight's share of
the map (within about ±40 %), and zones the template joins on one level **touch** (gates need no contact). Deterministic from (template, size, seed,
strategy); it draws only from the `layout` stream per level and uses exact arithmetic (no trigonometry), so it is the same on every platform.
- **Voronoi** (default): a spring model places the zone centres (joined zones attract, all repel; start from a random scatter or from a layered
  embedding with the best connected zone in the middle), then a local search on a 32×32 version of the map (`ZoneLayouter.CoarseSize`; first written here as 48×48) moves centres and swaps zones until every
  connection touches (the objective is the real contact of the area-balanced power diagram), the winner is painted at full size, re-balanced and repaired.
  Make every connection touch on all seeds tried for the shipped templates, including the 4-player wheel graph.
- **Rectangles**: recursive weighted bisection, the cut order a random walk over the zone graph. Blocky, fast, and only good for simple graphs; what it
  cannot make touch is repaired the same way and otherwise reported in `UnmetConnections`.
- **Repair** (`Join`): a connection that still does not touch gets a way cut through other zones along the shortest path, half to each side, through tiles
  those zones can spare without falling apart and that no other contact depends on.
- `sim layout <template> [--size N] [--seed S] [--rects]` prints a layout as text. Second shipped template: `Free for all` (4 players).
- The validator now also refuses a template with a level that has no zone.

**Terrain, borders and obstacles, stage 2 (2026-10-17)** (`TerrainPainter.Paint(template, layout, seed)` → `GeneratedTerrain`: the `AdventureMap`, the layout, `Walls` per pair of
zones and the list of `Obstacles`). User-approved defaults:
- **Terrain**: one main terrain per zone from its `Terrains` list, else the default pool of its level (surface: grass, dirt, rough, highlands, forest floor, sand,
  snow, swamp, wasteland; underground: subterranean, rocky ground, lava); a few patches (3-8 % of the zone each, up to four) of another allowed terrain, or
  dirt / rough / grass for a zone without a list. Terrain is drawn from the `terrain` stream per zone. A start zone's town type is not known at this stage (players pick it at setup), so native terrain is not matched.
- **Borders**: a tile with another zone among its **eight** neighbours becomes `Rock`, so walls are two tiles thick and nothing slips through diagonally (movement is 8-way).
  `Walls` lists the wall tiles per pair of touching zones (a tile at a corner is in several lists): the connections stage cuts its passages there. A walkable pocket a wall
  cuts off is filled in, so each zone keeps one 4-connected walkable piece.
- **Obstacles**: `TemplateZone.Obstacles` (`None / Sparse / Normal / Dense`, default `Sparse`; template format unchanged otherwise): clumps of 2-6 rock tiles, 3 / 8 / 16 per
  thousand tiles of the zone, kept three tiles from the walls and apart from each other; a clump that would cut off part of its zone is dropped (`obstacles` stream).
- `sim layout ... --terrain` prints the painted terrain (map file terrain codes; `#` is rock).

**Connections and their guards, stage 3 (2026-10-17)** (`ConnectionPlacer.Place(template, map, seed)` on a `GeneratedMap`: the terrain of stage 2 plus objects, occupied tiles,
`Passages` and `Problems`). User decisions:
- **Passages per connection are configurable**: `TemplateConnection.Passages` (1 to 8, default 1; one or more, spread along the border at least `max(6, size/12)` tiles apart; the template
  editor will expose it). Extra contact between the zones stays walled. A border too short for all of them gives as many as fit and a problem line.
- **Guards pick a tier by army power with overlapping ranges** (`GuardPicker`): every tier has the counts that look like a guard for it (tier 1: 6–150, 2: 2–80, 3: 1–40, 4: 1–20, 5: 1–10; superseded by the stage 4 addendum below: 6–60, 2–40, 1–25, 1–15, 1–10, `GuardPicker.CountsFor`);
  a creature qualifies when the stack it would need (`value / creature value`) is in its tier's range and within 20 % of the wanted power. So 500 can be about ten tier-1 creatures or
  three tier-2 ones, and 1000 five tier-2 or three tier-3 ones: the same tier in another number, or an adjacent tier. One qualifying creature is drawn (base forms of the playable
  towns and the neutrals), the count is rolled within ±5 %. With none qualifying, the nearest is taken with its count held to the range.
- **Normal** passage: a one-tile way through the wall with the guard on it (`Aggressive` neutral stack, `GuardValue` > 0); **Wide**: the same, five tiles wide (radius 2), never guarded;
  **Gate**: per passage a linked pair of two-way gates (monoliths on one level, subterranean gates across levels), one in each zone, near the border, each with a guard of the full
  value beside its entrance. A passage only opens wall tiles that touch no ground of a third zone (no leaks); if that finds nothing it is allowed to, so a corner contact still opens.
  Thin borders are done first.
- **Layout change**: the Voronoi search now prefers layouts where every connection shares at least about 3 tiles per 32 of map size, taking a layout where all merely touch after 16 tries.
  `sim layout <template> --seeds N` generates N maps (layout, terrain, connections) and lists the ones with problems (Free for all at 96: about 1 in 60 leaves a connection untouched).

**Towns, mines and resources, stage 4 (2026-10-17)** (`SettlementPlacer.Place(template, map, seed)` after the connections; `ZoneGuards` for the strength-to-power table). HoMM3-style defaults
(the user asked for them):
- **Start towns**: each start zone gets `PlayerTowns` (rolled in its range, at least one) in the middle of the zone, as unowned towns of a placeholder type (the zone's first `TownTypes`
  entry, else Haven). `GeneratedMap.Towns` records the player slot of each (`GeneratedTown.Slot`); game setup chooses the type and `GeneratedMap.SetTownType` swaps the town (same id, place,
  garrison) for one of the chosen type, then the owner is set when the scenario is built (last stage).
- **Neutral towns**: `NeutralTowns` in a zone, of a type from its `TownTypes` or any playable town, with a garrison of two stacks that share `ZoneGuards.TownGarrisonValue(monsters)`
  (weak 1500, average 4000, strong 9000; a zone without monsters still gets the weak garrison).
- **Mines**: the zone's minimum of each resource; in a start zone within about 14 tiles of the town and unguarded, elsewhere anywhere in the zone. `TemplateZone.MineGuards`
  (`Auto` default, `Always`, `Never`): auto guards mines outside start zones by the zone's monster strength through the mine's own guard (ADR-034), none when the strength is none.
  Power: weak 800, average 2000, strong 4500, times 1.5 for gold, 1.25 for the rare resources, 1 for lumber and ore.
- **Piles**: `ExtraPiles` per zone, unguarded, drawn 3:2:2:1:1:1 (gold, lumber, ore, crystal, mercury, sulfur); amounts are the defaults of the resource.
- Everything stands on free ground with a free tile all around (towns fall back to no margin in a crowded zone), objects are at least 12 tiles apart for towns, 4 for mines, 3 for piles
  (entrance to entrance), and the zone's entrances must all be reachable from each other or a problem is listed. Guard counts follow `GuardPicker`; its tier ranges are now 6-60, 2-40, 1-25, 1-15, 1-10.
- `sim layout ... --terrain` also shows towns (`T`), mines (`m`), piles (`p`) and lists them; `--seeds N` includes this stage.
**Roads, treasure and the scenario, stages 5–6 and assembly (2026-10-17)** (user answers RMG-1 to RMG-5; `MapGenerator.Generate(template, size, seed)` runs every stage,
`MapGenerator.Build(..., players)` also assembles the scenario):
- **Passages are kept free**: tiles a passage opened are reserved, so no later object stands on them (a mine on an unguarded passage blocked the way before).
- **Roads** (`RoadPlacer`, stage 5, no random draws; RMG-4): every zone has a hub, its first town's entrance or the walkable tile nearest its middle. Roads join the other towns of a zone
  to the hub and every passage to the hubs of both zones, so the zones are joined; where the zone has a town, also its gates and, per resource in `TemplateRoads.Mines`, the mine of that
  resource nearest the hub. Cheapest way over walkable ground, 8 ways, existing road cheaper (roads merge), never through an object's body. Template format **v2** adds
  `roads` (`enabled`, `type` default dirt, `mines` default lumber and ore, `gates` default on); v1 files get the defaults, and saving writes the current version.
  Later objects keep off road tiles.
- **Treasure** (`TreasurePlacer`, stage 6; RMG-1, user accepted the proposal): every band gives `Count` items worth a value drawn in the band. Below 4000 a pile (2 in 3) or a chest;
  from 4000 an artifact (1 in 2: the best tier the value reaches, minor 4000, major 8000, relic 15000, a relic once per map), else a chest or a pile. The generator fixes the contents:
  pile amounts at 1 gold, 100 per lumber or ore, 200 per rare unit (placeholder until marketplace rates, TOWN-2); chests the value as gold, 500 less as experience; the artifact.
- **Treasure guards** (RMG-2 with ADR-065): an item worth more than 1000 in a zone with monsters gets a guard beside it in **Area** mode, 40–60 % of its value × the zone's strength
  (weak 0.75, average 1, strong 1.5), half in start zones. Connection and gate guards stay Classic.
- **No free-roaming monsters** (RMG-3): monsters only guard passages, gates, mines and treasure for now.
- **Scenario assembly** (`ScenarioAssembler`, RMG-5): players in slot order from `PlayerSetup(Slot, Name, IsHuman, TownType, Hero?, Team?)`. A taken slot's towns get the picked type and the
  player as owner; the hero the player **picked at setup** (a hero of that town; none picked: drawn from the town's heroes, stream `heroes`) stands at the first town's entrance with
  one stack of its preferred creature worth about 1000 (placeholder). A slot nobody takes keeps its towns in place, **neutral** (user rule). Refused: no players, a slot taken twice or
  without a town, a human on an AI-only slot, a hero of another town or picked twice, a town that is not playable. The hero choice on the new-game screen is part of the
  new-game screen item (Godot).
- `sim layout ... --terrain` also shows roads (`+`), chests (`c`), artifacts (`a`), Area guards (`A`); `--seeds N` runs every stage (duel at 72: 0 of 40 with problems; Free for all
  at 96: 1 of 40, a crowded zone without room for its neutral town).

**The scenario and native start zones (2026-10-17)** (user answers on RMG-5 and RMG-7):
- **Factions are an input of the generator** in the game: `MapGenerator.Build` resolves "Random" (`PlayerSetup.RandomTown`, stream `factions`) first, then generates with
  the players' towns (`startTowns`, slot → town type). A start zone is painted with a mix of its town's native terrains (`TerrainPainter.NativeTerrains`, intersected with the
  zone's terrain list when that has any of them); other zones follow the template only. Start towns are placed as the picked type at once. Generated in the editor without
  towns, start zones keep the template's terrain and may not match a town chosen later.
- **Start towns**: the default start plus the start zone's `startBuildings` (building id → level) and `startDwellings` (one tier per dwelling, each a line of that tier drawn from
  the seed), template format **v3**; the validator refuses unknown buildings or levels, dwellings among the buildings, more dwellings than the 7-of-9 rule allows, and start
  buildings outside start zones. Without a tier-1 entry, the player's pick (`PlayerSetup.Tier1Dwelling`, chosen on the game creation screen after the faction) is built; a
  computer player, or a player who picked none, gets a random tier-1 dwelling. Every start dwelling opens with a week of creatures, the fort's bonus included (ADR-066).
- **Neutral towns** keep the default start. **A slot nobody takes**: its town stays in place, neutral, with a garrison by the zone's strength (two stacks, as neutral towns).
- **Start hero army** (HoMM3-style, replaces the one-stack placeholder): 2–3 stacks of the town's tier-1 and tier-2 creatures, the preferred creature first when it is one of
  them, 1000–1500 power shared 60/40 or 50/30/20.
- **Problems**: `MapGenerator.GenerateBest` tries the seed, then up to 9 sub-seeds (`AttemptSeed`, deterministic), and keeps the first map without problems; if every attempt
  has some, the one with the fewest, whose problems the new-game screen shows (`GenerationResult`).
- **Rules**: standard (`ScenarioRules.Default`: last team standing, elimination with the 7-day grace period); complexity and fog come with the new-game screen's options. The map
  is named "template size×size #seed".
- **Generator version**: `MapGenerator.Version` (1), bumped when the same inputs would give another map. Game records (format **v3**) carry `GenerationInfo(Template, Size,
  Seed, Attempt, GeneratorVersion)`; `VersionWarning` tells when the seed would give another map with this build. A generated game is saved as an ordinary map
  (`MapConverter.ToDocument`) and replays from it.

**Teams in templates (2026-10-17)** (user answers on RMG-6): a start zone has an optional `team` (format **v4**); start zones with the same number are allies, none = free for all.
The template's teams are the default: `PlayerSetup.Team` changes a player's team on the new-game screen unless the template has `teamsLocked` (then another team is refused and the
game's teams are locked, as for maps, ADR-032). A player with no team gets one of its own, numbered past every team in use. No team fairness checks in the validator (only:
teams on start zones, from 0), and no team rule in the layout: the connections decide where allies are. New starter template **Two against two** (`2v2.ewtemplate.json`, teams
locked): allies share a camp behind their starts and a guarded path between them; the camps meet at a strong front and two contested flanks (128: 0 of 20 maps with problems,
96: 1 of 20, which the retries cover).

**AI games on generated maps and the CI batch (2026-10-17)**: `sim game --template <name|key|file> [--size] [--players]` generates a map per run with
`MapGenerator.Build` (map seed = game seed, all computer players, every town "Random") and lets the adventure AI play it; `--record` keeps the generation info.
`sim maps` is the batch check, run by the CI job `generated-maps` (6 maps per shipped template, 14 days, about 30 s): sizes and player counts cycle (most
players first, so empty slots come up), and a map fails on generator problems after the retries, on a second generation that differs, when a player's hero
cannot walk to another player's start town (passable terrain around blocked cells, through guards and pickups, via gates; other entrances end a route),
on refused AI commands, or when the record does not replay through JSON to the same state. Shipped templates are found by name or file key (`BuiltInTemplates.Keys`:
`2v2`, `duel`, `ffa-4`). Fairness between start zones stays open (RMG-8). First runs: 18 of 18 maps pass, but no game ends within 40 days and heroes stay
at low levels: the AI hardly fights the generated guards (ADR-059 tuning).

**Generate from template in the editor, core (2026-10-17)**: `EditorMapGenerator.Generate(template, size, seed, players?, towns?, catalogs)` returns an
`EditorMap(Scenario, Generation, Issues)`; the editor opens the scenario in a new `MapEditorSession`, like "New" (a new map and undo history, not an undoable edit,
since size and everything else change). It reuses `MapGenerator.Build` with one `PlayerSetup` per slot (`EditorMapGenerator.Players`): the first N slots (default
all), named by colour, human on player-start slots up to `MaxHumans`, computer on AI-only ones, the picked town or "Random". So a generated map is the
game's scenario, playable as it is. Refused: a template with errors, a size the template does not list, more players than slots, a town for a slot without
a player. The map description records template, size, seed, attempt and generator version. `ScenarioAssembler.Build` and `MapGenerator.Build` now take the
building and artifact catalogs too (the editor passes its content). Found on the way: the map validator called a start hero in its town's entrance "inside
an object"; a hero may now stand in the entrance of a town of its own owner, in the validator and in `PlaceHero` (HoMM3 starts heroes visiting their
town). `sim maps` also fails a generated map on validator errors. Changing a town's faction in the editor (`SetTownType`) is a follow-up.

### ADR-065: Guard mode per monster stack (2026-10-17)
**Status**: accepted (2026-10-17), user decision on RMG-2; amends ADR-040
- **Context**: ADR-040 made the guard mode a map-wide editor setting: Classic (HoMM3 zone of control, pickups on guarded tiles can be stolen from outside) or Area (the whole 3×3 area is
  guarded). The user: Classic is the general-purpose mode, Area is mostly for guarding treasure so it cannot be picked up without a fight.
- The guard mode is a **per-stack** setting (`MonsterGuard.Mode`, default Classic); the map-wide setting is dropped (`GameState.GuardMode`, `SetGuardMode`, `GuardModeChanged` removed).
- Movement: a pickup on a guarded tile can be taken from outside only if every stack guarding it is Classic; otherwise the first Area stack (lowest id) fights. Any other entry into a
  guarded tile meets the first guard, as before. Pathfinding is unchanged.
- The AI counts only Area guards as opponents for pickups. Mine guards are unaffected (no mode; they defend only their mine).
- Map format **v13**: monsters get an optional `mode` (omitted = Classic); the top-level `guardMode` is no longer written, and maps before v13 apply it to every monster.
- Editor: `MapEditorSession.SetMonsterGuardMode` (undoable, publishes `MapObjectChanged`); the control moved from map properties to the monster rows of the inspector.
- Generator: passage and gate guards are Classic, treasure guards Area (ADR-064).
### ADR-066: What the fort does (2026-10-17)
**Status**: accepted (2026-10-17), user decision on TOWN-1; dwelling requirement and growth bonus implemented, fortifications wait for sieges (post-1.0)
- **Fort (level 1) unlocks the dwellings**: every `dwelling-*` level-1 row in `buildings.csv` requires `fort 1`. New towns start with fort 1 (unchanged), so this only matters for towns a
  map places without a fort; the editor refuses such dwellings (`TownRules.UnmetDwellingRequirement`), the validator flags them, and the AI builds the tavern and fort first in a fortless town.
- **Growth bonus (HoMM3 rule)**: a town's dwellings grow **+50 % with the Citadel** (fort 2) and **+100 % with the Castle** (fort 3). The bonus is rounded down (growth 1 → 1 with a
  Citadel, 2 with a Castle) and also applies to the week a new dwelling opens with. One rule, `TownRules.WeeklyGrowth(town, line)`, serves `TownService`, the town screen and the AI.
  Dwelling upgrades still do not raise growth (ADR-046).
- **Map dwellings are unaffected**: `CreatureDwelling` keeps the creature's growth, and caravans hire from it unchanged (ADR-054).
- **Fortifications are data only until sieges (post-1.0)**: Fort = town wall and gate, Citadel and Castle = better fortifications (proposed HoMM3 progression: a keep, then towers and
  stronger walls; decided with sieges). No siege code yet.

### ADR-067: Marketplace rates, Leadership, a defense initiative skill, the Primal Fury group (2026-10-18)
**Status**: accepted and implemented (2026-10-18), user notes in `ideas.md`; see "Implementation" at the end. Supersedes the "150 % in a governed town" part of ADR-063; settles the Stronghold half of HERO-2
and the third Offense and Defense skills of HERO-1 (numbers are placeholders for play-testing).
- **Marketplace trading** (TOWN-2), gold against one resource at a time; resources are not swapped directly. Resources are **basic** (lumber, ore) and **magic** (crystal, sulfur, mercury).
  - **X** = the player's towns **that have a marketplace**; a town with a **governor** counts **1.5**, any other 1. The factor is `min(X + 1, 10)`, so it stops growing at 9 towns.
  - **Buying 1 unit** costs `base / min(X + 1, 10)` gold, base **5000** (basic) or **10000** (magic): 2500 / 5000 gold with one marketplace, 500 / 1000 from 9. Rounded up (placeholder).
  - **Selling 1 unit** gives `floor(base * min(X + 1, 10))` gold, base **12.5** (basic) or **25** (magic): 25 / 50 gold with one marketplace, 125 / 250 from 9.
  - **No trade cap**: any amount, any number of trades a day (user decision on TOWN-2).
  - **Trading belongs to the player, not to a hero**: the player opens the trading screen from a UI button or by clicking the marketplace in a town; no hero needs to be present. It is a
    player command (`session.Execute`, ADR-036) that publishes a past-tense event. A player who owns no town with a marketplace cannot trade (user decision), except at a map marketplace (below).
  - **Intentionally harsh**: with a single marketplace trading is meant to be expensive and unfair (user decision); it improves as marketplace towns are added.
  - **Map marketplace** (adventure-map object): a hero that visits it opens the trading screen for its owner, at a **fixed rate as if X were 4** (factor 5): buy 1000 gold (basic) / 2000 (magic),
    sell 62 gold (basic, 62.5 rounded down) / 125 (magic). The rate ignores the player's towns and governors, so it is available also to a player with no marketplace. Same rules otherwise (no cap).
    It **cannot be flagged** (no owner; any hero may visit any number of times) and does **not affect** the trading rates in towns (user decisions). Trade only while the hero stands on it.
    Whether it is guarded is a map choice; in generated maps the zone's guard strength and the treasure value decide (below). Needs a new map object kind: map format **v14**, older maps keep
    loading, editor placement through `MapEditorSession`, and a validator check.
  - **Generator** (RMG): a map marketplace is a treasure item, placed only if the zone's treasure draw is valuable enough (user decision; placeholder: from the 4000 band, valued 4000), guarded by the
    usual treasure rule (items over 1000: 40–60 % of the value × zone strength).
  - **No AI trading for now** (user decision): the AI neither uses a town marketplace nor visits map marketplaces.
  - The governor's flat 150 % of ADR-063 is dropped: the governor's effect is now the 1.5 weight in X. `StewardshipRules.MarketplaceValuePercent` is unused and goes when trading is built.
  - Noted for play-testing: buying is 100 times dearer than selling with one marketplace (20 times with nine), by the owner's numbers, so trading is a last resort and a gold sink. The random-map
    treasure values (100 gold a lumber or ore unit) do **not** follow the new rates (user decision).
- **Third Offense skill: Leadership** (`offense.third`, Expert max): all own stacks get **+1 / +2 / +3 morale** (Basic / Advanced / Expert), within the −5…+5 limit (ADR-053).
- **Third Defense skill, working title Alertness** (`defense.third`, Expert max): all own stacks get **+5 / +10 / +15 % initiative** in battle (ADR-009). The name is a placeholder.
- **No luck skill for now**; no skill raises luck.
- **Stronghold's group is Primal Fury** (working title; the code keeps the id `rage` until it is built). Points **4 + 3 + 3**:
  - **Blood Rage** (4 points, Master): close to HoMM5's Stronghold. Draft: an own stack that kills at least one creature in an attack gets +1 Attack, up to +2 / +4 / +6 / +8 (Basic … Master)
    until the battle ends.
  - **Warcry** (3 points, Expert), instead of the spells other towns have: the hero uses a warcry in battle, no mana: Basic Rally (own stacks +1 morale for 3 rounds), Advanced Intimidate (enemy
    stacks −1 morale and −20 % initiative for 2 rounds, the former `shamanism.war-cry`), Expert Terror (enemy stacks lose their next action with a 30 % chance each, once). One warcry a round,
    each once per battle; whether it uses the hero's action is MAG-4. Draft numbers.
  - **Spell resistance of the faction** (3 points, Expert), not the Defense skill of that name: the enemy hero's spells in a battle against this hero have **−2 / −4 / −6 Spell Power**
    (minimum 0).
  - The Shamanism spell drafts in [spells.md](../content/spells.md) feed the Warcry skill; the group's roster mages start with its first skill (Blood Rage) as before.
- **Adrenaline (Swamp Bestiary) stays a placeholder group** (HERO-2).
- **Implementation (2026-10-18)**:
  - Trading: `MarketplaceRules` (half-town maths: buy `ceil(2·base / min(halves + 2, 20))`, sell `floor(quarters · factorHalves / 8)`), `TradeResourceCommand` (`Resource`, `Direction`, `Amount`, optional `MapMarketplace`),
    `EconomyService.Trade`, events `ResourcesChanged` + `ResourcesTraded`. The map marketplace needs one of the player's heroes on its entrance at the moment of the trade; the visit itself is the ordinary `MapObjectVisited`.
    `StewardshipRules.MarketplaceValuePercent` is removed. UI: `MarketplacePanel`, a "Marketplace" HUD button (disabled without a marketplace town), a button in the town screen of a town with a marketplace, and it opens by itself when a hero
    visits a map marketplace (the Godot parts build but have not been run in the editor yet).
  - Map object: `MapObjectKind.Marketplace`, class `Marketplace`, definition id `marketplace` (3×2 footprint like a mine), map format **v14** (`marketplace` objects; v13 and older load unchanged), editor placement works through the generic `PlaceObject`.
  - Generator: `TreasurePlacer` may turn a chest or pile of an item worth 4000+ into a map marketplace (1 in 4, at most one per zone), guarded as a 4000-value item; `MapGenerator.Version` is 2 (seeds give other maps than version 1).
  - Skills: `offense.third` is **Leadership** (`SkillEffects.LeadershipMorale`, in `Hero.TotalMorale`); `defense.third` is **Alertness** (`SkillEffects.AlertnessInitiativePercent`, `HeroBattleBonuses.InitiativePercent`, applied in `BattleState.InitiativeOf`
    before morale's Haste or Slow, rounded to the nearest point so +5 % helps from an initiative of 10). The ids keep `third`.
  - Primal Fury: group `rage` named "Primal Fury"; `rage.first` Blood Rage, `rage.second` Warcry, `rage.third` faction spell resistance. Blood Rage: `BattleUnit.RageAttack` (a stack that kills in any strike, retaliation included, gains +1 Attack up to the cap), added in
    `DamageCalculator`. Warcries: `BattleWarcryCommand` (a battle command, accepted while one of the player's units is up, it does not use the unit's action), `BattleState.WarcryRefusal`, at most one a round and each once a battle;
    Rally and Intimidate are timed `BattleSideEffect`s counted from the round after the one they were used in (the morale and the order of a round are set when it starts), Terror rolls 30 % per enemy creature stack at once and sets `LosesNextAction`.
    The battle AI uses the strongest available warcry each round. The spell resistance is data (`HeroBattleBonuses.EnemySpellPowerPenalty`) until casting exists (Phase 5).
  - Not built: Adrenaline, a luck skill, AI trading.

### ADR-068: The adventure-map object catalogue (2026-10-19)
**Status**: accepted as a design (2026-10-19), user notes in `ideas.md`; **nothing implemented**. Numbers are drafts; unclear points are MAP-1 … MAP-13 in `open-questions.md`.
- **Content**: about 60 objects in seven groups (resource providers, one-time loot, creature dwellings with guards, creature banks, permanent power-ups, temporary buffs, utilities), written out in
  [adventure-map.md](../design/adventure-map.md#planned-objects-adr-068). The mines, resource piles, treasure chest, artifact pickup, map dwelling and marketplace already exist; the rest are new.
- **Artifact tiers** (user decision, MAP-1): Minor, Major, Relic as in `artifacts.csv`; the notes' Treasure tier is dropped (every (t) became Minor). A Combo (combination) artifact is never dropped, only assembled; its pieces drop. Relics come from a pool; an empty pool is refilled, so duplicates are fine.
- **Visit scopes** (user decision): weekly (per object, resets on Monday), once per hero (visited-by set on the object), first visitor, vanishing. **Temporary effects** are a hero status that expires
  after the next battle, at the end of the day or of the week.
- **Creature banks** (user decision): a new object kind, guards always fight (no join or flee), reward only on victory, the guard tier is rolled from the game RNG at map start or fixed by the map author,
  then the bank is plundered.
- **Creature names**: the notes used HoMM3 names; they are mapped to our roster (Zombie → Ghoul, Power Lich → Arch Lich, Dwarf / Battle Dwarf → Defender / Shield Guard, Medusa Queen → Putrid Medusa,
  Cavalier → Lancer). Dwarf is a placeholder until CONT-2.
- **Treasure chest** gets the five-row table of the notes (including a 5 % Minor artifact) in place of the three equal tiers of ADR-039 (ADR-039 is superseded on this point). **Dwellings of tier 3 / 4 / 5** are guarded by 9 / 6 / 3 base
  creatures, which settles the dwelling-guard part of TOWN-5.
- **Marketplace rounding** (TOWN-2): what the player gives is rounded up, what the player receives is rounded down. The implemented rule (buy price up, sale gain down) already does this.
- **Implementation order** is the roadmap block "Adventure map objects": shared visit rules and hero statuses first, then the groups in the order resource providers, loot, buffs, trainers, banks, utilities.
  Map format bumps follow CLAUDE.md (new version, older versions keep loading).
- **Answers on 2026-10-19** (MAP-1 … MAP-4): abandoned mines are separate objects that turn into the normal mine once the guard is defeated (the guard count is rolled at map start; a normal mine keeps its free
  guard choice); Mystical Garden is a random reward, 500 gold or 5 of one random magic resource, no choice; Campfire gives gold plus 4–6 of one random resource; Supply Crates give two different resources, 4–6
  each; Lean To one kind; Resource Pile and Traveler's Wagon amounts already match.
- **Answers on 2026-10-19, second round** (MAP-5 … MAP-8): Prison levels 1 / 5 / 10 / 20 / 30 at 30 / 30 / 20 / 15 / 5 %, faction drawn at map generation, never a player's chosen start hero, the visit fails at the hero limit,
  a higher level gets experience and game-chosen level-ups. Banks: "N each", default battle width, hidden and fixed-by-RNG guard tier (an author cannot set it), a retreat or defeat leaves the guard intact, only the
  Churchyard costs −1 morale (next battle) on later visits, banks refill 4 weeks after plundering (details MAP-14). Gazebo and Library once per hero; the Library does nothing below level 10.
- **Hero limit** (user decision): the template editor and the map generator get a setting for the **maximum number of heroes per player, 1 to 8** (today a fixed 8, ADR-060). It becomes a map rule (map format bump)
  that hiring, the tavern and the Prison respect.
- **Implementation (2026-10-19), steps 1 and 2 of the roadmap block**:
  - Visit scopes: `MapObjectDefinition.VisitScope` (`Unlimited` for every existing kind, also in definition JSON), `MapObject.Visits` (`VisitLog`: the
    absolute week of the last collection and the heroes that collected), `MapObjectInteractionService.TryCollect` (publishes `MapObjectCollected`).
    Weekly means once per object per calendar week (`GameDate.AbsoluteWeek`). The log is not in the map format: games replay their commands.
  - Hero statuses: `HeroStatus(Source, Kind, Value, Expires)`, kinds Morale, Luck, Movement (flat daily points), expiry NextBattle (ended by
    `BattleService` for both heroes, any outcome), EndOfDay (the owner's next turn start), EndOfWeek (`WeekStarted`). Granted and expired by
    `ProgressionService`, events `HeroStatusGained` and `HeroStatusesExpired`. Same source and kind replaces; other sources add (MAP-10 open).
    An immediate movement bonus ("+400 until the end of the day") will be a direct grant with the objects, not a status.
  - Rolls: `WeightedRoll.Pick` (weights need not add up to 100) and `ArtifactService.Draw(tier)`: Minor and Major may repeat; a Relic is never one
    drawn since the last refill nor one on the map or carried, and an empty pool refills (`GameState.DrawnRelics`).
  - Digest: statuses, visit logs and drawn relics are written only when present, so older replays keep their digests.
  - Hero limit: `ScenarioRules.MaxHeroes` (1–8, `GameState.SetRules` refuses others), `GameState.HeroLimitReached` used by the tavern; map format
    **v15** (`rules.maxHeroes`, omitted = 8); the editor's conditions panel has the spin box (undoable through `SetScenarioRules`); `MapValidator`
    warns when a player starts with more heroes than the limit (assumed: allowed, it only stops hiring); template format **v5** (`maxHeroes`,
    validated 1–8); `ScenarioAssembler`, `MapGenerator.Build` and `EditorMapGenerator.Generate` take an optional `maxHeroes` override. Assumed until
    MAP-14: one limit for every player.
- **Implementation (2026-10-19), step 3: resource providers**:
  - One object kind `ResourceProvider` (2×2, entrance bottom right), every definition `Weekly`; the definition's `Provider` rule (WaterWheel,
    Warehouse, Windmill, MysticalGarden) plus `Resource` and `Amount` for warehouses say what it gives (`ResourceProviderRules.Roll`). Built-in ids
    `provider.water-wheel`, `provider.windmill`, `provider.mystical-garden`, `warehouse.<resource>` (lumber and ore 8, magic 5, gold 2000).
  - Water Wheel: 500 gold in the game's first week (`AbsoluteWeek` 1), 1000 later. Windmill: 3–6 of one of the five non-gold resources, evenly.
    Mystical Garden: even odds of 500 gold or 5 of one magic resource, no choice.
  - Warehouses of a resource other than gold offer it or 500 gold as a `ResourceChoiceDecision`, answered with `ChooseResourceRewardCommand`
    (replay name `resourceReward`); the week is taken when the visitor chooses. That the visitor chooses is the design doc's reading of the notes.
  - Events `ResourcesFound` (paid out) and `MapObjectFoundEmpty` (already taken). Map format **v16** (`provider` objects, no per-instance data;
    the weekly log is game state). Editor placement through the generic `PlaceObject`; the Godot views label them; the decision panel shows the
    warehouse choice (not run in Godot yet).
  - AI: visits a provider it may collect from, valued by its resource values (Windmill and Garden at their average), and takes the more valuable
    warehouse option. Not yet: the random map generator does not place providers.
- **Answers on 2026-10-19, third round** (MAP-9, MAP-10): the Tree of Knowledge has no free option, the player pays 2500 gold or 5 crystal + 5 mercury + 5 sulfur,
  once per hero per tree; buffs of several objects stack within the ±5 cap, the Temple's +3 is on day 7 of the week, a buff is the visiting hero's only and
  ends after any battle (already so: every battle ends in `BattleService.Finish`).
- **Implementation (2026-10-19), step 4 and the tomb: one-time loot**:
  - One object kind `Loot` (`LootSite`), every definition `FirstVisitor`; the definition's `Loot` rule says what it gives (`LootRules.Roll` → resources and an
    artifact tier, drawn with `ArtifactService.Draw`). Ids `loot.campfire`, `loot.supply-crates`, `loot.travelers-wagon` (single tile, vanish, and count as
    pickups that Classic guards let be stolen), `loot.corpse` (single tile), `loot.lean-to` (2×1), `loot.warriors-tomb` (2×2), which stay.
  - Campfire gold is 400, 500 or 600 (in hundreds, a reading of "400–600"). Supply Crates: two different resources. A later visitor of a site that stays gets
    `MapObjectFoundEmpty`. The Warrior's Tomb gives every visitor, the first included, a −3 morale `NextBattle` status (source `loot.warriors-tomb`, so a second
    visit renews it rather than adding).
  - Treasure chest: `TreasureChest.Odds` (30 / 30 / 25 / 10 / 5); the artifact row takes a Minor artifact at once and empties the chest, without a decision.
    Chests with fixed values (author, generator) never hold an artifact.
  - An artifact that does not fit in a full backpack is lost (`ArtifactBackpackFull`); `ArtifactSource.Found` marks artifacts from chests and loot.
  - Event `LootFound(Hero, Object, Resources, Artifact)`. Map format **v17** (`loot` objects). Editor placement is generic; the AI values loot sites (a spent
    one is no target). Not yet: the generator does not place loot sites; resource piles keep fixed default amounts.
- **Answers on 2026-10-19, fourth round** (MAP-11 … MAP-14): Hill Fort at 150 % of the upgrade price, stack by stack, tiers 1–3 only; Town Gate to the owner's
  towns only, picked from a list where an occupied entrance shows as unavailable, no route planning; War Machine Factory sells the visitor's own offensive
  machine, the cart and the tent at the blacksmith's prices, never one the hero has; a bank's refill after 4 weeks re-rolls its tier and brings the whole
  reward back, for every bank. Hero limit: one number for all players from the generator; the editor may set a player's own limit (campaign-like maps);
  the editor refuses a hero or a limit that would break it; otherwise the surplus is removed. This supersedes the "allowed with a warning" assumption.
- **Implementation (2026-10-19), hero limit follow-ups**: `Player.MaxHeroes` (null = the map's) through `GameState.SetHeroLimit`, `GameState.HeroLimit(player)`;
  map format **v18** (`maxHeroes` on a player slot); `MapEditorSession.SetPlayerHeroLimit`, `PlaceHero` and `SetScenarioRules` refuse with the new
  `EditResult.HeroLimit` (`SetScenarioRules` now returns an `EditResult`); the players panel has the spin box (0 = the map's). `GameFlowService.RemoveSurplusHeroes`
  runs first in `GameSession.Start` and removes the newest heroes beyond a limit (`HeroRemoved`); the validator's warning says so.
- **Implementation (2026-10-19), steps 5 and 6: buffs and power-ups**:
  - New visit scopes `DailyPerHero` and `WeeklyPerHero` (the log keeps each hero's last day). Buff sites (kind `Buff`, `BuffSite`, `BuffRules`): Faerie Ring,
    Fountain of Youth, Temple, Mineral Spring and Magic Well once a day per hero, the Stables once a week per hero. Statuses as decided; movement now is a
    direct grant (`HeroMovementGained`); the Stables also add a +400 daily-movement status until the week ends and upgrade `haven.lancer` stacks to their
    upgrade on every visit; the Magic Well fills the mana (`HeroManaRestored`).
  - Training sites (kind `Training`, `TrainingSite`, `TrainingRules`), once per hero: Learning Stone +1000 XP; Library +2 to all four stats from level 10
    (below: `MapObjectRefused`); Arena and Colosseum ask through a `TrainingDecision` that must be answered; Gazebo (1500 gold → 2500 XP) and Tree of
    Knowledge (2500 gold or 5 crystal + 5 mercury + 5 sulfur → the next level's experience) offer only what the player can pay and may be declined
    (`ChooseTrainingCommand` with no option, replay name `training`); nothing payable → `MapObjectRefused`. A visit that trains nothing does not count.
    Stats are raised through `ProgressionService.RaiseStat` (`HeroPrimaryStatRaised`).
  - Map format **v18** (`buff` and `training` objects), definition JSON fields `buff` and `training`. All definitions are 2×2 with the entrance bottom right.
    Decision panel: training buttons and "Leave". AI: values buffs low (the Magic Well not at all until there are spells) and training by use (the Library
    from level 10, paid ones with 3000 gold to spare), picks the first stat, pays gold when rich, else magic resources, else leaves.
- **Implementation (2026-10-19), abandoned mines and dwelling guards**:
  - Abandoned mines are `Mine` objects of their own definitions `mine.abandoned.<resource>` (same footprint and yield), whose new definition fields say
    what they become (`Becomes`), who guards them (`GuardCreature` `dungeon.troglodyte`, `GuardMin`–`GuardMax`) and where they are fought (`BattleTerrain`
    Subterranean for gold and ore). `GuardService.RollUnsetCounts` gives an unguarded abandoned mine its Troglodytes when the game starts (a map's own guard
    is kept). Beating the guard replaces the object with the normal mine (same id, position, owner; `GameState.ObjectCatalog`, new and passed by the map
    converter) and flags it; `MapObjectChanged` follows, and `VisibilityService` now refreshes what players remember on it. No map format change.
  - The notes' "all native terrain bonuses": battles have no terrain bonuses yet, so only the battlefield's terrain changes (roadmap item).
  - Dwelling guards: `CreatureDwelling.Guard`, set at game start for an unowned dwelling of tier 3–5 (`DwellingGuards.Count`: 9 / 6 / 3 of its base creature).
    The guard always fights (`GuardService.Encounter`); a hero without an army cannot enter; beating it clears the guard and flags the dwelling. Game state only.
    The AI weighs the guard as the opponent at the dwelling.
- **Implementation (2026-10-19), utilities and the prison** (map format **v19**):
  - Sign (`SignPost.Message`, map data, `MapEditorSession.SetSignMessage`, inspector text field): a visit publishes `SignRead`.
  - Observatory: every visit publishes `SurroundingsRevealed`; `VisibilityService` reveals radius 20 around its entrance (explored terrain, as any
    reveal) and records the hero (`OncePerHero` log, which the AI reads to go there once).
  - Hill Fort: `UpgradeAtHillFortCommand(Player, Fort, Slot)` for the player's hero on the entrance; `HillFortRules`: tier 1–3 creatures with an
    upgrade, 150 % of the price difference rounded up per resource (no Stewardship); `HillFortUpgraded` + `ArmyChanged`.
  - Town Gate: a `TownGateDecision` lists the owner's towns (`TownGateOption`, unavailable when a hero stands at the entrance);
    `ChooseTownGateCommand(Player, Town?)` moves the hero there (`HeroTeleported`, no movement cost; a hero with Stewardship takes a governor-less
    town as usual) or stays. A player without towns is turned away (`MapObjectRefused`).
  - War Machine Factory: `BuyWarMachineAtFactoryCommand(Player, Factory, Slot)`, the blacksmith's prices (`WarMachines.Cost`) and the hero's own
    offensive machine; refused for a machine the hero has. `WarMachineBought`'s first field is now `Seller` (a town or a factory).
  - Prison (`Prison.Hero`, `Prison.Level`, map data; `SetPrison` and inspector rows): `PrisonRules.Prepare` fills what the map left open when the
    game starts (a random playable faction, then one of its heroes neither on the map nor in another prison, else any; the level by 30 / 30 / 20 /
    15 / 5 %). A generated map would do the same at generation once the generator places prisons. Freeing: refused with a notice at the hero limit;
    otherwise the prison goes and the hero (roster stats, no army) stands where the prison stood, with the level's experience and the game's level-up
    choices (`ProgressionService.LevelUpAutomatically`), moving from its owner's next turn (`HeroFreed`). The validator checks the prisoner, the
    tile the hero will stand on, and warns when the prisoner is already on the map.
  - UI: `ObjectServicePanel` (Hill Fort upgrades, factory purchases) opens on a visit; the decision panel lists the town gate's towns; a
    `NoticePanel` shows signs, refusals, emptied objects and freed prisoners. Replay names `townGate`, `hillFort`, `factory`.
  - AI: frees prisoners and visits an observatory once per hero; answers a town gate by staying; does not use hill forts or factories yet.
  - Assumed (not in the notes): the freed hero has no army (only its roster stats and skills), and the Prison's footprint is two tiles so the hero
    has a place to stand.
- **Implementation (2026-10-19), creature banks** (map format **v20**, `bank` objects without instance data):
  - Kind `CreatureBank`, definitions `bank.<name>` (3×2), `BankRule` and `CreatureBankRules.Tables` (the notes' tables with the roster's creatures:
    Ghoul, Vampire Lord, Skeleton / Wight / Wraith / Skeleton Warrior / Arch Lich, Defender / Shield Guard (placeholder, CONT-2), Imp / Familiar,
    Medusa / Putrid Medusa, Naga / Naga Queen). `GuardService.RollUnsetCounts` rolls each bank's tier when the game starts (30 / 30 / 30 / 10, the
    Churchyard 100), and with an upgrade a coin flip turns a fifth (rounded down) of the guard into the upgraded creature.
  - A visit to a full bank starts `BattleService.BeginBankBattle`: the guard always fights, at the default width (no Tactics choice); each kind of
    creature is split into a few battle stacks, at most seven in all. Winning plunders it (`Plunder`: resources to the winner, the artifact drawn by
    tier and lost to a full backpack, `CreatureBankPlundered`); losing or fleeing leaves the guard whole. `BattleService` now takes the `EconomyService`.
  - A plundered bank gives nothing (`MapObjectFoundEmpty`); the Churchyard's costs −1 morale for the next battle. `GuardService.RefillBanks` runs on
    every new day and refills a bank 28 days after it was plundered, with a new roll (`CreatureBankRefilled`, `MapObjectChanged`).
  - The Ruins publish `SurroundingsRevealed` (radius 12) on every visit; `ObservatoryUsed` was renamed to `SurroundingsRevealed` for both.
  - AI: a full bank is a target worth its reward (+1500 for an artifact) against the guard as the opponent, so the simulated fight decides.

- **Implementation (2026-10-19), random maps** (`MapGenerator.Version` **3**: the same seed gives another map than version 2):
  - `TreasurePlacer` turns one item in 3 (`SiteOdds`) into an object of the catalogue: a `TreasureSite` from `TreasureSites.All` worth between half
    the drawn value and the value, drawn by weight within its zone limit (1, loot 2) and map limit (the Town Gate once). When nothing fits or there is no
    room, the item is a pile, chest, artifact or marketplace as before. No template field: every template gets them, and the bands decide.
  - Values are placeholders (gold equivalents of the rewards; weekly providers count about two weeks). A site is guarded like other treasure by its own
    value; creature banks and abandoned mines get no guard beside them, since they fight for themselves. Banks, abandoned mines and prisons are rolled
    when the game starts, as on hand-made maps.
  - Not placed: signs (no text to give) and map dwellings (zone rules later).
- **Implementation (2026-10-19), follow-ups** (map format **v21**):
  - **Native terrain in battle** (decided: the HoMM3 rule): a creature of a town that the battlefield's terrain lists as native
    (`TerrainDefinition.NativeTowns`) gets +1 Attack, +1 Defense and +1 Speed (`BattleState.IsNative`, `NativeAttack` / `NativeDefense` /
    `NativeSpeed`; `DamageCalculator` and `SpeedOf`). Each stack counts on its own; neutrals and war machines never. An abandoned gold or ore mine
    fights underground, so its Troglodytes (Dungeon) are native there: the notes' "all native terrain bonuses".
  - **Dwarf** (decided, closes the banks' part of CONT-2): a new neutral tier-2 line, `neutral.dwarf` (A7 D8, 3–5, 28 HP, speed 8, SpellResistance 2)
    → `neutral.battle-dwarf` (A8 D10, 3–6, 30 HP, speed 9, SpellResistance 3, the most the format allows); draft numbers. The Dwarven Treasury uses
    them instead of the Defender / Shield Guard.
  - **Resource piles**: `ResourcePile.Amount` is optional; an open amount is rolled when the game starts (`GuardService.RollUnsetCounts`):
    5–10 lumber or ore, 3–7 magic, 500–1000 gold in hundreds. Editor placements and the generator's extra piles leave it open; the generator's
    treasure piles stay fixed by their value; a map file leaves `amount` out (v21), and every older file has one, so old maps keep theirs.
  - **AI**: a hill fort is a target worth the town price of the upgrades the hero can pay for with a third of the gold kept; a factory 300 per
    machine the hero lacks and would buy in a town (its offensive machine with Advanced Ballistics, a tent, a cart for shooters) with 1500 gold kept;
    a town gate is worth 90 % of the most needed own town (defence or troops waiting, the same need that sends heroes to towns) with a free
    entrance, and the gate's picker takes the hero there. Once on the entrance, the hero upgrades or buys.

### ADR-069: Skill rework, Necromancy, mana by level, one faction group, a second object catalogue (2026-10-20)
**Status**: accepted as a design (2026-10-20), user notes in `ideas.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of ADR-048 (skill
numbers and groups), ADR-063 (attunement table, the Adrenaline group), ADR-067 (Leadership and Alertness ids and numbers, the faction spell resistance) and
ADR-038 (the ranged half-damage rule, if CMB-6 says so). Open points: HERO-4, HERO-6, HERO-7, CMB-6, MAG-7, MAG-8, TOWN-6, MAP-15 … MAP-24 in `open-questions.md`.
- **Skill groups** (every group keeps three skills and 4 + 3 + 3 points; the Master skill is the 4-point one):

  | Group | Skills (max level: Basic / Advanced / Expert / Master) |
  |---|---|
  | Offense | Melee (Master: +9 / 16 / 23 / 30 % melee damage), Archery (Expert: +15 / 25 / 35 % ranged damage), **Marksmanship** (new, Expert: ranged penalty −1/4 / −1/2 / −3/4) |
  | Defense | Armor (unchanged), Spell resistance (Expert: −10 / 15 / 20 % to hostile spells' damage or duration, Phase 5), **Leadership** (moved here from Offense, +1 / 2 / 3 morale) |
  | Pathfinding | **Pathfinding** (now Master: −25 / 50 / 75 / 100 % of the non-native penalty), **Logistics** (now Expert: +10 / 20 / 30 % movement), **Scouting** (new, Expert: +2 / 4 / 6 sight radius); Troop management is dropped |
  | Tactics | **War Tempo** (Drill renamed, Master: +1 / 2 / 3 / 4 battlefield speed), **Tactics** (Expert: Basic rearranges troops before battle; Advanced also chooses width 30 or 32; Expert 28–34; the deployment zone is 4 / 5 / 6 cells deep), **Alertness** (moved here from Defense, working title, Expert: **+1 / 2 / 3 initiative**, flat instead of percent) |

  - The Deployment skill is merged into Tactics. Tactics Basic no longer chooses a width (it was 30 / 32); Advanced loses 28.
  - **Distance penalty for ranged attacks**: every 10 cells of distance add a **1/8 ranged penalty**. How it meets cover and the 15-cell half-damage rule
    (ADR-038), and what Marksmanship reduces, is CMB-6.
  - Skill ids: `offense.marksmanship`, `defense.leadership`, `pathfinding.scouting`, `tactics.wartempo`, `tactics.alert`; the rest keep theirs. How older maps
    and saved games with `offense.third`, `defense.third`, `tactics.drill`, `tactics.deployment`, `pathfinding.troops` and `adrenaline.*` load is HERO-6.
- **Attunement** (replaces the ADR-063 table; several add up):

  | Schools | Max mana | Mana a day | Spell cost |
  |---|---|---|---|
  | Arcana, Chaos | +10 / 20 / 30 / 40 | +1 / 2 / 3 / 4 | – |
  | Light | – | +2 / 4 / 6 / 8 | −1 / 2 / 3 / 4 |
  | Death | – | +2 / 4 / 6 / 8 | – (Necromancy instead) |
  | Nature, Shadow | +5 / 10 / 15 / 20 | +2 / 4 / 6 / 8 | – |

- **Mana by level**: the pool also grows by **10 × ⌈level / 10⌉** (+10 at levels 1–10, +20 at 11–20, …).
- **Necromancy** (Death attunement): after a **victory**, 5 / 10 / 15 / 20 % (Basic … Master) of the **value of the enemy creatures slain** comes back as undead
  in the hero's army. The attunement level caps the undead tier and maps enemy tiers to it:

  | Level | Undead raised | Enemy tier → undead tier |
  |---|---|---|
  | Basic | tier 1 | all → 1 (tier-1 lines in random proportion) |
  | Advanced | tiers 1–2 | 1–3 → 1, 4–5 → 2 |
  | Expert | tiers 1–3 | 1–2 → 1, 3–4 → 2, 5 → 3 |
  | Master | tiers 1–4 | 1–2 → 1, 3 → 2, 4 → 3, 5 → 4 |

  - **Necromancy Amplifier**, a Necropolis building: +5 % Necromancy for **every hero of its owner** that has the skill (10 / 15 / 20 / 25 %); a second amplifier
    adds nothing. It would be the first town-specific building (TOWN-3). Details: MAG-7, TOWN-6.
- **Faction group**: Primal Fury is the group of **both Stronghold and Swamp Bestiary**; **Adrenaline is dropped** (answers most of HERO-2). Its skills:
  Blood Rage (unchanged), Warcry (working names "Warcry / Spiritual Totems", unchanged), and **Interference** (replaces the faction spell resistance, `rage.third`):
  the enemy hero's spells have −2 / −4 / −6 Spell Power and cost +1 / +2 / +2 mana (Phase 5). The group is for the towns' **mage** heroes; their **warrior**
  heroes start with the **Offense** group (HERO-7).
- **Second object catalogue** (the owner's list, written out in [adventure-map.md](../design/adventure-map.md#second-object-catalogue-adr-069)):
  - Loot: Mage's Chest (minor artifacts).
  - Utilities: Window and Eye of the Magi, Tavern, Portal, One-Way Portal (both exist as monoliths, ADR-026), Garrison, Keymaster's Tent with border guards
    and gates (RMG-11).
  - Deposits that become mines after paying: Wood Pile → Sawmill, Ore Deposit → Ore Pit, Gold Vein → Gold Mine, Crystal Vein → Crystal Mine, Cinnabar Deposit
    → Alchemist's Lab.
  - Creature banks: Ruins with orcs (the name is taken, MAP-22), Lair (evil eyes), Keep (crossbowmen and pikemen), Hideout (bandits), Forest Glen (wolves),
    Dragon City (Black Dragons).
  - Magic: Mana Vortex (doubles the maximum mana), Fountain of Magic (restores 50 %), Shrines of levels 1–3 (teach a spell; Phase 5).
  - Buffs: Clover Field (+1 luck), Rainbow (+2 luck), Rally Flag (+400 movement today, +1 luck and morale) for the next battle.
  - Power-ups: Dream Teacher (500 XP, +500 for each other Dream Teacher the hero visited; once per hero), Training Grounds (+1 Attack), Marletto Tower
    (+1 Defense), Star Axis (+1 Spell Power), Garden of Revelation (+1 Knowledge), once per hero.

### ADR-070: Answers to the ADR-069 questions, teams, creature sizes, mirrored templates, template editor (2026-10-22)
**Status**: accepted as a design (2026-10-22), user answers in `ideas.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of ADR-038 (ranged
half damage), ADR-060 (tavern pools and price), ADR-026 (portal naming), ADR-068 (the undead Ruins bank's name and every bank's guards). Points marked
*(assumed)* are the agent's reading of a short answer; they are listed again in `open-questions.md` to be confirmed.

- **Teams**: a game has 2 to 8 teams (8 = free for all). Templates and maps set team defaults and restrictions (fixed teams, allowed team counts); the map
  editor may override what the template set, e.g. on a generated map.
- **Creature sizes**: about 95 % of creatures are Small (2×2) or Medium (3×3); Large (4×4) is kept mostly for dragons. Today 42 of 182 lines are Large;
  the roster pass moves the others down and re-checks `sim towns`.
- **RMG-9 mirrored templates**: only for 2, 4 or 8 players (8 uses the underground). Two players: axis symmetry, the axis vertical, horizontal or
  diagonal; four or eight players: point symmetry around the map centre. The template picks the symmetry *(assumed; "random axis" allowed)*. Mirrored
  zones share their rolls.
- **RMG-10 template editor**: no undo needed (unlike `MapEditorSession`); works like the HotA template editor (zone graph, zone and connection
  properties); "Generate" in the editor shows the result. User templates live in the user data folder *(assumed)*.
- **RMG-11 (part)**: a template may enable portals (monoliths) so the generator links zones with them; a connection's guard is a monster guard or a
  **garrison**, per template.
- **MAP-15 Mage's Chest**: 5 % nothing, 5 % a Major artifact, 60 % a Minor artifact, 30 % a Minor artifact and 500–1000 gold; vanishes.
- **MAP-16 Window and Eye of the Magi**: four colours (red, green, blue, black); a Window reveals radius 6 around each Eye of its colour, up to 4 Eyes a
  colour; placed only in the map editor, never by the generator. Every visit *(assumed, like HoMM3)*.
- **MAP-17 / TOWN-4 taverns**:
  - Town taverns: the per-player pool becomes **per player and faction**: a town offers its native and adjacent heroes (2 native + 2 adjacent with the
    Brotherhood), and the player's towns of one faction share that pool.
  - Brotherhood costs **1000 gold + 5 ore** (was 2000 gold + 5 lumber + 5 ore).
  - Hired heroes get a **starter stack** that follows the hero's preferred creature.
  - The **map tavern** works like a Brotherhood (4 offers); the hero must stand on its entrance and the new hero appears on a free cell next to it (one cell
    below by default). The hero limit applies; with the limit reached or no free cell, hiring is refused. Its pool: the answers differ (the player's pool
    or its own random heroes), open as TOWN-4.
- **MAP-18 portals**: "portal" and "monolith" are one thing (ADR-026); a portal type has exactly two ends, no groups of three or more.
- **MAP-19 garrison**: flagged like a mine, captured by beating its guards; a neutral garrison's guards come from the map or the generator; own heroes
  pass both ways; heroes leave and take creatures at any time; the generator may use it as a connection guard (RMG-11).
- **MAP-21 deposits**: the proposed prices (Gold Vein 5000 gold + 10 ore, the others 2000–3000 gold + 5 lumber or ore), paid from the player's resources by
  any visiting hero; the mine is flagged for that player; guarded by a normal monster guard; a **Sulfur Vent** (→ sulfur mine) is added; the generator
  places deposits separately, like abandoned mines.
- **MAP-22 creature banks**: the agent drafts tables like the existing banks (four rows at 30 / 30 / 30 / 10 %, rewards scaled to the guard's value).
  **Every bank, old and new, is re-drafted with 2–3 creature types of one faction.** The undead Ruins is renamed **Crypt** (`bank.crypt`, old maps load
  `bank.ruins` as the Crypt); the orc bank takes the name Ruins. Dragon City is the strongest bank by far.
- **MAP-23 Mana Vortex**: sets mana to twice the maximum until spent (HoMM3), once a week per hero. The Fountain of Magic: once a day per hero *(assumed)*.
- **MAP-24 shrines**: the spell is set in the editor or rolled at game start; learning needs the school's Magic skill at the spell's level; any hero, any
  number of visits; a hero without the skill or with the spell already shows a message.
- **TOWN-2**: the map marketplace's threshold stays the placeholder 4000. Nothing about trading is open.
- **TOWN-3 buildings**:
  - Towns get **their own building lists and names**: `buildings.csv` gains a town column (like `creatures.csv`); designers set names, costs and
    requirements there. Every town later gets a unique building.
  - **Capitol** (town hall 4): one per player; capturing a town with a Capitol when the player already has one downgrades it to town hall 3 *(assumed)*.
  - The mage guild 4 placeholder stays.
- **TOWN-6 Necromancy Amplifier**: 1500 gold + 5 mercury, needs mage guild 1; a captured one helps its new owner; Necropolis-only (the first unique building).
- **CMB-1 surrender**: not against neutrals; price = half the gold cost of the surviving creatures; the hero returns to the tavern pool. The AI **retreats**
  when its army is almost beaten (threshold: tuning); AI surrender is not planned.
- **CMB-3 war machines**: heroes exchange machines; a town garrison holds machines only through a hero in it.
- **CMB-4 teleporting creatures**: move like flyers (footprint must fit at the destination).
- **CMB-6 distance penalty** (replaces ADR-038's half damage beyond 15 cells):
  - Cells between the footprints: 0–9 no penalty, 10–19 1/8, 20–29 2/8, and so on.
  - Distance and cover penalties add up to one ranged penalty; above 7/8 the target cannot be shot (ADR-035).
  - Marksmanship subtracts a fixed −2/8 / −4/8 / −6/8; "almost no penalty" at Expert is intended.
  - War machines ignore the distance penalty.
- **MAG-4 casting in battle (part)**: the hero casts at most once a round, during the turn of one of the units the hero controls (machines that fire on their
  own do not count); spells that change initiative do not change this. Spells may target war machines; machines are `mechanical` and resist many spells.

### ADR-071: Data tables, fairness, keymasters, growth buildings, morale exemptions, spell books and guilds (2026-10-22)
**Status**: accepted as a design (2026-10-22), user answers in `open-questions.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of
ADR-044 (where the tables live), ADR-069 (the Spell resistance numbers) and the spells.md levels of Town Portal and Dimension Door. Points marked
*(assumed)* are listed under "To confirm" in `open-questions.md`.

- **DATA-1 data tables**: the `*.csv` tables move to a core-owned folder, embedded in `EternalWars.Core.Domain`; the apps read the embedded copy, with
  `--creatures=`-style flags for designers' edited files; the `res://shared/data` copy goes. CSV stays for flat tables, JSON for nested data. A schema
  test per table checks ids, references and columns. **Done 2026-10-23**: the folder is `src/Core/data` (templates in `data/templates`);
  artifacts got `--artifacts=` and `--artifact-sets=`; `DataTableSchemaTests`.
- **RMG-8 fairness**: start zones are compared by distance to the nearest mines, treasure value and guard power on the way, within ±15 %; a map that
  fails is retried with the next sub-seed, then reported.
- **RMG-12**: "richness" is dropped; treasure bands and mine minimums are enough.
- **RMG-13 optional connections**: every connection has a presence probability, 100 % by default.
- **MAP-20 keymasters**: four colours (like the Eyes of the Magi); the key belongs to the player; a border guard vanishes after the first key holder passes,
  a border gate stays and lets key holders through; templates may use them in connections (an option per connection).
- **TOWN-1 growth buildings** (placeholder): two buildings per town, one adds +5 a week to tier-1 growth, the other +2 to tier-2, whether the town has
  one or two dwellings of that tier *(assumed: each dwelling of the tier gets the bonus)*.
- **TOWN-4 map tavern**: its own weekly pool of 4 random heroes of any faction, shared by all players.
- **CMB-7**: undead, constructs and elementals have no morale. **CMB-8**: war machines have no morale but roll the hero's luck (as built).
- **MAG-2 spell books and guilds**:
  - Mage heroes start with a spell book; warriors buy one in a mage guild for 500 gold.
  - A hero learns a spell when the school's first skill (its Magic skill) is at least the spell's level.
  - A guild offers 3 spells at levels 1 and 2 and 2 spells at levels 3 and 4, of the town's own school. Two more buildings add spells from other
    schools by the magic circle (HoMM4-like; details MAG-12).
  - Stronghold and Swamp Bestiary: a 3-level placeholder building teaches warcries / spiritual totems instead of a guild.
- **MAG-3 (part)**: mana refills in a town with a mage guild. Spell Power multiplies damage spells' damage and buffs' and debuffs' duration (numbers with
  the spell list, MAG-1). Attunement "potency" is skipped; the ADR-069 attunement table stays as it is (mana pool, regeneration, Light's cost
  reduction, Death's Necromancy).
- **MAG-4**: the unit whose turn the hero cast in still acts (HoMM3).
- **MAG-5 (part)**: Town Portal and Dimension Door are level-4 spells.
- **MAG-6 (part)**: Spell resistance is −8 / 14 / 20 % (was −10 / 15 / 20 %) to both damage and duration; durations round away from zero, at least 1 round.

### ADR-072: Answers on deployment, Necromancy, skill ids, rivals, taverns and spells (2026-10-22)
**Status**: accepted as a design (2026-10-22), user answers in `open-questions.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of
ADR-048 (rival schools: the arch-rival only), ADR-060 (the basic tavern's offers), ADR-069 (Interference at Expert, Necromancy by value) and ADR-070
(the Fountain of Magic, CMB-6's check order). Points marked *(assumed)* are listed under "To confirm" in `open-questions.md`.

- **HERO-1 skill specialties**: the skill's effect +5 % per hero level (HoMM3-like), for now.
- **HERO-3 rivals**: the ring decides both rules. A town's rival school is that of the town **across the ring** (Sylvan–Dungeon, Haven–Necropolis,
  Academy–Inferno), one per town instead of two sworn enemies; the tavern keeps using the ring.
- **HERO-4 deployment** (Tactics): the zone is the number of **columns from the hero's own edge** in which the player may place troops (4 / 5 / 6).
  Only the side with the higher Tactics deploys; a tie lets nobody. The player drags stacks in the battle scene, then "Start battle"; the AI and a
  side that does not deploy are placed automatically at the very edge. Against heroes and neutral monsters, **not in creature banks**.
- **HERO-6 skill ids**: maps and saves read the old ids as their successors (`offense.third` → `defense.leadership`, `defense.third` →
  `tactics.alert`, `tactics.drill` → `tactics.wartempo`, `tactics.deployment` → `tactics.tactics`, `pathfinding.troops` → `pathfinding.scouting`,
  `adrenaline.*` → `rage.*`), no map format bump; a level above a skill's new maximum drops to it. Replays recorded before the change may diverge.
  Troop management's roster warriors start with Scouting.
- **HERO-7 Primal Fury and warriors**: the towns' mages start with the group; their warriors are offered it with a high weight so they learn it
  quickly *(assumed: the warrior weight 6, as a mage's own school)*. Every **Stronghold** warrior starts with an **Offense** skill, every **Swamp
  Bestiary** warrior with a **Defense** skill *(assumed: one of its two starting skills is swapped where it has none)*.
- **TOWN-7 basic tavern**: 1 native + 1 adjacent hero, the adjacent one from either ring neighbour at random.
- **TOWN-8 hero limit**: prisoners belong to nobody and do not count; heroes in a town garrison do not count either, but cannot leave the garrison
  while the player is at the limit.
- **RMG-11 town groups in templates**: post-1.0.
- **MAP-23**: the Fountain of Magic works **once a week per hero** (not once a day).
- **CMB-6 order**: 1. add the distance and cover penalties, 2. subtract Marksmanship, 3. apply the 7/8 "no shot" limit. Marksmanship can therefore
  make a target shootable that cover would hide.
- **MAG-1 Spell Power** (for now): damage × (1 + SP / 10), duration × (1 + SP / 10). The spell list stays open.
- **MAG-3 spell ranks**: Mastery does not upgrade spells by itself; it allows a known spell to rise (rank 2 at Basic, 3 at Advanced). The spell rises,
  at no cost, when the hero visits a town whose mage guild (or spell building) offers it *(assumed: straight to the rank Mastery allows)*.
- **MAG-5 adventure spells**: casting costs a quarter of the hero's daily movement points (500 of 2000) besides mana *(assumed: of the day's full
  points; a hero with fewer left cannot cast)*; Dimension Door lands only on explored tiles.
- **MAG-6 spell specialties**: the spell's effect +3 % per hero level.
- **MAG-7 Necromancy**:
  - The value is **hit points**: the share of the slain enemies' hit points, summed per undead tier, divided by the undead creature's hit points,
    rounded down.
  - Any hero with Death attunement.
  - Only the enemy's losses count, enemy undead included; constructs and elementals do not (war machines are not creatures). The hero's own losses never count.
  - The base creature of the tier is raised and joins its stack or a free slot; a tier's two lines split in a random proportion at every level.
    Only when there is neither, and the army has the upgraded creature's stack, it is raised as the upgrade at **3/4** of the count. Otherwise lost.
  - After every victory, never after a retreat or a surrender; automatic; shown on its own screen after the battle result and before any level-up.
- **MAG-8**: mana by level adds to the maximum only; every hero has at least 10 (intended).
- **MAG-9 Interference**: +1 / +2 / +3 mana per enemy spell (Expert +3, not +2).
- **MAG-10 Primal Fury mages**: Spell Power strengthens Blood Rage and the warcries (numbers when built); Knowledge does nothing; their level-ups
  favour Attack and Defense *(assumed: the warrior odds)*.
- **MAG-11 area spells**: a fixed area whatever the target's size; a creature with any cell inside is hit once.
- **MAG-12 spell buildings**: one building for each of the two schools next to the town's own on the ring, each adding 1 spell per guild level the
  town has; placeholder cost 2000 gold + 5 of the school's resource, needs mage guild 2.

### ADR-073: Confirmations, war machine footprints, caravan reach, base-creature map dwellings, victory conditions, week events (2026-10-23)
**Status**: accepted as a design (2026-10-23), user answers in `open-questions.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of
ADR-056 (war machine sizes and the machine column), ADR-057 (conditions, team victory), ADR-072 (MAG-5's movement cost, MAG-10's stat odds). Points
marked *(assumed)* are listed under "To confirm" in `open-questions.md`.

- **Confirmed**: every "To confirm" assumption of ADR-070 … ADR-072 stands (RMG-9, RMG-10, MAP-16, MAP-18, MAP-20, TOWN-1, TOWN-3 ×2, TOWN-4,
  TOWN-6, TOWN-7, DATA-1, MAG-3, MAG-5's 25 mana, HERO-7 ×2, CMB-1), except the two below.
- **MAG-10 Primal Fury mages**: level-ups use **even odds, 25 / 25 / 25 / 25 %** at every level (not the warrior odds).
- **MAG-5 adventure spells**: the movement cost is a **fixed 500 points**, whatever the hero's daily total; a hero with fewer than 500 left cannot
  cast.
- **HERO-4 war machines** (HoMM3-like: the machine stands mostly outside the grid): every machine occupies **one column only**, the outermost one
  of its side. The offensive machine and the healing tent are **1 × 3** cells, the ammo cart **1 × 2** *(assumed: every offensive machine —
  ballista, cannon, catapult, glaivethrower — is 1 × 3)*. Their models may overhang the grid edge. Stacks start in the next column (today the
  machine column is as wide as the largest machine plus a gap). The deployment zone of 4 / 5 / 6 columns **includes the machine column**; stacks
  may use its free cells *(assumed)*. Battle units get rectangular footprints for this (creatures stay square).
- **Map dwellings sell base creatures only** for now: no map dwelling of an upgraded creature. The editor offers only base creatures and the
  validator rejects an upgraded one; a map that has one loads it as its line's base creature *(assumed)*. Town dwellings keep their upgrade
  (level 2 sells both, ADR-046).
- **TOWN-5 caravans**: the 5-day reach is checked twice. When a dwelling is **flagged** (and when a town gets a caravan), the raw distance — the
  travel days at the creature's speed ignoring hostile heroes, monsters and guards *(assumed)* — decides whether the dwelling appears in that
  town's caravan list at all. When **ordering**, the full check (open route, 5 days) runs as today; on the way only the route is re-checked. A
  caravan waits at a full garrison until there is room (as built). The town's caravan screen shows a **queue**: caravans on their way (creature,
  count, days left) and those waiting for room.
- **FLOW-1 victory conditions**:
  - New victory conditions: **capture a given map object** *(assumed: an ownable one — mine, dwelling, town)*, **obtain a given artifact** (a hero
    of the player carries it), **release a given hero** from its prison. Capture a town and defeat a hero exist.
  - New defeat conditions: **lose a given artifact** *(assumed: no hero of the player carries it any more)*, next to lose a town and lose a hero
    (which can be the starting ones).
  - **Time limits**: a victory condition may carry a deadline ("defeat the enemy within 3 months"); missing it defeats the player *(assumed)*.
    The plain "time limit" defeat condition stays.
  - **Teams**: a team wins while at least one of its **human** players survives; eliminated human teammates share the win. A computer ally's
    survival does not win for the human players *(assumed: they have lost)*.
  - Campaign hooks stay open (Phase 6).
- **FLOW-2 week and month events** (HoMM3-like; a month is 4 weeks), announced at the start of the week:
  - **Weekly** (every week but a month's first): **75 % week of a creature** — a random creature's weekly growth doubles; picking either the base
    or the upgraded creature doubles its line *(assumed: in towns and map dwellings, that week only)*. **25 %** nothing.
  - **Monthly** (first week of a month; replaces that week's weekly roll):
    - **45 % month of a creature**: a random creature's growth doubles *(assumed: as the weekly one, that week)*, and stacks of it **spawn across
      the map next to random map objects** as their guards (numbers when built).
    - **15 % plague**: no growth that week, and the creatures waiting in every town's dwellings are **halved** (rounded down).
    - **15 % starvation**: no growth that week, nothing else.
    - **25 %** nothing *(assumed: then the weekly roll is made as usual)*.
  - No event in the game's first week *(assumed)*.
  - The map editor and the template editor choose which **factions' creatures** may be picked, separately for weekly and monthly events;
    default: the playable factions only. Rolls use `GameState.Random`.

### ADR-074: Size pass, per-town building rows and the Capitol (implementation, 2026-10-23)
**Status**: implemented (2026-10-23). Carries out parts of ADR-070 (size policy, TOWN-3) and ADR-073 (City Hall); choices the earlier ADRs left
open are recorded here.

- **Size pass**: 9 of 182 creatures stay Large: the five Dragon Utopia dragons, the Bone and Ghost Dragon, and Behemoth / Ancient Behemoth; the other
  33 that were Large are Medium. Whether a non-dragon line stays Large is revisited once there are visuals (CONT-2). `sim towns` keeps a similar
  spread (town totals 37–76 % against 36–74 % before).
- **Town column** in `buildings.csv` (after `id`): empty = shared by every town. A town type's rows of a building replace **all** shared levels of
  that building for that town, or add a building only that town has (the Necromancy Amplifier later). Each town's merged list is checked like the
  shared one (levels without gaps, known requirements, dwellings for every tier). `BuildingCatalog.For(town)` gives the list, `TownRules.Buildings`
  is what rules, the town screen, the editor and the validator use. Templates' start buildings are checked against the shared list.
- **Town hall names** (HoMM3): Village Hall, Town Hall, City Hall, **Capitol**. Capitol placeholder: 15 000 gold + 25 lumber + 25 ore, needs the
  **Castle** (fort 3), 4000 gold a day.
- **One Capitol per player**: building a second is refused; a captured Capitol drops to City Hall when the captor has one (`TownBuildingDowngraded`,
  after `TownOwnerChanged`); the map validator reports a second Capitol of one player. The AI builds the Capitol last.

### ADR-075: Fifth-round answers; caravan reach and queue, base-creature map dwellings, army morale (2026-10-23)
**Status**: accepted (2026-10-23), user answers in `open-questions.md` and a note in `ideas.md`; the caravan, dwelling and morale parts are
**implemented**. Supersedes ADR-073's 5-day caravan reach (TOWN-5) and its "no event in the first week" assumption (FLOW-2). Every other "To
confirm" assumption of ADR-073 stands.

- **TOWN-5 caravan reach**: no "days at the creature's speed" any more. The reach is **7500 movement points** with every step at the plain cost
  (100, diagonals 141): terrain and roads are ignored, and it is the same for every creature (`CaravanRules.MaxReach`, `Distance`).
  - The **raw** distance ignores hostile heroes, monsters and guarded tiles (confirmed) but not rock, water or blocked object cells; a dwelling
    beyond it is not in the town's caravan list (`CaravanRules.Dwellings`). Nothing moves that it counts, so it is a query, not a value stored
    when the dwelling is flagged.
  - **Ordering** needs an open route within the reach, as before; the **travel days** still come from the creatures' map speed along the cheapest
    open route, so a slow stack over swamp may take more than 5 days *(choice made here)*. Sending troops between towns uses the same reach.
  - The **queue** (`CaravanRules.Queue`): the caravans coming to a town, on their way first (fewest days left, then the order sent), then those
    waiting at the gates. The town screen shows it and, apart, the caravans sent from the town.
  - The AI builds a caravan only when a dwelling is within reach and hires only from the town's list.
- **Base-creature map dwellings** (ADR-073, built): `MapEditorSession.SetDwelling` refuses an upgrade (`EditResult.UpgradedCreature`), the editor
  lists base creatures only, the validator reports an upgrade as an error, and loading a map turns an upgraded dwelling into its line's base
  creature (`CreatureCatalog.BaseOf`; no format change).
- **CMB-2 army morale** (built, `BattleParticipant.ArmyMorale`, set when the battle starts):
  - **Mixed factions**: −1 morale per faction beyond the first among the army's creatures (HoMM3); neutral creatures count as one faction, war
    machines do not count. Applies to heroes' armies and town garrisons, not to neutral guards or banks.
  - **Brotherhood**: +1 morale for a town's defenders when the town has the upgraded tavern (tavern level 2): its garrison, or an own hero standing
    at its entrance when attacked.
  - With it, **CMB-7** is built: undead, constructs and elementals have no morale (their creature's, the hero's and warcries' included).
- **FLOW-2**: "no event in the first week" is the default, and the map editor (and the template) may also turn week and month events **off**, or
  start them later: skip up to **8 weeks** (2 months). Built with the week events (map format bump).
- **Campaign-like maps** may start a player **without a town**; losing a hero is then the defeat condition. This already works: a grace period of
  0 turns the town rule off and "lose a hero" is a defeat condition. The validator now warns when a player starts with heroes but no town while the
  grace period is on.
- **MAG-5**: the fixed 500 movement points stand; nothing to build before adventure spells (Phase 5).
- **Terrain relief** (art direction, `ideas.md`): the rules stay on the 2D grid, but the 3D map is **not flat**, Warcraft III-like: walkable ground
  lies between 0 and 4 cell widths of height; below that is water, above it cliffs that are cut off or mountains. Recorded in
  [presentation.md](../design/presentation.md); built with the terrain look-dev (Phase 5).

### ADR-076: War machine footprints and the deployment step (implementation, 2026-10-23)
**Status**: implemented (2026-10-23). Carries out ADR-073's war machine footprints and ADR-072's deployment (HERO-4); choices those ADRs left
open are recorded here.

- **Footprints**: `WarMachines.Footprint` gives 1 × 3 for every offensive machine and the tent, 1 × 2 for the cart; `BattleUnit.Width` ×
  `Height` (creatures stay square, `Size` is their side) and `RectAt` are what the grid, cover, breath areas and the views use.
  `BattleUnitInfo` carries `Width` and `Height` instead of `Size`. Machines stand in column 0 (the defender's last column), tent at row 1,
  offensive machine in the middle, cart at the bottom; stacks start in the next column (no gap). Without machines stacks start at the edge.
  In the 3D scene a machine's model is as long as its footprint and is pushed outwards, so it overhangs the edge.
- **Deployment step**: `BattlePhase.Deploying`, after the width choice and before the first round.
  - The deployer is the side ahead in Tactics (the same comparison as the width choice); a tie lets nobody. A computer side, and quick
    combat, never deploy (the AI keeps the automatic placement).
  - The zone depth follows the deployer's **own** Tactics level (Basic 4, Advanced 5, Expert 6 columns), not the effective level
    *(choice made here)*. It includes the machine column; obstacles are rolled outside the zone.
  - Before the step the army is placed as always; the player moves one stack at a time (`BattleDeployCommand`) to any free placement
    entirely inside the zone, then presses "Start battle" (`BattleStartCommand`, Enter). Machines stay where they are. Stacks the automatic
    placement left outside the zone may stay there *(choice made here)*.
  - Not in creature banks (they also have no width choice). Quick combat during the step ends it and fights the battle.
  - Front-ends: click a stack to pick it, then click where its footprint should be centred (`BattleTargeting.DeploymentPlacement`); a green
    or red preview in the 3D scene, the zone shaded. Drag and drop can come with the battle UI polish.
  - Events: `BattleDeploymentStarted`, `BattleUnitDeployed`, `BattleDeploymentEnded`. The step uses the Tactics skill already; the separate
    Deployment skill merges into Tactics with the skill rework (step 4).

### ADR-077: Map scenery, rivers, flaggable providers, resource caravans, caravan speed and limits, kingdom overview, Thieves' Guild, third object catalogue (2026-10-26)
**Status**: accepted as a design (2026-10-26), owner notes in `ideas.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of ADR-054
and ADR-075 (caravan travel time) and ADR-068 (resource providers are visited, not flagged). The open points are MAP-25 … MAP-34 and TOWN-9 …
TOWN-11 in `open-questions.md`, each with a proposal.

- **Map scenery**: static objects that only block and decorate: rocks, trees, dead trees (several sizes and kinds), ponds, bushes, mushrooms,
  flowers, fissures, mounds, ruined buildings, creature bones, castle walls. The generator places them as a zone's obstacles (today rock
  terrain clumps); the editor paints them. Which ones block is MAP-25.
- **Rivers**: like HoMM3 (MAP-26).
- **Resource providers become flaggable** like map dwellings: the Water Wheel, the Windmill and the Warehouses (whether the Mystical Garden too is
  part of TOWN-9). A town's **caravan** picks up their resources the way it hires creatures from flagged dwellings (TOWN-9).
- **Caravan speed**: a hired caravan, with creatures or resources, moves **2500 movement points a day**, ignoring terrain penalties (replacing
  the creature's map speed, ADR-075). With the 7500-point reach that is at most 3 days (TOWN-10).
- **Caravan limit**: a town has at most **6 caravans** ordered to it at once, **8** while it has a governor; further orders queue (TOWN-10).
- **Kingdom overview**: a screen with the player's whole economy: towns, mines, providers, artifacts and heroes that bring income, with totals.
- **Thieves' Guild** in the tavern, like HoMM3: how the player ranks against the others by towns, heroes, gold, lumber and ore, rare resources,
  artifacts, income, army strength, best hero and best creature (TOWN-11).
- **Third object catalogue** (adventure-map.md): Sphinx (MAP-27), Sanctuary (MAP-28), Sacrificial Altar (MAP-29), Refugee Camp (MAP-30), Den of
  Thieves (TOWN-11), Cartographer (MAP-31), Astrologer's Tower (MAP-32, needs the week events), Artifact Merchant (MAP-33), Witch Hut (MAP-34).
  The Subterranean Gate exists already (two-way gate between levels).

### ADR-078: The skill rework, Necromancy and the Necromancy Amplifier (implementation, 2026-10-26)
**Status**: implemented (2026-10-26). Carries out ADR-069's skill rework with ADR-070's distance penalty, ADR-072's details (HERO-1 … HERO-7,
MAG-7 … MAG-10, CMB-6, TOWN-6) and ADR-073's Primal Fury mage odds; choices those ADRs left open are recorded here.

- **Ids** (HERO-6): `BuiltInSkills.Renamed` maps `offense.third` → `defense.leadership`, `defense.third` → `tactics.alert`, `tactics.drill` →
  `tactics.wartempo`, `tactics.deployment` → `tactics.tactics`, `pathfinding.troops` → `pathfinding.scouting` and `adrenaline.*` → `rage.*`.
  `Resolve`/`Migrate` apply it wherever skills are read: hero skills, the hero table (skills and specialties), map and save heroes, artifact
  effects and `ChooseSkillCommand` in old replays. A level above the new maximum drops to it; two old skills that became one keep the higher
  level. No map format bump. `heroes.csv` uses the new ids.
- **Groups**: 13 (six general, six schools, Primal Fury). `SkillGroup.Factions` lists the towns of a faction group (`BelongsTo`); Primal Fury
  belongs to Stronghold and Swamp Bestiary and weighs 6 in the offers for mages and warriors alike. Rivals: one arch-rival per town (HERO-3).
- **Numbers**: Melee 9/16/23/30 %, Archery 15/25/35 %, Pathfinding −25 % a level up to Master, Logistics 10/20/30 % up to Expert, Alertness
  +1/2/3 initiative added to the creature's before the warcry percentages, Tactics width only from Advanced (30/32; Expert 28–34). Troop
  management is gone without a replacement effect.
- **Scouting**: `Hero.ScoutingRadius` is the base radius (5, or what maps and artifacts set) plus 2/4/6. `VisibilityService` reveals the wider
  circle on `HeroLeveledUp` for Scouting *(choice made here: at once, not at the next step)*.
- **Ranged penalty** (CMB-6): `BattleGrid.RangedPenaltyEighths` = distance (cells between the footprints / 10, none for machines) + cover −
  Marksmanship (2/4/6 eighths, not below 0); above 7/8 there is no shot (`BattleState.MaxRangedPenaltyEighths`). A shot deals (8 − penalty)/8;
  `HitModifiers.PenaltyEighths` replaces `CoverEighths`, and the 15-cell halving (`LongRange`) is gone.
- **Primal Fury**: Interference (`rage.third`) carries the Spell Power penalty and the +1/2/3 spell cost (`HeroBattleBonuses.EnemySpellCostPenalty`),
  data until casting. Primal Fury mages roll 25/25/25/25 at every level (`StatGrowth.Odds` takes the hero's town). Their Spell Power does not
  strengthen Blood Rage and warcries yet (no numbers; roadmap). Roster: Stronghold warriors Urzog, Kragash and Shara now start with Melee
  instead of Armor or Tactics; Swamp Bestiary warriors Ssarkh, Mudjaw and Oszrak with Armor, Brakkis with Leadership and Ylsa with Spell
  resistance *(choices made here: one starting skill swapped, the specialty kept)*.
- **Mana**: the ADR-069 attunement table; `ManaRules.LevelMana` = 10 × ⌈level / 10⌉ added to the maximum only (MAG-8).
- **Necromancy** (`NecromancyRules`, applied in `BattleService.Finish`): only after the enemy was **defeated** (heroes and neutral guards); not
  when it retreated *(choice made here; surrender does not exist yet)*. Order: `BattleEnded`, the survivors' `ArmyChanged`, then the raised
  undead (`ArmyChanged` and `UndeadRaised(Hero, Raised)`), then `HeroExperienceGained` and level-ups. The random split of a tier between its two
  lines is 0–100 % in whole percent (one game-RNG draw per tier). `UndeadRaised` lists what joined, with the upgrade id where 3/4 went to an
  upgraded stack; nothing is published when nothing joined.
- **Necromancy screen**: `BattleHud` keeps the last `UndeadRaised` text; "Return to the map" (Enter) first shows it on the result panel, then
  closes the battle. Not run in Godot yet.
- **Necromancy Amplifier**: the first town row in `buildings.csv` (`necromancy-amplifier`, Necropolis, 1500 gold + 5 mercury, mage guild 1).
  `NecromancyRules.IsAmplified` asks whether the player owns any town with it, so a captured one helps its new owner.
- **AI**: computer heroes take Necromancy (Death attunement) whenever a level-up offers it, otherwise the first offer
  (`SkillOffers.ComputerChoice`) *(choice made here; how much the AI should value other skills is AI tuning)*.
- Tests: `SkillReworkTests`, `NecromancyTests` and updated battle, mana and progression tests; `sim selftest` passes with a new digest.

### ADR-079: Flaggable providers, resource caravans, caravan speed and limit, Thieves' Guild, sanctuary, rivers and scenery (2026-10-26)
**Status**: implemented (2026-10-26). The owner's answers to TOWN-9, TOWN-10, TOWN-11, MAP-25, MAP-26 and MAP-28 (ADR-077's questions);
supersedes ADR-068's weekly visits of resource providers and ADR-075's travel time by the creatures' map speed. Map format **v22**; random map
generator version **4** (rivers and scenery change every generated map). Choices made here are marked *(choice)*.

- **Flaggable providers** (TOWN-9): the Water Wheel, the Windmill, the six Warehouses and the Mystical Garden are flagged like mines
  (`ResourceProvider.Owner`, `IFlaggable`, scouting radius 2). While flagged, a week's output is added to `Stored` at the start of every week
  (`EconomyService.StockProviders`, after the towns' growth; also at game start for providers owned from the start *(choice)*); it piles up.
  The first flagging of a neutral provider gives one week's output at once (`WasClaimed`); a later capture takes what the former owner left
  *(choice: no extra week)*; a defeated player's providers turn neutral and keep their store. An own hero's visit takes everything stored
  (`MapObjectFoundEmpty` when empty). The Warehouses' choice is gone: each week is, at random, the resource or 500 gold
  (`ResourceChoiceDecision` and `ChooseResourceRewardCommand` removed, so **replays that used `resourceReward` no longer load**). Events
  `ProviderOwnerChanged`, `ProviderStockChanged`. The editor sets an owner (`SetProviderOwner`); format v22 `owner` on `provider` objects.
- **Resource caravans** (TOWN-9): `FetchResourcesCommand` sends a town's caravan for everything stored at an own provider in the caravan's
  raw reach (`CaravanRules.Providers`, `CheckFetch`): a flat 100 gold (placeholder, `ResourceCaravanCost`), the same reach and open-route rules
  as creature caravans, and it counts towards the limit. A `Caravan` with `Cargo` (no creature); on arrival the cargo joins the owner's
  resources (no garrison slot needed); a blocked route puts the cargo back in the store and refunds the 100 gold, unless the provider changed
  hands (then the cargo is lost *(choice)*). The caravan events carry an optional `Cargo`. The AI fetches stores worth five times the price.
- **Caravan speed and limit** (TOWN-10): every caravan moves 2500 movement points a day at the plain step cost (terrain, roads and rivers
  ignored), so the 7500-point reach takes at most 3 days. A town takes at most 6 caravans at once (8 while it has a governor), counting those
  on their way and those waiting at the gates; troops sent between own towns count at the destination. Orders at the limit are refused with a
  message (`CaravanRules.Limit`, `CheckLimit`).
- **Thieves' Guild** (TOWN-11): `ThievesGuild.Table` ranks every player still in the game (computers and teammates too) row by row; equal
  values share a place. Rows by the player's taverns: 1 — towns, heroes; 2 — + gold, lumber and ore, rare resources; 3 — + artifacts, income;
  4 or more — + army strength (creature value of armies and garrisons), best hero (highest level), best creature (most valuable). A "Thieves'
  guild" button on the town screen of a town with a tavern opens `ThievesGuildPanel` (not run in Godot yet). The **Den of Thieves** map
  object (`den-of-thieves`) shows the whole table on every visit (`ThievesDenVisited`); nothing is kept.
- **Sanctuary** (MAP-28, `sanctuary`): a hero standing on its entrance cannot be attacked; an enemy stepping in is refused
  (`MapObjectRefused`, `MoveResult.Occupied`) and the AI does not target it. A hero may stay as long as it likes, one at a time; nothing else.
- **Rivers** (MAP-26): an overlay like roads (`RiverType` Clear, Icy, Muddy, Lava; looks only). Leaving a river tile costs 25 % more
  (`RiverRules.PenaltyPercent`; Pathfinding does not cut it *(choice)*); a road-to-road step ignores the river (a bridge). Format v22 `rivers`
  layer (`RiverCodes`); editor River brush (`PaintRiver`, `RiversPainted`); the 3D view draws them under the roads. The generator
  (`RiverPlacer`, after the roads) runs a river through about half the zones, from the foot of one wall to the farthest foot of another, over
  free ground, its look from the terrain where it starts *(choice: there are no lakes yet, so it joins borders)*.
- **Scenery** (MAP-25): a `Scenery` object kind from `src/Core/data/scenery.csv` (`SceneryTable`): rocks, trees, dead trees, ponds, bushes,
  mushrooms, flowers, fissures, mounds, ruins, bones and castle walls, every kind in two or more sizes, every cell blocking, no entrance
  (`Footprint.Obstacle`). Walls are 1×N straight pieces: horizontal and vertical (2–4) and diagonal both ways (2–3). Each row lists the
  terrains it suits. The generator (`SceneryPlacer`, right after the terrain) gives each rock clump back the ground around it and fills exactly
  the clump's tiles with fitting scenery, larger pieces first (a small rock where nothing else fits), so connectivity stays as checked.
  Format v22 `scenery` objects; the editor places them from the object palette.
- Tests: `ResourceProviderTests`, `CaravanTests`, `ThievesGuildAndSanctuaryTests`, `RiverTests`, `SceneryTests`.

### ADR-080: The third object catalogue: cartographer, artifact merchant, refugee camp, sacrificial altar, sphinx, witch hut (2026-10-26)
**Status**: implemented (2026-10-26), except the Astrologer's Tower (waits for the week events). The owner's answers to MAP-27 and MAP-29 …
MAP-34 (ADR-077); the Den of Thieves and the sanctuary are in ADR-079. Map format v22 (same bump as ADR-079). Choices made here are marked
*(choice)*; numbers are drafts.

- **Cartographer** (MAP-31): `cartographer.surface` and `cartographer.underground` (`RevealsLevel`). A visit asks the visitor's player whether
  to buy the map of that level for **3000 gold** (`CartographerDecision`, `AnswerCartographerCommand`); buying reveals the whole level
  (`MapBought`, then `SurroundingsRevealed` over the level). Once per player; the underground one on a single-level map, or a player short of
  gold, is refused (`MapObjectRefused`). The generator places at most one surface cartographer *(choice)*; the AI buys with three times the
  price in hand.
- **Artifact merchant** (MAP-33, `artifact-merchant`): 7 artifacts, each Minor or Major at even odds *(choice)*, rolled at game start and on
  the first day of every month (`MerchantRestocked`); price twice the artifact's worth, with worth by tier Minor 2000, Major 5000
  (`ArtifactMerchantRules`, *choice: there were no artifact prices*); a sold slot stays empty until the restock. The hero on the entrance buys
  (`BuyArtifactCommand`, `ArtifactBought`, `ArtifactSource.Bought`) with room in its backpack.
- **Refugee camp** (MAP-30, `refugee-camp`): every week (and at game start) a random base creature of tiers 3–5 from the playable towns
  (`RefugeeCampRules.Candidates`) with one week's growth (`RefugeesArrived`); unsold ones leave. Bought by the hero on the entrance at the
  normal price, no Stewardship discount *(choice)* (`RecruitAtRefugeeCampCommand`, `DwellingRecruited`). Not flaggable.
- **Sacrificial altar** (MAP-29, `sacrificial-altar`): by the hero's town (`ProgressionService.TownOf`): Necropolis, Inferno and Dungeon
  heroes sacrifice creatures for their hit points × 2 in experience (`SacrificeCreaturesCommand`; the hero keeps at least one creature
  *(choice)*); Haven, Sylvan and Academy heroes artifacts, worn or carried, for 2000 / 5000 / 10 000 by tier (`SacrificeArtifactCommand`);
  Stronghold and Swamp Bestiary heroes either. Any number of times (`SacrificeMade`, then the experience and any level-ups).
- **Sphinx** (MAP-27, `sphinx`): one riddle per hero (`VisitScope.OncePerHero`): the map author's (`question`, `answer`, `wrong1`, `wrong2` on
  the object, all or none) or one drawn from `src/Core/data/riddles.csv` (`RiddleTable`, ten drafts). Three answers, the right one's place
  rolled (`SphinxDecision`, `AnswerSphinxCommand`). Right: a Major artifact or 5000 experience at even odds (a full backpack loses the
  artifact). Wrong: the hero's movement points drop to 0 for the day and it gets −3 morale for its next battle (a `HeroStatus`). Event
  `SphinxAnswered`. The AI answers the first option.
- **Witch hut** (MAP-34, `witch-hut`): two skills of two groups, any group (general, school or faction), set in the map (`skills`) or rolled
  at game start. A visit offers each skill the hero can raise; where it cannot (maxed, or no free group), the first skill of the same group it
  can raise stands in (`WitchHutRules.Options`). Choosing raises it a level (Basic when new) (`WitchHutDecision`,
  `ChooseWitchHutSkillCommand`, `WitchHutSkillLearned`); a hero takes one witch hut lesson per hero level (`Hero.WitchHutLevel`), leaving
  without choosing uses nothing. The AI takes Necromancy when offered, else the first option.
- **Astrologer's Tower** (MAP-32): the answer is recorded (a visit shows next week's event; no re-roll); built after the week events.
- UI: the decision panel asks the cartographer, sphinx and witch hut questions; `ObjectServicePanel` serves the merchant, the refugee camp and
  the altar (not run in Godot yet). The generator's treasure sites include all six (values are drafts). The map editor places them; editing a
  sphinx's riddle or a hut's skills in the inspector is not built yet (the map file holds them).
- Tests: `ThirdCatalogueTests`.
