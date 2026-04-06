#define VERTEX

#include "../types.hlsl"
#include "../bindings.hlsl"
#include "../functions.hlsl"
#include "../attachments.hlsl"

struct VS_INPUT {
    uint vertex_index : SV_VertexID;
};

struct VS_OUTPUT {
    float4 clip_position : SV_POSITION;
    [[vk::location(0)]] float2 tile_uv : TEXCOORD0;
    [[vk::location(1)]] uint tile_index : TEXCOORD1;
    [[vk::location(2)]] float view_distance : TEXCOORD2;
    [[vk::location(3)]] float height : TEXCOORD3;
};

struct VertexInfo {
    uint tile_index;
    Coordinate coordinate;
    WorldCoordinate world_coordinate;
    Blend blend;
};

VertexInfo vertex_info(VS_INPUT input) {
    VertexInfo info;
    info.tile_index       = input.vertex_index / terrain_view.vertices_per_tile;
    info.coordinate       = compute_coordinate(input.vertex_index);
    info.world_coordinate = compute_world_coordinate(info.coordinate, info.tile_index, info.coordinate.uv);
    info.blend            = compute_blend(info.world_coordinate.view_distance);
    return info;
}

VS_OUTPUT vertex_output(inout VertexInfo info, float height) {
    VS_OUTPUT output;
    float3 world_pos = apply_height(info.world_coordinate, height);
    output.clip_position = mul(view.viewProj, float4(world_pos, 1.0f));
    output.tile_uv       = info.coordinate.uv;
    output.tile_index    = info.tile_index;
    output.view_distance = info.world_coordinate.view_distance;
    output.height        = height;
    return output;
}

VS_OUTPUT main(VS_INPUT input)
{
    VertexInfo info   = vertex_info(input);

    AtlasTile tile   = lookup_tile(info.coordinate, info.blend);
    float height = sample_height(tile);

    return vertex_output(info, height);
}
