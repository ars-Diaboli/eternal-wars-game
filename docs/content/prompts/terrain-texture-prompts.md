# Terrain texture prompts (for an image- or material-generating model)

Prompts for the **ground textures** of the adventure map and the battlefield: original, seamless PBR materials, one per terrain.
Three rules carry every prompt (ADR-244):

1. **Each terrain is recognisable at a glance**: its own colour and its own surface structure (see [Tell them apart](#tell-them-apart)).
   Two terrains that differ only in a few details are a failed result, however good each looks alone.
2. **Rich, saturated, fairly bright colour in the texture.** The game's light, fog and grading make the dim mood and can always take
   colour away; a faded texture cannot be made to read again.
3. **The real scale of the map**: one texture covers about **30 × 30 m** of ground on the adventure map (a tile is about 2 m).
   Plants read as dense masses and clumps, never as single big leaves or blades.

## Use a prompt

Each terrain section below ends with its own block, `TERRAIN-<id>` (the id is the texture's file name). A prompt is a lead block and
the terrain's block, each copied exactly and joined with a space:

```text
PROMPT   = MAP-LEAD + TERRAIN-<id>      (adventure map)
         = BATTLE-LEAD + TERRAIN-<id>   (battlefield)
NEGATIVE = TERRAIN-NEG   (only where the tool takes a negative prompt)
OUTPUT   = <id>.png and its maps, as under "Output for every terrain"
```

The **Signature**, **Look** and **Avoid** notes in each section explain the block and are for checking the result; they are not pasted.
The terrain's block starts with its signature on purpose: the model weighs the first words most. If a result shows something on the
Avoid list, add those words to the negative prompt for that terrain. Do not paraphrase the blocks.

## Blocks

**`MAP-LEAD`** (style and scale, first in every adventure-map prompt):

```text
Seamless tileable PBR ground material, perfectly top-down orthographic view, evenly lit with flat ambient light, no shadows, no
perspective. Rich saturated natural colour, vivid and clean, bright and clear like a sunny high-resolution aerial survey photo, strong
colour identity, crisp detail. Albedo without lighting, with matching normal, roughness, ambient occlusion and height maps. Covers a
large area of about 30 by 30 metres of ground, seen from high above like a drone photo; the ground's typical plants, stones and cracks
are small at this scale, many hundreds of them, forming dense natural clusters and drifts; a single grass tuft or pebble is only a few
pixels; clear medium-scale pattern of patches and clumps that defines the surface, bright raised parts and darker hollows.
```

**`BATTLE-LEAD`** (style and scale, first in every battlefield prompt):

```text
Seamless tileable PBR ground material, perfectly top-down orthographic view, evenly lit with flat ambient light, no shadows, no
perspective. Rich saturated natural colour, vivid and clean, bright and clear like a sunny high-resolution aerial survey photo, strong
colour identity, crisp detail. Albedo without lighting, with matching normal, roughness, ambient occlusion and height maps. Covers
about 12 by 12 metres of ground, seen from a low aerial view; individual tufts, stones, cracks and leaves visible but small, no single
feature larger than about 1 metre; no straight lines, no grid-like or tile-like pattern; the colour as strong as on the map.
```

**`TERRAIN-NEG`** (negative prompt, every terrain):

```text
dull, faded, washed out, desaturated, greyish cast, brownish haze, sepia, low contrast, muddy colours, overcast, perspective, horizon,
sky, cast shadows, sun glare, specular highlights, vignette, visible seams, tiled pattern, repeated identical objects, single large
standout object, oversized plants, close-up macro, text, logos, signatures, watermark, people, animals, footprints in a line, man-made
modern objects, plastic, cartoon outlines, cel shading, pixel art, low resolution, blurry, neon, pure black, pure white, another game's
art.
```

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
| rocky ground | dark blue-grey speckled granite, rusty ochre stains | wide smooth slabs with grit joints | medium-dark |

Stones belong to rough, rocky ground, rock, beach (shingle) and highlands (slabs) only; grass, forest floor, dirt, sand and swamp show
none or almost none. Grey stones scattered on every ground are what made the first set look alike.

## Output for every terrain

- **Seamless and tileable** on both axes, square; verify with a 3 × 3 repeat: no visible seam, no single feature that repeats as a
  pattern (one big skull, one odd stone, one dark puddle are the usual giveaways).
- **Straight top-down, orthographic**: no perspective, no horizon, no cast shadows, no baked highlight or specular glint, no vignette.
  Soft ambient occlusion in crevices only. **Evenly lit, no large-scale gradient**: the game adds its own broad light and dark patches.
- **Sizes**: adventure map **2048 px** (the game uses a 1024 px layer, so one tile gets about 64 px: a feature that should read must span
  several pixels); battlefield **2048 px**, or 4096 px for the closest terrains (grass, dirt, sand) *(proposal)*.
- **Real-world scale**: adventure map about **30 × 30 m**, battlefield about **12 × 12 m**. A result in which one plant is as large as a
  tenth of the picture is at the wrong scale.
- **Colour**: the dominant tone clearly coloured (saturation roughly 35–70 % for the living and earthy grounds; snow, ice and rock may be
  paler), not grey-brown.
- **Value range**: albedo between about **30 and 225 of 255** (snow and ice up to 240), each terrain at its row's value in the table.
  Clear contrast at the medium scale (the clumps and patches).
- **Maps**: albedo (sRGB, no lighting), normal (OpenGL, Y+), roughness, ambient occlusion, height (16-bit grey, **bright = raised**;
  neighbouring terrains blend by height, raised parts first, and the albedo's brightness stands in for a missing height map, so keep the
  albedo's light parts on the raised parts). Packed ORM (R occlusion, G roughness, B metallic = 0) is fine.
- **Names**: `<id>.png` (`forest_floor.png`, `cursed_lands.png`, `shallow_water.png`, `rocky_ground.png`, ...); the other maps
  `<id>_normal.png`, `<id>_roughness.png`, `<id>_ao.png`, `<id>_height.png`; battlefield variants `battle_<id>.png` *(proposal)*.
- **Light motifs only**: the world is dying (the Waning Eye, the Wheel of Ash), but the texture carries it only in each terrain's own way
  (the forest's grey rot, the cursed lands' ash, the wasteland's burn scars); no ash film, charcoal or sherds on every ground.

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

Academy's peaks.

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

Frozen lakes and glacier tongues.

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
- **Avoid**: smooth speckled granite slabs (rocky ground's), a pebble field, round boulders, a man-made wall.

**`TERRAIN-rock`** (subject):

```text
Rock: dark slate-grey fractured mountain bedrock, irregular blocks and ledges, wavy strata lines, green moss and yellow-green lichen
in the cracks, loose scree in the hollows, reads as a cliff face from any side, heavy and ancient.
```

## Rocky ground (`rocky_ground`)

Walkable bare rock; Dragon Utopia's ground.

- **Signature**: dark speckled granite slabs with rusty stains (Dragon Utopia's crag, ADR-284).
- **Look**: wide smooth slabs of dark charcoal and blue-grey coarse-grained granite, speckled with pale feldspar, rusty ochre mineral
  stains and patches of pale lichen, worn and pitted by wind, grit in the joints, sparse dry tufts; a few deep old claw scratches and
  a scorched patch.
- **Avoid**: red or pink sandstone; fractured slate with strata and green moss (rock's), broken stones (rough's), loose scree, a cliff.

**`TERRAIN-rocky_ground`** (subject):

```text
Rocky ground: ancient bedrock plateau of wide smooth slabs of dark charcoal and blue-grey coarse-grained granite speckled with pale
feldspar, rusty ochre mineral stains and patches of pale lichen, worn and pitted by wind, fine grit in the joints, sparse dry tufts, a
few deep old claw scratches across the stone, one small scorched patch.
```

## Battlefield variants

Same terrain block, with `BATTLE-LEAD` instead of `MAP-LEAD`. A thin white cell grid and the units are drawn on top, and one
texture fills the whole field, so:

- **No high-contrast speckle at cell size** (a cell is about 1.5 m) and no straight lines.
- **Variation from small clusters**, never from one large feature.
- **The same colour as the map**: a battle on grass looks like the grass the hero stood on.
- **No obstacles** in the texture: stones, trees and craters are separate models ([environment-prompts.md](environment-prompts.md)).

## After generating

1. Check the 3 × 3 repeat for seams and repeating features; regenerate rather than paint over seams.
2. Put all the terrains made so far side by side at 64 px: each must still be named at a glance. Regenerate the one closest to another.
3. Check the scale (a plant or stone is a speck at 64 px per 2 m), the colour and value range, and that the light parts match the height
   map's raised parts.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/terrain/` (adventure-only pictures in `textures/terrain/adventure/`,
   ADR-240), add a line to `CREDITS.md` with the tool and its licence terms, and look at them in the `game-weeks` and `game-battle-3d`
   smoke scenarios. If the map then looks too loud, tone it down in the game's light and grading, not in the texture.
