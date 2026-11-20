# Hero and town icon prompts (for an image-generating model)

Prompts for the **hero portraits** (the hero list, the hero screen, the tavern's cards, the exchange screen, the information windows)
and the **town icons** (the town list, the kingdom overview, the town screen's title). One portrait for each of the 128 roster heroes in
`heroes.csv` ([hero-roster.md](../hero-roster.md)), one icon for each of the ten town types. Generated as **sheets of 4 (2 × 2)** like
the icons ([icon-prompts.md](icon-prompts.md)), so the four heroes of a sheet come out matching. The designs are original.

## Compose a prompt

Every sheet below has a **Compose** line naming its blocks. Build the prompt from blocks, copying each block's text exactly and joining
them with a space:

```text
PROMPT   = HERO-STYLE + HERO-<town> + HERO-<class> + ICON-SHEET-4 + CELLS    (hero sheets)
         = TOWNICON-STYLE + ICON-SHEET-4 + CELLS                              (town sheets)
CELLS    = "1 <subject 1>; 2 <subject 2>; …"   the sheet table's Subject column, in its # order
NEGATIVE = ICON-NEG + ", " + HERO-NEG or TOWNICON-NEG   (only where the model takes a negative prompt)
OUTPUT   = 1024 × 1024; cut the sheet and save each cell as <File>.png
```

`ICON-SHEET-4` and `ICON-NEG` are in [icon-prompts.md](icon-prompts.md#blocks). Each town's heroes fill four sheets: warriors 1–4, warriors
5–8, mages 1–4, mages 5–8, in the order of the tables. Do not paraphrase blocks or subjects.

## Blocks

**`HERO-STYLE`** (style, first in every hero prompt):

```text
Game hero portrait sheet, hand-painted dark-fantasy style, rich painted detail, each portrait a single character's head and shoulders
in three-quarter view facing right, the face large and lit from the top left, a thin dark outline around the figure. Weathered, tired,
determined faces with real character, worn realistic materials. No photo realism, no 3D render look, no pixel art.
```

**`HERO-WARRIOR`** (class, after the town block on warrior sheets):

```text
Warriors: armour, weapons and scars, a commander's bearing.
```

**`HERO-MAGE`** (class, after the town block on mage sheets):

```text
Mages: robes, staves, books and charms, a faint glow of their magic at the hands or eyes.
```

**`HERO-<town>`** (look, second in every hero prompt):

| Block | Text |
|---|---|
| `HERO-haven` | `Haven heroes: humans of a grim sun-worshipping theocracy, soot-stained steel and black iron, worn brass, off-white and faded gold cloth, small sun emblems.` |
| `HERO-sylvan` | `Sylvan heroes: elves of a dying forest, long pointed ears, grey-green and moss cloth, pale carved wood and bark armour, green-gold leaves, a hint of grey rot.` |
| `HERO-academy` | `Academy heroes: humans of the frozen mage-cities, fur collars, frost, verdigris bronze, dark glass, pale-blue rune light.` |
| `HERO-necropolis` | `Necropolis heroes: necromancers, vampires and the risen dead, grey and bone-pale skin, rusted black iron, rotting grey cloth, a cold green glow in the eyes.` |
| `HERO-inferno` | `Inferno heroes: demon lords and demon-born, ash-grey and slag-black skin cracked with glowing ember seams, horns, blackened bronze, smoke.` |
| `HERO-dungeon` | `Dungeon heroes: dark elves of the deep, ash-grey skin and white hair, black and dark-violet armour with thin silver inlay, a faint violet glow.` |
| `HERO-stronghold` | `Stronghold heroes: orc and goblin clan leaders, grey-green skin and tusks, hide, bone and rusted scrap iron, red-ochre war paint.` |
| `HERO-swamp-bestiary` | `Swamp Bestiary heroes: lizardfolk and beast-cult leaders of the drowned lowlands, wet scales, shells, reeds, bone and driftwood, moss green.` |

**`HERO-NEG`** (negative prompt, after `ICON-NEG` on every hero sheet):

```text
background scenery, frame, full body, two people in one cell, glamour, clean flawless skin, modern clothes, real-world symbols, another
game's character
```

**`TOWNICON-STYLE`** (style, the town sheets):

```text
Game town icon sheet, hand-painted dark-fantasy style, each icon a whole small fantasy town seen from above at a steep angle like on a
strategy map, standing on a small round patch of its own ground that fades out at the edge, one tall landmark toward the back, chunky
readable shapes, light from the top left. No photo realism, no 3D render look, no pixel art.
```

**`TOWNICON-NEG`** (negative prompt, after `ICON-NEG` on every town sheet):

```text
square ground tile, landscape, horizon, people, banners in colour, text, front view, another game's town
```

## Output for every icon

- **Sheet**: 2 × 2 at 1024 × 1024, every cell 512 × 512, transparent background; cut as the icons.
- **File**: hero portraits `icon_hero_<hero id>.png` with the id from `heroes.csv` (`icon_hero_haven.aldric.png`); town icons
  `icon_town_<town>.png` and `icon_town_<town>_village.png` (a town without a fort, *proposal*). 32-bit RGBA, 512 × 512.
- **Shape**: a portrait reads at 48 px (the hero list): the face is the largest shape; a town icon reads at 32 px.
- **Facing**: portraits face right; the game mirrors one where it needs.
- **Backdrop**: a portrait is transparent; the game puts its town's creature backdrop behind it
  ([creature-icon-prompts.md](creature-icon-prompts.md#backdrops)) *(proposal)*.
- **Telling apart**: the four heroes of a sheet differ in age, build, colouring and one telling detail (the Subject's last item, often
  their preferred creature or specialty); no two heroes of a town look alike.

## Haven

**Compose**: `HERO-STYLE` + `HERO-haven` + `HERO-WARRIOR` or `HERO-MAGE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `HERO-NEG` · 1024 × 1024

| # | File | Subject |
|---|---|---|
| W1 | `icon_hero_haven.aldric` | Aldric Vane: a broad-shouldered man in his forties, close-cropped grey hair, a scar across the chin, a dented steel gorget |
| W2 | `icon_hero_haven.maren` | Maren Holt: a weathered woman with a short braid under an open steel sallet, wind-burned cheeks, a pike shaft over the shoulder |
| W3 | `icon_hero_haven.corwin` | Corwin Ashby: a gaunt young man with a shaved head and burn scars on the neck, a zealot's rope belt over mail, a lit torch behind |
| W4 | `icon_hero_haven.brenna` | Brenna Stahl: a stern woman with dark red hair tied back, a quilted jacket, a crossbow bolt tucked behind the ear |
| W5 | `icon_hero_haven.edric` | Edric Morrow: an older man with a grey beard and tired kind eyes, a monk's hood thrown back over plate armour |
| W6 | `icon_hero_haven.sabine` | Sabine Kell: a sharp-eyed woman with short black hair, pauldrons trimmed with tawny griffin feathers |
| W7 | `icon_hero_haven.gerrit` | Gerrit Hale: a heavy-set man with a thick moustache, a knight's tall plumed helm resting on the shoulder |
| W8 | `icon_hero_haven.ottilie` | Ottilie Brand: a tall woman with a hard steady gaze, soot-dark plate with gilded edges, a sun etched on the breastplate |
| M1 | `icon_hero_haven.ansel` | Ansel Wren: a thin pale young priest, an off-white and gold stole, a small sun pendant glowing at the throat |
| M2 | `icon_hero_haven.ilse` | Ilse Varro: a woman with ash-blonde hair in a tight bun, grey vestments, eyes lit faintly gold |
| M3 | `icon_hero_haven.matthias` | Matthias Crane: an old bald priest with a long white beard, bandaged hands holding a small lantern |
| M4 | `icon_hero_haven.rowena` | Rowena Pell: a woman with a sooty face and singed sleeves, an alchemist's leather apron, a vial of yellow sulfur |
| M5 | `icon_hero_haven.fabian` | Fabian Lorne: a severe man with a black goatee, an inquisitor's high collar, a smoking iron censer |
| M6 | `icon_hero_haven.agathe` | Agathe Sorrel: an older woman in a grey hood edged with griffin feathers, a staff topped with a small iron sun-cage |
| M7 | `icon_hero_haven.cyprian` | Cyprian Vey: a calm man with silver temples, a faint ring of pale gold light behind the head |
| M8 | `icon_hero_haven.elodie` | Elodie Marsh: a determined young woman with freckles, an off-white tabard, a prayer book with brass clasps |

## Sylvan

**Compose**: `HERO-STYLE` + `HERO-sylvan` + `HERO-WARRIOR` or `HERO-MAGE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `HERO-NEG` · 1024 × 1024

| # | File | Subject |
|---|---|---|
| W1 | `icon_hero_sylvan.thalion` | Thalion: a male elf with long silver hair, a leaf-shaped steel pauldron, a tiny glowing pixie at the shoulder |
| W2 | `icon_hero_sylvan.faelar` | Faelar: a male elf with auburn hair, armour of bark plates, a curled satyr horn hung on a cord |
| W3 | `icon_hero_sylvan.ilyra` | Ilyra: a female elf with braided black hair, two slim blade hilts rising over the shoulders |
| W4 | `icon_hero_sylvan.caelwyn` | Caelwyn: a young male elf in a deep green hood, keen grey eyes, a small war drum on a strap |
| W5 | `icon_hero_sylvan.sylvara` | Sylvara: a female elf with copper hair, a longbow with antler-tipped limbs over the shoulder |
| W6 | `icon_hero_sylvan.elaith` | Elaith: a male elf with golden hair, a gold leaf brooch, a white pegasus feather in the hair |
| W7 | `icon_hero_sylvan.varis` | Varis: a male elf with grey eyes and dark hair, silvered mail, a moonstone circlet |
| W8 | `icon_hero_sylvan.neriah` | Neriah: a female elf with moss in her hair, bark armour, earrings of pale rose crystal |
| M1 | `icon_hero_sylvan.elowen` | Elowen: a female elf druid with roots and ivy woven into her hair, a green glow at the fingertips |
| M2 | `icon_hero_sylvan.taeral` | Taeral: a male elf in a mist-grey robe, a reed pipe, faint green motes drifting around him |
| M3 | `icon_hero_sylvan.ysolde` | Ysolde: a female elf with green-gold eyes, a staff with a fresh sprout growing from its tip |
| M4 | `icon_hero_sylvan.faenor` | Faenor: a male elf in a travel-worn cloak, a leather map case on the shoulder |
| M5 | `icon_hero_sylvan.ithiel` | Ithiel: a male elf with an amber amulet holding a trapped insect, a thin cloud of tiny insects around him |
| M6 | `icon_hero_sylvan.sariel` | Sariel: a female elf in white robes, a mantle of white pegasus feathers |
| M7 | `icon_hero_sylvan.calen` | Calen: a male elf with patches of bark-like skin on one cheek, an oak staff |
| M8 | `icon_hero_sylvan.nymara` | Nymara: an ancient female elf with pale eyes, grey rot creeping over one hand, a carved memory tablet on a cord |

## Academy

**Compose**: `HERO-STYLE` + `HERO-academy` + `HERO-WARRIOR` or `HERO-MAGE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `HERO-NEG` · 1024 × 1024

| # | File | Subject |
|---|---|---|
| W1 | `icon_hero_academy.corvin` | Corvin Halde: a soot-smudged man with brass goggles pushed up on the forehead, a heavy wrench on the shoulder |
| W2 | `icon_hero_academy.stellan` | Stellan Frost: a broad man with a frost-white beard, fur-lined steel armour, a dwarven war hammer |
| W3 | `icon_hero_academy.ingrid` | Ingrid Vael: a woman with grey braids and a stone-grey cloak, a small granite gargoyle perched on the shoulder |
| W4 | `icon_hero_academy.tobias` | Tobias Rime: a heavy-jawed man with a riveted iron pauldron, a gauntlet lit with pale-blue runes |
| W5 | `icon_hero_academy.hedda` | Hedda Strom: a woman with icy blue eyes under a white fur hood, a crossbow rimmed with frost |
| W6 | `icon_hero_academy.kasimir` | Kasimir Lode: a portly man in a heavy fur coat, a gold chain of office across the chest |
| W7 | `icon_hero_academy.runa` | Runa Halvard: a woman with wind-blown storm-grey hair, a broken bronze chain wrapped around the forearm |
| W8 | `icon_hero_academy.dorian` | Dorian Quill: a sharp-eyed man in a dark-teal scale cloak, a brass spyglass in the hand |
| M1 | `icon_hero_academy.aurelian` | Aurelian Vex: a young man with dark curls, a small arrow of pale-blue light hovering by the hand |
| M2 | `icon_hero_academy.seraphine` | Seraphine Lux: a woman with silver hair, a verdigris bronze circlet, a pale-blue rune glowing on the brow |
| M3 | `icon_hero_academy.theodric` | Theodric Wynn: an old man with a long white beard, a small bronze hourglass amulet |
| M4 | `icon_hero_academy.isolde` | Isolde Marrow: a severe woman with a dark-glass monocle, a large iron key on a chain |
| M5 | `icon_hero_academy.cassius` | Cassius Thorne: a lean man with frost in his beard, a shard of ice floating above the palm |
| M6 | `icon_hero_academy.mirela` | Mirela Quist: a woman with ink-stained fingers, a quill behind the ear, a rolled star chart |
| M7 | `icon_hero_academy.lucan` | Lucan Esk: a bald man tattooed with fading runes, a cracked ring of pale-blue light around one wrist |
| M8 | `icon_hero_academy.valeska` | Valeska Rune: a proud woman with a circlet set with rose-red crystal shards, a dark fur collar |

## Necropolis

**Compose**: `HERO-STYLE` + `HERO-necropolis` + `HERO-WARRIOR` or `HERO-MAGE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `HERO-NEG` · 1024 × 1024

| # | File | Subject |
|---|---|---|
| W1 | `icon_hero_necropolis.morthal` | Morthal: a gaunt skull-like face with thin grey skin, rusted black armour, cold green eyes |
| W2 | `icon_hero_necropolis.vexa` | Vexa: a pale woman with a beaked plague mask pushed up on the head, green fumes curling at the collar |
| W3 | `icon_hero_necropolis.grimwald` | Grimwald: a hulking risen warrior with grey skin and an iron brace holding the jaw |
| W4 | `icon_hero_necropolis.ulric` | Ulric Ashgrave: a gaunt grey-skinned nobleman, a collar of spider-web lace, a gold signet ring |
| W5 | `icon_hero_necropolis.hespera` | Hespera: a vampire woman with black hair, red eyes and a high black collar |
| W6 | `icon_hero_necropolis.corvane` | Corvane: a revenant knight, half the face bare bone, a tattered shroud as a cloak |
| W7 | `icon_hero_necropolis.drusa` | Drusa: a woman in black plate with a horned helm under the arm, a skull on the pauldron |
| W8 | `icon_hero_necropolis.malgrin` | Malgrin: a skeletal warrior with a crown of black iron and a rotting fur mantle |
| M1 | `icon_hero_necropolis.necrys` | Necrys: a hooded necromancer with a sunken face, a staff topped with a skull, green light in the eye sockets |
| M2 | `icon_hero_necropolis.sepulka` | Sepulka: an old woman with grave dirt on the hands and a necklace of finger bones |
| M3 | `icon_hero_necropolis.vorlath` | Vorlath: a thin man with sunken grey eyes and black lips, a twisted iron curse charm |
| M4 | `icon_hero_necropolis.ysmera` | Ysmera: a pale woman with silver tears of mercury on the cheeks, a spider brooch |
| M5 | `icon_hero_necropolis.kael` | Kael Duskmire: a vampire noble with slicked dark hair, a goblet of dark red |
| M6 | `icon_hero_necropolis.lethe` | Lethe: a translucent ghostly woman, hair floating as if under water |
| M7 | `icon_hero_necropolis.ozmund` | Ozmund: a bloated man in stained plague robes, a black rat on the shoulder |
| M8 | `icon_hero_necropolis.thessaly` | Thessaly: a lich with a jewelled skull, a glowing green phylactery on a chain |

## Inferno

**Compose**: `HERO-STYLE` + `HERO-inferno` + `HERO-WARRIOR` or `HERO-MAGE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `HERO-NEG` · 1024 × 1024

| # | File | Subject |
|---|---|---|
| W1 | `icon_hero_inferno.azgaroth` | Azgaroth: a horned demon lord with scarred black skin and ember eyes, a small imp on the shoulder |
| W2 | `icon_hero_inferno.brakka` | Brakka: a brutish demon with heavy ram horns, armour of slag plates |
| W3 | `icon_hero_inferno.vorhaz` | Vorhaz: a lean demon with a hound-skull pauldron, smoke curling from the mouth |
| W4 | `icon_hero_inferno.skarn` | Skarn: a demon with iron nails driven into the skin, hooked blades rising behind the shoulders |
| W5 | `icon_hero_inferno.ixira` | Ixira: a female demon with long swept horns and folded black wings |
| W6 | `icon_hero_inferno.malphor` | Malphor: a grinning demon with gold teeth and gold rings in the horns |
| W7 | `icon_hero_inferno.zarduk` | Zarduk: a huge demon with a bull-skull face, chains across the chest |
| W8 | `icon_hero_inferno.kezra` | Kezra: a lean female demon in slag armour, a bone spyglass on the belt |
| M1 | `icon_hero_inferno.belzra` | Belzra: a female demon with short horns, a ball of fire burning in the palm |
| M2 | `icon_hero_inferno.nyxoth` | Nyxoth: a demon with glassy black skin and glowing ember-red runes |
| M3 | `icon_hero_inferno.ashmedai` | Ashmedai: a tall demon crowned with flame, cold amused eyes |
| M4 | `icon_hero_inferno.lilivex` | Lilivex: a female demon with a mask of iron nails pushed aside, a sly smile |
| M5 | `icon_hero_inferno.pyrrhon` | Pyrrhon: a demon with eyes of fire and a screaming pale mask hung on the chest |
| M6 | `icon_hero_inferno.veshra` | Veshra: a female demon with a mane of fire, a horse skull as a shoulder guard |
| M7 | `icon_hero_inferno.orzhul` | Orzhul: an old demon with a long beard of flame and a staff of cooled slag |
| M8 | `icon_hero_inferno.sythra` | Sythra: a female demon with gold coins melted into the armour |

## Dungeon

**Compose**: `HERO-STYLE` + `HERO-dungeon` + `HERO-WARRIOR` or `HERO-MAGE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `HERO-NEG` · 1024 × 1024

| # | File | Subject |
|---|---|---|
| W1 | `icon_hero_dungeon.vaelrith` | Vaelrith: a male dark elf with long white hair, black armour, a cloak of pale troglodyte hide |
| W2 | `icon_hero_dungeon.zhaun` | Zhaun: a male dark elf in a scout's black hood, a thin silver band across the eyes |
| W3 | `icon_hero_dungeon.ilvara` | Ilvara: a female dark elf with small silver snakes coiled in her hair ornament |
| W4 | `icon_hero_dungeon.kharis` | Kharis: a male dark elf with a helm crowned by two bull horns |
| W5 | `icon_hero_dungeon.shyntra` | Shyntra: a female dark elf in armour of black lizard scales, a lance at the shoulder |
| W6 | `icon_hero_dungeon.vorn` | Vorn: a male dark elf heavy with gold jewellery, a cold smile |
| W7 | `icon_hero_dungeon.lysara` | Lysara: a female dark elf with a helm plume shaped like a scorpion's tail |
| W8 | `icon_hero_dungeon.xundar` | Xundar: a male dark elf with an amulet of one great violet eye |
| M1 | `icon_hero_dungeon.nethra` | Nethra: a female dark-elf sorceress, the eyes hidden behind a black veil |
| M2 | `icon_hero_dungeon.sabal` | Sabal: a male dark elf with violet tattoos glowing along the jaw |
| M3 | `icon_hero_dungeon.ghaunis` | Ghaunis: a female dark elf half wrapped in drifting shadow smoke |
| M4 | `icon_hero_dungeon.zesyra` | Zesyra: a female dark elf with a staff of carved minotaur bone |
| M5 | `icon_hero_dungeon.dryel` | Dryel: a male dark elf with a grief-worn face and violet tears |
| M6 | `icon_hero_dungeon.vhaeris` | Vhaeris: a female dark elf with a tall silver headdress like spider legs |
| M7 | `icon_hero_dungeon.phyrra` | Phyrra: a female dark elf with black lightning crackling between the fingers |
| M8 | `icon_hero_dungeon.ilphar` | Ilphar: a male dark elf with an amulet of many small eyes |

## Stronghold

**Compose**: `HERO-STYLE` + `HERO-stronghold` + `HERO-WARRIOR` or `HERO-MAGE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `HERO-NEG` · 1024 × 1024

| # | File | Subject |
|---|---|---|
| W1 | `icon_hero_stronghold.grukk` | Grukk: a scarred goblin chieftain in a dented helmet too big for the head |
| W2 | `icon_hero_stronghold.mazga` | Mazga: a female orc with a headdress of harpy feathers, a flask of quicksilver on a cord |
| W3 | `icon_hero_stronghold.thrak` | Thrak: an orc with one broken tusk, a red-ochre hand print across the face |
| W4 | `icon_hero_stronghold.urzog` | Urzog: an orc with a long horse-tail plume on the helmet, a spear at the shoulder |
| W5 | `icon_hero_stronghold.borka` | Borka: a huge half-ogre with a flat nose, a bone club on the shoulder |
| W6 | `icon_hero_stronghold.kragash` | Kragash: an orc with a wolf-pelt hood and gold teeth |
| W7 | `icon_hero_stronghold.shara` | Shara: a female orc with eagle-sharp eyes, giant roc feathers braided into the hair |
| W8 | `icon_hero_stronghold.durgash` | Durgash: an orc with a leather eye patch and a necklace of a giant's teeth |
| M1 | `icon_hero_stronghold.ugra` | Ugra: an old female goblin shaman with a bone rattle |
| M2 | `icon_hero_stronghold.zhaka` | Zhaka: a female orc in a feathered cloak, mouth open in a war cry |
| M3 | `icon_hero_stronghold.mograth` | Mograth: an orc shaman with a red-ochre painted face, smoke rising from a bowl |
| M4 | `icon_hero_stronghold.yazna` | Yazna: a female orc with a horse-skull mask pushed up on the head |
| M5 | `icon_hero_stronghold.kurgh` | Kurgh: a horned ogre shaman with a small flame in the palm |
| M6 | `icon_hero_stronghold.ashka` | Ashka: a female orc with a wolf-tooth necklace, howling |
| M7 | `icon_hero_stronghold.vrolk` | Vrolk: an orc with storm-grey feathers and white lightning marks painted on the skin |
| M8 | `icon_hero_stronghold.nazgra` | Nazgra: an old orc with a third eye painted on the forehead |

## Swamp Bestiary

**Compose**: `HERO-STYLE` + `HERO-swamp-bestiary` + `HERO-WARRIOR` or `HERO-MAGE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `HERO-NEG` · 1024 × 1024

| # | File | Subject |
|---|---|---|
| W1 | `icon_hero_swamp-bestiary.ssarkh` | Ssarkh: a green-scaled lizardfolk warrior with a pauldron made of a giant shell |
| W2 | `icon_hero_swamp-bestiary.kethra` | Kethra: a female lizardfolk with yellow sulfur crust on her scale markings, a pouch of sulfur |
| W3 | `icon_hero_swamp-bestiary.mudjaw` | Mudjaw: a crocodile-headed brute caked in drying mud |
| W4 | `icon_hero_swamp-bestiary.vessk` | Vessk: a lean lizardfolk with blades made of mantis claws |
| W5 | `icon_hero_swamp-bestiary.brakkis` | Brakkis: a lizardfolk with a helm made from a basilisk's spiny crest |
| W6 | `icon_hero_swamp-bestiary.ylsa` | Ylsa: a female beast-folk with owl feathers around the face, sharp golden eyes |
| W7 | `icon_hero_swamp-bestiary.tharg` | Tharg: a massive lizardfolk in armour of dark iron-grey gorgon plates |
| W8 | `icon_hero_swamp-bestiary.oszrak` | Oszrak: a lizardfolk with a cloak of wyvern wing leather, a spyglass made of shell |
| M1 | `icon_hero_swamp-bestiary.hissla` | Hissla: a female lizardfolk shaman with reeds tied into her crest |
| M2 | `icon_hero_swamp-bestiary.sethek` | Sethek: a shaman with a turtle-shell hood, a staff of driftwood |
| M3 | `icon_hero_swamp-bestiary.murra` | Murra: a female beast-folk wearing a striped jungle-cat pelt, green war paint |
| M4 | `icon_hero_swamp-bestiary.zhessk` | Zhessk: a shaman with a cloak of translucent insect wings |
| M5 | `icon_hero_swamp-bestiary.ilthra` | Ilthra: a female lizardfolk with glowing green eyes, patches of scales turned to stone |
| M6 | `icon_hero_swamp-bestiary.gossk` | Gossk: an old beast-folk shaman with an owlbear-claw necklace, roaring |
| M7 | `icon_hero_swamp-bestiary.venna` | Venna: a female lizardfolk with iron-grey scales, a curl of green vapour at the lips |
| M8 | `icon_hero_swamp-bestiary.szask` | Szask: a lizardfolk shaman wearing a wyvern skull as a headdress |

## Towns

**Compose**: `TOWNICON-STYLE` + `ICON-SHEET-4` + cells · negative `ICON-NEG` + `TOWNICON-NEG` · 1024 × 1024

Each sheet holds two towns: cells 1–2 the towns with their walls (`icon_town_<town>`), cells 3–4 the same towns as villages without
walls (`icon_town_<town>_village`): "the same town as the one above it, as a small village: a few houses around the bare foundation of its
landmark, no walls". The subjects follow the town models ([town-model-prompts.md](town-model-prompts.md)).

| Sheet | Town | Subject (walled) |
|---|---|---|
| T1 | `haven` | a walled hill-town of soot-grey limestone and dark slate roofs, a cathedral keep crowned by an iron cage holding a burning sun-brazier |
| T1 | `sylvan` | a ring of colossal living trees forming the walls, halls in their crowns, one vast half-bare ancient tree at the back |
| T2 | `academy` | a frozen citadel of blue-grey granite with verdigris domes, a domed observatory holding a bronze orrery of rings |
| T2 | `necropolis` | stacked black ruins of many ages, ossuary walls of skulls, a crooked black spire with a cold green light |
| T3 | `inferno` | a jagged fortress of black slag with glowing lava cracks, sickle-shaped spires, an altar-furnace venting smoke |
| T3 | `dungeon` | a town sunk into a cavern sinkhole, black and violet stepped pyramids, a spire topped by a glowing violet crystal |
| T4 | `stronghold` | a log palisade around hide yurts and tents, a great lodge under a huge horned beast skull |
| T4 | `swamp-bestiary` | half-sunken tilted stone ruins with stilt huts, a rampart of giant shells, a tall totem of coiled beasts |
| T5 | `conflux` | a ring-shaped sanctuary around a colossal half-buried gear of stone and green bronze, four small elemental shrines |
| T5 | `dragon-utopia` | a stepped pyramid eyrie carved into a sandstone crag, huge perches, a broken colossal dragon statue |

## After generating

1. Put a town's sixteen portraits side by side at 48 px: each face is distinct, the town's look is shared.
2. Check that each portrait faces right and that the face, not the gear, is the largest shape.
3. Put the town icons side by side at 32 px: each town is named at a glance, walled and village apart.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/heroes/` and `textures/ui/towns/` *(proposal)* and add a line to each
   folder's `CREDITS.md` with the tool and its licence terms.
