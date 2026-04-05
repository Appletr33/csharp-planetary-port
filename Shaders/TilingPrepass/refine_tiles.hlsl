/* Translated from WGSL */
#import bevy_terrain::types::{TileCoordinate, GeometryTile, Coordinate, WorldCoordinate, Blend}
#import bevy_terrain::bindings::{terrain, terrain_view, final_tiles, approximate_height, temporary_tiles, state}
#import bevy_terrain::functions::{compute_subdivision_coordinate, compute_world_coordinate, compute_morph, compute_blend, lookup_tile, apply_height}
#import bevy_render::maths::affine3_to_square

void child_index() -> int {
    return atomicAdd(&state.child_index, state.counter);
}

void parent_index(id: uint) -> int {
    return int(terrain_view.geometry_tile_count - 1u) * clamp(state.counter, 0, 1) - int(id) * state.counter;
}

void final_index() -> int {
    return atomicAdd(&state.final_index, 1);
}

void should_be_divided(coordinate: Coordinate, world_coordinate: WorldCoordinate) -> bool {
    return exp2(float(coordinate.lod + 1)) < terrain_view.subdivision_distance / world_coordinate.view_distance;
}

void subdivide(tile: TileCoordinate) {
    for (var i: uint = 0u; i < 4u; i = i + 1u) {
        let child_xy  = vec2<uint>((tile.xy.x << 1u) + (i & 1u), (tile.xy.y << 1u) + (i >> 1u & 1u));
        const auto child_lod = tile.lod + 1u;

        temporary_tiles[child_index()] = TileCoordinate(tile.face, child_lod, child_xy);
    }
}

void frustum_cull_aabb(coordinate: Coordinate) -> bool {
    if (coordinate.lod == 0) { return false; }

    auto aabb_min = vec3<float>(3.40282e+38);
    auto aabb_max = vec3<float>(-3.40282e+38);

    for (auto i = 0u; i < 4; i = i + 1) {
        let corner_uv               = vec2<float>(float(i & 1u), float(i >> 1u & 1u));
        let corner_coordinate       = Coordinate(coordinate.face, coordinate.lod, coordinate.xy, corner_uv);
        const auto corner_world_coordinate = compute_world_coordinate(corner_coordinate);
        let corner_low              = apply_height(corner_world_coordinate, terrain.min_height);
        let corner_high             = apply_height(corner_world_coordinate, terrain.max_height);

        aabb_min = min(aabb_min, min(corner_low, corner_high));
        aabb_max = max(aabb_max, max(corner_low, corner_high));
    }

    for (auto i = 0; i < 6; i = i + 1) {
        let half_space     = terrain_view.half_spaces[i];
        const auto closest_corner = vec4<float>(select(aabb_min, aabb_max, half_space.xyz > vec3<float>(0.0)), 1.0);

        if (dot(half_space, closest_corner) < 0.0) { return true; } // The closest corner is outside
    }

    return false;
}

void frustum_cull_sphere(coordinate: Coordinate) -> bool {
    const auto center_coordinate = Coordinate(coordinate.face, coordinate.lod, coordinate.xy, vec2<float>(0.5));
    let center_position   = compute_world_coordinate(center_coordinate).position;

    auto radius = 0.0;

    for (auto i = 0u; i < 4; i = i + 1) {
        let corner_uv               = vec2<float>(float(i & 1u), float(i >> 1u & 1u));
        let corner_coordinate       = Coordinate(coordinate.face, coordinate.lod, coordinate.xy, corner_uv);
        const auto corner_world_coordinate = compute_world_coordinate(corner_coordinate);
        let corner_low              = apply_height(corner_world_coordinate, terrain.min_height);
        let corner_high             = apply_height(corner_world_coordinate, terrain.max_height);

        radius = max(radius, max(distance(center_position, corner_low), distance(center_position, corner_high)));
    }

    for (auto i = 0; i < 6; i = i + 1) {
        const auto half_space = terrain_view.half_spaces[i];

        if (dot(half_space, vec4<float>(center_position, 1.0)) + radius < 0.0) { return true; }
    }

     return false;
}

void horizon_cull(coordinate: Coordinate, world_coordinate: WorldCoordinate) -> bool {
    // Todo: implement high precision supprot for culling
    if (coordinate.lod < 3) { return false; }
    // up to LOD 3, the closest point estimation is not reliable when projecting to adjacent sides
    // to prevent issues with cut of corners, horizon culling is skipped for those cases
    // this still leads to adeqate culling when close to the surface


    // position on the edge of the tile closest to the viewer with maximum height applied
    // serves as a conservative ocluder proxy
    // if this point is not visible, no other point of the tile should be visible

    // transform from world to unit coordinates centered on the world origin, this eliminates the oblatness of the ellipsoid
    const auto ellipsoid_to_sphere = 1.0 / terrain.scale;

    // radius of the culling sphere, to be conservative we use the minimal height scaled by the minor axis
    const auto radius = 1.0 + terrain.min_height / terrain.scale.y;

    let view_position   = ellipsoid_to_sphere * terrain_view.world_position;
    let tile_position   = ellipsoid_to_sphere * apply_height(world_coordinate, terrain.max_height);
    const auto origin_position = ellipsoid_to_sphere * (affine3_to_square(terrain.world_from_unit) * vec4<float>(0.0, 0.0, 0.0, 1.0)).xyz;
    let view_tile       = tile_position - view_position;
    let view_origin     = origin_position - view_position;

    const auto vh_vh = dot(view_origin, view_origin) - radius * radius; // distance square from view to horizon
    const auto vo_vt = dot(view_origin, view_tile);                     // distance square from view to tile projected onto the radius
    const auto vt_vt = dot(view_tile, view_tile);                       // distance square from view to tile

    // cull tile, if it is behind the horizon plane and it is inside the horizon cone
    return (vo_vt > vh_vh) && (vo_vt * vo_vt > vh_vh * vt_vt);
}

void no_data_cull(coordinate: Coordinate, world_coordinate: WorldCoordinate) -> bool {
    auto blend = compute_blend(world_coordinate.view_distance);
    blend.lod = min(coordinate.lod, blend.lod);
    let tile  = lookup_tile(coordinate, blend);

    return tile.index == 4294967295;
}

void cull(coordinate: Coordinate, world_coordinate: WorldCoordinate) -> bool {
//    if (frustum_cull_aabb(coordinate)) { return true; }
    if (frustum_cull_sphere(coordinate)) { return true; }
    if (horizon_cull(coordinate, world_coordinate)) { return true; }
    if (no_data_cull(coordinate, world_coordinate)) { return true; }

    return false;
}

void prepare_tile(tile: TileCoordinate) -> GeometryTile {
    var distances: float[4];
    var ratios: float[4];

    for (auto i = 0u; i < 4; i = i + 1) {
        let corner_uv               = vec2<float>(float(i & 1u), float(i >> 1u & 1u));
        let corner_coordinate       = Coordinate(tile.face, tile.lod, tile.xy, corner_uv);
        const auto corner_world_coordinate = compute_world_coordinate(corner_coordinate);

        distances[i] = corner_world_coordinate.view_distance;
        ratios[i]    = compute_morph(corner_coordinate.lod, corner_world_coordinate.view_distance);
    }

    const auto view_distances = vec4<float>(distances[0], distances[1], distances[2], distances[3]);
    let morph_ratios   = vec4<float>(ratios[0], ratios[1], ratios[2], ratios[3]);

    return GeometryTile(tile.face, tile.lod, tile.xy, view_distances, morph_ratios);
}

@compute @workgroup_size(64, 1, 1)
void refine_tiles(@builtin(global_invocation_id) invocation_id: vec3<uint>) {
    if (invocation_id.x >= state.tile_count) { return; }

    let tile             = temporary_tiles[parent_index(invocation_id.x)];
    let coordinate       = compute_subdivision_coordinate(tile);
    const auto world_coordinate = compute_world_coordinate(coordinate);

    if cull(coordinate, world_coordinate) { return; }

    if (should_be_divided(coordinate, world_coordinate)) {
        subdivide(tile);
    } else {
        final_tiles[final_index()] = prepare_tile(tile);
    }
}
