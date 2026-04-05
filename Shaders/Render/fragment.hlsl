Texture2DArray tileAtlas : register(t0);
SamplerState atlasSampler : register(s0);

struct PS_INPUT {
    float4 clip_position : SV_POSITION;
    float2 tile_uv : TEXCOORD0;
    uint tile_index : TEXCOORD1;
    float view_distance : TEXCOORD2;
    float height : TEXCOORD3;
};

struct PS_OUTPUT {
    float4 color : SV_Target;
};

PS_OUTPUT main(PS_INPUT input)
{
    PS_OUTPUT output;
    
    // Basic Albedo mapping proxying to the Atlas layer instead of complex Bevy Tangent space PBR
    // Atlas Z layer correlates to v_TileIndex in memory
    float4 tex_color = tileAtlas.Sample(atlasSampler, float3(input.tile_uv, (float)input.tile_index));
    
    // Stub visualization indicating mesh grid bounds and tile indices visually!
    output.color = lerp(tex_color, float4(0.5f, 0.5f, 0.5f, 1.0f), 0.5f);
    
    // Dummy wireframe border highlight to see quadnodes
    if (input.tile_uv.x < 0.02f || input.tile_uv.x > 0.98f || input.tile_uv.y < 0.02f || input.tile_uv.y > 0.98f) {
        output.color = float4(1.0f, 0.0f, 0.0f, 1.0f); // Red borders for LOD edges
    }
    
    return output;
}
