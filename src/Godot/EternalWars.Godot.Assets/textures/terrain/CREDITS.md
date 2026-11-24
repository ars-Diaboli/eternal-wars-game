# Terrain textures

Ground textures, one per `TerrainType`. The adventure map blends them in `shaders/terrain_splat.gdshader` (`TerrainSplat` puts them
into one texture array, 1024 px a layer; other sizes are resized); the battlefield uses one with world-space triplanar UVs. A missing
file falls back to the flat `Palette` colour.

| File | Source | Licence |
|---|---|---|
| grass, dirt, rough, highlands, forest_floor, wasteland, snow, lava, sand, swamp, water, rock, beach, ice, shallow_water, cursed_lands, rocky_ground (each `.png` with `_normal`, `_roughness`, `_ao`, `_height`) | the owner's terrain PBR sets of 2026-11-24 (`<name>_pbr_120m`: grass_meadow, dirt_churned, rough_limestone, highlands_fine_turf, forest_floor_brown, wasteland_hardpan, snow_windpacked, lava_obsidian_red, sand_golden_ripples, swamp_olive_fine_duckweed, water_deep_calm, rock_fine_slate, beach_fine_shingle, ice_frozen_lake_uniform, water_shallow_ford_fine, cursed_lands_subtle_ash, rocky_granite_fine_plateau); `_normal` is the set's OpenGL normal map, `_height` its 16-bit height cut to 8 bits; lava's emissive map is not used | supplied by the owner: **record the source and licence** |
| subterranean.png | rocks_ground_06 (Poly Haven), from the earlier sandbox project (`reference/ew`) | CC0 |

A picture's `_normal`, `_roughness`, `_ao` and `_height` maps lie beside it; the adventure map reads them, the battlefield only the
picture. Pictures in an `adventure/` folder here (none yet) are the adventure map's in place of the shared ones of the same name (the
battlefield keeps those), with their maps beside them (ADR-240).

A terrain whose own picture is missing borrows a related one with a tint (`AdventureMapView.Tint`): shallow water water's, rocky ground
rough's, cursed lands wasteland's, ice snow's. Every terrain but subterranean has its own picture now, so none borrows.
