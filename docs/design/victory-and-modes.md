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
- **ADR-073 (not built)**:
  - New victory conditions: capture a given map object (an ownable one — mine, dwelling, town), obtain a given
    artifact (a hero of the player carries it), release a given hero from its prison.
  - New defeat condition: lose a given artifact (no hero of the player carries it any more); lose a town and lose a
    hero already cover the starting ones.
  - **Deadlines**: a victory condition may carry a time limit ("defeat the enemy within 3 months"); missing it defeats the
    player.
  - **Teams**: a team wins while at least one of its **human** players survives; eliminated human teammates share the win. A
    computer ally's survival does not win for them: they have lost (built, ADR-083).
  - The assumptions above were confirmed in ADR-075.
- **Players without a town** (ADR-075): a campaign-like map may start a player with heroes only; losing a hero is then the
  defeat condition. Set the grace period to 0 (the town rule is off) and add "lose a hero"; the validator warns about a
  player that starts without a town while the grace period is on.
- Campaign hooks are open ❓ (FLOW-1, Phase 6).

## Game modes (priority order)
1. **Single scenario vs. AI** (implemented).
2. **Hot-seat multiplayer** (implemented: several human players share the machine; battles between humans are played
   on the same screen).
3. **Story campaign**: chained scenarios, carry-over heroes, scripted map events and dialogue. This needs editor
   support for triggers and a script/event format (Phase 6).
- **LAN multiplayer** (ADR-088, planned; when and how: MP-1 ❓): the host is the source of truth. Players pick a map or generate a random one
  when they create the game. A multiplayer save loads in multiplayer, single player (the player picks one of the colours that were human,
  the computer plays the rest) or hot-seat. The first week may be played **simultaneously** while players stay in their start zones and do
  not touch the same object; when that breaks, the turn is played again in the normal order (MP-2 ❓). Online play over the internet is
  not planned.

## Game options
Chosen on the new-game screen: map or template, players and teams (**2 to 8 teams**, 8 = free for all; the map or template
sets defaults and may fix the teams or limit the team count, ADR-070), fog mode ([adventure-map.md](adventure-map.md)),
complexity (starting resources, [towns-and-economy.md](towns-and-economy.md)), seed. Saves and replays record them
(ADR-058).
Random maps (ADR-086): template, size, players (human or computer, town, hero, team).

**Turn timer** (ADR-088, planned, FLOW-3 ❓): off by default, 1 to 30 minutes a turn, for human players. When it runs out, the turn ends on
its own. It runs on the adventure map and in battle; if it runs out during a battle, the battle is finished first.
