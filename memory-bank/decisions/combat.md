# Decisions: Combat

**Covers**: The battlefield, turn order, damage and cover, morale and luck, capabilities, war machines, deployment, sieges, the 3D battle scene.

**Current rules**: [docs/design/combat.md](../../docs/design/combat.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [009](#adr-009-initiative-orders-turns-one-action-per-round) | Initiative orders turns, one action per round | accepted |
| [021](#adr-021-combat-ground-rules) | Combat ground rules | accepted |
| [033](#adr-033-combat-baseline-tactics-width-choice-homm3-damage-cover) | Combat baseline: Tactics width choice, HoMM3 damage, cover | accepted |
| [035](#adr-035-cumulative-ranged-cover) | Cumulative ranged cover | accepted; partly superseded |
| [038](#adr-038-tactical-battle-baseline) | Tactical battle baseline | accepted |
| [042](#adr-042-homm4-style-creature-capabilities-at-most-five) | HoMM4-style creature capabilities, at most five | accepted |
| [052](#adr-052-the-3d-battle-scene) | The 3D battle scene | accepted |
| [053](#adr-053-morale-and-luck) | Morale and luck | accepted |
| [056](#adr-056-war-machines) | War machines | accepted |
| [076](#adr-076-war-machine-footprints-and-the-deployment-step-implementation-2026-10-23) | War machine footprints and the deployment step (implementation, 2026-10-23) | implemented |
| [089](#adr-089-battle-movement-as-a-circle-cmb-9-2026-10-28) | Battle movement as a circle (CMB-9, 2026-10-28) | implemented |
| [090](#adr-090-auto-combat-and-speed-settings-cmb-10-ui-1-2026-10-29) | Auto combat and speed settings (CMB-10, UI-1, 2026-10-29) | auto |
| [109](#adr-109-sieges-built-adr-099-adr-106-2026-10-30) | Sieges built (ADR-099, ADR-106, 2026-10-30) | accepted |
| [144](#adr-144-the-towns-own-look-on-the-siege-field-adr-106-adr-109-2026-11-06) | The town's own look on the siege field (ADR-106, ADR-109, 2026-11-06) | implemented |
| [145](#adr-145-stack-health-bars-in-battle-adr-118-2026-11-06) | Stack health bars in battle (ADR-118, 2026-11-06) | implemented |
| [146](#adr-146-dividing-cliffs-on-the-battlefield-adr-118-2026-11-06) | Dividing cliffs on the battlefield (ADR-118, 2026-11-06) | implemented |
| [155](#adr-155-drag-and-drop-in-the-deployment-step-adr-072-adr-076-2026-11-09) | Drag and drop in the deployment step (ADR-072, ADR-076, 2026-11-09) | implemented |


### ADR-009: Initiative orders turns, one action per round
**Status**: accepted (2026-10-15)
Each combat round every stack acts exactly once, ordered by an Initiative stat (separate from Speed).
HoMM5's timeline UI without its "fast units act twice". This keeps the round structure that spells
and morale reason about, while Initiative stays a tuning lever separate from movement. Amended by ADR-053: good
morale may give a stack a second action, and morale may raise or lower its initiative for a round.

### ADR-021: Combat ground rules
**Status**: accepted (2026-10-15); widths refined by ADR-033 (28–34 by Tactics, 36 for sieges only)
- Battlefield 24 cells tall × 28–36 wide, set by the hero's Tactics skill (default 30).
- No facing and no generic flanking bonus. Engagement (which enemies are adjacent, on how many sides)
  and per-round hit counts are tracked so **creature abilities** can use them.
- HoMM3 Wait (end of round, reverse initiative order) and ties (attacker side, then speed, then a
  seeded roll).
- Breath has HoMM3 friendly fire; cleave/multi-head hits enemies only; only the primary target retaliates.

### ADR-033: Combat baseline: Tactics width choice, HoMM3 damage, cover
**Status**: accepted (2026-10-15); cover rule replaced by ADR-035
- Battlefield 24 tall. The side with effective Tactics (own level − enemy level) picks the width before
  battle: Basic {30, 32}, Advanced {28, 30, 32}, Expert {28, 30, 32, 34}; otherwise 30. 36 is reserved
  for sieges. This refines ADR-021.
- Damage uses the HoMM3 formula as a baseline (+5 %/point Attack over Defense, cap +300 %;
  −2.5 %/point Defense over Attack, cap −70 %). Rolls come from the seeded battle RNG.
- Cover: any unit (friend or foe) or obstacle on the line of fire reduces ranged damage. The flat
  −25 % is superseded by ADR-035.

### ADR-035: Cumulative ranged cover
**Status**: accepted (2026-10-15); supersedes the flat cover value in ADR-033
Every blocker on the line of fire between shooter and target adds a penalty, counted once per blocker:
2×2/3×3 creature 1/8, 4×4 creature 1/4, small obstacle 1/8, big obstacle 1/4, city wall 1/2.
Penalties add up and ranged damage is multiplied by (1 − total). Above 7/8 the shot is not allowed.
Penalties are exact eighths, so they are stored as integer eighths (`CoverEighths`) to keep battle
maths integer and deterministic. Units of either side count; the shooter and target never do.
Obstacle definitions carry a cover class (small/big/wall).

### ADR-038: Tactical battle baseline
**Status**: accepted (2026-10-15); defaults confirmed by the user, tunable; `NoMeleePenalty` replaced by opt-in
`MeleePenalty` (ADR-042); morale/luck added by ADR-053, war machines by ADR-056; surrender deferred, still open (ADR-060,
[open-questions.md](../open-questions.md))
- Field 24 tall, width 30; the side with effective Tactics picks the width first (ADR-033), the AI keeps 30.
  Units deploy automatically in slot order, in columns at their edge. 2–5 obstacles (small 2×2 = 1/8 cover, big
  3×3 = 1/4) are rolled between the deployment zones, never touching each other.
- Movement: 8-way, one cell per step, up to Speed. A footprint needs all its cells free (clearance by prefix
  sums); a diagonal step needs one of the two orthogonal placements free, so no squeezing through corners.
  Flyers land on any free placement within Speed.
- Turn order (ADR-009, ADR-021): Initiative, then the attacker's side, then Speed, then a seeded roll per round.
  Wait: once per round; waiting units act after everyone, in reverse order. Defend: +20 % Defense (at least
  +1) until the unit's next turn.
- Melee: move next to the target (8-way footprint adjacency) and strike; one retaliation per round unless
  `UnlimitedRetaliation`; `NoEnemyRetaliation` suppresses it; `DoubleStrike` strikes again afterwards.
- Ranged: needs shots and no adjacent enemy; half damage beyond 15 cells; shooters in melee deal half unless
  `NoMeleePenalty`. Line of fire = supercover of the segment between the footprint centres (cells touched only
  at a corner count too); cover per ADR-035; above 7/8 no shot. No skill reduces cover yet.
- Area: breath hits every unit, either side, touching the target-sized area behind the target; cleave hits up
  to two other enemies next to both attacker and target; multi-head hits every other adjacent enemy. One roll
  for all victims, each unit hit once, only the primary target retaliates.
- Damage: ADR-033 in integer maths, with the hero's Attack/Defense added. Only the top creature carries
  damage; stacks heal after the battle; dead stacks leave the army.
- End: a side without living units loses. The winning hero gains the HP of the enemy creatures killed, +500
  for defeating (not routing) a hero. Losing heroes leave the map. Safety net: a battle still undecided after
  100 rounds is won by the defender.
- Control: each human commands their own side, also during another player's turn (hot-seat). Neutrals and
  computer players use `BattleAi` (greedy, deterministic). Quick combat (`AutoResolveBattleCommand`) may be
  requested by either participant and hands both sides to the AI.
- Retreat (user decision): a hero may retreat; the army is lost, the hero leaves the map, and the winner gets
  no +500. Surrender comes with taverns in Phase 3.
- A hero without creatures cannot attack (`MoveResult.NoArmy`); an army-less defender is overrun without a
  battlefield. No ambush bonus, morale/luck, hero spells or war machines in Phase 2.

### ADR-042: HoMM4-style creature capabilities, at most five
**Status**: accepted (2026-10-15); refines ADR-037 and ADR-038
- Creature special rules are called **capabilities** (`CreatureCapability`, `CapabilityKind`; JSON
  `"capabilities"`). Every one counts, flying and area attacks included: **at most 5 per creature**, no
  duplicates, enforced when definitions are validated (user decisions).
- A capability is `(Kind, Value, Parameter)`: the value is a percentage (pack hunter, relentless) or casts per
  battle (caster); the parameter is a spell tag (immunity) or a spell id (caster).
- New combat capabilities (HoMM4 rules, confirmed by the user):
  - `FirstStrike`: the defender retaliates **before** the attacker's blow, using its retaliation for the
    round; if both have it, normal order; an attacker killed this way does not strike.
  - `RangedRetaliation`: a shooter that is shot at fires back if it could shoot the attacker right now (shots
    left, not engaged, cover ≤ 7/8); it spends a shot and its retaliation for the round.
  - `ShootsTwice`: a second shot after the (ranged) retaliation, one shot of ammunition each.
    `DoubleStrike` is the melee equivalent.
  - `MeleePenalty` is now **opt-in**: only shooters that have it deal 50 % in melee. It replaces
    `NoMeleePenalty`. Shooter-only capabilities on a creature without shots are invalid.
  - `NoEnemyRetaliation` now also prevents ranged retaliation.
- Spell capabilities are **data and hooks only** until magic exists (user decision): `SpellImmunity(tag)` with
  `CreatureDefinition.IsImmuneTo(tags)` (well-known tags in `SpellTags`: all, mind, fire, curse; the magic
  system will tag its spells), and `Caster(spell, casts)` with `CreatureDefinition.SpellCasts` and
  `BattleUnit.CastsLeft`. They change no battle outcome yet.
- The draft rosters now use them, e.g. guardian and lacerator first strike, marksman shoots twice, monk/priest
  and succubus mistress ranged retaliation, archer/monk/succubus melee penalty, mind/fire/curse immunities,
  priest/seraph/pit lord casters.
- Battle events report a shot back as `BattleAttackKind.RangedRetaliation`.

### ADR-052: The 3D battle scene
**Status**: accepted (2026-10-16); replaces the game app's quick-combat-only fallback (Phase 2)
- **Same scene tree, own camera.** `BattleView3D` (Godot.Shared) lives next to the adventure map, far from it
  (`BattleSpace.Origin`), and has its own `Camera3D`. While a battle is shown, the app hides the adventure map and its
  HUD and pauses the strategy camera; after the result screen it switches back. No scene change, so the session,
  views and subscriptions stay as they are.
- **Built from events only.** The view builds the battlefield from `BattleStarted` (units, obstacles, width) and
  follows `BattleUnitMoved` / `Attacked` / `Died` / `TurnStarted` / `Ended`. Every event is one step of a single
  animation queue, because the core resolves a whole AI turn (or a quick combat) inside one command. Geometry of
  queued steps uses where units *will* stand, not the live state, which is ahead of the animation. The view is busy
  while the queue plays; reachable cells, previews and input are offered only once it has caught up. Quick combat and
  battles without a human side skip the animation.
- **Pointer intent is a core query.** `BattleTargeting.ActionAt` turns a pointed cell (with the exact pointer
  position) into the `BattleAction` a click would perform: shoot an enemy if possible, else melee it from the
  reachable side nearest the pointer, else move to the reachable placement covering the cell. Engine-free and tested;
  the 3D game and the 2D harness both use it, and the rules still validate the command.
- **Placeholders for now.** The ground uses the adventure-map texture of the terrain the battle is fought on
  (`AdventureMapView.TerrainMaterial`); units are primitive meshes (capsule walkers, cylinder shooters, hovering
  sphere flyers) with a base in the side's colour, a facing nose and a count; obstacles are boulders and crags. Real
  models, animations and themed backdrops come with the art pass (Phase 5).
- **HUD** (`BattleHud`, Godot.UI): sides and round, initiative timeline (active, queue, waiting), the log in step with
  the animation, hovered or active unit stats, wait / defend / retreat / quick combat buttons (W, D, R, Q), and a
  result screen with casualties and experience. The width choice before the battle stays in `DecisionPanel`.

### ADR-053: Morale and luck
**Status**: accepted (2026-10-16), from [ideas.md](../ideas.md); answers the former open question 5 (now CMB-2 for the sources). Rates marked *draft* were filled in
where the notes left them open; artifacts add morale and luck (ADR-061)
- **Values**: every stack has morale and luck from **−5 to +5**: its creature's (`morale` and `luck` columns of
  `data/creatures.csv`, empty = 0) plus its hero's (`Hero.Morale` / `Hero.Luck`, map data in format v7 and the
  editor inspector), limited to ±5 (`BattleState.MoraleOf` / `LuckOf`). Guards and garrisons have only their
  creatures'. Skills, artifacts and army composition will add to the hero's value later.
- **Morale**, rolled for every stack at the **start of each round**:
  - good morale: **2 % per point** to **act twice**: right after its action the stack acts again (moving, attacking or
    shooting; defending does not trigger it, and a wait postpones it). This is the one exception to "exactly once per
    round" (ADR-009);
  - bad morale: 2 % per point to **lose its turn** that round (it "freezes"; its defend bonus ends);
  - if that did not trigger: **4 % per point** for **+50 % initiative** (good) or **−50 %** (bad, at least 1) for that
    round's acting order.
- **Luck**, rolled on **every hit** (retaliation, area hits and ranged retaliation included) by the attacker and by
  each target: *draft* **4 % per point**. Good luck of the attacker or bad luck of the defender means **+75 % damage**;
  good luck of the defender or bad luck of the attacker **−50 % damage**. When both sides' rolls trigger in opposite
  directions, nothing happens; two triggers in the same direction still count once. An area attack rolls the
  attacker's luck once and each target's for itself. *Draft*: the user's notes covered positive luck only; bad luck as
  the mirror image is our reading.
- Events: `BattleMoraleTriggered(unit, effect)` (haste and slow when the round starts, the extra action as it begins, the
  lost turn when it would have come) and `BattleUnitAttacked.Luck`. A stack with morale and luck 0 draws no random
  numbers, so battles without either replay exactly as before.
- Not decided yet: which skills, artifacts, buildings and army mixes change morale and luck, and whether creatures such
  as undead or constructs ignore morale (HoMM3 does).

### ADR-056: War machines
**Status**: accepted (2026-10-16), from [ideas.md](../ideas.md) and user decisions; numbers are a draft; Ballistics partly
superseded by ADR-063
- **Three per hero**: the town's **offensive machine**, a **healing tent** and an **ammo cart** (`Hero.WarMachines`,
  `WarMachineSlot`). Every hero starts with its offensive machine; the tent and cart are bought at a **blacksmith**
  (`BuyWarMachineCommand`, the visiting hero, HoMM3 prices: offensive 2500, tent 750, cart 1000 gold), which also
  replaces a lost offensive machine. A hero gets its own town's offensive machine wherever it buys it.
- **Offensive machine by town**, paired by theme (user decision): ballista (Haven, Necropolis), cannon (Academy,
  Inferno), catapult (Stronghold, Swamp Bestiary), glaivethrower (Sylvan, Dungeon, shoots twice). Custom heroes without
  a town use the ballista. Stats are code (`WarMachines`), not the creature table, so machines never appear as
  guards or recruits.
- **War machines skill group** (`machines`, 4 + 4 + 2 = 10 points, user decision):
  - **Ballistics** (4 levels, the group's main skill): the offensive machine fights only in **sieges** until **Advanced**
    Ballistics, then in **regular battles** too (user decision); Expert +25 %, Master +50 % its damage. Sieges do not
    exist yet, so Basic Ballistics has no effect for now.
  - **Healing** (4 levels): the tent restores 10 health (no skill) + 10 per level to the top creature of **every own
    stack** at the start of each round, never above its maximum and without reviving anyone (user decision: simpler).
  - **Ammo supply** (2 levels): the cart gives every own shooter **+25 %** shots at battle start without the skill,
    **+50 %** with Basic, and **unlimited** shots with Advanced while the cart stands (user decision: simpler; the
    +25 % without the skill is our draft).
- **Units on the field** (user decision): machines deploy in a column of their own at their side's back edge (tent at
  the top, offensive machine in the middle, cart at the bottom), the army in front of them. They never move, never
  fight in melee, never retaliate and have no morale; enemies can attack and destroy them. Only the offensive machine
  takes turns (by initiative, commanded like any unit; it has unlimited shots and no long-range penalty); the tent
  and the cart work passively. A side whose creatures are all gone has lost, machines or not. **Destroyed machines are
  lost** (`WarMachineLost`); the hero buys new ones.
- Map format **v9**: heroes may list `warMachines` (omitted = the offensive machine only); the editor inspector edits
  them. The 3D battle shows machines as wooden boxes with a barrel, roof or wheels; the 2D harness town panel has the
  blacksmith.
- Open: siege use (walls, Basic Ballistics), stats and prices after play-testing, exchanging machines between heroes,
  machines in garrisons.

### ADR-076: War machine footprints and the deployment step (implementation, 2026-10-23)
**Status**: implemented (2026-10-23). Carries out ADR-073's war machine footprints and ADR-072's deployment (HERO-4); choices those ADRs left
open are recorded here.

- **Footprints**: `WarMachines.Footprint` gives 1 × 3 for every offensive machine and the tent, 1 × 2 for the cart; `BattleUnit.Width` ×
  `Height` (creatures stay square, `Size` is their side) and `RectAt` are what the grid, cover, breath areas and the views use.
  `BattleUnitInfo` carries `Width` and `Height` instead of `Size`. Machines stand in column 0 (the defender's last column), tent at row 1,
  offensive machine in the middle, cart at the bottom; stacks start in the next column (no gap). Without machines stacks start at the edge.
  In the 3D scene a machine's model is as long as its footprint and is pushed outwards, so it overhangs the edge.
- **Deployment step**: `BattlePhase.Deploying`, after the width choice and before the first round.
  - The deployer is the side ahead in Tactics (the same comparison as the width choice); a tie lets nobody. A computer side, and quick
    combat, never deploy (the AI keeps the automatic placement).
  - The zone depth follows the deployer's **own** Tactics level (Basic 4, Advanced 5, Expert 6 columns), not the effective level
    *(choice made here)*. It includes the machine column; obstacles are rolled outside the zone.
  - Before the step the army is placed as always; the player moves one stack at a time (`BattleDeployCommand`) to any free placement
    entirely inside the zone, then presses "Start battle" (`BattleStartCommand`, Enter). Machines stay where they are. Stacks the automatic
    placement left outside the zone may stay there *(choice made here)*.
  - Not in creature banks (they also have no width choice). Quick combat during the step ends it and fights the battle.
  - Front-ends: click a stack to pick it, then click where its footprint should be centred (`BattleTargeting.DeploymentPlacement`); a green
    or red preview in the 3D scene, the zone shaded. Drag and drop can come with the battle UI polish.
  - Events: `BattleDeploymentStarted`, `BattleUnitDeployed`, `BattleDeploymentEnded`. The step uses the Tactics skill already; the separate
    Deployment skill merges into Tactics with the skill rework (step 4).

### ADR-089: Battle movement as a circle (CMB-9, 2026-10-28)
**Status**: implemented (2026-10-28). Answers CMB-9 (ADR-088); the owner's answers, with the reach shape picked from drawn examples.

- **Reach** (`BattleGrid.Reachable`): a stack reaches a placement whose path is at most **Speed + 0.25 cells** long (`ReachSlack`). The path may
  run **at any angle**: on an open field the reach is the circle of that radius (Speed 5: 88 cells, Speed 10: 332). Around obstacles it is the
  way round, found by Theta* (a path goes straight on from an earlier placement when the line from there is clear; nearly the shortest). The
  owner rejected both octagon variants (a diagonal step at 1.41 with or without a free last step) and a circle without the 0.25: the 0.25
  removes the single cells that stuck out on the axes and rounds the edge.
- **Lengths** are integers in thousandths of a cell (`BattleGrid.Unit`, `Length`: the exact square root rounded down), so every machine
  computes the same reach. `Reachable` now maps a placement to its path length in those units, not to a number of steps.
- **Flyers and teleporters**: any free placement within the same straight-line distance (CMB-9 c).
- **The distance penalty** for shooting measures the gap between the footprints the same way (`CellRect.DistanceTo`; 1/8 per whole 10 cells,
  CMB-9 d). Diagonal shots get a penalty sooner than before: 8 cells across and 8 down is 11.3 cells, 1/8.
- Still as before: a footprint must fit at every placement it passes, and a diagonal step needs one of the two orthogonal placements free.
- **Speeds +10 %** (CMB-9 e): every creature's battle speed in `creatures.csv` × 1.1, rounded half up (6 → 7, 10 → 11, 15 → 17, 28 → 31). Map
  speeds are unchanged. The speed bonuses (War Tempo, the preferred creature, artifacts) stay whole cells.
- A Speed of 1 can no longer step diagonally (1.41 > 1.25); no creature is that slow.
- **Compatibility**: battles play differently, so **saves and replays made before this change no longer match their digests** (as with
  ADR-084).
- Tests: `BattleGridTests` (the circle for walkers and flyers, the way round an obstacle, lengths, the straight-line distance penalty).

### ADR-090: Auto combat and speed settings (CMB-10, UI-1, 2026-10-29)
**Status**: auto combat implemented (2026-10-29); the speed settings are built too (ADR-095). Answers CMB-10 and UI-1 (ADR-088): the owner
accepted both proposals.

**Auto combat** (CMB-10):
- **The setting** (a): per human player, **off** (default), **ask before each battle** or **always**, in the game menu (F10) and changeable at
  any time. It is player state (`Player.AutoCombat`) set by `SetAutoCombatCommand`, which any human player may send outside its turn too, so it
  is saved with the game and kept by replays. It changes no rule; the digest shows it only when it is on, so older digests hold.
- **Which battles** (b): a battle between one human player and a computer player or neutrals, attacking or defending
  (`BattleService.AutoCombatPlayer`). Two humans (hot-seat) fight by hand.
- **The preview leaves no trace** (c): `BattleService.Preview` plays a copy of the battle (`BattleState.Copy`: units, queues and its random
  generator as they are) to the end as quick combat would, with no events heard and the game untouched. **Accept** sends the ordinary
  `AutoResolveBattleCommand`, which ends the battle exactly as previewed (the same numbers are drawn); **Fight** plays on by hand from the state
  before the preview, so rejecting re-rolls nothing.
- **Choices inside the battle** (d): the AI takes them as in quick combat: the default width, no deployment. The computer side's hero may
  retreat as usual; the human's side never retreats or surrenders.
- **The outcome screen** (e): like the result screen: who wins and how, both sides' casualties, the experience, the undead Necromancy would
  raise (a copy of the game's random generator gives the same draw) and the defeated hero's artifacts. Accepting returns to the map without a
  second result screen.
- **Quick combat** (f) stays during the battle; its result is final.
- **When it asks**: the battle scene opens as usual, and the question (or the outcome) comes once the scene has played the moves made so far, so
  the computer's opening moves have happened; a battle still choosing its width is asked about over the map. A battle loaded from a save is not
  asked about.
- Front-end: `AutoCombatPanel` (game app, above the battle scene); the setting row in `GameMenuPanel`. Tests: `AutoCombatTests` (the preview
  changes nothing and quick combat matches it, also mid-battle and with a width choice and deployment; Necromancy and artifacts foreseen; which
  battles; the setting at any time and in replays; refused for a computer player). Smoke: `game-auto-combat`.

**Speed settings** (UI-1), to build:
- Four steps, **slow / normal / fast / instant**; normal is today's battle animation speed.
- Three settings: the player's own heroes on the map, the computer's heroes (with "don't show"), battle animations.
- A settings screen in the game menu, kept per computer in a settings file in the user data folder, not in saves.
- Animated hero walking is built together with the setting (today a hero jumps from tile to tile).

### ADR-109: Sieges built (ADR-099, ADR-106, 2026-10-30)
**Status**: accepted (2026-10-30), built. ADR-106's siege rules in core, the battle AI and the adventure AI's estimate, with the 2D board and the
3D battle scene drawing them (the 3D part is not run in Godot yet). Points marked *(assumed)* are the agent's choices where ADR-106 says
nothing; numbers stay placeholders.

- **When** (as ADR-106): a hero attacking a town's garrison, or an enemy hero standing at the entrance of its own town, fights a siege when
  the town has a Fort (`SiegeRules.SpecOf`); the field is 36 wide, with no width choice; deployment works as before. Map garrisons are not
  besieged. New towns start with a Fort, so **every town battle is a siege now**: saves and replays of older games that fought one replay
  differently.
- **Layout** *(assumed)*: the wall line is column 24 of 36 (24 columns for the attackers, 11 behind the walls, room for two columns of 4 × 4
  defenders). Four sections of 5 rows and the gate of 4 rows in the middle (a 4 × 4 creature passes). The moat is the 2 columns in front of
  the walls, bridged in the gate's rows. The **keep and towers stand off the grid**: the keep behind the defenders' edge, the towers above and
  below the wall line; they block nothing, and only the siege engine reaches them. Random obstacles stay in front of the moat.
- **Walls and gate**: standing sections block both sides, the gate only the attackers; flyers and teleporters land beyond them. An attacker's
  shot whose line crosses a standing section or the gate takes the wall's 1/2 once; the defenders shoot out freely *(assumed)*.
- **The moat**: only the attackers stop in it and take its damage *(assumed)*. A stack stops as soon as its footprint touches the moat
  (walking; a flyer landing in it is hurt too), and moves on freely from there; leaving and coming back is a new entry.
- **Keep and towers**: they shoot **when each round starts** *(assumed)*, a flat 10 + 2 per building (the keep double), at the attacking
  creature stack worth most to hit. **Buildings** count every built level of every building and dwelling *(assumed)*.
- **The siege engine**: the attacker's offensive machine always joins a siege, whatever its Ballistics. Without Ballistics it fires by
  itself, even for a person (walls, the gate after all four, the keep, the towers); from Basic the player aims it
  (`BattleShootStructureCommand`, a click on a structure); troops only from Advanced, which also adds the second shot after a kill. Events:
  `SiegeStructureShot`, `BattleUnitSiegeDamaged`; `BattleStarted` carries the `SiegeLayout`.
- **AI**: the estimate fights (`BattleEstimator`) are sieges for fortified towns and heroes in their own towns, with the hero's own machines.
  In battle the defenders hold the walls (they fight what they can reach but do not advance out); walking attackers take the way round the walls to a
  breach or the broken gate, and while the walls hold close up to the moat without entering it; the AI's engine always fires at the walls
  first.
- **Not built**: one defending shooting stack in the keep (ADR-106 g); Earthquake (with magic); a Godot run and a smoke scenario for the
  siege scene; the town's own look on the siege field (built, ADR-144).

### ADR-144: The town's own look on the siege field (ADR-106, ADR-109, 2026-11-06)
**Status**: implemented; seen in Godot (Haven, Academy, Necropolis, Inferno sieges from the siege smoke map). Shapes and colours are the
agent's placeholders until the town models (Phase 5 presentation).

- **Core**: `SiegeSpec` carries the town type (`SiegeRules.SpecOf` sets it; estimates may leave it null), and `BattleStarted`'s
  `SiegeLayout.Town` passes it to the views. It changes no rule.
- **Look** (`SiegeLook` in Godot.Shared, one row per town type, a grey default for an unknown one): the colour of the stone (walls, keep,
  towers), the gate, the roofs and their shape (Haven, Sylvan, Swamp cones; Academy, Conflux domes; Dungeon, Dragon Utopia pyramids;
  Necropolis, Inferno spikes; Stronghold battlements on a log palisade), the moat (Inferno lava and Conflux's shimmer glow) and the yard
  behind the walls, tinted over the battle terrain.
- **3D field**: walls carry battlements, the gate a lintel of the town's stone, keep and towers the town's roof; a fallen structure loses its
  roof with the rubble. Beyond the defenders' edge stand 7, 9 or 9 of the town's houses (Fort, Citadel, Castle) under the same roofs. The 2D
  board draws walls, gate and moat in the town's colours.

### ADR-145: Stack health bars in battle (ADR-118, 2026-11-06)
**Status**: implemented; seen in Godot (the siege smoke map: full, yellow and red bars).

- **Core**: `BattleUnitAttacked`, `BattleUnitSiegeDamaged` and `BattleUnitSpellDamaged` carry the stack's `TopHealth` after the hit, as
  `BattleUnitHealed` and `BattleUnitRevived` already did, so the view shows the bar as each hit plays, not the state ahead of it.
  `StackHealth` gives the bar's fraction and band (green when the top creature has over half its health, yellow over a fifth, red at a fifth
  and below). It changes no rule.
- **3D field**: a bar under the stack count, facing the camera and drawn over the field; it updates on hits, heals, revives and rebirth. The
  2D board draws the same bar along each stack's bottom edge. Sizes and colours are placeholders.

### ADR-146: Dividing cliffs on the battlefield (ADR-118, 2026-11-06)
**Status**: implemented; seen in Godot (the battle smoke map, with highlands and the chance forced for the check). Shape and height are
placeholders.

- **Rule**: on highlands, rough, rocky and subterranean ground one battle in three gets a cliff; never in a siege or a creature bank (Pandora's
  Boxes and map-event battles may have one). It stands in the middle of the open ground between the armies (the deployer's zone stays free).
  Half the time it is one piece of 6–12 cells with at least 4 open rows above and below; otherwise two pieces of 6–10 cells, up to two columns
  apart, with at least 4 rows between them and at least 4 more above or below. Each piece is 2 columns wide and runs down the field. Gaps of 4
  let a large creature through. Random obstacles are fewer (1–3 instead of 2–5) and keep 4 columns off the cliff, so they never close a gap.
- **Movement and shots**: walkers go round (the existing pathing); flyers and teleporters pass over. A shot across a cliff takes 1/4 cover once
  per piece, like a crag.
- **AI**: a walker on a field with a cliff advances along the steps round it to the nearest enemy (as attackers do in a siege) instead of
  by straight-line distance, which would park it against the cliff's face.
- **Code**: `ObstacleClass.Cliff`; `BattleObstacle` takes a `Length` (its rect is 2 × length); `BattleState.CliffsAllowed` (false for banks);
  `BattleSetup.PlaceCliff`. The 3D field draws a ridge (a prism) as long as the piece; the 2D board draws its rect like any obstacle.

### ADR-155: Drag and drop in the deployment step (ADR-072, ADR-076, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Drag**: in the deployment step the player presses on an own stack, drags it and lets go where it should stand. While it is dragged the
  stack's model follows the pointer, lifted a little, and the placement preview under it is green where it may stand and red where not.
  Letting go places it (the same `BattleDeployCommand` and placement, `BattleTargeting.DeploymentPlacement`, as a click); dropped where it
  may not stand, it goes back and the refusal shows on the status line; dropped off the field or on its own cells it goes back silently.
  Escape while dragging puts it back. The release counts even over the HUD.
- **Click to place stays**: a press without a drag *(proposal: under 6 px of pointer travel)* picks the stack, and the next click on a cell
  puts it there. A press on the picked stack itself now starts a drag instead of placing it on its own cells.
- **No swap** *(proposal)*: dropping a stack on another own stack is refused like any occupied cell; swapping places would be a rule change in
  `BattleSetup.DeploymentRefusal`, not done.
- **Code**: Godot `BattleView3D.Carry` (the picked model under a field point, or back); the game app maps press, motion and release
  (`DeployAt`, `DragDeploy`, `DropDeploy`). `AutoInput`'s scripted `drag` now reports the held button on its motion. No core change, no
  save format change; smoke scenario `game-deploy-drag` (a stack dragged down the zone, another dropped outside it and refused).
- **Not done**: the 2D harness keeps click to place; touch drag.
