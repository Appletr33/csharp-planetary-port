using System;
using Stride.Core.Mathematics;

namespace PlanetaryTerrainRenderer.Picking
{
    public struct GpuPickingData
    {
        public Vector2 CursorCoords;
        public float Depth;
        public uint Stencil;
        public Matrix WorldFromClip;
        public Int3 Cell;
    }

    public class PickingData
    {
        public Vector2 CursorCoords;
        public Int3 Cell;
        public Vector3? Translation;
        public Matrix WorldFromClip;

        // Using a generalized buffer reference ID for Stride compute
        public int BufferId;
    }

    public static class PickingSystem
    {
        public static void UpdateSystem(PickingData data, Vector2 cursorPosition, Vector2 windowSize, Matrix worldFromClip, Int3 cell)
        {
            var cursorCoords = new Vector2(cursorPosition.X, windowSize.Y - cursorPosition.Y) / windowSize;

            // Map to struct for compute shader updates
            var gpuData = new GpuPickingData
            {
                CursorCoords = cursorCoords,
                Depth = 0.0f,
                Stencil = 255,
                WorldFromClip = worldFromClip,
                Cell = cell
            };

            // In Stride, you would push this data into a structured buffer bound to the context.
        }

        public static void Readback(PickingData data, GpuPickingData gpuData)
        {
            var ndcCoords = new Vector3((2.0f * gpuData.CursorCoords.X) - 1.0f, (2.0f * gpuData.CursorCoords.Y) - 1.0f, gpuData.Depth);

            data.CursorCoords = gpuData.CursorCoords;
            data.Cell = gpuData.Cell;

            if (gpuData.Depth > 0.0f)
            {
                var projected = Vector3.TransformCoordinate(ndcCoords, gpuData.WorldFromClip);
                data.Translation = projected;
            }
            else
            {
                data.Translation = null;
            }

            data.WorldFromClip = gpuData.WorldFromClip;
        }
    }
}
