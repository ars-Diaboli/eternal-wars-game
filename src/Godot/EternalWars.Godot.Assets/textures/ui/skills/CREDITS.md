# Skill icons

Painted skill icons, 128 × 128 with transparency, generated for this project by the owner (2026-11-16, sets 3–6 replaced 2026-11-21; the magic schools 2026-11-21, sets 9–14) as icon sheets and cut into one
file per skill (prompts in `docs/content/prompts/icon-prompts.md`). Each file is named by its skill id (`offense.melee.png`). Loaded through
`EternalWars.Godot.UI.HeroIcons`; a skill without a file shows its text alone.

| Files | Used for |
|---|---|
| offense.\*, defense.\*, estates.\*, pathfinding.\*, tactics.\*, machines.\*, rage.\* | the hero screen's skill rows, the level-up and witch hut choices |
| magic.&lt;town&gt;.spells / .attunement / .mastery | the magic schools' skills, in the same places |
| magic.&lt;town&gt;.png | the school's emblem (the skill group's id), before the school's row in the spell book |
| estates.treasury.png | also the treasury: the Kingdom (K) button and the player's gold on hand |

