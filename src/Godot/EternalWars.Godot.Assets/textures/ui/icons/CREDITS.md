# UI icons

Button icons, 128 × 128 with transparency, generated for this project by the owner (2026-11-05, 2026-11-11, 2026-11-17; the action and battle icons of sets 15–18 and the buttons of sets 19–22 replaced 2026-11-21), cut from the
sheets of [icon-prompts.md](../../../../../../docs/content/prompts/icon-prompts.md) (sets 15–22). Loaded through
`EternalWars.Godot.UI.UiIcons`, which scales them to button size; a missing file leaves the button text-only.

| File | Used for |
|---|---|
| close.png | Close buttons, Delete (saves, map events) |
| confirm.png | OK, Accept, Start, Apply |
| hero.png (plumed helmet, the owner's select-hero cursor art) | the map's "Hero screen (H)" action button |
| info.png (scroll) | not used |
| arrow_left.png / arrow_right.png | previous / next hero, Back |
| arrow_up.png / arrow_down.png | move a map event or event action up / down |
| plus.png / minus.png | add player; remove player, remove a condition |
| ban.png | the editor's Bans tab; Cancel in the map events form |
| settings.png (cogwheel) | the game menu's Settings tab |
| menu.png (three stone bars) | the game's Menu (F10) button |
| save.png (closed book) / load.png (open book) | Save and Load in the game menu and the map editor, the "Save and load" tab, the template editor's Save, "Save replay" |
| undo.png / redo.png | the map editor's Undo and Redo |
| zoom_in.png / zoom_out.png, lock.png / unlock.png | not used yet |
| hourglass.png (brass hourglass on its side) | Wait (W) in battle |
| end_turn.png (wooden hourglass) | End turn |
| switch_level.png (cave mouth, up and down arrows) | Switch level (L); the map editor's Level (L) |
| spell_book.png (purple leather book with a glowing rune) | the map's "Spell book (C)" action button; the hero's mana in battle, before the spell buttons; the town's "Spell book (owned)" |
| spells.png (open) | the hero screen's spell list |
| quests.png (sealed scroll) | Quests (Q) |
| puzzle_map.png (open scroll) | Puzzle map (P) |
| dig.png (shovel) | Dig (G) |
| kingdom.png (castle) | Kingdom (K) |
| marketplace.png (balance scales) | Marketplace |
| thieves_guild.png (hooded mask, daggers) | Thieves' guild (town, tavern) |
| defend.png (round red shield with a cross) | Defend (D) in battle |
| warcry.png (war horn) | the warcry buttons in battle |
| retreat.png (tattered white flag on a spear) | Retreat (R) |
| surrender.png (sword offered on open hands) | Surrender |
| quick_combat.png (crossed swords, lightning) | Quick combat (Q) |
| auto_combat.png (crossed swords, cogwheel) | Auto combat (the offer before a battle) |
| move.png (horseshoe, hoofprints), next_hero.png (helmet, arrow), melee.png (dagger), shoot.png (arrow), cast.png (book over a hand) | not used yet |
| difficulty_easy.png (pale wood pawn), difficulty_normal.png (knight), difficulty_hard.png (walnut bishop), difficulty_expert.png (rook), difficulty_master.png (blackened queen, gold crown) | the difficulty levels (ADR-225): the new-game screen's difficulty row, the victory screen, the leaderboard; cut from sheet 29 (2026-11-21), the sheet's spare queen unused |

## Map and template editor

Generated for this project by the owner (2026-11-21), cut from the sheets of
[editor-icon-prompts.md](../../../../../../docs/content/prompts/editor-icon-prompts.md) (E1–E6, C1–C3, C5, C6, C8–C11; one cell each, the
E6 spare unused). Sheets C4 (view) and C7 (map settings) are not drawn yet: those commands keep their lettered placeholders.

| File | Used for |
|---|---|
| tool_terrain, tool_road, tool_river, tool_height, tool_start_zone, tool_scatter | the brush tools (palette, Tools menu) |
| tool_object, tool_monster, tool_grail, tool_link_gates, tool_stamp | Object, Monster (also the template's Monsters guard), Grail, Link gates, Paste and Save selection as stamp |
| tool_measure | the Measure tool |
| tool_select, tool_move, tool_erase | Select, Move, Erase |
| tool_fill | the Terrain tool's Fill option |
| zone_start, zone_ai_start, zone_treasure, zone_junction | the template editor's Add buttons and zone Type list |
| connection_normal, connection_wide, connection_gate, connection_portal | the template connection's Kind list |
| guard_garrison, guard_border, guard_border_gate | the template's guard lists |
| cut, copy, paste, duplicate, delete, select_all, deselect, rotate, mirror | the Edit menu's commands |
| crop, resize | Crop to selection, Resize map |
| link | the inspector's New and Edit event links |
| new, save_as, quit, play, help, command_palette | New map (and the template's New), Save as, Quit, Test the map, Help, Command palette |
| panel_left, panel_bottom, panel_right, panels_hide | the View menu's panel switches |
| eye, eye_off | a layer shown or hidden (Layers) |
| inspector, layers, events, history, output | the panel tabs and the View menu |
| filter | not used yet |
| error, warning | the Problems tab, its filters and list, the status bar's problem counts, the template's issues |
| favourite, favourite_off | the object browser's favourite button |
| height_raise, height_lower, height_level, height_smooth | the Height tool's actions |
| brush_round, brush_square | the brush's Round option |
| generate, new_seed, arrange | Generate (the panel and the template editor), New seed, Arrange |
| mirror_vertical, mirror_horizontal, mirror_diagonal | the template's Symmetry list |
| strength_none, strength_weak, strength_average, strength_strong | the template zone's Monsters list |

The resource icons are in `../resources/` (see its CREDITS.md).
