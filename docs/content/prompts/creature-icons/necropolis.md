# Creature icon prompts: Necropolis

The Necropolis portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells by the shared **Cell rule** (top row the bases, bottom row their upgrades).

## Blocks

**`CREATURE-necropolis`** (look, second in every prompt of this file):

```text
Necropolis creatures: the dead of many ages, grey bone, rusted iron, rotting grey cloth, ash, a cold green glow in eyes and wounds
only; ancient and patient.
```

**`BACKDROP-necropolis`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_necropolis.png`):

```text
grey ash plain under a sickly green sky, dead trees and leaning tombstones
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-necropolis` + `ICON-SHEET-6` + cells (Cell rule) · negative `ICON-NEG` + `CREATURE-NEG` · 1536 × 1024

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| N1 | `icon_creature_necropolis.skeleton` | a skeleton soldier with a rusted sword and a cracked round shield, cold green light in the eye sockets | `icon_creature_necropolis.skeleton-warrior` | rusted plate pieces bolted onto the bones, a tall shield with a bony hand emblem, a notched axe |
| N1 | `icon_creature_necropolis.plague-bearer` | a hunched bloated corpse wrapped in rags, carrying a dripping censer of green fumes | `icon_creature_necropolis.flesh-golem` | a hulking golem of corpses stitched together with iron staples, one enormous fist |
| N1 | `icon_creature_necropolis.ghoul` | a gaunt grey ghoul crouched low, long claws, a jaw of jagged teeth, sunken eyes | `icon_creature_necropolis.alghoul` | larger, skin peeled back to show bone, hooked bone spurs along the spine |
| N2 | `icon_creature_necropolis.wight` | a pale translucent figure in a tattered grey shroud, cold green eyes, reaching forward with long fingers | `icon_creature_necropolis.wraith` | a hood of black smoke and torn shroud, a cold green flame held between both hands |
| N2 | `icon_creature_necropolis.death-weaver` | a huge spider of black bone and grey chitin, a skull-like pattern on its back, cold green threads trailing | `icon_creature_necropolis.fate-weaver` | a pale bone-white body, legs ending in sickle points, a glowing green web spun behind it |
| N2 | `icon_creature_necropolis.vampire` | a pale gaunt vampire in a black high-collared coat, red eyes, fangs bared, a bat-wing cloak | `icon_creature_necropolis.vampire-lord` | silver hair, a tarnished iron crown, a coat embroidered in dark red, the cloak spread like wings |
| N3 | `icon_creature_necropolis.dark-knight` | a skeletal knight in black rusted plate on a skeletal horse, a long black lance | `icon_creature_necropolis.death-knight` | the horse barded in black iron with green fire in its eyes, a tall spiked helm, a great curved glaive |
| N3 | `icon_creature_necropolis.lich` | a skeletal sorcerer in rotting grey robes, a staff topped with a skull, cold green fire around one hand | `icon_creature_necropolis.arch-lich` | a black iron crown on the skull, robes hung with bone ornaments, a glowing green phylactery on a chain |
| N3 | `icon_creature_necropolis.bone-dragon` | the skull and neck of a dragon built of bleached bones, cold green fire in the eye sockets, broken wing bones behind | `icon_creature_necropolis.ghost-dragon` | a translucent grey-green ghostly body around the bones, trailing smoke |
