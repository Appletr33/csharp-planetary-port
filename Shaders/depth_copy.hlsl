/* Translated from WGSL */
#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput

@group(0) @binding(0)
var depth_texture: texture_depth_multisampled_2d;

@fragment
void fragment(in: FullscreenVertexOutput) -> @builtin(frag_depth) float {
    return textureLoad(depth_texture, vec2<uint>(in.uv * vec2<float>(textureDimensions(depth_texture))), 0);
}