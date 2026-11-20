# Creature icon prompts (for an image-generating model)

Prompts for the **creature portraits**: the army slots of the hero, town and exchange screens, the tavern, the dwellings, the battle's
initiative bar and the creature windows. One portrait for each creature in `creatures.csv`, generated as **sheets of 6 (2 × 3)** like the
icons ([icon-prompts.md](icon-prompts.md)): a town sheet holds three creatures in its top row and **their upgrades directly below
them**, so each pair comes out as the same creature. The designs are original: the prompts never imitate another game's creatures.

## Compose a prompt

Every sheet below has a **Compose** line naming its blocks. Build the prompt from blocks, copying each block's text exactly and joining
them with a space:

```text
PROMPT   = CREATURE-STYLE + CREATURE-<town> + ICON-SHEET-6 (or ICON-SHEET-4) + CELLS   (sheets in the faction files)
CELLS    = "1 <subject 1>; 2 <subject 2>; …"   from the sheet's table, by its Cell rule
NEGATIVE = ICON-NEG + ", " + CREATURE-NEG   (only where the model takes a negative prompt)
OUTPUT   = the size on the Compose line; cut the sheet and save each cell as <File>.png
```

**Cell rule** (town and neutral pair sheets): cells 1–3 are the **Base** subjects of the sheet's three rows, in order; cells 4–6 are
"the same creature as the one above it, upgraded, bigger and more dangerous: " + **Upgrade adds**, in the same order. Sheets of single
creatures list their cells in full. `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in [icon-prompts.md](icon-prompts.md#blocks). Do
not paraphrase blocks or subjects.

## Blocks

**`CREATURE-STYLE`** (style, first in every prompt):

```text
Game creature portrait sheet, hand-painted dark-fantasy style, rich painted detail on bold readable shapes, worn realistic materials,
soft painted shading, light from the top left, a thin dark outline around each subject. Each portrait shows the creature from the
chest up in three-quarter view facing right, head and weapon inside the middle of the cell; small creatures and beasts show the whole
body, giant creatures and dragons only the head and neck. Grim, weathered, never cute. No photo realism, no 3D render look, no pixel
art.
```

**`CREATURE-NEG`** (negative prompt, after `ICON-NEG` on every creature sheet):

```text
background scenery, ground, frame, card border, full-body figure for a large creature, cropped head, two creatures in one cell, cartoon,
chibi, cute, toy, glossy plastic, modern clothes, real-world symbols, another game's creature design
```

**`CREATURE-<town>`** and **`BACKDROP-<town>`**: one of each in every faction's file (below).

**`BACKDROP-STYLE`** (style, the one backdrop a town, see [Backdrops](#backdrops)):

```text
Square painted background for a creature portrait, hand-painted dark-fantasy style, soft and blurred like a distant landscape, no
creature, no figures, no text, darker toward the edges, the middle third calm and even so a portrait reads on it.
```

## Output for every portrait

- **Sheet**: 2 × 3 at 1536 × 1024 or 2 × 2 at 1024 × 1024, every cell 512 × 512, transparent background; cut as the icons.
- **File**: `icon_creature_<creature id>.png` with the id from `creatures.csv` (`icon_creature_haven.footman.png`), 32-bit RGBA, 512 × 512.
- **Framing**: the army slots are wider than tall, so the game may show only the middle band of the cell: the **head (or face) and the
  weapon stay in the middle 60 % of the cell's height**.
- **Facing**: always to the right, the attacker's side; the game mirrors a portrait where it needs the other way.
- **Telling apart**: a base creature and its upgrade share the silhouette; the upgrade is bigger, better armed and more ornate. Two
  creatures of one town never share a silhouette.
- **Backdrop**: the portrait itself is transparent; the game puts the town's backdrop behind it *(proposal)*.

## Factions

One file a faction: its look and backdrop blocks and its sheets.

| File | Blocks |
|---|---|
| [Haven](creature-icons/haven.md) | `CREATURE-haven`, `BACKDROP-haven` |
| [Sylvan](creature-icons/sylvan.md) | `CREATURE-sylvan`, `BACKDROP-sylvan` |
| [Academy](creature-icons/academy.md) | `CREATURE-academy`, `BACKDROP-academy` |
| [Necropolis](creature-icons/necropolis.md) | `CREATURE-necropolis`, `BACKDROP-necropolis` |
| [Inferno](creature-icons/inferno.md) | `CREATURE-inferno`, `BACKDROP-inferno` |
| [Dungeon](creature-icons/dungeon.md) | `CREATURE-dungeon`, `BACKDROP-dungeon` |
| [Stronghold](creature-icons/stronghold.md) | `CREATURE-stronghold`, `BACKDROP-stronghold` |
| [Swamp Bestiary](creature-icons/swamp-bestiary.md) | `CREATURE-swamp-bestiary`, `BACKDROP-swamp-bestiary` |
| [Conflux](creature-icons/conflux.md) | `CREATURE-conflux`, `BACKDROP-conflux` |
| [Dragon Utopia](creature-icons/dragon-utopia.md) | `CREATURE-dragon-utopia`, `BACKDROP-dragon-utopia` |
| [Neutral](creature-icons/neutral.md) | `CREATURE-neutral`, `BACKDROP-neutral` |

## Backdrops

One painted backdrop a faction, generated alone: `BACKDROP-STYLE` + `BACKDROP-<town>`, 1024 × 1024, saved as
`creature_backdrop_<town>.png` (opaque, the game scales it). A creature keeps its own faction's backdrop wherever it is hired; neutral
creatures use `neutral`. Hero portraits use the same backdrops ([hero-town-icon-prompts.md](hero-town-icon-prompts.md)).

## After generating

1. Put each base and its upgrade side by side: the same creature, clearly upgraded.
2. Put a town's eighteen portraits side by side at 48 px: each is named at a glance, no two share a silhouette.
3. Crop a portrait to the army slot's wide band (about 16 : 9 through the middle): the face and the weapon still show.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/creatures/` *(proposal)* and add a line to its `CREDITS.md` with the
   tool and its licence terms.
