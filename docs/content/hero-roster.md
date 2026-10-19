# Hero roster

All 128 roster heroes of the eight playable towns, with the rules that come with a hero. Rules: ADR-048 (roster,
skills, levels), ADR-055 (primary stats), ADR-056 (war machines). The roster is the table
`src/Godot/EternalWars.Godot.Assets/data/heroes.csv` (columns in [development.md](../technical/development.md#editing-heroes));
update this page when the table changes. Skill effects
are listed in [skills.md](skills.md), towns and dwellings in [town-buildings.md](town-buildings.md). Names and
numbers are a draft.

## What every hero has

- **Town and class**: every playable town has **16 heroes**, a **warrior** and a **mage** for each of its eight
  tier 1–4 creature lines. Conflux and Dragon Utopia have no heroes (not playable).
- **Preferred creature**: that line, base and upgrade, gets **+2 Attack, +2 Defense and +1 battlefield speed** in the
  hero's army.
- **Specialty** (HoMM3-style): a daily resource (works), a skill or a spell (no effect yet; see
  [skills.md](skills.md#specialties)).
- **Starting skills**: two skills at Basic.
- **Primary stats** (A / D / SP / K = Attack / Defense / Spell Power / Knowledge): every roster hero starts with
  5 points; warriors 2/1/1/1 or 1/2/1/1, mages 0/0/3/2 or 0/1/2/2. Each level adds 1 to one stat, with odds by class
  ([skills.md](skills.md#primary-stats)).
- **Hiring**: heroes are hired in a town's tavern for 2500 gold ([town-buildings.md](town-buildings.md)). A basic tavern
  offers a native hero and one of an adjacent town; the upgraded one (Brotherhood) two natives, one adjacent and one of
  the rest. The towns form a ring, Sylvan – Haven – Academy – Dungeon – Necropolis – Inferno – back to Sylvan
  ([lore.md](lore.md)). Adjacent means the town's two ring neighbours (Sylvan: Inferno, Haven; Haven: Sylvan, Academy;
  Academy: Haven, Dungeon; Dungeon: Academy, Necropolis; Necropolis: Dungeon, Inferno; Inferno: Necropolis, Sylvan);
  opposing is the town across the ring (Sylvan–Dungeon, Academy–Inferno, Haven–Necropolis) and is offered last. Stronghold and Swamp Bestiary
  have no ring: a basic tavern offers a native and a warrior of another town, the Brotherhood two natives and two
  warriors (`TavernRules.Slots`). Offers are shuffled per player every week, heroes on the map
  are not offered, and a player has at most 8 heroes, or the map's lower limit (ADR-060, ADR-068).
- **Morale and luck** 0 at the start (maps and the editor can set them, ADR-053).
- **Army**: up to 7 stacks. A hero without creatures cannot attack.
- **War machines**: its town's offensive machine from the start; the healing tent and ammo cart are bought (below).

The roster follows one template in every town: the hero for the same slot (for example the warrior who prefers the
first tier 1 line) has the same kind of specialty, the same starting skills and the same stats everywhere; only the
town's school and creatures differ. In Stronghold and Swamp Bestiary (no school, ADR-063) "own magic" is the faction
group's first skill (`rage.first` = Blood Rage, `adrenaline.first`), and "a spell" / "own attunement" specialties are
`skill rage.first` / `skill rage.second` (= Warcry; Adrenaline likewise). The Stronghold group is called Primal Fury
(working title, ADR-067); the roster table below still says "Rage" and the ids stay `rage.*`.

| Slot (preferred line) | Warrior: specialty · skills · stats | Mage: specialty · skills · stats |
|---|---|---|
| Tier 1, first line | Melee · Melee, Tactics · 2/1/1/1 | a spell · own magic, Treasury · 0/0/3/2 |
| Tier 1, second line | +350 gold · Armor, Melee · 1/2/1/1 | own attunement · own magic, Spell resistance · 0/1/2/2 |
| Tier 2, first line | Logistics · Logistics, Archery · 2/1/1/1 | a spell · own magic, Logistics · 0/0/3/2 |
| Tier 2, second line | +2 ore · Drill, Armor · 1/2/1/1 | +1 mercury · own magic, own attunement · 0/1/2/2 |
| Tier 3, first line | Archery · Archery, Pathfinding · 2/1/1/1 | a spell · own magic, Prospecting · 0/0/3/2 |
| Tier 3, second line | +2 lumber · Treasury, Tactics · 1/2/1/1 | own attunement · own magic, Armor · 0/1/2/2 |
| Tier 4, first line | Tactics · Tactics, Armor · 2/1/1/1 | a spell · own magic, Pathfinding · 0/0/3/2 |
| Tier 4, second line | +350 gold · Troop management, Melee · 1/2/1/1 | +350 gold · own magic, own attunement · 0/1/2/2 |

"First" and "second" line follow the creature table's order. Open: whether heroes should differ more between towns
(per-town stat odds, as in HoMM3, are an option in ADR-055).

## War machines

Every hero has up to three machines (ADR-056). Draft numbers:

| Machine | Slot | Towns | Attack / Defense | Damage | Health | Initiative | Notes |
|---|---|---|---|---|---|---|---|
| Ballista | offensive | Haven, Necropolis | 10 / 10 | 40–55 | 250 | 10 | |
| Cannon | offensive | Academy, Inferno | 12 / 8 | 55–75 | 220 | 7 | |
| Catapult | offensive | Stronghold, Swamp Bestiary | 10 / 10 | 45–70 | 260 | 8 | |
| Glaivethrower | offensive | Sylvan, Dungeon | 11 / 9 | 25–35 | 200 | 12 | shoots twice |
| Healing tent | tent | all | 0 / 5 | – | 150 | – | heals every own stack each round |
| Ammo cart | cart | all | 0 / 5 | – | 200 | – | extra or unlimited shots |

- Every hero starts with its town's offensive machine (custom heroes without a town: the ballista). It fights only in
  sieges until **Advanced Ballistics**, then in every battle.
- A town's **blacksmith** sells the tent (750 gold), the cart (1000) and a lost offensive machine (2500) to the
  visiting hero; a hero always gets its own town's offensive machine.
- Machines stand at the back of the field, never move, fight in melee or retaliate, and are lost when destroyed.
  Skills: [skills.md](skills.md#war-machines-machines).

## Haven

School **Light** · native terrain highlands, grass · rivals Necropolis, Inferno · offensive machine **Ballista**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Aldric Vane | Warrior | Footman / Crusader (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Ansel Wren | Mage | Footman / Crusader (T1) | spell: Bless | Light magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Maren Holt | Warrior | Pikeman / Guardian (T1) | +350 gold a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Ilse Varro | Mage | Pikeman / Guardian (T1) | Light attunement | Light magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Corwin Ashby | Warrior | Zealot / Inquisitor (T2) | Logistics | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Matthias Crane | Mage | Zealot / Inquisitor (T2) | spell: Heal | Light magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Brenna Stahl | Warrior | Archer / Marksman (T2) | +2 ore a day | Drill Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Rowena Pell | Mage | Archer / Marksman (T2) | +1 mercury a day | Light magic Basic, Light attunement Basic | 0 / 1 / 2 / 2 |
| Edric Morrow | Warrior | Monk / Priest (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Fabian Lorne | Mage | Monk / Priest (T3) | spell: Smite | Light magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Sabine Kell | Warrior | Griffin / Royal Griffin (T3) | +2 lumber a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Agathe Sorrel | Mage | Griffin / Royal Griffin (T3) | Light attunement | Light magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Gerrit Hale | Warrior | Lancer / Champion (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Cyprian Vey | Mage | Lancer / Champion (T4) | spell: Sanctuary | Light magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Ottilie Brand | Warrior | Paladin / Sacred Warrior (T4) | +350 gold a day | Troop management Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Elodie Marsh | Mage | Paladin / Sacred Warrior (T4) | +350 gold a day | Light magic Basic, Light attunement Basic | 0 / 1 / 2 / 2 |

## Sylvan

School **Nature** · native terrain forest floor, grass · rivals Dungeon, Necropolis · offensive machine **Glaivethrower**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Thalion | Warrior | Pixie / Sprite (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Elowen | Mage | Pixie / Sprite (T1) | spell: Entangle | Nature magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Faelar | Warrior | Satyr / Satyr Warrior (T1) | +350 gold a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Taeral | Mage | Satyr / Satyr Warrior (T1) | Nature attunement | Nature magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Ilyra | Warrior | Blade Dancer / War Dancer (T2) | Logistics | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Ysolde | Mage | Blade Dancer / War Dancer (T2) | spell: Regrowth | Nature magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Caelwyn | Warrior | Hunter / Arcane Archer (T2) | +2 ore a day | Drill Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Faenor | Mage | Hunter / Arcane Archer (T2) | +1 mercury a day | Nature magic Basic, Nature attunement Basic | 0 / 1 / 2 / 2 |
| Sylvara | Warrior | Druid / Druid Elder (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Ithiel | Mage | Druid / Druid Elder (T3) | spell: Swarm | Nature magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Elaith | Warrior | Pegasus / Silver Pegasus (T3) | +2 lumber a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Sariel | Mage | Pegasus / Silver Pegasus (T3) | Nature attunement | Nature magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Varis | Warrior | Qilin / Pristine Unicorn (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Calen | Mage | Qilin / Pristine Unicorn (T4) | spell: Barkskin | Nature magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Neriah | Warrior | Treant / Ancient Treant (T4) | +350 gold a day | Troop management Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Nymara | Mage | Treant / Ancient Treant (T4) | +350 gold a day | Nature magic Basic, Nature attunement Basic | 0 / 1 / 2 / 2 |

## Academy

School **Arcana** · native terrain snow, ice · rivals Inferno, Dungeon · offensive machine **Cannon**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Corvin Halde | Warrior | Gremlin / Gremlin Master (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Aurelian Vex | Mage | Gremlin / Gremlin Master (T1) | spell: Magic Arrow | Arcana magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Stellan Frost | Warrior | Defender / Shield Guard (T1) | +350 gold a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Seraphine Lux | Mage | Defender / Shield Guard (T1) | Arcana attunement | Arcana magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Ingrid Vael | Warrior | Gargoyle / Obsidian Gargoyle (T2) | Logistics | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Theodric Wynn | Mage | Gargoyle / Obsidian Gargoyle (T2) | spell: Haste | Arcana magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Tobias Rime | Warrior | Iron Golem / Animated Armor (T2) | +2 ore a day | Drill Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Isolde Marrow | Mage | Iron Golem / Animated Armor (T2) | +1 mercury a day | Arcana magic Basic, Arcana attunement Basic | 0 / 1 / 2 / 2 |
| Hedda Strom | Warrior | Snow Maiden / Enchantress (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Cassius Thorne | Mage | Snow Maiden / Enchantress (T3) | spell: Frost Bolt | Arcana magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Kasimir Lode | Warrior | Sorcerer / Mage (T3) | +2 lumber a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Mirela Quist | Mage | Sorcerer / Mage (T3) | Arcana attunement | Arcana magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Runa Halvard | Warrior | Djinn / Djinn Sultan (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Lucan Esk | Mage | Djinn / Djinn Sultan (T4) | spell: Dispel | Arcana magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Dorian Quill | Warrior | Naga / Naga Queen (T4) | +350 gold a day | Troop management Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Valeska Rune | Mage | Naga / Naga Queen (T4) | +350 gold a day | Arcana magic Basic, Arcana attunement Basic | 0 / 1 / 2 / 2 |

## Necropolis

School **Death** · native terrain cursed lands · rivals Haven, Sylvan · offensive machine **Ballista**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Morthal | Warrior | Skeleton / Skeleton Warrior (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Necrys | Mage | Skeleton / Skeleton Warrior (T1) | spell: Raise Dead | Death magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Vexa | Warrior | Plague Bearer / Flesh Golem (T1) | +350 gold a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Sepulka | Mage | Plague Bearer / Flesh Golem (T1) | Death attunement | Death magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Grimwald | Warrior | Ghoul / Alghoul (T2) | Logistics | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Vorlath | Mage | Ghoul / Alghoul (T2) | spell: Curse | Death magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Ulric Ashgrave | Warrior | Death Weaver / Fate Weaver (T2) | +2 ore a day | Drill Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Ysmera | Mage | Death Weaver / Fate Weaver (T2) | +1 mercury a day | Death magic Basic, Death attunement Basic | 0 / 1 / 2 / 2 |
| Hespera | Warrior | Vampire / Vampire Lord (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Kael Duskmire | Mage | Vampire / Vampire Lord (T3) | spell: Drain Life | Death magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Corvane | Warrior | Wight / Wraith (T3) | +2 lumber a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Lethe | Mage | Wight / Wraith (T3) | Death attunement | Death magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Drusa | Warrior | Dark Knight / Death Knight (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Ozmund | Mage | Dark Knight / Death Knight (T4) | spell: Plague | Death magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Malgrin | Warrior | Lich / Arch Lich (T4) | +350 gold a day | Troop management Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Thessaly | Mage | Lich / Arch Lich (T4) | +350 gold a day | Death magic Basic, Death attunement Basic | 0 / 1 / 2 / 2 |

## Inferno

School **Chaos** · native terrain lava · rivals Haven, Academy · offensive machine **Cannon**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Azgaroth | Warrior | Imp / Familiar (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Belzra | Mage | Imp / Familiar (T1) | spell: Fireball | Chaos magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Brakka | Warrior | Horned Demon / Horned Overseer (T1) | +350 gold a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Nyxoth | Mage | Horned Demon / Horned Overseer (T1) | Chaos attunement | Chaos magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Vorhaz | Warrior | Hell Hound / Cerberus (T2) | Logistics | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Ashmedai | Mage | Hell Hound / Cerberus (T2) | spell: Inferno | Chaos magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Skarn | Warrior | Tormentor / Lacerator (T2) | +2 ore a day | Drill Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Lilivex | Mage | Tormentor / Lacerator (T2) | +1 mercury a day | Chaos magic Basic, Chaos attunement Basic | 0 / 1 / 2 / 2 |
| Ixira | Warrior | Succubus / Succubus Mistress (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Pyrrhon | Mage | Succubus / Succubus Mistress (T3) | spell: Terror | Chaos magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Malphor | Warrior | Hell Charger / Nightmare (T3) | +2 lumber a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Veshra | Mage | Hell Charger / Nightmare (T3) | Chaos attunement | Chaos magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Zarduk | Warrior | Pit Fiend / Pit Lord (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Orzhul | Mage | Pit Fiend / Pit Lord (T4) | spell: Hellfire | Chaos magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Kezra | Warrior | Juggernaut / Ravager (T4) | +350 gold a day | Troop management Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Sythra | Mage | Juggernaut / Ravager (T4) | +350 gold a day | Chaos magic Basic, Chaos attunement Basic | 0 / 1 / 2 / 2 |

## Dungeon

School **Shadow** · native terrain subterranean · rivals Academy, Sylvan · offensive machine **Glaivethrower**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Vaelrith | Warrior | Troglodyte / Deep Troglodyte (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Nethra | Mage | Troglodyte / Deep Troglodyte (T1) | spell: Blind | Shadow magic Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Zhaun | Warrior | Scout / Stalker (T1) | +350 gold a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Sabal | Mage | Scout / Stalker (T1) | Shadow attunement | Shadow magic Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Ilvara | Warrior | Medusa / Putrid Medusa (T2) | Logistics | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Ghaunis | Mage | Medusa / Putrid Medusa (T2) | spell: Shadow Step | Shadow magic Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Kharis | Warrior | Minotaur / Minotaur Guard (T2) | +2 ore a day | Drill Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Zesyra | Mage | Minotaur / Minotaur Guard (T2) | +1 mercury a day | Shadow magic Basic, Shadow attunement Basic | 0 / 1 / 2 / 2 |
| Shyntra | Warrior | Dark Raider / Grim Raider (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Dryel | Mage | Dark Raider / Grim Raider (T3) | spell: Despair | Shadow magic Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Vorn | Warrior | Shadow Mistress / Shadow Matriarch (T3) | +2 lumber a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Vhaeris | Mage | Shadow Mistress / Shadow Matriarch (T3) | Shadow attunement | Shadow magic Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Lysara | Warrior | Manticore / Scorpicore (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Phyrra | Mage | Manticore / Scorpicore (T4) | spell: Dark Bolt | Shadow magic Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Xundar | Warrior | Spectator / Beholder (T4) | +350 gold a day | Troop management Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Ilphar | Mage | Spectator / Beholder (T4) | +350 gold a day | Shadow magic Basic, Shadow attunement Basic | 0 / 1 / 2 / 2 |

## Stronghold

Faction group **Rage** (no magic school, ADR-063) · native terrain rough, wasteland · rivals – · offensive machine **Catapult**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Grukk | Warrior | Goblin / Hobgoblin (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Ugra | Mage | Goblin / Hobgoblin (T1) | Rage (1st)| Rage (1st) Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Mazga | Warrior | Harpy / Fury (T1) | +350 gold a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Zhaka | Mage | Harpy / Fury (T1) | Rage (2nd) | Rage (1st) Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Thrak | Warrior | Orc / Orc Berserker (T2) | Logistics | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Mograth | Mage | Orc / Orc Berserker (T2) | Rage (1st)| Rage (1st) Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Urzog | Warrior | Centaur / Centaur Warchief (T2) | +2 ore a day | Drill Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Yazna | Mage | Centaur / Centaur Warchief (T2) | +1 mercury a day | Rage (1st) Basic, Rage (2nd) Basic | 0 / 1 / 2 / 2 |
| Borka | Warrior | Ogre / Ogre Mage (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Kurgh | Mage | Ogre / Ogre Mage (T3) | Rage (1st)| Rage (1st) Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Kragash | Warrior | Wolf Rider / Wolf Raider (T3) | +2 lumber a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Ashka | Mage | Wolf Rider / Wolf Raider (T3) | Rage (2nd) | Rage (1st) Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Shara | Warrior | Roc / Thunderbird (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Vrolk | Mage | Roc / Thunderbird (T4) | Rage (1st)| Rage (1st) Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Durgash | Warrior | Cyclops / Cyclops King (T4) | +350 gold a day | Troop management Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Nazgra | Mage | Cyclops / Cyclops King (T4) | +350 gold a day | Rage (1st) Basic, Rage (2nd) Basic | 0 / 1 / 2 / 2 |

## Swamp Bestiary

Faction group **Adrenaline** (no magic school, ADR-063) · native terrain swamp, shallow water · rivals – · offensive machine **Catapult**

| Hero | Class | Preferred creature | Specialty | Starting skills | A / D / SP / K |
|---|---|---|---|---|---|
| Ssarkh | Warrior | Lizardman / Lizard Warrior (T1) | Melee | Melee Basic, Tactics Basic | 2 / 1 / 1 / 1 |
| Hissla | Mage | Lizardman / Lizard Warrior (T1) | Adrenaline (1st)| Adrenaline (1st) Basic, Treasury Basic | 0 / 0 / 3 / 2 |
| Kethra | Warrior | Kappa / Kappa Shaman (T1) | +350 gold a day | Armor Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Sethek | Mage | Kappa / Kappa Shaman (T1) | Adrenaline (2nd) | Adrenaline (1st) Basic, Spell resistance Basic | 0 / 1 / 2 / 2 |
| Mudjaw | Warrior | Jungle Horror / Jungle Terror (T2) | Logistics | Logistics Basic, Archery Basic | 2 / 1 / 1 / 1 |
| Murra | Mage | Jungle Horror / Jungle Terror (T2) | Adrenaline (1st)| Adrenaline (1st) Basic, Logistics Basic | 0 / 0 / 3 / 2 |
| Vessk | Warrior | Mantis / Dragon Mantis (T2) | +2 ore a day | Drill Basic, Armor Basic | 1 / 2 / 1 / 1 |
| Zhessk | Mage | Mantis / Dragon Mantis (T2) | +1 mercury a day | Adrenaline (1st) Basic, Adrenaline (2nd) Basic | 0 / 1 / 2 / 2 |
| Brakkis | Warrior | Basilisk / Greater Basilisk (T3) | Archery | Archery Basic, Pathfinding Basic | 2 / 1 / 1 / 1 |
| Ilthra | Mage | Basilisk / Greater Basilisk (T3) | Adrenaline (1st)| Adrenaline (1st) Basic, Prospecting Basic | 0 / 0 / 3 / 2 |
| Ylsa | Warrior | Owlbear / Elder Owlbear (T3) | +2 lumber a day | Treasury Basic, Tactics Basic | 1 / 2 / 1 / 1 |
| Gossk | Mage | Owlbear / Elder Owlbear (T3) | Adrenaline (2nd) | Adrenaline (1st) Basic, Armor Basic | 0 / 1 / 2 / 2 |
| Tharg | Warrior | Gorgon / Mighty Gorgon (T4) | Tactics | Tactics Basic, Armor Basic | 2 / 1 / 1 / 1 |
| Venna | Mage | Gorgon / Mighty Gorgon (T4) | Adrenaline (1st)| Adrenaline (1st) Basic, Pathfinding Basic | 0 / 0 / 3 / 2 |
| Oszrak | Warrior | Haspid / Royal Haspid (T4) | +350 gold a day | Troop management Basic, Melee Basic | 1 / 2 / 1 / 1 |
| Szask | Mage | Haspid / Royal Haspid (T4) | +350 gold a day | Adrenaline (1st) Basic, Adrenaline (2nd) Basic | 0 / 1 / 2 / 2 |
