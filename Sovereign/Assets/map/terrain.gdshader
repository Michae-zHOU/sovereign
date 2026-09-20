shader_type spatial;
render_mode cull_disabled, diffuse_burley;

uniform sampler2D landcover : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D physical : filter_linear_mipmap, repeat_disable;
uniform float map_mode = 0.0;
uniform vec2 selected_capital = vec2(0.5, 0.22);
varying vec3 world_pos;

void vertex() {
    world_pos = VERTEX;
}

void fragment() {
    vec4 geography = texture(physical, UV);
    if (geography.g < 0.56) { discard; }
    vec3 source = texture(landcover, UV).rgb;
    // Neutral, subdued geographic colour grading, preserving the real shaded relief.
    vec3 colour = source * vec3(0.64, 0.65, 0.60);
    float luminance = dot(source, vec3(0.299, 0.587, 0.114));
    float ice = smoothstep(0.53, 0.81, luminance) * smoothstep(0.08, 0.28, geography.r);
    float snow = smoothstep(0.42, 0.66, geography.r);
    colour = mix(colour, source * vec3(0.84, 0.87, 0.86), max(ice, snow) * 0.90);
    // Terrain receives a small amount of neutral ambient fill to retain valley detail.
    ALBEDO = colour;
    ROUGHNESS = 0.96;
    SPECULAR = 0.12;
    float dx = texture(physical, UV + vec2(1.0 / 8192.0, 0.0)).r - texture(physical, UV - vec2(1.0 / 8192.0, 0.0)).r;
    float dz = texture(physical, UV + vec2(0.0, 1.0 / 4096.0)).r - texture(physical, UV - vec2(0.0, 1.0 / 4096.0)).r;
    NORMAL_MAP = normalize(vec3(-dx * 12.0, dz * 12.0, 1.0)) * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = 0.42;
    if (map_mode > 0.5 && map_mode < 1.5) {
        float distance_to_capital = length((UV - selected_capital) * vec2(2.0, 1.0));
        float influence = exp(-distance_to_capital * 150.0);
        ALBEDO = mix(ALBEDO, vec3(0.68, 0.47, 0.19), influence * 0.24);
    }
}
