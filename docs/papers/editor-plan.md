# Map and template editor overhaul: study and plan

The plan to rebuild the map editor and the template editor (ADR-247). The goal is a **creator app**: the quality, usability and style
people expect from a professional tool. In function it should match the HoMM3 editor with HotA's additions and the HoMM5 editor,
keeping this game's own rules and file formats.

This file has five parts. The **study** looks at what we have and what the references do. The **target** sets the bar. **The
workspace and its tools** describes every part of the new editor. **Technical design** covers how to build it. **Roadmap**,
**questions** and **risks** close it. Nothing here is built yet. The questions (EDIT-1 … EDIT-12) are also in
[open-questions.md](../../memory-bank/open-questions.md#tools-and-editor); each one has a proposal that the plan follows until the
owner answers. When every step is built, this file moves to `archive/` and the rules live in [tools.md](../design/tools.md) and
[random-maps.md](../design/random-maps.md).

## Study

### What the editor has today

The editor is a **standalone app** (ADR-023) over a core `MapEditorSession` whose edits are undoable commands (ADR-015). The core is
strong:
- terrain, roads, rivers, height, start zones;
- objects, monsters, heroes, towns, the Grail;
- players, teams and conditions;
- the event tree (ADR-129) and the bans (ADR-133);
- a validator;
- generation from templates (ADR-087).

The template editor (ADR-117) has a zone graph and a live validator. What holds both back is the **shell around the core**.

| Part today | What it does | What holds it back |
|---|---|---|
| Tool palette (`ToolPalette`) | A row of 12 text buttons (Terrain … Grail), a second row with the tool's options | No icons or shortcuts; options change the row's width; a brush's radius (0–5) and shape are its only options |
| Object placement | One drop-down of the 87 object kinds | No pictures, search, categories or favourites. Placed objects cannot be moved, only removed and placed again |
| Selection | The Select tool inspects one hero or object | No area selection, multi-select, copy, paste, duplicate or move |
| File row | A name field and New, New (2 levels), From template, Templates, Save, Load, Undo, Redo, Level, Grid | No file dialog (files go by name to `user://maps`), no recent files, no unsaved marker, no autosave, Save does not ask before overwriting |
| Panels | A fixed 560 px tab box on the right: Map, Players, Conditions, Inspector, Events, Bans, Validation, Generate | Cannot be resized or moved; the inspector and validation hide behind each other; long forms scroll inside a small box |
| Navigation | The strategy camera (WASD, QE, wheel), L for the level | No minimap, no "go to", no bookmarks; the camera is the game's, not tuned for editing |
| Overlays | The grid, the start zones, the event cells | No passability, owner, guard-strength or reach overlay; nothing to toggle per kind of object |
| Validation | A list; a click jumps to the tile or opens the event | No fix-its, filters or severity counts in sight while editing |
| Testing a map | None | Save, start the game, find the map, play; nothing jumps back |
| Map size | Set when the map is made (`BlankScenario`, 36 × 36 or by template) | No resize, crop or shift (*planned* in [tools.md](../design/tools.md)) |
| Template editor (`TemplateEditorPanel`, `TemplateGraphView`) | A full-screen overlay: the zone graph (drag, right-drag to connect, zoom, pan), an inspector for the template, a zone or a connection, the validator's issues, Generate with a size and seed | **No undo** (an owner's answer in ADR-117); terrains and town types only in the file; one map per Generate, so fairness over seeds needs the simulator |
| Look | The game theme (ADR-191), Godot's default fonts | Reads as a debug tool: text buttons, dense rows, no grouping |

### The HoMM3 editor and HotA's additions

- **Layout**: a menu bar and a toolbar. The map fills the middle. On the right are the minimap and the tool panel, with the object
  browser under it in tabs (towns, monsters, heroes, artifacts, resources, dwellings, objects and more). Each object is a picture, and
  a double click on a placed object opens its properties dialog.
- **Terrain tools**:
  - brushes of 1, 2 and 4 tiles, plus an area fill;
  - roads and rivers;
  - automatic terrain transitions;
  - an obstacle tool that fills an area with fitting scenery;
  - erase by kind (objects, obstacles, everything).
- **Map specifications**: the name, description, difficulty and size. Per player: human or computer, the allowed factions, the
  starting hero, a hero generated at the main town, and the AI's behaviour. Also the victory and loss conditions, teams, rumours,
  timed events, and the allowed heroes, artifacts, spells and skills.
- **Properties dialogs**: per object, the owner, a guard and its message, the contents (Pandora's box, events), the garrison and a
  town's buildings.
- **Validation**: a "Validate map" report.
- **HotA's editor adds**:
  - many more objects and their settings;
  - custom heroes per map;
  - the global bans;
  - its own random map templates and the template editor;
  - quality-of-life tools: show the blocked tiles, find an object, keyboard shortcuts, more brush sizes;
  - a much larger map list.
- **What it gets right**:
  - everything is a picture in a browser;
  - every object opens one dialog with all its settings;
  - the map-wide settings are one window with tabs;
  - the editor is fast on large maps.
- **What it gets wrong**:
  - modal dialogs block the map while they are open;
  - there is no copy or paste of areas in the classic editor;
  - testing means leaving the editor;
  - the tool panel is cramped.

### The HoMM5 editor

- **3D editing**:
  - the map is shown as in the game;
  - objects are placed freely with rotation (decoration) or on the grid (game objects);
  - a height brush and texture painting with layers;
  - plants and scatter brushes.
- **A property grid** for any selected object or several at once. A tree of object kinds with a 3D preview.
- **Logic**: regions, named objects and scripting (Lua) for scenario events, and tools to test the map in the game.
- **What it gets right**: the map looks as it plays; selecting, moving and multi-editing objects; decoration that is not tied to the
  grid.
- **What it gets wrong**: it is hard to learn, scripting needs a programmer, it is slow on big maps, and its errors are hard to
  read.

### Professional creator tools (the quality bar)

The workspace conventions people bring from tile and level editors, game engines, 3D tools and design apps:

- **One workspace**: resizable, collapsible panels whose layout is kept, with a menu bar, a toolbar and a status bar.
- **Commands everywhere**: every action is a command with a name, an icon and a shortcut, found in the menus, the toolbar, a right
  click and a **command palette** (Ctrl+Shift+P). Shortcuts can be rebound.
- **Selection is the centre**: click, marquee and lasso; Shift adds and Alt removes; then move, duplicate, copy, paste, delete and
  edit the whole selection in one inspector.
- **Undo for everything** with a history panel (jump to any step).
- **Never lose work**: an unsaved marker, autosave, crash recovery, and a question before closing.
- **Feedback that stays visible**: live problems with counts in the status bar; hover tells what a click will do.
- **Fast iteration**: test in place and come back to the same view.

### Gap analysis

| Feature | HoMM3 + HotA | HoMM5 | Ours today | Target |
|---|---|---|---|---|
| Object browser with pictures and search | Pictures, tabs | Tree, 3D preview | Drop-down of names | Categories, thumbnails, search, favourites, recent |
| Select, move, multi-select | Single only | Yes | Inspect one | Marquee, multi-select, move, multi-edit |
| Copy and paste areas, stamps | HotA partly | Yes | No | Yes, with stamps kept in a library |
| Terrain brushes | 1/2/4, fill | Soft brushes | Radius 0–5 | Sizes 1–9, square and round, fill, line |
| Obstacle fill | Yes | Scatter | No (the generator only) | Scatter scenery over an area (EDIT-6) |
| Erase by kind | Yes | Yes | One object a click | Erase by kind over a selection |
| Map resize, crop, shift | HotA | Yes | No | Yes (EDIT-8) |
| Minimap | Yes | Yes | No | Yes, click to go there |
| Overlays (blocked, owners, zones) | Blocked only | Some | Grid, start zones | Passability, owners, guard strength, reach from starts, zones |
| Property dialogs or inspector | Modal dialogs | Property grid | Tabbed forms in a small box | A docked inspector, sections, multi-edit |
| Map settings, players, conditions | Tabbed window | Yes | Three tabs | One "Map settings" window plus wizard |
| Events and scripts | Timed events, rumours | Lua | A rich event tree (ADR-129) | The tree, plus a node view later (EDIT-7) |
| Validation | Report | Errors | Live list | Live, with counts, filters and fix-its |
| Balance and fairness report | No | No | Generator only (`sim`) | A fairness report for any map |
| Test the map | No | Yes | No | Play-test from the editor (EDIT-5) |
| Files | Dialogs | Dialogs | A name field | Dialogs, recent, autosave, recovery |
| Template editor | HotA (separate) | No | Graph, no undo | Graph with undo, presets, previews of many seeds |
| Shortcuts and command palette | Some | Some | Ctrl+Z / Y, L | Everything, rebindable |

## Target

The bar the overhaul is measured against. Each item can be checked; step 6 of the [roadmap](#roadmap) closes on them.

- **Discoverable**: every action is in a menu with its shortcut, and in the command palette. Every tool has an icon, a tooltip with
  its shortcut, and a status-bar hint saying what a click will do.
- **Direct**: a new user can make a playable two-player map in **15 minutes** using the new-map wizard and the browser, without
  reading a manual. This gets tested with one person.
- **Safe**: every change to the map and the template can be undone. An autosave every 2 minutes, crash recovery on the next start, and
  a question before closing or opening over unsaved work.
- **Fast**:
  - a 256 × 256 two-level map opens in under 3 s and keeps 60 fps while painting;
  - a brush stroke shows at once;
  - validation runs in the background and never stalls the editor.
- **Truthful**: the editor shows a map as the game will play it (the same views, the same rules), and the play-test is one click away.
- **One look**: the game's theme and fonts, compact for a tool. Dense but grouped, with consistent icons, spacing and a UI scale
  setting (100–200 %).

## The workspace and its tools

### Layout

```text
┌ File  Edit  View  Map  Tools  Templates  Test  Help ─────────────────────────────── [cmd palette] ┐
├─[New][Open][Save] │ [Undo][Redo] │ [Select][Terrain][Height][Road][River][Object][Erase]… │ [▶ Test] ┤
├───────────────┬───────────────────────────────────────────────────────────────┬──────────────────┤
│ TOOL OPTIONS  │                                                               │ MINIMAP          │
│ size ◯ □ 1–9  │                                                               │ [ click to go ]  │
│ terrain tiles │                  MAP VIEW (3D, as in the game)                ├──────────────────┤
├───────────────┤                                                               │ INSPECTOR        │
│ BROWSER       │       hover: the tile, what is there, what a click does       │ (selection,      │
│ [search…]     │       selection: outline + handles                            │  multi-edit,     │
│ ▸ Towns       │                                                               │  sections)       │
│ ▸ Monsters    │                                                               │                  │
│ ▸ Mines …     │                                                               ├──────────────────┤
│ ★ Favourites  ├───────────────────────────────────────────────────────────────┤ LAYERS/OVERLAYS  │
│ ⟲ Recent      │ PROBLEMS (3 ✗ 12 ⚠) │ HISTORY │ OUTPUT                       │ ☑ objects ☐ reach│
├───────────────┴───────────────────────────────────────────────────────────────┴──────────────────┤
│ x 41 y 17 L1 · grass, height 1.0 · Gold mine (Red)        ✗ 3  ⚠ 12   saved 2 min ago   100 %   │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Regions** (EDIT-1):
  - left: the tool options and the browser;
  - right: the minimap, the inspector, and the layers and overlays;
  - bottom: Problems, History and Output.

  Each region can be resized with a splitter, collapsed (F11 hides them all), and has tabs. The layout is kept in the editor's
  settings. No floating windows.
- **Menus**: File, Edit, View, Map, Tools, Templates, Test, Help. Every item is a command (see [commands](#commands-and-shortcuts)).
- **Status bar**:
  - the tile under the pointer (x, y, level), its terrain and height, and the object there;
  - the problem counts (a click opens Problems);
  - the save state, the zoom and the background work (validation, analysis).
- **Workspaces** (EDIT-3): **Map** and **Template**, as two tabs at the top of the window. Each keeps its own panels; switching keeps
  both open.

### Files

- **New map wizard**: the size (the presets 48 … 256, or a free size), levels, the default terrain, the number of players and their
  kind, and a template. It ends either on a blank map or in Generate.
- **Open / Save / Save as**: file dialogs (the system's own where Godot offers one), starting in the maps folder. **Recent** lists
  the last ten files. An asterisk in the title marks unsaved work.
- **Autosave**: every 2 minutes, to `user://maps/.autosave/` (the last five). After a crash the next start offers to restore.
- **Export**:
  - a map screenshot (the whole map, top-down, PNG);
  - the map's validation and fairness report (Markdown).
- **Close and quit**: ask over unsaved work.

### Map view and navigation

- The game's 3D view of the map, with an **editor camera**:
  - top-down (orthographic) or tilted, with F for focus on the selection;
  - Home to see the whole map;
  - the middle mouse button pans, the wheel zooms;
  - Ctrl+1 … 9 store bookmarks and 1 … 9 return to them.
- **Minimap**: drawn as in the game, with the view's frame. A click or drag moves the view.
- **Go to**: Ctrl+G goes to a tile (x, y, level) or an object by name or id, with search as you type.
- **Levels**: the surface and the underground (L, or the level switcher in the toolbar). Optionally the other level shows ghosted.

### Tools

| Tool | Key | What it does | Options |
|---|---|---|---|
| Select | V | Click, marquee (drag), Shift adds, Alt removes, double click selects all objects of that kind | Filter: objects, heroes, monsters, scenery, events |
| Move | M (or drag a selection) | Moves the selection on the grid; refused where it does not fit, the cells shown in red | — |
| Terrain | T | Paints a terrain; transitions as now (ADR-212) | Size 1–9, round or square, **fill** (flood fill a connected area of one terrain), **line** (Shift + click) |
| Height | H | Raise, lower, level, smooth, from terrain (ADR-182) | Size, strength, falloff |
| Road / River | R / I | Paints roads and rivers | Kind, size 1–3 |
| Start zone | Z | Paints a player's start zone (ADR-197) | The player, Erase |
| Object | O | Places the browser's selection; a ghost shows where it would go, red where it does not fit | Snap the entrance to a road, random variant |
| Scatter | S | Fills a brush area or the selection with scenery fitting the terrain, as the generator does (ADR-157) | Density, scenery kinds, keep paths open |
| Erase | E | Removes what is under the brush, by kind | Kinds: objects, scenery, monsters, roads, rivers, events |
| Link | K | Links gates, portals and one-way exits, drawn as arrows | — |
| Grail | G | Buries the Grail | — |
| Measure | U | The steps and days a hero would take between two tiles, along the route the game would take | The hero's movement points |

- **Every brush tool** shows its outline on the ground before the click. Every stroke is one undo step (as now).
- **The clipboard**: Ctrl+C, X and V, and Ctrl+D to duplicate. The copied area keeps its terrain, height, roads, rivers, objects,
  their settings and links inside the area. It is pasted as a ghost to place, with the option to paste only some layers.
- **Stamps**: save a selection to the **stamp library** (`user://stamps/`), such as a guarded mine with its road, or a town with its
  surroundings. Stamps appear in the browser like objects.

### Browser

- **Categories**:
  - towns;
  - heroes;
  - monsters (by faction and tier);
  - mines and providers;
  - dwellings (by faction);
  - treasure and pickups;
  - artifacts (by slot and tier);
  - power-ups;
  - utilities and travel;
  - banks;
  - events and quests;
  - scenery (by kind and terrain);
  - stamps.
- **Each entry** shows a thumbnail (rendered from its model or icon; placeholders until the art exists) and its name. Hovering shows
  a larger preview, its footprint and a one-line rule summary.
- **Search** across everything ("gold" finds the gold mine, the gold pile and the treasury). **Favourites** (★) and **Recent**.
- **Filter by terrain**: show only the scenery that suits the terrain under the pointer, or a chosen one.

### Inspector

- **The selection's properties in sections**:
  - identity (name, kind, owner);
  - contents (army, artifacts, resources);
  - guard;
  - messages;
  - links;
  - events.

  The existing `InspectorForm` sections become the basis. The forms are the same as today's (hero, town, monster, Pandora's box,
  sphinx, owned objects), redone in the new layout.
- **Multi-edit**: with several objects of one kind selected, a field changed applies to all of them (one undo step). Fields that
  differ show "—".
- **Pickers with pictures**: creatures, heroes, artifacts, spells and skills are chosen from searchable grids with their portraits and
  icons (from the prompt files, ADR-245), not from drop-downs.
- **Jump links**: a gate's partner, an event's cells and a quest's target are links. A click selects them and the view goes there.

### Map settings

- **One window with tabs** (opened from Map → Settings, or F7). It replaces today's Map, Players, Conditions and Bans tabs:
  - **General**: name, description, difficulty, hero limit, the arena switch;
  - **Players**: per colour, human or computer, team, allowed factions, start town and hero, and the AI's style;
  - **Victory and loss** (ADR-057);
  - **Bans** (ADR-133);
  - **Week events** (ADR-084);
  - **Rumours** (if EDIT-12 b is adopted).
- **Events**: the existing tree (ADR-129), which keeps its own tab in the right region. A **graph view** of triggers, conditions and
  actions is proposed for later (EDIT-7).

### Layers and overlays

- **Show / hide / lock** per kind: terrain, roads, rivers, objects, scenery, monsters, heroes, events, start zones, the grid. A locked
  kind cannot be selected or erased.
- **Overlays** (one at a time, with a legend):
  - **passability**: blocked cells, entrances and the cells around a monster;
  - **owners**: each object tinted in its owner's colour;
  - **guard strength**: monsters and guards by army power, as a heat map;
  - **reach**: from a chosen start, how many days a hero needs to every tile, using the game's pathfinding;
  - **zones**: the start zones and the zones of a generated map, read from its description;
  - **height**: contour lines.

### Problems, analysis and testing

- **Problems**: today's validator, run in the background after every change (debounced). Errors and warnings can be filtered by kind.
  A click selects the culprit, and a **fix** button appears where the fix is clear (link the unlinked gate, give the start town an
  owner).
- **Fairness report** (EDIT-9), for any map:
  - per start, the distance in days to the nearest towns, mines and resource providers;
  - the treasure value and guard strength within 3 and 7 days;
  - the start town's surroundings.

  A warning appears when the starts differ by more than a set share. The same numbers come from the core for the simulator.
- **Play-test** (F5, EDIT-5): saves to a temporary file and starts the game app on it, with options:
  - which player;
  - every other player computer or off;
  - reveal the map;
  - start at a chosen hero or tile;
  - the starting day;
  - a cheat panel (add resources, levels, creatures).

  The editor stays open. When the game closes, the editor is where it was.

### Template editor

The **Template** workspace tab, with the same shell:

- **The graph**:
  - zones as cards rather than plain circles: type, owner colour, size, towns, mine summary, monster strength;
  - connections as lines: kind, guard value, passages, chance;
  - snapping to a grid, align and spread, auto-arrange (as now);
  - multi-select with a marquee; copy, paste and duplicate zones with their inner connections;
  - mirror tools that make a zone's image and keep it in sync (`mirror`).
- **Undo for everything** (EDIT-2): every template change is one step with a name, shown in History.
- **The inspector** edits every field, including the ones only the file holds today (terrains, neutral town types), with pickers.
  **Zone presets** cover the common cases: rich treasure, mine junction, guarded start. User presets can be saved.
- **Preview**:
  - Generate shows the map in a **preview panel** without leaving the template: top-down, the zones outlined;
  - **Generate many** runs N seeds (default 20) in the background with the simulator's checks (`sim maps`: problems, determinism,
    starts reachable) and shows a **gallery** of thumbnails, with fairness numbers per seed and the spread;
  - **Open in map editor** takes one result into the Map workspace (as now).
- **Files**: open, save and save as, recent, autosave, as for maps. Shipped templates open read-only and are saved as a copy.

### Commands and shortcuts

- A **command registry**: each command has an id, a name, an icon, a default shortcut, a "can run now" check and its action. Menus,
  the toolbar, context menus and the **command palette** (Ctrl+Shift+P, search as you type) are built from it.
- **Shortcuts** are rebindable in Preferences (kept in the editor's settings file).
- Defaults follow common tools:

  | Keys | Command |
  |---|---|
  | Ctrl+N / O / S, Ctrl+Shift+S | New / Open / Save, Save as |
  | Ctrl+Z / Y | Undo / Redo |
  | Ctrl+C / X / V / D | Copy / Cut / Paste / Duplicate |
  | Delete | Delete |
  | Ctrl+A | Select all |
  | Escape | Drop the selection or the tool |
  | Ctrl+F | Find |
  | Ctrl+G | Go to |
  | F5 | Test |
  | F1 | Help on the hovered thing |
  | Single letters | Tools ([table above](#tools)) |

### Help and first steps

- **F1** opens the help page for the hovered tool, field or problem: short pages inside the app, made from the design docs' text.
- **Sample maps** open from the Help menu. The **new-map wizard** and the empty state ("Start with a template, or paint a terrain")
  guide a first map.
- **Preferences**: UI scale, the theme's contrast, autosave interval, camera speeds, shortcuts, default map size.

## Technical design

### Core (engine-free, tested)

The session already makes every edit an undoable step and publishes events. The overhaul adds:

- **Selection and clipboard**:
  - `MapSelection`: tiles plus objects, heroes and events;
  - `MapFragment`: an area's terrain, heights, roads, rivers, objects, settings and inner links;
  - `MapEditorSession.Copy`, `Paste` (with placement checks and new ids), `Move`, `Delete` and `DuplicateAt`, each one undo step.

  Stamps are fragments saved in a small file format of their own (versioned like the others, ADR-174).
- **Object moves**: `ObjectEditor.Move` and `HeroEditor.Move`, with the footprint checked and the ground levelled as on placement.
- **Map resize**: `MapEditorSession.Resize(width, height, anchor)`, plus crop and shift. Objects that end up outside are listed
  first, then removed in the same step. Heights and start zones are cut or extended.
- **Area tools**: `TerrainEditor.Fill`, `ScatterEditor` (the generator's scenery picker, ADR-157, reused on an area with the map's own
  random seed), and an erase by kind over a selection.
- **Multi-edit**: a property edit (`PropertyEdit`) over a list of objects as one step.
- **Analysis**:
  - `MapAnalysis` gives reach in days from a tile (the game's pathfinding), the fairness numbers per start, and the guard strength per
    cell;
  - `MapValidator` gives fix-its as edits.

  These are shared with the simulator, so the report and `sim` agree.
- **Templates**:
  - `TemplateEditorSession` gets an undo history like `UndoHistory`, which reverses ADR-117's "no undo" if EDIT-2 is accepted;
  - zone copy and paste, presets, mirror-keeping edits;
  - `TemplateBatch`: generate N seeds and return each map's checks and fairness, cancellable, off the main thread.
- **Map format**: stamps are a new file. Scenery rotation and mirroring (EDIT-6) would bump `MapDocument.CurrentFormatVersion`, with a
  format sample.

### Godot

- **The shell** (`EternalWars.Godot.UI/Editor/Shell/`):
  - `EditorWorkspace` (the regions as nested `SplitContainer`s with tab boxes, collapsible, the layout kept);
  - `CommandRegistry`, `MenuBarView`, `ToolbarView`, `CommandPalette`;
  - `StatusBarView`, `ProblemsPanel`, `HistoryPanel`, `OutputPanel`;
  - `FileService` (dialogs, recent, autosave, recovery), `Preferences`.
- **The app** (`src/Apps/EternalWars.MapEditor/Main.cs`) keeps only composition and input mapping (the project's rule). Today's 548
  lines shrink, because tools become classes with an interface (`IEditorTool`: activate, hover, press, drag, release, cancel, its
  options panel) and the app routes input to the active tool.
- **Views**:
  - `SelectionView` (outlines, handles, ghosts of pastes and placements);
  - `OverlayView` (one multimesh per overlay);
  - `MinimapWidget`, reused from the game;
  - an editor camera beside `StrategyCamera`.
- **Browser thumbnails**: rendered once from each object's model in a hidden `SubViewport`, cached in `user://cache/thumbs/` by the
  model's hash. Icons are used where they exist.
- **Background work**: validation, analysis and batch generation run on tasks over a snapshot (the map document), never on the live
  state. Results come back on the main thread.
- **Play-test**: start the game app as a process with `--map=<temp file> --player=<n> --reveal --start=<x,y,l> --cheats`. These
  launch arguments are new and the game app maps them in its composition (`LaunchArguments`).

### Tests and checks

- **Core**:
  - copy and paste round trips (paste in place gives the same map);
  - moves and resizes with objects on the edge;
  - undo of every new edit;
  - fill and scatter that are the same for a given seed;
  - fairness numbers on `TestWorld` maps;
  - template undo;
  - batch generation equal to single runs.
- **Smoke scenarios** (`tools/smoke-godot.ps1`), each saving screenshots:
  - `editor-shell` (layout, menus, palette);
  - `editor-select-move-paste`;
  - `editor-browser-place`;
  - `editor-resize`;
  - `editor-overlays`;
  - `editor-playtest-args` (the arguments a play-test would pass);
  - `editor-template-undo`;
  - `editor-template-batch`.
- **Performance**: a 256 × 256 two-level generated map, timed opening, painting and validation, kept in the roadmap's checks.

## Roadmap

Seven steps. Each ends in a working editor with a green test suite. The checkboxes are in
[roadmap.md](../../memory-bank/roadmap.md) under "Editor overhaul".

| Step | What | Depends on | Done when |
|---|---|---|---|
| 0 | **Decide and sketch**: answers to EDIT-1 … EDIT-12; a clickable greybox of the workspace and the new-map wizard | — | The owner signs off the layout |
| 1 | **The shell**: workspace regions, menus, toolbar, command registry and palette, status bar, Problems / History / Output, files (dialogs, recent, unsaved marker, autosave, recovery), preferences; today's panels moved in as they are | 0 | Every action of today's editor is a command in a menu; no work is lost on a crash |
| 2 | **Selection and editing**: select, marquee, multi-select, move, copy, paste, duplicate, delete, erase by kind, fill, line, larger brushes, scatter, stamps, map resize / crop / shift | 1 | A map can be laid out by selecting and copying; resize is undoable |
| 3 | **Browser and inspector**: categories, thumbnails, search, favourites, recent; the inspector in sections with multi-edit and picture pickers; the map settings window; the new-map wizard | 1 | Nothing is picked from a plain drop-down any more |
| 4 | **Overlays, analysis and testing**: minimap, go to, bookmarks, the editor camera; layers and the overlays; problems with fix-its; the fairness report; play-test | 2, 3 | A map can be checked for fairness and played from the editor |
| 5 | **Template workspace**: undo, cards and snapping, multi-select and copy, presets, mirror tools, every field in the inspector, preview, Generate many with the gallery | 1 | A template can be built, checked over 20 seeds and opened in the map workspace without touching its file |
| 6 | **Polish**: F1 help, sample maps, UI scale, shortcut editor, performance on the largest maps, a first-use test with one person, the [target](#target) list | 1–5 | The target list is ticked; this file moves to `archive/` |

Steps 2, 3 and 5 can run in parallel once the shell exists.

## Questions

Each question also has an entry in [open-questions.md](../../memory-bank/open-questions.md#tools-and-editor). The plan follows the
**proposal** until the owner answers.

<a id="edit-1"></a>
**EDIT-1 The workspace layout.** Free docking (panels dragged anywhere, floating windows), or fixed regions that can be resized and
collapsed? **Proposal**: fixed regions (left, right, bottom) with tabs, splitters and collapse, the layout kept per user. Godot has no
docking for game UIs, and free docking costs much for little gain.

<a id="edit-2"></a>
**EDIT-2 Undo in the template editor.** ADR-117 recorded the owner's "no undo". Does a professional-grade editor change that?
**Proposal**: yes, undo for every template change, with a History panel like the map's.

<a id="edit-3"></a>
**EDIT-3 Where the template editor lives.** An overlay as today, a workspace tab in the same app, or an app of its own? **Proposal**: a
workspace tab ("Map" | "Template") in the map editor app. Both stay open, and a generated map goes to the Map tab.

<a id="edit-4"></a>
**EDIT-4 Several maps open at once.** Document tabs for more than one map? **Proposal**: no. One map and one template at a time, plus
Recent and fast switching. Copy and paste works between maps through the clipboard and stamps.

<a id="edit-5"></a>
**EDIT-5 Play-test.** (a) A separate game process or the game inside the editor? (b) Which options? **Proposal**: (a) a separate
process of the game app with new launch arguments; (b) the player, the others computer or off, reveal, start at a hero or tile, the
day, and a cheat panel only in play-tests.

<a id="edit-6"></a>
**EDIT-6 Decoration off the grid.** HoMM5 lets scenery be rotated and placed freely. Do we add rotation, mirroring or free placement
for scenery, a map format change? **Proposal**: rotation in 90° steps and mirroring for scenery only, on the grid, stored in the map
(a format bump). Game objects keep their fixed facing. No free placement.

<a id="edit-7"></a>
**EDIT-7 Scripting.** A scripting language (HoMM5's Lua) for map makers, or the event tree (ADR-115, ADR-129) only? **Proposal**: no
scripting language. The event tree stays, and a node-graph view of it comes after step 6.

<a id="edit-8"></a>
**EDIT-8 Map resize.** (a) Where is the map anchored? (b) What happens to objects outside? (c) Any size, or only the presets?
**Proposal**: (a) a nine-point anchor plus crop to a selection and shift; (b) listed, then removed in the same undo step; (c) any square
or rectangular size from 16 to 256, the presets offered first.

<a id="edit-9"></a>
**EDIT-9 What the fairness report measures.** **Proposal**: per start:
- days to the nearest town, mines and providers;
- the treasure value and the guard strength within 3 and 7 days;
- the resources around the start town.

A warning appears when one start's number is more than 20 % from the mean (a placeholder). The same code serves the generator's
fairness (ADR-117) where it fits.

<a id="edit-10"></a>
**EDIT-10 Importing HoMM3 or HotA maps and templates.** ADR-023's scope leaves template import out of 1.0. Maps too? **Proposal**: no
import of either, for 1.0 and after. The formats and content differ too much, and the maps are other people's work.

<a id="edit-11"></a>
**EDIT-11 Map text in several languages.** Should map names, descriptions, event texts and rumours have per-language texts?
**Proposal**: wait for the game's localisation decision (ADR-239). Until then one language, with all of a map's texts listed in one
"Texts" view so a later translation finds them.

<a id="edit-12"></a>
**EDIT-12 Map content the editor lacks.** Which of these join the map format?
- (a) Custom heroes per map: name, portrait, biography, skills.
- (b) Rumours for the tavern.
- (c) Per-player AI style.
- (d) Per-player allowed factions on a fixed map.
- (e) Placed objects named for events and quests.

**Proposal**: b, d, e yes; a yes for name, biography and starting skills (the portrait from the roster); c later, with the AI's
styles.

## Risks

| Risk | Effect | Answer |
|---|---|---|
| A shell rewrite breaks today's tools | The editor is unusable for weeks | Step 1 moves today's panels in unchanged; tools move to `IEditorTool` one at a time behind the same commands |
| Godot's UI for a dense tool | It feels like a game menu, not a tool | A compact editor theme variant, own widgets for property grids and pickers, a UI scale setting |
| Big maps are slow (256 × 256, two levels) | Painting and overlays stutter | Overlays as multimeshes; analysis and validation on snapshots in the background; timed checks in step 6 |
| Copy and paste with links and ids | Broken gates, duplicated ids | One `MapFragment` type, remapped ids, links kept inside the fragment only, round-trip tests |
| Batch generation hogs the machine | The editor freezes | Off the main thread, cancellable, a cap on parallel work |
| Thumbnails need art that does not exist yet | A browser of grey boxes | Placeholder renders of today's models, refreshed when the art lands (ADR-245) |
| Scope creep (scripting, docking, multi-document) | The overhaul never ends | EDIT-1, EDIT-4 and EDIT-7 keep them out; each new map feature gets its own ADR |
