# Creature icon prompts: Academy

The Academy portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells by the shared **Cell rule** (top row the bases, bottom row their upgrades).

## Blocks

**`CREATURE-academy`** (look, second in every prompt of this file):

```text
Academy creatures: mages, dwarves and bound constructs of the frozen peaks, blue-grey granite, frost, verdigris bronze, dark glass,
faint pale-blue rune light; cold and scholarly.
```

**`BACKDROP-academy`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_academy.png`):

```text
snowy mountain peaks under a cold pale-blue sky, a distant domed observatory
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-academy` + `ICON-SHEET-6` + cells (Cell rule) · negative `ICON-NEG` + `CREATURE-NEG` · 1536 × 1024

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| A1 | `icon_creature_academy.gremlin` | a small big-eared gremlin with grey-green skin and a toothy grin, dragging an iron ball on a chain, the whole body shown | `icon_creature_academy.gremlin-master` | brass goggles, a leather apron full of tools, a small sparking rune-lit hand cannon |
| A1 | `icon_creature_academy.dwarf` | a stocky dwarf in a frost-rimmed steel helmet and fur cloak, a braided red beard, a war hammer | `icon_creature_academy.dwarf-defender` | heavy plate with verdigris bronze rune plates, a large square shield, the beard braided with iron rings |
| A1 | `icon_creature_academy.gargoyle` | a crouching gargoyle of cracked grey granite, bat wings folded, small horns | `icon_creature_academy.obsidian-gargoyle` | glossy black obsidian with sharp facets, faint pale-blue runes in the cracks |
| A2 | `icon_creature_academy.iron-golem` | a hulking golem of riveted iron plates, a dark glass eye slit glowing pale blue | `icon_creature_academy.animated-armor` | an empty suit of ornate plate standing on its own, pale-blue light inside the helmet, verdigris bronze trim |
| A2 | `icon_creature_academy.snow-maiden` | a pale young spirit of living snow and frost, hair of icicles, a gown of drifting snowflakes | `icon_creature_academy.enchantress` | a crown of ice crystals, a long staff of blue ice, frost spreading from the hands |
| A2 | `icon_creature_academy.sorcerer` | a human sorcerer in a dark blue robe with a fur collar, a pale-blue rune glowing in an open palm | `icon_creature_academy.mage` | older, a verdigris bronze circlet, a staff topped with a tiny orrery of rings, a small orange flame in the other hand |
| A3 | `icon_creature_academy.djinn` | a bound air spirit whose body is a swirl of pale-blue cloud below the waist, a stern face of mist, verdigris bronze shackles with broken chains on its wrists | `icon_creature_academy.djinn-sultan` | a larger body crackling with small lightning, a crown of floating bronze rings, the shackles lit with runes |
| A3 | `icon_creature_academy.naga` | a guardian with a human upper body in verdigris bronze scale armour and a coiled dark-teal serpent tail, two curved swords | `icon_creature_academy.naga-queen` | four arms each holding a blade, a tall bronze crown with a pale-blue gem, gold bands on the tail |
| A3 | `icon_creature_academy.colossus` | the head and shoulders of a giant carved from grey granite like a statue, pale-blue rune lines across the chest, a stone maul | `icon_creature_academy.titan` | living bronze instead of stone, a stern bearded face, a spear of crackling lightning, storm cloud around the shoulders |
