# Spells

The spell catalog for magic (Phase 5): six schools, four spell levels, three ranks per spell. **No spells or casting
yet** (roadmap, "Magic"); mana is in (`ManaRules`, ADR-063). This is the content the spell table `data/spells.csv` will
start from. Rules: ADR-047 (structure), ADR-048 (skills), ADR-055 (Spell Power and Knowledge), ADR-042 (creature casters,
immunities), ADR-063 (six schools, mana, faction groups), ADR-070 and ADR-071 (casting in battle, spell books, guilds, Spell Power, levels of
Town Portal and Dimension Door), ADR-072 (Spell Power formula, ranks). Lore: [lore.md](lore.md). Skills that drive magic:
[skills.md](skills.md#magic-schools).

**Re-draft (ADR-099, MAG-1, 2026-10-29)**: the list is re-drafted to fit the lore, with the HoMM3, HoMM4, HoMM5 and Olden Era spell books as
reference: **17 spells a school, 102 in all** (5 / 5 / 4 / 3 by level, adventure spells included), so a guild draws a selection and two towns
of one school can differ (MAG-13, ADR-106). The 24 spells of the first draft keep their ids (`data/heroes.csv` names them as hero specialties).
Numbers are drafts; final tuning waits for decent visuals (the owner's answer). Each spell's line in the tables reads: base numbers, the
rank II and III changes. **Review (ADR-106, MAG-13)**: the list stays as drafted, every spell has an element (below), and the summons call
town creatures (and Conflux elementals).

## How magic works

- **Schools**: six, one per ring town (Light, Nature, Arcana, Death, Chaos, Shadow; `BuiltInSkills.Schools`). Stronghold
  and Swamp Bestiary have the faction group Primal Fury instead (ADR-063, ADR-069): warcries, no spells ([below](#stronghold-and-swamp-bestiary)).
  Any hero may learn any school (the arch-rival's school is rarely offered, ADR-048, ADR-072).
- **Spell books** (ADR-071, MAG-2; not built): mage heroes start with one; warriors buy one in a mage guild for **500 gold**.
- **Levels**: spells have levels 1–4. The school's **magic** skill unlocks them (Basic → level 1, Advanced → 2, Expert → 3, Master → 4): a
  hero learns a spell when that skill is at least the spell's level (ADR-071). The **mage guild** of the same level offers spells of that
  level, of the town's own school: **3** at levels 1 and 2, **2** at levels 3 and 4; two more town buildings add spells from the two schools
  next to the town's own on the ring, 1 spell per guild level each (HoMM4-like; ADR-071, ADR-072). Map **Shrines** of level 1–3 teach one
  spell (ADR-070).
- **Which spells a guild offers** (ADR-106, MAG-13): drawn per town when the game starts, from the game's random source, without repeats
  among the school's spells of that level (HoMM3). The map may fix some of them for a town in the editor; the rest are drawn. Adventure spells
  are drawn like the others: no guild level always offers one (Town Portal is not in every Academy guild 4). The two extra-school buildings
  draw their spell per guild level the same way, from their own school.
- **Elements** (ADR-106, MAG-13): every spell also belongs to one of four elements, **air**, **earth**, **fire** or **water** (the
  "Element" column), whatever its school; the four elemental orbs ([artifacts.md](artifacts.md)) raise the spells of their element. Two
  exceptions: Magic Arrow belongs to all four (HoMM3), Bind Elemental to the kind of elemental it binds. The element is not an immunity tag:
  a fire-immune creature ignores spells tagged `fire`, not every fire-element spell.
- **Ranks**: every spell has three ranks (I, II, III). A hero knows rank I; the school's **mastery** skill allows a learned spell to rise to
  rank II (Basic) and rank III (Advanced); it rises, free, when the hero visits a town whose guild offers it (ADR-072, MAG-3). A rank changes
  what a spell does, not just its strength.
- **Mana** (`ManaRules`, ADR-063, built): pool of **10 × Knowledge** plus attunement plus 10 × ⌈level / 10⌉; at the start of the owner's
  turn a hero regains **⌈Knowledge / 10⌉** plus attunement. A spell costs its listed mana (the same at every rank) minus attunement, never
  less than **25 %** of the base, rounded up. A hero in a town with a mage guild refills its mana (ADR-071; not built).
- **Strength** (ADR-072): **Spell Power** (SP) multiplies every damage, healing and summoned amount by **(1 + SP / 10)** and every duration
  by **(1 + SP / 10)**, rounded down, at least 1 round. The tables give the **base** numbers (at SP 0); "a + b·SP" is gone. A spell specialty
  adds +3 % of the spell's effect per hero level (MAG-6). An area spell has a fixed area; a big creature with any cell inside is hit once
  (MAG-11). Areas are in battlefield cells (the field is 30 × 20; a Small creature is one cell, ADR-070).
- **In battle** (ADR-070, ADR-071): the hero casts **at most one spell a round**, during the turn of one of the units it controls; casting
  does not use that unit's action. Spells may target war machines, which are `mechanical` and resist every `mind`, `poison` and healing spell.
- **Immunity and resistance**: creatures carry immunity tags (`all`, `mind`, `fire`, `ice`, `lightning`, `poison`, `curse`, `blind`);
  a spell lists its tags. New tags: `holy` (double against undead and demons; creatures tagged `holy` ignore it),
  `summon` (summoned creatures vanish when the battle ends and give no experience; they are town creatures' rows, or Conflux elementals, never
  neutrals, ADR-106). `SpellResistance n` creatures ignore spells of level n
  and below (rank III counts one level higher, ADR-049). The hero skill **Spell resistance** cuts hostile spells' damage and duration by
  **8 / 14 / 20 %** (ADR-071).
- **Words**: *stack* is a unit on the battlefield; *own* and *enemy* are from the caster's side; *rounds* are battle rounds.

## Light (Haven)

The faithful call it the "lamp": prayer shaped into wards and flame, answered by something that is not entirely kind. Light is the
cheapest school to cast (Light attunement lowers costs) and the best at keeping an army standing; it hurts the dead and the demons most.

| Lvl | Spell (id) | Mana | Element | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|---|
| 1 | **Bless** (`light.bless`) | 5 | water | – | an own stack deals maximum damage, 3 rounds | all own stacks | all own stacks, +10 % damage |
| 1 | **Shield of Faith** (`light.shield-of-faith`) | 5 | earth | – | an own stack takes −15 % melee damage, 3 rounds | −25 % | all own stacks |
| 1 | **Purge** (`light.purge`) | 5 | water | – | removes the enemy's spells from an own stack | also heals 20 | all own stacks |
| 1 | **Steadfast** (`light.steadfast`) | 4 | water | mind | an own stack +1 morale and cannot be made to flee (Terror), 3 rounds | +2 morale | all own stacks |
| 1 | **Distant Vision** (`light.distant-vision`, adventure) | 5 | air | – | shows the creatures, heroes and their armies in radius 10 around a cell in sight, until the next turn | radius 14 | also under the fog |
| 2 | **Heal** (`light.heal`) | 9 | water | – | an own stack regains 40 health | + 50 %, cures negative spells | also revives the dead of the stack up to a quarter of those lost |
| 2 | **Righteous Might** (`light.righteous-might`) | 8 | fire | – | an own stack +3 Attack, 3 rounds | +5 | all own stacks |
| 2 | **Deflect Missiles** (`light.deflect-missiles`) | 9 | air | – | an own stack takes −40 % ranged damage, 3 rounds | −50 % | all own stacks |
| 2 | **Vigil** (`light.vigil`) | 8 | fire | – | an own stack retaliates against every attack this round | 2 rounds | all own stacks, 1 round |
| 2 | **Consecrate** (`light.consecrate`) | 10 | fire | holy | a 3 × 3 area: enemy undead and demons in it take 15 a round, 3 rounds | 5 × 5 | own stacks in it also heal 10 a round |
| 3 | **Smite** (`light.smite`) | 16 | fire | holy | 80 damage to an enemy stack, double to undead and demons | also hits the stacks next to it at half | ignores spell resistance |
| 3 | **Prayer** (`light.prayer`) | 16 | water | – | an own stack +2 Attack, Defense and speed, 3 rounds | all own stacks | all own stacks, +3 |
| 3 | **Martyrdom** (`light.martyrdom`) | 14 | earth | – | an own stack takes half of the damage meant for another own stack, 3 rounds | all of it | also +2 Defense for the martyr |
| 3 | **Holy Word** (`light.holy-word`) | 18 | air | holy | every enemy undead and demon stack takes 50 | 70 | they also lose their next action with 30 % |
| 4 | **Sanctuary** (`light.sanctuary`) | 25 | earth | – | an own stack cannot be attacked in melee, 2 rounds | all own stacks | 3 rounds; shooters need a clear line too |
| 4 | **Resurrection** (`light.resurrection`) | 25 | earth | – | revives 150 health of an own stack's dead (lost again after the battle) | the revived stay | also from a stack destroyed this battle |
| 4 | **Wrath of the Eye** (`light.wrath-of-the-eye`) | 30 | fire | holy | every enemy stack takes 60, undead and demons double; own stacks are spared | 80 | enemy stacks also −1 morale for the battle |

- *Bless*: the cathedral-cities of Haven chant over the army before the march; the soldiers swear the chant is warm.
- *Smite*: a flagellant's prayer, loud enough to hurt what the dead remember.
- *Martyrdom*: Haven canonises the dead of every battle; volunteers are never lacking.
- *Sanctuary*: "No blade shall find you while you carry the light"; the light is a rented thing, and the lender collects.
- *Wrath of the Eye*: the dying sun opens once more, for a moment, and looks at the field.

## Nature (Sylvan)

Root-and-branch magic: the forests do not obey, they answer, and the price is memory (see Sylvan in the lore). Nature holds the enemy in place,
calls beasts and storms, and makes its own stacks hard to kill.

| Lvl | Spell (id) | Mana | Element | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|---|
| 1 | **Entangle** (`nature.entangle`) | 5 | earth | – | roots hold an enemy stack: it loses its next move (it can still shoot or attack next to it) | 2 stacks | 3 stacks, they also take 15 |
| 1 | **Wasp Swarm** (`nature.wasp-swarm`) | 5 | air | – | 30 damage to an enemy stack and −20 % initiative, 1 round | 40 | 2 stacks |
| 1 | **Bear's Strength** (`nature.bears-strength`) | 5 | earth | – | an own stack +20 % melee damage, 3 rounds | +30 % | all own melee stacks |
| 1 | **Hunter's Sight** (`nature.hunters-sight`) | 5 | air | – | an own shooting stack ignores the distance penalty and cover, 3 rounds | +10 % ranged damage | all own shooters |
| 1 | **Pathfinder's Call** (`nature.pathfinders-call`, adventure) | 5 | earth | – | until the end of the day the hero's army moves on forest floor, grass and swamp as native terrain | every terrain | also +10 % movement |
| 2 | **Regrowth** (`nature.regrowth`) | 9 | water | – | an own stack regains 15 health at the start of each round, 3 rounds | all own stacks | 5 rounds |
| 2 | **Call Lightning** (`nature.call-lightning`) | 10 | air | lightning | 60 damage to an enemy stack | 80 | jumps to a second stack at half |
| 2 | **Fleetfoot** (`nature.fleetfoot`) | 8 | air | – | an own stack +2 speed and can pass through other stacks this round | 2 rounds | all own stacks, 1 round |
| 2 | **Thornwall** (`nature.thornwall`) | 10 | earth | – | a line of 5 cells of thorns, 3 rounds: blocks movement, a stack walking out of it takes 20 | 7 cells | also blocks shots through it |
| 2 | **View Terrain** (`nature.view-terrain`, adventure) | 9 | earth | – | reveals the map around the hero, radius 12, until the next turn | radius 18 | the revealed area stays explored |
| 3 | **Swarm** (`nature.swarm`) | 16 | air | – | a cloud of insects: an enemy stack −3 Attack and Defense, 3 rounds | 2 stacks | all enemy stacks |
| 3 | **Call the Herd** (`nature.summon-pack`, was Summon the Pack) | 16 | air | summon | a stack of Sylvan Pegasi for the battle, 150 health worth | 220 | Silver Pegasi |
| 3 | **Chain Lightning** (`nature.chain-lightning`) | 18 | air | lightning | 70 damage to an enemy stack, then 35 and 17 to the two nearest stacks (own stacks too) | 90 / 45 / 22 | only enemy stacks are chained |
| 3 | **Oakheart** (`nature.oakheart`) | 15 | earth | – | an own stack +4 Defense and immune to `mind`, 3 rounds | +6 | all own stacks, +4 |
| 4 | **Barkskin** (`nature.barkskin`) | 25 | earth | – | an own stack takes −30 % damage, 3 rounds | all own stacks | −40 %, melee attackers take 10 % of their damage back |
| 4 | **Earthquake** (`nature.earthquake`) | 25 | earth | – | every stack on the ground takes 40 and loses 20 % initiative; in a siege, 2 wall sections take 1 damage each | flyers are spared | enemy stacks only; 3 wall sections |
| 4 | **Awaken the Grove** (`nature.awaken-grove`) | 30 | earth | summon | a stack of Sylvan Treants for the battle, 400 health worth; they root and cannot move | 550 | Ancient Treants, they can move |

- *Entangle*: old-growth roots, tended for centuries and quick to grip.
- *Barkskin*: the elf who casts it ages a year in that moment; the Sylvan do not talk about the count.
- *Earthquake*: the Sylvan learned it from the Dungeon's tunnels, and use it on the Dungeon's walls.
- *Awaken the Grove*: each treant called is a tree that does not go back to sleep in the old place.

## Arcana (Academy)

Cold, exact and precise: the Academy's cities study the Wheel itself, and their spells read like equations. Arcana bends time and space,
binds elementals and undoes other magic; it has the most mana (Arcana attunement raises the pool).

| Lvl | Spell (id) | Mana | Element | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|---|
| 1 | **Magic Arrow** (`arcana.magic-arrow`) | 5 | all four | – | 40 damage to an enemy stack; never misses, ignores cover | 60 | two arrows, two targets |
| 1 | **Slow** (`arcana.slow`) | 6 | earth | – | an enemy stack −30 % speed and initiative, 3 rounds | −40 % | 2 stacks |
| 1 | **Arcane Ward** (`arcana.arcane-ward`) | 5 | water | – | an own stack takes −30 % spell damage, 3 rounds | −50 % | all own stacks |
| 1 | **Precision** (`arcana.precision`) | 5 | air | – | an own shooting stack +25 % ranged damage, 3 rounds | +35 % | all own shooters |
| 1 | **Detect Magic** (`arcana.detect-magic`, adventure) | 4 | air | – | shows every map object's contents (chests, banks' guards, Pandora's Boxes, shrines' spells) in radius 10 | radius 15 | also creature banks' rewards |
| 2 | **Haste** (`arcana.haste`) | 9 | air | – | an own stack +3 speed and +50 % initiative, 3 rounds | 2 stacks | all own stacks |
| 2 | **Frost Ring** (`arcana.frost-ring`) | 10 | water | ice | 40 damage to every stack in the ring of cells around a centre cell (not the centre) | 55 | own stacks are spared |
| 2 | **Mirror Image** (`arcana.mirror-image`) | 10 | water | – | a copy of an own stack fights for 2 rounds; it deals full damage and vanishes when hit | 3 rounds | the copy survives one hit |
| 2 | **Unravel** (`arcana.unravel`) | 8 | water | – | removes the own spells from an enemy stack | all enemy stacks | also −1 Spell Power for the enemy hero, the battle |
| 2 | **Teleport** (`arcana.teleport`) | 9 | water | – | moves an own stack to a free cell within 10 | anywhere | the stack can still act this round |
| 3 | **Frost Bolt** (`arcana.frost-bolt`) | 16 | water | ice | 90 damage to an enemy stack, −2 speed, 2 rounds | 2 stacks | 3 stacks; a frozen stack loses its next action |
| 3 | **Bind Elemental** (`arcana.bind-elemental`) | 18 | the elemental's | summon | a stack of Conflux air, earth, fire or water elementals (the caster's choice) for the battle, 200 health worth | 280 | two kinds |
| 3 | **Anti-Magic** (`arcana.anti-magic`) | 15 | earth | – | an own stack is immune to every spell of level 1–3 (also the own), 3 rounds | level 1–4 | all own stacks, levels 1–2 |
| 3 | **Clockwork Precision** (`arcana.clockwork`) | 14 | fire | – | own war machines +50 % damage and act twice, 2 rounds | 3 rounds | the offensive machine ignores cover |
| 4 | **Dispel** (`arcana.dispel`) | 25 | water | – | removes every spell from one stack | every stack in a 5 × 5 area | every spell on the battlefield; half the mana if only enemy spells are removed |
| 4 | **Stasis** (`arcana.stasis`) | 25 | earth | – | an enemy stack is taken out of time: it cannot act or be harmed, 2 rounds | 3 rounds | 2 stacks |
| 4 | **Town Portal** (`arcana.town-portal`, adventure) | 25 | earth | – | the hero returns to the nearest own town | any own town | costs no movement points beyond the 500 |

- *Magic Arrow*: the first spell every apprentice learns; the Academy teaches that it is always the same arrow.
- *Stasis*: the Academy's proof that the Wheel can be stopped, for a moment, for one stack.
- *Dispel*: the Academy's real secret, kept from outsiders: they claim they can undo whatever the Wheel has done.

## Death (Necropolis)

Necromancy as accounting: the dead remember every age and are patient creditors. Death weakens the living, drains them, and puts the dead
back into the ranks; it is the school Haven and Sylvan hate most. Death attunement gives Necromancy (ADR-069).

| Lvl | Spell (id) | Mana | Element | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|---|
| 1 | **Curse** (`death.curse`) | 5 | fire | curse | an enemy stack deals minimum damage, 3 rounds | 2 stacks | 3 stacks, −10 % damage |
| 1 | **Weakness** (`death.weakness`) | 5 | water | curse | an enemy stack −3 Attack, 3 rounds | −5 | 2 stacks |
| 1 | **Grave Chill** (`death.grave-chill`) | 5 | water | – | an enemy stack −20 % initiative and −1 speed, 3 rounds; living only | −30 % | 2 stacks |
| 1 | **Unholy Vigour** (`death.unholy-vigour`) | 5 | earth | – | an own undead stack +2 Attack and Defense, 3 rounds | +3 | all own undead stacks |
| 1 | **Grave Whisper** (`death.grave-whisper`, adventure) | 5 | air | – | marks the dead of the last battles in radius 10: the hero's next battle there raises +10 % with Necromancy | +15 % | also shows the guards' numbers in radius 10 |
| 2 | **Drain Life** (`death.drain-life`) | 9 | water | – | 50 damage to a living enemy stack; an own stack (the caster's choice) heals half of it | 2 stacks | the healing can revive the stack's dead |
| 2 | **Animate Dead** (`death.animate-dead`) | 9 | earth | – | revives 60 health of an own undead stack's dead (they stay) | 90 | 2 stacks |
| 2 | **Death Cloud** (`death.death-cloud`) | 10 | air | poison | 30 damage to every living stack in a 3 × 3 area (own too) | 45 | living enemy stacks only |
| 2 | **Sorrow** (`death.sorrow`) | 9 | earth | mind, curse | an enemy stack −2 morale and −1 luck, 3 rounds | 2 stacks | all enemy stacks, 2 rounds |
| 2 | **Ledger of Ages** (`death.ledger-of-ages`, adventure) | 10 | air | – | shows the next week's event and the armies of the enemy heroes in sight | also of the heroes in towns | lasts a week |
| 3 | **Raise Dead** (`death.raise-dead`) | 16 | earth | – | after the battle, skeletons worth 15 % of the slain enemies' health join the army (on top of Necromancy) | also once during the battle, from a destroyed stack | each round during the battle |
| 3 | **Death Ripple** (`death.death-ripple`) | 16 | earth | – | every living stack on the battlefield takes 30 (own too) | 45 | living enemy stacks only |
| 3 | **Call the Debt** (`death.call-the-debt`) | 18 | fire | curse | an enemy stack takes 50 % of the damage it has dealt this battle | 70 % | 2 stacks |
| 3 | **Bone Wall** (`death.bone-wall`) | 15 | earth | – | a line of 5 cells of bone, 3 rounds: blocks movement and shots; own undead walk through it | 7 cells | it also deals 20 to a living stack next to it each round |
| 4 | **Plague** (`death.plague`) | 25 | water | poison | an enemy stack loses 5 % of its creatures each round, 3 rounds; living only | 2 stacks | all living enemy stacks |
| 4 | **Vampirism** (`death.vampirism`) | 25 | water | – | an own stack heals 30 % of the damage it deals and can revive its dead, 3 rounds | 50 % | all own stacks, 30 % |
| 4 | **Final Reckoning** (`death.final-reckoning`) | 30 | earth | – | every living stack loses 10 % of its creatures (own too; at least 1) | 15 % | living enemy stacks only |

- *Curse*: a debt called in on the field; the Necropolis keeps a ledger of every blade that ever struck it.
- *Raise Dead*: Necromancers do not raise the dead; they remind the dead that they once fought.
- *Call the Debt*: the Ashen Court's lawyers say no spell is crueller than an honest account.
- *Final Reckoning*: every age ends with one; the Necropolis only brings it forward.

## Chaos (Inferno)

Pact-magic: the demons of the ash beneath the world treat every war as a harvest, and teach the fire to count. Chaos burns: the strongest
damage, fire that stays on the field, fury and summoned demons.

| Lvl | Spell (id) | Mana | Element | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|---|
| 1 | **Fireball** (`chaos.fireball`) | 6 | fire | fire | 35 damage to every stack in a 3 × 3 area | 5 × 5 | 5 × 5, own stacks spared |
| 1 | **Bloodlust** (`chaos.bloodlust`) | 5 | fire | – | an own stack +30 % melee damage, 3 rounds | 2 stacks | all own melee stacks |
| 1 | **Brand** (`chaos.brand`) | 5 | fire | fire, curse | an enemy stack takes +20 % damage from every attack, 3 rounds | +30 % | 2 stacks |
| 1 | **Firebolt** (`chaos.firebolt`) | 5 | fire | fire | 45 damage to an enemy stack | 65 | sets it burning: 10 a round, 2 rounds |
| 1 | **Ashwalk** (`chaos.ashwalk`, adventure) | 5 | fire | – | until the end of the day the hero's army moves on lava, wasteland and cursed lands as native terrain | every terrain | also +10 % movement |
| 2 | **Terror** (`chaos.terror`) | 9 | air | mind | an enemy stack flees: it cannot attack, 2 rounds, and retaliates at half | 2 stacks | 3 stacks |
| 2 | **Frenzy** (`chaos.frenzy`) | 9 | fire | mind | an enemy stack attacks the nearest stack, friend or foe, on its next turn | 2 turns | it also takes +20 % damage while frenzied |
| 2 | **Fire Wall** (`chaos.fire-wall`) | 10 | fire | fire | a line of 5 burning cells, 3 rounds; a stack entering or starting in it takes 30 | 7 cells | 3 × 5 area |
| 2 | **Hellgate** (`chaos.hellgate`) | 10 | fire | summon | Inferno Imps for the battle, 80 health worth | 120 | Familiars (they steal 1 enemy hero mana for each own attack) |
| 2 | **Blood Price** (`chaos.blood-price`) | 0 | fire | – | an own stack loses 10 % of its creatures (at least 1); the hero gains mana equal to the health lost / 5 | / 4 | / 3 |
| 3 | **Inferno** (`chaos.inferno`) | 16 | fire | fire | 100 damage to a stack, burning 15 a round, 3 rounds | 2 stacks | 3 stacks; burning stacks take +20 % damage |
| 3 | **Meteor Shower** (`chaos.meteor-shower`) | 18 | earth | – | 70 damage to every stack in a 3 × 3 area | 5 × 5 | the area's cells become rubble (an obstacle) |
| 3 | **Pact of Ash** (`chaos.pact-of-ash`) | 15 | fire | – | an own stack +5 Attack and +2 speed; it loses 10 % of its creatures at the end of each round, 3 rounds | 5 % | 2 stacks |
| 3 | **Summon Demons** (`chaos.summon-demons`) | 18 | fire | summon | a stack of Inferno Horned Demons for the battle, 250 health worth | 350 | they stay with the army after the battle (up to the slots) |
| 4 | **Hellfire** (`chaos.hellfire`) | 25 | fire | fire | 160 damage to every stack in a column of the battlefield | 2 columns | every enemy stack, half to own |
| 4 | **Ashfall** (`chaos.ashfall`) | 30 | fire | fire | every stack on the battlefield takes 120 (Armageddon) | 160 | own demons spared |
| 4 | **Harvest** (`chaos.harvest`) | 25 | earth | – | an enemy stack below 25 % of its starting health is destroyed; else 100 damage | 33 % | 2 stacks |

- *Fireball*: the first thing a demon teaches a mortal, and usually the last the mortal enjoys.
- *Blood Price*: no demon gives mana away; it is bought, with the same coin every time.
- *Ashfall*: the Wheel turning for one battle: everything burns, and the Inferno calls it spring.

## Shadow (Dungeon)

Dark-elf magic of the lightless places: cut vision, steal footing, and make the enemy afraid of the dark it brought. Shadow blinds, misleads
and takes control; its damage spell ignores armour.

| Lvl | Spell (id) | Mana | Element | Tags | Rank I | Rank II | Rank III |
|---|---|---|---|---|---|---|---|
| 1 | **Blind** (`shadow.blind`) | 5 | fire | blind, mind | an enemy stack cannot see: it does not act until hit or 2 rounds pass, retaliates at half | 3 rounds | 2 stacks |
| 1 | **Shroud** (`shadow.shroud`) | 5 | air | – | an own stack takes −50 % ranged damage and cannot be targeted by spells of level 1, 3 rounds | levels 1–2 | all own stacks |
| 1 | **Forgetfulness** (`shadow.forgetfulness`) | 5 | water | mind | an enemy shooting stack forgets how to shoot (melee only), 3 rounds | 2 stacks | all enemy shooters, 2 rounds |
| 1 | **Shadow Dart** (`shadow.shadow-dart`) | 5 | air | – | 30 damage to an enemy stack, ignores Defense | 45 | 2 stacks |
| 1 | **Veil** (`shadow.veil`, adventure) | 5 | air | – | the hero's army shows only as "a few", "many" … to other players, a week | also its artifacts hidden | its hero hidden on the map from heroes farther than 3 cells |
| 2 | **Shadow Step** (`shadow.shadow-step`) | 9 | air | – | moves an own stack to a free cell in shadow (not next to an enemy) | anywhere | the stack can still attack this round |
| 2 | **Confusion** (`shadow.confusion`) | 9 | water | mind | an enemy stack moves and attacks at random, 1 round | 2 rounds | 2 stacks |
| 2 | **Darkness** (`shadow.darkness`) | 10 | earth | – | a 5 × 5 area: shots into or out of it are halved, 3 rounds | 7 × 7 | own stacks inside also +2 Defense |
| 2 | **Night Terrors** (`shadow.night-terrors`) | 9 | fire | mind | an enemy stack −2 morale and loses retaliation, 2 rounds | 3 rounds | 2 stacks |
| 2 | **Eavesdrop** (`shadow.eavesdrop`, adventure) | 8 | air | – | shows the town screen (buildings, garrison, the hero inside) of an enemy town in sight | any enemy town | for a week |
| 3 | **Despair** (`shadow.despair`) | 16 | earth | mind | an enemy stack −2 morale for the battle | 2 stacks | all enemy stacks, −3 |
| 3 | **Hypnotize** (`shadow.hypnotize`) | 18 | air | mind | the caster controls an enemy stack of up to 300 health for its next turn | 450 | 2 turns |
| 3 | **Eclipse** (`shadow.eclipse`) | 16 | air | – | every enemy stack −20 % initiative and −1 luck, 2 rounds | 3 rounds | also −2 Attack |
| 3 | **Unseen** (`shadow.unseen`) | 15 | air | – | an own stack cannot be targeted by attacks or spells until it acts, 2 rounds | 3 rounds | it deals +25 % damage on its first attack out of hiding |
| 4 | **Dark Bolt** (`shadow.dark-bolt`) | 25 | earth | – | 140 damage, ignores Defense | + 20 a round for 2 rounds | hits 2 stacks |
| 4 | **Puppet Master** (`shadow.puppet-master`) | 30 | air | mind | an enemy stack fights for the caster for 2 rounds (not against its own hero's stacks once it would die) | 3 rounds | up to 600 health; the rest stays on its side |
| 4 | **Dimension Door** (`shadow.dimension-door`, adventure) | 25 | air | – | the hero moves up to 6 cells in a straight line to an explored tile, ignoring terrain and objects; once a day | 9 cells | twice a day |

- *Blind*: the Dungeon's oldest trick: the dark is not the absence of light, it is a thing that can be thrown.
- *Despair*: a dark-elf commander does not need to kill a stack if the stack has already stopped believing.
- *Puppet Master*: the lightless things the Dungeon serves taught it; nobody asks what they control.

## Stronghold and Swamp Bestiary

No school and no spells (ADR-063, ADR-072): their heroes use **Primal Fury**, whose **warcries** cost no mana
([skills.md](skills.md#primal-fury-stronghold-and-swamp-bestiary-adr-067-adr-069-draft-numbers)). The first draft's Shamanism and Primal
spells are spread out:

| First draft | Now |
|---|---|
| Bloodlust (`shamanism.bloodlust`) | Chaos, `chaos.bloodlust` (the Ogre Mage's caster spell) |
| War Cry (`shamanism.war-cry`) | the Warcry skill's Intimidate (built) |
| Earthquake (`shamanism.earthquake`) | Nature, `nature.earthquake` (sieges come before 1.0, ADR-099) |
| Ancestral Spirits, Venom, Mire, Regenerate, Beast Call, Feast | material for the 3-level warcry / totem building (ADR-071, not built) and creature abilities |

## Creature casters

The creature table names caster spells (`Caster fireball 1`, …; the number is the rank). With this list they become:

| Id in `creatures.csv` | Becomes |
|---|---|
| `fireball`, `frost-bolt`, `blind`, `bloodlust` | `chaos.fireball`, `arcana.frost-bolt`, `shadow.blind`, `chaos.bloodlust` |
| `cure` | `light.heal` |
| `lightning` | `nature.call-lightning` |
| `confusion`, `death-cloud` | `shadow.confusion`, `death.death-cloud` |
| `resurrection` | `light.resurrection` |
| `rebirth` (Phoenix), `summon-imps` (Breeder) | creature-only abilities: the Phoenix rises once with 30 % of its stack; the Breeder calls imps like `chaos.hellgate` |

## Adventure spells

Cast on the adventure map from the spell book, for mana and a fixed **500 movement points** (ADR-073, MAG-5; a hero with fewer left cannot
cast). Each school has two or three, at its levels:

| School | Spells |
|---|---|
| Light | Distant Vision (1) |
| Nature | Pathfinder's Call (1), View Terrain (2) |
| Arcana | Detect Magic (1), Town Portal (4) |
| Death | Grave Whisper (1), Ledger of Ages (2) |
| Chaos | Ashwalk (1) |
| Shadow | Veil (1), Eavesdrop (2), Dimension Door (4) |

## Open

- Numbers: costs, base damage and durations, after the visuals (the owner's answer to MAG-1); the simulator checks damage per mana.
- Summoned creatures (ADR-106, MAG-13): town creatures' rows, never neutrals; elementals are the exception (Conflux rows). Call the Herd
  calls Sylvan Pegasi (Silver Pegasi at rank III), Awaken the Grove Sylvan Treants (Ancient Treants), Bind Elemental the Conflux air, earth,
  fire and water elementals, Hellgate Inferno Imps (Familiars), Summon Demons Inferno Horned Demons; the Breeder's imps are Inferno Imps.
  The creatures for Call the Herd and Summon Demons are assumed; the owner may pick others.
- Elements: one per spell, drafted with HoMM3's spell elements as reference where a spell has a counterpart (ADR-106); the owner may move any.
