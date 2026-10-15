# Architecture

## 1. Goals

- **Game logic is engine-independent.** Rules run and are tested without Godot, which makes them
  fast to test and possible to reuse in headless servers, AI simulation and tools.
- **Three front-ends, one core.** The 3D game, map editor and 2D harness all drive the same core
  services.
- **Event-driven presentation.** Core publishes facts through a custom pub/sub bus; Godot views
  subscribe and render. Views never poll core state for changes.

## 2. Tiers

```
┌──────────────────────────────── Tier 3 · Apps (Godot projects) ───────────────────────────────┐
│  EternalWars.Game            EternalWars.MapEditor            EternalWars.Harness2D            │
│  3D game (Forward+)          3D editor (Forward+)             2D debug front-end (Compatibility)│
└──────────────▲───────────────────────────▲───────────────────────────────▲─────────────────────┘
               │                           │                               │
┌──────────────┴───────────── Tier 2 · Godot libraries (depend on GodotSharp) ───────────────────┐
│  EternalWars.Godot.UI        HUD widgets, panels, debug overlays (bound to core events)        │
│  EternalWars.Godot.Shared    map/hero views, camera, coordinate mapping, input, event glue      │
│  EternalWars.Godot.Assets    models, textures, materials, shaders, prefabs (no code, no csproj) │
└──────────────▲─────────────────────────────────────────────────────────────────────────────────┘
               │
┌──────────────┴───────────── Tier 1 · Core (plain .NET 10, no Godot) ──────────────────────────┐
│  EternalWars.Core.Data       file formats (maps; later saves, content definitions)             │
│  EternalWars.Core.Domain     model + rules: map, heroes, economy, turns, editing, scenarios    │
│  EternalWars.Core.Events     pub/sub primitives: IEvent, IEventHandler, IEventPublisher, bus   │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
         tests/EternalWars.Core.Tests  (xUnit v3, covers Tier 1)
```

### Dependency rules

| From \ To | Core.Events | Core.Domain | Core.Data | Godot.Shared | Godot.UI | Apps |
|---|---|---|---|---|---|---|
| Core.Events | — | ✗ | ✗ | ✗ | ✗ | ✗ |
| Core.Domain | ✓ | — | ✗ | ✗ | ✗ | ✗ |
| Core.Data | ✓ | ✓ | — | ✗ | ✗ | ✗ |
| Godot.Shared | ✓ | ✓ | (avoid) | — | ✗ | ✗ |
| Godot.UI | ✓ | ✓ | (avoid) | ✓ | — | ✗ |
| Apps | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ (apps never reference each other) |

These are enforced by:
- `Directory.Build.targets`: the build **fails** if any project under `src/Core` uses
  `Godot.NET.Sdk`, references `GodotSharp`, or references a `*Godot*` project.
- `Directory.Build.props`: core projects build with `TreatWarningsAsErrors`.

## 3. Runtime composition

```
           input (keys, clicks)                       render
 Player ───────────────────────► App (Main.cs) ◄──────────────── Godot views (Tier 2)
                                     │  calls                           ▲
                                     ▼                                  │ subscribe
                               GameSession (Core.Domain)                │
                         ┌──────────┼─────────────┐                     │
                    TurnService  HeroMovement  EconomyService ...       │
                         └──────────┼─────────────┘                     │
                                    │ mutate GameState, then publish    │
                                    ▼                                   │
                               EventBus (Core.Events) ──────────────────┘
```

- **`GameSession`** is the composition root of a running game: one `GameState`, one `IEventBus`,
  and the services that mutate the state. Front-ends call services and subscribe to `session.Events`.
- **Services** validate, mutate `GameState`, then publish past-tense events. They depend on
  `IEventPublisher` only.
- **`GameState`** is plain data. Setters are `internal` so only Domain services can mutate it.
- **Views** (`AdventureMapView`, `HeroView`, `ResourceBar`, ...) subscribe with
  `node.SubscribeWhileInTree<T>(...)`, which disposes the subscription when the node leaves the tree.
- **Apps** contain composition and input mapping only. If an app needs logic, it probably belongs
  in core.

### Planned evolution
- **Commands.** Player intents (`MoveHero`, `BuildStructure`, `EndTurn`) become serializable command
  objects dispatched through a command processor. Then UI, AI and network all submit the same thing,
  and a game can be replayed from its seed plus its command log. Today services expose methods directly;
  the change is mechanical and planned for Phase 2 (see roadmap).
- **Deterministic RNG.** Replace `System.Random` with a seeded, serializable PRNG stored in `GameState`.
- **Content definitions.** Factions, creatures, spells and artifacts load from JSON in `Core.Data`.

## 4. Godot libraries: constraints

- Tier 2 projects use `Godot.NET.Sdk` (4.7.2) so the Godot source generators run and node subclasses
  (overrides such as `_Ready`, `_Process`) work.
- Godot attaches scripts by `res://` path. A class compiled into a library has no path inside the
  app's project, so **library nodes are created from code** (`new HeroView()`), not attached to
  scenes in the Godot editor. Classes that must be attachable in the editor (`[GlobalClass]`,
  inspector `[Export]`s) live in the app project.
  - *Phase 0 spike*: confirm `[GlobalClass]` behaviour for referenced assemblies in Godot 4.7. If it
    works, prefabs can carry library scripts.
- Assets can't live in a class library. `EternalWars.Godot.Assets` is mounted into each app as
  `res://shared/` by `tools/link-shared-assets.ps1` (junction/symlink, git-ignored).

## 5. Threading model

The simulation is single-threaded and runs on Godot's main thread. `EventBus` is deliberately not
thread-safe. Heavy work (AI turn search, pathfinding over large maps, map generation) may later run
on a background thread against a **copy** of the state, returning commands to apply on the main thread.

## 6. Solution layout

```
EternalWars.sln
global.json                     .NET SDK 10, Godot.NET.Sdk 4.7.2, MTP test runner
Directory.Build.props/.targets  shared settings + architecture guard
docs/
src/
  Core/
    EternalWars.Core.Events/
    EternalWars.Core.Domain/    Common, Map, Heroes, Players, Economy, Turns, Editing, Game, Events, Scenarios
    EternalWars.Core.Data/      Maps (MapDocument, MapConverter, MapSerializer)
  Godot/
    EternalWars.Godot.Shared/   Map, Cameras, Controls, Events
    EternalWars.Godot.UI/       Hud, Debugging
    EternalWars.Godot.Assets/   (linked into apps as res://shared)
  Apps/
    EternalWars.Game/           project.godot + Main.tscn/Main.cs
    EternalWars.MapEditor/
    EternalWars.Harness2D/
tests/
  EternalWars.Core.Tests/
tools/
  link-shared-assets.ps1
```
