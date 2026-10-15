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

## Controls (placeholder)
| | Game | Editor | Harness |
|---|---|---|---|
| Move hero | Arrows / numpad (8-way) | — | Arrows / numpad, click adjacent tile |
| Camera | WASD pan, Q/E rotate (±45°), wheel zoom | same | — |
| Paint | — | LMB (choose terrain + radius in toolbar) | — |

## Conventions
- **Where does code go?** If it can be written without `using Godot;`, it goes in Tier 1.
- Namespaces follow folders: `EternalWars.Core.Domain.Heroes`, `EternalWars.Godot.Shared.Map`.
- Godot node classes are `partial` (required by Godot source generators).
- Library nodes are created in code and expose `Bind(...)` methods instead of `[Export]` wiring.
- Events: see [event-system.md](event-system.md). Past tense, `sealed record`, publish after mutation.
- State setters are `internal`; mutate state only through services.
- Tests: xUnit v3 on Microsoft.Testing.Platform, `MethodUnderTest_Scenario[_Expected]` naming.
  Build adventure-map situations with `TestWorld` ASCII maps (`.` grass, `~` water, `#` rock,
  `s` swamp, `l` lava, `A`/`C` Blue heroes, `B` Red hero), `TestWorld.Road(...)` for roads, and
  `TestWorld.Start(state)`, which pre-reveals the map unless the test is about fog.
- Map files are `*.ewmap.json` (format v3: terrain + optional road rows, teams, objects incl. one-way
  gates). Terrain rules can be overridden with `TerrainDefinitionSerializer`. Object definitions can be extended with a JSON list of
  definitions (`MapObjectDefinitionSerializer`); see `BuiltInMapObjects` for ids.
- CI (`.github/workflows/ci.yml`) runs `dotnet build` + `dotnet test` on every push to main and on PRs.
- Core builds with warnings as errors; keep Godot projects warning-free as well.
- Commits: imperative mood, small and focused.
- Binary assets (`*.glb`, `*.png`, `*.wav`, `*.ogg`, ...) go through **Git LFS** once we start adding
  them (to be configured with the first asset commit).
