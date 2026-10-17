# Adventure map

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../project/open-questions.md).

## Size and levels
- **Map sizes**: square presets 48, 72, 96, 128, 160, 192 and 256 (max), each with an optional
  underground level. **Up to 8 players** (ADR-020).
- Square grid, up to 2 levels (surface + underground) connected by **subterranean gates** (implemented).

## Movement (HoMM3 rules, implemented)
- A step costs the terrain of the tile being **left**. Water and rock are impassable on foot; there is no sea
  travel (ADR-051).
- **Terrain costs** (ADR-045): leaving a tile costs **100 for an all-native army** and the terrain's cost for everyone
  else. No terrain is free for everyone; only roads, Pathfinding and artifacts help.

  | Terrain | Non-native cost | Native towns |
  |---|---|---|
  | Grass | 125 | Haven, Sylvan |
  | Highlands | 125 | Haven, Conflux |
  | Forest floor | 125 | Sylvan |
  | Cursed lands | 125 | Necropolis |
  | Subterranean | 125 | Dungeon |
  | Sand (desert), beach | 125 | – |
  | Dirt | 150 | – |
  | Rough, wasteland | 150 | Stronghold |
  | Lava | 150 | Inferno |
  | Rocky ground | 150 | Dragon Utopia |
  | Ice | 175 | Academy |
  | Shallow water | 175 | Swamp Bestiary |
  | Snow | 200 | Academy |
  | Swamp | 200 | Swamp Bestiary |
  | Water (deep), rock (cliffs) | impassable | – |

  - **Native = the whole army** (HoMM3): the penalty is waived only if **every** creature in the army
    belongs to one of the terrain's native towns. Neutrals count as non-native.
  - The **Pathfinding** skill reduces the non-native penalty by 25/50/75 % (Basic/Advanced/Expert;
    swamp 200 → 175/150/125). Artifacts reduce it or make terrains native (implemented, ADR-061).
  - Implemented as data (`TerrainCatalog`); an all-native army (`MovementRules.ArmyTown`) pays 100, and
    Pathfinding applies in `MovementRules.StepCost`, which routes and steps share.
- **Roads (HoMM3-style, ADR-024/032)**: a road layer on top of terrain, drawn in the editor (generated roads
  between towns and through zone connections are planned, [random-maps.md](random-maps.md)). Road types and costs:
  **dirt 50, gravel 65, cobblestone 75**. The road cost replaces the terrain cost and **ignores native penalties**.
  It applies only when the step goes **from a road tile to a road tile**; diagonals ×1.41 as usual.
- 8-way movement; diagonal steps cost ×1.41.
- **Last step**: a hero with any movement points left may make one more step, whatever it costs,
  and ends with 0.
- **Daily movement points** = base from the **slowest creature in the army** (map speed ≤3: 1500, 4: 1560,
  5: 1630, 6: 1700, 7: 1760, 8: 1830, 9: 1900, 10: 1960, ≥11: 2000) × (100 % + skill bonus, e.g.
  Logistics) + flat artifact bonus. Recomputed at the start of the owner's turn. Without an army, 1500.
  The table uses each creature's adventure **map speed** (`MapSpeed`, HoMM3 scale), which is separate from
  its battlefield Speed in cells (ADR-037).

## Pathfinding (implemented)
Cheapest route; visible heroes and object bodies block; routes may only *end* on an object's entrance (except gates,
which continue on the other side); unexplored tiles are never used. Every step carries a day index for HoMM-style day
markers. Heroes the player can't see don't affect the plan; movement stops when it bumps into one.

## Map objects
Objects have a footprint and are entered only through their entrance tile, usually from below.

| Object | Behaviour | Status |
|---|---|---|
| Resource pile | Picked up | implemented |
| Mine | Flagged, daily income (gold 1000, lumber/ore 2, rare 1); may be guarded | implemented |
| Treasure chest | Choose gold *or* XP | implemented |
| Artifact | Picked up into the hero's slots or backpack | implemented (ADR-061) |
| Town | Captured, garrisoned, built up ([towns-and-economy.md](towns-and-economy.md)) | implemented |
| Map dwelling | Flagged, recruits one creature, supplies caravans | implemented (ADR-054) |
| Neutral monster | Fights, joins or flees | implemented (ADR-040) |
| Gates | Teleport (see below) | implemented |
| Shrines and other visitables | – | planned ❓ |

- **Treasure chests roll their reward** (seeded game RNG): 1000/1500/2000 gold *or* 500/1000/1500 XP, the three tiers
  equally likely (ADR-039). Map authors may fix the values. Whether chests may hold artifacts is open ❓.
- **Mines may be guarded**: the editor or random map generator can attach a guard stack. The guard must be
  defeated before the first flagging; after that any hero re-flags the mine by visiting it. Mine guards always fight.
- **Neutral monsters** (ADR-040): a creature stack on a tile; stepping onto it attacks it, and it guards the 8 tiles
  around it. The **guard mode** is a map setting chosen in the editor:
  - **Classic** (HoMM3): entering a guarded tile starts the encounter, but pickups lying on guarded tiles
    can be grabbed from outside without a fight ("stealing").
  - **Area**: the whole 3×3 area is guarded; touching anything inside it, pickups included, starts the fight.
  - Dispositions (HoMM3 names, deterministic): Compliant, Friendly, Aggressive, Hostile, Savage decide whether
    the stack offers to **join** (free, needs room) or tries to **flee** from a much stronger army (≥ 3×), else
    it fights. The player accepts or fights. Unset counts are rolled by tier at game start.
- **Towns** (ADR-046, ADR-050): 7×7 with the corners free, entered through the bottom-middle cell
  (`.BBBBB.`, five rows of `BBBBBBB`, `.BBVBB.`). Entering an enemy or neutral town fights its garrison, if any, and
  captures it; an own hero on the entrance is the town's visiting hero.

## Meeting heroes
Moving onto a tile with another hero is an interaction, like visiting an object, so routes may **end** on that hero's
tile (never pass through it):

| Other hero | Result | Status |
|---|---|---|
| **Own** | Opens the **exchange screen**: swap army stacks and artifacts. The moving hero stays on its adjacent tile. | implemented: stacks move with `MoveStackCommand` (split, merge, swap; a hero keeps one stack), `ExchangePanel` in the game; artifact exchange in core, its UI pending |
| **Enemy** | Starts a **battle**; the loser (or a hero that retreats) leaves the map | implemented |
| **Allied** (team games) | **No interaction for now**: the tile is simply blocked. Exchange with allies may come later. | decided (ADR-027) |

- **Teams**: the map defines default teams; the game setup screen may change them unless the map locks them
  (story/scenario maps). Free-for-all when nothing is defined. Templates have no teams yet ❓.
- Under refog: a moving hero **automatically stops** when an enemy hero comes into sight. If it still bumps into an
  unseen enemy, a **battle starts (ambush)**; no ambush bonus (ADR-031, ADR-038).

## Gates and teleporters
No random-exit portals for now (ADR-026). Every link is fixed in the map, so movement and pathfinding stay
deterministic and predictable.

| Kind | Behaviour | Status |
|---|---|---|
| **Two-way gate** | Linked pair; visiting either end moves the hero to the other. Subterranean gates connect levels; two-way monoliths work on the same level. | implemented |
| **One-way gate** | An **entrance** linked to exactly one **exit**. Several entrances may share an exit. The exit is a landing spot only; visiting it does nothing, it can't be entered as a gate, and routes may walk over it. | implemented (one-way monolith) |

- Pathfinding goes through both kinds: a route continues from the linked end, and one-way gates only in
  their direction. A blocked exit (a hero standing on it) means no teleport.
- Editor: link tool for both kinds; the validator flags unlinked gates and exits that no entrance uses.

## Fog of war (implemented, ADR-017, ADR-031)
Two modes, **chosen when the game is created** (`GameOptions.FogMode`):
1. **Permanent reveal** (HoMM3): once explored, terrain, objects and units stay visible.
2. **Shroud + refog for units**: terrain and objects stay revealed once explored, but other players'
   heroes are only visible on tiles currently **in sight**. Sight has hysteresis: a tile comes into sight within an
   observer's scouting radius, but only drops out once every observer is farther than radius + **refog margin**
   (default 2, configurable). Example: radius 3 + margin 4 reveals at 3 tiles and refogs at 7.

- Observers: heroes and owned towns (radius 5), owned mines and map dwellings (radius 2).
- Under refog, **objects out of sight show their last known state** (flag colour, chest still there),
  updated when they come back into sight. Units are hidden entirely (`ObjectMemory`).
