# Town screen overhaul: study and plan

The plan to rebuild the town screen (ADR-246): a **3D town scene** as the main screen, with the windows of a full town screen around it
(the building tree, recruitment, the mage guild, the tavern, the marketplace, the blacksmith and the rest). The quality target is a
current commercial strategy game. In function the screen should match HoMM3 with HotA and HoMM5, keeping this game's own rules.

This file has five parts. The **study** looks at what we have and what the references do. The **target** sets the bar. **Screens**
describes each screen. **Technical design** and **art and audio** cover how to build it. **Roadmap**, **questions** and **risks** close
it. Nothing here is built yet. The questions (TOWN-14 … TOWN-23) are also in
[open-questions.md](../../memory-bank/open-questions.md#towns); each one has a proposal that the plan follows until the owner answers.
When every step is built, this file stays in `papers/` as the report (ADR-280) and the rules move into [towns-and-economy.md](../design/towns-and-economy.md) and
[presentation.md](../design/presentation.md).

## Study

### What the game has today

`TownPanel` (ADR-046, ADR-114, split into sections by ADR-223) is a full-screen **2D panel that scrolls**. It is one column of rows
over an opaque background, opened from the adventure map. The town rules behind it are complete and tested. What is missing is the
screen itself.

| Part today | What it does | What holds it back |
|---|---|---|
| Header (`TownHeaderSection`) | Crest, name, income, "Trade" and "Thieves' Guild" buttons, close | No picture of the town, no previous / next town, no growth summary |
| Buildings (`BuildingTreeSection`) | One column per requirement depth, a text button per building, a tooltip with cost, ✓ / ✗ requirements and effect | Text only. No icons or pictures, no lines between requirements, colour is the only sign of state, the cost hides in a tooltip |
| Dwellings (`DwellingsSection`) | A grid row per line: state, waiting, growth, Build / Upgrade, "Recruit N" for the base and the upgrade | Recruits the most you can afford in one click. No count to choose, no portraits or stats, the 7-of-9 choice is only a ⚠ in the text |
| Governor (`GovernorSection`) | Appoint / dismiss | A row of its own for a rare action |
| Armies (`TownArmiesSection`) | Garrison and visiting-hero slots: click a stack, then a slot. Global "Split" and "Recruit to visiting hero" checkboxes | No drag and drop, no split amount, no hero portraits, the checkboxes change what other rows do |
| Caravan, Transformer | Their orders | Usable, but rows in the same list |
| Blacksmith (`BlacksmithSection`) | Buy war machines and a spell book; what the guild teaches now | The mage guild has no view of its own |
| Tavern (`TavernSection` + `TavernPanel`) | Hire buttons, and a tavern screen with hero cards | The screen exists; no portraits yet |
| Marketplace (`MarketplacePanel`) | Gold against one resource, at the rules' rates | One trade pair only (ADR-067); a separate screen with no town look |

The cross-cutting state of the UI (ADR-191, ADR-204, ADR-245):

- Theme: dark slate with bronze frames, on Godot's default fonts.
- Icons: icon-only buttons and the status bar are in place. Most icon images and every portrait are still to be drawn.
- Prompts: the UI frame, portrait and town-screen background prompts exist.
- Town art: no 3D town art exists. Adventure-map towns are placeholders, with model prompts in
  [town-model-prompts.md](../content/prompts/town-model-prompts.md).
- Engine pattern: the battle screen already shows how the game hides the adventure map and shows a 3D scene of its own with its own
  camera (`BattleView3D`). The town scene can follow it.

### HoMM3 and HotA

- **Main screen**: a painted picture of the whole town fills the top three quarters. Each building is a hot spot. Hovering names it on
  the status line, a click opens its window, and a right click describes it. A newly built building appears in the picture with a short
  fade. The bottom bar holds the town's portrait and income, then two rows of seven slots: the garrison (with the garrison hero's
  portrait) and the visiting hero. On the right are the town list and Exit. Stacks move by drag and click. Swapping the two heroes
  moves a hero into or out of the garrison.
- **Hall** (build screen): a fixed grid of every building the town can have, each a picture with a state frame (built, can build,
  can't afford, missing a requirement, already built today). A click opens a card with the description, cost and requirements, and a
  Build button.
- **Fort / Castle window**: every creature line on one screen with its picture, stats, growth and waiting count. A click opens the
  recruit dialog.
- **Recruit dialog**: a slider, a Max button, the price of one, the total, and what is available.
- **Mage guild**: shelves, one per guild level, with spell scrolls. A visiting hero learns automatically, and the window shows what the
  guild offers.
- **Marketplace**: trades any resource for any other, sells artifacts (Artifact Merchants), transfers resources to another player, and
  has the Freelancer's Guild (sell creatures).
- **Blacksmith**: a dialog for its one war machine. **Tavern**: two heroes for hire, a rumour line and the Thieves' Guild.
- **HotA additions**:
  - quick recruitment: "buy all" from the fort window, top tiers first;
  - split shortcuts: one creature, split evenly, merge;
  - hotkeys for every window;
  - creature growth shown on the fort screen;
  - mage-guild spell research (swap a guild spell for another for gold);
  - showing what a stack would cost to upgrade.
- **What it gets right**:
  - the town picture *is* the menu, and the bottom bar never moves;
  - every common action is one or two clicks away;
  - the states can be read at a glance;
  - the screen grows visibly as the town is built.

### HoMM5

- **Main screen**: a **3D town** in a fixed diorama. The camera drifts slowly around the town or is turned within limits, and zooms in
  on a building when one is clicked or built. Each building is a 3D model with idle life: smoke, banners, water, light. A new building
  rises in a short construction scene. The same bottom bar as HoMM3 sits over the scene.
- **Town Hall build screen**: a **tree** of building icons with requirement links, grouped by kind. The states are shown by frame
  colour. Hovering shows the requirements.
- **Recruitment**: per dwelling, and a "Recruit all" screen for every line at once.
- **Windows**: the 2D windows (tavern, marketplace, guild, blacksmith) open over the 3D scene, which keeps playing behind them.
- **What it gets right**: the town is a *place*. Building it is a reward the player sees, and the faction's look is strongest here.
- **What it gets wrong**: the camera moves to a building before its window opens, which feels slow when repeated. Buildings are hard to
  pick out in a busy scene. Some status is visible only in the scene. Our plan avoids all three: windows open at once, hovering outlines
  the building, and the hall shows every state in 2D.

### Modern references (quality bar, not features)

Recent turn-based strategy games with 3D settlements set the visual and UX bar for this plan:

- **Readable 3D**: a strong rim outline on hover, a label and the cursor change. The buildings' silhouettes differ even at a
  distance.
- **Information in layers**: a short tooltip, the right-click info window (ADR-154) and the detail card. Numbers show where the
  decision is made, never only in tooltips.
- **Motion and sound** confirm every action: coins on recruiting and trading, a hammer on building, a bell when a hero arrives.
- **Every screen works** with mouse, keyboard and controller (ADR-034, the Steam Deck).

### Gap analysis

| Feature | HoMM3 / HotA | HoMM5 | Ours today | Target |
|---|---|---|---|---|
| Town shown as a place | Painted 2D | 3D diorama | None (list) | 3D diorama (ADR-246) |
| Click a building to use it | Yes | Yes | No | Yes, with a hover outline |
| Building grows visibly | Fade-in | Construction scene | No | Short construction scene, skippable |
| Build screen with states | Grid with frames | Tree with links | Text tree | Tree with icons, links, states shown by colour **and** by symbol |
| All dwellings on one screen | Fort window | Recruit all | Grid rows | Recruitment window with cards |
| Recruit count slider | Yes | Yes | No (max only) | Yes |
| Recruit all / buy all | HotA | Yes | No | Yes (TOWN-17 a) |
| Upgrade all stacks | Hill Fort only | No | One stack | Yes (TOWN-17 b) |
| Drag and drop stacks, split dialog | Yes | Yes | Click-click, a global checkbox | Yes, plus HotA-style shortcuts |
| Hero swap garrison ↔ visitor | Yes | Yes | Button | Drag a portrait, or a button |
| Mage guild window | Yes | Yes | One text row | Shelves per level, spell icons |
| Spell research | HotA | No | No | Candidate (TOWN-17 g) |
| Resource ↔ resource trade | Yes | Yes | Gold ↔ one resource | Proposal: any pair (TOWN-17 c) |
| Sell artifacts, transfer resources, sell creatures | Yes | Partly | No | Proposal: transfer to allies only (TOWN-17 d–f) |
| Tavern rumours | Yes | Yes | No | Yes (TOWN-17 h) |
| Previous / next town without leaving | Yes | Yes | No | Yes |
| Faction music, ambient sound | Yes | Yes | No | Yes |
| Keyboard and controller | Hotkeys | Partly | Escape only | Full |

## Target

The bar the overhaul is measured against. Each item can be checked; step 6 of the [roadmap](#roadmap) closes on them.

- **One look**: every window uses the shared frames, buttons and slots from
  [ui-element-prompts.md](../content/prompts/ui-element-prompts.md), the game's own fonts, and the faction's accent colour on the
  frame. No default Godot control shows.
- **No text-only actions**: every action button has an icon, every creature and hero a portrait, every building a picture.
- **Reach**: every common action is at most **two clicks** from the main screen: recruit, build, move a stack, swap heroes, buy a
  machine, hire, trade. Every window opens with a hotkey.
- **State without colour**: built, can build, can't afford, needs buildings, built today and locked forever each have their own
  symbol as well as a colour.
- **Speed**:
  - the town screen opens in **under 1 s** (the town's scene is loaded before the hero arrives);
  - a window opens in 150 ms;
  - the construction scene lasts 1.5 s and can be skipped;
  - the scene holds **60 fps** at 1440p on the reference PC and **40 fps** on the Steam Deck.
- **Sound and motion**: every action gives a sound and a motion. Nothing moves without a cause, except idle life in the scene.
- **Truth**: every number comes from the same rules as the game (`TownRules`, `BuildingTree`), so the screen, the AI and the
  simulator never disagree.

## Screens

### Map of the screens

```text
Adventure map ──(hero enters / town list / T)──► TOWN MAIN SCREEN (3D scene + bottom bar)
   ▲                                                   │ click a building, or its hotkey
   └──────────── Escape / Exit ◄───────────────────────┤
                                                       ├─► Hall: building tree (B)
                                                       ├─► Recruitment: every dwelling (R) ─► recruit dialog
                                                       ├─► Dwelling (click its building) ──► recruit dialog
                                                       ├─► Mage guild (G) · War lodge
                                                       ├─► Tavern (V) ─► Thieves' Guild
                                                       ├─► Marketplace (M) · Resource Silo
                                                       ├─► Blacksmith (S)
                                                       ├─► Fort / Castle: the town's defences (F)
                                                       ├─► Caravan · Undead Transformer · unique buildings · Grail
                                                       ├─► Hero screen (H, a hero's portrait) · creature info (right click)
                                                       └─► Previous / next town ([ / ])
```

Windows open over the scene, which keeps running behind them, dimmed. Only one window is open at a time. Escape closes the window, and
a second Escape leaves the town.

### Main screen

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────┐
│ [crest] Brightwatch  ◄ ►  (Haven · City Hall · governed by Ilsa)        [B][R][G][V][M][S] [×] │
│                                                                                              │
│                                                                                              │
│                         3D TOWN SCENE (fills the window)                                     │
│              hover: outline + name; click: open; right click: info window                   │
│                                                                                              │
│                                                        ┌───────────────┐                     │
│                                                        │ Built today:  │                     │
│                                                        │ Citadel       │                     │
│                                                        └───────────────┘                     │
├──────────┬────────────────────────────────────────────────────────────────┬──────────────────┤
│ [town    │ [garrison hero] [ ][ ][ ][ ][ ][ ][ ]   [Split] [Upgrade all]   │ Income +2500/day │
│ portrait]│ [visiting hero] [ ][ ][ ][ ][ ][ ][ ]   [Swap]  [Dismiss]       │ Growth ▸ (hover) │
│          │                                                                │ Week 2 of Month 1│
├──────────┴────────────────────────────────────────────────────────────────┴──────────────────┤
│ status line ......................................................... resources ........... │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Top bar**:
  - the crest, the town's name (click to rename, TOWN-17 j), and ◄ ► through the player's towns;
  - one line of facts: faction, town hall level, governor;
  - quick buttons for the main windows (icon only, labels and hotkeys on the status bar, ADR-204);
  - the close "×" (ADR-225).
- **Bottom bar**: the HoMM layout, kept because it works.
  - The town's portrait (the town icon from [hero-town-icon-prompts.md](../content/prompts/hero-town-icon-prompts.md)).
  - Two army rows of seven slots, each led by a hero portrait (empty frame when there is no hero).
  - Next to the rows: Split, Upgrade all, Swap and Dismiss.
  - On the right: the daily income, the week's growth (hover for each line's breakdown: base, fort, growth building, week event,
    artifacts), and the date.
- **Army rows**:
  - drag a stack onto a slot to move, merge or swap;
  - **Shift** + drag opens the split dialog (a slider);
  - **Ctrl** + drag moves one creature;
  - **Alt** + click spreads a stack evenly over the empty slots;
  - double click opens the creature's info.
  - A stack that can upgrade shows an arrow badge. Its tooltip gives the price difference, and a click upgrades it (the existing
    `UpgradeStackCommand`).
  - Drag a hero's portrait to the other row to swap, which uses `EnterGarrisonCommand` and `LeaveGarrisonCommand`.
- **Notices**: one card in a corner of the scene says what was built today, or that nothing was. Caravans arriving and the governor's
  discount have small chips there as well.
- **Built today**: when the day's building is done, the Hall's quick button has a lock badge, and the hall shows why.

### The 3D town scene

- **A diorama per faction**: a fixed ground of 60 × 40 m (a placeholder size) with the faction's landscape around it. It is never
  the adventure map's terrain. A painted sky and far distance come from the `bg_town_<town>` pictures
  ([ui-element-prompts.md](../content/prompts/ui-element-prompts.md)), used as the sky.
- **Plots**: every building and dwelling the town type can have gets a **plot**. A plot is a fixed place with:
  - one model per level (town hall 1–4, fort 1–3, a dwelling and its upgrade, ...);
  - a hover shape;
  - a camera focus point;
  - the window it opens.

  All nine dwelling lines have plots. What a plot shows depends on its state (TOWN-15):

  | Plot state | Shows |
  |---|---|
  | Not built (whether it can still be built or its line is closed by the 7-of-9 choice) | Unused land: a piece of the diorama's scenery, no markers or rubble |
  | Built today | Scaffolding, then the model |
  | Built | The model for its level |
  | Closed dwelling, once the town has the Capitol | Homes of the town's citizens |

  The Capitol needs six dwellings of tiers 1–4 (ADR-291), so a town with it has exactly two closed tier 1–4 lines. Building the Capitol
  fills those two plots with citizens' homes, as part of its construction scene; a captured Capitol that drops to City Hall keeps them.
- **Walls follow the fort**: no walls at level 0. A palisade or low wall with the Fort, towers with the Citadel, and the full wall and
  moat with the Castle. These are the same parts the siege uses ([combat.md](../design/combat.md#sieges)), so the town looks as it
  fights.
- **Camera** (TOWN-14):
  - a fixed "hero shot" of the whole town;
  - the player may turn it ±25° and zoom in two steps;
  - a click on a building opens its window **at once**; the camera eases toward the building behind the window, which does not wait
    for it;
  - after Escape the camera eases back.
  - The new-building scene moves the camera to the plot, plays 1.5 s (scaffold, dust, the model rising, a bell) and returns.
    Fast and instant speeds (ADR-090) cut it short or skip it.
- **Hover and pick**:
  - an outline in the UI's bronze on the building under the pointer;
  - its name and state on the status line, with the cursor showing a hand;
  - a right click opens the building's info window (ADR-154).
  - Plots that are built or can be built are picked by a hover shape, not by the mesh, so a thin tower is as easy to hit as a hall.
- **Life**:
  - banners in the player's colour (ADR-211);
  - smoke from the blacksmith, the guild's glow, water, birds;
  - the visiting hero's mount at the gate (TOWN-22);
  - when creature models exist, one idle creature at each built dwelling (TOWN-20).
  - Ambient sound per building, louder near the camera's focus.
- **Mood** (TOWN-18): one lighting set per faction (Haven a pale grey dawn, Inferno a red dusk, and so on), as the art direction asks
  (low sun, fog, strong shadows). The week's event can tint the scene: banners of the week's creature, or a grey haze in a plague month.
- **Arrival** (TOWN-23): from the adventure map, a 0.5 s fade through black into the hero shot. Skipped at instant speed.

### Hall: the building tree

```text
┌─ Hall ─ Brightwatch ──────────────────────────────────────────────────────── [×] ┐
│ Core        │ Military    │ Magic         │ Dwellings (7 of 9)         │ Unique  │
│ [Hall 3/4]──┼─►[Fort 2/3] │ [Guild 2/4]   │ T1 [a ✓]  [b  ]            │ [Grail] │
│ [Tavern 2]  │ [Smith ✓]   │ [Spell bldg]  │ T2 [a ✓]  [b ✗ closed]     │ [own]   │
│ [Market 1]  │ [Caravan]   │               │ T3 [a  ]  [b  ]            │         │
│             │             │               │ T4 [a  ]  [b  ]            │         │
│             │             │               │ T5 [   ]                   │         │
├─────────────┴─────────────┴───────────────┴────────────────────────────┴─────────┤
│ [picture] Citadel (Fort 2 of 3)    Cost: 3000 ● 10 ▲ 10 ◆  (you lack 2 ▲)         │
│ Needs: ✓ Fort.  Gives: +50 % growth, towers in sieges.          [Show in town] [Build] │
└───────────────────────────────────────────────────────────────────────────────────┘
```

- **Columns by kind**, not by requirement depth: Core, Military, Magic, Dwellings, Unique. The requirement **links** are drawn as
  lines. Hovering a building lights up its whole chain of requirements and dims the rest.
- **A tile per building**: its picture, its level (n of m) and a state frame with a symbol:

  | State | Frame | Symbol |
  |---|---|---|
  | Built out | Green | ✓ |
  | Can build | Bronze | Hammer |
  | Can't afford | Amber | Coin |
  | Needs buildings | Grey | Chain |
  | Built today | Blue | Hourglass |
  | Locked for ever | Dark red | ✗ |
- **Detail card** (the selected tile):
  - the picture and the next level's name;
  - the cost, with what the player **lacks** marked;
  - the requirements as ✓ / ✗ links (a click selects that building);
  - what the level gives;
  - the refusal in plain words;
  - **Build**;
  - **Show in town**, which closes the hall and shows a ghost of the model on its plot.
- **The 7-of-9 choice** is drawn as a block: a row per tier, two tiles side by side, and the one tier-5 tile. Selecting a dwelling
  that would close others draws the closed ones crossed through in the preview, with the warning on the card.
  `BuildingTree.DwellingChoices` already computes this.
- **Planning** (TOWN-17 k, a proposal for later): pin one building as the town's goal. The card then shows the days of income needed,
  and the town list marks the town once it can afford the goal.

### Recruitment

- **Recruitment window**:
  - one **card** per dwelling line: up to seven built ones, plus the unbuilt ones, greyed and marked "build in the Hall";
  - each card shows the creature's portrait, a **base / upgraded** toggle, attack, defence, damage, health, speed and abilities as
    icons, the number waiting, the growth this week, and the cost of one;
  - a click opens the recruit dialog;
  - **Recruit all** buys the most of every line the treasury allows, top tier first. Before paying it shows a summary ("12 Footmen,
    4 Griffins … for 6200 gold") (TOWN-17 a).
- **Recruit dialog**:
  - the portrait;
  - a slider with − / + and **Max**;
  - the cost of one, the total, and the treasury after paying (red where short);
  - the destination as a choice of **Garrison** or **Visiting hero**, preselected to where the creature fits (this replaces today's
    global checkbox);
  - **Recruit** (`RecruitCommand`).
- **Dwelling window** (click a dwelling in the scene): the same card large, with the building's picture and the dialog under it.
  **Upgrade the dwelling** is there too when it can be built.
- **Upgrade all** (bottom bar): upgrades every stack in both rows that the town can upgrade and the player can pay for. It shows the
  total first (TOWN-17 b).

### Mage guild (and the war lodge)

- **Shelves**: one row per guild level that is built (four rows, the fourth a placeholder). Each row holds that level's spell icons
  ([spell-icon-prompts.md](../content/prompts/spell-icon-prompts.md)). The spell buildings' extra spells sit on the same row, framed in
  their school's colour. Levels not yet built are dark shelves that say what they need.
- **The visiting hero**: a mark on each spell the hero knows, a "learned now" flash for spells learned on this visit, and greyed
  spells that need more of a Magic skill (with the reason). **Buy a spell book** stands here (`BuySpellBookCommand`), not at the
  blacksmith.
- **Research** (TOWN-17 g, a candidate): swap a guild spell for another spell of that level and school, for gold and the school's
  resource, once a day.
- **War lodge** (Stronghold, Swamp Bestiary): the same window, showing the warcries by level.

### Tavern

The existing `TavernPanel`, restyled to the new frames:

- **Hero cards**: the portrait, name, class, specialty, primary stats, skills as icons, the starter stack and **Hire**.
- Returning heroes carry a "returns with its army" badge.
- **Rumour**: one line a week (TOWN-17 h). **Thieves' Guild** opens from here.
- The town's main screen shows the **next hire** in the tavern's hover text.

### Marketplace

- **Two columns**: what you give and what you get, each as six resource tiles. Pick one on each side, and the rate shows between them.
  Then a slider, Max, the amounts, and **Trade** (`TradeResourceCommand`).
- **Today the rules trade only gold against one resource**, so the left column is fixed to gold when buying and the right one when
  selling. Any pair (TOWN-17 c) would need a rule change: an ADR, a new command and tests.
- **Resource Silo**: its income is shown here, and its hover text in the scene says the same.
- **More tabs if adopted**: transfer to an ally (TOWN-17 e), sell artifacts (TOWN-17 d), sell creatures (TOWN-17 f).

### Blacksmith

- **Three cards**, one per war-machine slot (`WarMachineSlot`): the machine's picture or a turning 3D model, its stats, whether the
  visiting hero has it, the price, and **Buy** (`BuyWarMachineCommand`).
- Without a visiting hero the cards still show, with the reason the buttons are greyed.

### The other windows

- **Fort / Castle** (the town's defences):
  - a picture of the walls, towers, gate and moat at their current level;
  - the next level's gain;
  - the garrison's strength against a siege;
  - the unique building's siege bonus.
- **Caravan**: today's `CaravanSection` as its own window, with dwellings in reach as cards (distance, days, what they hold) and the
  queue of incoming and outgoing caravans.
- **Governor**: in the town's portrait slot. A click opens a list of heroes with Stewardship, each with the discount it would give.
- **Undead Transformer**: the visiting hero's and the garrison's stacks with "→ result" previews, and a toggle each, as today.
- **Unique buildings, Grail building, Observatory**: an info window with the effect, built from `InfoSheets`.
- **Information windows** (ADR-154) everywhere: a right click on any building, stack, portrait, spell, machine or resource.

### Input, accessibility and text

- **Hotkeys**:

  | Key | Opens or does |
  |---|---|
  | B | Hall |
  | R | Recruitment |
  | G | Guild |
  | V | Tavern |
  | M | Marketplace |
  | S | Blacksmith |
  | F | Fort |
  | C | Caravan |
  | U | Upgrade all |
  | [ ] | Previous / next town |
  | H | Hero screen |
  | Space | Swap the heroes |
  | Escape | Back |

- **Controller**: the d-pad walks the plots in the scene (left / right along a ring of plots), A opens, B goes back, and the shoulder
  buttons switch towns. Every window has a focus order.
- **Accessibility**: states never by colour alone; a UI scale setting (100–150 %); text at least 16 px at 1080p; animations shortened
  by the speed settings.
- **Text**: every string goes through one place, ready for the localisation decision (ADR-239).

## Technical design

### Core (engine-free, tested)

The rules exist. The screen needs **read models** and a few **commands**:

- **Read models**:
  - `TownOverview`: income, the growth breakdown per line, built today, the governor's discount, plot states;
  - `RecruitPlan`: what Recruit all would buy, line by line, at what cost;
  - `UpgradePlan`: which stacks Upgrade all would upgrade, and the total.

  All of them are computed from `TownRules` and `BuildingTree`, so the AI and the simulator can use them too.
- **New commands** (each with an ADR when its rule is adopted, and tests):
  - `RecruitManyCommand` (a list of orders, all or nothing, one event);
  - `UpgradeAllCommand`;
  - `DismissStackCommand` (TOWN-17 i);
  - the trade, transfer, research and rename commands, if TOWN-17 adopts them.

  Commands keep replays and LAN (ADR-036, ADR-098) exact. Two hundred separate recruit commands would not.
- **Events**: the scene and the windows refresh on the existing events (`TownBuilt`, `DwellingBuilt`, `CreaturesRecruited`,
  `GarrisonChanged`, ...), by the `RefreshScheduler` pattern (ADR-223). No polling.

### Godot

- **`TownScene3D`** (in `EternalWars.Godot.Shared`, beside `BattleView3D`) is a `Node3D` with its own camera, light and
  `WorldEnvironment`. The game app hides the adventure world while it is open, as it does for a battle. It builds the scene from a
  **layout** and the town's state, plays the construction scene on `TownBuilt` / `DwellingBuilt`, and raises `BuildingPicked`,
  `BuildingHovered`.
- **Layouts as assets**: one scene per town type, `scenes/towns/<town>.tscn` in `EternalWars.Godot.Assets`. It holds the diorama
  ground, the sky and named `Marker3D` plots (`plot_town-hall`, `plot_dwelling-<line>`, ...), each with a child hover shape and a
  camera focus. Models per level are found by name (`models/towns/<town>/<building>_<level>.glb`). Code finds the plots by name, so an
  artist can move a building without changing code. A new building (TOWN-12) needs a plot and its models.
- **Greybox first**: a plot without a model gets a procedural placeholder: a block sized by building kind, a roof, its name on a sign.
  This lets the whole screen work, and be smoke-tested, before any art exists.
- **The UI** (`EternalWars.Godot.UI/Hud/TownScreen/`): the main screen's bars and every window are `ScreenSection`-based parts
  (ADR-223), rebuilt alone on their own events. New shared widgets, also usable by the hero and exchange screens:
  - `ArmyBar` (drag and drop, split dialog, badges);
  - `CostLabel` (have / lack);
  - `CreatureCard`, `BuildingTile`, `SliderDialog`, `WindowFrame` (nine-slice, faction accent).
- **The 2D harness** keeps today's `TownPanel` as a tool view (TOWN-16). The game app uses the new screen only. Once the new screen
  covers every action, `TownPanel` stays in the harness alone.
- **Loading**: the town type's scene is loaded in the background when an own hero's route ends at an own town, or at the start of the
  turn for the selected town. One scene stays cached.
- **Rendering**:
  - Forward+, one directional light with shadows, a reflection probe per scene, SSAO, volumetric fog, and bloom on magic only;
  - baked ambient occlusion in each model's texture (the buildings change, so a baked lightmap of the whole scene does not fit);
  - a budget per scene: **≤ 1.5 M triangles, ≤ 400 draw calls, ≤ 1 GB of video memory** at full build (placeholders, to be set by
    the look-dev spike);
  - LOD on everything;
  - a Deck preset: half-resolution fog, no SSAO, shadows at 2048.

### Tests and checks

- **Core**:
  - tests for every read model, including Recruit all's order and the case where money runs short;
  - command tests: all or nothing, refusals, events;
  - `TestWorld` towns for the governor's discount and the week's events.
- **Smoke scenarios** (`tools/smoke-godot.ps1`), each saving screenshots:
  - `game-town-open` (enter, the scene and the bars);
  - `game-town-build` (Hall, build, construction scene);
  - `game-town-recruit` (dialog, Recruit all);
  - `game-town-armies` (drag, split, swap);
  - `game-town-windows` (guild, tavern, market, smith);
  - `game-town-switch` (◄ ►).
- **Performance**: a fully built Haven and Inferno measured at 1440p and on the Deck; a frame-time capture in the roadmap's check
  list.

## Art and audio

- **Volume per faction**:

  | Kind | Models |
  |---|---|
  | Town hall | 4 levels |
  | Fort | 3 levels, plus wall pieces |
  | Tavern | 2 |
  | Marketplace and Silo | 2 |
  | Blacksmith | 1 |
  | Mage guild (or war lodge) | 4 (3) |
  | Caravan | 1 |
  | Spell buildings | 2 |
  | Growth buildings | 2 |
  | Unique building(s) | 1+ |
  | Grail building | 1 |
  | Dwellings | 9 lines × 2 levels = 18 |
  | Scaffolding, citizens' homes (per faction, 2) | Shared scaffolding; homes per faction |
  | Diorama ground, sky and props | 1 set |

  That is about **40 models a faction, about 320 for the eight**, the largest art task in the game. Hence the order: greybox, then two
  factions, then six.
- **How it is made** (TOWN-21):
  - concept art per faction first (a key painting, then a sheet of the main buildings);
  - then models by an image-to-3D model with clean-up, or kitbashed from a shared kit per faction (roofs, walls, windows, props).
  - A new prompt file, `town-scene-prompts.md`, in the same layout as the others (ADR-245). It reuses each faction's look from
    [town-model-prompts.md](../content/prompts/town-model-prompts.md).
- **Building pictures** for the Hall: rendered from the finished models in a fixed studio light (a tool scene), so the pictures always
  match the scene. Greybox renders stand in until then.
- **Audio**: a town theme per faction (8; the roadmap's "town themes"), an ambient loop per diorama, and short sounds for build,
  recruit, trade, hire, buy, the hero's arrival and windows opening and closing.

## Roadmap

Six steps. Each ends in a working game with a green test suite, and steps 1 and 2 already help players. The roadmap's checkboxes are in
[roadmap.md](../../memory-bank/roadmap.md) under "Town screen overhaul".

| Step | What | Depends on | Done when |
|---|---|---|---|
| 0 | **Decide and sketch**: answers to TOWN-14 … TOWN-23; wireframes of every window as a clickable greybox in Godot; one faction's style frame (key art plus a lit greybox) | — | The owner signs off the wireframes and the style frame |
| 1 | **UI foundation**: own fonts, nine-slice frames, buttons and slots from the UI prompts; the shared widgets (`ArmyBar`, `SliderDialog`, `CostLabel`, `CreatureCard`, `BuildingTile`, `WindowFrame`); hotkeys and focus order | 0; the UI art (ADR-245) or greybox frames | Hero, exchange and current town screens use the widgets; drag, split and swap work |
| 2 | **The windows over a 2D main screen**: Hall, Recruitment and the recruit dialog, Mage guild, Tavern, Marketplace, Blacksmith, Fort, Caravan; core read models and the new commands; the bottom bar; ◄ ► between towns | 1 | Every action of today's screen is in a window; the long scrolling list is gone from the game app |
| 3 | **The 3D scene in greybox**: `TownScene3D`, layouts with plots for all eight playable towns, camera, hover and pick, construction scene, walls by fort level, arrival, loading ahead | 2 | The game app's town screen is the 3D scene for every playable town, in greybox, at budget |
| 4 | **Vertical slice art**: Haven and Inferno (ADR-034) fully modelled, lit, with life, sound and music; Hall pictures rendered; Deck preset | 3; the look-dev spike | Both towns meet every item of the [target](#target) |
| 5 | **The other six factions**: Sylvan, Academy, Necropolis, Dungeon, Stronghold, Swamp Bestiary | 4 | All eight at the slice's quality |
| 6 | **Polish and extras**: controller pass, UI scale, the TOWN-17 mechanics the owner adopts, idle creatures (once creature models exist), the final check against the [target](#target) | 5 | The target list is ticked |

Conflux and Dragon Utopia are not playable, so they get no town screen.

## Questions

Each question also has an entry in [open-questions.md](../../memory-bank/open-questions.md#towns). The plan follows the **proposal**
until the owner answers.

<a id="town-14"></a>
**TOWN-14 The town camera.** (a) A fixed hero shot, free orbit, or HoMM5's slow drift? (b) Does a click wait for the camera to fly to
the building? **Proposal**: (a) a fixed hero shot that the player may turn ±25° and zoom in two steps, no drift; (b) no. The window
opens at once, and the camera eases behind it.
Answer : use proposals;

<a id="town-15"></a>
**TOWN-15 What plots show.** (a) Do the two dwelling alternatives of a tier both have a plot? (b) What does a plot show before it is
built, and once its line is closed for ever? **Proposal**: (a) yes, all nine lines; (b) marker stones while it can be built, overgrown
rubble with a broken marker once closed, scaffolding on the day it is built.
Answer : (a) yes; (b) no marker stones or rubble: a plot not built looks like unused land or a piece of scenery, whether it can still be
built or is closed; scaffolding on the day it is built. New rule: the Capitol needs all six dwellings of tiers 1–4 first (ADR-291), which
leaves two unused plots for the closed lines; building the Capitol fills them with homes of the town's citizens.

<a id="town-16"></a>
**TOWN-16 A 2D fallback.** Keep a 2D main screen (a list or a painted picture) for weak hardware or as a setting? **Proposal**: no. The
3D scene has a low preset for the Deck, and the windows carry all the information. The harness keeps today's `TownPanel` as a tool.
Answer : use proposals;

<a id="town-17"></a>
**TOWN-17 Town mechanics to add.** Which of these join the rules?
- (a) Recruit all.
- (b) Upgrade all.
- (c) Any resource for any other at the marketplace.
- (d) Selling artifacts at the marketplace.
- (e) Sending resources to an ally.
- (f) Selling creatures (Freelancer's Guild).
- (g) Mage-guild spell research.
- (h) Tavern rumours.
- (i) Dismissing a stack.
- (j) Renaming a town.
- (k) A building pinned as the town's goal.

**Proposal**:
- Yes now: a, b, h, i, j.
- c: yes, at rates derived from today's: give A for B at A's sell price divided by B's buy price.
- e: yes, only between teammates.
- k: yes, later (UI only).
- d and f: no. The Sacrificial Altar and the map's merchants already cover them.
- g: a candidate after step 6.
- 
  Answer : use proposals;

<a id="town-18"></a>
**TOWN-18 Light and weather in the town.** One fixed mood per faction, or a time of day and weather? **Proposal**: one lighting set per
faction, no day cycle. The week's event shows lightly: banners of the week's creature, or a grey haze in a plague month.
Answer : use proposals;

<a id="town-19"></a>
**TOWN-19 How much the town grows in the scene.** Do the town hall's and fort's levels change the scene's own look (a village becoming a
walled city), beyond each building's model? **Proposal**: yes. The walls follow the fort level, as in a siege. The town hall's level
adds streets and houses as filler in three steps (village, town, city).
Answer : use proposals;

<a id="town-20"></a>
**TOWN-20 Creatures in the scene.** Does each built dwelling show its creature idling before it? **Proposal**: yes, one creature per
dwelling, once creature models exist (Phase 5 art). Until then the dwellings stand empty.
Answer : Does each built dwelling show its creature idling before it - no;

<a id="town-21"></a>
**TOWN-21 How the town art is made.** About 320 models: image-to-3D from concept art with clean-up, a kitbash kit per faction, or
commissioned work? Which factions come first? **Proposal**: a kit per faction for walls and roofs, and image-to-3D with clean-up for
the landmark buildings. Haven and Inferno first (the vertical slice, ADR-034). A `town-scene-prompts.md` file in the ADR-245 layout.
Answer : use proposals, however take into account latest design and atmosphere direction;

<a id="town-22"></a>
**TOWN-22 Heroes in the scene.** Does the visiting hero appear in the town, and the garrison hero? **Proposal**: the visiting hero's
model and mount wait at the gate. The garrison hero shows only as its banner on the keep.
Answer : the visiting hero's model and mount wait at the gate - no, only banner on the keep;

<a id="town-23"></a>
**TOWN-23 Entering and leaving.** How does the screen open from the map? **Proposal**: a 0.5 s fade through black into the hero shot,
skipped at instant speed. The adventure map's camera does not fly into the town model.
Answer : use proposals;

## Risks

| Risk | Effect | Answer |
|---|---|---|
| Art volume (about 320 models) | The overhaul waits on art for months | Greybox makes every step playable; two factions first; a kit per faction |
| Performance on the Deck | Under 40 fps with fog and shadows | A budget per scene from step 3, a Deck preset, LOD, baked AO |
| Loading time | A pause on entering a town | Load ahead when a route ends at a town; one cached scene |
| A busy 3D scene hides information | Players miss states | The Hall and the bottom bar hold every number; the scene is never the only place a state shows |
| Scope creep from TOWN-17 | Rules change late | Each adopted mechanic gets its own ADR and tests in step 6, outside the screen work |
| Godot's UI theme limits | Frames and fonts look default | Step 1 builds the theme and widgets once, used by every screen |
| Network and replay | A UI action not repeatable | Every action stays a command (ADR-036); the scene and windows only read state |
