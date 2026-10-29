# Artifacts

The artifact catalog (roadmap, "Artifacts"). **Implemented in core (ADR-061)**: the table `data/artifacts.csv` (with
`data/artifact_sets.csv`) and the rules for the effects the game has. Rules touched: ADR-018 (movement), ADR-025 (income sources), ADR-045
(terrain penalties), ADR-053 (morale and luck), ADR-055 (primary stats), ADR-068 (tiers and drops). Lore: [lore.md](lore.md).

**Re-draft (ADR-099, CONT-3, 2026-10-29)**: every artifact is now a lore item; the 31 HoMM3 placeholders are replaced by lore items with the
**same slot, tier and effect** (table below, so the table change is a rename), and 23 new items (90 in all) bring effects from the HoMM3, HoMM4, HoMM5
and Olden Era lists (sight, experience, mana, creature health, school orbs, spell tomes, no retreat, war machines, Necromancy, growth). **The
table still holds the first draft** until the roadmap item "Artifact table: the re-draft" is done; then the table is the truth for numbers
again (see "Editing artifacts" in [development.md](../technical/development.md)). Every name and number is a draft; the review is CONT-6 ❓.

**Status column**: ✅ the table can say it today (an existing effect kind); 🆕 needs a new effect kind; ⏳ needs magic (Phase 5) or sieges.
Where a row has both, the ✅ part is applied first.

## How artifacts work

- **Paper-doll slots** (a hero wears one artifact per slot, plus a backpack for the rest):

  | Slot | Count | Typical bonus |
  |---|---|---|
  | Head | 1 | Knowledge, Spell Power, skills |
  | Neck | 1 | morale, luck, mana, immunities |
  | Cloak | 1 | movement, Defense, spell protection |
  | Body | 1 | Defense, damage reduction |
  | Weapon | 1 | Attack, melee or ranged damage |
  | Shield | 1 | Defense, resistances |
  | Ring | 2 | luck, Spell Power, mana, daily resources |
  | Boots | 1 | movement, battle speed |
  | Misc | 3 | everything else |
  | Backpack | 50 | unworn artifacts; no effect |

- **Tiers** (ADR-068): **Minor** (common, small bonuses), **Major** (strong), **Relic** (big effects, with a story; drawn from a pool that
  is refilled when empty, so duplicates are possible). A set's pieces have their own tiers; wearing all of them adds the set's bonus.
  Chests hold a Minor artifact 5 % of the time, wagons and corpses Minor ones, the Warrior's Tomb up to a Relic; a full backpack loses what is
  found.
- **Stacking**: bonuses add; terrain artifacts and Pathfinding together never reduce the penalty below zero. The morale / luck cap of ±5
  stays (ADR-053); damage-taken reductions stop at 90 %.
- **Loot**: from the map (pickups, guarded), a defeated hero (the winner takes what fits), chests and loot sites (ADR-068), the Artifact
  Merchant (MAP-33, built) and the Sacrificial Altar's other side (MAP-29). Heroes hand artifacts to each other in the exchange.
- **New effect kinds** the re-draft needs (🆕): `sight 1` (scouting radius), `experience 5` (percent), `mana-a-day 1`, `mana-max 10`,
  `creature-health 10` (percent), `school-power arcana 25` (percent effect of one school), `spell-cost light 20` (percent less), `necromancy 5`
  (points), `growth 10` (percent in the owner's towns), `no-retreat`, `surrender-cost 30` (percent less), `machine-power 50`, `immune mind`,
  `ignore-distance-penalty`, `spells school 1-2` (known while worn), `no-spells-above 2` (both sides), `full-mana`.

## Minor artifacts

Worn by the first generation of Vael's soldiers, not much more than a keepsake. Carried by the many, held by few.

| Name (id) | Slot | Effect | Status | Flavour |
|---|---|---|---|---|
| Ash Sigil (`ash-sigil`) | Neck | +1 morale | ✅ | struck from a cold coal of the last Wheel fire; soldiers swear it is still warm |
| Charm of Embers (`charm-of-embers`) | Neck | +1 mana a day | 🆕 | a coal in a cage of wire, fed by whoever wears it |
| Pilgrim's Boots (`pilgrims-boots`) | Boots | +10 % movement points | ✅ | worn through three crusades; the soles remember every road |
| Weathered Cloak (`weathered-cloak`) | Cloak | −25 % of the non-native terrain penalty | ✅ | soaked in every terrain once |
| Iron Band (`iron-band`) | Ring | +1 luck | ✅ | a ring of plain iron: whoever lost it never returned for it |
| Courier's Ring (`couriers-ring`, was Ring of the Wayfarer) | Ring | +1 battle speed | ✅ | Academy messengers wear it between the peaks |
| Squire's Shield (`squires-shield`) | Shield | +1 Defense | ✅ | dented, honest and painted over three times |
| Covenant Buckler (`covenant-buckler`, was Shield of the Dwarven Lords) | Shield | +2 Defense | ✅ | the three suns of the Dawn Covenant, one of them scratched out |
| Rusted Blade (`rusted-blade`) | Weapon | +1 Attack | ✅ | an old sword; what it cut was worth the edge |
| Clan Cleaver (`clan-cleaver`, was Centaur's Ax) | Weapon | +2 Attack | ✅ | Stronghold axes are made to be lost and taken back |
| Gravewarden's Blade (`gravewardens-blade`, was Blackshard of the Dead Knight) | Weapon | +3 Attack | ✅ | the Necropolis arms its dead with what they were buried with |
| Scholar's Cap (`scholars-cap`) | Head | +1 Knowledge | ✅ | the Academy's apprentices wear it until they can afford better |
| Ash-Priest's Hood (`ash-priests-hood`, was Helm of the Alabaster Unicorn) | Head | +1 Knowledge | ✅ | grey wool, worn by the priests who tend the pyres |
| Lich-Scribe's Circlet (`lich-scribes-circlet`, was Skull Helmet) | Head | +2 Knowledge | ✅ | the scribe kept writing after he died; the circlet kept him company |
| Barkweave Vest (`barkweave-vest`, was Breastplate of Petrified Wood) | Body | +1 Defense | ✅ | Sylvan bark, given freely by a tree that was dying anyway |
| Bone Cuirass (`bone-cuirass`, was Rib Cage) | Body | +2 Defense | ✅ | ribs of an earlier owner, fitted to the next |
| Hearth Charm (`hearth-charm`) | Misc | +100 gold a day | ✅ | a coin worn smooth, said to always arrive on the right day |
| Woodcutter's Axe (`woodcutters-axe`) | Misc | +1 lumber a day | ✅ | rumoured to have felled a tree twice |
| Miner's Lamp (`miners-lamp`) | Misc | +1 ore a day | ✅ | the flame never goes out, even in the rain |
| Deepdelver's Pouch (`deepdelvers-pouch`, was Endless Pouch of Ore) | Misc | +2 ore a day | ✅ | Dungeon-sewn; the ore in it is never quite the ore that went in |
| Marsh Talisman (`marsh-talisman`) | Misc | swamp and shallow water are native | ✅ | strung from teeth; a Swamp Bestiary hunter's gift |
| Candle of the Pious (`candle-of-the-pious`) | Misc | +1 Spell Power | ✅ | wax and prayer; Haven's chapels burn these by the thousand |
| Outrider's Gloves (`outriders-gloves`, was Equestrian's Gloves) | Misc | +300 movement points | ✅ | Haven's outriders carry the news of every lost province first |
| Scout's Spyglass (`scouts-spyglass`) | Misc | +1 sight radius | 🆕 | brass from a drowned ruin; the lens shows what is there, mostly |
| Novice's Primer (`novices-primer`) | Misc | +5 % experience | 🆕 | every page annotated by a different dead student |
| Fletcher's Quiver (`fletchers-quiver`) | Misc | +5 % ranged damage | ✅ | Sylvan arrows; the fletcher's name is cut into each shaft |

## Major artifacts

Made by masters, bound by oaths, and fought over in every age.

| Name (id) | Slot | Effect | Status | Flavour |
|---|---|---|---|---|
| Crown of the Waning Eye (`crown-of-the-waning-eye`) | Head | +3 Knowledge, +1 level to the hero's best magic skill (the school with the most skill points; on a tie, the first in the ring order from the hero's town) | ✅ / ⏳ | forged when the sun first dimmed; the wearer sees the dark earlier than others |
| Frostglass Circlet (`frostglass-circlet`, was Helm of Chaos) | Head | +3 Knowledge | ✅ | Academy ice that does not melt, even on a lich |
| Hunter's Mark (`hunters-mark`) | Neck | +2 luck | ✅ | a bone token with a hole in it; the bearer's arrows find whatever hid |
| Pilgrim's Reliquary (`pilgrims-reliquary`, was Pendant of Courage) | Neck | +2 morale, +2 luck | ✅ | holds a finger bone of a saint, or of whoever was nearest |
| Torc of the Hunt (`torc-of-the-hunt`, was Necklace of Swiftness) | Neck | +1 battle speed | ✅ | Sylvan silver; the hunt it was made for never ended |
| Wardstone Amulet (`wardstone-amulet`) | Neck | the army is immune to `mind` spells | 🆕 ⏳ | a pebble from the Conflux's mechanism; thoughts slide off it |
| Statesman's Seal (`statesmans-seal`) | Neck | surrender costs 30 % less | 🆕 | the Academy signs every treaty with it, and keeps a copy |
| Mantle of the Last Grove (`mantle-of-the-last-grove`) | Cloak | grass and forest floor are native | ✅ | spun from moss that grows only in dying groves |
| Windrunner's Cloak (`windrunners-cloak`, was Cape of Velocity) | Cloak | +1 battle speed, +10 % movement points | ✅ | it pulls toward wherever the wind is going |
| Cloak of Silence (`cloak-of-silence`) | Cloak | no spell above level 2 can be cast in the hero's battles | 🆕 ⏳ | woven by the Dungeon from the dark between two words |
| Plate of the Faithful (`plate-of-the-faithful`) | Body | +4 Defense, −10 % ranged damage taken | ✅ | riveted in the cathedral-forges of Haven; the rivets are blessed, the plate is not comfortable |
| Drake-Scale Jerkin (`drake-scale-jerkin`, was Scales of the Greater Basilisk) | Body | +3 Defense | ✅ | the drake was not dead when the cult began to sew |
| Clanhide Harness (`clanhide-harness`, was Tunic of the Cyclops King) | Body | +4 Defense | ✅ | every clan that wore it added a strap |
| Brimstone Plate (`brimstone-plate`, was Breastplate of Brimstone) | Body | +5 Defense, −5 % damage taken | ✅ | still smoking; Inferno smiths call that "fresh" |
| Warden's Edge (`wardens-edge`) | Weapon | +4 Attack, +10 % melee damage | ✅ | a Sylvan blade-dancer's twin, the other half never found |
| Flail of the Penitent (`flail-of-the-penitent`, was Greater Gnoll's Flail) | Weapon | +4 Attack | ✅ | Haven's flagellants use it on themselves first |
| Warlord's Maul (`warlords-maul`, was Ogre's Club of Havoc) | Weapon | +5 Attack | ✅ | a stone on a log; the clans have never needed better |
| Harvester's Scythe (`harvesters-scythe`, was Sword of Hellfire) | Weapon | +6 Attack | ✅ | the Inferno reaps every age; this is the tool |
| Elvenwood Bow (`elvenwood-bow`) | Weapon | +10 % ranged damage, shooters ignore the distance penalty | ✅ / 🆕 | strung with the hair of an elf who forgot whose it was |
| Aegis of Ash (`aegis-of-ash`) | Shield | +4 Defense, the hero's stacks take −10 % fire damage | ✅ / ⏳ | quenched in the cinders of a burned city |
| Mire-Hide Shield (`mire-hide-shield`, was Buckler of the Gnoll King) | Shield | +4 Defense | ✅ | it stays wet; the swamp cults say that is how you know it is alive |
| Bulwark of the Last City (`bulwark-of-the-last-city`, was Targ of the Rampaging Ogre) | Shield | +5 Defense | ✅ | a gate of a Haven city that fell, cut down to a shield |
| Ring of Reckoning (`ring-of-reckoning`) | Ring | +2 luck, +1 morale | ✅ | Necropolis lawyers wear it; they prefer the winning side of the ledger |
| Ring of Pale Fire (`ring-of-pale-fire`) | Ring | +3 Spell Power | ✅ | a Chaos pact in a band of gold; the flame inside does not like being looked at |
| Ring of the Wellspring (`ring-of-the-wellspring`) | Ring | +2 mana a day, +10 maximum mana | 🆕 | Academy-cut from a spring that runs upward |
| Boots of the Wheel-Runner (`boots-of-the-wheel-runner`) | Boots | +20 % movement points, +1 battle speed | ✅ | stitched from the hide of a beast that ran every circuit of the Wheel |
| Outrider's Boots (`outriders-boots`, was Boots of Speed) | Boots | +600 movement points | ✅ | the outriders' second pair, for the last road |
| Sceptre of Cinders (`sceptre-of-cinders`) | Misc | +2 Attack, +2 Spell Power | ✅ | the Inferno's demons gift these, and always want them back |
| Amulet of the Deep (`amulet-of-the-deep`) | Misc | +2 Knowledge; with magic: +4 mana per Knowledge point instead | ✅ / 🆕 | dark-elves wear it to remember the surface; they say it helps them forget |
| War-Drum (`war-drum`) | Misc | +1 morale, own stacks start the battle with +10 % initiative | ✅ / 🆕 | Stronghold clans carry these to every war; the beat is older than the clan |
| Alchemist's Satchel (`alchemists-satchel`) | Misc | +1 of each rare resource a day | ✅ | Academy glassware; the vials never run dry, and never explain why |
| Ledger of Debts (`ledger-of-debts`) | Misc | +300 gold a day | ✅ | a Necropolis bookkeeper's crown jewel; every entry in it is a promise |
| Tithe Box (`tithe-box`, was Endless Purse of Gold) | Misc | +500 gold a day | ✅ | Haven collects for the light; the box is never empty, and never full |
| Coffer of the Ashen Court (`coffer-of-the-ashen-court`, was Endless Bag of Gold) | Misc | +750 gold a day | ✅ | paid into by every age that ended |
| Grove-Warden's Bundle (`grove-wardens-bundle`, was Endless Pouch of Lumber) | Misc | +3 lumber a day | ✅ | windfall only; the wardens never cut |
| Elixir of the Grove (`elixir-of-the-grove`) | Misc | the army's creatures +10 % health | 🆕 | sap from a tree the Sylvan will not name |
| Engineer's Toolbelt (`engineers-toolbelt`) | Misc | war machines +50 % health and damage | 🆕 | Haven's siege-masters never take it off, even to pray |
| Chain of the Unyielding (`chain-of-the-unyielding`) | Misc | neither side may retreat or surrender in the hero's battles | 🆕 | Stronghold chains a chieftain to the field; the field remembers |
| Cartographer's Lens (`cartographers-lens`) | Misc | +2 sight radius | 🆕 | ground in the Academy, focused on the edge of every map |
| Six school orbs: Orb of the Lamp (Light), of the Root (Nature), of the Glass (Arcana), of the Ledger (Death), of Embers (Chaos), of the Void (Shadow) (`orb-of-the-lamp`, …) | Misc | +25 % effect of that school's spells (damage, healing, summoned amount) | ⏳ | one for each town of the ring; the Firstborn made seven, and do not say where the seventh is |

## Relics

One of each in a game is likely (the pool refills when empty, ADR-068); their stories are the Wheel's. Not in chests: guarded, in a quest,
a bank, the Warrior's Tomb, or the reward of a campaign.

| Name (id) | Slot | Effect | Status | Story |
|---|---|---|---|---|
| Heart of the Wheel (`heart-of-the-wheel`) | Misc | +3 to every primary stat | ✅ | a splinter of the mechanism, warm; the Conflux wardens say it is not theirs to guard |
| Gilded Hourglass (`gilded-hourglass`) | Misc | +1 level to every skill the hero has (max Master) | ✅ | gold sand that never quite finishes falling; Academy scholars have studied it for six ages |
| Dawn Banner (`dawn-banner`) | Misc | +2 morale; own stacks may re-roll one failed morale roll a round | ✅ / 🆕 | first raised at the Siege of the Last Light; Haven says the light was not the sun |
| Ember Crown (`ember-crown`) | Head | +5 Spell Power; Chaos spells cost half | ✅ / ⏳ | worn by the first Archdevil; the demons say it is a loan |
| Diadem of the Wheel's Scholars (`diadem-of-the-wheels-scholars`, was Crown of the Supreme Magi) | Head | +4 Knowledge, +2 Spell Power | ✅ | every scholar who wore it wrote one page of the same book |
| Bone Dragon's Fang (`bone-dragons-fang`) | Weapon | +6 Attack; melee attacks have a 10 % chance of double damage | ✅ / 🆕 | a relic of a dragon that died in an earlier turning |
| Blade of the Turning (`blade-of-the-turning`, was Titan's Gladius) | Weapon | +12 Attack, −3 Defense | ✅ | it cut the first age from the second; nobody has cleaned it since |
| Orcs' First Axe (`orcs-first-axe`) | Weapon | +5 Attack, +1 luck | ✅ | passed to every Stronghold chieftain; none has lost a fight with it |
| Scale of the Elder Dragon (`scale-of-the-elder-dragon`) | Body | +6 Defense; the army takes −25 % spell damage | ✅ / ⏳ | shed by a Firstborn dragon that remembers the world before the Wheel |
| Conflux Carapace (`conflux-carapace`, was Titan's Cuirass) | Body | +10 Defense, −2 Spell Power | ✅ | a plate of the Wheel's housing; magic does not like to touch it |
| Wall of the First Age (`wall-of-the-first-age`, was Sentinel's Shield) | Shield | +12 Defense, −3 Attack | ✅ | a stone from the first city's wall, still standing somewhere without it |
| Root of the Oldest Tree (`root-of-the-oldest-tree`) | Misc | own stacks regain 5 % of their health each round (for now: −5 % damage taken) | ✅ / 🆕 | Sylvan heartwood; the treant that guarded it died with its roots in the soil |
| Mirror of the Lightless (`mirror-of-the-lightless`) | Misc | +2 Defense, +1 luck; enemy spells are reflected at their caster with 20 % | ✅ / ⏳ | Dungeon-made: the dark does not forgive being looked at |
| Eye of the Swamp (`eye-of-the-swamp`) | Neck | +2 luck; the army is immune to `poison` | ✅ / 🆕 | something looked back from the water; the cult says it is not a threat |
| Hoard of the Last Dragon (`hoard-of-the-last-dragon`, was Endless Sack of Gold) | Misc | +1000 gold a day | ✅ | the dragon is not quite the last, and wants it back |
| Seed of the World Tree (`seed-of-the-world-tree`) | Misc | +1 of every resource a day (+100 gold) | ✅ | it will not sprout on Vael; the Sylvan keep trying |
| Wellspring Chalice (`wellspring-chalice`) | Misc | the hero's mana is full at the start of each turn | 🆕 | the Academy built a city around it; the city froze, the chalice did not |
| Unwritten Codex (`unwritten-codex`) | Misc | the hero knows every level 1 and 2 spell of every school while worn (with a spell book) | ⏳ | the pages fill as they are read and are blank again when it is closed |
| Banner of the Legion (`banner-of-the-legion`) | Misc | +10 % creature growth in the owner's towns | 🆕 | carried by the army that held the last lit city for a year |

## Sets

Set pieces have their own effects (above); wearing **every** piece of a set adds the set's bonus.

| Set (id) | Pieces | Full set | Status |
|---|---|---|---|
| **Pilgrim's Road** (`pilgrims-road`) | Pilgrim's Boots, Weathered Cloak, Hearth Charm | +20 % movement points, the whole terrain penalty gone | ✅ |
| **Armor of the Dawn Covenant** (`dawn-covenant-armor`) | Plate of the Faithful, Aegis of Ash, Candle of the Pious | +2 morale; Light and Nature spells cost 20 % less | ✅ / ⏳ |
| **Ashen Court Regalia** (`ash-court-regalia`, renamed from "Ash Court Regalia" to match the lore) | Ring of Reckoning, Sceptre of Cinders, Ledger of Debts | +2 Spell Power, +1 luck; +1 rank to Chaos and Death damage spells while worn | ✅ / ⏳ |
| **The Unbound's Trophies** (`unbound-trophies`) | War-Drum, Marsh Talisman, Hunter's Mark | +2 morale, +2 luck while fighting on non-native terrain (for now always) | ✅ / 🆕 |
| **Shroud of the Last Debt** (`shroud-of-the-last-debt`) | Gravewarden's Blade, Bone Cuirass, Lich-Scribe's Circlet | +10 Necromancy points (HoMM3's Cloak of the Undead King); raised creatures may be one tier higher | 🆕 |
| **The Last Grove's Bow** (`last-groves-bow`) | Elvenwood Bow, Torc of the Hunt, Fletcher's Quiver | own shooters +2 shots and +10 % ranged damage | ✅ / 🆕 |
| **Regalia of the Firstborn** (`firstborn-regalia`) | Blade of the Turning, Wall of the First Age, Conflux Carapace, Diadem of the Wheel's Scholars | the penalties of the pieces go away; the army is immune to `mind` | 🆕 ⏳ |

## Implementation

The core is in place (ADR-061): table and catalog, hero equipment and backpack, equip, unequip and give commands, effects in
battle, movement, income and terrain, map pickups that roll by tier, loot, map format v11, editor session calls, the AI,
and the hero screen with the paper doll and backpack (`src/Godot/EternalWars.Godot.UI/Hud/HeroPanel.cs`). The re-draft's steps
(roadmap, "Artifacts"): rename the placeholders (ids and names; tests that name them follow), add the ✅ rows, then each new effect kind with
its rows (🆕), and the magic effects with Phase 5 (⏳).

## Open

- The review of the re-draft (names, which effects, the new effect kinds): CONT-6 ❓.
- Numbers, after play-tests; whether Major artifacts need a guard.
- Whether a campaign can carry Relics between maps (FLOW-1).
