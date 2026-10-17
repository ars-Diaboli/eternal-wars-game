# Tools

> Part of the [game design](overview.md). How to run them: [development.md](../technical/development.md).

## Map editor
A **separate standalone app**, not an in-game mode (ADR-023). Implemented in core (`MapEditorSession`, undo/redo) with
Godot UI (tool palette, inspector, properties, players, validation and conditions panels):
- terrain painting (brush radius, drag strokes) and roads;
- object placement and removal, gate linking, mine owners and guards, map dwellings;
- heroes (type, army, war machines, morale and luck, artifacts), monsters, towns (buildings, dwellings, garrison);
- player slots and teams, map name and description, victory and defeat conditions (ADR-057);
- validation.

Artifacts on heroes and artifact pickups are in the editor core; their inspector rows are pending. Later: events and
triggers, map resize, the **template editor** and "generate map from template" ([random-maps.md](random-maps.md)).

## 2D harness
- **Viewer** (Godot 2D app): a top-down debug front-end over the same core, for rule development and visual debugging
  of AI.
- **Headless simulator** (plain .NET console, no Godot, `tools/EternalWars.Tools.Sim`): battle statistics, scripted
  and AI-vs-AI games, replays, balance reports (towns, creatures, lines) and template layouts. It is fast enough for CI
  and for tuning balance and AI over thousands of games; random-map batch validation in CI is planned.
