# Adventure map

> Part of the [game design](overview.md). Everything here is built unless tagged *(planned)*; numbers are drafts. Open items ❓:
> [open-questions.md](../project/open-questions.md).

## Size and levels
- Square presets **48, 72, 96, 128, 160, 192, 256** (max), each with an optional underground level; **up to 8 players** (ADR-020).
- Square grid, surface + underground, connected by subterranean gates.

## Movement
HoMM3 rules. No sea travel: water and rock are impassable on foot (ADR-051).
- A step costs the terrain of the tile being **left**; 8-way, diagonals ×1.41.
- **Terrain costs** (ADR-045, `TerrainCatalog`): **100 for an all-native army**, else the terrain's cost. No terrain is free for everyone.

  | Terrain | Non-native cost | Native towns |
  |---|---|---|
  | Grass | 125 | Haven, Sylvan |
  | Highlands | 125 | Haven, Conflux |
  | Forest floor | 125 | Sylvan |
  | Cursed lands | 125 | Necropolis |
  | Subterranean | 125 | Dungeon |
  | Sand (desert), beach | 125 | – |
  | Dirt | 150 | – |
  | Rough, wasteland | 150 | Stronghold |
  | Lava | 150 | Inferno |
  | Rocky ground | 150 | Dragon Utopia |
  | Ice | 175 | Academy |
  | Shallow water | 175 | Swamp Bestiary |
  | Snow | 200 | Academy |
  | Swamp | 200 | Swamp Bestiary |
  | Water (deep), rock (cliffs) | impassable | – |

  - **Native = the whole army**: the penalty is waived only if every creature belongs to one of the terrain's native towns; neutrals are
    non-native (`MovementRules.ArmyTown`).
  - **Pathfinding** skill cuts the non-native penalty by 25 / 50 / 75 / 100 % (swamp 200 → 175 / 150 / 125 / 100); artifacts reduce it or
    make terrains native (ADR-061). Applied in `MovementRules.StepCost`, shared by routes and steps.
- **Roads** (ADR-024, ADR-032): a layer over terrain, painted in the editor or laid by the [generator](random-maps.md#pipeline). **Dirt 50,
  gravel 65, cobblestone 75**; replaces the terrain cost and ignores native penalties, only for a step **from a road tile to a road tile**.
- **Last step**: a hero with any movement points left may make one more step, whatever it costs, and ends with 0.
- **Daily movement points** = base from the **slowest creature's map speed** (`MapSpeed`, HoMM3 scale, separate from battlefield Speed,
  ADR-037): ≤3: 1500, 4: 1560, 5: 1630, 6: 1700, 7: 1760, 8: 1830, 9: 1900, 10: 1960, ≥11: 2000; × (100 % + skill bonus, e.g. Logistics) +
  flat artifact bonus. Recomputed at the owner's turn start; 1500 without an army.

## Pathfinding
Cheapest route; visible heroes and object bodies block; routes may only *end* on an object's entrance (gates continue on the other side);
unexplored tiles are never used. Every step carries a day index for day markers. Heroes the player can't see don't affect the plan; movement
stops when it bumps into one.

## Map objects
Objects have a footprint and are entered only through their entrance tile, usually from below.

- **Town**: 7×7 with the corners free, entrance bottom-middle (`.BBBBB.`, five rows `BBBBBBB`, `.BBVBB.`; ADR-046, ADR-050). Entering an
  enemy or neutral town fights its garrison, if any, and captures it; an own hero on the entrance is the town's visiting hero.
- **Marketplace**: opens the trading screen for the hero's owner at a fixed rate ([rates](towns-and-economy.md#marketplace)); not flaggable,
  any number of visits.
- **Mines may be guarded** (editor or generator): the guard must be beaten before the first flagging; after that any hero re-flags by
  visiting. Mine guards always fight.

<a id="planned-objects-adr-068"></a><a id="second-object-catalogue-adr-069"></a><a id="third-object-catalogue-adr-077"></a>
## Shared object rules
Objects from the owner's lists ADR-068, ADR-069 (built ADR-082, ADR-120) and ADR-077 (built ADR-079, ADR-080, ADR-084); questions `MAP-n`.
The generator places them as treasure by value ([random-maps.md](random-maps.md#pipeline)).

- **Artifact tiers** (MAP-1): **Minor**, **Major**, **Relic** as in `artifacts.csv`. Assembled relics never drop; their pieces do (ADR-121).
  Relics are drawn from a pool without repeats, refilled when empty, so a hero may hold two copies.
- **Visit scopes** (data of the object definition, state in the map format): *weekly* (a per-object counter reset on Monday: one reward a
  week unless someone took it; an emptied object says so), *once per hero* (visited-by set), *first visitor* (spent after one visit),
  *vanishing* (removed after the visit).
- **Temporary effects** are hero statuses (setting one is a published event) that belong to the visiting hero only. "Until the end of the
  day" ends at the owner's next turn start, "the rest of the week" when the next week starts, "the next battle" when that battle ends,
  whatever the outcome or enemy (hero, neutrals, garrison, bank, mine guard). A second status of the same kind from the same object
  replaces the first; different objects add up; battles cap morale and luck at ±5 (ADR-053, MAP-10). Each hero takes a buff once a day
  (Stables once a week), so walking off and on gives nothing.
- **Random rewards** draw from `GameState.Random` at the visit; a map author may fix the outcome.
- "Random resource" never means gold ([resource groups](towns-and-economy.md#resources)).
- **Neutral creatures for objects**: Dwarf and Battle Dwarf are neutral tier-2 creatures (`neutral.dwarf`, `neutral.battle-dwarf`).

## Neutral monsters
ADR-040. A creature stack on a tile; stepping onto it attacks it, and it guards the 8 tiles around it. **Guard mode** per stack (ADR-065,
editor inspector or generator):
- **Classic** (HoMM3, default; passage guards): entering a guarded tile starts the encounter, but pickups on guarded tiles can be grabbed
  from outside ("stealing").
- **Area** (treasure guards): touching anything inside the 3×3, pickups included, starts the fight.
- A pickup next to several stacks can be stolen only if all are Classic; otherwise the first Area stack defends it. Anything else in a
  guarded tile is defended by the first guard (lowest id). Mine guards have no mode: they defend only their mine.
- **Dispositions** (HoMM3 names, deterministic): Compliant, Friendly, Aggressive, Hostile, Savage decide whether the stack offers to
  **join** (free, needs room) or **flees** from a much stronger army (≥ 3×), else fights. The player accepts or fights. Unset counts are
  rolled by tier at game start.

## Meeting heroes
Moving onto another hero's tile is an interaction, so routes may **end** there (never pass through):
- **Own**: the **exchange screen** (the mover stays on its adjacent tile): stacks (`MoveStackCommand`: split, merge, swap; a hero keeps one
  stack), artifacts (UI *planned*) and [war machines](combat.md#war-machines).
- **Enemy**: a battle; the loser (or a hero that retreats) leaves the map.
- **Allied** ([teams](victory-and-modes.md#game-options)): no interaction, the tile is blocked (ADR-027).
- Under refog a moving hero **stops** when an enemy hero comes into sight; bumping into an unseen one starts a battle (**ambush**, no
  bonus; ADR-031, ADR-038).

## Gates and teleporters
Every link is fixed in the map; no random-exit portals (ADR-026). "Portal" and "monolith" are the same thing (MAP-18).

| Kind | Behaviour |
|---|---|
| **Two-way gate / Portal** | A linked pair, **exactly two ends** (no groups); visiting either end moves the hero to the other. Subterranean gates connect levels |
| **One-way gate / One-Way Portal** | An entrance linked to one exit; several entrances may share an exit. The exit is a landing spot only: visiting does nothing, routes may walk over it |

Routes go through both (one-way only forwards); a hero on the exit blocks the teleport. The editor links them; the validator flags unlinked
gates and unused exits. Templates may link zones with portals.

<a id="fog-of-war-implemented-adr-017-adr-031"></a>
## Fog of war
ADR-017, ADR-031. Two modes, chosen at game creation (`GameOptions.FogMode`):
1. **Permanent reveal** (HoMM3): once explored, terrain, objects and units stay visible.
2. **Shroud + refog for units**: terrain and objects stay revealed (out of sight, in their **last known state**, `ObjectMemory`); other
   players' heroes show only on tiles **in sight**: within an observer's radius, dropping out only beyond radius + **refog margin**
   (default 2, configurable).

Observers: heroes and owned towns radius 5, owned mines, providers and map dwellings radius 2. Scouting adds 2 / 4 / 6 to a hero's radius,
revealed at once when learned.

## Mines and resource providers
| Object | Effect |
|---|---|
| Gold Mine, Sawmill, Ore Mine, Alchemist's Lab, Crystal Pond, Sulfur Dune | Flagged; daily **+1000 gold, +2 lumber, +2 ore, +1 mercury, +1 crystal, +1 sulfur**; optional guard of any kind |
| Abandoned mines | Separate objects (MAP-2), guarded by Troglodytes, **150–295** at the gold mine, **100–245** elsewhere, rolled at game start (a map's guard is kept). Beaten, it becomes the normal mine flagged for the winner. Gold and ore fight on **subterranean** ground (Troglodytes native, [combat.md](combat.md#damage)), the others on their own terrain |
| Deposits | A visiting hero pays and the deposit becomes its mine, flagged for the player (asked first; any hero; may have a normal guard; MAP-21). Gold Vein → Gold Mine **5000 gold + 10 ore**; Wood Pile → Sawmill **2000 + 5 ore**; Ore Deposit → Ore Mine **2000 + 5 lumber**; Crystal Vein → Crystal Pond, Cinnabar Deposit → Alchemist's Lab, Sulfur Vent → Sulfur Dune **3000 + 5 ore** |
| Water Wheel | A week: **500 gold** in the game's first week, **1000** after |
| Warehouse of Wood / Ore | A week: **8** lumber / ore **or 500 gold**, at random each week (ADR-079) |
| Warehouse of Sulfur / Mercury / Crystal | A week: **5** of it **or 500 gold**, at random |
| Warehouse of Gold | A week: **2000 gold** |
| Windmill | A week: **3–6** of one random non-gold resource |
| Mystical Garden | A week: **500 gold or 5 of one random magic resource**, even odds (MAP-3) |

**Providers** (ADR-079, TOWN-9) are flagged like mines. While flagged, a week's output is added to the provider's store every week start (and
at game start when owned from the start); it piles up. The first flagging of a neutral provider brings one week at once; capturing an enemy's
takes its store. An own hero's visit takes the whole store, or a caravan fetches it ([towns-and-economy.md](towns-and-economy.md#map-dwellings-and-caravans)).
A defeated player's providers turn neutral and keep their store.

## Treasure and one-time loot
| Object | Effect |
|---|---|
| Resource Pile | An amount the map leaves open is rolled at game start: **5–10** lumber / ore, **3–7** magic, **500–1000** gold in hundreds; vanishes |
| Treasure Chest | Player chooses gold or XP: 30 % 1000 / 500 XP; 30 % 1500 / 1000; 25 % 2000 / 1500; 10 % 2500 / 2000; **5 % a Minor artifact** (taken at once); vanishes. Map authors and the generator may fix gold and XP; a fixed chest never holds an artifact |
| Artifact | Picked up into a slot or the backpack (ADR-061) |
| Campfire | 400 / 500 / 600 gold and **4–6** of one random non-gold resource; vanishes |
| Supply Crates | Two different random non-gold resources, **4–6** of each; vanishes |
| Traveler's Wagon | First visitor: 10 % nothing, 30 % Minor artifact, 60 % 2–5 of one random non-gold resource; vanishes |
| Mage's Chest | 5 % nothing, 5 % Major, 60 % Minor, 30 % Minor and 500–1000 gold; vanishes (MAP-15) |
| Corpse | First visitor: 80 % nothing, 20 % Minor; stays |
| Warrior's Tomb | First visitor: 80 % Minor, 15 % Major, 5 % Relic, and **−3 morale for the next battle**; later visitors only the −3 |
| Lean To | First visitor: 1–6 of one random non-gold resource; stays |
| Prison | A hero joins the visitor's player; vanishes. Level **30 % 1, 30 % 5, 20 % 10, 15 % 20, 5 % 30**, or set by the author; above level 1 the game picks its level-ups. On a generated map its **faction is random** and the hero is never a chosen starting hero or one already on the map. At the hero limit the visit fails with a message and the prison stays |

## Creature dwellings
A map dwelling is one creature line with its own growth (ADR-054; recruiting and caravans: [towns-and-economy.md](towns-and-economy.md#map-dwellings-and-caravans)).
- A neutral dwelling of **tier 3 / 4 / 5** is guarded by **9 / 6 / 3** base creatures of its line, set at game start; tiers 1–2 and owned
  dwellings are unguarded. The guard always fights; beating it flags the dwelling (ADR-068).
- **Base creatures only** (ADR-073): the editor refuses an upgrade, the validator reports one, and loading turns it into the line's base.

## Creature banks
- Entering fights the guard, which **always fights**; a hero without an army cannot enter. Default width, no Tactics choice or deployment.
  Each creature kind is split into a few stacks (at most seven).
- Reward only on victory; the bank is then **plundered** and **refills 4 weeks later**, guard re-rolled, whole reward back (MAP-7, MAP-14).
  Retreat or defeat leaves the guard **intact**.
- Guard row rolled at game start (30 / 30 / 30 / 10 %), **hidden**, not fixable by the author. Guards are **2–3 types of one faction**
  (neutrals count as one), rewards about 25–60 % of the guard's gold worth (ADR-112, MAP-22; `CreatureBank.cs`). "N each" = N of every named
  resource; "a fifth" = half the time a fifth of the first type is upgraded.

| Bank | Guard rows (30 / 30 / 30 / 10 %) and reward |
|---|---|
| Churchyard | Skeletons + Ghouls: 40 / 30, 3000 gold, Minor · 60 / 40, 4500, Minor · 80 / 50, 6000, Minor · 100 / 70, 8000, Major. A later visit to the plundered bank: **−1 morale for the next battle** (only bank with a penalty) |
| Mansion | Vampires + Vampire Lords: 25 / 15, 8000 gold, 2 each mercury / sulfur / crystal, Major · 35 / 25, 12000, 3 each, Major · 45 / 35, 16000, 4 each, Major · 60 / 50, 22000, 5 each, Major |
| Crypt (`bank.crypt`; old maps' `bank.ruins`) | Skeletons + Wights + Liches: 30 / 5 / 1, 2000 gold · 40 / 8 / 2, 3500 · 50 / 10 / 3, 5000 · 60 / 15 / 4, 7000. Also **removes the fog within 12 tiles**, on every visit |
| Dwarven Treasury | Dwarves + Thanes: 40 / 5, 3000 gold, 2 crystal · 60 / 8, 4500, 4 · 80 / 12, 6000, 6 · 110 / 18, 8500, 10. A fifth Battle Dwarves |
| Imp Cache | Imps + Horned Demons: 80 / 20, 1500 gold, 2 mercury · 120 / 30, 2000, 4 · 160 / 40, 3000, 6 · 240 / 60, 4000, 8. A fifth Familiars |
| Medusa Stores | Medusas + Minotaurs: 12 / 5, 2000 gold, 4 sulfur · 20 / 8, 3000, 6 · 28 / 12, 4000, 8 · 38 / 16, 5000, 10. A fifth Putrid Medusas |
| Naga Bank | Nagas + Gargoyles: 4 / 15, 3500 gold · 8 / 20, 6500 · 12 / 30, 10000 · 16 / 40, 13000. A fifth Naga Queens |
| Ruins (`bank.orc-ruins`) | Orcs + Goblins + Ogres: 15 / 20 / 0, 20 / 30 / 2, 30 / 40 / 3, 40 / 50 / 5; 2000–6000 gold |
| Lair | Spectators + Troglodytes: 3 / 20 … 10 / 40; 2000–6000 gold and a Minor (top row Major). A fifth Beholders |
| Keep | Pikemen + Archers + Griffins: 20 / 10 / 0 … 60 / 30 / 5; 1500–5000 gold and a Minor (top row Major) |
| Hideout | Bandits + Trolls: 12 / 2 … 40 / 6; 1500–5000 gold |
| Forest Glen | Wolves + Mammoths: 25 / 1 … 80 / 3; 1500–4500 gold and 5–20 lumber. A fifth Dire Wolves |
| Dragon City | Black / Red / Green Dragons: 2 / 0 / 2, 3 / 2 / 0, 4 / 2 / 2, 5 / 3 / 3; 10000–30000 gold, a Relic and 1 / 1 / 2 / 3 Major artifacts. By far the strongest |

## Permanent hero power-ups
Once per hero. A paid power-up offers only prices the player can pay and may be left unpaid; a visit that trains nothing does not count.

| Object | Effect |
|---|---|
| Learning Stone | **+1000 experience** |
| Gazebo | Pay **1500 gold** for **2500 experience** |
| Library of Enlightenment | **+2** to all four primary skills, hero **level 10+** (lower: a message) |
| Tree of Knowledge | Once per hero per tree: experience to the next level for **2500 gold or 5 crystal + 5 mercury + 5 sulfur**, player's choice (MAP-9) |
| Arena | **+2 Attack or +2 Defense**, player's choice |
| Colosseum of the Magi | **+2 Spell Power or +2 Knowledge**, player's choice |
| Dream Teacher | 500 experience, +500 for every other Dream Teacher the hero has visited on this map |
| Training Grounds / Marletto Tower / Star Axis / Garden of Revelation | +1 Attack / Defense / Spell Power / Knowledge |

## Temporary buffs
Rules: [shared object rules](#shared-object-rules).

| Object | Effect |
|---|---|
| Faerie Ring, Clover Field | +1 luck, next battle |
| Rainbow | +2 luck, next battle |
| Fountain of Youth | +400 movement today, +1 morale next battle |
| Mineral Spring | +600 movement today, +1 luck next battle |
| Rally Flag | +400 movement today, +1 luck and +1 morale next battle |
| Temple | +2 morale next battle, **+3 on day 7** of the week (calendar day, MAP-10) |
| Stables | +400 movement at once and a day for the rest of the week (once a week per hero); upgrades Lancers to Champions free on every visit |

## Utilities and travel
| Object | Effect |
|---|---|
| Sign | Shows the author's message |
| Observatory | Reveals the terrain within **20 tiles**, every visit |
| Hill Fort | Upgrades tiers **1–3**, stack by stack, for **150 % of the price difference**; tiers 4–5 only in towns (MAP-11) |
| Town Gate | Teleports to one of **its owner's towns** (not teammates'), no movement cost, picked from a list; a town with an occupied entrance is listed but unavailable; routes do not plan through it (MAP-12) |
| War Machine Factory | Sells the hero **its own offensive machine** (ADR-056), the ammo cart and the healing tent at blacksmith prices; not one it already has (MAP-13) |
| Window / Eye of the Magi | Four colours (red, green, blue, black); a Window reveals **radius 6** around every Eye of its colour, at most **4 Eyes** a colour, every visit (ADR-073). Editor only, never generated |
| Tavern | Like a **Brotherhood** (4 offers): the hero stands on its entrance, the new hero appears on a free cell next to it (one below by default); refused at the hero limit or with no free cell. Its **own pool**: 4 random heroes of any faction, rolled weekly, shared by all players (ADR-071, TOWN-4) |
| Garrison | Flagged like a mine, captured by beating its guards (from the map or generator). The owner's heroes pass both ways and may leave or take creatures; enemies must beat the guards (MAP-19). Generator connection guard |
| Keymaster's Tent | Four colours; a visit gives the **player** that key. A **border guard** of the colour vanishes after the first key holder passes; a **border gate** stays and lets every key holder through (MAP-20). Generator connection guard |

## Magic
| Object | Effect |
|---|---|
| Magic Well | Restores 100 % of the hero's mana, once a day |
| Fountain of Magic | Restores 50 % of the maximum, once a week per hero |
| Mana Vortex | Mana to **twice the maximum** until spent, once a week per hero (MAP-23) |
| Shrine (levels 1–3) | Teaches its spell of that level (any school; set in the editor or rolled at game start) to a hero with a spell book and the school's Magic at the spell's level; any number of visits; otherwise a message (MAP-24, ADR-120). Generator values 500 / 1500 / 2500 |

## Special sites
| Object | Rules |
|---|---|
| Sphinx | One riddle per hero (author's or `riddles.csv`, three answers). Right: a **Major artifact or 5000 experience** (even odds). Wrong: movement ends for the day and **−3 morale** next battle (MAP-27) |
| Sanctuary | A hero on its entrance **cannot be attacked**, one hero at a time (MAP-28) |
| Sacrificial Altar | Necropolis, Inferno, Dungeon heroes sacrifice creatures for **hit points × 2** experience (keeping one); Haven, Sylvan, Academy heroes artifacts for **2000 / 5000 / 10 000** by tier; Stronghold and Swamp Bestiary either; any number of times; a whole assembled relic is refused (MAP-29, ADR-121) |
| Refugee Camp | Each week a random **tier 3–5 base creature** of the playable towns, one week's growth, normal price; not flaggable; unsold creatures leave (MAP-30) |
| Den of Thieves | The **whole** [Thieves' Guild](towns-and-economy.md#thieves-guild) table on every visit |
| Cartographer | Surface or underground: **3000 gold** reveals that level, once per player (MAP-31) |
| Astrologer's Tower | Shows next week's event (MAP-32) |
| Artifact Merchant | **7** Minor or Major artifacts (pieces, never whole relics) at **twice their worth** (Minor 2000, Major 5000), restocked on each month's first day; a sold slot stays empty until then (MAP-33) |
| Witch Hut | **Two skills** (map-set or rolled); a visit raises one a level, or the same group's next skill where it cannot; **one lesson per hero level** (MAP-34) |
| University / War Academy *(planned, ADR-126)* | Lessons in the magic schools / the general groups: up to 2 new groups and 2 skills raised a level per hero, each **1000 gold + 5 crystal, mercury and sulfur**; learned skills shown disabled |

<a id="scenery-and-rivers-adr-077-built-adr-079"></a>
## Scenery and rivers
- **Scenery** (MAP-25, `scenery.csv`): static objects with no entrance (rocks, trees, dead trees, ponds, bushes, mushrooms, flowers,
  fissures, mounds, ruins, bones, castle walls; each in two or more sizes). **Every cell blocks.** Castle walls are 1×N straight pieces
  (horizontal, vertical, both diagonals). Each row lists the terrains it suits; the generator fills obstacle clumps with fitting scenery.
- **Rivers** (MAP-26): a tile overlay, four looks (clear, icy, muddy, lava). **Leaving a river tile costs 25 % more** (Pathfinding does
  not cut it); a road-to-road step is a bridge and ignores it. Caravans ignore rivers.

<a id="pandoras-box-and-map-events-adr-096-planned"></a>
## Pandora's Box and map events
- **Pandora's Box** (ADR-096, ADR-100, ADR-105): the author fills it (resources, experience, skills, artifacts, creatures, spells, …); a
  visiting hero is asked whether to open it; it opens once and vanishes. Gains only; an optional guard is fought (like a bank's) after "open?"
  and the box opens only on victory; optional message; gains the hero has no room for are lost. Contents hidden until opened (the AI knows
  them); an empty box is a validator error. The generator places boxes of gold, experience or creatures from a value of 5000.
- **Map events** (ADR-096, ADR-115, ADR-124, ADR-127, ADR-128, ADR-129): HotA-like extended events, a tree of conditions and actions over map-wide whole-number
  variables, stored in the map file (`events`; the node types are in `MapEvents/MapEventData.cs`).
  - **Triggers**: hero events run when a hero steps onto one of their invisible cells (every step, until the event disables or removes
    itself; routes ignore cells and stop only for a message, a question or a battle); player events at every player's turn start after
    income, growth and the week's event; town events at the owner's turn start, neutral towns' at the start of the day. Several at one
    moment run in list order; who triggers is a condition.
  - **Conditions**: and / or / not, comparisons of expressions (numbers, variables, random numbers, + − × ÷, the player's resources, the
    hero's army, stats, skills, artifacts, level, mana and movement, the date, the complexity, hero and town counts), the hero, its class,
    the player, human or computer, its faction, team and allies, a town's owner, and who beat a monster or hero an event names.
  - **Actions**: branches, stop, variables, enable or disable an event, remove the cell; messages (with reward lines, given once read) and
    questions with two or three answers; resources, creatures (a person's full army chooses what to keep, a computer keeps its strongest),
    creatures to hire, buildings (free, with what they need), experience, primary and secondary skills (gains only), artifacts, war machines,
    spell book and spells, mana, movement, morale and luck until the next battle, a battle (won: the event goes on; lost, fled or
    surrendered: it stops), reveal an area, give a roster hero. A computer player sees no messages.
  - **Quests**: an event of its own kind with stages done one after another; a stage has a requirement (no random numbers), a proposal,
    an optional progress text and completion question, and the actions run on completion (what it takes, then the rewards). Ready forms
    write HoMM3's quests (level, stats, artifacts, creatures, resources, beat a monster or hero, be a hero, class or player). Each quest
    object names its own quest; the stage reached is shared by all players.
    - **Seer's Hut**: shows the proposal (later the progress) or, when met, asks to complete; then shows the next stage; empty after the last.
    - **Quest Guard**: a one-tile barrier every hero bumps into; it leaves after its last stage.
    - **Quest Gate**: a one-tile barrier with one stage that takes nothing; heroes meeting it walk through, the others bump and see it.
    - **Quest log** per player: the quests its heroes were shown, their stage and whether it completed them; another player's completion
      drops the open entries.
  - **Loops and shared events**: a repeat (its count drawn once) and a while loop, each at most 100 rounds; "run another event" runs that
    event's actions for the same hero, player and town (a disabled one is skipped, a stop in it ends only it, at most 16 deep).
  - **A Pandora's Box** may name a hero event, run for the opener after the contents.
  - **Game app**: messages, questions and the full army's choice in the decision prompt; the quest log (Q) with "Show" to look at a quest's
    object.
  - **The AI** answers a question with the answer worth the most (random numbers as their average, a battle by the estimator, a lost one
    worst), does a quest stage only when what it gives is worth more than what it takes (a Quest Guard also for the way it opens), treats Quest
    Guards and Gates as obstacles until its hero meets them, and does not look ahead at events (cells, player and town events, a box's event).
  - **Editor**: the "Events" tab (the events in run order, a chosen event's cells, towns or quest objects, its tree, a node edited as the
    map file's text, the map variables), simple forms that write HoMM3's timed events, event cells and town events as event trees, event
    cells drawn on the map, "New" and "Edit" links in the inspector, and validator errors and warnings (an event that runs itself or that
    nothing runs, a quest no object asks or that cannot be met, a variable nothing reads, …). The editor opens a map whose events do not fit;
    the game does not.
  - **Campaign variables**: a map variable may be "kept for the next map" (its value at the end is what a campaign carries on) and / or
    "taken from the last map" (it starts with the carried value, else its own); ticked in the Events tab. The campaign that carries them is
    Phase 6 (FLOW-1).
  - **Ranking**: an event that gives something only to people (a condition only people meet, such as a ready form with "computer may
    trigger" left off) makes the map campaign-like, so it is not ranked; the validator warns ([victory and modes](victory-and-modes.md)).
  - *(planned)* The camera moving to what a turn-start message concerns.
