# Fog of war textures (optional)

`shaders/fog_of_war.gdshader` draws the fog procedurally (ADR-176). A file here replaces or adds to a procedural part when it exists:

| File | Used for | Format |
|---|---|---|
| `nebula.png` | the nebula under the Dark look (replaces the procedural one; the stars stay procedural) | seamless tile, 2048×2048, RGB, mostly black |
| `clouds.png` | detail in the Clouds look (mixed with the procedural clouds) | seamless tile, 1024×1024 or 2048×2048, grayscale, white = thick cloud |

Both must tile seamlessly: the shader repeats them over the whole void. Record the source and licence of each file in `CREDITS.md` here.
