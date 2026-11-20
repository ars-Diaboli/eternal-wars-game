# Hero and town icon prompts (for an image-generating model)

Prompts for the **hero portraits** (the hero list, the hero screen, the tavern's cards, the exchange screen, the information windows)
and the **town icons** (the town list, the kingdom overview, the town screen's title). One portrait for each of the 128 roster heroes in
`heroes.csv` ([hero-roster.md](../hero-roster.md)), one icon for each of the ten town types. Generated as **sheets of 4 (2 × 2)** like
the icons ([icon-prompts.md](icon-prompts.md)), so the four heroes of a sheet come out matching. The designs are original.

## Use a prompt

Copy the sheet's **Prompt** block whole and paste the negative block it names (below) where the model takes a negative prompt.
Generate at the size given, cut the sheet and save each cell as the table's File (`.png`); the table rows are the cells in order (top
row left to right, then the bottom row). Each town's heroes fill four sheets: warriors 1–4, warriors 5–8, mages 1–4, mages 5–8. Do not
paraphrase a prompt: the wording keeps the sheets consistent with each other.

## Shared wording

The style paragraph ("Game hero portrait sheet in a realistic…" or "Game town icon sheet in a…"), the town look ("Haven heroes: …"),
the class ("Warriors: armour, …", "Mages: robes, …") and the grid sentence ("4 separate icons arranged in a grid…") are repeated word
for word in each prompt so each is ready to paste. A change to one of them must be made in every prompt that has it: search for its
first words.

## Negative prompts

**`NEG-HERO`** (every hero sheet):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, scenery, ground shadow, photograph, plastic CGI render,
pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, cartoon, comic book, anime, manga,
chibi, cute, toy-like, childish, cel shading, flat colours, flat shading, vector art, clip art, sticker, emoji, mobile game icon,
casual game art, thick black outlines, ink line art, exaggerated proportions, oversized head, big eyes, rounded bubbly shapes, glossy
plastic, smooth airbrushed gradients, oversaturated colours, candy colours, background scenery, frame, full body, two people in one
cell, glamour, clean flawless skin, smooth doll-like face, heroic cartoon jaw, modern clothes, real-world symbols, another game's
character
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
- **Shape**: a portrait reads at 48 px (the hero list): the face is the largest shape; a town icon reads at 32 px.
- **Facing**: portraits face right; the game mirrors one where it needs.
- **Backdrop**: a portrait is transparent; the game puts its town's creature backdrop behind it
  ([creature-icon-prompts.md](creature-icon-prompts.md)) *(proposal)*.
- **Telling apart**: the four heroes of a sheet differ in age, build, colouring and one telling detail (the last item of its cell in the
  prompt, often their preferred creature or specialty); no two heroes of a town look alike.

## Haven

### Haven · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Haven heroes: humans of a grim sun-worshipping theocracy, soot-stained steel and black iron, worn brass, off-white and faded
gold cloth, small sun emblems. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2
columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Aldric Vane: a broad-shouldered man in his forties,
close-cropped grey hair, a scar across the chin, a dented steel gorget; 2 Maren Holt: a weathered woman with a short braid under an
open steel sallet, wind-burned cheeks, a pike shaft over the shoulder; 3 Corwin Ashby: a gaunt young man with a shaved head and burn
scars on the neck, a zealot's rope belt over mail, a lit torch behind; 4 Brenna Stahl: a stern woman with dark red hair tied back, a
quilted jacket, a crossbow bolt tucked behind the ear
```

| # | File |
|---|---|
| W1 | `icon_hero_haven.aldric` |
| W2 | `icon_hero_haven.maren` |
| W3 | `icon_hero_haven.corwin` |
| W4 | `icon_hero_haven.brenna` |

### Haven · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Haven heroes: humans of a grim sun-worshipping theocracy, soot-stained steel and black iron, worn brass, off-white and faded
gold cloth, small sun emblems. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2
columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Edric Morrow: an older man with a grey beard and tired
kind eyes, a monk's hood thrown back over plate armour; 2 Sabine Kell: a sharp-eyed woman with short black hair, pauldrons trimmed with
tawny griffin feathers; 3 Gerrit Hale: a heavy-set man with a thick moustache, a knight's tall plumed helm resting on the shoulder; 4
Ottilie Brand: a tall woman with a hard steady gaze, soot-dark plate with gilded edges, a sun etched on the breastplate
```

| # | File |
|---|---|
| W5 | `icon_hero_haven.edric` |
| W6 | `icon_hero_haven.sabine` |
| W7 | `icon_hero_haven.gerrit` |
| W8 | `icon_hero_haven.ottilie` |

### Haven · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Haven heroes: humans of a grim sun-worshipping theocracy, soot-stained steel and black iron, worn brass, off-white and faded
gold cloth, small sun emblems. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate
icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at
the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Ansel Wren: a thin pale young
priest, an off-white and gold stole, a small sun pendant glowing at the throat; 2 Ilse Varro: a woman with ash-blonde hair in a tight
bun, grey vestments, eyes lit faintly gold; 3 Matthias Crane: an old bald priest with a long white beard, bandaged hands holding a
small lantern; 4 Rowena Pell: a woman with a sooty face and singed sleeves, an alchemist's leather apron, a vial of yellow sulfur
```

| # | File |
|---|---|
| M1 | `icon_hero_haven.ansel` |
| M2 | `icon_hero_haven.ilse` |
| M3 | `icon_hero_haven.matthias` |
| M4 | `icon_hero_haven.rowena` |

### Haven · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Haven heroes: humans of a grim sun-worshipping theocracy, soot-stained steel and black iron, worn brass, off-white and faded
gold cloth, small sun emblems. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate
icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at
the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Fabian Lorne: a severe man with a
black goatee, an inquisitor's high collar, a smoking iron censer; 2 Agathe Sorrel: an older woman in a grey hood edged with griffin
feathers, a staff topped with a small iron sun-cage; 3 Cyprian Vey: a calm man with silver temples, a faint ring of pale gold light
behind the head; 4 Elodie Marsh: a determined young woman with freckles, an off-white tabard, a prayer book with brass clasps
```

| # | File |
|---|---|
| M5 | `icon_hero_haven.fabian` |
| M6 | `icon_hero_haven.agathe` |
| M7 | `icon_hero_haven.cyprian` |
| M8 | `icon_hero_haven.elodie` |

## Sylvan

### Sylvan · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Sylvan heroes: elves of a dying forest, long pointed ears, grey-green and moss cloth, pale carved wood and bark armour,
green-gold leaves, a hint of grey rot. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid
of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with
the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Thalion: a male elf with long silver hair, a
leaf-shaped steel pauldron, a tiny glowing pixie at the shoulder; 2 Faelar: a male elf with auburn hair, armour of bark plates, a
curled satyr horn hung on a cord; 3 Ilyra: a female elf with braided black hair, two slim blade hilts rising over the shoulders; 4
Caelwyn: a young male elf in a deep green hood, keen grey eyes, a small war drum on a strap
```

| # | File |
|---|---|
| W1 | `icon_hero_sylvan.thalion` |
| W2 | `icon_hero_sylvan.faelar` |
| W3 | `icon_hero_sylvan.ilyra` |
| W4 | `icon_hero_sylvan.caelwyn` |

### Sylvan · Warriors 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Sylvan heroes: elves of a dying forest, long pointed ears, grey-green and moss cloth, pale carved wood and bark armour,
green-gold leaves, a hint of grey rot. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid
of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with
the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Sylvara: a female elf with copper hair, a longbow with
antler-tipped limbs over the shoulder; 2 Elaith: a male elf with golden hair, a gold leaf brooch, a white pegasus feather in the hair;
3 Varis: a male elf with grey eyes and dark hair, silvered mail, a moonstone circlet; 4 Neriah: a female elf with moss in her hair,
bark armour, earrings of pale rose crystal
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Sylvan heroes: elves of a dying forest, long pointed ears, grey-green and moss cloth, pale carved wood and bark armour,
green-gold leaves, a hint of grey rot. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4
separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty
margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no
text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Elowen: a
female elf druid with roots and ivy woven into her hair, a green glow at the fingertips; 2 Taeral: a male elf in a mist-grey robe, a
reed pipe, faint green motes drifting around him; 3 Ysolde: a female elf with green-gold eyes, a staff with a fresh sprout growing from
its tip; 4 Faenor: a male elf in a travel-worn cloak, a leather map case on the shoulder
```

| # | File |
|---|---|
| M1 | `icon_hero_sylvan.elowen` |
| M2 | `icon_hero_sylvan.taeral` |
| M3 | `icon_hero_sylvan.ysolde` |
| M4 | `icon_hero_sylvan.faenor` |

### Sylvan · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Sylvan heroes: elves of a dying forest, long pointed ears, grey-green and moss cloth, pale carved wood and bark armour,
green-gold leaves, a hint of grey rot. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4
separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty
margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no
text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Ithiel: a
male elf with an amber amulet holding a trapped insect, a thin cloud of tiny insects around him; 2 Sariel: a female elf in white robes,
a mantle of white pegasus feathers; 3 Calen: a male elf with patches of bark-like skin on one cheek, an oak staff; 4 Nymara: an ancient
female elf with pale eyes, grey rot creeping over one hand, a carved memory tablet on a cord
```

| # | File |
|---|---|
| M5 | `icon_hero_sylvan.ithiel` |
| M6 | `icon_hero_sylvan.sariel` |
| M7 | `icon_hero_sylvan.calen` |
| M8 | `icon_hero_sylvan.nymara` |

## Academy

### Academy · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Academy heroes: humans of the frozen mage-cities, fur collars, frost, verdigris bronze, dark glass, pale-blue rune light.
Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Corvin Halde: a soot-smudged man with brass goggles pushed up on the forehead, a heavy wrench
on the shoulder; 2 Stellan Frost: a broad man with a frost-white beard, fur-lined steel armour, a dwarven war hammer; 3 Ingrid Vael: a
woman with grey braids and a stone-grey cloak, a small granite gargoyle perched on the shoulder; 4 Tobias Rime: a heavy-jawed man with
a riveted iron pauldron, a gauntlet lit with pale-blue runes
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Academy heroes: humans of the frozen mage-cities, fur collars, frost, verdigris bronze, dark glass, pale-blue rune light.
Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in
its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 Hedda Strom: a woman with icy blue eyes under a white fur hood, a crossbow rimmed with frost;
2 Kasimir Lode: a portly man in a heavy fur coat, a gold chain of office across the chest; 3 Runa Halvard: a woman with wind-blown
storm-grey hair, a broken bronze chain wrapped around the forearm; 4 Dorian Quill: a sharp-eyed man in a dark-teal scale cloak, a brass
spyglass in the hand
```

| # | File |
|---|---|
| W5 | `icon_hero_academy.hedda` |
| W6 | `icon_hero_academy.kasimir` |
| W7 | `icon_hero_academy.runa` |
| W8 | `icon_hero_academy.dorian` |

### Academy · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Academy heroes: humans of the frozen mage-cities, fur collars, frost, verdigris bronze, dark glass, pale-blue rune light.
Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons arranged in a grid of 2
columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Aurelian Vex: a young man with dark curls, a small
arrow of pale-blue light hovering by the hand; 2 Seraphine Lux: a woman with silver hair, a verdigris bronze circlet, a pale-blue rune
glowing on the brow; 3 Theodric Wynn: an old man with a long white beard, a small bronze hourglass amulet; 4 Isolde Marrow: a severe
woman with a dark-glass monocle, a large iron key on a chain
```

| # | File |
|---|---|
| M1 | `icon_hero_academy.aurelian` |
| M2 | `icon_hero_academy.seraphine` |
| M3 | `icon_hero_academy.theodric` |
| M4 | `icon_hero_academy.isolde` |

### Academy · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Academy heroes: humans of the frozen mage-cities, fur collars, frost, verdigris bronze, dark glass, pale-blue rune light.
Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons arranged in a grid of 2
columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Cassius Thorne: a lean man with frost in his beard, a
shard of ice floating above the palm; 2 Mirela Quist: a woman with ink-stained fingers, a quill behind the ear, a rolled star chart; 3
Lucan Esk: a bald man tattooed with fading runes, a cracked ring of pale-blue light around one wrist; 4 Valeska Rune: a proud woman
with a circlet set with rose-red crystal shards, a dark fur collar
```

| # | File |
|---|---|
| M5 | `icon_hero_academy.cassius` |
| M6 | `icon_hero_academy.mirela` |
| M7 | `icon_hero_academy.lucan` |
| M8 | `icon_hero_academy.valeska` |

## Necropolis

### Necropolis · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Necropolis heroes: necromancers, vampires and the risen dead, grey and bone-pale skin, rusted black iron, rotting grey
cloth, a cold green glow in the eyes. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid
of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with
the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Morthal: a gaunt skull-like face with thin grey skin,
rusted black armour, cold green eyes; 2 Vexa: a pale woman with a beaked plague mask pushed up on the head, green fumes curling at the
collar; 3 Grimwald: a hulking risen warrior with grey skin and an iron brace holding the jaw; 4 Ulric Ashgrave: a gaunt grey-skinned
nobleman, a collar of spider-web lace, a gold signet ring
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Necropolis heroes: necromancers, vampires and the risen dead, grey and bone-pale skin, rusted black iron, rotting grey
cloth, a cold green glow in the eyes. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid
of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with
the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Hespera: a vampire woman with black hair, red eyes and
a high black collar; 2 Corvane: a revenant knight, half the face bare bone, a tattered shroud as a cloak; 3 Drusa: a woman in black
plate with a horned helm under the arm, a skull on the pauldron; 4 Malgrin: a skeletal warrior with a crown of black iron and a rotting
fur mantle
```

| # | File |
|---|---|
| W5 | `icon_hero_necropolis.hespera` |
| W6 | `icon_hero_necropolis.corvane` |
| W7 | `icon_hero_necropolis.drusa` |
| W8 | `icon_hero_necropolis.malgrin` |

### Necropolis · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Necropolis heroes: necromancers, vampires and the risen dead, grey and bone-pale skin, rusted black iron, rotting grey
cloth, a cold green glow in the eyes. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4
separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty
margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no
text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Necrys: a
hooded necromancer with a sunken face, a staff topped with a skull, green light in the eye sockets; 2 Sepulka: an old woman with grave
dirt on the hands and a necklace of finger bones; 3 Vorlath: a thin man with sunken grey eyes and black lips, a twisted iron curse
charm; 4 Ysmera: a pale woman with silver tears of mercury on the cheeks, a spider brooch
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Necropolis heroes: necromancers, vampires and the risen dead, grey and bone-pale skin, rusted black iron, rotting grey
cloth, a cold green glow in the eyes. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4
separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty
margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no
text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Kael
Duskmire: a vampire noble with slicked dark hair, a goblet of dark red; 2 Lethe: a translucent ghostly woman, hair floating as if under
water; 3 Ozmund: a bloated man in stained plague robes, a black rat on the shoulder; 4 Thessaly: a lich with a jewelled skull, a
glowing green phylactery on a chain
```

| # | File |
|---|---|
| M5 | `icon_hero_necropolis.kael` |
| M6 | `icon_hero_necropolis.lethe` |
| M7 | `icon_hero_necropolis.ozmund` |
| M8 | `icon_hero_necropolis.thessaly` |

## Inferno

### Inferno · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Inferno heroes: demon lords and demon-born, ash-grey and slag-black skin cracked with glowing ember seams, horns, blackened
bronze, smoke. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Azgaroth: a horned demon lord with scarred black skin and ember eyes, a
small imp on the shoulder; 2 Brakka: a brutish demon with heavy ram horns, armour of slag plates; 3 Vorhaz: a lean demon with a
hound-skull pauldron, smoke curling from the mouth; 4 Skarn: a demon with iron nails driven into the skin, hooked blades rising behind
the shoulders
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Inferno heroes: demon lords and demon-born, ash-grey and slag-black skin cracked with glowing ember seams, horns, blackened
bronze, smoke. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and 2 rows,
each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Ixira: a female demon with long swept horns and folded black wings; 2
Malphor: a grinning demon with gold teeth and gold rings in the horns; 3 Zarduk: a huge demon with a bull-skull face, chains across the
chest; 4 Kezra: a lean female demon in slag armour, a bone spyglass on the belt
```

| # | File |
|---|---|
| W5 | `icon_hero_inferno.ixira` |
| W6 | `icon_hero_inferno.malphor` |
| W7 | `icon_hero_inferno.zarduk` |
| W8 | `icon_hero_inferno.kezra` |

### Inferno · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Inferno heroes: demon lords and demon-born, ash-grey and slag-black skin cracked with glowing ember seams, horns, blackened
bronze, smoke. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Belzra: a female demon with short horns, a
ball of fire burning in the palm; 2 Nyxoth: a demon with glassy black skin and glowing ember-red runes; 3 Ashmedai: a tall demon
crowned with flame, cold amused eyes; 4 Lilivex: a female demon with a mask of iron nails pushed aside, a sly smile
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Inferno heroes: demon lords and demon-born, ash-grey and slag-black skin cracked with glowing ember seams, horns, blackened
bronze, smoke. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons arranged in a
grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and
with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Pyrrhon: a demon with eyes of fire and a
screaming pale mask hung on the chest; 2 Veshra: a female demon with a mane of fire, a horse skull as a shoulder guard; 3 Orzhul: an
old demon with a long beard of flame and a staff of cooled slag; 4 Sythra: a female demon with gold coins melted into the armour
```

| # | File |
|---|---|
| M5 | `icon_hero_inferno.pyrrhon` |
| M6 | `icon_hero_inferno.veshra` |
| M7 | `icon_hero_inferno.orzhul` |
| M8 | `icon_hero_inferno.sythra` |

## Dungeon

### Dungeon · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Dungeon heroes: dark elves of the deep, ash-grey skin and white hair, black and dark-violet armour with thin silver inlay, a
faint violet glow. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Vaelrith: a male dark elf with long white hair, black armour, a cloak
of pale troglodyte hide; 2 Zhaun: a male dark elf in a scout's black hood, a thin silver band across the eyes; 3 Ilvara: a female dark
elf with small silver snakes coiled in her hair ornament; 4 Kharis: a male dark elf with a helm crowned by two bull horns
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Dungeon heroes: dark elves of the deep, ash-grey skin and white hair, black and dark-violet armour with thin silver inlay, a
faint violet glow. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Shyntra: a female dark elf in armour of black lizard scales, a lance
at the shoulder; 2 Vorn: a male dark elf heavy with gold jewellery, a cold smile; 3 Lysara: a female dark elf with a helm plume shaped
like a scorpion's tail; 4 Xundar: a male dark elf with an amulet of one great violet eye
```

| # | File |
|---|---|
| W5 | `icon_hero_dungeon.shyntra` |
| W6 | `icon_hero_dungeon.vorn` |
| W7 | `icon_hero_dungeon.lysara` |
| W8 | `icon_hero_dungeon.xundar` |

### Dungeon · Mages 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Dungeon heroes: dark elves of the deep, ash-grey skin and white hair, black and dark-violet armour with thin silver inlay, a
faint violet glow. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons arranged
in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale
and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Nethra: a female dark-elf sorceress, the
eyes hidden behind a black veil; 2 Sabal: a male dark elf with violet tattoos glowing along the jaw; 3 Ghaunis: a female dark elf half
wrapped in drifting shadow smoke; 4 Zesyra: a female dark elf with a staff of carved minotaur bone
```

| # | File |
|---|---|
| M1 | `icon_hero_dungeon.nethra` |
| M2 | `icon_hero_dungeon.sabal` |
| M3 | `icon_hero_dungeon.ghaunis` |
| M4 | `icon_hero_dungeon.zesyra` |

### Dungeon · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Dungeon heroes: dark elves of the deep, ash-grey skin and white hair, black and dark-violet armour with thin silver inlay, a
faint violet glow. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons arranged
in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale
and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers.
Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Dryel: a male dark elf with a grief-worn
face and violet tears; 2 Vhaeris: a female dark elf with a tall silver headdress like spider legs; 3 Phyrra: a female dark elf with
black lightning crackling between the fingers; 4 Ilphar: a male dark elf with an amulet of many small eyes
```

| # | File |
|---|---|
| M5 | `icon_hero_dungeon.dryel` |
| M6 | `icon_hero_dungeon.vhaeris` |
| M7 | `icon_hero_dungeon.phyrra` |
| M8 | `icon_hero_dungeon.ilphar` |

## Stronghold

### Stronghold · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Stronghold heroes: orc and goblin clan leaders, grey-green skin and tusks, hide, bone and rusted scrap iron, red-ochre war
paint. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Grukk: a scarred goblin chieftain in a dented helmet too big for the head;
2 Mazga: a female orc with a headdress of harpy feathers, a flask of quicksilver on a cord; 3 Thrak: an orc with one broken tusk, a
red-ochre hand print across the face; 4 Urzog: an orc with a long horse-tail plume on the helmet, a spear at the shoulder
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Stronghold heroes: orc and goblin clan leaders, grey-green skin and tusks, hide, bone and rusted scrap iron, red-ochre war
paint. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 Borka: a huge half-ogre with a flat nose, a bone club on the shoulder; 2
Kragash: an orc with a wolf-pelt hood and gold teeth; 3 Shara: a female orc with eagle-sharp eyes, giant roc feathers braided into the
hair; 4 Durgash: an orc with a leather eye patch and a necklace of a giant's teeth
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Stronghold heroes: orc and goblin clan leaders, grey-green skin and tusks, hide, bone and rusted scrap iron, red-ochre war
paint. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons arranged in a grid of
2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Ugra: an old female goblin shaman with a bone rattle;
2 Zhaka: a female orc in a feathered cloak, mouth open in a war cry; 3 Mograth: an orc shaman with a red-ochre painted face, smoke
rising from a bowl; 4 Yazna: a female orc with a horse-skull mask pushed up on the head
```

| # | File |
|---|---|
| M1 | `icon_hero_stronghold.ugra` |
| M2 | `icon_hero_stronghold.zhaka` |
| M3 | `icon_hero_stronghold.mograth` |
| M4 | `icon_hero_stronghold.yazna` |

### Stronghold · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Stronghold heroes: orc and goblin clan leaders, grey-green skin and tusks, hide, bone and rusted scrap iron, red-ochre war
paint. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons arranged in a grid of
2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the
same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent
background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Kurgh: a horned ogre shaman with a small flame in the
palm; 2 Ashka: a female orc with a wolf-tooth necklace, howling; 3 Vrolk: an orc with storm-grey feathers and white lightning marks
painted on the skin; 4 Nazgra: an old orc with a third eye painted on the forehead
```

| # | File |
|---|---|
| M5 | `icon_hero_stronghold.kurgh` |
| M6 | `icon_hero_stronghold.ashka` |
| M7 | `icon_hero_stronghold.vrolk` |
| M8 | `icon_hero_stronghold.nazgra` |

## Swamp Bestiary

### Swamp Bestiary · Warriors 1–4

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, wet scales, shells, reeds, bone and
driftwood, moss green. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Ssarkh: a green-scaled lizardfolk warrior with a pauldron made of a
giant shell; 2 Kethra: a female lizardfolk with yellow sulfur crust on her scale markings, a pouch of sulfur; 3 Mudjaw: a
crocodile-headed brute caked in drying mud; 4 Vessk: a lean lizardfolk with blades made of mantis claws
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, wet scales, shells, reeds, bone and
driftwood, moss green. Warriors: armour, weapons and scars, a commander's bearing. 4 separate icons arranged in a grid of 2 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 Brakkis: a lizardfolk with a helm made from a basilisk's spiny crest;
2 Ylsa: a female beast-folk with owl feathers around the face, sharp golden eyes; 3 Tharg: a massive lizardfolk in armour of dark
iron-grey gorgon plates; 4 Oszrak: a lizardfolk with a cloak of wyvern wing leather, a spyglass made of shell
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
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, wet scales, shells, reeds, bone and
driftwood, moss green. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons
arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the
same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Hissla: a female lizardfolk
shaman with reeds tied into her crest; 2 Sethek: a shaman with a turtle-shell hood, a staff of driftwood; 3 Murra: a female beast-folk
wearing a striped jungle-cat pelt, green war paint; 4 Zhessk: a shaman with a cloak of translucent insect wings
```

| # | File |
|---|---|
| M1 | `icon_hero_swamp-bestiary.hissla` |
| M2 | `icon_hero_swamp-bestiary.sethek` |
| M3 | `icon_hero_swamp-bestiary.murra` |
| M4 | `icon_hero_swamp-bestiary.zhessk` |

### Swamp Bestiary · Mages 5–8

**Prompt** · 1024 × 1024 · negative `NEG-HERO`

```text
Game hero portrait sheet in a realistic painterly dark-fantasy style, like an oil-painted character portrait: each portrait a single
character's head and shoulders in three-quarter view facing right, the face large. Realistic facial anatomy and proportions, natural
eyes, skin with pores, scars, stubble, wrinkles and weather; worn real materials with fine texture. Naturalistic light from the top
left with soft shadows and a faint cool fill light, a full range of values, muted natural colours; the figure set apart by value
contrast, no drawn outline. Weathered, tired, determined faces with real character. Not a photograph, not a plastic CGI render, no
pixel art. Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, wet scales, shells, reeds, bone and
driftwood, moss green. Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes. 4 separate icons
arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the
same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no
numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 Ilthra: a female lizardfolk with
glowing green eyes, patches of scales turned to stone; 2 Gossk: an old beast-folk shaman with an owlbear-claw necklace, roaring; 3
Venna: a female lizardfolk with iron-grey scales, a curl of green vapour at the lips; 4 Szask: a lizardfolk shaman wearing a wyvern
skull as a headdress
```

| # | File |
|---|---|
| M5 | `icon_hero_swamp-bestiary.ilthra` |
| M6 | `icon_hero_swamp-bestiary.gossk` |
| M7 | `icon_hero_swamp-bestiary.venna` |
| M8 | `icon_hero_swamp-bestiary.szask` |

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
with alpha. The icons, top row left to right, then bottom row: 1 a walled hill-town of soot-grey limestone and dark slate roofs, a
cathedral keep crowned by an iron cage holding a burning sun-brazier; 2 a ring of colossal living trees forming the walls, halls in
their crowns, one vast half-bare ancient tree at the back; 3 the same town as the one above it, as a small village: a few houses around
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
observatory holding a bronze orrery of rings; 2 stacked black ruins of many ages, ossuary walls of skulls, a crooked black spire with a
cold green light; 3 the same town as the one above it, as a small village: a few houses around the bare foundation of its landmark, no
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
sickle-shaped spires, an altar-furnace venting smoke; 2 a town sunk into a cavern sinkhole, black and violet stepped pyramids, a spire
topped by a glowing violet crystal; 3 the same town as the one above it, as a small village: a few houses around the bare foundation of
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
with alpha. The icons, top row left to right, then bottom row: 1 a ring-shaped sanctuary around a colossal half-buried gear of stone
and green bronze, four small elemental shrines; 2 a stepped pyramid eyrie carved into a sandstone crag, huge perches, a broken colossal
dragon statue; 3 the same town as the one above it, as a small village: a few houses around the bare foundation of its landmark, no
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
