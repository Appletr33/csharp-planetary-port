using System;
using Silk.NET.Vulkan;

namespace PlanetaryTerrainRenderer.Render
{
    public unsafe class TerrainMaterial
    {
        private Vk _vk;
        private Device _device;
        public Pipeline GraphicsPipeline { get; private set; }
        public PipelineLayout PipelineLayout { get; private set; }
        
        public TerrainMaterial(Vk vk, Device device, Pipeline pipeline, PipelineLayout layout)
        {
            _vk = vk;
            _device = device;
            GraphicsPipeline = pipeline;
            PipelineLayout = layout;
        }

        public void Bind(CommandBuffer cmd)
        {
            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, GraphicsPipeline);
        }
    }
}
