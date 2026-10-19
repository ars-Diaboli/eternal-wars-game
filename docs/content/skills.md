# Hero skills

Every hero skill there is today, what each level does and whether it works yet. Rules: ADR-048 (skills and levels),
ADR-047 (magic schools), ADR-055 (primary stats), ADR-056 (war machines), ADR-063 (Stewardship, mana, faction
groups). Numbers are a draft. The skills are code, not data: `BuiltInSkills` and `SkillEffects` in
`src/Core/EternalWars.Core.Domain/Heroes/Skills.cs`.

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
  | the hero's own town's school, or its faction group (Rage, Adrenaline) | 6 | 2 |
  | another school | 2 | 1 |
  | a group the hero has already started | ×3 | ×3 |

  A **rival town's school** (the sworn enemies of [lore.md](lore.md)) is never in the weighted pool; it takes one of
  the three offers in only **5 %** of level-ups. Skills at their maximum, and new groups once 6 are learned, are not
  offered. Faction groups are offered only to heroes of their own town.
- **Experience**: HoMM3's table (level 2 at 1000, 3 at 2000, 4 at 3200 … 12 at 20 600); after that each level's XP gap is
  20 % larger than the one before (`ExperienceTable`).

## Primary stats

| Stat | Effect | Status |
|---|---|---|
| Attack | added to every own stack's Attack in battle | ✅ |
| Defense | added to every own stack's Defense in battle | ✅ |
| Spell Power | spell strength | ⏳ magic (Phase 5) |
| Knowledge | mana pool (10 each) and daily mana (⌈K / 10⌉) | ✅ numbers (`ManaRules`) · ⏳ casting |

Each level-up raises one stat by 1 (`StatGrowth`, ADR-055):

| Class | Levels 2–9 (Attack / Defense / Spell Power / Knowledge) | Levels 10+ |
|---|---|---|
| Warrior | 35 / 45 / 10 / 10 % | 30 / 30 / 20 / 20 % |
| Mage | 10 / 10 / 40 / 40 % | 20 / 20 / 30 / 30 % |

Morale and luck (−5…+5) are hero values too (ADR-053); Leadership raises morale (ADR-067), no skill changes luck for now.

## General groups

### Offense (`offense`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Melee | `offense.melee` | Master | +10 % melee damage | +20 % | +30 % | +40 % | ✅ |
| Archery | `offense.archery` | Expert | +10 % ranged damage | +25 % | +50 % | – | ✅ |

### Defense (`defense`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Armor | `defense.armor` | Master | −5 % damage taken | −10 % | −15 % | −20 % | ✅ |
| Spell resistance | `defense.resistance` | Expert | resists hostile spells | | | – | ⏳ magic |
| Leadership | `offense.third` | Expert | +1 morale to all own stacks | +2 | +3 | – | ✅ (ADR-067; the id still reads "third") |

### Estates (`estates`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Treasury | `estates.treasury` | Master | +250 gold a day | +500 | +750, and 0.5 % of the gold on hand | +1000, and 1 % | ✅ |
| Prospecting | `estates.prospecting` | Expert | +1 lumber, +1 ore a day | +2 lumber, +2 ore | +2 lumber, +2 ore, +1 crystal, mercury and sulfur | – | ✅ |
| Stewardship | `estates.stewardship` | Expert | −5 % building, −10 % creature costs in the town the hero governs | −10 %, −15 % | −15 %, −20 % | – | ✅ (ADR-063) |

Prospecting also gives a **10 / 20 / 30 % chance** that a resource pile picked up gives **50 % more** (rounded up); the roll
uses the game RNG. Treasury interest is paid on the gold the player holds at the start of the day, before income is added
(ADR-062).

Stewardship works only in a town with a **governor**: a hero appointed with `AppointGovernorCommand` (one town per hero), or a
visiting hero with the skill in a town that has none. It also applies to recruiting at map dwellings (the hero's own level) and to caravans of a
governed town (ADR-054). A governed town counts 1.5 instead of 1 towards the marketplace rates (ADR-067; this
replaces the former 150 % value).

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
| Ballistics | `machines.ballistics` | Master | machine fires by itself, only at fortifications (sieges) | you control it | also targets troops, in regular battles, and fires twice after a kill | never misses a fortification, +25 % damage | 2-3 structure damage, +50 % | ✅ troops · ⏳ sieges (rules in `WarMachines`, ADR-063) |
| Healing | `machines.healing` | Master | tent heals 10 per stack a round | 20 | 30 | 40 | 50 | ✅ |
| Ammo supply | `machines.ammo` | Advanced | cart: +25 % shots | +50 % shots | unlimited shots while the cart stands | – | – | ✅ |

Against a fortification a shot hits 2 times in 3 (always from Expert) and takes 1-2 hit points off a defence structure
(2-3 at Master); a fully upgraded fortification gives its structures 3 hit points. Placeholders until sieges exist.

The tent heals the top creature of every own stack at the start of each round, never reviving. The machines
themselves (which town has which, stats, prices) are in [hero-roster.md](hero-roster.md#war-machines) and ADR-056.

## Magic schools

One group per town with a school (ADR-047, six of eight, ADR-063); any hero may learn any school. Working titles:

| Town | School | Group id |
|---|---|---|
| Haven | Light | `magic.haven` |
| Sylvan | Nature | `magic.sylvan` |
| Academy | Arcana | `magic.academy` |
| Necropolis | Death | `magic.necropolis` |
| Inferno | Chaos | `magic.inferno` |
| Dungeon | Shadow | `magic.dungeon` |

Stronghold and Swamp Bestiary have **no school** (ADR-063); see [Faction groups](#faction-groups).

Every school has the same three skills (shown for Light):

| Skill | Id | Max | Per level | Status |
|---|---|---|---|---|
| Light magic | `magic.haven.spells` | Master | learn spells of level 1 / 2 / 3 / 4 | ⏳ magic |
| Light attunement | `magic.haven.attunement` | Master | max mana, daily mana and spell cost, by school (below) | ✅ numbers (`ManaRules`) · ⏳ casting |
| Light mastery | `magic.haven.mastery` | Advanced | upgrade learned spells to their second / third level | ⏳ magic |

Attunement (ADR-063; several add up):

| Schools | Max mana | Mana a day | Spell cost |
|---|---|---|---|
| Arcana, Chaos | +12 / 25 / 37 / 50 | +1 / 2 / 3 / 4 | – |
| Light, Death | – | +3 / 6 / 9 / 12 | −1 / 2 / 3 / 4 |
| Nature, Shadow | +5 / 10 / 15 / 20 | +2 / 4 / 6 / 8 | – |

The mana pool is 10 × Knowledge plus attunement; a hero regains ⌈Knowledge / 10⌉ mana a day plus attunement; a spell costs
at least 25 % of its base, rounded up.

## Faction groups

Stronghold (**Primal Fury**, working title, id `rage`) and Swamp Bestiary (**Adrenaline**, `adrenaline`) have no magic
school. Only heroes of the town are offered their group. Skills are 4 + 3 + 3 points. Primal Fury is built (ADR-067): its ids stay
`rage.first`, `rage.second` and `rage.third`; the Adrenaline group still has three effect-less placeholder skills (`adrenaline.first` Master,
`adrenaline.second` and `adrenaline.third` Expert).

### Primal Fury (Stronghold, ADR-067, draft numbers)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Blood Rage (4 points) | `rage.first` | Master | an own stack that kills a creature in an attack gets +1 Attack, up to +2 until the battle ends | up to +4 | up to +6 | up to +8 | ✅ |
| Warcry (3 points) | `rage.second` | Expert | Rally: own stacks +1 morale, 3 rounds | + Intimidate: enemy stacks −1 morale, −20 % initiative, 2 rounds | + Terror: each enemy stack loses its next action with 30 % | – | ✅ |
| Spell resistance (3 points) | `rage.third` | Expert | the enemy hero's spells have −2 Spell Power | −4 | −6 | – | ⏳ data only, needs casting (Phase 5) |

Warcries cost no mana (a Stronghold mage has no school): one warcry a round, each once per battle. A warcry is a free action of the hero:
the player may use it whenever one of their units is up, and it does not use that unit's action (MAG-4 still decides spells). The faction's spell resistance is not the Defense skill of that name. Rally and Intimidate count from the round after the one they were used in; Terror takes effect at once. The Shamanism drafts in
[spells.md](spells.md) are the source of the warcries.

### Adrenaline (Swamp Bestiary)

Placeholder skills, no effect yet ❓: adrenaline stacks or other faction abilities are still to be designed (HERO-2).

## Specialties

Each roster hero has one HoMM3-style specialty ([hero-roster.md](hero-roster.md)):

| Kind | Example | Status |
|---|---|---|
| Resource | `resource gold 350`: +350 gold a day | ✅ (`HeroIncome`) |
| Skill | `skill offense.melee`: a stronger version of the skill | ⏳ effect not designed ❓ |
| Spell | `spell light.bless`: a stronger version of the spell | ⏳ magic |

## Open

- What skill specialties do.
- A luck skill (none for now).
- The Adrenaline mechanics; the final name Primal Fury; Leadership and Alertness numbers are placeholders.
- Sieges themselves (Ballistics numbers are placeholders).
