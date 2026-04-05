using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;

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
        public int RefineTilesPipelineId;
        public int PrepareRootPipelineId;
        public int PrepareNextPipelineId;
        public int PrepareRenderPipelineId;
    }

    public class TerrainTilingPrepassPipelines
    {
        // Layouts and shaders
    }

    public class GpuTerrain
    {
        // Terrain Bind group
    }

    public class TerrainMaterialPlugin
    {
    }

    public class GpuTerrainView
    {
        public uint RefinementCount;
        public Stride.Graphics.Buffer IndirectBuffer; // Maps to ID3D11Buffer for DrawInstancedIndirect
        // Bind groups
    }

    public class TerrainViewBindGroup
    {
    }

    public class TerrainPass
    {
    }

    public class TilingPrepass
    {
        public void Run(CommandList commandList, Dictionary<Tuple<int, int>, TilingPrepassItem> prepassItems, Dictionary<int, GpuTerrain> gpuTerrains, Dictionary<Tuple<int, int>, GpuTerrainView> gpuTerrainViews, bool freeze)
        {
            if (freeze) return;

            foreach (var kvp in prepassItems)
            {
                var terrainId = kvp.Key.Item1;
                var viewId = kvp.Key.Item2;
                var item = kvp.Value;

                var gpuTerrain = gpuTerrains[terrainId];
                var gpuTerrainView = gpuTerrainViews[kvp.Key];

                // Equivalent of dispatch_workgroups(1,1,1) for root
                // commandList.Dispatch(1, 1, 1);

                for (uint i = 0; i < gpuTerrainView.RefinementCount; i++)
                {
                    // commandList.DispatchIndirect(...)
                    // commandList.Dispatch(1, 1, 1);
                }

                // commandList.DispatchIndirect(...)
                // commandList.Dispatch(1, 1, 1);
            }
        }
    }
}
