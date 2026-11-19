# Terrain texture prompts (for an image- or material-generating model)

Prompts to **replace the placeholder ground textures** of the adventure map and the battlefield with original, seamless PBR materials.
They are for a text-to-texture / text-to-material model (or an image model, with the maps derived afterwards).

Three rules carry every prompt (ADR-244):

1. **Each terrain is recognisable at a glance**: its own colour and its own surface structure (see [Tell them apart](#tell-them-apart)).
   Two terrains that differ only in a few details are a failed result, however good each looks alone.
2. **Rich, saturated, fairly bright colour in the texture.** The world's dim, cold mood comes from the game's light, fog and grading,
   which can always take colour away; a faded texture cannot be made to read again.
3. **The real scale of the map**: one texture covers about **30 × 30 m** of ground on the adventure map, so a tile is about 2 m, a
   hero's height. Plants read as dense masses and clumps, never as single big leaves or blades.

## Compose a prompt

Each terrain section below ends with its own block, `TERRAIN-<id>` (the id is the texture's file name). Build the prompt from blocks,
copying each block's text exactly and joining them with a space:

```text
Adventure map:  TERRAIN-STYLE + TERRAIN-MAP    + TERRAIN-<id>
Battlefield:    TERRAIN-STYLE + TERRAIN-BATTLE + TERRAIN-<id>
NEGATIVE:       TERRAIN-NEG   (only where the tool takes a negative prompt)
OUTPUT:         the sizes, maps and names under "Output for every terrain"
```

The **Signature**, **Look** and **Avoid** notes in each section explain the block and are for checking the result; they are not pasted.
The terrain's block starts with its signature on purpose: the model weighs the first words most. If a result shows something on the
Avoid list, add those words to the negative prompt for that terrain. Do not paraphrase the blocks.

## Blocks

**`TERRAIN-STYLE`** (style, first in every prompt):

```text
Seamless tileable PBR ground material, perfectly top-down orthographic view, evenly lit with flat ambient light, no shadows, no
perspective. Rich saturated natural colour, vivid and clean, bright and clear like a sunny high-resolution aerial survey photo,
strong colour identity, crisp detail. Albedo without lighting, with matching normal, roughness, ambient occlusion and height maps.
```

**`TERRAIN-MAP`** (scale, adventure-map textures):

```text
Covers a large area of about 30 by 30 metres of ground, seen from high above like a drone photo; the ground's typical plants, stones
and cracks are small at this scale, many hundreds of them, forming dense natural clusters and drifts; a single grass tuft or pebble is
only a few pixels; clear medium-scale pattern of patches and clumps that defines the surface, bright raised parts and darker hollows.
```

**`TERRAIN-BATTLE`** (scale, battlefield textures):

```text
Covers about 12 by 12 metres of ground, seen from a low aerial view; individual tufts, stones, cracks and leaves visible but small, no
single feature larger than about 1 metre; no straight lines, no grid-like or tile-like pattern; the colour as strong as on the map.
```

**`TERRAIN-NEG`** (negative prompt, every terrain):

```text
dull, faded, washed out, desaturated, greyish cast, brownish haze, sepia, low contrast, muddy colours, overcast, perspective, horizon,
sky, cast shadows, sun glare, specular highlights, vignette, visible seams, tiled pattern, repeated identical objects, single large
standout object, oversized plants, close-up macro, text, logos, signatures, watermark, people, animals, footprints in a line, man-made
modern objects, plastic, cartoon outlines, cel shading, pixel art, low resolution, blurry, neon, pure black, pure white, another game's
art.
```

Sources: the world from [lore.md](../lore.md) (a draft), art direction from [presentation.md](../../design/presentation.md), the native
terrains of the towns as in [town-model-prompts.md](town-model-prompts.md), the rendering from ADR-183 (`shaders/terrain_splat.gdshader`,
`TerrainSplat`) and `BattleFieldBuilder`. The current textures and their licences are listed in
`src/Godot/EternalWars.Godot.Assets/textures/terrain/CREDITS.md`. Numbers marked *(proposal)* are not decided yet.

## How the game uses them

- **Adventure map** (ADR-183): one splat-shaded mesh a level. Each terrain is one layer of a texture array (**1024 px a layer**; other
  sizes are resized), projected from above in world space and repeated every **16 tiles** (`AdventureMapView.TilesPerTextureRepeat`;
  1 tile = 1 Godot unit), its repeat hidden by shifted cells. So one tile gets only **64 px** of the layer: anything smaller than about
  a tenth of a tile is a speck, and a feature that should read needs to span several pixels. Neighbouring terrains blend along rounded,
  wandering lines **by height** (ADR-200): each ground's **raised parts win first**, so its stones, tufts and crests poke into its
  neighbour and the neighbour fills its hollows. The height is `<terrain>_height.png`, or the albedo's brightness when there is none.
  Steep ground (cliffs) is textured from the sides as well (triplanar). Broad lighter and darker patches are added by the shader, so the
  texture itself should be **evenly lit, with no large-scale gradient**.
- **Battlefield** (`BattleFieldBuilder.Ground`): one plane of the battle terrain, **24 cells deep** (a cell is 0.5 units, so 12 units)
  inside a darker 6-unit band of the same ground, triplanar in world space, repeated every **4 units = 8 cells**. A creature stands on
  about a cell, so 8 cells are about 12 m. The camera is much closer than on the map: battle textures need **finer detail and twice the
  resolution**. A thin white cell grid (12 % alpha) is drawn on top, so the ground must not have strong straight lines or a cell-sized
  pattern.
- **Mood is the game's job**: light, fog, the shader's patches and the post grading darken and cool the ground. A texture that already
  looks dim ends up grey-brown on screen, and every terrain looks alike. Keep the colour in the texture.
- **Today the albedo, the height map and (adventure only) the normal, roughness and AO maps are read** (ADR-240).
- **Borrowed textures**: shallow water borrows water's, rocky ground rough's, cursed lands wasteland's, ice snow's (`AdventureMapView.TextureOf`,
  told apart by a tint). **Snow and ice have no texture yet** (palette colour). Each terrain below gets its own texture; when they exist,
  `TextureOf` and `Tint` drop the borrowing.

## Tell them apart

Every terrain owns a hue and a structure no neighbour shares. Check a new texture against this table and against the textures already
made, side by side at thumbnail size (64 px): each must still be named correctly.

| Terrain | Signature colour | Signature structure | Value |
|---|---|---|---|
| grass | lush emerald and spring green | dense unbroken carpet of grass clumps, nothing else | medium-bright |
| highlands | cool blue-green turf, magenta-purple heather | heather cushions in patches, flat grey stone slabs | medium |
| forest floor | russet, copper and orange leaves, bright moss green | leaf litter with moss mats and curving roots | medium-dark |
| dirt | rich chocolate and reddish-brown, wet | churned mud with ruts, hoof prints and glossy puddles | medium-dark |
| rough | warm ochre and tan, orange lichen | angular broken stones packed edge to edge | medium |
| wasteland | pale bone-yellow and cream, white salt | flat hardpan in big polygon cracks, nearly bare | bright |
| cursed lands | violet-black, sickly pale green-white | ashen soil, black dead stalks, mould blooms | dark |
| sand | golden yellow and orange | smooth wind ripples, nothing else | bright |
| beach | pale grey-beige and slate | rounded shingle, wet dark sand, wrack lines | medium-bright |
| swamp | dark olive mud, vivid lime duckweed | duckweed mats on black water, reed clumps | dark |
| water | deep teal and blue | near-uniform depth, soft murk | dark |
| shallow water | clear amber and green over golden sand | sand bottom with pebbles and weed under clear water | medium |
| snow | white with soft blue shade | wind ripples and drifts | brightest |
| ice | pale cyan-blue | smooth sheets with a white crack web | bright |
| lava | black crust, vivid orange-red seams | cracked plates with glowing cracks | dark, hot accents |
| subterranean | blue-grey slate, violet minerals | smooth wet flowstone, mineral veins | dark |
| rock | dark slate grey | fractured blocks and ledges | medium-dark |
| rocky ground | terracotta and salmon-pink sandstone | wide smooth slabs with grit joints | medium-bright |

Stones belong to rough, rocky ground, rock, beach (shingle) and highlands (slabs) only; grass, forest floor, dirt, sand and swamp show
none or almost none. Grey stones scattered on every ground are what made the first set look alike.

## Output for every terrain

- **Seamless and tileable** on both axes, square; verify with a 3 × 3 repeat: no visible seam, no single feature that repeats as a
  pattern (one big skull, one odd stone, one dark puddle are the usual giveaways).
- **Straight top-down, orthographic**: no perspective, no horizon, no cast shadows of a low sun, no baked highlight or specular glint,
  no vignette. Soft ambient occlusion in crevices only.
- **Sizes**: adventure map **2048 px**, downsampled to the 1024 px layer *(proposal: keeps detail sharp at 1024)*; battlefield
  **2048 px** used as is, or 4096 px for the closest terrains (grass, dirt, sand) *(proposal)*.
- **Real-world scale** (what one texture covers): adventure map about **30 × 30 m** (16 tiles of about 2 m), battlefield about
  **12 × 12 m** (8 cells of about 1.5 m). State it in the prompt; tufts, stones and cracks must be sized to it. A result in which one
  plant is as large as a tenth of the picture is at the wrong scale.
- **Colour**: the dominant tone clearly coloured (saturation roughly 35–70 % for the living and earthy grounds; snow, ice and rock are
  allowed to be paler), not grey-brown. The game takes colour away; it does not add it.
- **Value range**: albedo between about **30 and 225 of 255** (snow and ice up to 240), each terrain at its row's value in the table, so
  the light, dark and middle grounds stay apart. Clear contrast at the medium scale (the clumps and patches); no large-scale gradient.
- **Maps**: albedo (sRGB, no lighting), normal (OpenGL, Y+, as Godot expects), roughness, ambient occlusion, height (16-bit grey,
  **bright = raised**: the splat's height blend reads it, and the albedo's brightness stands in for it until it exists, so keep the
  albedo's light parts on the height map's raised parts). Packed ORM (R occlusion, G roughness, B metallic = 0) is fine.
- **Names**: `<terrain>.png` as today (`forest_floor.png`, `cursed_lands.png`, `shallow_water.png`, `rocky_ground.png`, `snow.png`,
  `ice.png`); the other maps as `<terrain>_normal.png`, `<terrain>_roughness.png`, `<terrain>_ao.png`, `<terrain>_height.png`; a
  picture for the adventure map only in `textures/terrain/adventure/` (ADR-240); battlefield variants as `battle_<terrain>.png`
  *(proposal: the game does not load them yet; until it does, the map texture serves both)*.
- **Licence**: every new texture gets a line in `CREDITS.md` with the tool and its licence terms; the three placeholders marked
  "check before release" are the first to replace (grass, highlands, forest floor).

**The world** (for the Look notes, not the colour): *Vael* is dying under a dimming sun, the **Waning Eye**; every age ends when the
**Wheel of Ash** turns and kingdoms burn. The game shows that through its light, fog and grading, and through objects on the map. The
textures carry it only lightly, in each terrain's own way (the forest's grey rot, the cursed lands' ash, the wasteland's burn scars);
no ash film, charcoal or sherds on every ground.

| Group | Native ground |
|---|---|
| Dawn Covenant | highlands, grass (Haven), forest floor (Sylvan), snow and ice (Academy) |
| Ashen Court | cursed lands (Necropolis), lava (Inferno), subterranean (Dungeon) |
| Unbound | rough, wasteland (Stronghold), swamp, shallow water (Swamp Bestiary) |
| Firstborn | rocky ground (Dragon Utopia) |

## Grass (`grass`)

The common lowland of the living kingdoms; the ground seen most, so it sets the map's tone.

- **Signature**: lush saturated green, dense and unbroken.
- **Look**: a thick wild meadow seen from high above: grass clumps packed so close no soil shows, in several greens (emerald, spring
  green, a little yellow-green on the tops), scattered tiny white and yellow flower specks, darker green in the hollows between clumps.
- **Avoid**: bare earth, stones, straw-yellow dead grass, single large leaves or blades, a mown lawn's stripes.

**`TERRAIN-grass`** (subject):

```text
Grass: lush saturated emerald and spring green, a dense thick wild meadow, countless grass clumps packed tightly together so no soil
shows, yellow-green sunlit tops and deep green hollows between the clumps, tiny scattered white and yellow flower specks, healthy and
vivid.
```

## Dirt (`dirt`)

Trodden earth: tracks, fallow fields and the ground around camps, softened by rain.

- **Signature**: rich wet brown mud, churned.
- **Look**: chocolate to reddish-brown soil, wet and soft, churned by hooves, boots and wheels into irregular ruts and ridges, glossy
  puddles in the low spots (roughness low there), clods and a little trampled straw. Lighter dry crests, dark wet hollows.
- **Avoid**: dry cracked earth (wasteland's), stones, grass tufts, a black swamp (swamp's), orange clay, straight tyre tracks.

**`TERRAIN-dirt`** (subject):

```text
Dirt: rich wet chocolate and reddish-brown mud, soft churned earth of a busy track, irregular ruts, ridges and hoof prints going every
way, glossy brown puddles in the low spots, wet clods and a little trampled straw, lighter drying crests and dark wet hollows, no
grass and no stones.
```

## Rough (`rough`)

Broken stony ground of the badlands; Stronghold's home.

- **Signature**: warm ochre broken stones, packed edge to edge.
- **Look**: angular stones from fist to boulder size in warm ochre, tan and rust tones, packed close with coarse gravel between, orange
  and yellow lichen crusts on the larger stones, a few wiry dry shrubs. Clearly walkable, not a cliff.
- **Avoid**: grey stones (rock's), round river pebbles, a solid rock face, green grass.

**`TERRAIN-rough`** (subject):

```text
Rough: warm ochre, tan and rust-coloured broken stony ground, countless angular stones from fist to boulder size packed edge to edge,
coarse golden gravel between them, bright orange and yellow lichen crusts on the larger stones, a few wiry dry shrubs, harsh and sunny.
```

## Highlands (`highlands`)

Upland moor and hill pasture; Haven's heartland. Must never be mistaken for grass.

- **Signature**: cool blue-green turf with magenta-purple heather.
- **Look**: short cool blue-green turf, large soft cushions of heather in bloom (magenta, purple, a little rust), pale green moss,
  flat light-grey stone slabs breaking through here and there.
- **Avoid**: warm yellow-green (grass's), a uniform carpet, brown dead heather everywhere.

**`TERRAIN-highlands`** (subject):

```text
Highlands: cool blue-green moorland turf with large patches of magenta and purple flowering heather, soft cushions of pale green
moss, flat light-grey stone slabs breaking through the thin turf here and there, fresh windswept upland colours.
```

## Forest floor (`forest_floor`)

The floor of the old forests; Sylvan's home. Its forest is dying from the root.

- **Signature**: russet and copper leaf litter with bright moss.
- **Look**: a deep litter of fallen leaves in russet, copper, orange and dark red, needles and twigs, mats of bright green moss in
  between, small fern fronds, soft curving roots, patches of dark humus; here and there pale grey rot on leaves and roots.
- **Avoid**: stones, big mushrooms (Dungeon's), a uniform leaf carpet, plain brown.

**`TERRAIN-forest_floor`** (subject):

```text
Forest floor: deep litter of fallen leaves in russet, copper, orange and dark red, with mats of bright green moss between them, pine
needles and small twigs, small fern fronds, soft curving tree roots, patches of dark humus, a few patches of pale grey rot with fine
grey fungal threads on leaves and roots.
```

## Wasteland (`wasteland`)

Burned-out land between the kingdoms; Stronghold's second home.

- **Signature**: pale bone-yellow hardpan in big polygon cracks.
- **Look**: flat, almost bare cracked earth in pale bone, cream and straw-yellow, large polygon cracks with darker edges, crusts of
  white salt, orange dust drifts, a few black burn scars and dead stubble. The brightest dry ground.
- **Avoid**: brown soil (dirt's), stones (rough's), sand dunes (sand's), grass.

**`TERRAIN-wasteland`** (subject):

```text
Wasteland: pale bone-yellow and cream cracked hardpan, a flat sun-baked plain split into large polygon cracks with darker edges,
crusts of bright white salt, drifts of orange dust, a few black burn scars and dead stubble, almost bare, dry and glaring.
```

## Cursed lands (`cursed_lands`)

Necropolis' ground, where the dead rule.

- **Signature**: violet-black ashen soil with pale mould.
- **Look**: dark soil with a strong cold violet and indigo cast, drifts of pale grey-lilac ash, withered black stalks, blooms of sickly
  pale green-white mould and fungus, small splinters of bleached bone.
- **Avoid**: glowing ground (the town's), bright green slime, cartoon skulls, plain black.

**`TERRAIN-cursed_lands`** (subject):

```text
Cursed lands: dark soil with a strong cold violet and indigo tint, drifts of pale grey-lilac ash, withered black plant stalks, blooms
of sickly pale green-white mould and fungus, small splinters of bleached bone, eerie and cold.
```

## Snow (`snow`)

Academy's peaks. No texture yet.

- **Signature**: white with soft blue shade, wind ripples.
- **Look**: clean wind-packed snow in soft curving ripples and drifts, blue in the hollows, a broken crust here and there, a few dark
  grass tips poking through.
- **Value**: the brightest ground in the game, under about 240 of 255 so it still shows relief.

**`TERRAIN-snow`** (subject):

```text
Snow: clean bright wind-packed mountain snow, soft curving wind ripples and drifts, light blue shade in the hollows, a broken crust in
places, a few dark grass tips poking through, crisp and cold.
```

## Ice (`ice`)

Frozen lakes and glacier tongues. No texture yet; today snow's palette colour.

- **Signature**: pale cyan-blue sheets with a white crack web.
- **Look**: smooth ice in pale cyan and blue, deeper blue where it is thick, trapped bubbles, a natural web of white pressure cracks,
  frosted matte patches between polished ones (roughness map: polished low, frosted high), snow dust in the cracks.
- **Avoid**: white like snow, a glass-clear rink, neon cyan.

**`TERRAIN-ice`** (subject):

```text
Ice: smooth frozen lake ice in pale cyan and clear blue, deeper blue where it is thick, trapped air bubbles, a natural web of white
pressure cracks with snow dust in them, frosted matte patches between polished ones.
```

## Lava (`lava`)

Inferno's ground: cooled flows of slag over the ash of burned cities.

- **Signature**: black crust, vivid orange-red glowing seams.
- **Look**: black and charcoal basalt crust, ropy and cracked in plates, with **vivid orange to yellow-red molten seams** in the cracks
  (about a fifth of the area), bright yellow sulphur crust around vents, cinders.
- **Emissive**: the molten seams also as an emissive map (`lava_emissive.png`) *(proposal: the shader does not read it yet)*.
- **Avoid**: a lake of lava (it must be walkable ground), flames, smoke.

**`TERRAIN-lava`** (subject):

```text
Lava: walkable cooled lava field, black and charcoal basalt crust in ropy wrinkles and cracked plates, vivid glowing orange and
yellow-red molten seams in the cracks, bright yellow sulphur crust around small vents, scattered black cinders, hot and intense.
```

## Subterranean (`subterranean`)

The cavern floors of the underground level; Dungeon's home.

- **Signature**: wet blue-grey slate with violet mineral veins.
- **Look**: smooth wet flowstone and worn rock in blue-grey and slate, a wet sheen in the hollows (roughness: wet low), veins and
  streaks of violet and teal minerals, pale cave silt, small clusters of pale fungus.
- **Avoid**: brown or warm tones, big mushrooms, glow, lava.

**`TERRAIN-subterranean`** (subject):

```text
Subterranean: a wet cavern floor of smooth blue-grey and slate flowstone, worn flat stalagmite stumps, a wet sheen in the hollows,
veins and streaks of violet and teal minerals, pale cave silt, small clusters of pale cave fungus, cold and deep.
```

## Sand (`sand`)

Dunes and sandy flats.

- **Signature**: golden yellow-orange wind ripples.
- **Look**: fine sand in golden yellow and warm orange, soft irregular curving ripples with lighter crests and darker troughs, almost
  nothing else (a few shell bits in the troughs).
- **Avoid**: pale grey sand (beach's), stones, grass, perfectly parallel ripples.

**`TERRAIN-sand`** (subject):

```text
Sand: fine golden yellow and warm orange desert sand, soft irregular curving wind ripples with bright crests and deeper orange
troughs, a few tiny shell fragments in the troughs, clean and sunlit.
```

## Beach (`beach`)

The strip between land and water.

- **Signature**: pale grey-beige rounded shingle, wet slate sand.
- **Look**: rounded pebbles and shingle in pale grey, beige and white, patches of wet dark slate-grey sand (no shore line in the
  texture: the blend makes it), dark green-brown seaweed wrack, white shell bits, bleached driftwood twigs.
- **Avoid**: golden sand (sand's), a tropical white beach, foam lines, a straight tide line.

**`TERRAIN-beach`** (subject):

```text
Beach: a northern shore of rounded pebbles and shingle in pale grey, beige and white, patches of wet dark slate-grey sand, tangles of
dark green-brown seaweed wrack, white shell bits, bleached driftwood twigs, fresh and bright.
```

## Swamp (`swamp`)

The drowned lowlands; Swamp Bestiary's home.

- **Signature**: vivid lime duckweed on black water, reed clumps.
- **Look**: black water and dark olive mud, large mats of vivid lime-green duckweed and algae floating on it, clumps of reeds and
  sedge in olive and yellow-green, rotting logs, a few marsh-gas bubbles. Wet sheen everywhere (roughness low).
- **Avoid**: brown mud without plants (dirt's), clear blue water, neon slime.

**`TERRAIN-swamp`** (subject):

```text
Swamp: a drowned marsh, black water and dark olive mud covered in large mats of vivid lime-green duckweed and algae, clumps of olive
and yellow-green reeds and sedge, rotting waterlogged logs, small marsh-gas bubbles, wet and glossy.
```

## Water (`water`)

Deep water: impassable to walkers, crossed by boats.

- **Signature**: deep teal and blue.
- **Look**: the **water's depth**, not its surface: a deep teal to sapphire blue field with soft depth variation and faint drifting
  silt; no waves, no reflections (the PBR pass will add a surface).
- **Avoid**: a visible bottom, foam, sky reflections, near-black.

**`TERRAIN-water`** (subject):

```text
Water: deep clear water seen from above, rich teal and sapphire blue depth with soft variation in tone, faint drifting silt, calm, no
waves, no reflections, no visible bottom.
```

## Shallow water (`shallow_water`)

Fords and shoals: walkable water.

- **Signature**: clear amber-green water over golden sand.
- **Look**: a visible bottom of golden sand, rounded pebbles and green water weed under a thin clear layer of amber-green water, soft
  ripple caustics.
- **Avoid**: deep blue (water's), murky brown, a tropical turquoise lagoon.

**`TERRAIN-shallow_water`** (subject):

```text
Shallow water: a shallow ford seen from above, a visible bottom of golden sand, rounded pebbles and green waving water weed under a
thin layer of clear amber-green water, soft ripple light patterns, fresh and clear.
```

## Rock (`rock`)

The impassable cliffs: the walkable ground's highest rim and the shader's **cliff sides** (triplanar), so it is seen from above and
from the side.

- **Signature**: dark slate-grey fractured blocks.
- **Look**: dark slate-grey bedrock in fractured blocks and ledges, wavy strata, green moss and yellow-green lichen in the cracks,
  scree in the hollows. Must work from the side as well as from above (no top-down-only features such as puddles).
- **Avoid**: warm sandstone (rocky ground's), a pebble field, round boulders, a man-made wall.

**`TERRAIN-rock`** (subject):

```text
Rock: dark slate-grey fractured mountain bedrock, irregular blocks and ledges, wavy strata lines, green moss and yellow-green lichen
in the cracks, loose scree in the hollows, reads as a cliff face from any side, heavy and ancient.
```

## Rocky ground (`rocky_ground`)

Walkable bare rock; Dragon Utopia's ground.

- **Signature**: terracotta and salmon-pink sandstone slabs.
- **Look**: wide smooth slabs of terracotta, salmon-pink and warm cream sandstone, worn and pitted by wind, grit in the joints, sparse
  hardy tufts; a few deep old claw scratches and a scorched patch.
- **Avoid**: grey stone (rock's), broken stones (rough's), loose scree, a cliff.

**`TERRAIN-rocky_ground`** (subject):

```text
Rocky ground: ancient bedrock plateau of wide smooth slabs of terracotta, salmon-pink and warm cream sandstone, worn and pitted by
wind, fine grit in the joints, sparse hardy green tufts, a few deep old claw scratches across the stone, one small scorched patch.
```

## Battlefield variants

The battlefield uses the terrain the battle is fought on (`BattleState.Terrain`; a map object can set it). Use the same terrain prompt
with `TERRAIN-BATTLE` instead of `TERRAIN-MAP` (see [Compose a prompt](#compose-a-prompt)).

Points to keep in mind:

- **The cell grid and the units read on top**: no high-contrast speckle at cell size (0.5 units), no lines.
- **One texture fills the whole field** (12 units deep, plus the 6-unit band): variation must come from small clusters, not from a
  single large feature; the battlefield has no splat mixing or patch noise.
- **Same colour as the map**: a battle on grass must look like the grass the hero stood on.
- **Water and deep lava** are not battle grounds in the rules' sense of standing on them; when a battle is fought "on water" (a boat
  object), use shallow water's battlefield texture *(proposal)*.
- **Obstacles** are separate meshes (stones, trees, craters, cliffs); keep them out of the ground texture.

## After generating

1. Check the 3 × 3 repeat for seams and repeating features; regenerate rather than paint over seams.
2. Put all the terrains made so far side by side at 64 px: each must still be named at a glance (see [Tell them apart](#tell-them-apart)).
   Regenerate the one that is closest to another.
3. Check the scale: a single plant or stone should be a speck at 64 px per 2 m, not a shape.
4. Check the colour and value range (about 30–225 of 255, clearly coloured) and that the light parts match the raised parts of the
   height map.
5. Put the file in `src/Godot/EternalWars.Godot.Assets/textures/terrain/` under the name above, add its line to `CREDITS.md`, and run
   the `game-weeks` and `game-battle-3d` smoke scenarios to see it on the map and the battlefield. If the map then looks too loud,
   tone it down in the game's light and grading, not in the texture.
6. For a terrain that had a borrowed texture (shallow water, rocky ground, cursed lands, ice), remove it from
   `AdventureMapView.TextureOf` and its `Tint` so its own texture shows untinted.
