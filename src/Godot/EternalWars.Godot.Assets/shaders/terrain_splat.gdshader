// The adventure map's ground (ADR-011, ADR-183, ADR-212): one mesh a level, splat-shaded.
//
// Which terrain a tile has comes from a map-sized texture (one texel a tile, the terrain's number); the terrains' pictures are the layers
// of one texture array. Biomes run into each other along wandering lines that do not follow the tile grid:
//  - each terrain's "influence" at a pixel is the share of its tiles in a small square round the pixel, so it rises from 0 to 1 over a
//    band `blend_width` tiles to each side of a tile edge (a third of a tile: the border blends over two thirds of one) and a pixel
//    further inside a tile is that tile's terrain alone;
//  - the place the picture is read at is pushed about by a slow noise, so the lines wander;
//  - where influences are close the terrains mix by height (height-based splat blending): each layer's alpha is its ground's height
//    (stone tops high, the sand between them low); a terrain stands at its height plus its influence, and only the terrains within
//    `blend_depth` of the highest show, weighted by how far they rise above that line. So stones poke out of the sand and the sand fills
//    the gaps between them before it covers their tops, instead of the two cross-fading.
// One terrain's picture repeats every 16 tiles; to hide the repeat the ground is cut into cells `variation_cell` tiles wide (along the same
// wandering lines), each showing the picture shifted by its own random amount, and neighbouring cells meet with the same height blend over
// the same band, so no straight seam and no regular pattern shows.
// Steep ground (cliffs) is textured from the side as well (triplanar).
// A terrain with detail maps (a layer of `detail_layers`: normal XY, roughness, ambient occlusion) bends the light by its normal map, read
// from above only (the sides stay flat), and gives its roughness and occlusion; the others are flat, 0.95 rough and unoccluded.
// The tile grid is an overlay drawn here: thin anti-aliased lines along the tile edges, on when `grid` is.
shader_type spatial;

const int MAX_TERRAINS = 32;

// R = the tile's terrain number / 255. One texel a tile.
uniform sampler2D terrain_map : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float tile_size = 1.0;

// RGB = the picture, A = the ground's height (bright = raised).
uniform sampler2DArray albedo_layers : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
// Per terrain number: its layer and the colour multiplied into it.
uniform int layer_of[MAX_TERRAINS];
uniform vec4 tint_of[MAX_TERRAINS] : source_color;
// RG = the normal map's XY (OpenGL: green is up the picture), B = roughness, A = ambient occlusion. Layer 0 is unused (flat).
uniform sampler2DArray detail_layers : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
// Per terrain number: its detail layer, 0 for none.
uniform int detail_of[MAX_TERRAINS];
// How strongly the normal maps bend the light.
uniform float normal_strength : hint_range(0.0, 4.0) = 2.5;
// The occlusion map raised to this power: above 1 deepens the crevices.
uniform float occlusion_power : hint_range(0.5, 4.0) = 2.0;
// How much the occlusion darkens direct (sun) light, 0 to 1; Godot's default 0 leaves only ambient light occluded.
uniform float occlusion_light_affect : hint_range(0.0, 1.0) = 1.0;
// Texture repeats per world unit.
uniform float uv_scale = 0.0625;

// How far a border's blend reaches to each side of it, in tiles (at most 0.5).
uniform float blend_width : hint_range(0.02, 0.5) = 0.33;
// How much a ground's relief counts against its influence (0 to 1): the wider, the further one ground's high parts reach into the other.
uniform float height_relief : hint_range(0.0, 1.0) = 1.0;
// How soft the line between the grounds is, in the same units: small = crisp outlines around each stone, large = a cross-fade.
uniform float blend_depth : hint_range(0.01, 0.5) = 0.2;
// How far the noise moves the borders, in tiles.
uniform float edge_noise : hint_range(0.0, 0.5) = 0.3;
// The side of a cell showing one shift of a picture, in tiles.
uniform float variation_cell : hint_range(2.0, 32.0) = 7.0;

uniform float grid : hint_range(0.0, 1.0) = 0.0;
uniform vec4 grid_color : source_color = vec4(0.0, 0.0, 0.0, 0.4);

// Normal XY 0.5 (flat), roughness, no occlusion.
const vec4 FLAT_DETAIL = vec4(0.5, 0.5, 0.95, 1.0);

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

vec3 hash32(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
	p3 += dot(p3, p3.yxz + 33.33);
	return fract((p3.xxy + p3.yzz) * p3.zyx);
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

// 0 well before an edge, 1 well past it, a smooth rise over `width` to each side; x is the distance past the edge.
float ramp(float x, float width) {
	return smoothstep(-width, width, x);
}

// Along one axis, the shares of the tiles at -1, 0 and +1 from the nearest one; d is how far the pixel is from that tile's centre
// (-0.5 to 0.5). Each tile's share is the part of the band round the pixel that lies on it, so the shares add up to 1 and a tile's share
// is already 0 when it enters or leaves the three: no seam anywhere.
vec3 coverage(float d) {
	return vec3(
		ramp(d + 1.5, blend_width) - ramp(d + 0.5, blend_width),
		ramp(d + 0.5, blend_width) - ramp(d - 0.5, blend_width),
		ramp(d - 0.5, blend_width) - ramp(d - 1.5, blend_width));
}

// A terrain's colour (RGB) and height (A) at p (texture space), from above and, where the ground is steep, from the sides; its detail
// from above only, flat on the sides. The gradients are passed in because this runs inside branches.
vec4 terrain_sample(int terrain, vec3 p, vec3 sides, vec3 dx, vec3 dy, out vec4 detail) {
	float layer = float(layer_of[terrain]);
	vec4 texel = sides.y * textureGrad(albedo_layers, vec3(p.xz, layer), dx.xz, dy.xz);
	if (sides.x > 0.0) {
		texel += sides.x * textureGrad(albedo_layers, vec3(p.zy, layer), dx.zy, dy.zy);
	}
	if (sides.z > 0.0) {
		texel += sides.z * textureGrad(albedo_layers, vec3(p.xy, layer), dx.xy, dy.xy);
	}
	detail = FLAT_DETAIL;
	if (detail_of[terrain] > 0) {
		detail = mix(FLAT_DETAIL, textureGrad(detail_layers, vec3(p.xz, float(detail_of[terrain])), dx.xz, dy.xz), sides.y);
	}
	return vec4(texel.rgb * tint_of[terrain].rgb, texel.a);
}

// The terrain's picture with the repeat hidden: the variation cells round `cell` (the read place in cells) each shift the picture by their
// own random amount, and where two or four meet they mix by height over the border's band, as two terrains do. RGB = colour, A = height;
// the detail mixes alike.
vec4 varied_sample(int terrain, vec3 p, vec2 cell, vec3 sides, vec3 dx, vec3 dy, out vec4 detail) {
	ivec2 first = ivec2(floor(cell - 0.5));
	vec2 past = cell - vec2(first + 1); // how far past the line between the first cells and the next, -0.5 to 0.5
	vec2 next = vec2(ramp(past.x, blend_width / variation_cell), ramp(past.y, blend_width / variation_cell));

	vec4 texels[4];
	vec4 details[4];
	float heights[4];
	float top = -1.0;
	for (int i = 0; i < 4; i++) {
		ivec2 offset = ivec2(i % 2, i / 2);
		float share = (offset.x == 1 ? next.x : 1.0 - next.x) * (offset.y == 1 ? next.y : 1.0 - next.y);
		heights[i] = -1.0;
		texels[i] = vec4(0.0);
		details[i] = FLAT_DETAIL;
		if (share > 0.0) {
			vec3 shift = hash32(vec2(first + offset) + float(layer_of[terrain]) * 17.31);
			texels[i] = terrain_sample(terrain, p + shift, sides, dx, dy, details[i]);
			heights[i] = share + texels[i].a * height_relief;
			top = max(top, heights[i]);
		}
	}

	float line = top - blend_depth;
	vec4 result = vec4(0.0);
	detail = vec4(0.0);
	float total = 0.0;
	for (int i = 0; i < 4; i++) {
		float share = max(heights[i] - line, 0.0);
		result += texels[i] * share;
		detail += details[i] * share;
		total += share;
	}
	detail /= total;
	return result / total;
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
	vec2 cell = read_at / variation_cell + vec2(0.37, 0.61); // the cells' borders off the tile edges

	ivec2 nearest = ivec2(floor(read_at + 0.5));
	vec2 d = read_at - vec2(nearest);
	vec3 kx = coverage(d.x);
	vec3 ky = coverage(d.y);

	// Each terrain's influence: the 3 × 3 tiles' shares, the same terrain on several tiles added up in its first slot.
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

	// Height-based blend: a terrain stands at its influence plus its ground's height there; the ones within blend_depth of the highest show,
	// each by how far it rises above that line. The strongest terrain stands at least at its influence, so one that cannot rise to within
	// blend_depth of that even at its highest is not read at all: well inside a terrain that is every other one.
	vec3 colors[9];
	vec4 details[9];
	float heights[9];
	float top = 0.0;
	for (int i = 0; i < 9; i++) {
		colors[i] = vec3(0.0);
		details[i] = FLAT_DETAIL;
		heights[i] = -1.0;
		if (influence[i] > 0.0 && influence[i] + height_relief + blend_depth > strongest) {
			vec4 texel = varied_sample(terrains[i], p, cell, sides, dx, dy, details[i]);
			colors[i] = texel.rgb;
			heights[i] = influence[i] + texel.a * height_relief;
			top = max(top, heights[i]);
		}
	}

	float line = top - blend_depth;
	vec3 color = vec3(0.0);
	vec4 detail = vec4(0.0);
	float total = 0.0;
	for (int i = 0; i < 9; i++) {
		float share = max(heights[i] - line, 0.0);
		color += colors[i] * share;
		detail += details[i] * share;
		total += share;
	}
	color /= total;
	detail /= total;

	// Broad patches a little lighter and darker, so a wide plain of one texture does not read as a tiled picture.
	color *= 0.86 + 0.28 * soft_noise(at * 0.13 + 5.0);

	if (grid > 0.0) {
		vec2 edge = at + 0.5; // tile edges lie on whole numbers
		// Only on the map's own tiles, not on the ground that continues past its edge under the fog.
		float on_map = step(0.0, min(min(edge.x, edge.y), min(map_size.x - edge.x, map_size.y - edge.y)));
		vec2 distance_to = abs(fract(edge + 0.5) - 0.5);
		vec2 pixel = fwidth(edge);
		vec2 lines = 1.0 - smoothstep(pixel * 0.5, pixel * 1.5, distance_to);
		// Zoomed far out the lines would fill the tiles: they fade away instead.
		float grid_line = max(lines.x, lines.y) * (1.0 - smoothstep(0.12, 0.35, max(pixel.x, pixel.y)));
		color = mix(color, grid_color.rgb, grid_line * grid_color.a * grid * on_map);
	}

	// The normal map read from above: its X along the map's x, its Y (up the picture) along -z, added to the mesh's normal.
	vec2 bend_xy = (detail.xy * 2.0 - 1.0) * normal_strength;
	vec3 bent = normalize(normalize(local_normal) + vec3(bend_xy.x, 0.0, -bend_xy.y));
	NORMAL = normalize((VIEW_MATRIX * (MODEL_MATRIX * vec4(bent, 0.0))).xyz);

	ALBEDO = color;
	ROUGHNESS = detail.z;
	// The map is lit mostly by the sun, so the occlusion darkens direct light as well, not only the (faint) ambient light.
	AO = pow(detail.w, occlusion_power);
	AO_LIGHT_AFFECT = occlusion_light_affect;
}
