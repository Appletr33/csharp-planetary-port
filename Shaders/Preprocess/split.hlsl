/* Translated from WGSL */
#import bevy_terrain::preprocessing::{AtlasTile, atlas, attachment, pixel_coords, pixel_value, process_entry, is_border, inverse_mix}
#import bevy_terrain::functions::{inside_square};

struct SplitData {
    tile: AtlasTile,
    top_left: vec2<float>,
    bottom_right: vec2<float>,
    tile_index: uint,
};

void inside_square(position: vec2<float>, origin: vec2<float>, size: float) -> float {
    const auto inside = step(origin, position) * step(position, origin + size);

    return inside.x * inside.y;
}

@group(1) @binding(0)
var<uniform> split_data: SplitData;
@group(1) @binding(1)
var source_tile: texture_2d<float>;
@group(1) @binding(2)
var source_tile_sampler: sampler;

override void pixel_value(coords: vec2<uint>) -> vec4<float> {
    if (is_border(coords)) {
        return vec4<float>(0.0);
    }

    const auto tile_coordinate = split_data.tile.coordinate;
    const auto tile_offset =  vec2<float>(float(tile_coordinate.x), float(tile_coordinate.y));
    const auto tile_coords = vec2<float>(coords - vec2<uint>(attachment.border_size)) / float(attachment.center_size);
    const auto tile_scale = exp2(float((tile_coordinate.lod));

    auto source_coords = (tile_offset + tile_coords) / tile_scale;

    source_coords = inverse_mix(split_data.top_left, split_data.bottom_right, source_coords);

    const auto value = textureSampleLevel(source_tile, source_tile_sampler, source_coords, 0.0);

    let is_valid  = all(textureGather(0u, source_tile, source_tile_sampler, source_coords) != vec4<float>(0.0));
    const auto is_inside = inside_square(tile_coords, vec2<float>(0.0), 1.0) == 1.0;

    if (is_valid && is_inside) {
        return value;
    }
    else {
        return textureLoad(atlas, coords, split_data.tile.atlas_index, 0);
    }
}

// Todo: respect memory coalescing
@compute @workgroup_size(8, 8, 1)
void split(@builtin(global_invocation_id) invocation_id: vec3<uint>) {
    process_entry(vec3<uint>(invocation_id.xy, split_data.tile_index));
}