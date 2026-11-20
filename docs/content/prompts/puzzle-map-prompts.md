# Puzzle map prompts (for an image-generating model)

Prompts for the **puzzle map's faction pictures** (ADR-140): the picture that covers a player's puzzle map until obelisks open its
pieces. Each playable faction has its own; the window shows the player's faction's picture. The puzzle is cut into **10 × 10 pieces**;
a visit to an obelisk opens a share of them (the outer ring first, the centre last), showing the map beneath. Today closed pieces are
plain parchment.

## Compose a prompt

Each faction below has its own block, `PUZZLE-<town>`. Build the prompt from blocks, copying each block's text exactly and joining them
with a space:

```text
PROMPT   = PUZZLE-STYLE + PUZZLE-<town>
         = PUZZLE-STYLE + PUZZLE-FRAME   (the window's frame, once)
NEGATIVE = PUZZLE-NEG   (only where the tool takes a negative prompt)
OUTPUT   = puzzle_<town>.png, as under "Output"
```

The **Centre** and **Avoid** notes explain the block and are for checking the result; they are not pasted. Do not paraphrase the
blocks.

## Blocks

**`PUZZLE-STYLE`** (style, first in every prompt):

```text
Square hand-painted dark-fantasy picture, a rich detailed painting in the manner of an old illuminated map or a painted tapestry, warm
aged colours, soft painted light, visible brush texture. The whole square is filled edge to edge with detail evenly spread across it,
every part of the picture interesting on its own, the most important figure exactly in the centre. No text, no letters, no frame, no
border, no compass, no grid.
```

**`PUZZLE-NEG`** (negative prompt, every picture):

```text
text, letters, numbers, signature, watermark, frame, border, grid lines, empty sky, large plain areas, blank parchment, photo, 3D render,
pixel art, cartoon, modern objects, another game's art
```

**`PUZZLE-FRAME`** (subject, the puzzle window's frame):

```text
An empty square frame of old dark wood with worn bronze corner fittings around a sheet of aged parchment, the parchment plain and even
inside, faint stains and creases, seen exactly from the front, mirror-symmetric, the four corners identical.
```

## Output

- **Picture**: 2048 × 2048 px, square, PNG (opaque). The game cuts it into 10 × 10 pieces of about 205 px and shows each closed piece
  over its 2 × 2 map cells *(proposal: the window draws parchment today)*.
- **Detail everywhere**: a single piece, a tenth of the picture across, must show something (a leaf, a stone, a fold of cloth), never
  an empty patch; a player sees the picture break up from the edges inward.
- **The centre last**: the middle 2 × 2 pieces close over the Grail and open last, so the faction's chief figure stands there and stays
  longest. The red cross of the Grail's spot is drawn by the game.
- **Frame**: `puzzle_frame.png`, 2048 × 2048, nine-slice margins of 160 px *(proposal)*.
- **Names**: `puzzle_<town>.png` (`puzzle_haven.png`, `puzzle_swamp-bestiary.png`, ...).

## Haven (`puzzle_haven`)

- **Centre**: the sun-brazier in its iron cage, burning.
- **Avoid**: a bright golden paradise; a white fairy-tale castle.

**`PUZZLE-haven`** (subject):

```text
Haven: a burning sun-brazier held in a great iron cage at the centre, around it a tiered hill-city of soot-stained limestone and
slate roofs, a procession of hooded zealots with torches, griffins circling the towers, knights in soot-dark plate, a pyre smoking by a
gate, war banners of off-white and faded gold, a dim pale sun low in a grey sky.
```

## Sylvan (`puzzle_sylvan`)

- **Centre**: the great half-bare elder tree.
- **Avoid**: a lush healthy forest; candy colours.

**`PUZZLE-sylvan`** (subject):

```text
Sylvan: a vast ancient tree at the centre, its crown half green and half bare, around it a ring of colossal trees with elven halls in
their branches, archers on galleries, a white unicorn in a moonlit glade, ribbons and carved tablets tied to the roots, grey rot creeping
up the trunks, falling leaves of green and gold, pale green lanterns.
```

## Academy (`puzzle_academy`)

- **Centre**: the bronze orrery of nested rings.
- **Avoid**: a desert palace; floating islands.

**`PUZZLE-academy`** (subject):

```text
Academy: a giant bronze orrery of nested rings at the centre under an observatory dome, around it a frozen mountain citadel of
blue-grey granite and verdigris domes, mages in fur-collared robes reading star charts, stone golems at a gate, a half-built construct
in a workshop, snow on every ledge, pale-blue rune light, cold stars above.
```

## Necropolis (`puzzle_necropolis`)

- **Centre**: a skeletal hand gripping the rim of a great spoked wheel.
- **Avoid**: cartoon skulls; Halloween colours.

**`PUZZLE-necropolis`** (subject):

```text
Necropolis: a great skeletal hand gripping the rim of a spoked stone wheel at the centre, around it a city of stacked ruins from many
ages, ossuary walls of skulls, a crooked black spire with a cold green light, ranks of skeleton soldiers, a bone dragon coiled on a
broken tower, liches in grey robes, ash drifting, a sickly green sky.
```

## Inferno (`puzzle_inferno`)

- **Centre**: a harvest sickle whose blade is a flame.
- **Avoid**: pitchforks; flames on every surface.

**`PUZZLE-inferno`** (subject):

```text
Inferno: a great harvest sickle with a blade of fire at the centre, around it a fortress of black slag and fused rubble of burned
cities, sickle-shaped spires, rivers of glowing lava, horned demons and hell hounds marching, imps flying through smoke, cages hanging
from chains, an altar-furnace venting black smoke, a red sky.
```

## Dungeon (`puzzle_dungeon`)

- **Centre**: a spider whose body is a cut violet gem.
- **Avoid**: green goblin caves; red lava.

**`PUZZLE-dungeon`** (subject):

```text
Dungeon: a great black spider whose body is a cut violet gem at the centre, around it a cavern town of black and violet stepped
pyramids, a labyrinth's walls, dark elves in silver-inlaid armour, a minotaur at a gate, glowing mushrooms and violet crystals, webs
between stalactites, a dark pool reflecting violet light.
```

## Stronghold (`puzzle_stronghold`)

- **Centre**: a great horned beast skull on a pole frame.
- **Avoid**: stone keeps; real-world tribal motifs.

**`PUZZLE-stronghold`** (subject):

```text
Stronghold: a huge horned beast skull on a pole frame at the centre, around it an orc war camp of hide tents and yurts behind a log
palisade, war drums on a platform, a dirt fighting ring, orcs and goblins on wolves, a cyclops carrying a boulder, a roc overhead, carts
ready to move, red-ochre painted hides, dust and ochre badlands.
```

## Swamp Bestiary (`puzzle_swamp-bestiary`)

- **Centre**: a coiled serpent with three heads rising from the coil.
- **Avoid**: a jungle temple of a real culture; tropical colours.

**`PUZZLE-swamp-bestiary`** (subject):

```text
Swamp Bestiary: a coiled three-headed serpent rising from black water at the centre, around it half-sunken ruins of an old stone city,
stilt huts and boardwalks, a rampart of giant shells and bones, lizardfolk in reed boats, a basilisk on a mossy stone, wyverns over the
mist, a toppled statue head sinking in the mud.
```

## After generating

1. Cut the picture into 10 × 10 pieces and look at a few single pieces at the window's size: none is empty.
2. Take away the outer ring (it opens first), then the mid ring: the faction is still recognised from the centre that is left.
3. Put the eight pictures side by side at thumbnail size: each faction is named at a glance.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/puzzle/` *(proposal)* and add a line to its `CREDITS.md` with the
   tool and its licence terms.
