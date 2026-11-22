// The town screen's sky (the town screen plan's step 3, task 2): a town type's painting (bg_town_<town>) spread over the part of the sky the
// hero shot sees. Its middle faces `azimuth` (radians, 0 toward -Z), it is `span` radians wide and as tall as its shape says, and its
// horizon (`horizon`, from the bottom) lies on the true horizon; above the painting the sky fades to `above`.
shader_type sky;

uniform sampler2D picture : source_color, filter_linear, repeat_disable;
uniform float azimuth = 0.0;
uniform float span = 2.1;
uniform float horizon = 0.35;
uniform vec3 above : source_color = vec3(0.3, 0.45, 0.7);

void sky() {
	vec2 size = vec2(textureSize(picture, 0));
	float turn = mod(atan(EYEDIR.x, -EYEDIR.z) - azimuth + PI, TAU) - PI;
	float elevation = asin(clamp(EYEDIR.y, -1.0, 1.0));
	float u = 0.5 + turn / span;
	float v = horizon + elevation / (span * size.y / size.x);
	vec3 painted = texture(picture, vec2(clamp(u, 0.0, 1.0), 1.0 - clamp(v, 0.0, 1.0))).rgb;
	COLOR = mix(painted, above, smoothstep(0.97, 1.2, v));
}
