# Creature icon prompts: Haven

The Haven portraits. The shared blocks (`CREATURE-STYLE`, `CREATURE-NEG`, `BACKDROP-STYLE`), the output rules and the checks are in
[creature-icon-prompts.md](../creature-icon-prompts.md); `ICON-SHEET-4`, `ICON-SHEET-6` and `ICON-NEG` are in
[icon-prompts.md](../icon-prompts.md#blocks). Cells by the shared **Cell rule** (top row the bases, bottom row their upgrades).

## Blocks

**`CREATURE-haven`** (look, second in every prompt of this file):

```text
Haven creatures: grim human zealots and their beasts, soot-stained steel and black iron, worn brass trim, off-white and faded gold
cloth, a small sun motif; disciplined, desperate.
```

**`BACKDROP-haven`** (the backdrop, after `BACKDROP-STYLE`; generated alone at 1024 × 1024 as `creature_backdrop_haven.png`):

```text
misty green highland hills under a pale dawn sky, a distant ruined stone tower
```

## Portraits

**Compose**: `CREATURE-STYLE` + `CREATURE-haven` + `ICON-SHEET-6` + cells (Cell rule) · negative `ICON-NEG` + `CREATURE-NEG` · 1536 × 1024

| Sheet | Base | Base subject | Upgrade | Upgrade adds |
|---|---|---|---|---|
| H1 | `icon_creature_haven.footman` | a human foot soldier in a dented kettle helmet and a grey quilted coat, a short sword raised behind a round iron-rimmed shield | `icon_creature_haven.crusader` | a steel helm with a small brass sun on the brow, a faded off-white tabard with a gold sun, a second sword |
| H1 | `icon_creature_haven.pikeman` | a gaunt human pikeman in an open steel sallet and padded coat, a long pike held upright, its head rising above the shoulder | `icon_creature_haven.guardian` | full steel plate with brass edges, a tall tower shield bearing an iron sun-cage, the pike lowered to strike |
| H1 | `icon_creature_haven.zealot` | a flagellant zealot with a shaved, scarred head and bare welted shoulders, a spiked flail in one hand and a burning torch in the other | `icon_creature_haven.inquisitor` | a hood and a black iron mask, long grey robes, a red-hot branding iron glowing in one hand, a chain of iron sun medallions |
| H2 | `icon_creature_haven.archer` | a human crossbowman in a leather cap and quilted jacket, a heavy crossbow resting on the shoulder | `icon_creature_haven.marksman` | a steel helmet with a lifted visor, a crossbow with a brass winch aimed forward, a bandolier of bolts across the chest |
| H2 | `icon_creature_haven.monk` | a monk in a coarse grey habit, hood down, a sun mark of ash on the forehead, swinging a smoking iron censer | `icon_creature_haven.priest` | soot-stained off-white and gold vestments, a tall staff topped with a small iron cage holding a flame |
| H2 | `icon_creature_haven.griffin` | a griffin with an eagle's head and a tawny lion's body, grey and tawny feathers, wings half open, a fierce yellow eye | `icon_creature_haven.royal-griffin` | white and pale-gold plumage, a brass chain collar with a sun medallion, wings spread wide |
| H3 | `icon_creature_haven.lancer` | a knight in plain steel plate on a grey horse barded in mail, the lance held upright | `icon_creature_haven.champion` | a white horse in an off-white caparison with a gold sun, a brass sun crest on the helm, the lance lowered for the charge |
| H3 | `icon_creature_haven.paladin` | a grim knight on foot in soot-darkened plate, a greatsword held point-down in front, a sun etched on the breastplate | `icon_creature_haven.sacred-warrior` | gilded edges on the plate, a small burning brazier as the helm's crest, the greatsword's blade glowing faint white |
| H3 | `icon_creature_haven.radiant-spirit` | a spirit of pale gold light shaped like a robed figure with a blank glowing face and two wings of light | `icon_creature_haven.seraph` | six wings of white-gold light, a ring of small flames above the head, a long spear of light |
