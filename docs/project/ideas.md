Raw design notes from the owner. Once a note is implemented or recorded ([decisions.md](decisions.md), the
[design docs](../design/overview.md)), it is removed from here; open points move to [open-questions.md](open-questions.md).

Recorded so far:
- Heroes, skills and magic: ADR-047 / ADR-048 (2026-10-16).
- Roster notes and spell resistance: ADR-049 (2026-10-16). No sea travel: ADR-051.
- Morale and luck: ADR-053 (2026-10-16). War machines: ADR-056 (2026-10-16).
- Treasury and Prospecting: ADR-062 (2026-10-17). Caravans and map dwellings: ADR-054 (2026-10-17).
- Stewardship and governors, starting resources (complexity), Ballistics against fortifications, six magic schools with
  attunement and mana, faction groups for Stronghold and Swamp Bestiary: ADR-063 (2026-10-17).
- Marketplace rates, Leadership, the defense initiative skill, the Stronghold Primal Fury group: ADR-067 (2026-10-18).
- Marketplace rounding, artifact tiers, and the whole adventure-map object catalogue (resource providers, loot, dwelling
  guards, creature banks, power-ups, buffs, utilities): ADR-068 (2026-10-19), table in
  [adventure-map.md](../design/adventure-map.md#planned-objects-adr-068).
- Attunement table, Necromancy and the Necromancy Amplifier, mana by level, the Pathfinding / Tactics / Defense / Offense
  rework (Marksmanship, distance penalty), Primal Fury for Swamp Bestiary (Adrenaline dropped), and the second object catalogue:
  ADR-069 (2026-10-20); tables in [skills.md](../content/skills.md) and
  [adventure-map.md](../design/adventure-map.md#second-object-catalogue-adr-069).
- Answers to the ADR-069 questions (Mage's Chest, Window of the Magi, map tavern, portals, garrison, deposits, banks re-drafted per faction, Mana Vortex,
  shrines, Capitol, per-town buildings, tavern pools, Necromancy Amplifier cost, surrender, distance penalty, casting once a round), teams 2–8,
  creature sizes, mirrored templates and the template editor: ADR-070 (2026-10-22). The data-table note is DATA-1; the question clean-up is done
  (open-questions.md, 2026-10-22).
- Warcraft III-like terrain relief (walkable 0–4 cell widths of height, water below, cliffs or mountains above): ADR-075 (2026-10-23),
  [presentation.md](../design/presentation.md).
- Map scenery, rivers, flaggable resource providers and resource caravans, caravan speed (2500 a day) and limit (6, 8 with a governor), the
  kingdom overview, the Thieves' Guild, and the third object catalogue (Sphinx, Sanctuary, Sacrificial Altar, Refugee Camp, Den of Thieves,
  Cartographer, Astrologer's Tower, Artifact Merchant, Witch Hut; the Subterranean Gate exists): ADR-077 (2026-10-26), questions MAP-25 …
  MAP-34 and TOWN-9 … TOWN-11; [adventure-map.md](../design/adventure-map.md#third-object-catalogue-adr-077).
- Diagonal battle steps at √2, hero and battle speed settings, the turn timer, auto combat with accept or reject, and LAN multiplayer (host as the
  source of truth, saves that load in multiplayer, single player or hot-seat, a simultaneous first week): ADR-088 (2026-10-28), questions
  CMB-9, CMB-10, UI-1, FLOW-3, MP-1 and MP-2.
- A HoMM3-like victory screen with a score for maps that are not campaign-like, ranking tables separate for random and shipped maps, saving
  a replay from the ranking table, a leaderboard and "watch a replay" (finished games and unfinished saves) in the main menu: ADR-092
  (2026-10-29), questions FLOW-4 and UI-2.
- Rarer resource specialties (4 gold, 6 rare resources), 2 Necromancy and 2 Logistics specialists with flat bonuses: ADR-093 (2026-10-29),
  question HERO-8, answered in ADR-107 (the proposals, for now).
- Pandora's Box (HoMM3 / HoMM4-like) and map events like HotA's extended event system (hero, player, town and quest events with
  conditions, actions and variables; Seer's Huts, Quest Guards and Quest Gates; the AI weighing them): ADR-096 (2026-10-29), questions
  MAP-35 (answered: ADR-100) and EVT-1 … EVT-6.
- Arena mode like HotA's (a day of preparation, then one battle between two heroes decides the game): ADR-097 (2026-10-29), questions ARN-1
  and ARN-2 (answered: ADR-100, before 1.0 with LAN, three shipped maps).

- Map events: EVT-1 … EVT-6 answered with the proposals (ADR-115, 2026-11-02); a Phase 5 item in three steps.
- A prologue / tutorial (Haven against neutrals and the undead, placeholder lore), stack health bars (green over 50 %, yellow 21–50 %, red
  20 % and below), obstacles that split the battlefield, more scenery on random maps, a spacing pass and a configurable layout (initiative
  bar, minimap corner, hero and town lists, action panel), information windows and advanced tooltips, icon prompts
  ([icon-prompts.md](../content/icon-prompts.md)) and a list of the HoMM3 / HotA / HoMM5 mechanics we lack
  ([missing-mechanics.md](../design/missing-mechanics.md)): ADR-116 (2026-11-02), questions CAMP-1, UI-3 and CMB-12, answered in ADR-118 (2026-11-03: the proposals, but the health bar shows the top creature's
  hit points).

- Troops break the siege gate too (100 hit points, 150 with upgraded fortifications, Defense 10, a blow capped at 50; a war machine still
  takes it in one hit): ADR-120 (2026-11-03), built.

Still to decide: see [open-questions.md](open-questions.md) (the open questions by area).


Skills - scouting : reveal additional information on enemy / environment - similar to homm3;