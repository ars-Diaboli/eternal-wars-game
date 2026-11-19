# Active context

**As of 2026-11-19.** Refresh this file at the end of every piece of work: replace, do not append.

## Where the project is
Phases 0–4 are done and closed out (ADR-114, ADR-117). Phase 5 (content and polish) is in progress: magic, sieges, relics, map events,
the owner's missing-mechanics picks, the arena and LAN multiplayer are built. Phase 6 has begun: campaigns and story events (ADR-241,
ADR-242). The November refactoring plan is closed (ADR-226; the plan is in [docs/papers](../docs/papers/refactoring-plan-2026-11.md)).
Detail: [roadmap.md](roadmap.md).

## Last work
- ADR-241 (game modes): campaigns: maps won one after another, heroes and campaign variables carried, campaign files, saves keep their
  campaign (save v6), the sample campaign Border Watch; Campaign in the main menu (smoke `game-campaign`).
- ADR-242 (map events): dialogues, ending the game from an event, captured and battle events (map format v42).
- ADR-240 (presentation): the splat reads adventure-only pictures (`textures/terrain/adventure/`) and `_normal` / `_roughness` / `_ao`
  maps beside a picture; no terrain has them yet (the owner's PBR grass set was tried and dropped).

## Next
1. A person's full game on Ember March (the Phase 3 exit's last check).
2. The prologue map (CAMP-1), as the first map of a campaign; Border Watch's story is a placeholder.
3. The remaining Phase 5 items in [roadmap.md](roadmap.md) ("Presentation and UI", "Platform and art", "Roster").
4. The "Code health" list in [roadmap.md](roadmap.md): typed content ids, and `InfoSheets` as data once localisation is decided (ADR-239).

## Known problems
- Numbers marked *(placeholder)* in the ADRs and in [open-questions.md](open-questions.md) wait for play-testing.
