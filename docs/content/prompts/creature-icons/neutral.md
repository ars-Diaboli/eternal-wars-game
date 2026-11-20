# Creature icon prompts: Neutral

The Neutral portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Sheets X1–X2 by the shared **Cell rule** (top row the bases, bottom row their upgrades); sheet X3 as listed.

## Blocks

**`CREATURE-neutral`** (look, second in every prompt of this file):

```text
Neutral creatures of the wilds and the ruins: no faction colours, natural and weathered materials, each creature in its own colours.
```

**`BACKDROP-neutral`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_neutral.png`):

```text
a plain grey-green wilderness of rolling hills and distant mountains under an overcast sky
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-neutral` + `ICON-SHEET-6` + cells (Cell rule, X1–X2) or `ICON-SHEET-4` + cells (X3) · negative `ICON-NEG` + `ICON-NEG-TOON` + `CREATURE-NEG` · 1536 × 1024 (X1–X2), 1024 × 1024 (X3)

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| X1 | `icon_creature_neutral.wolf` | a grey timber wolf with raised hackles, snarling, the whole body shown | `icon_creature_neutral.dire-wolf` | huge and scarred, a ridge of bristling black fur |
| X1 | `icon_creature_neutral.specter` | a drifting pale-grey ghost in a torn hooded shroud, an empty dark face | `icon_creature_neutral.banshee` | long white hair streaming upward, the mouth wide open in a wail |
| X1 | `icon_creature_neutral.thane` | the head and shoulders of a mountain giant with frost-grey skin and a braided beard, a huge stone axe | `icon_creature_neutral.thunder-thane` | lightning crackling in the beard, an iron storm crown, a hammer wrapped in lightning |
| X2 | `icon_creature_neutral.mammoth` | a woolly mammoth with long curved tusks and shaggy dark fur | `icon_creature_neutral.war-mammoth` | a wooden fighting tower on its back, iron caps on the tusks |
| X2 | `icon_creature_neutral.gog` | a small red-skinned demon with crude horns, a ball of fire held in one hand | `icon_creature_neutral.magog` | larger, black horns, a fireball blazing in both hands |
| X2 | `icon_creature_neutral.breeder` | a bloated demonic brood-mother with leathery grey-red skin, small imps clinging to its back | `icon_creature_neutral.fel-breeder` | sickly green-black skin, a sac of glowing eggs, veins of fire |

| Sheet | # | File | Subject |
|---|---|---|---|
| X3 | 1 | `icon_creature_neutral.bandit` | a hooded human bandit with a scarf over the lower face, a short sword and a dagger |
| X3 | 2 | `icon_creature_neutral.troll` | a tall green-grey troll with long arms and mossy hide, a branch club, wounds closing on its skin |
| X3 | 3 | `icon_creature_neutral.efreet` | a fire spirit with a muscular upper body of red flame, black horns, a trail of smoke instead of legs |
| X3 | 4 | `icon_creature_neutral.golden-golem` | a golem cast of gold, a smooth featureless face, finely engraved gold plates |
