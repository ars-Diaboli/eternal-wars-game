# Open questions

Design questions still to answer. Answers move into the [design docs](../design/overview.md) and
[decisions.md](decisions.md); the question then leaves this file and gets one line under [Answered](#answered).

Every question has a stable id (`RMG-8`, `TOWN-4`, …) so the roadmap and ADRs can point at it. A question covers **one
design area**; its lettered points (a, b, …) are parts of the same decision. **🚧** marks a question that stops a roadmap
item; the others can wait. Each question ends with a **proposal** the agent will use if the owner agrees.

**Updated 2026-10-29** (ADR-099): CMB-5 (sieges before 1.0), MAG-1 and CONT-3 (spells and artifacts re-drafted), MP-3 and MP-4
answered; new: CMB-11 (siege rules), MAG-13 (guild selection, spell review), CONT-6 (artifact review).
Before that (ADR-098): MP-1 and MP-2 (LAN multiplayer) answered; they bring MP-3 (clients that see only their own view)
and MP-4 (start zones).
Before that (ADR-097), the arena mode brought ARN-1 and ARN-2 ([below](#new-ideas-adr-097-2026-10-29)).
Before that (ADR-096), new ideas brought MAP-35 (Pandora's Box) and EVT-1 … EVT-6 (map events like HotA's extended events,
quests) ([below](#new-ideas-adr-096-2026-10-29)).
Before that (ADR-092, ADR-093), new ideas bring FLOW-4 (victory screen and ranking), UI-2 (replays) and HERO-8 (specialties)
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

Nothing blocks the current work (the Phase 4 fairness check and the Phase 3 play-test). CMB-11 stops only the siege items, and so do FLOW-4, UI-2 and HERO-8 (ADR-092, ADR-093) and MAP-35, EVT-1 … EVT-6 (ADR-096) and ARN-1, ARN-2 (ADR-097);
EVT-1 (b) and ARN-1 (a) decide where the map events and the arena go in the plan.

## Heroes

<a id="hero-5"></a>
**HERO-5 Names and drafts.** Hero names, school names and specialties in `heroes.csv`; primary stat odds and starting values (ADR-055).

## Combat

<a id="cmb-11"></a>
**CMB-11 Siege rules** (ADR-099: sieges before 1.0). Decided so far: a Fort gives a wall and a gate, the Citadel a keep, the Castle towers
and stronger walls (ADR-066); 36 wide is the siege field (ADR-063); walls give 1/2 cover (combat.md); siege Ballistics (ADR-063: the
machine fires by itself at the fortifications without the skill, 2 hits in 3, 1–2 structure points, 3 points a structure when fully built).
(a) **When**: every battle at a town with a Fort, whether a hero is inside or only the garrison? A town without a Fort is an ordinary battle?
(b) **Walls**: how many sections (HoMM3: 4 sections and the gate), their points (2 at Fort, 3 at Castle?), and a broken section is a gap the
attackers walk through?
(c) **The gate**: the defenders pass, the attackers break it (as a wall section)?
(d) **Keep and towers**: the keep (Citadel) and two towers (Castle) shoot once a round each, damage growing with the town's buildings (HoMM3:
the number of buildings)? Can they be destroyed?
(e) **Moat**: HoMM3's moat (a creature entering stops and takes damage), with the Citadel? Or none?
(f) **The catapult**: HoMM3 gives every attacker a free catapult in a siege. Ours: each town has one offensive machine (one of them is the
catapult, ADR-056). A free siege catapult for every attacker, or only an offensive machine that fires at walls (Ballistics)?
(g) **Flyers and teleporters** cross the walls; shooters behind the walls get the 1/2 cover; the defender's shooters in towers?
(h) **The AI**: attacks a fortified town only when its estimate wins against the walls; defends behind them?
*Proposal*: (a) yes, yes; (b) 4 sections and the gate, 2 points each at Fort, 3 at Castle; a broken section is a gap; (c) yes; (d) yes, the
keep and towers shoot (base 10 damage + 2 per town building, the keep double), they can be destroyed (3 points); (e) a moat with the Citadel:
a creature entering it stops and takes 10 % of its health; (f) a free siege catapult (Ballistics drives it), the offensive machine as usual;
(g) yes, defenders' shooters may stand in the keep (one stack); (h) yes.

## Magic (Phase 5)

<a id="mag-13"></a>
**MAG-13 Guild selection and the spell list review** (ADR-099). [spells.md](../content/spells.md) has 17 spells a school (5 / 5 / 4 / 3 by
level); a guild offers 3 / 3 / 2 / 2.
(a) **Which spells a guild offers**: drawn per town when the game starts from the game's random source (HoMM3), or set by the map (the editor
may fix them, the rest drawn)?
(b) **Adventure spells** count as the level's spells in the draw, or does every guild level that has one always offer it (Town Portal in every
Academy guild 4)?
(c) **The extra-school buildings** (1 spell per guild level from each neighbouring school): drawn the same way?
(d) **The list itself**: anything to drop, rename or move (e.g. Earthquake in Nature for sieges, Bloodlust in Chaos, the summons), and the new
tags `holy` and `summon`?
(e) **Summoned creatures** (wolves, treants, elementals, imps, demons): reuse creature table rows, or their own rows?
*Proposal*: (a) drawn at game start, the editor may fix them; (b) counted in the draw; (c) yes; (d) as drafted; (e) existing creature rows
(Wolf as a new neutral, the Sylvan Treant, Conflux elementals, Inferno Imp and Demon).

## Game flow and content

<a id="flow-1"></a>
**FLOW-1 Campaign hooks** (Phase 6). How does a campaign hook into the victory and defeat conditions (ADR-073 sets the conditions)?

<a id="cont-1"></a>
**CONT-1 Lore names.** Working titles (Light, Darkness, Wild, Primordial) or the lore names (Dawn Covenant, Ashen Court, Unbound, Firstborn)?

<a id="cont-2"></a>
**CONT-2 Roster review.** Names, capabilities and numbers in `creatures.csv`. The size pass is done (ADR-074): 9 Large, the seven dragons and
the Behemoth line; whether a non-dragon line stays Large (Behemoths, Hydras or none) is decided once there are visuals.

<a id="cont-6"></a>
**CONT-6 The artifact re-draft review** (ADR-099). [artifacts.md](../content/artifacts.md): the placeholders renamed to lore items with the
same effects, 23 new items, 7 sets.
(a) **Names and flavour**: anything to change?
(b) **New effect kinds** (sight, experience, mana a day and maximum, creature health, school orbs, spells known while worn, no spells above
level 2, no retreat, surrender cost, war machines, Necromancy, growth, immunities): all wanted, and in which order?
(c) **Strong items**: the Unwritten Codex (every level 1–2 spell), the Wellspring Chalice (full mana each turn), the Cloak of Silence and the
Chain of the Unyielding (rules for both sides), the Banner of the Legion (growth): keep as Relics, make them Major, or drop?
(d) **Sets**: the two new sets (Shroud of the Last Debt for Necromancy, the Last Grove's Bow for shooters) and the Firstborn regalia (four
Relics); a set per town instead?
*Proposal*: (a) as drafted; (b) all, in the order sight, experience, mana, creature health, Necromancy, growth, no retreat, surrender, war
machines, then the magic ones with Phase 5; (c) keep as Relics, the Cloak and the Chain as Major; (d) as drafted.

<a id="cont-4"></a>
**CONT-4 More twists.** Which other systems should depart from HoMM3 ([departures.md](../design/departures.md))?

<a id="cont-5"></a>
**CONT-5 Primal Fury names.** One name for both towns, or a Swamp name? "Warcry" for Stronghold and "Spiritual Totems" for Swamp Bestiary, or one?

## New ideas (ADR-097, 2026-10-29)

<a id="arn-1"></a>
**ARN-1 Arena mode: the rules.** (ADR-097) A day of preparation, then one battle between two heroes decides the game (HotA).
(a) **When**: before 1.0 (it needs little that is new: a mode switch, combat places, the day-2 battle) or after? Shipped arena maps too?
(b) **Players**: two only, or also more with teams (HotA picks the attacker and one defender; the others just lose)? Hot-seat and against the
computer; later LAN (MP-1)?
(c) **Day 1 in hot-seat**: the players take turns, so the second player sees nothing of the first's turn only if the map keeps them apart.
Is "the map must keep them apart" enough, or are the players' areas hidden from each other on day 1 whatever the fog setting?
(d) **Preparation**: unlimited movement and any number of buildings on day 1, as HotA. Also: everything costs nothing, or only the map's
starting resources? A longer preparation (the map sets 1 to 3 days)?
(e) **Choosing the heroes**: HotA's rules (the defender among heroes on combat places; the strongest hero by army value; a hero in a town
when none is on the map)? Or each player marks the hero that fights?
(f) **The enemy shown**: HotA shows the enemy hero's army only roughly (Expert Disguise). We have no Disguise; show the enemy as fog shows
it (nothing outside sight), or the enemy hero's army roughly once day 2 starts?
(g) **The battle**: on the combat place's terrain; no experience; retreat and surrender allowed (the player who leaves loses), and the AI
never leaves? Auto combat (ADR-090) and quick combat as usual?
(h) **The end**: the battle's winner wins the game; a draw or no hero is a loss for both. The result screen offers "Play again" (the same
map, a new seed) and "Save replay" (UI-2)? Not ranked (FLOW-4)?
*Proposal*:
(a) before 1.0, after the Phase 3 exit; two shipped arena maps;
(b) two players (or two teams, each fighting with its strongest hero); hot-seat and against the computer, LAN later;
(c) the map keeps them apart; the validator warns when one player's heroes can reach the other's area on day 1;
(d) as HotA, the map's resources; preparation is one day;
(e) HotA's rules;
(f) fog as usual; on day 2 the battle screen shows both armies;
(g) yes; auto and quick combat as usual;
(h) yes, with "Play again" and "Save replay"; not ranked.

<a id="arn-2"></a>
**ARN-2 Arena maps and the editor.**
(a) **Map**: an "Arena" switch on the editor's conditions tab that removes the victory and loss conditions (HotA), and a **combat place**
object (a main cell and an attack cell with an obstacle on day 1)? Map format bump.
(b) **Choices on day 1**: HotA leaves them to ordinary objects (towns, dwellings, Pandora's Boxes, Seer's Huts, Witch Huts, universities).
Anything special for the arena, e.g. a "choose one of three heroes" object or a creature market with a budget?
(c) **Contents shown on day 1**: Pandora's Boxes (MAP-35), quests (EVT-5), Witch Huts' skills, Shrines' spells; anything else (creature
banks' guards, chests)?
(d) **Random arena maps**: an arena template for the generator (two halves, a town and objects each, a combat place in the middle), or
hand-made maps only?
(e) **Validator**: an error without a combat place; a warning when the players can meet on day 1; a warning for each map event that only
runs on later days?
*Proposal*: (a) yes; (b) nothing special: ordinary objects; (c) the HotA list plus Witch Huts and Shrines; (d) hand-made only for now;
(e) yes.

## New ideas (ADR-096, 2026-10-29)

<a id="map-35"></a>
**MAP-35 Pandora's Box.** (ADR-096) A box the hero is asked to open; it gives what the map author put in it and vanishes.
(a) **Contents**: HoMM3's list (gold, the other resources, experience, mana, morale and luck until the next battle, primary skills, secondary
skills, artifacts, spells, creatures)? Gains only, or losses too (HoMM3 allows taking resources away)?
(b) **Guards**: an army the hero fights before the box opens (HoMM3 has one), and the author's message?
(c) **Left open in the editor**: a box without contents is an error, or its contents are rolled when the game starts (as resource piles
and prisons, ADR-068)?
(d) **Hidden**: the player sees only "Pandora's Box" until it is opened (HoMM3)? The AI weighs it by its contents (it knows them)?
(e) **Random maps**: HoMM3's generator places boxes with 5000 … 20 000 gold, 5000 … 20 000 experience, creatures or spells. Ours too, by
value band in `TreasureSites`?
(f) **With map events** (EVT): a box may run a hero event instead of its list (HotA)?
*Proposal*:
(a) HoMM3's list, gains only (losses come with the map events); spells once magic exists (Phase 5);
(b) yes, an optional guard army fought after "open?", the box opened only when the hero wins, and an optional message;
(c) an error in the validator; the generator fills its own boxes;
(d) hidden from the players; the AI weighs the contents against the guards;
(e) yes: gold, experience and creatures by value band (spells after Phase 5);
(f) yes, once the map events exist.

<a id="evt-1"></a>
**EVT-1 Map events: scope, form and place in the plan.** (ADR-096) The author's events in the editor, like HotA's extended event system.
(a) **Scope**: the whole HotA system (four event types, variables, loops, running another event, quest stages), or a first set (hero,
player and town events with conditions, variables and the main actions), quests next, loops and "run another event" last?
(b) **When**: before 1.0, as content after the Phase 3 and 4 exits (single maps gain from it too), or with the campaign (Phase 6)?
(c) **Name**: "map events" in the docs and an "Events" tab in the editor (an "event" is already the code's pub/sub record and the weekly
event)?
(d) **Form**: a tree of actions and conditions stored as data in the map file and edited as a tree (HotA), or a small script language the
author types?
(e) **HoMM3's simple events**: HoMM3 has plain timed events (a day, a repeat, resources, which players) and plain event cells (a message,
rewards, guards, which players, "computer may trigger", "once"). Should the editor offer these as ready forms that write the same tree?
(f) **Campaign variables** ("kept for the next map", "taken from the last map"): recorded now and built with the campaign (FLOW-1)?
*Proposal*:
(a) all four types, in three steps: hero, player and town events with conditions, variables, messages, questions and the main actions;
then the quest objects and the quest log; then loops and "run another event";
(b) the Pandora's Box with its own list now (Phase 3 objects); the map events as one Phase 5 item, after the Phase 3 and 4 exits and
before the campaign, which builds on them;
(c) yes;
(d) a data tree (no script language: it stays deterministic, safe to load and easy to show in the editor);
(e) yes, "simple event" forms for timed events, event cells and town events;
(f) yes.

<a id="evt-2"></a>
**EVT-2 Triggers and event cells.**
(a) **Event cell**: invisible to the players, as in HoMM3, and ignored by route planning (it must not give itself away)? It runs when a hero
steps onto it; does the hero's route stop there always, or only when the event shows a message, a question or a battle?
(b) **Repeat**: an event cell runs on every step until the event disables or removes itself (HotA), with "once" as a ready form?
(c) **Who triggers**: only through conditions ("player is human", "current player"), as HotA, or also HoMM3's tick boxes (players, "computer
may trigger") on the cell?
(d) **Turn start**: player events and town events run after income, growth and the week's event, before the player gets control. Neutral
towns: at the start of each day, before the first player's turn?
(e) **Order**: several events at the same moment run in the editor's list order?
(f) **The screen**: a turn-start message of a human player shows when the turn begins; the camera moves to the town or hero it concerns?
*Proposal*: (a) invisible and ignored by routes; the route stops only when the event shows something or fights; (b) yes; (c) conditions,
with the tick boxes in the ready forms (they write the conditions); (d) yes, neutral towns at the start of the day; (e) yes; (f) yes.

<a id="evt-3"></a>
**EVT-3 Conditions, expressions and variables.**
(a) **HotA's list** (ADR-096) mapped to ours: the difficulty is the complexity (Beginner … Expert); "starting town" is the faction; secondary
skills have four levels with Master. Anything to drop?
(b) **More conditions** we have and HotA does not: the player's team, an ally, the week's event, the day of the week, the hero on the
surface or underground, the hero's attunement, mana?
(c) **Variables**: whole numbers, map-wide, with a starting value (HotA). Also per player?
(d) **Random numbers**: drawn from the game's random source, so a loaded game draws the same (HotA too)?
(e) **Records**: "monster defeated by player" and "hero defeated by player" need the game to remember who beat which map monster and hero.
Kept for every monster and hero, or only for those an event names?
*Proposal*: (a) take the whole list; (b) team, ally and the day of the week now, the rest on request; (c) map-wide only; (d) yes; (e) only for
those an event names.

<a id="evt-4"></a>
**EVT-4 Actions.**
(a) **HotA's list** (ADR-096) with our rules: secondary skills up to Master and only into a free slot; war machines are ours (ADR-056); a
building ignores bans and prerequisites and adds the prerequisites (HotA); spell book and spells wait for Phase 5. Anything to drop?
(b) **Losses**: HotA cannot lower experience, primary or secondary skills, but can take resources, creatures, artifacts, mana and movement.
The same?
(c) **More actions**: give or take a hero (like a prison), change a town's or mine's owner, reveal an area, move a hero, change a condition?
(d) **Battle**: the hero attacks the author's army on the cell's terrain. Won: experience and the event goes on; lost: the hero is lost and the
event stops (HotA). Retreat and surrender as in any battle, and the event stops?
(e) **A full army**: creatures given to a hero with no free slot: the hero chooses what to leave (a pending decision, HoMM3), or the
creatures that do not fit are lost?
(f) **Messages**: text only for now (no art), with lines for the rewards; images once there are icons?
*Proposal*: (a) the whole list; (b) the same; (c) reveal an area and give a hero now, the rest later; (d) yes; (e) the hero chooses; (f) yes.

<a id="evt-5"></a>
**EVT-5 Quests: Seer's Hut, Quest Guard, Quest Gate, quest log.**
(a) **Objects**: the three as in HotA? The Quest Gate is like the border gate (ADR-071) with a quest in place of a key.
(b) **HoMM3's quest forms** (reach a level, primary skills, defeat a hero, defeat a monster, bring artifacts, creatures or resources, be a
given hero, be a given player, a deadline day): ready forms that write the quest stages?
(c) **Items taken**: a completed HotA stage does not take what it asked for; the ready forms take it (bring resources: paid on completion)?
(d) **Quest log**: a screen per player (a key and a button) with each active quest's hint and object; kept in the game state?
(e) **Gates and routes**: a hero that meets a gate's quest passes without stopping (HotA's "free passage"); route planning treats a gate as
open only for a hero that meets it now?
(f) **Random maps**: the generator places Seer's Huts (bring an artifact or creatures for a reward) or none for now?
*Proposal*: (a) yes; (b) yes; (c) yes; (d) yes, Q opens it; (e) yes; (f) none for now.

<a id="evt-6"></a>
**EVT-6 The AI and the editor.**
(a) **AI**: as HotA: it answers questions by value (random numbers as their average), does Seer's Hut quests when the reward outweighs the
cost, takes Quest Guards and Gates as obstacles until it meets them, and does not look ahead at event cells, player or town events?
(b) **Editor**: an "Events" tab with the events by type, the tree of actions (add, copy, move, remove, undo through `MapEditorSession`),
the variables, and one event shared by several objects; an object's inspector picks or creates its event?
(c) **Validator**: unknown names, an event that runs itself (directly or through others), events and variables nobody uses (warnings), a
quest nobody can finish (warning where it can tell)?
(d) **Trying an event**: a "test" button in the editor that runs an event on a chosen hero, or test by playing the map?
*Proposal*: (a) yes; (b) yes; (c) yes; (d) by playing the map for now.

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
- **Combat**: CMB-5 (sieges before 1.0) ADR-099; grid and sizes ADR-005/021/070, battlefield ADR-033, turn order ADR-009, damage ADR-033, cover ADR-035, tuning ADR-038, speeds ADR-037,
  morale and luck ADR-053, machines ADR-056/063, victory ADR-057, CMB-1/3/4/6 ADR-070, CMB-7/8 ADR-071, CMB-6 check order ADR-072, CMB-2 ADR-075, CMB-9 (movement as a circle) ADR-089, CMB-10 (auto combat) ADR-090.
- **Magic**: MAG-1 (the spell list re-drafted) ADR-099; MAG-4 ADR-070/071; MAG-2, MAG-3 (mana refill, Spell Power role, potency), MAG-5 (levels), MAG-6 (resistance) ADR-071; MAG-1 (Spell Power formula), MAG-3, MAG-5, MAG-6 … MAG-12 ADR-072; MAG-5 (fixed 500 points) and MAG-10 (even stat odds) ADR-073.
- **Random maps**: ADR-023, ADR-034, ADR-064; RMG-1 … RMG-7 ADR-064/065; RMG-9, RMG-10, teams ADR-070; RMG-8, RMG-12, RMG-13 ADR-071; RMG-11 ADR-070/072.
- **Data**: DATA-1 ADR-071.
- **Presentation**: UI-1 (speed settings) ADR-090, built ADR-095.
- **LAN multiplayer**: MP-1 (before 1.0, host, clients that see only their view, lobby, saves) and MP-2 (simultaneous first week) ADR-098;
  MP-3 (clients' view, saves, smooth fog) and MP-4 (start zones) ADR-099.
- **Content**: CONT-3 (artifacts re-drafted) ADR-099.
- **New ideas** (ADR-077): TOWN-9 … TOWN-11, MAP-25, MAP-26, MAP-28 ADR-079; MAP-27, MAP-29 … MAP-34 ADR-080 (MAP-32 built after the week
  events).
- **Game flow**: victory ADR-057; FLOW-1 (conditions, deadlines, teams) and FLOW-2 (week and month events) ADR-073, the conditions built ADR-094; FLOW-2 event start and town-less players ADR-075; FLOW-3 (turn timer) ADR-091.
- **Assumptions** of ADR-070 … ADR-072: confirmed in ADR-073; of ADR-073: confirmed in ADR-075.
