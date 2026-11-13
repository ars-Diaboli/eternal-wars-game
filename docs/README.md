# Eternal Wars documentation

## Design: how the game plays (`design/`)
The game design document, split by mechanic. Start at the overview.

| Document | What it covers |
|---|---|
| [overview.md](design/overview.md) | Vision, pillars, core loop, platforms, non-goals; index of the design docs |
| [adventure-map.md](design/adventure-map.md) | Map sizes, terrain and movement, roads, map objects, neutral monsters, meeting heroes, gates, fog of war, map events |
| [heroes.md](design/heroes.md) | Primary stats, skills, levels, army, artifacts, mana, governors |
| [towns-and-economy.md](design/towns-and-economy.md) | Resources, buildings, the creature tech tree, growth, map dwellings and caravans, tavern, income |
| [factions.md](design/factions.md) | Town groups, playable and non-playable towns, rivalries |
| [combat.md](design/combat.md) | Battlefield, footprints, turn order, damage, cover, morale and luck, capabilities, war machines |
| [magic.md](design/magic.md) | Schools, spell levels, mana |
| [victory-and-modes.md](design/victory-and-modes.md) | Victory and defeat, game modes, game options |
| [random-maps.md](design/random-maps.md) | Templates and the random map generator |
| [tools.md](design/tools.md) | Map editor, 2D harness, headless simulator |
| [presentation.md](design/presentation.md) | Art direction, camera, UI, audio |
| [departures.md](design/departures.md) | Deliberate departures from HoMM3 |
| [missing-mechanics.md](design/missing-mechanics.md) | HoMM3/HotA and HoMM5 mechanics we lack, with status and next candidates |

## Content: catalogs and lore (`content/`)

| Document | What it covers |
|---|---|
| [lore.md](content/lore.md) | World premise, groups, the ring of towns and the sworn enemies (draft for review) |
| [hero-roster.md](content/hero-roster.md) | All 128 roster heroes by faction, the roster template and the war machines |
| [skills.md](content/skills.md) | Every hero skill and primary stat: effects per level, status, offer weights |
| [town-buildings.md](content/town-buildings.md) | Town buildings, the tech tree and every town's dwellings |
| [spells.md](content/spells.md) | Spell catalog: six schools, four levels, three ranks |
| [artifacts.md](content/artifacts.md) | Artifact catalog: slots, tiers, sets and lore |
| [icon-prompts.md](content/icon-prompts.md) | Image-model prompts for the UI icons, with the shared style block |

## Technical: how the code works (`technical/`)

| Document | What it covers |
|---|---|
| [architecture.md](technical/architecture.md) | Three-tier solution layout, dependency rules, runtime composition |
| [event-system.md](technical/event-system.md) | The custom pub/sub (event publisher / handler), conventions and event ordering |
| [development.md](technical/development.md) | Toolchain, build/run/test commands, the simulator, smoke tests, coding conventions |

## Project: plans and decisions (`project/`)

| Document | What it covers |
|---|---|
| [roadmap.md](project/roadmap.md) | Phased delivery plan with milestones and exit criteria |
| [open-questions.md](project/open-questions.md) | Design questions still to answer; **🚧 blocking** ones first |
| [decisions.md](project/decisions.md) | Architecture and design decision log (short ADRs), ADR-101 on |
| [decisions-archive.md](project/decisions-archive.md) | ADR-001 … ADR-100, read-only; search for `### ADR-NNN` |
| [ideas.md](project/ideas.md) | Owner's raw notes; each leaves once it becomes an ADR or an open question |
| [archive/code-review-2026-11.md](project/archive/code-review-2026-11.md) | The November 2026 code review and what became of each item (ADR-165 … ADR-175), read-only |
| [archive/relief-plan.md](project/archive/relief-plan.md) | How the adventure map's relief was built in five steps (ADR-181, ADR-182), read-only |

Rules for keeping these in step: a decision goes into `decisions.md` and the matching design doc; an answered question
moves to the Answered list of `open-questions.md`; roadmap checkboxes follow the code. Numbers that live in data files
(`data/*.csv`) are quoted, not redefined, in the docs.
