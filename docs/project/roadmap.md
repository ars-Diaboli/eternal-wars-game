# Roadmap

Each phase ends with something **playable or usable** and a green test suite. Items are small enough to land in one
change each; `❓` marks items that wait for a design answer in [open-questions.md](open-questions.md). Core items come
with tests (CLAUDE.md); UI items are checked with `tools/smoke-godot.ps1` where a smoke scenario exists.

**Where we are (2026-10-19)**: the adventure-map object catalogue is designed (ADR-068, block in Phase 3); its shared rules, the hero limit, the resource providers and the one-time loot are built, the other objects are next. Phases 0–2 are done (the 3D battle scene still needs a run in Godot). Phase 3 has towns,
dwellings, map dwellings and caravans, heroes, skills, the tavern, artifacts, every income source, victory and defeat, and
save/load, and the fort's effect; trading waits for TOWN-2. Phase 4: the template format (v2) and every generator stage
(layout, terrain, connections, towns and mines, roads, treasure, scenario assembly, MapGenerator) are in core. Next up:
whole-map validation and the new-game screen (RMG-5, RMG-7; [open-questions.md](open-questions.md#-blocking-now)).

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
- [x] 3D battle scene in the game app (ADR-052), placeholder meshes; not yet run in Godot (no Godot on the
      development machine at the time): run `tools/smoke-godot.ps1` and check the frames
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
      backdrops, flyer arcs, battle music; a deployment step for the Deployment skill (Phase 3)

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

### Towns: building effects (❓ [TOWN-1, TOWN-2, TOWN-3](open-questions.md#towns-and-economy))
- [x] Fort: unlocks the dwellings; Citadel +50 % / Castle +100 % town growth, rounded down (ADR-066); fortifications wait
      for sieges (post-1.0)
- [x] Guard mode per monster stack: map format v13, monster inspector control, old maps converted (ADR-065)
- [x] Marketplace trading: gold against one resource, rates by `min(X + 1, 10)` with X the marketplace towns, governed ones 1.5; needs a town marketplace; `TradeResourceCommand`, trading screen (HUD button, town screen; Godot part not run in the editor yet); no AI trading (ADR-067); details ❓ TOWN-2
- [x] Map marketplace object: a visiting hero trades at the fixed X = 4 rate; map format v14, editor placement, validator; the generator places it as a treasure item where the zone's treasure value allows (ADR-067)
- [x] Blacksmith: sells the healing tent, the ammo cart and a lost offensive machine to the visiting hero (ADR-056)
- [ ] Mage guild: spells per level (waits for magic, Phase 5); until then cost and requirements only
- [ ] Town hall level 4 (capitol-like)? ❓ TOWN-3
- [ ] Per-town building lists or names (every town shares one table today)
- [x] Dwellings on the adventure map (ADR-054): `CreatureDwelling`, weekly growth, flagging, recruiting by a visiting hero
      (`RecruitAtDwellingCommand`); map format v12; editor placement and `SetDwelling`; validator
- [x] Caravan building (ADR-054): `OrderCaravanCommand` hires from owned map dwellings within 5 days of travel along an open
      route, `SendTroopsCommand` moves garrison stacks between own towns; creatures arrive in the garrison (`CaravanArrived`)
- [x] Caravan rules (user decisions): a blocked route returns the caravan with the price back, a lost destination scatters the
      creatures, caravans are invisible; building 1000 gold + 5 lumber (placeholder)
- [x] Town screen: caravan section (hire, send the picked stack, caravans on the way); `DwellingPanel` opens when a hero visits a dwelling
- [x] AI: heroes flag and recruit at map dwellings, towns build a caravan once the player owns a dwelling, hire from owned dwellings and send troops to an empty town (`AdventureAi`)
- [ ] Smoke scenario for dwellings and caravans (needs Godot); caravan rules (TOWN-5), caravan cost tuning; dwelling guards are
      in the objects block below (ADR-068)

### Towns: Godot
- [x] 2D harness town panel (`TownPanel`, `--scenario=towns`): build, dwellings, recruit, upgrade, garrison
- [x] Towns on the 3D map with flag and name; editor inspector for towns
- [x] Town screen in the game app: the 2D `TownPanel` as a full-screen panel over the map (ADR-050), opened by
      visiting, from the town list or with T; shows the governor (appoint / dismiss)
- [x] Town list in the HUD next to the hero list; select and centre on a town (`TownListPanel`)
- [ ] Town screen polish: building tree with requirements, cost tooltips, "built today" state, dwelling choice
      warnings (which tier would become unreachable)
- [x] Smoke scenario for the town screen (`game-town-screen`, `tools/smoke/towns.ewmap.json`; not yet run in Godot)

### Heroes and skills
- [x] Hero roster `data/heroes.csv`: 16 per town, mage/warrior, preferred creature, specialty (ADR-048)
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
- [ ] Adrenaline (Swamp Bestiary): design its skills (placeholders today) ❓ HERO-2
- [x] Complexity levels with starting resources (ADR-063); new-game screen option
- [x] AI that appoints governors on purpose (ADR-059)
- [ ] The `sim` tool picks a complexity
- [ ] Siege Ballistics: use the `WarMachines` rules once fortifications exist (auto-fire, control, second shot); needs
      sieges, post-1.0 (CMB-5)
- [ ] Skill specialties: what a skill specialty adds ❓ HERO-1
- [x] Leadership (`offense.third`, +1/2/3 morale) and Alertness (`defense.third`, +5/10/15 % initiative, working title) (ADR-067)
- [ ] Deployment: rearrange troops before battle (needs a deployment step in `BattleSetup` and UI) ❓ HERO-4
- [x] Hero screen (Godot): stats, mana, skills with descriptions, specialty, army, paper doll, war machines; opened from the hero list (`HeroPanel`, H or "Info"; smoke scenario `game-hero-screen`, not yet run in Godot)

### Tavern
- [x] Hire heroes (ADR-060): per-player weekly pool by ranks (native, adjacent, not opposing, opposing; warriors for
      Stronghold and Swamp Bestiary), 2 heroes basic and 4 with the upgraded tavern (Brotherhood), 2500 gold, hero on the
      entrance (`HireHeroCommand`), at most 8 heroes (or the map's limit, ADR-068)
- [x] Tavern as an upgradeable building (`tavern` level 2 in `buildings.csv`)
- [x] Heroes that retreat or are defeated return to the pool (offers exclude only heroes on the map)
- [x] The AI hires heroes
- [ ] Surrender in battle: pay gold to leave with the army (ADR-038) ❓ CMB-1
- [x] Hire buttons in the town panel (`TownPanel`)
- [ ] Tavern screen in the game app with hero details (waits for the town screen)

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

### Game flow
- [x] Victory and loss conditions (ADR-057): default (defeat every enemy: no towns and no heroes); map-specific
      conditions (capture a town, defeat a hero, gather resources, survive N days; lose a town, lose a hero, time limit)
      in the map format (v10) and the editor
- [x] Player elimination: `PlayerDefeated`, `GameEnded`, skipped turns, the end of commands
- [x] Grace period for players without towns: 7 days by default, configurable per map (HoMM3, ADR-057)
- [x] Save/load (ADR-058): save files from map + options + command log (`GameRecord`), verified by `GameStateDigest`,
      at any moment including mid-battle
- [x] Autosave: two slots, the start of the current and of the previous turn (ADR-058)
- [ ] Save/load: a state snapshot if replays get too slow
- [x] Game app: victory / defeat screen (`GameResultPanel`)
- [x] Game app: save / load menu (F10, F5 quick save, F9 quick load; also in battle)
- [ ] Week events ("week of X") ❓ FLOW-2

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
- [ ] Spells (Phase 5), marketplace use (TOWN-2); hiring heroes, artifacts and complexity levels are done
- [x] AI-vs-AI games on generated maps (`sim game --template`); CI plays two weeks on every shipped template (`sim maps`)
- [ ] Balance the AI over several maps (`sim game --runs`); on generated maps it hardly fights guards yet (heroes still
      level 1 after 40 days on `duel`), and no game finishes in 40 days

### Adventure map objects (ADR-068; [catalogue](../design/adventure-map.md#planned-objects-adr-068), ❓ [MAP-11 … MAP-14](open-questions.md#adventure-map-objects))
Each item is one change with core tests (`TestWorld` maps), a map format bump where needed, editor placement through
`MapEditorSession`, a validator check, and a generator hook later. Done in this order; the first item unblocks the rest.
- [x] Shared rules: visit scopes (weekly per object, once per hero, first visitor; vanishing is removal), hero statuses that expire (next
      battle, end of day, end of week), weighted rolls, artifact draw by tier (Minor, Major, Relic; the Relic pool refills when empty,
      duplicates allowed); no object uses them yet
- [x] Resource providers: Water Wheel, the six Warehouses, Windmill, Mystical Garden (map format v16, editor placement, AI visits them;
      warehouse choice in the decision panel, not run in Godot yet; the generator does not place them yet)
- [ ] Abandoned mines (six, a separate object that becomes the normal mine when the guard falls; Troglodyte guards, subterranean battle terrain for gold and ore)
- [x] One-time loot: Campfire, Supply Crates, Traveler's Wagon, Corpse, Lean To (map format v17, editor placement, AI picks them up); the new
      Treasure Chest table with a 5 % Minor artifact
- [ ] Resource Pile amounts rolled within the notes' ranges when the map leaves them open (today fixed defaults: 750 / 7 / 4)
- [x] Warrior's Tomb (artifact roll, the −3 morale status for every visitor)
- [ ] Temporary buffs: Faerie Ring, Fountain of Youth, Temple, Mineral Spring, Stables (+ Lancer → Champion), Magic Well (with magic)
- [ ] Permanent power-ups: Learning Stone, Gazebo, Library of Enlightenment, Tree of Knowledge, Arena, Colosseum of the Magi
- [ ] Creature banks: the object kind, the rolled guard tier, win-only reward, plundered state; Churchyard, Mansion, Ruins (with the
      12-tile reveal), Dwarven Treasury, Imp Cache, Medusa Stores, Naga Bank; refill after 4 weeks ❓ MAP-14, CONT-2
- [ ] Guards for map dwellings of tier 3 / 4 / 5 (9 / 6 / 3 base stacks)
- [ ] Prison (level odds, faction drawn at generation, fails at the hero limit)
- [x] Hero limit per player (1 to 8) as a map rule: map format v15, the tavern respects it, editor setting (conditions panel; the Godot
      part builds but is not run in the editor yet), validator warning, template format v5 and a generator override; the Prison will use
      it; the new-game and "generate map" screens will offer the override when they exist ❓ MAP-14
- [ ] Utilities: Sign, Observatory, Hill Fort, Town Gate, War Machine Factory ❓ MAP-11, MAP-12, MAP-13
- [ ] Map format version, editor placement and inspector rows, validator checks for all of them
- [ ] Random map generator: which objects `TreasurePlacer` and the zone rules may place, by value band (RMG-1)
- [ ] AI: visits the objects that help it (resources, power-ups, buffs before fights, banks it can win)

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
- [ ] Template model follow-ups: mirroring (1.0 scope, RMG-9); portals, optional connections, border guards (RMG-11)
- [x] Template validator (`TemplateValidator`: errors and fairness warnings); shipped templates `duel`, `ffa-4` ("Free for all") and `2v2` ("Two against two")

### Generator pipeline
- [x] Zone layout from the connection graph (`ZoneLayouter`: Voronoi and Rectangles strategies, ADR-064; `sim layout`)
- [x] Terrain per zone (`TerrainPainter`, ADR-064)
- [x] Borders and obstacles (walls two tiles thick between zones, rock clumps inside; `Walls` kept for the connections stage)
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
- [ ] Connectivity and fairness validation of the whole map ❓ RMG-8 (whole-map reachability of start towns is checked in
      `sim maps`, not yet by the generator itself)
- [ ] Mirrored/symmetric zones for competitive templates (1.0 scope) ❓ RMG-9

### Tooling and UI
- [x] Headless batch in CI: `sim maps` generates N maps per shipped template over its sizes and player counts and checks
      them (no generator problems, deterministic, every start town reachable from every player, AI-vs-AI play with no
      refused command, replay round trip); CI job `generated-maps`; fairness waits for RMG-8 (item above)
- [ ] Editor: template editor (zone graph: add zones, set properties, draw connections) ❓ RMG-10
- [x] Editor: "generate map from template" core (`EditorMapGenerator`): a template, size, seed, player count and optional
      town per slot give a new editable scenario with coloured players, start towns, heroes and armies, a description
      that records how it was made, and the generator's problems and validator issues to show
- [ ] Editor: "generate map from template" UI (template, size, seed, players, towns; show problems and issues) (Godot)
- [ ] Editor: change a town's faction (an undoable `SetTownType`), e.g. after generating
- [ ] Game: new-game screen picks template, size, players (town and hero per player), seed and options, then generates
- [ ] Starter templates: 2-player duel, 4-player free-for-all and 2v2 (shipped), 8-player
- Not in 1.0: water zones and islands (no sea travel, ADR-051), HotA template import (ADR-034)

**Exit**: a player picks a template in the game and gets a fair, playable random map; designers
author templates in the editor.

## Phase 5: Content and polish

### Magic (ADR-047, ADR-063)
Content draft: [spells.md](../content/spells.md). Open questions: MAG-1 … MAG-6.

- [ ] Spell table (school, level, cost, tags, three ranks); spell book (MAG-1, MAG-2); mana on heroes is done (ADR-063)
- [ ] Mage guild offers spells by level; heroes learn them by visiting (MAG-2)
- [ ] School skills take effect: magic (spell levels), attunement (mana, regeneration and cost are in, ADR-063; potency? MAG-3), mastery
      (spell ranks)
- [ ] Combat spells: hero casts one spell per round; immunity, resistance and creature casters become live (ADR-042,
      ADR-049)
- [ ] Adventure spells (MAG-5)
- [ ] Spell resistance skill and spell specialties take effect (MAG-6)
- [ ] Spell tags and creature caster spells ❓ MAG-1

### Combat additions
- [x] Morale and luck (ADR-053): ±5 from creature table and hero (map format v7, editor); per round 2 %/point extra
      action or lost turn, else 4 %/point ±50 % initiative; per hit 4 %/point luck for +75 % / −50 % damage, opposite
      rolls cancel; shown in the 3D battle (captions, log, unit stats)
- [ ] Morale and luck sources: skills, buildings, army composition (artifacts are in, ADR-061); undead/constructs without morale? ❓ CMB-2
- [x] War machines (ADR-056): offensive machine per town (ballista, cannon, catapult, glaivethrower), healing tent,
      ammo cart; War machines skill group (Ballistics, Healing, Ammo supply); units at the back of the field, offensive
      machine in regular battles from Advanced Ballistics; lost when destroyed; map format v9, editor, 3D battle
- [ ] War machines follow-ups: siege use (needs sieges), exchanging machines between heroes, tuning ❓ CMB-3
- [ ] Teleporting creatures and footprints ❓ CMB-4
- [ ] Siege battles (36-wide battlefield, walls 1/2 cover, towers) ❓ post-1.0, CMB-5

### Platform and art
- [ ] Platform pass: Linux/Steam Deck builds, controller-friendly UI, Deck performance budget (ADR-034)
- [ ] Look-dev spike: one terrain biome + one creature + lighting setup, to lock the visual target and budget
- [ ] Terrain: heightmapped landscape with splat shader and blended biome transitions; grid only as overlay
- [ ] PBR models for heroes, creatures, towns and map objects; fog/volumetrics
- [ ] UI theme (HoMM-style right-side panel, status bar)
- [ ] Audio: per-terrain ambient music, town themes

## Phase 6: Story campaign
- [ ] Scenario chaining and hero carry-over
- [ ] Map triggers and events, dialogue
- [ ] Editor support for triggers and scripting

## Risks
| Risk | Mitigation |
|---|---|
| Godot C# library limitations (scripts in referenced assemblies) | Phase 0 spike; code-first nodes; app-side wrappers for editor-attachable scripts |
| 3D readability on dense maps | Early camera and art-style prototypes; keep the 2D harness as the logic reference |
| AI complexity | Deterministic core + commands give headless simulation for AI tuning |
| Random maps unfair or broken | Batch generation + automated validation/fairness metrics in CI; templates versioned like maps |
| Asset production cost | Look-dev spike early; buy/adapt PBR asset packs where the dark-fantasy style allows |
