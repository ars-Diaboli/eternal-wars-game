# Development guide

Build, test and asset-link commands are in the root `CLAUDE.md`.

## Toolchain
- .NET SDK 10 (pinned in `global.json`, `rollForward: latestFeature`).
- Godot 4.7.2 **.NET edition** (https://godotengine.org/download/windows/), needed only to run the apps.
- Binary assets (`*.png`, `*.glb`, `*.wav`, fonts, ...) go through **Git LFS** (`.gitattributes`; `git lfs install`
  once); `*.import` sidecars stay text.

## Running the apps
Apps are standalone Godot projects: `src/Apps/EternalWars.Game`, `src/Apps/EternalWars.MapEditor`,
`src/Apps/EternalWars.Harness2D`. Import `project.godot` in the editor and press F5 (an app-local `.sln` it generates
may be committed), or:
```powershell
godot --path src/Apps/EternalWars.Game [-- --map=<file.ewmap.json>]
```
In a fresh clone, import assets once per app (`<Godot console exe> --headless --path src/Apps/<App> --import`) or
every texture load fails.

App arguments (after `--`): `--map=`, `--new-game` (game opens on the new-game screen, not the main menu), `--replay=<file>` (the
replay viewer on a replay or save),
`--scenario=`, `--settings=`, `--saves=<folder>` (the game's saves and autosaves go there), `--autoinput=` (smoke tests), LAN games (ADR-194): `--host[=<port>]` (opens the host's lobby, default port
47820, discovery on the next port), `--join=<address>[:<port>]` (joins that host's lobby or game), `--name=<player>`, and content overrides `--creatures=`, `--heroes=`,
`--buildings=`, `--artifacts=`, `--artifact-sets=` (a bad file is reported with line and column; the embedded copy
is used instead). A play-test (the editor's Test, ADR-267): `--player=N` with the map, and `--others=ai|off`, `--reveal`,
`--start=x,y[,level]`, `--day=N`, `--cheats`; the game starts the map at once.

## Files and locations

| What | Format / where |
|---|---|
| Maps | `*.ewmap.json`, `MapDocument` (version history in its doc comment); editor saves to `user://maps/` (`%APPDATA%\Godot\app_userdata\Eternal Wars Map Editor\maps`) |
| Saves | `*.ewsave.json` (ADR-058) in `user://saves` (`%APPDATA%\Godot\app_userdata\Eternal Wars\saves`): game record + label (+ the campaign, ADR-241); load replays commands and checks the digest |
| Campaigns | `*.ewcampaign.json` (ADR-241) in `user://campaigns` or beside their maps in the map editor's `maps` folder; each map names a map file in the campaign file's folder |
| Replays | `*.ewreplay.json` (`GameRecord`) |
| Ranking (ADR-101) | `ranking.json` in the user data folder (`RankingFile`), each row's record in `ranking/*.ewreplay.json` |
| Templates | `*.ewtemplate.json`; shipped ones in `src/Core/data/templates` |
| Stamps | `*.ewstamp.json`, `StampDocument` (its own format version; its objects are in the map format the file names, upgraded like an older map's; samples in `tests/.../Data/StampSamples`, `vNN-mapMM` for one written under an older map format); the editor saves to `user://stamps/` |
| Content tables | `src/Core/data/*.csv`, embedded at build time (see below) |
| JSON overrides | `CreatureDefinitionSerializer`, `TerrainDefinitionSerializer`, `MapObjectDefinitionSerializer` (ids in `BuiltInMapObjects`) |

## Headless simulator
`dotnet run --project tools/EternalWars.Tools.Sim -- <command>`; no Godot, drives the core through commands only. Any
command takes `--creatures path.csv`. One class per command (`Commands/`); options are strict (ADR-224): an unknown option, a
missing or bad value, an option given twice or a missing argument is `error: ...` naming it and listing what the command takes, exit code 2.

| Command | Does |
|---|---|
| `battle haven.pikeman:20,haven.archer:10 inferno.hell-hound:12 [--runs N] [--seed S]` | battle statistics |
| `record <file> [--days N] [--seed S] [--computer]` / `replay <file>` | record a game (`--computer`: Red is a computer player, its battles fought within one command) / verify a replay and print how long reading and replaying it took (what loading it costs) |
| `selftest` | determinism + replay check (CI) |
| `game --map towns [--days N] [--runs N] [--record f]` | AI plays every slot; prints fights won/lost by kind, what is left on the map, garrison hero moves |
| `game --template duel [--size N] [--players N]` | same on a new generated map per run (as the new-game screen would) |
| `game ... --weight Name=value` | overrides one `AiWeights` number for the adventure AI (repeatable; only `game`, `maps`, `selftest`); an unknown name, a bad value or a negative one is an error listing the names |
| `game ... --fog refog` | plays with shroud and refog instead of permanent reveal (the AI then sees only what is in sight) |
| `game ... --length L` | a tournament map lasts L days (its default, 14, otherwise; an error on any other map), and `--days` defaults to past its battle; every run that reaches the battle prints the champions' levels and army values, and the runs together the least, median and most |
| `game ... --profile` | also the time of each AI turn, its path searches and cost maps (count, routes found, tiles expanded, time; `PathFinder.Counters`), its battle estimates (asked, played, time; `BattleEstimateCounters`) and battles, per player, per week and for the slowest turns. Counts are the same on every run of a seed, times vary by about 10 %. Run a Release build (`-c Release`) for timings; the baselines are in `docs/papers/refactoring-plan-2026-11.md` |
| `maps [--template t] [--maps N] [--days N] [--seed S]` | per template and size: generates without problems, deterministically, every start town reachable, no validator errors, N days of AI play with no refused command, replays to the same state. CI runs `maps --maps 6 --days 14`; a failure names the repro (`maps --template <t> --seed <s> --maps 1`) |
| `fairness [--template t] [--seeds 20] [--size N] [--verbose]` | per start: mine distance, treasure value, guard power, fairness problems (ADR-117); with `--verbose` also the map analysis's numbers and warnings (ADR-263) |
| `layout <template> [--size N] [--seed S] [--rects] [--terrain] [--seeds N]` | zone layout as text; `--terrain` the generated map; `--seeds N` batch-checks N seeds with timing |
| `generate <template> --out <file>` / `export towns\|demo\|ember --out <file>` | write a generated / built-in map |
| `towns [--runs N] [--upgraded]`, `lines [...]`, `creatures [--write f.csv]` | town-army round robin; creature lines vs their tier; weekly power table |

Templates are named by name (`"Two against two"`), file key (`2v2`, `duel`, `ffa-4`) or path.
Debug env vars: `EW_SIM_EXPLAIN=10,25` (on those days, each hero's best targets as the AI sees them,
`AdventureAi.Explain`), `EW_SIM_GUARDS=1` (every monster stack, explored or not), `EW_SIM_TRACE=1` (battles, defeated heroes, adventure spells cast, and after each turn where every hero stands with its army power and mana).

## Godot smoke tests
`./tools/smoke-godot.ps1 -Godot <Godot .NET console exe> [-Only 'game-*'] [-Jobs 8] [-NoBuild] [-Png] [-Seed <n>]` runs the scenarios listed in
the script under Movie Maker and saves frames to `.shots/<scenario>/`. **A pass only means no errors: look at the frames.**
- Frames are `frame00000000.jpg`, … (zero-based), cut out of the scenario's `frame.avi` (Motion JPEG). `-Png` writes lossless
  `frame00000000.png`, … instead, for comparing frames exactly; compressing PNGs takes most of such a run (several times slower).
- Scenarios run side by side (`-Jobs`, default half the logical processors, at most 8; more gains nothing), the longest first; each
  is its own Godot process with its own settings file and save folder (`--settings=`, `--saves=`) in its `.shots` folder, next to
  its `godot.log`. Under Movie Maker the apps' windows take no focus (`window/size/no_focus.movie` in each `project.godot`): a
  window taking the focus closes the other windows' drop-down lists and loses their clicks.
  The windows share the one mouse pointer, so hover highlights can differ between runs; `-Jobs 1` when a frame must be exact.
- A scenario runs at 10 frames a second; `Fps = <n>` in its entry changes that (the turn timer runs at 2, so its minute is 120 frames).
- `Settings = '<json>'` starts a game scenario from that settings file (a UI scale, the mirror). `Resolution = '<w>x<h>'` records it at
  that window size: Movie Maker ignores `--resolution`, so such scenarios run in groups of one size with an `override.cfg` beside the app's
  `project.godot`, removed after them; the window must fit on the desktop to be recorded whole. The `hud-*` scenarios shoot the adventure
  HUD at each target size, UI scale and mirror; the `battle-hud-*` ones the battle HUD at each size and UI scale, the timeline at the
  bottom and on top.
- Script entries are `frame:kind:argument`: `click` / `rclick` (`x,y`), `key` (Godot key name, modifiers before it: `Ctrl+Shift+P`), `hold` (`Key,frames`),
  `wheel` (`up|down,count`), `drag` / `rdrag` (`x1,y1,x2,y2`, over three frames). Replayed by `AutoInput`.
- Game scenarios start with `--new-game` and `key:Enter` (Start has focus), so they do not depend on the screen layout;
  all start from default settings.
- Every scenario passes `--seed=<n>`, which the new-game screen uses instead of a new seed, so its games and battles are the same every
  run: the entry's `Seed = <n>`, else a seed made from the scenario's name (renaming a scenario changes its seed). `-Seed <n>` plays
  every scenario with that seed instead, to look for a failure under other battles; a failed scenario's line names its seed.
- In a scripted run the battle log's lines also go to the scenario's `godot.log` as `battle log: …`; search them rather than reading
  the log off the frames.
- Maps live in `tools/smoke/` (or come from `sim generate` / `sim export`); `SmokeMapTests` checks they still do what
  the scripts expect.

## Editor timing
`./tools/perf-editor.ps1 -Godot <Godot .NET console exe> [-NoBuild] [-Runs N] [-Map <file>] [-Size N] [-Seed S] [-TimeoutSeconds N]` times the map editor on a
large map (ADR-278): it generates a 256 × 256 two-level map from `ffa-8` into `.perf/` (once), then runs the editor in a real window with
vsync off and `--map=<file> --perf=<report>`. `EditorPerfProbe` opens the map as File → Open does, times idle frames, paints a Terrain
stroke of real mouse events that crosses new ground every frame, waits for the Problems panel's check, and writes `EditorTimings` to
`.perf/run<N>/report.txt`. The budgets: open within 3 s (from the command to the first frame that shows the map), paint at a mean of
60 fps, and no frame over 50 ms while the map is validated (nor the validation's copy on the main thread). A missed budget or an error
in the log fails the script, as does a map that did not open or a run past the timeout (300 s). The numbers are this computer's: compare
runs on one machine.

## Town scene budget
F3 in the game shows the rendering budget overlay (ADR-308) on every screen: the frame's triangles, draw calls (the shadow passes'
share beside them), the game's video memory and the frame time (CPU and GPU), each against the town scene's budget (`TownSceneBudget`:
1.5 M triangles, 2,000 draw calls with the depth pass and the shadow cascades counted, 1 GB, 60 fps, or 40 on a Steam Deck), and the town
scene's opening time (a second). `--budget` starts with it shown and logs a `Budget [<town type>]: …` line every ten frames;
`--budget=2560x1440` also renders the 3D that wide whatever the window. `./tools/smoke-godot.ps1 -Only game-town-budget` goes round the
eight fully built greybox towns twice that way: read the numbers in its `godot.log`. `game-map-budget` does the same for the adventure map with its HUD (Ember
March at 2560×1440), `game-battle-budget` for a siege battle (`Budget [battle]`); Godot's draw call count is the 3D scene's, the 2D HUD's canvas is not in it. The times are the machine's own.

CI (`.github/workflows/ci.yml`) runs build + test + `sim selftest`, and a parallel `generated-maps` job; no Godot.

## Conventions
- Rules from `CLAUDE.md` apply (Tier 1 if it compiles without `using Godot;`, `partial` nodes, `Bind(...)`,
  commands only, `GameState.Random`). Events: [event-system.md](event-system.md).
- Namespaces follow folders (`EternalWars.Core.Domain.Heroes`).
- Core builds with warnings as errors; keep Godot projects warning-free too.
- Tests: `MethodUnderTest_Scenario[_Expected]`. Adventure-map tests use `TestWorld` ASCII maps (legend in its doc
  comment; `TestWorld.Road(...)`; `TestWorld.Start(state)` pre-reveals the map unless the test is about fog; heroes
  carry `DefaultArmy()`, 1500 MP) and issue commands with `SessionCommands` helpers (`session.Step`, `MoveTo`,
  `EndTurn`, `AutoResolve`, ...). Rule-level battle tests place units with `TestBattle`.
- Commits: imperative mood, small and focused.

## Controls (placeholder)
| | Game | Editor | Harness |
|---|---|---|---|
| Select hero | Click, Tab, hero list | Select tool | Click |
| Hero / town screen | H / T, hero or town list, entering an own town; Escape closes | — | Click the town |
| Move hero | Click tile (preview), click again; arrows / numpad | Move tool (M), or drag a selected hero | Same |
| Level | L or button | L or button | — |
| Camera | WASD pan, Q/E rotate, wheel zoom | same | — |
| Edit | — | LMB with the active tool (Shift + click: a line); Ctrl+Z / Ctrl+Y; Ctrl+X / C / V / D | — |
| Battle | Point to preview, click move / attack (from the pointer's side) / shoot; W wait, D defend, R retreat, Q quick combat (`BattleHotkeys`); wheel zoom; Enter / Esc leave the result | — | Click; same keys |
| Save / load | F10 or Menu; F5 quick save, F9 quick load; autosave at turn start (two slots) | — | — |

## Content tables
Every table in `src/Core/data` (ADR-071) is a CSV with columns matched by header name (Excel's semicolon CSV works;
extra columns such as `notes` are ignored). Ids are referenced by maps, saves and code: rename with care. Apps,
tests and the default catalog use the copy embedded at build time; to try an edit without rebuilding, pass the
matching `--creatures=`-style flag (apps) or `--creatures path.csv` (sim). `DataTableSchemaTests` checks every table's
columns, ids and references; other tests check balance rules (e.g. nine upgradeable lines per town, tiers getting
stronger, no copy-pasted stat lines).

### Editing creature stats
`creatures.csv` (ADR-044), one row per creature.

| Column | Meaning |
|---|---|
| `id` | `town.kebab-name` |
| `name`, `town`, `tier` | display name; town id (empty for neutrals); tier 1–5 |
| `size` | `Small` (2×2 cells), `Medium` (3×3), `Large` (4×4) |
| `upgrade` | id of the upgraded form (same town and tier) |
| `attack`, `defense`, `min_damage`, `max_damage`, `health` | per creature |
| `speed`, `map_speed`, `initiative` | battle cells per turn; HoMM3-scale adventure speed; turn order |
| `shots` | ammunition; empty or 0 = melee |
| `morale`, `luck` | −5…+5, empty = 0; the hero's are added in battle |
| `fight_value` | how dangerous one creature is (ADR-187), read by the battle AI and wherever army strength is weighed; empty = derived from the stats (`DerivedFightValue`) |
| `growth`, `gold`, `resources` | weekly growth; gold cost; other costs like `mercury 1` |
| `tags` | words separated by `;` (`human; holy`) |
| `capabilities` | up to five, `;`-separated: `Flying`, `PackHunter 20`, `SpellImmunity mind`, `Caster chaos.fireball 1` (a spell table id, or `rebirth`), … (`CapabilityKind`) |

### Editing artifacts
`artifacts.csv` (ADR-061) and `artifact_sets.csv` (`id, name, effects, notes`; a set needs two or more pieces).

| Column | Meaning |
|---|---|
| `id`, `name` | kebab-case id, display name |
| `slot` | `Head`, `Neck`, `Cloak`, `Body`, `Weapon`, `Shield`, `Ring` (two fit), `Boots`, `Misc` (three fit) |
| `tier` | `Minor`, `Major`, `Relic`: what a map pickup of that tier can roll |
| `set` | id in `artifact_sets.csv`, or empty |
| `effects` | `;`-separated, e.g. `attack 2`, `income gold 100`, `skill machines.ballistics 1`, `spells 1-2`; the words and units are `ArtifactEffect`'s table and `ArtifactEffectKind`'s doc comments (`Artifacts/ArtifactTypes.cs`); negatives work |
| `notes` | effects the game cannot apply yet are marked "Not yet" |
| `pieces` | for an **assembled relic** (ADR-121): `;`-separated ordinary artifacts (two or more, each in one relic only); the relic's `slot` is its first piece's and blocks the others' slots; relics never drop, only pieces |

Pickups `artifact.minor` / `.major` / `.relic` roll at game start; the editor can fix one (`SetArtifactPickup`).

### Editing town buildings
`buildings.csv` (ADR-046, ADR-050), one row per building level.

| Column | Meaning |
|---|---|
| `id`, `level`, `name` | building id (`town-hall`, `fort`, …); level 1, 2, … without gaps; display name |
| `town` | empty: shared by every town. A town type: that town's rows replace all shared levels of the building, or add a town-only building (ADR-074); level `0` removes the shared building from that town (ADR-112) |
| `gold`, `resources` | cost (`lumber 5; ore 5`) |
| `requires` | `blacksmith 1; fort 2`; `dwelling-3 1` is met by any tier-3 dwelling |
| `income` | daily while this is the highest level built: `gold 500` |
| `growth` | flat weekly bonus for every dwelling of a tier: `dwelling-1 5` (ADR-112) |
| `kind` | the town screen Hall's column: `core`, `military`, `magic`, `dwelling` or `unique` (empty: unique); the first level's counts (ADR-296) |

`dwelling-1` … `dwelling-5` price the dwellings of a tier (level 1 builds, 2 upgrades) for every line of that tier; the
lines come from the creature table. The 7-of-9 rule (ADR-030) is in code.

`rumours.csv` (ADR-296) holds the taverns' rumours for maps without their own: `id, text, notes`, one line each. A text may name
`{strongest}`, `{richest}`, `{mosttowns}` with `{towns}`, and `{treasure}` with `{where}`; the game fills them, and does not tell a line
naming something it has not got.

### Town layouts
The town screen's 3D scene (ADR-297) reads one Godot scene per town type: `scenes/towns/<town>.tscn` in `EternalWars.Godot.Assets`
(`res://shared/scenes/towns/`), its root a `Node3D`. It holds the diorama (ground, water, trees, mountains) and a `Marker3D` plot for every
building and dwelling line of the town type, anywhere under the root, named `plot_` and the plot's id with dots as underscores
(`TownOverview.PlotNode`): `plot_town-hall`, `plot_mage-guild`, `plot_dwelling-haven_footman`. A plot may have these children:

| Child | Meaning |
|---|---|
| `Model` (`Node3D`) | the layout's own greybox of the building, shown while it is built |
| `Hover` (`Area3D`) | its `CollisionShape3D`s are what the pointer picks (as boxes around each shape); without it, a box around what the plot shows |
| `Focus` (`Marker3D`) | where the camera looks at the building; without it, 3 m above the plot |

A built plot shows `models/towns/<town>/<plot id>_<level>.glb` (or the nearest lower level's) when there is one, else its `Model`, else a
block sized by the building's kind with a roof and a sign. The root's `Camera` is the scene's hero shot, and its `WorldEnvironment` and
`DirectionalLight3D` its light; a layout without them gets defaults. Moving a plot moves the building: no code changes.
`TownLayoutTests` fails when a layout lacks a plot for a building or line of its town type, or has one for something the town has not;
in the game, `TownScene3D` reports an error when it loads a layout without a plot for one of the town's buildings or lines (smoke
`game-town-layouts`, which opens a fully built town of each type). Every playable town type has a layout (ADR-300); a town type without
one shows the key painting.

The town type's painting `textures/ui/backgrounds/towns/bg_town_<town>.jpg`, once it exists, is the sky of a layout whose
`WorldEnvironment` has a sky (`shaders/town_sky.gdshader`): its middle where the hero shot looks and its horizon on the horizon. The root's
metadata `sky_span` (the painting's width in radians, 2.1) and `sky_horizon` (its horizon's height from the bottom, 0.35) adjust the fit.
Without the painting the layout's own sky shows.

Besides the plots a layout holds the town's scenery (ADR-301), shown as the town has it (`TownScenery`); every piece is a marker that
`TownDressing` builds in the town's siege look, unless the marker has children of its own (the layout's art):

| Node | Meaning |
|---|---|
| `Walls` with `Post0`, `Post1`, … and `Gate` (`Marker3D`s) | the wall's line in order and its gate: sections, gatehouse, moat outside (away from the plots) and towers, as the siege has them |
| `Streets/Village`, `Streets/Town`, `Streets/City` | shown from that settlement on (the town hall's level 1, 2, 3) |
| `Street*` in a street group | a strip of paving; metadata `size` (`Vector2`, width × length along the marker's Z) |
| `House*` in a street group | a house; metadata `size` (`Vector3`) |
| `Banner*` in a street group | a banner in the owner's colour with the faction's charge |
| `HeroBanner` | where the garrison hero's banner flies; without it, on top of the town hall's plot |
| a plot's `Land` | shown while the plot is not built or its line is closed |
| a plot's `Homes` | the citizens' homes on a closed line once the town has the Capitol; without it, a few greybox houses |

`TownLayoutTests` fails on a layout without a `Walls` line (two posts and a gate) or without houses for each settlement.

### Editing heroes
`heroes.csv` (ADR-048).

| Column | Meaning |
|---|---|
| `id`, `name`, `town` | `town.name`; display name; town id |
| `class` | `Warrior` or `Mage` |
| `preferred` | base id of a tier 1–4 line of the same town; each line has one standard mage and one standard warrior |
| `specialty` | `skill <id>`, `spell <id>` or `resource <resource> <amount a day>` |
| `skills` | `offense.melee Basic; tactics.tactics Advanced` (ids in `BuiltInSkills`) |
| `attack`, `defense`, `spell_power`, `knowledge` | starting primary stats (empty = 0) |
| `campaign` | `yes` for a campaign hero (ADR-298): never drawn at random, in play only where a map places it; empty otherwise |
