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
dotnet run --project tools/EternalWars.Tools.Sim -- selftest   # what CI runs
```

## Controls (placeholder)
| | Game | Editor | Harness |
|---|---|---|---|
| Select hero | Click it, Tab, hero list | Select tool | Click it |
| Move hero | Click tile (route preview), click again to go; arrows / numpad | — | Same |
| Level | L or button (follows gates) | L or button | — |
| Camera | WASD pan, Q/E rotate (±45°), wheel zoom | same | — |
| Edit | — | LMB with the active tool (terrain, road, object, monster, hero, erase, link gates, select); Ctrl+Z / Ctrl+Y | — |
| Battle | Quick combat button | — | Click: move / attack / shoot; W wait, D defend, R retreat, Q quick combat |
| Decisions | Prompt buttons, exchange screen | — | Prompt buttons, exchange screen |

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
- Map files are `*.ewmap.json` (format v4: terrain + optional road rows, teams, guard mode, heroes with stats and
  armies, objects incl. one-way gates, monsters and mine guards). Replays are `*.ewreplay.json`. Creature
  stats live in the creature table `src/Godot/EternalWars.Godot.Assets/data/creatures.csv` (see "Editing creature
  stats" below); JSON definitions can still be layered on with `CreatureDefinitionSerializer`. Terrain rules can be overridden with `TerrainDefinitionSerializer`. Object definitions can be extended with a JSON list of
  definitions (`MapObjectDefinitionSerializer`); see `BuiltInMapObjects` for ids.
- CI (`.github/workflows/ci.yml`) runs `dotnet build` + `dotnet test` on every push to main and on PRs.
- Core builds with warnings as errors; keep Godot projects warning-free as well.
- Commits: imperative mood, small and focused.
- Binary assets (`*.glb`, `*.png`, `*.wav`, `*.ogg`, ...) go through **Git LFS** once we start adding
  them (to be configured with the first asset commit).

## Editing creature stats

Every creature is one row of `src/Godot/EternalWars.Godot.Assets/data/creatures.csv` (ADR-044). Edit it in any
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
| `growth`, `gold`, `resources` | weekly growth; gold cost; other costs like `mercury 1` |
| `tags` | free words separated by `;` (`human; holy`) |
| `capabilities` | up to five, separated by `;`: `Flying`, `PackHunter 20`, `SpellImmunity mind`, `Caster fireball 1`, … (`CapabilityKind`) |
| `notes` | free text; ignored, like any extra column |

- **Game, editor and 2D harness** read the file when they start: save and restart the app. A mistake is reported
  in the Godot output with its line and column, and the built-in copy is used instead. `-- --creatures=path.csv`
  loads another table.
- **Simulator** without a rebuild: `dotnet run --project tools/EternalWars.Tools.Sim -- towns --creatures
  src/Godot/EternalWars.Godot.Assets/data/creatures.csv`. `sim creatures` shows each line's strength against its
  tier, `sim lines` duels the lines of a tier, `sim towns` plays one-week town armies against each other.
- **Tests and the default catalog** use the copy embedded at build time; `dotnet test` rebuilds it. Tests check
  the table's rules (nine upgradeable lines per town, tiers getting stronger, no copy-pasted stat lines).

## Editing town buildings

Town buildings are `src/Godot/EternalWars.Godot.Assets/data/buildings.csv` (ADR-046, ADR-050), one row per building level,
with the same file rules and reload behaviour as the creature table (`-- --buildings=path.csv` loads another one).

| Column | Meaning |
|---|---|
| `id`, `level`, `name` | building id (`town-hall`, `fort`, …); level 1, 2, … without gaps; display name of that level |
| `gold`, `resources` | cost: gold, and other resources like `lumber 5; ore 5` |
| `requires` | other buildings first: `blacksmith 1; fort 2`. `dwelling-3 1` is met by any tier-3 dwelling |
| `income` | paid every day while this is the highest level built: `gold 500`, `lumber 1; ore 1` |
| `notes` | free text |

`dwelling-1` … `dwelling-5` price the dwellings of a tier (level 1 builds, level 2 upgrades) for every creature line of
that tier; which lines a town has comes from the creature table. The 7-of-9 rule (ADR-030) is in code, not in the table.

## Editing heroes

The hero roster is `src/Godot/EternalWars.Godot.Assets/data/heroes.csv` (ADR-048), with the same file rules and
reload behaviour as the creature table (`-- --heroes=path.csv` loads another one).

| Column | Meaning |
|---|---|
| `id`, `name`, `town` | `town.name`; display name; town id |
| `class` | `Warrior` or `Mage` |
| `preferred` | base id of a tier 1–4 creature line of the same town; each line has one mage and one warrior |
| `specialty` | `skill <skill id>`, `spell <spell id>` or `resource <resource> <amount a day>` |
| `skills` | starting skills: `offense.melee Basic; tactics.tactics Advanced` (ids in `BuiltInSkills`) |
| `attack`, `defense` | starting primary stats |
| `notes` | free text |

