# Decisions: Project scope and owner answers

**Covers**: Scope and phases, the owner's batches of answers that touch several areas, phase close-outs and the Godot checks, closing reviews.

**Current rules**: [memory-bank/roadmap.md](../roadmap.md), [docs/design/overview.md](../../docs/design/overview.md), [docs/design/departures.md](../../docs/design/departures.md), [docs/design/missing-mechanics.md](../../docs/design/missing-mechanics.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [034](#adr-034-scope-slice-towns-platforms-modding-tooling-random-map-features) | Scope: slice towns, platforms, modding, tooling, random-map features | accepted |
| [043](#adr-043-phase-0-spike-results-godot-smoke-tests) | Phase 0 spike results; Godot smoke tests | accepted |
| [070](#adr-070-answers-to-the-adr-069-questions-teams-creature-sizes-mirrored-templates-template-editor-2026-10-22) | Answers to the ADR-069 questions, teams, creature sizes, mirrored templates, template editor (2026-10-22) | accepted; partly superseded |
| [071](#adr-071-data-tables-fairness-keymasters-growth-buildings-morale-exemptions-spell-books-and-guilds-2026-10-22) | Data tables, fairness, keymasters, growth buildings, morale exemptions, spell books and guilds (2026-10-22) | accepted; partly superseded |
| [072](#adr-072-answers-on-deployment-necromancy-skill-ids-rivals-taverns-and-spells-2026-10-22) | Answers on deployment, Necromancy, skill ids, rivals, taverns and spells (2026-10-22) | accepted; partly superseded |
| [073](#adr-073-confirmations-war-machine-footprints-caravan-reach-base-creature-map-dwellings-victory-conditions-week-events-2026-10-23) | Confirmations, war machine footprints, caravan reach, base-creature map dwellings, victory conditions, week events (2026-10-23) | accepted; partly superseded |
| [075](#adr-075-fifth-round-answers-caravan-reach-and-queue-base-creature-map-dwellings-army-morale-2026-10-23) | Fifth-round answers; caravan reach and queue, base-creature map dwellings, army morale (2026-10-23) | accepted |
| [083](#adr-083-smaller-roadmap-items-town-type-in-the-editor-team-victory-skill-specialties-teleporters-machine-exchange-sim-complexity-2026-10-28) | Smaller roadmap items: town type in the editor, team victory, skill specialties, teleporters, machine exchange, sim complexity (2026-10-28) | implemented |
| [085](#adr-085-godot-smoke-pass-2026-10-28) | Godot smoke pass (2026-10-28) | done |
| [088](#adr-088-diagonal-battle-movement-speed-settings-turn-timer-auto-combat-lan-multiplayer-2026-10-28) | Diagonal battle movement, speed settings, turn timer, auto combat, LAN multiplayer (2026-10-28) | accepted |
| [099](#adr-099-sieges-before-10-spells-and-artifacts-re-drafted-clients-view-smooth-fog-start-zones-2026-10-29) | Sieges before 1.0; spells and artifacts re-drafted; clients' view, smooth fog, start zones (2026-10-29) | accepted |
| [100](#adr-100-artifacts-with-homm-names-and-assembled-relics-the-arena-before-10-with-lan-pandoras-box-2026-10-29) | Artifacts with HoMM names and assembled relics; the arena before 1.0 with LAN; Pandora's Box (2026-10-29) | accepted |
| [113](#adr-113-the-godot-check-of-what-was-built-but-never-run-sieges-garrison-heroes-dwellings-and-caravans-generated-connections-adr-085-adr-109-adr-112-2026-11-02) | The Godot check of what was built but never run: sieges, garrison heroes, dwellings and caravans, generated connections (ADR-085, ADR-109, ADR-112, 2026-11-02) | — |
| [114](#adr-114-phase-3-close-out-spell-buildings-the-town-screens-tree-the-tavern-screen-specialties-inspector-rows-ember-march-adr-072-adr-083-adr-093-adr-107-2026-11-02) | Phase 3 close-out: spell buildings, the town screen's tree, the tavern screen, specialties, inspector rows, Ember March (ADR-072, ADR-083, ADR-093, ADR-107, 2026-11-02) | implemented |
| [116](#adr-116-new-ideas-prologue-health-bars-dividing-obstacles-scenery-layout-information-windows-icon-prompts-missing-mechanics-2026-11-02) | New ideas: prologue, health bars, dividing obstacles, scenery, layout, information windows, icon prompts, missing mechanics (2026-11-02) | recorded |
| [118](#adr-118-answers-to-camp-1-ui-3-and-cmb-12-adr-116-2026-11-03) | Answers to CAMP-1, UI-3 and CMB-12 (ADR-116, 2026-11-03) | accepted |
| [126](#adr-126-the-owners-picks-from-the-missing-mechanics-map-bans-random-placeholders-university-and-war-academy-spell-scrolls-the-grail-the-first-unique-buildings-adr-116-2026-11-04) | The owner's picks from the missing mechanics: map bans, random placeholders, University and War Academy, spell scrolls, the Grail, the first unique buildings (ADR-116, 2026-11-04) | accepted |
| [148](#adr-148-the-godot-check-of-what-was-built-but-never-run-magic-sieges-the-grail-unique-buildings-map-sites-game-flow-adr-113-2026-11-09) | The Godot check of what was built but never run: magic, sieges, the Grail, unique buildings, map sites, game flow (ADR-113, 2026-11-09) | done |
| [175](#adr-175-closing-the-code-review-adr-165--adr-174-2026-11-11) | Closing the code review (ADR-165 … ADR-174, 2026-11-11) | implemented |
| [226](#adr-226-closing-the-refactoring-plan-adr-202--adr-224-2026-11-19) | Closing the refactoring plan (ADR-202 … ADR-224, 2026-11-19) | implemented |
| [235](#adr-235-the-godot-check-of-what-adr-148-had-not-seen-resurrection-the-intel-spells-the-ais-battle-magic-a-hand-set-guild-spell-a-dug-hole-meteor-showers-rubble-adr-148-adr-177-2026-11-19) | The Godot check of what ADR-148 had not seen: Resurrection, the intel spells, the AI's battle magic, a hand-set guild spell, a dug hole, Meteor Shower's rubble (ADR-148, ADR-177, 2026-11-19) | done |


### ADR-034: Scope: slice towns, platforms, modding, tooling, random-map features
**Status**: accepted (2026-10-15)
- Vertical slice towns: **Haven and Inferno**.
- Treasure chests roll HoMM3-style tiers using the seeded game RNG; map authors may fix values.
- Mines may carry an optional guard; after the guard is beaten, any hero re-flags by visiting.
- Platforms for 1.0: Windows, Linux/Steam Deck. macOS later.
- Player modding is not a 1.0 requirement; formats stay data-driven and versioned anyway.
- Harness: the Godot 2D viewer plus a headless .NET simulator (`EternalWars.Tools.Sim`) for scripted
  scenarios, AI-vs-AI and RMG batch validation in CI.
- Random maps for 1.0: mirrored/symmetric templates and saving generated maps. No water/islands and no
  HotA template import.

### ADR-043: Phase 0 spike results; Godot smoke tests
**Status**: accepted (2026-10-15)
Ran all three apps in Godot 4.7.2 .NET (`Godot_v4.7.2-stable_mono_win64`):
- All three import, build and run; the 2D harness on Compatibility, game and editor on Forward+.
- **Library nodes created in code work** (views, HUD, camera, panels from `Godot.Shared`/`Godot.UI`), including
  `_Ready`/`_Process`/`_Draw`/`_GuiInput` overrides and C# events. ADR-007 stands.
- **`[GlobalClass]` does not register classes from referenced assemblies**: a probe class left
  `global_script_class_cache.cfg` empty. Classes that must be attachable in the Godot editor stay in the app
  project; prefabs cannot carry library scripts.
- Separate projects with linked shared assets (ADR-004) work; the junctions import cleanly.
- Godot rewrites app `.csproj` files on import: it pins `Godot.NET.Sdk/4.7.2` and inserts
  `<TargetFramework>net8.0</TargetFramework>` because it cannot see `Directory.Build.props`. The apps therefore
  state `net10.0` explicitly; Godot keeps it, and .NET 10 assemblies load fine. Godot 4.4+ `*.cs.uid` files are
  committed.
- Smoke testing: Godot's Movie Maker (`--write-movie`, `--fixed-fps`, `--quit-after`) renders frames to PNGs, and
  `AutoInput` (`-- --autoinput=frame:click:x,y;frame:key:Name`) feeds scripted input through the normal input
  pipeline. `tools/smoke-godot.ps1` runs scripted scenarios for every app and fails on Godot/C# errors. It needs a
  GPU, so it runs locally, not in CI (yet).
- The first runs found two core bugs, now fixed and tested: no route could reach a monster (all its neighbours
  are guarded; the pathfinder now allows the last step from a tile guarded by the target monster), and
  `DecisionRequested` for a battle was published before the width-choice phase was set.

### ADR-070: Answers to the ADR-069 questions, teams, creature sizes, mirrored templates, template editor (2026-10-22)
**Status**: accepted as a design (2026-10-22), user answers in `ideas.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of ADR-038 (ranged
half damage), ADR-060 (tavern pools and price), ADR-026 (portal naming), ADR-068 (the undead Ruins bank's name and every bank's guards). Points marked
*(assumed)* are the agent's reading of a short answer; they are listed again in `open-questions.md` to be confirmed.

- **Teams**: a game has 2 to 8 teams (8 = free for all). Templates and maps set team defaults and restrictions (fixed teams, allowed team counts); the map
  editor may override what the template set, e.g. on a generated map.
- **Creature sizes**: about 95 % of creatures are Small (2×2) or Medium (3×3); Large (4×4) is kept mostly for dragons. Today 42 of 182 lines are Large;
  the roster pass moves the others down and re-checks `sim towns`.
- **RMG-9 mirrored templates**: only for 2, 4 or 8 players (8 uses the underground). Two players: axis symmetry, the axis vertical, horizontal or
  diagonal; four or eight players: point symmetry around the map centre. The template picks the symmetry *(assumed; "random axis" allowed)*. Mirrored
  zones share their rolls.
- **RMG-10 template editor**: no undo needed (unlike `MapEditorSession`); works like the HotA template editor (zone graph, zone and connection
  properties); "Generate" in the editor shows the result. User templates live in the user data folder *(assumed)*.
- **RMG-11 (part)**: a template may enable portals (monoliths) so the generator links zones with them; a connection's guard is a monster guard or a
  **garrison**, per template.
- **MAP-15 Mage's Chest**: 5 % nothing, 5 % a Major artifact, 60 % a Minor artifact, 30 % a Minor artifact and 500–1000 gold; vanishes.
- **MAP-16 Window and Eye of the Magi**: four colours (red, green, blue, black); a Window reveals radius 6 around each Eye of its colour, up to 4 Eyes a
  colour; placed only in the map editor, never by the generator. Every visit *(assumed, like HoMM3)*.
- **MAP-17 / TOWN-4 taverns**:
  - Town taverns: the per-player pool becomes **per player and faction**: a town offers its native and adjacent heroes (2 native + 2 adjacent with the
    Brotherhood), and the player's towns of one faction share that pool.
  - Brotherhood costs **1000 gold + 5 ore** (was 2000 gold + 5 lumber + 5 ore).
  - Hired heroes get a **starter stack** that follows the hero's preferred creature.
  - The **map tavern** works like a Brotherhood (4 offers); the hero must stand on its entrance and the new hero appears on a free cell next to it (one cell
    below by default). The hero limit applies; with the limit reached or no free cell, hiring is refused. Its pool: the answers differ (the player's pool
    or its own random heroes), open as TOWN-4.
- **MAP-18 portals**: "portal" and "monolith" are one thing (ADR-026); a portal type has exactly two ends, no groups of three or more.
- **MAP-19 garrison**: flagged like a mine, captured by beating its guards; a neutral garrison's guards come from the map or the generator; own heroes
  pass both ways; heroes leave and take creatures at any time; the generator may use it as a connection guard (RMG-11).
- **MAP-21 deposits**: the proposed prices (Gold Vein 5000 gold + 10 ore, the others 2000–3000 gold + 5 lumber or ore), paid from the player's resources by
  any visiting hero; the mine is flagged for that player; guarded by a normal monster guard; a **Sulfur Vent** (→ sulfur mine) is added; the generator
  places deposits separately, like abandoned mines.
- **MAP-22 creature banks**: the agent drafts tables like the existing banks (four rows at 30 / 30 / 30 / 10 %, rewards scaled to the guard's value).
  **Every bank, old and new, is re-drafted with 2–3 creature types of one faction.** The undead Ruins is renamed **Crypt** (`bank.crypt`, old maps load
  `bank.ruins` as the Crypt); the orc bank takes the name Ruins. Dragon City is the strongest bank by far.
- **MAP-23 Mana Vortex**: sets mana to twice the maximum until spent (HoMM3), once a week per hero. The Fountain of Magic: once a day per hero *(assumed)*.
- **MAP-24 shrines**: the spell is set in the editor or rolled at game start; learning needs the school's Magic skill at the spell's level; any hero, any
  number of visits; a hero without the skill or with the spell already shows a message.
- **TOWN-2**: the map marketplace's threshold stays the placeholder 4000. Nothing about trading is open.
- **TOWN-3 buildings**:
  - Towns get **their own building lists and names**: `buildings.csv` gains a town column (like `creatures.csv`); designers set names, costs and
    requirements there. Every town later gets a unique building.
  - **Capitol** (town hall 4): one per player; capturing a town with a Capitol when the player already has one downgrades it to town hall 3 *(assumed)*.
  - The mage guild 4 placeholder stays.
- **TOWN-6 Necromancy Amplifier**: 1500 gold + 5 mercury, needs mage guild 1; a captured one helps its new owner; Necropolis-only (the first unique building).
- **CMB-1 surrender**: not against neutrals; price = half the gold cost of the surviving creatures; the hero returns to the tavern pool. The AI **retreats**
  when its army is almost beaten (threshold: tuning); AI surrender is not planned.
- **CMB-3 war machines**: heroes exchange machines; a town garrison holds machines only through a hero in it.
- **CMB-4 teleporting creatures**: move like flyers (footprint must fit at the destination).
- **CMB-6 distance penalty** (replaces ADR-038's half damage beyond 15 cells):
  - Cells between the footprints: 0–9 no penalty, 10–19 1/8, 20–29 2/8, and so on.
  - Distance and cover penalties add up to one ranged penalty; above 7/8 the target cannot be shot (ADR-035).
  - Marksmanship subtracts a fixed −2/8 / −4/8 / −6/8; "almost no penalty" at Expert is intended.
  - War machines ignore the distance penalty.
- **MAG-4 casting in battle (part)**: the hero casts at most once a round, during the turn of one of the units the hero controls (machines that fire on their
  own do not count); spells that change initiative do not change this. Spells may target war machines; machines are `mechanical` and resist many spells.

### ADR-071: Data tables, fairness, keymasters, growth buildings, morale exemptions, spell books and guilds (2026-10-22)
**Status**: accepted as a design (2026-10-22), user answers in `open-questions.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of
ADR-044 (where the tables live), ADR-069 (the Spell resistance numbers) and the spells.md levels of Town Portal and Dimension Door. Points marked
*(assumed)* are listed under "To confirm" in `open-questions.md`.

- **DATA-1 data tables**: the `*.csv` tables move to a core-owned folder, embedded in `EternalWars.Core.Domain`; the apps read the embedded copy, with
  `--creatures=`-style flags for designers' edited files; the `res://shared/data` copy goes. CSV stays for flat tables, JSON for nested data. A schema
  test per table checks ids, references and columns. **Done 2026-10-23**: the folder is `src/Core/data` (templates in `data/templates`);
  artifacts got `--artifacts=` and `--artifact-sets=`; `DataTableSchemaTests`.
- **RMG-8 fairness**: start zones are compared by distance to the nearest mines, treasure value and guard power on the way, within ±15 %; a map that
  fails is retried with the next sub-seed, then reported.
- **RMG-12**: "richness" is dropped; treasure bands and mine minimums are enough.
- **RMG-13 optional connections**: every connection has a presence probability, 100 % by default.
- **MAP-20 keymasters**: four colours (like the Eyes of the Magi); the key belongs to the player; a border guard vanishes after the first key holder passes,
  a border gate stays and lets key holders through; templates may use them in connections (an option per connection).
- **TOWN-1 growth buildings** (placeholder): two buildings per town, one adds +5 a week to tier-1 growth, the other +2 to tier-2, whether the town has
  one or two dwellings of that tier *(assumed: each dwelling of the tier gets the bonus)*.
- **TOWN-4 map tavern**: its own weekly pool of 4 random heroes of any faction, shared by all players.
- **CMB-7**: undead, constructs and elementals have no morale. **CMB-8**: war machines have no morale but roll the hero's luck (as built).
- **MAG-2 spell books and guilds**:
  - Mage heroes start with a spell book; warriors buy one in a mage guild for 500 gold.
  - A hero learns a spell when the school's first skill (its Magic skill) is at least the spell's level.
  - A guild offers 3 spells at levels 1 and 2 and 2 spells at levels 3 and 4, of the town's own school. Two more buildings add spells from other
    schools by the magic circle (HoMM4-like; details MAG-12).
  - Stronghold and Swamp Bestiary: a 3-level placeholder building teaches warcries / spiritual totems instead of a guild.
- **MAG-3 (part)**: mana refills in a town with a mage guild. Spell Power multiplies damage spells' damage and buffs' and debuffs' duration (numbers with
  the spell list, MAG-1). Attunement "potency" is skipped; the ADR-069 attunement table stays as it is (mana pool, regeneration, Light's cost
  reduction, Death's Necromancy).
- **MAG-4**: the unit whose turn the hero cast in still acts (HoMM3).
- **MAG-5 (part)**: Town Portal and Dimension Door are level-4 spells.
- **MAG-6 (part)**: Spell resistance is −8 / 14 / 20 % (was −10 / 15 / 20 %) to both damage and duration; durations round away from zero, at least 1 round.

### ADR-072: Answers on deployment, Necromancy, skill ids, rivals, taverns and spells (2026-10-22)
**Status**: accepted as a design (2026-10-22), user answers in `open-questions.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of
ADR-048 (rival schools: the arch-rival only), ADR-060 (the basic tavern's offers), ADR-069 (Interference at Expert, Necromancy by value) and ADR-070
(the Fountain of Magic, CMB-6's check order). Points marked *(assumed)* are listed under "To confirm" in `open-questions.md`.

- **HERO-1 skill specialties**: the skill's effect +5 % per hero level (HoMM3-like), for now.
- **HERO-3 rivals**: the ring decides both rules. A town's rival school is that of the town **across the ring** (Sylvan–Dungeon, Haven–Necropolis,
  Academy–Inferno), one per town instead of two sworn enemies; the tavern keeps using the ring.
- **HERO-4 deployment** (Tactics): the zone is the number of **columns from the hero's own edge** in which the player may place troops (4 / 5 / 6).
  Only the side with the higher Tactics deploys; a tie lets nobody. The player drags stacks in the battle scene, then "Start battle"; the AI and a
  side that does not deploy are placed automatically at the very edge. Against heroes and neutral monsters, **not in creature banks**.
- **HERO-6 skill ids**: maps and saves read the old ids as their successors (`offense.third` → `defense.leadership`, `defense.third` →
  `tactics.alert`, `tactics.drill` → `tactics.wartempo`, `tactics.deployment` → `tactics.tactics`, `pathfinding.troops` → `pathfinding.scouting`,
  `adrenaline.*` → `rage.*`), no map format bump; a level above a skill's new maximum drops to it. Replays recorded before the change may diverge.
  Troop management's roster warriors start with Scouting.
- **HERO-7 Primal Fury and warriors**: the towns' mages start with the group; their warriors are offered it with a high weight so they learn it
  quickly *(assumed: the warrior weight 6, as a mage's own school)*. Every **Stronghold** warrior starts with an **Offense** skill, every **Swamp
  Bestiary** warrior with a **Defense** skill *(assumed: one of its two starting skills is swapped where it has none)*.
- **TOWN-7 basic tavern**: 1 native + 1 adjacent hero, the adjacent one from either ring neighbour at random.
- **TOWN-8 hero limit**: prisoners belong to nobody and do not count; heroes in a town garrison do not count either, but cannot leave the garrison
  while the player is at the limit.
- **RMG-11 town groups in templates**: post-1.0.
- **MAP-23**: the Fountain of Magic works **once a week per hero** (not once a day).
- **CMB-6 order**: 1. add the distance and cover penalties, 2. subtract Marksmanship, 3. apply the 7/8 "no shot" limit. Marksmanship can therefore
  make a target shootable that cover would hide.
- **MAG-1 Spell Power** (for now): damage × (1 + SP / 10), duration × (1 + SP / 10). The spell list stays open.
- **MAG-3 spell ranks**: Mastery does not upgrade spells by itself; it allows a known spell to rise (rank 2 at Basic, 3 at Advanced). The spell rises,
  at no cost, when the hero visits a town whose mage guild (or spell building) offers it *(assumed: straight to the rank Mastery allows)*.
- **MAG-5 adventure spells**: casting costs a quarter of the hero's daily movement points (500 of 2000) besides mana *(assumed: of the day's full
  points; a hero with fewer left cannot cast)*; Dimension Door lands only on explored tiles.
- **MAG-6 spell specialties**: the spell's effect +3 % per hero level.
- **MAG-7 Necromancy**:
  - The value is **hit points**: the share of the slain enemies' hit points, summed per undead tier, divided by the undead creature's hit points,
    rounded down.
  - Any hero with Death attunement.
  - Only the enemy's losses count, enemy undead included; constructs and elementals do not (war machines are not creatures). The hero's own losses never count.
  - The base creature of the tier is raised and joins its stack or a free slot; a tier's two lines split in a random proportion at every level.
    Only when there is neither, and the army has the upgraded creature's stack, it is raised as the upgrade at **3/4** of the count. Otherwise lost.
  - After every victory, never after a retreat or a surrender; automatic; shown on its own screen after the battle result and before any level-up.
- **MAG-8**: mana by level adds to the maximum only; every hero has at least 10 (intended).
- **MAG-9 Interference**: +1 / +2 / +3 mana per enemy spell (Expert +3, not +2).
- **MAG-10 Primal Fury mages**: Spell Power strengthens Blood Rage and the warcries (numbers when built); Knowledge does nothing; their level-ups
  favour Attack and Defense *(assumed: the warrior odds)*.
- **MAG-11 area spells**: a fixed area whatever the target's size; a creature with any cell inside is hit once.
- **MAG-12 spell buildings**: one building for each of the two schools next to the town's own on the ring, each adding 1 spell per guild level the
  town has; placeholder cost 2000 gold + 5 of the school's resource, needs mage guild 2.

### ADR-073: Confirmations, war machine footprints, caravan reach, base-creature map dwellings, victory conditions, week events (2026-10-23)
**Status**: accepted as a design (2026-10-23), user answers in `open-questions.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of
ADR-056 (war machine sizes and the machine column), ADR-057 (conditions, team victory), ADR-072 (MAG-5's movement cost, MAG-10's stat odds). Points
marked *(assumed)* are listed under "To confirm" in `open-questions.md`.

- **Confirmed**: every "To confirm" assumption of ADR-070 … ADR-072 stands (RMG-9, RMG-10, MAP-16, MAP-18, MAP-20, TOWN-1, TOWN-3 ×2, TOWN-4,
  TOWN-6, TOWN-7, DATA-1, MAG-3, MAG-5's 25 mana, HERO-7 ×2, CMB-1), except the two below.
- **MAG-10 Primal Fury mages**: level-ups use **even odds, 25 / 25 / 25 / 25 %** at every level (not the warrior odds).
- **MAG-5 adventure spells**: the movement cost is a **fixed 500 points**, whatever the hero's daily total; a hero with fewer than 500 left cannot
  cast.
- **HERO-4 war machines** (HoMM3-like: the machine stands mostly outside the grid): every machine occupies **one column only**, the outermost one
  of its side. The offensive machine and the healing tent are **1 × 3** cells, the ammo cart **1 × 2** *(assumed: every offensive machine —
  ballista, cannon, catapult, glaivethrower — is 1 × 3)*. Their models may overhang the grid edge. Stacks start in the next column (today the
  machine column is as wide as the largest machine plus a gap). The deployment zone of 4 / 5 / 6 columns **includes the machine column**; stacks
  may use its free cells *(assumed)*. Battle units get rectangular footprints for this (creatures stay square).
- **Map dwellings sell base creatures only** for now: no map dwelling of an upgraded creature. The editor offers only base creatures and the
  validator rejects an upgraded one; a map that has one loads it as its line's base creature *(assumed)*. Town dwellings keep their upgrade
  (level 2 sells both, ADR-046).
- **TOWN-5 caravans**: the 5-day reach is checked twice. When a dwelling is **flagged** (and when a town gets a caravan), the raw distance — the
  travel days at the creature's speed ignoring hostile heroes, monsters and guards *(assumed)* — decides whether the dwelling appears in that
  town's caravan list at all. When **ordering**, the full check (open route, 5 days) runs as today; on the way only the route is re-checked. A
  caravan waits at a full garrison until there is room (as built). The town's caravan screen shows a **queue**: caravans on their way (creature,
  count, days left) and those waiting for room.
- **FLOW-1 victory conditions**:
  - New victory conditions: **capture a given map object** *(assumed: an ownable one — mine, dwelling, town)*, **obtain a given artifact** (a hero
    of the player carries it), **release a given hero** from its prison. Capture a town and defeat a hero exist.
  - New defeat conditions: **lose a given artifact** *(assumed: no hero of the player carries it any more)*, next to lose a town and lose a hero
    (which can be the starting ones).
  - **Time limits**: a victory condition may carry a deadline ("defeat the enemy within 3 months"); missing it defeats the player *(assumed)*.
    The plain "time limit" defeat condition stays.
  - **Teams**: a team wins while at least one of its **human** players survives; eliminated human teammates share the win. A computer ally's
    survival does not win for the human players *(assumed: they have lost)*.
  - Campaign hooks stay open (Phase 6).
- **FLOW-2 week and month events** (HoMM3-like; a month is 4 weeks), announced at the start of the week:
  - **Weekly** (every week but a month's first): **75 % week of a creature** — a random creature's weekly growth doubles; picking either the base
    or the upgraded creature doubles its line *(assumed: in towns and map dwellings, that week only)*. **25 %** nothing.
  - **Monthly** (first week of a month; replaces that week's weekly roll):
    - **45 % month of a creature**: a random creature's growth doubles *(assumed: as the weekly one, that week)*, and stacks of it **spawn across
      the map next to random map objects** as their guards (numbers when built).
    - **15 % plague**: no growth that week, and the creatures waiting in every town's dwellings are **halved** (rounded down).
    - **15 % starvation**: no growth that week, nothing else.
    - **25 %** nothing *(assumed: then the weekly roll is made as usual)*.
  - No event in the game's first week *(assumed)*.
  - The map editor and the template editor choose which **factions' creatures** may be picked, separately for weekly and monthly events;
    default: the playable factions only. Rolls use `GameState.Random`.

### ADR-075: Fifth-round answers; caravan reach and queue, base-creature map dwellings, army morale (2026-10-23)
**Status**: accepted (2026-10-23), user answers in `open-questions.md` and a note in `ideas.md`; the caravan, dwelling and morale parts are
**implemented**. Supersedes ADR-073's 5-day caravan reach (TOWN-5) and its "no event in the first week" assumption (FLOW-2). Every other "To
confirm" assumption of ADR-073 stands.

- **TOWN-5 caravan reach**: no "days at the creature's speed" any more. The reach is **7500 movement points** with every step at the plain cost
  (100, diagonals 141): terrain and roads are ignored, and it is the same for every creature (`CaravanRules.MaxReach`, `Distance`).
  - The **raw** distance ignores hostile heroes, monsters and guarded tiles (confirmed) but not rock, water or blocked object cells; a dwelling
    beyond it is not in the town's caravan list (`CaravanRules.Dwellings`). Nothing moves that it counts, so it is a query, not a value stored
    when the dwelling is flagged.
  - **Ordering** needs an open route within the reach, as before; the **travel days** still come from the creatures' map speed along the cheapest
    open route, so a slow stack over swamp may take more than 5 days *(choice made here)*. Sending troops between towns uses the same reach.
  - The **queue** (`CaravanRules.Queue`): the caravans coming to a town, on their way first (fewest days left, then the order sent), then those
    waiting at the gates. The town screen shows it and, apart, the caravans sent from the town.
  - The AI builds a caravan only when a dwelling is within reach and hires only from the town's list.
- **Base-creature map dwellings** (ADR-073, built): `MapEditorSession.SetDwelling` refuses an upgrade (`EditResult.UpgradedCreature`), the editor
  lists base creatures only, the validator reports an upgrade as an error, and loading a map turns an upgraded dwelling into its line's base
  creature (`CreatureCatalog.BaseOf`; no format change).
- **CMB-2 army morale** (built, `BattleParticipant.ArmyMorale`, set when the battle starts):
  - **Mixed factions**: −1 morale per faction beyond the first among the army's creatures (HoMM3); neutral creatures count as one faction, war
    machines do not count. Applies to heroes' armies and town garrisons, not to neutral guards or banks.
  - **Brotherhood**: +1 morale for a town's defenders when the town has the upgraded tavern (tavern level 2): its garrison, or an own hero standing
    at its entrance when attacked.
  - With it, **CMB-7** is built: undead, constructs and elementals have no morale (their creature's, the hero's and warcries' included).
- **FLOW-2**: "no event in the first week" is the default, and the map editor (and the template) may also turn week and month events **off**, or
  start them later: skip up to **8 weeks** (2 months). Built with the week events (map format bump).
- **Campaign-like maps** may start a player **without a town**; losing a hero is then the defeat condition. This already works: a grace period of
  0 turns the town rule off and "lose a hero" is a defeat condition. The validator now warns when a player starts with heroes but no town while the
  grace period is on.
- **MAG-5**: the fixed 500 movement points stand; nothing to build before adventure spells (Phase 5).
- **Terrain relief** (art direction, `ideas.md`): the rules stay on the 2D grid, but the 3D map is **not flat**, Warcraft III-like: walkable ground
  lies between 0 and 4 cell widths of height; below that is water, above it cliffs that are cut off or mountains. Recorded in
  [presentation.md](../../docs/design/presentation.md); built with the terrain look-dev (Phase 5).

### ADR-083: Smaller roadmap items: town type in the editor, team victory, skill specialties, teleporters, machine exchange, sim complexity (2026-10-28)
**Status**: implemented (2026-10-28). Carries out ADR-070 (CMB-3, CMB-4), ADR-072 (HERO-1) and ADR-073 (teams). Choices made here are marked
*(choice)*.

- **Editor: change a town's faction** (`MapEditorSession.SetTownType`, undoable; the inspector's "Faction" list): a town of the new type takes the
  old one's id, place, owner, garrison and a custom name. Buildings the new type also has stay (at most its top level); each dwelling and the
  creatures waiting in it move to the new type's line **at the same place in the same tier** *(choice)*; town-specific buildings drop out.
  Refused (`RequirementMissing`) when the result would lack a requirement. Views see `MapObjectRemoved` + `MapObjectPlaced`.
- **Team victory** (`GameFlowService.End`): the winners are the winning team's players still in the game plus, while one of its **human** players
  survives, its eliminated human players. Eliminated computer players never share the win; when only a computer ally survives, the eliminated
  humans have lost.
- **Skill specialties** (`SkillEffects.WithSpecialty`): the skill's effect × (100 + 5 × hero level) / 100, rounded down, for the effects that
  are amounts: Melee and Archery damage percent, Logistics movement percent, and an attunement's pool and daily mana. Not scaled yet *(choice)*:
  Tactics (columns and widths are steps, not amounts), Blood Rage and Warcry (MAG-10's numbers are not set), Death attunement's Necromancy
  share and spell cost reduction (with magic).
- **Teleporting creatures** (`CapabilityKind.Teleporting`, CMB-4): move like flyers (`CreatureDefinition.MovesFreely`: any free spot within the
  speed, the footprint must fit) and are valued like them (+20 %). No creature has it yet.
- **War machine exchange** (`GiveWarMachineCommand`, `WarMachineGiven`, CMB-3): while two heroes' exchange is open, either hands a machine to
  the other when the receiver has none in that slot. Machines are slots, so an offensive machine becomes the receiver's town's kind *(choice)*.
  Buttons in the exchange panel (not run in Godot yet); the AI's main hero takes the machines it lacks. Town garrisons keep machines through a
  garrison hero, which does not exist yet.
- **`sim game --complexity beginner|easy|normal|hard|expert`** starts every player with that level's resources (`GameOptions.Complexity`);
  without it the map's resources stay.

### ADR-085: Godot smoke pass (2026-10-28)
**Status**: done (2026-10-28), in Godot 4.7.2 .NET with `tools/smoke-godot.ps1`.

- **Assets**: a fresh clone has no `.godot/imported`, so every texture load failed (`game-battle-3d` failed on the terrain textures). Each
  app needs one `--headless --import` first (now in `development.md`).
- **False passes**: four of the five game scenarios clicked "Start" at a fixed point. The new-game screen had grown a row, so they never left
  it and still passed, because the script only looks for errors. The Start button now takes the focus, and the scenarios start with
  `key:Enter`. A passing run still needs a look at the frames.
- **Screens over the map**: Godot's default panel is translucent, so the HUD's text ran into the town and hero screens. The centred
  pop-ups (kingdom overview, Thieves' Guild, marketplace, dwelling, object service) grew right and down from the screen's middle, past its
  edge. `Screens.UseOpaqueBackground` / `Screens.CenterAsScreen` give them an opaque background and make them grow to both sides.
- **New scenarios**: `game-kingdom` (K, Escape), `game-weeks` (a week of "End turn": the week's notice and the event next to the date),
  `game-thieves-guild` (from the town screen), `editor-week-events` (tick a faction, one more skipped week, undo both). The skip-weeks box
  now reads "1 week(s) without events at the start".
- **Seen working**: the 3D battle scene, the town, hero, kingdom and Thieves' Guild screens, the week notice and the date's event, and the
  editor's week event settings with undo.
- **Not covered yet**: the Astrologer's Tower notice, a month's spawned stacks on the map, the marketplace, dwelling, object service,
  Necromancy and caravan screens, the exchange panel's war machines and the editor's town faction change. None of them has a scenario yet.
- **Seen but not fixed**: in the editor the terrain tool's second row of buttons runs under the right-hand panel. The exchange scenario's
  status line shows a raw `MovementOutcome { ... }` record.

### ADR-088: Diagonal battle movement, speed settings, turn timer, auto combat, LAN multiplayer (2026-10-28)
**Status**: accepted as a design (2026-10-28), owner notes in `ideas.md`; **nothing implemented**. The open points are CMB-9, CMB-10, UI-1,
FLOW-3, MP-1 and MP-2 in `open-questions.md`, each with a proposal. **Changes ADR-008's scope**: online multiplayer was out; LAN multiplayer is now
planned (before 1.0, ADR-098).

- **Diagonal battle movement** (CMB-9; built as a circle, ADR-089): a diagonal step on the battlefield costs **√2 ≈ 1.41** of a creature's Speed
  instead of 1, as on the adventure map. Today a stack moves up to Speed steps in any of the 8 directions (`BattleGrid`, Chebyshev distance), so diagonals are
  ~41 % too cheap. This changes reach, the AI and every replay digest.
- **Speed settings** (UI-1; decided in ADR-090): the player sets how fast heroes move on the adventure map and how fast battle animations play. These are player
  settings, not rules: they change no game state and are not saved with a game.
- **Turn timer** (FLOW-3; built, ADR-091): an option on the new-game screen, off by default, **1 to 30 minutes** a turn, for **human** players only. When it
  runs out the player's turn ends on its own. It runs on the adventure map and in battle. If it runs out during a battle, the battle is finished
  first and the turn ends after it.
- **Auto combat** (CMB-10; built, ADR-090): a setting that skips a battle the player starts. The battle AI plays **both sides**, a screen shows the outcome,
  and the player **accepts** it or **rejects** it. Rejecting plays the battle by hand as usual. This is a step before the battle; the quick
  combat button during a battle stays.
- **LAN multiplayer** (MP-1, MP-2): over a local network for now. The **host is the source of truth**. Players pick a map or generate a
  random one when they create the game. A multiplayer game can be saved and loaded again in multiplayer, single player or hot-seat. Loaded in
  single player, the player picks one of the colours that were human, and the computer plays the rest. The **first week may be played
  simultaneously** while players stay in their start zones and do not touch the same object. When that is broken, the turn is played again
  in the normal order.
- Fits the core as it is: commands go through `session.Execute` and the game is deterministic, so a host can run the game and clients can
  send commands (MP-1). The timer's end of turn is an ordinary `EndTurnCommand`, and auto combat can preview a battle on a copy of the state.

### ADR-099: Sieges before 1.0; spells and artifacts re-drafted; clients' view, smooth fog, start zones (2026-10-29)
**Status**: accepted (2026-10-29), the owner's answers to CMB-5, MAG-1, CONT-3, MP-3 and MP-4; the re-drafts are **content drafts** (docs),
nothing in code. New questions: CMB-11 (siege rules), MAG-13 (guild selection, spell review), CONT-6 (artifact review).

- **Sieges come before 1.0** (CMB-5; changes ADR-066's and ADR-063's "post-1.0"): a battle at a town with a Fort is a siege on the
  36-wide field with walls, gate and towers; the Fort, Citadel and Castle fortifications and the siege Ballistics rules (ADR-063) take effect.
  The rules are CMB-11.
- **Spells re-drafted** (MAG-1: "draft / update the list according to the lore, with the HoMM3, 4, 5 and Olden Era spell books as reference;
  final tuning when there are decent visuals"): [spells.md](../../docs/content/spells.md) now has **17 spells a school** (5 / 5 / 4 / 3 by level,
  102 in all), adventure spells included; the 24 first-draft spells keep their ids (hero specialties name them). Base numbers only: Spell
  Power multiplies them (ADR-072), so the old "a + b·SP" formulas are gone. New tags `holy` and `summon`. The Shamanism and Primal drafts move
  to Chaos (Bloodlust), Nature (Earthquake, now for sieges), the warcries and creature abilities. Creature caster ids map onto the list.
  A guild offers fewer spells than a school has, so which ones it offers is MAG-13.
- **Artifacts re-drafted** (CONT-3: "according to the lore, with the HoMM3, 4, 5 and Olden Era lists"): [artifacts.md](../../docs/content/artifacts.md)
  replaces the 31 HoMM3 placeholders with lore items of the **same slot, tier and effect** (a rename in the table) and adds **23** items with
  new effect kinds (sight, experience, mana, creature health, school orbs, a spell tome, no retreat, surrender cost, war machines,
  Necromancy, growth, immunities): 90 in all, 7 sets ("Ash Court Regalia" renamed "Ashen Court Regalia", the lore's name). Crown of the
  Waning Eye's "best magic skill" is the school with the most skill points, a tie going to the first in ring order from the hero's town.
  `artifacts.csv` keeps the first draft until the roadmap's re-draft items are built.
- **Clients' view** (MP-3, all proposals): the client runs no rules and keeps a partial copy of the state, from the events the host sends
  it; no digest check; what is under the fog stays as last seen; of other players only what the Thieves' Guild and the kingdom overview
  show; both sides see a whole battle, others only its result where they see it; the host's save loads anywhere (multiplayer, single player,
  hot-seat); a client's save is the whole record the host sends (fine on a LAN); a replay is the host's full record at the end.
- **Smooth fog** (the owner's note with MP-3): the fog fades from the visible area to the hidden one, and objects at the edge are drawn
  correctly when only part of them is explored (a town or a mine half under the fog shows its explored part, not a hard tile cut). For
  rendering only: the rules keep their cell-by-cell visibility ([presentation.md](../../docs/design/presentation.md)).
- **Start zones** (MP-4, all proposals): the generator writes each player's start zone (the template zone of its start town) into the map;
  the editor paints one per player (a cell layer, map format bump); the validator warns when zones touch, overlap or miss the player's start
  town; a step into another player's zone is the conflict.

### ADR-100: Artifacts with HoMM names and assembled relics; the arena before 1.0 with LAN; Pandora's Box (2026-10-29)
**Status**: accepted (2026-10-29), the owner's answers to CONT-6, ARN-1, ARN-2 and MAP-35; **nothing implemented** (the artifact list is a
content draft in the docs). New question: CONT-7 (assembled relics, the dropped effects), answered in ADR-108.

- **Artifacts** (CONT-6; changes ADR-099's re-draft, [artifacts.md](../../docs/content/artifacts.md)):
  - **Names and flavour closer to HoMM3 and HoMM4** (a): the 31 HoMM3 placeholders keep their names and ids, except the two gnoll items (Vael
    has no gnolls: Lizard Warrior's Flail, Buckler of the Lizard King). Lore items take a HoMM name where one fits (Badge of Courage; Charm,
    Talisman and Mystic Orb of Mana; Spyglass; Speculum; Pendant of Free Will; Bow of Elven Cherrywood; Bowstring of the Unicorn's Mane; Vial
    of Lifeblood; Dragon Scale Armor; Recanter's Cloak) and keep their own name otherwise. The bow and Necromancy sets become the Bow of the
    Sharpshooter and the Regalia of the Undead King. The table change is now a few renames instead of 31.
  - **New effect kinds** (b), in this order: sight; mana a day and maximum; creature health; elemental orbs (one school's spells +25 %);
    spells known while worn; no spells above level 2; no retreat; Necromancy; growth; immunities. **Not wanted**: experience, surrender
    cost, war machines, so the Novice's Primer, the Statesman's Seal and the Engineer's Toolbelt are dropped. Effects the question did not
    list (no distance penalty, initiative, morale re-rolls, cheaper spells, spell reflection, regeneration, …) are dropped from their items;
    an item that would be too weak without them gets an existing effect instead *(assumed; CONT-7)*.
  - **Strong items** (c): the Unwritten Codex and the Recanter's Cloak (was the Cloak of Silence, a Major) are Relics. The Wellspring
    Chalice, the Chain of the Unyielding and the Banner of the Legion become **assembled relics** (HoMM3's combination artifacts): the
    **Wizard's Well** (Charm, Talisman and Mystic Orb of Mana; full mana each turn), the **Shackles of War** (Collar, Shackle and Chain of War;
    no retreat or surrender for either side) and the **Statue of Legion** (Head, Torso and Legs of Legion; growth). Each piece is an ordinary
    artifact with its own effect and tier. A hero wearing all three may join them; the relic keeps the pieces' effects and adds its own, sits
    in the first piece's slot with the other pieces' slots blocked, and can be taken apart again; it is never found whole, only its pieces
    drop *(HoMM3's rules; the details are CONT-7)*. The Wizard's Well needs one more effect kind, full mana.
  - **Sets** (d): as drafted (7 sets).
  - **95 artifacts**: 25 Minor, 49 Major (the pieces included), 18 Relics, 3 assembled relics. `artifacts.csv` keeps the first draft until the
    roadmap's re-draft items are built.
- **Arena** (ARN-1, ARN-2; ADR-097): the proposals, with two changes:
  - **LAN before 1.0** (ARN-1 b; the proposal said "LAN later"): the arena is played hot-seat, against the computer and over the LAN, all
    before 1.0. Over the LAN each player sees only its own view (MP-3), so day 1 hides one player's preparation from the other whatever the
    map; day 1 is played turn by turn, or simultaneously on an arena map with start zones (MP-2), as on any map.
  - **Three shipped arena maps** (ARN-2 d; ARN-1 a proposed two), hand-made and ready when the mode ships; **no arena template** for the
    generator.
  - The rest as proposed. ARN-1: before 1.0, after the Phase 3 exit (a); two players, or two teams each fighting with its strongest hero (b);
    the map keeps the players apart on day 1 and the validator warns when one player's heroes can reach the other's area (c); preparation as
    HotA (unlimited movement and building) with the map's starting resources, one day (d); HotA's hero choice (e); the fog as usual, the
    battle screen shows both armies (f); the battle on the combat place's terrain, no experience, retreat and surrender allowed (the player who
    leaves loses), the AI never leaves, auto and quick combat as usual (g); the winner wins the game, a draw or no hero is a loss for both,
    "Play again" and "Save replay", not ranked (h). ARN-2: an "Arena" switch on the editor's conditions tab and the combat place object, map
    format bump (a); no special objects (b); shown on day 1: Pandora's Boxes, quests, Witch Huts' skills, Shrines' spells (c); validator: an
    error without a combat place, warnings when the players can meet on day 1 and for map events that run only on later days (e).
- **Pandora's Box** (MAP-35, all proposals): HoMM3's contents (gold, the other resources, experience, mana, morale and luck until the next
  battle, primary and secondary skills, artifacts, creatures; spells with Phase 5), **gains only** (losses come with the map events); an
  optional **guard army** fought after "open?", the box opening only when the hero wins, and an optional message; a box without contents is a
  validator error, the generator fills its own; the contents are **hidden** from the players until opened, the AI (which knows them) weighs
  them against the guards; random maps place boxes by value band in `TreasureSites` (gold, experience, creatures; spells after Phase 5); a box
  may run a hero event instead once the map events exist (EVT).

### ADR-113: The Godot check of what was built but never run: sieges, garrison heroes, dwellings and caravans, generated connections (ADR-085, ADR-109, ADR-112, 2026-11-02)
Four new smoke scenarios in `tools/smoke-godot.ps1` (run in Godot 4.7.2; frames under `.shots/`), their maps checked by `SmokeMapTests`:
`game-siege` (`tools/smoke/siege.ewmap.json`), `game-garrison-hero` (the towns map), `game-dwelling-caravan` (`tools/smoke/caravan.ewmap.json`)
and `editor-generated-connections` (a map generated from `tools/smoke/connections.ewtemplate.json` by the new `sim generate`).

- **Found and fixed**:
  - The battle view queued later steps onto a tween that had already started (Godot refuses: "Can't append to a Tween that has started"), so
    quick combat pressed during an animation, or any event a frame after the queue began, lost its end state. Steps now wait for the next tween
    when the current one has played a frame, and quick combat applies everything queued at once (`BattleView3D.Step`).
  - Keymasters' tents, border guards and gates (and the Magi's Eyes and Windows) were all drawn in one yellow; they now take their object colour
    (`Palette.ForColour`).
  - The caravan queue named the source "Creature dwelling"; it now says "Footman dwelling at 12,1" like the hire rows.
- **Tooling**: `sim generate <template> --out <file>` saves a generated map with human players; the editor opens `--map=<file>`; `AutoInput`
  takes `hold:Key,frames` (held keys, for camera panning) and `wheel:up|down,count`.
- **Seen working**: the siege field (wall sections, gate, moat, towers and keep off the field, rubble after a hit), the ballista aiming at a wall,
  quick combat and the result, the captured town; entering and leaving the garrison from the town screen; the dwelling panel, the caravan
  section (hire, queue, arrival in the garrison a day later), the mage guild's spells and the tavern on the town screen; garrisons, border posts,
  keymasters and portals on a generated map. Still not seen: the town's own look on the siege field (there is none yet), the spell book button.
- Walls between generated zones are flat rock terrain and hard to tell from the ground; left for the terrain look-dev (Phase 5).

### ADR-114: Phase 3 close-out: spell buildings, the town screen's tree, the tavern screen, specialties, inspector rows, Ember March (ADR-072, ADR-083, ADR-093, ADR-107, 2026-11-02)
**Status**: implemented (2026-11-02). Numbers marked *(proposal)* are the agent's, for the owner to confirm or change.

- **Spell buildings** (ADR-072, MAG-12): every ring town has two `school-<town>` rows in `buildings.csv`, one per ring neighbour's school
  (Sylvan: Inferno, Haven; …), 2000 gold + 5 of the school's resource, needing mage guild 2. *(Choice)* "The school's resource" is the rare
  resource of the school's town (Light crystal, Shadow sulfur, Nature, Arcana, Death and Chaos mercury). Each adds one spell of its school per
  spell level; a guild of level L teaches the first L (`MagicRules.Offered(state, town)`). *(Choice)* The spells are picked from a shuffle
  seeded by the game seed, the town and the school (`MagicRules.SchoolSpells`, like the tavern pool), so existing games' random numbers and
  replays stay the same and nothing is stored. Building one teaches the visiting hero at once. Stronghold and Swamp Bestiary have none.
- **Skill specialties** (ADR-083's open ones; *(proposal)* numbers): **Tactics** +1 deployment column, +2 from hero level 10 (only with the
  skill); **Blood Rage** the Attack cap +5 % a level (Master 8 → 12 at level 10; `HeroBattleBonuses.BloodRagePercent`); **Warcry** the
  warcries' strength +5 % a level (Intimidate's initiative cut and Terror's chance; Terror never above 60 %; `WarcryPercent`); **Armor**, **War
  Tempo**, **Treasury** (gold, not the interest) and **Scouting** +5 % a level, rounded down, like Melee and Archery.
- **Flat specialties** (ADR-093, ADR-107): `skill <id> <amount>` in `heroes.csv`, only for Logistics (+N % movement even without the skill,
  no levels) and Death attunement (+N Necromancy points with the skill, `NecromancyRules.HeroBonus`; its mana is not scaled).
- **The roster draw** (ADR-107 d, once, seed 107, a scratch script): the 48 resource and 8 Logistics specialists were redrawn into 4 gold
  (four towns), 6 rare (2 each, six towns), 2 Necromancy (Sepulka, the first Necropolis mage with Death attunement, and Corvane, a Necropolis
  warrior) and 2 flat Logistics; the other 42 took their first starting skill with an effect *(choice: a school's Magic skill and Logistics
  are skipped, so per-level Logistics specialists do not come back)*. Names in [hero-roster.md](../../docs/content/hero-roster.md).
  `HeroSpecialty.Describe` says what a specialty does (hero and tavern screens).
- **Town screen**: `BuildingTree.For` (core) gives each building its level, next level, cost after Stewardship, requirements met or not, a
  status (available, complete, built today, needs buildings, cannot afford, refused) and a depth (the longest requirement chain under its
  first level); the town screen draws one column per depth with a tooltip (cost, ✓ / ✗ requirements, income, growth, spells, the refusal) and
  a "Built today: …" line (`Town.LastBuiltName`, kept in memory only). `BuildingTree.DwellingChoices` names, for each unbuilt dwelling, the
  lines building it would close and, when the 7-of-9 rule refuses it, the tiers it would leave out; the dwelling rows warn with "⚠ closes …".
- **Tavern screen** (`TavernPanel`): a card per offer with class, faction (native), specialty and what it does, skills, stats, preferred
  creature, the starter stack or a returning hero's level and army, and the hire button with its refusal; the Thieves' Guild from there.
  Opened from the town screen's "Visit the tavern…" (the game app); the 2D harness keeps the hire buttons only.
- **Editor**: `SetSphinxRiddle` (trimmed texts, three different answers, null for a drawn one) and `SetWitchHutSkills` (none or two
  different known skills, old ids resolved), both undoable; inspector rows for them, a pickup's artifact (or random by its tier) and a hero's
  worn and backpack artifacts.
- **Ember March** (`EmberMarchScenario`): the shipped two-player map, 36 × 30, Haven (a person) against Inferno (the computer); a rock ridge
  with a middle pass on the road (3 rows, all in 24 wolves' zone of control) and a northern pass (8 trolls, a Major artifact); per side a gold,
  lumber and ore mine, a guarded rare mine, a tier-2 map dwelling and loot; shared sulfur and crystal mines, a witch hut, a sphinx with the
  author's riddle and two Learning Stones. On the new-game screen after Two Towns; `sim game --map ember`; `sim export <map> --out <file>`
  saves a built-in map as a file.
- **AI fixes found by playing it** (AI against AI): (1) a hero that reached an own town on its way took the garrison only at the next turn's
  start, so the troops called it home every day; it now takes them on arrival; (2) the creatures waiting in a town's dwellings counted as
  troops to collect even when the player could not pay for them; now only what the gold buys counts; (3) a player without heroes hires
  before it builds and saves for a hero instead of building. Results: Two Towns 3 of 3 games to a winner (days 13–37); Ember March 18 of 18
  (days 8–71, 9 wins each); generated `duel` maps still do not finish in 60 days (roadmap).
- **Checked in Godot 4.7.2**: smoke scenarios `game-tavern`, `game-ember-march` and `editor-inspector-objects`; the whole suite passes (24 of
  24). In the tavern scenario the first Escape closes the hover tooltip, as Godot does.
- **Not done**: a person playing Ember March to the end in the app (the Phase 3 exit's last check); the 2D harness has no tavern screen.

### ADR-116: New ideas: prologue, health bars, dividing obstacles, scenery, layout, information windows, icon prompts, missing mechanics (2026-11-02)
**Status**: recorded (2026-11-02) from the owner's notes in `ideas.md`; questions CAMP-1, UI-3 and CMB-12 with proposals; the two writing tasks
are done.

- **Prologue / tutorial**: a small first campaign map, Haven against neutral creatures and the undead, placeholder lore (CAMP-1; roadmap
  Phase 6, using the map events of ADR-115).
- **Combat**: a health bar on each stack, green over 50 %, yellow 21–50 %, red 20 % and below (the owner's thresholds; what it measures is
  CMB-12 a); obstacles that split the field partly, like HoMM3's long cliffs (CMB-12 b, c).
- **Random maps**: more scenery (trees, rocks) so maps feel lived in; the scenery objects of ADR-077 exist, the generator places more (no
  question).
- **UI**: a spacing pass (placement, not fonts or theme), a configurable layout (initiative bar, minimap corner, hero and town lists, action
  panel), information windows for skills, spells, artifacts and the rest, and an advanced tooltip mode (UI-3).
- **Icon prompts**: [icon-prompts.md](../../docs/content/prompts/icon-prompts.md): the format (512 px source, 32 px readable, transparent PNG), a style block,
  lore colours, and prompts for resources, stats, skills, schools, war machines, map and battle actions and buttons.
- **Missing mechanics**: [missing-mechanics.md](../../docs/design/missing-mechanics.md) lists what HoMM3 with HotA and HoMM5 have and we do not, with
  where each stands (dropped, planned, partly, missing) and candidates for next.

### ADR-118: Answers to CAMP-1, UI-3 and CMB-12 (ADR-116, 2026-11-03)
**Status**: accepted (2026-11-03), the owner's answers in `open-questions.md`. Nothing implemented; the roadmap items lose their ❓.

- **CAMP-1, the prologue** ("use the proposals"): (a) the campaign's first mission, playable on its own from the main menu as "Prologue"
  before the campaign screen exists; (b) it teaches through messages from the map events (needs EVT step 1, ADR-115), with a "Hints"
  setting to turn them off; (c) one map of about two weeks; (d) neutral monsters first, then one computer-held Necropolis town with a small
  hero; (e) Haven scouts the border of the Ashen Court, the undead are a Necropolis vanguard (placeholder lore).
- **UI-3, layout and information windows** ("use the proposals"): (a) a "Layout" tab in the game menu (initiative bar top or bottom,
  minimap corner, hero and town lists vertical or horizontal, the action panel's side), kept in the player's settings, not the game;
  (b) the spacing pass comes first: one set of margins and gaps as theme constants, no font or theme change; (c) information windows
  on right-click (a long press on touch later) for creatures, heroes, skills, spells, artifacts, buildings and map objects; the hover
  tooltip stays short; (d) a "Detailed tooltips" setting, off by default, adds the formulas and numbers.
- **CMB-12, health bars and dividing obstacles**: (a) *the owner's choice, not the proposal*: the bar shows the **top creature's hit
  points** (the wounded one at the top of the stack) against its full health, green over 50 %, yellow 21–50 %, red 20 % and below; the
  count beside it shows the losses; (b) long cliffs or walls of 6–12 cells that split the field partly, always with at least two gaps,
  flyers and teleporters pass over, shooters' cover as for crags (ADR-035); (c) only on highlands, rough, rocky and subterranean ground,
  one battle in three there, never in sieges or creature banks.
- **Numbers to tune**: the owner's list stays as placeholders until play-testing (caravan cost, complexity resources, Stewardship and
  governor percentages, Ballistics, zone guards and `GuardPicker`, building costs, growth buildings, war machines, morale and luck per
  point, deposit prices and the marketplace threshold, the AI's retreat threshold).

### ADR-126: The owner's picks from the missing mechanics: map bans, random placeholders, University and War Academy, spell scrolls, the Grail, the first unique buildings (ADR-116, 2026-11-04)
**Status**: accepted (2026-11-04); map bans and random placeholders built (ADR-133), spell scrolls (ADR-136), the first unique buildings (ADR-138), the University and War Academy (ADR-139), the Grail part 1 (ADR-140; the puzzle map to rework, MAP-36). Owner's notes in `ideas.md`;
points marked *(proposal)* were filled in by the agent and stand unless the owner changes them. More unique buildings: TOWN-12; the Grail
buildings: ADR-130.

- **Map bans** (HoMM3, HotA): the map's rules list banned heroes, artifacts, spells and skills. The tavern pools, artifact rolls (chests,
  banks, Pandora's Boxes, merchants, loot), guild draws, shrines, level-ups, Witch Huts, the University and the War Academy skip them.
  *(proposal)* What the author places by hand (a hero, an artifact, a fixed guild spell) is kept, and the validator warns; a banned skill
  is never offered but a hero that starts with it keeps it; a ban that empties a pool falls back to the full list. Map format bump.
- **Random placeholders** (HoMM3, editor and map format): a **random monster** of tier 1–7 (or any tier) picks a creature of that tier from
  the playable towns' and neutral lines at game start; a **random town** takes its owner's chosen faction, a neutral one a random playable
  faction; a **hero placeholder** becomes a random hero of its owner's faction at game start (*(proposal)* campaigns later put a carried
  hero there, FLOW-1). The draw uses `GameState.Random` at game start, like guild spells.
- **University and War Academy**: adventure-map objects any hero may visit. The **University** teaches the magic schools, the **War
  Academy** the general groups (offense … war machines). Each shows a hero up to **2 new groups** (the group's first skill at Basic) and
  **2 of its skills** in those groups raised one level; a lesson costs **1000 gold + 5 crystal, 5 mercury and 5 sulfur**. Skills already
  learned (or at their maximum, or new groups past the 6-group limit) are shown disabled. *(proposal)* The offers are drawn per hero with
  the level-up weights on its first visit and kept for that hero at that object (walking off and on draws nothing new); the rival school
  can be bought; any number of lessons a visit. Generator values like a Witch Hut's, higher.
- **Spell scrolls**: one artifact per spell (Misc slot, a `spell-scroll` row with the spell as its parameter). *(proposal)* The hero casts
  the spell while carrying it, at its own rank for the school, with a spell book and the normal mana cost; scrolls of spell levels 1–3 come
  from the artifact rolls by level (minor 1, major 2, relic 3), level 4 only placed by hand; never pieces of a relic.
- **Grail** (HoMM3, HoMM5): *(proposal)* the Grail lies buried on free passable ground (set in the editor or rolled at game start, not within
  10 cells of a town); **obelisks** (map objects) each show the player a share of the **puzzle map** around it; a hero **digs** with its
  whole day's movement (only with full movement, not on roads or objects), a hole stays; the Grail is an artifact that cannot be traded or
  sold, and a hero bringing it into one of its owner's towns builds that town's **Grail building** at once (one per town). The generator
  places obelisks per template. Map format bump. Grail building effects: ADR-130.
- **Unique buildings, the first set** (one per town, beside the Necromancy Amplifier). Each hero gets a "once" bonus **once per town**
  (another town of the same kind gives it again); the record stays with the town when it is captured *(proposal)*. The siege bonus goes to
  the defending hero (in the town or its garrison) in a siege of that town. Working names *(proposal)*:
  - **Haven, Stables**: as the map's Stables (+400 movement at once and a day for the rest of the week, once a week per hero), without the
    free Lancer → Champion upgrade.
  - **Stronghold, Proving Ring**: +1 Attack once per hero; +1 Attack to the defending hero in sieges.
  - **Swamp Bestiary, Shell Wall**: +1 Defense once per hero; +1 Defense in sieges.
  - **Academy, Hall of Records**: +1 Knowledge once per hero; +1 Knowledge in sieges.
  - **Inferno, Altar of Brimstone**: +1 Spell Power once per hero; +1 Spell Power in sieges.
  - **Sylvan, Elder Grove**: +1000 experience once per hero; +1 luck in sieges.
  - **Dungeon, Mana Spring**: mana +75 % of the hero's maximum, may go above it until spent (like the Mana Vortex), once a week per hero;
    +1 morale in sieges.
  - **Necropolis**: the Necromancy Amplifier (ADR-069).
  - *(proposal)* Cost 2000 gold, 5 ore and 5 of the town's rare resource; needs Fort 1 (Stables: town hall 2). `buildings.csv` rows per town.

### ADR-148: The Godot check of what was built but never run: magic, sieges, the Grail, unique buildings, map sites, game flow (ADR-113, 2026-11-09)
**Status**: done; closes the "Open checks in Godot" list of Phase 3 apart from the items under "Still not seen", which ADR-235 saw.

Thirteen new smoke scenarios in `tools/smoke-godot.ps1` (run in Godot 4.7.2; frames under `.shots/`), their maps checked by `SmokeMapTests`:
`game-unique-buildings` and `game-stables-market` (`tools/smoke/unique-buildings.ewmap.json`), `game-map-sites` and `game-map-sites-red`
(`sites.ewmap.json`: ten sites in a row, a typed hero under each, the Sphinx's riddle fixed in the map), `game-grail` (`grail.ewmap.json`),
`game-magic` and `game-battle-magic` (`magic.ewmap.json`), `game-necromancy` (`necromancy.ewmap.json`), `game-siege-gate` (the siege map),
`game-siege-earthquake` (`siege-magic.ewmap.json`), `game-victory`, `editor-grail-and-town`, `editor-template-obelisks`.

- **Found and fixed**:
  - The battle HUD's log and action buttons sat off-screen in every battle: the HUD was anchored without its offsets. It now fills the screen
    (`SetAnchorsAndOffsetsPreset`), the actions box grows up and left above the menu button, and the button, warcry and spell rows wrap.
  - The University's panel was empty on a hero's first visit: it opened before the lessons were drawn. `ObjectServicePanel` now refreshes on
    `LessonsDrawn`.
  - The town screen did not come back after the Elder Grove's level-up; `TownPanel` refreshes on `DecisionResolved`.
  - Unique buildings and the Sphinx said nothing: the notice panel now tells what a unique building gave (`UniqueBuildingVisited`) and the
    Sphinx's verdict (`SphinxAnswered`); the Grail building notice names the town.
  - The hero list did not update movement after the Stables, a dug hole or the Sphinx; it refreshes on those events.
  - The puzzle map's parchment stretched to the window's width; it stays square.
  - The editor's Grail hint wrapped one letter per line (no minimum width), and the conditions panel's difficulty row showed nothing for
    "not set" (its id was -1, which an `OptionButton` treats as no item).
  - The game's status line printed raw enum names ("GuardEncountered", a `MovementOutcome` record); `Main.Describe` puts them in words.
- **Tooling**: `AutoInput` takes `press:<text>` (clicks the topmost visible, enabled button with that text or tooltip, exact then
  case-insensitive contains, scrolling it into view) and `dump` (prints every visible button with its centre).
- **Seen working**: the Undead Transformer section (choices and Transform); the Elder Grove and Stables with their notices; the trading screen;
  the University's and War Academy's lessons, the Arena's choice, Pandora's Box, the Witch Hut, the Sphinx, the Sacrificial Altar, the Artifact
  Merchant, the Refugee Camp and the Cartographer; the obelisk notice, the puzzle map (P), digging (G) both empty and on the Grail, the Grail
  building notice; spell ranks on the hero screen, the "Assemble" button of a relic, the adventure spells (Detect Magic's and Distant Vision's
  windows, View Terrain); the guild's spell book purchase and the guild spells fixed by the map; in battle Bless and Blind cast, the AI's
  Dispel, surrender, the deployment step with the tent and cart, the Necromancy screen; in a siege the ballista breaking the gate, the keep
  and towers shooting, Earthquake on units and walls; the victory screen with its score and "Save replay", the main menu; in the editor the
  conditions panel's hero limit and difficulty, the Grail tool, the town inspector's faction and guild spells, the template editor's obelisk
  count; the new-game screen's user templates.
- **Still not seen**: Resurrection; Grave Whisper, Ledger of Ages and Eavesdrop; the AI's buffs, curses and revives (only its Dispel came up);
  setting a guild spell by hand in the editor; the drawing of a dug hole on the map.
- An Escape pressed within about 0.6 s of a scripted click did not close the intel popup once; not tried by hand yet, possibly the scripted input.

### ADR-175: Closing the code review (ADR-165 … ADR-174, 2026-11-11)
**Status**: implemented (2026-11-11); tested. The last items of the code quality review, which is archived in
`docs/project/archive/code-review-2026-11.md`.

- **Tests through commands** (ADR-036, R4): 41 of the 55 direct service calls in the tests now go through `Execute`. `SessionCommands`
  gained `Visit` (walk onto an object's entrance), `Revisit` (step off and back on), `PlayUntilDay` / `NextDay` (instead of setting the
  date) and `TakeLevelUps`; looting, a map event's scroll, level-up offers and battle statuses come from real battles, events and visits.
  Left *(choice)*: 14 calls that test an internal rule no command reaches without changing what is checked: `TryCollect` on a made-up
  weekly object, the stacking of made-up statuses, end-of-day statuses (only adventure spells grant them), and `Artifacts.Draw` (the relic
  pool's no-repeat rule, scroll odds over 2000 draws). `SecondCatalogueTests` is `ManaSiteAndPowerUpTests`.
- **The game's tables everywhere** (B2, R10): Demo Valley, Two Towns and Ember March place objects from the content's object table (and Demo
  Valley checks terrain against its terrain table); the template editor validates against the game's tables (`TemplateEditorSession`
  takes `ContentCatalogs`; the map editor passes `GameContent.All`); a creature caster's spell and a stack's spells are named from the
  game's spell table in the info sheets (`InfoSheets.Capability(capability, spells)`), and the hero panel's artifact bonuses and lent
  spells from the game's.
- **Battle HUD** (R7): the two-click prompt asks `SpellEffect.SecondIsStack` instead of checking for `SpellEffectKind.Move`; no front-end
  reads an effect kind.
- **AI fight bound** (section 4): a target choice plays out at most 40 fights (`AdventureAi.FightsChecked`), unsafe ones included, beside
  the 10 safe candidates it checks. Before, a hero ringed by monsters too strong for it simulated every one. The bound is above what
  ordinary choices reach: the AI games replay the same.
- **Left as chosen** in ADR-166 … ADR-169: the AI's targets, the validator, the info sheets and the inspector switch on the object type
  (each with a safe default); five hero commands stay unmarked (their services refuse with their own texts); the map editor app loads its
  template folder itself.
- **Checked in Godot** (the UI work of ADR-167, ADR-172, ADR-173 and this one): `tools/smoke-godot.ps1` passes 53 of 55 scenarios;
  `game-turn-timer` and `game-random-map` fail the same way on the commit before the review (a scripted click on a long drop-down
  list opens it under the cursor and the release picks an item).
- **Behaviour**: unchanged with the built-in content (AI games replay the same).

### ADR-226: Closing the refactoring plan (ADR-202 … ADR-224, 2026-11-19)
**Status**: implemented (2026-11-19); tested. The codebase review and refactoring plan is archived in
`docs/project/archive/refactoring-plan-2026-11.md`, with a table of how each phase was checked.

- **Checked**: every plan item against the code, and the diffs of the twelve refactoring commits for bugs (one reviewer per phase). Phases
  0–4 are done as the plan records them, except where noted below; no behaviour change was found in the per-kind handlers, the AI split,
  the `GameHost` split, the battle guard outcomes, the decision steps or the caches of Phases 1–2.
- **Fixed**:
  - A LAN client's tile → hero lookup went stale: `ViewMirror` writes a hero's place by reflection, past `Hero.Position`'s setter, so
    `GameState.HeroAt` kept the old tiles until a hero came or went (clicking one's own hero, route previews, cursors and panels were
    wrong on the client). The mirror now tells the sink (`IViewSink.HeroChanged`), which drops the lookup. `LanGameTests` reproduces it.
  - `GameRecordSerializer.Save` wrote straight into the target since ADR-224, so a record that failed part way (an unlisted command)
    truncated a file already there (`sim record`, `sim game --record`). It now writes a `.part` file beside it and moves it over, deleting
    it on failure. `SaveFileStreamTests` checks it.
  - The three xUnit analyzer warnings; `AdventureAi.Explain`'s comment names `EW_SIM_EXPLAIN` (there is no `--explain`).
- **Corrections to the record**: the AI keeps one cost map, for the last hero asked, not one per hero (ADR-205); the game app's `Main`
  still holds some policy (ADR-222, see Left); `EventsPanel` was not split (ADR-223); `ViewMirror` copies heroes and objects already in the
  view in place, by reflection, beside the sink (ADR-218).
- **Left**: the roadmap's "Code health" section lists them.

### ADR-235: The Godot check of what ADR-148 had not seen: Resurrection, the intel spells, the AI's battle magic, a hand-set guild spell, a dug hole, Meteor Shower's rubble (ADR-148, ADR-177, 2026-11-19)
**Status**: done (2026-11-19); closes ADR-148's "Still not seen" list. Run in Godot 4.7.2 and looked at frame by frame. No rule change.

- **Five new smoke scenarios** on a new map, `tools/smoke/spells.ewmap.json` (`SmokeMapTests` checks it): `game-meteor-rubble`,
  `game-adventure-intel`, `game-ai-spells`, `game-dug-hole`, `editor-guild-spell`.
- **Seen working**:
  - **Meteor Shower III's rubble**: Pyro learns the spell at rank III in a guild that teaches it, then casts it on a wolf stack. Small
    stones lie on the free cells of the area ("leaves rubble on 11 cell(s)").
  - **Grave Whisper**: its window ("the next victory this week raises 10 % more").
  - **Ledger of Ages**: next week's event and the armies of the Red heroes in sight.
  - **Eavesdrop**: a cell of Gloomhold picked on the map opens its buildings, recruits and garrison, read-only. It is a window of text,
    not the town screen.
  - **Resurrection**: cast by a person on archers who had lost men ("regain 195 health, 20 of them now"); a stack with no dead refuses it
    ("has nothing to revive").
  - **The AI's battle magic**: buffs (Bless, Haste), curses (Slow, and with a fuller book Curse and Weakness), and revives (Resurrection on
    its own stacks, several times).
  - **A guild spell set by hand** in the editor's town inspector: the level-1 slot shows the spell picked.
  - **A dug hole** is drawn on the map once its hero steps off: a dark round mark.
- **Fixed**: Ledger of Ages' window ended the week's event with two full stops.
- **Tooling**:
  - `--seed=<n>` makes the new-game screen use that seed, and a smoke entry's `Seed = <n>` passes it, so the scenario's battles repeat
    exactly. Two runs of `game-ai-spells` gave the same battle log. Only the new scenarios set one; the others still draw a seed (the
    roadmap's "fixed seeds for the smoke games").
  - During a scripted run the battle log's lines also go to `godot.log` (`battle log: …`), so what a battle did can be searched for
    instead of read off frames.
