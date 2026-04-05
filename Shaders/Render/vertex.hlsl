cbuffer CameraData : register(b0)
{
    matrix view;
    matrix projection;
    matrix viewProj;
    float3 cameraPos;
};

// SSBOs usually map to StructuredBuffer in HLSL
StructuredBuffer<uint> tile_indices : register(t0);

struct VS_INPUT {
    uint vertex_id : SV_VertexID;
};

struct VS_OUTPUT {
    float4 clip_position : SV_POSITION;
    float2 tile_uv : TEXCOORD0;
    uint tile_index : TEXCOORD1;
    float view_distance : TEXCOORD2;
    float height : TEXCOORD3;
};

VS_OUTPUT main(VS_INPUT input)
{
    VS_OUTPUT output;
    
    // Stub implementation to get pixels on the screen mapping vertices to bounds
    uint vertices_per_tile = 64; 
    uint tile_index = input.vertex_id / vertices_per_tile;
    
    float2 tile_uv = float2((float)(input.vertex_id % 8) / 8.0f, (float)((input.vertex_id % 64) / 8) / 8.0f);
    
    float height = 0.0f;
    float3 world_pos = float3(tile_uv.x * 10.0f, height, tile_uv.y * 10.0f); 
    
    output.clip_position = mul(float4(world_pos, 1.0f), viewProj);
    output.tile_uv = tile_uv;
    output.tile_index = tile_index;
    output.view_distance = distance(world_pos, cameraPos);
    output.height = height;
    
    return output;
}
