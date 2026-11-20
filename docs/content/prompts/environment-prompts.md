# Environment prompts (for a 3D- or material-generating model)

Prompts for the **static environment**: the adventure map's scenery (rocks, trees, ponds, ruins, ... from `scenery.csv`), the
battlefield's obstacles, small scattered debris, and the shared materials they are textured with. For a text-to-3D or image-to-3D model
(or a concept-art pass first), and a text-to-material model for the materials. The designs are original.

## Compose a prompt

Each piece below has its own block. Build the prompt from blocks, copying each block's text exactly and joining them with a space:

```text
PROMPT   = ENV-STYLE + SCENERY-<kind> + SIZE-<size> + LOOK-<family>   (map scenery)
         = ENV-STYLE + OBSTACLE-<kind> + LOOK-<family>                (battle obstacles)
         = ENV-STYLE + SCATTER-<id>                                   (debris)
         = MAT-STYLE + MAT-<id>                                       (materials)
NEGATIVE = ENV-NEG or MAT-NEG   (only where the tool takes a negative prompt)
OUTPUT   = the file named under "Output", to its footprint and budget
```

**`LOOK-<family>`** says which ground the piece stands on: the same rock is grey on highlands and black basalt on lava, so each kind is
made once per family it appears in (the **Families** column). The **Avoid** notes are for checking the result; they are not pasted. Do
not paraphrase the blocks.

## Blocks

**`ENV-STYLE`** (style, first in every model prompt):

```text
Game-ready 3D model of a natural or ruined map feature for a turn-based strategy game, seen from above at a steep angle, grim dark
fantasy, realistic PBR materials, weathered and worn, rich natural colour. Chunky readable shapes and a clear outline from above, flat
bottom sitting on the ground, no ground plane, no base slab, no characters, no lights.
```

**`ENV-NEG`** (negative prompt, every model):

```text
ground plane, terrain skirt, base slab, people, animals, text, logos, modern objects, cartoon, toy, low-poly flat shading, thin spindly
parts, floating parts, perfectly symmetric, another game's design
```

**`MAT-STYLE`** (style, first in every material prompt):

```text
Seamless tileable PBR material seen straight on, evenly lit with flat ambient light, no shadows, no perspective, rich natural colour,
crisp detail, covering about 2 by 2 metres of surface. Albedo without lighting, with matching normal, roughness, ambient occlusion and
height maps.
```

**`MAT-NEG`** (negative prompt, every material):

```text
perspective, horizon, cast shadows, visible seams, one large standout feature, text, logos, watermark, blurry, dull washed-out colour
```

**`SIZE-<size>`** (after the kind on scenery; sizes in map tiles, 1 tile = 1 Godot unit, about 2 m):

| Block | Text |
|---|---|
| `SIZE-1` | `small, fits one 1 by 1 tile square, about 1 unit wide and up to 1 unit tall.` |
| `SIZE-2` | `medium, fills a 2 by 1 tile strip, about 2 units long, 1 unit wide and up to 1.2 units tall.` |
| `SIZE-4` | `large, fills a 2 by 2 tile square, about 2 units wide and up to 1.6 units tall.` |
| `SIZE-6` | `very large, fills a 3 by 2 tile area, about 3 units long, 2 wide and up to 2 units tall.` |
| `SIZE-9` | `huge, fills a 3 by 3 tile square, about 3 units wide and up to 2.5 units tall.` |

**`LOOK-<family>`** (last on scenery and obstacles):

| Block | Grounds | Text |
|---|---|---|
| `LOOK-green` | grass, dirt, forest floor, highlands | `Temperate lowland look: grey stone with green moss and yellow lichen, green and gold leaves, dark bark.` |
| `LOOK-cold` | snow, ice | `Frozen look: blue-grey stone, snow lying on every upper surface, icicles, dark pine needles, frost.` |
| `LOOK-dry` | rough, wasteland, sand, beach | `Dry badlands look: warm ochre and tan stone, orange lichen, sun-bleached wood, dust.` |
| `LOOK-wet` | swamp, shallow water | `Drowned marsh look: dark wet stone with algae, black water stains, olive moss, hanging weed.` |
| `LOOK-dead` | cursed lands | `Cursed look: violet-black ashen stone and wood, pale lilac ash, sickly pale-green mould, bone splinters.` |
| `LOOK-fire` | lava | `Volcanic look: black basalt with glowing ember-orange cracks, cinders, yellow sulphur crust, charred wood.` |
| `LOOK-deep` | subterranean | `Cavern look: wet blue-grey slate and flowstone, violet mineral veins, pale cave fungus.` |
| `LOOK-rock` | rocky ground | `Ancient plateau look: terracotta and salmon-pink sandstone worn by wind, old claw scratches.` |

## Output

- **Footprint**: a piece fills the tiles its scenery row blocks (the **Footprint** column, from `scenery.csv`: `B` blocked, `.` free,
  `/` a new row), with the origin at the centre of that area on the ground, Y up, and stays inside it; a free corner stays free.
- **Height**: low, so heroes and objects behind it stay visible: at most the height in its `SIZE` block.
- **Readability**: the camera looks down 15–30° from vertical, so the **top-down outline** carries the piece; one piece reads as one
  shape at 30 px a tile.
- **Model**: glTF binary, PBR metal-roughness, **500–3000 triangles** for 1 tile, up to 8000 for 9 tiles *(proposal)*; one 1024 px
  texture set (albedo, normal, ORM), or the shared materials below. No animation (the game sways trees and ripples water itself).
- **Names**: `scenery_<kind>_<size>_<family>.glb` (`scenery_rock_small_green.glb`, `scenery_tree_grove_cold.glb`), with `<kind>` and
  `<size>` from the scenery id (`scenery.rock.small` → `rock_small`); `obstacle_<kind>_<family>.glb`; `scatter_<id>.glb`;
  `mat_<id>.png` and its maps (`mat_<id>_normal.png`, `..._roughness.png`, `..._ao.png`, `..._height.png`).
- **Colour**: as the terrains, rich in the model; the game's light and grading make the mood. A piece must stand apart from the ground
  it sits on (darker or lighter, never the same tone).

## Map scenery

Every scenery cell blocks the map. **Compose**: `ENV-STYLE` + `SCENERY-<kind>` + the row's `SIZE` + `LOOK-<family>` · negative
`ENV-NEG` · one model per row and family.

| Scenery ids | Footprint | Size | Families | Block |
|---|---|---|---|---|
| `scenery.rock.small` | `B` | `SIZE-1` | every family | `SCENERY-rock` |
| `scenery.rock.medium` | `BB` | `SIZE-2` | green, cold, dry, fire, deep, rock | `SCENERY-rock` |
| `scenery.rock.large` | `BB/BB` | `SIZE-4` | green, cold, dry, fire, deep, rock | `SCENERY-rock` |
| `scenery.rock.huge` | `BBB/BBB/.B.` | `SIZE-9` | green, dry, fire, deep, rock | `SCENERY-crag` |
| `scenery.tree.small` | `B` | `SIZE-1` | green, cold, dry, wet | `SCENERY-tree` |
| `scenery.tree.pair` | `BB` | `SIZE-2` | green, cold, wet | `SCENERY-trees` |
| `scenery.tree.grove` | `BB/BB` | `SIZE-4` | green, cold | `SCENERY-trees` |
| `scenery.tree.wood` | `BBB/BB.` | `SIZE-6` | green | `SCENERY-trees` |
| `scenery.dead-tree.small`, `.pair`, `.stand` | `B`, `B/B`, `BB/BB` | `SIZE-1`, `SIZE-2`, `SIZE-4` | dry, wet, dead, cold | `SCENERY-dead-tree` |
| `scenery.pond.small`, `.medium`, `.large` | `BB`, `BB/BB`, `BBB/BBB` | `SIZE-2`, `SIZE-4`, `SIZE-6` | green, wet, cold | `SCENERY-pond` |
| `scenery.bush.small`, `.row` | `B`, `BB` | `SIZE-1`, `SIZE-2` | green, dry | `SCENERY-bush` |
| `scenery.mushrooms.small`, `.ring` | `B`, `BB/BB` | `SIZE-1`, `SIZE-4` | green, wet, deep | `SCENERY-mushrooms` |
| `scenery.flowers.small`, `.bed` | `B`, `BB` | `SIZE-1`, `SIZE-2` | green | `SCENERY-flowers` |
| `scenery.fissure.short`, `.long`, `.down` | `BB`, `BBB`, `B./.B` | `SIZE-2`, `SIZE-6`, `SIZE-4` | dry, fire, dead, deep, rock | `SCENERY-fissure` |
| `scenery.mound.small`, `.large` | `B`, `BB/BB` | `SIZE-1`, `SIZE-4` | green, dry, cold | `SCENERY-mound` |
| `scenery.ruins.small`, `.medium`, `.large` | `BB/BB`, `BBB/BBB`, `BBB/BBB/BBB` | `SIZE-4`, `SIZE-6`, `SIZE-9` | green, dry, dead, deep | `SCENERY-ruins` (+ hut, house or keep) |
| `scenery.bones.small`, `.large` | `B`, `BB` | `SIZE-1`, `SIZE-2` | dry, dead, fire, deep, wet | `SCENERY-bones` |
| `scenery.wall.*` | 1 × 2–4 tiles, straight or diagonal | one 1-tile piece | green, cold, dry, deep, rock | `SCENERY-wall` |

The castle walls are built from **one modular piece a family** (`scenery_wall_<family>.glb`, 1 unit long, ends that join) and a corner
post; the game lines them up for every length and the diagonals *(proposal)*.

**`SCENERY-rock`**: `A cluster of weathered boulders of different sizes, cracked and rounded by age, the largest leaning on the others.`

**`SCENERY-crag`**: `A jagged crag of fractured bedrock rising in stepped ledges, scree at its foot, one sharp peak toward the back.`

**`SCENERY-tree`**: `A single old tree with a thick gnarled trunk and a broad rounded crown of dense foliage in clumps, roots gripping the ground.`

**`SCENERY-trees`**: `A close group of old trees of different heights with dense clumped crowns merging into one canopy, trunks and roots at the edge.`

**`SCENERY-dead-tree`**: `A dead tree with a cracked bare trunk and a few twisted broken branches reaching up, bark peeling, a hollow at its base.`

**`SCENERY-pond`**: `A small natural pond sunk into the ground, a dark still water surface with a soft edge of wet stones, reeds and mud around it.`

**`SCENERY-bush`**: `A dense low thicket of rounded shrubs with small leaves and a few thorny branches poking out.`

**`SCENERY-mushrooms`**: `A cluster of oversized mushrooms of different heights with broad pale caps and thick stems, small ones around the big ones.`

**`SCENERY-flowers`**: `A low dense patch of wildflowers in white, yellow and violet among green leaves, a few taller stems.`

**`SCENERY-fissure`**: `A deep jagged crack split into the ground, broken raised edges and loose rubble along it, darkness inside.`

**`SCENERY-mound`**: `A low rounded mound of earth and stones, grassed or bare as the ground, a few stones poking out of it.`

**`SCENERY-ruins`**: `The ruin of an old stone building of an earlier age, broken walls standing at different heights, a collapsed roof, fallen blocks and rubble inside, weeds in the cracks.` Add `a small hut` (small), `a two-room house` (medium) or `a square keep with one broken tower` (large).

**`SCENERY-bones`**: `The bleached bones of a huge long-dead beast half sunk into the ground, curved ribs rising like arches, a skull at one end.` (small: `a scattering of large bones and a horned skull`).

**`SCENERY-wall`**: `A straight 1-unit section of an old ruined castle wall of fitted stone blocks, battlements broken here and there, about 1 unit tall, both ends cut clean to join the next section.`

**Avoid** on every piece: a scene (several kinds at once), anything taller than its size, a piece wider than its footprint.

## Battle obstacles

The battlefield places **small** (2 × 2 cells, 1 × 1 unit, 1/8 cover) and **big** (3 × 3 cells, 1.5 × 1.5 units, 1/4 cover) obstacles
and **cliffs** (2 cells wide, 6–12 cells long). The camera is much closer than on the map: more detail, and the same family look as the
ground the battle is fought on. **Compose**: `ENV-STYLE` + `OBSTACLE-<kind>` + `LOOK-<family>` · negative `ENV-NEG` · one model per
kind and family.

| Kind | Size | Block |
|---|---|---|
| `small` | 1 × 1 unit, up to 0.8 tall | `OBSTACLE-small`: `A single battlefield obstacle filling a 1 by 1 unit square: a heavy boulder, a thick tree stump or a dense bush as suits the ground, solid and readable from close.` |
| `big` | 1.5 × 1.5 units, up to 1.2 tall | `OBSTACLE-big`: `A single large battlefield obstacle filling a 1.5 by 1.5 unit square: a pile of big rocks, a fallen tree with its roots in the air or a ruined wall corner as suits the ground.` |
| `cliff` | 1 unit long, 1 wide, up to 1.5 tall | `OBSTACLE-cliff`: `One straight modular section of a low rock ridge, 1 unit long and 1 wide, steep rocky sides, both ends cut clean to join the next section.` |
| `crater` | 1.5 × 1.5 units, flat | `OBSTACLE-crater`: `A shallow scorched crater in the ground, a raised broken rim of earth and stones, charred debris inside, very low.` |

## Scatter debris

Small non-blocking props the game may scatter on free tiles and the battlefield's edge for life *(proposal: not placed yet)*. **Compose**:
`ENV-STYLE` + `SCATTER-<id>` · negative `ENV-NEG` · each about 0.3–0.6 units, 200–800 triangles.

| Id | Block text |
|---|---|
| `pebbles` | `A small loose cluster of pebbles and two flat stones.` |
| `tufts` | `A few clumps of tall wild grass and seed heads.` |
| `log` | `A short moss-covered fallen log, split at one end.` |
| `stump` | `A low cut tree stump with axe marks and a few mushrooms.` |
| `cart` | `A broken wooden cart lying on its side, one wheel off.` |
| `barrels` | `Two old wooden barrels and a broken crate.` |
| `weapons` | `A rusted sword stuck in the ground beside a dented shield.` |
| `skull` | `A large horned animal skull half sunk in the ground.` |
| `stones` | `Three weathered standing stones in a short row, one fallen.` |
| `signpost` | `A leaning wooden signpost with two blank boards.` |

## Materials

Shared tileable materials for the models, so pieces of one family match. **Compose**: `MAT-STYLE` + `MAT-<id>` · negative `MAT-NEG` ·
2048 px.

| Id | Block text |
|---|---|
| `bark-oak` | `Bark: thick deeply furrowed dark grey-brown oak bark, green moss in the furrows.` |
| `bark-pine` | `Bark: reddish-brown pine bark in flaking plates, resin streaks.` |
| `bark-dead` | `Bark: bare silver-grey dead wood, cracked and split along the grain, a few peeling strips of dark bark.` |
| `foliage` | `Foliage: a dense mass of small green leaves in clumps with lighter sunlit tips, for a tree crown seen from above, with an alpha mask at the leaf edges.` |
| `rock-grey` | `Rock: weathered grey granite, rough and fractured, green moss and yellow lichen patches.` |
| `rock-ochre` | `Rock: warm ochre and tan sandstone, layered, orange lichen crusts.` |
| `rock-basalt` | `Rock: black basalt with a ropy surface, fine red-glowing cracks (also as an emissive map).` |
| `rock-slate` | `Rock: wet blue-grey slate with violet mineral veins and a slight sheen.` |
| `masonry` | `Masonry: old fitted grey stone blocks with worn edges, dark mortar joints, moss and stains running down.` |
| `bone` | `Bone: bleached weathered bone, fine cracks, yellowed in the hollows.` |
| `ice-snow` | `Snow cover: clean packed snow with blue shade, to lie on top surfaces.` |

## After generating

1. Look at each piece from above at the map's angle at 30 px a tile: one clear shape, readable against its family's grounds.
2. Check the footprint, the height and the triangle budget; check that a model of one family sits naturally on all its grounds.
3. Put the scenery of one kind side by side across families: the same shape, each in its family's look.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/models/scenery/`, `models/obstacles/` and `textures/materials/` *(proposal)* and
   add a line to each folder's `CREDITS.md` with the tool and its licence terms.
