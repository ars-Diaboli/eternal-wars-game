# Eternal Wars documentation

Documents for people: how the game plays, what is in it, how the code works, and papers on past work. The project's working memory
for LLM agents (decisions, roadmap, open questions, the current focus) is in [memory-bank/](../memory-bank/README.md).

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

## Content: catalogs, lore and asset prompts (`content/`, `content/prompts/`)

| Document | What it covers |
|---|---|
| [lore.md](content/lore.md) | World premise, groups, the ring of towns and the sworn enemies (draft for review) |
| [hero-roster.md](content/hero-roster.md) | All 128 roster heroes by faction, the roster template and the war machines |
| [skills.md](content/skills.md) | Every hero skill and primary stat: effects per level, status, offer weights |
| [town-buildings.md](content/town-buildings.md) | Town buildings, the tech tree and every town's dwellings |
| [spells.md](content/spells.md) | Spell catalog: six schools, four levels, three ranks |
| [artifacts.md](content/artifacts.md) | Artifact catalog: slots, tiers, sets and lore |
| [faction-art.md](content/faction-art.md) | The owner's key paintings of every town and the look they set per faction: setting, light, landmark, buildings, colours, what to avoid |
| [icon-prompts.md](content/prompts/icon-prompts.md) | Image-model prompts for the UI icons and the faction heraldry, as 2×2 and 2×3 sheets with shared style blocks and the shared anti-cartoon negative |
| [cursor-prompts.md](content/prompts/cursor-prompts.md) | Image-model prompts for the mouse cursors (menus, adventure map, battle) in grey iron, and their hotspots |
| [spell-icon-prompts.md](content/prompts/spell-icon-prompts.md) | Image-model prompts for the icons of the 36 most common spells, one sheet a school |
| [creature-icon-prompts.md](content/prompts/creature-icon-prompts.md) | Image-model prompts for the portraits of all creatures, one file a faction in `creature-icons/`, and their backdrops |
| [editor-icon-prompts.md](content/prompts/editor-icon-prompts.md) | Image-model prompts for the map and template editor icons: tools, commands, panel tabs, tool options, template zones and connections |
| [hero-town-icon-prompts.md](content/prompts/hero-town-icon-prompts.md) | Image-model prompts for the 128 hero portraits and the town icons |
| [ui-element-prompts.md](content/prompts/ui-element-prompts.md) | Image-model prompts for frames, buttons, slots, bars and screen backgrounds |
| [question-picture-prompts.md](content/prompts/question-picture-prompts.md) | Image-model prompts for the pictures on the adventure map's question cards, one for each kind of question |
| [puzzle-map-prompts.md](content/prompts/puzzle-map-prompts.md) | Image-model prompts for the puzzle map's faction pictures |
| [terrain-texture-prompts.md](content/prompts/terrain-texture-prompts.md) | Image-model prompts for the terrain textures of the adventure map and the battlefield |
| [town-model-prompts.md](content/prompts/town-model-prompts.md) | 3D-model prompts for the ten town types on the adventure map: format, style block, lore per town |
| [road-river-prompts.md](content/prompts/road-river-prompts.md) | Texture prompts for roads and rivers, and 3D prompts for the bridges |
| [environment-prompts.md](content/prompts/environment-prompts.md) | 3D and material prompts for map scenery, battle obstacles, scatter debris and their shared materials |

## Technical: how the code works (`technical/`)

| Document | What it covers |
|---|---|
| [architecture.md](technical/architecture.md) | Three-tier solution layout, dependency rules, runtime composition |
| [event-system.md](technical/event-system.md) | The custom pub/sub (event publisher / handler), conventions and event ordering |
| [development.md](technical/development.md) | Toolchain, build/run/test commands, the simulator, smoke tests, coding conventions |

## Papers: plans and reports (`papers/`)

| Document | What it covers |
|---|---|
| [adventure-hud-plan.md](papers/adventure-hud-plan.md) | The adventure HUD redesign: the guide to every part as decided from the mockups, the open points, a six-step plan and the prompts for the next adventure and battle HUD mockups |
| [battle-hud-plan.md](papers/battle-hud-plan.md) | The battle HUD restyle in the adventure HUD's look: the guide from the round-1 mockups' recommendations, pending the owner's decisions, and a five-step plan |
| [code-review-2026-11.md](papers/code-review-2026-11.md) | The November 2026 code review and what became of each item (ADR-165 … ADR-175) |
| [relief-plan.md](papers/relief-plan.md) | How the adventure map's relief was built in five steps (ADR-181, ADR-182) |
| [town-screen-plan.md](papers/town-screen-plan.md) | The town screen overhaul: a study of today's screen, HoMM3 with HotA and HoMM5, the target, every screen, the technical and art plan, six steps and the open questions (ADR-246) |
| [editor-plan.md](papers/editor-plan.md) | How the map and template editor overhaul was planned and built: the study of the old editor, the HoMM3 / HotA and HoMM5 editors and creator tools, the target and how it was checked, the workspace and every tool, the technical plan and its seven steps (ADR-247, ADR-280) |
| [refactoring-plan-2026-11.md](papers/refactoring-plan-2026-11.md) | The November 2026 codebase review and refactoring plan, its baselines and how each item was checked (ADR-202 … ADR-226) |

## Project state (in `memory-bank/`)

| Document | What it covers |
|---|---|
| [roadmap.md](../memory-bank/roadmap.md) | Phased delivery plan with milestones and exit criteria |
| [open-questions.md](../memory-bank/open-questions.md) | Design questions still to answer; **🚧 blocking** ones first |
| [decisions/](../memory-bank/decisions/README.md) | Every decision (short ADRs) by category, with one index |
| [ideas.md](../memory-bank/ideas.md) | Owner's raw notes; each leaves once it becomes an ADR or an open question |

Rules for keeping these in step: a decision becomes an ADR in its `memory-bank/decisions/` category and the matching design doc states
the rule; an answered question moves to the Answered list of `open-questions.md`; roadmap checkboxes follow the code. Numbers that
live in data files (`data/*.csv`) are quoted, not redefined, in the docs.
