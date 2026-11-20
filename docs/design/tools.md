# Tools

> Part of the [game design](overview.md). How to run them: [development.md](../technical/development.md).

## Map editor
A **separate standalone app**, not an in-game mode (ADR-023). Core `MapEditorSession` (undo / redo) with Godot UI (tool palette, inspector,
properties, players, validation and conditions panels):
- terrain painting (brush radius, drag strokes), roads and rivers; **start zones** (the Start zone tool: a player's zone or Erase, shown as
  tiles of the player's colour; MP-4, ADR-197), where a player's day stays its own in a simultaneous first week;
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

**The window** (EDIT-1, ADR-250): a bar on top (the menus, the toolbar with the map's name), three docked regions around the map
view, each a tab box, and a status bar at the bottom. The tools and the options of the one in use are on the left, the map's settings
and the inspector on the right (Map, Players, Conditions, Inspector, Events, Bans, Generate), Problems, History and Output at the
bottom. A region is resized with its splitter and collapsed with its
button on the toolbar or in the View menu; F11 hides them all and shows them again. A panel larger than its region scrolls. The
splitter positions, the collapsed regions and each region's open tab are kept in `editor-settings.cfg` in the user data folder.

**Commands**: every action is a command with a name, an optional icon and shortcut, and a check whether it can run now. The menus
(File, Edit, View, Map, Tools, Templates, Test, Help) and the toolbar are built from the commands; an item that cannot run is greyed
and says why in its tooltip. Shortcuts: Ctrl+Z undo, Ctrl+Y redo, L the next level, F11 the panels. The Map menu opens the map's
tabs, the Tools menu picks a tool. Test (F5) and Help (F1) are in their menus and greyed until they are built.

**The bottom panels**:
- **Problems**: the validator's issues, checked a moment after every change, in the background on a copy of the map. Two buttons with
  the counts show and hide the errors and the warnings; a list picks one kind (heroes, objects, ground, players, rules, content,
  events). A click on a problem shows its tile; a double click on an event's problem opens the event.
- **History**: the start, then every undo step, oldest first; the undone steps are dim. A click on a row jumps there.
- **Output**: the editor's messages with their time (what was saved, loaded or generated, why an edit was refused).

**The status bar**, left to right: the tile under the pointer (x, y, level, terrain, height, the hero, town or object there); the
last message; "Checking the map…" while the problems are being checked; the counts of errors and warnings (a click opens Problems);
the save state ("Not saved" for a new map, "No changes" after a load, "Saved" with the time, and "Unsaved changes" whenever the map
differs from the saved one: undoing back to the saved step clears it); the zoom in percent.

*Planned*: map resize, and the rest of an overhaul of the map and template editors into one creator-app workspace (ADR-247,
[plan](../papers/editor-plan.md)).

## 2D harness and simulator
- **Viewer** (Godot 2D app): a top-down debug front-end over the same core, for rule development and visual debugging of AI.
- **Headless simulator** (`tools/EternalWars.Tools.Sim`, plain .NET, no Godot): battle statistics, scripted and AI-vs-AI games on hand-made
  or generated maps, replays, balance reports (towns, creatures, lines), template layouts and [fairness](random-maps.md#pipeline). Fast
  enough for CI and for tuning over thousands of games; CI generates and plays every shipped template (`sim maps`).

## Data tables
Creatures, heroes, buildings, artifacts, spells, scenery and riddles are CSV tables in `src/Core/data` (JSON for nested data), embedded in
`EternalWars.Core.Domain` (ADR-071, DATA-1); designers edit a copy and load it with `--creatures=`-style flags
([development.md](../technical/development.md#editing-creature-stats)).
