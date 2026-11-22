# Puzzle map prompts (for an image-generating model)

Prompts for the **puzzle map's faction pictures** (ADR-140): the picture that covers a player's puzzle map until obelisks open its
pieces. Each playable faction has its own; the window shows the player's faction's picture. The puzzle is cut into **10 × 10 pieces**;
a visit to an obelisk opens a share of them (the outer ring first, the centre last), showing the map beneath. A faction without a
picture keeps plain parchment.

## Use a prompt

Each faction below has its own block, `PUZZLE-<town>`. A prompt is the style block and the faction's block (or the frame's), each
copied exactly and joined with a space:

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

- **Picture**: square, at least 1024 × 1024 px (the pictures in the game are 1254), PNG (opaque). The window scales it to the puzzle
  (480 px) and shows it on the closed pieces, each a tenth of the picture across over its 2 × 2 map cells.
- **Detail everywhere**: a single piece, a tenth of the picture across, must show something (a leaf, a stone, a fold of cloth), never
  an empty patch; a player sees the picture break up from the edges inward.
- **The centre last**: the middle 2 × 2 pieces close over the Grail and open last, so the faction's chief figure stands there and stays
  longest. The red cross of the Grail's spot is drawn by the game.
- **Frame**: `puzzle_frame.png`, the same size as the pictures, its wood and torn parchment edge about 12 % of the side on each edge;
  the window draws it nine-sliced around the puzzle, the border 48 px on screen.
- **Names**: `puzzle_<town>.png` (`puzzle_haven.png`, `puzzle_swamp-bestiary.png`, ...).

## Haven (`puzzle_haven`)

- **Centre**: a gold sun on a white war banner, held high.
- **Avoid**: a golden paradise; a white fairy-tale castle; clean gold.

**`PUZZLE-haven`** (subject):

```text
Haven: a white war banner with a gold sun held high at the centre, around it a lakeside town below snowy mountains, a grey stone
castle on a rocky rise with round towers and dark conical slate roofs, a stone church with a bell tower, a mage tower crowned by a
blue orb in a ring, a thatched tavern and a smithy with a glowing forge, pikemen and crossbowmen drilling at an archery range, knights
riding out of the gate, griffins circling the towers, a sailing boat at a jetty, pine forest and a pale blue sky with soft cloud.
```

## Sylvan (`puzzle_sylvan`)

- **Centre**: the great half-bare elder tree.
- **Avoid**: a lush healthy forest; candy colours; mushrooms.

**`PUZZLE-sylvan`** (subject):

```text
Sylvan: a vast ancient tree at the centre, its crown half green and half bare and propped with timber braces, around it a misty forest
gorge with waterfalls, colossal trees with elven halls and tall tree towers in their branches, rope and plank bridges across the
chasm, archers on galleries, a white unicorn by a ring of carved standing stones, ribbons and carved tablets tied to the roots, grey
rot creeping up the trunks, blue-teal mist, warm amber windows and pale green lanterns.
```

## Academy (`puzzle_academy`)

- **Centre**: the bronze orrery of nested rings.
- **Avoid**: a desert palace; floating islands.

**`PUZZLE-academy`** (subject):

```text
Academy: a giant bronze orrery of nested rings at the centre on an observatory tower, around it a snowy mountain valley by a frozen
lake, a castle of blue-grey granite with verdigris domes and blue banners with a gold star, a white columned temple on a peak above
the clouds with a titan at its steps, a domed pavilion with a golden lamp and a genie rising from it in blue smoke, mages in
fur-collared robes reading star charts, chained stone golems at a gate, a half-built golem under a crane, snow on every ledge,
pale-blue rune light.
```

## Necropolis (`puzzle_necropolis`)

- **Centre**: a skeletal hand gripping the rim of a great spoked wheel.
- **Avoid**: cartoon skulls; Halloween colours; green glow.

**`PUZZLE-necropolis`** (subject):

```text
Necropolis: a great skeletal hand gripping the rim of a spoked stone wheel at the centre, around it a grey dead plain under heavy dark
cloud, a dark castle keep with a skull over its gate, a tall straight black obelisk spire with a cold cyan light at its tip, gothic
manors and a bell tower, a graveyard behind an iron fence, ranks of skeleton soldiers, a bone dragon lying across broken ruins, liches
in grey robes, gaunt dead trees, cyan fire in braziers, everything in black and ash grey.
```

## Inferno (`puzzle_inferno`)

- **Centre**: a harvest sickle whose blade is a flame.
- **Avoid**: pitchforks; flames on every surface; a uniformly red picture.

**`PUZZLE-inferno`** (subject):

```text
Inferno: a great harvest sickle with a blade of orange fire at the centre, around it a field of black volcanic ash and obsidian, a
citadel with curved sickle-shaped spires, a stepped altar-furnace belching flame and black smoke, a foundry with three chimneys,
birthing pools of molten rock, thin rivers of lava glowing red, horned demons and hell hounds marching, imps flying through the smoke,
cages hanging from chains, a smoke-black sky lit red from below.
```

## Dungeon (`puzzle_dungeon`)

- **Centre**: a spider whose body is a cut violet gem.
- **Avoid**: sky; water; green goblin caves; red lava.

**`PUZZLE-dungeon`** (subject):

```text
Dungeon: a great black spider whose body is a cut violet gem at the centre, around it a vast enclosed cavern under hanging
stalactites, a gothic black citadel of many spires with violet-lit windows, a tall spire of glowing violet crystal, a labyrinth's
walls with a minotaur at its gate, a lair carved as a giant beast's maw, dark elves in silver-inlaid armour, clusters of glowing
violet and cyan mushrooms, warm orange braziers, webs between the stalactites.
```

## Stronghold (`puzzle_stronghold`)

- **Centre**: a great horned beast skull on a pole frame.
- **Avoid**: stone keeps; real-world tribal motifs.

**`PUZZLE-stronghold`** (subject):

```text
Stronghold: a huge horned beast skull on a pole frame at the centre, around it ochre badlands of mesas and canyon walls with broken
ancient arches, an orc war camp of hide tents and yurts behind a log palisade with watchtowers, great war drums on a platform under a
red banner, a dirt fighting ring of stakes, a forge glowing in a cave, orcs and goblins on wolves, a cyclops carrying a boulder, a roc
on a tall nest tower, carts ready to move, red-ochre painted hides, dust in the low warm sun.
```

## Swamp Bestiary (`puzzle_swamp-bestiary`)

- **Centre**: a coiled serpent with three heads rising from the coil.
- **Avoid**: a jungle temple of a real culture; tropical colours; a wide lake.

**`PUZZLE-swamp-bestiary`** (subject):

```text
Swamp Bestiary: a coiled three-headed serpent rising from black water at the centre, around it half-sunken ruins of an ancient stone
city in a deep dark pine forest, a towering totem of coiled serpents and toad-like beasts, a rampart of giant spiral shells and tusks,
reed-thatched stilt huts on mossy islands, spawning pools of eggs glowing cyan, lizardfolk with spears, a basilisk on a mossy stone, a
wyvern on a broken tower's nest, a toppled statue head in the ferns, dark green shadow and warm gold light through the pines.
```

## After generating

1. Cut the picture into 10 × 10 pieces and look at a few single pieces at the window's size: none is empty.
2. Take away the outer ring (it opens first), then the mid ring: the faction is still recognised from the centre that is left.
3. Put the eight pictures side by side at thumbnail size: each faction is named at a glance.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/puzzle/` and add a line to its `CREDITS.md` with the tool and
   its licence terms.
