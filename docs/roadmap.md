# Roadmap

Each phase ends with something **playable or usable** and a green test suite. Items are small enough to land in one
change each; `❓` marks items that wait for a design answer in [open-questions.md](open-questions.md). Core items come
with tests (CLAUDE.md); UI items are checked with `tools/smoke-godot.ps1` where a smoke scenario exists.

**Where we are (2026-10-16)**: Phases 0–2 are done (the new 3D battle scene still needs a run in Godot), and Phase 3 has towns,
dwellings, heroes, skills and every income source except artifacts. Next up in Phase 3: town building effects, the
tavern, artifacts, game flow (victory, save/load) and the adventure AI.

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
- [x] Neutral monsters with two map-level guard modes (HoMM3 zone of control with stealing, or a guarded
      3×3 area), HoMM3 dispositions (join / flee / fight, ADR-040), counts rolled by tier; optional mine guards;
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

### Towns: building effects (❓ open question 6 for the numbers)
- [ ] Fort: growth bonus per level and/or garrison defence (decide; sieges come post-1.0)
- [ ] Marketplace trading: resource exchange rates that improve with the number of owned marketplaces
- [x] Blacksmith: sells the healing tent, the ammo cart and a lost offensive machine to the visiting hero (ADR-056)
- [ ] Mage guild: spells per level (waits for magic, Phase 5); until then cost and requirements only
- [ ] Town hall level 4 (capitol-like)? ❓
- [ ] Per-town building lists or names (every town shares one table today)
- [ ] Dwellings on the adventure map (ADR-054): flaggable map object with one creature line, weekly growth that
      accumulates, recruiting by a visiting hero; map format and editor
- [ ] Caravan building (ADR-054): hire from owned map dwellings within 5 days of travel at the creatures' map speed,
      along a route no enemy hero or monster blocks, checked before paying; the creatures travel virtually and join
      the garrison on arrival (`CaravanArrived`); the same for troops between two own towns; UI in the town screen
- [ ] Caravan rules still open: blocked route or captured destination during travel, visibility, cost (ADR-054)

### Towns: Godot
- [x] 2D harness town panel (`TownPanel`, `--scenario=towns`): build, dwellings, recruit, upgrade, garrison
- [x] Towns on the 3D map with flag and name; editor inspector for towns
- [ ] Town screen in the game app: the 2D `TownPanel` as a full-screen panel over the map (ADR-050), opened by
      visiting or from the town list
- [ ] Town list in the HUD next to the hero list; select and centre on a town
- [ ] Town screen polish: building tree with requirements, cost tooltips, "built today" state, dwelling choice
      warnings (which tier would become unreachable)
- [ ] Smoke scenario for the town screen

### Heroes and skills
- [x] Hero roster `data/heroes.csv`: 16 per town, mage/warrior, preferred creature, specialty (ADR-048)
- [x] Skill groups, levels, level-ups with three offers and rival-school odds (`LevelUpDecision`)
- [x] Skill effects without towns or magic: melee, archery, armor, treasury, prospecting, logistics, pathfinding,
      troop management, drill, Tactics width
- [x] Native terrain only for all-native armies; Pathfinding −25/50/75 % of the penalty; routes and steps share one
      cost rule (ADR-024, ADR-032, ADR-045)
- [x] Primary stat growth (ADR-055): one point per level, HoMM3-style odds by class (draft), shown in the level-up
      prompt; Spell Power and Knowledge added (roster columns, map format v8, editor), effects with magic
- [ ] Stewardship: −5/10/15 % building and recruit costs in the town the hero visits
- [ ] Skill specialties: what a skill specialty adds ❓ open question 9
- [ ] Third offense and third defense skills (placeholders today) ❓ open question 9
- [ ] Deployment: rearrange troops before battle (needs a deployment step in `BattleSetup` and UI)
- [ ] Hero screen (Godot): stats, skills with descriptions, specialty, army; opened from the hero list

### Tavern
- [ ] Hire heroes: per-player tavern pool of two heroes from the roster, refreshed weekly; cost; hero appears on
      the town entrance (`HireHeroCommand`)
- [ ] Heroes who retreat or surrender return to the pool
- [ ] Surrender in battle: pay gold to leave with the army, needs an own tavern-like rule (ADR-038)
- [ ] Hire screen in the town panel

### Artifacts
Content draft: [artifacts.md](artifacts.md).

- [ ] Artifact table `data/artifacts.csv` (id, name, slot, tier, effects), embedded in core like creatures
- [ ] Hero paper-doll slots and a backpack; equip / unequip / move commands
- [ ] Stat effects: Attack, Defense, movement points, battle speed, skill levels
- [ ] Artifact income (`ArtifactIncome`, an `IIncomeSource`) for resource-producing artifacts (ADR-025)
- [ ] Terrain artifacts: remove the non-native penalty (ADR-045)
- [ ] Artifacts on the map (pickup object), in treasure chests (the HoMM3 artifact tier, ADR-039) and as battle loot
      from defeated heroes
- [ ] Artifact exchange between own heroes (`ExchangePanel`)
- [ ] Map format and editor: artifacts on heroes and on the map
- [ ] Godot: paper-doll and backpack on the hero screen

### Game flow
- [ ] Victory and loss conditions: default (defeat every enemy: no towns and no heroes); map-specific conditions
      (capture a town, defeat a hero, gather resources, survive N days) in the map format and editor
- [ ] Player elimination: a player without towns for 7 days loses (HoMM3) ❓; `PlayerDefeated`, `GameEnded`
- [ ] Save/load: save files from map + options + command log (`GameRecord`), verified by `GameStateDigest`; a state
      snapshot if replays get too slow
- [ ] Game app: save / load menu, victory / defeat screen
- [ ] Week events ("week of X") ❓

### Adventure AI (computer players)
- [ ] AI driver that plays a computer player's turn through commands only (ADR-036), deterministic from the RNG
- [ ] Economy: build order and recruiting in its towns
- [ ] Exploration: pick up resources, flag mines, visit chests (value per movement point)
- [ ] Fights: attack guards and heroes it can beat (`sim battle`-style estimate); avoid stronger enemies
- [ ] Town defence and capture of undefended enemy towns
- [ ] Simulator: AI-vs-AI games (`sim game`) for tuning and CI

### Content for the slice
- [ ] Haven and Inferno play-tested end to end on a small map (towns work for all eight; the slice decides what gets
      art and balance first)
- [ ] A small two-player scenario map vs. AI shipped with the game

**Exit**: complete small-map game vs. AI from start to victory.

## Phase 4: Random map generation
Needs towns, guards and treasure from Phases 2–3. The template *format* and validator can start earlier.

### Templates
- [ ] New core project `EternalWars.Core.Generation` (engine-free, deterministic from seed)
- [ ] Template model + `*.ewtemplate.json`: sizes, players, levels, zones (type, owner, size, level, towns, richness,
      treasure bands, monster strength, terrains), connections (guard strength, wide / guarded / gate / portal,
      optional) (game-design §4.9)
- [ ] Template validator

### Generator pipeline
- [ ] Zone layout from the connection graph
- [ ] Terrain per zone
- [ ] Borders and obstacles
- [ ] Connections and their guards
- [ ] Towns (player and neutral)
- [ ] Mines and resources
- [ ] Treasure by value band
- [ ] Neutral monsters
- [ ] Roads between towns and through connections
- [ ] Connectivity validation; the output is an ordinary `MapDocument`, editable in the map editor
- [ ] Mirrored/symmetric zones for competitive templates (1.0 scope)

### Tooling and UI
- [ ] Headless batch: generate N maps per template × size × seed and validate them (reachability, fairness between
      start zones), in CI
- [ ] Editor: template editor (zone graph: add zones, set properties, draw connections)
- [ ] Editor: "generate map from template" into an editable map
- [ ] Game: new-game screen picks template, size, players, seed and options, then generates
- [ ] Starter templates: 2-player duel, 4-player free-for-all, 2v2, 8-player
- Not in 1.0: water zones and islands (no sea travel, ADR-051), HotA template import (ADR-034)

**Exit**: a player picks a template in the game and gets a fair, playable random map; designers
author templates in the editor.

## Phase 5: Content and polish

### Magic (ADR-047)
Content draft: [spells.md](spells.md).

- [ ] Spell table (school, level, cost, tags, three ranks); spell book and mana on heroes
- [ ] Mage guild offers spells by level; heroes learn them by visiting
- [ ] School skills take effect: magic (spell levels), attunement (mana, regeneration, cost or potency), mastery
      (spell ranks)
- [ ] Combat spells: hero casts one spell per round; immunity, resistance and creature casters become live (ADR-042,
      ADR-049)
- [ ] Adventure spells
- [ ] Spell resistance skill and spell specialties take effect
- [ ] Spell tags and creature caster spells ❓ open question 7

### Combat additions
- [x] Morale and luck (ADR-053): ±5 from creature table and hero (map format v7, editor); per round 2 %/point extra
      action or lost turn, else 4 %/point ±50 % initiative; per hit 4 %/point luck for +75 % / −50 % damage, opposite
      rolls cancel; shown in the 3D battle (captions, log, unit stats)
- [ ] Morale and luck sources: skills, artifacts, buildings, army composition; undead/constructs without morale? ❓
- [x] War machines (ADR-056): offensive machine per town (ballista, cannon, catapult, glaivethrower), healing tent,
      ammo cart; War machines skill group (Ballistics, Healing, Ammo supply); units at the back of the field, offensive
      machine in regular battles from Advanced Ballistics; lost when destroyed; map format v9, editor, 3D battle
- [ ] War machines follow-ups: siege use (needs sieges), exchanging machines between heroes, tuning ❓
- [ ] Teleporting creatures and footprints ❓ open question 4
- [ ] Siege battles (36-wide battlefield, walls 1/2 cover, towers) ❓ post-1.0

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
