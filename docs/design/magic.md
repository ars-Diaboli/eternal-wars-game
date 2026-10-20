# Magic

> Part of the [game design](overview.md). Spell catalog: [spells.md](../content/spells.md) (design draft). Open items ❓:
> [open-questions.md](../project/open-questions.md). Casting is Phase 5; mana and the school skills exist.

- **Six schools** (ADR-047, ADR-063), one for each town on the ring: Light (Haven), Nature (Sylvan), Arcana (Academy),
  Death (Necropolis), Chaos (Inferno), Shadow (Dungeon); names are working titles ❓. Each school is also a hero skill
  group; any hero may learn any school, a sworn enemy's school is rarely offered ([heroes.md](heroes.md)).
- **Stronghold and Swamp Bestiary have no school**: both have the faction group **Primal Fury** instead (ADR-063, ADR-069;
  Swamp Bestiary's Adrenaline group is dropped). Its warcries replace spells (no mana, ADR-067); its Interference skill
  weakens and raises the cost of the enemy hero's spells. What their mage guilds offer is open ❓.
- **Four spell levels** and a **four-level mage guild**; every spell has three levels of its own (upgraded twice).
- A school's skills: **Magic** (4 levels, unlocking spell levels), **Attunement** (4 levels: max mana, daily mana and
  spell cost by school, ADR-063, table reworked by ADR-069; Death attunement also gives **Necromancy**), **Mastery**
  (2 levels: spell upgrades). Tables: [skills.md](../content/skills.md#magic-schools).
- **Mana** (implemented, `ManaRules`): the pool is 10 × Knowledge plus attunement; a hero regains ⌈Knowledge / 10⌉
  plus attunement at the start of its owner's turn; a spell costs its base minus attunement, never less than 25 % of
  the base (rounded up). ADR-069 adds **10 × ⌈level / 10⌉** to the pool (not built yet). Refilling in a town is open ❓.
- **Necromancy** (ADR-069, not built): after a victory a hero with Death attunement raises 5–20 % of the slain enemies' value
  as undead, of tiers capped by the attunement level; a Necropolis building, the Necromancy Amplifier, adds 5 %
  ([skills.md](../content/skills.md#necromancy-death-attunement-adr-069-not-built)). Open: MAG-7.
- **Map objects for magic** (ADR-069): Mana Vortex, Fountain of Magic and the Shrines that teach spells
  ([adventure-map.md](adventure-map.md#second-object-catalogue-adr-069)).
- **Spell Power**: what it does in numbers is open ❓.
- The hero casts **one spell per round** in battle; creature casters use the Caster capability
  ([combat.md](combat.md#creature-capabilities-homm4-style-implemented-adr-042)).
- Spell resistance and immunity tags exist on creatures and take effect with magic.
