# Game design (living document)

> Status: **draft**. Items marked ❓ are open; see [open-questions.md](open-questions.md).

## 1. Vision

*Eternal Wars* is a turn-based **dark fantasy** strategy game inspired by Heroes of Might and Magic III,
presented in full 3D. Players lead heroes across an adventure map, gather resources, develop towns,
recruit armies and fight tactical battles on a square grid where creatures occupy 2×2, 3×3 or 4×4
cells.

It is **HoMM-inspired, not a clone**: we keep the proven loop (heroes, towns, weekly growth, tactical
battles) and deliberately change systems where we can add identity. Every twist is logged in §8.

The 3D presentation is meant to **add atmosphere without hurting readability**: the camera, map and
battles must stay as legible as HoMM3's hand-painted 2D.

**Modes, in priority order**: single-player vs AI → hot-seat → story campaign. Online multiplayer is
out of scope, but the deterministic core keeps it possible.

## 2. Pillars

1. **"One more turn"**: short, satisfying turns; meaningful decisions every day of the week.
2. **Readable at a glance**: every tile, object and unit identifiable from the default camera.
3. **Moddable by data**: factions, units, spells, artifacts and map objects are data, not code.
4. **Deterministic core**: the same inputs always produce the same game. This enables replays, tests,
   AI simulation and (later) multiplayer.

## 3. Core loop

```
 ┌──────────── Day (each player in turn) ────────────┐
 │ Move heroes ─► visit objects / pick up loot        │
 │      │                 │                           │
 │      ▼                 ▼                           │
 │   Battles ◄──── Neutral / enemy armies             │
 │      │                                             │
 │      ▼                                             │
 │ Experience, artifacts ─► stronger heroes           │
 │ Towns: build 1 structure/day, recruit weekly growth│
 └──────────────── End turn ─────────────────────────┘
        Week start: creature growth, "week of X" events
```

## 4. Feature set (target for 1.0)

### 4.1 Adventure map
- **Map sizes**: square presets 48, 72, 96, 128, 160, 192 and 256 (max), each with an optional
  underground level. **Up to 8 players.**
- Square grid, up to 2 levels (surface + underground) connected by **subterranean gates** (implemented).
- **Movement (HoMM3 rules, implemented)**:
  - A step costs the terrain of the tile being **left**. Water/rock are impassable on foot.
  - **Terrain costs** (accepted, ADR-045): leaving a tile costs **100 for an all-native army** and the terrain's
    cost for everyone else. No terrain is free for everyone; only roads, Pathfinding and artifacts help.

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
      belongs to one of the terrain's native towns. Neutrals count as non-native (decided).
    - The **Pathfinding** secondary skill reduces the non-native penalty by 25/50/75 % (basic/advanced/expert;
      swamp 200 → 175/150/125); artifacts can remove it (with artifacts, Phase 3).
    - Implemented as data (`TerrainCatalog`); an all-native army (`MovementRules.ArmyTown`) pays 100, and
      Pathfinding applies in `MovementRules.StepCost`, which routes and steps share.
  - **Roads (HoMM3-style)**: a road layer on top of terrain, drawn in the editor and generated
    between towns and through zone connections. Road types and costs: **dirt 50, gravel 65,
    cobblestone 75**. The road cost replaces the terrain cost and **ignores native/rough penalties**.
    It applies only when the step goes **from a road tile to a road tile** (HoMM3); diagonals ×1.41 as usual.
  - 8-way movement; diagonal steps cost ×1.41.
  - **Last step**: a hero with any movement points left may make one more step, whatever it costs,
    and ends with 0.
  - **Daily movement points** = base from the **slowest creature in the army** (speed ≤3: 1500, 4: 1560,
    5: 1630, 6: 1700, 7: 1760, 8: 1830, 9: 1900, 10: 1960, ≥11: 2000) × (100 % + skill bonus, e.g.
    Logistics) + flat artifact bonus. Recomputed at the start of the owner's turn. Without an army, 1500.
    The table uses each creature's adventure **map speed** (`MapSpeed`, HoMM3 scale), which is separate from
    its battlefield Speed in cells (ADR-037).
- **Pathfinding** (implemented): cheapest route; visible heroes and object bodies block; routes may only
  *end* on an object's entrance (except gates, which continue on the other side); unexplored tiles are
  never used. Every step carries a day index for HoMM-style day markers. Heroes the player can't see
  don't affect the plan; movement stops when it bumps into one.
- **Map objects** (implemented): resource piles (picked up), mines (flagged, daily income), treasure
  chest (choose gold *or* XP), subterranean gate (two-way link). Objects have a footprint and
  are entered only through their entrance tile, usually from below.
  - **Treasure chests roll their reward** (seeded game RNG): 1000/1500/2000 gold *or* 500/1000/1500 XP
    (implemented: the three tiers are equally likely, ADR-039); later a small artifact chance. Map authors may
    fix the values.
  - **Mines may be guarded**: the editor or random map generator can attach a guard stack. The guard must be
    defeated before the first flagging; after that any hero re-flags the mine by visiting it (implemented;
    mine guards always fight).
  - **Neutral monsters** (implemented, ADR-040): a creature stack on a tile; stepping onto it attacks it, and it
    guards the 8 tiles around it. The **guard mode** is a map setting chosen in the editor:
    - **Classic** (HoMM3): entering a guarded tile starts the encounter, but pickups lying on guarded tiles
      can be grabbed from outside without a fight ("stealing").
    - **Area**: the whole 3×3 area is guarded; touching anything inside it, pickups included, starts the fight.
    - Dispositions (HoMM3 names, deterministic): Compliant, Friendly, Aggressive, Hostile, Savage decide whether
      the stack offers to **join** (free, needs room) or tries to **flee** from a much stronger army (≥ 3×), else
      it fights. The player accepts or fights. Unset counts are rolled by tier at game start.
  - **Towns** (implemented, ADR-046, ADR-050): 7×7 with the corners free, entered through the bottom-middle cell
    (`.BBBBB.`, five rows of `BBBBBBB`, `.BBVBB.`). Entering an enemy or neutral town fights its garrison, if any, and
    captures it; an own hero on the entrance is the town's visiting hero.
  Planned: artifacts, map dwellings, shrines. No boats or shipyards: water is an obstacle (ADR-051).
- **Meeting heroes**: moving onto a tile with another hero is an interaction, like visiting an
  object, so routes may **end** on that hero's tile (never pass through it):

  | Other hero | Result | Status |
  |---|---|---|
  | **Own** | Opens the **exchange screen**: swap army stacks and artifacts. The moving hero stays on its adjacent tile. | implemented: stacks move with `MoveStackCommand` (split, merge, swap; a hero keeps one stack), `ExchangePanel` in the game; artifacts in Phase 3 |
  | **Enemy** | Starts a **battle**; the loser (or a hero that retreats) leaves the map | implemented (Phase 2) |
  | **Allied** (team games) | **No interaction for now**: the tile is simply blocked. Exchange with allies may come later. | decided |

  - **Teams**: the map (or template) defines default teams; the game setup screen may change them
    unless the map locks them (story/scenario maps). Free-for-all when nothing is defined.
  - Under refog (implemented): a moving hero **automatically stops** when an enemy hero comes into sight.
    If it still bumps into an unseen enemy, a **battle starts (ambush)**; no ambush bonus for now (decided).
- **Gates and teleporters**: no random-exit portals for now. Every link is fixed in the map, so
  movement and pathfinding stay deterministic and predictable.

  | Kind | Behaviour | Status |
  |---|---|---|
  | **Two-way gate** | Linked pair; visiting either end moves the hero to the other. Subterranean gates connect levels; two-way monoliths work on the same level. | implemented (subterranean gate, two-way monolith) |
  | **One-way gate** | An **entrance** linked to exactly one **exit**. Several entrances may share an exit. The exit is a landing spot only; visiting it does nothing, it can't be entered as a gate, and routes may walk over it. | implemented (one-way monolith) |

  - Pathfinding goes through both kinds: a route continues from the linked end, and one-way gates only in
    their direction. A blocked exit (a hero standing on it) means no teleport.
  - Editor: link tool for both kinds; the validator flags unlinked gates and exits that no entrance uses.
- **Fog of war** (implemented): two modes, **chosen when the game is created** (`GameOptions.FogMode`):
  1. **Permanent reveal** (HoMM3): once explored, terrain, objects and units stay visible.
  2. **Shroud + refog for units**: terrain and objects stay revealed once explored, but other players'
     heroes (later also armies) are only visible on tiles currently **in sight**. Sight has hysteresis:
     a tile comes into sight within an observer's scouting radius, but only drops out once every observer
     is farther than radius + **refog margin** (default 2, configurable). Example: radius 3 + margin 4
     reveals at 3 tiles and refogs at 7.
  - Observers: heroes (scouting radius 5) and owned mines (radius 2).
  - Under refog, **objects out of sight show their last known state** (flag colour, chest still there),
    updated when they come back into sight. Units are hidden entirely. (Implemented: `ObjectMemory`.)

### 4.2 Heroes
- **Roster** (implemented as data, ADR-048): 16 heroes per playable town, a **mage** and a **warrior** for each of its
  eight tier 1–4 creature lines. That line is the hero's **preferred creature**: +2 Attack, +2 Defense and +1 battlefield
  speed for its base and upgraded form. Each hero has a **specialty** (a skill, a spell or a daily resource).
  Edited in `data/heroes.csv`.
- **Primary stats** (implemented, ADR-055): Attack and Defense (added to every stack in battle), Spell Power and
  Knowledge (for magic, Phase 5). Every level raises one of them by 1, HoMM3-style, with odds by class (draft): warriors
  35/45/10/10 % (Attack/Defense/Spell Power/Knowledge) up to level 9, then 30/30/20/20; mages 10/10/40/40, then
  20/20/30/30. The level-up prompt shows the stat with the skill choice. Roster heroes start with 5 points.
- **Skills** (HoMM4-like, implemented, ADR-048): groups of **three** skills; **10 points** learn a group fully (usually
  4/3/3, magic 4/4/2); at most **6 groups** per hero. Levels Basic, Advanced, Expert, Master.

  | Group | Skills |
  |---|---|
  | Offense | Melee (+10 % melee damage per level), Archery (+10/25/50 % ranged), third skill ❓ |
  | Defense | Armor (−5 % damage taken per level), Spell resistance, third skill ❓ |
  | Estates | Treasury (+100/200/350/500 gold a day), Prospecting (other resources), Stewardship (cheaper costs in the hero's town) |
  | Pathfinding | Logistics (+10 % movement per level), Pathfinding (−25/50/75 % terrain penalty), Troop management (+1 army map speed per level) |
  | Tactics | Tactics (battlefield width), Deployment (rearrange troops before battle), Drill (+1 battlefield speed per level) |
  | 8 magic schools | Magic (spell levels 1–4), Attunement (mana, regeneration, cost or potency), Mastery (spell upgrades) |
- **Levels** (implemented): HoMM3 experience table; each level gives one skill point, spent on one of **three offered
  skills**. Mages are offered their own school more often, warriors the general groups; a rival town's school only in
  5 % of level-ups.
- Army of up to 7 creature stacks (implemented). A hero without creatures cannot attack.
- Artifacts with paper-doll slots and backpack.
- Spell book, mana.

### 4.3 Towns and economy
- **Six resources**: gold, lumber, ore, crystal, mercury, sulfur (implemented as `ResourceSet`).
- One building per town per day (implemented, ADR-050); the building list is one table, `data/buildings.csv`, shared
  by all towns for now. New towns start with town hall 1, fort 1 and tavern 1; pre-built buildings keep the
  requirements. The town screen is 2D in the first iteration, in the 3D game too (ADR-050).
- **Town turn** (implemented, HoMM3-style): at any time on the owner's turn, build, recruit into the garrison or the
  visiting hero, upgrade stacks for the price difference, and move stacks between garrison and visiting hero.
- **Creature tech tree** (accepted; details ❓ below):
  - Creatures have **5 tiers** (not HoMM3's 7).
  - Each playable town offers **9 creature dwellings**: **2 alternatives at each of tiers 1–4** and
    **1 at tier 5**.
  - A town may build **7 of the 9**, with **at least one dwelling per tier**. Since tier 5 has only one,
    every town ends up with the tier-5 dwelling, and **both** alternatives at two of tiers 1–4 but
    **one** at the other two. Example: T1 a+b, T2 a, T3 a+b, T4 b, T5. These **7 creature types** fill
    exactly the hero's 7 army slots.
  - **All creatures of playable towns are upgradeable** (per dwelling).
  - **Neutral creatures** (and the non-playable Conflux / Dragon Utopia) **may** lack upgrades.

  | Tier | Dwellings offered | Must build | Can build |
  |---|---|---|---|
  | 1–4 | 2 alternatives each | ≥ 1 | 1 or 2 |
  | 5 | 1 | 1 | 1 |
  | **Total** | **9** | **5** | **7** |

  - **The choice is final, per town.** Once 7 are built, the remaining 2 are locked forever in that
    town. Dwellings can't be demolished. Another town of the same type may choose differently.
  - **Upgrades: one per dwelling** (HoMM3-style). Each dwelling has a single upgrade building; an
    upgraded dwelling produces upgraded creatures, and existing stacks can be upgraded in that town for
    the price difference. The upgrade does not raise growth; growth bonuses come from other buildings ❓
    (ADR-046).
  - **Buildings** (draft, ADR-046): town hall (3 levels, 500/1000/2000 gold a day), fort (3), blacksmith,
    marketplace (2; level 2 adds 1 ore and 1 lumber a day), mage guild (4, ADR-047), tavern, and the dwellings
    (build 500 × tier gold, upgrade 1000 × tier, plus lumber, ore, mercury and crystal by tier).
  - **Locked-out creatures are still obtainable** from external dwellings on the adventure map and
    from other towns of the same type that built them. The town choice shapes your core army; it is
    not a hard ban.
  - **Neutral creatures use the same 1–5 tier scale**, which is used for guard strength, treasure value
    and random-map balancing. Some have upgrades, most don't.
- Weekly creature growth (implemented): a new dwelling opens with one week of creatures, every Monday adds the
  creature's `growth`, and unbought creatures accumulate. Dwellings on the map later.
- **Caravans** (designed, ADR-054; needs map dwellings): a town building that hires creatures from owned dwellings on
  the adventure map. The creatures travel virtually to the town at their map speed, along a route no enemy hero or
  monster blocks, and a dwelling more than 5 days away cannot supply the town; both are checked before paying.
  Caravans also move troops between two own towns.
- **Income** is paid at the start of the owner's turn. **There is no base or placeholder income**:
  players earn only what their sources produce. Sources:
  - **Mines** (implemented): gold 1000, lumber/ore 2, rare resources 1.
  - **Towns** (implemented): town hall 500 / 1000 / 2000 gold; marketplace level 2 adds 1 ore and 1 lumber (ADR-046).
  - **Hero secondary skills** (implemented): Treasury and Prospecting (Estates group).
  - **Hero specialisations** (implemented): resource specialties pay +N of a resource per day (`HeroIncome`).
  - **Artifacts**, e.g. resource-producing items (with artifacts, Phase 3).

  Each source is a pluggable `IIncomeSource`, and `EconomyService.DailyIncome` sums them.

### 4.4 Factions and towns
Towns are organised into four groups (names are working titles ❓). **Groups matter only in lore and
the campaign**: story, scenario setup, campaign alliances and scripted events. In standard and random-map
games they have **no rule effect**. There are no automatic alliances, shared skills, diplomacy or
group-based bonuses; teams are set per game, and native terrain belongs to individual towns.
Templates and the random map generator may still use groups as a *filter* for which towns appear,
because that is content selection, not a rule.

| Group | Towns | Playable |
|---|---|---|
| 1 · Light | **Haven** (humans), **Sylvan** (elves), **Academy** (mages) | yes |
| 2 · Darkness | **Necropolis** (undead), **Inferno** (daemons), **Dungeon** (underground dwellers / dark elves) | yes |
| 3 · Wild | **Stronghold** (orcs), **Swamp Bestiary** | yes |
| 4 · Primordial | **Conflux** (elementals), **Dragon Utopia** (all dragons except bone dragons) | **no**: not balanced for PvP; campaign and neutral (map guardians, dwellings) only |

- 8 playable towns + 2 special towns. Each playable town has 9 creature lines over 5 tiers (2+2+2+2+1),
  all upgradeable; a town builds 7 of them (§4.3).
- Bone dragons belong to Necropolis, not Dragon Utopia.
- **Rivalries are lore only.** Opposition between towns or groups (the rivalry ring in
  [lore.md](lore.md): Haven vs Necropolis & Inferno, Academy vs Inferno & Dungeon, Sylvan vs Dungeon &
  Necropolis) exists
  in story and campaign, but has **no combat or rules effect**. There are no hate/enmity damage bonuses
  (unlike HoMM3 Angels vs Devils) and no alignment modifiers. Creature capabilities target *properties*
  (undead, flying, size, …), never town or group membership. The one exception outside combat: a rival town's magic
  school is rarely offered at level-up (ADR-048).
- **Vertical slice: Haven and Inferno** (decided). The holy-vs-demonic contrast reads instantly in a dark,
  realistic art style; the rivalry is story only.

### 4.5 Combat
- **Separate battlefield** (HoMM3-style cut to an arena themed by the terrain where the battle starts). Implemented in
  the game app as a 3D battle scene with placeholder meshes on that terrain's texture (ADR-052): hover previews the
  move, attack side or shot, AI turns play as animations, a timeline shows the initiative order, and a result screen
  ends the battle.
- **Square grid** with **multi-cell units**. Every creature stack has a footprint:
  | Size | Footprint | Typical creatures |
  |---|---|---|
  | Small | 2×2 cells | infantry, archers, imps |
  | Medium | 3×3 cells | cavalry, griffins, golems |
  | Large | 4×4 cells | dragons, behemoths, titans |
  - The fine cell grid lets units of different sizes share one board. Positions refer to the top-left
    cell of the footprint.
  - **Battlefield**: **24 cells tall**, **30 cells wide** by default. 8-way movement, one cell per step up to
    the creature's Speed; a diagonal step needs one of the two orthogonal placements free (no squeezing
    through corners); flyers land on any free placement within their Speed (decided, ADR-038).
    - **Tactics lets its owner choose the width before the battle**:

      | Effective Tactics | Width choices |
      |---|---|
      | none | 30 |
      | Basic | 30, 32 |
      | Advanced | 28, 30, 32 |
      | Expert | 28, 30, 32, 34 |

    - **The enemy hero's Tactics reduces yours**: effective level = your level − enemy level (minimum
      none). At most one side can have an effective level above none, and that side chooses.
    - **36 wide is reserved for town sieges.**
  - **Surround / flanking**: a unit's perimeter grows with its size, so more attackers fit around a
    large unit. **No generic flanking bonus**: damage is the same from any side, however many units
    surround the target. **No facing.** Engagement is a hook for **special abilities**: some creatures
    get bonuses when the target is already engaged by another enemy, per extra side, per number of times
    the target was hit this round, and so on. The core tracks engagement and hit history so abilities can
    use them.
  - **Body blocking & cover**: footprints block movement. Every **unit (friend or foe) or obstacle** that the
    line of fire passes through between shooter and target adds a cover penalty. Each blocker counts **once**,
    however many of its cells the line crosses:

    | Blocker | Penalty |
    |---|---|
    | 2×2 or 3×3 creature | 1/8 |
    | 4×4 creature | 1/4 |
    | Small obstacle | 1/8 |
    | Big obstacle | 1/4 |
    | City wall (sieges) | 1/2 |

    - **Penalties add up.** Ranged damage × (1 − total penalty).
    - **If the total exceeds 7/8, the target cannot be shot at all** (it is out of line of fire).
      Example: a wall (4/8) + a 4×4 creature (2/8) = 6/8, so the shot deals 25 % damage. Add a small obstacle
      (1/8) and it is still allowed at 7/8 (12.5 % damage). One more blocker and the shot is impossible.
    - Each obstacle definition declares its size class (small/big). Walls are a separate class.
    - Line of fire: supercover of the segment from footprint centre to footprint centre; cells touched only at
      a corner count too (decided). No skill reduces cover penalties yet.
  - **Damage formula: HoMM3 baseline.** Base damage (rolled per stack within the creature's min–max) ×
    stack size; +5 % per point of Attack above the target's Defense (cap +300 %); −2.5 % per point of
    Defense above Attack (cap −70 %); then multiplicative modifiers (skills, cover, abilities, spells).
    Rolls use the seeded battle RNG.
  - Size alone does not grant reach, cleave or trample/push.
  - **Area attacks are creature capabilities**, independent of size:
    - **Breath** (dragon- or phoenix-like): also hits the cells directly behind the target in the
      line of attack.
    - **Cleave / multi-head** (cerberus- or hydra-like): also hits enemies adjacent to the attacker
      (cerberus: up to 3 in front, hydra: all around).
    - **HoMM3-style friendly fire**: breath also hits friendly units in its line; cleave/multi-head
      hits enemies only. Only the primary target retaliates.
    - A unit whose footprint touches the area is hit once (decided). Breath's area is a target-sized block
      directly behind the target; cleave takes up to two extra enemies next to both attacker and target.
  - Open ❓: how teleporting creatures use footprints.
- **Turn order: initiative decides order, not frequency.** Each round every stack acts **exactly
  once**, in descending **Initiative** (a stat separate from **Speed**, which is movement range).
  A HoMM5-style timeline bar shows the order. Unlike HoMM5, no unit acts twice in a round.
  - **Ties**: attacker's side first, then higher speed, then a deterministic seeded roll.
  - **Wait** (HoMM3): waiting units act at the end of the round, in **reverse** initiative order.
  - Open ❓: spells that change initiative mid-round.
- **Morale and luck** (implemented, ADR-053; rates are drafts): each stack has −5…+5 of each, its creature's plus its
  hero's.
  - **Morale**, rolled every round: 2 % per point to act twice (good) or lose the turn (bad); otherwise 4 % per point
    for ±50 % initiative that round. The extra action is the one exception to "once per round".
  - **Luck**, rolled on every hit by attacker and defender, 4 % per point: the attacker's good luck (or the defender's
    bad luck) deals +75 % damage, the defender's good luck (or the attacker's bad luck) −50 %; opposite rolls cancel.
  - Which skills, artifacts and army mixes change them is still open ❓.
- Wait/defend (+20 % Defense), retaliation (once per round unless a capability says otherwise), ranged units
  (no shot while engaged, half damage beyond 15 cells), flyers, rolled obstacles. Implemented (ADR-038).
- **Creature capabilities** (HoMM4-style, implemented, ADR-042): each creature has **at most 5** special rules,
  shown as its capability list. Every rule counts, flying and area attacks included.

  | Capability | Effect |
  |---|---|
  | Flying | Lands on any free spot within its speed |
  | Strikes twice / Shoots twice | A second blow or shot after the retaliation |
  | First strike | Retaliates before the attacker's blow (normal order if both have it) |
  | Ranged retaliation | A shooter that is shot at fires back if it can shoot the attacker |
  | Unlimited retaliation / No enemy retaliation | Retaliates every time / provokes no retaliation (melee and ranged) |
  | Melee penalty | Shooter deals 50 % in melee; shooters without it fight at full strength |
  | Breath, cleave, multi-head | Area attacks (see above) |
  | Pack hunter (x %), relentless (x %) | Engagement and hit-history damage bonuses |
  | Spell immunity (tag) | Unaffected by spells with that tag (all, mind, fire, curse, …); takes effect with magic |
  | Spell resistance (level n) | Unaffected by spells of level n and below; a spell upgraded twice counts as one level higher; takes effect with magic |
  | Caster (spell, n) | Casts the spell n times per battle; takes effect with magic |
- **Retreat**: a hero may leave the battle; its army is lost and it leaves the map. Surrender (paying gold)
  comes with taverns in Phase 3.
- Hero casts one spell per round; catapult, ballista, first-aid tent as war machines ❓.
- Quick combat: either side may hand the rest of the battle to the deterministic battle AI (implemented).
  Neutrals and computer players always use it.
- Siege battles with walls and towers (post-1.0 ❓).

### 4.6 Magic
- **Eight schools**, one per playable town (working titles: Light, Nature, Arcana, Death, Chaos, Shadow, Shamanism,
  Primal), each also a hero skill group (ADR-047). Any hero may learn any school.
- **Four spell levels** and a **four-level mage guild**; every spell has three levels of its own (upgraded twice).
- A school's skills: magic (4 levels, unlocking spell levels), attunement (4 levels: mana pool and regeneration, spell
  cost or potency by school), mastery (2 levels: spell upgrades).

### 4.7 Game modes (priority order)
1. Single scenario vs. AI.
2. Hot-seat multiplayer (turn structure already models it).
3. Story campaign: chained scenarios, carry-over heroes, scripted map events and dialogue. This needs
   editor support for triggers and a script/event format.
- Online multiplayer: not planned (core determinism keeps the door open).

### 4.8 Tools
- **Map editor**: a **separate standalone app** (not an in-game mode). Implemented in core with
  undo/redo: terrain painting (brush radius, drag strokes), object placement/removal, gate linking,
  mine owners, hero placement, armies, monsters, towns, player slots, map name/description, validation, all with
  Godot UI (tool palette, inspector, properties, players, validation panel). Later: artifacts, victory/loss conditions, events/triggers, map resize, and the **template editor**
  plus "generate map from template" (§4.9).
- **2D harness**: two parts:
  - **Viewer** (Godot 2D app): a top-down debug front-end over the same core, for rule development and
    visual debugging of AI.
  - **Headless simulator** (plain .NET console, no Godot): runs scripted scenarios, AI-vs-AI games and
    random-map batch generation/validation. It is fast enough for CI and for tuning balance and AI over
    thousands of games.

### 4.9 Random map generation (templates)
Modelled on the Heroes 3: Horn of the Abyss template system.

- **Both the game and the editor generate maps.** The game's new-game screen picks a template from the
  available ones, a map size, players, a seed and game options, then generates and starts. The editor
  generates into an **editable** map, so designers can hand-polish a generated map and save it.
- **Templates are authored in the editor** (template editor: a zone graph) and saved as
  `*.ewtemplate.json`. The game lists every template it can find, both built-in and user/mod folders.
- **A template** defines:
  - Supported map sizes, player-count range, whether an underground level is used, and human/AI
    restrictions.
  - **Zones** (map areas), each with:
    - Type: player start, AI-only start, treasure, junction.
    - Owner slot for start zones.
    - Relative size.
    - Level: surface or underground.
    - **Towns**: player towns and neutral towns (min/max), allowed town groups or "same as zone owner".
    - **Resource richness**: minimum mines per resource, extra resource piles.
    - **Treasure**: value bands with density (e.g. 500–3000 ×9, 3000–6000 ×6, 10000–15000 ×1).
    - **Monster strength**: none/weak/average/strong, plus allowed terrain types.
  - **Connections** between zones: which zones connect, how strongly the passage is guarded,
    and the passage type: normal, wide (unguarded), border guard/gate, or portal/subterranean
    gate across levels. A connection may also be optional or randomly present.
- **Generation is deterministic** from (template, size, players, seed), so a map can be shared by its seed.
- **Pipeline**:
  1. Place zones on the grid from the connection graph.
  2. Paint terrain per zone.
  3. Raise borders and obstacles.
  4. Cut connections and place their guards.
  5. Place towns.
  6. Place mines and resources.
  7. Place treasure by value band.
  8. Place neutral monsters.
  9. Validate: every start zone can reach the others, and start zones are fair against each other.
- **In scope for 1.0**:
  - **Mirrored/symmetric generation**: a template can mark zones as mirrored, so competitive maps are
    generated symmetrically (point or axis symmetry) for fairness.
  - **Saving generated maps**: a generated map can be saved as an ordinary map file, as well as
    reproduced from its seed.
- **Not in 1.0**: water zones/islands in templates, and importing HotA templates. No boats at all (ADR-051).
- Open ❓: how army speed and native-terrain rules feed into treasure/guard values.

## 5. Presentation

- **Art direction**: **grim, dark fantasy** with **realistic lighting and PBR textures**, plus nods
  to the original game (recognisable silhouettes for archetypal creatures and buildings, heraldic
  player colours, iconic map objects).
  - Lighting: physically based, low sun angles, volumetric fog, strong shadows. Day/night tint per
    in-game day ❓. Godot Forward+ with SDFGI/VoxelGI or baked lightmaps, to be decided by a perf spike.
  - Palette: desaturated earth tones. Player colours and interactive objects stay saturated so the
    map remains readable in a dark scene (readability pillar).
  - Materials: PBR (albedo/normal/roughness), weathered and worn.
  - **Terrain: smooth heightmapped landscape** (texture splatting, triplanar on slopes, blended
    biome transitions). The logical square grid stays invisible and appears as an overlay only for
    hover, path preview, movement range and in the editor. Walkability and costs still come from
    core tile data, so visual height must not contradict them (cliffs = Rock tiles, etc.).
  - Post-processing: subtle colour grading, bloom on magic only, vignette.
  - Placeholder until then: coloured tiles and capsules.
- **Camera**: classic strategy camera, tilted **15–30° from vertical** (more tilted when zoomed in,
  more top-down when zoomed out), **rotatable ±45°** around the vertical axis, zoom in/out, pan.
- **Terrain rendering**: start with per-tile meshes/colours; later blended terrain shader with
  heightmap and transitions.
- **UI**: HoMM-style right-side panel (minimap, hero/town list, end turn) and bottom status bar.
- **Audio**: per-terrain ambient music, faction town themes.

## 6. Platforms and scope
- **Platforms for 1.0: Windows and Linux / Steam Deck** (controller-friendly UI and Deck performance
  budget to test). macOS later.
- **Player modding is not a 1.0 requirement.** Content stays data-driven, with versioned JSON formats,
  because that helps us. A supported mod loader and mod folder may come after 1.0.

## 7. Non-goals (for now)
- Real-time elements, online multiplayer, procedural campaign, mobile touch UI, official mod support.

## 8. Deliberate departures from HoMM3

| # | Area | HoMM3 | Eternal Wars | Status |
|---|---|---|---|---|
| 1 | Combat grid | 15×11 hexes; units occupy 1 or 2 hexes | Square cell grid; units occupy 2×2 / 3×3 / 4×4 | accepted |
| 2 | Tone | Bright high fantasy | Dark, grim fantasy, realistic lighting | accepted |
| 3 | Turn order | Speed decides order | Separate Initiative stat decides order (HoMM5 timeline UI); one action per round | accepted |
| 4 | Unit size | Mostly cosmetic (1 vs 2 hexes) | Size drives flanking exposure, body blocking and ranged cover | accepted |
| 5 | Towns | 9 towns, all playable | 8 playable in 3 groups + 2 non-playable "primordial" towns for campaign/neutrals | accepted |
| 6 | Terrain | Painted tiles | Smooth 3D terrain with on-demand grid overlay | accepted |
| 7 | Resources | 7 (incl. gems) | 6: gold, lumber, ore, crystal, mercury, sulfur | accepted |
| 8 | Fog of war | Permanent reveal only | Choice per game: permanent reveal, or shroud + unit refog with hysteresis | accepted |
| 9 | Map scale | Up to 144×144 | Presets up to 256×256, 8 players | accepted |
| 10 | Random maps | Fixed templates (HotA adds a template editor) | Template editor in our map editor; game and editor both generate | accepted |
| 11 | Terrain penalties | One native terrain per town; grass, dirt and roads free for everyone | Every terrain penalises non-natives (125–200); towns may have several native terrains; only roads, Pathfinding and artifacts help | accepted |
| 12 | Creature tiers | 7 tiers, 1 dwelling per tier, all buildable | 5 tiers, 9 dwellings, build 7 of 9 (final per town) | accepted |
| 13 | Tactics | Larger deployment zone | Choose battlefield width (28–34) before battle; enemy Tactics reduces yours | accepted |
| 14 | Ranged | Range and wall penalties only | Cumulative cover from units (1/8, 4×4: 1/4), obstacles (1/8, 1/4) and walls (1/2); above 7/8 no shot | accepted |
| 15 | Fog ambush | — | Under refog, bumping into an unseen enemy starts an ambush battle | accepted |
| 16 | Neutral guards | Zone of control (pickups can be stolen) | Map setting: classic zone of control, or a fully guarded 3×3 area | accepted |
| 17 | Speed | One speed for battle and map | Battle Speed in cells, separate adventure map speed | accepted |
| 18 | Morale | Extra turn or freeze only | Also ±50 % initiative for the round when no extra action / freeze triggers | accepted |
| 19 | Luck | Attacker only, double damage | Both sides roll: +75 % (attacker) or −50 % (defender); opposite rolls cancel | accepted |
| 20 | External dwellings | Visit them with a hero | Caravans hire from owned dwellings within 5 days; creatures travel the map to the town | accepted (not built yet) |
| … | | | *more twists to be proposed* ❓ | |
