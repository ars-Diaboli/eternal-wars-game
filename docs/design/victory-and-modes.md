# Victory, defeat and game modes

> Part of the [game design](overview.md). Everything here is built unless tagged *(planned)*. Open items ❓:
> [open-questions.md](../../memory-bank/open-questions.md).

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
  validator warns about a townless start while the grace period is on (not on an arena map, which eliminates nobody).
- A defeated player's heroes leave, its towns, mines, providers and map dwellings turn neutral and its caravans vanish; the game then refuses
  its commands and the app shows the result.
- Campaign hooks are open ❓ (FLOW-1, Phase 6).

## Victory screen, ranking and replays
ADR-092, ADR-101, built ADR-102 … ADR-104, the viewer ADR-164.
- A won game on a map that is not campaign-like ends on a HoMM3-like **victory screen**: days, difficulty, **score**, rank title. A lost game,
  or one won only by computers, has no score.
- **Not ranked**: campaign-like maps (a human starts without a town, or map events give something only to humans; the editor's
  validator warns) and arena maps. No editor switch.
- **Difficulty** (Easy, Normal, Hard, Expert, Master; ADR-225): chosen on the new-game screen, the map's own difficulty (Normal when not
  set) preselected. It never changes the AI.
- **Score** = max(10, 200 − days) × difficulty (Easy 80 %, Normal 100 %, Hard 130 %, Expert 160 %, Master 200 %) × turn timer (off or over
  10 min ×1.0, 6–10 ×1.05, 3–5 ×1.10, 1–2 ×1.20; placeholders). Nothing else counts. **14 rank titles** named after our creatures, weakest to
  a dragon.
- **Ranking table** (top 20, per computer): separate tables for random and shipped maps (editor maps join the shipped one as "custom"). Every
  human winner gets a row (hot-seat too): rank, name (asked on the victory screen), faction, map or template and size, difficulty, days,
  score, title, date.
- **Replays**: each row keeps its game's record; every end screen offers "Save replay" (`user://replays/*.ewreplay.json`); a replay made with
  other content fails like a save.
- **Main menu**: Continue (the newest save that loads, naming any newer one that did not; disabled without a save), New game, Load, Multiplayer (LAN games), Campaign, Watch a replay,
  Leaderboard, Quit (unbuilt ones disabled), on a picture ([presentation.md](presentation.md#camera-and-ui)); reached from the end
  screen and the F10 menu.
- **Replay viewer** (ADR-164): "Watch a replay" lists two tabs, **Replays** (`user://replays`) and **Saves** (autosaves too), newest
  first. A record that does not replay to its digest is refused with the reason (other content). The viewer plays the record command by
  command on the map and in battles, computer turns and battles in full; it issues no command of its own. A bar along the bottom:
  play / pause (Space), four speeds (heroes and battles alike), next turn, next day, a day to go to (forward, the game plays on to it at
  once; back, it is replayed from the start to that day's first turn), the whole map or a player's eyes, and the main menu. In a battle, **"Skip to the result"** plays its moves at
  once and shows the result, which closes by itself after a moment (Enter at once). A save plays to its last command and stops. Nothing is
  ranked, saved or autosaved.
- **"Play from here"**, in a battle: the battle goes on by hand as a separate game, not ranked, the computer's side played as usual; when
  its result screen is left, the viewer comes back paused where it was, the replay unchanged. A battle a person commands goes on from the
  move shown, one ended by quick combat from just before it. A battle only the computer fights starts again from its start, the viewer
  commanding the attacker (the defender stays the computer's; the battlefield width is the one the computer fought on).
- **"Play from here"**, on the adventure map (ADR-185): while the game is not over and a person is still in it. The viewer closes and the
  game goes on from that command as the player's own, as if a save made there were loaded: saving, autosave, the end screen and the ranking
  as usual; the players stay as recorded, and on a computer's turn the computer plays on first. The replay file does not change.

## Game modes
In priority order:
1. **Single scenario vs. AI**.
2. **Hot-seat**: several humans share the machine; battles between humans on the same screen.
3. **Story campaign** *(planned, Phase 6)*: chained scenarios, carry-over heroes, scripted events and dialogue, on the
   [map events](adventure-map.md#pandoras-box-and-map-events) with variables kept between maps; a prologue map first (ADR-118, CAMP-1).
- **Arena** (ADR-097, ADR-100, ADR-158), as in HotA; the map's "Arena" switch (map format v38) turns it on:
  - **Day 1 is preparation**: heroes have nearly unlimited movement (1 000 000 points) and towns build any number of buildings, with the
    usual starting resources (the difficulty's, or the map's own). The map's victory and defeat conditions, the last team standing and the
    elimination do not count: nobody is out before the battle.
  - **Day 2 is the battle**, before anyone's turn. The **defender** is the first player in turn order with a hero on a
    [combat place](adventure-map.md#special-sites)'s entrance, and that place is the battlefield; the **attacker** is the first player in turn
    order who is its enemy. With no hero on a combat place, the first player attacks and the next enemy defends, at the first combat place.
    Each side (team) fights with its **strongest hero** by army value (count × Fight Value; the lower hero id on a tie), one on the map, else
    a garrison hero, which comes out with the garrison. The defender is put on the combat place's entrance, the attacker on its attack cell (a
    hero in the way swaps places). The battle is fought on that cell's terrain, never as a siege; it gives **no experience**; the computer
    never retreats or surrenders.
  - **The end**: the battle's winner's team wins the game; retreat or surrender loses; both armies fallen is a **draw**, a loss for
    everyone, and so is a side without a hero (or no enemy to fight). A side without an army loses without a battle. Never ranked.
  - **In the game** (ADR-159): an arena map on the new-game screen says what the mode is, and each player is set to Human or Computer
    (hot-seat, or against the computer; at least one human). Day 2's battle opens by itself, both armies shown in full, and the status line
    names the two champions. The result screen tells who attacked whom, that arena maps are not ranked, and offers **"Play again"** (the
    same map, players, teams, factions and options, a new seed) and "Save replay".
  - **The computer on day 1** (ADR-160) spends everything on the battle's army: its towns buy what waits, then build the dwellings (with
    the buildings they need) whose first creatures they can still buy, and the dwelling upgrades worth it, best army per gold first, then
    hire with what is left. Its heroes take what helps today (piles, chests, artifacts, buffs, training, map dwellings, joining monsters) and
    skip what pays later (mines, providers, flags, obelisks); they fight only for small losses and never go for enemy heroes or towns
    (beating the enemy's heroes would leave nobody for the battle). At the end of the day its strongest hero visits every own town and every
    other hero brings it its army and war machines.
  - **What day 1 shows** (ADR-161): on the preparation day a right click on a Pandora's Box shows its contents and guard (its "Open it?"
    prompt too), on a Shrine its spell, on a Witch Hut its skills, on a quest object its current stage (what it asks, wants, takes and gives).
  - **In the editor** (ADR-161): the "Arena" switch heads the Conditions tab; while it is on, the victory and defeat rows are greyed out
    (kept, not counted). Combat places are ordinary objects of the Object tool. The validator: an error without a combat place or with one
    whose attack cell is impassable; warnings when two enemies' heroes can reach the same ground on day 1 (from their heroes and town
    entrances, through gates; monsters, garrisons and barriers count as passable), for map events that do something only after day 1 (by
    the date in their conditions), for conditions (ignored), and for a combat place on a map that is not an arena.
  - **Shipped maps** (ADR-180), hand-made (no generator template), on the new-game screen's Arenas tab: **Proving Grounds**
    (two players, each picking its faction for a random town and hero, mirrored halves, a sealed combat place); **Frostfang Pass** (two
    players and no towns: a Haven and a Necropolis mage buy their armies at map dwellings); **Four Banners** (two against two, allies sharing
    a half, each half with its own combat place). Random heroes there start with the same neutral army. Their halves are kept apart by
    rock five tiles wide, which a hero's sight does not cross, and each player has a start zone (its half, or its quarter on Four Banners).
  - **Over the LAN** (ADR-199): an arena map in the host's lobby offers each colour as Here, LAN player (the default after the first) or
    Computer. Each player prepares in its own view; day 1 is played turn by turn, or at once with the simultaneous switch on a map with a
    start zone for every person. Day 2's battle opens by itself on both sides' computers and is fought live. The result screen tells each
    computer its own victory or defeat; the host's "Play again" starts the rematch for everyone, with the same colours.
- **LAN multiplayer** *(before 1.0, ADR-088, ADR-098; MP-3, MP-4 ❓; network, view, lobby, live battles, drop-outs, saves, start zones, the simultaneous first week, the turn timer and arena maps built: ADR-192 to ADR-199)*: the **host** runs
  the game and computer players; clients send commands and get only what they may see. **Lobby**: Multiplayer lists the games found on the
  network (or an address is typed); the host sets the game up on the new-game screen, each colour played here, by a LAN player or by the
  computer; a joined player takes an open colour; the host's choices, never the seed, are shown to all; a colour nobody has joined at the
  start waits for a player during the game; the chat stays over the map and the battle. **Battles** open only on the computers of their
  sides; between players on two computers each commands its own side live and there is no quick combat. **A player who drops out** pauses
  the game for everyone, and so does a colour nobody has joined when its turn or its battle comes: every command waits until a player joins
  that colour or the host lets the computer play it from then on (mid-battle too). **Saves**: the host saves and autosaves the game; a
  client's save and replay are the host's whole record of the game, sent when asked. Any save goes on in a LAN game ("Host a saved game":
  each colour a person plays is played here, by a LAN player or by the computer) or on one computer (a save with several people's colours
  asks which play here, hot-seat, and which the computer plays from then on; F9's quick load keeps them all here). **Simultaneous first week** (the host's switch in the lobby; maps with
  a start zone for every person, which random maps have and the editor paints; one colour played on the host; the lobby says when the map
  lacks one): the people play each day of the first week (an arena's preparation day) at once, each on their own computer, and the computer players after them. One battle or question at a time: while one person
  fights or answers, the others wait. A hero entering another person's start zone, a person seeing another person's hero it did not see when
  the day began, or a hero visiting an object another person's hero visited that day discards the day for all (an ally's zone, hero or
  visit never does): the host goes back to the
  day's start and it is played again turn by turn (no second income), and so is every day after it. A save made during a simultaneous day
  goes on turn by turn when loaded on one computer. The turn timer is the host's, one clock for all. No internet play.

## Game options
The new-game screen has four tabs (ADR-201): **Scenarios** (hand-made maps played to win: the shipped ones and the editor's), **Random
maps** (the templates), **Arenas** and **Campaign** (campaign-like maps: a person starts without a town, a map event favours people, or the
map keeps or takes campaign variables; `MapKinds`). Each tab lists its maps on the left and shows the chosen one's size, players,
description and player setup on the right; the options below are shared by every tab. Every list sorts by **Name** (A–Z, the default) or
**Size** (small first), a second press reversing it, and filters by **Size** (All or one preset, 48 … 256; a map counts as the smallest
preset holding its larger side, a template under every size it supports); the choice is kept in `settings.json` for all tabs (ADR-204).
The options (ADR-225): **Game difficulty**, a row of five square chess-piece buttons (Easy … Master; a map's own difficulty, else Normal,
preselected; the hovered one's name, starting resources and score factor on the status bar); **Fog of war** (Permanent reveal or Refog)
with the **hero limit** beside it; a **turn timer** checkbox with the minutes beside it when ticked; the **seed** only in debug builds. On a
random map "+" under the players adds one and "−" on a player's row removes it, within the template's range. Each player row picks the
player's **colour** (taking another's swaps them) and its town from a 2 × 4 panel of crests. The screen closes with an "×" at its top
right (or Esc).

Chosen on the new-game screen and recorded in saves and replays (ADR-058): map or template (with size and players: human or computer, town,
hero, team; ADR-086), fog mode ([adventure-map.md](adventure-map.md#fog-of-war)), difficulty ([starting resources](towns-and-economy.md#starting-resources)), the players' colours,
seed, turn timer. For a map, a player whose faction the map leaves open (a random town, or a hero placeholder without a town) picks a town
or "Random" ([random placeholders](adventure-map.md#map-bans-and-random-placeholders), ADR-133).
- **Teams** (ADR-070): **2 to 8 teams** (8 = free for all; free for all when nothing is defined). The map or template sets defaults and may
  fix the teams (story maps) or limit the team count; setup may change them unless locked; the map editor may override a template's.
- **Turn timer** (ADR-088, ADR-091): off, or 1–30 minutes a turn for humans; a loaded game restarts the turn at full time. Warnings at 1
  min, 30, 15, 5 s. Runs on the map and in battles against computer or neutrals (finished first); stops in human-vs-human battles. At zero,
  pending choices take their default (chest gold; leave or stay as offered; neutrals' offers taken), a level-up or riddle waits, and the
  turn ends. A LAN game has **one clock, the host's** (ADR-198): every computer shows it, and the host ends a client's turn on it. A
  **simultaneous day** has one clock for the day: it runs while any person's turn is open (one who has ended waits for the others), and at
  zero every open turn ends.
