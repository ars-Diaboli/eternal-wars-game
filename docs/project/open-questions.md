# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then moves to [Answered](#answered) with its ADR.

Every question has a stable id (`RMG-1`, `TOWN-2`, …) so the roadmap and ADRs can point at it. **🚧 Blocking** marks a
question that stops the named roadmap item; the others can wait or only tune numbers.

## 🚧 Blocking now

Phase 4's generator stages 1–6 and the scenario assembly are in. What still blocks the next steps:

| Id | Question | Blocks |
|---|---|---|
| [TOWN-2](#town-2) | Marketplace trading rates | Phase 3: marketplace, governor's 150 %, AI marketplace use; treasure resource values |
| [HERO-1](#hero-1) | Third Offense and Defense skills, skill specialties | Phase 3: skills |
| [HERO-2](#hero-2) | Rage and Adrenaline | Phase 3: faction groups |

## Random maps (Phase 4)

RMG-1 to RMG-7 are answered (see [Answered](#answered)).

<a id="rmg-8"></a>
**RMG-8 Fairness metrics** (blocks the CI batch validation). Which numbers compare start zones (distance to the
nearest mines, treasure value, guard power on the way), what tolerance, and what happens on failure (retry or
report)?

<a id="rmg-9"></a>
**RMG-9 Mirrored templates** (blocks mirrored generation, 1.0 scope). Point or axis symmetry; whole map or per-zone
mirror pairs; which player counts; the underground; how a mirrored layout fits the Voronoi search; mirrored zones
must share their rolls (today each zone rolls on its own stream).

<a id="rmg-10"></a>
**RMG-10 Template editor** (blocks the template editor). An undoable `TemplateEditorSession` like `MapEditorSession`;
where user and mod templates are saved and scanned; live validation and a preview (layout or terrain dump).

<a id="rmg-11"></a>
**RMG-11 Template fields still in the design but not in the format**: town *groups* as a filter (the format lists town
types, ADR-028 says groups may filter), "richness" beyond the mine minimums, border guards (keymasters), optional or
randomly present connections. Keep, drop, or post-1.0?

## Towns and economy

<a id="town-1"></a>
**TOWN-1 Growth beyond the fort.** The fort is answered (ADR-066). Which other buildings raise growth, if any?

<a id="town-2"></a>
**TOWN-2 🚧 Marketplace trading** (blocks trading and the governor's 150 %). Base rates, how they improve with owned
marketplaces, how the governor's 150 % combines with them.

<a id="town-3"></a>
**TOWN-3 Town hall 4, mage guild 4 cost, per-town building lists.** A fourth town hall level? Is the mage guild 4
placeholder (7500 gold + 7 crystal, mercury, sulfur) right? Do towns get their own building lists or names?

<a id="town-4"></a>
**TOWN-4 Tavern.** Are the numbers right (hero 2500 gold; Brotherhood 2000 gold + 5 lumber + 5 ore; at most 8 heroes
per player; a tavern offers 2 heroes, the Brotherhood 4)? Per-town instead of per-player pools? Hiring while a hero
stands at the entrance? A starter stack for hired heroes? Do heroes in garrisons or prisons count towards the 8?

<a id="town-5"></a>
**TOWN-5 Caravan details.** Is the 5-day limit checked only when ordering (today only the route is re-checked
daily)? How long may a caravan wait at a full garrison? Guards on map dwellings?

## Heroes

<a id="hero-1"></a>
**HERO-1 🚧 Third skills and specialties** (blocks the Phase 3 skill items). The third Offense and Defense skills
(placeholders `offense.third`, `defense.third`); what a skill specialty adds.

<a id="hero-2"></a>
**HERO-2 🚧 Rage and Adrenaline** (blocks the faction groups). What the three skills of each group do (rage or
adrenaline stacks, or other faction abilities); whether the Shamanism and Primal spell drafts become these abilities;
what Spell Power and Knowledge give Stronghold and Swamp mages; the offer weights (today the own-school weights).

<a id="hero-3"></a>
**HERO-3 Rival schools.** Level-ups use the sworn-enemy pairs from the lore, the tavern uses the ring. Keep both, or
use one relation for both?

<a id="hero-4"></a>
**HERO-4 Deployment skill** (blocks the Deployment item). Zone size per level; how a human deploys and how the AI does.

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names (Light, Nature, Arcana, Death, Chaos, Shadow) and specialties in
`data/heroes.csv`; primary stat odds and starting values (ADR-055); the ADR-048 skill numbers.

## Combat

<a id="cmb-1"></a>
**CMB-1 Surrender.** Price formula, allowed against neutrals, where the hero goes (back to the tavern pool?), may the
AI surrender or retreat (it never does today)?

<a id="cmb-2"></a>
**CMB-2 Morale and luck sources.** Artifacts already add them (ADR-061). Which skills, buildings and army mixes add
or remove them; do undead or constructs ignore morale; are 2 %/4 % per point and the mirror-image bad luck right?
War machines have no morale but roll the hero's luck: intended?

<a id="cmb-3"></a>
**CMB-3 War machines.** Machine stats and prices after play-testing; can heroes exchange machines; can towns hold
them in the garrison; which machines the AI buys.

<a id="cmb-4"></a>
**CMB-4 Teleporting creatures** (Phase 5). How they use footprints.

<a id="cmb-5"></a>
**CMB-5 Sieges** (post-1.0). Confirm sieges are after 1.0; until then the siege Ballistics rules (ADR-063) stay unused.

## Magic (Phase 5)

These block Phase 5 magic, not Phase 4.

<a id="mag-1"></a>
**MAG-1 Spell list.** The spells and numbers per school ([spells.md](../content/spells.md)); spell tags beyond the
free-form immunity tags (`all`, `mind`, `fire`, `curse`, `holy`); which spells creature casters get (`cure`,
`resurrection`, `fireball` are placeholders).

<a id="mag-2"></a>
**MAG-2 Spell book and learning.** Does every hero start with a spell book or buy one? How are spells learned (guild
visit, Magic level needed)? What does a guild offer: how many spells per level, only its own school? What do
Stronghold and Swamp Bestiary guilds offer, having no school?

<a id="mag-3"></a>
**MAG-3 Power and mana.** What Spell Power does in numbers; mastery ranks; is attunement "potency" (ADR-047) kept,
dropped by ADR-063's table? Does mana refill in a town or guild?

<a id="mag-4"></a>
**MAG-4 Casting in battle.** Does the hero's spell use a turn in the initiative order or happen outside it; spells
that change initiative mid-round; area shapes against 2×2–4×4 footprints; can spells target war machines.

<a id="mag-5"></a>
**MAG-5 Adventure spells.** Which Magic level unlocks them (spells.md is inconsistent: Town Portal and Dimension Door
are level 3); movement cost; how they interact with fog, the AI and replays.

<a id="mag-6"></a>
**MAG-6 Spell resistance and spell specialties.** The Spell resistance skill's numbers; how strong a spell specialty
is.

## Game flow and content

<a id="flow-1"></a>
**FLOW-1 Victory conditions.** More conditions (grail artifact, flag all mines, defeat all monsters); does survival
win for the whole team; how a campaign hooks into them.

<a id="flow-2"></a>
**FLOW-2 Week events** ("week of X"): which events and how likely.

<a id="cont-1"></a>
**CONT-1 Lore review.** Premise, world and group names ([lore.md](../content/lore.md)); the design docs use working
titles (Light, Darkness, Wild, Primordial), lore and the artifact sets use Dawn Covenant, Ashen Court ("Ash Court" in
`artifact_sets.csv`), Unbound, Firstborn. Which names stay?

<a id="cont-2"></a>
**CONT-2 Roster review.** Every roster in `data/creatures.csv` (names, sizes, capabilities, numbers); re-run
`sim towns` for the current balance.

<a id="cont-3"></a>
**CONT-3 Artifacts.** The draft numbers and tiers (ADR-061); should chests hold artifacts (also RMG-1); are the
HoMM3-style placeholders to stay or be replaced by lore items; Crown of the Waning Eye's "best magic skill" and ties.

<a id="cont-4"></a>
**CONT-4 More twists.** Which other systems should depart from HoMM3 ([departures.md](../design/departures.md))?

## Numbers to tune (not questions)
Placeholders that wait for play-testing, not for a decision: caravan building cost (1000 gold, 5 lumber) and its
place after town hall 2 (ADR-054); complexity starting resources other than Expert (ADR-063); Stewardship and governor
percentages (ADR-063); siege Ballistics formulas (ADR-063); zone guard and garrison powers and `GuardPicker` ranges
(ADR-064); building costs (`buildings.csv`).

## Answered
Grouped by topic; the ADR holds the detail. Later ADRs that changed an answer are noted.

**Scope and style**
- Fidelity: HoMM-inspired with our own twists (ADR-008, [departures.md](../design/departures.md)).
- Art: realistic lighting and textures, dark and grim fantasy, nods to the original
  ([presentation.md](../design/presentation.md)).
- Modes: single-player vs AI → hot-seat → story campaign; no online multiplayer.
- Platforms: Windows + Linux/Steam Deck for 1.0; modding not required for 1.0 (ADR-034).
- Harness: Godot 2D viewer + headless .NET simulator (ADR-034).
- Vertical slice: Haven and Inferno (ADR-034).
- Lore: Claude drafts, the owner reviews ([lore.md](../content/lore.md), review is CONT-1).

**Adventure map**
- Map scale: 48–256, max 8 players (ADR-020). Camera: 15–30° from vertical, ±45° rotation, zoom (ADR-022).
- Terrain: smooth terrain with a grid overlay (ADR-011); every terrain penalises non-natives 125–200, several natives
  per town, beach, ice, shallow water, cursed lands, rocky ground (ADR-045, replaces the terrain classes of ADR-024 and
  ADR-032; Necropolis is native on cursed lands, Stronghold on wasteland).
- Native terrain: the whole army must be native; Pathfinding 25/50/75 % (ADR-032).
- Roads: HoMM3-style, road cost only road → road (ADR-024, ADR-032).
- Movement: HoMM3 rules, pay for the tile left, last step with any points left; points from the slowest creature's map
  speed + skills + artifacts (ADR-018, ADR-037).
- Fog of war: chosen per game, permanent reveal or shroud + unit refog with hysteresis (ADR-017); auto-stop when an
  enemy comes into sight, ambush battles, last known state of objects (ADR-031).
- Gates: two-way and one-way gates; no random-exit portals (ADR-026).
- Meeting heroes: own → exchange, enemy → battle, allies → no interaction (ADR-027). Teams: map defaults, editable at
  setup unless locked (ADR-032).
- Resources: gold, lumber, ore, crystal, mercury, sulfur; no gems (ADR-019).
- Treasure chests: HoMM3 tiers without the artifact, equally likely; authors may fix values (ADR-034, ADR-039).
- Mines: optional guards, then re-flagged by visiting (ADR-034).
- Neutral guards: Classic or Area guard mode, HoMM3 dispositions with deterministic thresholds (ADR-040).
- Sea travel: none; water is an obstacle (ADR-051).

**Towns and economy**
- Towns: 8 playable in 3 groups + Conflux and Dragon Utopia in a fourth, non-playable group (ADR-010).
- Groups and rivalries: lore and campaign only, no combat effect (ADR-028, ADR-029); the two rule effects outside
  combat are rival-school odds (ADR-048) and tavern ranks (ADR-060).
- Tech tree: 5 tiers, 9 dwellings, build 7 with every tier, all playable creatures upgradeable, final per town
  (ADR-030). Dwelling upgrades produce the upgraded creature, no growth bonus (ADR-046).
- Town footprint: 7×7, corners free, entrance bottom-middle (ADR-046).
- Income: no placeholder income; towns, mines, skills, specialties, artifacts (ADR-025).
- Caravans: hire from owned map dwellings within 5 days on an open route, also send troops between towns; a blocked
  route returns them with a refund, a lost town scatters them, invisible (ADR-054).
- Tavern: per-player weekly pool ranked by the ring; 2 offers, 4 with the Brotherhood (ADR-060).
- Governors, complexity starting resources (ADR-063).

**Heroes**
- Roster: 16 heroes per town, mage and warrior per tier 1–4 line, flat preferred-creature bonus (ADR-048).
- Skills: HoMM4-like groups, named levels, one point per level, rival schools at 5 % (ADR-048).
- Primary stats: one point per level, HoMM3-style odds by class (ADR-055). Attack and Defense are map data (ADR-041);
  Tactics became a skill (ADR-048).
- Magic schools: six, one per ring town; Stronghold and Swamp Bestiary have Rage and Adrenaline (ADR-063, was eight in
  ADR-047); four spell and guild levels, spells upgradable twice (ADR-047); mana numbers (ADR-063).
- Artifacts: slots, a backpack of 50, loot that does not fit is lost, most valuable first (ADR-061).

**Combat**
- Separate battlefield, square grid, footprints 2×2 / 3×3 / 4×4 (ADR-005).
- Size: surround and body blocking; no reach, cleave or trample from size; breath and cleave are creature abilities
  (ADR-021).
- Battlefield 24 tall, 30 wide by default; Tactics chooses 28–34 (Basic 30/32, Advanced 28/30/32, Expert 28–34),
  enemy Tactics reduces yours; 36 only for sieges (ADR-033, refines ADR-021).
- Turn order: initiative decides order, one action per round, HoMM3 wait and ties (ADR-009); good morale may add an
  action (ADR-053).
- Damage: HoMM3 formula (ADR-033). Cover: cumulative per blocker, above 7/8 no shot (ADR-035).
- No facing, no generic flanking; ability hooks instead; breath hits friends (ADR-021).
- Tuning: no ambush bonus, neutrals non-native everywhere, 8-way movement without corner squeezing, supercover line
  of fire, area hits a unit once (ADR-038).
- Speeds: battle Speed and map speed are separate (ADR-037).
- Battle end: retreat built; surrender still open (CMB-1, ADR-038, ADR-060).
- Morale and luck: ±5 per stack, morale per round, luck per hit (ADR-053).
- War machines: offensive machine per town, tent, cart, War machines group (ADR-056); siege Ballistics (ADR-063).
- Victory and defeat: default rules, grace period, map conditions (ADR-057).

**Random maps**
- Standalone editor; HotA-style templates authored in the editor; game and editor generate (ADR-023).
- 1.0: mirrored templates, saved generated maps; no water or islands, no HotA import (ADR-034).
- Generator output is a scenario `GameState` (ADR-064).
- Connections: one or more passages per connection, set in the template (ADR-064).
- Guards: army power maps to overlapping tier ranges, so one power can be different counts of one tier or an
  adjacent tier (ADR-064).
- Towns, mines, piles: HoMM3-style defaults (ADR-064).
- RMG-1 treasure: a band's value is filled with piles and chests (2 : 1), artifacts only from 4000 (half the items
  there); the generator fixes the contents (ADR-064).
- RMG-2 guard modes: per monster stack, Classic for general use, Area for treasure so it cannot be picked up without a
  fight (ADR-065); treasure worth more than 1000 is guarded with 40–60 % of its value, half in start zones (ADR-064).
- RMG-3 neutral monsters: no free-roaming (walking) monsters for now; monsters guard passages, gates, mines and
  treasure (ADR-064).
- RMG-4 roads: towns of a zone and the zones (through their passages) are joined by road; gates and the lumber mill and
  ore mine closest to the town get a road where the zone has a town; configurable per template (ADR-064).
- RMG-5 (part): a slot nobody takes keeps its towns as neutral towns; players pick their start hero at setup (ADR-064).
- RMG-5 (rest, 2026-10-17; implemented, ADR-064):
  - Start towns: pre-built buildings come from a field of the template's start zone (hand-made maps already set them per
    town in the map editor). Without a tier-1 dwelling there, the player picks one on the game creation screen after
    picking the faction; a computer player gets a random tier-1 dwelling.
  - Neutral towns: the default start (town hall 1, fort 1, tavern 1).
  - A slot nobody takes: its neutral town gets a garrison by the zone's strength, like any neutral town.
  - Start hero army: HoMM3-style, 2–3 stacks of the town's tier-1 and tier-2 creatures (the preferred creature among
    them when it is tier 1–2), about 1000–1500 power.
  - Problems: retry with the next sub-seed of the same seed (up to 10 times, deterministic); if all fail, use the best
    map and show its problems on the new-game screen.
  - Rules: standard victory, elimination with the 7-day grace period, complexity and fog from the new-game screen; the
    map is named after the template, size and seed.
  - Generator version: a `MapGenerator.Version` constant saved with the game record and shown with the seed; another
    version warns that the map may differ.
- RMG-7 start zones and town types (2026-10-17; implemented, ADR-064): in the game, factions are chosen before generation and
  are an input of the generator ("Random" is drawn from the seed first), so start zones are painted native; generated
  in the editor with a random or later-chosen town, the terrain may differ. A start zone is painted with a mix of its
  town's native terrains (intersected with the template's terrain list when it has one); other zones follow the
  template only.
- RMG-6 teams (2026-10-17, implemented, ADR-064): an optional team per start zone (same number = allies, none = free for
  all); players may change teams at setup unless the template locks them; no team fairness checks; the connections, not a
  layout rule, decide where allies are.

**Towns (2026-10-17)**
- TOWN-1 the fort: fort 1 is needed to build creature dwellings and gives a wall and gate in sieges; Citadel +50 %,
  Castle +100 % growth of the town's dwellings and better fortifications (ADR-066).
