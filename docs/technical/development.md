# Development guide

Build, test and asset-link commands are in the root `CLAUDE.md`.

## Toolchain
- .NET SDK 10 (pinned in `global.json`, `rollForward: latestFeature`).
- Godot 4.7.2 **.NET edition** (https://godotengine.org/download/windows/), needed only to run the apps.
- Binary assets (`*.png`, `*.glb`, `*.wav`, fonts, ...) go through **Git LFS** (`.gitattributes`; `git lfs install`
  once); `*.import` sidecars stay text. Exception: `textures/terrain/beach.png` is a plain blob (LFS quota).

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
`--scenario=`, `--settings=`, `--autoinput=` (smoke tests), and content overrides `--creatures=`, `--heroes=`,
`--buildings=`, `--artifacts=`, `--artifact-sets=` (a bad file is reported with line and column; the embedded copy
is used instead).

## Files and locations

| What | Format / where |
|---|---|
| Maps | `*.ewmap.json`, `MapDocument` (version history in its doc comment); editor saves to `user://maps/` (`%APPDATA%\Godot\app_userdata\Eternal Wars Map Editor\maps`) |
| Saves | `*.ewsave.json` (ADR-058) in `user://saves` (`%APPDATA%\Godot\app_userdata\Eternal Wars\saves`): game record + label; load replays commands and checks the digest |
| Replays | `*.ewreplay.json` (`GameRecord`) |
| Ranking (ADR-101) | `ranking.json` in the user data folder (`RankingFile`), each row's record in `ranking/*.ewreplay.json` |
| Templates | `*.ewtemplate.json`; shipped ones in `src/Core/data/templates` |
| Content tables | `src/Core/data/*.csv`, embedded at build time (see below) |
| JSON overrides | `CreatureDefinitionSerializer`, `TerrainDefinitionSerializer`, `MapObjectDefinitionSerializer` (ids in `BuiltInMapObjects`) |

## Headless simulator
`dotnet run --project tools/EternalWars.Tools.Sim -- <command>`; no Godot, drives the core through commands only. Any
command takes `--creatures path.csv`.

| Command | Does |
|---|---|
| `battle haven.pikeman:20,haven.archer:10 inferno.hell-hound:12 [--runs N] [--seed S]` | battle statistics |
| `record <file> [--days N] [--seed S]` / `replay <file>` | record a game / verify a replay |
| `selftest` | determinism + replay check (CI) |
| `game --map towns [--days N] [--runs N] [--record f]` | AI plays every slot; prints fights won/lost by kind, what is left on the map, garrison hero moves |
| `game --template duel [--size N] [--players N]` | same on a new generated map per run (as the new-game screen would) |
| `maps [--template t] [--maps N] [--days N] [--seed S]` | per template and size: generates without problems, deterministically, every start town reachable, no validator errors, N days of AI play with no refused command, replays to the same state. CI runs `maps --maps 6 --days 14`; a failure names the repro (`maps --template <t> --seed <s> --maps 1`) |
| `fairness [--template t] [--seeds 20] [--size N] [--verbose]` | per start: mine distance, treasure value, guard power, fairness problems (ADR-117) |
| `layout <template> [--size N] [--seed S] [--rects] [--terrain] [--seeds N]` | zone layout as text; `--terrain` the generated map; `--seeds N` batch-checks N seeds with timing |
| `generate <template> --out <file>` / `export towns\|demo\|ember --out <file>` | write a generated / built-in map |
| `towns [--runs N] [--upgraded]`, `lines [...]`, `creatures [--write f.csv]` | town-army round robin; creature lines vs their tier; weekly power table |

Templates are named by name (`"Two against two"`), file key (`2v2`, `duel`, `ffa-4`) or path.
Debug env vars: `EW_SIM_EXPLAIN=10,25` (on those days, each hero's best targets as the AI sees them,
`AdventureAi.Explain`), `EW_SIM_GUARDS=1` (every monster stack, explored or not), `EW_SIM_TRACE=1` (battles, defeated heroes).

## Godot smoke tests
`./tools/smoke-godot.ps1 -Godot <Godot .NET console exe> [-Only 'game-*']` runs the scenarios listed in the script under
Movie Maker and saves frames to `.shots/<scenario>/`. **A pass only means no errors: look at the frames.**
- Script entries are `frame:kind:argument`: `click` / `rclick` (`x,y`), `key` (Godot key name), `hold` (`Key,frames`),
  `wheel` (`up|down,count`), `drag` / `rdrag` (`x1,y1,x2,y2`, over three frames). Replayed by `AutoInput`.
- Game scenarios start with `--new-game` and `key:Enter` (Start has focus), so they do not depend on the screen layout;
  all start from default settings.
- Maps live in `tools/smoke/` (or come from `sim generate` / `sim export`); `SmokeMapTests` checks they still do what
  the scripts expect.

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
| Move hero | Click tile (preview), click again; arrows / numpad | — | Same |
| Level | L or button | L or button | — |
| Camera | WASD pan, Q/E rotate, wheel zoom | same | — |
| Edit | — | LMB with the active tool; Ctrl+Z / Ctrl+Y | — |
| Battle | Point to preview, click move / attack (from the pointer's side) / shoot; W wait, D defend, R retreat, Q quick combat; wheel zoom; Enter / Esc leave the result | — | Click; same keys |
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
| `growth`, `gold`, `resources` | weekly growth; gold cost; other costs like `mercury 1` |
| `tags` | words separated by `;` (`human; holy`) |
| `capabilities` | up to five, `;`-separated: `Flying`, `PackHunter 20`, `SpellImmunity mind`, `Caster fireball 1`, … (`CapabilityKind`) |

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

`dwelling-1` … `dwelling-5` price the dwellings of a tier (level 1 builds, 2 upgrades) for every line of that tier; the
lines come from the creature table. The 7-of-9 rule (ADR-030) is in code.

### Editing heroes
`heroes.csv` (ADR-048).

| Column | Meaning |
|---|---|
| `id`, `name`, `town` | `town.name`; display name; town id |
| `class` | `Warrior` or `Mage` |
| `preferred` | base id of a tier 1–4 line of the same town; each line has one mage and one warrior |
| `specialty` | `skill <id>`, `spell <id>` or `resource <resource> <amount a day>` |
| `skills` | `offense.melee Basic; tactics.tactics Advanced` (ids in `BuiltInSkills`) |
| `attack`, `defense`, `spell_power`, `knowledge` | starting primary stats (empty = 0) |
