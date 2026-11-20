# Cursor prompts (for an image-generating model)

Prompts for the **mouse cursors** (ADR-204): a plain arrow on menus and screens, a horse on the adventure map, one cursor per action in
battle. They are generated like the icons ([icon-prompts.md](icon-prompts.md)): **sheets of 4 (2 × 2) or 6 (2 × 3)**, cut into one file
per cursor. Every cursor is **cold grey iron and steel**: no brown, no bronze, no gold, so it stands apart from the warm bronze UI and
from the ground under it.

## Compose a prompt

Every sheet below has a **Compose** line naming its blocks. Build the prompt from blocks, copying each block's text exactly and joining
them with a space:

```text
PROMPT   = CURSOR-STYLE (+ CURSOR-HORSE on sheet 2) + ICON-SHEET-4 or ICON-SHEET-6 + CELLS
CELLS    = "1 <subject 1>; 2 <subject 2>; …"   the sheet table's Subject column, in its # order, spare cell included
NEGATIVE = ICON-NEG + ", " + CURSOR-NEG   (only where the model takes a negative prompt)
OUTPUT   = the size on the Compose line; cut the sheet and save each cell as <File>.png
```

`CURSOR-*` blocks are below; `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in [icon-prompts.md](icon-prompts.md#blocks). The
**Hotspot** column is not pasted: it is where the pointing part must land, checked after cutting. Sheets 4–5 give a **Cell rule** instead
of full subjects: apply it to each row. If a result comes out brown or bronze, add the colour words to the negative prompt again and
regenerate; do not paraphrase blocks or subjects.

## Blocks

**`CURSOR-STYLE`** (style, first in every prompt):

```text
Game mouse cursor sheet in a realistic painterly dark-fantasy style: each cursor a clear, simple silhouette made as a real forged metal
object, hammered and engraved iron and steel with true metallic reflections, fine scratches, pitting and worn edges catching the light,
true proportions. Cold grey palette: dark gunmetal iron, polished pale steel and blackened iron, cool blue-grey highlights, no warm
colours except where a cell names one. Light from the top left, a thick near-black outline around each subject and a thin pale silver
edge just inside it so it reads on dark and on light ground, flat front or side view as each cell says, no cast shadow, no background.
Not a photograph, not a plastic CGI render, no pixel art.
```

**`CURSOR-HORSE`** (after `CURSOR-STYLE` on sheet 2):

```text
Every horse is the same horse: a war horse forged from dark grey iron like a polished steel statuette, a cool gunmetal sheen with pale
steel highlights on the muscles, a black iron mane and tail, a plain steel bridle, never brown; in pure side profile facing right
unless the cell says otherwise.
```

**`CURSOR-NEG`** (negative prompt, after `ICON-NEG` on every cursor sheet):

```text
text, letters, numbers, frames, drop shadows, thin lines, scenery, background, extra objects, brown, bronze, brass, copper, gold, warm
tones, sepia, bay horse, chestnut horse, brown horse, leather brown, wood brown, cartoon, comic, cute, toy-like, flat colour fills,
vector clip art, glossy plastic, bubbly rounded shapes
```

## Output for every cursor

- **Sheet**: 2 × 2 at 1024 × 1024, 2 × 3 at 1536 × 1024, every cell 512 × 512, transparent background; cut as the icons.
- **File**: `cursor_<group>_<id>.png`, 32-bit RGBA, 64 × 64 in the game, cut from the 512 px cell; shown at 43 and 32. One still
  image a cursor, no animation.
- **Hotspot**: given per cursor in 1/8 of the cell from the top left (`1,1` is 64 px in at 512 px). The **pointing part touches the
  hotspot**: the arrow's tip, the sword's point, the centre of a ring. A cursor redrawn with a moved hotspot needs its row changed in
  `GameCursors.Definition`.
- **Shape**: reads at 32 px over a busy 3D map: one clear silhouette, a **thick dark outline (about 5 % of the cell)**, a light inner edge,
  no thin lines, no text or numbers (the game draws the days a move takes).
- **Colour**: grey iron and steel; the only colours are those a subject names (the violet of spells, the dark red of the blocked sign).
- **Spare cell**: a second take of the hardest cursor on the sheet.

## Menus and screens

### 1 · Pointers (2 × 2)

**Compose**: `CURSOR-STYLE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `CURSOR-NEG` · 1024 × 1024

Shown on menus, the town and hero screens and panels; busy while a computer turn plays or a game loads; blocked where a click does nothing.

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_ui_pointer` | the standard computer arrow pointer shape, tip at the top left, tilted about 30°, with a short tail; dark polished steel with a pale steel bevel | `0,0` (the tip) |
| 2 | `cursor_ui_busy` | a small upright hourglass in a blackened iron frame, pale grey sand running into the lower bulb | `4,4` (centre) |
| 3 | `cursor_ui_blocked` | a round steel ring with one diagonal bar from top left to bottom right, dark red inside the ring | `4,4` |
| 4 | — | spare: a second take of the pointer | |

## Adventure map

### 2 · Moving and visiting (2 × 3)

**Compose**: `CURSOR-STYLE` + `CURSOR-HORSE` + `ICON-SHEET-6` + cells · negative `ICON-NEG` + `CURSOR-NEG` · 1536 × 1024

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_adv_move` | the iron horse galloping to the right, all four legs off the ground | `4,7` (under its hooves) |
| 2 | `cursor_adv_visit` | the iron horse rearing up, front hooves raised high, facing right | `4,7` |
| 3 | `cursor_adv_attack` | the iron horse's head and neck facing right, two crossed steel swords behind it | `4,4` |
| 4 | `cursor_adv_exchange` | two iron horse heads facing each other, a curved steel double-headed arrow between them | `4,4` |
| 5 | `cursor_adv_enter-town` | the iron horse walking right towards a small grey stone gate arch, its rump at the left edge | `4,7` |
| 6 | `cursor_adv_spell` | a violet swirl of sparks around a small bright white star at the centre | `4,4` |

### 3 · Selecting and digging (2 × 2)

**Compose**: `CURSOR-STYLE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `CURSOR-NEG` · 1024 × 1024

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_adv_select-hero` | a plain steel knight's helmet in three-quarter view, a small pale grey plume | `4,4` |
| 2 | `cursor_adv_select-town` | a small grey stone keep with one tower and a dark slate-blue conical roof, seen from the front | `4,4` |
| 3 | `cursor_adv_dig` | a short iron shovel with an iron shaft stuck in the ground at an angle, a little heap of dark grey earth beside it | `4,7` (the blade's tip) |
| 4 | — | spare: a second take of the exchange cursor | |

## Battle

### 4–5 · Melee attack, by side (2 × 2 each)

**Compose**: `CURSOR-STYLE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `CURSOR-NEG` · 1024 × 1024

One sword, the same on all eight cells, filling its cell, the hilt towards the side the attacker strikes from; the file names that
side. The point is the hotspot: when cut, the sword is placed so its point lands one eighth in from the edges it points at.

**Cell rule**: subject = "a straight steel sword with a blackened iron cross-guard and grip, " + Sword; sheet 4 takes the rows marked 4,
sheet 5 those marked 5.

| Sheet | # | File | Sword | Hotspot |
|---|---|---|---|---|
| 4 | 1 | `cursor_bat_melee_from-n` | points straight down, hilt at the top | `4,7` (the point) |
| 4 | 2 | `cursor_bat_melee_from-ne` | points down-left, hilt at the top right | `1,7` (the point) |
| 4 | 3 | `cursor_bat_melee_from-e` | points left, hilt at the right | `1,4` (the point) |
| 4 | 4 | `cursor_bat_melee_from-se` | points up-left, hilt at the bottom right | `1,1` (the point) |
| 5 | 1 | `cursor_bat_melee_from-s` | points straight up, hilt at the bottom | `4,1` (the point) |
| 5 | 2 | `cursor_bat_melee_from-sw` | points up-right, hilt at the bottom left | `7,1` (the point) |
| 5 | 3 | `cursor_bat_melee_from-w` | points right, hilt at the left | `7,4` (the point) |
| 5 | 4 | `cursor_bat_melee_from-nw` | points down-right, hilt at the top left | `7,7` (the point) |

If the model cannot keep the eight swords alike, generate sheet 4 alone and mirror it: `from-w`, `from-sw` and `from-nw` are `from-e`,
`from-se` and `from-ne` mirrored left to right, `from-s` is `from-n` flipped upside down.

### 6 · Shooting, spells and machines (2 × 3)

**Compose**: `CURSOR-STYLE` + `ICON-SHEET-6` + cells · negative `ICON-NEG` + `CURSOR-NEG` + `ICON-NEG-MECH` · 1536 × 1024

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_bat_shoot` | a drawn short bow of blackened iron seen from the front, a steel-tipped arrow nocked and pointing up-left, **mirror-symmetric arms** | `0,0` (the arrow's tip, up-left) |
| 2 | `cursor_bat_shoot-weak` | the same arrow without the bow, snapped in the middle, the two halves slightly apart | `0,0` |
| 3 | `cursor_bat_spell` | a short iron wand pointing up-left, a small bright violet star at its tip | `0,0` (the star) |
| 4 | `cursor_bat_spell-area` | a round target ring of violet light with four short marks at the top, bottom, left and right, a spark at the centre | `4,4` |
| 5 | `cursor_bat_siege` | a round grey stone boulder flying into a broken piece of grey stone wall, chips flying | `4,4` |
| 6 | — | spare: a second take of the siege cursor | |

### 7 · Moving in battle (2 × 2)

**Compose**: `CURSOR-STYLE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `CURSOR-NEG` · 1024 × 1024

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_bat_walk` | an armoured steel sabaton (a knight's iron boot) in side profile, facing right, mid-step | `4,7` (the sole) |
| 2 | `cursor_bat_fly` | one spread feathered wing of pale silver and steel-grey feathers, rising to the right | `4,4` |
| 3 | `cursor_bat_teleport` | a violet ring of sparks around an empty dark centre, like a small portal seen from the front | `4,4` |
| 4 | — | spare: a second take of the wing | |

## After generating

1. Look at every cursor at 32 px on a dark and on a light background: one clear shape, no brown or bronze cast. Recolour by hand only
   a slight warm tint; regenerate a brown one.
2. Check the hotspot: the pointing part lands on the cell given in the table.
3. Cut each cursor to 64 × 64 with its pointing part on the hotspot (the arrow's tip one pixel in from the top left, the hooves, boot
   and shovel at 7/8 down, the swords' points as in their table, the others centred), put the files in
   `src/Godot/EternalWars.Godot.Assets/textures/ui/cursors/` and update its `CREDITS.md`.
