# Decisions: Computer players

**Covers**: The adventure AI and the battle AI: targets, cost maps, estimates, spells, trading, arenas.

**Current rules**: [docs/technical/architecture.md](../../docs/technical/architecture.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [059](#adr-059-the-adventure-ai) | The adventure AI | accepted |
| [119](#adr-119-the-adventure-ai-on-generated-maps-exploring-guards-garrison-heroes-keys-and-gates-adr-059-adr-071-adr-112-adr-114-2026-11-03) | The adventure AI on generated maps: exploring, guards, garrison heroes, keys and gates (ADR-059, ADR-071, ADR-112, ADR-114, 2026-11-03) | implemented |
| [160](#adr-160-the-adventure-ai-on-an-arena-maps-preparation-day-adr-097-adr-158-2026-11-09) | The adventure AI on an arena map's preparation day (ADR-097, ADR-158, 2026-11-09) | implemented |
| [184](#adr-184-the-adventure-ai-casts-adventure-spells-two-towns-rush-and-starting-armies-adr-059-adr-112-adr-119-adr-120-adr-123-2026-11-16) | The adventure AI casts adventure spells; Two Towns' rush and starting armies (ADR-059, ADR-112, ADR-119, ADR-120, ADR-123, 2026-11-16) | implemented |
| [187](#adr-187-a-fight-value-per-creature-for-the-battle-ai-adr-118-owner-notes-2026-11-16) | A Fight Value per creature for the battle AI (ADR-118, owner notes, 2026-11-16) | accepted |
| [188](#adr-188-cmb-13-answered-a-cleverer-battle-ai-in-steps-adr-187-2026-11-16) | CMB-13 answered: a cleverer battle AI, in steps (ADR-187, 2026-11-16) | implemented |
| [189](#adr-189-the-adventure-ai-trades-at-its-marketplace-adr-067-adr-059-roadmap-2026-11-16) | The adventure AI trades at its marketplace (ADR-067, ADR-059, roadmap, 2026-11-16) | accepted |
| [206](#adr-206-the-ais-battle-estimates-play-battles-of-their-own-and-are-kept-from-turn-to-turn-adr-059-adr-106-2026-11-17) | The AI's battle estimates play battles of their own and are kept from turn to turn (ADR-059, ADR-106, 2026-11-17) | implemented |
| [220](#adr-220-the-adventure-ai-is-a-coordinator-of-parts-with-tunable-weights-adr-059-adr-119-2026-11-19) | The adventure AI is a coordinator of parts, with tunable weights (ADR-059, ADR-119, 2026-11-19) | implemented |


### ADR-059: The adventure AI
**Status**: accepted (2026-10-16), first version; the numbers are a draft to tune with `sim game`; hiring heroes and
artifacts built by ADR-060/ADR-061, dwellings and caravans by ADR-054; defence, several heroes, war machines and governors added (2026-10-17, below)
- **Through commands only** (`AdventureAi` in `Core.Domain/AI`, ADR-036): it plays with `GameSession.Execute`, so its
  turns are in the command log and replay exactly. It draws **no random numbers**; the same state gives the same turn.
  It uses only what its player has explored (pathfinding and targets), not the whole map.
- **Who plays**: computer players (`!IsHuman`) in the game app (`Main._Process`, one turn a frame, not while a battle
  scene or the menu is open); with `controlHumanSlots` every slot, as in the simulator. `PlayTurn()` can be called again
  after it returned false (a human has to act in a battle) and carries on where the turn stands.
- **A turn**: answer pending decisions (chest: experience if it levels the hero, else gold; guard offers: accept a join,
  fight a stack that would flee; exchange: close; level-up: the first offer, which computer heroes get anyway), run the
  towns, move each hero, run the towns again, end the turn.
- **Towns**: one build a day from a fixed priority (the order is the strategy): one dwelling per tier from tier 1 up
  (the line with most weekly power), marketplace, blacksmith, mage guild, town hall, dwelling upgrades, fort, town hall,
  second lines, tavern. It **saves gold** for the building it wants when that is within about three days of income (and
  other resources within a week), skips buildings that need resources it will never get, upgrades stacks whose dwelling is
  upgraded, recruits the strongest creatures first (a first garrison of the cheapest line, half of what waits), and a
  visiting hero takes the garrison with it except the weakest stack.
- **Heroes**: every known object gets a value in gold (piles by resource, chests 1500, mines eight days of yield, towns
  4000+, monsters a share of their strength, much more when they would join, enemy heroes 2000 + 80 % of their army power,
  own towns with a lot waiting). The best of `value / (0.5 + days of travel)` among the ten best candidates wins; with
  nothing to do the hero explores the nearest reachable explored tile next to unexplored ground. A hero makes at most 24
  moves a turn.
- **Fights are played, not guessed** (`BattleEstimator`): before attacking a monster, a guard, a garrison or a hero the AI
  runs three battles with the real engine on a throwaway game (fixed seeds of their own, so the game's random numbers are
  untouched). It goes only when it wins all three and loses at most 40 % of its army power against monsters, 50 %
  otherwise. The power sum (Σ count × creature value) is only a pre-filter: it called a fight even that was lost every
  time, because it does not see shooters.
- **Exploring never walks into** heroes, objects or guarded ground, which are targets with a fight check.
- **`sim game`** plays a map with the AI in every slot (`--map towns|demo|file`, `--days`, `--seed`, `--runs`, `--record`)
  and prints the outcome and each player's towns, heroes and army; the self-test replays an AI game through JSON.
- **Not yet**: spells, difficulty levels, using the marketplace, sieges. (Defence, several heroes, war machines and governors: below.)

**Phase 3 follow-ups (2026-10-17)**:
- **Town defence**: a visible enemy hero threatens what lies within a day's march of it (max movement / 150 tiles, a rough average). An own town it
  threatens that its garrison alone could not hold (garrison below 80 % of the enemy's power) is a target worth 6000 for a hero that can hold it together
  with the garrison; a hero already in such a town stays there. Heroes **avoid** places a stronger enemy can reach: a target or an exploring step is
  taken only when the hero (with the garrison, at an own town) has at least 80 % of the strongest threat's power there, the enemy being attacked
  left out. Without this a computer player lost its towns to whoever walked in first; the sim game matches on the towns map last longer now.
- **Several heroes**: the strongest army is the **main hero** and moves first; the others explore and fight as before, and carry troops: one with at
  least 1000 army power (or a quarter of the main army) goes to the main hero, and the meeting's exchange hands over its stacks, strongest first (same
  creature, a free slot, or a swap for a weaker stack of a full army); it keeps one creature.
- **War machines**: after building and recruiting, the visiting hero buys a healing tent, an ammo cart when its army shoots, and its offensive
  machine back once lost when it can field it (Advanced Ballistics), each only while 1500 gold stay beyond what is saved for a building.
- **Governors**: heroes with Stewardship that govern nothing are appointed to the ungoverned towns that spend the most (most dwellings, then the
  highest town hall), the best steward first (ADR-063).

### ADR-119: The adventure AI on generated maps: exploring, guards, garrison heroes, keys and gates (ADR-059, ADR-071, ADR-112, ADR-114, 2026-11-03)
**Status**: implemented (2026-11-03). Numbers marked *(proposal)* are the agent's tuning, for play-testing.

- **What was wrong** (found with the new `sim game` diagnostics): on generated `duel` maps the heroes fought once in 60 days and stayed at
  level 1–5 with armies of 35 000–420 000 power, because (1) exploring was only the fallback when no target had any value, so a daily buff or
  troops waiting at home kept a hero shuttling forever, and none of the passage guards was ever even seen; (2) the exploring search priced only
  the 8 frontier tiles nearest in a straight line, which were often ground seen over a wall, so a hero said "nothing to explore" with its own
  zone half unexplored.
- **Exploring** (`AdventureAi.Explore`): a flood over open ground (no objects, heroes or guarded tiles) finds the frontier the hero can walk
  to, nearest first; a border post the player has the key for and an own or allied garrison are passes; a gate or portal whose exit is
  unexplored is a frontier tile itself (walking in takes the hero through), one whose exit is explored lets the flood go on there. Exploring
  is now a target like the others, worth **900** *(proposal)* against the targets' `value / (0.5 + days)`; a main hero that knows no enemy town
  adds **10 %** of its army power *(proposal)*, so a strong army goes looking for the enemy.
- **Guards**: a monster stack or a neutral garrison within 3 tiles of unexplored ground is also worth the exploring it opens (+900): passage
  guards stand between zones.
- **Keymasters**: a tent is worth 2500 once a border post of its colour is known (it bars the way), else 600 *(proposal)*; key holders explore
  and route through the posts (the route finder already let them, ADR-071).
- **Garrison heroes** (ADR-112): a **secondary** hero defending its town (an enemy can reach it, the garrison alone cannot hold, the two
  together at least match the danger) goes into the garrison, while the main hero is out on the map: inside it leads every stack behind the
  walls, at the entrance it would fight alone and the garrison after it. The player's only or main hero never goes in, as a garrison hero
  never retreats (a first version let Two Towns end on day 2 when the only hero went in and lost). A garrison hero comes out once no enemy can
  reach the town, and leaves its weakest stack behind.
- **Tools**: `AdventureAi.Explain(hero)` lists a hero's best targets with value, fight check, route cost and danger, then its exploring tile;
  `sim game` prints the fights won / lost per player and kind, what is left on the map, the keys and the garrison hero moves; `EW_SIM_EXPLAIN`
  and `EW_SIM_GUARDS` ([development.md](../../docs/technical/development.md)).
- **Results** (`sim game --runs`, the AI on both sides): generated `duel` 8 of 8 games to a winner by day 100 (days 27–63, 3 : 5), before 1 of 6
  by day 60; the connections smoke template (garrison, border gate, border guard) 3 of 4 by day 100 (keys taken, the garrison beaten, the
  border guard passed); Mirror duel 3 of 4 by day 100; `2v2` 2 of 3 and `ffa-4` 1 of 3 by day 120 (the larger maps take longer); Ember March 8 of 8 (days 6–41, 4 : 4; before days 8–71); Two Towns ends on days 4–5 (before
  13–37): its towns are 23 tiles apart and an exploring hero finds the other town on day 3 (a fair rush, not changed).
- **Fix**: the AI recruited into a garrison whose seven slots a garrison hero's stacks had filled (refused commands on `2v2`); it now skips
  a line with no room anywhere. `AdventureAi.Refusals` keeps the refused commands with their reasons, and `sim game` prints them.

### ADR-160: The adventure AI on an arena map's preparation day (ADR-097, ADR-158, 2026-11-09)
**Status**: implemented (2026-11-09): the roadmap's arena "AI" item. Only the preparation day changes (`ArenaRules.IsPreparationDay`);
every other game plays as before (the self-test digest is unchanged). Numbers are placeholders.

- **Never leaves the battle**: done in ADR-158 (no AI retreat in the arena battle; the AI never surrenders anywhere).
- **Towns** (`AdventureAi.Arena.cs`, `PrepareTown`): nothing is saved for tomorrow. A town first buys what waits, then builds step by step
  (any number a day): of every dwelling it lacks and every dwelling upgrade, with the buildings each needs first (the requirement chain,
  worked out from the building table), it takes the plan with the most **army gained per gold**, counting a new dwelling only when the town
  can still buy at least half of its first week (a new dwelling opens with one, ADR-066), and an upgrade by the creatures waiting and in its
  armies; it buys after each step. Income, growth, caravans, the marketplace and the like are worth nothing on the last day. Then upgrades,
  war machines, and **a hero hired with what is left** (more visits that day, and its starter stack), never once the armies are being gathered.
- **Heroes**: up to 64 moves each (movement does not stop them). Targets that pay only on later days are skipped (`Target.Later`: mines,
  deposits, empty providers, observatories, obelisks and the Grail, town gates, keymaster tents not known to bar the way, neutral
  garrisons, prisons, whose hero brings no army); flagging a map dwelling is worth nothing, buying there takes all the gold. Fights are
  taken only when the simulated loss stays under **15 %** (40 % otherwise). No **danger** is seen (the enemy's day-1 reach would cover the
  map; the map keeps the players apart, ARN-1 c), and the main hero does not hunt for enemy towns.
- **No contact with the enemy**: enemy heroes and enemy-owned towns are not targets on the preparation day *(choice)*: beating the enemy's
  only hero leaves nobody for the battle, a loss for both (ADR-158), and taking its town brings its heroes onto ours. Found on Ember March
  made an arena (its players can meet on day 1): without it nobody fought on day 2 in three runs of three; with it the battle was fought in
  all three.
- **Gathering** (`GatherForBattle`, before the turn ends): the strongest hero by `ArenaRules.ArmyValue` becomes the main hero, walks to every
  own town it can reach (buying and building there straight into its army, and taking the **whole** garrison: nobody attacks a town before
  the battle), then every other hero with something to give walks to it and hands over its troops (all but one creature) and war machines
  (`Exchange`). That hero is the one `ArenaRules.Champion` sends. On the arena smoke map the computer's champion went from its starting two
  stacks to five.
- Tests: `ArenaAiTests` (several buildings and the gold spent, mines skipped though taken on an ordinary map, one army at the end of the day,
  the battle fought to the end on day 2, a weaker enemy hero in reach left alone though attacked on an ordinary map).
- **Not done**: the AI casts no adventure spells on day 1 and does not trade at the marketplace (as elsewhere, ADR-067); it does not pick
  the combat place to stand on (the defender's side); it does not weigh Pandora's Box contents shown on day 1 (that showing is the editor
  item's).

### ADR-184: The adventure AI casts adventure spells; Two Towns' rush and starting armies (ADR-059, ADR-112, ADR-119, ADR-120, ADR-123, 2026-11-16)
**Status**: implemented. Numbers marked *(proposal)* are the agent's tuning, for play-testing. No rule, save or map format changes.

- **Spells the AI casts** (`AdventureAi.Spells.cs`), each through `CastAdventureSpellCommand`, never on an arena's preparation day:
  - **Town Portal**: to an own town an enemy can reach that the garrison alone cannot hold but the hero and the garrison together can, when
    the hero cannot walk there today; or to a town whose waiting troops call the hero home (`TownNeed`) when the walk there takes more than
    a day. From rank II it picks the town that needs the hero most, else it goes to the nearest.
  - **Dimension Door**: when the hero cannot reach its chosen target today, the landing (a straight line in each of the eight directions,
    out of reach of an enemy hero too strong for it) whose route to the target is cheapest, if it saves **300** movement points beyond the
    cast's 500 *(proposal)*. It is weighed at most 3 times per hero per turn *(proposal)*.
  - **Pathfinder's Call, Ashwalk**: when today's part of the route to the target leaves enough tiles of their terrains off the road to save
    **100** movement points beyond the 500 (rank III's +10 % counted) *(proposal)*.
  - **View Terrain**: when at least **30 %** of the ground in its radius is unexplored *(proposal)*.
  - **Grave Whisper**: a hero with Necromancy and no Grave Whisper on it.
  - **Veil**: the main hero, once an enemy hero is in sight, while it has no Veil.
  - Not cast: Distant Vision, Detect Magic, Ledger of Ages and Eavesdrop. The AI reads what its player knows from the state (ADR-125), so
    they would tell it nothing new.
  - **Mana**: the convenience spells (all but Town Portal and Dimension Door) are cast only when the mana left still pays for the hero's
    dearest battle spell, as a fight may come the same day.
- **The town at whose entrance a hero stands**: the AI weighed such a town by its garrison alone, but the hero there defends it first,
  behind the walls (ADR-106, ADR-112). Two Towns ended on days 4–5 because the first player's hero walked into the second's town against
  the "112-power garrison" and met its hero. The town target now fights that hero (its stats, the walls when it is its own town), worth the
  town plus the hero.
- **Where an enemy can go in a day**: the danger check counted 150 movement points a tile (about 11 tiles), so a hero left a town that an enemy
  25 road tiles away took the next day. It now floods each visible enemy hero's next day of movement (what its army allows) with its own step costs (roads,
  terrain, native ground). Open ground is passed; a hero, an object's entrance or a guarded tile can be reached but not passed. The flood is
  kept for the turn. `PathFinder.FindPath` takes a `from` tile, for the Dimension Door landings.
- **Two Towns' starting armies**: 20 Pikemen and 10 Archers against 22 Horned Demons and 8 Hell Hounds won 94 % of fights as the attacker,
  and the demons only 50 % (`sim battle`, 200 runs). Red now has **23 Horned Demons and 9 Hell Hounds** *(proposal)*: the attacker wins
  about three fights in four either way (73 % and 80 %).
- **Results** (`sim game --runs 12 --days 80`, the AI on both sides):
  - **Two Towns**: Blue won 12 of 12 (days 11–44; before, 6 of 6 on days 4–5). With the towns and the armies swapped (Inferno first) it was
    6 : 6. So the first seat and Haven each win about three games in four on this map. The first-mover edge is not an AI fault left to fix.
  - **Ember March**: Red 8, Blue 4, every game to a winner by day 73 (two after day 60). It was already Red 5 : 1 before these changes. A main
    hero now sees an enemy army that can reach its town and holds it when it would lose in the open (seed 2: Red's main army stayed home
    from day 30 to 62), so even armies take longer; `EmberMarchTests` allows 80 days (was 60).
  - **Generated `duel`**: 4 of 4 games to a winner (days 39–79), no refused command.
  - Haven also leads the one-week round robin (`sim towns`): 75 % overall, 62 % against Inferno.
  - Lowering the AI's guess of hidden stack counts (the lower third of a word's range instead of the middle) changed neither map.
- **Left open** (roadmap): two equal main armies take each other's empty towns in turn and do not meet (Ember March's seed 1 went past day 60
  that way until the entrance fix gave them a fight); the AI's hero hiring when it is beaten (a beaten player kept 3500 gold and no hero);
  the larger templates' game length; marketplace use (ADR-067).
- **Tools**: `EW_SIM_TRACE` also prints the spells cast and, after each turn, every hero's place, army power and mana.
- **Tests**: `AdventureAiTests.AHero_GoesHomeToATownAnEnemyThreatens_AndStaysThere` passed only because the old guess never had the enemy
  reach the town; its enemy is now 74 footmen (a horde, as the AI reckons it), so the hero and the garrison together can hold.
  `AdventureAiSpellTests`: Dimension Door over a wall, not for a short walk; View Terrain over unexplored ground; Pathfinder's Call
  on swamp, not on open ground; the mana kept for a battle spell; Grave Whisper; Town Portal to a threatened town three days away.

### ADR-187: A Fight Value per creature for the battle AI (ADR-118, owner notes, 2026-11-16)
**Status**: accepted (2026-11-16), built (2026-11-16): the `fight_value` column after `luck`; `CreatureDefinition.FightValue` replaces the
derived `Value` everywhere, the formula kept as `DerivedFightValue`. From the owner's notes in `ideas.md`; *(proposal)* marks the agent's
fill-ins. The rest of the note ("a cleverer battle AI") is ADR-188.

- **Fight Value**, as in HoMM3: a number per creature in `creatures.csv` (a `fight_value` column) that says how dangerous one creature is
  in battle. Placeholder values to start: today's derived `CreatureDefinition.Value` (health × average damage × (20 + Attack + Defense) / 80,
  ×1.5 shooters, ×1.2 flyers), written into the table so nothing changes on the day it lands *(proposal)*; tuned later with play-testing
  (it joins "Numbers to tune"). A row without a value falls back to the formula *(proposal)*.
- **One number** *(proposal)*: Fight Value replaces the derived `Value` wherever it is read (the battle AI's targets and spells, neutral
  dispositions, guard and army strength), so the table is the only place to tune it.
- **The battle AI** uses it for the most dangerous stacks on the field: a stack's threat is its count × Fight Value, raised while it can
  shoot unhindered or still has casts *(proposal)*, and the AI's targets, spells and retreat weigh the threat it removes rather than only the
  value of the hit points.
- Not a map or save format change; the content table gains a column (other content changes the replay digest, as any table change does).

### ADR-188: CMB-13 answered: a cleverer battle AI, in steps (ADR-187, 2026-11-16)
**Status**: implemented; steps (a), (d), (b) and (c) built (2026-11-16, `BattleAiLevel.FocusFire` … `HoldBack`), (e) and (f) built
(2026-11-19, `CastAhead`, `Sieges`). The owner's answer: a
stack's **Fight Value × its count** is the measure to use where it fits; the proposals for the rest.

- **The measure**: a stack's threat is its Fight Value (ADR-187) × the creatures in it, so a big stack of weak creatures can outrank a few
  strong ones. It is used where the AI asks "which stack matters most": the target to focus, the stack to block or keep away from, whom a
  buff or a curse goes to. Where the AI weighs a single blow (the hit points a hit removes, a retaliation taken) it keeps weighing what that
  blow does *(proposal, as before)*.
- **The order**: (a) focus fire on the stack with the highest threat, and finish off stacks it can kill this turn; (d) avoid retaliation:
  hit stacks that have already retaliated this round, and let attackers without retaliation go first; then (b) protect its shooters
  (block enemy shooters with a melee stack, keep its own out of reach) and (c) wait or defend when it is outranged or shoots better,
  instead of always closing in; later (e) spells weighed a whole round ahead (a buff before its strongest stack moves, a mass spell over
  several stacks) and (f) sieges (the gate or a breach together, shooters on the walls).
- **Checked** with `sim battle` win rates against the AI before each step; the AI stays deterministic (`GameState.Random` only).
- **Built, step (a)**: `BattleAiLevel` names the steps (`Greedy` is the AI before this ADR, `FocusFire` step (a); the game plays `Latest`), and
  `BattleService.AiLevels` sets one per side, outside the game state. A shot or blow scores what the greedy AI scored (the Fight Value of the
  hit points it takes off), plus that much again × the target's threat ÷ the biggest threat on the field; a blow expected to destroy the stack
  adds its whole threat. A stack's threat is Fight Value × count, ×1.25 while it can shoot unhindered or has casts left *(proposal)*. Walking
  still heads for the nearest enemy.
- **Measured** with `sim ai` (every playable town's one-week army against every town's, both sides, the new level on one side and the old
  on the other; 32 battles a side): `FocusFire` wins 50.5 % overall and 53.9 % of the mirror matches with base armies, 50 % and 51.2 % with
  upgraded ones, about even with `Greedy`: the greedy score already weighs each hit point by its Fight Value, so the threat weighting and the
  finishing bonus change few choices. Variants (no threat weighting, a larger finishing bonus, a bonus for stacks still to act this round,
  walking at the biggest threat within reach) all stayed within 48–52 %; a sanity run that picks the worst target wins 16 %. Larger gains are
  expected from (d) avoiding retaliation, which the AI does not weigh at all yet.
- **Built, step (d)** (`AvoidRetaliation`): a blow or shot scores what step (a) scored less the Fight Value of the health the target's
  retaliation is expected to take off the unit (the blow of the creatures left after the hit; all of them before a first strike; none when it
  cannot hit back: it has this round, is blinded, held or under a no-retaliation spell, the attacker has no-enemy-retaliation, or it is
  destroyed). A melee stack whose best target would hit back **waits**, once a round, when an own stack still to act this round can hit that
  target without being hit back (a shooter that can shoot it, or a no-enemy-retaliation creature that can reach it) *(proposal)*.
- **Built, step (b)** (`ProtectShooters`): a blow at an enemy shooter that can shoot and survives counts 25 % of its threat more (its shots
  stopped) *(proposal)*; a melee stack strikes from the placement next to the most threatening enemy shooters it can block; a walking melee
  stack heads for the biggest enemy shooter that can shoot instead of the nearest enemy, while there is one. Keeping own shooters out of reach
  is left to step (c).
- **Built, step (c)** (`HoldBack`): outside a siege, a stack that is not a shooter and has nothing to hit **waits** once a round while an enemy
  stack that can walk is still to act, so the enemy comes to it. After waiting it **holds its ground** (defends) instead of walking out while
  its side's shooters that can shoot are worth more (Fight Value × count) than the enemy's and the enemy has a walking melee stack; otherwise
  it walks as before. When the shooters run out of shots or are blocked, the hold ends. The game plays `HoldBack`.
- **Measured** the same way (32 battles a side; base armies / upgraded): `AvoidRetaliation` against `FocusFire` 58.1 % / 57.5 % (mirror
  matches 61.7 % / 64.3 %); `ProtectShooters` against `AvoidRetaliation` 49.2 % / 51 %, about even (kept: it does no harm, and (c) builds on
  it); `HoldBack` against `ProtectShooters` 75.7 % / 59.3 % (mirror 87.3 % / 72.9 %), largely because the older AI walks into the waiting
  stacks and the shooters' fire. Against a human the gain will be smaller.
- **Built, step (e)** (`CastAhead`): a hero casts once a round, on its side's first turn (every other own stack but a war machine still to
  act). Of the casts it can afford, worth something by the spell's own measure (ADR-120, ADR-123), the best aim of each of the **3** best
  spells *(proposal)* and casting nothing are each played out on a copy of the battle (`BattleState.Copy` with a generator of its own, mixed
  from the battle's state, so it neither foresees nor draws the battle's rolls) to the end of the next round, every stack played by the same
  AI, the side's hero casting nothing more there (each option is one cast against none) and the enemy's as usual. The option whose copy
  leaves the side best off is cast: the Fight Value of its stacks' health less the enemy's (copies left out, a beaten side's counted as
  none), plus the lasting spells still on the stacks at the usual share a round (4 %, 10 % for control, 20 % for a taken stack) for the
  rounds or turns left, for their caster. On a tie the best-looking cast wins, so nothing changes where the look-ahead sees no difference.
  Creature casters are as before.
- **Built, step (f)** (`Sieges`), all *(proposal)*: a besieger's walking stack with nothing to hit, outside the walls, stops 4 cells short of
  the way in (the nearest open breach or broken gate, else the standing gate) while less than half of its side's other walking stacks
  outside that can move (by Fight Value × count) could get within 3 of it on their next move, so the stacks go in, or break the gate,
  together. A siege engine the hero aims (Ballistics) fires at the gate first (one hit breaks it and opens the bridged way, with no moat),
  then the keep, the towers, then the wall sections, those beside the gate first; without Ballistics it fires by itself as before
  (ADR-106). A defender's walking stack with nothing to hit walks to the placement inside the walls (the gap itself allowed) nearest the
  open breach or broken gate closest to the attackers' walking stacks, so they come in through it under the defenders' fire.
- **Measured** with `sim ai` (32 battles a side; base armies / upgraded): `CastAhead` against `HoldBack` with both heroes knowing the same 5
  battle spells of levels 1–3 drawn per battle (Spell Power 3, Knowledge 4; `--spells`) 59.3 % / 54.3 % (mirror 66.4 % / 54.7 %); it plays
  each battle with casters about three times as long. `Sieges` against `CastAhead` in Castle sieges with a besieger 75 % larger and Basic
  Ballistics (`--siege`; at equal size the town wins every siege) 64.7 % / 60.4 % (mirror 69.5 % / 63.3 %). The gain is all the gate-first
  aim: without it 50.7 %, without the gathering 64.4 %, without the blocking 65.1 %; with no Ballistics (the engine firing by itself)
  `Sieges` wins 50.7 % / 51.5 %, 50.9 % without the gathering. The gathering and the blocking are kept as they do no measurable harm and
  answer what a person does better than this AI (picking off stacks that come in one by one, walking in through an unguarded breach).
- **Not built**: own shooters stepping away from a blocker; creature casters' spells weighed ahead.

### ADR-189: The adventure AI trades at its marketplace (ADR-067, ADR-059, roadmap, 2026-11-16)
**Status**: accepted (2026-11-16), built (2026-11-16): `AdventureAi.Market.cs`. Lifts ADR-067's "no AI trading for now" for town
marketplaces; *(proposal)* marks the agent's numbers, for play-testing.

- **Only for a building**: trading is dear (ADR-067), so the AI trades only as the last step to the building a town is saving for (the
  first reachable one in its build order), through `TradeResourceCommand` at its towns' rate. Never for troops, heroes or war machines.
- **Buying**: when the building lacks lumber, ore or a magic resource that the income would not bring within **2 days** *(proposal)*, the
  AI buys all that is missing, if its gold covers the building's gold and the trade, and the trade takes at most **50 %** of its gold
  *(proposal)*. A building whose missing resources can be bought that way (the gold counted with **3 days** of income, as for any building
  it saves for) now counts as within reach in the build order, and the gold it keeps back includes the trade.
- **Selling**: when gold alone is missing, the AI sells what it holds beyond the building's needs and a kept stock of **20** lumber and ore
  and **10** of each magic resource *(proposal)*, the resource that yields most first, just enough to cover the shortfall, and only if the
  spare stock covers all of it.
- **Not built**: map marketplaces (the AI neither targets nor trades at them), and trading on the arena's preparation day.
- Measured on three 60-day `duel` games (`sim game --template duel`): 8, 19 and 13 trades a game, mostly sales of 2–38 units to finish a
  building, a few purchases of one or two units of mercury.

### ADR-206: The AI's battle estimates play battles of their own and are kept from turn to turn (ADR-059, ADR-106, 2026-11-17)
**Status**: implemented. No save, map or protocol format changes.

- **Why**: each estimate built three complete games (map, services, heroes) to fight one battle in each, and the estimates were forgotten at
  every turn's start, keyed by a text built from both armies.
- **Rule**: `BattleEstimator` sets the battle up as `BattleStarter.BeginHeroBattle` sets up a hero battle — two bare heroes (Attack and
  Defense only) of computer players on grass; in a siege the attacker with its own war machines — and plays it out unheard, outside any game,
  as auto combat's preview does, through `BattleFlow.PlayComputer` with retreat off. The three fights use fixed seeds of their own, the
  same battles the throwaway games played, so every estimate is what it was.
- **Kept**: an estimate depends only on both armies slot by slot, the sides' Attack and Defense, and a siege's fort level and building count
  with the attacker's war machines, so it is kept under a struct key of those (hash worked out once) for the rest of the game, in two
  generations of `BattleEstimator.Capacity` (4096): when the newer is full it becomes the older and the oldest are dropped; an estimate found
  in the older moves to the newer.
- **Effect** (sim A/B, 18 generated games of 60 days: duel, ffa-4, 2v2, seeds 1–6): all end in the same state, in 243 s
  instead of 339 s. The big-map baseline asks for 2,959 estimates and plays 150 of them.
- **Checked** by `BattleEstimatorTests` (outcomes, the key, the bound) and the A/B digests.

### ADR-220: The adventure AI is a coordinator of parts, with tunable weights (ADR-059, ADR-119, 2026-11-19)
**Status**: implemented. No rule change; the AI plays as before.

- **Why**: `AdventureAi` had grown to about 2,500 lines in one class, and the numbers it weighs with were literals spread through it.
- **Parts** (internal, in `AI/`), sharing an `AiContext` (the session, the estimator, the weights, the commands issued, the turn's main hero
  and claimed targets, the army and resource measures):
  - `ThreatMap`: enemy heroes' reach and danger, cached while the command log does not grow.
  - `TargetValuer`: values and picks a hero's targets (map events and quests included), with the route cost cache.
  - `TownManager`: building, recruiting, garrisons, caravans, marketplace trades and the arena's town plans.
  - `DecisionResponder`: answers pending decisions (map event questions, hero meetings, level-ups).
  - `AdventureAi` keeps the turn and hero loops, `Explain`, spells and the arena's gathering into the champion.
- **Weights**: `AiWeights` (public record, 32 numbers; `Default` is the shipped AI). `AdventureAi(session, controlHumanSlots, weights)`.
  `AiWeights.With("Name=value")` sets one by name, strictly (unknown name, bad value, NaN refused); the sim takes
  `--weight Name=value`, repeatable. Per-object values and the safety caps stay constants.
- **Checked**: `AdventureAiPartsTests`; the sim digests are unchanged (no draw, loop or tie-break moved).
