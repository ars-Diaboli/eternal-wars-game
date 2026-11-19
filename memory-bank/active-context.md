# Active context

**As of 2026-11-20.** Refresh this file at the end of every piece of work: replace, do not append.

## Where the project is
Phases 0–4 are done and closed out (ADR-114, ADR-117). Phase 5 (content and polish) is in progress: magic, sieges, relics, map events,
the owner's missing-mechanics picks, the arena and LAN multiplayer are built. Phase 6 has begun: campaigns and story events (ADR-241,
ADR-242). The November refactoring plan is closed (ADR-226; the plan is in [docs/papers](../docs/papers/refactoring-plan-2026-11.md)).
Detail: [roadmap.md](roadmap.md).

## Last work
- ADR-244 (presentation): new terrain texture prompts: each terrain its own saturated colour and structure, a texture shows
  about 30 m of map; the mood comes from light and grading.
- ADR-243 (code health): creature, spell, artifact and building ids are types of their own (`CreatureId`, `SpellId`, `ArtifactId`,
  `BuildingId`); the file formats are unchanged.
- ADR-241 (game modes): campaigns: maps won one after another, heroes and campaign variables carried, campaign files, saves keep their
  campaign (save v6), the sample campaign Border Watch; Campaign in the main menu (smoke `game-campaign`).

## Next
1. A person's full game on Ember March (the Phase 3 exit's last check).
2. The prologue map (CAMP-1), as the first map of a campaign; Border Watch's story is a placeholder.
3. The remaining Phase 5 items in [roadmap.md](roadmap.md) ("Presentation and UI", "Platform and art", "Roster").
4. The "Code health" list in [roadmap.md](roadmap.md): skills, heroes and town types as typed ids; `InfoSheets` as data once localisation
   is decided (ADR-239).

## Known problems
- Numbers marked *(placeholder)* in the ADRs and in [open-questions.md](open-questions.md) wait for play-testing.
