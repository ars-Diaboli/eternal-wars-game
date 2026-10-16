# Terrain textures

Placeholder ground textures taken from the earlier sandbox project (`reference/ew`), one per `TerrainType`.
Rendered with world-space triplanar UVs by `AdventureMapView`; a missing file falls back to the flat
`Palette` colour. Snow and ice have no texture yet.

| File | Source (sandbox file) | Licence |
|---|---|---|
| grass.png | top-view-bright-green-grass-texture-background | unknown stock image: **check before release** |
| dirt.png | brown_mud_03 (Poly Haven) | CC0 |
| rough.png | rock_06 (Poly Haven) | CC0 |
| highlands.png | grass-field | unknown stock image: **check before release** |
| forest_floor.png | Poliigon GrassPatchyGround 4585 | Poliigon free licence: **check redistribution terms** |
| wasteland.png | lichen_rock (Poly Haven) | CC0 |
| sand.png | rocky_trail (Poly Haven) | CC0 |
| lava.png | red_laterite_soil_stones (Poly Haven), darkened in the material | CC0 |
| swamp.png | coast_sand_rocks_02 (Poly Haven) | CC0 |
| subterranean.png | rocks_ground_06 (Poly Haven) | CC0 |
| rock.png | rock_pitted_mossy (Poly Haven) | CC0 |
| water.png | ganges_river_pebbles (Poly Haven), tinted blue in the material | CC0 |
| beach.png | coast_land_rocks_01 (Poly Haven) | CC0 |

Shallow water, rocky ground, cursed lands and ice borrow the water, rough, wasteland and snow textures with a
tint (`AdventureMapView.TextureOf`); ice and snow have no texture yet, so they use their palette colours.
