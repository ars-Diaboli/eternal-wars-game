# Decision log

Short ADRs. Add new entries at the bottom; never rewrite accepted ones, supersede them instead.
ADR-001 … ADR-100 are in [decisions-archive.md](decisions-archive.md). Find an ADR with a search for `### ADR-NNN` rather than reading either file whole.

### ADR-101: Victory screen, ranking, replays and the main menu (FLOW-4, UI-2, 2026-10-29)
**Status**: accepted (2026-10-29), the owner's answers to FLOW-4 and UI-2 (ADR-092); the score and ranking core is built (ADR-102), the
map's difficulty and the victory screen too (ADR-103), the main menu (ADR-104), the map events' check (ADR-131); the replay viewer is not. Points marked *(assumed)* are
the agent's reading of a short answer; numbers marked *(placeholder)* wait for play-testing.

- **Which games are ranked** (FLOW-4 a, changed): worked out from the map, with **no "not ranked" flag** in the editor *(assumed: the answer
  kept "worked out" and not the flag)*. A map is campaign-like, and not ranked, when a human player starts without a town (ADR-075), **or
  when its map events favour human players over computer players**: an event that gives something only to humans (a "player is human"
  condition, or the ready forms' "computer may trigger" left off, EVT-2 c) *(assumed reading)*. The event check comes with the map events
  (ADR-096); until then only the town rule applies. Only a won game scores; arena maps are never ranked (ADR-100).
- **Difficulty** (FLOW-4 b, changed): the difficulty is the complexity (ADR-063). **"Map default"** (the map's own starting resources) counts as
  **Normal**, unless the map author sets the map's difficulty in the editor (a map property: which complexity the map's starting resources
  stand for; map format bump). The difficulty **does not change the AI**: the computer plays the same at every level, as today.
- **The turn timer raises the score** (FLOW-4 b, changed): a shorter timer is worth more. Factor *(placeholder)*: off or over 10 minutes
  ×1.0, 6–10 minutes ×1.05, 3–5 minutes ×1.10, 1–2 minutes ×1.20. Nothing else changes the score (opponents, map size, fog).
- **The rest of FLOW-4 as proposed**: score = max(10, 200 − days) × the difficulty factor (Beginner 80 %, Easy 100 %, Normal 130 %, Hard
  160 %, Expert 200 %) × the timer factor (c); 14 rank titles named after our creatures, weakest to a dragon (d); each human winner gets a row,
  hot-seat included (e); maps made in the editor go into the shipped maps' table marked "custom" (f); the row: rank, player name (asked on
  the victory screen), faction, map or template (with size), difficulty, days, score, title, date (g); the top 20 per table, kept per
  computer in the user data folder, shown on the victory screen (the new row highlighted) and under "Leaderboard" in the main menu (h).
- **Replay files** (UI-2 a, g, as proposed): each ranking row keeps its game's record (written when the row is made); "Save replay" writes it
  to `user://replays` as `*.ewreplay.json` under a name the player gives. **Every end screen** (a defeat, a game that is not ranked, the
  arena) offers "Save replay" too. A replay made with other content fails like a save: "made with other content" (ADR-058).
- **Main menu** (UI-2 b, changed): the game opens on a main menu: **New game, Load, Multiplayer, Campaign, Watch a replay, Leaderboard,
  Quit**. Multiplayer (LAN, ADR-098) and Campaign (Phase 6) are shown disabled until they exist *(assumed)*.
- **The viewer** (UI-2 c, d, e, h, as proposed): the whole map revealed by default, or any player's eyes; play, pause, the four speeds
  (ADR-095), next turn, next day, and back to the start of any day (replayed from the start, ADR-058); battles move by move at the battle
  speed with "skip to the result"; computer turns in full; two tabs, Replays and Saves (autosaves too); a save plays to its last command and
  stops.
- **Take over only in battles** (UI-2 f, changed): "Play from here" exists **only in a battle** for now: the viewer stops the replay in a
  battle and plays that battle on by hand from that moment, as a separate game that is not ranked and does not change the replay *(assumed
  reading)*. Taking over on the adventure map comes later; until then a save is played on with Load.
- Fits the core as it is: the record is already the replay (`GameRecordDocument`), the viewer runs its commands with `session.Execute`
  one at a time while the views show the events; "back to a day" replays from the start; a battle taken over starts a session from the record
  cut at that command. New are the ranking file, the score rules (core), the map's difficulty property, the main menu, the viewer's controls
  and the reveal-all view.

### ADR-102: Score and ranking core (implementation, 2026-10-29)
**Status**: implemented (2026-10-29). Carries out ADR-101's score and ranking rules (FLOW-4) in core; the victory screen, the main menu and the
replay viewer are later items. Choices made here are marked *(choice)*.

- **Start town**: `Player.StartTown` is the town type of the player's first town (lowest id) when the game starts (`GameFlowService.Begin`),
  null without one. It is the row's faction and the campaign-like check. It is not in the digest: no rule reads it.
- **Score** (`ScoreRules`, Domain): `IsRanked` is false when a human player that took part started without a town (the map-events check
  waits for the map events); `Scores` gives one `GameScore` per human winner of a finished game (none when nobody won, only computers won or
  the map is campaign-like). Score = max(10, 200 − days) × difficulty % × turn timer %, rounded down; days is the day the game ended;
  "Map default" counts as Normal (the map's own difficulty is the next roadmap item). Rank titles: 14 bands, lower bounds 0, 20, 40, 60, 80,
  100, 125, 150, 175, 200, 230, 260, 300, 350, named Imp, Goblin, Skeleton, Pikeman, Centaur, Medusa, Griffin, Vampire, Ogre, Naga, Pit Lord,
  Phoenix, Titan, Black Dragon *(choice; placeholders)*.
- **Ranking file** (`RankingFile`, Core.Data): `ranking.json` in the folder the front-end passes (the user data folder), two tables
  (`RandomMaps` for generated games, `Maps` for the rest, editor maps marked `custom` by the caller, since there are no shipped maps yet),
  the best 20 each. A new row goes below rows with the same score *(choice)*. `AddGame` writes the rows, the game's record once in
  `ranking/<id>.ewreplay.json` (several human winners share it) and deletes the records no row names any more; it returns the places for the
  victory screen to highlight. Player names come from the caller (the victory screen asks), else the player's name. A missing or broken file
  is an empty ranking. The title is stored as the creature's name at the time *(choice: the row is a record of what was shown)*.
- Tests: `ScoreRulesTests` (the formula, the factors, the titles, start towns, campaign-like maps, computer and allied winners),
  `RankingFileTests` (order and ties, the 20-row cap, the file round trip and broken files, rows of hand-made and random maps, the record
  written and replayed, records of rows that fall out deleted, nothing written for a game that does not make the table).

### ADR-103: The map's difficulty and the victory screen (implementation, 2026-10-29)
**Status**: implemented (2026-10-29); the Godot screens build but have not been run (no Godot on the machine that built them, no smoke
scenario yet). Carries out ADR-101's map difficulty and victory screen. Map format **v26**. Choices made here are marked *(choice)*.

- **The map's difficulty**: `ScenarioRules.Difficulty` (a `Complexity`, null = not set), in the map file as `rules.difficulty` (v26, left out
  when not set; older maps load without). `ScoreRules.Difficulty` is the game's complexity, else the map's, else Normal. It changes no rule.
  The editor's conditions panel has a "Difficulty" row (not set, Beginner … Expert) through `SetScenarioRules`, so it is undoable.
- **Victory screen** (`GameResultPanel`): after the reason, winners and day, each human winner's score line (points, rank title, day,
  difficulty, turn timer) and a name field (the player's name to start with). "Enter the ranking", or closing the panel, enters the game
  once *(choice: closing enters it too, so a win is never lost)*; the table it went into is then shown with this game's rows highlighted,
  or "did not make the ranking". A human win on a campaign-like map says "This map is not ranked".
- **Save replay** on every end screen: a name field and a button; `GameSaves.SaveReplay` writes the record to `user://replays` as
  `<name>.ewreplay.json` (an empty name gives "<map> day <n>") *(choice)*.
- **Shipped or custom**: the game app counts a hand-made map as shipped when its name is one of the built-in scenarios (Demo Valley, Two
  Towns), custom otherwise *(choice; a save does not record where its map came from)*. The ranking is in the user data folder.
- Tests: `MapDifficultyTests` (editor and undo, the map file, older maps), `ScoreRulesTests` (the map's difficulty and the chosen complexity),
  `RankingFileTests` (a saved replay replays to the end).

### ADR-104: The main menu (implementation, 2026-10-29)
**Status**: implemented (2026-10-29); builds, not run in Godot yet (smoke scenario `game-main-menu` written, not run). Carries out
ADR-101's main menu (UI-2). Choices made here are marked *(choice)*.

- **The game opens on the main menu** (`MainMenuScreen`): New game, Load, Multiplayer, Campaign, Watch a replay, Leaderboard, Quit.
  Multiplayer, Campaign and **Watch a replay** are disabled with a tooltip until they exist *(choice: Watch a replay waits for the replay
  viewer)*; disabled items take no focus, New game has it, so the menu works with the arrow keys and Enter.
- **New game** opens the new-game screen, which got a Back button; **Load** opens the save list of the F10 menu (as before a game);
  **Leaderboard** (`LeaderboardPanel`) shows the two ranking tables, Maps and Random maps, one tab each, through the same table view as the
  victory screen (`RankingTableView`); **Quit** quits. Escape goes back from the new-game screen and the leaderboard.
- **Back to the main menu** from a game: "Main menu" on the end screen (it enters the ranking first, like closing it) and in the F10 menu
  during a game *(choice: no confirmation; the autosave keeps the start of the turn)*. Leaving takes the game's world and HUD down.
- **`--new-game`** opens the game on the new-game screen *(choice)*: the smoke scenarios pass it, so their clicks and keys are unchanged.
- Save replay on every end screen was already built (ADR-103); the arena's result screen will offer it when the arena exists (ADR-100).

### ADR-105: Pandora's Box (implementation, 2026-10-30)
**Status**: implemented (2026-10-30); core tested, the Godot parts build but have not been run in Godot. Carries out ADR-100's Pandora's Box
(MAP-35, ADR-096). Map format **v27**, generator version **7**. Choices made here are marked *(choice)*.

- **The object** (`PandorasBox`, kind `PandorasBox`, definition `pandoras-box`, one cell): `Contents` (`BoxContents`: resources,
  experience, mana, morale, luck, the four primary stats, skills with a level, artifacts, creature stacks), an optional `Guard` (up to seven
  stacks) and an optional `Message`. Gains only: a negative value, morale or luck above +3, an unknown skill or a level the skill does not
  have are refused by the editor and are format errors in a map file.
- **A visit** asks "open?" (`PandorasBoxDecision`, answered by `AnswerPandorasBoxCommand`, record name `pandorasBox`), showing neither
  the contents nor the guard. Leaving keeps the box. A guarded box turns away a hero without an army *(choice)*.
- **The guard** fights after "open?", split like a creature bank's guard, with no Tactics choice and no deployment *(choice: like banks)*.
  The box opens only when the hero wins; a lost or fled battle leaves the guard whole, as at a bank *(choice)*.
- **Opening**: the box vanishes, then the hero gets resources, stats, mana (up to twice its maximum, as the Mana Vortex), morale and luck
  for its next battle, and skills (raised to at least the level given). Skills whose group the hero has no room for are lost, and so are
  artifacts that do not fit a full backpack and creatures without a slot *(choice: HoMM3 asks which stack to drop; here they are simply
  lost)*. Then `PandorasBoxOpened` reports what the hero actually got, with the message. The experience comes last, for its level-ups.
- **AI**: knows the contents and values them in gold: resources at their value, experience 1:1, a stat 750, morale and luck 150 a point,
  a skill 1000 a level gained, artifacts as pickups, creatures at 85 % where they fit, mana nothing until spells exist. It weighs that
  against the box's own guard (else the monsters next to it) through the usual simulated fight, and at the box opens only when that fight
  is safe.
- **Validator**: an empty box is an error; so are unknown artifacts and creatures, and contents that are not gains.
- **Editor**: the inspector has the box's resources, experience, mana, morale, luck, stats, skills, artifacts, creatures, guard and
  message. Each list shows its entries plus one empty row; every change is one undoable `SetPandorasBox` edit.
- **Generator** *(placeholders)*: from a value of 5000, one treasure item in five that is not a catalogue site is a box. It holds one of
  gold (the value, in steps of 500), experience (four fifths of it) or one stack of creatures worth the value, drawn evenly. It is guarded
  by a monster next to it like any item of its value, and has no guard of its own.
- **Front-end**: a decision prompt with Open and Leave, a notice with the message and what was inside, and the guard named "Pandora's box
  guard" in battle. Not built: spells (Phase 5), running a hero event instead (map events, EVT), and showing the contents on arena day 1
  (the arena).

### ADR-106: Siege rules; guild spell draw, spell elements and summons (CMB-11, MAG-13, 2026-10-30)
**Status**: accepted (2026-10-30), the owner's answers to CMB-11 and MAG-13 (ADR-099): the proposals, with the changes below. The sieges
are **built in ADR-109**; the magic part is not (magic is not built). Points marked *(assumed)* are the agent's reading of a short answer; numbers are
placeholders.

- **Sieges** (CMB-11; [combat.md](../design/combat.md#sieges-adr-099-adr-106-built-adr-109)):
  - As proposed: every battle at a town with a Fort is a siege, hero inside or garrison only; a town without one has an ordinary battle (a).
    **4 wall sections and the gate**, 2 hit points a section at Fort and Citadel, 3 at Castle; a destroyed section is a gap (b). The keep
    (Citadel) and the two towers (Castle) shoot once a round each, **10 + 2 per town building**, the keep double, and can be destroyed (3
    points) (d). Flyers and teleporters cross the walls, shooters behind them get the 1/2 cover, one defending shooting stack may stand in the
    keep (g). The AI attacks a fortified town only when its estimate wins against the walls, and defends behind them (h).
  - **The gate** (c): the defenders pass, the attackers break it; it has **1 hit point**. A machine **firing by itself** aims at the gate
    only once **all four wall sections are destroyed**; a player in control may aim at it any time *(assumed)*. After the gate, the keep and
    towers *(assumed order)*.
  - **The moat** (e; with the Citadel): a creature entering it stops and takes damage worth **about two tower shots**: placeholder
    **2 × (10 + 2 per town building)** to the stack, each time it enters (the proposal had 10 % of its health).
  - **No free siege catapult** (f; the proposal had one for every attacker): the attacking hero's **own offensive machine** (ballista,
    cannon, catapult or glaivethrower) is what hits the walls, under ADR-063's siege Ballistics. The catapult is only Stronghold's and Swamp
    Bestiary's offensive machine, with no siege role of its own. The owner's answer says "ballista" for the attacking machine; it is read
    as any town's offensive machine *(assumed)*. An attacker whose machine was destroyed (and not bought again at a blacksmith) cannot break
    walls; Earthquake still can.
- **Guild spells** (MAG-13; [spells.md](../content/spells.md), [magic.md](../design/magic.md)):
  - As proposed: a guild's 3 / 3 / 2 / 2 spells are **drawn per town when the game starts** from the game's random source, the editor may
    fix some (a, a map format bump with the item); the extra-school buildings draw theirs the same way, from their own school (c).
  - **Adventure spells** are drawn like the others (b): no guild level always offers one (no guaranteed Town Portal in Academy guild 4).
- **The list** (MAG-13 d): as drafted, with the new tags `holy` and `summon`, and **every spell gets an element**: air, earth, fire or water
  (a new "Element" column in spells.md, one per spell, drafted with HoMM3's spell elements as reference; Magic Arrow is all four, Bind
  Elemental takes the bound elemental's). The element is not an immunity tag. The **elemental orbs** now raise an **element's** spells of
  any school: ADR-100's six school orbs become four (Orb of the Firmament, Orb of Silt, Orb of Tempestuous Fire, Orb of Driving Rain; effect
  kind `element-power`, was `school-power`), so the artifact list has **92** items (46 Major; ADR-100's count of 49 Major was one too high).
- **Summoned creatures** (MAG-13 e; the proposal had a new neutral Wolf): summons call **town creatures**, never neutrals; elementals are the
  exception (Conflux rows). Summon the Pack becomes **Call the Herd** (same id `nature.summon-pack`): Sylvan Pegasi, Silver Pegasi at rank
  III *(assumed creature)*; Summon Demons calls Inferno Horned Demons *(assumed creature)*; Awaken the Grove Sylvan Treants (Ancient at rank
  III); Hellgate Inferno Imps (Familiars); Bind Elemental Conflux air, earth, fire and water elementals (was "ice").

### ADR-107: Rarer specialties: the HERO-8 proposals, for now (2026-10-30)
**Status**: accepted for now (2026-10-30): the owner's answer to HERO-8 (ADR-093) is "use the proposals for now", so they may still change
once the roster is play-tested. **Nothing implemented.**

- **The whole roster** (a): ADR-093's numbers are for all 128 heroes and replace today's 48 resource specialists and 8 Logistics
  specialists: 4 heroes with +350 gold a day, 6 with +1 rare resource a day (2 sulfur, 2 crystal, 2 mercury), 2 Necromancy and 2 Logistics
  specialists.
- **No ore or lumber specialists** (b): their mines are common.
- **Heroes that lose their specialty** (c; about 50): get a specialty in their **first starting skill** (+5 % per hero level); its effect is
  built where it is missing (Armor, Treasury, War Tempo, …).
- **Drawn once** (d) into `heroes.csv` with a fixed seed (the same heroes every game), and listed in `hero-roster.md`.
- **Factions** (e): no two gold heroes and no two rare-resource heroes of one town, but a town may have one of each; all eight towns take
  part; any class.
- **Necromancy** (f): both specialists are Necropolis heroes: the mage slot that has Death attunement as its specialty today, and a warrior.
  **+5 points on top of the skill** (Basic 5 → 10 % … Master 20 → 25 %), stacking with the Necromancy Amplifier; only with the skill.
- **Logistics** (g): a flat **+10 % movement on land**, even without the Logistics skill. The other skill specialties (Melee, Archery,
  Tactics, the attunements) keep +5 % per hero level.

### ADR-108: Assembled relics: the rules, and six more relics (CONT-7, 2026-10-30)
**Status**: accepted (2026-10-30), the owner's answer to CONT-7 (ADR-100): the proposals, except (f). **Nothing implemented**; the new
relics are a content draft in [artifacts.md](../content/artifacts.md#assembled-relics).

- **As proposed**: the game asks to join when the hero puts on the last piece, and the hero screen has an "Assemble" button too (a); the
  relic sits in the first piece's slot and the other pieces' slots stay blocked (b); it can be taken apart any time on the hero screen, the
  pieces going back to their slots (c); a defeated hero's relic goes to the winner whole when its slots are free, otherwise as pieces into
  the backpack, and the Artifact Merchant and the Sacrificial Altar take and sell pieces only (d); the AI always joins and values a piece
  more when it already has the others (e); the dropped effects stay dropped (g).
- **More assembled relics** (f; the proposal kept three): HoMM3's combinations named in the question, and the bow and Necromancy sets turned
  into relics. Drafted *(the pieces, effects and numbers are the agent's draft)*:
  - **Elixir of Life**: Ring of Vitality, Ring of Life (new) and Vial of Lifeblood; creature health, +30 % in all (HoMM3's regeneration was
    dropped by CONT-6).
  - **Ring of the Magi**: Collar, Ring and Cape of Conjuring (new, +1 Spell Power each); +3 Spell Power more. HoMM3's longer spells come
    from Spell Power, which already lengthens spells (ADR-072).
  - **Cornucopia**: Everflowing Crystal Cloak, Eversmoking Ring of Sulfur, Everpouring Vial of Mercury (new, +1 a day each); +2 of each more.
  - **Angelic Alliance**: Sword of Judgement, Lion's Shield of Courage, Celestial Necklace of Bliss (new); +2 to every primary stat and no
    morale loss for mixed factions (a new effect kind, `no-faction-penalty`; HoMM3's Prayer at the battle start is left out). Three pieces,
    not HoMM3's six.
  - **Regalia of the Undead King** and **Bow of the Sharpshooter**: no longer sets but assembled relics of the same pieces, the set bonus
    becoming the relic's added effect (+10 Necromancy points; +15 % ranged damage, +1 battle speed).
- **Counts** (with ADR-106's four orbs): **109 artifacts**: 29 Minor, 53 Major (the pieces included), 18 Relics, 9 assembled relics; **5
  sets**.

### ADR-109: Sieges built (ADR-099, ADR-106, 2026-10-30)
**Status**: accepted (2026-10-30), built. ADR-106's siege rules in core, the battle AI and the adventure AI's estimate, with the 2D board and the
3D battle scene drawing them (the 3D part is not run in Godot yet). Points marked *(assumed)* are the agent's choices where ADR-106 says
nothing; numbers stay placeholders.

- **When** (as ADR-106): a hero attacking a town's garrison, or an enemy hero standing at the entrance of its own town, fights a siege when
  the town has a Fort (`SiegeRules.SpecOf`); the field is 36 wide, with no width choice; deployment works as before. Map garrisons are not
  besieged. New towns start with a Fort, so **every town battle is a siege now**: saves and replays of older games that fought one replay
  differently.
- **Layout** *(assumed)*: the wall line is column 24 of 36 (24 columns for the attackers, 11 behind the walls, room for two columns of 4 × 4
  defenders). Four sections of 5 rows and the gate of 4 rows in the middle (a 4 × 4 creature passes). The moat is the 2 columns in front of
  the walls, bridged in the gate's rows. The **keep and towers stand off the grid**: the keep behind the defenders' edge, the towers above and
  below the wall line; they block nothing, and only the siege engine reaches them. Random obstacles stay in front of the moat.
- **Walls and gate**: standing sections block both sides, the gate only the attackers; flyers and teleporters land beyond them. An attacker's
  shot whose line crosses a standing section or the gate takes the wall's 1/2 once; the defenders shoot out freely *(assumed)*.
- **The moat**: only the attackers stop in it and take its damage *(assumed)*. A stack stops as soon as its footprint touches the moat
  (walking; a flyer landing in it is hurt too), and moves on freely from there; leaving and coming back is a new entry.
- **Keep and towers**: they shoot **when each round starts** *(assumed)*, a flat 10 + 2 per building (the keep double), at the attacking
  creature stack worth most to hit. **Buildings** count every built level of every building and dwelling *(assumed)*.
- **The siege engine**: the attacker's offensive machine always joins a siege, whatever its Ballistics. Without Ballistics it fires by
  itself, even for a person (walls, the gate after all four, the keep, the towers); from Basic the player aims it
  (`BattleShootStructureCommand`, a click on a structure); troops only from Advanced, which also adds the second shot after a kill. Events:
  `SiegeStructureShot`, `BattleUnitSiegeDamaged`; `BattleStarted` carries the `SiegeLayout`.
- **AI**: the estimate fights (`BattleEstimator`) are sieges for fortified towns and heroes in their own towns, with the hero's own machines.
  In battle the defenders hold the walls (they fight what they can reach but do not advance out); walking attackers take the way round the walls to a
  breach or the broken gate, and while the walls hold close up to the moat without entering it; the AI's engine always fires at the walls
  first.
- **Not built**: one defending shooting stack in the keep (ADR-106 g); Earthquake (with magic); a Godot run and a smoke scenario for the
  siege scene; the town's own look on the siege field (built, ADR-144).

### ADR-110: Magic, first slice: spell table, guilds, spell books, casting in battle (ADR-047, ADR-071, ADR-072, ADR-106, 2026-10-30)
**Status**: accepted (2026-10-30), built. The first Phase 5 magic slice: rank I only, numbers placeholders. Points marked *(assumed)* are the
agent's choices where the earlier ADRs say nothing.

- **Spell table**: `data/spells.csv` (embedded, `SpellTable`, `SpellCatalog`, `GameState.Spells`) holds all 102 spells of the re-draft: id,
  name, school (the town id), level, mana, element, tags, whether it is an adventure spell, its rank I battle effect and its notes. The
  **battle effect** is a small grammar (`SpellEffect.Parse`): `damage N`, `heal N`, `buff …` and `curse …` with the stats they change
  (`attack`, `defense`, `speed`, `speed%`, `initiative%`, `melee%`, `ranged%`, `taken%`, `melee-taken%`, `ranged-taken%`, `spell-taken%`,
  `morale`, `luck`, `roll max|min`), `rounds N|battle`, the reach (`area N`, `enemies` / `allies`, `all`) and a filter (`living`, `undead`,
  `undead-demon`, `shooter`). 37 spells have one; the rest (summons, control, resurrection, adventure spells, ranks II / III) wait. The table
  is generated from `spells.md` for now.
- **Spell books**: a Mage of a magic town starts with a book holding its specialty spell; warriors and Primal Fury heroes have none. A book
  costs **500 gold** in a mage guild (`BuySpellBookCommand`, the town's visiting hero). Books and known spells are not part of the map format
  yet: roster heroes get theirs from the class, the rest come from play.
- **Guilds** (ADR-106): when the game starts, every town by id draws 3 / 3 / 2 / 2 of its school's spells per level, without repeats, from
  `GameState.Random` (`MagicRules.DrawGuilds`), whether the guild is built or not; Stronghold and Swamp Bestiary towns draw none. A guild of
  level L teaches levels 1 to L. The map cannot fix guild spells yet.
- **Learning and mana**: an own hero on a town's entrance with a mage guild learns the guild's spells its school's **Magic** skill allows
  (Basic level 1 … Master level 4): on entering or capturing the town, at the start of the owner's turn, when a guild is built or a book
  bought while it stands there, and when it is hired there *(assumed: all of these)*. Its mana fills to the maximum only after **a night in
  the town**: at the start of the owner's turn with the hero still on the entrance, not on entering (the user's rule).
- **Casting** (ADR-071): once a round per side, during the turn of one of its own units, which keeps its action (`BattleCastCommand`; the AI
  too). The battle copies the hero's book, Spell Power, level and mana (`BattleCaster`); mana spent is written back when the battle ends
  (`HeroManaSpent`). Stack spells aim at an enemy stack (damage, curses) or an own one (healing, buffs) they work on; area spells at a cell
  (a square of N cells centred there, hitting both sides); side spells at the enemy or own side; field spells at every stack.
- **Numbers** (ADR-072, MAG-6): damage and healing are base × (10 + Spell Power) / 10; a spell specialty adds 3 % a hero level to the amount
  (not the duration) *(assumed)*; holy spells do double damage to undead and demons. Durations are rounds × (10 + Spell Power) / 10, at least 1,
  and count **after the round of the cast** *(assumed, like warcries)*: the effect lasts the rest of this round and that many more. The enemy
  hero's **Spell resistance** (8 / 14 / 20 %) cuts hostile damage and duration (rounded up, at least 1); **Interference** takes its Spell
  Power off and adds its mana to the cost. A spell cast again on a stack replaces its earlier effect; different spells add up.
- **What stops a spell**: creature immunities to the spell's tags; creature spell resistance against hostile spells; holy creatures ignore
  holy spells; war machines take only damage spells without a filter *(assumed)*; the filters. Such stacks are refused as targets, and
  reported (`BattleSpellResisted`) when an area or side spell reaches them.
- **Effects in battle**: Attack and Defense add to the damage formula's; melee, ranged and damage-taken percents multiply the damage; speed is
  flat then percent (at least 1); initiative percent joins the warcries'; morale and luck add; Bless rolls the maximum and Curse the
  minimum (no random draw). Effects end as a round starts (`BattleSpellExpired`).
- **AI**: the battle AI casts the damage spell and aim worth most (the creature value of the hit points taken off enemy stacks, less its own),
  if any is worth something; it does not buff yet. The adventure AI buys a book for a visiting hero with a Magic skill when it has gold to spare.
- **Front-ends**: the battle HUD shows the acting hero's mana and one button per battle spell; a stack or area spell then waits for a click on
  the field (Esc cancels). The 2D harness arms spells with C. The hero panel lists the book's spells.
- **Not built**: ranks II / III and Mastery; adventure spells; summons, control, resurrection and the other effects without a battle line;
  creature casters; shrines; the extra-school spell buildings and the Primal Fury building; guild spells set by the map; spell books and
  spells in the map format; the AI's buffs; the orbs' element power (built since, ADR-111).

### ADR-111: Artifacts, the re-draft: the table and the new effect kinds (ADR-061, ADR-099, ADR-100, ADR-106, ADR-108, 2026-10-30)
**Status**: accepted (2026-10-30), built. The roadmap item "Artifact table: the re-draft": `data/artifacts.csv` now holds the catalog of
[artifacts.md](../content/artifacts.md), so the table is the truth for numbers again. Points marked *(assumed)* are the agent's choices.

- **Table**: 101 artifacts and 5 sets. Ids renamed: `ash-sigil` → `badge-of-courage`, `greater-gnolls-flail` → `lizard-warriors-flail`,
  `buckler-of-the-gnoll-king` → `buckler-of-the-lizard-king`, `scale-of-the-elder-dragon` → `dragon-scale-armor` (no shipped map used them;
  older saves and maps naming them fail to load them like any unknown artifact). New rows: every ✅ / 🆕 / ⏳ row of the draft, the
  assembled relics' pieces with their own effects (the relics themselves are the next roadmap item), the four orbs, the Spyglass and
  Speculum, the Seed of the World Tree, the Unwritten Codex and the Recanter's Cloak. Changed numbers: Dawn Banner +1 luck, Ember Crown +6 Spell
  Power, Bone Dragon's Fang +2 luck, Dragon Scale Armor +2 Attack, Mirror +3 Defense / +2 luck, Aegis −5 % damage taken, Amulet of the Deep
  +10 maximum mana; the dropped "Not yet" notes are gone. Sets: the Armor of the Dawn Covenant adds +2 Spell Power, "Ashen Court Regalia" is
  renamed (id kept), the Unbound's Trophies lose their terrain condition, and the **Regalia of the Firstborn** is new (+3 Attack, +3 Defense,
  +2 Spell Power, immune to `mind`).
- **New effect words** (`ArtifactEffect.Parse`, summed in `ArtifactBonuses`): `sight N` (scouting radius), `mana-a-day N`, `mana-max N`,
  `creature-health N` (percent), `no-retreat`, `necromancy N` (percent points), `growth N` (percent), `immune <tag>`, `element-power
  <air|earth|fire|water> N` (percent), `spells L1-L2`, `no-spells-above L`.
- **Rules**:
  - Mana: added to `ManaRules.MaxMana` / `DailyMana`. Sight: added to `Hero.ScoutingRadius` (seen on the next reveal, *(assumed)* not at once).
  - Creature health: the hero's creatures (not war machines) fight with health × (100 + N) / 100, rounded down, for the battle; Necromancy
    counts the slain with the table's health.
  - Growth: the sum over the owner's heroes on the map adds to the Monday growth of every town of the owner, like the fort bonus *(assumed:
    not to a new dwelling's first week)*.
  - Necromancy points add to the share only for a hero with the skill *(assumed)*.
  - Immunities: the enemy's hostile spells with the tag do not reach the army (own buffs with the tag still do: Steadfast is `mind`).
  - No retreat: either hero's artifact forbids retreat and surrender to both sides (`SurrenderRules.CheckRetreat`); the AI does not try.
  - No spells above L: neither side casts a spell above the lower cap of the two heroes (`BattleMagic.SpellLevelCap`).
  - Element power: multiplies the amount of the hero's damage and healing spells of that element (Magic Arrow's "all" gets none
    *(assumed)*).
  - Spells known while worn: with a spell book, every spell of those levels of every school is castable at rank 1 (`MagicRules.Known`);
    nothing is learned for good. The hero panel counts them.
- **AI**: the new kinds have weights in `ArtifactValue` (placeholders).
- **Not built**: the Crown of the Waning Eye's +1 to the best magic skill; the assembled relics (joining, blocked slots, `full-mana`,
  `no-faction-penalty`); creature casters' spells against artifact immunities (creature casters are not built).

### ADR-112: Growth buildings, the war lodge, banks per faction, garrison heroes, template connection options (ADR-064, ADR-068, ADR-070, ADR-071, ADR-106, 2026-11-02)
**Status**: accepted (2026-11-02), built. Closes the roadmap items on growth buildings, the Stronghold and Swamp Bestiary third building, per-town
building names and costs, the bank re-draft (MAP-22), garrison heroes (with the hero limit and towns keeping war machines) and the template
follow-ups (RMG-11, RMG-13, MAP-20). Points marked *(assumed)* are the agent's choices; all numbers are placeholders.

- **Buildings** (`data/buildings.csv`):
  - A new `growth` column (`dwelling-T N`) and the buildings `growth-1` (+5 a week to tier 1; 1000 gold + 5 lumber; needs a tier-1 dwelling)
    and `growth-2` (+2 to tier 2; 1500 gold + 5 ore + 2 of the town's rare resource; needs a tier-2 dwelling) in every town, each with its own
    name (Haven: Drill Yard, Cloister of Vigil; Stronghold: Warren, Mess Hall; …). TOWN-1: every dwelling of the tier gets the bonus.
  - The bonus is flat and added after the fort's percent and the artifacts' growth (not raised by them) *(assumed)*; a dwelling built this week
    gets it in its opening week too *(assumed)*.
  - A town's row at **level 0** removes a shared building from that town. Stronghold and Swamp Bestiary have no mage guild: their third building
    is the **war lodge** (`war-lodge`, three levels: War Lodge / Hall of Warcries / Drums of the Horde; Totem Circle / Spirit Totems / Elder Totem;
    2000 gold + 5 lumber + 5 ore, 3500 + 5 + 3 crystal or sulfur, 5000 + 5 + 5). Their town hall 2 needs war lodge 1 instead of mage guild 1.
  - **Warcries** (Primal Fury): a hero knows a warcry only once learned at a war lodge of at least its level (Rally 1, Intimidate 2, Terror 3),
    up to its Primal Fury level: `Hero.WarcriesLearned`, raised by a visit or by building the lodge with the hero in town (`WarcriesLearned`
    event). Starting heroes know none *(assumed, like spells)*.
  - Start buildings a town type lacks are dropped and levels capped at its maximum (scenario assembly).
- **Banks (MAP-22)**: every bank has four rows (weights 30/30/30/10) of two or three creature types of one faction (neutral creatures count as a
  faction for the neutral banks) and pays about 25–60 % of its guard's gold worth; Dragon City is worth clearly most. Some banks upgrade a fifth of
  their first stack only *(assumed)*. The tables are in `CreatureBank.cs` and [adventure-map.md](../design/adventure-map.md).
- **Garrison heroes** (`Town.GarrisonHero`, `EnterGarrisonCommand` / `LeaveGarrisonCommand`, `HeroEnteredGarrison` / `HeroLeftGarrison`):
  - The visiting hero enters with its army merged into the garrison (refused when it does not fit); it leaves the map (`GameState.Heroes` and
    `HeroesOf` are map heroes, `AllHeroesOf` and `GetHero` include garrison heroes). Leaving takes the **whole garrison** out onto the entrance
    (HoMM3), refused with the entrance taken, an empty garrison or the player at its hero limit: a garrison hero does not count (**TOWN-8**).
  - It earns income, regains mana and movement, levels up and governs like the others; its war machines stay with it, so **a town keeps war
    machines** through its garrison hero.
  - A siege of its town is fought by the garrison under the hero (its skills, artifacts, spells and machines). It **cannot retreat or surrender**
    *(assumed)*. Winning keeps the survivors (and raised undead) in the garrison; losing removes the hero and the town falls. A garrison hero with
    no troops is beaten outright: its artifacts go to the attacker.
  - Not built *(assumed out of scope)*: map files placing garrison heroes; the AI using garrisons (its attack estimate ignores the hero);
    movement commands for a garrison hero ("Unknown hero").
- **Template connections** (format **v7**, older files load with the defaults):
  - `kind: Portal`: a pair of linked portals anywhere in the two zones, on any levels; the layout does not make the zones touch. Guarded on both
    ends like a gate.
  - `guard` per connection, `connectionGuard` per template (default `Monsters`): `Garrison` (a neutral garrison in the way with troops of the
    guard value; where no tile of the way closes it, a monster guard instead), `BorderGuard` or `BorderGate` of `colour` (drawn from the seed when
    omitted) on the passage, with the **Keymaster's Tent** of that colour in the connection's first zone (`from`), guarded by the guard value.
    One tent per colour per map. Only normal connections take these; wide, gate and portal connections ignore them (a validator warning).
  - `chance` 1–100 (RMG-13, default 100): rolled from the seed (`presence` stream); a dropped connection that the zones need to stay connected
    is made anyway, in template order (`ConnectionPlacer.Present`). The layout still treats dropped connections as edges *(assumed)*.
  - The generator gives the same maps as before for templates that use none of this, so `MapGenerator.Version` stays 7.

### ADR-113: The Godot check of what was built but never run: sieges, garrison heroes, dwellings and caravans, generated connections (ADR-085, ADR-109, ADR-112, 2026-11-02)
Four new smoke scenarios in `tools/smoke-godot.ps1` (run in Godot 4.7.2; frames under `.shots/`), their maps checked by `SmokeMapTests`:
`game-siege` (`tools/smoke/siege.ewmap.json`), `game-garrison-hero` (the towns map), `game-dwelling-caravan` (`tools/smoke/caravan.ewmap.json`)
and `editor-generated-connections` (a map generated from `tools/smoke/connections.ewtemplate.json` by the new `sim generate`).

- **Found and fixed**:
  - The battle view queued later steps onto a tween that had already started (Godot refuses: "Can't append to a Tween that has started"), so
    quick combat pressed during an animation, or any event a frame after the queue began, lost its end state. Steps now wait for the next tween
    when the current one has played a frame, and quick combat applies everything queued at once (`BattleView3D.Step`).
  - Keymasters' tents, border guards and gates (and the Magi's Eyes and Windows) were all drawn in one yellow; they now take their object colour
    (`Palette.ForColour`).
  - The caravan queue named the source "Creature dwelling"; it now says "Footman dwelling at 12,1" like the hire rows.
- **Tooling**: `sim generate <template> --out <file>` saves a generated map with human players; the editor opens `--map=<file>`; `AutoInput`
  takes `hold:Key,frames` (held keys, for camera panning) and `wheel:up|down,count`.
- **Seen working**: the siege field (wall sections, gate, moat, towers and keep off the field, rubble after a hit), the ballista aiming at a wall,
  quick combat and the result, the captured town; entering and leaving the garrison from the town screen; the dwelling panel, the caravan
  section (hire, queue, arrival in the garrison a day later), the mage guild's spells and the tavern on the town screen; garrisons, border posts,
  keymasters and portals on a generated map. Still not seen: the town's own look on the siege field (there is none yet), the spell book button.
- Walls between generated zones are flat rock terrain and hard to tell from the ground; left for the terrain look-dev (Phase 5).

### ADR-114: Phase 3 close-out: spell buildings, the town screen's tree, the tavern screen, specialties, inspector rows, Ember March (ADR-072, ADR-083, ADR-093, ADR-107, 2026-11-02)
**Status**: implemented (2026-11-02). Numbers marked *(proposal)* are the agent's, for the owner to confirm or change.

- **Spell buildings** (ADR-072, MAG-12): every ring town has two `school-<town>` rows in `buildings.csv`, one per ring neighbour's school
  (Sylvan: Inferno, Haven; …), 2000 gold + 5 of the school's resource, needing mage guild 2. *(Choice)* "The school's resource" is the rare
  resource of the school's town (Light crystal, Shadow sulfur, Nature, Arcana, Death and Chaos mercury). Each adds one spell of its school per
  spell level; a guild of level L teaches the first L (`MagicRules.Offered(state, town)`). *(Choice)* The spells are picked from a shuffle
  seeded by the game seed, the town and the school (`MagicRules.SchoolSpells`, like the tavern pool), so existing games' random numbers and
  replays stay the same and nothing is stored. Building one teaches the visiting hero at once. Stronghold and Swamp Bestiary have none.
- **Skill specialties** (ADR-083's open ones; *(proposal)* numbers): **Tactics** +1 deployment column, +2 from hero level 10 (only with the
  skill); **Blood Rage** the Attack cap +5 % a level (Master 8 → 12 at level 10; `HeroBattleBonuses.BloodRagePercent`); **Warcry** the
  warcries' strength +5 % a level (Intimidate's initiative cut and Terror's chance; Terror never above 60 %; `WarcryPercent`); **Armor**, **War
  Tempo**, **Treasury** (gold, not the interest) and **Scouting** +5 % a level, rounded down, like Melee and Archery.
- **Flat specialties** (ADR-093, ADR-107): `skill <id> <amount>` in `heroes.csv`, only for Logistics (+N % movement even without the skill,
  no levels) and Death attunement (+N Necromancy points with the skill, `NecromancyRules.HeroBonus`; its mana is not scaled).
- **The roster draw** (ADR-107 d, once, seed 107, a scratch script): the 48 resource and 8 Logistics specialists were redrawn into 4 gold
  (four towns), 6 rare (2 each, six towns), 2 Necromancy (Sepulka, the first Necropolis mage with Death attunement, and Corvane, a Necropolis
  warrior) and 2 flat Logistics; the other 42 took their first starting skill with an effect *(choice: a school's Magic skill and Logistics
  are skipped, so per-level Logistics specialists do not come back)*. Names in [hero-roster.md](../content/hero-roster.md).
  `HeroSpecialty.Describe` says what a specialty does (hero and tavern screens).
- **Town screen**: `BuildingTree.For` (core) gives each building its level, next level, cost after Stewardship, requirements met or not, a
  status (available, complete, built today, needs buildings, cannot afford, refused) and a depth (the longest requirement chain under its
  first level); the town screen draws one column per depth with a tooltip (cost, ✓ / ✗ requirements, income, growth, spells, the refusal) and
  a "Built today: …" line (`Town.LastBuiltName`, kept in memory only). `BuildingTree.DwellingChoices` names, for each unbuilt dwelling, the
  lines building it would close and, when the 7-of-9 rule refuses it, the tiers it would leave out; the dwelling rows warn with "⚠ closes …".
- **Tavern screen** (`TavernPanel`): a card per offer with class, faction (native), specialty and what it does, skills, stats, preferred
  creature, the starter stack or a returning hero's level and army, and the hire button with its refusal; the Thieves' Guild from there.
  Opened from the town screen's "Visit the tavern…" (the game app); the 2D harness keeps the hire buttons only.
- **Editor**: `SetSphinxRiddle` (trimmed texts, three different answers, null for a drawn one) and `SetWitchHutSkills` (none or two
  different known skills, old ids resolved), both undoable; inspector rows for them, a pickup's artifact (or random by its tier) and a hero's
  worn and backpack artifacts.
- **Ember March** (`EmberMarchScenario`): the shipped two-player map, 36 × 30, Haven (a person) against Inferno (the computer); a rock ridge
  with a middle pass on the road (3 rows, all in 24 wolves' zone of control) and a northern pass (8 trolls, a Major artifact); per side a gold,
  lumber and ore mine, a guarded rare mine, a tier-2 map dwelling and loot; shared sulfur and crystal mines, a witch hut, a sphinx with the
  author's riddle and two Learning Stones. On the new-game screen after Two Towns; `sim game --map ember`; `sim export <map> --out <file>`
  saves a built-in map as a file.
- **AI fixes found by playing it** (AI against AI): (1) a hero that reached an own town on its way took the garrison only at the next turn's
  start, so the troops called it home every day; it now takes them on arrival; (2) the creatures waiting in a town's dwellings counted as
  troops to collect even when the player could not pay for them; now only what the gold buys counts; (3) a player without heroes hires
  before it builds and saves for a hero instead of building. Results: Two Towns 3 of 3 games to a winner (days 13–37); Ember March 18 of 18
  (days 8–71, 9 wins each); generated `duel` maps still do not finish in 60 days (roadmap).
- **Checked in Godot 4.7.2**: smoke scenarios `game-tavern`, `game-ember-march` and `editor-inspector-objects`; the whole suite passes (24 of
  24). In the tavern scenario the first Escape closes the hover tooltip, as Godot does.
- **Not done**: a person playing Ember March to the end in the app (the Phase 3 exit's last check); the 2D harness has no tavern screen.

### ADR-115: Map events: the EVT-1 … EVT-6 proposals (ADR-096, 2026-11-02)
**Status**: accepted (2026-11-02), the owner's answer to EVT-1 … EVT-6: "use the proposals". Implemented by ADR-124, ADR-127, ADR-128, ADR-129, ADR-131 and ADR-132.

- **Scope and plan** (EVT-1): all four event types in three steps: (1) hero, player and town events with conditions, variables, messages,
  questions and the main actions; (2) the quest objects and the quest log; (3) loops and "run another event". A Phase 5 item, after the
  Phase 3 and 4 exits and before the campaign, which builds on it. Called "map events" with an "Events" tab in the editor. Stored as a data
  tree in the map file (no script language: deterministic, safe to load, easy to show). "Simple event" forms for HoMM3's timed events, event
  cells and town events that write the same tree. Campaign variables are recorded now and built with the campaign (FLOW-1).
- **Triggers** (EVT-2): event cells are invisible and ignored by route planning; the route stops only when the event shows something or
  fights; a cell runs on every step until the event disables or removes itself ("once" is a ready form); who triggers is a condition (the
  ready forms' tick boxes write them); turn-start runs after income, growth and the week's event, neutral towns at the start of the day;
  several events at one moment in the editor's list order; a turn-start message shows when the turn begins and the camera moves to what it
  concerns.
- **Conditions** (EVT-3): HotA's whole list mapped to ours (complexity for difficulty, faction for starting town, four skill levels), plus
  the team, an ally and the day of the week now; map-wide whole-number variables; random numbers from the game's source; "defeated by"
  records only for the monsters and heroes an event names.
- **Actions** (EVT-4): HotA's whole list with our rules (skills to Master into a free slot, our war machines, a building ignores bans and
  adds its prerequisites, spells with Phase 5); losses as HotA (no lowering experience or skills); reveal an area and give a hero now; a
  battle action (won: experience and the event goes on; lost: the hero is lost and the event stops; retreat and surrender stop it); a full army
  chooses what to leave (a pending decision); messages are text with reward lines, images once there are icons.
- **Quests** (EVT-5): Seer's Hut, Quest Guard and Quest Gate (like the border gate, a quest for a key); HoMM3's quest forms as ready forms
  that take what they ask; a quest log per player (Q) in the game state; a hero that meets a gate's quest passes without stopping and routes
  treat the gate as open only for such a hero; no Seer's Huts on random maps for now.
- **AI and editor** (EVT-6): the AI answers questions by value (random numbers as their average), does quests worth their cost, treats Quest
  Guards and Gates as obstacles until it meets them and does not look ahead at event cells or player and town events; the "Events" tab (the
  tree, variables, shared events, undo through `MapEditorSession`), object inspectors pick or create their event; validator checks (unknown
  names, an event that runs itself, unused events and variables, an unfinishable quest where it can tell); events are tested by playing the map.

### ADR-116: New ideas: prologue, health bars, dividing obstacles, scenery, layout, information windows, icon prompts, missing mechanics (2026-11-02)
**Status**: recorded (2026-11-02) from the owner's notes in `ideas.md`; questions CAMP-1, UI-3 and CMB-12 with proposals; the two writing tasks
are done.

- **Prologue / tutorial**: a small first campaign map, Haven against neutral creatures and the undead, placeholder lore (CAMP-1; roadmap
  Phase 6, using the map events of ADR-115).
- **Combat**: a health bar on each stack, green over 50 %, yellow 21–50 %, red 20 % and below (the owner's thresholds; what it measures is
  CMB-12 a); obstacles that split the field partly, like HoMM3's long cliffs (CMB-12 b, c).
- **Random maps**: more scenery (trees, rocks) so maps feel lived in; the scenery objects of ADR-077 exist, the generator places more (no
  question).
- **UI**: a spacing pass (placement, not fonts or theme), a configurable layout (initiative bar, minimap corner, hero and town lists, action
  panel), information windows for skills, spells, artifacts and the rest, and an advanced tooltip mode (UI-3).
- **Icon prompts**: [icon-prompts.md](../content/icon-prompts.md): the format (512 px source, 32 px readable, transparent PNG), a style block,
  lore colours, and prompts for resources, stats, skills, schools, war machines, map and battle actions and buttons.
- **Missing mechanics**: [missing-mechanics.md](../design/missing-mechanics.md) lists what HoMM3 with HotA and HoMM5 have and we do not, with
  where each stands (dropped, planned, partly, missing) and candidates for next.
### ADR-117: Phase 4 close-out: whole-map fairness, teams 2–8, mirrored templates, the eight-player template, the template editor (ADR-064, ADR-070, ADR-071, ADR-073, 2026-11-03)
**Status**: implemented (2026-11-03). Numbers marked *(proposal)* are the agent's, for the owner to confirm or change.

- **Whole-map check** (`MapFairness.Check`, generation stage 8, ADR-071 / RMG-8): after the treasure, every start town must reach every
  other start town, and every town, mine and treasure item must be reachable from some start. The walk uses the step costs (2 straight,
  3 diagonal), skips blocked cells, crosses gates and portals by their target, and stops at entrances that end a route, except guards,
  pickups and garrisons, which it walks through and counts. Each start is measured by three numbers: the **mine distance** (per resource,
  the distance to the nearest mine, summed), the **treasure value** (each item's recorded worth split between the starts by 1 / distance²)
  and the **guard power** (the least guard power on the way to the nearest rival; a Dijkstra on guard, then distance).
- **Teams, not players**, are compared: allies share camps on `2v2`, so a team's numbers are its members' sums (a player without a team is a
  team of one), and only teams whose zones are alike (size, level, towns, mines, piles, treasure bands, monsters) are compared. A team fails
  when a number is more than **±15 %** off its group's average and more than a slack off it *(proposal: 4 tiles, 1500 gold, 300 power, so
  small maps with small numbers are not refused for a step)*. A failure is a generator problem, so `GenerateBest` retries with the next
  sub-seed and reports the last one if every attempt fails. `sim fairness [--template] [--seeds 20] [--size] [--verbose]` prints the
  numbers. `MapGenerator.Version` 8.
- **Teams 2–8** (ADR-070): templates (format v8) and maps (format v28) carry `minTeams` / `maxTeams` (2–8, empty = any). The template
  validator checks 2 ≤ fewest ≤ most ≤ players and the fewest at most `MinPlayers`, and that locked teams fit; `ScenarioAssembler.CheckTeams`
  refuses a setup with another team count; `GameState.SetTeamCounts` before the game starts and `AssignTeams` refuses a change that breaks
  it. The map editor has "Teams from / to" in the map properties (undoable `SetTeamCounts`), the validator an error for locked default teams
  outside the counts (a warning when not locked); the new-game screen shows "— 2 teams" and refuses others. `2v2` is set to exactly 2.
- **Mirrored templates** (ADR-070, ADR-073, RMG-9): `symmetry` None, Vertical, Horizontal, Diagonal, RandomAxis (drawn from the seed),
  HalfTurn or QuarterTurn, and a `mirror` per zone naming its image. Axes and the half turn are for 2 players, the quarter turn for 4 or
  8 (8 = four per level). The validator checks that mirrors exist, are used once, stay on their level, form cycles of 1 or the symmetry's
  order with zones alike, that no start zone is its own image and that every connection's image is a connection like it. The layout
  keeps the zone centres symmetric (only the first zone of each cycle moves), then copies the grid from the first tile of each cycle of
  tiles. Mirrored zones and connections **share their rolls**: every stage seeds a zone by `RollZone` (the cycle's first zone) and walks its
  tiles in the source zone's mapped order (`ZoneLayout.Tiles`), passages are sorted in the first connection's frame, and treasure values
  are drawn before they are placed. The result is the same zone content on every side, though not tile for tile where walls and roads
  meet on the axis.
- **Templates**: `ffa-4` is now mirrored (quarter turn); new `mirror-duel` ("Mirror duel", random axis, 2 teams) and `ffa-8` ("Eight
  kingdoms": 8 players, underground, quarter turn with four starts a level, reserves in a ring, a surface and an underground hub joined by a
  5000 gate; sizes 128, 160, 192) *(proposal: zone sizes and treasure)*.
- **Template editor** (ADR-070, RMG-10): `TemplateEditorSession` in core (blank, load, the template's own settings, add / edit / rename /
  move / remove zones, connect / edit / disconnect, arrange the graph; each change publishes `TemplateChanged` with the validator's live
  issues; **no undo**, as decided). The graph places are stored in the template (`graphX`, `graphY`; old templates get an arranged graph).
  The map editor's "Templates" button opens `TemplateEditorPanel`: the zone graph (drag to move, right-drag to connect, drag the ground to
  pan, wheel to zoom, Delete), an inspector for the template, the zone or the connection, the issues list, New, Open (shipped and user
  templates), Save to `user://templates`, and Generate with a size and seed, which opens the map. The editor's Generate tab lists the user
  templates that validate next to the shipped ones; the game's new-game screen still lists only the shipped ones.
- **Tools**: `sim maps`'s reachability check now walks on from a gate's exit (it stopped at the exit before, which hid nothing on the old
  templates but failed on `ffa-8`); the auto input has `drag` / `rdrag` (`x1,y1,x2,y2`); smoke scenario `editor-template-editor`.
- **Results** (`sim fairness --seeds 6` on each template's sizes): fair at the first attempt in 4–6 of 6 seeds, after retries 6 of 6
  everywhere (duel, 2v2, ffa-4, Mirror duel, Eight kingdoms); `sim maps --days 3`: 15 maps of 5 templates, none failed; 1158 tests pass.
- **Not done**: the new-game screen's user templates; the AI on generated maps (the next step).

### ADR-118: Answers to CAMP-1, UI-3 and CMB-12 (ADR-116, 2026-11-03)
**Status**: accepted (2026-11-03), the owner's answers in `open-questions.md`. Nothing implemented; the roadmap items lose their ❓.

- **CAMP-1, the prologue** ("use the proposals"): (a) the campaign's first mission, playable on its own from the main menu as "Prologue"
  before the campaign screen exists; (b) it teaches through messages from the map events (needs EVT step 1, ADR-115), with a "Hints"
  setting to turn them off; (c) one map of about two weeks; (d) neutral monsters first, then one computer-held Necropolis town with a small
  hero; (e) Haven scouts the border of the Ashen Court, the undead are a Necropolis vanguard (placeholder lore).
- **UI-3, layout and information windows** ("use the proposals"): (a) a "Layout" tab in the game menu (initiative bar top or bottom,
  minimap corner, hero and town lists vertical or horizontal, the action panel's side), kept in the player's settings, not the game;
  (b) the spacing pass comes first: one set of margins and gaps as theme constants, no font or theme change; (c) information windows
  on right-click (a long press on touch later) for creatures, heroes, skills, spells, artifacts, buildings and map objects; the hover
  tooltip stays short; (d) a "Detailed tooltips" setting, off by default, adds the formulas and numbers.
- **CMB-12, health bars and dividing obstacles**: (a) *the owner's choice, not the proposal*: the bar shows the **top creature's hit
  points** (the wounded one at the top of the stack) against its full health, green over 50 %, yellow 21–50 %, red 20 % and below; the
  count beside it shows the losses; (b) long cliffs or walls of 6–12 cells that split the field partly, always with at least two gaps,
  flyers and teleporters pass over, shooters' cover as for crags (ADR-035); (c) only on highlands, rough, rocky and subterranean ground,
  one battle in three there, never in sieges or creature banks.
- **Numbers to tune**: the owner's list stays as placeholders until play-testing (caravan cost, complexity resources, Stewardship and
  governor percentages, Ballistics, zone guards and `GuardPicker`, building costs, growth buildings, war machines, morale and luck per
  point, deposit prices and the marketplace threshold, the AI's retreat threshold).

### ADR-119: The adventure AI on generated maps: exploring, guards, garrison heroes, keys and gates (ADR-059, ADR-071, ADR-112, ADR-114, 2026-11-03)
**Status**: implemented (2026-11-03). Numbers marked *(proposal)* are the agent's tuning, for play-testing.

- **What was wrong** (found with the new `sim game` diagnostics): on generated `duel` maps the heroes fought once in 60 days and stayed at
  level 1–5 with armies of 35 000–420 000 power, because (1) exploring was only the fallback when no target had any value, so a daily buff or
  troops waiting at home kept a hero shuttling forever, and none of the passage guards was ever even seen; (2) the exploring search priced only
  the 8 frontier tiles nearest in a straight line, which were often ground seen over a wall, so a hero said "nothing to explore" with its own
  zone half unexplored.
- **Exploring** (`AdventureAi.Explore`): a flood over open ground (no objects, heroes or guarded tiles) finds the frontier the hero can walk
  to, nearest first; a border post the player has the key for and an own or allied garrison are passes; a gate or portal whose exit is
  unexplored is a frontier tile itself (walking in takes the hero through), one whose exit is explored lets the flood go on there. Exploring
  is now a target like the others, worth **900** *(proposal)* against the targets' `value / (0.5 + days)`; a main hero that knows no enemy town
  adds **10 %** of its army power *(proposal)*, so a strong army goes looking for the enemy.
- **Guards**: a monster stack or a neutral garrison within 3 tiles of unexplored ground is also worth the exploring it opens (+900): passage
  guards stand between zones.
- **Keymasters**: a tent is worth 2500 once a border post of its colour is known (it bars the way), else 600 *(proposal)*; key holders explore
  and route through the posts (the route finder already let them, ADR-071).
- **Garrison heroes** (ADR-112): a **secondary** hero defending its town (an enemy can reach it, the garrison alone cannot hold, the two
  together at least match the danger) goes into the garrison, while the main hero is out on the map: inside it leads every stack behind the
  walls, at the entrance it would fight alone and the garrison after it. The player's only or main hero never goes in, as a garrison hero
  never retreats (a first version let Two Towns end on day 2 when the only hero went in and lost). A garrison hero comes out once no enemy can
  reach the town, and leaves its weakest stack behind.
- **Tools**: `AdventureAi.Explain(hero)` lists a hero's best targets with value, fight check, route cost and danger, then its exploring tile;
  `sim game` prints the fights won / lost per player and kind, what is left on the map, the keys and the garrison hero moves; `EW_SIM_EXPLAIN`
  and `EW_SIM_GUARDS` ([development.md](../technical/development.md)).
- **Results** (`sim game --runs`, the AI on both sides): generated `duel` 8 of 8 games to a winner by day 100 (days 27–63, 3 : 5), before 1 of 6
  by day 60; the connections smoke template (garrison, border gate, border guard) 3 of 4 by day 100 (keys taken, the garrison beaten, the
  border guard passed); Mirror duel 3 of 4 by day 100; `2v2` 2 of 3 and `ffa-4` 1 of 3 by day 120 (the larger maps take longer); Ember March 8 of 8 (days 6–41, 4 : 4; before days 8–71); Two Towns ends on days 4–5 (before
  13–37): its towns are 23 tiles apart and an exploring hero finds the other town on day 3 (a fair rush, not changed).
- **Fix**: the AI recruited into a garrison whose seven slots a garrison hero's stacks had filled (refused commands on `2v2`); it now skips
  a line with no room anywhere. `AdventureAi.Refusals` keeps the refused commands with their reasons, and `sim game` prints them.

### ADR-120: Phase 5 magic and sieges: summons, creature casters, adventure spells, shrines, spell books in the map format; the breakable gate, the keep's shooter, Earthquake (ADR-071, ADR-072, ADR-073, ADR-106, ADR-109, ADR-110, 2026-11-03)
**Status**: implemented (2026-11-03); the owner's gate note (ideas.md) is built as written, the rest are the agent's proposals *(proposal)*,
for play-testing. The 3D views are written but not run in Godot yet; the 2D harness has no creature casts.

- **The gate** (owner's note): a hero can arrive without a war machine (never bought, or destroyed earlier), so the attackers' **troops
  break the gate too**: the gate has **100** hit points, **150** with upgraded fortifications (*Citadel and Castle are "upgraded"*, assumed),
  and **10 Defense**. A stack's blow at the gate is rolled like an attack on a creature with Defense 10 (+5 % per Attack point above, −2.5 % per
  point below, a melee penalty halves it) and **capped at 50**, so a large stack breaks it in 2–3 blows. **War machine rules do not change**: a
  machine's hit takes the whole gate. Only the attackers' troops strike it, and only the gate (`Siege.StructureAttackRefusal`); a stack
  next to the gate attacks it like an enemy (`BattleAttackStructureCommand`, pointing at the gate in both views). Event
  `SiegeStructureDamaged`. The battle AI breaks the gate when no wall is open and its machine is gone (`BattleAi.BreakGate`).
- **One shooter in the keep** (ADR-106 g) *(proposal)*: from the Citadel up, the defenders' strongest shooting stack starts behind the
  walls next to the keep, **in the keep**: it never moves, cannot be attacked in melee, and enemy shots at it take the wall cover. When the
  keep falls the stack leaves it (`BattleUnitLeftKeep`) and fights normally. Deployment cannot move it.
- **Earthquake** (rank I) *(proposal)*: 40 damage to every stack, −20 % initiative for 2 rounds, and **1 hit point off each of 2 random standing
  wall sections** (`walls 2` in the effect grammar). Outside sieges only the damage and the slow work.
- **Summons** (`summon id|id N [rooted]`): Summon Pack (150 Pegasi worth), Awaken Grove (400, rooted Treants), Bind Elemental (200; the
  first element not yet summoned in this battle *(proposal)*), Hellgate (80 Imps), Summon Demons (250 Horned Demons). The number is
  the health called (count = amount / the creature's health) and grows with Spell Power like damage; the stack appears at the caster's edge
  nearest the middle row, a second cast adds to it, it vanishes after the battle, gives no experience, counts for no casualties, Necromancy or the guard count. A rooted summon
  never moves. Event `BattleUnitSummoned`.
- **Creature casters** (`Caster <spell> <n>` in `creatures.csv`): **n is casts per battle**; a caster stack's **Spell Power is its count
  / 2** *(proposal)*, rank I effects, no mana. Blind, Confusion and Resurrection are not castable by creatures yet (their effect
  kinds are missing). The Phoenix's **rebirth**: a destroyed stack comes back once with **30 %** of its creatures *(proposal)*
  (`BattleUnitReborn`). Command `BattleCreatureCastCommand`; the battle AI weighs a cast (damage, summons, healing) against the stack's attack; the 3D battle
  HUD has a cast button for the caster stack.
- **Adventure spells** (ADR-073, MAG-5): cast from the book on the map for the mana and a fixed **500 movement points** (fewer left: no
  cast). Built: **Town Portal** (the nearest own town with a free entrance), **Dimension Door** (up to 6 tiles in a straight line to an
  explored, free, passable, unguarded tile, once a day), **View Terrain** (reveals radius 12), **Pathfinder's Call** and **Ashwalk** (three
  terrains native for the rest of the day). The spells that only show something (Distant Vision, Detect Magic, Grave Whisper, Ledger of
  Ages, Veil, Eavesdrop) wait for their screens. Command `CastAdventureSpellCommand`, events `AdventureSpellCast`, `AreaRevealed`; the hero
  panel lists them, Dimension Door arms a click on the map. The adventure AI uses Town Portal to come home to a threatened town.
- **Shrines** (ADR-068): three objects, `shrine.1` … `shrine.3`, each teaching **one spell of its level** (any school), set in the editor or
  drawn at game start; a visiting hero with a spell book and the school's Magic at the spell's level learns it, any number of visits.
  Generator values **500 / 1500 / 2500** *(proposal)*; the AI values a learnable shrine at 400 × its level.
- **Spell books in the map format** (**v29**): a map hero has `spellBook` and `spells` (absent in older maps: the hero's class decides, as
  before); a shrine has its `spell`. The editor sets them (`SetHeroSpells`, `SetShrineSpell`, undoable; inspector rows); the validator
  refuses a hero with spells and no book.
### ADR-121: Assembled relics, the full-mana and no-faction-penalty effects, war machine tuning (ADR-056, ADR-061, ADR-068, ADR-100, ADR-108, 2026-11-03)
**Status**: implemented (2026-11-03), ADR-108's rules as written; *(proposal)* marks the agent's choices.

- **Treasure-chest artifacts** (roadmap, ADR-039, CONT-3) were already built in ADR-068 (5 % of chests hold a Minor artifact, no choice) and
  tested; the roadmap box was only left unticked.
- **The nine relics** are rows of `artifacts.csv` with the new `pieces` column (tier Relic): Wizard's Well, Statue of Legion, Shackles of
  War, Elixir of Life, Ring of the Magi, Cornucopia, Angelic Alliance, Regalia of the Undead King, Bow of the Sharpshooter. A relic's
  `effects` are what it **adds**; worn, it has its pieces' effects too (`ArtifactCatalog.EffectsOf`). It sits in its **first piece's slot**
  (the column order chooses it *(proposal)*: Misc for the well, statue, shackles, elixir and cornucopia, Ring for the Ring of the Magi, Weapon
  for the alliance and the bow, Body for the regalia) and **blocks the other pieces' slots**. The catalog refuses a relic in the wrong slot,
  with fewer than two pieces, a repeated or unknown piece, or a piece of two relics. Relics never drop (`OfTier` leaves them out), so map
  pickups, chests, banks, merchants and the generator give only pieces.
- **Joining**: equipping the last piece publishes `RelicReady` and the hero screen says so and shows an **Assemble** button
  (`AssembleRelicCommand`) *(the "ask" is this notice and button, not a blocking decision: a decision would break an open exchange)*.
  **Taking apart** any time (`TakeRelicApartCommand`): a worn relic into its pieces in their slots, a carried one into the backpack (needs
  the room). A carried relic is worn again only when the slots it blocks are free (the slot itself swaps as usual).
- **Loot**: a defeated hero's relic goes to the winner **whole when the winner can wear it** (its slot and the blocked ones free), else as
  its pieces into the backpack. The **Sacrificial Altar** refuses a whole relic (take it apart first); the **Artifact Merchant** sells pieces
  only (its stock is drawn by tier).
- **Effects**: `full-mana` (the hero's mana is full at the start of each of its turns) and `no-faction-penalty` (no mixed-factions morale
  for the hero's army, garrison included). Values for the AI: 40 and 15 *(proposal)*; a relic is worth its pieces plus its own effects.
- **AI**: always assembles what it can after putting artifacts on; a piece found is worth **50 % more for each other piece of its relic
  the hero has** *(proposal)*; it does not swap out a worn piece for an ordinary artifact.
- **War machine tuning** *(proposal)*: the four offensive machines now deal about the same expected damage per initiative (average damage ×
  shots × initiative / 10, `WarMachines.Throughput`): ballista 47.5, cannon 45.5, catapult 46; the glaivethrower was 72 and is now **20–26
  damage, initiative 11** (50.6), still the most, with the least health (200). Tent, cart, prices and the Ballistics table stay; machines
  already scale with the hero's Attack through the damage formula. A test keeps the four within 15 %.

### ADR-122: Spell ranks II and III and the school Mastery skill (ADR-047, ADR-072, ADR-099, ADR-110, MAG-3, 2026-11-03)
**Status**: implemented (2026-11-03). The roadmap items "School skills take effect" and the ranks part of "Spell table follow-ups";
*(proposal)* marks the agent's choices.

- **School skills**: Magic (spell levels, ADR-071) and attunement (ADR-063) were already in; **Mastery** is new. `MagicRules.MaxRank`: rank 1
  without it, **2 at Basic, 3 at Advanced** (MAG-3). When a hero with a book is in an own town with a mage guild (on entering, at the start of
  its turn there, after a building or book is bought), every spell the guild or a spell building **offers** that the hero knows rises,
  free, **straight to the rank its Mastery allows** (MAG-3's assumption); spells learned in that visit rise in the same visit. A spell learned
  elsewhere (a Shrine, the map) rises when the hero visits a guild that offers it. `SpellsRaised` (spell → new rank) after `SpellsLearned`.
  Shrines teach at rank I; the Unwritten Codex's spells are rank I *(proposal)*.
- **Table**: `spells.csv` gets `battle2` and `battle3` after `battle`, each the **whole** effect at that rank in the same grammar (not a
  difference); empty means the rank below's (`SpellDefinition.EffectAt`). A rank needs the rank I effect and keeps its kind (the parser refuses
  otherwise). 42 of the 43 battle spells have a rank II or III effect (not Smite).
- **New words**: `targets N` (a stack spell also reaches the **N − 1 stacks nearest the aimed one** on its side that it works on, nearer
  first by cell distance, then by id *(proposal: no extra clicks, the aimed stack chooses the group)*); `spare-own` (an area or field spell
  leaves the caster's stacks alone); filters `melee` (no shots) and `ground` (not flying; war machines count as on the ground).
- **Battle**: `KnownSpell.Effect` is the effect at its rank, used for refusals, reach, damage, durations, summons, the AI's values and the
  HUD; rank III already counted one level higher against creature spell resistance (ADR-049). The battle HUD and hero screen show "Bless II".
- **Rank changes not built** (the rank keeps the one below): Smite's splash and its ignoring spell resistance, Holy Word's lost action, Heal's
  cure and revival, Call Lightning's jump, Barkskin's thorns, Bind Elemental's two kinds, Firebolt's and Dark Bolt's burning, Meteor Shower's
  rubble, Summon Demons staying, Ashfall sparing own demons, Frost Bolt's freezing; the Familiars' mana theft (Hellgate III calls them). The
  adventure spells' ranks (radius, Dimension Door's reach and casts, Town Portal's choice, every-terrain walking) are not built either.
- **Not changed**: the map format (map heroes' spells start at rank I; no format bump); the adventure AI does not plan guild visits for ranks
  (it gets them when it passes through); mana costs are the same at every rank (spells.md). Old replays whose heroes had Mastery and visited
  guilds may now differ.

### ADR-123: The rest of Phase 5 magic: control, revival and most battle spells, the AI's buffs, elements, guild spells fixed by the map (v30), the show-only adventure spells and the adventure spells' ranks (ADR-099, ADR-106, ADR-110, ADR-120, ADR-122, 2026-11-04)
**Status**: implemented (2026-11-04); *(proposal)* marks the agent's choices, for play-testing. The 3D battle lines, the HUD, the map editor's
guild-spell rows and the adventure-spell screens are written but not run in Godot yet.

- **Effect grammar** (`SpellEffect`): new kinds `revive N [living|undead] [lasting] [fallen]`, `dispel own|enemy|any` and `slay N` (percent of the
  creatures, at least 1). Statuses on buffs and curses: `blind`, `confused`, `entangled`, `no-shoot`, `no-attack`, `no-retaliation`, `fearless`,
  `vigil`, `true-shot`, `sanctuary`, `stasis`, `unseen`, `phase`, `ward-mind`; numbers `burn`, `regen` (each round as it starts, scaled by
  Spell Power when cast), `decay%`, `immune-level` (every spell up to the level, the own too), `shroud` (the enemy's), `thorns%`, `lifesteal%`.
  Damage words `splash`, `chain`, `pierce`, `stun%`, `execute`, `debt%`, `ring`, `column`. `rounds 0` is the rest of this round.
- **Blind** (rounds 2 / 3 / 3 and 2 stacks): the stack's turns pass (`BattleUnitSpellbound`); any hit ends it, after the blow, so it still
  retaliates **at half** (ranged retaliation: none). **Confusion** (1 / 2 / 2 rounds and 2 stacks): the stack's turn is played by itself, for any
  side, at random from the battle's generator: one of the enemies it can shoot or reach, or a move to a placement it can reach *(proposal:
  enemies only; Frenzy is the friend-or-foe spell)*; its hero does not cast in that turn. **Resurrection** (150, living): the top creature first,
  then the dead up to the stack's starting count; rank I's revived are **lost again after the battle** and die first when the stack loses
  creatures (`BattleUnit.Revived`, `KeptCount` for survivors, casualties, experience and Necromancy); rank II they stay; rank III also brings
  back an own stack destroyed in the battle when its place is free (it acts from the next round; the HUD aims at the fallen stack's cell).
  Creature casters (the Shadow Mistress' and Shadow Matriarch's Blind, the Djinn Sultan's Confusion, the Seraph's Resurrection) cast them at rank I.
- **Other battle spells now built**, with their ranks from spells.md: Purge, Steadfast, Vigil, Sanctuary, Entangle, Hunter's Sight, Regrowth,
  Fleetfoot, Chain Lightning (70, then 35 and 17 to the nearest stacks, own too; III enemies only), Oakheart, Frost Ring, Unravel, Anti-Magic,
  Dispel, Stasis (no turns; cannot be attacked or hit by spells but a dispel), Animate Dead, Call the Debt (the damage the stack dealt; not
  scaled by Spell Power *(proposal)*), Plague, Vampirism, Final Reckoning, Terror (cannot attack or shoot, retaliates at half; fearless stacks,
  Steadfast, ignore it and the Terror warcry), Inferno, Pact of Ash, Hellfire, Harvest, Shroud, Forgetfulness, Night Terrors, Unseen (cannot be
  attacked or aimed at by the enemy until it acts). 75 of the 91 battle spells work; still to come: Consecrate, Martyrdom, Thornwall, Mirror
  Image, Teleport, Clockwork Precision, Drain Life, Raise Dead, Bone Wall, Frenzy, Fire Wall, Blood Price, Shadow Step, Darkness, Hypnotize,
  Puppet Master (they need field zones, copies, control changes or two-click aiming).
- **ADR-122's rank changes built**: Smite's splash (II, enemy stacks next to it at half) and its ignoring spell resistance (III), Holy Word's
  lost action (III, 30 %), Call Lightning's jump (III), Barkskin's thorns (III, 10 %), Firebolt's (III) and Dark Bolt's (II) burning, Frost
  Bolt's freezing (III, the next action lost). **Still not built**: Heal's cure and revival, Purge II's healing, Meteor Shower's rubble, Summon
  Demons staying, Ashfall sparing own demons, Bind Elemental's two kinds, the Familiars' mana theft, Entangle III's damage, Unravel III's Spell
  Power, Dispel III's half mana, Hellfire III, Inferno III's +20 %, Sanctuary III's line, Unseen III's first strike.
- **The battle AI** casts every kind: damage (splash and chain hits counted), summons, healing, revives (the value of the health), kills,
  dispels and buffs and curses: **4 %** of the stack's creature value per round it lasts, **10 %** for Blind, Stasis and Confusion, at most 5 rounds
  counted (a spell for the battle counts 5) *(proposal)*; nothing for a spell the stack already has or a ward when the enemy cannot cast.
  Creature casters use the same values (a buff still only when the caster has nothing to hit).
- **Elements** (ADR-106): the orbs' `element-power` was already in (ADR-111); Magic Arrow now takes the **best** of the four orbs *(proposal)*,
  Bind Elemental the element of the kind it binds (from the creature id). The catalog refuses `bound` on anything but a summon of several kinds.
- **Tags and creature casters** (ADR-099): `holy` and `summon` were in (ADR-110, ADR-120); the catalog now refuses a summon without the `summon`
  tag or the tag without a summon. Every caster id in `creatures.csv` maps to a spell with a battle effect (or is the Phoenix's rebirth); a test
  keeps it so.
- **Guild spells fixed by the map** (map format **v30**): a town's optional `guildSpells` (spell ids of the town's own school, at most 3 / 3 / 2 / 2
  a level, no repeats); the rest is drawn at game start as before. Spell buildings now also draw their spells at game start from
  `GameState.Random` (they were picked from a shuffle seeded by the game seed, town and school); the map cannot fix those *(proposal)*. Changing
  a town's faction in the editor drops its fixed spells (undoable). Editor: `SetTownGuildSpells`, a "Guild spells" section in the town inspector;
  the loader and validator refuse bad lists.
- **Show-only adventure spells** (each mana + 500 movement points): Distant Vision (armies within 10 of a cell in sight; II 14, III under the
  fog), Detect Magic (what objects within 10 hold; II 15, III the banks' rewards), Grave Whisper (+10 % Necromancy on the next victory that raises
  undead, until the week ends; II 15 %, III the guards' numbers within 10), Ledger of Ages (next week's event, enemy heroes in sight; II in towns
  too, III read again free that week), Veil (other players see the army as amount words until the week ends; II artifacts hidden, III the hero
  hidden from players with no hero within 3), Eavesdrop (an enemy or neutral town in sight, read-only; II any explored one, III again free that
  week). Events `ArmiesSighted`, `MagicDetected`, `GravesMarked`, `LedgerRead`, `TownOverheard`; `AdventureIntel.ShowHero` is the one query that
  shows another player's army; the hero panel arms a map click for every aimed spell (`AdventureSpellArmed`); `AdventureIntelPanel` shows them.
- **Adventure spells' ranks**: Dimension Door 9 cells (II), twice a day (III); View Terrain radius 18 (II); Pathfinder's Call and Ashwalk every
  terrain (II), +10 % of the day's movement (III) *(proposal)*; Town Portal to a town the player clicks (II, III); at ranks I–II the portal ends the
  day's movement *(proposal: reading III's "no movement points beyond the 500")*, which changes the built behaviour and the AI's portal home.
- **Not done**: the adventure AI casts none of the show-only spells; old replays with casting heroes may differ (the AI casts more, spell
  buildings draw from the game's generator).
### ADR-124: Map events, step 1 in core: the event tree, triggers, conditions, actions, map format v31 (ADR-096, ADR-115, EVT-1 … EVT-4, 2026-11-04)
**Status**: implemented in core (2026-11-04). Quests: ADR-127; the game app's windows, loops and "run another event": ADR-128; the editor's
"Events" tab, validator warnings and the AI answering by value: ADR-129.

- **Data**: `MapEventSet` (events in list order, declared variables with starting values, event cells) is the domain's own tree, written to the
  map as it is (`events`, v31): every node carries a `type` name (`MapEventJson`), so there are no parallel document types. An event has a
  name, a kind (hero, player, town), its actions, whether it starts enabled and, for a town event, its towns. Invisible cells are not map
  objects: views, fog, routes and the AI never see them.
- **Running** (`MapEventService`): triggers queue runs; one runs at a time and pauses on a message, a question, a full army, a battle or
  anything else pending (a level-up); the session carries it on after every command. Level-ups from an event are offered once the events
  are done, and only after an event ran, so games without events play exactly as before. A message or question for a player or town event
  has no hero (`MapEventDecision.NoHero`).
- **Rules taken where ADR-115 left a choice**: a disabled event is skipped when its turn comes (an earlier event may disable a later one at
  the same moment); a message's reward amounts are drawn when it shows, so its lines tell what comes; a hero without an army cannot fight
  an event's battle and the event stops; a computer player is shown no messages and its full army keeps the strongest stacks; neutral
  towns' events run at the start of every day, day 1 included; "defeated by" counts battles won by defeat (not retreats or surrenders);
  a given hero moves at once at a turn start and from the next turn when a hero event gives it; a building ignores the day's build.
- **Loading**: `MapEventRules.Problems` refuses events that do not fit the map (duplicate names, undeclared variables, unknown skills,
  artifacts, spells, creatures, heroes or events, questions without two or three answers, cells off free passable ground or naming a
  non-hero event, town events naming non-towns).
- **Commands**: `AnswerMapEventCommand` (an answer, or 0 to close a message) and `ChooseEventArmyCommand` (the stacks to keep). Events:
  `MapEventRan`, `MapVariableChanged`, `MapEventEnabledChanged`, `MapEventCellRemoved`, `MapEventHeroGiven`, `MapEventSkillGained`,
  `MapEventSpellBookGained`, `MapEventSpellLearned`, `MapEventWarMachineGained`.

### ADR-125: Scouting reveals information; amount words with their ranges (HERO-9, ADR-069, ADR-123, 2026-11-04)
**Status**: accepted (2026-11-04), built (ADR-142). The owner's answer to HERO-9: the proposals, and every amount word shows its range.

- **Baseline**: every army a player does not own (other players', neutral stacks, bank guards, garrisons), in sight or remembered, shows
  an **amount word with its range** instead of the count: a few (1–4), several (5–9), a pack (10–19), lots (20–49), a horde (50–99), a
  throng (100–249), a swarm (250–499), zounds (500–999), a legion (1000+) (`AdventureIntel.AmountWord`).
- **Scouting levels**, besides the sight radius (+2 / 4 / 6): **Basic**: exact counts of neutral stacks and bank guards in sight, an
  enemy hero's level and primary stats; **Advanced**: exact counts of enemy heroes' armies and town garrisons in sight, their artifacts and
  war machines; **Expert**: an enemy hero's skills and known spells, a town's available creatures. The best Scouting among the player's
  heroes that see the target counts.
- **Veil**: rank I hides counts from Scouting below Expert; rank II hides artifacts from everyone; rank III as built (ADR-123).
- **AI**: sees only what its heroes' Scouting shows, through the same `AdventureIntel` query; a word counts as the middle of its range
  (a legion as 1000).
- The information spells (Distant Vision, Ledger of Ages, Eavesdrop) keep showing exact counts.

### ADR-126: The owner's picks from the missing mechanics: map bans, random placeholders, University and War Academy, spell scrolls, the Grail, the first unique buildings (ADR-116, 2026-11-04)
**Status**: accepted (2026-11-04); map bans and random placeholders built (ADR-133), spell scrolls (ADR-136), the first unique buildings (ADR-138), the University and War Academy (ADR-139), the Grail part 1 (ADR-140; the puzzle map to rework, MAP-36). Owner's notes in `ideas.md`;
points marked *(proposal)* were filled in by the agent and stand unless the owner changes them. More unique buildings: TOWN-12; the Grail
buildings: ADR-130.

- **Map bans** (HoMM3, HotA): the map's rules list banned heroes, artifacts, spells and skills. The tavern pools, artifact rolls (chests,
  banks, Pandora's Boxes, merchants, loot), guild draws, shrines, level-ups, Witch Huts, the University and the War Academy skip them.
  *(proposal)* What the author places by hand (a hero, an artifact, a fixed guild spell) is kept, and the validator warns; a banned skill
  is never offered but a hero that starts with it keeps it; a ban that empties a pool falls back to the full list. Map format bump.
- **Random placeholders** (HoMM3, editor and map format): a **random monster** of tier 1–7 (or any tier) picks a creature of that tier from
  the playable towns' and neutral lines at game start; a **random town** takes its owner's chosen faction, a neutral one a random playable
  faction; a **hero placeholder** becomes a random hero of its owner's faction at game start (*(proposal)* campaigns later put a carried
  hero there, FLOW-1). The draw uses `GameState.Random` at game start, like guild spells.
- **University and War Academy**: adventure-map objects any hero may visit. The **University** teaches the magic schools, the **War
  Academy** the general groups (offense … war machines). Each shows a hero up to **2 new groups** (the group's first skill at Basic) and
  **2 of its skills** in those groups raised one level; a lesson costs **1000 gold + 5 crystal, 5 mercury and 5 sulfur**. Skills already
  learned (or at their maximum, or new groups past the 6-group limit) are shown disabled. *(proposal)* The offers are drawn per hero with
  the level-up weights on its first visit and kept for that hero at that object (walking off and on draws nothing new); the rival school
  can be bought; any number of lessons a visit. Generator values like a Witch Hut's, higher.
- **Spell scrolls**: one artifact per spell (Misc slot, a `spell-scroll` row with the spell as its parameter). *(proposal)* The hero casts
  the spell while carrying it, at its own rank for the school, with a spell book and the normal mana cost; scrolls of spell levels 1–3 come
  from the artifact rolls by level (minor 1, major 2, relic 3), level 4 only placed by hand; never pieces of a relic.
- **Grail** (HoMM3, HoMM5): *(proposal)* the Grail lies buried on free passable ground (set in the editor or rolled at game start, not within
  10 cells of a town); **obelisks** (map objects) each show the player a share of the **puzzle map** around it; a hero **digs** with its
  whole day's movement (only with full movement, not on roads or objects), a hole stays; the Grail is an artifact that cannot be traded or
  sold, and a hero bringing it into one of its owner's towns builds that town's **Grail building** at once (one per town). The generator
  places obelisks per template. Map format bump. Grail building effects: ADR-130.
- **Unique buildings, the first set** (one per town, beside the Necromancy Amplifier). Each hero gets a "once" bonus **once per town**
  (another town of the same kind gives it again); the record stays with the town when it is captured *(proposal)*. The siege bonus goes to
  the defending hero (in the town or its garrison) in a siege of that town. Working names *(proposal)*:
  - **Haven, Stables**: as the map's Stables (+400 movement at once and a day for the rest of the week, once a week per hero), without the
    free Lancer → Champion upgrade.
  - **Stronghold, Proving Ring**: +1 Attack once per hero; +1 Attack to the defending hero in sieges.
  - **Swamp Bestiary, Shell Wall**: +1 Defense once per hero; +1 Defense in sieges.
  - **Academy, Hall of Records**: +1 Knowledge once per hero; +1 Knowledge in sieges.
  - **Inferno, Altar of Brimstone**: +1 Spell Power once per hero; +1 Spell Power in sieges.
  - **Sylvan, Elder Grove**: +1000 experience once per hero; +1 luck in sieges.
  - **Dungeon, Mana Spring**: mana +75 % of the hero's maximum, may go above it until spent (like the Mana Vortex), once a week per hero;
    +1 morale in sieges.
  - **Necropolis**: the Necromancy Amplifier (ADR-069).
  - *(proposal)* Cost 2000 gold, 5 ore and 5 of the town's rare resource; needs Fort 1 (Stables: town hall 2). `buildings.csv` rows per town.

### ADR-127: Map events, step 2 in core: quests, the Seer's Hut, Quest Guard and Quest Gate, the quest log, map format v32 (ADR-115, EVT-5, 2026-11-04)
**Status**: implemented in core (2026-11-04). The quest log window, the object shapes and the inspector link: ADR-128; writing quests in the
editor and the AI weighing them: ADR-129.

- **Quests are map events** of a fourth kind, `Quest`: instead of actions a quest has **stages**, done one after another. A stage has a
  **requirement** (any condition of EVT-3, but no random numbers, so routes can test it), a **proposal** (first visit and the quest log), an
  optional **progress** text (later visits before it is met), an optional **completion** question and the **actions** run when the hero
  completes it: what it takes, then its rewards (any action, a battle included; a lost battle leaves the stage open).
- **Ready forms** (`QuestForms`): HoMM3's quests as stages that take what they ask: level, primary stats, artifacts, creatures, resources, beat a
  monster, beat a hero, be a hero, a class or a player. The editor will offer them; they write the same tree.
- **Quest objects** (`QuestSite`, `questSite` in the map): each names its **own** quest (two objects never share one; none = an empty hut, a
  closed barrier, a validator warning). A stage's progress is the object's, shared by all players.
  - **Seer's Hut** (a building): a visit that does not meet the stage shows the proposal or progress; one that does asks "Complete / Not now".
    Completing runs the actions, then shows the next stage's proposal; after the last stage the hut stands empty.
  - **Quest Guard** (one tile): every hero bumps into it (the hero does not move, `MoveResult.QuestVisited`); completing its last stage
    removes it.
  - **Quest Gate** (one tile, one stage that takes and gives nothing): heroes that meet its quest walk through without stopping; the others
    bump into it and are shown the proposal. Routes treat both barriers as walls and may end on them (a click bumps).
- **Quest log** (per player, in the game state, `MapEventState.QuestLog`): an entry per quest object the player's heroes were shown, with its
  stage and whether the player completed it; when someone completes a stage, the other players' open entries for that object go. A gate's
  entry is done once one of the player's heroes passes.
- A computer player completes any stage it meets (it takes the first answer); the AI's judgement of quests is the AI step of the roadmap.
- **Loading**: quests have stages and no actions, every stage a proposal, no random numbers in requirements; only quests have stages; a quest
  object names a quest, not shared, and a gate's quest has one empty stage. A map with quest objects and no events is checked too.
- Events: `QuestStageCompleted`, `QuestLogChanged`. Map format **v32**: an event's `stages` and the `questSite` objects (`seer-hut`,
  `quest.guard`, `quest.gate`).

### ADR-128: Map events: the game app's windows and quest log, quest object shapes; step 3: loops, "run another event", a Pandora's Box's hero event, map format v33 (ADR-115, ADR-124, ADR-127, MAP-35, 2026-11-04)
**Status**: implemented (2026-11-04); core tested; the game app checked in Godot 4.7.2 with the new smoke scenario `game-quests`. The camera moving to what a
turn-start message concerns (EVT-2 f): ADR-132. The editor's "Events" tab and the AI's use of events and quests: ADR-129. Choices made here are marked *(choice)*.

- **Step 3, loops** (`RepeatAction`, `WhileAction`): a repeat's count is drawn once, when it starts; 0 or less runs nothing. A while loop tests
  its condition before every round. Both stop after **100 rounds** (`MaxLoopRounds`) *(choice)*. A message, question, battle or full army
  inside a loop pauses it, and it goes on afterwards.
- **Step 3, running another event** (`RunEventAction`): the named event's actions run there and then for the same player, hero, town and
  cell, and the event goes on after them. *(choice)*:
  - a disabled event is skipped;
  - a stop ends only the run event, and "this event" in it (enable or disable) means the run event;
  - a quest cannot be run (a load error);
  - nesting deeper than **16** (`MaxRunDepth`) is skipped, so an event that runs itself ends;
  - each run event publishes `MapEventRan`.
  - One run takes at most **100 000 actions** (`MaxActionsPerRun`) and then ends, a guard against loops inside loops.
- **Pandora's Box** (MAP-35): a box may name a **hero event** (`Event`), which runs for the opener after the contents and the experience (and
  any level-up) are given *(choice: both, not instead)*.
  - A box with only an event is valid. An event name that is not a hero event of the map is a validator error and a load error.
  - The AI values only the contents, so it leaves a box that holds nothing but an event (no look-ahead at events, EVT-6).
- **Map format v33**: the action types `repeat`, `while` and `runEvent`, and a box's `event`. A map whose boxes name events but which has no
  events is checked too.
- **Editor**: `SetPandorasBoxEvent` and `SetQuestSiteQuest`, both undoable. The inspector gets a picker of the map's hero events for a box,
  and of its quests for a quest object.
- **Game app**:
  - the decision prompt shows a map event's message (with its reward lines, OK) and question (one button per answer), and the full army's
    choice (tick one to seven stacks, Keep), so ADR-124's windows are in;
  - the **quest log** (Q, or the HUD's "Quests (Q)"): open quests first, each with its object, where it stands, "stage n of m", "open" or
    "done", and its proposal; "Show" moves the camera there; the log follows `QuestLogChanged`;
  - the Q press that opens or closes the log does not also turn the camera: `StrategyCamera.IgnoreHeldKeys` ignores held keys until they are
    let go.
- **Quest object shapes** (placeholders until the art phase's models): the Seer's Hut gets a pointed roof and a lamp, the Quest Guard a figure
  with a spear, the Quest Gate two posts, a lintel and a seal.

### ADR-129: Map events, step 4: the AI's answers and quests, the editor's "Events" tab, validator checks (ADR-115, EVT-6, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`AdventureAiEventTests`, `MapEventEditorTests`); the editor's tab checked in Godot 4.7.2 with
the new smoke scenario `editor-events`. No map format change. Choices made here are marked *(choice)*.

- **The AI and events** (`AdventureAi.Events.cs`):
  - A **question** is answered with the answer worth the most to the player (the first of equals), where an answer's worth is that of its
    actions. Random numbers count as their average, `(min + max) / 2`, so the AI still draws none.
  - **Values** *(choice)*, the AI's usual gold values where it has them: resources at their gold value, experience 1 a point, a primary
    stat 750, a secondary skill 1000 a level, an artifact 800 + 80 × its find score, a war machine 300 (taking one −300), a spell book 500, a
    spell 400 × its level, mana 2 and movement 1 a point, morale and luck 150 a point, creatures 0.85 × their value (0.6 to a town's
    garrison, 0.4 as creatures to hire), a building 1000, revealing an area 100, a hero 2500; what is taken counts negative.
  - A **battle** is played out by the battle estimator: one the hero wins is worth 0.35 × the enemy's power; one it loses is worth
    −(its army's power + 5000) and ends the answer's worth there, as does a stop. Branches count the branch whose condition holds now
    (the AI reads the variables, it draws no random numbers); a repeat counts its average count; a while loop counts one round
    *(choice)*; "run another event" is followed 4 deep *(choice)*.
  - **Quests**: a hero goes to a Seer's Hut or Quest Guard only when it meets the current stage and the stage is worth more than nothing
    (what it takes, then its rewards); a Quest Guard adds 500 for getting it out of the way *(choice)*, plus the unexplored ground behind it.
    The completion question is answered the same way, so a stage not worth doing is declined. A Quest Gate is open only to a hero that meets
    it (exploring and routes); a guard or gate it does not meet stays an obstacle.
  - **No look-ahead**: event cells, player and town events and a Pandora's Box's event are worth nothing until they run.
- **Editor core**:
  - `MapEditorSession`: add, replace (a rename rewrites "run another event", enable and disable references, cells, boxes and quest objects),
    remove (also its cells and the references of boxes and quest objects), move, map variables, and event cells (only on passable,
    free tiles, only for hero events); all undoable, publishing `MapEventsChanged`.
  - The editor holds events that do not fit yet: `GameState.SetMapEvents(set, check: false)`, and the editor opens maps with
    `MapConverter.ToGameState(..., checkEvents: false)`; the validator reports what the game's load would refuse. The game still refuses
    such a map.
  - `MapEventTreeEdit` addresses an action by its path of lists (a quest's stages, then, then and else, do, an answer's actions, a message's
    rewards) to insert, replace, remove and move it; `MapEventText` describes a node in one line for the tree.
  - **Simple forms** (`SimpleEventForms`) write HoMM3's events as ordinary event trees *(choice: no form kept in the file)*: a **timed event**
    (a player event: its first day and every n days, for which players, people and or computers, a message and rewards), an **event cell**
    (a hero event, once or every time) and a **town event** (its towns, days, buildings and creatures to hire). "Every n days" is written with
    the arithmetic the conditions have, there being no remainder operator.
  - `MapEventTemplates`: a blank event of each kind, one action of every type and the usual conditions, valid on the map.
- **Node text** *(choice)*: the tab edits a chosen action, or a quest stage without its actions, as the JSON the map file writes
  (`IMapEventNodeText`, implemented by `MapEventNodeJson` in Core.Data and handed in by the editor app, so Godot.UI does not use Core.Data). A
  node with a value missing that may not be missing is refused with the reason, so every action, condition and expression is editable
  without a form of its own; the menus insert templates.
- **Validator** (`MapEventRules.Check` returns problems with their event and position; `MapEventChecks.Warnings`): the load errors as
  errors; as warnings an event that runs itself, an event nothing runs (no cell, box, other event, trigger or quest object), a quest no
  object asks, a town event with no towns, an event that starts disabled and that nothing enables, a message without text, a quest stage
  that cannot be met (an unknown hero, player, monster, town, artifact, creature, resource or skill, or a level heroes cannot reach), and a
  variable nothing reads. An issue sits on the event's position, cell, quest object or town.
- **Editor app**:
  - the **"Events" tab**: the events in run order (new of each kind or from a form, up, down, delete), the chosen event's name, whether it
    starts enabled, its cells ("Place cells" makes map clicks place or remove them; "Show"), towns or quest objects ("Show", "Add stage"), its
    tree with "Add action", up, down, delete, the node's text with Apply and "Insert condition", the validator's findings for it, and the map
    variables with their starting values;
  - the event cells drawn on the map in violet with their event's name, the chosen event's in gold (`EventCellsView`, the editor only);
  - **inspector links**: a box's hero event and a quest object's quest get "New" (a blank event, picked, one undo step) and "Edit"; a town
    lists its town events and has "New town event"; a double click on a validation issue of an event opens it in the tab.
- **Tested by playing the map**: a test writes events with the editor's forms and edits, saves and loads the map, and plays it in the game.

### ADR-130: The Grail buildings; more unique buildings stay open (TOWN-12, ADR-126, 2026-11-05)
**Status**: accepted (2026-11-05); built with the Grail's part 1 (ADR-140), core tested. Owner's answer to TOWN-12; points marked *(proposal)* were filled in
by the agent and stand unless the owner changes them.

- **Grail buildings** (b): every town's Grail building does the same: **+5000 gold a day** and **+50 % growth** of every dwelling in its
  town, and in a **siege of that town** the defending side gets **+2 Initiative** for each of its stacks and **+5 Attack, Defense, Spell
  Power and Knowledge** for its hero. The proposal's per-faction effects for all the owner's heroes (morale, luck, guild spells, Spell
  Power, Necromancy, the revealed map, Attack, Defense) are dropped. *(proposal)* The siege bonus applies like the unique buildings' (the
  hero in the town or its garrison; the stacks also without a hero) and counts for the battle only (Knowledge adds no mana in it); the
  +50 % applies to the week's growth after the growth buildings, rounded down; names per town come with the buildings' rows.
- **More unique buildings** (a): stays open. The first set (ADR-126) is the only one planned; the owner adds further buildings per town to
  TOWN-12 later.

### ADR-131: Map events: campaign variables and the score check, map format v34 (ADR-101, ADR-115, EVT-1 f, FLOW-1, 2026-11-05)
**Status**: implemented (2026-11-05). Points marked *(proposal)* are the agent's reading and stand unless the owner changes them.

- **Campaign variables** (EVT-1 f): a map variable may be marked **"kept for the next map"** and / or **"taken from the last map"** (the
  events' `keptVariables` and `takenVariables`, names of declared variables; map format v34, older maps load without them).
  - A taken variable starts with the value the campaign carries in (`GameOptions.CampaignVariables`), or with its own starting value when
    it carries none; carried names the map does not take are ignored. The carried values are part of the game's options, so a replay or a
    save starts the same (the record format is unchanged; older records have none).
  - At any moment, and so at the end of the game, `MapEventState.CampaignVariables` gives the kept variables' current values: what a
    campaign carries on to the next map. The campaign itself (FLOW-1, Phase 6) passes them; until then nothing does.
  - A mark on an undeclared variable is a load error; a kept variable that no event reads is not warned about (the next map reads it).
  - **Editor**: "Kept" and "Taken" tick boxes beside each variable in the Events tab (`MapEditorSession.SetCampaignVariable`, undoable);
    removing a variable drops its marks.
- **Score check** (ADR-101, FLOW-4 a): a map is campaign-like, and not ranked, also when a map event **favours people**
  (`MapEventChecks.FavoursPeople`): it gives something only where its condition holds for people alone. Worked out from the tree, without
  playing: the "then" of an "if" (and the body of a "while") whose condition needs "the player is a person" (directly, in an "all of", in
  every part of an "any of", or as "not" of a condition every computer player meets), the "else" of an "if" whose condition every computer
  player meets, a quest stage only people can meet, and the events such a branch runs. A ready form with "computer may trigger" left off is
  such an "if". *(proposal)* "Gives something" is any action but a message without rewards, a question, a stop, a variable, enabling or
  disabling events, removing the cell and a loss (negative amounts, taking an artifact or war machine, lower morale and luck); a gift that
  only a variable leads to is not followed. The editor's validator warns about such an event: "games on this map are not ranked".
- **Tests**: the carried values, the kept values after play, the format round trip and a replay with carried values, the editor's marks,
  the load error, an older map and record; six events that favour people and six that do not.

### ADR-132: Map events: the camera on a turn-start message (ADR-115, EVT-2 f, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`MapEventTests`); checked in Godot 4.7.2 with the new smoke scenario `game-turn-start-camera`.
No map format change. With it the map events of ADR-115 are complete. Points marked *(proposal)* are the agent's reading and stand unless
the owner changes them.

- A message or question that a **turn-start** run shows (a player event, or a town event at its owner's turn start) names the place it
  concerns (`MapEventDecision.Place`), and the game app moves the camera there (and to its level) when it shows the window. What it concerns
  *(proposal)*, first that applies:
  - a place the message's own rewards name: the centre of an area they reveal, a cell they give a hero at;
  - the last place the run touched before it: the centre of an area it revealed, where a hero it gave stands;
  - the town of a town event (its entrance).
  Otherwise there is no place and the camera stays on the turn's first hero.
- Hero events (cells, quest objects, a Pandora's Box's event) name no place: their hero is in view. A computer player sees no messages.
- The camera moves after the turn's own move to the first hero, and again for each later message of the turn; not while the computer's
  moves are still being shown (the turn's showing comes back to it).

### ADR-133: Map bans and random placeholders built (ADR-126, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`MapBanAndPlaceholderTests`); checked in Godot 4.7.2 with the new smoke scenarios
`game-placeholders` and `editor-bans`. Map format v35. Builds ADR-126's map bans and random placeholders; points marked *(proposal)* are the
agent's choices made while building and stand unless the owner changes them. Rules: [adventure-map.md](../design/adventure-map.md#map-bans-and-random-placeholders).

- **Bans** are part of the map's rules (`ScenarioRules.Bans`, `rules.bans` in the file: heroes, artifacts, spells, skills; sorted, empty
  lists left out). Read by the town and map taverns, prisons, hero placeholders, every artifact roll (one place: pickups left open and
  `Draw`), the guild and spell building draws, shrines, level-up offers and witch huts. A pool the bans would empty stays whole (ADR-126).
  *(proposal)* A guild's spells fixed by the map stay even when banned (placed by hand); a witch hut's own skills stay, but the stand-in
  skill of a group it offers when the hero cannot take its skill skips banned ones; the tavern pool is shuffled before the bans are taken
  out, so a ban does not reorder the rest.
- **Validator**: an unknown id in the bans is an error; a hero of a banned type, a banned artifact, spell or skill on a hero, a banned
  prisoner, artifact pickup, shrine spell, fixed guild spell, witch hut skill or Pandora's Box artifact is a warning ("placed by hand, so
  it stays").
- **Random monster** (`randomTier` on a monster: 0 any tier, 1–7): *(proposal)* the creatures it may become are the growing creatures of
  the playable towns and the neutral ones, base and upgraded; a count set by the author stays, otherwise the count is rolled for the drawn
  creature; the creature in the file only shows it in the editor (the editor shows the tier's first such creature).
- **Random town** (`random` on a town): an owned one takes its owner's chosen faction (`GameOptions.StartTowns`, set on the new-game
  screen's new "Town" column for a player with a random town or with a hero placeholder and no town; kept in saves and replays);
  *(proposal)* without a choice, one faction is drawn per player and all its random towns share it; a neutral one draws its own. The town is
  re-made as the editor's type change does (`TownRules.Retyped`: same id, place, owner, garrison, an author's name; buildings carry over,
  dwellings move by tier). Making a town random in the editor drops its fixed guild spells.
- **Hero placeholder** (`placeholder` on a hero): *(proposal)* its faction is its owner's first town once the random towns are drawn, else
  the chosen faction, else the player's drawn one; it becomes a free roster hero of that faction (not on the map, in a prison or retired;
  not banned), else any free hero. It keeps its place, army, artifacts and id (so conditions naming it still hold) and takes the roster
  hero's identity, stats, skills and spells; an empty army gets the hero's starter stack (as a tavern hire).
- **Game start**: the placeholders are drawn first (`PlaceholderRules.Resolve`): towns, monsters, heroes, from `GameState.Random`; views
  built before the start redraw from `MapObjectChanged` and `HeroRemoved` + `HeroPlaced`.
- **Editor**: a "Bans" tab after "Events" (a list per kind with a filter and "banned only"; ticking bans; undo says "Edit the bans");
  "Random faction" on a town ("Shown as" its type), "Random" (any tier or a tier) on a monster, "Placeholder" on a hero; captions mark
  random towns and monsters.
- **Not in this step**: the generator places none of them; the University and War Academy (ADR-139) read the bans; a campaign's
  carried hero in a placeholder waits for FLOW-1.

### ADR-134: New-game screen: the tier-1 dwelling pick and the hero limit (ADR-086, ADR-068, RMG-7, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`StartSlotsTests`); checked in Godot 4.7.2 with the reworked smoke scenario
`game-random-map`. Closes the two points ADR-086 left open; no rule or map format change, the generator core already took both.

- **Tier-1 dwelling**: a "Tier-1 dwelling" column on a random map's player rows, between Hero and Team: "Random" and the picked town's
  tier-1 lines by name (`StartSlots.Tier1Dwellings`), passed as `PlayerSetup.Tier1Dwelling`. *(proposal)* It is open only for a human player
  with a picked town whose start zone gives no tier-1 dwelling (`StartSlot.PicksTier1Dwelling`); otherwise it stays "Random" and its tooltip
  says why (a computer player's is drawn, as RMG-7 says). Changing the town or human / computer resets it.
- **Hero limit**: a "Hero limit" row under "Players" (1–8, the template's `MaxHeroes` by default). Another value goes to `MapGenerator.Build`
  as the `maxHeroes` override; one limit for every player, as in the editor's Generate tab (ADR-087). Hand-made maps keep their own limits.
- **Smoke**: `game-random-map` had drifted to "Two against two" as the map list grew and never left the screen; it now picks Duel,
  Necropolis, the first hero, Skeleton and a hero limit of 7, starts, opens the kingdom and ends the turn.

### ADR-135: Roster revision: renames, two swaps, classes A and B, Necromancy by class (CONT-2, ADR-072, 2026-11-05)
**Status**: implemented (2026-11-05), from the owner's notes in [ideas.md](ideas.md); core tested (`CreatureTests`, `NecromancyTests`,
`CreatureBankTests`). Stats marked "placeholder stats (ADR-135)" in `creatures.csv` wait for play-testing; points marked *(proposal)* are the
agent's choices. The Undead Transformer from the same notes is TOWN-13, answered in ADR-137.

- **Renames** (no stat change; ids follow the names): Qilin → **Moon Doe** (`sylvan.moon-doe`), Pristine Unicorn → **Unicorn**
  (`sylvan.unicorn`), Defender → **Dwarf** (`academy.dwarf`), Shield Guard → **Dwarf Defender** (`academy.dwarf-defender`), Dragon Mantis →
  **Serpent Fly** (`swamp-bestiary.serpent-fly`). *(proposal)* The Dwarves lose the `human` tag.
- **The neutral Dwarf and Battle Dwarf are removed**; the Dwarven Treasury is guarded by the Academy's Dwarves (a fifth Dwarf Defenders) and
  neutral Thanes, the Dwarf counts ×3 to keep the guard's worth (120 / 180 / 240 / 330).
- **Necropolis tier swap**: the Wight line (Wight, Wraith) is **tier 2**, the Death Weaver line (Death Weaver, Fate Weaver) **tier 3**, with
  placeholder stats of their new tier (growth and prices as the other line of the tier). The Crypt's Wights are ×2.4 (12 / 20 / 24 / 36) to keep
  its guard's worth.
- **Haspid and Wyvern swap towns**: the **Wyvern** line (`swamp-bestiary.wyvern`, `…wyvern-monarch`) is the Swamp Bestiary's second tier-4
  line (growth 3, 1100 / 1450 gold); the **Haspid** line (`dragon-utopia.haspid`, `…royal-haspid`) joins Dragon Utopia (growth 2, 1650 / 2150
  gold); placeholder stats. Oszrak and Szask now prefer the Wyvern.
- **Classes**: each playable town's eight tier 1–4 lines are class **A** or **B**, one of each a tier, as the owner listed them; an upgrade
  has its base's class (the catalog refuses another); tier 5, Conflux, Dragon Utopia and neutrals have none. `CreatureDefinition.Category`,
  the `category` column after `upgrade` in `creatures.csv`.
- **Necromancy by class**: a slain creature's hit points go to the undead line of its own class in the tier the skill level gives (ADR-072's
  table is unchanged); *(proposal)* an unclassed creature's are split between the two lines in a random proportion, as before.
- **No migration**: maps and saves naming the old ids no longer load (no shipped map uses them; as with the Wyvern's move in ADR-049).
- `creatures.csv` loses the "sandbox stats were copy-pasted" notes.

### ADR-136: Spell scrolls built (ADR-126, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`SpellScrollTests`); the Godot part (scrolls on the paper doll, their spells in the spell
book and the hero screen's artifact line, the editor's spell choice on a scroll pickup) not run yet. Builds ADR-126's spell scrolls, with the
owner's changes of 2026-11-05 (one scroll per spell level, 5 % of the rolls, the spell decided by the generator or the editor, values by
level); points marked *(proposal)* are the agent's choices made while building and stand unless the owner changes them. Rules:
[artifacts.md](../content/artifacts.md#how-artifacts-work).

- **One scroll per spell level**: four rows in `artifacts.csv`, `spell-scroll-1` … `spell-scroll-4` (Spell Scroll I–IV, Misc), each with the
  effect `scroll <level>` (a new effect kind) and no set or pieces; tiers 1 Minor, 2 Major, 3 and 4 Relic. A scroll with a spell written on it
  is the id `spell-scroll-1:light.bless` ("Scroll of Bless"): the catalog looks it up from its level's row (a spell of another level is not an
  artifact), so heroes, pickups, loot, trade, events and the map file carry it like any artifact id. No map format change.
- **When the spell is decided**: the random map generator writes it when it places a scroll. In the editor the author picks the scroll and
  then a spell of its level or "random"; a random (blank) scroll gets its spell at game start, as do blank scrolls a hero starts with.
  A scroll from a roll during play (rewards, banks, merchants, loot sites) is written when drawn, and a blank one given by a map event, a
  Pandora's Box or a quest when given. The spell is drawn from the game's random source among the level's spells of every school, without
  the map's banned spells unless they ban the whole level.
- **Casting**: a worn written scroll adds its spell to the spells the hero can cast (`MagicRules.Known`), so the battle caster, adventure
  spells, the spell book and the AI see it. It needs a spell book and pays the normal mana. *(proposal)* It needs no school skill (as
  HoMM3's scrolls need no Wisdom), and its rank is what the school's Mastery allows; a learned spell keeps the higher of the two ranks.
  *(proposal)* "Carrying" means wearing: like every other artifact, a scroll in the backpack does nothing, so a hero uses at most three at a
  time (the Misc slots). Nothing is learned from a scroll.
- **Rolls**: every roll (pickups left open, rewards, banks, merchants, loot sites, the generator's treasure) goes through
  `ArtifactRolls.Pick`: the tier's scroll takes **5 %** of the rolls, the other artifacts share the rest. Level 4 scrolls are never rolled
  (`ArtifactCatalog.OfTier` leaves them out), only placed by the map's author. A relic scroll counts once in the relic pool's "one of each
  before repeats", whatever spell it holds; the generator places at most one level 3 scroll per map, like any relic.
- **Value**: by spell level, blank or written (`ArtifactValue.OfScroll`): 5 / 10 / 18 / 28 for levels 1–4 (+1 Attack is 10). The AI uses it to
  choose and equip artifacts and to weigh pickups; loot that does not fit goes by it.

### ADR-137: The Undead Transformer (TOWN-13, ADR-135, 2026-11-05)
**Status**: accepted (2026-11-05), built (ADR-143). The owner's answers to TOWN-13; the other points are the agent's proposals the owner took.

- **What it is**: a second Necropolis-only building. It turns living creatures of tier 1–2 into the tier-1 undead of their class (ADR-135:
  class A to Skeletons, B to Plague Bearers) and of tier 3–4 into the tier-2 undead (Ghouls, Wights); never elementals or constructs. It also
  swaps tier 1–2 undead for the other class's line of the same tier (Plague Bearer to Skeleton, Ghoul to Wight).
- **Counts**: one undead per living creature, as HoMM3's Skeleton Transformer. A swap gives the old count × 0.75, rounded down (at least 1).
- **Upgrades**: an upgraded creature counts as its base. The result is the upgraded undead when the town has the upgraded dwelling of that
  undead line, else the base undead (owner).
- **Creatures without a class** (tier 5, neutrals, Conflux and Dragon Utopia): tier 5 is refused; an unclassed tier 1–4 creature may become
  either line, the player picks.
- **Use**: 1000 gold + 5 mercury, needs a tier-1 dwelling. The visiting hero's or the garrison's stacks, **several at a time** as in HoMM3
  (owner); free, any number of times a day.
- **AI**: turns every eligible stack it does not need into undead when its hero visits.

### ADR-138: The first set of unique buildings built (ADR-126, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`UniqueBuildingTests`); the Godot part (the buildings on the town screen, which lists the
table's rows) not run yet. Builds ADR-126's first set; points marked *(proposal)* are the agent's choices made while building. Rules:
[town-buildings.md](../content/town-buildings.md).

- **Rows**: one row per town in `buildings.csv` (`stables`, `proving-ring`, `shell-wall`, `hall-of-records`, `altar-of-brimstone`,
  `elder-grove`, `mana-spring`), 2000 gold, 5 ore and 5 of the town's rare resource (Haven and Stronghold crystal, Swamp Bestiary and Dungeon
  sulfur, Academy, Inferno and Sylvan mercury), Fort 1 (the Stables town hall 2). The effects are code (`UniqueBuildings`), keyed by the id.
- **Visits**: every town visit of an own hero gives the bonus: entering the town, being hired or teleported there, standing in it (or in its
  garrison) at the start of the turn, or standing there when the building is finished. *(proposal)* Allied heroes get nothing. The record is
  the town's visit log, so it stays with the town when it is captured; the Stables and the Mana Spring are once a week per hero, the others
  once per hero.
- **Stables**: the map Stables' status (same source, so the two do not add up) and +400 movement at once, without the Lancer upgrade.
  **Mana Spring**: mana + 75 % of the maximum (rounded down), above the maximum until spent, capped at twice it like the Mana Vortex.
  **Elder Grove**: 1000 experience (a level-up is offered as usual).
- **Sieges**: the hero defending the town in a siege (a hero on its entrance or in its garrison, the town with a Fort) gets the building's
  bonus to its Attack, Defense, Spell Power, morale or luck for the battle. *(proposal)* +1 Knowledge lends 10 mana for the battle: spent first,
  the rest gone afterwards; a hero without a spell book gets nothing from Knowledge or Spell Power. A garrison without a hero gets nothing.
  `UniqueBuildings.SiegeOf` sums the town's buildings, for the Grail buildings to join (ADR-130).
- **AI**: builds its town's unique building after the growth buildings; its heroes take the bonuses when they pass through. *(proposal)* It
  does not yet go to a town for them.
- **Names**: working names. The Inferno **Altar of Brimstone** is close to the **Brimstone Altar**, Sylvan's Inferno spell building; a
  rename of one of them is open to the owner.

### ADR-139: The University and the War Academy built (ADR-126, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`SkillSchoolTests`); the Godot part (the lessons in the object panel) not run yet. Builds
ADR-126's University and War Academy; points marked *(proposal)* are the agent's choices made while building. Rules:
[adventure-map.md](../design/adventure-map.md#special-sites).

- **Objects**: one kind, `SkillSchool`, two definitions: `university` (the magic schools) and `war-academy` (the six general groups, offense …
  war machines); never the faction group (Primal Fury). Map format v36 (`skillSchool`; which one comes from the definition).
- **Lessons**: on a hero's first visit the school draws that hero's lessons and keeps them for it (`SkillSchool.Lessons`, `LessonsDrawn`):
  up to 2 groups the hero has not learned, each as the group's first skill not banned at Basic, and up to 2 skills of its learned groups of
  that kind below their maximum, one level up. Both draws use the level-up weights (`SkillOffers.Weight`); the rival school is drawn like any
  other school. New groups are drawn even when the hero has six groups.
- **Taking one** (`TakeLessonCommand`, `LessonTaken`): the hero on the entrance, 1000 gold + 5 crystal, mercury and sulfur, any number a
  visit. A lesson is open while the skill is one level below it and the hero can raise it; it shows **learned** once the skill is at its level
  (taken here or learned elsewhere since) and **no room** when it would start a seventh group. *(proposal)* A hero with no lessons at all is
  told the school has nothing to teach it.
- **Bans**: banned skills are never drawn; a group whose skills are all banned is not offered.
- **AI**: weighs an unvisited school as one lesson (1200 gold) when it can pay, a visited one by the lessons it would take; on the entrance it
  takes its open lessons while the spare resources last (a third of the gold kept), raises before new groups, a new group only with a free
  group slot. *(proposal)*
- **Generator**: treasure value 2500 each, weight 1, at most one of each per map *(proposal)*.
- **Game app**: the object panel lists the hero's lessons with the price; learned and no-room lessons are disabled.

### ADR-140: The Grail built; the owner's puzzle map rules (ADR-126, ADR-130, MAP-36, 2026-11-06)
**Status**: implemented (part 1 2026-11-05, the puzzle map, the editor tool and the generator's obelisks 2026-11-06); core tested
(`GrailTests`); the Godot part (puzzle window, digging, holes, the editor's Grail tool, the template editor's obelisk count) built, not
run. Points marked *(proposal)* are the agent's choices made while building. Rules: [adventure-map.md](../design/adventure-map.md#special-sites).

**Built in part 1** (stays):
- **The Grail**: an artifact (`grail`, Misc slot, value 500 for the AI) never rolled, not sold, traded or sacrificed. It is buried at
  game start (`GrailRules.Bury`, `GameState.Random`) only on maps with at least one obelisk, on diggable ground more than 10 cells
  (Chebyshev, same level) from every town's entrance *(proposal)*, any diggable cell when none is that far; or where the author put it
  (map format v37, `grail`; the editor's `SetGrail` with undo, the validator's checks).
- **Digging** (`DigCommand`, key G): only with the whole day's movement, which it uses up; not on roads, objects or an old hole. A hole
  stays and is shown. *(proposal)* With a full backpack the Grail stays buried and no hole is left.
- **Grail building**: a hero carrying the Grail into an own town without one raises it at once (`GrailBuildingRaised`); it does not count
  as the day's building. Effects as ADR-130; per-town names *(proposal)*: Haven Dawnspire, Stronghold Skull of the First Khan, Swamp
  Bestiary Mother Mire, Academy Orrery of Ages, Inferno Everburning Pyre, Sylvan Heartwood Tree, Dungeon Abyssal Lantern, Necropolis
  Throne of Bones.
- **AI**: visits obelisks its player has not visited, digs where its puzzle shows the spot, carries the Grail to an own town without the
  building.
- **Game app**: the puzzle map window (P), notices for obelisks, holes and the Grail building. **Editor**: the Grail tool (click to bury,
  click again to take away); placing a 26th obelisk is refused.

**The puzzle map, owner's rules** (2026-11-06, replace part 1's 15×15 map of 3×3 pieces opened outer ring first):
- **Size**: the puzzle covers **20×20 map cells** in **pieces of 2×2 cells**: 10×10 = **100 pieces**.
- **Segments**: the **centre** is the middle 2×2 pieces (4×4 cells), the **mid ring** the rest of the middle 6×6 pieces (32), the **outer
  ring** the remaining 64 pieces.
- **The Grail** lies in the centre's 4×4 cells, not always in the same one of them; so the Grail cannot lie too close to the map's edge.
- **Obelisks**: at most **25** on a map, so an obelisk opens at least 4 pieces. The **first half** of the obelisks a player visits open
  random pieces of the outer ring only; the **next ones but the last** random pieces of the outer and mid rings; the **last** opens the
  centre.
- **What it shows**: terrain and the objects that cannot be removed, without ownership (no flag colours).
- Like HoMM2 and HoMM3: each obelisk visited opens a share of the picture.

**MAP-36, the owner's answers** (2026-11-06):
- **Shares** (a): the 100 pieces split over the map's obelisks as evenly as can be, the first visits getting the smaller shares, the last's
  share including the centre's 4 (`GrailRules.Share`): 25 obelisks open 4 each, 11 open ten 9s then 10, 2 open 50 and 50.
- **Halves** (b): the first obelisks / 2 visits (rounded down) open the outer ring, the next ones but the last the outer and mid rings, the
  last the rest with the centre. With 2 obelisks the first opens 50 of the 64 outer pieces. A ring with fewer pieces left than the share
  opens what is left.
- **Draws** (c): at each visit, from `GameState.Random`; the number of obelisks the player has visited counts, not which.
- **Players** (d): a puzzle per player; allies' visits do not count.
- **Edges** (e): the Grail lies at least **10 cells** from every edge, so maps under **24×24** have none. The editor refuses a nearer spot
  (`TooNearEdge`), the validator reports one as an error, and warns about obelisks on a map too small for a Grail. The puzzle's place is
  drawn at game start so the Grail falls in its centre 4×4 cells and the 20×20 cells lie on the map (`GrailState.PuzzleOrigin`).
- **Shown** (f): terrain and every object that stays (towns, mines, dwellings, gates, sites, scenery …); not piles, chests, artifacts,
  loot, Pandora's Boxes, monsters, prisons or quest guards (`GrailRules.ShowsOnPuzzle`). The window names the level.
- **Generator** (g): a zone's `obelisks` in the template (format v9): a count (0–25), or unset to share the map's 25 with the other unset
  zones, the first zones getting one more; so a map gets **25 by default**. Placed after the treasure, unguarded, three tiles apart
  (`TreasurePlacer.PlaceObelisks`, stream `obelisks`); none on a map under 24×24. A zone without room gets fewer (reported when the
  template set its count).

### ADR-141: A town sees from its middle (owner's bug report, 2026-11-06)
**Status**: implemented; core tested (`VisibilityTests`).
- **Bug**: at game start the fog opened around the starting hero, not the town: an owned town revealed radius 5 from its entrance, the
  bottom-middle cell where the starting hero stands, so the land above and beside the 7×7 town stayed dark.
- **Rule** (owner, 2026-11-06): an owned town reveals and watches from the centre cell of its footprint (`Town.SightCenter`), radius
  **10**: 7 cells past its walls on every side. Mines, providers and dwellings keep radius 2 from their entrance. In refog mode the
  town's sight is measured the same way.
- Games and replays now open more of the map at the start and when a town is taken; no format change.

### ADR-142: Scouting reveals information, built (ADR-125, 2026-11-06)
**Status**: implemented, core tested (`ScoutingIntelTests`); the game app's right-click not yet run in Godot.

- **Core**: `AdventureIntel.ScoutHero`, `ScoutMonster`, `ScoutStacks` (guards, map garrisons), `ScoutTown` and `ScoutingAt`; `ShownStack` now
  carries the range of its amount word (`Amount` reads "a pack (10–19)", `Estimate` the middle) and `ShownArmy.Details` (`ScoutedHero`)
  what Scouting tells of a hero. The information spells keep their own exact queries (`ShowHero`, `Town`, `ArmiesAround` …).
- **Where ADR-125 left a choice** *(proposal)*: a hero "sees" an army when the army's cell (any cell of a town) is within its sight circle,
  so a town's or mine's sight shows only words; another player's map garrison counts as a garrison (Advanced), a neutral one as a guard
  (Basic); an ally's armies are not the player's own, so they show as words too; a Scouting town view shows no buildings; the stats shown
  include what artifacts add; a word for a remembered army under the fog is read from the army as it is now.
- **Screens**: right-click on another player's hero or a neutral stack in sight (status line), on a town not the player's own (the intel
  panel, titled "Scouting"); every hidden count shows its word with its range, Eavesdrop and the other spells' windows included.
- **AI**: reads monsters, guards, bank and box guards, map garrisons, enemy town garrisons and enemy heroes' armies through the same query,
  each hidden count as the middle of its range (a legion as 1000), for its targets, threats and simulated fights. *Not done*: it still
  reads an enemy hero's Attack and Defense for the simulated fight whatever its Scouting.
- **Effect**: an AI without Scouting overrates an army at the bottom of its word's range (10 footmen read as 14) and fears an equal
  enemy; AI tests whose enemy hero only stands in give their own hero 12 footmen (`TestWorld.Steady`). Replays of games with AI players
  may differ.

### ADR-143: The Academy's Observatory; the Undead Transformer built (ADR-137, ADR-138, 2026-11-06)
**Status**: implemented, core tested (`UndeadTransformerTests`); the town screen's Transformer section not yet run in Godot. The Observatory is
the owner's note in [ideas.md](ideas.md) ("academy: observatory - sets town's scouting radius to 24"); points marked *(proposal)* are the
agent's choices.

- **Observatory** (Academy, `observatory` in `buildings.csv`): an Academy town with it sees **24** cells from its centre instead of 10
  (ADR-141), from the moment it is built. *(proposal)* 2500 gold, 5 ore, 5 mercury, needs mage guild 1; a second unique building of the
  Academy beside the Hall of Records, with no visit or siege bonus. A town's sight never reaches its hero's Scouting (ADR-142), so it shows
  armies as words.
- **Undead Transformer** (Necropolis, `undead-transformer`): ADR-137's rules, 1000 gold + 5 mercury, needs a tier-1 dwelling.
  `TransformCreaturesCommand` lists the stacks of the garrison and the visiting hero (army, slot, and the line for a creature without a class)
  and turns them all or none. *(proposal)* Only the Necropolis's own tier 1–2 undead swap lines; other undead (and tier 3+ Necropolis) are
  refused. A result joins a stack of the same creature in its army, else takes the old slot. Event `CreaturesTransformed`; the replay name is
  `transformCreatures`.
- **Town screen**: when built, a toggle per stack it can take ("10 Footman → 10 Skeleton"; two toggles, one per line, for a creature without a
  class) and a "Transform" button for the chosen ones.
- **AI** *(proposal for "every stack it does not need")*: in each army of its town with the Transformer it turns all living stacks into
  undead when the army is already mostly undead by worth (creature value × count), elsewhere only those whose undead are worth at least as
  much; a creature without a class becomes the more valuable line; it never swaps lines. It builds both buildings after the unique building.

### ADR-144: The town's own look on the siege field (ADR-106, ADR-109, 2026-11-06)
**Status**: implemented; seen in Godot (Haven, Academy, Necropolis, Inferno sieges from the siege smoke map). Shapes and colours are the
agent's placeholders until the town models (Phase 5 presentation).

- **Core**: `SiegeSpec` carries the town type (`SiegeRules.SpecOf` sets it; estimates may leave it null), and `BattleStarted`'s
  `SiegeLayout.Town` passes it to the views. It changes no rule.
- **Look** (`SiegeLook` in Godot.Shared, one row per town type, a grey default for an unknown one): the colour of the stone (walls, keep,
  towers), the gate, the roofs and their shape (Haven, Sylvan, Swamp cones; Academy, Conflux domes; Dungeon, Dragon Utopia pyramids;
  Necropolis, Inferno spikes; Stronghold battlements on a log palisade), the moat (Inferno lava and Conflux's shimmer glow) and the yard
  behind the walls, tinted over the battle terrain.
- **3D field**: walls carry battlements, the gate a lintel of the town's stone, keep and towers the town's roof; a fallen structure loses its
  roof with the rubble. Beyond the defenders' edge stand 7, 9 or 9 of the town's houses (Fort, Citadel, Castle) under the same roofs. The 2D
  board draws walls, gate and moat in the town's colours.

### ADR-145: Stack health bars in battle (ADR-118, 2026-11-06)
**Status**: implemented; seen in Godot (the siege smoke map: full, yellow and red bars).

- **Core**: `BattleUnitAttacked`, `BattleUnitSiegeDamaged` and `BattleUnitSpellDamaged` carry the stack's `TopHealth` after the hit, as
  `BattleUnitHealed` and `BattleUnitRevived` already did, so the view shows the bar as each hit plays, not the state ahead of it.
  `StackHealth` gives the bar's fraction and band (green when the top creature has over half its health, yellow over a fifth, red at a fifth
  and below). It changes no rule.
- **3D field**: a bar under the stack count, facing the camera and drawn over the field; it updates on hits, heals, revives and rebirth. The
  2D board draws the same bar along each stack's bottom edge. Sizes and colours are placeholders.

### ADR-146: Dividing cliffs on the battlefield (ADR-118, 2026-11-06)
**Status**: implemented; seen in Godot (the battle smoke map, with highlands and the chance forced for the check). Shape and height are
placeholders.

- **Rule**: on highlands, rough, rocky and subterranean ground one battle in three gets a cliff; never in a siege or a creature bank (Pandora's
  Boxes and map-event battles may have one). It stands in the middle of the open ground between the armies (the deployer's zone stays free).
  Half the time it is one piece of 6–12 cells with at least 4 open rows above and below; otherwise two pieces of 6–10 cells, up to two columns
  apart, with at least 4 rows between them and at least 4 more above or below. Each piece is 2 columns wide and runs down the field. Gaps of 4
  let a large creature through. Random obstacles are fewer (1–3 instead of 2–5) and keep 4 columns off the cliff, so they never close a gap.
- **Movement and shots**: walkers go round (the existing pathing); flyers and teleporters pass over. A shot across a cliff takes 1/4 cover once
  per piece, like a crag.
- **AI**: a walker on a field with a cliff advances along the steps round it to the nearest enemy (as attackers do in a siege) instead of
  by straight-line distance, which would park it against the cliff's face.
- **Code**: `ObstacleClass.Cliff`; `BattleObstacle` takes a `Length` (its rect is 2 × length); `BattleState.CliffsAllowed` (false for banks);
  `BattleSetup.PlaceCliff`. The 3D field draws a ridge (a prism) as long as the piece; the 2D board draws its rect like any obstacle.

### ADR-147: The Crown of the Waning Eye's best magic skill (ADR-099, ADR-111, 2026-11-06)
**Status**: implemented; rules only (the hero screen's artifact line names it).

- **Rule**: the Crown adds one level to the hero's best magic skill: the "magic" skill (which spell levels the hero can learn) of the school
  whose three skills have the most learned levels together. On a tie, the first school in the ring order (Sylvan, Haven, Academy, Dungeon,
  Necropolis, Inferno) counting from the hero's own town (its roster town, else its preferred creature's); a hero of a town without a school
  counts from Sylvan. Nothing while the hero has no school started; a skill at Master stays at Master. Only learned levels count, so other
  artifacts' skill levels do not move the choice. The level counts everywhere a skill level does (learning spells in a guild, the AI).
- **Code**: the effect word `best-magic-skill` (`ArtifactEffectKind.BestMagicSkill`); `Hero.BestMagicSkill()`; `Hero.SkillLevelOf` adds it.
  `ArtifactBonuses.HomeTown` keeps the hero's town for the tie, set by `GameState.RefreshArtifacts` (also refreshed when a placeholder
  becomes a roster hero). AI weight 8 (placeholder).

### ADR-148: The Godot check of what was built but never run: magic, sieges, the Grail, unique buildings, map sites, game flow (ADR-113, 2026-11-09)
**Status**: done; closes the "Open checks in Godot" list of Phase 3 apart from the items under "Still not seen".

Thirteen new smoke scenarios in `tools/smoke-godot.ps1` (run in Godot 4.7.2; frames under `.shots/`), their maps checked by `SmokeMapTests`:
`game-unique-buildings` and `game-stables-market` (`tools/smoke/unique-buildings.ewmap.json`), `game-map-sites` and `game-map-sites-red`
(`sites.ewmap.json`: ten sites in a row, a typed hero under each, the Sphinx's riddle fixed in the map), `game-grail` (`grail.ewmap.json`),
`game-magic` and `game-battle-magic` (`magic.ewmap.json`), `game-necromancy` (`necromancy.ewmap.json`), `game-siege-gate` (the siege map),
`game-siege-earthquake` (`siege-magic.ewmap.json`), `game-victory`, `editor-grail-and-town`, `editor-template-obelisks`.

- **Found and fixed**:
  - The battle HUD's log and action buttons sat off-screen in every battle: the HUD was anchored without its offsets. It now fills the screen
    (`SetAnchorsAndOffsetsPreset`), the actions box grows up and left above the menu button, and the button, warcry and spell rows wrap.
  - The University's panel was empty on a hero's first visit: it opened before the lessons were drawn. `ObjectServicePanel` now refreshes on
    `LessonsDrawn`.
  - The town screen did not come back after the Elder Grove's level-up; `TownPanel` refreshes on `DecisionResolved`.
  - Unique buildings and the Sphinx said nothing: the notice panel now tells what a unique building gave (`UniqueBuildingVisited`) and the
    Sphinx's verdict (`SphinxAnswered`); the Grail building notice names the town.
  - The hero list did not update movement after the Stables, a dug hole or the Sphinx; it refreshes on those events.
  - The puzzle map's parchment stretched to the window's width; it stays square.
  - The editor's Grail hint wrapped one letter per line (no minimum width), and the conditions panel's difficulty row showed nothing for
    "not set" (its id was -1, which an `OptionButton` treats as no item).
  - The game's status line printed raw enum names ("GuardEncountered", a `MovementOutcome` record); `Main.Describe` puts them in words.
- **Tooling**: `AutoInput` takes `press:<text>` (clicks the topmost visible, enabled button with that text or tooltip, exact then
  case-insensitive contains, scrolling it into view) and `dump` (prints every visible button with its centre).
- **Seen working**: the Undead Transformer section (choices and Transform); the Elder Grove and Stables with their notices; the trading screen;
  the University's and War Academy's lessons, the Arena's choice, Pandora's Box, the Witch Hut, the Sphinx, the Sacrificial Altar, the Artifact
  Merchant, the Refugee Camp and the Cartographer; the obelisk notice, the puzzle map (P), digging (G) both empty and on the Grail, the Grail
  building notice; spell ranks on the hero screen, the "Assemble" button of a relic, the adventure spells (Detect Magic's and Distant Vision's
  windows, View Terrain); the guild's spell book purchase and the guild spells fixed by the map; in battle Bless and Blind cast, the AI's
  Dispel, surrender, the deployment step with the tent and cart, the Necromancy screen; in a siege the ballista breaking the gate, the keep
  and towers shooting, Earthquake on units and walls; the victory screen with its score and "Save replay", the main menu; in the editor the
  conditions panel's hero limit and difficulty, the Grail tool, the town inspector's faction and guild spells, the template editor's obelisk
  count; the new-game screen's user templates.
- **Still not seen**: Resurrection; Grave Whisper, Ledger of Ages and Eavesdrop; the AI's buffs, curses and revives (only its Dispel came up);
  setting a guild spell by hand in the editor; the drawing of a dug hole on the map.
- An Escape pressed within about 0.6 s of a scripted click did not close the intel popup once; not tried by hand yet, possibly the scripted input.

### ADR-149: Field-zone battle spells: Consecrate, Thornwall, Bone Wall, Fire Wall, Darkness (ADR-099, ADR-122, ADR-123, 2026-11-09)
**Status**: implemented; *(proposal)* marks the agent's choices, for play-testing. Seen in Godot: Darkness cast and drawn
(`game-battle-zone`); the other four only in tests. The zones' looks are placeholders (translucent blocks in `ZoneLook`).

- **Grammar**: a new kind `zone` with `area N` (a square) or `line N [wide W]` (W columns, N cells down the field, centred on the aimed
  cell), the triggers `enter N`, `start N`, `leave N`, `beside N`, `heal N`, the flags `blocks`, `blocks-shots`, `own-undead`, `dims-shots`,
  `defense N` (own stacks in it), the filters and `spare-own` for whom its damage hits, and `rounds N` (at least 1). A zone counts as hostile,
  so creature spell resistance and the army's immunities count against it.
- **The spells** (spells.md's ranks): Consecrate `area 3 start 15 undead-demon spare-own` (II area 5, III also `heal 10`); Thornwall
  `line 5 blocks leave 20` (II line 7, III also `blocks-shots`); Bone Wall `line 5 blocks blocks-shots own-undead` (II line 7, III also
  `beside 20 living spare-own`); Fire Wall `line 5 enter 30 start 30` (II line 7, III `line 5 wide 3`, the 3 × 5 area); Darkness
  `area 5 dims-shots` (II area 7, III also `defense 2`). All last 3 rounds.
- **Aiming** *(proposal)*: one click, like an area spell; a line always runs down the field (across the armies' way), so no direction is
  chosen. The zone is clipped to the field and may be cast over stacks and obstacles. Several zones may stand at once.
- **Movement**: blocking cells keep walkers out, except the cells a stack already covers, so a stack caught in a wall can walk out
  (Thornwall then tears it). Flyers and teleporters pass over walls. `enter` hits a walker whose path (`BattleGrid.Path`, the placements along
  the path `Reachable` found) crosses the zone from outside it, and a flyer or teleporter only where it lands *(proposal)*. `leave` hits a
  stack that moves while standing in the zone. Move damage comes after the moat's.
- **Shots**: `blocks-shots` makes a shot impossible when its line of fire crosses a zone cell that is neither the shooter's nor the target's
  (ranged retaliation too). `dims-shots` halves a shot whose shooter or target stands in the zone, once even when both do.
- **Rounds**: a zone lasts the rest of the round it is cast in plus its rounds × (10 + Spell Power) / 10 (not cut by the enemy's Spell
  resistance, which is per stack), and goes as a round starts (`BattleZoneExpired`). As a round starts, after burning and regrowth, each zone
  in id order deals `start` to the stacks in it and `beside` to the stacks next to it that it hurts, and heals own creature stacks in it.
- **Amounts**: grown with Spell Power, the specialty and the orbs when cast, like burning; each hit less the enemy hero's Spell resistance and
  changed by the stack's spell-taken percent. Holy does not double Consecrate's 15 *(proposal: spells.md gives it as the number taken)*. War
  machines are never touched (as for other non-damage spells).
- **AI** *(proposal)*: a zone is worth one round of its damage to the stacks standing in or next to it now (Thornwall's tearing counted for
  stacks in it), enemies for, own against, plus its healing; it aims at the enemy stacks' centres. Blocking and darkening are not valued, so the
  AI casts the walls and Darkness only for damage. Moving stacks do not avoid fire or thorns.
- **Code**: `SpellEffectKind.Zone`, `ZoneEffect`, `SpellEffect.AreaHeight`; `BattleZone` and `BattleZones` (rules), `BattleState.Zones`;
  the engine's `MakeZone`, `TickZones` and `MoveUnit`; `BattleGrid.Path` and the clearance; `BattleZoneCreated` and `BattleZoneExpired`, drawn by
  the 3D field and the 2D board. Battles are not saved as state (saves replay commands), so no save format change; replays of battles where a
  hero knew one of these spells may now differ (the AI casts them).

### ADR-150: Control spells: Frenzy, Hypnotize, Puppet Master, and spells that last turns (ADR-099, ADR-122, ADR-123, 2026-11-09)

- **Grammar**: the statuses `frenzied` and `controlled` (a curse on one stack only), `turns N` instead of `rounds N` for a buff or curse (the
  stack's next N turns), and `health N` for a `controlled` spell (the most total health a stack may have to be taken; the cast is refused
  otherwise).
- **The spells** (spells.md's ranks): Frenzy `curse frenzied turns 1` (II 2 turns, III also `taken% 20`); Hypnotize
  `curse controlled health 300 turns 1` (II 450, III 2 turns); Puppet Master `curse controlled rounds 2` (II 3 rounds). **Not built**: Puppet
  Master III's split (up to 600 health comes over, the rest stays); rank III keeps rank II's effect. spells.md's "(not against its own hero's
  stacks once it would die)" is dropped as unclear *(proposal)*: the puppet fights its own side's stacks like any enemy.
- **Sides**: a stack has an `Owner` (the side it came with) and a `Side` (the side it fights for): the caster's while a `controlled` spell is on
  it, else the owner's. Everything in the battle goes by `Side`: who commands its turns (a person or the AI), who may cast during them, its
  friends and enemies, and the hero whose skills, morale, luck and artifacts count for it *(proposal, like HoMM3)*. The owner counts when the
  battle ends: survivors, casualties, experience, Necromancy, a guard's count and the surrender price; and a side stays in the battle while it
  owns a living creature stack, even one the enemy controls *(proposal)*. The control ends when the spell runs out, when a dispel takes it off
  (the owner's Unravel or anyone's Dispel can free it; the caster's Purge cannot), or when the stack dies (`BattleUnitSideChanged` each time).
- **Turns**: a spell that lasts turns lasts the stack's next N turns, played or passed (Blind, Stasis, Terror, bad morale); waiting does not use
  one, and a morale extra action comes after the turn that used it. N grows with Spell Power and is cut by the enemy's Spell resistance like
  rounds. Hypnotize's health limit grows with Spell Power like an amount *(proposal; HoMM3's limit grows too)*.
- **Frenzy**: the frenzied stack's turn is played by itself, for any side (like Confusion): it goes for the nearest other stack, friend or foe
  (the gap between footprints, then the id), not in Stasis or unseen by it; it shoots it when it can, else attacks it from the nearest
  placement, else walks to the reachable placement nearest it, else defends. Every stack is its foe that turn (it cannot shoot with any stack
  next to it; cleave and multi-head hit friends too); a friend it hits retaliates. Its hero does not cast during that turn.
- **AI** *(proposal)*: Frenzy is valued like the other control curses (10 % of the stack's value a turn), Hypnotize and Puppet Master at 20 %
  a turn or round; a controlled stack is played by its new side's AI like an own stack.
- **Code**: `SpellStatus.Frenzied` and `Controlled`, `SpellEffect.Turns` and `HealthLimit`; `BattleUnit.Owner`, `Side` from the spells,
  `ActiveSpell.TurnsLeft`, `BattleState.OwnedArmyOf`; `BattleMagic.Turns` and `HealthLimit`; the engine's `UseTurn` and `SideChanged`;
  `BattleGrid.IsFoe`; `BattleAi.ActFrenzied`; `BattleSpellApplied.Turns` and `BattleUnitSideChanged`, which turns the 3D stack (plate, nose,
  facing) and redraws the 2D board. No save format change (saves replay commands); replays of battles where a hero knew one of these spells
  may now differ.

### ADR-151: Two-click spells: Teleport, Shadow Step, Drain Life (ADR-099, ADR-122, ADR-149, 2026-11-09)

- **Grammar**: a new kind `move N|field [shadow] [again|again-attack]` (an own stack to a free cell within N cells or anywhere; `shadow` only
  where it is not next to an enemy stack; `again` / `again-attack` for rank III); for damage aimed at a stack, `drain% N` (that percent of the
  damage dealt heals an own stack the caster picks) and `drain-revive` (the healing also revives its dead).
- **The spells** (spells.md's ranks): Teleport `move 10` (II `move field`, III `move field again`); Shadow Step `move field shadow` (II
  `move field`: "anywhere" lifts the shadow rule, as rank I already reaches the whole field; III `move field again-attack`); Drain Life
  `damage 50 living drain% 50` (II `targets 2`, III also `drain-revive`).
- **Aiming**: `BattleCastCommand` gains `Second`. A move takes `Target` (the stack) and `Cell` (its footprint's top-left; the game centres the
  footprint on the clicked cell); a drain takes `Target` (the enemy) and `Second` (the own stack), which may be left out (nothing is healed).
  The battle HUD arms the spell, takes the stack on the first click and the cell or own stack on the second; Esc cancels.
- **Moves**: any living own creature stack, the active one too (it keeps its action, as for every hero spell); not a rooted stack, the keep's
  stack or a war machine; Stasis wards it off like every spell. The footprint must fit (inside the field, no obstacle, no stack, no wall zone
  for a walker), elsewhere than it stands. "Within 10" is the straight line between the top-left cells, with a flyer's 0.25 slack, not grown by
  Spell Power *(proposal)*. "In shadow" is read from spells.md's parenthetical: not next to an enemy stack (Darkness is not needed). The stack
  lands over everything between: a zone it leaves or lands in, and the moat, hurt it as they would a flyer (`BattleUnitTeleported`).
- **Rank III** *(proposal)*: "can still act / attack this round" means a stack that had already taken its turn this round gets one more, after
  the waiting stacks; it may not wait then. Teleport's is a whole turn; Shadow Step's is a turn without moving (it may attack what stands next
  to it, shoot or defend). A stack still to act, or the active one, gets nothing more (`BattleUnitTurnRegained`).
- **Drain**: the own stack (living, not a war machine, not in Stasis) gets half the damage dealt to all the stacks hit (the damage they really
  lost), as its top creature's wounds; at rank III as a revive, for good (like Vampirism) *(proposal)*. Undead and constructs are not hit
  (`living`), but any own stack may drink, undead too.
- **AI** *(proposal)*: a move only for the active stack, when it fights in melee, cannot shoot now and cannot reach an enemy, to a cell next to
  an enemy it may land on, worth the blow it then strikes (so Shadow Step I is never cast by the AI); a drain picks the own stack whose healing
  is worth most and adds that to the damage's value.
- **Code**: `SpellEffectKind.Move`, `SpellTurnAgain`, `SpellEffect.MoveRange`, `Shadow`, `Again`, `DrainPercent`, `DrainRevives`, `TwoClicks`;
  `BattleMagic.MoveRefusal`, `CanLand`, `DrainRefusal`, `Drained`, `DrainHeals`; `BattleUnit.StaysPut` (in `IsFixed`); the
  engine's `Teleport` and `Drain`; `BattleTargeting.SpellPlacement`; `BattleUnitTeleported` (the 3D stack sinks and rises at its new place, the
  2D board redraws) and `BattleUnitTurnRegained`; `BattleSpellCast.Second`. No save format change (saves replay commands; `Second` is optional).

### ADR-152: The last battle spells: Martyrdom, Mirror Image, Clockwork Precision, Raise Dead, Blood Price, Puppet Master III (ADR-099, ADR-122, ADR-150, ADR-151, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing. All 91 battle spells now work at every rank.

- **Grammar**: new kinds `copy rounds N [survives N]`, `raise N [once|each-round]` (no aim) and `sacrifice N per M`; for buffs `martyr% N`
  (another own stack, the second aim, takes that share of each blow meant for the stack) and `martyr-defense N` (for the martyr); the status
  `twice` (a second turn right after the first, once a round) and the filter `machine` (war machines only; they take any spell with it); for a
  `controlled` curse with `health N`, `split`.
- **The spells**: Martyrdom `buff martyr% 50 rounds 3` (II 100 %, III also `martyr-defense 2`); Mirror Image `copy rounds 2` (II 3 rounds,
  III `survives 1`); Clockwork Precision `buff melee% 50 ranged% 50 twice rounds 2 allies machine` (II 3 rounds, III also `true-shot`: "ignores
  cover" given to every own machine, only the offensive one shoots); Raise Dead `raise 15` (II `once`, III `each-round`); Blood Price
  `sacrifice 10 per 5` (II per 4, III per 3); Puppet Master III `curse controlled rounds 3 health 600 split`.
- **Martyrdom**: aimed with two clicks (ADR-151), the guarded stack, then the martyr (another living own creature stack, not a copy). A *blow*
  is an attack, a damage spell, a field zone's damage, a tower's shot or the moat; the martyr takes its share after the blow (it does not pass it
  on again), and none while it is in Stasis. Final Reckoning, decay (Plague, Pact of Ash) and Blood Price are losses, not blows *(proposal)*.
  The link ends with the spell or when either stack dies.
- **Mirror Image** *(proposal for the open points)*: the copy has the stack's count, its top creature's health and its shots, none of its spells
  or casts; it stands on the free placement nearest the stack, for the caster's side, and acts from the next round. It is summoned (gone after the
  battle, worth no experience, nobody's loss) and does not keep a side in the battle. Any blow it takes uses a hit, after the damage: it vanishes
  when none is left (rank III: the second blow), or when its rounds (lengthened by Spell Power) end. No copy of a copy, nor of a war machine.
- **Clockwork Precision**: the second turn comes right after the machine's turn, like good morale's extra action, and not after defending.
- **Raise Dead** *(proposal)*: cast with no aim, once a battle per side. After the battle, when its hero wins against a defeated enemy,
  skeletons worth 15 % of the health of the enemy creatures slain (those Necromancy counts, not the stacks it raised in the battle; the
  percent is not raised by Spell Power) join the army with Necromancy's undead, reported in the same `UndeadRaised`. "From a destroyed stack"
  (II) and "each round" (III): when cast, and then as each round starts, the destroyed stack worth most health (either side's, not a war machine,
  summoned stack, copy or raised skeletons, nor constructs or elementals) rises on its place as a new stack of skeletons worth 15 % of its
  starting health, at least 1, for the caster; rank II raises one in all (the first chance), rank III one a round. Raised skeletons act from the
  next round and join the army whenever their side's hero wins, however the battle ended.
- **Blood Price**: an own creature stack (not a copy) loses 10 % of its creatures, at least 1, the wounded top creature first; the hero gains
  the health lost / 5 (4, 3) in mana at once, rounded down, with no cap in battle *(proposal)*. Mana left above what the hero brought is lost
  when the battle ends (the rule since ADR-110).
- **Puppet Master III**: a stack within 600 health (lengthened by Spell Power, like Hypnotize's limit) is taken whole; a bigger one is split:
  as many full-health creatures as the limit holds leave it as a stack of their own on the nearest free placement, with its spells and shots,
  and come under control; the rest stays on its side. The part comes from the same army slot (its starting count is moved over too), so the
  creatures go back together after the battle. It acts from the next round. With no room to split, or not one creature within the limit, the
  spell is refused.
- **AI** *(proposal)*: Martyrdom is valued like a buff on the guarded stack, with the other own stack with the most health as the martyr; Mirror
  Image at 10 % of the stack's value a round; Clockwork Precision like a buff on each machine; Raise Dead at the skeletons the enemy's slain
  would give now, plus the stack rising now (so not before something dies); Puppet Master III by the part it takes. It never casts Blood Price.
- **Code**: `SpellEffectKind.Copy`, `Raise`, `Sacrifice`, `SpellRises`, `SpellStatus.Twice`, `SpellFilter.Machine`, `SpellModifiers.MartyrPercent`;
  `BattleUnit.IsCopy` / `CopyHits` / `IsRaised` / `TookSecondTurn`, `InitialCount` settable for a split; `ActiveSpell.Martyr` / `IsMartyr`;
  `BattleState.Raising` / `Risen`; the engine's `Take` (blows, the martyr's share), `Mirror`, `Rise`, `SplitOff`; `BattleService`'s
  `JoinUndead` adds Raise Dead's skeletons. Events `BattleUnitCopied`, `BattleCopyVanished`, `BattleUnitShielded`, `BattleManaGained`,
  `BattleUnitActsAgain`, `BattleUnitRaised`, `BattleUnitSplit` (the 3D view adds the new stacks and narrates; the 2D board redraws). Smoke
  scenario `game-battle-mirror`. No save format change.

### ADR-153: The artifact exchange screen (ADR-061, ADR-070, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Screen**: the exchange of two own heroes covers the map like the hero screen: the two heroes side by side, each with its total primary
  stats, seven army slots, war machines (a button gives one), paper doll (12 slots), assembled relics ready to join, and backpack (with a count;
  copies of one artifact show once with ×N). Escape drops what is picked, else closes the exchange.
- **Artifacts** *(proposal: clicks, not drag and drop, which stays open)*: click an artifact, worn or carried, of either hero, then where it
  goes: a slot of the other hero's paper doll (any slot; the artifact goes to its own kind) gives it and puts it on, the other's backpack
  ("Into the backpack" or any carried artifact) gives it to carry; the own paper doll puts a carried one on, the own backpack takes a worn one
  off. Clicking another artifact of the same hero picks that one instead.
- **Rule**: `GiveArtifactCommand` gains `Worn` (give the worn copy, not one carried with the same id) and `Wear` (the receiver puts it on, as
  `EquipArtifactCommand` would: the one worn in a full slot goes to its backpack, and an assembled relic needs the slots it blocks free). The
  receiver's backpack needs room only for what ends up in it. Everything is checked first, so a refused give changes nothing. Events: the
  receiver's `ArtifactUnequipped` (the swapped one) and `ArtifactEquipped`, then `ArtifactGiven`. Replays without the two fields load as before.
- **Code**: `ArtifactService.Give` and the shared `Fit` / `PutOn` (also used by `Equip`); `ExchangePanel` rebuilt, on the game app's screen
  layer. The smoke scenario `game-exchange-and-gate` gives Orrin's Rusted Blade onto Valeska's doll (her Centaur's Axe goes to her backpack),
  hands over the Iron Band to carry and moves a stack before closing. No save format change.

### ADR-154: Right-click information windows and "Detailed tooltips" (ADR-118 UI-3 c–d, ADR-125, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Window**: a right click opens one panel beside the pointer, above every screen: a title, what kind of thing it is, then its lines.
  *(proposal)* Holding the right button and letting go after 0.3 s closes it again (read while held); a quick click leaves it open until the
  next click or key. A left click or Escape that closes it does nothing else; another right click goes on to open the next window. The hover
  tooltips stay as they were.
- **What has one**: on the map a hero, a neutral stack, a town, any other object and open ground (terrain, road, river); in battle a stack
  (its count, top creature's health, Attack and Defense with what its hero, terrain and spells add, speed, initiative, morale, luck, shots,
  whether it retaliated, its rules in words and the spells on it); on the hero screen the army, the skills, the known spells (now one button
  each), the adventure spells and the artifacts; on the exchange screen the two heroes, their stacks and artifacts; on the town screen the
  buildings (every level with its cost, needs and income, what is built), the dwellings, the recruit buttons and the stacks; in battle the
  hero's and the stack's spell buttons.
- **What the player may know**: another player's hero, a neutral stack and a town show what the viewer's Scouting shows (ADR-125); an object
  under the fog shows as last seen (*"Last seen here; it may have changed since"*), an unexplored cell has no window. A town of the player's
  own lists its buildings; an object given once per hero or to its first visitor says which of the player's heroes took it.
- **"Detailed tooltips"** (off by default): a checkbox in the F10 menu, kept in `settings.json` beside the speeds (a new `interface` section;
  a file without it reads as off). It adds the formulas and the raw numbers: a creature's strength and map speed, the damage formula in
  battle, a spell's effect at each rank with the amount at the hero's Spell Power, an artifact's effects, a hero's base stats, ids.
- **Code**: core `Info/InfoSheets` (`InfoSheet` records, pure functions of the state: `Creature`, `BattleStack`, `Hero`, `Skill`, `Spell`,
  `Artifact`, `Building`, `Dwelling`, `MapCell`, `Town`); `DamageCalculator.AttackOf` / `DefenseOf` (what `Damage` already added, now shared);
  `InterfaceSettings`, `SettingsFile.LoadInterface`. Godot `InfoWindow` (a canvas layer) and `control.WithInfo(detailed => sheet)`. The map's
  right click (which showed a scouted army on the status line and an enemy town in the intel panel) now opens the window. Tests
  `InfoSheetTests`, `SettingsFileTests`; smoke scenario `game-info-windows`. No save format change.
- **Not done**: a long press on touch; windows for the kingdom overview, the tavern's heroes and the decision prompts.

### ADR-155: Drag and drop in the deployment step (ADR-072, ADR-076, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Drag**: in the deployment step the player presses on an own stack, drags it and lets go where it should stand. While it is dragged the
  stack's model follows the pointer, lifted a little, and the placement preview under it is green where it may stand and red where not.
  Letting go places it (the same `BattleDeployCommand` and placement, `BattleTargeting.DeploymentPlacement`, as a click); dropped where it
  may not stand, it goes back and the refusal shows on the status line; dropped off the field or on its own cells it goes back silently.
  Escape while dragging puts it back. The release counts even over the HUD.
- **Click to place stays**: a press without a drag *(proposal: under 6 px of pointer travel)* picks the stack, and the next click on a cell
  puts it there. A press on the picked stack itself now starts a drag instead of placing it on its own cells.
- **No swap** *(proposal)*: dropping a stack on another own stack is refused like any occupied cell; swapping places would be a rule change in
  `BattleSetup.DeploymentRefusal`, not done.
- **Code**: Godot `BattleView3D.Carry` (the picked model under a field point, or back); the game app maps press, motion and release
  (`DeployAt`, `DragDeploy`, `DropDeploy`). `AutoInput`'s scripted `drag` now reports the held button on its motion. No core change, no
  save format change; smoke scenario `game-deploy-drag` (a stack dragged down the zone, another dropped outside it and refused).
- **Not done**: the 2D harness keeps click to place; touch drag.

### ADR-156: The configurable layout and the "Layout" tab (ADR-118 UI-3, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **The tab**: the F10 menu now has three tabs, "Save and load", "Settings" (auto combat, the speeds, "Detailed tooltips") and "Layout".
  Close and "Main menu" stay below the tabs. The layout choices: the battle's initiative bar (top, under the title, or bottom), the
  minimap's corner (any of four), the hero and town lists (beside the minimap, or along the bottom) and the action panel's side (right or
  left), plus "Default layout". A change shows at once, on the map and in a running battle.
- **Kept per computer**: `LayoutSettings` in the `layout` section of `settings.json`, beside the speeds and the interface settings. It is
  not in saves and changes no rule. A file without the section gives the default, which is the layout before this ADR.
- **Where the parts go** *(proposal)*, `HudArrangement`: each corner holds a stack of parts, from the corner outwards. The minimap (with
  "Switch level") is nearest its corner. Lists "beside the minimap" follow it in its stack, as in HoMM. Lists "along the bottom" run as rows
  between the bottom stacks, with narrower entries. The action panel (kingdom, quests, puzzle map and dig, marketplace) stands at the top
  of its side, after the minimap and lists if they are there. The turn, resources and status block does not move: it stays at the top left,
  beside the top-left stack. The bottom right leaves room for the menu button. With the initiative bar at the bottom, the battle log and
  the actions move up above it.
- **Code**: core `LayoutSettings` and `HudArrangement` (Domain) and `SettingsFile.LoadLayout` / `ReadLayout` (Data), with tests. Godot
  `AdventureHudLayout` is a full-screen container that lets the mouse through. It places the parts and moves them without reparenting,
  because a part's event subscriptions last only while it is in the tree. `HeroListPanel` and `TownListPanel` have a `Horizontal` form,
  `BattleHud` has `Layout`, and `GameMenuPanel` has its tabs and `LayoutChanged`. No save format change. Smoke scenario `game-layout`;
  `game-auto-combat` and `game-speed-settings` now open the "Settings" tab.
- **Not done**: the spacing pass (ADR-118 b); a layout for the 2D harness and the map editor.

### ADR-157: More scenery on random maps, denser by terrain (ADR-077, 2026-11-09)
**Status**: implemented (2026-11-09). Random map generator version **9** (every generated map changes). Numbers are placeholders.

- **How many clumps** *(choice)*: the obstacle densities give 4 / 10 / 20 clumps per 1000 tiles (were 3 / 8 / 16), and each tile counts by
  its own terrain (`SceneryDensity.Clumps`): forest floor 250 %, highlands 175 %, rough, swamp and rocky ground 150 %, grass, snow and
  subterranean 125 %, dirt, wasteland and cursed lands 100 %, lava 75 %, sand, beach, ice and the rest 50 %. Mirrored zones keep their images,
  since their tiles and terrains are images too.
- **How large** *(choice)*: a clump aims at 1–6 tiles (was 2–6) plus its start tile's `SceneryDensity.ExtraSize`: forest floor 4, highlands
  2, rough, swamp, rocky ground, grass, snow and subterranean 1. The other clump rules stay: three tiles from the walls, one apart, dropped if
  it would cut off a part of the zone.
- **What fills them** *(choice)*: `SceneryPlacer` draws a kind among those with a fitting piece by `SceneryPlacer.Weight`, then that kind's
  largest fitting piece (before: the largest piece of any kind, which gave many castle walls). Trees 24 on forest floor, 10 on grass,
  highlands and snow, 6 elsewhere; rocks 16 on rough, highlands, rocky ground, lava, subterranean and snow, 6 elsewhere; dead trees 12 on
  wasteland, cursed lands and swamp, 4 elsewhere; mushrooms 6 underground and in swamps, 2 elsewhere; bushes and mounds 3; walls and ruins
  1; the rest 2. On six sample maps (duel and 2v2 at 96, seeds 1–3) scenery went from 289 to 795 pieces, trees and rocks first; walls from 74 to 7.
- **Treasure keeps room for its guard**: in a zone with monsters, every treasure item (not only map sites) is placed where a free cell by its
  entrance is left for the guard (`SettlementPlacer.Find`'s `accept`), since thicker scenery could leave none.
- Tests: `TerrainPainterTests.Obstacles_AreDenserOnWoodedAndBrokenGround_ThanOnSand`, `SceneryTests.TheGenerator_FillsAZone_MostlyWithItsTerrainsOwnScenery`.
- **Not done**: scenery art (the pieces are still placeholder boxes); scenery outside clumps (single trees along roads, forest edges).

### ADR-158: The arena's core rules (ADR-097, ADR-100, ARN-1, ARN-2, 2026-11-09)
**Status**: implemented (2026-11-09): the roadmap's arena "Core" item. Map format **v38**. The game app, the AI's preparation, the editor and
the shipped maps are the arena's other roadmap items.

- **The switch**: `ScenarioRules.Arena` (`rules.arena` in the map, omitted = off). On an arena map `GameFlowService` checks nothing but the
  arena battle: the victory and defeat conditions, the last team standing and the elimination do not count, so nobody is out before the
  battle *(choice: a player who loses its heroes on day 1 stays in and has "no hero" on day 2)*. The conditions stay in the rules; the editor
  item decides whether the switch clears them. Never ranked (`ScoreRules.IsRanked`).
- **Combat place**: a new object kind, `combat-place` (`BV`: the attacker from the west, as the battlefield puts it on the left) and
  `combat-place.vertical` (`B` over `V`: from the north). The entrance is the defender's cell, the blocked cell the attacker's
  (`CombatPlace.AttackCell`), so no hero reaches it on day 1. A visit does nothing and routes go through it. Map document `combatPlace`.
- **Day 1** (`ArenaRules.IsPreparationDay`): `MovementAllowance.Daily(state, hero)` gives **1 000 000** movement points ("nearly unlimited";
  every hero's daily allowance goes through it now, hires and freed prisoners too; a garrison hero gets them as well), and
  `TownRules.HasBuiltToday` lifts the one-building-a-day rule (the rule, the building tree, the AI and the town screen use it). **Starting
  resources** are the usual ones (the complexity, or the map's own with "Map default") *(assumed: ARN-1 d's "the map's starting resources"
  as against "everything costs nothing")*.
- **Day 2**: when the calendar reaches day 2, `TurnService` calls `ArenaService.StartBattle` after `DayStarted` and starts no turn (no income,
  no week or map events). `ArenaRules.Plan` follows ADR-097: the defender is the first player in turn order with a hero on a combat place's
  entrance (that place is the battlefield), the attacker the first player in turn order who is its enemy; without one, the first player
  attacks the next enemy at the first combat place (lowest id). Each team's **champion** is its strongest hero by army value (Σ count ×
  creature value, ties to the lower hero id), a hero on the map first, else a garrison hero, which comes out with the garrison as its army.
  The defender is moved to the entrance, the attacker to the attack cell (`HeroTeleported`; a hero already there swaps places), then
  `ArenaChampionsChosen`, then an ordinary hero battle on that cell's terrain (`BattleService.BeginArenaBattle`: no siege, no ambush). With no
  combat place the heroes stay and fight where the defender stands *(choice; the validator item makes such a map an error)*.
- **The battle**: no experience; the computer never retreats or surrenders in it (the AI item's "never leaves", one check). Its result goes
  into `GameState.Arena` (`ArenaBattle`: place, sides, champions, `ArenaResult`) when it finishes: the winner, or a **draw** when both sides'
  creatures are all dead (the engine still names a winner then). A retreat or surrender is an ordinary loss. A side whose champion has no army
  loses without a battle; both without armies is a draw. No enemy side, or a side without any hero: no battle (`NoBattle`).
- **The end**: `GameFlowService` ends the game from `GameState.Arena`: the winner's team wins ("Blue won the arena battle."); a draw or no
  battle ends it with no winner. `GameStateDigest` writes the arena line only once it is chosen, so older replays keep their digests.
- Tests: `ArenaTests` (movement, building, the hero choice, swapping, garrison champions, retreat, draw, no hero, no army, ignored
  conditions, the AI not retreating), `ArenaFormatTests` (v38 round trip, v37 loads).
- **Not done** (the arena's other roadmap items): the new-game screen and the battle screen showing both armies, the result screen, the AI's
  day-1 preparation, the editor's switch and combat place tools, the validator, objects showing their contents on day 1, the shipped maps.
  Combat places are drawn as plain coloured boxes.

### ADR-159: The arena in the game app (ADR-097, ADR-100, ADR-158, ARN-1, 2026-11-09)
**Status**: implemented (2026-11-09): the roadmap's arena "Game app" item.

- **New-game screen**: an arena map (any map with the switch: `--map`, the editor's maps folder, later the shipped ones) shows one line on
  what the mode is under the map's name, and each player's "Controlled by" is a Human / Computer choice instead of the map's fixed label:
  hot-seat (two humans) or against the computer. It goes through `GameState.AssignControllers` (game setup, like `AssignTeams`: refused once
  started, for unknown players, and when nobody would be a person). Other maps keep the map's controllers *(choice: the roadmap asked it for
  the arena only)*.
- **The battle starts by itself**: nothing new was needed: the battle scene opens on any `BattleStarted`, and shows every unit of both sides
  with its count (ARN-1 f: "the battle screen shows both armies"). The status line says "Day 2: the arena battle. Orrin (Blue) attacked
  Caelwyn (Red)." from `ArenaChampionsChosen`; `ArenaBattle.Summary` keeps that sentence in the core because a defeated hero leaves the game.
  The turn panel refreshes on `DayStarted` too, since day 2 starts no turn.
- **Result screen**: the reason, who attacked whom, "Arena maps are not ranked." (no ranking rows, ADR-158), **"Play again"** and the usual
  "Save replay". "Play again" rebuilds the game's start map (`_initialMap`, so the same players, controllers and teams) with the same options
  (factions, fog, complexity, timer) and a **new seed** *(choice: a rematch with its own luck, not the same rolls)*; only arena maps offer it.
- **Fix**: a game begun from inside another ("Play again", and a load during a game) made no camera current, since the old game's camera
  was still current until it was freed: the map stayed grey. `BeginGame` now makes the new camera current.
- Smoke: `tools/smoke/arena.ewmap.json` (Blue and Red behind a rock wall, the combat place in a closed pocket) and the `game-arena` scenario
  (end day 1, the computer prepares, quick combat, the result screen, "Play again"); `SmokeMapTests` plays the map against the AI and checks the
  hot-seat setup. Hot-seat was checked in the core only; a battle between two people on one screen is the existing hot-seat battle.

### ADR-160: The adventure AI on an arena map's preparation day (ADR-097, ADR-158, 2026-11-09)
**Status**: implemented (2026-11-09): the roadmap's arena "AI" item. Only the preparation day changes (`ArenaRules.IsPreparationDay`);
every other game plays as before (the self-test digest is unchanged). Numbers are placeholders.

- **Never leaves the battle**: done in ADR-158 (no AI retreat in the arena battle; the AI never surrenders anywhere).
- **Towns** (`AdventureAi.Arena.cs`, `PrepareTown`): nothing is saved for tomorrow. A town first buys what waits, then builds step by step
  (any number a day): of every dwelling it lacks and every dwelling upgrade, with the buildings each needs first (the requirement chain,
  worked out from the building table), it takes the plan with the most **army gained per gold**, counting a new dwelling only when the town
  can still buy at least half of its first week (a new dwelling opens with one, ADR-066), and an upgrade by the creatures waiting and in its
  armies; it buys after each step. Income, growth, caravans, the marketplace and the like are worth nothing on the last day. Then upgrades,
  war machines, and **a hero hired with what is left** (more visits that day, and its starter stack), never once the armies are being gathered.
- **Heroes**: up to 64 moves each (movement does not stop them). Targets that pay only on later days are skipped (`Target.Later`: mines,
  deposits, empty providers, observatories, obelisks and the Grail, town gates, keymaster tents not known to bar the way, neutral
  garrisons, prisons, whose hero brings no army); flagging a map dwelling is worth nothing, buying there takes all the gold. Fights are
  taken only when the simulated loss stays under **15 %** (40 % otherwise). No **danger** is seen (the enemy's day-1 reach would cover the
  map; the map keeps the players apart, ARN-1 c), and the main hero does not hunt for enemy towns.
- **No contact with the enemy**: enemy heroes and enemy-owned towns are not targets on the preparation day *(choice)*: beating the enemy's
  only hero leaves nobody for the battle, a loss for both (ADR-158), and taking its town brings its heroes onto ours. Found on Ember March
  made an arena (its players can meet on day 1): without it nobody fought on day 2 in three runs of three; with it the battle was fought in
  all three.
- **Gathering** (`GatherForBattle`, before the turn ends): the strongest hero by `ArenaRules.ArmyValue` becomes the main hero, walks to every
  own town it can reach (buying and building there straight into its army, and taking the **whole** garrison: nobody attacks a town before
  the battle), then every other hero with something to give walks to it and hands over its troops (all but one creature) and war machines
  (`Exchange`). That hero is the one `ArenaRules.Champion` sends. On the arena smoke map the computer's champion went from its starting two
  stacks to five.
- Tests: `ArenaAiTests` (several buildings and the gold spent, mines skipped though taken on an ordinary map, one army at the end of the day,
  the battle fought to the end on day 2, a weaker enemy hero in reach left alone though attacked on an ordinary map).
- **Not done**: the AI casts no adventure spells on day 1 and does not trade at the marketplace (as elsewhere, ADR-067); it does not pick
  the combat place to stand on (the defender's side); it does not weigh Pandora's Box contents shown on day 1 (that showing is the editor
  item's).

### ADR-161: The arena in the map editor; what objects show on day 1 (ADR-097, ADR-100, ADR-158, ARN-2, 2026-11-09)
**Status**: implemented (2026-11-09): the roadmap's arena "Editor" item. No map format change (the switch and the combat place are v38,
ADR-158).

- **The switch**: an "Arena" tick box at the top of the Conditions tab (`ScenarioRules.Arena` through `MapEditorSession.SetScenarioRules`,
  so undoable). While it is on, the last team standing, the elimination, the grace period and "Add condition" are greyed out and a line says
  the arena ignores the conditions; the conditions stay stored *(choice: switching back restores them; ADR-158 left whether the switch
  clears them to this item)*. The hero limit, week events and difficulty stay editable.
- **Combat places**: placed with the Object tool like any object ("Combat place", "Combat place (vertical)"); the inspector says where the
  defender and the attacker stand and that a hero on the entrance at the end of day 1 makes its owner the defender.
- **Validator** (`MapValidator.Arena.cs`):
  - an **error** for an arena map without a combat place (ARN-2 e), and for a combat place whose attack cell is on impassable ground (no
    battle can be fought there);
  - a **warning** when two enemies **can meet on day 1**: each player's day-1 reach is a flood fill from its heroes and its towns'
    entrances over passable, unblocked cells, through gates (`DayOneReach`); monsters, garrisons, border and quest barriers count as
    passable *(choice: a hero may beat or open them that day)*, so a map keeps its players apart with impassable ground or blocking
    objects. Allies may meet;
  - a **warning** for a **map event that does something only after day 1** (`MapEventChecks.RunsOnlyAfter`): every top action is an "if"
    without an "else" whose condition the date alone makes false on day 1 (day, day of the week, week, month compared with numbers, so a
    timed event's later first day); conditions that depend on more than the date are not guessed, quests are not checked;
  - a warning for victory or defeat conditions on an arena map (ignored), and for fewer than two teams with a hero or a town; the ordinary
    "fewer than two teams" check is skipped on arena maps;
  - a **warning** for a combat place on a map that is not an arena (it does nothing).
- **Contents on day 1** (ARN-2 c, `AdventureIntel.ArenaContents`): on an arena map's preparation day the right-click sheet shows what a
  **Pandora's Box** holds (and its guard), a **Shrine**'s spell, a **Witch Hut**'s two skills, and a **quest** object's current stage (its
  proposal, what it wants, what it then takes and gives); the box's "Open it?" prompt names the contents too. Other days and other maps
  keep them hidden as before. Scrolls and universities (ADR-097's list) do not exist yet.
- Tests: `ArenaEditorTests` (no combat place, a combat place on an ordinary map, meeting and walls, gates, allies, later events and what
  is not guessed, ignored conditions, the undoable switch, the contents shown on day 1 and hidden elsewhere); `SmokeMapTests` (the arena
  smoke map passes). Smoke: `editor-arena` (the switch, unticked, the validator's warning).
- **Not done**: the computer does not yet weigh the contents shown (it knows them anyway, ADR-100); map event messages are still shown on
  the arena (ADR-097's "not shown" for players and towns); the shipped arena maps.

### ADR-162: Drag and drop of artifacts on the hero and exchange screens (ADR-050, ADR-153, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Drag**: on the hero screen and the exchange screen a worn or carried artifact is pressed, dragged and let go where it goes; a label
  with its name follows the pointer. The places are the same as the clicks' (ADR-153): a slot of the other hero's paper doll gives it and
  puts it on, the other's backpack gives it to carry, the own doll puts a carried one on, the own backpack takes a worn one off. Godot's
  control drag: a press that moves a few pixels starts it, a press without moving stays a click; letting go over a place that would
  change nothing (back where it came from) or off the places does nothing, and the pointer shows "no drop" there.
- **Places** *(proposal)*: any slot of a paper doll, empty or not (the artifact goes to its own kind, as with clicks; not only the
  matching slot as in HoMM3), the doll's free space, every backpack button, "Into the backpack" and the backpack's free space. Clicks stay
  as they were; a drag drops what a click had picked.
- **Code**: Godot `ArtifactDrag` (`WithArtifactDrag` on a control, `Accepts`, `MoveCommand`: the give, equip or unequip command a move
  is); `HeroPanel` and `ExchangePanel` use it, and the exchange's clicks now share `MoveCommand`. No core change, no save format change.
  Smoke scenario `game-artifact-drag` (exchange: onto the other's empty slot, own backpack onto own doll, onto the other's backpack's free
  space; hero screen: onto the doll, into the backpack).
- **Not done**: dragging army stacks (still click, then slot); dropping on a slot of the wrong kind is not refused; touch drag.

### ADR-163: The spacing pass, the main menu's picture and small usability fixes (ADR-101, ADR-118, UI-3 b, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Spacing** (UI-3 b): one set of numbers, `UiSpacing`, as theme constants, with Godot's default fonts, colours and boxes: 4 px between
  slots and grid cells, 6 between rows, 8 between controls side by side, 16 above a section's heading, 8 inside a panel, 16 inside a
  screen or window over the map, 8 from the window's edge *(proposal: the numbers)*. Each app merges them into Godot's default theme at
  start (`UiSpacing.Apply`): a theme on the root window does not reach controls under a `CanvasLayer`, where every HUD part and screen
  is. Panels lose their own separation numbers; the HUD's corners, the battle HUD, the turn timer, the information window, the menu
  button and the editor's and harness's panels place themselves with `UiSpacing.ScreenEdge`. Section headings (`AddHeading`) on the
  hero, exchange and town screens, the F10 menu's speed settings and the editor's inspector and conditions panels.
- **Main menu picture**: the reference prototype's main-menu picture (`reference/ew`, the owner's), saved as
  `textures/ui/backgrounds/main-menu.jpg` (JPEG, 0.4 MB instead of a 2.9 MB PNG, for the LFS quota), covers the window behind the main
  menu, the new-game screen and the leaderboard (`MenuBackground`: cropped to fit, never stretched, darkened by a quarter
  *(proposal)*). Those screens stand in dark panels that let a little of it through (`Screens.UseMenuPanel`); the main menu is a column
  on the left, as in the prototype.
- **Usability** *(proposal: which ones)*:
  - **Continue** heads the main menu: it loads the newest save (autosave or named; its tooltip names it) and has the focus; without a
    save it is disabled and New game has the focus. A save that fails to load leaves the F10 menu open with the reason.
  - The "Menu (F10)" button shows only in a game; on the screens before a game it covered nothing useful and opened a menu with only
    Load (F10 still works).
  - The F10 menu and the result screen are opaque, like the other screens: the map and the picture no longer show through their text.
  - The new-game screen's title is centred, like the leaderboard's.
- **Smoke**: `game-main-menu` presses its buttons by name (which button has the focus depends on whether the computer has a save).
- **Not done**: fonts and a theme of our own (the art pass); the town screen's inner rows keep their own layout; the editor's toolbar
  rows keep fixed offsets.

### ADR-164: The replay viewer (ADR-101, UI-2, 2026-11-10)
**Status**: implemented (2026-11-10); core tested, the Godot parts run in two smoke scenarios. Carries out ADR-101's viewer. *(choice)* marks the
agent's choices.

- **Core** (`ReplayPlayer`, Core.Data): `Open` replays the whole record once, so a record that does not replay (a refused command, another
  final digest: other content) fails before anything is shown, and indexes every turn (`ReplayTurn`: where it starts, its date and player)
  and where battles are fought. The watched `Session` runs one command per `Step`, so the views see the events; `Seek` puts a new session at
  any point, replayed unheard from the start (ADR-058), which is how the viewer jumps to the next turn, the next day or any day, back or
  forward *(choice: forward jumps rebuild too, rather than run the commands under the views)*. `TakeOver` gives a separate session at the
  current point; `CanTakeOver` only in a battle being fought that a person commands. `GameSaves.ListReplays` lists `*.ewreplay.json` like
  the saves.
- **The list**: "Watch a replay" in the main menu opens two tabs, Replays (`user://replays`) and Saves (the save folder with the autosaves),
  newest first; Watch or a double click; a record that cannot be watched stays listed with the reason.
- **Watching**: the same world, HUD and battle views as a game, with nothing that issues a command: no End turn, questions, exchange screen,
  dig or marketplace buttons, auto combat question, turn timer, end screen, ranking, saving or autosave; town and hero screens do not open
  (the town list centres the map on a town). The battle view's `Watching` makes nobody its commander and plays **every** battle move by move,
  the computer's and quick combat too. Each frame, once the heroes' steps and the battle's animations have played, the next command runs; a
  turn starts after a short pause and a battle's result stays 1.5 s *(choice; scaled by the speed, none at Instant)*, then closes by itself
  (Enter closes it at once). The camera follows every hero it can see and switches level with it.
- **The bar** (`ReplayControlBar`, along the bottom; the map's HUD and the battle's keep its height free): play / pause (Space), the four
  speeds for heroes and battles alike (Normal to start, not kept) *(choice)*, next turn, next day, a day and Go, the whole map or a player's
  eyes (the fog as that player knew it), "Skip to the result" and "Play from here" in a battle, Main menu; the line under it shows the date,
  whose turn, the command count and the end ("The end of the replay", "The end of the save", or the game's result).
- **Skip to the result**: the battle's remaining commands run at once with the battle shown instantly, so its result screen comes up; a battle
  the core plays in one command (the computer's, quick combat) only skips its animation.
- **Play from here**: the battle goes on by hand in a separate session; the computer's side plays as usual; it is not ranked, saved or
  autosaved. Leaving the battle's result screen (or "Back to the replay") shows the viewer again, paused where it was; the replay does not
  change. Offered only when a person commands a side *(assumed: the computer's own battles stay the computer's)*.
- Right click opens the information windows (through the eyes shown, or the current player's on the whole map); L switches the level.
- `--replay=<file>` opens the game on the viewer (smoke). Smoke: `game-replay-list` (the tabs), `game-replay-viewer` (a recorded computer game:
  pause, speeds, next day, next turn, the computer's battle played and skipped, the result closing by itself, the end).
- Fixed on the way: the battle log grows upwards when full, no longer under what is below it.
- Tests: `ReplayPlayerTests` (stepping to the recorded state, the turn index, seeking back and forward like stepping, next turn and day,
  a battle's end and the take-over leaving the replay unchanged, a save ending in its battle, records that do not replay, the replays list).
- **Not done**: "Play from here" on the adventure map; taking over a battle only the computer fights; a snapshot for long replays (a jump
  replays from the start); "Play from here" has no smoke scenario (the recorded games have no battle a person fights).

### ADR-165: Refusals, content tables and running events after the code review (ADR-036, ADR-042, ADR-115, 2026-11-10)
**Status**: implemented (2026-11-10); tested. Fixes from the code quality review.

- **Refusals** (ADR-036): a rule refuses a command with `CommandRefusedException` (Core.Domain `Commands`), thrown before anything changes;
  `GameSession.Execute` catches only that. Before, it took every `InvalidOperationException` for a refusal, so a bug (a `First()` on nothing)
  passed as a quiet refusal. `GameState.GetHero` / `GetObject` / `GetPlayer` refuse an unknown id with it. The answers to a chest, a deposit, a
  cartographer, a Pandora's Box, a witch hut and an event's army choice now find everything they need before they spend, take or clear
  anything (a deposit's price was paid before the deposit was looked up). `GameSession.CheckRefusals` (internal) compares the digest text
  before and after every refused command and fails loudly when it changed; the test assembly turns it on for every game (`TestSetup`),
  about 10 s on the whole run *(choice: every test game, not one dedicated test)*.
- **Content tables**: `ContentCatalogs` (creatures, heroes, buildings, artifacts; the spells are the artifact table's) is what the
  scenarios and `MapConverter.ToGameState(document, content, options)` take; the apps pass `GameContent.All`. An edited artifact table
  (`--artifacts=`) was lost for Demo Valley, Two Towns, the editor's blank maps and the 2D harness. `MapConverter` checks shrine and
  guild spells against the game's spell table, not the built-in one.
- **Creature casters** (ADR-042, ADR-120): `Caster` in `creatures.csv` names the spell table id (`Caster chaos.fireball 1`); the code's
  name table (`BattleMagic.CasterSpells`) is gone, and a caster's spell comes from the battle's spell table (`BattleState.Spells`, the
  game's), not the built-in one. `rebirth` stays the Phoenix's own ability.
- **Running map events** (ADR-115): the queue of events to run, the one running and the battle record for "defeated by" are kept in
  `MapEventState` (`Runs`, `Current`, `Fight`), not in the service; the running ones are in the digest while there are any *(a save made
  while an event waited for an answer no longer matches its old digest)*.
- **Map files without a version** are version 1 (`MapDocument.UnversionedFormatVersion`), so every older version's handling applies; a
  document made in code is still current.
- **Smaller fixes**: the AI's battle estimate cache key uses the hero's total attack and defence (with artifacts), as the fight does;
  `EconomyService.Grant` / `TrySpend` are internal; `SubscribeWhileInTree` subscribes while the node is in the tree (again if it comes
  back), and the map editor subscribes its undo line on the session's UI instead of the root, which kept every old session alive.
- **Not done**: the tests still call `session.Interactions.Visit`, `Progression.GrantStatus` / `OfferNext`, `Artifacts.Draw` / `Loot` /
  `Grant` and `Interactions.TryCollect` / `StartWeek` directly (about 55 calls in 20 files) instead of going through `Execute`.

### ADR-166: Map object kinds in registries, not type switches (ADR-013, ADR-065, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R5).

- **Problem**: a new map object kind touched 9–12 places, several of which fell through silently: the digest's `_ =>` left a new kind's
  state out, `Visit` did nothing for an unknown type, and the map format needed a `[JsonDerivedType]` plus a `case` in both directions.
- **Domain**: `MapObjectKinds` registers each `MapObjectKind` with its instance type and a factory (`MapObject.Create` uses it). Each
  subclass states its own digest text (`MapObject.DigestState`, abstract, so the compiler asks for it; "" for none) and whether it is a
  pickup (`IsPickup`) or defended against a visitor (`IsDefendedAgainst`), which the move reads instead of a type list. The digest text is
  unchanged (checked against the old switch on the built-in and generated maps, and fresh instances of every kind).
- **Visits**: `MapObjectInteractionService` keeps one rule per instance type (a table, `Nothing` for objects used from a panel); visiting a
  type without a rule throws instead of doing nothing.
- **Map format**: `MapConverter.ObjectCodecs` (Core.Data) holds each type's `kind` name, document type, save and load; the JSON
  discriminators are added from it by a resolver modifier (`AddObjectTypes`, in `JsonDefaults` and the replay options) instead of 45
  attributes on `MapObjectDocument`. The names and the JSON are the same, so the format version stays 38.
- **Held together by tests** (`MapObjectKindsTests`): every kind is registered and makes its type; every type has a visit rule and exactly
  one codec under a unique name; a fresh object of every kind comes back from a map file with the same digest text.
- **Not done** *(choice)*: the AI's targets, the validator, the info sheets and the editor's inspector still switch on the type. Each has a
  safe default for a kind it does not know (no target, no check, a plain sheet, no fields), so a new kind is playable before they learn it.

### ADR-167: The large classes split along their seams (ADR-036, ADR-166, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R6).

- **Problem**: six classes of 1100–2000 lines each mixed several jobs, and some held copies of the same rule (five ways to heal the top
  creature of a stack, seven-way type switches over guard kinds). One change meant reading the whole file.
- **Rule**: a pure refactor of the game rules. Events, their order, refusals, `GameState.Random` draws and the digest text are unchanged;
  AI-played games on the built-in scenarios and every built-in template (two seeds each) give the same per-turn digest and event log as
  before. The map format is unchanged.
- **Map editor**: `MapEditorSession` is a facade over per-area editors (`Terrain`, `Objects`, `Contents`, `Monsters`, `Towns`, `Heroes`,
  `MapEvents`, `Players`, `Map`) sharing an internal `EditorContext` (scenario, catalog, bus, undo history, open stroke). One-value edits are a
  `PropertyEdit<T>`, which records and publishes nothing when the value is already there; the three paint tools share `PaintLayer<T>`. Edits
  that are not one value (hero type, town type, gate links, adding or removing events and players) stay their own undoable commands.
- **Map objects**: `MapObjectInteractionService` keeps the visit rules; shops, decisions (`Resolve*`), the week and map preparation, and the
  Grail are `ObjectShopService`, `ObjectDecisionService`, `ObjectLifecycleService` and `GrailService`, with rewarding visits in
  `MapObjectRewardVisits` and shared helpers in `ObjectInteractionContext`.
- **Battles**: `BattleService` is a facade over `BattleStarter`, `BattleCommandHandler`, `BattleFlow` (deployment and the AI loop both need)
  and `BattleAftermath`. The objects that guard a battle implement `IBattleGuard` (what defeat and survivors do to them) instead of two type
  switches. `BattleEngine` keeps actions and turn flow; spells are `BattleSpellResolver`, rounds and the end `BattleRounds`, and damage,
  deaths and healing `BattleWounds`, which holds the one `HealTop`, `KillCreatures` and `RollDamage`. `BattleState.NextUnitId()` makes new
  stack ids.
- **Presentation**: the combat log text is `BattleNarrator` in Core (tested). `BattleView3D` coordinates `BattleAnimationQueue`,
  `BattleFieldBuilder` (one material per colour per battle), `BattleStage` and `BattleAnimations`. The game app's `Main` keeps composition;
  status text moved to Core (`MoveOutcomeText`, `AdventureSpellPrompt`, `SaveSlotText`, `RankingText`, `TemplateFolder`), and the HUD,
  map and battle input, deployment drag and setup screens to Godot.UI (`AdventureHud`, `AdventureInputController`,
  `BattleInputController`, `DeploymentDragController`, `SetupScreenHost`). The input controllers are made per game, so an armed spell or a
  drag in progress does not carry into a loaded game.
- **Not done** *(choice)*: the map editor app keeps its own template-folder loading; `Main.Saves.cs` stays a partial of `Main` because it
  needs most of its state.

### ADR-168: A handler per spell effect kind (ADR-099, ADR-123, ADR-167, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R7).

- **Problem**: what one effect kind does was spread over the target checks, the resolver and the AI's valuation, each switching on
  `SpellEffectKind`; a new mechanic meant five or six edits, and the AI's value of a spell could drift from what the spell does.
- **Rule**: each `SpellEffectKind` has one internal sealed handler in `Battles/Spells/` (`ISpellEffectHandler`, found through
  `SpellEffectHandlers.For(kind)`). It holds the kind's own refusals (at three fixed points: before the aim checks, before and after
  `Affects`), which extra stacks `targets N` reaches, how the cast resolves, the AI's value of a cast (`Value(CastContext)`) and the aims the
  AI tries. Buff and Curse share `EnchantHandler`, which the damage handler also uses for damage that leaves an effect.
- **Shared**: aiming, reach and area cells, filters, immunity and resistance, and the damage plan (used by the rule and the AI) stay in
  `BattleMagic`, as do small formulas both a rule and its value read (`Missing`, `Dispelled`, `Slain`, `SacrificeYield`, …).
- **A new effect kind** is its parse in `SpellEffect.Parse` plus one handler class; a test fails while a kind has no handler or two.
- **Behaviour**: events, their order and the random draws are unchanged (AI games with 89 casts replay the same). The kind's refusal
  checks run at their fixed points, which gives the same answer for every effect the spell table can express.
- **Not done** *(choice)*: the spell table parser and `SpellEffect.IsHostile`/`TwoClicks` stay on the effect (they describe the format);
  the battle HUD keeps its two checks of `Move` for the two-click prompt, as the registry is internal to Core.

### ADR-169: Commands by handler and marker, turn phases as one ordered list (ADR-036, ADR-167, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R8).

- **Commands**: `GameSession` dispatches through `CommandHandlers`, one registration per command type, grouped by area
  (`AdventureCommandHandlers`, `ObjectCommandHandlers`, `TownCommandHandlers`, `ArtifactCommandHandlers`, `BattleCommandHandlers`). A second
  registration for a type throws, and a test fails while a command has no handler. A new command is its record plus one registration line.
- **Authorization**: `IBattleCommand` marks the commands that need a running battle and are checked against the side to act;
  `IHeroCommand` (`HeroId Hero`) marks those whose hero the session checks is on the map and the player's. Equip, Unequip, Assemble, Take
  Relic Apart and Appoint Governor carry a hero but are checked by their own services, with their own refusals, and are not marked;
  `MoveStackCommand` names its `FromHero` as the hero; `SetAutoCombatCommand` is allowed at any time. The markers add nothing to the
  commands' JSON.
- **Turns**: `TurnService` calls an ordered list of `ITurnPhaseParticipant`s built in `GameSession` (map events, progression, movement, mana,
  towns, caravans, economy, guards, map objects), each hook a no-op by default (`SetUpGame`, `StockFirstWeek`, `StartDay`, `StartWeek`,
  `ApplyWeekEvent`, `StockWeek`, `OpenDay`, `PrepareTurn`, `OpenTurn`). The list's order is the order of the turn. The arena stays a direct
  dependency, as its battle day replaces the whole day.
- **Subscriptions**: visibility, map events and adventure magic subscribe themselves (`SubscribeTo(bus)`), in that order.
- **Behaviour**: unchanged (events, refusals, random draws, digests; AI games replay the same).

### ADR-170: `*Rules` classes are pure queries; their changes are services' (ADR-167, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R9).

- **Rule**: a static `*Rules` class holds constants and queries (checks, costs, odds, offers) only. Whatever changes the state or publishes
  lives in a service that the session builds and hands to its callers. `RulesPurityTests` fails on a `*Rules` method that takes an
  `IEventPublisher` or returns nothing.
- **`TownVisitService`** (new): a hero's arrival in a town (`Enter` = governor by Stewardship, then `Visit`: the Grail building, the unique
  buildings' bonuses, the war lodge's warcries, the guild's spells and their ranks, mana after a night), `VisitBy` after a build, governors
  (`AutoAppoint`, `SetGovernor`) and `Capture`. Movement, object decisions, Town Portal, map events, `TownService` and the battle aftermath use
  it; the guard kinds reach it through `BattleGuardOutcome.Towns`. It replaces `MagicRules.Visit`/`VisitBy`, `GrailRules.RaiseBuilding`,
  `UniqueBuildings.Visit` and the static `TownService.Capture`/`AutoAppoint`.
- **Others**: `GrailService.Bury` and `VisitObelisk` (were `GrailRules.Bury`/`Visit`), `ObjectLifecycleService.PreparePrisons`
  (was `PrisonRules.Prepare`), `PandorasBoxService.Open` (was `PandorasBoxRules.Open` with 7 parameters; a won guard battle reaches it through
  `BattleGuardOutcome.Boxes`), `PlaceholderService.Resolve` (was `PlaceholderRules.Resolve`), `TownService.DrawGuilds` (was
  `MagicRules.DrawGuilds`). `CreatureBankRules.Roll` returns the rolled tier and `GuardService` fills the bank with it (was
  `CreatureBankRules.Fill`). The unique buildings' bonuses use the session's `ProgressionService` instead of a new one per visit.
- **Behaviour**: unchanged (events, refusals, random draws, digests; AI games replay the same). The game-start order is the same: placeholders,
  Grail, surplus heroes, begin, unset pickups, prisoners, object rolls, guild spells.

### ADR-171: One `ContentCatalogs` record from the apps to the generator (ADR-165, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (R10).

- **Record**: `ContentCatalogs` holds every content table a game reads (creatures, spells, artifacts, buildings, hero roster, and also the map
  objects and terrains); `ContentCatalogs.Default` is the built-in content. `GameState` takes its object and terrain tables from it like the
  others.
- **Entry points**: `MapConverter.ToGameState(document, content?, options?, checkEvents)`, `GameSaves.Load(path, content?)`,
  `GameReplay.Replay` / `ReplayPlayer.Open(record, content?)`, and the generator's `MapGenerator.Build` / `Generate` / `GenerateBest` and
  `EditorMapGenerator.Generate` take one optional `ContentCatalogs` instead of one optional parameter per table; omitted means the built-in
  content. The apps build their record once (`GameContent.All`; the Sim tool from `--creatures`) and pass it everywhere.
- **Generator**: `GeneratedMap.Content` carries the record through every stage; no stage reads a built-in table itself
  (`ZoneLayouter`, `TerrainPainter` and `TemplateValidator` take the record too). The template editor still validates against the built-in
  tables, as it has no game content.
- **Behaviour**: unchanged with the built-in content (digests and AI games replay the same).

### ADR-172: One copy of each shared step (code review section 3, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (section 3, duplication).

- **Joining heroes**: `HeroRecruitment.Join` (a roster hero with its experience levelled up at once, a hired one's starter stack, moving
  today or next turn) and `Return` (a retired hero hired again) replace the four copies in `TownService.HireHero`, the map tavern, prisons
  and map events. The callers still check and publish.
- **Building**: `TownConstruction.Raise` / `RaiseDwelling` are the day's build and a map event's free build alike; the rule that a guild,
  war lodge, school or unique building lets the town's heroes take it at once is written once.
- **Taking an artifact away**: `ArtifactService.Remove(hero, artifact, wornFirst)` for the Grail building, the altar and map events (a map
  event takes a worn one first, the others a carried one).
- **Battle end**: `BattleOutcome.Of` reads the outcome once; `BattleAftermath.Finish` applies it and auto combat's preview shows it, and
  the preview raises undead into the army the hero fought with (`BattleAftermath.PreviewRaised`). This fixes two disagreements: the
  preview promised experience for the arena battle, and it raised a garrison hero's undead into the hero's own (empty) army rather than the
  garrison. `BattleOutcome.KeepSurvivors` is the one survivors-into-slots step (hero, town or map garrison).
- **Battle input**: `BattleTargeting.CommandFor` / `CommandAt` turn an action into its command for the game and the 2D harness. The keys
  stay each app's input mapping (the harness keeps S for surrender; the game has the HUD's button).
- **UI**: `FormBuilder` (Godot.Shared/Controls) replaces the panels' own `Row` / `Spin` / `Options` helpers (inspector, events, conditions,
  template editor, new game, game menu); `node.ClearChildren(detach)` replaces 35 free-all-children loops.
- **Sim**: its decision answers are `TurnTimer.Default` except where it deliberately differs (witch huts and training take the first
  option, riddles and level-ups the first answer, boxes stay shut, guards are always fought). The connectivity check moved to
  `MapConnectivity` in Core.Domain (`Editing/`), next to `MapValidator` so the validator can use it; not in Core.Generation, which the
  validator cannot reference.
- **Behaviour**: unchanged apart from the preview fixes (events, random draws, digests; AI games replay the same; the Sim's self-test digest
  is the same).

### ADR-173: Caches beside what they cache (code review section 4, 2026-11-10)
**Status**: implemented (2026-11-10); tested. From the code quality review (section 4, performance).

- **Spell sums**: a stack's spells are an `ActiveSpellList` that keeps their sum (`BattleUnit.SpellModifiers`, under every `Has(status)`,
  damage roll and AI estimate) until the list changes.
- **Ids**: `GameState` keeps hero and object dictionaries by id beside its lists (and the cell index); `GetHero`, `GetObject` and
  `FindObject` no longer scan. Garrison heroes are still found through their towns.
- **AI threats**: `AdventureAi` keeps each player's visible enemy threats until the next command (only commands change the game, so the
  session's command count is the key); every candidate target and every flooded explore tile used to scout every enemy hero again.
  The 10-candidate cap still counts only safe candidates: counting every fight simulation would change what the AI picks, which is
  tuning, not a cache.
- **Views**: `TownPanel` and `HeroPanel` (29 and 24 event kinds) and the minimap refresh through a `RefreshScheduler`, once at the end of
  the frame. The minimap's tiles are an image, one pixel each, of which only the changed pixels are written again; heroes are drawn over
  it.
- **Behaviour**: unchanged (events, random draws, digests; AI games replay the same).

### ADR-174: Map format upgrades and testing safety nets (code review section 5, 2026-11-11)
**Status**: implemented (2026-11-11); tested. From the code quality review (section 5).

- **Upgrades**: an older map file's JSON is upgraded before it is read (`MapMigrations` in Core.Data `Maps`, `IMapMigration` steps, oldest
  first; each runs on files older than its `Version`), so `MapDocument` and `MapConverter` know only the current format. The steps: v5
  `tactics` → the hero's `skills`; v13 the map-wide `guardMode` → each monster's `mode`; v23 `bank.ruins` → `bank.crypt`. A file without
  `formatVersion` is v1. `MapSerializer.Deserialize` and `GameRecordSerializer.Deserialize` (a save's or replay's `map`) upgrade; a file of
  the current version is read as it is. Gone: `MapDocument.GuardMode`, `HeroPlacementDocument.Tactics`, `PerMonsterGuardModeVersion`,
  `RenamedDefinitions`. A change: the three old forms now count only in files older than their version (before, a current file with
  `tactics` or `bank.ruins` was read the old way). A version that only adds fields needs no step. Upgrading through `JsonNode` reads
  property names without case, as the serializer does.
- **Pins** (`Pinned`: a file next to the test; `EW_UPDATE_PINNED=1` rewrites it): a sample map per format version that has an upgrade step
  (the version before it, and v1) and for the current version (`Data/FormatSamples`, with every object kind), each read and saved again to a
  pinned result; the JSON shape of maps and saves (`Data/FormatShape.txt`: property names, required fields, object and command kinds, enum
  names), so a rename shows; the maps the shipped templates generate (`Generation/GeneratedMaps.txt`), whose re-pin is refused unless
  `MapGenerator.Version` changed. A new map format version needs its sample.
- **Editor undo**: `EditorUndoTests` runs every editor operation (73 cases, a reflection check that none is missing) and compares the
  map file and digest after undo and redo. It found that undoing a removal put the object or hero back at the end of its list, and a
  town's type change moved the town to the end: `GameState.RemoveObject` / `RemoveHero` return the index and the editor puts it back there.
- **Battle copy**: `BattleCopyTests` compares a busy siege battle and its `Copy()` field by field, checks that nothing changed in place is
  shared, and lists the fields of the copied types, so a new field fails until `Copy()` is checked.
- **Architecture**: `ArchitectureTests` checks every project's references against the dependency table (architecture.md).
- **Refusals**: already covered by ADR-165 (`GameSession.CheckRefusals` in every test game).
- **Test cleanup**: `TestFormat` (`Save`, `Load`, `RoundTrip`) and `TestEditor` (`Blank`, `World`) replace the inline round trips and
  editor factories; the milestone-named catalogue tests are split by object (`CartographerAndMerchantTests`, `SphinxAndWitchHutTests`,
  `RefugeeCampAndAltarTests`, `MapGarrisonTests`, `DepositTests`, `MagiWindowAndBorderPostTests`, `MapTavernTests`, …).
- **Behaviour**: unchanged for play (AI games replay the same); the editor's undo of a removal and a town's type change keep the order.

### ADR-175: Closing the code review (ADR-165 … ADR-174, 2026-11-11)
**Status**: implemented (2026-11-11); tested. The last items of the code quality review, which is archived in
`docs/project/archive/code-review-2026-11.md`.

- **Tests through commands** (ADR-036, R4): 41 of the 55 direct service calls in the tests now go through `Execute`. `SessionCommands`
  gained `Visit` (walk onto an object's entrance), `Revisit` (step off and back on), `PlayUntilDay` / `NextDay` (instead of setting the
  date) and `TakeLevelUps`; looting, a map event's scroll, level-up offers and battle statuses come from real battles, events and visits.
  Left *(choice)*: 14 calls that test an internal rule no command reaches without changing what is checked: `TryCollect` on a made-up
  weekly object, the stacking of made-up statuses, end-of-day statuses (only adventure spells grant them), and `Artifacts.Draw` (the relic
  pool's no-repeat rule, scroll odds over 2000 draws). `SecondCatalogueTests` is `ManaSiteAndPowerUpTests`.
- **The game's tables everywhere** (B2, R10): Demo Valley, Two Towns and Ember March place objects from the content's object table (and Demo
  Valley checks terrain against its terrain table); the template editor validates against the game's tables (`TemplateEditorSession`
  takes `ContentCatalogs`; the map editor passes `GameContent.All`); a creature caster's spell and a stack's spells are named from the
  game's spell table in the info sheets (`InfoSheets.Capability(capability, spells)`), and the hero panel's artifact bonuses and lent
  spells from the game's.
- **Battle HUD** (R7): the two-click prompt asks `SpellEffect.SecondIsStack` instead of checking for `SpellEffectKind.Move`; no front-end
  reads an effect kind.
- **AI fight bound** (section 4): a target choice plays out at most 40 fights (`AdventureAi.FightsChecked`), unsafe ones included, beside
  the 10 safe candidates it checks. Before, a hero ringed by monsters too strong for it simulated every one. The bound is above what
  ordinary choices reach: the AI games replay the same.
- **Left as chosen** in ADR-166 … ADR-169: the AI's targets, the validator, the info sheets and the inspector switch on the object type
  (each with a safe default); five hero commands stay unmarked (their services refuse with their own texts); the map editor app loads its
  template folder itself.
- **Checked in Godot** (the UI work of ADR-167, ADR-172, ADR-173 and this one): `tools/smoke-godot.ps1` passes 53 of 55 scenarios;
  `game-turn-timer` and `game-random-map` fail the same way on the commit before the review (a scripted click on a long drop-down
  list opens it under the cursor and the release picks an item).
- **Behaviour**: unchanged with the built-in content (AI games replay the same).

### ADR-176: Fog of war looks: a starry void or clouds, opening as the hero walks (2026-11-11)
**Status**: accepted and implemented (2026-11-11), the owner's request with reference shots of a starry fog; builds ADR-099's smooth fog.
- **Two looks, a setting on this computer** (`InterfaceSettings.Fog`, menu "Settings" → "Fog of war"; the settings file's `interface.fog`):
  **Dark** (the default *(owner's choice)*), a starry void: the map floats in space; **Clouds**, thick drifting clouds. Both hide exactly
  what the rules hide; **Clouds are opaque** like the void *(owner's choice: not see-through)*. Explored tiles out of sight (refog mode)
  are veiled: dimmed under the void, a thin haze under the clouds. The editor shows no fog.
- **Technique: a full-screen pass, not a mesh over the tiles** (`FogView`, `shaders/fog_of_war.gdshader`). Each pixel reads the depth
  buffer, finds where it stands on the map and reads a fog mask there (one texel a tile, with a hidden border). So an object at the edge
  shows the part that stands on explored ground and the rest dissolves (ADR-099); pixels with nothing behind them use the ground plane.
  The mask is blurred and read through a slowly moving noise warp of about a tile, so the edge wanders and never shows the grid; an edge
  between an explored and an unexplored tile fades over about 1.3 tiles in both looks *(owner's choice)*. The map's own edge melts into
  the void the same way.
- **The animation is real 3D parallax, not a scrolling picture**: three star layers and a nebula lie on planes 40 to 160 units below the
  map, so they shift against the land as the camera pans, turns and zooms; the nebula drifts, the stars twinkle. The void is mostly
  truly black: sparse stars, about one in eight big and bright, and small faint violet-blue nebula wisps *(owner's choice)*. The clouds lie on two
  planes above the ground and drift with the wind. Edge of the land under the void: a wide dark rim first, then the stars.
- **Procedural by default; textures optional**: `textures/fog/nebula.png` replaces the procedural nebula and `textures/fog/clouds.png`
  adds detail to the clouds when present (prompts for an image model were given with this ADR; the shots in the request are the target).
- **Smooth reveal**: the core reveals at once; the view fades each tile over 0.9 s, starting when the step that revealed it starts to play
  (`MapViewContext.LastStepStart`), so the fog rolls back as the hero walks. A reveal of 30 tiles or more (a captured town, a Crypt, a
  spell) opens as a wave from its middle. Entering or leaving sight fades in 0.5 s. A new viewer (hot-seat) or level snaps without fading.
- Rendering only: rules, saves and the AI are unchanged.