# Creature icon prompts: Sylvan

The Sylvan portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells by the shared **Cell rule** (top row the bases, bottom row their upgrades).

## Blocks

**`CREATURE-sylvan`** (look, second in every prompt of this file):

```text
Sylvan creatures: elves and fey of a dying forest, grey-brown bark, pale carved wood, green-gold leaves and moss, a faint pale-green
glow, grey rot at the edges; graceful and failing.
```

**`BACKDROP-sylvan`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_sylvan.png`):

```text
deep old forest of grey trunks, pale green light through thin leaves, falling leaves
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-sylvan` + `ICON-SHEET-6` + cells (Cell rule) · negative `ICON-NEG` + `CREATURE-NEG` · 1536 × 1024

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| S1 | `icon_creature_sylvan.pixie` | a tiny fey creature with dragonfly wings and a pale-green glow, a thorn held like a spear, the whole body shown | `icon_creature_sylvan.sprite` | moth wings patterned with eye spots, a crown of tiny white flowers, a silver glow |
| S1 | `icon_creature_sylvan.satyr` | a goat-legged satyr with curled horns and a shaggy beard, a wooden pipe in one hand and a short knotted club in the other | `icon_creature_sylvan.satyr-warrior` | horns capped in carved pale wood, a leather jerkin, a curved bone-handled sickle |
| S1 | `icon_creature_sylvan.blade-dancer` | a lithe elf warrior with braided silver hair in grey-green leather, two slim curved blades held crossed | `icon_creature_sylvan.war-dancer` | caught mid-spin, long green ribbons trailing, both blades glinting green-gold |
| S2 | `icon_creature_sylvan.hunter` | an elf hunter in a mossy green hooded cloak, a tall longbow drawn, the arrow at the cheek | `icon_creature_sylvan.arcane-archer` | a bow of pale living wood with leaves still growing on it, the arrowhead glowing pale green |
| S2 | `icon_creature_sylvan.druid` | an elf druid in grey-green robes, a headdress of dead antler-like branches, a gnarled staff with a pale-green crystal | `icon_creature_sylvan.druid-elder` | a long white braid and a mantle of moss, the staff crackling with silver-white lightning |
| S2 | `icon_creature_sylvan.pegasus` | a slender white winged horse with grey dapples, wings raised, a braided mane | `icon_creature_sylvan.silver-pegasus` | a silver-white coat with a faint sheen, feathers tipped in silver, a carved pale-wood brow plate |
| S3 | `icon_creature_sylvan.moon-doe` | a pale silver deer with large dark eyes and long slim legs, a faint crescent of moonlight on its brow | `icon_creature_sylvan.unicorn` | a white unicorn with one long spiral horn of pale gold and a mane like mist |
| S3 | `icon_creature_sylvan.treant` | a walking tree with a face of knotted bark, branch arms and a half-bare crown, grey rot creeping up from its roots | `icon_creature_sylvan.ancient-treant` | huge and older, its split bark braced with woven living branches, pale-green lanterns hanging in its crown |
| S3 | `icon_creature_sylvan.fire-bird` | a long-tailed bird of flame, red and orange feathers burning at the tips, the whole body shown | `icon_creature_sylvan.phoenix` | wings of white-gold fire spread wide, a crest of flame, embers falling from it |
