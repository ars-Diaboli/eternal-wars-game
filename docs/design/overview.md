# Game design: overview

> Status: **living document**, split by mechanic into the files of this folder. Decisions: [decisions.md](../project/decisions.md) (the
> ADR-n pointers); open items ❓: [open-questions.md](../project/open-questions.md); progress: [roadmap.md](../project/roadmap.md).
>
> **Convention**: the docs describe the game as it plays now; a rule is built unless tagged *(planned, ADR-n)*. ❓ marks an open point.
> Numbers that live in `src/Core/data/*.csv` are quoted, not restated; the content catalogs are in [docs/content](../content/).

| Document | Covers |
|---|---|
| [adventure-map.md](adventure-map.md) | Map sizes, terrain and movement, roads, objects, neutral monsters, heroes meeting, gates, fog of war |
| [heroes.md](heroes.md) | Roster, primary stats, skills, levels, army, artifacts |
| [towns-and-economy.md](towns-and-economy.md) | Resources, buildings, the creature tech tree, growth, events, caravans, tavern, marketplace, income |
| [factions.md](factions.md) | Town groups, playable and non-playable towns, rivalries |
| [combat.md](combat.md) | Battlefield, footprints, turn order, damage, cover, morale and luck, capabilities, war machines, sieges |
| [magic.md](magic.md) | Schools, guilds, mana, casting, Necromancy (spell catalog: [spells.md](../content/spells.md)) |
| [victory-and-modes.md](victory-and-modes.md) | Victory and defeat, score and replays, game modes and options |
| [tools.md](tools.md) | Map editor, 2D harness, headless simulator |
| [random-maps.md](random-maps.md) | Templates and the random map generator |
| [presentation.md](presentation.md) | Art direction, camera, UI, audio, speed settings |
| [departures.md](departures.md) | Deliberate departures from HoMM3 |
| [missing-mechanics.md](missing-mechanics.md) | What HoMM3 + HotA and HoMM5 have and we do not |

## 1. Vision
*Eternal Wars* is a turn-based **dark fantasy** strategy game inspired by Heroes of Might and Magic III, presented in full 3D. Players lead
heroes across an adventure map, gather resources, develop towns, recruit armies and fight tactical battles on a square grid where creatures
occupy 2×2, 3×3 or 4×4 cells.

It is **HoMM-inspired, not a clone**: we keep the proven loop (heroes, towns, weekly growth, tactical battles) and deliberately change
systems where we can add identity ([departures.md](departures.md)).

The 3D presentation must **add atmosphere without hurting readability**: camera, map and battles stay as legible as HoMM3's 2D.

**Modes** (priority order): single player vs AI → hot-seat → story campaign; LAN multiplayer before 1.0
([victory-and-modes.md](victory-and-modes.md#game-modes)). Internet play is out of scope.

## 2. Pillars
1. **"One more turn"**: short, satisfying turns; meaningful decisions every day of the week.
2. **Readable at a glance**: every tile, object and unit identifiable from the default camera.
3. **Moddable by data**: factions, units, spells, artifacts and map objects are data, not code.
4. **Deterministic core**: the same inputs always produce the same game, enabling replays, tests, AI simulation and multiplayer.

## 3. Core loop
```
 ┌──────────── Day (each player in turn) ────────────┐
 │ Move heroes ─► visit objects / pick up loot        │
 │      │                 │                           │
 │      ▼                 ▼                           │
 │   Battles ◄──── Neutral / enemy armies             │
 │      │                                             │
 │      ▼                                             │
 │ Experience, artifacts ─► stronger heroes           │
 │ Towns: build 1 structure/day, recruit weekly growth│
 └──────────────── End turn ─────────────────────────┘
        Week start: creature growth, week and month events
```

## 4. Platforms and scope
- **1.0: Windows and Linux / Steam Deck** (controller-friendly UI, Deck performance budget to test). macOS later.
- **Player modding is not a 1.0 requirement.** Content stays data-driven with versioned JSON formats; a mod loader may come after 1.0.

## 5. Non-goals (for now)
Real-time elements, internet multiplayer, procedural campaign, mobile touch UI, official mod support, sea travel (ADR-051).
