using System.Collections.Generic;
using Silk.NET.Vulkan;
using PlanetaryTerrainRenderer.TerrainData;

namespace PlanetaryTerrainRenderer
{
    public unsafe class TerrainManager : System.IDisposable
    {
        private Vk _vk;
        private Device _device;
        private PhysicalDevice _physicalDevice;
        private List<TerrainInstance> _terrains;

        public IReadOnlyList<TerrainInstance> Terrains => _terrains;
        // Native GPU Buffer validation tracking
        public Render.GpuBuffer<float> DebugVertexBuffer;
        public Render.GpuBuffer<uint> DebugIndexBuffer;

        public TerrainManager(Vk vk, Device device, PhysicalDevice physicalDevice)
        {
            _vk = vk;
            _device = device;
            _physicalDevice = physicalDevice;
            _terrains = new List<TerrainInstance>();
        }

        public void AddTerrain(TerrainInstance terrain)
        {
            _terrains.Add(terrain);
        }

        public void Update()
        {
            // Compute Requests & Tile Tree Management per terrain
            foreach (var terrain in _terrains)
            {
                if (terrain.TileTree != null)
                {
                    terrain.TileTree.ComputeRequests();
                }
            }
        }

        public void ExtractAndPrepare()
        {
            // Compute Noise Topology Arrays
            if (DebugVertexBuffer == null)
            {
                uint res = 256;
                float[] verts = new float[res * res * 3];
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float u = (float)x / (res - 1);
                        float v = (float)y / (res - 1);
                        
                        // Map 2D grid to 3D sphere coordinates (Mercator-ish)
                        float theta = u * 2.0f * (float)System.Math.PI;
                        float phi = (v - 0.5f) * (float)System.Math.PI;

                        float radius = 100.0f; // Base planet radius

                        float dirX = (float)(System.Math.Cos(phi) * System.Math.Cos(theta));
                        float dirY = (float)System.Math.Sin(phi);
                        float dirZ = (float)(System.Math.Cos(phi) * System.Math.Sin(theta));

                        // Generate 3D noise based on direction for seamless wrapping
                        float h = PlanetaryTerrainRenderer.Math.SimplexNoise.Fractal3D(dirX * 2.0f, dirY * 2.0f, dirZ * 2.0f, 6);

                        // Remap noise from [-1, 1] to [0, 1], then scale
                        float elevation = (h * 0.5f + 0.5f) * 15.0f; // 15 units high mountains

                        // Flatten "oceans"
                        if (elevation < 7.0f) elevation = 7.0f;

                        verts[(y * res + x) * 3 + 0] = dirX * (radius + elevation);
                        verts[(y * res + x) * 3 + 1] = dirY * (radius + elevation);
                        verts[(y * res + x) * 3 + 2] = dirZ * (radius + elevation);
                    }
                }
                
                DebugVertexBuffer = new Render.GpuBuffer<float>(_vk, _device, _physicalDevice, BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, (uint)verts.Length);
                DebugVertexBuffer.SetData(verts);
                DebugVertexBuffer.Update();

                // Generate index buffer for solid triangle mesh
                uint[] indices = new uint[(res - 1) * (res - 1) * 6];
                int i = 0;
                for (uint y = 0; y < res - 1; y++)
                {
                    for (uint x = 0; x < res - 1; x++)
                    {
                        uint i0 = y * res + x;
                        uint i1 = i0 + 1;
                        uint i2 = i0 + res;
                        uint i3 = i2 + 1;

                        indices[i++] = i0;
                        indices[i++] = i2;
                        indices[i++] = i1;

                        indices[i++] = i1;
                        indices[i++] = i2;
                        indices[i++] = i3;
                    }
                }

                DebugIndexBuffer = new Render.GpuBuffer<uint>(_vk, _device, _physicalDevice, BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, (uint)indices.Length);
                DebugIndexBuffer.SetData(indices);
                DebugIndexBuffer.Update();
            }
        }

        public void QueueComputePass(CommandBuffer commandBuffer)
        {
            // Queue tiling prepass
            // _vk.CmdDispatch(commandBuffer, ...);
        }

        public void RenderOpaquePass(CommandBuffer commandBuffer, PipelineLayout pipelineLayout)
        {
            // Bind terrains and issue their vkCmdDrawIndirect calls
            foreach (var terrain in _terrains)
            {
                if (terrain.Material != null)
                {
                    // In Vulkan, material represents Bound Descriptor Sets and Pipeline state
                    // terrain.Material.Bind(commandBuffer);
                }

                if (terrain.GpuView != null)
                {
                    // Bind the indirect buffer containing the primitive offset commands
                    // _vk.CmdDrawIndirect(commandBuffer, terrain.GpuView.IndirectBuffer.Handle, 0, 1, (uint)sizeof(DrawIndirectCommand));
                }
            }
        }

        public void Dispose()
        {
            if (DebugVertexBuffer != null)
            {
                DebugVertexBuffer.Dispose();
                DebugVertexBuffer = null;
            }

            if (DebugIndexBuffer != null)
            {
                DebugIndexBuffer.Dispose();
                DebugIndexBuffer = null;
            }
        }
    }

    public class TerrainInstance
    {
        public TerrainConfig Config { get; set; }
        public TerrainSettings Settings { get; set; }
        public TileTree TileTree { get; set; }
        public TileAtlas TileAtlas { get; set; }
        public TerrainData.GpuTileAtlas GpuTileAtlas { get; set; }
        public Render.GpuTerrainView GpuView { get; set; }
        public Render.TerrainMaterial Material { get; set; } // Will need Vulkan refactoring next

        public TerrainInstance(TerrainConfig config, TerrainSettings settings, TileTree tileTree, TileAtlas tileAtlas)
        {
            Config = config;
            Settings = settings;
            TileTree = tileTree;
            TileAtlas = tileAtlas;
        }
    }
}
