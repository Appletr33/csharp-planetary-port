#ifndef FUNCTIONS_HLSL
#define FUNCTIONS_HLSL

#include "types.hlsl"
#include "bindings.hlsl"

#define SIGMA (0.87 * 0.87)

bool high_precision(float view_distance) {
#ifdef HIGH_PRECISION
    return view_distance < terrain_view.precision_distance;
#else
    return false;
#endif
}

void coordinate_change_lod(inout Coordinate coordinate, uint new_lod) {
    int lod_difference = int(new_lod) - int(coordinate.lod);

    if (lod_difference == 0) { return; }

    float scale = exp2(float(lod_difference));
    uint2 xy = coordinate.xy;
    float2 uv = coordinate.uv * scale;

    coordinate.lod = new_lod;
    coordinate.xy = uint2(float2(xy) * scale) + uint2(uv.x, uv.y);
    coordinate.uv = frac(uv) + (lod_difference > 0 ? (float2(xy % uint2(uint(1.0f / scale), uint(1.0f / scale))) * scale) : float2(0.0f, 0.0f));

#ifdef FRAGMENT
    coordinate.uv_dx *= scale;
    coordinate.uv_dy *= scale;
#endif
}

Coordinate compute_view_coordinate(uint face, uint lod) {
    ViewCoordinate view_coord_data = terrain_view.coordinates[face];

#ifdef FRAGMENT
    Coordinate view_coordinate = {face, terrain_view.lod, view_coord_data.xy, view_coord_data.uv, float2(0.0f, 0.0f), float2(0.0f, 0.0f)};
#else
    Coordinate view_coordinate = {face, terrain_view.lod, view_coord_data.xy, view_coord_data.uv};
#endif

    coordinate_change_lod(view_coordinate, lod);

    return view_coordinate;
}

#ifdef VERTEX
Coordinate compute_coordinate(uint vertex_index) {
    // use first and last indices of the rows twice, to form degenerate triangles
    uint tile_index   = vertex_index / terrain_view.vertices_per_tile;
    uint column_index = vertex_index % terrain_view.vertices_per_tile / terrain_view.vertices_per_row;
    uint row_index    = clamp(vertex_index % terrain_view.vertices_per_row, 1u, terrain_view.vertices_per_row - 2u) - 1u;
    uint2 grid_index   = uint2(column_index + (row_index & 1u), row_index >> 1u);

    GeometryTile tile    = geometry_tiles[tile_index];
    float2 tile_uv = float2(grid_index) / terrain_view.grid_size;
    float2 even_uv = float2(grid_index & uint2(~1u, ~1u)) / terrain_view.grid_size;

    float morph_ratio = lerp(lerp(tile.morph_ratios.x, tile.morph_ratios.y, tile_uv.x),
                          lerp(tile.morph_ratios.z, tile.morph_ratios.w, tile_uv.x), tile_uv.y);

    Coordinate result = {tile.face, tile.lod, tile.xy, lerp(tile_uv, even_uv, morph_ratio)};
    return result;
}
#endif

#ifdef FRAGMENT
Coordinate compute_coordinate(uint tile_index, float2 tile_uv) {
    GeometryTile tile = geometry_tiles[tile_index];
    Coordinate result = {tile.face, tile.lod, tile.xy, tile_uv, ddx(tile_uv), ddy(tile_uv)};
    return result;
}
#endif

WorldCoordinate compute_world_coordinate_imprecise(Coordinate coordinate, float height) {
    float2 uv = (float2(coordinate.xy) + coordinate.uv) / exp2(float(coordinate.lod));

#ifdef SPHERICAL
    float2 xy = (2.0f * uv - 1.0f) / sqrt(1.0f - 4.0f * SIGMA * (uv - 1.0f) * uv);

    float3 unit_position;
    switch (coordinate.face) {
        case 0u: unit_position = float3( -1.0, -xy.y,  xy.x); break;
        case 1u: unit_position = float3( xy.x, -xy.y,   1.0); break;
        case 2u: unit_position = float3( xy.x,   1.0,  xy.y); break;
        case 3u: unit_position = float3(  1.0, -xy.x,  xy.y); break;
        case 4u: unit_position = float3( xy.y, -xy.x,  -1.0); break;
        case 5u: unit_position = float3( xy.y,  -1.0,  xy.x); break;
        default: unit_position = float3(0.0f, 0.0f, 0.0f); break;
    }

    unit_position   = normalize(unit_position);
    float3 unit_normal = unit_position;
#else
    float3 unit_position = float3(uv.x - 0.5f, 0.0f, uv.y - 0.5f);
    float3 unit_normal   = float3(0.0f, 1.0f, 0.0f);
#endif

    float4x4 position_world_from_unit = {
        terrain.world_from_unit[0].x, terrain.world_from_unit[1].x, terrain.world_from_unit[2].x, terrain.world_from_unit[3].x,
        terrain.world_from_unit[0].y, terrain.world_from_unit[1].y, terrain.world_from_unit[2].y, terrain.world_from_unit[3].y,
        terrain.world_from_unit[0].z, terrain.world_from_unit[1].z, terrain.world_from_unit[2].z, terrain.world_from_unit[3].z,
        0.0f, 0.0f, 0.0f, 1.0f
    };
    float3 world_position = mul(position_world_from_unit, float4(unit_position, 1.0f)).xyz;

    float3x3 normal_world_from_unit = {
        terrain.unit_from_world_transpose_a[0].x, terrain.unit_from_world_transpose_a[0].y, terrain.unit_from_world_transpose_a[1].x,
        terrain.unit_from_world_transpose_a[1].y, terrain.unit_from_world_transpose_a[2].x, terrain.unit_from_world_transpose_a[2].y,
        terrain.unit_from_world_transpose_a[3].x, terrain.unit_from_world_transpose_a[3].y, terrain.unit_from_world_transpose_b
    };
    float3 world_normal = normalize(mul(normal_world_from_unit, unit_normal));

    float view_distance = distance(world_position + height * world_normal, terrain_view.world_position);

    WorldCoordinate wc = {world_position, world_normal, view_distance};
    return wc;
}

#ifdef HIGH_PRECISION
WorldCoordinate compute_world_coordinate_precise(Coordinate coordinate, float height) {
    Coordinate view_coordinate = compute_view_coordinate(coordinate.face, coordinate.lod);

    float2 relative_uv = (float2(int2(coordinate.xy) - int2(view_coordinate.xy)) + coordinate.uv - view_coordinate.uv) / exp2(float(coordinate.lod));
    float u = relative_uv.x;
    float v = relative_uv.y;

    SurfaceApproximation approximation = terrain_view.surface_approximation[coordinate.face];

    float3 world_position = approximation.p + approximation.p_u * u + approximation.p_v * v +
                         approximation.p_uu * u * u + approximation.p_uv * u * v + approximation.p_vv * v * v;
    float3 world_normal = normalize(cross(approximation.p_v, approximation.p_u));

    float view_distance = distance(world_position + height * world_normal, terrain_view.world_position);

    WorldCoordinate wc = {world_position, world_normal, view_distance};
    return wc;
}
#else
WorldCoordinate compute_world_coordinate_precise(Coordinate coordinate, float height) {
    WorldCoordinate wc = {float3(0.0f, 0.0f, 0.0f), float3(0.0f, 0.0f, 0.0f), 0.0f};
    return wc;
}
#endif

#ifdef PREPASS
WorldCoordinate compute_world_coordinate(Coordinate coordinate) {
    float height = 0.0f; // Mock height for prepass as it uses approximate height bounds
    WorldCoordinate world_coordinate = compute_world_coordinate_imprecise(coordinate, height);

    if (high_precision(world_coordinate.view_distance)) {
        world_coordinate = compute_world_coordinate_precise(coordinate, height);
    }

    return world_coordinate;
}
#endif

#ifdef VERTEX
WorldCoordinate compute_world_coordinate(Coordinate coordinate, uint tile_index, float2 tile_uv) {
    GeometryTile tile = geometry_tiles[tile_index];
    float view_distance = lerp(lerp(tile.view_distances.x, tile.view_distances.y, tile_uv.x),
                            lerp(tile.view_distances.z, tile.view_distances.w, tile_uv.x), tile_uv.y);

    if (high_precision(view_distance)) { return compute_world_coordinate_precise(coordinate, 0.0f); }
    else {                               return compute_world_coordinate_imprecise(coordinate, 0.0f); }
}
#endif

#ifdef FRAGMENT
WorldCoordinate compute_world_coordinate(Coordinate coordinate, float height, float view_distance) {
    if (high_precision(view_distance)) { return compute_world_coordinate_precise(coordinate, height); }
    else {                               return compute_world_coordinate_imprecise(coordinate, height); }
}
#endif

TangentSpace compute_tangent_space(WorldCoordinate world_coordinate) {
    float3 position_dx = ddx(world_coordinate.position);
    float3 position_dy = ddy(world_coordinate.position);

    float3 tangent_x = cross(position_dy, world_coordinate.normal);
    float3 tangent_y = cross(world_coordinate.normal, position_dx);
    float scale = 1.0f / dot(position_dx, tangent_x);

    TangentSpace ts = {tangent_x, tangent_y, scale};
    return ts;
}

float3 apply_height(WorldCoordinate world_coordinate, float height) {
    return world_coordinate.position + height * world_coordinate.normal;
}

float inverse_mix(float a, float b, float value) {
    return saturate((value - a) / (b - a));
}

float compute_morph(uint lod, float view_distance) {
#ifdef MORPH
    float target_lod = log2(terrain_view.morph_distance / view_distance);

    return (lod == 0) ? 0.0f : saturate(1.0f - (target_lod - float(lod)) / terrain_view.morph_range);
#else
    return 0.0f;
#endif
}

Blend compute_blend(float view_distance) {
    float target_lod = log2(terrain_view.blend_distance / view_distance);

#ifdef BLEND
    float ratio = saturate(1.0f - frac(target_lod) / terrain_view.blend_range);
#else
    float ratio = 0.0f;
#endif

    Blend b = {min((uint)target_lod, terrain.lod_count - 1), (target_lod < 1.0f || (uint)target_lod >= terrain.lod_count) ? 0.0f : ratio};
    return b;
}


Coordinate compute_subdivision_coordinate(TileCoordinate tile) {
    Coordinate view_coordinate = compute_view_coordinate(tile.face, tile.lod);

    int2 offset = int2(view_coordinate.xy) - int2(tile.xy);
    float2 uv = view_coordinate.uv;

    if      (offset.x < 0) { uv.x = 0.0f; }
    else if (offset.x > 0) { uv.x = 1.0f; }
    if      (offset.y < 0) { uv.y = 0.0f; }
    else if (offset.y > 0) { uv.y = 1.0f; }

#ifdef FRAGMENT
    Coordinate result = {tile.face, tile.lod, tile.xy, uv, float2(0.0f, 0.0f), float2(0.0f, 0.0f)};
#else
    Coordinate result = {tile.face, tile.lod, tile.xy, uv};
#endif
    return result;
}


float2 compute_tile_tree_uv(Coordinate coordinate) {
    Coordinate view_coordinate = compute_view_coordinate(coordinate.face, coordinate.lod);

    int tile_count = int(exp2(float(coordinate.lod)));
    int tree_size  = min(int(terrain_view.tree_size), tile_count);
    int2 tree_xy    = int2(view_coordinate.xy) + int2(round(view_coordinate.uv)) - int2(terrain_view.tree_size / 2, terrain_view.tree_size / 2);
    int2 view_xy    = clamp(tree_xy, int2(0, 0), int2(tile_count - tree_size, tile_count - tree_size));

    return (float2(int2(coordinate.xy) - view_xy) + coordinate.uv) / float(tree_size);
}


TileTreeEntry lookup_tile_tree_entry(Coordinate coordinate) {
    uint2 tree_xy    = coordinate.xy % terrain_view.tree_size;
    uint tree_index = ((coordinate.face * terrain.lod_count +
                       coordinate.lod) * terrain_view.tree_size +
                       tree_xy.x)      * terrain_view.tree_size +
                       tree_xy.y;

    return tile_tree[tree_index];
}

BestLookup lookup_best(Coordinate lookup_coordinate) {
    Coordinate coordinate; float2 tile_tree_uv;

    Coordinate new_coordinate   = lookup_coordinate;
    coordinate_change_lod(new_coordinate , 0u);
    float2 new_tile_tree_uv = new_coordinate.uv;

    while (new_coordinate.lod < terrain.lod_count && !any(new_tile_tree_uv <= float2(0.0f, 0.0f)) && !any(new_tile_tree_uv >= float2(1.0f, 1.0f))) {
        coordinate  = new_coordinate;
        tile_tree_uv = new_tile_tree_uv;

        new_coordinate = lookup_coordinate;
        coordinate_change_lod(new_coordinate, coordinate.lod + 1u);
        new_tile_tree_uv = compute_tile_tree_uv(new_coordinate);
    }

    TileTreeEntry tile_tree_entry = lookup_tile_tree_entry(coordinate);

    coordinate_change_lod(coordinate, tile_tree_entry.atlas_lod);

    AtlasTile tile = {tile_tree_entry.atlas_index, coordinate, 0.0f};
    BestLookup bl = {tile, tile_tree_uv};
    return bl;
}

AtlasTile lookup_tile(Coordinate lookup_coordinate, Blend blend) {
#ifdef TILE_TREE_LOD
    return lookup_best(lookup_coordinate).tile;
#else
    Coordinate coordinate = lookup_coordinate;

    coordinate_change_lod(coordinate, blend.lod);

    TileTreeEntry tile_tree_entry = lookup_tile_tree_entry(coordinate);

    coordinate_change_lod(coordinate, tile_tree_entry.atlas_lod);

    AtlasTile tile = {tile_tree_entry.atlas_index, coordinate, blend.ratio};
    return tile;
#endif
}

#endif // FUNCTIONS_HLSL
