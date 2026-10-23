# Combat

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../project/open-questions.md).

## Battlefield
- **Separate battlefield** (HoMM3-style cut to an arena themed by the terrain where the battle starts). Implemented in
  the game app as a 3D battle scene with placeholder meshes on that terrain's texture (ADR-052): hover previews the
  move, attack side or shot, AI turns play as animations, a timeline shows the initiative order, and a result screen
  ends the battle. Not yet run in Godot on this machine.
- **Square grid** with **multi-cell units**. Every creature stack has a footprint (ADR-005, ADR-037):

  | Size | Footprint | Typical creatures |
  |---|---|---|
  | Small | 2×2 cells | infantry, archers, imps |
  | Medium | 3×3 cells | cavalry, griffins, golems |
  | Large | 4×4 cells | mostly dragons |

  The fine cell grid lets units of different sizes share one board. Positions refer to the top-left cell of the
  footprint.
- **Size policy** (ADR-070, applied 2026-10-23): about **95 %** of creatures are Small or Medium; Large is kept mostly for
  dragons. 9 of 182 are Large: the five Dragon Utopia dragons, the Bone and Ghost Dragon and the Behemoth line; the 33
  others that were Large are Medium (80 Small, 93 Medium). `sim towns` keeps a similar spread.
- **Size**: **24 cells tall**, **30 cells wide** by default (ADR-021, ADR-033). 8-way movement, one cell per step up to
  the creature's Speed; a diagonal step needs one of the two orthogonal placements free (no squeezing through
  corners); flyers land on any free placement within their Speed (ADR-038).
- **Tactics lets its owner choose the width before the battle**:

  | Effective Tactics | Width choices |
  |---|---|
  | none | 30 |
  | Basic | 30, 32 |
  | Advanced | 28, 30, 32 |
  | Expert | 28, 30, 32, 34 |

  - **The enemy hero's Tactics reduces yours**: effective level = your level − enemy level (minimum none). At most one
    side can have an effective level above none, and that side chooses. A computer player keeps 30.
  - **36 wide is reserved for town sieges** (sieges are post-1.0 ❓).
  - **ADR-069 (not built)**: Tactics also **deploys** troops before the battle and chooses the width only from Advanced: Advanced
    30 or 32, Expert 28, 30, 32 or 34.
  - **Deployment** (ADR-072, HERO-4; built, ADR-076): the player may place the troops within the first **4 / 5 / 6 columns** (Basic /
    Advanced / Expert) from their own edge. Only the side with the higher Tactics deploys (a tie: nobody). The player drags stacks
    in the battle scene, then presses "Start battle"; computer players, and every side that does not deploy, are placed
    automatically at the very edge. Against heroes and neutral monsters, not in creature banks. The war machines keep the
    outermost column (ADR-073, below); the zone includes it, and stacks may use its free cells (confirmed, ADR-075).

## Engagement and blocking
- **Surround / flanking**: a unit's perimeter grows with its size, so more attackers fit around a large unit. **No
  generic flanking bonus**: damage is the same from any side, however many units surround the target. **No facing.**
  Engagement is a hook for **special abilities**: some creatures get bonuses when the target is already engaged by
  another enemy, per extra side, per number of times the target was hit this round, and so on. The core tracks
  engagement and hit history so abilities can use them.
- Size alone does not grant reach, cleave or trample/push.
- **Body blocking and cover** (ADR-035): footprints block movement. Every **unit (friend or foe) or obstacle** that the
  line of fire passes through between shooter and target adds a cover penalty. Each blocker counts **once**, however
  many of its cells the line crosses:

  | Blocker | Penalty |
  |---|---|
  | 2×2 or 3×3 creature | 1/8 |
  | 4×4 creature | 1/4 |
  | Small obstacle | 1/8 |
  | Big obstacle | 1/4 |
  | City wall (sieges) | 1/2 |

  - **Penalties add up.** Ranged damage × (1 − total penalty).
  - **If the total exceeds 7/8, the target cannot be shot at all** (it is out of line of fire). Example: a wall (4/8)
    + a 4×4 creature (2/8) = 6/8, so the shot deals 25 % damage. Add a small obstacle (1/8) and it is still allowed at
    7/8 (12.5 % damage). One more blocker and the shot is impossible.
  - Each obstacle definition declares its size class (small/big). Walls are a separate class.
  - Line of fire: supercover of the segment from footprint centre to footprint centre; cells touched only at a corner
    count too (ADR-038).
- **Distance penalty** (ADR-069, ADR-070, CMB-6; not built): replaces the half damage beyond 15 cells (ADR-038). Counted in
  cells between the two footprints: **0–9 no penalty, 10–19 1/8, 20–29 2/8**, and so on. Distance and cover add up to **one
  ranged penalty** with the same limit: above 7/8 the target cannot be shot. War machines ignore the distance penalty.
- **Marksmanship** (Offense skill, ADR-070): subtracts a **fixed −2/8 / −4/8 / −6/8** (Basic / Advanced / Expert) from the ranged
  penalty, so an Expert marksman shoots with almost no penalty (intended). **Order** (ADR-072, CMB-6): 1. add the distance and
  cover penalties, 2. subtract Marksmanship, 3. check the 7/8 limit. So Marksmanship can make a target shootable that cover would
  hide from others.

## Damage
**HoMM3 baseline** (ADR-033). Base damage (rolled per stack within the creature's min–max) × stack size; +5 % per point
of Attack above the target's Defense (cap +300 %); −2.5 % per point of Defense above Attack (cap −70 %); then
multiplicative modifiers (skills, cover, abilities, spells). Rolls use the seeded battle RNG.

**Native terrain** (implemented, ADR-068, HoMM3 rule): a creature whose town lists the battlefield's terrain as native
(`TerrainDefinition.NativeTowns`, the same list the adventure map uses for movement) gets **+1 Attack, +1 Defense and +1
Speed**. Neutral creatures and war machines have no native terrain; the whole army need not be native, each stack counts on
its own. The battlefield is the terrain where the battle starts, or the object's own (abandoned gold and ore mines fight
underground, so their Troglodytes are at home). The battle panel shows it.

## Area attacks
Creature capabilities, independent of size:
- **Breath** (dragon- or phoenix-like): also hits a target-sized block directly behind the target in the line of
  attack.
- **Cleave** (cerberus-like): the target plus up to 2 enemies adjacent to both attacker and target. **Multi-head**
  (hydra-like): every adjacent enemy.
- **HoMM3-style friendly fire**: breath also hits friendly units in its line; cleave/multi-head hits enemies only. Only
  the primary target retaliates.
- A unit whose footprint touches the area is hit once (ADR-038). Area **spells** follow the same rule: their area has a
  fixed size whatever the target's footprint, and a 3×3 or 4×4 creature with any cell inside is hit once (ADR-072, MAG-11;
  Phase 5).
- **Teleporting creatures** move like flyers: the footprint must fit at the destination (ADR-070, CMB-4).

## Turn order
- **Initiative decides order, not frequency** (ADR-009). Each round every stack acts **once**, in descending
  **Initiative** (a stat separate from **Speed**, which is movement range). A HoMM5-style timeline bar shows the order.
  The only extra action is a good-morale roll (ADR-053).
- **Ties**: attacker's side first, then higher speed, then a deterministic seeded roll.
- **Wait** (HoMM3): waiting units act at the end of the round, in **reverse** initiative order.
- Spells that change initiative do not change when the hero may cast (ADR-070, below); their other effects on the
  order are open ❓.

## Morale and luck (implemented, ADR-053; rates are drafts)
Each stack has −5…+5 of each, its creature's plus its hero's (and the hero's artifacts, ADR-061) and its army's (below).
- **Morale**, rolled every round: 2 % per point to act twice (good) or lose the turn (bad); otherwise 4 % per point
  for ±50 % initiative that round. War machines have no morale (ADR-071, CMB-8, as built). **Undead, constructs and
  elementals have no morale** either, whatever the hero or a warcry gives (ADR-071, CMB-7; built with ADR-075).
- **Luck**, rolled on every hit by attacker and defender, 4 % per point: the attacker's good luck (or the defender's
  bad luck) deals +75 % damage, the defender's good luck (or the attacker's bad luck) −50 %; opposite rolls cancel.
  War machines roll their hero's luck (ADR-071, CMB-8, as built).
- **Army morale** (ADR-075, CMB-2, built), set when the battle starts:
  - **Mixed factions** (HoMM3): −1 morale per faction beyond the first among the army's creatures; neutral creatures
    count as one faction, war machines not at all. Heroes' armies and town garrisons; not neutral guards or banks.
  - **Brotherhood**: +1 morale for a town's defenders when it has the upgraded tavern: its garrison, or an own hero
    standing at its entrance.
  - Skills and artifacts that change morale: Leadership, warcries, artifacts and map buffs (built).

## Actions (implemented, ADR-038)
- **Defend**: +20 % Defense (at least +1) until the unit's next turn. **Wait**: see turn order.
- **Retaliation**: once per round unless a capability says otherwise.
- **Ranged units**: no shot while engaged; half damage when more than 15 cells separate the footprints (war machines
  exempt). This is the built rule; ADR-070 replaces it with the distance penalty (CMB-6, above).
- Flyers, rolled obstacles.

## Creature capabilities (HoMM4-style, implemented, ADR-042)
Each creature has **at most 5** special rules, shown as its capability list. Every rule counts, flying and area
attacks included.

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

## Leaving a battle
- **Retreat** (implemented): a hero may leave the battle; its army is lost and it leaves the map.
- **Surrender** (ADR-070, CMB-1; not built): a hero keeps its army by paying **half the gold cost of its surviving
  creatures** and leaves the map; the hero returns to the tavern pool. Not possible against neutrals.
- **Necromancy** (ADR-069, ADR-072): after a victory, a hero with Death attunement raises undead from the enemy's losses;
  a screen after the battle result, before any level-up, shows them ([skills.md](../content/skills.md#necromancy-death-attunement-adr-069-not-built)).
  Not after a retreat or a surrender.
- The AI **retreats** when its army is almost beaten (threshold: tuning, 20 % of the enemy's value, ADR-073); AI surrender is
  not planned (ADR-070).

## War machines (implemented, ADR-056, ADR-063)
Every hero owns its town's offensive machine (ballista, cannon, catapult or glaivethrower) and can buy a healing tent
and an ammo cart at a blacksmith. They stand at the back of the field, never move or fight in melee, and are lost when
destroyed. The offensive machine shoots on its turn, outside sieges only from Advanced Ballistics; the tent heals
every own stack a little each round; the cart adds shots (unlimited with Advanced Ammo supply). Siege Ballistics rules
exist (ADR-063) but sieges do not.

ADR-070 (not built): heroes **exchange** machines like stacks; a town garrison holds machines only through a hero in it.
Spells may target machines; they are `mechanical` and resist many spells (CMB-3, MAG-4).

ADR-073 (HERO-4, built with ADR-076): like HoMM3, a machine stands mostly outside the grid and occupies **only the outermost
column** of its side: the offensive machine and the tent **1 × 3** cells, the ammo cart **1 × 2** (tent at the top, offensive
machine in the middle, cart at the bottom). Stacks start in the next column. The models are as long as the footprint and
overhang the grid's edge.

## Other
- **Quick combat**: either side may hand the rest of the battle to the deterministic battle AI (implemented).
  Neutrals and computer players always use it. The battle AI never retreats (ADR-070 plans an AI retreat, above).
- The hero casts **at most once a round**, during the turn of one of the units the hero controls; machines that fire on
  their own do not count, and spells that change initiative do not change this ([magic.md](magic.md), ADR-070 MAG-4 part,
  Phase 5). Casting does not use that unit's action: it still acts afterwards, as in HoMM3 (ADR-071, MAG-4; not built).
- **Siege battles** with walls and towers: post-1.0 ❓.
