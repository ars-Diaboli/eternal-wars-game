# Road and river prompts (for an image- or 3D-generating model)

Prompts for the **roads** (dirt, gravel, cobblestone) and **rivers** (clear, icy, muddy, lava) of the adventure map, and the **bridges**
where a road crosses a river. Today the game draws both as flat coloured plates on each tile; these textures replace the colour with
painted strips laid along the path *(proposal)*. Each road and river reads at a glance from the map's camera, and sits on any terrain
([terrain-texture-prompts.md](terrain-texture-prompts.md)) without fighting it.

## Compose a prompt

Each road and river below has its own block. Build the prompt from blocks, copying each block's text exactly and joining them with a
space:

```text
PROMPT   = PATH-STYLE + ROAD-<id>      (roads)
         = PATH-STYLE + RIVER-<id>     (rivers)
         = BRIDGE-STYLE + BRIDGE-<id>  (bridges, a 3D model)
NEGATIVE = PATH-NEG or BRIDGE-NEG   (only where the tool takes a negative prompt)
OUTPUT   = the files under "Output", named as there
```

The **Signature** and **Avoid** notes explain the block and are for checking the result; they are not pasted. Do not paraphrase the
blocks.

## Blocks

**`PATH-STYLE`** (style, first in every road and river prompt):

```text
Seamless strip texture for a strategy game map, perfectly top-down orthographic view, evenly lit with flat ambient light, no shadows,
no perspective. A single path running straight from the top edge to the bottom edge of a tall picture, tileable from top to bottom,
centred, its two long edges irregular and natural, fading into full transparency at the sides. Covers about 2 metres across and 4 metres
along. Rich clear colour, crisp detail, albedo without lighting.
```

**`PATH-NEG`** (negative prompt, every road and river):

```text
perspective, horizon, curve, bend, fork, crossing, straight ruler edges, visible seam at the top or bottom, cast shadows, sun glare,
grass verge, surrounding terrain, people, carts, animals, footprints in a line, text, logos, watermark, modern asphalt, tyre tracks, road
markings, pixel art, blurry, dull washed-out colour
```

**`BRIDGE-STYLE`** (style, first in every bridge prompt):

```text
Game-ready 3D model of a short bridge for a turn-based strategy game, seen from above at a steep angle, grim dark fantasy, realistic
PBR materials, weathered and worn. Spans a narrow river of about 1.6 metres between two banks, about 2 metres long and 1 metre wide,
straight, flat deck, the two ends level with the ground. Chunky readable shapes, flat bottom ends, no ground, no water, no characters.
```

**`BRIDGE-NEG`** (negative prompt, every bridge):

```text
modern bridge, steel girders, suspension cables, railings with fine detail, tall arches, towers, water, terrain, people, cartoon, toy,
low-poly flat shading, thin spindly parts
```

## Output

- **Strips**: 512 × 1024 px (width across the path, length along it), PNG with alpha: albedo (sRGB) with the edges' transparency in its
  alpha, normal (OpenGL, Y+), roughness. Seamless from top to bottom; check with three copies stacked.
- **Width inside the strip**: a road's solid part fills about the middle **45 %** of the width, a river's water about the middle
  **80 %**, as wide as today's plates; the rest is the soft, fading edge (a road's trodden shoulder, a river's wet bank).
- **Rivers** also get a **flow map**: a second normal map of small ripples, tileable top to bottom, that the game scrolls along the
  river *(proposal)*; the lava river gets an emissive map of its glowing parts.
- **Bridges**: glTF binary, 1500–4000 triangles, one 1024 px texture set (albedo, normal, ORM); origin at the deck's centre at ground
  level, the deck along the X axis; Y up.
- **Names**: `road_<id>.png`, `road_<id>_normal.png`, `road_<id>_roughness.png`; `river_<id>.png` and its maps, `river_<id>_flow.png`,
  `river_lava_emissive.png`; `bridge_<id>.glb` (one per road type).
- **Colour**: clearly coloured and a little brighter than the terrains, so a road or river reads on any ground: the game darkens it with
  its light and grading.

## Roads

Roads are faster the better they are built (dirt, gravel, cobblestone); the player should see the difference at a glance.

### Dirt road (`road_dirt`)

- **Signature**: a pale packed earth track with two worn wheel ruts.
- **Avoid**: mud and puddles (the dirt terrain's), stones, a dark colour that vanishes on dirt ground.

**`ROAD-dirt`** (subject):

```text
Dirt road: a pale sandy-brown packed earth track, trodden hard and dry, two shallow worn wheel ruts running its whole length, a strip of
sparse short grass between the ruts, small dust-coloured pebbles, the edges frayed into the ground.
```

### Gravel road (`road_gravel`)

- **Signature**: light grey crushed stone, raked and even.
- **Avoid**: big stones, cobbles, a dark wet look.

**`ROAD-gravel`** (subject):

```text
Gravel road: a light grey and warm white crushed-stone road, small angular gravel packed even, slightly raised in the middle, a thin
line of larger stones along each edge, a few bare patches, clean and bright.
```

### Cobblestone road (`road_cobblestone`)

- **Signature**: rounded grey-blue setts in curved rows, with a kerb.
- **Avoid**: a modern brick pattern, square tiles, a dirt surface.

**`ROAD-cobblestone`** (subject):

```text
Cobblestone road: an old paved road of rounded blue-grey and warm grey stone setts laid in gently curved rows, worn smooth in the
middle, moss and dark soil in the joints, a kerb of larger long stones along each edge, a few stones missing.
```

## Rivers

A river crosses many terrains; its banks stay narrow and neutral so the ground around it shows.

### Clear river (`river_clear`)

- **Signature**: clear blue-green water over a pebble bed.
- **Avoid**: deep blue (the water terrain's), foam everywhere, a muddy colour.

**`RIVER-clear`** (subject):

```text
Clear river: a narrow fresh stream of clear blue-green water, a bed of rounded golden and grey pebbles visible through it, a few small
white ripples over stones, narrow banks of wet dark earth and pebbles.
```

### Icy river (`river_icy`)

- **Signature**: pale cyan water half covered by cracked white ice.
- **Avoid**: plain white snow, a frozen solid sheet with no water showing.

**`RIVER-icy`** (subject):

```text
Icy river: a narrow cold stream of dark cyan water, plates of cracked white and pale blue ice along both banks and drifting floes in the
middle, frost on the bank stones, snow dust on the ice.
```

### Muddy river (`river_muddy`)

- **Signature**: opaque ochre-brown water with reed banks.
- **Avoid**: black swamp water, clear water, grass on the water.

**`RIVER-muddy`** (subject):

```text
Muddy river: a narrow slow stream of opaque ochre-brown water swirling with silt, banks of soft wet mud with a few reed clumps, small
floating twigs and leaves.
```

### Lava river (`river_lava`)

- **Signature**: molten orange-yellow lava between black crusted banks.
- **Avoid**: flames above the surface, smoke, a lava lake.

**`RIVER-lava`** (subject):

```text
Lava river: a narrow channel of molten orange and bright yellow lava with darker cooling crusts drifting on it, banks of black ropy
basalt crust with glowing red cracks, scattered cinders.
```

## Bridges

A road over a river is a bridge (the rules ignore the river there); one bridge model per road type, placed by the game on each road tile
that crosses a river *(proposal)*.

**`BRIDGE-dirt`** (subject):

```text
Dirt road bridge: a rough plank bridge of split grey timber on two log beams, the planks uneven and lashed with rope, two short leaning
posts at each end.
```

**`BRIDGE-gravel`** (subject):

```text
Gravel road bridge: a sturdy timber bridge with a squared plank deck, a low plank rail on each side, iron-strapped posts, gravel
spilled at both ends.
```

**`BRIDGE-cobblestone`** (subject):

```text
Cobblestone road bridge: a low single-arch stone bridge of grey weathered blocks, a cobbled deck, a low stone parapet on each side,
moss in the joints.
```

## After generating

1. Stack three copies of each strip: no seam, no feature that repeats as a pattern.
2. Lay each road and each river over the terrains it may cross at the map's zoom: it reads on all of them, and the three roads are
   told apart at a glance.
3. Check that the solid part of a road fills about the middle 45 % and a river's water about the middle 80 % of the width.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/paths/` and the bridges in `models/bridges/` *(proposal)* and add a
   line to each folder's `CREDITS.md` with the tool and its licence terms.
