# Artifacts

The artifact catalog (roadmap, "Artifacts"). **Implemented in core (ADR-061)**: the table `data/artifacts.csv` (with
`data/artifact_sets.csv`) holds every artifact below plus 31 HoMM3-style placeholder items (67 artifacts in all), and the rules apply
the effects the game has. This page is the design and lore; the tables are the truth for numbers (see "Editing artifacts"
in [development.md](development.md)). Rules touched: ADR-018 (movement), ADR-025 (income sources), ADR-045 (terrain
penalties), ADR-053 (morale and luck), ADR-055 (primary stats). Lore: [lore.md](lore.md). Every name and number is a
draft to react to.

**What is applied and what is not.** The rows below state the intended effect. The game applies attack, defense,
spell power and knowledge (as stats, they matter with magic), morale, luck, movement, battle speed, melee and ranged
damage, damage taken, daily income, native terrain, terrain penalty and skill levels. Effects that need systems that do
not exist yet are in the table's notes as "Not yet": a mana pool (Amulet of the Deep), spell resistance and reflection
(Aegis of Ash, Scale of the Elder Dragon, Mirror of the Lightless), regeneration (Root of the Oldest Tree), re-rolled
morale (Dawn Banner), starting initiative (War-Drum), double damage (Bone Dragon's Fang), fire resistance, the best
magic skill (Crown of the Waning Eye), cheaper spells and offensive spell levels (sets). Where the table substitutes a
number (Amulet of the Deep: +2 Knowledge; Root of the Oldest Tree: 5 % less damage taken; Mirror of the Lightless:
+2 Defense, +1 luck; sets: see below) the table wins.

**HoMM3 style placeholders.** Weapons (Centaur's Ax +2, Blackshard of the Dead Knight +3, Greater Gnoll's Flail +4, Ogre's
Club of Havoc +5, Sword of Hellfire +6, Titan's Gladius +12 Attack −3 Defense), shields (Dwarven Lords +2, Buckler of the
Gnoll King +4, Targ of the Rampaging Ogre +5, Sentinel's Shield +12 Defense −3 Attack), helms (Alabaster Unicorn,
Skull Helmet, Helm of Chaos, Crown of the Supreme Magi), body armour (Petrified Wood, Rib Cage, Basilisk Scales, Cyclops
King's Tunic, Brimstone, Titan's Cuirass), Pendant of Courage, Necklace of Swiftness, Cape of Velocity, Boots of Speed,
Equestrian's Gloves, Ring of the Wayfarer, and the Endless pouches, purses, bag and sack of gold, lumber and ore. They fill
the gaps between the lore artifacts so that every slot has items of every tier.

## How artifacts work

- **Paper-doll slots** (a hero wears one artifact per slot, plus a backpack for the rest):

  | Slot | Count | Typical bonus |
  |---|---|---|
  | Head | 1 | Knowledge, Spell Power, skills |
  | Neck | 1 | morale, luck, mana |
  | Cloak | 1 | movement, Defense |
  | Body | 1 | Defense, damage reduction |
  | Weapon | 1 | Attack, melee or ranged damage |
  | Shield | 1 | Defense, resistances |
  | Ring | 2 | luck, Spell Power, daily resources |
  | Boots | 1 | movement, battle speed |
  | Misc | 3 | everything else |
  | Backpack | 50 | unworn artifacts; no effect |

- **Tiers** (what a chest, a guard or a defeated hero's pack can drop): **Minor** (common, small bonuses),
  **Major** (strong), **Relic** (one of each per game, big effects, with a story), and **Set pieces** (worn together
  for a bonus). The HoMM3 chest "artifact tier" of ADR-039 picks a tier; **Relics** are not in chests.
- **Stacking**: bonuses add; terrain artifacts and Pathfinding together never reduce the penalty below zero (at most 100 %).
  The morale/luck cap of ±5 stays (ADR-053); damage-taken reductions stop at 90 %.
- **Loot**: artifacts come from the map (pickup objects, guarded) and from a defeated hero (the winner gets all of them, what
  does not fit in its backpack is lost; a fleeing hero takes its artifacts away). Chests do not hold artifacts yet. Heroes
  hand artifacts to each other while their exchange is open.
- **Not in the town**: no artifact market for now (artifacts are not for sale; a later merchant is an option).

## Minor artifacts

Worn by the first generation of Vael's soldiers, not much more than a keepsake. Carried by the many, held by few.

| Name | Slot | Effect | Flavour |
|---|---|---|---|
| Ash Sigil | Neck | +1 morale | struck from a cold coal of the last Wheel fire; soldiers swear it is still warm |
| Pilgrim's Boots | Boots | +10 % movement points | worn through three crusades; the soles remember every road |
| Weathered Cloak | Cloak | −25 % of the non-native terrain penalty | a cloak that has been soaked in every terrain once |
| Iron Band | Ring | +1 luck | a ring of plain iron: whoever lost it never returned for it |
| Squire's Shield | Shield | +1 Defense | dented, honest and painted over three times |
| Rusted Blade | Weapon | +1 Attack | an old sword; what it cut was worth the edge |
| Scholar's Cap | Head | +1 Knowledge | the Academy's apprentices wear it until they can afford better |
| Hearth Charm | Misc | +100 gold a day | a coin worn smooth, said to always arrive on the right day |
| Woodcutter's Axe | Misc | +1 lumber a day | rumoured to have felled a tree twice |
| Miner's Lamp | Misc | +1 ore a day | the flame never goes out, even in the rain |
| Marsh Talisman | Misc | removes the non-native penalty on swamp and shallow water | strung from teeth; a Swamp Bestiary hunter's gift |
| Candle of the Pious | Misc | +1 Spell Power | wax and prayer; Haven's chapels burn these by the thousand |

## Major artifacts

Made by masters, bound by oaths, and fought over in every age.

| Name | Slot | Effect | Flavour |
|---|---|---|---|
| Crown of the Waning Eye | Head | +3 Knowledge, +1 level to the best magic skill | forged when the sun first dimmed; the wearer sees the dark earlier than others |
| Hunter's Mark | Neck | +2 luck | a bone token with a hole in it; the bearer's arrows find whatever hid |
| Mantle of the Last Grove | Cloak | removes the non-native penalty on grass and forest floor | Sylvan weavers spin it from moss that grows only in dying groves |
| Plate of the Faithful | Body | +4 Defense, −10 % damage taken from ranged attacks | riveted in the cathedral-forges of Haven; the rivets are blessed, the plate is not comfortable |
| Wardens' Edge | Weapon | +4 Attack, +10 % melee damage | a Sylvan blade-dancer's twin, the other half never found |
| Aegis of Ash | Shield | +4 Defense, 10 % fire resistance for the hero's stacks | quenched in the cinders of a burned city |
| Ring of Reckoning | Ring | +2 luck, +1 morale | Necropolis lawyers wear it; they prefer to be on the winning side of the ledger |
| Ring of Pale Fire | Ring | +3 Spell Power | a Chaos pact in a band of gold; the flame inside does not like being looked at |
| Boots of the Wheel-Runner | Boots | +20 % movement points, +1 battlefield speed | stitched from the hide of a beast that ran every circuit of the Wheel |
| Sceptre of Cinders | Misc | +2 Attack, +2 Spell Power | the Inferno's demons gift these, and always want them back |
| Amulet of the Deep | Misc | +4 mana per Knowledge point | Dungeon dark-elves wear it to remember the surface; they say it helps them forget |
| War-Drum | Misc | +1 morale, own stacks start the battle with +10 % initiative | Stronghold clans carry these to every war; the beat is older than the clan |
| Alchemist's Satchel | Misc | +1 of each rare resource a day | Academy glassware; the vials never run dry, and never explain why |
| Ledger of Debts | Misc | +300 gold a day | a Necropolis bookkeeper's crown jewel; every entry in it is a promise |

## Relics

One of each in a game; their stories are the Wheel's. Not in chests: guarded, in a quest, or the reward of a campaign.

| Name | Slot | Effect | Story |
|---|---|---|---|
| Heart of the Wheel | Misc | +3 to every primary stat | a splinter of the mechanism, warm; the Conflux wardens say it is not theirs to guard |
| Gilded Hourglass | Misc | +1 level to every skill the hero has (max Master) | gold sand that never quite finishes falling; Academy scholars have studied it for six ages |
| Dawn Banner | Misc | +2 morale, own stacks may re-roll one failed morale roll per round | first raised at the Siege of the Last Light; Haven says the light was not the sun |
| Ember Crown | Head | +5 Spell Power, fire spells cost half mana | worn by the first Archdevil; the demons say it is a loan |
| Bone Dragon's Fang | Weapon | +6 Attack, melee attacks have a 10 % chance to deal double damage | a relic of a dragon that died in an earlier turning |
| Scale of the Elder Dragon | Body | +6 Defense, 25 % resistance to all spells | shed by a Firstborn dragon that remembers the world before the Wheel |
| Root of the Oldest Tree | Misc | own stacks regenerate 5 % of their health each round | Sylvan heartwood; the Treant that guarded it died with its roots in the soil |
| Mirror of the Lightless | Misc | enemy spells have a 20 % chance to be reflected at their caster | Dungeon-made: the dark does not forgive being looked at |
| Orcs' First Axe | Weapon | +5 Attack, +1 luck | a crude axe passed to every Stronghold chieftain; none has lost a fight with it |
| Eye of the Swamp | Neck | +2 luck, immune to the Mire spell | something looked back from the water; the cult says it is not a threat |

## Sets

Set pieces: wearing all of a set gives an extra bonus, listed after the individual ones.

| Set | Pieces | Individual effects | Full set |
|---|---|---|---|
| **Pilgrim's Road** | Pilgrim's Boots, Weathered Cloak, Hearth Charm | movement, terrain, gold | +20 % movement points, the whole terrain penalty gone (applied) |
| **Armor of the Dawn Covenant** | Plate of the Faithful, Aegis of Ash, Candle of the Pious | Defense, fire resistance, Spell Power | +2 morale (applied); Light and Nature spells cost 20 % less mana (not yet) |
| **Ash Court Regalia** | Ring of Reckoning, Sceptre of Cinders, Ledger of Debts | luck, Attack, gold | +2 Spell Power, +1 luck (applied); +1 level to offensive spells, healing from damage dealt (not yet) |
| **The Unbound's Trophies** | War-Drum, Marsh Talisman, Hunter's Mark | morale, terrain, luck | +2 morale, +2 luck (applied, always; the "only on non-native terrain" condition is not yet) |

## Implementation

The core is in place (ADR-061): table and catalog, hero equipment and backpack, equip, unequip and give commands, effects in
battle, movement, income and terrain, map pickups that roll by tier, loot, map format v11, editor session calls, the AI.
What is left: the hero screen and paper-doll UI, editor inspector rows, artifacts in chests, the "Not yet" effects.

## Open

- Numbers, after play-tests; whether Major artifacts need a guard.
- Which artifacts count as Relics, and whether a campaign can reuse them.
- Loot that does not fit in a full backpack is lost; the winner always takes all of a defeated hero's artifacts (user decision); when they do not all fit in its 50 slots, the most valuable come first (`ArtifactValue`).
- How sets work across towns (any set bonus for non-town heroes?).
- Whether a merchant town building could sell artifacts later, and whether chests may hold one.
- Which skills, artifacts and buildings should change morale and luck besides the artifacts (open question 5).
