# Hero skills

Every hero skill there is today, what each level does and whether it works yet. Rules: ADR-048 (skills and levels),
ADR-047 (magic schools), ADR-055 (primary stats), ADR-056 (war machines), ADR-063 (Stewardship, mana, faction
groups). Numbers are a draft. The skills are code, not data: `BuiltInSkills` and `SkillEffects` in
`src/Core/EternalWars.Core.Domain/Heroes/Skills.cs`.

Status: ✅ works in play · ⏳ defined, waits for a feature (named) · 🔜 decided, not built yet · ❓ not decided yet.

**ADR-069 rework** (2026-10-20, built: ADR-078): it moved Leadership to Defense and Alertness to Tactics, added Marksmanship and
Scouting, dropped Troop management and merged Deployment into Tactics. It also changed the attunement table, added Necromancy and
gave Swamp Bestiary the Primal Fury group (Adrenaline is dropped). ADR-072 settles its details (old ids, the faction group,
deployment, ranged penalties, Necromancy); ADR-073 the war machines and the Primal Fury mages' odds.

**Old ids** (ADR-072, HERO-6): maps, saves, the hero table and replays may still name the ids from before the rework; they read as
the new skill (`BuiltInSkills.Renamed`), and a level above the new skill's maximum drops to it:

| Old id | Now |
|---|---|
| `offense.third` | Leadership, `defense.leadership` |
| `defense.third` | Alertness, `tactics.alert` |
| `tactics.drill` | War Tempo, `tactics.wartempo` |
| `tactics.deployment` | Tactics, `tactics.tactics` |
| `pathfinding.troops` | Scouting, `pathfinding.scouting` |
| `adrenaline.first` / `.second` / `.third` | Blood Rage / Warcry / Interference, `rage.first` / `.second` / `.third` |

## How skills work

- **Skill groups** (HoMM4-like): every group has **three skills**, and **10 points** learn a whole group (4 + 3 + 3, or
  4 + 4 + 2 for magic schools and war machines). A hero learns at most **6 groups** (`HeroSkills.MaxGroups`).
- **Levels**: Basic, Advanced, Expert, Master. A 3-level skill stops at Expert; a 2-level skill at Advanced.
- **Level-up**: each level gives **one skill point** and **+1 to one primary stat**. The player picks one of **three
  offered skills** (`LevelUpDecision`, `ChooseSkillCommand`); computer heroes take Necromancy whenever it is offered, else the
  first offer (`SkillOffers.ComputerChoice`). A hero that levels
  up in another player's turn chooses when its owner's turn starts.
- **Offer weights** (`SkillOffers`), drawn from the game RNG:

  | Skill is in… | Mage | Warrior |
  |---|---|---|
  | a general group (offense … war machines) | 2 | 4 |
  | the hero's own town's school | 6 | 2 |
  | the hero's faction group (Primal Fury) | 6 | 6 (ADR-072, ADR-073) |
  | another school | 2 | 1 |
  | a group the hero has already started | ×3 | ×3 |

  A **rival town's school** is never in the weighted pool; it takes one of the three offers in only **5 %** of level-ups.
  The rival is the town **across the ring** of [lore.md](lore.md), one per town (ADR-072, HERO-3):
  Sylvan–Dungeon, Haven–Necropolis, Academy–Inferno. The tavern uses the same ring. Skills at their maximum, and new groups once 6 are learned, are not
  offered. Faction groups are offered only to heroes of their own towns.
- **Experience**: HoMM3's table (level 2 at 1000, 3 at 2000, 4 at 3200 … 12 at 20 600); after that each level's XP gap is
  20 % larger than the one before (`ExperienceTable`).

## Primary stats

| Stat | Effect | Status |
|---|---|---|
| Attack | added to every own stack's Attack in battle | ✅ |
| Defense | added to every own stack's Defense in battle | ✅ |
| Spell Power | multiplies damage spells' damage and buffs' and debuffs' duration (ADR-071; numbers with the spell list, MAG-1) | ⏳ magic (Phase 5) |
| Knowledge | mana pool (10 each) and daily mana (⌈K / 10⌉); nothing for Primal Fury mages | ✅ numbers (`ManaRules`) · ⏳ casting |

Each level-up raises one stat by 1 (`StatGrowth`, ADR-055):

| Class | Levels 2–9 (Attack / Defense / Spell Power / Knowledge) | Levels 10+ |
|---|---|---|
| Warrior | 35 / 45 / 10 / 10 % | 30 / 30 / 20 / 20 % |
| Mage | 10 / 10 / 40 / 40 % | 20 / 20 / 30 / 30 % |
| Mage of Stronghold or Swamp Bestiary (ADR-073) | 25 / 25 / 25 / 25 % | 25 / 25 / 25 / 25 % |

Morale and luck (−5…+5) are hero values too (ADR-053); Leadership raises morale (ADR-067), no skill changes luck for now.

## General groups

### Offense (`offense`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Melee | `offense.melee` | Master | +9 % melee damage | +16 % | +23 % | +30 % | ✅ |
| Archery | `offense.archery` | Expert | +15 % ranged damage | +25 % | +35 % | – | ✅ |
| Marksmanship | `offense.marksmanship` | Expert | ranged penalty −2/8 | −4/8 | −6/8 | – | ✅ (ADR-070) |

Ranged attacks get a **distance penalty** (ADR-069, ADR-070; built), replacing the half damage beyond 15 cells: 0–9 cells between the
footprints none, 10–19 1/8, 20–29 2/8, and so on; it adds to the cover penalty, and above 7/8 the target cannot be shot. War
machines ignore it ([combat.md](../design/combat.md#engagement-and-blocking)). Marksmanship subtracts a fixed amount from the total
ranged penalty; "almost no penalty" at Expert is intended. The order (ADR-072, CMB-6): add distance and cover, subtract Marksmanship, then
check the 7/8 limit, so a marksman can shoot past cover that would stop others.

### Defense (`defense`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Armor | `defense.armor` | Master | −5 % damage taken | −10 % | −15 % | −20 % | ✅ |
| Spell resistance | `defense.resistance` | Expert | hostile spells −8 % damage and duration | −14 % | −20 % | – | ✅ (ADR-110) |
| Leadership | `defense.leadership` | Expert | +1 morale to all own stacks | +2 | +3 | – | ✅ (ADR-067; was `offense.third`) |

Spell resistance cuts both a hostile spell's damage and its duration; durations round away from zero, at least 1 round (ADR-071, MAG-6).

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
| Pathfinding | `pathfinding.pathfinding` | Master | −25 % of the non-native terrain penalty | −50 % | −75 % | −100 % | ✅ |
| Logistics | `pathfinding.logistics` | Expert | +10 % movement points | +20 % | +30 % | – | ✅ |
| Scouting | `pathfinding.scouting` | Expert | +2 sight radius on the map | +4 | +6 | – | ✅ at once when learned (replaces Troop management, `pathfinding.troops`) |

There is no navigation skill: there is no sea travel (ADR-051). A hero's sight radius is 5 plus Scouting ([adventure-map.md](../design/adventure-map.md#fog-of-war-implemented-adr-017-adr-031)).

### Tactics (`tactics`)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| War Tempo | `tactics.wartempo` | Master | +1 battlefield speed for every stack | +2 | +3 | +4 | ✅ (was Drill, `tactics.drill`) |
| Tactics | `tactics.tactics` | Expert | rearrange troops before the battle (4 columns) | also choose width 30 or 32 (5 columns) | width 28–34 (6 columns) | – | ✅ width and deployment step (ADR-072, ADR-076; Deployment, `tactics.deployment`, merged in) |
| Alertness (working title) | `tactics.alert` | Expert | +1 initiative to all own stacks in battle | +2 | +3 | – | ✅ (was `defense.third`, +5 / 10 / 15 %) |

Tactics counts as the hero's Tactics level minus the enemy hero's; only the side ahead chooses, otherwise the width is
30 (36 is kept for sieges).

**Deployment** (ADR-072, HERO-4; built, ADR-076): the zone is the number of columns, counted from the hero's own edge of the battlefield, in
which the player may place the troops. Only the side with the higher Tactics deploys; on a tie nobody does. The player drags stacks in the
battle scene (click a stack, then where it should stand) and presses "Start battle"; troops that are not deployed, and every computer player's, are placed automatically at the very edge.
It works against heroes and neutral monsters, not in creature banks. The war machines keep the outermost column, one cell wide (ADR-073);
the zone includes that column and stacks may use its free cells (confirmed, ADR-075).

### War machines (`machines`)

| Skill | Id | Max | No skill | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|---|
| Ballistics | `machines.ballistics` | Master | machine fires by itself, only at fortifications (sieges) | you control it | also targets troops, in regular battles, and fires twice after a kill | never misses a fortification, +25 % damage | 2-3 structure damage, +50 % | ✅ (sieges ADR-109) |
| Healing | `machines.healing` | Master | tent heals 10 per stack a round | 20 | 30 | 40 | 50 | ✅ |
| Ammo supply | `machines.ammo` | Advanced | cart: +25 % shots | +50 % shots | unlimited shots while the cart stands | – | – | ✅ |

Against a fortification a shot hits 2 times in 3 (always from Expert) and takes 1-2 hit points off a defence structure
(2-3 at Master); a Castle's wall sections, the keep and the towers have 3 hit points. Placeholders (sieges built in ADR-109; siege rules
ADR-106). There is no free siege catapult: the attacking hero's own offensive machine, whatever its kind, is what fires at the
walls, and it joins every siege whatever the hero's Ballistics; firing by itself it aims at the gate only once all four wall sections are
down ([combat.md](../design/combat.md#sieges-adr-099-adr-106-built-adr-109)).

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

Attunement (ADR-069, replacing ADR-063's table; several add up), ✅ numbers:

| Schools | Max mana | Mana a day | Spell cost |
|---|---|---|---|
| Arcana, Chaos | +10 / 20 / 30 / 40 | +1 / 2 / 3 / 4 | – |
| Light | – | +2 / 4 / 6 / 8 | −1 / 2 / 3 / 4 |
| Death | – | +2 / 4 / 6 / 8 | – (Necromancy 5 / 10 / 15 / 20 % instead, below) |
| Nature, Shadow | +5 / 10 / 15 / 20 | +2 / 4 / 6 / 8 | – |

The mana pool is 10 × Knowledge plus attunement, plus **10 × ⌈level / 10⌉** (ADR-069: +10 up to level 10, +20 up to 20, …; the maximum only, MAG-8); a hero regains
⌈Knowledge / 10⌉ mana a day plus attunement; a spell costs at least 25 % of its base, rounded up. Attunement does not raise
spell "potency" (ADR-071); it changes only the mana numbers in the table above (and gives Necromancy).

### Necromancy (Death attunement, ADR-069; built, ADR-078)

After a victory, any hero with Death attunement raises **5 / 10 / 15 / 20 %** (Basic … Master) of the **hit points** of the enemy
creatures slain as Necropolis undead. The level also caps the tier raised:

| Death attunement | Undead raised | Enemy tier → undead tier |
|---|---|---|
| Basic | tier 1 | every tier → 1 (the tier-1 lines in random proportion) |
| Advanced | tiers 1–2 | 1–3 → 1, 4–5 → 2 |
| Expert | tiers 1–3 | 1–2 → 1, 3–4 → 2, 5 → 3 |
| Master | tiers 1–4 | 1–2 → 1, 3 → 2, 4 → 3, 5 → 4 |

A player who owns a Necropolis with the **Necromancy Amplifier** gives every one of their heroes with the skill +5 % (10 / 15 / 20 / 25 %);
more amplifiers add nothing. It costs 1500 gold + 5 mercury, needs mage guild 1, and a captured one helps its new owner (ADR-070;
built, `necromancy-amplifier` in `buildings.csv`, [town-buildings.md](town-buildings.md)).

Rules (ADR-072, MAG-7):
- **Counted**: only the enemy's losses, enemy undead included; constructs and elementals (by the creature tags) and war machines are not. The
  hero's own losses never count.
- **Count**: the slain hit points are summed per undead tier (by the table), times the share, divided by the undead creature's hit points,
  rounded down. A tier with two lines (tier 1 Skeleton and Plague Bearer, tier 2 Ghoul and Death Weaver, tier 3 Vampire and Wight, tier 4 Dark
  Knight and Lich) is split between them in a random proportion (game RNG), at every level.
- **Base or upgraded**: the base creature is raised; it joins a stack of the same creature or a free slot. Only when there is neither, and the
  army has the upgraded creature's stack, it is raised as the upgrade, at **3/4** of the base count (rounded down). Otherwise it is lost.
- **When**: after every victory over an enemy that was defeated (heroes and neutral guards), never when the enemy retreated or
  surrendered; automatic. A separate screen shows what rose, after the battle result and before any level-up (`UndeadRaised`).
- Rules in `NecromancyRules`, applied by `BattleService`. Computer heroes take the skill whenever it is offered.

## Faction groups

Stronghold and Swamp Bestiary have no magic school. Since ADR-069 both have **Primal Fury** (working title, id `rage`); the **Adrenaline**
group is dropped (its old ids read as the Primal Fury skills). Only heroes of the two
towns are offered the group. Their **mages** start with it; their **warriors** do not, but are offered it at a high weight so they learn it
soon (ADR-072, HERO-7). Stronghold warriors start with an **Offense** skill, Swamp Bestiary warriors with a **Defense** skill. Skills are
4 + 3 + 3 points; the ids stay `rage.first`, `rage.second` and `rage.third`.

These mages have no spells (ADR-072, MAG-10): their **Spell Power** strengthens Blood Rage and the warcries (numbers when built), Knowledge
does nothing, and their level-ups use **even odds, 25 / 25 / 25 / 25 %** at every level (ADR-073).

### Primal Fury (Stronghold and Swamp Bestiary, ADR-067, ADR-069, draft numbers)

| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Blood Rage (4 points) | `rage.first` | Master | an own stack that kills a creature in an attack gets +1 Attack, up to +2 until the battle ends | up to +4 | up to +6 | up to +8 | ✅ |
| Warcry / Spiritual Totems (working names, 3 points) | `rage.second` | Expert | Rally: own stacks +1 morale, 3 rounds | + Intimidate: enemy stacks −1 morale, −20 % initiative, 2 rounds | + Terror: each enemy stack loses its next action with 30 % | – | ✅ |
| Interference (3 points) | `rage.third` | Expert | the enemy hero's spells have −2 Spell Power and cost +1 mana | −4, +2 | −6, +3 | – | ⏳ data only, needs casting (Phase 5) |

Warcries cost no mana (the group's mages have no school): one warcry a round, each once per battle. A warcry is a free action of the hero:
the player may use it whenever one of their units is up, and it does not use that unit's action (the same holds for spells, ADR-071, MAG-4). Rally and
Intimidate count from the round after the one they were used in; Terror takes effect at once. The Shamanism drafts in
[spells.md](spells.md) are the source of the warcries.

## Specialties

Each roster hero has one HoMM3-style specialty ([hero-roster.md](hero-roster.md)):

| Kind | Example | Status |
|---|---|---|
| Resource | `resource gold 350`: +350 gold a day | ✅ (`HeroIncome`) |
| Skill | `skill offense.melee`: the skill's effect +5 % per hero level (HoMM3-like, ADR-072, HERO-1) | ✅ Melee, Archery, Logistics, attunement mana (ADR-083); 🔜 Tactics, Blood Rage, Warcry, Necromancy |
| Spell | `spell light.bless`: the spell's effect +3 % per hero level (ADR-072, MAG-6) | ⏳ magic |

Planned (ADR-093, ADR-107; the HERO-8 proposals, for now): only 4 heroes with +350 gold and 6 with +1 sulfur, crystal or mercury a day
(no two of one faction each, a town may have one of each; no ore or lumber specialists); 2 Necromancy specialists (Necropolis, a mage and a
warrior: +5 points on top of the skill, stacking with the Necromancy Amplifier, only with the skill) and 2 Logistics specialists (a flat
+10 % movement on land, even without the skill). The heroes that lose their specialty get one in their first starting skill (+5 % per
level). Other skill specialties keep +5 % per hero level.

## Open

- A luck skill (none for now).
- The final names Primal Fury, Warcry / Spiritual Totems and Alertness (CONT-5); Leadership and Alertness numbers are placeholders.
- Confirmed (ADR-075): the deployment zone includes the war machines' column.
- Sieges themselves (Ballistics numbers are placeholders).
