# Combat

> Part of the [game design](overview.md). Everything here is built unless tagged *(planned)*; numbers are drafts. Open items ❓:
> [open-questions.md](../project/open-questions.md).

## Battlefield
- **Separate battlefield**, an arena themed by the terrain where the battle starts (ADR-052): 3D scene with move / attack / shot previews,
  animated AI turns, an initiative timeline and a result screen.
- **Square grid, 30 wide × 24 tall** by default (ADR-021, ADR-033); 36 wide in sieges.
- **Multi-cell units** (ADR-005, ADR-037); positions refer to the footprint's top-left cell:

  | Size | Footprint | Typical creatures |
  |---|---|---|
  | Small | 2×2 | infantry, archers, imps |
  | Medium | 3×3 | cavalry, griffins, golems |
  | Large | 4×4 | mostly dragons |

  **Size policy** (ADR-070): about 95 % Small or Medium; Large only for the five Dragon Utopia dragons, the Bone and Ghost Dragon and the
  Behemoth line (9 of 182; 80 Small, 93 Medium). `sim towns` keeps a similar spread.
- **Movement is a circle** (ADR-089, CMB-9): a stack reaches any placement whose path, at any angle, is at most **Speed + 0.25 cells**; around
  obstacles the way round. A diagonal step needs one of the two orthogonal placements free. Flyers and teleporters land on any free placement
  within the same straight-line distance (ADR-038).
- **Tactics** (ADR-069, ADR-072, HERO-4):
  - **Width**: effective level = own Tactics − enemy hero's Tactics (minimum none); only a side with an effective level chooses; the
    computer keeps 30. None / Basic: 30; Advanced: 30, 32; Expert: 28, 30, 32, 34. Sieges have a fixed width.
  - **Deployment**: the side with the higher Tactics (a tie: nobody) places its troops within its first **4 / 5 / 6 columns** (Basic /
    Advanced / Expert), the machines' column included, then presses "Start battle"; others are placed at the edge. Against heroes and
    neutral monsters, not in creature banks.

## Engagement and blocking
- **No facing, no generic flanking bonus**: damage is the same from any side. A unit's perimeter grows with size, so more attackers fit around
  a big one. The core tracks engagement and hit history for **abilities** (bonuses when the target is engaged by another enemy, per extra
  side, per hit this round, …). Size alone grants no reach, cleave or trample.
- **Body blocking and cover** (ADR-035): footprints block movement. Each **unit (friend or foe) or obstacle** the line of fire passes through
  counts **once**:

  | Blocker | Penalty |
  |---|---|
  | 2×2 or 3×3 creature, small obstacle | 1/8 |
  | 4×4 creature, big obstacle | 1/4 |
  | City wall (sieges) | 1/2 |

  Obstacle definitions declare small / big; walls are a class of their own. Line of fire: supercover of the segment between footprint
  centres; cells touched only at a corner count (ADR-038).
- **Distance penalty** (ADR-070, CMB-6): straight-line cells between footprints (8 across and 8 down = 11.3): **0–9 none, 10–19 1/8, 20–29
  2/8**, and so on. War machines ignore it.
- **Ranged penalty** (ADR-072): 1. add distance and cover, 2. subtract **Marksmanship** (−2/8 / −4/8 / −6/8), 3. **above 7/8 the target
  cannot be shot**. A shot deals (8 − penalty)/8 (`BattleGrid.RangedPenaltyEighths`). Example: wall 4/8 + 4×4 creature 2/8 + small obstacle
  1/8 = 7/8, still allowed (12.5 %); one more blocker and the shot is impossible.

## Damage
**HoMM3 baseline** (ADR-033): base damage (rolled per stack in the creature's min–max) × stack size; +5 % per Attack point above the target's
Defense (cap +300 %), −2.5 % per Defense point above Attack (cap −70 %); then multiplicative modifiers (skills, cover, abilities, spells). Rolls
use the seeded battle RNG.

**Native terrain** (ADR-068): a stack whose town lists the battlefield terrain as native (`TerrainDefinition.NativeTowns`) gets **+1 Attack,
Defense and Speed**; each stack counts on its own; neutrals and war machines have no native terrain. The battlefield is the terrain where
the battle starts, or the object's own (abandoned gold and ore mines fight underground).

## Area attacks
Creature capabilities, independent of size:
- **Breath**: also hits a target-sized block directly behind the target, friendly units included. **Cleave**: the target plus up to 2 enemies
  adjacent to both. **Multi-head**: every adjacent enemy. Only the primary target retaliates.
- A unit whose footprint touches the area is hit once (ADR-038). **Area spells** have a fixed area whatever the target's size; a big creature
  with any cell inside is hit once (ADR-072, MAG-11).

## Turn order
- **Initiative decides order, not frequency** (ADR-009): each round every stack acts **once**, in descending Initiative (separate from
  Speed = movement range), shown on a timeline. The only extra action is good morale.
- **Ties**: attacker's side, then higher speed, then a seeded roll.
- **Wait**: waiting units act at the end of the round in **reverse** initiative order.
- Spells that change initiative do not change when the hero may cast; their other effects on the order are open ❓.

## Morale and luck
ADR-053; rates are drafts. Each stack has −5 … +5 of each: creature + hero (and its artifacts) + army.
- **Morale**, rolled every round: 2 % per point to act twice (good) or lose the turn (bad); otherwise 4 % per point for ±50 % initiative that
  round. **No morale**: war machines, undead, constructs and elementals, whatever the hero or a warcry gives (ADR-071, CMB-7, CMB-8).
- **Luck**, rolled on every hit by both sides, 4 % per point: attacker's good (or defender's bad) luck +75 % damage, defender's good (or
  attacker's bad) −50 %; opposite rolls cancel. War machines roll their hero's luck.
- **Army morale** (ADR-075, CMB-2), set at battle start: **mixed factions** −1 per faction beyond the first (neutrals one faction, machines
  none; heroes' armies and garrisons, not neutral guards or banks); **Brotherhood** +1 for a town's defenders (garrison or an own hero at the
  entrance) when it has the upgraded tavern; Leadership, warcries, artifacts and map buffs.

## Actions
ADR-038.
- **Defend**: +20 % Defense (at least +1) until the unit's next turn. **Wait**: see turn order.
- **Retaliation**: once per round unless a capability says otherwise.
- **Ranged units**: no shot while engaged; distance and cover lower the damage (above).
- The hero's casting: [magic.md](magic.md#casting-in-battle).

<a id="creature-capabilities-homm4-style-implemented-adr-042"></a>
## Creature capabilities
HoMM4-style (ADR-042): **at most 5** special rules per creature, every rule counted (flying and area attacks included).

| Capability | Effect |
|---|---|
| Flying | Lands on any free spot within its speed |
| Teleporting | Moves like a flyer; the footprint must fit at the destination (CMB-4, ADR-083) |
| Strikes twice / Shoots twice | A second blow or shot after the retaliation |
| First strike | Retaliates before the attacker's blow (normal order if both have it) |
| Ranged retaliation | A shooter that is shot at fires back if it can |
| Unlimited retaliation / No enemy retaliation | Retaliates every time / provokes none (melee and ranged) |
| Melee penalty | Shooter deals 50 % in melee; shooters without it fight at full strength |
| Breath, cleave, multi-head | Area attacks (above) |
| Pack hunter (x %), relentless (x %) | Engagement and hit-history bonuses |
| Spell immunity (tag) | Unaffected by spells with that tag (all, mind, fire, curse, …) |
| Spell resistance (level n) | Unaffected by hostile spells of level n and below; a rank III spell counts one level higher |
| Caster (spell, n) | Casts the spell n times per battle ([magic.md](magic.md#casting-in-battle)) |

## Leaving a battle
- **Retreat**: the army is lost and the hero leaves the map; it waits in its player's taverns ([tavern](towns-and-economy.md#tavern)).
- **Surrender** (CMB-1, ADR-081): pay **half the gold cost of the surviving creatures** (rounded up, to the enemy player) and leave with the
  army. Not against neutrals. The winner gets no hero bonus and no loot.
- **Necromancy** after a victory, before any level-up: [magic.md](magic.md); not when the enemy retreated or surrendered.
- The AI **retreats** when its army is worth 20 % of the enemy's or less (tuning, ADR-081); it never surrenders.

<a id="war-machines-implemented-adr-056-adr-063"></a>
## War machines
ADR-056, ADR-063, ADR-121.
- Every hero owns its town's **offensive machine** (ballista, cannon, catapult or glaivethrower); a **healing tent** and **ammo cart** are
  bought at a blacksmith or the War Machine Factory. Machines never move or melee and are lost when destroyed.
- The offensive machine fires on its turn, outside sieges only from Advanced Ballistics ([skills.md](../content/skills.md#war-machines-machines));
  the tent heals every own stack a little each round; the cart adds shots (unlimited with Advanced Ammo supply). The four offensive machines
  are tuned to about the same damage per initiative (ADR-121).
- **Placement** (ADR-073): only the outermost column of their side, offensive machine and tent **1 × 3**, cart **1 × 2** (tent top, machine
  middle, cart bottom); stacks start in the next column; models overhang the grid edge.
- **Exchange**: a machine moves to the other hero when it has none in that slot; an offensive machine becomes the receiver's town's kind
  (ADR-083). A town holds machines only through a garrison hero.
- Spells may target machines; they are `mechanical` and take only damage spells (CMB-3, MAG-4).

<a id="sieges-adr-099-adr-106-built-adr-109"></a>
## Sieges
ADR-099, ADR-106, built ADR-109 and ADR-120 (CMB-11). Numbers are placeholders; *(assumed)* marks choices made while building.
- **When**: every battle at a town with a **Fort** (hero inside or only the garrison) is a siege on the **36-wide** field, fixed width,
  normal deployment. "Inside" = a hero at its own town's entrance, or a [garrison hero](towns-and-economy.md#garrison-heroes). No Fort: an
  ordinary battle. Map garrisons are not besieged.
- **Fortifications** by fort level (ADR-066): Fort: 4 wall sections and the gate; Citadel: + keep and moat; Castle: + two towers and
  stronger walls.
- **Walls**: a section has **2** hit points (Fort, Citadel) or **3** (Castle); a destroyed one is a gap. Attackers' shots across a wall take
  the 1/2 cover, once per shot *(assumed: defenders shoot out freely)*.
- **Layout** *(assumed)*: wall line at column 24 of 36 (24 columns attackers, 11 behind); four sections of 5 rows, the gate 4 rows in the
  middle; the moat is the 2 columns in front, bridged at the gate. Keep (behind the defenders' edge) and towers (above and below the wall
  line) are **off the grid**: they block nothing and only the siege engine reaches them. Random obstacles stay in front of the moat.
- **Gate**: defenders pass, attackers break it. **100** hit points, **150** from the Citadel *(assumed)*, **10 Defense**. A machine's hit
  takes all of it; an attacking stack next to it strikes it like Defense 10, **capped at 50** a blow.
- **Who breaks walls**: no free siege catapult; the attacker's own **offensive machine** always joins and fires at the fortifications (the
  catapult is just Stronghold's and Swamp Bestiary's machine). Without Ballistics it fires by itself, only at fortifications, 2 hits in 3,
  1–2 hit points a hit; Basic gives control. By itself it aims at wall sections, at the gate once all four sections are down, then keep and
  towers *(order assumed)*. Without a machine only the troops break the gate. **Earthquake** takes 1 hit point off each of 2 sections.
- **Keep and towers**: each shoots **once a round**, as the round starts *(assumed)*, at the attacking stack worth most to hit: flat **10 + 2
  per town building** (every built level of every building and dwelling *(assumed)*), the keep double. **3** hit points each.
- **Moat** (Citadel): a stack entering it (walking or landing) stops and takes **2 × (10 + 2 per town building)**, once per entry; attackers
  only *(assumed)*; it moves on freely from there.
- **Flyers and teleporters** cross the walls. Defenders' shooters behind the walls get the 1/2 cover. From the Citadel the defenders'
  strongest shooter starts **in the keep** *(proposal, ADR-120)*: never moves, no melee against it, shots at it take wall cover; it leaves
  when the keep falls.
- **AI**: attacks a fortified town only when a siege estimate wins; defenders stay behind the walls; walking attackers go round to a breach
  or the broken gate and, while walls hold, close up to the moat without entering; with no machine and no breach they break the gate.

## Auto and quick combat
- **Auto combat** (ADR-088, ADR-090), per human player in the game menu: off, ask before each battle, or always; for battles against
  computer players and neutrals. The battle AI plays a copy for both sides and shows the outcome (casualties, experience, raised undead,
  artifacts). **Accept** ends the battle exactly so; **Fight** plays it by hand from where it stands, re-rolling nothing.
- **Quick combat**: either side may hand the rest of a battle to the deterministic battle AI; neutrals and computer players always use it.
- Health bars per stack and field-dividing cliffs *(planned, ADR-118, CMB-12)*.
