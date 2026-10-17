# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then moves to [Answered](#answered) with its ADR.

Every question has a stable id (`RMG-1`, `TOWN-2`, …) so the roadmap and ADRs can point at it. **🚧 Blocking** marks a
question that stops the named roadmap item; the others can wait or only tune numbers.

## 🚧 Blocking now

The next roadmap steps are Phase 4's generator stages 5–8 and the Phase 3 building effects. These answers are needed
first, in this order:

| Id | Question | Blocks |
|---|---|---|
| [RMG-1](#rmg-1) | What treasure is made of and what it is worth | Phase 4: treasure by value band (**next step**) |
| [RMG-2](#rmg-2) | Guards on treasure, guard mode on generated maps | Phase 4: treasure, neutral monsters |
| [RMG-3](#rmg-3) | Free-roaming neutral monsters per zone | Phase 4: neutral monsters |
| [RMG-4](#rmg-4) | Roads on generated maps | Phase 4: roads |
| [RMG-5](#rmg-5) | Building the scenario: missing players, heroes, towns, defaults | Phase 4: scenario assembly, new-game screen |
| [RMG-6](#rmg-6) | Teams in templates | Phase 4: scenario assembly, 2v2 template |
| [RMG-7](#rmg-7) | Start zones and the player's town type | Phase 4: scenario assembly (fairness) |
| [TOWN-1](#town-1) | What the fort does | Phase 3: fort effect, AI town defence |
| [TOWN-2](#town-2) | Marketplace trading rates | Phase 3: marketplace, governor's 150 %, AI marketplace use; also RMG-1 values |
| [HERO-1](#hero-1) | Third Offense and Defense skills, skill specialties | Phase 3: skills |
| [HERO-2](#hero-2) | Rage and Adrenaline | Phase 3: faction groups |

## Random maps (Phase 4)

<a id="rmg-1"></a>
**RMG-1 🚧 Treasure objects and values** (blocks the treasure stage). A template zone has value bands with a count
(e.g. 500–3000 ×9). Which objects fill a band: resource piles, chests, artifact pickups (minor/major/relic), map
dwellings? What is each worth in gold (a gold value per resource needs TOWN-2 or a fixed table; artifacts have no gold
value today)? Is `Count` exact or a density? Does the generator fix chest and artifact values, or leave them to the
game-start roll (ADR-039, ADR-061), which makes the band approximate? Proposal: mostly piles and chests (≈60/30),
artifacts only in bands above 4000, values fixed by the generator.

<a id="rmg-2"></a>
**RMG-2 🚧 Treasure guards and guard mode** (blocks treasure and monsters). Is every treasure above a threshold guarded
in proportion to its value (HoMM3), or are guards placed only by the monster stage? Proposal: guard treasure worth more
than ~1000 with 40–60 % of its value, lighter or none in start zones. Which guard mode (Classic or Area, ADR-040) do
generated maps use: a template field, a setup option, or always Classic?

<a id="rmg-3"></a>
**RMG-3 🚧 Neutral monsters** (blocks the monster stage). How does a zone's strength (none/weak/average/strong) become
monster power and density beyond guards; does it scale with map size or distance from the start zones? Creature
choice: any playable town plus neutrals (as guards do), the zone's terrain or town types? Dispositions: connection
guards are always Aggressive today; random dispositions elsewhere? Counts fixed by the generator (`GuardPicker`
ranges) or rolled at game start (`GuardService`, tier 1 20–50)? How army speed and native terrain affect values.

<a id="rmg-4"></a>
**RMG-4 🚧 Roads** (blocks the roads stage). Which places are joined (towns, mines, passages, gates)? Road type per
zone, per connection or by distance from the start? Roads through guarded passages? Templates have no road fields yet.

<a id="rmg-5"></a>
**RMG-5 🚧 Building the scenario** (blocks the scenario assembly and the new-game screen). With fewer players than
the template's maximum, are unused start zones dropped, left neutral, or kept as empty towns? How do `AiStart` zones
map to computer players? Starting heroes: a random or chosen roster hero of the player's town, with what army?
Pre-built buildings and dwellings for start and neutral towns? Scenario defaults: victory/defeat rules, grace days,
complexity, name and description. What happens when a stage reports `Problems`: accept, fail, or retry with the next
sub-seed? Where is the generator version stored?

<a id="rmg-6"></a>
**RMG-6 🚧 Teams in templates** (blocks the 2v2 starter template). ADR-032 says templates define default teams, but
the format has none. A field per start zone (team) in template format v2?

<a id="rmg-7"></a>
**RMG-7 🚧 Start zones and town types** (blocks fair scenario assembly). Start-zone terrain is painted before the
player picks a town, so it may not be native (a large cost under ADR-045). Repaint the start zone after the pick, or
choose towns before generating? Is "random town" a setup option?

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
**TOWN-1 🚧 The fort** (blocks the fort effect). Growth bonus per level, garrison defence, or both, given that sieges
are post-1.0? Which other buildings raise growth, and do growth bonuses reach map dwellings?

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
