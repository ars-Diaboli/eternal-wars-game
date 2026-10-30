# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-10-30** (ADR-107, ADR-108): HERO-8 (rarer specialties) answered: the proposals, for now; CONT-7 (assembled relics)
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

Nothing blocks the current work (the Phase 4 fairness check and the Phase 3 play-test). EVT-1 … EVT-6 (ADR-096) stop only the map events; EVT-1 (b) decides where the map events go in the plan.

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

## New ideas (ADR-096, 2026-10-29)

<a id="evt-1"></a>
**EVT-1 Map events: scope, form and place in the plan.** (ADR-096) The author's events in the editor, like HotA's extended event system.
(a) **Scope**: the whole HotA system (four event types, variables, loops, running another event, quest stages), or a first set (hero,
player and town events with conditions, variables and the main actions), quests next, loops and "run another event" last?
(b) **When**: before 1.0, as content after the Phase 3 and 4 exits (single maps gain from it too), or with the campaign (Phase 6)?
(c) **Name**: "map events" in the docs and an "Events" tab in the editor (an "event" is already the code's pub/sub record and the weekly
event)?
(d) **Form**: a tree of actions and conditions stored as data in the map file and edited as a tree (HotA), or a small script language the
author types?
(e) **HoMM3's simple events**: HoMM3 has plain timed events (a day, a repeat, resources, which players) and plain event cells (a message,
rewards, guards, which players, "computer may trigger", "once"). Should the editor offer these as ready forms that write the same tree?
(f) **Campaign variables** ("kept for the next map", "taken from the last map"): recorded now and built with the campaign (FLOW-1)?
*Proposal*:
(a) all four types, in three steps: hero, player and town events with conditions, variables, messages, questions and the main actions;
then the quest objects and the quest log; then loops and "run another event";
(b) the Pandora's Box with its own list now (Phase 3 objects); the map events as one Phase 5 item, after the Phase 3 and 4 exits and
before the campaign, which builds on them;
(c) yes;
(d) a data tree (no script language: it stays deterministic, safe to load and easy to show in the editor);
(e) yes, "simple event" forms for timed events, event cells and town events;
(f) yes.

<a id="evt-2"></a>
**EVT-2 Triggers and event cells.**
(a) **Event cell**: invisible to the players, as in HoMM3, and ignored by route planning (it must not give itself away)? It runs when a hero
steps onto it; does the hero's route stop there always, or only when the event shows a message, a question or a battle?
(b) **Repeat**: an event cell runs on every step until the event disables or removes itself (HotA), with "once" as a ready form?
(c) **Who triggers**: only through conditions ("player is human", "current player"), as HotA, or also HoMM3's tick boxes (players, "computer
may trigger") on the cell?
(d) **Turn start**: player events and town events run after income, growth and the week's event, before the player gets control. Neutral
towns: at the start of each day, before the first player's turn?
(e) **Order**: several events at the same moment run in the editor's list order?
(f) **The screen**: a turn-start message of a human player shows when the turn begins; the camera moves to the town or hero it concerns?
*Proposal*: (a) invisible and ignored by routes; the route stops only when the event shows something or fights; (b) yes; (c) conditions,
with the tick boxes in the ready forms (they write the conditions); (d) yes, neutral towns at the start of the day; (e) yes; (f) yes.

<a id="evt-3"></a>
**EVT-3 Conditions, expressions and variables.**
(a) **HotA's list** (ADR-096) mapped to ours: the difficulty is the complexity (Beginner … Expert); "starting town" is the faction; secondary
skills have four levels with Master. Anything to drop?
(b) **More conditions** we have and HotA does not: the player's team, an ally, the week's event, the day of the week, the hero on the
surface or underground, the hero's attunement, mana?
(c) **Variables**: whole numbers, map-wide, with a starting value (HotA). Also per player?
(d) **Random numbers**: drawn from the game's random source, so a loaded game draws the same (HotA too)?
(e) **Records**: "monster defeated by player" and "hero defeated by player" need the game to remember who beat which map monster and hero.
Kept for every monster and hero, or only for those an event names?
*Proposal*: (a) take the whole list; (b) team, ally and the day of the week now, the rest on request; (c) map-wide only; (d) yes; (e) only for
those an event names.

<a id="evt-4"></a>
**EVT-4 Actions.**
(a) **HotA's list** (ADR-096) with our rules: secondary skills up to Master and only into a free slot; war machines are ours (ADR-056); a
building ignores bans and prerequisites and adds the prerequisites (HotA); spell book and spells wait for Phase 5. Anything to drop?
(b) **Losses**: HotA cannot lower experience, primary or secondary skills, but can take resources, creatures, artifacts, mana and movement.
The same?
(c) **More actions**: give or take a hero (like a prison), change a town's or mine's owner, reveal an area, move a hero, change a condition?
(d) **Battle**: the hero attacks the author's army on the cell's terrain. Won: experience and the event goes on; lost: the hero is lost and the
event stops (HotA). Retreat and surrender as in any battle, and the event stops?
(e) **A full army**: creatures given to a hero with no free slot: the hero chooses what to leave (a pending decision, HoMM3), or the
creatures that do not fit are lost?
(f) **Messages**: text only for now (no art), with lines for the rewards; images once there are icons?
*Proposal*: (a) the whole list; (b) the same; (c) reveal an area and give a hero now, the rest later; (d) yes; (e) the hero chooses; (f) yes.

<a id="evt-5"></a>
**EVT-5 Quests: Seer's Hut, Quest Guard, Quest Gate, quest log.**
(a) **Objects**: the three as in HotA? The Quest Gate is like the border gate (ADR-071) with a quest in place of a key.
(b) **HoMM3's quest forms** (reach a level, primary skills, defeat a hero, defeat a monster, bring artifacts, creatures or resources, be a
given hero, be a given player, a deadline day): ready forms that write the quest stages?
(c) **Items taken**: a completed HotA stage does not take what it asked for; the ready forms take it (bring resources: paid on completion)?
(d) **Quest log**: a screen per player (a key and a button) with each active quest's hint and object; kept in the game state?
(e) **Gates and routes**: a hero that meets a gate's quest passes without stopping (HotA's "free passage"); route planning treats a gate as
open only for a hero that meets it now?
(f) **Random maps**: the generator places Seer's Huts (bring an artifact or creatures for a reward) or none for now?
*Proposal*: (a) yes; (b) yes; (c) yes; (d) yes, Q opens it; (e) yes; (f) none for now.

<a id="evt-6"></a>
**EVT-6 The AI and the editor.**
(a) **AI**: as HotA: it answers questions by value (random numbers as their average), does Seer's Hut quests when the reward outweighs the
cost, takes Quest Guards and Gates as obstacles until it meets them, and does not look ahead at event cells, player or town events?
(b) **Editor**: an "Events" tab with the events by type, the tree of actions (add, copy, move, remove, undo through `MapEditorSession`),
the variables, and one event shared by several objects; an object's inspector picks or creates its event?
(c) **Validator**: unknown names, an event that runs itself (directly or through others), events and variables nobody uses (warnings), a
quest nobody can finish (warning where it can tell)?
(d) **Trying an event**: a "test" button in the editor that runs an event on a chosen hero, or test by playing the map?
*Proposal*: (a) yes; (b) yes; (c) yes; (d) by playing the map for now.

## Numbers to tune (not questions)
Placeholders that wait for play-testing: caravan cost (ADR-054); complexity starting resources (ADR-063); Stewardship and governor percentages
(ADR-063); siege Ballistics (ADR-063); zone guard and garrison powers, `GuardPicker` ranges (ADR-064); building costs; the growth buildings (+5 / +2, ADR-071); war machine stats and prices
(CMB-3); morale 2 % and luck 4 % per point (ADR-053); deposit prices and the map marketplace threshold (ADR-070); the AI's retreat threshold.

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
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070, CMB-7/8 ADR-071, CMB-6 check order ADR-072, CMB-2 ADR-075, CMB-9 (movement as a circle) ADR-089, CMB-10 (auto combat) ADR-090.
- **Magic**: MAG-13 (guild spell draw, spell elements, summons) ADR-106; MAG-1 (the spell list re-drafted) ADR-099; MAG-4 ADR-070/071; MAG-2, MAG-3 (mana refill, Spell Power role, potency), MAG-5 (levels), MAG-6 (resistance) ADR-071; MAG-1 (Spell Power formula), MAG-3, MAG-5, MAG-6 … MAG-12 ADR-072; MAG-5 (fixed 500 points) and MAG-10 (even stat odds) ADR-073.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070; RMG-8, RMG-12, RMG-13 ADR-071; RMG-11 ADR-070/072.
- **Data**: DATA-1 ADR-071.
- **Presentation**: UI-1 (speed settings) ADR-090, built ADR-095; UI-2 (replays, main menu) ADR-101.
- **LAN multiplayer**: MP-1 (before 1.0, host, clients that see only their view, lobby, saves) and MP-2 (simultaneous first week) ADR-098;
  MP-3 (clients' view, saves, smooth fog) and MP-4 (start zones) ADR-099.
- **Content**: CONT-3 (artifacts re-drafted) ADR-099; CONT-6 (HoMM names, assembled relics) ADR-100; CONT-7 (assembled relic rules, six more relics) ADR-108.
- **New ideas** (ADR-077): TOWN-9 … TOWN-11, MAP-25, MAP-26, MAP-28 ADR-079; MAP-27, MAP-29 … MAP-34 ADR-080 (MAP-32 built after the week
  events).
- **Game flow**: victory ADR-057; FLOW-1 (conditions, deadlines, teams) and FLOW-2 (week and month events) ADR-073, the conditions built ADR-094; FLOW-2 event start and town-less players ADR-075; FLOW-3 (turn timer) ADR-091; FLOW-4 (victory screen, ranking) ADR-101.
- **Arena**: ARN-1 and ARN-2 (before 1.0, with LAN, three shipped maps) ADR-100.
- **Assumptions** of ADR-070 … ADR-072: confirmed in ADR-073; of ADR-073: confirmed in ADR-075.
