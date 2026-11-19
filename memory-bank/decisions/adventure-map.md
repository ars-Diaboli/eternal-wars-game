# Decisions: Adventure map

**Covers**: The grid, terrain classes and movement, roads, map objects and catalogues, neutral guards, fog-of-war rules, scouting, week events, the Grail.

**Current rules**: [docs/design/adventure-map.md](../../docs/design/adventure-map.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [005](#adr-005-square-adventure-grid-square-combat-grid-with-multi-cell-units) | Square adventure grid; square combat grid with multi-cell units | accepted; superseded by ADR-021 |
| [011](#adr-011-smooth-terrain-over-a-logical-square-grid) | Smooth terrain over a logical square grid | accepted |
| [012](#adr-012-map-objects--data-definitions--per-kind-instance-classes) | Map objects = data definitions + per-kind instance classes | accepted |
| [013](#adr-013-visiting-is-mandatory-routes-end-at-entrances) | Visiting is mandatory; routes end at entrances | accepted |
| [016](#adr-016-fog-of-war-is-per-player-permanent-shroud-provisional) | Fog of war is per-player permanent shroud (provisional) | superseded |
| [017](#adr-017-fog-mode-is-chosen-per-game-refog-uses-hysteresis) | Fog mode is chosen per game; refog uses hysteresis | accepted |
| [018](#adr-018-homm3-movement-rules) | HoMM3 movement rules | accepted |
| [020](#adr-020-map-size-presets-up-to-256-max-8-players) | Map size presets up to 256, max 8 players | accepted |
| [024](#adr-024-terrain-classes-and-homm3-style-roads) | Terrain classes and HoMM3-style roads | accepted; superseded by ADR-045 |
| [026](#adr-026-two-way-and-one-way-gates-no-random-exits) | Two-way and one-way gates, no random exits | accepted |
| [027](#adr-027-meeting-heroes) | Meeting heroes | accepted |
| [031](#adr-031-refog-behaviour-auto-stop-ambush-last-known-objects) | Refog behaviour: auto-stop, ambush, last-known objects | accepted |
| [032](#adr-032-teams-native-terrain-pathfinding-roads) | Teams, native terrain, Pathfinding, roads | accepted |
| [039](#adr-039-treasure-chest-rolls) | Treasure chest rolls | accepted |
| [040](#adr-040-neutral-guards-two-guard-modes-deterministic-dispositions) | Neutral guards: two guard modes, deterministic dispositions | accepted |
| [045](#adr-045-sandbox-terrain-model-every-terrain-penalises-non-natives) | Sandbox terrain model: every terrain penalises non-natives | accepted |
| [051](#adr-051-no-sea-travel) | No sea travel | accepted |
| [065](#adr-065-guard-mode-per-monster-stack-2026-10-17) | Guard mode per monster stack (2026-10-17) | accepted |
| [068](#adr-068-the-adventure-map-object-catalogue-2026-10-19) | The adventure-map object catalogue (2026-10-19) | accepted |
| [077](#adr-077-map-scenery-rivers-flaggable-providers-resource-caravans-caravan-speed-and-limits-kingdom-overview-thieves-guild-third-object-catalogue-2026-10-26) | Map scenery, rivers, flaggable providers, resource caravans, caravan speed and limits, kingdom overview, Thieves' Guild, third object catalogue (2026-10-26) | accepted; partly superseded |
| [079](#adr-079-flaggable-providers-resource-caravans-caravan-speed-and-limit-thieves-guild-sanctuary-rivers-and-scenery-2026-10-26) | Flaggable providers, resource caravans, caravan speed and limit, Thieves' Guild, sanctuary, rivers and scenery (2026-10-26) | implemented |
| [080](#adr-080-the-third-object-catalogue-cartographer-artifact-merchant-refugee-camp-sacrificial-altar-sphinx-witch-hut-2026-10-26) | The third object catalogue: cartographer, artifact merchant, refugee camp, sacrificial altar, sphinx, witch hut (2026-10-26) | implemented |
| [082](#adr-082-the-second-object-catalogue-2026-10-27) | The second object catalogue (2026-10-27) | implemented |
| [084](#adr-084-week-and-month-events-the-astrologers-tower-and-the-kingdom-overview-2026-10-28) | Week and month events, the Astrologer's Tower and the kingdom overview (2026-10-28) | implemented |
| [125](#adr-125-scouting-reveals-information-amount-words-with-their-ranges-hero-9-adr-069-adr-123-2026-11-04) | Scouting reveals information; amount words with their ranges (HERO-9, ADR-069, ADR-123, 2026-11-04) | accepted |
| [133](#adr-133-map-bans-and-random-placeholders-built-adr-126-2026-11-05) | Map bans and random placeholders built (ADR-126, 2026-11-05) | implemented |
| [140](#adr-140-the-grail-built-the-owners-puzzle-map-rules-adr-126-adr-130-map-36-2026-11-06) | The Grail built; the owner's puzzle map rules (ADR-126, ADR-130, MAP-36, 2026-11-06) | implemented |
| [141](#adr-141-a-town-sees-from-its-middle-owners-bug-report-2026-11-06) | A town sees from its middle (owner's bug report, 2026-11-06) | implemented |
| [142](#adr-142-scouting-reveals-information-built-adr-125-2026-11-06) | Scouting reveals information, built (ADR-125, 2026-11-06) | implemented |
| [205](#adr-205-routes-are-the-cheapest-the-ai-weighs-its-targets-with-one-cost-map-adr-013-adr-040-adr-120-2026-11-17) | Routes are the cheapest; the AI weighs its targets with one cost map (ADR-013, ADR-040, ADR-120, 2026-11-17) | implemented |


### ADR-005: Square adventure grid; square combat grid with multi-cell units
**Status**: accepted (2026-10-15); "reach" superseded by ADR-021/ADR-038 (size grants no reach: melee needs footprint adjacency)
The adventure map uses a square grid with 8-way movement (as in HoMM3). Battles use a **separate
battlefield** with a fine **square cell grid**: creature stacks occupy 2×2, 3×3 or 4×4 cells by size
class. This replaces HoMM3's hex grid. Size becomes a real tactical property (blocking, surround,
reach), and one coordinate type serves both map and battle maths.
Consequences: combat pathfinding has to test footprint clearance (precompute clearance maps per
size), and AI and UI must handle footprints rather than single cells.

### ADR-011: Smooth terrain over a logical square grid
**Status**: accepted (2026-10-15)
Rendering: heightmapped, splat-textured terrain. Logic: unchanged `AdventureMap` tiles. The map
format will grow a height/decoration layer that is purely visual. Core never reads it.

### ADR-012: Map objects = data definitions + per-kind instance classes
**Status**: accepted (2026-10-15)
Definitions (footprint, kind parameters) are data: built-in defaults in code, overridable/extendable
from JSON, which keeps the door open for modding. Behaviour is selected by `MapObjectKind` and lives in
`MapObjectInteractionService`. A plugin/behaviour registry is deferred until there are many kinds.

### ADR-013: Visiting is mandatory; routes end at entrances
**Status**: accepted (2026-10-15)
Stepping onto an entrance always visits the object, so the pathfinder never routes *through* one.
Gates are the exception: the route continues from the partner gate. Pathfinding state is
(tile, arrived-by-teleport) so a gate exit can be walked away from without bouncing back.

### ADR-016: Fog of war is per-player permanent shroud (provisional)
**Status**: superseded by ADR-017
Matches HoMM3. Stored in `GameState.Fog`, updated by `VisibilityService` reacting to movement events.
Re-fogging would add a "currently visible" layer computed from heroes, towns and mines.

### ADR-017: Fog mode is chosen per game; refog uses hysteresis
**Status**: accepted (2026-10-15)
`GameOptions.FogMode` is set at game creation and stored in `GameState` (it is a game rule, not
map data). `PermanentReveal` = HoMM3. `ShroudWithRefog` keeps terrain and objects explored, but other
players' units are only visible on tiles in sight. A tile enters sight within an observer's radius and
leaves only beyond radius + `RefogMargin`, so units at the edge of vision don't flicker. Sight is a
pure function of current observers and is recomputed per affected player, stored as a set (cheap even
on 256×256 maps). The planner ignores unseen heroes; real movement bumps into them.

### ADR-018: HoMM3 movement rules
**Status**: accepted (2026-10-15); `Hero.SlowestCreatureSpeed` replaced by the army's creature `MapSpeed` (ADR-037)
A step costs the terrain being left (×1.41 diagonally). Any positive remainder allows one last step.
Daily points come from the slowest creature's speed (HoMM3 table), then a percentage bonus (skills),
then a flat bonus (artifacts), recomputed at the owner's turn start (`MovementAllowance`). Until
armies exist `Hero.SlowestCreatureSpeed` is a stored placeholder; Phase 2 derives it from the army.

### ADR-020: Map size presets up to 256, max 8 players
**Status**: accepted (2026-10-15)
Square presets 48, 72, 96, 128, 160, 192, 256 (`MapSizes`); `AdventureMap` rejects dimensions above
256 and `GameState` more than 8 players. Data structures were checked for 256×256×2 levels: pathfinding
arrays are ~260k entries per search; fog sight is set-based.

### ADR-024: Terrain classes and HoMM3-style roads
**Status**: accepted (2026-10-15); the terrain classes are superseded by ADR-045, roads stand
Every terrain belongs to a class:
- **Open**: no penalty for anyone.
- **Rough**: a fixed penalty for all factions (e.g. +25 %).
- **Native**: no penalty for armies native to it, a penalty for everyone else.

Natives are defined per terrain in data (`TerrainDefinition`: class, base cost, native towns), not in
code, so new terrains and towns are content. Roads are a separate per-tile map layer (dirt 50, gravel 65,
cobblestone 75). Road cost replaces terrain cost, including native and rough penalties.

Consequences:
- The map format gains a road layer, with one character per tile like the terrain rows.
- Step cost depends on the hero's army, so `StepCost` takes the moving hero/army. Pathfinding stays
  per-hero.
- The random map generator lays roads between towns and through zone connections.

### ADR-026: Two-way and one-way gates, no random exits
**Status**: accepted (2026-10-15)
Two-way gates link a pair, on the same level or across levels (the former `SubterraneanGate`, now `TwoWayGate`).
One-way gates link an entrance to exactly one exit; several entrances may share an exit, and exits are
landing spots only. Random-exit portals are left out for now, so every link is fixed map data. That
keeps pathfinding exact and avoids hidden randomness. If they are added later, the exit must come from
the seeded game RNG and the pathfinder must stop at the entrance.
Implemented (2026-10-15) as planned: generalise `SubterraneanGate` into a two-way gate kind (subterranean gate and
two-way monolith become definitions), add `OneWayGateEntrance`/`OneWayGateExit` kinds, extend
`PathFinder` teleport edges to be directional, map format v3, and add an editor link tool and validation.

### ADR-027: Meeting heroes
**Status**: accepted (2026-10-15); enemy battles built (ADR-038)
Stepping onto another hero's tile is an interaction, not a move: the moving hero stays where it is.
- Own hero: exchange (opened as a `PendingDecision`-style exchange session, resolved by the UI/AI).
- Enemy: battle (Phase 2).
- Ally: no interaction for now (blocked).

The pathfinder lets a route *end* on an own or visible enemy hero, never pass through. This needs
**teams/alliances** in `GameState` (player → team) so "ally" is defined; free-for-all is the default
(every player their own team).

### ADR-031: Refog behaviour: auto-stop, ambush, last-known objects
**Status**: accepted (2026-10-15)
In `ShroudWithRefog`:
- `MoveAlong` stops after a step when a previously unseen enemy hero comes into sight
  (new `MoveResult.EnemySpotted`).
- Bumping into an unseen enemy hero starts a battle (ambush) instead of returning `Occupied`.
- Players see the **last known state** of out-of-sight objects. Each player keeps a per-object
  snapshot (owner, present/removed), refreshed when the object is in sight. Views read that snapshot,
  never live state, for objects out of sight.

### ADR-032: Teams, native terrain, Pathfinding, roads
**Status**: accepted (2026-10-15); native terrains and costs replaced by ADR-045
- Teams: maps/templates define default teams; setup may change them unless the map locks them.
  Stored in `GameState` (player → team id) and map player slots.
- Terrain: three new native terrains: **highlands** (Haven), **forest floor** (Sylvan), **wasteland**
  (Necropolis). Each needs a terrain code, colour and editor brush. A terrain is native to an army
  only if **every** creature in it belongs to that terrain's town.
- The Pathfinding skill reduces non-native and rough penalties by 25/50/75 %.
- Roads: the road cost applies only road → road (HoMM3).

### ADR-039: Treasure chest rolls
**Status**: accepted (2026-10-15)
Unfixed chests roll from the game RNG when opened: 1000 gold or 500 XP / 1500 or 1000 / 2000 or 1500,
equally likely (HoMM3's tiers without the artifact outcome, normalised). Values fixed in the editor override
the roll.

### ADR-040: Neutral guards: two guard modes, deterministic dispositions
**Status**: accepted (2026-10-15); the guard mode became a per-stack setting (ADR-065)
- Monsters are map objects (`MonsterGuard`: creature, count, disposition, never-flees). Their tile is the
  entrance (attacked from where the hero stands) and they guard the 8 tiles around them. The pathfinder ends a
  route on a guarded tile and never passes through one.
- **The guard mode is a map setting chosen in the editor** (user decision). `Classic` = HoMM3 zone of control:
  entering a guarded tile starts the encounter, but pickups (piles, chests) on guarded tiles can be taken from
  outside without a fight ("stealing"). `Area` = the whole 3×3 area is guarded, pickups included. In both modes
  the intercepted hero stays where it was.
- Dispositions use HoMM3's names without randomness. R = hero army power ÷ guard power. A free join is offered
  when the army has room and the guard is Compliant (always), Friendly (R ≥ 1), Aggressive (R ≥ 2) or Hostile
  (R ≥ 4); Savage never joins. Otherwise the guard offers to flee when R ≥ 3, unless it never flees. Otherwise
  battle. Offers are a `GuardEncounterDecision`, answered with `RespondToGuardCommand` (accept = join, or let it
  go without XP; decline = fight).
- Counts left open are rolled at game start from the creature tier: T1 20–50, T2 12–25, T3 8–16, T4 4–10,
  T5 1–4. In battle a guard splits into min(count, 5) stacks.
- Mine guards (ADR-034) always fight; beating them flags the mine for the winner.

### ADR-045: Sandbox terrain model: every terrain penalises non-natives
**Status**: accepted (2026-10-16); replaces the classes of ADR-024 and the natives of ADR-032
- **Costs** (user decision, from the sandbox `TerrainExtensions`): a step costs the terrain being left, **100 for an
  all-native army** and the terrain's cost for everyone else. No terrain is free for everyone; only roads (ADR-024),
  the Pathfinding skill and artifacts reduce the penalty.

  | Non-native cost | Terrains (native towns) |
  |---|---|
  | 125 | grass (Haven, Sylvan), highlands (Haven, Conflux), forest floor (Sylvan), cursed lands (Necropolis), subterranean (Dungeon), sand/desert (none), beach (none) |
  | 150 | dirt (none), rough (Stronghold), wasteland (Stronghold), lava (Inferno), rocky ground (Dragon Utopia) |
  | 175 | ice (Academy), shallow water (Swamp Bestiary) |
  | 200 | snow (Academy), swamp (Swamp Bestiary) |
  | impassable | water (deep; no boats, ADR-051), rock (cliffs, borders) |
- **A town may be native to several terrains** (`TerrainDefinition.NativeTowns`); the whole army must be native
  (ADR-032 stands). Changes against ADR-032: Necropolis moves from wasteland to **cursed lands**, Stronghold from sand
  to **rough + wasteland**; sand becomes a neutral desert.
- **New terrains**: beach `b`, ice `i`, shallow water `=` (walkable), cursed lands `c`, rocky ground `k` (walkable;
  rock stays the impassable cliff). Map format **v5** (new codes only; v1–v4 still load). `TerrainClass` is now
  `Passable`/`Impassable`. Ice and shallow water cost 175 as in the sandbox core (its 3D client said 200).
- Consequence: heroes with no army or a mixed army pay at least 125 everywhere, so off-road daily ranges shrink by
  about a fifth and roads matter more.

### ADR-051: No sea travel
**Status**: accepted (2026-10-16), from [ideas.md](../ideas.md) (user decision)
- Water bodies are **obstacles only**: deep water stays impassable, and there are no boats, shipyards, sea travel or
  navigation skills (no Navigation-like skill in any group). Shallow water stays walkable terrain (ADR-045).
- Replaces "boats later" (ADR-045) and the planned boats/shipyards map objects. Random maps already left out water
  zones and islands for 1.0 (ADR-034).

### ADR-065: Guard mode per monster stack (2026-10-17)
**Status**: accepted (2026-10-17), user decision on RMG-2; amends ADR-040
- **Context**: ADR-040 made the guard mode a map-wide editor setting: Classic (HoMM3 zone of control, pickups on guarded tiles can be stolen from outside) or Area (the whole 3×3 area is
  guarded). The user: Classic is the general-purpose mode, Area is mostly for guarding treasure so it cannot be picked up without a fight.
- The guard mode is a **per-stack** setting (`MonsterGuard.Mode`, default Classic); the map-wide setting is dropped (`GameState.GuardMode`, `SetGuardMode`, `GuardModeChanged` removed).
- Movement: a pickup on a guarded tile can be taken from outside only if every stack guarding it is Classic; otherwise the first Area stack (lowest id) fights. Any other entry into a
  guarded tile meets the first guard, as before. Pathfinding is unchanged.
- The AI counts only Area guards as opponents for pickups. Mine guards are unaffected (no mode; they defend only their mine).
- Map format **v13**: monsters get an optional `mode` (omitted = Classic); the top-level `guardMode` is no longer written, and maps before v13 apply it to every monster.
- Editor: `MapEditorSession.SetMonsterGuardMode` (undoable, publishes `MapObjectChanged`); the control moved from map properties to the monster rows of the inspector.
- Generator: passage and gate guards are Classic, treasure guards Area (ADR-064).

### ADR-068: The adventure-map object catalogue (2026-10-19)
**Status**: accepted as a design (2026-10-19), user notes in `ideas.md`; **nothing implemented**. Numbers are drafts; unclear points are MAP-1 … MAP-13 in `open-questions.md`.
- **Content**: about 60 objects in seven groups (resource providers, one-time loot, creature dwellings with guards, creature banks, permanent power-ups, temporary buffs, utilities), written out in
  [adventure-map.md](../../docs/design/adventure-map.md). The mines, resource piles, treasure chest, artifact pickup, map dwelling and marketplace already exist; the rest are new.
- **Artifact tiers** (user decision, MAP-1): Minor, Major, Relic as in `artifacts.csv`; the notes' Treasure tier is dropped (every (t) became Minor). A Combo (combination) artifact is never dropped, only assembled; its pieces drop. Relics come from a pool; an empty pool is refilled, so duplicates are fine.
- **Visit scopes** (user decision): weekly (per object, resets on Monday), once per hero (visited-by set on the object), first visitor, vanishing. **Temporary effects** are a hero status that expires
  after the next battle, at the end of the day or of the week.
- **Creature banks** (user decision): a new object kind, guards always fight (no join or flee), reward only on victory, the guard tier is rolled from the game RNG at map start or fixed by the map author,
  then the bank is plundered.
- **Creature names**: the notes used HoMM3 names; they are mapped to our roster (Zombie → Ghoul, Power Lich → Arch Lich, Dwarf / Battle Dwarf → Defender / Shield Guard, Medusa Queen → Putrid Medusa,
  Cavalier → Lancer). Dwarf is a placeholder until CONT-2.
- **Treasure chest** gets the five-row table of the notes (including a 5 % Minor artifact) in place of the three equal tiers of ADR-039 (ADR-039 is superseded on this point). **Dwellings of tier 3 / 4 / 5** are guarded by 9 / 6 / 3 base
  creatures, which settles the dwelling-guard part of TOWN-5.
- **Marketplace rounding** (TOWN-2): what the player gives is rounded up, what the player receives is rounded down. The implemented rule (buy price up, sale gain down) already does this.
- **Implementation order** is the roadmap block "Adventure map objects": shared visit rules and hero statuses first, then the groups in the order resource providers, loot, buffs, trainers, banks, utilities.
  Map format bumps follow CLAUDE.md (new version, older versions keep loading).
- **Answers on 2026-10-19** (MAP-1 … MAP-4): abandoned mines are separate objects that turn into the normal mine once the guard is defeated (the guard count is rolled at map start; a normal mine keeps its free
  guard choice); Mystical Garden is a random reward, 500 gold or 5 of one random magic resource, no choice; Campfire gives gold plus 4–6 of one random resource; Supply Crates give two different resources, 4–6
  each; Lean To one kind; Resource Pile and Traveler's Wagon amounts already match.
- **Answers on 2026-10-19, second round** (MAP-5 … MAP-8): Prison levels 1 / 5 / 10 / 20 / 30 at 30 / 30 / 20 / 15 / 5 %, faction drawn at map generation, never a player's chosen start hero, the visit fails at the hero limit,
  a higher level gets experience and game-chosen level-ups. Banks: "N each", default battle width, hidden and fixed-by-RNG guard tier (an author cannot set it), a retreat or defeat leaves the guard intact, only the
  Churchyard costs −1 morale (next battle) on later visits, banks refill 4 weeks after plundering (details MAP-14). Gazebo and Library once per hero; the Library does nothing below level 10.
- **Hero limit** (user decision): the template editor and the map generator get a setting for the **maximum number of heroes per player, 1 to 8** (today a fixed 8, ADR-060). It becomes a map rule (map format bump)
  that hiring, the tavern and the Prison respect.
- **Implementation (2026-10-19), steps 1 and 2 of the roadmap block**:
  - Visit scopes: `MapObjectDefinition.VisitScope` (`Unlimited` for every existing kind, also in definition JSON), `MapObject.Visits` (`VisitLog`: the
    absolute week of the last collection and the heroes that collected), `MapObjectInteractionService.TryCollect` (publishes `MapObjectCollected`).
    Weekly means once per object per calendar week (`GameDate.AbsoluteWeek`). The log is not in the map format: games replay their commands.
  - Hero statuses: `HeroStatus(Source, Kind, Value, Expires)`, kinds Morale, Luck, Movement (flat daily points), expiry NextBattle (ended by
    `BattleService` for both heroes, any outcome), EndOfDay (the owner's next turn start), EndOfWeek (`WeekStarted`). Granted and expired by
    `ProgressionService`, events `HeroStatusGained` and `HeroStatusesExpired`. Same source and kind replaces; other sources add (MAP-10 open).
    An immediate movement bonus ("+400 until the end of the day") will be a direct grant with the objects, not a status.
  - Rolls: `WeightedRoll.Pick` (weights need not add up to 100) and `ArtifactService.Draw(tier)`: Minor and Major may repeat; a Relic is never one
    drawn since the last refill nor one on the map or carried, and an empty pool refills (`GameState.DrawnRelics`).
  - Digest: statuses, visit logs and drawn relics are written only when present, so older replays keep their digests.
  - Hero limit: `ScenarioRules.MaxHeroes` (1–8, `GameState.SetRules` refuses others), `GameState.HeroLimitReached` used by the tavern; map format
    **v15** (`rules.maxHeroes`, omitted = 8); the editor's conditions panel has the spin box (undoable through `SetScenarioRules`); `MapValidator`
    warns when a player starts with more heroes than the limit (assumed: allowed, it only stops hiring); template format **v5** (`maxHeroes`,
    validated 1–8); `ScenarioAssembler`, `MapGenerator.Build` and `EditorMapGenerator.Generate` take an optional `maxHeroes` override. Assumed until
    MAP-14: one limit for every player.
- **Implementation (2026-10-19), step 3: resource providers**:
  - One object kind `ResourceProvider` (2×2, entrance bottom right), every definition `Weekly`; the definition's `Provider` rule (WaterWheel,
    Warehouse, Windmill, MysticalGarden) plus `Resource` and `Amount` for warehouses say what it gives (`ResourceProviderRules.Roll`). Built-in ids
    `provider.water-wheel`, `provider.windmill`, `provider.mystical-garden`, `warehouse.<resource>` (lumber and ore 8, magic 5, gold 2000).
  - Water Wheel: 500 gold in the game's first week (`AbsoluteWeek` 1), 1000 later. Windmill: 3–6 of one of the five non-gold resources, evenly.
    Mystical Garden: even odds of 500 gold or 5 of one magic resource, no choice.
  - Warehouses of a resource other than gold offer it or 500 gold as a `ResourceChoiceDecision`, answered with `ChooseResourceRewardCommand`
    (replay name `resourceReward`); the week is taken when the visitor chooses. That the visitor chooses is the design doc's reading of the notes.
  - Events `ResourcesFound` (paid out) and `MapObjectFoundEmpty` (already taken). Map format **v16** (`provider` objects, no per-instance data;
    the weekly log is game state). Editor placement through the generic `PlaceObject`; the Godot views label them; the decision panel shows the
    warehouse choice (not run in Godot yet).
  - AI: visits a provider it may collect from, valued by its resource values (Windmill and Garden at their average), and takes the more valuable
    warehouse option. Not yet: the random map generator does not place providers.
- **Answers on 2026-10-19, third round** (MAP-9, MAP-10): the Tree of Knowledge has no free option, the player pays 2500 gold or 5 crystal + 5 mercury + 5 sulfur,
  once per hero per tree; buffs of several objects stack within the ±5 cap, the Temple's +3 is on day 7 of the week, a buff is the visiting hero's only and
  ends after any battle (already so: every battle ends in `BattleService.Finish`).
- **Implementation (2026-10-19), step 4 and the tomb: one-time loot**:
  - One object kind `Loot` (`LootSite`), every definition `FirstVisitor`; the definition's `Loot` rule says what it gives (`LootRules.Roll` → resources and an
    artifact tier, drawn with `ArtifactService.Draw`). Ids `loot.campfire`, `loot.supply-crates`, `loot.travelers-wagon` (single tile, vanish, and count as
    pickups that Classic guards let be stolen), `loot.corpse` (single tile), `loot.lean-to` (2×1), `loot.warriors-tomb` (2×2), which stay.
  - Campfire gold is 400, 500 or 600 (in hundreds, a reading of "400–600"). Supply Crates: two different resources. A later visitor of a site that stays gets
    `MapObjectFoundEmpty`. The Warrior's Tomb gives every visitor, the first included, a −3 morale `NextBattle` status (source `loot.warriors-tomb`, so a second
    visit renews it rather than adding).
  - Treasure chest: `TreasureChest.Odds` (30 / 30 / 25 / 10 / 5); the artifact row takes a Minor artifact at once and empties the chest, without a decision.
    Chests with fixed values (author, generator) never hold an artifact.
  - An artifact that does not fit in a full backpack is lost (`ArtifactBackpackFull`); `ArtifactSource.Found` marks artifacts from chests and loot.
  - Event `LootFound(Hero, Object, Resources, Artifact)`. Map format **v17** (`loot` objects). Editor placement is generic; the AI values loot sites (a spent
    one is no target). Not yet: the generator does not place loot sites; resource piles keep fixed default amounts.
- **Answers on 2026-10-19, fourth round** (MAP-11 … MAP-14): Hill Fort at 150 % of the upgrade price, stack by stack, tiers 1–3 only; Town Gate to the owner's
  towns only, picked from a list where an occupied entrance shows as unavailable, no route planning; War Machine Factory sells the visitor's own offensive
  machine, the cart and the tent at the blacksmith's prices, never one the hero has; a bank's refill after 4 weeks re-rolls its tier and brings the whole
  reward back, for every bank. Hero limit: one number for all players from the generator; the editor may set a player's own limit (campaign-like maps);
  the editor refuses a hero or a limit that would break it; otherwise the surplus is removed. This supersedes the "allowed with a warning" assumption.
- **Implementation (2026-10-19), hero limit follow-ups**: `Player.MaxHeroes` (null = the map's) through `GameState.SetHeroLimit`, `GameState.HeroLimit(player)`;
  map format **v18** (`maxHeroes` on a player slot); `MapEditorSession.SetPlayerHeroLimit`, `PlaceHero` and `SetScenarioRules` refuse with the new
  `EditResult.HeroLimit` (`SetScenarioRules` now returns an `EditResult`); the players panel has the spin box (0 = the map's). `GameFlowService.RemoveSurplusHeroes`
  runs first in `GameSession.Start` and removes the newest heroes beyond a limit (`HeroRemoved`); the validator's warning says so.
- **Implementation (2026-10-19), steps 5 and 6: buffs and power-ups**:
  - New visit scopes `DailyPerHero` and `WeeklyPerHero` (the log keeps each hero's last day). Buff sites (kind `Buff`, `BuffSite`, `BuffRules`): Faerie Ring,
    Fountain of Youth, Temple, Mineral Spring and Magic Well once a day per hero, the Stables once a week per hero. Statuses as decided; movement now is a
    direct grant (`HeroMovementGained`); the Stables also add a +400 daily-movement status until the week ends and upgrade `haven.lancer` stacks to their
    upgrade on every visit; the Magic Well fills the mana (`HeroManaRestored`).
  - Training sites (kind `Training`, `TrainingSite`, `TrainingRules`), once per hero: Learning Stone +1000 XP; Library +2 to all four stats from level 10
    (below: `MapObjectRefused`); Arena and Colosseum ask through a `TrainingDecision` that must be answered; Gazebo (1500 gold → 2500 XP) and Tree of
    Knowledge (2500 gold or 5 crystal + 5 mercury + 5 sulfur → the next level's experience) offer only what the player can pay and may be declined
    (`ChooseTrainingCommand` with no option, replay name `training`); nothing payable → `MapObjectRefused`. A visit that trains nothing does not count.
    Stats are raised through `ProgressionService.RaiseStat` (`HeroPrimaryStatRaised`).
  - Map format **v18** (`buff` and `training` objects), definition JSON fields `buff` and `training`. All definitions are 2×2 with the entrance bottom right.
    Decision panel: training buttons and "Leave". AI: values buffs low (the Magic Well not at all until there are spells) and training by use (the Library
    from level 10, paid ones with 3000 gold to spare), picks the first stat, pays gold when rich, else magic resources, else leaves.
- **Implementation (2026-10-19), abandoned mines and dwelling guards**:
  - Abandoned mines are `Mine` objects of their own definitions `mine.abandoned.<resource>` (same footprint and yield), whose new definition fields say
    what they become (`Becomes`), who guards them (`GuardCreature` `dungeon.troglodyte`, `GuardMin`–`GuardMax`) and where they are fought (`BattleTerrain`
    Subterranean for gold and ore). `GuardService.RollUnsetCounts` gives an unguarded abandoned mine its Troglodytes when the game starts (a map's own guard
    is kept). Beating the guard replaces the object with the normal mine (same id, position, owner; `GameState.ObjectCatalog`, new and passed by the map
    converter) and flags it; `MapObjectChanged` follows, and `VisibilityService` now refreshes what players remember on it. No map format change.
  - The notes' "all native terrain bonuses": battles have no terrain bonuses yet, so only the battlefield's terrain changes (roadmap item).
  - Dwelling guards: `CreatureDwelling.Guard`, set at game start for an unowned dwelling of tier 3–5 (`DwellingGuards.Count`: 9 / 6 / 3 of its base creature).
    The guard always fights (`GuardService.Encounter`); a hero without an army cannot enter; beating it clears the guard and flags the dwelling. Game state only.
    The AI weighs the guard as the opponent at the dwelling.
- **Implementation (2026-10-19), utilities and the prison** (map format **v19**):
  - Sign (`SignPost.Message`, map data, `MapEditorSession.SetSignMessage`, inspector text field): a visit publishes `SignRead`.
  - Observatory: every visit publishes `SurroundingsRevealed`; `VisibilityService` reveals radius 20 around its entrance (explored terrain, as any
    reveal) and records the hero (`OncePerHero` log, which the AI reads to go there once).
  - Hill Fort: `UpgradeAtHillFortCommand(Player, Fort, Slot)` for the player's hero on the entrance; `HillFortRules`: tier 1–3 creatures with an
    upgrade, 150 % of the price difference rounded up per resource (no Stewardship); `HillFortUpgraded` + `ArmyChanged`.
  - Town Gate: a `TownGateDecision` lists the owner's towns (`TownGateOption`, unavailable when a hero stands at the entrance);
    `ChooseTownGateCommand(Player, Town?)` moves the hero there (`HeroTeleported`, no movement cost; a hero with Stewardship takes a governor-less
    town as usual) or stays. A player without towns is turned away (`MapObjectRefused`).
  - War Machine Factory: `BuyWarMachineAtFactoryCommand(Player, Factory, Slot)`, the blacksmith's prices (`WarMachines.Cost`) and the hero's own
    offensive machine; refused for a machine the hero has. `WarMachineBought`'s first field is now `Seller` (a town or a factory).
  - Prison (`Prison.Hero`, `Prison.Level`, map data; `SetPrison` and inspector rows): `PrisonRules.Prepare` fills what the map left open when the
    game starts (a random playable faction, then one of its heroes neither on the map nor in another prison, else any; the level by 30 / 30 / 20 /
    15 / 5 %). A generated map would do the same at generation once the generator places prisons. Freeing: refused with a notice at the hero limit;
    otherwise the prison goes and the hero (roster stats, no army) stands where the prison stood, with the level's experience and the game's level-up
    choices (`ProgressionService.LevelUpAutomatically`), moving from its owner's next turn (`HeroFreed`). The validator checks the prisoner, the
    tile the hero will stand on, and warns when the prisoner is already on the map.
  - UI: `ObjectServicePanel` (Hill Fort upgrades, factory purchases) opens on a visit; the decision panel lists the town gate's towns; a
    `NoticePanel` shows signs, refusals, emptied objects and freed prisoners. Replay names `townGate`, `hillFort`, `factory`.
  - AI: frees prisoners and visits an observatory once per hero; answers a town gate by staying; does not use hill forts or factories yet.
  - Assumed (not in the notes): the freed hero has no army (only its roster stats and skills), and the Prison's footprint is two tiles so the hero
    has a place to stand.
- **Implementation (2026-10-19), creature banks** (map format **v20**, `bank` objects without instance data):
  - Kind `CreatureBank`, definitions `bank.<name>` (3×2), `BankRule` and `CreatureBankRules.Tables` (the notes' tables with the roster's creatures:
    Ghoul, Vampire Lord, Skeleton / Wight / Wraith / Skeleton Warrior / Arch Lich, Defender / Shield Guard (placeholder, CONT-2), Imp / Familiar,
    Medusa / Putrid Medusa, Naga / Naga Queen). `GuardService.RollUnsetCounts` rolls each bank's tier when the game starts (30 / 30 / 30 / 10, the
    Churchyard 100), and with an upgrade a coin flip turns a fifth (rounded down) of the guard into the upgraded creature.
  - A visit to a full bank starts `BattleService.BeginBankBattle`: the guard always fights, at the default width (no Tactics choice); each kind of
    creature is split into a few battle stacks, at most seven in all. Winning plunders it (`Plunder`: resources to the winner, the artifact drawn by
    tier and lost to a full backpack, `CreatureBankPlundered`); losing or fleeing leaves the guard whole. `BattleService` now takes the `EconomyService`.
  - A plundered bank gives nothing (`MapObjectFoundEmpty`); the Churchyard's costs −1 morale for the next battle. `GuardService.RefillBanks` runs on
    every new day and refills a bank 28 days after it was plundered, with a new roll (`CreatureBankRefilled`, `MapObjectChanged`).
  - The Ruins publish `SurroundingsRevealed` (radius 12) on every visit; `ObservatoryUsed` was renamed to `SurroundingsRevealed` for both.
  - AI: a full bank is a target worth its reward (+1500 for an artifact) against the guard as the opponent, so the simulated fight decides.

- **Implementation (2026-10-19), random maps** (`MapGenerator.Version` **3**: the same seed gives another map than version 2):
  - `TreasurePlacer` turns one item in 3 (`SiteOdds`) into an object of the catalogue: a `TreasureSite` from `TreasureSites.All` worth between half
    the drawn value and the value, drawn by weight within its zone limit (1, loot 2) and map limit (the Town Gate once). When nothing fits or there is no
    room, the item is a pile, chest, artifact or marketplace as before. No template field: every template gets them, and the bands decide.
  - Values are placeholders (gold equivalents of the rewards; weekly providers count about two weeks). A site is guarded like other treasure by its own
    value; creature banks and abandoned mines get no guard beside them, since they fight for themselves. Banks, abandoned mines and prisons are rolled
    when the game starts, as on hand-made maps.
  - Not placed: signs (no text to give) and map dwellings (zone rules later).
- **Implementation (2026-10-19), follow-ups** (map format **v21**):
  - **Native terrain in battle** (decided: the HoMM3 rule): a creature of a town that the battlefield's terrain lists as native
    (`TerrainDefinition.NativeTowns`) gets +1 Attack, +1 Defense and +1 Speed (`BattleState.IsNative`, `NativeAttack` / `NativeDefense` /
    `NativeSpeed`; `DamageCalculator` and `SpeedOf`). Each stack counts on its own; neutrals and war machines never. An abandoned gold or ore mine
    fights underground, so its Troglodytes (Dungeon) are native there: the notes' "all native terrain bonuses".
  - **Dwarf** (decided, closes the banks' part of CONT-2): a new neutral tier-2 line, `neutral.dwarf` (A7 D8, 3–5, 28 HP, speed 8, SpellResistance 2)
    → `neutral.battle-dwarf` (A8 D10, 3–6, 30 HP, speed 9, SpellResistance 3, the most the format allows); draft numbers. The Dwarven Treasury uses
    them instead of the Defender / Shield Guard.
  - **Resource piles**: `ResourcePile.Amount` is optional; an open amount is rolled when the game starts (`GuardService.RollUnsetCounts`):
    5–10 lumber or ore, 3–7 magic, 500–1000 gold in hundreds. Editor placements and the generator's extra piles leave it open; the generator's
    treasure piles stay fixed by their value; a map file leaves `amount` out (v21), and every older file has one, so old maps keep theirs.
  - **AI**: a hill fort is a target worth the town price of the upgrades the hero can pay for with a third of the gold kept; a factory 300 per
    machine the hero lacks and would buy in a town (its offensive machine with Advanced Ballistics, a tent, a cart for shooters) with 1500 gold kept;
    a town gate is worth 90 % of the most needed own town (defence or troops waiting, the same need that sends heroes to towns) with a free
    entrance, and the gate's picker takes the hero there. Once on the entrance, the hero upgrades or buys.

### ADR-077: Map scenery, rivers, flaggable providers, resource caravans, caravan speed and limits, kingdom overview, Thieves' Guild, third object catalogue (2026-10-26)
**Status**: accepted as a design (2026-10-26), owner notes in `ideas.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of ADR-054
and ADR-075 (caravan travel time) and ADR-068 (resource providers are visited, not flagged). The open points are MAP-25 … MAP-34 and TOWN-9 …
TOWN-11 in `open-questions.md`, each with a proposal.

- **Map scenery**: static objects that only block and decorate: rocks, trees, dead trees (several sizes and kinds), ponds, bushes, mushrooms,
  flowers, fissures, mounds, ruined buildings, creature bones, castle walls. The generator places them as a zone's obstacles (today rock
  terrain clumps); the editor paints them. Which ones block is MAP-25.
- **Rivers**: like HoMM3 (MAP-26).
- **Resource providers become flaggable** like map dwellings: the Water Wheel, the Windmill and the Warehouses (whether the Mystical Garden too is
  part of TOWN-9). A town's **caravan** picks up their resources the way it hires creatures from flagged dwellings (TOWN-9).
- **Caravan speed**: a hired caravan, with creatures or resources, moves **2500 movement points a day**, ignoring terrain penalties (replacing
  the creature's map speed, ADR-075). With the 7500-point reach that is at most 3 days (TOWN-10).
- **Caravan limit**: a town has at most **6 caravans** ordered to it at once, **8** while it has a governor; further orders queue (TOWN-10).
- **Kingdom overview**: a screen with the player's whole economy: towns, mines, providers, artifacts and heroes that bring income, with totals.
- **Thieves' Guild** in the tavern, like HoMM3: how the player ranks against the others by towns, heroes, gold, lumber and ore, rare resources,
  artifacts, income, army strength, best hero and best creature (TOWN-11).
- **Third object catalogue** (adventure-map.md): Sphinx (MAP-27), Sanctuary (MAP-28), Sacrificial Altar (MAP-29), Refugee Camp (MAP-30), Den of
  Thieves (TOWN-11), Cartographer (MAP-31), Astrologer's Tower (MAP-32, needs the week events), Artifact Merchant (MAP-33), Witch Hut (MAP-34).
  The Subterranean Gate exists already (two-way gate between levels).

### ADR-079: Flaggable providers, resource caravans, caravan speed and limit, Thieves' Guild, sanctuary, rivers and scenery (2026-10-26)
**Status**: implemented (2026-10-26). The owner's answers to TOWN-9, TOWN-10, TOWN-11, MAP-25, MAP-26 and MAP-28 (ADR-077's questions);
supersedes ADR-068's weekly visits of resource providers and ADR-075's travel time by the creatures' map speed. Map format **v22**; random map
generator version **4** (rivers and scenery change every generated map). Choices made here are marked *(choice)*.

- **Flaggable providers** (TOWN-9): the Water Wheel, the Windmill, the six Warehouses and the Mystical Garden are flagged like mines
  (`ResourceProvider.Owner`, `IFlaggable`, scouting radius 2). While flagged, a week's output is added to `Stored` at the start of every week
  (`EconomyService.StockProviders`, after the towns' growth; also at game start for providers owned from the start *(choice)*); it piles up.
  The first flagging of a neutral provider gives one week's output at once (`WasClaimed`); a later capture takes what the former owner left
  *(choice: no extra week)*; a defeated player's providers turn neutral and keep their store. An own hero's visit takes everything stored
  (`MapObjectFoundEmpty` when empty). The Warehouses' choice is gone: each week is, at random, the resource or 500 gold
  (`ResourceChoiceDecision` and `ChooseResourceRewardCommand` removed, so **replays that used `resourceReward` no longer load**). Events
  `ProviderOwnerChanged`, `ProviderStockChanged`. The editor sets an owner (`SetProviderOwner`); format v22 `owner` on `provider` objects.
- **Resource caravans** (TOWN-9): `FetchResourcesCommand` sends a town's caravan for everything stored at an own provider in the caravan's
  raw reach (`CaravanRules.Providers`, `CheckFetch`): a flat 100 gold (placeholder, `ResourceCaravanCost`), the same reach and open-route rules
  as creature caravans, and it counts towards the limit. A `Caravan` with `Cargo` (no creature); on arrival the cargo joins the owner's
  resources (no garrison slot needed); a blocked route puts the cargo back in the store and refunds the 100 gold, unless the provider changed
  hands (then the cargo is lost *(choice)*). The caravan events carry an optional `Cargo`. The AI fetches stores worth five times the price.
- **Caravan speed and limit** (TOWN-10): every caravan moves 2500 movement points a day at the plain step cost (terrain, roads and rivers
  ignored), so the 7500-point reach takes at most 3 days. A town takes at most 6 caravans at once (8 while it has a governor), counting those
  on their way and those waiting at the gates; troops sent between own towns count at the destination. Orders at the limit are refused with a
  message (`CaravanRules.Limit`, `CheckLimit`).
- **Thieves' Guild** (TOWN-11): `ThievesGuild.Table` ranks every player still in the game (computers and teammates too) row by row; equal
  values share a place. Rows by the player's taverns: 1 — towns, heroes; 2 — + gold, lumber and ore, rare resources; 3 — + artifacts, income;
  4 or more — + army strength (creature value of armies and garrisons), best hero (highest level), best creature (most valuable). A "Thieves'
  guild" button on the town screen of a town with a tavern opens `ThievesGuildPanel` (not run in Godot yet). The **Den of Thieves** map
  object (`den-of-thieves`) shows the whole table on every visit (`ThievesDenVisited`); nothing is kept.
- **Sanctuary** (MAP-28, `sanctuary`): a hero standing on its entrance cannot be attacked; an enemy stepping in is refused
  (`MapObjectRefused`, `MoveResult.Occupied`) and the AI does not target it. A hero may stay as long as it likes, one at a time; nothing else.
- **Rivers** (MAP-26): an overlay like roads (`RiverType` Clear, Icy, Muddy, Lava; looks only). Leaving a river tile costs 25 % more
  (`RiverRules.PenaltyPercent`; Pathfinding does not cut it *(choice)*); a road-to-road step ignores the river (a bridge). Format v22 `rivers`
  layer (`RiverCodes`); editor River brush (`PaintRiver`, `RiversPainted`); the 3D view draws them under the roads. The generator
  (`RiverPlacer`, after the roads) runs a river through about half the zones, from the foot of one wall to the farthest foot of another, over
  free ground, its look from the terrain where it starts *(choice: there are no lakes yet, so it joins borders)*.
- **Scenery** (MAP-25): a `Scenery` object kind from `src/Core/data/scenery.csv` (`SceneryTable`): rocks, trees, dead trees, ponds, bushes,
  mushrooms, flowers, fissures, mounds, ruins, bones and castle walls, every kind in two or more sizes, every cell blocking, no entrance
  (`Footprint.Obstacle`). Walls are 1×N straight pieces: horizontal and vertical (2–4) and diagonal both ways (2–3). Each row lists the
  terrains it suits. The generator (`SceneryPlacer`, right after the terrain) gives each rock clump back the ground around it and fills exactly
  the clump's tiles with fitting scenery, larger pieces first (a small rock where nothing else fits), so connectivity stays as checked.
  Format v22 `scenery` objects; the editor places them from the object palette.
- Tests: `ResourceProviderTests`, `CaravanTests`, `ThievesGuildAndSanctuaryTests`, `RiverTests`, `SceneryTests`.

### ADR-080: The third object catalogue: cartographer, artifact merchant, refugee camp, sacrificial altar, sphinx, witch hut (2026-10-26)
**Status**: implemented (2026-10-26), except the Astrologer's Tower (waits for the week events). The owner's answers to MAP-27 and MAP-29 …
MAP-34 (ADR-077); the Den of Thieves and the sanctuary are in ADR-079. Map format v22 (same bump as ADR-079). Choices made here are marked
*(choice)*; numbers are drafts.

- **Cartographer** (MAP-31): `cartographer.surface` and `cartographer.underground` (`RevealsLevel`). A visit asks the visitor's player whether
  to buy the map of that level for **3000 gold** (`CartographerDecision`, `AnswerCartographerCommand`); buying reveals the whole level
  (`MapBought`, then `SurroundingsRevealed` over the level). Once per player; the underground one on a single-level map, or a player short of
  gold, is refused (`MapObjectRefused`). The generator places at most one surface cartographer *(choice)*; the AI buys with three times the
  price in hand.
- **Artifact merchant** (MAP-33, `artifact-merchant`): 7 artifacts, each Minor or Major at even odds *(choice)*, rolled at game start and on
  the first day of every month (`MerchantRestocked`); price twice the artifact's worth, with worth by tier Minor 2000, Major 5000
  (`ArtifactMerchantRules`, *choice: there were no artifact prices*); a sold slot stays empty until the restock. The hero on the entrance buys
  (`BuyArtifactCommand`, `ArtifactBought`, `ArtifactSource.Bought`) with room in its backpack.
- **Refugee camp** (MAP-30, `refugee-camp`): every week (and at game start) a random base creature of tiers 3–5 from the playable towns
  (`RefugeeCampRules.Candidates`) with one week's growth (`RefugeesArrived`); unsold ones leave. Bought by the hero on the entrance at the
  normal price, no Stewardship discount *(choice)* (`RecruitAtRefugeeCampCommand`, `DwellingRecruited`). Not flaggable.
- **Sacrificial altar** (MAP-29, `sacrificial-altar`): by the hero's town (`ProgressionService.TownOf`): Necropolis, Inferno and Dungeon
  heroes sacrifice creatures for their hit points × 2 in experience (`SacrificeCreaturesCommand`; the hero keeps at least one creature
  *(choice)*); Haven, Sylvan and Academy heroes artifacts, worn or carried, for 2000 / 5000 / 10 000 by tier (`SacrificeArtifactCommand`);
  Stronghold and Swamp Bestiary heroes either. Any number of times (`SacrificeMade`, then the experience and any level-ups).
- **Sphinx** (MAP-27, `sphinx`): one riddle per hero (`VisitScope.OncePerHero`): the map author's (`question`, `answer`, `wrong1`, `wrong2` on
  the object, all or none) or one drawn from `src/Core/data/riddles.csv` (`RiddleTable`, ten drafts). Three answers, the right one's place
  rolled (`SphinxDecision`, `AnswerSphinxCommand`). Right: a Major artifact or 5000 experience at even odds (a full backpack loses the
  artifact). Wrong: the hero's movement points drop to 0 for the day and it gets −3 morale for its next battle (a `HeroStatus`). Event
  `SphinxAnswered`. The AI answers the first option.
- **Witch hut** (MAP-34, `witch-hut`): two skills of two groups, any group (general, school or faction), set in the map (`skills`) or rolled
  at game start. A visit offers each skill the hero can raise; where it cannot (maxed, or no free group), the first skill of the same group it
  can raise stands in (`WitchHutRules.Options`). Choosing raises it a level (Basic when new) (`WitchHutDecision`,
  `ChooseWitchHutSkillCommand`, `WitchHutSkillLearned`); a hero takes one witch hut lesson per hero level (`Hero.WitchHutLevel`), leaving
  without choosing uses nothing. The AI takes Necromancy when offered, else the first option.
- **Astrologer's Tower** (MAP-32): the answer is recorded (a visit shows next week's event; no re-roll); built after the week events.
- UI: the decision panel asks the cartographer, sphinx and witch hut questions; `ObjectServicePanel` serves the merchant, the refugee camp and
  the altar (not run in Godot yet). The generator's treasure sites include all six (values are drafts). The map editor places them; editing a
  sphinx's riddle or a hut's skills in the inspector is not built yet (the map file holds them).
- Tests: `ThirdCatalogueTests`.

### ADR-082: The second object catalogue (2026-10-27)
**Status**: implemented (2026-10-27). Builds the ADR-069 catalogue with the answers of ADR-070 … ADR-073, except the Shrines (they need
spells, Phase 5) and the faction re-draft of every bank's guard (still pending). Map format **v23**, generator **version 5**. Choices made
here are marked *(choice)*; every number is a draft.

- **Buffs** (`BuffRule`): Clover Field +1 luck, Rainbow +2 luck, Rally Flag +400 movement now with +1 luck and +1 morale, all for the next
  battle and once a day per hero. **Mana Vortex** sets the mana to twice the maximum, once a week per hero; the extra mana stays until it is
  spent (`Hero.Mana` may now reach 2 × `MaxMana`; daily regeneration never lowers it). **Fountain of Magic** gives half the maximum back, up
  to it, once a week per hero (`BuffRules.ManaAfter`).
- **Power-ups** (`TrainingRule`), once per hero: Dream Teacher 500 experience + 500 for every other Dream Teacher the hero learnt at; Training
  Grounds, Marletto Tower, Star Axis, Garden of Revelation +1 Attack / Defense / Spell Power / Knowledge, without a choice.
- **Mage's Chest** (`LootRule.MagesChest`): 5 % nothing, 5 % Major, 60 % Minor, 30 % Minor + 500–1000 gold *(choice: in hundreds, like the
  campfire)*; vanishes.
- **Deposits** (`Deposit`, `DepositRules`): Wood Pile, Ore Deposit, Gold Vein, Crystal Vein, Cinnabar Deposit, Sulfur Vent. A visitor's
  player is asked (`DepositDecision` / `AnswerDepositCommand`) to pay; the deposit becomes the mine of its kind with the same id, flagged
  for that player (`DepositBought`). Prices *(choice within the drafts)*: Gold Vein 5000 gold + 10 ore; Wood Pile 2000 + 5 ore; Ore Deposit
  2000 + 5 lumber; the magic veins 3000 + 5 ore. The generator places them as treasure with an ordinary guard.
- **Windows and Eyes of the Magi** (`MagiWindow`, `MagiEye`; red, green, blue, black, a definition per colour): every visit to a Window
  reveals radius 6 around each Eye of its colour. The editor refuses a fifth Eye of a colour (`EditResult.TooManyEyes`); the validator reports
  more than four and a Window without an Eye. Never generated.
- **Keymaster's Tents, border guards and border gates** (`KeymasterTent`, `BorderPost`, four colours): a tent gives the hero's **player** the
  key (`Player.Keys`, `KeyReceived`). A border post is a one-tile barrier: heroes of a player without the key cannot step on it (the move is
  refused with a message, routes go round it); key holders walk through. A guard leaves after the first key holder passes
  (`BorderGuardPassed`), a gate stays. The validator warns about posts with no tent of their colour. The template option to guard
  connections with them is not built yet (RMG-11).
- **Map tavern** (`MapTavern`, `MapTavernRules`, `HireAtMapTavernCommand`): four heroes of any faction, the same for every player, a new pool
  each week (a shuffle seeded from the game, the tavern and the week, so no stored state); a hired hero leaves the pool. The hero on the
  entrance hires for 2500 gold; the new hero appears below the entrance or on the next free neighbour, with a starter stack and full
  movement *(choice, as in a town)*; refused at the hero limit or without a free cell. The service panel lists the offers.
- **Garrisons** (`MapGarrison`; a horizontal and a vertical definition): flagged like a mine. Its owner's and allies' heroes pass through
  both ways (routes go through it); anyone else stops there and fights the troops (`BattleService.BeginGarrisonBattle`, the owner commands
  them, the AI for neutrals and computer players) or flags an empty one (`GarrisonOwnerChanged`). Beating the troops flags it for the winner.
  The owner's hero on the entrance moves whole stacks in or out (`MoveGarrisonStackCommand`), keeping one stack. A defeated player's
  garrisons turn neutral and keep their troops *(choice)*. The editor sets owner and troops (`SetMapGarrisonOwner`, `SetGarrisonSlot`). The
  generator does not use them as connection guards yet (RMG-11).
- **Banks**: the undead Ruins are the **Crypt** (`bank.crypt`; older maps' `bank.ruins` load as it). New: Ruins (orcs, `bank.orc-ruins`),
  Lair (Spectators, a fifth Beholders), Keep (Pikemen and Archers), Hideout (Bandits), Forest Glen (Timber Wolves, a fifth Dire Wolves; gold
  and lumber), Dragon City (3–6 Black Dragons, a Relic and 1–3 Major artifacts, `BankTier.MoreArtifacts`). Tables at 30 / 30 / 30 / 10 %,
  rewards about half the guard's gold value *(choice)*; the notes' creatures, not yet the "2–3 types of one faction" re-draft.
- **Portals**: the monoliths are called Portal / One-way portal (ids unchanged).
- **Generator**: the new loot, buffs, power-ups, deposits, banks and one map tavern are treasure sites (`TreasureSites`); a guarded site is
  only placed where its guard has room beside the entrance.
- **AI**: values the new buffs, power-ups and the Mage's Chest; buys deposits it can afford with its gold cushion; fetches keys; attacks
  garrisons it can beat; plunders the new banks like the old.
- UI: deposit question in the decision panel; map tavern and garrison in the object service panel; garrison owner and troops in the editor
  inspector; palette colours (none run in Godot yet).
- Tests: `SecondCatalogueTests`, `SecondCatalogueObjectTests`.

### ADR-084: Week and month events, the Astrologer's Tower and the kingdom overview (2026-10-28)
**Status**: implemented (2026-10-28). Carries out ADR-073's and ADR-075's week and month events (FLOW-2), ADR-077's kingdom overview and MAP-32
(ADR-080). Map format **v24**, template format **v6**, random map generator version **6**. Choices made here are marked *(choice)*; the chances
are ADR-073's drafts.

- **Rolls** (`WeekEventRules.Roll`, `GameState.Random`): a month's first week rolls 45 % month of a creature, 15 % plague, 15 % starvation, else
  the weekly table; every other week 75 % week of a creature, else nothing. The creature is any growing creature (base or upgrade, by id) of the
  chosen factions; none to pick means an ordinary week. Nothing is drawn while events are off or the week is skipped.
- **A week ahead** *(choice, for MAP-32's "no re-roll")*: `GameState.NextWeekEvent` is rolled when the week before starts (week 2's at game
  start), and becomes `GameState.WeekEvent` when its week starts. So the Astrologer's Tower tells what will happen, and visiting changes nothing.
- **Effects**, at the week's start after `WeekStarted` and before the growth (`WeekEventAnnounced`): the week's creature's **line** grows twice its
  growth (fort bonus included) in towns and map dwellings (`WeekEvent.Growth`); plague and starvation stop all growth; plague also halves the
  creatures waiting in every **town** (rounded down; map dwellings keep theirs, `TownCreaturesGrew`). A new dwelling still opens with one plain
  week *(choice)*. Refugee camps are not dwellings and are not affected.
- **Month of a creature stacks** (`GuardService.SpawnMonthly`, numbers *(choice)*): one stack per 10 map objects it could guard (at least 1, at most
  16). Those objects have an entrance that is not guarded yet. Towns, monsters, gates, garrisons, border posts and scenery are left out. Each
  stack stands on a random free tile beside the entrance, with no hero within one tile, and its count is rolled by tier like any unset guard
  (`GuardService.CountRange`). Classic guard mode, Aggressive. Events: `MapObjectPlaced` for each stack, then `MonstersSpawned`.
  `VisibilityService` refreshes memories on `MapObjectPlaced`.
- **Settings** (`ScenarioRules.WeekEvents`, `WeekEventSettings`): on or off, `SkipWeeks` 0–8 (default 1, so the first week has none) and the
  weekly and monthly factions (town ids incl. Conflux and Dragon Utopia, or `neutral`; default the playable towns). Map v24 `rules.weekEvents`
  (omitted when default; older maps get the default). Template v6 `weekEvents` (`TemplateWeekEvents`), passed to the scenario. The editor's
  conditions panel edits them (undoable, `SetScenarioRules` refuses a bad skip or an unknown faction). The map and template validators check
  them, and the map validator warns when a table has no creature to pick.
- **Game start**: with `SkipWeeks` 0 the first week can have an event. A plague then halves the map's town stocks and a month brings its stacks,
  but there is no growth to double.
- **AI**: it recruits the week's creature first; spawned stacks are ordinary guards to it.
- **Astrologer's Tower** (`astrologers-tower`, 2×2 like the Den of Thieves; `AstrologerConsulted(Hero, Tower, Next)` on every visit). It is in
  the map format v24 (`astrologersTower`) and the generator's treasure sites (value 500, one a map), hence generator version 6.
- **Kingdom overview** (`KingdomOverviews.For`): rows for the player's towns (`TownRules.DailyIncome`, with the paying buildings), mines,
  flagged providers (what a week brings in words, `ResourceProviderRules.DescribeWeek`, and what waits there) and heroes. Each hero source is its
  own row: Treasury with its interest, Prospecting, specialty, artifacts. The daily total equals `EconomyService.DailyIncome` with the default
  sources. Game app: `KingdomOverviewPanel` from the "Kingdom (K)" button or K (not run in Godot yet).
- **Front-ends**: `NoticePanel` shows the week's event on each human's first turn of the week and what an Astrologer's Tower says. `TurnPanel`
  shows the event next to the date. The editor's conditions panel has the settings. None of this has been run in Godot yet.
- **Compatibility**: games now draw week event rolls, so **saves and replays made before this change no longer match their digests** (as with
  ADR-079). `GameStateDigest` lists the week events only when one is set.
- Tests: `WeekEventTests`, `KingdomOverviewTests`.

### ADR-125: Scouting reveals information; amount words with their ranges (HERO-9, ADR-069, ADR-123, 2026-11-04)
**Status**: accepted (2026-11-04), built (ADR-142). The owner's answer to HERO-9: the proposals, and every amount word shows its range.

- **Baseline**: every army a player does not own (other players', neutral stacks, bank guards, garrisons), in sight or remembered, shows
  an **amount word with its range** instead of the count: a few (1–4), several (5–9), a pack (10–19), lots (20–49), a horde (50–99), a
  throng (100–249), a swarm (250–499), zounds (500–999), a legion (1000+) (`AdventureIntel.AmountWord`).
- **Scouting levels**, besides the sight radius (+2 / 4 / 6): **Basic**: exact counts of neutral stacks and bank guards in sight, an
  enemy hero's level and primary stats; **Advanced**: exact counts of enemy heroes' armies and town garrisons in sight, their artifacts and
  war machines; **Expert**: an enemy hero's skills and known spells, a town's available creatures. The best Scouting among the player's
  heroes that see the target counts.
- **Veil**: rank I hides counts from Scouting below Expert; rank II hides artifacts from everyone; rank III as built (ADR-123).
- **AI**: sees only what its heroes' Scouting shows, through the same `AdventureIntel` query; a word counts as the middle of its range
  (a legion as 1000).
- The information spells (Distant Vision, Ledger of Ages, Eavesdrop) keep showing exact counts.

### ADR-133: Map bans and random placeholders built (ADR-126, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`MapBanAndPlaceholderTests`); checked in Godot 4.7.2 with the new smoke scenarios
`game-placeholders` and `editor-bans`. Map format v35. Builds ADR-126's map bans and random placeholders; points marked *(proposal)* are the
agent's choices made while building and stand unless the owner changes them. Rules: [adventure-map.md](../../docs/design/adventure-map.md#map-bans-and-random-placeholders).

- **Bans** are part of the map's rules (`ScenarioRules.Bans`, `rules.bans` in the file: heroes, artifacts, spells, skills; sorted, empty
  lists left out). Read by the town and map taverns, prisons, hero placeholders, every artifact roll (one place: pickups left open and
  `Draw`), the guild and spell building draws, shrines, level-up offers and witch huts. A pool the bans would empty stays whole (ADR-126).
  *(proposal)* A guild's spells fixed by the map stay even when banned (placed by hand); a witch hut's own skills stay, but the stand-in
  skill of a group it offers when the hero cannot take its skill skips banned ones; the tavern pool is shuffled before the bans are taken
  out, so a ban does not reorder the rest.
- **Validator**: an unknown id in the bans is an error; a hero of a banned type, a banned artifact, spell or skill on a hero, a banned
  prisoner, artifact pickup, shrine spell, fixed guild spell, witch hut skill or Pandora's Box artifact is a warning ("placed by hand, so
  it stays").
- **Random monster** (`randomTier` on a monster: 0 any tier, 1–7): *(proposal)* the creatures it may become are the growing creatures of
  the playable towns and the neutral ones, base and upgraded; a count set by the author stays, otherwise the count is rolled for the drawn
  creature; the creature in the file only shows it in the editor (the editor shows the tier's first such creature).
- **Random town** (`random` on a town): an owned one takes its owner's chosen faction (`GameOptions.StartTowns`, set on the new-game
  screen's new "Town" column for a player with a random town or with a hero placeholder and no town; kept in saves and replays);
  *(proposal)* without a choice, one faction is drawn per player and all its random towns share it; a neutral one draws its own. The town is
  re-made as the editor's type change does (`TownRules.Retyped`: same id, place, owner, garrison, an author's name; buildings carry over,
  dwellings move by tier). Making a town random in the editor drops its fixed guild spells.
- **Hero placeholder** (`placeholder` on a hero): *(proposal)* its faction is its owner's first town once the random towns are drawn, else
  the chosen faction, else the player's drawn one; it becomes a free roster hero of that faction (not on the map, in a prison or retired;
  not banned), else any free hero. It keeps its place, army, artifacts and id (so conditions naming it still hold) and takes the roster
  hero's identity, stats, skills and spells; an empty army gets the hero's starter stack (as a tavern hire).
- **Game start**: the placeholders are drawn first (`PlaceholderRules.Resolve`): towns, monsters, heroes, from `GameState.Random`; views
  built before the start redraw from `MapObjectChanged` and `HeroRemoved` + `HeroPlaced`.
- **Editor**: a "Bans" tab after "Events" (a list per kind with a filter and "banned only"; ticking bans; undo says "Edit the bans");
  "Random faction" on a town ("Shown as" its type), "Random" (any tier or a tier) on a monster, "Placeholder" on a hero; captions mark
  random towns and monsters.
- **Not in this step**: the generator places none of them; the University and War Academy (ADR-139) read the bans; a campaign's
  carried hero in a placeholder waits for FLOW-1.

### ADR-140: The Grail built; the owner's puzzle map rules (ADR-126, ADR-130, MAP-36, 2026-11-06)
**Status**: implemented (part 1 2026-11-05, the puzzle map, the editor tool and the generator's obelisks 2026-11-06); core tested
(`GrailTests`); the Godot part (puzzle window, digging, holes, the editor's Grail tool, the template editor's obelisk count) built, not
run. Points marked *(proposal)* are the agent's choices made while building. Rules: [adventure-map.md](../../docs/design/adventure-map.md#special-sites).

**Built in part 1** (stays):
- **The Grail**: an artifact (`grail`, Misc slot, value 500 for the AI) never rolled, not sold, traded or sacrificed. It is buried at
  game start (`GrailRules.Bury`, `GameState.Random`) only on maps with at least one obelisk, on diggable ground more than 10 cells
  (Chebyshev, same level) from every town's entrance *(proposal)*, any diggable cell when none is that far; or where the author put it
  (map format v37, `grail`; the editor's `SetGrail` with undo, the validator's checks).
- **Digging** (`DigCommand`, key G): only with the whole day's movement, which it uses up; not on roads, objects or an old hole. A hole
  stays and is shown. *(proposal)* With a full backpack the Grail stays buried and no hole is left.
- **Grail building**: a hero carrying the Grail into an own town without one raises it at once (`GrailBuildingRaised`); it does not count
  as the day's building. Effects as ADR-130; per-town names *(proposal)*: Haven Dawnspire, Stronghold Skull of the First Khan, Swamp
  Bestiary Mother Mire, Academy Orrery of Ages, Inferno Everburning Pyre, Sylvan Heartwood Tree, Dungeon Abyssal Lantern, Necropolis
  Throne of Bones.
- **AI**: visits obelisks its player has not visited, digs where its puzzle shows the spot, carries the Grail to an own town without the
  building.
- **Game app**: the puzzle map window (P), notices for obelisks, holes and the Grail building. **Editor**: the Grail tool (click to bury,
  click again to take away); placing a 26th obelisk is refused.

**The puzzle map, owner's rules** (2026-11-06, replace part 1's 15×15 map of 3×3 pieces opened outer ring first):
- **Size**: the puzzle covers **20×20 map cells** in **pieces of 2×2 cells**: 10×10 = **100 pieces**.
- **Segments**: the **centre** is the middle 2×2 pieces (4×4 cells), the **mid ring** the rest of the middle 6×6 pieces (32), the **outer
  ring** the remaining 64 pieces.
- **The Grail** lies in the centre's 4×4 cells, not always in the same one of them; so the Grail cannot lie too close to the map's edge.
- **Obelisks**: at most **25** on a map, so an obelisk opens at least 4 pieces. The **first half** of the obelisks a player visits open
  random pieces of the outer ring only; the **next ones but the last** random pieces of the outer and mid rings; the **last** opens the
  centre.
- **What it shows**: terrain and the objects that cannot be removed, without ownership (no flag colours).
- Like HoMM2 and HoMM3: each obelisk visited opens a share of the picture.

**MAP-36, the owner's answers** (2026-11-06):
- **Shares** (a): the 100 pieces split over the map's obelisks as evenly as can be, the first visits getting the smaller shares, the last's
  share including the centre's 4 (`GrailRules.Share`): 25 obelisks open 4 each, 11 open ten 9s then 10, 2 open 50 and 50.
- **Halves** (b): the first obelisks / 2 visits (rounded down) open the outer ring, the next ones but the last the outer and mid rings, the
  last the rest with the centre. With 2 obelisks the first opens 50 of the 64 outer pieces. A ring with fewer pieces left than the share
  opens what is left.
- **Draws** (c): at each visit, from `GameState.Random`; the number of obelisks the player has visited counts, not which.
- **Players** (d): a puzzle per player; allies' visits do not count.
- **Edges** (e): the Grail lies at least **10 cells** from every edge, so maps under **24×24** have none. The editor refuses a nearer spot
  (`TooNearEdge`), the validator reports one as an error, and warns about obelisks on a map too small for a Grail. The puzzle's place is
  drawn at game start so the Grail falls in its centre 4×4 cells and the 20×20 cells lie on the map (`GrailState.PuzzleOrigin`).
- **Shown** (f): terrain and every object that stays (towns, mines, dwellings, gates, sites, scenery …); not piles, chests, artifacts,
  loot, Pandora's Boxes, monsters, prisons or quest guards (`GrailRules.ShowsOnPuzzle`). The window names the level.
- **Generator** (g): a zone's `obelisks` in the template (format v9): a count (0–25), or unset to share the map's 25 with the other unset
  zones, the first zones getting one more; so a map gets **25 by default**. Placed after the treasure, unguarded, three tiles apart
  (`TreasurePlacer.PlaceObelisks`, stream `obelisks`); none on a map under 24×24. A zone without room gets fewer (reported when the
  template set its count).

### ADR-141: A town sees from its middle (owner's bug report, 2026-11-06)
**Status**: implemented; core tested (`VisibilityTests`).
- **Bug**: at game start the fog opened around the starting hero, not the town: an owned town revealed radius 5 from its entrance, the
  bottom-middle cell where the starting hero stands, so the land above and beside the 7×7 town stayed dark.
- **Rule** (owner, 2026-11-06): an owned town reveals and watches from the centre cell of its footprint (`Town.SightCenter`), radius
  **10**: 7 cells past its walls on every side. Mines, providers and dwellings keep radius 2 from their entrance. In refog mode the
  town's sight is measured the same way.
- Games and replays now open more of the map at the start and when a town is taken; no format change.

### ADR-142: Scouting reveals information, built (ADR-125, 2026-11-06)
**Status**: implemented, core tested (`ScoutingIntelTests`); the game app's right-click not yet run in Godot.

- **Core**: `AdventureIntel.ScoutHero`, `ScoutMonster`, `ScoutStacks` (guards, map garrisons), `ScoutTown` and `ScoutingAt`; `ShownStack` now
  carries the range of its amount word (`Amount` reads "a pack (10–19)", `Estimate` the middle) and `ShownArmy.Details` (`ScoutedHero`)
  what Scouting tells of a hero. The information spells keep their own exact queries (`ShowHero`, `Town`, `ArmiesAround` …).
- **Where ADR-125 left a choice** *(proposal)*: a hero "sees" an army when the army's cell (any cell of a town) is within its sight circle,
  so a town's or mine's sight shows only words; another player's map garrison counts as a garrison (Advanced), a neutral one as a guard
  (Basic); an ally's armies are not the player's own, so they show as words too; a Scouting town view shows no buildings; the stats shown
  include what artifacts add; a word for a remembered army under the fog is read from the army as it is now.
- **Screens**: right-click on another player's hero or a neutral stack in sight (status line), on a town not the player's own (the intel
  panel, titled "Scouting"); every hidden count shows its word with its range, Eavesdrop and the other spells' windows included.
- **AI**: reads monsters, guards, bank and box guards, map garrisons, enemy town garrisons and enemy heroes' armies through the same query,
  each hidden count as the middle of its range (a legion as 1000), for its targets, threats and simulated fights. *Not done*: it still
  reads an enemy hero's Attack and Defense for the simulated fight whatever its Scouting.
- **Effect**: an AI without Scouting overrates an army at the bottom of its word's range (10 footmen read as 14) and fears an equal
  enemy; AI tests whose enemy hero only stands in give their own hero 12 footmen (`TestWorld.Steady`). Replays of games with AI players
  may differ.

### ADR-205: Routes are the cheapest; the AI weighs its targets with one cost map (ADR-013, ADR-040, ADR-120, 2026-11-17)
**Status**: implemented. No save, map or protocol format changes.

- **Why**: the route search's distance estimate counted a diagonal step as at least 71, but a diagonal on a dirt road costs 70 (50 × 1.41 =
  70.5, rounded to even). An estimate above the real cost lets A* settle for a dearer route: on maps without linked gates, routes came out
  up to a few points dearer than the cheapest, and the AI's sums were off by as much. And the AI searched a full route for every target
  it weighed (about 60–80 searches a turn).
- **Rule**: the estimate counts 50 a straight step and 70 a diagonal one, so `FindPath` always finds the cheapest route. With linked gates it
  is plain Dijkstra, as before.
- **The AI's route costs**: `PathFinder.CostsFrom` (one tile to every tile) and `CostsTo` (every tile to one tile) are Dijkstra searches over
  the same rules that run only as far as the questions so far need. Each answers exactly what `FindPath` gives as the route's cost: a
  hero or quest object bumped into and a monster stepped onto from the tile it guards count only for that tile. The AI keeps one map per
  hero until the game changes (`GameSession.ChangeStamp`: commands accepted, events published, hero moves) and prices its candidate
  targets, its exploring tiles, Town Portal and the walk before a Dimension Door with it. A Dimension Door's landings are priced with
  one `CostsTo` map instead of a search per landing. Only the chosen target gets a full route, when the hero moves there.
- **Last route**: `HeroMovementService.FindPath` gives its last route again while the change stamp stays the same, so a move just after a
  check (the AI's route spells and arena gathering, a click after the hover preview) does not search again.
- **Effect** (sim A/B, 18 generated games of 60 days: duel, ffa-4, 2v2, seeds 1–6): 15 end in the same state; in 3 the cheaper routes
  change choices (one duel is won by the same player on day 54 instead of 46). The baseline games keep their digests.
- **Checked** by `PathCostMapTests`: on two-level maps with monsters, gates, a garrison, a quest guard, a border guard and heroes, with and
  without fog, every tile's answer equals `FindPath`'s, in any order of asking; random road maps where the old estimate gave dearer routes.
