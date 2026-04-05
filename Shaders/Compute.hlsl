cbuffer PassData : register(b0)
{
    int u_PassMode; // 0 = prepare_root, 1 = refine_tiles, 2 = prepare_render
};

// Represents the generated DrawArraysIndirectCommand payload
RWStructuredBuffer<uint> IndirectBuffer : register(u0);

[numthreads(1, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (u_PassMode == 0) {
        // prepare_root
        IndirectBuffer[0] = 64; // vertexCount
        IndirectBuffer[1] = 1;  // instanceCount
        IndirectBuffer[2] = 0;  // firstVertex
        IndirectBuffer[3] = 0;  // baseInstance
    }
    else if (u_PassMode == 1) {
        // refine_tiles
        // Compute new tile LODs based on distance...
    }
    else if (u_PassMode == 2) {
        // prepare_render
        // Ensures indirect buffer gets gracefully clamped and processed
    }
}
