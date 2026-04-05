using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using PlanetaryTerrainRenderer.Math;
using PlanetaryTerrainRenderer.TerrainData;

namespace PlanetaryTerrainRenderer.Preprocess
{
    public class AtlasTileAttachment
    {
        public AttachmentLabel Label { get; set; } = AttachmentLabel.Height;
    }

    public enum PreprocessTaskType
    {
        Split,
        Stitch,
        Downsample,
        Save,
        Barrier
    }

    public class PreprocessTask
    {
        public AtlasTileAttachment Tile;
        public PreprocessTaskType TaskType;

        public static PreprocessTask Barrier() => new PreprocessTask { TaskType = PreprocessTaskType.Barrier };
    }

    public class GpuPreprocessor
    {
        public Queue<PreprocessTask> ReadyTasks { get; } = new Queue<PreprocessTask>();

        public void ProcessTasks(GL gl)
        {
            while (ReadyTasks.Count > 0)
            {
                var task = ReadyTasks.Dequeue();

                if (task.TaskType == PreprocessTaskType.Barrier)
                {
                    gl.MemoryBarrier(MemoryBarrierMask.TextureFetchBarrierBit | MemoryBarrierMask.ShaderImageAccessBarrierBit);
                    continue;
                }

                // Dispatch appropriate compute shaders based on TaskType
                // e.g., if task.TaskType == PreprocessTaskType.Downsample
                // gl.UseProgram(downsampleShaderId);
                // gl.DispatchCompute(groupsX, groupsY, 1);
            }
        }
    }
}
