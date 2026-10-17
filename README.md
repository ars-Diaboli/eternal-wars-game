# Eternal Wars

A turn-based fantasy strategy game in the spirit of *Heroes of Might and Magic III*, in 3D.
Built with **Godot 4.7.2 (.NET)** and **C# / .NET 10**.

## Solution at a glance

| Tier | Projects | Depends on Godot? |
|---|---|---|
| 1 · Core | `EternalWars.Core.Events` (pub/sub), `EternalWars.Core.Domain` (rules), `EternalWars.Core.Data` (file formats), `EternalWars.Core.Generation` (random maps) | **No** (enforced by the build) |
| 2 · Godot libraries | `EternalWars.Godot.Shared` (views, camera, glue), `EternalWars.Godot.UI` (HUD/widgets), `EternalWars.Godot.Assets` (shared assets) | Yes |
| 3 · Apps | `EternalWars.Game`, `EternalWars.MapEditor`, `EternalWars.Harness2D` | Yes (Godot projects) |
| Tools | `EternalWars.Tools.Sim` (headless simulator: battle statistics, AI-vs-AI games on hand-made and generated maps, town and creature balance, random map layouts, replays, CI self-test and generated-map check) | No |

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
Phases 0–2 are done: adventure map, editor, armies, tactical battles with a 3D battle scene, commands and replays.
Phase 3 (towns, heroes, skills, artifacts, tavern, game flow, save/load, adventure AI) is mostly done. Phase 4 (random
maps) is in progress: template format and validator, and the generator stages layout, terrain, connections, towns and mines.
See [docs/project/roadmap.md](docs/project/roadmap.md).

## Documentation
Start at [docs/README.md](docs/README.md): game design, content, architecture, event system, development guide,
roadmap, decision log and open questions.
