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
    
    // Simply color based on height
    float normalized_height = saturate(input.height / 25.0f);
    
    // Map from blue (low) to green (mid) to white (high)
    float3 low_color = float3(0.1f, 0.3f, 0.8f); // Water-ish
    float3 mid_color = float3(0.2f, 0.6f, 0.2f); // Grass-ish
    float3 high_color = float3(0.9f, 0.9f, 0.9f); // Snow-ish
    
    float3 final_color;
    if (normalized_height < 0.5f) {
        final_color = lerp(low_color, mid_color, normalized_height * 2.0f);
    } else {
        final_color = lerp(mid_color, high_color, (normalized_height - 0.5f) * 2.0f);
    }
    
    output.color = float4(final_color, 1.0f);

    return output;
}
