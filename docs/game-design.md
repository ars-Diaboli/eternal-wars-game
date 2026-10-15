# Game design (living document)

> Status: **draft**. Items marked ❓ are open; see [open-questions.md](open-questions.md).

## 1. Vision

*Eternal Wars* is a turn-based **dark fantasy** strategy game inspired by Heroes of Might and Magic III,
presented in full 3D. Players lead heroes across an adventure map, gather resources, develop towns,
recruit armies and fight tactical battles on a square grid where creatures occupy 2×2, 3×3 or 4×4
cells.

It is **HoMM-inspired, not a clone**: we keep the proven loop (heroes, towns, weekly growth, tactical
battles) and deliberately change systems where we can add identity. Every twist is logged in §7.

The 3D presentation is meant to **add atmosphere without hurting readability**: the camera, map and
battles must stay as legible as HoMM3's hand-painted 2D.

**Modes, in priority order**: single-player vs AI → hot-seat → story campaign. Online multiplayer is
out of scope, but the deterministic core keeps it possible.

## 2. Pillars

1. **"One more turn"**: short, satisfying turns; meaningful decisions every day of the week.
2. **Readable at a glance**: every tile, object and unit identifiable from the default camera.
3. **Moddable by data**: factions, units, spells, artifacts and map objects are data, not code.
4. **Deterministic core**: the same inputs always produce the same game. This enables replays, tests,
   AI simulation and (later) multiplayer.

## 3. Core loop

```
 ┌──────────── Day (each player in turn) ────────────┐
 │ Move heroes ─► visit objects / pick up loot        │
 │      │                 │                           │
 │      ▼                 ▼                           │
 │   Battles ◄──── Neutral / enemy armies             │
 │      │                                             │
 │      ▼                                             │
 │ Experience, artifacts ─► stronger heroes           │
 │ Towns: build 1 structure/day, recruit weekly growth│
 └──────────────── End turn ─────────────────────────┘
        Week start: creature growth, "week of X" events
```

## 4. Feature set (target for 1.0)

### 4.1 Adventure map
- Square grid, up to 2 levels (surface + underground) connected by portals/gates (HoMM3 model).
- Terrain types with movement costs (implemented: grass, dirt, sand, snow, swamp, rough, lava,
  subterranean, water, rock).
- 8-way movement, diagonal steps cost ×1.41, daily movement points per hero.
- Map objects: towns, mines, resource piles, treasure chests, artifacts, dwellings, shrines,
  teleporters, guarded neutral stacks, boats/shipyards.
- Fog of war / shroud per player ❓ (HoMM3: permanent reveal vs. re-fogging).
- Pathfinding with path preview and per-day move markers.

### 4.2 Heroes
- Primary stats: Attack, Defense, Spell Power, Knowledge.
- Secondary skills (levels Basic/Advanced/Expert), level-up choice between two skills.
- Army of up to 7 creature stacks.
- Artifacts with paper-doll slots and backpack.
- Spell book, mana.

### 4.3 Towns and economy
- Seven resources: gold, wood, ore, mercury, sulfur, crystal, gems (implemented as `ResourceSet`).
- One building per town per day; building tree per faction.
- Weekly creature growth, dwellings on the map.
- Mines produce daily income; towns produce gold by hall level.

### 4.4 Factions and towns
Towns are organised into four groups. Group names are working titles ❓, and so is whether a group has
gameplay meaning (alliances, diplomacy, shared skills) or is only thematic.

| Group | Towns | Playable |
|---|---|---|
| 1 · Light | **Haven** (humans), **Sylvan** (elves), **Academy** (mages) | yes |
| 2 · Darkness | **Necropolis** (undead), **Inferno** (daemons), **Dungeon** (underground dwellers / dark elves) | yes |
| 3 · Wild | **Stronghold** (orcs), **Swamp Bestiary** | yes |
| 4 · Primordial | **Conflux** (elementals), **Dragon Utopia** (all dragons except bone dragons) | **no**: not balanced for PvP; campaign and neutral (map guardians, dwellings) only |

- 8 playable towns + 2 special towns. Each town has 7 creature tiers with upgrades ❓.
- Bone dragons belong to Necropolis, not Dragon Utopia.
- Vertical slice: **2 towns from opposing groups**. Proposal: Haven vs Necropolis, the most iconic
  pairing and the best showcase for the dark tone ❓.

### 4.5 Combat
- **Separate battlefield** (HoMM3-style cut to an arena themed by the terrain where the battle starts).
- **Square grid** with **multi-cell units**. Every creature stack has a footprint:
  | Size | Footprint | Typical creatures |
  |---|---|---|
  | Small | 2×2 cells | infantry, archers, imps |
  | Medium | 3×3 cells | cavalry, griffins, golems |
  | Large | 4×4 cells | dragons, behemoths, titans |
  - The fine cell grid lets units of different sizes share one board. Positions refer to the top-left
    cell of the footprint.
  - Battlefield size ❓ (e.g. 30×22 cells ≈ HoMM3's 15×11 hexes at small-unit scale).
  - **Surround / flanking**: a unit's perimeter grows with its size, so more attackers fit around a
    large unit. Attacks from the flank or rear get bonuses ❓ (values; is "facing" tracked, or does
    flanking count attackers on opposite/adjacent sides?).
  - **Body blocking & cover**: footprints block movement. Ranged attacks whose line of fire crosses a
    unit (friend or foe) larger than the target ❓ suffer a cover penalty.
  - Not adopted: reach/cleave by size, trample/push.
  - Open ❓: 4-way vs 8-way movement, diagonal squeeze rules, how flyers and teleporters use footprints.
- **Turn order: initiative decides order, not frequency.** Each round every stack acts **exactly
  once**, in descending **Initiative** (a stat separate from **Speed**, which is movement range).
  A HoMM5-style timeline bar shows the order. Unlike HoMM5, no unit acts twice in a round.
  - Open ❓: tie-breaking (attacker first? higher speed?), *Wait* (move to end of round? reverse
    initiative order among waiting units?), spells and morale that change initiative mid-round.
- Wait/defend, retaliation, ranged units, flyers, obstacles.
- Hero casts one spell per round; catapult, ballista, first-aid tent as war machines ❓.
- Auto-combat and quick-combat options.
- Siege battles with walls and towers (post-1.0 ❓).

### 4.6 Magic
- Four schools, five spell levels, mage guild in towns.

### 4.7 Game modes (priority order)
1. Single scenario vs. AI.
2. Hot-seat multiplayer (turn structure already models it).
3. Story campaign: chained scenarios, carry-over heroes, scripted map events and dialogue. This needs
   editor support for triggers and a script/event format.
- Online multiplayer: not planned (core determinism keeps the door open).

### 4.8 Tools
- **Map editor**: terrain painting (implemented: brush with radius), object placement, player
  setup, victory/loss conditions, events/triggers, random-map generator (later).
- **2D harness**: a top-down debug front-end over the same core, used for rule development,
  scenario scripting and visual debugging of AI.

## 5. Presentation

- **Art direction**: **grim, dark fantasy** with **realistic lighting and PBR textures**, plus nods
  to the original game (recognisable silhouettes for archetypal creatures and buildings, heraldic
  player colours, iconic map objects).
  - Lighting: physically based, low sun angles, volumetric fog, strong shadows. Day/night tint per
    in-game day ❓. Godot Forward+ with SDFGI/VoxelGI or baked lightmaps, to be decided by a perf spike.
  - Palette: desaturated earth tones. Player colours and interactive objects stay saturated so the
    map remains readable in a dark scene (readability pillar).
  - Materials: PBR (albedo/normal/roughness), weathered and worn.
  - **Terrain: smooth heightmapped landscape** (texture splatting, triplanar on slopes, blended
    biome transitions). The logical square grid stays invisible and appears as an overlay only for
    hover, path preview, movement range and in the editor. Walkability and costs still come from
    core tile data, so visual height must not contradict them (cliffs = Rock tiles, etc.).
  - Post-processing: subtle colour grading, bloom on magic only, vignette.
  - Placeholder until then: coloured tiles and capsules.
- **Camera**: angled perspective, ~55° pitch, pan/zoom; optional rotation ❓.
- **Terrain rendering**: start with per-tile meshes/colours; later blended terrain shader with
  heightmap and transitions.
- **UI**: HoMM-style right-side panel (minimap, hero/town list, end turn) and bottom status bar.
- **Audio**: per-terrain ambient music, faction town themes.

## 6. Non-goals (for now)
- Real-time elements, online multiplayer, procedural campaign, mobile touch UI.

## 7. Deliberate departures from HoMM3

| # | Area | HoMM3 | Eternal Wars | Status |
|---|---|---|---|---|
| 1 | Combat grid | 15×11 hexes; units occupy 1 or 2 hexes | Square cell grid; units occupy 2×2 / 3×3 / 4×4 | accepted |
| 2 | Tone | Bright high fantasy | Dark, grim fantasy, realistic lighting | accepted |
| 3 | Turn order | Speed decides order | Separate Initiative stat decides order (HoMM5 timeline UI); one action per round | accepted |
| 4 | Unit size | Mostly cosmetic (1 vs 2 hexes) | Size drives flanking exposure, body blocking and ranged cover | accepted |
| 5 | Towns | 9 towns, all playable | 8 playable in 3 groups + 2 non-playable "primordial" towns for campaign/neutrals | accepted |
| 6 | Terrain | Painted tiles | Smooth 3D terrain with on-demand grid overlay | accepted |
| … | | | *more twists to be proposed* ❓ | |
