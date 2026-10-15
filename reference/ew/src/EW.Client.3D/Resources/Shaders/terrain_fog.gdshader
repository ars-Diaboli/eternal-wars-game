shader_type spatial;
render_mode diffuse_lambert; // Removed 'blended' to fix the compilation error

// Completely decoupled global lookups hosted on GPU memory!
global uniform sampler2D fog_texture_mask : filter_linear, repeat_disable;
global uniform vec2 fog_map_size;

// Your original standard mesh asset texture setups (like your grass or brick fields)
uniform sampler2D albedo_texture : source_color;

varying vec3 world_vertex_pos;

void vertex() {
    world_vertex_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // 1. Calculate the base UV coordinate mapping by checking our world placement 
    // assuming your tiles are roughly 2x2 units across in dimensions
    vec2 fog_uv = (world_vertex_pos.xz / 2.0) / fog_map_size;
    
    // 2. Fetch the exploration byte flag color directly from our top-down data texture mask
    float visibility_factor = texture(fog_texture_mask, fog_uv).r;
    
    // 3. Keep your existing mesh graphic asset look intact!
    vec4 base_color = texture(albedo_texture, UV);
    
    // 4. Blend seamlessly to solid black where data texture factor values read 0.0
    ALBEDO = mix(vec3(0.0), base_color.rgb, visibility_factor);
}