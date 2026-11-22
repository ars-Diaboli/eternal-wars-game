# UI element prompts (for an image-generating model)

Prompts for the **interface pieces the game still draws as flat boxes**: window and panel frames, buttons, slots, bars, small controls
and the screen backgrounds. They replace the flat theme (dark slate panels with a bronze line, `UiTheme`) with painted pieces in the same
colours: **dark slate-blue grounds framed in worn bronze** (ADR-191). Icons are in [icon-prompts.md](icon-prompts.md), the main menu's
picture already exists.

## Use a prompt

Every group below has a **Use** line naming its blocks. A prompt is either one ready block, or a lead block and a subject from the
table, copied exactly and joined with a space:

```text
PROMPT   = UI-FRAME-LEAD + <subject>          (pieces made alone: frames, bars)
         = UI-SHEET-<sheet>                   (state sheets, one ready block per sheet)
         = UI-BACKGROUND + <subject>          (screen backgrounds)
NEGATIVE = UI-NEG   (only where the model takes a negative prompt)
OUTPUT   = the size in the table; save as <File>.png
```

A **piece made alone** is one element in the middle of a square picture: frames must come out symmetric, which a sheet spoils. A **state
sheet** puts the states of one control (normal, hover, pressed, disabled) on a 2 × 2 sheet so they match; its cells are in the table's
order. Do not paraphrase blocks or subjects.

## Blocks

**`UI-FRAME-LEAD`** (style and frame, first in every piece made alone):

```text
Game user interface element, hand-painted dark-fantasy style, surfaces of dark slate-blue stone and dark worn leather, framed in old
worn bronze with darker recesses and soft highlights on the raised edges, seen straight from the front, flat and orthographic, evenly
lit from the top left, crisp clean edges, restrained ornament. No text, no letters, no icons, no photo realism, no 3D render look, no
pixel art. A rectangular frame seen exactly from the front, mirror-symmetric left to right and top to bottom, the four corners
decorated identically, the straight edges plain and even along their whole length so they can be stretched, the inside plain and even
with no pattern, centred in the picture with an empty transparent margin around it.
```

**`UI-BACKGROUND`** (style, screen backgrounds):

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun.
```

**`UI-NEG`** (negative prompt, every piece):

```text
text, letters, numbers, icons, logos, perspective, tilted, asymmetric corners, uneven edges, pattern on the inside, bright saturated
colours, gold everywhere, gems everywhere, neon glow, cluttered ornament, drop shadow outside the element, photo, 3D render, pixel art,
another game's interface
```

## Output for every element

- **Pieces**: PNG, 32-bit RGBA, transparent around the element, at the size in the table; the element fills the picture but for its
  margin. The game draws frames as **nine-slice** textures (`StyleBoxTexture`): the **Margins** column says how many pixels of each
  edge are corner and border (left, top, right, bottom) at the master size *(proposal)*; everything inside them stretches, so it must be
  plain.
- **State sheets**: 2 × 2 at 1024 × 1024, cut into four 512 × 512 cells, then each cell **trimmed to the element** (`magick cell.png -trim
  +repage`); all four states of one control keep the same outline and size.
- **Colours**: ground about the theme's slate (RGB 28, 31, 38), frame bronze (140, 110, 61), its highlight (217, 173, 92). States: hover
  brighter bronze, pressed darker and inset, disabled grey and desaturated.
- **Backgrounds**: 2560 × 1440 (16 : 9), generated at the model's largest landscape size and upscaled; JPEG at quality 90. The game crops
  them to fit, never stretches them, and darkens them a little.
- **Names**: `ui_<group>_<id>.png` as in the tables; backgrounds `bg_<id>.jpg`.

## Frames and panels

**Use**: `UI-FRAME-LEAD` + subject · negative `UI-NEG` · size in the table

| File | Size | Margins | Subject |
|---|---|---|---|
| `ui_frame_window` | 1024 × 1024 | 48, 48, 48, 48 | an opaque window: a dark slate-blue stone panel inside a heavy bronze frame with small riveted corner plates |
| `ui_frame_overlay` | 1024 × 1024 | 40, 40, 40, 40 | a panel over the map: a thinner bronze frame around a dark slate-blue inside that is slightly see-through *(the game sets the alpha)* |
| `ui_frame_side-panel` | 512 × 1024 | 32, 64, 32, 64 | a tall narrow column frame of bronze with a cast ornament at the top and the bottom centre, plain long sides |
| `ui_frame_status-bar` | 2048 × 128 | 64, 24, 64, 24 | a long thin bar: a dark slate strip between two narrow bronze rails, a small bronze end cap at each end |
| `ui_frame_tooltip` | 512 × 512 | 16, 16, 16, 16 | a small information window: a very thin bronze line with tiny corner studs around a dark slate inside |
| `ui_frame_header` | 1024 × 256 | 96, 48, 96, 48 | a window's title plate: a wide bronze plate with a recessed dark slate field for a title, short scrolled ends |
| `ui_frame_divider` | 1024 × 64 | 64, 0, 64, 0 | a horizontal divider: a thin bronze line with a small diamond ornament at each end |
| `ui_frame_minimap` | 512 × 512 | 40, 40, 40, 40 | a square bronze frame for a map, heavier than the window's, a small round boss at each corner, an empty transparent inside |

## Buttons and controls

**Use**: `UI-SHEET-<sheet>` · negative `UI-NEG` · 1024 × 1024. Each sheet's cells are in the order of its **States** column; the files
add the state (`ui_button_text_normal.png`, ...).

| Sheet | File | Margins (trimmed) | States (cells 1–4) |
|---|---|---|---|
| B1 | `ui_button_text_<state>` | 24, 24, 24, 24 | `normal`, `hover`, `pressed`, `disabled` |
| B2 | `ui_button_icon_<state>` | 20, 20, 20, 20 | `normal`, `hover`, `pressed`, `disabled` |
| B3 | `ui_tab_<state>` | 24, 24, 24, 8 | `normal`, `hover`, `pressed`, `disabled` |
| B4 | `ui_check_<state>` | none | `off`, `on`, `radio-off`, `radio-on` |

**`UI-SHEET-B1`**:

```text
Game user interface element, hand-painted dark-fantasy style, surfaces of dark slate-blue stone and dark worn leather, framed in old
worn bronze with darker recesses and soft highlights on the raised edges, seen straight from the front, flat and orthographic, evenly
lit from the top left, crisp clean edges, restrained ornament. No text, no letters, no icons, no photo realism, no 3D render look, no
pixel art. 4 separate interface elements arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred
with about 10 % empty margin, all the same outline, size and light, each mirror-symmetric with identical corners and plain even edges;
nothing crosses into another cell. No grid lines, no text, no labels. Transparent background, PNG with alpha. The elements, top row
left to right, then bottom row: a wide rectangular button about three times as wide as tall, a dark slate face in a bronze rim; 1 at
rest; 2 the rim brighter and the face a little lighter; 3 the face darker and pressed in, the rim's shadow on top; 4 grey and
desaturated.
```

**`UI-SHEET-B2`**:

```text
Game user interface element, hand-painted dark-fantasy style, surfaces of dark slate-blue stone and dark worn leather, framed in old
worn bronze with darker recesses and soft highlights on the raised edges, seen straight from the front, flat and orthographic, evenly
lit from the top left, crisp clean edges, restrained ornament. No text, no letters, no icons, no photo realism, no 3D render look, no
pixel art. 4 separate interface elements arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred
with about 10 % empty margin, all the same outline, size and light, each mirror-symmetric with identical corners and plain even edges;
nothing crosses into another cell. No grid lines, no text, no labels. Transparent background, PNG with alpha. The elements, top row
left to right, then bottom row: a square button, a dark slate face in a bevelled bronze rim with small corner studs; 1 at rest; 2 the
rim brighter and the face a little lighter; 3 the face darker and pressed in, the rim's shadow on top; 4 grey and desaturated.
```

**`UI-SHEET-B3`**:

```text
Game user interface element, hand-painted dark-fantasy style, surfaces of dark slate-blue stone and dark worn leather, framed in old
worn bronze with darker recesses and soft highlights on the raised edges, seen straight from the front, flat and orthographic, evenly
lit from the top left, crisp clean edges, restrained ornament. No text, no letters, no icons, no photo realism, no 3D render look, no
pixel art. 4 separate interface elements arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred
with about 10 % empty margin, all the same outline, size and light, each mirror-symmetric with identical corners and plain even edges;
nothing crosses into another cell. No grid lines, no text, no labels. Transparent background, PNG with alpha. The elements, top row
left to right, then bottom row: a tab of a tabbed panel, a rectangle with the top corners cut, open at the bottom; 1 a quiet dark tab
at rest; 2 the same lit at the rim; 3 the selected tab, brighter and joined to the panel below; 4 grey disabled.
```

**`UI-SHEET-B4`**:

```text
Game user interface element, hand-painted dark-fantasy style, surfaces of dark slate-blue stone and dark worn leather, framed in old
worn bronze with darker recesses and soft highlights on the raised edges, seen straight from the front, flat and orthographic, evenly
lit from the top left, crisp clean edges, restrained ornament. No text, no letters, no icons, no photo realism, no 3D render look, no
pixel art. 4 separate interface elements arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred
with about 10 % empty margin, all the same outline, size and light, each mirror-symmetric with identical corners and plain even edges;
nothing crosses into another cell. No grid lines, no text, no labels. Transparent background, PNG with alpha. The elements, top row
left to right, then bottom row: 1 an empty small square box with a bronze rim; 2 the same box holding a bold bronze tick; 3 an empty
small round socket with a bronze rim; 4 the same socket holding a round bronze stud.
```

## Slots

**Use**: `UI-SHEET-<sheet>` · negative `UI-NEG` · 1024 × 1024. The inside of every slot is empty and transparent: the game draws the
creature, artifact or portrait in it.

| Sheet | File | Margins (trimmed) | States (cells 1–4) |
|---|---|---|---|
| L1 | `ui_slot_army_<state>` | 16, 16, 16, 16 | `empty`, `filled`, `selected`, `target` |
| L2 | `ui_slot_<id>` | 16, 16, 16, 16 | `artifact`, `portrait`, `backpack`, `spell` |
| L3 | `ui_frame_skill_<level>` | 16, 16, 16, 16 | `basic`, `advanced`, `expert`, `master` |

**`UI-SHEET-L1`**:

```text
Game user interface element, hand-painted dark-fantasy style, surfaces of dark slate-blue stone and dark worn leather, framed in old
worn bronze with darker recesses and soft highlights on the raised edges, seen straight from the front, flat and orthographic, evenly
lit from the top left, crisp clean edges, restrained ornament. No text, no letters, no icons, no photo realism, no 3D render look, no
pixel art. 4 separate interface elements arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred
with about 10 % empty margin, all the same outline, size and light, each mirror-symmetric with identical corners and plain even edges;
nothing crosses into another cell. No grid lines, no text, no labels. Transparent background, PNG with alpha. The elements, top row
left to right, then bottom row: a frame a little wider than tall around an empty dark inside; 1 a quiet dim bronze rim; 2 the normal
bronze rim; 3 the rim bright with a soft warm glow; 4 the rim glowing pale green (a valid drop).
```

**`UI-SHEET-L2`**:

```text
Game user interface element, hand-painted dark-fantasy style, surfaces of dark slate-blue stone and dark worn leather, framed in old
worn bronze with darker recesses and soft highlights on the raised edges, seen straight from the front, flat and orthographic, evenly
lit from the top left, crisp clean edges, restrained ornament. No text, no letters, no icons, no photo realism, no 3D render look, no
pixel art. 4 separate interface elements arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred
with about 10 % empty margin, all the same outline, size and light, each mirror-symmetric with identical corners and plain even edges;
nothing crosses into another cell. No grid lines, no text, no labels. Transparent background, PNG with alpha. The elements, top row
left to right, then bottom row: 1 a square slot with a round recess; 2 a portrait frame a little taller than wide with an arched top; 3
a smaller plain square slot; 4 a square slot with a thin ring of small runes engraved in the rim.
```

**`UI-SHEET-L3`**:

```text
Game user interface element, hand-painted dark-fantasy style, surfaces of dark slate-blue stone and dark worn leather, framed in old
worn bronze with darker recesses and soft highlights on the raised edges, seen straight from the front, flat and orthographic, evenly
lit from the top left, crisp clean edges, restrained ornament. No text, no letters, no icons, no photo realism, no 3D render look, no
pixel art. 4 separate interface elements arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred
with about 10 % empty margin, all the same outline, size and light, each mirror-symmetric with identical corners and plain even edges;
nothing crosses into another cell. No grid lines, no text, no labels. Transparent background, PNG with alpha. The elements, top row
left to right, then bottom row: one square frame for a skill icon in four materials, the same shape each time: 1 plain dark iron; 2
bronze; 3 polished silver with small studs; 4 old gold with a small gem at the top.
```

## Bars

**Use**: `UI-FRAME-LEAD` + subject · negative `UI-NEG` · size in the table. Each bar's frame and fill are separate files:
the game stretches the fill inside the frame.

| File | Size | Margins | Subject |
|---|---|---|---|
| `ui_bar_frame` | 1024 × 128 | 32, 16, 32, 16 | a long thin empty gauge: a bronze frame around an empty dark recessed track |
| `ui_bar_fill_health` | 1024 × 64 | 8, 8, 8, 8 | a long glossy bar of deep red liquid light, brighter along its top edge |
| `ui_bar_fill_mana` | 1024 × 64 | 8, 8, 8, 8 | a long glossy bar of deep blue liquid light, brighter along its top edge |
| `ui_bar_fill_experience` | 1024 × 64 | 8, 8, 8, 8 | a long glossy bar of warm gold light, brighter along its top edge |
| `ui_scroll_track` | 64 × 1024 | 16, 32, 16, 32 | a narrow vertical scroll track, a dark recessed groove between thin bronze rails |
| `ui_scroll_grabber` | 64 × 256 | 16, 24, 16, 24 | a narrow vertical bronze grip with three short grooves in its middle |
| `ui_slider_track` | 1024 × 64 | 24, 16, 24, 16 | a thin horizontal dark groove with a bronze rim |
| `ui_slider_knob` | 128 × 128 | none | a round bronze knob seen from above with a dark centre |

## Screen backgrounds

**Use**: `UI-BACKGROUND` + subject · negative `UI-NEG` · 2560 × 1440

| File | Screen | Subject |
|---|---|---|
| `bg_hero` | the hero screen | a war tent's interior at night, a worn map table, an armour stand and a weapon rack in the shadows, lantern light |
| `bg_kingdom` | the kingdom overview | a council chamber, a great table with a painted map of the realm, banners hanging in the dark |
| `bg_tavern` | the tavern | a smoky tavern's common room, long tables, a fire in a stone hearth, empty benches |
| `bg_marketplace` | the marketplace and trade windows | a covered market at dusk, crates, sacks and scales, lanterns on poles |
| `bg_loading` | loading a game | a road winding toward a distant ruined city under a dim sun, banners of many factions on the roadside |
| `bg_victory` | the victory screen | a battlefield at dawn, a lone banner standing on a hill, the sun breaking through clouds |
| `bg_defeat` | the defeat screen | a battlefield at dusk, a broken banner in the mud, smoke drifting, ravens in the sky |
| `bg_battle-result` | a battle's result window | a quiet field after a battle, scattered shields and spears, low mist |
| `bg_town_haven` | Haven's town screen | a lakeside town under snowy mountains, a grey castle with conical slate roofs on a rocky rise, a church with a gold sun |
| `bg_town_sylvan` | Sylvan's town screen | a forest town in a misty gorge among colossal living trees, a vast half-bare ancient tree, blue-teal haze, amber windows |
| `bg_town_academy` | Academy's town screen | a mage citadel on a frozen peak, verdigris domes, a great observatory with bronze rings |
| `bg_town_necropolis` | Necropolis' town screen | a dark keep and gothic manors on a grey dead plain under heavy cloud, a straight black obelisk spire with a cyan light |
| `bg_town_inferno` | Inferno's town screen | a fortress of black slag and fused rubble over rivers of lava, sickle-shaped spires, red smoke |
| `bg_town_dungeon` | Dungeon's town screen | a town in a vast enclosed cavern under stalactites, a gothic black citadel, a violet crystal spire, glowing mushrooms |
| `bg_town_stronghold` | Stronghold's town screen | an orc war camp on ochre badlands, log palisade, hide tents, a great beast skull over a lodge |
| `bg_town_swamp-bestiary` | Swamp Bestiary's town screen | half-sunken ruins in a dark pine forest, stilt huts, a carved totem of coiled beasts, cyan-glowing eggs |

Each town screen's background leaves the **lower two thirds calm**: the game places the buildings, the garrison and the panels over it.

## After generating

1. Stretch every frame to twice its width and height with its margins: the corners stay sharp, the edges show no repeat or smear.
2. Put the four states of each control side by side: same outline and size, each state clearly told apart at the button's real size.
3. Look at the backgrounds behind a panel: the panel and its text still read.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/frames/`, `ui/controls/` and `ui/backgrounds/` *(proposal)* and
   add a line to each folder's `CREDITS.md` with the tool and its licence terms.
