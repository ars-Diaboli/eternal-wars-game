# Tools

> Part of the [game design](overview.md). How to run them: [development.md](../technical/development.md).

## Map editor
A **separate standalone app**, not an in-game mode (ADR-023). Core `MapEditorSession` (undo / redo) with Godot UI (tool palette, inspector,
properties, players, validation and conditions panels):
- terrain painting (brush radius, drag strokes), roads and rivers;
- the ground's height (ADR-181, ADR-182), the "Height" tool: raise, lower, level (towards the height where the stroke started) and smooth
  with a round brush (the radius, and a strength of 1–8 eighths of a cell width a dab, fading over one tile at the edge); "From terrain"
  under the brush and a "Heights from terrain" button for the whole map; an **"Auto height"** switch (on by default) that gives newly
  painted terrain its height there and about 3 tiles around. Placing an object of more than one tile levels the ground under it at its
  entrance's height, and painting near one levels it again. Each stroke or placement is one undo step. Heights are clamped to the
  [relief's limits](presentation.md#art-direction) (walkable 0–4, water below 0, impassable rock above 4) and nothing more; the validator
  does not warn about height;
- objects: placement and removal, gate linking, mine owners and guards, map dwellings, artifact pickups, riddles, witch huts, shrines;
- heroes (type, army, war machines, morale and luck, artifacts, spell book, placeholder), monsters (with guard mode, ADR-065; random by
  tier), towns (buildings, dwellings, garrison, guild spells, random faction);
- player slots and teams, map name and description, victory and defeat conditions (ADR-057);
- map events, the "Events" tab ([adventure-map.md](adventure-map.md#pandoras-box-and-map-events), ADR-129), and the map's bans, the "Bans"
  tab ([adventure-map.md](adventure-map.md#map-bans-and-random-placeholders), ADR-133);
- validation; [generate from a template and the template editor](random-maps.md#editor).

*Planned*: map resize.

## 2D harness and simulator
- **Viewer** (Godot 2D app): a top-down debug front-end over the same core, for rule development and visual debugging of AI.
- **Headless simulator** (`tools/EternalWars.Tools.Sim`, plain .NET, no Godot): battle statistics, scripted and AI-vs-AI games on hand-made
  or generated maps, replays, balance reports (towns, creatures, lines), template layouts and [fairness](random-maps.md#pipeline). Fast
  enough for CI and for tuning over thousands of games; CI generates and plays every shipped template (`sim maps`).

## Data tables
Creatures, heroes, buildings, artifacts, spells, scenery and riddles are CSV tables in `src/Core/data` (JSON for nested data), embedded in
`EternalWars.Core.Domain` (ADR-071, DATA-1); designers edit a copy and load it with `--creatures=`-style flags
([development.md](../technical/development.md#editing-creature-stats)).
