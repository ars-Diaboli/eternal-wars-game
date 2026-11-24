# Icon prompts (for an image-generating model)

Prompts for the **UI icons** (ADR-116, ADR-204, ADR-312): resources, primary stats, skills, magic schools, war machines, adventure-map
and battle actions, plain icon buttons, the faction heraldry and the five difficulty levels. Every icon but the heraldry is **aluminium**:
pale satin metal with a fine grainy texture and worn edges, light grey to near-white, in a thick dark outline, made to sit on the game's
near-black panels with their grey rims (ADR-311).
They are generated as **sheets of 4 (2 × 2) or 6 (2 × 3)**, the shapes the image model handles best, and cut into one file per icon. A
sheet groups icons of the **same level of detail and the same use**, so they come out matching. Other icon sets have their own files: [cursor-prompts.md](cursor-prompts.md),
[spell-icon-prompts.md](spell-icon-prompts.md), [creature-icon-prompts.md](creature-icon-prompts.md), [editor-icon-prompts.md](editor-icon-prompts.md).

## Use a prompt

1. Copy the sheet's **Prompt** block whole; it already holds the style, the grid and every cell.
2. Where the model takes a negative prompt, paste the negative block named on the Prompt line ([Negative prompts](#negative-prompts)).
3. Generate at the size on the Prompt line, cut the sheet into its cells and save each cell as its **File** (`<File>.png`).

Do not paraphrase the prompts: the wording keeps the sheets consistent with each other.

## Shared wording

Each prompt repeats its style paragraph ("Game UI icon sheet of small aluminium objects…", "Game UI icon sheet of aluminium glyphs…",
"Game UI button symbol sheet of aluminium glyphs…", "Fantasy heraldry crest sheet in a realistic…", "Heraldic charge sheet, each figure a
single…") and the grid sentence ("4 separate icons arranged in a grid…" or "6 separate icons arranged in a grid…") verbatim, so
each is ready to paste. A change to them must be made in every prompt of this file: search for those first words.

## Negative prompts

**`NEG-ALU`** (every aluminium sheet: tiers A, B and C):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, background, scenery, ground shadow, drop shadow, photograph,
plastic CGI render, pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, gold, yellow,
bronze, brass, copper, warm tones, sepia, rust, smooth chrome, mirror polish, high-contrast reflections, dark iron, blackened metal,
gunmetal, dark grey metal, colour accents, coloured glow, saturated colours, cartoon, comic book, anime, chibi, cute, toy-like, emoji,
sticker, mobile game icon, flat colour fills, vector clip art, glossy plastic, bubbly rounded shapes, neon
```

**`NEG-ALU-MECH`** (aluminium sheets with mechanical subjects: machines, bows, scales, carts):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, background, scenery, ground shadow, drop shadow, photograph,
plastic CGI render, pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, gold, yellow,
bronze, brass, copper, warm tones, sepia, rust, smooth chrome, mirror polish, high-contrast reflections, dark iron, blackened metal,
gunmetal, dark grey metal, colour accents, coloured glow, saturated colours, cartoon, comic book, anime, chibi, cute, toy-like, emoji,
sticker, mobile game icon, flat colour fills, vector clip art, glossy plastic, bubbly rounded shapes, neon, warped or oval wheels,
wheels at different angles, wheels tilted off the axle, extra wheels, asymmetric bow arms, bent or doubled string, extra arms, tangled
ropes, floating or disconnected parts
```

**`NEG-PAINTED`** (the crests):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, scenery, ground shadow, photograph, plastic CGI render,
pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, cartoon, comic book, anime, manga,
chibi, cute, toy-like, childish, cel shading, flat colours, flat shading, vector art, clip art, sticker, emoji, mobile game icon,
casual game art, thick black outlines, ink line art, exaggerated proportions, oversized head, big eyes, rounded bubbly shapes, glossy
plastic, smooth airbrushed gradients, oversaturated colours, candy colours
```

**`NEG-PLAIN`** (the charges):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, scenery, ground shadow, photograph, plastic CGI render,
pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell.
```

## Output for every icon

- **Sheet**: 2 × 2 at **1024 × 1024**, 2 × 3 (3 columns, 2 rows) at **1536 × 1024**; every cell is 512 × 512, listed in reading order
  (top row left to right, then the bottom row). Keep the sheets (`sheet_<name>.png`) as the masters.
- **Icon**: one PNG a cell, 32-bit RGBA, sRGB, **512 × 512**, transparent background (crests and charges included), named as in the
  tables (`icon_resource_gold.png`). The game scales them to 64, 48 and 32 px, so every shape must read at 32 px: one clear silhouette, no
  line thinner than 6 % of the cell, no text, letters or numbers.
- **Cutting**: split the sheet into equal tiles (ImageMagick: `magick sheet.png -crop 3x2@ +repage icon_%d.png`, `2x2@` for a 2 × 2
  sheet); each tile keeps the sheet's transparency.
- **Spare cell**: where a sheet has fewer icons than cells, the free cell is a second take of the hardest icon on it. Keep the better one.
- **Light**: from the top left; one soft highlight, no rim light, no cast shadow outside the subject.
- **Aluminium** (tiers A, B and C): one metal, pale satin aluminium with a fine grainy bead-blasted texture, faint brushed lines, small
  scratches and worn edges; light silvery grey faces, near-white bevels, soft cool grey only in the recesses; soft, low-contrast
  reflections, never dark and never mirror-bright; no gold, bronze or other colour, no coloured glow. A thick near-black outline with a
  thin white edge inside it, as on the cursors, so the icon reads on the dark panels and on light ground alike. A subject is told apart
  by its shape alone.
- **Crests**: painted like a realistic oil study, not a cartoon: true proportions, real material texture, values doing the work that
  outlines do in cartoon icons.
- **Colour**: only the crests carry colour; the faction groups own theirs: Dawn Covenant (Haven, Sylvan, Academy) gold, white, green,
  sky blue; Ashen Court (Necropolis, Inferno, Dungeon) ash grey, ember red, violet, bone; Unbound (Stronghold, Swamp Bestiary) raw wood,
  leather, bone, moss green; Firstborn (Conflux, Dragon Utopia) stone, crystal, old bronze.

## Detail tiers

| Tier | What | Where it is shown | Sheets |
|---|---|---|---|
| **A · Objects** | small objects cast in aluminium, three-quarter view | the resource bar, trade and cost lists, the army row's machines, the difficulty row; 24–64 px | 1, 7, 29 |
| **B · Glyphs** | bold symbols in aluminium relief, flat front view | the hero card's and hero screen's stats, skill and spell rows, the action panel, the battle bar, the adventure HUD's marks; 24–48 px | 2–6, 8–18, 30, 32 |
| **C · Plain** | the simplest aluminium symbols, which the theme tints | buttons, the replay bar; 20–32 px | 19–22, 31 |
| **Heraldry** | faction crests in colour, and their one-colour charges | town and player panels, flags and banners; 24–128 px | 23–28 |

## Tier A · Objects

### 1 · Resources (2 × 3) — the resource bar, trade and cost lists

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of small aluminium objects in a realistic painterly dark-fantasy style: each icon one simple object made as a real
cast and machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine
scratches, small dents and worn edges catching the light, gently bevelled edges and a little engraved detail, true proportions and
believable construction. Pale aluminium palette: light silvery grey faces with a soft matte sheen, near-white on the bevels and raised
edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low contrast, the metal reads pale and
light overall, never dark, never mirror-bright. One metal only, cool neutral greys, no other colour, no glow. Light from the top left, a
thick near-black outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast
shadow, no background. Three-quarter view slightly from above unless a cell says otherwise; each subject a clear, compact silhouette
that reads at 24 px. Not a photograph, not a plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3
columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a small heap of thick, worn coins stamped with a sun,
one coin standing upright in front; 2 three cut logs stacked as a pyramid (two below, one on top), tied with a rope; ridged bark, the
end grain with its rings facing the viewer; 3 three rough cubic lumps of ore stacked together, two below and one on top, faceted and
pitted, a few bright metallic flecks; 4 a cluster of three crystals (one tall, two short) growing from a small rock base, sharp clean
facets that catch the light; 5 a round-bellied flask with a short neck and a stopper, half full of liquid mercury that mirrors the
light, one round droplet beside it; 6 a small pile of rough, powdery crystals, one thin curl of smoke rising from it
```

| # | File |
|---|---|
| 1 | `icon_resource_gold` |
| 2 | `icon_resource_lumber` |
| 3 | `icon_resource_ore` |
| 4 | `icon_resource_crystal` |
| 5 | `icon_resource_mercury` |
| 6 | `icon_resource_sulfur` |

### 7 · War machines (2 × 2) — the hero's army row, the blacksmith

Image models draw machines badly (wheels at odd angles, uneven bow arms). This sheet follows the **rules for mechanical subjects** below;
generate it on its own and expect to roll it several times.

**Prompt** · 1024 × 1024 · negative `NEG-ALU-MECH`, plus `flat side view, pure side profile, top-down view, seen from directly
above, orthographic, blueprint, technical drawing, diagram`

```text
Game UI icon sheet of small aluminium objects in a realistic painterly dark-fantasy style: each icon one simple object made as a real
cast and machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine
scratches, small dents and worn edges catching the light, gently bevelled edges and a little engraved detail, true proportions and
believable construction. Pale aluminium palette: light silvery grey faces with a soft matte sheen, near-white on the bevels and raised
edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low contrast, the metal reads pale and
light overall, never dark, never mirror-bright. One metal only, cool neutral greys, no other colour, no glow. Light from the top left, a
thick near-black outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast
shadow, no background. Three-quarter view slightly from above unless a cell says otherwise; each subject a clear, compact silhouette
that reads at 24 px. Not a photograph, not a plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2
columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a heavy siege crossbow on a low four-legged trestle,
aimed to the upper right: a long stock running diagonally across the cell, the bow across its front end as one wide arc,
mirror-symmetric, both arms the same length and curve, the string a straight line from tip to tip pulled back into a V to the trigger,
one thick bolt lying along the stock; bands round the stock; the timber shows its grain in the aluminium; no wheels; 2 a catapult facing
to the right: a low rectangular frame on four solid plank wheels with rims; the two near wheels fully in view, upright ellipses of
exactly the same shape, both square to one straight axle; the two far wheels partly hidden behind the frame, the same ellipses; one
throwing arm angled up at 45° with a cup holding a round stone, a crossbar where the arm stops; the timber shows its grain in the
aluminium; 3 a small field tent, its flap tied open, a pole at the front with a flame-shaped pennant (no cross: it is a protected
emblem); 4 a two-wheeled handcart, its two long handles reaching toward the viewer at the lower left: two solid plank wheels with rims
on one straight axle under the box, the near wheel a full upright ellipse, the far wheel the same ellipse partly hidden behind the box;
the box full of bundled arrows standing upright
```

| # | File |
|---|---|
| 1 | `icon_machine_ballista` |
| 2 | `icon_machine_catapult` |
| 3 | `icon_machine_healing-tent` |
| 4 | `icon_machine_ammo-cart` |

**Rules for mechanical subjects** (machines, bows, scales, carts):

- **Show machines with depth, and pin the perspective**: a wheeled machine or a siege engine in a three-quarter view from the front left
  and about 30° above (a flat side profile or a view from straight above reads as a diagram, not an object), and describe the wheels in
  that view: "upright ellipses of exactly the same shape, square to one straight axle". A hand-held bow or scales may stay straight front.
- **Say the counts and shapes**: "one visible wheel, a perfect circle, six spokes", "two wheels", "one throwing arm".
- **Say the symmetry**: "mirror-symmetric, both arms the same length and curve, the string straight from tip to tip".
- **Fewer parts**: no ropes, winches, gears or crew unless the subject needs them; solid plank wheels are safer than spoked ones.
- **Use `NEG-ALU-MECH` as the negative** (the Prompt lines of sheets 3, 7 and 16 name it; use it for any new sheet with a wheel, a bow or scales).
- **If it still fails**: generate the one icon alone (1 × 1 at 1024 px); or sketch it (a few boxes and cylinders in Blender or Godot, a
  screenshot from the same three-quarter camera is enough) and give it to the model as the image input at medium strength; or fix the
  wheel by hand with an ellipse.

### 29 · Difficulty (2 × 3) — the new-game screen's difficulty row, the victory screen, the leaderboard (ADR-225)

Five chess pieces, from humble to grand, so the row reads as a climb at a glance: **aluminium from rough and matte to bright and finely finished**, each piece
standing upright on its own round base, **the same height and lighting** in every cell, three-quarter view facing left, each finer than
the last.
Generate the five on one sheet so they match.

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of small aluminium objects in a realistic painterly dark-fantasy style: each icon one simple object made as a real
cast and machined aluminium object, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine
scratches, small dents and worn edges catching the light, gently bevelled edges and a little engraved detail, true proportions and
believable construction. Pale aluminium palette: light silvery grey faces with a soft matte sheen, near-white on the bevels and raised
edges, soft cool grey only in the recesses and engraved lines; reflections soft and diffuse, low contrast, the metal reads pale and
light overall, never dark, never mirror-bright. One metal only, cool neutral greys, no other colour, no glow. Light from the top left, a
thick near-black outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast
shadow, no background. Three-quarter view slightly from above unless a cell says otherwise; each subject a clear, compact silhouette
that reads at 24 px. Not a photograph, not a plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3
columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a pawn of rough bead-blasted aluminium, a few nicks in
the base; 2 a knight of brushed aluminium, a horse's head with a carved mane; 3 a bishop of satin aluminium, a thin engraved band round
its waist; 4 a rook of fine satin aluminium, its battlements and the ring at its base finely engraved; 5 a queen of the brightest satin
aluminium, a crown set with one clear faceted gem, engraved rings at the waist and base; 6 a second take of the queen
```

| # | File |
|---|---|
| 1 | `icon_difficulty_easy` |
| 2 | `icon_difficulty_normal` |
| 3 | `icon_difficulty_hard` |
| 4 | `icon_difficulty_expert` |
| 5 | `icon_difficulty_master` |
| 6 | spare |

## Tier B · Glyphs

### 2 · Hero stats (2 × 3) — the hero screen's header, level-up

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 two straight swords crossed in an X, points up, short cross-guards and round pommels; 2 a heater
shield seen from the front, a raised rim and a plain field; 3 a short magic wand pointing up to the right, a four-rayed star at its tip;
4 an open book seen from the front, its pages fanned, a small rune on the left page; 5 one large drop, a small four-rayed star inside
it; 6 a five-pointed star with bevelled faces
```

| # | File |
|---|---|
| 1 | `icon_stat_attack` |
| 2 | `icon_stat_defense` |
| 3 | `icon_stat_spell-power` |
| 4 | `icon_stat_knowledge` |
| 5 | `icon_stat_mana` |
| 6 | `icon_stat_experience` |

### 3 · Combat skills (2 × 3) — skill rows, level-up offers

The level (Basic … Master) is shown by the game with a frame, not in the icon.

**Prompt** · 1536 × 1024 · negative `NEG-ALU-MECH`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 two broadswords crossed in an X, blades up, a small four-rayed spark where the blades cross; 2 a drawn
longbow held upright, an arrow nocked and the string pulled back in a clean V; both limbs the same length and curve (mirror-symmetric);
3 a round archery target seen from the front, three rings, one arrow buried in the exact centre, its feathers toward the viewer; 4 a
breastplate on its own, seen from the front: rounded shoulder plates, a few rivets; 5 a round shield seen from the front; a zig-zag
lightning bolt strikes it from the upper right and shatters into small sparks on its face; 6 a commander's closed helmet in
three-quarter view, a tall plume
```

| # | File |
|---|---|
| 1 | `icon_skill_offense.melee` |
| 2 | `icon_skill_offense.archery` |
| 3 | `icon_skill_offense.marksmanship` |
| 4 | `icon_skill_defense.armor` |
| 5 | `icon_skill_defense.resistance` |
| 6 | `icon_skill_defense.leadership` |

### 4 · Kingdom skills (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a chest with its lid open, heaped with coins that spill over its front edge; 2 a miner's pickaxe
leaning on a small heap of ore with two crystals in it; 3 a ring with three large old keys, lying across a rolled scroll with a round
seal; 4 a path winding from the bottom of the cell between two small hills into the distance, a tiny signpost at its side; 5 a pair of
packed saddlebags joined by a broad strap, a rolled blanket tied on top; 6 a spyglass, extended in three sections, lying diagonally, a
glint on the lens
```

| # | File |
|---|---|
| 1 | `icon_skill_estates.treasury` |
| 2 | `icon_skill_estates.prospecting` |
| 3 | `icon_skill_estates.stewardship` |
| 4 | `icon_skill_pathfinding.pathfinding` |
| 5 | `icon_skill_pathfinding.logistics` |
| 6 | `icon_skill_pathfinding.scouting` |

### 5 · Battlecraft skills (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a small battle map seen from above at an angle, three tiny banner pins stuck in it, a curved arrow
between them; 2 an open eye seen front-on, a round pupil, short rays around it; 3 a round war drum with zig-zag cords on its side, two
drumsticks crossed above it; 4 a clenched fist seen from the front, wrapped in strips of cloth, small drops flying from it; 5 the head
of a tusked orc warrior seen exactly from the side, mouth wide open in a shout, three curved sound waves in front of the mouth; 6 a mage
staff snapped in two in the middle, splinters and a small spark at the break
```

| # | File |
|---|---|
| 1 | `icon_skill_tactics.tactics` |
| 2 | `icon_skill_tactics.alert` |
| 3 | `icon_skill_tactics.wartempo` |
| 4 | `icon_skill_rage.first` (Blood Rage) |
| 5 | `icon_skill_rage.second` (Warcry) |
| 6 | `icon_skill_rage.third` (Interference) |

### 6 · War-machine skills (2 × 2)

These show what the skill does, not the machine (the machines have their own sheet).

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a round boulder smashing into a block of masonry wall: cracks spreading from the hit, stone chips
flying; 2 a rolled bandage, a small jar of salve with a lid, and a sprig of herb, grouped together; 3 a full quiver standing upright,
packed with arrows and crossbow bolts, their feathers fanned at the top; 4 a second take of cell 1
```

| # | File |
|---|---|
| 1 | `icon_skill_machines.ballistics` |
| 2 | `icon_skill_machines.healing` |
| 3 | `icon_skill_machines.ammo` |
| 4 | spare |

### 8 · Hero condition (2 × 2) — the hero panel, creature windows

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a tall banner on a short pole, the cloth streaming to the right; 2 a four-leaf clover seen from above,
fine veins on the leaves; 3 a traveller's boot seen from the side, a small feathered wing at the heel; 4 a closed eye drawn as a curved
lid with lashes, a small crescent moon above it
```

| # | File |
|---|---|
| 1 | `icon_stat_morale` |
| 2 | `icon_stat_luck` |
| 3 | `icon_stat_movement` |
| 4 | `icon_action_sleep` |

### 9–14 · Magic schools (2 × 2 each, one sheet per school)

Every school sheet has the same four cells; only the emblem changes.

#### 9 · Light (`haven`)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a radiant sun disc with twelve straight rays of alternating length, alone and large; 2 a radiant sun
disc with twelve straight rays of alternating length, above an open spell book seen from the front, its pages fanned; 3 a radiant sun
disc with twelve straight rays of alternating length, smaller, inside a thin ring of small engraved runes; 4 a radiant sun disc with
twelve straight rays of alternating length, smaller, above a small three-pointed crown
```

| # | File |
|---|---|
| 1 | `icon_school_haven` |
| 2 | `icon_skill_magic.haven.spells` |
| 3 | `icon_skill_magic.haven.attunement` |
| 4 | `icon_skill_magic.haven.mastery` |

#### 10 · Nature (`sylvan`)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a single leaf with fine veins, a thin vine curling into a spiral around its stem, one small bud, alone
and large; 2 a single leaf with fine veins, a thin vine curling into a spiral around its stem, one small bud, above an open spell book
seen from the front, its pages fanned; 3 a single leaf with fine veins, a thin vine curling into a spiral around its stem, one small
bud, smaller, inside a thin ring of small engraved runes; 4 a single leaf with fine veins, a thin vine curling into a spiral around its
stem, one small bud, smaller, above a small three-pointed crown
```

| # | File |
|---|---|
| 1 | `icon_school_sylvan` |
| 2 | `icon_skill_magic.sylvan.spells` |
| 3 | `icon_skill_magic.sylvan.attunement` |
| 4 | `icon_skill_magic.sylvan.mastery` |

#### 11 · Arcana (`academy`)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 an eight-pointed star drawn in thin raised lines over a faint circle, a bright point at its centre,
alone and large; 2 an eight-pointed star drawn in thin raised lines over a faint circle, a bright point at its centre, above an open
spell book seen from the front, its pages fanned; 3 an eight-pointed star drawn in thin raised lines over a faint circle, a bright point
at its centre, smaller, inside a thin ring of small engraved runes; 4 an eight-pointed star drawn in thin raised lines over a faint
circle, a bright point at its centre, smaller, above a small three-pointed crown
```

| # | File |
|---|---|
| 1 | `icon_school_academy` |
| 2 | `icon_skill_magic.academy.spells` |
| 3 | `icon_skill_magic.academy.attunement` |
| 4 | `icon_skill_magic.academy.mastery` |

#### 12 · Death (`necropolis`)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a skull seen from the front over an upturned crescent, dark eye sockets, alone and large; 2 a skull
seen from the front over an upturned crescent, dark eye sockets, above an open spell book seen from the front, its pages fanned; 3 a
skull seen from the front over an upturned crescent, dark eye sockets, smaller, inside a thin ring of small engraved runes; 4 a skull
seen from the front over an upturned crescent, dark eye sockets, smaller, above a small three-pointed crown
```

| # | File |
|---|---|
| 1 | `icon_school_necropolis` |
| 2 | `icon_skill_magic.necropolis.spells` |
| 3 | `icon_skill_magic.necropolis.attunement` |
| 4 | `icon_skill_magic.necropolis.mastery` |

#### 13 · Chaos (`inferno`)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a twisting flame with a few sparks flying off it, alone and large; 2 a twisting flame with a few
sparks flying off it, above an open spell book seen from the front, its pages fanned; 3 a twisting flame with a few sparks flying off
it, smaller, inside a thin ring of small engraved runes; 4 a twisting flame with a few sparks flying off it, smaller, above a small
three-pointed crown
```

| # | File |
|---|---|
| 1 | `icon_school_inferno` |
| 2 | `icon_skill_magic.inferno.spells` |
| 3 | `icon_skill_magic.inferno.attunement` |
| 4 | `icon_skill_magic.inferno.mastery` |

#### 14 · Shadow (`dungeon`)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a crescent moon half covered by curling smoke, alone and large; 2 a crescent moon half covered by
curling smoke, above an open spell book seen from the front, its pages fanned; 3 a crescent moon half covered by curling smoke, smaller,
inside a thin ring of small engraved runes; 4 a crescent moon half covered by curling smoke, smaller, above a small three-pointed crown
```

| # | File |
|---|---|
| 1 | `icon_school_dungeon` |
| 2 | `icon_skill_magic.dungeon.spells` |
| 3 | `icon_skill_magic.dungeon.attunement` |
| 4 | `icon_skill_magic.dungeon.mastery` |

### 15 · Adventure map: hero actions (2 × 3) — the action panel

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 two bold chevrons pointing right side by side; 2 a hero's head and shoulders in profile facing right,
a cloak over the shoulder, like a medal; 3 an upright hourglass in a frame, the top bulb almost empty, the last grains falling; 4 a
closed thick spell book, corner fittings and a clasp, a rune on the cover; 5 a shovel stuck upright in a small mound of earth, a few
clods around it; 6 a cave mouth in a rock face, one small arrow above it pointing up and one beside it pointing down (surface and
underground)
```

| # | File |
|---|---|
| 1 | `icon_action_move` |
| 2 | `icon_action_hero` |
| 3 | `icon_action_end-turn` |
| 4 | `icon_action_spell-book` |
| 5 | `icon_action_dig` |
| 6 | `icon_action_level` |

Move walks the selected hero along its route; there is no Next hero button (Tab), and the hero screen opens from the grid and the hero
card's portrait.

### 16 · Adventure map: kingdom (2 × 2) — the action panel, kingdom overview

**Prompt** · 1024 × 1024 · negative `NEG-ALU-MECH`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a small castle seen from the front: three towers with conical roofs, a tiny flag on the middle one; 2
balance scales seen from the front, mirror-symmetric frame, the beam level, coins in the left pan and a crystal in the right; 3 a hooded
mask with empty eye holes, a curved dagger crossed behind it; 4 a rolled scroll tied with a cord, a round wax seal hanging from the cord
```

| # | File |
|---|---|
| 1 | `icon_action_kingdom` |
| 2 | `icon_action_marketplace` |
| 3 | `icon_action_thieves-guild` |
| 4 | `icon_action_quest-log` |

### 30 · Adventure HUD: town marks and levels (2 × 2) — the town list, the minimap's level caption

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a mason's hammer laid diagonally over a squared building stone; 2 a small stone keep seen from the
front, a laurel wreath hanging on its gate; 3 a low hill under a small sun with short rays; 4 a cave arch in rock with three stalactites
hanging from its top, a small crystal inside
```

| # | File |
|---|---|
| 1 | `icon_town_can-build` |
| 2 | `icon_town_fully-built` |
| 3 | `icon_level_surface` |
| 4 | `icon_level_underground` |

The town list shows `icon_town_can-build` lit when the town can build today and dimmed (the game darkens it) when it built today;
`icon_town_fully-built` replaces it when nothing is left to build. The level caption under the minimap shows the level's icon beside its
name.

### 32 · Adventure map: route end markers (2 × 3) — the marker at the end of a hero's route

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 two short swords crossed point up; 2 an open hand seen from the front, palm out in greeting; 3 an open
hand seen from above reaching down toward a single coin; 4 a small town gate seen from the front, an open arch between two towers; 5 two
gauntlets clasped in a handshake; 6 a second take of cell 3
```

| # | File |
|---|---|
| 1 | `icon_route_attack` |
| 2 | `icon_route_visit` |
| 3 | `icon_route_pick-up` |
| 4 | `icon_route_enter-town` |
| 5 | `icon_route_meet-hero` |

The marker at a route's end shows the icon of what the hero does there, as the adventure cursor decides it (`AdventureCursors`):
attack, visit, enter a town, meet an own hero; picking up (a resource, an artifact, a chest) gets its own. A plain move shows no icon,
only the bigger marker.

### 17 · Battle: unit actions (2 × 3) — the battle bar, the cursor

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a sword striking down diagonally from the top right, a short motion arc behind the blade; 2 one arrow
in flight from left to right, rising slightly, three speed lines behind its feathers; 3 an open hand palm up under a small floating open
spell book, a four-rayed spark rising from the pages; 4 a raised round shield seen from the front, a small spark on its upper rim; 5 an
hourglass lying on its side, sand resting in both bulbs; 6 a curved war horn with a strap, three curved sound waves coming from its
mouth
```

| # | File |
|---|---|
| 1 | `icon_battle_melee` |
| 2 | `icon_battle_shoot` |
| 3 | `icon_battle_cast` |
| 4 | `icon_battle_defend` |
| 5 | `icon_battle_wait` |
| 6 | `icon_battle_warcry` |

### 18 · Battle: commands (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI icon sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each icon one bold symbol seen flat from the front,
a solid shape cast in aluminium, light satin metal with a fine grainy bead-blasted surface texture, faint brushed lines, fine scratches,
small dents and worn edges catching the light, a soft bevel and a few fine engraved lines for detail. Pale aluminium palette: light
silvery grey faces with a soft matte sheen, near-white on the bevels and raised edges, soft cool grey only in the recesses and engraved
lines; reflections soft and diffuse, low contrast, the metal reads pale and light overall, never dark, never mirror-bright. One metal
only, cool neutral greys, no other colour, no glow unless a cell names a soft white one. Light from the top left, a thick near-black
outline around each subject and a thin white edge just inside it so it reads on dark and on light ground, no cast shadow, no background.
Simple, solid forms with the same weight and amount of detail on every icon, no thin lines, readable at 24 px. Not a photograph, not a
plastic CGI render, not a cartoon, no pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 a tattered flag on a broken spear shaft, leaning to the left; 2 a sword laid flat across two open,
upturned hands, offered forward; 3 two swords crossed in an X, a lightning bolt running down between them; 4 two swords crossed in an X,
a small cogwheel where they cross
```

| # | File |
|---|---|
| 1 | `icon_battle_retreat` |
| 2 | `icon_battle_surrender` |
| 3 | `icon_battle_quick-combat` |
| 4 | `icon_battle_auto-combat` |

## Tier C · Plain

The symbols are tier B's aluminium, only simpler; the theme tints them for hover, pressed and disabled.

### 19 · Confirm (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI button symbol sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each symbol one bold, simple shape seen
flat from the front, solid cast aluminium, light satin metal with a fine grainy bead-blasted texture and a soft bevel, light silvery
grey faces, near-white on its upper left edges and soft cool grey on its lower right, soft low-contrast reflections, no other colour, no
glow, a thick near-black outline and a thin white edge just inside it, no engraving, no ornament, the same stroke weight on every
symbol, readable at 20 px. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 a tick mark, the short stroke and the long stroke the same
thickness; 2 a circle with one diagonal stroke from top left to bottom right; 3 an X of two short, equal strokes; 4 a small open scroll,
rolled at both ends
```

| # | File |
|---|---|
| 1 | `icon_button_ok` |
| 2 | `icon_button_cancel` |
| 3 | `icon_button_close` |
| 4 | `icon_button_info` |

### 20 · Navigation (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-ALU`

```text
Game UI button symbol sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each symbol one bold, simple shape seen
flat from the front, solid cast aluminium, light satin metal with a fine grainy bead-blasted texture and a soft bevel, light silvery
grey faces, near-white on its upper left edges and soft cool grey on its lower right, soft low-contrast reflections, no other colour, no
glow, a thick near-black outline and a thin white edge just inside it, no engraving, no ornament, the same stroke weight on every
symbol, readable at 20 px. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 a thick arrow pointing left: a short shaft and a wide triangular
head; 2 the same arrow pointing right; 3 the same arrow pointing up; 4 the same arrow pointing down
```

| # | File |
|---|---|
| 1 | `icon_button_back` |
| 2 | `icon_button_forward` |
| 3 | `icon_button_up` |
| 4 | `icon_button_down` |

### 21 · Editing (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI button symbol sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each symbol one bold, simple shape seen
flat from the front, solid cast aluminium, light satin metal with a fine grainy bead-blasted texture and a soft bevel, light silvery
grey faces, near-white on its upper left edges and soft cool grey on its lower right, soft low-contrast reflections, no other colour, no
glow, a thick near-black outline and a thin white edge just inside it, no engraving, no ornament, the same stroke weight on every
symbol, readable at 20 px. Not a photograph, not a plastic CGI render, no pixel art. 6 separate icons arranged in a grid of 3 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 a plus sign with equal arms; 2 a minus sign, as long as the plus
sign's arms; 3 a thick arrow curving up and back to the left, like a hook; 4 the same arrow mirrored, curving to the right; 5 a
magnifying glass with a round lens and a short handle to the lower right, a plus sign in the lens; 6 the same magnifying glass with a
minus sign in the lens
```

| # | File |
|---|---|
| 1 | `icon_button_add` |
| 2 | `icon_button_remove` |
| 3 | `icon_button_undo` |
| 4 | `icon_button_redo` |
| 5 | `icon_button_zoom-in` |
| 6 | `icon_button_zoom-out` |

### 22 · System (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI button symbol sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each symbol one bold, simple shape seen
flat from the front, solid cast aluminium, light satin metal with a fine grainy bead-blasted texture and a soft bevel, light silvery
grey faces, near-white on its upper left edges and soft cool grey on its lower right, soft low-contrast reflections, no other colour, no
glow, a thick near-black outline and a thin white edge just inside it, no engraving, no ornament, the same stroke weight on every
symbol, readable at 20 px. Not a photograph, not a plastic CGI render, no pixel art. 6 separate icons arranged in a grid of 3 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 a closed book seen from the front with a ribbon bookmark hanging
below it; 2 an open book seen from the front, pages fanned; 3 a cogwheel with eight square teeth and a round hole in the middle; 4 a
closed padlock, the shackle down in the body; 5 the same padlock open, the shackle raised and turned aside; 6 three thick horizontal
bars of equal length, evenly spaced
```

| # | File |
|---|---|
| 1 | `icon_button_save` |
| 2 | `icon_button_load` |
| 3 | `icon_button_settings` |
| 4 | `icon_button_lock` |
| 5 | `icon_button_unlock` |
| 6 | `icon_action_menu` |

### 31 · Replay bar (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-ALU`

```text
Game UI button symbol sheet of aluminium glyphs in a realistic painterly dark-fantasy style: each symbol one bold, simple shape seen
flat from the front, solid cast aluminium, light satin metal with a fine grainy bead-blasted texture and a soft bevel, light silvery
grey faces, near-white on its upper left edges and soft cool grey on its lower right, soft low-contrast reflections, no other colour, no
glow, a thick near-black outline and a thin white edge just inside it, no engraving, no ornament, the same stroke weight on every
symbol, readable at 20 px. Not a photograph, not a plastic CGI render, no pixel art. 6 separate icons arranged in a grid of 3 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 two thick upright bars of equal height side by side; 2 a solid
triangle pointing right against one thick upright bar; 3 two solid triangles pointing right, the second touching the first; 4 a solid
triangle pointing right toward a small flag on a pole; 5 a gauntlet seen from the side gripping an upright pole; 6 a second take of cell
5
```

| # | File |
|---|---|
| 1 | `icon_button_pause` |
| 2 | `icon_button_next-turn` |
| 3 | `icon_button_next-day` |
| 4 | `icon_button_skip-to-result` |
| 5 | `icon_button_take-over` |

The replay bar's one Play/Pause toggle shows `icon_button_pause` while playing and the play icon while paused; `icon_button_take-over` is
"Play from here".

## Heraldry

Every town type and every group has a **crest** and a **charge**, one design in two forms:

- **Crest** (`crest_<id>.png`): the charge on a shield in the faction's own colours, painted. For the UI: the town screen, the
  new-game screen, the kingdom overview, information windows, loading and result screens.
- **Charge** (`charge_<id>.png`): the same figure alone, as a flat white shape on a transparent background, used as a mask (its
  alpha). The game paints it on cloth and
  panels in any colour: town banners and hero flags in the **player colour** (the banner meshes are light grey for this, see
  [town-model-prompts.md](town-model-prompts.md)), small markers on the map and the minimap. It must read at **24 px**: one silhouette, no
  inner detail thinner than 8 % of the cell, no lettering.

Generate the crests first, then the charges, whose prompts use the same figures, so both keep one design. The figures come from the lore
and the town models: each repeats its town's landmark. The charge sheets are cut as the other sheets; the game uses the alpha as the mask
and paints the white in the colour it needs.

### Figures

Why each id has its figure (the figure and the crest colours are written into the crest and charge prompts below):

| Id | Why |
|---|---|
| `haven` | the last lit cities, the sun-brazier that "feeds the light" |
| `sylvan` | the old forest dying from the root |
| `academy` | the mages who study, and may want to control, the Wheel |
| `necropolis` | the Wheel cannot be stopped, only ruled |
| `inferno` | every war is harvest |
| `dungeon` | the lightless ones waiting to inherit |
| `stronghold` | the war camp under the beast skull; nothing worth burning |
| `swamp-bestiary` | the beast cults of the mire, the hydra |
| `conflux` | the wardens of the Wheel's mechanism |
| `dragon-utopia` | the dragons who remember the world before the Wheel |
| `group_dawn-covenant` | stop the Wheel; three towns |
| `group_ashen-court` | hasten the turning |
| `group_unbound` | bound to neither side |
| `group_firstborn` | the Wheel's keepers |

### 23 · Crests: Dawn Covenant and Ashen Court (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-PAINTED`

```text
Fantasy heraldry crest sheet in a realistic painterly dark-fantasy style, each crest a heater-shaped shield of real worn metal with a
dark forged iron rim, every shield the same shape, size and angle, seen exactly from the front; one strong figure centred on the field
in the colours named, painted in aged enamel with chips, scratches, small dents and a dull patina, the figure slightly raised so it
catches the light. Naturalistic light from the top left, soft shadows, no drawn outline, no mantling, no helmet, no scroll, no motto,
no text. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own equal square cell, centred with about 10 %
empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a crest
with a sun with straight rays held inside an iron cage of four curved bars, in white field / gold sun, black cage; 2 a crest with a
tree whose crown is leafy on one side and bare branches on the other, its roots forming a circle below, in deep green field / silver
tree; 3 a crest with three nested rings at different tilts (an orrery) around a small eight-pointed star, in sky-blue field / bronze
rings, white star; 4 a crest with a skeletal hand gripping the rim of a spoked wheel, in ash-grey field / bone hand, black wheel, a
cold-green glint; 5 a crest with a curved harvest sickle whose blade is a flame, in black field / ember-red flame-blade, dark iron
handle; 6 a crest with a spider with eight legs whose body is a cut gem, in dark-violet field / black spider, violet gem
```

| # | File |
|---|---|
| 1 | `crest_haven` |
| 2 | `crest_sylvan` |
| 3 | `crest_academy` |
| 4 | `crest_necropolis` |
| 5 | `crest_inferno` |
| 6 | `crest_dungeon` |

### 24 · Crests: Unbound and Firstborn (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-PAINTED`

```text
Fantasy heraldry crest sheet in a realistic painterly dark-fantasy style, each crest a heater-shaped shield of real worn metal with a
dark forged iron rim, every shield the same shape, size and angle, seen exactly from the front; one strong figure centred on the field
in the colours named, painted in aged enamel with chips, scratches, small dents and a dull patina, the figure slightly raised so it
catches the light. Naturalistic light from the top left, soft shadows, no drawn outline, no mantling, no helmet, no scroll, no motto,
no text. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 %
empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a crest
with a horned beast skull, horns sweeping up and out, in red-ochre field (like painted hide) / bone skull; 2 a crest with a coiled
serpent with three heads rising from the coil, in moss-green field / dull-gold serpent; 3 a crest with a cogged wheel split into four
quarters by a cross, in stone-grey field / old-bronze wheel, each quarter tinted faintly (ice blue, ember, pale air, moss); 4 a crest
with a dragon coiled around a rayed sun, wings closed, in dark-bronze field / gold dragon, white sun
```

| # | File |
|---|---|
| 1 | `crest_stronghold` |
| 2 | `crest_swamp-bestiary` |
| 3 | `crest_conflux` |
| 4 | `crest_dragon-utopia` |

### 25 · Crests: the four groups (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-PAINTED`

```text
Fantasy heraldry crest sheet in a realistic painterly dark-fantasy style, each crest a heater-shaped shield of real worn metal with a
dark forged iron rim, every shield the same shape, size and angle, seen exactly from the front; one strong figure centred on the field
in the colours named, painted in aged enamel with chips, scratches, small dents and a dull patina, the figure slightly raised so it
catches the light. Naturalistic light from the top left, soft shadows, no drawn outline, no mantling, no helmet, no scroll, no motto,
no text. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 %
empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a crest
with a sun rising half above a straight horizon line, three long rays, in sunrise-gold field / white sun; 2 a crest with a spoked wheel
wreathed in flame, in ash-grey field / ember-red burning wheel; 3 a crest with a broken chain: two links pulled apart, the loose ends
hanging, in raw-leather brown field / iron chain; 4 a crest with a whole spoked wheel of stone, complete and unbroken, in stone field /
old-bronze wheel
```

| # | File |
|---|---|
| 1 | `crest_group_dawn-covenant` |
| 2 | `crest_group_ashen-court` |
| 3 | `crest_group_unbound` |
| 4 | `crest_group_firstborn` |

### 26 · Charges: Dawn Covenant and Ashen Court (2 × 3)

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Heraldic charge sheet, each figure a single flat pure-white silhouette, no shading, no gradient, no outline, no shield, bold and simple
like a stencil, readable at 24 pixels, every figure filling about 75 % of its cell. 6 separate icons arranged in a grid of 3 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 a sun with straight rays held inside an iron cage of four curved
bars; 2 a tree whose crown is leafy on one side and bare branches on the other, its roots forming a circle below; 3 three nested rings
at different tilts (an orrery) around a small eight-pointed star; 4 a skeletal hand gripping the rim of a spoked wheel; 5 a curved
harvest sickle whose blade is a flame; 6 a spider with eight legs whose body is a cut gem
```

| # | File |
|---|---|
| 1 | `charge_haven` |
| 2 | `charge_sylvan` |
| 3 | `charge_academy` |
| 4 | `charge_necropolis` |
| 5 | `charge_inferno` |
| 6 | `charge_dungeon` |

### 27 · Charges: Unbound and Firstborn (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-PLAIN`

```text
Heraldic charge sheet, each figure a single flat pure-white silhouette, no shading, no gradient, no outline, no shield, bold and simple
like a stencil, readable at 24 pixels, every figure filling about 75 % of its cell. 4 separate icons arranged in a grid of 2 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 a horned beast skull, horns sweeping up and out; 2 a coiled
serpent with three heads rising from the coil; 3 a cogged wheel split into four quarters by a cross; 4 a dragon coiled around a rayed
sun, wings closed
```

| # | File |
|---|---|
| 1 | `charge_stronghold` |
| 2 | `charge_swamp-bestiary` |
| 3 | `charge_conflux` |
| 4 | `charge_dragon-utopia` |

### 28 · Charges: the four groups (2 × 2)

**Prompt** · 1024 × 1024 · negative `NEG-PLAIN`

```text
Heraldic charge sheet, each figure a single flat pure-white silhouette, no shading, no gradient, no outline, no shield, bold and simple
like a stencil, readable at 24 pixels, every figure filling about 75 % of its cell. 4 separate icons arranged in a grid of 2 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 a sun rising half above a straight horizon line, three long rays;
2 a spoked wheel wreathed in flame; 3 a broken chain: two links pulled apart, the loose ends hanging; 4 a whole spoked wheel of stone,
complete and unbroken
```

| # | File |
|---|---|
| 1 | `charge_group_dawn-covenant` |
| 2 | `charge_group_ashen-court` |
| 3 | `charge_group_unbound` |
| 4 | `charge_group_firstborn` |

## After generating

1. Look at each sheet's icons side by side at 32 px: one clear shape each, the same scale and light across the sheet.
2. Look for colour and for the wrong finish: any gold, bronze or coloured glow, a painterly or grimy surface, a cartoon look (thick
   outlines, flat fills, bubbly shapes, plastic shine). Regenerate such a sheet (with its negative where the model takes one) rather than
   fixing it by hand; a slight warm cast may be taken out by desaturating.
3. Put icons of different sheets side by side on a near-black panel: the same metal, brightness and stroke weight.
4. Check mechanical subjects for wheels of different shapes or tilted off their axle, flat side or top-down views, and uneven bow arms; regenerate rather than fix by hand where possible.
5. The icons already in `textures/ui/` (resources, stats, skills, machines, schools, actions, buttons, difficulty) are the earlier
   painted takes in colour: regenerate tiers A, B and C with these prompts; the crests and the charges stay.
6. Put the files in the folder of their group under `src/Godot/EternalWars.Godot.Assets/textures/ui/` and add a line to its
   `CREDITS.md` with the tool and its licence terms.
