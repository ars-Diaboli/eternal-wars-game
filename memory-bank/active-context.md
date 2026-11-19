# Active context

**As of 2026-11-19.** Refresh this file at the end of every piece of work: replace, do not append.

## Where the project is
Phases 0–4 are done and closed out (ADR-114, ADR-117). Phase 5 (content and polish) is in progress: magic, sieges, relics, map events,
the owner's missing-mechanics picks, the arena and LAN multiplayer are built. The November refactoring plan is closed (ADR-226; the
plan is in [docs/papers](../docs/papers/refactoring-plan-2026-11.md)). Detail: [roadmap.md](roadmap.md).

## Last work
- ADR-239 (code health): every smoke scenario has its own seed (`game-battle-control` passes); `CheckRefusals` is a runtime switch the
  test project sets; map objects hand out read-only collections (`MapObjectCollectionsTests`).
- ADR-238 (code health): map-event answers take their decision through `DecisionSteps`; a Pandora's Box queues its hero event itself
  (`MapEventState.QueueHeroEvent`), and `PandorasBoxOpened` no longer carries it.

## Next
1. A person's full game on Ember March (the Phase 3 exit's last check).
2. The remaining Phase 5 items in [roadmap.md](roadmap.md) ("Presentation and UI", "Platform and art", "Roster").
3. The "Code health" list in [roadmap.md](roadmap.md): typed content ids, and `InfoSheets` as data once localisation is decided (ADR-239).

## Known problems
- `PathCostMapTests.TheMove_FollowsTheRouteJustPlanned_WithoutSearchingAgain` fails now and then in a full run: it reads the
  process-wide `PathFinder.Counters`, which tests running alongside also move.
- Numbers marked *(placeholder)* in the ADRs and in [open-questions.md](open-questions.md) wait for play-testing.
