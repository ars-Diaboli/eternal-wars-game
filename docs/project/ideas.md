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
- Marketplace rounding, artifact tiers, and the whole adventure-map object catalogue (resource providers, loot, dwelling
  guards, creature banks, power-ups, buffs, utilities): ADR-068 (2026-10-19), table in
  [adventure-map.md](../design/adventure-map.md#planned-objects-adr-068).
- Attunement table, Necromancy and the Necromancy Amplifier, mana by level, the Pathfinding / Tactics / Defense / Offense
  rework (Marksmanship, distance penalty), Primal Fury for Swamp Bestiary (Adrenaline dropped), and the second object catalogue:
  ADR-069 (2026-10-20); tables in [skills.md](../content/skills.md) and
  [adventure-map.md](../design/adventure-map.md#second-object-catalogue-adr-069).

Still to decide (now in open-questions.md):
- Marketplace details: the map marketplace's treasure threshold (ADR-067): TOWN-2.
- ADR-069: HERO-4, HERO-6, HERO-7, CMB-6, MAG-6, MAG-7, MAG-8, TOWN-6, MAP-15 … MAP-24.


Q1: RMG-9 Mirrored templates** (blocks mirrored generation, 1.0 scope). Point or axis symmetry; whole map or per-zone
mirror pairs; which player counts; the underground; how a mirrored layout fits the Voronoi search; mirrored zones
must share their rolls (today each zone rolls on its own stream).
A1: As i see Mirrored templates can work only for 2, 4 or 8 (with underground) players; 
use point(center of the map) symmetry for 4 or 8 players;
use axis symmetry for 2 players; 
map could be symmetrical either across vertical, horizontal or diagonal axis

Q2: RMG-10 Template editor** (blocks the template editor). An undoable `TemplateEditorSession` like `MapEditorSession`;
where user and mod templates are saved and scanned; live validation and a preview (layout or terrain dump).
A2: template editor could be without "undo" functionality.
Template editor should be functionally very similar to homm3 hota template editor
when working in the template editor user should be able to generate map to see the results

Q3: MAP-15 Mage's Chest.** "Often a Minor artifact": what are the odds, and what is in it otherwise (gold, experience, nothing)? Does it vanish like
the Treasure Chest? Proposal: 70 % a Minor artifact, 30 % 1000–1500 gold; vanishes.
A3: vanishes after pickup, odds: 5% nothing; 5% major artifact; 60% Minor artifact; 30% Minor artifact and 500-1000 gold

Q4: MAP-16 Window and Eye of the Magi.** How many colours (HoMM3 has one kind; the notes say "of the corresponding colour")? What radius around
each Eye (proposal: 5, like a hero's sight)? Every visit, or once per hero like the Observatory? Does the generator place them, and how many Eyes
per Window?
A4: radius around each Eye - 6
Does the generator place them - no, only user in map editor
and how many Eyes per Window? - up to 4;
How many colours - four (red, green, blue, black)

Q5: MAP-17 Map Tavern.** Does it offer the player's weekly tavern pool like a basic town tavern (2 heroes, 2500 gold, ADR-060), or its own offers?
Where does the hired hero appear (next to the tavern, or the visitor must stand at the entrance like in towns)? Does the hero limit apply (yes,
assumed)?
A5: Does it offer the player's weekly tavern pool like a basic town tavern - works like upgraded tavern (4 heroes) uses player's weekly tavern pool
Where does the hired hero appear - visitor must stand at the entrance like in towns, new hero appears next cell near entrance (default - 1 cell below if free)
Does the hero limit apply - yes;
if hero limit is reached or new hero cannot be placed - hireing screed does not allow hero recruitment.

Q6: MAP-18 Portals.** A linked pair is today's two-way monolith and the one-way portal is today's one-way monolith (ADR-026). Is anything new meant:
three or more portals of one type (ADR-026 rules out a random exit; proposal: the player picks the exit from a list, like the Town Gate), new
art and names only, or generator placement (RMG-11 lists portals in templates)?
A6: portal and monolith are synonyms - so yes;
remove three or more portals of one type rule; 2 portal of one type for now;
Templates should allow monoliths - if enabled, so generator could place them to connect zones;

Q7: MAP-19 Garrison.** Is it flagged by its owner like a mine, and is it captured by beating its guards? Can a neutral garrison have guards set by
the map or generator? Can an own hero walk through it (both directions, or one-way like some HoMM3 garrisons)? Can heroes leave and take
creatures there at any time? Does the generator put garrisons in zone connections?
A7: Is it flagged by its owner like a mine - yes;
and is it captured by beating its guards? - yes;
Can a neutral garrison have guards set by the map or generator? - yes;
Can an own hero walk through it - both directions for now;
Can heroes leave and take creatures there at any time? - yes;
Does the generator put garrisons in zone connections? - yes, should be one of the options, along with regular creature guard, according to map teplate;

Q8: MAP-21 Deposits.** The price of each (proposal: about two weeks of the mine's income in gold plus building material: Gold Vein 5000 gold and 10 ore,
the others 2000–3000 gold and 5 lumber or ore)? Who may pay (any visiting hero; the new mine is flagged for its player)? May a deposit be
guarded? There is no sulfur deposit in the list (Cinnabar is mercury): add one (a Sulfur Vent becoming the Sulfur Dune)? Does the generator place
deposits instead of some mines?
A8: The price of each - use proposed values for now;
Who may pay - any visiting hero from players resources;
new mine is flagged for its player - yes;
May a deposit be guarded? - by standard creature guard on the map;
There is no sulfur deposit in the list (Cinnabar is mercury): add one (a Sulfur Vent becoming the Sulfur Dune)? - yes add missing deposit;
Does the generator place deposits instead of some mines? - yes but not instead, generator should place them separately, like abandoned mines;

Q9: MAP-22 New creature banks.** Their guard tables and rewards (the notes name only the guards and the kind of reward): may the agent draft them
like the existing banks (four rows at 30 / 30 / 30 / 10 %, rewards scaled to the guard's value)? Names: the orc bank needs another name than the
undead Ruins (proposal: Orc Ruins, or rename the undead one to Crypt). Which creatures: Keep = Archers / Marksmen and Pikemen / Guardians (Haven);
Lair = Spectators / Beholders (Dungeon); Forest Glen = Timber / Dire Wolves (neutral); Hideout = Bandits (neutral); Ruins = Orcs / Orc Berserkers
(Stronghold); Dragon City = Black Dragons (Dragon Utopia, 9900 gold each, so a very strong bank). "Item artifacts" for the Keep means Minor
artifacts?
A9: draft them like the existing banks
rename the undead bank to Crypt
Re-draft all creature banks (including existing ones) so they would be more faction-aligned. use 2-3 creature types of the same faction as guards;
Dragon City - yes it should be the strongest bank by far;