# Cursor prompts (for an image-generating model)

Prompts for the mouse cursors (owner's note, ADR-204). The cursors change with what the pointer would do, as in HoMM3 and HoMM5: a plain
arrow on menus and screens, a horse on the adventure map, and one cursor per action in battle. They are generated like the icons
([icon-prompts.md](icon-prompts.md)): **sheets of 4 (2 × 2) or 6 (2 × 3)**, cut into one file per cursor. Each prompt is: the **cursor style
block** + the icons' **sheet block** (from icon-prompts.md) + the sheet's numbered cells, as written below.

## Format

- **Sheet**: as the icons: 2 × 2 at 1024 × 1024, 2 × 3 at 1536 × 1024, every cell 512 × 512, transparent background; cut the same way.
- **Cursor**: each cell becomes `cursor_<group>_<id>.png`, 32-bit RGBA, kept at 512 px as the master. The game ships it at **64 px** and
  scales it to 48 and 32 px for smaller UI scales; one still image a cursor, no animation.
- **Hotspot**: the pixel that points, given per cursor below as where it is in the cell (in 1/8 of the cell from the top left, so `1,1` is
  64 px in at 512 px and 8 px in at 64 px). The game reads it from `cursors.csv` (id, file, hotspot x, hotspot y), so a redrawn cursor only
  needs its row changed. The **pointing part touches the hotspot**: the arrow's tip, the sword's point, the centre of a target ring.
- **Shape**: every cursor must read at 32 px over a busy 3D map: one clear silhouette, a **thick dark outline (about 5 % of the cell)**, a
  light inner edge, no thin lines, no text, letters or numbers. Where a cursor needs a number (the days a move takes) the game draws it.
- **Spare cell**: as for the icons, a second take of the hardest cursor on the sheet.

**Cursor style block**:

> Game mouse cursor sheet, hand-painted dark-fantasy style, simple bold shapes with light painted shading, old bronze, steel, wood and
> leather, light from the top left, a thick dark outline around each subject and a thin light edge just inside it so it reads on dark and
> on light ground, flat front or side view as each cell says, no cast shadow, no background. No photo realism, no 3D render look, no pixel
> art.

Add to the negative prompt: "text, letters, numbers, frames, drop shadows, thin lines, scenery, background, extra objects".

## Menus and screens

### 1 · Pointers (2 × 2) — menus, town and hero screens, panels, the battle's bar

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_ui_pointer` | the standard computer arrow pointer shape, tip at the top left, tilted about 30°, with a short tail; old bronze with a dark outline | `0,0` (the tip) |
| 2 | `cursor_ui_busy` | a small upright hourglass in a bronze frame, the sand running into the lower bulb | `4,4` (centre) |
| 3 | `cursor_ui_blocked` | a round bronze ring with one diagonal bar from top left to bottom right, dark red inside the ring | `4,4` |
| 4 | — | spare: a second take of the pointer | |

**Use**: the pointer everywhere off the map and the battlefield, and over the side panel, the status bar and the pop-ups on them. Busy while a
computer turn plays and while a game loads. Blocked where the pointer can do nothing on the map or the battlefield (an unreachable tile, a
cell no stack can reach).

## Adventure map

All horses face **right**, in pure side profile, the same horse in the same colours on every cell (dark bay with a bronze bridle).

### 2 · Moving and visiting (2 × 3)

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_adv_move` | a horse galloping to the right, all four legs off the ground | `4,7` (under its hooves) |
| 2 | `cursor_adv_visit` | a horse rearing up, front hooves raised high, facing right | `4,7` |
| 3 | `cursor_adv_attack` | a horse's head and neck facing right, two crossed swords behind it | `4,4` |
| 4 | `cursor_adv_exchange` | two horse heads facing each other, a curved bronze double-headed arrow between them | `4,4` |
| 5 | `cursor_adv_enter-town` | a horse walking right towards a small stone gate arch, its rump at the left edge | `4,7` |
| 6 | `cursor_adv_spell` | a violet swirl of sparks around a small bright star at the centre | `4,4` |

### 3 · Selecting and digging (2 × 2)

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_adv_select-hero` | a plain steel knight's helmet in three-quarter view, a small bronze plume | `4,4` |
| 2 | `cursor_adv_select-town` | a small stone keep with one tower and a dark-red conical roof, seen from the front | `4,4` |
| 3 | `cursor_adv_dig` | a short iron shovel stuck in the ground at an angle, a little heap of earth beside it | `4,7` (the blade's tip) |
| 4 | — | spare: a second take of the exchange cursor | |

**Use on the adventure map** (for the chosen hero; the first that applies):

| Pointer over | Cursor |
|---|---|
| an own hero (not the chosen one) | `select-hero`; `exchange` when the chosen hero can reach it this turn or later |
| an own town | `enter-town` when the chosen hero can reach it; else `select-town` |
| an enemy hero, town or a guarded tile / neutral stack | `attack` |
| an object the hero can visit (a mine, a dwelling, a chest, a teleporter) | `visit` |
| an empty tile it can reach | `move`; the game draws the number of days in its corner from the 2nd day on (2, 3, 4+) |
| a tile it cannot reach | `ui_blocked` |
| a target while an adventure spell waits for one (Dimension Door, Town Portal's map pick, …) | `spell` |
| the tile under the hero while digging is offered (the Grail, ADR-140) | `dig` |

Without a chosen hero the map shows the pointer, `select-hero` and `select-town`. An ally's hero or town counts as an own one for
`select-*`, never for `exchange` or `enter-town`.

## Battle

### 4–5 · Melee attack, by side (2 × 2 each)

One sword, the same on all eight cells, a straight steel blade with a bronze cross-guard, **its point at the cell's centre** and the hilt
towards the side the attacker strikes from. Each cell names the **direction the sword points** (where the blow goes); the file names the
side the attacker stands on.

| Sheet | # | File | Sword | Hotspot |
|---|---|---|---|---|
| 4 | 1 | `cursor_bat_melee_from-n` | points straight down, hilt at the top | `4,4` (the point) |
| 4 | 2 | `cursor_bat_melee_from-ne` | points down-left, hilt at the top right | `4,4` |
| 4 | 3 | `cursor_bat_melee_from-e` | points left, hilt at the right | `4,4` |
| 4 | 4 | `cursor_bat_melee_from-se` | points up-left, hilt at the bottom right | `4,4` |
| 5 | 1 | `cursor_bat_melee_from-s` | points straight up, hilt at the bottom | `4,4` |
| 5 | 2 | `cursor_bat_melee_from-sw` | points up-right, hilt at the bottom left | `4,4` |
| 5 | 3 | `cursor_bat_melee_from-w` | points right, hilt at the left | `4,4` |
| 5 | 4 | `cursor_bat_melee_from-nw` | points down-right, hilt at the top left | `4,4` |

If the model cannot keep the eight swords alike, generate `from-n` and `from-ne` alone and rotate them by 90° steps (the light then turns
with them, which is acceptable at 32–64 px).

### 6 · Shooting, spells and machines (2 × 3)

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_bat_shoot` | a drawn short bow seen from the front, an arrow nocked and pointing up-left, **mirror-symmetric arms** | `0,0` (the arrow's tip, up-left) |
| 2 | `cursor_bat_shoot-weak` | the same arrow without the bow, snapped in the middle, the two halves slightly apart | `0,0` |
| 3 | `cursor_bat_spell` | a short wooden wand with a bronze tip pointing up-left, a small bright violet star at its tip | `0,0` (the star) |
| 4 | `cursor_bat_spell-area` | a round target ring of violet light with four short marks at the top, bottom, left and right, a spark at the centre | `4,4` |
| 5 | `cursor_bat_siege` | a round stone boulder flying into a broken piece of stone wall, chips flying | `4,4` |
| 6 | — | spare: a second take of the siege cursor | |

### 7 · Moving in battle (2 × 2)

| # | File | Subject | Hotspot |
|---|---|---|---|
| 1 | `cursor_bat_walk` | a worn leather boot in side profile, facing right, mid-step | `4,7` (the sole) |
| 2 | `cursor_bat_fly` | one spread feathered wing, white and bronze, rising to the right | `4,4` |
| 3 | `cursor_bat_teleport` | a violet ring of sparks around an empty dark centre, like a small portal seen from the front | `4,4` |
| 4 | — | spare: a second take of the wing | |

**Use in battle** (for the stack whose turn it is; the first that applies):

| Pointer over | Cursor |
|---|---|
| a target while a spell waits for one | `spell` (one target) or `spell-area` (an area spell: the cells it hits are highlighted) |
| an enemy stack the stack can shoot at | `shoot`; `shoot-weak` when the shot deals half or less (range, walls, obstacles: `BattleGrid.RangedPenaltyEighths` ≥ 4) |
| an enemy stack it can reach in melee | `melee_from-<side>` (below) |
| a wall section, the gate, the keep or a tower, on the siege engine's turn when the hero controls it (Ballistics, combat.md) | `siege` |
| a free cell it can reach | `walk`, `fly` or `teleport` by how the stack moves (combat.md's capabilities) |
| anything else on the field | `ui_blocked`; the battle bar and windows show the pointer |

**Melee direction** (the owner's note): over an enemy stack, the pointer's place in the hovered cell picks one of **eight sides**: the cell
is split into eight 45° sectors round its centre, and the sector the pointer is in names the neighbouring side the attacker strikes from
(north, north-east, east, …). For a 2 × 2 target the side is that of the target's whole footprint, and a corner sector of an edge cell
picks the diagonal. The sword cursor shows that side, and the cells the attacker would **stand on** (its whole footprint, 1 × 1 or 2 × 2)
are highlighted on the grid. If the attacker cannot stand on that side (blocked, out of reach, off the field) the nearest side it can reach
(by angle) is used instead; with none, the cursor is `ui_blocked`. A click attacks from the shown side. A shooter next to an enemy (which
must strike in melee) uses the same rule.
