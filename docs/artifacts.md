# Artifacts

The artifact catalog for Phase 3 (roadmap, "Artifacts"). **Nothing here is implemented yet**; this is the content the
artifact table `data/artifacts.csv` (id, name, slot, tier, effects) will start from. Rules touched: ADR-018 (movement),
ADR-025 (income sources), ADR-039 (treasure chests), ADR-045 (terrain penalties), ADR-053 (morale and luck),
ADR-055 (primary stats). Lore: [lore.md](lore.md). Every name and number is a draft to react to; effects use only
things the game has or plans: primary stats, movement, battle speed, morale and luck, skill levels, daily resources,
terrain penalties, spell power and mana (with magic, [spells.md](spells.md)).

## How artifacts work (proposal)

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
  | Backpack | 12 | unworn artifacts; no effect |

- **Tiers** (what a chest, a guard or a defeated hero's pack can drop): **Minor** (common, small bonuses),
  **Major** (strong), **Relic** (one of each per game, big effects, with a story), and **Set pieces** (worn together
  for a bonus). The HoMM3 chest "artifact tier" of ADR-039 picks a tier; **Relics** are not in chests.
- **Stacking**: bonuses add; terrain artifacts and Pathfinding together never reduce the penalty below zero. The
  morale/luck cap of ±5 stays (ADR-053).
- **Loot**: artifacts come from the map (pickup objects, guarded), chests, and from a defeated hero's pack (the winner
  may take any one; open question). Owned artifacts move through the exchange screen.
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
| **Pilgrim's Road** | Pilgrim's Boots, Weathered Cloak, Hearth Charm | movement, terrain, gold | +20 % movement points, the hero's army is native to every terrain it walks on |
| **Armor of the Dawn Covenant** | Plate of the Faithful, Aegis of Ash, Candle of the Pious | Defense, fire resistance, Spell Power | +2 morale, Light and Nature spells cost 20 % less mana |
| **Ash Court Regalia** | Ring of Reckoning, Sceptre of Cinders, Ledger of Debts | luck, Attack, gold | +1 level to all offensive spells, the hero's stacks heal for 5 % of the damage they deal |
| **The Unbound's Trophies** | War-Drum, Marsh Talisman, Hunter's Mark | morale, terrain, luck | +3 morale and luck while fighting on non-native terrain |

## Notes for implementation

- The table needs: `id`, `name`, `slot`, `tier`, `set` (empty = none), `effects` (a list of `kind value`: `attack 2`,
  `movement 20`, `income gold 100`, `skill-level best-magic 1`, …), `notes`.
- Effects map onto existing rules: attack/defense (hero stats), `HeroMovement` (movement percent), `HeroIncome` /
  `IIncomeSource` (daily resources), terrain penalty (`MovementAllowance`), morale/luck (`HeroBattleBonuses`), skill
  levels (`HeroSkills`). Spell effects wait for magic.
- Commands: equip, unequip, move to the backpack, give to another hero (exchange screen); events are past-tense records
  (`ArtifactEquipped`, …); the map format and editor get an artifact on a hero and an artifact on the map (bump the
  format).
- Artifacts on the map, in chests, and as loot are Phase 3 items in the roadmap.

## Open

- Numbers, after play-tests; whether Major artifacts need a guard.
- Which artifacts count as Relics, and whether a campaign can reuse them.
- Whether a winner takes one artifact from a defeated hero, and which.
- How sets work across towns (any set bonus for non-town heroes?).
- Whether a merchant town building could sell artifacts later.
- The artifacts' effect on morale and luck (open question 5).
