# Town screen overhaul: study and plan

The plan to rebuild the town screen (ADR-246): a **3D town scene** as the main screen, with the windows of a full town screen around it
(the building tree, recruitment, the mage guild, the tavern, the marketplace, the blacksmith and the rest). The quality target is a
current commercial strategy game. In function the screen should match HoMM3 with HotA and HoMM5, keeping this game's own rules.

This file has five parts. The **study** looks at what we have and what the references do. The **target** sets the bar. **Screens**
describes each screen. **Technical design** and **art and audio** cover how to build it. **Roadmap**, **questions** and **risks** close
it. Nothing here is built yet. The questions (TOWN-14 … TOWN-23) are answered (ADR-292, with ADR-291 for the plots), and the plan
follows the answers.
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
  - Drag a hero's portrait to the other row to swap (`SwapTownHeroesCommand`, ADR-294), or, with one hero, to move it in or
    out (`EnterGarrisonCommand`, `LeaveGarrisonCommand`).
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
- **Streets follow the town hall** (TOWN-19): streets and houses fill the space between the buildings in three steps, village, town and
  city, by the town hall's level; the Capitol adds the citizens' homes on the closed plots.
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
  - the garrison hero's banner on the keep; no hero models in the scene (TOWN-22);
  - no creatures in the scene (TOWN-20).
  - Ambient sound per building, louder near the camera's focus.
- **Mood** (TOWN-18): one lighting set per faction, taken from the faction's key painting (ADR-284), as the art direction asks
  (low sun, fog, strong shadows). No day cycle. The week's event can tint the scene: banners of the week's creature, or a grey haze in a plague month.
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
  - every model follows the faction's key paintings and [faction-art.md](../content/faction-art.md) (ADR-284), realistic, not cartoon
    (ADR-248);
  - concept art per faction first: the key paintings exist, then a sheet of the main buildings;
  - then models by an image-to-3D model with clean-up, or kitbashed from a shared kit per faction (roofs, walls, windows, props).
  - A new prompt file, `town-scene-prompts.md`, in the same layout as the others (ADR-245). It reuses each faction's look from
    [town-model-prompts.md](../content/prompts/town-model-prompts.md).
- **Building pictures** for the Hall: rendered from the finished models in a fixed studio light (a tool scene), so the pictures always
  match the scene. Greybox renders stand in until then.
- **Audio**: a town theme per faction (8; the roadmap's "town themes"), an ambient loop per diorama, and short sounds for build,
  recruit, trade, hire, buy, the hero's arrival and windows opening and closing.

## Roadmap

Six steps after the decisions. Each ends in a working game with a green test suite, and steps 1 and 2 already help players. The
roadmap's checkboxes are in [roadmap.md](../../memory-bank/roadmap.md) under "Town screen overhaul".

| Step | What | Depends on | Done when |
|---|---|---|---|
| 0 | **Decide and sketch**: the answers (ADR-292), a greybox shell of the new screen, Haven's style frame | — | The owner signs off the shell and the style frame |
| 1 | **UI foundation**: fonts, frames, the shared widgets, hotkeys and focus order | 0 | Hero, exchange and today's town screens use the widgets; drag, split and swap work |
| 2 | **The windows over a 2D main screen**, with the core read models and the adopted TOWN-17 rules | 1 | Every action of today's screen is in a window; the scrolling list is gone from the game app |
| 3 | **The 3D scene in greybox** for the eight playable towns | 2 | The game app's town screen is the 3D scene for every playable town, at budget |
| 4 | **Vertical slice art**: Haven and Inferno (ADR-034) | 3 | Both towns meet every item of the [target](#target) |
| 5 | **The other six factions** | 4 | All eight at the slice's quality |
| 6 | **Polish**: controller, UI scale, the goal building, the final check | 5 | The target list is ticked |

Conflux and Dragon Utopia are not playable, so they get no town screen.

### Implementation plan

The tasks of each step in build order. A task is one reviewable change that leaves the game working.

**Every task**:
- Core first. A rule or a read model that can be engine-free goes into `src/Core/**` with tests in `tests/EternalWars.Core.Tests`
  (`TestWorld` towns). A new action is a command (ADR-036) that publishes a past-tense event after the change; randomness only from
  `GameState.Random`. A new rule gets its own ADR.
- Then the Godot part: widgets and windows in `EternalWars.Godot.UI/Hud/TownScreen/` as `ScreenSection`-based parts rebuilt on their
  own events (ADR-223); the 3D scene in `EternalWars.Godot.Shared/Town/`. Views subscribe with `SubscribeWhileInTree`; the game app
  only composes and maps input.
- A smoke scenario in `tools/smoke-godot.ps1` where the task changes what the player sees, saving screenshots; the existing ones keep
  passing.
- `dotnet build` and `dotnet test` green. A save format change bumps its version and keeps older saves loading.
- At the end of a step: tick [roadmap.md](../../memory-bank/roadmap.md), refresh `active-context.md`, and move the rules that are now
  built into [towns-and-economy.md](../design/towns-and-economy.md) and [presentation.md](../design/presentation.md).

#### Step 0: Decide and sketch

1. **Decisions**: TOWN-14 … TOWN-23 answered (ADR-292); the Capitol's dwellings and the plots' looks (ADR-291). *Done.*
2. **Greybox shell**: `TownScreen` in `Hud/TownScreen/` with the top bar, the bottom bar, a window host (one window at a time; Escape
   closes the window, a second Escape leaves) and an empty, titled window per hotkey. The main area shows the faction's key painting
   (`docs/content/art/towns/`, copied to the assets) as a stand-in. Opened instead of `TownPanel` with a launch option
   (`--town-screen=new`) until step 2 ends. It is not thrown away: steps 1 and 2 fill it. Smoke `game-town-shell`. *Done (ADR-294); the
   launch option went with step 2 (ADR-296).*
3. **Style frame and look-dev spike**: `scenes/towns/haven.tscn` blocked out after `key_haven-1` (lakeside, castle with conical-roofed
   towers, one focal point, low calm foreground), with its light, fog and grading (TOWN-18: the painting's light is the faction's
   lighting set). It settles the renderer settings (SDFGI, VoxelGI or none; fog; SSAO) and the first budget numbers on the reference PC
   and the Deck. Screenshots beside the painting for the owner. *Done (ADR-294): smoke `town-style-frame-haven`; the budget numbers
   wait for the reference PC and the Deck.*

#### Step 1: UI foundation

1. **Fonts**: a heading face and a body face, open licences, in `fonts/` with their credits; `UiTheme` uses them everywhere; text at
   least 16 px at 1080p. *(proposal)* The agent proposes two pairs as screenshots of today's screens; the owner picks. *Done
   (ADR-294): Cinzel with Alegreya Sans (`game-town-shell`) and Marcellus SC with Fira Sans (`game-fonts-b`); the owner's pick is open.*
2. **Frames and theme**: `WindowFrame` (nine-slice, the faction's accent on the frame) and the button, slot and tab styles in
   `UiTheme`, from greybox textures until the UI art of [ui-element-prompts.md](../content/prompts/ui-element-prompts.md) exists.
   Today's panels switch to them, so no default Godot control shows. *Done (ADR-294).*
3. **`CostLabel`**: a cost with what the player lacks marked. Used by today's building tree, tavern, blacksmith and marketplace.
   *Done (ADR-294).*
4. **`SliderDialog`**: a count with − / +, Max, the cost of one, the total and the treasury after paying (red where short). *Done
   (ADR-294).*
5. **Stack moves in core**: an engine-free helper for "spread a stack evenly over the empty slots" as a list of `MoveTownStackCommand`
   / `MoveStackCommand`s (both already take a count), and a `SwapTownHeroesCommand` that swaps the garrison and visiting heroes in one
   step (today `EnterGarrisonCommand` and `LeaveGarrisonCommand` refuse when both are there). ADR, tests. *Done (ADR-294).*
6. **`ArmyBar`**: seven slots led by a hero portrait (an empty frame without one). Drag onto a slot moves, merges or swaps;
   Shift + drag opens the split dialog; Ctrl + drag moves one creature; Alt + click spreads; double click opens the creature's info;
   an arrow badge on a stack the town can upgrade, with the price in the tooltip (`UpgradeStackCommand`); dragging a hero portrait to
   the other row swaps the heroes. Used by the hero screen, the exchange screen and today's town armies section, which loses its
   global "Split" and "Recruit to visiting hero" checkboxes. Smokes `game-town-armies`, `game-hero-armies`. *Done (ADR-294).*
7. **`CreatureCard` and `BuildingTile`**: the card with portrait, base / upgraded toggle, stats and abilities as icons, waiting,
   growth and cost; the tile with picture, level n of m and the six states by frame **and** symbol (built out ✓, can build hammer,
   can't afford coin, needs buildings chain, built today hourglass, locked for ever ✗). Core: the tree's status gains "locked for
   ever" for a closed dwelling line (`BuildingTree.DwellingChoices`). Greybox building pictures until step 4. *Done (ADR-294):
   `BuildingStatus.Closed`, `BuildingTree.DwellingStatus`.*
8. **Hotkeys and focus order**: the town screen's keys in the app's input map (B, R, G, V, M, S, F, C, U, [ ], H, Space, Escape), each
   shown on the status bar with its button (ADR-204); every window gets a focus order for the keyboard and the controller. *Done
   (ADR-294): `TownScreenKeys`, `FocusOrder`.*

#### Step 2: The windows over a 2D main screen

Core first (tasks 1–7), then the screen (8–16). Each TOWN-17 rule gets its own ADR with its numbers.

1. **`TownOverview`**: income; each line's growth with its parts (base, fort, growth building, week event, artifacts, the Grail);
   built today; the governor's discount; each plot's state (not built, built today, built at level n, closed, citizens' homes once the
   Capitol stands, ADR-291). Tests. *Done (ADR-296): next week's event is left out of the growth (it is drawn on Monday); the homes show while the Capitol stands; a captured Capitol that dropped keeping
   them waits for step 3.*
2. **Recruit all** (TOWN-17 a): `RecruitPlan` (the most of every line the treasury allows, top tier first, into the garrison, then
   the visiting hero where it fits) and `RecruitManyCommand` (a list of orders, all or nothing, one event). Tests for the order, a
   short treasury and full armies. *Done (ADR-296): `TownPlans.Recruit`.*
3. **Upgrade all** (TOWN-17 b): `UpgradePlan` (every stack in both rows the town can upgrade and the player can pay, best first) and
   `UpgradeAllCommand`. Tests. *Done (ADR-296): `TownPlans.Upgrade`.*
4. **Dismiss a stack** (TOWN-17 i): `DismissStackCommand` for a town or hero stack, never a hero's last one. Tests. *Done (ADR-296).*
5. **Rename a town** (TOWN-17 j): `RenameTownCommand` (own towns, trimmed, a length limit), kept in saves and replays. Tests. *Done (ADR-296).*
6. **Tavern rumours** (TOWN-17 h): a rumour table in `data/`, one line a week for each player drawn from `GameState.Random` on
   Monday, some lines filled from the game (the strongest player, an unvisited treasure). Saved. Tests. *Done (ADR-296), drawn instead
   from a source seeded by the game's seed, the player and the week, as the tavern's offers are: the same all week, given back by replays
   and saves without storing it, and the game's random source untouched; a map's own rumours still come first.*
7. **Trading** (TOWN-17 c, e): any resource for any other at the marketplace, at A's sell price divided by B's buy price, by the
   marketplace count (ADR-067); sending resources to a teammate only (`SendResourcesCommand`). The AI may use both. Tests, and the
   replay names. *Done (ADR-296): `ExchangeResourcesCommand`, `SendResourcesCommand` (from a player with a marketplace town); the AI
   does not use them yet.*
8. **The screen's frame**: the top bar (crest, name with rename, ◄ ► through the player's towns, faction, town hall, governor, quick
   buttons, ×), the bottom bar (`ArmyBar` rows with Split, Upgrade all, Swap and Dismiss; the town's portrait with the governor;
   income, growth with its breakdown on hover, the date) and the notices card (built today, caravans, the discount). The 2D main
   screen is the key painting with a strip of building buttons, until step 3. Smokes `game-town-open`, `game-town-switch`. *Done
   (ADR-296).*
9. **Hall**: columns by kind (Core, Military, Magic, Dwellings, Unique; the kind is a column in `buildings.csv`), the requirement
   links as lines, hovering lights a building's chain; the detail card (cost with what is lacking, ✓ / ✗ requirements that select,
   what it gives, the refusal, Build); the 7-of-9 block with the closed lines crossed through in the preview. "Show in town" waits for
   step 3. Smoke `game-town-build`. *Done (ADR-296): the dwellings' block is a column per tier, so it is two tiles high.*
10. **Recruitment**: a card per line (unbuilt ones greyed, "build in the Hall"), the recruit dialog (`SliderDialog`, destination
    Garrison or Visiting hero preselected to where it fits), Recruit all with its summary before paying; the dwelling window (the
    large card, the dialog, Upgrade the dwelling). Smoke `game-town-recruit`. *Done (ADR-296).*
11. **Mage guild and war lodge**: shelves per built level with spell icons (the spell buildings' extras framed in their school's
    colour), dark shelves saying what they need; the visiting hero's known, newly learned and too-hard spells; Buy a spell book moves
    here from the blacksmith. *Done (ADR-296).*
12. **Tavern**: `TavernPanel` in the new frame, hero cards with portraits, the returning-hero badge, the week's rumour, the Thieves'
    Guild; the next hire in the tavern's hover text. *Done (ADR-296): its cards are `TavernCard`s in the tavern window,
    which replaces `TavernPanel`; the crest stands in for the portraits, which need art.*
13. **Marketplace**: two columns of six resource tiles, the rate between them, slider, Max, Trade; the Silo's income; a "Send to
    ally" tab where a teammate exists. *Done (ADR-296): the resources as toggle buttons; sending below the trade.*
14. **Blacksmith, Fort, Caravan**: three machine cards (greyed with the reason without a visiting hero); the defences window (walls,
    towers, gate and moat at their level, the next level's gain, the unique building's siege bonus); the caravan window with cards of
    the dwellings in reach and the queue. *Done (ADR-296): the defences in words until their picture; the caravan's rows as today's.*
15. **The rest**: the governor list from the portrait slot (each hero's discount); the Transformer's stacks with previews; info
    windows for the unique buildings, the Grail building and the Observatory from `InfoSheets`; a right click on everything opens its
    information window (ADR-154). Smoke `game-town-windows`. *Done (ADR-296): a building without a window shows its information on a
    click of its strip button.*
16. **Switch over**: the game app opens the new screen only and drops the launch option; `TownPanel` stays in the 2D harness as a tool
    (TOWN-16); `game-town-screen` moves to the new screen. Every action of the old screen is checked off in a window. *Done (ADR-296).*

#### Step 3: The 3D scene in greybox

1. **`TownScene3D`**: a `Node3D` with its own camera, light and `WorldEnvironment`; the game app hides the adventure world while it is
   open, as for a battle. It reads a layout (`scenes/towns/<town>.tscn`, `Marker3D` plots named `plot_<building>` and
   `plot_dwelling-<line>`, each with a hover shape and a camera focus) and the town's `TownOverview`. A plot without a model gets a
   procedural block sized by building kind, with a roof and a sign. Raises `BuildingHovered` and `BuildingPicked`. *Done (ADR-297):
   Haven's layout from its style frame (its buildings as the plots' own models); plots named with dots as underscores; picking by a ray
   against the hover boxes; the town screen shows the scene where a layout exists and the painting elsewhere.*
2. **Layouts for the eight towns**, each composed after its key painting ([faction-art.md](../content/faction-art.md)): the landmark
   as the focal point, the setting around a fixed 60 × 40 m ground, the `bg_town_<town>` picture as the sky. A smoke check
   `game-town-layouts` loads each one and fails on a building or line without a plot.
3. **Plot states**: unused land (a piece of the diorama's scenery, no markers or rubble), the model for its level, scaffolding on the
   day it is built, the citizens' homes on the two closed lines once the Capitol stands (ADR-291); walls by fort level (the siege's
   parts); streets and houses as filler by town hall level in three steps, village, town and city (TOWN-19); banners in the player's
   colour; the garrison hero's banner on the keep (TOWN-22).
4. **Camera** (TOWN-14): the hero shot, turned ±25° and zoomed in two steps; a click opens the window at once while the camera eases
   toward the building, and eases back after Escape.
5. **Hover and pick**: the bronze outline, name and state on the status line, the hand cursor, right click for the info window;
   picking by hover shape; the controller walks a ring of plots. "Show in town" from the Hall shows a ghost of the model on its plot.
6. **Construction scene**: on `TownBuilt` / `DwellingBuilt`, 1.5 s at the plot (scaffold, dust, the model rising, a bell), cut short
   or skipped by the speed settings (ADR-090); the Capitol's scene also raises the homes.
7. **Arrival and loading** (TOWN-23): a 0.5 s fade through black into the hero shot, skipped at instant speed; the town type's scene
   loaded in the background when an own hero's route ends at an own town or at the turn's start for the selected town; one cached
   scene. Opens in under 1 s.
8. **Budget**: an overlay with triangles, draw calls, video memory and frame time; a fully built greybox Haven and Inferno measured at
   1440p and on the Deck; the numbers set in this plan. Smokes `game-town-open` and `game-town-build` now show the 3D scene.

#### Step 4: Vertical slice art: Haven and Inferno

1. **Prompts**: `town-scene-prompts.md` in the ADR-245 layout, paste-ready (ADR-249), realistic (ADR-248), following the key paintings
   and [faction-art.md](../content/faction-art.md) (ADR-284): each faction's kit (walls, roofs, windows, props) and its landmark
   buildings, one block a sheet. The owner generates the concept sheets.
2. **Model pipeline**: the import convention (`models/towns/<town>/<building>_<level>.glb`), LODs, baked ambient occlusion, a check
   that every model is within its share of the budget; image-to-3D for the landmarks, the kit for the rest (TOWN-21).
3. **Haven**: every building, every level and the nine dwellings and their upgrades; the citizens' homes; the walls; the street
   filler; the diorama around it.
4. **Mood** (TOWN-18): the lighting set from `key_haven-1`; the week's tint (banners of the week's creature, a grey haze in a plague
   month), with `key_haven-2`'s overcast light as the tint's reference. No day cycle.
5. **Life**: banners, smoke from the blacksmith, the guild's glow, water, birds; ambient sound per building, louder near the camera's
   focus. No creatures (TOWN-20) and no hero models (TOWN-22) in the scene.
6. **Audio**: the game's first sound and music. Buses and volume settings, the Haven town theme, the diorama's ambient loop, and the
   sounds for build, recruit, trade, hire, buy, the hero's arrival and windows opening and closing.
7. **Hall pictures**: a tool scene that renders each building in a fixed studio light into the Hall's tiles.
8. **Deck preset**: half-resolution fog, no SSAO, shadows at 2048; measured.
9. **Inferno**: tasks 3–5 and its theme and ambience, to the same check.
10. **The check**: both towns against every item of the [target](#target), with a frame-time capture; what fails is fixed before
    step 5.

#### Step 5: The other six factions

One task per faction, each tasks 3–5 of step 4 plus its theme, ambience and Hall pictures, measured against the budget: Sylvan,
Academy, Necropolis, Dungeon, Stronghold, Swamp Bestiary.

#### Step 6: Polish

1. **Controller pass**: every window and the scene with the Deck's controls; the shoulder buttons switch towns.
2. **UI scale** (100–150 %) as a setting, and the accessibility check (no state by colour alone, text sizes, shortened animations).
3. **Goal building** (TOWN-17 k): pin one building as the town's goal; the card shows the days of income needed, and the town list
   marks the town once it can afford it. UI only.
4. **Spell research** (TOWN-17 g): asked again as a candidate, not built unless the owner adopts it.
5. **The final check** against the [target](#target); this file stays in `papers/` as the report (ADR-280).

## Questions

All answered by the owner on 2026-11-22 (ADR-292; TOWN-15 with ADR-291). The answers stay here as given.

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
| Scope creep from TOWN-17 | Rules change late | Each adopted mechanic gets its own ADR and tests as a core task of step 2, before the window that uses it |
| Godot's UI theme limits | Frames and fonts look default | Step 1 builds the theme and widgets once, used by every screen |
| Network and replay | A UI action not repeatable | Every action stays a command (ADR-036); the scene and windows only read state |
