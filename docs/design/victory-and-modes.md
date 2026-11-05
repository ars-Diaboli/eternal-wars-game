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
- **Not ranked**: campaign-like maps (a human starts without a town, or map events give something only to humans; the editor's
  validator warns) and arena maps. No editor switch.
- **Difficulty** = the complexity; "Map default" counts as Normal unless the author sets the map's difficulty. It never changes the AI.
- **Score** = max(10, 200 − days) × difficulty (Beginner 80 %, Easy 100 %, Normal 130 %, Hard 160 %, Expert 200 %) × turn timer (off or over
  10 min ×1.0, 6–10 ×1.05, 3–5 ×1.10, 1–2 ×1.20; placeholders). Nothing else counts. **14 rank titles** named after our creatures, weakest to
  a dragon.
- **Ranking table** (top 20, per computer): separate tables for random and shipped maps (editor maps join the shipped one as "custom"). Every
  human winner gets a row (hot-seat too): rank, name (asked on the victory screen), faction, map or template and size, difficulty, days,
  score, title, date.
- **Replays**: each row keeps its game's record; every end screen offers "Save replay" (`user://replays/*.ewreplay.json`); a replay made with
  other content fails like a save.
- **Main menu**: New game, Load, Multiplayer, Campaign, Watch a replay, Leaderboard, Quit (unbuilt ones disabled); reached from the end
  screen and the F10 menu.
- **Replay viewer** *(planned)*: replays and saves (to their last command); whole map or any player's eyes; play, pause, four speeds, jump
  by turn or day; battles move by move or skipped; **"Play from here"** in a battle (a separate unranked game), on the map later.

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
- **LAN multiplayer** *(planned before 1.0, ADR-088, ADR-098; MP-3, MP-4 ❓)*: the **host** runs the game and computer players; clients send
  commands and get only what they may see. Lobby (LAN discovery or address; map or random map, options, colours, computer players); battles
  between two remote humans are live; a dropped player pauses the game until the host hands it to the computer or the player rejoins; text
  chat; the host's save resumes in multiplayer, a client's in single player. **Simultaneous first week** (host's choice, maps with start
  zones): entering another player's zone, touching an object another player touched that day, or seeing another player's hero discards that
  day for all, and it is replayed turn by turn. One turn timer for all. No internet play.

## Game options
Chosen on the new-game screen and recorded in saves and replays (ADR-058): map or template (with size and players: human or computer, town,
hero, team; ADR-086), fog mode ([adventure-map.md](adventure-map.md#fog-of-war)), complexity ([starting resources](towns-and-economy.md#starting-resources)),
seed, turn timer. For a map, a player whose faction the map leaves open (a random town, or a hero placeholder without a town) picks a town
or "Random" ([random placeholders](adventure-map.md#map-bans-and-random-placeholders), ADR-133).
- **Teams** (ADR-070): **2 to 8 teams** (8 = free for all; free for all when nothing is defined). The map or template sets defaults and may
  fix the teams (story maps) or limit the team count; setup may change them unless locked; the map editor may override a template's.
- **Turn timer** (ADR-088, ADR-091): off, or 1–30 minutes a turn for humans; a loaded game restarts the turn at full time. Warnings at 1
  min, 30, 15, 5 s. Runs on the map and in battles against computer or neutrals (finished first); stops in human-vs-human battles. At zero,
  pending choices take their default (chest gold; leave or stay as offered; neutrals' offers taken), a level-up or riddle waits, and the
  turn ends.
