[[vk::push_constant]]
struct PushConstants {
    matrix viewProj;
};

PushConstants push_constants;

struct VS_INPUT {
    float3 position : POSITION;
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
    
    float3 world_pos = input.position;
    
    output.clip_position = mul(push_constants.viewProj, float4(world_pos, 1.0f));
    
    // Just map world XZ to UV for now
    output.tile_uv = world_pos.xz * 0.01f;
    output.tile_index = 0;
    output.view_distance = 0;
    output.height = world_pos.y;
    
    return output;
}
