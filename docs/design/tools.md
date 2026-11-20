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

**The window** (EDIT-1, ADR-250): a bar on top (the menus, the Map and Template tabs, the toolbar), three docked regions around the map
view, each a tab box, and a status bar at the bottom. The tools and the options of the one in use are on the left, the map's settings
and the inspector on the right (Map, Players, Conditions, Inspector, Events, Bans, Generate), Problems, History and Output at the
bottom. A region is resized with its splitter and collapsed with its
button on the toolbar or in the View menu; F11 hides them all and shows them again. A panel larger than its region scrolls. The
splitter positions, the collapsed regions and each region's open tab are kept in `editor-settings.cfg` in the user data folder.

**Map and Template tabs** (EDIT-3), after the menus: the Map tab is the map with its regions; the Template tab is the
[template editor](random-maps.md#template-editor), which fills the room between the bars. Both stay as they were left while the other
shows; the template editor also stays so when another map is opened. The File menu and its shortcuts always act on the map (the
template has its own Save button). Generating a map in the Template tab opens it in the Map tab. While the Template tab shows, the commands that work on the map's
view (undo and redo, the level, the grid, the regions, the tools) are greyed; a command that opens one of the map's panels shows the
Map tab.

**The look**: the game's theme (ADR-191) in a compact variant: the same colours, a 14 px font, tighter rows and gaps and lower buttons.
Every tool and command has an icon in the menus, on the toolbar and in the palette; where the art does not exist yet it is a
placeholder, a coloured tile with the name's two letters.

**Files** (File menu):
- **Open** (Ctrl+O), **Save** (Ctrl+S) and **Save as** (Ctrl+Shift+S) use a file dialog, the system's own where there is one. It
  starts in the maps folder (`user://maps`), or in the folder of the map's file. Save writes to the map's file; a map without a file
  asks for one. The dialog asks before it overwrites a file.
- **Open recent** lists the last ten files opened or saved, the newest first (kept in `editor-settings.cfg`).
- The window's title is the file's name (the map's name for a map without a file), with an asterisk in front while the map has
  unsaved work.
- **Unsaved work**: New (Ctrl+N), From template, Open, Quit (Ctrl+Q) and closing the window first ask "Save", "Don't save" or
  "Cancel". Escape is Cancel.
- **Autosave**: every 2 minutes (a preference), a map with unsaved work that changed since the last autosave is copied to `user://maps/.autosave/`;
  the newest five stay. The map's own file is not touched.
- **Recovery**: when the editor did not close properly and an autosave was made after the last save (or after the last map was
  opened), the next start offers to restore the newest one. A restored map has no file and counts as unsaved work. A second editor
  started while one runs offers nothing.

**Commands**: every action is a command with a name, an icon, an optional shortcut, and a check whether it can run now. The menus
(File, Edit, View, Map, Tools, Templates, Test, Help) and the toolbar are built from the commands; an item that cannot run is greyed
and says why in its tooltip. Shortcuts: the File menu's, Ctrl+Z undo, Ctrl+Y redo, Ctrl+A select all, Escape deselect, Delete, V the Select
tool, M the Move tool, L the next level, F11 the panels, Ctrl+Shift+P the command palette, Ctrl+, Preferences. The Map menu opens the map's
tabs, the Tools menu picks a tool. Test (F5) and Help (F1) are in their menus and greyed until they are built.

**Selection** (EDIT-1, ADR-250). The selection holds tiles, objects, heroes and map events. It is not an undo step, and a thing that
leaves the map leaves the selection.
- The **Select** tool (V): a click selects the hero, the object or the events of the cell under the pointer; a click on open ground
  drops the selection. A drag selects the tiles of the rectangle and what stands in it (an object with any of its cells there). Shift
  adds to the selection, Alt takes out of it. A double click on an object selects every object of its kind in view.
- The **filter** (five boxes in the tool's options: objects, heroes, monsters, scenery, events) says which kinds of things clicks,
  rectangles and Select all pick.
- **Select all** (Ctrl+A) selects every thing on the shown level that the filter lets through, **Deselect** (Escape) nothing. **Delete**
  removes the selected objects and heroes as one undo step; selected tiles and events stay.
- Selected tiles are tinted and outlined in blue; a selected object or hero has a yellow outline round its cells with a handle at
  each corner; a selected event's cells are outlined in violet.
- After a click, a rectangle or a move with these tools the status bar counts what is selected, and a selection whose only thing is
  one object or one hero opens in the inspector. Select all and Deselect leave the inspector as it is.
- While a text field has the focus, plain keys (letters, Delete, Escape) are the field's and run no command.

**Move**. A drag that starts on a selected object or hero moves everything selected by the same offset, on its level; released on
the tile it started from, it moves nothing. With the
**Move** tool (M) a drag on an object or hero that is not selected selects it alone and moves it.
- A ghost shows the cells the things would take: green where they fit, red where they do not. A move with a red cell is refused.
- The rules are placement's: every cell on the map; no cell on another object or hero (a hero may stand on its own town's entrance);
  an object's entrance and a hero's tile on passable terrain. Things that move together are not in each other's way.
- The ground under a moved object of more than one tile is levelled as placement does; the ground it left stays as it is.
- An object keeps its links (gates), owner and contents. A move is one undo step, which also puts the ground back.
- After a move the things stay selected; selected tiles and events are let go.

**The command palette** (Ctrl+Shift+P, or View > Command palette): a search field and the commands that can run now, each with its
icon and shortcut. Typing narrows the list as you type: every typed word must fit the name, a name that starts with a word comes
before one that has it at a word's start, then inside a word, then as letters in order ("svas" finds "Save as"). Up and Down move
through the list, Enter or a click runs the command, Escape closes the palette.

**Preferences** (Edit > Preferences, Ctrl+,), kept in `editor-settings.cfg` and applied on "OK":

| Preference | Range | At start |
|---|---|---|
| UI scale | 100, 125, 150, 175 or 200 % | 100 % |
| Autosave interval | 1 to 30 minutes | 2 |
| Camera move speed | 4 to 40 tiles a second (at 100 % zoom) | 12 |
| Camera turn speed | 30 to 180 degrees a second | 90 |
| Camera zoom speed | 1 to 5 (one notch of the wheel) | 3 |
| New map's size | 16 to 256 tiles a side | 36 |

"Defaults" puts the values of a new installation into the fields; "Cancel" or Escape changes nothing.

The camera keys do nothing while a text field is being typed into, while a question, a dialog, the palette or Preferences is open,
while Ctrl is held, and on the Template tab. A shortcut pressed in the middle of a stroke on the map is ignored.

**The bottom panels**:
- **Problems**: the validator's issues, checked a moment after every change, in the background on a copy of the map. Two buttons with
  the counts show and hide the errors and the warnings; a list picks one kind (heroes, objects, ground, players, rules, content,
  events). A click on a problem shows its tile; a double click on an event's problem opens the event.
- **History**: the start, then every undo step, oldest first; the undone steps are dim. A click on a row jumps there.
- **Output**: the editor's messages with their time (what was saved, opened, autosaved or generated, why an edit was refused).

**The status bar**, left to right: the tile under the pointer (x, y, level, terrain, height, the hero, town or object there); the
last message; "Checking the map…" while the problems are being checked; the counts of errors and warnings (a click opens Problems);
the save state ("Not saved" for a map without a file, "No changes" after a file is opened, "Saved" with the time, and "Unsaved changes" whenever the map
differs from the saved one: undoing back to the saved step clears it); the zoom in percent.

*Planned*: copy and paste, map resize, and the rest of an overhaul of the map and template editors into one creator-app workspace (ADR-247,
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
