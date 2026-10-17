# Eternal Wars documentation

## Design: how the game plays (`design/`)
The game design document, split by mechanic. Start at the overview.

| Document | What it covers |
|---|---|
| [overview.md](design/overview.md) | Vision, pillars, core loop, platforms, non-goals; index of the design docs |
| [adventure-map.md](design/adventure-map.md) | Map sizes, terrain and movement, roads, map objects, neutral monsters, meeting heroes, gates, fog of war |
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

## Content: catalogs and lore (`content/`)

| Document | What it covers |
|---|---|
| [lore.md](content/lore.md) | World premise, groups, the ring of towns and the sworn enemies (draft for review) |
| [hero-roster.md](content/hero-roster.md) | All 128 roster heroes by faction, the roster template and the war machines |
| [skills.md](content/skills.md) | Every hero skill and primary stat: effects per level, status, offer weights |
| [town-buildings.md](content/town-buildings.md) | Town buildings, the tech tree and every town's dwellings |
| [spells.md](content/spells.md) | Spell catalog: six schools, four levels, three ranks (design, not implemented) |
| [artifacts.md](content/artifacts.md) | Artifact catalog: slots, tiers, sets and lore |

## Technical: how the code works (`technical/`)

| Document | What it covers |
|---|---|
| [architecture.md](technical/architecture.md) | Three-tier solution layout, dependency rules, runtime composition |
| [event-system.md](technical/event-system.md) | The custom pub/sub (event publisher / handler) and the event catalogue |
| [development.md](technical/development.md) | Toolchain, build/run/test commands, the simulator, smoke tests, coding conventions |
| [reference-sandbox.md](technical/reference-sandbox.md) | What was taken from the earlier sandbox project, what was left, what looked wrong |

## Project: plans and decisions (`project/`)

| Document | What it covers |
|---|---|
| [roadmap.md](project/roadmap.md) | Phased delivery plan with milestones and exit criteria |
| [open-questions.md](project/open-questions.md) | Design questions still to answer; **🚧 blocking** ones first |
| [decisions.md](project/decisions.md) | Architecture and design decision log (short ADRs) |
| [ideas.md](project/ideas.md) | Raw design notes from the owner; removed once implemented or recorded |

Rules for keeping these in step: a decision goes into `decisions.md` and the matching design doc; an answered question
moves to the Answered list of `open-questions.md`; roadmap checkboxes follow the code. Numbers that live in data files
(`data/*.csv`) are quoted, not redefined, in the docs.
