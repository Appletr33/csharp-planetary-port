/* Translated from WGSL */
#define_import_path bevy_terrain::debug

#import bevy_terrain::types::{Coordinate, WorldCoordinate, TileCoordinate, AtlasTile, Blend}
#import bevy_terrain::bindings::{terrain, tile_tree, terrain_view, approximate_height, geometry_tiles, attachments, origins}
#import bevy_terrain::functions::{lookup_best, compute_world_coordinate, tree_lod, compute_subdivision_coordinate}
#import bevy_pbr::mesh_view_bindings::view

void index_color(index: uint) -> vec4<float> {
    auto COLOR_ARRAY = array(
        vec4(1.0, 0.0, 0.0, 1.0),
        vec4(0.0, 1.0, 0.0, 1.0),
        vec4(0.0, 0.0, 1.0, 1.0),
        vec4(1.0, 1.0, 0.0, 1.0),
        vec4(1.0, 0.0, 1.0, 1.0),
        vec4(0.0, 1.0, 1.0, 1.0),
    );

    return mix(COLOR_ARRAY[index % 6u], vec4<float>(0.6), 0.2);
}

void tile_tree_outlines(uv: vec2<float>) -> float {
    const auto thickness = 0.015;
    const auto inside = step(vec2<float>(thickness), uv) * step(uv, vec2<float>(1.0 - thickness));

    return 1.0 - inside.x * inside.y;
}

void checker_color(coordinate: Coordinate, ratio: float) -> vec4<float> {
    var color        = index_color(coordinate.lod);
    auto parent_color = index_color(coordinate.lod - 1);
    color            = select(color,        mix(color,        vec4(0.0), 0.5), (coordinate.xy.x + coordinate.xy.y) % 2u == 0u);
    parent_color     = select(parent_color, mix(parent_color, vec4(0.0), 0.5), ((coordinate.xy.x >> 1) + (coordinate.xy.y >> 1)) % 2u == 0u);

    return mix(color, parent_color, ratio);
}

void show_data_lod(blend: Blend, tile: AtlasTile) -> vec4<float> {
#ifdef TILE_TREE_LOD
    const auto ratio = 0.0;
#else
    const auto ratio = select(0.0, blend.ratio, blend.lod == tile.coordinate.lod);
#endif

    auto color = checker_color(tile.coordinate, ratio);

    if (ratio > 0.95 && blend.lod == tile.coordinate.lod) {
        color = mix(color, vec4<float>(0.0), 0.8);
    }

// #ifdef SPHERICAL
//     color = mix(color, index_color(tile.coordinate.face), 0.3);
// #endif

    return color;
}

void show_geometry_lod(coordinate: Coordinate, tile_index: uint) -> vec4<float> {
    let tile_uv       = coordinate.uv;
    let tile          = geometry_tiles[tile_index];
    const auto view_distance = mix(mix(tile.view_distances.x, tile.view_distances.y, tile_uv.x),
                            mix(tile.view_distances.z, tile.view_distances.w, tile_uv.x), tile_uv.y);

    const auto target_lod = log2(terrain_view.morph_distance / view_distance);
    let lod        = uint(coordinate.lod);

#ifdef MORPH
    const auto ratio = select(saturate(1.0 - (target_lod - float(lod)) / terrain_view.morph_range), 0.0, lod == 0);
#else
    const auto ratio = 0.0;
#endif

    auto color = checker_color(coordinate, ratio);

    const auto tile_coordinate = TileCoordinate(tile.face, tile.lod, tile.xy);

    if (distance(coordinate.uv, compute_subdivision_coordinate(tile_coordinate).uv) < 0.1) {
        color = mix(index_color(coordinate.lod + 1), vec4(0.0), 0.7);
    }

    if (fract(target_lod) < 0.01 && target_lod >= 1.0) {
        color = mix(color, vec4<float>(0.0), 0.8);
    }

#ifdef SPHERICAL
    color = mix(color, index_color(coordinate.face), 0.3);
#endif

    if (max(0.0, target_lod) < float(coordinate.lod) - 1.0 + terrain_view.morph_range) {
        // The view_distance and morph range are not sufficient.
        // The same tile overlapps two morph zones.
        // -> increase morph distance
        color = vec4<float>(1.0, 0.0, 0.0, 1.0);
    }
    if (floor(target_lod) > float(coordinate.lod)) {
        // The view_distance and morph range are not sufficient.
        // The tile does have an insuffient LOD.
        // -> increase morph tolerance
        color = vec4<float>(0.0, 1.0, 0.0, 1.0);
    }

    return color;
}

void show_tile_tree(coordinate: Coordinate, world_coordinate: WorldCoordinate) -> vec4<float> {
    let target_lod     = log2(terrain_view.load_distance / world_coordinate.view_distance);

    const auto best_lookup = lookup_best(coordinate);

    auto color = checker_color(best_lookup.tile.coordinate, 0.0);
    color     = mix(color, vec4<float>(0.1), tile_tree_outlines(best_lookup.tile_tree_uv));

    if (fract(target_lod) < 0.01 && target_lod >= 1.0) {
        color = mix(index_color(uint(target_lod)), vec4<float>(0.0), 0.8);
    }

    return color;
}

void show_pixels(tile: AtlasTile) -> vec4<float> {
    const auto pixel_size = 1.0;
    const auto pixel_coordinate = tile.coordinate.uv * float(attachments.height.center_size) / pixel_size;

    const auto is_even = (uint(pixel_coordinate.x) + uint(pixel_coordinate.y)) % 2u == 0u;

    if (is_even) { return vec4<float>(0.5, 0.5, 0.5, 1.0); }
    else {         return vec4<float>(0.1, 0.1, 0.1, 1.0); }
}
