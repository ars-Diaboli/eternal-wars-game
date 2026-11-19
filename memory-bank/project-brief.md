# Project brief

**Eternal Wars**: a turn-based dark-fantasy strategy game inspired by Heroes of Might and Magic III, in full 3D. HoMM-inspired, not a
clone (ADR-008): heroes on an adventure map, towns with weekly growth, tactical battles on a square grid with creatures of 1×1 to
4×4 cells. Modes: single player against the AI, hot-seat, LAN multiplayer and the arena before 1.0, a story campaign later; no
internet play. Vision and pillars: [docs/design/overview.md](../docs/design/overview.md).

## Stack
- Godot 4.7.2 .NET, C# on .NET 10 (ADR-001); xUnit v3 on Microsoft.Testing.Platform.
- Build `dotnet build EternalWars.sln`; test `dotnet test --solution EternalWars.sln`; Godot smoke runs `tools/smoke-godot.ps1 -Godot <console exe>`.
- Toolchain, commands, the simulator and conventions: [docs/technical/development.md](../docs/technical/development.md).

## Layout (three tiers, ADR-002; [architecture.md](../docs/technical/architecture.md))

| Tier | Projects | Notes |
|---|---|---|
| 1 Core (plain .NET) | `src/Core/EternalWars.Core.{Events,Domain,Data,Generation,Network}` | Rules, commands, events, AI, formats, random maps, LAN. **Never uses Godot** (the build fails if it does). |
| 2 Godot libraries | `src/Godot/EternalWars.Godot.{Shared,UI}`, `EternalWars.Godot.Assets` (no csproj) | Views, cameras, HUD, screens; code-first nodes configured by `Bind(...)` |
| 3 Apps | `src/Apps/EternalWars.{Game,MapEditor,Harness2D}` | Composition and input mapping only |
| Tests and tools | `tests/EternalWars.Core.Tests`, `tools/EternalWars.Tools.Sim`, `tools/*.ps1` | Tests cover Tier 1; the sim runs games headless |

## Hard rules (also in CLAUDE.md)
- State changes only in Domain services, which publish past-tense `sealed record` events after mutating; views subscribe with
  `SubscribeWhileInTree<T>` and never poll ([event-system.md](../docs/technical/event-system.md)).
- A running game changes only through `session.Execute(command)` (ADR-036); rules draw randomness only from `GameState.Random`.
- Player choices go through `GameState.PendingDecision` + `DecisionRequested`, never callbacks (ADR-014).
- Editor changes go through `MapEditorSession` (undoable, ADR-015).
- A map format change bumps `MapDocument.CurrentFormatVersion`, adds a sample under `tests/.../Data/FormatSamples` and a migration
  step when old files need upgrading (ADR-174).
- Every core rule change comes with tests (`TestWorld` ASCII maps for adventure-map cases).
- Numbers that live in `src/Core/data/*.csv` are quoted in the docs, not restated.
