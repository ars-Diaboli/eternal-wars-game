# Adventure HUD redesign: guide and plan

The plan to rebuild the adventure map's HUD from the mockups decided on 2026-11-24 (the decision table is the Claude doc
[Adventure HUD redesign: discrepancies and decisions](https://claude.ai/artifact/C6tLszBWFkrV3VsMh3dqeH); its row numbers, such as 3.7, are
cited below). This file has three parts. The **guide** says what the new HUD is and how each part behaves. The **plan** has six steps from
the ADR to the painted pieces. The **mockup prompts** produce the next round of pictures: the adventure HUD with every decision applied, and
the battle HUD in the same style. ADR-309 records it; the roadmap tracks the steps, and the plan's ticks say what is built. When every step is built, this file stays in
`papers/` as the report and the rules move into [presentation.md](../design/presentation.md).

## Guide

### Screen at a glance

The default arrangement; the mirrored one swaps the left and right columns. The status bar carries every name, hotkey and reason, so the
buttons need no labels.

```text
+---------------------------------------------------------------------------------+
| [Resources (6)]            [ Turn banner ]                 [ Minimap          ] |
|                              [Timer pill]                  [ camera frame     ] |
|                                                            [Level caption][+][-]|
| +------+                                                                        |
| |[sel] |                    3D adventure map                         +-------+  |
| |Heroes|                                                             | Towns |  |
| |grows |          (o) . . . . .  Day 5                               |4 slots|  |
| |      |                         . . . . . O  Day 6                  |+scroll|  |
| +------+                                                             +-------+  |
|                                                 Actions 2x5         [Chat LAN]  |
| +-------------------+ +---------------------+ +-----------------+ +----------+  |
| | Hero card         | | Army strip          | | >  .  .  .  .   | | End turn |  |
| | portrait, 3 bars, | | 7 slots, empty dim  | |                 | | oooo*oo  |  |
| | stats, morale/luck| |                     | | .  .  .  .  .   | | Z        |  |
| +-------------------+ +---------------------+ +-----------------+ +----------+  |
| Status bar: subject . detail (names, hotkeys, reasons)                 Calendar |
+---------------------------------------------------------------------------------+
```

### Rules for every part

- **Player colour.** Every selection, accent and highlight takes the colour of the player whose HUD it is: the selection ring on the map,
  the selected list entry, the hourglass's day dots, the turn banner. The frames stay bronze on dark slate (ADR-191).
- **Icons, no labels.** Buttons show an icon only. The name and the hotkey show on the status bar on hover ("Dig [G]"), as icon-only
  buttons do today (ADR-204).
- **Disabled.** A button that cannot act now is greyed; hovering it puts its name and the reason on the status bar ("Marketplace · Build a
  marketplace in one of your towns"). No pop-over lists reasons.
- **Names on hover.** A town's, a hero's or a map object's name shows on the status bar on hover; lists show pictures, not names.
- **No hero selected.** The hero card and the army strip are hidden; every other part keeps its place.
- **One height along the bottom.** The hero card, the army strip, the action grid and End turn are panels of the same height, top and
  bottom edges in line.
- **Replay viewer.** Everything that would issue a command or open a hero's screens is disabled; End turn is hidden (ADR-164).

### The parts

**Top bar: resources**
- Top left: the six resources, icon and amount, large amounts shortened (ADR-186), the full amount in the tooltip.
- Stays over the town, hero and other full-screen panels, as the status bar does today (ADR-204).

**Turn banner and turn timer**
- Banner "<Player>'s turn" with the player's crest, top centre. Single player: shown for a few seconds when a turn starts. Hot-seat and
  LAN: stays while the turn lasts, and the timer pill sits just under it.
- Timer (timed games, ADR-091): a pill with the time left at the top centre, white above 60 s, red from 60 s down. Warnings at 60, 30, 15
  and 5 s, today's wording ("1 minute left!"), each shown 3 s in a red ribbon under the pill.
- The pill also shows "stopped during the battle" and "Time is up: the turn ends as soon as it can". The clock keeps running while a
  question is open (ADR-091 gives each question its default).

**Minimap**
- Top right, a rectangle in the map's proportions, unexplored cells black.
- Towns and heroes as small pictograms; other owned objects as cells in their owner's colour.
- The camera's frame drawn on it (the map editor's view frame, ADR-261).
- Under it, on one line: the level caption, then the zoom-in and zoom-out buttons. They zoom the camera, as the wheel does. No centre
  button: picking a hero or a town in a list centres the map on it.
- The caption shows "Surface" or "Underground" with that level's icon on two-level maps; clicking it switches level. On one-level maps
  only the zoom buttons show.

**Hero list**
- Left edge, one entry per hero, growing with the heroes (today's behaviour); no heading.
- An entry: the portrait, then two thin upright bars, green for movement left and blue for mana.
- Hover: a card beside the list with the name, movement and mana as numbers, the four stats and the army; the selected hero's card does
  not move.
- Click: selects the hero and centres the map on it.

**Town list**
- Right edge, four slots, scroll arrows above and below when there are more towns; narrow entries (about 130 px); no heading.
- An entry: the town's picture, the governor's portrait without a name (none: no portrait), and a build mark: lit hammer = can build
  today, dim = built today, a separate mark = fully built: every building of the town's faction at its highest level, counting 7 of the 9 creature
  dwellings when the map uses the 7-of-9 rule (ADR-030, set in the editor), all 9 without it.
- Name on the status bar on hover; no hover card. Click: centres the map and opens the town screen.

**Selected-hero card and army strip**
- Bottom left. Portrait (click: hero screen), level, name, Attack, Defence, Spell power, Knowledge.
- Three thin bars beside the portrait: movement (green), mana (blue), experience to the next level (gold).
- Morale and luck as icons with a signed value (+2, −1).
- Army strip: always 7 slots, empty ones dim; click a stack for the hero screen, right click for its information window.

**Action buttons**
- Bottom right, beside End turn: a 2×5 grid of buttons wider than tall: Move (»), Spellbook, Kingdom, Quests, Puzzle map / Dig,
  Marketplace, Switch level, Hero screen, Menu (amends ADR-210). No Next hero button: Tab stays.
- Move walks the selected hero along its route; disabled with no route or no movement left.

**End turn**
- An hourglass tile, no label; 7 dots for the days of the week, today's lit in the player's colour.
- Greyed while it is not this player's turn. Hotkey Z.
- When a hero can still move, it asks first: "Mara and Aldric can still move. End the turn?"

**Route on the map**
- Markers coloured by the day each step is reached, in placeholder colours until the art pass: today's steps warm gold (RGB 217, 173, 92, the
  frames' bronze highlight), later days alternating pale steel blue (140, 170, 200) and pale silver (200, 200, 205); a tag "Day N" at the start of the first two days and the last; a
  bigger marker at the end, rimmed in the player's colour, holding the icon of what the hero does there: attack, visit, pick up, enter a
  town, meet an own hero (none for a plain move; icon sheet 32).
- The selection ring under the selected hero, in the player's colour.

**Status bar**
- Bottom, capped at a centred 16:9. Left: the subject (icon + name of what the pointer is over), a divider, the detail. Right: the
  calendar.
- The detail is the latest message, else the subject's description; the hotkey hints stay the first text, until something else replaces
  it.
- Descriptions depend on ownership: "Yours: +2 ore a day" against "Produces 2 ore a day when captured".

**Hover card and information window**
- Hovering a map object shows a small card beside the pointer: name, owner, what it gives. No picture.
- Right click still opens the full information window (ADR-118, ADR-154), in the same frame.

**Pop-ups**
- Question card: a picture, the question, one button per choice (mockup E). One picture per kind of question
  ([question-picture-prompts.md](../content/prompts/question-picture-prompts.md)). Up to four answers are buttons; more (a town gate's
  towns) are a scrolling list of rows (proposal). Notices in the same card with Continue.
- The map behind questions and the pause card is dimmed.
- Pause card (LAN): "Game paused", the reason, and on the host the "Computer plays <colour>" buttons; no Resume.
- LAN chat: today's `ChatPanel`, opened with Enter; on big screens in the right column between the town list and End turn, on smaller
  screens to the left of the town list.

**Replay bar**
- Under the status bar. One Play/Pause toggle (Space), the speed, a slider by day with a mark per week ("Day 5 / 34"), Next turn, Next
  day, whose eyes, Skip to the result, Play from here, Main menu, Back to the replay.

### Hotkeys

Every hotkey shows on the status bar with its action's name; M, Space and Z are new.

| Action | Key | Status |
| --- | --- | --- |
| Next hero | Tab | today, no button |
| Next town | T | today |
| Hero screen | H | today |
| Spellbook | C | today |
| Kingdom | K | today |
| Quests | Q | today |
| Puzzle map | P | today |
| Dig | G | today |
| Switch level | L | today |
| Menu | F10 | today |
| Marketplace | M | new |
| Move | Space | new (Space is play/pause only in the replay viewer, where Move is disabled) |
| End turn | Z | new |

### Sizes, mirroring and UI scale

- **One arrangement, mirrored.** The Layout tab shrinks to one choice, lists and minimap on the right or the left; corners, lists in a
  bottom row and the side panel go (ADR-156, ADR-191, ADR-204 superseded in part). Saved layouts still load, as the default or its mirror.
- **Target sizes:** 1280×720, 1280×800 (Steam Deck), 1920×1080, 2560×1440 and 21:9.
- **Narrow screens:** the buttons are already in 2 rows; where the bottom row still does not fit, the army strip is cut off with a fade at
  its right end, and hovering it shows it whole, over the action buttons.
- **UI scale:** a game setting, 100 %, 150 % or 200 %, applied like the map editor's (`ContentScaleFactor`); the editor offers 100–200 %
  in steps of 25.

### Still open

Two points are unsettled and are now open questions: [HUD-1](../../memory-bank/open-questions.md#hud-1), which events stop play with
a notice card (decision 9.2), and [HUD-2](../../memory-bank/open-questions.md#hud-2), long answer lists (decision 9.1). Settled on
2026-11-24: Move on Space, Marketplace on M, End turn on Z, Tab stays for the next hero, End turn asks when a hero can still move, the
hero card's clicks as recommended, Switch level both in the grid and on the caption, the hero list grows while the town list scrolls;
then the timer under the banner, placeholder route colours, an action icon on the route's end, the fully built rule, the question
pictures, the chat's place and the narrow-screen army strip.

## Plan

The redesign lands in six steps: record it, build the engine-free models, rebuild the HUD on today's flat theme, check it at every size,
then swap in the painted pieces. The battle HUD only gets mockup prompts for now. Each step ends with the build and tests green and its
memory-bank entries updated.

### 1. Record the decisions
- [x] ADR-309 (presentation): the adventure HUD redesign, citing this paper; it supersedes in part ADR-156 (Layout tab), ADR-186 (resources
  on the status bar), ADR-191 (side panel), ADR-204 (End turn in the side panel) and ADR-210 (grid order).
- [x] Index row in `memory-bank/decisions/README.md`, next free number 310; roadmap checkboxes; refresh `active-context.md`.
- [x] Answer the guide's Still open list, or move it to `open-questions.md` (HUD-1, HUD-2).

### 2. Core (engine-free, with tests)
- [x] `LayoutSettings`: only a mirrored flag; `HudArrangement` reduced to it; older saved layouts load as the default or its mirror.
- [x] `InterfaceSettings.UiScalePercent`: 100, 150 or 200, clamped like `EditorPreferences`.
- [ ] A hero card model: movement, mana, experience progress (`TrainingRules.ExperienceToNextLevel`), total morale and luck
  (`Hero.TotalMorale`, `TotalLuck`), 7 slots.
- [x] A town build state: can build, built today (`TownRules.HasBuiltToday`), fully built (a new rule: every faction
  building at its highest level, 7 of the 9 dwellings under the 7-of-9 rule, ADR-030; `BuildingTree.IsFullyBuilt`).
- [ ] Object descriptions by ownership (the status line and the hover card), with `TestWorld` maps for owned and unowned mines.
- [ ] Route days for the tags: the day each step is reached, as the route preview already colours it. The action at the route's
  end, as `AdventureCursors` decides it, plus picking up (resources, artifacts, chests): done (`AdventureCursors.RouteEndOf`); the
  days still to do.
- [ ] The hotkey table as data the status bar and the input controller both read (new: Space, M, Z). The own heroes that can still
  move today, for End turn's question.

### 3. The HUD, on the flat theme
- [ ] Top resource bar on its own layer over the full-screen panels; the status bar drops the resources.
- [ ] Status bar: subject and detail, calendar on the right, hints first.
- [ ] Turn banner; timer pill and warning ribbon (`TurnTimerPanel`).
- [ ] Minimap: zoom buttons, the editor's view frame, the level caption, pictograms for towns and heroes.
- [ ] Hero list: portraits, two bars, a hover card. Town list: 4 slots, scroll arrows, picture, governor, build mark (the build mark
  is in).
- [ ] Hero card and army strip (`ArmyBar`), hidden with no hero.
- [ ] The 2×5 action grid, Move first; icon-only with names and keys on the status bar; End turn hourglass with day dots, on Z, asking
  first when a hero can still move.
- [ ] Route: day colours and tags in `PathPreview`; selection ring in the player's colour (the end's action mark is in).
- [ ] Hover card on map objects; question, notice and pause cards; dimming behind questions; the chat beside the town list.
- [ ] Replay bar: one toggle, a day slider, the other controls kept (the icons and the one Play/Pause toggle are in; the slider to do).
- [ ] Narrow screens: the army strip cut off with a fade, shown whole over the action buttons on hover; UI scale applied at start and from the settings.

### 4. Check
- [ ] Smoke screenshots of the map at 1280×720, 1280×800, 1920×1080, 2560×1440 and 21:9, at 100, 150 and 200 %, both mirrors.
- [ ] Smokes `game-town-open` and `game-town-build` still pass; the replay viewer and a LAN game on one machine.
- [ ] Budget overlay (F3): draw calls stay under the 2,000 limit with the new HUD (ADR-308).

### 5. Painted pieces
- [x] Icon sheets 30 (town marks, level icons), 31 (replay bar) and 32 (route end icons) in
  [icon-prompts.md](../content/prompts/icon-prompts.md), cut into `textures/ui/icons/`.
- [ ] Generate the rest: icon sheet 15 changed (Move as », Hero screen in place of Next hero); the list-slot sheet L4, the movement fill
  and the HUD pieces in [ui-element-prompts.md](../content/prompts/ui-element-prompts.md); the question pictures
  ([question-picture-prompts.md](../content/prompts/question-picture-prompts.md)).
- [ ] Swap them in for the flat theme's boxes; player-colour pieces are tinted at run time. Rewrite the HUD part of
  [presentation.md](../design/presentation.md) to state the new rules once.

### 6. Battle HUD
- [ ] Generate mockups from the prompts below, decide them the same way as the adventure HUD, then plan the restyle.

## Mockup prompts

Two prompts for an image model: the adventure HUD with every decision applied, for a check before building, and the battle HUD in the same
style (decision 11.5). Paste a block whole, then add one variant line.

### Adventure HUD, round 2

```text
A 16:9 screenshot at 2560x1440 of an original dark-fantasy turn-based strategy game, the adventure map in 3D seen from above at an angle, hand-painted look: forests, a river, a stone bridge, a walled town, a mine, a camp. The interface frames are dark slate-blue panels in worn bronze; accents and selections are in the current player's colour, crimson. No real game's logo or interface.
Top left: a slim bar with six resources, icon and number each: gold 2400, wood 12, ore 8, crystal 4, mercury 3, sulfur 6.
Top centre: a crimson ribbon banner with a lion crest reading "Rowan's turn".
Top right: a rectangular minimap with fog in black, small castle and hero pictograms, owned mines as coloured dots, a thin white rectangle for the camera's view; under it, on one line, a small caption plate with a sun-over-hills icon reading "Surface" and two small square buttons + and -.
Left edge: a column of three hero portraits in square frames, the first framed in crimson as selected, each with two thin upright bars to its right, green and blue.
Right edge: a column of four narrow slots: two small paintings of towns, each with a tiny round governor portrait in one corner and a small hammer mark in the other (one lit, one dim); two empty slots; small scroll arrows above and below the column.
Bottom left: a card with a large hero portrait, three thin horizontal bars beside it (green, blue, gold), "Lv 6 Mara", four stat icons with numbers 6 5 3 4, a banner icon "+1" and a clover icon "0"; to its right a strip of 7 creature slots with counts 48 36 12 8 7 4 1.
Bottom right: a grid of 2 rows of 5 icon-only buttons, each wider than tall, without labels: double chevron, spell book, crown, scroll, puzzle map / pickaxe, market stall, layered stack, hero bust, three bars; the market stall greyed out. Next to the grid a tile with an hourglass and 7 small dots in a row, the 5th lit crimson. The hero card, the creature strip, the button grid and the hourglass tile are panels of exactly the same height, their top and bottom edges in line.
On the map: a mounted hero with a crimson ring at its feet, a route of dots from the hero to the mine, yellow dots for the first day with a small tag "Day 5", pale blue dots for the next day with a tag "Day 6", a larger marker at the end.
A thin full-width bar along the bottom: a market icon, "Marketplace", a divider, "Build a marketplace in one of your towns.", and on the right "Month 1 · Week 1 · Day 5".
Clean, legible, no text other than the quoted words.
```

Variant lines, one per picture:

- *Underground:* the same scene in a cavern with glowing mushrooms; the minimap caption reads "Underground" with a cave icon.
- *Replay:* no banner; under the status bar a second bar with a play button, a speed box "1x", a slider with week marks and "Day 5 / 34",
  and small buttons for next turn, next day, whose eyes, skip, play from here, main menu; the action buttons and the hourglass greyed.
- *Timed hot-seat:* a red pill "0:30" just under the banner and a red ribbon "30 seconds left!".
- *Hover:* the pointer over the mine shows a small card "Ore Mine · Owner: Rowan · +2 ore per day", no picture.
- *Question:* the map dimmed, a centred card with a treasure chest picture, "Take the treasure?" and two buttons "1,000 gold" and "500
  experience".
- *Small screen:* 1280x720; the army strip under the hero card, the 2x5 grid beside the hourglass.
- *Mirrored:* the minimap, town list and actions on the left, the hero list on the right.

### Battle HUD, round 1

```text
A 16:9 screenshot at 2560x1440 of an original dark-fantasy turn-based strategy game's battle: a 3D battlefield on a grass plain seen from above at an angle, two armies of fantasy creatures facing each other on a hexagonal grid, hand-painted look. The interface uses the adventure map's style: dark slate-blue panels in worn bronze, icon-only buttons, accents in each side's player colour (crimson for the left side, blue for the right). No real game's logo or interface.
Top left: the attacking hero's portrait in a crimson frame with name "Mara" and a small spell book button; top right: the defending side's portrait in blue, "Neutral wolves".
Top centre: "Round 3" on a small plate.
Along the bottom, above the status bar: an initiative timeline, a row of square creature portraits with stack counts, the active stack first and larger with a bright rim in its side's colour, then the queue, a thin divider, then the waiting stacks dimmed.
Bottom left: a panel with the hovered stack's portrait, name "Swordsmen" and its numbers: attack, defence, damage, health, speed, initiative, with small icons.
Bottom right: square icon-only buttons: hourglass (wait), shield (defend), running figure (retreat), white flag (surrender), crossed swords with a lightning bolt (quick combat), spell book.
A small semi-transparent combat log in the lower right corner above the buttons, three short lines.
A thin full-width status bar along the bottom: a sword icon, "Swordsmen", a divider, "Attack the wolves: 38-45 damage, 2 may die."
Clean, legible, no text other than the quoted words.
```

Variant lines:

- *Deployment:* no timeline yet; a wide button "Start battle" at the bottom centre; your side's deployment hexes tinted crimson.
- *Result:* the field dimmed, a centred window "Victory" with both sides' losses as creature portraits with counts, experience gained, and
  a button "Return to the map".
- *Necromancy:* after the result, a window with raised skeletons as a portrait and a count, and a Continue button.
- *Small screen:* 1280x720; the stats panel collapses to the portrait with three numbers.
