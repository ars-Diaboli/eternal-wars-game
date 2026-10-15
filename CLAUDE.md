# CLAUDE.md

HoMM3-like 3D turn-based strategy. Godot 4.7.2 .NET, C# on .NET 10. Read `docs/architecture.md` and
`docs/event-system.md` before changing structure or events.

## Commands
- Build: `dotnet build EternalWars.sln`
- Test: `dotnet test --solution EternalWars.sln` (xUnit v3 on Microsoft.Testing.Platform)
- Link shared assets into apps (once per clone): `./tools/link-shared-assets.ps1`
- Godot editor is not required to build; it is required to run the apps.

## Rules
- `src/Core/**` must never use Godot (build fails if it does). Put any logic that can be engine-free there.
- State changes happen only in Domain services, which publish past-tense `sealed record` events *after*
  mutating. Views subscribe via `node.SubscribeWhileInTree<T>(...)`; they never poll.
- Godot node classes must be `partial`. Library nodes are created in code and configured via `Bind(...)`.
- Apps (`src/Apps/*`) contain composition and input mapping only.
- Add/adjust tests in `tests/EternalWars.Core.Tests` for every core rule change. Use `TestWorld`
  ASCII maps for adventure-map scenarios.
- Player choices go through `GameState.PendingDecision` + `DecisionRequested` (never callbacks).
- Editor changes go through `MapEditorSession` so they are undoable and publish events.
- Bump `MapDocument.CurrentFormatVersion` when the map format changes, and keep older versions loading.
- Record significant decisions in `docs/decisions.md`; update `docs/roadmap.md` checkboxes.
