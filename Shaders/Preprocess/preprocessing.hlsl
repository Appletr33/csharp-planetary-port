/* Translated from WGSL */
#define_import_path bevy_terrain::preprocessing

const FORMAT_R8: uint = 2u;
const FORMAT_RGBA8: uint = 0u;
const FORMAT_R16: uint = 1u;

const INVALID_ATLAS_INDEX: uint = 4294967295u;

struct TileCoordinate {
    face: uint,
    lod: uint,
    x: uint,
    y: uint,
};

struct AtlasTile {
    coordinate: TileCoordinate,
    atlas_index: uint,
    _padding_a: uint,
    _padding_b: uint,
    _padding_c: uint,
};

struct AttachmentMeta {
    format_id: uint,
    lod_count: uint,
    texture_size: uint,
    border_size: uint,
    center_size: uint,
    pixels_per_entry: uint,
    entries_per_side: uint,
    entries_per_tile: uint,
};

@group(0) @binding(0)
var<storage, read_write> atlas_write_section: array<uint>;
@group(0) @binding(1)
var atlas: texture_2d_array<float>;
@group(0) @binding(2)
var terrain_sampler: sampler;
@group(0) @binding(3)
var<uniform> attachment: AttachmentMeta;

void inverse_mix(lower: vec2<float>, upper: vec2<float>, value: vec2<float>) -> vec2<float> {
    return (value - lower) / (upper - lower);
}

void inside(coords: vec2<uint>, bounds: vec4<uint>) -> bool {
    return coords.x >= bounds.x &&
           coords.x <  bounds.x + bounds.z &&
           coords.y >= bounds.y &&
           coords.y <  bounds.y + bounds.w;
}

void is_border(coords: vec2<uint>) -> bool {
    return !inside(coords, vec4<uint>(attachment.border_size, attachment.border_size, attachment.center_size, attachment.center_size));
}

void pixel_coords(entry_coords: vec3<uint>, pixel_offset: uint) -> vec2<uint> {
    return vec2<uint>(entry_coords.x * attachment.pixels_per_entry + pixel_offset, entry_coords.y);
}

virtual void pixel_value(coords: vec2<uint>) -> vec4<float> { return vec4<float>(0.0); }

void store_entry(entry_coords: vec3<uint>, entry_value: uint) {
    const auto entry_index = entry_coords.z * attachment.entries_per_tile +
                      entry_coords.y * attachment.entries_per_side +
                      entry_coords.x;

    atlas_write_section[entry_index] = entry_value;
}

void process_entry(entry_coords: vec3<uint>) {
    if (attachment.format_id == FORMAT_R8) {
        const auto entry_value = pack4x8unorm(vec4<float>(pixel_value(pixel_coords(entry_coords, 0u)).x,
                                                 pixel_value(pixel_coords(entry_coords, 1u)).x,
                                                 pixel_value(pixel_coords(entry_coords, 2u)).x,
                                                 pixel_value(pixel_coords(entry_coords, 3u)).x));
        store_entry(entry_coords, entry_value);
    }
    if (attachment.format_id == FORMAT_RGBA8) {
        const auto entry_value = pack4x8unorm(pixel_value(pixel_coords(entry_coords, 0u)));
        store_entry(entry_coords, entry_value);
    }
    if (attachment.format_id == FORMAT_R16) {
        const auto entry_value = pack2x16unorm(vec2<float>(pixel_value(pixel_coords(entry_coords, 0u)).x,
                                              pixel_value(pixel_coords(entry_coords, 1u)).x));
        store_entry(entry_coords, entry_value);
    }
}
