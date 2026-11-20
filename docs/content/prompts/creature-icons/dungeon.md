# Creature icon prompts: Dungeon

The Dungeon portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells by the shared **Cell rule** (top row the bases, bottom row their upgrades).

## Blocks

**`CREATURE-dungeon`** (look, second in every prompt of this file):

```text
Dungeon creatures: dark elves and lightless beasts of the deep, ash-grey and black skin, black and dark-violet armour with thin
silver inlay, a faint violet glow; cold, elegant, patient.
```

**`BACKDROP-dungeon`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_dungeon.png`):

```text
a vast dark cavern, violet glowing crystals and pale mushrooms in the distance
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-dungeon` + `ICON-SHEET-6` + cells (Cell rule) · negative `ICON-NEG` + `ICON-NEG-TOON` + `CREATURE-NEG` · 1536 × 1024

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| D1 | `icon_creature_dungeon.troglodyte` | a pale eyeless cave dweller with grey skin and a lizard-like face, a crude stone-tipped spear | `icon_creature_dungeon.deep-troglodyte` | violet painted scars, a bone helmet, a spear in each hand |
| D1 | `icon_creature_dungeon.scout` | a dark-elf scout with ash-grey skin and white hair under a black hood, two curved daggers | `icon_creature_dungeon.stalker` | a black mask over the lower face, violet-edged blades, a cloak melting into shadow |
| D1 | `icon_creature_dungeon.medusa` | a medusa with green scales, hair of small snakes and a long serpent tail, a curved silver dagger, eyes glowing faint violet | `icon_creature_dungeon.putrid-medusa` | pale diseased scales, the hair-snakes dripping venom, a ragged black veil |
| D2 | `icon_creature_dungeon.minotaur` | a hulking bull-headed minotaur with dark fur, a double-bladed axe of black iron with silver inlay | `icon_creature_dungeon.minotaur-guard` | black plate on the shoulders, a horned helm, a larger axe glowing faint violet |
| D2 | `icon_creature_dungeon.dark-raider` | a dark-elf rider on a large black two-legged lizard, a long curved lance | `icon_creature_dungeon.grim-raider` | the lizard in spiked black plate, the rider in a violet-plumed helm with a barbed spear |
| D2 | `icon_creature_dungeon.shadow-mistress` | a dark-elf sorceress in black and violet robes, white hair, an orb of violet shadow held in both hands | `icon_creature_dungeon.shadow-matriarch` | older, a tall silver headdress shaped like spider legs, violet glowing eyes |
| D3 | `icon_creature_dungeon.manticore` | a manticore with a lion's body, dark leathery bat wings, a scorpion tail and a fierce face | `icon_creature_dungeon.scorpicore` | a black chitin carapace over the back, a huge stinger glowing violet |
| D3 | `icon_creature_dungeon.spectator` | a floating sphere of black stone-like hide with one great violet eye, a fringe of pale tendrils hanging beneath it like a jellyfish | `icon_creature_dungeon.beholder` | larger, a second smaller eye above the great one, the tendrils tipped with violet lights |
| D3 | `icon_creature_dungeon.shadow-horror` | a tall creature of living darkness with long blade-like arms and a pale mask-like face | `icon_creature_dungeon.shadow-reaper` | a hood of shadow, a huge violet-edged scythe, black smoke streaming off it |
