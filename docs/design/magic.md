# Magic

> Part of the [game design](overview.md). Spell catalog: [spells.md](../content/spells.md) (design draft). Open items ❓:
> [open-questions.md](../project/open-questions.md). Casting is Phase 5; mana and the school skills exist.

- **Six schools** (ADR-047, ADR-063), one for each town on the ring: Light (Haven), Nature (Sylvan), Arcana (Academy),
  Death (Necropolis), Chaos (Inferno), Shadow (Dungeon); names are working titles ❓. Each school is also a hero skill
  group; any hero may learn any school, a sworn enemy's school is rarely offered ([heroes.md](heroes.md)).
- **Stronghold and Swamp Bestiary have no school**: both have the faction group **Primal Fury** instead (ADR-063, ADR-069;
  Swamp Bestiary's Adrenaline group is dropped). Its warcries replace spells (no mana, ADR-067); its Interference skill
  weakens and raises the cost of the enemy hero's spells. Instead of a mage guild they get a **3-level placeholder
  building** that teaches warcries / spiritual totems (ADR-071, MAG-2; not built).
- **Four spell levels** and a **four-level mage guild**; every spell has three levels of its own (upgraded twice).
- **Spell books and learning** (ADR-071, MAG-2; not built): mage heroes start with a spell book; warriors buy one in a mage
  guild for **500 gold**. A hero learns a spell when its school's Magic skill is at least the spell's level. A guild offers
  spells of the town's own school: **3** at each of levels 1 and 2, **2** at each of levels 3 and 4. Two more town buildings
  add spells from other schools by the magic circle (HoMM4-like; details MAG-12).
- A school's skills: **Magic** (4 levels, unlocking spell levels), **Attunement** (4 levels: max mana, daily mana and
  spell cost by school, ADR-063, table reworked by ADR-069; no spell "potency", ADR-071; Death attunement also gives **Necromancy**), **Mastery**
  (2 levels: spell upgrades). Tables: [skills.md](../content/skills.md#magic-schools).
- **Mana** (implemented, `ManaRules`): the pool is 10 × Knowledge plus attunement; a hero regains ⌈Knowledge / 10⌉
  plus attunement at the start of its owner's turn; a spell costs its base minus attunement, never less than 25 % of
  the base (rounded up). ADR-069 adds **10 × ⌈level / 10⌉** to the pool (not built yet). A hero in a town with a mage guild refills its mana (ADR-071, MAG-3; not built).
- **Necromancy** (ADR-069, not built): after a victory a hero with Death attunement raises 5–20 % of the slain enemies' value
  as undead, of tiers capped by the attunement level; a Necropolis building, the Necromancy Amplifier, adds 5 %
  ([skills.md](../content/skills.md#necromancy-death-attunement-adr-069-not-built)). Open: MAG-7.
- **Map objects for magic** (ADR-069, ADR-070; not built; [adventure-map.md](adventure-map.md#magic)):
  - **Mana Vortex**: sets the hero's mana to twice the maximum until it is spent, once a week per hero.
  - **Fountain of Magic**: restores 50 % of the maximum, once a day per hero *(assumed)*.
  - **Shrines** (levels 1–3): teach their spell (set in the editor or rolled at game start) to any hero with the school's
    Magic skill at the spell's level; any number of visits; a hero without the skill or with the spell gets a message.
- **Spell Power** (ADR-071, MAG-3; not built): multiplies damage spells' damage and buffs' and debuffs' duration; the numbers
  come with the spell list (MAG-1).
- The hero casts **at most once a round** in battle, during the turn of one of the units it controls (machines firing on
  their own do not count); spells that change initiative do not change this. Spells may target war machines, which are
  `mechanical` and resist many spells (ADR-070, MAG-4 part). Casting does not use the unit's action; it still acts afterwards (HoMM3; ADR-071, MAG-4).
  Creature casters use the Caster capability ([combat.md](combat.md#creature-capabilities-homm4-style-implemented-adr-042)).
- Spell resistance and immunity tags exist on creatures and take effect with magic.
- The hero skill **Spell resistance** cuts hostile spells' damage and duration by **8 / 14 / 20 %** (ADR-071, MAG-6; was
  10 / 15 / 20 %); durations round away from zero, at least 1 round. Not built.
- **Adventure spells** Town Portal and Dimension Door are **level 4** (ADR-071, MAG-5; [spells.md](../content/spells.md#adventure-spells-proposal)).
