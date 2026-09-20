shader_type spatial;
render_mode cull_disabled, diffuse_burley;

uniform sampler2D landcover : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D physical : filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D territory_ids : filter_nearest, repeat_disable;
uniform sampler2D territory_palette : source_color, filter_nearest, repeat_disable;
uniform sampler2D province_ids : filter_nearest, repeat_disable;
uniform sampler2D qing_territory_mask : filter_nearest, repeat_disable;
uniform float map_mode = 0.0;
uniform float map_zoom = 7.2;
uniform float province_mode = 0.0;
uniform float selected_province = 0.0;
uniform float selected_territory = 0.0;
uniform vec2 selected_capital = vec2(0.5, 0.22);

float territory(vec2 uv) {
    vec2 encoded = texture(territory_ids, uv).rg;
    return floor(encoded.r * 255.0 + 0.5) + floor(encoded.g * 255.0 + 0.5) * 256.0;
}
float province_at(vec2 uv) {
    return floor(texture(province_ids, uv).r * 255.0 + 0.5);
}
float ink_edge(float center, float left, float right, float top, float bottom) {
    return step(0.5, max(max(abs(center - left), abs(center - right)), max(abs(center - top), abs(center - bottom))));
}

void fragment() {
    vec4 geography = texture(physical, UV);
    if (geography.g < 0.56) { discard; }
    vec3 source = texture(landcover, UV).rgb;
    float luminance = dot(source, vec3(0.299, 0.587, 0.114));
    float close_detail = 1.0 - smoothstep(2.4, 8.0, map_zoom);
    vec2 terrain_texel = vec2(1.0 / 8192.0, 1.0 / 4096.0);
    float dx = texture(physical, UV + vec2(terrain_texel.x, 0.0)).r - texture(physical, UV - vec2(terrain_texel.x, 0.0)).r;
    float dz = texture(physical, UV + vec2(0.0, terrain_texel.y)).r - texture(physical, UV - vec2(0.0, terrain_texel.y)).r;
    // Source elevation gradients and shaded relief supply detail, without invented mountain chains.
    vec3 relief_normal = normalize(vec3(-dx * 18.0, dz * 18.0, 1.0));
    float hillshade = clamp(dot(relief_normal, normalize(vec3(-0.48, 0.60, 1.25))) + 0.19, 0.64, 1.17);
    float relief_tone = clamp(0.83 + luminance * 0.48, 0.83, 1.17);
    // Political cartography retains landform cues without competing with province names.
    // Reduce both fine source-relief contrast and dynamic lighting by 40%; terrain mode stays unchanged.
    if (map_mode < 0.5) {
        hillshade = mix(1.0, hillshade, 0.60);
        relief_tone = mix(1.0, relief_tone, 0.60);
    }
    vec3 colour = source * vec3(0.84, 0.91, 0.79);
    float snow = smoothstep(0.42, 0.70, geography.r);
    float ice = smoothstep(0.53, 0.81, luminance) * smoothstep(0.08, 0.28, geography.r);
    colour = mix(colour, vec3(0.78, 0.81, 0.75), max(ice, snow) * 0.82);
    float country = territory(UV);
    vec2 border_step = max(vec2(1.0 / 4096.0, 1.0 / 2048.0), fwidth(UV) * 0.60);
    if (map_mode < 0.5 && country > 0.5) {
        vec3 tint = texture(territory_palette, vec2((country + 0.5) / 512.0, 0.5)).rgb;
        vec3 paper_tint = mix(tint, vec3(0.85, 0.79, 0.62), 0.27);
        colour = paper_tint * mix(1.0, relief_tone * hillshade, 0.30 + close_detail * 0.58);
        colour = mix(colour, colour * 0.72 + source * 0.38, close_detail * 0.16);
        bool selected = abs(country - selected_territory) < 0.5;
        if (selected) colour = mix(colour, vec3(0.94, 0.82, 0.48), 0.12);
        float edge = ink_edge(country, territory(UV - vec2(border_step.x, 0.0)), territory(UV + vec2(border_step.x, 0.0)),
            territory(UV - vec2(0.0, border_step.y)), territory(UV + vec2(0.0, border_step.y)));
        colour = mix(colour, selected ? vec3(0.52, 0.37, 0.16) : vec3(0.27, 0.28, 0.21), edge * 0.58);
    }
    float province = province_at(UV);
    if (province_mode > 0.5 && province > 0.5 && texture(qing_territory_mask, vec2((country + 0.5) / 512.0, 0.5)).r > 0.5) {
        vec3 shade = vec3(0.77, 0.71, 0.48) + vec3(sin(province * 2.3), sin(province * 1.7), cos(province * 2.0)) * 0.068;
        colour = mix(colour, shade * relief_tone * hillshade, 0.38);
        bool selected = abs(province - selected_province) < 0.5;
        if (selected) colour = mix(colour, vec3(0.97, 0.84, 0.44) * hillshade, 0.35);
        float edge = ink_edge(province, province_at(UV - vec2(border_step.x, 0.0)), province_at(UV + vec2(border_step.x, 0.0)),
            province_at(UV - vec2(0.0, border_step.y)), province_at(UV + vec2(0.0, border_step.y)));
        colour = mix(colour, selected ? vec3(0.98, 0.85, 0.51) : vec3(0.48, 0.40, 0.26), edge * (selected ? 0.84 : 0.37));
    }
    float coast = 1.0 - smoothstep(0.56, 0.91, geography.g);
    colour = mix(colour, vec3(0.36, 0.40, 0.29), coast * 0.32);
    float lit = map_mode < 0.5 ? 0.204 + close_detail * 0.12 : 0.94;
    ALBEDO = colour * lit;
    EMISSION = colour * (1.0 - lit);
    ROUGHNESS = 0.98;
    SPECULAR = 0.08;
    NORMAL_MAP = relief_normal * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = map_mode < 0.5 ? 0.084 + close_detail * 0.114 : 0.46;
}
