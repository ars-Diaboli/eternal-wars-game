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
