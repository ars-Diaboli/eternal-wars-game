# Presentation

> Part of the [game design](overview.md). Open items ❓: [open-questions.md](../../memory-bank/open-questions.md).

## Art direction
**Grim, dark fantasy** with **realistic lighting and PBR textures**, plus nods to the original (recognisable silhouettes for archetypal
creatures and buildings, heraldic player colours, iconic map objects).
- **Lighting**: physically based, low sun angles, volumetric fog, strong shadows; Godot Forward+ with SDFGI / VoxelGI or baked lightmaps, to
  be decided by a perf spike. Day / night tint per in-game day ❓.
- **Palette**: desaturated earth tones on screen; player colours and interactive objects stay saturated so the map reads in a dark
  scene. The ground textures themselves are saturated, each terrain with its own colour and structure, and the light, fog and grading
  make the mood (ADR-244).
- **Faction looks** (ADR-284): the owner's key paintings, one or more a town, set each faction's setting, light, landmark, buildings and
  colours; every faction asset follows them ([faction-art.md](../content/faction-art.md)). Shared: painterly realism from one camera,
  a low calm foreground, readable separate buildings, one focal point, warm light against cool shadow, magic glow only in small accents.
- **Materials**: PBR (albedo / normal / roughness), weathered and worn. **Post**: subtle grading, bloom on magic only, vignette.
- **Terrain** (ADR-011, ADR-183) *(placeholder textures, objects and units)*: a smooth heightmapped landscape; the terrains' textures
  run into each other where biomes meet, over about two thirds of a tile (ADR-212), along wandering lines that do not follow the tile
  grid (a single tile of another terrain is a patch with rounded, wandering edges); a texture repeats every 16 tiles, its repeat hidden
  by cells that each shift it and blend into each other the same way; cliffs are textured from the sides. Visual height must not contradict core tile data (cliffs = rock tiles).
- **Tile grid** (ADR-183): an overlay of thin lines on the tile edges, a setting on this computer: "Show the tile grid on the map" in the
  F10 menu's "Settings" tab, off by default; the map editor has its own "Grid" box, on by default.
- **Relief** (ADR-075, ADR-181, ADR-182, Warcraft III-like): looks only; no rule, the AI or a battle reads it, and the
  battlefield stays flat. Walkable ground lies between **0 and 4 cell widths** high, water below 0, impassable rock above 4 (4.5–6, the
  cliffs). The height is the map's own data, one value **per tile corner** in steps of 1/8 cell width, so tiles always meet and slopes are
  smooth; the [generator](random-maps.md#relief) sets it by terrain and the [map editor](tools.md#map-editor) reshapes it within the same
  limits. Heights by terrain *(placeholders)*: swamp, shallow water and ice 0–0.25; beach 0.5; sand 0.75; grass, dirt and cursed lands 1;
  forest floor and subterranean 1.25; wasteland 1.5; snow 2; rough 2.5; highlands 3; lava 3.25; rocky ground 3.5; deep water −0.5.
  Objects stand on ground levelled to their entrance's height; heroes walk over the slopes; roads, rivers and route markers lie on the
  ground; a click picks the tile under the cursor on any slope. Underground has the same limits and no
  drawn ceiling. Older maps get their terrains' heights, blended, when loaded.
- **Fog of war** (ADR-099, ADR-176, ADR-179): a smooth fade from visible to hidden over about two tiles, never a hard cell edge, so a revealed
  circle reads as a round clearing; a partly explored object shows its explored part.
  Two looks, a setting on this computer: **Dark** (default), a starry void (sparse stars, mostly white with orange to blue accents) with drifting nebulae under the map, in 3D parallax; **Clouds**,
  opaque drifting clouds floating above the level's highest ground. Revealed tiles open over 0.9 s as the hero's step plays; large reveals open as a wave from their middle.
  The map's edge is never a straight line: the ground goes on four tiles past it (its edge terrain and heights continued), and the fog
  swallows that ground along a wandering shoreline between about a third of a tile and two tiles out; the map's own tiles stay clear.
  Where nothing is drawn, only the fog shows. Without fog (the map editor, the replay's whole-map view) the map ends at its own edge, with the void (or clouds) around it.
  Rendering only: the rules keep cell-by-cell visibility.

## Camera and UI
- **Camera** (ADR-022): tilted **15–30° from vertical** (more tilted zoomed in), **rotatable ±45°**, zoom, pan.
- **UI**: a HoMM-style side panel on the right, or the left when mirrored (ADR-191, ADR-309, ADR-313): the minimap in a framed panel in
  the top corner, which takes the clicks on it, and the town list in its own panel below; the hero list at the other edge; the bottom row above the
  status bar; the resources on the top bar and the calendar (date, week's event; a tournament's countdown, "Battle in 5 days", ADR-366) on
  the status bar's right end (ADR-225, ADR-309);
  full-screen town and hero screens. The F10 menu has three tabs: "Save and load", "Settings" (auto combat, speeds, detailed tooltips, the fog's look, the tile grid, the window mode) and "Layout" (the battle's timeline, the adventure map's mirror, the UI scale, the panel
  opacity).
- **Bottom row** (ADR-309): above the status bar, framed panels of one height: from the edge away from the minimap the selected hero's
  card and its army strip, from the minimap's edge End turn and the action buttons (mirrored with the layout). With no own hero selected
  the card and the strip are hidden and the others keep their places.
- **Hero card** (ADR-309, ADR-310): the portrait as tall as the card, the same margin to its frame all round (a click opens the hero
  screen), two thin bars beside it (movement green, experience to the next level gold; the numbers in their tooltips), then three rows:
  the level and name ("Lv 3 | Orrin"); Attack, Defence, Spell power and Knowledge; morale and luck as icons with a signed value ("+2",
  "−1") and the mana left as an icon and a number (the maximum in its tooltip), then "Hero 1/1" when the player may have only one hero (a
  tournament's, ADR-366; why in its tooltip); the card 360 px wide. The army strip: always seven slots
  as tall as the row and two thirds as wide, empty ones dim; a stack shows the creature cut to 2:3 and the count in large type, its name on the status bar; a click opens the hero screen, a right click the creature's information window. A screen too
  narrow for the whole row cuts the army strip at its end towards the middle, fading out over the map; the pointer on it shows it whole,
  over the action buttons. Where the cut would leave the strip under half its width (ADR-315), the action buttons move up, just above
  the place they leave (beside the minimap's panel, towards the middle, where they would cover it), and the strip takes that place; a
  list whose foot would stand under them stops above them.
- **Action buttons** (ADR-210, ADR-309, ADR-316): two rows of four square icon buttons: Move (Space), spell book (C), kingdom (K),
  quests (Q); puzzle map (P), dig (G), marketplace (M), menu (F10). The hero screen (H, the hero card) and the level switch (L, the
  minimap's caption) have no button. No labels: each one's name and key
  come from one hotkey table, which the keys also follow, and show on the status bar ("Dig [G]"); a greyed button adds why ("Move [Space]
  · Click a cell to plan a route first"). Move walks the selected hero along the route shown on the map; it is greyed with no hero, no
  route of its or no movement left today. The spell book and dig act on the selected hero and are greyed without one (the spell book also
  when the hero has none); the marketplace without a town marketplace. In the replay viewer Move and the spell book, dig and marketplace
  buttons are greyed. The spell book is a window over the map: the hero's mana, its adventure spells to cast,
  its battle spells by school with their costs.
- **End turn** (ADR-204, ADR-309, ADR-310): a square hourglass tile as tall as the bottom row, with no label ("End turn [Z]" on the status
  bar) and no button frame inside the tile's own, ringed near its edge by the week's seven dots (the first at the top, then clockwise):
  the days gone by in the colour of the player whose HUD it is, today's in it with a soft glow, the days to come grey; the hourglass brightens under the pointer; greyed while it is not this screen's player's turn, left out in the
  replay viewer. Pressed (or Z) while a hero can still move, it asks first in the middle of the screen: "Mara and Aldric can still move.
  End the turn?", with End the turn and Cancel; Z or Enter answers it, and a new turn closes it.
- **Top bar** (ADR-309): the six resources (short amounts past 6 digits for gold and 4 for the others, the full one in the tooltip) in the
  top corner away from the minimap, within the status bar's 16:9 width, on a layer over the full-screen panels. The town, hero and exchange
  screens stop short of it, and while one is open it runs across that width.
- **Status bar** (ADR-186, ADR-204, ADR-309): a narrow stripe along the bottom of the adventure map, as wide as the screen up to 16:9 and a
  centred 16:9 width on a wider screen. The battle, town and hero screens and the screens before a game have the same stripe; windows over
  a screen use that screen's bar; the map editor keeps pop-up tooltips. On the left the subject: the icon and name of what the pointer is
  over (a HUD control's icon and label, a town building's name, the hero, object or ground under it on the map), then a divider and the
  detail: the subject's description while the pointer is over it (a control's tooltip, a building's state, a map object's wording by
  ownership: "Yours: +2 ore a day" against "Produces 2 ore a day when captured"), else the latest message; the hotkey hints are the
  first. On the adventure map the calendar is on the right (ADR-225). On the adventure map the menu is one of the action buttons; in battle the
  "Menu [F10]" button stands on the bar's right end, and before a game and in the replay viewer's battles in the bottom right corner. In
  the replay viewer the bar stands above the viewer's bar. What it says in battle is in the [battle screen](#battle-screen).
- **Icon buttons** (ADR-204): a button with an icon shows the icon alone on a square button, 48 px in bars and panels (End turn; the
  action buttons and the battle's action grid 36 px on square buttons), 32 px in rows, window headers and the replay viewer's bar (Play/Pause as one button showing what
  pressing it does, Next turn, Next day, Skip to the result, Play from here; ADR-309) (a window's or screen's
  close "×" always in its top-right corner, ADR-225); hovering it puts its label and hotkey on the
  status bar, with no pop-up tooltip. A button whose icon is missing keeps its text. The buttons along a window's bottom (OK, Accept,
  Start, Back, Close, Save, Load) keep their text after a small icon, as wide as the text, at the right of their row.
- **Turn banner and timer** (ADR-091, ADR-309): "<Player>'s turn" with the player's crest (its first town's faction) on the player's
  colour at the top centre. With one person at the game it shows for 3 s when that person's turn starts, and not in the computer's
  turns; in hot-seat and LAN games it stays while every turn lasts ("Everyone's turn" on a simultaneous day). The replay viewer has none.
  A timed game's clock is a pill just under it, over the map and the battle: the time left ("1:05"), white above a minute and red from
  it down, "0:30 stopped during the battle", "Time is up: the turn ends as soon as it can"; each warning ("1 minute left!") shows 3 s
  in a red ribbon under the pill.
- **Minimap** (ADR-309): in the map's proportions, unexplored cells black, other owned objects as cells in their owner's colour, towns
  as a little keep and heroes as a pennant in their owner's colour; the camera's frame drawn on it. Under it, on one line, the level
  caption ("Surface" or "Underground" with that level's icon; a click switches level; not on one-level maps), then Zoom in and Zoom out,
  which zoom the camera as the wheel does. A click on it centres the map there. The map fills its sunk slot up to the slot's rim
  (ADR-316); its longer side is 320 px, less on a low screen so that the minimap's panel takes at most a third of the screen's height
  (never under 140 px).
- **Hero list** (ADR-309): at the edge away from the minimap, down on the bottom row (above the hero card) and growing up to the top bar, one entry per hero and growing with them, no heading:
  the portrait (a lettered placeholder while a hero has none), then two thin upright bars, movement left (green) and mana (blue); the
  selected hero's entry framed in the player's colour. Hovering one puts the name on the status bar and a card beside the list, towards
  the middle: name and level, movement and mana as numbers, the four stats, the army's seven slots. A click selects the hero. More heroes than
  the edge holds scroll with the wheel. In a computer's turn the lists and the resources stay the person's, never the computer's.
- **Town list** (ADR-309, ADR-313): its own panel on the minimap's side, the hero list's twin: down on the bottom row (above End turn
  and the actions) and growing up to the minimap's panel, one entry per town and growing with them, no heading and no text; more towns
  than the room holds scroll with the wheel. An entry is the town's picture filling it, a wide painted view of its town type, walled or, without a fort, a village (the faction's crest while a picture is missing), the governor's portrait in the top corner and the build mark in the
  bottom one: a hammer on a stone, lit while the town can build today and dimmed once it has; a keep with a laurel wreath instead when
  nothing is left to build: every building at its last level and every dwelling line built out or closed by the 7-of-9 rule (ADR-030),
  the Grail building and a second Capitol not counted. The name and the build state go to the status bar on hover; a click centres the
  map on the town and opens its screen.
- **Route** (ADR-309): the route's markers are coloured by the day each step is reached (placeholders until the art pass: today's warm
  gold, the later days alternating pale steel blue and pale silver), with a "Day N" tag (the day counted from the game's start) at the
  start of the route's first two days and its last; the selected hero stands in a ring of its owner's colour. Over the last marker floats the mark of what the
  hero does there, as the cursor decides it: crossed swords (attack), an open hand (visit), a hand reaching for a coin (pick up a pile, a
  chest or an artifact), a town gate (enter an own town), a handshake (meet an own hero); a plain move shows none.
- **Heraldry** (ADR-211): each town type has a crest (its figure on a painted shield). It stands before the faction's name wherever the
  UI names one: the town list, the town and hero screens' titles, the kingdom overview, the tavern's cards, the new-game screen's
  factions, the leaderboard and the map editor's faction choices.
- **Cursors** (ADR-204, [cursor-prompts.md](../content/prompts/cursor-prompts.md)): the pointer shows what a click would do. An arrow on menus,
  screens and panels, an hourglass while the computer plays or a game loads; on the adventure map a horse (move, with the days from the 2nd
  on; visit; attack; exchange with a hero; enter a town), a helmet or a keep to select, a shovel to dig, a spell cursor for an adventure
  spell's target; in battle a sword (melee), a bow or a broken arrow (a shot dealing half or less), a wand or a ring (one-target or area
  spell), a boulder (the controlled siege engine), a boot, wing or portal (walk, fly, teleport), and a blocked sign. Over an enemy the
  pointer's place round the target's centre (eight 45° sectors) picks the side to strike from, one of eight; the sword points that way and the cells the
  attacker would stand on are highlighted; an unreachable side gives way to the nearest reachable one. Still images drawn at 64 px, shown at 43 px (the arrow, the hourglass and the
  blocked sign at 32 px); while an image is missing the nearest system cursor stands in (arrow, hand, cross, forbidden, busy). Controls show the arrow.
- **Pop-ups** (ADR-186, ADR-309, ADR-317): questions open in the middle of the screen as a card: a picture of the kind of question on top (one
  per kind, `q_<id>.png`; until it is painted a low slate box with its name; none for the battle), the question, one button per answer;
  more than four answers (a town gate's towns) are a scrolling list of rows (picture and name), the card's height capped. Notices are the
  same card with Continue, for what the player must not miss (ADR-317): a map event's or an object's message, the new week, a player
  defeated, a quest done or lost to another player, an artifact picked up. A card for a human whose turn it is not waits for its next
  turn; several show one after another. Routine outcomes (resources picked up, a site's ordinary reward, a mine or dwelling flagged, a
  town or hero lost) are only the status bar's line. The map (not the
  HUD) is dimmed behind a question, End turn's question and the LAN pause card. The pause card stands in the middle: "Game paused", why,
  and on the host "Computer plays <colour>" buttons; no Resume. The information window (right click) opens at the pointer.
- **Hover card** (ADR-309): resting the pointer on a map object or a hero for a moment shows a small card beside it: the name, the owner
  ("you", a player's name, "none") and what it gives ("+2 ore a day"); no picture, none for the bare ground, none over a HUD control or
  while the map is dimmed. The name and its description are on the status bar at once.
- **LAN chat** (ADR-194, ADR-309, ADR-313): over the map between the minimap's panel and the town list when there is room for it, else
  beside the minimap's panel towards the middle; over a battle, see the [battle screen](#battle-screen).
- **Window mode** (ADR-186): Windowed or Fullscreen (borderless, the screen's own size), in the "Settings" tab; Alt+Enter switches it, in
  the game and in the map editor. Kept in `settings.json`, applied at once and at start.
- **Layout** (ADR-118, ADR-156, ADR-309): the "Layout" tab sets the battle's timeline (in the bottom row, the default, or at the top) and the adventure map's HUD:
  the default arrangement or its mirror, left and right swapped. The change shows at once and is kept in `settings.json` with the speeds,
  not in saves. An older saved layout loads as the default or its mirror, whichever side its HUD stood on.
- **UI scale** (ADR-309): 100, 150 or 200 % of the game's UI, chosen on the "Layout" tab and applied at once and at start like the map
  editor's (the 2D parts drawn larger, the 3D views at the window's own pixels); kept in `settings.json` with the interface settings; a
  value outside the range is brought into it and onto the nearest of the three (the smaller of two as near). On a small window the scale
  is lowered, never below 100 %, so the UI keeps at least 960 by 540 of its own pixels (where the HUD fits whole): 1280×720 draws 200 %
  at 133 %.
- **Frames** (ADR-310, ADR-311): every frame of the game is drawn in code by one shader, not painted: the map's HUD (the column, the
  hero list, the bottom row, the top bar, the status bar, the turn banner and timer, the map's hover card), the screens and their
  windows, questions, notices and the pause card, dialogs, tooltips, the menu's panels, cards and tiles, lists and text fields, and every
  button. The look: a near-black face with a faint top-to-bottom shade, corners
  of 6 px, a thin grey rim lit from the upper left (bright where it faces the light and near the lit corners, fading and a little worn
  along the edges, near black on the far side), a dark line inside it and a soft drop shadow. Panels stand out with a slight bevel;
  slots (army stacks, the hero and town entries, the portrait, the minimap, a tile, a list, a text field) are sunk in; buttons bulge
  slightly, light up under the pointer, sink while held and dim when disabled. The selected hero's entry takes the player's colour on its
  rim with a glow, a town's entry its owner's colour on the rim, the turn banner its player's, a town's windows its faction's accent, a
  building tile its state's colour and a spell its school's. Windows, dialogs and tooltips are opaque; a screen's panel over the main
  menu's picture lets a little of it through. Each part keeps the content margins of the frame it replaced, so nothing moves.
- **Panel opacity** (ADR-310): 40, 60, 80 or 100 % (the default), chosen on the "Layout" tab, applied at once and at start, kept in
  `settings.json` with the interface settings; a value off the list goes to the nearest (the smaller of two as near). It is the
  opacity of the HUD frames' faces; their rims and shadows stay whole, and a button's face is never under 55 %.
- **Theme** (ADR-191, ADR-294, ADR-311): the frames above, and the controls the shader does not frame drawn flat to match: a
  near-black face and a thin grey rim with 6 px corners on tabs, drop-down menus, scroll bars, sliders, progress bars, check boxes and
  radio buttons; dividers a quiet grey line; a window's title over a divider. Every control the screens use has the look, so no default
  Godot control shows. High contrast keeps flat boxes in light frames. Icons and cursors are aluminium (ADR-312): pale satin metal with a
  fine grainy texture and worn edges, light grey to near-white in a thick dark outline, no other colour; only the faction crests keep their colours. Two faces: headings and titles in a carved
  capital face, the rest in a humanist sans, body text at least 16 px at 1080p; two pairings are proposed for the owner's pick (Cinzel with
  Alegreya Sans, the default, and Marcellus SC with Fira Sans), the licences beside them in `fonts/`. The map editor keeps the colours and
  Godot's font in its compact variant.
- **Army rows** (ADR-294): an army is a row of seven slots led by its hero's frame (empty without a hero), each stack shown by its
  portrait and count. Drag a stack onto a slot to move it, merge it with the same creature or swap it with another; **Shift** + drag asks
  how many to split off, **Ctrl** + drag moves one creature, **Alt** + click spreads the stack evenly over the empty slots (the stack keeps
  what does not divide), a double or right click opens its information. A click picks a stack and a click on another slot moves it there
  (Shift: split), for the keyboard and the controller. In a town, a stack the town can upgrade carries an arrow badge with the price in
  its tooltip, and a hero's frame dragged onto the other row swaps the heroes, moves the visitor in or leads the garrison hero out. The
  town, hero and exchange screens use the rows.
- **Counts and costs** (ADR-294): a count is chosen in a dialog with a slider, − and +, Max (what the treasury pays for), the cost of
  one, the total and the treasury after paying, short amounts in red (recruiting, splitting a stack). A cost shows what the player lacks
  in red, and its tooltip names the shortfall.
- **Spacing** (ADR-118, ADR-163): one set of margins and gaps for every panel:
  4 px between slots and the cells of a grid (army slots, paper doll, backpack), 6 between rows, 8 between controls side by side, 16
  above a section's heading (Army, Worn artifacts, Buildings, ...), 8 inside a panel, 16 inside a screen or window over the map, 8 from
  the window's edge to the HUD parts and between them.
- **Before a game** (ADR-163): the main menu, the new-game screen and the leaderboard stand on a full-window picture (cropped to fit, never
  stretched, a little darkened), in dark panels that let some of it through; the main menu is a column on its left. The game's "Menu [F10]"
  button shows only in a game (F10 still opens the menu). The F10 menu and the result screen are opaque, so the map does not show through.
- **Audio**: per-terrain ambient music, faction town themes.

<a id="battle-screen"></a>

## Battle screen
ADR-314, in the adventure HUD's look and frames (ADR-309, ADR-310, ADR-311); the report of its making:
[battle-hud-plan.md](../papers/battle-hud-plan.md). The field is the 3D [battlefield](combat.md#battlefield); the HUD stands over it, the status bar along the bottom as on the map. Each side's portrait rim, timeline rims, stack bases and stack panel
rim take its player's colour, neutrals bone grey. No mirror: the attacker stays on the left. A siege adds nothing to the HUD.
- **Side plates**: top left the attacker, top right the defender, mirrored: the hero's portrait rimmed in the side's colour (a side
  without a hero shows its strongest stack) and the side's name; a side with a caster adds its mana as the mana icon, a thin blue bar and
  "14 / 24". A click on the portrait opens the hero's information window: one's own hero in full, the opponent's as the Scouting of the
  viewer's hero in the battle shows it (ADR-125). The spell book's button stands beside the plate of the side the book is for.
- **Round plate**: top centre, "Round 3", or "Deployment" during the deployment step, as tall as the map's turn banner; a timed game's
  pill stands under it.
- **Bottom row**: above the status bar, three framed panels of one height in the adventure HUD's sizes, edges in line: the stack panel,
  the timeline and the action grid.
- **Stack panel**: bottom left, as wide as the hero card; the stack under the pointer (on the field or the timeline), else the active one.
  Its portrait as tall as the panel; the name and count, with defending, native terrain and the spells on the stack as small marks at the
  line's right (each named on the status bar); then icons with numbers in two rows of matching columns: damage, Attack, speed, morale and,
  for a shooter, shots; health, Defence, initiative, luck and, for a creature caster, its casts left. The numbers are the effective ones;
  a creature without morale shows "–" there, a war machine only the numbers it has. A missing icon leaves the number's short name.
- **Timeline**: in the middle of the bottom row, or under the round plate between the side plates when the Layout tab says top. Each
  entry is a slot like the army strip's, 2:3 and as tall as the panel: the stack's portrait filling it (the defender's mirrored to face
  its enemy, its name while it has no portrait), the count in the corner, rimmed in its side's colour. The active stack first with a
  bright rim, the stacks still to act this round, a bronze groove, then the waiting stacks dimmed; as many as fit the panel, from the
  front. The pointer on an entry lights the stack on the field and shows it in the stack panel; a right click opens its information
  window.
- **Action grid**: bottom right, 4 × 2 square icon buttons: Wait (W), Defend (D), Quick combat (Q), Combat settings; Retreat (R),
  Surrender, Spell book (C), Menu (F10). A creature caster's own spell adds a fifth column while it acts (its spell's icon, else the cast
  icon). No labels: the name, key and, when greyed, the reason go to the status bar on hover; Surrender has no key and shows its price
  there. Quick combat is off between two computers (ADR-195). In a tournament's battle Retreat and Surrender are greyed out, and the first
  round's prompt says so (ADR-366).
- **Spell book** (C): a window over the battle with the hero's battle spells by school, rank and cost, and the warcries it knows as a
  category of their own (ADR-067), each greyed with its reason (cast this round, not enough mana). A spell aimed at a side or the whole
  field is cast at once; any other is armed: the status bar asks for its target ("Teleport: click the stack, then where it goes (Esc
  cancels).") and a two-click spell for its second aim; Escape or another stack's turn drops it. Picking a warcry cries it. Warcries have no buttons of their own.
- **Combat settings**: a window over the battle with its view settings, kept in `settings.json` and changing no rule: the battle's
  animation speed, the grid over the field and the active stack's reach lit (both on by default; the deployment zone shows either way).
  A change shows at once.
- **Log**: above the action grid, three see-through lines of the latest events; the pointer on it opens it upwards to the battle's last
  20 lines, no higher than the screen's top quarter.
- **LAN chat**: at the bottom left above the stack panel.
- **Status bar**: the latest message is the prompt ("Swordsmen to act"), a refusal or, while the other computer plays, its last move.
  The pointer over the field names what a click would do: a move, a blow or a shot with its estimate ("Swordsmen · Attack the Wolves:
  38–45 damage, 1–2 die; they strike back"), a siege structure, an armed spell's aim with its estimate or why it cannot be cast there.
  The estimate is the lowest and highest damage of the blow, shot or damage spell without luck and the fewest and most creatures those
  kill; for a blow or a shot also whether the target answers: "they strike back", "they shoot back" (a shooter with ranged
  retaliation), or "no return blow" (a target with none left this round, an attacker that is not answered, a blow whose weakest roll
  kills the whole stack before it could answer). Morale, luck, a second blow and the return blow's own damage are left out.
- **Windows over the field**: while one is open the field takes no command and shows no preview; a click beside it or Escape closes
  it.
- **Keys**: the action grid's keys above; Enter starts the battle; Enter or Escape leaves the result and Necromancy windows; Escape
  also cancels a deployment drag.
- **Deployment**: the deploying player's columns tinted in their colour; no timeline; a wide Start battle in the middle of the bottom row,
  and the grid shows only Quick combat, Combat settings and Menu. A stack is placed by dragging it, or by a click on it and then on a
  cell; while carried its place shows under the pointer, over the HUD too.
- **Result and Necromancy**: the field dimmed above the status bar; a framed window titled "Victory" or "Defeat" for the player at this
  screen ("<Name> wins" when both or neither side play here), the reason under it, each side's losses as portraits with counts, the
  experience gained and Return to the map. Raised undead get their own window after it: the stack's portrait and count, Continue. Auto
  combat's question (ADR-090) takes the same look: the expected losses as portraits, Fight and Accept.
- **Narrow form**: below 1,400 px of UI width the stack panel keeps the portrait, name and count, damage, Attack, health and Defence in a
  narrower panel, without the marks, and the log keeps two lines; the UI scale is lowered as on the map.
- **Replay viewer**: the bottom row stands above the viewer's bar; the action grid keeps Combat settings, the spell book (greyed) and
  Menu.

<a id="town-screen"></a>
## Town screen
ADR-246, ADR-294, ADR-296, ADR-297, ADR-300, ADR-301, ADR-302, ADR-304; the plan and its 3D scene: [town-screen-plan.md](../papers/town-screen-plan.md).
- **Main screen**: the top bar has the town's crest and name (a click renames it), ◄ ► through the player's towns ([ and ] too), the faction,
  town hall and governor, the quick buttons of the windows and the close "×". The main area is the town's 3D scene where its town type
  has a layout (every playable town type has one), else the faction's key painting. In the scene each built building stands on its plot (a greybox block with
  a roof and its name on a sign until it has a model), in scaffolding on the day it goes up, and unbuilt plots are bare land; once the
  town has the Capitol its two closed dwelling plots hold citizens' homes. Streets and houses fill the town in three steps by the town
  hall's level (village, town, city); walls rise with the fort as the siege has them (the wall and gate, then the moat, then towers);
  banners fly in the owner's colour, and the garrison hero's banner on the keep. The pointer over a building outlines it in bronze and
  shows the hand, its name and state on the status bar (built or not, and what its next level waits for); a click opens its window (or
  its information, for one without a window) and a right click its information. Bare land the town can still build on picks too: it shows
  a ghost of its building, and a click opens the Hall with that building selected. ← and → (the controller's d-pad) walk the plots as a
  ring round the town, Enter (A) opens the one reached. The scene opens at the town's hero shot: Q and E turn it up to 25° either way,
  the wheel or + and − zoom in two steps; a building's window opens at once while the camera eases toward the building behind it, and
  back when the window closes. A new building closes the window and plays its construction scene (at instant speed neither): the camera eases to it and it rises
  through its scaffolding in dust (with the Capitol, the citizens' homes too), 1.5 s at normal speed, by the speed setting for the
  player's own heroes (none at instant); a click ends it. From the map the screen fades through black into the hero shot (0.5 s, by the
  same setting, none at instant), and the town's scene is loaded ahead, so it opens at once. The
  adventure map and its side panels are put away while the scene shows. Along the main area's bottom runs a strip of the town's buildings
  (a click opens a building's window, or the information of one without a window), and a notices card stands in its corner (built today,
  caravans coming, the governor's discount). The bottom bar has the town's
  crest with its governor, the garrison and visiting hero as [army rows](#camera-and-ui) with the swap or garrison button between them,
  Split, Upgrade all, Swap and Dismiss (Split and Dismiss act on the picked stack, Upgrade all and Dismiss ask first), the daily income,
  next Monday's growth (each line's parts in its tooltip: base, fort, artifacts, growth building, Grail; next week's event is drawn on
  Monday) and the date.
- **Windows**: one at a time over the dimmed main area, as large as it; Escape closes it, a second Escape leaves the town. B, R,
  G, V, M, S, F and C open the Hall, Recruitment, the Mage guild, the Tavern, the Marketplace, the Blacksmith, the Fort and the Caravan; U
  is Upgrade all, H the visiting hero's screen, Space swaps the heroes.
  - **Hall**: the buildings in columns by kind (Core, Military, Magic, Dwellings, Unique; the `kind` column of `buildings.csv`) as tiles
    with their state by frame and symbol, the requirements drawn as lines between them; hovering a tile lights its chain of requirements
    and dims the rest. The dwellings stand as the 7-of-9 block, a column per tier. A click selects a tile for the card below: the next
    level, its cost with what is lacking, the requirements as ✓ / ✗ buttons that select them, what it gives, the refusal, Build and
    **Show in town** (the Hall closes and a ghost of that level stands on its plot until Escape or a click); a dwelling that would close
    others draws them crossed through, with the warning on the card.
  - **Recruitment**: a creature card per line (unbuilt lines greyed), Recruit opening the count dialog with **Garrison** or the visiting
    hero (preselected to where the creature fits), and Recruit all with its summary. A dwelling of the strip opens its own window: the
    card, its building, recruiting base or upgraded, and building or upgrading the dwelling.
  - **Mage guild**: a shelf per level, dark with what it needs until built; the spell buildings' spells framed in their school's colour;
    the visiting hero's known spells marked, those learned on this visit marked apart, those it cannot learn greyed with the reason; the
    spell book is bought here. The war lodge shows the warcries by level.
  - **Tavern**: the hero cards (the returning heroes marked), the week's rumour and the Thieves' Guild; the tavern's strip button names the
    next hire. **Marketplace**: what you give and what you get as two columns of the six resources, the rate between them, a slider with
    Max and Trade; the Resource Silo's income; sending to an ally when the player has one. **Blacksmith**: a card per machine with its
    stats, price and Buy, greyed with the reason without a visiting hero. **Fort**: the walls, keep, towers, gate and moat of its level,
    the next level's gain, the garrison's strength and the siege bonus of the town's buildings. **Caravan**: the dwellings in reach, the
    resource stores and the queue. The town's crest opens the **governor list** (each hero with Stewardship and its discounts), the Undead
    Transformer's strip button its window.
- The 2D harness keeps the earlier one-screen town panel as a tool (TOWN-16).

## Information windows
ADR-118, ADR-154, ADR-178, ADR-190. A right click opens the window of what is under the pointer: on the map a hero, a stack, a town, an object or the ground;
in battle a stack; on the screens a stack, a skill, a spell, an artifact or a building; in the kingdom overview a row (what it pays,
then its town, mine, provider or hero) or the totals; in the tavern a hero for hire; on a decision prompt what it is about (the object
visited and what it offers, the stack, the hero) and each skill, town or stack to choose. It shows only what the player may know (Scouting,
ADR-125; objects under the fog as last seen). Held and let go it closes; a quick click leaves it open until the next click or key. On a
touch screen (ADR-190) a finger held still for half a second opens it and it stays open when lifted; a tap is a click, a moved finger a drag. The
"Detailed tooltips" setting (off by default, the F10 menu's "Settings" tab, kept with the speed settings) adds the formulas and the raw numbers.

## Speed settings
ADR-088, ADR-090, ADR-095. Player settings (`settings.json` in the user data folder, not in saves; they change nothing in the game) for own
heroes (every human player's), computer heroes (with "don't show") and battle animations, in the F10 menu (the battle's also in its combat settings window, ADR-314): slow (half), normal, fast
(double), instant. Heroes walk with a small hop. A computer turn plays back hero by hero where the player can see, the camera following;
the next turn waits for it.
