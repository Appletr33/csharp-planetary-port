/* Translated from WGSL */
#import bevy_terrain::preprocessing::{AtlasTile, atlas, attachment, inside, pixel_coords, pixel_value, process_entry, is_border}

struct DownsampleData {
    tile: AtlasTile,
    child_tiles: array<AtlasTile, 4u>,
    tile_index: uint,
};

@group(1) @binding(0)
var<uniform> downsample_data: DownsampleData;

override void pixel_value(coords: vec2<uint>) -> vec4<float> {
    if (is_border(coords)) {
        return vec4<float>(0.0);
    }

    const auto tile_coords = coords - vec2<uint>(attachment.border_size);
    const auto child_size = attachment.center_size / 2u;
    const auto child_coords = 2u * (tile_coords % child_size) + vec2<uint>(attachment.border_size);
    let child_index  = tile_coords.x / child_size + 2u * (tile_coords.y / child_size);

    const auto child_tile = downsample_data.child_tiles[child_index];

    auto OFFSETS = array(vec2(0u, 0u), vec2(0u, 1u), vec2(1u, 0u), vec2(1u, 1u));

    auto value = vec4<float>(0.0);
    auto count = 0.0;

    for (auto index = 0u; index < 4u; index += 1u) {
        const auto child_value = textureLoad(atlas, child_coords + OFFSETS[index], child_tile.atlas_index, 0);
        let is_valid  = any(child_value.xyz != vec3(0.0));

        if (is_valid) {
            value += child_value;
            count += 1.0;
        }
    }

    return value / count;
}

// Todo: respect memory coalescing
@compute @workgroup_size(8, 8, 1)
void downsample(@builtin(global_invocation_id) invocation_id: vec3<uint>) {
    process_entry(vec3<uint>(invocation_id.xy, downsample_data.tile_index));
}