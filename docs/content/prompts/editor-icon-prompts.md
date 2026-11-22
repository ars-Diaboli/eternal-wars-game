# Editor icon prompts (for an image-generating model)

Prompts for the **map editor and template editor icons**: the tool palette, the menus, the toolbar, the command palette, the panel
tabs, the tool options and the template editor's zones and connections. Like the game's icons ([icon-prompts.md](icon-prompts.md)) they
are generated as **sheets of 4 (2 × 2) or 6 (2 × 3)** and cut into one file per icon, and each prompt is ready to paste whole: nothing
is composed by hand.

## Use a prompt

1. Copy the sheet's **Prompt** block whole; it already holds the style, the grid and every cell.
2. Where the model takes a negative prompt, paste the negative block named on the Prompt line: `NEG-PAINTED` and `NEG-PLAIN` are in
   [icon-prompts.md](icon-prompts.md#negative-prompts).
3. Generate at the size on the Prompt line, cut the sheet into its cells and save each cell as its **File** (`<File>.png`).

Do not paraphrase the prompts: the wording keeps these sheets consistent with the game's icons.

## Shared wording

The prompts repeat, word for word, the style paragraphs of [icon-prompts.md](icon-prompts.md): "Game UI icon sheet of dark-fantasy
emblems…" (tier B, as sheets 15–18) and "Game UI button symbol sheet, flat front view…" (tier C, as sheets 19–22), and its grid sentences
("4 separate icons arranged in a grid…", "6 separate icons arranged in a grid…"). A change to one of them is made in both files.

## Output for every icon

- **Sheet**, **cutting**, **spare cell** and **light**: as in [icon-prompts.md](icon-prompts.md#output-for-every-icon).
- **File**: one PNG a cell, 32-bit RGBA, transparent background, scaled to 128 × 128 like the other files of
  `src/Godot/EternalWars.Godot.Assets/textures/ui/icons/`, named as in the tables (`tool_terrain.png`). The names are the ones
  `UiIcons` loads (snake_case, no prefix for plain commands), so a file goes in the folder as it is cut.
- **Size**: the editor shows these at 16 px (tabs), 18 px (menus and buttons) and 26 px (toolbar and tool palette): one clear shape,
  no line thinner than 6 % of the cell, no text, letters or numbers.

## Already drawn

These editor actions use icons that exist, and get no new art: Open (`load`), Save (`save`, also the template's Save), Undo and Redo
(`undo`, `redo`), Preferences (`settings`), Bans (`ban`), Switch level (`switch_level`), Hero tool (`hero`), Add and Remove
(`plus`, `minus`), Close and OK (`close`, `confirm`), Zoom (`zoom_in`, `zoom_out`), a layer locked or unlocked (`lock`, `unlock`), Move
up and down (`arrow_up`, `arrow_down`), the template's Monsters guard (`tool_monster`, below).

## Still to draw

The editor already asks for these files and shows a lettered placeholder until each exists: sheets **C4** (view), **C7** (map
settings), **C12** (panels of the side bars) and **C13** (export and workspaces).

## Tier B · Emblems: tools and template parts

Painted emblems like the adventure map's action icons (sheets 15–18 of [icon-prompts.md](icon-prompts.md)): every tool and template
part has its own object, so the tool palette reads by shape at 26 px.

### E1 · Brush tools (2 × 3): the tool palette, the Tools menu

**Prompt** · 1536 × 1024 · negative `NEG-PAINTED`

```text
Game UI icon sheet of dark-fantasy emblems in a realistic painterly style, flat front view, each emblem one strong symbolic shape made
as a real object: hammered and engraved metal, carved stone or bone, aged enamel or worn cloth, with true material texture, small dents
and wear. Two or three muted colours plus a soft glow where named, the glow lighting the surface around it naturally. Light from the
top left, the shape set apart by value contrast and a faint dark edge, no drawn outline. Not a photograph, not a plastic CGI render, no
pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own equal square cell, centred with about 10
% empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a wide
flat paint brush with a worn wooden handle, its bristles leaving one short thick stroke that is green grass at one end and brown earth
at the other; 2 a short stretch of cobbled road seen from above, curving from the lower left to the upper right, grey stones with dark
joints and worn edges; 3 a narrow winding stream of deep-blue water seen from above, curving from top to bottom between two thin strips
of brown bank, a faint pale glint on the water; 4 a single grey rocky hill seen from the side, one small gold arrow pointing up above
its peak and one small gold arrow pointing down beside its foot; 5 a small pennant on a short pole planted in the middle of a ring of
six grey standing stones seen slightly from above, the pennant plain faded red; 6 an open hand seen from the side, palm down, letting
fall a fan of small objects: two tiny pine trees, three pebbles and a small bush, spreading as they fall
```

| # | File | Used for |
|---|---|---|
| 1 | `tool_terrain` | Terrain (T) |
| 2 | `tool_road` | Road |
| 3 | `tool_river` | River |
| 4 | `tool_height` | Height |
| 5 | `tool_start_zone` | Start zone |
| 6 | `tool_scatter` | Scatter |

### E2 · Placing tools (2 × 3): the tool palette, the Tools menu

**Prompt** · 1536 × 1024 · negative `NEG-PAINTED`

```text
Game UI icon sheet of dark-fantasy emblems in a realistic painterly style, flat front view, each emblem one strong symbolic shape made
as a real object: hammered and engraved metal, carved stone or bone, aged enamel or worn cloth, with true material texture, small dents
and wear. Two or three muted colours plus a soft glow where named, the glow lighting the surface around it naturally. Light from the
top left, the shape set apart by value contrast and a faint dark edge, no drawn outline. Not a photograph, not a plastic CGI render, no
pixel art. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its own equal square cell, centred with about 10
% empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a small
square stone watchtower with a dark doorway and a pointed slate roof, standing on a square base of flagstones; 2 a horned beast skull
seen from the front, yellowed bone with cracks, long fangs in the upper jaw, a faint red glow deep in the eye sockets; 3 a plain golden
chalice on a short stem, worn and dented, a soft warm golden glow rising from its rim; 4 two small stone archways side by side, each
with a faint violet glow inside, joined above by one thin glowing violet arc from the top of one to the top of the other; 5 a heavy
wax-seal stamp with a turned wooden handle, pressed upright onto a small round disc of dark-red wax; 6 a pair of brass dividers
standing open in an upside-down V, two sharp steel points at the bottom, a round hinge at the top
```

| # | File | Used for |
|---|---|---|
| 1 | `tool_object` | Object (O) |
| 2 | `tool_monster` | Monster; the template's Monsters guard |
| 3 | `tool_grail` | Grail |
| 4 | `tool_link_gates` | Link gates |
| 5 | `tool_stamp` | Paste, stamps |
| 6 | `tool_measure` | Measure (U) |

### E3 · Selecting tools (2 × 2): the tool palette, the Tools menu, the terrain brush's Fill option

**Prompt** · 1024 × 1024 · negative `NEG-PAINTED`

```text
Game UI icon sheet of dark-fantasy emblems in a realistic painterly style, flat front view, each emblem one strong symbolic shape made
as a real object: hammered and engraved metal, carved stone or bone, aged enamel or worn cloth, with true material texture, small dents
and wear. Two or three muted colours plus a soft glow where named, the glow lighting the surface around it naturally. Light from the
top left, the shape set apart by value contrast and a faint dark edge, no drawn outline. Not a photograph, not a plastic CGI render, no
pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10
% empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a steel
gauntlet seen from the side with its index finger pointing to the upper left, the other fingers curled; 2 a steel gauntlet seen from
the front closed in a grip, four short gold arrows around it pointing up, down, left and right; 3 a short straw broom with a dark
wooden handle held at a slant, sweeping a few small pebbles and splinters off to the lower right; 4 a wooden bucket tipped on its side,
a broad flow of green grass spilling out of it and spreading flat in front
```

| # | File | Used for |
|---|---|---|
| 1 | `tool_select` | Select (V) |
| 2 | `tool_move` | Move (M) |
| 3 | `tool_erase` | Erase (E) |
| 4 | `tool_fill` | the terrain brush's Fill option |

### E4 · Template zones (2 × 2): the template editor's Add buttons and zone cards

**Prompt** · 1024 × 1024 · negative `NEG-PAINTED`

```text
Game UI icon sheet of dark-fantasy emblems in a realistic painterly style, flat front view, each emblem one strong symbolic shape made
as a real object: hammered and engraved metal, carved stone or bone, aged enamel or worn cloth, with true material texture, small dents
and wear. Two or three muted colours plus a soft glow where named, the glow lighting the surface around it naturally. Light from the
top left, the shape set apart by value contrast and a faint dark edge, no drawn outline. Not a photograph, not a plastic CGI render, no
pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10
% empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a tall
cloth banner hanging from a crossbar on a pole, faded red cloth with a small gold crown sewn on it; 2 the same tall cloth banner on a
pole, faded grey cloth with a small brass cogwheel sewn on it; 3 a small open wooden chest with iron bands, heaped with gold coins and
one red gem, a soft golden glow over the coins; 4 a weathered wooden signpost on a short post with three arms pointing left, right and
up, no writing on the arms
```

| # | File | Used for |
|---|---|---|
| 1 | `zone_start` | + Start (a player's start zone) |
| 2 | `zone_ai_start` | + AI start |
| 3 | `zone_treasure` | + Treasure |
| 4 | `zone_junction` | + Junction |

### E5 · Template connections (2 × 2): the template editor's connection inspector and graph

**Prompt** · 1024 × 1024 · negative `NEG-PAINTED`

```text
Game UI icon sheet of dark-fantasy emblems in a realistic painterly style, flat front view, each emblem one strong symbolic shape made
as a real object: hammered and engraved metal, carved stone or bone, aged enamel or worn cloth, with true material texture, small dents
and wear. Two or three muted colours plus a soft glow where named, the glow lighting the surface around it naturally. Light from the
top left, the shape set apart by value contrast and a faint dark edge, no drawn outline. Not a photograph, not a plastic CGI render, no
pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10
% empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a narrow
dirt path seen from above running straight from the lower left to the upper right, a few grass tufts along its edges; 2 a broad paved
road of large flat grey slabs seen from above running from the lower left to the upper right, twice as wide as a footpath; 3 a stone
archway seen from the front with an iron portcullis half lowered in it; 4 a ring of carved grey stone standing upright, filled with a
swirling deep-blue glow
```

| # | File | Used for |
|---|---|---|
| 1 | `connection_normal` | Normal |
| 2 | `connection_wide` | Wide |
| 3 | `connection_gate` | Gate |
| 4 | `connection_portal` | Portal |

### E6 · Template guards (2 × 2): the template editor's connection inspector (the Monsters guard uses `tool_monster`)

**Prompt** · 1024 × 1024 · negative `NEG-PAINTED`

```text
Game UI icon sheet of dark-fantasy emblems in a realistic painterly style, flat front view, each emblem one strong symbolic shape made
as a real object: hammered and engraved metal, carved stone or bone, aged enamel or worn cloth, with true material texture, small dents
and wear. Two or three muted colours plus a soft glow where named, the glow lighting the surface around it naturally. Light from the
top left, the shape set apart by value contrast and a faint dark edge, no drawn outline. Not a photograph, not a plastic CGI render, no
pixel art. 4 separate icons arranged in a grid of 2 columns and 2 rows, each alone in its own equal square cell, centred with about 10
% empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames,
no text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row: 1 a small
round stone guard tower with crenellations, three spear points rising above the battlements; 2 a squat stone pillar seen from the front
with a single coloured keystone set in its face, the keystone faded blue; 3 a closed stone gate seen from the front, two heavy wooden
doors under a lintel, a faded blue keystone set in the middle of the lintel; 4 a second take of icon 3, the same subject drawn again
```

| # | File | Used for |
|---|---|---|
| 1 | `guard_garrison` | Garrison |
| 2 | `guard_border` | Border guard |
| 3 | `guard_border_gate` | Border gate |
| 4 | (spare) | a second take; keep the better one |

## Tier C · Plain: commands and options

Light-grey symbols like the buttons of sheets 19–22 of [icon-prompts.md](icon-prompts.md); the theme tints them.

### C1 · Clipboard (2 × 3): the Edit menu, the command palette, context menus

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a pair of open scissors, the blades pointing up, two round finger
rings at the bottom; 2 two identical blank sheets, one behind the other, offset down and to the right; 3 a clipboard: a tall board with
a clip at the top holding a blank sheet; 4 two identical squares, one behind the other, offset down and to the right, a small plus sign
in the front square; 5 a waste bin with a flat lid lifted slightly above it, three vertical grooves on the bin; 6 a square drawn with a
dashed line, a small solid square filling its middle
```

| # | File | Used for |
|---|---|---|
| 1 | `cut` | Cut (Ctrl+X) |
| 2 | `copy` | Copy (Ctrl+C) |
| 3 | `paste` | Paste (Ctrl+V) |
| 4 | `duplicate` | Duplicate (Ctrl+D) |
| 5 | `delete` | Delete (Del) |
| 6 | `select_all` | Select all (Ctrl+A) |

### C2 · Selection and shape (2 × 3): the Edit and Map menus, the inspector's event links

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a square drawn with a dashed line, an X of two short strokes inside
it; 2 a thick arrow bent almost into a full circle, running clockwise, its head at the top; 3 two equal triangles pointing away from
each other, one on each side of a vertical dashed line; 4 two thick L-shaped corner marks, one at the top left and one at the bottom
right, overlapping to frame a square; 5 a square with a thick two-headed arrow running diagonally from its lower-left corner out past
its upper-right corner; 6 two oval chain links joined at a slant
```

| # | File | Used for |
|---|---|---|
| 1 | `deselect` | Deselect (Esc) |
| 2 | `rotate` | Rotate scenery (Ctrl+R) |
| 3 | `mirror` | Mirror scenery (Ctrl+M) |
| 4 | `crop` | Crop to selection |
| 5 | `resize` | Resize map |
| 6 | `link` | the inspector's New and Edit event links |

### C3 · File and application (2 × 3): the File, Test and Help menus, the toolbar

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a blank sheet with its top-right corner folded down, a plus sign in
its middle; 2 a closed book seen from the front with a quill pen laid across it at a slant; 3 a door frame with the door open, a thick
arrow pointing out through it to the right; 4 a solid triangle pointing right; 5 a bold question-mark symbol inside a circle; 6 a wide
rectangle with a small chevron pointing right at its left end and a short horizontal bar after it
```

| # | File | Used for |
|---|---|---|
| 1 | `new` | New map (Ctrl+N), the template's New |
| 2 | `save_as` | Save as (Ctrl+Shift+S) |
| 3 | `quit` | Quit (Ctrl+Q) |
| 4 | `play` | Test the map (F5) |
| 5 | `help` | Help (F1) |
| 6 | `command_palette` | Command palette (Ctrl+Shift+P) |

### C4 · View (2 × 3): the View menu, the toolbar, the status bar

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a square divided into nine equal smaller squares by two vertical and
two horizontal lines; 2 a flat square seen from straight above, a thick arrow pointing straight down at it from above; 3 a circle with
four short ticks pointing inward at top, bottom, left and right, a small dot in its centre; 4 a small square in the middle of a large
square, four short arrows pointing from the small square's corners to the large square's corners; 5 a map pin: an upside-down teardrop
with a round hole near its top; 6 a tall ribbon bookmark with a V-shaped notch cut into its bottom end
```

| # | File | Used for |
|---|---|---|
| 1 | `grid` | Grid |
| 2 | `top_down` | Top-down view |
| 3 | `focus` | Focus the selection (F) |
| 4 | `whole_map` | Whole map (Home) |
| 5 | `go_to` | Go to (Ctrl+G) |
| 6 | `bookmark` | Bookmarks, store and go to a bookmark |

### C5 · Panels and visibility (2 × 3): the View menu, the toolbar, the layers panel

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a window rectangle with a thick frame, its left third filled solid; 2
the same window rectangle, its bottom third filled solid; 3 the same window rectangle, its right third filled solid; 4 the same window
rectangle, empty inside, four short arrows pointing out from its corners; 5 an open eye: an almond shape with a round pupil; 6 the same
eye closed: a downward curve with four short lashes, one diagonal stroke across it
```

| # | File | Used for |
|---|---|---|
| 1 | `panel_left` | Left panel |
| 2 | `panel_bottom` | Bottom panel |
| 3 | `panel_right` | Right panel |
| 4 | `panels_hide` | Hide all panels (F11) |
| 5 | `eye` | a layer shown |
| 6 | `eye_off` | a layer hidden |

### C6 · Panel tabs (2 × 3): the right and bottom panels' tabs, the View menu

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a sheet with three rows, each a small square on the left and a short
bar on the right; 2 three flat diamond-shaped slabs stacked one above another with gaps between; 3 a small pennant on a short pole with
a five-pointed star on the cloth; 4 a round clock face with two hands, a thick arrow curving backward around its left side; 5 a
rectangle holding four horizontal bars of different lengths, aligned to the left, like lines of a log; 6 a funnel: a wide top narrowing
to a short spout
```

| # | File | Used for |
|---|---|---|
| 1 | `inspector` | Inspector |
| 2 | `layers` | Layers |
| 3 | `events` | Events |
| 4 | `history` | History |
| 5 | `output` | Output |
| 6 | `filter` | the problem and selection filters |

### C7 · Map settings (2 × 3): the Map menu, the map settings window's tabs

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a map folded into three upright panels in a zigzag, seen from the
front; 2 two simple head-and-shoulders silhouettes side by side, the left one slightly behind; 3 a trophy cup with two handles on a
short stem and a square base; 4 a calendar page with two rings on top and seven small squares in a row across its middle; 5 two
overlapping speech bubbles, the front one with a short tail to the lower left; 6 a quill pen at a slant, its tip touching a short
horizontal line
```

| # | File | Used for |
|---|---|---|
| 1 | `map` | Map settings (F7); Name, difficulty and arena |
| 2 | `players` | Players and teams |
| 3 | `conditions` | Victory and loss |
| 4 | `week_events` | Week events |
| 5 | `rumours` | Rumours |
| 6 | `texts` | Texts |

### C8 · Problems and favourites (2 × 2): the problems panel, the status bar, the object browser

**Prompt** · 1024 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 an octagon with a thick horizontal bar across its middle; 2 a triangle
standing on its base with a thick exclamation-mark symbol inside; 3 a solid five-pointed star; 4 the same five-pointed star as a hollow
outline
```

| # | File | Used for |
|---|---|---|
| 1 | `error` | Errors, the status bar's Problems button |
| 2 | `warning` | Warnings |
| 3 | `favourite` | a favourite (on), the Favourites header |
| 4 | `favourite_off` | a favourite (off) |

### C9 · Height and brush options (2 × 3): the Height tool's actions, the brush shape

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a low rounded mound with a thick arrow above it pointing up; 2 a
shallow rounded dip with a thick arrow above it pointing down into it; 3 a mound with its top cut off flat, a thick horizontal bar
lying on the flat top; 4 a jagged zigzag line on the left turning into a gentle wave on the right; 5 a solid circle; 6 a solid square
with sharp corners, as tall as the circle
```

| # | File | Used for |
|---|---|---|
| 1 | `height_raise` | Raise |
| 2 | `height_lower` | Lower |
| 3 | `height_level` | Level |
| 4 | `height_smooth` | Smooth |
| 5 | `brush_round` | Round brush |
| 6 | `brush_square` | Square brush |

### C10 · Generate and layout (2 × 3): the generate panel, the template editor's top bar and mirror setting

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 two dice, one in front of the other at a slant, the pips shown as
round holes; 2 one die seen at a slant, a thick arrow curving around it clockwise; 3 three circles at the corners of a triangle joined
by straight lines, a small square grid of four dots behind them; 4 a square split by a vertical dashed line, a solid triangle in the
left half and its mirror image in the right half; 5 a square split by a horizontal dashed line, a solid triangle in the top half and
its mirror image in the bottom half; 6 a square split by a dashed diagonal from the top left to the bottom right, a solid triangle in
one half and its mirror image in the other
```

| # | File | Used for |
|---|---|---|
| 1 | `generate` | Generate |
| 2 | `new_seed` | New seed |
| 3 | `arrange` | Arrange |
| 4 | `mirror_vertical` | Mirror: vertical |
| 5 | `mirror_horizontal` | Mirror: horizontal |
| 6 | `mirror_diagonal` | Mirror: diagonal |

### C11 · Monster strength (2 × 2): the template's zone and connection monster strength

**Prompt** · 1024 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 an empty ring; 2 one thick curved claw slash from the upper right to
the lower left; 3 two parallel thick curved claw slashes from the upper right to the lower left; 4 three parallel thick curved claw
slashes from the upper right to the lower left
```

| # | File | Used for |
|---|---|---|
| 1 | `strength_none` | None |
| 2 | `strength_weak` | Weak |
| 3 | `strength_average` | Average |
| 4 | `strength_strong` | Strong |

### C12 · Panels of the side bars (2 × 3): the side bars, the View and Edit menus

**Prompt** · 1536 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 6 separate icons arranged in a grid of 3 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a hammer and a small paintbrush crossed in an X; 2 a flat
square seen from above with a second square laid over half of it at an angle, its edge drawn as a dashed line; 3 a balance scale: a
short upright post with a level beam on top and a shallow pan hanging from each end; 4 a small square map with a smaller hollow rectangle
inside its upper left part; 5 a magnifying glass: a thick ring with a short straight handle to the lower right; 6 three thin books
standing upright side by side, the right one leaning on the others
```

| # | File | Used for |
|---|---|---|
| 1 | `tool_panel` | the Tools panel (the tools, their options and the object browser) |
| 2 | `overlays` | Overlays |
| 3 | `fairness` | Fairness |
| 4 | `minimap` | the minimap's heading |
| 5 | `find` | Find on the map (Ctrl+F) |
| 6 | `sample_maps` | the sample maps on the empty map's card |

### C13 · Export and workspaces (2 × 2): the File and View menus, the toolbar

**Prompt** · 1024 × 1024 · negative `NEG-PLAIN`

```text
Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
other colour, no glow, bold and simple, equal stroke weight on every symbol. 4 separate icons arranged in a grid of 2 columns and 2
rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a sheet of paper with its top right corner folded, three
short horizontal lines on it and a thick arrow pointing out of its right side; 2 a framed landscape picture, two mountain peaks and a
round sun, a thick arrow pointing out of its right side; 3 a square grid of four by four small squares seen from straight above; 4
three circles joined by two straight lines into a V, like a small graph of nodes
```

| # | File | Used for |
|---|---|---|
| 1 | `export_report` | Export report (Markdown) |
| 2 | `export_picture` | Export map picture (PNG) |
| 3 | `map_tab` | the Map tab |
| 4 | `template_tab` | the Template tab |

## After generating

1. Look at each sheet's icons side by side at 18 px and at 26 px: one clear shape each, the same scale and light across the sheet.
2. Put the tool palette's sixteen icons in one row at 26 px: no two share a silhouette.
3. Put the tier C icons next to `undo`, `save` and `settings` already in the folder: the same grey, bevel and outline weight.
4. Look for a cartoon look on the tier B sheets (thick outlines, flat fills, bubbly shapes, plastic shine) and regenerate such a sheet
   rather than fixing it by hand.
5. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/icons/` and add their lines to its `CREDITS.md` with the tool and
   its licence terms. A file shows in the editor once `UiIcon` has a value of the same name in Pascal case (`tool_terrain`: `ToolTerrain`;
   `UiIcons` loads the snake-case file of it) and the command, tab, tool (its `Icon`) or option is given it.
