# Active context

**As of 2026-11-22.** Refresh this file at the end of every piece of work: replace, do not append.

## Where the project is
Phases 0–4 are done and closed out (ADR-114, ADR-117). Phase 5 (content and polish) is in progress: magic, sieges, relics, map events,
the owner's missing-mechanics picks, the arena and LAN multiplayer are built. Phase 6 has begun: campaigns and story events (ADR-241,
ADR-242). The November refactoring plan is closed (ADR-226; the plan is in [docs/papers](../docs/papers/refactoring-plan-2026-11.md)).
Detail: [roadmap.md](roadmap.md).

## Last work
- Campaign heroes and the hero portraits redone (ADR-298): 48 campaign heroes (4 per town, 8 each for Conflux and Dragon
  Utopia) never drawn at random; 14 heroes renamed as men (about a third women); varied close-crop prompts. Generator version 13.
- Stronghold's Wolf Rider line moved to tier 2 and the Centaur line to tier 3, numbers swapped with the tier; generator version 12,
  maps re-pinned (ADR-295).
- The town screen plan's step 3, task 1 (ADR-297): `TownScene3D` behind the town screen's bars for town types with a layout (Haven
  so far, its style frame turned into the layout), plots read from `plot_*` markers and shown as the town has them (model, the
  layout's greybox or a block with a roof and a sign), hover and pick opening the windows, the adventure map put away meanwhile.
  Next is task 2 (the layouts of the other seven towns and `game-town-layouts`).
- The town screen plan's step 2 (ADR-296): the game's only town screen is the new one, its windows over the key painting with a strip
  of the town's buildings (Hall with columns by kind and requirement links, Recruitment and the dwelling window, Mage guild, Tavern,
  Marketplace, Blacksmith, Fort, Caravan, governor list, Transformer), the bars with Split, Upgrade all, Swap and Dismiss, rename and
  ◄ ►; in core `TownOverview`, `TownPlans`, Recruit all, Upgrade all, dismissing, renaming, the rumour table, resource for resource
  and sending to an ally. `TownPanel` stays in the 2D harness. Next is step 3 (the 3D scene in greybox).
- The town screen plan's steps 0 and 1 (ADR-294): the shell, Haven's greybox style frame (`scenes/towns/haven.tscn`), two font
  pairings for the owner's pick (`game-fonts-b`), nine-slice frames, the shared widgets, the heroes' swap and the stack spread. Open:
  the owner's font pick, the frame budget on the reference PC and the Deck.
- Sylvan creature prompts redone to the owner's review: faerie pixies, male saber dancers, huntress and male archer with
  recurve bows, ridden pegasi, ghostly moon doe, black dire wolf, no moss or leaves on bodies (ADR-293).
- The town screen plan's questions answered (ADR-292: the proposals, no creatures or hero models in the scene, art after the key
  paintings) and its roadmap broken into tasks per step ([implementation plan](../docs/papers/town-screen-plan.md#implementation-plan));
  next is step 0's greybox shell and Haven's style frame.
- The Capitol needs six dwellings of tiers 1–4 (ADR-291, owner's new rule with the TOWN-15 answer): in build, the town tree, the editor,
  the validator and the AI; the plan's plots show unused land when not built and citizens' homes on the two closed lines once the
  Capitol stands.
- The editor plan's second check (ADR-290): every section checked against the code; built what was left out: the gate links drawn as
  arrows while LinkGates is picked, Escape that drops the tool's half-done action, the selection or the tool, a problem's click that
  selects the culprit, the fairness numbers worked out in the background, the Monster and Hero tools' ghost, the owner on the status
  bar and Test on the toolbar. Smoke `editor-link-arrows`.
- Creature portraits in high contrast, plain bases and clearly upgraded upgrades on every sheet; Academy creatures redone to the
  owner's review (no teal, musket gremlin, female djinn sultan, living colossus and steel titan); the thanes, efreet and golden golem
  drawn as Academy by lore (A6), the specter and banshee as Necropolis (N5), the wolves as Sylvan (S5) and the gogs and breeders as
  Inferno (I5, I6), all still neutral in the game; one neutral sheet left (ADR-289).
- The editor app split (ADR-288): `Main` (about 540 lines, from 1452) makes the lasting parts, builds each map's views and panels and
  maps the input; the commands, menus and windows are composers by area (`FileComposer`, `EditComposer`, `MapComposer`,
  `ViewComposer`, `TestComposer`, `WindowComposer`, `GenerateComposer`, `MenuComposer`, `ContextMenuComposer`) over a shared
  `EditorParts`; folders in `EditorFolders`. No change in behaviour; every editor smoke scenario passes.
- Editor icons C4, C7, C12 and C13: the owner's sheets cut into `textures/ui/icons/` (22 files); every editor command, panel and tab
  now has its drawn icon, none a lettered placeholder.
- The other level ghosted and the height falloff (ADR-287): View > Ghost the other level (the minimap's box and the map's context menu
  too; `MapViewContext.GhostOtherLevel`) mixes the other level's terrain colours into the shown ground (`terrain_splat.gdshader`'s
  `ghost_map`, `AdventureMapView.Ghost`) and the minimap; the Height tool's Falloff (0–6 tiles, `HeightEditor` brushes' `falloff`).
  Tests in `HeightEditorTests`; smoke `editor-minimap`.
- Editor context menus and Object tool options (ADR-286): a right click on the map (by what is selected; a thing under the pointer is
  selected first), a browser entry (Object, Favourite, Find on the map) or a panel's header or side bar button opens a menu of registry
  commands (`CommandPopup`, shared with the menu bar; `MenuItems.Tidy`); the Object tool's Random variant (`BrowserCatalog.VariantsOf`,
  `EntryEditor.PlaceAny`, drawn from the scenario's random numbers, suiting the terrain) and Snap entrance to a road
  (`EntryEditor.Spot`, `Preview`). Tests `ObjectToolOptionsTests`, `MenuItemsTests`; smoke `editor-context-menus`.
- Creature and puzzle prompts redone to the key paintings, creature sheets 2 × 2 (five a town), rocky ground dark granite in the
  prompts and as a placeholder tint (ADR-285).
- Faction key paintings (ADR-284): the owner's town paintings (19, every town incl. Conflux and Dragon Utopia) in
  `docs/content/art/towns/` set each faction's look; [faction-art.md](../docs/content/faction-art.md) describes them and the recipe for
  more. Town-model, town-icon, town-screen background, puzzle and Necropolis backdrop prompts aligned; three existing pictures to
  regenerate.
- The editor's Measure tool (U, ADR-283): two clicks, the route the game's pathfinding takes drawn by day (the world view's
  `PathPreview`), its steps, points and days in the status bar; options: movement points a day and the player. Core `MapMeasure` over
  `PathFinder.FindPathFor` (a hero not on the map). Smoke `editor-measure`.
- Editor plan check and side bars (ADR-282): IDE-like side bars with a button per panel, one panel per region under a header, an
  icon-only tool palette, the scroll bar's room kept in every scrolled editor panel (`UiScroll`; an editor hang at some widths), Find
  (Ctrl+F) by name, id or kind ("gold mine"), Go to by id, an orthographic top-down view, icon prompts C12 and C13. The gaps the check
  found are in the plan's "After the close" and the roadmap. Smoke `editor-side-bars`.
- Editor overhaul, step 3's done-when (ADR-281): content is picked from pictures everywhere (the Monster tool's creature through
  `ToolOptions.Picker`, the Victory and loss tab's hero and artifact, crests on Generate's towns; `PicturePicker.SetItems`); "Any
  player" in a condition no longer becomes the first player. Smoke `editor-content-pickers`.
- Editor overhaul closed (step 6 task 7, ADR-280; ADR-247 implemented): the plan's target list ticked item by item; every tool has a
  key and a status-bar hint when picked (`IEditorTool.ClickHint`); View > Store bookmark. The plan stays in `docs/papers/` as the
  report. Left open in the roadmap: the first-use test with a person, the owner's look at the workspace.
- Editor overhaul, step 6 task 6 (ADR-279): a scripted first use (smoke `editor-first-use`) and its fixes: the Object tool places a
  town, mine, provider, garrison or dwelling for an owner (`EntryEditor.HasOwner`); fixes for a player without a town (nearest neutral
  town) and a hero without an army (`HeroArmyFix`, the starter stack); the blank map's next steps in the status bar; one extension on a
  saved name (`RecentFiles.WithExtension`). Tests `FirstMapTests`.
- Editor overhaul, step 6 task 5 (ADR-278): `tools/perf-editor.ps1` times the editor on a generated 256 × 256 two-level map
  (`--perf=`, `EditorPerfProbe`; `FrameStats` and `EditorTimings` with the budgets in the core, tests `EditorTimingsTests`). Hot spots
  fixed: `GroundEdit` compares whole corner arrays (`AdventureMap.Corners`), the minimap repaints only painted tiles, ground chunks are
  computed in parallel from pooled arrays, roads and rivers filled in one buffer. Open 2.4 → 1.3 s, painting 67 → ~110 fps.
- Editor overhaul, step 6 tasks 3 and 4: Help > Sample maps (smoke `editor-samples`); UI scale and contrast (ADR-277): the editor fits
  any window at any scale (regions capped to a share of the room, overlay boxes scroll, Template and Problems bars wrap), a High contrast
  preference switched live through `UiTheme.Apply` and theme variations; AutoInput points are UI points and reveal hidden tabs. Smoke
  `editor-scale-100/150/200`, `editor-contrast`.
- Editor overhaul, step 6 task 2 (ADR-276): F1 help. `HelpBook` (Core.Domain.Editing) makes pages from tools.md and random-maps.md
  (bold-led paragraphs and bullets, anchors as ids, aliases, decision numbers dropped), built into Godot.UI as embedded resources;
  `EditorHelp` finds the page for the hovered control (`IHelpTopics`, tags, text, node names; the Template tab prefers random-maps.md);
  `HelpWindow` lists, searches and links the pages. tools.md gained leads for Start zones, Height, Link gates, the Grail and each kind
  of problem. Tests `HelpBookTests` (on the real docs); smoke `editor-help`.
- Editor overhaul, step 6 task 1 (ADR-275): the shortcut editor. Preferences has a Shortcuts tab (`ShortcutEditor`): click a key, press
  the new one; None, Default, a filter; conflicts in red and OK disabled while any. The rules in the core (`KeyChord`,
  `ShortcutBindings`: Resolve, Changes, Conflicts); the registry's `UseShortcuts`, the commands' `DefaultShortcut`; the changes kept in
  `editor-settings.cfg` (`ShortcutStore`). Tests `ShortcutBindingsTests`; smoke `editor-shortcuts`.
- Editor overhaul, step 5 tasks 7 and 8 (ADR-274): Generate in the template editor runs off the main thread and shows the map in the
  Preview tab beside the graph (`ZonePicture` in the core, `ZonePictureView`; zones outlined and named; Open in map editor through
  `TemplateEditorHost.Open`). The Seeds tab's Generate many runs `TemplateBatch.RunAsync` (N seeds, capped parallel, cancellable; each
  map's `sim maps` checks and fairness imbalances) into a gallery of thumbnails with the spread over the batch. Tests
  `TemplateBatchTests`; smoke `editor-template-preview`, `editor-template-batch`. Step 5 is done.
- Editor overhaul, step 5 task 6 (ADR-273): the template inspector edits every field: tick-box pickers for a zone's terrains and neutral
  town types, the week events' factions and the roads' mines; start buildings by level and start dwellings by tier; road type and gates,
  weeks before events. Zone presets: `ZonePresets` (Rich treasure, Mine junction, Guarded start), the session's `AddZone(preset)` and
  `ApplyPreset`, user presets as `*.ewzone.json` in `user://template-presets/` (`ZonePresetFolder` in Core.Data, the editor's
  `ZonePresetFiles` behind `IZonePresetFiles`); + Preset, Apply preset, Save as preset…, Delete preset. Tests `ZonePresetTests` and the
  undo theory; smoke `editor-template-presets`.
- Editor overhaul, step 5 task 5 (ADR-272): mirror tools. `TemplateEditorSession.MakeMirror` adds the images of zones under the
  template's symmetry (new ids, free slots, a mirror cycle, mirrored graph places, the connections' images); while a cycle is closed an
  edit carries its changed fields to the images (`TemplateMirrors.Carry`) and Connect / a connection change / Disconnect apply to the
  connection's images; `BreakMirror` unlinks a cycle. Mirror over the graph and in the inspectors; dotted mirror lines. Tests
  `TemplateMirrorTests`; smoke `editor-template-mirror`.
- Editor overhaul, step 5 tasks 3 and 4 (ADR-270, ADR-271): the template graph. Zones are cards (type colour and icon, owner, team,
  size, monsters, towns, mines), connections carry labels (kind, guard, value, passages, chance); Snap to grid, Align in a row / column,
  Spread across / down and Arrange sit over the graph (`TemplateGraph` in Core.Generation holds the geometry). Multi-select with
  Ctrl/Shift + click and a marquee; a selection drags as one step (`MoveZones`); `TemplateFragment` with the session's `Copy`, `Paste`,
  `Duplicate` and `RemoveZones` (new ids, free player slots, inner connections and mirrors). Edit > Select all / Deselect / Cut / Copy /
  Paste / Duplicate / Delete follow the tab shown. The blank template's zones are 3 units apart. Tests `TemplateGraphTests` and the
  undo theory; smoke `editor-template-graph`.
- Editor overhaul, step 5 task 2 (ADR-269): template files. `TemplateFileService` (Godot.UI, Editor/Shell) over the app's
  `ITemplateFiles` (`TemplateFiles` in the editor): New, Open, Save, Save as, recent, shipped templates read-only (saved as a copy),
  "Template*" over unsaved work, the question before leaving it (quitting asks for the template first), the autosave and its recovery
  offered when the Template tab first shows. `AutosaveFolder` (Core.Data) is shared by `MapAutosaves` and `TemplateAutosaves`;
  `EditorFileDialog` is both services' dialog. File > Open / Save / Save as follow the tab shown. Tests `TemplateAutosavesTests`; smoke
  `editor-template-files`.
- Editor overhaul, step 5 task 1 (ADR-268): template undo. `TemplateEditorSession` records every change as a named step of whole
  templates (`UndoHistory`, `IUndoSession` shared with `MapEditorSession`), `BeginStep`/`EndStep` for a drag, `Load` starts the history
  anew, `TemplateChanged.Replaced` after an undo; the template editor shows its History beside the Issues, and Undo / Redo follow the tab
  shown. Tests in `TemplateEditorSessionTests`; smoke `editor-template-undo`.
- Editor icons: 77 files cut from the owner's sheets of `editor-icon-prompts.md` into `textures/ui/icons/`;
  `UiIcon` has their values (the file name is the value in snake case), `IEditorTool.Icon` gives each tool its own, and the commands,
  panel tabs, problems, layers, favourites, brush and height options and the template editor's lists show them.
- Editor overhaul, step 4 task 8 (ADR-267), which closes step 4: play-test. `PlayTestArguments` (Core.Domain/Game) reads the game's
  `--player= --others= --reveal --start= --day= --cheats` (`LaunchArguments.PlayTest`) and prepares the map; `PlayTestRules` in
  `GameOptions.PlayTest` (save format 7) sets the day, the start tile and the reveal at `GameSession.Start`, and idles the others in
  `GameDirector`; cheat commands `CheatResourcesCommand`, `CheatLevelsCommand`, `CheatCreaturesCommand`; never ranked
  (`ScoreRules.IsRanked`). The game starts a play-test at once (`Main.PlayTest.cs`) with `CheatPanel`; the editor's Test (F5,
  `PlayTestWindow`) saves to a temporary file and starts the game (`PlayTestLaunch`, `EditorPreferences.GamePath`). Tests
  `PlayTestTests`; smokes `editor-playtest-args`, `game-playtest`. Next: step 3's done-when (the drop-downs below), then step 5.
- Editor overhaul, step 4 task 7 (ADR-266): the fairness report and the exports. `MapReport` (Core.Domain/Analysis) lays out
  `MapAnalysis.Fairness` as a table (`FairnessTable`) and the whole report as Markdown with the problems; `FairnessPanel` (Godot.UI, a
  right-region tab) shows it and computes it while shown; File > Export report (Markdown) and Export map picture (PNG, `MapPicture`: a
  viewport sharing the map's world, top-down) through `FileService.PickExport`. Tests `MapReportTests`; smoke `editor-fairness`.
- Editor overhaul, step 4 task 6 (ADR-265): fix-its. `MapIssue.Fix` (`MapFix`: `LinkGatesFix`, `GateExitFix`, `OwnerFix`) where the
  answer is clear (gates with one possible partner, a missing player's things made neutral, a player's only neutral start-zone town);
  `MapEditorSession.Fix` runs one as an undo step and refuses it when the map changed since the check. The Problems panel's Fix button
  runs the selected one. Tests `MapFixTests`.
- Editor overhaul, step 4 task 5 (ADR-264): overlays. `MapOverlays` (Core.Domain/Analysis) computes passability, owners, guard strength,
  reach from a `MapAnalysis.Starts` start or a cell, zones and height contours per level as tinted cells or lines with a legend;
  `GeneratedZones` (Core.Generation) lays a generated map's zones out again from its description. `OverlayView` (Godot.Shared) draws
  one multimesh per overlay; `OverlaysPanel` (Godot.UI, a right-region tab) chooses it and shows `OverlayLegend` over the map view.
  Tests `MapOverlaysTests`; smoke `editor-overlays`.
- Editor overhaul, step 4 task 4 (ADR-263): `MapAnalysis` (Core.Domain/Analysis) over any `GameState`: `Reach` in days by the game's
  step costs for a scout of the start's faction that walks past guards and pickups (`PathFinder.CostsFor`, `passThrough`), `GuardStrength` per cell, `GuardPower`, `TreasureValue`, and `Fairness` per start
  with warnings over 20 % from the mean. `MapFairness` and `TreasurePlacer` take guard power and gold values from it; `sim fairness
  --verbose` prints it. Tests `MapAnalysisTests`.
- Editor overhaul, step 4 task 3 (ADR-262): layers. `MapLayers` (`MapEditorSession.Layers`, Core.Domain) hides and locks per `MapLayer`;
  what is hidden or locked is not selected (`SelectionEditor`) or erased (`EraseEditor`), and leaves the selection when its layer
  changes (`MapLayersChanged`). `MapViewContext.Hidden` hides kinds in the shared map views; the Layers tab is `LayersPanel`. Tests
  `EditorLayersTests`; smoke `editor-layers`.
- Editor overhaul, step 4 task 2 (ADR-261): the minimap `MinimapPanel` under the right region's tabs (`EditorWorkspace.SetFooter`; the
  HUD's `MinimapWidget` with `DragMoves`, `ViewFrame`, `GhostOtherLevel`, `Refresh`); Go to (Ctrl+G, `GoToBox` over `MapGoTo.Find`,
  results are `MapLink`s run through the inspector's jump). `EditorCamera.Centre` puts a tile in the middle of the map view (Go to, the
  Problems panel, the events, the minimap, switching level). Tests `MapGoToTests`; smoke `editor-minimap`.
- Editor overhaul, step 4 task 1 (ADR-260): the editor's own camera `EditorCamera` (Godot.Shared, beside `StrategyCamera`): tilted or top
  down (View > Top-down view), middle-button drag, the wheel zooming by a share of the distance; F frames the selection
  (`MapSelection.Cells`), Home the whole level, both into `EditorWorkspace.MapView`; Ctrl+1 … 9 / 1 … 9 bookmarks (`CameraBookmarks`).
  AutoInput has `mdrag`. Smoke `editor-camera`.
- Editor overhaul, step 3 task 10 (ADR-259): the new-map wizard `NewMapWizard` (File → New map, Ctrl+N; replaces New, New (2 levels) and
  From template): size, levels and ground, players and their kind, a template; it ends on a blank map (`NewMapSettings`,
  `BlankScenario.Create(settings)`) or in the Generate tab (`GeneratePanel.Preset`, `MapSizes.Nearest`, `EditorMapGenerator`'s `humans`).
  The empty state `EmptyStateCard` over the start-up map offers the wizard, Open and `SampleMaps`. Tests `NewMapTests`; smoke
  `editor-new-map`. Next: step 3's done-when: replace the
  remaining drop-downs (`ConditionsPanel`, `BansPanel`, `GeneralSettingsPanel` difficulty, `EventsPanel` quest kind, `GeneratePanel`,
  `TemplateEditorPanel`, `ToolOptions`, `PreferencesWindow`, the `ObjectBrowser` and `ProblemsPanel` filters), then step 4.
- Editor overhaul, step 3 task 9 (ADR-258, EDIT-11): the map's texts in one core list (`MapTexts.List`, `MapText` with an id, group and
  place) set through `MapTextEditor.Set` (the session's `Texts`), which makes each text's own panel edit; the map settings window's Texts
  tab (`TextsPanel`, Map → Texts) edits them in place with a search box. `MapTextsTests` fails when a string the map file writes is
  neither a listed text nor named as not one. Smoke `editor-texts`.
- Editor overhaul, step 3 task 8 (ADR-257, map format 44): rumours (`ScenarioRules.Rumours`, the Rumours tab `RumoursPanel`, the taverns
  show `TavernRules.Rumour`), a player's factions (`Player.Factions`, `PlayerFactions`, the Players tab's Factions menu, the new-game
  screen's `CrestPicker.Towns`, a random choice drawn among them), object names (`MapObject.Name`/`GivenName`/`Label`, a town's is its
  name; `ObjectEditor.SetName`, the inspector's Name field, `ObjectNames`, the quest form takes names, `NameCheck`), custom heroes
  (`Hero.Biography`, `Hero.Portrait`, `HeroEditor.SetName`/`SetBiography`/`SetPortrait`/`SetSkill`; the hero screen shows the
  biography). `FactionsCheck` warns of a fixed town outside a player's factions. Tests `MapContentTests`; smoke `editor-map-content`.
- Editor overhaul, step 3 task 7 (ADR-256): the map settings window `MapSettingsWindow` (F7, Map menu) with the tabs General
  (`GeneralSettingsPanel`: name, description, difficulty, hero limit, arena), Players (`PlayersPanel`, now with teams locked and team
  counts), Victory and loss (`ConditionsPanel`), Bans (`BansPanel`) and Week events (`WeekEventsPanel`); the right region keeps
  Inspector, Events and Generate; `MapPropertiesPanel` is gone. Also from the review of tasks 5 and 6: a mine's guard is two fields
  (`MineGuardCreature`, `MineGuardCount`), a "—" number box applies a typed number on Enter, chests' values wait for "Fixed values", a
  multi-edit that throws is taken back, a retyped town stays selected (`EditorContext.ReplaceObject`). Smoke `editor-map-settings`.
- Editor overhaul, step 3 tasks 5 and 6 (ADR-255). Multi-edit: `ObjectProperty<TObject, TValue>` and `SharedValue<T>`, the field
  list `ObjectProperties`, `MapEditorSession.EditAll` (one undo step, all or none) and `ObjectsOfOneKind`; the inspector's
  sections take a list of objects, `InspectorForm` binds fields with "—" for differing values, the Select tool opens several
  objects in the inspector. Pickers: `PicturePicker` / `PickerItems` (Godot.UI) for creatures, heroes, artifacts, spells and
  skills in the inspector and the Events tab's event form. Also from the review of tasks 3 and 4: thumbnails only for models no
  other entry shares, quest links never to the quest object itself or to an id of the wrong kind, a jump switches to the Select
  tool, a gate's missing target is named. Tests `EditorMultiEditTests`, `EditorLinksTests`; smoke `editor-multi-edit`.
- Editor overhaul, step 3 tasks 3 and 4 (ADR-254). Thumbnails: `PlaceholderModel` (Godot.Shared: the parts of an object's
  stand-in model, drawn by `MapObjectsView`), `ThumbnailRenderer` (hidden `SubViewport`s, PNG cache by the model's hash in
  `user://cache/thumbs/`), used by `ObjectBrowser` after icons and before the lettered placeholder. Inspector: `InspectorPart`
  (identity, contents, guard, messages, links, events) with `InspectorForm.In`, closed parts kept in the settings; jump links
  from `MapLinks` (Core.Domain.Editing), `InspectorPanel.LinkRequested`. Tests `EditorLinksTests`; smoke
  `editor-inspector-sections`.
- Editor overhaul, step 3 tasks 1 and 2 (ADR-253). Core: `BrowserCatalog` / `BrowserEntry` / `BrowserCategory` (every map object
  kind has a category; monsters and dwellings per creature, pickups per artifact, the hero), `BrowserCatalog.Search`, `BrowserLists`
  (favourites, recent), `EntryEditor` (`session.Placing`: `Check` for the ghost, `Place` as one step), `HeroEditor.CanPlace`,
  `SceneryTable.KindOf`. UI: `ObjectBrowser` under the tool palette in the left region, `BrowserChoice` shared with `ObjectTool`
  (O; ghost through `SelectionView.ShowMove`); the Object tool's drop-down and stamp buttons are gone. `AutoInput` no longer aims at a
  button a scroll box has scrolled out of view. Tests `EditorBrowserTests`; smoke `editor-browser-place`.
- Editor overhaul, step 2 tasks 7 and 8 (ADR-252); step 2 is complete. Scenery orientation: `Orientation` (mirrored, then 0 to 3
  quarter turns clockwise) on `Scenery`; `MapObject.Footprint` / `KnownObject.Footprint` are the shape as it lies
  (`Footprint.Oriented`), read by the cell index, the fog's memory, the minimap and `MapObjectsView`; `ObjectEditor.Rotate` /
  `Mirror` / `CheckTurn` (`SceneryTurner`: round the middle of the box, all or none, one step, `MapObjectMoved` per piece) and
  `SelectionEditor.Rotate` / `Mirror`. Map format 43 (`turns`, `mirrored` on scenery; step `OlderSceneryIsNotTurned`; sample v43;
  stamps of an older map format are upgraded on reading, sample `v01-map42`). Scatter: `ScatterEditor` in Core.Generation
  (`ScatterOptions`: density, kinds, keep paths open; `SceneryPlacer.Pick`), through `MapEditorSession.AsOneStep(description,
  random => …)`, whose undo restores the random state. UI: `ScatterTool` (no key: S pans the camera; scatters on release over the stroke's tiles), commands
  Rotate scenery (Ctrl+R) and Mirror scenery (Ctrl+M). Tests `SceneryOrientationTests`, `ScatterEditorTests`, cases in
  `EditorUndoTests`; smoke `editor-scatter-rotate`.
- Editor overhaul, step 2 tasks 9 and 10 (ADR-250, ADR-251). Core:
  `MapEditorSession.Resize(width, height, anchor)`, `Crop()`, `Shift(dx, dy)` and their `Check…` (a `ResizeCheck`: the new frame and
  what would leave the map), all through `MapResizer`: what is outside is removed by the other editors, then one command reframes
  the map in place (`AdventureMap.Reframed` / `TakeFrom`, `GameState.Reframe`: objects, heroes, the guard index), moves the event
  cells and the places event actions name, moves or drops the Grail, and publishes `MapResized`. Sizes 16 to 256 a side
  (`MapSizes.MinDimension`), square or not. Data: stamps (`Core.Data/Stamps`: `StampDocument` v1, `StampSerializer`, `StampFolder`;
  pinned sample in `tests/.../Data/StampSamples`). UI: `ResizeWindow` (size, presets, anchor, shift, the list of what is removed),
  `NameBox`, commands Resize map, Crop to selection, Save selection as stamp; the browser lists the stamps and hands one to the
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
   "Platform and art", "Roster"); the town screen overhaul starts with the owner's answers (TOWN-14 … TOWN-23); the editor
   overhaul is closed (the plan's "After the close" list done, ADR-288), with the first-use test with a person and the owner's look
   at the workspace open.
4. The "Code health" list in [roadmap.md](roadmap.md): skills, heroes and town types as typed ids; `InfoSheets` as data once localisation
   is decided (ADR-239).

## Known problems
- Numbers marked *(placeholder)* in the ADRs and in [open-questions.md](open-questions.md) wait for play-testing.
