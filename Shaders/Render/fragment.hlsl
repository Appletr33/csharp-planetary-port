#define FRAGMENT

#include "../types.hlsl"
#include "../bindings.hlsl"
#include "../functions.hlsl"
#include "../attachments.hlsl"
#include "../debug.hlsl"

struct PS_INPUT {
    float4 clip_position : SV_POSITION;
    [[vk::location(0)]] float2 tile_uv : TEXCOORD0;
    [[vk::location(1)]] uint tile_index : TEXCOORD1;
    [[vk::location(2)]] float view_distance : TEXCOORD2;
    [[vk::location(3)]] float height : TEXCOORD3;
};

struct PS_OUTPUT {
    float4 color : SV_Target;
};

struct FragmentInfo {
    float4 clip_position;
    uint tile_index;
    float height;
    Coordinate coordinate;
    WorldCoordinate world_coordinate;
    TangentSpace tangent_space;
    Blend blend;
};

FragmentInfo fragment_info(PS_INPUT input) {
    FragmentInfo info;
    info.clip_position    = input.clip_position;
    info.tile_index       = input.tile_index;
    info.height           = input.height;
    info.coordinate       = compute_coordinate(input.tile_index, input.tile_uv);
    info.world_coordinate = compute_world_coordinate(info.coordinate, input.height, input.view_distance);
    info.tangent_space    = compute_tangent_space(info.world_coordinate);
    info.blend            = compute_blend(info.world_coordinate.view_distance);
    return info;
}

void fragment_output(inout FragmentInfo info, inout PS_OUTPUT output, float4 color, float3 surface_gradient) {
    float3 world_position = apply_height(info.world_coordinate, info.height);

#ifdef LIGHTING
    // For now we just implement simple lambertian lighting as full PBR is too complex to inline here without bevy_pbr
    float3 light_dir = normalize(float3(1.0f, 1.0f, 1.0f));
    float3 normal = normalize(info.world_coordinate.normal - surface_gradient);
    float diff = max(dot(normal, light_dir), 0.0f);
    float3 diffuse = diff * float3(1.0f, 1.0f, 1.0f);

    // Add relief shading if available
    float rs = relief_shading(info.world_coordinate, surface_gradient);

    output.color = float4(color.rgb * (diffuse + float3(0.1f, 0.1f, 0.1f)) * rs, 1.0f);
#else
    output.color = color;
#endif
}

void fragment_debug(inout FragmentInfo info, inout PS_OUTPUT output, AtlasTile tile, float3 surface_gradient) {
    float3 normal = normalize(info.world_coordinate.normal - surface_gradient);

#ifdef SHOW_DATA_LOD
    output.color = show_data_lod(info.blend, tile);
#endif
#ifdef SHOW_GEOMETRY_LOD
    output.color = show_geometry_lod(info.coordinate, info.tile_index);
#endif
#ifdef SHOW_TILE_TREE
    output.color = show_tile_tree(info.coordinate, info.world_coordinate);
#endif
#ifdef SHOW_PIXELS
    output.color = lerp(output.color, show_pixels(tile), 0.5f);
#endif
#ifdef SHOW_UV
    output.color = float4(tile.coordinate.uv, 0.0f, 1.0f);
#endif
#ifdef SHOW_NORMALS
    output.color = float4(normal, 1.0f);
    // output.color = float4(surface_gradient, 1.0f);
#endif
#ifdef TEST3
    if (high_precision(info.world_coordinate.view_distance)) {
        output.color = lerp(output.color, float4(0.3f, 0.3f, 0.3f, 1.0f), 0.5f);
    }
#endif
}

PS_OUTPUT main(PS_INPUT input)
{
    FragmentInfo info = fragment_info(input);

    AtlasTile tile             = lookup_tile(info.coordinate, info.blend);
    bool mask             = sample_height_mask(tile);
    
    // Create base color depending on height
    float normalized_height = saturate((input.height - 7.0f) / 8.0f);
    float3 low_color = float3(0.1f, 0.3f, 0.8f);
    float3 mid_color = float3(0.2f, 0.6f, 0.2f);
    float3 high_color = float3(0.9f, 0.9f, 0.9f);
    
    float3 final_color;
    if (normalized_height < 0.5f) {
        final_color = lerp(low_color, mid_color, normalized_height * 2.0f);
    } else {
        final_color = lerp(mid_color, high_color, (normalized_height - 0.5f) * 2.0f);
    }

    float4 color = float4(final_color, 1.0f);
    
    float3 surface_gradient = sample_surface_gradient(tile, info.tangent_space);

    if (mask) { discard; }

    PS_OUTPUT output;
    fragment_output(info, output, color, surface_gradient);
    fragment_debug(info, output, tile, surface_gradient);

    // Force alpha to 1.0
    output.color.a = 1.0f;
    return output;
}
