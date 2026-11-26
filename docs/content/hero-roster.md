# Hero roster

All 128 standard heroes of the eight playable towns and the 48 campaign heroes, with the rules that come with a hero. Rules:
ADR-048 (roster, skills, levels), ADR-055 (primary stats), ADR-056 (war machines). The roster is the table
`src/Core/data/heroes.csv` (columns in [development.md](../technical/development.md#editing-heroes); it moves to a core-owned
folder, ADR-071, not done yet); update this page when the table changes. Skill effects are listed in [skills.md](skills.md), towns
and dwellings in [town-buildings.md](town-buildings.md). Names and numbers are a draft.

## What every hero has

- **Town and class**: every playable town has **16 heroes**, a **warrior** and a **mage** for each of its eight
  tier 1–4 creature lines.
- **Campaign heroes** (ADR-298): four more per playable town and eight each for Conflux and Dragon Utopia (whose towns
  are not playable). Random draws never pick them (taverns, hero placeholders, random prisons, random starts); a map or campaign
  brings one into play by placing it (a hero on the map, a prison, a Give hero event). Each copies a standard hero's specialty,
  starting skills and stats (`campaign` column of `heroes.csv`).
- **Preferred creature**: that line, base and upgrade, gets **+2 Attack, +2 Defense and +1 battlefield speed** in the
  hero's army.
- **Specialty** (HoMM3-style): a daily resource, a skill or a spell ([skills.md](skills.md#specialties)). Since ADR-114 (the ADR-093 /
  ADR-107 draw, done once with a fixed seed): **4 heroes** give +350 gold a day (Elaith, Sythra, Ulric, Vorn: four towns), **6** give +1 rare
  resource a day (sulfur: Rowena, Kethra; crystal: Neriah, Valeska; mercury: Mazga, Ysmera: six towns), **2 Necromancy specialists** (Sepulka,
  a mage, and Corvane, a warrior, both Necropolis) add +5 Necromancy points, and **2 Logistics specialists** (Maren, Faenor) move 10 %
  farther even without the skill. The other heroes that had a resource or Logistics specialty took their first starting skill with an
  effect (a school's Magic skill and Logistics skipped), at +5 % per hero level.
- **Starting skills**: two skills at Basic.
- **Primary stats** (A / D / SP / K = Attack / Defense / Spell Power / Knowledge): every roster hero starts with
  5 points; warriors 2/1/1/1 or 1/2/1/1, mages 0/0/3/2 or 0/1/2/2. Each level adds 1 to one stat, with odds by class
  ([skills.md](skills.md#primary-stats)).
- **Hiring**: heroes are hired in a town's tavern for 2500 gold ([town-buildings.md](town-buildings.md)). A basic tavern
  offers a native hero and one of an adjacent town; the upgraded one (Brotherhood) two natives and two adjacent heroes (ADR-070). The towns form a ring, Sylvan – Haven – Academy – Dungeon – Necropolis – Inferno – back to Sylvan
  ([lore.md](lore.md)). Adjacent means the town's two ring neighbours (Sylvan: Inferno, Haven; Haven: Sylvan, Academy;
  Academy: Haven, Dungeon; Dungeon: Academy, Necropolis; Necropolis: Dungeon, Inferno; Inferno: Necropolis, Sylvan);
  opposing is the town across the ring (Sylvan–Dungeon, Academy–Inferno, Haven–Necropolis) and is offered last. Stronghold and Swamp Bestiary
  have no ring: a basic tavern offers a native and a warrior of another town, the Brotherhood two natives and two
  warriors (`TavernRules.Slots`). Offers are shuffled per player and town faction every week (the
  player's towns of one faction share them), heroes on the map are not offered, and a player has at most 8 heroes, or the map's
  lower limit (ADR-060, ADR-068). A hired hero gets a **starter stack**: a week's growth of its preferred creature; a hero that
  retreated or surrendered comes back as it was (ADR-070, ADR-081).
- **Morale and luck** 0 at the start (maps and the editor can set them, ADR-053).
- **Army**: up to 7 stacks. A hero without creatures cannot attack.
- **War machines**: its town's offensive machine from the start; the healing tent and ammo cart are bought (below).

The roster follows one template in every town: the hero for the same slot (for example the warrior who prefers the
first tier 1 line) has the same kind of specialty, the same starting skills and the same stats everywhere; only the
town's school and creatures differ. In Stronghold and Swamp Bestiary (no school, ADR-063) "own magic" is the faction
group's first skill (`rage.first` = Blood Rage), and "a spell" / "own attunement" specialties are `skill rage.first` /
`skill rage.second` (= Warcry). Both towns have the Primal Fury group (working title, ADR-067, ADR-069; the ids stay `rage.*`).
Their warriors swap one starting skill so that every **Stronghold** warrior starts with an **Offense** skill and every
**Swamp Bestiary** warrior with a **Defense** skill (ADR-072, HERO-7); their tables below show it.

| Slot (preferred line) | Warrior: specialty · skills · stats | Mage: specialty · skills · stats |
|---|---|---|
| Tier 1, first line | Melee · Melee, Tactics · 2/1/1/1 | a spell · own magic, Treasury · 0/0/3/2 |
| Tier 1, second line | Armor (was +350 gold) · Armor, Melee · 1/2/1/1 | own attunement · own magic, Spell resistance · 0/1/2/2 |
| Tier 2, first line | Archery (was Logistics) · Logistics, Archery · 2/1/1/1 | a spell · own magic, Logistics · 0/0/3/2 |
| Tier 2, second line | War Tempo (was +2 ore) · War Tempo, Armor · 1/2/1/1 | own attunement (was +1 mercury) · own magic, own attunement · 0/1/2/2 |
| Tier 3, first line | Archery · Archery, Pathfinding · 2/1/1/1 | a spell · own magic, Prospecting · 0/0/3/2 |
| Tier 3, second line | Treasury (was +2 lumber) · Treasury, Tactics · 1/2/1/1 | own attunement · own magic, Armor · 0/1/2/2 |
| Tier 4, first line | Tactics · Tactics, Armor · 2/1/1/1 | a spell · own magic, Pathfinding · 0/0/3/2 |
| Tier 4, second line | Scouting (was +350 gold) · Scouting, Melee · 1/2/1/1 | own attunement (was +350 gold) · own magic, own attunement · 0/1/2/2 |

Since ADR-069 / ADR-072 (built, ADR-078): Drill became War Tempo, Troop management was dropped (its tier-4 second-line warriors
start with **Scouting**, HERO-6), and Swamp Bestiary's Adrenaline gave way to Primal Fury. Primal Fury mages start with it and
raise their stats with even odds; their warriors are offered it often (HERO-7). The rival school is the arch-rival across the
ring only (HERO-3). Old skill ids in maps, saves and replays read as the new skills.

"First" and "second" line follow the creature table's order. Open: whether heroes should differ more between towns
(per-town stat odds, as in HoMM3, are an option in ADR-055).

## War machines

Every hero has up to three machines (ADR-056). Draft numbers, tuned in ADR-121: the four offensive machines deal about the same damage per
initiative (average damage × shots × initiative / 10: ballista 47.5, cannon 45.5, catapult 46, glaivethrower 50.6; WarMachines.Throughput),
the glaivethrower with the least health:

| Machine | Slot | Towns | Attack / Defense | Damage | Health | Initiative | Notes |
|---|---|---|---|---|---|---|---|
| Ballista | offensive | Haven, Necropolis | 10 / 10 | 40–55 | 250 | 10 | |
| Cannon | offensive | Academy, Inferno | 12 / 8 | 55–75 | 220 | 7 | |
| Catapult | offensive | Stronghold, Swamp Bestiary | 10 / 10 | 45–70 | 260 | 8 | |
| Glaivethrower | offensive | Sylvan, Dungeon | 11 / 9 | 20–26 (was 25–35, ADR-121) | 200 | 11 (was 12) | shoots twice |
| Healing tent | tent | all | 0 / 5 | – | 150 | – | heals every own stack each round |
| Ammo cart | cart | all | 0 / 5 | – | 200 | – | extra or unlimited shots |

- Every hero starts with its town's offensive machine (custom heroes without a town: the ballista). It fights only in
  sieges until **Advanced Ballistics**, then in every battle.
- A town's **blacksmith** sells the tent (750 gold), the cart (1000) and a lost offensive machine (2500) to the
  visiting hero; a hero always gets its own town's offensive machine.
- Machines stand at the back of the field, never move, fight in melee or retaliate, and are lost when destroyed.
  Skills: [skills.md](skills.md#war-machines-machines).

## Haven

School **Light** · native terrain highlands, grass · rival Necropolis (arch-rival, ADR-072) · offensive machine **Ballista**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Aldric Vane | Warrior | Footman / Crusader (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Ansel Wren | Mage | Footman / Crusader (T1) | spell: Bless | Light magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Maren Holt | Warrior | Pikeman / Guardian (T1) | Logistics (flat +10 % movement) | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Ivo Varro | Mage | Pikeman / Guardian (T1) | Light attunement | Light magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Corwin Ashby | Warrior | Zealot / Inquisitor (T2) | Archery | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Matthias Crane | Mage | Zealot / Inquisitor (T2) | spell: Heal | Light magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Bertram Stahl | Warrior | Archer / Marksman (T2) | War Tempo | War Tempo Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Rowena Pell | Mage | Archer / Marksman (T2) | +1 sulfur a day | Light magic Basic, Light attunement Basic | 0 / 1 / 2 / 2 |
| Edric Morrow | Warrior | Monk / Priest (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Fabian Lorne | Mage | Monk / Priest (T3) | spell: Smite | Light magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Sabine Kell | Warrior | Griffin / Royal Griffin (T3) | Treasury | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Agathe Sorrel | Mage | Griffin / Royal Griffin (T3) | Light attunement | Light magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Gerrit Hale | Warrior | Lancer / Champion (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Cyprian Vey | Mage | Lancer / Champion (T4) | spell: Sanctuary | Light magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Osric Brand | Warrior | Paladin / Sacred Warrior (T4) | Scouting | Scouting Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Elodie Marsh | Mage | Paladin / Sacred Warrior (T4) | Light attunement | Light magic Basic, Light attunement Basic | 0 / 1 / 2 / 2 |

Campaign heroes (ADR-298), never drawn at random; each copies a standard hero's preferred creature, specialty, skills and stats.

| Hero | Class | Copies | Who |
|---|---|---|---|
| Leopold Ardent | Warrior | Gerrit Hale | The old Lord Marshal of the Sun Throne, who broke the first siege of the Ashen March |
| Isaura Voss | Mage | Cyprian Vey | A seer cast out by the Synod, who reads the future in eclipses |
| Tamsin Rook | Warrior | Corwin Ashby | A poacher turned captain of the King's rangers |
| Benedikt Grau | Mage | Fabian Lorne | The Grand Inquisitor of the Synod, feared in every province |

## Sylvan

School **Nature** · native terrain forest floor, grass · rival Dungeon (arch-rival, ADR-072) · offensive machine **Glaivethrower**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Thalion | Warrior | Pixie / Sprite (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Elowen | Mage | Pixie / Sprite (T1) | spell: Entangle | Nature magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Faelar | Warrior | Satyr / Satyr Warrior (T1) | Armor | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Taeral | Mage | Satyr / Satyr Warrior (T1) | Nature attunement | Nature magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Aerendil | Warrior | Blade Dancer / War Dancer (T2) | Archery | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Eredan | Mage | Blade Dancer / War Dancer (T2) | spell: Regrowth | Nature magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Caelwyn | Warrior | Hunter / Arcane Archer (T2) | War Tempo | War Tempo Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Faenor | Mage | Hunter / Arcane Archer (T2) | Logistics (flat +10 % movement) | Nature magic Basic, Nature attunement Basic | 0 / 1 / 2 / 2 |
| Sylvara | Warrior | Druid / Druid Elder (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Ithiel | Mage | Druid / Druid Elder (T3) | spell: Swarm | Nature magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Elaith | Warrior | Pegasus / Silver Pegasus (T3) | +350 gold a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Sariel | Mage | Pegasus / Silver Pegasus (T3) | Nature attunement | Nature magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Varis | Warrior | Moon Doe / Unicorn (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Calen | Mage | Moon Doe / Unicorn (T4) | spell: Barkskin | Nature magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Neriah | Warrior | Treant / Ancient Treant (T4) | +1 crystal a day | Scouting Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Nymara | Mage | Treant / Ancient Treant (T4) | Nature attunement | Nature magic Basic, Nature attunement Basic | 0 / 1 / 2 / 2 |

Campaign heroes (ADR-298), never drawn at random; each copies a standard hero's preferred creature, specialty, skills and stats.

| Hero | Class | Copies | Who |
|---|---|---|---|
| Lithariel | Mage | Calen | The queen-mother of the Silverwood, who has outlived three forests |
| Rhovan | Warrior | Thalion | A wild elf of the deep forest who has never lived in a city |
| Siannon | Warrior | Caelwyn | The hunt-mistress of the outer glades |
| Orrindel | Mage | Taeral | A hermit druid, the last who speaks the tongue of the oaks |

## Academy

School **Arcana** · native terrain snow, ice · rival Inferno (arch-rival, ADR-072) · offensive machine **Cannon**

Not all of the Academy's heroes are human (ADR-303): Stellan Frost, Kasimir Lode and Edvard Lunde are dwarves; Sesha is a naga;
Raziya, Layth, Qadir, Tariq and Nadia are djinn, ageless and always young; Talos is a golem.

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Corvin Halde | Warrior | Gremlin / Gremlin Master (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Aurelian Vex | Mage | Gremlin / Gremlin Master (T1) | spell: Magic Arrow | Arcana magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Stellan Frost | Warrior | Dwarf / Dwarf Defender (T1) | Armor | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Seraphine Lux | Mage | Dwarf / Dwarf Defender (T1) | Arcana attunement | Arcana magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Ingrid Vael | Warrior | Gargoyle / Obsidian Gargoyle (T2) | Archery | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Tariq | Mage | Gargoyle / Obsidian Gargoyle (T2) | spell: Haste | Arcana magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Talos | Warrior | Iron Golem / Animated Armor (T2) | War Tempo | War Tempo Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Ivar Marrow | Mage | Iron Golem / Animated Armor (T2) | Arcana attunement | Arcana magic Basic, Arcana attunement Basic | 0 / 1 / 2 / 2 |
| Henrik Strom | Warrior | Snow Maiden / Enchantress (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Qadir | Mage | Snow Maiden / Enchantress (T3) | spell: Frost Bolt | Arcana magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Kasimir Lode | Warrior | Sorcerer / Mage (T3) | Treasury | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Sesha | Mage | Sorcerer / Mage (T3) | Arcana attunement | Arcana magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Raziya | Warrior | Djinn / Djinn Sultan (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Layth | Mage | Djinn / Djinn Sultan (T4) | spell: Dispel | Arcana magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Dorian Quill | Warrior | Naga / Naga Queen (T4) | Scouting | Scouting Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Valeska Rune | Mage | Naga / Naga Queen (T4) | +1 crystal a day | Arcana magic Basic, Arcana attunement Basic | 0 / 1 / 2 / 2 |

Campaign heroes (ADR-298), never drawn at random; each copies a standard hero's preferred creature, specialty, skills and stats.

| Hero | Class | Copies | Who |
|---|---|---|---|
| Magnus Hollin | Mage | Tariq | The Archmage of the White Spire, who bound the djinn to the city |
| Halvor Brenn | Mage | Seraphine Lux | The White Spire's envoy to the dwarven holds |
| Edvard Lunde | Warrior | Talos | The dwarf master of the golem foundries |
| Nadia | Mage | Layth | A young djinn who unmade a storm over the White Spire |

## Necropolis

School **Death** · native terrain cursed lands · rival Haven (arch-rival, ADR-072) · offensive machine **Ballista**

Not all of the Necropolis's heroes are alike (ADR-307): Necrys, Sepulka and Ossian Grael are living humans; Morthal,
Vorlath, Malgrin and Thessaly are liches; Hespera, Kael Duskmire, Ulric Ashgrave, Ysmera, Maladrea and Aldous
Wraithe are vampires; Lethe is a ghost and Corvane a wraith; Grimwald is a ghoul, Vexa and Ozmund plague corpses,
Drusus a death knight and Ravenna a skeleton.

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Morthal | Warrior | Skeleton / Skeleton Warrior (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Necrys | Mage | Skeleton / Skeleton Warrior (T1) | spell: Raise Dead | Death magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Vexa | Warrior | Zombie / Flesh Golem (T1) | Armor | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Sepulka | Mage | Zombie / Flesh Golem (T1) | Necromancy (+5 points) | Death magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Grimwald | Warrior | Ghoul / Alghoul (T2) | Archery | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Vorlath | Mage | Ghoul / Alghoul (T2) | spell: Curse | Death magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Ulric Ashgrave | Warrior | Death Weaver / Fate Weaver (T3) | +350 gold a day | War Tempo Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Ysmera | Mage | Death Weaver / Fate Weaver (T3) | +1 mercury a day | Death magic Basic, Death attunement Basic | 0 / 1 / 2 / 2 |
| Hespera | Warrior | Vampire / Vampire Lord (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Kael Duskmire | Mage | Vampire / Vampire Lord (T3) | spell: Drain Life | Death magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Corvane | Warrior | Wight / Wraith (T2) | Necromancy (+5 points) | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Lethe | Mage | Wight / Wraith (T2) | Death attunement | Death magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Drusus | Warrior | Dark Knight / Death Knight (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Ozmund | Mage | Dark Knight / Death Knight (T4) | spell: Plague | Death magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Malgrin | Warrior | Lich / Arch Lich (T4) | Scouting | Scouting Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Thessaly | Mage | Lich / Arch Lich (T4) | Death attunement | Death magic Basic, Death attunement Basic | 0 / 1 / 2 / 2 |

Campaign heroes (ADR-298), never drawn at random; each copies a standard hero's preferred creature, specialty, skills and stats.

| Hero | Class | Copies | Who |
|---|---|---|---|
| Maladrea | Mage | Necrys | The vampire Pale Matriarch, who first bound the dead of the plague years |
| Aldous Wraithe | Warrior | Hespera | A Haven crusader who fell at the Black Ford and rose again |
| Ravenna | Warrior | Morthal | A skeleton duellist who commands the skeleton legions |
| Ossian Grael | Mage | Kael Duskmire | The living gravekeeper of the old kings' crypts |

## Inferno

School **Chaos** · native terrain lava · rival Academy (arch-rival, ADR-072) · offensive machine **Cannon**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Azgaroth | Warrior | Imp / Familiar (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Belzra | Mage | Imp / Familiar (T1) | spell: Fireball | Chaos magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Brakka | Warrior | Horned Demon / Horned Overseer (T1) | Armor | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Nyxoth | Mage | Horned Demon / Horned Overseer (T1) | Chaos attunement | Chaos magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Vorhaz | Warrior | Hell Hound / Cerberus (T2) | Archery | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Ashmedai | Mage | Hell Hound / Cerberus (T2) | spell: Inferno | Chaos magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Skarn | Warrior | Tormentor / Lacerator (T2) | War Tempo | War Tempo Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Lilivex | Mage | Tormentor / Lacerator (T2) | Chaos attunement | Chaos magic Basic, Chaos attunement Basic | 0 / 1 / 2 / 2 |
| Ixira | Warrior | Succubus / Succubus Mistress (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Pyrrhon | Mage | Succubus / Succubus Mistress (T3) | spell: Terror | Chaos magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Malphor | Warrior | Hell Charger / Nightmare (T3) | Treasury | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Veshra | Mage | Hell Charger / Nightmare (T3) | Chaos attunement | Chaos magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Zarduk | Warrior | Pit Fiend / Pit Lord (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Orzhul | Mage | Pit Fiend / Pit Lord (T4) | spell: Hellfire | Chaos magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Kezrath | Warrior | Juggernaut / Ravager (T4) | Scouting | Scouting Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Sythra | Mage | Juggernaut / Ravager (T4) | +350 gold a day | Chaos magic Basic, Chaos attunement Basic | 0 / 1 / 2 / 2 |

Campaign heroes (ADR-298), never drawn at random; each copies a standard hero's preferred creature, specialty, skills and stats.

| Hero | Class | Copies | Who |
|---|---|---|---|
| Vazgoroth | Warrior | Zarduk | The Pit King's warmaster, who led the first breach of the Veil |
| Ishara | Mage | Belzra | A mortal sorceress who sold her soul and kept her face |
| Grizhak | Warrior | Vorhaz | A hell-hound master scarred by his own pack |
| Zethrax | Mage | Ashmedai | A demon scholar, keeper of the burning library |

## Dungeon

School **Shadow** · native terrain subterranean · rival Sylvan (arch-rival, ADR-072) · offensive machine **Glaivethrower**

Not all of the Dungeon's heroes are dark elves (ADR-305): Kharis, Vorn, Zesrin and Malaroth are minotaurs; Vaelrith,
Nethra and Ilphar are troglodytes; Ilvara is a medusa; Dryel and Kaelvorn are humans of the surface.

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Vaelrith | Warrior | Troglodyte / Deep Troglodyte (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Nethra | Mage | Troglodyte / Deep Troglodyte (T1) | spell: Blind | Shadow magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Zhaun | Warrior | Scout / Stalker (T1) | Armor | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Sabal | Mage | Scout / Stalker (T1) | Shadow attunement | Shadow magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Ilvara | Warrior | Medusa / Putrid Medusa (T2) | Archery | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Ghaunis | Mage | Medusa / Putrid Medusa (T2) | spell: Shadow Step | Shadow magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Kharis | Warrior | Minotaur / Minotaur Guard (T2) | War Tempo | War Tempo Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Zesrin | Mage | Minotaur / Minotaur Guard (T2) | Shadow attunement | Shadow magic Basic, Shadow attunement Basic | 0 / 1 / 2 / 2 |
| Shyrran | Warrior | Dark Raider / Grim Raider (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Dryel | Mage | Dark Raider / Grim Raider (T3) | spell: Despair | Shadow magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Vorn | Warrior | Shadow Mistress / Shadow Matriarch (T3) | +350 gold a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Vhaeris | Mage | Shadow Mistress / Shadow Matriarch (T3) | Shadow attunement | Shadow magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Lyrthas | Warrior | Manticore / Scorpicore (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Phyrra | Mage | Manticore / Scorpicore (T4) | spell: Dark Bolt | Shadow magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Xundar | Warrior | Spectator / Beholder (T4) | Scouting | Scouting Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Ilphar | Mage | Spectator / Beholder (T4) | Shadow attunement | Shadow magic Basic, Shadow attunement Basic | 0 / 1 / 2 / 2 |

Campaign heroes (ADR-298), never drawn at random; each copies a standard hero's preferred creature, specialty, skills and stats.

| Hero | Class | Copies | Who |
|---|---|---|---|
| Malaroth | Warrior | Kharis | The minotaur Lord of the Labyrinth |
| Seyrissa | Mage | Phyrra | The oracle of the spider shrines |
| Kaelvorn | Warrior | Vaelrith | A human renegade who fought the surface wars for gold |
| Ozrael | Mage | Sabal | A blind seer who hears the deep |

## Stronghold

Faction group **Primal Fury** (no magic school, ADR-063, ADR-069) · native terrain rough, wasteland · rivals – · offensive machine **Catapult**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Grukk | Warrior | Goblin / Hobgoblin (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Ugra | Mage | Goblin / Hobgoblin (T1) | Blood Rage | Primal Fury (1st) Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Mazga | Warrior | Harpy / Fury (T1) | +1 mercury a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Zhakar | Mage | Harpy / Fury (T1) | Warcry | Primal Fury (1st) Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Thrak | Warrior | Orc / Orc Berserker (T2) | Archery | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Mograth | Mage | Orc / Orc Berserker (T2) | Blood Rage | Primal Fury (1st) Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Kragash | Warrior | Wolf Rider / Wolf Raider (T2) | Treasury | Treasury Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Ashka | Mage | Wolf Rider / Wolf Raider (T2) | Warcry | Primal Fury (1st) Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Urzog | Warrior | Centaur / Centaur Warchief (T3) | War Tempo | War Tempo Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Yazna | Mage | Centaur / Centaur Warchief (T3) | Blood Rage | Primal Fury (1st) Basic, Primal Fury (2nd) Basic | 0 / 1 / 2 / 2 |
| Borka | Warrior | Ogre / Ogre Mage (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Kurgh | Mage | Ogre / Ogre Mage (T3) | Blood Rage | Primal Fury (1st) Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Shara | Warrior | Roc / Thunderbird (T4) | Tactics | Tactics Basic, Melee Basic | 2 / 1 / 1 / 1 |
| Vrolk | Mage | Roc / Thunderbird (T4) | Blood Rage | Primal Fury (1st) Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Durgash | Warrior | Cyclops / Cyclops King (T4) | Scouting | Scouting Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Nazgra | Mage | Cyclops / Cyclops King (T4) | Blood Rage | Primal Fury (1st) Basic, Primal Fury (2nd) Basic | 0 / 1 / 2 / 2 |

Campaign heroes (ADR-298), never drawn at random; each copies a standard hero's preferred creature, specialty, skills and stats.

| Hero | Class | Copies | Who |
|---|---|---|---|
| Gorthak Ironjaw | Warrior | Shara | The Great Khan who united the clans |
| Sakra | Mage | Vrolk | A storm-singer who calls the thunderbirds |
| Snikket | Warrior | Thrak | A goblin cutthroat who rose to lead the raiders |
| Uthgar | Mage | Kurgh | An ogre magus who learned his spells from captured wizards |

## Swamp Bestiary

Faction group **Primal Fury** (no magic school, ADR-063, ADR-069) · native terrain swamp, shallow water · rivals – · offensive machine **Catapult**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Ssarkh | Warrior | Lizardman / Lizard Warrior (T1) | Melee | Melee Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Hissla | Mage | Lizardman / Lizard Warrior (T1) | Blood Rage | Primal Fury (1st) Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Kethra | Warrior | Kappa / Kappa Shaman (T1) | +1 sulfur a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Sethek | Mage | Kappa / Kappa Shaman (T1) | Warcry | Primal Fury (1st) Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Mudjaw | Warrior | Jungle Horror / Jungle Terror (T2) | Armor | Logistics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Murrok | Mage | Jungle Horror / Jungle Terror (T2) | Blood Rage | Primal Fury (1st) Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Vessk | Warrior | Mantis / Serpent Fly (T2) | War Tempo | War Tempo Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Zhessk | Mage | Mantis / Serpent Fly (T2) | Blood Rage | Primal Fury (1st) Basic, Primal Fury (2nd) Basic | 0 / 1 / 2 / 2 |
| Brakkis | Warrior | Basilisk / Greater Basilisk (T3) | Archery | Archery Basic, Leadership Basic | 2 / 1 / 1 / 1 |
| Ilthra | Mage | Basilisk / Greater Basilisk (T3) | Blood Rage | Primal Fury (1st) Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Ylsa | Warrior | Owlbear / Elder Owlbear (T3) | Treasury | Treasury Basic, Spell resistance Basic | 1 / 2 / 1 / 1 |
| Gossk | Mage | Owlbear / Elder Owlbear (T3) | Warcry | Primal Fury (1st) Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Tharg | Warrior | Gorgon / Mighty Gorgon (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Venna | Mage | Gorgon / Mighty Gorgon (T4) | Blood Rage | Primal Fury (1st) Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Oszrak | Warrior | Wyvern / Wyvern Monarch (T4) | Scouting | Scouting Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Szask | Mage | Wyvern / Wyvern Monarch (T4) | Blood Rage | Primal Fury (1st) Basic, Primal Fury (2nd) Basic | 0 / 1 / 2 / 2 |

Campaign heroes (ADR-298), never drawn at random; each copies a standard hero's preferred creature, specialty, skills and stats.

| Hero | Class | Copies | Who |
|---|---|---|---|
| Ssethrak | Warrior | Tharg | The Drowned King, oldest of the lizard lords |
| Nhalla | Mage | Hissla | The frog-witch of the sunken temple |
| Krusk | Warrior | Ssarkh | A snapping-turtle champion of the fighting pits |
| Ruuk | Mage | Gossk | A heron-folk storm caller |

## Conflux

The Firstborn's elemental wardens (non-playable town, [lore.md](lore.md)): only campaign heroes. Each copies a standard hero's specialty,
starting skills and stats, magic school included; its preferred creature is its own element's line (the storm and frost wardens' are the air and
water spirits, whose upgrades are the Storm and Ice Elementals).

| Hero | Class | Preferred creature | Copies | Who |
|---|---|---|---|---|
| Orrath | Warrior | Earth Spirit (T2) | Stellan Frost (Academy) | The warden of the mountain roots |
| Zephra | Warrior | Air Spirit (T2) | Corwin Ashby (Haven) | The wind warden who rides the high storms |
| Ignar | Warrior | Fire Spirit (T2) | Azgaroth (Inferno) | The flame warden of the burning gate |
| Maerin | Warrior | Water Spirit (T2) | Varis (Sylvan) | The tide warden of the drowned shore |
| Ilumiel | Mage | Wisp (T1) | Ansel Wren (Haven) | The light warden, a voice of the Wheel itself |
| Kalderon | Mage | Psychic Elemental (T4) | Belzra (Inferno) | The forge warden, smith of the elemental lords |
| Vaeyra | Mage | Air Spirit (T2) | Theodric Wynn (Academy) | The storm warden of the high peaks |
| Hrimnar | Mage | Water Spirit (T2) | Cassius Thorne (Academy) | The frost warden of the world's northern edge |

## Dragon Utopia

The dragon-blooded wardens (non-playable town): only campaign heroes, copied like Conflux's.

| Hero | Class | Preferred creature | Copies | Who |
|---|---|---|---|---|
| Vharos | Warrior | Dragonyte (T4) | Aldric Vane (Haven) | The Wyrmguard captain who rides at the black dragon's side |
| Seravyn | Warrior | Haspid / Royal Haspid (T4) | Ilvara (Dungeon) | A haspid rider of the cliff eyries |
| Garundar | Warrior | Haspid / Royal Haspid (T4) | Faelar (Sylvan) | The old shield of the Utopia's gate |
| Kaira Ashwing | Warrior | Dragonyte (T4) | Dorian Quill (Academy) | The scout who maps the dragons' lost lands |
| Solvaric | Mage | Dragonyte (T4) | Matthias Crane (Haven) | The gold dragon's high priest |
| Nymeriss | Mage | Haspid / Royal Haspid (T4) | Elowen (Sylvan) | A faerie-dragon singer |
| Ruvak | Mage | Haspid / Royal Haspid (T4) | Orzhul (Inferno) | A red dragon's fire-speaker |
| Morvayne | Mage | Dragonyte (T4) | Dryel (Dungeon) | The black dragon's whisperer |
