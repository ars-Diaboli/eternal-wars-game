# Hero skills

Every hero skill there is today, what each level does and whether it works yet. Rules: ADR-048 (skills and levels),
ADR-047 (magic schools), ADR-055 (primary stats), ADR-056 (war machines), ADR-063 (Stewardship, mana, faction
groups). Numbers are a draft. The skills are code, not data: `BuiltInSkills` and `SkillEffects` in
`src/Core/EternalWars.Core.Domain/Heroes/Skills.cs`.

Status: ✅ works in play · ⏳ defined, waits for a feature (named) · 🔜 decided in ADR-069 (2026-10-20) or later (ADR-070, ADR-071), not built yet; the
code still has the earlier rule, shown as "today" · ❓ not decided yet.

**ADR-069 rework** (2026-10-20): the tables below show the new design. It moves Leadership to Defense and Alertness to Tactics,
adds Marksmanship and Scouting, drops Troop management and merges Deployment into Tactics. It also changes the attunement
table, adds Necromancy and gives Swamp Bestiary the Primal Fury group (Adrenaline is dropped). Open: HERO-6 (old ids),
HERO-7 (faction group), HERO-4 (deployment), CMB-6 (ranged penalties), MAG-7 (Necromancy).

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
  | the hero's own town's school, or its faction group (Primal Fury) | 6 | 2 |
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
| Spell Power | multiplies damage spells' damage and buffs' and debuffs' duration (ADR-071; numbers with the spell list, MAG-1) | ⏳ magic (Phase 5) |
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
| Melee | `offense.melee` | Master | +9 % melee damage | +16 % | +23 % | +30 % | 🔜 (today +10 / 20 / 30 / 40 %) |
| Archery | `offense.archery` | Expert | +15 % ranged damage | +25 % | +35 % | – | 🔜 (today +10 / 25 / 50 %) |
| Marksmanship | `offense.marksmanship` | Expert | ranged penalty −2/8 | −4/8 | −6/8 | – | 🔜 new (ADR-070) |

Ranged attacks get a **distance penalty** (ADR-069, ADR-070), replacing the half damage beyond 15 cells: 0–9 cells between the
footprints none, 10–19 1/8, 20–29 2/8, and so on; it adds to the cover penalty, and above 7/8 the target cannot be shot. War
machines ignore it ([combat.md](../design/combat.md#engagement-and-blocking)). Marksmanship subtracts a fixed amount from the total
ranged penalty; "almost no penalty" at Expert is intended. Whether the 7/8 check comes before the reduction is CMB-6.

### Defense (`defense`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Armor | `defense.armor` | Master | −5 % damage taken | −10 % | −15 % | −20 % | ✅ |
| Spell resistance | `defense.resistance` | Expert | hostile spells −8 % damage and duration | −14 % | −20 % | – | ⏳ magic; 🔜 numbers (ADR-071, was −10 / 15 / 20 %) |

Spell resistance cuts both a hostile spell's damage and its duration; durations round away from zero, at least 1 round (ADR-071, MAG-6).
| Leadership | `defense.leadership` | Expert | +1 morale to all own stacks | +2 | +3 | – | ✅ effect (ADR-067); 🔜 moves from Offense (today `offense.third`) |

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
| Pathfinding | `pathfinding.pathfinding` | Master | −25 % of the non-native terrain penalty | −50 % | −75 % | −100 % | 🔜 (today Expert, up to −75 %) |
| Logistics | `pathfinding.logistics` | Expert | +10 % movement points | +20 % | +30 % | – | 🔜 (today Master, +40 %) |
| Scouting | `pathfinding.scouting` | Expert | +2 sight radius on the map | +4 | +6 | – | 🔜 new, replaces Troop management (`pathfinding.troops`, +1 / 2 / 3 army map speed) |

There is no navigation skill: there is no sea travel (ADR-051). A hero's sight radius is 5 ([adventure-map.md](../design/adventure-map.md#fog-of-war-implemented-adr-017-adr-031)).

### Tactics (`tactics`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| War Tempo | `tactics.wartempo` | Master | +1 battlefield speed for every stack | +2 | +3 | +4 | ✅ effect; 🔜 renamed from Drill (`tactics.drill`) |
| Tactics | `tactics.tactics` | Expert | rearrange troops before the battle (zone 4 cells deep) | also choose width 30 or 32 (zone 5 cells) | width 28–34 (zone 6 cells) | – | ✅ width (today Basic 30 / 32, Advanced 28–32); 🔜 deployment step (HERO-4), merges the Deployment skill |
| Alertness (working title) | `tactics.alert` | Expert | +1 initiative to all own stacks in battle | +2 | +3 | – | 🔜 moves from Defense (today `defense.third`, +5 / 10 / 15 %) |

Tactics counts as the hero's Tactics level minus the enemy hero's; only the side ahead chooses, otherwise the width is
30 (36 is kept for sieges). Whether the same rule decides who deploys is HERO-4.

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
| Light magic | `magic.haven.spells` | Master | learn spells of level 1 / 2 / 3 / 4 (a spell is learned when this skill is at least its level, ADR-071) | ⏳ magic |
| Light attunement | `magic.haven.attunement` | Master | max mana, daily mana and spell cost, by school (below) | ✅ numbers (`ManaRules`) · ⏳ casting |
| Light mastery | `magic.haven.mastery` | Advanced | upgrade learned spells to their second / third level | ⏳ magic |

Attunement (ADR-069, replacing ADR-063's table; several add up), 🔜:

| Schools | Max mana | Mana a day | Spell cost |
|---|---|---|---|
| Arcana, Chaos | +10 / 20 / 30 / 40 | +1 / 2 / 3 / 4 | – |
| Light | – | +2 / 4 / 6 / 8 | −1 / 2 / 3 / 4 |
| Death | – | +2 / 4 / 6 / 8 | – (Necromancy 5 / 10 / 15 / 20 % instead, below) |
| Nature, Shadow | +5 / 10 / 15 / 20 | +2 / 4 / 6 / 8 | – |

Today (ADR-063): Arcana and Chaos +12 / 25 / 37 / 50 max mana; Light and Death +3 / 6 / 9 / 12 a day and −1 / 2 / 3 / 4 cost.

The mana pool is 10 × Knowledge plus attunement, plus **10 × ⌈level / 10⌉** (ADR-069, 🔜: +10 up to level 10, +20 up to 20, …); a hero regains
⌈Knowledge / 10⌉ mana a day plus attunement; a spell costs at least 25 % of its base, rounded up. Attunement does not raise
spell "potency" (ADR-071); it changes only the mana numbers in the table above (and gives Necromancy).

### Necromancy (Death attunement, ADR-069, not built)

After a victory, a hero with Death attunement raises **5 / 10 / 15 / 20 %** (Basic … Master) of the value of the enemy creatures slain as
Necropolis undead. The level also caps the tier raised:

| Death attunement | Undead raised | Enemy tier → undead tier |
|---|---|---|
| Basic | tier 1 | every tier → 1 (the tier-1 lines in random proportion) |
| Advanced | tiers 1–2 | 1–3 → 1, 4–5 → 2 |
| Expert | tiers 1–3 | 1–2 → 1, 3–4 → 2, 5 → 3 |
| Master | tiers 1–4 | 1–2 → 1, 3 → 2, 4 → 3, 5 → 4 |

A player who owns a Necropolis with the **Necromancy Amplifier** gives every one of their heroes with the skill +5 % (10 / 15 / 20 / 25 %);
more amplifiers add nothing. It costs 1500 gold + 5 mercury, needs mage guild 1, and a captured one helps its new owner (ADR-070,
[town-buildings.md](town-buildings.md#open)). Open (MAG-7): which value, which creatures (base or upgraded, which line), what cannot be raised, army room, rounding.

## Faction groups

Stronghold and Swamp Bestiary have no magic school. Since ADR-069 both have **Primal Fury** (working title, id `rage`); the **Adrenaline**
group is dropped (🔜: the code still has its three effect-less placeholders `adrenaline.first`, `.second`, `.third`). Only heroes of the two
towns are offered the group, meant for their **mage** heroes; their **warrior** heroes start with the Offense group (HERO-7). Skills are
4 + 3 + 3 points; the ids stay `rage.first`, `rage.second` and `rage.third`.

### Primal Fury (Stronghold and Swamp Bestiary, ADR-067, ADR-069, draft numbers)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Blood Rage (4 points) | `rage.first` | Master | an own stack that kills a creature in an attack gets +1 Attack, up to +2 until the battle ends | up to +4 | up to +6 | up to +8 | ✅ (Stronghold; 🔜 Swamp Bestiary) |
| Warcry / Spiritual Totems (working names, 3 points) | `rage.second` | Expert | Rally: own stacks +1 morale, 3 rounds | + Intimidate: enemy stacks −1 morale, −20 % initiative, 2 rounds | + Terror: each enemy stack loses its next action with 30 % | – | ✅ (Stronghold; 🔜 Swamp Bestiary) |
| Interference (3 points) | `rage.third` | Expert | the enemy hero's spells have −2 Spell Power and cost +1 mana | −4, +2 | −6, +2 | – | ⏳ data only, needs casting (Phase 5); 🔜 the cost part (today "Spell resistance (faction)", Spell Power only) |

Warcries cost no mana (the group's mages have no school): one warcry a round, each once per battle. A warcry is a free action of the hero:
the player may use it whenever one of their units is up, and it does not use that unit's action (the same holds for spells, ADR-071, MAG-4). Rally and
Intimidate count from the round after the one they were used in; Terror takes effect at once. The Shamanism drafts in
[spells.md](spells.md) are the source of the warcries.

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
- The final names Primal Fury, Warcry / Spiritual Totems and Alertness; Leadership and Alertness numbers are placeholders.
- ADR-069 details: old skill ids (HERO-6), the faction group's offers and starting skills (HERO-7), the deployment step (HERO-4),
  ranged penalties (CMB-6), Necromancy (MAG-7).
- Sieges themselves (Ballistics numbers are placeholders).
