# Decisions: LAN multiplayer

**Covers**: The host and clients, each client's view, the lobby, live battles, LAN saves, start zones and the simultaneous first week.

**Current rules**: [docs/design/victory-and-modes.md](../../docs/design/victory-and-modes.md), [docs/technical/architecture.md](../../docs/technical/architecture.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [098](#adr-098-lan-multiplayer-a-host-that-hides-the-fog-saves-a-simultaneous-first-week-mp-1-mp-2-2026-10-29) | LAN multiplayer: a host that hides the fog, saves, a simultaneous first week (MP-1, MP-2, 2026-10-29) | accepted |
| [192](#adr-192-the-lan-network-layer-a-host-that-runs-the-game-clients-that-send-commands-adr-088-adr-098-mp-1-roadmap-2026-11-16) | The LAN network layer: a host that runs the game, clients that send commands (ADR-088, ADR-098, MP-1, roadmap, 2026-11-16) | implemented |
| [193](#adr-193-each-lan-client-holds-only-its-players-view-mp-3-adr-099-roadmap-2026-11-16) | Each LAN client holds only its player's view (MP-3, ADR-099, roadmap, 2026-11-16) | implemented |
| [194](#adr-194-the-lan-lobby-finding-games-setting-one-up-colours-chat-mp-1-adr-098-roadmap-2026-11-16) | The LAN lobby: finding games, setting one up, colours, chat (MP-1, ADR-098, roadmap, 2026-11-16) | implemented |
| [195](#adr-195-lan-battles-fought-live-between-two-computers-a-player-who-drops-out-pauses-the-game-mp-1-adr-098-roadmap-2026-11-16) | LAN battles fought live between two computers; a player who drops out pauses the game (MP-1, ADR-098, roadmap, 2026-11-16) | implemented |
| [196](#adr-196-saves-of-lan-games-the-hosts-save-goes-on-anywhere-a-client-saves-the-hosts-record-mp-3-adr-099-roadmap-2026-11-16) | Saves of LAN games: the host's save goes on anywhere, a client saves the host's record (MP-3, ADR-099, roadmap, 2026-11-16) | implemented |
| [197](#adr-197-start-zones-in-the-map-and-the-simultaneous-first-week-mp-2-mp-4-adr-098-adr-099-roadmap-2026-11-16) | Start zones in the map and the simultaneous first week (MP-2, MP-4, ADR-098, ADR-099, roadmap, 2026-11-16) | implemented |
| [198](#adr-198-one-turn-timer-in-a-lan-game-and-on-a-simultaneous-day-flow-3-mp-2-e-adr-091-adr-197-roadmap-2026-11-17) | One turn timer in a LAN game and on a simultaneous day (FLOW-3, MP-2 e, ADR-091, ADR-197, roadmap, 2026-11-17) | implemented |
| [199](#adr-199-arena-maps-over-the-lan-adr-100-arn-1-mp-2-adr-197-roadmap-2026-11-17) | Arena maps over the LAN (ADR-100, ARN-1, MP-2, ADR-197, roadmap, 2026-11-17) | implemented |
| [203](#adr-203-what-a-link-may-cost-a-lan-host-adr-192-adr-194-adr-196-2026-11-17) | What a link may cost a LAN host (ADR-192, ADR-194, ADR-196, 2026-11-17) | implemented |
| [217](#adr-217-one-coordinator-for-the-simultaneous-days-adr-197-adr-214-2026-11-18) | One coordinator for the simultaneous days (ADR-197, ADR-214, 2026-11-18) | implemented |
| [218](#adr-218-the-lan-code-changes-a-clients-game-through-a-view-sink-a-schema-hash-guards-the-join-adr-193-adr-203-2026-11-18) | The LAN code changes a client's game through a view sink; a schema hash guards the join (ADR-193, ADR-203, 2026-11-18) | implemented |
| [221](#adr-221-gamehost-coordinates-seats-pause-chat-and-the-day-played-again-adr-192-adr-195-adr-197-2026-11-19) | `GameHost` coordinates seats, pause, chat and the day played again (ADR-192, ADR-195, ADR-197, 2026-11-19) | implemented |
| [228](#adr-228-lan-hardening-sends-that-never-wait-the-chats-allowance-the-lobbys-waiting-players-the-record-per-colour-adr-203-adr-226-2026-11-19) | LAN hardening: sends that never wait, the chat's allowance, the lobby's waiting players, the record per colour (ADR-203, ADR-226, 2026-11-19) | implemented |


### ADR-098: LAN multiplayer: a host that hides the fog, saves, a simultaneous first week (MP-1, MP-2, 2026-10-29)
**Status**: accepted as a design (2026-10-29), the owner's answers to MP-1 and MP-2 (ADR-088); **nothing implemented**. Points marked
*(assumed)* are the agent's reading of a short answer; they and the new questions are MP-3 and MP-4 in `open-questions.md`.

- **When** (MP-1 a): **before 1.0**, after the Phase 3 and 4 exits and before the campaign.
- **Host** (MP-1 b): clients send commands; the host checks them and runs them with `session.Execute`. The host also plays the computer
  players.
- **Clients see only their own view** (MP-1 g): a client starts with the map's **static** content (terrain, scenery, the objects as placed)
  for drawing. The host sends each client the **actual state it may see**; a client never gets what is under its fog, so a modified client
  cannot see through it. This replaces the part of MP-1 (b)'s proposal where every client runs every command itself and compares
  `GameStateDigest`: a client has too little state to run the game *(assumed; MP-3)*.
- **Finding a game, lobby, battles, chat** (MP-1 c, d, e, i): LAN broadcast discovery plus typing an address; the host picks the map or
  the template and its options, each client a free colour, and the host may set the other colours to computer; in a battle between two
  remote humans each controls its own side live; a simple text chat.
- **A player who drops out** (MP-1 f): the game pauses for everyone; the host chooses to let the computer take over or to wait for a rejoin.
- **Saves** (MP-1 h): the **host's** save resumes in multiplayer. A **client's** save loads in single player only (the client's colour,
  the computer playing the rest). Whether the host's save also loads in single player or hot-seat, as ADR-088 said, and what a client's
  save holds (the client knows only its own view): MP-3.
- **Simultaneous first week** (MP-2):
  - only on maps that define a **start zone** for each player (MP-2 a); how a map defines one: MP-4;
  - the **conflicts** that end it: a hero entering another player's start zone, touching an object another player touched that day, or
    seeing another player's hero (MP-2 b);
  - on a conflict the **whole day is discarded** for everyone and played again turn by turn in the normal order; the players lose what they
    did that day (MP-2 c). The host goes back to the state at the start of the day (the game is deterministic: the start map and the
    commands before that day rebuild it, ADR-058);
  - the host turns it on or off when it creates the game; always the first week for now (MP-2 d);
  - one turn timer for all; the day ends when everyone has ended the turn (MP-2 e);
  - multiplayer only; hot-seat cannot hide one player's day from another (MP-2 f).
- **Fits the core as it is**: commands and the deterministic core suit a host; fog (`Visibility`) already says what a player sees. New are the
  network layer, a per-player view of the state and of the events (what the views subscribe to on a client), start zones in the map and a way
  back to the start of a day.

### ADR-192: The LAN network layer: a host that runs the game, clients that send commands (ADR-088, ADR-098, MP-1, roadmap, 2026-11-16)
**Status**: implemented (2026-11-16), the roadmap's first LAN item; *(proposal)* marks the agent's choices. The per-player view (MP-3,
ADR-099), the lobby, battles between two remote humans, drop-outs and saves are the roadmap's next items.

- **The host runs the game** (MP-1 b): the one real `GameSession` is the host's. It takes clients into the human colours played elsewhere,
  runs their commands with `session.Execute`, and plays the computer players as in a game on one computer. Every command it logs, whoever
  gave it (the host's player, a client, the AI, the turn timer), goes to every client in the log's order.
- **A client keeps a copy** *(proposal; interim until MP-3)*: on joining it gets the game's record (the start map, the options, every command so
  far; the save format), replays it and checks the digest, then applies the host's commands one by one; a command out of order or one the
  copy refuses stops the client ("cut off from the host") instead of letting it play another game. The copy is the whole game, so a client
  could look under its fog; the per-player view replaces the copy with what the player may see.
- **Who gives which command**: a client sends only its own colour's commands; its copy refuses another colour's at once, and the host
  refuses them too. On the host's screen a remote colour's command is refused ("Red plays on another computer"). A client's command is
  checked against its copy first (whose turn, whose hero), so most refusals need no round trip; the host's rules have the last word, and
  their refusal comes back as a message. The command is not run on the client: `Execute` answers `CommandResult.Sent`, and the game changes
  when the command comes back from the host.
- **Wire** *(proposal)*: TCP, port **47820** by default; each message is its length (4 bytes) and its JSON, with the save files' settings
  for the commands and records it carries. Messages: join, joined (the colour and the record), join refused, command, command applied (its
  index in the log), command refused. A protocol number; a host and a client of different numbers refuse to play.
- **Seats** *(proposal, until the lobby)*: the game is started with `--host[=port]`; the game started from the new-game screen is hosted, its
  **first human colour plays on the host**, the other human colours wait for clients, which join with `--join=address[:port]`
  (`--name=` names the player) in the first free colour. A remote colour nobody has joined yet holds up its turn. A client that leaves frees
  its colour; joining again gets the game as it stands then (the pause and the computer taking over are the drop-out item).
- **This screen's player**: the map turns only to the colours that play at this screen; on another computer's turn it stays with this
  screen's player, whose heroes, towns and resources the side panel and the status bar keep showing; questions and object screens open only
  for the colours here. On one computer nothing changes (hot-seat: every human plays here).
- **Code**: core `GameSession.Route` (`ICommandRoute`) and `CommandResult.Sent` (Domain); a new project **`EternalWars.Core.Network`**
  (Tier 1, no Godot): `NetMessage`s and `NetSerializer`, `TcpConnection` / `TcpGameListener` / `LoopbackConnection` (`INetConnection`;
  messages wait in a queue for the game's thread), `GameHost`, `GameClient`; `GameRecordSerializer.CreateOptions`. Tests `LanGameTests`
  (in memory and over TCP on this computer). Godot: `MapViewContext.PlaysElsewhere` / `PlaysHere` / `ListedPlayer`, `JoinGameScreen`; the
  game app's `Main.Network`. No save format change.
- **Not done**: the per-player view (fog over the wire), the lobby and LAN discovery, chat; a battle between two remote humans is fought by
  each side's commands but both screens show it as on one computer; the turn timer still runs on each computer; a load or a new game on
  the host ends the LAN game for the clients; the main menu's Multiplayer stays disabled.

### ADR-193: Each LAN client holds only its player's view (MP-3, ADR-099, roadmap, 2026-11-16)
**Status**: implemented (2026-11-16), the roadmap's per-player view; replaces ADR-192's interim copy (the client no longer gets the record, so it
cannot look under its fog). *(proposal)* marks the agent's choices.

- **What the client gets**: on joining, the ground of the map (terrain, roads, rivers, heights; no object, no hero), the players (name, colour,
  team, person or computer), the options with the seed left out and the scenario's rules. Then **updates**, once a host frame, with what changed
  in the player's view since the last one:
  - its own heroes, towns, mines and everything it owns, as they are; its retired heroes and caravans;
  - other heroes while it sees them (`CanSee`), taken away when it no longer does;
  - map objects while a cell of theirs is in its sight; **out of sight an object stays as last sent** ("as last seen"); an object the player
    remembers but has not seen since it joined comes as a bare object of its kind at its place, with the flag it last saw; one its memory
    lets go (seen gone) is taken away;
  - its memory of objects and its fog (tiles explored, tiles in sight under refog);
  - the date, the week's event, whose turn it is, the outcome; a question only when it is the player's; **a battle only when the player
    fights it**;
  - of the other players their name, team and whether they are out, **never their resources**.
- **Events** the player may see, in order: its own (resources, fog, memory, spells' reports), the turn order and who is out, those about a
  hero it sees (a step counts when either end is in its sight) or an object in its sight or its own, **every event of a battle it fights**;
  a third player sees a battle's result only where it sees it (the defeated hero leaves the map in its sight). A command's execution is
  published only for the player's own commands.
- **The client runs no rules** (`GameSession.FollowingHost`): no service listens to the events, every command goes to the host. Its state is
  what the host sent; the views read it as on one computer. A piece that changes is copied into the hero or object already there, so the
  figures and screens holding it follow; a hero coming into sight is announced with `HeroPlaced` before the update's events, one leaving it
  with `HeroRemoved` after them; an event naming a hero or object the view does not hold (in sight only between two updates) is left out.
- **Wire** *(proposal)*: the pieces are written field by field (`StateCodec`): every field of a hero, an object, a battle, a question or an
  event, with no code per type, so a field the rules add crosses the wire with it; creature and object definitions by id; only the game's own
  types are read back. The host keeps the text last sent of each piece and sends a piece only when it changed. Protocol 2.
- **Saves**: a client saves nothing (no autosave, no save, no ranking, no replay): its view does not replay the game. The host saves as
  before. A client's save as the host's whole record is the saves item.
- **Code**: Core.Network `PlayerViewFeed` (host), `ViewMirror` (client), `StateCodec`, `ViewUpdate` / `ViewChange` / `PlayerSeat`;
  `JoinAccepted` carries the ground, `CommandApplied` is gone. Domain: `GameSession.FollowingHost`, `FogOfWar` and `ObjectMemory` setters for
  the client (internal, visible to Core.Network). Tests `LanGameTests` (fog, last seen, private events, battles' sides and a third player, a
  computer game on a generated map checked every turn) and `StateCodecTests` (every hero and object of the smoke maps, a siege).
- **Not done**: another player's hero in sight is sent whole (its exact army, artifacts and skills, where HoMM shows an estimate) and so is
  an enemy town in sight (buildings, garrison); the Thieves' Guild and the kingdom overview show on a client only what the client holds of
  others; the lobby, live battles between two remote humans, drop-outs, saves and the turn timer are the roadmap's next items.

### ADR-194: The LAN lobby: finding games, setting one up, colours, chat (MP-1, ADR-098, roadmap, 2026-11-16)
**Status**: implemented (2026-11-16), the roadmap's lobby; replaces ADR-192's `--host` / `--join` seats. *(proposal)* marks the agent's choices.

- **Multiplayer** (main menu, now enabled): the player's name, "Host a game", the games found on the network (one button each: host, map,
  lobby or playing, open colours; another version's shown but not joinable) and an address to type (`name` or `name:port`).
- **Finding games** *(proposal)*: a UDP probe, broadcast and sent to this computer, on port **47821** (the game's port plus one), asked every
  1.5 s; each host answers with its version, player, game, open colours, TCP port and whether it has started. A game that misses two probes
  leaves the list. A host on this computer is listed once, by its network address.
- **The host's lobby is the new-game screen** *(proposal)*: the same maps, templates and options (ADR-086), with this computer's addresses
  and the chat beside it. Each colour a person may play is **Here** (this computer), **LAN player** or **Computer**; by default the first is
  Here and the others LAN players. "Joined" shows who took each LAN colour. At least one colour must be played here.
- **Joined players** see the map, the settings in words, each colour (who plays it; its town, hero and team) and the players without a colour;
  they take an open colour with "Take". A joining player gets the first open colour, or waits without one; a colour the host takes back
  leaves its player waiting until another opens. Players do not choose their own town or hero: the host does *(proposal; HoMM lets each
  player choose)*.
- **The seed stays on the host** *(proposal)*: it would let a client generate the random map and see under its fog.
- **Starting**: a LAN colour nobody has joined asks for "Start anyway"; it then waits for a player during the game (ADR-192), who finds the game
  on the list as playing. A player without a colour is turned away when the game starts. The seated players get the game once it has
  started (`GameHost` seats them at its first `Poll` after the start).
- **Chat**: in the lobby and over the map (bottom left; Enter starts writing, Enter sends, Escape leaves the line). The host passes each line
  to everyone; joins, colours taken and players leaving are the game's own lines. Lines are cut to 200 characters.
- **The simultaneous first week**: its switch is on the lobby, shown off and disabled, until maps have start zones (MP-2, MP-4).
- **Wire**: protocol 3; new messages `lobby` (the lobby and the player's colour), `takeSeat`, `say` and `chat`.
- **Code**: Core.Network `LobbyHost`, `LobbyState` / `LobbySeat` / `SeatKind`, `LanAnnouncer`, `LanBrowser`, `LanGameInfo`; `GameHost` takes
  seated players from the lobby (`Admit`) and passes the chat on (`Say`, `ChatReceived`); `GameClient` follows the lobby (`Lobby`,
  `LobbySeat`, `TakeSeat`) and the chat (`Say`, `ChatReceived`). Godot.UI `MultiplayerScreen`, `LobbyScreen` (replaces `JoinGameScreen`),
  `ChatPanel`, `NewGameScreen.Lan`; the game app's `Main.Network`. `--host` opens the host's lobby, `--join=` joins a lobby or a game. Smoke
  input `text:` types into the focused field. Tests `LobbyTests` (colours, a colour taken back, chat in the lobby and the game, the start,
  an open colour joined later, another version, a player leaving, discovery on this computer).
- **Not done**: each player picking their own town and hero; a host's "Play again" or load still ends the LAN game for the clients; the
  roadmap's next items (live battles between two remote humans, drop-outs, saves, the turn timer, arena maps in the lobby).

### ADR-195: LAN battles fought live between two computers; a player who drops out pauses the game (MP-1, ADR-098, roadmap, 2026-11-16)
**Status**: implemented (2026-11-16), the roadmap's live battles and drop-outs. *(proposal)* marks the agent's choices.

- **Battles between two computers**: a battle opens only on the computers of its sides (a third player, the host's included, sees its
  result on the map); each player commands only their own side, and their screen says when the other side's player is to act ("Red plays on
  another computer: waiting for their move"). The host sends every battle event to both sides as it happens (ADR-193), so each sees the
  other's moves live. One computer (hot-seat) is unchanged.
- **No quick combat between two computers** *(proposal)*: either side's player could otherwise end the other's battle with the AI; the host
  refuses it, and the button and Q are off. Auto combat's question (ADR-090) is asked only on the computer of the player it concerns, and not
  on a client (its view lacks the random numbers a preview needs).
- **A player who drops out** (MP-1 f): the game pauses for everyone. Every command is refused (the host's player's, the clients', the AI's,
  the turn timer's) and the pause panel says why. It ends when a player joins that colour (the first free colour a joining player gets is a
  dropped one; the game is listed as playing with an open colour) or when the host presses "Computer plays Red": the colour becomes a
  computer player for the rest of the game, its battle side too if it is fighting one (the AI plays its units from the one that is to act,
  and chooses a width or ends a deployment it was to make). The chat goes on during the pause.
- **A colour nobody has joined** *(proposal)*: one left open at the start (ADR-194) pauses the game the same way when its turn or its
  battle comes, so the host can let the computer play it too.
- **The handover is a command**: `HandToComputerCommand` (the player), allowed at any time like the auto combat setting; refused for a
  computer player, a player that is out, and the last player a person plays; logged, so saves and replays keep it. Only the host gives it: a
  client's is refused. Event `PlayerHandedToComputer`, which every player sees.
- **Wire**: protocol 4; new message `paused` (why, or nothing when the game goes on).
- **Code**: Domain `HandToComputerCommand`, `GameFlowService.HandToComputer`, `BattleCommandHandler.HandToComputer`, `PlayerHandedToComputer`;
  `BattleState.Attacker` / `Defender` settable inside the domain. Core.Network `GameHost.Waiting`, `IsPaused`, `PauseReason`, `PauseChanged`,
  `HandToComputer`, `GamePaused`; `GameClient.Paused`, `PauseChanged`. Godot `BattleView3D.PlaysHere`, `WaitingFor`, `QuickCombatPlayer`;
  Godot.UI `PausePanel`; the battle HUD, input, decision panel and auto combat follow them. The game app: the AI and the turn timer wait
  during a pause; the host's AI waits for a battle it does not show; the chat stays over the battle, above its log. Tests `LivePlayTests` (a
  battle fought live from both computers, a drop-out paused until a rejoin, the computer taking over a colour, mid-battle, an open colour at
  its turn, the handover's refusals); `LanGameTests` checks that quick combat is refused between two computers.
- **Not done**: the turn timer still runs on each computer (its roadmap item); anyone may take a dropped colour, not only the player who
  left it; a colour the computer took over does not go back to a person.

### ADR-196: Saves of LAN games: the host's save goes on anywhere, a client saves the host's record (MP-3, ADR-099, roadmap, 2026-11-16)
**Status**: implemented (2026-11-16), the roadmap's saves item. *(proposal)* marks the agent's choices.

- **The host saves** as on one computer (save, quick save, autosave), the whole game.
- **A client's save is the host's whole record** (ADR-099): Save and F5 ask the host for the game's record, labelled with the save's name,
  and write it in the client's own saves, where it loads like any save. It holds the whole game, under the client's fog too (fine on a LAN,
  ADR-099). A client still autosaves nothing *(proposal)*: the host's autosave covers the game.
- **Replays are the host's record**: a client's "Save replay" on the end screen asks for the record without a label and writes it in its
  replays. The ranking stays the host's.
- **A save goes on anywhere**: a save with several people's colours still in the game asks who plays each from now on, on the
  **saved-game screen** (the save, its date and whose turn, each colour's towns and heroes): **Here** (several make a hot-seat game) or
  **Computer**; at least one is played here. A colour given to the computer is handed over with `HandToComputerCommand` (ADR-195), so the
  game's record keeps it and later saves load that way. A save with one person loads at once, as before; F9's quick load keeps every person
  here, as saved *(proposal: no question on a quick load)*. Loading leaves the game being played.
- **Host a saved game** (Multiplayer): the saved-game screen as the host's lobby, with the save list, **LAN player** as a third choice, who
  has joined each colour, the address and the chat. The joined players see the save's map and name, its date and the colours. The game starts
  with them seated, as a new game does; a LAN colour nobody has joined pauses the game at its turn or battle (ADR-195).
- **Wire**: protocol 5; new messages `askRecord` (the request's number and the save's name, none for a replay) and `record` (the record, or
  why there is none).
- **Code**: Core.Network `RecordRequest`, `RecordSent`, `GameHost.RecordOf`, `GameClient.RequestRecord`; Core.Data `GameSaves.SaveRecord`,
  `SaveReplayRecord`, `People`. Godot.UI `SavedGameScreen` (`SavedSeat`, `SavedGameView`), `MultiplayerScreen.HostSavedRequested`,
  `GameResultPanel.ShowMessage`; the game app's `Main.Saves` (`ShowSavedGame`, `StartSaved`) and `Main.Network`. Tests `LanSavesTests` (a
  client's save loads on one computer to the host's state, a client's replay, the refusals, the host's save hosted again and joined, the
  computer taking a colour over at load and the save keeping it).
- **Not done**: a colour the computer plays (in the save or given to it) does not go back to a person; a client's save names no colour as its
  own, so loading it asks again who plays each.

### ADR-197: Start zones in the map and the simultaneous first week (MP-2, MP-4, ADR-098, ADR-099, roadmap, 2026-11-16)
**Status**: implemented (2026-11-16), the roadmap's start zones and simultaneous first week. *(proposal)* marks the agent's choices.

- **Start zones** (MP-4): a cell layer of the map, each tile in one player's zone or none. **Map format v40** (`startZones`, one character
  per tile: `.` none, `0`–`7` the player's number; omitted = none; older maps have none, so no upgrade step). The **generator** writes each
  player's start zone (the template zone of its start town); an untaken slot's zone is nobody's; generator version 11. The **editor** paints
  them with the Start zone tool (a player or Erase, a brush radius; one undo step a stroke) and shows them as tiles of the player's colour.
  The **validator**: an error for a zone of a player the map does not have; warnings for a player without a zone (on a map that has
  zones), a zone without any of its player's towns, a town or hero standing in another player's zone (the zones overlap the starts) and two
  players' zones that touch.
- **The simultaneous first week** (MP-2): the host's switch in the lobby (on the new-game screen), kept in the game's options
  (`SimultaneousFirstWeek`) with the host's colour (`HostPlayer`). A day is simultaneous while the switch is on, no conflict has ended
  it, it is in the first week, the map is no arena, and at least two people play, each with a start zone; otherwise it is played turn by
  turn (MP-2 a). It needs **one colour played on the host** *(proposal: hot-seat cannot hide one person's day from another, MP-2 f)*.
- **How a simultaneous day runs**: when it opens, every person's turn begins at once, in order (income, movement, questions). Each person
  acts and ends their own turn on their own computer; one who has ended waits for the others. Once all have, the **computer players play
  their turns one by one** *(proposal)*, then the next day opens. A person the computer takes over (ADR-195) leaves the open turns and the
  computer plays that turn after the people.
- **One battle or question at a time** *(proposal)*: the game holds one battle and one question, so while one person fights or answers, the
  others' commands wait ("Wait: a battle is being fought.").
- **Conflicts** (MP-2 b): a hero entering another person's start zone; a person seeing another person's hero it did not see when the day
  opened *(proposal: what each already saw then is no conflict)*; a hero visiting an object another person's hero visited that day. The first
  one stops the day: every command is refused until the host has played it again.
- **The day played again** (MP-2 c): the host rebuilds the game from its record up to the day's start, adds `PlayTurnByTurnCommand` (the
  reason, which everyone is told in the chat and the status line) and goes on in that game: the day is played turn by turn from its first
  player, with no second income (a turn begins once a day), and so is every day after it *(proposal: one conflict ends the simultaneous
  week)*. What the people did that day is gone, also from the record. Each client gets the new game's view as on joining.
- **Determinism**: the current player between commands is the host's open turn (else the first); a command runs as its own player's. Both
  follow from the record, so a simultaneous game replays and loads exactly. A save made during a simultaneous day goes on turn by turn when it
  is loaded on one computer.
- **Clients**: each sees its own open turn as the current one and only its own questions; a day's start and its end in conflict reach
  everyone (`SimultaneousDayStarted`, `SimultaneousDaysEnded`).
- **Code**: Domain `AdventureMap.GetStartZone` / `SetStartZone` / `StartZoneOwners` / `StartZoneTiles`, `TerrainEditor.PaintStartZone`
  (`StartZonesPainted`), `MapValidator.StartZones.cs`; `GameOptions.SimultaneousFirstWeek` / `HostPlayer`, `GameState.OpenTurns` / `IsTurnOf` /
  `PlayersToAct` / `DecisionPlayer` / `SimultaneousEnded` / `SimultaneousConflict` / `Content`, `TurnService` (open turns, `PassTurn`,
  `PlayTurnByTurn`, `LeaveOpenTurns`, a turn begun once a day), `SimultaneousDayWatch`, `PlayTurnByTurnCommand` (recorded as `turnByTurn`),
  `GameSession.SimultaneousDayStart`, events `SimultaneousDayStarted` / `SimultaneousDayConflicted` / `SimultaneousDaysEnded`. Data
  `MapDocument.StartZones`; Generation `ScenarioAssembler` writes the zones. Network `GameHost.SessionReplaced`, a client joined again by a
  new `JoinAccepted`, `PlayerViewFeed` per-player current turn and questions. Godot `StartZonesView`, the editor's Start zone tool, the
  lobby's switch (`NewGameScreen.Lan`). Tests `StartZoneTests` (format, generator, editor, validator) and `SimultaneousDayTests` (open turns,
  replay, computers after the people, the host's colour, a zone conflict played again turn by turn, a sight conflict, turn by turn without
  zones and after the first week, a person handed to the computer, a LAN game whose conflict hands out a new game).
- **Not done**: the turn timer still runs on each computer, not one for all (its roadmap item); the shipped hand-made maps have no start
  zones yet; the lobby does not say whether a hand-made map has a zone for every person (it is played turn by turn otherwise); a question or
  battle holds the other people up.

### ADR-198: One turn timer in a LAN game and on a simultaneous day (FLOW-3, MP-2 e, ADR-091, ADR-197, roadmap, 2026-11-17)
**Status**: implemented (2026-11-17), the roadmap's turn timer in multiplayer. *(proposal)* marks the agent's choices. No rule, save or map
format changes; LAN protocol 6.

- **One clock, the host's**: in a LAN game only the host runs the turn timer (ADR-091). It answers the defaults and ends the turn of any
  colour, a client's too (`GameHost.ExecuteAsHost`, which the host's screen cannot otherwise do for a remote colour). It sends the clock to
  the clients when it changes, in whole seconds, with whether it stands still for a battle between two people and whether the time is up
  (`ClockUpdate`, a `TurnClock`); a client that joins gets it with the game. A client runs no timer: it shows the host's clock and its
  warnings (1 minute, 30, 15, 5 seconds), so all computers show the same time and a turn ends once.
- **A simultaneous day** (MP-2 e): one clock for the day, the full time when the day opens, not one per person. It runs while any person's
  turn is open; one who ends early waits for the others, and the day ends when all have ended their turn, or when the time is up: then the
  question being asked gets its default and every open turn ends, in player order. A question without a default (a level-up, a riddle) or a
  battle holds every turn until it is answered or fought, as it holds the others' commands (ADR-197).
- **What holds the end of a turn on the host**: its menu, battle scene and auto combat question hold only the turns of colours played on the
  host *(proposal)*; a client's turn ends on time whatever the host shows. The clock stops while the game is paused (ADR-195). A conflict's
  day played again starts with the full time.
- **Code**: Domain `TurnTimer` (an optional command runner, the day's clock while `GameState.IsSimultaneous`, `TimedPlayers`, `Clock`,
  `WarningPassed`), `TurnClock`; Network `ClockUpdate` ("clock", protocol 6), `GameHost.ExecuteAsHost` / `ShowClock`, `GameClient.Clock`; the
  game app runs the timer on the host and on one computer, and shows the host's clock on a client. Tests `SimultaneousDayTests` (the day's
  clock, both turns ended on time; the host's clock ending a client's turn and reaching the client).
- **Not done**: a client's screens (its battle scene's last moves, a screen left open) do not hold the end of its turn; the clock's time is
  sent in whole seconds, so a client may show it up to a frame late.

### ADR-199: Arena maps over the LAN (ADR-100, ARN-1, MP-2, ADR-197, roadmap, 2026-11-17)
**Status**: implemented (2026-11-17), the roadmap's arena maps in the lobby. *(proposal)* marks the agent's choices. No save or map format
change; no protocol change.

- **The lobby**: an arena map lets every player be a person, so in the host's lobby each colour is Here, LAN player or Computer, the first
  Here and the others **LAN player** by default *(proposal; on other maps a computer-only slot stays the computer's)*. Under the simultaneous
  switch the lobby says what it does with this map and these colours: the first week's days (an arena's **preparation day**) at once;
  turn by turn with fewer than two people; or turn by turn because a named person's colour has no start zone on this map.
- **Day 1** (ARN-1 b): each player prepares in its own view (ADR-193), turn by turn, or **at once** on a map with a start zone for every
  person: an arena map is no longer kept out of the simultaneous first week. Day 2's battle opens by itself on the two sides' computers
  and is fought live (ADR-195); the others see its result.
- **Allies are no conflict** *(proposal)*: on a simultaneous day an ally's start zone, an ally's hero in sight and an object an ally visited
  that day no longer discard the day (allies share a half on Four Banners and hand each other troops); a visit counts against the first
  visitor that day, so an enemy visiting it after two allies still does. The validator no longer warns when allies' start zones touch.
- **The shipped arenas** (ADR-180) get start zones: each player's half on Proving Grounds and Frostfang Pass, each player's quarter of its
  side's half on Four Banners. The rock between the halves is **five tiles** wide (was four), so a hero's ordinary sight (5) does not see
  across: day 1 stays hidden even turn by turn *(proposal)*; the maps are now 35 × 18, 24 × 31 and 41 × 24. A hero with more sight (Scouting,
  an artifact) may still see the other side, which on a simultaneous day plays the day again turn by turn.
- **The result screen**: its headline is this computer's players' victory or defeat (was: any person's, which on a LAN client showed the
  winner's "Victory" to the loser). **"Play again"** (ADR-159) on the host starts the rematch for everyone: the same map, colours and options
  with a new seed, a colour handed to the computer staying the computer's; each client gets the new game as on joining, and the chat says
  the host started it again. A client has no "Play again"; its result screen says the host may start the game again.
- **Code**: Domain `TurnService.IsSimultaneousDay` (arenas allowed), `SimultaneousDayWatch` (allies), `MapValidator.StartZones` (allies'
  zones may touch), `MapSketch.StartZone` and the three arena scenarios. Network `GameHost.PlayAgain` (shares `Replace` with the conflict's
  replay). Godot.UI `NewGameScreen` (arena colours, the switch's note), `GameResultPanel.CanPlayAgain` / `PlaysHere` / `Headline`; the game
  app's `PlayAgain` on a host. Tests `LanArenaTests` (day 1 at once in each player's view and the battle fought live; turn by turn without
  zones; the host's rematch reaching the client), `ArenaMapsTests` (each shipped arena prepared at once by four or two people, no conflict,
  then the battle), `SimultaneousDayTests` (allies' zones and heroes).
- **Not done**: a client cannot ask for a rematch; the host's "Play again" does not go back to the lobby, so its players cannot change
  colours or factions in between.

### ADR-203: What a link may cost a LAN host (ADR-192, ADR-194, ADR-196, 2026-11-17)
**Status**: implemented; extended by ADR-228 (sends that never wait, the chat's allowance, the lobby's waiting players), whose record interval per colour replaces the one per link here. No rule, save, map or protocol format changes.

- **Why**: anyone on the network can connect to a host. A link read any length up to 64 MB and allocated it before the body came, could wait
  for ever without joining, any number could, a `Poll` read everything a link had sent, a message whose reading threw anything but a
  few exception types stopped its reader with the link still up (a seat held by nobody), and every `RecordRequest` made the host write the
  whole record.
- **Limits** (`NetLimits`, Core.Network), the same for the lobby (`LobbyHost`) and the game (`GameHost`):
  - A link the host accepts (`TcpGameListener`) reads messages of at most **64 KB** (`MaxClientMessageBytes`), checked on the length
    before anything is allocated, and closes once more than **256** wait for `Poll` (`MaxClientMessagesWaiting`). It is the limit for the
    link's whole life, not only before the join: a client only sends joins, colours, chat (200 characters), commands and record requests.
    The client's end still reads up to 64 MB (`TcpConnection.MaxMessageBytes`): the host sends the map and whole records.
  - A link must send its `JoinRequest` within **10 seconds** (`JoinTimeout`), else it gets `JoinRefused` and is closed. A seated client,
    or one the lobby hands to the game, has no timeout.
  - At most **8** links wait to join at once (`MaxUnjoined`); one more is turned away at once (`JoinRefused`).
  - A `Poll` takes at most **16** messages from a link (`MaxMessagesPerPoll`); the rest wait for the next.
  - A client gets the game's record at most once every **5 seconds** (`RecordInterval`); a request sooner is answered with an error
    (`RecordSent` without a record), which the client shows like any refused save.
  - Whatever reading a message throws closes the link with the exception's message.
- **Clock**: the hosts read `TimeProvider` (`Time`, internal; the lobby hands its own to the game's host), so tests move time by hand.
- **Checked** by `NetLimitsTests` (TCP: a long length, unreadable JSON, a flood; loopback: the timeout, the waiting cap, the per-`Poll`
  share, the record interval, the lobby's timeout and cap).

### ADR-217: One coordinator for the simultaneous days (ADR-197, ADR-214, 2026-11-18)
**Status**: implemented. No rule, save, map or protocol format change; no event order change.

- **Why**: the simultaneous days were spread over three places. `TurnService` opened the day and set a public `OpenedSimultaneousDay`
  flag; `GameSession` read and cleared it, kept the day's start in the command log and the conflict watch, pointed the current player
  at a command's player and back, and refused the waiting players; both edited `GameState`'s open turns and ended turns.
- **Description**: `SimultaneousDayCoordinator` (Turns; the session creates it and hands it to `TurnService`) holds all of it: whether
  today is played at once and opening it (`TryOpen`, beginning each turn through `TurnService`), a person's turn ending, leaving the open
  turns, the days ending (`PlayTurnByTurnCommand`), the conflict refusal, whose turn a command runs in and who must wait, the conflict
  watch and the day's start in the log (`GameSession.SimultaneousDayStart`). The session tells it `Started`, `Refused` and `Executed`
  with the log's length; the flag is private to it. It alone writes `GameState.OpenTurns`, the turns ended today, `SimultaneousEnded`
  and `SimultaneousConflict`; the state keeps them, since they are part of the game and its digest.
- **Checked**: `SimultaneousDayTests` (the day's start in the log; leaving the open turns). The digests are unchanged (duel baseline
  `A0F1FADCC6D8DEE4`, sim self-test `82AE941CD62212E2`).

### ADR-218: The LAN code changes a client's game through a view sink; a schema hash guards the join (ADR-193, ADR-203, 2026-11-18)
**Status**: implemented. LAN protocol 7 (`JoinRequest.Schema`); no rule, save or map format change.

- **Why**: Domain declared `InternalsVisibleTo` for Core.Network, so the client's `ViewMirror` set the game's date, players, heroes, objects,
  memory and fog through any internal member it liked, and nothing kept Network from calling rule internals on a host. Two builds with the
  same protocol number but different fields in a hero or a town joined, then misread each other's views.
- **Sink**: `IViewSink` (Domain, Game) is the one way into a client's game: `ShowGame`, `ShowPlayer`, `AddHero` / `RemoveHero`,
  `AddObject` / `RemoveObject`, `Remember` / `Forget`, `ReplaceRetiredHeroes`, `ReplaceCaravans`, `MarkExplored`, `SetSight`.
  `GameSession.ViewSink` gives it only on a session made by `FollowingHost`; a host's session has none, so its game still changes only
  through `Execute`. `GameView` / `PlayerView` (what a player is shown of the game and of a player) are public Domain records; the
  host-side reads it needs, `FogOfWar.ExploredOf` and `SightOf`, are public. Domain shows its internals to the tests only.
- **Schema**: `StateCodec.SchemaHash` hashes (SHA-256, first 8 bytes in hex) every Domain type with the keys and types of the fields the
  codec writes, compiler-made types left out and generic types named without assembly versions, so two machines on different runtimes
  agree. A client sends it in `JoinRequest.Schema` (`NetSerializer.Schema`); `GameHost` and `LobbyHost` refuse another protocol or
  another schema (`NetSerializer.VersionRefusal`).
- **Checked**: `NetworkAccessTests` (internals shown to the tests only; only a following session has a sink; the hash and a matching join;
  a different or missing schema refused) and the LAN tests.

### ADR-221: `GameHost` coordinates seats, pause, chat and the day played again (ADR-192, ADR-195, ADR-197, 2026-11-19)
**Status**: implemented. No protocol change.

- **Why**: `GameHost` held the seats, the pause, the chat, the replay of a conflicting simultaneous day and the session routing in one class.
- **Parts** (internal, Core.Network):
  - `SeatManager`: the remote colours, the client links (`HostPeer`), the colours whose player left, choosing a colour on a join, the limit
    on links waiting to join.
  - `PausePolicy`: which colours hold the game up, the refusal while paused, the pause last sent, and the hand-over that goes through it.
  - `ChatRelay`: cleans players' lines and sends every line to the clients with the game and to the host's screen.
  - `SimultaneousDayRewinder`: no state; replays the host's record up to `GameSession.SimultaneousDayStart` plus a `PlayTurnByTurnCommand`.
- `GameHost` keeps the session, the command route, the turn clock, the views, record requests and the order in which `Poll` calls the
  parts. Its public API, wire messages, refusal texts and order of sends are unchanged.
- **Checked**: `SeatManagerTests`, `PausePolicyTests`, `ChatRelayTests`, `SimultaneousDayRewinderTests` and the LAN tests.

### ADR-228: LAN hardening: sends that never wait, the chat's allowance, the lobby's waiting players, the record per colour (ADR-203, ADR-226, 2026-11-19)
**Status**: implemented (2026-11-19), the roadmap's "Code health" item; extends ADR-203 and replaces its record interval per link. No
rule, save, map or protocol format change.

- **Why** (the refactoring close-out, ADR-226): `TcpConnection.Send` wrote on the game's thread with no timeout, so a client that
  stopped reading froze `GameHost.Poll` (and the lobby's) once the socket's buffers filled; a lobby took any number of joined players
  without a colour; a player could write as many chat lines as `MaxMessagesPerPoll` let through, each sent to everyone; and the record
  interval was the link's, so leaving and joining again asked for a new record at once.
- **Sends** (`TcpConnection`, both ends): `Send` serialises the message and queues it; a background task writes the queue in 64 KB
  pieces. The link closes when nothing of the queue goes out for **30 seconds** (`NetLimits.SendTimeout`, counted from the last piece),
  or at once when more than **128 MB** wait (`MaxBytesWaitingToSend`, two of the biggest messages). `Close` still writes what waits for
  up to **2 seconds** (`CloseLinger`), so a refusal's reason reaches the client, then drops the socket; `IsClosed` is true at once.
- **Lobby**: a player joining when no colour is open and **8** players already wait for one (`MaxWaitingForColour`) gets `JoinRefused`
  ("No colour is open and 8 players already wait for one."). Players the host's setup leaves without a colour are not turned away.
- **Chat**: each link may write **5 lines in any 10 seconds** (`ChatLines`, `ChatWindow`; `ChatAllowance`), in the lobby and in the game
  alike; a line beyond it is dropped and its writer gets one lobby line, "You write too fast: wait a moment.", until a line goes through
  again. Lines that are empty once cleaned do not count. The host's own player has no allowance.
- **Records**: the 5-second interval (`RecordInterval`) is the colour's (`GameHost`), not the link's.
- **Checked** by `NetLimitsTests`: over TCP a client that never reads (the sends return at once, the link closes on the timeout), the
  byte cap, a refusal sent just before `Close` still arriving; on loopback the chat allowance in the game and the lobby, the lobby's
  waiting cap, the record interval surviving a new link. The per-`Poll` share is now checked with commands (chat would hit the allowance).
