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
    swamp 200 → 175/150/125). Artifacts reduce it or make terrains native (implemented, ADR-061).
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
| Loot sites | Campfire, Supply Crates, Traveler's Wagon (picked up, vanish), Corpse, Lean To, Warrior's Tomb (stay): only the first visitor finds something ([below](#treasure-and-one-time-loot)) | implemented (ADR-068), map format v17 |
| Resource providers | Water Wheel, Warehouses, Windmill, Mystical Garden: resources once a week per object ([below](#mines-and-resource-providers)) | implemented (ADR-068), map format v16 |
| Visitables, loot, banks, trainers, buffs, utilities | The catalogue in [Planned objects](#planned-objects-adr-068) below | planned (ADR-068) |

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

- **Teams**: the map defines default teams; the game setup screen may change them unless the map locks them
  (story/scenario maps). Free-for-all when nothing is defined. Templates have no teams yet ❓.
- Under refog: a moving hero **automatically stops** when an enemy hero comes into sight. If it still bumps into an
  unseen enemy, a **battle starts (ambush)**; no ambush bonus (ADR-031, ADR-038).

## Gates and teleporters
No random-exit portals for now (ADR-026). Every link is fixed in the map, so movement and pathfinding stay
deterministic and predictable.

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

- Observers: heroes and owned towns (radius 5), owned mines and map dwellings (radius 2).
- Under refog, **objects out of sight show their last known state** (flag colour, chest still there),
  updated when they come back into sight. Units are hidden entirely (`ObjectMemory`).

## Planned objects (ADR-068)
The owner's catalogue of adventure-map objects (from `ideas.md`, 2026-10-19). **Nothing here is built yet** apart from the
mines, resource piles, treasure chest, artifact pickup, map dwelling and marketplace in the table above; see the roadmap block
"Adventure map objects". Every number is a draft. Questions are `MAP-n` in [open-questions.md](../project/open-questions.md).

### Rules shared by all of them
**Built (2026-10-19)**: the shared rules below (visit scopes, hero statuses, weighted rolls, artifact draws), the hero limit, the
resource providers, the one-time loot and the new treasure chest.

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
  Dwarf / Battle Dwarf → Defender / Shield Guard (placeholder, ❓ CONT-2), Medusa Queen → Putrid Medusa, Cavalier → Lancer
  (its upgrade is the Champion). Troglodyte, Imp, Familiar, Medusa, Naga, Naga Queen, Skeleton, Skeleton Warrior, Wight, Wraith and
  Vampire Lord exist as written. Neutral stacks use the creature table's stats.

### Mines and resource providers
| Object | Effect |
|---|---|
| Gold Mine, Sawmill, Ore Mine, Alchemist's Lab, Crystal Pond, Sulfur Dune | Flagged; daily +1000 gold, +2 lumber, +2 ore, +1 mercury, +1 crystal, +1 sulfur (**implemented** as the mines; the notes only rename them) |
| Abandoned versions of each | A **separate object** (decided, MAP-2), guarded by Troglodytes: **150–295** at the gold mine, **100–245** elsewhere, rolled when the map starts. When the guard is defeated it **turns into the normal mine** of its kind. The gold mine and the ore mine fight on **subterranean** terrain with all its native bonuses, the others on the terrain they stand on. A normal mine keeps today's optional guard with a free choice of guard. |
| Water Wheel | **Implemented.** A visiting hero gets **500 gold** in the game's first week and **1000 gold** once a week after that (weekly) |
| Warehouse of Wood / Ore | **Implemented.** Weekly: **8** lumber / ore **or 500 gold**, the visitor chooses (the week counts as taken once chosen) |
| Warehouse of Sulfur / Mercury / Crystal | **Implemented.** Weekly: **5** of that resource **or 500 gold** |
| Warehouse of Gold | **Implemented.** Weekly: **2000 gold** |
| Windmill | **Implemented.** Weekly: **3–6** of one random non-gold resource |
| Mystical Garden | **Implemented.** Weekly: a random reward (even odds), **500 gold or 5 of one random magic resource**; the visitor does not choose (decided, MAP-3) |

### Treasure and one-time loot
| Object | Effect |
|---|---|
| Campfire | **Implemented.** 400, 500 or 600 gold and **4–6 of one random non-gold resource**, e.g. 5 lumber or 4 crystal; vanishes |
| Supply Crates | **Implemented.** Two different random non-gold resources, **4–6 of each** (e.g. 6 sulfur and 4 ore); vanishes |
| Resource Pile | **Implemented**, with fixed defaults inside the notes' ranges (750 gold, 7 lumber / ore, 4 magic; an author may set any amount). Rolling within 5–10 lumber / ore, 3–7 magic, 500–1000 gold is a follow-up; vanishes |
| Traveler's Wagon | **Implemented.** First visitor: 10 % nothing, 30 % Minor artifact, 60 % 2–5 of one random non-gold resource; vanishes |
| Treasure Chest | **Implemented.** Gold or experience, chosen by the player: 30 % 1000 gold or 500 XP; 30 % 1500 or 1000; 25 % 2000 or 1500; 10 % 2500 or 2000; **5 % a Minor artifact**; vanishes. Replaces the three-tier chest of ADR-039 (answers the chest half of CONT-3 and RMG-1) |
| Corpse | **Implemented.** First visitor: 80 % nothing, 20 % a Minor artifact; stays |
| Warrior's Tomb | **Implemented.** First visitor: 80 % Minor, 15 % Major, 5 % Relic artifact, and **−3 morale for the next battle**. Every later visitor gets only the −3 |
| Lean To | **Implemented.** First visitor: 1–6 of one random non-gold resource; stays |
| Prison | A hero joins the visitor's player; vanishes. The level is rolled: **30 % level 1, 30 % level 5, 20 % level 10, 15 % level 20, 5 % level 30**, or the map author sets the hero. When the map is generated the prison's **faction is drawn at random** and the hero is picked from it, never one of the heroes the players chose as starting heroes (and not one already on the map). If the visitor already has the most heroes the map allows, the **visit fails** with a notification and the prison stays. A hero above level 1 gets the experience for its level and its skills and stats from level-ups the game chooses |

### Creature dwellings
A map dwelling (**implemented**, ADR-054) is one creature line with its own growth. New: dwellings of **tier 3 / 4 / 5 are
guarded by 9 / 6 / 3 base (non-upgraded) stacks** of the creature they produce; tiers 1 and 2 are unguarded. This answers the
"guards on map dwellings" part of TOWN-5.

### Creature banks
A bank is a new object kind. Entering it starts a battle against the rolled guard; guards **always fight** (no join, no flee,
unlike neutral monsters, ADR-040). The reward is paid only if the visitor wins; the bank is then **plundered**. The guard's
tier is rolled when the map starts from the percentages below; it is **not shown** on the map and an author cannot fix it. "N each" in the
table means N of every named resource. The battle uses the **default width** (no Tactics choice). If the hero retreats or
perishes, the guard stays **intact** and the bank can be tried again.

| Bank | Guard and reward |
|---|---|
| Churchyard | 100 %: 90 Ghouls, 2500 gold, a Minor artifact. A later visit to the plundered bank gives **−1 morale for the next battle**; no other bank has such a penalty |
| Mansion | 30 %: 40 Vampire Lords, 2500 gold, 2 each of mercury / sulfur / crystal, a Major artifact · 30 %: 60, 5000 gold, 3 each, Major · 30 %: 80, 7500 gold, 4 each, Major · 10 %: 100, 10000 gold, 5 each, Major |
| Ruins | 30 %: 20 Skeletons, 3 Wights, 3 Wraiths, 10 Skeleton Warriors, 1 Arch Lich, 1000 gold · 30 %: 30 / 5 / 5 / 10 / 1, 2000 gold · 30 %: 40 / 7 / 7 / 20 / 2, 3000 gold · 10 %: 50 / 9 / 9 / 20 / 3, 4000 gold. Also **removes the fog within 12 tiles** of the ruins, even when they were visited before |
| Dwarven Treasury | 30 %: 50 Defenders, 2500 gold, 2 crystal · 30 %: 75, 4000, 4 · 30 %: 100, 5000, 6 · 10 %: 150, 7500, 10. 50 % chance that a fifth of the guard are Shield Guards |
| Imp Cache | 30 %: 100 Imps, 1000 gold, 2 mercury · 30 %: 150, 1500, 4 · 30 %: 200, 2000, 6 · 10 %: 300, 3000, 8. 50 % chance that a fifth are Familiars |
| Medusa Stores | 30 %: 15 Medusas, 2000 gold, 4 sulfur · 30 %: 30, 3000, 6 · 30 %: 40, 4000, 8 · 10 %: 50, 5000, 10. 50 % chance that a fifth are Putrid Medusas |
| Naga Bank | 30 %: 5 Nagas, 3000 gold · 30 %: 10, 6000 · 30 %: 15, 9000 · 10 %: 20, 12000. 50 % chance that a fifth are Naga Queens |

**Refill** (MAP-7, MAP-14): a plundered bank fills again **4 weeks** after it was plundered.

### Permanent hero power-ups
| Object | Effect |
|---|---|
| Gazebo | The visiting hero pays **1500 gold** for **2500 experience**, once per hero |
| Learning Stone | **+1000 experience** on the first visit of each hero |
| Library of Enlightenment | **+2** to all four primary skills for a hero of **level 10 or more**, once per hero; a lower-level hero gets nothing and a message |
| Tree of Knowledge | Once per hero per tree: the hero's experience rises to the next level; the player pays **2500 gold or 5 crystal, 5 mercury and 5 sulfur**, choosing which (no free option; decided, MAP-9) |
| Arena | Once per hero: **+2 Attack or +2 Defense**, the player chooses |
| Colosseum of the Magi | Once per hero: **+2 Spell Power or +2 Knowledge**, the player chooses |

### Temporary buffs
| Object | Effect |
|---|---|
| Faerie Ring | +1 luck for the next battle |
| Fountain of Youth | +400 movement points until the end of the day and +1 morale for the next battle |
| Temple | +2 morale for the next battle, **+3 on day 7** of the week (the calendar day, MAP-10) |
| Mineral Spring | +1 luck for the next battle and +600 movement points until the end of the day |
| Stables | +400 movement points a day **for the rest of the week**; upgrades Lancers to Champions for free |
| Magic Well | Restores 100 % of the hero's mana once per turn (useful with magic, Phase 5) |

Buffs of different objects stack within the ±5 battle cap; visiting the same object again only renews its buff (MAP-10).

### Utility objects
| Object | Effect |
|---|---|
| Sign | Shows a custom message |
| Observatory | Reveals the terrain within **20 tiles** of the hero at once |
| Hill Fort | Upgrades creatures of tiers **1–3** for **150 %** of the upgrade price (MAP-11) |
| Town Gate | The visiting hero teleports to **any allied town** at no movement cost (MAP-12) |
| War Machine Factory | Sells the visiting hero an **Ammo Cart for 1000 gold**, a **Ballista for 2500 gold** and a **First Aid Tent for 750 gold** (MAP-13) |
