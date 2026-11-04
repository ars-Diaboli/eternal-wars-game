# Victory, defeat and game modes

> Part of the [game design](overview.md). Everything here is built unless tagged *(planned)*. Open items ❓:
> [open-questions.md](../project/open-questions.md).

## Victory and defeat
ADR-057, ADR-073, ADR-075, ADR-094.
- **Default**: a player with no towns and no heroes is out at once; one that lost its last town has 7 days (configurable grace period) to own
  one again; the last team standing wins.
- A map can switch these off and add conditions; any one of them decides (map format v25):
  - **Victory**: capture a town, defeat a hero, gather resources, survive until a day, capture a given map object (any flaggable one owned by
    the team: mine, dwelling, provider, garrison, town), obtain a given artifact (a hero of the team carries it, worn or in the backpack),
    release a given hero from its prison.
  - **Defeat**: lose a town, lose a hero, a time limit, lose a given artifact (no hero of the player carries it).
  - **Deadlines**: a victory condition may carry an absolute day; the day after it, the players it applies to are defeated.
- **Teams**: a team wins while at least one of its **human** players survives; eliminated human teammates share the win; a surviving computer
  ally does not win for them (ADR-083).
- **Players without a town**: a campaign-like map may start a player with heroes only: set the grace period to 0 and add "lose a hero"; the
  validator warns about a townless start while the grace period is on.
- A defeated player's heroes leave, its towns, mines, providers and map dwellings turn neutral and its caravans vanish; the game then refuses
  its commands and the app shows the result.
- Campaign hooks are open ❓ (FLOW-1, Phase 6).

## Victory screen, ranking and replays
ADR-092, ADR-101, built ADR-102 … ADR-104.
- A won game on a map that is not campaign-like ends on a HoMM3-like **victory screen**: days, difficulty, **score**, rank title. A lost game,
  or one won only by computers, has no score.
- **Not ranked**: campaign-like maps (a human starts without a town, or map events give something only to humans) and arena maps. No editor
  switch.
- **Difficulty** = the complexity; "Map default" counts as Normal unless the author sets the map's difficulty. It never changes the AI.
- **Score** = max(10, 200 − days) × difficulty (Beginner 80 %, Easy 100 %, Normal 130 %, Hard 160 %, Expert 200 %) × turn timer (off or over
  10 min ×1.0, 6–10 ×1.05, 3–5 ×1.10, 1–2 ×1.20; placeholders). Nothing else counts. **14 rank titles** named after our creatures, weakest to
  a dragon.
- **Ranking table** per computer in the user data folder (top 20); random maps and shipped maps have **separate tables**, editor-made maps go
  into the shipped table marked "custom". Every human winner gets a row (hot-seat too): rank, name (asked on the victory screen), faction,
  map or template (with size), difficulty, days, score, title, date; the new row is highlighted.
- **Replays**: each row keeps its game's record; "Save replay" (on every end screen) writes `user://replays/*.ewreplay.json`. A replay made
  with other content fails like a save.
- **Main menu**: New game, Load, Multiplayer, Campaign, Watch a replay, Leaderboard, Quit (the unbuilt ones disabled); "Main menu" on the end
  screen and in the F10 menu returns to it.
- **Replay viewer** *(planned)*: tabs Replays and Saves (autosaves too; a save plays to its last command). Whole map revealed or any player's
  eyes; play, pause, four speeds, next turn, next day, back to any day's start; battles move by move with "skip to the result"; computer
  turns in full. **"Play from here"** only in a battle for now (plays it on by hand as a separate unranked game); on the adventure map later.

## Game modes
In priority order:
1. **Single scenario vs. AI**.
2. **Hot-seat**: several humans share the machine; battles between humans on the same screen.
3. **Story campaign** *(planned, Phase 6)*: chained scenarios, carry-over heroes, scripted events and dialogue, on the
   [map events](adventure-map.md#pandoras-box-and-map-events) with variables kept between maps; a prologue map first (ADR-118, CAMP-1).
- **Arena** *(planned before 1.0, ADR-097, ADR-100)*, hot-seat, vs. the computer and over LAN, as in HotA: day 1 is preparation (unlimited
  movement and building, objects show what they hold); at day 2's start each side's strongest hero is moved to a **combat place** and the
  winner of that battle wins the game. No victory or loss conditions, not ranked, normal fog, both armies shown, no experience; retreat or
  surrender loses. **Three hand-made arena maps**; no generator template.
- **LAN multiplayer** *(planned before 1.0, ADR-088, ADR-098; MP-3, MP-4 ❓)*: the **host** runs the game and the computer players; clients
  send commands and get only what they may see (no seeing through the fog). Lobby (LAN discovery or an address; map or random map and
  options; colours; computer players); a battle between two remote humans is played live by both; a dropped player pauses the game until the
  host hands it to the computer or the player rejoins; text chat. The host's save resumes in multiplayer, a client's loads in single player.
  **Simultaneous first week** (host's choice, maps with start zones): a hero entering another player's zone, touching an object another
  player touched that day, or seeing another player's hero discards that day for everyone, and it is replayed turn by turn. One turn timer
  for all. Internet play is not planned.

## Game options
Chosen on the new-game screen and recorded in saves and replays (ADR-058): map or template (with size and players: human or computer, town,
hero, team; ADR-086), fog mode ([adventure-map.md](adventure-map.md#fog-of-war)), complexity ([starting resources](towns-and-economy.md#starting-resources)),
seed, turn timer.
- **Teams** (ADR-070): **2 to 8 teams** (8 = free for all; free for all when nothing is defined). The map or template sets defaults and may
  fix the teams (story maps) or limit the team count; setup may change them unless locked; the map editor may override a template's.
- **Turn timer** (ADR-088, ADR-091): off by default, 1–30 minutes a turn, for humans; a loaded game starts the turn with full time. Warnings
  at 1 minute, 30, 15 and 5 seconds. Runs on the adventure map and in battles against the computer or neutrals (a battle running out is
  finished first); stops in battles between two humans. When it runs out, pending choices get their default (a chest gives gold; leave or
  stay where offered; neutrals' offers are taken), a level-up or riddle waits for the player, and the turn ends.
