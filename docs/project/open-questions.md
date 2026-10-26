# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-10-26** (ADR-077): new ideas (scenery, rivers, flaggable providers, caravans, Thieves' Guild, the third object catalogue) bring
MAP-25 … MAP-34 and TOWN-9 … TOWN-11. Fifth round (ADR-075): the ADR-073 assumptions confirmed (TOWN-5's reach and FLOW-2's first week
changed); CMB-2 answered; town-less campaign players.
Fourth round (ADR-073): the ADR-070 … ADR-072 assumptions confirmed (MAG-5 and MAG-10 changed); HERO-4, TOWN-5,
FLOW-2 answered; FLOW-1 except the campaign hooks.
Third round (ADR-072): HERO-1, HERO-3, HERO-6, HERO-7, TOWN-7, TOWN-8, RMG-11, CMB-6, MAG-3, MAG-5 … MAG-12 answered,
MAP-23 corrected; MAG-1 partly (the Spell Power formula); HERO-4 answered except where the war machines stand.
Second round (ADR-071): DATA-1, RMG-8, RMG-12, RMG-13, MAP-20, TOWN-1, TOWN-4, CMB-7, CMB-8, MAG-2, MAG-4 answered. First round (ADR-070): MAP-15 …
MAP-19, MAP-21 … MAP-24, TOWN-2, TOWN-3, TOWN-6, CMB-1, CMB-3, CMB-4, RMG-9, RMG-10 answered.

## 🚧 Blocking now

Nothing blocks the current work (the skill rework, ADR-069 / ADR-072). The ADR-077 items wait for the questions below; each has a proposal, so
"agreed" on a question is enough to start it.

## Heroes

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names and specialties in `heroes.csv`; primary stat odds and starting values (ADR-055).

## Towns and caravans

<a id="town-9"></a>
**TOWN-9 Flaggable providers and resource caravans** (ADR-077).
a) A flagged provider produces its week's output **for its owner**; the output waits at the provider. Does it pile up over several weeks, or
does one week's output wait at most?
b) Does a visiting hero still collect it (the owner's hero; an enemy hero flags it and collects)?
c) Which providers: Water Wheel, Windmill, the Warehouses, and also the Mystical Garden?
d) The Warehouses' choice (8 lumber *or* 500 gold) for a caravan: chosen when ordering?
e) Does a resource caravan cost anything, and does it count towards TOWN-10's limit?
**Proposal**: a) one week's output waits, it does not pile up (no hoarding without visiting or a caravan); b) yes, any visit by the owner's
hero collects, an enemy hero flags it and takes what waits; c) all four, the Mystical Garden too; d) chosen when ordering; e) a flat 100 gold
per caravan (placeholder) and it counts towards the limit. Reach and route as for creature caravans (7500 points, open route).

<a id="town-10"></a>
**TOWN-10 Caravan speed and limit** (ADR-077).
a) 2500 points a day at the plain step cost (100, diagonal 141, roads no faster): with the 7500-point reach, a caravan arrives in **at most 3
days**. Keep the 7500 reach (so 3 days), or raise it to 12 500 to keep "5 days"?
b) The limit of 6 (8 with a governor): does it count caravans on their way *and* those waiting at a full garrison? Do troops sent from another
own town count towards the destination's limit?
c) "Further orders queue": refused at the limit, or ordered and held until a place frees (paid now, leaving later)?
**Proposal**: a) keep 7500 (3 days at most); b) both count, troops sent between own towns count at the destination; c) refused at the limit
with a message (simplest; the queue screen shows what is coming).

<a id="town-11"></a>
**TOWN-11 Thieves' Guild and the Den of Thieves** (ADR-077).
a) HoMM3 shows more rows the more taverns the player has. Same here, or the full table from one tavern?
b) The Den of Thieves (map object): the full table while the hero stands there, or as if the player had one more tavern for the week?
**Proposal**: a) HoMM3-like: 1 tavern towns and heroes; 2 also gold, lumber and ore, rare resources; 3 also artifacts and income; 4 or more
also army strength, best hero and best creature (computer and teammate players included); b) the full table on every visit, nothing kept.

## Combat

<a id="cmb-5"></a>
**CMB-5 Sieges** (post-1.0). Confirm sieges come after 1.0; until then the siege Ballistics rules (ADR-063) stay unused.

## Magic (Phase 5)

<a id="mag-1"></a>
**MAG-1 Spell list.** The spells and numbers per school ([spells.md](../content/spells.md)); spell tags beyond `all`, `mind`, `fire`, `curse`,
`holy`; which spells creature casters get. The Spell Power formula is set for now (ADR-072: damage and duration × (1 + SP / 10)); the
spells.md tables still use "a + b·SP" and are rewritten with the list.

## Adventure map

<a id="map-25"></a>
**MAP-25 Scenery** (ADR-077). Static objects: rocks, trees, dead trees, ponds, bushes, mushrooms, flowers, fissures, mounds, ruined buildings,
creature bones, castle walls.
a) Do all of them block, or are the small ones (flowers, mushrooms, small bushes) passable decoration?
b) Sizes: one footprint per kind, or several sizes per kind (1×1 to 4×4, walls as straight pieces)?
c) Terrain themes: which kinds the generator uses on which terrain?
**Proposal**: a) flowers, mushrooms and small bushes are passable decoration (no effect); the rest block; b) several sizes per kind, each a
fixed footprint like other objects; castle walls as 1×N straight pieces; c) a terrain list per kind in a new `scenery.csv` (trees on grass
and forest-like terrains, dead trees and bones on wasteland, …); the generator replaces today's rock clumps with them.

<a id="map-26"></a>
**MAP-26 Rivers** (ADR-077). In HoMM3 rivers are drawn on the terrain like roads and change nothing. Same here, or do they block and need
bridges or fords?
**Proposal**: HoMM3: a tile overlay like roads (four kinds for looks), passable, no movement effect; the editor paints them, the generator
draws some between zones' lakes and borders. No map format change beyond the overlay layer (one version bump).

<a id="map-27"></a>
**MAP-27 Sphinx.** A riddle: who writes the riddles (a list in data, map author's own), how is the answer typed (free text, or one of three
choices), what is the reward and the punishment?
**Proposal**: one of three choices (controller-friendly, no spelling problems); riddles in `riddles.csv` plus an optional author riddle per
Sphinx; one try per hero; right: a Major artifact or 5000 experience (rolled); wrong: the hero loses its remaining movement points for the day.

<a id="map-28"></a>
**MAP-28 Sanctuary.** A hero standing in it cannot be attacked. How long may it stay, and may it leave its army there?
**Proposal**: as long as it likes, one hero at a time; enemies cannot enter while it is occupied; nothing else (no healing, no army storage).

<a id="map-29"></a>
**MAP-29 Sacrificial Altar.** Which factions sacrifice creatures and which artifacts, and how much experience?
**Proposal**: Darkness heroes (Necropolis, Inferno, Dungeon) sacrifice creatures: experience = the creatures' hit points × 2; Light heroes
(Haven, Sylvan, Academy) sacrifice artifacts: Minor 2000, Major 5000, Relic 10 000; Wild heroes (Stronghold, Swamp Bestiary) may do either.
Any number of times.

<a id="map-30"></a>
**MAP-30 Refugee Camp.** HoMM3: a random creature each week, any town. Which tiers ("high level")?
**Proposal**: each week a random base creature of tier 3–5 from the playable factions, its weekly growth available (like a dwelling, not
flaggable, not accumulating); bought by a visiting hero at the normal price.

<a id="map-31"></a>
**MAP-31 Cartographer.** There is no sea (ADR-051): one kind for the surface and one for the underground?
**Proposal**: two kinds, surface and underground; 1000 gold reveals that whole level for the visiting player (HoMM3's price).

<a id="map-32"></a>
**MAP-32 Astrologer's Tower.** Needs the week and month events (FLOW-2). What does "change it for a reward" mean?
**Proposal**: a visit shows next week's event; for 2000 gold the player may re-roll it once (one re-roll per week for the whole map, the first
payer decides). Built after the events.

<a id="map-33"></a>
**MAP-33 Artifact Merchant.** HoMM3's Black Market: what does it sell, and at what price?
**Proposal**: 7 artifacts rolled at game start (Minor and Major, no Relics), refreshed on the first day of every month; price = 2 × the
artifact's value; a sold slot stays empty until the refresh.

<a id="map-34"></a>
**MAP-34 Witch Hut.** Owner's note: learn a new skill or improve a known one; one extra upgrade per hero level. Which skill, and what limits?
**Proposal**: each hut has one skill of the general groups (set in the editor or rolled at start; never a school or faction skill); a visit
raises it by one level (learns it at Basic if the hero has a free group); a hero may take one Witch Hut upgrade per hero level (the hut
remembers nothing; the hero remembers the level it last used one at).

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
  ADR-066, TOWN-2 ADR-067/068/070, TOWN-3 ADR-070, TOWN-6 ADR-070, TOWN-1 growth buildings and TOWN-4 ADR-071, TOWN-7 and TOWN-8 ADR-072, TOWN-5 and base-creature map dwellings ADR-073, TOWN-5 reach ADR-075.
- **Heroes**: roster ADR-048, skills ADR-048/069, stats ADR-055, schools ADR-063/069, artifacts ADR-061, HERO-2 ADR-069 (rest in HERO-7, MAG-9, MAG-10,
  CONT-5); HERO-1, HERO-3, HERO-6, HERO-7 and HERO-4 ADR-072, HERO-4's war machines ADR-073.
- **Combat**: grid and sizes ADR-005/021/070, battlefield ADR-033, turn order ADR-009, damage ADR-033, cover ADR-035, tuning ADR-038, speeds ADR-037,
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070, CMB-7/8 ADR-071, CMB-6 check order ADR-072, CMB-2 ADR-075.
- **Magic**: MAG-4 ADR-070/071; MAG-2, MAG-3 (mana refill, Spell Power role, potency), MAG-5 (levels), MAG-6 (resistance) ADR-071; MAG-1 (Spell Power formula), MAG-3, MAG-5, MAG-6 … MAG-12 ADR-072; MAG-5 (fixed 500 points) and MAG-10 (even stat odds) ADR-073.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070; RMG-8, RMG-12, RMG-13 ADR-071; RMG-11 ADR-070/072.
- **Data**: DATA-1 ADR-071.
- **New ideas** (ADR-077, 2026-10-26): open as MAP-25 … MAP-34 and TOWN-9 … TOWN-11 above.
- **Game flow**: victory ADR-057; FLOW-1 (conditions, deadlines, teams) and FLOW-2 (week and month events) ADR-073; FLOW-2 event start and town-less players ADR-075.
- **Assumptions** of ADR-070 … ADR-072: confirmed in ADR-073; of ADR-073: confirmed in ADR-075.
