# Towns and economy

> Part of the [game design](overview.md). Full list of buildings, the tech tree and every town's dwellings:
> [town-buildings.md](../content/town-buildings.md). Open items ❓: [open-questions.md](../project/open-questions.md).

## Resources
**Six resources**: gold, lumber, ore, crystal, mercury, sulfur (ADR-019, `ResourceSet`). No gems.

## Town turn (implemented, ADR-050)
- One building per town per day; the building list is one table, `data/buildings.csv`, shared by all towns for now
  (per-town lists ❓). New towns start with town hall 1, fort 1 and tavern 1; pre-built buildings keep the
  requirements. The town screen is 2D in the first iteration, in the 3D game too.
- At any time on the owner's turn: build, recruit into the garrison or the visiting hero, upgrade stacks for the price
  difference, and move stacks between garrison and visiting hero.

## Buildings (draft numbers, ADR-046; numbers live in `buildings.csv`)

| Building | Levels | Effect | Status |
|---|---|---|---|
| Town hall | 3 | 500 / 1000 / 2000 gold a day | implemented; a fourth level ❓ |
| Fort | 3 | Fort: needed for every dwelling; Citadel / Castle: town dwellings grow +50 % / +100 %. Sieges: wall and gate, then keep, then towers (ADR-066) | dwellings and growth implemented; fortifications wait for sieges (post-1.0) |
| Blacksmith | 1 | Sells the healing tent and ammo cart (ADR-056) | implemented |
| Marketplace | 2 | Level 2 adds 1 ore and 1 lumber a day; trading: gold against resources, rates by marketplace towns (ADR-067) | income and trading implemented (ADR-067) |
| Mage guild | 4 | Spells by level (ADR-047, ADR-050) | built, no effect until magic (Phase 5) |
| Tavern / Brotherhood | 2 | Hires heroes: 2 / 4 offered (ADR-060) | implemented |
| Caravan | 1 | Hires from map dwellings, sends troops (ADR-054); 1000 gold + 5 lumber, needs town hall 2 (placeholder) | implemented |
| Dwellings | 9 + upgrades | Build 500 × tier gold, upgrade 1000 × tier, plus lumber, ore, mercury and crystal by tier | implemented |

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
Map dwellings keep the creature's growth. One rule, `TownRules.WeeklyGrowth`, serves the service, town screen and AI
(ADR-066).

## Map dwellings and caravans (implemented, ADR-054)
- Dwellings of one creature on the adventure map grow every Monday, owned or not; a visiting hero flags one and
  recruits from it.
- A **caravan** (town building) hires creatures from your dwellings: they travel virtually to the town at their map
  speed along a route no hostile hero, monster or guarded tile blocks, and a dwelling more than 5 days away cannot
  supply it; both are checked before paying.
- A blocked route sends the caravan back (refund), a captured destination scatters the creatures, and caravans cannot
  be seen or attacked. Caravans also send garrison troops to another own town.

## Tavern (implemented, ADR-060)
- A hero costs **2500 gold** and appears on the town's entrance (which must be free). A player has at most **8 heroes**.
- A tavern offers **2** heroes, the Brotherhood (level 2) **4**. Offers come from a per-player pool, reshuffled every
  week, ranked by the town: on the ring of towns ([lore.md](../content/lore.md)) native, adjacent, not opposing,
  opposing; Stronghold and Swamp Bestiary rank native, warriors, everyone else.

  | Tavern | Ring towns | Stronghold, Swamp Bestiary |
  |---|---|---|
  | Tavern | native + adjacent | native + warrior |
  | Brotherhood | 2 native + adjacent + not opposing | 2 native + 2 warriors |

- Heroes on the map are never offered. Surrender (paying to leave a battle) is still open ❓.

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

It works for any player, also one without a marketplace town. Open: [TOWN-2](../project/open-questions.md#town-2). [TOWN-2](../project/open-questions.md#town-2).

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
