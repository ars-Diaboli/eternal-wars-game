# Artifacts

The artifact catalog (roadmap, "Artifacts"). **Implemented in core (ADR-061)**: the table `data/artifacts.csv` (with
`data/artifact_sets.csv`) and the rules for the effects the game has. Rules touched: ADR-018 (movement), ADR-025 (income sources), ADR-045
(terrain penalties), ADR-053 (morale and luck), ADR-055 (primary stats), ADR-068 (tiers and drops). Lore: [lore.md](lore.md).

**Re-draft (ADR-099, ADR-100, CONT-3, CONT-6, 2026-10-29)**: names and flavour follow **HoMM3 and HoMM4** (CONT-6 a). The 31 HoMM3
placeholders keep their names and ids, except the two gnoll items (Vael has no gnolls; they become lizardmen's). The first-draft lore items
keep their names where HoMM has nothing close and take the HoMM name where it has (Ash Sigil → Badge of Courage). New effect kinds are only
those the owner chose (CONT-6 b); the items and secondary effects that needed others are dropped (list at the end). The Unwritten Codex and
the Recanter's Cloak are Relics; the other strong items are **assembled relics** (CONT-6 c), HoMM3's combination artifacts. **The table still
holds this re-draft** since ADR-111 (101 artifacts, 5 sets): the table is the truth for numbers again (see "Editing artifacts" in
[development.md](../technical/development.md)). Every number is a draft. Still missing: the assembled relics themselves (their pieces are in) and
the Crown of the Waning Eye's +1 best magic skill.

**Status column** (the draft's plan; since ADR-111 every 🆕 and ⏳ row is built except the assembled relics and the Crown's skill level): ✅ an
effect kind that existed; 🆕 a new effect kind; ⏳ needed magic (Phase 5).
Where a row has both, the ✅ part is applied first. **Was** names the first re-draft's (ADR-099) name or id where it changed.

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
- **Assembled relics** (ADR-100, ADR-108, CONT-6 c, CONT-7; HoMM3's combination artifacts): pieces that a hero wearing all of them may join
  into one Relic. The relic keeps every piece's effect and adds its own; it sits in the first piece's slot and the other pieces' slots stay
  blocked. It is never found whole: only its pieces drop, by their own tiers.
  - **Joining**: the game asks when the hero puts on the last piece (HoMM3), and the hero screen has an "Assemble" button too.
  - **Taking apart**: any time on the hero screen; the pieces go back to their slots.
  - **Loot and trade**: a defeated hero's relic goes to the winner whole when its slots are free, otherwise as pieces into the backpack. The
    Artifact Merchant and the Sacrificial Altar take and sell pieces only.
  - **AI**: always joins, and values a piece more when it already has the others.
- **Stacking**: bonuses add; terrain artifacts and Pathfinding together never reduce the penalty below zero. The morale / luck cap of ±5
  stays (ADR-053); damage-taken reductions stop at 90 %.
- **Loot**: from the map (pickups, guarded), a defeated hero (the winner takes what fits), chests and loot sites (ADR-068), the Artifact
  Merchant (MAP-33, built) and the Sacrificial Altar's other side (MAP-29). Heroes hand artifacts to each other in the exchange.
- **New effect kinds** (🆕, CONT-6 b), in the owner's order: `sight 1` (scouting radius), `mana-a-day 1` and `mana-max 10`,
  `creature-health 10` (percent), `element-power fire 25` (the elemental orbs: percent effect of one element's spells, ADR-106; was `school-power`), `spells 1-2` (known
  while worn, with a spell book), `no-spells-above 2` (both sides), `no-retreat` (both sides: no retreat, no surrender), `necromancy 5`
  (points), `growth 10` (percent in the owner's towns), `immune mind` (a spell tag). The assembled Wizard's Well also needs `full-mana`, and the Angelic Alliance `no-faction-penalty` (ADR-108). All of them are
  built (ADR-111), the magic ones too.

## Minor artifacts

Worn by the first generation of Vael's soldiers, not much more than a keepsake. Carried by the many, held by few.

| Name (id) | Slot | Effect | Status | Flavour |
|---|---|---|---|---|
| Badge of Courage (`badge-of-courage`, was Ash Sigil) | Neck | +1 morale | ✅ | struck from a cold coal of the last Wheel fire; soldiers swear it is still warm |
| Charm of Mana (`charm-of-mana`, was Charm of Embers) | Neck | +1 mana a day; a piece of the Wizard's Well | 🆕 | a coal in a cage of wire, fed by whoever wears it |
| Pilgrim's Boots (`pilgrims-boots`) | Boots | +10 % movement points | ✅ | worn through three crusades; the soles remember every road |
| Weathered Cloak (`weathered-cloak`) | Cloak | −25 % of the non-native terrain penalty | ✅ | soaked in every terrain once |
| Iron Band (`iron-band`) | Ring | +1 luck | ✅ | a ring of plain iron: whoever lost it never returned for it |
| Ring of the Wayfarer (`ring-of-the-wayfarer`) | Ring | +1 battle speed | ✅ | Academy messengers wear it between the peaks |
| Squire's Shield (`squires-shield`) | Shield | +1 Defense | ✅ | dented, honest and painted over three times |
| Shield of the Dwarven Lords (`shield-of-the-dwarven-lords`) | Shield | +2 Defense | ✅ | the dwarves sold their last shields to Haven, and then their halls |
| Rusted Blade (`rusted-blade`) | Weapon | +1 Attack | ✅ | an old sword; what it cut was worth the edge |
| Centaur's Axe (`centaurs-ax`) | Weapon | +2 Attack | ✅ | Stronghold axes are made to be lost and taken back |
| Blackshard of the Dead Knight (`blackshard-of-the-dead-knight`) | Weapon | +3 Attack; a piece of the Regalia of the Undead King | ✅ | the Necropolis arms its dead with what they were buried with |
| Scholar's Cap (`scholars-cap`) | Head | +1 Knowledge | ✅ | the Academy's apprentices wear it until they can afford better |
| Helm of the Alabaster Unicorn (`helm-of-the-alabaster-unicorn`) | Head | +1 Knowledge | ✅ | the Sylvan carved it for a unicorn's rider, who did not come back |
| Skull Helmet (`skull-helmet`) | Head | +2 Knowledge; a piece of the Regalia of the Undead King | ✅ | the scribe kept writing after he died; the helmet kept him company |
| Breastplate of Petrified Wood (`breastplate-of-petrified-wood`) | Body | +1 Defense | ✅ | Sylvan bark, given freely by a tree that was dying anyway |
| Rib Cage (`rib-cage`) | Body | +2 Defense; a piece of the Regalia of the Undead King | ✅ | ribs of an earlier owner, fitted to the next |
| Hearth Charm (`hearth-charm`) | Misc | +100 gold a day | ✅ | a coin worn smooth, said to always arrive on the right day |
| Woodcutter's Axe (`woodcutters-axe`) | Misc | +1 lumber a day | ✅ | rumoured to have felled a tree twice |
| Miner's Lamp (`miners-lamp`) | Misc | +1 ore a day | ✅ | the flame never goes out, even in the rain |
| Endless Pouch of Ore (`endless-pouch-of-ore`) | Misc | +2 ore a day | ✅ | Dungeon-sewn; the ore in it is never quite the ore that went in |
| Marsh Talisman (`marsh-talisman`) | Misc | swamp and shallow water are native | ✅ | strung from teeth; a Swamp Bestiary hunter's gift |
| Candle of the Pious (`candle-of-the-pious`) | Misc | +1 Spell Power | ✅ | wax and prayer; Haven's chapels burn these by the thousand |
| Equestrian's Gloves (`equestrians-gloves`) | Misc | +300 movement points | ✅ | Haven's outriders carry the news of every lost province first |
| Spyglass (`spyglass`, was Scout's Spyglass) | Misc | +1 sight radius | 🆕 | brass from a drowned ruin; the lens shows what is there, mostly |
| Bowstring of the Unicorn's Mane (`bowstring-of-the-unicorns-mane`, was Fletcher's Quiver) | Misc | +5 % ranged damage; a piece of the Bow of the Sharpshooter | ✅ | the unicorn gave it; the Sylvan do not say what they gave back |
| Ring of Vitality (`ring-of-vitality`, ADR-108) | Ring | the army's creatures +5 % health; a piece of the Elixir of Life | 🆕 | a green band; the Sylvan say it was a vine, and still is |
| Collar of Conjuring (`collar-of-conjuring`, ADR-108) | Neck | +1 Spell Power; a piece of the Ring of the Magi | ✅ | an apprentice's collar, worn until the spells stop slipping |
| Ring of Conjuring (`ring-of-conjuring`, ADR-108) | Ring | +1 Spell Power; a piece of the Ring of the Magi | ✅ | Academy brass, engraved on the inside with a proof nobody has finished |
| Cape of Conjuring (`cape-of-conjuring`, ADR-108) | Cloak | +1 Spell Power; a piece of the Ring of the Magi | ✅ | the lining is a star chart of a sky Vael no longer has |

## Major artifacts

Made by masters, bound by oaths, and fought over in every age.

| Name (id) | Slot | Effect | Status | Flavour |
|---|---|---|---|---|
| Crown of the Waning Eye (`crown-of-the-waning-eye`) | Head | +3 Knowledge, +1 level to the hero's best magic skill (the school with the most skill points; on a tie, the first in the ring order from the hero's town) | ✅ / ⏳ | forged when the sun first dimmed; the wearer sees the dark earlier than others |
| Helm of Chaos (`helm-of-chaos`) | Head | +3 Knowledge | ✅ | Inferno iron; it thinks along with the wearer, and disagrees |
| Hunter's Mark (`hunters-mark`) | Neck | +2 luck | ✅ | a bone token with a hole in it; the bearer's arrows find whatever hid |
| Pendant of Courage (`pendant-of-courage`) | Neck | +2 morale, +2 luck | ✅ | holds a finger bone of a saint, or of whoever was nearest |
| Necklace of Swiftness (`necklace-of-swiftness`) | Neck | +1 battle speed; a piece of the Bow of the Sharpshooter | ✅ | Sylvan silver; the hunt it was made for never ended |
| Pendant of Free Will (`pendant-of-free-will`, was Wardstone Amulet) | Neck | the army is immune to `mind` spells | 🆕 ⏳ | a pebble from the Conflux's mechanism; thoughts slide off it |
| Talisman of Mana (`talisman-of-mana`) | Misc | +2 mana a day; a piece of the Wizard's Well | 🆕 | Academy glass with a spark in it that never settles |
| Mystic Orb of Mana (`mystic-orb-of-mana`) | Misc | +3 mana a day; a piece of the Wizard's Well | 🆕 | it hums at dawn, the only thing on Vael that still does |
| Mantle of the Last Grove (`mantle-of-the-last-grove`) | Cloak | grass and forest floor are native | ✅ | spun from moss that grows only in dying groves |
| Cape of Velocity (`cape-of-velocity`) | Cloak | +1 battle speed, +10 % movement points | ✅ | it pulls toward wherever the wind is going |
| Plate of the Faithful (`plate-of-the-faithful`) | Body | +4 Defense, −10 % ranged damage taken | ✅ | riveted in the cathedral-forges of Haven; the rivets are blessed, the plate is not comfortable |
| Scales of the Greater Basilisk (`scales-of-the-greater-basilisk`) | Body | +3 Defense | ✅ | the basilisk was not dead when the cult began to sew |
| Tunic of the Cyclops King (`tunic-of-the-cyclops-king`) | Body | +4 Defense | ✅ | every clan that wore it added a strap |
| Breastplate of Brimstone (`breastplate-of-brimstone`) | Body | +5 Defense, −5 % damage taken | ✅ | still smoking; Inferno smiths call that "fresh" |
| Warden's Edge (`wardens-edge`) | Weapon | +4 Attack, +10 % melee damage | ✅ | a Sylvan blade-dancer's twin, the other half never found |
| Lizard Warrior's Flail (`lizard-warriors-flail`, was Greater Gnoll's Flail) | Weapon | +4 Attack | ✅ | swung by the swamp's champions; the spikes are teeth |
| Ogre's Club of Havoc (`ogres-club-of-havoc`) | Weapon | +5 Attack | ✅ | a stone on a log; the clans have never needed better |
| Sword of Hellfire (`sword-of-hellfire`) | Weapon | +6 Attack | ✅ | the Inferno reaps every age; this is the tool |
| Bow of Elven Cherrywood (`bow-of-elven-cherrywood`, was Elvenwood Bow) | Weapon | +10 % ranged damage; a piece of the Bow of the Sharpshooter | ✅ | strung with the hair of an elf who forgot whose it was |
| Aegis of Ash (`aegis-of-ash`) | Shield | +4 Defense, −5 % damage taken | ✅ | quenched in the cinders of a burned city |
| Buckler of the Lizard King (`buckler-of-the-lizard-king`, was Buckler of the Gnoll King) | Shield | +4 Defense | ✅ | it stays wet; the swamp cults say that is how you know it is alive |
| Targ of the Rampaging Ogre (`targ-of-the-rampaging-ogre`) | Shield | +5 Defense | ✅ | a gate of a Haven city that fell, cut down to a shield |
| Ring of Reckoning (`ring-of-reckoning`) | Ring | +2 luck, +1 morale | ✅ | Necropolis lawyers wear it; they prefer the winning side of the ledger |
| Ring of Pale Fire (`ring-of-pale-fire`) | Ring | +3 Spell Power | ✅ | a Chaos pact in a band of gold; the flame inside does not like being looked at |
| Ring of the Wellspring (`ring-of-the-wellspring`) | Ring | +2 mana a day, +10 maximum mana | 🆕 | Academy-cut from a spring that runs upward |
| Boots of the Wheel-Runner (`boots-of-the-wheel-runner`) | Boots | +20 % movement points, +1 battle speed | ✅ | stitched from the hide of a beast that ran every circuit of the Wheel |
| Boots of Speed (`boots-of-speed`) | Boots | +600 movement points | ✅ | the outriders' second pair, for the last road |
| Sceptre of Cinders (`sceptre-of-cinders`) | Misc | +2 Attack, +2 Spell Power | ✅ | the Inferno's demons gift these, and always want them back |
| Amulet of the Deep (`amulet-of-the-deep`) | Misc | +2 Knowledge, +10 maximum mana | ✅ / 🆕 | dark-elves wear it to remember the surface; they say it helps them forget |
| War-Drum (`war-drum`) | Misc | +1 morale | ✅ | Stronghold clans carry these to every war; the beat is older than the clan |
| Alchemist's Satchel (`alchemists-satchel`) | Misc | +1 of each rare resource a day | ✅ | Academy glassware; the vials never run dry, and never explain why |
| Ledger of Debts (`ledger-of-debts`) | Misc | +300 gold a day | ✅ | a Necropolis bookkeeper's crown jewel; every entry in it is a promise |
| Endless Purse of Gold (`endless-purse-of-gold`) | Misc | +500 gold a day | ✅ | Haven collects for the light; the purse is never empty, and never full |
| Endless Bag of Gold (`endless-bag-of-gold`) | Misc | +750 gold a day | ✅ | paid into by every age that ended |
| Endless Pouch of Lumber (`endless-pouch-of-lumber`) | Misc | +3 lumber a day | ✅ | windfall only; the wardens never cut |
| Vial of Lifeblood (`vial-of-lifeblood`, was Elixir of the Grove) | Misc | the army's creatures +10 % health; a piece of the Elixir of Life | 🆕 | sap from a tree the Sylvan will not name |
| Speculum (`speculum`, was Cartographer's Lens) | Misc | +2 sight radius | 🆕 | ground in the Academy, focused on the edge of every map |
| Head, Torso and Legs of Legion (`head-of-legion`, `torso-of-legion`, `legs-of-legion`, were the Banner of the Legion) | Misc | +5 % creature growth in the owner's towns each; the pieces of the Statue of Legion | 🆕 | a statue of the army that held the last lit city for a year, broken up so that no one lord would own it |
| Collar, Shackle and Chain of War (`collar-of-war` Neck, `shackle-of-war` Misc, `chain-of-war` Misc, were the Chain of the Unyielding) | as given | +2 Defense; +2 Attack; +1 morale; the pieces of the Shackles of War | ✅ | Stronghold chains a chieftain to the field; the field remembers |
| Ring of Life (`ring-of-life`, ADR-108) | Ring | the army's creatures +5 % health, +1 morale; a piece of the Elixir of Life | 🆕 | Haven's healers pass it hand to hand down the line of the wounded |
| Everflowing Crystal Cloak (`everflowing-crystal-cloak`, ADR-108) | Cloak | +1 crystal a day; a piece of the Cornucopia | ✅ | it sheds a shard at every dawn and is never the lighter for it |
| Eversmoking Ring of Sulfur (`eversmoking-ring-of-sulfur`, ADR-108) | Ring | +1 sulfur a day; a piece of the Cornucopia | ✅ | the Inferno's smiths wear it to keep their forges honest |
| Everpouring Vial of Mercury (`everpouring-vial-of-mercury`, ADR-108) | Misc | +1 mercury a day; a piece of the Cornucopia | ✅ | the stopper is the hard part; nobody has made a second |
| Sword of Judgement (`sword-of-judgement`, ADR-108) | Weapon | +4 Attack, +1 morale; a piece of the Angelic Alliance | ✅ | Haven forged it to judge the dead; it has never found one innocent |
| Lion's Shield of Courage (`lions-shield-of-courage`, ADR-108) | Shield | +4 Defense, +1 morale; a piece of the Angelic Alliance | ✅ | the lion on it was painted over a dragon, and over something older under that |
| Celestial Necklace of Bliss (`celestial-necklace-of-bliss`, ADR-108) | Neck | +2 Spell Power, +2 Knowledge; a piece of the Angelic Alliance | ✅ | strung with the last light of the sun, which is why the sun is dimmer |
| Four elemental orbs (ADR-106, MAG-13; were six school orbs): Orb of the Firmament (air), Orb of Silt (earth), Orb of Tempestuous Fire (fire), Orb of Driving Rain (water) (`orb-of-the-firmament`, `orb-of-silt`, `orb-of-tempestuous-fire`, `orb-of-driving-rain`) | Misc | +25 % effect of that element's spells, of any school (damage, healing, summoned amount; [spells.md](spells.md), "Element" column) | 🆕 ⏳ | the Firstborn made one for each of the four things the world is made of, and do not say what the fifth would be |

## Relics

One of each in a game is likely (the pool refills when empty, ADR-068); their stories are the Wheel's. Not in chests: guarded, in a quest,
a bank, the Warrior's Tomb, or the reward of a campaign.

| Name (id) | Slot | Effect | Status | Story |
|---|---|---|---|---|
| Heart of the Wheel (`heart-of-the-wheel`) | Misc | +3 to every primary stat | ✅ | a splinter of the mechanism, warm; the Conflux wardens say it is not theirs to guard |
| Gilded Hourglass (`gilded-hourglass`) | Misc | +1 level to every skill the hero has (max Master) | ✅ | gold sand that never quite finishes falling; Academy scholars have studied it for six ages |
| Dawn Banner (`dawn-banner`) | Misc | +2 morale, +1 luck | ✅ | first raised at the Siege of the Last Light; Haven says the light was not the sun |
| Ember Crown (`ember-crown`) | Head | +6 Spell Power | ✅ | worn by the first Archdevil; the demons say it is a loan |
| Crown of the Supreme Magi (`crown-of-the-supreme-magi`) | Head | +4 Knowledge, +2 Spell Power | ✅ | every scholar who wore it wrote one page of the same book |
| Bone Dragon's Fang (`bone-dragons-fang`) | Weapon | +6 Attack, +2 luck | ✅ | a relic of a dragon that died in an earlier turning |
| Titan's Gladius (`titans-gladius`) | Weapon | +12 Attack, −3 Defense | ✅ | it cut the first age from the second; nobody has cleaned it since |
| Orcs' First Axe (`orcs-first-axe`) | Weapon | +5 Attack, +1 luck | ✅ | passed to every Stronghold chieftain; none has lost a fight with it |
| Dragon Scale Armor (`dragon-scale-armor`, was Scale of the Elder Dragon) | Body | +6 Defense, +2 Attack | ✅ | shed by a Firstborn dragon that remembers the world before the Wheel |
| Titan's Cuirass (`titans-cuirass`) | Body | +10 Defense, −2 Spell Power | ✅ | a plate of the Wheel's housing; magic does not like to touch it |
| Sentinel's Shield (`sentinels-shield`) | Shield | +12 Defense, −3 Attack | ✅ | a stone from the first city's wall, still standing somewhere without it |
| Recanter's Cloak (`recanters-cloak`, was Cloak of Silence, Major) | Cloak | no spell above level 2 can be cast in the hero's battles | 🆕 ⏳ | woven by the Dungeon from the dark between two words |
| Root of the Oldest Tree (`root-of-the-oldest-tree`) | Misc | −5 % damage taken, the army's creatures +10 % health | ✅ / 🆕 | Sylvan heartwood; the treant that guarded it died with its roots in the soil |
| Mirror of the Lightless (`mirror-of-the-lightless`) | Misc | +3 Defense, +2 luck | ✅ | Dungeon-made: the dark does not forgive being looked at |
| Eye of the Swamp (`eye-of-the-swamp`) | Neck | +2 luck; the army is immune to `poison` | ✅ / 🆕 | something looked back from the water; the cult says it is not a threat |
| Endless Sack of Gold (`endless-sack-of-gold`) | Misc | +1000 gold a day | ✅ | the last dragon's hoard; the dragon is not quite the last, and wants it back |
| Seed of the World Tree (`seed-of-the-world-tree`) | Misc | +1 of every resource a day (+100 gold) | ✅ | it will not sprout on Vael; the Sylvan keep trying |
| Unwritten Codex (`unwritten-codex`) | Misc | the hero knows every level 1 and 2 spell of every school while worn (with a spell book) | 🆕 ⏳ | the pages fill as they are read and are blank again when it is closed |

## Assembled relics

Joined from their pieces (above) by a hero who wears all of them (ADR-100, ADR-108). Each keeps its pieces' effects and adds the line below.
Not in the Relic pool: only the pieces drop. The six added by ADR-108 (CONT-7 f: HoMM3's combinations, and the bow and Necromancy sets made
into relics) are drafts; their numbers wait for play-testing.

| Name (id) | Pieces | Added effect | Status | Story |
|---|---|---|---|---|
| Wizard's Well (`wizards-well`, was Wellspring Chalice) | Charm of Mana, Talisman of Mana, Mystic Orb of Mana (+6 mana a day) | the hero's mana is full at the start of each turn | 🆕 | the Academy built a city around it; the city froze, the well did not |
| Statue of Legion (`statue-of-legion`, was Banner of the Legion) | Head, Torso and Legs of Legion (+15 % growth) | +35 % more creature growth in the owner's towns (+50 % in all) | 🆕 | whole again, it remembers the year it held the city, and the city's levies come back to it |
| Shackles of War (`shackles-of-war`, was Chain of the Unyielding) | Collar, Shackle and Chain of War (+2 Attack, +2 Defense, +1 morale) | neither side may retreat or surrender in the hero's battles | 🆕 | the chieftain who wore all three never left a field; neither did anyone else |
| Elixir of Life (`elixir-of-life`, ADR-108) | Ring of Vitality, Ring of Life, Vial of Lifeblood (+20 % health, +1 morale) | the army's creatures +10 % health more (+30 % in all) | 🆕 | the Sylvan brewed it once, for a war they then did not have to fight |
| Ring of the Magi (`ring-of-the-magi`, ADR-108) | Collar, Ring and Cape of Conjuring (+3 Spell Power) | +3 Spell Power more (+6 in all; Spell Power also lengthens spells, ADR-072) | ✅ | the Academy's first chancellor wore it, and every chancellor since has claimed to |
| Cornucopia (`cornucopia`, ADR-108) | Everflowing Crystal Cloak, Eversmoking Ring of Sulfur, Everpouring Vial of Mercury (+1 of each rare resource a day) | +2 crystal, sulfur and mercury a day more (+3 of each in all) | ✅ | a horn of the Firstborn's last harvest; it gives until the giver is forgotten |
| Angelic Alliance (`angelic-alliance`, ADR-108) | Sword of Judgement, Lion's Shield of Courage, Celestial Necklace of Bliss (+4 Attack, +4 Defense, +2 Spell Power, +2 Knowledge, +2 morale) | +2 to every primary stat; no morale loss for mixed factions in the army | ✅ / 🆕 | Haven's last oath, sworn by angels who have not been seen since |
| Regalia of the Undead King (`undead-king-regalia`, was a set, ADR-108) | Blackshard of the Dead Knight, Rib Cage, Skull Helmet (+3 Attack, +2 Defense, +2 Knowledge) | +10 Necromancy points (HoMM3's Cloak of the Undead King) | 🆕 | the king was buried in three graves so he would not rise; he rose three times |
| Bow of the Sharpshooter (`bow-of-the-sharpshooter`, was a set, ADR-108) | Bow of Elven Cherrywood, Bowstring of the Unicorn's Mane, Necklace of Swiftness (+15 % ranged damage, +1 battle speed) | +15 % ranged damage more, +1 battle speed | ✅ | whoever holds it does not miss; the Sylvan say that is the curse |

## Sets

Set pieces have their own effects (above); wearing **every** piece of a set adds the set's bonus. Unlike an assembled relic, a set's pieces
stay separate and use their own slots.

| Set (id) | Pieces | Full set | Status |
|---|---|---|---|
| **Pilgrim's Road** (`pilgrims-road`) | Pilgrim's Boots, Weathered Cloak, Hearth Charm | +20 % movement points, the whole terrain penalty gone | ✅ |
| **Armor of the Dawn Covenant** (`dawn-covenant-armor`) | Plate of the Faithful, Aegis of Ash, Candle of the Pious | +2 morale, +2 Spell Power | ✅ |
| **Ashen Court Regalia** (`ash-court-regalia`, renamed from "Ash Court Regalia" to match the lore) | Ring of Reckoning, Sceptre of Cinders, Ledger of Debts | +2 Spell Power, +1 luck | ✅ |
| **The Unbound's Trophies** (`unbound-trophies`) | War-Drum, Marsh Talisman, Hunter's Mark | +2 morale, +2 luck | ✅ |
| **Regalia of the Firstborn** (`firstborn-regalia`) | Titan's Gladius, Sentinel's Shield, Titan's Cuirass, Crown of the Supreme Magi | +3 Attack, +3 Defense, +2 Spell Power (the pieces' penalties made up); the army is immune to `mind` | ✅ / 🆕 |

## Dropped by CONT-6

The owner's list of new effect kinds (CONT-6 b) leaves out experience, surrender cost and war machines, and the effects the question did not
list. So (confirmed by CONT-7 g, ADR-108):

- **Items dropped**: Novice's Primer (+5 % experience), Statesman's Seal (surrender cost), Engineer's Toolbelt (war machines).
- **Secondary effects dropped** (the item keeps the rest, sometimes with a ✅ effect instead): the Elvenwood Bow's "no distance penalty", the
  War-Drum's initiative, the Dawn Banner's morale re-roll (now +1 luck), the Ember Crown's half-cost Chaos spells (now +6 Spell Power), the
  Bone Dragon's Fang's double damage (now +2 luck), the Root's regeneration, the Mirror's spell reflection (now +3 Defense, +2 luck), the
  Aegis's fire protection (now −5 % damage taken), the Elder Dragon's scale's spell protection (now +2 Attack), the Amulet of the Deep's mana per
  Knowledge (now +10 maximum mana); the sets' spell costs and ranks, the bow set's extra shots, the Necromancy set's higher tier and the
  Unbound set's "non-native terrain" condition.

## Implementation

The core is in place (ADR-061): table and catalog, hero equipment and backpack, equip, unequip and give commands, effects in
battle, movement, income and terrain, map pickups that roll by tier, loot, map format v11, editor session calls, the AI,
and the hero screen with the paper doll and backpack (`src/Godot/EternalWars.Godot.UI/Hud/HeroPanel.cs`). The re-draft is in the table
(ADR-111): the renamed rows, the ✅ rows, every new effect kind with its rows, and the magic effects. Next: the assembled relics (roadmap,
"Artifacts").

## Open

- Numbers, after play-tests; whether Major artifacts need a guard.
- Whether a campaign can carry Relics between maps (FLOW-1).
