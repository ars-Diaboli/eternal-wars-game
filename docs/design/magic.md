# Magic

> Part of the [game design](overview.md). Everything here is built unless tagged *(planned)*; numbers are drafts. Spell catalog:
> [spells.md](../content/spells.md) (data: `src/Core/data/spells.csv`); school skill tables: [skills.md](../content/skills.md#magic-schools).
> Open items ❓: [open-questions.md](../project/open-questions.md).

## Schools
- **Six schools** (ADR-047, ADR-063), one per ring town: Light (Haven), Nature (Sylvan), Arcana (Academy), Death (Necropolis), Chaos
  (Inferno), Shadow (Dungeon); names are working titles ❓. Each is a hero skill group; any hero may learn any school, the arch-rival's
  rarely ([heroes.md](heroes.md#skills-and-levels)).
- **Stronghold and Swamp Bestiary have no school**: their faction group **Primal Fury** (ADR-063, ADR-069) has warcries instead of spells
  (no mana, ADR-067) and Interference, which weakens the enemy hero's spells and raises their cost. A hero knows a warcry
  once a **war lodge** ([building](../content/town-buildings.md)) of its level taught it (Rally 1, Intimidate 2, Terror 3; ADR-071, ADR-112),
  up to its Warcry skill.
- **Four spell levels**; each school has **17 spells** (5 / 5 / 4 / 3 by level, ADR-099). Every spell has **three ranks**.
- **Elements** (ADR-106, MAG-13): every spell also has an element (air, earth, fire, water); the four elemental orbs raise their element's
  spells, whatever the school. Summons call town creatures (elementals: Conflux).
- **School skills** (ADR-063, ADR-069): **Magic** (4 levels: spell levels 1–4), **Attunement** (4 levels: max mana, daily mana, spell cost;
  no spell potency, ADR-071; Death attunement also gives Necromancy), **Mastery** (2 levels: spell ranks).

## Guilds and learning
ADR-071, ADR-072, ADR-106, ADR-110, ADR-114, ADR-122, ADR-123 (MAG-2, MAG-3, MAG-12, MAG-13).
- **Mage guild** (4 levels; level 4 a placeholder): offers its town's school, **3 / 3 / 2 / 2** spells at levels 1–4, **drawn per town at
  game start** from `GameState.Random`; the map may fix them (`guildSpells`, map format v30). A guild of level L teaches levels 1 to L.
- **Spell buildings** (HoMM4-like magic circle): two per ring town, one for each neighbouring school, each adding **1 spell per guild level**,
  drawn at game start (not fixable by the map); 2000 gold + 5 of the school's resource (its town's rare resource), needs mage guild 2.
- **Spell book**: mages of magic towns start with one holding their specialty spell; others buy one in a mage guild for **500 gold**. Map
  heroes may be given a book and spells (map format v29).
- **Learning**: an own hero with a book on a guild town's entrance learns what the guild and spell buildings offer that its school's Magic
  skill allows (spell level ≤ Magic level). Shrines and the map also teach spells ([adventure-map.md](adventure-map.md#magic)).
- **Ranks**: Mastery allows rank II (Basic) and III (Advanced). In a guild town a known spell the town offers rises, free, straight to the
  rank Mastery allows; a spell learned elsewhere rises at a guild that offers it. Mana costs are the same at every rank.
- **Mana refill**: a night in a guild town (still on the entrance when the owner's turn starts) fills the hero's mana.

## Mana
`ManaRules` (ADR-063, ADR-069, ADR-072 MAG-8):
- **Pool** = 10 × Knowledge + 10 × ⌈level / 10⌉ + attunement (so every hero has at least 10).
- **Daily regain** = ⌈Knowledge / 10⌉ + attunement, at the owner's turn start.
- **Cost** = base − attunement, never below 25 % of the base (rounded up).

## Casting in battle
- The hero casts **at most once a round**, during the turn of one of its units, which keeps its action (HoMM3; ADR-071, MAG-4). Machines
  firing by themselves don't count; spells that change initiative don't change this.
- **Spell Power** (ADR-072): damage and healing × (1 + SP / 10), durations × (1 + SP / 10), at least 1 round; durations count after the
  round of the cast. The spell list's numbers are at SP 0 (ADR-099).
- **Spell specialty** (MAG-6): +3 % of the amount per hero level.
- **Spell resistance** skill: hostile spells' damage and duration −8 / 14 / 20 % (rounded away from zero, at least 1 round).
- **What stops a spell**: creature immunity tags and spell resistance ([capabilities](combat.md#creature-capabilities)); holy creatures
  ignore holy spells; war machines are `mechanical` and take only damage spells. Such stacks are no target and untouched by area spells.
  Holy spells deal double to undead and demons. Area rules: [combat.md](combat.md#area-attacks).
- A spell cast again on a stack replaces its earlier effect; different spells add up.
- **Creature casters** (`Caster` capability): n casts per battle at rank I, Spell Power = count / 2, no mana (ADR-120).
- **Battle spells**: 75 of 91 work (ADR-123). *(Planned)*: the 16 needing field zones, copies, control changes or two-click aiming, and some
  rank effects (list in ADR-123).

## Adventure spells
Each cast costs mana plus a fixed **500 movement points** (fewer left: no cast; ADR-073). Town Portal and Dimension Door are level 4
(MAG-5); Dimension Door lands only on explored tiles. List and ranks: [spells.md](../content/spells.md#adventure-spells) (ADR-120, ADR-123).
The AI casts Town Portal home, not yet the show-only spells.

## Necromancy
ADR-069, ADR-072 (MAG-7), ADR-078. After a victory (not over a retreat or surrender) a hero with Death attunement raises 5–20 % of the
slain enemies' hit points as undead, tiers capped by its attunement; the Necromancy Amplifier adds 5 %. Shown after the battle result,
before any level-up. Rules: [skills.md](../content/skills.md#necromancy-death-attunement-adr-069-built-adr-078).

## Magic artifacts
ADR-111; catalog [artifacts.md](../content/artifacts.md). Elemental orbs (+25 % to their element's damage and healing; Magic Arrow takes
the best), the Unwritten Codex (all level 1–2 spells at rank I while worn), the Recanter's Cloak (no spells above level 2 in its battles),
army immunities, mana artifacts. **Spell scrolls** (ADR-136): a worn scroll lets a hero with a spell book cast its spell without the school
skill, at the rank its Mastery allows, for the normal mana.
