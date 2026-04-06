#ifndef BINDINGS_HLSL
#define BINDINGS_HLSL

#include "types.hlsl"

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
