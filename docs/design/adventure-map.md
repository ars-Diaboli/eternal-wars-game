# Adventure map

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../project/open-questions.md).

## Size and levels
- **Map sizes**: square presets 48, 72, 96, 128, 160, 192 and 256 (max), each with an optional
  underground level. **Up to 8 players** (ADR-020).
- Square grid, up to 2 levels (surface + underground) connected by **subterranean gates** (implemented).

## Movement (HoMM3 rules, implemented)
- A step costs the terrain of the tile being **left**. Water and rock are impassable on foot; there is no sea
  travel (ADR-051).
- **Terrain costs** (ADR-045): leaving a tile costs **100 for an all-native army** and the terrain's cost for everyone
  else. No terrain is free for everyone; only roads, Pathfinding and artifacts help.

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

  - **Native = the whole army** (HoMM3): the penalty is waived only if **every** creature in the army
    belongs to one of the terrain's native towns. Neutrals count as non-native.
  - The **Pathfinding** skill reduces the non-native penalty by 25/50/75 % (Basic/Advanced/Expert;
    swamp 200 → 175/150/125); ADR-069 adds Master, −100 % (not built). Artifacts reduce it or make terrains native (implemented, ADR-061).
  - Implemented as data (`TerrainCatalog`); an all-native army (`MovementRules.ArmyTown`) pays 100, and
    Pathfinding applies in `MovementRules.StepCost`, which routes and steps share.
- **Roads (HoMM3-style, ADR-024/032)**: a road layer on top of terrain, drawn in the editor (generated roads
  between towns and through zone connections are planned, [random-maps.md](random-maps.md)). Road types and costs:
  **dirt 50, gravel 65, cobblestone 75**. The road cost replaces the terrain cost and **ignores native penalties**.
  It applies only when the step goes **from a road tile to a road tile**; diagonals ×1.41 as usual.
- 8-way movement; diagonal steps cost ×1.41.
- **Last step**: a hero with any movement points left may make one more step, whatever it costs,
  and ends with 0.
- **Daily movement points** = base from the **slowest creature in the army** (map speed ≤3: 1500, 4: 1560,
  5: 1630, 6: 1700, 7: 1760, 8: 1830, 9: 1900, 10: 1960, ≥11: 2000) × (100 % + skill bonus, e.g.
  Logistics) + flat artifact bonus. Recomputed at the start of the owner's turn. Without an army, 1500.
  The table uses each creature's adventure **map speed** (`MapSpeed`, HoMM3 scale), which is separate from
  its battlefield Speed in cells (ADR-037).

## Pathfinding (implemented)
Cheapest route; visible heroes and object bodies block; routes may only *end* on an object's entrance (except gates,
which continue on the other side); unexplored tiles are never used. Every step carries a day index for HoMM-style day
markers. Heroes the player can't see don't affect the plan; movement stops when it bumps into one.

## Map objects
Objects have a footprint and are entered only through their entrance tile, usually from below.

| Object | Behaviour | Status |
|---|---|---|
| Resource pile | Picked up | implemented |
| Mine | Flagged, daily income (gold 1000, lumber/ore 2, rare 1); may be guarded | implemented |
| Treasure chest | Choose gold *or* XP | implemented |
| Artifact | Picked up into the hero's slots or backpack | implemented (ADR-061) |
| Town | Captured, garrisoned, built up ([towns-and-economy.md](towns-and-economy.md)) | implemented |
| Map dwelling | Flagged, recruits one creature, supplies caravans | implemented (ADR-054) |
| Neutral monster | Fights, joins or flees | implemented (ADR-040) |
| Gates | Teleport (see below) | implemented |
| Marketplace | A visiting hero opens the trading screen for its owner at a fixed rate (as if X were 4, [towns-and-economy.md](towns-and-economy.md#marketplace-implemented-adr-067)); cannot be flagged, any number of visits, does not change town rates; guarded as the map or generator decides | implemented (ADR-067), map format v14 |
| Utility objects | Sign, Observatory, Hill Fort, Town Gate, War Machine Factory ([below](#utility-objects)) | implemented (ADR-068), map format v19 |
| Creature banks | Churchyard, Mansion, Crypt (built as the undead "Ruins", renamed by ADR-070), Dwarven Treasury, Imp Cache, Medusa Stores, Naga Bank ([below](#creature-banks)) | implemented (ADR-068), map format v20; guards to be re-drafted (ADR-070) |
| Prison | Frees a hero for the visitor's player ([below](#treasure-and-one-time-loot)) | implemented (ADR-068), map format v19 |
| Buff sites | Faerie Ring, Fountain of Youth, Temple, Mineral Spring, Stables, Magic Well: statuses for the next battle or the week, movement now ([below](#temporary-buffs)) | implemented (ADR-068), map format v18 |
| Training sites | Learning Stone, Gazebo, Library of Enlightenment, Tree of Knowledge, Arena, Colosseum of the Magi: once per hero ([below](#permanent-hero-power-ups)) | implemented (ADR-068), map format v18 |
| Loot sites | Campfire, Supply Crates, Traveler's Wagon (picked up, vanish), Corpse, Lean To, Warrior's Tomb (stay): only the first visitor finds something ([below](#treasure-and-one-time-loot)) | implemented (ADR-068), map format v17 |
| Resource providers | Water Wheel, Warehouses, Windmill, Mystical Garden: flagged, a week's output stored every week for the owner ([below](#mines-and-resource-providers)) | implemented (ADR-068, ADR-079), map format v16 / v22 |
| Second catalogue | Mage's Chest, Windows and Eyes of the Magi, Tavern, Garrison, Keymaster's Tent, deposits, six more banks, magic sites, three buffs, five power-ups ([below](#second-object-catalogue-adr-069)) | planned (ADR-069) |
| Second catalogue | Mage's Chest, deposits, Windows and Eyes of the Magi, map tavern, portals, garrisons, Keymasters and border posts, six banks, mana sites, buffs, power-ups ([below](#second-object-catalogue-adr-069)) | implemented (ADR-082) except the Shrines, map format v23 |
| Third catalogue | Sphinx, Sanctuary, Sacrificial Altar, Refugee Camp, Den of Thieves, Cartographer, Astrologer's Tower, Artifact Merchant, Witch Hut ([below](#third-object-catalogue-adr-077)) | implemented (ADR-079, ADR-080) except the Astrologer's Tower, map format v22 |
| Scenery | Rocks, trees, dead trees, ponds, bushes, mushrooms, flowers, fissures, mounds, ruins, bones, castle walls: obstacles, all blocking, from `scenery.csv` ([below](#scenery-and-rivers-adr-077-built-adr-079)); rivers too | implemented (ADR-079), map format v22 |

- **Treasure chests roll their reward** (seeded game RNG, ADR-068, replacing ADR-039's three equal tiers): the five-row table
  [below](#treasure-and-one-time-loot), gold *or* XP, or 5 % a Minor artifact taken at once. Map authors (and the generator) may fix the
  gold and XP; a fixed chest never holds an artifact.
- **Mines may be guarded**: the editor or random map generator can attach a guard stack. The guard must be
  defeated before the first flagging; after that any hero re-flags the mine by visiting it. Mine guards always fight.
- **Neutral monsters** (ADR-040): a creature stack on a tile; stepping onto it attacks it, and it guards the 8 tiles
  around it. Every stack has its own **guard mode** (ADR-065), set in the editor's inspector or by the generator:
  - **Classic** (HoMM3, the default, general purpose): entering a guarded tile starts the encounter, but pickups
    lying on guarded tiles can be grabbed from outside without a fight ("stealing"). Passage guards are Classic.
  - **Area** (for treasure guards): the whole 3×3 area is guarded; touching anything inside it, pickups included,
    starts the fight.
  - A pickup next to several stacks can be stolen only when all of them are Classic; otherwise the (first) Area
    stack defends it. Anything else in a guarded tile is defended by the first guard (lowest id). Mine guards have
    no mode: they defend only their mine.
  - Dispositions (HoMM3 names, deterministic): Compliant, Friendly, Aggressive, Hostile, Savage decide whether
    the stack offers to **join** (free, needs room) or tries to **flee** from a much stronger army (≥ 3×), else
    it fights. The player accepts or fights. Unset counts are rolled by tier at game start.
- **Towns** (ADR-046, ADR-050): 7×7 with the corners free, entered through the bottom-middle cell
  (`.BBBBB.`, five rows of `BBBBBBB`, `.BBVBB.`). Entering an enemy or neutral town fights its garrison, if any, and
  captures it; an own hero on the entrance is the town's visiting hero.

## Meeting heroes
Moving onto a tile with another hero is an interaction, like visiting an object, so routes may **end** on that hero's
tile (never pass through it):

| Other hero | Result | Status |
|---|---|---|
| **Own** | Opens the **exchange screen**: swap army stacks and artifacts. The moving hero stays on its adjacent tile. | implemented: stacks move with `MoveStackCommand` (split, merge, swap; a hero keeps one stack), `ExchangePanel` in the game; artifact exchange in core, its UI pending |
| **Enemy** | Starts a **battle**; the loser (or a hero that retreats) leaves the map | implemented |
| **Allied** (team games) | **No interaction for now**: the tile is simply blocked. Exchange with allies may come later. | decided (ADR-027) |

- **Teams**: a game has **2 to 8 teams** (8 = free for all, ADR-070). The map or template defines default teams and
  restrictions (fixed teams, allowed team counts); the game setup screen may change them unless they are locked
  (story/scenario maps). Free-for-all when nothing is defined. The map editor may override what a template set, e.g. on a
  generated map ([random-maps.md](random-maps.md)).
- Under refog: a moving hero **automatically stops** when an enemy hero comes into sight. If it still bumps into an
  unseen enemy, a **battle starts (ambush)**; no ambush bonus (ADR-031, ADR-038).

## Gates and teleporters
No random-exit portals for now (ADR-026). Every link is fixed in the map, so movement and pathfinding stay
deterministic and predictable. "Portal" and "monolith" name the same thing (ADR-070, MAP-18).

| Kind | Behaviour | Status |
|---|---|---|
| **Two-way gate** | Linked pair; visiting either end moves the hero to the other. Subterranean gates connect levels; two-way monoliths work on the same level. | implemented |
| **One-way gate** | An **entrance** linked to exactly one **exit**. Several entrances may share an exit. The exit is a landing spot only; visiting it does nothing, it can't be entered as a gate, and routes may walk over it. | implemented (one-way monolith) |

- Pathfinding goes through both kinds: a route continues from the linked end, and one-way gates only in
  their direction. A blocked exit (a hero standing on it) means no teleport.
- Editor: link tool for both kinds; the validator flags unlinked gates and exits that no entrance uses.

## Fog of war (implemented, ADR-017, ADR-031)
Two modes, **chosen when the game is created** (`GameOptions.FogMode`):
1. **Permanent reveal** (HoMM3): once explored, terrain, objects and units stay visible.
2. **Shroud + refog for units**: terrain and objects stay revealed once explored, but other players'
   heroes are only visible on tiles currently **in sight**. Sight has hysteresis: a tile comes into sight within an
   observer's scouting radius, but only drops out once every observer is farther than radius + **refog margin**
   (default 2, configurable). Example: radius 3 + margin 4 reveals at 3 tiles and refogs at 7.

- Observers: heroes and owned towns (radius 5), owned mines and map dwellings (radius 2). The Scouting skill adds
  2 / 4 / 6 to a hero's radius (ADR-069), revealed at once when the hero learns a level.
- Under refog, **objects out of sight show their last known state** (flag colour, chest still there),
  updated when they come back into sight. Units are hidden entirely (`ObjectMemory`).

## Planned objects (ADR-068)
The owner's catalogue of adventure-map objects (from `ideas.md`, 2026-10-19). **Nothing here is built yet** apart from the
mines, resource piles, treasure chest, artifact pickup, map dwelling and marketplace in the table above; see the roadmap block
"Adventure map objects". Every number is a draft. Questions are `MAP-n` in [open-questions.md](../project/open-questions.md).

### Rules shared by all of them
**Built (2026-10-19)**: the shared rules below (visit scopes, hero statuses, weighted rolls, artifact draws), the hero limit, the
resource providers, the one-time loot, the new treasure chest, the buffs, the power-ups, abandoned mines, dwelling guards, the
utility objects, the prison and the creature banks: the whole catalogue. The random map generator places them as treasure by value
(all but signs and map dwellings, [random-maps.md](random-maps.md); generator version 3).

- **Artifact tiers** (decided, MAP-1): **Minor** (m), **Major** (M), **Relic** (R) as in `artifacts.csv`; the notes' extra *Treasure*
  tier is dropped, so every (t) in the notes became Minor. A **Combo** artifact is never dropped, only assembled from its pieces,
  which drop as usual (the game has sets but no combination artifacts yet). Relics are drawn from a pool without repeats; when the pool is empty it is **refilled** and the draw starts anew,
  so a hero may hold two copies of one Relic.
- **Visit scopes** (decided): *weekly* objects keep a per-object week counter that resets on Monday, so a hero gets the reward
  once a week unless someone else has taken it that week (a visit to an emptied object reports so and gives nothing); *once per hero* objects keep a visited-by set of hero ids; *first
  visitor* objects are spent after one visit; *vanishing* objects are removed after the visit. The scope is data of the object
  definition and the map format records the state.
- **Temporary effects** ("for the next battle", "until the end of the day", "for the rest of the week") are a hero status that
  expires at the stated moment; morale and luck stay within ±5 in battle (ADR-053). Setting a status is a published event. A second
  status from the same object and of the same kind replaces the first; different objects add up, and battles cap the total at ±5
  (decided, MAP-10). A buff belongs to the hero that visited, not to heroes it meets later.
  "Until the end of the day" ends at the owner's next turn start, "the rest of the week" when the next week starts, "the next
  battle" when that battle ends, whatever its outcome and whoever the enemy is: a hero, neutral guards, a garrison, a bank or a mine
  guard (decided, MAP-10).
- **Random rewards** draw from `GameState.Random` when the visit happens; a map author may fix the outcome.
- **Resource groups**: *basic* = lumber, ore; *magic* = crystal, sulfur, mercury (ADR-067); "random resource" never means gold.
- **Creature names** are mapped to our roster (the notes used HoMM3 names, ADR-068): Zombie → Ghoul, Power Lich → Arch Lich,
  Dwarf / Battle Dwarf are new neutral tier-2 creatures (`neutral.dwarf`, `neutral.battle-dwarf`; draft stats, decided 2026-10-19), Medusa Queen → Putrid Medusa, Cavalier → Lancer
  (its upgrade is the Champion). Troglodyte, Imp, Familiar, Medusa, Naga, Naga Queen, Skeleton, Skeleton Warrior, Wight, Wraith and
  Vampire Lord exist as written. Neutral stacks use the creature table's stats.

### Mines and resource providers
| Object | Effect |
|---|---|
| Gold Mine, Sawmill, Ore Mine, Alchemist's Lab, Crystal Pond, Sulfur Dune | Flagged; daily +1000 gold, +2 lumber, +2 ore, +1 mercury, +1 crystal, +1 sulfur (**implemented** as the mines; the notes only rename them) |
| Abandoned versions of each | **Implemented.** A **separate object** (decided, MAP-2), guarded by Troglodytes: **150–295** at the gold mine, **100–245** elsewhere, rolled when the game starts (a guard the map sets is kept). When the guard is defeated it **turns into the normal mine** of its kind, flagged for the winner. The gold mine and the ore mine fight on **subterranean** terrain, the others on the terrain they stand on; on subterranean ground the Troglodytes (Dungeon creatures) get the native-terrain bonus (+1 Attack, Defense and Speed, [combat.md](combat.md#damage)). A normal mine keeps today's optional guard with a free choice of guard. |
| Water Wheel | **Implemented.** A week's output: **500 gold** in the game's first week, **1000 gold** after that |
| Warehouse of Wood / Ore | **Implemented.** A week: **8** lumber / ore **or 500 gold**, decided at random each week (no choice, ADR-079) |
| Warehouse of Sulfur / Mercury / Crystal | **Implemented.** A week: **5** of that resource **or 500 gold**, at random |
| Warehouse of Gold | **Implemented.** A week: **2000 gold** |
| Windmill | **Implemented.** A week: **3–6** of one random non-gold resource |
| Mystical Garden | **Implemented.** A week: **500 gold or 5 of one random magic resource** at even odds (MAP-3) |
| *All providers* | **Implemented (ADR-079, TOWN-9)**: flagged like mines (scouting radius 2). While flagged, a week's output is added to the provider's store at the start of every week (and at game start when owned from the start); it piles up. The first flagging of a neutral provider brings one week at once; capturing an enemy's takes what it stored. An own hero's visit takes the whole store; a town's caravan can fetch it for 100 gold ([towns-and-economy.md](towns-and-economy.md#map-dwellings-and-caravans-implemented-adr-054)). A defeated player's providers turn neutral and keep their store. |

### Treasure and one-time loot
| Object | Effect |
|---|---|
| Campfire | **Implemented.** 400, 500 or 600 gold and **4–6 of one random non-gold resource**, e.g. 5 lumber or 4 crystal; vanishes |
| Supply Crates | **Implemented.** Two different random non-gold resources, **4–6 of each** (e.g. 6 sulfur and 4 ore); vanishes |
| Resource Pile | **Implemented.** An amount the map leaves open (map format v21; every pile placed in the editor) is rolled when the game starts: **5–10** lumber / ore, **3–7** of a magic resource, **500–1000** gold in hundreds; a map may fix any amount (older maps keep theirs, the generator's treasure piles are fixed by their value); vanishes |
| Traveler's Wagon | **Implemented.** First visitor: 10 % nothing, 30 % Minor artifact, 60 % 2–5 of one random non-gold resource; vanishes |
| Treasure Chest | **Implemented.** Gold or experience, chosen by the player: 30 % 1000 gold or 500 XP; 30 % 1500 or 1000; 25 % 2000 or 1500; 10 % 2500 or 2000; **5 % a Minor artifact**; vanishes. Replaces the three-tier chest of ADR-039 (answers the chest half of CONT-3 and RMG-1) |
| Corpse | **Implemented.** First visitor: 80 % nothing, 20 % a Minor artifact; stays |
| Warrior's Tomb | **Implemented.** First visitor: 80 % Minor, 15 % Major, 5 % Relic artifact, and **−3 morale for the next battle**. Every later visitor gets only the −3 |
| Lean To | **Implemented.** First visitor: 1–6 of one random non-gold resource; stays |
| Prison | **Implemented.** A hero joins the visitor's player; vanishes. The level is rolled: **30 % level 1, 30 % level 5, 20 % level 10, 15 % level 20, 5 % level 30**, or the map author sets the hero. When the map is generated the prison's **faction is drawn at random** and the hero is picked from it, never one of the heroes the players chose as starting heroes (and not one already on the map). If the visitor already has the most heroes the map allows, the **visit fails** with a notification and the prison stays. A hero above level 1 gets the experience for its level and its skills and stats from level-ups the game chooses |

### Creature dwellings
A map dwelling (**implemented**, ADR-054) is one creature line with its own growth. **Implemented (ADR-068)**: a neutral dwelling of
**tier 3 / 4 / 5 is guarded by 9 / 6 / 3 base (non-upgraded) creatures** of the line it produces, set when the game starts; tiers 1
and 2, and dwellings the map gives an owner, are unguarded. The guard always fights; beating it flags the dwelling. This answers the
"guards on map dwellings" part of TOWN-5.

**Base creatures only** (ADR-073, built with ADR-075): for now a map dwelling always sells a base (non-upgraded) creature. The editor offers
only base creatures and refuses an upgrade, the validator reports one as an error, and loading a map turns an upgraded dwelling into its
line's base creature.

### Creature banks
**Implemented.** A bank is a new object kind. Entering it starts a battle against the rolled guard; guards **always fight** (no join, no flee,
unlike neutral monsters, ADR-040). The reward is paid only if the visitor wins; the bank is then **plundered**. The guard's
tier is rolled when the map starts from the percentages below; it is **not shown** on the map and an author cannot fix it. "N each" in the
table means N of every named resource. The battle uses the **default width** (no Tactics choice). If the hero retreats or
perishes, the guard stays **intact** and the bank can be tried again. A hero without an army cannot enter. Each kind of creature in the guard
is split into a few battle stacks (at most seven stacks in all).

| Bank | Guard and reward |
|---|---|
| Churchyard | 100 %: 90 Ghouls, 2500 gold, a Minor artifact. A later visit to the plundered bank gives **−1 morale for the next battle**; no other bank has such a penalty |
| Mansion | 30 %: 40 Vampire Lords, 2500 gold, 2 each of mercury / sulfur / crystal, a Major artifact · 30 %: 60, 5000 gold, 3 each, Major · 30 %: 80, 7500 gold, 4 each, Major · 10 %: 100, 10000 gold, 5 each, Major |
| Crypt (`bank.crypt`; `bank.ruins` before ADR-082) | 30 %: 20 Skeletons, 3 Wights, 3 Wraiths, 10 Skeleton Warriors, 1 Arch Lich, 1000 gold · 30 %: 30 / 5 / 5 / 10 / 1, 2000 gold · 30 %: 40 / 7 / 7 / 20 / 2, 3000 gold · 10 %: 50 / 9 / 9 / 20 / 3, 4000 gold. Also **removes the fog within 12 tiles** of the ruins, even when they were visited before |
| Dwarven Treasury | 30 %: 50 Defenders, 2500 gold, 2 crystal · 30 %: 75, 4000, 4 · 30 %: 100, 5000, 6 · 10 %: 150, 7500, 10. 50 % chance that a fifth of the guard are Shield Guards |
| Imp Cache | 30 %: 100 Imps, 1000 gold, 2 mercury · 30 %: 150, 1500, 4 · 30 %: 200, 2000, 6 · 10 %: 300, 3000, 8. 50 % chance that a fifth are Familiars |
| Medusa Stores | 30 %: 15 Medusas, 2000 gold, 4 sulfur · 30 %: 30, 3000, 6 · 30 %: 40, 4000, 8 · 10 %: 50, 5000, 10. 50 % chance that a fifth are Putrid Medusas |
| Naga Bank | 30 %: 5 Nagas, 3000 gold · 30 %: 10, 6000 · 30 %: 15, 9000 · 10 %: 20, 12000. 50 % chance that a fifth are Naga Queens |

**Refill** (decided, MAP-7, MAP-14): every bank fills again **4 weeks** after it was plundered; its guard tier is rolled again and the whole
reward, the artifact included, comes back.

**Re-draft (ADR-070, not built)**: every bank, these and the [second catalogue's](#creature-banks-1), gets a new guard of **2–3 creature
types of one faction**; the tables above are the built state until the drafts are written. The undead Ruins is renamed **Crypt**
(`bank.crypt`; older maps load `bank.ruins` as the Crypt), and the name Ruins goes to the orc bank (`bank.orc-ruins`); the rename is built
(ADR-082).

### Permanent hero power-ups
| Object | Effect |
|---|---|
| Gazebo | **Implemented.** The visiting hero may pay **1500 gold** for **2500 experience**, once per hero; without the gold it is turned away |
| Learning Stone | **Implemented.** **+1000 experience** on the first visit of each hero |
| Library of Enlightenment | **Implemented.** **+2** to all four primary skills for a hero of **level 10 or more**, once per hero; a lower-level hero gets nothing and a message |
| Tree of Knowledge | **Implemented.** Once per hero per tree: the hero's experience rises to the next level; the player pays **2500 gold or 5 crystal, 5 mercury and 5 sulfur**, choosing which (no free option; decided, MAP-9) |
| Arena | **Implemented.** Once per hero: **+2 Attack or +2 Defense**, the player must choose |
| Colosseum of the Magi | **Implemented.** Once per hero: **+2 Spell Power or +2 Knowledge**, the player must choose |

A paid power-up (Gazebo, Tree) offers only the prices the player can pay and may be left without paying; a visit that trains nothing
does not count.

### Temporary buffs
| Object | Effect |
|---|---|
| Faerie Ring | **Implemented.** +1 luck for the next battle |
| Fountain of Youth | **Implemented.** +400 movement points until the end of the day and +1 morale for the next battle |
| Temple | **Implemented.** +2 morale for the next battle, **+3 on day 7** of the week (the calendar day, MAP-10) |
| Mineral Spring | **Implemented.** +1 luck for the next battle and +600 movement points until the end of the day |
| Stables | **Implemented.** +400 movement points at once and a day **for the rest of the week** (once a week per hero); upgrades Lancers to Champions for free on every visit |
| Magic Well | **Implemented.** Restores 100 % of the hero's mana once a day (useful with magic, Phase 5) |

Buffs of different objects stack within the ±5 battle cap; visiting the same object again only renews its buff (MAP-10). Each hero
takes a buff once a day (the Stables once a week), so walking off and on again gives nothing.

### Utility objects
| Object | Effect |
|---|---|
| Sign | **Implemented.** Shows the map author's message |
| Observatory | **Implemented.** Reveals the terrain within **20 tiles** of it at once (every visit; it remembers which heroes looked) |
| Hill Fort | **Implemented.** Upgrades the hero's stacks of tiers **1–3**, the player picking stack by stack, for **150 % of the upgrade price** (the price difference, as in towns); tiers 4 and 5 are upgraded only in towns (decided, MAP-11) |
| Town Gate | **Implemented.** The visiting hero teleports to one of **its owner's towns** (teammates' towns not for now) at no movement cost. The player picks the town from a list; a town whose entrance is occupied is listed but unavailable. Routes do not plan through it (decided, MAP-12) |
| War Machine Factory | **Implemented.** Sells the visiting hero **its own offensive machine** (ballista, cannon, catapult or glaivethrower, ADR-056), the **ammo cart** and the **healing tent**, at the **blacksmith's prices**; not a machine the hero already has (decided, MAP-13) |

## Second object catalogue (ADR-069)
The owner's second list (`ideas.md`, 2026-10-20). **Built** (ADR-082, map format v23) except the Shrines (Phase 5) and the faction
re-draft of the banks' guards; the prices and bank tables below are ADR-082's drafts. The shared rules above (visit scopes, statuses, rolls, artifact
tiers, creature names) apply. MAP-15 … MAP-24 were answered by ADR-070; MAP-20 (Keymasters) and the map tavern's pool (TOWN-4) by ADR-071;
the points once marked *(assumed)* were confirmed in ADR-073.
Every number is a draft.

### Loot
| Object | Effect |
|---|---|
| Mage's Chest | 5 % nothing, 5 % a Major artifact, 60 % a Minor artifact, 30 % a Minor artifact and 500–1000 gold; vanishes (ADR-070, MAP-15) |

### Utilities and travel
| Object | Effect | Open |
|---|---|---|
| Window of the Magi / Eye of the Magi | Four colours (red, green, blue, black). Visiting a Window reveals **radius 6** around every Eye of its colour; at most **4 Eyes** a colour. Works on every visit (like HoMM3; ADR-073). Placed only in the map editor, never by the generator (ADR-070) | – |
| Tavern | Works like a **Brotherhood** (4 offers, ADR-060). The hero must stand on its entrance; the new hero appears on a free cell next to it (one cell below by default). The hero limit applies; with the limit reached or no free cell, hiring is refused (ADR-070). Its pool is **its own**: 4 random heroes of any faction, rolled weekly and shared by all players (ADR-071, TOWN-4) | – |
| Portal | Takes the hero to the other end of its type: today's **two-way monolith** (implemented). A type has **exactly two ends**, no groups of three or more (ADR-070, MAP-18) | – |
| One-Way Portal | Takes the hero to its exit, which cannot be entered: today's **one-way monolith** (implemented). Templates may let the generator link zones with portals (RMG-11, [random-maps.md](random-maps.md)) | – |
| Garrison | **Flagged like a mine** and captured by beating its guards; a neutral garrison's guards come from the map or the generator. The owner's heroes pass both ways and may leave or take creatures at any time; enemies must defeat the guards to pass. The generator may use it as a connection guard (ADR-070, MAP-19, RMG-11) | – |
| Keymaster's Tent | **Four colours** (like the Eyes of the Magi). A visit gives the **player** the key of its colour (every hero of that player holds it). A **border guard** of that colour vanishes after the first key holder passes; a **border gate** stays and lets every key holder through. Templates may guard a connection with either, an option per connection (ADR-071, MAP-20, [random-maps.md](random-maps.md)) | – |

### Deposits (become mines)
A visiting hero pays from its player's resources and the deposit turns into the mine of its kind, flagged for that player. Any hero
may pay; a deposit may have a normal monster guard. The generator places deposits separately, like abandoned mines (ADR-070,
MAP-21). Prices (drafts, ADR-082): Gold Vein **5000 gold + 10 ore**; Wood Pile **2000 + 5 ore**; Ore Deposit **2000 + 5 lumber**; Crystal Vein,
Cinnabar Deposit and Sulfur Vent **3000 + 5 ore**. The player is asked before paying.

| Deposit | Becomes |
|---|---|
| Wood Pile | Sawmill |
| Ore Deposit | Ore Pit (the Ore Mine) |
| Gold Vein | Gold Mine |
| Crystal Vein | Crystal Mine (the Crystal Pond) |
| Cinnabar Deposit | Alchemist's Lab |
| Sulfur Vent | Sulfur mine (the Sulfur Dune; added by ADR-070) |

### Creature banks
Like the banks above: the guard always fights, the reward only on victory. Tables follow the existing banks: four rows at
30 / 30 / 30 / 10 %, rewards scaled to the guard's value (ADR-070, MAP-22). Like every bank, each guard is **2–3 creature types of one
faction**; the creatures below are the notes' starting point and the **drafts are pending**. Dragon City is the strongest bank by far.
**Built (ADR-082)** with the notes' creatures: Ruins 20 / 30 / 40 / 60 Orcs (a fifth Berserkers half the time) for 2000–6000 gold; Lair
4 / 6 / 8 / 12 Spectators (a fifth Beholders) for 2000–6000 gold and a Minor artifact (Major in the top row); Keep 20–60 Pikemen and 10–30
Archers for 1500–5000 gold and a Minor (Major) artifact; Hideout 15–50 Bandits for 1500–5000 gold; Forest Glen 30–100 Timber Wolves (a
fifth Dire Wolves) for 1000–3000 gold and 5–20 lumber; Dragon City 3 / 4 / 5 / 6 Black Dragons for 10000–30000 gold, a Relic and 1–3 Major
artifacts.

| Bank | Guard (notes, roster creatures) | Reward (notes) |
|---|---|---|
| Ruins (orcs; the undead bank became the Crypt, ADR-070) | Orc / Orc Berserker | gold |
| Lair | evil eyes: Spectator / Beholder | gold and artifacts |
| Keep | crossbowmen and pikemen: Archer / Marksman and Pikeman / Guardian | gold and artifacts ("item artifacts") |
| Hideout | Bandit (neutral, no upgrade) | gold |
| Forest Glen | Timber Wolf / Dire Wolf (neutral) | gold and lumber |
| Dragon City | Black Dragon (Dragon Utopia, no upgrade) | gold, a Relic and Major artifacts |

### Magic
| Object | Effect |
|---|---|
| Mana Vortex | Sets the hero's mana to **twice the maximum** until it is spent (HoMM3); once a week per hero (ADR-070, MAP-23) |
| Fountain of Magic | Restores 50 % of the hero's maximum mana; **once a week per hero** (ADR-070, ADR-072) |
| Shrine (levels 1–3) | Teaches its spell, set in the editor or rolled at game start. Learning needs the school's Magic skill at the spell's level; any hero, any number of visits; a hero without the skill or with the spell already gets a message (ADR-070, MAP-24; needs spells, Phase 5) |

### Temporary buffs
Same rules as the buffs above (once a day per hero, gone after the next battle, ±5 cap):

| Object | Effect |
|---|---|
| Clover Field | +1 luck for the next battle |
| Rainbow | +2 luck for the next battle |
| Rally Flag | +400 movement points until the end of the day, +1 luck and +1 morale for the next battle |

### Permanent power-ups
Once per hero, like the Learning Stone:

| Object | Effect |
|---|---|
| Dream Teacher | 500 experience, plus 500 for every other Dream Teacher the hero has visited on this map |
| Training Grounds | +1 Attack |
| Marletto Tower | +1 Defense |
| Star Axis | +1 Spell Power |
| Garden of Revelation | +1 Knowledge |

## Scenery and rivers (ADR-077, built ADR-079)
- **Scenery** (MAP-25): static objects with no interaction and no entrance, from `src/Core/data/scenery.csv`: rocks, trees, dead trees,
  ponds, bushes, mushrooms, flowers, fissures, mounds, ruined buildings, creature bones and castle walls, each kind in two or more sizes.
  **Every cell blocks** (no passable decoration for now). Castle walls are 1×N straight pieces: horizontal, vertical and diagonal both ways.
  Each row lists the terrains it suits; the generator fills its obstacle clumps with fitting scenery; the editor places any of them.
- **Rivers** (MAP-26): a tile overlay like roads, four looks (clear, icy, muddy, lava). The opposite of a road: **leaving a river tile
  costs 25 % more** (Pathfinding does not cut it); a road-to-road step is a bridge and ignores the river. Caravans ignore rivers. The editor
  paints them with a brush; the generator runs some across zones from wall to wall.

## Third object catalogue (ADR-077)
The owner's third list (`ideas.md`, 2026-10-25), answered and built in ADR-079 and ADR-080 except the Astrologer's Tower. The shared rules
above apply. The **Subterranean Gate** on the list exists already (two-way gate between levels, [above](#gates-and-teleporters)). Numbers are
drafts.

| Object | Rules | Status |
|---|---|---|
| Sphinx | One riddle per hero: the map author's or one from `riddles.csv`, three answers. Right: a **Major artifact or 5000 experience** (even odds). Wrong: the hero's movement ends for the day and it has **−3 morale** in its next battle | implemented (MAP-27) |
| Sanctuary | A hero on its entrance **cannot be attacked**; it stays as long as it likes, one hero at a time; nothing else | implemented (MAP-28) |
| Sacrificial Altar | Necropolis, Inferno, Dungeon heroes sacrifice creatures for **hit points × 2** experience (keeping one creature); Haven, Sylvan, Academy heroes artifacts for **2000 / 5000 / 10 000** by tier; Stronghold and Swamp Bestiary heroes either; any number of times | implemented (MAP-29) |
| Refugee Camp | Each week a random **tier 3–5 base creature** of the playable towns, one week's growth, at the normal price; not flaggable, unsold creatures leave | implemented (MAP-30) |
| Den of Thieves | Shows the **whole** Thieves' Guild table on every visit ([towns-and-economy.md](towns-and-economy.md#thieves-guild-adr-077)) | implemented (TOWN-11) |
| Cartographer | Surface or underground; **3000 gold** reveals that whole level for the player, once per player | implemented (MAP-31) |
| Astrologer's Tower | A visit shows next week's event; no re-roll | waits for the week events (MAP-32) |
| Artifact Merchant | **7** Minor or Major artifacts for **twice their worth** (Minor 2000, Major 5000), restocked on the first day of every month; a sold slot stays empty until then | implemented (MAP-33) |
| Witch Hut | **Two skills** of any group (set by the map or rolled); a visit raises one a level, or the same group's next skill where the hero cannot; **one lesson per hero level** | implemented (MAP-34) |
