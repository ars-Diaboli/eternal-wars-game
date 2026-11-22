# Creature icon prompts (for an image-generating model)

Prompts for the **creature portraits**: the army slots of the hero, town and exchange screens, the tavern, the dwellings, the battle's
initiative bar and the creature windows. One portrait for each creature in `creatures.csv`, generated as **sheets of 4 (2 × 2)**: a
town sheet holds two creatures in its top row and **their upgrades directly below them**, so each pair comes out as the same creature;
a town's ninth pair fills the left column of its last sheet, and the right column takes a second try at each. The designs are
original: the prompts never imitate another game's creatures.

## Use a prompt

1. Open the faction's file (see [Factions](#factions)) and copy a sheet's prompt block whole.
2. Where the model takes a negative prompt, paste [`NEG-CREATURE`](#negative-prompt) as it.
3. Generate at the size on the sheet's line, cut the sheet and save each cell as the **File** its table gives (`<File>.png`).
4. Do not paraphrase a prompt or a subject.

## Negative prompt

**`NEG-CREATURE`** (every creature sheet):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, scenery, ground shadow, photograph, plastic CGI render,
pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, cartoon, comic book, anime, manga,
chibi, cute, toy-like, childish, cel shading, flat colours, flat shading, vector art, clip art, sticker, emoji, mobile game icon,
casual game art, thick black outlines, ink line art, exaggerated proportions, oversized head, big eyes, rounded bubbly shapes, glossy
plastic, smooth airbrushed gradients, oversaturated colours, candy colours, background scenery, ground, frame, card border, full-body
figure for a large creature, cropped head, two creatures in one cell, monster-toy look, friendly expression, modern clothes, real-world
symbols, another game's creature design
```

## Shared wording

Every creature prompt repeats three passages word for word, so that each prompt is ready to paste: the creature style ("Game
creature portrait sheet in a realistic painterly…"), its faction's look ("Haven creatures: …" and so on) and the grid sentence ("4
separate icons arranged in a grid…"). Every backdrop prompt starts with the same backdrop style ("Square painted
background for a creature portrait…"). A change to one of them is made in every prompt that holds it: search the faction files for its
first words.

## Output for every portrait

- **Sheet**: 2 × 2 at 1024 × 1024, every cell 512 × 512, transparent background; cut as the icons.
- **File**: `icon_creature_<creature id>.png` with the id from `creatures.csv` (`icon_creature_haven.footman.png`), 32-bit RGBA, 512 × 512.
- **Framing**: the army slots are wider than tall, so the game may show only the middle band of the cell: the **head (or face) and the
  weapon stay in the middle 60 % of the cell's height**.
- **Facing**: always to the right, the attacker's side; the game mirrors a portrait where it needs the other way.
- **Telling apart**: a base creature and its upgrade share the silhouette; the upgrade is bigger, better armed and more ornate. Two
  creatures of one town never share a silhouette.
- **Backdrop**: the portrait itself is transparent; the game puts the town's backdrop behind it *(proposal)*.

## Factions

One file a faction: its sheets' prompts, which cell is which file, and its backdrop prompt.

| File |
|---|
| [Haven](creature-icons/haven.md) |
| [Sylvan](creature-icons/sylvan.md) |
| [Academy](creature-icons/academy.md) |
| [Necropolis](creature-icons/necropolis.md) |
| [Inferno](creature-icons/inferno.md) |
| [Dungeon](creature-icons/dungeon.md) |
| [Stronghold](creature-icons/stronghold.md) |
| [Swamp Bestiary](creature-icons/swamp-bestiary.md) |
| [Conflux](creature-icons/conflux.md) |
| [Dragon Utopia](creature-icons/dragon-utopia.md) |
| [Neutral](creature-icons/neutral.md) |

## Backdrops

One painted backdrop a faction, generated alone from the ready prompt under **Backdrop** in its faction's file, 1024 × 1024, saved
as `creature_backdrop_<town>.png` (opaque, the game scales it). A creature keeps its own faction's backdrop wherever it is hired; neutral
creatures use `neutral`. Hero portraits use the same backdrops ([hero-town-icon-prompts.md](hero-town-icon-prompts.md)).

## After generating

1. Put each base and its upgrade side by side: the same creature, clearly upgraded.
2. Put a town's eighteen portraits side by side at 48 px: each is named at a glance, no two share a silhouette.
3. Crop a portrait to the army slot's wide band (about 16 : 9 through the middle): the face and the weapon still show.
4. Check for a cartoon look (thick outlines, flat fills, big eyes, a friendly face, toy-like proportions) and regenerate such a
   sheet.
5. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/creatures/` *(proposal)* and add a line to its `CREDITS.md` with the
   tool and its licence terms.
