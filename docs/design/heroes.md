# Heroes

> Part of the [game design](overview.md). Full lists: [hero-roster.md](../content/hero-roster.md) (every roster hero)
> and [skills.md](../content/skills.md) (every skill). Open items ❓: [open-questions.md](../project/open-questions.md).

- **Roster** (implemented as data, ADR-048): 16 heroes per playable town, a **mage** and a **warrior** for each of its
  eight tier 1–4 creature lines. That line is the hero's **preferred creature**: +2 Attack, +2 Defense and +1 battlefield
  speed for its base and upgraded form. Each hero has a **specialty** (a skill, a spell or a daily resource).
  Edited in `data/heroes.csv`.
- **Hiring**: in a town's tavern, 2500 gold; a player has at most **8 heroes**, or fewer if the map says so (1–8, also per player on campaign-like maps, ADR-068); a tavern offers 2 heroes, the upgraded
  one (Brotherhood) 4 ([towns-and-economy.md](towns-and-economy.md#tavern-implemented-adr-060), ADR-060). ADR-070 (not
  built): the offers come from a pool per player and faction, a hired hero gets a **starter stack** of its preferred
  creature, a map tavern hires like a Brotherhood (from its own weekly pool of 4 random heroes shared by all players,
  ADR-071), and a hero who surrenders returns to the pool.
- **Primary stats** (implemented, ADR-055): Attack and Defense (added to every stack in battle), Spell Power (multiplies
  spell damage and buff/debuff duration, ADR-071; Phase 5) and Knowledge (sets the mana pool, see below). Every level raises one of them by 1, HoMM3-style,
  with odds by class (draft): warriors 35/45/10/10 % (Attack/Defense/Spell Power/Knowledge) up to level 9, then
  30/30/20/20; mages 10/10/40/40, then 20/20/30/30. The level-up prompt shows the stat with the skill choice. Roster
  heroes start with 5 points.
- **Skills** (HoMM4-like, implemented, ADR-048): groups of **three** skills; **10 points** learn a group fully (usually
  4/3/3; magic schools and war machines 4/4/2); at most **6 groups** per hero. Levels Basic, Advanced, Expert, Master.

  | Group | Skills |
  |---|---|
  The groups as reworked by ADR-069 (2026-10-20, not built yet; [skills.md](../content/skills.md) shows today's numbers next to the new ones):

  | Group | Skills |
  |---|---|
  | Offense | Melee (+9/16/23/30 % melee damage), Archery (+15/25/35 % ranged), Marksmanship (ranged penalty −2/8 / −4/8 / −6/8, ADR-070) |
  | Defense | Armor (−5 % damage taken per level), Spell resistance (−8/14/20 % to hostile spells' damage and duration, ADR-071), Leadership (+1/2/3 morale, ADR-067) |
  | Estates | Treasury (+250/500/750/1000 gold a day, Expert and Master also 0.5/1 % of the gold on hand, ADR-062), Prospecting (other resources, and a chance of +50 % from resource piles), Stewardship (cheaper buildings and creatures in the town the hero governs, ADR-063) |
  | Pathfinding | Pathfinding (−25/50/75/100 % terrain penalty), Logistics (+10/20/30 % movement), Scouting (+2/4/6 sight radius) |
  | Tactics | War Tempo (+1 battlefield speed per level), Tactics (deployment before battle, battlefield width from Advanced), Alertness (+1/2/3 initiative) |
  | War machines | Ballistics (offensive machine: automatic, controlled, targets troops from Advanced; +25/50 % damage), Healing (tent heals more), Ammo supply (cart: +50 % / unlimited shots) (ADR-056, ADR-063) |
  | 6 magic schools (Light, Nature, Arcana, Death, Chaos, Shadow) | Magic (spell levels 1–4), Attunement (mana, regeneration, spell cost; Death: Necromancy), Mastery (spell upgrades) (ADR-047, ADR-063, ADR-069) |
  | Primal Fury (Stronghold and Swamp Bestiary) | Offered only to those towns' heroes, meant for their mages: Blood Rage, Warcry, Interference (ADR-067, ADR-069; Adrenaline is dropped) |
- **Levels** (implemented): HoMM3 experience table; each level gives one skill point, spent on one of **three offered
  skills**. Mages are offered their own school more often, warriors the general groups; a sworn enemy's school only in
  5 % of level-ups (ADR-048).
- **Army** of up to 7 creature stacks (implemented). A hero without creatures cannot attack.
- **War machines** (ADR-056): every hero owns its town's offensive machine; tent and cart are bought at a blacksmith
  ([combat.md](combat.md#war-machines-implemented-adr-056-adr-063)).
- **Artifacts** (implemented, ADR-061, catalog in [artifacts.md](../content/artifacts.md)): head, neck, cloak, body,
  weapon, shield, two rings, boots, three miscellaneous slots and a backpack of 50. They add stats, morale and luck,
  movement, battle speed, damage, daily income, terrain rules and skill levels. Found on the map, looted from defeated
  heroes (the most valuable are kept when the backpack is full, the rest lost), handed over in an exchange.
- **Hero screen** (implemented): stats, mana, experience, specialty, skills, army, paper doll and backpack, war
  machines; opened with H or from the hero list. Drag and drop is pending.
- **Mana** (implemented, ADR-063): the pool is 10 × Knowledge (plus attunement); a hero regains ⌈Knowledge / 10⌉ (plus
  attunement) at the start of its owner's turn. ADR-069 adds 10 × ⌈level / 10⌉ to the pool (not built yet). **Spell book** and casting: Phase 5 ([magic.md](magic.md)).
  ADR-071 (not built): mage heroes start with a spell book, warriors buy one in a mage guild for 500 gold; mana refills in a
  town with a mage guild.
- **Governors** (ADR-063): a hero may govern a town; with Stewardship that town's buildings and creatures are cheaper
  ([towns-and-economy.md](towns-and-economy.md)).
