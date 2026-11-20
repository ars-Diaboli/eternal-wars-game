# Creature icon prompts: Swamp Bestiary

The Swamp Bestiary portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells by the shared **Cell rule** (top row the bases, bottom row their upgrades).

## Blocks

**`CREATURE-swamp-bestiary`** (look, second in every prompt of this file):

```text
Swamp Bestiary creatures: beasts and beast-folk of the drowned lowlands, wet scales, shells, reeds, driftwood and bone, algae and
moss green, muddy brown; feral and ancient.
```

**`BACKDROP-swamp-bestiary`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_swamp-bestiary.png`):

```text
misty green marsh, reeds and sunken ruins in still dark water
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-swamp-bestiary` + `ICON-SHEET-6` + cells (Cell rule) · negative `ICON-NEG` + `ICON-NEG-TOON` + `CREATURE-NEG` · 1536 × 1024

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| W1 | `icon_creature_swamp-bestiary.lizardman` | a green-scaled lizardman with a short spear and a shield made of a giant shell | `icon_creature_swamp-bestiary.lizard-warrior` | armour of large fish scales, a sword edged with teeth |
| W1 | `icon_creature_swamp-bestiary.kappa` | a small turtle-like river creature with a shell on its back, a beak and a water-filled hollow on its head, the whole body shown | `icon_creature_swamp-bestiary.kappa-shaman` | a necklace of shells, glowing teal markings, a jet of water spat from its beak |
| W1 | `icon_creature_swamp-bestiary.jungle-horror` | a lean big cat with moss-green striped fur, long fangs, swamp reeds caught in its fur | `icon_creature_swamp-bestiary.jungle-terror` | black stripes, bony ridges along the spine, glowing yellow eyes |
| W2 | `icon_creature_swamp-bestiary.mantis` | a giant green mantis with raised scythe forelegs, the whole body shown | `icon_creature_swamp-bestiary.serpent-fly` | a serpent's body with four translucent dragonfly wings and a long stinger |
| W2 | `icon_creature_swamp-bestiary.basilisk` | a heavy six-legged lizard with grey-green scales and a crest of spines, eyes glowing faint green | `icon_creature_swamp-bestiary.greater-basilisk` | a larger bone crest, patches of scales turned to grey stone, piercing yellow-green eyes |
| W2 | `icon_creature_swamp-bestiary.owlbear` | a bear-bodied beast with an owl's face and beak, grey and brown feathers on its shoulders | `icon_creature_swamp-bestiary.elder-owlbear` | a white feathered ruff, huge claws, moss growing on its back |
| W3 | `icon_creature_swamp-bestiary.gorgon` | a heavy bull covered in dark iron-grey metallic plates, steam from its nostrils | `icon_creature_swamp-bestiary.mighty-gorgon` | green corroded plates, a curl of green vapour from its mouth |
| W3 | `icon_creature_swamp-bestiary.wyvern` | a two-legged winged wyvern with moss-green scales and a barbed tail | `icon_creature_swamp-bestiary.wyvern-monarch` | a crest of horns like a crown, darker teal scales, a stinger dripping venom |
| W3 | `icon_creature_swamp-bestiary.hydra` | a serpent hydra with five heads on long necks, dark green scales | `icon_creature_swamp-bestiary.chaos-hydra` | seven heads, black and green scales, venom dripping, broken shell plates on its back |
