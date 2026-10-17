# Factions and towns

> Part of the [game design](overview.md). World, group and town flavour: [lore.md](../content/lore.md). Open items ❓:
> [open-questions.md](../project/open-questions.md).

Towns are organised into four groups (ADR-010). **Groups matter only in lore and the campaign** (ADR-028): story,
scenario setup, campaign alliances and scripted events. In standard and random-map games they have **no rule effect**.
There are no automatic alliances, shared skills, diplomacy or group-based bonuses; teams are set per game, and native
terrain belongs to individual towns. Groups are not modelled in code yet; `BuiltInMapObjects.PlayableTowns` lists the
eight playable towns.

| Group (working title / lore proposal ❓) | Towns | Playable |
|---|---|---|
| 1 · Light / Dawn Covenant | **Haven** (humans), **Sylvan** (elves), **Academy** (mages) | yes |
| 2 · Darkness / Ashen Court | **Necropolis** (undead), **Inferno** (daemons), **Dungeon** (underground dwellers / dark elves) | yes |
| 3 · Wild / Unbound | **Stronghold** (orcs), **Swamp Bestiary** | yes |
| 4 · Primordial / Firstborn | **Conflux** (elementals), **Dragon Utopia** (all dragons except bone dragons) | **no**: not balanced for PvP; campaign and neutral (map guardians, dwellings) only |

- 8 playable towns in 3 groups + 2 special towns in a fourth, non-playable group. Each playable town has 9 creature
  lines over 5 tiers (2+2+2+2+1), all upgradeable; a town builds 7 of them
  ([towns-and-economy.md](towns-and-economy.md)).
- Bone dragons belong to Necropolis, not Dragon Utopia.
- **Rivalries have no combat effect** (ADR-029). Opposition between towns or groups exists in story and campaign, but
  there are no hate/enmity damage bonuses (unlike HoMM3 Angels vs Devils) and no alignment modifiers. Creature
  capabilities target *properties* (undead, flying, size, …), never town or group membership.
- **Two rule effects outside combat**:
  - a town's **sworn enemies** (two per town, [lore.md](../content/lore.md)) have their magic school offered in only
    5 % of level-ups (ADR-048, `BuiltInSkills` in `Skills.cs`);
  - the **ring of towns** orders the heroes a tavern offers (ADR-060, `TavernRules.Ring`).
  Whether both should use the same relation is open ❓.
- **Vertical slice: Haven and Inferno** (ADR-034). The holy-vs-demonic contrast reads instantly in a dark,
  realistic art style; the rivalry is story only.
