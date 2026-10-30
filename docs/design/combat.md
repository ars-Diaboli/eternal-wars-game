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
- **Size**: **24 cells tall**, **30 cells wide** by default (ADR-021, ADR-033).
- **Movement is a circle** (ADR-089, CMB-9): a stack reaches a placement whose path is at most **Speed + 0.25 cells** long, the path
  running at any angle: on an open field a circle, around obstacles the way round. A diagonal step needs one of the two orthogonal
  placements free (no squeezing through corners). Flyers and teleporters land on any free placement within the same straight-line
  distance (ADR-038). Battle speeds were raised by 10 % with this change.
- **Tactics lets its owner choose the width before the battle**:

  | Effective Tactics | Width choices |
  |---|---|
  | none, Basic | 30 |
  | Advanced | 30, 32 |
  | Expert | 28, 30, 32, 34 |

  - **The enemy hero's Tactics reduces yours**: effective level = your level − enemy level (minimum none). At most one
    side can have an effective level above none, and that side chooses. A computer player keeps 30.
  - **36 wide is reserved for town sieges** (sieges come before 1.0, ADR-099; rules ADR-106, [below](#sieges-adr-099-adr-106-built-adr-109)).
  - Since ADR-069 (built, ADR-078) Tactics also **deploys** troops before the battle and chooses the width only from Advanced.
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
- **Distance penalty** (ADR-069, ADR-070, CMB-6; built, ADR-078): replaces the half damage beyond 15 cells (ADR-038). Counted in
  cells between the two footprints, as a straight line like movement (ADR-089: 8 across and 8 down is 11.3): **0–9 no penalty, 10–19 1/8, 20–29 2/8**, and so on. Distance and cover add up to **one
  ranged penalty** with the same limit: above 7/8 the target cannot be shot. War machines ignore the distance penalty.
- **Marksmanship** (Offense skill, ADR-070): subtracts a **fixed −2/8 / −4/8 / −6/8** (Basic / Advanced / Expert) from the ranged
  penalty, so an Expert marksman shoots with almost no penalty (intended). **Order** (ADR-072, CMB-6): 1. add the distance and
  cover penalties, 2. subtract Marksmanship, 3. check the 7/8 limit. So Marksmanship can make a target shootable that cover would
  hide from others. A shot deals (8 − penalty)/8 of its damage (`BattleGrid.RangedPenaltyEighths`).

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
- **Teleporting creatures** move like flyers: the footprint must fit at the destination (ADR-070, CMB-4; the `Teleporting` capability,
  ADR-083).

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
- **Ranged units**: no shot while engaged; the distance penalty and cover lower the damage (above; war machines have no
  distance penalty).
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
- **Retreat** (implemented): a hero may leave the battle; its army is lost and it leaves the map. A roster hero waits in its
  player's taverns as it was and can be hired back (ADR-081).
- **Surrender** (ADR-070, CMB-1; built ADR-081): a hero keeps its army by paying **half the gold cost of its surviving
  creatures** (rounded up, to the enemy player) and leaves the map; it waits in its player's taverns with its army. Not possible
  against neutrals. The winner gets no hero bonus and no loot.
- **Necromancy** (ADR-069, ADR-072): after a victory, a hero with Death attunement raises undead from the enemy's losses;
  a screen after the battle result, before any level-up, shows them ([skills.md](../content/skills.md#necromancy-death-attunement-adr-069-built-adr-078)).
  Not when the enemy retreated or surrendered. Built (ADR-078): `NecromancyRules`, the `UndeadRaised` event and its screen.
- The AI **retreats** when its army is almost beaten: worth 20 % of the enemy's or less (threshold: tuning, built ADR-081); AI
  surrender is not planned (ADR-070).

## War machines (implemented, ADR-056, ADR-063)
Every hero owns its town's offensive machine (ballista, cannon, catapult or glaivethrower) and can buy a healing tent
and an ammo cart at a blacksmith. They stand at the back of the field, never move or fight in melee, and are lost when
destroyed. The offensive machine shoots on its turn, outside sieges only from Advanced Ballistics; the tent heals
every own stack a little each round; the cart adds shots (unlimited with Advanced Ammo supply). Siege Ballistics rules
apply in sieges (ADR-063, ADR-106, built ADR-109): the attacker's offensive machine is the siege engine and always joins a siege (below).

Heroes **exchange** machines in their exchange: one moves to the other hero when it has none in that slot; an offensive machine becomes the
receiver's town's kind (ADR-070, ADR-083). A town garrison holds machines only through a hero in it (not built: no garrison heroes yet).
Spells may target machines; they are `mechanical` and resist many spells (CMB-3, MAG-4).

ADR-073 (HERO-4, built with ADR-076): like HoMM3, a machine stands mostly outside the grid and occupies **only the outermost
column** of its side: the offensive machine and the tent **1 × 3** cells, the ammo cart **1 × 2** (tent at the top, offensive
machine in the middle, cart at the bottom). Stacks start in the next column. The models are as long as the footprint and
overhang the grid's edge.

## Sieges (ADR-099, ADR-106; built, ADR-109)
The owner's answers to CMB-11; built in ADR-109 (core, battle AI, adventure AI, 2D board and 3D scene; the 3D scene is not run in Godot yet).
Numbers are placeholders; *(assumed)* marks choices made while building.

- **When**: every battle at a town with a **Fort**, whether a hero is inside or only the garrison, is a siege on the 36-wide field. A town
  without a Fort has an ordinary battle. "Inside" is a hero standing at the entrance of its own town; map garrisons are not besieged. The
  width is fixed (no Tactics choice); deployment works as in other battles.
- **Fortifications** by the town's fort level (ADR-066): Fort: four wall sections and the gate; Citadel: also the keep and the moat; Castle:
  also two towers, and stronger walls.
- **Walls**: **4 sections and the gate**. A section has **2** hit points (Fort, Citadel) or **3** (Castle). A destroyed section is a gap the
  attackers walk through. Shooters shooting across a wall take the 1/2 cover penalty (cover table above), once per shot; only the attackers'
  shots *(assumed: the defenders shoot out freely)*.
- **Layout** *(assumed)*: the wall line is column 24 of the 36 (24 columns for the attackers, 11 behind the walls). Four sections of 5 rows,
  the gate of 4 rows in the middle (a 4 × 4 creature passes). The moat is the 2 columns in front of the walls, bridged in the gate's rows.
  The keep stands behind the defenders' edge and the towers above and below the wall line, **off the grid**: they block nothing and only the
  siege engine reaches them. Random obstacles stay in front of the moat.
- **The gate**: the defenders pass through it, the attackers break it. It has **1** hit point.
- **Who breaks the walls**: there is **no free siege catapult**. The attacking hero's own **offensive machine** (ballista, cannon, catapult or
  glaivethrower, [hero-roster.md](../content/hero-roster.md#war-machines)) fires at the fortifications; the catapult is only Stronghold's and
  Swamp Bestiary's offensive machine, with no siege role of its own. Siege Ballistics (ADR-063, [skills.md](../content/skills.md#war-machines-machines))
  as drafted: without the skill the machine fires by itself, only at fortifications, 2 hits in 3, 1–2 hit points a hit; Basic gives control.
  **Firing by itself** it aims at the wall sections, at the gate only when all four sections are destroyed, then at the keep and towers *(the
  order after the gate is assumed)*; a player in control may aim at any of them. An attacker without an offensive machine (destroyed and not
  bought again) breaks nothing; Earthquake still damages wall sections ([spells.md](../content/spells.md); with magic). In a siege the
  attacker's offensive machine always joins, whatever its Ballistics; without the skill it fires by itself for a person too.
- **Keep and towers**: the keep (Citadel) and the two towers (Castle) each shoot **once a round** at an attacking stack. Damage: **10 + 2 per
  building in the town**, the keep double, flat damage to the attacking creature stack worth most to hit, **when each round starts**
  *(assumed)*. They can be destroyed: **3** hit points each. **Buildings** count every built level of every building and dwelling *(assumed)*.
- **Moat** (Citadel): a band of cells in front of the walls. A creature entering it (walking or landing) stops there and takes damage worth
  about **two tower shots**: placeholder **2 × (10 + 2 per town building)** to the stack, once each time it enters. Only the attackers
  *(assumed)*; a walking stack stops as soon as its footprint touches the moat and moves on freely from there.
- **Flyers and teleporters** cross the walls. The defenders' shooters behind the walls get the 1/2 cover against attacking shooters; one
  defending shooting stack may stand in the keep (**not built**).
- **The AI** attacks a fortified town only when its battle estimate wins against the walls, towers and moat, and defends behind them: the
  estimate fights are sieges; in battle the defenders do not advance out of the walls (they fight what they can reach), walking attackers take the way round to a breach or the
  broken gate and, while the walls hold, close up to the moat without entering it.

## Other
- **Auto combat** (ADR-088, ADR-090): each human player's setting in the game menu: off, ask before each battle, or always. It covers battles
  against computer players and neutrals. The battle AI plays a copy of the battle for both sides and a screen shows the outcome (casualties,
  experience, raised undead, artifacts taken). **Accept** ends the battle exactly so (quick combat); **Fight** fights it by hand from where it
  stands, so rejecting re-rolls nothing.
- **Quick combat**: either side may hand the rest of the battle to the deterministic battle AI (implemented).
  Neutrals and computer players always use it. The battle AI never retreats (ADR-070 plans an AI retreat, above).
- The hero casts **at most once a round**, during the turn of one of the units the hero controls; machines that fire on
  their own do not count, and spells that change initiative do not change this ([magic.md](magic.md), ADR-070 MAG-4 part,
  Phase 5). Casting does not use that unit's action: it still acts afterwards, as in HoMM3 (ADR-071, MAG-4; not built).
- **Siege battles** with walls, gate and towers: built (ADR-109); rules [above](#sieges-adr-099-adr-106-built-adr-109) (ADR-106).
