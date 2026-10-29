# Development guide

## Toolchain
- **.NET SDK 10** (pinned via `global.json`, `rollForward: latestFeature`)
- **Godot 4.7.2 .NET edition** (the "Godot Engine - .NET" download, not the standard build):
  https://godotengine.org/download/windows/
- IDE: Rider or Visual Studio 2026 / VS Code with C# Dev Kit

## First-time setup
```powershell
./tools/link-shared-assets.ps1   # mounts shared assets into each app as res://shared
dotnet build EternalWars.sln
dotnet test --solution EternalWars.sln
```

## Running the apps
Each app is a standalone Godot project:

| App | Folder |
|---|---|
| Game | `src/Apps/EternalWars.Game` |
| Map editor | `src/Apps/EternalWars.MapEditor` |
| 2D harness | `src/Apps/EternalWars.Harness2D` |

- **Godot editor**: *Import* the folder's `project.godot`, then press F5. On the first build Godot
  may generate an app-local `.sln` next to the project. It is fine to commit it.
- **Command line** (with `godot` = the .NET editor executable on PATH):
  ```powershell
  godot --path src/Apps/EternalWars.Game
  godot --path src/Apps/EternalWars.Game -- --map="$env:APPDATA/Godot/app_userdata/Eternal Wars Map Editor/maps/MyMap.ewmap.json"
  ```
- The map editor saves to `user://maps/` (on Windows: `%APPDATA%\Godot\app_userdata\Eternal Wars Map Editor\maps`).

## Headless simulator
No Godot needed; drives the core through commands only.
```powershell
dotnet run --project tools/EternalWars.Tools.Sim -- battle haven.pikeman:20,haven.archer:10 inferno.hell-hound:12 --runs 200
dotnet run --project tools/EternalWars.Tools.Sim -- record demo.ewreplay.json --days 7 --seed 1
dotnet run --project tools/EternalWars.Tools.Sim -- replay demo.ewreplay.json
dotnet run --project tools/EternalWars.Tools.Sim -- game --map towns --days 40 --runs 8   # the adventure AI plays every slot
dotnet run --project tools/EternalWars.Tools.Sim -- game --template duel --size 96 --runs 4 [--players N]   # the same on a new generated map per run
dotnet run --project tools/EternalWars.Tools.Sim -- maps [--template 2v2] --maps 6 --days 14   # what the CI generated-maps job runs
dotnet run --project tools/EternalWars.Tools.Sim -- towns --runs 32 [--upgraded]   # round robin of one-week town armies
dotnet run --project tools/EternalWars.Tools.Sim -- creatures [--write table.csv]  # each line's weekly power against its tier
dotnet run --project tools/EternalWars.Tools.Sim -- lines --runs 4 [--upgraded]    # every creature line against its tier
dotnet run --project tools/EternalWars.Tools.Sim -- layout duel --size 96 --seed 1 [--rects] [--terrain] [--seeds N]
dotnet run --project tools/EternalWars.Tools.Sim -- selftest   # what CI runs
```
`layout` takes a shipped template name or a `*.ewtemplate.json` path and prints the zone layout as text (`--rects`: the
Rectangles strategy; `--terrain`: the generated map with walls, passages, guards (`M`, Area-mode `A`), towns, mines, piles, chests (`c`),
artifacts (`a`) and roads (`+`); `--seeds N`: generate N seeds in a row through every stage and report the ones with problems and the time).
Templates are named by their name (`"Two against two"`) or file key (`2v2`, `duel`, `ffa-4`), or given as a path.
`game --template` generates a map per run (seed = run seed, size = `--size` or the template's smallest, `--players` or the
template's maximum, every town "Random", all computer players) as the new-game screen would, and prints the attempt kept and any
problems; `--record` saves the first game with its generation info. `maps` checks `--maps` maps per shipped template (or
`--template`), cycling through the template's sizes and from the most players down: each map must generate without problems and
the same way twice, every player's hero must be able to walk to every other player's start town (through guards and pickups, via
gates), the map validator must find no errors, and `--days` days of AI-vs-AI play must issue no refused command and replay through JSON to the same state. A failed line
names the template, size, seed and player count to reproduce it (`maps --template <t> --seed <s> --maps 1`, or `game --template`).
Any command takes `--creatures path.csv`.

## Controls (placeholder)
| | Game | Editor | Harness |
|---|---|---|---|
| Select hero | Click it, Tab, hero list | Select tool | Click it |
| Hero screen | H (selected hero), "Info" in the hero list; Escape closes | — | — |
| Town screen | T (next own town), town list, entering an own town; Escape closes | — | Click the town |
| Move hero | Click tile (route preview), click again to go; arrows / numpad | — | Same |
| Level | L or button (follows gates) | L or button | — |
| Camera | WASD pan, Q/E rotate (±45°), wheel zoom | same | — |
| Edit | — | LMB with the active tool (terrain, road, object, monster, hero, erase, link gates, select); Ctrl+Z / Ctrl+Y | — |
| Battle | 3D battle scene: point to preview, click to move / attack (from the side the pointer is on) / shoot; W wait, D defend, R retreat, Q quick combat (or the buttons), wheel zoom; Enter / Esc leave the result | — | Click: move / attack / shoot; W wait, D defend, R retreat, Q quick combat |
| Decisions | Prompt buttons, exchange screen | — | Prompt buttons, exchange screen |
| Save / load | F10 or the Menu button: list, name, Save / Load / Delete; F5 quick save, F9 quick load; in battle too; autosave at every turn start (two slots) | — | — |

## Conventions
- **Where does code go?** If it can be written without `using Godot;`, it goes in Tier 1.
- Namespaces follow folders: `EternalWars.Core.Domain.Heroes`, `EternalWars.Godot.Shared.Map`.
- Godot node classes are `partial` (required by Godot source generators).
- Library nodes are created in code and expose `Bind(...)` methods instead of `[Export]` wiring.
- Events: see [event-system.md](event-system.md). Past tense, `sealed record`, publish after mutation.
- State setters are `internal`; mutate state only through services. Front-ends, AI, tools and tests change a
  running game only with `session.Execute(command)` (ADR-036); never call `System.Random` in rules, draw from
  `GameState.Random`.
- Tests: xUnit v3 on Microsoft.Testing.Platform, `MethodUnderTest_Scenario[_Expected]` naming.
  Build adventure-map situations with `TestWorld` ASCII maps (`.` grass, `~` water, `#` rock,
  `s` swamp, `l` lava, `A`/`C` Blue heroes, `B` Red hero), `TestWorld.Road(...)` for roads, and
  `TestWorld.Start(state)`, which pre-reveals the map unless the test is about fog. TestWorld heroes carry
  `TestWorld.DefaultArmy()` (10 footmen, 1500 movement points). Issue commands with the `SessionCommands`
  helpers (`session.Step`, `session.MoveTo`, `session.EndTurn`, `session.AutoResolve`, ...). Rule-level battle
  tests place units exactly with `TestBattle`.
- Saved games are `*.ewsave.json` (ADR-058) in `user://saves` (Windows: `%APPDATA%\Godot\app_userdata\Eternal Wars\saves`): a game record plus a label. Loading replays the commands and checks the digest.
- The ranking tables (ADR-101, ADR-102) are `ranking.json` in the user data folder (`RankingFile`), each row's game record in `ranking/*.ewreplay.json` next to it.
- Map files are `*.ewmap.json` (format v22: terrain + optional road and river rows, teams, heroes with roster type,
  primary stats, skills, war machines, morale, luck, artifacts and armies, objects incl. one-way gates, artifact pickups, creature dwellings, marketplaces, resource providers, loot, buff and training sites, signs, prisons and other utility objects, creature banks, scenery, the third catalogue's objects, monsters with their guard mode, mine guards and towns, victory and defeat conditions, the hero limits; see the
  version history on `MapDocument`). Replays are `*.ewreplay.json`. Random map templates are `*.ewtemplate.json` (`MapTemplate`; the shipped ones are in
  `src/Core/data/templates`, embedded into `Core.Data`). Creature
  stats live in the creature table `src/Core/data/creatures.csv` (see "Editing creature
  stats" below; ADR-071); JSON definitions can still be layered on with `CreatureDefinitionSerializer`. Terrain rules can be overridden with `TerrainDefinitionSerializer`. Object definitions can be extended with a JSON list of
  definitions (`MapObjectDefinitionSerializer`); see `BuiltInMapObjects` for ids.
- Godot smoke tests: `./tools/smoke-godot.ps1 -Godot <Godot .NET console exe>` runs scripted input against each app
  (scenarios `harness-guard-battle`, `game-route-and-move`, `game-exchange-and-gate`, `game-battle-3d`, `game-town-screen`,
  `game-hero-screen`, `game-kingdom`, `game-weeks`, `game-thieves-guild`, `game-random-map`, `editor-tools`, `editor-generate`, `editor-week-events`)
  and saves frames to `.shots/`. In a fresh clone import the assets once per app first
  (`<Godot console exe> --headless --path src/Apps/<App> --import`), or every texture load fails. A passing run only means
  no errors: look at the frames too. The game scenarios start with `key:Enter` (the Start button has the focus), so they
  do not depend on the new-game screen's layout. Smoke maps live in `tools/smoke/`; `SmokeMapTests` checks that they still do what the
  scripts expect.
- CI (`.github/workflows/ci.yml`) runs `dotnet build` + `dotnet test` + `sim selftest` on every push to main and on PRs,
  and a parallel `generated-maps` job: `sim maps --maps 6 --days 14` (6 maps per shipped template, about 30 s).
- Core builds with warnings as errors; keep Godot projects warning-free as well.
- Commits: imperative mood, small and focused.
- Binary assets (`*.png`, `*.jpg`, `*.glb`, `*.fbx`, `*.wav`, `*.ogg`, fonts, ...) go through **Git LFS** (`.gitattributes`;
  run `git lfs install` once). Godot's `*.import` sidecars stay plain text. Exception: `textures/terrain/beach.png` is a
  plain blob because the GitHub LFS quota refused it.

## Editing creature stats

Every creature is one row of `src/Core/data/creatures.csv` (ADR-044; every table lives in `src/Core/data`, ADR-071). Edit it in any
spreadsheet or text editor; Excel's semicolon CSV works too. Columns, matched by header name:

| Column | Meaning |
|---|---|
| `id` | `town.kebab-name`; maps, saves and code refer to it, so rename with care |
| `name`, `town`, `tier` | display name; town id (empty for neutrals); tier 1–5 |
| `size` | `Small` (2×2 cells), `Medium` (3×3) or `Large` (4×4) |
| `upgrade` | id of the upgraded form (same town and tier), empty on upgrades and most neutrals |
| `attack`, `defense`, `min_damage`, `max_damage`, `health` | per creature |
| `speed`, `map_speed`, `initiative` | battlefield cells per turn; HoMM3-scale adventure speed; turn order |
| `shots` | ammunition; empty or 0 = melee |
| `morale`, `luck` | −5…+5, empty = 0; the hero's are added in battle (ADR-053) |
| `growth`, `gold`, `resources` | weekly growth; gold cost; other costs like `mercury 1` |
| `tags` | free words separated by `;` (`human; holy`) |
| `capabilities` | up to five, separated by `;`: `Flying`, `PackHunter 20`, `SpellImmunity mind`, `Caster fireball 1`, … (`CapabilityKind`) |
| `notes` | free text; ignored, like any extra column |

- **Game, editor and 2D harness** use the copy embedded at build time. To try an edit without a rebuild, start the app
  with `-- --creatures=path.csv` (also `--heroes=`, `--buildings=`, `--artifacts=`, `--artifact-sets=`); a mistake is
  reported in the Godot output with its line and column, and the embedded copy is used instead.
- **Simulator** without a rebuild: `dotnet run --project tools/EternalWars.Tools.Sim -- towns --creatures
  src/Core/data/creatures.csv`. `sim creatures` shows each line's strength against its
  tier, `sim lines` duels the lines of a tier, `sim towns` plays one-week town armies against each other.
- **Random map layouts**: `dotnet run --project tools/EternalWars.Tools.Sim -- layout duel --size 96 --seed 3 [--rects] [--terrain]`
  prints the zone layout of a template (a shipped name or a `*.ewtemplate.json` path) as text, one letter per zone, plus the
  area of every zone; `--terrain` prints the generated map instead.
- **Tests and the default catalog** use the copy embedded at build time; `dotnet test` rebuilds it. Tests check
  the table's rules (nine upgradeable lines per town, tiers getting stronger, no copy-pasted stat lines), and
  `DataTableSchemaTests` checks every shipped table's columns, ids and references.

## Editing artifacts

Artifacts are `src/Core/data/artifacts.csv` (ADR-061), with the sets in `artifact_sets.csv`, same file
rules and loading as the other tables (`--artifacts=` and `--artifact-sets=`; a mistake is reported with line and column
and the built-in copy is used).

| Column | Meaning |
|---|---|
| `id`, `name` | kebab-case id (maps and saves refer to it), display name |
| `slot` | `Head`, `Neck`, `Cloak`, `Body`, `Weapon`, `Shield`, `Ring` (two fit), `Boots` or `Misc` (three fit) |
| `tier` | `Minor`, `Major` or `Relic`: what a map pickup of that tier can roll |
| `set` | id of a row of `artifact_sets.csv`, or empty; wearing every piece of a set adds the set's effects |
| `effects` | separated by `;`: `attack 2`, `defense`, `spell-power`, `knowledge`, `morale`, `luck`, `movement 10` (percent), `movement-flat 300`, `battle-speed 1`, `melee-damage 10`, `ranged-damage 10`, `damage-taken 5` (percent less), `ranged-damage-taken 10`, `income gold 100`, `native-terrain swamp`, `terrain-penalty 25` (percent removed), `skill machines.ballistics 1`, `all-skills 1`; negative numbers work |
| `notes` | free text; effects the game cannot apply yet are marked "Not yet" here |

`artifact_sets.csv` has `id, name, effects, notes`; a set needs at least two pieces. Map objects `artifact.minor`,
`artifact.major` and `artifact.relic` roll their artifact at game start; the editor can fix one (`SetArtifactPickup`).

## Editing town buildings

Town buildings are `src/Core/data/buildings.csv` (ADR-046, ADR-050), one row per building level,
with the same file rules and loading as the creature table (`-- --buildings=path.csv` loads another one).

| Column | Meaning |
|---|---|
| `id`, `level`, `name` | building id (`town-hall`, `fort`, …); level 1, 2, … without gaps; display name of that level |
| `town` | empty: the row is shared by every town. A town type (`necropolis`): that town's own rows of the building replace all shared levels of it, or add a building only that town has (ADR-074) |
| `gold`, `resources` | cost: gold, and other resources like `lumber 5; ore 5` |
| `requires` | other buildings first: `blacksmith 1; fort 2`. `dwelling-3 1` is met by any tier-3 dwelling |
| `income` | paid every day while this is the highest level built: `gold 500`, `lumber 1; ore 1` |
| `notes` | free text |

`dwelling-1` … `dwelling-5` price the dwellings of a tier (level 1 builds, level 2 upgrades) for every creature line of
that tier; which lines a town has comes from the creature table. The 7-of-9 rule (ADR-030) is in code, not in the table.

## Editing heroes

The hero roster is `src/Core/data/heroes.csv` (ADR-048), with the same file rules and
loading as the creature table (`-- --heroes=path.csv` loads another one).

| Column | Meaning |
|---|---|
| `id`, `name`, `town` | `town.name`; display name; town id |
| `class` | `Warrior` or `Mage` |
| `preferred` | base id of a tier 1–4 creature line of the same town; each line has one mage and one warrior |
| `specialty` | `skill <skill id>`, `spell <spell id>` or `resource <resource> <amount a day>` |
| `skills` | starting skills: `offense.melee Basic; tactics.tactics Advanced` (ids in `BuiltInSkills`) |
| `attack`, `defense`, `spell_power`, `knowledge` | starting primary stats (empty = 0) |
| `notes` | free text |

