# Heroes

> Part of the [game design](overview.md). Everything here is built unless tagged *(planned)*; numbers are drafts. Full lists:
> [hero-roster.md](../content/hero-roster.md) (data: `src/Core/data/heroes.csv`) and [skills.md](../content/skills.md) (every skill and
> level). Open items ❓: [open-questions.md](../project/open-questions.md).

## Roster
ADR-048. **16 heroes per playable town**: a **mage** and a **warrior** for each of its eight tier 1–4 creature lines.
- That line is the hero's **preferred creature**: +2 Attack, +2 Defense and +1 battlefield speed for its base and upgraded form; hired
  heroes start with a stack of it ([tavern](towns-and-economy.md#tavern)).
- **Specialty**: a skill (+5 % of the skill's effect per hero level, ADR-083, ADR-114), a spell (+3 % per level, [magic.md](magic.md#casting-in-battle))
  or a daily resource; resource specialties are rare, and Logistics and Necromancy have flat ones (ADR-093, ADR-107).
- **Hiring and the hero limit**: [towns-and-economy.md](towns-and-economy.md#tavern).

## Primary stats
ADR-055. Attack and Defense (added to every stack in battle), Spell Power and Knowledge ([magic.md](magic.md#mana)). Every level raises one
by 1, odds by class: warriors 35 / 45 / 10 / 10 % (A / D / SP / K) up to level 9, then 30 / 30 / 20 / 20; mages 10 / 10 / 40 / 40, then 20 /
20 / 30 / 30. The level-up prompt shows it with the skill choice. Roster heroes start with 5 points.

## Skills and levels
HoMM4-like (ADR-048, reworked ADR-069, built ADR-078).
- Groups of **three** skills; **10 points** learn a group (4 / 3 / 3; magic schools and war machines 4 / 4 / 2); at most **6 groups**.
  Levels Basic, Advanced, Expert, Master.
- **Groups**: Offense (Melee, Archery, Marksmanship), Defense (Armor, Spell resistance, Leadership), Estates (Treasury, Prospecting,
  Stewardship), Pathfinding (Pathfinding, Logistics, Scouting), Tactics (War Tempo, Tactics, Alertness), War machines (Ballistics, Healing,
  Ammo supply), the six magic schools (Magic, Attunement, Mastery) and Primal Fury (Blood Rage, Warcry, Interference; Stronghold and Swamp
  Bestiary heroes only: their mages start with it, their warriors are offered it often).
- **Levels**: HoMM3 experience table; each level gives one skill point, spent on one of **three offered skills**. Mages are offered their
  own school more often, warriors the general groups; the arch-rival's school (across the ring) only in **5 %** of level-ups (ADR-072).

## Army, machines and artifacts
- **Army** of up to 7 stacks. A hero without creatures cannot attack.
- **War machines**: [combat.md](combat.md#war-machines).
- **Artifacts** (ADR-061, catalog [artifacts.md](../content/artifacts.md)): head, neck, cloak, body, weapon, shield, two rings, boots, three
  misc slots and a backpack of 50. Found on the map, looted from defeated heroes (the most valuable kept when the backpack is full, the rest
  lost), handed over in an exchange.
- **Governing**: [towns-and-economy.md](towns-and-economy.md#governors).
- **Hero screen** (H or the hero list): everything above plus mana and spell book; drag and drop *(planned)*.
