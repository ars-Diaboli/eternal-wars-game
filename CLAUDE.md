# CLAUDE.md

HoMM3-like 3D turn-based strategy. Godot 4.7.2 .NET, C# on .NET 10. Read `docs/technical/architecture.md` and
`docs/technical/event-system.md` before changing structure or events.

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
- Front-ends, AI, tools and tests change a running game only via `session.Execute(command)` (ADR-036); rules
  draw randomness only from `GameState.Random` (never `System.Random`).
- Editor changes go through `MapEditorSession` so they are undoable and publish events.
- Bump `MapDocument.CurrentFormatVersion` when the map format changes, and keep older versions loading: add a sample in
  `tests/.../Data/FormatSamples`, and an `IMapMigration` step (`MapMigrations`) when older files need upgrading (ADR-174).
- Start at `memory-bank/README.md` (project brief, active context, then only what the task needs). Human docs are in `docs/`
  (design, content, technical, papers); the memory bank points into them and never restates them.
- Record significant decisions as ADRs in `memory-bank/decisions/<category>.md` (next free number and every ADR's file are in
  `memory-bank/decisions/README.md`; read one entry, not whole files); update `memory-bank/roadmap.md` checkboxes and refresh
  `memory-bank/active-context.md` when work lands. Owner notes in `memory-bank/ideas.md` become an ADR or an
  `memory-bank/open-questions.md` question, then leave `ideas.md`. Code cites decisions by number (`ADR-024`), never by file.
- Docs state the current rules once; no change history in them (ADRs and git keep it).
