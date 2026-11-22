# Decisions: Presentation and UI

**Covers**: Art direction, the camera, terrain and fog drawing, the HUD, layout and theme, information windows, icons, cursors and heraldry.

**Current rules**: [docs/design/presentation.md](../../docs/design/presentation.md), [docs/content/prompts/](../../docs/content/prompts/). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [008](#adr-008-homm-inspired-not-a-clone-dark-fantasy-realistic-presentation) | HoMM-inspired, not a clone; dark-fantasy realistic presentation | accepted |
| [022](#adr-022-strategy-camera) | Strategy camera | accepted |
| [095](#adr-095-speed-settings-and-hero-walking-implementation-2026-10-29) | Speed settings and hero walking (implementation, 2026-10-29) | implemented |
| [154](#adr-154-right-click-information-windows-and-detailed-tooltips-adr-118-ui-3-cd-adr-125-2026-11-09) | Right-click information windows and "Detailed tooltips" (ADR-118 UI-3 c–d, ADR-125, 2026-11-09) | implemented |
| [156](#adr-156-the-configurable-layout-and-the-layout-tab-adr-118-ui-3-2026-11-09) | The configurable layout and the "Layout" tab (ADR-118 UI-3, 2026-11-09) | implemented |
| [163](#adr-163-the-spacing-pass-the-main-menus-picture-and-small-usability-fixes-adr-101-adr-118-ui-3-b-2026-11-09) | The spacing pass, the main menu's picture and small usability fixes (ADR-101, ADR-118, UI-3 b, 2026-11-09) | implemented |
| [176](#adr-176-fog-of-war-looks-a-starry-void-or-clouds-opening-as-the-hero-walks-2026-11-11) | Fog of war looks: a starry void or clouds, opening as the hero walks (2026-11-11) | accepted |
| [178](#adr-178-information-windows-for-the-kingdom-overview-the-tavern-and-the-decision-prompts-adr-154-2026-11-11) | Information windows for the kingdom overview, the tavern and the decision prompts (ADR-154, 2026-11-11) | implemented |
| [179](#adr-179-a-rounder-smoother-fog-edge-half-the-stars-coloured-like-real-ones-adr-176-2026-11-11) | A rounder, smoother fog edge; half the stars, coloured like real ones (ADR-176, 2026-11-11) | accepted |
| [183](#adr-183-the-ground-is-one-splat-shaded-mesh-the-tile-grid-is-an-overlay-and-a-setting-adr-011-adr-182-2026-11-13) | The ground is one splat-shaded mesh; the tile grid is an overlay and a setting (ADR-011, ADR-182, 2026-11-13) | implemented |
| [186](#adr-186-fullscreen-short-resource-amounts-a-status-bar-along-the-bottom-and-centred-pop-ups-adr-118-adr-156-owner-notes-2026-11-16) | Fullscreen, short resource amounts, a status bar along the bottom and centred pop-ups (ADR-118, ADR-156, owner notes, 2026-11-16) | accepted |
| [190](#adr-190-a-long-press-opens-the-information-window-on-touch-adr-154-roadmap-2026-11-16) | A long press opens the information window on touch (ADR-154, roadmap, 2026-11-16) | implemented |
| [191](#adr-191-the-ui-theme-and-the-homm-style-side-panel-adr-118-adr-156-adr-163-adr-186-roadmap-2026-11-16) | The UI theme and the HoMM-style side panel (ADR-118, ADR-156, ADR-163, ADR-186, roadmap, 2026-11-16) | implemented |
| [200](#adr-200-terrains-blend-by-height-on-the-adventure-map-adr-183-2026-11-17) | Terrains blend by height on the adventure map (ADR-183, 2026-11-17) | implemented |
| [204](#adr-204-cursors-icon-only-buttons-a-status-bar-on-every-screen-sorted-map-lists-difficulty-icons-adr-116-adr-186-adr-201-owner-notes-2026-11-17) | Cursors, icon-only buttons, a status bar on every screen, sorted map lists, difficulty icons (ADR-116, ADR-186, ADR-201, owner notes, 2026-11-17) | accepted |
| [207](#adr-207-the-ground-is-drawn-in-chunks-editor-strokes-redraw-only-what-they-reach-adr-183-adr-197-2026-11-17) | The ground is drawn in chunks; editor strokes redraw only what they reach (ADR-183, ADR-197, 2026-11-17) | implemented |
| [208](#adr-208-map-objects-are-drawn-in-batches-captions-show-nearby-or-under-the-pointer-adr-031-adr-207-2026-11-18) | Map objects are drawn in batches; captions show nearby or under the pointer (ADR-031, ADR-207, 2026-11-18) | implemented |
| [209](#adr-209-the-ground-is-shown-as-a-smooth-surface-each-tile-drawn-as-2--2-squares-adr-182-adr-207-2026-11-18) | The ground is shown as a smooth surface, each tile drawn as 2 × 2 squares (ADR-182, ADR-207, 2026-11-18) | implemented |
| [210](#adr-210-the-adventure-maps-action-buttons-are-a-3--3-grid-in-a-corner-of-their-own-with-a-spell-book-adr-156-adr-191-adr-204-owner-notes-2026-11-18) | The adventure map's action buttons are a 3 × 3 grid in a corner of their own, with a spell book (ADR-156, ADR-191, ADR-204, owner notes, 2026-11-18) | implemented |
| [211](#adr-211-faction-crests-stand-beside-a-faction-wherever-the-ui-names-one-owners-heraldry-sheets-2026-11-18) | Faction crests stand beside a faction wherever the UI names one (owner's heraldry sheets, 2026-11-18) | — |
| [212](#adr-212-terrain-borders-blend-over-two-thirds-of-a-tile-a-picture-repeats-every-16-tiles-with-the-repeat-hidden-owner-2026-11-18) | Terrain borders blend over two thirds of a tile; a picture repeats every 16 tiles with the repeat hidden (owner, 2026-11-18) | — |
| [240](#adr-240-the-splat-reads-adventure-only-pictures-and-normal-roughness-and-occlusion-maps-owner-adr-183-2026-11-19) | The splat reads adventure-only pictures and normal, roughness and occlusion maps (owner, ADR-183, 2026-11-19) | implemented |
| [244](#adr-244-ground-textures-each-terrain-its-own-saturated-colour-and-structure-at-a-30-m-scale-the-mood-comes-from-light-and-grading-owner-adr-183-2026-11-20) | Ground textures: each terrain its own saturated colour and structure, at a 30 m scale; the mood comes from light and grading (owner, ADR-183, 2026-11-20) | accepted |
| [245](#adr-245-asset-prompts-for-spells-creatures-heroes-and-towns-ui-pieces-roads-and-rivers-the-puzzle-map-and-the-environment-cursors-in-grey-iron-one-layout-for-every-prompt-file-owner-adr-116-adr-204-adr-244-2026-11-20) | Asset prompts for spells, creatures, heroes and towns, UI pieces, roads and rivers, the puzzle map and the environment; cursors in grey iron; one layout for every prompt file (owner, ADR-116, ADR-204, ADR-244, 2026-11-20) | accepted |
| [246](#adr-246-town-screen-overhaul-a-3d-town-scene-with-full-town-windows-planned-in-six-steps-owner-adr-050-adr-114-adr-223-2026-11-20) | Town screen overhaul: a 3D town scene with full town windows, planned in six steps (owner, ADR-050, ADR-114, ADR-223, 2026-11-20) | accepted |
| [248](#adr-248-icons-and-portraits-painted-realistically-not-as-cartoons-new-style-blocks-and-an-anti-cartoon-negative-owner-adr-116-adr-245-2026-11-20) | Icons and portraits painted realistically, not as cartoons: new style blocks and an anti-cartoon negative (owner, ADR-116, ADR-245, 2026-11-20) | accepted |
| [249](#adr-249-prompts-ready-to-paste-one-block-a-sheet-at-most-a-lead-and-an-item-elsewhere-negatives-joined-in-advance-owner-adr-245-adr-248-2026-11-20) | Prompts ready to paste: one block a sheet, at most a lead and an item elsewhere, negatives joined in advance (owner, ADR-245, ADR-248, 2026-11-20) | accepted |
| [284](#adr-284-faction-key-paintings-set-each-towns-look-owner-adr-008-adr-246-adr-248-2026-11-22) | Faction key paintings set each town's look (owner, ADR-008, ADR-246, ADR-248, 2026-11-22) | accepted |
| [285](#adr-285-creature-portraits-in-2--2-sheets-creature-and-puzzle-prompts-redone-to-the-key-paintings-rocky-ground-is-dark-granite-owner-adr-284-2026-11-22) | Creature portraits in 2 × 2 sheets; creature and puzzle prompts redone to the key paintings; rocky ground is dark granite (owner, ADR-284, 2026-11-22) | accepted |
| [289](#adr-289-creature-portraits-in-high-contrast-with-plain-bases-and-clearly-upgraded-upgrades-academy-creatures-redone-owner-adr-285-2026-11-22) | Creature portraits in high contrast with plain bases and clearly upgraded upgrades; Academy creatures redone (owner, ADR-285, 2026-11-22) | accepted |
| [292](#adr-292-town-screen-answers-the-proposals-no-creatures-or-hero-models-in-the-scene-art-after-the-key-paintings-town-14--town-23-owner-adr-246-2026-11-22) | Town screen answers: the proposals, no creatures or hero models in the scene, art after the key paintings (TOWN-14 … TOWN-23, owner, ADR-246, 2026-11-22) | accepted |
| [293](#adr-293-sylvan-creatures-redone-to-the-owners-review-owner-adr-289-2026-11-22) | Sylvan creatures redone to the owner's review (owner, ADR-289, 2026-11-22) | accepted |
| [294](#adr-294-town-screen-steps-0-and-1-the-shell-behind---town-screennew-havens-style-frame-two-font-pairings-nine-slice-frames-the-shared-widgets-the-heroes-swap-adr-246-adr-292-2026-11-22) | Town screen steps 0 and 1: the shell behind --town-screen=new, Haven's style frame, two font pairings, nine-slice frames, the shared widgets, the heroes' swap (ADR-246, ADR-292, 2026-11-22) | implemented |
| [296](#adr-296-town-screen-step-2-the-windows-over-the-key-painting-the-overview-and-plans-in-core-recruit-all-upgrade-all-dismissing-renaming-the-rumour-table-resource-for-resource-sending-to-an-ally-town-17-adr-246-adr-292-2026-11-22) | Town screen step 2: the windows over the key painting, the overview and plans in core, Recruit all, Upgrade all, dismissing, renaming, the rumour table, resource for resource, sending to an ally (TOWN-17, ADR-246, ADR-292, 2026-11-22) | implemented |
| [297](#adr-297-town-screen-step-3-task-1-the-3d-town-scene-behind-the-bars-plots-read-from-layout-scenes-greybox-blocks-hover-and-pick-adr-246-adr-292-2026-11-22) | Town screen step 3, task 1: the 3D town scene behind the bars, plots read from layout scenes, greybox blocks, hover and pick (ADR-246, ADR-292, 2026-11-22) | implemented |
| [299](#adr-299-hero-portraits-womens-hair-medium-to-long-a-pose-of-each-kind-on-every-sheet-owner-adr-298-2026-11-22) | Hero portraits: women's hair medium to long, a pose of each kind on every sheet (owner, ADR-298, 2026-11-22) | accepted |
| [300](#adr-300-town-screen-step-3-task-2-greybox-layouts-for-the-eight-towns-after-their-key-paintings-the-layout-check-in-the-game-the-towns-painting-as-the-sky-adr-246-adr-292-adr-297-2026-11-22) | Town screen step 3, task 2: greybox layouts for the eight towns after their key paintings, the layout check in the game, the town's painting as the sky (ADR-246, ADR-292, ADR-297, 2026-11-22) | implemented |
| [301](#adr-301-town-screen-step-3-task-3-plot-states-walls-as-the-siege-has-them-streets-by-the-town-hall-banners-and-the-garrison-heros-banner-town-15-town-19-town-22-adr-291-2026-11-22) | Town screen step 3, task 3: plot states, walls as the siege has them, streets by the town hall, banners and the garrison hero's banner (TOWN-15, TOWN-19, TOWN-22, ADR-291, 2026-11-22) | implemented |
| [302](#adr-302-town-screen-step-3-task-4-the-town-camera-turned-25-round-the-town-and-zoomed-in-two-steps-by-the-lens-easing-toward-a-building-behind-its-window-town-14-adr-292-2026-11-22) | Town screen step 3, task 4: the town camera, turned ±25° round the town and zoomed in two steps by the lens, easing toward a building behind its window (TOWN-14, ADR-292, 2026-11-22) | implemented |
| [304](#adr-304-town-screen-step-3-tasks-5-and-6-the-bronze-outline-picking-unused-land-the-ring-of-plots-show-in-town-and-the-construction-scene-town-14-town-15-adr-090-adr-292-2026-11-23) | Town screen step 3, tasks 5 and 6: the bronze outline, picking unused land, the ring of plots, "Show in town" and the construction scene (TOWN-14, TOWN-15, ADR-090, ADR-292, 2026-11-23) | implemented |
| [306](#adr-306-town-screen-step-3-task-7-the-fade-through-black-into-the-hero-shot-and-the-layout-loaded-ahead-town-23-adr-090-adr-292-2026-11-23) | Town screen step 3, task 7: the fade through black into the hero shot and the layout loaded ahead (TOWN-23, ADR-090, ADR-292, 2026-11-23) | implemented |
| [308](#adr-308-town-screen-step-3-task-8-the-budget-overlay-the-town-scenes-budget-set-and-a-layouts-resources-let-go-town-23-adr-294-adr-306-2026-11-23) | Town screen step 3, task 8: the budget overlay, the town scene's budget set and a layout's resources let go (TOWN-23, ADR-294, ADR-306, 2026-11-23) | implemented |


### ADR-008: HoMM-inspired, not a clone; dark-fantasy realistic presentation
**Status**: accepted (2026-10-15)
We keep HoMM3's loop and structure but take deliberate departures, logged in [departures.md](../../docs/design/departures.md).
Art: realistic lighting and PBR materials, dark and grim atmosphere, recognisable nods to the
original. Modes in priority order: single-player vs AI, hot-seat, story campaign. Online multiplayer is
out of scope (LAN multiplayer added by ADR-088, ADR-098).

### ADR-022: Strategy camera
**Status**: accepted (2026-10-15)
Tilt 15–30° from vertical, coupled to zoom (zoomed in = 30°, zoomed out = 15°); rotation limited to ±45°
around the vertical axis, so the map never appears upside down and art can assume a "south-ish"
viewing direction; zoom and pan. Implemented in `StrategyCamera` (Godot.Shared), verified in Godot by the Phase 0 spike (ADR-043).

### ADR-095: Speed settings and hero walking (implementation, 2026-10-29)
**Status**: implemented (2026-10-29). Carries out ADR-090's speed settings (UI-1). Choices made here are marked *(choice)*.

- **Settings**: `SpeedSettings` (core, `Settings`): own heroes, computer heroes, "show computer heroes" and battle animations, each **slow,
  normal, fast or instant** (`AnimationSpeed`; 0.5×, 1×, 2×, no animation *(choice)*). "Own heroes" means every human player's heroes (hot-seat
  included); "don't show" is the computer heroes' fifth choice. Defaults: normal everywhere, computer heroes shown *(choice)*.
- **The file**: `SettingsFile` (Core.Data) keeps them in `settings.json` in the user data folder (`{"speed": {...}}`, room for later settings);
  a missing or broken file, or a missing value, gives the default. `--settings=<file>` uses another file (the smoke scenarios start from the
  defaults). Not in saves; no rule reads them.
- **Screen**: three rows under the save list and auto combat in the game menu (F10); a change applies at once and is written to the file.
- **Hero walking**: a step is 0.15 s at normal speed, as before, now with a small hop of the placeholder capsule. A computer hero's step is
  shown only when computer heroes are shown and the viewer sees one end of the step; otherwise it happens at once.
- **Computer turns**: the AI still plays a whole turn in one go. The hero views play the steps back one hero after another on one clock
  (`MapViewContext.Schedule`, advanced by the world view's frame time, so Movie Maker runs keep step). While computer steps are shown, the game
  app holds the AI, the next human turn, the input and the turn clock, and the camera follows each computer hero the viewer can see when it
  starts to walk; the map turns to the next human player when the playback ends.
- **Battles**: `BattleView3D.Speed` scales the animation queue; instant applies each step at once, like quick combat.
- Tests: `SettingsFileTests` (round trip, broken and missing files, partial files, step lengths). Smoke: `game-speed-settings` (the menu sets
  "Your heroes" to Slow, then the hero walks a route); the `game-auto-combat` clicks moved with the taller menu; `smoke-godot.ps1 -Only`
  runs some scenarios.

### ADR-154: Right-click information windows and "Detailed tooltips" (ADR-118 UI-3 c–d, ADR-125, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Window**: a right click opens one panel beside the pointer, above every screen: a title, what kind of thing it is, then its lines.
  *(proposal)* Holding the right button and letting go after 0.3 s closes it again (read while held); a quick click leaves it open until the
  next click or key. A left click or Escape that closes it does nothing else; another right click goes on to open the next window. The hover
  tooltips stay as they were.
- **What has one**: on the map a hero, a neutral stack, a town, any other object and open ground (terrain, road, river); in battle a stack
  (its count, top creature's health, Attack and Defense with what its hero, terrain and spells add, speed, initiative, morale, luck, shots,
  whether it retaliated, its rules in words and the spells on it); on the hero screen the army, the skills, the known spells (now one button
  each), the adventure spells and the artifacts; on the exchange screen the two heroes, their stacks and artifacts; on the town screen the
  buildings (every level with its cost, needs and income, what is built), the dwellings, the recruit buttons and the stacks; in battle the
  hero's and the stack's spell buttons.
- **What the player may know**: another player's hero, a neutral stack and a town show what the viewer's Scouting shows (ADR-125); an object
  under the fog shows as last seen (*"Last seen here; it may have changed since"*), an unexplored cell has no window. A town of the player's
  own lists its buildings; an object given once per hero or to its first visitor says which of the player's heroes took it.
- **"Detailed tooltips"** (off by default): a checkbox in the F10 menu, kept in `settings.json` beside the speeds (a new `interface` section;
  a file without it reads as off). It adds the formulas and the raw numbers: a creature's strength and map speed, the damage formula in
  battle, a spell's effect at each rank with the amount at the hero's Spell Power, an artifact's effects, a hero's base stats, ids.
- **Code**: core `Info/InfoSheets` (`InfoSheet` records, pure functions of the state: `Creature`, `BattleStack`, `Hero`, `Skill`, `Spell`,
  `Artifact`, `Building`, `Dwelling`, `MapCell`, `Town`); `DamageCalculator.AttackOf` / `DefenseOf` (what `Damage` already added, now shared);
  `InterfaceSettings`, `SettingsFile.LoadInterface`. Godot `InfoWindow` (a canvas layer) and `control.WithInfo(detailed => sheet)`. The map's
  right click (which showed a scouted army on the status line and an enemy town in the intel panel) now opens the window. Tests
  `InfoSheetTests`, `SettingsFileTests`; smoke scenario `game-info-windows`. No save format change.
- **Not done**: a long press on touch; windows for the kingdom overview, the tavern's heroes and the decision prompts.

### ADR-156: The configurable layout and the "Layout" tab (ADR-118 UI-3, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **The tab**: the F10 menu now has three tabs, "Save and load", "Settings" (auto combat, the speeds, "Detailed tooltips") and "Layout".
  Close and "Main menu" stay below the tabs. The layout choices: the battle's initiative bar (top, under the title, or bottom), the
  minimap's corner (any of four), the hero and town lists (beside the minimap, or along the bottom) and the action panel's side (right or
  left), plus "Default layout". A change shows at once, on the map and in a running battle.
- **Kept per computer**: `LayoutSettings` in the `layout` section of `settings.json`, beside the speeds and the interface settings. It is
  not in saves and changes no rule. A file without the section gives the default, which is the layout before this ADR.
- **Where the parts go** *(proposal)*, `HudArrangement`: each corner holds a stack of parts, from the corner outwards. The minimap (with
  "Switch level") is nearest its corner. Lists "beside the minimap" follow it in its stack, as in HoMM. Lists "along the bottom" run as rows
  between the bottom stacks, with narrower entries. The action panel (kingdom, quests, puzzle map and dig, marketplace) stands at the top
  of its side, after the minimap and lists if they are there. The turn, resources and status block does not move: it stays at the top left,
  beside the top-left stack. The bottom right leaves room for the menu button. With the initiative bar at the bottom, the battle log and
  the actions move up above it.
- **Code**: core `LayoutSettings` and `HudArrangement` (Domain) and `SettingsFile.LoadLayout` / `ReadLayout` (Data), with tests. Godot
  `AdventureHudLayout` is a full-screen container that lets the mouse through. It places the parts and moves them without reparenting,
  because a part's event subscriptions last only while it is in the tree. `HeroListPanel` and `TownListPanel` have a `Horizontal` form,
  `BattleHud` has `Layout`, and `GameMenuPanel` has its tabs and `LayoutChanged`. No save format change. Smoke scenario `game-layout`;
  `game-auto-combat` and `game-speed-settings` now open the "Settings" tab.
- **Not done**: the spacing pass (ADR-118 b); a layout for the 2D harness and the map editor.

### ADR-163: The spacing pass, the main menu's picture and small usability fixes (ADR-101, ADR-118, UI-3 b, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Spacing** (UI-3 b): one set of numbers, `UiSpacing`, as theme constants, with Godot's default fonts, colours and boxes: 4 px between
  slots and grid cells, 6 between rows, 8 between controls side by side, 16 above a section's heading, 8 inside a panel, 16 inside a
  screen or window over the map, 8 from the window's edge *(proposal: the numbers)*. Each app merges them into Godot's default theme at
  start (`UiSpacing.Apply`): a theme on the root window does not reach controls under a `CanvasLayer`, where every HUD part and screen
  is. Panels lose their own separation numbers; the HUD's corners, the battle HUD, the turn timer, the information window, the menu
  button and the editor's and harness's panels place themselves with `UiSpacing.ScreenEdge`. Section headings (`AddHeading`) on the
  hero, exchange and town screens, the F10 menu's speed settings and the editor's inspector and conditions panels.
- **Main menu picture**: the reference prototype's main-menu picture (`reference/ew`, the owner's), saved as
  `textures/ui/backgrounds/main-menu.jpg` (JPEG, 0.4 MB instead of a 2.9 MB PNG, for the LFS quota), covers the window behind the main
  menu, the new-game screen and the leaderboard (`MenuBackground`: cropped to fit, never stretched, darkened by a quarter
  *(proposal)*). Those screens stand in dark panels that let a little of it through (`Screens.UseMenuPanel`); the main menu is a column
  on the left, as in the prototype.
- **Usability** *(proposal: which ones)*:
  - **Continue** heads the main menu: it loads the newest save (autosave or named; its tooltip names it) and has the focus; without a
    save it is disabled and New game has the focus. A save that fails to load leaves the F10 menu open with the reason.
  - The "Menu (F10)" button shows only in a game; on the screens before a game it covered nothing useful and opened a menu with only
    Load (F10 still works).
  - The F10 menu and the result screen are opaque, like the other screens: the map and the picture no longer show through their text.
  - The new-game screen's title is centred, like the leaderboard's.
- **Smoke**: `game-main-menu` presses its buttons by name (which button has the focus depends on whether the computer has a save).
- **Not done**: fonts and a theme of our own (the art pass); the town screen's inner rows keep their own layout; the editor's toolbar
  rows keep fixed offsets.

### ADR-176: Fog of war looks: a starry void or clouds, opening as the hero walks (2026-11-11)
**Status**: accepted and implemented (2026-11-11), the owner's request with reference shots of a starry fog; builds ADR-099's smooth fog.
- **Two looks, a setting on this computer** (`InterfaceSettings.Fog`, menu "Settings" → "Fog of war"; the settings file's `interface.fog`):
  **Dark** (the default *(owner's choice)*), a starry void: the map floats in space; **Clouds**, thick drifting clouds. Both hide exactly
  what the rules hide; **Clouds are opaque** like the void *(owner's choice: not see-through)*. Explored tiles out of sight (refog mode)
  are veiled: dimmed under the void, a thin haze under the clouds. The editor shows no fog.
- **Technique: a full-screen pass, not a mesh over the tiles** (`FogView`, `shaders/fog_of_war.gdshader`). Each pixel reads the depth
  buffer, finds where it stands on the map and reads a fog mask there (one texel a tile, with a hidden border). So an object at the edge
  shows the part that stands on explored ground and the rest dissolves (ADR-099); pixels with nothing behind them use the ground plane.
  The mask is blurred and read through a slowly moving noise warp of about a tile, so the edge wanders and never shows the grid; an edge
  between an explored and an unexplored tile fades over about 1.3 tiles in both looks *(owner's choice)*. The map's own edge melts into
  the void the same way.
- **The animation is real 3D parallax, not a scrolling picture**: three star layers and a nebula lie on planes 40 to 160 units below the
  map, so they shift against the land as the camera pans, turns and zooms; the nebula drifts, the stars twinkle. The void is mostly
  truly black: sparse stars, about one in eight big and bright, and small faint violet-blue nebula wisps *(owner's choice)*. The clouds lie on two
  planes above the ground and drift with the wind. Edge of the land under the void: a wide dark rim first, then the stars.
- **Procedural by default; textures optional**: `textures/fog/nebula.png` replaces the procedural nebula and `textures/fog/clouds.png`
  adds detail to the clouds when present (prompts for an image model were given with this ADR; the shots in the request are the target).
- **Smooth reveal**: the core reveals at once; the view fades each tile over 0.9 s, starting when the step that revealed it starts to play
  (`MapViewContext.LastStepStart`), so the fog rolls back as the hero walks. A reveal of 30 tiles or more (a captured town, a Crypt, a
  spell) opens as a wave from its middle. Entering or leaving sight fades in 0.5 s. A new viewer (hot-seat) or level snaps without fading.
- Rendering only: rules, saves and the AI are unchanged.

### ADR-178: Information windows for the kingdom overview, the tavern and the decision prompts (ADR-154, 2026-11-11)
**Status**: implemented (2026-11-11); *(proposal)* marks the agent's choices, for play-testing.

- **Kingdom overview**: a right click on a row's name or detail opens what it pays (a provider: what a week brings and what waits there),
  then the town's, mine's, provider's or hero's own window, as on the map. The "Every day" and "Waiting at providers" rows open the totals
  split into towns, mines and heroes *(proposal)*.
- **Tavern**: a right click on a hero's card or its Hire button (the tavern screen, and the town screen's Tavern row) opens the hero for hire:
  stats, specialty, each starting skill with what it does, the preferred creature and what that gives, the troops it brings, the price or why
  it is refused; detailed, its roster id and its rank in the faction's pool. A hero of the player's own coming back shows its hero window. The
  card's preferred creature and troops lines open the creature's window.
- **Decision prompts**: a right click on the prompt opens what it is about: the object the hero visits, with what it offers now in place of
  "Visit it to see what it holds" (the chest's two rewards, the price, the sphinx's question, the hut's skills, the gate's towns); the stack
  offering to join or fleeing, with its exact count; the hero who levels up or whose army is full. A map event's message, the battle and the
  exchange have none. The skill buttons (a level-up, a witch hut) open the skill with the level it would reach, a town gate's buttons the
  town, a full army's ticks the stack, a guard's buttons the stack.
- **Code**: core `InfoSheets.Income`, `KingdomTotal`, `TavernHero`, `Decision`, `SkillChoice` (`InfoSheets.Screens.cs`); the Godot panels add
  `WithInfo`. Tests `InfoSheetScreenTests`; smoke scenarios `game-info-screens` and `game-info-prompts`. No rule, map or save format change.
- **Not done**: the map tavern's offers (the object service screen), a long press on touch.

### ADR-179: A rounder, smoother fog edge; half the stars, coloured like real ones (ADR-176, 2026-11-11)
**Status**: accepted and implemented (2026-11-11), the owner's request: the revealed area looked rugged, not round.
- **A round clearing with a soft edge**: the fog mask (one texel a tile) is blurred with a 5×5 binomial filter (close to a Gaussian of about
  a tile) instead of a 3×3 tent, so a revealed circle of tiles loses its staircase; the edge fades over about two tiles. The noise warp that
  sways the edge is broad and slow (two octaves, under a tile) instead of four octaves over almost two tiles, so the line stays smooth. Under
  the void the dark rim starts a little later, so the clearing keeps its size; under the clouds the puffs move the edge less.
- **Half the stars**: each of the three star layers has half the stars it had; their size, brightness and twinkle are unchanged.
- **Star colours by temperature, like real stars** *(owner's choice)*: most are near white, pale yellow to pale blue; a few are accents from
  deep orange to blue. A star's halo carries its colour, its core is a little whiter. A bright star is dimmed as a whole rather than clipped,
  so a hot star stays blue instead of burning out to white.
- Rendering only (`shaders/fog_of_war.gdshader`): rules, saves and the AI are unchanged.

### ADR-183: The ground is one splat-shaded mesh; the tile grid is an overlay and a setting (ADR-011, ADR-182, 2026-11-13)
**Status**: implemented. Rendering only: no rule, save or map format changes.

- **One mesh a level** (`AdventureMapView`): a vertex per tile corner, shared by the tiles around it, and one per tile centre, at the
  relief's heights (ADR-182), so the surface is unchanged and `MapSpace.ToGround` and picking still meet it. One draw call whatever the map
  size. Only a change of heights rebuilds it.
- **Splat shader** (`shaders/terrain_splat.gdshader`, `TerrainSplat`): the terrains' textures are the layers of one texture array (1024 px,
  built once from `textures/terrain/`, shared by every level; a texture borrowed by several terrains is one layer with a tint per terrain;
  a terrain without a texture is its palette colour). Which terrain a tile has is a map-sized texture, one texel a tile, so painting
  terrain changes texels, not the mesh.
- **Blended biomes, not following the grid**: a terrain's influence at a pixel is the tile picture blurred over the 3 × 3 tiles around it
  with a round kernel that reaches 1.5 tiles, so a single tile of another terrain is a round patch (not a square) and a diagonal staircase
  of tiles a slanted line; the place the picture is read at is pushed about by a slow noise (up to about a third of a tile), so borders
  wander. Where influences are close the terrains mix, the brighter texels of each ground first, so one ground creeps into the other
  rather than cross-fading. Broad lighter and darker patches break up wide plains. Steep ground (cliffs) is also textured from the sides.
- **The grid is an overlay** of the same shader: thin anti-aliased lines on the tile edges that fade out when zoomed far away. It is a
  setting: in the game "Show the tile grid on the map" (the F10 menu's "Settings" tab, `InterfaceSettings.ShowGrid`, kept in
  `settings.json`, **off by default**); in the map editor a "Grid" box beside "Level", on by default and not saved. Views read it from
  `MapViewContext.ShowGrid`.
- **Not changed**: the battlefield keeps its single-terrain material (`AdventureMapView.TerrainMaterial`); roads and rivers stay plates on
  the ground; the textures are still the placeholders (no normal or roughness maps yet: the look-dev spike and the PBR art).

### ADR-186: Fullscreen, short resource amounts, a status bar along the bottom and centred pop-ups (ADR-118, ADR-156, owner notes, 2026-11-16)
**Status**: accepted (2026-11-16), built (2026-11-16). From the owner's notes in `ideas.md`;
*(proposal)* marks the agent's fill-ins, for the owner to change.

- **Fullscreen**: the game plays only in a window today. A "Window mode" row in the F10 menu's "Settings" tab: Windowed or Fullscreen
  (borderless, the screen's own size) *(proposal: Godot's borderless fullscreen, not exclusive)*; kept in `settings.json` with the other
  settings, not in saves; applied at once and at start. Alt+Enter switches it too *(proposal)*. The map editor gets the same setting
  *(proposal)*.
  - *Built*: `InterfaceSettings.Window` (`WindowMode`: Windowed, Fullscreen) in the `interface` section of `settings.json`; Godot's
    non-exclusive fullscreen. The map editor has no menu: Alt+Enter alone switches it, kept in the editor's own `settings.json`.
- **Short resource amounts** everywhere the UI shows a stock of resources (the resource bar, the kingdom overview, the marketplace,
  costs): gold shows up to 6 digits (999 999); above that in millions with one decimal, "321.7m". The other resources show up to 4 digits
  (9 999); above that in thousands with one decimal, "31.7k". Rounded down *(proposal: never shows more than there is)*; a trailing ".0"
  stays *(proposal: the width stays steady)*; gold past 999.9m goes on in millions ("1234.5m") *(proposal)*. The right-click information
  window shows the full number *(proposal)*. One engine-free formatter in core, with tests.
  - *Built*: `ResourceAmountText` in core; the resource bar, the kingdom overview, the marketplace, every cost row (town, tavern, decision
    prompts, buttons with a price) and the gold balance use it. A shortened amount names the full one in its tooltip; the information
    windows and costs in words (tooltips) keep full numbers. Past 999.9 of a unit the formatter steps up to the next one, for gold as well:
    "1.2b" rather than "1234.5m", and other resources go "999.9k" → "1.0m" (as `ResourceAmountText` was written and tested before this
    was built; the owner may still prefer millions for gold).
- **Status bar** on the adventure map: a narrow stripe along the bottom edge, edge to edge on screens up to 16:9; on wider screens it
  stays 16:9 wide, centred *(proposal: centred)*. The resources sit on it, aligned right; its left part shows the game's tooltips and
  labels (what is under the pointer, the hints line and the messages that now go to the status line at the top left). The top-left block
  keeps the turn and End turn *(proposal)*. Lists "along the bottom" (ADR-156) and the bottom corners' parts stand above the stripe; the
  "Menu (F10)" button moves onto the stripe's right end *(proposal)*. In the replay viewer the viewer's bar stands above it *(proposal)*.
  - *Built*: `StatusBar` on the adventure HUD; the top-left block holds only the turn and End turn. On the map, the status line names the
    hero, object or ground under the pointer (the information window's title). In the replay viewer the status bar stands *above* the
    viewer's bar, not below it: the viewer's bar is also shown in battle, where there is no status bar, so it keeps the bottom edge; the
    menu button stays in the corner beside it there. In battle the menu button stands in the bottom right corner as before.
- **Pop-ups in the centre**: decision prompts (questions, choices), notices and alerts open in the middle of the screen, over the map,
  instead of in the top-left block. The information window (right click) still opens at the pointer *(proposal)*.
  - *Built*: the questions (`DecisionPanel`) and notices (`NoticePanel`, which also carries the week's event and other alerts) in a
    centred column, 520 px wide, with opaque panels.

### ADR-190: A long press opens the information window on touch (ADR-154, roadmap, 2026-11-16)
**Status**: implemented (2026-11-16); *(proposal)* marks the agent's choices, for play-testing.

- **Touch as the mouse**: the game reads a touch screen itself instead of through Godot's mouse emulation (turned off while the game runs).
  One finger is the pointer: a **tap** is a left click where the finger went down; a finger **moved** more than 12 px *(proposal)* is a
  left drag from there; a finger **held still for 0.5 s** *(proposal)* is a right click there, so it opens the information window of what is
  under it, on the map, in battle and on every screen, exactly as a right click does (ADR-154, ADR-178). Every screen keeps handling the
  mouse only.
- **No click under a long press**: the left press waits until the touch is known to be a tap or a drag, so holding a finger on a hero or a
  tile never also selects it or plans a route. A tap therefore acts when the finger lifts.
- **The window stays open** *(proposal)* when the finger lifts, unlike a held right button (a finger covers the screen while it is held);
  the next tap closes it and does nothing else, as a left click does.
- **A second finger** while the first is still undecided cancels the touch; there are no two-finger gestures (pinch zoom) yet.
- **Code**: core `Settings/TouchGesture` (the tap, drag and long press, from finger events and a clock; no Godot), Godot `TouchInput` (a node
  the game adds: touch events in, mouse events out through `Input.ParseInputEvent`). `AutoInput` gained `touch:x,y,frames`. Tests
  `TouchGestureTests`; smoke scenario `game-info-touch`. The map editor does not take touch. No save format change.

### ADR-191: The UI theme and the HoMM-style side panel (ADR-118, ADR-156, ADR-163, ADR-186, roadmap, 2026-11-16)
**Status**: implemented (2026-11-16); *(proposal)* marks the agent's choices, for play-testing and the art pass.

- **The side panel**: one framed column down a side of the adventure map, from the top edge to the status bar (ADR-186), holding the
  minimap with "Switch level", the hero and town lists and the action panel, as in HoMM. It is opaque and takes the clicks on it, so a
  click there never reaches the map. A new row in the F10 menu's "Layout" tab, **Side panel**: Right (the default), Left or Off. The
  owner's note: the panel can stand on either side, as the game's settings say.
- **With the panel on** the minimap, lists and action rows of the tab are greyed: the panel puts the minimap at the top of its side, the lists
  beside it (vertical) and the action panel below them. Their values are kept, and apply again when the panel is **off**: then the parts
  stand free in the corners as in ADR-156. A settings file without the row gets the panel on the right *(proposal: the HoMM look is the
  default, also for a file that had placed the parts freely)*.
- **The turn block** (date, player, End turn) stays at the top of the map, beside the panel when the panel is on the left *(proposal: not in
  the panel; the date with the week's event is too wide for it)*. The status bar keeps its place and the menu button; the panel stops
  above it.
- **The theme** *(proposal: the colours)*: dark slate panels framed in bronze, Godot's default fonts. `UiTheme` holds the colours and
  builds the theme from `UiSpacing`'s spacing (ADR-163) plus the panel box and the buttons' boxes (Button, OptionButton, MenuButton: a
  dark face, a dim bronze frame, bright bronze under the pointer, with the focus and pressed); each app merges it into Godot's default theme
  at start (`UiTheme.Apply`, in place of `UiSpacing.Apply`), so the map editor and the 2D harness get it too. The opaque screens, the
  menu panels before a game, the status bar (a 2 px bronze top edge) and the side panel (a 3 px frame) share the colours.
- **Code**: core `SidePanel` in `LayoutSettings` (the `sidePanel` key of the `layout` section in `settings.json`), `HudArrangement.PanelSide`
  and `Effective`, with tests; Godot `UiTheme`, `AdventureHudLayout` draws the panel behind its side's stack, `GameMenuPanel` has the row.
  Smoke scenario `game-side-panel`; `game-layout` turns the panel off first. No save format change.
- **Not done**: the camera still centres on the whole window, so a hero it centres on stands a little towards the panel's side; fonts,
  textured frames and icons of our own (the art pass); the battle HUD and the town screen keep their own layouts.

### ADR-200: Terrains blend by height on the adventure map (ADR-183, 2026-11-17)
**Status**: implemented. Rendering only: no rule, save or map format changes.

- **Height-based splat blending** (`shaders/terrain_splat.gdshader`), after the method of "height-based texture blending"
  (habr.com/ru/articles/442924): each layer of the terrain texture array carries its ground's height in alpha (bright = raised). A terrain
  stands at its influence (ADR-183's blurred, noise-pushed tile picture, 0 to 1) plus `height_relief` × its height there; the terrains
  within `blend_depth` of the highest show, each weighted by how far it rises above that line. Defaults are the article's: relief 1,
  depth 0.2. So at a border the stones of one ground stand out of the other's sand and the sand fills the gaps between them, instead
  of a cross-fade or ADR-183's brightness-scaled mix. A terrain that cannot come within `blend_depth` even at its highest is not read.
- **Where the height comes from** (`TerrainSplat`): `textures/terrain/<terrain>_height.png` when it exists (the name the texture prompts
  already ask for), else the albedo's brightness (the article's "alpha from greyscale"); either is stretched to the full 0–255 range
  (2 % clipped at each end) so every ground's relief counts alike. Layers are RGBA8; the plain white layer is at mid height.
- **Not changed**: the battlefield's single-terrain material; terrain textures, the influence kernel and the edge noise.

### ADR-204: Cursors, icon-only buttons, a status bar on every screen, sorted map lists, difficulty icons (ADR-116, ADR-186, ADR-201, owner notes, 2026-11-17)
**Status**: accepted (2026-11-17); built: the map lists' sort and filter (`MapListView`, `MapSizeClass` in Core.Domain, kept as
`InterfaceSettings.MapSort` / `MapSortReversed` / `MapSizeFilter`; a map file that does not load has no size: listed under All sizes only, last
by size); the cursors' choice (`AdventureCursors`, `BattleCursors`, `GameCursor`, Core.Domain `Cursors`) and the melee side
(`BattleTargeting.SideOf`, `CompassSide`), shown by `GameCursors` (Godot.UI), which falls back to system cursors (arrow, hand, cross,
forbidden, busy) until the images exist; the hotspots are a table in `GameCursors.Definition`, not a `cursors.csv`; the icon-only buttons
(`UiIcons.WithIcon`, label in `status_label` metadata, `SetLabel` for buttons renamed later; the map editor shows the label as the tooltip);
the status bar on the town, hero and exchange screens (the map's bar on its own layer above them), in battle (no resources; the menu button
on its right end) and before a game (`SetupScreenHost.Bar`). Not built: the difficulty icons (no art yet); the days a move takes on the move
cursor (drawn once the images exist); a button with a description or a refusal in its tooltip still shows it as a pop-up as well. From the
owner's notes in `ideas.md` and their answers; *(proposal)* marks the agent's
fill-ins, for the owner to change. No rule, save or map format change.

- **Cursors** ([cursor-prompts.md](../../docs/content/prompts/cursor-prompts.md)): the pointer changes with what a click would do, with HoMM3's and HoMM5's
  states and our own art. A standard arrow on menus, the town and hero screens and panels; an hourglass while a computer turn plays or a game
  loads; a horse on the adventure map (move, visit, attack, exchange with a hero, enter a town; the days a move takes drawn by the game from
  the 2nd day on), a helmet and a keep to select a hero or town, a shovel to dig, a spell cursor for an adventure spell's target; in battle a
  sword for melee, a bow (an arrow, and a broken arrow when the shot deals half or less), a wand for a one-target spell, a ring for an area
  spell, a boulder for the controlled siege engine against walls, a boot, a wing or a portal for walking, flying and teleporting, and a
  blocked sign where nothing can be done. **Static images** (the owner's answer), shipped at 64 px and scaled to 48 and 32; the hotspot per
  cursor in a `cursors.csv` *(proposal)*. A missing file falls back to the system arrow, as missing icons keep the text.
- **Melee direction**: over an enemy, the pointer's place in the hovered cell (eight 45° sectors round its centre) picks the side to strike
  from, one of **eight**; the sword cursor points that way and the cells the attacker would stand on (its whole footprint) are
  highlighted. The sectors are taken round the **target's centre** (for a 2 × 2 target its whole footprint), not the hovered cell's. A side
  the attacker cannot reach gives way to the nearest reachable one by angle *(proposal)*; a click attacks from the shown side. The choice of side and stand cells is engine-free (Core.Domain, beside `BattleGrid.AttackPositions`, with tests); so is the
  choice of cursor for what is under the pointer, on the map and in battle *(proposal: one resolver each, returning a cursor id; the views
  only map ids to images)*.
- **Icon-only buttons**: a button with an icon (`UiIcons`) shows the icon alone on a **square** button, **48 px** where buttons stand in a
  bar or panel (the side panel's actions, wrapping into rows; the battle bar; End turn) and **32 px** in rows and window headers (Close,
  the hero screen's arrows, the hero list's info) *(proposal)*; no text label. The owner's change: the buttons along a **window's bottom**
  (OK, Accept, Cancel, Start, Back, Leave, Close (Esc), Save, Load, Delete, Save replay) keep their **text** with a small icon before it,
  are as wide as their text and stand at the **right** of their row (`Screens.ButtonRow`). Hovering it shows its label, with its hotkey when it has one ("End turn (E)"), on the **status bar**, not as a pop-up
  tooltip. A button whose icon file is missing keeps its text.
- **The turn block in the side panel** (the owner's change): with the side panel on, **End turn** stands with the panel's action buttons
  (after the marketplace) and the **calendar** (the date with the week's event, whose turn it is) at the **bottom** of the panel, above the
  status bar, its lines wrapping to the panel's width. With the panel off, both stay at the top left as before (`TurnPanel.InPanel`).
- **A status bar on every screen** (the owner's answer): the adventure map's (ADR-186) also along the bottom of the battle, town and hero
  screens and the screens before a game (main menu, new-game screen, lobby, leaderboard, replay list) *(proposal: the same 16:9-wide
  stripe; resources only on the map and the town screen)*. Windows over a screen use that screen's bar. The map editor keeps pop-up
  tooltips *(proposal)*.
- **Map lists sorted and filtered**: every map list on the new-game screen's tabs (ADR-201), in the game and the LAN lobby, gets a **Sort**
  choice, **Name** (A–Z, the default) or **Size** (small first, then by name), a second press reversing it, and a **Size** filter: All, or
  one of the map presets (48 … 256; adventure-map.md) *(proposal)*. A map counts as the smallest preset that holds its larger side (an
  arena of 35 × 18 is 48); a template is listed under every size it supports and sorts by its smallest. The tab counts show what passes the
  filter; a chosen map that the filter hides gives way to the first listed. Sort and filter are kept in `settings.json`, the same for every
  tab *(proposal)*. The ordering and size rule are engine-free (Core, with tests).
- **Difficulty icons** ([icon-prompts.md](../../docs/content/prompts/icon-prompts.md), sheet 29): five icons for the five levels (Beginner … Expert), one
  helmet growing grander, as HoMM3 shows its levels as a row of pictures. The owner's answer: **icons only**; difficulty still sets only
  the starting resources and the score and never changes the AI (ADR-101). On the new-game screen the complexity becomes a row of the
  five icon buttons after a "Map default" text button *(proposal)*, the label on the status bar; the victory screen and the leaderboard
  show the icon beside the name.

### ADR-207: The ground is drawn in chunks; editor strokes redraw only what they reach (ADR-183, ADR-197, 2026-11-17)
**Status**: implemented. Rendering only: no rule, save or map format changes. Replaces ADR-183's "one mesh a level".

- **Why**: on a 256 × 256 map a height dab in the editor took about 250 ms: the whole ground mesh was built again (about 80 ms), and the
  start zones' plates (about 160 ms) were all laid again on every change of heights. Terrain painting rebuilt the whole terrain map
  texture from the map on every dab.
- **Chunks** (`AdventureMapView`): the ground of a level is meshes of `ChunkTiles` (32) × 32 tiles, built like the one mesh was (a vertex per
  corner, one per tile centre); a corner on a chunk's edge is worked out the same in both chunks, so they meet without a seam. A change of
  heights rebuilds the chunks with a tile that has a corner within one of a changed corner (a corner's normal comes from its neighbours),
  the apron only when that reaches the map's edge, and the roads and rivers of the tiles it touches. A few draw calls more; the camera skips
  the chunks out of view.
- **Painting** changes only the painted tiles' bytes of the terrain map, then uploads the texture (Godot has no partial update).
- **Start zones** (`StartZonesView`): a change of heights lays again only the plates on the tiles it touches; painting zones and a change of
  level still redraw them all.
- **Effect** (the editor on a generated 256 × 256 two-level map, Debug build, 20 height strokes / 20 terrain strokes): a height dab
  250 → 12 ms, a terrain dab 78 → 5 ms. The editor smoke frames are the same as before.

### ADR-208: Map objects are drawn in batches; captions show nearby or under the pointer (ADR-031, ADR-207, 2026-11-18)
**Status**: implemented. Rendering only: no rule, save or map format changes.

- **Why**: every object was a node with a mesh instance per box (its blocked cells, its entrance, its flag) and a caption of its own: a
  generated 256 × 256 map of about 2,000 objects made about 7,000 nodes, and every caption on the level was drawn wherever the camera was.
- **Batches** (`MapObjectsView`): the boxes, plates and flags of a level's objects are instances of one `MultiMesh` per box size, coloured per
  instance (the colours read as before: the instance colour is taken as sRGB). A removed object's instances are hidden and reused; when all
  are taken, the instances are doubled. Only the quest objects' shapes (EVT-5) stay nodes of their own.
- **Captions** show within `MapObjectsView.CaptionRange` (20 world units) of the camera, fading out over the last 2; the camera starts 16
  from what it looks at, so the captions around it show and zooming out hides them. The object under the pointer shows its caption from
  any distance: the game's and the editor's input set `MapViewContext.Pointed`.
- **Effect** (the editor on generated maps): nodes 7,017 → 1,250 at 256 × 256 (1,969 objects), 3,883 → 1,176 at 144 × 144. Draw calls in
  view stay about the same (about 135); the gain is in nodes, memory and the far captions not drawn.

### ADR-209: The ground is shown as a smooth surface, each tile drawn as 2 × 2 squares (ADR-182, ADR-207, 2026-11-18)
**Status**: implemented (the owner: "more detailed terrain, geometry ×4", smoothed hills, the apron alike). Rendering only: no rule, save or
map format changes. Changes ADR-183's and ADR-207's mesh (a vertex per corner and per tile centre, at the relief's bilinear heights).

- **Why**: four flat triangles a tile made hills faceted, with a crease along every tile edge.
- **The surface** (`ReliefSurface`, Core): monotone bicubic through the relief's corners: a cubic along x through four rows of corners,
  then one along y, with Catmull-Rom tangents limited after Fritsch and Carlson. It passes through every corner and is smooth across tile
  edges; over a tile it stays between that tile's own corners, so there is no ripple at a cliff's foot, flat ground stays flat and a
  level's height range is still its corners' range. Past the map's edge it takes the nearest edge corners, as before. Rules keep the
  bilinear `AdventureMap.GroundHeightAt` (the editor's "height where the stroke starts", levelling at entrances).
- **The view follows it**: `MapSpace.ToGround`, `GroundY` (heroes walking, picking) and `GroundNormal` (plates on slopes) read
  `ReliefSurface`, so what stands on the ground meets the mesh. A tile's shown ground reads one corner past its own on every side, so a
  change of heights re-places what stands on tiles touching `ReliefSurface.Reach(area)` (the area grown by one).
- **The mesh** (`AdventureMapView.Ground`): every tile is `Subdivisions` (2) × 2 squares of four triangles to the square's centre, 16
  triangles a tile (4 before); the chunks and the apron (as four strips) are built alike. Heights come from `ReliefSurface.Grid`, which works
  each corner row's cubic out once a column; normals come from the surface a quarter tile either way.
- **Cost** (the editor, Debug build, generated maps): building a level's ground 75 → 200 ms at 256 × 256, 32 → 75 ms at 144 × 144; a height
  dab's ground rebuild 1–2 → 4–9 ms.

### ADR-210: The adventure map's action buttons are a 3 × 3 grid in a corner of their own, with a spell book (ADR-156, ADR-191, ADR-204, owner notes, 2026-11-18)
**Status**: implemented. No rule, save or map format change; the settings file's layout loses `actionPanel` (a side) for `actionButtons` (a corner).

- **The grid** (`AdventureHud.BuildActions`): nine square 48 px icon buttons, three to a row: hero screen (H), spell book (C), kingdom
  (K); quests (Q), puzzle map (P), dig (G); marketplace, switch level (L), menu (F10). It takes in the level switch that stood under the
  minimap, the hero list's per-hero "Info" button (now one button for the selected hero) and the menu button that stood on the status
  bar's right end (on the map; in battle it stays there).
- **Disabled, not hidden**: the hero screen, spell book and dig act on the selected hero (`AdventureHud.ShowSelected`) and are greyed
  without one, the spell book also for a hero without a book; the marketplace without a town marketplace; the level switch on a one-level
  map. The tooltip (shown on the status bar) says why. In the replay viewer the hero, spell book, dig and marketplace buttons are greyed.
- **The spell book** (`SpellBookPanel`): a window over the map with the hero's mana, its adventure spells to cast (the same buttons as the
  hero screen's, `AdventureSpellButtons`; an aimed one closes the book and waits for its cell) and its battle spells by school with their
  costs; a right click opens a spell's information window. C or Escape closes it, and so does the next turn.
- **Where** (`LayoutSettings.ActionButtons`, `HudArrangement`): any corner, chosen in the "Layout" tab like the minimap, after the minimap
  and the lists when they share it; default top right. With the side panel on the grid stands in the panel under the lists, as before.
  End turn no longer moves among the actions with the panel on: it stays with the calendar, at the panel's bottom.
- **Menu icon**: a stone gear from the owner (`menu.png`).

### ADR-211: Faction crests stand beside a faction wherever the UI names one (owner's heraldry sheets, 2026-11-18)

- **Art**: the owner's heraldry sheets ([icon-prompts.md](../../docs/content/prompts/icon-prompts.md), sets 23–28), cut into `textures/ui/heraldry/`:
  `crest_<town>.png` and `crest_group_<group>.png` (the figure on a painted shield) and `charge_<id>.png` (the figure alone, engraved
  silver rather than the flat white the prompts asked for: its alpha still serves as a mask). Loaded by `Heraldry`, scaled and cached like
  the other icons; without the file the UI shows the name alone.
- **Where**: the town type's crest before the town's name in the town list and the kingdom overview, beside the town screen's title and the
  hero screen's (a roster hero's faction), on each tavern card, in the new-game screen's faction column (each choice and the fixed
  faction), the leaderboard's faction column and the map editor's faction choices (the town inspector, the week events' factions).
- **Not yet**: the group crests (no screen names a group) and the charges in the player's colour on banners and map flags, which need
  textured banner meshes first.

### ADR-212: Terrain borders blend over two thirds of a tile; a picture repeats every 16 tiles with the repeat hidden (owner, 2026-11-18)

- **Border**: a terrain's influence at a pixel (ADR-183) is the share of its tiles in a small square round the pixel, not a round blur, so
  it rises from 0 to 1 over a band a third of a tile to each side of a tile edge (`blend_width`): a border blends over about two thirds of
  a tile and the rest of a tile is its terrain alone. The edge noise still bends the borders; a lone tile of another terrain is a square
  with rounded, wandering edges. The height blend (ADR-200) is unchanged.
- **Scale**: one repeat of a terrain's picture covers 16 × 16 tiles (32 × 32 squares of the ground mesh), was 3 × 3.
- **Repeat hidden**: the ground is cut into cells 7 tiles wide (`variation_cell`, along the same wandering lines, off the tile edges);
  each cell shows each terrain's picture shifted by a random amount of its own, and neighbouring cells meet with the same height blend over
  the same band. A pixel inside a cell reads each terrain once, near a cell border twice, at a corner four times.

### ADR-240: The splat reads adventure-only pictures and normal, roughness and occlusion maps (owner, ADR-183, 2026-11-19)
**Status**: implemented. View only: no rule, save, map format or LAN change.

- **Adventure-only pictures**: a picture in `textures/terrain/adventure/` is the adventure map's in place of the shared one of the same
  name; the battlefield keeps the shared one. No picture uses it yet: the owner tried a 1024 px PBR grass set (albedo, height, normal,
  roughness, occlusion) and dropped it; the support stays for the next set.
- **Detail maps**: beside a picture, `<name>_normal.png`, `_roughness.png` and `_ao.png` (any of them) give it a layer of a second texture
  array (`detail_layers`: normal XY, roughness, occlusion; `detail_of` per terrain, 0 for none). The splat mixes the detail with the same
  height-blend shares as the colour. The normal map is read from above only (OpenGL convention, green up the picture) and added to the
  mesh's normal; steep sides stay flat. A terrain without maps is flat, roughness 0.95 (as before) and unoccluded.
- **Strength** (owner: the first cut looked flat): the map is lit almost only by the sun, and Godot's `AO` darkens only ambient light, so
  the occlusion also darkens direct light (`occlusion_light_affect` 1) and is deepened (`occlusion_power` 2); the normal map is bent
  harder (`normal_strength` 2.5), because a high sun shows little of a gentle one. *(placeholders, tune by eye)*
- A metallic map is not read: ground is not metal, and the shader's default is 0.

### ADR-244: Ground textures: each terrain its own saturated colour and structure, at a 30 m scale; the mood comes from light and grading (owner, ADR-183, 2026-11-20)
**Status**: accepted. Art direction for the terrain textures; no code, rule or format change. The prompts are in
[terrain-texture-prompts.md](../../docs/content/prompts/terrain-texture-prompts.md).

- **Why** (owner): the first generated set (made from the "desaturated, grim, 6 m patch" prompts) came out faded, grey-brown and nearly
  alike, with plants as large as a hero on the map.
- **Distinct grounds**: every terrain owns a signature colour and a signature surface structure that no other terrain shares (the
  prompts' "Tell them apart" table); stones only on the stony grounds. Grass is dense and green, dirt is wet churned mud, highlands are
  blue-green with flowering heather, and so on.
- **Saturated textures, mood in the game**: the textures are rich in colour and fairly bright; the dim, cold world comes from the
  light, fog, the splat's patches and the post grading, which can take colour away. *(choice: a faded texture cannot be made to read
  again; a vivid one can be toned down.)* This narrows presentation.md's "desaturated earth tones" to the final image, not the
  textures.
- **Scale**: one texture shows about **30 × 30 m** on the adventure map (it repeats every 16 tiles, so a tile is about 2 m, a hero's
  height) and about **12 × 12 m** on the battlefield (8 cells). The prompts size plants and stones to that.
- If the new set looks too loud on the map, a saturation and brightness control in the grading or the splat comes next; it is not
  built.

### ADR-245: Asset prompts for spells, creatures, heroes and towns, UI pieces, roads and rivers, the puzzle map and the environment; cursors in grey iron; one layout for every prompt file (owner, ADR-116, ADR-204, ADR-244, 2026-11-20)
**Status**: accepted; the "one layout" point superseded by ADR-249. Art direction and asset prompts only; no code, rule or format change. Parts marked *(proposal)* in the prompt
files (creature and hero backdrops, the puzzle picture over closed pieces, textured road and river strips, bridges, scatter debris,
nine-slice UI frames) need view work once the art exists.

- **One layout** (owner): every prompt file in `docs/content/prompts/` reads the same way: a short intro, "Compose a prompt" (a formula
  of named blocks), "Blocks", "Output", the subjects, "After generating". Engine detail, sources and lore that do not go into a prompt
  are left to the design docs and ADRs; the terrain file loses its "How the game uses them" and world sections.
- **Cursors in grey iron** (owner: the first takes came out brown, the horses above all): every cursor is cold grey iron and steel,
  the horse a forged iron figure, bronze, gold and brown named in the negative prompt. Sets 1–3 are to be regenerated.
- **New prompt files**: [spell-icon-prompts.md](../../docs/content/prompts/spell-icon-prompts.md) (36 common spells, six a school: the
  heroes' specialties, the creature casters' spells, the staple adventure spells),
  [creature-icon-prompts.md](../../docs/content/prompts/creature-icon-prompts.md) (all 180 creatures, a base above its upgrade on one
  sheet, one file a faction under `creature-icons/`, and a backdrop a faction),
  [hero-town-icon-prompts.md](../../docs/content/prompts/hero-town-icon-prompts.md) (the 128 roster heroes, the ten town types walled
  and as villages), [ui-element-prompts.md](../../docs/content/prompts/ui-element-prompts.md) (frames, buttons and their states, slots,
  skill-level frames, bars, screen and town-screen backgrounds),
  [road-river-prompts.md](../../docs/content/prompts/road-river-prompts.md) (three roads, four rivers, three bridges),
  [puzzle-map-prompts.md](../../docs/content/prompts/puzzle-map-prompts.md) (a picture a playable faction, its chief figure in the centre
  that opens last), [environment-prompts.md](../../docs/content/prompts/environment-prompts.md) (`scenery.csv`'s pieces per ground
  family, battle obstacles, scatter debris, shared materials).
- **Portraits** face right and keep the face and weapon in the middle band of the cell, so the wide army slots can crop them.

### ADR-246: Town screen overhaul: a 3D town scene with full town windows, planned in six steps (owner, ADR-050, ADR-114, ADR-223, 2026-11-20)
**Status**: accepted (the direction and the plan); nothing built. Supersedes ADR-050's "the town screen is 2D, also in the 3D game"
once step 3 of the plan lands. Plan, study and questions: [town-screen-plan.md](../../docs/papers/town-screen-plan.md).

- **Owner**: the town screen is to be overhauled to the quality of a current commercial strategy game, its main screen a **3D town
  scene**, and in function on the level of HoMM3 with HotA and HoMM5: a main screen, a building tree, recruitment, the tavern, the
  marketplace, the blacksmith and the other town windows.
- **Shape**: a 3D diorama per playable faction with a **plot** per building and dwelling (a model per level), hovered and clicked to
  open the building's window; the HoMM bottom bar (garrison and visiting hero, seven slots each) kept; 2D windows over the scene
  (Hall, Recruitment, Mage guild, Tavern, Marketplace, Blacksmith, Fort, Caravan and the rest). Every action stays a command
  (ADR-036); the scene and windows only read state.
- **Order**: six steps, each leaving a working game: decisions and wireframes; the UI foundation (fonts, frames, shared widgets);
  the windows over a 2D main screen; the 3D scene in greybox for all eight playable towns; Haven and Inferno in full art (ADR-034);
  the other six; polish and the adopted extras.
- **Open**: TOWN-14 … TOWN-23 (camera, plots, a 2D fallback, new town mechanics, mood, growth in the scene, creatures and heroes in
  it, the art method, the arrival); the plan follows their proposals until answered. New rules among them (Recruit all, Upgrade all,
  trading any pair, …) each get their own ADR and tests.

### ADR-248: Icons and portraits painted realistically, not as cartoons: new style blocks and an anti-cartoon negative (owner, ADR-116, ADR-245, 2026-11-20)
**Status**: accepted. Art direction and asset prompts only; no code, rule or format change.

- **Owner**: the generated icons come out too cartoonish; the prompts, including those of icons already generated, are to make them
  less so.
- **Style blocks** (`ICON-STYLE-A`, `ICON-STYLE-B`, `ICON-STYLE-CREST`, `SPELL-STYLE`, `CREATURE-STYLE`, `BACKDROP-STYLE`, `HERO-STYLE`,
  `TOWNICON-STYLE`, `CURSOR-STYLE`): a realistic painterly style like a detailed oil painting or concept-art study; true proportions and
  anatomy; real materials with fine wear; naturalistic light with a full range of values and muted colour. Edges come from light and
  shadow, not drawn lines; "bold", "simple" and "chunky" are gone. Readability at 32 px rests on silhouette and value contrast. The
  cursors keep their thick dark outline, which they need over the map, but their metal is drawn realistically.
- **`ICON-NEG-TOON`** (in [icon-prompts.md](../../docs/content/prompts/icon-prompts.md)): cartoon, comic, cel shading, flat fills,
  vector and clip art, mobile-game icons, thick outlines, exaggerated proportions, glossy plastic, oversaturated colour. Added to the
  negative of every painted sheet (tiers A and B, crests, spells, creatures, heroes, towns). The cursors get the same words in
  `CURSOR-NEG`, without the outline terms. `ICON-NEG` now names "plastic CGI render" instead of "3D render", so realism is not pushed
  away.
- **Unchanged**: tier C (one-colour button symbols the theme tints) and the heraldic charges (flat white stencils), which are flat by
  design.
- **Regenerate**: the icons and crests already in `textures/ui/` (tiers A and B, the crests) and cursor sets 1–3.

### ADR-249: Prompts ready to paste: one block a sheet, at most a lead and an item elsewhere, negatives joined in advance (owner, ADR-245, ADR-248, 2026-11-20)
**Status**: accepted. Asset prompts only; no code, rule or format change. Supersedes ADR-245's "one layout" point.

- **Owner**: a prompt should not need three or more text blocks put together by hand.
- **Sheets** (icons, cursors, spells, creatures, heroes, town icons, UI state sheets): every sheet has **one complete prompt block**:
  style, look, grid sentence and the numbered cells already joined, every "cell rule" written out. The tables keep the cell → file
  mapping and the check-only columns (hotspot, "reads as"); the subjects live only in the prompt.
- **Single pieces with variants** (terrain map or battlefield, town stages, scenery per ground family, siege pieces): at most **two
  pieces**, a pre-joined lead and the item's own block.
- **Negatives**: one ready negative block per kind of prompt, joined in advance; every file is self-contained (no blocks taken from
  another file), except the faction creature files, which link to one negative in the creature file.
- **Cost**: the shared style wording is repeated in each prompt of a file; a change to it is made in every copy (each file's "Shared
  wording" note says which words to search for).

### ADR-284: Faction key paintings set each town's look (owner, ADR-008, ADR-246, ADR-248, 2026-11-22)
**Status**: accepted. Art direction only; no code, rule or format change.

- **Owner**: the generated town paintings (one to three a town, Conflux and Dragon Utopia included) set the factions' and the game's
  design direction.
- **Where**: the paintings are in `docs/content/art/towns/`; [faction-art.md](../../docs/content/faction-art.md) states what they
  share and each town's setting, light, landmark, buildings, colours and what to avoid. Where a town has more than one painting, the
  first is the reference and the others are variations.
- **Precedence**: every faction asset follows them (the town scene and its models, the adventure-map town models, the town-screen
  backgrounds, backdrops, puzzle pictures). A prompt or model that disagrees is fixed, not the painting.
- **Changes in the prompt files** (town models, town icons, town-screen backgrounds, the Haven and Necropolis puzzle pictures, the
  Necropolis backdrop): Haven is a lakeside town under a castle with round, conical-roofed towers and the gold sun on its
  banners (no sun-cage brazier); Necropolis' light is cyan on a grey plain and its spire a straight obelisk; Dungeon is an enclosed
  cavern with a gothic citadel (no sinkhole, no pool); Conflux has the Altar of Thought, a castle and a mage guild instead of the gear;
  Dragon Utopia is dark granite with a dragon-head crag (no sun relief); Swamp Bestiary stands in a dark pine forest with cyan accents.
- **Shared look** (ADR-008, ADR-248): painterly realism from one camera, a low calm foreground, seven to nine readable buildings, one
  focal point, warm light against cool shadow, magic glow only in small accents.
- **Regenerate**: the existing `puzzle_haven`, `puzzle_necropolis` and `creature_backdrop_necropolis` pictures, made from the old
  prompts (hill-town, crooked spire, green sky).
- **Open**: TOWN-18 (one mood per faction) can take each painting's light as the faction's lighting set; the second takes' light
  (Haven overcast, Stronghold dusk) could serve for weather or a week's tint.

### ADR-285: Creature portraits in 2 × 2 sheets; creature and puzzle prompts redone to the key paintings; rocky ground is dark granite (owner, ADR-284, 2026-11-22)
**Status**: accepted. Asset prompts and two placeholder colours; no rule or format change.

- **Owner**: regenerate the puzzle and creature icon prompts to the updated design; creature sheets 2 × 2 instead of 2 × 3; Dragon
  Utopia's ground darker.
- **Creature sheets**: every sheet is 2 × 2 at 1024 × 1024. A town sheet holds two creatures over their upgrades; a town's nine pairs
  take five sheets, the last with its pair in the left column and a second try at each in the right (the better take is kept). Sheets
  of single creatures (Conflux, Dragon Utopia, the last neutral sheet) hold four, spare cells taking a second try. Creature subjects are
  unchanged except Necropolis' glow (green to cyan) and the dragonyte's scales (granite grey).
- **Faction looks and backdrops** in every creature file follow [faction-art.md](../../docs/content/faction-art.md): Haven's grey steel,
  gold sun and lake; Sylvan's misty gorge; Academy's gold star; Necropolis' cyan on grey; Inferno's orange fire and red lava on black
  ash; Dungeon's enclosed cavern; Stronghold's low warm sun; Swamp Bestiary's dark pine green with cyan; Conflux's element crystals;
  Dragon Utopia's granite.
- **Puzzle pictures**: all eight subjects rewritten to the paintings' towns, centres kept except Haven's (a white banner with the gold
  sun instead of the sun-cage brazier).
- **Rocky ground**: dark blue-grey speckled granite with rusty ochre stains and pale lichen, medium-dark, in the terrain texture and
  the plateau scenery prompts; told apart from rock by smooth wide slabs and speckle, not slate strata and moss. Until it has its own
  texture, the borrowed rough texture is tinted dark blue-grey and the minimap colour darkened (`AdventureMapView.Tint`, `Palette`).
- **Regenerate**: every creature portrait and backdrop, every puzzle picture, and the rocky-ground texture when it is made.

### ADR-289: Creature portraits in high contrast with plain bases and clearly upgraded upgrades; Academy creatures redone (owner, ADR-285, 2026-11-22)
**Status**: accepted. Asset prompts only; no rule or format change.

- **Owner**, on the first Academy sheets: a base and its upgrade differ too little; the colours look faded; both apply to every
  faction. Academy: no green or teal; the master gremlin not sci-fi, a musket instead of a hand gun; the djinn sultan a woman in
  saturated blue; no cloth on the gargoyle; three creatures looked like golems; both snow maidens looked like the djinn; the golems
  not robots, only the upgrade with a gladius and in real steel; only the titan of steel, the colossus with no weapon.
- **Every creature sheet**: the light passage asks for strong directional light, high contrast and rich deep colours (was soft
  light and muted colours); the upgrade cell asks for the same face and build, larger, heavier armed, more ornate and richer in
  colour, told apart at a glance. `NEG-CREATURE` adds faded, washed out, low contrast, desaturated, grey cast, sci-fi, robot, mecha and
  an upgrade identical to its base. The output rules say a base stays plain and its upgrade gains a feature that shows at 48 px.
- **Academy**: creatures in royal blue, bright steel, gold and ice-blue light, no verdigris (the buildings keep it); stone, iron and
  spirit creatures wear no cloth. The master gremlin carries a flintlock musket and powder horn; the iron golem is a crude forged iron
  statue with bare fists; the obsidian gargoyle keeps spread wings and horns; the animated armour is polished steel plate with a
  gladius; the snow maiden and enchantress are solid women in white fur and royal blue, not mist; the djinn sultan is a woman with
  sapphire-blue skin; the colossus is a living frost-grey giant with no weapon, the titan the same giant in polished steel with a
  lightning spear.
- **Academy by lore**: the thane and thunder thane, the efreet and the golden golem stay neutral in the game (not hired in the
  town, ids unchanged) but are Academy creatures by lore: drawn in its style on its backdrop, on Academy sheet A6. The thane is a
  pale-blue rune-tattooed half-giant with an axe (told apart from the colossus); the efreet a fire spirit in the djinn's gold
  shackles; the golden golem a slender gold statue. Likewise the specter and the banshee are Necropolis creatures by lore, in the
  spare right column of Necropolis sheet N5: a veiled ghost woman fading into mist (told apart from the wight and the wraith), the
  banshee her wailing upgrade with streaming white hair, cyan only. The timber and dire wolves are Sylvan by lore, in the spare
  right column of Sylvan sheet S5 (moss in the fur, pale-green eyes; the dire wolf with an elven collar of carved wood and antler).
  The gogs and the breeders are Inferno by lore: the gog and magog in the spare right column of Inferno sheet I5 (a squat wingless
  ash-grey demon, told apart from the imp), the breeders on a new sheet I6 (ember eggs, no green). One neutral sheet is left: the
  mammoths with the bandit over the troll.
- **Regenerate**: every creature sheet.

### ADR-292: Town screen answers: the proposals, no creatures or hero models in the scene, art after the key paintings (TOWN-14 … TOWN-23, owner, ADR-246, 2026-11-22)
**Status**: accepted; nothing built. The owner's answers in the [town screen plan](../../docs/papers/town-screen-plan.md#questions);
TOWN-15 is ADR-291. The plan's step-by-step implementation is in its [roadmap](../../docs/papers/town-screen-plan.md#implementation-plan).

- **The proposals** (TOWN-14, TOWN-16, TOWN-17, TOWN-18, TOWN-19, TOWN-23): a fixed hero shot turned ±25° and zoomed in two steps, the
  window opening at once; no 2D fallback, `TownPanel` kept in the harness; one lighting set per faction with a light week tint, no day
  cycle; walls by fort level and street filler by town hall level in three steps; a 0.5 s fade into the town.
- **TOWN-17 mechanics**: Recruit all, Upgrade all, tavern rumours, dismissing a stack and renaming a town now; any resource for any
  other at A's sell price over B's buy price; resources sent only between teammates; a goal building later (UI only); no selling of
  artifacts or creatures; spell research a candidate after step 6. Each is built as a core task of the plan's step 2 with its own ADR.
- **TOWN-20** (owner): no creatures idle at the dwellings; the scene has no creatures.
- **TOWN-22** (owner): no hero models in the scene; the garrison hero shows only as its banner on the keep.
- **TOWN-21** (owner): the proposal (a kit per faction, image-to-3D with clean-up for landmarks, Haven and Inferno first,
  `town-scene-prompts.md`), following the latest art direction: the key paintings and faction-art.md (ADR-284) and realistic, not
  cartoon (ADR-248). The style frame of step 0 is Haven's greybox lit after `key_haven-1`.

### ADR-293: Sylvan creatures redone to the owner's review (owner, ADR-289, 2026-11-22)
**Status**: accepted. Asset prompts only; no rule or format change.

- **Owner**, on the first Sylvan sheets: the pixie and sprite looked like goblins and carried weapons; the satyr smoked a pipe and its
  upgrade looked the same; the dancers should be men with sabers, closer to the classic elven blade dancers; the hunter a woman and
  her upgrade a man, with a composite or recurve bow, not roots and branches; the druid a man, its upgrade looked the same; the
  pegasus needs a rider, its upgrade in mithril; the moon doe a glowing ghost; the unicorn too hairy; no lanterns on the ancient
  treant; no leaves and moss on the wolves, the dire wolf bigger and black.
- **Sylvan look**: leaves, moss and vines only as patterns on clothing and armour, never growing on bodies, fur or weapons; elves in
  fitted leather and pale carved-wood armour with leaf-shaped steel and gold-leaf trim.
- **Subjects**: the pixie a tiny elven faerie woman with bright dragonfly wings and empty hands, the sprite with luminous blue-violet
  butterfly wings; the satyr bare-chested with a club, its upgrade in a cuirass with gold-capped horns, a war sickle and a shield; the
  blade and war dancers elf men with two curved sabers; the hunter an elf woman and the arcane archer an elf man, both with clean
  composite recurve bows; the druid a bearded man in plain robes, the elder an old white-bearded man in a grand mantle with an antler
  crown; the pegasus ridden by an elf with a spear, the silver pegasus and its rider in mithril; the moon doe a translucent glowing
  spirit; the unicorn with a short neat mane; the ancient treant with a bark beard and autumn crown; the timber wolf clean grey, the
  dire wolf a massive black wolf.
- **Regenerate**: Sylvan sheets S1–S5.

### ADR-294: Town screen steps 0 and 1: the shell behind --town-screen=new, Haven's style frame, two font pairings, nine-slice frames, the shared widgets, the heroes' swap (ADR-246, ADR-292, 2026-11-22)
**Status**: implemented; the font pairing waits for the owner's pick, the budget numbers for the reference PC and the Deck. The
[town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s steps 0 and 1.

- **Shell** (step 0): `TownScreenPanel` (`Hud/TownScreen/`) opens instead of `TownPanel` with `--town-screen=new` until step 2 ends:
  the top bar (crest, name, ◄ ►, faction, town hall, governor, quick buttons, ×), the faction's key painting as the main area
  (`textures/ui/backgrounds/towns/`) with the notices card, the bottom bar (crest, the two army bars with Swap, income, date), and one
  window at a time over the dimmed painting (Escape closes it, a second Escape leaves). The Hall shows building tiles, the
  recruitment window creature cards with the recruit dialog; the other windows are titled and empty, the marketplace and tavern
  windows open today's screens. Both screens are `ITownScreen`s for the adventure HUD; the town sections take an `ITownSectionHost`,
  so both screens hold them.
- **Style frame** (step 0): `scenes/towns/haven.tscn`, Haven blocked out in CSG after `key_haven-1` (lake left, castle on a rise right,
  the mage guild's orb as the focal point, the tavern in the low foreground, snowy mountains far back). First renderer settings:
  Forward+, no SDFGI or VoxelGI (sky ambient light, SSAO, glow), filmic tone mapping, depth fog with aerial perspective, one shadowed
  sun. The frame budget is still to be measured on the reference PC and the Deck. Smoke `town-style-frame-haven` (a scenario may run a
  scene: `Scene`).
- **Fonts**: two OFL pairings in `fonts/` with their licences: Cinzel with Alegreya Sans (the default) and Marcellus SC with Fira Sans
  (`--fonts=b`, smoke `game-fonts-b`), for the owner's pick. Body text 16 px, headings 22, titles 28 (`HeadingLabel`, `TitleLabel`);
  the editor's compact variant keeps Godot's font and its look.
- **Frames and theme**: `UiFrames` draws greybox textures in code (a window's nine-slice bevelled bronze frame with an inner accent line
  per faction, slots, check and radio marks, the slider's grabber) until the UI art exists; `UiTheme` styles tabs, check boxes, fields,
  scroll bars, sliders, lists, menus, tooltips, separators, progress bars and embedded windows. Every panel with an opaque background
  takes the window frame. `WindowFrame` is a titled frame with a "×".
- **Widgets**: `CostLabel` (the lack in red, the shortfall in the tooltip; `WithCost(…, treasury)` on today's building tree, dwellings,
  tavern, blacksmith; the marketplace's buy price); `SliderDialog` (− / +, Max at what the treasury pays, cost of one, total, the
  treasury after paying; recruiting and splitting use it); `ArmyBar` with `ArmyBarGroup` (drag moves, merges or swaps; Shift + drag
  splits; Ctrl + drag moves one; Alt + click spreads; double or right click opens the information; a click picks and a click moves for
  the keyboard and the controller; the upgrade badge; a hero's frame dragged to the other row) on the town, hero and exchange screens,
  which lose their "Split" and "Recruit to visiting hero" check boxes (recruits go to the garrison, or to the visiting hero when the
  garrison has no room; the caravan sends the whole picked stack); `CreatureCard`; `BuildingTile` with its state by frame and symbol;
  `FocusOrder`.
- **Rules**: `SwapTownHeroesCommand`: the garrison and visiting heroes change places, each with its own troops (the garrison hero leads
  the garrison out, the visitor's army becomes the garrison); it needs both heroes and troops in the garrison, and the heroes on the map
  stay as many. `StackSpread`: a stack shared evenly over its army's empty slots, the stack keeping what does not divide, run as the
  existing stack commands. `TownRules.CheckUpgradeLine` for the badge and the command alike; `BuildingStatus.Closed` and
  `BuildingTree.DwellingStatus` for a line the 7-of-9 rule closed for good. Tests: `TownScreenFoundationTests`; the record names
  `swapTownHeroes` (re-pinned; older saves load unchanged).
- **Keys**: `TownScreenKeys`, the plan's keys as input actions the game app adds to its input map (the harness falls back to the keys);
  each quick button's label and key show on the status bar.
- **Smokes**: `game-town-shell`, `game-town-armies`, `game-hero-armies`, `game-fonts-b`, `town-style-frame-haven`; `game-artifact-drag`'s
  drop points follow the taller army rows.

### ADR-296: Town screen step 2: the windows over the key painting, the overview and plans in core, Recruit all, Upgrade all, dismissing, renaming, the rumour table, resource for resource, sending to an ally (TOWN-17, ADR-246, ADR-292, 2026-11-22)
**Status**: implemented. The [town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s step 2; the TOWN-17 answers
(ADR-292: a, b, c, e, h, i, j now) as rules.

- **Read models** (core, tested): `TownOverview` (the daily income; each line's growth next Monday by part, base, fort, artifacts, growth
  building and Grail, adding up to what the dwelling gets before next week's event, which is drawn on Monday; built today; the governor and its discounts; each plot's
  state: not built, built today, built, closed, citizens' homes while the Capitol stands) and `TownPlans` (Recruit all's and Upgrade
  all's orders and totals), so the screen, the AI and the commands agree. `Town.LastBuiltPlot` names the plot built today.
- **Recruit all** (TOWN-17 a): line by line, top tier first, the most the treasury still pays of the best creature each dwelling sells
  (the upgraded one in an upgraded dwelling), into the garrison while it has room for it, else the visiting hero; a line without room or
  money is skipped. `RecruitManyCommand` carries the orders: each checked as `RecruitCommand` would on copies, all or nothing, paid in
  one sum, one `CreaturesRecruitedMany` event.
- **Upgrade all** (TOWN-17 b): every stack of both rows the town can upgrade, best first (tier, then the larger stack, the garrison
  before the hero), each while the treasury pays the whole stack; skipped stacks leave room for cheaper ones. `UpgradeAllCommand` runs the
  plan it computes itself; refused when the plan is empty.
- **Dismissing** (TOWN-17 i): `DismissStackCommand` for a stack of an own hero on the map (never its last) or an own garrison (may be
  emptied); nothing paid back; `StackDismissed`. **Renaming** (TOWN-17 j): `RenameTownCommand`, one line of 1 to 32 characters once
  trimmed; `TownRenamed`. Both live in saves and replays as commands.
- **Rumours** (TOWN-17 h): the table `src/Core/data/rumours.csv` for maps without their own (a map's rumours, ADR-257, still come first).
  A player's taverns tell one line a week, drawn from a source seeded by the game's seed, the player and the week, as the tavern's offers
  are (ADR-070), not from `GameState.Random` on Monday as the plan said: the same all week while what it names stays, given back by replays
  and saves without being stored, and older records' random draws unchanged. Lines may name the strongest, richest or most-towned player and an artifact lying on
  the map with its third of the land (or the depths); a line naming what the game has not got is not told.
- **Trading** (TOWN-17 c, e): `ExchangeResourcesCommand` gives B units for `ceil(B × buy(B) / sell(A))` units of A at the player's
  marketplace rate (or a map marketplace's), gold on neither side; `SendResourcesCommand` sends any resources to a teammate still in the
  game, from a player with a marketplace town. The AI does not use them yet.
- **Hall columns**: a `kind` column in `buildings.csv` (`core`, `military`, `magic`, `dwelling`, `unique`; `BuildingCatalog.KindOf`).
- **The screen**: `TownScreenPanel` is the game's only town screen and the launch option is gone; `TownPanel` stays in the 2D harness
  (TOWN-16). The top bar renames the town and switches towns; the main area is the key painting with a strip of the town's buildings and
  the notices card; the bottom bar has the governor, the army rows, Split, Upgrade all, Swap, Dismiss, the income, the growth with its
  parts and the date. Windows (one partial-class file per group): the Hall (columns by kind, requirement links, hover chains, the 7-of-9
  block as a column per tier, the detail card with crossed-through previews), Recruitment and the dwelling window (the count dialog with
  its destination, Recruit all's summary), the Mage guild's shelves and the war lodge, the Tavern's hero cards (`TavernCard`, which
  replaces the old tavern screen; the crest until portraits), rumour and Thieves' Guild, the Marketplace (two columns, rate, slider, Trade, the Silo, sending to an ally), the Blacksmith's machine
  cards, the Fort's defences in words, the Caravan, the governor list, the Transformer. `SliderDialog` may ask a choice beside the count;
  `ConfirmDialog` asks before Recruit all, Upgrade all and a dismissal.
- **Tests and smokes**: `TownScreenRulesTests`; the record names `recruitMany`, `upgradeAll`, `dismissStack`, `renameTown`,
  `exchangeResources`, `sendResources` (re-pinned; older saves load unchanged). Smokes `game-town-open`, `game-town-switch`,
  `game-town-build`, `game-town-recruit`, `game-town-windows`; the town smokes run without the option, and `game-dwelling-caravan`,
  `game-info-screens`, `game-unique-buildings` and `game-magic` open the windows they need.

### ADR-297: Town screen step 3, task 1: the 3D town scene behind the bars, plots read from layout scenes, greybox blocks, hover and pick (ADR-246, ADR-292, 2026-11-22)
**Status**: implemented. The [town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s step 3, task 1.

- **`TownScene3D`** (Godot.Shared/Towns): a town type's layout scene with its own camera, light and `WorldEnvironment`, behind the town
  screen's bars. It loads `scenes/towns/<town>.tscn`, keeps one layout loaded, out of the tree while closed (its environment and lights
  leave with it), and frees it with the scene. Its plots follow `TownOverview.PlotsOf` and refresh on `TownBuilt`, `DwellingBuilt`,
  `TownBuildingDowngraded` and `TurnStarted`; a plot is rebuilt only when its state or level changed.
- **Layout convention** (the plan's "layouts as assets", made exact): a `Marker3D` per building and dwelling line, named
  `TownOverview.PlotNode(id)`: `plot_` and the plot id with dots as underscores, since a Godot node's name has no dots
  (`plot_dwelling-haven_footman`). Optional children: `Model` (the layout's own greybox, shown while built), `Hover` (an `Area3D`
  whose shapes are picked as boxes) and `Focus` (the camera's point). The root's `Camera` is the hero shot; missing camera, environment
  or sun get defaults. `TownLayoutTests` checks every layout's plots against its town type, both ways.
- **What a plot shows**: built (and built today, until the construction scene of task 6), the first of
  `models/towns/<town>/<plot id>_<level>.glb` (or a lower level's), the layout's `Model`, or a `PlotBlock`: a block sized by the
  building's kind (core broad, magic tall, a dwelling by its tier, unique), a gabled roof (a cone on magic buildings) and its name on a
  billboard sign. Not built, closed and citizens' homes show the bare land for now (homes, scaffolding and walls by level are task 3).
- **Hover and pick** are a ray against the plots' hover boxes, not physics: nothing waits for a physics frame and nothing collides. The
  town screen hands the scene the pointer through a transparent stage over the main area: the hand cursor, the name as tooltip and on the
  status bar, a click opens the building's window (a dwelling's window, or the information of a building without one) as its strip
  button does, a right click its information.
- **The game app** hides the adventure world, its sun, its camera and the HUD's map parts (`AdventureHud.MapPartsHidden`) while the
  scene is open (`GameScreens.ShowTown`); the notices go too, as the painting hid them; the status bar stays (a question hides the screen). A town type without a layout keeps the key
  painting, as does the 2D harness, which has no scene.
- **Haven's layout**: the style frame (ADR-294) became it: its castle keep is the town hall's model, the curtain walls and corner towers
  the fort's, the mage tower the mage guild's, the tavern, smithy and stables theirs, the archery targets the Archer's dwelling, the
  church the Monk's, the harbour the marketplace's; the other 13 plots stand on free ground as blocks. The style frame smoke runs it with
  every model in sight.
- **Smokes**: `game-town-scene` (the scene, a click on the tavern and on the keep, a right click on a block, back to the map);
  `game-town-open` and the other town smokes now open Haven's scene.

### ADR-299: Hero portraits: women's hair medium to long, a pose of each kind on every sheet (owner, ADR-298, 2026-11-22)
**Status**: accepted. Asset prompts only.

- **Owner**, on the redone Haven sheets: they look better; no short-haired women, medium to long hair; every portrait looked the same
  way, so vary the angle: some straight at the viewer, some to the left or right, some slightly upward.
- **Hair**: the style passage says women wear their hair medium to long, never cropped or shaved; the 30 cells of women with short,
  cropped, shaved or unnamed hair now name medium or long hair; the negative prompt adds short-haired women.
- **Pose**: each cell names its pose after the hero's name; every sheet has one towards the viewer (straight or head tilted), one
  turned left, one turned right (three-quarter, sometimes eyes on the viewer) and one with the chin lifted only a little (a first try
  tipped the head right back; the negative prompt adds head tipped back), in an order that changes
  from sheet to sheet; the light stays top left. The game does not mirror portraits (the old "face right" rule is dropped).
- **Sylvan** (owner, on the Sylvan sheet): elven men have no beards; no wide faces; some may look slightly East Asian. The Sylvan
  line asks for long narrow or oval faces, beardless clean-shaven men and some slightly East Asian features; Faelar, Calen, Rhovan and
  Orrindel lose their beards, Faelar, Caelwyn, Neriah, Calen and Rhovan their wide or square faces, and Aerendil, Varis, Ithiel and
  Siannon get slightly East Asian features.
- **Single cells** (owner, on the generated sheets): Benedikt Grau's chin lifted less; Sabine Kell more European; Rowena Pell a
  younger European woman; Sylvara, Faenor and Rhovan paler (Rhovan's dreadlocks loose hair); Neriah and Calen new non-African
  faces; Nymara young.
- **Haven portraits**: the owner's five Haven sheets are cut into `textures/ui/heroes/icon_hero_haven.<hero id>.png` (specks and
  fringe cleared); the game does not show hero portraits yet.
- **Regenerate**: every other hero sheet.

### ADR-300: Town screen step 3, task 2: greybox layouts for the eight towns after their key paintings, the layout check in the game, the town's painting as the sky (ADR-246, ADR-292, ADR-297, 2026-11-22)
**Status**: implemented. The [town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s step 3, task 2.

- **Eight layouts**: Sylvan, Academy, Necropolis, Inferno, Dungeon, Stronghold and Swamp Bestiary join Haven's (ADR-297) in
  `scenes/towns/`, each after its key painting and [faction-art.md](../../docs/content/faction-art.md): the faction's ground, sky, fog and
  sun, and its setting around the town (Sylvan's gorge with a waterfall and giant trees, Academy's snow, frozen lake and the cloud
  temple's peak, Necropolis' dead plain and far ruins, Inferno's ash and lava channels, Dungeon's cavern with a ceiling of stalactites
  and no sky, Stronghold's badlands and ruins, Swamp's channels, dark pines and sunken ruins).
- **One composition**: every layout uses Haven's plot places (the keep and fort on a rise at the back right, the landmark in the middle,
  the lesser buildings around it, a calm front band), so the shared hero shot frames them all; a few plots move where the painting
  wants it (Academy's cloud temple on its peak and observatory on a spur, Sylvan's gallery on a ledge, Swamp's Shell Wall before the
  stronghold). The landmark, the keep, the fort and the buildings the painting names have their own greybox `Model` (primitive meshes,
  so their hover boxes are written in the layout); the rest are blocks. The landmarks: the Elder Grove, Academy's mage tower with the
  castle's orrery beside it, the Necromancy Amplifier, the Altar of Brimstone, the Mana Spring, Stronghold's lodge under the beast skull,
  the Elder Totem (Swamp's War Lodge).
- **The layout check in the game**: `TownScene3D` reports an error when a layout it loads has no plot for one of the town's buildings or
  lines (the core `TownLayoutTests` checks both ways against a default town). The smoke `game-town-layouts` opens a fully built town of
  each type in turn, so it fails on such a layout and leaves a frame of each.
- **The sky**: when `textures/ui/backgrounds/towns/bg_town_<town>.jpg` exists and the layout's environment has a sky, that painting
  becomes the sky (`town_sky.gdshader`): its middle where the hero shot looks, its horizon on the horizon, 2.1 radians wide, the sky's
  top colour above it; the layout's `sky_span` and `sky_horizon` metadata change the fit. The procedural sky stays until the pictures
  are made; Dungeon's cavern has no sky and keeps none.

### ADR-301: Town screen step 3, task 3: plot states, walls as the siege has them, streets by the town hall, banners and the garrison hero's banner (TOWN-15, TOWN-19, TOWN-22, ADR-291, 2026-11-22)
**Status**: implemented. The [town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s step 3, task 3.

- **Plot states** (`TownPlot`, as `TownOverview.PlotsOf` has them): a plot not built, or a closed line, is unused land (its `Land` child
  when the layout has one, else the layout's ground) with nothing to pick; a building built today stands in scaffolding round its model
  until the next day; a closed line once the town has the Capitol shows the citizens' homes (its `Homes` child, else a few greybox houses in
  the town's look), with nothing to pick.
- **The scenery** is a core read model, `TownScenery`: the settlement (none, village, town, city by the town hall's level 0, 1, 2, 3–4), the
  wall's parts from `SiegeRules.Create` itself (Fort: wall and gate; Citadel: the moat too; Castle: towers and a wall as high as it is
  strong, `WallHealth`), the owner and the garrison hero with the town type of the hero's school. The siege's keep is not built: the
  layouts model it as the town hall on its rise, inside the fort's own walls.
- **Walls**: a layout's `Walls` node holds the wall's line (`Post0`, `Post1`, … markers) and its `Gate` marker; `TownDressing` builds the
  parts along it in the town's siege look (ADR-144): sections, the gatehouse, the moat outside (away from the plots) with a bridge, a
  tower with a banner at every post. Every layout's line runs up its left side and across its back to the keep's rise, the front left
  open for the hero shot.
- **Streets**: `Streets/Village`, `Streets/Town` and `Streets/City`, each shown from its settlement on, hold `Street*` (paving), `House*`
  (a house in the town's look) and `Banner*` markers; the layouts place them clear of the plots and the setting: a street in from the
  gate, lanes inside the walls, houses along them and in the gaps between the plots, the village nearest the middle of the hero shot.
- **Banners**: the owner's colour (grey for a neutral town) with the faction's charge (ADR-211) on the gatehouse, the towers and the
  streets; the garrison hero's banner, bigger and with the charge of the hero's own town type, on top of the town hall (the layout's
  `HeroBanner` marker moves it). A marker with children of its own is the layout's art and shows as it is.
- The scene follows `TownOwnerChanged`, `HeroEnteredGarrison` and `HeroLeftGarrison` besides the building events and the new day. Smoke
  `game-town-states` opens a village, a town, a city and a Capitol; `game-town-build` shows the scaffolding, `game-town-armies` the hero's
  banner.
- Not yet: a captured Capitol dropping to City Hall loses the homes (the plan keeps them; it needs the town to remember the Capitol).

### ADR-302: Town screen step 3, task 4: the town camera, turned ±25° round the town and zoomed in two steps by the lens, easing toward a building behind its window (TOWN-14, ADR-292, 2026-11-22)
**Status**: implemented. The [town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s step 3, task 4.

- **At rest the camera is the layout's hero shot**, and every town opens there (a town switched to with [ or ] too). Its moves are three
  numbers eased together (about 90 % of the way in 0.45 s, closing exponentially, so a new move mid-way just changes the goal): the turn,
  where it looks and the lens.
- **Turn**: Q and E held (60° a second; they turn it the way they turn the adventure map's camera) turn the hero shot up to 25° either way round the
  vertical through the plots' middle, which stays where it is on the screen; the shot's own tilt is kept.
- **Zoom**: the wheel over the scene, or + and − (the numpad's too), steps between the hero shot and two steps in by narrowing the lens (the tangent of half
  the field at 1, 0.75 and 0.56), not by moving the camera, so a close view never runs into the keep's rise or a wall.
- **A building's window**: the window opens at once; the camera eases toward the building's `Focus` marker behind it (to 0.42 of the hero
  shot's distance, the lens back to the hero shot's, the turn kept), whether the window was opened by a click in the scene, the building
  strip or a key. Closing the window, by Escape, ×, or a click beside it, eases it back to the turned and zoomed view. Turning and zooming
  wait while a window is open; Recruitment and the governor's window have no building of their own: opened from the scene's view they leave
  the camera where it is, opened over a building's window they close it and the camera eases back. The building under a still pointer
  follows the moving view, but not under an open window.
- The keys are input actions (`town_turn_left`, `town_turn_right`,
  `town_zoom_in`, `town_zoom_out`) beside the screen's others (ADR-294). The smoke script's `wheel:` takes a screen point
  (`wheel:up,2,640,360`; without one it turns at the screen's centre, as before); smoke `game-town-camera` clicks the tavern, turns, zooms, opens the Hall and comes back.

### ADR-304: Town screen step 3, tasks 5 and 6: the bronze outline, picking unused land, the ring of plots, "Show in town" and the construction scene (TOWN-14, TOWN-15, ADR-090, ADR-292, 2026-11-23)
**Status**: implemented. The [town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s step 3, tasks 5 and 6.

- **Outline**: the building under the pointer gets an outline in the UI's bright bronze, an inverted hull a few pixels wide at any distance
  (`town_outline.gdshader`, the meshes' material overlay), so it reads on any model without a second render pass. Its name and state are on the
  status bar: built, built today or not built, then what its next level waits for (can be built or upgraded, costs more than the player has,
  needs other buildings, or tomorrow when the town built today), in words, never by colour alone. The hand cursor and the right click for the
  information window stay as ADR-297 made them.
- **Unused land picks too**: a plot is picked by its hover shape when a building stands on it, or when the town can still build there
  (`PlotInfo.Buildable`: not a closed line, not the Grail building; lacking resources or buildings still counts). Its
  hover shape is the layout's `Hover`, or a box the size of its greybox block. Hovered, it shows a translucent bronze ghost of its first level;
  a click opens the Hall with that building selected (the Hall's tile keys are the plot ids). Closed lines, the Grail's plot and the
  citizens' homes pick nothing.
- **The ring of plots**: ← and → (the d-pad) walk the pickable plots ordered round the plots' middle as the screen shows them, so → goes right
  along the front and left along the back; the walk goes on from the plot under the pointer, or starts at the front's middle. The plot
  reached is hovered as the pointer would hover it; Enter (A) opens it. The ring takes its keys ahead of a focused button while no window
  is open; a key with nothing to walk or open goes on (Enter to the chat line). The controller's B is Escape and its shoulder buttons are [ and ]. They are input actions (`town_ring_left`,
  `town_ring_right`, `town_ring_open`) with joypad events beside the keys; the screen takes joypad buttons in `_UnhandledInput`.
- **"Show in town"** on the Hall's card closes the Hall and keeps a ghost of the selected level on its plot, the camera eased toward it as
  for a window; its line stands under the bottom bar's date. It is offered only for a plot that stands built or can still be built. Escape, a click in the scene, a step of the ring or any window puts it away and
  the camera eases back. The ghost is the model for that level, the layout's `Model`, or the greybox block, as the built plot would show.
- **The construction scene**: on `TownBuilt` or `DwellingBuilt` for the town shown, once the plots show it and unless the speed is instant,
  the window closes (one building a day: the Hall has nothing left to build), the camera eases to the plot and the building rises out of the ground through its scaffolding in a
  cloud of dust, then the camera eases back. With the Capitol, the citizens' homes on the closed lines rise with it. 1.5 s at normal speed,
  scaled by the own heroes' speed setting (ADR-090: slow 3 s, fast 0.75 s, none at instant), the setting for what the player does; a
  click, Enter or Escape ends it at once. The bell waits for the game's first sounds (step 4, task 6), which list the build sound.
- The smoke script's `move:x,y` moves the pointer without a button (hover). Smoke `game-town-pick` hovers the tavern and the caravan's land,
  walks the ring, opens the Stables' land, shows it in town and clicks the caravan's land; `game-town-build` now plays the construction scene
  before reopening the Hall.

### ADR-306: Town screen step 3, task 7: the fade through black into the hero shot and the layout loaded ahead (TOWN-23, ADR-090, ADR-292, 2026-11-23)
**Status**: implemented. The [town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s step 3, task 7.

- **The fade**: when the 3D scene opens from the map, the map's last frame darkens to black and the town's hero shot comes up out of it,
  0.5 s in all at normal speed, scaled by the own heroes' speed setting as the construction scene is (ADR-090: slow 1 s, fast 0.25 s, none
  at instant). It is only a picture over the screen (`TownArrival`, a canvas layer over the town screen and the status bar, under the
  dialogs): the town screen takes the pointer and the keys from its first frame. The map's frame is copied from the viewport once, as the
  scene opens. A town switched to with [ or ] does not fade (the scene stays open), nor does a town type without a layout (its painting).
- **Loaded ahead**: `TownScene3D.Prepare(town)` loads the town type's layout scene and its sky painting on the resource loader's threads
  while the scene is closed; once they are in, the layout is instantiated out of the tree and its plots dressed as that town has them, in
  one frame on the main thread (with the plots' own models, loaded there too: cheap in greybox, a candidate for the loader's threads when
  step 4's models come), so opening only adds it to the tree. One layout is kept, and the latest wish wins: a `Prepare` for another town, or
  the opening of a town of another type, lets a pending load go (collected once it finishes, or when the game leaves). Opening a town whose
  layout is still loading waits for that load rather than starting another. `TownPlot` measures
  its models through the transforms up to its marker, so the plots can be built out of the tree.
- **When**: a route shown on the map (`PathPreview.Shown`) that ends at the gate of a town of the player at this screen, which comes a
  click before the move; and at the start of that player's turn (and the game's) for the town T opens, the first in the town list, since the
  map has no selected town.
- **Under a second**: measured in the smokes at 1600 × 900, the first open took about 90 ms with the greybox layout loaded on the spot, 10–20
  ms loaded ahead, about 3 ms after that (the fade's copy of the map's frame comes after, in `Opened`). `Open` warns when it takes over a second; `LastOpenMilliseconds` keeps the time for the budget
  overlay (task 8).
- Smokes `game-town-arrival` (Orrin walks into Castlebridge, loaded at the turn's start: the fade; T: the fade again) and `game-town-route`
  (a route to Mosswatch loads Sylvan's layout in place of Haven's; the walk in opens it loaded).

### ADR-308: Town screen step 3, task 8: the budget overlay, the town scene's budget set and a layout's resources let go (TOWN-23, ADR-294, ADR-306, 2026-11-23)
**Status**: implemented; the measurement on the reference PC and on the Deck waits for those machines. The [town screen plan](../../docs/papers/town-screen-plan.md#implementation-plan)'s step 3, task 8.

- **The overlay**: `BudgetOverlay`, a canvas layer over every screen in the game app, shows the frame's triangles, draw calls (with the shadow
  passes' share), the game's video memory and the frame time (the slower of the processor's and the graphics card's, each shown) against
  the budget, a number over its limit in red, and the town scene's opening time while one shows. F3 shows and hides it. The counts are the
  last frame's, the times the mean of half a second; the viewport's render times are measured only while it shows. The budget and the lines
  are engine-free (`TownSceneBudget`, `FrameSample` in Core.Domain/Towns, tests `TownSceneBudgetTests`). The frame time's limit is the
  Deck's when the game runs on one (Steam sets `SteamDeck=1`).
- **Measuring**: `--budget` starts the game with the overlay shown and writes its numbers to the log every ten frames, each line naming the
  town type shown (ten frames are a second of a smoke's movie; in a real window they are a sixth of one at 60 fps); `--budget=2560x1440`
  also renders the 3D that wide through the viewport's 3D scale (kept on a resize, the map's and the battle's 3D too), since a smoke's movie is the
  project's 1600 × 900 window whatever `--resolution` says (the bars are drawn at 1600 × 900, so the 3D costs what 1440p does and the 2D
  less). Smoke `game-town-budget` goes round the layouts map's eight fully built towns twice at 1440p. `TownScene3D.LastOpenMilliseconds`
  now times a town switched to with [ or ] as well as one opened from the map, since a switch to another type loads its layout.
- **The budget at full build**, the whole frame counted as Godot counts it: **≤ 1.5 M triangles; ≤ 2,000 draw calls**, the depth pass and
  the four shadow cascades included; **≤ 1 GB of video memory** for the game while the town shows; **60 fps** (16.7 ms) on the reference
  PC at 1440p and **40 fps** (25 ms) on the Deck; **opening within a second** (TOWN-23). The plan's placeholder of 400 draw calls is
  raised: the fully built greybox towns already draw 850 to 1,450, about half of them for the shadow cascades, and Godot draws each
  surface once for depth and once for colour. The art of step 4 keeps a building to a few surfaces; the Deck preset (step 4, task 8) is the
  place for fewer cascades.
- **Measured** on the development laptop (an RTX 500 Ada laptop GPU, not the reference PC; a debug build under Movie Maker, the 3D at
  2560 × 1440): Haven 33 k triangles, 853 draw calls (449 for shadows), 4–8 ms on the GPU, 8–11 ms on the CPU, opened in 10 ms
  loaded ahead and 22 ms on a switch; Inferno 39 k triangles, 942 draw calls (524), 5–12 ms on the GPU, 9–13 ms on the CPU, 25–85 ms
  to open on a switch. Over all eight: 33 k (Haven) to 385 k (Academy) triangles, 853 to 1,437 (Stronghold) draw calls, 464–511 MB of video
  memory for the whole game (the map alone about 370 MB), never more than 90 ms to open. The laptop's times wander by a factor of two
  between runs; the numbers that hold the line are the reference PC's and the Deck's, measured with the same smoke and F3.
- **A layout's resources let go**: the overlay showed video memory rising by the sky painting (about 37 MB) with each town type opened and
  never coming back, to 736 MB after the eight. The C# wrappers of what `TownScene3D` loaded or made (the layout's packed scene, the sky's
  painting, material, sky and environment, the loader's abandoned results, a plot's model scene) kept their resources alive until a garbage
  collection, which video memory does not prompt. They are disposed as soon as the scene holds what they wrap, so one layout's resources
  stay, as TOWN-23 wants: the second round of the eight stays at about 510 MB.
- Smokes `game-town-open` and `game-town-build` show the 3D scene (Haven's, since ADR-297); their descriptions say so.