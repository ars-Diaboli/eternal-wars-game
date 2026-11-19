# Relief: implementation plan

The adventure map's height (ADR-181, ADR-182). Five steps, each one change with a green test suite; steps 1–3 are core only, step 4 is
the 3D view, step 5 the editor. Once all are built this file moves to `archive/` and the rules live in the design docs.

All five steps are built (2026-11-13); archived. The rules live in `presentation.md`, `random-maps.md`, `tools.md` and ADR-182.

## Shared decisions
- **Name**: `GroundHeights` (not `Height`, which is already the map's row count). Values are `sbyte` **eighths of a cell width**.
- **Where it lives**: `AdventureMap` keeps one `(W + 1) × (H + 1)` corner array per level beside terrain, roads and rivers. Rules never read
  it (ADR-182 a); `GameStateDigest` stays as it is, so replays and the AI are untouched.
- **Limits** (ADR-182 h), applied per corner from the tiles that share it: any walkable tile → 0–4 (0–32 eighths); only water → below 0;
  only impassable rock → above 4; water meets walkable → 0 (the shoreline); rock meets walkable → 4 (the cliff foot). Stored range
  −2 … +7.875 (−16 … 63 eighths).
- **One terrain height table** in core (`ReliefTable`, ADR-182 c), read by the generator, the load fill and the editor. Ranges (swamp
  0–0.25, rock 4.5–6) use their middle for "from terrain"; the generator's noise moves within them.
- **"From terrain" shaping** (`ReliefShaper.FromTerrain(map, level, area?)`): each corner takes the mean of its tiles' table heights, then
  a separable blur over about 3 tiles, then the limits; shallow water and ice corners are pinned to their own height after the blur so they
  stay flat. Pure, deterministic, no randomness: the generator adds noise on top, the load fill and the editor use it as is.
- **Levelling** (ADR-182 f, `ReliefShaper.Level(map, footprint, entrance)`): the corners of a multi-tile object's footprint take the
  entrance tile's mean height; a one-tile ring around it is feathered so no step shows. One-tile objects and heroes are not levelled.
- **Sampling** (`AdventureMap.GroundHeightAt(level, x, y)` for fractional positions): bilinear over the tile's four corners, in cell widths.
  Engine-free, so the view, picking and tests use the same function.

## Step 1: the height layer in core ✅ (2026-11-13)
- `AdventureMap`: the corner arrays, `GetCorner` / `SetCorner`, `GroundHeightAt`; a new map starts at 0 everywhere.
- `ReliefTable`, `ReliefLimits`, `ReliefShaper.FromTerrain` and `Level` (Domain, `Map/`). The blend is a separable triangle blur of
  radius 3 corners. `FromTerrain` works on a whole level; the area overload comes with the editor (step 5).
- Tests: `ReliefTests`: the table covers every `TerrainType`; corners on a shore are 0 and under a cliff 4; a lake of shallow water is flat; two terrains
  blend without a jump of more than ¼ cell between neighbouring corners; `Level` makes a footprint flat at the entrance's height;
  `GroundHeightAt` at corners, mid-edge and centre.

## Step 2: the map format, older maps and built-in maps ✅ (2026-11-13)
- `MapDocument` **v39**: a nullable `Heights` layer, per level `H + 1` rows of `W + 1` characters (`HeightCodes`): `0`–`9`, `A`–`Z`,
  `a`–`z` are 0–61 eighths, `^` `~` 62–63, and `!#$%()*,-./:;=?@` −16 to −1; none is escaped in JSON (grass `8`, the walkable top `W`).
  Omitted when every corner is 0 (ground never shaped), which reads back flat, so such maps round-trip exactly.
- **Older maps** (ADR-182 j): an upgrade step (`MapMigrations.HeightsFromTerrain`, v39) decodes the terrain and writes
  `ReliefShaper.FromTerrain`'s heights into the JSON, so v1–v38 maps, saves and replays get blended terrain heights with no noise.
- **Maps built in code**: `EmberMarchScenario`, `MapSketch.BuildArena` (the three arenas), `DemoScenario`, `TownsScenario` call
  `ReliefShaper.Shape` (from terrain, then objects levelled); `BlankScenario` calls `FromTerrain`. `ReliefShaper.Level(map, objects)`
  levels every multi-tile object with an entrance in two passes (rings, then footprints), so no ring moves another object's footprint.
- Tests: `HeightFormatTests`; `FormatSamples/v38` (now upgraded, `v38.upgraded.json`) and `v39` (hand-shaped corners); `ReliefTests`
  for the built-in maps. `GeneratorPinTests` hashes the generated map without its format version, so a format bump that leaves generated
  maps alone no longer reads as a generator change (checked: v9's maps are unchanged).

## Step 3: the generator ✅ (2026-11-13)
- **`ReliefPainter.Paint(map, seed, zoneOf)`** (Generation) runs at the end of `MapGenerator.Generate`, after treasure, not right after
  `TerrainPainter.Paint`: the passages and scenery change the terrain later, so the heights come from the final terrain. `FromTerrain`, then
  smooth value noise (lattice every 6 corners) of ±2 eighths from the `heights` stream per level; none on corners with only shallow water
  or ice around; on corners with only sand around, dunes of ±1.2 eighths in rows 5 corners apart, direction (one of four) and phase drawn
  per zone from the `dunes` stream, bent by the noise, a rounded triangle wave (plain arithmetic, no `Math.Sin`, so every platform agrees);
  then the limits. No other stream is touched.
- **Levelling** right after: `ReliefShaper.Level(map, objects)` for every multi-tile object with an entrance. Roads and rivers are not touched.
- `MapGenerator.Version` **10**; `GeneratedMaps.txt` re-pinned.
- Tests (`ReliefPainterTests`): the same seed gives the same ground, another seed another; grass stays within ±2 eighths of its terrain
  height and varies; sand goes further (dunes); shallow water and ice stay flat; a generated map is within the limits everywhere with
  every footprint flat; reshaping or flattening a generated scenario's ground leaves its rules digest (`GameStateDigest`) unchanged.

## Step 4: the 3D adventure map ✅ (2026-11-13)
- **Ground mesh**: `AdventureMapView` builds one `ArrayMesh` per terrain per level, replacing the boxes: five vertices a tile (the four
  corners and the centre at `GroundHeightAt`, so the mesh meets `ToGround` exactly there), four triangles, normals from the neighbouring
  corners; the same triplanar `TerrainMaterial`. Rebuilt on `TerrainPainted` (step 5 adds `GroundHeightsChanged`). The splat shader and
  blended biome edges stay with the roadmap's "Terrain" item.
- **Tile grid**: the boxes' gaps used to show it; now a line mesh along every tile edge, 1 cm above the ground, black at 35 % and drawn
  before the fog pass (`RenderPriority` −1) so the fog still covers it.
- **Roads and rivers**: the same plates as before, lying on each tile's slope (`MapSpace.OnSlope`) just above the ground.
- **Things on the ground**: `MapSpace.ToGround(map, position)`, `GroundY`, `GroundNormal`, `OnSlope`; `MapObjectsView`, `HeroView`,
  `PathPreview` (bound to the map by `AdventureWorldView`), `GrailView` and `EventCellsView` use them; route markers, dug holes and event
  plates lie on the slope. `HeroView` walks a step with `TweenMethod`, the height sampled every frame. Its unused no-context `Bind` is gone.
- **Picking**: `MapSpace.PickTile(camera, point, map, level)` marches the ray from the level's highest ground down to its lowest in steps
  of 0.2 tile and refines the first crossing by bisection; the game, the replay viewer and the editor pass their map.
- **Fog**: the empty-depth fallback and the stars stay at the level's base; a new `top_y` uniform (the level's highest ground) lifts the
  cloud layers to +3 / +6 above it.
- **Camera**: `StrategyCamera.FocusOn` still aims at the level's base (not done: a focus on high ground sits a little off centre).
- Checks: smoke scenarios `game-route-and-move` (a route on the Demo map's slopes: picked, previewed, walked), `game-ember-march`,
  `game-arena-shipped`, `game-grail`, `editor-generate`, `editor-generated-connections`, `editor-tools`, all clean.

## Step 5: the editor ✅ (2026-11-13)
- **Core**: `HeightEditor` (`session.Heights`): `Raise`, `Lower`, `Level(…, target)` (towards a height; the app passes `HeightAt` of the
  stroke's first tile), `Smooth` (towards the eight neighbours' mean as they were before the dab, all the way at strength 4), each with a
  radius and a strength of 1–8 eighths; the brush is round, full within radius + ¾ tile of the tile's centre and fading (smoothstep) over
  one more tile. `FromTerrain(center, radius)` (the square brush, the "selection", as the editor has no selection tool) and `FromTerrain()`
  (every level, objects levelled again, `ReliefShaper.Shape`). All clamped to the limits.
- **Commands**: a `GroundEdit` runs the shaping once, keeps the changed corners with their old and new heights, and on redo/undo puts
  those back; it publishes `GroundHeightsChanged(level, CornerArea)` (`EditorEvents.cs`) per level. A tool that changes no corner records
  nothing (`EditorContext.Done` records a command that already ran). Strokes group dabs as for terrain.
- **Core shaping**: `ReliefShaper.FromTerrain(map, level, CornerArea)` reshapes an area with exactly the whole-level values (the blur runs
  on a window 3 corners larger); `ReliefShaper.Level(map, objects, others)` levels some objects and leaves the others' footprints.
- **Auto height** (`HeightEditor.AutoHeight`, on by default, not saved): `TerrainEditor.Paint` reshapes the painted tiles' corners plus 3
  corners around from terrain and levels again the objects whose ground reaches in, in the same command (`PaintLayer`'s ground hook).
- **Levelling on placement**: `ObjectEditor.Place` levels a multi-tile object with an entrance (towns are placed through it) before adding
  it, so its view is built on level ground; neighbours' footprints stay. One-tile objects, monsters and heroes are not levelled.
- **Views**: `AdventureMapView` rebuilds the ground and the roads and rivers in the area; `MapObjectsView` re-places the objects there;
  `HeroView` a standing hero; `GrailView` and `EventCellsView` redraw; `FogView` moves the clouds' `top_y`.
- **UI**: a "Height" tool in `ToolPalette` (Raise / Lower / Level / Smooth / From terrain, radius, strength, "Heights from terrain", "Auto
  height"; the terrain tool shows "Auto height" too); `Main.cs` routes it like the terrain brush.
- Tests: `HeightEditorTests` (the brush and its fade, radius, clamping at 0 and 4 and at the shore, level, smooth, from terrain under the
  brush, the area reshape equal to the whole level's, Auto height on and off with one undo, a town levelled on a slope and undone,
  re-levelled after painting near it, a neighbour's footprint kept, one-tile objects not levelled); `EditorUndoTests` (every height tool, a
  stroke, painting with Auto height off); `EditorNoOpEditTests` (tools that change nothing record nothing). Smoke scenario `editor-heights`.

## Not in this plan
- The heightmapped splat terrain (roadmap "Terrain"); tuning the table (look-dev spike); `StrategyCamera.FocusOn` at the ground's height.
