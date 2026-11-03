# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-11-03** (ADR-118): CAMP-1 and UI-3 answered: the proposals; CMB-12 answered: the top creature's hit points, the
proposals for the rest.
Before that (ADR-115, ADR-116): EVT-1 … EVT-6 (map events) answered: the proposals; new: CAMP-1 (prologue), UI-3 (layout and
information windows), CMB-12 (health bars, dividing obstacles).
Before that (ADR-107, ADR-108): HERO-8 (rarer specialties) answered: the proposals, for now; CONT-7 (assembled relics)
answered, with six more relics.
Before that (ADR-106): CMB-11 (siege rules) and MAG-13 (guild spell draw, spell elements, summons) answered.
Before that (ADR-101): FLOW-4 (victory screen and ranking) and UI-2 (replays and the main menu) answered.
Before that (ADR-100): CONT-6 (artifacts with HoMM names, assembled relics), ARN-1 and ARN-2 (the arena before 1.0, with
LAN and three shipped maps) and MAP-35 (Pandora's Box) answered; new: CONT-7 (assembled relics, the dropped effects).
Before that (ADR-099): CMB-5 (sieges before 1.0), MAG-1 and CONT-3 (spells and artifacts re-drafted), MP-3 and MP-4
answered; new: CMB-11 (siege rules), MAG-13 (guild selection, spell review), CONT-6 (artifact review).
Before that (ADR-098): MP-1 and MP-2 (LAN multiplayer) answered; they bring MP-3 (clients that see only their own view)
and MP-4 (start zones).
Before that (ADR-097), the arena mode brought ARN-1 and ARN-2.
Before that (ADR-096), new ideas brought MAP-35 (Pandora's Box) and EVT-1 … EVT-6 (map events like HotA's extended events,
quests) ([below](#new-ideas-adr-096-2026-10-29)).
Before that (ADR-092, ADR-093), new ideas bring FLOW-4 (victory screen and ranking), UI-2 (replays) and HERO-8 (specialties);
UI-2 follows the owner's revised note (the replay saved from the ranking table, a leaderboard
in the main menu, unfinished saves watched too).
Before that (ADR-090, ADR-091): CMB-10 (auto combat, built), UI-1 (speed settings, built ADR-095) and FLOW-3 (turn timer, built) answered. Before that (2026-10-28, ADR-088), new
ideas brought CMB-9, CMB-10, UI-1, FLOW-3, MP-1 and MP-2 ([below](#new-ideas-adr-088-2026-10-28)); CMB-9 is answered and built (ADR-089).
Before that (2026-10-26, ADR-079, ADR-080), the sixth round: TOWN-9 … TOWN-11 and MAP-25 … MAP-34 answered and built (the Astrologer's
Tower waits for the week events). Before that (ADR-077): new ideas (scenery, rivers, flaggable providers, caravans, Thieves' Guild, the third object catalogue) bring
MAP-25 … MAP-34 and TOWN-9 … TOWN-11. Fifth round (ADR-075): the ADR-073 assumptions confirmed (TOWN-5's reach and FLOW-2's first week
changed); CMB-2 answered; town-less campaign players.
Fourth round (ADR-073): the ADR-070 … ADR-072 assumptions confirmed (MAG-5 and MAG-10 changed); HERO-4, TOWN-5,
FLOW-2 answered; FLOW-1 except the campaign hooks.
Third round (ADR-072): HERO-1, HERO-3, HERO-6, HERO-7, TOWN-7, TOWN-8, RMG-11, CMB-6, MAG-3, MAG-5 … MAG-12 answered,
MAP-23 corrected; MAG-1 partly (the Spell Power formula); HERO-4 answered except where the war machines stand.
Second round (ADR-071): DATA-1, RMG-8, RMG-12, RMG-13, MAP-20, TOWN-1, TOWN-4, CMB-7, CMB-8, MAG-2, MAG-4 answered. First round (ADR-070): MAP-15 …
MAP-19, MAP-21 … MAP-24, TOWN-2, TOWN-3, TOWN-6, CMB-1, CMB-3, CMB-4, RMG-9, RMG-10 answered.

## 🚧 Blocking now

Nothing blocks the current work.

## Heroes

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names and specialties in `heroes.csv`; primary stat odds and starting values (ADR-055).

## Game flow and content

<a id="flow-1"></a>
**FLOW-1 Campaign hooks** (Phase 6). How does a campaign hook into the victory and defeat conditions (ADR-073 sets the conditions)?

<a id="cont-1"></a>
**CONT-1 Lore names.** Working titles (Light, Darkness, Wild, Primordial) or the lore names (Dawn Covenant, Ashen Court, Unbound, Firstborn)?

<a id="cont-2"></a>
**CONT-2 Roster review.** Names, capabilities and numbers in `creatures.csv`. The size pass is done (ADR-074): 9 Large, the seven dragons and
the Behemoth line; whether a non-dragon line stays Large (Behemoths, Hydras or none) is decided once there are visuals.

<a id="cont-4"></a>
**CONT-4 More twists.** Which other systems should depart from HoMM3 ([departures.md](../design/departures.md))?

<a id="cont-5"></a>
**CONT-5 Primal Fury names.** One name for both towns, or a Swamp name? "Warcry" for Stronghold and "Spiritual Totems" for Swamp Bestiary, or one?

## Numbers to tune (not questions)
Placeholders that wait for play-testing (kept as they are, ADR-118): caravan cost (ADR-054); complexity starting resources (ADR-063);
Stewardship and governor percentages (ADR-063); siege Ballistics (ADR-063); zone guard and garrison powers, `GuardPicker` ranges (ADR-064);
building costs; the growth buildings (+5 / +2, ADR-071); war machine stats and prices (CMB-3); morale 2 % and luck 4 % per point (ADR-053);
deposit prices and the map marketplace threshold (ADR-070); the AI's retreat threshold; the fairness tolerances and slacks (ADR-117);
the gate's cap and Defense, Earthquake, summon amounts, a creature caster's Spell Power (count / 2) and the Phoenix's 30 %, shrine values
(ADR-120); the relics' added effects, their slots, the AI's piece bonus and the glaivethrower's 20–26 (ADR-121).
## Answered
One line per question; the ADR holds the detail.

- **Scope**: fidelity ADR-008; art [presentation.md](../design/presentation.md); modes, platforms, harness, slice ADR-034; lore drafted by Claude.
- **Adventure map**: scale ADR-020, camera ADR-022, terrain ADR-011/045, native terrain ADR-032, roads ADR-024/032, movement ADR-018/037, fog
  ADR-017/031, gates ADR-026, meeting heroes ADR-027, resources ADR-019, chests ADR-039, mines ADR-034, guards ADR-040/065, no sea ADR-051.
- **Objects**: MAP-1 … MAP-14 ADR-068; MAP-15 … MAP-19, MAP-21 … MAP-24 ADR-070; MAP-20 ADR-071; MAP-23 corrected ADR-072; MAP-35 (Pandora's Box) ADR-100.
- **Towns**: groups ADR-010/028/029, tech tree ADR-030/046, income ADR-025, caravans ADR-054, tavern ADR-060/070, governors ADR-063, TOWN-1 fort
  ADR-066, TOWN-2 ADR-067/068/070, TOWN-3 ADR-070, TOWN-6 ADR-070, TOWN-1 growth buildings and TOWN-4 ADR-071, TOWN-7 and TOWN-8 ADR-072, TOWN-5 and base-creature map dwellings ADR-073, TOWN-5 reach ADR-075.
- **Heroes**: roster ADR-048, skills ADR-048/069, stats ADR-055, schools ADR-063/069, artifacts ADR-061, HERO-2 ADR-069 (rest in HERO-7, MAG-9, MAG-10,
  CONT-5); HERO-1, HERO-3, HERO-6, HERO-7 and HERO-4 ADR-072, HERO-4's war machines ADR-073; HERO-8 (rarer specialties, the proposals for now) ADR-107.
- **Combat**: CMB-5 (sieges before 1.0) ADR-099; CMB-11 (siege rules) ADR-106; grid and sizes ADR-005/021/070, battlefield ADR-033, turn order ADR-009, damage ADR-033, cover ADR-035, tuning ADR-038, speeds ADR-037,
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070, CMB-7/8 ADR-071, CMB-6 check order ADR-072, CMB-2 ADR-075, CMB-9 (movement as a circle) ADR-089, CMB-10 (auto combat) ADR-090, CMB-12 (health bars, dividing obstacles) ADR-118.
- **Magic**: MAG-13 (guild spell draw, spell elements, summons) ADR-106; MAG-1 (the spell list re-drafted) ADR-099; MAG-4 ADR-070/071; MAG-2, MAG-3 (mana refill, Spell Power role, potency), MAG-5 (levels), MAG-6 (resistance) ADR-071; MAG-1 (Spell Power formula), MAG-3, MAG-5, MAG-6 … MAG-12 ADR-072; MAG-5 (fixed 500 points) and MAG-10 (even stat odds) ADR-073.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070; RMG-8, RMG-12, RMG-13 ADR-071; RMG-11 ADR-070/072.
- **Data**: DATA-1 ADR-071.
- **Presentation**: UI-1 (speed settings) ADR-090, built ADR-095; UI-2 (replays, main menu) ADR-101; UI-3 (layout, information windows) ADR-118.
- **LAN multiplayer**: MP-1 (before 1.0, host, clients that see only their view, lobby, saves) and MP-2 (simultaneous first week) ADR-098;
  MP-3 (clients' view, saves, smooth fog) and MP-4 (start zones) ADR-099.
- **Content**: CONT-3 (artifacts re-drafted) ADR-099; CONT-6 (HoMM names, assembled relics) ADR-100; CONT-7 (assembled relic rules, six more relics) ADR-108.
- **New ideas** (ADR-077): TOWN-9 … TOWN-11, MAP-25, MAP-26, MAP-28 ADR-079; MAP-27, MAP-29 … MAP-34 ADR-080 (MAP-32 built after the week
  events).
- **Game flow**: victory ADR-057; FLOW-1 (conditions, deadlines, teams) and FLOW-2 (week and month events) ADR-073, the conditions built ADR-094; FLOW-2 event start and town-less players ADR-075; FLOW-3 (turn timer) ADR-091; FLOW-4 (victory screen, ranking) ADR-101.
- **Map events**: EVT-1 … EVT-6 (scope and plan, triggers, conditions, actions, quests, AI and editor: the proposals) ADR-115.
- **Campaign**: CAMP-1 (the prologue) ADR-118.
- **Arena**: ARN-1 and ARN-2 (before 1.0, with LAN, three shipped maps) ADR-100.
- **Assumptions** of ADR-070 … ADR-072: confirmed in ADR-073; of ADR-073: confirmed in ADR-075.
