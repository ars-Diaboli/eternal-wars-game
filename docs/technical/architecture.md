# Architecture

## 1. Goals
- **Game logic is engine-independent**: rules run and are tested without Godot (headless sim, AI, tools).
- **Three front-ends, one core**: the 3D game, the map editor and the 2D harness drive the same core services.
- **Event-driven presentation**: core publishes facts on a custom bus ([event-system.md](event-system.md)); views
  subscribe and render, never poll.

## 2. Tiers

```
Tier 3 · Apps (Godot projects)       EternalWars.Game (3D, Forward+), EternalWars.MapEditor (3D, Forward+),
                                     EternalWars.Harness2D (2D debug, Compatibility)
Tier 2 · Godot libraries             EternalWars.Godot.UI      HUD, panels, editor and setup screens, debug overlays
         (GodotSharp)                EternalWars.Godot.Shared  map/hero/battle views, cameras, input, event glue, GameContent
                                     EternalWars.Godot.Assets  models, textures, shaders, prefabs (no code, no csproj)
Tier 1 · Core (plain .NET 10)        EternalWars.Core.Network     LAN games: messages, TCP links, discovery, lobby, host and client, each player's view, chat, pause, saves, simultaneous days (ADR-192 to ADR-197)
                                     EternalWars.Core.Data        file formats: maps, saves/replays, settings, templates
                                     EternalWars.Core.Generation  random maps: template model, validator, generator stages
                                     EternalWars.Core.Domain      model + rules, commands, events, AI, editing
                                     EternalWars.Core.Events      pub/sub primitives
tests/EternalWars.Core.Tests   xUnit v3, covers Tier 1
tools/EternalWars.Tools.Sim    headless console app over Tier 1 (see development.md)
```

### Dependency rules

| From \ To | Core.Events | Core.Domain | Core.Generation | Core.Data | Core.Network | Godot.Shared | Godot.UI |
|---|---|---|---|---|---|---|---|
| Core.Domain | ✓ | — | ✗ | ✗ | ✗ | ✗ | ✗ |
| Core.Generation | ✓ | ✓ | — | ✗ | ✗ | ✗ | ✗ |
| Core.Data | ✓ | ✓ | ✓ (template format) | — | ✗ | ✗ | ✗ |
| Core.Network | ✓ | ✓ | ✓ | ✓ (records, commands) | — | ✗ | ✗ |
| Godot.Shared | ✓ | ✓ | (avoid) | (avoid) | ✗ | — | ✗ |
| Godot.UI | ✓ | ✓ | ✓ (template editor) | (avoid) | ✗ | ✓ | — |
| Apps | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Core.Events depends on nothing; apps never reference each other. Enforced by `Directory.Build.targets` (build fails if a
`src/Core` project uses `Godot.NET.Sdk` or references `GodotSharp` or a `*Godot*` project) and by `ArchitectureTests` (every project's
references against this table); `Directory.Build.props` builds core with `TreatWarningsAsErrors`.

## 3. Runtime composition

```
Player input ──► App (Main.cs) ──session.Execute(cmd)──► GameSession (Core.Domain)
                                                           │ services validate, mutate GameState, publish
Godot views ◄──subscribe── session.Events (EventBus) ◄─────┘
```

- **`GameSession`**: composition root of a running game: one `GameState`, one `IEventBus`, the services
  (`TurnService`, `HeroMovementService` + `PathFinder`, the map object services, `EconomyService`,
  `VisibilityService`, `ArmyService`, `ProgressionService`, `ArtifactService`, `BattleService`, `GuardService`,
  `TownService`, `TownVisitService`, `CaravanService`, `AdventureMagicService`, `GameFlowService`). The map object services share one
  `ObjectInteractionContext` (state, publisher, economy and the common steps: `Ask`, `RemoveFromMap`, `TryCollect`,
  `GiveResources`, `HeroAt<T>`): `MapObjectInteractionService` (visits via `VisitRules`, the reward objects' rules in
  `MapObjectRewardVisits`; hero meetings), `ObjectDecisionService` (the answers to the decisions visits open),
  `ObjectShopService` (what a hero does at an object from its panel: recruit, buy, upgrade, hire, sacrifice, lessons,
  garrison troops), `ObjectLifecycleService` (game-start rolls and prisoners, the weekly refugees / merchant stock),
  `GrailService` (burying the Grail, obelisk visits, digging; the rules in `GrailRules`) and `PandorasBoxService` (opening a box,
  after a yes or a won guard battle). `TownVisitService` is every hero's arrival in a town (governor, Grail building, unique
  buildings' bonuses, guild spells, mana after a night) and every capture; the movement, decision, battle, map event and
  adventure-magic services all go through it. `TownConstruction` raises a building or dwelling level for both the day's build and
  a map event's free build (a guild, war lodge, school or unique building lets the town's heroes take it at once). A hero joining a
  player (town or map tavern, prison, map event) goes through `HeroRecruitment`; an artifact taken away (Grail building, altar, map
  event) through `ArtifactService.Remove`. Game start: `PlaceholderService` draws the placeholders, then the Grail, the
  prisoners, the objects' rolls and the guild spells (`TownService.DrawGuilds`) are drawn in that order.
- **`*Rules` classes are pure**: static `*Rules` classes hold constants and queries (checks, costs, odds, offers) and never
  change the state or publish; the changes they lead to belong to a service. `RulesPurityTests` fails on a `*Rules` method that
  takes an `IEventPublisher` or returns nothing.
- **Commands only** (ADR-036): front-ends, AI, tools and tests change the game with `session.Execute(GameCommand)`;
  service mutators are `internal`. Accepted commands go to `session.CommandLog`; map + seed + log replay the game
  (`GameReplay`).
- **Executing a command**: `Execute` authorizes it, then hands it to its handler (`CommandHandlers`, one
  `ICommandHandler<T>` per concrete command type; adding a second throws). Authorization: an `IBattleCommand` needs a running
  battle, whoever's turn it is (the battle checks the side); `SetAutoCombatCommand` is allowed any time; everything else only in
  the player's turn, and an `IHeroCommand` only for an own hero on the map (`Hero`; `MoveStackCommand` maps it to `FromHero` and
  also needs its `ToHero` on the map). Other commands that name a hero (artifacts, governors) leave that check to their service.
- **Adding a command**: a `sealed record … : GameCommand(Player)` in `Commands/GameCommands.cs`, marked `IBattleCommand` or
  `IHeroCommand` when it is one; one line in the area's registration (`AdventureCommandHandlers`, `ObjectCommandHandlers`,
  `TownCommandHandlers`, `ArtifactCommandHandlers`, `BattleCommandHandlers`): `handlers.Add<T>(c => service.Method(...))`, or
  `AddWithOutcome<T>` when the result goes back in `CommandResult.Outcome`; and its replay name in `GameRecordSerializer.CommandTypes`
  (Core.Data). `CommandHandlerTests` fails for a command without a handler, `RecordCommandTypesTests` for one without a replay name (or a name used twice).
- **Turns**: `TurnService` moves the calendar (players, days, weeks; on a simultaneous day every person's turn at once, ADR-197, whose open
  turns, ended turns, conflict watch and start in the command log are `SimultaneousDayCoordinator`'s alone, ADR-217), publishes `GameStarted` / `DayStarted` / `WeekStarted` /
  `WeekEventAnnounced` / `TurnStarted` / `TurnEnded` and rolls the week's events; everything else happens in the
  `ITurnPhaseParticipant`s the session gives it, called hook by hook (`SetUpGame`, `StockFirstWeek`, `StartDay`, `StartWeek`,
  `ApplyWeekEvent`, `StockWeek`, `OpenDay`, `PrepareTurn`, `OpenTurn`) in list order: map events, progression, movement, mana
  (`ManaRestoration`), towns, caravans, economy, guards, map objects. The order fixes the events and the random draws, so it is
  part of the rules. The deciding battle's day of an arena or a tournament replaces the day (`DecidingBattleService.StartBattle`).
- **Core subscriptions**: services that react to events list them in their own `SubscribeTo(IEventSubscriber)`
  (`VisibilityService`, `MapEventService`, `AdventureMagicService`); the session calls them, visibility first, before any view
  subscribes.
- **Services** validate, mutate `GameState`, then publish past-tense events. They depend on `IEventPublisher` only.
  A rule refuses a command by throwing `CommandRefusedException` (an unknown id from `GameState.GetHero` / `GetObject` /
  `GetPlayer` does too) before it changes anything; `Execute` turns only that into a rejected `CommandResult`, so any other
  exception is a bug and goes on up. The tests turn on `GameSession.CheckRefusals` (the runtime switch `EternalWars.CheckRefusals` in
  the test project), which fails a refusal that changed the digest (ADR-165, ADR-239).
- **Content tables** travel together as `ContentCatalogs` (creatures, heroes, buildings, artifacts and their spells, map
  objects, terrains; each left out is the built-in table, `ContentCatalogs.Default` all of them). Everything that builds a
  game takes one record and no separate tables: the scenarios, `MapConverter.ToGameState(document, content, options)`,
  `GameSaves.Load` / `GameReplay.Replay` / `ReplayPlayer.Open`, `MapGenerator` and `EditorMapGenerator`. Each app builds one
  record (the Godot apps `GameContent.All` from the `--creatures=`-style flags, the Sim tool from `--creatures`) and passes it on.
- **`GameState`** is plain data with `internal` setters. It carries `GameOptions` (fog mode, refog margin, seed, difficulty, the players' colours; game
  rules, not map data, so passed to `MapConverter.ToGameState(document, content, options)`) and `GameState.Random`
  (`GameRandom`), the only randomness rules may use.
- **Pending decisions**: outcomes needing a choice (chest, exchange, guard offer, level-up, battle, ...) set
  `GameState.PendingDecision` and publish `DecisionRequested`; movement and End Turn are refused until the matching
  command resolves it. A battle is a `BattleDecision` resolved when the battle ends. Core never waits; UI or AI answers
  whenever it wants. Every service asks, takes and resolves through `DecisionSteps` (ADR-215), which also removes objects from
  the map: take the decision, check the answer (a refusal changes nothing), resolve, then apply the answer.
- **Settle**: after `Start()` and every accepted command `GameSession` runs `GameFlowService.Evaluate()` (eliminations,
  winner, passing the turn of a just-defeated player), then publishes `CommandExecuted`. Once `GameState.Outcome` is set,
  `Execute` refuses everything. `TurnService` skips defeated players; `GameState.Rules` (`ScenarioRules`) says what counts and
  `GameState.Tracking` (`ScenarioTracking`, ADR-213) holds what the rules remember from the start (teams, heroes, prisoners).
- **Click-to-move**: `Execute(MoveHeroCommand)` → `MoveTo` → `PathFinder` → per step `TryStep` (validated by
  `MovementRules`, publishes `HeroMoved`) → on an entrance `MapObjectInteractionService.Visit` → outcome events /
  `DecisionRequested`. A step towards a monster or into its guarded area goes to `GuardService`; onto an enemy hero to
  `BattleService`.
- **Routes**: `PathFinder.FindPath` is A* (a distance estimate that never counts more than the cheapest steps, so routes are the cheapest),
  plain Dijkstra once a gate links two tiles. `HeroMovementService.FindPath` gives its last route again while
  `GameSession.ChangeStamp` (commands accepted + events published + its own heroes' moves + a LAN client's view writes) stays the same, so a move right after a check does not search
  again. For many targets at once, `PathFinder.CostsFrom` (from one tile) and `CostsTo` (to one tile, from many) are Dijkstra searches that
  run only as far as the questions need and answer exactly what `FindPath` would give as the route's cost; the AI weighs its targets, its
  exploring and its Dimension Door landings with them and searches the full route only for the target it picks (by moving).
- **Views** subscribe with `node.SubscribeWhileInTree<T>(...)`. Code-built forms use `FormBuilder` (Godot.Shared/Controls:
  `AddRow`, `AddSpin`, `AddOptions`, `AddChoice<T>`, `AddOwnerChoice`) and empty a container with `node.ClearChildren()`. A view that rebuilds on many
  events uses a `RefreshScheduler` (Godot.Shared/Events: `.On<T>(events)`), which refreshes once at the end of the frame however many
  asked; with `.OnlyWhileShown()` a hidden view (a tab not shown, the editor's events panel) waits and refreshes once when it shows. A large screen is made of sections (`ScreenSection<TSubject>`, ADR-223: the town screen's in `Hud/TownScreen`, the hero
  screen's in `Hud/HeroScreen`), each with its own scheduler and events, rebuilding only itself; the screen rebuilds them all when it
  opens and on the few events that change what it shows at all; the town screen's sections take an `ITownSectionHost`, so the game's
  `TownScreenPanel` and the 2D harness's `TownPanel` hold the same ones. `TownScreenPanel` is one partial class over several files (ADR-296):
  the frame (bars, building strip, window host, keys) and one file per group of windows (`.Hall`, `.Recruitment`, `.Shops`, `.Defences`);
  the open window is rebuilt whole on the screen's events. Its main area shows the town's `TownScene3D` (Godot.Shared/Towns, ADR-297)
  where the town type has a layout, the key painting elsewhere and in the harness: the screen hands the scene the pointer (`Hover`,
  `Pick`) and opens a window on its `BuildingPicked`; the game app hides the adventure world, its light, its camera and the HUD's map
  parts on the scene's `Opened` (`GameScreens.ShowTown`). The numbers they show come from core read models: `TownOverview` (income, each
  line's growth by part, built today, the governor's discounts, each plot's state) and `TownPlans` (what Recruit all and Upgrade all
  would do), which `RecruitManyCommand` and `UpgradeAllCommand` carry out. Widgets shared by several screens are in `Widgets/` (ADR-294,
  ADR-296): the army row (`ArmyBar`, its moves given by the screen through an `ArmyBarGroup`), `CostLabel`, `SliderDialog` (with an
  optional choice beside the count), `ConfirmDialog`, `CreatureCard`, `BuildingTile`, `WindowFrame` and `FocusOrder`. The editor's inspector has one `InspectorSection<T>` per map object
  type in the `InspectorSections` registry (`Editor/Inspector`). The map editor's actions are commands in a `CommandRegistry` (`Editor/Shell`), from which its menus and toolbar are built and which
  runs the shortcuts. Its tools are classes (`IEditorTool`, `Editor/Tools`): the app routes
  the pointer to the one in use, and the tool edits through the session. The editor's
  Problems panel validates a copy of the map (the map document) on a task and takes the result back on the main thread; the validator
  marks each `MapIssue` with the `IssueKind` of the check that found it. The editor app's `Main` builds each map's views and panels and
  maps the input; its commands, menus and windows are composers, one per area (`FileComposer`, `EditComposer`, `ViewComposer`, …),
  sharing an `EditorParts` that holds the lasting parts and the open map's (ADR-288). **Apps** hold composition and input mapping only.
- **The game app** (ADR-222, ADR-231): `Main` builds the views and maps input; the policy it runs lives elsewhere. `GameDirector` (Core.Domain/Game)
  runs a shown game once a frame: the colours played here (`PlaysHere`, `PlaysElsewhere`, `Viewer`), the turn timer's clock and when it
  may end a turn (`RunClock`), a person's turn that waits for the computer's moves to be shown (`TurnStarted`, `TakeTurnToShow`) and the
  computer players' turns (`PlayComputer`), changing the game only through the AI's and the timer's commands. What it is told about the
  computer it runs on is a `LocalSeats` (one computer by default; a LAN host's or client's from `LanSeats` in Core.Network). The replay
  viewer's stepping is `ReplayController` (Core.Data/Records) over a `ReplayPlayer`: play and pause, speed, the eyes the map is seen
  through, the moments at a turn's start and on a battle's result, what holds a frame (`ReplayViews`), putting a diverged game back,
  skipping a battle, playing one on. When to autosave is `AutosaveSchedule` and where the map's camera goes and which hero is selected is
  `MapFocus` (both Core.Domain/Game); a rematch is `GameRematch` and a loaded save's colours `SavedGameStart` (Core.Data/Records); a typed
  address, the games found and a lobby's colours read through `LanAddress` and `LanListing`, and `LanAccept` routes a host's new
  connection (Core.Network). Which screen shows (the screens before a game, the F10 menu and its button, the map or the battle scene) is
  `GameScreens` (Godot.UI).

### Feature map (Core.Domain unless noted)

| Area | Key types and rules |
|---|---|
| Artifacts (ADR-061, ADR-121) | `ArtifactCatalog` (from `artifacts.csv`), `HeroArtifacts`, `ArtifactBonuses` read via `Hero.TotalAttack` / `SkillLevelOf` / `Bonus`; call `GameState.RefreshArtifacts` whenever equipment changes (incl. hero joining, editor edits). `ArtifactService` equips, gives, picks up, loots, `Draw(tier)` (relics from `GameState.DrawnRelics`). |
| Marketplace (ADR-067) | `MarketplaceRules` rates; `TradeResourceCommand` → `EconomyService.Trade`. The AI trades for a building (`TownManager.Market.cs`, ADR-189). |
| Tavern (ADR-060) | `TavernRules` (offer = stateless weekly shuffle; hire allowed?), `TownService.HireHero`. Tavern is a building level in `buildings.csv`. |
| Hero limit (ADR-068) | `ScenarioRules.MaxHeroes`, `Player.MaxHeroes`; `GameState.HeroLimitReached` is the one check. `GameSession.Start` removes surplus heroes. |
| Adventure AI (ADR-059, ADR-119, ADR-220) | `AdventureAi` (`AI/`) plays only through `Execute`, a coordinator of parts sharing an `AiContext`: `ThreatMap` (enemy heroes' danger), `TargetValuer` (a hero's targets), `TownManager` (building, recruiting, garrisons, trades) and `DecisionResponder` (pending decisions); every number they are tuned with is an `AiWeights` (`Default` is the shipped AI; the sim's `--weight Name=value` overrides one, never below 0; ADR-232); `BattleEstimator` plays the real engine on battles of its own, outside the game, through `BattleFlow.PlayComputer`, and keeps its estimates from turn to turn (bounded); `Explain` for debugging; an arena's preparation day in `AdventureAi.Arena.cs` and `TownManager.Arena.cs` (ADR-160); the adventure spells it casts in `AdventureAi.Spells.cs` (ADR-184). The game app's `GameDirector` calls `PlayTurn()` once a frame for computer players (ADR-222). |
| Magic (ADR-110, ADR-120, ADR-122) | `Magic/`: `Spells` (from `spells.csv`), `MagicRules`, `AdventureMagic` / `AdventureMagicService`; battle casting in `Battles/BattleMagic`, effect kinds in `Battles/Spells/` (see Battles). |
| Campaigns (ADR-241) | Core.Domain `Campaigns`: `Campaign` / `CampaignMap` (the maps in order and what each carries in), `CampaignProgress` (the next map, difficulty, variables, heroes, days), `CampaignRules` (a map's options, `Finish`: what a won map carries on), `CarriedHeroService` (puts `GameOptions.CarriedHeroes` on the map at game start, before the placeholders). Core.Data: `CampaignSerializer` (`*.ewcampaign.json`), the record's `CampaignRecord` (save v6). The shipped campaign is built in code (`Scenarios/BorderWatchCampaign`). The game app's `Main.Campaign` runs the screens between maps. |
| Content ids (ADR-243) | `CreatureId`, `SpellId`, `ArtifactId`, `BuildingId` (`Common/ContentIds.cs`): one type per kind, a string converts to one without a cast, one converts back only through `.Value`. Files and LAN messages write them as plain strings (`ContentIdJsonConverterFactory`, `StateCodec`). `FirstOrDefault()` over ids gives an empty id, never null. |
| Map format (ADR-174) | `MapDocument` is the current format only; `MapSerializer` / `GameRecordSerializer` upgrade an older file's JSON first through `MapMigrations` (`IMapMigration` steps by version). Pinned in tests: a sample per version with a step and the current one, the JSON shape of maps and saves, and generated maps per `MapGenerator.Version`. |
| LAN games (ADR-192 to ADR-199, ADR-202, ADR-203, ADR-228 to ADR-230) | Core.Network: before the game, `LobbyHost` takes the joining clients into the colours the host's setup leaves to LAN players (`LobbyState`: the map and settings in words, the colours and who plays them, never the seed) and passes the chat on; `StartGame` hands the seated clients to a `GameHost`. `LanAnnouncer` answers the UDP probes of `LanBrowser`s with the host's game (`LanGameInfo`; `LanAnnouncer.Describe`: the lobby's game and open colours, or the started game's remote colours without a player). `GameHost` runs the one real session, seats clients in the remote human colours and runs their commands; it coordinates `SeatManager` (colours, links, who left), `PausePolicy` (the pause), `ChatRelay` (the chat) and `SimultaneousDayRewinder` (a conflicting day played again) (ADR-221). Each client holds only its player's view: a `PlayerViewFeed` on the host sends, once a `Poll`, the pieces of the game the player may see that changed (its own heroes and possessions, the heroes it sees, the objects in its sight, its memory and fog, the date and turn, its own question and battle; never another's resources) and the events whose declared audience it is in (`[SeenBy]`, ADR-202) (`ViewUpdate`); out of sight an object stays as last sent. `StateCodec` writes those pieces field by field (no per-type code; definitions by id, only the game's own types read back). The client's `GameSession.FollowingHost` runs no rules; its `ViewMirror` copies each piece into the objects the views already hold, changing the state only through the session's `IViewSink` (`GameSession.ViewSink`, which only such a session has; Core.Network sees no Domain internals, ADR-218), and publishes the events, with `HeroPlaced` / `HeroRemoved` for heroes coming into and out of sight. A client joins with the protocol version and `NetSerializer.Schema` (`StateCodec.SchemaHash`, a hash of the fields the views carry and of enum numbers, runtime base types by name only); a host or lobby refuses either differing. A client saves the host's whole record: it asks for it (`RecordRequest`) and the host answers with `GameHost.RecordOf` (`RecordSent`); a save hosted again is a loaded session handed to the lobby like a new one. The simultaneous first week (`GameOptions.SimultaneousFirstWeek`, `HostPlayer`): `SimultaneousDayCoordinator` opens every person's turn at once (`GameState.OpenTurns`, `IsTurnOf`; `TurnService` begins each, once a day, `BegunToday`), runs each command as its own player's and points the current player back at the host's open turn between commands; its `SimultaneousDayWatch` flags the day's conflict (start zones from `AdventureMap.GetStartZone`, objects visited, heroes seen), after which `GameHost` replays its record up to `GameSession.SimultaneousDayStart` plus a `PlayTurnByTurnCommand` and hands the new session out (`SessionReplaced`; each client gets a new `JoinAccepted`); an arena's "Play again" hands out a new game the same way (`GameHost.PlayAgain`). Both plug into `GameSession.Route` (`ICommandRoute`): the host refuses a remote colour's command given on its screen, quick combat in a battle between two computers, and every command while it is paused; a client sends its own colour's to the host (`CommandResult.Sent`). `GameHost.Waiting` lists the remote colours without a player that hold the game up (one whose player left, one nobody joined whose turn or battle it is); while there is one the game is paused (`GamePaused` to the clients) until a player joins it or `HandToComputer` runs the core's `HandToComputerCommand` (the player and its battle side become the computer's). The turn timer is the host's alone (`LanSeats.Of(host)` for its `GameDirector`): its `TurnTimer` runs the commands through `GameHost.ExecuteAsHost` (any colour) and `GameHost.ShowClock` sends its `TurnClock` when it changes (`ClockUpdate`), which a client shows (`GameClient.Clock`); on a simultaneous day the clock is the day's and at zero ends every open turn. `NetMessage`s are JSON with the save files' settings (`GameRecordSerializer.CreateOptions`), framed by length over TCP (`TcpConnection`, `TcpGameListener`) or in memory (`LoopbackConnection`); everything runs on the game's thread in `Poll`. What a link may cost the host is bounded (`NetLimits`, ADR-203, ADR-228): a client's messages are at most 64 KB with at most 256 waiting, a link joins within 10 seconds, at most 8 wait to join, at most 8 joined players wait in a lobby with no colour open, a `Poll` takes at most 16 messages a link, a player writes at most 5 chat lines in 10 seconds (`ChatAllowance`), and a colour gets the record at most every 5 seconds. `TcpConnection.Send` never waits: a thread of the link's own writes the queue (another reads), closing the link when a 64 KB piece does not go out within 30 seconds or more than 128 MB wait, and a closed link still writes what waits, each message within 2 seconds. The HUD asks `MapViewContext.PlaysElsewhere` / `PlaysHere` / `ListedPlayer` whose turn shows and whose lists it shows. Godot.UI takes plain records (`LanSetup`, `LobbyView`, `LanGameListing`): `MultiplayerScreen`, the new-game screen as the host's lobby (`NewGameScreen.Lan`), `LobbyScreen`, `ChatPanel`, `PausePanel`, `SavedGameScreen` (a save and how its people's colours are played from now on, on one computer or as a LAN host's lobby); the game app's `Main.Network` connects them. `BattleView3D.PlaysHere` opens only the battles of this computer's players and lets them command only their own side. |
| Saves (ADR-058) | `GameSaves` (Core.Data): initial map + options + command log, digest-checked on load with the record's `DigestVersion` (ADR-213: older records check with version 1); works mid-battle since battle actions are commands. Autosave hooks `CommandExecuted`. Game app builds its nodes under one `Game` node (freed on load); `BattleView3D.Restore` shows a running battle. |
| Arena (ADR-097, ADR-158) | `ScenarioRules.Arena`; `ArenaRules` (preparation day: `MovementAllowance.Daily(state, hero)`, `TownRules.HasBuiltToday`; `Plan` chooses sides and champions); the battle is the deciding battle it shares with the tournament (`Game/DecidingBattle.cs`: `DecidingBattles` (`Decides`, `Day`, `IsDue`, `Champion`), `DecidingBattleService.StartBattle` runs from `TurnService` on its day instead of a turn; `BattleService` records the result in `GameState.DecidingBattle` (no experience, no AI retreat); `GameFlowService` ends the game from it). The tournament (ADR-366): `ScenarioRules.Tournament`, `GameOptions.TournamentDays`, `TournamentRules` (lengths, `BattleDay`, what a tournament must be; `Plan` with the coin toss, the neutral `Battlefield`, `Reaches` for the start zone that sight (`FogOfWar.Reach`), the adventure spells and `AdventureIntel` keep to), `BattleState.NoLeaving`, the per-player `TavernRules.Taken`, `GrailState.Zones` and `GameState.DrawnRelicsOf`, `Editing/Checks/TournamentCheck.cs`. `CombatPlace` object. The editor's checks in `Editing/Checks/ArenaCheck.cs`, the contents shown on day 1 in `AdventureIntel.ArenaContents` (ADR-161). |

### Battles
- `BattleState` (`GameState.Battle`): field (width × 24 cells), units with footprints, obstacles, round, queue, own
  forked RNG. `BattleGrid`: pure geometry (reach, adjacency, line of fire, cover); a unit's reach is kept beside its battle and handed out again until something the search reads changes. `BattleEngine`: rules for the active
  unit's actions and blows; it builds one `BattleWounds` (damage, deaths, healing, Martyrdom shares, blind wake-ups; shared per action),
  `BattleSpellResolver` (casting; each effect kind's handler does the rest) and `BattleRounds` (turn order, round start, end of battle) per instance. `BattleAi`: computer
  sides. `BattleService`: the session's facade for map ↔ battle, composed of `BattleStarter` (sides, battlefield, siege, width choice),
  `BattleCommandHandler` (players' battle commands, auto combat preview), `BattleFlow` (deployment, AI play until a person acts) and
  `BattleAftermath` (mana, survivors, `NecromancyRules.Raise` after survivors and before experience, defeated heroes). A guarded map object
  implements `IBattleGuard`: it only describes what the battle's end does to it (a `GuardOutcome`: keeps its survivors, or is removed, flagged,
  plundered, opened, captured), and `BattleGuardService` applies the description (ADR-216).
  `Siege`, `WarMachines`,
  `PrimalFury` (rage, warcries via `BattleWarcryCommand`), `SurrenderRules`, `AutoCombat` beside them.
- Battle spells: each `SpellEffectKind` has one `ISpellEffectHandler` (`Battles/Spells/`, registered in `SpellEffectHandlers`,
  looked up with `For(kind)`) holding that kind's rule and its AI value side by side: its own aim checks (`CastRefusal`, `AimRefusal`,
  `AffectedRefusal`), how a cast works (`Apply`), what the AI thinks it is worth (`Value(CastContext)`) and where the AI aims a
  hero's cast (`Aims`). `CastContext` bundles the battle, side, spell at its rank, target, cell, second aim and a creature caster's
  Spell Power. The checks and numbers every kind shares stay in `BattleMagic` (aim legality, reach and area, `Affects`, the damage
  plan used by the damage handler and the AI, amounts and durations); `BattleSpellResolver` pays and reports the cast and keeps the
  shared frames (`OnReach`: resisting stacks, Earthquake, wake-ups; Raise Dead's `Rise`); `BattleAi` only dispatches. A new effect
  kind is its head in `Magic/Parsing/SpellEffectHeads` (and any new words in `SpellEffectWords`) plus one handler class registered in
  `SpellEffectHandlers` (`SpellEffectHandlersTests` checks every kind has exactly one). The same pattern (ADR-219) holds map event actions
  (`MapEvents/Actions/MapEventActionHandlers`: running, a message's reward, what the action holds, its editor line and its worth to the AI,
  ADR-237), adventure spells (`Magic/AdventureSpells/AdventureSpellHandlers`), scenario conditions
  (`Game/Conditions/ScenarioConditionHandlers`) and the editor's checks (`Editing/Checks/MapChecks`, run in order); a test per registry
  fails while one is missing or doubled.
- Creature special rules are data (`CreatureCapability`, max `CreatureDefinition.MaxCapabilities` = 5, ADR-042); code
  asks `creature.Has(kind)`.
- Battle commands are authorized per side, not per map turn (hot-seat defender acts during the attacker's turn).
- Morale per stack at round start (`BattleUnit.RoundMorale`), luck per hit; values from `BattleState.MoraleOf` /
  `LuckOf` / `InitiativeOf` (creature + hero + timed `BattleSideEffect`s). **Nothing is rolled at 0**, so other battles'
  random sequence is unchanged.
- War machines (ADR-056) are `BattleUnit`s with `Machine` set: speed 0, no melee/retaliation/morale; only the offensive
  one is queued; one-column footprint (`WarMachines.Footprint`); `BattleState.LivingArmyOf` (creatures only) decides the
  end.
- Deployment (ADR-072, ADR-076): `BattlePhase.Deploying` for a human side with higher Tactics; `BattleDeployCommand` →
  `Redeploy` (checked by `BattleSetup.DeploymentRefusal`), `BattleStartCommand` → `EndDeployment`.
- Front-ends map a pointed cell to an action with `BattleTargeting.ActionAt` / `DeploymentPlacement`, and an action to its command
  with `BattleTargeting.CommandFor` (`CommandAt` does both).
- The end of a battle is read once into a `BattleOutcome` (winner, reason, losses, the winning hero's experience: none in an arena
  battle, the defeated hero's artifacts). `BattleAftermath.Finish` applies it and auto combat's `Preview` shows it, with the undead raised
  into the army the hero fought with (a held town's garrison for a garrison hero) from a copy of the random numbers.
- Presentation (ADR-052): `BattleView3D` (Godot.Shared/Battle) builds from `BattleStarted` and plays later events as
  one sequential animation queue (a whole AI turn resolves inside one command); it takes input only once the queue has
  caught up. It subscribes and coordinates: `BattleStage` (the field's live pieces and the step each event becomes),
  `BattleFieldBuilder` (field nodes; each material made once per builder),
  `BattleAnimationQueue` (the sequenced tween playback), `BattleAnimations` (tween recipes) and the engine-free
  `BattleNarrator` (Core `Battles/`: the combat-log line of each battle event, usable headless). `BattleHud`
  (Godot.UI/Battle) overlays it. The harness's `BattleBoard2D` redraws from live state.
- Town scene (ADR-297): `TownScene3D` (Godot.Shared/Towns) loads a town type's layout scene, keeps one loaded (out of the tree while
  closed, so its environment and lights leave with it) and shows each plot (`TownPlot`) as `TownOverview.PlotsOf` has it: the
  building's model for its level, the layout's own `Model`, or a `PlotBlock`. It refreshes its plots on the building events and the new
  day. Picking is a ray against the plots' hover boxes, not physics, so it works in the frame a plot appears. A layout missing a
  plot reports an error when it loads; the town type's `bg_town_<town>` painting, when there is one, replaces the layout's sky
  (`town_sky.gdshader`, ADR-300). Around the plots `TownDressing` shows the core's `TownScenery` (ADR-301): the layout's wall line
  built as the siege has it (`SiegeRules.Create`), the street groups by the town hall's level, banners in the owner's colour and
  the garrison hero's banner; `TownPlot` adds scaffolding the day a building goes up and the citizens' homes (`SceneryKit`). The camera
  (ADR-302) rests at the hero shot, turns and zooms (`Turn`, `Zoom`, by the town screen's keys and wheel) and eases toward a building
  behind its window (`FocusOn`, `Unfocus`). Hover and pick (ADR-304): the hovered building's meshes get `town_outline.gdshader` as their
  material overlay; plots the core marks `PlotInfo.Buildable` pick too and show a ghost (`TownPlot.ShowGhost`, also "Show in town",
  `TownScene3D.ShowGhost`); `Step` and `PickHovered` are the ring of plots. The construction scene runs in the scene's own `_Process` on
  `TownBuilt` / `DwellingBuilt` for the town shown (`TownPlot.BeginRise`, a `CpuParticles3D` of dust), timed by `Speed`, which the game
  app sets to the own heroes' speed setting. `Prepare` (ADR-306) loads a town's layout on the resource loader's threads and builds it out
  of the tree while the scene is closed (one layout kept); the UI's `TownArrival` calls it for a route shown ending at an own town and at
  the turn's start, and fades through black when the scene opens. What the scene loads or makes in C# (the layout's packed scene, the
  sky's painting and material) is disposed once the scene holds it, so a replaced layout's resources go at once rather than at a garbage
  collection (ADR-308). The game app's `BudgetOverlay` (F3) reads the renderer's counts against `TownSceneBudget`.

### Map objects
- **Definition** (`MapObjectDefinition`, data: id, `Kind`, footprint, kind parameters) vs **instance** (`MapObject`
  subclass per kind: id, top-left position, mutable state). Built-in definitions in `BuiltInMapObjects`; JSON
  additions via `MapObjectDefinitionSerializer` + `MapObjectCatalog.With(...)`. An object may turn into another
  definition of `GameState.ObjectCatalog` (same id). Owned objects implement `IFlaggable`.
- **Footprint** rows: `B` blocked, `V` entrance (exactly one), `.` free. `GameState` indexes occupied cells (O(1) lookup).
- Behaviour the rules ask of any object is declared on the instance (`EndsRoute`, `TeleportTarget`, `IsPickup`,
  `IsDefendedAgainst`, `DigestState`), so pathfinding, movement and the digest have no per-kind code.
- **New kind** (ADR-166) = enum value + subclass (the compiler asks for its `DigestState`) + entry in `MapObjectKinds` + rule in
  `MapObjectInteractionService.VisitRules` + document type and codec in `MapConverter.ObjectCodecs`; `MapObjectKindsTests` fails
  until all of them exist. Optional, with safe defaults: AI targets (`AdventureAi`), validator checks, info sheet, editor inspector
  fields (an `InspectorSection<T>` in `InspectorSections`). Kind-specific rules sit in `*Rules` classes (`LootRules`, `BuffRules`, `CreatureBankRules`, ...).
- **Rewards (ADR-068)**: definition `VisitScope` + instance `VisitLog` (`MapObject.Visits`, game state only), checked and
  recorded by `ObjectInteractionContext.TryCollect`. Temporary `HeroStatus`es live on the hero, granted/expired via
  `ProgressionService`. Random rewards use `WeightedRoll.Pick`.

### Terrain, roads, teams, knowledge
- Terrain rules are data (`GameState.Terrains`, `TerrainCatalog`, ADR-045). **All movement cost goes through
  `MovementRules.StepCost`** (road → road, else terrain being left, ×1.41 diagonal; `ArmyTown` native-terrain rule);
  `PathFinder` uses it too, so a planned route costs what walking it costs.
- Roads and rivers are extra per-tile layers on `AdventureMap`. The ground's height is a per-corner layer beside them (ADR-182,
  `ReliefShaper`, `GroundHeightAt`), for looks only: no rule reads it. The 3D view shows it as the smooth surface `ReliefSurface` lays
  through the corners (ADR-209); views put things on it with `MapSpace.ToGround` (and `OnSlope` for flat markers) and pick tiles with
  `MapSpace.PickTile(camera, point, map, level)`, which follows the slopes. The editor shapes it through `MapEditorSession.Heights`
  (`HeightEditor`); terrain painting (Auto height) and object placement shape it inside their own commands. Every change publishes
  `GroundHeightsChanged(level, area)` and the views re-place what stands on tiles touching `ReliefSurface.Reach(area)`.
- Teams: `Player.Team`; `AreAllies` / `AreEnemies` drive encounters (own → exchange, enemy → battle, ally → blocked).
- `FogOfWar` and `ObjectMemory` (last known object state per player) are maintained by `VisibilityService`: from movement and
  ownership events, and through `IFogRevealer.RevealArea` for the rules that reveal an area on purpose (observatories, Magi windows, the
  Ruins, cartographers, View Terrain, map events), which call it before publishing `SurroundingsRevealed` / `AreaRevealed`. **Player
  views draw objects from memory, never live state.** Under refog, `FogOfWar.UpdateSight` remembers the observers it last worked from
  and works out again only the circles of those that moved, came, went or changed their radius; `GameState.Flaggables` keeps the
  objects that can fly a flag.

### Random map generation (ADR-064)
- `MapGenerator.Generate` runs static stages in order on a `GeneratedMap`: `ZoneLayouter` → `TerrainPainter` →
  `SceneryPlacer` → `ConnectionPlacer` (+ `GuardPicker`) → `SettlementPlacer` → `RoadPlacer` → `RiverPlacer` →
  `TreasurePlacer`. Each stage draws from its own `SeedStreams` stream: same inputs → same map on every platform.
- The stages take every table (objects, terrain, creatures, artifacts) from the `ContentCatalogs` the generator was given,
  which `GeneratedMap.Content` carries; none uses a built-in table directly. The scenario's game gets the same record.
- `ScenarioAssembler.Build` / `MapGenerator.Build` produce the scenario `GameState` (`PlayerSetup`); Core.Generation
  cannot see `MapDocument`. The editor uses `EditorMapGenerator.Generate`; `MapConverter.ToDocument` saves a map.
- Templates: model `MapTemplate` (Core.Generation), file `*.ewtemplate.json` via `TemplateSerializer` /
  `BuiltInTemplates` (Core.Data), `TemplateValidator` (errors, fairness), `MapFairness`. `TemplateEditorSession`
  edits a template, one undo step a change (an `UndoHistory` of whole templates, `IUndoSession` like the map's session), and publishes
  `TemplateChanged`, `UndoHistoryChanged` and `UnsavedWorkChanged`. The graph's geometry (snap, align, spread) is `TemplateGraph` and
  its clipboard `TemplateFragment`, the mirror tools' rules `TemplateMirrors` and the zone presets `ZonePresets`
  (Core.Generation), the user's preset files `ZonePresetFolder` (Core.Data) behind the app's `IZonePresetFiles`; the preview's
  `ZonePicture` and Generate many's `TemplateBatch` (Core.Generation), run off the main thread by the panel; `TemplateGraphView` (Godot.UI) draws and edits through the session. Its files are `TemplateFileService` (Godot.UI) over the app's
  `ITemplateFiles`; both editors' autosaves are an `AutosaveFolder` (Core.Data).
- Generation touches no shared state and must stay fast (target < 5 s for 256×256×2).

### Map editor
`MapEditorSession` (`Editing/`) wraps a scenario `GameState` that never starts. Every edit is an `IEditorCommand`
(execute/undo) in `UndoHistory` (its steps' descriptions oldest first, and the position: how many are applied; `UndoTo(step)` jumps
there by single undos or redos; `UndoHistoryChanged` follows every record, undo, redo and jump; the history remembers the step the front-end saved at, `MarkSaved`,
so `IsDirty` is exact and `UnsavedWorkChanged` tells when it flips; the editor's `FileService` (Godot.UI) owns the map's file, the
dialogs, the recent files (`RecentFiles`, kept in `EditorSettingsFile`), the questions over unsaved work (`QuestionBox`), the autosave
timer and the recovery offer, and reads and writes through the app's `IMapFiles` (the map format, and `MapAutosaves` in Core.Data:
the autosave folder and its session marker); `CommandPalette` searches the `CommandRegistry` with `CommandSearch`; the user's
`EditorPreferences` are edited in `PreferencesWindow`, kept by `PreferencesStore` and applied by the app, and the user's shortcuts
in its `ShortcutEditor` (the rules in `ShortcutBindings`, Core.Domain.Editing; kept by `ShortcutStore`, put over the defaults by
`CommandRegistry.UseShortcuts`); the F1 help's `HelpWindow` shows the `HelpBook` (Core.Domain.Editing) that `EditorHelp` makes from the
design documents built into Godot.UI, at the page for the hovered control; `EditorWorkspace` holds the
Map and Template pages) and publishes the same kind of events as the game, so editor views are ordinary
subscribers. The session is a facade: edits live in per-area editors (`Terrain`, `Objects`, `Contents`, `Monsters`, `Towns`,
`Heroes`, `MapEvents`, `Players`, `Map`, `Selecting`) over one internal `EditorContext` (scenario, catalog, bus, history, open stroke,
selection). The session owns the `MapSelection` (tiles, objects, heroes, event names); only `SelectionEditor` changes it, publishing
`SelectionChanged`, and it is no undo step. `MapMover` moves objects and heroes together under placement's rules (`ObjectEditor.Move`,
`HeroEditor.Move`, `SelectionEditor.Move`; `MapObjectMoved`, `HeroRepositioned`). The editor's `SelectionView` draws the selection,
the marquee, the ghost of a move or a paste, and the outline of the brush. `FragmentEditor` (`session.Fragments`) copies the selection
into a `MapFragment` (tiles, corner heights, objects) and pastes fragments with new ids and remapped links; objects are copied
through `IMapObjectCopier`, which the data layer implements with the map format's own codecs (`MapObjectCopier`), so a copy has what
a saved object has. `TerrainEditor` paints sets of tiles (`Brush`: size, shape, line) and flood-fills; `EraseEditor`
(`session.Erasing`) erases by kind. An editor operation made of several edits runs through `EditorContext.AsOneStep`.
`MapEditorSession.Resize`, `Crop` and `Shift` (ADR-251) give the map a new frame in place: `MapResizer` removes what would be outside
through the other editors, then `GameState.Reframe` makes the same `AdventureMap` object a copy of the reframed one
(`AdventureMap.Reframed`, `TakeFrom`) and moves every object and hero; `CheckResize` / `CheckCrop` / `CheckShift` say beforehand what
would be removed. A map's `Width` and `Height` therefore change only there, before a game starts; on `MapResized` the editor app
builds the map's views again. Stamps are `MapFragment`s in files of their own versioned format (`StampDocument`, `StampSerializer`,
`StampFolder` in Core.Data; the objects are written as the map format writes them).
Scenery has an `Orientation` (ADR-252: mirrored, then 0 to 3 quarter turns clockwise). `MapObject.Footprint` is the shape as the
object lies (`Footprint.Oriented`), and the cell index, the fog's memory (`KnownObject`) and the map view read that, not the
definition's footprint; `ObjectEditor.Rotate` / `Mirror` (`SceneryTurner`) change it, `SelectionEditor.Rotate` / `Mirror` for the
selected scenery. `ScatterEditor` (Core.Generation, beside `EditorMapGenerator`) scatters scenery over an area through the session,
with `SceneryPlacer`'s choice of pieces; it draws from the scenario's random numbers inside
`MapEditorSession.AsOneStep(description, random => …)`, whose undo also puts the generator back.
The object browser's data is `BrowserCatalog` (ADR-253; Core.Domain.Editing), built from the object, creature and artifact catalogs:
`BrowserEntry`s with a `BrowserCategory` (`CategoryOf(MapObjectKind)`, which a test holds against every kind), a group, search words,
terrains and a one-line rule; `Search` ranks them. `EntryEditor` (`session.Placing`) places an entry through the other editors and
`Check` gives the cells for the ghost. The panel is `ObjectBrowser` (Godot.UI); it and the Object tool share a `BrowserChoice`, and
`BrowserLists` holds the rules of the Favourites and Recent lists the editor's settings file keeps.
An object's stand-in model is `PlaceholderModel` (Godot.Shared, ADR-254): a list of `ModelPart`s that `MapObjectsView` draws and
`ThumbnailRenderer` (Godot.UI) renders for the browser in hidden `SubViewport`s, kept as PNG files under the parts' hash.
The inspector's forms put their rows into `InspectorPart`s through `InspectorForm.In(part)` (ADR-254); its jump links are
`MapLinks.Of(scenario, thing)` (Core.Domain.Editing), computed from the scenario each time and never stored.
Multi-edit (ADR-255): `ObjectProperty<TObject, TValue>` is one field of one object type, read from an object and written through
the session's editors; `ObjectProperties` lists the fields edited over several objects; `MapEditorSession.EditAll` writes to a
list of objects as one undo step, all or none, and `ObjectsOfOneKind` gives the objects the inspector may edit together. The
inspector's sections take a list of objects (`IInspectorSection.Show`), and `InspectorForm` binds controls to properties with "—"
for values that differ. `PicturePicker` (Godot.UI) is the searchable picture grid that replaces the drop-downs for creatures,
heroes, artifacts, spells and skills; `PickerItems` builds its lists.
Map settings (ADR-256): `MapSettingsWindow` (Godot.UI, Editor.Shell) is an overlay with one tab per settings panel
(`GeneralSettingsPanel`, `PlayersPanel`, `ConditionsPanel`, `BansPanel`, `WeekEventsPanel`, `RumoursPanel`, `TextsPanel`); each panel
edits through the session, so the window adds no rule of its own.
Map content (ADR-257): rumours are `ScenarioRules.Rumours` (`TavernRules.Rumour` picks the week's, the rumour table's `Rumours.OfWeek` on a
map without them, ADR-296), a player's factions `Player.Factions`
(`PlayerFactions`: what the new-game screen offers and what a random choice draws among), an object's name `MapObject.Name` (a town's is
its own name; `ObjectNames` finds an object or a hero by id or name), a hero's `Biography` and `Portrait`. The map format reads an object's
name through `MapObject.WithName`, since `Core.Data` sees only the domain's public members.
Map texts (ADR-258): `MapTexts.List` lists every text of a map with an id; `MapTextEditor.Set` (the session's `Texts`) sets one by
calling the editor that owns it, so the Texts tab adds no rule of its own. `MapTextsTests` checks every string the map file writes.
New maps (ADR-259): the wizard (`NewMapWizard`, Godot.UI) collects a `NewMapSettings` (Core.Domain) for `BlankScenario.Create`, or presets
the `GeneratePanel`; the app opens the result through `FileService.OpenNew`. `SampleMaps` lists the shipped maps the empty state offers.
The editor's camera (ADR-260) is `EditorCamera` (Godot.Shared/Cameras), not the game's `StrategyCamera`: it frames an area into the map
view's rectangle (`Frame`); the selection's cells come from `MapSelection.Cells` (Core.Domain); bookmarks are `CameraBookmarks` (Godot.UI).
The editor's minimap (ADR-261) is the HUD's `MinimapWidget` inside `MinimapPanel` (Godot.UI), a footer of the right region; Go to is
`GoToBox` over `MapGoTo` (Core.Domain), whose results are the inspector's `MapLink`s.
The editor's layers (ADR-262) are `MapLayers` in the session (Core.Domain), which the selection and the eraser consult; the shared map
views leave out what `MapViewContext.Hidden` names, and `LayersPanel` (Godot.UI) is the tab that sets them.
The map analysis (ADR-263) is `MapAnalysis` (Core.Domain/Analysis): reach in days through `PathFinder.CostsFor` for a scout that is not
on the map, guard strength, treasure value and the fairness numbers per start; the generator's `MapFairness` takes its guard power
from it.
The editor's overlays (ADR-264) are computed by `MapOverlays` (Core.Domain/Analysis) as tinted tiles or lines with a legend; a generated
map's zones come from `GeneratedZones` (Core.Generation), which lays the zones out again from the map's description. `OverlayView`
(Godot.Shared) draws one as a multimesh, and `OverlaysPanel` (Godot.UI) chooses it, computes it again after edits and shows the legend.
The fairness report (ADR-266) is laid out by `MapReport` (Core.Domain/Analysis: the table and the Markdown with the problems);
`FairnessPanel` (Godot.UI) shows the table, and `MapPicture` (Godot.UI) renders the map top-down in a viewport that shares the map's world
for File > Export.
A play-test (ADR-267) is `PlayTestArguments` (Core.Domain/Game: the game's launch arguments and the setup) with `PlayTestRules` in
`GameOptions.PlayTest`, which `GameSession.Start` and `GameDirector` apply; its cheats are commands. The editor's Test builds the game's
command with `PlayTestLaunch` (Core.Domain/Editing) and starts it from the app; `PlayTestWindow` (Godot.UI) chooses the options.
Single-value edits are `PropertyEdit<T>` commands; an edit to the value already there records no undo step and publishes
nothing. The three tile layers paint through `PaintLayer<TValue, TEvent>`. `BeginStroke`/`EndStroke` merge a drag into
one undo step. `MapValidator` reports what single edits cannot prevent; an issue with a clear answer carries a `MapFix` (ADR-265), data
naming the objects it changes, which `MapEditorSession.Fix` runs on the live map as one undo step. `MapConnectivity` tells where a hero can walk on a fresh
map (through guards and pickups, via gates); the Sim's generated-map check reports the rival towns a player cannot reach.

### Content tables (ADR-071)
`src/Core/data/*.csv` (creatures, heroes, buildings, artifacts, artifact sets, spells, riddles, scenery) are embedded
into Core.Domain; `data/templates` into Core.Data. Apps use the embedded copy (`GameContent`), or designer files via
`--creatures=`-style flags. CSV for flat tables, JSON for nested data. `DataTableSchemaTests` checks each table.

## 4. Godot libraries: constraints
- Tier 2 projects use `Godot.NET.Sdk` 4.7.2 so source generators and node overrides work.
- Godot attaches scripts by `res://` path, and `[GlobalClass]` types in referenced assemblies are not registered
  (ADR-043). So **library nodes are created from code** (`new HeroView()`) and configured with `Bind(...)`; classes that
  must be attachable in the editor (`[GlobalClass]`, `[Export]`) live in the app project.
- App `.csproj` files state `<TargetFramework>net10.0</TargetFramework>` explicitly (Godot cannot see
  `Directory.Build.props` and would insert `net8.0`).
- View composition (`Godot.Shared/Map`): `MapViewContext` (viewing player, shown level; raises a C# event) →
  `AdventureWorldView` composes per level `AdventureMapView` (the ground: meshes of 32 × 32 tiles, ADR-207, each tile 2 × 2 squares on the smooth surface, ADR-209, splat-shaded by `TerrainSplat` /
  `shaders/terrain_splat.gdshader`, the tile grid as its overlay, `MapViewContext.ShowGrid`; ADR-183, height blend ADR-200, adventure-only pictures and normal / roughness / occlusion maps ADR-240), `MapObjectsView` (viewer's `ObjectMemory`, or live objects
  without a viewer; object cells as one `MultiMesh` per box size, captions nearby or under the pointer, `MapViewContext.Pointed`, ADR-208), `FogView`, `HeroView`s, `PathPreview`. Game and editor both use it. Panels live in `Godot.UI`
  (`Hud`, `Setup`, `Editor`, `Battle`, `Debugging`). The game's adventure HUD and its screens are one `AdventureHud`; the map's and the
  battle scene's clicks and keys are `AdventureInputController` and `BattleInputController` (deployment drags: `DeploymentDragController`);
  the screens before a game share a `SetupScreenHost`. Their texts come from core (`MoveOutcomeText`, `AdventureSpellPrompt` in
  Core.Domain; `SaveSlotText`, `RankingText` in Core.Data, which the app maps onto the panels' view records).
- Assets cannot live in a class library: `EternalWars.Godot.Assets` is mounted into each app as `res://shared/` by
  `tools/link-shared-assets.ps1` (junction, git-ignored).
- **Spacing** (ADR-163): every app calls `UiTheme.Apply()` first, which merges the shared gaps and margins (`UiSpacing`) and the look into Godot's default theme; the map editor calls `Apply(compact: true)` for a smaller font and tighter spacing (ADR-250) (a
  theme on the root window would not reach controls under a `CanvasLayer`). Panels take the theme's spacing instead of their own numbers:
  `AddHeading` for a section, `WithCellGap` for a row of slots, `Screens.UseOpaqueBackground` / `CenterAsScreen` for a screen over the map,
  `Screens.UseMenuPanel` for one over the main menu's picture; code that places controls itself uses `UiSpacing.ScreenEdge`.
- **Look** (ADR-191, ADR-223): `UiTheme` holds the colours (panels, frames, buttons, text tints such as `TextCaution` or `IssueError`) and
  the panels' widths and slot sizes (`ScreenTextWidth`, `TownSlotSize`, `InspectorTextWidth`, ...); panels name a token instead of a
  literal. Drawing code (the battle board, the template graph's zones and connections) keeps its own palette. The editor's high contrast
  (ADR-277) switches a running UI through `UiTheme.Apply`: editor panels take their frames and quiet text from the theme's variations
  (`RegionPanel`, `DialogPanel`, `CardPanel`, `TilePanel`, `PagePanel`, `QuietLabel`), never from stylebox or colour overrides, so they
  follow the switch.

## 5. Threading model
Single-threaded on Godot's main thread; `EventBus` is deliberately not thread-safe. Heavy work may later run on a
background thread against a **copy** of the state, returning commands to apply on the main thread.

## 6. Solution layout
```
global.json                     .NET SDK 10, Godot.NET.Sdk 4.7.2, MTP test runner
Directory.Build.props/.targets  shared settings + architecture guard
src/Core/data/                  content tables and templates, embedded at build time
src/Core/EternalWars.Core.*/    Tier 1; namespaces follow folders
src/Godot/EternalWars.Godot.*/  Tier 2
src/Apps/EternalWars.*/         Tier 3: project.godot + Main.tscn/Main.cs
tests/EternalWars.Core.Tests/   TestWorld, TestBattle, SessionCommands helpers
tools/                          link-shared-assets.ps1, smoke-godot.ps1, smoke/ maps, EternalWars.Tools.Sim
.github/workflows/ci.yml        build + test + sim selftest; generated-maps job
```
