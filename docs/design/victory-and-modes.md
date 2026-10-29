# Victory, defeat and game modes

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../project/open-questions.md).

## Victory and defeat (implemented, ADR-057)
- By default a player with no towns and no heroes is out at once, a player that lost its last town has 7 days
  (configurable) to own one again, and the last team standing wins.
- A map can switch these off and add special **victory** conditions (capture a town, defeat a hero, gather
  resources, survive until a day) and **defeat** conditions (lose a town, lose a hero, a time limit); any one of them
  decides.
- A defeated player's heroes leave, its towns, mines and map dwellings turn neutral and its caravans vanish; the game
  then refuses commands and the game app shows the result.
- **ADR-073 (built, ADR-094)**:
  - New victory conditions: capture a given map object (an ownable one — mine, dwelling, town), obtain a given
    artifact (a hero of the player carries it), release a given hero from its prison.
  - New defeat condition: lose a given artifact (no hero of the player carries it any more); lose a town and lose a
    hero already cover the starting ones.
  - **Deadlines**: a victory condition may carry a time limit ("defeat the enemy within 3 months"); missing it defeats the
    player.
  - **Teams**: a team wins while at least one of its **human** players survives; eliminated human teammates share the win. A
    computer ally's survival does not win for them: they have lost (built, ADR-083).
  - The assumptions above were confirmed in ADR-075.
  - As built (ADR-094): "capture a map object" counts any object with a flag (mine, dwelling, provider, garrison, town) owned by the team;
    "obtain an artifact" counts a hero of the team carrying it, worn or in the backpack; "release a prisoner" names the prison. A deadline is
    an absolute day; on the day after it, the players the condition applies to are defeated. Map format v25.
- **Players without a town** (ADR-075): a campaign-like map may start a player with heroes only; losing a hero is then the
  defeat condition. Set the grace period to 0 (the town rule is off) and add "lose a hero"; the validator warns about a
  player that starts without a town while the grace period is on.
- Campaign hooks are open ❓ (FLOW-1, Phase 6).

## Victory screen, ranking and replays (ADR-092, ADR-101, planned)
- A won game on a map that is not campaign-like ends on a HoMM3-like **victory screen**: the days taken, the difficulty, the **score** and
  a rank title. A lost game, or one won only by computer players, has no score.
- **Campaign-like** (not ranked) is worked out: a human player starts without a town, or the map events give something only to human
  players (once map events exist). Arena maps are not ranked. There is no "not ranked" switch in the editor.
- **Difficulty** is the complexity. "Map default" counts as Normal unless the map author sets the map's difficulty in the editor. The
  difficulty never changes the AI.
- **Score** = max(10, 200 − days) × difficulty factor (Beginner 80 %, Easy 100 %, Normal 130 %, Hard 160 %, Expert 200 %) × turn timer
  factor (off or over 10 minutes ×1.0, 6–10 ×1.05, 3–5 ×1.10, 1–2 ×1.20; placeholders). Nothing else counts. **14 rank titles** named after
  our creatures, weakest to a dragon.
- The score goes into a **ranking table**, kept per computer in the user data folder (top 20). Random maps and the maps shipped with the
  game have **separate tables**; maps made in the editor go into the shipped maps' table marked "custom". Every human winner gets a row
  (hot-seat too): rank, player name (asked on the victory screen), faction, map or template (with size), difficulty, days, score, title,
  date. The new row is highlighted on the victory screen.
- **Replays**: each row keeps its game's record; "Save replay" writes it to `user://replays` as `*.ewreplay.json`. Every end screen (a defeat,
  an unranked game, the arena) offers "Save replay" too. A replay made with other content fails like a save.
- **Main menu**: New game, Load, Multiplayer, Campaign, Watch a replay, Leaderboard, Quit (Multiplayer and Campaign disabled until built).
- **Replay viewer**: two tabs, Replays and Saves (autosaves too; a save plays to its last command and stops). The whole map revealed by
  default, or any player's eyes; play, pause, the four speeds, next turn, next day, back to the start of any day; battles move by move with
  "skip to the result"; computer turns in full. **"Play from here"** only in a battle for now: the viewer plays that battle on by hand, as a
  separate unranked game; taking over on the adventure map comes later.

## Game modes (priority order)
1. **Single scenario vs. AI** (implemented).
2. **Hot-seat multiplayer** (implemented: several human players share the machine; battles between humans are played
   on the same screen).
3. **Story campaign**: chained scenarios, carry-over heroes, scripted map events and dialogue. This needs editor
   support for triggers and a script/event format (Phase 6): the map events of ADR-096 (HotA-like, [adventure-map.md](adventure-map.md#pandoras-box-and-map-events-adr-096-planned)),
   with variables kept from one map to the next.
- **Arena** (ADR-097, ADR-100; planned before 1.0, hot-seat, against the computer and over the LAN): as in HotA, one battle decides the game. Day 1 is preparation (unlimited
  movement and building, objects show what they hold); at the start of day 2 each side's strongest hero is moved to a **combat place** and
  they fight; the winner wins the game. An arena map has no victory or loss conditions; it is not ranked. The fog works as usual and the
  battle screen shows both armies; the battle gives no experience; a player who retreats or surrenders loses. **Three hand-made arena maps**
  ship with the mode; the generator has no arena template.
- **LAN multiplayer** (ADR-088, ADR-098, planned before 1.0; open: MP-3, MP-4 ❓): the **host** runs the game and the computer players;
  clients send commands. A client starts with the map's static content and gets from the host only what it may see, so it cannot see through
  the fog. The host creates the game in a lobby (LAN discovery or an address; the map or a random map and its options; colours; computer
  players); a battle between two remote humans is played live by both; a player who drops out pauses the game until the host lets the
  computer take over or the player rejoins; a text chat. The host's save resumes in multiplayer, a client's save loads in single player.
  The **first week may be played simultaneously** (the host's choice) on maps that define start zones: a hero entering another player's
  zone, touching an object another player touched that day, or seeing another player's hero discards that whole day for everyone, and the
  day is played again turn by turn. One turn timer for all; multiplayer only. Online play over the internet is not planned.

## Game options
Chosen on the new-game screen: map or template, players and teams (**2 to 8 teams**, 8 = free for all; the map or template
sets defaults and may fix the teams or limit the team count, ADR-070), fog mode ([adventure-map.md](adventure-map.md)),
complexity (starting resources, [towns-and-economy.md](towns-and-economy.md)), seed. Saves and replays record them
(ADR-058).
Random maps (ADR-086): template, size, players (human or computer, town, hero, team).

**Turn timer** (ADR-088, ADR-091): off by default, 1 to 30 minutes a turn (the same every turn), for human players; a loaded game starts the
turn with the full time. Warnings at 1 minute, 30, 15 and 5 seconds left. It runs on the adventure map and in a battle against the computer or
neutrals (if it runs out there, the battle is finished first); it stops during a battle between two humans. When it runs out, pending choices
get their default (a chest gives gold; leave or stay where offered; neutrals' offers are taken), a level-up or a riddle waits for the player,
and then the turn ends on its own.
