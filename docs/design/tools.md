# Tools

> Part of the [game design](overview.md). How to run them: [development.md](../technical/development.md).

## Map editor
A **separate standalone app**, not an in-game mode (ADR-023). Core `MapEditorSession` (undo / redo) with Godot UI (tool palette, inspector,
problems panel, map settings window):
- terrain painting, roads and rivers with a [brush](#editor-brushes) (drag strokes, lines, flood fill); **start zones** (the Start zone tool: a player's zone or Erase, shown as
  tiles of the player's colour; MP-4, ADR-197), where a player's day stays its own in a simultaneous first week;
- the ground's height (ADR-181, ADR-182), the "Height" tool: raise, lower, level (towards the height where the stroke started) and smooth
  with a round brush (the radius, and a strength of 1–8 eighths of a cell width a dab, fading over one tile at the edge); "From terrain"
  under the brush and a "Heights from terrain" button for the whole map; an **"Auto height"** switch (on by default) that gives newly
  painted terrain its height there and about 3 tiles around. Placing an object of more than one tile levels the ground under it at its
  entrance's height, and painting near one levels it again. Each stroke or placement is one undo step. Heights are clamped to the
  [relief's limits](presentation.md#art-direction) (walkable 0–4, water below 0, impassable rock above 4) and nothing more; the validator
  does not warn about height;
- the map's size: [resize, crop and shift](#editor-resize); [stamps](#editor-stamps) of map pieces;
- scenery: [scatter](#editor-scatter) over an area, [rotation and mirroring](#editor-scenery-turn);
- objects: placement from the [object browser](#editor-browser) and removal ([erase by kind](#editor-brushes)), gate linking, mine owners and guards, map dwellings, artifact pickups, riddles, witch huts, shrines;
- heroes (type, army, war machines, morale and luck, artifacts, spell book, placeholder), monsters (with guard mode, ADR-065; random by
  tier), towns (buildings, dwellings, garrison, guild spells, random faction);
- the map settings window: map name and description, player slots and teams, victory and defeat conditions (ADR-057), the map's bans
  ([adventure-map.md](adventure-map.md#map-bans-and-random-placeholders), ADR-133) and the week events;
- map events, the "Events" tab ([adventure-map.md](adventure-map.md#pandoras-box-and-map-events), ADR-129);
- validation; [generate from a template and the template editor](random-maps.md#editor).

**The window** (EDIT-1, ADR-250): a bar on top (the menus, the Map and Template tabs, the toolbar), three docked regions around the map
view, each a tab box, and a status bar at the bottom. The tools and the options of the one in use are on the left, the inspector
on the right (Inspector, Events, Generate), Problems, History and Output at the bottom. A region is resized with its splitter and collapsed with its
button on the toolbar or in the View menu; F11 hides them all and shows them again. A panel larger than its region scrolls. The
splitter positions, the collapsed regions and each region's open tab are kept in `editor-settings.cfg` in the user data folder.

**Map and Template tabs** (EDIT-3), after the menus: the Map tab is the map with its regions; the Template tab is the
[template editor](random-maps.md#template-editor), which fills the room between the bars. Both stay as they were left while the other
shows; the template editor also stays so when another map is opened. The File menu and its shortcuts always act on the map (the
template has its own Save button). Generating a map in the Template tab opens it in the Map tab. While the Template tab shows, the commands that work on the map's
view (undo and redo, the level, the grid, the regions, the tools) are greyed; a command that opens one of the map's panels or the map
settings window shows the Map tab.

**Map settings** (EDIT-1, ADR-256): one window over the editor, opened with Map → Map settings or F7 (the Map menu's other entries
open it at a tab). Its tabs:
- **General**: the map's name and description ("Apply name and description" sets both), its difficulty (ADR-101), the hero limit per
  player (ADR-068) and the arena switch (ADR-161);
- **Players**: the player slots (name, human or computer by default, team, own hero limit, the factions it may take; add a slot,
  remove the last), whether the setup screen may change teams, and the team counts it must keep to (ADR-117). The factions are a menu of
  the playable towns to tick, or "Any faction"; the new-game screen offers only the ticked ones (ADR-257);
- **Victory and loss** (ADR-057): the default rules and the special conditions; on an arena map they are kept but greyed out;
- **Bans** (ADR-133);
- **Week events** (ADR-084): on or off, the weeks without events at the start, the factions whose creatures the weekly and the monthly
  events pick;
- **Rumours** (ADR-257): one rumour a line; "Apply rumours" sets them. The game's taverns tell one a week, in this order;
- **Texts** (EDIT-11, ADR-258): every text of the map by group (the map, players, heroes, objects, rumours, events) with a search box,
  each edited in place and applied on Enter or when the field is left, as the edit of its own panel (its rules hold; the list also keeps
  the map's and players' names and an event's texts from being blank); a refused text stays in its field and the line above the list says
  why. Event and variable names are not listed: things are found by them.

Each change is an undo step at once, as in a panel. The window's Undo and Redo (also Ctrl+Z and Ctrl+Y outside a text field) take
back any step while it is open; nothing else in the editor takes input then. Close, Escape or F7 closes it; it opens again at the tab
shown last.

**The look**: the game's theme (ADR-191) in a compact variant: the same colours, a 14 px font, tighter rows and gaps and lower buttons.
Every tool and command has an icon in the menus, on the toolbar and in the palette; where the art does not exist yet it is a
placeholder, a coloured tile with the name's two letters.

**Files** (File menu):
- **New map** (Ctrl+N, ADR-259) opens the new-map wizard, in three steps: the size (the presets 48 × 48 to 256 × 256, or any size
  from 16 to 256 a side), the levels (the surface, or the surface and the underground) and the ground (a terrain a hero can walk on);
  the players (1 to 8), each a person or the computer by default; a template, or none. Without a template it makes a blank map: the
  ground, the underground below, the players named by colour, each with a hero. With one it ends in the Generate tab, set to the
  template at its size nearest the chosen one and to the players (the template decides the ground and the levels); Generate makes
  the map. Cancel or Escape closes the wizard.
- **The empty state**: the editor starts on a blank map with a card over it that offers the wizard, Open and the sample maps (Ember
  March, Two Towns and the arenas, each opened as a new, unsaved map). The map can be painted at once; the card goes at the first edit,
  with its close button or when another map opens.
- **Open** (Ctrl+O), **Save** (Ctrl+S) and **Save as** (Ctrl+Shift+S) use a file dialog, the system's own where there is one. It
  starts in the maps folder (`user://maps`), or in the folder of the map's file. Save writes to the map's file; a map without a file
  asks for one. The dialog asks before it overwrites a file.
- **Open recent** lists the last ten files opened or saved, the newest first (kept in `editor-settings.cfg`).
- The window's title is the file's name (the map's name for a map without a file), with an asterisk in front while the map has
  unsaved work.
- **Unsaved work**: making a new map (the wizard's blank map, Generate, a sample map), Open, Quit (Ctrl+Q) and closing the window first ask "Save", "Don't save" or
  "Cancel". Escape is Cancel.
- **Autosave**: every 2 minutes (a preference), a map with unsaved work that changed since the last autosave is copied to `user://maps/.autosave/`;
  the newest five stay. The map's own file is not touched.
- **Recovery**: when the editor did not close properly and an autosave was made after the last save (or after the last map was
  opened), the next start offers to restore the newest one. A restored map has no file and counts as unsaved work. A second editor
  started while one runs offers nothing.

**Commands**: every action is a command with a name, an icon, an optional shortcut, and a check whether it can run now. The menus
(File, Edit, View, Map, Tools, Templates, Test, Help) and the toolbar are built from the commands; an item that cannot run is greyed
and says why in its tooltip. Shortcuts: the File menu's, Ctrl+Z undo, Ctrl+Y redo, Ctrl+X cut, Ctrl+C copy, Ctrl+V paste, Ctrl+D duplicate, Delete, Ctrl+A select all, Escape
deselect, Ctrl+R rotate scenery, Ctrl+M mirror scenery, the tools' keys (T Terrain, O Object, E Erase, V Select, M Move), L the next level, F11 the panels, F7 the map settings, Ctrl+Shift+P the command palette, Ctrl+, Preferences. The Map
menu opens the map settings (at a tab) and the Events tab, the Tools menu picks a tool. Test (F5) and Help (F1) are in their menus and greyed until they are built.

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

**The clipboard** (EDIT-4, ADR-250). Copy (Ctrl+C) takes the selected tiles and objects as a fragment; Cut (Ctrl+X) also removes the
copied objects from the map (the terrain, roads, rivers and ground stay), as one undo step.
- A fragment holds the tiles' terrain, roads and rivers, the heights of their corners, and the objects with all their settings. A
  gate's link stays when both ends are in the fragment and is dropped otherwise. Heroes, start zones and event cells are not copied.
  Of a selection over several levels, the lowest level's part is taken.
- Paste (Ctrl+V) picks the **Paste** tool: the fragment's middle follows the pointer as a ghost (green cells, red where an object
  does not fit), every click pastes it once more, Escape goes back to the Select tool. Duplicate (Ctrl+D) does the same with a copy
  of the selection and leaves the clipboard as it was.
- The tool's five boxes (terrain, heights, roads, rivers, objects) say which layers a paste brings.
- A paste is one undo step. Its objects are checked as placement checks them (on the map, on no other object or hero, the entrance
  on passable terrain, counting the terrain the paste brings; the limits on obelisks and Eyes of the Magi); one that does not fit
  refuses the whole paste. Tiles that would land off the map are left out.
- Pasted objects get new ids, and the links between them point at the new copies. What names an object by its id (a victory
  condition's town, an event's town) does not follow a cut and paste; a move keeps the id.
- A pasted object loses what the map does not have: an owner that is not one of its players (it becomes neutral), a Pandora's box's
  event that is no hero event there, and a quest that is no quest there or that another quest object has already.
- The ground under a pasted object of more than one tile is levelled as placement does, and so is the ground of objects the pasted
  heights reach. When the heights are pasted, the terrain's Auto height does not shape the ground.
- What was pasted is selected. The clipboard is the editor's, not the map's: it stays when another map is opened, so a piece of one
  map can be pasted into another.

<a id="editor-stamps"></a>
**Stamps** (ADR-250, ADR-251). "Save selection as stamp" (Edit menu) asks for a name and writes a copy of the selection, a fragment
as Copy makes it, to a stamp file in the editor's `stamps` folder; a stamp saved under a name that exists replaces it.
- The object browser lists the saved stamps. A click on one puts it on the pointer as a paste does: the ghost, a click for every
  stamp, the layer boxes, Escape to end. The clipboard stays as it was.
- A stamp belongs to no map, so it can be used in every map. What a map does not have is taken from the stamped objects as from
  pasted ones.

<a id="editor-browser"></a>
**The object browser** (EDIT-1, ADR-253) stands in the left region, under the tools and the options of the tool in use. It lists
everything that can be put on the map. A click on an entry makes it the Object tool's choice and brings that tool in.
- **Categories**, each a heading that opens and closes: towns, heroes, monsters, mines and providers, dwellings, treasure, artifacts,
  power-ups, utilities and travel, banks, events and quests, scenery. Inside a category the entries stand in groups:
  - monsters and dwellings by faction (the neutrals last), in tier order; one entry per creature, a dwelling for base creatures only;
  - artifacts by slot, in tier order, after the three "random artifact" pickups that roll one of their tier;
  - mines, then providers, deposits and abandoned mines;
  - scenery by kind (trees, rocks, walls…).
- **Search**: typing lists the entries that fit, whatever their category. Every typed word must start a word of the name or one of
  the entry's search words (its category, group, resource, faction, tier as "tier3" or "t3", slot, colour, what a bank pays), or stand
  inside the name. Names that start with the word come first, entries found by a search word only come last: "gold" finds the gold
  mine, the gold pile and the dwarven treasury. At most 60 entries show; typing more narrows the list.
- **Terrain filter**: any terrain, the terrain under the pointer on the map, or a chosen one. It hides the scenery that is not made
  for the terrain; everything else suits every terrain.
- **★ Favourites and ⟲ Recent** stand first. A right click on an entry, or the ★ on the card, adds the entry to the
  favourites or takes it out. Recent holds the last 12 entries that were placed, the newest first. Both are kept in the editor's
  settings, so they are the same in every map.
- **Stamps**: the saved [stamps](#editor-stamps), under their own heading above the categories; the search lists, after the
  entries, the stamps whose name holds every typed word.
- **The card** at the bottom shows the entry under the pointer, or the chosen one: its picture, its footprint (the entrance
  lighter), its category and group, its size, a one-line rule and, for scenery, its terrains.
- **Pictures** (ADR-254, ADR-255): an entry shows its icon where one exists (a town's crest, a resource pile's resource, the hero),
  else a thumbnail of its model when no other entry is drawn alike, else a coloured tile with two letters of its name. A thumbnail
  is rendered once, the first time its entry is listed, and kept in `user://cache/thumbs/` under the model's hash; a model that
  changes gets a new picture, and deleting the folder only costs rendering again. Entries whose models are alike (every monster,
  the dwellings, the mines, the named artifacts) keep their letters until their own portraits and icons exist.

<a id="editor-inspector"></a>
**The inspector** (ADR-254) shows the selected hero or object in sections; a click on a section's heading closes or opens it, and
what is closed is kept in the editor's settings. A section shows only when the thing has something for it.
- **Identity**: what it is and whose (a hero's type and stats, a town's owner, name and faction, a monster's creature and number). A
  single selected object other than a town has a **Name** field (ADR-257): a name of its own, up to 60 characters and not only digits,
  applied on Enter or when the field is left, blank for none.
  The map events, the conditions and the quest form show it, and the quest form takes a monster's or a hero's name in place of its id;
  the map keeps the id, so a rename changes nothing. Names must be unique (Problems reports a given name that another object also has,
  ignoring case, a town named as its faction too; two such towns are not reported). A hero has its name, the roster hero whose portrait it shows ("(its own)" for its roster
  entry's), a biography for the hero screen, and its skills, each with a level ("(remove)" unlearns it) and "Add a skill…"; Tactics is
  with the stats.
- **Contents**: what it holds or gives (an army, spells, artifacts, buildings, a box's gifts, a chest's values).
- **Guard**: who must be fought for it (a garrison, a mine's or a box's guard, a monster's disposition and guard mode).
- **Messages**: the texts it shows (a sign, a box's message, a sphinx's riddle).
- **Links**: jump links to the things it is tied to. A click selects the linked object or hero, shows it in the inspector and
  moves the view there; a link to an event cell moves the view. The links are:
  - a gate's partner or exit, and for an exit every gate that leads to it;
  - what a quest object's quest names: the monster or hero to beat, the town to hold;
  - the cells that run the same event as a Pandora's box;
  - for a monster, a hero or a town an event names: the quest object, box or cell that runs that event.
- **Events**: the map events it runs (a box's hero event, a quest object's quest, a town's events), each with "Edit".

**Several objects** (ADR-255): when the selection is two or more objects of one kind (all monsters, all mines, all towns) and no
hero, the inspector shows them together. A field set there is set on every one of them as one undo step, and each keeps its own
values of the other fields; a field whose values differ shows "—" until it is set. If one of the objects refuses the value, none
of them changes. A mine's guard creature keeps each mine's own count; chests' gold and experience are typed after "Fixed values"
is ticked. The fields that only make sense for one object (a town's name, buildings, dwellings, guild spells and garrison,
a box's contents, guard and message, a garrison's troops, a scroll's spell) say "select one to edit". Shrines of different levels
are given a spell one level at a time. Objects of different kinds are listed by kind, with nothing to edit.

**Pickers** (ADR-255): creatures, heroes, artifacts, spells and skills are chosen with a button that shows the chosen one's picture
and name. A click opens a grid under the row: a search field (Enter takes the first found), and the items by group (creatures by
town, heroes by town and class, artifacts by tier or by slot, spells by level and school, skills by group), each with its picture.
The pictures are the lettered tiles until portraits and icons exist (ADR-245). The inspector and the Events tab's event form use
them.

**The Object tool** (O) places the browser's choice with its top-left tile where clicked; the treasure chest is chosen at the start.
Before the click a ghost shows the tiles it would take, red where it does not fit; all of them are red when the map may hold no
more of the thing (a player's heroes, the obelisks, the Eyes of one colour). A monster is placed aggressive with its number
left to the game's start, a dwelling and an artifact pickup already hold their creature or artifact (one undo step), and a hero is
placed for the player picked in the tool's options and opens in the inspector. The Monster and Hero tools place the same things with
their own options.

<a id="editor-scatter"></a>
**Scatter** (EDIT-6, ADR-252). The Scatter tool puts scenery on the tiles its brush is dragged over, when the button comes up;
"Scatter in selection" does the same over the selected tiles. The scenery is chosen as the random map generator chooses it
(ADR-157): for each tile a kind drawn by its weight on the tile's terrain, then that kind's largest piece that fits.
- **Free tiles only**: passable ground with no object, hero, road, river or event cell. A terrain none of the ticked kinds is made
  for stays free.
- **Density** (1 to 100 %, 35 at first): how many of the free tiles are covered. Pieces of several tiles lie where covered tiles
  are side by side.
- **Kinds**: a box for each kind of scenery (trees, rocks, walls…), with All and None.
- **Keep paths open** (on at first): a piece is left out where it would close a way between the tiles round it, or the way to an
  entrance beside it. Whatever could be walked to before can be walked to after.
- The draws come from the map's own random numbers, which start from the map's seed each time the map is opened: the same
  edits on the same map give the same scenery. A scatter is one undo step, and undoing it puts the random numbers back, so the
  same scatter comes again.

<a id="editor-scenery-turn"></a>
**Scenery rotation and mirroring** (EDIT-6, ADR-252). "Rotate scenery" (Ctrl+R) turns the selected scenery a quarter turn
clockwise, each piece round the middle of its own box; "Mirror scenery" (Ctrl+M) flips each left to right. Both are in the Edit
menu and are one undo step each.
- A turned piece takes the cells of its turned shape: a wall of 3 × 1 tiles becomes 1 × 3. Four turns bring it back to its place.
- The turned shapes must fit as placed objects do: on the map, on no other object or hero. When one piece does not fit, none is
  turned. Pieces turned together may take each other's old cells.
- Only scenery turns. Game objects keep their facing; selected with scenery, they stay as they are.
- The map file keeps how each piece lies; copies, pastes and stamps lie as their original. Scenery a random map is generated with
  is not turned.

<a id="editor-resize"></a>
**Resize, crop and shift** (EDIT-8, ADR-250, ADR-251). "Resize map" (Map menu) opens a window with the new width and height, 16 to
256 tiles each (square or not; the square presets are buttons), and nine anchor buttons that say where the map as it is stays in
the new one. The same window shifts everything by a number of tiles on a map of the same size. "Crop to selection" (Map menu) makes
the box round the selected tiles the map; the box must be at least 16 tiles a side.
- Ground that ends up outside is cut with its roads, rivers, heights and start zones. New ground is grass (subterranean below the
  surface) at its terrain's height.
- An object with any cell outside, a hero or an event cell outside is removed. A buried Grail that would lie nearer than 10 tiles
  to the new edge is taken away. When anything would be removed, the window lists it first ("Remove and resize", Back, Cancel).
- Everything is one undo step, the removals included. What stays keeps its ids, links and settings; the places event actions name
  (reveal, give a hero) move with the map, and the validator warns of one that is off the map. The selection is let go.
- With the middle anchor and an odd change of size, the odd tile is added (or cut) on the right and at the bottom.

<a id="editor-brushes"></a>
**Brushes** (EDIT-1, ADR-250). The Terrain, Road, River, Start zone and Erase tools share one brush: a size of 1 to 9 tiles across,
square or round.
- The tiles under the brush are outlined in white before the click. A stroke is one undo step.
- A drag paints along the straight line between the tiles the pointer passes, so a fast drag leaves no gaps. Shift + click draws a
  line from the tile painted last.
- **Fill** (a box in the Terrain tool): a click gives the terrain to the whole area of the clicked tile's terrain that is joined side
  by side (not across corners) on that level, as one undo step.
- The Height tool keeps its own round brush with a radius.
- A key that runs a command does not also move or turn the camera while it is held (E picks Erase and is the camera's turn key).
- The **Erase** tool (E) removes the ticked kinds under the brush: objects, scenery, monsters, heroes, roads, rivers, event cells
  (the first four are ticked at the start). An object goes when the brush touches any of its cells, and what linked to a removed gate
  loses its link. "Erase in selection" removes the ticked kinds over the selection instead: the selected objects and heroes, every
  cell of the selected events, and what is on the selected tiles.

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

*Planned*: the rest of an overhaul of the map and template editors into one creator-app workspace (ADR-247,
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
