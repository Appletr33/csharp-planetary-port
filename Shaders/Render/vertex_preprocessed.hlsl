#define VERTEX

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

#ifndef BINDINGS_HLSL
#define BINDINGS_HLSL


struct Attachments {
    AttachmentConfig height;
    AttachmentConfig config1;
    AttachmentConfig config2;
    AttachmentConfig config3;
    AttachmentConfig config4;
    AttachmentConfig config5;
    AttachmentConfig config6;
    AttachmentConfig config7;
};

struct View {
    float4x4 viewProj;
};

// refine tiles bindings
#ifdef PREPASS
StructuredBuffer<TerrainView> terrain_view : register(t0, space0);
RWStructuredBuffer<float> approximate_height : register(u1, space0);
StructuredBuffer<TileTreeEntry> tile_tree : register(t2, space0);
RWStructuredBuffer<GeometryTile> final_tiles : register(u3, space0);
RWStructuredBuffer<TileCoordinate> temporary_tiles : register(u4, space0);
RWStructuredBuffer<PrepassState> state : register(u5, space0);
RWStructuredBuffer<IndirectBuffer> indirect_buffer : register(u0, space2);
#endif

// terrain view bindings
#ifndef PREPASS
cbuffer ViewBuffer : register(b0, space0) {
    View view;
};
StructuredBuffer<TerrainView> terrain_view_buffer : register(t0, space2);
StructuredBuffer<float> approximate_height : register(t1, space2);
StructuredBuffer<TileTreeEntry> tile_tree : register(t2, space2);
StructuredBuffer<GeometryTile> geometry_tiles : register(t3, space2);

static TerrainView terrain_view = terrain_view_buffer[0];

#endif

// terrain bindings
StructuredBuffer<Terrain> terrain_buffer : register(t0, space1);
cbuffer AttachmentsBuffer : register(b1, space1) {
    Attachments attachments;
};
SamplerState terrain_sampler : register(s2, space1);
Texture2DArray<float> height_attachment : register(t3, space1);
Texture2DArray<float> texture1_attachment : register(t4, space1);
Texture2DArray<float> texture2_attachment : register(t5, space1);
Texture2DArray<float> texture3_attachment : register(t6, space1);
Texture2DArray<float> texture4_attachment : register(t7, space1);
Texture2DArray<float> texture5_attachment : register(t8, space1);
Texture2DArray<float> texture6_attachment : register(t9, space1);
Texture2DArray<float> texture7_attachment : register(t10, space1);

static Terrain terrain = terrain_buffer[0];

#endif // BINDINGS_HLSL

#ifndef FUNCTIONS_HLSL
#define FUNCTIONS_HLSL


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
#ifndef ATTACHMENTS_HLSL
#define ATTACHMENTS_HLSL


#ifdef FRAGMENT
SampleUV compute_sample_uv(AtlasTile tile, AttachmentConfig attachment) {
    float2 uv    = tile.coordinate.uv * attachment.scale + attachment.offset;
    float lod   = log2(attachment.texture_size * max(length(tile.coordinate.uv_dx), length(tile.coordinate.uv_dy)));
    float scale = exp2(max(1.5f * tile.blend_ratio - lod, 0.0f));
    float2 dx    = tile.coordinate.uv_dx * scale;
    float2 dy    = tile.coordinate.uv_dy * scale;

    SampleUV s = {uv, dx, dy};
    return s;
}
#else
SampleUV compute_sample_uv(AtlasTile tile, AttachmentConfig attachment) {
    float2 uv = tile.coordinate.uv * attachment.scale + attachment.offset;

    SampleUV s = {uv};
    return s;
}
#endif

float sample_height(AtlasTile tile) {
    SampleUV uv = compute_sample_uv(tile, attachments.height);

#ifdef FRAGMENT
#ifdef SAMPLE_GRAD
    return terrain.height_scale * height_attachment.SampleGrad(terrain_sampler, float3(uv.uv, tile.index), uv.dx, uv.dy).x;
#else
    return terrain.height_scale * height_attachment.SampleLevel(terrain_sampler, float3(uv.uv, tile.index), tile.blend_ratio).x;
#endif
#else
    return terrain.height_scale * height_attachment.SampleLevel(terrain_sampler, float3(uv.uv, tile.index), 0.0f).x;
#endif
}

bool sample_height_mask(AtlasTile tile) {
    AttachmentConfig attachment = attachments.height;

    if (attachment.mask == 0) { return false; }

    float2 uv         = tile.coordinate.uv * attachment.scale + attachment.offset;
    float4 raw_height = height_attachment.GatherRed(terrain_sampler, float3(uv, tile.index));
    uint4 mask       = asuint(raw_height) & uint4(1, 1, 1, 1);

    return any(mask == uint4(0, 0, 0, 0));
}

#ifdef FRAGMENT
float3 sample_surface_gradient(AtlasTile tile, TangentSpace tangent_space) {
    AttachmentConfig attachment = attachments.height;
    SampleUV uv         = compute_sample_uv(tile, attachment);
    float scale      = max(length(uv.dx), length(uv.dy));
    float step       = 0.5f * scale;

#ifdef SAMPLE_GRAD
    float height   = height_attachment.SampleGrad(terrain_sampler, float3(uv.uv + float2(-step, -step), tile.index), uv.dx, uv.dy).x;
    float height_u = height_attachment.SampleGrad(terrain_sampler, float3(uv.uv + float2( step, -step), tile.index), uv.dx, uv.dy).x;
    float height_v = height_attachment.SampleGrad(terrain_sampler, float3(uv.uv + float2(-step,  step), tile.index), uv.dx, uv.dy).x;
#else
    float height   = height_attachment.SampleLevel(terrain_sampler, float3(uv.uv + float2(-step, -step), tile.index), tile.blend_ratio).x;
    float height_u = height_attachment.SampleLevel(terrain_sampler, float3(uv.uv + float2( step, -step), tile.index), tile.blend_ratio).x;
    float height_v = height_attachment.SampleLevel(terrain_sampler, float3(uv.uv + float2(-step,  step), tile.index), tile.blend_ratio).x;
#endif

    float2 height_duv = float2(height_u - height, height_v - height) / scale;

    float start = 0.5f;
    float end   = 0.05f;
    float lod   = max(0.0f, log2(attachment.texture_size * scale));
    float ratio = saturate((lod - start) / (end - start));

    if (ratio > 0.0f && tile.coordinate.lod == terrain.lod_count - 1) {
        float2 coord       = attachment.texture_size * uv.uv - 0.5f;
        float2 coord_floor = floor(coord);
        float2 center_uv   = (coord_floor + 0.5f) / attachment.texture_size;

        float4 height_TL = height_attachment.GatherRed(terrain_sampler, float3(center_uv, tile.index), int2(-1, -1));
        float4 height_TR = height_attachment.GatherRed(terrain_sampler, float3(center_uv, tile.index), int2( 1, -1));
        float4 height_BL = height_attachment.GatherRed(terrain_sampler, float3(center_uv, tile.index), int2(-1,  1));
        float4 height_BR = height_attachment.GatherRed(terrain_sampler, float3(center_uv, tile.index), int2( 1,  1));
        float4x4 height_matrix = float4x4(height_TL.w, height_TL.z, height_TR.w, height_TR.z,
                                        height_TL.x, height_TL.y, height_TR.x, height_TR.y,
                                        height_BL.w, height_BL.z, height_BR.w, height_BR.z,
                                        height_BL.x, height_BL.y, height_BR.x, height_BR.y);

        float2 t  = saturate(coord - coord_floor);
        float2 A  = float2(1.0f - t.x, t.x);
        float2 B  = float2(1.0f - t.y, t.y);
        float4 X  = 0.25f * float4(A.x, 2.0f * A.x + A.y, A.x + 2.0f * A.y, A.y);
        float4 Y  = 0.25f * float4(B.x, 2.0f * B.x + B.y, B.x + 2.0f * B.y, B.y);
        float4 dX = 0.5f * float4(-A.x, -A.y, A.x, A.y);
        float4 dY = 0.5f * float4(-B.x, -B.y, B.x, B.y);

        float2 upscaled_height_duv = attachment.texture_size * float2(dot(Y, mul(height_matrix, dX)), dot(dY, mul(height_matrix, X)));
        height_duv = lerp(height_duv, upscaled_height_duv, ratio);
    }

    float height_dx = dot(height_duv, tile.coordinate.uv_dx);
    float height_dy = dot(height_duv, tile.coordinate.uv_dy);

//    float height_dx = ddx(height);
//    float height_dy = ddy(height);

    return terrain.height_scale * tangent_space.scale * (height_dx * tangent_space.tangent_x + height_dy * tangent_space.tangent_y);
}
#endif

float compute_slope(float3 world_normal, float3 surface_gradient) {
    float3 normal  = normalize(world_normal - surface_gradient);
    float cos_slope = min(dot(normal, world_normal), 1.0f); // avoid artifacts
    return acos(cos_slope); // slope in radians
}

float3 random_unit_vector(float seed) {
    float angle1 = frac(sin(seed * 12.9898f) * 43758.5453f) * 6.28318f;
    float angle2 = frac(cos(seed * 78.233f) * 43758.5453f) * 3.14159f;

    float x = sin(angle2) * cos(angle1);
    float y = sin(angle2) * sin(angle1);
    float z = cos(angle2);

    return float3(x, y, z);
}

// Todo: clean up relief shading
float relief_shading(WorldCoordinate world_coordinate, float3 surface_gradient) {
    float seed = 6.0f;
    int num_lights = 4;  // Number of lights in the cone
    float theta_max = 0.8f; // Max cone angle (radians), adjust for stronger effect
    float scale = 0.5f * log2(world_coordinate.view_distance);
    float3 normal = normalize(world_coordinate.normal - scale * surface_gradient);

    float total_intensity = 0.0f;

    for (int i = 0; i < num_lights; i++) {
        // Generate a random point in the cone around world_normal
        float3 rand_offset = random_unit_vector(seed + float(i));
        float3 light_dir = normalize(world_coordinate.normal + theta_max * rand_offset);
        total_intensity += max(dot(normal, light_dir), 0.0f);
    }

    return total_intensity / float(num_lights);
}

#endif // ATTACHMENTS_HLSL

struct VS_INPUT {
    uint vertex_index : SV_VertexID;
};

struct VS_OUTPUT {
    float4 clip_position : SV_POSITION;
    [[vk::location(0)]] float2 tile_uv : TEXCOORD0;
    [[vk::location(1)]] uint tile_index : TEXCOORD1;
    [[vk::location(2)]] float view_distance : TEXCOORD2;
    [[vk::location(3)]] float height : TEXCOORD3;
};

struct VertexInfo {
    uint tile_index;
    Coordinate coordinate;
    WorldCoordinate world_coordinate;
    Blend blend;
};

VertexInfo vertex_info(VS_INPUT input) {
    VertexInfo info;
    info.tile_index       = input.vertex_index / terrain_view.vertices_per_tile;
    info.coordinate       = compute_coordinate(input.vertex_index);
    info.world_coordinate = compute_world_coordinate(info.coordinate, info.tile_index, info.coordinate.uv);
    info.blend            = compute_blend(info.world_coordinate.view_distance);
    return info;
}

VS_OUTPUT vertex_output(inout VertexInfo info, float height) {
    VS_OUTPUT output;
    float3 world_pos = apply_height(info.world_coordinate, height);
    output.clip_position = mul(view.viewProj, float4(world_pos, 1.0f));
    output.tile_uv       = info.coordinate.uv;
    output.tile_index    = info.tile_index;
    output.view_distance = info.world_coordinate.view_distance;
    output.height        = height;
    return output;
}

VS_OUTPUT main(VS_INPUT input)
{
    VertexInfo info   = vertex_info(input);

    AtlasTile tile   = lookup_tile(info.coordinate, info.blend);
    float height = sample_height(tile);

    return vertex_output(info, height);
}
