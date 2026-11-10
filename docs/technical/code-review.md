# Code review: Eternal Wars

The codebase is in good shape: the build has 0 warnings, all 1693 tests pass, and the architecture rules are mostly followed. Changes go through `session.Execute`, randomness is deterministic, events are sealed records published after the change, and choices use `PendingDecision`.

The problems are about growth. A handful of god classes and long type switches mean every new spell, object kind or command touches many files. I found 3 real bugs and a few latent risks worth fixing first. I read the code only (four parallel reviewers, one per area) and changed nothing. I checked the bugs and the top risks in the code myself.

## Progress

| Item | Status | Where |
|---|---|---|
| B1–B4, R1–R4 | Done | commit `6c700b2`, ADR-165 (`CommandRefusedException`, `ContentCatalogs`, `MapEventRuns`, missing `formatVersion` = v1, `internal` economy mutators) |
| R5 object-kind registry | Done | commit `1dc37b6`, ADR-166 |
| R6 `MapObjectInteractionService` | Done (uncommitted on main), ADR-167 | 1321 → 334 lines; `ObjectShopService`, `ObjectDecisionService`, `ObjectLifecycleService`, `GrailService`, `MapObjectRewardVisits`, `ObjectInteractionContext` |
| R6 `BattleService` | Done (uncommitted on main), ADR-167 | 1111 → 108 lines (facade); `BattleStarter`, `BattleCommandHandler`, `BattleFlow`, `BattleAftermath`, `IBattleGuard` on the 7 guard kinds |
| R6 `BattleEngine` | Done (uncommitted on main), ADR-167 | 1474 → 517 lines; `BattleSpellResolver`, `BattleRounds`, `BattleWounds` (`HealTop`, `KillCreatures`, `RollDamage`), `BattleState.NextUnitId()` |
| R6 `BattleView3D` | Done (uncommitted on main), ADR-167 | 1519 → 476 lines; `BattleNarrator` (Core, tested), `BattleAnimationQueue`, `BattleFieldBuilder` (cached materials), `BattleStage`, `BattleAnimations`. Not checked in Godot yet |
| R6 `MapEditorSession` | Done (uncommitted on main), ADR-167 | 2029 → 72 lines (facade); `EditorContext`, `PropertyEdit<T>` (≈35 setters; unchanged values record no undo step and publish nothing), `PaintLayer<T>`, and `session.Terrain`, `.Objects`, `.Contents`, `.Monsters`, `.Towns`, `.Heroes`, `.MapEvents` (not `.Events`, which is the event subscriber), `.Players`, `.Map`. Hero type, town type, gate links and event-set/player add/remove stay plain undoable commands |
| R6 `Game/Main.cs` | Done (uncommitted on main), ADR-167 | 1683 → 601 lines (+ `Main.Saves.cs` 260, `NewGameChoices.cs` 82); `MoveOutcomeText`, `AdventureSpellPrompt` (Core.Domain), `SaveSlotText`, `RankingText`, `TemplateFolder` (Core.Data), `AdventureHud`, `AdventureInputController`, `BattleInputController`, `DeploymentDragController`, `SetupScreenHost` (Godot.UI). An armed spell or a drag no longer survives loading a game. Not checked in Godot yet |
| R6–R8 verification | Done | All together: 1844 tests pass; AI-played games on 13 maps (EmberMarch ×2, Towns, every built-in template ×2 seeds) give per-turn digests, final state and full event logs byte-identical to the pre-R6 trace (checked after R6, after R8 and after R7) |
| R7 spell effect handlers | Done (uncommitted on main), ADR-168 | `Battles/Spells/`: `ISpellEffectHandler` (refusals at three fixed points, `Apply`, `Value(CastContext)`, `Aims`), `CastContext`, 13 sealed handlers (one per `SpellEffectKind`), `EnchantHandler` base for Buff/Curse, `SpellEffectHandlers` registry. No `SpellEffectKind` switch left in `Battles/` (the parser in `Spells.cs` and two two-click checks in `BattleHud` remain). `BattleSpellResolver` 381 → 129, `BattleAi` 592 → 423, `BattleMagic` 611 → 571. Game trace (89 spell casts) identical. Three refusal checks run in a different order, the same for any effect the parser can produce |
| R8 smaller `GameSession` / `TurnService` | Done (uncommitted on main), ADR-169 | `GameSession` 488 → 261, `TurnService` 171 → 141. `IBattleCommand` (16), `IHeroCommand` (5; Equip/Unequip/Assemble/TakeRelicApart/AppointGovernor stay unmarked because their services refuse with their own texts), `CommandHandlers` registry with per-area registrations replacing the `Dispatch` switch, `SubscribeTo(bus)` on visibility, map events and adventure magic, `ITurnPhaseParticipant` with 9 hooks in one ordered list (arena stays direct: its battle day replaces the whole day). Tested: every command has one handler, marker sets pinned. Game trace identical |
| R9–R10, sections 3–5 | Not started | — |

## 1. Bugs and risks to fix first

| # | Problem | Where | Fix |
|---|---|---|---|
| B1 | The AI's battle-estimate cache key uses `attacker.Attack/Defense`, but the simulation uses `TotalAttack/TotalDefense`. After equipping an artifact, the AI can reuse an out-of-date estimate. | `AI/BattleEstimator.cs:43` vs `:71` | Build the key from the totals |
| B2 | Creature spells are looked up in `SpellCatalog.Default`, not the game's spell table, so a custom spells file is ignored for creatures. The `CasterSpells` table hard-codes spell ids in code (ADR-042 says creature rules are data). `MapConverter` also uses the global spell table (`:652`, `:714`). | `Battles/BattleMagic.cs:120` | Resolve through `battle.Spells`; put the spell id in the Caster capability's parameter |
| B3 | Each New/Load/Generate in the editor subscribes on the root node, which never leaves the tree, so old editor sessions are never freed. | `MapEditor/Main.cs:241` with `NodeEventExtensions.cs:15` | Subscribe on `_ui`. Make `SubscribeWhileInTree` subscribe on `TreeEntered`, not immediately |
| B4 | `--artifacts=` is ignored for the Demo Valley and Two Towns scenarios, the editor's blank map, and the 2D harness. | `Game/Main.cs:1601`, `MapEditor/Main.cs:69,264`, `Harness2D/Main.cs:364` | A `ContentCatalogs` record (see R6) |
| R1 | `Execute` catches every `InvalidOperationException` as a refusal. There are about 200 such throws, and `GetHero`/`GetObject` use `First()`, which throws the same type, so real bugs show up as quiet refusals. Some resolvers change state before a throw can happen: `ResolveTreasureChest` clears `PendingDecision` first; `ResolveDeposit` spends resources first. The command is then not logged, so replays diverge. | `GameSession.cs:157-165`, `MapObjectInteractionService.cs:32,776` | Add a `CommandRefusedException` (or a returned `Refusal`) and catch only that. Add one test asserting `GameStateDigest` is unchanged after any rejected command |
| R2 | The running map-event queue lives in private fields of the service (`_queue`, `_current`, `_battle`), outside `GameState` and its digest. A copied state, as the background-AI plan needs, would lose it. | `MapEvents/MapEventService.cs:38-40` | Move it into `MapEventState` as records |
| R3 | A missing `formatVersion` defaults to the current version, so an old map without the field skips all old-version handling. | `MapDocument.cs:60` | Treat a missing field as v1 |
| R4 | `EconomyService.Grant` / `TrySpend` are `public` on the public `session.Economy`, so code can change the game without `Execute`. Tests also call services directly 36 times through `InternalsVisibleTo`, against the CLAUDE.md rule. | `EconomyService.cs:32,49` | Make them `internal`; move tests to `Execute` |

## 2. Structural refactors (open/closed and single responsibility)

**R5. A registry instead of type switches for object kinds** (highest leverage).
A new map object kind really touches about 9–12 places,
not the 4 the architecture doc lists:
- `Visit`, a switch with about 50 cases;
- the 45 `[JsonDerivedType]` attributes;
- `MapConverter.ToDocument` and `ToObject`;
- `MapObject.Create`;
- `GameStateDigest`, whose catch-all `_ =>` silently leaves new state out;
- `MapValidator`, `AdventureAi.Targets`, `InfoSheets`,
  the "defended" check in `HeroMovementService`, `InspectorPanel`.

Suggested shape:
- `IObjectVisitHandler` registered per type in `GameSession`.
- A `MapObjectCodec` registry in Core.Data, `Register<SignPost, SignDocument>("sign", toDoc, fromDoc)`, feeding the JSON resolver. `MapEventJson` already works this way.
- A reflection test: every `MapObject` subclass has a codec, a visit handler and a digest entry.

**R6. Split the god classes along seams that already exist:**

| Class | Lines | Split into |
|---|---|---|
| `MapEditorSession` | ~2000 | Per-area editors (`session.Towns`, `.Heroes`, `.Events`) over a shared `EditorContext`; a generic `PropertyEdit<T>` command for the ~30 copy-paste setters, which also skips no-op edits; one `PaintLayer<T>` for the three paint methods |
| `MapObjectInteractionService` | ~1280 | Visit handlers (R5), `ObjectShopService`, `ObjectDecisionService` (the `Resolve*` methods), `ObjectLifecycleService` (`PrepareObjects`, `StartWeek`), `GrailService` |
| `BattleService` | ~1100 | `BattleStarter`, `BattleCommandHandler`, `BattleAftermath`; an `IBattleGuard` interface replaces the 7-way type switches in `DefeatGuard` / `SetGuardCount` |
| `BattleEngine` | ~1475 | A `BattleSpellResolver` for the spell half. Helpers for the copied code: `HealTop` (5 copies), `KillCreatures` (3), `RollDamage` (2), `BattleState.NextUnitId()` (4) |
| `Game/Main.cs` | ~1680 | Not "composition and input mapping only". Move `MoveOutcomeText` to Core, save/ranking presenters to Core.Data, and `AdventureHud.Build(...)`, `SetupScreenHost`, `DeploymentDragController` to Godot.UI |
| `BattleView3D` | ~1520 | `BattleNarrator` in Core (the combat-log text, testable and reusable by the Sim), `BattleAnimationQueue`, `BattleFieldBuilder` with cached materials |

**R7. A handler per spell effect.** `SpellEffect` is a bag of about 40 flags. Each flag is read again in `TargetRefusal`, `Affects`, `DamagePlan`, `BattleEngine.Work`, `Enchant` and `BattleAi.Value`, so one new mechanic means 5–6 edits, and the AI's valuation can drift from the rules. An `ISpellEffectHandler` with `Refusal`, `Apply` and `Value(CastContext)` per effect kind keeps the rule and its AI value side by side.

**R8. Make `GameSession` and `TurnService` smaller:**
- `IBattleCommand` and `IHeroCommand { HeroId Hero }` marker interfaces replace the 16-type `IsBattleCommand` list and the hero switch in `Authorize`.
- `ICommandHandler<T>` objects replace the 210-line `Dispatch` switch.
- `visibility.SubscribeTo(bus)` replaces the 19 hand-wired subscriptions.
- An ordered list of `ITurnPhaseParticipant`s replaces the 11 constructor dependencies of `TurnService`.

**R9. Keep `*Rules` classes pure.** `TownService.Capture`, `MagicRules.Visit`, `GrailRules.Bury`, `PrisonRules.Prepare` and `PandorasBoxRules.Open` (7 parameters) change state and publish events from static classes. Move the changes into services so a "Rules" class always means "pure query".

**R10. Pass a `ContentCatalogs` record everywhere.** Today `ToGameState` takes 9 optional parameters, `MapGenerator.Build` 10, and `GameSaves.Load` 7. The apps repeat the four-catalog argument list 14 times, which is how B4 happened. The generator also hard-codes the default object and terrain catalogs in about 20 places.

## 3. Duplication

- **Copies of game logic:**
    - Hero creation (prison, map tavern, map event, `TownService.HireHero`): one `HeroRecruitment.Join(...)`.
    - Building, where `MapEventService.Build` copies the mage-guild rule word for word.
    - Artifact removal that skips `ArtifactService`.
- **Auto-combat preview** re-implements the end-of-battle logic, so the preview can disagree with the real result. Fix: a pure `BattleOutcome.Compute`, used by both `Finish` and `Preview`.
- **The battle action-to-command mapping** appears in both the Game and the Harness (and has already drifted: the Harness has surrender). Fix: `BattleTargeting.CommandFor(...)` in Core.
- **UI helpers:** `Spin`/`Options`/`Row` are re-declared in about 7 panels, and there are 37 hand-written "free all children" loops. Fix: a shared `FormBuilder` and `node.ClearChildren()`.
- **The Sim** re-implements `TurnTimer.Default`'s auto-answers, with different choices, and a map-connectivity rule. Move the connectivity rule into Core.Generation, where `MapValidator` could use it too.

## 4. Performance

- **`TownPanel` and `HeroPanel` rebuild their whole node tree on every event.** They have 29 and 25 subscriptions, including every `HeroMoved` of any hero, AI included. Fix: a `RefreshScheduler` that marks the panel dirty and refreshes once per frame with `CallDeferred`, or refresh only on `CommandExecuted`. This is the cheapest big win.
- **The minimap rebuilds and redraws every cell on each hero step.** That is about 65k rectangles per step on a 256² map. Fix: draw into an `ImageTexture` and update only changed cells.
- **`BattleUnit.Has(status)` recomputes the combined spell effects on every call.** It folds all active spells into a new 23-field record each time, and it sits under every damage and AI loop. Fix: cache the result and clear it when a spell is added or removed.
- **The adventure AI recomputes threats inside inner loops.** `Danger()` re-scouts every enemy hero per candidate and per flood-fill tile. The 10-candidate cap doesn't cap simulations, because unsafe candidates skip the counter. Fix: a per-decision `TurnContext` cache, and count every simulation against the limit.
- **Object and hero lookups scan whole lists.** `GetHero`, `GetObject` and `FindObject` are linear. Add id dictionaries next to the existing cell index.

## 5. Map format and testing safety nets

- **Map-format migrations.** Upgrades for old maps are scattered through `MapConverter` (`legacyGuardMode`, `RenamedDefinitions`, Tactics→Skills…). The file also stores domain types and enum names directly, so renaming a domain type silently changes the format.
    - Add an `IMapMigration` chain that upgrades the raw JSON before deserializing.
    - Keep one sample map file per format version, and pin the serialized enum names in a test.
- **Missing tests:**
    - every editor operation undoes cleanly (same digest after undo and after redo);
    - a rejected command leaves the state unchanged (R1);
    - pinned hashes of generated maps, so output changes need a `MapGenerator.Version` bump;
    - a completeness check for `BattleState.Copy()`;
    - an architecture test for the rules between core projects. `Directory.Build.targets` only blocks Godot.
- **Test cleanup:** 81 inline serialize/deserialize round-trips could use one `TestFormat.RoundTrip(state)` helper. There are three different editor factories. `SecondCatalogueTests` / `ThirdCatalogueTests` are named after milestones rather than behaviour.

## 6. Done well

- **Determinism:** separate random streams per generator stage, a forked battle RNG, ties broken by id, nothing rolled at 0, and replay-digest tests.
- **The AI uses the real rules:** `DamageCalculator.Expected`, the real battle engine for estimates, and only `Execute` to act.
- **Pure query helpers** (`TownRules.Check*`, `BattleTargeting`, `InfoSheets`, `Minimap.Build`) mean the UI is mostly rendering what Core already tested.
- **`EventBus`:** events raised by handlers are dispatched only after the current event reaches everyone, and changing subscriptions during dispatch is safe. `UndoHistory` strokes and temp-file-then-move saves are solid too.
- **Tests:** `TestWorld` ASCII maps and the `SessionCommands` helpers keep tests readable and behaviour-focused.

## Suggested order

1. **Fix the bugs (small):** B1–B4, R3.
2. **Close the refusal and state gaps:** R1 (refusal type and the digest test), R4 (encapsulation), R2 (event queue into `GameState`).
3. **Quick performance wins:** the refresh scheduler and `ClearChildren`, the spell-effect cache, the AI threat cache.
4. **The object-kind registry (R5):** it prevents future digest and format gaps and makes the `MapObjectInteractionService` split mechanical.
5. **The remaining splits as you touch those areas:** `MapEditorSession` (partial files first), `BattleService`/`BattleEngine`, spell handlers, `Main.cs`, `BattleView3D`, migrations.

If you want, I can start on the first item (B1–B4 and R3) with tests. The larger refactors would each deserve an ADR in `docs/project/decisions.md`. I can also put this review on a shareable page.