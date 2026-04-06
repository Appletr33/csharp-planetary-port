#ifndef DEBUG_HLSL
#define DEBUG_HLSL

#include "types.hlsl"
#include "bindings.hlsl"
#include "functions.hlsl"

float4 index_color(uint index) {
    float4 COLOR_ARRAY[6] = {
        float4(1.0f, 0.0f, 0.0f, 1.0f),
        float4(0.0f, 1.0f, 0.0f, 1.0f),
        float4(0.0f, 0.0f, 1.0f, 1.0f),
        float4(1.0f, 1.0f, 0.0f, 1.0f),
        float4(1.0f, 0.0f, 1.0f, 1.0f),
        float4(0.0f, 1.0f, 1.0f, 1.0f)
    };

    return lerp(COLOR_ARRAY[index % 6u], float4(0.6f, 0.6f, 0.6f, 1.0f), 0.2f);
}

float tile_tree_outlines(float2 uv) {
    float thickness = 0.015f;
    float2 inside = step(float2(thickness, thickness), uv) * step(uv, float2(1.0f - thickness, 1.0f - thickness));

    return 1.0f - inside.x * inside.y;
}

float4 checker_color(Coordinate coordinate, float ratio) {
    float4 color        = index_color(coordinate.lod);
    float4 parent_color = index_color(coordinate.lod - 1);
    color            = ((coordinate.xy.x + coordinate.xy.y) % 2u == 0u) ? lerp(color, float4(0.0f, 0.0f, 0.0f, 0.0f), 0.5f) : color;
    parent_color     = (((coordinate.xy.x >> 1) + (coordinate.xy.y >> 1)) % 2u == 0u) ? lerp(parent_color, float4(0.0f, 0.0f, 0.0f, 0.0f), 0.5f) : parent_color;

    return lerp(color, parent_color, ratio);
}

float4 show_data_lod(Blend blend, AtlasTile tile) {
#ifdef TILE_TREE_LOD
    float ratio = 0.0f;
#else
    float ratio = (blend.lod == tile.coordinate.lod) ? blend.ratio : 0.0f;
#endif

    float4 color = checker_color(tile.coordinate, ratio);

    if (ratio > 0.95f && blend.lod == tile.coordinate.lod) {
        color = lerp(color, float4(0.0f, 0.0f, 0.0f, 1.0f), 0.8f);
    }

// #ifdef SPHERICAL
//     color = lerp(color, index_color(tile.coordinate.face), 0.3f);
// #endif

    return color;
}

float4 show_geometry_lod(Coordinate coordinate, uint tile_index) {
    float2 tile_uv       = coordinate.uv;
    GeometryTile tile          = geometry_tiles[tile_index];
    float view_distance = lerp(lerp(tile.view_distances.x, tile.view_distances.y, tile_uv.x),
                            lerp(tile.view_distances.z, tile.view_distances.w, tile_uv.x), tile_uv.y);

    float target_lod = log2(terrain_view.morph_distance / view_distance);
    uint lod        = coordinate.lod;

#ifdef MORPH
    float ratio = (lod == 0) ? 0.0f : saturate(1.0f - (target_lod - float(lod)) / terrain_view.morph_range);
#else
    float ratio = 0.0f;
#endif

    float4 color = checker_color(coordinate, ratio);

    TileCoordinate tile_coordinate = {tile.face, tile.lod, tile.xy};

    if (distance(coordinate.uv, compute_subdivision_coordinate(tile_coordinate).uv) < 0.1f) {
        color = lerp(index_color(coordinate.lod + 1), float4(0.0f, 0.0f, 0.0f, 1.0f), 0.7f);
    }

    if (frac(target_lod) < 0.01f && target_lod >= 1.0f) {
        color = lerp(color, float4(0.0f, 0.0f, 0.0f, 1.0f), 0.8f);
    }

#ifdef SPHERICAL
    color = lerp(color, index_color(coordinate.face), 0.3f);
#endif

    if (max(0.0f, target_lod) < float(coordinate.lod) - 1.0f + terrain_view.morph_range) {
        // The view_distance and morph range are not sufficient.
        // The same tile overlapps two morph zones.
        // -> increase morph distance
        color = float4(1.0f, 0.0f, 0.0f, 1.0f);
    }
    if (floor(target_lod) > float(coordinate.lod)) {
        // The view_distance and morph range are not sufficient.
        // The tile does have an insuffient LOD.
        // -> increase morph tolerance
        color = float4(0.0f, 1.0f, 0.0f, 1.0f);
    }

    return color;
}

float4 show_tile_tree(Coordinate coordinate, WorldCoordinate world_coordinate) {
    float target_lod     = log2(terrain_view.load_distance / world_coordinate.view_distance);

    BestLookup best_lookup = lookup_best(coordinate);

    float4 color = checker_color(best_lookup.tile.coordinate, 0.0f);
    color     = lerp(color, float4(0.1f, 0.1f, 0.1f, 1.0f), tile_tree_outlines(best_lookup.tile_tree_uv));

    if (frac(target_lod) < 0.01f && target_lod >= 1.0f) {
        color = lerp(index_color(uint(target_lod)), float4(0.0f, 0.0f, 0.0f, 1.0f), 0.8f);
    }

    return color;
}

float4 show_pixels(AtlasTile tile) {
    float pixel_size = 1.0f;
    float2 pixel_coordinate = tile.coordinate.uv * float(attachments.height.center_size) / pixel_size;

    bool is_even = (uint(pixel_coordinate.x) + uint(pixel_coordinate.y)) % 2u == 0u;

    if (is_even) { return float4(0.5f, 0.5f, 0.5f, 1.0f); }
    else {         return float4(0.1f, 0.1f, 0.1f, 1.0f); }
}

#endif // DEBUG_HLSL
