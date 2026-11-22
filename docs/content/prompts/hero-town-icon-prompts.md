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
pixel art, blurry, top of the head cut off, subjects touching or overlapping, two subjects in one cell, cartoon, comic book, anime, manga,
chibi, cute, toy-like, childish, cel shading, flat colours, flat shading, vector art, clip art, sticker, emoji, mobile game icon,
casual game art, thick black outlines, ink line art, exaggerated proportions, oversized head, big eyes, rounded bubbly shapes, glossy
plastic, smooth airbrushed gradients, oversaturated colours, candy colours, background scenery, frame, full body, two people in one
cell, glamour, clean flawless skin, smooth doll-like face, heroic cartoon jaw, modern clothes, real-world symbols, another game's
character, faded colours, washed out, low contrast, muted colours, grey cast, identical faces, same face, twins, sisters, matching
outfits, one colour scheme for all, everyone in armour, hands, fingers, held objects, books, staves, wands, vials, lanterns, torches,
censers, weapons in hand, half body, bust with wide shoulders
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
- **Facing**: portraits face right; the game mirrors one where it needs.
- **Backdrop**: a portrait is transparent; the game puts its town's creature backdrop behind it
  ([creature-icon-prompts.md](creature-icon-prompts.md)) *(proposal)*.
- **Telling apart**: every hero is a different person: face shape, jaw, nose, eyes, age and skin tone, then their own clothing style
  and colours (no town colour scheme); only some wear armour, and armour differs in make. One telling detail on the head or collar
  (the last item of its cell, often their preferred creature or specialty). Women are about a third of each town's heroes, and no two
  of them share a face.

## Haven

### Haven · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Haven heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones,
each dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged
in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same
scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Aldric Vane: a broad-shouldered
man in his forties with a square jaw, a broken nose and deep-set grey eyes, weathered olive skin, close-cropped grey hair, a scar
across the chin, a high dented steel gorget over a dark blue padded collar; 2 Maren Holt: a lean road-worn woman in her thirties with
a long narrow face, high cheekbones, a sharp straight nose and pale green eyes, sun-browned freckled skin, a short sandy braid, a
mustard-yellow wool hood with a sheepskin collar; no armour; 3 Corwin Ashby: a gaunt young man with hollow cheeks, a hooked nose and
feverish dark eyes, pale skin, a shaved head, burn scars climbing the neck, a coarse brown sackcloth hood and a knotted rope at the
throat; 4 Bertram Stahl: a stocky man in his fifties with a wide flat face, a bulbous nose and small shrewd blue eyes, ruddy skin, a
bushy ginger beard going white, a crimson felt cap with a crossbow-bolt badge and a quilted crimson jacket
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Haven heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones,
each dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged
in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same
scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Edric Morrow: an older man with
a long gentle face, heavy-lidded tired brown eyes and a grey forked beard, tanned lined skin, an undyed wool monk's hood thrown back
over a mail coif; 2 Sabine Kell: a hawk-faced woman in her forties with a strong aquiline nose, a wide thin mouth and narrow amber
eyes, deep brown skin, short black hair shaved at the sides, a black leather riding coat with a collar of tawny griffin feathers; no
armour; 3 Gerrit Hale: a heavy-set jowly man with a thick brown moustache, a round bald head and small bright eyes, pink sunburnt
skin, a burnished steel bevor with gilt rivets and a red silk sash; 4 Osric Brand: a tall man in his thirties with a long horse-like
face, a cleft chin, pale grey eyes and a thin blond moustache, fair skin, straight shoulder-length blond hair, blackened plate with
gilded edges and a sun etched on the gorget
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Haven heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones,
each dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged
in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same
scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Ansel Wren: a thin pale young
priest with a narrow pointed chin, large earnest hazel eyes and jug ears, mousy brown hair cut in a tonsure, an off-white and gold
stole over a plain black cassock, a small sun pendant glowing at the throat; 2 Ivo Varro: a soft-faced middle-aged man with a round
double chin, a small upturned nose and calm dark eyes lit faintly gold, light brown skin, thinning black hair oiled back, a
plum-coloured scholar's gown with a white ruff; 3 Matthias Crane: an old bald priest with a craggy face, a bulbous nose, pale watery
eyes and a long white beard, liver-spotted skin, a coarse grey hermit's cowl, a faint golden glow on the brow; 4 Rowena Pell: a stout
woman in her fifties with a broad face, plump cheeks, a wide mouth and small bright black eyes, dark brown skin, grey-streaked frizzy
hair under a leather cap, soot and a yellow sulfur stain on the cheek, a scorched leather apron collar
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Haven heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones,
each dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged
in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same
scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Fabian Lorne: a severe man in
his forties with a gaunt angular face, a sharp black goatee, thin lips and cold black eyes, sallow skin, slicked-back black hair, an
inquisitor's tall stiff black collar with silver sun clasps; 2 Agathe Sorrel: a woman in her sixties with a long weathered face, a
prominent Roman nose and deep-set blue eyes under heavy brows, wind-reddened skin, a long iron-grey plait, a grey wool hood edged with
griffin feathers; 3 Cyprian Vey: a calm man in his fifties with an oval face, a neat silver-grey beard, a straight nose and warm brown
eyes, light olive skin, silver temples, a deep green velvet mantle with gold embroidery, a faint ring of pale gold light behind the
head; 4 Elodie Marsh: a young woman of about twenty with a heart-shaped face, a snub nose, wide-set grey eyes and a gap-toothed
determined mouth, freckled pale skin, a short tousled copper bob, a sky-blue wool pilgrim's hood over an off-white tabard
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Haven heroes: humans of a grim sun-worshipping theocracy, soldiers, nobles, priests and commoners of many origins and skin tones,
each dressed in their own way; a small sun emblem somewhere on each, a pin, an earring or a stitched mark. 4 separate icons arranged
in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same
scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Leopold Ardent: an old commander
in his seventies with a lean noble face, a long thin nose, a white moustache waxed to points and piercing pale blue eyes,
parchment-thin skin, thin white hair combed back, an ornate gilded steel gorget over a royal-blue velvet collar; 2 Isaura Voss: a
woman in her thirties with a long oval face, a sharp narrow nose, hooded dark eyes and full lips, deep umber skin, long black braids
threaded with gold beads, a deep crimson silk scarf wrapped as a hood, a gold sun-and-moon earring; 3 Tamsin Rook: a wiry woman in her
twenties with a fox-like face, a pointed chin, a crooked smile and quick hazel eyes, tanned freckled skin, short choppy chestnut hair,
a moss-green hooded jerkin with a sun badge at the throat; 4 Benedikt Grau: a corpulent man in his sixties with a heavy round face, a
fleshy hooked nose, a small cruel mouth and pouched pale eyes, pallid skin, a shaved head, a scarlet skullcap and a high scarlet
collar with a gold sun
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Sylvan heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed
in their own way; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone
in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses
into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The
icons, top row left to right, then bottom row: 1 Thalion: a male elf in his prime with a long narrow face, high sharp cheekbones, a
thin straight nose and silver-grey eyes, pale ivory skin, long straight silver hair, a leaf-shaped steel gorget over dark green
leather, a tiny glowing pixie at his ear; 2 Faelar: a stocky male elf with a broad square face for an elf, a flattened nose, a short
auburn beard and amber eyes, ruddy tanned skin, short tangled auburn curls, a brown fur-trimmed hunting jerkin, a small curled satyr
horn on a cord at the throat; 3 Aerendil: a lithe young male elf with a fine triangular face, arched brows, slanted violet-grey eyes
and a faint smile, light golden-brown skin, long black hair pulled into a high tail, leaf tattoos on the neck, a sleeveless grey-green
dancer's tunic; 4 Caelwyn: a young male elf with a boyish round face, a small upturned nose and keen grey eyes, fair freckled skin,
short tousled straw-blond hair, a deep green hood and a mottled brown scout's cloak
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Sylvan heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed
in their own way; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone
in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses
into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The
icons, top row left to right, then bottom row: 1 Sylvara: a female elf in her middle years with a long angular face, a strong straight
nose, a wide mouth and green-flecked brown eyes, warm copper-brown skin, copper hair cut short at the jaw, a russet leather coat with
a tall collar and a stag-antler pin; 2 Elaith: a proud male elf with a handsome heavy-lidded face, a strong jaw, a long aquiline nose
and golden eyes, honey-coloured skin, shoulder-length golden hair, a cream silk high-collared coat with a gold leaf brooch, a white
pegasus feather behind the ear; 3 Varis: a stern older male elf with a lean lined face, deep-set dark grey eyes, a thin mouth and a
scar through one brow, olive skin, dark hair streaked with grey tied back, a silvered mail collar and a moonstone circlet; 4 Neriah: a
broad-shouldered female elf with a wide face, a strong square jaw, a broad nose and calm hazel eyes, deep brown skin, thick black hair
in many small braids, carved pale-wood plate armour at the neck, earrings of pale rose crystal
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Sylvan heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed
in their own way; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone
in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses
into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The
icons, top row left to right, then bottom row: 1 Elowen: a young female elf with a small heart-shaped face, large round green eyes and
a pointed chin, milk-pale freckled skin, long wild red hair, a plain undyed linen dress with green embroidery at the neck, a faint
green glow in the eyes; 2 Taeral: a dreamy male elf with a long oval face, drooping eyelids, soft full lips and pale blue eyes, light
skin, ash-blond hair to the shoulders, a mist-grey hooded robe, faint green motes drifting around him; 3 Eredan: a gaunt male elf with
an ascetic hollow-cheeked face, a long thin nose and green-gold eyes, sallow olive skin, a shaved head with a leaf tattoo on the
scalp, a plain dark brown wrap over one shoulder; 4 Faenor: a weather-beaten male elf traveller with a craggy face for an elf, a
crooked broken nose and laughing blue eyes, deeply tanned skin, wind-tangled brown hair, a travel-worn blue-grey cloak
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Sylvan heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed
in their own way; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone
in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses
into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The
icons, top row left to right, then bottom row: 1 Ithiel: a sharp-featured male elf with a narrow fox-like face, slanted yellow-green
eyes and thin lips, sallow skin, black hair cropped short, a dark ochre coat, an amber amulet holding a trapped insect, tiny insects
drifting around him; 2 Sariel: a serene female elf with a long swan-like neck, a narrow face, a long straight nose and pale silver
eyes, porcelain skin, white-blonde hair in a crown of braids, a mantle of white pegasus feathers over white robes; 3 Calen: a
middle-aged male elf with a heavy brow, a broad flat nose and deep brown eyes, dark brown skin with patches of bark-like skin on one
cheek, a short grey-brown beard, a moss-brown hooded robe; 4 Nymara: an ancient female elf with a deeply lined narrow face, sunken
cheeks and milky pale eyes, translucent papery skin, long thin snow-white hair, a dark wine-red shawl, grey rot creeping up her neck
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Sylvan heroes: elves of a dying forest, long pointed ears, each a distinct person of their own age, build and colouring, dressed
in their own way; leaf motifs only in jewellery or embroidery. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone
in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses
into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The
icons, top row left to right, then bottom row: 1 Lithariel: a regal elderly female elf with a long noble face, high cheekbones, a
slightly hooked nose and stern violet eyes, pale lined skin, silver hair under a delicate circlet of white-gold leaves, a high collar
of deep plum velvet; 2 Rhovan: a rugged male elf with a square weather-cut face, a broken nose, a short dark beard and fierce green
eyes, deeply tanned skin with white scar lines, wild black dreadlocks, a wolf-pelt cloak over bare shoulders; 3 Siannon: a young
female elf with a narrow freckled face, a long pointed nose, a wide mouth and quick brown eyes, light tan skin, short spiky chestnut
hair, a hooded tunic of tawny suede; 4 Orrindel: a very old male elf with a gnarled long face, bushy white brows, a long hooked nose
and tiny bright green eyes, dark weathered brown skin, a long white beard braided with beads, a patched brown hooded cloak
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Academy heroes: humans of the frozen mage-cities, northern and southern faces alike, each dressed in their own way; fur, steel,
gold and a pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Corvin Halde: a soot-smudged man in his thirties with a long rubbery face,
a big crooked nose, bushy black eyebrows and quick dark eyes, sallow skin, a mop of greasy black hair, a brown leather work coat with
brass buttons; 2 Stellan Frost: a broad man in his sixties with a massive square head, a frost-white beard down to the chest, a
flattened nose and small ice-blue eyes, ruddy wind-burned skin, fur-lined steel plate with gold trim at the neck; 3 Ingrid Vael: a
hard-faced woman in her fifties with a broad jaw, high flat cheekbones, a small nose and pale grey eyes, weathered pale skin,
iron-grey braids coiled over the ears, a stone-grey wool cloak with a carved granite clasp; 4 Tobias Rime: a heavy-jawed young man
with a wide flat face, a thick neck, a short nose and dull blue eyes, pale freckled skin, a blond crew cut, the riveted edge of an
iron pauldron, pale-blue runes tattooed on the neck
```

| # | File |
|---|---|
| W1 | `icon_hero_academy.corvin` |
| W2 | `icon_hero_academy.stellan` |
| W3 | `icon_hero_academy.ingrid` |
| W4 | `icon_hero_academy.tobias` |

### Academy · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Academy heroes: humans of the frozen mage-cities, northern and southern faces alike, each dressed in their own way; fur, steel,
gold and a pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Henrik Strom: a lean hunter in his forties with a narrow wolfish face, a
long pointed nose, icy blue eyes and a short grizzled beard, frost-reddened skin, a white fur hood; 2 Kasimir Lode: a portly man in
his fifties with a round jowly face, a small pursed mouth, a pointed grey beard and shrewd little eyes, rosy skin, a balding head, a
heavy black fur coat and a gold chain of office; 3 Runa Halvard: a tall raw-boned woman in her twenties with a long face, a strong
hooked nose, a wide mouth and stormy grey eyes, wind-chapped pale skin, wind-blown storm-grey hair, a royal-blue quilted gambeson, a
broken gold chain round the neck; 4 Dorian Quill: a sharp-eyed man in his thirties with a lean triangular face, a pencil moustache, an
aquiline nose and narrow black eyes, light brown skin, slicked black hair, a royal-blue naval coat with a high gold-braided collar
```

| # | File |
|---|---|
| W5 | `icon_hero_academy.henrik` |
| W6 | `icon_hero_academy.kasimir` |
| W7 | `icon_hero_academy.runa` |
| W8 | `icon_hero_academy.dorian` |

### Academy · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Academy heroes: humans of the frozen mage-cities, northern and southern faces alike, each dressed in their own way; fur, steel,
gold and a pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Aurelian Vex: a young man with a soft round face, dimpled cheeks, a short
wide nose and bright curious brown eyes, warm brown skin, a mass of dark curls, a deep blue student's gown, a small arrow of pale-blue
light hovering by the cheek; 2 Seraphine Lux: a composed woman in her forties with a long oval face, a narrow nose, thin arched brows
and pale ice-blue eyes, very fair skin, silver hair cut sharply at the jaw, a white ermine collar, a pale-blue rune glowing on the
brow; 3 Theodric Wynn: an old man with a long gaunt face, a huge white beard, bushy white brows and kind watery blue eyes, wrinkled
pink skin, a black velvet skullcap, a midnight-blue robe collar stitched with gold stars, a small gold hourglass amulet; 4 Ivar
Marrow: a severe middle-aged man with a long gaunt face, a sharp widow's peak of black hair, a narrow nose and one eye behind a
dark-glass monocle, grey pallid skin, a black high-necked coat with steel buttons
```

| # | File |
|---|---|
| M1 | `icon_hero_academy.aurelian` |
| M2 | `icon_hero_academy.seraphine` |
| M3 | `icon_hero_academy.theodric` |
| M4 | `icon_hero_academy.ivar` |

### Academy · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Academy heroes: humans of the frozen mage-cities, northern and southern faces alike, each dressed in their own way; fur, steel,
gold and a pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Cassius Thorne: a lean man in his forties with a hatchet face, sharp
cheekbones, a long straight nose and pale grey eyes, wind-burned skin, frost in his short dark beard, a white fur mantle, a shard of
ice floating beside the face; 2 Mirela Quist: a plump bookish woman in her thirties with a round soft face, a small chin, round
spectacles and warm brown eyes, olive skin, a messy dark bun stuck with a quill, an ink smudge on the cheek, a mustard-yellow wool
shawl; 3 Lucan Esk: a bald tall man with a long skull, sunken cheeks, a thin hooked nose and deep-set black eyes, dark brown skin
tattooed with fading pale-blue runes, a charcoal-grey robe with a high collar; 4 Valeska Rune: a proud woman in her fifties with a
long angular face, a strong jaw, a high-bridged nose and hooded dark eyes, olive skin, black hair streaked with white in a tall coil,
a circlet set with rose-red crystal shards, a dark sable collar
```

| # | File |
|---|---|
| M5 | `icon_hero_academy.cassius` |
| M6 | `icon_hero_academy.mirela` |
| M7 | `icon_hero_academy.lucan` |
| M8 | `icon_hero_academy.valeska` |

### Academy · Campaign 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Academy heroes: humans of the frozen mage-cities, northern and southern faces alike, each dressed in their own way; fur, steel,
gold and a pale ice-blue rune light as accents, no green and no teal. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Magnus Hollin: an imposing old man with a broad lion-like face, a mane of
white hair and beard, a broad flat nose and fierce gold-brown eyes, ruddy skin, a deep crimson robe with a heavy gold collar; 2 Liv
Ostrander: a square-built woman in her forties with a broad round face, a wide flat nose, ruddy cheeks and small blue eyes, pale
freckled skin, thick blond hair in two braids, a steel gorget over a red wool collar; 3 Edvard Lunde: a burly bald man in his fifties
with a heavy brow ridge, a broken nose, a thick black beard and dark eyes, swarthy skin marked by small burn scars, a brown leather
smith's collar; 4 Nadia Sorell: a girl of about eighteen with a narrow elfin face, a sharp chin, large dark eyes and thick straight
brows, light brown skin, a short black pixie cut, a royal-blue high-collared jacket, pale-blue rune light in her irises
```

| # | File |
|---|---|
| C1 | `icon_hero_academy.magnus` |
| C2 | `icon_hero_academy.liv` |
| C3 | `icon_hero_academy.edvard` |
| C4 | `icon_hero_academy.nadia` |

## Necropolis

### Necropolis · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a close-up
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Necropolis heroes: necromancers, vampires and the risen dead, from the barely living to bare bone, grave-cold skin in many shades
of pallor, each dressed in their own way; a cold green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Morthal: a skull-like male face, grey skin stretched thin over the
bones, a lipless mouth and sunken cold green eyes, a few strands of white hair, a rusted black steel gorget; 2 Vexa: a sharp-faced
woman in her thirties with a narrow pointed face, a long thin nose and red-rimmed pale eyes, sickly yellow-grey skin, lank black hair,
a beaked leather plague mask pushed up on the forehead, green fumes curling at the collar of a stained oilskin coat; 3 Grimwald: a
hulking risen warrior with a huge square head, grey-blue dead skin, a heavy brow, one milky eye and a jaw held on by an iron brace,
grey stubble; 4 Ulric Ashgrave: a gaunt aristocrat with a long horse-like face, a pointed grey beard, a high forehead and heavy-lidded
eyes, grey parchment skin, a collar of black spider-web lace on a burgundy velvet doublet
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Necropolis heroes: necromancers, vampires and the risen dead, from the barely living to bare bone, grave-cold skin in many shades
of pallor, each dressed in their own way; a cold green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Hespera: a vampire woman with a sharp heart-shaped face, a small
cruel mouth, a narrow nose and burning red eyes, alabaster skin, short sleek black hair, a high black lacquered collar with a ruby
pin; 2 Corvane: a revenant knight, half the face bare bone and half pale dead flesh with a black beard, one cold green eye, a tattered
grey shroud as a hood over black mail; 3 Drusus: a grim broad-faced death knight in his forties with a heavy jaw, a flattened boxer's
nose and pale dead eyes, ash-grey skin, a shaved scarred head, black plate with a horned gorget; 4 Malgrin: a skeletal warrior king, a
long bare skull of cracked yellow bone, green fire in the eye sockets, a crown of black iron, a rotting brown fur mantle
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Necropolis heroes: necromancers, vampires and the risen dead, from the barely living to bare bone, grave-cold skin in many shades
of pallor, each dressed in their own way; a cold green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Necrys: a hooded necromancer with a sunken narrow face, a long hooked
nose and green light in the deep eye sockets, waxy grey skin, a black wool hood; 2 Sepulka: an old woman with a wide wrinkled
toad-like face, a flat nose, a toothless grin and tiny black eyes, earthy brown skin, wild grey hair, grave dirt on the face, a
necklace of finger bones over a patched brown shawl; 3 Vorlath: a thin man with a narrow ferret face, sunken grey eyes, black lips and
a receding chin, pale bluish skin, thin greasy dark hair, a high-collared dark violet coat, a twisted iron charm at the throat; 4
Ysmera: a pale woman in her twenties with a long oval face, a delicate nose, large silvery eyes and silver tears of mercury on the
cheeks, bloodless white skin, straight ash-blonde hair, a black lace mourning veil, a silver spider brooch
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Necropolis heroes: necromancers, vampires and the risen dead, from the barely living to bare bone, grave-cold skin in many shades
of pallor, each dressed in their own way; a cold green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Kael Duskmire: a vampire noble with a chiselled long face, a widow's
peak of slicked dark hair, a thin smile showing fangs and pale gold eyes, marble-white skin, a crimson silk cravat and a black velvet
collar; 2 Lethe: a translucent ghostly woman with a soft round young face, wide sad eyes and parted lips, glowing blue-white skin,
long hair floating as if under water, a drowned girl's lace collar; 3 Ozmund: a bloated man with a fat round face, sagging jowls, a
pocked nose and bloodshot eyes, greenish blotched skin, a few strands of hair, stained mustard-yellow plague robes, a black rat on his
shoulder; 4 Thessaly: a lich, a narrow elegant skull with jewels set in the cheekbones, violet fire in the sockets, a tall jagged
crown of tarnished silver, a glowing green phylactery on a chain at the throat
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Necropolis heroes: necromancers, vampires and the risen dead, from the barely living to bare bone, grave-cold skin in many shades
of pallor, each dressed in their own way; a cold green glow in the eyes of some. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Maladrea: a tall regal woman of no clear age with a long angular
face, sharp high cheekbones, a hooked nose and black eyes with green pinpoints, chalk-white skin, black hair swept up under a tall
bone headdress, a stiff collar of black and silver; 2 Aldous Wraithe: a vampire in his fifties with a broad scarred face, a broken
nose, a clipped grey beard and red eyes, grey-white skin, a cropped grey head, a faded white surcoat collar with a scorched sun
emblem; 3 Ravenna: a wiry woman in her forties with a narrow hatchet face, sunken cheeks, a scar splitting the lips and pale grey
eyes, grey-tinged skin, cropped white hair, a black leather gorget studded with small bones; 4 Ossian Grael: a hunched ancient man
with a long drooping face, a huge hooked nose, rheumy grey eyes and a wispy white beard, liver-spotted grey skin, a bald head under a
moth-eaten brown cap
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Inferno heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze,
glowing ember seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Azgaroth: a horned demon lord with a long wedge-shaped face, curling black horns, a heavy
brow and ember-orange eyes, scarred black skin with glowing seams, a high collar of blackened bronze, a small imp perched by his ear;
2 Brakka: a brutish demon with a wide ape-like face, a jutting tusked jaw, heavy curled ram horns and small red eyes, brick-red hide,
slag-plate armour at the neck; 3 Vorhaz: a lean demon with a narrow jackal face, swept-back thin horns, yellow eyes and smoke curling
from the mouth, ash-grey skin, the edge of a hound-skull pauldron; 4 Skarn: a demon with a gaunt hollow face, no nose, a lipless grin
of needle teeth and white eyes, sickly bronze skin pierced by iron nails, two hooked horns pointing forward
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Inferno heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze,
glowing ember seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Ixira: a female demon with a sharp angular face, long swept-back horns, high cheekbones and
slit gold eyes, deep crimson skin, short black hair, the tops of folded black wings at the edges; 2 Malphor: a grinning demon with a
broad toad-like face, gold teeth, gold rings in short stubby horns and greedy small eyes, orange-brown skin, a gold-trimmed black
brocade collar; 3 Zarduk: a huge demon with a bull-skull face, wide horns and smouldering eye sockets, charred black hide, chains
across the chest; 4 Kezrath: a lean demon with a hawk-like face, a beaked nose, two short straight horns and burning white eyes,
ash-grey skin with red seams, a hood of chain mail
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Inferno heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze,
glowing ember seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Belzra: a female demon with a round impish face, short goat horns, a wide grin and bright
orange eyes, sooty grey skin, a wild mane of black hair, a ball of fire floating beside the cheek; 2 Nyxoth: a demon with a smooth
eyeless face of glassy black skin cracked with glowing ember-red runes, two long backswept horns, a high collar of dark iron; 3
Ashmedai: a tall regal demon with a long elegant face, a straight nose, cold amused violet eyes and a crown of flame between ridged
horns, smooth bronze skin, a black silk collar; 4 Lilivex: a female demon with a narrow sly face, a pointed chin, a sly smile and
heavy-lidded green-gold eyes, dusky purple skin, small curled horns, a mask of iron nails pushed aside onto the forehead
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Inferno heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze,
glowing ember seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Pyrrhon: a demon with a long gaunt face, eyes of fire, a fanged mouth and three short horns
on the brow, cracked charcoal skin, a screaming pale mask hung at the chest; 2 Veshra: a female demon with a long horse-like face, a
mane of fire, large dark eyes and flared nostrils, smoky grey skin, a horse skull as a shoulder guard; 3 Orzhul: an old demon with a
wrinkled long face, drooping horns, squinting ember eyes and a long beard of flame, cracked grey-brown skin; 4 Sythra: a female demon
with a broad strong face, a wide mouth, a hooked nose and gold eyes, dark bronze skin, curving gold-tipped horns, gold coins melted
into her collar
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Inferno heroes: demon lords and the demon-born, horns of every shape, skin from ash-grey and slag-black to deep red and bronze,
glowing ember seams on some, each dressed in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Vazgoroth: a colossal ancient demon with a broad flat face, four huge horns, a cleft chin
and molten gold eyes, obsidian-black skin webbed with white-hot cracks, a crown of iron spikes; 2 Ishara: a human woman in her forties
with a long proud face, a strong nose, dark kohl-lined eyes and full lips, deep brown skin with a glowing brand on the brow, tiny
horns budding at the temples, a crimson silk headwrap; 3 Grizhak: a squat demon with a broad pug face, a split upper lip, broken horn
stumps and small mean orange eyes, rusty brown hide covered in bite scars, a spiked leather collar; 4 Zethrax: a thin bookish demon
with a long narrow face, small round spectacles of smoked glass, a thin moustache of smoke and amber eyes, pale ash-grey skin, a pair
of thin spiral horns, a high-buttoned black coat
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Dungeon heroes: dark elves of the deep, long pointed ears, skin from ash-grey to slate-blue and charcoal, hair from white to
silver, each dressed in their own way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Vaelrith: a male dark elf in his prime with a long narrow face, high hard
cheekbones, a thin nose and pale red eyes, ash-grey skin, long straight white hair, black plate at the neck under a cloak of pale
troglodyte hide; 2 Zhaun: a young male dark elf with a small sharp face, a pointed chin and darting violet eyes, slate-blue skin,
short spiky white hair, a black scout's hood, a thin silver band across the brow; 3 Ilvara: a female dark elf with a broad strong
face, a wide mouth, a broken nose and yellow eyes, charcoal-black skin, a shaved head with a silver snake ornament coiled over one
ear, a black leather collar; 4 Kharis: a massive male dark elf with a square heavy face, a thick neck, a flattened nose and dull red
eyes, dark grey skin, a white topknot, a helm crowned with two bull horns
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Dungeon heroes: dark elves of the deep, long pointed ears, skin from ash-grey to slate-blue and charcoal, hair from white to
silver, each dressed in their own way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Shyrran: a male dark elf rider with a lean wolfish face, a hooked nose, a
long scar down one cheek and silver eyes, blue-grey skin, white hair in a long braid, armour of black lizard scales at the neck; 2
Vorn: a heavy-set older male dark elf with a fleshy round face, a double chin, a cold smile and small gold eyes, ash-grey skin, a
balding head with white side hair, heavy gold earrings and a gold collar; 3 Lyrthas: a male dark elf with an angular cruel face, thin
lips, a long sharp nose and pale violet eyes, dark slate skin, silver hair shaved on one side, a helm plume shaped like a scorpion's
tail; 4 Xundar: an old male dark elf with a gaunt lined face, a bent nose, one milky eye and one violet eye, grey skin, a long thin
white beard, an amulet of one great violet eye at the throat
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Dungeon heroes: dark elves of the deep, long pointed ears, skin from ash-grey to slate-blue and charcoal, hair from white to
silver, each dressed in their own way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Nethra: a female dark-elf sorceress with a long elegant face, a straight
nose and full dark lips, ash-grey skin, the eyes hidden behind a sheer black veil, white hair bound in silver wire; 2 Sabal: a male
dark elf with a broad smooth face, a wide flat nose and narrow amber eyes, deep charcoal skin, a shaved head, violet tattoos glowing
along the jaw; 3 Ghaunis: a female dark elf in her later years with a gaunt narrow face, hollow cheeks and pale lilac eyes, ash-grey
skin, a short ragged white crop, half the face wrapped in drifting shadow smoke; 4 Zesrin: a young male dark elf with a soft boyish
face, large dark violet eyes and a nervous mouth, smooth blue-grey skin, shoulder-length silver hair, a deep red silk collar, a
minotaur-horn earring
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Dungeon heroes: dark elves of the deep, long pointed ears, skin from ash-grey to slate-blue and charcoal, hair from white to
silver, each dressed in their own way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Dryel: a male dark elf with a long grief-worn face, drooping eyes shedding
violet tears and a thin beard, grey skin, lank white hair, a black mourning collar; 2 Vhaeris: a female dark-elf high priestess with a
long narrow face, a sharp chin and cold white eyes, slate-black skin, a tall silver headdress like spider legs; 3 Phyrra: a young
female dark elf with a wild angular face, a wide grin and crackling bright violet eyes, light ash skin, white hair standing on end,
black lightning crackling about her head; 4 Ilphar: a hunched male dark elf with a round wrinkled face, a pug nose and squinting eyes,
mottled grey skin, a bald head, an amulet of many small eyes
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Dungeon heroes: dark elves of the deep, long pointed ears, skin from ash-grey to slate-blue and charcoal, hair from white to
silver, each dressed in their own way; a faint violet glow on some. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Malaroth: a towering older male dark elf with a long craggy face, a
jutting jaw, deep-set red eyes and a scar across both lips, coal-black skin, a white mane, a collar of black horn plates; 2 Seyrissa:
a female dark elf with a small round face, a snub nose, full lips and a row of tiny silver studs above each brow, deep violet-grey
skin, white hair in two thick braids, a high collar of black spider silk; 3 Kaelvorn: a male dark elf with a rugged square face, a
broken nose, white stubble and one silver eye, ash-grey skin burnt darker by the sun, short white hair, a weathered brown leather
coat; 4 Ozrael: a very old male dark elf with a long skeletal face, a beak nose and white sightless eyes, papery grey skin, long thin
white hair, a hood of dark green velvet
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap
iron, feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its
own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Grukk: a scarred goblin chieftain with a pinched triangular face, huge bat ears, a long
warty nose and beady yellow eyes, mottled yellow-green skin, a dented iron helmet too big for the head; 2 Mazga: a female orc with a
broad flat face, short tusks, a pierced nose and keen amber eyes, olive skin, a headdress of harpy feathers, a flask of quicksilver on
a cord at the neck; 3 Thrak: an orc with a long jaw, one broken tusk, a hooked nose and small black eyes, grey-green skin, a red-ochre
hand print across the face, a shaved head with a black scalp lock; 4 Urzog: an orc with a square heavy face, long tusks, a flat nose
and calm brown eyes, dark brown skin, a horse-tail plume on an iron cap, a leather collar
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap
iron, feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its
own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Borka: a huge half-ogre with a wide slab face, a flat nose, a jutting underbite and tiny
eyes, pale grey-pink skin, a bald warty dome of a head; 2 Kragash: an orc with a long narrow face, gold teeth among the tusks, a
scarred lip and narrow green eyes, ash-grey skin, a wolf-pelt hood with the wolf's head on top; 3 Shara: a female orc with a lean
angular face, small tusks, a hawk nose and eagle-sharp gold eyes, deep green skin, long black hair braided with giant roc feathers; 4
Durgash: an orc with a massive scarred face, a leather eye patch, thick tusks and a broken nose, dark olive skin, a grey braided
beard, a necklace of a giant's teeth
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap
iron, feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its
own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Ugra: an old female goblin shaman with a wizened tiny face, enormous drooping ears, a
beak-like nose and milky eyes, wrinkled grey-yellow skin, wisps of white hair, bone charms in the ears; 2 Zhakar: a male orc shaman
with a wide snarling face, a mouth open in a war cry, short tusks and wild red eyes, olive-brown skin, black war-paint stripes, a
feathered cloak collar; 3 Mograth: an orc shaman with a long sorrowful face, drooping tusks and deep-set black eyes, grey skin, a
red-ochre painted face, a hood of smoked hide, a thread of smoke curling past; 4 Yazna: a female orc with a broad round face, small
tusks, freckles and warm brown eyes, sandy brown skin, beaded braids, a horse-skull mask pushed up on the head
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap
iron, feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its
own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Kurgh: a horned ogre shaman with a huge brutish face, a broad nose ring, small horns and
little yellow eyes, blue-grey skin, a small flame floating beside the cheek; 2 Ashka: a female orc with a narrow wolfish face, a long
nose, tusks and pale grey eyes, ash-grey skin, head tilted back howling, a wolf-tooth necklace and a black fur collar; 3 Vrolk: an orc
with a gaunt hollow face, short tusks and stormy blue eyes, dark grey skin with white lightning marks painted on it, storm-grey
feathers in the hair; 4 Nazgra: an old orc with a heavy creased face, a white beard, worn-down tusks and a third eye painted on the
forehead, dark olive skin, a hood of bear fur
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Stronghold heroes: orc, goblin and ogre clan leaders, tusks, skin from grey-green and olive to brown and ash, hide, bone, scrap
iron, feathers and war paint, each in their own way. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its
own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Gorthak Ironjaw: an old orc warlord with a massive square face, an iron plate in place of
the lower jaw, a long white braided moustache and deep-set fierce eyes, dark green skin, a crown of iron spikes and wolf fur; 2 Sakra:
a young female orc with a long oval face, small tusks, a ring through the lip and bright blue eyes, grey-green skin painted with white
spirals, a shaved head with a crest of blue-dyed hair; 3 Snikket: a goblin with a narrow rat-like face, a long sharp nose, a sly grin
with missing teeth and one torn ear, sickly green skin, a black leather hood; 4 Uthgar: an ogre with a big lumpy face, a heavy brow, a
single curled horn and clever small green eyes, reddish-brown skin, a battered wizard's blue hat
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather
pattern and colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Ssarkh: a lizardfolk warrior with a long narrow snout, a row of small
spines over the brow and amber eyes, bright green scales with black bands, the rim of a giant-shell pauldron; 2 Kethra: a female
lizardfolk with a short blunt snout, a frilled neck and golden eyes, sandy tan scales with yellow sulfur crust on the markings; 3
Mudjaw: a crocodile-headed brute with a huge long jaw full of teeth and tiny eyes, dark olive armoured scutes caked in drying grey
mud; 4 Vessk: a lean lizardfolk with a slim pointed head, a crest of thin quills and red eyes, glossy black-and-crimson scales, mantis
claws hung as trophies at the collar
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather
pattern and colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Brakkis: a stocky lizardfolk with a broad flat toad-like head,
heavy-lidded eyes and warty brown scales, a helm made from a basilisk's spiny crest; 2 Ylsa: a female beast-folk with a flat owl-like
face ringed with tawny feathers, a small hooked beak-nose and huge golden eyes, a cloak of barred brown feathers; 3 Tharg: a massive
lizardfolk with a square heavy head, a bony brow and dull yellow eyes, dark iron-grey scales, gorgon-plate armour at the neck; 4
Oszrak: a lizardfolk with a long elegant head, a crown of backswept horns and violet eyes, pale blue-grey scales, a cloak of wyvern
wing leather
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather
pattern and colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Hissla: a female lizardfolk shaman with a slender snout, a tall soft
crest with reeds tied into it and pale green eyes, emerald scales fading to cream at the throat; 2 Sethek: an ancient turtle-like
shaman with a beaked mouth, deep wrinkles and tiny black eyes, leathery grey-green skin, a turtle-shell hood; 3 Murrok: a male
beast-folk with a broad cat-like face, a flat wide nose, long whiskers and slitted green eyes, tawny fur, a striped jungle-cat pelt
over the head, green war paint; 4 Zhessk: a shaman with a narrow mantis-like head, large compound eyes and twitching mandibles, glossy
pale green carapace, a cloak of translucent insect wings
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather
pattern and colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Ilthra: a female lizardfolk with a long delicate snout, glowing green
eyes and silver-grey scales, patches of scales turned to stone on one cheek; 2 Gossk: an old beast-folk shaman with a heavy bear-like
face, a beaked owl mouth roaring and furious amber eyes, grizzled brown fur and feathers, an owlbear-claw necklace; 3 Venna: a female
lizardfolk with a narrow serpentine head, a hood like a cobra's and cold yellow eyes, iron-grey scales, a curl of green vapour at the
lips; 4 Szask: a lizardfolk shaman with a short wide head and orange eyes, mottled red-brown scales, a wyvern skull as a headdress
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, each with its own head shape, scale or feather
pattern and colours, shells, reeds, bone and feathers worn in their own way. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Ssethrak: a gigantic ancient lizardfolk with a broad crocodilian
head, a jaw scarred by a hundred fights, one clouded eye and one bright gold eye, black-green barnacled scales, a crown of coral and
bone; 2 Nhalla: a female frog-folk with a wide flat face, a huge mouth, bulging copper eyes and smooth spotted blue-and-yellow skin,
strings of shells and teeth at the throat; 3 Krusk: a turtle-folk warrior with a hooked snapping beak, a huge bony head and tiny red
eyes, mossy dark brown skin, a shell rim rising behind the neck; 4 Ruuk: a heron-folk shaman with a long thin dagger beak, a crest of
long grey plumes and fierce yellow eyes, slate-grey feathers, a collar of blue-dyed reeds
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Conflux heroes: the Firstborn wardens of the elements, ageless humanlike beings each made of or touched by one element in skin,
eyes and hair, stone, wind, flame, water, light, magma, storm or ice; collars of metal and crystal. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Orrath: a massive man of living stone
with a craggy square face like weathered granite, a heavy brow and eyes of glowing amber crystal, grey stone skin veined with quartz,
a beard of crystal shards, a collar of rough bronze; 2 Zephra: a slender woman with a narrow elfin face, a sharp nose and pale
swirling white eyes, translucent pale-blue skin, long silver-white hair streaming upward as if in a gale, a high grey silk collar
dissolving into mist; 3 Ignar: a broad man with a heavy square face, a flat nose and eyes of white fire, skin of cracked black basalt
with molten orange light in the cracks, hair and beard of rising flames, a collar of blackened bronze; 4 Maerin: a strong-featured
woman with a wide face, full lips and deep sea-green eyes without pupils, glossy blue-green skin like wet glass, long dark hair
flowing like moving water, a collar of mother-of-pearl and coral
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Conflux heroes: the Firstborn wardens of the elements, ageless humanlike beings each made of or touched by one element in skin,
eyes and hair, stone, wind, flame, water, light, magma, storm or ice; collars of metal and crystal. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Ilumiel: an ageless man with a smooth
oval face, a delicate nose and eyes of pure white light, luminous pale golden skin, short white hair glowing at the tips, a high
collar of pale gold; 2 Kalderon: a stocky old man with a broad wrinkled face, a big bulbous nose and eyes like glowing coals, dark
brown skin crusted with cooled lava, a long beard of grey ash, a heavy collar of slag iron; 3 Vaeyra: a fierce woman in her forties
with a long angular face, a hawk nose and eyes crackling with blue lightning, storm-grey skin with branching white lightning scars,
short wild dark-grey hair standing on end, a collar of tarnished silver; 4 Hrimnar: a tall gaunt man with a long hollow face, a thin
straight nose and pale crystalline eyes, frost-white skin rimed with ice, long straight white hair and a beard of icicles, a collar of
clear blue ice
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Dragon Utopia heroes: dragon-blooded wardens and dragon priests, humans with dragon traits of their own kind, small horns of
different shapes, patches of scales, slit-pupil eyes; scale and gold ornaments. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Vharos: a towering man in his fifties with a long hard face, a broad
nose and slit-pupil gold eyes, bronze skin with patches of red scales on the cheeks and brow, two short swept-back horns, black hair
in a warrior's knot, a gorget of overlapping red scales; 2 Seravyn: a lean woman with a narrow sharp face, a long thin nose and green
slit-pupil eyes, olive skin with a line of tiny green scales down the jaw, short black hair, small curved horns, a hood of
green-and-gold scale leather; 3 Garundar: a stout old man with a broad heavy face, a flattened nose, a thick white beard and pale blue
slit-pupil eyes, weathered pale skin with grey scales on the forehead, two stubby broken horns, a heavy collar of grey dragon scale; 4
Kaira Ashwing: a young woman with a round open face, a snub nose, freckles of copper scales on the cheeks and bright amber slit-pupil
eyes, warm brown skin, a mass of curly copper hair, tiny horn nubs, a weathered tan leather collar
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
of one character's face in three-quarter view facing right, cropped from the top of the head to the collarbones, the face filling most
of the cell, the shoulders only at the bottom edge. Realistic facial anatomy and proportions, natural eyes, skin with pores, scars,
stubble, wrinkles and weather; worn real materials with fine texture. Strong directional light from the top left with crisp highlights
and a faint cool rim light, deep rich shadows and high contrast, a full range of values from near-black to bright highlights, rich
deep natural colours, vivid but never garish, never faded or washed out; the figure set apart by value contrast, no drawn outline.
Every face a different person with its own face shape, jaw, nose, eyes, brows, mouth, age and skin tone, never one face with another
hairstyle; every hero dressed in their own fashion and colours, armour only where the cell names it. No hands and nothing held or
carried: no books, staves, wands, vials, lanterns, torches, censers or weapons. Not a photograph, not a plastic CGI render, no pixel
art. Dragon Utopia heroes: dragon-blooded wardens and dragon priests, humans with dragon traits of their own kind, small horns of
different shapes, patches of scales, slit-pupil eyes; scale and gold ornaments. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Solvaric: an old man with a long serene face, a high forehead, a
hooked nose and gold slit-pupil eyes, deep brown skin with fine gold scales at the temples, a long white beard, curling golden horns,
a high gold collar; 2 Nymeriss: a small delicate woman with a heart-shaped face, a tiny nose and huge iridescent violet slit-pupil
eyes, pale skin shimmering with tiny rainbow scales, translucent frills behind the ears, short lilac hair; 3 Ruvak: a middle-aged man
with a sharp hatchet face, a narrow nose and burning orange slit-pupil eyes, ruddy skin with crimson scales across the nose, black
horns curving forward, a shaved head, a collar of blackened red scales; 4 Morvayne: a gaunt man with a long narrow face, high
cheekbones, a thin mouth and black slit-pupil eyes, ash-grey skin with black scales across the throat, long straight black hair, long
thin black horns, a collar of black scales
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
