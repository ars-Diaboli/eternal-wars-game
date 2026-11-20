# Terrain textures

Placeholder ground textures taken from the earlier sandbox project (`reference/ew`), one per `TerrainType`.
The adventure map blends them in `shaders/terrain_splat.gdshader` (`TerrainSplat` puts them into one texture array,
1024 px a layer; other sizes are resized); the battlefield uses one with world-space triplanar UVs. A missing file
falls back to the flat `Palette` colour. Snow and ice have no texture yet.

| File | Source (sandbox file) | Licence |
|---|---|---|
| grass.png, grass_normal/roughness/ao/height.png | grass_uniform_pbr, a PBR set supplied by the owner (2026-11-20); the AO map lightened to 0.75–1 (it was 0.07–0.89 and darkened the grass to near black) | supplied by the owner: **record the source and licence** |
| dirt.png, dirt_normal/roughness/ao/height.png | dirt_pbr_maps, a PBR set supplied by the owner (2026-11-20), 512 px (the game scales it to 1024); made tileable by blending its edges with the middle (it was not seamless), the AO map lightened to 0.75–1 | supplied by the owner: **record the source and licence** |
| rough.png, rough_normal/roughness/ao/height.png | limestone_pbr_material, a PBR set supplied by the owner (2026-11-20); its left and right edges blended with the middle (the albedo had a bright left band, a dark right one and a hard seam) | supplied by the owner: **record the source and licence** |
| highlands.png, highlands_normal/roughness/ao/height.png | highlands_cyan_pbr, a PBR set supplied by the owner (2026-11-20); the maps' bright 5 px border blended with the middle (it drew a raised grid), the AO map lightened to 0.75–1 | supplied by the owner: **record the source and licence** |
| forest_floor.png, forest_floor_normal/roughness/ao/height.png | forest_floor_pbr, a PBR set supplied by the owner (2026-11-20); its mirrored 48 px edges replaced by a blend with the middle (they showed as kaleidoscope strips) | supplied by the owner: **record the source and licence** |
| wasteland.png, wasteland_normal/roughness/ao/height.png | wasteland_pbr_improved, a PBR set supplied by the owner (2026-11-20); its smeared 30 px edges replaced by a blend with the middle | supplied by the owner: **record the source and licence** |
| sand.png | rocky_trail (Poly Haven) | CC0 |
| lava.png | red_laterite_soil_stones (Poly Haven), darkened in the material | CC0 |
| swamp.png | coast_sand_rocks_02 (Poly Haven) | CC0 |
| subterranean.png | rocks_ground_06 (Poly Haven) | CC0 |
| rock.png | rock_pitted_mossy (Poly Haven) | CC0 |
| water.png | ganges_river_pebbles (Poly Haven), tinted blue in the material | CC0 |
| beach.png | coast_land_rocks_01 (Poly Haven) | CC0 |

A picture's `_normal`, `_roughness`, `_ao` and `_height` maps lie beside it (grass, dirt, rough, highlands, forest floor and wasteland so far); the adventure map reads them, the
battlefield only the picture. Pictures in an `adventure/` folder here (none yet) are the adventure map's in place of the shared ones of
the same name (the battlefield keeps those), with their maps beside them (ADR-240).

Shallow water, rocky ground, cursed lands and ice borrow the water, rough, wasteland and snow textures with a
tint (`AdventureMapView.TextureOf`); ice and snow have no texture yet, so they use their palette colours.
