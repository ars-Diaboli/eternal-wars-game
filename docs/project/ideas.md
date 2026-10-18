Raw design notes from the owner. Once a note is implemented or recorded ([decisions.md](decisions.md), the
[design docs](../design/overview.md)), it is removed from here; open points move to [open-questions.md](open-questions.md).

Recorded so far:
- Heroes, skills and magic: ADR-047 / ADR-048 (2026-10-16).
- Roster notes and spell resistance: ADR-049 (2026-10-16). No sea travel: ADR-051.
- Morale and luck: ADR-053 (2026-10-16). War machines: ADR-056 (2026-10-16).
- Treasury and Prospecting: ADR-062 (2026-10-17). Caravans and map dwellings: ADR-054 (2026-10-17).
- Stewardship and governors, starting resources (complexity), Ballistics against fortifications, six magic schools with
  attunement and mana, faction groups for Stronghold and Swamp Bestiary: ADR-063 (2026-10-17).
- Marketplace rates, Leadership, the defense initiative skill, the Stronghold Primal Fury group: ADR-067 (2026-10-18).

Still to decide (now in open-questions.md):
- Swamp Bestiary: what Adrenaline does (placeholder group, ADR-063): HERO-2.
- Marketplace details: rounding, who may trade, the buy / sell spread (ADR-067): TOWN-2.
--------------------

marketplace rounding : for resources that player is going to sell - round upwards
for resources that player recieves during trade - round down

artifact tiers : Treasure, Minor, Major, Relic, Combo

adventure map objects :

Campfire: gives the player between 400–600 Gold and 4–6 other resources. Disappears after the visit.
Supply crates: gives the player 2 sets of resources between 4–6 (except Gold). Disappears after the visit.

Faerie Ring: gives a visiting hero +1 to luck for the next battle.

Fountain of Youth: gives visiting heroes +400 movement points until the end of the day, 
and +1 to morale for the next battle.

Gold Mine: produces +1000 Gold per day for a player when flagged.
Sawmill: produces +2 Lumber per day for a player when flagged.
Ore mine mil: produces +2 Ore per day for a player when flagged.
Alchemist's Lab: produces +1 Mercury per day for a player when flagged.
Crystal pond: produces +1 Crystal per day for a player when flagged.
Sulfur Dune: produces +1 Sulfur per day for a player when flagged.

Observatory: immediately reveals terrain within a 20-square radius around the hero.

Sign : Displays a custom message.

Resources pile: according to its type can give:
5-10 Wood or Ore;
3-7 Sulfur, Mercury, Crystal;
500-1000 Gold.
Disappears after being visited.

Prison: a "loot" object that will reward you with a random hero 
of 1/5/10/20/30 level or a hero set by map-maker. 
Disappears after being visited.

Mineral Spring: a hero receives +1 luck for the next battle and +600 movement points until the end of the day.

Mystical Garden: brings 500 Gold or 5 random magic resource to a visiting hero once a week.

Creature dwelling: 
enables a visiting hero to hire one type of creatures. 
Each creature in the game has a different structure. 
Creature dwellings of level 3/4/5 are guarded by 9/6/3 
non-upgraded versions of the creatures the dwelling produces.

Arena: when visited, offers once for every hero to increase either their attack or defense skill by 2.
Colosseum of the Magi: when visited, offers once for every hero to increase either their spell power or knowledge by 2.
Corpse: gives the first visiting hero:80% : nothing, 20% : Artifact (t) or Artifact (m)

Churchyard: a creature bank guarded by:
100% : Zombie x90 for 2500 Gold, Artifact (m)
Visiting a previously plundered Churchyard inflicts a -1 penalty to morale.

Abandoned Mine: guarded by 100-245 Troglodytes. 
After being released works as a random mine (except Sawmill). 
Combat taking place in Abandoned Mine will always use subterranean terrain with all native terrain bonuses.

Hill Fort: allows a hero to upgrade creatures of 1-3 levels for the 150% price
Stables: give the visiting hero a bonus of +400 movement points per day for the rest of the week. 
Can upgrade Cavaliers to Champions for free.

Town Gate: allows visiting hero to teleport to any allied town. 
This does not cost any additional movement points.

Temple: gives a visiting hero the bonus to morale for the next combat. 
On the week's first 6 days the bonus is +2, but on 7th day the bonus is +3.

Tree of Knowledge: increases the visiting hero's experience right to the next 
level once per game either for free, for 2500 Gold or for 5 magic resources each. 
(The free level-up cannot be rejected).

War Machine Factory: enables visiting hero to buy:
Ammo Cart for 1000 Gold,
Ballista for 2500 Gold
First Aid Tent for 750 Gold.

Wagon: may give the first visiting hero:
10% : nothing
20% : Artifact (t)
10% : Artifact (m)
60% : 2-5 any resource (except gold).

Treasure Chest: a loot item which can offer:
30% : 1000 Gold or 500 Experience
30% : 1500 Gold or 1000 Experience
25% : 2000 Gold or 1500 Experience
10% : 2500 Gold or 2000 Experience
5% : Artifact (t)

Mansion: a creature bank guarded by:
30% : Vampire Lord Vampire Lord x40 for 2500 Gold, Mercury Sulfur Crystal x 2 and Artifact (M)
30% : Vampire Lord Vampire Lord x60 for 5000 Gold, Mercury Sulfur Crystal x 3 and Artifact (M)
30% : Vampire Lord Vampire Lord x80 for 7500 Gold, Mercury Sulfur Crystal x 4 and Artifact (M)
10% : Vampire Lord Vampire Lord x100 for 10000 Gold, Mercury Sulfur Crystal x 5 and Artifact (M)

Ruins: a creature bank guarded by:
30% : Skeleton  x20, Wight  x3, Wraith  x3, Skeleton Warrior  x10 , Power Lich  x1 for 1000 Gold.
30% : Skeleton  x30, Wight  x5, Wraith  x5, Skeleton Warrior  x10 , Power Lich  x1 for 2000 Gold.
30% : Skeleton  x40, Wight  x7, Wraith  x7, Skeleton Warrior  x20 , Power Lich  x2 for 3000 Gold.
10% : Skeleton  x50, Wight  x9, Wraith  x9, Skeleton Warrior  x20 , Power Lich  x3 for 4000 Gold.
Shroud(fog of war) is removed from all tiles within 12 tiles from Ruins. 
If Ruins are already visited, shroud is removed anyway.

Dwarven Treasury: a creature bank guarded by:
30% : Dwarf x50 for 2500 Gold and 2 Crystal
30% : Dwarf x75 for 4000 Gold and 4 Crystal
30% : Dwarf x100 for 5000 Gold and 6 Crystal
10% : Dwarf x150 for 7500 Gold and 10 Crystal
50% probability for 1/5 of the guards to be Battle Dwarf Battle Dwarves.

Imp Cache: a creature bank guarded by:
30% : Imp x100 for 1000 Gold and 2 Mercury;
30% : Imp x150 for 1500 Gold and 4 Mercury;
30% : Imp x200 for 2000 Gold and 6 Mercury;
10% : Imp x300 for 3000 Gold and 8 Mercury.
50% probability for 1/5 of the guards to be Familiar Familiars.

Medusa Stores: a creature bank guarded by:
30% : Medusa Medusa x15 for 2000 Gold and 4 Sulfur
30% : Medusa Medusa x30 for 3000 Gold and 6 Sulfur
30% : Medusa Medusa x40 for 4000 Gold and 8 Sulfur
10% : Medusa Medusa x50 for 5000 Gold and 10 Sulfur
50% probability for 1/5 of the guards to be Medusa Queen Medusa Queens.

Naga Bank: a creature bank guarded by:
30% : Naga Naga x5 for 3000 Gold
30% : Naga Naga x10 for 6000 Gold
30% : Naga Naga x15 for 9000 Gold
10% : Naga Naga x20 for 12000 Gold
50% probability for 1/5 of the guards to be Naga Queen Naga Queens.

Water Wheel: gives a visiting hero 500 Gold at the first week of the first month 
and 750 Gold at the rest of the game once per week.

Windmill: gives a visiting hero 3-6 of random resource (except Gold) once a week.

Warehouse of Wood: brings 10 Wood when visited once a week.
Warehouse of Ore: brings 10 Ore when visited once a week.
Warehouse of Sulfur: brings 6 Sulfur when visited once a week.
Warehouse of Mercury: brings 6 Mercury when visited once a week.
Warehouse of Crystal: brings 6 Crystal when visited once a week.
Warehouse of Gold: brings 2000 Gold when visited once a week.

Warrior's Tomb: gives the first visiting hero:
30% : Artifact (t)
50% : Artifact (m)
15% : Artifact (M)
5% : Artifact (R)
and -3 to morale for the next battle. All the next visitors will get -3 morale.

Lean To: gives the first visiting hero 1–6 units of resources other than gold.

Gazebo: a visiting hero receives 2500 experience for 1500 Gold.
Learning Stone: gives visiting heroes +1000 experience upon their first visit.

Library of Enlightenment: increases all four primary 
skills of visiting heroes by +2 if they have reached level 10

Magic Well: a hero can restore 100% of mana reserves here once per turn.

