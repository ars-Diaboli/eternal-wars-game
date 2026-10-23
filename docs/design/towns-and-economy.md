# Towns and economy

> Part of the [game design](overview.md). Full list of buildings, the tech tree and every town's dwellings:
> [town-buildings.md](../content/town-buildings.md). Open items ❓: [open-questions.md](../project/open-questions.md).

## Resources
**Six resources**: gold, lumber, ore, crystal, mercury, sulfur (ADR-019, `ResourceSet`). No gems.

## Town turn (implemented, ADR-050)
- One building per town per day; the building list is one table, `data/buildings.csv`, shared by all towns for now.
  ADR-070 (not built): towns get **their own building lists and names**; `buildings.csv` gains a town column (like
  `creatures.csv`) where designers set names, costs and requirements, and every town later gets a unique building. New towns
  start with town hall 1, fort 1 and tavern 1; pre-built buildings keep the requirements. The town screen is 2D in the first
  iteration, in the 3D game too.
- At any time on the owner's turn: build, recruit into the garrison or the visiting hero, upgrade stacks for the price
  difference, and move stacks between garrison and visiting hero.

## Buildings (draft numbers, ADR-046; numbers live in `buildings.csv`)

| Building | Levels | Effect | Status |
|---|---|---|---|
| Town hall | 3 | 500 / 1000 / 2000 gold a day | implemented; a fourth level, the **Capitol**, decided (ADR-070, below) |
| Fort | 3 | Fort: needed for every dwelling; Citadel / Castle: town dwellings grow +50 % / +100 %. Sieges: wall and gate, then keep, then towers (ADR-066) | dwellings and growth implemented; fortifications wait for sieges (post-1.0) |
| Blacksmith | 1 | Sells the healing tent and ammo cart (ADR-056) | implemented |
| Marketplace | 2 | Level 2 adds 1 ore and 1 lumber a day; trading: gold against resources, rates by marketplace towns (ADR-067) | income and trading implemented (ADR-067) |
| Mage guild | 4 | Spells of the town's own school: **3** each at levels 1 and 2, **2** each at levels 3 and 4 (ADR-071, MAG-2); sells a **spell book for 500 gold**; a hero in the town refills its mana (ADR-071, MAG-3); level 4 stays a placeholder (ADR-070) | built, no effect until magic (Phase 5) |
| Extra spell buildings | 2 | Add spells of **other schools**, chosen by the magic circle (HoMM4-like; details MAG-12) (ADR-071) | planned |
| Warcry / totem building | 3 | **Stronghold and Swamp Bestiary** instead of a mage guild: teaches warcries / spiritual totems (placeholder, ADR-071, MAG-2) | planned |
| Growth buildings | 1 each | Two per town (placeholders): one adds **+5 a week** to tier-1 growth, the other **+2** to tier-2 (ADR-071, TOWN-1) | planned |
| Tavern / Brotherhood | 2 | Hires heroes: 2 / 4 offered (ADR-060); Brotherhood **1000 gold + 5 ore** (ADR-070) | implemented; new price and pools not built |
| Caravan | 1 | Hires from map dwellings, sends troops (ADR-054); 1000 gold + 5 lumber, needs town hall 2 (placeholder) | implemented |
| Dwellings | 9 + upgrades | Build 500 × tier gold, upgrade 1000 × tier, plus lumber, ore, mercury and crystal by tier | implemented |
| Necromancy Amplifier | 1 | Necropolis only: +5 % Necromancy for every hero of its owner with the skill (ADR-069); **1500 gold + 5 mercury**, needs mage guild 1; a captured one helps its new owner (ADR-070, TOWN-6) | planned |

- **Capitol** (town hall 4, ADR-070, not built): one per player; capturing a town with a Capitol when the player already has
  one downgrades it to town hall 3; town hall 3 is renamed City Hall (ADR-073). Its income and cost are not set yet.

## Creature tech tree (implemented, ADR-030, ADR-050)
- Creatures have **5 tiers** (not HoMM3's 7).
- Each playable town offers **9 creature dwellings**: **2 alternatives at each of tiers 1–4** and **1 at tier 5**.
- A town may build **7 of the 9**, with **at least one dwelling per tier**. Since tier 5 has only one,
  every town ends up with the tier-5 dwelling, and **both** alternatives at two of tiers 1–4 but
  **one** at the other two. Example: T1 a+b, T2 a, T3 a+b, T4 b, T5. These **7 creature types** fill
  exactly the hero's 7 army slots.

  | Tier | Dwellings offered | Must build | Can build |
  |---|---|---|---|
  | 1–4 | 2 alternatives each | ≥ 1 | 1 or 2 |
  | 5 | 1 | 1 | 1 |
  | **Total** | **9** | **5** | **7** |

- **The choice is final, per town.** Once 7 are built, the remaining 2 are locked forever in that
  town. Dwellings can't be demolished. Another town of the same type may choose differently.
- **All creatures of playable towns are upgradeable**, one upgrade per dwelling (HoMM3-style). An upgraded dwelling
  produces upgraded creatures, and existing stacks can be upgraded in that town for the price difference. The upgrade
  does not raise growth (ADR-046); the fort does (Citadel, Castle; ADR-066).
- **Locked-out creatures are still obtainable** from map dwellings and from other towns of the same type that built
  them. The town choice shapes your core army; it is not a hard ban.
- **Neutral creatures** (and the non-playable Conflux / Dragon Utopia) **may** lack upgrades. They use the same 1–5
  tier scale, which is used for guard strength, treasure value and random-map balancing.

## Growth (implemented)
A new dwelling opens with one week of creatures, every Monday adds the creature's `growth`, and unbought creatures
accumulate. Every dwelling needs the Fort. A town's dwellings grow **+50 % with the Citadel** (fort 2) and **+100 % with
the Castle** (fort 3), opening week included; the bonus is rounded down (growth 1 → 1 with a Citadel, 2 with a Castle).
Map dwellings keep the creature's growth. Week and month events change growth ([below](#week-and-month-events-adr-073-not-built)). One rule, `TownRules.WeeklyGrowth`, serves the service, town screen and AI
(ADR-066).

**Growth buildings** (ADR-071, TOWN-1; placeholders, not built): two buildings per town, one adding **+5 a week** to tier-1
growth, the other **+2** to tier-2, whether the town has one or two dwellings of that tier; each dwelling of the tier gets the
bonus (ADR-073).

## Week and month events (ADR-073, not built)
HoMM3-like, announced at the start of the week; a month is 4 weeks. Rolls use `GameState.Random`; numbers are drafts.

| Roll | Chance | Effect |
|---|---|---|
| Weekly (every week but a month's first) | 75 % | **Week of a creature**: its line's weekly growth doubles (base or upgrade picked, the line counts) |
| | 25 % | nothing |
| Monthly (a month's first week, instead of the weekly roll) | 45 % | **Month of a creature**: growth doubles as above, and stacks of it spawn next to random map objects as their guards |
| | 15 % | **Plague**: no growth that week; the creatures waiting in every town's dwellings are halved (rounded down) |
| | 15 % | **Starvation**: no growth that week |
| | 25 % | nothing *(assumed: the weekly roll is made as usual)* |

- *(Assumed)*: the doubling applies to towns and map dwellings for that one week; no event in the game's first week.
- The map editor and the template editor choose which factions' creatures may be picked, separately for weekly and monthly
  events; default: the playable factions only.

## Map dwellings and caravans (implemented, ADR-054)
- Dwellings of one creature on the adventure map grow every Monday, owned or not; a visiting hero flags one and
  recruits from it.
- A **caravan** (town building) hires creatures from your dwellings: they travel virtually to the town at their map
  speed along a route no hostile hero, monster or guarded tile blocks, and a dwelling more than 5 days away cannot
  supply it; both are checked before paying.
- A blocked route sends the caravan back (refund), a captured destination scatters the creatures, and caravans cannot
  be seen or attacked. Caravans also send garrison troops to another own town.
- At a full garrison a caravan waits at the gates until there is room.
- **ADR-073 (TOWN-5, not built)**:
  - The reach is checked twice. When a dwelling is flagged (and when a town gets a caravan), the **raw distance** — travel days at
    the creature's speed, ignoring hostile heroes, monsters and guards *(assumed)* — decides whether the dwelling appears in that
    town's caravan list at all; ordering then runs the full check as today.
  - The caravan screen shows a **queue**: caravans on their way (creature, count, days left) and those waiting for room.
  - Map dwellings sell base creatures only ([adventure-map.md](adventure-map.md#creature-dwellings)).

## Tavern (implemented, ADR-060)
- A hero costs **2500 gold** and appears on the town's entrance (which must be free). A player has at most **8 heroes**, or the map's lower limit (ADR-068).
- A tavern offers **2** heroes, the Brotherhood (level 2) **4**. Offers come from a per-player pool, reshuffled every
  week, ranked by the town: on the ring of towns ([lore.md](../content/lore.md)) native, adjacent, not opposing,
  opposing; Stronghold and Swamp Bestiary rank native, warriors, everyone else.

  | Tavern | Ring towns | Stronghold, Swamp Bestiary |
  |---|---|---|
  | Tavern | native + adjacent | native + warrior |
  | Brotherhood | 2 native + adjacent + not opposing | 2 native + 2 warriors |

- Heroes on the map are never offered. A hero who **surrenders** returns to the tavern pool (ADR-070,
  [combat.md](combat.md#leaving-a-battle)).
- **Who counts towards the hero limit** (ADR-072, TOWN-8): heroes on the map. A hero in a **prison** belongs to nobody and does
  not count. A hero in a **town's garrison** (once heroes can stand there) does not count either, but cannot leave the garrison
  while the player is at the limit.
- **ADR-070 (not built)**:
  - The pool becomes **per player and faction**: a town offers its native and adjacent heroes, and the player's towns of one
    faction share that pool. The basic tavern offers **1 native + 1 adjacent**, the adjacent hero from either ring neighbour at
    random; the Brotherhood **2 native + 2 adjacent** (ADR-072, TOWN-7). Stronghold and Swamp Bestiary, off the ring, keep
    "native + a warrior of another town" (ADR-073).
  - The Brotherhood costs **1000 gold + 5 ore** (was 2000 gold + 5 lumber + 5 ore).
  - A hired hero gets a **starter stack** that follows its preferred creature.
  - The **map tavern** works like a Brotherhood ([adventure-map.md](adventure-map.md#utilities-and-travel)) with **its own
    weekly pool of 4 random heroes of any faction**, shared by all players (ADR-071, TOWN-4).

## Governors (implemented, ADR-063)
A hero with Stewardship can govern one own town. With a governor, that town's buildings cost 5/10/15 % less and its
creatures (recruiting and upgrading) 10/15/20 % less, rounded up per resource. Map dwellings use the recruiting hero's
level, caravans the destination town's governor. A governed town counts 1.5 instead of 1 towards the marketplace rates (ADR-067, replacing the former 150 % value).

## Marketplace (implemented, ADR-067)
Gold is traded against one resource at a time. Basic resources are lumber and ore, magic resources crystal, sulfur and mercury.
**X** is the number of the player's towns with a marketplace, a governed one counting 1.5; the factor is `min(X + 1, 10)`.

| | Basic | Magic |
|---|---|---|
| Buy 1 unit (gold) | 5000 / factor, rounded up | 10 000 / factor, rounded up |
| Sell 1 unit (gold) | floor(12.5 × factor) | floor(25 × factor) |
| With one marketplace (buy / sell) | 2500 / 25 | 5000 / 50 |
| With nine or more (buy / sell) | 500 / 125 | 1000 / 250 |

There is **no trade cap**. The **player** trades, not a hero: from a button in the UI or by clicking the marketplace in a town
(unavailable while the player has no town with a marketplace). Buying is far dearer than selling, deliberately, so trading is a way out of a shortage, not a source of income;
a single marketplace is meant to be expensive and unfair. The AI does not trade for now.

**Map marketplace**: an adventure-map object ([adventure-map.md](adventure-map.md)). A visiting hero opens the trading screen for its owner at a fixed rate, as if X were 4 (factor 5):

| | Basic | Magic |
|---|---|---|
| Buy 1 unit (gold) | 1000 | 2000 |
| Sell 1 unit (gold) | 62 | 125 |

It works for any player, also one without a marketplace town. The generator places one only from the **4000** treasure band;
that threshold stays a placeholder to tune (ADR-070, TOWN-2). Nothing about trading is open.

## Income
Paid at the start of the owner's turn. **There is no base or placeholder income** (ADR-025): players earn only what
their sources produce. Each source is a pluggable `IIncomeSource`, and `EconomyService.DailyIncome` sums them.
- **Mines**: gold 1000, lumber/ore 2, rare resources 1.
- **Towns**: town hall 500 / 1000 / 2000 gold; marketplace level 2 adds 1 ore and 1 lumber.
- **Hero skills**: Treasury and Prospecting (Estates group, ADR-062).
- **Hero specialties**: resource specialties pay +N of a resource per day (`HeroIncome`).
- **Artifacts**: daily resources through `HeroIncome` (ADR-061).

## Starting resources (implemented, ADR-063)
The game's **complexity** (`GameOptions.Complexity`; none = the map's own resources) gives every player its starting
resources: Beginner 10000 gold, 30 lumber, 30 ore, 10 of each rare; Easy 7500, 20, 20, 6; Normal 5000, 15, 15, 3;
Hard 3500, 12, 12, 1; Expert 2500, 10, 10, none. Only Expert is the designer's set; the others are guesses to tune.
