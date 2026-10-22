# Tools

> Part of the [game design](overview.md). How to run them: [development.md](../technical/development.md).

## Map editor
A **separate standalone app**, not an in-game mode (ADR-023). Implemented in core (`MapEditorSession`, undo/redo) with
Godot UI (tool palette, inspector, properties, players, validation and conditions panels):
- terrain painting (brush radius, drag strokes) and roads;
- object placement and removal, gate linking, mine owners and guards, map dwellings;
- heroes (type, army, war machines, morale and luck, artifacts), monsters (incl. each stack's guard mode, ADR-065), towns (buildings, dwellings, garrison);
- player slots and teams, map name and description, victory and defeat conditions (ADR-057);
- validation.

Artifacts on heroes and artifact pickups are in the editor core; their inspector rows are pending. Later: events and
triggers, map resize, the **template editor** and "generate map from template" ([random-maps.md](random-maps.md)).

## 2D harness
- **Viewer** (Godot 2D app): a top-down debug front-end over the same core, for rule development and visual debugging
  of AI.
- **Headless simulator** (plain .NET console, no Godot, `tools/EternalWars.Tools.Sim`): battle statistics, scripted
  and AI-vs-AI games (on hand-made or generated maps), replays, balance reports (towns, creatures, lines) and template
  layouts. It is fast enough for CI and for tuning balance and AI over thousands of games; CI generates maps of every
  shipped template and plays them AI-vs-AI (`sim maps`). Fairness between start zones (distance to mines, treasure value, guard power on the way, within ±15 %; ADR-071, RMG-8)
  is decided but not built.

## Data tables
Creatures, heroes, buildings and artifacts are CSV tables (JSON for nested data). ADR-071 (DATA-1, not done yet) moves them
from `EternalWars.Godot.Assets/data` to a core-owned folder embedded in `EternalWars.Core.Domain`; designers edit a copy and
load it with `--creatures=`-style flags ([development.md](../technical/development.md#editing-creature-stats)).
