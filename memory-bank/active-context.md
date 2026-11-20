# Active context

**As of 2026-11-20.** Refresh this file at the end of every piece of work: replace, do not append.

## Where the project is
Phases 0–4 are done and closed out (ADR-114, ADR-117). Phase 5 (content and polish) is in progress: magic, sieges, relics, map events,
the owner's missing-mechanics picks, the arena and LAN multiplayer are built. Phase 6 has begun: campaigns and story events (ADR-241,
ADR-242). The November refactoring plan is closed (ADR-226; the plan is in [docs/papers](../docs/papers/refactoring-plan-2026-11.md)).
Detail: [roadmap.md](roadmap.md).

## Last work
- ADR-248 (presentation): icon and portrait prompts painted realistically, not as cartoons: new style blocks, `ICON-NEG-TOON` on every
  painted sheet; the icons and crests already generated and cursor sets 1–3 are to be regenerated.
- ADR-247 (random maps and editor): the map and template editor overhaul is planned: one creator-app workspace, seven steps;
  [docs/papers/editor-plan.md](../docs/papers/editor-plan.md); questions EDIT-1 … EDIT-12 asked; nothing built.
- ADR-246 (presentation): the town screen overhaul is planned: a 3D town scene per faction with full town windows, six steps;
  [docs/papers/town-screen-plan.md](../docs/papers/town-screen-plan.md); questions TOWN-14 … TOWN-23 asked; nothing built.

## Next
1. A person's full game on Ember March (the Phase 3 exit's last check).
2. The prologue map (CAMP-1), as the first map of a campaign; Border Watch's story is a placeholder.
3. The remaining Phase 5 items in [roadmap.md](roadmap.md) ("Presentation and UI", "Town screen overhaul", "Editor overhaul",
   "Platform and art", "Roster"); both overhauls start with the owner's answers (TOWN-14 … TOWN-23, EDIT-1 … EDIT-12).
4. The "Code health" list in [roadmap.md](roadmap.md): skills, heroes and town types as typed ids; `InfoSheets` as data once localisation
   is decided (ADR-239).

## Known problems
- Numbers marked *(placeholder)* in the ADRs and in [open-questions.md](open-questions.md) wait for play-testing.
