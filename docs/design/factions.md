# Factions and towns

> Part of the [game design](overview.md). World, group and town flavour: [lore.md](../content/lore.md). Open items ❓:
> [open-questions.md](../project/open-questions.md).

Towns form four groups (ADR-010). **Groups matter only in lore and the campaign** (ADR-028): story, scenario setup, campaign alliances and
scripted events. In standard and random-map games they have **no rule effect**: no automatic alliances, shared skills, diplomacy or group
bonuses; teams are set per game, and native terrain belongs to individual towns. Groups are not modelled in code;
`BuiltInMapObjects.PlayableTowns` lists the eight playable towns.

| Group (working title / lore proposal ❓) | Towns | Playable |
|---|---|---|
| 1 · Light / Dawn Covenant | **Haven** (humans), **Sylvan** (elves), **Academy** (mages) | yes |
| 2 · Darkness / Ashen Court | **Necropolis** (undead), **Inferno** (daemons), **Dungeon** (underground dwellers / dark elves) | yes |
| 3 · Wild / Unbound | **Stronghold** (orcs), **Swamp Bestiary** | yes |
| 4 · Primordial / Firstborn | **Conflux** (elementals), **Dragon Utopia** (all dragons but bone dragons, which are Necropolis') | **no**: campaign and neutral (guardians, dwellings) only |

- Each playable town has 9 creature lines ([tech tree](towns-and-economy.md#creature-tech-tree)).
- **Rivalries have no combat effect** (ADR-029): no hate damage (unlike HoMM3 Angels vs Devils), no alignment modifiers. Creature
  capabilities target *properties* (undead, flying, size, …), never town or group.
- **The ring of towns** ([lore.md](../content/lore.md); ADR-072, HERO-3) has two rule effects: the **arch-rival's** school (the town across
  the ring) is offered in only 5 % of level-ups (ADR-048; ❓ `BuiltInSkills` still uses the two sworn enemies), and it ranks the
  [tavern's offers](towns-and-economy.md#tavern) (`TavernRules.Ring`).
- **Vertical slice: Haven and Inferno** (ADR-034): the holy-vs-demonic contrast reads instantly in a dark, realistic style.
