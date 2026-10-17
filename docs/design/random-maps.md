# Random map generation (templates)

> Part of the [game design](overview.md). Modelled on the Heroes 3: Horn of the Abyss template system (ADR-023,
> ADR-034, ADR-064). Open items ❓: [open-questions.md](../project/open-questions.md#random-maps-phase-4).

**Status**: the template format (v2), its validator, two shipped templates (`duel`, `ffa-4` "Free for all") and every
generator stage including the scenario assembly are in core (`EternalWars.Core.Generation`, entry point `MapGenerator`).
There is no template editor, user template folder, whole-map validation or game/editor UI yet; `sim layout` prints
generated maps.

## Goals
- **Both the game and the editor generate maps.** The game's new-game screen picks a template, a map size, players,
  a seed and game options, then generates and starts. The editor generates into an **editable** map, so designers can
  hand-polish a generated map and save it.
- **Templates are authored in the editor** (template editor: a zone graph) and saved as `*.ewtemplate.json`. The game
  lists every template it can find, built-in and user/mod folders. Today only the built-in (embedded) templates load.
- **Generation is deterministic** from (template, size, players, seed, generator version), so a map can be shared by
  its seed. Every stage draws from its own named seed stream (`SeedStreams`).
- The generator outputs a scenario `GameState` (ADR-064); `MapConverter.ToDocument` saves it as an ordinary map.

## The template (`MapTemplate`, format v2)
- Name, description, supported map sizes, player-count range, max humans, whether an underground level is used.
- **Zones**, each with:
  - type: player start, AI-only start, treasure, junction (junction behaves like treasure; it only affects validator
    warnings);
  - owner slot for start zones; relative size; level (surface or underground);
  - **towns**: player towns and neutral towns (min/max), and the town types allowed for neutral towns (empty = any
    playable). A start town takes the type its player picks;
  - **mines**: minimum mines per resource, how they are guarded (`Auto`, `Always`, `Never`), extra resource piles;
  - **treasure**: value bands with a count each (e.g. 500–3000 ×9, 3000–6000 ×6, 10000–15000 ×1);
  - **monster strength**: none, weak, average, strong;
  - **obstacles**: none, sparse, normal, dense; allowed terrains (empty = the level's default pool).
- **Connections** between zones:
  - kind: **normal** (a one-tile passage guarded by `GuardValue` army power), **wide** (five tiles, unguarded) or
    **gate** (linked two-way gates, guarded on both ends; the only way across levels);
  - **passages**: 1–8 per connection, spread along the border.
  - Planned ❓: border guards, optional or randomly present connections.
- **Roads** (v2, whole template): on or off, the road type (default dirt), which mines get a road (default lumber and
  ore), whether gates do (default on).
- **Validator** (`TemplateValidator`): errors for players, sizes, zones, start zones, connections, passages, empty
  levels and connectivity; warnings for unfair start zones.
- Not in the format yet ❓: teams, mirrored zones.

## Pipeline

| Stage | Class | Does | Status |
|---|---|---|---|
| 1 | `ZoneLayouter` | Places zones on the grid from the connection graph (Voronoi with area balancing, or rectangles) | implemented |
| 2 | `TerrainPainter` | Terrain per zone, walls between zones, obstacles | implemented |
| 3 | `ConnectionPlacer` | Cuts passages, places their guards (`GuardPicker`) and gates | implemented |
| 4 | `SettlementPlacer` | Player towns, neutral towns with garrisons, mines with guards, resource piles | implemented |
| 5 | `RoadPlacer` | Roads: towns of a zone, passages between zones, gates and the nearest lumber mill and ore mine in zones with a town | implemented |
| 6 | `TreasurePlacer` | Treasure by value band, valuable items guarded in Area mode | implemented |
| 7 | `ScenarioAssembler` | The scenario `GameState`: players, owners, picked town types and heroes; empty slots stay neutral | implemented |
| 8 | – | Validate: every start zone reaches the others, start zones are fair | partly (template warnings, reachability per zone) ❓ RMG-8 |

- **No free-roaming monsters** for now (RMG-3): monsters guard passages and gates (Classic mode), mines (the mine's own
  guard) and treasure (Area mode, ADR-065).
- **Roads** (RMG-4): every zone has a hub, its first town or the middle of the zone; the cheapest way over walkable
  ground, merging with roads already laid. Objects placed later keep off roads and passages.
- **Treasure** (RMG-1): each band gives its count of items worth a value drawn in the band. Below 4000: a resource pile
  (2 in 3) or a chest; from 4000 an artifact (1 in 2; minor 4000, major 8000, relic 15000, a relic once per map), else a
  chest or pile. Contents are fixed by the generator (resources at 1 gold, 100 lumber/ore, 200 rare; chests the value as
  gold, 500 less as experience). Items worth more than 1000 get a guard of 40–60 % of the value × the zone's strength
  (weak 0.75, average 1, strong 1.5), half in start zones (RMG-2).
- **Scenario** (RMG-5): `PlayerSetup(Slot, Name, IsHuman, TownType, Hero?, Team?)` per player. A player's start towns
  take the picked type; the picked hero (else one of the town's heroes) stands at the first town with one stack of its
  preferred creature worth about 1000 (placeholder). A slot nobody takes keeps its towns, neutral.

- **Guards** (`GuardPicker`): army power is `count × creature value`. A power maps to overlapping tier ranges
  (counts 6–60, 2–40, 1–25, 1–15, 1–10 for tiers 1–5), so one power can be a few stacks of a high tier or many of a
  lower one. Creatures come from the playable towns and neutrals, never upgraded.
- **Zone strength** (`ZoneGuards`, placeholders): mine guards weak 800 / average 2000 / strong 4500 (gold ×1.5, rare
  ×1.25); neutral town garrisons 1500 / 4000 / 9000 in two stacks.
- Anything a stage cannot place is listed in `GeneratedMap.Problems`, not thrown; what to do with them is open (RMG-5).

## Scope
- **In 1.0**: mirrored/symmetric generation (a template marks zones as mirrored, point or axis symmetry) ❓; saving
  generated maps as ordinary map files as well as reproducing them from the seed.
- **Not in 1.0**: water zones/islands, boats (ADR-051), importing HotA templates.
- Open ❓: how army speed and native terrain feed into treasure and guard values.
