using System;
using Silk.NET.Vulkan;
using PlanetaryTerrainRenderer.TerrainData;

namespace PlanetaryTerrainRenderer.Preprocess
{
    public class GpuPreprocessor
    {
        public void Precompute(Vk vk, CommandBuffer cmd, GpuTileAtlas atlas)
        {
            // Compute shader downsampling maps using Vk dispatch commands
            // vk.CmdDispatch(cmd, x, y, z);
        }
    }
}
