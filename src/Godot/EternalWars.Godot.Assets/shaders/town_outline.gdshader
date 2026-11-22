// The town scene's hover outline (the town screen plan's step 3, task 5; ADR-304): an inverted hull a few pixels wide, drawn as a mesh's material
// overlay. Each vertex moves out along its normal on the screen, by `width` pixels at any distance; only the hull's back faces draw, so the
// building stays as it is and the hull shows round its edges in `colour` (the UI's bronze).
shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled, fog_disabled;

uniform vec4 colour : source_color = vec4(0.85, 0.68, 0.36, 1.0);
uniform float width = 3.0;

void vertex() {
	vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	vec3 normal = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	vec2 offset = (PROJECTION_MATRIX * vec4(normal, 0.0)).xy;
	if (length(offset) > 1e-5) {
		clip.xy += normalize(offset) * width * 2.0 / VIEWPORT_SIZE * clip.w;
	}
	POSITION = clip;
}

void fragment() {
	ALBEDO = colour.rgb;
}
