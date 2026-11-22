# Hero and town icon prompts (for an image-generating model)

Prompts for the **hero portraits** (the hero list, the hero screen, the tavern's cards, the exchange screen, the information windows)
and the **town icons** (the town list, the kingdom overview, the town screen's title). One portrait for each of the 176 heroes in
`heroes.csv` ([hero-roster.md](../hero-roster.md)): the 128 standard heroes and the 48 campaign heroes; one icon for each of the ten
town types. Generated as **sheets of 4 (2 × 2)** like the icons ([icon-prompts.md](icon-prompts.md)), so the four heroes of a sheet
come out matching. The designs are original.

## Use a prompt

Copy the sheet's **Prompt** block whole and paste the negative block it names (below) where the model takes a negative prompt.
Generate at the size given, cut the sheet and save each cell as the table's File (`.png`); the table rows are the cells in order (top
row left to right, then the bottom row). Each playable town's heroes fill five sheets: warriors 1–4, warriors 5–8, mages 1–4, mages
5–8 and its four campaign heroes; Conflux and Dragon Utopia fill two sheets each. Do not paraphrase a prompt: the wording keeps the
sheets consistent with each other.

## Shared wording

The style paragraph ("Game hero portrait sheet in a realistic…" or "Game town icon sheet in a…"), the town look ("Haven heroes: …"),
and the grid sentence ("4 separate icons arranged in a grid…") are repeated word for word in each prompt so each is ready to paste. A
change to one of them must be made in every prompt that has it: search for its first words.

## Negative prompts

**`NEG-HERO`** (every hero sheet):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, scenery, ground shadow, photograph, plastic CGI render,
pixel art, blurry, top of the head cut off, subjects touching or overlapping, two subjects in one cell, cartoon, comic book, anime,
manga, chibi, cute, toy-like, childish, cel shading, flat colours, flat shading, vector art, clip art, sticker, emoji, mobile game
icon, casual game art, thick black outlines, ink line art, exaggerated proportions, oversized head, big eyes, rounded bubbly shapes,
glossy plastic, smooth airbrushed gradients, oversaturated colours, candy colours, background scenery, frame, full body, two people in
one cell, glamour, clean flawless skin, smooth doll-like face, heroic cartoon jaw, modern clothes, real-world symbols, another game's
character, faded colours, washed out, low contrast, muted colours, grey cast, identical faces, same face, twins, sisters, matching
outfits, one colour scheme for all, everyone in armour, hands, fingers, held objects, books, staves, wands, vials, lanterns, torches,
censers, weapons in hand, half body, bust with wide shoulders, every head turned the same way, head tipped back, looking up at the
sky, face seen from below, short-haired women, women with shaved heads
```

**`NEG-TOWNICON`** (every town sheet):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, scenery, ground shadow, photograph, plastic CGI render,
pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, cartoon, comic book, anime, manga,
chibi, cute, toy-like, childish, cel shading, flat colours, flat shading, vector art, clip art, sticker, emoji, mobile game icon,
casual game art, thick black outlines, ink line art, exaggerated proportions, oversized head, big eyes, rounded bubbly shapes, glossy
plastic, smooth airbrushed gradients, oversaturated colours, candy colours, square ground tile, landscape, horizon, people, banners in
colour, text, front view, toy houses, board-game piece, another game's town
```

## Output for every icon

- **Sheet**: 2 × 2 at 1024 × 1024, every cell 512 × 512, transparent background; cut as the icons.
- **File**: hero portraits `icon_hero_<hero id>.png` with the id from `heroes.csv` (`icon_hero_haven.aldric.png`); town icons
  `icon_town_<town>.png` and `icon_town_<town>_village.png` (a town without a fort, *proposal*). 32-bit RGBA, 512 × 512.
- **Shape**: a portrait reads at 48 px (the hero list): a close crop from the top of the head to the collarbones, the face the
  largest shape and the shoulders only at the bottom edge; nothing held in the hands. A town icon reads at 32 px.
- **Facing**: each cell names its pose, one of each kind per sheet: towards the viewer, turned left, turned right, the chin
  lifted a little (never tipped back, the whole face seen); the light stays top left. The game does not mirror portraits.
- **Backdrop**: a portrait is transparent; the game puts its town's creature backdrop behind it
  ([creature-icon-prompts.md](creature-icon-prompts.md)) *(proposal)*.
- **Telling apart**: every hero is a different person: face shape, jaw, nose, eyes, age and skin tone, then their own clothing style
  and colours (no town colour scheme); only some wear armour, and armour differs in make. One telling detail on the head or collar
  (the last item of its cell, often their preferred creature or specialty). Women are about a third of each town's heroes, and no two
  of them share a face. Women wear their hair medium to long, never cropped or shaved.

## Haven

### Haven · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Haven
heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones, each
dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Aldric Vane, looking straight at the
viewer: a broad-shouldered man in his forties with a square jaw, a broken nose and deep-set grey eyes, weathered olive skin,
close-cropped grey hair, a scar across the chin, a high dented steel gorget over a dark blue padded collar; 2 Maren Holt, in
three-quarter view facing left: a lean road-worn woman in her thirties with a long narrow face, high cheekbones, a sharp straight nose
and pale green eyes, sun-browned freckled skin, a long sandy braid over the shoulder, a mustard-yellow wool hood with a sheepskin
collar; no armour; 3 Corwin Ashby, in three-quarter view facing right: a gaunt young man with hollow cheeks, a hooked nose and
feverish dark eyes, pale skin, a shaved head, burn scars climbing the neck, a coarse brown sackcloth hood and a knotted rope at the
throat; 4 Bertram Stahl, in three-quarter view facing right, the chin lifted only a little, eyes just above level, the whole face
clearly seen: a stocky man in his fifties with a wide flat face, a bulbous nose and small shrewd blue eyes, ruddy skin, a bushy ginger
beard going white, a crimson felt cap with a crossbow-bolt badge and a quilted crimson jacket
```

| # | File |
|---|---|
| W1 | `icon_hero_haven.aldric` |
| W2 | `icon_hero_haven.maren` |
| W3 | `icon_hero_haven.corwin` |
| W4 | `icon_hero_haven.bertram` |

### Haven · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Haven
heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones, each
dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Edric Morrow, in three-quarter view
facing left: an older man with a long gentle face, heavy-lidded tired brown eyes and a grey forked beard, tanned lined skin, an undyed
wool monk's hood thrown back over a mail coif; 2 Sabine Kell, in three-quarter view facing right: a hawk-faced woman in her forties
with a strong aquiline nose, a wide thin mouth and narrow grey-blue eyes, fair northern European skin weathered pink by the wind, dark
chestnut hair to the shoulders, swept back, a black leather riding coat with a collar of tawny griffin feathers; no armour; 3 Gerrit
Hale, in three-quarter view facing left, the chin lifted only a little, eyes just above level, the whole face clearly seen: a
heavy-set jowly man with a thick brown moustache, a round bald head and small bright eyes, pink sunburnt skin, a burnished steel bevor
with gilt rivets and a red silk sash; 4 Osric Brand, facing the viewer, the head tilted slightly: a tall man in his thirties with a
long horse-like face, a cleft chin, pale grey eyes and a thin blond moustache, fair skin, straight shoulder-length blond hair,
blackened plate with gilded edges and a sun etched on the gorget
```

| # | File |
|---|---|
| W5 | `icon_hero_haven.edric` |
| W6 | `icon_hero_haven.sabine` |
| W7 | `icon_hero_haven.gerrit` |
| W8 | `icon_hero_haven.osric` |

### Haven · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Haven
heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones, each
dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Ansel Wren, in three-quarter view facing
right: a thin pale young priest with a narrow pointed chin, large earnest hazel eyes and jug ears, mousy brown hair cut in a tonsure,
an off-white and gold stole over a plain black cassock, a small sun pendant glowing at the throat; 2 Ivo Varro, in three-quarter view
facing right, the chin lifted only a little, eyes just above level, the whole face clearly seen: a soft-faced middle-aged man with a
round double chin, a small upturned nose and calm dark eyes lit faintly gold, light brown skin, thinning black hair oiled back, a
plum-coloured scholar's gown with a white ruff; 3 Matthias Crane, looking straight at the viewer: an old bald priest with a craggy
face, a bulbous nose, pale watery eyes and a long white beard, liver-spotted skin, a coarse grey hermit's cowl, a faint golden glow on
the brow; 4 Rowena Pell, turned slightly to the left: a lean woman in her late twenties with a long narrow face, a high forehead, a
long straight nose, thin lips and clear grey eyes, fair northern European skin, long dark brown hair in a loose braid with a few
strands falling free, a soot smudge and a yellow sulfur stain on one cheekbone, a scorched leather apron collar over a rust-red linen
shirt
```

| # | File |
|---|---|
| M1 | `icon_hero_haven.ansel` |
| M2 | `icon_hero_haven.ivo` |
| M3 | `icon_hero_haven.matthias` |
| M4 | `icon_hero_haven.rowena` |

### Haven · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Haven
heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones, each
dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Fabian Lorne, in three-quarter view
facing left, the chin lifted only a little, eyes just above level, the whole face clearly seen: a severe man in his forties with a
gaunt angular face, a sharp black goatee, thin lips and cold black eyes, sallow skin, slicked-back black hair, an inquisitor's tall
stiff black collar with silver sun clasps; 2 Agathe Sorrel, facing the viewer, the head tilted slightly: a woman in her sixties with a
long weathered face, a prominent Roman nose and deep-set blue eyes under heavy brows, wind-reddened skin, a long iron-grey plait, a
grey wool hood edged with griffin feathers; 3 Cyprian Vey, turned slightly to the left: a calm man in his fifties with an oval face, a
neat silver-grey beard, a straight nose and warm brown eyes, light olive skin, silver temples, a deep green velvet mantle with gold
embroidery, a faint ring of pale gold light behind the head; 4 Elodie Marsh, in three-quarter view facing right, eyes on the viewer: a
young woman of about twenty with a heart-shaped face, a snub nose, wide-set grey eyes and a gap-toothed determined mouth, freckled
pale skin, long tousled copper hair, a sky-blue wool pilgrim's hood over an off-white tabard
```

| # | File |
|---|---|
| M5 | `icon_hero_haven.fabian` |
| M6 | `icon_hero_haven.agathe` |
| M7 | `icon_hero_haven.cyprian` |
| M8 | `icon_hero_haven.elodie` |

### Haven · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Haven
heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones, each
dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Leopold Ardent, looking straight at the
viewer: an old commander in his seventies with a lean noble face, a long thin nose, a white moustache waxed to points and piercing
pale blue eyes, parchment-thin skin, thin white hair combed back, an ornate gilded steel gorget over a royal-blue velvet collar; 2
Isaura Voss, in three-quarter view facing left: a woman in her thirties with a long oval face, a sharp narrow nose, hooded dark eyes
and full lips, deep umber skin, long black braids threaded with gold beads, a deep crimson silk scarf wrapped as a hood, a gold
sun-and-moon earring; 3 Tamsin Rook, in three-quarter view facing right, eyes on the viewer: a wiry woman in her twenties with a
fox-like face, a pointed chin, a crooked smile and quick hazel eyes, tanned freckled skin, long chestnut hair in a loose plait, a
moss-green hooded jerkin with a sun badge at the throat; 4 Benedikt Grau, in three-quarter view facing right, the chin lifted only a
little, eyes just above level, the whole face clearly seen: a corpulent man in his sixties with a heavy round face, a fleshy hooked
nose, a small cruel mouth and pouched pale eyes, pallid skin, a shaved head, a scarlet skullcap and a high scarlet collar with a gold
sun
```

| # | File |
|---|---|
| C1 | `icon_hero_haven.leopold` |
| C2 | `icon_hero_haven.isaura` |
| C3 | `icon_hero_haven.tamsin` |
| C4 | `icon_hero_haven.benedikt` |

## Sylvan

### Sylvan · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Sylvan
heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed in their own
way; long narrow or oval faces, never wide or broad; the men beardless and clean-shaven, no beard, moustache or stubble; some with
slightly East Asian features; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Thalion, in three-quarter view facing left: a male elf in his prime
with a long narrow face, high sharp cheekbones, a thin straight nose and silver-grey eyes, pale ivory skin, long straight silver hair,
a leaf-shaped steel gorget over dark green leather, a tiny glowing pixie at his ear; 2 Faelar, in three-quarter view facing right,
eyes on the viewer: a sturdy male elf with a lean long face, a straight nose bent from an old break, a clean-shaven pointed chin and
amber eyes, ruddy tanned skin, tangled auburn curls to the jaw, a brown fur-trimmed hunting jerkin, a small curled satyr horn on a
cord at the throat; 3 Aerendil, in three-quarter view facing left, the chin lifted only a little, eyes just above level, the whole
face clearly seen: a lithe young male elf with a fine triangular face and slightly East Asian features, straight brows, narrow
almond-shaped violet-grey eyes with a single eyelid and a faint smile, light golden skin, long black hair pulled into a high tail,
leaf tattoos on the neck, a sleeveless grey-green dancer's tunic; 4 Caelwyn, facing the viewer, the head tilted slightly: a young male
elf with a narrow boyish face, a small upturned nose and keen grey eyes, fair freckled skin, short tousled straw-blond hair, a deep
green hood and a mottled brown scout's cloak
```

| # | File |
|---|---|
| W1 | `icon_hero_sylvan.thalion` |
| W2 | `icon_hero_sylvan.faelar` |
| W3 | `icon_hero_sylvan.aerendil` |
| W4 | `icon_hero_sylvan.caelwyn` |

### Sylvan · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Sylvan
heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed in their own
way; long narrow or oval faces, never wide or broad; the men beardless and clean-shaven, no beard, moustache or stubble; some with
slightly East Asian features; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Sylvara, in three-quarter view facing right: a female elf in her
middle years with a long angular face, a strong straight nose, a wide mouth and green-flecked brown eyes, pale rosy skin, copper hair
to the shoulder blades, a russet leather coat with a tall collar and a stag-antler pin; 2 Elaith, in three-quarter view facing right,
the chin lifted only a little, eyes just above level, the whole face clearly seen: a proud male elf with a handsome heavy-lidded face,
a strong jaw, a long aquiline nose and golden eyes, honey-coloured skin, shoulder-length golden hair, a cream silk high-collared coat
with a gold leaf brooch, a white pegasus feather behind the ear; 3 Varis, looking straight at the viewer: a stern older male elf with
a lean lined face and slightly East Asian features, narrow deep-set dark eyes, a thin mouth and a scar through one brow, pale golden
skin, black hair streaked with grey tied in a topknot, a silvered mail collar and a moonstone circlet; 4 Neriah, turned slightly to
the left: a tall strong female elf in her thirties with a long oval face, a firm rounded chin, a fine straight nose, a calm level
mouth and grey-green eyes, fair skin with a light olive tone, thick ash-brown hair in one long braid over the shoulder, carved
pale-wood plate armour at the neck, earrings of pale rose crystal
```

| # | File |
|---|---|
| W5 | `icon_hero_sylvan.sylvara` |
| W6 | `icon_hero_sylvan.elaith` |
| W7 | `icon_hero_sylvan.varis` |
| W8 | `icon_hero_sylvan.neriah` |

### Sylvan · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Sylvan
heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed in their own
way; long narrow or oval faces, never wide or broad; the men beardless and clean-shaven, no beard, moustache or stubble; some with
slightly East Asian features; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Elowen, in three-quarter view facing left, the chin lifted only a
little, eyes just above level, the whole face clearly seen: a young female elf with a small heart-shaped face, large round green eyes
and a pointed chin, milk-pale freckled skin, long wild red hair, a plain undyed linen dress with green embroidery at the neck, a faint
green glow in the eyes; 2 Taeral, facing the viewer, the head tilted slightly: a dreamy male elf with a long oval face, drooping
eyelids, soft full lips and pale blue eyes, light skin, ash-blond hair to the shoulders, a mist-grey hooded robe, faint green motes
drifting around him; 3 Eredan, turned slightly to the left: a gaunt male elf with an ascetic hollow-cheeked face, a long thin nose and
green-gold eyes, sallow olive skin, a shaved head with a leaf tattoo on the scalp, a plain dark brown wrap over one shoulder; 4
Faenor, in three-quarter view facing right: a weather-beaten male elf traveller with a craggy face for an elf, a crooked broken nose,
a clean-shaven jaw and laughing blue eyes, fair skin reddened a little by the wind, wind-tangled light brown hair, a travel-worn
blue-grey cloak
```

| # | File |
|---|---|
| M1 | `icon_hero_sylvan.elowen` |
| M2 | `icon_hero_sylvan.taeral` |
| M3 | `icon_hero_sylvan.eredan` |
| M4 | `icon_hero_sylvan.faenor` |

### Sylvan · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Sylvan
heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed in their own
way; long narrow or oval faces, never wide or broad; the men beardless and clean-shaven, no beard, moustache or stubble; some with
slightly East Asian features; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Ithiel, looking straight at the viewer: a sharp-featured male elf
with a narrow fox-like face and slightly East Asian features, narrow yellow-green eyes with a single eyelid, high flat cheekbones and
thin lips, pale golden skin, straight black hair cropped short, a dark ochre coat, an amber amulet holding a trapped insect, tiny
insects drifting around him; 2 Sariel, in three-quarter view facing left: a serene female elf with a long swan-like neck, a narrow
face, a long straight nose and pale silver eyes, porcelain skin, white-blonde hair in a crown of braids, a mantle of white pegasus
feathers over white robes; 3 Calen, in three-quarter view facing right: a middle-aged male elf with a long narrow face, a heavy brow,
a long straight nose and deep-set hazel eyes, fair weathered skin with patches of bark-like skin on one cheek, clean-shaven,
grey-brown hair tied back, a moss-brown hooded robe; 4 Nymara, in three-quarter view facing right, the chin lifted only a little, eyes
just above level, the whole face clearly seen: a young female elf of about twenty with a narrow delicate face, hollow cheeks and milky
pale eyes, pale translucent skin, long straight silver-white hair, a dark wine-red shawl, grey rot creeping up her neck
```

| # | File |
|---|---|
| M5 | `icon_hero_sylvan.ithiel` |
| M6 | `icon_hero_sylvan.sariel` |
| M7 | `icon_hero_sylvan.calen` |
| M8 | `icon_hero_sylvan.nymara` |

### Sylvan · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Sylvan
heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed in their own
way; long narrow or oval faces, never wide or broad; the men beardless and clean-shaven, no beard, moustache or stubble, except the
ancient Orrindel's long beard; some with slightly East Asian features; leaf motifs only in jewellery or embroidery. 4 separate icons
arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the
same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Lithariel, in three-quarter view
facing left: a regal elderly female elf with a long noble face, high cheekbones, a slightly hooked nose and stern violet eyes, pale
lined skin, long silver hair under a delicate circlet of white-gold leaves, a high collar of deep plum velvet; 2 Rhovan, in
three-quarter view facing right, eyes on the viewer: a rugged male elf with a lean weather-cut face, hollow cheeks, a broken nose, a
clean-shaven jaw and fierce green eyes, pale skin with white scar lines, long wild black hair, loose and unbraided, a wolf-pelt cloak
over bare shoulders; 3 Siannon, in three-quarter view facing left, the chin lifted only a little, eyes just above level, the whole
face clearly seen: a young female elf with a narrow freckled face, a long straight nose, a wide mouth and quick brown eyes, light tan
skin, a faint scar on one cheek, long tousled chestnut hair in a high tail, a hooded tunic of tawny suede; 4 Orrindel, facing the
viewer, the head tilted slightly: a very old male elf with a gnarled long face, bushy white brows, a long hooked nose and tiny bright
green eyes, weathered brown skin, long white hair, a long white beard with one braid threaded with beads (the one bearded Sylvan), a
patched brown hooded cloak
```

| # | File |
|---|---|
| C1 | `icon_hero_sylvan.lithariel` |
| C2 | `icon_hero_sylvan.rhovan` |
| C3 | `icon_hero_sylvan.siannon` |
| C4 | `icon_hero_sylvan.orrindel` |

## Academy

### Academy · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Academy
heroes: the people of the frozen mage-cities, each as their cell says: humans with northern and southern faces, short broad dwarves
(the men bearded), djinn (air spirits, only half human: ageless and always young, smooth faintly glowing blue-toned skin, glowing eyes
without whites, small pointed ears, never any beard, moustache or stubble, shown from the chest up) a naga (a serpent-woman with a
still, emotionless face, bone-white skin and fine scales) and a forged golem; each dressed in their own way; fur, steel, gold and a
pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Corvin Halde, in three-quarter view facing right, eyes on the viewer: a soot-smudged man in
his thirties with a long rubbery face, a big crooked nose, bushy black eyebrows and quick dark eyes, sallow skin, a mop of greasy
black hair, a brown leather work coat with brass buttons; 2 Stellan Frost, in three-quarter view facing right, the chin lifted only a
little, eyes just above level, the whole face clearly seen: a dwarf in his sixties, short and very broad, with a massive square head,
a frost-white beard braided down to the chest, a flattened nose and small ice-blue eyes, ruddy wind-burned skin, fur-lined steel plate
with gold trim at the neck; 3 Ingrid Vael, looking straight at the viewer: a hard-faced woman in her fifties with a broad jaw, high
flat cheekbones, a small nose and pale grey eyes, weathered pale skin, iron-grey braids coiled over the ears, a stone-grey wool cloak
with a carved granite clasp; 4 Talos, turned slightly to the left: a golem forged of dark iron and riveted bronze plates, a solemn
mask-like face of hammered iron with a heavy brow and a straight seam for a mouth, two ice-blue rune eyes, a pale-blue rune glowing on
the brow, a gorget of overlapping iron plates, soot and old scorch marks; a forged statue, not a robot, no machinery
```

| # | File |
|---|---|
| W1 | `icon_hero_academy.corvin` |
| W2 | `icon_hero_academy.stellan` |
| W3 | `icon_hero_academy.ingrid` |
| W4 | `icon_hero_academy.talos` |

### Academy · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Academy
heroes: the people of the frozen mage-cities, each as their cell says: humans with northern and southern faces, short broad dwarves
(the men bearded), djinn (air spirits, only half human: ageless and always young, smooth faintly glowing blue-toned skin, glowing eyes
without whites, small pointed ears, never any beard, moustache or stubble, shown from the chest up) a naga (a serpent-woman with a
still, emotionless face, bone-white skin and fine scales) and a forged golem; each dressed in their own way; fur, steel, gold and a
pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Henrik Strom, in three-quarter view facing left, the chin lifted only a little, eyes just
above level, the whole face clearly seen: a lean hunter in his forties with a narrow wolfish face, a long pointed nose, icy blue eyes
and a short grizzled beard, frost-reddened skin, a white fur hood; 2 Kasimir Lode, facing the viewer, the head tilted slightly: a
portly dwarf in his fifties with a round jowly face, a small pursed mouth, a forked grey beard bound with gold rings and shrewd little
eyes, rosy skin, a balding head, a heavy black fur coat and a gold chain of office; 3 Raziya, turned slightly to the left: a female
djinn, a young ageless air spirit with a long fierce face, high sharp cheekbones, a narrow nose and glowing storm-grey eyes without
whites, smooth faintly glowing storm-grey-blue skin, long black hair streaming upward as if in a wind, a royal-blue silk vest edged in
gold, a broken gold chain round the neck; 4 Dorian Quill, in three-quarter view facing right, eyes on the viewer: a sharp-eyed man in
his thirties with a lean triangular face, a pencil moustache, an aquiline nose and narrow black eyes, light brown skin, slicked black
hair, a royal-blue naval coat with a high gold-braided collar
```

| # | File |
|---|---|
| W5 | `icon_hero_academy.henrik` |
| W6 | `icon_hero_academy.kasimir` |
| W7 | `icon_hero_academy.raziya` |
| W8 | `icon_hero_academy.dorian` |

### Academy · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Academy
heroes: the people of the frozen mage-cities, each as their cell says: humans with northern and southern faces, short broad dwarves
(the men bearded), djinn (air spirits, only half human: ageless and always young, smooth faintly glowing blue-toned skin, glowing eyes
without whites, small pointed ears, never any beard, moustache or stubble, shown from the chest up) a naga (a serpent-woman with a
still, emotionless face, bone-white skin and fine scales) and a forged golem; each dressed in their own way; fur, steel, gold and a
pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Aurelian Vex, looking straight at the viewer: a young man with a soft round face, dimpled
cheeks, a short wide nose and bright curious brown eyes, pale skin with a faint warm tone, a mass of dark curls, a deep blue student's
gown; 2 Seraphine Lux, in three-quarter view facing left: a composed woman in her forties with a long oval face, a narrow nose, thin
arched brows and pale ice-blue eyes, very fair skin, straight silver hair to the shoulders, a white ermine collar, a pale-blue rune
glowing on the brow; 3 Tariq, in three-quarter view facing right: a young ageless male djinn with a long serene face, high arched
brows, a fine straight nose and calm glowing gold eyes, smooth faintly glowing deep sapphire-blue skin, beardless, a midnight-blue
turban with a gold star pin, a small gold hourglass amulet; 4 Ivar Marrow, in three-quarter view facing right, the chin lifted only a
little, eyes just above level, the whole face clearly seen: a severe middle-aged man with a long gaunt face, a sharp widow's peak of
black hair, a narrow nose and one eye behind a dark-glass monocle, grey pallid skin, a black high-necked coat with steel buttons
```

| # | File |
|---|---|
| M1 | `icon_hero_academy.aurelian` |
| M2 | `icon_hero_academy.seraphine` |
| M3 | `icon_hero_academy.tariq` |
| M4 | `icon_hero_academy.ivar` |

### Academy · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Academy
heroes: the people of the frozen mage-cities, each as their cell says: humans with northern and southern faces, short broad dwarves
(the men bearded), djinn (air spirits, only half human: ageless and always young, smooth faintly glowing blue-toned skin, glowing eyes
without whites, small pointed ears, never any beard, moustache or stubble, shown from the chest up) a naga (a serpent-woman with a
still, emotionless face, bone-white skin and fine scales) and a forged golem; each dressed in their own way; fur, steel, gold and a
pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Qadir, in three-quarter view facing left: a young ageless male djinn with a lean hatchet
face, sharp cheekbones, a long straight nose and glowing frost-white eyes, smooth faintly glowing pale icy-blue skin rimed with frost,
beardless, white hair swept back like blown snow, a white fur mantle, a shard of ice floating beside the face; 2 Sesha, in
three-quarter view facing right: a naga, a serpent-woman of the Academy's guard, ageless, with a long perfectly still face that shows
no feeling at all, high smooth cheekbones, a small straight nose and a thin unmoving mouth, unblinking pale gold eyes with slit
pupils, super pale bone-white skin with fine white scales at the temples and down the throat, long straight black hair bound high with
gold rings, a collar of overlapping steel and gold scales; 3 Layth, in three-quarter view facing left, the chin lifted only a little,
eyes just above level, the whole face clearly seen: a young ageless male djinn with a long bald skull, hollow cheeks, a thin hooked
nose and deep-set eyes glowing white, smooth faintly glowing deep indigo skin traced with pale-blue runes, beardless, gold rings in
both pointed ears, a charcoal-grey silk collar; 4 Valeska Rune, facing the viewer, the head tilted slightly: a proud woman in her
fifties with a long angular face, a strong jaw, a high-bridged nose and hooded dark eyes, olive skin, black hair streaked with white
in a tall coil, a circlet set with rose-red crystal shards, a dark sable collar
```

| # | File |
|---|---|
| M5 | `icon_hero_academy.qadir` |
| M6 | `icon_hero_academy.sesha` |
| M7 | `icon_hero_academy.layth` |
| M8 | `icon_hero_academy.valeska` |

### Academy · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Academy
heroes: the people of the frozen mage-cities, each as their cell says: humans with northern and southern faces, short broad dwarves
(the men bearded), djinn (air spirits, only half human: ageless and always young, smooth faintly glowing blue-toned skin, glowing eyes
without whites, small pointed ears, never any beard, moustache or stubble, shown from the chest up) a naga (a serpent-woman with a
still, emotionless face, bone-white skin and fine scales) and a forged golem; each dressed in their own way; fur, steel, gold and a
pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Magnus Hollin, in three-quarter view facing right: a tall imposing old human man with a long
lion-like face, a mane of white hair and a long white beard, a broad flat nose and fierce gold-brown eyes, ruddy skin, a deep crimson
robe with a heavy gold collar; 2 Halvor Brenn, in three-quarter view facing right, the chin lifted only a little, eyes just above
level, the whole face clearly seen: a human man in his late forties with a long lean face, a high forehead, a straight nose and calm
grey-green eyes, fair weathered skin, short dark-brown hair greying at the temples, a close-trimmed greying beard, a slate-grey
travelling coat with a fur collar and a silver rune clasp; 3 Edvard Lunde, looking straight at the viewer: a burly bald dwarf smith in
his fifties with a heavy brow ridge, a broken nose, a thick black beard tucked into his collar and dark eyes, swarthy skin marked by
small burn scars, a brown leather smith's collar; 4 Nadia, turned slightly to the left: a young female djinn with a narrow elfin face,
a sharp chin, large glowing dark-blue eyes without whites and thick straight brows, smooth faintly glowing sky-blue skin, long black
hair in a single braid, a royal-blue high-collared jacket
```

| # | File |
|---|---|
| C1 | `icon_hero_academy.magnus` |
| C2 | `icon_hero_academy.halvor` |
| C3 | `icon_hero_academy.edvard` |
| C4 | `icon_hero_academy.nadia` |

## Necropolis

### Necropolis · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Necropolis heroes: the dead and the few living who serve them, each as their cell says: living human necromancers, vampires (ageless,
handsome and cruel, bloodless white skin, red or pale gold eyes, small fangs), liches (bare skulls with cold fire in the sockets,
crowns and rotting finery, no flesh, no skin, shown from the chest up), a ghost and a wraith (translucent, faintly glowing, fading at
the edges) and other risen dead, as a ghoul, plague corpses, a skeleton and a death knight; each dressed in their own way; a cold
green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell,
centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines,
no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then
bottom row: 1 Morthal, in three-quarter view facing left, the chin lifted only a little, eyes just above level, the whole face clearly
seen: a lich, a broad heavy skull of old brown bone with a cracked brow, cold green fire in the eye sockets, a few strands of white
hair clinging to it, a rusted black steel gorget and a rotting black mantle; 2 Vexa, facing the viewer, the head tilted slightly: a
plague corpse, a risen woman with a narrow pointed face, a long thin nose and clouded red-rimmed eyes, sickly yellow-grey rotting skin
with open sores, lank shoulder-length black hair, a beaked leather plague mask pushed up on the forehead, green fumes curling at the
collar of a stained oilskin coat; 3 Grimwald, turned slightly to the left: a ghoul, a hulking risen warrior with a huge square head,
grey-blue dead skin, a heavy brow, one milky eye, a lipless mouth of sharp yellow teeth and a jaw held on by an iron brace, a torn
leather collar; 4 Ulric Ashgrave, in three-quarter view facing right, eyes on the viewer: a vampire aristocrat with a gaunt long
horse-like face, a pointed grey beard, a high forehead and heavy-lidded pale gold eyes, small fangs, bloodless grey-white skin, a
collar of black spider-web lace on a burgundy velvet doublet
```

| # | File |
|---|---|
| W1 | `icon_hero_necropolis.morthal` |
| W2 | `icon_hero_necropolis.vexa` |
| W3 | `icon_hero_necropolis.grimwald` |
| W4 | `icon_hero_necropolis.ulric` |

### Necropolis · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Necropolis heroes: the dead and the few living who serve them, each as their cell says: living human necromancers, vampires (ageless,
handsome and cruel, bloodless white skin, red or pale gold eyes, small fangs), liches (bare skulls with cold fire in the sockets,
crowns and rotting finery, no flesh, no skin, shown from the chest up), a ghost and a wraith (translucent, faintly glowing, fading at
the edges) and other risen dead, as a ghoul, plague corpses, a skeleton and a death knight; each dressed in their own way; a cold
green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell,
centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines,
no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then
bottom row: 1 Hespera, looking straight at the viewer: a vampire woman with a sharp heart-shaped face, a small cruel mouth, a narrow
nose and burning red eyes, alabaster skin, long sleek black hair, a high black lacquered collar with a ruby pin; 2 Corvane, in
three-quarter view facing left: a wraith, a hooded shape of grey shadow and tattered shroud, the gaunt translucent face of a dead
knight with a black beard, hollow cheeks and cold green burning eyes, faintly glowing at the edges and fading into smoke below, the
ghost of black mail at the throat; 3 Drusus, in three-quarter view facing right, eyes on the viewer: a death knight, a dead warrior in
his prime with a broad face, a heavy jaw, a flattened boxer's nose and pale dead eyes glowing faint green, ash-grey dead skin cracked
at the cheek, a shaved scarred head, black plate with a horned gorget; 4 Malgrin, in three-quarter view facing right, the chin lifted
only a little, eyes just above level, the whole face clearly seen: a lich warrior king, a long bare skull of cracked yellow bone,
green fire in the eye sockets, a crown of black iron, a rotting brown fur mantle over old black mail
```

| # | File |
|---|---|
| W5 | `icon_hero_necropolis.hespera` |
| W6 | `icon_hero_necropolis.corvane` |
| W7 | `icon_hero_necropolis.drusus` |
| W8 | `icon_hero_necropolis.malgrin` |

### Necropolis · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Necropolis heroes: the dead and the few living who serve them, each as their cell says: living human necromancers, vampires (ageless,
handsome and cruel, bloodless white skin, red or pale gold eyes, small fangs), liches (bare skulls with cold fire in the sockets,
crowns and rotting finery, no flesh, no skin, shown from the chest up), a ghost and a wraith (translucent, faintly glowing, fading at
the edges) and other risen dead, as a ghoul, plague corpses, a skeleton and a death knight; each dressed in their own way; a cold
green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell,
centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines,
no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then
bottom row: 1 Necrys, in three-quarter view facing left: a living human necromancer in his fifties with a sunken narrow face, a long
hooked nose and deep-set dark eyes with a faint green light in them, waxy sallow skin, a short grey beard, a black wool hood; 2
Sepulka, in three-quarter view facing right, eyes on the viewer: a living human grave-witch, an old woman with a wide wrinkled
toad-like face, a flat nose, a toothless grin and tiny black eyes, earthy brown skin, wild grey hair, grave dirt on the face, a
necklace of finger bones over a patched brown shawl; 3 Vorlath, in three-quarter view facing left, the chin lifted only a little, eyes
just above level, the whole face clearly seen: a lich, a narrow pointed skull of grey bone with a missing lower jaw, sickly violet
fire in the eye sockets, a high-collared dark violet coat gone to rags, a twisted iron charm at the throat; 4 Ysmera, facing the
viewer, the head tilted slightly: a young vampire woman with a long oval face, a delicate nose, large silvery eyes and silver tears of
mercury on the cheeks, small fangs, bloodless white skin, long straight ash-blonde hair, a black lace mourning veil, a silver spider
brooch
```

| # | File |
|---|---|
| M1 | `icon_hero_necropolis.necrys` |
| M2 | `icon_hero_necropolis.sepulka` |
| M3 | `icon_hero_necropolis.vorlath` |
| M4 | `icon_hero_necropolis.ysmera` |

### Necropolis · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Necropolis heroes: the dead and the few living who serve them, each as their cell says: living human necromancers, vampires (ageless,
handsome and cruel, bloodless white skin, red or pale gold eyes, small fangs), liches (bare skulls with cold fire in the sockets,
crowns and rotting finery, no flesh, no skin, shown from the chest up), a ghost and a wraith (translucent, faintly glowing, fading at
the edges) and other risen dead, as a ghoul, plague corpses, a skeleton and a death knight; each dressed in their own way; a cold
green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell,
centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines,
no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then
bottom row: 1 Kael Duskmire, in three-quarter view facing right: a vampire noble with a chiselled long face, a widow's peak of slicked
dark hair, a thin smile showing fangs and pale gold eyes, marble-white skin, a crimson silk cravat and a black velvet collar; 2 Lethe,
in three-quarter view facing right, the chin lifted only a little, eyes just above level, the whole face clearly seen: a translucent
ghostly woman with a soft round young face, wide sad eyes and parted lips, glowing blue-white skin, long hair floating as if under
water, a drowned girl's lace collar; 3 Ozmund, looking straight at the viewer: a plague corpse, a bloated dead man with a fat round
face, sagging grey jowls, a rotting pocked nose and clouded bloodshot eyes, greenish blotched skin, a few strands of hair, stained
mustard-yellow plague robes, a black rat on his shoulder; 4 Thessaly, turned slightly to the left: a lich, a narrow elegant skull with
jewels set in the cheekbones, violet fire in the sockets, a tall jagged crown of tarnished silver, a glowing green phylactery on a
chain at the throat
```

| # | File |
|---|---|
| M5 | `icon_hero_necropolis.kael` |
| M6 | `icon_hero_necropolis.lethe` |
| M7 | `icon_hero_necropolis.ozmund` |
| M8 | `icon_hero_necropolis.thessaly` |

### Necropolis · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Necropolis heroes: the dead and the few living who serve them, each as their cell says: living human necromancers, vampires (ageless,
handsome and cruel, bloodless white skin, red or pale gold eyes, small fangs), liches (bare skulls with cold fire in the sockets,
crowns and rotting finery, no flesh, no skin, shown from the chest up), a ghost and a wraith (translucent, faintly glowing, fading at
the edges) and other risen dead, as a ghoul, plague corpses, a skeleton and a death knight; each dressed in their own way; a cold
green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell,
centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines,
no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then
bottom row: 1 Maladrea, in three-quarter view facing left, the chin lifted only a little, eyes just above level, the whole face
clearly seen: a vampire matriarch, tall and regal, of no clear age, with a long angular face, sharp high cheekbones, a hooked nose and
red eyes, small fangs, bloodless chalk-white skin, black hair swept up under a tall bone headdress, a stiff collar of black and
silver; 2 Aldous Wraithe, facing the viewer, the head tilted slightly: a vampire in his fifties with a broad scarred face, a broken
nose, a clipped grey beard and red eyes, grey-white skin, a cropped grey head, a faded white surcoat collar with a scorched sun
emblem; 3 Ravenna, turned slightly to the left: a skeleton duellist, a fine narrow skull of polished white bone, a cracked cheekbone,
cold pale-grey lights in the eye sockets, long white hair still clinging to the skull and tied back, a black leather gorget studded
with small bones; 4 Ossian Grael, in three-quarter view facing right: a living human gravekeeper, a hunched ancient man with a long
drooping face, a huge hooked nose, rheumy grey eyes and a wispy white beard, liver-spotted skin, a bald head under a moth-eaten brown
cap
```

| # | File |
|---|---|
| C1 | `icon_hero_necropolis.maladrea` |
| C2 | `icon_hero_necropolis.aldous` |
| C3 | `icon_hero_necropolis.ravenna` |
| C4 | `icon_hero_necropolis.ossian` |

## Inferno

### Inferno · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Inferno
heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze, glowing ember
seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal
square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell.
No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left
to right, then bottom row: 1 Azgaroth, looking straight at the viewer: a horned demon lord with a long wedge-shaped face, curling
black horns, a heavy brow and ember-orange eyes, scarred black skin with glowing seams, a high collar of blackened bronze, a small imp
perched by his ear; 2 Brakka, in three-quarter view facing left: a brutish demon with a wide ape-like face, a jutting tusked jaw,
heavy curled ram horns and small red eyes, brick-red hide, slag-plate armour at the neck; 3 Vorhaz, in three-quarter view facing
right: a lean demon with a narrow jackal face, swept-back thin horns, yellow eyes and smoke curling from the mouth, ash-grey skin, the
edge of a hound-skull pauldron; 4 Skarn, in three-quarter view facing right, the chin lifted only a little, eyes just above level, the
whole face clearly seen: a demon with a gaunt hollow face, no nose, a lipless grin of needle teeth and white eyes, sickly bronze skin
pierced by iron nails, two hooked horns pointing forward
```

| # | File |
|---|---|
| W1 | `icon_hero_inferno.azgaroth` |
| W2 | `icon_hero_inferno.brakka` |
| W3 | `icon_hero_inferno.vorhaz` |
| W4 | `icon_hero_inferno.skarn` |

### Inferno · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Inferno
heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze, glowing ember
seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal
square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell.
No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left
to right, then bottom row: 1 Ixira, in three-quarter view facing left: a female demon with a sharp angular face, long swept-back
horns, high cheekbones and slit gold eyes, deep crimson skin, long black hair, the tops of folded black wings at the edges; 2 Malphor,
in three-quarter view facing right, eyes on the viewer: a grinning demon with a broad toad-like face, gold teeth, gold rings in short
stubby horns and greedy small eyes, orange-brown skin, a gold-trimmed black brocade collar; 3 Zarduk, in three-quarter view facing
left, the chin lifted only a little, eyes just above level, the whole face clearly seen: a huge demon with a bull-skull face, wide
horns and smouldering eye sockets, charred black hide, chains across the chest; 4 Kezrath, facing the viewer, the head tilted
slightly: a lean demon with a hawk-like face, a beaked nose, two short straight horns and burning white eyes, ash-grey skin with red
seams, a hood of chain mail
```

| # | File |
|---|---|
| W5 | `icon_hero_inferno.ixira` |
| W6 | `icon_hero_inferno.malphor` |
| W7 | `icon_hero_inferno.zarduk` |
| W8 | `icon_hero_inferno.kezrath` |

### Inferno · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Inferno
heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze, glowing ember
seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal
square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell.
No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left
to right, then bottom row: 1 Belzra, in three-quarter view facing right, eyes on the viewer: a female demon with a round impish face,
short goat horns, a wide grin and bright orange eyes, sooty grey skin, a wild mane of black hair, a ball of fire floating beside the
cheek; 2 Nyxoth, in three-quarter view facing right, the chin lifted only a little, eyes just above level, the whole face clearly
seen: a demon with a smooth eyeless face of glassy black skin cracked with glowing ember-red runes, two long backswept horns, a high
collar of dark iron; 3 Ashmedai, looking straight at the viewer: a tall regal demon with a long elegant face, a straight nose, cold
amused violet eyes and a crown of flame between ridged horns, smooth bronze skin, a black silk collar; 4 Lilivex, turned slightly to
the left: a female demon with a narrow sly face, a pointed chin, a sly smile and heavy-lidded green-gold eyes, dusky purple skin,
small curled horns, long dark hair, a mask of iron nails pushed aside onto the forehead
```

| # | File |
|---|---|
| M1 | `icon_hero_inferno.belzra` |
| M2 | `icon_hero_inferno.nyxoth` |
| M3 | `icon_hero_inferno.ashmedai` |
| M4 | `icon_hero_inferno.lilivex` |

### Inferno · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Inferno
heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze, glowing ember
seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal
square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell.
No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left
to right, then bottom row: 1 Pyrrhon, in three-quarter view facing left, the chin lifted only a little, eyes just above level, the
whole face clearly seen: a demon with a long gaunt face, eyes of fire, a fanged mouth and three short horns on the brow, cracked
charcoal skin, a screaming pale mask hung at the chest; 2 Veshra, facing the viewer, the head tilted slightly: a female demon with a
long horse-like face, a mane of fire, large dark eyes and flared nostrils, smoky grey skin, a horse skull as a shoulder guard; 3
Orzhul, turned slightly to the left: an old demon with a wrinkled long face, drooping horns, squinting ember eyes and a long beard of
flame, cracked grey-brown skin; 4 Sythra, in three-quarter view facing right, eyes on the viewer: a female demon with a broad strong
face, a wide mouth, a hooked nose and gold eyes, dark bronze skin, curving gold-tipped horns, long black hair, gold coins melted into
her collar
```

| # | File |
|---|---|
| M5 | `icon_hero_inferno.pyrrhon` |
| M6 | `icon_hero_inferno.veshra` |
| M7 | `icon_hero_inferno.orzhul` |
| M8 | `icon_hero_inferno.sythra` |

### Inferno · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Inferno
heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze, glowing ember
seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal
square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell.
No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left
to right, then bottom row: 1 Vazgoroth, looking straight at the viewer: a colossal ancient demon with a broad flat face, four huge
horns, a cleft chin and molten gold eyes, obsidian-black skin webbed with white-hot cracks, a crown of iron spikes; 2 Ishara, in
three-quarter view facing left: a human woman in her forties with a long proud face, a strong nose, dark kohl-lined eyes and full
lips, deep brown skin with a glowing brand on the brow, tiny horns budding at the temples, a crimson silk headwrap; 3 Grizhak, in
three-quarter view facing right: a squat demon with a broad pug face, a split upper lip, broken horn stumps and small mean orange
eyes, rusty brown hide covered in bite scars, a spiked leather collar; 4 Zethrax, in three-quarter view facing right, the chin lifted
only a little, eyes just above level, the whole face clearly seen: a thin bookish demon with a long narrow face, small round
spectacles of smoked glass, a thin moustache of smoke and amber eyes, pale ash-grey skin, a pair of thin spiral horns, a high-buttoned
black coat
```

| # | File |
|---|---|
| C1 | `icon_hero_inferno.vazgoroth` |
| C2 | `icon_hero_inferno.ishara` |
| C3 | `icon_hero_inferno.grizhak` |
| C4 | `icon_hero_inferno.zethrax` |

## Dungeon

### Dungeon · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Dungeon
heroes: the peoples of the deep, each as their cell says: dark elves (long pointed ears, skin from ash-grey to slate-blue and
charcoal, hair from white to silver), minotaurs (huge bull-headed men with real bull's heads, muzzles, horns and short hide, nothing
human in the face, shown from the chest up), troglodytes (hunched cave lizard-folk with flat wide snouts, smooth eyeless faces, pale
grey scales and no hair), a medusa (a serpent-woman with living snakes for hair) and humans of the surface; each dressed in their own
way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square
cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid
lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to
right, then bottom row: 1 Vaelrith, in three-quarter view facing left: a troglodyte warrior in his prime, a lean hunched lizard-man
with a flat wide snout full of small sharp teeth, a smooth eyeless face, pale grey scales darker along the brow, a ridge of short
spines over the skull, a harness of black leather and bone at the neck under a cloak of pale cave-lizard hide; 2 Zhaun, in
three-quarter view facing right: a young male dark elf with a small sharp face, a pointed chin and darting violet eyes, slate-blue
skin, short spiky white hair, a black scout's hood, a thin silver band across the brow; 3 Ilvara, in three-quarter view facing left,
the chin lifted only a little, eyes just above level, the whole face clearly seen: a medusa, a serpent-woman with a broad strong face,
high hard cheekbones, a wide thin-lipped mouth and cold yellow eyes with slit pupils, grey-olive skin turning to fine dark scales on
the throat and shoulders, a crown of living hissing snakes instead of hair, a black leather collar with a silver snake clasp; 4
Kharis, facing the viewer, the head tilted slightly: a massive minotaur warrior in his prime with a broad dark-brown bull's head, a
heavy wet muzzle with a bronze ring through the nose, small dull red eyes, long curved horns capped with iron, a thick neck, a
battered black steel gorget
```

| # | File |
|---|---|
| W1 | `icon_hero_dungeon.vaelrith` |
| W2 | `icon_hero_dungeon.zhaun` |
| W3 | `icon_hero_dungeon.ilvara` |
| W4 | `icon_hero_dungeon.kharis` |

### Dungeon · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Dungeon
heroes: the peoples of the deep, each as their cell says: dark elves (long pointed ears, skin from ash-grey to slate-blue and
charcoal, hair from white to silver), minotaurs (huge bull-headed men with real bull's heads, muzzles, horns and short hide, nothing
human in the face, shown from the chest up), troglodytes (hunched cave lizard-folk with flat wide snouts, smooth eyeless faces, pale
grey scales and no hair), a medusa (a serpent-woman with living snakes for hair) and humans of the surface; each dressed in their own
way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square
cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid
lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to
right, then bottom row: 1 Shyrran, in three-quarter view facing right: a male dark elf rider with a lean wolfish face, a hooked nose,
a long scar down one cheek and silver eyes, blue-grey skin, white hair in a long braid, armour of black lizard scales at the neck; 2
Vorn, in three-quarter view facing right, the chin lifted only a little, eyes just above level, the whole face clearly seen: a heavy
old minotaur with a grizzled grey-black bull's head, a scarred sagging muzzle, a cold heavy-lidded stare and small gold eyes, thick
horns hung with gold rings, heavy gold rings in both ears and a broad gold collar; 3 Lyrthas, looking straight at the viewer: a male
dark elf with an angular cruel face, thin lips, a long sharp nose and pale violet eyes, dark slate skin, silver hair shaved on one
side, a helm plume shaped like a scorpion's tail; 4 Xundar, turned slightly to the left: an old male dark elf with a gaunt lined face,
a bent nose, one milky eye and one violet eye, grey skin, a long thin white beard, an amulet of one great violet eye at the throat
```

| # | File |
|---|---|
| W5 | `icon_hero_dungeon.shyrran` |
| W6 | `icon_hero_dungeon.vorn` |
| W7 | `icon_hero_dungeon.lyrthas` |
| W8 | `icon_hero_dungeon.xundar` |

### Dungeon · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Dungeon
heroes: the peoples of the deep, each as their cell says: dark elves (long pointed ears, skin from ash-grey to slate-blue and
charcoal, hair from white to silver), minotaurs (huge bull-headed men with real bull's heads, muzzles, horns and short hide, nothing
human in the face, shown from the chest up), troglodytes (hunched cave lizard-folk with flat wide snouts, smooth eyeless faces, pale
grey scales and no hair), a medusa (a serpent-woman with living snakes for hair) and humans of the surface; each dressed in their own
way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square
cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid
lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to
right, then bottom row: 1 Nethra, in three-quarter view facing left, the chin lifted only a little, eyes just above level, the whole
face clearly seen: a female troglodyte seer, slight and hunched, with a narrow lizard snout, a smooth eyeless face half under a sheer
black veil, pale ash-grey scales with a faint pearly sheen, thin violet runes painted down the snout, strings of silver wire and bone
beads at the throat; 2 Sabal, facing the viewer, the head tilted slightly: a male dark elf with a broad smooth face, a wide flat nose
and narrow amber eyes, deep charcoal skin, a shaved head, violet tattoos glowing along the jaw; 3 Ghaunis, turned slightly to the
left: a female dark elf in her later years with a gaunt narrow face, hollow cheeks and pale lilac eyes, ash-grey skin, long ragged
white hair, half the face wrapped in drifting shadow smoke; 4 Zesrin, in three-quarter view facing right, eyes on the viewer: a young
minotaur shaman with a narrow reddish-brown bull's head, short new horns painted with violet sigils, large dark violet eyes, a silver
ring through one ear, a deep red silk collar
```

| # | File |
|---|---|
| M1 | `icon_hero_dungeon.nethra` |
| M2 | `icon_hero_dungeon.sabal` |
| M3 | `icon_hero_dungeon.ghaunis` |
| M4 | `icon_hero_dungeon.zesrin` |

### Dungeon · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Dungeon
heroes: the peoples of the deep, each as their cell says: dark elves (long pointed ears, skin from ash-grey to slate-blue and
charcoal, hair from white to silver), minotaurs (huge bull-headed men with real bull's heads, muzzles, horns and short hide, nothing
human in the face, shown from the chest up), troglodytes (hunched cave lizard-folk with flat wide snouts, smooth eyeless faces, pale
grey scales and no hair), a medusa (a serpent-woman with living snakes for hair) and humans of the surface; each dressed in their own
way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square
cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid
lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to
right, then bottom row: 1 Dryel, looking straight at the viewer: a human man of the surface in his forties with a long grief-worn
face, drooping dark eyes shedding violet tears and a thin black beard, pale sickly skin, lank black hair, a black mourning collar; 2
Vhaeris, in three-quarter view facing left: a female dark-elf high priestess with a long narrow face, a sharp chin and cold white
eyes, slate-black skin, long white hair under a tall silver headdress like spider legs; 3 Phyrra, in three-quarter view facing right,
eyes on the viewer: a young female dark elf with a wild angular face, a wide grin and crackling bright violet eyes, light ash skin,
long white hair lifting in the air, black lightning crackling about her head; 4 Ilphar, in three-quarter view facing right, the chin
lifted only a little, eyes just above level, the whole face clearly seen: an old troglodyte with a broad blunt snout, deep wrinkles
across the smooth eyeless face, mottled grey scales, a crest of worn broken spines, a bald scarred skull, an amulet of many small
glass eyes
```

| # | File |
|---|---|
| M5 | `icon_hero_dungeon.dryel` |
| M6 | `icon_hero_dungeon.vhaeris` |
| M7 | `icon_hero_dungeon.phyrra` |
| M8 | `icon_hero_dungeon.ilphar` |

### Dungeon · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Dungeon
heroes: the peoples of the deep, each as their cell says: dark elves (long pointed ears, skin from ash-grey to slate-blue and
charcoal, hair from white to silver), minotaurs (huge bull-headed men with real bull's heads, muzzles, horns and short hide, nothing
human in the face, shown from the chest up), troglodytes (hunched cave lizard-folk with flat wide snouts, smooth eyeless faces, pale
grey scales and no hair), a medusa (a serpent-woman with living snakes for hair) and humans of the surface; each dressed in their own
way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square
cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid
lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to
right, then bottom row: 1 Malaroth, in three-quarter view facing left: a towering old minotaur lord with a massive coal-black bull's
head, a scar across the muzzle, deep-set red eyes, a grey-flecked mane down the neck, one horn broken short and capped with silver, a
collar of black horn plates; 2 Seyrissa, in three-quarter view facing right, eyes on the viewer: a female dark elf with a small round
face, a snub nose, full lips and a row of tiny silver studs above each brow, deep violet-grey skin, white hair in two thick braids, a
high collar of black spider silk; 3 Kaelvorn, in three-quarter view facing left, the chin lifted only a little, eyes just above level,
the whole face clearly seen: a human mercenary of the surface in his forties with a rugged square face, a broken nose, grey stubble
and one milky scarred eye, tanned weathered skin, short dark hair, a weathered brown leather coat; 4 Ozrael, facing the viewer, the
head tilted slightly: a very old male dark elf with a long skeletal face, a beak nose and white sightless eyes, papery grey skin, long
thin white hair, a hood of dark green velvet
```

| # | File |
|---|---|
| C1 | `icon_hero_dungeon.malaroth` |
| C2 | `icon_hero_dungeon.seyrissa` |
| C3 | `icon_hero_dungeon.kaelvorn` |
| C4 | `icon_hero_dungeon.ozrael` |

## Stronghold

### Stronghold · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap iron,
feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 Grukk, in three-quarter view facing right: a scarred goblin chieftain with a pinched triangular
face, huge bat ears, a long warty nose and beady yellow eyes, mottled yellow-green skin, a dented iron helmet too big for the head; 2
Mazga, in three-quarter view facing right, the chin lifted only a little, eyes just above level, the whole face clearly seen: a female
orc with a broad flat face, short tusks, a pierced nose and keen amber eyes, olive skin, long black hair under a headdress of harpy
feathers, a flask of quicksilver on a cord at the neck; 3 Thrak, looking straight at the viewer: an orc with a long jaw, one broken
tusk, a hooked nose and small black eyes, grey-green skin, a red-ochre hand print across the face, a shaved head with a black scalp
lock; 4 Urzog, turned slightly to the left: an orc with a square heavy face, long tusks, a flat nose and calm brown eyes, dark brown
skin, a horse-tail plume on an iron cap, a leather collar
```

| # | File |
|---|---|
| W1 | `icon_hero_stronghold.grukk` |
| W2 | `icon_hero_stronghold.mazga` |
| W3 | `icon_hero_stronghold.thrak` |
| W4 | `icon_hero_stronghold.urzog` |

### Stronghold · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap iron,
feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 Borka, in three-quarter view facing left, the chin lifted only a little, eyes just above level, the
whole face clearly seen: a huge half-ogre with a wide slab face, a flat nose, a jutting underbite and tiny eyes, pale grey-pink skin,
a bald warty dome of a head; 2 Kragash, facing the viewer, the head tilted slightly: an orc with a long narrow face, gold teeth among
the tusks, a scarred lip and narrow green eyes, ash-grey skin, a wolf-pelt hood with the wolf's head on top; 3 Shara, turned slightly
to the left: a female orc with a lean angular face, small tusks, a hawk nose and eagle-sharp gold eyes, deep green skin, long black
hair braided with giant roc feathers; 4 Durgash, in three-quarter view facing right: an orc with a massive scarred face, a leather eye
patch, thick tusks and a broken nose, dark olive skin, a grey braided beard, a necklace of a giant's teeth
```

| # | File |
|---|---|
| W5 | `icon_hero_stronghold.borka` |
| W6 | `icon_hero_stronghold.kragash` |
| W7 | `icon_hero_stronghold.shara` |
| W8 | `icon_hero_stronghold.durgash` |

### Stronghold · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap iron,
feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 Ugra, looking straight at the viewer: an old female goblin shaman with a wizened tiny face, enormous
drooping ears, a beak-like nose and milky eyes, wrinkled grey-yellow skin, long thin white hair, bone charms in the ears; 2 Zhakar, in
three-quarter view facing left: a male orc shaman with a wide snarling face, a mouth open in a war cry, short tusks and wild red eyes,
olive-brown skin, black war-paint stripes, a feathered cloak collar; 3 Mograth, in three-quarter view facing right: an orc shaman with
a long sorrowful face, drooping tusks and deep-set black eyes, grey skin, a red-ochre painted face, a hood of smoked hide, a thread of
smoke curling past; 4 Yazna, in three-quarter view facing right, the chin lifted only a little, eyes just above level, the whole face
clearly seen: a female orc with a broad round face, small tusks, freckles and warm brown eyes, sandy brown skin, beaded braids, a
horse-skull mask pushed up on the head
```

| # | File |
|---|---|
| M1 | `icon_hero_stronghold.ugra` |
| M2 | `icon_hero_stronghold.zhakar` |
| M3 | `icon_hero_stronghold.mograth` |
| M4 | `icon_hero_stronghold.yazna` |

### Stronghold · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap iron,
feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 Kurgh, in three-quarter view facing left: a horned ogre shaman with a huge brutish face, a broad
nose ring, small horns and little yellow eyes, blue-grey skin, a small flame floating beside the cheek; 2 Ashka, in three-quarter view
facing right, eyes on the viewer: a female orc with a narrow wolfish face, a long nose, tusks and pale grey eyes, ash-grey skin, long
grey hair, head tilted back howling, a wolf-tooth necklace and a black fur collar; 3 Vrolk, in three-quarter view facing left, the
chin lifted only a little, eyes just above level, the whole face clearly seen: an orc with a gaunt hollow face, short tusks and stormy
blue eyes, dark grey skin with white lightning marks painted on it, storm-grey feathers in the hair; 4 Nazgra, facing the viewer, the
head tilted slightly: an old orc with a heavy creased face, a white beard, worn-down tusks and a third eye painted on the forehead,
dark olive skin, a hood of bear fur
```

| # | File |
|---|---|
| M5 | `icon_hero_stronghold.kurgh` |
| M6 | `icon_hero_stronghold.ashka` |
| M7 | `icon_hero_stronghold.vrolk` |
| M8 | `icon_hero_stronghold.nazgra` |

### Stronghold · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art.
Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap iron,
feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own
equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into another
cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row
left to right, then bottom row: 1 Gorthak Ironjaw, in three-quarter view facing right, eyes on the viewer: an old orc warlord with a
massive square face, an iron plate in place of the lower jaw, a long white braided moustache and deep-set fierce eyes, dark green
skin, a crown of iron spikes and wolf fur; 2 Sakra, in three-quarter view facing right, the chin lifted only a little, eyes just above
level, the whole face clearly seen: a young female orc with a long oval face, small tusks, a ring through the lip and bright blue
eyes, grey-green skin painted with white spirals, long blue-dyed hair in a braided crest; 3 Snikket, looking straight at the viewer: a
goblin with a narrow rat-like face, a long sharp nose, a sly grin with missing teeth and one torn ear, sickly green skin, a black
leather hood; 4 Uthgar, turned slightly to the left: an ogre with a big lumpy face, a heavy brow, a single curled horn and clever
small green eyes, reddish-brown skin, a battered wizard's blue hat
```

| # | File |
|---|---|
| C1 | `icon_hero_stronghold.gorthak` |
| C2 | `icon_hero_stronghold.sakra` |
| C3 | `icon_hero_stronghold.snikket` |
| C4 | `icon_hero_stronghold.uthgar` |

## Swamp Bestiary

### Swamp Bestiary · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Swamp
Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather pattern and
colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Ssarkh, in three-quarter view facing left, the chin lifted only a little,
eyes just above level, the whole face clearly seen: a lizardfolk warrior with a long narrow snout, a row of small spines over the brow
and amber eyes, bright green scales with black bands, the rim of a giant-shell pauldron; 2 Kethra, facing the viewer, the head tilted
slightly: a female lizardfolk with a short blunt snout, a frilled neck and golden eyes, sandy tan scales with yellow sulfur crust on
the markings; 3 Mudjaw, turned slightly to the left: a crocodile-headed brute with a huge long jaw full of teeth and tiny eyes, dark
olive armoured scutes caked in drying grey mud; 4 Vessk, in three-quarter view facing right, eyes on the viewer: a lean lizardfolk
with a slim pointed head, a crest of thin quills and red eyes, glossy black-and-crimson scales, mantis claws hung as trophies at the
collar
```

| # | File |
|---|---|
| W1 | `icon_hero_swamp-bestiary.ssarkh` |
| W2 | `icon_hero_swamp-bestiary.kethra` |
| W3 | `icon_hero_swamp-bestiary.mudjaw` |
| W4 | `icon_hero_swamp-bestiary.vessk` |

### Swamp Bestiary · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Swamp
Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather pattern and
colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Brakkis, looking straight at the viewer: a stocky lizardfolk with a broad
flat toad-like head, heavy-lidded eyes and warty brown scales, a helm made from a basilisk's spiny crest; 2 Ylsa, in three-quarter
view facing left: a female beast-folk with a flat owl-like face ringed with tawny feathers, a small hooked beak-nose and huge golden
eyes, a cloak of barred brown feathers; 3 Tharg, in three-quarter view facing right: a massive lizardfolk with a square heavy head, a
bony brow and dull yellow eyes, dark iron-grey scales, gorgon-plate armour at the neck; 4 Oszrak, in three-quarter view facing right,
the chin lifted only a little, eyes just above level, the whole face clearly seen: a lizardfolk with a long elegant head, a crown of
backswept horns and violet eyes, pale blue-grey scales, a cloak of wyvern wing leather
```

| # | File |
|---|---|
| W5 | `icon_hero_swamp-bestiary.brakkis` |
| W6 | `icon_hero_swamp-bestiary.ylsa` |
| W7 | `icon_hero_swamp-bestiary.tharg` |
| W8 | `icon_hero_swamp-bestiary.oszrak` |

### Swamp Bestiary · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Swamp
Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather pattern and
colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Hissla, in three-quarter view facing left: a female lizardfolk shaman with
a slender snout, a tall soft crest with reeds tied into it and pale green eyes, emerald scales fading to cream at the throat; 2
Sethek, in three-quarter view facing right: an ancient turtle-like shaman with a beaked mouth, deep wrinkles and tiny black eyes,
leathery grey-green skin, a turtle-shell hood; 3 Murrok, in three-quarter view facing left, the chin lifted only a little, eyes just
above level, the whole face clearly seen: a male beast-folk with a broad cat-like face, a flat wide nose, long whiskers and slitted
green eyes, tawny fur, a striped jungle-cat pelt over the head, green war paint; 4 Zhessk, facing the viewer, the head tilted
slightly: a shaman with a narrow mantis-like head, large compound eyes and twitching mandibles, glossy pale green carapace, a cloak of
translucent insect wings
```

| # | File |
|---|---|
| M1 | `icon_hero_swamp-bestiary.hissla` |
| M2 | `icon_hero_swamp-bestiary.sethek` |
| M3 | `icon_hero_swamp-bestiary.murrok` |
| M4 | `icon_hero_swamp-bestiary.zhessk` |

### Swamp Bestiary · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Swamp
Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather pattern and
colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Ilthra, in three-quarter view facing right: a female lizardfolk with a
long delicate snout, glowing green eyes and silver-grey scales, patches of scales turned to stone on one cheek; 2 Gossk, in
three-quarter view facing right, the chin lifted only a little, eyes just above level, the whole face clearly seen: an old beast-folk
shaman with a heavy bear-like face, a beaked owl mouth roaring and furious amber eyes, grizzled brown fur and feathers, an
owlbear-claw necklace; 3 Venna, looking straight at the viewer: a female lizardfolk with a narrow serpentine head, a hood like a
cobra's and cold yellow eyes, iron-grey scales, a curl of green vapour at the lips; 4 Szask, turned slightly to the left: a lizardfolk
shaman with a short wide head and orange eyes, mottled red-brown scales, a wyvern skull as a headdress
```

| # | File |
|---|---|
| M5 | `icon_hero_swamp-bestiary.ilthra` |
| M6 | `icon_hero_swamp-bestiary.gossk` |
| M7 | `icon_hero_swamp-bestiary.venna` |
| M8 | `icon_hero_swamp-bestiary.szask` |

### Swamp Bestiary · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Swamp
Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather pattern and
colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Ssethrak, in three-quarter view facing left, the chin lifted only a
little, eyes just above level, the whole face clearly seen: a gigantic ancient lizardfolk with a broad crocodilian head, a jaw scarred
by a hundred fights, one clouded eye and one bright gold eye, black-green barnacled scales, a crown of coral and bone; 2 Nhalla,
facing the viewer, the head tilted slightly: a female frog-folk with a wide flat face, a huge mouth, bulging copper eyes and smooth
spotted blue-and-yellow skin, strings of shells and teeth at the throat; 3 Krusk, turned slightly to the left: a turtle-folk warrior
with a hooked snapping beak, a huge bony head and tiny red eyes, mossy dark brown skin, a shell rim rising behind the neck; 4 Ruuk, in
three-quarter view facing right, eyes on the viewer: a heron-folk shaman with a long thin dagger beak, a crest of long grey plumes and
fierce yellow eyes, slate-grey feathers, a collar of blue-dyed reeds
```

| # | File |
|---|---|
| C1 | `icon_hero_swamp-bestiary.ssethrak` |
| C2 | `icon_hero_swamp-bestiary.nhalla` |
| C3 | `icon_hero_swamp-bestiary.krusk` |
| C4 | `icon_hero_swamp-bestiary.ruuk` |

## Conflux

### Conflux · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Conflux
heroes: the Firstborn wardens of the elements, ageless humanlike beings each made of or touched by one element in skin, eyes and hair,
stone, wind, flame, water, light, magma, storm or ice; collars of metal and crystal. 4 separate icons arranged in a grid of 2 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Orrath, looking straight at the viewer: a massive man
of living stone with a craggy square face like weathered granite, a heavy brow and eyes of glowing amber crystal, grey stone skin
veined with quartz, a beard of crystal shards, a collar of rough bronze; 2 Zephra, in three-quarter view facing left: a slender woman
with a narrow elfin face, a sharp nose and pale swirling white eyes, translucent pale-blue skin, long silver-white hair streaming
upward as if in a gale, a high grey silk collar dissolving into mist; 3 Ignar, in three-quarter view facing right, eyes on the viewer:
a broad man with a heavy square face, a flat nose and eyes of white fire, skin of cracked black basalt with molten orange light in the
cracks, hair and beard of rising flames, a collar of blackened bronze; 4 Maerin, in three-quarter view facing right, the chin lifted
only a little, eyes just above level, the whole face clearly seen: a strong-featured woman with a wide face, full lips and deep
sea-green eyes without pupils, glossy blue-green skin like wet glass, long dark hair flowing like moving water, a collar of
mother-of-pearl and coral
```

| # | File |
|---|---|
| C1 | `icon_hero_conflux.orrath` |
| C2 | `icon_hero_conflux.zephra` |
| C3 | `icon_hero_conflux.ignar` |
| C4 | `icon_hero_conflux.maerin` |

### Conflux · Campaign 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Conflux
heroes: the Firstborn wardens of the elements, ageless humanlike beings each made of or touched by one element in skin, eyes and hair,
stone, wind, flame, water, light, magma, storm or ice; collars of metal and crystal. 4 separate icons arranged in a grid of 2 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Ilumiel, in three-quarter view facing left: an
ageless man with a smooth oval face, a delicate nose and eyes of pure white light, luminous pale golden skin, short white hair glowing
at the tips, a high collar of pale gold; 2 Kalderon, in three-quarter view facing right, eyes on the viewer: a stocky old man with a
broad wrinkled face, a big bulbous nose and eyes like glowing coals, dark brown skin crusted with cooled lava, a long beard of grey
ash, a heavy collar of slag iron; 3 Vaeyra, in three-quarter view facing left, the chin lifted only a little, eyes just above level,
the whole face clearly seen: a fierce woman in her forties with a long angular face, a hawk nose and eyes crackling with blue
lightning, storm-grey skin with branching white lightning scars, long wild dark-grey hair lifting in the wind, a collar of tarnished
silver; 4 Hrimnar, facing the viewer, the head tilted slightly: a tall gaunt man with a long hollow face, a thin straight nose and
pale crystalline eyes, frost-white skin rimed with ice, long straight white hair and a beard of icicles, a collar of clear blue ice
```

| # | File |
|---|---|
| C5 | `icon_hero_conflux.ilumiel` |
| C6 | `icon_hero_conflux.kalderon` |
| C7 | `icon_hero_conflux.vaeyra` |
| C8 | `icon_hero_conflux.hrimnar` |

## Dragon Utopia

### Dragon Utopia · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Dragon
Utopia heroes: dragon-blooded wardens and dragon priests, humans with dragon traits of their own kind, small horns of different
shapes, patches of scales, slit-pupil eyes; scale and gold ornaments. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Vharos, in three-quarter view facing right: a towering man in his fifties
with a long hard face, a broad nose and slit-pupil gold eyes, bronze skin with patches of red scales on the cheeks and brow, two short
swept-back horns, black hair in a warrior's knot, a gorget of overlapping red scales; 2 Seravyn, in three-quarter view facing right,
the chin lifted only a little, eyes just above level, the whole face clearly seen: a lean woman with a narrow sharp face, a long thin
nose and green slit-pupil eyes, olive skin with a line of tiny green scales down the jaw, long black hair, small curved horns, a hood
of green-and-gold scale leather; 3 Garundar, looking straight at the viewer: a stout old man with a broad heavy face, a flattened
nose, a thick white beard and pale blue slit-pupil eyes, weathered pale skin with grey scales on the forehead, two stubby broken
horns, a heavy collar of grey dragon scale; 4 Kaira Ashwing, turned slightly to the left: a young woman with a round open face, a snub
nose, freckles of copper scales on the cheeks and bright amber slit-pupil eyes, warm brown skin, shoulder-length curly copper hair,
tiny horn nubs, a weathered tan leather collar
```

| # | File |
|---|---|
| C1 | `icon_hero_dragon-utopia.vharos` |
| C2 | `icon_hero_dragon-utopia.seravyn` |
| C3 | `icon_hero_dragon-utopia.garundar` |
| C4 | `icon_hero_dragon-utopia.kaira` |

### Dragon Utopia · Campaign 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face, cropped from the top of the head to the collarbones, the face filling most of the cell, the shoulders only at
the bottom edge; each head turned its own way as its cell says, so no two on a sheet share a pose. Realistic facial anatomy and
proportions, natural eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Strong
directional light from the top left with crisp highlights and a faint cool rim light, deep rich shadows and high contrast, a full
range of values from near-black to bright highlights, rich deep natural colours, vivid but never garish, never faded or washed out;
the figure set apart by value contrast, no drawn outline. Every face a different person with its own face shape, jaw, nose, eyes,
brows, mouth, age and skin tone, never one face with another hairstyle; every hero dressed in their own fashion and colours, armour
only where the cell names it. Women wear their hair medium to long, never cropped or shaved. No hands and nothing held or carried: no
books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel art. Dragon
Utopia heroes: dragon-blooded wardens and dragon priests, humans with dragon traits of their own kind, small horns of different
shapes, patches of scales, slit-pupil eyes; scale and gold ornaments. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Solvaric, in three-quarter view facing left, the chin lifted only a
little, eyes just above level, the whole face clearly seen: an old man with a long serene face, a high forehead, a hooked nose and
gold slit-pupil eyes, deep brown skin with fine gold scales at the temples, a long white beard, curling golden horns, a high gold
collar; 2 Nymeriss, facing the viewer, the head tilted slightly: a small delicate woman with a heart-shaped face, a tiny nose and huge
iridescent violet slit-pupil eyes, pale skin shimmering with tiny rainbow scales, translucent frills behind the ears, long lilac hair;
3 Ruvak, turned slightly to the left: a middle-aged man with a sharp hatchet face, a narrow nose and burning orange slit-pupil eyes,
ruddy skin with crimson scales across the nose, black horns curving forward, a shaved head, a collar of blackened red scales; 4
Morvayne, in three-quarter view facing right: a gaunt man with a long narrow face, high cheekbones, a thin mouth and black slit-pupil
eyes, ash-grey skin with black scales across the throat, long straight black hair, long thin black horns, a collar of black scales
```

| # | File |
|---|---|
| C5 | `icon_hero_dragon-utopia.solvaric` |
| C6 | `icon_hero_dragon-utopia.nymeriss` |
| C7 | `icon_hero_dragon-utopia.ruvak` |
| C8 | `icon_hero_dragon-utopia.morvayne` |

## Towns

Each sheet holds two towns: cells 1–2 the towns with their walls (`icon_town_<town>`), cells 3–4 the same towns as villages without
walls (`icon_town_<town>_village`). The subjects follow the town models ([town-model-prompts.md](town-model-prompts.md)).

### T1 · haven, sylvan

**Prompt** · 1024 × 1024 · negative `NEG-TOWNICON`

```text
Game town icon sheet in a realistic painterly dark-fantasy style, like a small diorama painted in oils: each icon a whole small fantasy
town seen from above at a steep angle like on a strategy map, standing on a small round patch of its own ground that fades out at the
edge, one tall landmark toward the back. Believable architecture and scale (doors, windows, roof tiles, stonework, timber), weathered
materials with grime and moss, naturalistic light from the top left with soft shadows, muted natural colours, a strong silhouette
without drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a grey stone castle on a rocky rise, round towers with dark conical
slate roofs, a white banner with a gold sun on the keep; 2 a ring of colossal living trees forming the walls, halls in
their crowns, one vast half-bare ancient tree at the back, faint blue-teal mist; 3 the same town as the one above it, as a small village: a few houses around
the bare foundation of its landmark, no walls; 4 the same town as the one above it, as a small village: a few houses around the bare
foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `icon_town_haven` |
| 2 | `icon_town_sylvan` |
| 3 | `icon_town_haven_village` |
| 4 | `icon_town_sylvan_village` |

### T2 · academy, necropolis

**Prompt** · 1024 × 1024 · negative `NEG-TOWNICON`

```text
Game town icon sheet in a realistic painterly dark-fantasy style, like a small diorama painted in oils: each icon a whole small fantasy
town seen from above at a steep angle like on a strategy map, standing on a small round patch of its own ground that fades out at the
edge, one tall landmark toward the back. Believable architecture and scale (doors, windows, roof tiles, stonework, timber), weathered
materials with grime and moss, naturalistic light from the top left with soft shadows, muted natural colours, a strong silhouette
without drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a frozen citadel of blue-grey granite with verdigris domes, a domed
observatory holding a bronze orrery of rings; 2 a dark keep among black ruins on a grey plain, a tall straight black obelisk spire with
a cold cyan light at its tip; 3 the same town as the one above it, as a small village: a few houses around the bare foundation of its landmark, no
walls; 4 the same town as the one above it, as a small village: a few houses around the bare foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `icon_town_academy` |
| 2 | `icon_town_necropolis` |
| 3 | `icon_town_academy_village` |
| 4 | `icon_town_necropolis_village` |

### T3 · inferno, dungeon

**Prompt** · 1024 × 1024 · negative `NEG-TOWNICON`

```text
Game town icon sheet in a realistic painterly dark-fantasy style, like a small diorama painted in oils: each icon a whole small fantasy
town seen from above at a steep angle like on a strategy map, standing on a small round patch of its own ground that fades out at the
edge, one tall landmark toward the back. Believable architecture and scale (doors, windows, roof tiles, stonework, timber), weathered
materials with grime and moss, naturalistic light from the top left with soft shadows, muted natural colours, a strong silhouette
without drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a jagged fortress of black slag with glowing lava cracks,
sickle-shaped spires, an altar-furnace venting smoke; 2 a gothic black citadel of many spires on a cavern ledge under hanging stalactites, a
tall spire of glowing violet crystal; 3 the same town as the one above it, as a small village: a few houses around the bare foundation of
its landmark, no walls; 4 the same town as the one above it, as a small village: a few houses around the bare foundation of its
landmark, no walls
```

| # | File |
|---|---|
| 1 | `icon_town_inferno` |
| 2 | `icon_town_dungeon` |
| 3 | `icon_town_inferno_village` |
| 4 | `icon_town_dungeon_village` |

### T4 · stronghold, swamp-bestiary

**Prompt** · 1024 × 1024 · negative `NEG-TOWNICON`

```text
Game town icon sheet in a realistic painterly dark-fantasy style, like a small diorama painted in oils: each icon a whole small fantasy
town seen from above at a steep angle like on a strategy map, standing on a small round patch of its own ground that fades out at the
edge, one tall landmark toward the back. Believable architecture and scale (doors, windows, roof tiles, stonework, timber), weathered
materials with grime and moss, naturalistic light from the top left with soft shadows, muted natural colours, a strong silhouette
without drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a log palisade around hide yurts and tents, a great lodge under a huge
horned beast skull; 2 half-sunken tilted stone ruins with stilt huts, a rampart of giant shells, a tall totem of coiled beasts; 3 the
same town as the one above it, as a small village: a few houses around the bare foundation of its landmark, no walls; 4 the same town
as the one above it, as a small village: a few houses around the bare foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `icon_town_stronghold` |
| 2 | `icon_town_swamp-bestiary` |
| 3 | `icon_town_stronghold_village` |
| 4 | `icon_town_swamp-bestiary_village` |

### T5 · conflux, dragon-utopia

**Prompt** · 1024 × 1024 · negative `NEG-TOWNICON`

```text
Game town icon sheet in a realistic painterly dark-fantasy style, like a small diorama painted in oils: each icon a whole small fantasy
town seen from above at a steep angle like on a strategy map, standing on a small round patch of its own ground that fades out at the
edge, one tall landmark toward the back. Believable architecture and scale (doors, windows, roof tiles, stonework, timber), weathered
materials with grime and moss, naturalistic light from the top left with soft shadows, muted natural colours, a strong silhouette
without drawn outlines. Not a photograph, not a plastic CGI render, no pixel art. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a stepped round altar of weathered stone ringed by pillars, a
glowing violet orb with floating stones above it, four small domed elemental shrines; 2 a terraced eyrie cut into a dark granite crag,
huge cave mouths, an outcrop at its top shaped like a dragon's head; 3 the same town as the one above it, as a small village: a few houses around the bare foundation of its landmark, no
walls; 4 the same town as the one above it, as a small village: a few houses around the bare foundation of its landmark, no walls
```

| # | File |
|---|---|
| 1 | `icon_town_conflux` |
| 2 | `icon_town_dragon-utopia` |
| 3 | `icon_town_conflux_village` |
| 4 | `icon_town_dragon-utopia_village` |

## After generating

1. Put a town's sixteen portraits side by side at 48 px: each face is distinct, the town's look is shared.
2. Check that each portrait faces right and that the face, not the gear, is the largest shape.
3. Put the town icons side by side at 32 px: each town is named at a glance, walled and village apart.
4. Check for a cartoon look (thick outlines, flat colour fills, bubbly shapes, plastic shine, doll-like faces) and regenerate such a
   sheet.
5. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/heroes/` and `textures/ui/towns/` *(proposal)* and add a line to each
   folder's `CREDITS.md` with the tool and its licence terms.
