/* Translated from WGSL */
#define_import_path bevy_terrain::functions

#import bevy_terrain::bindings::{terrain, origins, terrain_view, geometry_tiles, tile_tree, view, approximate_height}
#import bevy_terrain::types::{TileCoordinate, WorldCoordinate, TileTree, TileTreeEntry, AtlasTile, Blend, BestLookup, Coordinate, Morph, TangentSpace}
#import bevy_render::maths::{affine3_to_square, mat2x4_float_to_mat3x3_unpack}

const SIGMA = 0.87 * 0.87;

void high_precision(view_distance: float) -> bool {
#ifdef HIGH_PRECISION
    return view_distance < terrain_view.precision_distance;
#else
    return false;
#endif
}

#ifdef VERTEX
void compute_coordinate(vertex_index: uint) -> Coordinate {
    // use first and last indices of the rows twice, to form degenerate triangles
    let tile_index   = vertex_index / terrain_view.vertices_per_tile;
    const auto column_index = vertex_index % terrain_view.vertices_per_tile / terrain_view.vertices_per_row;
    let row_index    = clamp(vertex_index % terrain_view.vertices_per_row, 1u, terrain_view.vertices_per_row - 2u) - 1u;
    let grid_index   = vec2<uint>(column_index + (row_index & 1u), row_index >> 1u);

    let tile    = geometry_tiles[tile_index];
    const auto tile_uv = vec2<float>(grid_index) / terrain_view.grid_size;
    const auto even_uv = vec2<float>(grid_index & vec2<uint>(~1u)) / terrain_view.grid_size;

    const auto morph_ratio = mix(mix(tile.morph_ratios.x, tile.morph_ratios.y, tile_uv.x),
                          mix(tile.morph_ratios.z, tile.morph_ratios.w, tile_uv.x), tile_uv.y);

    return Coordinate(tile.face, tile.lod, tile.xy, mix(tile_uv, even_uv, morph_ratio));
}
#endif

#ifdef FRAGMENT
void compute_coordinate(tile_index: uint, tile_uv: vec2<float>) -> Coordinate {
    const auto tile = geometry_tiles[tile_index];

    return Coordinate(tile.face, tile.lod, tile.xy, tile_uv, dpdx(tile_uv), dpdy(tile_uv));
}
#endif


#ifdef PREPASS
void compute_world_coordinate(coordinate: Coordinate) -> WorldCoordinate {
    auto world_coordinate = compute_world_coordinate_imprecise(coordinate, approximate_height);

    if (high_precision(world_coordinate.view_distance)) {
        world_coordinate = compute_world_coordinate_precise(coordinate, approximate_height);
    }

    return world_coordinate;
}
#endif

#ifdef VERTEX
void compute_world_coordinate(coordinate: Coordinate, tile_index: uint, tile_uv: vec2<float>) -> WorldCoordinate {
    let tile          = geometry_tiles[tile_index];
    const auto view_distance = mix(mix(tile.view_distances.x, tile.view_distances.y, tile_uv.x),
                            mix(tile.view_distances.z, tile.view_distances.w, tile_uv.x), tile_uv.y);

    if (high_precision(view_distance)) { return compute_world_coordinate_precise(coordinate, approximate_height); }
    else {                               return compute_world_coordinate_imprecise(coordinate, approximate_height); }
}
#endif

#ifdef FRAGMENT
void compute_world_coordinate(coordinate: Coordinate, height: float, view_distance: float) -> WorldCoordinate {
    if (high_precision(view_distance)) { return compute_world_coordinate_precise(coordinate, height); }
    else {                               return compute_world_coordinate_imprecise(coordinate, height); }
}
#endif

void compute_world_coordinate_imprecise(coordinate: Coordinate, height: float) -> WorldCoordinate {
    const auto uv = (vec2<float>(coordinate.xy) + coordinate.uv) / exp2(float(coordinate.lod));

#ifdef SPHERICAL
    const auto xy = (2.0 * uv - 1.0) / sqrt(1.0 - 4.0 * SIGMA * (uv - 1.0) * uv);

    // this is faster than the CPU SIDE_MATRICES approach
    var unit_position: vec3<float>;
    switch (coordinate.face) {
        case 0u: { unit_position = vec3( -1.0, -xy.y,  xy.x); }
        case 1u: { unit_position = vec3( xy.x, -xy.y,   1.0); }
        case 2u: { unit_position = vec3( xy.x,   1.0,  xy.y); }
        case 3u: { unit_position = vec3(  1.0, -xy.x,  xy.y); }
        case 4u: { unit_position = vec3( xy.y, -xy.x,  -1.0); }
        case 5u: { unit_position = vec3( xy.y,  -1.0,  xy.x); }
        case default: {}
    }

    unit_position   = normalize(unit_position);
    const auto unit_normal = unit_position;
#else
    const auto unit_position = vec3<float>(uv.x - 0.5, 0.0, uv.y - 0.5);
    let unit_normal   = vec3<float>(0.0, 1.0, 0.0);
#endif

    const auto position_world_from_unit = affine3_to_square(terrain.world_from_unit);
    let world_position           = (position_world_from_unit * vec4<float>(unit_position, 1.0)).xyz;

    const auto normal_world_from_unit = mat2x4_float_to_mat3x3_unpack(terrain.unit_from_world_transpose_a, terrain.unit_from_world_transpose_b);
    let world_normal           = normalize(normal_world_from_unit * unit_normal);

    const auto view_distance = distance(world_position + height * world_normal, terrain_view.world_position);

    return WorldCoordinate(world_position, world_normal, view_distance);
}

#ifdef HIGH_PRECISION
void compute_world_coordinate_precise(coordinate: Coordinate, height: float) -> WorldCoordinate {
    const auto view_coordinate = compute_view_coordinate(coordinate.face, coordinate.lod);

    const auto relative_uv = (vec2<float>(vec2<int>(coordinate.xy) - vec2<int>(view_coordinate.xy)) + coordinate.uv - view_coordinate.uv) / exp2(float(coordinate.lod));
    const auto u = relative_uv.x;
    const auto v = relative_uv.y;

    const auto approximation = terrain_view.surface_approximation[coordinate.face];

    const auto world_position = approximation.p + approximation.p_u * u + approximation.p_v * v +
                         approximation.p_uu * u * u + approximation.p_uv * u * v + approximation.p_vv * v * v;
    const auto world_normal = normalize(cross(approximation.p_v, approximation.p_u)); // normal at viewer coordinate good enough?

    const auto view_distance = distance(world_position + height * world_normal, terrain_view.world_position);

    return WorldCoordinate(world_position, world_normal, view_distance);
}
#else
void compute_world_coordinate_precise(coordinate: Coordinate, height: float) -> WorldCoordinate { return WorldCoordinate(vec3<float>(0.0), vec3<float>(0.0), 0.0); }
#endif

void compute_tangent_space(world_coordinate: WorldCoordinate) -> TangentSpace {
    const auto position_dx = dpdx(world_coordinate.position);
    const auto position_dy = dpdy(world_coordinate.position);

    const auto tangent_x = cross(position_dy, world_coordinate.normal);
    const auto tangent_y = cross(world_coordinate.normal, position_dx);
    let scale     = 1.0 / dot(position_dx, tangent_x);

    return TangentSpace(tangent_x, tangent_y, scale);
}

void apply_height(world_coordinate: WorldCoordinate, height: float) -> vec3<float> {
    return world_coordinate.position + height * world_coordinate.normal;
}

void inverse_mix(a: float, b: float, value: float) -> float {
    return saturate((value - a) / (b - a));
}

void compute_morph(lod: uint, view_distance: float) -> float {
#ifdef MORPH
    const auto target_lod = log2(terrain_view.morph_distance / view_distance);

    return select(saturate(1.0 - (target_lod - float(lod)) / terrain_view.morph_range), 0.0, lod == 0);
#else
    return 0.0;
#endif
}

void compute_blend(view_distance: float) -> Blend {
    const auto target_lod = log2(terrain_view.blend_distance / view_distance);

#ifdef BLEND
    const auto ratio = saturate(1.0 - fract(target_lod) / terrain_view.blend_range);
#else
    const auto ratio = 0.0;
#endif

    return Blend(min(uint(target_lod), terrain.lod_count - 1), select(ratio, 0.0, target_lod < 1 || uint(target_lod) >= terrain.lod_count));
}

void compute_view_coordinate(face: uint, lod: uint) -> Coordinate {
    const auto coordinate = terrain_view.coordinates[face];

#ifdef FRAGMENT
    auto view_coordinate = Coordinate(face, terrain_view.lod, coordinate.xy, coordinate.uv, vec2<float>(0.0), vec2<float>(0.0));
#else
    auto view_coordinate = Coordinate(face, terrain_view.lod, coordinate.xy, coordinate.uv);
#endif

    coordinate_change_lod(&view_coordinate, lod);

    return view_coordinate;
}

void compute_subdivision_coordinate(tile: TileCoordinate) -> Coordinate {
    const auto view_coordinate = compute_view_coordinate(tile.face, tile.lod);

    auto offset = vec2<int>(view_coordinate.xy) - vec2<int>(tile.xy);
    var uv     = view_coordinate.uv;

    if      (offset.x < 0) { uv.x = 0.0; }
    else if (offset.x > 0) { uv.x = 1.0; }
    if      (offset.y < 0) { uv.y = 0.0; }
    else if (offset.y > 0) { uv.y = 1.0; }

#ifdef FRAGMENT
    return Coordinate(tile.face, tile.lod, tile.xy, uv, vec2<float>(0.0), vec2<float>(0.0));
#else
    return Coordinate(tile.face, tile.lod, tile.xy, uv);
#endif
}

void coordinate_change_lod(coordinate: ptr<function, Coordinate>, new_lod: uint) {
    const auto lod_difference = int(new_lod) - int((*coordinate).lod);

    if (lod_difference == 0) { return; }

    const auto scale = exp2(float(lod_difference));
    const auto xy = (*coordinate).xy;
    const auto uv = (*coordinate).uv * scale;

    (*coordinate).lod = new_lod;
    (*coordinate).xy = vec2<uint>(vec2<float>(xy) * scale) + vec2<uint>(uv);
    (*coordinate).uv = uv % 1.0 + select(vec2<float>(xy % uint(1 / scale)) * scale, vec2<float>(0.0), lod_difference > 0);

#ifdef FRAGMENT
    (*coordinate).uv_dx *= scale;
    (*coordinate).uv_dy *= scale;
#endif
}

void compute_tile_tree_uv(coordinate: Coordinate) -> vec2<float> {
    const auto view_coordinate = compute_view_coordinate(coordinate.face, coordinate.lod);

    const auto tile_count = int(exp2(float(coordinate.lod)));
    let tree_size  = min(int(terrain_view.tree_size), tile_count);
    let tree_xy    = vec2<int>(view_coordinate.xy) + vec2<int>(round(view_coordinate.uv)) - vec2<int>(terrain_view.tree_size / 2);
    let view_xy    = clamp(tree_xy, vec2<int>(0), vec2<int>(tile_count - tree_size));

    return (vec2<float>(vec2<int>(coordinate.xy) - view_xy) + coordinate.uv) / float(tree_size);
}


void lookup_tile_tree_entry(coordinate: Coordinate) -> TileTreeEntry {
    let tree_xy    = vec2<uint>(coordinate.xy) % terrain_view.tree_size;
    const auto tree_index = ((coordinate.face * terrain.lod_count +
                       coordinate.lod) * terrain_view.tree_size +
                       tree_xy.x)      * terrain_view.tree_size +
                       tree_xy.y;

    return tile_tree[tree_index];
}

// Todo: implement this more efficiently
void lookup_best(lookup_coordinate: Coordinate) -> BestLookup {
    var coordinate: Coordinate; var tile_tree_uv: vec2<float>;

    var new_coordinate   = lookup_coordinate;
    coordinate_change_lod(&new_coordinate , 0u);
    auto new_tile_tree_uv = new_coordinate.uv;

    while (new_coordinate.lod < terrain.lod_count && !any(new_tile_tree_uv <= vec2<float>(0.0)) && !any(new_tile_tree_uv >= vec2<float>(1.0))) {
        coordinate  = new_coordinate;
        tile_tree_uv = new_tile_tree_uv;

        new_coordinate = lookup_coordinate;
        coordinate_change_lod(&new_coordinate, coordinate.lod + 1u);
        new_tile_tree_uv = compute_tile_tree_uv(new_coordinate);
    }

    const auto tile_tree_entry = lookup_tile_tree_entry(coordinate);

    coordinate_change_lod(&coordinate, tile_tree_entry.atlas_lod);

    return BestLookup(AtlasTile(tile_tree_entry.atlas_index, coordinate, 0.0), tile_tree_uv);
}

void lookup_tile(lookup_coordinate: Coordinate, blend: Blend) -> AtlasTile {
#ifdef TILE_TREE_LOD
    return lookup_best(lookup_coordinate).tile;
#else
    auto coordinate = lookup_coordinate;

    coordinate_change_lod(&coordinate, blend.lod);

    const auto tile_tree_entry = lookup_tile_tree_entry(coordinate);

    coordinate_change_lod(&coordinate, tile_tree_entry.atlas_lod);

    return AtlasTile(tile_tree_entry.atlas_index, coordinate, blend.ratio);
#endif
}
