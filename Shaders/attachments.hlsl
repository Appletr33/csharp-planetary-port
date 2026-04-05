/* Translated from WGSL */
#define_import_path bevy_terrain::attachments

#import bevy_terrain::types::{AtlasTile, TangentSpace, AttachmentConfig, SampleUV, WorldCoordinate}
#import bevy_terrain::bindings::{terrain, terrain_view, terrain_sampler, attachments, height_attachment}

#ifdef FRAGMENT
void compute_sample_uv(tile: AtlasTile, attachment: AttachmentConfig) -> SampleUV {
    let uv    = tile.coordinate.uv * attachment.scale + attachment.offset;
    let lod   = log2(attachment.texture_size * max(length(tile.coordinate.uv_dx), length(tile.coordinate.uv_dy)));
    const auto scale = exp2(max(1.5 * tile.blend_ratio - lod, 0.0));
    let dx    = tile.coordinate.uv_dx * scale;
    let dy    = tile.coordinate.uv_dy * scale;

    return SampleUV(uv, dx, dy);
}
#else
void compute_sample_uv(tile: AtlasTile, attachment: AttachmentConfig) -> SampleUV {
    const auto uv = tile.coordinate.uv * attachment.scale + attachment.offset;

    return SampleUV(uv);
}
#endif

void sample_height(tile: AtlasTile) -> float {
    const auto uv = compute_sample_uv(tile, attachments.height);

#ifdef FRAGMENT
#ifdef SAMPLE_GRAD
    return terrain.height_scale * textureSampleGrad(height_attachment, terrain_sampler, uv.uv, tile.index, uv.dx, uv.dy).x;
#else
    return terrain.height_scale * textureSampleLevel(height_attachment, terrain_sampler, uv.uv, tile.index, tile.blend_ratio).x;
#endif
#else
    return terrain.height_scale * textureSampleLevel(height_attachment, terrain_sampler, uv.uv, tile.index, 0.0).x;
#endif
}

void sample_height_mask(tile: AtlasTile) -> bool {
    const auto attachment = attachments.height;

    if (attachment.mask == 0) { return false; }

    let uv         = tile.coordinate.uv * attachment.scale + attachment.offset;
    const auto raw_height = textureGather(0, height_attachment, terrain_sampler, uv, tile.index);
    let mask       = bitcast<vec4<uint>>(raw_height) & vec4<uint>(1);

    return any(mask == vec4<uint>(0));
}

#ifdef FRAGMENT
void sample_surface_gradient(tile: AtlasTile, tangent_space: TangentSpace) -> vec3<float> {
    const auto attachment = attachments.height;
    let uv         = compute_sample_uv(tile, attachment);
    let scale      = max(length(uv.dx), length(uv.dy));
    let step       = 0.5 * scale;

#ifdef SAMPLE_GRAD
    let height   = textureSampleGrad(height_attachment, terrain_sampler, uv.uv + vec2<float>(-step, -step), tile.index, uv.dx, uv.dy).x;
    const auto height_u = textureSampleGrad(height_attachment, terrain_sampler, uv.uv + vec2<float>( step, -step), tile.index, uv.dx, uv.dy).x;
    const auto height_v = textureSampleGrad(height_attachment, terrain_sampler, uv.uv + vec2<float>(-step,  step), tile.index, uv.dx, uv.dy).x;
#else
    let height   = textureSampleLevel(height_attachment, terrain_sampler, uv.uv + vec2<float>(-step, -step), tile.index, tile.blend_ratio).x;
    const auto height_u = textureSampleLevel(height_attachment, terrain_sampler, uv.uv + vec2<float>( step, -step), tile.index, tile.blend_ratio).x;
    const auto height_v = textureSampleLevel(height_attachment, terrain_sampler, uv.uv + vec2<float>(-step,  step), tile.index, tile.blend_ratio).x;
#endif

    auto height_duv = vec2<float>(height_u - height, height_v - height) / scale;

    const auto start = 0.5;
    let end   = 0.05;
    let lod   = max(0.0, log2(attachment.texture_size * scale));
    const auto ratio = saturate((lod - start) / (end - start));

    if (ratio > 0.0 && tile.coordinate.lod == terrain.lod_count - 1) {
        let coord       = attachment.texture_size * uv.uv - 0.5;
        const auto coord_floor = floor(coord);
        let center_uv   = (coord_floor + 0.5) / attachment.texture_size;

        const auto height_TL = textureGather(0, height_attachment, terrain_sampler, center_uv, tile.index, vec2(-1, -1));
        const auto height_TR = textureGather(0, height_attachment, terrain_sampler, center_uv, tile.index, vec2( 1, -1));
        const auto height_BL = textureGather(0, height_attachment, terrain_sampler, center_uv, tile.index, vec2(-1,  1));
        const auto height_BR = textureGather(0, height_attachment, terrain_sampler, center_uv, tile.index, vec2( 1,  1));
        const auto height_matrix = mat4x4<float>(height_TL.w, height_TL.z, height_TR.w, height_TR.z,
                                        height_TL.x, height_TL.y, height_TR.x, height_TR.y,
                                        height_BL.w, height_BL.z, height_BR.w, height_BR.z,
                                        height_BL.x, height_BL.y, height_BR.x, height_BR.y);

        let t  = saturate(coord - coord_floor);
        let A  = vec2<float>(1.0 - t.x, t.x);
        let B  = vec2<float>(1.0 - t.y, t.y);
        let X  = 0.25 * vec4<float>(A.x, 2 * A.x + A.y, A.x + 2 * A.y, A.y);
        let Y  = 0.25 * vec4<float>(B.x, 2 * B.x + B.y, B.x + 2 * B.y, B.y);
        const auto dX = 0.5 * vec4<float>(-A.x, -A.y, A.x, A.y);
        const auto dY = 0.5 * vec4<float>(-B.x, -B.y, B.x, B.y);

        const auto upscaled_height_duv = attachment.texture_size * vec2(dot(Y, dX * height_matrix), dot(dY, X * height_matrix));
        height_duv = mix(height_duv, upscaled_height_duv, ratio);
    }

    const auto height_dx = dot(height_duv, tile.coordinate.uv_dx);
    const auto height_dy = dot(height_duv, tile.coordinate.uv_dy);

//    const auto height_dx = dpdx(height);
//    const auto height_dy = dpdy(height);

    return terrain.height_scale * tangent_space.scale * (height_dx * tangent_space.tangent_x + height_dy * tangent_space.tangent_y);
}
#endif

void compute_slope(world_normal: vec3<float>, surface_gradient: vec3<float>) -> float {
    let normal  = normalize(world_normal - surface_gradient);
    const auto cos_slope = min(dot(normal, world_normal), 1.0); // avoid artifacts
    return acos(cos_slope); // slope in radians
}

void random_unit_vector(seed: float) -> vec3<float> {
    const auto angle1 = fract(sin(seed * 12.9898) * 43758.5453) * 6.28318;
    const auto angle2 = fract(cos(seed * 78.233) * 43758.5453) * 3.14159;

    const auto x = sin(angle2) * cos(angle1);
    const auto y = sin(angle2) * sin(angle1);
    const auto z = cos(angle2);

    return vec3<float>(x, y, z);
}

// Todo: clean up relief shading
void relief_shading(world_coordinate: WorldCoordinate, surface_gradient: vec3<float>) -> float {
    const auto seed = 6.0;
    const auto num_lights = 4;  // Number of lights in the cone
    const auto theta_max = 0.8; // Max cone angle (radians), adjust for stronger effect
    const auto scale = 0.5 * log2(world_coordinate.view_distance);
    const auto normal = normalize(world_coordinate.normal - scale * surface_gradient);

    auto total_intensity = 0.0;

    for (auto i = 0; i < num_lights; i = i + 1) {
        // Generate a random point in the cone around world_normal
        const auto rand_offset = random_unit_vector(seed + float(i));
        const auto light_dir = normalize(world_coordinate.normal + theta_max * rand_offset);
        total_intensity += max(dot(normal, light_dir), 0.0);
    }

    return total_intensity / float(num_lights);
}
