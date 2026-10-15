# EternalWars.Godot.Assets

Engine assets shared by every Godot app: models, textures, materials, shaders, audio, fonts, themes and
reusable scenes ("prefabs").

There is no `.csproj` here. Godot can only load resources from inside a project's `res://` tree, so
`tools/link-shared-assets.ps1` mounts this folder into each app as `res://shared/` via a directory
junction (Windows) or symlink (Linux/macOS). The links are git-ignored. Run the script once after cloning.

```
models/      .glb / .gltf (+ .import files Godot generates)
textures/
materials/   .tres StandardMaterial3D / ShaderMaterial
shaders/     .gdshader
audio/
fonts/
themes/      UI .tres themes
scenes/      reusable .tscn prefabs (hero, town, map objects, UI widgets)
```

Rules:

- Reference assets by `res://shared/...` paths only.
- Scenes here must not attach scripts that live in an app project. Scripts for prefabs belong in
  `EternalWars.Godot.Shared` / `EternalWars.Godot.UI` (see docs/architecture.md, "Godot libraries").
- Large binaries go through Git LFS (`.gitattributes` at the repository root; see docs/development.md).
- Record the source and licence of every third-party asset next to it (e.g. `textures/terrain/CREDITS.md`).
