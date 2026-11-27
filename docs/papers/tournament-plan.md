# Tournament mode: guide and plan

The plan for a new game mode, the **tournament** (owner, 2026-11-27; ADR-366). Two players build up apart from each other, each in a zone
of their own against the map's neutrals, for a time chosen when the game is created. When the time is up their heroes meet in one battle
with no retreat and no surrender, and its winner wins the game. It is played against the computer, hot-seat and over the LAN, where both
players play every day at once. The guide states the rules as the owner decided them (TRN-1 to TRN-9, 2026-11-27: the proposals, with
the lengths up to 3 months, and battles at the same time on every simultaneous day, not only in tournaments). Once built, the rules move
into [victory-and-modes.md](../design/victory-and-modes.md#game-modes) and this file becomes the report.

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

### Rules

**Turning it on.**
- The map's `rules.tournament` switch makes a map a tournament map, as `rules.arena` does for the arena. The generator writes it from a
  tournament template, and the editor sets it on hand-made maps. The templates come first, the editor's switch after them.
- The length is a game option, `GameOptions.TournamentDays`, chosen on the new-game screen: any number of days from 1 to 14, then whole
  weeks from 3 to 12 (21, 28, … 84 days; 3 months is the upper limit). The default is 14 days.
- A tournament map always has exactly two players, no teams and a hero limit of 1.
- The map's victory and defeat conditions do not count, nor do the last team standing or elimination. Nobody is out before the battle.

**The building phase (days 1 to N).**
- These are ordinary days: income, building one structure a day, the week's growth, map events and the neutrals.
- Each player has one hero.
- A player whose hero falls may hire another in a town's tavern, still at most one, from their own tavern pool.
- The status bar's calendar says how many days remain before the battle. The last day says so too.

**Isolation.**
- Each player's start zone is cut off from the other's. No passage, gate, portal or teleporter connects them; the generator and
  the validator make sure of this.
- A player never sees, reveals or reaches a tile outside their own zone:
  - **Sight:** sight, scouting and revealing objects stop at the zone's edge, so a Scouting hero cannot look over the wall.
  - **Movement spells:** Dimension Door, Town Portal and the other spells that move a hero work only inside the zone.
  - **Information spells:** spells that show the map or other players (Distant Vision, Detect Magic, Eavesdrop, …) show only the zone.
- What players would otherwise share becomes each player's own:
  - the taverns' hero pools: a pool per player;
  - the Grail: one per zone (with the zone's own obelisks), none when the template has no obelisks;
  - the artifact rolls: mirrored per zone, so both zones can hold the same artifact.
- The week's events and rumours stay shared, since they treat both players alike.
- Trading between the players is off.
- The players learn nothing about each other before the battle beyond the opponent's name, colour and town. On the LAN, a line says when
  the opponent has ended the day.

**The battle (the start of day N + 1).**
- When the calendar reaches the day after the last building day, the battle opens before anyone's turn, as on an arena's day 2.
- **Champions:** each player's champion is their one hero: on the map, else in a town's garrison. It fights with its own army only, not
  the garrison it stands by, so a player must gather the army into the hero by the last day.
- **The battlefield** is neutral ground, not either player's zone, with its terrain and obstacles drawn from the seed. It is never a
  siege. The attacker is chosen by a coin toss from the game's random numbers and announced.
- **No retreat and no surrender:** both buttons are off for both sides, and so is the computer's leaving. The battle gives no experience.
- **The end:**
  - The battle's winner wins the game.
  - A side whose champion has no army, or that has no hero at all, loses without a battle. When this happens to both sides, it is a draw.
  - When both armies fall in the battle, it is a draw.
  - A draw ends the game with no winner. Tournaments are never ranked.

**Where it is played.**
- **Against the computer:** one person and the computer, each in their own zone, turn by turn.
- **Hot-seat:** two people on one screen, turn by turn. Each turn shows only the zone of the player whose turn it is, and the battle is fought on the
  shared screen.
- **Over the LAN:**
  - **Days:** every day is played at once (the simultaneous days of ADR-197, here from the first day to the last).
  - **Conflicts:** because the zones are cut off and sight stops at their edges, conflicts cannot happen. The conflict watch stays as a
    safety net, and a conflict there would be a bug.
  - **Battles and questions:** each player fights their own neutral battles and answers their own questions without waiting for the other.
    This holds on every simultaneous day, not only in tournaments: an ordinary LAN game's first week and an arena's preparation day too.
  - **The final battle** is fought live on both computers.
  - **Turn timer:** the host's turn timer is optional and is one clock for both.

**The result screen** is the arena's: who won, the champions and their armies, "Tournaments are not ranked", "Play again" (same map
and options, new seed) and "Save replay".

### Screen at a glance

```text
New-game screen, Tournaments tab
+-------------------------------------------+--------------------------------------+
| Templates and maps                        | Twin Vales  (2 players)              |
|  Twin Vales            M                  | Two mirrored valleys, cut off.       |
|  Twin Vales Large      L                  |                                      |
|  Island Gauntlet       S   (hand-made)    | Length: [<]  14 days (2 weeks)  [>]  |
|                                           | Blue  Haven   Human                  |
|                                           | Red   Inferno Computer               |
+-------------------------------------------+--------------------------------------+

Adventure map, status bar calendar:  Month 1, Week 2, Day 3 · Battle in 5 days
Last building day:                   Last day before the battle
```

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

### 3. Simultaneous days: the whole tournament, and battles at the same time (Core and Network)
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
  - **Not yet measured:** whether that is in the range a person reaches. No person's tournament is recorded to compare with; step 7's
    games against the computer are the check.

### 5. The game app
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
- **Hot-seat:** the hand-over screen between turns, so one person does not see the other's zone.
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
- **The "Tournament" switch** on the map settings window's General tab (`TournamentRules.Switched`):
  - Turned on, it sets the hero limit to 1 and greys it out, turns the arena off and hides its switch, and the Victory and loss tab
    greys out. Turned off, the hero limit stays at 1 until changed.
  - It is refused, as any hero limit is, while a player has more than one hero on the map.
- **Problems** lists step 2's checks. A player without a start zone has a fix, "Paint Red's start zone over the ground it reaches"
  (`StartZoneFix`, `TournamentCheck.MissingZone`):
  - It paints the tiles the player's heroes and town entrances walk to, over ground in no zone, and the footprint of each object whose
    entrance is among them.
  - There is no fix when that ground is not clearly the player's: another player's hero or town is on it, or a step leads from it into
    another player's zone.
- **The template inspector's "Tournament" row** (`TemplateEditorSession.WithTournament`): ticked, the hero limit becomes 1 and its row goes.
  The issues list shows the per-player checks: two players, no allies, the two parts kept apart.
- **Checks:** `TournamentEditorTests`, and the smoke `editor-tournament` (the switch, the greyed Victory and loss tab, Problems' missing
  start zones, the template's Tournament row and its joined-zones error).

### 7. Check
- **Seen in Godot:**
  - a tournament against the computer to the end;
  - a hot-seat tournament;
  - a LAN tournament between two computers, with days played at once and both players fighting neutrals in the same day.
- **AI runs:** the AI against itself over the templates (`sim game --runs`), checking that both sides reach the battle and how often the
  first seat wins.
- **Docs:** `victory-and-modes.md` states the rules; this file becomes the report; roadmap and active context updated.
