# Active context

**As of 2026-11-19.** Refresh this file at the end of every piece of work: replace, do not append.

## Where the project is
Phases 0–4 are done and closed out (ADR-114, ADR-117). Phase 5 (content and polish) is in progress: magic, sieges, relics, map events,
the owner's missing-mechanics picks, the arena and LAN multiplayer are built. The November refactoring plan is closed (ADR-226; the
plan is in [docs/papers](../docs/papers/refactoring-plan-2026-11.md)). Detail: [roadmap.md](roadmap.md).

## Last work
- ADR-235 (Godot check): what ADR-148 had not seen runs and was looked at (five smoke scenarios on `tools/smoke/spells.ewmap.json`);
  `--seed=` / a smoke entry's `Seed` repeats a scenario's battles; the battle log goes to `godot.log` in scripted runs.
- ADR-234 (save/load): replays measured (`sim replay` prints the time; 5 s for eight players to month 5); the replay viewer's forward jumps
  play on the watched game unheard (`GameSession.Unheard`, `Events` a bus of its own); no state snapshot yet.

## Next
1. A person's full game on Ember March (the Phase 3 exit's last check).
2. The remaining Phase 5 items in [roadmap.md](roadmap.md) ("Presentation and UI", "Platform and art", "Roster").
3. The "Code health" list in [roadmap.md](roadmap.md): `ChangeStamp` and `SightOf`, the map-event action switches and requests, the
   review's leftovers, fixed seeds for the smoke games (ADR-226).

## Known problems
- Smoke scenario `game-battle-control` fails at frame 184 (no Surrender while the computer's stack acts); it failed before ADR-227 too.
  The smoke games draw a random seed, so their battles differ between runs.
- `PathCostMapTests.TheMove_FollowsTheRouteJustPlanned_WithoutSearchingAgain` fails now and then in a full run: it reads the
  process-wide `PathFinder.Counters`, which tests running alongside also move.
- Numbers marked *(placeholder)* in the ADRs and in [open-questions.md](open-questions.md) wait for play-testing.
