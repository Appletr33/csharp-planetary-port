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
    }

    public static class PickingSystem
    {
        public static void Update() { }
    }
}
