# Decisions: Random maps and the map editor

**Covers**: Templates and the generator, scenery density, relief, the standalone editor and the template editor.

**Current rules**: [docs/design/random-maps.md](../../docs/design/random-maps.md), [docs/design/tools.md](../../docs/design/tools.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [015](#adr-015-editor-edits-are-undoable-commands-over-a-scenario-gamestate) | Editor edits are undoable commands over a scenario GameState | accepted |
| [023](#adr-023-standalone-editor-template-based-random-maps-in-both-game-and-editor) | Standalone editor; template-based random maps in both game and editor | accepted |
| [064](#adr-064-random-map-generation-project-layout-and-seed-streams-2026-10-17) | Random map generation: project layout and seed streams (2026-10-17) | accepted |
| [086](#adr-086-random-maps-on-the-new-game-screen-2026-10-28) | Random maps on the new-game screen (2026-10-28) | implemented |
| [087](#adr-087-generate-from-template-in-the-map-editor-2026-10-28) | "Generate from template" in the map editor (2026-10-28) | implemented |
| [117](#adr-117-phase-4-close-out-whole-map-fairness-teams-28-mirrored-templates-the-eight-player-template-the-template-editor-adr-064-adr-070-adr-071-adr-073-2026-11-03) | Phase 4 close-out: whole-map fairness, teams 2–8, mirrored templates, the eight-player template, the template editor (ADR-064, ADR-070, ADR-071, ADR-073, 2026-11-03) | implemented; "no undo" superseded by ADR-250 |
| [157](#adr-157-more-scenery-on-random-maps-denser-by-terrain-adr-077-2026-11-09) | More scenery on random maps, denser by terrain (ADR-077, 2026-11-09) | implemented |
| [181](#adr-181-adventure-map-relief-a-height-per-map-set-by-the-generator-and-the-editor-adr-011-adr-075-2026-11-12) | Adventure map relief: a height per map, set by the generator and the editor (ADR-011, ADR-075, 2026-11-12) | accepted |
| [182](#adr-182-map-37-answered-how-the-relief-is-stored-generated-and-edited-adr-075-adr-174-adr-181-2026-11-13) | MAP-37 answered: how the relief is stored, generated and edited (ADR-075, ADR-174, ADR-181, 2026-11-13) | implemented |
| [247](#adr-247-map-and-template-editor-overhaul-a-creator-app-workspace-planned-in-seven-steps-owner-adr-015-adr-023-adr-117-2026-11-20) | Map and template editor overhaul: a creator-app workspace, planned in seven steps (owner, ADR-015, ADR-023, ADR-117, 2026-11-20) | accepted |
| [250](#adr-250-edit-1--edit-12-answered-the-editor-plans-proposals-adopted-template-undo-scenery-rotation-new-map-content-owner-adr-117-adr-247-2026-11-20) | EDIT-1 … EDIT-12 answered: the editor plan's proposals adopted; template undo; scenery rotation; new map content (owner, ADR-117, ADR-247, 2026-11-20) | accepted |
| [251](#adr-251-map-resize-reframes-the-map-in-place-stamps-are-fragments-in-a-file-format-of-their-own-edit-8-adr-015-adr-250-2026-11-21) | Map resize reframes the map in place; stamps are fragments in a file format of their own (EDIT-8, ADR-015, ADR-250, 2026-11-21) | implemented |
| [252](#adr-252-scenery-has-an-orientation-stored-in-map-format-43-scatter-is-the-generators-scenery-choice-run-through-the-editor-session-edit-6-adr-079-adr-157-adr-174-adr-250-2026-11-21) | Scenery has an orientation, stored in map format 43; scatter is the generator's scenery choice run through the editor session (EDIT-6, ADR-079, ADR-157, ADR-174, ADR-250, 2026-11-21) | implemented |
| [253](#adr-253-the-object-browser-lists-entries-built-in-the-core-the-object-tool-places-its-choice-edit-1-adr-166-adr-250-2026-11-21) | The object browser lists entries built in the core; the Object tool places its choice (EDIT-1, ADR-166, ADR-250, 2026-11-21) | implemented |
| [254](#adr-254-thumbnails-are-renders-of-the-placeholder-model-cached-by-its-hash-the-inspector-has-six-parts-and-computed-jump-links-edit-1-adr-223-adr-245-adr-253-2026-11-21) | Thumbnails are renders of the placeholder model cached by its hash; the inspector has six parts and computed jump links (EDIT-1, ADR-223, ADR-245, ADR-253, 2026-11-21) | implemented |
| [255](#adr-255-multi-edit-writes-one-object-property-to-several-objects-of-one-kind-as-one-all-or-nothing-step-pickers-are-searchable-picture-grids-edit-1-adr-223-adr-245-adr-254-2026-11-21) | Multi-edit writes one object property to several objects of one kind as one all-or-nothing step; pickers are searchable picture grids (EDIT-1, ADR-223, ADR-245, ADR-254, 2026-11-21) | implemented |


### ADR-015: Editor edits are undoable commands over a scenario GameState
**Status**: accepted (2026-10-15)
The editor edits the same `GameState` type the game starts from. This avoids a second map model.
Each edit is an execute/undo closure pair recorded in `UndoHistory`. Commands publish domain events,
so editor views reuse game views.

### ADR-023: Standalone editor; template-based random maps in both game and editor
**Status**: accepted (2026-10-15); output refined by ADR-064 (a scenario `GameState`, saveable via `MapConverter.ToDocument`)
The map editor stays a separate app. Random maps are generated from **templates** (HotA-style zone
graphs: zones with towns, richness, treasure and monster strength, plus guarded connections). Templates
are authored in the editor's template editor. The game generates from any available template on its
new-game screen; the editor generates into an editable map. Generation lives in a new engine-free
project `EternalWars.Core.Generation`, is deterministic from its inputs, and outputs a plain
`MapDocument`, so generated and hand-made maps share one pipeline.

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

### ADR-086: Random maps on the new-game screen (2026-10-28)
**Status**: implemented (2026-10-28). Carries out the Phase 4 item "new-game screen picks template, size, players (town and hero per player),
seed and options, then generates" (ADR-064, RMG-5, RMG-7). Choices made here are marked *(choice)*.

- **One list** *(choice)*: the map dropdown lists the maps first, then each shipped template as "Random map: <name>" (`BuiltInTemplates.All`). A
  template shows its description, a **size** (its sizes; the middle one by default *(choice)*), the **number of players** (the template's
  minimum to its start slots; the first slots are taken, the others stay neutral as before), and the fog, complexity and seed rows the maps
  have. The seed is both the map's seed and the game's (as in `sim game --template`).
- **Players**, one row per taken slot: **human or computer**, **town** ("Random" or a playable town), **hero** ("Random" or one of the town's
  heroes, `StartSlots.Heroes`, by name; only "Random" while the town is random) and **team** (the start zones' teams by default; not editable
  when the template locks them). An AI-only start offers only "Computer".
- **Defaults** (`StartSlots.Of`) *(choice)*: the first slot a human may take is human, the others are computer players, as in HoMM3 (the editor's
  generator keeps making every player start human). Names by colour (`EditorMapGenerator.PlayerNames`).
- **New rule**: `ScenarioAssembler` refuses more humans than the template's `MaxHumans` (it was only respected by the editor's defaults).
  Refused setups (too many humans, the same hero twice, ...) show their message on the screen.
- **Generating**: on "Start", a frame after "Generating the map…" is shown. `MapGenerator.Build` already retries with sub-seeds. When even the
  best attempt has problems, the screen lists them and the button becomes **"Start anyway"** *(choice)*; any change of a setting drops that
  map.
- **Saves**: the game keeps the `GenerationInfo` (template, size, seed, attempt, generator version) of a generated game and writes it into every
  save and autosave (`GameSaves.Save` / `Autosave` take it). Loading shows the template, size and seed, and `VersionWarning` when the
  generator has changed since.
- **Layering**: `Godot.UI` does not reference `Core.Generation` (architecture table), so the screen takes plain records (`RandomMapChoice`,
  `RandomMapSlot`, `RandomMapRequest`, `RandomMapResult`), and the game app connects them to `MapGenerator.Build`.
- **Not yet**: the tier-1 dwelling pick (RMG-7; drawn at random as for a computer player), the hero limit override (ADR-068), user template
  folders, and the human or computer choice for hand-made maps (they keep the map's players). Town ids are shown as they are.
- Tests: `StartSlotsTests`. Smoke scenario `game-random-map` (Duel, Necropolis and its first hero, Start, K, end turn: the computer plays).

### ADR-087: "Generate from template" in the map editor (2026-10-28)
**Status**: implemented (2026-10-28). The UI for the editor's generator core (`EditorMapGenerator`, ADR-064 / ADR-083).

- **Where**: a "Generate" tab, the last one on the right-hand side. A "From template" button in the file row opens it. It offers the shipped
  templates (`BuiltInTemplates.All`) with the template's description, size (the middle one by default), seed (with "New seed"), the number of
  players (the template's minimum to its start slots), the **hero limit** (the template's by default; another value is passed as the
  `maxHeroes` override, ADR-068) and a **town per player** ("Random" = drawn from the seed; a picked town paints its start zone native).
- **Generate** works like "New": a new map and a new undo history replace the open one, unsaved changes included *(choice; the tab says to
  save first)*. The status line tells the template, size, seed and attempt, the generator's problems when every attempt had some, and how many
  validator issues there are (they are on the Validation tab). A refused request (a size or slot the template does not have) is shown under
  the button. The tab keeps its settings after generating, so the next seed is one click away.
- **Players** are `EditorMapGenerator.Players`: every player start is human (up to `MaxHumans`), which the designer can change on the
  Players tab. The game's new-game screen sets its players up itself (ADR-086).
- **Layering**: as for ADR-086, `GeneratePanel` (Godot.UI) takes plain records (`GeneratorTemplate`, `GenerateRequest`), and the editor app
  calls `EditorMapGenerator.Generate`.
- **Layout fix**: the right-hand tabs now start below the tool rows and are a little wider. A tool's second row (terrains, objects) no
  longer runs under them, and the sixth tab fits (seen in ADR-085).
- No core change. The generator core is covered by `EditorMapGeneratorTests`. Smoke scenario `editor-generate` (Sylvan for Blue, Two against
  two at 128, then the tab again).

### ADR-117: Phase 4 close-out: whole-map fairness, teams 2–8, mirrored templates, the eight-player template, the template editor (ADR-064, ADR-070, ADR-071, ADR-073, 2026-11-03)
**Status**: implemented (2026-11-03); the template editor's "no undo" superseded by ADR-250. Numbers marked *(proposal)* are the agent's, for the owner to confirm or change.

- **Whole-map check** (`MapFairness.Check`, generation stage 8, ADR-071 / RMG-8): after the treasure, every start town must reach every
  other start town, and every town, mine and treasure item must be reachable from some start. The walk uses the step costs (2 straight,
  3 diagonal), skips blocked cells, crosses gates and portals by their target, and stops at entrances that end a route, except guards,
  pickups and garrisons, which it walks through and counts. Each start is measured by three numbers: the **mine distance** (per resource,
  the distance to the nearest mine, summed), the **treasure value** (each item's recorded worth split between the starts by 1 / distance²)
  and the **guard power** (the least guard power on the way to the nearest rival; a Dijkstra on guard, then distance).
- **Teams, not players**, are compared: allies share camps on `2v2`, so a team's numbers are its members' sums (a player without a team is a
  team of one), and only teams whose zones are alike (size, level, towns, mines, piles, treasure bands, monsters) are compared. A team fails
  when a number is more than **±15 %** off its group's average and more than a slack off it *(proposal: 4 tiles, 1500 gold, 300 power, so
  small maps with small numbers are not refused for a step)*. A failure is a generator problem, so `GenerateBest` retries with the next
  sub-seed and reports the last one if every attempt fails. `sim fairness [--template] [--seeds 20] [--size] [--verbose]` prints the
  numbers. `MapGenerator.Version` 8.
- **Teams 2–8** (ADR-070): templates (format v8) and maps (format v28) carry `minTeams` / `maxTeams` (2–8, empty = any). The template
  validator checks 2 ≤ fewest ≤ most ≤ players and the fewest at most `MinPlayers`, and that locked teams fit; `ScenarioAssembler.CheckTeams`
  refuses a setup with another team count; `GameState.SetTeamCounts` before the game starts and `AssignTeams` refuses a change that breaks
  it. The map editor has "Teams from / to" in the map properties (undoable `SetTeamCounts`), the validator an error for locked default teams
  outside the counts (a warning when not locked); the new-game screen shows "— 2 teams" and refuses others. `2v2` is set to exactly 2.
- **Mirrored templates** (ADR-070, ADR-073, RMG-9): `symmetry` None, Vertical, Horizontal, Diagonal, RandomAxis (drawn from the seed),
  HalfTurn or QuarterTurn, and a `mirror` per zone naming its image. Axes and the half turn are for 2 players, the quarter turn for 4 or
  8 (8 = four per level). The validator checks that mirrors exist, are used once, stay on their level, form cycles of 1 or the symmetry's
  order with zones alike, that no start zone is its own image and that every connection's image is a connection like it. The layout
  keeps the zone centres symmetric (only the first zone of each cycle moves), then copies the grid from the first tile of each cycle of
  tiles. Mirrored zones and connections **share their rolls**: every stage seeds a zone by `RollZone` (the cycle's first zone) and walks its
  tiles in the source zone's mapped order (`ZoneLayout.Tiles`), passages are sorted in the first connection's frame, and treasure values
  are drawn before they are placed. The result is the same zone content on every side, though not tile for tile where walls and roads
  meet on the axis.
- **Templates**: `ffa-4` is now mirrored (quarter turn); new `mirror-duel` ("Mirror duel", random axis, 2 teams) and `ffa-8` ("Eight
  kingdoms": 8 players, underground, quarter turn with four starts a level, reserves in a ring, a surface and an underground hub joined by a
  5000 gate; sizes 128, 160, 192) *(proposal: zone sizes and treasure)*.
- **Template editor** (ADR-070, RMG-10): `TemplateEditorSession` in core (blank, load, the template's own settings, add / edit / rename /
  move / remove zones, connect / edit / disconnect, arrange the graph; each change publishes `TemplateChanged` with the validator's live
  issues; **no undo**, as decided). The graph places are stored in the template (`graphX`, `graphY`; old templates get an arranged graph).
  The map editor's "Templates" button opens `TemplateEditorPanel`: the zone graph (drag to move, right-drag to connect, drag the ground to
  pan, wheel to zoom, Delete), an inspector for the template, the zone or the connection, the issues list, New, Open (shipped and user
  templates), Save to `user://templates`, and Generate with a size and seed, which opens the map. The editor's Generate tab lists the user
  templates that validate next to the shipped ones; the game's new-game screen still lists only the shipped ones.
- **Tools**: `sim maps`'s reachability check now walks on from a gate's exit (it stopped at the exit before, which hid nothing on the old
  templates but failed on `ffa-8`); the auto input has `drag` / `rdrag` (`x1,y1,x2,y2`); smoke scenario `editor-template-editor`.
- **Results** (`sim fairness --seeds 6` on each template's sizes): fair at the first attempt in 4–6 of 6 seeds, after retries 6 of 6
  everywhere (duel, 2v2, ffa-4, Mirror duel, Eight kingdoms); `sim maps --days 3`: 15 maps of 5 templates, none failed; 1158 tests pass.
- **Not done**: the new-game screen's user templates; the AI on generated maps (the next step).

### ADR-157: More scenery on random maps, denser by terrain (ADR-077, 2026-11-09)
**Status**: implemented (2026-11-09). Random map generator version **9** (every generated map changes). Numbers are placeholders.

- **How many clumps** *(choice)*: the obstacle densities give 4 / 10 / 20 clumps per 1000 tiles (were 3 / 8 / 16), and each tile counts by
  its own terrain (`SceneryDensity.Clumps`): forest floor 250 %, highlands 175 %, rough, swamp and rocky ground 150 %, grass, snow and
  subterranean 125 %, dirt, wasteland and cursed lands 100 %, lava 75 %, sand, beach, ice and the rest 50 %. Mirrored zones keep their images,
  since their tiles and terrains are images too.
- **How large** *(choice)*: a clump aims at 1–6 tiles (was 2–6) plus its start tile's `SceneryDensity.ExtraSize`: forest floor 4, highlands
  2, rough, swamp, rocky ground, grass, snow and subterranean 1. The other clump rules stay: three tiles from the walls, one apart, dropped if
  it would cut off a part of the zone.
- **What fills them** *(choice)*: `SceneryPlacer` draws a kind among those with a fitting piece by `SceneryPlacer.Weight`, then that kind's
  largest fitting piece (before: the largest piece of any kind, which gave many castle walls). Trees 24 on forest floor, 10 on grass,
  highlands and snow, 6 elsewhere; rocks 16 on rough, highlands, rocky ground, lava, subterranean and snow, 6 elsewhere; dead trees 12 on
  wasteland, cursed lands and swamp, 4 elsewhere; mushrooms 6 underground and in swamps, 2 elsewhere; bushes and mounds 3; walls and ruins
  1; the rest 2. On six sample maps (duel and 2v2 at 96, seeds 1–3) scenery went from 289 to 795 pieces, trees and rocks first; walls from 74 to 7.
- **Treasure keeps room for its guard**: in a zone with monsters, every treasure item (not only map sites) is placed where a free cell by its
  entrance is left for the guard (`SettlementPlacer.Find`'s `accept`), since thicker scenery could leave none.
- Tests: `TerrainPainterTests.Obstacles_AreDenserOnWoodedAndBrokenGround_ThanOnSand`, `SceneryTests.TheGenerator_FillsAZone_MostlyWithItsTerrainsOwnScenery`.
- **Not done**: scenery art (the pieces are still placeholder boxes); scenery outside clumps (single trees along roads, forest edges).

### ADR-181: Adventure map relief: a height per map, set by the generator and the editor (ADR-011, ADR-075, 2026-11-12)
**Status**: accepted (the owner's note in `ideas.md`, 2026-11-12); not built. The points marked *(proposal)* were confirmed with MAP-37 in
ADR-182, which holds the details.
Changes ADR-075's "height follows the tile terrain": the height is now the map's own data, which terrain only suggests.

- **Variable height**: the adventure map's ground has a height of its own, kept in the map. The limits are ADR-075's: walkable ground lies
  between **0 and 4 cell widths**, water below, impassable rock above.
- **The generator prefers heights by terrain**:
  - **very low** (near the bottom limit): swamp, shallow water, ice;
  - **high**: rocky ground, lava, highlands;
  - **the rest**: placeholder heights in between *(proposal, in cell widths)*: beach 0.5, sand 0.75, grass, dirt and cursed lands 1,
    forest floor and subterranean 1.25, wasteland 1.5, snow 2, rough 2.5; the high ones 3–3.75; deep water −0.5; impassable rock 4.5–6.
- **Shape**: the height changes smoothly from one terrain to the next, never as a step. Shallow water and ice are **mostly flat**; sand has
  **shallow, wave-like dunes**; the others roll gently *(proposal: within ±0.25 of their height)*.
- **Editor**: the map editor gets tools that change the height, and they keep to the same limits.
- **Not changed**: the rules stay on the 2D grid (ADR-075). *(proposal)* Height does not change movement cost, sight or battles, so core
  rules never read it; the battlefield stays flat.
- **Needs**: a map format bump with a migration that gives older maps their terrains' heights (ADR-174); a generator pass and a generator
  version bump; the 3D map's mesh, and the things standing on it, at the ground's height. Built with the heightmapped terrain (roadmap,
  "Platform and art").

### ADR-182: MAP-37 answered: how the relief is stored, generated and edited (ADR-075, ADR-174, ADR-181, 2026-11-13)
**Status**: implemented (the owner: "use proposals", 2026-11-13; built in five steps, [plan](../../docs/papers/relief-plan.md)). "Heights from terrain"
for a selection is the Height tool's "From terrain" brush (the editor has no selection tool).
MAP-37 leaves the open questions. The heights are placeholders,
tuned once the look-dev spike shows them.

- **a. Looks only**: height changes no rule: not movement cost, sight, the AI, pathfinding or battles (the battlefield stays flat). Core
  rules never read it; it lives in the map so the generator, the editor, saves and the 3D view share one copy.
- **b. Rocks**: walkable rocky ground is high but inside the limit (3.5); impassable rock stands above it (4.5–6), as ADR-075's cliffs.
- **c. Heights by terrain** (cell widths): swamp, shallow water and ice 0–0.25; beach 0.5; sand 0.75; grass, dirt and cursed lands 1; forest
  floor and subterranean 1.25; wasteland 1.5; snow 2; rough 2.5; highlands 3; lava 3.25; rocky ground 3.5; deep water −0.5; impassable rock
  4.5–6. One table in core, used by the generator, the migration and the editor's "Heights from terrain".
- **d. Storage**: one height **per tile corner** (a grid one larger than the map each way, per level), in steps of **1/8 cell width**
  (whole numbers in the file), so neighbouring tiles always meet and slopes are smooth without extra rules. A map format version bump.
- **e. Generator**: a pass once the terrain is final (after the passages and scenery change it; then objects are levelled): each corner
  takes its terrain's height, blended over about **3 tiles** where terrains meet, plus gentle noise from the seed (±0.25; none on shallow
  water and ice; dunes of ±0.15 in rows on sand). Generator version 10; the noise draws only from its own seed streams.
- **f. Objects on a slope**: the ground under an object's footprint is levelled to its entrance's height, by the generator and when the
  editor places an object. Roads and rivers follow the ground; a river is not made to run downhill.
- **g. Editor tools**: raise, lower, level (to the height under the cursor) and smooth, with the terrain brush's radius and strength,
  undoable through `MapEditorSession`; "Heights from terrain" for a selection or the whole map. Painting terrain moves the height to the new
  terrain's only while **"Auto height"** is on (on by default).
- **h. Limits**: walkable terrains are clamped to 0–4, water below 0, impassable rock above 4; nothing more is enforced (a map maker may put
  swamp on a plateau) and the validator does not warn.
- **i. Underground**: the same limits and tools; no ceiling is drawn.
- **j. Older maps** (Ember March, the arenas, saved maps): when loaded they get their terrains' heights with the generator's blending but no
  noise (format v39, ADR-174: the upgrade step computes the height layer from the terrain), and can be reshaped in the editor
  afterwards. Maps built in code (`MapSketch`, Ember March) get the
  same heights from terrain when built.
- **Plan**: [archive/relief-plan.md](../../docs/papers/relief-plan.md).

### ADR-247: Map and template editor overhaul: a creator-app workspace, planned in seven steps (owner, ADR-015, ADR-023, ADR-117, 2026-11-20)
**Status**: accepted (the direction and the plan); nothing built. Plan, study and questions:
[editor-plan.md](../../docs/papers/editor-plan.md). EDIT-1 … EDIT-12 answered in ADR-250.

- **Owner**: the map editor and the template editor are to be overhauled to the quality, usability and style of a professional
  creator app, and in function on the level of the HoMM3 editor with HotA's additions and the HoMM5 editor.
- **Shape**: one workspace with a menu bar, toolbar, command registry and palette, resizable and collapsible regions (tool options and
  an object browser with pictures; the minimap, inspector and layers; problems, history and output) and a status bar; selection,
  move, copy and paste, stamps, area tools and map resize as undoable core edits; overlays, a fairness report and play-test from the
  editor; the template editor as a second workspace tab with undo, presets and a gallery of many seeds. Core keeps every edit
  (ADR-015); the app keeps composition and input mapping only.
- **Order**: seven steps, each leaving a working editor: decisions and a greybox; the shell (with files, autosave and recovery);
  selection and editing; browser and inspector; overlays, analysis and testing; the template workspace; polish.
- **Open**: EDIT-1 … EDIT-12 (layout, template undo, where the template editor lives, several maps, play-test, decoration off the
  grid, scripting, resize, fairness, import, map texts, map content); the plan follows their proposals until answered. New map content
  and format changes among them each get their own ADR.

### ADR-250: EDIT-1 … EDIT-12 answered: the editor plan's proposals adopted; template undo; scenery rotation; new map content (owner, ADR-117, ADR-247, 2026-11-20)
**Status**: accepted (the owner: "use proposals", 2026-11-20); nothing built. Supersedes ADR-117's "no undo" in the template editor. The
step-by-step build is in [editor-plan.md](../../docs/papers/editor-plan.md#implementation-plan); each map format change below gets its own
ADR when it is built.

- **EDIT-1 Layout**: fixed regions (left, right, bottom) with tabs, splitters and collapse; the layout kept per user. No free docking
  and no floating windows.
- **EDIT-2 Template undo**: every template change is one undo step with a name, shown in a History panel like the map's.
- **EDIT-3 Template editor's place**: a "Map" | "Template" workspace tab in the map editor app; both stay open, a generated map goes to
  the Map tab.
- **EDIT-4 Documents**: one map and one template at a time, with Recent; copy and paste between maps through the clipboard and stamps.
- **EDIT-5 Play-test**: a separate process of the game app with new launch arguments: the player, the others computer or off, reveal,
  start at a hero or tile, the day, and a cheat panel that exists only in play-tests.
- **EDIT-6 Decoration**: scenery can be rotated in 90° steps and mirrored, on the grid, stored in the map (a format bump); game objects
  keep their fixed facing; no free placement.
- **EDIT-7 Scripting**: no scripting language; the event tree (ADR-115, ADR-129) stays, and a node-graph view of it comes after the
  plan's step 6.
- **EDIT-8 Resize**: a nine-point anchor, crop to a selection and shift; objects that end up outside are listed, then removed in the
  same undo step; any size from 16 to 256 a side, square or not, the presets offered first.
- **EDIT-9 Fairness report**: per start, the days to the nearest town, mines and providers; the treasure value and guard strength within
  3 and 7 days; the resources around the start town. A warning when a start's number is more than 20 % from the mean *(placeholder)*.
  The same code serves the generator's fairness (ADR-117) where it fits.
- **EDIT-10 Import**: no import of HoMM3 or HotA maps or templates, for 1.0 and after.
- **EDIT-11 Map texts**: one language until the localisation decision (ADR-239); a "Texts" view lists all of a map's texts.
- **EDIT-12 Map content**: rumours for the tavern, per-player allowed factions on a fixed map, and placed objects named for events and
  quests join the map format; custom heroes per map with a name, biography and starting skills (the portrait from the roster).
  Per-player AI style comes later, with the AI's styles.

### ADR-251: Map resize reframes the map in place; stamps are fragments in a file format of their own (EDIT-8, ADR-015, ADR-250, 2026-11-21)
**Status**: implemented (plan step 2, tasks 9 and 10).

- **Resize in place**: `Resize`, `Crop` and `Shift` are one operation, a new frame round the map's content. The scenario keeps its
  `GameState` and its `AdventureMap` object: the map takes the reframed layers (`TakeFrom`) and `GameState.Reframe` moves every
  object and hero and rebuilds the indexes. So `AdventureMap.Width` and `Height` are no longer fixed for the object's life; they
  change only through the editor, before a game starts. A new scenario for every resize was rejected: the session, its undo history
  and every panel hold the scenario.
- **Undo**: the step keeps the map's layers before and after (a few megabytes at 256 × 256 on two levels) and the removals are the
  other editors' own commands in the same step, so an undo is exact.
- **What leaves**: an object with any cell outside, heroes and event cells outside, and a Grail nearer the new edge than placement
  allows. Places that event actions name move with the map and stay when they end up off it: the validator warns of one that is off the map.
- **New ground**: grass on the surface and subterranean below, at the terrain's height, as a new map has.
- **Views**: views of the map copy its size when bound, so the editor builds them again on `MapResized`; the game never sees one.
- **Stamps**: a stamp is a `MapFragment` in `*.ewstamp.json` with its own format version (1). Its objects are written as the map
  format writes them, and the file names that map format (`mapFormat`), so a later map format change can upgrade old stamps. Tiles the
  fragment does not have are spaces in the rows. No migration framework yet: there is one version.

### ADR-252: Scenery has an orientation, stored in map format 43; scatter is the generator's scenery choice run through the editor session (EDIT-6, ADR-079, ADR-157, ADR-174, ADR-250, 2026-11-21)
**Status**: implemented (plan step 2, tasks 7 and 8).

- **Orientation**: a piece of scenery lies mirrored left to right, then turned clockwise by 0 to 3 quarter turns (`Orientation`).
  Its cells are the definition's footprint so transformed, from its position as the top-left tile of the box. Only scenery has
  one: an object with an entrance keeps its facing, as EDIT-6 decided.
- **One shape to read**: `MapObject.Footprint` (and `KnownObject.Footprint`) is the shape as the object lies. Everything that asks
  where an object is (the cell index, movement, the fog's memory, the minimap, the view) reads it; the definition's footprint is
  the shape as drawn. A separate rotated definition per orientation was rejected: it would multiply the scenery table by eight.
- **Turning**: a quarter turn is round the middle of the piece's box (half an odd difference rounds towards 0, so four turns
  return it exactly); a mirroring stays in the box. The turned shapes must fit as placement demands, all pieces or none. The view
  is told with `MapObjectMoved`, also when the top-left tile stays.
- **Format 43**: `turns` (1 to 3) and `mirrored` on a `scenery` object, omitted when not set. Older files need no change to load;
  the step `OlderSceneryIsNotTurned` removes those two names from an older file's scenery, so nothing an older file happened to
  carry there turns a piece. A game's digest names the orientation only when there is one, so older replays keep their digests.
- **Stamps follow the map format**: a stamp written under an older map format has its objects upgraded by the map's own steps when
  it is read (ADR-251 promised this); the stamp format itself stays at 1.
- **Scatter**: `ScatterEditor` (Core.Generation) reuses `SceneryPlacer`'s choice (a kind by weight on the terrain, then the largest
  piece that fits) and places through `ObjectEditor.Place`, so it is ordinary undoable edits. The generator's own placement is
  unchanged, and scattered scenery is not turned. Density picks which free tiles are covered; pieces must lie on picked tiles.
- **Keep paths open** is a local rule: a cell is blocked only when the open ground among its eight neighbours stays one piece
  without it and every entrance beside it keeps open ground of that piece beside it. It is stricter than needed (it can leave a
  tile free that could have been blocked) and cheap; a whole-map reachability check per piece was rejected for its cost.
- **Randomness**: the draws come from `GameState.Random`, handed to the edit by `MapEditorSession.AsOneStep(description, random =>
  …)`. The step's undo restores the generator's state, and draws that led to no edit are taken back, so the same map and seed
  give the same scatter, also after an undo. A map file keeps no random state: the generator starts from the seed when a map is
  opened.
- **No key for the tool**: the plan's S is the camera's pan key in the editor, so Scatter is picked from the palette or the menu.
- **The tool scatters on release**, over all the tiles of the stroke: pieces of several tiles fit across the brush's dabs, and the
  result depends on the tiles alone, not on how the brush was dragged.

### ADR-253: The object browser lists entries built in the core; the Object tool places its choice (EDIT-1, ADR-166, ADR-250, 2026-11-21)
**Status**: implemented (plan step 3, tasks 1 and 2).

- **Entries, not definitions**: the browser lists `BrowserEntry`s. Most are one map object definition. A kind whose instances differ
  by one choice is listed once per choice, because that choice is what the user looks for: a monster and a dwelling per creature, an
  artifact pickup per artifact beside the "random" pickup of each tier. The hero is an entry without a definition. New definitions
  per creature or artifact were rejected: the map format and the catalog would grow for the browser's sake.
- **The category is a function of the kind** (`BrowserCatalog.CategoryOf`), not a column of the catalog: a new `MapObjectKind` without
  a category fails a test, as the other tables keyed by kind do (ADR-166). Groups, search words and the rule line are derived from
  what the definition already says (resource, colour, town, a bank's rewards), so the content tables carry nothing new.
- **Terrains** are the scenery table's; a thing made for no terrain suits every one. The terrain filter therefore narrows scenery only.
- **Search** is by word starts in the name and the search words, and inside the name; not the command palette's letters-in-order
  search, which over several hundred entries with search words would find nearly everything.
- **Placement in the core**: `EntryEditor` (`session.Placing`) places an entry through the existing editors, a dwelling or a pickup
  with its creature or artifact as one undo step, and `Check` gives the ghost's cells. The clicked tile is the top-left one, as
  before. Monsters from the browser are aggressive with their number left open; the Monster and Hero tools stay for their options.
- **One choice, shared**: `BrowserChoice` is the app's, so it outlives the map; the browser sets it, the Object tool places it and
  reports it, which fills Recent. Favourites and Recent are lists of entry ids in the editor's settings file (`BrowserLists`); an id
  the catalog no longer knows is kept and not shown.
- **Layout**: the browser stands under the tool palette in the left region's one tab, as the plan draws it; the list scrolls by
  itself, and when the tool's options are long the whole tab scrolls. Entries are buttons, so the smoke scripts press them by name.
  Stamps stand above the categories, where they are in reach without scrolling.
- **The hover preview is a card** at the bottom of the browser, not a tooltip: it also shows the chosen entry, and the footprint needs
  a drawing. Pictures are the placeholder tiles until thumbnails exist (plan step 3, task 3).
- **AutoInput**: a button whose centre a scroll box has scrolled out of view is no longer "visible" to an aimed click; the click
  would land on whatever is drawn there, and the scenario would pass without doing what it says.

### ADR-254: Thumbnails are renders of the placeholder model cached by its hash; the inspector has six parts and computed jump links (EDIT-1, ADR-223, ADR-245, ADR-253, 2026-11-21)
**Status**: implemented (plan step 3, tasks 3 and 4).

- **One description of the model**: there are no model files yet; an object is drawn as blocks on its footprint, with extra shapes
  for the quest objects. That drawing moved out of `MapObjectsView` into `PlaceholderModel` (a list of `ModelPart`s), which the
  map view draws and the thumbnails are rendered from, so the two cannot drift apart. When art lands, the model file takes the
  parts' place and its hash theirs.
- **Rendered once, cached by hash**: `ThumbnailRenderer` renders a model in a hidden `SubViewport` with a world of its own (six at
  a time, a picture after two frames) and writes a 96 pixel PNG named by the hash of the parts plus a version of the renderer's
  look. Definitions drawn alike share a file; a changed model or look gets a new name, so nothing has to be invalidated. The
  folder is `user://cache/thumbs/`; a file that cannot be read or written only costs a render. Pictures are asked for when an
  entry is listed, not for the whole catalog at start.
- **Which picture an entry shows**: its icon where one exists (a town's crest, a resource, the hero's icon), else the rendered
  model, else the lettered placeholder. Monsters and named artifacts keep the placeholder: every monster's model is the same
  block, and a tile with the name's letters tells them apart until portraits and artifact icons exist (ADR-245).
- **Six parts, not a form per kind**: the inspector's rows go into identity, contents, guard, messages, links and events
  (`InspectorPart`). The per-type forms of ADR-223 stay as they are and say which part their rows belong to (`InspectorForm.In`);
  a part without rows is not shown. Which parts are closed is the editor's setting, not the map's.
- **Jump links are computed, not stored**: `MapLinks.Of` reads a thing's links from the scenario each time the inspector fills:
  the gate links, what a quest's conditions name, the cells of a box's event, and the reverse (what runs an event that names the
  thing). Nothing new is saved and no format changes; a link whose target is gone is left out, and the validator reports it.
- **A jump selects**: the link's object or hero becomes the selection and the inspected thing, and the view goes to it; a cell
  only moves the view, since the Events tab is where cells are edited.

### ADR-255: Multi-edit writes one object property to several objects of one kind as one all-or-nothing step; pickers are searchable picture grids (EDIT-1, ADR-223, ADR-245, ADR-254, 2026-11-21)
**Status**: implemented (plan step 3, tasks 5 and 6).

- **One kind is one object type**: the inspector edits objects together when they are all of one type (all monsters, all mines,
  all towns), whatever their definitions; the fields are the type's. A selection of several types lists them by kind and edits
  nothing. Heroes are edited one at a time.
- **A field is an object property**: `ObjectProperty<TObject, TValue>` reads one value from an object and writes it through the
  editor that owns it, passing the object's own values of the fields that editor sets together with it (a monster's creature,
  count, disposition and never-flees), so a multi-edit of one field never copies the others from the first object. The fields
  edited together are listed once in `ObjectProperties` (core, tested); fields that only make sense for one object (names,
  buildings, contents, troops) are not properties and show for one object only.
- **One step, all or nothing**: `MapEditorSession.EditAll` runs the edit on each object inside one undo step; when one object refuses,
  what the others did is undone within the step and the refusal returned, so a multi-edit never leaves the map half changed.
  Values the objects already have record nothing.
- **"—" for values that differ**: `SharedValue<T>` gives the common value or that they differ (lists compare by their items). A
  drop-down gets a "—" item, a picker and a text field show "—", a number box shows "—" until a number is typed (applied on Enter
  or when the box is left, whatever number the box holds underneath), a tick box ends its text in "(—)". A field shown as "—" is
  set only by choosing a value, never by leaving it, and setting one field never writes another field's hidden value: a mine's
  guard is two fields (the creature keeps each mine's count; the count is set on the guarded mines), and chests' fixed values,
  which are one value, are typed after "Fixed values" is ticked.
- **A refused or failed multi-edit leaves nothing**: an edit that is refused or throws is taken back within the step; a town
  retyped (one id, a new object) stays selected.
- **Pickers are grids under the row, not windows**: `PicturePicker` opens a search field and the items by group, each with its
  picture, inside the inspector (the panel refills after every edit, which closes it). The pictures are the lettered placeholders
  until portraits and icons exist (ADR-245); `PickerItems` is the one place that builds the lists, so art lands in one place.
- **Thumbnails only where they tell entries apart** (refines ADR-254): the browser shows a rendered model only when no other entry
  is drawn alike; entries that share a model (every monster, the dwellings, the mines, the banks) keep their lettered tiles, which
  differ. A model that could not be rendered is not asked for again in that run, and a picture is written beside its file and then
  moved into place.
- **Jump links name only what a quest can mean**: a quest's monster or town link is offered only when the id is a monster or town on
  the map and not the quest object itself; a jump to an object or hero switches to the Select tool, so the next click selects.
