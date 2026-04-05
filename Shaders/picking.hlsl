/* Translated from WGSL */
struct PickingData {
    cursor_coords: vec2<float>,
    depth: float,
    stencil: uint,
    world_from_clip: mat4x4<float>,
    cell: vec3<int>,
};

@group(0) @binding(0)
var<storage, read_write> picking_data: PickingData;
@group(0) @binding(1)
var depth_texture: texture_depth_multisampled_2d;
@group(0) @binding(2)
var stencil_texture: texture_multisampled_2d<uint>;

@compute @workgroup_size(1, 1, 1)
void pick(@builtin(global_invocation_id) invocation_id: vec3<uint>) {
    const auto coords = vec2<float>(picking_data.cursor_coords.x, 1.0 - picking_data.cursor_coords.y) * vec2<float>(textureDimensions(depth_texture));
    picking_data.depth = textureLoad(depth_texture, vec2<uint>(coords), 0);
    picking_data.stencil = textureLoad(stencil_texture, vec2<uint>(coords), 0).x;
}