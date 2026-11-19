# Terrain texture prompts (for an image- or material-generating model)

Prompts to **replace the placeholder ground textures** of the adventure map and the battlefield with original, seamless PBR materials:
richer in detail, true to the world of Vael and believable as real ground. They are for a text-to-texture / text-to-material model
(or an image model, with the maps derived afterwards).

## Compose a prompt

Each terrain section below ends with its own block, `TERRAIN-<id>` (the id is the texture's file name). Build the prompt from blocks,
copying each block's text exactly and joining them with a space:

```text
Adventure map:  TERRAIN-STYLE + TERRAIN-MAP    + TERRAIN-<id>
Battlefield:    TERRAIN-STYLE + TERRAIN-BATTLE + TERRAIN-<id> + TERRAIN-BATTLE-CLOSE
NEGATIVE:       TERRAIN-NEG   (only where the tool takes a negative prompt)
OUTPUT:         the sizes, maps and names under "Output for every terrain"
```

The **Look**, **Lore** and **Avoid** notes in each section explain the block and are for checking the result; they are not pasted.
If a result shows something on the Avoid list, add those words to the negative prompt for that terrain. Do not paraphrase the blocks.

## Blocks

**`TERRAIN-STYLE`** (style, first in every prompt):

```text
Seamless tileable PBR ground material, perfectly top-down orthographic view, evenly lit with flat ambient light, no shadows, no
perspective. Grim dark fantasy world, photorealistic natural ground, weathered, desaturated earth tones, high-frequency natural detail,
believable geology and plant growth, no repeating standout features. Albedo without lighting, with matching normal, roughness, ambient
occlusion and height maps.
```

**`TERRAIN-MAP`** (scale, adventure-map textures):

```text
Covers about 6 by 6 metres of ground, seen from high above; medium detail that still reads when small on screen, natural variation in
soft clusters, low large-scale contrast so it blends with neighbouring terrains, light-coloured raised parts (stones, tufts, crests)
standing out a little from darker hollows.
```

**`TERRAIN-BATTLE`** (scale, battlefield textures):

```text
Covers about 4 by 4 metres of ground, seen up close from a low aerial view; crisp fine detail of individual stones, blades, cracks and
debris; no straight lines, no grid-like or tile-like pattern; calm enough at the middle scale that small armies stand out on it.
```

**`TERRAIN-NEG`** (negative prompt, every terrain):

```text
perspective, horizon, sky, cast shadows, sun glare, specular highlights, vignette, visible seams, tiled pattern, repeated identical
objects, single large standout object, text, logos, signatures, watermark, people, animals, footprints in a line, man-made modern
objects, plastic, cartoon, painterly, stylized, cel shading, pixel art, low resolution, blurry, oversaturated colours, neon, pure black,
pure white, another game's art.
```

**`TERRAIN-BATTLE-CLOSE`** (detail, battlefield textures, after the terrain's own block):

```text
Same ground as before, closer: individual grains, blades and pebbles visible, two to three scales of detail, no feature larger than
about 40 centimetres.
```

Sources: the world from [lore.md](../lore.md) (a draft), art direction from [presentation.md](../../design/presentation.md), the native
terrains of the towns as in [town-model-prompts.md](town-model-prompts.md), the rendering from ADR-183 (`shaders/terrain_splat.gdshader`,
`TerrainSplat`) and `BattleFieldBuilder`. The current textures and their licences are listed in
`src/Godot/EternalWars.Godot.Assets/textures/terrain/CREDITS.md`. Numbers marked *(proposal)* are not decided yet.

## How the game uses them

- **Adventure map** (ADR-183): one splat-shaded mesh a level. Each terrain is one layer of a texture array (**1024 px a layer**; other
  sizes are resized), projected from above in world space and repeated every **3 tiles** (`uv_scale = 0.333`; 1 tile = 1 Godot unit).
  Neighbouring terrains blend along rounded, wandering lines **by height** (ADR-200): each ground's **raised parts win first**, so its
  stones, tufts and crests poke into its neighbour and the neighbour fills its hollows. The height is `<terrain>_height.png`, or the
  albedo's brightness when there is none. Steep ground (cliffs) is textured from the sides as well (triplanar). Broad
  lighter and darker patches are added by the shader, so the texture itself should be **evenly lit, with no large-scale gradient**.
- **Battlefield** (`BattleFieldBuilder.Ground`): one plane of the battle terrain, **24 cells deep** (a cell is 0.5 units, so 12 units)
  inside a darker 6-unit band of the same ground, triplanar in world space, repeated every **4 units = 8 cells**. The camera is much
  closer than on the map, so the same 1024 px covers more of the screen: battle textures need **finer detail and twice the resolution**.
  A thin white cell grid (12 % alpha) is drawn on top, so the ground must not have strong straight lines or a cell-sized pattern.
- **Today only the albedo and the height map are read.** The normal, roughness and AO maps are asked for now so the PBR pass (ADR-183's "not
  changed" note) does not need the textures made again.
- **Borrowed textures**: shallow water borrows water's, rocky ground rough's, cursed lands wasteland's, ice snow's (`AdventureMapView.TextureOf`,
  told apart by a tint). **Snow and ice have no texture yet** (palette colour). Each terrain below gets its own texture; when they exist,
  `TextureOf` and `Tint` drop the borrowing.

## Output for every terrain

- **Seamless and tileable** on both axes, square; verify with a 3 × 3 repeat: no visible seam, no single feature that repeats as a
  pattern (one big skull, one odd stone, one dark puddle are the usual giveaways).
- **Straight top-down, orthographic**: no perspective, no horizon, no cast shadows of a low sun, no baked highlight or specular glint,
  no vignette. Soft ambient occlusion in crevices only.
- **Sizes**: adventure map **2048 px**, downsampled to the 1024 px layer *(proposal: keeps detail sharp at 1024)*; battlefield
  **2048 px** used as is, or 4096 px for the closest terrains (grass, dirt, sand) *(proposal)*.
- **Real-world scale** (what one texture covers): adventure map about **6 × 6 m** *(proposal: a tile reads as about 2 m of ground, so
  a hero's figure stays in scale)*, battlefield about **4 × 4 m** *(proposal: a cell reads as about 0.5 m)*. State it in the prompt;
  pebbles, tufts and cracks must be sized to it.
- **Maps**: albedo (sRGB, no lighting), normal (OpenGL, Y+, as Godot expects), roughness, ambient occlusion, height (16-bit grey,
  **bright = raised**: the splat's height blend reads it, and the albedo's brightness stands in for it until it exists, so keep the
  albedo's light parts on the height map's raised parts). Packed
  ORM (R occlusion, G roughness, B metallic = 0) is fine.
- **Value range**: albedo between about **40 and 200 of 255** in the dominant tone (no pure black, no blown-out white except snow and
  ice, kept under 235), so the dark grading and fog still read. Low to medium contrast at the large scale, rich contrast in the small.
- **Names**: `<terrain>.png` as today (`forest_floor.png`, `cursed_lands.png`, `shallow_water.png`, `rocky_ground.png`, `snow.png`,
  `ice.png`); the other maps as `<terrain>_normal.png`, `<terrain>_orm.png`, `<terrain>_height.png`; battlefield variants as
  `battle_<terrain>.png` *(proposal: the game does not load them yet; until it does, the map texture serves both)*.
- **Licence**: every new texture gets a line in `CREDITS.md` with the tool and its licence terms; the three placeholders marked
  "check before release" are the first to replace (grass, highlands, forest floor).

**The world in one breath** (keep it in mind for every terrain): *Vael* is dying under a dimming sun, the **Waning Eye**; every age ends
when the **Wheel of Ash** turns and kingdoms burn. So the ground is **old and tired**: light is weak and cool, plants are a little
starved, and **traces of earlier ages** show through in places (a pale ash film, charcoal flecks, a sherd of fired clay, a weathered
worked stone, a rusted nail). Keep those traces **rare and small** (at most a few per texture, never the subject), or they repeat
visibly. Colours stay muted; the only strong colours on the map are the players' and the magic's.

| Group | Native ground | Ground's mood |
|---|---|---|
| Dawn Covenant | highlands, grass (Haven), forest floor (Sylvan), snow and ice (Academy) | holding on: alive but thinning |
| Ashen Court | cursed lands (Necropolis), lava (Inferno), subterranean (Dungeon) | already past the turning: ash, slag, darkness |
| Unbound | rough, wasteland (Stronghold), swamp, shallow water (Swamp Bestiary) | harsh, raw, survivable |
| Firstborn | rocky ground (Dragon Utopia) | older than any age: worn bedrock |

## Grass (`grass`)

The common lowland of the living kingdoms. Today a stock image of bright green lawn: far too clean and too green.

- **Look**: a meadow gone tired: mixed grasses, olive and grey-green with straw-yellow dead stems through it, a few clover and low
  weeds, small patches of bare earth showing, scattered pebbles. Not mown, not lush.
- **Lore**: the weak sun leaves the green **dull and cool**; a faint grey ash film in places; a rare sherd or charcoal fleck in the soil.
- **Avoid**: lawn, golf-course green, flowers in bright colours, a uniform carpet.

**`TERRAIN-grass`** (subject):

```text
Grass: a tired wild meadow, mixed coarse grasses in muted olive and grey-green with straw-yellow dead stems woven through, small
clover and low weeds, little patches of bare brown earth between the tufts, scattered small pebbles, a faint film of grey ash on some
blades, a rare charcoal fleck in the soil.
```

## Dirt (`dirt`)

Trodden earth: tracks, fields left fallow, the ground around camps.

- **Look**: compacted brown soil with fine cracks where it dried, small stones and grit pressed in, a few dry roots and straw bits,
  shallow ruts softened by rain (no straight lines).
- **Lore**: ash mixed into the soil in grey streaks; a rare bone splinter or sherd.
- **Avoid**: wet mud (that is swamp's), clay that looks orange, tyre-like tracks.

**`TERRAIN-dirt`** (subject):

```text
Dirt: compacted brown earth of an old trodden field, fine drying cracks, small grey stones and grit pressed into the surface, dry
root fibres and bits of straw, shallow soft hollows from old rain, faint grey streaks of ash worked into the soil, a rare small sherd
of fired clay.
```

## Rough (`rough`)

Broken stony ground of the badlands; Stronghold's home.

- **Look**: a mix of angular stones from fist to head size, gravel between them, wiry dry grass and thorny scrub in the gaps, lichen
  on the larger stones. Clearly walkable, not a cliff.
- **Lore**: the scree of old fortifications: now and then a stone with a worked face or a mortar trace.
- **Avoid**: a solid rock face (that is rock's), round river pebbles.

**`TERRAIN-rough`** (subject):

```text
Rough: broken stony badland ground, angular grey-brown stones of mixed sizes half sunk in coarse gravel, wiry dry grass and small
thorny scrub in the gaps, crusts of pale lichen on the larger stones, an occasional stone with a flat worked face from a long-fallen
wall, dusty and harsh.
```

## Highlands (`highlands`)

Upland moor and hill pasture; Haven's heartland. Today a stock grass photo, nearly the same as grass.

- **Look**: short wind-pressed turf with heather and moss, bedrock breaking through in flat grey slabs, small cushion plants; cooler
  and greyer than grass, clearly a different ground from it.
- **Lore**: thin soil, worn by wind; a faint soot film from Haven's braziers carried on the wind is enough.
- **Avoid**: a lawn; purple heather in bloom everywhere (keep it brown and rust).

**`TERRAIN-highlands`** (subject):

```text
Highlands: windswept upland moor, short wind-flattened turf in grey-green, brown and rust heather, cushions of moss, flat slabs of
grey bedrock breaking through the thin soil, small stones, cool muted colours, a faint film of soot on the moss.
```

## Forest floor (`forest_floor`)

The floor of the old forests; Sylvan's home. Its forest is dying from the root.

- **Look**: a deep litter of fallen leaves in brown and dull gold, needles, twigs, moss mats, small ferns, exposed roots as soft
  curves (no straight lines), patches of dark humus.
- **Lore**: the **grey rot**: patches of pale grey decay on leaves and roots, a thin web of grey fungal threads; small carved tablets or
  ribbons belong to the town, not the ground.
- **Avoid**: big mushrooms (Dungeon's), a uniform leaf carpet, bright autumn orange.

**`TERRAIN-forest_floor`** (subject):

```text
Forest floor: the floor of an ancient dying forest, deep litter of fallen leaves in brown, umber and dull gold, needles and small
twigs, mats of green moss, a few small ferns, soft curving exposed roots, dark humus showing between, patches of pale grey rot on
leaves and roots with fine grey fungal threads.
```

## Wasteland (`wasteland`)

Burned-out land between the kingdoms; Stronghold's second home. Today a lichen rock, close to rough.

- **Look**: cracked hardpan of grey-tan earth, crusts of salt and dust, dead grass stubble, charred stumps cut down to their roots,
  grit; flat and exhausted.
- **Lore**: the scar of a war of the last age: charcoal, ash drifts in the cracks, a rare rusted iron scrap.
- **Avoid**: a desert of dunes (sand's), a rocky field (rough's).

**`TERRAIN-wasteland`** (subject):

```text
Wasteland: exhausted burned-out plain, cracked grey-tan hardpan with dust and white salt crust, dead grass stubble, small charred
root stumps, grey ash drifted into the cracks, scattered charcoal and grit, a rare fragment of rusted iron, flat and lifeless.
```

## Cursed lands (`cursed_lands`)

Necropolis' ground, where the dead rule. Today wasteland's texture tinted violet.

- **Look**: dead black-grey soil, a cold grey-violet cast, bone-dry, with withered black stalks, thin pale fungus and a frost-like
  bloom of white mould; small bone fragments and grave dust.
- **Lore**: the ground **remembers every age**: thin layers of old ash, a sherd of black pottery, a few bone fragments, all small.
- **Avoid**: green slime, glowing ground (glow is the town's), Halloween purple, cartoon skulls.

**`TERRAIN-cursed_lands`** (subject):

```text
Cursed lands: dead ground where the dead rule, dry black-grey soil with a cold grey-violet tint, layers of old pale ash, withered
blackened plant stalks, thin pale fungus and a frost-like bloom of white mould, small splinters of old bleached bone and grave dust,
a rare sherd of black pottery, silent and cold.
```

## Snow (`snow`)

Academy's peaks. No texture yet.

- **Look**: wind-packed snow with sastrugi ripples (soft, not straight), a crust that breaks in places, grit and small dark stones
  blown on top, a few dry grass tips showing through.
- **Lore**: the dim sun makes it **blue-grey, not white**; a fine grey dusting of ash on the windward faces.
- **Value**: the brightest ground in the game, keep it under about 235 of 255 so it still shows relief.

**`TERRAIN-snow`** (subject):

```text
Snow: wind-packed mountain snow, soft wind ripples and drifts, a broken crust in places, scattered grit and small dark stones blown on
top, a few dry grass tips poking through, cool blue-grey tone under a dim sky, a faint grey dusting of ash on the windward faces.
```

## Ice (`ice`)

Frozen lakes and glacier tongues. No texture yet; today snow's palette colour.

- **Look**: cloudy blue-grey ice with trapped bubbles and pressure cracks in a natural web, a thin snow dust gathered in the cracks,
  frosted rough patches between polished ones (roughness map: polished low, frosted high).
- **Lore**: dark lines of ash and grit frozen in layers inside the ice, like the rings of past ages.
- **Avoid**: a glass-clear ice rink, bright cyan.

**`TERRAIN-ice`** (subject):

```text
Ice: thick cloudy lake ice in blue-grey, trapped air bubbles, a natural web of white pressure cracks, thin snow dust caught in the
cracks, frosted matte patches between smoother polished ones, faint dark layers of ash and grit frozen inside the ice.
```

## Lava (`lava`)

Inferno's ground: cooled flows of slag over the ash of burned cities. Today a laterite soil darkened red.

- **Look**: black and dark-grey **cooled basalt crust**, ropy and cracked in plates, with **ember-red to orange molten seams** in the
  cracks only (a small share of the area), yellow sulphur crust around vents, cinders.
- **Lore**: melted remains of masonry and a twisted bit of bronze fused into the crust, rarely.
- **Emissive**: the molten seams also as an emissive map (`lava_emissive.png`) *(proposal: the shader does not read it yet)*; keep the
  glow in the cracks, the albedo of the crust dark.
- **Avoid**: a lake of bright orange lava (it must be walkable ground), flames, bright red everywhere.

**`TERRAIN-lava`** (subject):

```text
Lava: walkable cooled lava field, black and dark-grey basalt crust in ropy wrinkles and cracked plates, thin glowing ember-red and
orange molten seams only in the deepest cracks, yellow sulphur crust around small vents, scattered black cinders, a rare fused
lump of melted brick fixed in the crust, hot and harsh.
```

## Subterranean (`subterranean`)

The cavern floors of the underground level; Dungeon's home.

- **Look**: damp dark rock floor, flowstone and small stalagmite stumps worn flat, wet sheen in hollows (roughness: wet low), mineral
  streaks, cave silt, pale small fungus clusters.
- **Lore**: lightless, never touched by the sun; faint violet mineral veins (not glowing).
- **Avoid**: big mushrooms, glow, lava.

**`TERRAIN-subterranean`** (subject):

```text
Subterranean: a damp dark cavern floor, grey-black rock with smooth flowstone and worn stalagmite stumps, fine cave silt in the
hollows with a wet sheen, pale mineral streaks and faint dark-violet veins, small clusters of pale cave fungus, cold and lightless.
```

## Sand (`sand`)

Dunes and sandy flats. Today a rocky trail.

- **Look**: fine wind-rippled sand in pale ochre-grey, soft ripples (curved, irregular), coarse grains and small shells and pebbles
  sorted into the troughs, a few dry grass blades.
- **Lore**: the sand of a sea that dried in an earlier age: tiny shell bits, a sun-bleached splinter of wood, a dark streak of ash.
- **Avoid**: bright yellow, perfectly parallel ripples, a beach (beach's).

**`TERRAIN-sand`** (subject):

```text
Sand: fine wind-rippled desert sand in pale ochre-grey, soft irregular curved ripples, coarse grains, tiny shell fragments and small
pebbles collected in the troughs, a few dry grass blades, a bleached wood splinter, faint dark streaks of wind-blown ash.
```

## Beach (`beach`)

The strip between land and water.

- **Look**: damp grey-brown sand and shingle, a band of wet darker sand (no shore line in the texture: the blend makes it), seaweed
  wrack, shell bits, driftwood twigs, rounded pebbles.
- **Lore**: wrack and driftwood with the odd blackened plank from a burned boat.
- **Avoid**: a tropical white beach, foam lines, a straight tide line.

**`TERRAIN-beach`** (subject):

```text
Beach: a cold northern shore, damp grey-brown sand mixed with rounded shingle and pebbles, patches of darker wet sand, dark tangles
of dried seaweed wrack, broken shells, driftwood twigs, a rare blackened plank fragment, muted and grey.
```

## Swamp (`swamp`)

The drowned lowlands; Swamp Bestiary's home. Today a coastal sand.

- **Look**: black-brown mud with standing water in shallow puddles (wet sheen, low roughness), mats of algae and duckweed, reeds and
  sedge bent over, rotting wood, bubbles of marsh gas.
- **Lore**: an algae-green fragment of worked stone from the sunken city, once.
- **Avoid**: bright green slime, clear blue water.

**`TERRAIN-swamp`** (subject):

```text
Swamp: a drowned marsh, black-brown sucking mud with shallow puddles of dark water, mats of algae and duckweed, bent reeds and sedge,
rotting waterlogged wood, small marsh-gas bubbles, an algae-green fragment of old worked stone half sunk in the mud.
```

## Water (`water`)

Deep water: impassable to walkers, crossed by boats. Today river pebbles tinted blue.

- **Look**: the **water's depth**, not its surface: a dark blue-green to near-black field with soft depth variation and faint
  suspended silt; no waves, no reflections (the PBR pass will add a surface).
- **Avoid**: a visible bottom of pebbles, foam, sky reflections, bright cyan.

**`TERRAIN-water`** (subject):

```text
Water: deep dark water seen from above, a near-black blue-green depth with soft variation in tone, faint drifting silt and murk,
calm, no waves, no reflections, no visible bottom.
```

## Shallow water (`shallow_water`)

Fords and shoals: walkable water. Today water's texture tinted lighter.

- **Look**: a visible bottom of silt, pebbles and water plants under a thin brown-green water layer; ripple caustics only very faint.
- **Avoid**: a tropical lagoon, clear turquoise.

**`TERRAIN-shallow_water`** (subject):

```text
Shallow water: a shallow ford seen from above, a visible bottom of brown silt, rounded pebbles and waving water plants under a thin
layer of brown-green water, faint soft ripples, murky and cold.
```

## Rock (`rock`)

The impassable cliffs: the walkable ground's highest rim and the shader's **cliff sides** (triplanar), so it is seen from above and
from the side.

- **Look**: dark grey bedrock in fractured blocks and ledges, strata lines in irregular waves, lichen and moss in the cracks, scree in
  the hollows. Must work from the side as well as from above (no top-down-only features such as puddles).
- **Avoid**: a pebble field, round boulders, a man-made wall.

**`TERRAIN-rock`** (subject):

```text
Rock: dark grey fractured mountain bedrock, irregular blocks and ledges, wavy strata lines, crusts of lichen and dark moss in the
cracks, loose scree in the hollows, reads as a cliff face from any side, heavy and ancient.
```

## Rocky ground (`rocky_ground`)

Walkable bare rock; Dragon Utopia's ground. Today rough's texture greyed.

- **Look**: wide worn bedrock slabs, smoothed and pitted by ages of wind, grit in the joints, sparse hardy tufts; warm sandstone and
  granite tones, unlike rough's broken stones.
- **Lore**: older than the Wheel: **deep claw scratches** in a few slabs, a scorched patch, a glint of old bronze in a crack (rare).
- **Avoid**: a cliff, loose scree.

**`TERRAIN-rocky_ground`** (subject):

```text
Rocky ground: ancient worn bedrock plateau, wide smooth slabs of weathered sandstone and granite pitted by wind, grit and dust in the
joints, sparse hardy grass tufts, a few deep old claw scratches across the stone, a small scorched dark patch, a rare glint of old
bronze in a crack.
```

## Battlefield variants

The battlefield uses the terrain the battle is fought on (`BattleState.Terrain`; a map object can set it). Use the same terrain prompt
with `TERRAIN-BATTLE` instead of `TERRAIN-MAP`, and `TERRAIN-BATTLE-CLOSE` after the terrain's own block (see
[Compose a prompt](#compose-a-prompt)).

Points to keep in mind:

- **The cell grid and the units read on top**: no high-contrast speckle at cell size (0.5 m), no lines; the middle scale calm.
- **One texture fills the whole field** (12 units deep, plus the 6-unit band): variation must come from small clusters, not from a
  single large feature; the battlefield has no splat mixing or patch noise.
- **Water and deep lava** are not battle grounds in the rules' sense of standing on them; when a battle is fought "on water" (a boat
  object), use shallow water's battlefield texture *(proposal)*.
- **Obstacles** are separate meshes (stones, trees, craters, cliffs); keep them out of the ground texture.

## After generating

1. Check the 3 × 3 repeat for seams and repeating features; regenerate rather than paint over seams.
2. Check the value range (about 40–200 of 255) and that the light parts match the raised parts of the height map.
3. Put the file in `src/Godot/EternalWars.Godot.Assets/textures/terrain/` under the name above, add its line to `CREDITS.md`, and run
   the `game-weeks` and `game-battle-3d` smoke scenarios to see it on the map and the battlefield.
4. For a terrain that had a borrowed texture (shallow water, rocky ground, cursed lands, ice), remove it from
   `AdventureMapView.TextureOf` and its `Tint` so its own texture shows untinted.
