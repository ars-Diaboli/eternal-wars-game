// A UI panel's frame drawn from a rounded box's distance field (BevelFrame, the style sample): a dark fill with a vertical gradient, a
// raised face's bevel or a sunken one's inner shadow, a thin rim lit like a metal bevel (bright where it faces the light and near the lit
// corners, dimming along the edges, a little worn), a dark line inside the rim, a drop shadow and an optional coloured glow (a selected
// item). The node draws a rect larger than the panel by the room the shadow and glow need; `size` is the panel's own size and VERTEX is
// local, so (0, 0) is the panel's corner.
shader_type canvas_item;

uniform vec2 size = vec2(200.0, 100.0);
uniform float radius = 6.0;
uniform float rim_width = 1.4;
uniform float inner_line = 1.2;
uniform vec4 fill_top : source_color = vec4(0.068, 0.07, 0.076, 0.96);
uniform vec4 fill_bottom : source_color = vec4(0.052, 0.054, 0.06, 0.96);
uniform vec4 rim_lit : source_color = vec4(0.80, 0.80, 0.82, 1.0);
uniform vec4 rim_shade : source_color = vec4(0.17, 0.17, 0.19, 1.0);
uniform vec4 inner_line_color : source_color = vec4(0.0, 0.0, 0.0, 0.85);
// The colour the rim takes instead of grey (alpha: how much), keeping its light and shade: a selected item's red.
uniform vec4 rim_tint : source_color = vec4(0.9, 0.1, 0.12, 0.0);
// The angle the light comes from, in degrees (0: from the right, 90: from above; 135: the upper left).
uniform float light_angle = 125.0;
uniform float highlight = 1.0;
// How far (pixels) a lit corner's shine runs along the edges before it fades.
uniform float corner_falloff = 90.0;
uniform float rim_noise = 0.65;
uniform float grain = 0.02;
// 1: the panel stands out (lit edge on the light's side); -1: sunken, a slot or a pressed button.
uniform float raised = 1.0;
// A raised face's bevel: how far (pixels) inside the rim it is lit or shaded, and how much lighter its lit side is.
uniform float bevel_width = 6.0;
uniform float bevel_strength = 0.03;
// A sunken frame's inner shadow.
uniform float inner_shadow = 7.0;
uniform float inner_shadow_strength = 0.45;
uniform float shadow_size = 14.0;
uniform vec2 shadow_offset = vec2(0.0, 4.0);
uniform vec4 shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.6);
uniform vec4 glow_color : source_color = vec4(0.95, 0.12, 0.14, 0.0);
uniform float glow_size = 10.0;
uniform float seed = 0.0;

varying vec2 local;

void vertex() {
	local = VERTEX;
}

float box_sdf(vec2 p, vec2 half_size, float r) {
	vec2 q = abs(p) - half_size + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

vec2 box_normal(vec2 p, vec2 half_size, float r) {
	vec2 q = abs(p) - half_size + r;
	vec2 n;
	if (q.x > 0.0 && q.y > 0.0) {
		n = normalize(q);
	} else if (q.x > q.y) {
		n = vec2(1.0, 0.0);
	} else {
		n = vec2(0.0, 1.0);
	}
	return n * vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
}

float hash1(float x) {
	return fract(sin(x * 127.1 + seed * 311.7) * 43758.5453);
}

float noise1(float x) {
	float i = floor(x);
	float f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(hash1(i), hash1(i + 1.0), f);
}

float hash2(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233)) + seed) * 43758.5453);
}

vec4 over(vec4 dst, vec4 src) {
	float a = src.a + dst.a * (1.0 - src.a);
	vec3 c = (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / max(a, 1e-4);
	return vec4(c, a);
}

// How much of a pixel lies inside the distance d (antialiased over aa).
float inside(float d, float aa) {
	return clamp(0.5 - d / aa, 0.0, 1.0);
}

void fragment() {
	vec2 half_size = size * 0.5;
	vec2 p = local - half_size;
	float r = min(radius, min(half_size.x, half_size.y));
	float d = box_sdf(p, half_size, r);
	float aa = max(fwidth(d), 1e-3);
	vec2 n = box_normal(p, half_size, r);

	// Outside: the drop shadow and the glow.
	float ds = box_sdf(p - shadow_offset, half_size, r);
	// Only outside the panel, so a see-through fill shows the picture behind and not its own shadow.
	vec4 col = vec4(shadow_color.rgb, shadow_color.a * (1.0 - smoothstep(-shadow_size * 0.35, shadow_size, ds)) * (1.0 - inside(d, aa)));
	float outward = clamp(d / max(glow_size, 1e-3), 0.0, 1.0);
	col = over(col, vec4(glow_color.rgb, glow_color.a * (1.0 - outward) * (1.0 - outward) * step(0.0, d)));

	// The light: which way each edge faces it (1: towards it), turned round for a sunken frame.
	float a = radians(light_angle);
	vec2 to_light = vec2(cos(a), -sin(a));
	float towards = dot(n, to_light);
	float facing = towards * raised;
	bool convex = raised > 0.0;

	// The fill: a vertical gradient with a faint grain. A raised face bulges: lighter just inside the edges that face the light, darker
	// inside the far ones. A sunken one is a well: its shadow falls from the top edge.
	vec4 fill = mix(fill_top, fill_bottom, clamp(local.y / size.y, 0.0, 1.0));
	float depth = -d - rim_width - inner_line;
	if (convex) {
		float band = 1.0 - smoothstep(0.0, bevel_width, depth);
		fill.rgb = towards > 0.0
			? fill.rgb + vec3(bevel_strength * towards * band)
			: fill.rgb * (1.0 - 6.0 * bevel_strength * -towards * band); // the shaded side as strong as the lit one
	} else {
		float shade = inner_shadow_strength * (1.0 - smoothstep(0.0, inner_shadow, depth));
		fill.rgb *= 1.0 - shade * (0.6 + 0.8 * max(dot(n, vec2(0.0, -1.0)), 0.0));
	}
	fill.rgb += (hash2(floor(local)) - 0.5) * grain;
	col = over(col, vec4(fill.rgb, fill.a * inside(d, aa)));

	// The rim's light: from the edge's facing, the nearest corner and wear along the edge. A raised rim's far side sinks into shade.
	float lit = smoothstep(-0.45, 1.0, facing);
	float along = max(max(half_size.x - abs(p.x), half_size.y - abs(p.y)) - r, 0.0);
	float corner = exp(-along / max(corner_falloff, 1.0));
	float s = abs(n.x) > abs(n.y) ? p.y + (n.x > 0.0 ? 1000.0 : 3000.0) : p.x + (n.y > 0.0 ? 2000.0 : 0.0);
	float wear = noise1(s / 36.0) * 0.65 + noise1(s / 7.0) * 0.35;
	float b = lit * (0.3 + 0.7 * corner) * highlight;
	b *= mix(1.0, 0.35 + 1.3 * wear, rim_noise);
	vec3 shade_side = convex ? rim_shade.rgb * 0.35 : rim_shade.rgb;
	vec3 rim = mix(shade_side, rim_lit.rgb, clamp(b, 0.0, 1.0)) + max(b - 1.0, 0.0) * 0.4;
	rim = mix(rim, rim_tint.rgb * (0.45 + 0.9 * clamp(b + 0.3, 0.0, 1.2)), rim_tint.a);
	float rim_mask = inside(d, aa) - inside(d + rim_width, aa);
	col = over(col, vec4(rim, rim_mask));

	// The dark line inside the rim: a groove round a well, only a hint on a raised face (a full one reads as a lip round a tray).
	float line_mask = inside(d + rim_width, aa) - inside(d + rim_width + inner_line, aa);
	col = over(col, vec4(inner_line_color.rgb, inner_line_color.a * line_mask * (convex ? 0.3 : 1.0)));

	COLOR = col;
}
