# Tournament mode: report

How the **tournament** mode was built (owner, 2026-11-27; ADR-366). Two players build up apart from each other, each in a zone
of their own against the map's neutrals, for a time chosen when the game is created. When the time is up their heroes meet in one battle
with no retreat and no surrender, and its winner wins the game. It is played against the computer, hot-seat and over the LAN, where both
players play every day at once. The owner answered TRN-1 to TRN-9 on 2026-11-27: the proposals, with the lengths up to 3 months, and
battles at the same time on every simultaneous day, not only in tournaments. The rules as built are in
[victory-and-modes.md](../design/victory-and-modes.md#game-modes); this file tells what each step built and what the checks found.

## Guide

### The owner's request

- Two players, at most one hero each.
- The length is set when the game is created, such as 2 weeks or 1 month.
- Each player plays against the map (PvE) in their own zone and cannot reach the other.
- When the time is up, the two heroes fight one battle with no retreat and no surrender. The winner wins the game.
- Over the LAN every day is played at once.
- It is offered against the computer, hot-seat and over the LAN.

### What it reuses

The tournament is close to the [arena](../design/victory-and-modes.md#game-modes) (ADR-158). It is a preparation phase followed by a
battle that decides the game. The tournament stretches that phase from one day to weeks of ordinary play.

| Existing piece | ADR | What the tournament takes from it |
|---|---|---|
| Arena battle | ADR-158 | The battle that starts by itself, the champion, no experience, draw handling, the result in `GameState.DecidingBattle` and the end of the game |
| Arena in the game app | ADR-159 | The result screen with "Play again" and "Save replay", not ranked |
| Arena AI | ADR-160 | The last day's spending on the battle's army (`TownManager.Arena`) |
| Arena over the LAN | ADR-199 | The battle fought live, and the host's rematch |
| Start zones | ADR-197 | Each player's zone, written by the generator and painted in the editor |
| Simultaneous days | ADR-197, ADR-217 | `SimultaneousDayCoordinator`; the tournament runs it every day instead of only in the first week, and every simultaneous day gets battles and questions at the same time |
| One turn timer | ADR-198 | The optional turn timer, one clock for both players |
| Hero limit | ADR-068, ADR-134 | `ScenarioRules.MaxHeroes`, fixed at 1 |
| Mirrored templates | ADR-117 | The two zones get the same content, because mirrored zones share their rolls |

## Plan

Each step lands as one or more changes with a green test suite (CLAUDE.md). The core comes first, so the AI and the sim can play
tournaments before any screen exists.

### 0. Record the decisions (done)
- The owner answered TRN-1 to TRN-9 on 2026-11-27; ADR-366 holds the answers.

### 1. Core rules (engine-free, with tests; done)
- **Switch and option** (done):
  - `ScenarioRules.Tournament` (`rules.tournament`), and the map format bumped with a sample in `FormatSamples`.
  - `GameOptions.TournamentDays`, carried in saves and replays.
  - The checks:
    - a tournament map has exactly two players, no teams and `MaxHeroes` = 1;
    - an arena and a tournament exclude each other;
    - the length must be 1 to 14 days, or 3 to 12 whole weeks.
- **Game flow** (done):
  - `TournamentRules.BattleDay`, and `GameFlowService` ignoring the conditions.
  - `RulesCheck` (fewer than two teams, nobody can win), `GracePeriodCheck`, `MapKinds`, `ScoreRules` (never ranked) and the editor's
    `ConditionsPanel` treat the tournament as the arena. A map event's end of the game counts for nothing in either.
  - `TurnService` starting the battle instead of a turn on day N + 1. This shares the arena's battle (the champion, the battle, the result),
    generalised to "a deciding battle" (`DecidingBattles`, `DecidingBattleService`, `GameState.DecidingBattle`) with the arena and the
    tournament as its two triggers.
- **The battle** (done):
  - `TournamentRules.Plan` chooses it: a coin toss from the game's random numbers makes one player the attacker (the summary says so), and
    each side's champion is its one hero, on the map or else in a town's garrison, which it leads as its army (ADR-112).
  - No retreat or surrender for either side: `BattleState.NoLeaving` makes `SurrenderRules` refuse both commands, and the computer never
    leaves.
  - The battlefield is neutral ground (`TournamentRules.Battlefield`): a terrain no town is native to (sand, beach or dirt), drawn from the
    game's random numbers; the obstacles come from the battle's own numbers, as in every battle. Never a siege, and a town the defender
    stands in lends it nothing (no Brotherhood, no siege bonuses): the hero's army only.
  - The endings: a side without a hero loses without a battle, and so does a champion without an army; when neither side can fight it is a
    draw. The game's end message gives the reason.
- **Isolation** (done):
  - `TournamentRules.Reaches`: a player never sees, reveals or aims into the other player's start zone; tiles in no zone (the walls) stay in
    reach. `FogOfWar.Reach` clips every reveal and the sight (scouting, revealing objects, View Terrain), and the puzzle map hides those tiles.
  - The adventure spells: none is aimed outside the zone, Town Portal goes only to towns inside it, and Distant Vision, Detect Magic and
    Grave Whisper show only the zone.
  - `TavernRules.Taken`: each player's tavern pools (towns and map taverns in its zone) leave out only its own heroes.
  - Sending resources is refused.
  - `GrailState.Zones`: a Grail buried in each start zone that has an obelisk, with that zone's obelisks for its puzzle (`GrailRules.SiteOf`,
    `ObelisksOf`); the map author's Grail does not count.
  - The artifact rolls are unique per zone (`TournamentRules.ZoneOf`; a player without a zone shares the map's pool): the pickups rolled at the start and the relics drawn as rewards (`GameState.DrawnRelicsOf`).
    That both zones get the same content is the mirrored templates' (step 2).
- **Tests** (`TournamentTests`, using `TestWorld` ASCII maps; done):
  - the battle on day N + 1;
  - no retreat or surrender;
  - no hero, no army, a draw;
  - sight at the zone's edge with Scouting;
  - Dimension Door and Town Portal refused outside the zone;
  - a hero rehired after a loss, the limit of 1;
  - conditions ignored;
  - the map format round trip, with the previous version still loading.

### 2. Random maps and validation (done)
- **Template support** (done):
  - `MapTemplate.Tournament` (template format v10).
  - A player's part is the zones the connections reach from its start zone (`MapTemplate.Reach`). The validator wants two players who are
    not allies, every zone in one part, and no connection, gate or portal between the parts; it names the way it finds. It checks
    connectivity per part, not across the whole map, and warns that a hero limit other than 1 is ignored.
  - The generator writes `rules.tournament`, `MaxHeroes` = 1 and each player's whole part as its start zone. `MapFairness` checks the
    opposite of its usual connectivity: neither start town may walk to the other or into the other's part. A border guard's colour gets a
    Keymaster's Tent in each part. The generator refuses a setup of other than two players, or of allies; the template batch (`sim maps`)
    skips its rivals-reach-each-other check, and the start zones' touch warning is off in a tournament. Existing templates give the same
    maps, so the generator's version stays.
- **Shipped templates** (done): the mirrored Twin Vales Small (72), Twin Vales (96) and Twin Vales Large (128), guards from weak at the
  start to strong at the far end. `sim maps` checks them in CI as it does the others; for a tournament it skips the rivals-reach-each-other
  check, and the map validator's errors fail it.
- **Map validator (hand-made maps)** (done):
  - **Errors:**
    - a tournament map without a start zone for each player;
    - a passable route, a gate, a portal or a teleporter between the zones, either way, also from a player's heroes or towns standing
      outside its zone (guards, garrisons and entrances stop nobody);
    - an arena switch on as well;
    - a hero limit other than 1.
  - **Warning:** a zone much richer than the other: more towns, more mines of a resource, or treasure further apart than the generator
    lets alike starts be (`MapAnalysis.AlikePercent`, 15 %, at least 1500 gold).

### 3. Simultaneous days: the whole tournament, and battles at the same time (Core and Network; done)
- **Every day at once** (done): `SimultaneousDayCoordinator` opens every day of a tournament at once on the LAN, not only the first
  week's, with a start zone each; a conflict still ends the simultaneous days for good. The final battle's day is no simultaneous day:
  the battle starts before any turn opens. The LAN lobby has no switch for a tournament (owner, 2026-11-27): it always sets the
  simultaneous option, says "Every day before the battle is played at once" and wants one colour played on the host.
- **Battles and questions per player** (done), on every simultaneous day (a tournament's, an ordinary LAN game's first week, an arena's
  preparation day):
  - Each open turn has a `TurnSlot` of its own: its battle, its question and the map events' runs. `GameState.Battle`, `PendingDecision`
    and `MapEventState`'s runs are the slot of the turn a command runs in (a battle command's: the turn whose battle its player fights), so
    the battle and decision services are unchanged; on a day played turn by turn there is one, shared, slot.
  - "Wait: a battle is being fought." is gone. A battle between two people's armies is a conflict, and so is one against a hero, monster or
    town another person is fighting at that moment, allies' too. A conflict has the LAN host play the day again from its start, so the
    battles open then go with it; played on without the host, a slot still open waits for its person's turn, and of two battles against one
    foe the later person's goes unfought.
  - The command log stays the host's order, so replays stay exact; digest version 3 adds each person's slot and gives the day's current
    player as the host's turn.
  - `GameView.Of` sends each client its own battle and question (`BattleOf`, `DecisionFor`); the host's views hear only its own turn's
    battle and question events (`GameSession.InShownTurn`, `BattleView3D` by `GameState.SlotPlayer`).
  - The turn timer ends each turn but one held by a battle or a question without a default; the replay viewer follows one battle at a
    time (`GameSession.Watch`).
- **Tests** (done): `SimultaneousBattleTests` (two battles at once, a question beside a battle, map event questions at once, the same-foe
  conflict played on turn by turn, a battle between two people, the timer, the computer taking over in a battle, a save loaded on one
  computer, the replay viewer, the shown turn, the older digest);
  `LanSimultaneousBattleTests` (two battles on two computers, each sent its own; a conflict during a battle); `LanTournamentTests` (a
  tournament day past the first week with both fighting neutrals, the record replayed, the final battle live).

### 4. The computer (done)
- **Building phase** (done): the ordinary adventure AI plays its zone. It never looks for an enemy:
  - a strong hero puts no share of its army on exploring, as it does elsewhere while it knows no enemy town (`TargetValuer.ExploreWorth`);
  - ground in the other zone, which it never sees, is no frontier to explore and opens no way (`AiContext.Unknown`).
- **Planning for the deadline** (done): `AiContext.DaysLeft` counts the building days left, and three weights tune the rest
  (`CarefulDays` 3, `LeashShare` 0.75, `ArmingDays` 5).
  - **Its one hero:**
    - In the last 3 days it fights only for the arena's small losses (`ArenaLoss`, 15 % of its army).
    - Before the last day it ends each day no farther from its home town than three quarters of what the days left can walk back. The
      home town is the own town with the most dwellings. A hero that is farther walks straight home (`TargetValuer.Homecoming`).
  - **Growth against army:**
    - A mine or a deposit is worth the days of income left before the battle, at most eight as elsewhere.
    - A provider's coming week counts only when a week still begins before the battle, and so does a map dwelling's flag.
    - A town builds a level that only adds income when the days left pay its price back. It builds a growth building or a caravan only
      while a week is still to come, and saves only for what the income left can reach.
    - In the last 5 days, once no week begins before the battle, the towns spend as on an arena's day 1. They buy what waits, then the
      dwellings and upgrades with the most army per gold, counting the income still to come, still one building a day.
  - **The last day** is the arena's day 1 (ADR-160), with the day's own movement:
    - Before anything else the hero walks to every own town it reaches today, buys what waits and takes the whole garrison. When no town
      is within walking reach, it casts Town Portal home.
    - Then it takes only what helps today.
    - At the day's end it visits any town it can still reach.
- **Tuning** (done): `sim game --length L` sets a tournament's length. Each run prints the champions' levels and army values at the
  battle, and the runs together their spread. In six AI-against-AI runs a template, the median champion's army value (Σ count × creature
  value) at the battle:

  | Template | 7 days | 14 days | 28 days |
  |---|---|---|---|
  | Twin Vales Small | 6 103 | 17 181 | 42 235 |
  | Twin Vales | 6 417 | 17 045 | 35 376 |
  | Twin Vales Large | 4 351 | 16 535 | 38 764 |

  - **What it brings:** the towns' weeks of growth, with no refused command.
  - **Not yet measured:** whether that is in the range a person reaches. No person's tournament is recorded to compare with (step 7).

### 5. The game app (done)
- **New-game screen** (done):
  - A **Tournaments** tab (ADR-201's tabs) lists the tournament templates and the hand-made tournament maps (`MapKind.Tournament`).
  - Under the map's name, a line says what a tournament is. The template's hero limit row is hidden: it is 1.
  - The **length row** is a stepper, "Shorter", "14 days (2 weeks)", "Longer". It moves a day a step up to 14 days, then a week a step up
    to 12 weeks (`TournamentRules.Step`, `LengthText`), and sets `GameOptions.TournamentDays`.
  - **The two players:** Human or Computer here, and Here, LAN player or Computer in the lobby. Every player of a hand-made tournament
    map may be a person, as on an arena map.
  - A setup `TournamentRules.WhyNot` refuses, such as two allies, is named on the status line instead of starting.
  - The LAN lobby's settings name the length, and it notes that every day is played at once (done in step 3).
- **Adventure HUD** (done):
  - **The calendar** follows the date with the countdown: "M1 W2 D3 · Battle in 5 days", and "Last day before the battle" on the last
    day (`TournamentRules.Countdown`).
  - **The last day's notice:** each person's turn on the last day opens with it. It says that tomorrow the hero fights with its own army
    only, so the troops must be gathered into it today.
  - **The hero card** shows "Hero 1/1", and its hover text says why (`HeroCard.Limit`). A town's tavern, a map tavern and a prison
    refuse a second hero with the tournament's reason (`TavernRules.LimitRefusal`).
  - **No trading or other-player panels:** the town's "Send to an ally" is never shown. The Thieves' Guild and the Den of Thieves say only
    that the thieves have nothing to tell (`ThievesGuild.Closed`), and the tavern's guild button is greyed out with that reason. The
    taverns' rumours name no ranks and only treasures in the player's own reach.
  - **The one hero** counts a garrison's too: a hero in a town's garrison still stops a second being hired.
  - **Checks:** `TournamentScreenTests`, and the smoke `game-tournament-setup` (the Tournaments tab, the stepper, a one-day tournament
    started on the last day's notice).
- **Hot-seat hand-over** (done): each person's turn opens behind a screen that covers the map, the HUD and the timer, so neither sees the
  other's zone. It names whose turn it is, the day with the countdown, and asks the others to look away; "Start Blue's turn", Enter or
  Space shows that person's zone, and the last day's notice after it. The map stays drawn for the last person under it, and the turn
  timer stands still meanwhile (`GameDirector.HandOver`, `TournamentRules.HandOver`, `HandOverPanel`). A loaded hot-seat tournament opens
  behind it too. Checks: `GameDirectorTests`, and the smoke `game-tournament-hot-seat`.
- **Battle** (done):
  - The final battle opens by itself at the start of the battle's day, both armies shown on the neutral battlefield.
  - The retreat and surrender buttons are greyed out. Their hover text on the status bar, and the first round's prompt, say "No retreat or
    surrender in the tournament's battle." (`SurrenderRules.TournamentRefusal`).
- **Result screen** (done): the arena's, with the tournament's wording: the winner of the tournament battle, who won the coin toss and
  attacked whom, the day "after 14 days (2 weeks) of building up", "Tournaments are not ranked.", "Save replay" and "Play again" (the same
  map and length with a new seed; on the LAN the host's).
- **Smoke scenario `game-tournament`** (done): a one-day tournament against the computer, from the Tournaments tab to the battle on day 2,
  quick combat, the result screen and "Play again".

### 6. The map editor (done)
- **The "Tournament" switch** on the map settings window's General tab (`MapPropertiesEditor.SetTournament`, `TournamentRules.Switched`):
  - Turned on, in one undo step, it sets the hero limit to 1 and greys it out, clears the players' own hero limits, turns the arena off
    and hides its switch, and the Victory and loss tab greys out. Turned off, the hero limit stays at 1 until changed. A loaded map with
    both switches on still shows the arena's.
  - It is refused, as any hero limit is, while a player has more than one hero on the map.
- **Problems** lists step 2's checks. A player without a start zone has a fix, "Paint Red's start zone over the ground it reaches"
  (`StartZoneFix`, `TournamentCheck.MissingZone`):
  - It paints the tiles the player's heroes and town entrances walk to, over ground in no zone, and the footprint of each object whose
    entrance is among them.
  - There is no fix when that ground is not clearly the player's: one of its heroes or town entrances stands in another player's zone,
    another player's hero or town is on the ground, or a step leads from it into another player's zone.
- **The template inspector's "Tournament" row** (`TemplateEditorSession.WithTournament`): ticked, the hero limit becomes 1 and its row goes.
  The issues list shows the per-player checks: two players, no allies, the two parts kept apart.
- **Checks:** `TournamentEditorTests`, and the smoke `editor-tournament` (the switch, the greyed Victory and loss tab, Problems' missing
  start zones, the template's Tournament row and its joined-zones error).

### 7. Check (done)
- **Against the computer, seen in Godot** (smokes `game-tournament`, one day, and `game-tournament-week`, seven days): the countdown, the
  last day's notice, the computer building up while the person ends each day, the battle on day 8 against its grown army with Retreat and
  Surrender greyed out, and the result screen.
  - **Found:** the person watched the computer's battles in the computer's zone (a siege of a neutral town, its whole army shown). Now a
    tournament battle no person fights is shown on no screen (`TournamentRules.Shown`, `BattleView3D`).
- **Hot-seat, seen in Godot** (smoke `game-tournament-hot-seat`, two days): each turn shows only its player's zone and the countdown, each
  person's last day opens with the notice, the two people's battle on day 3 (its deployment step, then quick combat), the result screen.
  - **Found:** the screen turned to the next person's zone as soon as a turn ended. The hand-over screen (step 5) now opens each turn,
    seen in the same smoke: at the game's start, between the two people's turns and before each last day's notice.
- **Over the LAN, seen in Godot**: two Godot processes on one computer (`--host`, `--join=127.0.0.1`), Twin Vales Small with seed 10, two
  days. Both played day 1 at once. Each was asked by its own skeletons at the same moment, and each fought them on its own computer, the
  two battles at the same time. Each ended its days, the last day's notice opened each person's day 2, and the final battle opened on both
  computers. There each side's moves were seen live on the other ("Red plays on another computer: waiting for their move").
  - **Found:**
    - The final battle did not open on the client. Its player had never seen the other's champion, and a client drops an event that
      names a hero it does not hold. Now a player's view holds the heroes of the battle it fights (`PlayerViewFeed`;
      `LanTournamentTests`, whichever side ends the last day last). A champion from its town's garrison comes out onto the map when the
      battle starts, so the other player is never sent its town (`LanTournamentTests`).
    - Once its day was over, the host said "Red fight a battle on another computer". In a tournament that panel now says nothing, and
      elsewhere it says "fights" for one player (`DecisionPanel`).
    - Nothing told a player that the other had ended the day (TRN-6). The host now says it in the chat, "Red has ended the day.", on
      every day played at once while others still play it (`GameHost`).
  - **The final battle fought by hand to its end** (seed 10, two days without the neutral fights): Blue only defended, while Red's
    hunters shot and its satyrs walked over and struck, until Red won in round 17. Each move showed on both computers, then the battle's
    result (Victory for Red, Defeat for Blue, the same losses) and the tournament's result screen on each: who won the coin toss and
    attacked whom, after 2 days of building up, not ranked, with "Play again" on the host and "The host may start the game again" on the
    client.
- **AI runs** (`sim game --template T --length L --runs N`; the AI plays both seats):

  | Template | 7 days (10 runs) | 14 days (20 runs) | 28 days (10 runs) |
  |---|---|---|---|
  | Twin Vales Small | first seat won 6; median army 4 913 | 12; 14 572 | 4; 44 946 |
  | Twin Vales | 4; 4 206 | 12; 16 402 | 5; 44 283 |
  | Twin Vales Large | 5; 5 653 | 7; 16 754 | 5; 41 295 |

  - All 120 games reached the battle on the day after the last, both champions with an army, no command refused, none a draw.
  - The first seat won 60 of 120. The coin toss's attacker won 73 (61 %), and the side with the bigger army 83 (69 %).
  - Still not measured: how the computer's army compares with a person's. That needs games played by people.
- **Review of step 7** (fixed): the battle view's rule said plainly, a start off the map asked about no zone, the switch's undo step named
  when turned off, and tests for the switch turned off, a refused switch recording nothing and a hero walled in another zone.
- **Review of step 6** (fixed): the switch also clears the players' own hero limits; the fix never takes a tile of another player's zone;
  a loaded map with both switches on shows the arena's; a loaded tournament template with another hero limit shows its row.
