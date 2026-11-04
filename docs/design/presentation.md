# Presentation

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../project/open-questions.md).

## Art direction
**Grim, dark fantasy** with **realistic lighting and PBR textures**, plus nods to the original (recognisable silhouettes for archetypal
creatures and buildings, heraldic player colours, iconic map objects).
- **Lighting**: physically based, low sun angles, volumetric fog, strong shadows; Godot Forward+ with SDFGI / VoxelGI or baked lightmaps, to
  be decided by a perf spike. Day / night tint per in-game day ❓.
- **Palette**: desaturated earth tones; player colours and interactive objects stay saturated so the map reads in a dark scene.
- **Materials**: PBR (albedo / normal / roughness), weathered and worn. **Post**: subtle grading, bloom on magic only, vignette.
- **Terrain** (ADR-011) *(planned; now per-tile triplanar meshes and placeholder objects and units)*: a smooth heightmapped landscape
  (splatting, triplanar slopes, blended biomes); the grid shows only as an overlay (hover, path, range, editor). Visual height must not
  contradict core tile data (cliffs = rock tiles).
- **Relief** (ADR-075, Warcraft III-like): rules stay on the 2D grid; walkable ground lies between **0 and 4 cell widths** high, water
  below, cliffs or mountains above. Height follows the tile terrain (editor brush and generator pass come with the terrain look-dev).
- **Fog of war** (ADR-099): a smooth fade from visible to hidden, never a hard cell edge; a partly explored object shows its explored part.
  Rendering only: the rules keep cell-by-cell visibility.

## Camera and UI
- **Camera** (ADR-022): tilted **15–30° from vertical** (more tilted zoomed in), **rotatable ±45°**, zoom, pan.
- **UI**: HoMM-style right panel (minimap, hero / town list, end turn), bottom status bar, full-screen town and hero screens.
- **Audio**: per-terrain ambient music, faction town themes.

## Speed settings
ADR-088, ADR-090, ADR-095. Player settings (`settings.json` in the user data folder, not in saves; they change nothing in the game) for own
heroes (every human player's), computer heroes (with "don't show") and battle animations, in the F10 menu: slow (half), normal, fast
(double), instant. Heroes walk with a small hop. A computer turn plays back hero by hero where the player can see, the camera following;
the next turn waits for it.
