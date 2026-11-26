# Town picture prompts (for an image-generating model)

Prompts for the **town pictures**: the picture that fills an entry of the adventure map's town list (ADR-309, ADR-313), one for each of
the ten town types, walled and as a village without walls (a town without a fort). Generated as **sheets of 4 (2 × 2)**
like the icons ([icon-prompts.md](icon-prompts.md)), so the pictures of a sheet come out matching; each sheet holds two towns, walled
and as villages. The subjects follow the town models ([town-model-prompts.md](town-model-prompts.md)). The designs are original.

## The entry the picture fills

A town list entry is a framed slot **130 × 64 px** (about 2:1) at 100 % UI scale, 260 × 128 at 200 %. The picture fills it edge to
edge, cut to cover like the army strip's creatures; over it the game draws the governor's portrait in the **top-left corner** and the
build mark in the **bottom-right corner**, each a 24 px square 3 px in from the edges (about a fifth of the width and two fifths of the
height). So the picture is:

- **Wide and full-bleed**: a scene painted to all four edges of its cell, the town's own ground and sky behind it; no round patch of
  ground, no fade to transparency, no cut-out on an empty background (the round dioramas of the earlier town icons do not fit the slot).
- **Centred and low**: the town spreads left and right through the middle of the picture, its landmark in the centre against the sky;
  the top-left corner holds only sky, the bottom-right only plain ground.
- **Inside a 2:1 band**: the cell is taller than 2:1 and the cut keeps a 2:1 band near its top (the foreground ground goes), so the
  top eighth is only sky and the bottom eighth only foreground ground.
- **Read at 130 × 64**: two or three big masses (the walls or houses, the landmark, the ground), the landmark lighter or darker than the
  sky behind it, so each town is named at a glance and walled and village tell apart.

## Use a prompt

Copy the sheet's **Prompt** block whole and paste `NEG-TOWNPIC` (below) where the model takes a negative prompt. Generate at the size
given, cut the sheet and save each cell as the table's File (`.png`); the table rows are the cells in order (top row left to right,
then the bottom row). Do not paraphrase a prompt: the wording keeps the sheets consistent with each other.

## Shared wording

The style paragraph ("Game town picture sheet in a realistic…") and the grid sentence ("4 separate pictures arranged in a grid…") are
repeated word for word in each prompt so each is ready to paste. A change to one of them must be made in every prompt: search for its
first words.

## Negative prompt

**`NEG-TOWNPIC`** (every sheet):

```text
text, letters, numbers, labels, frames, borders, ornate borders, grid lines, white gaps, photograph, plastic CGI render, pixel art,
blurry, cropped town, town cut off at the sides, cartoon, comic book, anime, manga, chibi, cute, toy-like, childish, cel shading, flat
colours, flat shading, vector art, clip art, sticker, emoji, mobile game icon, casual game art, thick black outlines, ink line art,
exaggerated proportions, rounded bubbly shapes, glossy plastic, smooth airbrushed gradients, oversaturated colours, candy colours,
round patch of ground, circular vignette, floating island, diorama on a base, cut-out on a transparent background, edges fading to
white or transparent, round or oval picture, tall portrait picture, top-down view, town seen straight from above, close-up of one
building, landmark at the edge, buildings in the corners, people, crowds, banners in colour, toy houses, board-game piece, another
game's town
```

## Output for every picture

- **Sheet**: 2 × 2 at 1536 × 1024 (cells 3:2) or the model's wide size (1672 × 941 gives 16:9 cells), the cells parted by a thin
  plain black gap.
- **Cut**: each cell inside the gap, then a 260:128 band of its full width starting a little below its top (6 px of a 466 px cell:
  the landmark's tip stays, the foreground ground goes), scaled to **260 × 128** (Lanczos). 32-bit RGBA, opaque.
- **File**: `town_<town>.png` (walled) and `town_<town>_village.png`, the town type's id (`TownType`, as in `heroes.csv`'s `town` column)
  (`town_haven.png`, `town_swamp-bestiary_village.png`), in `src/Godot/EternalWars.Godot.Assets/textures/ui/towns/`; the town list
  shows a town without a fort as its village.
- **Light**: from the top left, as every icon; a dusk-to-day sky of muted colours, never night-black, so the frame's dark rim stands out.

## Towns

Each sheet holds two towns: cells 1–2 the towns with their walls (`town_<town>`), cells 3–4 the same towns as villages without walls
(`town_<town>_village`).

### T1 · haven, sylvan

**Prompt** · 1536 × 1024 · negative `NEG-TOWNPIC`

```text
Game town picture sheet in a realistic painterly dark-fantasy style, like a small oil painting: each picture a wide view of a whole
small fantasy town seen from a high three-quarter angle (about 35 degrees down) like a scout on a hill would see it, the town spread
left and right across the middle of the picture with its one tall landmark in the centre standing against the sky, the town's own
ground and sky painted all the way to the four edges of the picture. The top-left corner is open sky only and the bottom-right corner
is plain ground only, nothing important there; the top eighth is sky and the bottom eighth is foreground ground. Believable
architecture and scale (doors, windows, roof tiles, stonework, timber), weathered materials with grime and moss, naturalistic light
from the top left with soft shadows, muted natural colours, a muted day sky, big simple masses with strong value contrast between the
town and the sky so it reads when very small, no drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate
pictures arranged in a grid of 2 columns and 2 rows, each a full rectangle filling its own equal cell edge to edge, the cells parted by
a thin plain black gap, all at the same scale, the same viewing angle and the same light. No borders, no frames, no text, no labels,
no numbers. The pictures, top row left to right, then bottom row: 1 a grey stone castle on a rocky rise in green farmland, round
towers with dark conical slate roofs, a white banner with a gold sun on the keep; 2 a ring of colossal living trees forming the walls
in an old forest, halls in their crowns, one vast half-bare ancient tree in the centre, faint blue-teal mist; 3 the same town as the
one above it, as a small village on the same ground: a few houses around the bare foundation of its landmark, no walls; 4 the same
town as the one above it, as a small village on the same ground: a few houses around the bare foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `town_haven` |
| 2 | `town_sylvan` |
| 3 | `town_haven_village` |
| 4 | `town_sylvan_village` |

### T2 · academy, necropolis

**Prompt** · 1536 × 1024 · negative `NEG-TOWNPIC`

```text
Game town picture sheet in a realistic painterly dark-fantasy style, like a small oil painting: each picture a wide view of a whole
small fantasy town seen from a high three-quarter angle (about 35 degrees down) like a scout on a hill would see it, the town spread
left and right across the middle of the picture with its one tall landmark in the centre standing against the sky, the town's own
ground and sky painted all the way to the four edges of the picture. The top-left corner is open sky only and the bottom-right corner
is plain ground only, nothing important there; the top eighth is sky and the bottom eighth is foreground ground. Believable
architecture and scale (doors, windows, roof tiles, stonework, timber), weathered materials with grime and moss, naturalistic light
from the top left with soft shadows, muted natural colours, a muted day sky, big simple masses with strong value contrast between the
town and the sky so it reads when very small, no drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate
pictures arranged in a grid of 2 columns and 2 rows, each a full rectangle filling its own equal cell edge to edge, the cells parted by
a thin plain black gap, all at the same scale, the same viewing angle and the same light. No borders, no frames, no text, no labels,
no numbers. The pictures, top row left to right, then bottom row: 1 a frozen citadel of blue-grey granite with verdigris domes on a
snowy plateau, a domed observatory in the centre holding a bronze orrery of rings; 2 a dark keep among black ruins on a grey barren
plain, a tall straight black obelisk spire in the centre with a cold cyan light at its tip; 3 the same town as the one above it, as a
small village on the same ground: a few houses around the bare foundation of its landmark, no walls; 4 the same town as the one above
it, as a small village on the same ground: a few houses around the bare foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `town_academy` |
| 2 | `town_necropolis` |
| 3 | `town_academy_village` |
| 4 | `town_necropolis_village` |

### T3 · inferno, dungeon

**Prompt** · 1536 × 1024 · negative `NEG-TOWNPIC`

```text
Game town picture sheet in a realistic painterly dark-fantasy style, like a small oil painting: each picture a wide view of a whole
small fantasy town seen from a high three-quarter angle (about 35 degrees down) like a scout on a hill would see it, the town spread
left and right across the middle of the picture with its one tall landmark in the centre standing against the sky, the town's own
ground and sky painted all the way to the four edges of the picture. The top-left corner is open sky only and the bottom-right corner
is plain ground only, nothing important there; the top eighth is sky and the bottom eighth is foreground ground. Believable
architecture and scale (doors, windows, roof tiles, stonework, timber), weathered materials with grime and moss, naturalistic light
from the top left with soft shadows, muted natural colours, a muted day sky, big simple masses with strong value contrast between the
town and the sky so it reads when very small, no drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate
pictures arranged in a grid of 2 columns and 2 rows, each a full rectangle filling its own equal cell edge to edge, the cells parted by
a thin plain black gap, all at the same scale, the same viewing angle and the same light. No borders, no frames, no text, no labels,
no numbers. The pictures, top row left to right, then bottom row: 1 a jagged fortress of black slag with glowing lava cracks on ashen
volcanic ground under a smoky red-brown sky, sickle-shaped spires, an altar-furnace in the centre venting smoke; 2 a gothic black
citadel of many spires on a wide cavern ledge, the dark cavern vault with hanging stalactites taking the place of the sky, a tall spire
of glowing violet crystal in the centre; 3 the same town as the one above it, as a small village on the same ground: a few houses
around the bare foundation of its landmark, no walls; 4 the same town as the one above it, as a small village on the same ground: a
few houses around the bare foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `town_inferno` |
| 2 | `town_dungeon` |
| 3 | `town_inferno_village` |
| 4 | `town_dungeon_village` |

### T4 · stronghold, swamp-bestiary

**Prompt** · 1536 × 1024 · negative `NEG-TOWNPIC`

```text
Game town picture sheet in a realistic painterly dark-fantasy style, like a small oil painting: each picture a wide view of a whole
small fantasy town seen from a high three-quarter angle (about 35 degrees down) like a scout on a hill would see it, the town spread
left and right across the middle of the picture with its one tall landmark in the centre standing against the sky, the town's own
ground and sky painted all the way to the four edges of the picture. The top-left corner is open sky only and the bottom-right corner
is plain ground only, nothing important there; the top eighth is sky and the bottom eighth is foreground ground. Believable
architecture and scale (doors, windows, roof tiles, stonework, timber), weathered materials with grime and moss, naturalistic light
from the top left with soft shadows, muted natural colours, a muted day sky, big simple masses with strong value contrast between the
town and the sky so it reads when very small, no drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate
pictures arranged in a grid of 2 columns and 2 rows, each a full rectangle filling its own equal cell edge to edge, the cells parted by
a thin plain black gap, all at the same scale, the same viewing angle and the same light. No borders, no frames, no text, no labels,
no numbers. The pictures, top row left to right, then bottom row: 1 a log palisade around hide yurts and tents on dry rocky steppe, a
great lodge in the centre under a huge horned beast skull; 2 half-sunken tilted stone ruins with stilt huts in a misty green swamp, a
rampart of giant shells, a tall totem of coiled beasts in the centre; 3 the same town as the one above it, as a small village on the
same ground: a few houses around the bare foundation of its landmark, no walls; 4 the same town as the one above it, as a small
village on the same ground: a few houses around the bare foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `town_stronghold` |
| 2 | `town_swamp-bestiary` |
| 3 | `town_stronghold_village` |
| 4 | `town_swamp-bestiary_village` |

### T5 · conflux, dragon-utopia

**Prompt** · 1536 × 1024 · negative `NEG-TOWNPIC`

```text
Game town picture sheet in a realistic painterly dark-fantasy style, like a small oil painting: each picture a wide view of a whole
small fantasy town seen from a high three-quarter angle (about 35 degrees down) like a scout on a hill would see it, the town spread
left and right across the middle of the picture with its one tall landmark in the centre standing against the sky, the town's own
ground and sky painted all the way to the four edges of the picture. The top-left corner is open sky only and the bottom-right corner
is plain ground only, nothing important there; the top eighth is sky and the bottom eighth is foreground ground. Believable
architecture and scale (doors, windows, roof tiles, stonework, timber), weathered materials with grime and moss, naturalistic light
from the top left with soft shadows, muted natural colours, a muted day sky, big simple masses with strong value contrast between the
town and the sky so it reads when very small, no drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate
pictures arranged in a grid of 2 columns and 2 rows, each a full rectangle filling its own equal cell edge to edge, the cells parted by
a thin plain black gap, all at the same scale, the same viewing angle and the same light. No borders, no frames, no text, no labels,
no numbers. The pictures, top row left to right, then bottom row: 1 a stepped round altar of weathered stone ringed by pillars on a
grassy hill, a glowing violet orb with floating stones above it in the centre, four small domed elemental shrines around it; 2 a
terraced eyrie cut into a dark granite crag among mountains, huge cave mouths, an outcrop at its top in the centre shaped like a
dragon's head; 3 the same town as the one above it, as a small village on the same ground: a few houses around the bare foundation of
its landmark, no walls; 4 the same town as the one above it, as a small village on the same ground: a few houses around the bare
foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `town_conflux` |
| 2 | `town_dragon-utopia` |
| 3 | `town_conflux_village` |
| 4 | `town_dragon-utopia_village` |

## After generating

1. Cut each sheet and look at every picture at 130 × 64 inside a town list entry: the town fills the width, the landmark stands in the
   centre against the sky, and the top-left and bottom-right corners (the governor's portrait and the build mark) cover only sky and
   ground.
2. Put the twenty side by side at that size: each town is named at a glance, walled and village apart.
3. Regenerate a sheet that came out as round patches, islands or cut-outs, with a town cut off at the sides or a cartoon look (thick
   outlines, flat colour fills, bubbly shapes, plastic shine).
4. Put the files in `textures/ui/towns/` and add a line to its `CREDITS.md` with the tool and its licence terms.
