---
apply: always
---

# Godot 4 C# Strategy Game Rules

## Technology Stack
- Game Engine: Godot 4.x (C#/.NET)
- IDE: JetBrains Rider
- Language: C# 10+ / .NET 8+ using modern C# conventions.

## Code Style & Godot Best Practices
- **Do not output GDScript.** All code must be strictly typed C#.
- Use standard PascalCase for Godot node life-cycle methods: `_Ready()`, `_Process(double delta)`, `_PhysicsProcess(double delta)`. Note the `double` for delta.
- Always prefer `GD.Print()` over `Console.WriteLine()` for debugging.
- Use Godot 4's updated strongly-typed signals (`[Signal] public delegate void MySignalEventHandler()`).
- Use `GetNode<T>("Path")` or the `[Export]` attribute for node references. Avoid hardcoded string magic paths where possible.

## Architecture Guidelines (Strategy Game)
- **Turn Structure:** Separate UI/View logic from the simulation/State logic. Use a strict Model-View-Controller (MVC) or state machine setup for the turn loop (Movement Phase, Combat Phase, End Turn).
- **Grid System:** We use a 3D Grid (axial/cubic coordinate math for Hexagonal tiles if applicable). Assume pathfinding uses `AStar3D` or custom C# data structures.
- **Data-Driven Design:** Game data (Unit stats, Spells, Town structures) should favor Godot `Resource` types serialized via `[Export]` or decoupled C# Scriptable data objects.