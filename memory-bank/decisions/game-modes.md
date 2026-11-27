# Decisions: Game flow and modes

**Covers**: Victory and defeat, saves, the turn timer, score, ranking and difficulty, replays and the viewer, the main menu and new-game screen, the arena, the tournament.

**Current rules**: [docs/design/victory-and-modes.md](../../docs/design/victory-and-modes.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [057](#adr-057-victory-and-defeat) | Victory and defeat | accepted |
| [058](#adr-058-saved-games) | Saved games | accepted |
| [091](#adr-091-turn-timer-flow-3-2026-10-29) | Turn timer (FLOW-3, 2026-10-29) | implemented |
| [092](#adr-092-victory-screen-with-a-ranking-saved-replays-and-a-replay-viewer-2026-10-29) | Victory screen with a ranking, saved replays and a replay viewer (2026-10-29) | accepted |
| [094](#adr-094-capture-an-object-obtain-or-lose-an-artifact-release-a-prisoner-deadlines-implementation-2026-10-29) | Capture an object, obtain or lose an artifact, release a prisoner, deadlines (implementation, 2026-10-29) | implemented |
| [097](#adr-097-arena-mode-hota-like-2026-10-29) | Arena mode (HotA-like) (2026-10-29) | accepted |
| [101](#adr-101-victory-screen-ranking-replays-and-the-main-menu-flow-4-ui-2-2026-10-29) | Victory screen, ranking, replays and the main menu (FLOW-4, UI-2, 2026-10-29) | accepted |
| [102](#adr-102-score-and-ranking-core-implementation-2026-10-29) | Score and ranking core (implementation, 2026-10-29) | implemented |
| [103](#adr-103-the-maps-difficulty-and-the-victory-screen-implementation-2026-10-29) | The map's difficulty and the victory screen (implementation, 2026-10-29) | implemented |
| [104](#adr-104-the-main-menu-implementation-2026-10-29) | The main menu (implementation, 2026-10-29) | implemented |
| [134](#adr-134-new-game-screen-the-tier-1-dwelling-pick-and-the-hero-limit-adr-086-adr-068-rmg-7-2026-11-05) | New-game screen: the tier-1 dwelling pick and the hero limit (ADR-086, ADR-068, RMG-7, 2026-11-05) | implemented |
| [158](#adr-158-the-arenas-core-rules-adr-097-adr-100-arn-1-arn-2-2026-11-09) | The arena's core rules (ADR-097, ADR-100, ARN-1, ARN-2, 2026-11-09) | implemented |
| [159](#adr-159-the-arena-in-the-game-app-adr-097-adr-100-adr-158-arn-1-2026-11-09) | The arena in the game app (ADR-097, ADR-100, ADR-158, ARN-1, 2026-11-09) | implemented |
| [161](#adr-161-the-arena-in-the-map-editor-what-objects-show-on-day-1-adr-097-adr-100-adr-158-arn-2-2026-11-09) | The arena in the map editor; what objects show on day 1 (ADR-097, ADR-100, ADR-158, ARN-2, 2026-11-09) | implemented |
| [164](#adr-164-the-replay-viewer-adr-101-ui-2-2026-11-10) | The replay viewer (ADR-101, UI-2, 2026-11-10) | implemented |
| [180](#adr-180-the-three-shipped-arena-maps-adr-097-adr-100-adr-158-arn-2-2026-11-11) | The three shipped arena maps (ADR-097, ADR-100, ADR-158, ARN-2, 2026-11-11) | implemented |
| [185](#adr-185-play-from-here-on-the-adventure-map-adr-101-adr-164-2026-11-16) | "Play from here" on the adventure map (ADR-101, ADR-164, 2026-11-16) | implemented |
| [201](#adr-201-the-new-game-screen-has-tabs-by-kind-of-map-adr-086-adr-097-adr-101-adr-163-2026-11-17) | The new-game screen has tabs by kind of map (ADR-086, ADR-097, ADR-101, ADR-163, 2026-11-17) | implemented |
| [225](#adr-225-the-new-game-screens-options-tidied-difficulty-as-five-chess-pieces-the-calendar-on-the-status-bar-adr-063-adr-101-adr-201-adr-204-owner-notes-2026-11-19) | The new-game screen's options tidied; difficulty as five chess pieces; the calendar on the status bar (ADR-063, ADR-101, ADR-201, ADR-204, owner notes, 2026-11-19) | accepted |
| [227](#adr-227-building-adr-225-old-difficulty-names-player-colours-and-the-new-game-screens-players-adr-225-2026-11-19) | Building ADR-225: old difficulty names, player colours and the new-game screen's players (ADR-225, 2026-11-19) | accepted |
| [233](#adr-233-the-replay-viewer-takes-over-a-battle-played-in-one-command-the-computers-from-its-start-quick-combat-from-just-before-it-adr-164-adr-185-2026-11-19) | The replay viewer takes over a battle played in one command: the computer's from its start, quick combat from just before it (ADR-164, ADR-185, 2026-11-19) | implemented |
| [234](#adr-234-replays-measured-the-viewers-forward-jumps-play-on-unheard-instead-of-a-state-snapshot-adr-058-adr-164-2026-11-19) | Replays measured; the viewer's forward jumps play on unheard instead of a state snapshot (ADR-058, ADR-164, 2026-11-19) | implemented |
| [241](#adr-241-campaigns-maps-played-one-after-another-heroes-and-campaign-variables-carried-the-sample-campaign-border-watch-flow-1-adr-131-adr-126-2026-11-19) | Campaigns: maps played one after another, heroes and campaign variables carried, the sample campaign Border Watch (FLOW-1, ADR-131, ADR-126, 2026-11-19) | implemented |
| [366](#adr-366-tournament-mode-two-players-build-up-apart-then-one-battle-decides-owner-adr-097-adr-158-adr-197-2026-11-27) | Tournament mode: two players build up apart, then one battle decides (owner, ADR-097, ADR-158, ADR-197, 2026-11-27) | accepted |


### ADR-057: Victory and defeat
**Status**: accepted (2026-10-16), from the roadmap's game flow items and a user decision: the default is to defeat every
enemy, and a map can configure other conditions in the editor; save/load built (ADR-058); a defeated player's map
dwellings also turn neutral and its caravans vanish (ADR-054)
- **Default rules** (`ScenarioRules.Default`): a player with **no towns and no heroes is eliminated at once**, and the
  game is won when **one team is left standing**.
- **Grace period** (HoMM3's rule, `GraceDays`, default 7, 0 = off): a player that **owned a town and has none any more**
  is eliminated at the start of day *S + GraceDays* (*S* = the day it lost the last town) unless it owns a town again
  before; its heroes do not save it. `GracePeriodStarted(Player, DefeatDay)` and `GracePeriodEnded` tell the UI;
  `Player.TownlessSince` is the query. Players that never owned a town (hero-only starts) have no countdown.
- **Per scenario** (`GameState.Rules`, map format **v10**, editor tab *Conditions*, undoable with `SetScenarioRules`):
  - `GraceDays` sets the days a player without towns has (editor spin box, map field `graceDays`, omitted = 7).
  - `StandardVictory` and `Elimination` can each be switched off (`ScenarioRules.Endless` is a sandbox that never ends).
  - **Special victory conditions**, any one of which wins the game for the player's team: capture a town, defeat a hero
    (it is gone and its owner is an enemy of the player), gather an amount of one resource, survive until a day.
  - **Special defeat conditions**, any one of which eliminates the player: lose a town, lose a hero, a time limit (the
    player did not win by day N).
  - A condition may name one player; without one it applies to everybody and the first player it holds for, in turn order,
    wins. A defeat condition about a town or hero must name the player (the editor fills in the owner).
- **Who takes part**: only players that start with a hero or a town can be eliminated or win. A game that starts with a
  single team never ends by defeating enemies, which keeps one-sided tests and sandboxes running.
- **Order** (`GameFlowService.Evaluate`, after the game starts and after every accepted command): special victories, then
  defeats (special ones and the default), then the default victory. A battle in progress is settled first.
- **Defeat**: the player's heroes are removed (`HeroDefeated`), its towns and mines turn neutral, `PlayerDefeated` is
  published, its turns are skipped. A player defeated in its own turn passes it on at once.
- **The end**: `GameState.Outcome` (winners: the surviving players of the winning team; the reason in words) and
  `GameEnded`. From then on every command is refused ("The game is over"). The digest includes defeats and the outcome,
  so replays prove them.
- **Maps**: `MapDocument.Rules` is omitted for the default; older maps load with it. The validator reports conditions that
  point at a missing town, hero or player (errors), ones true at the start, and scenarios nobody can win (warnings).
- **UI**: the game shows a result screen (Victory, Defeat or Draw, the reason and the winners) after the battle screen, if
  one decided the game, has been closed.
- Open: conditions that need artifacts or
  objects (grail, flag all mines), team-wide survival, campaign hooks.

### ADR-058: Saved games
**Status**: accepted (2026-10-16), from the roadmap's save/load item and a user requirement: saving and loading must also
work during combat; replay times measured and the snapshot put off in ADR-234
- **A save is a game record** (ADR-036): the map as it was when the game started, the options (seed, fog) and every
  accepted command, plus a label (`SaveInfo`: name, UTC time, calendar date, whose turn, in battle, map name). The record
  document is version 2 (`GameRecordDocument.Save`; version 1 replays still load); saves use the extension
  `*.ewsave.json`. `GameSaves` writes (to a temp file, then moves it over the old one), lists (newest first; damaged files
  are listed with their error), loads and deletes.
- **Any moment works, a battle included**: battle commands are commands, so replaying the log lands in the same
  battle, in the same round, with the same active unit (`BattleView3D.Restore` shows it; a battle still choosing its width
  just opens when the width is chosen). The loaded state is proved against the saved `GameStateDigest`; a save made with
  other content (changed creature table) fails with that error instead of loading a different game.
- **No snapshot yet**: loading replays the whole game, which is fast for a game of a few hundred commands. If it gets too
  slow, a state snapshot taken every N days and replayed from is the planned way (roadmap).
- **The game starts from a map document**, taken right before `Start` (after the setup screen's team choice): the
  game app keeps it for saving. A loaded game keeps the map of its record, so saves can be saved again.
- **Game app**: F10 or the Menu button opens the list (name field, Save, Load, Delete); F5 quick-saves and F9 quick-loads
  (`quicksave`), on the adventure map and in battle. Saves live in `user://saves` (on Windows
  `%APPDATA%\Godot\app_userdata\Eternal Wars\saves`). The menu is also on the new-game screen, for loading.
  Loading rebuilds the world, HUD and battle scene from the loaded session; the battle HUD opens at the point it was
  saved. A game that is over can be saved and loaded too.
- **Autosave** (user decision: two slots): at the **start of every turn** (each player's turn, also at game start) the game
  app writes `autosave-current` and first moves the one that was current to `autosave-previous`, so the start of the
  latest turn and of the turn before are always there (`GameSaves.Autosave`; the new file is complete before the slots
  rotate). The save is made after the command that began the turn is logged and settled, from the new event
  `CommandExecuted(Command)` that `GameSession.Execute` publishes last: `TurnStarted` itself comes before the command
  is logged, and a save then would not match its digest. The slots are listed in the menu as "Autosave: start of the
  latest turn / of the turn before", cannot be written over by name, and loading one does not autosave. Games that are
  loaded resume without rewriting the slots until the next turn starts.
- Not covered: saving the 2D harness, a save from the editor-made `ewreplay` files, and saving games whose state changed
  outside commands (nothing does).

### ADR-091: Turn timer (FLOW-3, 2026-10-29)
**Status**: implemented (2026-10-29). Answers FLOW-3 (ADR-088): the owner accepted the proposal for (a), (e), (f) and (g) and changed (b), (c)
and (d).

- **The option** (f, g): on the new-game screen, next to the complexity: **off** (default) or **1 to 30 minutes** in steps of 1 minute. It is a
  game option (`GameOptions.TurnTimerMinutes`), so saves and replays keep it. It changes no rule and the digest does not show it.
- **A turn's time** (a): the same every turn, for human players only; nothing carries over. A loaded game starts the current turn with the full
  time (e).
- **Battles** (b, changed): the clock **stops during a battle between two human players** (hot-seat, later LAN). In a battle against a computer
  player or neutrals it runs on; if the time runs out there, the battle is fought to its end first (ADR-088) and the turn ends after it.
- **When the time is up** (c, changed): what is pending gets its screen's default, and then the turn ends:
  - a treasure chest gives **gold**;
  - where a screen offers to leave or stay, that is taken: a training site that may be declined, the cartographer, a deposit, a witch hut, a
    town gate; the exchange screen closes;
  - neutrals that offer to join or try to flee: the offer is taken (they join, or are let go), so no battle starts;
  - a **level-up** and a sphinx's riddle have no default: the player chooses, and the turn ends once the screen is closed;
  - a hero's move is one command, so a moving hero always finishes its move to the destination (it does not stop at the next tile).
- **Warnings** (d, changed): at **1 minute, 30, 15 and 5 seconds** left. The clock shows at the top of the screen, over the map and the battle
  scene; the last minute is red.
- **Replays need no clock** (f): the defaults and the end of the turn are ordinary commands in the log.
- Core: `TurnTimer` (Turns) runs on the time the front-end passes in (`Tick`), tells the warnings and ends the turn through `session.Execute`;
  its `Default` names each decision's default answer. The front-end holds the end of the turn while the battle scene (a battle's last moves, the
  result screen), the auto combat question or the menu is open, and closes the screens over the map when a turn starts.
- Front-end: the option on `NewGameScreen`, `TurnTimerPanel`, the clock in the game app's `_Process`. Tests: `TurnTimerTests` (warnings and the
  end of the turn, the next turn's full time; off by default and for computer players; a chest gives gold; a level-up waits for the player;
  the front-end holds the end; the clock stops between two humans; a battle against the computer is finished first; saves and replays keep the
  option). Smoke: `game-turn-timer` (1 minute, the hero screen open: the warnings, then Red's turn with the full time).

### ADR-092: Victory screen with a ranking, saved replays and a replay viewer (2026-10-29)
**Status**: accepted as a design (2026-10-29), owner notes in `ideas.md`; **nothing implemented**. FLOW-4 and UI-2 answered in ADR-101.

- **Victory screen with a score** (FLOW-4): a won game on a map that is **not campaign-like** ends on a HoMM3-like victory screen: the days
  taken, the difficulty, a score and a rank title. Today's result screen (ADR-057) only says who won and why.
- **Ranking tables**: the score goes into a table of the best games. **Random maps** and the **maps shipped with the game** have separate
  tables. A row shows the map's name (the template's for a random map), the difficulty, the days taken, the score and more (FLOW-4).
- **Save the replay**: the ranking table offers to save a game's replay (owner note, revised the same day; at first the end screen did).
- **Main menu** (UI-2): "Watch a replay", which plays a replay back on the map and in battles, and "Leaderboard", which opens the ranking
  tables. Watching works for a finished game's replay **and for an unfinished game's save**, up to the moment it was saved.
- Fits the core as it is: a save is already the game's record (the start map, the options and every command, ADR-058), so a replay file is
  that record, and playing it back is `session.Execute` of each command in turn while the views show the events. The score needs the days
  taken (`GameState.Date`), the difficulty and how the map was made (`GenerationInfo` for random maps). There is no main menu yet: the game
  app opens on the new-game screen, with the F10 menu for loading.

### ADR-094: Capture an object, obtain or lose an artifact, release a prisoner, deadlines (implementation, 2026-10-29)
**Status**: implemented (2026-10-29). Carries out ADR-073's remaining victory and defeat conditions (FLOW-1; its assumptions were confirmed in
ADR-075). Map format **v25**. Choices made here are marked *(choice)*.

- **New kinds** (`ConditionKind`, appended so older maps keep their numbers):
  - **Capture a map object** (`CaptureObject`, victory): the player's team owns the object `Target`. Any object with a flag counts: mine,
    creature dwelling, resource provider, garrison, town *(choice: the editor lists the non-town ones; towns keep "capture a town")*.
  - **Obtain an artifact** (`ObtainArtifact`, victory): a hero of the player's **team** carries `Artifact`, worn or in the backpack *(choice:
    the team, as "capture a town" does)*.
  - **Release a prisoner** (`ReleaseHero`, victory): the player's team frees the hero held in the prison `Target`. Freeing removes the prison,
    so `GameFlowService` remembers who sits in it (also a prisoner the map left open, drawn at the start) and looks for that roster hero among
    the team's heroes once the prison is gone. A prisoner freed by an enemy makes the condition impossible; nothing else happens *(choice)*.
  - **Lose an artifact** (`LoseArtifact`, defeat): no hero of the player carries `Artifact` any more. Like "lose a town", it needs the player
    who loses.
- **Deadlines**: every victory condition but "survive until a day" may carry `Deadline`, an absolute day. Met on that day or before, it wins as
  usual; on the day after, the players it applies to are defeated ("Missed the deadline: …"), all of them when it names no player
  *(choice; HoMM3-like)*. The plain "time limit" defeat stays.
- **Map format v25**: `ConditionDocument` gains `target` (an object or prison id), `artifact` and `deadline`; older maps load unchanged.
- **Validator**: errors for an object nobody can own, a target that is not a prison, an unknown artifact, "lose an artifact" without a player,
  a deadline on "survive" or before day 2; warnings for an object or artifact already owned or carried at the start (the game would end at
  once) and an artifact the losing player does not carry at the start (it would lose at once).
- **Editor**: the conditions panel offers the four kinds with an object, prison or artifact list and a "by day" field for the deadline (0 = none);
  "lose an artifact" without a chosen player goes to the player whose hero carries it.
- Tests: `GameFlowTests` (each kind wins or defeats; a prisoner named by the map or drawn at the start; a deadline missed and met; the map file
  round trip and an older version; the validator).

### ADR-097: Arena mode (HotA-like) (2026-10-29)
**Status**: accepted as a design (2026-10-29), owner note in `ideas.md`; **nothing implemented**. The open points are ARN-1 and ARN-2 in
`open-questions.md`, each with a proposal. Reference: the "Arena mode" chapter of the HotA documentation (h3hota.com/en/documentation).

- **The mode** (HotA): a game is **one battle between two heroes**. The map sets the armies, skills, artifacts and objects, and may let the
  players choose some of it.
- **Day 1 is preparation**: heroes have (nearly) unlimited movement, towns may build any number of buildings, and some objects show what they
  hold (Pandora's Boxes, quests, scrolls, a university's skills). Players visit objects, hire armies and get their heroes ready. The map must
  keep the players from meeting on day 1.
- **Day 2 is the battle**, which starts by itself: the map has **combat places** (a new object: a main cell where the defending hero stands
  and an attack cell, blocked on day 1, from where the attacker comes). The defender is chosen among the owners of heroes standing on combat
  places (else the first player in turn order is the attacker and the next non-allied one the defender); each side fights with its strongest
  hero by army value; heroes are moved to the combat place. The **winner of the battle wins the game**; no winner, or no hero, is a loss for
  both.
- **Other rules**: battles give no experience; the AI never retreats or surrenders; the enemy hero's army is shown only roughly (Expert
  Disguise); map event messages for players and towns are not shown; the game is not ranked.
- **Map**: an "Arena" switch on the map; it removes the victory and loss conditions; a map without a combat place is faulty.
- **Fits the core as it is**: the hero against hero battle, quick and auto combat (ADR-090), the AI's turn and army values (`BattleEstimator`)
  exist. New are the mode switch in the map, the combat place object, unlimited movement and building on day 1 (today one building a town a
  day, ADR-046), the automatic battle at the start of day 2 and its result as the game's end (ADR-057). There is no sea (ADR-051), so HotA's
  boat case does not arise. Related: MAP-35 and EVT-5 (contents shown), FLOW-4 (not ranked), MP-2 (simultaneous turns on day 1).

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

### ADR-164: The replay viewer (ADR-101, UI-2, 2026-11-10)
**Status**: implemented (2026-11-10); core tested, the Godot parts run in two smoke scenarios; a battle only the computer fights is taken over
since ADR-233; forward jumps play on the watched game since ADR-234. Carries out ADR-101's viewer. *(choice)* marks the
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

### ADR-180: The three shipped arena maps (ADR-097, ADR-100, ADR-158, ARN-2, 2026-11-11)
**Status**: implemented (2026-11-11): the roadmap's last arena item before LAN. Hand-made, in code like Ember March (`Scenarios/`); no arena
template for the generator (ADR-100). No rule or map format change. Layouts and numbers are *(proposal)*, for play-testing.

- **Proving Grounds** (34 × 18, two players): each player's **random town** and **random hero** take the faction picked on the new-game
  screen (ADR-126). The halves mirror each other left to right behind a rock wall; the combat place is in a sealed glade in the wall (grass).
  Each half: two gold piles, a chest, two Pandora's Boxes (experience; trolls behind a wolf guard), a Witch Hut (Leadership, Armor), a level 1
  Shrine, an Arena, a Temple, a Minor artifact, a Major one behind wolves.
- **Frostfang Pass** (24 × 30, two players, **no towns**): a Haven mage (Ansel Wren, Blue, the snowy north) against a Necropolis mage (Kael
  Duskmire, Red, the wasted south), each with a small army, buying the rest at four **map dwellings** of its faction (tiers 2, 3, 2, 4) with
  10 000 gold lying about, plus a war machine factory, Shrines of levels 1 and 2, three Pandora's Boxes (stats, a tier-3 stack of its
  faction, experience), a Witch Hut, Star Axis, Marletto Tower, a Learning Stone and artifacts. The halves mirror each other top to bottom;
  the vertical combat place (the attacker from the north) is sealed in the ridge, on snow.
- **Four Banners** (40 × 24, **two against two**): Blue and Green share the west, Red and Yellow the east, each player with a random town
  and hero. Allies share their half's objects and may trade troops; each side fights with its strongest hero (ADR-158). Each half has **its
  own combat place**: the first player in turn order with a hero standing on one defends there; without one, Blue attacks at the west's.
- **The same start for everyone** *(proposal)*: a random hero normally brings its own starter stack, which ranges from three tier-4
  creatures to nine tier-2 ones (an army value of about 10 000 against 1 500 in one test game). So the random heroes on these maps start with a
  fixed neutral army instead (wolves and bandits), and the factions differ only by their towns.
- **New-game screen**: the three come after Ember March, in the order above; they count as shipped maps in the ranking's split (never
  ranked themselves, ADR-158).
- **Validator fixes** found on the way: a hero placeholder without an army is no longer warned about (it gets its drawn hero's starter
  stack); "starts without a town and loses after N days" is not given on an arena map (an arena eliminates nobody).
- **Code**: `ProvingGroundsScenario`, `FrostfangPassScenario`, `FourBannersScenario`, listed by `ArenaScenarios.All`; `MapSketch` builds a
  map from rows of terrain letters and places one side's layout mirrored for the other. Tests `ArenaMapsTests` (no validator errors or
  warnings, round trip, every object reachable once its guards are beaten, the random towns and heroes, the teams, two computer players
  fighting day 2's battle with stronger champions than they started with); smoke `game-arena-shipped`.
- **Found, not done**: on Four Banners the computer does not share with its ally. The first player of a side picks up most of the half and
  its ally builds but may buy nothing, so the side's champion can be a hero that did little on day 1. A computer battle on day 1 waits on its
  result window as on any map.

### ADR-185: "Play from here" on the adventure map (ADR-101, ADR-164, 2026-11-16)
**Status**: implemented (2026-11-16); core tested, the Godot part runs in a smoke scenario. Carries out the map half of UI-2 f, which ADR-101
put off ("until then a save is played on with Load"). *(choice)* marks the agent's choices.

- **Core** (`ReplayPlayer`): `CanPlayOn` outside a battle (a battle being fought is `CanTakeOver`'s), while the game is not over and a person
  is still in it (not defeated); `PlayOn` gives a new session replayed from the record to the current command, so its command log is the record
  cut there and it saves, autosaves and loads like any game. The record and the watched session do not change. `PersonToShow(state)`: the
  current player when a person, else the next person in turn order still in the game.
- **The viewer**: "Play from here" is on the bar on the map too (disabled, with the reason, when the game is over or only computers are left;
  the tooltip says which kind it is). On the map it **leaves the viewer**: the game goes on from that command as the player's own, exactly as if
  a save cut there were loaded: saves, autosaves (into the usual slots), the end screen, "Save replay" and the ranking as for any loaded game
  *(choice: it is what Load of such a save already allows; the battle take-over stays unranked because it is a side game that returns)*. There
  is no way back to the replay from it *(choice)*; the main menu's "Watch a replay" opens it again. The players stay as recorded (people play,
  computers are the AI); on a computer's turn the AI plays on first and the map is seen through `PersonToShow`'s eyes.
- Smoke: `game-replay-play-on` (the demo record paused, next day, Play from here, End turn passes to Red). `game-replay-viewer`'s "Skip to the
  result" moved to frame 90: Red now reaches the wolves later than frame 70.
- Tests: `ReplayPlayerTests` (playing on from day 2: the same state, the log is the record cut there, the replay unchanged, its save
  replays to the same state; not offered in a battle or with only computers; whose eyes).
- **Not done**: taking over a battle only the computer fights (ADR-164).

### ADR-201: The new-game screen has tabs by kind of map (ADR-086, ADR-097, ADR-101, ADR-163, 2026-11-17)
**Status**: implemented. No rule, save or map format changes.

- **Four tabs** (`NewGameScreen`), each with its map count: **Scenarios**, **Random maps** (the shipped and the editor's valid templates),
  **Arenas** and **Campaign**. Each lists its maps on the left (a button a map, scrolled when long) and shows the chosen one on the right:
  its name, a summary (size and levels, or a template's player range and sizes; players; the team counts), the map's or template's
  description, the arena line, a template's size / players / hero limit, then the players. The game options (fog and refog margin,
  complexity and turn timer, seed with "New seed") sit in two columns under them for every tab. The screen is about 1240 px wide (1120
  beside a LAN lobby's chat). Each tab remembers the map last chosen on it; an empty tab says how its maps come to be and Start is off.
- **Which tab a map is on** (`MapKinds.Of`, Core): an arena map is an arena; a map is **campaign-like** when it would not be ranked for
  something other than being an arena (a person starts without a town, ADR-075, or a map event favours people, ADR-101) or when it
  keeps or takes campaign variables (EVT-1 f); anything else is a scenario. The shipped maps are placed by hand (`NewGameChoices`):
  Demo Valley, Two Towns and Ember March under Scenarios (Demo Valley has no towns, which the rule would read as campaign-like), the
  three arenas under Arenas. A map file that does not load goes under Scenarios, where choosing it says why.
- **Opens on** the tab of the first map, so a map given with `--map` is chosen and Enter starts it, as before; otherwise on Scenarios.
- **Smoke scripts** press a tab by part of its label (`press:Arenas`) and click a map by name (`click:@Four Banners`).
- **Not done**: campaigns themselves (Phase 6: chaining maps, carried heroes); the Campaign tab lists single campaign-like maps until then.

### ADR-225: The new-game screen's options tidied; difficulty as five chess pieces; the calendar on the status bar (ADR-063, ADR-101, ADR-201, ADR-204, owner notes, 2026-11-19)
**Status**: accepted and built (2026-11-19; ADR-227). From the owner's notes in `ideas.md`; *(proposal)* marks the agent's fill-ins, for the owner
to change. Map format **v41** (difficulty names); saves and the ranking file read the old names too.

- **Game difficulty** (replaces "Complexity" on the screen and in the code: `Complexity` → `Difficulty` *(proposal)*): the levels are
  **Easy, Normal, Hard, Expert, Master**, HoMM3's five steps. The old names move up one by position, so every level keeps its numbers:
  Beginner → Easy, Easy → Normal, Normal → Hard, Hard → Expert, Expert → Master. Starting resources (towns-and-economy.md) and the score
  factors (Easy 80 %, Normal 100 %, Hard 130 %, Expert 160 %, Master 200 %) are unchanged per level. `MapMigrations` renames
  `rules.difficulty` in maps before v41; saves and `ranking` files map the old names when they read them *(proposal)*.
- **A row of five square buttons** with **chess pieces** (pawn, knight, bishop, rook, queen; [icon-prompts.md](../../docs/content/prompts/icon-prompts.md),
  sheet 29, replacing ADR-204's helmets), one pressed at a time. Hovering one puts its name and what it means (the starting gold and
  resources, the score factor) on the **status bar**; the gold no longer stands in the labels. **No "Map default" button**:
  - **A map** preselects its own difficulty (`rules.difficulty`, set in the editor's conditions panel), **Normal** when it sets none. The
    player may pick another *(proposal: the map's difficulty is a default, not a lock)*. The map's level keeps the map's own starting
    resources (the game is sent with no difficulty, as "Map default" was); another level sets that level's resources *(proposal)*. The
    editor's "Difficulty" row says "Normal" for not set.
  - **A random map**: Normal preselected; the chosen level sets the resources and is the generated map's difficulty.
  - A map event's "difficulty" quantity reads the game's level, else the map's, else Normal (now 1, was 2 for "none").
- **Fog of war**: the choices read **Permanent reveal** and **Refog**. The **refog margin row goes**; a game started from the screen uses the
  default margin (2); the tools may still set it.
- **Turn timer**: a **checkbox**; ticked, a number box beside it takes the minutes (1–30) *(proposal: 5 when first ticked)*.
- **Seed**: the field and "New seed" are shown only in **debug builds** (`OS.IsDebugBuild()`); a release build draws a new seed for every
  game, still kept in saves and replays.
- **Layout**: the **hero limit** row moves into the game options beside the fog (shown when the hero limit applies, as now).
- **Players on a random map**: the "Players" number box goes. A **"+"** icon button under the players list adds the next slot (to the
  template's maximum); a **"−"** icon button on each player's row removes that player (to the template's minimum). Hidden at the limits.
- **Player colour**: each player row gets a colour choice (a swatch opening the eight colours); picking a colour another player has swaps
  the two *(proposal)*. Needs a player's colour apart from its `PlayerId` *(proposal: `Player.Colour`, set at the start, kept in saves;
  `Palette.ForPlayer` reads it)*; core change with tests.
- **Town choice**: the faction names give way to their **crests** (ADR-211): the row shows the chosen crest ("?" for Random); clicking it
  opens a **2 × 4 panel** of the eight towns' crests *(proposal: with a "Random" button under it)*. Hovering a crest puts the town's name on
  the status bar.
- **Close, not Back**: the new-game screen's "Back (Esc)" button goes; a 32 px **"×"** icon button stands in its **top-right corner** (Esc
  still closes). **Everywhere** an "×" (`UiIcon.Close`, icon-only) closes a window or screen, it stands in that window's top-right corner
  (the hero, town, tavern, marketplace, dwelling, exchange, spell book, kingdom, quest log, puzzle map, thieves' guild, object and intel
  windows). Text buttons along a window's bottom (ADR-204) stay as they are *(proposal: the other screens before a game keep their Back
  for now)*.
- **Adventure map**: the **calendar** (the date, the week's event) moves onto the **status bar, right of the resources**, in both side-panel
  modes *(proposal: the date short, the week's event in its hover text)*; the **"Turn: <player>" label goes**. End turn stays where
  ADR-204 put it.

### ADR-227: Building ADR-225: old difficulty names, player colours and the new-game screen's players (ADR-225, 2026-11-19)
**Status**: accepted (2026-11-19).

- **The rename**: `Complexity` is `Difficulty` (Easy … Master) in the code, the files and the sim tool (`sim game --difficulty`). Each level
  keeps its place, so its number, starting resources and score factor stay. Maps before **v41** (`MapMigrations`), saves and replays
  before **v5** (`options.complexity` becomes `options.difficulty`) and ranking files without a `formatVersion` (now **2**) have their
  names moved up one when read (`DifficultyNames`). `DifficultyText` describes a level for the status bar.
- **The map's own level sets the start too** (changes ADR-225's proposal): a map file has no starting resources of its own, so a game sent
  with no difficulty would start everyone with nothing. The old "Map default" never did that: its item had id −1, which Godot's
  `OptionButton` replaces with the index, 0, so it sent Beginner. The screen now always sends the level pressed, the map's own difficulty
  (else Normal) preselected. A game created without one (tools, tests) counts as the map's difficulty, else Normal, in the score and in a
  map event's "difficulty" (now 1 for none).
- **`Player.Colour`** (0–7, by default the number's) comes from `GameOptions.Colours` (player number → colour) when the game is created,
  so saves, replays and a LAN client's copy have it. No rule and no digest reads it. A player still named after its number's colour
  takes the chosen colour's name; a name the map gave stays. `Palette.ForPlayer(state, player)` draws it; `Palette.ForSlot` draws a slot
  where no game has colours yet (the editor, a lobby's or a save's seats).
- **A random map's players**: "−" removes that row, and the players after it move up a slot with their choices: controller where the slot
  allows it, town, hero, dwelling, team, colour. "+" adds a row with the slot's defaults, the first colour nobody has and, if its team is
  taken, the first free team.
- **Smaller choices**:
  - Ticking the turn timer focuses its minutes, selected, ready to type.
  - A release build draws a new seed each time "Start" is pressed.
  - The calendar reads `M1 W2 D3`; its hover text gives the full date and the week's event.
  - The town and colour choices open a panel under their button, in the screen's own layer, so the status bar names what the pointer is over.
  - Until the difficulty pieces are drawn, the level's name stands in on its button; the victory screen and the leaderboard then show the
    name alone.

### ADR-233: The replay viewer takes over a battle played in one command: the computer's from its start, quick combat from just before it (ADR-164, ADR-185, 2026-11-19)
**Status**: implemented (2026-11-19); core tested, the Godot part runs in two smoke scenarios. Carries out what ADR-164 and ADR-185 left
("taking over a battle only the computer fights"). *(choice)* marks the agent's choices.

- **Why it was not offered**: the core fights a battle nobody commands within the command that starts it (a computer hero's move), and
  quick combat plays a person's battle out in one command; the viewer then shows the battle move by move from its events, but the record
  has no point between those moves to go on from.
- **A battle only the computer fought** (`ReplayPlayer.TakesOverFromTheStart`): `Open` notes each command whose first battle had no person
  on either side and was over when the command ended, with the attacker's player. "Play from here", while that battle is shown, plays it
  **again from its start** in a separate game: replayed to that command, a person plays the attacker's player from then on
  (`HandToPersonCommand`), and the command runs again, so the battle waits for the person. The person commands the **attacker**; the
  defender (neutrals, or another computer player) stays the computer's *(choice)*. A battlefield width the person could now choose is the one
  the computer fought on (the default) *(choice: the viewer cannot answer a width question)*; a deployment the person's Tactics now allows is
  the person's. The status line says whose side is played. Should the command not start a battle the person commands (a map event that asks
  people only, say), the viewer says so and stays where it was.
- **A battle quick combat ended** (`TakesOverBeforeItsEnd`): while it is shown, "Play from here" goes on from just before the command that
  ended it, as in a battle being fought (ADR-164); so does a battle a person commanded whose last blow was the last command. The recorded
  games of the smoke scenarios (the AI playing people's colours) end their battles so.
- **`HandToPersonCommand`** (`handToPerson` in records): the mirror of `HandToComputerCommand`: a person plays the player from now on
  (`PlayerHandedToPerson`). Given on the player's own turn; refused for a player a person plays already and for one that is out. A LAN
  client's is refused like its `HandToComputerCommand`; only the viewer's separate game gives it. No rule changes; older records load.
- **The bar**: "Play from here" in such a battle; its tooltip says that a computer's battle starts again, commanding the attacker.
- `sim record --computer` makes Red a computer player, so its battles are fought within one command. Smoke: `game-replay-take-over` (the
  sim's record: Red's battle after quick combat, played on by hand, quick combat, back to the replay) and `game-replay-computer-battle`
  (the `--computer` record: Red's battle again from its start, as Red).
- Tests: `ReplayPlayerTests` (a computer's battle taken over from its start with the person on the attacker's side, the replay unchanged,
  only right after its command; a battle quick combat ended taken over from just before it; `HandToPersonCommand` and its refusals; the
  controller's status line).

### ADR-234: Replays measured; the viewer's forward jumps play on unheard instead of a state snapshot (ADR-058, ADR-164, 2026-11-19)
**Status**: implemented (2026-11-19); core tested. Answers the roadmap's "a state snapshot if replays get too slow" (ADR-058) with a
measurement and the owner's choice. No rule, save or map format change.

- **Measured** (Release, `sim replay`, which now prints the time to read and to replay): a duel to month 3 (1013 commands) replays in
  1.6 s; eight computer players on the largest map (Eight kingdoms, 192 with underground) to month 5 (6596 commands) in 5.0 s, plus 0.4 s to
  read the file. The time grows with the game's length (about 10 s at month 10). Loading a save pays it once; the replay viewer paid it on
  opening and again on **every jump**, each replayed from day 1.
- **Chosen** (owner): make the viewer's jumps fast; no state snapshot in saves for now. A snapshot means writing and reading back every
  piece of the game exactly, the random numbers included, with its own format version: it waits until loading itself is too slow.
- **A jump forward plays on the watched game**, with its events unheard by the views (`ReplayPlayer.Seek`), so it costs only the commands it
  skips: in the eight-player game at day 100, "Next day" takes about 0.15 s and "Next turn" 0.05 s where each took the whole replay (changes
  ADR-164's choice to rebuild forward jumps too). The views are built again on the state afterwards, as before. A jump **back** still
  replays from the start to that point (its cost grows with the day jumped to, not the record's length) *(choice: a checkpoint kept in
  memory needs a copy of the game state, which is the snapshot work)*. `ReplayPlayer.Reload` puts the record's game back when the watched
  one differs from it.
- **`GameSession.Events` is a bus of its own** for everyone outside the rules (views, tools, a LAN host), fed by the rules' bus after every
  rule has heard an event, so the order of events is unchanged. `GameSession.Unheard()` leaves those subscribers out until its scope ends
  (scopes nest); the rules still hear everything, so the game moves on exactly as when heard.
- Tests: `UnheardEventsTests` (nothing heard outside while the rules still move the fog; nesting), `ReplayPlayerTests` (a jump forward keeps
  the watched session, is unheard and reaches the recorded state; a jump back and `Reload` replay to the same state).

### ADR-241: Campaigns: maps played one after another, heroes and campaign variables carried, the sample campaign Border Watch (FLOW-1, ADR-131, ADR-126, 2026-11-19)
**Status**: implemented. Answers FLOW-1. Save format v6 (`campaign`, the options' `carriedHeroes`); older saves load. Points marked *(choice)* are
the agent's and stand unless the owner changes them.

- **A campaign** (`Campaigns/Campaign`): a name, a description and its maps in order (`CampaignMap`: the map, a title, an intro shown before it,
  an outro shown once it is won). Played by one person: the map's first human player (`CampaignRules.PlayerOf`). The difficulty is chosen when
  the campaign begins and kept for every map.
- **Victory and defeat** (FLOW-1): a campaign map ends by its own conditions, or by a map event (ADR-242). The person's team winning goes on to
  the next map; any other end offers "Try again", the map from its start with what was carried in and a new seed, or the main menu.
  *(choice: no "best of" or branching maps; a campaign is a straight line.)*
- **Carried heroes**: the next map chooses which heroes come in: the roster heroes it names (`CarryHeroes`, when the person still has them),
  then its `CarryStrongest` strongest others (level, then experience), with or without their armies (`KeepArmy`, default off) and artifacts
  (`KeepArtifacts`, default on). A hero keeps its level, experience, primary stats, skills, spell book and spells with their ranks, and war
  machines (`CarriedHero`, captured by `CampaignRules.Capture`). Only roster heroes carry. *(choice: what HoMM3 lets a campaign choose, without
  its per-map bonus choice.)*
- **Where a carried hero goes** (`CarriedHeroService`, before the hero placeholders of ADR-126): the person's hero of the same roster hero, else
  the person's first hero placeholder not yet used. A carried hero the map has no place for, or whose roster hero is another player's or in a
  prison, waits (`GameState.CarriedWaiting`) and can come into a later map. Carried in through `GameOptions.CarriedHeroes`, so a save or replay
  of the map starts the same.
- **Campaign variables** (ADR-131): a won map's kept variables are written over those carried so far, so a later map can take a value set
  several maps before.
- **Never ranked**: a game whose options carry campaign variables or heroes is a campaign map (`CampaignRules.InCampaign`) and is not ranked.
- **Saves** (record v6): a campaign map's save keeps the campaign (`CampaignRecord`: where it comes from, and the progress when the map began),
  so a loaded save goes on with its campaign once the map is won; a campaign file that is gone leaves the map playing on its own.
- **Campaign files** (`*.ewcampaign.json`, version 1, `CampaignSerializer`): the campaign's fields, each map naming a map file in the campaign
  file's folder. The game lists the shipped campaign, then the files in its `user://campaigns` and in the map editor's maps folder.
  *(choice: written by hand for now; no campaign editor.)*
- **Border Watch** (`Scenarios/BorderWatchCampaign`, placeholder story): two small maps, Haven against a Necropolis vanguard. Maren Holt holds
  the Grey Ford, may tend wounded scouts (the kept variable `scouts`) and takes the undead outpost (a captured event ends the map); she is
  carried with her army into the Ashen Gate through a hero placeholder, where the scouts bring archers on day 1 and taking the gate wins.
  Each map also plays on its own.
- **Game app**: Campaign in the main menu opens `CampaignScreen` (the campaigns, the chosen one's maps, the difficulty); `CampaignStoryScreen`
  shows the intro before each map, the outro after it and the days each map took at the end; the result screen offers "Next map", "Finish the
  campaign" or "Try again" (`GameResultPanel.Campaign`). Smoke scenario `game-campaign`.
- **Not done**: a campaign editor, cutscenes, per-map starting bonuses, the prologue map (CAMP-1, its own roadmap item).

### ADR-366: Tournament mode: two players build up apart, then one battle decides (owner, ADR-097, ADR-158, ADR-197, 2026-11-27)
**Status**: accepted (2026-11-27), being built: the switch, the length and their checks; the game flow to the deciding battle; the battle's rules; isolation; tournament templates, the generator's tournament, the shipped Twin Vales and the map validator's checks. [tournament-plan.md](../../docs/papers/tournament-plan.md) holds the guide and the plan; the
roadmap's "Tournament mode" section tracks it. The owner answered TRN-1 to TRN-9 the same day (below).

- **The owner's request**:
  - A new game mode for two players, with at most one hero each.
  - The length is set when the game is created (such as 2 weeks or 1 month).
  - Each player plays against the map in their own zone and cannot reach the other.
  - When the time is up, the two heroes meet in one battle with no retreat and no surrender, and its winner wins the game.
  - Over the LAN every day is played at once.
  - It is offered against the computer, hot-seat and over the LAN.
- **Built on what exists**:
  - The arena's deciding battle (ADR-158), result screen (ADR-159), last-day AI (ADR-160) and LAN battle (ADR-199).
  - Start zones and the simultaneous days (ADR-197, ADR-217), here for every day instead of the first week.
  - The hero limit (ADR-068), fixed at 1.
  - Mirrored templates (ADR-117), for zones with the same content.
- **Rules** (the plan's guide):
  - A map switch `rules.tournament` and a game option for the length.
  - The map's conditions do not count.
  - The zones are cut off, and sight, reveals and the moving and showing spells stop at the zone's edge.
  - What players would share (tavern pools, the Grail) becomes each player's own.
  - On the day after the last building day, the heroes fight on neutral ground, with no retreat, no surrender and no experience.
  - The winner wins. A side without a hero or army loses; both such sides, or both armies fallen, is a draw. Never ranked.
- **Why**: a mode where both players race to the strongest army in the same time and on the same ground, decided by one fight. It suits a
  short LAN evening, because the players never wait on each other's turns.
- **The owner's answers** (TRN-1 to TRN-9, 2026-11-27): the proposals, except where noted.
  - **Maps** (TRN-1): tournament templates first, then the editor's switch for hand-made maps, checked by the validator.
  - **Lengths** (TRN-2, the owner's own): 1 to 14 days a day at a time, then whole weeks up to 12 (3 months, the upper limit); the default
    is 14 days.
  - **The battle** (TRN-3): on neutral ground with the terrain and obstacles from the seed, never a siege; a coin toss from the game's random
    numbers picks the attacker and is announced; the hero fights with its own army only.
  - **A draw** (TRN-4): no winner, as in the arena.
  - **A lost hero** (TRN-5): another may be hired, still one at a time, from the player's own tavern pool.
  - **What players know** (TRN-6): the opponent's name, colour and town; on the LAN, when the opponent ended the day; nothing shows the
    other zone.
  - **Battles at the same time** (TRN-7, widened by the owner): per-player battle and question slots on **every** simultaneous day, not only
    in tournaments: an ordinary LAN game's first week and an arena's preparation day too.
  - **Shared map content** (TRN-8): a tavern pool per player; one Grail per zone (none without obelisks); artifact rolls mirrored per zone;
    the week's events and rumours shared.
  - **Score** (TRN-9): not ranked; "Play again" and "Save replay".
