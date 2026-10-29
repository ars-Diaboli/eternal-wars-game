# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-10-29** (ADR-092, ADR-093): new ideas bring FLOW-4 (victory screen and ranking), UI-2 (replays) and HERO-8 (specialties)
([below](#new-ideas-adr-092-2026-10-29)); UI-2 follows the owner's revised note (the replay saved from the ranking table, a leaderboard
in the main menu, unfinished saves watched too).
Before that (ADR-090, ADR-091): CMB-10 (auto combat, built), UI-1 (speed settings, built ADR-095) and FLOW-3 (turn timer, built) answered. Before that (2026-10-28, ADR-088), new
ideas brought CMB-9, CMB-10, UI-1, FLOW-3, MP-1 and MP-2 ([below](#new-ideas-adr-088-2026-10-28)); CMB-9 is answered and built (ADR-089).
Before that (2026-10-26, ADR-079, ADR-080), the sixth round: TOWN-9 … TOWN-11 and MAP-25 … MAP-34 answered and built (the Astrologer's
Tower waits for the week events). Before that (ADR-077): new ideas (scenery, rivers, flaggable providers, caravans, Thieves' Guild, the third object catalogue) bring
MAP-25 … MAP-34 and TOWN-9 … TOWN-11. Fifth round (ADR-075): the ADR-073 assumptions confirmed (TOWN-5's reach and FLOW-2's first week
changed); CMB-2 answered; town-less campaign players.
Fourth round (ADR-073): the ADR-070 … ADR-072 assumptions confirmed (MAG-5 and MAG-10 changed); HERO-4, TOWN-5,
FLOW-2 answered; FLOW-1 except the campaign hooks.
Third round (ADR-072): HERO-1, HERO-3, HERO-6, HERO-7, TOWN-7, TOWN-8, RMG-11, CMB-6, MAG-3, MAG-5 … MAG-12 answered,
MAP-23 corrected; MAG-1 partly (the Spell Power formula); HERO-4 answered except where the war machines stand.
Second round (ADR-071): DATA-1, RMG-8, RMG-12, RMG-13, MAP-20, TOWN-1, TOWN-4, CMB-7, CMB-8, MAG-2, MAG-4 answered. First round (ADR-070): MAP-15 …
MAP-19, MAP-21 … MAP-24, TOWN-2, TOWN-3, TOWN-6, CMB-1, CMB-3, CMB-4, RMG-9, RMG-10 answered.

## 🚧 Blocking now

Nothing blocks the current work (the Phase 4 fairness check and the Phase 3 play-test). The ADR-088 questions stop only their own
roadmap items, and so do FLOW-4, UI-2 and HERO-8 (ADR-092, ADR-093); MP-1 (a) decides where multiplayer goes in the plan.

## Heroes

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names and specialties in `heroes.csv`; primary stat odds and starting values (ADR-055).

## Combat

<a id="cmb-5"></a>
**CMB-5 Sieges** (post-1.0). Confirm sieges come after 1.0; until then the siege Ballistics rules (ADR-063) stay unused.

## Magic (Phase 5)

<a id="mag-1"></a>
**MAG-1 Spell list.** The spells and numbers per school ([spells.md](../content/spells.md)); spell tags beyond `all`, `mind`, `fire`, `curse`,
`holy`; which spells creature casters get. The Spell Power formula is set for now (ADR-072: damage and duration × (1 + SP / 10)); the
spells.md tables still use "a + b·SP" and are rewritten with the list.

## Game flow and content

<a id="flow-1"></a>
**FLOW-1 Campaign hooks** (Phase 6). How does a campaign hook into the victory and defeat conditions (ADR-073 sets the conditions)?

<a id="cont-1"></a>
**CONT-1 Lore names.** Working titles (Light, Darkness, Wild, Primordial) or the lore names (Dawn Covenant, Ashen Court, Unbound, Firstborn)?

<a id="cont-2"></a>
**CONT-2 Roster review.** Names, capabilities and numbers in `creatures.csv`. The size pass is done (ADR-074): 9 Large, the seven dragons and
the Behemoth line; whether a non-dragon line stays Large (Behemoths, Hydras or none) is decided once there are visuals.

<a id="cont-3"></a>
**CONT-3 Artifacts.** The draft numbers; HoMM3-style placeholders or lore items; Crown of the Waning Eye's "best magic skill" and ties.

<a id="cont-4"></a>
**CONT-4 More twists.** Which other systems should depart from HoMM3 ([departures.md](../design/departures.md))?

<a id="cont-5"></a>
**CONT-5 Primal Fury names.** One name for both towns, or a Swamp name? "Warcry" for Stronghold and "Spiritual Totems" for Swamp Bestiary, or one?

## New ideas (ADR-092, 2026-10-29)

<a id="flow-4"></a>
**FLOW-4 Victory screen and ranking.** A HoMM3-like score on winning a map that is not campaign-like; ranking tables, separate for random maps
and shipped maps (ADR-092).
(a) **Which games count**: how does the game know a map is campaign-like? A flag the editor sets on the map, or worked out (a human player
starts without a town, ADR-075)? And a game lost, or won by a computer only: no score?
(b) **Difficulty**: the game has no AI levels yet, only the complexity (starting resources, ADR-063). Is the difficulty the complexity, and
does "Map default" count as Normal? Does anything else change the score: the number of computer opponents, the map size, the fog mode, the
turn timer?
(c) **The score**: HoMM3 gives points for few days, times a difficulty factor (80 % … 200 %). Take that, or another formula?
(d) **Rank titles**: HoMM3 names each score band after a creature, the best after a dragon. Our own creatures, weakest to strongest?
(e) **Several humans**: in a hot-seat game each human winner (a team's surviving humans, ADR-083) gets a row? Is a hot-seat game ranked at all?
(f) **Maps made in the editor** (neither random nor shipped): their own table, the shipped maps' table, or not ranked?
(g) **The row**: which columns besides the map (the template and size for a random map), the difficulty, the days and the score? The player's
name (asked on the victory screen), the faction, the date played?
(h) **The tables**: how many rows (HoMM3 keeps the top 10 or so)? Kept per computer in the user data folder (like the speed settings, ADR-090)?
Where are they seen: the victory screen (the new row highlighted) and a "High scores" entry in the main menu?
*Proposal*:
(a) worked out: a map is campaign-like when a human player starts without a town, and the editor can also mark a map as not ranked; only a
won game scores;
(b) the difficulty is the complexity, "Map default" counts as Normal; nothing else changes the score for now;
(c) HoMM3's: base points = max(10, 200 − days) for maps, the factor by complexity Beginner 80 %, Easy 100 %, Normal 130 %, Hard 160 %, Expert
200 %;
(d) yes, 14 bands named after our creatures from the weakest to a dragon;
(e) each human winner gets a row, hot-seat included;
(f) the shipped maps' table, marked "custom";
(g) rank, player name, faction, map or template (with size), difficulty, days, score, title, date;
(h) the top 20 per table, kept per computer in the user data folder; on the victory screen and under "High scores" in the main menu.

<a id="ui-2"></a>
**UI-2 Replays and the main menu.** The ranking table offers to save a game's replay; the main menu gets "Watch a replay" (a finished game's
replay, or an unfinished game's save up to the moment it was saved) and "Leaderboard" (ADR-092).
(a) **The file**: a replay is the game record a save already is (start map, options, every command, ADR-058). "Save replay" on the ranking
table means each row keeps its game's record (written when the row is made), and the button copies it out under a name the player gives?
Its own folder and extension (`*.ewreplay.json`, as the simulator's)? Can a game that is not ranked (lost, campaign-like) be saved as a
replay at all, from its end screen?
(b) **The main menu**: today the game opens on the new-game screen. A main menu with New game, Load, Watch a replay, Leaderboard, Quit?
(c) **Whose view**: fog of war hides what the player did not see. Watch through one player's eyes (switchable), or with the whole map revealed?
(d) **Controls**: play and pause, a speed (the UI-1 steps), jump to the next turn or day? Going back (the game is replayed from the start to
that day, ADR-058)?
(e) **Battles**: shown move by move, or only their result unless the viewer opens them? And the computer's turns: shown in full?
(f) **Take over**: may the viewer stop the replay and play on from there, as from a save?
(g) **Other content**: a replay made with other creature tables or rules no longer replays the same game (ADR-058 refuses such a save). The
same error, or replays keep the content they were made with?
(h) **Watching a save**: "Watch a replay" lists the replays and the saves (autosaves too) in one list, or has two tabs? A save plays up to
the moment it was saved and then stops (with "Play from here", f)?
*Proposal*:
(a) each ranking row keeps its game's record; "Save replay" writes it to `user://replays` as `*.ewreplay.json` under a name the player gives;
every end screen (also a defeat or a game that is not ranked) offers "Save replay" too;
(b) yes;
(c) the whole map revealed by default, and a choice of any player's eyes;
(d) play, pause, the four speeds, next turn, next day; going back to the start of any day;
(e) battles shown move by move at the battle speed, with "skip to the result"; computer turns in full;
(f) yes, "Play from here" starts a game at that moment (it is then an ordinary game, not ranked);
(g) the same error as a save: "made with other content";
(h) two tabs, Replays and Saves; a save plays to its last command, then stops with "Play from here".

<a id="hero-8"></a>
**HERO-8 Rarer resource specialties; Necromancy and Logistics specialists.** (ADR-093) The note: 4 heroes with +350 gold a day (random, one per
faction), 6 with +1 rare resource a day (no two of one faction; 2 sulfur, 2 crystal, 2 mercury), 2 Necromancy specialists (a mage and a
warrior, a flat +5 %), 2 Logistics specialists (random, a flat +10 %). Resource specialties exist already: the roster template gives every town
3 heroes with +350 gold (24 in all), one each with +2 ore, +2 lumber and +1 mercury (8 each), 1 Logistics specialist (+5 % per level, 8 in
all), and 2 Necropolis mages with a Death attunement specialty (its Necromancy effect is not built).
(a) **The whole roster**: are the note's numbers for all 128 heroes, so they replace today's 48 resource specialists and 8 Logistics
specialists?
(b) **Ore and lumber**: no hero gives them any more?
(c) **The heroes that lose their specialty** (about 50): what do they get: a specialty in one of their starting skills, a spell, their own
attunement, a creature specialty (HoMM3: a creature line stronger)?
(d) **Random**: drawn once into `heroes.csv` (the same heroes every game, listed in the roster), or drawn per game from the seed?
(e) **One per faction**: 4 gold heroes from 4 different towns and 6 rare-resource heroes from 6 different towns; may a town have one of each?
Do Stronghold and Swamp Bestiary take part? Any class?
(f) **Necromancy**: both Necropolis heroes (only Necropolis has Death attunement)? A flat +5 points on top of the skill (Basic 5 → 10 % …
Master 20 → 25 %), stacking with the Necromancy Amplifier (up to 30 %)? Only with the skill?
(g) **Logistics**: +10 % movement on land with or without the Logistics skill? Do the other skill specialties (Melee, Archery, Tactics, the
attunements) keep +5 % per level, or become flat too?
*Proposal*:
(a) yes, the whole roster;
(b) yes, no ore or lumber specialists (their mines are common);
(c) a specialty in their first starting skill (+5 % per level), its effect built where it is missing (Armor, Treasury, War Tempo, …);
(d) drawn once into `heroes.csv` with a fixed seed, listed in `hero-roster.md`;
(e) one of each may share a town; all eight towns take part; any class;
(f) both Necropolis heroes, the slots that today have Death attunement as their specialty (the mage) and a warrior; +5 points on top of the
skill, stacking with the Amplifier; only with the skill;
(g) +10 % movement on land even without the skill; the other skill specialties keep +5 % per level.

## New ideas (ADR-088, 2026-10-28)

<a id="mp-1"></a>
**MP-1 LAN multiplayer: scope and model.** (ADR-088) 
(a) **When**: before 1.0 (after the Phase 3/4 exits, before the campaign), or after 1.0?
(b) **Model**: clients send commands to the host; the host checks and runs them with `session.Execute` and sends every accepted command to all
clients, which run it too (the core is deterministic) and compare `GameStateDigest` now and then. Or the host sends state? 
(c) **Finding a game**: LAN broadcast discovery plus typing an address? 
(d) **Lobby**: the host picks the map or the template and its options (ADR-086). Each client picks
a free colour; the host may set the other colours to computer. 
(e) **A battle between two remote humans**: each controls its own side live?
(f) **A player who drops out**: the game pauses for everyone, and the host may let the computer take over or wait for a rejoin? 
(g) **Fog and
cheating**: every client has the whole state (a modified client could see through the fog). Fine on a LAN? 
(h) **Saves**: only the host saves; the save records which colours were human. Loaded in hot-seat, the player chooses which colours are human. 
(i) **Chat**: in 1.0 or not? 
*Proposal*: (a) after 1.0 unless you want it sooner. It needs a stable game first, and hot-seat covers playing together until then; 
(b) commands from the host, digest check every turn; (c) yes; (d) yes; (e) yes; (f) pause, then the host chooses; 
(g) fine on a LAN; (h) yes; (i) a simple text chat.

<a id="mp-2"></a>
**MP-2 Simultaneous first week.** (ADR-088) 
(a) **Start zone** on a hand-made map: maps have no zones. The tiles within some distance of the
player's start towns, zones painted in the editor, or random maps only? 
(b) **Conflicts** that end it: entering another player's start zone,
touching an object another player touched this day, seeing another player's hero? 
(c) **"Played again in the normal order"**: discard the whole day for everyone and replay it turn by turn 
(the players lose what they did that day), or keep everything done before the conflict and finish
the day turn by turn from there (proposal)? 
(d) Is it a lobby option (on or off), and how long: always the first week, or settable (1 to 2 weeks)? 
(e) The **turn timer** in simultaneous days: one timer for all, the day ending when everyone ended their turn or the time is up? 
(f) Hot-seat cannot do it (one screen): multiplayer only?
*Proposal*: (a) random maps: the template's start zone; hand-made maps: tiles within 10 of a start town until the editor can paint zones; (b)
entering another start zone or touching an object another player touched that day; (c) keep what was done and finish the day in turn order,
with the player who caused the conflict last; (d) a lobby option, the first week, on by default; (e) one timer, the day ends when all have
ended their turn; (f) multiplayer only.

## Numbers to tune (not questions)
Placeholders that wait for play-testing: caravan cost (ADR-054); complexity starting resources (ADR-063); Stewardship and governor percentages
(ADR-063); siege Ballistics (ADR-063); zone guard and garrison powers, `GuardPicker` ranges (ADR-064); building costs; the growth buildings (+5 / +2, ADR-071); war machine stats and prices
(CMB-3); morale 2 % and luck 4 % per point (ADR-053); deposit prices and the map marketplace threshold (ADR-070); the AI's retreat threshold.

## Answered
One line per question; the ADR holds the detail.

- **Scope**: fidelity ADR-008; art [presentation.md](../design/presentation.md); modes, platforms, harness, slice ADR-034; lore drafted by Claude.
- **Adventure map**: scale ADR-020, camera ADR-022, terrain ADR-011/045, native terrain ADR-032, roads ADR-024/032, movement ADR-018/037, fog
  ADR-017/031, gates ADR-026, meeting heroes ADR-027, resources ADR-019, chests ADR-039, mines ADR-034, guards ADR-040/065, no sea ADR-051.
- **Objects**: MAP-1 … MAP-14 ADR-068; MAP-15 … MAP-19, MAP-21 … MAP-24 ADR-070; MAP-20 ADR-071; MAP-23 corrected ADR-072.
- **Towns**: groups ADR-010/028/029, tech tree ADR-030/046, income ADR-025, caravans ADR-054, tavern ADR-060/070, governors ADR-063, TOWN-1 fort
  ADR-066, TOWN-2 ADR-067/068/070, TOWN-3 ADR-070, TOWN-6 ADR-070, TOWN-1 growth buildings and TOWN-4 ADR-071, TOWN-7 and TOWN-8 ADR-072, TOWN-5 and base-creature map dwellings ADR-073, TOWN-5 reach ADR-075.
- **Heroes**: roster ADR-048, skills ADR-048/069, stats ADR-055, schools ADR-063/069, artifacts ADR-061, HERO-2 ADR-069 (rest in HERO-7, MAG-9, MAG-10,
  CONT-5); HERO-1, HERO-3, HERO-6, HERO-7 and HERO-4 ADR-072, HERO-4's war machines ADR-073.
- **Combat**: grid and sizes ADR-005/021/070, battlefield ADR-033, turn order ADR-009, damage ADR-033, cover ADR-035, tuning ADR-038, speeds ADR-037,
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070, CMB-7/8 ADR-071, CMB-6 check order ADR-072, CMB-2 ADR-075, CMB-9 (movement as a circle) ADR-089, CMB-10 (auto combat) ADR-090.
- **Magic**: MAG-4 ADR-070/071; MAG-2, MAG-3 (mana refill, Spell Power role, potency), MAG-5 (levels), MAG-6 (resistance) ADR-071; MAG-1 (Spell Power formula), MAG-3, MAG-5, MAG-6 … MAG-12 ADR-072; MAG-5 (fixed 500 points) and MAG-10 (even stat odds) ADR-073.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070; RMG-8, RMG-12, RMG-13 ADR-071; RMG-11 ADR-070/072.
- **Data**: DATA-1 ADR-071.
- **Presentation**: UI-1 (speed settings) ADR-090, built ADR-095.
- **New ideas** (ADR-077): TOWN-9 … TOWN-11, MAP-25, MAP-26, MAP-28 ADR-079; MAP-27, MAP-29 … MAP-34 ADR-080 (MAP-32 built after the week
  events).
- **Game flow**: victory ADR-057; FLOW-1 (conditions, deadlines, teams) and FLOW-2 (week and month events) ADR-073, the conditions built ADR-094; FLOW-2 event start and town-less players ADR-075; FLOW-3 (turn timer) ADR-091.
- **Assumptions** of ADR-070 … ADR-072: confirmed in ADR-073; of ADR-073: confirmed in ADR-075.
