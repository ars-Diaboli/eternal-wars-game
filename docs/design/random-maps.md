# Random map generation (templates)

> Part of the [game design](overview.md). Modelled on the Heroes 3: Horn of the Abyss template system (ADR-023,
> ADR-034, ADR-064). Open items ❓: [open-questions.md](../project/open-questions.md#random-maps-phase-4).

**Status**: the template format (v5), its validator, three shipped templates (`duel`, `ffa-4` "Free for all", `2v2` "Two against two") and every
generator stage including the scenario assembly are in core (`EternalWars.Core.Generation`, entry point `MapGenerator`).
The editor's "generate from template" exists in core (`EditorMapGenerator`, see [Editor](#editor)). There is no
template editor, user template folder, fairness validation (decided, ADR-071) or game/editor UI yet; `sim layout` prints
generated maps, `sim game --template` plays AI-vs-AI games on them, and CI checks every shipped template with
`sim maps` (no generator problems, deterministic, start towns reachable from every player, AI play and replay work).

## Goals
- **Both the game and the editor generate maps.** The game's new-game screen picks a template, a map size, players,
  a seed and game options, then generates and starts. The editor generates into an **editable** map, so designers can
  hand-polish a generated map and save it.
- **Templates are authored in the editor** (template editor: a zone graph) and saved as `*.ewtemplate.json`. The game
  lists every template it can find, built-in and user/mod folders. Today only the built-in (embedded) templates load.
- **Template editor** (ADR-070, RMG-10; not built): works like the HotA template editor (zone graph, zone and connection
  properties); **no undo** (unlike `MapEditorSession`); "Generate" in the editor shows the result. User templates are saved in
  the user data folder and listed next to the shipped ones (ADR-073).
- **Generation is deterministic** from (template, size, players, seed, generator version), so a map can be shared by
  its seed. Every stage draws from its own named seed stream (`SeedStreams`).
- The generator outputs a scenario `GameState` (ADR-064); `MapConverter.ToDocument` saves it as an ordinary map.

## The template (`MapTemplate`, format v6)
- Name, description, supported map sizes, player-count range, max humans, whether an underground level is used, and the
  **maximum number of heroes per player, 1 to 8** (v5, ADR-068; default 8). The generator writes it into the scenario's rules; its
  callers (`MapGenerator.Build`, `EditorMapGenerator.Generate`) may override it with `maxHeroes`. There is no template editor or
  generator UI yet, so the setting is in the file and in code. Generated maps use one limit for every player.
- **Week and month events** (v6, ADR-084): `weekEvents` with `enabled`, `skipWeeks` (0–8) and `weeklyFactions` / `monthlyFactions`
  (town ids or `neutral`; omitted = the playable towns), written into the scenario's rules like a map's.
- **Zones**, each with:
  - type: player start, AI-only start, treasure, junction (junction behaves like treasure; it only affects validator
    warnings);
  - owner slot for start zones; relative size; level (surface or underground);
  - **towns**: player towns and neutral towns (min/max), and the town types allowed for neutral towns (empty = any
    playable; filtering by town *group*, as ADR-028 allows, is post-1.0, ADR-072, RMG-11). A start town takes the type its player picks; start zones may list pre-built buildings and
    dwellings by tier (v3);
  - **mines**: minimum mines per resource, how they are guarded (`Auto`, `Always`, `Never`), extra resource piles;
  - **treasure**: value bands with a count each (e.g. 500–3000 ×9, 3000–6000 ×6, 10000–15000 ×1);
  - **monster strength**: none, weak, average, strong;
  - **obstacles**: none, sparse, normal, dense; allowed terrains (empty = the level's default pool).
- **Connections** between zones:
  - kind: **normal** (a one-tile passage guarded by `GuardValue` army power), **wide** (five tiles, unguarded) or
    **gate** (linked two-way gates, guarded on both ends; the only way across levels);
  - **passages**: 1–8 per connection, spread along the border.
  - Planned (ADR-070, RMG-11 part; not built): a template may **enable portals** (monoliths, two ends each) so the generator
    links zones with them, and a connection's guard is a **monster guard or a garrison** ([adventure-map.md](adventure-map.md#utilities-and-travel)),
    per template.
  - Planned (ADR-071, not built): a connection may be guarded by a **border guard or border gate** of one of the four
    Keymaster colours, an option per connection ([adventure-map.md](adventure-map.md#utilities-and-travel), MAP-20); every
    connection has a **presence probability**, 100 % by default, so a template can make a connection optional (RMG-13).
- **Roads** (v2, whole template): on or off, the road type (default dirt), which mines get a road (default lumber and
  ore), whether gates do (default on).
- **Validator** (`TemplateValidator`): errors for players, sizes, zones, start zones, connections, passages, empty
  levels and connectivity; warnings for unfair start zones.
- **Teams** (v4, RMG-6): an optional team per start zone (same number = allies, none = free for all), and `teamsLocked`;
  players may change teams at setup unless locked. The connections decide where allies are; there are no team fairness
  checks. ADR-070 (not built): a game has **2 to 8 teams** (8 = free for all); a template sets team defaults and restrictions
  (fixed teams, allowed team counts), and the map editor may override them on a generated map.
- **Mirrored templates** (ADR-070, RMG-9; not in the format yet): only for **2, 4 or 8 players** (8 uses the underground). Two
  players: **axis symmetry**, the axis vertical, horizontal or diagonal; four or eight: **point symmetry** around the map centre.
  The template picks the symmetry, "random axis" allowed (ADR-073). Mirrored zones share their rolls.

## Pipeline

| Stage | Class | Does | Status |
|---|---|---|---|
| 1 | `ZoneLayouter` | Places zones on the grid from the connection graph (Voronoi with area balancing, or rectangles) | implemented |
| 2 | `TerrainPainter` | Terrain per zone, walls between zones, obstacle clumps | implemented |
| 2b | `SceneryPlacer` | Gives each obstacle clump back the ground around it and fills exactly its tiles with scenery for that ground, larger pieces first (ADR-079, MAP-25) | implemented |
| 3 | `ConnectionPlacer` | Cuts passages, places their guards (`GuardPicker`) and gates | implemented |
| 4 | `SettlementPlacer` | Player towns, neutral towns with garrisons, mines with guards, resource piles | implemented |
| 5 | `RoadPlacer` | Roads: towns of a zone, passages between zones, gates and the nearest lumber mill and ore mine in zones with a town | implemented |
| 5b | `RiverPlacer` | About half the zones get a river from the foot of one wall to the farthest foot of another, over free ground; its look from the terrain (ADR-079, MAP-26) | implemented |
| 6 | `TreasurePlacer` | Treasure by value band, valuable items guarded in Area mode | implemented |
| 7 | `ScenarioAssembler` | The scenario `GameState`: players, owners, picked town types and heroes; empty slots stay neutral | implemented |
| 8 | – | Validate: every start zone reaches the others, start zones are fair | partly (template warnings, reachability per zone; start towns reachable from every player in `sim maps`); fairness metrics decided, not built (ADR-071, RMG-8, below) |

- **No free-roaming monsters** for now (RMG-3): monsters guard passages and gates (Classic mode), mines (the mine's own
  guard) and treasure (Area mode, ADR-065).
- **Roads** (RMG-4): every zone has a hub, its first town or the middle of the zone; the cheapest way over walkable
  ground, merging with roads already laid. Objects placed later keep off roads and passages.
- **Treasure** (RMG-1): each band gives its count of items worth a value drawn in the band. Below 4000: a resource pile
  (2 in 3) or a chest; from 4000 an artifact (1 in 2; minor 4000, major 8000, relic 15000, a relic once per map), else a
  chest or pile. Contents are fixed by the generator (resources at 1 gold, 100 lumber/ore, 200 rare; chests the value as
  gold, 500 less as experience). Items worth more than 1000 get a guard of 40–60 % of the value × the zone's strength
  (weak 0.75, average 1, strong 1.5), half in start zones (RMG-2).
  **Map marketplace** (ADR-067): a treasure item like an artifact, placed only where a treasure draw is valuable
  enough (placeholder: from the 4000 band, with a value of 4000); it is guarded by the same rule (a guard from
  items worth more than 1000, scaled by the zone's strength), so the zone's guard strength and the treasure value decide whether
  it is guarded. It has no owner, so it needs no flag.
  **Map objects** (ADR-068): before the roll above, one item in 3 (`TreasurePlacer.SiteOdds`) is an object of the
  catalogue worth between half the drawn value and the value, drawn by weight (`TreasureSites`, placeholder values):
  loot (Lean To 600 … Warrior's Tomb 4000), resource providers (1000–4000), buffs (500, Stables 1000), power-ups (1500–3000,
  Library 10000), Observatory 750, Factory 1500, Town Gate 3000 (once per map), Prison 5000, Hill Fort 7000, creature
  banks (2500–4000, Mansion 10000) and abandoned mines (2500–3500, gold 6000). Most kinds appear once per zone, loot twice.
  The object is guarded like other treasure by its own value, except banks and abandoned mines, which fight for themselves;
  their guards (and a prison's hero) are rolled when the game starts. Signs and map dwellings are not placed.
  Second catalogue (ADR-070, not built): deposits are placed separately, like abandoned mines; Windows and Eyes of the Magi are
  never placed by the generator.
- **Scenario** (RMG-5): `PlayerSetup(Slot, Name, IsHuman, TownType, Hero?, Team?, Tier1Dwelling?)` per player.
  - The factions are chosen before generation ("Random" is drawn from the seed first) and are an input of the
    generator, so each start zone is painted with a mix of its town's native terrains (RMG-7). A map generated in the
    editor without towns keeps the template's terrain.
  - Start towns: the default start plus the start zone's `startBuildings` and `startDwellings` (by tier); without a
    tier-1 dwelling there, the one the player picked at setup (a computer player gets a random one). Start dwellings
    open with a week of creatures.
  - The picked hero (else one of the town's heroes) stands at the first town with 2–3 stacks of the town's tier-1 and
    tier-2 creatures, 1000–1500 power.
  - Neutral towns keep the default start; a slot nobody takes keeps its town, neutral, garrisoned by the zone's strength.
  - Standard rules; complexity and fog from the new-game screen; the map is named "template size×size #seed".
- **Fairness** (ADR-071, RMG-8; not built): start zones are compared by distance to the nearest
  mines, treasure value and guard power on the way; they must agree within **±15 %**. A map that fails is retried with the next
  sub-seed (like other problems, below), then reported. There is no separate "richness" setting (RMG-12, dropped): treasure bands
  and mine minimums decide how rich a zone is.
- **Problems**: `MapGenerator.GenerateBest` retries with up to 9 sub-seeds and keeps the first clean map, else the one
  with the fewest problems, which the new-game screen shows.
- **Generator version**: `MapGenerator.Version` is saved with the game record (`GenerationInfo`) and shown with the seed;
  another version warns that the seed may give another map. Version 3 places the map objects of ADR-068.

- **Guards** (`GuardPicker`): army power is `count × creature value`. A power maps to overlapping tier ranges
  (counts 6–60, 2–40, 1–25, 1–15, 1–10 for tiers 1–5), so one power can be a few stacks of a high tier or many of a
  lower one. Creatures come from the playable towns and neutrals, never upgraded.
- **Zone strength** (`ZoneGuards`, placeholders): mine guards weak 800 / average 2000 / strong 4500 (gold ×1.5, rare
  ×1.25); neutral town garrisons 1500 / 4000 / 9000 in two stacks.
- Anything a stage cannot place is listed in `GeneratedMap.Problems`, not thrown (and retried, see above).

## Editor
"Generate from template" (`EditorMapGenerator.Generate`, core done, UI planned) works like "New": it opens a new map
(and a new undo history) instead of editing the open one.
- Input: a template (refused if it has errors), one of its sizes, a seed, how many start slots get a player (default
  all; the rest keep their towns, neutral) and optionally a town per slot (else drawn from the seed). Start zones are
  painted native to the drawn or picked town; a town changed later in the editor keeps that terrain (RMG-7).
- Players are named by colour (Blue, Red, Yellow, Green, Orange, Purple, Teal, Pink). Player-start slots are human up
  to the template's max humans, AI-only slots are computer players; teams and locked teams come from the template. The
  editor may then override the template's teams (ADR-070).
- Everything else is the game's scenario assembly: start towns with their buildings and dwellings, start heroes at the
  town entrances with their armies, garrisons, standard rules. The map is playable as generated.
- The map's description records the template, size, seed, attempt and generator version.
- The result carries the generator's problems (if every attempt had some) and the map validator's issues for the
  editor to show. A hero may stand in the entrance of its own town (the validator and hero placement accept it).

## Scope
- **In 1.0**: mirrored generation (2, 4 or 8 players, axis or point symmetry, [above](#the-template-maptemplate-format-v6), ADR-070);
  saving generated maps as ordinary map files as well as reproducing them from the seed.
- **Not in 1.0**: water zones/islands, boats (ADR-051), importing HotA templates.
- Open ❓: how army speed and native terrain feed into treasure and guard values.
