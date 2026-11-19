# Decisions: Towns, economy and factions

**Covers**: Resources and income, town groups and rosters, creatures, buildings and the tech tree, dwellings and caravans, unique buildings.

**Current rules**: [docs/design/towns-and-economy.md](../../docs/design/towns-and-economy.md), [docs/design/factions.md](../../docs/design/factions.md), [docs/content/town-buildings.md](../../docs/content/town-buildings.md), [docs/content/lore.md](../../docs/content/lore.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [010](#adr-010-town-roster-in-four-groups-two-non-playable-towns) | Town roster in four groups; two non-playable towns | accepted |
| [019](#adr-019-six-resources) | Six resources | accepted |
| [025](#adr-025-income-comes-only-from-sources-no-base-income) | Income comes only from sources; no base income | accepted |
| [028](#adr-028-town-groups-are-lore-and-campaign-only) | Town groups are lore and campaign only | accepted |
| [029](#adr-029-town-rivalries-do-not-affect-combat) | Town rivalries do not affect combat | accepted |
| [030](#adr-030-five-creature-tiers-choose-7-of-9-dwellings) | Five creature tiers, choose 7 of 9 dwellings | accepted |
| [037](#adr-037-creatures-as-data-two-speeds-draft-slice-rosters) | Creatures as data; two speeds; draft slice rosters | accepted |
| [044](#adr-044-all-town-rosters-the-creature-table-is-the-source-of-truth) | All town rosters; the creature table is the source of truth | accepted |
| [046](#adr-046-towns-on-the-map-and-the-building-list-from-the-sandbox) | Towns on the map and the building list (from the sandbox) | accepted |
| [049](#adr-049-roster-revision-from-the-design-notes) | Roster revision from the design notes | accepted |
| [050](#adr-050-towns-in-play-buildings-table-town-turn-garrisons-and-capture) | Towns in play: buildings table, town turn, garrisons and capture | accepted |
| [054](#adr-054-map-dwellings-and-caravans) | Map dwellings and caravans | accepted |
| [062](#adr-062-treasury-interest-and-prospecting-piles-2026-10-17) | Treasury interest and Prospecting piles (2026-10-17) | accepted; partly superseded |
| [066](#adr-066-what-the-fort-does-2026-10-17) | What the fort does (2026-10-17) | accepted |
| [067](#adr-067-marketplace-rates-leadership-a-defense-initiative-skill-the-primal-fury-group-2026-10-18) | Marketplace rates, Leadership, a defense initiative skill, the Primal Fury group (2026-10-18) | accepted; partly superseded |
| [074](#adr-074-size-pass-per-town-building-rows-and-the-capitol-implementation-2026-10-23) | Size pass, per-town building rows and the Capitol (implementation, 2026-10-23) | implemented |
| [112](#adr-112-growth-buildings-the-war-lodge-banks-per-faction-garrison-heroes-template-connection-options-adr-064-adr-068-adr-070-adr-071-adr-106-2026-11-02) | Growth buildings, the war lodge, banks per faction, garrison heroes, template connection options (ADR-064, ADR-068, ADR-070, ADR-071, ADR-106, 2026-11-02) | accepted |
| [130](#adr-130-the-grail-buildings-more-unique-buildings-stay-open-town-12-adr-126-2026-11-05) | The Grail buildings; more unique buildings stay open (TOWN-12, ADR-126, 2026-11-05) | accepted |
| [135](#adr-135-roster-revision-renames-two-swaps-classes-a-and-b-necromancy-by-class-cont-2-adr-072-2026-11-05) | Roster revision: renames, two swaps, classes A and B, Necromancy by class (CONT-2, ADR-072, 2026-11-05) | implemented |
| [137](#adr-137-the-undead-transformer-town-13-adr-135-2026-11-05) | The Undead Transformer (TOWN-13, ADR-135, 2026-11-05) | accepted |
| [138](#adr-138-the-first-set-of-unique-buildings-built-adr-126-2026-11-05) | The first set of unique buildings built (ADR-126, 2026-11-05) | implemented |
| [139](#adr-139-the-university-and-the-war-academy-built-adr-126-2026-11-05) | The University and the War Academy built (ADR-126, 2026-11-05) | implemented |
| [143](#adr-143-the-academys-observatory-the-undead-transformer-built-adr-137-adr-138-2026-11-06) | The Academy's Observatory; the Undead Transformer built (ADR-137, ADR-138, 2026-11-06) | implemented |


### ADR-010: Town roster in four groups; two non-playable towns
**Status**: accepted (2026-10-15); no `Group`/`IsPlayable` field exists: `BuiltInMapObjects.PlayableTowns` lists the eight playable towns
Playable: Haven, Sylvan, Academy | Necropolis, Inferno, Dungeon | Stronghold, Swamp Bestiary.
Non-playable (campaign/neutral): Conflux, Dragon Utopia. Balancing effort goes to the 8 playable towns
only. Data model: a town definition carries `Group` and `IsPlayable` so map setup, random maps and the
AI can filter on them.

### ADR-019: Six resources
**Status**: accepted (2026-10-15)
Gold, lumber, ore, crystal, mercury, sulfur. No gems. Rare resources are crystal, mercury and sulfur.

### ADR-025: Income comes only from sources; no base income
**Status**: accepted (2026-10-15); income artifacts built (ADR-061)
The placeholder 500 gold per turn was removed. Daily income is the sum of pluggable `IIncomeSource`s
(`MineIncome`, `TownIncome`, `HeroIncome` for Estates skills and resource specialties; artifacts later), collected at
the start of the owner's turn. Nothing is published when the income is zero. New income features add a
source instead of editing `EconomyService`.

### ADR-028: Town groups are lore and campaign only
**Status**: accepted (2026-10-15); native terrain per town is now ADR-045 (not ADR-024); no `Group` field exists yet
(`BuiltInMapObjects.PlayableTowns` lists the eight playable towns)
The four town groups carry no gameplay rules in standard or random-map games: no automatic alliances,
shared skills, diplomacy or group bonuses. Teams come from game setup (ADR-027); native terrain is
per town (ADR-024). Groups are used by the campaign (story, scripted alliances and events) and as
content filters (setup screens, random map templates). `Group` stays on the town definition as metadata.

### ADR-029: Town rivalries do not affect combat
**Status**: accepted (2026-10-15)
Rivalries such as Haven vs Necropolis are story and campaign material only. The combat engine has no
hate/enmity bonuses and no alignment or group modifiers. Creature abilities key off creature
*properties* (tags such as `Undead`, `Flying`, `Large`), never off town or group identity. This keeps
balance independent of pairings and lets any town be designed and balanced on its own.

### ADR-030: Five creature tiers, choose 7 of 9 dwellings
**Status**: accepted (2026-10-15); open details in [towns-and-economy.md](../../docs/design/towns-and-economy.md); a town's dwellings
derive from its creature lines in `creatures.csv`, not a town definition list (ADR-044, ADR-050)
Creatures have 5 tiers. Every playable town offers 9 dwellings: 2 alternatives at each of tiers 1–4
and 1 at tier 5. A town builds at most 7, at least one per tier, giving 7 creature types to match 7 army
slots, and making town builds a strategic choice. All playable-town creatures are upgradeable;
neutrals and the non-playable towns may not be. Data model: a town definition lists dwellings
`(tier, creature, upgradedCreature?)`. Build validation enforces the limits so the AI, the editor
(pre-built towns) and random maps share one rule. The constraint is "≤ 7 built, ≥ 1 per tier as the
goal": a town can be mid-build with fewer.
- Final per town: no demolition; the unbuilt alternatives are locked once 7 are built. The build
  validator must also refuse a build that would leave a tier impossible to fill. Example: with 6
  built and tier 3 still empty, only a tier-3 dwelling may be built.
- One upgrade building per dwelling; stacks can be upgraded in a town with that upgrade.
- Creatures from unbuilt dwellings remain available from map dwellings and other towns.
- Neutral creatures share the 1–5 tier scale (`CreatureDefinition.Tier`); `Upgrade` is optional.

### ADR-037: Creatures as data; two speeds; draft slice rosters
**Status**: accepted (2026-10-15); roster numbers are a draft for review; `NoMeleePenalty` replaced by opt-in `MeleePenalty`
(ADR-042); stats moved to `creatures.csv` (ADR-044)
- `CreatureDefinition` (data): tier 1–5, size class (Small 2×2, Medium 3×3, Large 4×4), Attack, Defense,
  damage range, health, Initiative, shots, growth, cost, optional `UpgradesTo`, free-form tags (ADR-029) and
  abilities (`Flying`, `DoubleStrike`, `NoEnemyRetaliation`, `UnlimitedRetaliation`, `NoMeleePenalty`,
  `Breath`, `Cleave`, `MultiHead`, plus the hook abilities `PackHunter` (engagement) and `Relentless` (hit
  history)). `CreatureCatalog` and `CreatureDefinitionSerializer` follow the map-object pattern (built-ins
  plus JSON overrides).
- **Two speeds** (user decision): `Speed` is battlefield cells per turn; `MapSpeed` is the HoMM3-scale value
  the adventure movement table uses (`MovementAllowance`, slowest creature of the army). This replaces the
  `Hero.SlowestCreatureSpeed` placeholder. `MovementRules.ArmyTown` returns the army's town when every creature
  shares one, which activates native terrain (ADR-032).
- The Haven and Inferno rosters (`BuiltInCreatures`) come from the reference project, rearranged into 9 lines
  over 5 tiers, all upgradeable. Haven: footman, pikeman | zealot, archer | monk, griffin | lancer, paladin |
  radiant spirit. Inferno: imp, horned demon | hell hound, tormentor | succubus, hell charger | pit fiend,
  juggernaut | devil. Neutrals: wolf, bandit, troll, wyvern, and a Dragon Utopia red dragon. Stats are a first
  pass to tune with `sim battle`.
- `Value` (health × average damage × (20 + Attack + Defense) / 40, ×1.5 for shooters, ×1.2 for flyers) is the
  shared strength estimate of the battle AI and neutral dispositions.

### ADR-044: All town rosters; the creature table is the source of truth
**Status**: accepted (2026-10-16); stats are a first balance pass for review; "Dark and Grim Raiders shoot" reversed by ADR-049
- **Rosters** for all eight playable towns (9 lines over 5 tiers, all upgradeable), Conflux, Dragon Utopia and
  neutrals, taken from the sandbox (`reference/ew`, `EW.CoreLib.Data/Monsters`). Only Haven and half of Academy had
  real numbers there; the rest were copy-pasted placeholders (all 10s, the class defaults, or Seraph copies), and
  Bestiary, Conflux and Dragon Utopia were name lists. Corrections against the sandbox: Academy Iron Golem upgrades
  to Animated Armor and tier 3 is Sorcerer/Mage + Snow Maiden (Thane was commented out and is now a neutral);
  Stronghold's Roc is tier 4 next to Cyclops, a Wolf Rider line fills tier 3 and the Mammoth is a neutral; Dark
  and Grim Raiders shoot, as in both sandbox copies; the sandbox's copy-paste flags (flying Cyclops and Behemoth,
  shots on Djinn and Naga, Pegasus without flying, Skeleton named "Devil") are not kept. Lore neutrals the
  sandbox had outside its live roster are in: Timber/Dire Wolf, Specter/Banshee, Thane, Gog/Magog, Breeder/Fel
  Breeder. The sandbox's Ballista and Flamethrower became war machines (ADR-056).
- **One table, no stats in code.** Every creature is one row of `src/Godot/EternalWars.Godot.Assets/data/creatures.csv`
  (`CreatureTable`: header-named columns in any order, comma/semicolon/tab, list cells with `;`, a free `notes`
  column). Core embeds the file as `CreatureCatalog.Default` (tests, simulator, default `GameState`);
  `BuiltInCreatures` keeps only the ids code refers to. The Godot apps read the file at start-up
  (`GameContent.Creatures`, `--creatures=path.csv` overrides), so a designer edits it and restarts: no build, and
  no import because `creatures.csv.import` uses Godot's `keep` importer (a `.csv` would otherwise be imported as
  translations). A broken table logs the line and column and falls back to the embedded copy.
- **Balance method.** Each line was scaled (health and damage together, so attack, defense, speed and
  capabilities keep their character) towards a per-tier weekly power target adjusted for its capabilities, then
  tuned with the simulator until lines win about half their duels within the tier (`sim lines`) and one-week town
  armies win 40–60 % against each other (`sim towns`, which rotates the four seven-dwelling builds that keep both
  lines of tiers 1–2). Gold is per tier per week (about 1000 / 1650 / 2250 / 3300 / 3300), split by growth;
  upgrades cost +30 % and are about 1.3–1.45× as strong. Neutrals sit a little below town lines; Dragon Utopia
  dragons above town tier 5, the black dragon about twice the others. `sim creatures` lists each line's value,
  weekly power against its tier and gold per value.
- Replays depend on the creature table like on any content: a record made with other stats fails its digest.

### ADR-046: Towns on the map and the building list (from the sandbox)
**Status**: accepted (2026-10-16) for the footprint and dwelling upgrades; the building list is a draft for Phase 3,
amended by ADR-050 (mage guild level 4), ADR-054 (caravan) and ADR-060 (Brotherhood); the numbers live in `buildings.csv`
- **Footprint**: a town is 7×7 with the four corners free (44 blocked cells plus the entrance) and its entrance on the bottom-middle
  cell, the same convention as mines (`BBB`/`BVB`):
  ```
  .BBBBB.
  BBBBBBB
  BBBBBBB
  BBBBBBB
  BBBBBBB
  BBBBBBB
  .BBVBB.
  ```
  The sandbox core used this mask but put the entrance inside the blocked area; its 3D client used a full 7×7 block
  entered from outside. Its mines are 3×2 like ours.
- **Dwelling upgrades produce the upgraded creature** (ADR-030 stands). The sandbox's second dwelling level, which
  raised growth by 50 %, is not adopted; growth bonuses come from other buildings.
- **Draft building list** (sandbox `TownTechTreeBase`; gold / lumber / ore unless noted; the sandbox's gems are read
  as mercury):

  | Building | Levels and costs | Requires | Effect |
  |---|---|---|---|
  | Town hall | 2500/5/5, 5000/10/10, 10000/20/20 | L2: blacksmith, marketplace, mage guild; L3: fort L2 | 500 / 1000 / 2000 gold a day |
  | Fort | 1500/5/5, 3000/10/10, 6000/15/15 | L1: tavern | defence, growth bonus ❓ |
  | Blacksmith | 1000/5/5 | – | sells war machines (ADR-056) |
  | Marketplace | 1250/5 lumber, 2500/5/5 | – | L2: +1 ore and +1 lumber a day; trading ❓ |
  | Mage guild | L1 2000 + 5 lumber + 2 mercury, L2 3500 + 3 crystal + 3 mercury, L3 5000 + 5 crystal + 5 sulfur, L4 ❓ | – | spells of that level (ADR-047) |
  | Tavern | 750/5 lumber | – | hire heroes |
  | Dwelling of tier t | build 500·t gold, upgrade 1000·t; + t lumber (t ≥ 2), + t ore (t ≥ 3), + 1 mercury (t ≥ 4), + 1 crystal (t = 5); the upgrade doubles the resources | – | weekly growth of its creature |
- In the sandbox towns start without buildings; one build per day; 7 of 9 dwellings with every tier covered (as
  ADR-030). Its weekly growth per dwelling (12, 10 / 8, 7 / 5, 4 / 3, 2 / 1) differs from the creature table's
  `growth`; the creature table rules (ADR-044).

### ADR-049: Roster revision from the design notes
**Status**: accepted (2026-10-16), from [ideas.md](../ideas.md); amends ADR-044's roster
- **Wyvern** moves to Dragon Utopia (`dragon-utopia.wyvern`, was `neutral.wyvern`) and upgrades to the new **Wyvern
  Monarch** (Flying, Relentless 10 for its venom).
- **Dragonyte** swaps Breath for **Cleave**.
- New neutrals: **Efreet** (tier 4, elemental and demon; Flying, fire immunity, one fireball), the demonic
  counterpart of the Djinn Sultan. It is a separate neutral and not a second Djinn upgrade, because a line has one
  upgrade and an upgrade shares its town (ADR-030). **Golden Golem** (tier 3 construct; mind immunity, spell
  resistance 2).
- **Dark and Grim Raiders** are melee cavalry: the sandbox's crossbow shots are dropped (reverses that point of
  ADR-044). Health was raised to 56 / 64 to keep the line's duel results.
- **Shadow Horror / Shadow Reaper** lose everything dragon-like (Flying, Breath, the `dragon` tag) and become walking
  shadows with First Strike and mind immunity (the Reaper adds Relentless 15). Stats were retuned with `sim lines`
  to keep the tier 5 win rate where it was (about 70 %).
- New capability **`SpellResistance n`**: unaffected by spells of level n and below (n is 1–3). A spell upgraded
  twice (rank 3, ADR-047) counts as the next level (`SpellLevels.Effective`, `CreatureDefinition.ResistsSpell`).
  Red, green, azure and gold dragons have `SpellResistance 3`, so only level 4 spells and twice-upgraded level 3
  spells affect them. Like the other spell capabilities, it is data and a query until magic exists (Phase 5).

### ADR-050: Towns in play: buildings table, town turn, garrisons and capture
**Status**: accepted (2026-10-16); implements the roadmap's town and dwelling items on top of ADR-030 and ADR-046; tavern
effect built by ADR-060, AI town play by ADR-059
- **Buildings are a table** (user decision): `data/buildings.csv`, one row per building level (`id, level, name, gold,
  resources, requires, income`), embedded in core as `BuildingCatalog.Default` and read by the apps at start-up like the
  creature table (`--buildings=`). Dwellings are the rows `dwelling-1` … `dwelling-5`: their level 1 builds and level 2
  upgrades any dwelling of that tier, priced as in ADR-046. A town's nine dwellings are its creature lines from the
  creature table. The draft mage guild level 4 (7500 gold, 7 crystal, mercury and sulfur) is a placeholder.
- **Towns are map objects** (`MapObjectKind.Town`, `town.<type>` for the eight playable towns) with the ADR-046
  footprint (44 blocked cells and the entrance). Per instance: owner, name, building levels, dwelling levels,
  creatures waiting per line, the garrison (seven slots) and the day of the last build.
- **New towns start with town hall 1, fort 1 and tavern 1** (user decision; the fort requires the tavern), also when a
  map omits the buildings. Pre-built buildings keep the requirements as in play (user decision): the editor refuses a
  level whose requirements are missing and the removal of a building something built requires, and the validator flags
  maps that break them. Pre-built dwellings follow the 7-of-9 rule.
- **Town turn, HoMM3-style** (user decision): on the owner's turn, with no decision pending, the owner may at any time
  build (one building per town per day, requirements and cost checked), build or upgrade a dwelling (7 of 9, a refused
  build would leave a tier unreachable, ADR-030), recruit, upgrade whole stacks for the price difference, and move
  stacks between the garrison and the visiting hero (an own hero on the entrance; it keeps one stack). Recruits go to
  the garrison or to the visiting hero. All are commands (`BuildCommand`, `BuildDwellingCommand`, `RecruitCommand`,
  `UpgradeStackCommand`, `MoveTownStackCommand`) and replay like the others.
- **Growth**: a new dwelling opens with one week of its creature's `growth`; every Monday each built dwelling adds its
  growth, owned or not, and unbought creatures accumulate. An upgraded dwelling sells the base or the upgraded
  creature from the same pool. Fort growth bonuses are still open.
- **Income**: an owned town pays the income of the highest built level of each building (town hall 500 / 1000 / 2000
  gold, resource silo +1 lumber +1 ore), through `TownIncome`.
- **Capture**: entering an enemy or neutral town with no garrison captures it. A garrison is fought first, as a guard
  commanded by the town's owner (the AI for computer players and neutral towns) without hero stats or skills; beating it
  captures the town, losing leaves the survivors in the garrison. A hero without creatures cannot attack a garrison. An
  enemy hero standing on the entrance is a normal hero battle; the garrison does not join it (for now). Allied towns can
  be entered but not managed. Owned towns watch a radius of 5 like heroes (mines: 2).
- **Map format v6** adds `town` objects; older maps load unchanged.
- **UI**: the 2D harness has a basic town panel (`TownPanel`; `--scenario=towns` loads `TownsScenario`), the editor
  inspector edits towns, and the 3D game shows towns with flag and name. **The town screen is 2D in the first
  iteration** (user decision), also in the 3D game: a full-screen 2D panel over the map; a 3D town scene may come later.
  The game app (2026-10-17) opens it (`TownPanel` with `FullScreen`, on its own canvas layer above the HUD) when an own hero
  steps into an own town on a human's turn, from the town list in the HUD (`TownListPanel`, next to the hero list; the map
  centres on the town) or with **T**; Escape or Close shuts it, and it takes the input while open. It also shows the
  governor with Appoint / Dismiss buttons (ADR-063).
  **The hero screen** (`HeroPanel`, same canvas layer and the same full-screen mode) opens with **H** for the selected hero or
  the "Info" button in the hero list; Escape or Close shuts it, "<" and ">" go through the player's heroes. It shows stats
  with what artifacts add, mana, experience, specialty, skills with their descriptions, the army (move and split stacks
  inside the hero), war machines, the paper doll and the backpack (click to take off or put on). All of it is commands
  (`MoveStackCommand`, `EquipArtifactCommand`, `UnequipArtifactCommand`).
- **Not yet**: effects of fort, marketplace trading and mage guild (the blacksmith sells war machines, ADR-056; the
  tavern hires heroes, ADR-060); town sieges.

### ADR-054: Map dwellings and caravans
**Status**: accepted design (2026-10-16), implemented 2026-10-17 with the open questions answered by the user. Core, editor,
the town / dwelling screens and the AI (see the end) are in.
- **Map dwellings** (`CreatureDwelling`, definition `dwelling`, kind `Dwelling`, 3×2 like a mine, entrance bottom-middle) are
  flaggable map objects with one creature (any creature of the catalog, set per instance, default wolf) and a count waiting
  (`Available`; omitted on the map = one week's growth when the game starts). They grow by the creature's weekly growth every
  Monday, **owned or not** (`DwellingCreaturesGrew`). An own hero that visits one flags it (`DwellingOwnerChanged`; they reveal and
  watch a radius of 2 like mines; a defeated player's dwellings turn neutral). A hero standing on the entrance recruits from it
  with `RecruitAtDwellingCommand` (any dwelling, owned or not; paid at the creature's price, discounted by the **hero's own**
  Stewardship level, ADR-063; into the hero's army). **Map format v12** (`dwelling` objects: creature, owner, available); older
  maps load. The editor places it from the object palette and edits it with `MapEditorSession.SetDwelling` (inspector rows).
- **Caravan** is a town building (`caravan` level 1: 1000 gold, 5 lumber, needs town hall 2; placeholder numbers in
  `buildings.csv`). A town with one can **hire creatures from owned map dwellings** (`OrderCaravanCommand`; paid at once at the
  town's price, so the governor's Stewardship discount applies; the creatures leave the dwelling) and **send troops from its
  garrison to another own town** (`SendTroopsCommand`, whole stack or part; the destination does not need a caravan).
- **Travel** is virtual (`CaravanRules.FindRoute`, Dijkstra): the creatures walk like an army made of them (their map speed,
  `MovementAllowance`, terrain and roads as for heroes; no hero skills; native terrain counts for the creature's own town),
  8-way, diagonals ×1.41, from the source's entrance to the town's entrance. **The route must be open**: impassable terrain and
  blocked object cells, any hostile hero, any neutral monster **and every tile it guards** block it. More than **5 days**
  (`CaravanRules.MaxDays`) means the source cannot supply the town. Both are checked **before** paying. At least 1 day.
- **Arrival** (`CaravanService.Advance`, at the start of the owner's turn, in id order): the creatures join the destination
  garrison (`CaravanArrived`); with no free slot the caravan waits at the gates until there is one.
- **User decisions (2026-10-17)**: a route that gets **blocked while travelling** makes the caravan **return** (checked every day;
  creatures go back to the dwelling and the **price is refunded**; troops return to their town's garrison, or are lost
  (`CaravanLost`) when it has no room); a **destination that is captured or lost** makes the creatures **scatter** (lost,
  gold stays spent); caravans are **invisible and cannot be attacked** (they are not on the map; only the route check matters).
- Caravans live in `GameState.Caravans` and are created only by commands, so replays rebuild them; they are not in the map file.
  They are part of the state digest. A defeated player's caravans vanish.
- **AI** (`AdventureAi`): a hero values an unflagged dwelling and what it can buy there as a target and recruits when standing on it
  (keeps a third of the gold); a town builds the caravan early once the player owns a dwelling, hires from owned dwellings with
  its spare gold (at most two caravans on the way to a town) and sends its weakest stack to an own town that has no garrison and
  no hero, keeping at least one stack.
- Not yet: a dwelling guard (use a monster next to it), marketplace
  trading with the governor's 150 %.

### ADR-062: Treasury interest and Prospecting piles (2026-10-17)
**Status**: accepted (2026-10-17); supersedes ADR-048's Treasury numbers
- **Treasury** pays **+250 / 500 / 750 / 1000** gold a day (was 100 / 200 / 350 / 500). Expert and Master also pay **0.5 % / 1 %**
  of the player's gold on hand at the start of the day, before income is added (rounded down, `HeroIncome.TreasuryInterest`).
- **Prospecting** keeps its daily resources and adds a **10 / 20 / 30 %** chance that a picked-up resource pile gives 50 %
  more (rounded up). The roll is drawn from `GameState.Random` only when the hero has the skill; `ResourcePileCollected`
  carries the amount actually granted.

### ADR-066: What the fort does (2026-10-17)
**Status**: accepted (2026-10-17), user decision on TOWN-1; dwelling requirement and growth bonus implemented, fortifications wait for sieges (post-1.0)
- **Fort (level 1) unlocks the dwellings**: every `dwelling-*` level-1 row in `buildings.csv` requires `fort 1`. New towns start with fort 1 (unchanged), so this only matters for towns a
  map places without a fort; the editor refuses such dwellings (`TownRules.UnmetDwellingRequirement`), the validator flags them, and the AI builds the tavern and fort first in a fortless town.
- **Growth bonus (HoMM3 rule)**: a town's dwellings grow **+50 % with the Citadel** (fort 2) and **+100 % with the Castle** (fort 3). The bonus is rounded down (growth 1 → 1 with a
  Citadel, 2 with a Castle) and also applies to the week a new dwelling opens with. One rule, `TownRules.WeeklyGrowth(town, line)`, serves `TownService`, the town screen and the AI.
  Dwelling upgrades still do not raise growth (ADR-046).
- **Map dwellings are unaffected**: `CreatureDwelling` keeps the creature's growth, and caravans hire from it unchanged (ADR-054).
- **Fortifications are data only until sieges (post-1.0; sieges moved before 1.0 by ADR-099)**: Fort = town wall and gate, Citadel and Castle = better fortifications (proposed HoMM3 progression: a keep, then towers and
  stronger walls; decided with sieges). No siege code yet.

### ADR-067: Marketplace rates, Leadership, a defense initiative skill, the Primal Fury group (2026-10-18)
**Status**: accepted and implemented (2026-10-18), user notes in `ideas.md`; see "Implementation" at the end. Supersedes the "150 % in a governed town" part of ADR-063; settles the Stronghold half of HERO-2
and the third Offense and Defense skills of HERO-1 (numbers are placeholders for play-testing).
- **Marketplace trading** (TOWN-2), gold against one resource at a time; resources are not swapped directly. Resources are **basic** (lumber, ore) and **magic** (crystal, sulfur, mercury).
  - **X** = the player's towns **that have a marketplace**; a town with a **governor** counts **1.5**, any other 1. The factor is `min(X + 1, 10)`, so it stops growing at 9 towns.
  - **Buying 1 unit** costs `base / min(X + 1, 10)` gold, base **5000** (basic) or **10000** (magic): 2500 / 5000 gold with one marketplace, 500 / 1000 from 9. Rounded up (placeholder).
  - **Selling 1 unit** gives `floor(base * min(X + 1, 10))` gold, base **12.5** (basic) or **25** (magic): 25 / 50 gold with one marketplace, 125 / 250 from 9.
  - **No trade cap**: any amount, any number of trades a day (user decision on TOWN-2).
  - **Trading belongs to the player, not to a hero**: the player opens the trading screen from a UI button or by clicking the marketplace in a town; no hero needs to be present. It is a
    player command (`session.Execute`, ADR-036) that publishes a past-tense event. A player who owns no town with a marketplace cannot trade (user decision), except at a map marketplace (below).
  - **Intentionally harsh**: with a single marketplace trading is meant to be expensive and unfair (user decision); it improves as marketplace towns are added.
  - **Map marketplace** (adventure-map object): a hero that visits it opens the trading screen for its owner, at a **fixed rate as if X were 4** (factor 5): buy 1000 gold (basic) / 2000 (magic),
    sell 62 gold (basic, 62.5 rounded down) / 125 (magic). The rate ignores the player's towns and governors, so it is available also to a player with no marketplace. Same rules otherwise (no cap).
    It **cannot be flagged** (no owner; any hero may visit any number of times) and does **not affect** the trading rates in towns (user decisions). Trade only while the hero stands on it.
    Whether it is guarded is a map choice; in generated maps the zone's guard strength and the treasure value decide (below). Needs a new map object kind: map format **v14**, older maps keep
    loading, editor placement through `MapEditorSession`, and a validator check.
  - **Generator** (RMG): a map marketplace is a treasure item, placed only if the zone's treasure draw is valuable enough (user decision; placeholder: from the 4000 band, valued 4000), guarded by the
    usual treasure rule (items over 1000: 40–60 % of the value × zone strength).
  - **No AI trading for now** (user decision): the AI neither uses a town marketplace nor visits map marketplaces.
  - The governor's flat 150 % of ADR-063 is dropped: the governor's effect is now the 1.5 weight in X. `StewardshipRules.MarketplaceValuePercent` is unused and goes when trading is built.
  - Noted for play-testing: buying is 100 times dearer than selling with one marketplace (20 times with nine), by the owner's numbers, so trading is a last resort and a gold sink. The random-map
    treasure values (100 gold a lumber or ore unit) do **not** follow the new rates (user decision).
- **Third Offense skill: Leadership** (`offense.third`, Expert max): all own stacks get **+1 / +2 / +3 morale** (Basic / Advanced / Expert), within the −5…+5 limit (ADR-053).
- **Third Defense skill, working title Alertness** (`defense.third`, Expert max): all own stacks get **+5 / +10 / +15 % initiative** in battle (ADR-009). The name is a placeholder.
- **No luck skill for now**; no skill raises luck.
- **Stronghold's group is Primal Fury** (working title; the code keeps the id `rage` until it is built). Points **4 + 3 + 3**:
  - **Blood Rage** (4 points, Master): close to HoMM5's Stronghold. Draft: an own stack that kills at least one creature in an attack gets +1 Attack, up to +2 / +4 / +6 / +8 (Basic … Master)
    until the battle ends.
  - **Warcry** (3 points, Expert), instead of the spells other towns have: the hero uses a warcry in battle, no mana: Basic Rally (own stacks +1 morale for 3 rounds), Advanced Intimidate (enemy
    stacks −1 morale and −20 % initiative for 2 rounds, the former `shamanism.war-cry`), Expert Terror (enemy stacks lose their next action with a 30 % chance each, once). One warcry a round,
    each once per battle; whether it uses the hero's action is MAG-4. Draft numbers.
  - **Spell resistance of the faction** (3 points, Expert), not the Defense skill of that name: the enemy hero's spells in a battle against this hero have **−2 / −4 / −6 Spell Power**
    (minimum 0).
  - The Shamanism spell drafts in [spells.md](../../docs/content/spells.md) feed the Warcry skill; the group's roster mages start with its first skill (Blood Rage) as before.
- **Adrenaline (Swamp Bestiary) stays a placeholder group** (HERO-2).
- **Implementation (2026-10-18)**:
  - Trading: `MarketplaceRules` (half-town maths: buy `ceil(2·base / min(halves + 2, 20))`, sell `floor(quarters · factorHalves / 8)`), `TradeResourceCommand` (`Resource`, `Direction`, `Amount`, optional `MapMarketplace`),
    `EconomyService.Trade`, events `ResourcesChanged` + `ResourcesTraded`. The map marketplace needs one of the player's heroes on its entrance at the moment of the trade; the visit itself is the ordinary `MapObjectVisited`.
    `StewardshipRules.MarketplaceValuePercent` is removed. UI: `MarketplacePanel`, a "Marketplace" HUD button (disabled without a marketplace town), a button in the town screen of a town with a marketplace, and it opens by itself when a hero
    visits a map marketplace (the Godot parts build but have not been run in the editor yet).
  - Map object: `MapObjectKind.Marketplace`, class `Marketplace`, definition id `marketplace` (3×2 footprint like a mine), map format **v14** (`marketplace` objects; v13 and older load unchanged), editor placement works through the generic `PlaceObject`.
  - Generator: `TreasurePlacer` may turn a chest or pile of an item worth 4000+ into a map marketplace (1 in 4, at most one per zone), guarded as a 4000-value item; `MapGenerator.Version` is 2 (seeds give other maps than version 1).
  - Skills: `offense.third` is **Leadership** (`SkillEffects.LeadershipMorale`, in `Hero.TotalMorale`); `defense.third` is **Alertness** (`SkillEffects.AlertnessInitiativePercent`, `HeroBattleBonuses.InitiativePercent`, applied in `BattleState.InitiativeOf`
    before morale's Haste or Slow, rounded to the nearest point so +5 % helps from an initiative of 10). The ids keep `third`.
  - Primal Fury: group `rage` named "Primal Fury"; `rage.first` Blood Rage, `rage.second` Warcry, `rage.third` faction spell resistance. Blood Rage: `BattleUnit.RageAttack` (a stack that kills in any strike, retaliation included, gains +1 Attack up to the cap), added in
    `DamageCalculator`. Warcries: `BattleWarcryCommand` (a battle command, accepted while one of the player's units is up, it does not use the unit's action), `BattleState.WarcryRefusal`, at most one a round and each once a battle;
    Rally and Intimidate are timed `BattleSideEffect`s counted from the round after the one they were used in (the morale and the order of a round are set when it starts), Terror rolls 30 % per enemy creature stack at once and sets `LosesNextAction`.
    The battle AI uses the strongest available warcry each round. The spell resistance is data (`HeroBattleBonuses.EnemySpellPowerPenalty`) until casting exists (Phase 5).
  - Not built: Adrenaline, a luck skill, AI trading.

### ADR-074: Size pass, per-town building rows and the Capitol (implementation, 2026-10-23)
**Status**: implemented (2026-10-23). Carries out parts of ADR-070 (size policy, TOWN-3) and ADR-073 (City Hall); choices the earlier ADRs left
open are recorded here.

- **Size pass**: 9 of 182 creatures stay Large: the five Dragon Utopia dragons, the Bone and Ghost Dragon, and Behemoth / Ancient Behemoth; the other
  33 that were Large are Medium. Whether a non-dragon line stays Large is revisited once there are visuals (CONT-2). `sim towns` keeps a similar
  spread (town totals 37–76 % against 36–74 % before).
- **Town column** in `buildings.csv` (after `id`): empty = shared by every town. A town type's rows of a building replace **all** shared levels of
  that building for that town, or add a building only that town has (the Necromancy Amplifier later). Each town's merged list is checked like the
  shared one (levels without gaps, known requirements, dwellings for every tier). `BuildingCatalog.For(town)` gives the list, `TownRules.Buildings`
  is what rules, the town screen, the editor and the validator use. Templates' start buildings are checked against the shared list.
- **Town hall names** (HoMM3): Village Hall, Town Hall, City Hall, **Capitol**. Capitol placeholder: 15 000 gold + 25 lumber + 25 ore, needs the
  **Castle** (fort 3), 4000 gold a day.
- **One Capitol per player**: building a second is refused; a captured Capitol drops to City Hall when the captor has one (`TownBuildingDowngraded`,
  after `TownOwnerChanged`); the map validator reports a second Capitol of one player. The AI builds the Capitol last.

### ADR-112: Growth buildings, the war lodge, banks per faction, garrison heroes, template connection options (ADR-064, ADR-068, ADR-070, ADR-071, ADR-106, 2026-11-02)
**Status**: accepted (2026-11-02), built. Closes the roadmap items on growth buildings, the Stronghold and Swamp Bestiary third building, per-town
building names and costs, the bank re-draft (MAP-22), garrison heroes (with the hero limit and towns keeping war machines) and the template
follow-ups (RMG-11, RMG-13, MAP-20). Points marked *(assumed)* are the agent's choices; all numbers are placeholders.

- **Buildings** (`data/buildings.csv`):
  - A new `growth` column (`dwelling-T N`) and the buildings `growth-1` (+5 a week to tier 1; 1000 gold + 5 lumber; needs a tier-1 dwelling)
    and `growth-2` (+2 to tier 2; 1500 gold + 5 ore + 2 of the town's rare resource; needs a tier-2 dwelling) in every town, each with its own
    name (Haven: Drill Yard, Cloister of Vigil; Stronghold: Warren, Mess Hall; …). TOWN-1: every dwelling of the tier gets the bonus.
  - The bonus is flat and added after the fort's percent and the artifacts' growth (not raised by them) *(assumed)*; a dwelling built this week
    gets it in its opening week too *(assumed)*.
  - A town's row at **level 0** removes a shared building from that town. Stronghold and Swamp Bestiary have no mage guild: their third building
    is the **war lodge** (`war-lodge`, three levels: War Lodge / Hall of Warcries / Drums of the Horde; Totem Circle / Spirit Totems / Elder Totem;
    2000 gold + 5 lumber + 5 ore, 3500 + 5 + 3 crystal or sulfur, 5000 + 5 + 5). Their town hall 2 needs war lodge 1 instead of mage guild 1.
  - **Warcries** (Primal Fury): a hero knows a warcry only once learned at a war lodge of at least its level (Rally 1, Intimidate 2, Terror 3),
    up to its Primal Fury level: `Hero.WarcriesLearned`, raised by a visit or by building the lodge with the hero in town (`WarcriesLearned`
    event). Starting heroes know none *(assumed, like spells)*.
  - Start buildings a town type lacks are dropped and levels capped at its maximum (scenario assembly).
- **Banks (MAP-22)**: every bank has four rows (weights 30/30/30/10) of two or three creature types of one faction (neutral creatures count as a
  faction for the neutral banks) and pays about 25–60 % of its guard's gold worth; Dragon City is worth clearly most. Some banks upgrade a fifth of
  their first stack only *(assumed)*. The tables are in `CreatureBank.cs` and [adventure-map.md](../../docs/design/adventure-map.md).
- **Garrison heroes** (`Town.GarrisonHero`, `EnterGarrisonCommand` / `LeaveGarrisonCommand`, `HeroEnteredGarrison` / `HeroLeftGarrison`):
  - The visiting hero enters with its army merged into the garrison (refused when it does not fit); it leaves the map (`GameState.Heroes` and
    `HeroesOf` are map heroes, `AllHeroesOf` and `GetHero` include garrison heroes). Leaving takes the **whole garrison** out onto the entrance
    (HoMM3), refused with the entrance taken, an empty garrison or the player at its hero limit: a garrison hero does not count (**TOWN-8**).
  - It earns income, regains mana and movement, levels up and governs like the others; its war machines stay with it, so **a town keeps war
    machines** through its garrison hero.
  - A siege of its town is fought by the garrison under the hero (its skills, artifacts, spells and machines). It **cannot retreat or surrender**
    *(assumed)*. Winning keeps the survivors (and raised undead) in the garrison; losing removes the hero and the town falls. A garrison hero with
    no troops is beaten outright: its artifacts go to the attacker.
  - Not built *(assumed out of scope)*: map files placing garrison heroes; the AI using garrisons (its attack estimate ignores the hero);
    movement commands for a garrison hero ("Unknown hero").
- **Template connections** (format **v7**, older files load with the defaults):
  - `kind: Portal`: a pair of linked portals anywhere in the two zones, on any levels; the layout does not make the zones touch. Guarded on both
    ends like a gate.
  - `guard` per connection, `connectionGuard` per template (default `Monsters`): `Garrison` (a neutral garrison in the way with troops of the
    guard value; where no tile of the way closes it, a monster guard instead), `BorderGuard` or `BorderGate` of `colour` (drawn from the seed when
    omitted) on the passage, with the **Keymaster's Tent** of that colour in the connection's first zone (`from`), guarded by the guard value.
    One tent per colour per map. Only normal connections take these; wide, gate and portal connections ignore them (a validator warning).
  - `chance` 1–100 (RMG-13, default 100): rolled from the seed (`presence` stream); a dropped connection that the zones need to stay connected
    is made anyway, in template order (`ConnectionPlacer.Present`). The layout still treats dropped connections as edges *(assumed)*.
  - The generator gives the same maps as before for templates that use none of this, so `MapGenerator.Version` stays 7.

### ADR-130: The Grail buildings; more unique buildings stay open (TOWN-12, ADR-126, 2026-11-05)
**Status**: accepted (2026-11-05); built with the Grail's part 1 (ADR-140), core tested. Owner's answer to TOWN-12; points marked *(proposal)* were filled in
by the agent and stand unless the owner changes them.

- **Grail buildings** (b): every town's Grail building does the same: **+5000 gold a day** and **+50 % growth** of every dwelling in its
  town, and in a **siege of that town** the defending side gets **+2 Initiative** for each of its stacks and **+5 Attack, Defense, Spell
  Power and Knowledge** for its hero. The proposal's per-faction effects for all the owner's heroes (morale, luck, guild spells, Spell
  Power, Necromancy, the revealed map, Attack, Defense) are dropped. *(proposal)* The siege bonus applies like the unique buildings' (the
  hero in the town or its garrison; the stacks also without a hero) and counts for the battle only (Knowledge adds no mana in it); the
  +50 % applies to the week's growth after the growth buildings, rounded down; names per town come with the buildings' rows.
- **More unique buildings** (a): stays open. The first set (ADR-126) is the only one planned; the owner adds further buildings per town to
  TOWN-12 later.

### ADR-135: Roster revision: renames, two swaps, classes A and B, Necromancy by class (CONT-2, ADR-072, 2026-11-05)
**Status**: implemented (2026-11-05), from the owner's notes in [ideas.md](../ideas.md); core tested (`CreatureTests`, `NecromancyTests`,
`CreatureBankTests`). Stats marked "placeholder stats (ADR-135)" in `creatures.csv` wait for play-testing; points marked *(proposal)* are the
agent's choices. The Undead Transformer from the same notes is TOWN-13, answered in ADR-137.

- **Renames** (no stat change; ids follow the names): Qilin → **Moon Doe** (`sylvan.moon-doe`), Pristine Unicorn → **Unicorn**
  (`sylvan.unicorn`), Defender → **Dwarf** (`academy.dwarf`), Shield Guard → **Dwarf Defender** (`academy.dwarf-defender`), Dragon Mantis →
  **Serpent Fly** (`swamp-bestiary.serpent-fly`). *(proposal)* The Dwarves lose the `human` tag.
- **The neutral Dwarf and Battle Dwarf are removed**; the Dwarven Treasury is guarded by the Academy's Dwarves (a fifth Dwarf Defenders) and
  neutral Thanes, the Dwarf counts ×3 to keep the guard's worth (120 / 180 / 240 / 330).
- **Necropolis tier swap**: the Wight line (Wight, Wraith) is **tier 2**, the Death Weaver line (Death Weaver, Fate Weaver) **tier 3**, with
  placeholder stats of their new tier (growth and prices as the other line of the tier). The Crypt's Wights are ×2.4 (12 / 20 / 24 / 36) to keep
  its guard's worth.
- **Haspid and Wyvern swap towns**: the **Wyvern** line (`swamp-bestiary.wyvern`, `…wyvern-monarch`) is the Swamp Bestiary's second tier-4
  line (growth 3, 1100 / 1450 gold); the **Haspid** line (`dragon-utopia.haspid`, `…royal-haspid`) joins Dragon Utopia (growth 2, 1650 / 2150
  gold); placeholder stats. Oszrak and Szask now prefer the Wyvern.
- **Classes**: each playable town's eight tier 1–4 lines are class **A** or **B**, one of each a tier, as the owner listed them; an upgrade
  has its base's class (the catalog refuses another); tier 5, Conflux, Dragon Utopia and neutrals have none. `CreatureDefinition.Category`,
  the `category` column after `upgrade` in `creatures.csv`.
- **Necromancy by class**: a slain creature's hit points go to the undead line of its own class in the tier the skill level gives (ADR-072's
  table is unchanged); *(proposal)* an unclassed creature's are split between the two lines in a random proportion, as before.
- **No migration**: maps and saves naming the old ids no longer load (no shipped map uses them; as with the Wyvern's move in ADR-049).
- `creatures.csv` loses the "sandbox stats were copy-pasted" notes.

### ADR-137: The Undead Transformer (TOWN-13, ADR-135, 2026-11-05)
**Status**: accepted (2026-11-05), built (ADR-143). The owner's answers to TOWN-13; the other points are the agent's proposals the owner took.

- **What it is**: a second Necropolis-only building. It turns living creatures of tier 1–2 into the tier-1 undead of their class (ADR-135:
  class A to Skeletons, B to Plague Bearers) and of tier 3–4 into the tier-2 undead (Ghouls, Wights); never elementals or constructs. It also
  swaps tier 1–2 undead for the other class's line of the same tier (Plague Bearer to Skeleton, Ghoul to Wight).
- **Counts**: one undead per living creature, as HoMM3's Skeleton Transformer. A swap gives the old count × 0.75, rounded down (at least 1).
- **Upgrades**: an upgraded creature counts as its base. The result is the upgraded undead when the town has the upgraded dwelling of that
  undead line, else the base undead (owner).
- **Creatures without a class** (tier 5, neutrals, Conflux and Dragon Utopia): tier 5 is refused; an unclassed tier 1–4 creature may become
  either line, the player picks.
- **Use**: 1000 gold + 5 mercury, needs a tier-1 dwelling. The visiting hero's or the garrison's stacks, **several at a time** as in HoMM3
  (owner); free, any number of times a day.
- **AI**: turns every eligible stack it does not need into undead when its hero visits.

### ADR-138: The first set of unique buildings built (ADR-126, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`UniqueBuildingTests`); the Godot part (the buildings on the town screen, which lists the
table's rows) not run yet. Builds ADR-126's first set; points marked *(proposal)* are the agent's choices made while building. Rules:
[town-buildings.md](../../docs/content/town-buildings.md).

- **Rows**: one row per town in `buildings.csv` (`stables`, `proving-ring`, `shell-wall`, `hall-of-records`, `altar-of-brimstone`,
  `elder-grove`, `mana-spring`), 2000 gold, 5 ore and 5 of the town's rare resource (Haven and Stronghold crystal, Swamp Bestiary and Dungeon
  sulfur, Academy, Inferno and Sylvan mercury), Fort 1 (the Stables town hall 2). The effects are code (`UniqueBuildings`), keyed by the id.
- **Visits**: every town visit of an own hero gives the bonus: entering the town, being hired or teleported there, standing in it (or in its
  garrison) at the start of the turn, or standing there when the building is finished. *(proposal)* Allied heroes get nothing. The record is
  the town's visit log, so it stays with the town when it is captured; the Stables and the Mana Spring are once a week per hero, the others
  once per hero.
- **Stables**: the map Stables' status (same source, so the two do not add up) and +400 movement at once, without the Lancer upgrade.
  **Mana Spring**: mana + 75 % of the maximum (rounded down), above the maximum until spent, capped at twice it like the Mana Vortex.
  **Elder Grove**: 1000 experience (a level-up is offered as usual).
- **Sieges**: the hero defending the town in a siege (a hero on its entrance or in its garrison, the town with a Fort) gets the building's
  bonus to its Attack, Defense, Spell Power, morale or luck for the battle. *(proposal)* +1 Knowledge lends 10 mana for the battle: spent first,
  the rest gone afterwards; a hero without a spell book gets nothing from Knowledge or Spell Power. A garrison without a hero gets nothing.
  `UniqueBuildings.SiegeOf` sums the town's buildings, for the Grail buildings to join (ADR-130).
- **AI**: builds its town's unique building after the growth buildings; its heroes take the bonuses when they pass through. *(proposal)* It
  does not yet go to a town for them.
- **Names**: working names. The Inferno **Altar of Brimstone** is close to the **Brimstone Altar**, Sylvan's Inferno spell building; a
  rename of one of them is open to the owner.

### ADR-139: The University and the War Academy built (ADR-126, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`SkillSchoolTests`); the Godot part (the lessons in the object panel) not run yet. Builds
ADR-126's University and War Academy; points marked *(proposal)* are the agent's choices made while building. Rules:
[adventure-map.md](../../docs/design/adventure-map.md#special-sites).

- **Objects**: one kind, `SkillSchool`, two definitions: `university` (the magic schools) and `war-academy` (the six general groups, offense …
  war machines); never the faction group (Primal Fury). Map format v36 (`skillSchool`; which one comes from the definition).
- **Lessons**: on a hero's first visit the school draws that hero's lessons and keeps them for it (`SkillSchool.Lessons`, `LessonsDrawn`):
  up to 2 groups the hero has not learned, each as the group's first skill not banned at Basic, and up to 2 skills of its learned groups of
  that kind below their maximum, one level up. Both draws use the level-up weights (`SkillOffers.Weight`); the rival school is drawn like any
  other school. New groups are drawn even when the hero has six groups.
- **Taking one** (`TakeLessonCommand`, `LessonTaken`): the hero on the entrance, 1000 gold + 5 crystal, mercury and sulfur, any number a
  visit. A lesson is open while the skill is one level below it and the hero can raise it; it shows **learned** once the skill is at its level
  (taken here or learned elsewhere since) and **no room** when it would start a seventh group. *(proposal)* A hero with no lessons at all is
  told the school has nothing to teach it.
- **Bans**: banned skills are never drawn; a group whose skills are all banned is not offered.
- **AI**: weighs an unvisited school as one lesson (1200 gold) when it can pay, a visited one by the lessons it would take; on the entrance it
  takes its open lessons while the spare resources last (a third of the gold kept), raises before new groups, a new group only with a free
  group slot. *(proposal)*
- **Generator**: treasure value 2500 each, weight 1, at most one of each per map *(proposal)*.
- **Game app**: the object panel lists the hero's lessons with the price; learned and no-room lessons are disabled.

### ADR-143: The Academy's Observatory; the Undead Transformer built (ADR-137, ADR-138, 2026-11-06)
**Status**: implemented, core tested (`UndeadTransformerTests`); the town screen's Transformer section not yet run in Godot. The Observatory is
the owner's note in [ideas.md](../ideas.md) ("academy: observatory - sets town's scouting radius to 24"); points marked *(proposal)* are the
agent's choices.

- **Observatory** (Academy, `observatory` in `buildings.csv`): an Academy town with it sees **24** cells from its centre instead of 10
  (ADR-141), from the moment it is built. *(proposal)* 2500 gold, 5 ore, 5 mercury, needs mage guild 1; a second unique building of the
  Academy beside the Hall of Records, with no visit or siege bonus. A town's sight never reaches its hero's Scouting (ADR-142), so it shows
  armies as words.
- **Undead Transformer** (Necropolis, `undead-transformer`): ADR-137's rules, 1000 gold + 5 mercury, needs a tier-1 dwelling.
  `TransformCreaturesCommand` lists the stacks of the garrison and the visiting hero (army, slot, and the line for a creature without a class)
  and turns them all or none. *(proposal)* Only the Necropolis's own tier 1–2 undead swap lines; other undead (and tier 3+ Necropolis) are
  refused. A result joins a stack of the same creature in its army, else takes the old slot. Event `CreaturesTransformed`; the replay name is
  `transformCreatures`.
- **Town screen**: when built, a toggle per stack it can take ("10 Footman → 10 Skeleton"; two toggles, one per line, for a creature without a
  class) and a "Transform" button for the chosen ones.
- **AI** *(proposal for "every stack it does not need")*: in each army of its town with the Transformer it turns all living stacks into
  undead when the army is already mostly undead by worth (creature value × count), elsewhere only those whose undead are worth at least as
  much; a creature without a class becomes the more valuable line; it never swaps lines. It builds both buildings after the unique building.
