# Presentation

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../../memory-bank/open-questions.md).

## Art direction
**Grim, dark fantasy** with **realistic lighting and PBR textures**, plus nods to the original (recognisable silhouettes for archetypal
creatures and buildings, heraldic player colours, iconic map objects).
- **Lighting**: physically based, low sun angles, volumetric fog, strong shadows; Godot Forward+ with SDFGI / VoxelGI or baked lightmaps, to
  be decided by a perf spike. Day / night tint per in-game day ❓.
- **Palette**: desaturated earth tones; player colours and interactive objects stay saturated so the map reads in a dark scene.
- **Materials**: PBR (albedo / normal / roughness), weathered and worn. **Post**: subtle grading, bloom on magic only, vignette.
- **Terrain** (ADR-011, ADR-183) *(placeholder textures, objects and units)*: a smooth heightmapped landscape; the terrains' textures
  run into each other where biomes meet, over about two thirds of a tile (ADR-212), along wandering lines that do not follow the tile
  grid (a single tile of another terrain is a patch with rounded, wandering edges); a texture repeats every 16 tiles, its repeat hidden
  by cells that each shift it and blend into each other the same way; cliffs are textured from the sides. Visual height must not contradict core tile data (cliffs = rock tiles).
- **Tile grid** (ADR-183): an overlay of thin lines on the tile edges, a setting on this computer: "Show the tile grid on the map" in the
  F10 menu's "Settings" tab, off by default; the map editor has its own "Grid" box, on by default.
- **Relief** (ADR-075, ADR-181, ADR-182, Warcraft III-like): looks only; no rule, the AI or a battle reads it, and the
  battlefield stays flat. Walkable ground lies between **0 and 4 cell widths** high, water below 0, impassable rock above 4 (4.5–6, the
  cliffs). The height is the map's own data, one value **per tile corner** in steps of 1/8 cell width, so tiles always meet and slopes are
  smooth; the [generator](random-maps.md#relief) sets it by terrain and the [map editor](tools.md#map-editor) reshapes it within the same
  limits. Heights by terrain *(placeholders)*: swamp, shallow water and ice 0–0.25; beach 0.5; sand 0.75; grass, dirt and cursed lands 1;
  forest floor and subterranean 1.25; wasteland 1.5; snow 2; rough 2.5; highlands 3; lava 3.25; rocky ground 3.5; deep water −0.5.
  Objects stand on ground levelled to their entrance's height; heroes walk over the slopes; roads, rivers and route markers lie on the
  ground; a click picks the tile under the cursor on any slope. Underground has the same limits and no
  drawn ceiling. Older maps get their terrains' heights, blended, when loaded.
- **Fog of war** (ADR-099, ADR-176, ADR-179): a smooth fade from visible to hidden over about two tiles, never a hard cell edge, so a revealed
  circle reads as a round clearing; a partly explored object shows its explored part.
  Two looks, a setting on this computer: **Dark** (default), a starry void (sparse stars, mostly white with orange to blue accents) with drifting nebulae under the map, in 3D parallax; **Clouds**,
  opaque drifting clouds floating above the level's highest ground. Revealed tiles open over 0.9 s as the hero's step plays; large reveals open as a wave from their middle.
  The map's edge is never a straight line: the ground goes on four tiles past it (its edge terrain and heights continued), and the fog
  swallows that ground along a wandering shoreline between about a third of a tile and two tiles out; the map's own tiles stay clear.
  Where nothing is drawn, only the fog shows. Without fog (the map editor, the replay's whole-map view) the map ends at its own edge, with the void (or clouds) around it.
  Rendering only: the rules keep cell-by-cell visibility.

## Camera and UI
- **Camera** (ADR-022): tilted **15–30° from vertical** (more tilted zoomed in), **rotatable ±45°**, zoom, pan.
- **UI**: by default a HoMM-style side panel on the right (ADR-191): one framed column from the top to the status bar with the minimap,
  the hero and town lists and the action buttons, which takes the clicks on it; End turn among the action
  buttons (ADR-204) and the calendar (date, week's event) on the status bar right of the resources (ADR-225); full-screen town and hero screens. The F10 menu has three tabs: "Save and load", "Settings" (auto combat, speeds, detailed tooltips, the
  fog's look, the tile grid, the window mode) and "Layout".
- **Action buttons** (ADR-210): a 3 × 3 grid of 48 px icon buttons: hero screen (H), spell book (C), kingdom (K); quests (Q), puzzle map
  (P), dig (G); marketplace, switch level (L), menu (F10). The hero screen, spell book and dig act on the selected hero and are greyed
  without one (the spell book also when the hero has none); the marketplace without a town marketplace; the level switch on a one-level map.
  In the replay viewer the hero, spell book, dig and marketplace buttons are greyed. The spell book is a window over the map: the hero's
  mana, its adventure spells to cast, its battle spells by school with their costs.
- **Status bar** (ADR-186, ADR-204): a narrow stripe along the bottom of the adventure map, as wide as the screen up to 16:9 and a centred
  16:9 width on a wider screen. The battle, town and hero screens and the screens before a game have the same stripe (resources only on
  the map and the town screen); windows over a screen use that screen's bar; the map editor keeps pop-up tooltips. On the left the status line: the hints, the game's messages and what the pointer is over (a HUD control's tooltip, or
  the name of the hero, object or ground under it on the map), whichever came last. On the right the resources (short amounts past 6 digits
  for gold and 4 for the others, the full one in the tooltip), and on the adventure map the calendar after them (ADR-225). On the adventure map the menu is one of the action buttons; in battle the
  "Menu (F10)" button stands on the bar's right end, and before a game and in the replay viewer's battles in the bottom right corner. In
  the replay viewer the bar stands above the viewer's bar.
- **Icon buttons** (ADR-204): a button with an icon shows the icon alone on a square button, 48 px in bars and panels (the action
  buttons; the battle bar; End turn), 32 px in rows and window headers (a window's or screen's
  close "×" always in its top-right corner, ADR-225); hovering it puts its label and hotkey on the
  status bar, with no pop-up tooltip. A button whose icon is missing keeps its text. The buttons along a window's bottom (OK, Accept,
  Start, Back, Close, Save, Load) keep their text after a small icon, as wide as the text, at the right of their row.
- **Heraldry** (ADR-211): each town type has a crest (its figure on a painted shield). It stands before the faction's name wherever the
  UI names one: the town list, the town and hero screens' titles, the kingdom overview, the tavern's cards, the new-game screen's
  factions, the leaderboard and the map editor's faction choices.
- **Cursors** (ADR-204, [cursor-prompts.md](../content/prompts/cursor-prompts.md)): the pointer shows what a click would do. An arrow on menus,
  screens and panels, an hourglass while the computer plays or a game loads; on the adventure map a horse (move, with the days from the 2nd
  on; visit; attack; exchange with a hero; enter a town), a helmet or a keep to select, a shovel to dig, a spell cursor for an adventure
  spell's target; in battle a sword (melee), a bow or a broken arrow (a shot dealing half or less), a wand or a ring (one-target or area
  spell), a boulder (the controlled siege engine), a boot, wing or portal (walk, fly, teleport), and a blocked sign. Over an enemy the
  pointer's place round the target's centre (eight 45° sectors) picks the side to strike from, one of eight; the sword points that way and the cells the
  attacker would stand on are highlighted; an unreachable side gives way to the nearest reachable one. Still images drawn at 64 px, shown at 43 px (the arrow, the hourglass and the
  blocked sign at 32 px); while an image is missing the nearest system cursor stands in (arrow, hand, cross, forbidden, busy). Controls show the arrow.
- **Pop-ups** (ADR-186): decision prompts (questions, choices) and notices open in the middle of the screen, over the map, in opaque
  panels. The information window (right click) opens at the pointer.
- **Window mode** (ADR-186): Windowed or Fullscreen (borderless, the screen's own size), in the "Settings" tab; Alt+Enter switches it, in
  the game and in the map editor. Kept in `settings.json`, applied at once and at start.
- **Layout** (ADR-118, ADR-156, ADR-191): the "Layout" tab sets the battle's initiative bar (top or bottom) and the side panel (right, left
  or off). With the panel off it also sets the minimap's corner, the hero and town lists (beside the minimap, or as rows along the bottom)
  and the action buttons' corner; with it on, those rows are greyed. The
  change shows at once and is kept in `settings.json` with the speeds, not in saves. The minimap is nearest its corner; lists beside it follow
  it; the action buttons stand in their corner, after the minimap and lists if they share it; End turn stays at the top left, beside anything there. The bottom parts stand
  above the status bar.
- **Theme** (ADR-191): dark slate panels and buttons framed in bronze, Godot's default fonts; the same in the map editor.
- **Spacing** (ADR-118, ADR-163): one set of margins and gaps for every panel:
  4 px between slots and the cells of a grid (army slots, paper doll, backpack), 6 between rows, 8 between controls side by side, 16
  above a section's heading (Army, Worn artifacts, Buildings, ...), 8 inside a panel, 16 inside a screen or window over the map, 8 from
  the window's edge to the HUD parts and between them.
- **Before a game** (ADR-163): the main menu, the new-game screen and the leaderboard stand on a full-window picture (cropped to fit, never
  stretched, a little darkened), in dark panels that let some of it through; the main menu is a column on its left. The game's "Menu (F10)"
  button shows only in a game (F10 still opens the menu). The F10 menu and the result screen are opaque, so the map does not show through.
- **Audio**: per-terrain ambient music, faction town themes.

## Information windows
ADR-118, ADR-154, ADR-178, ADR-190. A right click opens the window of what is under the pointer: on the map a hero, a stack, a town, an object or the ground;
in battle a stack; on the screens a stack, a skill, a spell, an artifact or a building; in the kingdom overview a row (what it pays,
then its town, mine, provider or hero) or the totals; in the tavern a hero for hire; on a decision prompt what it is about (the object
visited and what it offers, the stack, the hero) and each skill, town or stack to choose. It shows only what the player may know (Scouting,
ADR-125; objects under the fog as last seen). Held and let go it closes; a quick click leaves it open until the next click or key. On a
touch screen (ADR-190) a finger held still for half a second opens it and it stays open when lifted; a tap is a click, a moved finger a drag. The
"Detailed tooltips" setting (off by default, the F10 menu's "Settings" tab, kept with the speed settings) adds the formulas and the raw numbers.

## Speed settings
ADR-088, ADR-090, ADR-095. Player settings (`settings.json` in the user data folder, not in saves; they change nothing in the game) for own
heroes (every human player's), computer heroes (with "don't show") and battle animations, in the F10 menu: slow (half), normal, fast
(double), instant. Heroes walk with a small hop. A computer turn plays back hero by hero where the player can see, the camera following;
the next turn waits for it.
