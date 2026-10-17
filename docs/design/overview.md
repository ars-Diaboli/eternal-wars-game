# Game design: overview

> Status: **living document**, split by mechanic into the files of this folder. Items marked ❓ are open; see
> [open-questions.md](../project/open-questions.md). Decisions are recorded in [decisions.md](../project/decisions.md).

| Document | Covers |
|---|---|
| [overview.md](overview.md) | Vision, pillars, core loop, platforms, non-goals (this file) |
| [adventure-map.md](adventure-map.md) | Map sizes, terrain and movement, roads, objects, neutral monsters, heroes meeting, gates, fog of war |
| [heroes.md](heroes.md) | Primary stats, skills, levels, army, artifacts, mana |
| [towns-and-economy.md](towns-and-economy.md) | Resources, buildings, the creature tech tree, growth, map dwellings and caravans, tavern, income |
| [factions.md](factions.md) | Town groups, playable and non-playable towns, rivalries |
| [combat.md](combat.md) | Battlefield, footprints, turn order, damage, cover, morale and luck, capabilities, war machines |
| [magic.md](magic.md) | Schools, spell levels, mana (spell catalog: [spells.md](../content/spells.md)) |
| [victory-and-modes.md](victory-and-modes.md) | Victory and defeat, game modes |
| [tools.md](tools.md) | Map editor, 2D harness, headless simulator |
| [random-maps.md](random-maps.md) | Templates and the random map generator |
| [presentation.md](presentation.md) | Art direction, camera, UI, audio |
| [departures.md](departures.md) | Deliberate departures from HoMM3 |

## 1. Vision

*Eternal Wars* is a turn-based **dark fantasy** strategy game inspired by Heroes of Might and Magic III,
presented in full 3D. Players lead heroes across an adventure map, gather resources, develop towns,
recruit armies and fight tactical battles on a square grid where creatures occupy 2×2, 3×3 or 4×4
cells.

It is **HoMM-inspired, not a clone**: we keep the proven loop (heroes, towns, weekly growth, tactical
battles) and deliberately change systems where we can add identity. Every twist is logged in
[departures.md](departures.md).

The 3D presentation is meant to **add atmosphere without hurting readability**: the camera, map and
battles must stay as legible as HoMM3's hand-painted 2D.

**Modes, in priority order**: single-player vs AI → hot-seat → story campaign. Online multiplayer is
out of scope, but the deterministic core keeps it possible.

## 2. Pillars

1. **"One more turn"**: short, satisfying turns; meaningful decisions every day of the week.
2. **Readable at a glance**: every tile, object and unit identifiable from the default camera.
3. **Moddable by data**: factions, units, spells, artifacts and map objects are data, not code.
4. **Deterministic core**: the same inputs always produce the same game. This enables replays, tests,
   AI simulation and (later) multiplayer.

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
        Week start: creature growth ("week of X" events planned ❓)
```

## 4. Platforms and scope
- **Platforms for 1.0: Windows and Linux / Steam Deck** (controller-friendly UI and Deck performance
  budget to test). macOS later.
- **Player modding is not a 1.0 requirement.** Content stays data-driven, with versioned JSON formats,
  because that helps us. A supported mod loader and mod folder may come after 1.0.

## 5. Non-goals (for now)
- Real-time elements, online multiplayer, procedural campaign, mobile touch UI, official mod support, sea travel
  (ADR-051).
