#ifndef TYPES_HLSL
#define TYPES_HLSL

struct Terrain {
    uint lod_count;
    float3 scale;
    float min_height;
    float max_height;
    float height_scale;
    float4x3 world_from_unit; // mat3x4 in WGSL corresponds to float4x3 in HLSL memory layout mostly
    float4x2 unit_from_world_transpose_a; // mat2x4 -> float4x2
    float unit_from_world_transpose_b;
};

struct ViewCoordinate {
    uint2 xy;
    float2 uv;
};

#ifdef HIGH_PRECISION
struct SurfaceApproximation {
    float3 p;
    float3 p_u;
    float3 p_v;
    float3 p_uu;
    float3 p_uv;
    float3 p_vv;
};
#endif

struct TerrainView {
    uint tree_size;
    uint geometry_tile_count;
    float grid_size;
    uint vertices_per_row;
    uint vertices_per_tile;
    float morph_distance;
    float blend_distance;
    float load_distance;
    float subdivision_distance;
    float morph_range;
    float blend_range;
    float precision_distance;
    uint face;
    uint lod;
    ViewCoordinate coordinates[6];
    float height_scale;
    float3 world_position;
    float4 half_spaces[6];
#ifdef HIGH_PRECISION
    SurfaceApproximation surface_approximation[6]; // must be last field of this struct
#endif
};

struct TileCoordinate {
    uint face;
    uint lod;
    uint2 xy;
};

struct GeometryTile {
    uint face;
    uint lod;
    uint2 xy;
    float4 view_distances;
    float4 morph_ratios;
};

struct Coordinate {
    uint face;
    uint lod;
    uint2 xy;
    float2 uv;
#ifdef FRAGMENT
    float2 uv_dx;
    float2 uv_dy;
#endif
};

struct WorldCoordinate {
    float3 position;
    float3 normal;
    float view_distance;
};

struct PrepassState {
    uint tile_count;
    int counter;
    int child_index; // Should be atomic in compute but handled by InterlockedAdd
    int final_index;
};

struct Blend {
    uint lod;
    float ratio;
};

struct TileTreeEntry {
    uint atlas_index;
    uint atlas_lod;
};

struct AtlasTile {
    uint index;
    Coordinate coordinate;
    float blend_ratio;
};

struct BestLookup {
    AtlasTile tile;
    float2 tile_tree_uv;
};

struct AttachmentConfig {
    float texture_size;
    float center_size;
    float scale;
    float offset;
    uint mask;
    uint paddinga;
    uint paddingb;
    uint paddingc;
};

struct IndirectBuffer {
    uint3 workgroup_count;
};

struct TangentSpace {
    float3 tangent_x;
    float3 tangent_y;
    float scale;
};

struct SampleUV {
    float2 uv;
#ifdef FRAGMENT
    float2 dx;
    float2 dy;
#endif
};

#endif // TYPES_HLSL
