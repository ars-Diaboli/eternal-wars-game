# Random map generation (templates)

> Part of the [game design](overview.md). Modelled on the Heroes 3: Horn of the Abyss template system (ADR-023, ADR-034, ADR-064).
> Everything here is built unless tagged *(planned)*; numbers are placeholders. Open items ❓:
> [open-questions.md](../../memory-bank/open-questions.md).

Core `MapGenerator` (`EternalWars.Core.Generation`, version 8). Shipped templates in `src/Core/data/templates` (`duel`, `ffa-4`, `2v2`,
`mirror-duel`, `ffa-8`, and the tournaments `twin-vales-small`, `twin-vales`, `twin-vales-large`); CI checks each with `sim maps` (no
problems, deterministic, starts reachable (a tournament's apart), no map validator error, AI play and replay work).

## Goals
- **Game and editor both generate**: the new-game screen (template, size, players, seed, options; ADR-086) and the editor's
  [Generate](#editor) tab, into an **editable** map (ADR-087).
- **Templates** (`*.ewtemplate.json`) come from the [template editor](#template-editor), saved to `user://templates` by default; the editor lists
  shipped and valid user templates, the new-game screen only shipped ones so far.
- **Deterministic** from (template, size, players, seed, generator version), so a seed shares a map; each stage has its own seed stream
  (`SeedStreams`). Output is a scenario `GameState` (ADR-064), saved as an ordinary map by `MapConverter.ToDocument`.

## The template (`MapTemplate`, format v10)
- Name, description, supported sizes, player range, max humans, underground or not, **heroes per player 1–8** (default 8; written into the
  scenario's rules; `MapGenerator.Build` / `EditorMapGenerator.Generate`, the editor's Generate panel and the new-game screen may override it;
  one limit for every player).
- **Week and month events** (ADR-084): `weekEvents` with `enabled`, `skipWeeks` (0–8), `weeklyFactions` / `monthlyFactions` (town ids or
  `neutral`; omitted = playable towns); [rules](towns-and-economy.md#week-and-month-events).
- **Zones**:
  - type: player start, AI-only start, treasure, junction (= treasure, only affects validator warnings);
  - owner slot (start zones), relative size, level;
  - **towns**: player and neutral (min / max), allowed neutral town types (empty = any playable; filtering by town group is post-1.0, ADR-072,
    RMG-11); a start town takes its player's pick; start zones may list pre-built buildings and dwellings by tier;
  - **mines**: minimum per resource, guarding (`Auto`, `Always`, `Never`), extra resource piles;
  - **treasure**: value bands with a count each (e.g. 500–3000 ×9, 3000–6000 ×6, 10000–15000 ×1);
  - **obelisks** (format v9, ADR-140): 0–25, or unset; the unset zones share what is left of the map's 25, split evenly (the first zones
    one more), so by default a map gets 25. None on maps under 24×24 (no Grail fits);
  - **monster strength**: none, weak, average, strong; **obstacles**: none, sparse, normal, dense (4, 10, 20 clumps per 1000 tiles, each
    tile weighed by its terrain: forest floor 250 %, highlands 175 %, rough, swamp and rocky ground 150 %, grass, snow and subterranean
    125 %, dirt, wasteland and cursed lands 100 %, lava 75 %, sand, beach and ice 50 %); allowed terrains (empty = the level's default pool);
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
- **Tournament templates** (format v10, ADR-366): `tournament` makes every map of the template a
  [tournament](victory-and-modes.md#game-modes). The zone graph is in two parts, one per player: a player's part is the zones the
  connections reach from its start zone. The validator wants exactly two players who are not allies, every zone in one part, and no
  connection, gate or portal between the parts (it names the way it finds); the whole graph need not be connected, and a part may be a lone
  start zone. A hero limit other than 1 is ignored, with a warning. A border guard's colour gets a Keymaster's Tent in each part that has
  one. The generator refuses a setup of other than two players, or of two allies; the template batch does not ask its players to reach
  each other, and the map validator does not warn that the parts touch (they do across the wall between them). The shipped ones are the
  mirrored **Twin Vales** at 72 (Small: a start and a vale behind it), 96 (a start, a vale with a town, strongly held heights) and 128
  (Large: a start between a meadow and a fen, heights beyond both): guards from weak at the start to strong at the far end, for a
  tournament of two weeks to a month or more.
- **Validator** (`TemplateValidator`): errors for players, sizes, zones, start zones, connections, passages, empty levels, connectivity,
  teams; warnings for unfair start zones.

## Pipeline
| Stage | Class | Does |
|---|---|---|
| 1 | `ZoneLayouter` | Places zones from the connection graph (Voronoi with area balancing, or rectangles) |
| 2 | `TerrainPainter` | Terrain per zone, walls between zones, obstacle clumps (more and larger on wooded and broken ground, `SceneryDensity`) |
| 2b | `SceneryPlacer` | Gives each clump back the ground around it and fills its tiles with fitting [scenery](adventure-map.md#scenery-and-rivers): a kind by weight (trees and rocks most), its largest piece that fits (MAP-25, ADR-157) |
| 3 | `ConnectionPlacer` | Passages, their guards (`GuardPicker`), gates and portals |
| 4 | `SettlementPlacer` | Player towns, neutral towns with garrisons, mines with guards, resource piles |
| 5 | `RoadPlacer` | Roads: towns of a zone, passages, gates, the nearest lumber mill and ore mine in zones with a town |
| 5b | `RiverPlacer` | About half the zones get a river from the foot of one wall to the farthest foot of another, look by terrain (MAP-26) |
| 6 | `TreasurePlacer` | Treasure by value band; valuable items guarded in Area mode; then the obelisks, unguarded (ADR-140) |
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
- **Guards** (`GuardPicker`): power = `count × Fight Value`, mapped to overlapping tier ranges (counts 6–60, 2–40, 1–25, 1–15, 1–10 for
  tiers 1–5); creatures from the playable towns and neutrals, never upgraded. **Zone strength** (`ZoneGuards`): mine guards weak 800 /
  average 2000 / strong 4500 (gold ×1.5, rare ×1.25); neutral town garrisons 1500 / 4000 / 9000 in two stacks.
- **Scenario** (RMG-5): `PlayerSetup(Slot, Name, IsHuman, TownType, Hero?, Team?, Tier1Dwelling?)` per player.
  - Factions are chosen before generation ("Random" drawn from the seed first), so each start zone is painted with its town's native terrains
    (RMG-7); an editor map without towns keeps the template's terrain.
  - Start towns: the default start plus the zone's `startBuildings` / `startDwellings`; without a tier-1 dwelling there, the player's pick
    (the new-game screen offers it to a human player with a picked town; computer: random). Start dwellings open with a week of creatures.
  - The picked hero (else one of the town's) stands at the first town with 2–3 stacks of tier-1 and tier-2 creatures, 1000–1500 power.
  - Neutral towns keep the default start; an untaken slot keeps its town, neutral, garrisoned by the zone's strength.
  - Each player's start zone (the template zone of its start town) is written into the map as its start zone (MP-4, ADR-197), for the
    simultaneous first week; an untaken slot's zone is nobody's. In a tournament it is the player's whole part.
  - Standard rules (a tournament template's: `rules.tournament` and one hero a player, whatever the caller asks); difficulty and fog from the new-game screen; named "template size×size #seed".
- **Fairness** (RMG-8, ADR-117, `MapFairness.Check`): every start town reaches every other, and every town, mine and treasure item is
  reachable from some start (at step costs, through gates, portals, guards, pickups, garrisons). In a tournament neither start town reaches
  the other or anything in the other's part, and a start has no rival to measure guard power to (0). Each start gets **mine distance** (nearest
  mine per resource, summed), **treasure value** (each item split between starts by 1 / distance²) and **guard power** (least on the way to
  the nearest rival). Teams are compared (a team sums its members), only among alike zones. A number more than **±15 %** off its group's
  average *and* beyond a slack (4 tiles, 1500 gold, 300 power) is a problem. No separate richness setting (RMG-12): bands and mine minimums
  decide.
- **Problems** go to `GeneratedMap.Problems`, never thrown. `MapGenerator.GenerateBest` retries with up to 9 sub-seeds and keeps the first
  clean map, else the one with the fewest problems, which the new-game screen shows.
- **Generator version** (`MapGenerator.Version`) is saved with the game record (`GenerationInfo`) and shown with the seed; another version
  warns that the seed may give another map.

## Relief
(ADR-181, ADR-182.) The last pass (`ReliefPainter`), once the passages and scenery have settled the terrain,
gives every tile corner its terrain's height ([the table and limits](presentation.md#art-direction)), blended over about 3 tiles where
terrains meet, plus gentle noise from the seed: ±0.25 cell widths, none on shallow water and ice, dunes of ±0.15 in rows on sand (each zone
draws their direction). It draws only from its own streams (`heights` per level, `dunes` per zone), so nothing else on the map depends on
it. The ground under each multi-tile object's footprint is then levelled to its entrance's height; roads and rivers follow the ground.

## Editor
**Generate** (the Generate panel, `EditorMapGenerator.Generate`, ADR-087) opens a new map (and undo history) like "New": a template (refused with errors), a size,
a seed, how many start slots get a player (default all; the rest neutral) and optionally a town per slot (else drawn; start zones are
painted native to it and keep that terrain if the town is changed later, RMG-7). Players are named by colour (Blue, Red, Yellow, Green,
Orange, Purple, Teal, Pink); start slots are human up to max humans, AI-only slots computer; template teams may be overridden (ADR-070).
The rest is the game's scenario assembly, so the map is playable as generated; its description records template, size, seed, attempt and
generator version, and the generator's problems and validator issues are shown. A hero may stand in its own town's entrance.

## Template editor
The map editor's **Template** tab (RMG-10, ADR-117, EDIT-3) is a HotA-like editor: a zone graph, a top row (the template's name, New,
Open, Shipped, Save, Save as, add zones, add a zone from a preset, size, seed, Generate), beside the graph (with its tools over it) the
Preview and Seeds tabs, an inspector for the template, a zone, several zones or a connection, and under them the validator's issues and the
template's undo history. Core `TemplateEditorSession`: every change publishes `TemplateChanged` with the validator's issues and is one named
undo step (EDIT-2, ADR-268); dragging a zone (or a selection) is one step. Ctrl+Z and Ctrl+Y undo and redo on the Template tab, and a
click in the History list jumps.

**The graph** (ADR-270):
- each zone is a **card**: a header in its type's colour (a start zone in its player's, darker for an AI start) with its icon and id
  ("(U)" underground), then its type with owner and team and its size, the monster strength, the towns (own and neutral) and the mines
  (G, W for lumber, O, C, M, S with counts); zoomed far out, only the header;
- each connection is a line (wide: thick, gate and portal: dashed) with a **label**: its kind, its guard when a normal connection's is not
  monsters, the guard value, passages above one and a chance below 100;
- **Snap to grid** (on by default): dragged zones, new zones and aligned rows land on a grid of half a graph unit, drawn under the cards;
  pasted zones move by whole grid steps; a drag starts a few pixels from the press, so a click moves nothing;
- **Align in a row / column** puts the selected zones (two or more) at their mean height or x; **Spread across / down** spaces three or
  more evenly between the outer two; **Arrange** places every zone on rings around the best connected one.

**Selection and copy** (ADR-271): a click selects a zone or a connection; Ctrl or Shift + click adds or drops a zone; a drag on empty
ground draws a marquee (with Ctrl or Shift it adds); dragging a selected zone moves the whole selection. A middle drag, or a right drag
on empty ground, pans. On the Template tab the Edit menu's Select all, Deselect, Cut, Copy, Paste, Duplicate and Delete (Ctrl+A,
Escape, Ctrl+X, C, V, D, Delete) work on the graph: copied zones take the connections between them; Paste puts them under the pointer and
Duplicate beside the originals, each zone with a new id (its name's stem and the next free number), a start zone with the lowest free
player slot (refused when too few are free), a mirror kept only inside the copy. The clipboard stays while another template is opened.

**The inspector** (ADR-273) edits every field of the file. The template: name, description, players, humans, teams, hero limit,
underground, symmetry, connection guard, sizes, roads (on, type, the mines and gates they reach) and week events (on, the weeks before
the first, the weekly and monthly factions, the playable towns when left alone). A zone: type, slot, team, towns, size, level, monsters,
mine guards, obstacles, mirror, mines, resource piles, obelisks, three treasure bands, and pickers for its terrains (the passable ones)
and neutral town types (the playable towns), where none ticked means any; a start zone also its start buildings besides the default
start (each at a level) and its start dwellings by tier. A connection: kind, guard value, passages, chance, guard and Keymaster colour.

**Zone presets** (ADR-273): + Preset adds a zone with a preset's settings; a zone's **Apply preset** gives it a preset's settings while
its id, player slot, team, mirror, level and graph place stay (a zone made a start zone takes the lowest free slot; refused when none is
free); **Save as preset…** keeps the zone's settings under a name of the user's, and Delete preset forgets one. The game ships Rich
treasure, Mine junction and Guarded start; the user's are files in `user://template-presets/` (`*.ewzone.json`), listed after them.

**Mirrors** (ADR-272): in a template with a symmetry, **Mirror** (over the graph, and in the zone inspector as Make mirror image) adds the
images of the selected zones (none of them mirrored yet): one across an axis or by a half turn, three by a quarter turn, each alike but for
its id, player slot (with no team) and graph place (mirrored around the middle of the other zones, moved down while it would cover a card), the zones
naming each other in a cycle. Each connection of a mirrored zone gets its images (to the other zone's image, or to that zone itself when
it is its own image). A dotted line joins a zone to its mirror. While zones form a closed cycle they stay **in sync**: a field changed on
one (type, size, level, towns, mines, treasure, monsters, terrains, start buildings, …) changes on its images in the same step, while the
player slot, team, mirror and graph place stay each zone's own; connecting two zones, changing a connection or removing it does the same to
the connection's images. **Break mirror** unlinks a cycle, so its zones are edited apart.

**Preview** (ADR-274): Generate makes the map off the main thread, every start slot a player and towns drawn from the seed
(`TemplateBatch.Generate`), and shows it in the Preview tab from above: the tiles as the minimap colours them, the borders between the
generator's zones dark and each zone's id over it (`ZonePicture`), a level picker for a template with underground. The template stays
open and editable; a newer Generate replaces one still running, and the preview says when the template changed since. **Open in map
editor** opens the map as a new map like the Generate panel's; opened again, the same seed is generated afresh.

**Generate many** (ADR-274), in the Seeds tab: the template at the top row's size for N seeds (20 by default, at most 200) from the top
row's seed up, off the main thread at most half the processors at once, cancellable (`TemplateBatch.RunAsync`). Each map is checked as
`sim maps` checks one, without the AI's play: the generator's problems, the same map twice from the seed, every player able to reach every
rival start town, the map validator's errors; and the fairness measures of its starts (mine distance, treasure, guard power), each as how
far apart the starts are (the largest less the smallest, in percent of their mean). The gallery shows a thumbnail per seed, in seed
order as they come in, with its verdict, attempt and the three imbalances (the failures in its tooltip); a click puts the map in the
preview and its seed in the top row. Over the gallery: how many passed, and the smallest, mean and largest of the attempts and of each
imbalance over the maps.

**Files** (ADR-269), as for maps: Open and Save as with the file dialog (the user's templates folder first), Save, recent templates
(Templates menu), an asterisk over unsaved work ("Template*" on the tab), the question before unsaved work is left, an autosave every
few minutes while there is unsaved work, and after a session that did not close the offer to restore it (at start, or when the
Template tab first shows if the map asked something). A shipped template opens read-only and Save keeps a copy. On the Template tab, File > Open, Save and Save as act on the template.

## Scope
- **In 1.0**: mirrored generation; saving generated maps as map files as well as reproducing them from the seed.
- **Not in 1.0**: water zones / islands, boats (ADR-051), importing HotA templates.
- Open ❓: how army speed and native terrain feed into treasure and guard values.
