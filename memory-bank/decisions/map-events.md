# Decisions: Map events

**Covers**: Pandora's Box, the event tree (triggers, conditions, actions), quests and quest objects, loops, campaign variables.

**Current rules**: [docs/design/adventure-map.md](../../docs/design/adventure-map.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [096](#adr-096-pandoras-box-and-map-events-hota-like-extended-events-2026-10-29) | Pandora's Box and map events (HotA-like extended events) (2026-10-29) | accepted |
| [105](#adr-105-pandoras-box-implementation-2026-10-30) | Pandora's Box (implementation, 2026-10-30) | implemented |
| [115](#adr-115-map-events-the-evt-1--evt-6-proposals-adr-096-2026-11-02) | Map events: the EVT-1 … EVT-6 proposals (ADR-096, 2026-11-02) | accepted |
| [124](#adr-124-map-events-step-1-in-core-the-event-tree-triggers-conditions-actions-map-format-v31-adr-096-adr-115-evt-1--evt-4-2026-11-04) | Map events, step 1 in core: the event tree, triggers, conditions, actions, map format v31 (ADR-096, ADR-115, EVT-1 … EVT-4, 2026-11-04) | implemented |
| [127](#adr-127-map-events-step-2-in-core-quests-the-seers-hut-quest-guard-and-quest-gate-the-quest-log-map-format-v32-adr-115-evt-5-2026-11-04) | Map events, step 2 in core: quests, the Seer's Hut, Quest Guard and Quest Gate, the quest log, map format v32 (ADR-115, EVT-5, 2026-11-04) | implemented |
| [128](#adr-128-map-events-the-game-apps-windows-and-quest-log-quest-object-shapes-step-3-loops-run-another-event-a-pandoras-boxs-hero-event-map-format-v33-adr-115-adr-124-adr-127-map-35-2026-11-04) | Map events: the game app's windows and quest log, quest object shapes; step 3: loops, "run another event", a Pandora's Box's hero event, map format v33 (ADR-115, ADR-124, ADR-127, MAP-35, 2026-11-04) | implemented |
| [129](#adr-129-map-events-step-4-the-ais-answers-and-quests-the-editors-events-tab-validator-checks-adr-115-evt-6-2026-11-05) | Map events, step 4: the AI's answers and quests, the editor's "Events" tab, validator checks (ADR-115, EVT-6, 2026-11-05) | implemented |
| [131](#adr-131-map-events-campaign-variables-and-the-score-check-map-format-v34-adr-101-adr-115-evt-1-f-flow-1-2026-11-05) | Map events: campaign variables and the score check, map format v34 (ADR-101, ADR-115, EVT-1 f, FLOW-1, 2026-11-05) | implemented |
| [132](#adr-132-map-events-the-camera-on-a-turn-start-message-adr-115-evt-2-f-2026-11-05) | Map events: the camera on a turn-start message (ADR-115, EVT-2 f, 2026-11-05) | implemented |
| [242](#adr-242-map-events-for-stories-dialogues-the-end-of-the-game-captured-and-battle-events-map-format-v42-adr-115-adr-241-2026-11-19) | Map events for stories: dialogues, the end of the game, captured and battle events, map format v42 (ADR-115, ADR-241, 2026-11-19) | implemented |


### ADR-096: Pandora's Box and map events (HotA-like extended events) (2026-10-29)
**Status**: accepted as a design (2026-10-29), owner notes in `ideas.md`; Pandora's Box built (ADR-105), the map events not. The open points were MAP-35 (answered in ADR-100) and
EVT-1 … EVT-6 in `open-questions.md`, each with a proposal. Reference: the "Extended event system" chapter of the HotA documentation
(h3hota.com/en/documentation).

- **Pandora's Box** (MAP-35): a map object, as in HoMM3 and HoMM4, that holds what the map author put in it (gold, resources, experience,
  skills, artifacts, creatures, …). A visiting hero is asked whether to open it; it is opened once and then vanishes.
- **Map events**: events the map author writes in the editor, as in HotA: a tree of **actions** and **conditions** over map-wide
  integer **variables**. Four types:
  - **Hero events**: run when a hero steps onto an event cell (an invisible map object) or opens a Pandora's Box; they act on that hero.
  - **Player events**: run at the start of a player's turn; they act on that player.
  - **Town events**: run at the start of the town owner's turn (neutral towns too); they act on that town.
  - **Quest events**: the quests of three new objects: the **Seer's Hut** (quests one after another, with a reward), the **Quest Guard**
    (blocks the way until its quest is done, then vanishes) and the **Quest Gate** (lets through the heroes that meet its quest). A quest has
    stages with a proposal, a progress and a completion message, and an entry in a **quest log**.
- **Conditions**: and / or / not and comparisons of expressions over resources, creature counts, primary and secondary skills, artifacts,
  the hero, its class, the player, human or computer, the player's starting town, the date, the difficulty, town owners, monsters and heroes
  defeated by a player, and variables. Expressions add, subtract, multiply, divide and draw random numbers.
- **Actions**: resources; creatures in a hero's army or a town's garrison; creatures to hire; a building; experience, primary and secondary
  skills, artifacts, war machines, spell book and spells, mana and movement points; morale and luck until the next battle; a battle against an
  army the author sets; messages, messages with rewards and questions with two or three answers; variables, branches, loops and running
  another event; for quests, quest stages, the quest log entry and "finish the quest".
- **Synchronised events**: copies of one hero event on several cells share one state and act as one event (one entry point per cell); a
  quest object always has its own copy.
- **The AI** runs events as a human does: it picks the answer worth the most, does a Seer's Hut quest when the reward outweighs the cost,
  takes Quest Guards and Gates as obstacles, and weighs a Pandora's Box by its contents.
- **Naming**: in this project an "event" is already a domain event of the pub/sub ([event-system.md](../../docs/technical/event-system.md)) and the
  weekly or monthly event (ADR-073); the docs call the author's events **map events** (EVT-1).
- **Fits the core as it is**: an event is data in the map file, run by a core service at its trigger; a question or a full army is a pending
  decision (`DecisionRequested`), as the Sphinx's riddle already is; random numbers draw from `GameState.Random`; morale and luck until the
  next battle are hero statuses (the Warrior's Tomb, the Sphinx); a battle against a set army is a guard battle; the difficulty is the
  complexity (ADR-063). New are variables, event cells, turn-start actions written by the author, quest objects, the quest log and a record of
  who defeated which monster or hero. Spells wait for Phase 5; campaign variables ("kept for the next map") wait for the campaign (FLOW-1).
- **Where in the plan** (EVT-1): Phase 6 lists "map triggers and events" for the campaign. The Pandora's Box with its own contents can come
  with the Phase 3 objects; the map events are a bigger step.

### ADR-105: Pandora's Box (implementation, 2026-10-30)
**Status**: implemented (2026-10-30); core tested, the Godot parts build but have not been run in Godot. Carries out ADR-100's Pandora's Box
(MAP-35, ADR-096). Map format **v27**, generator version **7**. Choices made here are marked *(choice)*.

- **The object** (`PandorasBox`, kind `PandorasBox`, definition `pandoras-box`, one cell): `Contents` (`BoxContents`: resources,
  experience, mana, morale, luck, the four primary stats, skills with a level, artifacts, creature stacks), an optional `Guard` (up to seven
  stacks) and an optional `Message`. Gains only: a negative value, morale or luck above +3, an unknown skill or a level the skill does not
  have are refused by the editor and are format errors in a map file.
- **A visit** asks "open?" (`PandorasBoxDecision`, answered by `AnswerPandorasBoxCommand`, record name `pandorasBox`), showing neither
  the contents nor the guard. Leaving keeps the box. A guarded box turns away a hero without an army *(choice)*.
- **The guard** fights after "open?", split like a creature bank's guard, with no Tactics choice and no deployment *(choice: like banks)*.
  The box opens only when the hero wins; a lost or fled battle leaves the guard whole, as at a bank *(choice)*.
- **Opening**: the box vanishes, then the hero gets resources, stats, mana (up to twice its maximum, as the Mana Vortex), morale and luck
  for its next battle, and skills (raised to at least the level given). Skills whose group the hero has no room for are lost, and so are
  artifacts that do not fit a full backpack and creatures without a slot *(choice: HoMM3 asks which stack to drop; here they are simply
  lost)*. Then `PandorasBoxOpened` reports what the hero actually got, with the message. The experience comes last, for its level-ups.
- **AI**: knows the contents and values them in gold: resources at their value, experience 1:1, a stat 750, morale and luck 150 a point,
  a skill 1000 a level gained, artifacts as pickups, creatures at 85 % where they fit, mana nothing until spells exist. It weighs that
  against the box's own guard (else the monsters next to it) through the usual simulated fight, and at the box opens only when that fight
  is safe.
- **Validator**: an empty box is an error; so are unknown artifacts and creatures, and contents that are not gains.
- **Editor**: the inspector has the box's resources, experience, mana, morale, luck, stats, skills, artifacts, creatures, guard and
  message. Each list shows its entries plus one empty row; every change is one undoable `SetPandorasBox` edit.
- **Generator** *(placeholders)*: from a value of 5000, one treasure item in five that is not a catalogue site is a box. It holds one of
  gold (the value, in steps of 500), experience (four fifths of it) or one stack of creatures worth the value, drawn evenly. It is guarded
  by a monster next to it like any item of its value, and has no guard of its own.
- **Front-end**: a decision prompt with Open and Leave, a notice with the message and what was inside, and the guard named "Pandora's box
  guard" in battle. Not built: spells (Phase 5), running a hero event instead (map events, EVT), and showing the contents on arena day 1
  (the arena).

### ADR-115: Map events: the EVT-1 … EVT-6 proposals (ADR-096, 2026-11-02)
**Status**: accepted (2026-11-02), the owner's answer to EVT-1 … EVT-6: "use the proposals". Implemented by ADR-124, ADR-127, ADR-128, ADR-129, ADR-131 and ADR-132.

- **Scope and plan** (EVT-1): all four event types in three steps: (1) hero, player and town events with conditions, variables, messages,
  questions and the main actions; (2) the quest objects and the quest log; (3) loops and "run another event". A Phase 5 item, after the
  Phase 3 and 4 exits and before the campaign, which builds on it. Called "map events" with an "Events" tab in the editor. Stored as a data
  tree in the map file (no script language: deterministic, safe to load, easy to show). "Simple event" forms for HoMM3's timed events, event
  cells and town events that write the same tree. Campaign variables are recorded now and built with the campaign (FLOW-1).
- **Triggers** (EVT-2): event cells are invisible and ignored by route planning; the route stops only when the event shows something or
  fights; a cell runs on every step until the event disables or removes itself ("once" is a ready form); who triggers is a condition (the
  ready forms' tick boxes write them); turn-start runs after income, growth and the week's event, neutral towns at the start of the day;
  several events at one moment in the editor's list order; a turn-start message shows when the turn begins and the camera moves to what it
  concerns.
- **Conditions** (EVT-3): HotA's whole list mapped to ours (complexity for difficulty, faction for starting town, four skill levels), plus
  the team, an ally and the day of the week now; map-wide whole-number variables; random numbers from the game's source; "defeated by"
  records only for the monsters and heroes an event names.
- **Actions** (EVT-4): HotA's whole list with our rules (skills to Master into a free slot, our war machines, a building ignores bans and
  adds its prerequisites, spells with Phase 5); losses as HotA (no lowering experience or skills); reveal an area and give a hero now; a
  battle action (won: experience and the event goes on; lost: the hero is lost and the event stops; retreat and surrender stop it); a full army
  chooses what to leave (a pending decision); messages are text with reward lines, images once there are icons.
- **Quests** (EVT-5): Seer's Hut, Quest Guard and Quest Gate (like the border gate, a quest for a key); HoMM3's quest forms as ready forms
  that take what they ask; a quest log per player (Q) in the game state; a hero that meets a gate's quest passes without stopping and routes
  treat the gate as open only for such a hero; no Seer's Huts on random maps for now.
- **AI and editor** (EVT-6): the AI answers questions by value (random numbers as their average), does quests worth their cost, treats Quest
  Guards and Gates as obstacles until it meets them and does not look ahead at event cells or player and town events; the "Events" tab (the
  tree, variables, shared events, undo through `MapEditorSession`), object inspectors pick or create their event; validator checks (unknown
  names, an event that runs itself, unused events and variables, an unfinishable quest where it can tell); events are tested by playing the map.

### ADR-124: Map events, step 1 in core: the event tree, triggers, conditions, actions, map format v31 (ADR-096, ADR-115, EVT-1 … EVT-4, 2026-11-04)
**Status**: implemented in core (2026-11-04). Quests: ADR-127; the game app's windows, loops and "run another event": ADR-128; the editor's
"Events" tab, validator warnings and the AI answering by value: ADR-129.

- **Data**: `MapEventSet` (events in list order, declared variables with starting values, event cells) is the domain's own tree, written to the
  map as it is (`events`, v31): every node carries a `type` name (`MapEventJson`), so there are no parallel document types. An event has a
  name, a kind (hero, player, town), its actions, whether it starts enabled and, for a town event, its towns. Invisible cells are not map
  objects: views, fog, routes and the AI never see them.
- **Running** (`MapEventService`): triggers queue runs; one runs at a time and pauses on a message, a question, a full army, a battle or
  anything else pending (a level-up); the session carries it on after every command. Level-ups from an event are offered once the events
  are done, and only after an event ran, so games without events play exactly as before. A message or question for a player or town event
  has no hero (`MapEventDecision.NoHero`).
- **Rules taken where ADR-115 left a choice**: a disabled event is skipped when its turn comes (an earlier event may disable a later one at
  the same moment); a message's reward amounts are drawn when it shows, so its lines tell what comes; a hero without an army cannot fight
  an event's battle and the event stops; a computer player is shown no messages and its full army keeps the strongest stacks; neutral
  towns' events run at the start of every day, day 1 included; "defeated by" counts battles won by defeat (not retreats or surrenders);
  a given hero moves at once at a turn start and from the next turn when a hero event gives it; a building ignores the day's build.
- **Loading**: `MapEventRules.Problems` refuses events that do not fit the map (duplicate names, undeclared variables, unknown skills,
  artifacts, spells, creatures, heroes or events, questions without two or three answers, cells off free passable ground or naming a
  non-hero event, town events naming non-towns).
- **Commands**: `AnswerMapEventCommand` (an answer, or 0 to close a message) and `ChooseEventArmyCommand` (the stacks to keep). Events:
  `MapEventRan`, `MapVariableChanged`, `MapEventEnabledChanged`, `MapEventCellRemoved`, `MapEventHeroGiven`, `MapEventSkillGained`,
  `MapEventSpellBookGained`, `MapEventSpellLearned`, `MapEventWarMachineGained`.

### ADR-127: Map events, step 2 in core: quests, the Seer's Hut, Quest Guard and Quest Gate, the quest log, map format v32 (ADR-115, EVT-5, 2026-11-04)
**Status**: implemented in core (2026-11-04). The quest log window, the object shapes and the inspector link: ADR-128; writing quests in the
editor and the AI weighing them: ADR-129.

- **Quests are map events** of a fourth kind, `Quest`: instead of actions a quest has **stages**, done one after another. A stage has a
  **requirement** (any condition of EVT-3, but no random numbers, so routes can test it), a **proposal** (first visit and the quest log), an
  optional **progress** text (later visits before it is met), an optional **completion** question and the **actions** run when the hero
  completes it: what it takes, then its rewards (any action, a battle included; a lost battle leaves the stage open).
- **Ready forms** (`QuestForms`): HoMM3's quests as stages that take what they ask: level, primary stats, artifacts, creatures, resources, beat a
  monster, beat a hero, be a hero, a class or a player. The editor will offer them; they write the same tree.
- **Quest objects** (`QuestSite`, `questSite` in the map): each names its **own** quest (two objects never share one; none = an empty hut, a
  closed barrier, a validator warning). A stage's progress is the object's, shared by all players.
  - **Seer's Hut** (a building): a visit that does not meet the stage shows the proposal or progress; one that does asks "Complete / Not now".
    Completing runs the actions, then shows the next stage's proposal; after the last stage the hut stands empty.
  - **Quest Guard** (one tile): every hero bumps into it (the hero does not move, `MoveResult.QuestVisited`); completing its last stage
    removes it.
  - **Quest Gate** (one tile, one stage that takes and gives nothing): heroes that meet its quest walk through without stopping; the others
    bump into it and are shown the proposal. Routes treat both barriers as walls and may end on them (a click bumps).
- **Quest log** (per player, in the game state, `MapEventState.QuestLog`): an entry per quest object the player's heroes were shown, with its
  stage and whether the player completed it; when someone completes a stage, the other players' open entries for that object go. A gate's
  entry is done once one of the player's heroes passes.
- A computer player completes any stage it meets (it takes the first answer); the AI's judgement of quests is the AI step of the roadmap.
- **Loading**: quests have stages and no actions, every stage a proposal, no random numbers in requirements; only quests have stages; a quest
  object names a quest, not shared, and a gate's quest has one empty stage. A map with quest objects and no events is checked too.
- Events: `QuestStageCompleted`, `QuestLogChanged`. Map format **v32**: an event's `stages` and the `questSite` objects (`seer-hut`,
  `quest.guard`, `quest.gate`).

### ADR-128: Map events: the game app's windows and quest log, quest object shapes; step 3: loops, "run another event", a Pandora's Box's hero event, map format v33 (ADR-115, ADR-124, ADR-127, MAP-35, 2026-11-04)
**Status**: implemented (2026-11-04); core tested; the game app checked in Godot 4.7.2 with the new smoke scenario `game-quests`. The camera moving to what a
turn-start message concerns (EVT-2 f): ADR-132. The editor's "Events" tab and the AI's use of events and quests: ADR-129. Choices made here are marked *(choice)*.

- **Step 3, loops** (`RepeatAction`, `WhileAction`): a repeat's count is drawn once, when it starts; 0 or less runs nothing. A while loop tests
  its condition before every round. Both stop after **100 rounds** (`MaxLoopRounds`) *(choice)*. A message, question, battle or full army
  inside a loop pauses it, and it goes on afterwards.
- **Step 3, running another event** (`RunEventAction`): the named event's actions run there and then for the same player, hero, town and
  cell, and the event goes on after them. *(choice)*:
  - a disabled event is skipped;
  - a stop ends only the run event, and "this event" in it (enable or disable) means the run event;
  - a quest cannot be run (a load error);
  - nesting deeper than **16** (`MaxRunDepth`) is skipped, so an event that runs itself ends;
  - each run event publishes `MapEventRan`.
  - One run takes at most **100 000 actions** (`MaxActionsPerRun`) and then ends, a guard against loops inside loops.
- **Pandora's Box** (MAP-35): a box may name a **hero event** (`Event`), which runs for the opener after the contents and the experience (and
  any level-up) are given *(choice: both, not instead)*.
  - A box with only an event is valid. An event name that is not a hero event of the map is a validator error and a load error.
  - The AI values only the contents, so it leaves a box that holds nothing but an event (no look-ahead at events, EVT-6).
- **Map format v33**: the action types `repeat`, `while` and `runEvent`, and a box's `event`. A map whose boxes name events but which has no
  events is checked too.
- **Editor**: `SetPandorasBoxEvent` and `SetQuestSiteQuest`, both undoable. The inspector gets a picker of the map's hero events for a box,
  and of its quests for a quest object.
- **Game app**:
  - the decision prompt shows a map event's message (with its reward lines, OK) and question (one button per answer), and the full army's
    choice (tick one to seven stacks, Keep), so ADR-124's windows are in;
  - the **quest log** (Q, or the HUD's "Quests (Q)"): open quests first, each with its object, where it stands, "stage n of m", "open" or
    "done", and its proposal; "Show" moves the camera there; the log follows `QuestLogChanged`;
  - the Q press that opens or closes the log does not also turn the camera: `StrategyCamera.IgnoreHeldKeys` ignores held keys until they are
    let go.
- **Quest object shapes** (placeholders until the art phase's models): the Seer's Hut gets a pointed roof and a lamp, the Quest Guard a figure
  with a spear, the Quest Gate two posts, a lintel and a seal.

### ADR-129: Map events, step 4: the AI's answers and quests, the editor's "Events" tab, validator checks (ADR-115, EVT-6, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`AdventureAiEventTests`, `MapEventEditorTests`); the editor's tab checked in Godot 4.7.2 with
the new smoke scenario `editor-events`. No map format change. Choices made here are marked *(choice)*.

- **The AI and events** (`AdventureAi.Events.cs`):
  - A **question** is answered with the answer worth the most to the player (the first of equals), where an answer's worth is that of its
    actions. Random numbers count as their average, `(min + max) / 2`, so the AI still draws none.
  - **Values** *(choice)*, the AI's usual gold values where it has them: resources at their gold value, experience 1 a point, a primary
    stat 750, a secondary skill 1000 a level, an artifact 800 + 80 × its find score, a war machine 300 (taking one −300), a spell book 500, a
    spell 400 × its level, mana 2 and movement 1 a point, morale and luck 150 a point, creatures 0.85 × their value (0.6 to a town's
    garrison, 0.4 as creatures to hire), a building 1000, revealing an area 100, a hero 2500; what is taken counts negative.
  - A **battle** is played out by the battle estimator: one the hero wins is worth 0.35 × the enemy's power; one it loses is worth
    −(its army's power + 5000) and ends the answer's worth there, as does a stop. Branches count the branch whose condition holds now
    (the AI reads the variables, it draws no random numbers); a repeat counts its average count; a while loop counts one round
    *(choice)*; "run another event" is followed 4 deep *(choice)*.
  - **Quests**: a hero goes to a Seer's Hut or Quest Guard only when it meets the current stage and the stage is worth more than nothing
    (what it takes, then its rewards); a Quest Guard adds 500 for getting it out of the way *(choice)*, plus the unexplored ground behind it.
    The completion question is answered the same way, so a stage not worth doing is declined. A Quest Gate is open only to a hero that meets
    it (exploring and routes); a guard or gate it does not meet stays an obstacle.
  - **No look-ahead**: event cells, player and town events and a Pandora's Box's event are worth nothing until they run.
- **Editor core**:
  - `MapEditorSession`: add, replace (a rename rewrites "run another event", enable and disable references, cells, boxes and quest objects),
    remove (also its cells and the references of boxes and quest objects), move, map variables, and event cells (only on passable,
    free tiles, only for hero events); all undoable, publishing `MapEventsChanged`.
  - The editor holds events that do not fit yet: `GameState.SetMapEvents(set, check: false)`, and the editor opens maps with
    `MapConverter.ToGameState(..., checkEvents: false)`; the validator reports what the game's load would refuse. The game still refuses
    such a map.
  - `MapEventTreeEdit` addresses an action by its path of lists (a quest's stages, then, then and else, do, an answer's actions, a message's
    rewards) to insert, replace, remove and move it; `MapEventText` describes a node in one line for the tree.
  - **Simple forms** (`SimpleEventForms`) write HoMM3's events as ordinary event trees *(choice: no form kept in the file)*: a **timed event**
    (a player event: its first day and every n days, for which players, people and or computers, a message and rewards), an **event cell**
    (a hero event, once or every time) and a **town event** (its towns, days, buildings and creatures to hire). "Every n days" is written with
    the arithmetic the conditions have, there being no remainder operator.
  - `MapEventTemplates`: a blank event of each kind, one action of every type and the usual conditions, valid on the map.
- **Node text** *(choice)*: the tab edits a chosen action, or a quest stage without its actions, as the JSON the map file writes
  (`IMapEventNodeText`, implemented by `MapEventNodeJson` in Core.Data and handed in by the editor app, so Godot.UI does not use Core.Data). A
  node with a value missing that may not be missing is refused with the reason, so every action, condition and expression is editable
  without a form of its own; the menus insert templates.
- **Validator** (`MapEventRules.Check` returns problems with their event and position; `MapEventChecks.Warnings`): the load errors as
  errors; as warnings an event that runs itself, an event nothing runs (no cell, box, other event, trigger or quest object), a quest no
  object asks, a town event with no towns, an event that starts disabled and that nothing enables, a message without text, a quest stage
  that cannot be met (an unknown hero, player, monster, town, artifact, creature, resource or skill, or a level heroes cannot reach), and a
  variable nothing reads. An issue sits on the event's position, cell, quest object or town.
- **Editor app**:
  - the **"Events" tab**: the events in run order (new of each kind or from a form, up, down, delete), the chosen event's name, whether it
    starts enabled, its cells ("Place cells" makes map clicks place or remove them; "Show"), towns or quest objects ("Show", "Add stage"), its
    tree with "Add action", up, down, delete, the node's text with Apply and "Insert condition", the validator's findings for it, and the map
    variables with their starting values;
  - the event cells drawn on the map in violet with their event's name, the chosen event's in gold (`EventCellsView`, the editor only);
  - **inspector links**: a box's hero event and a quest object's quest get "New" (a blank event, picked, one undo step) and "Edit"; a town
    lists its town events and has "New town event"; a double click on a validation issue of an event opens it in the tab.
- **Tested by playing the map**: a test writes events with the editor's forms and edits, saves and loads the map, and plays it in the game.

### ADR-131: Map events: campaign variables and the score check, map format v34 (ADR-101, ADR-115, EVT-1 f, FLOW-1, 2026-11-05)
**Status**: implemented (2026-11-05). Points marked *(proposal)* are the agent's reading and stand unless the owner changes them.

- **Campaign variables** (EVT-1 f): a map variable may be marked **"kept for the next map"** and / or **"taken from the last map"** (the
  events' `keptVariables` and `takenVariables`, names of declared variables; map format v34, older maps load without them).
  - A taken variable starts with the value the campaign carries in (`GameOptions.CampaignVariables`), or with its own starting value when
    it carries none; carried names the map does not take are ignored. The carried values are part of the game's options, so a replay or a
    save starts the same (the record format is unchanged; older records have none).
  - At any moment, and so at the end of the game, `MapEventState.CampaignVariables` gives the kept variables' current values: what a
    campaign carries on to the next map. The campaign itself (FLOW-1, Phase 6) passes them; until then nothing does.
  - A mark on an undeclared variable is a load error; a kept variable that no event reads is not warned about (the next map reads it).
  - **Editor**: "Kept" and "Taken" tick boxes beside each variable in the Events tab (`MapEditorSession.SetCampaignVariable`, undoable);
    removing a variable drops its marks.
- **Score check** (ADR-101, FLOW-4 a): a map is campaign-like, and not ranked, also when a map event **favours people**
  (`MapEventChecks.FavoursPeople`): it gives something only where its condition holds for people alone. Worked out from the tree, without
  playing: the "then" of an "if" (and the body of a "while") whose condition needs "the player is a person" (directly, in an "all of", in
  every part of an "any of", or as "not" of a condition every computer player meets), the "else" of an "if" whose condition every computer
  player meets, a quest stage only people can meet, and the events such a branch runs. A ready form with "computer may trigger" left off is
  such an "if". *(proposal)* "Gives something" is any action but a message without rewards, a question, a stop, a variable, enabling or
  disabling events, removing the cell and a loss (negative amounts, taking an artifact or war machine, lower morale and luck); a gift that
  only a variable leads to is not followed. The editor's validator warns about such an event: "games on this map are not ranked".
- **Tests**: the carried values, the kept values after play, the format round trip and a replay with carried values, the editor's marks,
  the load error, an older map and record; six events that favour people and six that do not.

### ADR-132: Map events: the camera on a turn-start message (ADR-115, EVT-2 f, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`MapEventTests`); checked in Godot 4.7.2 with the new smoke scenario `game-turn-start-camera`.
No map format change. With it the map events of ADR-115 are complete. Points marked *(proposal)* are the agent's reading and stand unless
the owner changes them.

- A message or question that a **turn-start** run shows (a player event, or a town event at its owner's turn start) names the place it
  concerns (`MapEventDecision.Place`), and the game app moves the camera there (and to its level) when it shows the window. What it concerns
  *(proposal)*, first that applies:
  - a place the message's own rewards name: the centre of an area they reveal, a cell they give a hero at;
  - the last place the run touched before it: the centre of an area it revealed, where a hero it gave stands;
  - the town of a town event (its entrance).
  Otherwise there is no place and the camera stays on the turn's first hero.
- Hero events (cells, quest objects, a Pandora's Box's event) name no place: their hero is in view. A computer player sees no messages.
- The camera moves after the turn's own move to the first hero, and again for each later message of the turn; not while the computer's
  moves are still being shown (the turn's showing comes back to it).

### ADR-242: Map events for stories: dialogues, the end of the game, captured and battle events, map format v42 (ADR-115, ADR-241, 2026-11-19)
**Status**: implemented. Map format v42 (two event kinds, two actions; older maps load as they are). The editor's Events tab offers them.

- **Dialogue** (`DialogueAction`): lines, each with a speaker (a roster hero's id, shown as its name, any other name, or none for a narrator)
  and text. A person reads them one at a time (`MapEventDecision.Dialogue`; "Next", then "OK" answers the event, which goes on); a computer
  player is not shown it. No portraits until there are hero pictures.
- **End the game** (`EndGameAction`, with an optional reason): won, the event's player's team wins the map; lost, the player is defeated and
  the usual checks decide the rest. The event stops there and no other event runs until the game flow has settled it after the command
  (`GameState.ScriptedEnd`). Without a player (a neutral town's event) nothing happens. It is how a campaign map ends on its story (ADR-241).
- **Captured events** (`MapEventKind.Captured`): they name towns like town events; when one falls to a player they run for the new owner, the
  town and the hero at its gate, once the command that took it is done. The validator warns when one names no town.
- **Battle events** (`MapEventKind.Battle`): after every battle won on the map (the other side lost, fled or surrendered) they run for the
  winner and its hero; which battle it was is a condition ("defeated by", the hero). A map event's own battle runs none, so an event that
  fights cannot run itself.
- **Ranking** (ADR-131): a dialogue and a lost end give nothing; a won end only people can reach favours people.
- *(choice)* No separate "game start" trigger: a player event with "day 1" and "disable this event" does it.
