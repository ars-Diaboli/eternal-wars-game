# Missing mechanics: what HoMM3 (with HotA) and HoMM5 have that we do not

The owner asked for this list (ADR-116): game mechanics present in **HoMM3 + Horn of the Abyss** or **HoMM5** (with its expansions) that
Eternal Wars does not have, as of 2026-11-02. It is a list for planning, not a plan; each row says where the thing stands. Departures we
chose on purpose are in [departures.md](departures.md).

**Status**: **missing**: nothing decided. **Planned**: on the [roadmap](../project/roadmap.md) or decided in an ADR. **Dropped**: decided
against. **Partly**: some of it exists. **Candidate**: listed in the last column as a good next pick (small, fits what exists).

## Adventure map

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Sea travel: boats, shipyards, whirlpools, sea objects (sirens, mermaids, buoys, shipwrecks, lighthouse) | HoMM3, HotA | **Dropped** (ADR-051) | No sea; rivers and shallow water are scenery (ADR-077) |
| The Grail, obelisks and the puzzle map; digging | HoMM3, HoMM5 (Tear of Asha) | Missing | A dig action, obelisks that reveal pieces, the Grail building per town; **candidate** for a late Phase 5 item |
| Adventure spells cast on the map (Town Portal, Dimension Door, View Air / Earth, Fly, …) | HoMM3, HoMM5 | Partly | 11 adventure spells are in `spells.csv`; casting them on the map is not built (Phase 5) |
| Spell scrolls (an artifact that holds one spell) | HoMM3 | Missing | Fits the artifact table and the guild; **candidate** with adventure spells |
| Shrines of Magic | HoMM3 | Planned | Roadmap, Phase 5 (ADR-069, ADR-070) |
| Seer's Huts, Quest Guards, Quest Gates, map events with conditions and variables | HoMM3, HotA | Planned | EVT-1 … EVT-6 answered (ADR-115); Phase 5 item |
| University (buy secondary skills for gold) | HoMM3 | Missing | Our Witch Hut teaches one skill a level; a university would sell four |
| Scholar (a one-time primary stat, skill or spell) | HoMM3 | Missing | A small one-time object like the Learning Stone |
| Pyramid (a level-5 spell behind a guard) | HoMM3 | Missing | With adventure spells and Shrines |
| Freelancer's Guild / selling creatures | HoMM3 | Missing | The Sacrificial Altar takes creatures for experience instead |
| Hero placeholders, random towns, random heroes and random monsters by level in the editor | HoMM3 | Partly | Prisons and monster counts can be left to the game; random monster *types* by tier and random town objects are missing |
| Map bans (heroes, artifacts, spells, skills the map forbids) | HoMM3 editor, HotA | Missing | A map rule list read by the roster, the artifact roll, the guild draw and level-ups; **candidate** |
| Tavern rumours | HoMM3 | Missing | Flavour text in the tavern screen (ADR-114 has the screen now); **candidate** |
| Hero "sleep" and "wake", and the next-hero cycle skipping sleepers | HoMM3 | Missing | UI only |
| Town conversion: rebuild a captured town to your own faction | HoMM5 | Missing | A big rule; would need the per-town building tables mapped |
| Town Portal / Castle Gate building (move between own towns) | HoMM3, HoMM5 | Partly | The map's Town Gate object exists (ADR-068); a town building is missing |
| Simultaneous turns | HotA | Planned | LAN: a simultaneous first week (MP-2, ADR-098) |

## Towns and economy

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Each town's unique buildings (Lighthouse, Cover of Darkness, Portal of Summoning, Skeleton Transformer, …) | HoMM3, HoMM5 | Partly | The Necromancy Amplifier is the first; "every town's unique building comes later" (roadmap) |
| The Grail building | HoMM3, HoMM5 | Missing | With the Grail above |
| Creature alternative upgrades (two upgrades per line, pick one) | HoMM5 TotE | Missing | Our towns have nine lines with one upgrade each (7 of 9 built) |
| Resource silo per faction resource | HoMM3 | Partly | Ours gives lumber and ore to every town (ADR-046) |
| Artifact merchant in a town building | HoMM3 | Partly | Only the map's Artifact Merchant (ADR-079) |
| Mage guild spell research (swap a spell for gold) | HotA | Missing | **Candidate** once casting is complete |
| Hero portraits in the tavern, tavern rumours, hiring the last week's heroes | HoMM3 | Partly | The tavern screen exists (ADR-114); no portraits yet (art) |

## Heroes and skills

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Skill perks and the skill wheel (abilities with prerequisites, ultimates) | HoMM5 | Missing | Our skills have four levels and no perks; a big redesign, not a small item |
| Racial skills (Training, Gating, Counterstrike, Irresistible Magic, Avenger, Artificer, Runelore) | HoMM5 | Partly | Necromancy and Primal Fury (Blood Rage, Warcry) are ours; the others are missing |
| Diplomacy (cheaper surrender, creatures joining more often) | HoMM3 | Missing | Joining by disposition exists (ADR-040) |
| Eagle Eye (learn the enemy's spells), Scholar (swap spells between heroes), Learning (more experience), Intelligence, Sorcery, Mysticism, First Aid as skills | HoMM3 | Partly | Our attunement skills cover mana and cost (ADR-069); Healing covers First Aid; the others are missing on purpose (ADR-069 reworked the list) |
| Heroes exchanging spells | HoMM3 (Scholar) | Missing | |
| The hero as a unit in battle (acts in the initiative order, attacks) | HoMM5 | Missing | Our hero casts and uses warcries, like HoMM3 |
| Artifact crafting (Artificer, rune magic) | HoMM5 | Missing | Assembled relics are planned (ADR-108) |
| Hero specialties for creatures (better stats for a line with levels) | HoMM3 | Partly | The preferred creature is a flat bonus (ADR-048) |

## Combat

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Hit points of a stack shown as a bar | HoMM5 | Planned | The owner's note (ADR-116): green, yellow, red |
| Obstacles that split the field (long cliffs) | HoMM3 | Planned | The owner's note (ADR-116) |
| Terrain-themed battlefields and backdrops, special battlefields (magic plains, cursed ground, fiery fields) | HoMM3 | Partly | Native-terrain bonus exists (ADR-068); themes are a roadmap follow-up |
| Mass spells at Expert, spell ranks | HoMM3, HoMM5 | Planned | Rank I casting exists (ADR-110); ranks II–III with Mastery (Phase 5) |
| Summoning, resurrection, animate dead | HoMM3, HoMM5 | Planned | MAG-13 settled summons (ADR-106); not built |
| Creature abilities at scale (aging, poison, death stare, petrify, …) | HoMM3, HoMM5 | Partly | HoMM4-style capabilities, at most 5 a creature (ADR-042) |
| Battle replay | HotA | Partly | Whole-game replays exist; the viewer is planned (UI-2) |

## Game modes and flow

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Campaigns with cutscenes and carried-over heroes | HoMM3, HoMM5 | Planned | Phase 6; FLOW-1 open; a prologue is proposed (CAMP-1) |
| A tutorial | HoMM5 | Missing | Proposed as the prologue (CAMP-1, ADR-116) |
| AI difficulty levels (how well the computer plays, beyond starting resources) | HoMM3, HoMM5 | Missing | Our complexity sets resources only (ADR-063) |
| Duel mode / arena | HoMM5 duel, HotA arena | Planned | Before 1.0 (ADR-097, ADR-100) |
| Online lobby | HotA, HoMM5 | Planned | LAN before 1.0 (ADR-098); online later |
| Right-click information windows for creatures, heroes, objects, skills, spells, artifacts | HoMM3, HoMM5 | Partly | Tooltips exist in places; the owner's note asks for windows and an advanced mode (UI-3) |
