# Battle HUD redesign: guide and plan

The plan to restyle the battle's HUD in the adventure HUD's look (ADR-309, the adventure HUD's decision 11.5). The round-1 mockups are the
[Battle HUD mockups canvas](https://claude.ai/artifact/JJ89FYmLpmN7CvywfbGGQb), drawn from the prompt in
[adventure-hud-plan.md](adventure-hud-plan.md#battle-hud-round-1); the decision table is the Claude doc
[Battle HUD redesign: discrepancies and decisions](https://claude.ai/artifact/HdAjCooerLirLXZX7ifxMd), whose row numbers (such as 2.4)
are cited below. The guide states each row's recommendation; a row decided otherwise changes the guide before step 1 records it. When
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
|                                                         +--------------------+ |
|                                                         | log, 3 lines       | |
| +-----------------+ +-------------------------------+ +--------------------+ |
| | Stack panel     | | Timeline: active, queue | wait | | Wait  Defend Quick | |
| | portrait, icons | |                               | | Retr. Surr.  Book  | |
| +-----------------+ +-------------------------------+ +--------------------+ |
| Status bar: subject . detail (action, estimate, reasons)                  Menu |
+---------------------------------------------------------------------------------+
```

### Rules for every part

- **Colours.** Frames bronze on dark slate, the adventure HUD's painted pieces (ADR-294). Each side's portrait, timeline rims, stack
  bases and stack-panel rim take its player's colour; neutrals take bone grey (1.2, 1.3).
- **Icons, no labels.** Buttons show an icon; the name, hotkey and, when greyed, the reason go to the status bar on hover (5.3).
- **One height along the bottom.** The stack panel, the timeline and the action grid are panels of the same height, edges in line.
- **The status bar talks.** The prompt line ("Swordsmen to act"), refusals, the other LAN player's move and what a click on the field
  would do all go to the status bar (7.1, 7.2, 10.4).

### The parts

**Side plates** (2.1–2.3): top left the attacker, top right the defender: the hero's portrait in the side's colour and the name; a side
without a hero shows its strongest stack's portrait, a garrison its town's crest. A side with a caster shows its mana as a thin blue bar
and "14 / 24". A click on the portrait opens the hero's information window.

**Round plate** (2.5, 8.4): top centre, "Round 3", "Deployment" during the deployment step; a timed game's pill stands under it.

**Spell book** (2.4): a button beside the commanding side's plate and in the action grid, on C. It opens a window: the hero's battle
spells by school with rank and cost, greyed with the reason (cast this round, not enough mana); picking one arms it as today. A creature
caster's own spell is a button in the action grid.

**Timeline** (3.1–3.5): in the bottom row by default; the Layout tab's "top" puts it under the round plate. Square portraits with the
count in the corner, rimmed in the side's colour; the active stack first and larger with a bright rim; a bronze divider, then the
waiting stacks dimmed. As many as fit. Hover lights the stack on the field and shows it in the stack panel; right click opens its
information window.

**Stack panel** (4.1–4.4): bottom left; the hovered stack, else the active one: portrait, name and count, attack, defence, damage, health,
speed, initiative as icons with numbers, shots for a shooter, morale and luck; defending, native terrain and the spells on it as small
marks named on the status bar. A war machine shows the numbers it has.

**Action grid** (5.1, 5.2, 2.6): bottom right, 3 × 2: Wait (W), Defend (D), Quick combat (Q) / Retreat (R), Surrender, Spell book (C).
The creature caster's spell and the warcries add a column when present. Menu (F10) stays on the status bar's right end.

**Combat log** (6.1, 6.2): above the action grid, 3 lines, semi-transparent; hovering it grows it upwards to the battle's last 20
lines. The LAN chat stands at the bottom left above the stack panel (6.3).

**Attack estimate** (7.3): hovering an enemy puts "Attack the Wolves: 38–45 damage, 2 may die" on the status bar: the lowest and highest
damage of the blow, shot or damage spell without luck, and the kills each would make; morale, luck and the return blow left out.

**Deployment** (8.1–8.3): the deploying player's columns tinted in their colour; no timeline; a wide "Start battle" button in the middle
of the bottom row and Quick combat in the grid.

**Result and Necromancy** (9.1–9.5): the field dimmed; a framed window titled "Victory" or "Defeat" for the player at this screen ("<Name>
wins" when both or neither side play here), the reason under it, each side's losses as portraits with counts, the experience gained,
Return to the map. Raised undead get their own window after it: the stack's portrait and count, Continue. The auto-combat question takes
the same look: the expected losses as portraits, Fight and Accept.

**Sizes and modes** (10.1–10.5): below about 1,400 px of UI width the stack panel keeps the portrait, count, attack, defence and health,
the log 2 lines; the UI scale is lowered as on the map. No mirror: the attacker stays left. In the replay viewer the action grid is hidden
and the spell book greyed. A siege adds nothing to the HUD.

## Plan

Five steps, as for the adventure HUD: record, Core, the HUD on the flat theme, check, painted pieces. Each ends with the build and tests
green and its memory-bank entries updated.

### 1. Record the decisions
- [ ] The owner fills the Decision column; the guide above is brought in line with it.
- [ ] ADR-311 (presentation): the battle HUD restyle, citing this paper; it supersedes in part ADR-156 (the initiative bar's default
  place), ADR-110 (spells as buttons) and ADR-195 (the chat's place over a battle). Index row, next free number 312; roadmap; active
  context.

### 2. Core (engine-free, with tests)
- [ ] The attack estimate (7.3): `AttackEstimate` on `DamageCalculator.Damage` and `Expected`, the damage range and the kills of a blow, a shot and a damage
  spell, without luck, morale or the return blow.
- [ ] The battle's hotkey table as data the status bar and `BattleInputController` both read (W, D, Q, R, C, Enter, Escape), as
  `AdventureHotkeys` does.
- [ ] The spell book's rows (2.4): the commanding side's battle spells by school, rank, cost and the refusal (`BattleMagic`).
- [ ] The stack panel's numbers and marks (4.2) and the result's summary (9.1, 9.2): the title for a viewer, the reason, each side's
  losses.

### 3. The HUD, on the flat theme
- [ ] `BattleHudLayout`: the bottom row of equal panels, the side plates, the round plate and the timer pill; the Layout tab's top
  timeline.
- [ ] Timeline entries, the stack panel, the action grid with names, keys and reasons on the status bar; the spell book window.
- [ ] The log above the grid, growing on hover; the chat's new place; the prompt line, refusals and the attack estimate on the status
  bar.
- [ ] Deployment: the tinted zone, Start battle in the middle. The result, Necromancy and auto-combat windows; the field dimmed behind
  them.
- [ ] The narrow form below about 1,400 px of UI width; the replay viewer and the LAN wait.

### 4. Check
- [ ] Smoke screenshots of a battle at the adventure HUD's sizes, UI scales and with the timeline on top and at the bottom (as `hud-*`).
- [ ] The battle smokes (`game-battle-*`, `game-siege*`, `game-necromancy`, the replay viewer's battle) and a LAN battle pass.
- [ ] Budget overlay (F3) in a full battle: under the 2,000 draw calls (ADR-308).

### 5. Painted pieces
- [ ] The stat icons, the spell book and warcry buttons and the waiting divider in
  [icon-prompts.md](../content/prompts/icon-prompts.md) and [ui-element-prompts.md](../content/prompts/ui-element-prompts.md);
  generated and cut.
- [ ] Swapped in for the flat boxes; the battle part of [presentation.md](../design/presentation.md) rewritten to state the rules once.
