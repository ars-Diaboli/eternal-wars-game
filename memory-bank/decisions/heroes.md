# Decisions: Heroes and skills

**Covers**: Primary stats, skills and levels, specialties, Necromancy, governors, the tavern.

**Current rules**: [docs/design/heroes.md](../../docs/design/heroes.md), [docs/content/skills.md](../../docs/content/skills.md), [docs/content/hero-roster.md](../../docs/content/hero-roster.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [048](#adr-048-heroes-skills-and-levels) | Heroes, skills and levels | accepted; superseded by ADR-062 |
| [055](#adr-055-primary-stat-growth) | Primary stat growth | accepted |
| [060](#adr-060-the-tavern-and-hiring-heroes) | The tavern and hiring heroes | accepted |
| [063](#adr-063-governors-complexity-mana-faction-groups-and-siege-ballistics-2026-10-17) | Governors, complexity, mana, faction groups and siege Ballistics (2026-10-17) | accepted |
| [069](#adr-069-skill-rework-necromancy-mana-by-level-one-faction-group-a-second-object-catalogue-2026-10-20) | Skill rework, Necromancy, mana by level, one faction group, a second object catalogue (2026-10-20) | accepted; partly superseded |
| [078](#adr-078-the-skill-rework-necromancy-and-the-necromancy-amplifier-implementation-2026-10-26) | The skill rework, Necromancy and the Necromancy Amplifier (implementation, 2026-10-26) | implemented |
| [081](#adr-081-surrender-the-ais-retreat-returning-heroes-tavern-pools-per-faction-the-brotherhood-price-and-starter-stacks-2026-10-26) | Surrender, the AI's retreat, returning heroes, tavern pools per faction, the Brotherhood price and starter stacks (2026-10-26) | implemented |
| [093](#adr-093-rarer-resource-specialties-necromancy-and-logistics-specialties-2026-10-29) | Rarer resource specialties, Necromancy and Logistics specialties (2026-10-29) | accepted |
| [107](#adr-107-rarer-specialties-the-hero-8-proposals-for-now-2026-10-30) | Rarer specialties: the HERO-8 proposals, for now (2026-10-30) | accepted |


### ADR-048: Heroes, skills and levels
**Status**: accepted (2026-10-16), from [ideas.md](../ideas.md); numbers are a draft; Treasury numbers superseded by ADR-062;
Stewardship (governors), attunement and the schools by ADR-063. Note: the rival schools in code are the sworn-enemy pairs of
`BuiltInSkills.Rivals` (`Skills.cs`; two per school town, none for Stronghold and Swamp Bestiary), not the lore ring
- **Roster**: every playable town has **16 heroes**, a **mage** and a **warrior** for each of its eight tier 1–4
  creature lines, which is that hero's **preferred creature**. The preferred line (base and upgrade) gets a **flat
  +2 Attack, +2 Defense and +1 battlefield speed** (user decision). Each hero also has a HoMM3-style **specialty**: a
  skill, a spell or a daily resource. The roster is a table like the creatures, `data/heroes.csv` (`HeroTable`,
  embedded as `HeroCatalog.Default`, read by the apps at start-up, `--heroes=` overrides; ADR-044 conventions). Map
  heroes may name a roster entry (`type`, map format v5) and take its identity, stats and starting skills.
- **Skills** (HoMM4-like): **groups of three skills**, **10 points** to learn a group fully (usually 4/3/3; magic 4/4/2);
  a hero learns **at most 6 groups**. Levels are **Basic, Advanced, Expert, Master** (3-level skills stop at Expert).
  Groups: offense (melee, archery, a third skill to decide), defense (armor, spell resistance, a third to decide),
  estates (treasury: gold; prospecting: other resources; stewardship: cheaper costs in the hero's town), pathfinding
  (logistics: movement; pathfinding: terrain penalty; troop management: army map speed), tactics (tactics: battlefield
  width; deployment: rearrange troops before battle; drill: battlefield speed), and the eight schools (ADR-047). The
  Tactics skill of ADR-033 is now `tactics.tactics`; maps keep loading their old `tactics` field.
- **Levels**: HoMM3's experience table (+20 % per level after 12). Each level gives **one skill point**: the player
  picks one of **three offered skills** (`LevelUpDecision`, `ChooseSkillCommand`). Offers are weighted by class
  (mages towards their own school, warriors towards the general groups, skills already started ×3) and drawn from the
  game RNG. A **rival town's school** (the lore ring) is offered in only **5 %** of level-ups (user decision), the one
  rule effect of rivalries (an exception to ADR-029, which is about combat). Choices happen in the owner's turn; a hero
  that levels up during another player's turn chooses when its owner's turn starts; computer players take the first
  offer. Primary stat growth per level: ADR-055.
- **Effects now** (`SkillEffects`): melee +10 % per level, archery +10/25/50 %, armor −5 % damage taken per level,
  treasury +100/200/350/500 gold a day, prospecting +1/+2 lumber and ore (Expert also one of each rare resource),
  logistics +10 % movement per level, pathfinding −25/50/75 % of the terrain penalty, troop management +1 army map speed
  per level, drill +1 battlefield speed per level, resource specialties pay daily (`HeroIncome`). Not implemented
  yet: stewardship (towns exist now; it is a Phase 3 roadmap item), skill specialties; waiting for magic or a
  deployment step: spell resistance, deployment, the school skills, spell specialties.

### ADR-055: Primary stat growth
**Status**: accepted (2026-10-16), answers the former open question 8 (user decision: HoMM3-style, odds by class); odds and
starting values are a draft; Knowledge sets mana (ADR-063)
- Heroes have four primary stats: **Attack**, **Defense**, **Spell Power** and **Knowledge**. Spell Power and Knowledge
  are new (`Hero.SpellPower`, `Hero.Knowledge`); they do nothing until magic (Phase 5: spell strength and mana).
- **Every level-up raises one primary stat by 1**, besides the skill point (ADR-048). The stat is drawn from the game
  RNG with odds by class (`StatGrowth`), one table for levels 2–9 and one from level 10:

  | Class | Levels 2–9 (A / D / SP / K) | Levels 10+ |
  |---|---|---|
  | Warrior | 35 / 45 / 10 / 10 % | 30 / 30 / 20 / 20 % |
  | Mage | 10 / 10 / 40 / 40 % | 20 / 20 / 30 / 30 % |

  HoMM3 has one table per hero class of every town; ours has one per class for now, and may get per-town tables.
- The stat is rolled together with the skill offers, so the level-up prompt shows it ("+1 Attack. Choose a skill")
  (`LevelUpDecision.Stat`); `HeroLeveledUp` carries the stat and its new value. Computer heroes get theirs too.
- **Starting values** (`data/heroes.csv`, new columns `spell_power` and `knowledge`): every roster hero has 5 points;
  warriors keep their 2/1 or 1/2 Attack/Defense and get 1/1, mages get 3/2 (with 0/0) or 2/2 (with 0/1).
- Map format **v8**: map heroes may set `spellPower` and `knowledge` (omitted = 0 for custom heroes, the roster's values
  for roster heroes); the editor inspector edits them. The state digest includes all four stats.

### ADR-060: The tavern and hiring heroes
**Status**: accepted (2026-10-16), from user decisions on who a tavern offers; cost, upgrade price, limit and placement
are drafts
- **Tavern levels**: the tavern is an upgradeable building (`data/buildings.csv`): **Tavern** (750 gold, 5 lumber; every
  new town has it) offers **2** heroes, **Brotherhood** (2000 gold, 5 lumber, 5 ore, needs the fort) offers **4**.
- **Ranks** (`TavernRules`): the six main towns sit on the **ring of towns** (user decision, [lore.md](../../docs/content/lore.md)): Sylvan, Haven,
  Academy, Dungeon, Necropolis, Inferno, and back to Sylvan. For a town, a hero is **0 native**, **1 adjacent** (the
  town's two ring neighbours: Sylvan: Inferno, Haven; Haven: Sylvan, Academy; Academy: Haven, Dungeon; Dungeon: Academy,
  Necropolis; Necropolis: Dungeon, Inferno; Inferno: Necropolis, Sylvan), **2 not opposing** (the two towns two steps
  away, Stronghold and Swamp Bestiary) or **3 opposing** (the town across the ring, user decision: Sylvan–Dungeon,
  Academy–Inferno, Haven–Necropolis). The pool order is that order. The first version of this ADR used another ring
  (the sworn-enemy ring) and was corrected by the user.
- **Slots**: basic = native + adjacent; upgraded = native, native, adjacent, not opposing. Opposing heroes come last,
  only when nothing else is left. A slot with nobody left in its rank takes the next ranks, then the earlier ones.
- **Stronghold and Swamp Bestiary** (user decision) have no ring: rank 0 native, **1 warriors of the other towns**, 2 all
  other heroes. Basic = native + warrior (our reading); upgraded = 2 native + 2 warriors.
- **The pool is per player and weekly**: `TavernRules.PoolOrder` shuffles the roster from the game seed, the player and the
  week, so every player sees other heroes, the offers change every Monday, and nothing is stored or drawn from the
  game's random numbers. A hero on the map (anyone's) is not offered; a defeated or retreated hero is back in the pool.
  Hiring lets the next hero of that rank step forward; there is no empty slot until Monday.
- **Hiring** (`HireHeroCommand`, `TownService.HireHero`): the owner's turn, an own town with a tavern, a hero the town
  offers now, **2500 gold**, the entrance free, at most **8 heroes** per player. The hero stands at the entrance with its
  roster stats and skills, no army, full movement. Heroes get ids never used before (`NextUnusedHeroId`).
  `HeroHired(Town, Hero, Definition)` is published; visibility reacts to it.
- **UI**: the town panel lists the offers with a hire button (and why a hire is refused).
- **AI**: it hires the best offer (warriors first) while it has fewer heroes than one more than its towns, at most four, and
  the gold beyond what it saves for a building allows; the new hero is the town's visiting hero, so recruits go to it.
- Open: the draft numbers, whether surrender comes with taverns (ADR-038), per-town pools, hiring when an own hero stands
  at the entrance, and hero level or starter stacks for hired heroes.

### ADR-063: Governors, complexity, mana, faction groups and siege Ballistics (2026-10-17)
**Status**: accepted (2026-10-17)
Supersedes parts of ADR-047 (eight schools), ADR-048 (Stewardship, attunement) and ADR-056 (Ballistics). Numbers are drafts.

- **Governors and Stewardship**: a hero with Stewardship can be appointed governor of an own town (`AppointGovernorCommand`,
  `DismissGovernorCommand`, event `GovernorChanged`); a hero governs one town, appointing it elsewhere moves it. The
  assignment persists while the hero roams; it is void when the hero is gone, changes owner or loses the skill
  (`StewardshipRules.GovernorOf`), and is cleared when the town is captured. A visiting own hero with the skill becomes
  governor of a governor-less town by itself (on arrival and at the start of the owner's turn) unless it governs
  another. Stewardship applies only to a town with a governor: buildings **5 / 10 / 15 %** cheaper, creatures
  **10 / 15 / 20 %** cheaper (recruiting and upgrading; rounded up per resource). Map dwellings use the recruiting hero's own level and caravans the
  destination town's governor (ADR-054). Not built yet: marketplace trading (a governed town values resources at **150 %**,
  `StewardshipRules.MarketplaceValuePercent`).
- **Complexity** (`GameOptions.Complexity`, null = the map's own resources): Beginner, Easy, Normal, Hard, Expert. At the
  start of the game every player gets `StartingResources.For(level)`: 10000 gold with 30 lumber, 30 ore and 10 of each rare
  resource; 7500, 20, 20, 6; 5000, 15, 15, 3; 3500, 12, 12, 1; Expert 2500 gold, 10 lumber, 10 ore, no rares (the
  designer's hardest set; the other four are guesses to tune). Chosen on the new-game screen; saved in replays.
- **Mana** (`ManaRules`, `Hero.Mana`): the pool is **10 x Knowledge** plus attunement; every day at the start of the owner's turn a hero
  regains **ceil(Knowledge / 10)** plus attunement (`HeroManaRestored`, only when the pool is not full); a spell costs its base
  minus attunement but never less than **25 % of the base, rounded up**. A hero that never spent mana has a full pool.
  Casting waits for Phase 5.
- **School attunement** (the second skill of each of the six schools, 4 levels):

  | Schools | Max mana | Mana a day | Spell cost |
  |---|---|---|---|
  | Arcana, Chaos | +12 / 25 / 37 / 50 | +1 / 2 / 3 / 4 | - |
  | Light, Death | - | +3 / 6 / 9 / 12 | -1 / 2 / 3 / 4 |
  | Nature, Shadow | +5 / 10 / 15 / 20 | +2 / 4 / 6 / 8 | - |

  Several attunements add up.
- **Stronghold and Swamp Bestiary have no magic school**: six schools remain. Each has a faction ability group instead,
  **Rage** (`rage`, Stronghold) and **Adrenaline** (`adrenaline`, Swamp Bestiary): three placeholder skills each
  (`rage.first/second/third`, 4 + 3 + 3 points) with no effect; the mechanics (stacks of rage / adrenaline or other
  faction abilities) are open. Only heroes of the town are offered them; their roster mages start with `.first`, and their
  former spell specialties are `skill rage.first` / `skill adrenaline.first` for now. The Shamanism and Primal spells in
  [spells.md](../../docs/content/spells.md) are drafts waiting to be reused as faction abilities.
- **Ballistics in sieges** (rules in `WarMachines`; fortifications and sieges do not exist yet, so nothing calls them in battle):
  without the skill the offensive machine **fires by itself, only at fortifications**; **Basic** gives control of it; **Advanced**
  lets it also target troops (and fields it in regular battles, as ADR-056 had it) and fires a second shot at an automatically
  chosen target when the first destroyed its structure; **Expert** never misses a fortification (otherwise 2 of 3 shots hit)
  and keeps the +25 % damage against troops; **Master** does **2-3** structure damage instead of 1-2 (and +50 % against troops).
  A fully upgraded fortification's defence structures have **3** hit points. Placeholder formulas.

### ADR-069: Skill rework, Necromancy, mana by level, one faction group, a second object catalogue (2026-10-20)
**Status**: accepted as a design (2026-10-20), user notes in `ideas.md`; **nothing implemented**. Numbers are drafts. Supersedes parts of ADR-048 (skill
numbers and groups), ADR-063 (attunement table, the Adrenaline group), ADR-067 (Leadership and Alertness ids and numbers, the faction spell resistance) and
ADR-038 (the ranged half-damage rule, if CMB-6 says so). Open points: HERO-4, HERO-6, HERO-7, CMB-6, MAG-7, MAG-8, TOWN-6, MAP-15 … MAP-24 in `open-questions.md`.
- **Skill groups** (every group keeps three skills and 4 + 3 + 3 points; the Master skill is the 4-point one):

  | Group | Skills (max level: Basic / Advanced / Expert / Master) |
  |---|---|
  | Offense | Melee (Master: +9 / 16 / 23 / 30 % melee damage), Archery (Expert: +15 / 25 / 35 % ranged damage), **Marksmanship** (new, Expert: ranged penalty −1/4 / −1/2 / −3/4) |
  | Defense | Armor (unchanged), Spell resistance (Expert: −10 / 15 / 20 % to hostile spells' damage or duration, Phase 5), **Leadership** (moved here from Offense, +1 / 2 / 3 morale) |
  | Pathfinding | **Pathfinding** (now Master: −25 / 50 / 75 / 100 % of the non-native penalty), **Logistics** (now Expert: +10 / 20 / 30 % movement), **Scouting** (new, Expert: +2 / 4 / 6 sight radius); Troop management is dropped |
  | Tactics | **War Tempo** (Drill renamed, Master: +1 / 2 / 3 / 4 battlefield speed), **Tactics** (Expert: Basic rearranges troops before battle; Advanced also chooses width 30 or 32; Expert 28–34; the deployment zone is 4 / 5 / 6 cells deep), **Alertness** (moved here from Defense, working title, Expert: **+1 / 2 / 3 initiative**, flat instead of percent) |

  - The Deployment skill is merged into Tactics. Tactics Basic no longer chooses a width (it was 30 / 32); Advanced loses 28.
  - **Distance penalty for ranged attacks**: every 10 cells of distance add a **1/8 ranged penalty**. How it meets cover and the 15-cell half-damage rule
    (ADR-038), and what Marksmanship reduces, is CMB-6.
  - Skill ids: `offense.marksmanship`, `defense.leadership`, `pathfinding.scouting`, `tactics.wartempo`, `tactics.alert`; the rest keep theirs. How older maps
    and saved games with `offense.third`, `defense.third`, `tactics.drill`, `tactics.deployment`, `pathfinding.troops` and `adrenaline.*` load is HERO-6.
- **Attunement** (replaces the ADR-063 table; several add up):

  | Schools | Max mana | Mana a day | Spell cost |
  |---|---|---|---|
  | Arcana, Chaos | +10 / 20 / 30 / 40 | +1 / 2 / 3 / 4 | – |
  | Light | – | +2 / 4 / 6 / 8 | −1 / 2 / 3 / 4 |
  | Death | – | +2 / 4 / 6 / 8 | – (Necromancy instead) |
  | Nature, Shadow | +5 / 10 / 15 / 20 | +2 / 4 / 6 / 8 | – |

- **Mana by level**: the pool also grows by **10 × ⌈level / 10⌉** (+10 at levels 1–10, +20 at 11–20, …).
- **Necromancy** (Death attunement): after a **victory**, 5 / 10 / 15 / 20 % (Basic … Master) of the **value of the enemy creatures slain** comes back as undead
  in the hero's army. The attunement level caps the undead tier and maps enemy tiers to it:

  | Level | Undead raised | Enemy tier → undead tier |
  |---|---|---|
  | Basic | tier 1 | all → 1 (tier-1 lines in random proportion) |
  | Advanced | tiers 1–2 | 1–3 → 1, 4–5 → 2 |
  | Expert | tiers 1–3 | 1–2 → 1, 3–4 → 2, 5 → 3 |
  | Master | tiers 1–4 | 1–2 → 1, 3 → 2, 4 → 3, 5 → 4 |

  - **Necromancy Amplifier**, a Necropolis building: +5 % Necromancy for **every hero of its owner** that has the skill (10 / 15 / 20 / 25 %); a second amplifier
    adds nothing. It would be the first town-specific building (TOWN-3). Details: MAG-7, TOWN-6.
- **Faction group**: Primal Fury is the group of **both Stronghold and Swamp Bestiary**; **Adrenaline is dropped** (answers most of HERO-2). Its skills:
  Blood Rage (unchanged), Warcry (working names "Warcry / Spiritual Totems", unchanged), and **Interference** (replaces the faction spell resistance, `rage.third`):
  the enemy hero's spells have −2 / −4 / −6 Spell Power and cost +1 / +2 / +2 mana (Phase 5). The group is for the towns' **mage** heroes; their **warrior**
  heroes start with the **Offense** group (HERO-7).
- **Second object catalogue** (the owner's list, written out in [adventure-map.md](../../docs/design/adventure-map.md)):
  - Loot: Mage's Chest (minor artifacts).
  - Utilities: Window and Eye of the Magi, Tavern, Portal, One-Way Portal (both exist as monoliths, ADR-026), Garrison, Keymaster's Tent with border guards
    and gates (RMG-11).
  - Deposits that become mines after paying: Wood Pile → Sawmill, Ore Deposit → Ore Pit, Gold Vein → Gold Mine, Crystal Vein → Crystal Mine, Cinnabar Deposit
    → Alchemist's Lab.
  - Creature banks: Ruins with orcs (the name is taken, MAP-22), Lair (evil eyes), Keep (crossbowmen and pikemen), Hideout (bandits), Forest Glen (wolves),
    Dragon City (Black Dragons).
  - Magic: Mana Vortex (doubles the maximum mana), Fountain of Magic (restores 50 %), Shrines of levels 1–3 (teach a spell; Phase 5).
  - Buffs: Clover Field (+1 luck), Rainbow (+2 luck), Rally Flag (+400 movement today, +1 luck and morale) for the next battle.
  - Power-ups: Dream Teacher (500 XP, +500 for each other Dream Teacher the hero visited; once per hero), Training Grounds (+1 Attack), Marletto Tower
    (+1 Defense), Star Axis (+1 Spell Power), Garden of Revelation (+1 Knowledge), once per hero.

### ADR-078: The skill rework, Necromancy and the Necromancy Amplifier (implementation, 2026-10-26)
**Status**: implemented (2026-10-26). Carries out ADR-069's skill rework with ADR-070's distance penalty, ADR-072's details (HERO-1 … HERO-7,
MAG-7 … MAG-10, CMB-6, TOWN-6) and ADR-073's Primal Fury mage odds; choices those ADRs left open are recorded here.

- **Ids** (HERO-6): `BuiltInSkills.Renamed` maps `offense.third` → `defense.leadership`, `defense.third` → `tactics.alert`, `tactics.drill` →
  `tactics.wartempo`, `tactics.deployment` → `tactics.tactics`, `pathfinding.troops` → `pathfinding.scouting` and `adrenaline.*` → `rage.*`.
  `Resolve`/`Migrate` apply it wherever skills are read: hero skills, the hero table (skills and specialties), map and save heroes, artifact
  effects and `ChooseSkillCommand` in old replays. A level above the new maximum drops to it; two old skills that became one keep the higher
  level. No map format bump. `heroes.csv` uses the new ids.
- **Groups**: 13 (six general, six schools, Primal Fury). `SkillGroup.Factions` lists the towns of a faction group (`BelongsTo`); Primal Fury
  belongs to Stronghold and Swamp Bestiary and weighs 6 in the offers for mages and warriors alike. Rivals: one arch-rival per town (HERO-3).
- **Numbers**: Melee 9/16/23/30 %, Archery 15/25/35 %, Pathfinding −25 % a level up to Master, Logistics 10/20/30 % up to Expert, Alertness
  +1/2/3 initiative added to the creature's before the warcry percentages, Tactics width only from Advanced (30/32; Expert 28–34). Troop
  management is gone without a replacement effect.
- **Scouting**: `Hero.ScoutingRadius` is the base radius (5, or what maps and artifacts set) plus 2/4/6. `VisibilityService` reveals the wider
  circle on `HeroLeveledUp` for Scouting *(choice made here: at once, not at the next step)*.
- **Ranged penalty** (CMB-6): `BattleGrid.RangedPenaltyEighths` = distance (cells between the footprints / 10, none for machines) + cover −
  Marksmanship (2/4/6 eighths, not below 0); above 7/8 there is no shot (`BattleState.MaxRangedPenaltyEighths`). A shot deals (8 − penalty)/8;
  `HitModifiers.PenaltyEighths` replaces `CoverEighths`, and the 15-cell halving (`LongRange`) is gone.
- **Primal Fury**: Interference (`rage.third`) carries the Spell Power penalty and the +1/2/3 spell cost (`HeroBattleBonuses.EnemySpellCostPenalty`),
  data until casting. Primal Fury mages roll 25/25/25/25 at every level (`StatGrowth.Odds` takes the hero's town). Their Spell Power does not
  strengthen Blood Rage and warcries yet (no numbers; roadmap). Roster: Stronghold warriors Urzog, Kragash and Shara now start with Melee
  instead of Armor or Tactics; Swamp Bestiary warriors Ssarkh, Mudjaw and Oszrak with Armor, Brakkis with Leadership and Ylsa with Spell
  resistance *(choices made here: one starting skill swapped, the specialty kept)*.
- **Mana**: the ADR-069 attunement table; `ManaRules.LevelMana` = 10 × ⌈level / 10⌉ added to the maximum only (MAG-8).
- **Necromancy** (`NecromancyRules`, applied in `BattleService.Finish`): only after the enemy was **defeated** (heroes and neutral guards); not
  when it retreated *(choice made here; surrender does not exist yet)*. Order: `BattleEnded`, the survivors' `ArmyChanged`, then the raised
  undead (`ArmyChanged` and `UndeadRaised(Hero, Raised)`), then `HeroExperienceGained` and level-ups. The random split of a tier between its two
  lines is 0–100 % in whole percent (one game-RNG draw per tier). `UndeadRaised` lists what joined, with the upgrade id where 3/4 went to an
  upgraded stack; nothing is published when nothing joined.
- **Necromancy screen**: `BattleHud` keeps the last `UndeadRaised` text; "Return to the map" (Enter) first shows it on the result panel, then
  closes the battle. Not run in Godot yet.
- **Necromancy Amplifier**: the first town row in `buildings.csv` (`necromancy-amplifier`, Necropolis, 1500 gold + 5 mercury, mage guild 1).
  `NecromancyRules.IsAmplified` asks whether the player owns any town with it, so a captured one helps its new owner.
- **AI**: computer heroes take Necromancy (Death attunement) whenever a level-up offers it, otherwise the first offer
  (`SkillOffers.ComputerChoice`) *(choice made here; how much the AI should value other skills is AI tuning)*.
- Tests: `SkillReworkTests`, `NecromancyTests` and updated battle, mana and progression tests; `sim selftest` passes with a new digest.

### ADR-081: Surrender, the AI's retreat, returning heroes, tavern pools per faction, the Brotherhood price and starter stacks (2026-10-26)
**Status**: implemented (2026-10-26). Builds the ADR-070 / ADR-072 tavern and surrender answers (CMB-1, MAP-17 / TOWN-4 town taverns,
TOWN-7, TOWN-8); the map tavern stays with the second catalogue. Choices made here are marked *(choice)*; numbers are drafts.

- **Surrender** (`BattleSurrenderCommand`, `BattleService.Surrender`, `SurrenderRules`): during the fight, a side with a hero facing a
  player (a hero or an owned town's garrison, never neutrals) pays **half the gold cost of its surviving creatures**, rounded up (war
  machines free), **to the other player** *(choice, as HoMM3)*; refused without the gold. `HeroSurrendered(Hero, Owner, To, Gold)`, then
  `BattleEnded` with the new reason `Surrendered`. The winner gets the experience for what it killed, no hero bonus and no loot; the
  hero keeps its surviving creatures and machines.
- **Returning heroes** *(choice: HoMM3, needed for surrender to differ from a retreat)*: a roster hero that **retreats or surrenders** is not
  lost: it waits off the map (`GameState.RetiredHeroes`) as it was (level, experience, skills, artifacts, surviving machines; the army only
  after a surrender) and stops governing. Its player's taverns offer it **first**, in every town of the player, until it is hired; other
  players never see it. Hiring it (2500 gold) brings back the same hero with the same id. It does not count towards the hero limit; a
  defeated player's waiting heroes go back to the roster. Heroes made in the map editor without a roster entry still just leave.
  `HeroDefeated.Retreated` is true for both. Saves are replays; the digest lists waiting heroes only when there are any.
- **The AI retreats** (ADR-070): a computer player's hero, when one of its units is to act, gives the battle up once its army is worth
  **20 % of the enemy's or less** (creature value, machines excluded; `SurrenderRules.RetreatBelowShare`, tuning). Quick combat never
  retreats for a person; the AI's estimate fights go to the end (`BattleService.AiMayRetreat`). The AI does not surrender, and it hires
  its own returning heroes first.
- **Tavern pools** (ADR-070, TOWN-7): the weekly shuffle is per player **and town faction** (`TavernRules.PoolOrder(state, player,
  town)`), so the player's towns of one faction show the same offers and other factions their own. Basic tavern 1 native + 1 adjacent (from
  either ring neighbour, by the shuffle); the **Brotherhood 2 native + 2 adjacent** (`TavernRules.Slots`), for Stronghold and Swamp
  Bestiary 2 native + 2 warriors as before. Ranks with nobody left still fall through.
- **Brotherhood** costs **1000 gold + 5 ore** (`buildings.csv`).
- **Starter stack** (MAP-17): a hired hero with an empty army gets **one week's growth of its preferred creature** (`TavernRules.StarterStack`,
  placeholder *(choice)*); a returning hero that kept its army gets none. Freed prisoners get none.
- **Hero limit** (TOWN-8): prisoners never counted (they are objects); heroes in a town's garrison do not exist yet (no garrison hero
  slot), so that part waits for them. Waiting heroes do not count.
- UI: a "Surrender (N gold)" button in the battle HUD with the refusal as tooltip (S in the 2D harness); the result screen names a
  surrender; the town panel marks a returning hero with its level and "with its army" (not run in Godot yet).
- Tests: `SurrenderTests`, `TavernTests`.

### ADR-093: Rarer resource specialties, Necromancy and Logistics specialties (2026-10-29)
**Status**: accepted as a design (2026-10-29), owner notes in `ideas.md`; **nothing implemented**. The open points were HERO-8, answered
in ADR-107 (the proposals, for now). Changes the roster template (ADR-048) and the specialty table in `skills.md`.

- **Resource specialties become rare**: across the whole roster, **4 heroes** give **+350 gold a day** (picked at random, no two of one
  faction) and **6 heroes** give **+1 of a rare resource a day** (no two of one faction): 2 sulfur, 2 crystal, 2 mercury.
- **Necromancy specialty**: **2 heroes**, one mage and one warrior, raise a **flat +5 %** more with Necromancy.
- **Logistics specialty**: **2 heroes**, picked at random, move a **flat +10 %** farther.
- Today the roster template gives every town the same specialties: 3 heroes with +350 gold (24 in all), one each with +2 ore, +2 lumber and
  +1 mercury (8 each), 1 Logistics specialist (+5 % per level, 8 in all) and the Necropolis mages' Death attunement specialty (Necromancy's
  specialty effect is not built). What happens to the heroes that lose their specialty, and whether the flat bonuses replace the per-level
  ones, is HERO-8.
- Fits the core as it is: `HeroSpecialty` already has resource specialties (`HeroIncome`) and skill specialties; the change is mostly the table
  `heroes.csv`, plus a flat amount for a skill specialty (`HeroSpecialty.Amount`) and the Necromancy effect.

### ADR-107: Rarer specialties: the HERO-8 proposals, for now (2026-10-30)
**Status**: accepted for now (2026-10-30): the owner's answer to HERO-8 (ADR-093) is "use the proposals for now", so they may still change
once the roster is play-tested. **Nothing implemented.**

- **The whole roster** (a): ADR-093's numbers are for all 128 heroes and replace today's 48 resource specialists and 8 Logistics
  specialists: 4 heroes with +350 gold a day, 6 with +1 rare resource a day (2 sulfur, 2 crystal, 2 mercury), 2 Necromancy and 2 Logistics
  specialists.
- **No ore or lumber specialists** (b): their mines are common.
- **Heroes that lose their specialty** (c; about 50): get a specialty in their **first starting skill** (+5 % per hero level); its effect is
  built where it is missing (Armor, Treasury, War Tempo, …).
- **Drawn once** (d) into `heroes.csv` with a fixed seed (the same heroes every game), and listed in `hero-roster.md`.
- **Factions** (e): no two gold heroes and no two rare-resource heroes of one town, but a town may have one of each; all eight towns take
  part; any class.
- **Necromancy** (f): both specialists are Necropolis heroes: the mage slot that has Death attunement as its specialty today, and a warrior.
  **+5 points on top of the skill** (Basic 5 → 10 % … Master 20 → 25 %), stacking with the Necromancy Amplifier; only with the skill.
- **Logistics** (g): a flat **+10 % movement on land**, even without the Logistics skill. The other skill specialties (Melee, Archery,
  Tactics, the attunements) keep +5 % per hero level.
