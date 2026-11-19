# Active context

**As of 2026-11-19.** Refresh this file at the end of every piece of work: replace, do not append.

## Where the project is
Phases 0–4 are done and closed out (ADR-114, ADR-117). Phase 5 (content and polish) is in progress: magic, sieges, relics, map events,
the owner's missing-mechanics picks, the arena and LAN multiplayer are built. The November refactoring plan is closed (ADR-226; the
plan is in [docs/papers](../docs/papers/refactoring-plan-2026-11.md)). Detail: [roadmap.md](roadmap.md).

## Last work
- ADR-188 finished: the battle AI's last steps. (e) `CastAhead`: the hero casts on its side's first turn of a round, choosing between its
  three best spells and nothing by playing each out on a copy of the battle to the end of the next round. (f) `Sieges`: an aimed siege
  engine breaks the gate first, besiegers go in together, defenders block an open way in. `Latest` is `Sieges`. `sim ai`/`sim battle`
  take `--spells` and `--siege`, `sim battle` also `--trace`. Only the gate-first aim measurably pays in AI-against-AI sieges (ADR-188).
- ADR-225 / ADR-227: the new-game screen's options tidied; difficulty renamed Easy … Master with five chess-piece buttons (map
  format v41 migrates the old names); a colour per player and the crest picker; "×" on every window; the calendar on the status bar.

## Next
1. A person's full game on Ember March (the Phase 3 exit's last check).
2. The remaining Phase 5 items in [roadmap.md](roadmap.md) ("Presentation and UI", "Platform and art", "Roster").
3. The "Code health" list in [roadmap.md](roadmap.md): LAN hardening, the schema hash, AI weights, `Main`'s policy (ADR-226).

## Known problems
- Smoke scenario `game-battle-control` fails at frame 184 (no Surrender while the computer's stack acts); it failed before ADR-227 too.
  The smoke games draw a random seed, so their battles differ between runs.
- Numbers marked *(placeholder)* in the ADRs and in [open-questions.md](open-questions.md) wait for play-testing.
