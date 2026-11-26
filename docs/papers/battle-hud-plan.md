# Battle HUD redesign: guide and plan

The plan to restyle the battle's HUD in the adventure HUD's look (ADR-309, the adventure HUD's decision 11.5). The round-1 mockups are the
[Battle HUD mockups canvas](https://claude.ai/artifact/JJ89FYmLpmN7CvywfbGGQb), drawn from the prompt in
[adventure-hud-plan.md](adventure-hud-plan.md#battle-hud-round-1); the decision table is the Claude doc
[Battle HUD redesign: discrepancies and decisions](https://claude.ai/artifact/HdAjCooerLirLXZX7ifxMd), whose row numbers (such as 2.4)
are cited below. The owner decided every row on 2026-11-26: the recommendation, except 2.2 (the opponent's hero by Scouting), 2.6
(warcries in the spell book), 5.1 and 5.5 (a settings button and Menu in the action grid), 7.2 and 7.3 (the kills as a range, whether
the target strikes back) and 8.3 (settings and Menu during deployment). The guide states the decisions; ADR-314 records them. When
every step is built, this file stays in `papers/` as the report and the rules move into [presentation.md](../design/presentation.md).

## Guide

### Screen at a glance

```text
+---------------------------------------------------------------------------------+
| [M] Mara   [book]              [ Round 3 ]                    Neutral wolves [W]|
|     mana 14/24                 [timer pill]                                     |
|                                                                                 |
|                      3D battlefield, square grid 30 x 24 (36 in a siege)        |
|                                                                                 |
|                                                   +--------------------------+ |
|                                                   | log, 3 lines             | |
| +-----------------+ +-------------------------+ +--------------------------+ |
| | Stack panel     | | Timeline: active | wait | | Wait  Defend Quick Sett. | |
| | portrait, icons | |                         | | Retr. Surr.  Book  Menu  | |
| +-----------------+ +-------------------------+ +--------------------------+ |
| Status bar: subject . detail (action, estimate, kills, strikes back, reasons)  |
+---------------------------------------------------------------------------------+
```

### Rules for every part

- **Colours.** The frames are the adventure HUD's, drawn by the bevel shader (ADR-310, ADR-311; 1.3). Each side's portrait, timeline
  rims, stack bases and stack-panel rim take its player's colour; neutrals take bone grey (1.2).
- **Icons, no labels.** Buttons show an icon; the name, hotkey and, when greyed, the reason go to the status bar on hover (5.3).
- **One height along the bottom.** The stack panel, the timeline and the action grid are panels of the same height, edges in line.
- **The status bar talks.** The prompt line ("Swordsmen to act"), refusals, the other LAN player's move and what a click on the field
  would do all go to the status bar (7.1, 7.2, 10.4).

### The parts

**Side plates** (2.1–2.3): top left the attacker, top right the defender: the hero's portrait in the side's colour and the name; a side
without a hero shows its strongest stack's portrait, a garrison its town's crest. A side with a caster shows its mana as a thin blue bar
and "14 / 24". A click on the portrait opens the hero's information window: one's own hero in full, the opponent's as the Scouting of
the viewer's hero in the battle shows it (ADR-125: level and primary stats from Basic, war machines from Advanced, skills and spells
from Expert).

**Round plate** (2.5, 8.4): top centre, "Round 3", "Deployment" during the deployment step; a timed game's pill stands under it.

**Spell book** (2.4, 2.6): a button beside the commanding side's plate and in the action grid, on C. It opens a window: the hero's battle
spells by school with rank and cost, and the warcries the hero knows as a category of their own (ADR-067), each greyed with the reason
(cast this round, not enough mana); picking one arms or cries it as today. Warcries have no buttons of their own. A creature caster's own
spell is a button in the action grid.

**Timeline** (3.1–3.5): in the bottom row by default; the Layout tab's "top" puts it under the round plate. Square portraits with the
count in the corner, rimmed in the side's colour; the active stack first and larger with a bright rim; a bronze divider, then the
waiting stacks dimmed. As many as fit. Hover lights the stack on the field and shows it in the stack panel; right click opens its
information window.

**Stack panel** (4.1–4.4): bottom left; the hovered stack, else the active one: portrait, name and count, attack, defence, damage, health,
speed, initiative as icons with numbers, shots for a shooter, morale and luck; defending, native terrain and the spells on it as small
marks named on the status bar. A war machine shows the numbers it has.

**Action grid** (5.1, 5.2, 5.5): bottom right, 4 × 2: Wait (W), Defend (D), Quick combat (Q), Combat settings / Retreat (R),
Surrender, Spell book (C), Menu (F10). The creature caster's spell adds a column when present. Surrender has no key. Surrender's price
and a greyed button's reason are on the status bar on hover (5.3, 5.4).

**Combat settings** (5.1): a window over the battle with the battle's own view settings, kept per computer in the settings file and
changing no rule: the battle's animation speed (today on the menu's Settings tab), the grid over the field, the active stack's reach
highlighted. Further view settings of the battle join it.

**Combat log** (6.1, 6.2): above the action grid, 3 lines, semi-transparent; hovering it grows it upwards to the battle's last 20
lines. The LAN chat stands at the bottom left above the stack panel (6.3).

**Attack estimate** (7.2, 7.3): hovering an enemy puts "Swordsmen · Attack the Wolves: 38–45 damage, 1–2 die; they strike back" on the
status bar: the lowest and highest damage of the blow, shot or damage spell without luck, the fewest and most creatures those kill, and
whether the target returns the blow ("they strike back", a shooter with ranged retaliation shooting back too, or "no return blow": a
target with none left this round, an attacker that is not answered, a blow whose weakest roll kills the whole stack, a spell). Morale,
luck, a second blow and the return blow's own damage are left out.

**Deployment** (8.1–8.4): the deploying player's columns tinted in their colour; no timeline; a wide "Start battle" button in the middle
of the bottom row; the grid shows Quick combat, Combat settings and Menu.

**Result and Necromancy** (9.1–9.5): the field dimmed; a framed window titled "Victory" or "Defeat" for the player at this screen ("<Name>
wins" when both or neither side play here), the reason under it, each side's losses as portraits with counts, the experience gained,
Return to the map. Raised undead get their own window after it: the stack's portrait and count, Continue. The auto-combat question takes
the same look: the expected losses as portraits, Fight and Accept.

**Sizes and modes** (10.1–10.5): below about 1,400 px of UI width the stack panel keeps the portrait, count, attack, defence and health,
the log 2 lines; the UI scale is lowered as on the map. No mirror: the attacker stays left. In the replay viewer the action grid keeps
only Combat settings and Menu, and the spell book is greyed. A siege adds nothing to the HUD.

## Plan

Five steps, as for the adventure HUD: record, Core, the HUD in the bevel frames, check, painted pieces. Each ends with the build and tests
green and its memory-bank entries updated.

### 1. Record the decisions
- [x] The owner fills the Decision column (2026-11-26); the guide above is brought in line with it.
- [x] ADR-314 (presentation): the battle HUD restyle, citing this paper; it supersedes in part ADR-156 (the initiative bar's default
  place), ADR-110 (spells as buttons) and ADR-195 (the chat's place over a battle). Index row; roadmap; active context.

### 2. Core (engine-free, with tests)
- [x] The attack estimate (7.2, 7.3): `AttackEstimate.Melee`, `Shot` and `Spell` on `DamageCalculator.Damage` and
  `BattleMagic.DamagePlan`, the damage range and the fewest and most kills, without luck or morale; whether the target strikes back by
  the engine's own rule, now shared as `Retaliation.StrikesBack` and `ShootsBack`.
- [x] The battle's hotkey table as data the status bar and `BattleInputController` both read (W, D, Q, R, C, F10, Enter, Escape):
  `BattleHotkeys`, as `AdventureHotkeys` does; the battle's buttons take their hints from it ("Wait [W]"). C waits for the spell book
  window (step 3); F10 stays the app's menu key.
- [x] The spell book's rows (2.4, 2.6): `BattleSpellBook.Of`, the side's battle spells by school (the schools' order, then level), rank,
  cost and the refusal (`BattleMagic.BookRefusal`, which `CastRefusal` checks first), and the known warcries as their own category with
  `WarcryRefusal`'s; the battle's spell buttons read it until the window replaces them.
- [x] The opponent hero's information window by the viewer's Scouting in the battle (2.2): `AdventureIntel.ScoutBattleHero` at
  `ScoutingInBattle`, the Scouting of the viewer's hero in the battle, not of its heroes on the map (`ScoutHero`'s levels and Veil);
  `InfoSheets.BattleHero` without the army, which stands on the field, one's own hero with the battle's mana.
- [x] The combat settings (5.1): the battle's grid and reach highlighting, `InterfaceSettings.BattleGrid` and `BattleReach` (on by
  default; a file without them reads them on), kept in the settings file and shown on the menu's Settings tab under the battle's
  animation speed until the Combat settings window (step 3); `BattleView3D.ShowGrid` and `ShowReach` (the deployment zone shows either
  way). The initiative bar's default at the bottom (`LayoutSettings.InitiativeBar`, 3.1); a file that names Top keeps it.
- [x] The stack panel's numbers and marks (4.2) and the result's summary (9.1, 9.2): `StackPanel.Of`, the stack's Attack and Defense as
  it strikes and is struck now (`DamageCalculator.AttackOf`, `DefenseOf`), damage, health, speed, initiative, shots, morale (none for a
  creature without it) and luck, a war machine only the numbers it has, and its marks (defending, native terrain, each spell on it with
  how long it lasts and whose it is); `BattleSummary.Of`, "Victory" or "Defeat" when one side plays at this screen
  (`SidesPlayedHere`: a person's side, by the LAN's plays-here), "<Name> wins" otherwise, the reason (who retreated or surrendered, else who won: the round limit is no rout), each side's losses one
  entry a creature, the experience. The HUD's text reads both until step 3 draws them.

### 3. The HUD, in the bevel frames
- [x] `BattleHudLayout`: the bottom row of equal panels, the side plates, the round plate and the timer pill; the Layout tab's top
  timeline. The round plate is as tall as the map's turn banner, so the timer's pill stands under it unmoved; the timeline on top stands
  under both, between the plates. "Start battle" in the middle of the bottom row, the log above the action grid.
- [x] Timeline entries, the stack panel, the 4 × 2 action grid with names, keys and reasons on the status bar; the spell book window
  with its warcries; the combat settings window. `BattleTimeline` (hover shows the stack in the panel and lights it on the field,
  `BattleView3D.Light`; right click its window), `BattleStackPanel` (the numbers without their icon sheet show short names until step 5),
  `BattleSidePlate` (the portrait opens `InfoSheets.BattleHero`; the book's button beside the side it is for), `BattleSpellBookWindow`
  (C, the grid, the plate; greyed with the reason when it is not the side's turn), `CombatSettingsWindow` (the grid and reach leave the
  menu's Settings tab; the battle speed stays there too). The smokes cast from the book (C, then the spell). A garrison's town crest on a
  heroless plate waits: the side shows its strongest stack.
- [ ] The log above the grid, growing on hover; the chat's new place; the prompt line, refusals and the attack estimate on the status
  bar.
- [ ] Deployment: the tinted zone, Start battle in the middle, Quick combat, Combat settings and Menu. The result, Necromancy and
  auto-combat windows; the field dimmed behind them.
- [ ] The narrow form below about 1,400 px of UI width; the replay viewer and the LAN wait.

### 4. Check
- [ ] Smoke screenshots of a battle at the adventure HUD's sizes, UI scales and with the timeline on top and at the bottom (as `hud-*`).
- [ ] The battle smokes (`game-battle-*`, `game-siege*`, `game-necromancy`, the replay viewer's battle) and a LAN battle pass.
- [ ] Budget overlay (F3) in a full battle: under the 2,000 draw calls (ADR-308).

### 5. Painted pieces
- [ ] The frames, the timeline's divider and the plates are the bevel shader's (ADR-310, ADR-311); the buttons take the icons already
  cut (sheets 17 and 18, `spell_book`, `settings`, `menu`, `warcry` for the spell book's warcries). New: an icon sheet for the stack
  panel's numbers (damage, health, speed, initiative, shots) in [icon-prompts.md](../content/prompts/icon-prompts.md), generated and
  cut.
- [ ] The battle part of [presentation.md](../design/presentation.md) rewritten to state the rules once.
