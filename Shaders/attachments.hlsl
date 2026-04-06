#ifndef ATTACHMENTS_HLSL
#define ATTACHMENTS_HLSL

#include "types.hlsl"
#include "bindings.hlsl"

#ifdef FRAGMENT
SampleUV compute_sample_uv(AtlasTile tile, AttachmentConfig attachment) {
    float2 uv    = tile.coordinate.uv * attachment.scale + attachment.offset;
    float lod   = log2(attachment.texture_size * max(length(tile.coordinate.uv_dx), length(tile.coordinate.uv_dy)));
    float scale = exp2(max(1.5f * tile.blend_ratio - lod, 0.0f));
    float2 dx    = tile.coordinate.uv_dx * scale;
    float2 dy    = tile.coordinate.uv_dy * scale;

    SampleUV s = {uv, dx, dy};
    return s;
}
#else
SampleUV compute_sample_uv(AtlasTile tile, AttachmentConfig attachment) {
    float2 uv = tile.coordinate.uv * attachment.scale + attachment.offset;

    SampleUV s = {uv};
    return s;
}
#endif

float sample_height(AtlasTile tile) {
    SampleUV uv = compute_sample_uv(tile, attachments.height);

#ifdef FRAGMENT
#ifdef SAMPLE_GRAD
    return terrain.height_scale * height_attachment.SampleGrad(terrain_sampler, float3(uv.uv, tile.index), uv.dx, uv.dy).x;
#else
    return terrain.height_scale * height_attachment.SampleLevel(terrain_sampler, float3(uv.uv, tile.index), tile.blend_ratio).x;
#endif
#else
    return terrain.height_scale * height_attachment.SampleLevel(terrain_sampler, float3(uv.uv, tile.index), 0.0f).x;
#endif
}

bool sample_height_mask(AtlasTile tile) {
    AttachmentConfig attachment = attachments.height;

    if (attachment.mask == 0) { return false; }

    float2 uv         = tile.coordinate.uv * attachment.scale + attachment.offset;
    float4 raw_height = height_attachment.GatherRed(terrain_sampler, float3(uv, tile.index));
    uint4 mask       = asuint(raw_height) & uint4(1, 1, 1, 1);

    return any(mask == uint4(0, 0, 0, 0));
}

#ifdef FRAGMENT
float3 sample_surface_gradient(AtlasTile tile, TangentSpace tangent_space) {
    AttachmentConfig attachment = attachments.height;
    SampleUV uv         = compute_sample_uv(tile, attachment);
    float scale      = max(length(uv.dx), length(uv.dy));
    float step       = 0.5f * scale;

#ifdef SAMPLE_GRAD
    float height   = height_attachment.SampleGrad(terrain_sampler, float3(uv.uv + float2(-step, -step), tile.index), uv.dx, uv.dy).x;
    float height_u = height_attachment.SampleGrad(terrain_sampler, float3(uv.uv + float2( step, -step), tile.index), uv.dx, uv.dy).x;
    float height_v = height_attachment.SampleGrad(terrain_sampler, float3(uv.uv + float2(-step,  step), tile.index), uv.dx, uv.dy).x;
#else
    float height   = height_attachment.SampleLevel(terrain_sampler, float3(uv.uv + float2(-step, -step), tile.index), tile.blend_ratio).x;
    float height_u = height_attachment.SampleLevel(terrain_sampler, float3(uv.uv + float2( step, -step), tile.index), tile.blend_ratio).x;
    float height_v = height_attachment.SampleLevel(terrain_sampler, float3(uv.uv + float2(-step,  step), tile.index), tile.blend_ratio).x;
#endif

    float2 height_duv = float2(height_u - height, height_v - height) / scale;

    float start = 0.5f;
    float end   = 0.05f;
    float lod   = max(0.0f, log2(attachment.texture_size * scale));
    float ratio = saturate((lod - start) / (end - start));

    if (ratio > 0.0f && tile.coordinate.lod == terrain.lod_count - 1) {
        float2 coord       = attachment.texture_size * uv.uv - 0.5f;
        float2 coord_floor = floor(coord);
        float2 center_uv   = (coord_floor + 0.5f) / attachment.texture_size;

        float4 height_TL = height_attachment.GatherRed(terrain_sampler, float3(center_uv, tile.index), int2(-1, -1));
        float4 height_TR = height_attachment.GatherRed(terrain_sampler, float3(center_uv, tile.index), int2( 1, -1));
        float4 height_BL = height_attachment.GatherRed(terrain_sampler, float3(center_uv, tile.index), int2(-1,  1));
        float4 height_BR = height_attachment.GatherRed(terrain_sampler, float3(center_uv, tile.index), int2( 1,  1));
        float4x4 height_matrix = float4x4(height_TL.w, height_TL.z, height_TR.w, height_TR.z,
                                        height_TL.x, height_TL.y, height_TR.x, height_TR.y,
                                        height_BL.w, height_BL.z, height_BR.w, height_BR.z,
                                        height_BL.x, height_BL.y, height_BR.x, height_BR.y);

        float2 t  = saturate(coord - coord_floor);
        float2 A  = float2(1.0f - t.x, t.x);
        float2 B  = float2(1.0f - t.y, t.y);
        float4 X  = 0.25f * float4(A.x, 2.0f * A.x + A.y, A.x + 2.0f * A.y, A.y);
        float4 Y  = 0.25f * float4(B.x, 2.0f * B.x + B.y, B.x + 2.0f * B.y, B.y);
        float4 dX = 0.5f * float4(-A.x, -A.y, A.x, A.y);
        float4 dY = 0.5f * float4(-B.x, -B.y, B.x, B.y);

        float2 upscaled_height_duv = attachment.texture_size * float2(dot(Y, mul(height_matrix, dX)), dot(dY, mul(height_matrix, X)));
        height_duv = lerp(height_duv, upscaled_height_duv, ratio);
    }

    float height_dx = dot(height_duv, tile.coordinate.uv_dx);
    float height_dy = dot(height_duv, tile.coordinate.uv_dy);

//    float height_dx = ddx(height);
//    float height_dy = ddy(height);

    return terrain.height_scale * tangent_space.scale * (height_dx * tangent_space.tangent_x + height_dy * tangent_space.tangent_y);
}
#endif

float compute_slope(float3 world_normal, float3 surface_gradient) {
    float3 normal  = normalize(world_normal - surface_gradient);
    float cos_slope = min(dot(normal, world_normal), 1.0f); // avoid artifacts
    return acos(cos_slope); // slope in radians
}

float3 random_unit_vector(float seed) {
    float angle1 = frac(sin(seed * 12.9898f) * 43758.5453f) * 6.28318f;
    float angle2 = frac(cos(seed * 78.233f) * 43758.5453f) * 3.14159f;

    float x = sin(angle2) * cos(angle1);
    float y = sin(angle2) * sin(angle1);
    float z = cos(angle2);

    return float3(x, y, z);
}

// Todo: clean up relief shading
float relief_shading(WorldCoordinate world_coordinate, float3 surface_gradient) {
    float seed = 6.0f;
    int num_lights = 4;  // Number of lights in the cone
    float theta_max = 0.8f; // Max cone angle (radians), adjust for stronger effect
    float scale = 0.5f * log2(world_coordinate.view_distance);
    float3 normal = normalize(world_coordinate.normal - scale * surface_gradient);

    float total_intensity = 0.0f;

    for (int i = 0; i < num_lights; i++) {
        // Generate a random point in the cone around world_normal
        float3 rand_offset = random_unit_vector(seed + float(i));
        float3 light_dir = normalize(world_coordinate.normal + theta_max * rand_offset);
        total_intensity += max(dot(normal, light_dir), 0.0f);
    }

    return total_intensity / float(num_lights);
}

#endif // ATTACHMENTS_HLSL
