// The adventure map's ground (ADR-011, ADR-183): one mesh a level, splat-shaded.
//
// Which terrain a tile has comes from a map-sized texture (one texel a tile, the terrain's number); the terrains' pictures are the layers
// of one texture array. Biomes run into each other along rounded, wandering lines that do not follow the tile grid:
//  - each terrain's "influence" at a pixel is the tile picture blurred over the 3 × 3 tiles around it with a round, smooth kernel, so a
//    single tile of another terrain is a round patch and a diagonal staircase of tiles a slanted line;
//  - the place the picture is read at is pushed about by a slow noise, so the lines wander;
//  - where influences are close the terrains mix, the brighter texels of each ground first, so grass creeps between stones instead of
//    cross-fading.
// A pixel well inside one terrain reads one layer only. Steep ground (cliffs) is textured from the side as well (triplanar).
// The tile grid is an overlay drawn here: thin anti-aliased lines along the tile edges, on when `grid` is.
shader_type spatial;

const int MAX_TERRAINS = 32;

// R = the tile's terrain number / 255. One texel a tile.
uniform sampler2D terrain_map : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float tile_size = 1.0;

uniform sampler2DArray albedo_layers : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
// Per terrain number: its layer and the colour multiplied into it.
uniform int layer_of[MAX_TERRAINS];
uniform vec4 tint_of[MAX_TERRAINS] : source_color;
// Texture repeats per world unit.
uniform float uv_scale = 0.333;

// How wide the mix between two terrains is: a terrain shows where its influence is more than (1 - blend_softness) of the strongest one's.
uniform float blend_softness : hint_range(0.05, 0.8) = 0.42;
// How far the noise moves the borders, in tiles.
uniform float edge_noise : hint_range(0.0, 0.5) = 0.3;

uniform float grid : hint_range(0.0, 1.0) = 0.0;
uniform vec4 grid_color : source_color = vec4(0.0, 0.0, 0.0, 0.4);

varying vec3 local_position;
varying vec3 local_normal;

void vertex() {
	local_position = VERTEX;
	local_normal = NORMAL;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}

// Two octaves, in [0, 1].
float soft_noise(vec2 p) {
	return (value_noise(p) * 2.0 + value_noise(p * 2.13 + vec2(11.7, 3.1))) / 3.0;
}

int terrain_at(ivec2 tile) {
	tile = clamp(tile, ivec2(0), ivec2(map_size) - 1);
	return clamp(int(texelFetch(terrain_map, tile, 0).r * 255.0 + 0.5), 0, MAX_TERRAINS - 1);
}

// The blur's kernel along one axis, for the tiles at -1, 0 and +1 from the nearest one; d is how far the pixel is from that tile's centre
// (-0.5 to 0.5). It falls to nothing at 1.5 tiles, so a tile's share is zero when it enters or leaves the 3 × 3: no seam anywhere.
vec3 kernel(float d) {
	vec3 distance_to = (vec3(-1.0, 0.0, 1.0) - d) / 1.5;
	vec3 k = max(1.0 - distance_to * distance_to, 0.0);
	k = k * k * k;
	k *= k;
	return k / (k.x + k.y + k.z);
}

// A terrain's colour at p (texture space), from above and, where the ground is steep, from the sides. The gradients are passed in because
// this runs inside branches.
vec3 terrain_color(int terrain, vec3 p, vec3 sides, vec3 dx, vec3 dy) {
	float layer = float(layer_of[terrain]);
	vec3 color = sides.y * textureGrad(albedo_layers, vec3(p.xz, layer), dx.xz, dy.xz).rgb;
	if (sides.x > 0.0) {
		color += sides.x * textureGrad(albedo_layers, vec3(p.zy, layer), dx.zy, dy.zy).rgb;
	}
	if (sides.z > 0.0) {
		color += sides.z * textureGrad(albedo_layers, vec3(p.xy, layer), dx.xy, dy.xy).rgb;
	}
	return color * tint_of[terrain].rgb;
}

void fragment() {
	vec3 p = local_position * uv_scale;
	vec3 dx = dFdx(p);
	vec3 dy = dFdy(p);

	// Tiles are centred on whole numbers. The terrain is read at a place pushed about by noise: broad bends plus a little fine wobble.
	vec2 at = local_position.xz / tile_size;
	vec2 bend = vec2(soft_noise(at * 0.8), soft_noise(at * 0.8 + vec2(37.2, 17.9))) - 0.5;
	vec2 wobble = vec2(value_noise(at * 3.3 + 5.5), value_noise(at * 3.3 + vec2(71.1, 9.4))) - 0.5;
	vec2 read_at = at + (bend * 2.0 + wobble * 0.5) * edge_noise;

	ivec2 nearest = ivec2(floor(read_at + 0.5));
	vec2 d = read_at - vec2(nearest);
	vec3 kx = kernel(d.x);
	vec3 ky = kernel(d.y);

	// Each terrain's influence: the 3 × 3 tiles' kernel weights, the same terrain on several tiles added up.
	int terrains[9];
	float influence[9];
	float strongest = 0.0;
	for (int i = 0; i < 9; i++) {
		ivec2 offset = ivec2(i % 3, i / 3);
		terrains[i] = terrain_at(nearest + offset - 1);
		influence[i] = kx[offset.x] * ky[offset.y];
		for (int j = 0; j < i; j++) {
			if (terrains[j] == terrains[i] && influence[j] > 0.0) {
				influence[j] += influence[i];
				influence[i] = 0.0;
				break;
			}
		}
	}
	for (int i = 0; i < 9; i++) {
		strongest = max(strongest, influence[i]);
	}

	// Sides only where the ground is steep: gentle slopes keep the plain top-down picture.
	vec3 sides = pow(max(abs(normalize(local_normal)) - 0.45, 0.0), vec3(2.0));
	sides /= sides.x + sides.y + sides.z;

	// A terrain's height here is its influence, raised a little where its texture is bright. Only terrains that can reach the mix are read.
	float floor_height = 1.0 - blend_softness;
	vec3 colors[9];
	float heights[9];
	float top = 0.0;
	for (int i = 0; i < 9; i++) {
		colors[i] = vec3(0.0);
		heights[i] = 0.0;
		if (influence[i] * 1.2 > strongest * 0.8 * floor_height) {
			colors[i] = terrain_color(terrains[i], p, sides, dx, dy);
			heights[i] = influence[i] * (0.8 + 0.4 * sqrt(dot(colors[i], vec3(0.299, 0.587, 0.114))));
			top = max(top, heights[i]);
		}
	}

	vec3 color = vec3(0.0);
	float total = 0.0;
	for (int i = 0; i < 9; i++) {
		float share = max(heights[i] - top * floor_height, 0.0);
		color += colors[i] * share;
		total += share;
	}
	color /= total;

	// Broad patches a little lighter and darker, so a wide plain of one texture does not read as a tiled picture.
	color *= 0.86 + 0.28 * soft_noise(at * 0.13 + 5.0);

	if (grid > 0.0) {
		vec2 edge = at + 0.5; // tile edges lie on whole numbers
		vec2 distance_to = abs(fract(edge + 0.5) - 0.5);
		vec2 pixel = fwidth(edge);
		vec2 lines = 1.0 - smoothstep(pixel * 0.5, pixel * 1.5, distance_to);
		// Zoomed far out the lines would fill the tiles: they fade away instead.
		float line = max(lines.x, lines.y) * (1.0 - smoothstep(0.12, 0.35, max(pixel.x, pixel.y)));
		color = mix(color, grid_color.rgb, line * grid_color.a * grid);
	}

	ALBEDO = color;
	ROUGHNESS = 0.95;
}
