# Random map generation (templates)

> Part of the [game design](overview.md). Modelled on the Heroes 3: Horn of the Abyss template system (ADR-023, ADR-034, ADR-064).
> Everything here is built unless tagged *(planned)*; numbers are placeholders. Open items ❓:
> [open-questions.md](../project/open-questions.md).

Core `MapGenerator` (`EternalWars.Core.Generation`, version 8). Shipped templates in `src/Core/data/templates` (`duel`, `ffa-4`, `2v2`,
`mirror-duel`, `ffa-8`); CI checks each with `sim maps` (no problems, deterministic, starts reachable, AI play and replay work).

## Goals
- **Game and editor both generate**: the new-game screen (template, size, players, seed, options; ADR-086) and the editor's
  [Generate](#editor) tab, into an **editable** map (ADR-087).
- **Templates** (`*.ewtemplate.json`) come from the [template editor](#template-editor), saved to `user://templates`; the editor lists
  shipped and valid user templates, the new-game screen only shipped ones so far.
- **Deterministic** from (template, size, players, seed, generator version), so a seed shares a map; each stage has its own seed stream
  (`SeedStreams`). Output is a scenario `GameState` (ADR-064), saved as an ordinary map by `MapConverter.ToDocument`.

## The template (`MapTemplate`, format v8)
- Name, description, supported sizes, player range, max humans, underground or not, **heroes per player 1–8** (default 8; written into the
  scenario's rules; `MapGenerator.Build` / `EditorMapGenerator.Generate` and the editor's Generate tab may override it, the new-game screen
  does not; one limit for every player).
- **Week and month events** (ADR-084): `weekEvents` with `enabled`, `skipWeeks` (0–8), `weeklyFactions` / `monthlyFactions` (town ids or
  `neutral`; omitted = playable towns); [rules](towns-and-economy.md#week-and-month-events).
- **Zones**:
  - type: player start, AI-only start, treasure, junction (= treasure, only affects validator warnings);
  - owner slot (start zones), relative size, level;
  - **towns**: player and neutral (min / max), allowed neutral town types (empty = any playable; filtering by town group is post-1.0, ADR-072,
    RMG-11); a start town takes its player's pick; start zones may list pre-built buildings and dwellings by tier;
  - **mines**: minimum per resource, guarding (`Auto`, `Always`, `Never`), extra resource piles;
  - **treasure**: value bands with a count each (e.g. 500–3000 ×9, 3000–6000 ×6, 10000–15000 ×1);
  - **monster strength**: none, weak, average, strong; **obstacles**: none, sparse, normal, dense; allowed terrains (empty = the level's
    default pool);
  - `mirror` (below); `graphX` / `graphY` (template editor drawing only).
- **Connections**:
  - kind: **normal** (one-tile passage guarded by `GuardValue` army power), **wide** (five tiles, unguarded), **gate** (linked two-way gates
    near the border, guarded on both ends) or **portal** (ADR-112: linked portals anywhere in the two zones, any levels, guarded on both ends;
    the zones need not touch). Gates and portals are the ways across levels;
  - **passages**: 1–8, spread along the border;
  - **guard** (ADR-112, RMG-11, MAP-20; `connectionGuard` for the template, `guard` per connection, normal connections only): `Monsters`
    (default), `Garrison` (a neutral garrison holding troops of the guard value; monsters where no tile can hold one), `BorderGuard` or
    `BorderGate` of a `colour` (red, green, blue, black; drawn when omitted), with that colour's [Keymaster's Tent](adventure-map.md#utilities-and-travel)
    in the `from` zone, guarded by the guard value (one tent per colour);
  - **chance** (RMG-13): 1–100 %, default 100, rolled from the seed; a dropped connection the zones need to stay connected is made anyway (in
    template order); the layout places zones as if it were there.
- **Roads** (whole template): on / off, road type (default dirt), which mines get one (default lumber and ore), gates (default on).
- **Teams** (RMG-6, ADR-070, ADR-117): an optional team per start zone (same number = allies), `teamsLocked`, `minTeams` / `maxTeams`
  (empty = any). The validator checks 2 ≤ fewest ≤ most ≤ players, fewest ≤ `minPlayers`, and that locked teams fit. Generated maps carry
  the counts; team rules: [victory-and-modes.md](victory-and-modes.md#game-options).
- **Mirrored templates** (RMG-9, ADR-117): `symmetry` `None`, `Vertical`, `Horizontal`, `Diagonal`, `RandomAxis` (drawn from the seed) or
  `HalfTurn` for **2 players**, `QuarterTurn` for **4 or 8** (8 = four a level, with the underground). Each zone names its image with
  `mirror`; mirrors form cycles of 1 (a middle zone) or the symmetry's order, of alike zones on one level; a start zone is never its own
  image; every connection's image must be a connection like it. The layout keeps the symmetry (Voronoi, symmetric centres, the grid copied
  tile by tile) and mirrored zones share their rolls, so content is the same on every side.
- **Validator** (`TemplateValidator`): errors for players, sizes, zones, start zones, connections, passages, empty levels, connectivity,
  teams; warnings for unfair start zones.

## Pipeline
| Stage | Class | Does |
|---|---|---|
| 1 | `ZoneLayouter` | Places zones from the connection graph (Voronoi with area balancing, or rectangles) |
| 2 | `TerrainPainter` | Terrain per zone, walls between zones, obstacle clumps |
| 2b | `SceneryPlacer` | Gives each clump back the ground around it and fills its tiles with fitting [scenery](adventure-map.md#scenery-and-rivers), larger first (MAP-25) |
| 3 | `ConnectionPlacer` | Passages, their guards (`GuardPicker`), gates and portals |
| 4 | `SettlementPlacer` | Player towns, neutral towns with garrisons, mines with guards, resource piles |
| 5 | `RoadPlacer` | Roads: towns of a zone, passages, gates, the nearest lumber mill and ore mine in zones with a town |
| 5b | `RiverPlacer` | About half the zones get a river from the foot of one wall to the farthest foot of another, look by terrain (MAP-26) |
| 6 | `TreasurePlacer` | Treasure by value band; valuable items guarded in Area mode |
| 7 | `ScenarioAssembler` | Players, owners, picked towns and heroes; empty slots stay neutral |
| 8 | `MapFairness` | Reachability and fairness (below) |

- **No free-roaming monsters** (RMG-3): monsters guard passages and gates (Classic), mines (their own guard) and treasure (Area, ADR-065).
- **Roads** (RMG-4): each zone's hub is its first town or its middle; the cheapest way over walkable ground, merging with roads already laid.
  Later objects keep off roads and passages.
- **Treasure** (RMG-1): each band gives its count of items worth a value drawn in the band.
  - One item in 3 (`TreasurePlacer.SiteOdds`) is a [map object](adventure-map.md#shared-object-rules) worth between half the value and the
    value, drawn by weight from `TreasureSites`: loot (Lean To 600 … Warrior's Tomb 4000), providers 1000–4000, buffs 500 (Stables 1000),
    power-ups 1500–3000 (Library 10000), Observatory 750, Factory 1500, Town Gate 3000 (once per map), Prison 5000, Hill Fort 7000, banks
    2500–4000 (Mansion 10000), abandoned mines 2500–3500 (gold 6000), Shrines 500 / 1500 / 2500. Most kinds once per zone, loot twice. Banks and abandoned mines fight for themselves; their guards (and a
    prison's hero) are rolled at game start. Deposits are placed like abandoned mines with an ordinary guard. Not placed: signs, map
    dwellings, Windows and Eyes of the Magi.
  - Otherwise below 4000: a resource pile (2 in 3) or a chest; from 4000 an artifact (1 in 2; Minor 4000, Major 8000, Relic 15000, a Relic
    once per map), else a chest or pile. Contents are fixed (resources at 1 gold, 100 lumber / ore, 200 rare; chests the value as gold, 500
    less as experience).
  - **Map marketplace** (ADR-067, TOWN-2): from the **4000** band, worth 4000; unowned, no flag.
  - Items worth more than 1000 get a guard of 40–60 % of the value × the zone's strength (weak 0.75, average 1, strong 1.5), half in start
    zones (RMG-2).
- **Guards** (`GuardPicker`): power = `count × creature value`, mapped to overlapping tier ranges (counts 6–60, 2–40, 1–25, 1–15, 1–10 for
  tiers 1–5); creatures from the playable towns and neutrals, never upgraded. **Zone strength** (`ZoneGuards`): mine guards weak 800 /
  average 2000 / strong 4500 (gold ×1.5, rare ×1.25); neutral town garrisons 1500 / 4000 / 9000 in two stacks.
- **Scenario** (RMG-5): `PlayerSetup(Slot, Name, IsHuman, TownType, Hero?, Team?, Tier1Dwelling?)` per player.
  - Factions are chosen before generation ("Random" drawn from the seed first), so each start zone is painted with its town's native terrains
    (RMG-7); an editor map without towns keeps the template's terrain.
  - Start towns: the default start plus the zone's `startBuildings` / `startDwellings`; without a tier-1 dwelling there, the player's pick
    (computer: random). Start dwellings open with a week of creatures.
  - The picked hero (else one of the town's) stands at the first town with 2–3 stacks of tier-1 and tier-2 creatures, 1000–1500 power.
  - Neutral towns keep the default start; an untaken slot keeps its town, neutral, garrisoned by the zone's strength.
  - Standard rules; complexity and fog from the new-game screen; named "template size×size #seed".
- **Fairness** (RMG-8, ADR-117, `MapFairness.Check`): every start town reaches every other, and every town, mine and treasure item is
  reachable from some start (at step costs, through gates, portals, guards, pickups, garrisons). Each start gets **mine distance** (nearest
  mine per resource, summed), **treasure value** (each item split between starts by 1 / distance²) and **guard power** (least on the way to
  the nearest rival). Teams are compared (a team sums its members), only among alike zones. A number more than **±15 %** off its group's
  average *and* beyond a slack (4 tiles, 1500 gold, 300 power) is a problem. No separate richness setting (RMG-12): bands and mine minimums
  decide.
- **Problems** go to `GeneratedMap.Problems`, never thrown. `MapGenerator.GenerateBest` retries with up to 9 sub-seeds and keeps the first
  clean map, else the one with the fewest problems, which the new-game screen shows.
- **Generator version** (`MapGenerator.Version`) is saved with the game record (`GenerationInfo`) and shown with the seed; another version
  warns that the seed may give another map.

## Editor
"Generate" (`EditorMapGenerator.Generate`, ADR-087) opens a new map (and undo history) like "New": a template (refused with errors), a size,
a seed, how many start slots get a player (default all; the rest neutral) and optionally a town per slot (else drawn; start zones are
painted native to it and keep that terrain if the town is changed later, RMG-7). Players are named by colour (Blue, Red, Yellow, Green,
Orange, Purple, Teal, Pink); start slots are human up to max humans, AI-only slots computer; template teams may be overridden (ADR-070).
The rest is the game's scenario assembly, so the map is playable as generated; its description records template, size, seed, attempt and
generator version, and the generator's problems and validator issues are shown. A hero may stand in its own town's entrance.

## Template editor
The map editor's **Templates** button (RMG-10, ADR-117) opens a full-screen, HotA-like editor: a zone graph, a top row (New, Open, Save,
add zones, size, seed, Generate) and an inspector for the template, a zone or a connection; terrains and neutral town types are edited in
the file. Core `TemplateEditorSession`: every change publishes `TemplateChanged` with the validator's issues; **no undo**.

## Scope
- **In 1.0**: mirrored generation; saving generated maps as map files as well as reproducing them from the seed.
- **Not in 1.0**: water zones / islands, boats (ADR-051), importing HotA templates.
- Open ❓: how army speed and native terrain feed into treasure and guard values.
