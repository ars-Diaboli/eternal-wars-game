# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-10-22** (ADR-070): MAP-15 … MAP-19, MAP-21 … MAP-24, TOWN-2, TOWN-3, TOWN-6, CMB-1, CMB-3, CMB-4, RMG-9, RMG-10 answered; HERO-2 folded into
HERO-7; HERO-7, CMB-2, RMG-11, MAG-4 and TOWN-4 split into single-topic questions.

## 🚧 Blocking now

| Id | Question | Blocks |
|---|---|---|
| [HERO-6](#hero-6) | Old skill ids and the roster after the rework | Phase 3: skill rework |
| [HERO-7](#hero-7) | Primal Fury for warriors | Phase 3: Swamp Bestiary's group, roster starting skills |
| [MAG-8](#mag-8) | Mana by level | Phase 3: `ManaRules` |
| [MAG-7](#mag-7) | Necromancy details | Necromancy, the Necromancy Amplifier |
| [HERO-4](#hero-4) | The deployment step of Tactics | Phase 3: deployment |
| [HERO-1](#hero-1) | Skill specialties | Phase 3: specialties |
| [TOWN-4](#town-4) | The map tavern's hero pool | Second catalogue: map tavern |
| [MAP-20](#map-20) | Keymasters and border guards | Second catalogue; template border guards |
| [DATA-1](#data-1) | Where the data tables live | Per-town buildings table, the creature size pass |

## To confirm (assumed in ADR-070)

Short answers the agent read one way. Say "no" to any of them; otherwise they stand.

| Id | Assumption |
|---|---|
| RMG-9 | The template picks the mirror (axis: vertical, horizontal, diagonal or random; point for 4 and 8 players). |
| RMG-10 | User templates are saved in the user data folder and scanned next to the shipped ones. |
| MAP-16 | A Window of the Magi works on every visit. |
| MAP-23 | The Fountain of Magic works once a day per hero; both magic objects are built with the second catalogue (mana exists already). |
| TOWN-3 | Capturing a town with a Capitol downgrades it to town hall 3 only when the capturer already owns a Capitol. |
| TOWN-6 | The amplifier's id is `necromancy-amplifier`. |
| TOWN-3 | Town hall 3 is called "Capitol" in `buildings.csv` today; it is renamed (proposal: "City Hall") so the Capitol is level 4. |
| TOWN-4 | The Brotherhood's 4 offers become 2 native + 2 adjacent (today 2 native, 1 adjacent, 1 of the rest). |
| MAP-18 | "Two ends per portal type" covers two-way portals; one-way portals keep several entrances sharing one exit (ADR-026). |
| CMB-1 | The AI retreats when its surviving army is worth less than 20 % of the enemy's (tuning). |

## Data and tooling

<a id="data-1"></a>
**DATA-1 🚧 Where the data tables live.** Today each table exists once in git (`src/Godot/EternalWars.Godot.Assets/data/*.csv`); core embeds it, and
the Godot apps also read it from `res://shared/data/` (a junction made by `tools/link-shared-assets.ps1`, so it *looks* like a copy per app) so it can be
edited without a rebuild (ADR-044).
- a. Move the tables to a neutral folder owned by core (`data/` at the repo root, embedded in `EternalWars.Core.Domain`) instead of a Godot project?
- b. Do the apps read only the embedded copy, with `--creatures=`-style flags for designers' edited files, and drop the `res://` copy?
- c. Format: keep CSV for flat tables (spreadsheet-friendly, diffable), JSON only for nested data (maps, templates)?

Proposal: yes to a, b and c; a schema test per table checks ids, references and columns.

## Random maps (Phase 4)

<a id="rmg-8"></a>
**RMG-8 Fairness metrics** (blocks fairness checks in `sim maps`). Which numbers compare start zones (distance to the nearest mines, treasure value,
guard power on the way), what tolerance, and what happens on failure (retry or report)? Proposal: the three numbers, ±15 %, retry with the next
sub-seed, then report.

<a id="rmg-11"></a>
**RMG-11 Town groups in templates.** ADR-028 says a zone may filter towns by group; the format lists town types. Proposal: post-1.0.

<a id="rmg-12"></a>
**RMG-12 Zone richness.** Anything beyond today's mine minimums and treasure bands? Proposal: drop the word; bands and mines are enough.

<a id="rmg-13"></a>
**RMG-13 Optional connections.** Connections present only with some chance per map? Proposal: yes, a per-connection probability, 1.0 scope.

## Adventure map objects

<a id="map-20"></a>
**MAP-20 🚧 Keymasters, border guards and border gates.**
- a. How many colours (HoMM3: 8; the Eyes of the Magi use 4)?
- b. Is the key the player's (HoMM3) or the hero's?
- c. Does a border guard vanish after the first key holder passes, while a border gate stays and lets key holders through?
- d. May templates use them in connections?

Proposal: 8 colours, the player's key, HoMM3 behaviour, template option.

## Towns and economy

<a id="town-1"></a>
**TOWN-1 Growth beyond the fort.** Which other buildings raise growth, if any? Proposal: none for 1.0.

<a id="town-4"></a>
**TOWN-4 🚧 The map tavern's pool.** One answer says the map tavern uses the player's weekly pool, another that it has its own pool of random heroes.
Which one? Proposal: its own weekly pool of 4 random heroes, any faction, shared by all players.

<a id="town-5"></a>
**TOWN-5 Caravan details.** Is the 5-day limit checked only when ordering (today only the route is re-checked daily)? How long may a caravan wait at a
full garrison? Proposal: only when ordering; it waits until there is room.

<a id="town-7"></a>
**TOWN-7 The basic tavern's offers.** With the Brotherhood a town offers 2 native + 2 adjacent heroes. What does the basic tavern (2 offers) show?
Proposal: 1 native + 1 adjacent.

<a id="town-8"></a>
**TOWN-8 Who counts towards the hero limit.** Heroes in a town garrison? Heroes in prisons? Proposal: garrison heroes count, prisoners do not.

## Heroes

<a id="hero-1"></a>
**HERO-1 🚧 Skill specialties.** What does a skill specialty add? Proposal: +5 % of the skill's effect per hero level, like HoMM3.

<a id="hero-3"></a>
**HERO-3 Rival schools.** Level-ups use the sworn-enemy pairs from the lore, the tavern uses the ring. Keep both, or one relation for both?

<a id="hero-4"></a>
**HERO-4 🚧 Deployment** (Tactics, ADR-069: zone 4 / 5 / 6 cells deep).
- a. What the depth measures. Automatic placement already fills 6–10 columns. Proposal: extra depth forward of the normal zone.
- b. Does the enemy's Tactics cancel it, as for the width? Proposal: the side with the higher Tactics deploys; on a tie nobody does.
- c. Controls: drag stacks in the battle scene, then "Start battle". The AI keeps automatic placement. Agreed?
- d. Also against neutrals and banks? Proposal: yes.

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names and specialties in `heroes.csv`; primary stat odds and starting values (ADR-055).

<a id="hero-6"></a>
**HERO-6 🚧 Old skill ids and the roster.**
- a. Maps and saves read old ids as their successors (Deployment → Tactics, Troop management → Scouting, `adrenaline.*` → `rage.*`), no map format bump;
  a level above the new maximum drops to it. Agreed?
- b. Replays recorded before the change may diverge (the numbers change). Accept?
- c. Which starting skill replaces Troop management in `heroes.csv`? Proposal: Scouting.

<a id="hero-7"></a>
**HERO-7 🚧 Primal Fury and warriors** (Stronghold, Swamp Bestiary).
- a. Are these towns' warriors offered Primal Fury at all? Proposal: no, mages only.
- b. Must every warrior of these towns start with an Offense skill (today 3 of 8 do)? Proposal: yes, these two towns only.

## Combat

<a id="cmb-2"></a>
**CMB-2 Morale and luck sources.** Which skills, buildings and army mixes add or remove them (artifacts and Leadership exist)? Proposal: HoMM3's
mixed-factions penalty (−1 per extra faction) and a tavern +1 morale for defenders.

<a id="cmb-5"></a>
**CMB-5 Sieges** (post-1.0). Confirm sieges come after 1.0; until then the siege Ballistics rules (ADR-063) stay unused.

<a id="cmb-6"></a>
**CMB-6 Marksmanship and cover.** Marksmanship subtracts −2/8 / −4/8 / −6/8 from the ranged penalty (ADR-070). Is the >7/8 "no shot" check made before
the reduction (so Marksmanship never shoots through a wall) or after? Proposal: before.

<a id="cmb-7"></a>
**CMB-7 Creatures without morale.** Do undead and constructs ignore morale? Proposal: yes (tags `undead`, `construct`).

<a id="cmb-8"></a>
**CMB-8 War machines and luck.** Machines have no morale but roll the hero's luck. Intended? Proposal: yes.

## Magic (Phase 5)

<a id="mag-1"></a>
**MAG-1 Spell list.** The spells and numbers per school ([spells.md](../content/spells.md)); spell tags beyond `all`, `mind`, `fire`, `curse`,
`holy`; which spells creature casters get.

<a id="mag-2"></a>
**MAG-2 Spell book and learning.**
- a. Does every hero start with a spell book, or buy one?
- b. Learning: by visiting a guild with the Magic level the spell needs?
- c. A guild's offer: how many spells per level, only its own school?
- d. What do Stronghold and Swamp Bestiary guilds offer, having no school?

<a id="mag-3"></a>
**MAG-3 Spell Power and mana.** What Spell Power does in numbers; mastery ranks; is attunement "potency" (ADR-047) dropped? Does mana refill in a town?

<a id="mag-4"></a>
**MAG-4 Does casting use the unit's turn?** The hero casts once a round during a controlled unit's turn (ADR-070). Does that unit still act afterwards?
Proposal: yes (HoMM3).

<a id="mag-5"></a>
**MAG-5 Adventure spells.** Which Magic level unlocks them (Town Portal and Dimension Door are level 3 in spells.md); movement cost; fog, AI, replays.

<a id="mag-6"></a>
**MAG-6 Spell resistance and specialties.** The Spell resistance skill (−10 / 15 / 20 %): damage, duration, or both? How durations round. How strong a
spell specialty is.

<a id="mag-7"></a>
**MAG-7 🚧 Necromancy.**
- a. Value: the creatures' gold price? Proposal: yes.
- b. Who: any hero with Death attunement? Proposal: yes.
- c. What is raised: the base creature of the tier; two lines in a tier split in a random proportion at every level; upgraded if the hero has the
  upgraded stack. Agreed?
- d. Count: sum the value per undead tier, divide by the price, round down. Agreed?
- e. Who cannot be raised: undead, constructs, elementals, war machines, the hero's own losses (by tags). Agreed?
- f. Army room: same creature's stack or a free slot, else lost. Agreed?
- g. Which battles: every victory, not a retreat; automatic, shown on the result screen. Agreed?

<a id="mag-8"></a>
**MAG-8 🚧 Mana by level.** "+10 × ⌈level / 10⌉": the maximum only, or the daily regeneration too? Every hero, warriors included, gets at least 10.
Proposal: maximum only; intended.

<a id="mag-9"></a>
**MAG-9 Interference numbers.** Expert "−6, +2": is the extra cost +2 at Expert or +3? Per spell (+N mana), not a percent? Proposal: +1 / +2 / +3 mana.

<a id="mag-10"></a>
**MAG-10 Spell Power and Knowledge for Primal Fury mages.** They have no spells. Proposal: Spell Power raises Blood Rage and Warcry strength;
Knowledge does nothing; their level-up odds favour Attack and Defense instead.

<a id="mag-11"></a>
**MAG-11 Area spells and big creatures.** An area spell covers cells (say 3×3 around the target cell). A 3×3 or 4×4 creature is hit when any of its
cells is inside (like breath today), and only once. Should area sizes grow with the target's footprint? Proposal: no; any overlap hits once.

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
(ADR-063); siege Ballistics (ADR-063); zone guard and garrison powers, `GuardPicker` ranges (ADR-064); building costs; war machine stats and prices
(CMB-3); morale 2 % and luck 4 % per point (ADR-053); deposit prices and the map marketplace threshold (ADR-070); the AI's retreat threshold.

## Answered
One line per question; the ADR holds the detail.

- **Scope**: fidelity ADR-008; art [presentation.md](../design/presentation.md); modes, platforms, harness, slice ADR-034; lore drafted by Claude.
- **Adventure map**: scale ADR-020, camera ADR-022, terrain ADR-011/045, native terrain ADR-032, roads ADR-024/032, movement ADR-018/037, fog
  ADR-017/031, gates ADR-026, meeting heroes ADR-027, resources ADR-019, chests ADR-039, mines ADR-034, guards ADR-040/065, no sea ADR-051.
- **Objects**: MAP-1 … MAP-14 ADR-068; MAP-15 … MAP-19, MAP-21 … MAP-24 ADR-070.
- **Towns**: groups ADR-010/028/029, tech tree ADR-030/046, income ADR-025, caravans ADR-054, tavern ADR-060/070, governors ADR-063, TOWN-1 fort
  ADR-066, TOWN-2 ADR-067/068/070, TOWN-3 ADR-070, TOWN-6 ADR-070.
- **Heroes**: roster ADR-048, skills ADR-048/069, stats ADR-055, schools ADR-063/069, artifacts ADR-061, HERO-2 ADR-069 (rest in HERO-7, MAG-9, MAG-10,
  CONT-5).
- **Combat**: grid and sizes ADR-005/021/070, battlefield ADR-033, turn order ADR-009, damage ADR-033, cover ADR-035, tuning ADR-038, speeds ADR-037,
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070.
- **Magic**: MAG-4 (part) ADR-070.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070.
