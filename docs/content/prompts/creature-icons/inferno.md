# Creature icon prompts: Inferno

The Inferno portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells by the shared **Cell rule** (top row the bases, bottom row their upgrades).

## Blocks

**`CREATURE-inferno`** (look, second in every prompt of this file):

```text
Inferno creatures: demons born from the ash of burned cities, slag-black and ash-grey skin cracked with glowing ember-red seams,
blackened bronze, horns and smoke; hungry and cruel.
```

**`BACKDROP-inferno`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_inferno.png`):

```text
black slag field with glowing lava cracks under a smoky red sky
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-inferno` + `ICON-SHEET-6` + cells (Cell rule) · negative `ICON-NEG` + `CREATURE-NEG` · 1536 × 1024

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| I1 | `icon_creature_inferno.imp` | a small red-skinned imp with bat wings, a long thin tail and a wide grin, a glowing coal in its claws, the whole body shown | `icon_creature_inferno.familiar` | darker skin with ember cracks, small horns, a blackened bronze collar, a wisp of blue mana pulled into its claws |
| I1 | `icon_creature_inferno.horned-demon` | a muscular ash-grey demon with curved ram horns and hooves, a jagged cleaver of blackened bronze | `icon_creature_inferno.horned-overseer` | larger horns ringed with bronze, a harness of spikes, a whip of chains |
| I1 | `icon_creature_inferno.hell-hound` | a lean black hound with cracked skin glowing with lava, smoke rising from its jaws | `icon_creature_inferno.cerberus` | three heads breathing embers, a spiked iron collar on each neck |
| I2 | `icon_creature_inferno.tormentor` | a thin grey demon with hooked blades instead of hands, a mask of iron nails over its face | `icon_creature_inferno.lacerator` | four bladed arms, skin split by ember-red cracks, chains hanging from hooks in its back |
| I2 | `icon_creature_inferno.succubus` | a horned demon with ash-grey skin, black bat wings and blackened bronze armour, a ball of red fire gathered in one hand | `icon_creature_inferno.succubus-mistress` | long swept-back horns ringed with gold, crimson wings, a whip of fire |
| I2 | `icon_creature_inferno.hell-charger` | a black demonic horse with burning hooves and a mane of fire, ember eyes | `icon_creature_inferno.nightmare` | bony black plates over its body, a mane of blue-white fire, smoke pouring from its nostrils |
| I3 | `icon_creature_inferno.pit-fiend` | a huge demon with a horned bull-like skull face, slag-black skin cracked with lava, a heavy blackened bronze glaive | `icon_creature_inferno.pit-lord` | two horned heads, folded bat wings, a whip of fire beside the glaive |
| I3 | `icon_creature_inferno.juggernaut` | a massive beast armoured in plates of black slag like a rhino, one great horn, lava glowing between the plates | `icon_creature_inferno.ravager` | spikes along the plates, the horn burning, fire roaring from its open maw |
| I3 | `icon_creature_inferno.devil` | a tall gaunt devil with ash-grey skin, long curved horns, tattered black leather wings, a cruel smile, a black sickle | `icon_creature_inferno.arch-devil` | a crown of black iron, wings edged in ember red, the sickle's blade made of flame |
