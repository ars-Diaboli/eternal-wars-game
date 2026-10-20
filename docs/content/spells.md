# Spells

The spell catalog for magic (Phase 5): six schools, four spell levels, three ranks per spell. **No spells or casting
yet** (roadmap, "Magic"); mana is in (`ManaRules`, ADR-063). This is the content the spell table `data/spells.csv` will
start from. Rules: ADR-047 (structure), ADR-048 (skills), ADR-055 (Spell Power and Knowledge), ADR-042 (creature casters,
immunities), ADR-063 (six schools, mana, faction groups). Lore: [lore.md](lore.md). Skills that drive magic: [skills.md](skills.md#magic-schools). Spell ids and names are drafts;
every number is a proposal to react to.

The 24 school spell ids below are the ones `data/heroes.csv` already names as hero specialties (`spell light.bless`, …),
so the roster and this catalog agree. Stronghold and Swamp Bestiary have no school (ADR-063): their sections are
faction-ability drafts, not school spells.

## How magic works (proposal)

- **Schools**: six, one per ring town (Light, Nature, Arcana, Death, Chaos, Shadow; `BuiltInSkills.Schools`). Stronghold
  and Swamp Bestiary have the faction group Primal Fury instead (ADR-063, ADR-069; Adrenaline is dropped). Any hero may learn any school (a
  sworn enemy's school is rarely offered, ADR-048).
- **Levels**: spells have levels 1–4. The school's **magic** skill unlocks them (Basic → level 1 spells, Advanced → 2,
  Expert → 3, Master → 4). The **mage guild** of the same level in a town offers spells of that level; a visiting hero
  learns the ones its skills allow (a spell not in a guild can be found in map shrines, later).
- **Ranks**: every spell has three ranks (I, II, III). A hero knows rank I; the school's **mastery** skill upgrades
  learned spells to rank II (Basic) and rank III (Advanced). Rank changes what a spell does, not just its strength.
- **Mana** (`ManaRules`, ADR-063, built): pool of **10 × Knowledge** plus attunement; at the start of the owner's turn a
  hero regains **⌈Knowledge / 10⌉** plus attunement. A spell costs its listed mana (rank-independent) minus attunement,
  but never less than **25 %** of the base, rounded up. Refilling in a town is not in code ❓.
- **Strength**: damage and healing scale with **Spell Power** (SP); durations are in rounds.
- **In battle**: the hero casts **one spell per round** before or after its stacks act (exact timing ❓). Spells that target a stack cannot target war machines unless stated.
- **Immunity and resistance**: creatures carry immunity tags (`all`, `mind`, `fire`, `ice`, `lightning`, `poison`,
  `curse`, `blind`), already in the creature table; a spell lists its tags. Proposed addition: `holy` (Smite). `SpellResistance n` creatures ignore spells of
  level n and below (rank III counts one level higher, ADR-049).

## Light (Haven)

The faithful call it the "lamp": prayer shaped into wards and flame, answered by something that is not entirely kind.

| Lvl | Spell | Mana | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|
| 1 | Bless (`light.bless`) | 5 | – | one stack deals maximum damage, 3 rounds | all own stacks | all own stacks, 5 rounds |
| 2 | Heal (`light.heal`) | 9 | – | restore 20 + 8·SP health to a stack | + 50 % more, cures bad status | also revives up to a quarter of the lost creatures |
| 3 | Smite (`light.smite`) | 16 | holy | 30 + 12·SP damage to a stack, double to undead and demons | also hits adjacent stacks at half | ignores resistance, all enemy undead and demons take half |
| 4 | Sanctuary (`light.sanctuary`) | 25 | – | one stack cannot be attacked in melee for 2 rounds | all own stacks | all own stacks, 3 rounds, ranged attackers also need to see the stack |

- *Bless*: the cathedral-cities of Haven chant over the army before the march; the soldiers swear that the chant is
  warm.
- *Smite*: a flagellant's prayer, loud enough to hurt what the dead remember.
- *Sanctuary*: "No blade shall find you while you carry the light"; the light is a rented thing, and the lender
  collects.

## Nature (Sylvan)

Root-and-branch magic: the forests do not obey, they answer, and the price is memory (see Sylvan in the lore).

| Lvl | Spell | Mana | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|
| 1 | Entangle (`nature.entangle`) | 5 | – | roots hold an enemy stack: it loses its next move | 2 stacks | 3 stacks, they also take 5 + 3·SP damage |
| 2 | Regrowth (`nature.regrowth`) | 9 | – | a stack regenerates 5 + 3·SP health at the start of each round, 3 rounds | all own stacks | all own stacks, 5 rounds |
| 3 | Swarm (`nature.swarm`) | 16 | – | a cloud of insects: enemy stack −3 Attack and Defense, 3 rounds | 2 stacks | all enemy stacks, 4 rounds |
| 4 | Barkskin (`nature.barkskin`) | 25 | – | an own stack takes −30 % damage, 3 rounds | all own stacks | all own stacks, −40 %, melee attackers take thorns damage |

- *Entangle*: old-growth roots, tended for centuries and quick to grip.
- *Barkskin*: the elf who casts it ages a year in that moment; the Sylvan do not talk about the count.

## Arcana (Academy)

Cold, exact and precise: the Academy's cities study the Wheel itself, and their spells read like equations.

| Lvl | Spell | Mana | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|
| 1 | Magic Arrow (`arcana.magic-arrow`) | 5 | – | 10 + 8·SP damage to one stack, never misses or is blocked by cover | + 50 % | two arrows, two targets |
| 2 | Haste (`arcana.haste`) | 9 | – | an own stack gets +3 battlefield speed and +50 % initiative for 3 rounds | 2 stacks | all own stacks, 5 rounds |
| 3 | Frost Bolt (`arcana.frost-bolt`) | 16 | ice | 25 + 12·SP damage, the target is slowed (−2 speed) for 2 rounds | hits 2 stacks | 3 stacks, frozen stacks lose a turn |
| 4 | Dispel (`arcana.dispel`) | 25 | – | removes every spell from one stack | an area of stacks, friend or foe | every spell from the battlefield; costs half mana if only enemy spells are removed |

- *Magic Arrow*: the first spell every apprentice learns; the Academy teaches that it is always the same arrow.
- *Dispel*: the Academy's real secret, kept from outsiders: they claim they can undo whatever the Wheel has done.

## Death (Necropolis)

Necromancy as accounting: the dead remember every age and are patient creditors. This is the school Haven and Sylvan
hate most.

| Lvl | Spell | Mana | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|
| 1 | Curse (`death.curse`) | 5 | curse, mind | an enemy stack deals minimum damage for 3 rounds | 2 stacks | 3 stacks, 5 rounds |
| 2 | Drain Life (`death.drain-life`) | 9 | – | 15 + 6·SP damage; the hero's stack heals for half of it | hits 2 stacks | hits 3 stacks, healing may exceed the stack's health by a quarter |
| 3 | Raise Dead (`death.raise-dead`) | 16 | – | after the battle, 20 + 5·SP skeletons per the dead's total health join the army | also raises during the battle, once | during the battle each round |
| 4 | Plague (`death.plague`) | 25 | poison | an enemy stack loses 5 % of its creatures every round for 3 rounds; living only | 2 stacks | all enemy stacks, 4 rounds |

- *Curse*: a debt called in on the field; the Necropolis keeps a ledger of every blade that ever struck it.
- *Raise Dead*: Necromancers do not raise the dead; they remind the dead that they once fought.

## Chaos (Inferno)

Pact-magic: the demons of the ash beneath the world treat every war as a harvest, and teach the fire to count.

| Lvl | Spell | Mana | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|
| 1 | Fireball (`chaos.fireball`) | 5 | fire | 15 + 8·SP damage to a stack and its neighbours (area 3×3) | area 5×5 | area 5×5, no damage to own stacks |
| 2 | Terror (`chaos.terror`) | 9 | mind | an enemy stack flees: it cannot attack for 2 rounds, retaliates at half damage | 2 stacks | 3 stacks |
| 3 | Inferno (`chaos.inferno`) | 16 | fire | 40 + 14·SP damage to a stack, burning for 3 rounds | 2 stacks | 3 stacks, burning stacks take 20 % more damage |
| 4 | Hellfire (`chaos.hellfire`) | 25 | fire | a column of flame: 60 + 18·SP damage to every stack in a column of the battlefield | 2 columns | all enemy stacks, half damage to own |

- *Fireball*: the first thing a demon teaches a mortal, and usually the last the mortal enjoys.
- *Hellfire*: legend says it does not burn, only reminds the ground that it was once ash.

## Shadow (Dungeon)

Dark-elf magic of the lightless places: cut vision, steal footing, and make the enemy afraid of the dark it brought.

| Lvl | Spell | Mana | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|
| 1 | Blind (`shadow.blind`) | 5 | blind, mind | an enemy stack cannot see: its shots miss, melee hits halved; breaks when hit | 2 stacks | 3 stacks, does not break |
| 2 | Shadow Step (`shadow.shadow-step`) | 9 | – | teleport an own stack to a free cell on the battlefield | 2 stacks | the stack can still attack this round |
| 3 | Despair (`shadow.despair`) | 16 | mind | an enemy stack has −2 morale for the battle | 2 stacks | all enemy stacks, −3 |
| 4 | Dark Bolt (`shadow.dark-bolt`) | 25 | – | 50 + 16·SP damage, ignores Defense | + a damage over time of 5 + 2·SP per round, 2 rounds | hits 2 stacks |

- *Blind*: the Dungeon's oldest trick: the dark is not the absence of light, it is a thing that can be thrown.
- *Despair*: a dark-elf commander does not need to kill a stack if the stack has already stopped believing.

## Shamanism (Stronghold, faction-ability drafts)

> Not a hero school any more (ADR-063): Stronghold has the Primal Fury group, whose Warcry skill reuses War Cry (ADR-067). These spells are drafts to reuse as creature or faction abilities.

Orc rites: no spell books, only drums, bone and ancestors, and a loud confidence that the spirits are on their side.

| Lvl | Spell | Mana | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|
| 1 | Bloodlust (`shamanism.bloodlust`) | 5 | – | an own melee stack deals +30 % damage, 3 rounds | 2 stacks | all own melee stacks, +40 % |
| 2 | Ancestral Spirits (`shamanism.ancestral-spirits`) | 9 | – | spirits guard an own stack: +4 Defense, 3 rounds | 2 stacks | all own stacks, 5 rounds |
| 3 | War Cry (`shamanism.war-cry`) | 16 | mind | all enemy stacks −1 morale and their initiative −20 % for 2 rounds | −2 morale | −2 morale, own stacks +1 morale |
| 4 | Earthquake (`shamanism.earthquake`) | 25 | – | the battlefield shakes: damages every stack for 20 + 8·SP, **and** weakens walls in sieges | a larger tremor, flyers take half | all enemy stacks, 30 % chance to knock each prone (loses its turn) |

- *Bloodlust*: the clan drums do not ask whether you want it.
- *Earthquake*: the clans have survived several turnings by never building anything worth burning, and know exactly how
  to make others regret building.

## Primal (Swamp Bestiary, faction-ability drafts)

> Not a hero school any more (ADR-063): Swamp Bestiary has the Primal Fury group, like Stronghold (ADR-069; the Adrenaline group is dropped). These spells are drafts to reuse as creature or faction abilities, perhaps for the "Spiritual Totems" name of the Warcry skill (HERO-7).

Beast-cult magic of the drowned lowlands: not spells but invitations, answered by whatever lives in the mire.

| Lvl | Spell | Mana | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|
| 1 | Venom (`primal.venom`) | 5 | poison | an enemy stack takes 5 + 3·SP damage a round for 3 rounds; living only | 2 stacks | 3 stacks, 4 rounds |
| 2 | Mire (`primal.mire`) | 9 | – | a patch of bog on the battlefield: stacks in it have −2 speed, 3 rounds | larger patch | no flying over it, flyers land and slow too |
| 3 | Regenerate (`primal.regenerate`) | 16 | – | an own stack regenerates fully each round, 3 rounds | 2 stacks | all own stacks |
| 4 | Beast Call (`primal.beast-call`) | 25 | – | summon a stack of swamp beasts for the battle, strength 20 + 10·SP health | two stacks | three stacks, they persist as a neutral reinforcement for the next battle |

- *Venom*: the cults brew it from what the old-age ruins sank into the mire.
- *Beast Call*: no one is quite sure the beasts answered a call; some say they were already coming.

## Creature casters and placeholder ids

The creature table already names caster spells (`Caster fireball 1`, …). Some are catalog spells; the rest become
creature-only abilities when magic arrives ([MAG-1](../project/open-questions.md#mag-1)):

| Id in `creatures.csv` | Becomes |
|---|---|
| `fireball`, `frost-bolt`, `blind` | `chaos.fireball`, `arcana.frost-bolt`, `shadow.blind` |
| `bloodlust` | `shamanism.bloodlust` (a faction-ability draft, not a school spell) |
| `cure` | `light.heal` (rank I) |
| `resurrection`, `rebirth` | creature-only: raise a stack (rank-limited) |
| `lightning` | creature-only, tag `lightning` |
| `confusion`, `death-cloud`, `summon-imps` | creature-only: confuse a stack, a poison cloud, imps for the battle |

## Adventure spells (proposal)

A few spells are cast on the adventure map, from the hero's spell book, at the cost of mana (and the day's movement
where noted). They belong to a school and follow its levels: level 1–2 at Basic or Advanced magic, level 3 (Town Portal,
Dimension Door) at Expert ❓.

| Spell | School | Level | Mana | Effect |
|---|---|---|---|---|
| Town Portal | Arcana | 3 | 16 | the hero returns to the nearest own town (costs half its movement points) |
| Dimension Door | Shadow | 3 | 16 | the hero moves up to 6 cells in a straight line, ignoring terrain and objects; once a day |
| View Terrain | Nature | 2 | 9 | reveals the map around the hero (radius 12) until the next turn |
| Grave Whisper | Death | 2 | 9 | shows the dead of recent battles on the map: the next battle gives +10 % raised skeletons |
| Distant Vision | Light | 1 | 5 | reveals the creatures and heroes in a region |
| Feast | Primal (faction-ability draft) | 2 | 9 | the army gets +1 map speed until the next turn |

## Open

- Numbers: mana pool, cost scale, damage formulas, durations (play-test with the simulator).
- Timing: exactly when the hero casts (before the round or when the hero's slot comes), and spell targeting of
  machines.
- Spell tags ([MAG-1](../project/open-questions.md#mag-1)), and which tags creatures resist.
- Artifacts that raise spell power, knowledge or school skills, see [artifacts.md](artifacts.md).
- Whether Raise Dead and Beast Call keep summoned creatures across battles.
- Adventure spells and what they cost in movement; whether Town Portal and Dimension Door should need only Advanced magic.
- Whether attunement also raises spell **potency** for some schools (ADR-047 says so; ADR-063 and `ManaRules` have pool,
  daily mana and cost only).
- Whether a town refills mana (not in code).
