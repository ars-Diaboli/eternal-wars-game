# Roadmap

Each phase ends with something **playable or usable** and a green test suite. Items are small enough to land in one
change each; `❓` marks items that wait for a design answer in [open-questions.md](open-questions.md). Core items come
with tests (CLAUDE.md); UI items are checked with `tools/smoke-godot.ps1` where a smoke scenario exists.

Done items are summarised with their ADR numbers; the detail is in [decisions.md](decisions.md) and the design docs.

**Where we are (2026-11-09)**: Phases 0–2 are done; Phase 3 (ADR-114) and Phase 4 (ADR-117) are closed out in core, the apps and the
editor, and the AI plays generated maps to the end (ADR-119). Phase 5 is in progress: magic is built (ADR-110, ADR-120, ADR-122, ADR-123)
and every battle spell is in (ADR-149 to ADR-152); sieges, relics and war machine tuning are in (ADR-109, ADR-120, ADR-121); map events are complete: core, the game app (with the turn-start camera), quests, loops and run events, the editor's "Events" tab, the AI, campaign variables and the score check (ADR-124, ADR-127, ADR-128, ADR-129, ADR-131, ADR-132); map bans and random monsters, towns and hero placeholders are in (ADR-133). The views built but never run were seen in Godot (ADR-148).
**Next**: (1) a person's full game on Ember March (the Phase 3 exit's last check); (2) the remaining Phase 5 items: the rank changes
ADR-123 lists as not built, presentation; the few views not yet seen (below) when those spells or a game bring them up.

## Open checks in Godot
- [x] Godot run of (built and tested in core, the Godot part not run yet), seen with 13 new smoke scenarios (ADR-148):
      - Spells and magic: rank display in the HUD and hero screen (ADR-122); ADR-123 control / resurrection spells and the AI's buffs,
        curses, dispels and revives; the info adventure spells' screens (Distant Vision, Detect Magic, …); guild spells fixed by the map
        in the editor (map format v30); the guild's spell book button
      - ADR-120 / ADR-121: the siege's breakable gate, keep shooter and Earthquake in 3D; assembled relics' hero-screen buttons
      - Battle: deployment step (ADR-076) with drag and drop (ADR-155), war machine footprints in 3D, surrender button (ADR-081), Necromancy screen (ADR-078)
      - Game flow: victory screen with score and "Save replay", main menu (ADR-103, ADR-104), the conditions panel's difficulty row,
        the trading screen (ADR-067)
      - Towns: the unique buildings on the town screen and their visit messages (ADR-138); the Undead Transformer's section (ADR-143)
      - Map objects: the University's and War Academy's lessons in the object panel (ADR-139)
      - The Grail: puzzle map window (P), digging (G), holes, obelisk and Grail building notices, the editor's Grail tool, the template
        editor's obelisk count (ADR-140)
      - Map objects: power-up choices (Learning Stone etc.), utility panels and the notice panel, the second and third catalogues'
        screens (ADR-079, ADR-080, ADR-082), Pandora's Box (ADR-105)
      - Editor: hero limit setting on the conditions panel, town faction in the inspector (ADR-083)
      - New-game screen: user templates from the editor's `templates` folder
- [ ] Still not seen in Godot (ADR-148): Resurrection; Grave Whisper, Ledger of Ages, Eavesdrop; the AI's buffs, curses and revives; setting
      a guild spell by hand in the editor; a dug hole's drawing on the map

## Bugs
- [x] At game start the fog opens around the starting hero, not around the starting town (owner, 2026-11-06): towns now see from their
      centre cell, radius 10 (ADR-141)

## Data and content groundwork ✅
Data tables in `src/Core/data` embedded in core with designer overrides (DATA-1, ADR-071); creature size pass (ADR-070); per-town
building column (TOWN-3, ADR-074); per-faction bank tables and the Ruins → Crypt rename (MAP-22, ADR-082, ADR-112).

## Phase 0: Foundation ✅
Three-tier solution with the MSBuild architecture guard, `EventBus`, domain skeleton, map file format, the game / editor / 2D harness
apps, the Godot spike (ADR-043), CI.

## Phase 1: Adventure map ✅
Pathfinding with day markers, click-to-move, data-driven map objects, resources and mines, pending decisions, fog of war with refog
(ADR-031), minimap, `MapEditorSession` with undo and `MapValidator`, two levels, meeting heroes and teams (ADR-027, ADR-032), terrains,
gates (ADR-026), roads (ADR-024), terrain catalogue; Godot: 3D world view, new-game screen, exchange, decision prompts, hero / town lists,
minimap, editor UI, 2D harness.

## Phase 2: Armies and combat ✅
- [x] Built: command pipeline and replays (ADR-036), `sim` tool, chest tiers (ADR-039), creature data and stacks (ADR-037), tactical
      battle (ADR-035, ADR-038), battle AI and quick combat, neutral guards and dispositions (ADR-040, ADR-065), all eight rosters and
      `creatures.csv` (ADR-044, ADR-049), terrain model (ADR-045), creature capabilities (ADR-042); 2D battle board and 3D battle
      scene (ADR-052, ADR-085, ADR-113)
- [ ] Battle scene follow-ups: creature models and animations (Phase 5 art), terrain-themed obstacles and backdrops, flyer arcs, battle
      music

## Phase 3: Towns and progression (closed out, ADR-114)

### Towns
- [x] Built: towns, buildings, dwellings, growth, capture and income (ADR-030, ADR-046, ADR-050); building effects (Fort ADR-066,
      marketplace ADR-067, blacksmith ADR-056, mage guild ADR-071 / ADR-110, growth buildings and war lodge ADR-071 / ADR-112, spell
      buildings ADR-072 / ADR-114, Capitol ADR-074, per-town buildings ADR-074 / ADR-112); map dwellings and caravans (ADR-054,
      ADR-073, ADR-075, ADR-079); flaggable providers and resource caravans (ADR-079); kingdom overview (ADR-084); town screen and its
      polish (ADR-050, ADR-114)
- [ ] Caravan cost tuning
- [x] Every town's unique building: the first set (ADR-126, ADR-138)
- [ ] More unique buildings per town (TOWN-12)

### Heroes and skills
- [x] Built: hero roster and specialties (ADR-048, ADR-093, ADR-107, ADR-114), skills and level-ups, primary stats (ADR-055),
      Stewardship and governors (ADR-059, ADR-063), Treasury / Prospecting (ADR-062), Primal Fury (ADR-067), complexity levels (ADR-063,
      ADR-083), Ballistics in sieges (ADR-109), skill specialties (ADR-072, ADR-083), deployment and machine footprints (ADR-076);
      skill rework with Scouting, the distance penalty, Marksmanship, Necromancy and the Amplifier (ADR-069, ADR-072, ADR-073, ADR-078);
      hero screen (ADR-085)
- [ ] Spell Power strengthens Blood Rage and warcries for Primal Fury mages (ADR-072, MAG-10; numbers not set)

### Tavern ✅
Hiring and pools (ADR-060, ADR-070, ADR-081), surrender and AI retreat (ADR-081), hero limit and garrison heroes (ADR-072, ADR-081,
ADR-112), tavern screen (ADR-114), Thieves' Guild (ADR-079).

### Artifacts
Content draft: [artifacts.md](../content/artifacts.md).
- [x] Built: artifact tables, paper doll and backpack, effects, pickups and loot, AI use (ADR-061, ADR-068); the re-draft (101 artifacts,
      ADR-099, ADR-100, ADR-111); assembled relics (ADR-108, ADR-121); editor rows (ADR-114)
- [x] The Crown of the Waning Eye's +1 to the best magic skill (ADR-147)
- [x] Artifact exchange screen between heroes: both paper dolls and backpacks, click to give, wear or carry (ADR-153)
- [x] Drag and drop on the paper doll and the exchange screen (ADR-162)

### Game flow
- [x] Built: victory and loss conditions (ADR-057, ADR-073, ADR-094, ADR-083), save / load and autosave (ADR-058), score, ranking,
      victory screen and main menu (ADR-101 … ADR-104), week and month events (ADR-084), turn timer (ADR-091), auto combat (ADR-090)
- [x] Replay viewer: Replays and Saves tabs; reveal-all or a player's eyes; play, pause, speeds, next turn / day, back to a day; battles
      with "skip to the result"; "Play from here" in battles only (ADR-101, UI-2, ADR-164)
- [ ] Replay viewer: "Play from here" on the adventure map (a save played on with Load meanwhile); a battle where only the computer
      fights taken over by the viewer
- [ ] Save/load: a state snapshot if replays get too slow

### Arena mode (ADR-097, ADR-100; before 1.0, after the Phase 3 exit)
- [x] Core: the arena switch in the map (no victory or loss conditions), combat places; day 1 with unlimited movement and building (the
      map's starting resources); the battle at the start of day 2 (HotA's hero choice, heroes moved, no experience, retreat and surrender
      lose), its winner wins the game, a draw is a loss for both; map format bump (ARN-1, ARN-2; ADR-158)
- [x] Game app: arena maps on the new-game screen, hot-seat and against the computer; the battle starts by itself, both armies shown;
  result screen with "Play again" and "Save replay", not ranked (ARN-1; ADR-159)
- [x] AI: prepares on day 1 (visits, hires, builds) for the battle; never leaves the battle (ADR-158, ADR-160)
- [x] Editor: the arena switch on the conditions tab, combat places; validator (no combat place, players that can meet on day 1, map
      events that run only later); Pandora's Boxes, quests, Witch Huts and Shrines show their contents on day 1 (ARN-2; ADR-161)
- [ ] Three shipped arena maps, hand-made; no arena template for the generator (ARN-2)
- Arena over the LAN: in [LAN multiplayer](#lan-multiplayer-adr-088-adr-098-before-10-after-the-phase-3-and-4-exits)

### Adventure AI
- [x] Built: command-only deterministic AI (ADR-036, ADR-059): economy, exploration, fights, captures, town defence, several heroes,
      war machines, `sim game`; generated maps (ADR-119)
- [ ] Adventure spells (it does not cast the info spells, ADR-123) and marketplace use (no AI trading for now, ADR-067)
- [ ] Balance the AI further over several maps (`sim game --runs`): the first-mover edge on Two Towns (a day-4 rush), the larger templates'
      game length, the AI's hero hiring when it is beaten

### Adventure map objects ✅
First catalogue (ADR-068), second catalogue (ADR-069, ADR-082, Shrines ADR-120), third catalogue (ADR-077, ADR-079, ADR-080), Pandora's
Box (MAP-35, ADR-105), hero limit (MAP-14), generator placement (`TreasurePlacer`) and AI use. See the
[catalogue](../design/adventure-map.md#planned-objects-adr-068).

### Content for the slice
- [x] Built: Ember March scenario; Haven and Inferno played to the end by the AI on both sides (ADR-114)
- [ ] Haven against Inferno played to the end by a person in the game app (the first turns are seen in Godot, `game-ember-march`)

## Phase 4: Random map generation (closed out, ADR-117)
- [x] Built: `EternalWars.Core.Generation`, templates and their validator, the generator pipeline (layout, terrain, scenery, rivers,
      connections, towns, mines, roads, treasure, assembly; ADR-064, ADR-079, RMG-1 … RMG-7, ADR-112), whole-map fairness, teams 2–8,
      mirrored templates, the template editor and starter templates (ADR-117); `sim maps` in CI; editor generation (ADR-087) and the
      new-game screen's random maps (ADR-086), user templates on it
- [x] New-game screen: the tier-1 dwelling pick and the hero limit override (ADR-134)
- Not in 1.0: water zones and islands (no sea travel, ADR-051), HotA template import (ADR-034)

## Phase 5: Content and polish

### Magic (ADR-047, ADR-063; [spells.md](../content/spells.md))
- [x] Built: spell table, guild draw, spell books, rank I casting (ADR-106, ADR-110); summons, creature casters, adventure spells,
      shrines, spell books in the map (ADR-120); ranks II / III and mastery (ADR-122); the table follow-ups, control and resurrection
      spells, info adventure spells, guild spells fixed by the map (ADR-123); spell resistance, specialties, elements (ADR-071, ADR-072,
      ADR-099, ADR-106)
- [x] Field zones: Consecrate, Thornwall, Bone Wall, Fire Wall, Darkness, at all three ranks (ADR-149)
- [x] Control spells: Frenzy, Hypnotize, Puppet Master, spells that last turns (ADR-150; rank III's split, ADR-152)
- [x] Two-click spells: Teleport, Shadow Step, Drain Life, at all three ranks (ADR-151)
- [x] The last battle spells: Martyrdom, Mirror Image, Clockwork Precision, Raise Dead, Blood Price, at all three ranks (ADR-152)
- [ ] Spell table, the rest: the rank changes ADR-123 lists as not built, final numbers after the visuals

### Combat additions
- [x] Built: morale and luck (ADR-053, ADR-075), war machines (ADR-056, ADR-083, ADR-112, ADR-121), teleporting (ADR-083), sieges
      (ADR-106, ADR-109, ADR-113, ADR-120), circular battle movement (ADR-089)
- [x] Sieges: the town's own look on the siege field (ADR-144)

### Map events (ADR-096, ADR-115; HotA's extended events) ✅
- [x] Step 1, core: the event tree, variables, event cells, turn-start player and town events, HotA's conditions and actions, messages,
      questions, battles, a full army's choice, "defeated by" records, map format v31 (ADR-124)
- [x] Step 1, game app: the message and question window (with reward lines) and the full army's choice (ADR-128)
- [x] Step 1, game app: the camera moves to what a turn-start message concerns (EVT-2 f, ADR-132)
- [x] Step 2, core: quests with stages, HoMM3's quest forms that take what they ask, Seer's Hut, Quest Guard, Quest Gate (passage for a
      hero that meets its quest), the quest log in the game state, map format v32; no Seer's Huts on random maps for now (ADR-127)
- [x] Step 2, game app: the quest log window (Q), placeholder shapes for the three quest objects (ADR-128)
- [x] Step 3: loops and "run another event"; a Pandora's Box that runs a hero event (MAP-35), map format v33 (ADR-128)
- [x] AI: answers by value (random numbers as their average), Seer's Hut quests worth doing, Quest Guards and Gates as obstacles until met,
      no look-ahead at event cells (EVT-6, ADR-129)
- [x] Editor: an "Events" tab (the tree, variables, shared events, "simple event" forms for HoMM3's timed events, event cells and town
      events), object inspector links, validator checks; tested by playing the map (EVT-6, ADR-129)
- [x] Campaign variables ("kept for the next map", "taken from the last map") recorded with the campaign (EVT-1 f, FLOW-1, ADR-131)
- [x] Score: the campaign-like check counts map events (ADR-101, ADR-131)

### Missing mechanics, the owner's picks (ADR-125, ADR-126; [missing-mechanics.md](../design/missing-mechanics.md))
- [x] Scouting reveals information: amount words with ranges for armies not owned, what each Scouting level shows, Veil against it,
      the AI reads only what it sees (ADR-125, ADR-142; the AI still reads enemy heroes' Attack and Defense)
- [x] Map bans: heroes, artifacts, spells and skills in the map rules, read by every pool and roll; editor tab, validator warnings; map
      format bump (ADR-133)
- [x] Random monsters by tier, random towns, hero placeholders: editor, map format, drawn at game start; the new-game screen's
      faction choice (ADR-133)
- [x] University and War Academy map objects: lessons, disabled offers, screen, AI use, generator placement; map format v36 (ADR-139)
- [x] Spell scrolls: one per spell level, the spell written by the generator, the editor or at game start, casting while worn, 5 % of the
      artifact rolls (ADR-136)
- [x] Grail: the Grail artifact, burying at game start, digging, obelisks (at most 25), the puzzle map (20×20 cells, 100 pieces by rings,
      a puzzle per player), Grail buildings (ADR-130), the AI, the editor's Grail tool, the template's obelisk counts (v9); map format v37
      (ADR-140, MAP-36)
- [x] Unique buildings, the first set: Stables (Haven), Proving Ring (Stronghold), Shell Wall (Swamp Bestiary), Hall of Records
      (Academy), Altar of Brimstone (Inferno), Elder Grove (Sylvan), Mana Spring (Dungeon); the siege bonuses (ADR-138)

### Roster (CONT-2)
- [x] Renames, the Wight / Death Weaver tier swap, the Haspid / Wyvern town swap, classes A and B, Necromancy by class (ADR-135)
- [x] Undead Transformer: the Necropolis building that turns living creatures into undead of their class, and swaps undead lines
      (ADR-137, ADR-143)
- [x] Observatory: the Academy building that lets its town see 24 cells (ADR-143)

### Presentation and UI (ADR-116)
- [x] Built: icon prompts and the list of missing mechanics (ADR-116)
- [x] Stack health bars in battle: the top creature's hit points against its full health, green over 50 %, yellow 21–50 %, red 20 % and
      below (ADR-118, ADR-145)
- [x] Dividing obstacles: cliffs or walls of 6–12 cells that split the field partly, at least two gaps, flyers pass over; on highlands,
      rough, rocky and subterranean ground, one battle in three, never in sieges or banks (ADR-118, ADR-146)
- [x] More scenery on random maps (trees, rocks and the like, denser by zone terrain; ADR-077's scenery, ADR-157)
- [x] Spacing pass: shared margins and gaps for every panel, no font or theme change; first (ADR-118, ADR-163)
- [x] Configurable layout: initiative bar, minimap corner, hero and town lists, action panel; a "Layout" tab, kept in the player's settings (ADR-118, ADR-156)
- [x] Information windows (right-click) for creatures, heroes, skills, spells, artifacts, buildings and map objects; "detailed tooltips", off by default (ADR-118, ADR-154)
- [ ] Information windows on touch (a long press), and for the kingdom overview, the tavern and the decision prompts (ADR-154)
- [ ] The icons themselves (from [icon-prompts.md](../content/icon-prompts.md))

### Platform and art
- [x] Built: speed settings (ADR-095, UI-1)
- [ ] Platform pass: Linux/Steam Deck builds, controller-friendly UI, Deck performance budget (ADR-034)
- [ ] Look-dev spike: one terrain biome + one creature + lighting setup, to lock the visual target and budget
- [ ] Smooth fog (ADR-099): a soft fade from the visible to the hidden area; objects half under the fog show their explored part
      correctly (rendering only; the rules keep cell-by-cell visibility)
- [ ] Terrain: heightmapped landscape with splat shader and blended biome transitions; grid only as overlay
- [ ] PBR models for heroes, creatures, towns and map objects; fog/volumetrics
- [ ] UI theme (HoMM-style right-side panel, status bar)
- [ ] Audio: per-terrain ambient music, town themes

## LAN multiplayer (ADR-088, ADR-098; before 1.0, after the Phase 3 and 4 exits)
- [ ] Network layer: host and clients over the LAN; clients send commands, the host runs them and plays the computer players (MP-1)
- [ ] Per-player view: a client starts from the map's static content; the host sends each client only the events it may see (fog);
      the client keeps a partial copy of the state for its views; under the fog stays as last seen; both sides see a whole battle, others
      its result (MP-3, ADR-099)
- [ ] Lobby: LAN discovery and typing an address, map or random map with its options (ADR-086), colours, computer players, the
      simultaneous first week on or off; text chat (MP-1)
- [ ] Battles between two remote humans, each side live; a player who drops out (pause, the host lets the computer take over or waits
      for a rejoin) (MP-1)
- [ ] Saves: the host's save loads in multiplayer, single player or hot-seat; a client's save (the whole record, sent by the host)
      loads in single player; replays are the host's record (MP-3, ADR-099)
- [ ] Start zones in the map: the generator writes them, the editor paints them, validator (touching, overlapping, no start town);
      map format bump (MP-4, ADR-099)
- [ ] Simultaneous first week on maps with start zones: conflicts (entering another start zone, touching an object another player touched
      that day, seeing another player's hero) discard the day for everyone, which is played again turn by turn (MP-2, ADR-098)
- [ ] The turn timer in multiplayer and in simultaneous days: one timer, the day ends when all have ended their turn (FLOW-3, MP-2)
- [ ] Arena maps in the lobby (ADR-100, ARN-1): day 1 hidden by each player's own view, turn by turn or simultaneous with start zones

**Exit**: two players on two computers play a random map from the lobby to the end, save it and load it again.

## Phase 6: Story campaign
- [ ] Prologue / tutorial: one small map, Haven against neutrals and a Necropolis vanguard, hints through the map events, a "Hints" setting; playable from the main menu as "Prologue" (ADR-116, ADR-118)
- [ ] Scenario chaining and hero carry-over (campaign variables: see Map events, FLOW-1, EVT-1)
- [ ] Dialogue and campaign triggers, built on the map events and their editor "Events" tab (ADR-096)

## Risks
| Risk | Mitigation |
|---|---|
| Godot C# library limitations (scripts in referenced assemblies) | Phase 0 spike; code-first nodes; app-side wrappers for editor-attachable scripts |
| 3D readability on dense maps | Early camera and art-style prototypes; keep the 2D harness as the logic reference |
| AI complexity | Deterministic core + commands give headless simulation for AI tuning |
| LAN multiplayer: a client's partial view drifts from the host's state, or leaks what is under the fog | Only the host runs rules; tests that compare a client's view with the host's state filtered by fog |
| Random maps unfair or broken | Batch generation + automated validation/fairness metrics in CI; templates versioned like maps |
| Asset production cost | Look-dev spike early; buy/adapt PBR asset packs where the dark-fantasy style allows |
