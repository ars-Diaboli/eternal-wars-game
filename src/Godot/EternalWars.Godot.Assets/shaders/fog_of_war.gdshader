// Fog of war over the adventure map (ADR-176): a full-screen pass drawn after the opaque scene.
//
// Each pixel finds where it is on the map from the depth buffer and reads the fog mask there (one texel a tile, faded by FogView), so a tall
// object at the edge shows the part that stands on explored ground and the rest dissolves into the fog, never a hard tile cut. Pixels with
// nothing behind them (the void around the map, the gaps between tiles) use the ground plane.
//
// Two looks, same mask:
//  - Dark: a starry void. Three star layers and a nebula lie on planes far below the map, so they shift with true parallax as the camera
//    pans, rotates and zooms; the nebula drifts and the stars twinkle. The edge of the explored land darkens before the stars come in.
//  - Clouds: two cloud layers on planes above the ground, drifting with the wind, lit on top and grey in the folds; billowy edges.
// Optional textures (textures/fog/nebula.png, clouds.png) replace the procedural nebula and add detail to the clouds.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, fog_disabled, shadows_disabled;

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest, repeat_disable;

// R = hidden (1 = unexplored), G = out of sight (1 = explored but not in sight; refog mode). One texel a tile, with a hidden border.
uniform sampler2D fog_mask : filter_linear, repeat_disable;
uniform vec2 mask_size = vec2(3.0);
uniform float ground_y = 0.0;
uniform int look = 0; // 0 = dark, 1 = clouds

uniform sampler2D nebula_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform bool has_nebula_texture = false;
uniform sampler2D cloud_texture : filter_linear_mipmap, repeat_enable;
uniform bool has_cloud_texture = false;

const float OUT_OF_SIGHT_DARK = 0.45;
const float OUT_OF_SIGHT_CLOUDS = 0.38;

void vertex() {
	POSITION = vec4(VERTEX.xy, 0.5, 1.0);
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

vec2 hash22(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.xx + p3.yz) * p3.zy);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p, int octaves) {
	float sum = 0.0;
	float amplitude = 0.5;
	mat2 turn = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
	for (int i = 0; i < octaves; i++) {
		sum += amplitude * value_noise(p);
		p = turn * p * 2.03 + vec2(11.7, 3.1);
		amplitude *= 0.5;
	}
	return sum;
}

// The weights 1 4 6 4 1 of a 5-tap binomial filter.
float binomial(int i) {
	int a = abs(i);
	return a == 0 ? 6.0 : (a == 1 ? 4.0 : 1.0);
}

// Where the ray from the camera meets the horizontal plane at height y (rays never look up here; the camera tilts at most 30°).
vec2 on_plane(vec3 origin, vec3 dir, float y) {
	float t = (y - origin.y) / min(dir.y, -0.05);
	return origin.xz + dir.xz * max(t, 0.0);
}

// Hidden and out-of-sight amounts at a map point, blurred (a 5×5 binomial, close to a Gaussian of about a tile) so the tile staircase
// rounds off: a revealed circle of tiles reads as a round clearing and its edge fades softly over about two tiles.
vec2 mask_at(vec2 xz) {
	vec2 uv = (xz + 1.5) / mask_size;
	vec2 texel = 0.8 / mask_size;
	vec2 sum = vec2(0.0);
	for (int y = -2; y <= 2; y++) {
		for (int x = -2; x <= 2; x++) {
			float weight = binomial(x) * binomial(y);
			sum += texture(fog_mask, uv + vec2(float(x), float(y)) * texel).rg * weight;
		}
	}
	return sum / 256.0;
}

// A star's colour by its temperature, like real stars: most are near white (pale yellow to pale blue), a few are accents, from deep orange
// (cool) to blue (hot). h in [0, 1). The colours are as seen on screen (sRGB) and returned linear, since the pass's output is converted to sRGB.
vec3 star_tint_srgb(float h) {
	// An S-curve keeps most stars near the white middle; only the ends reach the strong colours.
	float t = h < 0.5 ? 0.5 * pow(2.0 * h, 0.55) : 1.0 - 0.5 * pow(2.0 - 2.0 * h, 0.55);
	vec3 orange = vec3(1.0, 0.55, 0.28);
	vec3 yellow = vec3(1.0, 0.86, 0.62);
	vec3 white = vec3(1.0, 0.97, 0.94);
	vec3 pale_blue = vec3(0.78, 0.86, 1.0);
	vec3 blue = vec3(0.45, 0.6, 1.0);
	if (t < 0.25) return mix(orange, yellow, t / 0.25);
	if (t < 0.5) return mix(yellow, white, (t - 0.25) / 0.25);
	if (t < 0.75) return mix(white, pale_blue, (t - 0.5) / 0.25);
	return mix(pale_blue, blue, (t - 0.75) / 0.25);
}

vec3 star_tint(float h) {
	return pow(star_tint_srgb(h), vec3(2.2));
}

// One layer of stars on a plane: a few cells hold a star of their own size, colour and twinkle.
vec3 star_layer(vec2 p, float cell, float density, float size, float time) {
	vec2 grid = p / cell;
	vec2 id = floor(grid);
	vec3 light = vec3(0.0);
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 c = id + vec2(float(x), float(y));
			float chance = hash12(c + 71.3);
			if (chance > density) {
				continue;
			}
			vec2 at = c + 0.15 + 0.7 * hash22(c);
			float d = length(grid - at) * cell;
			// About one star in eight is a big bright one.
			float big = step(0.88, hash12(c + 13.9));
			float bright = 0.35 + 0.65 * pow(hash12(c + 5.1), 3.0) + 0.5 * big;
			float r = size * (0.6 + 0.8 * hash12(c + 9.7)) * (1.0 + 1.3 * big);
			float twinkle = 0.7 + 0.3 * sin(time * (1.0 + 2.0 * hash12(c + 2.2)) + 6.2831 * chance * 37.0);
			float core = smoothstep(r, 0.0, d);
			float glow = 0.25 * smoothstep(r * 3.5, 0.0, d);
			// The core is whiter than its halo (a bright point burns out to white); the halo carries the star's colour.
			vec3 tint = star_tint(hash12(c + 3.3));
			light += bright * twinkle * (mix(tint, vec3(1.0), 0.12) * core * core + tint * glow * glow);
		}
	}
	return light;
}

vec3 space(vec3 origin, vec3 dir, float time) {
	// The nebula: deepest; two noise fields, violet and blue, drifting slowly.
	vec2 deep = on_plane(origin, dir, ground_y - 160.0);
	vec3 nebula;
	if (has_nebula_texture) {
		nebula = texture(nebula_texture, deep * 0.0035 + vec2(time * 0.0012, 0.0)).rgb * 0.6;
	} else {
		vec2 q = deep * 0.02 + vec2(time * 0.004, time * 0.0015);
		float warp = fbm(q * 0.7 + 4.0, 3);
		float violet = fbm(q + warp * 1.6, 5);
		float blue = fbm(q * 1.3 - warp + 9.0, 4);
		// Small faint wisps, violet-blue, on a truly black sky: most of the void has no nebula at all.
		nebula = vec3(0.06, 0.05, 0.28) * pow(smoothstep(0.66, 0.92, violet), 2.0) * 0.55
			+ vec3(0.02, 0.05, 0.20) * pow(smoothstep(0.68, 0.95, blue), 2.0) * 0.4;
	}

	vec3 stars = vec3(0.0);
	// Star layers: the farther, the denser and fainter, each sized so a star covers about the same few pixels wherever it is.
	float t1 = abs((ground_y - 40.0 - origin.y) / min(dir.y, -0.05));
	float t2 = abs((ground_y - 80.0 - origin.y) / min(dir.y, -0.05));
	float t3 = abs((ground_y - 150.0 - origin.y) / min(dir.y, -0.05));
	stars += star_layer(on_plane(origin, dir, ground_y - 40.0), 3.0, 0.02, t1 * 0.0030, time) * 1.8;
	stars += star_layer(on_plane(origin, dir, ground_y - 80.0), 3.6, 0.03, t2 * 0.0022, time * 0.8) * 1.2;
	stars += star_layer(on_plane(origin, dir, ground_y - 150.0), 4.4, 0.05, t3 * 0.0016, time * 0.6) * 0.8;
	// A bright star is dimmed as a whole rather than clipped channel by channel, so a hot star stays blue instead of burning out to white.
	stars /= max(1.0, max(stars.r, max(stars.g, stars.b)));
	return nebula + stars;
}

void fragment() {
	float depth = texture(depth_texture, SCREEN_UV).x;
	vec3 origin = INV_VIEW_MATRIX[3].xyz;
	vec4 far_view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, 0.5, 1.0);
	vec3 through = (INV_VIEW_MATRIX * vec4(far_view.xyz / far_view.w, 1.0)).xyz;
	vec3 dir = normalize(through - origin);

	// Where on the map this pixel is: the drawn surface, or the ground plane where nothing is drawn.
	vec2 xz;
	if (depth > 0.0) {
		vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
		xz = (INV_VIEW_MATRIX * vec4(view.xyz / view.w, 1.0)).xz;
	} else {
		xz = on_plane(origin, dir, ground_y);
	}

	float time = TIME;
	// The edge sways gently: the mask is read through a slow, broad noise warp of under a tile (few octaves, so the line stays smooth).
	vec2 warp = vec2(fbm(xz * 0.14 + vec2(time * 0.03, 0.0), 2), fbm(xz * 0.14 + vec2(17.3, time * -0.025), 2)) - 0.5;
	vec2 mask = mask_at(xz + warp * 0.9);
	// Past the map's edge is always fully hidden, and the map's own edge melts into it over about a tile along a wandering line, never a
	// straight cut, even where the land is explored up to the edge.
	vec2 map_size = mask_size - 2.0;
	vec2 beyond = max(vec2(-0.5) - xz, xz - (map_size - 0.5));
	float edge = max(beyond.x, beyond.y) + (warp.x + warp.y) * 2.0;
	float outside = smoothstep(-0.5, 0.9, edge);
	float hidden = max(mask.r, outside);
	float unseen = mask.g;

	if (look == 0) {
		// A wide dark rim first (the land sinks into shadow), then the stars come in.
		float fog = smoothstep(0.1, 0.85, hidden);
		float stars = smoothstep(0.6, 1.0, hidden);
		vec3 void_color = space(origin, dir, time) * stars;
		float veil = (1.0 - fog) * OUT_OF_SIGHT_DARK * smoothstep(0.1, 0.9, unseen);
		float alpha = fog + veil;
		vec3 color = alpha > 0.0 ? void_color * fog / alpha : vec3(0.0);
		// Nothing is drawn behind the map's edge: cover the background there even where the fog is still thin (the colour stays the dark rim).
		float backdrop = depth <= 0.0 ? smoothstep(-0.8, -0.3, edge) : 0.0;
		alpha = max(alpha, backdrop);
		ALBEDO = color;
		ALPHA = clamp(alpha, 0.0, 1.0);
	} else {
		vec2 wind = vec2(0.22, 0.09) * time;
		vec2 high = on_plane(origin, dir, ground_y + 6.0) + wind;
		vec2 low = on_plane(origin, dir, ground_y + 3.0) + wind * 1.6;
		float n1 = fbm(high * 0.11, 5);
		float n2 = fbm(low * 0.19 + 5.0, 4);
		if (has_cloud_texture) {
			n1 = mix(n1, texture(cloud_texture, high * 0.02).r, 0.6);
			n2 = mix(n2, texture(cloud_texture, low * 0.035 + 0.37).r, 0.6);
		}
		float puff = 0.6 * n1 + 0.4 * n2;
		// A wide ramp with only a little puff in it (so the edge stays a smooth line), yet fully hidden ground is always fully covered (puff
		// stays over 0.1: (1 - 0.2) * 1.75 - 0.4 * 0.5 > 1) and explored ground never gets a wisp (puff under 0.9: -0.2 * 1.75 + 0.4 * 0.5 < 0).
		float density = clamp((hidden - 0.2) * 1.75 + (puff - 0.5) * 0.5, 0.0, 1.0);
		float cloud = smoothstep(0.0, 1.0, density);
		// Lit tops, blue-grey folds; kept below pure white so the clouds have shape (and the HUD's text stays readable).
		float lit = smoothstep(0.38, 0.75, n1 + 0.45 * (n2 - 0.5));
		vec3 cloud_color = mix(vec3(0.42, 0.48, 0.60), vec3(0.86, 0.88, 0.93), lit);
		// Thin edges catch the light.
		cloud_color = mix(cloud_color, vec3(0.95), (1.0 - cloud) * 0.3);
		float haze = (1.0 - cloud) * OUT_OF_SIGHT_CLOUDS * smoothstep(0.1, 0.9, unseen) * (0.75 + 0.5 * n2);
		float alpha = cloud + haze;
		vec3 haze_color = vec3(0.86, 0.89, 0.95);
		ALBEDO = alpha > 0.0 ? (cloud_color * cloud + haze_color * haze) / alpha : vec3(1.0);
		ALPHA = clamp(alpha, 0.0, 1.0);
	}
}
