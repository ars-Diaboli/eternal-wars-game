# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-10-29** (ADR-090): CMB-10 (auto combat, built) and UI-1 (speed settings) answered. Before that (2026-10-28, ADR-088), new
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

Nothing blocks the current work (the Phase 4 fairness check and the Phase 3 play-test). The ADR-088 questions stop only their own
roadmap items; MP-1 (a) decides where multiplayer goes in the plan.

## Heroes

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names and specialties in `heroes.csv`; primary stat odds and starting values (ADR-055).

## Combat

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

## New ideas (ADR-088, 2026-10-28)

<a id="flow-3"></a>
**FLOW-3 Turn timer.** On the new-game screen, 1 to 30 minutes, human players only (ADR-088). (a) **A turn's time**: the same every turn, or a
bank that carries unused time over (a chess clock)? (b) **A battle between two humans** (hot-seat, LAN): whose time runs, the attacker's
only, or each player's while it is their stack's turn? (c) **When it runs out** with a question open (level-up, chest, guards that offer to
join): the computer player's choice is taken? And a hero in the middle of a move stops after the step it is taking? (d) A **warning** at
1 minute and 10 seconds left? (e) **Saves**: a loaded game starts the current turn with a full timer? (f) Is the timer a game option saved with
the game (so replays end turns where the timer did), or does each machine run its own? (g) Steps of 1 minute, or also 30 seconds?
*Proposal*: (a) the same every turn; (b) the attacker's time runs (the defender plays inside the attacker's turn); (c) yes to both; (d) yes;
(e) yes; (f) a game option in the save; the timer's end of turn is an ordinary `EndTurnCommand` in the log, so replays need no clock; (g) 1
minute.

<a id="mp-1"></a>
**MP-1 LAN multiplayer: scope and model.** (ADR-088) (a) **When**: before 1.0 (after the Phase 3/4 exits, before the campaign), or after 1.0?
(b) **Model**: clients send commands to the host; the host checks and runs them with `session.Execute` and sends every accepted command to all
clients, which run it too (the core is deterministic) and compare `GameStateDigest` now and then. Or the host sends state? (c) **Finding a game**:
LAN broadcast discovery plus typing an address? (d) **Lobby**: the host picks the map or the template and its options (ADR-086). Each client picks
a free colour; the host may set the other colours to computer. (e) **A battle between two remote humans**: each controls its own side live?
(f) **A player who drops out**: the game pauses for everyone, and the host may let the computer take over or wait for a rejoin? (g) **Fog and
cheating**: every client has the whole state (a modified client could see through the fog). Fine on a LAN? (h) **Saves**: only the host saves;
the save records which colours were human. Loaded in hot-seat, the player chooses which colours are human. (i) **Chat**: in 1.0 or not?
*Proposal*: (a) after 1.0 unless you want it sooner. It needs a stable game first, and hot-seat covers playing together until then; (b) commands
from the host, digest check every turn; (c) yes; (d) yes; (e) yes; (f) pause, then the host chooses; (g) fine on a LAN; (h) yes; (i) a simple
text chat.

<a id="mp-2"></a>
**MP-2 Simultaneous first week.** (ADR-088) (a) **Start zone** on a hand-made map: maps have no zones. The tiles within some distance of the
player's start towns, zones painted in the editor, or random maps only? (b) **Conflicts** that end it: entering another player's start zone,
touching an object another player touched this day, seeing another player's hero? (c) **"Played again in the normal order"**: discard the whole
day for everyone and replay it turn by turn (the players lose what they did that day), or keep everything done before the conflict and finish
the day turn by turn from there (proposal)? (d) Is it a lobby option (on or off), and how long: always the first week, or settable (1 to 2
weeks)? (e) The **turn timer** in simultaneous days: one timer for all, the day ending when everyone ended their turn or the time is up? (f)
Hot-seat cannot do it (one screen): multiplayer only?
*Proposal*: (a) random maps: the template's start zone; hand-made maps: tiles within 10 of a start town until the editor can paint zones; (b)
entering another start zone or touching an object another player touched that day; (c) keep what was done and finish the day in turn order,
with the player who caused the conflict last; (d) a lobby option, the first week, on by default; (e) one timer, the day ends when all have
ended their turn; (f) multiplayer only.

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
  ADR-066, TOWN-2 ADR-067/068/070, TOWN-3 ADR-070, TOWN-6 ADR-070, TOWN-1 growth buildings and TOWN-4 ADR-071, TOWN-7 and TOWN-8 ADR-072, TOWN-5 and base-creature map dwellings ADR-073, TOWN-5 reach ADR-075.
- **Heroes**: roster ADR-048, skills ADR-048/069, stats ADR-055, schools ADR-063/069, artifacts ADR-061, HERO-2 ADR-069 (rest in HERO-7, MAG-9, MAG-10,
  CONT-5); HERO-1, HERO-3, HERO-6, HERO-7 and HERO-4 ADR-072, HERO-4's war machines ADR-073.
- **Combat**: grid and sizes ADR-005/021/070, battlefield ADR-033, turn order ADR-009, damage ADR-033, cover ADR-035, tuning ADR-038, speeds ADR-037,
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070, CMB-7/8 ADR-071, CMB-6 check order ADR-072, CMB-2 ADR-075, CMB-9 (movement as a circle) ADR-089, CMB-10 (auto combat) ADR-090.
- **Magic**: MAG-4 ADR-070/071; MAG-2, MAG-3 (mana refill, Spell Power role, potency), MAG-5 (levels), MAG-6 (resistance) ADR-071; MAG-1 (Spell Power formula), MAG-3, MAG-5, MAG-6 … MAG-12 ADR-072; MAG-5 (fixed 500 points) and MAG-10 (even stat odds) ADR-073.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070; RMG-8, RMG-12, RMG-13 ADR-071; RMG-11 ADR-070/072.
- **Data**: DATA-1 ADR-071.
- **Presentation**: UI-1 (speed settings) ADR-090.
- **New ideas** (ADR-077): TOWN-9 … TOWN-11, MAP-25, MAP-26, MAP-28 ADR-079; MAP-27, MAP-29 … MAP-34 ADR-080 (MAP-32 built after the week
  events).
- **Game flow**: victory ADR-057; FLOW-1 (conditions, deadlines, teams) and FLOW-2 (week and month events) ADR-073; FLOW-2 event start and town-less players ADR-075.
- **Assumptions** of ADR-070 … ADR-072: confirmed in ADR-073; of ADR-073: confirmed in ADR-075.
