# Cursor prompts (for an image-generating model)

Prompts for the **mouse cursors** (ADR-204): a plain arrow on menus and screens, a horse on the adventure map, one cursor per action in
battle. They are generated like the icons ([icon-prompts.md](icon-prompts.md)): **sheets of 4 (2 × 2) or 6 (2 × 3)**, cut into one file
per cursor. Every cursor is **pale satin aluminium in a thick dark outline**, the metal of the UI icons (ADR-312): no other colour, so it
matches the HUD and stands apart from the ground under it.

## Use a prompt

Copy the sheet's **Prompt** block whole; where the model takes a negative prompt, paste the negative block the Prompt line names (from
[Negative prompts](#negative-prompts)). Generate at the size given, cut the sheet and save each cell as its **File** (`<File>.png`). The
**Hotspot** column is not pasted: it is where the pointing part must land, checked after cutting. If a result comes out coloured (brown,
bronze, gold, violet), add the colour words to the negative prompt again and regenerate; do not paraphrase the prompts.

**Shared wording**: the style paragraph ("Game mouse cursor sheet in a realistic painterly…"), the horse paragraph of sheet 2 ("Every horse
is the same horse…") and the grid sentence ("4 separate icons…" / "6 separate icons…") are repeated verbatim in each prompt so
each is ready to paste; a change to one of them must be made in every prompt of this file.

## Negative prompts

**`NEG-CURSOR`** (every cursor sheet but 6):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, background, scenery, ground shadow, drop shadow, photograph,
plastic CGI render, pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, gold, yellow,
bronze, brass, copper, warm tones, sepia, rust, smooth chrome, mirror polish, high-contrast reflections, dark iron, blackened metal,
gunmetal, dark grey metal, violet, coloured glow, saturated colours, bay horse, chestnut horse, brown horse, cartoon, comic book, anime,
chibi, cute, toy-like, emoji, sticker, mobile game icon, flat colour fills, vector clip art, glossy plastic, bubbly rounded shapes, neon
```

**`NEG-CURSOR-MECH`** (sheet 6, with the bow):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, background, scenery, ground shadow, drop shadow, photograph,
plastic CGI render, pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, gold, yellow,
bronze, brass, copper, warm tones, sepia, rust, smooth chrome, mirror polish, high-contrast reflections, dark iron, blackened metal,
gunmetal, dark grey metal, violet, coloured glow, saturated colours, bay horse, chestnut horse, brown horse, cartoon, comic book, anime,
chibi, cute, toy-like, emoji, sticker, mobile game icon, flat colour fills, vector clip art, glossy plastic, bubbly rounded shapes,
neon, warped or oval wheels, wheels at different angles, wheels tilted off the axle, extra wheels, asymmetric bow arms, bent or doubled
string, extra arms, tangled ropes, floating or disconnected parts
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
- **Colour**: pale aluminium only, light silvery grey to near-white, grainy and satin, never dark or mirror-bright; the one colour is
  the dark red inside the blocked sign; spells and the portal are told apart by their shape and a soft white glow.
- **Spare cell**: a second take of the hardest cursor on the sheet.

## Menus and screens

### 1 · Pointers (2 × 2)

Shown on menus, the town and hero screens and panels; busy while a computer turn plays or a game loads; blocked where a click does nothing.

**Prompt** · 1024 × 1024 · negative `NEG-CURSOR`

```text
Game mouse cursor sheet in a realistic painterly dark-fantasy style: each cursor a clear, simple silhouette made as a real cast and
machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches, small
dents and worn edges catching the light, true proportions. Pale aluminium palette: light silvery grey faces with a soft matte sheen,
near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low
contrast, the metal reads pale and light overall, never dark, never mirror-bright. Cool neutral greys, no warm colours except where a
cell names one, no glow unless a cell names a soft white one. Light from the top left, a thick near-black outline around each subject
and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background. Flat front or side view as
each cell says. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha.
The icons, top row left to right, then bottom row: 1 the standard computer arrow pointer shape, tip at the top left, tilted about 30°,
with a short tail, a bevel along its edges; 2 a small upright hourglass in a frame, fine sand running into the lower bulb; 3 a round
ring with one diagonal bar from top left to bottom right, dark red inside the ring; 4 a second take of the pointer
```

| # | File | Hotspot |
|---|---|---|
| 1 | `cursor_ui_pointer` | `0,0` (the tip) |
| 2 | `cursor_ui_busy` | `4,4` (centre) |
| 3 | `cursor_ui_blocked` | `4,4` |
| 4 | — (spare) | |

## Adventure map

### 2 · Moving and visiting (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-CURSOR`

```text
Game mouse cursor sheet in a realistic painterly dark-fantasy style: each cursor a clear, simple silhouette made as a real cast and
machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches, small
dents and worn edges catching the light, true proportions. Pale aluminium palette: light silvery grey faces with a soft matte sheen,
near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low
contrast, the metal reads pale and light overall, never dark, never mirror-bright. Cool neutral greys, no warm colours except where a
cell names one, no glow unless a cell names a soft white one. Light from the top left, a thick near-black outline around each subject
and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background. Flat front or side view as
each cell says. Not a photograph, not a plastic CGI render, no pixel art. Every horse is the same horse: a war horse cast in pale satin
aluminium like a small statuette, near-white on the muscles, a soft grey mane and tail, a plain bridle, never brown; in pure side
profile facing right unless the cell says otherwise. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 the aluminium horse galloping to the right, all four legs off the ground; 2 the aluminium horse
rearing up, front hooves raised high, facing right; 3 the aluminium horse's head and neck facing right, two crossed swords behind it; 4
two aluminium horse heads facing each other, a curved double-headed arrow between them; 5 the aluminium horse walking right towards a
small stone gate arch, its rump at the left edge; 6 a swirl of sparks around a small bright star at the centre, a soft white glow
```

| # | File | Hotspot |
|---|---|---|
| 1 | `cursor_adv_move` | `4,7` (under its hooves) |
| 2 | `cursor_adv_visit` | `4,7` |
| 3 | `cursor_adv_attack` | `4,4` |
| 4 | `cursor_adv_exchange` | `4,4` |
| 5 | `cursor_adv_enter-town` | `4,7` |
| 6 | `cursor_adv_spell` | `4,4` |

### 3 · Selecting and digging (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-CURSOR`

```text
Game mouse cursor sheet in a realistic painterly dark-fantasy style: each cursor a clear, simple silhouette made as a real cast and
machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches, small
dents and worn edges catching the light, true proportions. Pale aluminium palette: light silvery grey faces with a soft matte sheen,
near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low
contrast, the metal reads pale and light overall, never dark, never mirror-bright. Cool neutral greys, no warm colours except where a
cell names one, no glow unless a cell names a soft white one. Light from the top left, a thick near-black outline around each subject
and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background. Flat front or side view as
each cell says. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha.
The icons, top row left to right, then bottom row: 1 a plain knight's helmet in three-quarter view, a small plume; 2 a small stone keep
with one tower and a conical roof, seen from the front; 3 a short shovel stuck in the ground at an angle, a little heap of earth beside
it; 4 a second take of the exchange cursor
```

| # | File | Hotspot |
|---|---|---|
| 1 | `cursor_adv_select-hero` | `4,4` |
| 2 | `cursor_adv_select-town` | `4,4` |
| 3 | `cursor_adv_dig` | `4,7` (the blade's tip) |
| 4 | — (spare) | |

## Battle

### 4–5 · Melee attack, by side (2 × 2 each)

One sword, the same on all eight cells, filling its cell, the hilt towards the side the attacker strikes from; the file names that
side. The point is the hotspot: when cut, the sword is placed so its point lands one eighth in from the edges it points at.

**Prompt, sheet 4** · 1024 × 1024 · negative `NEG-CURSOR`

```text
Game mouse cursor sheet in a realistic painterly dark-fantasy style: each cursor a clear, simple silhouette made as a real cast and
machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches, small
dents and worn edges catching the light, true proportions. Pale aluminium palette: light silvery grey faces with a soft matte sheen,
near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low
contrast, the metal reads pale and light overall, never dark, never mirror-bright. Cool neutral greys, no warm colours except where a
cell names one, no glow unless a cell names a soft white one. Light from the top left, a thick near-black outline around each subject
and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background. Flat front or side view as
each cell says. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha.
The icons, top row left to right, then bottom row: 1 a straight aluminium sword with a satin blade and a darker grey cross-guard and
grip, points straight down, hilt at the top; 2 a straight aluminium sword with a satin blade and a darker grey cross-guard and grip,
points down-left, hilt at the top right; 3 a straight aluminium sword with a satin blade and a darker grey cross-guard and grip, points
left, hilt at the right; 4 a straight aluminium sword with a satin blade and a darker grey cross-guard and grip, points up-left, hilt at
the bottom right
```

**Prompt, sheet 5** · 1024 × 1024 · negative `NEG-CURSOR`

```text
Game mouse cursor sheet in a realistic painterly dark-fantasy style: each cursor a clear, simple silhouette made as a real cast and
machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches, small
dents and worn edges catching the light, true proportions. Pale aluminium palette: light silvery grey faces with a soft matte sheen,
near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low
contrast, the metal reads pale and light overall, never dark, never mirror-bright. Cool neutral greys, no warm colours except where a
cell names one, no glow unless a cell names a soft white one. Light from the top left, a thick near-black outline around each subject
and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background. Flat front or side view as
each cell says. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha.
The icons, top row left to right, then bottom row: 1 a straight aluminium sword with a satin blade and a darker grey cross-guard and
grip, points straight up, hilt at the bottom; 2 a straight aluminium sword with a satin blade and a darker grey cross-guard and grip,
points up-right, hilt at the bottom left; 3 a straight aluminium sword with a satin blade and a darker grey cross-guard and grip, points
right, hilt at the left; 4 a straight aluminium sword with a satin blade and a darker grey cross-guard and grip, points down-right, hilt
at the top left
```

| Sheet | # | File | Hotspot |
|---|---|---|---|
| 4 | 1 | `cursor_bat_melee_from-n` | `4,7` (the point) |
| 4 | 2 | `cursor_bat_melee_from-ne` | `1,7` (the point) |
| 4 | 3 | `cursor_bat_melee_from-e` | `1,4` (the point) |
| 4 | 4 | `cursor_bat_melee_from-se` | `1,1` (the point) |
| 5 | 1 | `cursor_bat_melee_from-s` | `4,1` (the point) |
| 5 | 2 | `cursor_bat_melee_from-sw` | `7,1` (the point) |
| 5 | 3 | `cursor_bat_melee_from-w` | `7,4` (the point) |
| 5 | 4 | `cursor_bat_melee_from-nw` | `7,7` (the point) |

If the model cannot keep the eight swords alike, generate sheet 4 alone and mirror it: `from-w`, `from-sw` and `from-nw` are `from-e`,
`from-se` and `from-ne` mirrored left to right, `from-s` is `from-n` flipped upside down.

### 6 · Shooting, spells and machines (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-CURSOR-MECH`

```text
Game mouse cursor sheet in a realistic painterly dark-fantasy style: each cursor a clear, simple silhouette made as a real cast and
machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches, small
dents and worn edges catching the light, true proportions. Pale aluminium palette: light silvery grey faces with a soft matte sheen,
near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low
contrast, the metal reads pale and light overall, never dark, never mirror-bright. Cool neutral greys, no warm colours except where a
cell names one, no glow unless a cell names a soft white one. Light from the top left, a thick near-black outline around each subject
and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background. Flat front or side view as
each cell says. Not a photograph, not a plastic CGI render, no pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha.
The icons, top row left to right, then bottom row: 1 a drawn short bow seen from the front, an arrow nocked and pointing up-left,
mirror-symmetric arms; 2 the same arrow without the bow, snapped in the middle, the two halves slightly apart; 3 a short wand pointing
up-left, a small bright star at its tip with a soft white glow; 4 a round target ring with four short marks at the top, bottom, left and
right, a spark at the centre; 5 a round boulder flying into a broken piece of stone wall, chips flying; 6 a second take of the siege
cursor
```

| # | File | Hotspot |
|---|---|---|
| 1 | `cursor_bat_shoot` | `0,0` (the arrow's tip, up-left) |
| 2 | `cursor_bat_shoot-weak` | `0,0` |
| 3 | `cursor_bat_spell` | `0,0` (the star) |
| 4 | `cursor_bat_spell-area` | `4,4` |
| 5 | `cursor_bat_siege` | `4,4` |
| 6 | — (spare) | |

### 7 · Moving in battle (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-CURSOR`

```text
Game mouse cursor sheet in a realistic painterly dark-fantasy style: each cursor a clear, simple silhouette made as a real cast and
machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches, small
dents and worn edges catching the light, true proportions. Pale aluminium palette: light silvery grey faces with a soft matte sheen,
near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low
contrast, the metal reads pale and light overall, never dark, never mirror-bright. Cool neutral greys, no warm colours except where a
cell names one, no glow unless a cell names a soft white one. Light from the top left, a thick near-black outline around each subject
and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background. Flat front or side view as
each cell says. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha.
The icons, top row left to right, then bottom row: 1 an armoured sabaton (a knight's boot) in side profile, facing right, mid-step; 2
one spread feathered wing rising to the right; 3 a ring of sparks around an empty dark centre, like a small portal seen from the front,
a soft white glow; 4 a second take of the wing
```

| # | File | Hotspot |
|---|---|---|
| 1 | `cursor_bat_walk` | `4,7` (the sole) |
| 2 | `cursor_bat_fly` | `4,4` |
| 3 | `cursor_bat_teleport` | `4,4` |
| 4 | — (spare) | |

## After generating

1. Look at every cursor at 32 px on a dark and on a light background: one clear shape, no colour cast. Desaturate by hand only a
   slight tint; regenerate a coloured one.
2. Check the hotspot: the pointing part lands on the cell given in the table.
3. Cut each cursor to 64 × 64 with its pointing part on the hotspot (the arrow's tip one pixel in from the top left, the hooves, boot
   and shovel at 7/8 down, the swords' points as in their table, the others centred), put the files in
   `src/Godot/EternalWars.Godot.Assets/textures/ui/cursors/` and update its `CREDITS.md`.
