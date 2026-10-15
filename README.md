# Eternal Wars

A turn-based fantasy strategy game in the spirit of *Heroes of Might and Magic III*, in 3D.
Built with **Godot 4.7.2 (.NET)** and **C# / .NET 10**.

## Solution at a glance

| Tier | Projects | Depends on Godot? |
|---|---|---|
| 1 · Core | `EternalWars.Core.Events` (pub/sub), `EternalWars.Core.Domain` (rules), `EternalWars.Core.Data` (file formats) | **No** (enforced by the build) |
| 2 · Godot libraries | `EternalWars.Godot.Shared` (views, camera, glue), `EternalWars.Godot.UI` (HUD/widgets), `EternalWars.Godot.Assets` (shared assets) | Yes |
| 3 · Apps | `EternalWars.Game`, `EternalWars.MapEditor`, `EternalWars.Harness2D` | Yes (Godot projects) |
| Tools | `EternalWars.Tools.Sim` (headless simulator: battle statistics, replays, CI self-test) | No |

## Quick start
```powershell
./tools/link-shared-assets.ps1
dotnet build EternalWars.sln
dotnet test --solution EternalWars.sln
dotnet run --project tools/EternalWars.Tools.Sim -- selftest
```
Then open `src/Apps/<App>/project.godot` in the Godot 4.7.2 .NET editor and press F5.
With Godot installed, `./tools/smoke-godot.ps1 -Godot <path to the console exe>` runs scripted smoke scenarios
of every app and saves screenshots to `.shots/`.

## Status
Phases 0 and 1 are done: all three apps run in Godot 4.7.2 (setup screen, fog, objects, path preview, minimap,
hero list, exchange, full editor UI). Phase 2 core (armies, tactical battles with HoMM4-style creature
capabilities, neutral guards, commands and replays) is done; the 3D battle scene is next (the game uses quick
combat meanwhile, the 2D harness has a battle board).
See [docs/roadmap.md](docs/roadmap.md).

## Documentation
Start at [docs/README.md](docs/README.md): game design, architecture, event system, roadmap,
development guide, decision log and open questions.
