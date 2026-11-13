# Presentation

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../project/open-questions.md).

## Art direction
**Grim, dark fantasy** with **realistic lighting and PBR textures**, plus nods to the original (recognisable silhouettes for archetypal
creatures and buildings, heraldic player colours, iconic map objects).
- **Lighting**: physically based, low sun angles, volumetric fog, strong shadows; Godot Forward+ with SDFGI / VoxelGI or baked lightmaps, to
  be decided by a perf spike. Day / night tint per in-game day ❓.
- **Palette**: desaturated earth tones; player colours and interactive objects stay saturated so the map reads in a dark scene.
- **Materials**: PBR (albedo / normal / roughness), weathered and worn. **Post**: subtle grading, bloom on magic only, vignette.
- **Terrain** (ADR-011) *(planned; now one triplanar mesh per terrain over the relief's corner heights, a faint tile grid on it, and placeholder objects and units)*: a smooth heightmapped landscape
  (splatting, triplanar slopes, blended biomes); the grid shows only as an overlay (hover, path, range, editor). Visual height must not
  contradict core tile data (cliffs = rock tiles).
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
  Rendering only: the rules keep cell-by-cell visibility.

## Camera and UI
- **Camera** (ADR-022): tilted **15–30° from vertical** (more tilted zoomed in), **rotatable ±45°**, zoom, pan.
- **UI**: by default a HoMM-style right panel (minimap, hero and town lists, the action panel), the turn, resources and status at the top
  left, full-screen town and hero screens. The F10 menu has three tabs: "Save and load", "Settings" (auto combat, speeds, detailed tooltips)
  and "Layout".
- **Layout** (ADR-118, ADR-156): the "Layout" tab sets the battle's initiative bar (top or bottom), the minimap's corner, the hero and town
  lists (beside the minimap, or as rows along the bottom) and the action panel's side (kingdom, quests, puzzle map, dig, marketplace). The
  change shows at once and is kept in `settings.json` with the speeds, not in saves. The minimap is nearest its corner; lists beside it follow
  it; the action panel stands at the top of its side; the turn and status block stays at the top left, beside anything there.
- **Spacing** (ADR-118, ADR-163): one set of margins and gaps for every panel, in Godot's default look (no font or colour of its own):
  4 px between slots and the cells of a grid (army slots, paper doll, backpack), 6 between rows, 8 between controls side by side, 16
  above a section's heading (Army, Worn artifacts, Buildings, ...), 8 inside a panel, 16 inside a screen or window over the map, 8 from
  the window's edge to the HUD parts and between them.
- **Before a game** (ADR-163): the main menu, the new-game screen and the leaderboard stand on a full-window picture (cropped to fit, never
  stretched, a little darkened), in dark panels that let some of it through; the main menu is a column on its left. The game's "Menu (F10)"
  button shows only in a game (F10 still opens the menu). The F10 menu and the result screen are opaque, so the map does not show through.
- **Audio**: per-terrain ambient music, faction town themes.

## Information windows
ADR-118, ADR-154, ADR-178. A right click opens the window of what is under the pointer: on the map a hero, a stack, a town, an object or the ground;
in battle a stack; on the screens a stack, a skill, a spell, an artifact or a building; in the kingdom overview a row (what it pays,
then its town, mine, provider or hero) or the totals; in the tavern a hero for hire; on a decision prompt what it is about (the object
visited and what it offers, the stack, the hero) and each skill, town or stack to choose. It shows only what the player may know (Scouting,
ADR-125; objects under the fog as last seen). Held and let go it closes; a quick click leaves it open until the next click or key. The
"Detailed tooltips" setting (off by default, the F10 menu's "Settings" tab, kept with the speed settings) adds the formulas and the raw numbers.

## Speed settings
ADR-088, ADR-090, ADR-095. Player settings (`settings.json` in the user data folder, not in saves; they change nothing in the game) for own
heroes (every human player's), computer heroes (with "don't show") and battle animations, in the F10 menu: slow (half), normal, fast
(double), instant. Heroes walk with a small hop. A computer turn plays back hero by hero where the player can see, the camera following;
the next turn waits for it.
