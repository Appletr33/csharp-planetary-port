/* Translated from WGSL */
#define_import_path bevy_terrain::types

struct Terrain {
    lod_count: uint,
    scale: vec3<float>,
    min_height: float,
    max_height: float,
    height_scale: float,
    world_from_unit: mat3x4<float>,
    unit_from_world_transpose_a: mat2x4<float>,
    unit_from_world_transpose_b: float,
};

struct TerrainView {
    tree_size: uint,
    geometry_tile_count: uint,
    grid_size: float,
    vertices_per_row: uint,
    vertices_per_tile: uint,
    morph_distance: float,
    blend_distance: float,
    load_distance: float,
    subdivision_distance: float,
    morph_range: float,
    blend_range: float,
    precision_distance: float,
    face: uint,
    lod: uint,
    coordinates: ViewCoordinate[6],
    height_scale: float,
    world_position: vec3<float>,
    half_spaces: array<vec4<float>, 6>,
#ifdef HIGH_PRECISION
    surface_approximation: SurfaceApproximation[6], // must be last field of this struct
#endif
};

struct TileCoordinate {
    face: uint,
    lod: uint,
    xy: vec2<uint>,
};

struct GeometryTile {
    face: uint,
    lod: uint,
    xy: vec2<uint>,
    view_distances: vec4<float>,
    morph_ratios: vec4<float>,
};

struct Coordinate {
    face: uint,
    lod: uint,
    xy: vec2<uint>,
    uv: vec2<float>,
#ifdef FRAGMENT
    uv_dx: vec2<float>,
    uv_dy: vec2<float>,
#endif
};

struct WorldCoordinate {
    position: vec3<float>,
    normal: vec3<float>,
    view_distance: float,
};

struct ViewCoordinate {
    xy: vec2<uint>,
    uv: vec2<float>,
};

struct PrepassState {
    tile_count: uint,
    counter: int,
    child_index: atomic<int>,
    final_index: atomic<int>,
};

struct Blend {
    lod: uint,
    ratio: float,
};

struct TileTreeEntry {
    atlas_index: uint,
    atlas_lod: uint,
};

// A tile inside the tile atlas, looked up based on the view of a tile tree.
struct AtlasTile {
    index: uint,
    coordinate: Coordinate,
    blend_ratio: float,
};

#ifdef HIGH_PRECISION
struct SurfaceApproximation {
    p: vec3<float>,
    p_u: vec3<float>,
    p_v: vec3<float>,
    p_uu: vec3<float>,
    p_uv: vec3<float>,
    p_vv: vec3<float>,
};
#endif

struct BestLookup {
    tile: AtlasTile,
    tile_tree_uv: vec2<float>,
};

struct AttachmentConfig {
    texture_size: float,
    center_size: float,
    scale: float,
    offset: float,
    mask: uint,
    paddinga: uint,
    paddingb: uint,
    paddingc: uint,
};


struct IndirectBuffer {
    workgroup_count: vec3<uint>,
};

struct TangentSpace {
    tangent_x: vec3<float>,
    tangent_y: vec3<float>,
    scale: float,
};

struct SampleUV {
    uv: vec2<float>,
#ifdef FRAGMENT
    dx: vec2<float>,
    dy: vec2<float>,
#endif
};
