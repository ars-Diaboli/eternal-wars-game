# Creature icon prompts: Dragon Utopia

The Dragon Utopia portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells as listed.

## Blocks

**`CREATURE-dragon-utopia`** (look, second in every prompt of this file):

```text
Dragon Utopia creatures: the last dragons and their kin, old and scarred, heavy scales, horns worn smooth, weathered sandstone and
old bronze tones besides each dragon's own colour.
```

**`BACKDROP-dragon-utopia`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_dragon-utopia.png`):

```text
a sandstone crag with huge ledges under a hazy gold sky, distant winged shapes
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-dragon-utopia` + `ICON-SHEET-6` (U1) or `ICON-SHEET-4` (U2) + cells · negative `ICON-NEG` + `CREATURE-NEG` · 1536 × 1024 (U1), 1024 × 1024 (U2)

| Sheet | # | File | Subject |
|---|---|---|---|
| U1 | 1 | `icon_creature_dragon-utopia.haspid` | a wingless serpent-like dragon kin with a cobra hood of bronze scales, fangs bared |
| U1 | 2 | `icon_creature_dragon-utopia.royal-haspid` | the same serpent-like dragon kin, larger, a golden hood crested with horns, gem-studded scales |
| U1 | 3 | `icon_creature_dragon-utopia.dragonyte` | a dragon-headed humanoid warrior with sandstone-coloured scales, a broad cleaving axe of old bronze |
| U1 | 4 | `icon_creature_dragon-utopia.faerie-dragon` | a small dragon with soft gold and teal butterfly wings, tiny lightning sparks on its whiskers, the whole body shown |
| U1 | 5 | `icon_creature_dragon-utopia.red-dragon` | the head and neck of a red dragon, heavy swept horns, smoke from the nostrils, embers in the jaws |
| U1 | 6 | `icon_creature_dragon-utopia.green-dragon` | the head and neck of a green dragon with a frill of spines, green vapour curling from its mouth |
| U2 | 1 | `icon_creature_dragon-utopia.azure-dragon` | the head and neck of a deep-blue dragon with frost-white horns, icy breath |
| U2 | 2 | `icon_creature_dragon-utopia.gold-dragon` | the head and neck of a gold dragon with a mane of fine tendrils, lightning crackling between its horns |
| U2 | 3 | `icon_creature_dragon-utopia.black-dragon` | the head and neck of a black dragon with dull obsidian scales and spines, a violet-black void in its open jaws |
| U2 | 4 | — | spare: a second take of the black dragon |
