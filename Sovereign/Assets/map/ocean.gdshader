shader_type spatial;
render_mode unshaded, cull_disabled;

uniform float paper_amount = 1.0;
uniform float map_zoom = 7.2;
uniform sampler2D physical : filter_linear_mipmap_anisotropic, repeat_disable;
varying vec3 world_pos;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}
float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
               mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
void vertex() {
    world_pos = VERTEX;
}
void fragment() {
    vec2 world_uv = vec2(world_pos.x / 43.2 + 0.5, world_pos.z / 21.6 + 0.5);
    vec4 geography = texture(physical, world_uv);
    float shallow = pow(geography.b, 1.8);
    float large = noise(world_pos.xz * 1.3);
    float waves = noise(world_pos.xz * vec2(33.0, 65.0) + vec2(TIME * 0.045, TIME * 0.021));
    float waves2 = noise(world_pos.xz * vec2(65.0, 29.0) - vec2(TIME * 0.027, TIME * 0.049));
    float glint = pow(max(0.0, waves * 0.62 + waves2 * 0.38), 8.0);
    vec3 deep = mix(vec3(0.040, 0.135, 0.169), vec3(0.062, 0.165, 0.186), large);
    vec3 coast = vec3(0.16, 0.29, 0.29);
    vec3 water = mix(deep, coast, shallow * 0.75);
    water += vec3(0.13, 0.18, 0.18) * glint * 0.65;
    float surf = smoothstep(0.965, 1.0, geography.b) * (1.0 - geography.g);
    water = mix(water, vec3(0.31, 0.41, 0.36), surf * 0.30);
    vec3 paper = mix(vec3(0.73, 0.67, 0.51), vec3(0.86, 0.81, 0.66), large * 0.34 + 0.46);
    // The existing coast-distance channel makes engraving follow real geographic shorelines.
    float distance_band = (1.0 - geography.b) * 42.0;
    float line_distance = abs(fract(distance_band + 0.5) - 0.5);
    float aa = max(fwidth(distance_band), 0.045);
    float engraving = (1.0 - smoothstep(0.035, 0.035 + aa, line_distance)) * smoothstep(0.16, 0.82, geography.b) * (1.0 - geography.g);
    paper -= vec3(0.18, 0.17, 0.13) * engraving * 0.50;
    paper = mix(paper, vec3(0.55, 0.62, 0.56), shallow * 0.13);
    vec2 grid = abs(fract(world_pos.xz / 1.2 + 0.5) - 0.5);
    vec2 grid_aa = max(fwidth(world_pos.xz / 1.2), vec2(0.001));
    float grid_line = max(1.0 - smoothstep(0.0, grid_aa.x, grid.x), 1.0 - smoothstep(0.0, grid_aa.y, grid.y));
    paper *= 1.0 - grid_line * 0.07;
    paper += (noise(world_pos.xz * 120.0) - 0.5) * 0.023;
    ALBEDO = mix(water, paper, paper_amount);
}
