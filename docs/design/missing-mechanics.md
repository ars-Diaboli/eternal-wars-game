# Missing mechanics: what HoMM3 (with HotA) and HoMM5 have that we do not

A planning list (ADR-116), not a plan; updated 2026-11-04. Rows leave when built. Deliberate departures are in
[departures.md](departures.md).

**Status**: **Missing** (nothing decided) · **Planned** (roadmap or ADR) · **Partly** (some exists) · **Dropped** (decided against).
**Candidate** in Notes marks a good next pick (small, fits what exists).

## Adventure map

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Sea travel: boats, shipyards, whirlpools, sea objects | HoMM3, HotA | Dropped (ADR-051) | Rivers and shallow water are scenery (ADR-077); no Fly / Water Walk spells |
| Grail, obelisks, puzzle map, digging | HoMM3, HoMM5 | Yes | ADR-126, ADR-140 |
| Spell scrolls (an artifact holding one spell) | HoMM3 | Yes | ADR-126, ADR-136 |
| Seer's Huts, Quest Guards / Gates, map events with conditions and variables | HoMM3, HotA | Yes | events (ADR-124), quests (ADR-127), loops, run events, the game app's windows and quest log (ADR-128), the editor's Events tab and the AI (ADR-129) |
| University (buy secondary skills) | HoMM3 | Yes | ADR-126, ADR-139, with a War Academy for the general groups |
| Scholar (one-time stat, skill or spell) | HoMM3 | Missing | Small one-time object like the Learning Stone |
| Pyramid (a level-4 spell behind a guard) | HoMM3 | Missing | Shrines exist (ADR-120); a guarded spell object is the step up; **candidate** |
| Freelancer's Guild / selling creatures | HoMM3 | Missing | The Sacrificial Altar takes creatures for experience instead |
| Tavern rumours | HoMM3 | Missing | Flavour text in the tavern screen (ADR-114); **candidate** |
| Hero sleep / wake; next-hero cycle skips sleepers | HoMM3 | Missing | UI only |
| Town conversion to your own faction | HoMM5 | Missing | Big rule; needs the per-town building tables mapped |
| Castle Gate town building (move between own towns) | HoMM3, HoMM5 | Partly | The map's Town Gate (ADR-068) and Town Portal spell (ADR-120) exist |
| Simultaneous turns | HotA | Planned | LAN, simultaneous first week (MP-2, ADR-098) |

## Towns and economy

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Each town's unique buildings | HoMM3, HoMM5 | Partly | The first set built, one per town (ADR-126, ADR-138); the Undead Transformer planned (ADR-137); more open (TOWN-12) |
| Grail building | HoMM3, HoMM5 | Yes | +5000 gold, +50 % growth, siege bonus (ADR-130, ADR-140) |
| Alternative creature upgrades (pick one of two) | HoMM5 TotE | Missing | One upgrade per line now |
| Resource silo per faction resource | HoMM3 | Partly | Ours gives lumber and ore (ADR-046) |
| Artifact merchant as a town building | HoMM3 | Partly | Only the map's Artifact Merchant (ADR-079) |
| Mage guild spell research (swap a spell for gold) | HotA | Missing | Guild spells can be fixed by the map (ADR-123); **candidate** |
| Hero portraits in the tavern | HoMM3 | Partly | Tavern screen exists (ADR-114); portraits wait for art |

## Heroes and skills

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Information by skill: seeing more of enemy armies and the map | HoMM4 Scouting, HoMM3 count words | Planned | ADR-125 |
| Skill perks and the skill wheel | HoMM5 | Missing | Our skills have four levels and no perks; a big redesign |
| Racial skills (Training, Gating, Counterstrike, Irresistible Magic, Avenger, Artificer, Runelore) | HoMM5 | Partly | Necromancy and Primal Fury exist |
| Diplomacy (cheaper surrender, more creatures join) | HoMM3 | Missing | Joining by disposition exists (ADR-040) |
| Eagle Eye, Scholar, Learning, Intelligence, Sorcery, Mysticism | HoMM3 | Dropped (ADR-069) | Attunement and Mastery cover mana, cost and ranks; Healing covers First Aid |
| Heroes exchanging spells | HoMM3 (Scholar) | Missing | |
| The hero as a unit in battle | HoMM5 | Missing | Our hero casts and uses warcries, like HoMM3 |
| Artifact crafting (Artificer, runes) | HoMM5 | Partly | Assembled relics exist (ADR-121) |
| Creature specialties that grow with level | HoMM3 | Partly | Flat preferred-creature bonus (ADR-048) |

## Combat

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Stack health bars | HoMM5 | Planned | ADR-118: top creature's hit points, green / yellow / red |
| Obstacles that split the field | HoMM3 | Planned | ADR-118 |
| Terrain-themed and special battlefields | HoMM3 | Partly | Native-terrain bonus exists (ADR-068); themes are a roadmap follow-up |
| Raise Dead, field walls (Fire Wall, Bone Wall), Teleport, Hypnotize, clones | HoMM3, HoMM5 | Planned | 16 battle spells left (ADR-123): field zones, copies, control changes, two-click aiming |
| Creature abilities at scale (aging, poison, death stare, petrify, …) | HoMM3, HoMM5 | Partly | HoMM4-style capabilities, at most 5 a creature (ADR-042) |
| Battle replay viewer | HotA | Partly | Replays are recorded; the viewer is planned (UI-2) |

## Game modes and flow

| Mechanic | From | Status | Notes |
|---|---|---|---|
| Campaigns with cutscenes and carried-over heroes | HoMM3, HoMM5 | Planned | Phase 6; FLOW-1 open |
| Tutorial | HoMM5 | Planned | The prologue (CAMP-1, ADR-118) |
| AI difficulty levels (how well it plays) | HoMM3, HoMM5 | Missing | Complexity sets resources only (ADR-063) |
| Duel mode / arena | HoMM5, HotA | Planned | Before 1.0 (ADR-097, ADR-100) |
| Online lobby | HotA, HoMM5 | Planned | LAN before 1.0 (ADR-098); online later |
| Right-click information windows | HoMM3, HoMM5 | Planned | UI-3 (ADR-118) |
