# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-10-23, fourth round** (ADR-073): the ADR-070 … ADR-072 assumptions confirmed (MAG-5 and MAG-10 changed); HERO-4, TOWN-5,
FLOW-2 answered; FLOW-1 except the campaign hooks.
Third round (ADR-072): HERO-1, HERO-3, HERO-6, HERO-7, TOWN-7, TOWN-8, RMG-11, CMB-6, MAG-3, MAG-5 … MAG-12 answered,
MAP-23 corrected; MAG-1 partly (the Spell Power formula); HERO-4 answered except where the war machines stand.
Second round (ADR-071): DATA-1, RMG-8, RMG-12, RMG-13, MAP-20, TOWN-1, TOWN-4, CMB-7, CMB-8, MAG-2, MAG-4 answered. First round (ADR-070): MAP-15 …
MAP-19, MAP-21 … MAP-24, TOWN-2, TOWN-3, TOWN-6, CMB-1, CMB-3, CMB-4, RMG-9, RMG-10 answered.

## 🚧 Blocking now

Nothing blocks a roadmap item. The assumptions below stand unless the owner says no.

## To confirm

Short answers the agent read one way (ADR-073). Say "no" to any of them; otherwise they stand.

| Id | Assumption |
|---|---|
| HERO-4 | Every offensive machine (ballista, cannon, catapult, glaivethrower) is 1 × 3, like the ballista. |
| HERO-4 | The 4 / 5 / 6 deployment columns include the machine column; stacks may use its free cells. |
| — | A map that has a dwelling of an upgraded creature loads it as the line's base creature. |
| TOWN-5 | "Raw distance" is the travel days at the creature's speed, ignoring hostile heroes, monsters and guards. |
| FLOW-1 | "Capture a map object" means an ownable one (mine, dwelling, town). |
| FLOW-1 | "Lose an artifact": no hero of the player carries it any more. |
| FLOW-1 | Missing a victory condition's deadline defeats the player. |
| FLOW-1 | When only computer players of a team survive, its eliminated human players have lost. |
| FLOW-2 | A week or month of a creature doubles its line's growth in towns and map dwellings for that one week. |
| FLOW-2 | A month roll of "nothing" (25 %) lets the weekly roll happen as usual. |
| FLOW-2 | No event in the game's first week. |

Answers/additional suggestions:
| FLOW-2 | No event in the game's first week. - default yes; make it configurable in map editor so there could be no meekly/monthly events at all or skip up to 2 months/8 weeks;
| TOWN-5 | "Raw distance" is the travel days at the creature's speed, ignoring hostile heroes, monsters and guards. remove 5 days of creature speed at all, use 7500(ignore terrain) movement points for distance evaluation;
 ignoring hostile heroes, monsters and guards. - assumption is correct;
 For campaign like scenarios/maps allow player not to have a town at all; loosing a hero becomes a defeat condition instead;

## Heroes

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names and specialties in `heroes.csv`; primary stat odds and starting values (ADR-055).

## Combat

<a id="cmb-2"></a>
**CMB-2 Morale and luck sources.** Which skills, buildings and army mixes add or remove them (artifacts and Leadership exist)? Proposal: HoMM3's
mixed-factions penalty (−1 per extra faction) and a tavern +1 morale for defenders.
Answer - tavern +1 morale for defenders : use upgraded tavern; rest of the proposal is correct;

<a id="cmb-5"></a>
**CMB-5 Sieges** (post-1.0). Confirm sieges come after 1.0; until then the siege Ballistics rules (ADR-063) stay unused.

## Magic (Phase 5)

<a id="mag-1"></a>
**MAG-1 Spell list.** The spells and numbers per school ([spells.md](../content/spells.md)); spell tags beyond `all`, `mind`, `fire`, `curse`,
`holy`; which spells creature casters get. The Spell Power formula is set for now (ADR-072: damage and duration × (1 + SP / 10)); the
spells.md tables still use "a + b·SP" and are rewritten with the list.

## Game flow and content

<a id="flow-1"></a>
**FLOW-1 Campaign hooks** (Phase 6). How does a campaign hook into the victory and defeat conditions (ADR-073 sets the conditions)?

<a id="cont-1"></a>
**CONT-1 Lore names.** Working titles (Light, Darkness, Wild, Primordial) or the lore names (Dawn Covenant, Ashen Court, Unbound, Firstborn)?

<a id="cont-2"></a>
**CONT-2 Roster review.** Names, capabilities and numbers in `creatures.csv`. The size pass is done (ADR-074): 9 Large, the seven dragons and
the Behemoth line; whether a non-dragon line stays Large (Behemoths, Hydras or none) is decided once there are visuals.

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
  ADR-066, TOWN-2 ADR-067/068/070, TOWN-3 ADR-070, TOWN-6 ADR-070, TOWN-1 growth buildings and TOWN-4 ADR-071, TOWN-7 and TOWN-8 ADR-072, TOWN-5 and base-creature map dwellings ADR-073.
- **Heroes**: roster ADR-048, skills ADR-048/069, stats ADR-055, schools ADR-063/069, artifacts ADR-061, HERO-2 ADR-069 (rest in HERO-7, MAG-9, MAG-10,
  CONT-5); HERO-1, HERO-3, HERO-6, HERO-7 and HERO-4 ADR-072, HERO-4's war machines ADR-073.
- **Combat**: grid and sizes ADR-005/021/070, battlefield ADR-033, turn order ADR-009, damage ADR-033, cover ADR-035, tuning ADR-038, speeds ADR-037,
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070, CMB-7/8 ADR-071, CMB-6 check order ADR-072.
- **Magic**: MAG-4 ADR-070/071; MAG-2, MAG-3 (mana refill, Spell Power role, potency), MAG-5 (levels), MAG-6 (resistance) ADR-071; MAG-1 (Spell Power formula), MAG-3, MAG-5, MAG-6 … MAG-12 ADR-072; MAG-5 (fixed 500 points) and MAG-10 (even stat odds) ADR-073.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070; RMG-8, RMG-12, RMG-13 ADR-071; RMG-11 ADR-070/072.
- **Data**: DATA-1 ADR-071.
- **Game flow**: victory ADR-057; FLOW-1 (conditions, deadlines, teams) and FLOW-2 (week and month events) ADR-073.
- **Assumptions** of ADR-070 … ADR-072: confirmed in ADR-073.
