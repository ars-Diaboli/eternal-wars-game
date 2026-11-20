# Creature icon prompts: Stronghold

The Stronghold portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells by the shared **Cell rule** (top row the bases, bottom row their upgrades).

## Blocks

**`CREATURE-stronghold`** (look, second in every prompt of this file):

```text
Stronghold creatures: orc clans and the beasts of the badlands, raw hide, rope, bone and rusted scrap iron, dust, red-ochre war
paint; rough and enduring.
```

**`BACKDROP-stronghold`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_stronghold.png`):

```text
dusty ochre badlands with jagged rocks under a hazy orange sky
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-stronghold` + `ICON-SHEET-6` + cells (Cell rule) · negative `ICON-NEG` + `ICON-NEG-TOON` + `CREATURE-NEG` · 1536 × 1024

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| T1 | `icon_creature_stronghold.goblin` | a small wiry green goblin with big ears and a sly grin, a hide vest and a crude spear | `icon_creature_stronghold.hobgoblin` | taller, a dented scrap-iron helmet, a spiked club, red-ochre war paint |
| T1 | `icon_creature_stronghold.harpy` | a harpy with a wild-haired human head and torso, dusty grey wings and taloned bird legs | `icon_creature_stronghold.fury` | black wings, a red-ochre painted face, iron caps on the talons |
| T1 | `icon_creature_stronghold.orc` | a muscular grey-green orc with tusks, a leather harness and a heavy cleaver | `icon_creature_stronghold.orc-berserker` | a bare chest covered in red-ochre scars, an axe in each hand, a bone necklace, a furious roar |
| T2 | `icon_creature_stronghold.centaur` | a centaur with a dusty dun horse body and a weathered human torso, a spear and a hide shield | `icon_creature_stronghold.centaur-warchief` | a horned beast-skull helmet, a red-ochre painted war cape, a long spear hung with feathers |
| T2 | `icon_creature_stronghold.ogre` | a huge pot-bellied ogre with grey skin and one tusk, a tree-trunk club on the shoulder | `icon_creature_stronghold.ogre-mage` | a shaman's headdress of horns, red-ochre runes on its skin, a bone staff, a small flame in its palm |
| T2 | `icon_creature_stronghold.wolf-rider` | a goblin rider on a big grey wolf, a curved blade raised | `icon_creature_stronghold.wolf-raider` | the wolf armoured in scrap-iron plates, the rider in a horned helmet with a spear |
| T3 | `icon_creature_stronghold.roc` | a giant eagle-like bird with dusty plumage, wings spread, a hooked beak | `icon_creature_stronghold.thunderbird` | dark grey-blue feathers crackling with lightning, a crest of white feathers, eyes glowing white |
| T3 | `icon_creature_stronghold.cyclops` | a one-eyed giant with grey skin and hide clothing, a boulder held overhead | `icon_creature_stronghold.cyclops-king` | a crown of beast teeth and rusted iron, red-ochre war paint, a larger boulder |
| T3 | `icon_creature_stronghold.behemoth` | the head and forequarters of a massive shaggy beast with huge claws and a flat face of fangs, dusty brown fur | `icon_creature_stronghold.ancient-behemoth` | grey-white fur, a ridge of bone spikes along the back, broken chain shackles on its forelegs |
