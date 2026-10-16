# Hero skills

Every hero skill there is today, what each level does and whether it works yet. Rules: ADR-048 (skills and levels),
ADR-047 (magic schools), ADR-055 (primary stats), ADR-056 (war machines). Numbers are a draft. The skills are code,
not data: `BuiltInSkills` and `SkillEffects` in `src/Core/EternalWars.Core.Domain/Heroes/Skills.cs`.

Status: ✅ works in play · ⏳ defined, waits for a feature (named) · ❓ not decided yet.

## How skills work

- **Skill groups** (HoMM4-like): every group has **three skills**, and **10 points** learn a whole group (4 + 3 + 3, or
  4 + 4 + 2 for magic schools and war machines). A hero learns at most **6 groups** (`HeroSkills.MaxGroups`).
- **Levels**: Basic, Advanced, Expert, Master. A 3-level skill stops at Expert; a 2-level skill at Advanced.
- **Level-up**: each level gives **one skill point** and **+1 to one primary stat**. The player picks one of **three
  offered skills** (`LevelUpDecision`, `ChooseSkillCommand`); computer heroes take the first offer. A hero that levels
  up in another player's turn chooses when its owner's turn starts.
- **Offer weights** (`SkillOffers`), drawn from the game RNG:

  | Skill is in… | Mage | Warrior |
  |---|---|---|
  | a general group (offense … war machines) | 2 | 4 |
  | the hero's own town's school | 6 | 2 |
  | another school | 2 | 1 |
  | a group the hero has already started | ×3 | ×3 |

  A **rival town's school** (the lore ring, see [heroes.md](heroes.md)) is never in the weighted pool; it takes one of
  the three offers in only **5 %** of level-ups. Skills at their maximum, and new groups once 6 are learned, are not
  offered.
- **Experience**: HoMM3's table (level 2 at 1000, 3 at 2000, 4 at 3200 … 12 at 20 600), then +20 % per level.

## Primary stats

| Stat | Effect | Status |
|---|---|---|
| Attack | added to every own stack's Attack in battle | ✅ |
| Defense | added to every own stack's Defense in battle | ✅ |
| Spell Power | spell strength | ⏳ magic (Phase 5) |
| Knowledge | mana | ⏳ magic (Phase 5) |

Each level-up raises one stat by 1 (`StatGrowth`, ADR-055):

| Class | Levels 2–9 (Attack / Defense / Spell Power / Knowledge) | Levels 10+ |
|---|---|---|
| Warrior | 35 / 45 / 10 / 10 % | 30 / 30 / 20 / 20 % |
| Mage | 10 / 10 / 40 / 40 % | 20 / 20 / 30 / 30 % |

Morale and luck (−5…+5) are hero values too (ADR-053); no skill changes them yet ❓.

## General groups

### Offense (`offense`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Melee | `offense.melee` | Master | +10 % melee damage | +20 % | +30 % | +40 % | ✅ |
| Archery | `offense.archery` | Expert | +10 % ranged damage | +25 % | +50 % | – | ✅ |
| *(third skill)* | `offense.third` | Expert | no effect | | | – | ❓ placeholder |

### Defense (`defense`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Armor | `defense.armor` | Master | −5 % damage taken | −10 % | −15 % | −20 % | ✅ |
| Spell resistance | `defense.resistance` | Expert | resists hostile spells | | | – | ⏳ magic |
| *(third skill)* | `defense.third` | Expert | no effect | | | – | ❓ placeholder |

### Estates (`estates`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Treasury | `estates.treasury` | Master | +100 gold a day | +200 | +350 | +500 | ✅ |
| Prospecting | `estates.prospecting` | Expert | +1 lumber, +1 ore a day | +2 lumber, +2 ore | +2 lumber, +2 ore, +1 crystal, mercury and sulfur | – | ✅ |
| Stewardship | `estates.stewardship` | Expert | −5 % costs in the town the hero is in | −10 % | −15 % | – | ⏳ (towns exist; roadmap Phase 3) |

### Pathfinding (`pathfinding`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Logistics | `pathfinding.logistics` | Master | +10 % movement points | +20 % | +30 % | +40 % | ✅ |
| Pathfinding | `pathfinding.pathfinding` | Expert | −25 % of the non-native terrain penalty | −50 % | −75 % | – | ✅ |
| Troop management | `pathfinding.troops` | Expert | +1 army map speed | +2 | +3 | – | ✅ |

There is no navigation skill: there is no sea travel (ADR-051).

### Tactics (`tactics`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Tactics | `tactics.tactics` | Expert | choose battlefield width 30 or 32 | 28–32 | 28–34 | – | ✅ (ADR-033) |
| Deployment | `tactics.deployment` | Expert | rearrange troops before the battle | | | – | ⏳ deployment step |
| Drill | `tactics.drill` | Master | +1 battlefield speed for every stack | +2 | +3 | +4 | ✅ |

Tactics counts as the hero's Tactics level minus the enemy hero's; only the side ahead chooses, otherwise the width is
30 (36 is kept for sieges).

### War machines (`machines`)

| Skill | Id | Max | No skill | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|---|
| Ballistics | `machines.ballistics` | Master | offensive machine in sieges only | same | also in regular battles | +25 % its damage | +50 % | ✅ (Basic waits for sieges) |
| Healing | `machines.healing` | Master | tent heals 10 per stack a round | 20 | 30 | 40 | 50 | ✅ |
| Ammo supply | `machines.ammo` | Advanced | cart: +25 % shots | +50 % shots | unlimited shots while the cart stands | – | – | ✅ |

The tent heals the top creature of every own stack at the start of each round, never reviving. The machines
themselves (which town has which, stats, prices) are in [heroes.md](heroes.md#war-machines) and ADR-056.

## Magic schools

One group per playable town (ADR-047); any hero may learn any school. Working titles:

| Town | School | Group id |
|---|---|---|
| Haven | Light | `magic.haven` |
| Sylvan | Nature | `magic.sylvan` |
| Academy | Arcana | `magic.academy` |
| Necropolis | Death | `magic.necropolis` |
| Inferno | Chaos | `magic.inferno` |
| Dungeon | Shadow | `magic.dungeon` |
| Stronghold | Shamanism | `magic.stronghold` |
| Swamp Bestiary | Primal | `magic.swamp-bestiary` |

Every school has the same three skills (shown for Light):

| Skill | Id | Max | Per level | Status |
|---|---|---|---|---|
| Light magic | `magic.haven.spells` | Master | learn spells of level 1 / 2 / 3 / 4 | ⏳ magic |
| Light attunement | `magic.haven.attunement` | Master | mana pool and regeneration; per school spell cost or potency | ⏳ magic |
| Light mastery | `magic.haven.mastery` | Advanced | upgrade learned spells to their second / third level | ⏳ magic |

## Specialties

Each roster hero has one HoMM3-style specialty ([heroes.md](heroes.md)):

| Kind | Example | Status |
|---|---|---|
| Resource | `resource gold 350`: +350 gold a day | ✅ (`HeroIncome`) |
| Skill | `skill offense.melee`: a stronger version of the skill | ⏳ effect not designed ❓ |
| Spell | `spell light.bless`: a stronger version of the spell | ⏳ magic |

## Open

- The third offense and defense skills (open questions, `ideas.md`).
- What skill specialties do.
- Which skills raise morale and luck.
- What Basic Ballistics does in sieges (open question 12).
