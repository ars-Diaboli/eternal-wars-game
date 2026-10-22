# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-10-22, third round** (ADR-072): HERO-1, HERO-3, HERO-6, HERO-7, TOWN-7, TOWN-8, RMG-11, CMB-6, MAG-3, MAG-5 … MAG-12 answered,
MAP-23 corrected; MAG-1 partly (the Spell Power formula); HERO-4 answered except where the war machines stand.
Second round (ADR-071): DATA-1, RMG-8, RMG-12, RMG-13, MAP-20, TOWN-1, TOWN-4, CMB-7, CMB-8, MAG-2, MAG-4 answered. First round (ADR-070): MAP-15 …
MAP-19, MAP-21 … MAP-24, TOWN-2, TOWN-3, TOWN-6, CMB-1, CMB-3, CMB-4, RMG-9, RMG-10 answered.

## 🚧 Blocking now

| Id | Question | Blocks |
|---|---|---|
| [HERO-4](#hero-4) | War machines during deployment | Phase 3: deployment |

Nothing else blocks the skill rework, Necromancy or the second catalogue; the assumptions below stand unless the owner says no.

## To confirm

Short answers the agent read one way (ADR-070 … ADR-072). Say "no" to any of them; otherwise they stand.

| Id | Assumption |
|---|---|
| RMG-9 | The template picks the mirror (axis: vertical, horizontal, diagonal or random; point for 4 and 8 players). |
| RMG-10 | User templates are saved in the user data folder and scanned next to the shipped ones. |
| MAP-16 | A Window of the Magi works on every visit. |
| TOWN-3 | Capturing a town with a Capitol downgrades it to town hall 3 only when the capturer already owns a Capitol. |
| TOWN-6 | The amplifier's id is `necromancy-amplifier`. |
| TOWN-3 | Town hall 3 is called "Capitol" in `buildings.csv` today; it is renamed (proposal: "City Hall") so the Capitol is level 4. |
| TOWN-4 | The Brotherhood's 4 offers become 2 native + 2 adjacent (today 2 native, 1 adjacent, 1 of the rest). |
| TOWN-7 | Stronghold and Swamp Bestiary (off the ring) keep "1 native + 1 warrior of another town" in the basic tavern. |
| MAP-18 | "Two ends per portal type" covers two-way portals; one-way portals keep several entrances sharing one exit (ADR-026). |
| TOWN-1 | Each dwelling of the tier gets the growth bonus (two tier-1 dwellings: +5 each). |
| DATA-1 | The shipped templates (`*.ewtemplate.json`, now in the same `Assets/data` folder) move to the new data folder too. |
| MAP-20 | A border guard or gate replaces the monster guard of its connection (no monster stack beside it). |
| MAG-5 | Town Portal and Dimension Door cost 25 mana like other level-4 spells (16 today). |
| MAG-5 | The movement cost is a quarter of the hero's full daily points (500 of 2000); a hero with fewer points left cannot cast. |
| MAG-3 | A guild visit raises a known spell straight to the rank the hero's Mastery allows (not one rank per visit). |
| HERO-7 | "High probability" for warriors: Primal Fury gets weight 6 in their offers (a mage's own-school weight; today 2). |
| HERO-7 | A Stronghold warrior without an Offense starting skill (or a Swamp warrior without a Defense one) swaps one of its two skills for one. |
| MAG-10 | Primal Fury mages level up with the warrior's stat odds (35 / 45 / 10 / 10 %, then 30 / 30 / 20 / 20 %). |
| CMB-1 | The AI retreats when its surviving army is worth less than 20 % of the enemy's (tuning). |

## Towns and economy

<a id="town-5"></a>
**TOWN-5 Caravan details.** Is the 5-day limit checked only when ordering (today only the route is re-checked daily)? How long may a caravan wait at a
full garrison? Proposal: only when ordering; it waits until there is room.

## Heroes

<a id="hero-4"></a>
**HERO-4 🚧 Deployment: the war machines.** Decided (ADR-072): the player places troops within the first 4 / 5 / 6 columns from their edge. Today
the machines stand in their own column at that edge (2–4 cells wide plus a gap), so at Basic a 3×3 or 4×4 stack would barely fit beside them. Are
the machines placed by the player too (inside the zone), do they stay at the edge with the zone counted in front of them, or does the zone include
their column? Proposal: the machines stay at the edge and the columns are counted in front of them.

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names and specialties in `heroes.csv`; primary stat odds and starting values (ADR-055).

## Combat

<a id="cmb-2"></a>
**CMB-2 Morale and luck sources.** Which skills, buildings and army mixes add or remove them (artifacts and Leadership exist)? Proposal: HoMM3's
mixed-factions penalty (−1 per extra faction) and a tavern +1 morale for defenders.

<a id="cmb-5"></a>
**CMB-5 Sieges** (post-1.0). Confirm sieges come after 1.0; until then the siege Ballistics rules (ADR-063) stay unused.

## Magic (Phase 5)

<a id="mag-1"></a>
**MAG-1 Spell list.** The spells and numbers per school ([spells.md](../content/spells.md)); spell tags beyond `all`, `mind`, `fire`, `curse`,
`holy`; which spells creature casters get. The Spell Power formula is set for now (ADR-072: damage and duration × (1 + SP / 10)); the
spells.md tables still use "a + b·SP" and are rewritten with the list.

## Game flow and content

<a id="flow-1"></a>
**FLOW-1 Victory conditions.**
- a. More conditions: the Grail, flag all mines, defeat all monsters?
- b. Does one ally's survival win for the whole team?
- c. How does a campaign hook into them? (Phase 6)

<a id="flow-2"></a>
**FLOW-2 Week events** ("week of X"): which events and how likely.

<a id="cont-1"></a>
**CONT-1 Lore names.** Working titles (Light, Darkness, Wild, Primordial) or the lore names (Dawn Covenant, Ashen Court, Unbound, Firstborn)?

<a id="cont-2"></a>
**CONT-2 Roster review.** Names, capabilities and numbers in `creatures.csv`; the size pass of ADR-070 comes first.

<a id="cont-3"></a>
**CONT-3 Artifacts.** The draft numbers; HoMM3-style placeholders or lore items; Crown of the Waning Eye's "best magic skill" and ties.

<a id="cont-4"></a>
**CONT-4 More twists.** Which other systems should depart from HoMM3 ([departures.md](../design/departures.md))?

<a id="cont-5"></a>
**CONT-5 Primal Fury names.** One name for both towns, or a Swamp name? "Warcry" for Stronghold and "Spiritual Totems" for Swamp Bestiary, or one?

## Numbers to tune (not questions)
Placeholders that wait for play-testing: caravan cost (ADR-054); complexity starting resources (ADR-063); Stewardship and governor percentages
(ADR-063); siege Ballistics (ADR-063); zone guard and garrison powers, `GuardPicker` ranges (ADR-064); building costs; the growth buildings (+5 / +2, ADR-071); war machine stats and prices
(CMB-3); morale 2 % and luck 4 % per point (ADR-053); deposit prices and the map marketplace threshold (ADR-070); the AI's retreat threshold.

## Answered
One line per question; the ADR holds the detail.

- **Scope**: fidelity ADR-008; art [presentation.md](../design/presentation.md); modes, platforms, harness, slice ADR-034; lore drafted by Claude.
- **Adventure map**: scale ADR-020, camera ADR-022, terrain ADR-011/045, native terrain ADR-032, roads ADR-024/032, movement ADR-018/037, fog
  ADR-017/031, gates ADR-026, meeting heroes ADR-027, resources ADR-019, chests ADR-039, mines ADR-034, guards ADR-040/065, no sea ADR-051.
- **Objects**: MAP-1 … MAP-14 ADR-068; MAP-15 … MAP-19, MAP-21 … MAP-24 ADR-070; MAP-20 ADR-071; MAP-23 corrected ADR-072.
- **Towns**: groups ADR-010/028/029, tech tree ADR-030/046, income ADR-025, caravans ADR-054, tavern ADR-060/070, governors ADR-063, TOWN-1 fort
  ADR-066, TOWN-2 ADR-067/068/070, TOWN-3 ADR-070, TOWN-6 ADR-070, TOWN-1 growth buildings and TOWN-4 ADR-071, TOWN-7 and TOWN-8 ADR-072.
- **Heroes**: roster ADR-048, skills ADR-048/069, stats ADR-055, schools ADR-063/069, artifacts ADR-061, HERO-2 ADR-069 (rest in HERO-7, MAG-9, MAG-10,
  CONT-5); HERO-1, HERO-3, HERO-6, HERO-7 and HERO-4 (but the machines) ADR-072.
- **Combat**: grid and sizes ADR-005/021/070, battlefield ADR-033, turn order ADR-009, damage ADR-033, cover ADR-035, tuning ADR-038, speeds ADR-037,
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070, CMB-7/8 ADR-071, CMB-6 check order ADR-072.
- **Magic**: MAG-4 ADR-070/071; MAG-2, MAG-3 (mana refill, Spell Power role, potency), MAG-5 (levels), MAG-6 (resistance) ADR-071; MAG-1 (Spell Power formula), MAG-3, MAG-5, MAG-6 … MAG-12 ADR-072.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070; RMG-8, RMG-12, RMG-13 ADR-071; RMG-11 ADR-070/072.
- **Data**: DATA-1 ADR-071.
