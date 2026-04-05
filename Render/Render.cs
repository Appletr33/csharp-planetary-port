using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace PlanetaryTerrainRenderer.Render
{
    [Flags]
    public enum TilingPrepassPipelineKey : uint
    {
        None = 0,
        RefineTiles = 1 << 0,
        PrepareRoot = 1 << 1,
        PrepareNext = 1 << 2,
        PrepareRender = 1 << 3,
        Spherical = 1 << 4,
        HighPrecision = 1 << 5,
        Morph = 1 << 6,
        Blend = 1 << 7,
        Test1 = 1 << 8,
        Test2 = 1 << 9,
        Test3 = 1 << 10
    }

    public class TilingPrepassItem
    {
        public uint RefineTilesPipelineId;
        public uint PrepareRootPipelineId;
        public uint PrepareNextPipelineId;
        public uint PrepareRenderPipelineId;
    }

    public class GpuTerrain
    {
        // Buffers and parameters
    }

    public class GpuTerrainView
    {
        public uint RefinementCount;
        public uint IndirectBuffer; // GL buffer object for DrawInstancedIndirect
    }

    public class TilingPrepass
    {
        public void Run(GL gl, Dictionary<Tuple<int, int>, TilingPrepassItem> prepassItems, Dictionary<int, GpuTerrain> gpuTerrains, Dictionary<Tuple<int, int>, GpuTerrainView> gpuTerrainViews, bool freeze)
        {
            if (freeze) return;

            foreach (var kvp in prepassItems)
            {
                var terrainId = kvp.Key.Item1;
                var viewId = kvp.Key.Item2;
                var item = kvp.Value;

                var gpuTerrain = gpuTerrains[terrainId];
                var gpuTerrainView = gpuTerrainViews[kvp.Key];

                gl.UseProgram(item.PrepareRootPipelineId);
                gl.DispatchCompute(1, 1, 1);
                gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);

                for (uint i = 0; i < gpuTerrainView.RefinementCount; i++)
                {
                    gl.UseProgram(item.RefineTilesPipelineId);
                    gl.BindBuffer(BufferTargetARB.DispatchIndirectBuffer, gpuTerrainView.IndirectBuffer);
                    gl.DispatchComputeIndirect(0); // Offset 0
                    gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);

                    gl.UseProgram(item.PrepareNextPipelineId);
                    gl.DispatchCompute(1, 1, 1);
                    gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);
                }

                gl.UseProgram(item.PrepareRenderPipelineId);
                gl.BindBuffer(BufferTargetARB.DispatchIndirectBuffer, gpuTerrainView.IndirectBuffer);
                gl.DispatchComputeIndirect(0);
                gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);
            }
        }
    }
}
