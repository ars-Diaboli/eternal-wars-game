# Presentation

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../project/open-questions.md).

- **Art direction**: **grim, dark fantasy** with **realistic lighting and PBR textures**, plus nods to the original
  game (recognisable silhouettes for archetypal creatures and buildings, heraldic player colours, iconic map objects).
  - Lighting: physically based, low sun angles, volumetric fog, strong shadows. Day/night tint per in-game day ❓.
    Godot Forward+ with SDFGI/VoxelGI or baked lightmaps, to be decided by a perf spike.
  - Palette: desaturated earth tones. Player colours and interactive objects stay saturated so the map remains
    readable in a dark scene (readability pillar).
  - Materials: PBR (albedo/normal/roughness), weathered and worn.
  - **Terrain: smooth heightmapped landscape** (ADR-011: texture splatting, triplanar on slopes, blended biome
    transitions). The logical square grid stays invisible and appears as an overlay only for hover, path preview,
    movement range and in the editor. Walkability and costs still come from core tile data, so visual height must not
    contradict them (cliffs = rock tiles, etc.).
  - Post-processing: subtle colour grading, bloom on magic only, vignette.
- **Now**: textured per-tile terrain meshes (triplanar), placeholder meshes for objects and units. Later: the
  heightmapped, blended terrain shader.
- **Camera** (ADR-022): classic strategy camera, tilted **15–30° from vertical** (more tilted when zoomed in, more
  top-down when zoomed out), **rotatable ±45°** around the vertical axis, zoom in/out, pan.
- **UI**: HoMM-style right-side panel (minimap, hero/town list, end turn) and bottom status bar; full-screen town and
  hero screens.
- **Audio**: per-terrain ambient music, faction town themes.
