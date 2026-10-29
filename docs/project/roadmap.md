# Roadmap

Each phase ends with something **playable or usable** and a green test suite. Items are small enough to land in one
change each; `❓` marks items that wait for a design answer in [open-questions.md](open-questions.md). Core items come
with tests (CLAUDE.md); UI items are checked with `tools/smoke-godot.ps1` where a smoke scenario exists.

**Where we are (2026-10-28)**: five design rounds are decided; building them started with the groundwork and the quick items. ADR-069
reworks the skills, adds Necromancy and a second object catalogue; ADR-070 answers most of its questions and adds teams 2–8,
the creature size pass, per-town buildings and the Capitol, tavern changes, surrender, the distance penalty, mirrored templates
and the template editor; ADR-071 settles the data tables, fairness metrics, keymasters, growth buildings, spell books and guilds; ADR-072 answers deployment, Necromancy, the skill ids, rivals, taverns and the spell rules; ADR-073 confirms the open assumptions and settles the war machine footprints, caravan reach, base-creature map dwellings, victory conditions and week events; ADR-075 confirms ADR-073's, changes the caravan reach to 7500 movement points and settles the morale sources; ADR-077 adds map scenery, rivers,
flaggable providers with resource caravans, caravan speed and limits, the kingdom overview, the Thieves' Guild and a third object catalogue
(questions MAP-25 … MAP-34, TOWN-9 … TOWN-11, answered in ADR-079 and ADR-080). Phases 0–2 are done (the 3D battle scene ran in Godot 4.7.2, ADR-085). Phase 3 has towns, dwellings,
caravans, heroes, skills, the tavern, artifacts, income, victory and defeat, save/load, trading and the first object catalogue.
Phase 4 has every generator stage in core. **Next up** (in this order): the groundwork is done (data tables, size pass, per-town building column, Capitol), and so are the quick items
(caravan reach and queue, base-creature map dwellings, army morale) and the war machine footprints with deployment. The **skill rework is
done** (ADR-078): new ids and numbers, Scouting, the distance penalty with Marksmanship, Primal Fury for both towns, the attunement table,
mana by level, Necromancy with its screen and the Necromancy Amplifier (the Necromancy screen still needs a run in Godot). Next: the
ADR-070 / ADR-071 items that need no answer (surrender, taverns: done, ADR-081), then the second catalogue (built, ADR-082, except the
Shrines and the bank re-draft). The ADR-077 items are answered and built
(ADR-079, ADR-080): flaggable providers with resource caravans, caravan speed and limit, the Thieves' Guild, rivers, scenery and the third
catalogue (their Godot screens still need a run). The week and month events, the Astrologer's Tower and the kingdom overview are built
(ADR-084). The Godot smoke pass ran (ADR-085), the new-game screen starts random maps from the shipped templates (ADR-086) and the editor generates them
(ADR-087). New ideas (ADR-088): diagonal battle steps, speed settings, a turn timer, auto combat and LAN multiplayer, waiting for CMB-9, CMB-10,
UI-1, FLOW-3, MP-1 and MP-2 (MP-1 and MP-2 answered in ADR-098: LAN before 1.0, MP-3 and MP-4 still open); CMB-9 is built (ADR-089), auto combat too (ADR-090) and the turn timer (ADR-091), and so are the speed settings (ADR-095). More ideas (ADR-092, ADR-093): a victory screen with a ranking, saved
replays and a replay viewer (FLOW-4 and UI-2 answered in ADR-101), and rarer hero specialties, waiting for HERO-8. ADR-099 brings sieges before 1.0
(CMB-11) and re-drafts the spells and artifacts (MAG-13; CONT-6 answered in ADR-100). ADR-073's remaining victory conditions
and deadlines are built (ADR-094). HotA-like map events with quests (ADR-096) wait for EVT-1 … EVT-6. ADR-100 settles Pandora's Box
(MAP-35) and the arena (ARN-1, ARN-2: before 1.0, with LAN, three shipped maps), and gives the artifacts HoMM names and assembled relics (CONT-7).
**Next**: the whole-map fairness check (Phase 4) and a full game against the AI on a
small random map (Phase 3).

## Data and content groundwork (ADR-070, new 2026-10-22)
- [x] One home for the data tables: `*.csv` and the templates in `src/Core/data`, embedded in core; apps read the embedded
      copy, `--creatures=` / `--heroes=` / `--buildings=` / `--artifacts=` / `--artifact-sets=` for designers; `DataTableSchemaTests`
      (DATA-1, ADR-071)
- [x] Creature size pass (ADR-070): 9 of 182 Large (the seven dragons and the Behemoth line), the other 33 Large → Medium;
      `sim towns` re-run (similar spread, tuning stays CONT-2)
- [x] `buildings.csv` town column (TOWN-3, ADR-074): a town's rows replace a shared building or add one only it has; `BuildingCatalog.For`,
      `TownRules.Buildings`; the town screen, editor, validator and AI use the town's list (the Necromancy Amplifier is the first town row)
- [ ] Bank tables re-drafted per faction (2–3 creature types of one faction, old and new banks; MAP-22); the undead Ruins → Crypt
      rename with old maps loading `bank.ruins` is done (ADR-082)

## Phase 0: Foundation ✅
- [x] Three-tier solution, .NET 10, Godot 4.7.2 (`Godot.NET.Sdk`), architecture guard in MSBuild
- [x] Custom pub/sub (`EventBus`) with documented semantics and tests
- [x] Domain skeleton: map, terrain costs, heroes, 8-way movement, players, resources, calendar, turns
- [x] Map file format (`*.ewmap.json`, git-diffable terrain rows) + round-trip tests
- [x] Game: 3D tile map, hero capsules, strategy camera, HUD, keyboard movement, end turn
- [x] Map editor: terrain palette, brush radius, paint by mouse, save/load
- [x] 2D harness: top-down map, click/keyboard movement, live event log
- [x] **Spike** (ADR-043): all three projects run in Godot 4.7.2 .NET; library nodes created in code work;
      `[GlobalClass]` is not registered for referenced assemblies (code-first stays); apps pin `net10.0`
- [x] CI (GitHub Actions): `dotnet build` + `dotnet test` on push/PR (`.github/workflows/ci.yml`)

## Phase 1: Adventure map ✅

### Core (Tier 1) ✅
- [x] Pathfinding: A*/Dijkstra, terrain costs, diagonals, heroes and object cells as blockers,
      visit tiles end a route, gates continue it, fog-aware; per-step **day markers** (`HeroPath`)
- [x] Click-to-move: `MoveTo`/`MoveAlong` re-validate every step, publish one `HeroMoved` per step,
      stop at day boundaries, on decisions, or when the world changed (`Interrupted`)
- [x] Map objects framework: data-driven `MapObjectDefinition` (footprint `B`/`V`/`.`), built-in
      catalog + JSON definition loader for content/mods, visit events
- [x] Objects: resource piles (7), mines (7, ownership + daily income), treasure chest (gold-or-XP
      choice through the new **pending decision** mechanism), subterranean gate (linked teleport)
- [x] Fog of war per player, two modes chosen per game (permanent reveal / shroud + unit refog with
      hysteresis), hero scouting radius 5, owned mines radius 2, `TilesRevealed`/`SightChanged`
- [x] HoMM3 movement rules: cost of tile left, last step, daily points from slowest creature + bonuses
- [x] Six resources (gold, lumber, ore, crystal, mercury, sulfur); map presets up to 256×256; 8 players
- [x] Minimap query (`Minimap.Build`): per-tile explored/terrain/marker/owner for any viewer
- [x] Editor model `MapEditorSession`: terrain painting with strokes, object placement/removal,
      gate linking, mine owners, hero placement, player slots, map properties, **undo/redo**, `MapValidator`
- [x] Two-level maps end to end (domain, pathfinding, map format v2, demo scenario)
- [x] Map format v2 (description + polymorphic objects, still loads v1)

### Core follow-ups ✅
- [x] Meeting heroes (ADR-027): teams in `Player.Team` / map player slots (map default, overridable at
      setup via `GameState.AssignTeams` unless `TeamsLocked`, ADR-032); routes may end on own/visible
      enemy heroes; own → `HeroesMet` + `HeroExchangeDecision` (`CompleteExchange`), enemy →
      `BattleTriggered` (battle, Phase 2), ally → `Occupied`
- [x] Refog (ADR-031): `MoveResult.EnemySpotted` auto-stop; `BattleTriggered.IsAmbush` when bumping into
      an unseen enemy; per-player `ObjectMemory` (last known state) + `KnownObjectsChanged`; minimap
      draws objects from memory
- [x] Terrain: highlands, forest floor, wasteland (codes `h`/`f`/`x`, colours, demo) (ADR-032)
- [x] Gates: `TwoWayGate` kind (subterranean gate + two-way monolith definitions), one-way
      `OneWayGateEntrance` → `OneWayGateExit` (exits are walkable landing spots), directional
      pathfinding, editor `LinkGates` / `SetGateExit` with link-safe removal and undo, validator checks;
      dangling links are dead ends, not crashes (ADR-026)
- [x] Roads: road layer in `AdventureMap`, dirt 50 / gravel 65 / cobblestone 75 road → road costs in
      movement and pathfinding (A* heuristic uses road cost), editor `PaintRoad` with undo, validator
      warning on impassable terrain (ADR-024, ADR-032)
- [x] Terrain definitions as data: `TerrainCatalog` (class, cost, native towns), JSON overrides via
      `TerrainDefinitionSerializer`
- [x] Map format v3: roads, teams + `teamsLocked`, one-way gates; v1/v2 still load; null fields omitted

### Godot (Tier 2/3) ✅ (verified in Godot 4.7.2 with `tools/smoke-godot.ps1`)
- [x] `AdventureWorldView` + `MapViewContext`: terrain and roads, map objects from the viewer's `ObjectMemory`
      (live state in the editor), fog overlay (unexplored black, out of sight dimmed), both levels with a level
      switch (L, button, follows gates), unseen enemy heroes hidden (`CanSee`); hot-seat switches the viewer
- [x] New-game setup screen (`NewGameScreen`): demo, `--map`, editor maps; teams unless locked; fog mode, refog
      margin, seed
- [x] Exchange screen (`ExchangePanel`): both armies, click-to-move stacks, split, close
- [x] Editor: road brush, gate link tool, players with teams and teams-locked
- [x] Mouse picking and hero selection; path preview with day-coloured markers; click twice to move; Tab, arrows
- [x] `HeroView`: one queued tween per `HeroMoved`, `HeroTeleported` jumps in order, selection ring, level/sight
      visibility
- [x] Decision prompts (`DecisionPanel`): treasure chest, guard offers, battle width, quick combat, level-up skills
- [x] Hero list panel (`HeroListPanel`) and minimap (`MinimapWidget`, click to look there)
- [x] Editor UI: tool palette (terrain, road, object, monster, hero, erase, link gates, select), map
      properties, players, inspector, validation panel, two-level maps, Ctrl+Z/Ctrl+Y
- [x] 2D harness: objects from memory, fog, roads, path preview, decision prompt

**Exit** ✅: a hot-seat game on an editor-made map where players race to collect resources. Playable in the game app
(hot-seat, fog, guards, quick-combat battles); final art comes in Phase 5.

## Phase 2: Armies and combat: done (3D battle scene still to be checked in Godot)

### Core (Tier 1) ✅
- [x] Command pipeline (ADR-036): serializable `GameCommand`s through `GameSession.Execute` only (services'
      mutators are internal), command log, `GameRandom` (seeded SplitMix64) in state, replay files
      (`*.ewreplay.json`) verified by `GameStateDigest`
- [x] Headless simulator `tools/EternalWars.Tools.Sim`: `battle`, `lines`, `towns`, `creatures` statistics,
      `record`/`replay`, `selftest` (runs in CI)
- [x] Treasure chests roll HoMM3-style reward tiers from the game RNG; map authors may fix values (ADR-039)
- [x] Creature definitions as data (ADR-037): tier 1–5, base/upgraded, size, battle `Speed` + adventure
      `MapSpeed`, initiative, shots, capabilities, tags; JSON overrides; stacks; hero army (7 slots); exchange
      (`MoveStackCommand`: split, merge, swap, keep one stack)
- [x] Tactical battle (ADR-038) on a 24 × 28–34 cell grid with 2×2 / 3×3 / 4×4 footprints: automatic
      deployment, rolled obstacles with cover classes, footprint-aware movement (clearance, no corner
      squeezing, flyers), initiative order with HoMM3 wait/ties, defend, melee/ranged, retaliation,
      engagement and hit-history hooks (`PackHunter`, `Relentless`), body-blocking, cumulative cover in
      eighths with the > 7/8 no-shot rule (ADR-035), breath with friendly fire, cleave, multi-head, HoMM3
      damage formula, Tactics width choice, victory/defeat, retreat, casualties and experience
- [x] Deterministic battle AI for neutrals and computer players; quick combat for either side
- [x] Army speed drives `MovementAllowance`
- [x] Neutral monsters with two guard modes per stack (HoMM3 zone of control with stealing, or a guarded
      3×3 area for treasure guards; ADR-065), HoMM3 dispositions (join / flee / fight, ADR-040), counts rolled by tier; optional mine guards;
      ambush battles (ADR-031); hero vs hero battles with hot-seat control of both sides
- [x] **Haven and Inferno** draft rosters (9 lines each, 5 tiers, all upgradeable) + 5 neutrals
- [x] **All eight town rosters**, Conflux, Dragon Utopia and lore neutrals from the sandbox, first balance pass with
      `sim lines` / `sim towns` / `sim creatures` (ADR-044)
- [x] Sandbox terrain model (ADR-045): every terrain penalises non-natives, several natives per town, beach / ice /
      shallow water / cursed lands / rocky ground, map format v5
- [x] Creature table `data/creatures.csv` as the single source of stats: embedded in core, read by the apps at
      start-up so it can be edited without a rebuild or re-import (ADR-044)
- [x] HoMM4-style creature capabilities, at most 5 per creature (ADR-042): first strike, ranged retaliation,
      strikes/shoots twice, opt-in melee penalty, unlimited/no retaliation, area attacks, engagement hooks;
      spell immunity, resistance and casting as data + queries until magic (Phase 5)
- [x] Roster revision from the design notes (ADR-049): Dragon Utopia wyvern line, Efreet and Golden Golem neutrals,
      melee Dark/Grim Raiders, non-dragon Shadow Horror, spell resistance for the elemental dragons
- [x] Map format v4 (armies, hero stats, monsters, mine guards, guard mode, optional chest values) and editor
      commands for all of it with undo; validator checks

### Godot (Tier 2/3)
- [x] 2D harness: battle board (`BattleBoard2D`) with click-to-move/attack/shoot, wait/defend/retreat/quick
      keys, width choice and quick combat; verified in Godot 4.7.2 (smoke scenario `harness-guard-battle`)
- [x] Editor UI for armies, hero stats, monsters, mine guards, guard mode and chest values (`InspectorPanel`,
      `MapPropertiesPanel`)
- [x] 3D battle scene in the game app (ADR-052), placeholder meshes; verified in Godot 4.7.2 (smoke scenario
      `game-battle-3d`, ADR-085)
  - [x] Battlefield scene (`BattleView3D`): ground in the battle terrain's adventure-map texture, cell grid, obstacles
        (boulders, crags), own camera with zoom; the adventure map and its HUD are hidden meanwhile
  - [x] Unit views (`BattleUnitView`): footprint base in the side's colour, body shaped by role (walker, shooter,
        flyer), facing, stack count, active-unit ring
  - [x] Input: reachable-cell overlay, hover preview, move / attack from the side the pointer is on / shoot
        (`BattleTargeting` in core, shared with the 2D harness), wait, defend, retreat, quick combat
  - [x] Initiative timeline (active unit, queue, waiting units), unit stats on hover, combat log (`BattleHud`)
  - [x] Animations queued per battle event (move, melee lunge, projectile, hit flash, damage text, death), so AI turns
        play in order; quick combat and all-AI battles skip them
  - [x] Result screen: winner, casualties per side, experience; back to the map with Enter
  - [x] Smoke scenario `game-battle-3d` (`tools/smoke/battle.ewmap.json`, checked by `SmokeMapTests`)
- [ ] Battle scene follow-ups: creature models and animations (Phase 5 art), terrain-themed obstacles and
      backdrops, flyer arcs, battle music; the deployment step is in (ADR-076), drag and drop instead of click-to-place later

**Exit**: heroes fight neutral stacks and each other; battles are replayable from logs. Met in core (`sim selftest`,
replay tests) and in the game app's 3D battle scene; it still has to be checked in Godot.

## Phase 3: Towns and progression

### Towns: core ✅ (ADR-030, ADR-046, ADR-050)
- [x] Town map object: 7×7 with the corners free, entrance bottom-middle; one `town.<type>` per playable town
- [x] Building table `data/buildings.csv` (`BuildingCatalog`): levels, costs, requirements, income; `--buildings=`
- [x] New towns start with town hall 1, fort 1 and tavern 1
- [x] Build one building per day (requirements and cost checked)
- [x] Dwellings: nine creature lines per town, build 7 with every tier covered, upgrade to the upgraded creature
- [x] Weekly growth (new dwelling opens with one week), accumulation, recruiting to the garrison or the visiting hero
- [x] Stack upgrades for the price difference; garrison ↔ visiting hero stacks
- [x] Capture: empty towns flip, garrisons fight as guards, owned towns give sight (radius 5)
- [x] Town income (`TownIncome`): town hall 500 / 1000 / 2000 gold, resource silo +1 lumber +1 ore
- [x] Map format v6 (`town` objects); editor places towns and edits owner, name, buildings, dwellings, garrison
- [x] Editor enforces building requirements; validator flags broken requirements and the 7-of-9 rule

### Towns: building effects (ADR-066, ADR-067, ADR-070)
- [x] Fort: unlocks the dwellings; Citadel +50 % / Castle +100 % town growth, rounded down (ADR-066); fortifications wait
      for sieges (before 1.0 since ADR-099)
- [x] Guard mode per monster stack: map format v13, monster inspector control, old maps converted (ADR-065)
- [x] Marketplace trading: gold against one resource, rates by `min(X + 1, 10)` with X the marketplace towns, governed ones 1.5; needs a town marketplace; `TradeResourceCommand`, trading screen (HUD button, town screen; Godot part not run in the editor yet); no AI trading (ADR-067); nothing open (ADR-070)
- [x] Map marketplace object: a visiting hero trades at the fixed X = 4 rate; map format v14, editor placement, validator; the generator places it as a treasure item where the zone's treasure value allows (ADR-067)
- [x] Blacksmith: sells the healing tent, the ammo cart and a lost offensive machine to the visiting hero (ADR-056)
- [ ] Mage guild: spells per level (waits for magic, Phase 5); until then cost and requirements only; refills mana; sells the
      spell book (500 gold) (ADR-071)
- [ ] Growth buildings (placeholder, ADR-071): +5 a week for tier 1, +2 for tier 2, per town in `buildings.csv`; the AI builds them
- [ ] Stronghold and Swamp Bestiary: a 3-level building teaching warcries / spiritual totems instead of a guild (placeholder, ADR-071)
- [ ] Two spell buildings per town, one per ring neighbour's school, 1 spell per guild level each (2000 gold + 5 of the school's resource,
      needs guild 2; ADR-072)
- [x] Capitol (town hall 4, ADR-070, ADR-074): Village / Town / City Hall / Capitol; one per player (build refused, captured one drops to
      City Hall, `TownBuildingDowngraded`, validator error); placeholder 15 000 gold + 25 lumber + 25 ore, needs the Castle, 4000 a day; the AI
      builds it last
- [ ] Per-town names, costs and requirements filled in `buildings.csv` (the column exists, ADR-074); every town's unique
      building comes later
- [x] Dwellings on the adventure map (ADR-054): `CreatureDwelling`, weekly growth, flagging, recruiting by a visiting hero
      (`RecruitAtDwellingCommand`); map format v12; editor placement and `SetDwelling`; validator
- [x] Caravan building (ADR-054): `OrderCaravanCommand` hires from owned map dwellings within reach (5 days at first, ADR-075 below) along an open
      route, `SendTroopsCommand` moves garrison stacks between own towns; creatures arrive in the garrison (`CaravanArrived`)
- [x] Caravan rules (user decisions): a blocked route returns the caravan with the price back, a lost destination scatters the
      creatures, caravans are invisible; building 1000 gold + 5 lumber (placeholder)
- [x] Town screen: caravan section (hire, send the picked stack, caravans on the way); `DwellingPanel` opens when a hero visits a dwelling
- [x] AI: heroes flag and recruit at map dwellings, towns build a caravan once the player owns a dwelling, hire from owned dwellings and send troops to an empty town (`AdventureAi`)
- [x] Caravan reach (ADR-073, ADR-075): 7500 movement points at the plain step cost (terrain and speed ignored); the town lists dwellings
      within the raw reach (hostile blockers ignored, `CaravanRules.Dwellings`), ordering needs an open route within it; the AI builds and
      hires within reach (travel days by the map speed until ADR-079)
- [x] Caravan queue on the town screen: caravans coming here, on their way (fewest days left first) then waiting at a full garrison
      (`CaravanRules.Queue`), and those sent from here (ADR-073; Godot part not run yet)
- [x] Map dwellings sell base creatures only: the editor offers and accepts base creatures (`EditResult.UpgradedCreature`), the validator
      rejects upgrades, older maps load the line's base creature (ADR-073, ADR-075)
- [ ] Smoke scenario for dwellings and caravans (needs Godot); caravan cost tuning; dwelling guards are
      in the objects block below (ADR-068)
- [x] Caravan speed (ADR-079, TOWN-10): 2500 movement points a day at the plain step cost for every caravan (3 days at most)
- [x] Caravan limit (ADR-079, TOWN-10): 6 caravans coming to a town at once, 8 with a governor, refused at the limit; the caravan section
      shows the count
- [x] Flaggable resource providers and resource caravans (ADR-079, TOWN-9): all four flagged like mines, storing a week's output every week;
      own visits and `FetchResourcesCommand` (100 gold) collect it; map format v22; the AI flags them and fetches large stores
- [x] Kingdom overview screen (ADR-077, ADR-084): every income source with its amount and the totals, from the income rules
      (`KingdomOverviews.For`; `KingdomOverviewPanel` from the HUD or K; smoke scenario `game-kingdom`, ADR-085)

### Towns: Godot
- [x] 2D harness town panel (`TownPanel`, `--scenario=towns`): build, dwellings, recruit, upgrade, garrison
- [x] Towns on the 3D map with flag and name; editor inspector for towns
- [x] Town screen in the game app: the 2D `TownPanel` as a full-screen panel over the map (ADR-050), opened by
      visiting, from the town list or with T; shows the governor (appoint / dismiss)
- [x] Town list in the HUD next to the hero list; select and centre on a town (`TownListPanel`)
- [ ] Town screen polish: building tree with requirements, cost tooltips, "built today" state, dwelling choice
      warnings (which tier would become unreachable)
- [x] Smoke scenario for the town screen (`game-town-screen`, `tools/smoke/towns.ewmap.json`; run in Godot 4.7.2, ADR-085)

### Heroes and skills
- [x] Hero roster `data/heroes.csv`: 16 per town, mage/warrior, preferred creature, specialty (ADR-048)
- [ ] Rarer resource specialties (4 gold, 6 rare resources in the roster), 2 Necromancy (+5 %) and 2 Logistics (+10 %) specialists with flat
      bonuses (ADR-093) ❓ HERO-8
- [x] Skill groups, levels, level-ups with three offers and rival-school odds (`LevelUpDecision`)
- [x] Skill effects without towns or magic: melee, archery, armor, treasury, prospecting, logistics, pathfinding,
      troop management, drill, Tactics width
- [x] Native terrain only for all-native armies; Pathfinding −25/50/75 % of the penalty; routes and steps share one
      cost rule (ADR-024, ADR-032, ADR-045)
- [x] Primary stat growth (ADR-055): one point per level, HoMM3-style odds by class (draft), shown in the level-up
      prompt; Spell Power and Knowledge added (roster columns, map format v8, editor), effects with magic
- [x] Stewardship and governors (ADR-063): −5/10/15 % buildings, −10/15/20 % creatures in a town with a governor
- [x] Stewardship for map dwellings (the hero's level) and caravans (the town's governor); governor UI in the town screen
- [x] Governed towns count 1.5 in the marketplace rates, replacing the 150 % value (ADR-067)
- [x] Treasury and Prospecting rework (ADR-062)
- [x] Mana numbers and school attunement skills (ADR-063); casting waits for Phase 5
- [x] Primal Fury (Stronghold, ADR-067): Blood Rage and Warcry in battle (BattleHud buttons, AI uses them); the faction spell resistance is data until casting (Phase 5)
- [x] Complexity levels with starting resources (ADR-063); new-game screen option
- [x] AI that appoints governors on purpose (ADR-059)
- [x] The `sim` tool picks a complexity (`sim game --complexity`, ADR-083)
- [ ] Siege Ballistics: use the `WarMachines` rules once fortifications exist (auto-fire, control, second shot); needs
      sieges (ADR-099, CMB-11)
- [x] Skill specialties: the skill's effect +5 % per hero level (ADR-072, ADR-083: Melee, Archery, Logistics, attunement mana)
- [ ] Skill specialties for Tactics, Blood Rage and Warcry (numbers not set) and Necromancy (ADR-083)
- [x] Rival schools: the arch-rival across the ring only, one per town (ADR-072, HERO-3; ADR-078)
- [x] Leadership (`offense.third`, +1/2/3 morale) and Alertness (`defense.third`, +5/10/15 % initiative, working title) (ADR-067)
- [x] Deployment (ADR-072, ADR-076): `BattlePhase.Deploying`, the first 4 / 5 / 6 columns from the own edge by the deployer's Tactics; only
      the higher Tactics deploys, and only a human side; heroes and neutrals, not banks; the zone includes the machine column and has no
      obstacles; `BattleDeployCommand` / `BattleStartCommand`; click a stack, then a cell, then "Start battle" (3D scene and 2D harness; Godot
      part not run yet)
- [x] War machine footprints (ADR-073, ADR-076): one column at the outer edge, offensive machine and tent 1 × 3, ammo cart 1 × 2, stacks from
      the next column; `BattleUnit.Width` × `Height`; the 3D models overhang the grid's edge (not run in Godot yet)

#### Skill rework (ADR-069, decided 2026-10-20; built 2026-10-26, ADR-078)
- [x] New numbers: Melee 9/16/23/30 %, Archery 15/25/35 %, Pathfinding to Master (−100 %), Logistics to Expert (30 %), Alertness flat +1/2/3
      initiative, Tactics width from Advanced (30/32) and Expert (28–34)
- [x] Regrouping and new ids: Leadership to Defense (`defense.leadership`), Alertness to Tactics (`tactics.alert`), Drill → War Tempo
      (`tactics.wartempo`), Deployment merged into Tactics, Troop management dropped; old ids are read as their successors, no map format bump (ADR-072)
- [x] Scouting (`pathfinding.scouting`): +2/4/6 sight radius, revealed at once when learned
- [x] Distance penalty for ranged attacks (ADR-070: 1/8 per whole 10 cells, adds to cover, replaces the 15-cell half
      damage, machines exempt) and Marksmanship (`offense.marksmanship`, −2/8 / −4/8 / −6/8); order: add distance and cover,
      subtract Marksmanship, then the 7/8 check (ADR-072)
- [x] Primal Fury for Swamp Bestiary, Adrenaline removed; Interference (`rage.third`) with the spell-cost part (data until Phase 5); Interference
      +1 / +2 / +3 mana; mages start with the group, warriors are offered it often; Stronghold warriors start with Offense, Swamp
      warriors with Defense; their mages level up with even odds 25 / 25 / 25 / 25 % (ADR-073; names CONT-5)
- [ ] Spell Power strengthens Blood Rage and warcries for Primal Fury mages (ADR-072, MAG-10; numbers not set)
- [x] Attunement table of ADR-069 and mana by level (+10 × ⌈level / 10⌉) in `ManaRules` (maximum only, ADR-072)
- [x] Necromancy (Death attunement): raise undead from the slain enemies' hit points after victories (ADR-072 rules), its screen
      between the battle result and level-ups (`UndeadRaised`; not run in Godot yet); computer heroes take it whenever offered
- [x] Necropolis Necromancy Amplifier, the first town-specific building (1500 gold + 5 mercury, mage guild 1, ADR-070);
      needs the per-town table and Necromancy
- [x] Docs: `heroes.csv` and [hero-roster.md](../content/hero-roster.md) follow (Drill, Troop management → Scouting, Adrenaline names,
      warriors' Offense / Defense starting skills)
- [x] Hero screen (Godot): stats, mana, skills with descriptions, specialty, army, paper doll, war machines; opened from the hero list (`HeroPanel`, H or "Info"; smoke scenario `game-hero-screen`, run in Godot 4.7.2, ADR-085)

### Tavern
- [x] Hire heroes (ADR-060): per-player weekly pool by ranks (native, adjacent, not opposing, opposing; warriors for
      Stronghold and Swamp Bestiary), 2 heroes basic and 4 with the upgraded tavern (Brotherhood), 2500 gold, hero on the
      entrance (`HireHeroCommand`), at most 8 heroes (or the map's limit, ADR-068)
- [x] Tavern as an upgradeable building (`tavern` level 2 in `buildings.csv`)
- [x] Heroes that retreat or are defeated return to the pool (offers exclude only heroes on the map)
- [x] The AI hires heroes
- [x] Surrender in battle (ADR-070, ADR-081): not against neutrals, half the gold cost of the survivors paid to the enemy; retreated and
      surrendered heroes wait in their player's taverns as they were (the army only after a surrender); battle HUD button (not run in
      Godot yet)
- [x] The battle AI retreats when its army is worth 20 % of the enemy's or less (ADR-081; threshold: tuning)
- [x] Tavern pools per player and faction (ADR-070, ADR-081): same-faction towns share the offers; basic 1 native + 1 adjacent, the
      Brotherhood 2 native + 2 adjacent
- [ ] Hero limit: prisoners and waiting heroes do not count (done); heroes in a town garrison wait for garrison heroes, which do not exist
      yet (ADR-072, ADR-081)
- [x] Brotherhood price 1000 gold + 5 ore; hired heroes get a starter stack: a week's growth of their preferred creature (ADR-070, ADR-081)
- [x] Hire buttons in the town panel (`TownPanel`)
- [ ] Tavern screen in the game app with hero details (waits for the town screen)
- [x] Thieves' Guild in the tavern (ADR-079, TOWN-11): `ThievesGuild` ranks by towns, heroes, gold, lumber and ore, rare resources,
      artifacts, income, army strength, best hero, best creature; rows by the number of taverns; `ThievesGuildPanel` from the town screen
      (smoke scenario `game-thieves-guild`, ADR-085)

### Artifacts
Content draft: [artifacts.md](../content/artifacts.md).

- [x] Artifact tables `data/artifacts.csv` and `data/artifact_sets.csv` (ADR-061): 67 artifacts, 4 sets, embedded in core
- [x] Hero paper-doll slots and a backpack of 50; equip / unequip / give commands
- [x] Stat effects: Attack, Defense, Spell Power, Knowledge, morale, luck, movement, battle speed, damage, skill levels
- [x] Income artifacts (daily resources through `HeroIncome`)
- [x] Terrain artifacts: native terrain and terrain penalty reduction (ADR-045)
- [x] Artifact pickups on the map (rolled by tier at game start, relics unique) and loot from defeated heroes
- [ ] Artifacts in treasure chests (the HoMM3 artifact tier, ADR-039) ❓ CONT-3
- [x] Artifact exchange between own heroes (`GiveArtifactCommand`, while the exchange is open); screen pending
- [x] Map format v11 and editor session calls (`SetHeroArtifacts`, `SetArtifactPickup`); validator
- [x] The AI picks up, values and equips artifacts
- [ ] Artifact effects that wait for other systems ("Not yet" in the table: mana, spell resistance, regeneration, …)
- [ ] Editor: inspector rows for hero artifacts and pickups (Godot)
- [x] Godot: paper-doll and backpack on the hero screen (click to wear / take off, tooltips with effects; no drag and drop, no artifact exchange between heroes yet)

- [ ] Artifact table: the re-draft (ADR-099, ADR-100, [artifacts.md](../content/artifacts.md)): the few renamed rows (the gnoll items, lore
      items with HoMM names; tests follow), the rows whose effects exist, "Ashen Court Regalia"; then the new effect kinds with their rows in
      the owner's order (sight, mana a day and maximum, creature health, no retreat, Necromancy, growth, immunities); the magic ones
      (elemental orbs, spells known while worn, no spells above level 2) with Phase 5
- [ ] Assembled relics (ADR-100): Wizard's Well, Statue of Legion, Shackles of War; joining, blocked slots, taking apart, loot as pieces,
      the AI, the hero screen ❓ CONT-7

### Game flow
- [x] Victory and loss conditions (ADR-057): default (defeat every enemy: no towns and no heroes); map-specific
      conditions (capture a town, defeat a hero, gather resources, survive N days; lose a town, lose a hero, time limit)
      in the map format (v10) and the editor
- [x] Player elimination: `PlayerDefeated`, `GameEnded`, skipped turns, the end of commands
- [x] Grace period for players without towns: 7 days by default, configurable per map (HoMM3, ADR-057)
- [x] Save/load (ADR-058): save files from map + options + command log (`GameRecord`), verified by `GameStateDigest`,
      at any moment including mid-battle
- [x] Score and ranking core (ADR-101, ADR-102, FLOW-4): campaign-like check (a human without a town; the map-events check comes with
      EVT), score = max(10, 200 − days) × difficulty × turn timer factor, 14 rank titles (`ScoreRules`), ranking file (top 20 per table,
      random / shipped + custom) in the user data folder with each row's game record (`RankingFile`); tests
- [ ] The map's difficulty in the editor ("Map default" counts as it, Normal when unset); map format bump (ADR-101)
- [ ] Victory screen with the score, rank title, player name and the highlighted row; "Save replay" on every end screen (ADR-101)
- [ ] Main menu: New game, Load, Multiplayer and Campaign (disabled until built), Watch a replay, Leaderboard, Quit (ADR-101, UI-2)
- [ ] Replay viewer: Replays and Saves tabs; reveal-all or a player's eyes; play, pause, speeds, next turn / day, back to a day; battles
      with "skip to the result"; "Play from here" in battles only (ADR-101, UI-2)
- [x] Autosave: two slots, the start of the current and of the previous turn (ADR-058)
- [ ] Save/load: a state snapshot if replays get too slow
- [x] Game app: victory / defeat screen (`GameResultPanel`)
- [x] Game app: save / load menu (F10, F5 quick save, F9 quick load; also in battle)
- [x] Week and month events (ADR-073, ADR-084): weekly 75 % week of a creature (its line's growth doubles); monthly 45 % month of a creature
      (doubled growth, guards spawn next to random objects), 15 % plague (no growth, town stocks halved), 15 % starvation (no growth);
      announcement at week start (rolled a week ahead); the AI recruits the week's creature first (notice and date: smoke scenario `game-weeks`, ADR-085)
- [x] Event creature factions per map and template (weekly and monthly separately; default playable factions); events on or off and the
      first one after 0 to 8 skipped weeks (default: from the second week, ADR-075); map format v24, template v6, editor conditions panel (ADR-084; smoke scenario `editor-week-events`)
- [x] Players without a town on campaign-like maps (ADR-075): grace period 0 and a "lose a hero" condition (both existed); validator warning
      when a player starts without a town while the grace period is on
- [x] More conditions (ADR-073, ADR-094): capture a map object, obtain an artifact, release a prisoner; lose an artifact; deadlines on
      victory conditions; editor conditions panel; validator; map format v25
- [x] Team victory: a team wins while one of its human players survives; eliminated human teammates share the win (ADR-073, ADR-083)
- [x] Turn timer (ADR-088, ADR-091, FLOW-3): a new-game option, 1 to 30 minutes, human players only, saved with the game; warnings,
      defaults for pending choices, a battle against the computer finished first, stopped in a battle between two humans
- [x] Auto combat (ADR-088, ADR-090, CMB-10): each human player's setting (off / ask / always) in the game menu, saved with the game; a
      preview on a copy of the battle, an outcome screen with Accept (quick combat, same result) or Fight (by hand, nothing re-rolled)

### Arena mode (ADR-097, ADR-100; before 1.0, after the Phase 3 exit)
- [ ] Core: the arena switch in the map (no victory or loss conditions), combat places; day 1 with unlimited movement and building (the
      map's starting resources); the battle at the start of day 2 (HotA's hero choice, heroes moved, no experience, retreat and surrender
      lose), its winner wins the game, a draw is a loss for both; map format bump (ARN-1, ARN-2)
- [ ] Game app: arena maps on the new-game screen, hot-seat and against the computer; the battle starts by itself, both armies shown;
      result screen with "Play again" and "Save replay", not ranked (ARN-1)
- [ ] AI: prepares on day 1 (visits, hires, builds) for the battle; never leaves the battle
- [ ] Editor: the arena switch on the conditions tab, combat places; validator (no combat place, players that can meet on day 1, map
      events that run only later); Pandora's Boxes, quests, Witch Huts and Shrines show their contents on day 1 (ARN-2)
- [ ] Three shipped arena maps, hand-made; no arena template for the generator (ARN-2)
- [ ] Arena over the LAN: see [LAN multiplayer](#lan-multiplayer-adr-088-adr-098-before-10-after-the-phase-3-and-4-exits) (ADR-100)

### Adventure AI (computer players)
- [x] AI driver that plays a computer player's turn through commands only (ADR-036, ADR-059), deterministic (no RNG)
- [x] Economy: build order, saving for the next building, stack upgrades, recruiting, garrison to hero
- [x] Exploration: pick up resources, flag mines, visit chests (value per travel time), explore the unknown
- [x] Fights: attack guards, garrisons and heroes it wins in simulated fights (`BattleEstimator`)
- [x] Capture of undefended and weak enemy towns
- [x] Game app: computer players play their turns (`Main._Process`)
- [x] Simulator: AI-vs-AI games (`sim game`) for tuning; the self-test replays one
- [x] Town defence: keep or bring a hero home when an enemy is near; avoid stronger enemy heroes (ADR-059)
- [x] Several heroes: a main hero; the others carry troops to it and hand them over in the exchange (ADR-059)
- [x] Buy war machines at the blacksmith: tent, cart for shooters, the offensive machine back with Advanced Ballistics (ADR-059)
- [ ] Spells (Phase 5), marketplace use (later; no AI trading for now, ADR-067); hiring heroes, artifacts and complexity levels are done
- [x] AI-vs-AI games on generated maps (`sim game --template`); CI plays two weeks on every shipped template (`sim maps`)
- [ ] Balance the AI over several maps (`sim game --runs`); on generated maps it hardly fights guards yet (heroes still
      level 1 after 40 days on `duel`), and no game finishes in 40 days

### Adventure map objects (ADR-068; [catalogue](../design/adventure-map.md#planned-objects-adr-068))
Each item is one change with core tests (`TestWorld` maps), a map format bump where needed, editor placement through
`MapEditorSession`, a validator check, and a generator hook later. Done in this order; the first item unblocks the rest.
- [x] Shared rules: visit scopes (weekly per object, once per hero, first visitor; vanishing is removal), hero statuses that expire (next
      battle, end of day, end of week), weighted rolls, artifact draw by tier (Minor, Major, Relic; the Relic pool refills when empty,
      duplicates allowed); no object uses them yet
- [x] Resource providers: Water Wheel, the six Warehouses, Windmill, Mystical Garden (map format v16, editor placement, AI visits them;
      warehouse choice in the decision panel; flagged instead since ADR-079)
- [x] Abandoned mines (six, a separate object that becomes the normal mine when the guard falls; Troglodyte guards, subterranean battle terrain for gold
      and ore; ordinary mine objects in the map format, no format change)
- [x] Native-terrain bonuses in battle: +1 Attack, Defense and Speed for creatures of a town native to the battlefield's terrain (neutrals and
      machines never; the battle panel shows it)
- [x] One-time loot: Campfire, Supply Crates, Traveler's Wagon, Corpse, Lean To (map format v17, editor placement, AI picks them up); the new
      Treasure Chest table with a 5 % Minor artifact
- [x] Resource Pile amounts rolled within the notes' ranges when the map leaves them open (map format v21; editor and generator piles leave them open)
- [x] Warrior's Tomb (artifact roll, the −3 morale status for every visitor)
- [x] Temporary buffs: Faerie Ring, Fountain of Youth, Temple, Mineral Spring, Stables (+ Lancer → Champion), Magic Well (map format v18;
      once a day per hero, the Stables once a week)
- [x] Permanent power-ups: Learning Stone, Gazebo, Library of Enlightenment, Tree of Knowledge, Arena, Colosseum of the Magi (map format
      v18; choices in the decision panel, not run in Godot yet; the AI trains where it is worth it)
- [x] Creature banks: the object kind, the rolled guard tier, win-only reward, plundered state; Churchyard, Mansion, Ruins (with the
      12-tile reveal), Dwarven Treasury, Imp Cache, Medusa Stores, Naga Bank; refill after 4 weeks with a new roll (map format v20; the AI
      plunders banks it can beat); the Dwarven Treasury is guarded by the new neutral Dwarf / Battle Dwarf
- [x] Guards for neutral map dwellings of tier 3 / 4 / 5 (9 / 6 / 3 base creatures, set at game start; the AI fights them)
- [x] Prison (level odds, faction drawn when the game starts for a map that leaves it open, refused at the hero limit; editor rows; map format v19)
- [x] Hero limit per player (1 to 8) as a map rule: map format v15, the tavern respects it, editor setting (conditions panel; the Godot
      part builds but is not run in the editor yet), validator warning, template format v5 and a generator override; the Prison will use
      it; the editor's "Generate" tab offers the override (ADR-087), the new-game screen not yet
- [x] Hero limit follow-ups (MAP-14): a player's own limit in the editor (players panel) and the map (v18); the editor refuses a hero or a
      limit that breaks it; surplus heroes of a map made elsewhere are removed when the game starts
- [x] Utilities: Sign, Observatory, Hill Fort, Town Gate (town picker), War Machine Factory (map format v19; game panels and the notice panel
      not run in Godot yet; the AI uses all but the sign)
- [ ] Map format version, editor placement and inspector rows, validator checks for all of them
- [x] Random map generator: `TreasurePlacer` places the catalogue as treasure by value band (`TreasureSites`, placeholder values, one item in 3;
      banks and abandoned mines unguarded; not signs or map dwellings; generator version 3)
- [x] AI: visits the objects that help it (resources, loot, power-ups, buffs, banks and guards it can beat, prisons, the observatory), upgrades at
      hill forts and buys missing machines at factories when it can pay, and takes a town gate to the own town it needs most; buffs are
      visited for their value, not timed before a fight

#### Second catalogue (ADR-069, decided 2026-10-20; built 2026-10-27, ADR-082; [catalogue](../design/adventure-map.md#second-object-catalogue-adr-069))
Same steps per object as above (map format v23, editor, validator, AI, generator values in `TreasureSites`); the Godot screens are not run yet.
- [x] Buffs: Clover Field, Rainbow, Rally Flag
- [x] Power-ups: Dream Teacher, Training Grounds, Marletto Tower, Star Axis, Garden of Revelation
- [x] Mage's Chest (5 % nothing, 5 % Major, 60 % Minor, 30 % Minor + 500–1000 gold; vanishes)
- [x] Deposits that become mines (Wood Pile, Ore Deposit, Gold Vein, Crystal Vein, Cinnabar Deposit, **Sulfur Vent**); paid by
      any visiting hero, normal monster guards, the generator places them like abandoned mines
- [x] Six creature banks: Ruins (orcs), Lair, Keep, Hideout, Forest Glen, Dragon City (the strongest); the undead Ruins renamed Crypt
- [x] Window and Eye of the Magi (four colours, radius 6, up to 4 Eyes a colour; editor only, not generated)
- [x] Garrison (flagged, captured by its guards, both ways, leave and take troops)
- [ ] Generator option to guard connections with garrisons, border guards or gates (RMG-11, template format)
- [x] Map tavern (4 offers from its own weekly pool of random heroes shared by all players; hire at the entrance, hero on the
      next free cell, hero limit; ADR-070, ADR-071)
- [x] Portals: rename monoliths to portals in the UI (one thing, two ends per type, ADR-070); nothing new in core
- [x] Mana Vortex (twice the maximum until spent, weekly per hero), Fountain of Magic (weekly per hero, ADR-072)
- [x] Keymaster's Tent, border guards and border gates (four colours, the player's key, guards vanish, gates stay; ADR-071)
- [ ] Shrines (spell set in the editor or rolled at start, school Magic at the spell's level, unlimited visits) (Phase 5)

#### Third catalogue (ADR-077; built 2026-10-26, ADR-079 / ADR-080; [catalogue](../design/adventure-map.md#third-object-catalogue-adr-077))
Core, map format v22, editor placement, generator sites and tests for each; the Godot screens are not run yet.
- [x] Witch Hut (two skills of any group, the same group's next where the hero cannot, one lesson per hero level; MAP-34)
- [x] Refugee Camp (a weekly tier 3–5 base creature at the normal price; MAP-30)
- [x] Cartographer (surface / underground, 3000 gold reveals the level, once per player; MAP-31)
- [x] Artifact Merchant (7 Minor or Major artifacts for twice their worth, monthly restock; MAP-33)
- [x] Sacrificial Altar (creatures for Darkness, artifacts for Light, either for Wild heroes; MAP-29)
- [x] Sanctuary (a hero inside cannot be attacked; MAP-28)
- [x] Den of Thieves (the whole Thieves' Guild table; TOWN-11)
- [x] Sphinx (author's or table riddle, three answers; Major artifact or 5000 experience, or the day's movement and −3 morale; MAP-27)
- [x] Astrologer's Tower (a visit shows next week's event, no re-roll; MAP-32, ADR-084; map format v24, generator version 6)
- [ ] Editor inspector for a sphinx's riddle and a witch hut's skills (the map format holds them)

#### Pandora's Box (ADR-096)
- [ ] Pandora's Box (MAP-35, ADR-100): the author's contents, gains only (resources, experience, mana, morale and luck, skills, artifacts,
      creatures; spells with Phase 5), "open?", then an optional guard army (opens only when the hero wins) and message, vanishes; contents
      hidden; map format bump, editor rows, validator (an empty box is an error), AI value against the guards, generator boxes by value band

### Content for the slice
- [ ] Haven and Inferno play-tested end to end on a small map (towns work for all eight; the slice decides what gets
      art and balance first)
- [ ] A small two-player scenario map vs. AI shipped with the game

**Exit**: complete small-map game vs. AI from start to victory.

## Phase 4: Random map generation
Needs towns, guards and treasure from Phases 2–3. The template *format* and validator can start earlier.

### Templates
- [x] New core project `EternalWars.Core.Generation` (engine-free, deterministic from seed): project, `SeedStreams` (ADR-064)
- [x] Template model + `*.ewtemplate.json` (v1; v2 roads, v3 start buildings, v4 teams): sizes, players, levels, zones (type, owner, size, level, towns, richness,
      treasure bands, monster strength, terrains), connections (guard value, wide / guarded / gate) ([random-maps.md](../design/random-maps.md), ADR-064)
- [x] Teams in templates: a team per start zone, `teamsLocked` (RMG-6, template v4)
- [ ] Template model follow-ups (ADR-070): portals (monoliths) linking zones, garrisons as a connection guard; optional
      connections with a presence probability (100 % default), border guards and gates as a connection option (ADR-071)
- [ ] Teams 2–8 (ADR-070): team defaults and restrictions in templates and maps; the map editor may override the template's;
      new-game screen respects them
- [x] Template validator (`TemplateValidator`: errors and fairness warnings); shipped templates `duel`, `ffa-4` ("Free for all") and `2v2` ("Two against two")

### Generator pipeline
- [x] Zone layout from the connection graph (`ZoneLayouter`: Voronoi and Rectangles strategies, ADR-064; `sim layout`)
- [x] Terrain per zone (`TerrainPainter`, ADR-064)
- [x] Borders and obstacles (walls two tiles thick between zones, rock clumps inside; `Walls` kept for the connections stage)
- [x] Scenery objects (ADR-079, MAP-25): `Scenery` from `scenery.csv` (12 kinds in several sizes, 1×N walls in four directions, all
      blocking, terrains per row); `SceneryPlacer` turns the rock clumps into scenery; editor palette; map format v22
- [x] Rivers (ADR-079, MAP-26): an overlay like roads, +25 % for leaving a river tile unless road to road; editor brush; `RiverPlacer`
      runs some across zones; 3D view and the 2D harness draw them (generator version 4)
- [x] Connections and their guards (`ConnectionPlacer`, `GuardPicker`; passages per connection configurable, ADR-064)
- [x] Towns (player and neutral): `SettlementPlacer`, HoMM3-style defaults (ADR-064); start-town type swap for game setup (`SetTownType`)
- [x] Mines and resources (guards by zone strength, piles)
- [x] Roads between towns, through connections, to gates and the nearest lumber mill and ore mine (`RoadPlacer`, RMG-4)
- [x] Treasure by value band: piles, chests, artifacts with fixed contents; Area-mode guards on valuable items
      (`TreasurePlacer`, RMG-1, RMG-2)
- [x] Neutral monsters: no free-roaming stacks for now (RMG-3); guards on passages, gates, mines and treasure
- [x] Scenario assembly: players, owners, start-town types, picked or drawn start heroes, empty slots stay neutral; the
      output is a scenario `GameState` (`ScenarioAssembler`, `MapGenerator.Build`, ADR-064)
- [x] Scenario follow-ups (RMG-5, RMG-7): start-zone buildings and dwellings in the template (v3) + a tier-1 dwelling
      picked at setup (AI random); garrisons for empty slots; HoMM3-style start armies; retry on problems with sub-seeds
      (`GenerateBest`); standard rules; `MapGenerator.Version` in game records (v3); factions as generator input with
      native start-zone terrain, "Random" drawn first; a generated game saves as a map and replays
- [ ] Connectivity and fairness validation of the whole map (ADR-071: distance to mines, treasure value, guard power on the way
      within ±15 %; retry with the next sub-seed, then report) (whole-map reachability of start towns is checked in
      `sim maps`, not yet by the generator itself)
- [ ] Mirrored templates (1.0 scope, ADR-070): 2 players by an axis (vertical, horizontal, diagonal), 4 and 8 players by the
      centre point (8 with the underground); mirrored zones share their rolls; layout search keeps the symmetry

### Tooling and UI
- [x] Headless batch in CI: `sim maps` generates N maps per shipped template over its sizes and player counts and checks
      them (no generator problems, deterministic, every start town reachable from every player, AI-vs-AI play with no
      refused command, replay round trip); CI job `generated-maps`; fairness is the item above
- [ ] Editor: template editor like HotA's (zone graph: add zones, set properties, draw connections; live validation; no
      undo) with "Generate" to see the result (ADR-070)
- [x] Editor: "generate map from template" core (`EditorMapGenerator`): a template, size, seed, player count and optional
      town per slot give a new editable scenario with coloured players, start towns, heroes and armies, a description
      that records how it was made, and the generator's problems and validator issues to show
- [x] Editor: "generate map from template" UI (template, size, seed, players, towns, hero limit; problems and issues on the status line
      and the Validation tab): the "Generate" tab and "From template" (ADR-087; smoke scenario `editor-generate`)
- [x] Editor: change a town's faction (an undoable `SetTownType`), e.g. after generating (ADR-083; inspector not run in Godot yet)
- [x] Game: new-game screen picks template, size, players (human or computer, town and hero per player, team), seed and options, then
      generates; problems of the best attempt are shown with "Start anyway"; saves keep the generation info (ADR-086; smoke scenario
      `game-random-map`). Still open: the tier-1 dwelling pick and the hero limit override
- [ ] Starter templates: 2-player duel, 4-player free-for-all and 2v2 (shipped), 8-player
- Not in 1.0: water zones and islands (no sea travel, ADR-051), HotA template import (ADR-034)

**Exit**: a player picks a template in the game and gets a fair, playable random map; designers
author templates in the editor.

## Phase 5: Content and polish

### Magic (ADR-047, ADR-063)
Content draft: [spells.md](../content/spells.md), re-drafted (ADR-099): 17 spells a school, base numbers × Spell Power. Open: MAG-13.

- [ ] Spell table `data/spells.csv` from the re-draft (school, level, cost, tags, three ranks; adventure spells; final numbers after the
      visuals); mana on heroes is done (ADR-063)
- [ ] Guild spell selection (3 / 3 / 2 / 2 of the school's 5 / 5 / 4 / 3), the extra-school buildings ❓ MAG-13
- [ ] Summoned creatures (`summon`: vanish after the battle, no experience) ❓ MAG-13
- [ ] Spell book: mages start with one, warriors buy it for 500 gold in a guild (ADR-071)
- [ ] Guild offers 3 / 3 / 2 / 2 spells of the town's school per level; a hero learns a spell when the school's Magic skill is at least
      its level (ADR-071)
- [ ] Spell Power multiplies damage and buff / debuff duration: × (1 + SP / 10) for now (ADR-072)
- [ ] School skills take effect: magic (spell levels), attunement (mana, regeneration and cost are in, ADR-063; no potency, ADR-071), mastery
      (allows ranks 2 / 3; a spell rises, free, on a visit to a guild that offers it, ADR-072)
- [ ] Combat spells: the hero casts once a round during a controlled unit's turn (ADR-070); the unit still acts (ADR-071); machines can be
      targeted and resist as `mechanical`; immunity, resistance and creature casters become live (ADR-042, ADR-049)
- [ ] Adventure spells: a fixed 500 movement points besides mana (fewer left: no cast); Dimension Door only to explored tiles (ADR-072)
- [ ] Area spells: a fixed area; a big creature with any cell inside is hit once (ADR-072)
- [ ] Spell resistance skill (−8 / 14 / 20 % to damage and duration, durations away from zero, at least 1 round; ADR-071); spell
      specialties +3 % of the effect per hero level (ADR-072)
- [ ] Spell tags (`holy`, `summon` new) and creature caster spells mapped onto the list (ADR-099)

### Combat additions
- [x] Morale and luck (ADR-053): ±5 from creature table and hero (map format v7, editor); per round 2 %/point extra
      action or lost turn, else 4 %/point ±50 % initiative; per hit 4 %/point luck for +75 % / −50 % damage, opposite
      rolls cancel; shown in the 3D battle (captions, log, unit stats)
- [x] Morale sources (ADR-075, CMB-2): −1 per faction beyond the first in an army (neutrals one faction), +1 for a town's defenders with
      the Brotherhood (`BattleParticipant.ArmyMorale`); undead, constructs and elementals have no morale (ADR-071, CMB-7); Leadership,
      warcries, artifacts and buffs were in
- [x] War machines (ADR-056): offensive machine per town (ballista, cannon, catapult, glaivethrower), healing tent,
      ammo cart; War machines skill group (Ballistics, Healing, Ammo supply); units at the back of the field, offensive
      machine in regular battles from Advanced Ballistics; lost when destroyed; map format v9, editor, 3D battle
- [x] Exchanging war machines between heroes (ADR-070, ADR-083: `GiveWarMachineCommand`, exchange panel, AI)
- [ ] Towns keep machines only through a garrison hero (waits for garrison heroes); war machine tuning
- [x] Teleporting creatures move like flyers (ADR-070, ADR-083: `Teleporting` capability; no creature has it yet)
- [ ] Siege battles before 1.0 (ADR-099): a battle at a town with a Fort; 36-wide field, wall sections and gate, keep and towers that
      shoot, moat, siege catapult, 1/2 cover behind walls; fortifications by Fort / Citadel / Castle (ADR-066) ❓ CMB-11
- [ ] Sieges: the 3D battle scene (walls, gate, towers, breaches), the AI attacking and defending fortified towns ❓ CMB-11
- [x] Battle movement as a circle (ADR-088, ADR-089, CMB-9): reach within Speed + 0.25 cells at any angle (Theta* around obstacles),
      flyers and teleporters by straight-line distance, the distance penalty measured the same way, battle speeds +10 %; old replay
      digests change

### Map events (ADR-096; HotA's extended events; where they go: EVT-1 ❓)
- [ ] Core: map events as data (hero, player and town events), variables, conditions and expressions, the main actions, messages and
      questions as pending decisions; event cells (synchronised copies); turn-start runs; map format bump ❓ EVT-1, EVT-2, EVT-3, EVT-4
- [ ] Quests: Seer's Hut, Quest Guard, Quest Gate, quest stages, the quest log screen; HoMM3's quest forms ❓ EVT-5
- [ ] Loops and "run another event"; a Pandora's Box that runs a hero event (MAP-35)
- [ ] AI: answers by value, Seer's Hut quests worth doing, Quest Guards and Gates as obstacles ❓ EVT-6
- [ ] Editor: an "Events" tab (the tree, variables, shared events, ready forms for HoMM3's simple events), object inspector links,
      validator checks ❓ EVT-6

### Platform and art
- [ ] Platform pass: Linux/Steam Deck builds, controller-friendly UI, Deck performance budget (ADR-034)
- [ ] Look-dev spike: one terrain biome + one creature + lighting setup, to lock the visual target and budget
- [ ] Smooth fog (ADR-099): a soft fade from the visible to the hidden area; objects half under the fog show their explored part
      correctly (rendering only; the rules keep cell-by-cell visibility)
- [ ] Terrain: heightmapped landscape with splat shader and blended biome transitions; grid only as overlay
- [ ] PBR models for heroes, creatures, towns and map objects; fog/volumetrics
- [ ] UI theme (HoMM-style right-side panel, status bar)
- [x] Speed settings (ADR-088, ADR-090, ADR-095, UI-1): slow / normal / fast / instant for own heroes, computer heroes (with "don't show")
      and battle animations; rows in the game menu, kept in `settings.json`; hero walking with a hop; computer turns played back hero by
      hero (smoke scenario `game-speed-settings`)
- [ ] Audio: per-terrain ambient music, town themes

## LAN multiplayer (ADR-088, ADR-098; before 1.0, after the Phase 3 and 4 exits)
- [ ] Network layer: host and clients over the LAN; clients send commands, the host runs them and plays the computer players (MP-1)
- [ ] Per-player view: a client starts from the map's static content; the host sends each client only the events it may see (fog);
      the client keeps a partial copy of the state for its views; under the fog stays as last seen; both sides see a whole battle, others
      its result (MP-3, ADR-099)
- [ ] Lobby: LAN discovery and typing an address, map or random map with its options (ADR-086), colours, computer players, the
      simultaneous first week on or off; text chat (MP-1)
- [ ] Battles between two remote humans, each side live; a player who drops out (pause, the host lets the computer take over or waits
      for a rejoin) (MP-1)
- [ ] Saves: the host's save loads in multiplayer, single player or hot-seat; a client's save (the whole record, sent by the host)
      loads in single player; replays are the host's record (MP-3, ADR-099)
- [ ] Start zones in the map: the generator writes them, the editor paints them, validator (touching, overlapping, no start town);
      map format bump (MP-4, ADR-099)
- [ ] Simultaneous first week on maps with start zones: conflicts (entering another start zone, touching an object another player touched
      that day, seeing another player's hero) discard the day for everyone, which is played again turn by turn (MP-2, ADR-098)
- [ ] The turn timer in multiplayer and in simultaneous days: one timer, the day ends when all have ended their turn (FLOW-3, MP-2)
- [ ] Arena maps in the lobby (ADR-100, ARN-1): day 1 hidden by each player's own view, turn by turn or simultaneous with start zones

**Exit**: two players on two computers play a random map from the lobby to the end, save it and load it again.

## Phase 6: Story campaign
- [ ] Scenario chaining and hero carry-over
- [ ] Map triggers and events, dialogue: built on the map events (ADR-096); campaign variables kept from one map to the next (FLOW-1, EVT-1)
- [ ] Editor support for triggers and scripting: the map events' "Events" tab (ADR-096)

## Risks
| Risk | Mitigation |
|---|---|
| Godot C# library limitations (scripts in referenced assemblies) | Phase 0 spike; code-first nodes; app-side wrappers for editor-attachable scripts |
| 3D readability on dense maps | Early camera and art-style prototypes; keep the 2D harness as the logic reference |
| AI complexity | Deterministic core + commands give headless simulation for AI tuning |
| LAN multiplayer: a client's partial view drifts from the host's state, or leaks what is under the fog | Only the host runs rules; tests that compare a client's view with the host's state filtered by fog |
| Random maps unfair or broken | Batch generation + automated validation/fairness metrics in CI; templates versioned like maps |
| Asset production cost | Look-dev spike early; buy/adapt PBR asset packs where the dark-fantasy style allows |
