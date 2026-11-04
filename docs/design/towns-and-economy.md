# Towns and economy

> Part of the [game design](overview.md). Everything here is built unless tagged *(planned)*; numbers are drafts. Every building with its
> cost, requirements and effect, the tech tree and each town's dwellings: [town-buildings.md](../content/town-buildings.md) (data:
> `src/Core/data/buildings.csv`). Open items ❓: [open-questions.md](../project/open-questions.md).

## Resources
**Six**: gold, lumber, ore, crystal, mercury, sulfur (ADR-019, `ResourceSet`). No gems. *Basic* = lumber, ore; *magic* = crystal, sulfur,
mercury.

## Town turn
ADR-050.
- **One building per town per day**. At any time on the owner's turn: build, recruit into the garrison or the visiting hero, upgrade stacks
  for the price difference, move stacks between garrison and visiting hero.
- New towns start with town hall 1, fort 1 and tavern 1; pre-built buildings keep the requirements. The town screen is 2D, in the 3D game too.

## Buildings
Each building's levels, cost, requirements and effect: [town-buildings.md](../content/town-buildings.md) (ADR-046). Rules kept here:
- **Capitol** (town hall 4, ADR-070, ADR-074): one per player; a second cannot be built, and a captured one drops to City Hall when the
  captor already has one.
- **Fort** levels raise [growth](#growth) and add [siege](combat.md#sieges) fortifications; every dwelling needs the Fort.
- **Mage guild** and spell buildings: [magic.md](magic.md#guilds-and-learning); Stronghold and Swamp Bestiary have the **war lodge**
  instead (warcries; town hall 2 needs it; ADR-112).
- **Necromancy Amplifier** (Necropolis, ADR-069, TOWN-6): +5 % Necromancy for its owner's heroes with the skill; more than one adds nothing;
  a captured one helps its new owner.
- **Per-town lists** (ADR-070, ADR-074, ADR-112): a town's own rows in `buildings.csv` replace the shared ones, add a building only it has,
  or (level 0) remove one. Every town gets a unique building *(planned, ADR-070)*.

## Garrison heroes
ADR-112, TOWN-8. The visiting hero may enter the garrison (its army joins it; refused if it does not fit) and leave with the **whole**
garrison. It is off the map and not counted towards the hero limit, but cannot leave while the player is at the limit. It earns, rests,
levels and governs like the others; in a siege it leads the garrison with its skills, spells and war machines (so a town keeps machines
through it) and cannot retreat or surrender.

## Creature tech tree
ADR-030, ADR-050.
- **5 tiers**. Each playable town offers **9 dwellings**: **2 alternatives at each of tiers 1–4**, **1 at tier 5**.
- A town builds **7 of the 9**, **at least one per tier**: the tier-5 dwelling, both alternatives at two tiers, one at the other two
  (e.g. T1 a+b, T2 a, T3 a+b, T4 b, T5). These **7 creature types** fill the hero's 7 army slots.
- **Final per town**: the other 2 are locked forever; dwellings can't be demolished. Another town of the same type may choose differently,
  and locked-out creatures still come from such towns and map dwellings.
- **Every playable-town creature has one upgrade** (HoMM3-style): the upgraded dwelling sells upgraded creatures and stacks upgrade in town
  for the price difference. The upgrade does not raise growth (ADR-046).
- **Neutral creatures** (and Conflux / Dragon Utopia) may lack upgrades; they use the same 1–5 tiers for guard strength, treasure value and
  random-map balance.

## Growth
- A new dwelling opens with one week of creatures; every Monday adds the creature's `growth`; unbought creatures accumulate.
- **Fort bonus** (ADR-066): +50 % with the Citadel, +100 % with the Castle, opening week included, rounded down (growth 1 → 1 / 2). Map
  dwellings never get it. One rule, `TownRules.WeeklyGrowth`, for service, screen and AI.
- **Growth buildings**: **+5 a week** to tier 1, **+2** to tier 2, for every dwelling of that tier; flat, added after the fort's and
  artifacts' percent; included in a new dwelling's opening week (ADR-073).

<a id="week-and-month-events-adr-073-built-adr-084"></a>
## Week and month events
ADR-073, ADR-084. Announced at the week's start; a month is 4 weeks. Rolled from `GameState.Random` when the week before starts (so the
Astrologer's Tower can tell it).

| Roll | Chance | Effect |
|---|---|---|
| Weekly (every week but a month's first) | 75 % | **Week of a creature**: its line's growth doubles that week |
| | 25 % | nothing |
| Monthly (a month's first week, instead) | 45 % | **Month of a creature**: growth doubles as above, and stacks of it spawn as guards next to random map objects |
| | 15 % | **Plague**: no growth; the creatures waiting in every town's dwellings are halved (rounded down) |
| | 15 % | **Starvation**: no growth |
| | 25 % | nothing; the weekly roll is made as usual |

- The doubling covers towns and map dwellings and includes the fort bonus. A dwelling built that week opens with one plain week. Plague
  halves only the towns' stock.
- **Spawned stacks**: one per 10 map objects that could take a guard (1 to 16), beside the entrance, count rolled by tier.
- **Settings** (map editor, template): no event in the first week by default; events can be turned off or skipped for up to **8 weeks**;
  which factions' creatures may be picked, separately for weekly and monthly (default: playable factions).

<a id="map-dwellings-and-caravans-implemented-adr-054"></a>
## Map dwellings and caravans
ADR-054, ADR-073, ADR-075, ADR-079.
- Map dwellings ([adventure-map.md](adventure-map.md#creature-dwellings)) grow every Monday, owned or not; a visiting hero flags one and
  recruits from it.
- A **caravan** (town building) hires from your dwellings or sends garrison troops to another own town; creatures travel virtually, unseen
  and unattackable.
- **Reach** **7500**, **speed 2500 movement points a day** (at most 3 days; TOWN-10), at the plain step cost (100, diagonals 141; terrain,
  roads, rivers, creature speed ignored). The town lists dwellings within the **raw** reach (rock, water, objects count; hostile heroes,
  monsters, guards don't); ordering needs a route within reach none of them blocks, checked before paying.
- **Limit** (TOWN-10): at most **6 caravans** per town, **8** with a governor, counting those on their way and waiting at the gates (troops
  between own towns count at the destination); orders at the limit are refused.
- **Resource caravans** (TOWN-9): fetch the store of an own flagged [resource provider](adventure-map.md#mines-and-resource-providers) within
  reach for **100 gold**; same route rules and limit; resources join the treasury on arrival.
- A blocked route sends a caravan back (refund; resources go back to the store); a captured destination scatters the creatures; at a full
  garrison a caravan waits at the gates.
- The caravan screen shows the town's **queue**: incoming caravans (fewest days left first), then those waiting; outgoing ones apart.

<a id="tavern-implemented-adr-060"></a>
## Tavern
ADR-060, ADR-070, ADR-081.
- A hero costs **2500 gold** and appears on the town's entrance (must be free). A tavern offers **2**, the Brotherhood (level 2) **4**.
- **Hero limit** (ADR-068, ADR-072): at most **8 heroes** a player, or the map's lower limit (1–8, also per player on campaign-like maps).
  Only heroes on the map count: not prisoners, garrison heroes, or heroes waiting in a tavern.
- **Offers**: a pool per player and town faction, reshuffled weekly (same offers in a player's towns of one faction); never heroes on the
  map. Ranked by the [ring](../content/lore.md): native, adjacent, not opposing, opposing (Stronghold, Swamp Bestiary: native, warriors,
  others); an empty rank falls through:

  | Tavern | Ring towns | Stronghold, Swamp Bestiary |
  |---|---|---|
  | Tavern | native + adjacent (either neighbour) | native + warrior |
  | Brotherhood | 2 native + 2 adjacent | 2 native + 2 warriors |

- A hired hero gets a **starter stack**: one week's growth of its preferred creature.
- A hero who **retreats or surrenders** waits in its player's taverns as it was (level, skills, artifacts; its army only after a surrender)
  and is offered **first** in all its player's towns until hired back; other players don't see it.
- The [map tavern](adventure-map.md#utilities-and-travel) has its own pool.

<a id="thieves-guild-adr-077"></a>
## Thieves' Guild
ADR-077, TOWN-11. The tavern ranks the player against every other player still in the game (computers and teammates too); equal values share
a place. Rows grow with the player's **taverns**: 1 — towns, heroes; 2 — + gold, lumber and ore, rare resources; 3 — + artifacts, income;
4+ — + army strength, best hero (highest level), best creature (most valuable). The Den of Thieves shows the whole table.

## Kingdom overview
ADR-077, ADR-084. One screen listing every income source (towns, mines, providers, artifacts, skills, specialties) with its daily or weekly
amount and the totals, read from the income rules, so it matches what the turn pays.

## Governors
ADR-063. A hero with Stewardship can govern one own town: its buildings cost 5 / 10 / 15 % less and its creatures (recruiting and upgrading)
10 / 15 / 20 % less, rounded up per resource. Map dwellings use the recruiting hero's level, caravans the destination town's governor. A
governed town counts 1.5 towards the marketplace rates.

<a id="marketplace-implemented-adr-067"></a>
## Marketplace
ADR-067. Gold is traded against one resource at a time. **X** = the player's towns with a marketplace (a governed one 1.5); factor =
`min(X + 1, 10)`.

| | Basic | Magic |
|---|---|---|
| Buy 1 unit (gold) | 5000 / factor, rounded up | 10 000 / factor, rounded up |
| Sell 1 unit (gold) | floor(12.5 × factor) | floor(25 × factor) |
| One marketplace (buy / sell) | 2500 / 25 | 5000 / 50 |
| Nine or more | 500 / 125 | 1000 / 250 |

- **No trade cap.** The **player** trades (a UI button or the town's marketplace), not a hero; unavailable without a marketplace town. Buying
  is far dearer than selling on purpose: trading is a way out of a shortage, not income. The AI does not trade.
- **Map marketplace**: a visiting hero trades for its owner as if X were 4 (factor 5): basic 1000 / 62, magic 2000 / 125; for any player,
  also without a marketplace town. Generator placement: [random-maps.md](random-maps.md#pipeline) (TOWN-2).

## Income
Paid at the owner's turn start. **No base income** (ADR-025): only sources pay, each an `IIncomeSource` summed by
`EconomyService.DailyIncome`: [mines and providers](adventure-map.md#mines-and-resource-providers), town buildings (town hall, Resource
Silo), the Treasury and Prospecting skills (ADR-062), resource specialties and artifacts (`HeroIncome`, ADR-061).

## Starting resources
ADR-063. The game's **complexity** (`GameOptions.Complexity`; none = the map's own) sets each player's start: Beginner 10000 gold, 30 lumber,
30 ore, 10 of each rare; Easy 7500, 20, 20, 6; Normal 5000, 15, 15, 3; Hard 3500, 12, 12, 1; Expert 2500, 10, 10, none. Only Expert is the
designer's set; the others are guesses.
