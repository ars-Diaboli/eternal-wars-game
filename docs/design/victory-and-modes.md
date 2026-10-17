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
- More conditions (grail, flag all mines, defeat all monsters), team survival and campaign hooks are open ❓.

## Game modes (priority order)
1. **Single scenario vs. AI** (implemented).
2. **Hot-seat multiplayer** (implemented: several human players share the machine; battles between humans are played
   on the same screen).
3. **Story campaign**: chained scenarios, carry-over heroes, scripted map events and dialogue. This needs editor
   support for triggers and a script/event format (Phase 6).
- Online multiplayer: not planned (core determinism keeps the door open).

## Game options
Chosen on the new-game screen: map or template, players and teams, fog mode ([adventure-map.md](adventure-map.md)),
complexity (starting resources, [towns-and-economy.md](towns-and-economy.md)), seed. Saves and replays record them
(ADR-058).
