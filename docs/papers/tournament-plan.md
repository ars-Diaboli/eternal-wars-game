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

### 2. Random maps and validation
- **Template support:**
  - A template's `tournament` flag (template version bump).
  - Its validator requires two players, start zones that are each connected inside, and no connection, gate or portal between the two
    players' zones. The connectivity check runs per player instead of across the whole map.
  - The generator writes `rules.tournament`, the start zones and `MaxHeroes` = 1. A generator version bump is needed if output changes
    for existing templates; it should not.
- **Shipped templates:** two or three mirrored ones (small, medium, large) with a neutral spread fit for a month, for example "Twin
  Vales". `sim maps` checks them in CI as it does the others.
- **Map validator (hand-made maps):**
  - **Errors:**
    - a tournament map without two start zones;
    - a passable route, a portal or a teleporter between the zones;
    - an arena switch on as well;
    - a hero limit above 1.
  - **Warning:** a zone much richer than the other, using the generator's fairness numbers.

### 3. Simultaneous days: the whole tournament, and battles at the same time (Core and Network)
- **Every day at once:** `SimultaneousDayCoordinator` opens every day of a tournament at once on the LAN, not only the first week's.
  The final battle's day is no simultaneous day.
- **Battles and questions per player**, on every simultaneous day (a tournament's, an ordinary LAN game's first week, an arena's
  preparation day):
  - `GameState.Battle` and `PendingDecision` become per-player slots; on a day played turn by turn there is only ever one.
  - "Wait: a battle is being fought." goes; a conflict that ends the day while battles are open discards them with the day.
  - The command log stays the host's order, so replays stay exact.
  - `PlayerViewFeed` sends each client its own battle.
  - This is the largest technical item: the battle and decision services, `GameStateDigest`, the replay viewer and the LAN views all read
    a single battle today.
- **Tests:**
  - a LAN tournament played at once for a week, with both players fighting neutrals in the same day;
  - an ordinary LAN game's first week with two battles open at once, and a conflict during one;
  - the record replayed;
  - a save loaded on one computer going on turn by turn;
  - the final battle live.

### 4. The computer
- **Building phase:** the ordinary adventure AI plays its zone. It never looks for an enemy, since there is none it can reach.
- **Planning for the deadline:**
  - It keeps its one hero alive (no risky fights in the last days).
  - It weighs growth (dwellings, mines) against army power by the days left.
  - On the last day it spends everything on the battle's army and gathers it into the hero, as on an arena's day 1 (ADR-160).
- **Tuning:** `sim game --runs` on the tournament templates checks that the computer arrives with an army in the range a person would.

### 5. The game app
- **New-game screen:**
  - A **Tournaments** tab (ADR-201's tabs) lists the tournament templates and maps, with the length row and the two players
    (Human / Computer here, and Here / LAN player / Computer in the lobby). The length is a stepper: a day a step up to 14 days, then a
    week a step up to 12 weeks.
  - The LAN lobby notes that every day is played at once.
- **Adventure HUD:**
  - The calendar's countdown and the last day's notice.
  - The hero card shows the limit of 1 (the tavern refuses with the reason).
  - No trading or other-player panels.
- **Hot-seat:** the hand-over screen between turns, so one person does not see the other's zone.
- **Battle:**
  - The final battle opens by itself, both armies shown.
  - The retreat and surrender buttons greyed out, with "No retreat in the tournament's battle" on the status bar.
- **Result screen:** the arena's, with the tournament's wording.
- **Smoke scenario `game-tournament`:** a short tournament against the computer played to the battle and the result.

### 6. The map editor
- The "Tournament" switch on the map settings window's General tab: it greys out the victory and loss tab, sets the hero limit to 1 and
  hides the arena switch.
- The validator's checks from step 2 shown in Problems, with fix-its where one exists (painting the missing start zone).
- The template editor's `tournament` flag in the template inspector, with the per-player connectivity check.

### 7. Check
- **Seen in Godot:**
  - a tournament against the computer to the end;
  - a hot-seat tournament;
  - a LAN tournament between two computers, with days played at once and both players fighting neutrals in the same day.
- **AI runs:** the AI against itself over the templates (`sim game --runs`), checking that both sides reach the battle and how often the
  first seat wins.
- **Docs:** `victory-and-modes.md` states the rules; this file becomes the report; roadmap and active context updated.
