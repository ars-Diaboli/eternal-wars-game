# Active context

**As of 2026-11-21.** Refresh this file at the end of every piece of work: replace, do not append.

## Where the project is
Phases 0–4 are done and closed out (ADR-114, ADR-117). Phase 5 (content and polish) is in progress: magic, sieges, relics, map events,
the owner's missing-mechanics picks, the arena and LAN multiplayer are built. Phase 6 has begun: campaigns and story events (ADR-241,
ADR-242). The November refactoring plan is closed (ADR-226; the plan is in [docs/papers](../docs/papers/refactoring-plan-2026-11.md)).
Detail: [roadmap.md](roadmap.md).

## Last work
- Editor overhaul, step 2 tasks 7 and 8 (ADR-252); step 2 is complete. Scenery orientation: `Orientation` (mirrored, then 0 to 3
  quarter turns clockwise) on `Scenery`; `MapObject.Footprint` / `KnownObject.Footprint` are the shape as it lies
  (`Footprint.Oriented`), read by the cell index, the fog's memory, the minimap and `MapObjectsView`; `ObjectEditor.Rotate` /
  `Mirror` / `CheckTurn` (`SceneryTurner`: round the middle of the box, all or none, one step, `MapObjectMoved` per piece) and
  `SelectionEditor.Rotate` / `Mirror`. Map format 43 (`turns`, `mirrored` on scenery; step `OlderSceneryIsNotTurned`; sample v43;
  stamps of an older map format are upgraded on reading, sample `v01-map42`). Scatter: `ScatterEditor` in Core.Generation
  (`ScatterOptions`: density, kinds, keep paths open; `SceneryPlacer.Pick`), through `MapEditorSession.AsOneStep(description,
  random => …)`, whose undo restores the random state. UI: `ScatterTool` (no key: S pans the camera; scatters on release over the stroke's tiles), commands
  Rotate scenery (Ctrl+R) and Mirror scenery (Ctrl+M). Tests `SceneryOrientationTests`, `ScatterEditorTests`, cases in
  `EditorUndoTests`; smoke `editor-scatter-rotate`. Next: step 3 of the plan.
- Editor overhaul, step 2 tasks 9 and 10 (ADR-250, ADR-251). Core:
  `MapEditorSession.Resize(width, height, anchor)`, `Crop()`, `Shift(dx, dy)` and their `Check…` (a `ResizeCheck`: the new frame and
  what would leave the map), all through `MapResizer`: what is outside is removed by the other editors, then one command reframes
  the map in place (`AdventureMap.Reframed` / `TakeFrom`, `GameState.Reframe`: objects, heroes, the guard index), moves the event
  cells and the places event actions name, moves or drops the Grail, and publishes `MapResized`. Sizes 16 to 256 a side
  (`MapSizes.MinDimension`), square or not. Data: stamps (`Core.Data/Stamps`: `StampDocument` v1, `StampSerializer`, `StampFolder`;
  pinned sample in `tests/.../Data/StampSamples`). UI: `ResizeWindow` (size, presets, anchor, shift, the list of what is removed),
  `NameBox`, commands Resize map, Crop to selection, Save selection as stamp; the Object tool lists the stamps and hands one to the
  Paste tool; the app builds the world's views again on `MapResized` (a new `MapViewContext`). Tests `EditorResizeTests`,
  `StampFormatTests`, cases in `EditorUndoTests`; smokes `editor-resize`, `editor-select-move-paste`.
- Editor overhaul, step 2 tasks 4 to 6 (ADR-250). Core: `MapFragment` and `FragmentEditor` (`session.Fragments`: `Copy`, `Cut`,
  `CheckPaste`, `Paste` with `FragmentLayers`, `DuplicateAt`; new ids, links remapped inside, dropped to outside; no heroes, start
  zones or event cells); objects are copied through `IMapObjectCopier`, implemented in Core.Data by `MapObjectCopier` over the map
  format's codecs and passed to `MapEditorSession` (`copier:`; without one, copying an object throws). `Brush` (size 1 to 9, square or
  round, `Line`), `TerrainEditor` paints tile sets and has `Fill`; `EraseEditor` (`session.Erasing`: `Erase` under tiles,
  `EraseSelection`, `EraseKinds`); `MapEventEditor.RemoveCells`; `EditorContext.AsOneStep` for operations made of several edits.
  UI: `BrushTool` paints along lines, Shift + click line, brush outline (`SelectionView.ShowBrush`), `TileBrushTool` with the shared
  `EditorToolContext.Brush`; Terrain tool (T) with Fill; `EraseTool` (E) and `PasteTool` in `Tools/FragmentTools.cs`; commands Cut,
  Copy, Paste, Duplicate; the clipboard (`EditorClipboard`) is the app's and outlives the map. A key that ran a command is ignored by the camera while held (E is Erase and
  the camera's turn key). Open for the owner: a cut and paste gives new ids, so conditions and events that name the object lose it. Tests `EditorFragmentTests`, cases in `EditorUndoTests`; smoke
  `editor-clipboard`.
- Editor overhaul, step 2 tasks 1 to 3 (ADR-250). Core: `MapSelection` (tiles, objects, heroes, event names) on the session,
  changed only through `session.Selecting` (`SelectionEditor`: `At`, `In` a `TileArea`, `OfKind`, `Everything` with a
  `SelectionFilter`; `Set` / `Add` / `Remove` / `Clear` publish `SelectionChanged`, no undo step; `Delete` and `Move` are one step);
  a removed thing leaves the selection. `MapMover` checks and makes moves under placement's rules and levels the ground;
  `ObjectEditor.Move`, `HeroEditor.Move`; events `MapObjectMoved`, `HeroRepositioned` (`MapObject.Position` has an internal setter,
  used only while the object is off the cell index). UI: `SelectTool` (V) and `MoveTool` (M) in `Tools/SelectTools.cs`,
  `SelectionView` (Godot.Shared) for outlines, handles, marquee and ghost; `EditorToolContext` carries the press's modifiers
  (`PointerPress`), the filter and the visible area; commands Select all (Ctrl+A, the shown level), Deselect (Escape), Delete; tools may have a
  shortcut key. Plain keys run no command while a text field has the focus. Tests `EditorSelectionTests`, cases in
  `EditorUndoTests`; smoke `editor-selection`.
- Editor overhaul, step 1 tasks 10 to 12 (ADR-250), which close step 1. `CommandPalette` (Ctrl+Shift+P; `CommandSearch` in core
  ranks the names) offers the commands that can run now. `EditorPreferences` (core record with its ranges) are edited in
  `PreferencesWindow` (Ctrl+,), kept by `PreferencesStore` in `EditorSettingsFile` and applied by the app: the window's
  `ContentScaleFactor`, `FileService.AutosaveMinutes`, the camera's `PanSpeed`, `RotateSpeedDegrees` and new `ZoomStep`, the size
  of a new map. The editor runs `UiTheme.Apply(compact: true)` (14 px font, tighter spacing); `PlaceholderIcons` draws a tile with
  two letters for every tool and command without art (menus, toolbar, tool palette, command palette). `EditorWorkspace` has pages
  with tabs after the menus (`AddPage`, `ShowPage`): Map (the regions) and Template (`TemplateEditorPanel`, no longer an overlay;
  its Close button and Escape are gone); map-view commands are greyed on the Template tab. The palette, Preferences and the
  `QuestionBox` share `EditorOverlay`. `StrategyCamera` ignores its keys while a text field is edited (the game too) and while the app says so (`KeysBlocked`: an overlay, the Template tab). The template panel is made once and moved into each new workspace.
  `AutoInput` takes `key:Ctrl+Shift+P`. The compact theme moved the `editor-*` smoke clicks; new smokes `editor-palette` and
  `editor-preferences`.
- Editor overhaul, step 1 tasks 8 and 9 (ADR-250): `FileService` (`Godot.UI/Editor/Shell/`) owns the map's file: Open, Save, Save
  as (Godot's `FileDialog`, native unless the run is scripted), Open recent (`RecentFiles`, ten, in `EditorSettingsFile`, which the
  workspace's layout shares), the title's asterisk, the question over unsaved work (`QuestionBox`: New, From template, Open, Quit,
  the window's close request), the autosave every 2 minutes and the recovery offer at start. Every map the app shows goes through
  it (`Start`, `OpenNew`). It reads and writes through `IMapFiles`, which the app implements (`MapFiles`) with the map format and
  `MapAutosaves` (Core.Data: the newest five autosaves, the session marker with its process id and the last safe time).
  `MapEditorSession.MarkUnsaved` marks a restored map as unsaved work. The name field is gone. With `--settings=` the editor keeps
  its maps folder and autosaves beside that file. Smokes `editor-files` and `editor-recovery`.
- Editor overhaul, step 1 task 7 (ADR-250): `UndoHistory` remembers the position saved at (`MarkSaved`), so
  `MapEditorSession.IsDirty` is exact (an undo back to the saved step is clean; a step recorded over the saved one, or the saved
  one dropped from a full history, stays dirty until the next save); `UnsavedWorkChanged` is published when it flips. The editor's
  status bar reads it. Tests in `EditorHistoryTests`.
- Editor overhaul, step 1 tasks 5 and 6 (ADR-250): the bottom region holds `ProblemsPanel` (the validator on a copy of the map, on a
  task, 0.2 s after the last change; counts, a filter by severity and by `IssueKind`, which the validator now sets on every
  `MapIssue` from its check), `HistoryPanel` (a click jumps, `UndoTo`) and `OutputPanel` (the messages, kept in an `EditorLog`);
  `StatusBarView` under the regions shows the pointed tile, the last message, the background work, the problem counts, the save
  state and the zoom (`StrategyCamera.ZoomPercent`). `ValidationPanel` and the top bar's status row are gone, so the `editor-*`
  smoke clicks moved 29 px up; new smoke `editor-bottom-panels`.
- Editor overhaul, step 1 task 4 (ADR-250): `UndoHistory` is a list of steps with a position (`Steps`, `Position`, `Count`);
  `MapEditorSession.UndoTo(step)` jumps there by single undos or redos and publishes one `UndoHistoryChanged`, which now carries the
  position and the count; tests in `EditorHistoryTests`.
- Editor overhaul, step 1 task 3 (ADR-250): `CommandRegistry` (`Godot.UI/Editor/Shell/`) holds every action of the editor as an
  `EditorCommand` (id, name, icon, shortcut, why it cannot run, whether it is on); `MenuBarView` and `ToolbarView` are built from it
  and the app hands it the unhandled keys. The file row and the workspace's own toggle buttons are gone; the map's name field stays
  beside the toolbar until the file dialogs (task 8). Test and Help are greyed placeholders. The top bar is 8 px taller, so the
  `editor-*` smoke clicks were re-aimed; `editor-shell` also opens the Edit and Tools menus.
- Editor overhaul, step 1 task 2 (ADR-250): the editor's tools are classes (`IEditorTool` in `Godot.UI/Editor/Tools/`: `BrushTool`
  for the five that paint, one class per tool, `EditorToolContext` for the session, the inspector and the status line); `ToolPalette`
  shows the tools it is given, and the app only routes press, drag, release and hover to the one in use. No change in behaviour.

- Editor overhaul, step 1 task 1 (ADR-250): `EditorWorkspace` (`Godot.UI/Editor/Shell/`) docks the editor's panels in three regions
  around the map (tools left, settings and inspector right, Validation bottom), with splitters, collapse, F11 and the layout kept in
  `editor-settings.cfg`; smoke `editor-shell`, the other `editor-*` scenarios re-aimed. The map is still drawn behind the regions and
  the camera centres on the window, not on the map view (the editor camera is step 4).
- ADR-250 (random maps and editor): EDIT-1 … EDIT-12 answered with the proposals (template undo, scenery rotation, new map
  content, play-test as a separate process, resize 16–256); [editor-plan.md](../docs/papers/editor-plan.md#implementation-plan) now
  has a step-by-step implementation plan; next is the step 0 greybox.
- ADR-249 (presentation): every prompt file reworked so a prompt is ready to paste: one block a sheet (cells written out),
  at most a lead and an item for variants, negatives joined in advance; no blocks borrowed from other files.
- ADR-248 (presentation): icon and portrait prompts painted realistically, not as cartoons: new style blocks, `ICON-NEG-TOON` on every
  painted sheet; the icons and crests already generated and cursor sets 1–3 are to be regenerated.
- ADR-247 (random maps and editor): the map and template editor overhaul is planned: one creator-app workspace, seven steps;
  [docs/papers/editor-plan.md](../docs/papers/editor-plan.md); nothing built.
- ADR-246 (presentation): the town screen overhaul is planned: a 3D town scene per faction with full town windows, six steps;
  [docs/papers/town-screen-plan.md](../docs/papers/town-screen-plan.md); questions TOWN-14 … TOWN-23 asked; nothing built.

## Next
1. A person's full game on Ember March (the Phase 3 exit's last check).
2. The prologue map (CAMP-1), as the first map of a campaign; Border Watch's story is a placeholder.
3. The remaining Phase 5 items in [roadmap.md](roadmap.md) ("Presentation and UI", "Town screen overhaul", "Editor overhaul",
   "Platform and art", "Roster"); the town screen overhaul starts with the owner's answers (TOWN-14 … TOWN-23), the editor
   overhaul continues with step 2 task 7 (the step 0 greybox of the wizard is still open).
4. The "Code health" list in [roadmap.md](roadmap.md): skills, heroes and town types as typed ids; `InfoSheets` as data once localisation
   is decided (ADR-239).

## Known problems
- Numbers marked *(placeholder)* in the ADRs and in [open-questions.md](open-questions.md) wait for play-testing.
