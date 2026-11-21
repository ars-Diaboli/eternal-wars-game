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
| [256](#adr-256-the-map-settings-are-one-window-with-tabs-over-the-editor-each-tab-one-of-the-settings-panels-edit-1-adr-250-adr-255-2026-11-21) | The map settings are one window with tabs over the editor, each tab one of the settings panels (EDIT-1, ADR-250, ADR-255, 2026-11-21) | implemented |
| [257](#adr-257-the-map-content-rumours-a-players-factions-object-names-and-custom-heroes-in-one-format-bump-edit-12-adr-250-adr-256-2026-11-21) | The map content: rumours, a player's factions, object names and custom heroes, in one format bump (EDIT-12, ADR-250, ADR-256, 2026-11-21) | implemented |
| [258](#adr-258-the-maps-texts-one-core-list-with-a-setter-each-and-the-texts-tab-edit-11-adr-256-adr-257-2026-11-21) | The map's texts: one core list with a setter each, and the Texts tab (EDIT-11, ADR-256, ADR-257, 2026-11-21) | implemented |
| [259](#adr-259-the-new-map-wizard-and-the-empty-state-adr-250-adr-087-2026-11-21) | The new-map wizard and the empty state (ADR-250, ADR-087, 2026-11-21) | implemented |
| [260](#adr-260-the-editors-camera-framing-and-bookmarks-adr-022-adr-250-2026-11-21) | The editor's camera: framing and bookmarks (ADR-022, ADR-250, 2026-11-21) | implemented |
| [261](#adr-261-the-editors-minimap-and-go-to-adr-260-2026-11-21) | The editor's minimap and Go to (ADR-260, 2026-11-21) | implemented |
| [262](#adr-262-the-editors-layers-adr-250-2026-11-21) | The editor's layers (ADR-250, 2026-11-21) | implemented |
| [263](#adr-263-the-map-analysis-in-the-core-adr-117-adr-250-2026-11-21) | The map analysis in the core (ADR-117, ADR-250, 2026-11-21) | implemented |
| [264](#adr-264-the-editors-overlays-adr-262-adr-263-2026-11-21) | The editor's overlays (ADR-262, ADR-263, 2026-11-21) | implemented |
| [265](#adr-265-fix-its-for-the-maps-problems-adr-250-2026-11-21) | Fix-its for the map's problems (ADR-250, 2026-11-21) | implemented |
| [266](#adr-266-the-fairness-report-and-the-exports-adr-263-2026-11-21) | The fairness report and the exports (ADR-263, 2026-11-21) | implemented |
| [267](#adr-267-play-test-from-the-editor-adr-250-2026-11-21) | Play-test from the editor (ADR-250, 2026-11-21) | implemented |
| [268](#adr-268-template-undo-edit-2-adr-250-2026-11-21) | Template undo (EDIT-2, ADR-250, 2026-11-21) | implemented |
| [269](#adr-269-template-files-adr-250-2026-11-21) | Template files (ADR-250, 2026-11-21) | implemented |
| [270](#adr-270-the-template-graph-zone-cards-labelled-connections-grid-align-and-spread-adr-117-adr-250-2026-11-21) | The template graph: zone cards, labelled connections, grid, align and spread (ADR-117, ADR-250, 2026-11-21) | implemented |
| [271](#adr-271-the-template-graphs-selection-and-clipboard-adr-268-adr-270-2026-11-21) | The template graph's selection and clipboard (ADR-268, ADR-270, 2026-11-21) | implemented |
| [272](#adr-272-template-mirror-tools-adr-117-adr-271-2026-11-21) | Template mirror tools (ADR-117, ADR-271, 2026-11-21) | implemented |
| [273](#adr-273-the-template-inspectors-pickers-and-zone-presets-adr-117-adr-250-2026-11-21) | The template inspector's pickers and zone presets (ADR-117, ADR-250, 2026-11-21) | implemented |
| [274](#adr-274-the-template-preview-and-generate-many-adr-087-adr-117-2026-11-21) | The template preview and Generate many (ADR-087, ADR-117, 2026-11-21) | implemented |
| [275](#adr-275-the-shortcut-editor-adr-250-2026-11-21) | The shortcut editor (ADR-250, 2026-11-21) | implemented |
| [276](#adr-276-the-editors-f1-help-made-from-the-design-documents-adr-250-2026-11-21) | The editor's F1 help, made from the design documents (ADR-250, 2026-11-21) | implemented |
| [277](#adr-277-the-editors-ui-scale-and-contrast-adr-250-2026-11-21) | The editor's UI scale and contrast (ADR-250, 2026-11-21) | implemented |


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

### ADR-256: The map settings are one window with tabs over the editor, each tab one of the settings panels (EDIT-1, ADR-250, ADR-255, 2026-11-21)
**Status**: implemented (plan step 3, task 7).

- **A window, not a region**: the map's settings are set once in a while, not beside the map, so they leave the right region (which
  keeps the inspector, the events and "From template") for `MapSettingsWindow`, an overlay like Preferences, opened with Map → Map
  settings or F7; the Map menu's other entries open it at a tab, and it opens again at the tab shown last.
- **Tabs**: General (name, description, difficulty, hero limit, arena switch), Players (slots, teams locked, team counts), Victory and
  loss, Bans, Week events. The arena switch is a general setting: it changes what the map is, and the Victory and loss tab only greys
  its rows while it is on. Rumours and the texts list come with their own tasks (EDIT-11, EDIT-12).
- **Changes apply at once**: each control is an undoable session edit, as the panels were; there is no OK or Cancel. The window has its
  own Undo and Redo (and Ctrl+Z, Ctrl+Y outside a text field), since the editor takes no other input while it is open.
- **The panels stay the units**: each tab is a panel bound to the session (`GeneralSettingsPanel`, `PlayersPanel`, `ConditionsPanel`,
  `BansPanel`, `WeekEventsPanel`), so the window holds no rule and no state of the map.

### ADR-257: The map content: rumours, a player's factions, object names and custom heroes, in one format bump (EDIT-12, ADR-250, ADR-256, 2026-11-21)
**Status**: implemented (plan step 3, task 8).

- **One format bump, no migration step**: map format 44 only adds fields (`rules.rumours`, `factions` on a player slot, `name` on any
  object, `biography` and `portrait` on a hero). A town's `name` keeps its key and meaning, now read for every object, so older files read
  as they are (ADR-174's rule for added fields); the v43 sample became the v44 one with every new field.
- **Rumours** are a list in the rules, at most 100, each one line of at most 500 characters. Every tavern, a town's and the map's, tells one a week: the
  map's rumours in order by the week of the game, starting over after the last. No draw, so no randomness changes and every player hears
  the same one. Edited in the map settings window's Rumours tab, one a line, as one undo step.
- **A player's factions** (a fixed map's slot): a list of playable towns, kept in their order; null and every town both mean any, and an
  empty list is refused. The new-game screen's crest picker offers only these; a choice outside them, or Random, is drawn among them when
  the game starts, in the same draw as before (the same result when every town is allowed). They only narrow a choice: a fixed town of
  another faction stays the player's, which Problems warns of. Edited in the Players tab.
- **Object names**: every object may have a name of its own, at most 60 characters and not only digits (a number where an object is asked
  for is its id); a town's name is that name, its faction's when it has none. The editor refuses a name that does not fit, the loader too. References stay ids (events, quests, conditions), so a rename breaks nothing; what names an object shows its name (the event lines,
  the conditions' texts and lists, the inspector's links), and the quest form takes a monster's or hero's name for the id. Names are
  unique, ignoring case: Problems reports as an error a given name that another object's name, a default town name too, matches. Copies keep the name, so a pasted named object is reported
  until renamed. The format reads a name through a public `MapObject.WithName`, for objects not on a map yet: `Core.Data` uses only the
  domain's public members.
- **Custom heroes**: any map hero may have its own name (at most 40 characters, not only digits), skills (one at a time, keeping the group limit), a
  biography (at most 2000 characters, shown on the hero screen and in its full information) and a roster hero's portrait (null: its own
  roster entry's). Portraits are not drawn yet (ADR-245's placeholders); the editor's picker shows the roster hero's. A placeholder that
  becomes a roster hero, or a map hero a campaign's carried hero takes over, drops the biography and the portrait.
- **Not here**: the Texts tab (EDIT-11) lists these texts with the others.

### ADR-258: The map's texts: one core list with a setter each, and the Texts tab (EDIT-11, ADR-256, ADR-257, 2026-11-21)
**Status**: implemented (plan step 3, task 9).

- **One list in the core**: `MapTexts.List` gives every text of a map, each with an id that names its place (a rumour's number, an event
  action's path, so an edit that adds or moves things may give it another text), its
  group, where it is and whether it may have line breaks: the map's name and description, the players' names, the names authors gave
  objects and every town's name, the heroes' names and biographies, sign and Pandora's Box messages, an author's sphinx riddle (its
  question and three answers), the rumours, and the map events' texts (messages, questions and answers, dialogue lines, speakers that are
  not roster heroes, end-of-game texts, a quest stage's proposal, progress and completion). Event names, variable names and content ids
  are not texts: things are found by them.
- **A setter for each, through the panels' edits**: `MapTextEditor.Set(id, text)` (the session's `Texts`) makes the edit the text's own
  panel makes, so its rules hold (a name's length, a rumour on one line, a riddle's different answers) and the change is one undo step;
  texts that must not be blank are refused blank (the map's, a player's and a hero's names, an event's message, question, answer and dialogue
  line, a quest stage's proposal). An id the map no longer has is refused, and so is one whose text is not the one the caller expected
  (the Texts tab passes the text it showed); a text the map would keep as it is, trimmed where the panel trims, records nothing.
- **The test that keeps it whole**: every string the map file writes is either a text, which a test writes with the editor and then finds
  in the list and changes through its setter (one undo step), or named in the test as not a text. A new string field fails the test
  until it is one or the other.
- **The Texts tab** of the map settings window lists them by group with a search box; each is edited in place, applied on Enter or when
  the field is left, with the shown-value guard of the inspector's fields (ADR-257) so a field freed after an undo does not apply again.
- **One language**: a text has one value until the game's localisation decision (ADR-239); the list is where a translation will start.

### ADR-259: The new-map wizard and the empty state (ADR-250, ADR-087, 2026-11-21)
**Status**: implemented (plan step 3, task 10).

- **One way to start a map**: File → New map (Ctrl+N) opens the wizard, which replaces New, New (2 levels) and From template. Three steps:
  the size (the presets 48 to 256, or any size from 16 to 256 a side, square or not), the levels (the surface, or with the underground)
  and the ground (a terrain a hero can walk on, not the underground's); the players (1 to 8) and their kind, a person or the computer by
  default; a template, or none. Everything is picked from buttons; the players' count is a number box.
- **The ends**: without a template, a blank map (`BlankScenario.Create(NewMapSettings)`): the ground, the underground below, the players
  named by colour, each with a hero (the first two in opposite corners, then the other corners, then the middles of the sides). With one,
  the Generate tab, set to the template at its size nearest the chosen (`MapSizes.Nearest`, the smaller of two as near) and to the chosen
  players within the template's range; the designer presses Generate. The template decides the ground and the levels. The kinds go with the
  request: a player the wizard made a computer one is generated as one (`EditorMapGenerator`'s `humans`); a person's slot is still a player
  start within the template's human limit.
- **The empty state**: the editor still starts on a blank map, with a card over it ("Start with a template, or paint a terrain.") that offers
  the wizard, Open and the sample maps. The map under it takes clicks at once; the card goes at the first edit, with its close button or
  when another map opens, and does not come back until the next start.
- **Sample maps** (`SampleMaps`): the shipped hand-made maps (Ember March, Two Towns, the three arenas), opened through the map format as a
  new, unsaved map. The Help menu's sample maps and their own files come with step 6.

### ADR-260: The editor's camera: framing and bookmarks (ADR-022, ADR-250, 2026-11-21)
**Status**: implemented (plan step 4, task 1).

- **A camera of its own**: `EditorCamera` (Godot.Shared) beside the game's `StrategyCamera` (ADR-022), which stays as it is. It keeps the
  game camera's tilt (15° to 30° from vertical, more tilted closer, up to 45° of turn) and adds a straight-down view; it goes out to
  400 tiles' distance, so the largest map fits. The wheel zooms by a share of the distance (a factor of 1.15 a notch at the default preference), so a
  notch feels the same close up and over a 256 × 256 map. W, A, S, D and Q, E stay the camera's keys.
- **The middle button drags the map**: the ground point under the press stays under the pointer (on the plane of the camera's focus),
  followed through the input before the panels see it, so the drag goes on over them.
- **Framing** (`Frame`): an area fits into the part of the window the map shows in (`EditorWorkspace.MapView`, between the panels) with
  two tiles of room, and its middle comes under that part's centre. Straight down the distance is worked out; tilted, the near edge
  shows larger than the far one, so the nearest distance at which all four corners show is searched. Home frames the shown level;
  F frames the selection
  (`MapSelection.Cells`: its tiles and every cell of its objects, heroes and events), on the shown level if it has cells there, else on
  the level of its first, at the ground's mean height there.
- **Bookmarks** (`CameraBookmarks`, Godot.UI): Ctrl+1 … 9 store the view (focus, distance, turn, top-down) with the shown level, 1 … 9
  return to it; View > Bookmarks lists them. They belong to the map open, go when another opens and are not saved: a view is a
  designer's place in a session, not part of the map.

### ADR-261: The editor's minimap and Go to (ADR-260, 2026-11-21)
**Status**: implemented (plan step 4, task 2).

- **The game's minimap, reused**: `MinimapPanel` (Godot.UI) holds the HUD's `MinimapWidget` under the right region's tabs (a footer of the
  region, `EditorWorkspace.SetFooter`, so it shows whichever tab is open and moves nothing above it), fitted into a 200-pixel square. The
  widget gains what the editor needs and the game does not use: a drag reports every tile it passes (`DragMoves`), the view's frame is
  drawn from the ground under the map view's corners (`ViewFrame`, through `EditorCamera.GroundAt`), and it rebuilds after every edit
  (`Refresh` on `UndoHistoryChanged`), since moves, owners and roads publish no event it listens to. A click or a drag moves the view so
  the tile is in the middle of the map view (`EditorCamera.Centre`); the panel is made again for every map and every new size.
- **The other level ghosted**: on a map of two levels, a box under the minimap mixes 35 % of the other level's colour into every cell
  (`GhostOtherLevel`), so the underground can be laid out against the surface. Only the minimap shows it; the 3D view shows one level.
- **Go to** (Ctrl+G, View > Go to…): `GoToBox` searches as you type through `MapGoTo.Find` (Core.Domain). Two numbers, with a third for
  the level, are a tile (the shown level when none is typed), offered first; any text is also searched among the heroes, the named
  objects (a town always has a name) and the events with a cell, ranked as the command palette ranks commands (`CommandSearch`); an
  empty text lists them all, at most 50. A result is a `MapLink`, so going there is the inspector's jump: the view shows it and a hero or
  an object becomes the selection.

### ADR-262: The editor's layers (ADR-250, 2026-11-21)
**Status**: implemented (plan step 4, task 3).

- **Layers in the core**: `MapLayers` (`MapEditorSession.Layers`, Core.Domain) holds which kinds (`MapLayer`: terrain, roads, rivers,
  objects, scenery, monsters, heroes, events, start zones) are hidden and which locked, and publishes `MapLayersChanged`. They belong to
  the session: no undo step, not saved, back to all shown and unlocked with every map opened.
- **Hidden or locked cannot be selected or erased**: the selection finds nothing of such a kind (`At`, `In`, `OfKind`, `Everything`),
  `Set` and `Add` leave it out, and a change of the layers lets go of what the selection held of it (the selection is trimmed before
  `MapLayersChanged` is published); the eraser leaves it, under the brush and over the selection. So Delete, Move, Rotate, Mirror and
  Cut, which work on the selected things, never touch it either. A copy takes no road or river while their layer is hidden or locked,
  and a paste or a duplicate does not paint over them. A hidden kind counts as locked: what cannot be seen is not changed by a box drawn
  over it. Tiles are no kind and stay selectable.
- **Jumps**: a jump link or Go to on a thing of such a kind shows it but neither selects nor inspects it, and the selection stays.
- **What locks**: only the kinds something selects or erases (`MapLayers.Lockable`: roads, rivers, objects, scenery, monsters, heroes,
  events). The terrain and the start zones only hide. The brushes (terrain, roads, rivers, start zones) and the placing tools are
  picked on purpose and work whatever the layers say; what a paste places is not selected when its kind is locked.
- **The views**: `MapViewContext.Hidden` tells the shared map views what to leave out (the ground, roads, rivers, objects by kind,
  heroes, event cells, start zones); the game never sets it. The objects are drawn again only when an object layer changes. The grid
  is drawn on the ground, so it hides with the terrain. The Layers tab (`LayersPanel`, Godot.UI) in the right region has a Show box per
  kind, a Locked box per lockable kind, the grid's box, and "Show and unlock all".

### ADR-263: The map analysis in the core (ADR-117, ADR-250, 2026-11-21)
**Status**: implemented (plan step 4, task 4; EDIT-9).

- **`MapAnalysis`** (Core.Domain, `Analysis`) measures any `GameState`, a scenario in the editor or a game, and changes nothing:
  - **Reach**: the days from a tile to every other, by the game's step costs (`PathFinder.CostsFor`, the cost flood the AI uses) for a
    scout of the player: a hero without skill or artifact whose army is one creature of the start town's faction, so it is at home on
    that faction's terrain (100 a step there, the terrain's own cost elsewhere) as a start hero is; a day is 1500 points
    (`MovementAllowance.NoArmyBase`) whatever the army, so factions compare. Unlike a route in the game, the scout's goes on past
    guards (fought), pickups and other visits (`passThrough`), as a hero that fights and takes walks on; heroes on the map stand in its
    way, and fog is ignored. Days are a route's cost over a day's movement, rounded up: the game lets a day's last step run over, so
    this is an upper bound.
  - **Guard strength** of a cell: the army power (count × fight value) of the monsters whose zone it is in and of one standing on it.
    `GuardPower` is the strength of a monster or a garrison; an open count is the middle of its tier's rolled range, a random monster
    the mean of the creatures it may become. The guards of mines, creature banks and dwellings are not counted (the generator's
    measure leaves them out too).
  - **Treasure value** in gold: resource piles (an open amount as the middle of its range), chests (fixed gold or experience, the more;
    else the rolls' average), artifacts on the ground, Pandora's boxes (resources, experience and artifacts; not creatures or stats). Resources and
    artifacts are worth what the generator gives them (`GoldValue`, `ArtifactValue`, which `TreasurePlacer` reads from here).
  - **Fairness** per start (a player's first town's entrance, else its first hero and its army's faction): days to the nearest town
    (not its own), to the nearest mine of each resource the map has and to the nearest resource provider; treasure value and guard
    strength of what is reached within 3 and within 7 days (a garrison that lets the player through is no guard to it); how many mines
    of each resource lie within 3 days. A warning names each number where a start is more than 20 % from the starts' mean
    *(placeholder)*, and for days also more than a day, as days are whole numbers; or where it reaches none and another start does.
- **Shared where the numbers mean the same**: the generator's `MapFairness` takes its guard power from `GuardPower` (the generator sets
  every count, so its numbers do not change) and its treasure values from the same gold values. Its own measures stay: they compare
  zones the template makes alike, by walking distance in tiles, which is not days of a hero's movement. `sim fairness --verbose` also
  prints the analysis of the game of each seed with every slot taken (built as the new-game screen builds it, which may be another
  attempt than the single map measured; its time is not counted in a seed's).
- The editor's fairness report and overlays (plan step 4) are views of these numbers.

### ADR-264: The editor's overlays (ADR-262, ADR-263, 2026-11-21)
**Status**: implemented (plan step 4, task 5).

- **One overlay at a time**, or none, chosen in the Overlays tab of the right region (View > Overlays; View > Overlay lists them too):
  - **Passability**: an object's entrance, a blocked cell (impassable terrain or an object's blocked cell), a cell a monster guards.
  - **Owners**: the cells of everything that can have an owner and of every hero in the owner's colour; an unowned one neutral.
  - **Guard strength**: the army power of the monsters guarding each cell (`MapAnalysis.GuardStrength`) in five heat bands that split
    the level's weakest to strongest guard evenly on a log scale (one band when all are alike); the legend gives each band's powers.
  - **Reach**: the days from a chosen start to every tile (`MapAnalysis.Reach`), a band per day up to 7 and one for more; a tile no
    route reaches is not tinted. The start is a player's (`MapAnalysis.Starts`: its first town, else its first hero) or the first
    selected cell, for the first player.
  - **Zones**: a generated map's zones, laid out again from its description: it names the template, size, seed and attempt, and only
    the layout stage runs (`GeneratedZones`, Core.Generation, over the shipped and the user's templates). The legend names each zone with
    its type and whose start it is, and warns when another generator version made the map. A map that was not generated, whose template
    is not found or whose size changed shows its start zones instead, and the legend says why. The zones move with the map's content
    when it is shifted, resized or cropped in the same session; a move made in an earlier session and saved cannot be told.
  - **Height contours**: lines where the ground crosses every half cell of height, thicker at whole cells, traced over the tile
    corners (marching squares).
- Overlays show the whole map whatever layers are hidden (ADR-262): they are for checking it.
- **The numbers are the core's** (`MapOverlays`, Core.Domain/Analysis): per level, the tinted tiles or the lines, a legend of bands
  (`OverlayTint` says what a band means; the views choose the colour) and a note. The overlay is computed again after every edit and
  when the level changes; a generated map's zones are laid out once per description and size.
- **Drawn as one multimesh per overlay** (`OverlayView`, Godot.Shared): a see-through plate on each tinted tile, or a thin bar along
  each line, over the ground. The legend lies over the map view's lower left corner while an overlay is shown. Overlays are views: they
  change nothing, add no undo step and are not saved; every map opened starts with none.

### ADR-265: Fix-its for the map's problems (ADR-250, 2026-11-21)
**Status**: implemented (plan step 4, task 6).

- **A `MapIssue` may carry a fix** (`MapFix`) where the answer is clear; none is guessed where there are several:
  - an unlinked two-way gate: link it with the one gate of its kind that points at it, else with the one other unlinked gate of its kind
    when the map has exactly two that nothing points at (`LinkGatesFix`; a subterranean gate never pairs with a portal);
  - a two-way link that is not mutual, when the gate pointed at is of its kind, linked to nothing and pointed at by this gate alone:
    link that one back;
  - a one-way entrance without an exit, or an exit no entrance uses, when the map has exactly one of each: point the one at the other
    (`GateExitFix`);
  - a town, mine or garrison that belongs to a player the map does not have: make it neutral (`OwnerFix`);
  - a player without a starting hero or town, when exactly one neutral town has its entrance in that player's start zone: give it to
    the player (`OwnerFix`; the issue then points at the town).
- **A fix is data**: the ids of what it changes and what it found there (an owner fix records the owner it saw), because the Problems
  panel validates a copy of the map on a task; object ids are the live map's. `MapEditorSession.Fix` validates the live map again and
  runs the fix only when an issue there still carries the same fix: through the editors, as one undo step named "Fix: …". Otherwise it
  refuses (`InvalidTarget`, nothing changed).
- **The Problems panel** names a problem's fix in its row; the Fix button runs the selected problem's fix (it is off while the map is
  being checked) and the editor's messages say what was done or why not. The map is checked again after it, refused or not.

### ADR-266: The fairness report and the exports (ADR-263, 2026-11-21)
**Status**: implemented (plan step 4, task 7).

- **The Fairness tab** of the right region (View > Fairness, Map > Fairness) shows `MapAnalysis.Fairness` (ADR-263): a row per start
  with its player (numbered as the warnings name it, and its name), its tile, the days to the nearest town, mine of each resource the map has and resource provider (— where none is
  reached), the treasure and guards within 3 and 7 days, and the mines within 3 days (the start town's surroundings); under it the
  warnings and what the columns mean. Selecting a row shows the start on the map. The numbers are computed again a moment after the
  edits stop, and only while the tab shows; a hidden tab computes when it is shown.
- **The layout is the core's** (`MapReport`, Core.Domain/Analysis): `Table` gives the headers and the rows' texts that the panel and the
  report share, `Markdown` the whole report.
- **File > Export report (Markdown)** writes the report of the map as it is now: its name and size, its problems (validated again,
  errors first, each with its kind, tile and fix), the fairness table, what the columns mean, and the warnings.
- **File > Export map picture (PNG)** writes the level shown, top-down and north up: a hidden viewport that shares the map view's world
  renders it with an orthographic camera (`MapPicture`), 4 to 32 pixels a tile so the longer side is at most 4096. What the map view
  draws is in it (an overlay shown, the grid); the selection's marks and the objects' captions are left out.
- Both ask for the file with a save dialog that starts beside the map's file (else in the maps folder) with a name made from the map's,
  and say in the editor's messages where they wrote. Exports change nothing in the map and add no undo step.

### ADR-267: Play-test from the editor (ADR-250, 2026-11-21)
**Status**: implemented (plan step 4, task 8; EDIT-5).

- **The game's play-test arguments** (after `--`, read by `PlayTestArguments.Parse` in Core.Domain, exposed as `LaunchArguments.PlayTest`):
  `--player=N` (the player number played, 1 for the first; it makes the run a play-test), `--others=ai|off`, `--reveal`,
  `--start=x,y[,level]`, `--day=N` (1 to 9999), `--cheats`; the map is `--map=`. A play-test starts that map at once, with no menu; an
  argument that cannot be read, a missing map or a start that cannot be used opens the main menu with a window that says why.
- **Setup**: the play-tester's player is played by a person, every other by the computer (`PlayTestArguments.Prepare`). The rest are the
  game's options (`GameOptions.PlayTest`, `PlayTestRules`), kept in saves and replays (save format 7) and applied when the game starts:
  the starting day; the first person's first hero (after the hero placeholders are drawn) on the start tile, unless a hero of theirs
  stands there, which is then the hero selected; the whole map revealed to the people playing. A start must be on the map, on walkable
  ground and free of objects and of other players' heroes; an arena map starts on day 1.
- **Others off**: the players nobody plays end each turn at once (`AdventureAi.PassTurn`: what they are asked is answered as the AI
  answers it, then their turn ends).
- **Cheats are commands** (ADR-036): `CheatResourcesCommand` (no amount negative), `CheatLevelsCommand` (1 to 10 levels' experience; the
  level-ups are offered as usual), `CheatCreaturesCommand` (1 to 9999, merged or in a free slot). Each is refused unless the game is a
  play-test with cheats. The cheat panel (`CheatPanel`, Godot.UI) exists only then, over the map's left edge, for the player whose turn
  it is and the selected hero: Resources (10 000 gold, 20 lumber and ore, 10 of each other), +1 level, +10 of the hero's first creature.
- **Not ranked**: a play-test never scores (`ScoreRules.IsRanked`), so the ranking never gets it.
- **The editor's Test** (F5, Test > Test the map): a window with the player, the others off, where the hero starts (where the map puts it,
  the selected hero, whose owner is then played, or the first selected cell), the starting day, reveal and cheats, and the command the game
  is started with. A map with errors is not tested. Test saves the map to `playtest.ewmap.json` in the system's temporary folder
  (`EternalWars/`) and starts the game as a separate process; the editor stays as it is, and the choices are offered again by the next
  Test. The game is the path in Preferences: an exported game's program, run as it is, or the game's Godot project (its folder or its
  `project.godot`), run in the Godot the editor runs in (`PlayTestLaunch`; an exported editor is no Godot, so it needs an exported game);
  empty, the game project beside the editor's where there is one. The window refuses choices the game would refuse (a start the player
  cannot use, an arena map not on day 1). A play-test keeps its saves in the temporary folder (`EternalWars/playtest-saves`), apart from
  the player's; its cheats wait for a battle or a question to end and are refused in a LAN game.

### ADR-268: Template undo (EDIT-2, ADR-250, 2026-11-21)
**Status**: implemented (plan step 5, task 1).

- **Every change is one named step** (`TemplateEditorSession`, Core.Generation): the session keeps an `UndoHistory` whose steps hold the
  template before and after (templates are values, so a step keeps both and puts one back whole). Names: "Add zone treasure-1",
  "Change zone middle", "Rename zone a to b", "Move zone x", "Remove zone x", "Connect a – b", "Change connection a – b",
  "Remove connection a – b", "Change the template's settings", "Arrange the graph". A change that leaves the template as it is (a field
  left as it was, a zone moved to where it is) records nothing.
- **Undo, Redo, UndoTo** put a kept template back and publish `TemplateChanged` with `Replaced`, so the views show every part again;
  `UndoHistoryChanged` and `UnsavedWorkChanged` are the map's events, published on the template's bus. `IUndoSession` (Core.Domain) is
  what the map's and the template's sessions share for the History list.
- **A drag is one step**: `BeginStep` / `EndStep` gather the changes between them (the graph opens one when a zone is pressed and closes it
  on the release, when it is hidden, and before a Delete or a connection); a drag that ends where it began records nothing. An undo, a
  redo, a load or the question over unsaved work ends an open step first.
- **Loading** (New, a file, a shipped template, a restored autosave) starts the history anew and counts as saved; zones without a graph
  place are placed as part of the load (`Load(arrange: true)`), not as a step.
- **The editor**: Edit > Undo and Redo (Ctrl+Z, Ctrl+Y) work on the tab shown, the template's history on the Template tab. The template
  editor shows the template's History beside its Issues under the graph (the bottom region's History belongs to the Map tab); a click on a
  row jumps there. A field of the inspector (text or number) that loses the focus while an undo rebuilds the inspector does not apply
  what was typed in it.

### ADR-269: Template files (ADR-250, 2026-11-21)
**Status**: implemented (plan step 5, task 2).

- **`TemplateFileService`** (Godot.UI, Editor/Shell) does for the template what `FileService` does for the map: which file the template
  is, New, Open and Save as with the file dialog (in the user's templates folder, `user://templates`), Save to the file, the recent files
  (the editor settings' `template-files` section), the question before unsaved work is left (another template, quitting: the template's
  question comes first, then the map's), and the autosave every few minutes (the Preferences' interval) while there is unsaved work.
  The formats are the app's (`ITemplateFiles`, implemented by the editor's `TemplateFiles` with `TemplateSerializer`).
- **The unsaved marker**: the Template tab reads "Template*", and the template editor's top row names the template with an asterisk.
- **Shipped templates** open read-only (the top row's Shipped list, Templates > Shipped templates): they have no file, the name says
  "(shipped, read-only)", and Save asks where to keep a copy.
- **Autosaves and recovery**: `AutosaveFolder` (Core.Data) holds the session marker and the timed autosaves for both editors;
  `MapAutosaves` and `TemplateAutosaves` are its two kinds, each in its own folder (`templates/.autosave` for templates). A template
  autosave left by a session that did not close is offered at start when the map asks nothing, else when the Template tab first shows
  (never over the map's question); quitting before then offers it first. Until it is offered the session is not marked safe, so a
  second crash still finds it. A Templates-menu command chosen while that question is open is not run.
- **Commands**: Templates > New template, Open template…, Recent templates, Shipped templates, Save template, Save template as… (each
  shows the Template tab first). File > Open, Save and Save as, and their shortcuts, work on the tab shown; New map… stays the map's.
  The template editor's top row has New, Open…, Shipped…, Save and Save as.

### ADR-270: The template graph: zone cards, labelled connections, grid, align and spread (ADR-117, ADR-250, 2026-11-21)
**Status**: implemented (plan step 5, task 3).

- **Cards, not circles** (`TemplateGraphView`): a zone is a fixed-size card (150 × 84 pixels; header only below a zoom of 50 pixels per
  graph unit) whose header has the type's colour, icon and the id, and whose lines give the type with owner and team, the size, the
  monster strength, the towns and the mines. The size no longer draws as the node's radius: it is written, since a card's area would
  not read as a weight anyway. A connection's label (kind, guard, value, passages, chance) is drawn over the cards so that it always
  reads, and a click on it selects the connection.
- **The blank template spaces its zones three graph units apart** (was two), so the cards do not overlap at the default zoom; new zones
  look for a free spot a card's size away.
- **Geometry in the core** (`TemplateGraph`, Core.Generation): `Snap` to a grid (`Grid` = half a graph unit), `Align` (a row at the
  zones' mean height or a column at their mean x, snapped) and `Spread` (even steps between the outer two, in their order along the
  line). The session's `AlignZones` (two zones or more) and `SpreadZones` (three or more) apply them as one step; they change only graph
  places, never the map.
- **Snap to grid** is a view setting, on by default and not saved: dragging snaps the held zone and moves the rest of the selection by
  the same step, so zones off the grid keep their offsets; nothing moves until the pointer is 4 pixels from the press, so a click on a
  zone off the grid does not snap it. Align snaps; Spread does not (it would break the even steps); Paste moves the zones by whole grid
  steps from where they were copied. A drag whose step an undo or a load ended moves nothing more.
- **Graph tools** sit in a row over the graph: Snap to grid, Align in a row, Align in a column, Spread across, Spread down, Arrange
  (moved from the top row; auto-arrange is unchanged).

### ADR-271: The template graph's selection and clipboard (ADR-268, ADR-270, 2026-11-21)
**Status**: implemented (plan step 5, task 4).

- **Selection** (`GraphSelection`): several zones, or one connection, or nothing. Click selects, Ctrl or Shift + click adds or drops a
  zone, a left drag on empty ground draws a marquee (adding with Ctrl or Shift); pressing a zone of a multi-selection drags all of it
  (`MoveZones`, one step "Move N zones"), and a click on it without a drag keeps only that zone. Panning moved to the middle button and a
  right drag on empty ground, since a left drag there is the marquee. Several selected zones show their own inspector (the ids, the
  connections between them, align, spread, copy, duplicate, remove).
- **Clipboard in the core** (`TemplateFragment`, Core.Generation): the zones and the connections with both ends among them.
  `TemplateEditorSession.Copy`, `Paste(fragment, x, y)` (centred there) and `Duplicate(ids, dx, dy)` are one step each ("Paste N zones",
  "Duplicate zone x"). Pasted zones get new ids (the id's stem without its number plus the next free number: `treasure-2` → `treasure-3`,
  `middle` → `middle-1`), start zones the lowest free player slots with their teams kept (the whole paste is refused when the slots are
  too few), a mirror only when its image is in the fragment, and every other field as copied. `RemoveZones` removes a selection with its
  connections and the mirrors naming it as one step.
- **Commands**: Edit > Select all, Deselect, Cut, Copy, Paste, Duplicate and Delete (and their shortcuts) work on the tab shown, the
  graph's selection on the Template tab, as Undo does (ADR-268); the graph no longer handles Delete itself. Paste goes under the pointer
  when it is over the graph, else to the view's middle, on the grid; Duplicate goes one unit right and one and a half down. The
  clipboard belongs to the template editor, so it survives opening another template (zones go from one template to another) and is apart
  from the map's.

### ADR-272: Template mirror tools (ADR-117, ADR-271, 2026-11-21)
**Status**: implemented (plan step 5, task 5).

- **Make the images** (`TemplateEditorSession.MakeMirror`): only in a template with a symmetry, only for zones that name no mirror and
  that no zone names (`MirrorRefusal` says why not; the Mirror button shows it). One image per step of the symmetry (one, or three for a
  quarter turn) per zone, as one step "Mirror zone x" / "Mirror N zones": a copy with a new id (as Paste, ADR-271), the lowest free player
  slot for a start zone and no team (an image is another player, no ally unless set so), and the zones linked in a cycle (zone → first image → … → zone). The images' graph places are the
  zone's place under the symmetry around the middle of the other zones (a random axis draws as the vertical one); an image that would
  cover a card (`TemplateGraph.CardRoom`) moves down until it does not. Graph places are only a picture, so this keeps them readable
  rather than exact.
- **Connections follow**: each connection of a mirrored zone gets its images, to the other zone's image when that zone is mirrored (or
  among the zones mirrored) and to the zone itself when it is its own image; a connection whose other end is in a broken chain gets none.
- **In sync while the cycle is closed** (`TemplateMirrors.Cycle`): an edit of a zone carries only the fields it changed to its images
  (`TemplateMirrors.Carry`), so a field an image has differently (from a file) stays until it is edited; the id, player slot, team,
  mirror and graph place are always each zone's own. A zone turned into a start zone gives its images the lowest free slots (refused when
  too few are free); turned into another type, they drop slot and team; without a change of type, slots and teams stay. A zone naming
  itself as its mirror counts as its own image, as the validator reads it. An edit that changes the zone's mirror itself carries nothing. Connect, a change of a connection
  (while its ends stay) and Disconnect apply to the connection's images (`ImagePairs`) in the same step. Moving, renaming and removing
  zones are not mirrored: removing one leaves the validator to report the broken cycle.
- **Break mirror** (`BreakMirror`) unlinks a whole cycle as one step, so zones that should differ can be edited apart. The inspector's
  Mirror field names a mirror by hand.
- The graph draws a thin dotted line from each zone to its mirror.

### ADR-273: The template inspector's pickers and zone presets (ADR-117, ADR-250, 2026-11-21)
**Status**: implemented (plan step 5, task 6).

- **Every field in the inspector**: each field of the file has a control. Lists of choices are **tick-box pickers**, not drop-downs:
  a zone's terrains (the terrains the session's tables, `TemplateEditorSession.Content`, call passable), its neutral town types (the
  playable towns), the week events' factions (`WeekEventSettings.Factions`) and the roads' mines. None ticked means any for terrains and
  town types (kept as null); factions ticked as the playable towns are kept as null, the default. A start zone's start buildings are a
  level per town building (0: not built; the Grail is left out) and its start dwellings a count per tier (two for tiers 1 to 4, one for
  tier 5); the validator judges the whole (ADR-030). Roads get their type, mines and gates; week events the weeks before the first.
- **Zone presets** (`ZonePreset`, `ZonePresets`, Core.Generation): a name and a zone's settings without what belongs to the zone (id,
  slot, team, mirror, level, graph place). Built in: Rich treasure, Mine junction, Guarded start. The session's `AddZone(preset, x, y)`
  ("Add zone x (preset)") and `ApplyPreset(id, preset)` ("Apply preset p to zone x") are one step each; applying keeps the zone's own
  fields, gives a zone made a start zone the lowest free slot (refused when none is free) and drops slot and team otherwise, and goes
  through the mirror sync (ADR-272).
- **User presets are files** (`ZonePresetFolder`, Core.Data): `*.ewzone.json` holding the template format's version, the name and the
  zone, in `user://template-presets/` (the app's `ZonePresetFiles` behind the panel's `IZonePresetFiles`, since Godot.UI does not use
  Core.Data). The name makes the file name (characters a file name cannot have become `_`); saving over a name replaces it; a file that
  cannot be read, or one of a newer template format, is left out of the list; a list, range or band a hand-edited file leaves null is
  made empty (`ZonePresets.Settings`, which also copies the tables, so zones made from one preset share none), and Delete finds a preset
  whose file was renamed by hand. The lists are read each time a preset menu opens, so
  presets saved elsewhere show without a restart.

### ADR-274: The template preview and Generate many (ADR-087, ADR-117, 2026-11-21)
**Status**: implemented (plan step 5, tasks 7 and 8).

- **The template editor generates in the core** (`TemplateBatch.Generate`, Core.Generation): every start slot a player, towns drawn from
  the seed, as `EditorMapGenerator` makes it. The panel runs it off the main thread and shows the map in a **Preview** tab beside the
  graph instead of opening it; the app only opens a map it is handed (`TemplateEditorHost.Open`, "Open in map editor"), and gives the
  content tables (`TemplateEditorHost.Content`). A newer Generate drops the result of one still running. A handed-over map belongs to the
  map editor, so opening again generates the seed afresh rather than sharing a `GameState` between two sessions.
- **The picture is core data** (`ZonePicture`): each tile's minimap cell (`Minimap.Build`, omniscient) and its zone from the generation's
  `ZoneLayout`, the borders between zones and a label tile per zone (its tile nearest the mean of its tiles, so a bent zone's label stays
  on it). The view (`ZonePictureView`, Godot.UI) only colours it: at three or more pixels a tile a border is the tile's last pixel row or
  column, below that the tile is darkened. Built off the main thread with the map.
- **Generate many** (`TemplateBatch.RunAsync`): N seeds (default 20, at most 200) counting up from the top row's seed, at most
  `DefaultParallel` (half the processors) at once through `Parallel.ForEachAsync`, cancellable, returned in seed order; a template with
  errors, a size it lacks or a bad count is refused before any map. Each map (`BatchMap`, `Check`) has the `sim maps` checks without the
  AI's play: the generator's problems, the same digest from a second generation, `MapConnectivity.UnreachableRivalTowns`, the map
  validator's errors. Fairness is the generator's own `StartMeasure`s (already computed per map); per map each measure's **imbalance** is
  (largest - smallest) / mean in percent, and the batch's `BatchSummary` gives how many passed and the smallest, mean and largest of the
  attempts and of each imbalance. A batch's maps equal single runs of their seeds (tested).
- **Workers never call Godot**: many workers deferring calls at once crashed the engine, so Generate's task and the batch's workers queue
  their results (`ConcurrentQueue`) and the panel drains the queue in `_Process` while one runs, dropping a Generate older than the newest
  and what belongs to an older batch. A batch runs on while the panel leaves the tree (it does at every map open) and stops only when
  the panel is freed. Handed-over maps are remembered weakly, so the panel keeps no closed map alive.

### ADR-275: The shortcut editor (ADR-250, 2026-11-21)
**Status**: implemented (plan step 6, task 1).

- **Any command can be rebound**, in Preferences' Shortcuts tab; the keys are the command registry's, so the menus, the toolbar's
  tooltips, the palette and the tool buttons show the key in effect. A command's registered key is its default
  (`EditorCommand.DefaultShortcut`); the registry puts the user's keys over the defaults (`CommandRegistry.UseShortcuts`).
- **The rules are in the core** (`KeyChord`, `ShortcutBindings`, Core.Domain.Editing): a chord is a key name with Ctrl, Shift and Alt,
  kept as text ("Ctrl+Shift+S"); `Conflicts` lists the chords several commands share; `Changes` is what differs from the defaults;
  `Resolve` puts kept changes over the defaults, dropping changes for commands that no longer exist and changes giving one chord to
  several commands (a hand-edited file), and taking a default chord from its command when the user gave it to another (the user's
  choice wins, so a later default never silently steals a user's key). No chord is ever two commands'.
- **Conflicts are shown, not allowed**: both commands in red, each naming the other, a line over the list; OK is disabled until every
  key is one command's. The editor never runs a shared key ambiguously.
- **Kept in the editor settings** (`editor-settings.cfg`, section `shortcuts`): only the changes, by command id; an empty value is no
  key, and a key the engine does not know counts as none.
- **Capturing**: a click on a key waits for the next key press; Escape gives up waiting, so binding Escape itself is only by
  "Default" (Deselect's default). Only the commands are rebindable; the camera's keys and the panels' own keys stay as they are.

### ADR-276: The editor's F1 help, made from the design documents (ADR-250, 2026-11-21)
**Status**: implemented (plan step 6, task 2).

- **One text**: the help's pages are the design documents' own paragraphs, so the docs stay the one statement of the rules and the help
  never drifts from them. `docs/design/tools.md` (section "Map editor") and `random-maps.md` ("The template", "Editor", "Template
  editor") are built into Godot.UI as embedded resources; no second copy is kept in `res://`.
- **Pages by structure** (`HelpBook`, Core.Domain.Editing): a heading, and every paragraph or bullet that starts with a bold term
  (optionally after "The"), is a page titled with the term; a bullet with a term under a paragraph, or nested under such a bullet, is
  its child; text without a term at its start belongs to the page above. An anchor before a page is its id (so the docs' own
  `#editor-…` links open pages); other bold terms in a page are its aliases (the Terrain, Road and River tools find Brushes). The
  decision and question numbers are left out of the shown text. The docs are written so this works: each tool and each kind of
  problem has a paragraph or bullet led by its name, and new editor text keeps doing so.
- **The page for what is hovered** (`EditorHelp`, Godot.UI): from the hovered control outwards, a control's own topics
  (`IHelpTopics`: the Problems list gives the row's kind, "Hero problems"), a topic set with `Tag`, for a tab the topic of what it
  shows, the hovered control's text (a button, a label, a tab; an icon button's tooltip), then the node names ("Inspector",
  "Problems"). Where a word means two things the control is tagged: the Events panel ("Map events", not the inspector's Events), the
  workspace's page tabs, the Preferences' tabs. Links resolve anchors and `file.md#heading` targets of the two documents. A topic matches a page id, then a title, then an alias,
  ignoring case, articles, a trailing "tool" or "tab" and a plural; the Template tab prefers random-maps.md's pages, the Map tab
  tools.md's. Nothing found: the first page.
- **F1 is the Help command's key** (rebindable, ADR-275), taken in the app's `_Input` so it also works over a window (Preferences, the
  map settings) and inside text fields (a plain key rebound to it stays the field's); the Help menu opens the first page. The help window lists the pages by document and as
  sub-pages, searches titles as the command palette does, and links the page's parent, children and the docs' links.
- **Tests on the real documents**: every tool name, every kind of problem, the panels and the template's fields find a page, and no
  page shows a decision number; a doc change that breaks one fails the build's tests.

### ADR-277: The editor's UI scale and contrast (ADR-250, 2026-11-21)
**Status**: implemented (plan step 6, task 4).

- **Fit any window at any scale**: the scale is the window's content scale, so a large scale leaves few points (1600 × 900 pixels at
  200 % is 800 × 450). Rather than refuse a scale, the editor fits: when the layout is applied (at start, and when the window or the
  scale changes) the left region takes at most 30 % of the width, the right 35 %, the bottom 40 % of the height, while the kept sizes stay
  in the settings for a larger window; the boxes over the editor scroll inside the window (`EditorOverlay`); the Template tab's bars and
  the Problems filters wrap (flow containers); tab rows scroll with their arrows.
- **High contrast** is a preference (`EditorPreferences.HighContrast`), not a second theme file: `UiTheme` picks its colours by
  `UiTheme.HighContrast` (opaque near-black grounds, light frames, white text, lighter quiet and disabled text, a 2-pixel focus frame),
  and `UiTheme.Apply` again switches a running editor: every theme item is set in both looks except the text colours, which go back to
  Godot's own. The editor's panels built in code take their look from theme variations (`RegionPanel`, `DialogPanel`, `CardPanel`,
  `TilePanel`, `PagePanel`, `QuietLabel`), so they follow a switch without being rebuilt; drawn controls redraw, the Problems rows and
  the status bar's counts are shown again, and other rows coloured in code (the history, the seeds) take the new tones at their next
  refresh. The game keeps the normal look.
- **Checked by smoke at every scale**: Movie Maker records at the project's window size whatever is asked, so the scales are checked
  at 1600 × 900: the same tour of every panel and window at 100, 150 and 200 % and in high contrast, turned off at the end. AutoInput's
  points are the UI's (turned into window pixels when sent) and it scrolls a tab out of sight into view before clicking it, so one
  script runs at every scale.
