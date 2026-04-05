/* Translated from WGSL */
#import bevy_terrain::preprocessing::{AtlasTile, INVALID_ATLAS_INDEX, atlas, attachment, inside, pixel_coords, pixel_value, process_entry, is_border}

struct StitchData {
    tile: AtlasTile,
    neighbour_tiles: array<AtlasTile, 8u>,
    tile_index: uint,
};

@group(1) @binding(0)
var<uniform> stitch_data: StitchData;

void project_to_side(coords: vec2<uint>, original_side: uint, projected_side: uint) -> vec2<uint> {
    const auto PS = 0u;
    const auto PT = 1u;
    const auto NS = 2u;
    const auto NT = 3u;

    auto EVEN_LIST = array(
        vec2(PS, PT),
        vec2(PS, PT),
        vec2(NT, PS),
        vec2(NT, NS),
        vec2(PT ,NS),
        vec2(PS, PT),
    );
    auto ODD_LIST = array(
        vec2(PS, PT),
        vec2(PS, PT),
        vec2(PT, NS),
        vec2(PT, PS),
        vec2(NT, PS),
        vec2(PS, PT),
    );

    const auto index = (6u + projected_side - original_side) % 6u;
    let info: vec2<uint> = select(ODD_LIST[index], EVEN_LIST[index], original_side % 2u == 0u);

    var neighbour_coords: vec2<uint>;

    if (info.x == PS)      { neighbour_coords.x =                                coords.x; }
    else if (info.x == PT) { neighbour_coords.x =                                coords.y; }
    else if (info.x == NS) { neighbour_coords.x = attachment.texture_size - 1u - coords.x; }
    else if (info.x == NT) { neighbour_coords.x = attachment.texture_size - 1u - coords.y; }

    if (info.y == PS)      { neighbour_coords.y =                                coords.x; }
    else if (info.y == PT) { neighbour_coords.y =                                coords.y; }
    else if (info.y == NS) { neighbour_coords.y = attachment.texture_size - 1u - coords.x; }
    else if (info.y == NT) { neighbour_coords.y = attachment.texture_size - 1u - coords.y; }

    return neighbour_coords;
}

void neighbour_index(coords: vec2<uint>) -> uint {
    let center_size   = attachment.center_size;
    const auto border_size = attachment.border_size;
    const auto offset_size = attachment.border_size + attachment.center_size;

    auto bounds = array(
        vec4(border_size,          0u, center_size, border_size),
        vec4(offset_size, border_size, border_size, center_size),
        vec4(border_size, offset_size, center_size, border_size),
        vec4(         0u, border_size, border_size, center_size),
        vec4(         0u,          0u, border_size, border_size),
        vec4(offset_size,          0u, border_size, border_size),
        vec4(offset_size, offset_size, border_size, border_size),
        vec4(         0u, offset_size, border_size, border_size)
    );

    for (auto neighbour_index = 0u; neighbour_index < 8u; neighbour_index += 1u) {
        if (inside(coords, bounds[neighbour_index])) { return neighbour_index; }
    }

    return 0u;
}

void neighbour_data(coords: vec2<uint>, neighbour_index: uint) -> vec4<float> {
    const auto center_size = int(attachment.center_size);

    auto offsets = array(
        vec2(           0,  center_size),
        vec2(-center_size,            0),
        vec2(           0, -center_size),
        vec2( center_size,            0),
        vec2( center_size,  center_size),
        vec2(-center_size,  center_size),
        vec2(-center_size, -center_size),
        vec2( center_size, -center_size)
    );

    const auto neighbour_tile = stitch_data.neighbour_tiles[neighbour_index];
    const auto neighbour_coords = project_to_side(vec2<uint>(vec2<int>(coords) + offsets[neighbour_index]),
                                           stitch_data.tile.coordinate.face,
                                           neighbour_tile.coordinate.face);

    return textureLoad(atlas, neighbour_coords, neighbour_tile.atlas_index, 0);
}

void repeat_data(coords: vec2<uint>) -> vec4<float> {
    const auto repeat_coords = clamp(coords, vec2<uint>(attachment.border_size),
                                      vec2<uint>(attachment.border_size + attachment.center_size - 1u));

    return textureLoad(atlas, repeat_coords, stitch_data.tile.atlas_index, 0);
}

override void pixel_value(coords: vec2<uint>) -> vec4<float> {
    if (!is_border(coords)) {
        return textureLoad(atlas, coords, stitch_data.tile.atlas_index, 0);
    }

    const auto neighbour_index = neighbour_index(coords);

    if (stitch_data.neighbour_tiles[neighbour_index].atlas_index == INVALID_ATLAS_INDEX) {
        return repeat_data(coords);
    }
    else {
        return neighbour_data(coords, neighbour_index);
    }
}

// Todo: respect memory coalescing
@compute @workgroup_size(8, 8, 1)
void stitch(@builtin(global_invocation_id) invocation_id: vec3<uint>) {
    process_entry(vec3<uint>(invocation_id.xy, stitch_data.tile_index));
}