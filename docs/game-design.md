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
- **Map sizes**: square presets 48, 72, 96, 128, 160, 192 and 256 (max), each with an optional
  underground level. **Up to 8 players.**
- Square grid, up to 2 levels (surface + underground) connected by **subterranean gates** (implemented).
- **Movement (HoMM3 rules, implemented)**:
  - A step costs the terrain of the tile being **left**. Water/rock are impassable on foot.
  - **Terrain classes** (accepted; the per-terrain assignment below is a proposal ❓):

    | Class | Rule | Proposed terrains |
    |---|---|---|
    | **Open** | cost 100 for everyone | grass, dirt |
    | **Rough** | fixed penalty for **all** factions, e.g. +25 % (125) | rough |
    | **Native** | 100 for armies **native** to it; penalty for everyone else | sand 150, snow 150, swamp 175, lava 150, subterranean 150 |

    - Proposed native owners ❓: Academy (snow), Swamp Bestiary (swamp), Inferno (lava),
      Dungeon (subterranean), Stronghold (sand). Haven, Sylvan and Necropolis may need native
      terrains of their own (e.g. new *highlands*, *forest floor*, *wasteland* types), or they could live on open terrain.
    - **Who counts as native** ❓: HoMM3 rule is that the penalty is waived only if **every** creature in
      the army is native. A *Pathfinding* skill reduces non-native penalties (HoMM3: 25/50/75 %).
    - Today's implementation still uses fixed per-terrain costs (no factions yet). It switches to
      classes when towns/factions exist (Phase 3).
  - **Roads (HoMM3-style)**: a road layer on top of terrain, drawn in the editor and generated
    between towns and through zone connections. Road types and costs: **dirt 50, gravel 65,
    cobblestone 75**. The road cost replaces the terrain cost and **ignores native/rough penalties**.
    It applies when the step goes from a road tile to a road tile ❓ (HoMM3 behaviour); diagonals ×1.41 as usual.
  - 8-way movement; diagonal steps cost ×1.41.
  - **Last step**: a hero with any movement points left may make one more step, whatever it costs,
    and ends with 0.
  - **Daily movement points** = base from the **slowest creature in the army** (speed ≤3: 1500, 4: 1560,
    5: 1630, 6: 1700, 7: 1760, 8: 1830, 9: 1900, 10: 1960, ≥11: 2000) × (100 % + skill bonus, e.g.
    Logistics) + flat artifact bonus. Recomputed at the start of the owner's turn. Without an army, 1500.
- **Pathfinding** (implemented): cheapest route; visible heroes and object bodies block; routes may only
  *end* on an object's entrance (except gates, which continue on the other side); unexplored tiles are
  never used. Every step carries a day index for HoMM-style day markers. Heroes the player can't see
  don't affect the plan; movement stops when it bumps into one.
- **Map objects** (implemented): resource piles (picked up), mines (flagged, daily income), treasure
  chest (choose gold 1000 *or* 500 XP), subterranean gate (two-way link). Objects have a footprint and
  are entered only through their entrance tile, usually from below.
  Planned: towns, artifacts, dwellings, shrines, guarded neutral stacks, boats/shipyards.
- **Meeting heroes**: moving onto a tile with another hero is an interaction, like visiting an
  object, so routes may **end** on that hero's tile (never pass through it):

  | Other hero | Result | Status |
  |---|---|---|
  | **Own** | Opens the **exchange screen**: swap army stacks and artifacts. The moving hero stays on its adjacent tile. | planned (Phase 1 follow-up for the meeting, Phase 2/3 for armies/artifacts) |
  | **Enemy** | Starts a **battle** | Phase 2 |
  | **Allied** (team games) | **No interaction for now**: the tile is simply blocked. Exchange with allies may come later. | decided |

  - Under refog, an unseen enemy hero blocks the step (`Occupied`) instead of starting a battle ❓.
    Alternatively, bumping into it could start the battle (ambush).
- **Gates and teleporters**: no random-exit portals for now. Every link is fixed in the map, so
  movement and pathfinding stay deterministic and predictable.

  | Kind | Behaviour | Status |
  |---|---|---|
  | **Two-way gate** | Linked pair; visiting either end moves the hero to the other. Subterranean gates connect levels; two-way monoliths work on the same level. | subterranean gate implemented; two-way monolith uses the same rules |
  | **One-way gate** | An **entrance** linked to exactly one **exit**. Several entrances may share an exit. The exit is a landing spot only; visiting it does nothing, and it can't be entered as a gate. | planned (Phase 1 follow-up) |

  - Pathfinding goes through both kinds: a route continues from the linked end, and one-way gates only in
    their direction. A blocked exit (a hero standing on it) means no teleport.
  - Editor: link tool for both kinds; the validator flags unlinked gates and exits that no entrance uses.
- **Fog of war** (implemented): two modes, **chosen when the game is created** (`GameOptions.FogMode`):
  1. **Permanent reveal** (HoMM3): once explored, terrain, objects and units stay visible.
  2. **Shroud + refog for units**: terrain and objects stay revealed once explored, but other players'
     heroes (later also armies) are only visible on tiles currently **in sight**. Sight has hysteresis:
     a tile comes into sight within an observer's scouting radius, but only drops out once every observer
     is farther than radius + **refog margin** (default 2, configurable). Example: radius 3 + margin 4
     reveals at 3 tiles and refogs at 7.
  - Observers: heroes (scouting radius 5) and owned mines (radius 2).

### 4.2 Heroes
- Primary stats: Attack, Defense, Spell Power, Knowledge.
- Secondary skills (levels Basic/Advanced/Expert), level-up choice between two skills.
- Army of up to 7 creature stacks.
- Artifacts with paper-doll slots and backpack.
- Spell book, mana.

### 4.3 Towns and economy
- **Six resources**: gold, lumber, ore, crystal, mercury, sulfur (implemented as `ResourceSet`).
- One building per town per day; building tree per faction.
- Weekly creature growth, dwellings on the map.
- **Income** is paid at the start of the owner's turn. **There is no base or placeholder income**:
  players earn only what their sources produce. Sources:
  - **Mines** (implemented): gold 1000, lumber/ore 2, rare resources 1.
  - **Towns**: gold by hall level, plus resource buildings (Phase 3).
  - **Hero secondary skills**, Estates-like (Phase 3).
  - **Hero specialisations**, e.g. +N of a resource per day (Phase 3).
  - **Artifacts**, e.g. resource-producing items (Phase 3).

  Each source is a pluggable `IIncomeSource`, and `EconomyService.DailyIncome` sums them.

### 4.4 Factions and towns
Towns are organised into four groups (names are working titles ❓). **Groups matter only in lore and
the campaign**: story, scenario setup, campaign alliances and scripted events. In standard and random-map
games they have **no rule effect**. There are no automatic alliances, shared skills, diplomacy or
group-based bonuses; teams are set per game, and native terrain belongs to individual towns.
Templates and the random map generator may still use groups as a *filter* for which towns appear,
because that is content selection, not a rule.

| Group | Towns | Playable |
|---|---|---|
| 1 · Light | **Haven** (humans), **Sylvan** (elves), **Academy** (mages) | yes |
| 2 · Darkness | **Necropolis** (undead), **Inferno** (daemons), **Dungeon** (underground dwellers / dark elves) | yes |
| 3 · Wild | **Stronghold** (orcs), **Swamp Bestiary** | yes |
| 4 · Primordial | **Conflux** (elementals), **Dragon Utopia** (all dragons except bone dragons) | **no**: not balanced for PvP; campaign and neutral (map guardians, dwellings) only |

- 8 playable towns + 2 special towns. Each town has 7 creature tiers with upgrades ❓.
- Bone dragons belong to Necropolis, not Dragon Utopia.
- **Rivalries are lore only.** Opposition between towns or groups (e.g. Haven vs Necropolis) exists
  in story and campaign, but has **no combat or rules effect**. There are no hate/enmity damage bonuses
  (unlike HoMM3 Angels vs Devils) and no alignment modifiers. Creature abilities target *properties*
  (undead, flying, size, …), never town or group membership.
- Vertical slice: 2 towns. Proposal: Haven and Necropolis, the most iconic pairing and the best
  showcase for the dark tone. Their rivalry is story only ❓.

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
  - **Battlefield**: **24 cells tall**, **28–36 cells wide**, depending on the hero's learned
    *Tactics* skill (default **30**). Exact width per Tactics level ❓ (e.g. none 30, basic 32,
    advanced 34, expert 36; or does a defender's Tactics shrink it toward 28?). 8-way movement assumed ❓.
  - **Surround / flanking**: a unit's perimeter grows with its size, so more attackers fit around a
    large unit. **No generic flanking bonus**: damage is the same from any side, however many units
    surround the target. **No facing.** Engagement is a hook for **special abilities**: some creatures
    get bonuses when the target is already engaged by another enemy, per extra side, per number of times
    the target was hit this round, and so on. The core tracks engagement and hit history so abilities can
    use them.
  - **Body blocking & cover**: footprints block movement. Ranged attacks whose line of fire crosses a
    unit (friend or foe) larger than the target ❓ suffer a cover penalty.
  - Size alone does not grant reach, cleave or trample/push.
  - **Area attacks are creature abilities**, independent of size:
    - **Breath** (dragon- or phoenix-like): also hits the cells directly behind the target in the
      line of attack.
    - **Cleave / multi-head** (cerberus- or hydra-like): also hits enemies adjacent to the attacker
      (cerberus: up to 3 in front, hydra: all around).
    - **HoMM3-style friendly fire**: breath also hits friendly units in its line; cleave/multi-head
      hits enemies only. Only the primary target retaliates.
    - A unit whose footprint touches the area is hit once (assumed ❓).
  - Open ❓: diagonal squeeze rules between units/obstacles, how flyers and teleporters use footprints.
- **Turn order: initiative decides order, not frequency.** Each round every stack acts **exactly
  once**, in descending **Initiative** (a stat separate from **Speed**, which is movement range).
  A HoMM5-style timeline bar shows the order. Unlike HoMM5, no unit acts twice in a round.
  - **Ties**: attacker's side first, then higher speed, then a deterministic seeded roll.
  - **Wait** (HoMM3): waiting units act at the end of the round, in **reverse** initiative order.
  - Open ❓: spells and morale that change initiative mid-round.
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
- **Map editor**: a **separate standalone app** (not an in-game mode). Implemented in core with
  undo/redo: terrain painting (brush radius, drag strokes), object placement/removal, gate linking,
  mine owners, hero placement, player slots, map name/description, validation. Godot UI for most of it
  is pending. Later: victory/loss conditions, events/triggers, map resize, and the **template editor**
  plus "generate map from template" (§4.9).
- **2D harness**: a top-down debug front-end over the same core, used for rule development,
  scenario scripting and visual debugging of AI.

### 4.9 Random map generation (templates)
Modelled on the Heroes 3: Horn of the Abyss template system.

- **Both the game and the editor generate maps.** The game's new-game screen picks a template from the
  available ones, a map size, players, a seed and game options, then generates and starts. The editor
  generates into an **editable** map, so designers can hand-polish a generated map and save it.
- **Templates are authored in the editor** (template editor: a zone graph) and saved as
  `*.ewtemplate.json`. The game lists every template it can find, both built-in and user/mod folders.
- **A template** defines:
  - Supported map sizes, player-count range, whether an underground level is used, and human/AI
    restrictions.
  - **Zones** (map areas), each with:
    - Type: player start, AI-only start, treasure, junction.
    - Owner slot for start zones.
    - Relative size.
    - Level: surface or underground.
    - **Towns**: player towns and neutral towns (min/max), allowed town groups or "same as zone owner".
    - **Resource richness**: minimum mines per resource, extra resource piles.
    - **Treasure**: value bands with density (e.g. 500–3000 ×9, 3000–6000 ×6, 10000–15000 ×1).
    - **Monster strength**: none/weak/average/strong, plus allowed terrain types.
  - **Connections** between zones: which zones connect, how strongly the passage is guarded,
    and the passage type: normal, wide (unguarded), border guard/gate, or portal/subterranean
    gate across levels. A connection may also be optional or randomly present.
- **Generation is deterministic** from (template, size, players, seed), so a map can be shared by its seed.
- **Pipeline**:
  1. Place zones on the grid from the connection graph.
  2. Paint terrain per zone.
  3. Raise borders and obstacles.
  4. Cut connections and place their guards.
  5. Place towns.
  6. Place mines and resources.
  7. Place treasure by value band.
  8. Place neutral monsters.
  9. Validate: every start zone can reach the others, and start zones are fair against each other.
- Open ❓: symmetric generation for competitive templates; water/islands; how the slowest-creature and
  native-terrain rules feed into treasure/guard values; template-level restrictions per town group.

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
- **Camera**: classic strategy camera, tilted **15–30° from vertical** (more tilted when zoomed in,
  more top-down when zoomed out), **rotatable ±45°** around the vertical axis, zoom in/out, pan.
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
| 7 | Resources | 7 (incl. gems) | 6: gold, lumber, ore, crystal, mercury, sulfur | accepted |
| 8 | Fog of war | Permanent reveal only | Choice per game: permanent reveal, or shroud + unit refog with hysteresis | accepted |
| 9 | Map scale | Up to 144×144 | Presets up to 256×256, 8 players | accepted |
| 10 | Random maps | Fixed templates (HotA adds a template editor) | Template editor in our map editor; game and editor both generate | accepted |
| 11 | Terrain penalties | Native terrain per town, flat costs otherwise | Three classes: open (no penalty), rough (fixed penalty for all), native (penalty only for non-natives) | accepted |
| … | | | *more twists to be proposed* ❓ | |
